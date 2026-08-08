using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Shared.Repositories;
using Vulgata.Web.Data;
using Vulgata.Web.Validators;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.ReleaseGate;

internal sealed record PreparedRepositoryIdentity(string Alias, string Ref, string Commit);

internal sealed record Story115EnvironmentVariable(
    string Name,
    bool IsSecret,
    string Validation,
    string Purpose);

internal sealed record Story115FinalEnvironment(
    string PostgreSqlAdministratorConnection,
    Uri AccountRepositoryUrl,
    Uri ChannelRepositoryUrl,
    string CandidateCommit,
    string CandidateTree,
    string ProductionManifestSha256);

internal sealed record Story115FinalRunPlan(
    Story115FinalEnvironment Environment,
    Story115RunIdentity Identity,
    IReadOnlyList<int> Ports,
    IReadOnlyList<string> OrderedStages,
    string RunDirectory,
    string KeyRingDirectory,
    string AppEnvironmentFile,
    IReadOnlyList<Story115ExternalCommand> Commands,
    Story115PortReservation PortReservation);

internal sealed record Story115ExternalCommand(
    string Name,
    string FileName,
    IReadOnlyList<string> Arguments);

internal sealed record Story115ProcessResult(
    Story115ExternalCommand Command,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    DateTimeOffset ObservedAtUtc,
    string LogSha256)
{
    internal Story115CommandEvidence ToEvidence() =>
        new(Command.Name, ExitCode, ObservedAtUtc, LogSha256, null);
}

internal sealed class Story115ProcessExecutor
{
    private const int RetainedCharacterLimit = 64 * 1024;

    internal async Task<Story115ProcessResult> ExecuteAsync(
        Story115ExternalCommand command,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(environment);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(30))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var startInfo = new ProcessStartInfo(command.FileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach ((string name, string value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException($"Failed to start {command.Name}.");
        Task<string> standardOutputTask = ReadBoundedAsync(process.StandardOutput);
        Task<string> standardErrorTask = ReadBoundedAsync(process.StandardError);
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (
            timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKillProcessTree(process);
            await process.WaitForExitAsync(CancellationToken.None);
            throw new TimeoutException($"Command {command.Name} exceeded its bounded timeout.");
        }
        catch
        {
            TryKillProcessTree(process);
            throw;
        }

        string standardOutput = await standardOutputTask;
        string standardError = await standardErrorTask;
        string logSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            standardOutput + "\n" + standardError))).ToLowerInvariant();
        return new(
            command,
            process.ExitCode,
            standardOutput,
            standardError,
            DateTimeOffset.UtcNow,
            logSha256);
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader)
    {
        var retained = new StringBuilder(RetainedCharacterLimit);
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
        {
            int remaining = RetainedCharacterLimit - retained.Length;
            if (remaining > 0)
            {
                retained.Append(buffer, 0, Math.Min(read, remaining));
            }
        }

        return retained.ToString();
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the bounded check and the kill request.
        }
    }
}

internal sealed record Story115ProductRunState(
    Guid SystemId,
    Guid AccountRepositoryId,
    Guid ChannelRepositoryId,
    Guid AccountScanRunId,
    Guid ChannelScanRunId,
    Guid AccountScanExecutionId,
    Guid ChannelScanExecutionId,
    RepositoryAccessScope Scope,
    ResolvedPreparedPass2Flow PreparedPass2Flow,
    string OwnerEmail,
    string OwnerPassword,
    string NegativeEmail,
    string NegativePassword);

internal static class Story115ProductOrchestrator
{
    internal static IReadOnlyList<string> Pass2PayloadSlots { get; } =
        Array.AsReadOnly(
        [
            nameof(Pass2BusinessLogicJobPayload.PreparedFlowSchemaIdentity),
            nameof(Pass2BusinessLogicJobPayload.ManifestId),
            nameof(Pass2BusinessLogicJobPayload.FlowId),
            nameof(Pass2BusinessLogicJobPayload.SystemId),
            nameof(Pass2BusinessLogicJobPayload.RepositoryId),
            nameof(Pass2BusinessLogicJobPayload.TraceEvidence),
            nameof(Pass2BusinessLogicJobPayload.CodeLogicDocumentVersions),
        ]);

    internal static IReadOnlyList<string> RequiredProductCalls { get; } =
        Array.AsReadOnly(
        [
            "UserManager.CreateAsync",
            "IAdministratorRoleCoordinator.AssignInitialRoleAsync",
            "ISystemRepository.AddAsync",
            "ISystemOwnershipCoordinator.AssignOwnerAsync",
            "IRepositoryManagementCoordinator.CreateAsync",
            "ILlmProviderManagementCoordinator.CreateAsync",
            "ISystemLlmProviderOverrideCoordinator.UpsertAsync",
            "IRepositoryProfilingCoordinator.StartSystemRepositoryAsync",
            "SequentialScanRunner.StartAsync",
            "SequentialScanRunner.RunPass1Async",
            "ExactCrossRepositoryResolutionCoordinator.ResolveAfterPass1Async",
            "IPreparedDemoPass2FlowPlanner.PlanAsync",
            "SequentialScanRunner.RunPreparedPass2Async",
        ]);

    internal static async Task<Story115ProductRunState> ExecuteAsync(
        IServiceProvider services,
        Story115FinalEnvironment environment,
        Story115RunIdentity identity,
        IReadOnlyList<int> ports,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(ports);
        if (ports.Count != 3)
        {
            throw new InvalidOperationException("The prepared provider requires exactly three ports.");
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        await services.GetRequiredService<RoleSeeder>().SeedAsync();
        IAdministratorRoleCoordinator roleCoordinator =
            services.GetRequiredService<IAdministratorRoleCoordinator>();
        string password = CreatePassword();
        ApplicationUser administrator = await CreateUserAsync(
            userManager,
            $"story115-admin-{identity.Nonce}@example.invalid",
            password,
            cancellationToken);
        RequireIdentity(await roleCoordinator.AssignInitialRoleAsync(
            administrator,
            cancellationToken));

        string ownerEmail = $"story115-owner-{identity.Nonce}@example.invalid";
        string ownerPassword = CreatePassword();
        ApplicationUser owner = await CreateUserAsync(
            userManager,
            ownerEmail,
            ownerPassword,
            cancellationToken);
        RequireIdentity(await roleCoordinator.AssignInitialRoleAsync(owner, cancellationToken));

        string negativeEmail = $"story115-negative-{identity.Nonce}@example.invalid";
        string negativePassword = CreatePassword();
        ApplicationUser negative = await CreateUserAsync(
            userManager,
            negativeEmail,
            negativePassword,
            cancellationToken);
        RequireIdentity(await roleCoordinator.AssignInitialRoleAsync(negative, cancellationToken));

        ISystemRepository systemRepository = services.GetRequiredService<ISystemRepository>();
        var system = new SystemEntity(
            $"Story 11.5 {identity.Nonce}",
            "Story 11.5 final release gate",
            "Prepared two-Repository demo boundary",
            DateTimeOffset.UtcNow);
        await systemRepository.AddAsync(system, cancellationToken);
        await systemRepository.SaveChangesAsync(cancellationToken);
        SystemOwnershipAssignmentResult ownership = await services
            .GetRequiredService<ISystemOwnershipCoordinator>()
            .AssignOwnerAsync(system.Id, owner.Id, cancellationToken);
        if (ownership.Outcome != SystemOwnershipAssignmentOutcome.Assigned)
        {
            throw new InvalidOperationException($"System ownership provisioning failed: {ownership.Outcome}.");
        }

        var ownerContext = new Story115UserContext(owner.Id, IsAdministrator: false);
        IRepositoryManagementCoordinator repositoryCoordinator =
            services.GetRequiredService<IRepositoryManagementCoordinator>();
        var account = await repositoryCoordinator.CreateAsync(
            system.Id,
            new CreateRepositoryRequest
            {
                Name = Story115ReleaseHarness.Contract.AccountRepository.Alias,
                GitUrl = environment.AccountRepositoryUrl.AbsoluteUri,
            },
            ownerContext,
            cancellationToken);
        var channel = await repositoryCoordinator.CreateAsync(
            system.Id,
            new CreateRepositoryRequest
            {
                Name = Story115ReleaseHarness.Contract.ChannelRepository.Alias,
                GitUrl = environment.ChannelRepositoryUrl.AbsoluteUri,
            },
            ownerContext,
            cancellationToken);
        if (account.Outcome != RepositoryMutationOutcome.Success || account.Value is null ||
            channel.Outcome != RepositoryMutationOutcome.Success || channel.Value is null)
        {
            throw new InvalidOperationException(
                $"Repository provisioning failed: {account.Outcome}/{channel.Outcome}.");
        }

        ILlmProviderManagementCoordinator providerCoordinator =
            services.GetRequiredService<ILlmProviderManagementCoordinator>();
        var workerProvider = await providerCoordinator.CreateAsync(
            ProviderRequest(identity, "worker", ports[0], AgentType.Worker),
            cancellationToken);
        var chatProvider = await providerCoordinator.CreateAsync(
            ProviderRequest(identity, "chat", ports[1], AgentType.Chat),
            cancellationToken);
        if (workerProvider.Outcome != LlmProviderMutationOutcome.Success ||
            workerProvider.Value is null ||
            chatProvider.Outcome != LlmProviderMutationOutcome.Success ||
            chatProvider.Value is null)
        {
            throw new InvalidOperationException(
                $"Provider provisioning failed: {workerProvider.Outcome}/{chatProvider.Outcome}.");
        }

        ISystemLlmProviderOverrideCoordinator overrideCoordinator =
            services.GetRequiredService<ISystemLlmProviderOverrideCoordinator>();
        await RequireOverrideAsync(overrideCoordinator.UpsertAsync(
            system.Id,
            null,
            OverrideRequest(system.Id, workerProvider.Value.Id, AgentType.Worker),
            ownerContext,
            cancellationToken));
        await RequireOverrideAsync(overrideCoordinator.UpsertAsync(
            system.Id,
            null,
            OverrideRequest(system.Id, chatProvider.Value.Id, AgentType.Chat),
            ownerContext,
            cancellationToken));

        IRepositoryProfilingCoordinator profiling =
            services.GetRequiredService<IRepositoryProfilingCoordinator>();
        RepositoryProfilingResult accountProfile = await profiling.StartSystemRepositoryAsync(
            system.Id,
            account.Value.Id,
            ownerContext,
            cancellationToken);
        RepositoryProfilingResult channelProfile = await profiling.StartSystemRepositoryAsync(
            system.Id,
            channel.Value.Id,
            ownerContext,
            cancellationToken);
        if (accountProfile.Outcome != RepositoryProfilingOutcome.Success || accountProfile.Value is null ||
            channelProfile.Outcome != RepositoryProfilingOutcome.Success || channelProfile.Value is null)
        {
            throw new InvalidOperationException(
                $"Repository profiling failed: {accountProfile.Outcome}/{channelProfile.Outcome}.");
        }

        var verifier = ActivatorUtilities.CreateInstance<RepositoryAuthorizationVerifier>(
            services,
            ownerContext);
        var scope = new RepositoryAccessScope(
            [account.Value.Id, channel.Value.Id],
            verifier);
        SequentialScanRunner runner = services.GetRequiredService<SequentialScanRunner>();
        Guid accountExecution = await runner.StartAsync(
            account.Value.Id,
            accountProfile.Value.Id,
            system.Id,
            cancellationToken);
        Guid channelExecution = await runner.StartAsync(
            channel.Value.Id,
            channelProfile.Value.Id,
            system.Id,
            cancellationToken);
        await runner.RunPass1Async(accountExecution, scope, cancellationToken);
        await runner.RunPass1Async(channelExecution, scope, cancellationToken);

        ExactResolutionProfile profile = services
            .GetRequiredService<ExactResolutionProfileCatalog>()
            .Load();
        ExactResolutionRuntimeRequest runtimeRequest = ExactResolutionRuntimeRequest.Create(
            system.Id,
            profile,
            [
                new(
                    Story115ReleaseHarness.Contract.AccountRepository.Alias,
                    profile.Repositories.Single(value => value.Alias ==
                        Story115ReleaseHarness.Contract.AccountRepository.Alias).StableGitIdentity,
                    account.Value.Id,
                    accountProfile.Value.Id,
                    accountExecution),
                new(
                    Story115ReleaseHarness.Contract.ChannelRepository.Alias,
                    profile.Repositories.Single(value => value.Alias ==
                        Story115ReleaseHarness.Contract.ChannelRepository.Alias).StableGitIdentity,
                    channel.Value.Id,
                    channelProfile.Value.Id,
                    channelExecution),
            ]);
        ExactCrossRepositoryResolutionCoordinator exactCoordinator =
            ActivatorUtilities.CreateInstance<ExactCrossRepositoryResolutionCoordinator>(
                services,
                ownerContext);
        ExactResolutionOutcome exact = await exactCoordinator.ResolveAfterPass1Async(
            runtimeRequest,
            scope,
            cancellationToken);
        if (exact.Kind is not (ExactResolutionOutcomeKind.Applied or
            ExactResolutionOutcomeKind.AlreadyApplied or
            ExactResolutionOutcomeKind.UncertaintyRecorded or
            ExactResolutionOutcomeKind.AlreadyRecorded))
        {
            throw new InvalidOperationException($"Exact resolution failed: {exact.Kind}/{exact.Code}.");
        }

        ResolvedPreparedPass2Flow? resolved = await services
            .GetRequiredService<IPreparedDemoPass2FlowPlanner>()
            .PlanAsync(profile, runtimeRequest, scope, cancellationToken);
        if (resolved is null || resolved.RuntimeBinding is null)
        {
            throw new InvalidOperationException("The runtime-prepared Pass 2 flow is unavailable.");
        }

        await runner.RunPreparedPass2Async(
            accountExecution,
            resolved,
            scope,
            cancellationToken);
        return new(
            system.Id,
            account.Value.Id,
            channel.Value.Id,
            accountProfile.Value.Id,
            channelProfile.Value.Id,
            accountExecution,
            channelExecution,
            scope,
            resolved,
            ownerEmail,
            ownerPassword,
            negativeEmail,
            negativePassword);
    }

    private static CreateLlmProviderRequest ProviderRequest(
        Story115RunIdentity identity,
        string suffix,
        int port,
        AgentType agentType) =>
        new()
        {
            Name = $"story115-{identity.Nonce}-{suffix}",
            BaseEndpointUrl = $"http://127.0.0.1:{port}/v1",
            ApiKey = "story115-loopback-only",
            SupportedApiTypes = ApiType.ChatCompletions,
            DefaultAgentType = agentType,
        };

    private static UpsertSystemLlmProviderOverrideRequest OverrideRequest(
        Guid systemId,
        Guid providerId,
        AgentType agentType) =>
        new()
        {
            SystemId = systemId,
            LlmProviderId = providerId,
            AgentType = agentType,
        };

    private static async Task RequireOverrideAsync(
        Task<SystemLlmProviderOverrideMutationResult<
            SystemLlmProviderOverrideSummaryDto>> operation)
    {
        var result = await operation;
        if (result.Outcome != SystemLlmProviderOverrideMutationOutcome.Success)
        {
            throw new InvalidOperationException($"Provider override failed: {result.Outcome}.");
        }
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Story 11.5",
        };
        RequireIdentity(await userManager.CreateAsync(user, password));
        return user;
    }

    private static void RequireIdentity(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Identity provisioning failed: " +
                string.Join(',', result.Errors.Select(error => error.Code)) + ".");
        }
    }

    private static string CreatePassword() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(18)) + "aA1!";

    private sealed record Story115UserContext(string UserId, bool IsAdministrator) : IUserContext
    {
        public SystemOwnerFilter ToSystemOwnerFilter() => IsAdministrator
            ? SystemOwnerFilter.AllSystems()
            : SystemOwnerFilter.OwnedBy(UserId);
    }
}

internal interface IStory115FinalRunOperations
{
    Task ExecuteAsync(Story115FinalRunPlan plan, CancellationToken cancellationToken);
}

internal sealed record Story115ReleaseContract(
    string DatabasePrefix,
    PreparedRepositoryIdentity AccountRepository,
    PreparedRepositoryIdentity ChannelRepository,
    IReadOnlyList<string> OrderedFinalPath,
    string DockerfilePath,
    string ProviderFixturePath,
    IReadOnlyList<string> ProductProvisioningServices,
    IReadOnlyList<string> FinalImageBuildSteps);

internal sealed record Story115RunIdentity(
    string Nonce,
    string DatabaseName,
    string PostgreSqlContainer,
    string ProviderContainer,
    string AppContainer,
    string Network,
    string Volume)
{
    internal IReadOnlyList<string> ResourceNames { get; } =
        Array.AsReadOnly(
        [
            PostgreSqlContainer,
            ProviderContainer,
            AppContainer,
            Network,
            Volume,
        ]);
}

internal interface IStory115ReleaseOperations
{
    Task PrepareInfrastructureAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken);

    Task ProveDatabaseAbsentAsync(
        Story115RunIdentity identity,
        bool afterDrop,
        CancellationToken cancellationToken);

    Task CreateDatabaseAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken);

    Task MigrateAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken);

    Task RemoveAppAndProviderAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken);

    Task TerminateDatabaseConnectionsAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken);

    Task DropDatabaseAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken);

    Task RemoveRemainingOwnedStateAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken);
}

internal sealed class Story115ExecutableFinalRunOperations :
    IStory115FinalRunOperations,
    IStory115ReleaseOperations
{
    private const string DataProtectionApplicationName = "Vulgata.Story115.ReleaseGate";
    private static readonly IReadOnlyDictionary<string, string> _emptyEnvironment =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));
    private readonly string _repositoryRoot;
    private readonly Story115ProcessExecutor _executor = new();
    private readonly List<Story115CommandEvidence> _commandEvidence = [];
    private readonly Dictionary<string, bool> _cleanup = new(StringComparer.Ordinal);
    private Story115FinalRunPlan? _plan;
    private Story115ProvisioningFactory? _provisioningFactory;
    private Story115ProductRunState? _product;
    private string? _administratorConnectionString;
    private string? _targetConnectionString;
    private string? _imageId;
    private IReadOnlyDictionary<string, string>? _imageLabels;
    private Story115PositiveAnswerEvidence? _positiveAnswer;
    private string? _negativeAuthorizationOutcome;
    private bool _databaseDropped;
    private bool _databaseAbsenceObserved;

    internal Story115ExecutableFinalRunOperations(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        if (!File.Exists(Path.Combine(_repositoryRoot, "Vulgata.slnx")))
        {
            throw new DirectoryNotFoundException(
                "The Story 11.5 final-run root must contain Vulgata.slnx.");
        }
    }

    internal static IReadOnlyList<string> ConcreteStages { get; } =
        Array.AsReadOnly(
        [
            "postgresql-17-lifecycle",
            "production-migrations-and-product-services",
            "isolated-worker-chat-provider",
            "single-source-bound-image",
            "label-and-health-inspection",
            "identity-interactive-positive",
            "identity-negative",
            "post-selection-revocation",
            "sanitized-evidence",
            "unconditional-owned-cleanup",
        ]);

    internal static IReadOnlyList<string> InteractiveWaitStates { get; } =
        Array.AsReadOnly(["Loaded", "Empty", "Error"]);

    public async Task ExecuteAsync(
        Story115FinalRunPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (_plan is not null)
        {
            throw new InvalidOperationException("A final-run operations instance is single use.");
        }

        _plan = plan;
        await Story115ReleaseHarness.ExecuteLifecycleAsync(
            plan.Identity,
            this,
            ExecutePrimaryAsync,
            cancellationToken);
        await Story115ReleaseHarness.WriteEvidenceAsync(
            _repositoryRoot,
            CreateEvidence(plan),
            cancellationToken);
    }

    public async Task PrepareInfrastructureAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken)
    {
        Story115FinalRunPlan plan = RequirePlan(identity);
        Directory.CreateDirectory(plan.RunDirectory);
        Directory.CreateDirectory(plan.KeyRingDirectory);

        var supplied = new NpgsqlConnectionStringBuilder(
            plan.Environment.PostgreSqlAdministratorConnection);
        RequirePostgreSqlCredentials(supplied);
        await ExecuteRequiredAsync(FindCommand(plan, "network-create"), cancellationToken);
        await ExecuteRequiredAsync(
            FindCommand(plan, "postgres-run"),
            cancellationToken,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["POSTGRES_USER"] = supplied.Username,
                ["POSTGRES_PASSWORD"] = supplied.Password,
                ["POSTGRES_DB"] = supplied.Database,
            });

        Story115ProcessResult portResult = await ExecuteRequiredAsync(
            new(
                "postgres-port-inspect",
                "docker",
                ["port", identity.PostgreSqlContainer, "5432/tcp"]),
            cancellationToken);
        int port = ParsePublishedPort(portResult.StandardOutput);
        supplied.Host = IPAddress.Loopback.ToString();
        supplied.Port = port;
        supplied.Pooling = false;
        supplied.Timeout = 5;
        _administratorConnectionString = supplied.ConnectionString;

        bool ready = await Story115ReleaseHarness.PollAsync(
            async token =>
            {
                try
                {
                    await using var connection = new NpgsqlConnection(_administratorConnectionString);
                    await connection.OpenAsync(token);
                    return true;
                }
                catch (NpgsqlException)
                {
                    return false;
                }
            },
            maximumAttempts: 60,
            delay: TimeSpan.FromSeconds(1),
            cancellationToken);
        if (!ready)
        {
            throw new TimeoutException("The nonce-owned PostgreSQL 17 container did not become ready.");
        }

        var target = new NpgsqlConnectionStringBuilder(_administratorConnectionString)
        {
            Database = identity.DatabaseName,
        };
        _targetConnectionString = target.ConnectionString;
    }

    public async Task ProveDatabaseAbsentAsync(
        Story115RunIdentity identity,
        bool afterDrop,
        CancellationToken cancellationToken)
    {
        RequirePlan(identity);
        if (_administratorConnectionString is null)
        {
            if (afterDrop)
            {
                return;
            }

            throw new InvalidOperationException("PostgreSQL administration was not prepared.");
        }

        await using var connection = new NpgsqlConnection(_administratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            connection);
        command.Parameters.AddWithValue("name", identity.DatabaseName);
        bool exists = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        if (exists)
        {
            throw new InvalidOperationException(
                afterDrop
                    ? "The nonce-owned database still exists after cleanup."
                    : "The nonce-owned database unexpectedly existed before creation.");
        }

        if (afterDrop)
        {
            _databaseAbsenceObserved = true;
            _cleanup["database-absence-proved"] = true;
        }
    }

    public async Task CreateDatabaseAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken)
    {
        RequirePlan(identity);
        await ExecuteAdministrativeNonQueryAsync(
            $"CREATE DATABASE {QuoteIdentifier(identity.DatabaseName)};",
            cancellationToken);
    }

    public Task MigrateAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken)
    {
        Story115FinalRunPlan plan = RequirePlan(identity);
        cancellationToken.ThrowIfCancellationRequested();
        if (_targetConnectionString is null)
        {
            throw new InvalidOperationException("The nonce-owned target connection is unavailable.");
        }

        _provisioningFactory = new Story115ProvisioningFactory(
            _targetConnectionString,
            plan.KeyRingDirectory,
            Path.Combine(plan.RunDirectory, "codegraph"));
        _ = _provisioningFactory.Services;
        return Task.CompletedTask;
    }

    public async Task RemoveAppAndProviderAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken)
    {
        RequirePlan(identity);
        _provisioningFactory?.Dispose();
        _provisioningFactory = null;
        await RemoveExactContainerAsync(identity.AppContainer, cancellationToken);
        await RemoveExactContainerAsync(identity.ProviderContainer, cancellationToken);
        _cleanup["app-and-provider-removed"] = true;
    }

    public async Task TerminateDatabaseConnectionsAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken)
    {
        RequirePlan(identity);
        if (_administratorConnectionString is null)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_administratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
            "WHERE datname = @name AND pid <> pg_backend_pid();",
            connection);
        command.Parameters.AddWithValue("name", identity.DatabaseName);
        await command.ExecuteNonQueryAsync(cancellationToken);
        _cleanup["database-connections-terminated"] = true;
    }

    public async Task DropDatabaseAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken)
    {
        RequirePlan(identity);
        if (_administratorConnectionString is null)
        {
            return;
        }

        await ExecuteAdministrativeNonQueryAsync(
            $"DROP DATABASE IF EXISTS {QuoteIdentifier(identity.DatabaseName)};",
            cancellationToken);
        _databaseDropped = true;
        _cleanup["database-dropped"] = true;
    }

    public async Task RemoveRemainingOwnedStateAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken)
    {
        Story115FinalRunPlan plan = RequirePlan(identity);
        await RemoveExactContainerAsync(identity.PostgreSqlContainer, cancellationToken);
        await ExecuteCleanupAsync(
            new("image-remove", "docker", ["image", "rm", "--force", ImageName(identity)]),
            cancellationToken);
        await ExecuteCleanupAsync(
            new("volume-remove", "docker", ["volume", "rm", "--force", identity.Volume]),
            cancellationToken);
        await ExecuteCleanupAsync(
            new("network-remove", "docker", ["network", "rm", identity.Network]),
            cancellationToken);
        if (Directory.Exists(plan.RunDirectory))
        {
            Directory.Delete(plan.RunDirectory, recursive: true);
        }

        _cleanup["remaining-owned-state-removed"] = true;
    }

    private async Task ExecutePrimaryAsync(CancellationToken cancellationToken)
    {
        Story115FinalRunPlan plan = _plan!;
        Story115ProvisioningFactory factory = _provisioningFactory ??
            throw new InvalidOperationException("Production migrations did not initialize the product host.");
        plan.PortReservation.Release();
        await ExecuteRequiredAsync(
            MakeProviderFixtureAbsolute(FindCommand(plan, "provider-run")),
            cancellationToken);

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            _product = await Story115ProductOrchestrator.ExecuteAsync(
                scope.ServiceProvider,
                plan.Environment,
                plan.Identity,
                plan.Ports,
                cancellationToken);
        }

        await ExecuteRequiredAsync(
            FindCommand(plan, "source-bound-image-build"),
            cancellationToken,
            timeout: TimeSpan.FromMinutes(30));
        Story115ProcessResult imageInspection = await ExecuteRequiredAsync(
            FindCommand(plan, "image-label-inspect"),
            cancellationToken);
        ParseAndVerifyImageInspection(imageInspection.StandardOutput, plan.Environment);
        await WriteAppEnvironmentAsync(plan, cancellationToken);
        await ExecuteRequiredAsync(FindCommand(plan, "app-run"), cancellationToken);

        bool healthy = await Story115ReleaseHarness.PollAsync(
            async token =>
            {
                Story115ProcessResult result = await ExecuteAsync(
                    FindCommand(plan, "curl-health-inspect"),
                    token,
                    timeout: TimeSpan.FromSeconds(20));
                return result.ExitCode == 0 &&
                    StringComparer.Ordinal.Equals(result.StandardOutput.Trim(), "healthy");
            },
            maximumAttempts: 60,
            delay: TimeSpan.FromSeconds(2),
            cancellationToken);
        if (!healthy)
        {
            throw new TimeoutException("The source-bound application container did not become healthy.");
        }

        await ExecuteBrowserCircuitAsync(plan, factory.Services, cancellationToken);
    }

    private async Task ExecuteBrowserCircuitAsync(
        Story115FinalRunPlan plan,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        Story115ProductRunState product = _product ??
            throw new InvalidOperationException("Product state was not provisioned.");
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(
            new() { Headless = true });
        string origin = $"http://127.0.0.1:{plan.Ports[2]}";

        await using (IBrowserContext ownerContext = await browser.NewContextAsync())
        {
            IPage page = await ownerContext.NewPageAsync();
            await LoginAsync(page, origin, product.OwnerEmail, product.OwnerPassword);
            await SelectPreparedScopeAsync(page, product);
            await page.GetByRole(AriaRole.Button, new() { Name = "业务模式" }).ClickAsync();
            await page.Locator("#chat-question").FillAsync("客户如何完成跨仓库付款？");
            await page.GetByRole(AriaRole.Button, new() { Name = "发送" }).ClickAsync();
            await page.Locator(".complete-answer").WaitForAsync(
                new() { State = WaitForSelectorState.Visible, Timeout = 120_000 });
            int claims = await page.Locator(".material-claim").CountAsync();
            int citations = await page.Locator(".document-citation").CountAsync();
            if (claims < 1 || citations < 1)
            {
                throw new InvalidOperationException(
                    "The positive Business answer lacked material claims or Document Citations.");
            }

            _positiveAnswer = new("Business", claims, citations, Validated: true);
        }

        await using (IBrowserContext negativeContext = await browser.NewContextAsync())
        {
            IPage page = await negativeContext.NewPageAsync();
            await LoginAsync(page, origin, product.NegativeEmail, product.NegativePassword);
            await WaitForLoadStateAsync(page, "Empty");
            int accessibleSystems = await page.Locator("#chat-system option:not([value=''])").CountAsync();
            if (accessibleSystems != 0)
            {
                throw new InvalidOperationException("The inaccessible-scope identity saw an authorized System.");
            }

            _negativeAuthorizationOutcome = "no-authorized-system-option";
        }

        await using (IBrowserContext revokedContext = await browser.NewContextAsync())
        {
            IPage page = await revokedContext.NewPageAsync();
            await LoginAsync(page, origin, product.OwnerEmail, product.OwnerPassword);
            await SelectPreparedScopeAsync(page, product);
            using IServiceScope scope = services.CreateScope();
            SystemOwnershipRemovalResult removal = await scope.ServiceProvider
                .GetRequiredService<ISystemOwnershipCoordinator>()
                .RemoveOwnerAsync(product.SystemId, await OwnerIdAsync(scope, product.OwnerEmail), cancellationToken);
            if (removal.Outcome != SystemOwnershipRemovalOutcome.Removed)
            {
                throw new InvalidOperationException(
                    $"Post-selection ownership revocation failed: {removal.Outcome}.");
            }

            await page.Locator("#chat-question").FillAsync("撤权后不得返回证据");
            await page.GetByRole(AriaRole.Button, new() { Name = "发送" }).ClickAsync();
            await WaitForLoadStateAsync(page, "Error");
            if (await page.Locator(".complete-answer").CountAsync() != 0 ||
                await page.Locator(".material-claim").CountAsync() != 0 ||
                await page.Locator(".document-citation").CountAsync() != 0)
            {
                throw new InvalidOperationException(
                    "Post-selection revocation returned a complete answer or evidence.");
            }

            _negativeAuthorizationOutcome += ";post-selection-revocation-denied";
        }
    }

    private static async Task<string> OwnerIdAsync(
        IServiceScope scope,
        string ownerEmail)
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser owner = await users.FindByEmailAsync(ownerEmail) ??
            throw new InvalidOperationException("The prepared owner could not be reloaded.");
        return owner.Id;
    }

    private static async Task LoginAsync(
        IPage page,
        string origin,
        string email,
        string password)
    {
        await page.GotoAsync(origin + "/Account/Login");
        await page.Locator("#Input\\.Email").FillAsync(email);
        await page.Locator("#Input\\.Password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "登录" }).ClickAsync();
        await page.WaitForURLAsync(origin + "/", new() { Timeout = 30_000 });
    }

    private static async Task SelectPreparedScopeAsync(
        IPage page,
        Story115ProductRunState product)
    {
        await WaitForLoadStateAsync(page, "Loaded");
        await page.Locator("#chat-system").SelectOptionAsync(product.SystemId.ToString());
        await page.Locator("#chat-repository-first").SelectOptionAsync(
            product.AccountRepositoryId.ToString());
        await page.Locator("#chat-repository-second").SelectOptionAsync(
            product.ChannelRepositoryId.ToString());
    }

    private static async Task WaitForLoadStateAsync(IPage page, string state)
    {
        if (!InteractiveWaitStates.Contains(state, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The interactive wait state is outside the closed circuit.");
        }

        await page.Locator($".chat-page[data-load-state='{state}']").WaitForAsync(
            new() { State = WaitForSelectorState.Visible, Timeout = 120_000 });
    }

    private Story115ReleaseEvidence CreateEvidence(Story115FinalRunPlan plan) =>
        new(
            "story-11-5-release-evidence-v1",
            new(
                plan.Environment.CandidateCommit,
                plan.Environment.CandidateTree,
                plan.Environment.ProductionManifestSha256,
                _imageId,
                _imageLabels),
            [Story115ReleaseHarness.Contract.AccountRepository,
                Story115ReleaseHarness.Contract.ChannelRepository],
            _product?.PreparedPass2Flow.RuntimeBinding?.ProfileFingerprint,
            CreateRuntimePass2Evidence(),
            _commandEvidence.AsReadOnly(),
            _positiveAnswer,
            _negativeAuthorizationOutcome,
            new(plan.Identity.DatabaseName, _databaseDropped, _databaseAbsenceObserved),
            new ReadOnlyDictionary<string, bool>(_cleanup),
            Story115ReleaseHarness.KnownLimitations);

    private Story115RuntimePass2Evidence? CreateRuntimePass2Evidence()
    {
        if (_product is null)
        {
            return null;
        }

        PreparedPass2RuntimeBinding binding = _product.PreparedPass2Flow.RuntimeBinding ??
            throw new InvalidOperationException("Runtime Pass 2 binding was not retained for evidence.");
        return new(
            binding.SupportFingerprint,
            _product.AccountRepositoryId,
            SupportedRepositoryCount: 2,
            PayloadSlotCount: Story115ProductOrchestrator.Pass2PayloadSlots.Count,
            DispatchCount: 1,
            CommitCount: 1);
    }

    private async Task WriteAppEnvironmentAsync(
        Story115FinalRunPlan plan,
        CancellationToken cancellationToken)
    {
        var external = new NpgsqlConnectionStringBuilder(_targetConnectionString)
        {
            Host = plan.Identity.PostgreSqlContainer,
            Port = 5432,
            Pooling = true,
        };
        string content = string.Join('\n',
        [
            "ASPNETCORE_ENVIRONMENT=Production",
            "ASPNETCORE_URLS=http://+:8080",
            "ConnectionStrings__DefaultConnection=" + external.ConnectionString,
            "DataProtection__KeyRingPath=/var/lib/vulgata-dpkeys",
            "DataProtection__ApplicationName=" + DataProtectionApplicationName,
        ]) + "\n";
        await File.WriteAllTextAsync(plan.AppEnvironmentFile, content, cancellationToken);
    }

    private void ParseAndVerifyImageInspection(
        string json,
        Story115FinalEnvironment environment)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement image = document.RootElement.EnumerateArray().Single();
        _imageId = image.GetProperty("Id").GetString() ??
            throw new InvalidOperationException("The source-bound image ID was absent.");
        JsonElement labels = image.GetProperty("Config").GetProperty("Labels");
        var actual = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["org.vulgata.source-commit"] = labels.GetProperty("org.vulgata.source-commit").GetString()!,
            ["org.vulgata.source-tree"] = labels.GetProperty("org.vulgata.source-tree").GetString()!,
            ["org.vulgata.production-source-manifest-sha256"] = labels
                .GetProperty("org.vulgata.production-source-manifest-sha256").GetString()!,
        };
        if (!StringComparer.Ordinal.Equals(actual["org.vulgata.source-commit"], environment.CandidateCommit) ||
            !StringComparer.Ordinal.Equals(actual["org.vulgata.source-tree"], environment.CandidateTree) ||
            !StringComparer.Ordinal.Equals(
                actual["org.vulgata.production-source-manifest-sha256"],
                environment.ProductionManifestSha256))
        {
            throw new InvalidOperationException("The single final image labels do not match the candidate source.");
        }

        _imageLabels = new ReadOnlyDictionary<string, string>(actual);
    }

    private Story115ExternalCommand MakeProviderFixtureAbsolute(Story115ExternalCommand command)
    {
        string relative = Story115ReleaseHarness.Contract.ProviderFixturePath.Replace('\\', '/');
        string absolute = Path.Combine(
            _repositoryRoot,
            Story115ReleaseHarness.Contract.ProviderFixturePath.Replace('/', Path.DirectorySeparatorChar));
        return command with
        {
            Arguments = Array.AsReadOnly(
            [
                .. command.Arguments.Select(argument => argument.Replace(
                    $"source={relative}",
                    $"source={absolute}",
                    StringComparison.Ordinal)),
            ]),
        };
    }

    private async Task ExecuteAdministrativeNonQueryAsync(
        string commandText,
        CancellationToken cancellationToken)
    {
        if (_administratorConnectionString is null)
        {
            throw new InvalidOperationException("PostgreSQL administration was not prepared.");
        }

        await using var connection = new NpgsqlConnection(_administratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RemoveExactContainerAsync(
        string name,
        CancellationToken cancellationToken)
    {
        Story115ReleaseHarness.RequireOwnedResourceName(name);
        await ExecuteCleanupAsync(
            new("container-remove-" + name[^8..], "docker", ["rm", "--force", name]),
            cancellationToken);
    }

    private async Task ExecuteCleanupAsync(
        Story115ExternalCommand command,
        CancellationToken cancellationToken)
    {
        Story115ProcessResult result = await ExecuteAsync(command, cancellationToken);
        if (result.ExitCode != 0 &&
            !result.StandardError.Contains("No such", StringComparison.OrdinalIgnoreCase) &&
            !result.StandardError.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cleanup command {command.Name} failed with exit code {result.ExitCode}.");
        }
    }

    private async Task<Story115ProcessResult> ExecuteRequiredAsync(
        Story115ExternalCommand command,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null,
        TimeSpan? timeout = null)
    {
        Story115ProcessResult result = await ExecuteAsync(
            command,
            cancellationToken,
            environment,
            timeout);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Command {command.Name} failed with exit code {result.ExitCode}.");
        }

        return result;
    }

    private async Task<Story115ProcessResult> ExecuteAsync(
        Story115ExternalCommand command,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null,
        TimeSpan? timeout = null)
    {
        Story115ProcessResult result = await _executor.ExecuteAsync(
            command,
            _repositoryRoot,
            environment ?? _emptyEnvironment,
            timeout ?? TimeSpan.FromMinutes(2),
            cancellationToken);
        _commandEvidence.Add(result.ToEvidence());
        return result;
    }

    private Story115FinalRunPlan RequirePlan(Story115RunIdentity identity)
    {
        Story115FinalRunPlan plan = _plan ??
            throw new InvalidOperationException("The final-run plan was not assigned.");
        if (!StringComparer.Ordinal.Equals(plan.Identity.Nonce, identity.Nonce))
        {
            throw new InvalidOperationException("The lifecycle identity differs from the final-run plan.");
        }

        return plan;
    }

    private static Story115ExternalCommand FindCommand(
        Story115FinalRunPlan plan,
        string name) =>
        plan.Commands.Single(command => StringComparer.Ordinal.Equals(command.Name, name));

    private static void RequirePostgreSqlCredentials(NpgsqlConnectionStringBuilder connection)
    {
        if (string.IsNullOrWhiteSpace(connection.Username) ||
            string.IsNullOrWhiteSpace(connection.Password) ||
            string.IsNullOrWhiteSpace(connection.Database))
        {
            throw new InvalidOperationException(
                "The PostgreSQL administrator connection requires username, password, and database.");
        }
    }

    private static int ParsePublishedPort(string output)
    {
        string endpoint = output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).Single();
        int separator = endpoint.LastIndexOf(':');
        if (separator < 0 ||
            !int.TryParse(
                endpoint[(separator + 1)..],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out int port) ||
            port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("Docker returned an invalid PostgreSQL host port.");
        }

        return port;
    }

    private static string QuoteIdentifier(string value) =>
        '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';

    private static string ImageName(Story115RunIdentity identity) =>
        "vulgata-story115-" + identity.Nonce + "-image";

    private sealed class Story115ProvisioningFactory(
        string connectionString,
        string keyRingDirectory,
        string codeGraphDirectory) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["DataProtection:KeyRingPath"] = keyRingDirectory,
                    ["DataProtection:ApplicationName"] = DataProtectionApplicationName,
                    ["CodeGraph:DataRootPath"] = codeGraphDirectory,
                }));
        }
    }
}

internal sealed class Story115ReleaseGateException(
    Exception? primaryFailure,
    Exception? cleanupFailure)
    : Exception(CreateMessage(primaryFailure, cleanupFailure), primaryFailure ?? cleanupFailure)
{
    internal Exception? PrimaryFailure { get; } = primaryFailure;

    internal Exception? CleanupFailure { get; } = cleanupFailure;

    private static string CreateMessage(Exception? primary, Exception? cleanup) =>
        $"Story 11.5 release gate failed (primary: {primary?.Message ?? "none"}; " +
        $"cleanup: {cleanup?.Message ?? "none"}).";
}

internal sealed record Story115ReleaseEvidence(
    string SchemaVersion,
    Story115CandidateEvidence Candidate,
    IReadOnlyList<PreparedRepositoryIdentity> PreparedRepositories,
    string? ProfileFingerprint,
    Story115RuntimePass2Evidence? RuntimePass2,
    IReadOnlyList<Story115CommandEvidence> Commands,
    Story115PositiveAnswerEvidence? PositiveAnswer,
    string? NegativeAuthorizationOutcome,
    Story115DatabaseEvidence Database,
    IReadOnlyDictionary<string, bool> Cleanup,
    IReadOnlyList<string> KnownLimitations)
{
    internal static Story115ReleaseEvidence CreateFastContractEvidence(
        Story115RunIdentity identity,
        string sourceCommit) =>
        new(
            "story-11-5-release-evidence-v1",
            new(sourceCommit, new string('0', 40), new string('0', 64), null, null),
            [Story115ReleaseHarness.Contract.AccountRepository,
                Story115ReleaseHarness.Contract.ChannelRepository],
            null,
            null,
            [],
            null,
            null,
            new(identity.DatabaseName, false, false),
            new ReadOnlyDictionary<string, bool>(new Dictionary<string, bool>()),
            Story115ReleaseHarness.KnownLimitations);
}

internal sealed record Story115CandidateEvidence(
    string Commit,
    string Tree,
    string ProductionManifestSha256,
    string? ImageId,
    IReadOnlyDictionary<string, string>? ImageLabels);

internal sealed record Story115RuntimePass2Evidence(
    string SupportFingerprint,
    Guid ConsumerRepositoryId,
    int SupportedRepositoryCount,
    int PayloadSlotCount,
    int DispatchCount,
    int CommitCount);

internal sealed record Story115CommandEvidence(
    string CommandName,
    int ExitCode,
    DateTimeOffset ObservedAtUtc,
    string LogSha256,
    string? RelativeLogPath);

internal sealed record Story115PositiveAnswerEvidence(
    string Mode,
    int ClaimCount,
    int CitationCount,
    bool Validated);

internal sealed record Story115DatabaseEvidence(
    string Name,
    bool Dropped,
    bool AbsenceObserved);

internal sealed class Story115PortReservation : IAsyncDisposable
{
    private readonly IReadOnlyList<TcpListener> _listeners;
    private bool _released;

    internal Story115PortReservation(IReadOnlyList<TcpListener> listeners)
    {
        _listeners = listeners;
        Ports = Array.AsReadOnly(
        [
            .. listeners.Select(listener =>
                ((IPEndPoint)listener.LocalEndpoint).Port),
        ]);
    }

    internal IReadOnlyList<int> Ports { get; }

    internal void Release()
    {
        if (_released)
        {
            return;
        }

        foreach (TcpListener listener in _listeners)
        {
            listener.Stop();
        }

        _released = true;
    }

    public ValueTask DisposeAsync()
    {
        Release();
        return ValueTask.CompletedTask;
    }
}

internal static partial class Story115ReleaseHarness
{
    private const string ResourcePrefix = "vulgata-story115-";

    internal const string ReleaseEvidencePath =
        "tests/Vulgata.Tests/TestResults/story-11-5-release-evidence.json";

    private static readonly JsonSerializerOptions _evidenceJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    internal static IReadOnlyList<Story115EnvironmentVariable> EnvironmentContract { get; } =
        Array.AsReadOnly<Story115EnvironmentVariable>(
        [
            new("VULGATA_STORY115_RUN_FINAL", false, "exactly 1", "Activates the unique Release-only live lane."),
            new("VULGATA_STORY115_POSTGRES_ADMIN_CONNECTION", true, "absolute PostgreSQL connection string with an administrator database", "Creates and drops only the nonce-owned database."),
            new("VULGATA_STORY115_ACCOUNT_REPOSITORY_URL", false, "absolute HTTPS, SSH, file, or local Git URI", "Acquires mbs_account at the accepted ref and full commit."),
            new("VULGATA_STORY115_CHANNEL_REPOSITORY_URL", false, "absolute HTTPS, SSH, file, or local Git URI", "Acquires mbs_channel at the accepted ref and full commit."),
            new("VULGATA_STORY115_CANDIDATE_COMMIT", false, "40 lowercase hexadecimal characters", "Binds the final image source-commit label."),
            new("VULGATA_STORY115_CANDIDATE_TREE", false, "40 lowercase hexadecimal characters", "Binds the final image source-tree label."),
            new("VULGATA_STORY115_PRODUCTION_MANIFEST_SHA256", false, "64 lowercase hexadecimal characters", "Binds the canonical production-source-manifest label."),
        ]);

    internal static Story115ReleaseContract Contract { get; } = new(
        DatabasePrefix: "vulgata_story115_",
        AccountRepository: new(
            "mbs_account",
            "refs/tags/vulgata/August15th-sample",
            "19c60cd3f3976b7697c487c7bafd96142e3a7625"),
        ChannelRepository: new(
            "mbs_channel",
            "refs/tags/vulgata/August15th-sample",
            "47f6b627a7598f862873610413eec1e1e7bf46bf"),
        OrderedFinalPath: ReadOnly(
            "migrate",
            "product-provision",
            "profile",
            "pass1-account",
            "pass1-channel",
            "exact-cross-repository-resolution",
            "pass2",
            "chat-retrieval",
            "chat-selection",
            "chat-currentness",
            "chat-final-authorization",
            "source-bound-image",
            "label-inspection",
            "curl-health",
            "identity-playwright-circuit",
            "inaccessible-scope",
            "post-selection-revocation"),
        DockerfilePath: "docker/Dockerfile",
        ProviderFixturePath:
            "tests/Vulgata.Tests/ReleaseGate/loopback-openai-provider.py",
        ProductProvisioningServices: ReadOnly(
            "UserManager<ApplicationUser>",
            "RoleSeeder",
            "IAdministratorRoleCoordinator",
            "ISystemOwnershipCoordinator",
            "IRepositoryManagementCoordinator",
            "IRepositoryProfilingCoordinator",
            "SequentialScanRunner",
            "ExactCrossRepositoryResolutionCoordinator",
            "ILlmProviderManagementCoordinator",
            "ISystemLlmProviderOverrideCoordinator",
            "SecretEncryptionService"),
        FinalImageBuildSteps: ReadOnly("source-bound-image"));

    internal static IReadOnlyList<string> KnownLimitations { get; } = ReadOnly(
        "automatic scope selection",
        "file upload",
        "saved chat history",
        "iterative retrieval tools",
        "retry and failover",
        "concurrent scanning",
        "generalized Cross-Repository matching",
        "graph dashboard and SignalR",
        "deferred Epic 5-10 behavior");

    internal static Story115RunIdentity CreateRunIdentity()
    {
        string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
            .ToLowerInvariant();
        return new(
            nonce,
            Contract.DatabasePrefix + nonce,
            ResourcePrefix + nonce + "-postgres",
            ResourcePrefix + nonce + "-provider",
            ResourcePrefix + nonce + "-app",
            ResourcePrefix + nonce + "-network",
            ResourcePrefix + nonce + "-volume");
    }

    internal static Dictionary<string, string> CreateValidEnvironmentForFastTests() =>
        new(StringComparer.Ordinal)
        {
            ["VULGATA_STORY115_RUN_FINAL"] = "1",
            ["VULGATA_STORY115_POSTGRES_ADMIN_CONNECTION"] =
                "Host=127.0.0.1;Port=5432;Database=postgres;Username=gate;Password=not-emitted",
            ["VULGATA_STORY115_ACCOUNT_REPOSITORY_URL"] = "https://example.invalid/mbs_account.git",
            ["VULGATA_STORY115_CHANNEL_REPOSITORY_URL"] = "https://example.invalid/mbs_channel.git",
            ["VULGATA_STORY115_CANDIDATE_COMMIT"] = new('a', 40),
            ["VULGATA_STORY115_CANDIDATE_TREE"] = new('b', 40),
            ["VULGATA_STORY115_PRODUCTION_MANIFEST_SHA256"] = new('c', 64),
        };

    internal static async Task<bool> RunActivatedFinalAsync(
        IReadOnlyDictionary<string, string> values,
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!values.TryGetValue("VULGATA_STORY115_RUN_FINAL", out string? activation) ||
            !StringComparer.Ordinal.Equals(activation, "1"))
        {
            return false;
        }

        await RunFinalAsync(
            values,
            new Story115ExecutableFinalRunOperations(repositoryRoot),
            cancellationToken);
        return true;
    }

    internal static Task<bool> RunFromProcessEnvironmentAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Story115EnvironmentVariable variable in EnvironmentContract)
        {
            string? value = Environment.GetEnvironmentVariable(variable.Name);
            if (value is not null)
            {
                values[variable.Name] = value;
            }
        }

        return RunActivatedFinalAsync(values, repositoryRoot, cancellationToken);
    }

    internal static async Task RunFinalAsync(
        IReadOnlyDictionary<string, string> values,
        IStory115FinalRunOperations operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(operations);
        Story115FinalEnvironment environment = ParseFinalEnvironment(values);
        Story115RunIdentity identity = CreateRunIdentity();
        await using Story115PortReservation ports = ReserveDistinctPorts();
        string runDirectory = Path.Combine(
            Path.GetTempPath(),
            ResourcePrefix + identity.Nonce);
        string keyRingDirectory = Path.Combine(runDirectory, "key-ring");
        string appEnvironmentFile = Path.Combine(runDirectory, "app.env");
        var plan = new Story115FinalRunPlan(
            environment,
            identity,
            ports.Ports,
            Contract.OrderedFinalPath,
            runDirectory,
            keyRingDirectory,
            appEnvironmentFile,
            CreateExternalCommands(
                environment,
                identity,
                ports.Ports,
                keyRingDirectory,
                appEnvironmentFile),
            ports);
        await operations.ExecuteAsync(plan, cancellationToken);
    }

    private static IReadOnlyList<Story115ExternalCommand> CreateExternalCommands(
        Story115FinalEnvironment environment,
        Story115RunIdentity identity,
        IReadOnlyList<int> ports,
        string keyRingDirectory,
        string appEnvironmentFile)
    {
        string imageName = ResourcePrefix + identity.Nonce + "-image";
        string providerFixture = Contract.ProviderFixturePath.Replace('\\', '/');
        return Array.AsReadOnly<Story115ExternalCommand>(
        [
            new(
                "network-create",
                "docker",
                ["network", "create", "--internal", identity.Network]),
            new(
                "postgres-run",
                "docker",
                [
                    "run", "--detach", "--name", identity.PostgreSqlContainer,
                    "--network", identity.Network,
                    "--mount", $"type=volume,source={identity.Volume},target=/var/lib/postgresql/data",
                    "--publish", "127.0.0.1::5432",
                    "--env", "POSTGRES_USER",
                    "--env", "POSTGRES_PASSWORD",
                    "--env", "POSTGRES_DB",
                    "postgres:17",
                ]),
            new(
                "provider-run",
                "docker",
                [
                    "run", "--detach", "--name", identity.ProviderContainer,
                    "--network", identity.Network,
                    "--publish", $"127.0.0.1:{ports[0]}:{ports[0]}",
                    "--publish", $"127.0.0.1:{ports[1]}:{ports[1]}",
                    "--publish", $"127.0.0.1:{ports[2]}:8080",
                    "--mount", $"type=bind,source={providerFixture},target=/fixture/loopback-openai-provider.py,readonly",
                    "python:3.12-bookworm@sha256:9bed8554e926c07c6f908841d5ee88c33e8df9236b191526bbce81a9062ab43a",
                    "python", "/fixture/loopback-openai-provider.py",
                    "--host", "0.0.0.0",
                    "--worker-port", ports[0].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--chat-port", ports[1].ToString(System.Globalization.CultureInfo.InvariantCulture),
                ]),
            new(
                "source-bound-image-build",
                "docker",
                [
                    "build", "--no-cache", "--progress=plain",
                    "--file", Contract.DockerfilePath,
                    "--tag", imageName,
                    "--build-arg", $"VULGATA_SOURCE_COMMIT={environment.CandidateCommit}",
                    "--build-arg", $"VULGATA_SOURCE_TREE={environment.CandidateTree}",
                    "--build-arg", $"VULGATA_PRODUCTION_SOURCE_MANIFEST_SHA256={environment.ProductionManifestSha256}",
                    ".",
                ]),
            new(
                "image-label-inspect",
                "docker",
                ["image", "inspect", imageName]),
            new(
                "app-run",
                "docker",
                [
                    "run", "--detach", "--name", identity.AppContainer,
                    "--network", $"container:{identity.ProviderContainer}",
                    "--env-file", appEnvironmentFile,
                    "--mount", $"type=bind,source={keyRingDirectory},target=/var/lib/vulgata-dpkeys,readonly",
                    "--health-cmd", "curl --fail --silent --show-error http://127.0.0.1:8080/Account/Login",
                    "--health-interval", "2s",
                    "--health-timeout", "5s",
                    "--health-retries", "30",
                    "--health-start-period", "15s",
                    imageName,
                ]),
            new(
                "curl-health-inspect",
                "docker",
                [
                    "inspect", "--format", "{{.State.Health.Status}}",
                    identity.AppContainer,
                ]),
            new(
                "identity-playwright-circuit",
                "Microsoft.Playwright",
                [$"http://127.0.0.1:{ports[2]}"]),
        ]);
    }

    private static Story115FinalEnvironment ParseFinalEnvironment(
        IReadOnlyDictionary<string, string> values)
    {
        string Read(string name)
        {
            if (!values.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing required Story 11.5 environment variable: {name}.");
            }

            return value.Trim();
        }

        if (!StringComparer.Ordinal.Equals(Read("VULGATA_STORY115_RUN_FINAL"), "1"))
        {
            throw new InvalidOperationException("VULGATA_STORY115_RUN_FINAL must be exactly 1.");
        }

        string administratorConnection = Read("VULGATA_STORY115_POSTGRES_ADMIN_CONNECTION");
        if (!administratorConnection.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
            !administratorConnection.Contains("Database=", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The PostgreSQL administrator connection is malformed.");
        }

        Uri ReadRepositoryUri(string name)
        {
            string value = Read(name);
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
                uri.Scheme is not ("https" or "ssh" or "file"))
            {
                throw new InvalidOperationException($"{name} must be an absolute HTTPS, SSH, or file URI.");
            }

            return uri;
        }

        string ReadHash(string name, int length)
        {
            string value = Read(name);
            if (value.Length != length || value.Any(character =>
                    character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            {
                throw new InvalidOperationException($"{name} has an invalid source identity.");
            }

            return value;
        }

        return new(
            administratorConnection,
            ReadRepositoryUri("VULGATA_STORY115_ACCOUNT_REPOSITORY_URL"),
            ReadRepositoryUri("VULGATA_STORY115_CHANNEL_REPOSITORY_URL"),
            ReadHash("VULGATA_STORY115_CANDIDATE_COMMIT", 40),
            ReadHash("VULGATA_STORY115_CANDIDATE_TREE", 40),
            ReadHash("VULGATA_STORY115_PRODUCTION_MANIFEST_SHA256", 64));
    }

    internal static Story115PortReservation ReserveDistinctPorts()
    {
        var listeners = new List<TcpListener>(3);
        try
        {
            while (listeners.Count < 3)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                if (port == 8080)
                {
                    listener.Stop();
                    continue;
                }

                listeners.Add(listener);
            }

            return new Story115PortReservation(listeners.AsReadOnly());
        }
        catch
        {
            foreach (TcpListener listener in listeners)
            {
                listener.Stop();
            }

            throw;
        }
    }

    internal static async Task<bool> PollAsync(
        Func<CancellationToken, Task<bool>> probeAsync,
        int maximumAttempts,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(probeAsync);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumAttempts, 60);
        if (delay < TimeSpan.Zero || delay > TimeSpan.FromSeconds(5))
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }

        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await probeAsync(cancellationToken))
            {
                return true;
            }

            if (attempt < maximumAttempts && delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        return false;
    }

    internal static bool IsOwnedDatabaseName(string? databaseName) =>
        databaseName is not null && OwnedDatabaseNameRegex().IsMatch(databaseName);

    internal static void RequireOwnedResourceName(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        if (!OwnedResourceNameRegex().IsMatch(resourceName))
        {
            throw new InvalidOperationException(
                "Refusing to operate on a resource outside this Story 11.5 nonce.");
        }
    }

    internal static async Task ExecuteLifecycleAsync(
        Story115RunIdentity identity,
        IStory115ReleaseOperations operations,
        Func<CancellationToken, Task> executePrimaryAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(executePrimaryAsync);
        RequireOwnedIdentity(identity);

        Exception? primaryFailure = null;
        var cleanupFailures = new List<Exception>();
        try
        {
            await operations.PrepareInfrastructureAsync(identity, cancellationToken);
            await operations.ProveDatabaseAbsentAsync(
                identity,
                afterDrop: false,
                cancellationToken);
            await operations.CreateDatabaseAsync(identity, cancellationToken);
            await operations.MigrateAsync(identity, cancellationToken);
            await executePrimaryAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
        }
        finally
        {
            await CaptureCleanupFailureAsync(
                () => operations.RemoveAppAndProviderAsync(identity, CancellationToken.None),
                cleanupFailures);
            await CaptureCleanupFailureAsync(
                () => operations.TerminateDatabaseConnectionsAsync(
                    identity,
                    CancellationToken.None),
                cleanupFailures);
            await CaptureCleanupFailureAsync(
                () => operations.DropDatabaseAsync(identity, CancellationToken.None),
                cleanupFailures);
            await CaptureCleanupFailureAsync(
                () => operations.ProveDatabaseAbsentAsync(
                    identity,
                    afterDrop: true,
                    CancellationToken.None),
                cleanupFailures);
            await CaptureCleanupFailureAsync(
                () => operations.RemoveRemainingOwnedStateAsync(
                    identity,
                    CancellationToken.None),
                cleanupFailures);
        }

        Exception? cleanupFailure = cleanupFailures.Count switch
        {
            0 => null,
            1 => cleanupFailures[0],
            _ => new AggregateException("Multiple cleanup operations failed.", cleanupFailures),
        };
        if (primaryFailure is not null || cleanupFailure is not null)
        {
            throw new Story115ReleaseGateException(primaryFailure, cleanupFailure);
        }
    }

    internal static string SerializeEvidence(Story115ReleaseEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        string json = JsonSerializer.Serialize(evidence, _evidenceJsonOptions);
        ValidateSanitizedJson(json);
        return json;
    }

    internal static async Task WriteEvidenceAsync(
        string repositoryRoot,
        Story115ReleaseEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        string root = Path.GetFullPath(repositoryRoot);
        string destination = Path.GetFullPath(Path.Combine(
            root,
            ReleaseEvidencePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!destination.StartsWith(rootPrefix, comparison))
        {
            throw new InvalidOperationException("Evidence path escaped the repository root.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + ".tmp-" +
            Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        try
        {
            await File.WriteAllTextAsync(
                temporary,
                SerializeEvidence(evidence),
                cancellationToken);
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static async Task CaptureCleanupFailureAsync(
        Func<Task> cleanupAsync,
        ICollection<Exception> failures)
    {
        try
        {
            await cleanupAsync();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static void RequireOwnedIdentity(Story115RunIdentity identity)
    {
        if (!IsOwnedDatabaseName(identity.DatabaseName) ||
            !StringComparer.Ordinal.Equals(
                identity.DatabaseName,
                Contract.DatabasePrefix + identity.Nonce))
        {
            throw new InvalidOperationException(
                "Refusing a database identity outside the Story 11.5 nonce.");
        }

        foreach (string resourceName in identity.ResourceNames)
        {
            RequireOwnedResourceName(resourceName);
            if (!resourceName.Contains(identity.Nonce, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "All release resources must carry the exact run nonce.");
            }
        }
    }

    private static void ValidateSanitizedJson(string json)
    {
        string[] forbidden =
        [
            "password",
            "connectionstring",
            "apikey",
            "plaintext",
            "rawprompt",
            "rawresponse",
            "authorization: bearer",
            "C:\\",
            "G:\\",
        ];
        foreach (string token in forbidden)
        {
            if (json.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Release evidence contains a forbidden sensitive field or value.");
            }
        }
    }

    private static ReadOnlyCollection<string> ReadOnly(params string[] values) =>
        Array.AsReadOnly(values);

    [GeneratedRegex("^vulgata_story115_[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnedDatabaseNameRegex();

    [GeneratedRegex(
        "^vulgata-story115-[0-9a-f]{32}-(postgres|provider|app|network|volume)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex OwnedResourceNameRegex();
}
