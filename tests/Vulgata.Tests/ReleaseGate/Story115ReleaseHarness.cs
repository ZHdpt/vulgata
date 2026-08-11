using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Git;
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

internal sealed record Story115ProvisioningConfiguration(
    string GitWorkingCopyRootPath,
    string DataProtectionApplicationName,
    string DataProtectionKeyRingPath,
    bool ProcessConfigurationAppliedBeforeHostCreation);

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
        TimeSpan maximumTimeout = StringComparer.Ordinal.Equals(
            command.Name,
            "provisioner-run")
            ? Story115ReleaseHarness.ProvisionerProcessTimeout
            : TimeSpan.FromMinutes(45);
        if (timeout <= TimeSpan.Zero || timeout > maximumTimeout)
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
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        Task<string> standardOutputTask = ReadBoundedAsync(
            process.StandardOutput,
            linkedSource.Token);
        Task<string> standardErrorTask = ReadBoundedAsync(
            process.StandardError,
            linkedSource.Token);
        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
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
    }

    private static async Task<string> ReadBoundedAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var retained = new StringBuilder(RetainedCharacterLimit);
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
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

internal sealed record Story115ContainerProductState(
    Guid SystemId,
    Guid AccountRepositoryId,
    Guid ChannelRepositoryId,
    string ProfileFingerprint,
    string RuntimeSupportFingerprint,
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
    string ProviderContainer,
    string ProvisionerContainer,
    string AppContainer,
    string Network)
{
    internal IReadOnlyList<string> ResourceNames { get; } =
        Array.AsReadOnly(
        [
            ProviderContainer,
            ProvisionerContainer,
            AppContainer,
            Network,
        ]);
}

internal interface IStory115ReleaseOperations
{
    Task PrepareInfrastructureAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken);

    Task PreflightDockerDesktopPostgreSqlAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken);

    Task ProveDatabaseAbsentAsync(
        Story115RunIdentity identity,
        bool afterDrop,
        CancellationToken cancellationToken);

    Task CreateDatabaseAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken);

    Task<bool> ReconcileDatabaseExistsAfterAmbiguousCreateAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken);

    Task MigrateAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken);

    Task RemoveProvisionerAsync(
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
    private Story115ContainerProductState? _product;
    private string? _administratorConnectionString;
    private string? _targetConnectionString;
    private string? _imageId;
    private IReadOnlyDictionary<string, string>? _imageLabels;
    private Story115PositiveAnswerEvidence? _positiveAnswer;
    private string? _negativeAuthorizationOutcome;
    private bool _databaseDropped;
    private bool _databaseAbsenceObserved;
    private string? _administratorEmail;
    private string? _administratorPassword;
    private string? _ownerEmail;
    private string? _ownerPassword;
    private string? _negativeEmail;
    private string? _negativePassword;
    private string? _providerApiKey;
    private string? _provisionerResultSha256;
    private Story115ProvisionerDriverAttestation? _driverAttestation;
    private IReadOnlyList<string>? _imageAppInventory;

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
            "postgresql-17-administrator-database-lifecycle",
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

    internal static Story115ProvisioningConfiguration ResolveProvisioningConfigurationForTests(
        string runDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runDirectory);
        string root = Path.GetFullPath(runDirectory);
        string keyRingDirectory = Path.Combine(root, "keys");
        string codeGraphDirectory = Path.Combine(root, "codegraph");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(keyRingDirectory);

        using var environment = new Story115ProvisioningConfigurationEnvironment(
            keyRingDirectory,
            codeGraphDirectory);
        using var factory = new LoginLogoutTests.CustomWebApplicationFactory();
        IServiceProvider services = factory.Services;
        string gitWorkingCopyRootPath = services
            .GetRequiredService<GitWorkingCopyOptions>()
            .RootPath;
        var keyRepository = AssertIsFileSystemKeyRepository(services);
        string applicationName = services
            .GetRequiredService<IOptions<Microsoft.AspNetCore.DataProtection.DataProtectionOptions>>()
            .Value
            .ApplicationDiscriminator;
        return new(
            gitWorkingCopyRootPath,
            applicationName,
            keyRepository.Directory.FullName,
            environment.IsApplied);
    }

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
        Directory.CreateDirectory(Path.Combine(plan.RunDirectory, "config"));
        Directory.CreateDirectory(Path.Combine(plan.RunDirectory, "output"));
        Directory.CreateDirectory(Path.Combine(plan.RunDirectory, "work"));
        Directory.CreateDirectory(Path.Combine(plan.RunDirectory, "sources"));

        var supplied = new NpgsqlConnectionStringBuilder(
            plan.Environment.PostgreSqlAdministratorConnection);
        RequirePostgreSqlCredentials(supplied);
        if (StringComparer.Ordinal.Equals(supplied.Database, identity.DatabaseName))
        {
            throw new InvalidOperationException(
                "The supplied PostgreSQL administrator database must be distinct from the nonce-owned database.");
        }

        Story115ProcessResult candidateCommit = await ExecuteRequiredAsync(
            FindCommand(plan, "candidate-commit-inspect"),
            cancellationToken);
        Story115ProcessResult candidateTree = await ExecuteRequiredAsync(
            FindCommand(plan, "candidate-tree-inspect"),
            cancellationToken);
        if (!StringComparer.Ordinal.Equals(
                candidateCommit.StandardOutput.Trim(),
                plan.Environment.CandidateCommit) ||
            !StringComparer.Ordinal.Equals(
                candidateTree.StandardOutput.Trim(),
                plan.Environment.CandidateTree))
        {
            throw new InvalidDataException("The reviewed candidate identity drifted.");
        }

        await ExecuteRequiredAsync(
            FindCommand(plan, "candidate-driver-diff-inspect"),
            cancellationToken);
        Story115ProcessResult untrackedDriver = await ExecuteRequiredAsync(
            FindCommand(plan, "candidate-untracked-driver-inspect"),
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(untrackedDriver.StandardOutput))
        {
            throw new InvalidDataException("The provisioner driver source contains unreviewed files.");
        }
        Story115ProcessResult worktree = await ExecuteRequiredAsync(
            FindCommand(plan, "candidate-worktree-inspect"),
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(worktree.StandardOutput))
        {
            throw new InvalidDataException("The reviewed candidate worktree is not clean.");
        }

        await ExecuteRequiredAsync(
            FindCommand(plan, "provisioner-driver-publish"),
            cancellationToken,
            timeout: TimeSpan.FromMinutes(10));
        await ExecuteRequiredAsync(
            FindCommand(plan, "source-bound-image-build"),
            cancellationToken,
            timeout: TimeSpan.FromMinutes(45));
        Story115ProcessResult imageInspection = await ExecuteRequiredAsync(
            FindCommand(plan, "image-label-inspect"),
            cancellationToken);
        ParseAndVerifyImageInspection(imageInspection.StandardOutput, plan.Environment);
        Story115ProcessResult imageInventory = await ExecuteRequiredAsync(
            Story115ReleaseHarness.BindImmutableImageId(
                FindCommand(plan, "image-app-inventory-inspect"),
                _imageId!),
            cancellationToken);
        _imageAppInventory = Array.AsReadOnly(
        [
            .. imageInventory.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim()),
        ]);
        _driverAttestation = Story115ReleaseHarness.CreateProvisionerDriverAttestation(
            Path.Combine(plan.RunDirectory, "driver"),
            plan.Environment.CandidateCommit,
            _imageAppInventory);
        await AcquireRepositoryAsync(
            plan.Environment.AccountRepositoryUrl,
            Story115ReleaseHarness.Contract.AccountRepository,
            Path.Combine(plan.RunDirectory, "sources", "mbs_account"),
            cancellationToken);
        await AcquireRepositoryAsync(
            plan.Environment.ChannelRepositoryUrl,
            Story115ReleaseHarness.Contract.ChannelRepository,
            Path.Combine(plan.RunDirectory, "sources", "mbs_channel"),
            cancellationToken);
        await ExecuteRequiredAsync(FindCommand(plan, "network-create"), cancellationToken);
        await Story115ReleaseHarness.StartProviderAtReservedPortBoundaryAsync(
            plan.PortReservation,
            token => ExecuteRequiredAsync(
                MakeProviderFixtureAbsolute(FindCommand(plan, "provider-run")),
                token),
            cancellationToken);
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
                    await using var command = new NpgsqlCommand(
                        "SELECT current_setting('server_version_num')::integer;",
                        connection);
                    int serverVersionNumber = (int)(
                        await command.ExecuteScalarAsync(token) ?? 0);
                    Story115ReleaseHarness.RequirePostgreSql17ServerVersion(
                        serverVersionNumber);
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
            throw new TimeoutException("The supplied PostgreSQL 17 administrator endpoint did not become ready.");
        }

        var target = new NpgsqlConnectionStringBuilder(_administratorConnectionString)
        {
            Database = identity.DatabaseName,
        };
        _targetConnectionString = target.ConnectionString;
    }

    public async Task PreflightDockerDesktopPostgreSqlAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken)
    {
        Story115FinalRunPlan plan = RequirePlan(identity);
        var supplied = new NpgsqlConnectionStringBuilder(
            plan.Environment.PostgreSqlAdministratorConnection);
        RequirePostgreSqlCredentials(supplied);
        await ExecuteRequiredAsync(
            Story115ReleaseHarness.CreateDockerDesktopPostgreSqlPreflightCommand(
                identity,
                supplied.Port),
            cancellationToken,
            timeout: TimeSpan.FromSeconds(15));
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

        bool exists = await DatabaseExistsInCatalogAsync(identity, cancellationToken);
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

    public async Task<bool> ReconcileDatabaseExistsAfterAmbiguousCreateAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken)
    {
        bool exists = await DatabaseExistsInCatalogAsync(identity, cancellationToken);
        if (!exists)
        {
            _databaseAbsenceObserved = true;
            _cleanup["database-absence-proved"] = true;
        }

        return exists;
    }

    private async Task<bool> DatabaseExistsInCatalogAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken)
    {
        RequirePlan(identity);
        if (_administratorConnectionString is null)
        {
            throw new InvalidOperationException("PostgreSQL administration was not prepared.");
        }

        await using var connection = new NpgsqlConnection(_administratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            connection);
        command.Parameters.AddWithValue("name", identity.DatabaseName);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
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

    public async Task MigrateAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken)
    {
        Story115FinalRunPlan plan = RequirePlan(identity);
        if (_targetConnectionString is null || _imageId is null)
        {
            throw new InvalidOperationException("The nonce database or immutable image is unavailable.");
        }

        Story115ReleaseHarness.ValidateProvisionerDriverAttestation(
            Path.Combine(plan.RunDirectory, "driver"),
            plan.Environment.CandidateCommit,
            _imageAppInventory ?? throw new InvalidDataException(
                "The image inventory attestation is missing."),
            _driverAttestation);

        await WriteProvisionerInputAsync(plan, cancellationToken);
        Story115ExternalCommand provisioner = Story115ReleaseHarness.BindImmutableImageId(
            FindCommand(plan, "provisioner-run"),
            _imageId);
        await ExecuteRequiredAsync(
            provisioner,
            cancellationToken,
            timeout: Story115ReleaseHarness.ProvisionerProcessTimeout);
        Story115ProcessResult inspection = await ExecuteRequiredAsync(
            FindCommand(plan, "provisioner-image-inspect"),
            cancellationToken);
        RequireSameImageId(inspection.StandardOutput);
        _product = await ReadProvisionerResultAsync(plan, cancellationToken);
        await ExecuteRequiredAsync(FindCommand(plan, "provisioner-remove"), cancellationToken);
        Story115ProcessResult absence = await ExecuteAsync(
            FindCommand(plan, "provisioner-absence-inspect"),
            cancellationToken);
        if (absence.ExitCode == 0)
        {
            throw new InvalidOperationException("The provisioner still existed before app start.");
        }

        _cleanup["provisioner-removed-before-app"] = true;
        _provisioningFactory = new Story115ProvisioningFactory(
            _targetConnectionString,
            plan.KeyRingDirectory,
            Path.Combine(plan.RunDirectory, "work"));
        _ = _provisioningFactory.Services;
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

    public async Task RemoveProvisionerAsync(
        Story115RunIdentity identity,
        CancellationToken cancellationToken)
    {
        RequirePlan(identity);
        await RemoveExactContainerAsync(identity.ProvisionerContainer, cancellationToken);
        _cleanup["provisioner-removed"] = true;
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
        await ExecuteCleanupAsync(
            new("image-remove", "docker", ["image", "rm", "--force", ImageName(identity)]),
            cancellationToken);
        await ExecuteCleanupAsync(
            new("network-remove", "docker", ["network", "rm", identity.Network]),
            cancellationToken);
        await Story115ReleaseHarness.DeleteOwnedRunDirectoryAsync(
            plan.RunDirectory,
            cancellationToken);

        _cleanup["remaining-owned-state-removed"] = true;
    }

    private async Task ExecutePrimaryAsync(CancellationToken cancellationToken)
    {
        Story115FinalRunPlan plan = _plan!;
        Story115ProvisioningFactory factory = _provisioningFactory ??
            throw new InvalidOperationException("The container provisioner did not initialize product access.");
        _ = _product ?? throw new InvalidOperationException("The sanitized provisioner result is unavailable.");
        await WriteAppEnvironmentAsync(plan, cancellationToken);
        await ExecuteRequiredAsync(
            Story115ReleaseHarness.BindImmutableImageId(
                FindCommand(plan, "app-run"),
                _imageId!),
            cancellationToken);
        Story115ProcessResult appImage = await ExecuteRequiredAsync(
            FindCommand(plan, "app-image-inspect"),
            cancellationToken);
        RequireSameImageId(appImage.StandardOutput);

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
        Story115ContainerProductState product = _product ??
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
            ILocator claimLocators = page.Locator(".answer-claim");
            int claims = await claimLocators.CountAsync();
            var observedClaims = new List<Story115ObservedClaim>(claims);
            int citations = 0;
            for (int claimIndex = 0; claimIndex < claims; claimIndex++)
            {
                ILocator claim = claimLocators.Nth(claimIndex);
                ILocator citationLocators = claim.Locator(".document-citation");
                int nestedCitations = await citationLocators.CountAsync();
                var observedCitations = new List<Story115ObservedCitation>(nestedCitations);
                for (int citationIndex = 0; citationIndex < nestedCitations; citationIndex++)
                {
                    ILocator citation = citationLocators.Nth(citationIndex);
                    ILocator details = citation.Locator("details.document-citation__details");
                    ILocator summary = details.Locator("summary");
                    bool detailsOperated = await details.CountAsync() == 1 &&
                        await summary.CountAsync() == 1;
                    if (detailsOperated)
                    {
                        await summary.ClickAsync();
                        detailsOperated = await details.GetAttributeAsync("open") is not null &&
                            await details.Locator(".citation-locator").IsVisibleAsync();
                    }

                    string path = await citation.GetAttributeAsync(
                        "data-repository-relative-path") ?? string.Empty;
                    bool lineStartParsed = int.TryParse(
                        await citation.GetAttributeAsync("data-line-start"),
                        out int lineStart);
                    bool lineEndParsed = int.TryParse(
                        await citation.GetAttributeAsync("data-line-end"),
                        out int lineEnd);
                    observedCitations.Add(new Story115ObservedCitation(
                        path,
                        lineStartParsed ? lineStart : 0,
                        lineEndParsed ? lineEnd : 0,
                        detailsOperated));
                }

                citations += nestedCitations;
                observedClaims.Add(new Story115ObservedClaim(observedCitations.AsReadOnly()));
            }

            Story115ReleaseHarness.ValidateObservedAnswer(observedClaims);
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
        Story115ContainerProductState product)
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
            CreateProvisionerEvidence(),
            [Story115ReleaseHarness.Contract.AccountRepository,
                Story115ReleaseHarness.Contract.ChannelRepository],
            _product?.ProfileFingerprint,
            CreateRuntimePass2Evidence(),
            _commandEvidence.AsReadOnly(),
            _positiveAnswer,
            _negativeAuthorizationOutcome,
            new(plan.Identity.DatabaseName, _databaseDropped, _databaseAbsenceObserved),
            new ReadOnlyDictionary<string, bool>(_cleanup),
            Story115ReleaseHarness.KnownLimitations);

    private Story115ProvisionerReleaseEvidence? CreateProvisionerEvidence()
    {
        if (_driverAttestation is null)
        {
            return null;
        }

        return new(
            BuildCount: 1,
            ProvisionerImageId: _imageId,
            AppImageId: _imageId,
            ProvisionerRemovedBeforeAppStart: _cleanup.GetValueOrDefault(
                "provisioner-removed-before-app"),
            ProvisionerImageCreated: false,
            _driverAttestation.DriverInventorySha256,
            _driverAttestation.ImageAppInventorySha256,
            _driverAttestation.Files,
            [
                .. _driverAttestation.Files.Select(file => file.MountIdentity),
                "source/mbs_account->/vulgata/sources/mbs_account:readonly",
                "source/mbs_channel->/vulgata/sources/mbs_channel:readonly",
                "config/input.json->/vulgata/run/config/input.json:readonly",
                "output->/vulgata/run/output:writable",
                "work->/var/lib/vulgata-codegraph:writable",
                "key-ring->/var/lib/vulgata-dpkeys:provisioner-writable-app-readonly",
            ],
            _provisionerResultSha256);
    }

    private Story115RuntimePass2Evidence? CreateRuntimePass2Evidence()
    {
        if (_product is null)
        {
            return null;
        }

        return new(
            _product.RuntimeSupportFingerprint,
            _product.AccountRepositoryId,
            SupportedRepositoryCount: 2,
            PayloadSlotCount: Story115ProductOrchestrator.Pass2PayloadSlots.Count,
            DispatchCount: 1,
            CommitCount: 1);
    }

    private async Task AcquireRepositoryAsync(
        Uri source,
        PreparedRepositoryIdentity expected,
        string destination,
        CancellationToken cancellationToken)
    {
        Story115ProcessResult clone = await ExecuteRequiredAsync(
            Story115ReleaseHarness.CreateRepositoryAcquisitionCommand(
                source,
                expected,
                destination),
            cancellationToken,
            timeout: TimeSpan.FromMinutes(5));
        _ = clone;
        Story115ProcessResult head = await ExecuteRequiredAsync(
            new(
                "repository-verify-" + expected.Alias,
                "git",
                ["-C", destination, "rev-parse", "HEAD"]),
            cancellationToken);
        if (!StringComparer.Ordinal.Equals(head.StandardOutput.Trim(), expected.Commit))
        {
            throw new InvalidOperationException("A prepared Repository commit drifted.");
        }
    }

    private async Task WriteProvisionerInputAsync(
        Story115FinalRunPlan plan,
        CancellationToken cancellationToken)
    {
        var external = new NpgsqlConnectionStringBuilder(_targetConnectionString)
        {
            Host = "host.docker.internal",
            Pooling = true,
        };
        _administratorEmail = $"story115-admin-{plan.Identity.Nonce}@example.invalid";
        _ownerEmail = $"story115-owner-{plan.Identity.Nonce}@example.invalid";
        _negativeEmail = $"story115-negative-{plan.Identity.Nonce}@example.invalid";
        _administratorPassword = CreateGateSecret();
        _ownerPassword = CreateGateSecret();
        _negativePassword = CreateGateSecret();
        _providerApiKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        string json = JsonSerializer.Serialize(new
        {
            SchemaIdentity = "vulgata.story115.provisioner-input/1",
            RunNonce = plan.Identity.Nonce,
            ConnectionString = external.ConnectionString,
            AccountRepositoryUri = new Uri("file:///vulgata/sources/mbs_account"),
            ChannelRepositoryUri = new Uri("file:///vulgata/sources/mbs_channel"),
            WorkerEndpoint = $"http://127.0.0.1:{plan.Ports[0]}/v1",
            ChatEndpoint = $"http://127.0.0.1:{plan.Ports[1]}/v1",
            ProviderApiKey = _providerApiKey,
            DataProtectionApplicationName,
            AdministratorEmail = _administratorEmail,
            AdministratorPassword = _administratorPassword,
            OwnerEmail = _ownerEmail,
            OwnerPassword = _ownerPassword,
            NegativeEmail = _negativeEmail,
            NegativePassword = _negativePassword,
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await Story115ReleaseHarness.WriteCurrentUserOnlyFileAsync(
            Path.Combine(plan.RunDirectory, "config", "input.json"),
            json,
            cancellationToken);
    }

    private async Task<Story115ContainerProductState> ReadProvisionerResultAsync(
        Story115FinalRunPlan plan,
        CancellationToken cancellationToken)
    {
        string path = Path.Combine(plan.RunDirectory, "output", "result.json");
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        if (bytes.Length is <= 0 or > 65_536)
        {
            throw new InvalidDataException("The provisioner result size is invalid.");
        }

        string json = Encoding.UTF8.GetString(bytes);
        Story115ProvisionerResult result = Story115ReleaseHarness.ValidateProvisionerResult(
            json,
            plan.Identity.Nonce);
        _provisionerResultSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));

        return new(
            result.SystemId,
            result.AccountRepositoryId,
            result.ChannelRepositoryId,
            result.ProfileFingerprint,
            result.RuntimeSupportFingerprint,
            _ownerEmail!,
            _ownerPassword!,
            _negativeEmail!,
            _negativePassword!);
    }

    private void RequireSameImageId(string observed)
    {
        if (!StringComparer.Ordinal.Equals(observed.Trim(), _imageId))
        {
            throw new InvalidOperationException("A container did not use the inspected immutable image ID.");
        }
    }

    private static string CreateGateSecret() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(18)) + "aA1!";

    private async Task WriteAppEnvironmentAsync(
        Story115FinalRunPlan plan,
        CancellationToken cancellationToken)
    {
        var external = new NpgsqlConnectionStringBuilder(_targetConnectionString)
        {
            Host = "host.docker.internal",
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
        await Story115ReleaseHarness.WriteCurrentUserOnlyFileAsync(
            plan.AppEnvironmentFile,
            content,
            cancellationToken);
    }

    private void ParseAndVerifyImageInspection(
        string json,
        Story115FinalEnvironment environment)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement image = document.RootElement.EnumerateArray().Single();
        _imageId = image.GetProperty("Id").GetString() ??
            throw new InvalidOperationException("The source-bound image ID was absent.");
        if (!Regex.IsMatch(
                _imageId,
                "^sha256:[0-9a-f]{64}$",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("The source-bound image ID was not immutable sha256.");
        }
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

    private static FileSystemXmlRepository AssertIsFileSystemKeyRepository(IServiceProvider services) =>
        services
            .GetRequiredService<IOptions<KeyManagementOptions>>()
            .Value
            .XmlRepository as FileSystemXmlRepository ??
        throw new InvalidOperationException(
            "The Story 11.5 provisioning host did not use the configured key-ring directory.");

    private sealed class Story115ProvisioningConfigurationEnvironment : IDisposable
    {
        private static readonly object EnvironmentGate = new();
        private IReadOnlyDictionary<string, string?> _originalValues =
            new Dictionary<string, string?>(StringComparer.Ordinal);
        private IReadOnlyDictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private bool _disposed;

        public Story115ProvisioningConfigurationEnvironment(
            string keyRingDirectory,
            string codeGraphDirectory)
        {
            Monitor.Enter(EnvironmentGate);
            bool constructed = false;
            try
            {
                _values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["CodeGraph__DataRootPath"] = codeGraphDirectory,
                    ["DataProtection__ApplicationName"] = DataProtectionApplicationName,
                    ["DataProtection__KeyRingPath"] = keyRingDirectory,
                };
                _originalValues = _values.ToDictionary(
                    pair => pair.Key,
                    pair => Environment.GetEnvironmentVariable(pair.Key),
                    StringComparer.Ordinal);
                foreach ((string name, string value) in _values)
                {
                    Environment.SetEnvironmentVariable(name, value);
                }
                constructed = true;
            }
            catch (Exception exception)
            {
                Exception? restorationFailure = RestoreOriginalValues();
                if (restorationFailure is not null)
                {
                    throw new AggregateException(
                        "The Story 11.5 provisioning configuration could not be applied or restored.",
                        exception,
                        restorationFailure);
                }

                throw;
            }
            finally
            {
                if (!constructed)
                {
                    Monitor.Exit(EnvironmentGate);
                }
            }
        }

        public bool IsApplied => _values.All(pair => StringComparer.Ordinal.Equals(
            Environment.GetEnvironmentVariable(pair.Key), pair.Value));

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                Exception? restorationFailure = RestoreOriginalValues();
                if (restorationFailure is not null)
                {
                    throw restorationFailure;
                }
            }
            finally
            {
                _disposed = true;
                Monitor.Exit(EnvironmentGate);
            }
        }

        private Exception? RestoreOriginalValues()
        {
            var failures = new List<Exception>();
            foreach ((string name, string? value) in _originalValues)
            {
                try
                {
                    Environment.SetEnvironmentVariable(name, value);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            return failures.Count switch
            {
                0 => null,
                1 => failures[0],
                _ => new AggregateException(
                    "The Story 11.5 provisioning configuration could not be fully restored.",
                    failures),
            };
        }
    }

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
    Story115ProvisionerReleaseEvidence? Provisioner,
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
            new(
                1,
                "sha256:" + new string('1', 64),
                "sha256:" + new string('1', 64),
                true,
                false,
                new string('2', 64),
                new string('3', 64),
                [
                    .. Story115ReleaseHarness.ProvisionerDriverFileNames.Select(name =>
                        new Story115ProvisionerDriverFileAttestation(
                            name,
                            new string('4', 64),
                            $"driver/{name}->/app/{name}:readonly")),
                ],
                ["config/input.json->/vulgata/run/config/input.json:readonly"],
                new string('5', 64)),
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

internal sealed record Story115ProvisionerReleaseEvidence(
    int BuildCount,
    string? ProvisionerImageId,
    string? AppImageId,
    bool ProvisionerRemovedBeforeAppStart,
    bool ProvisionerImageCreated,
    string DriverInventorySha256,
    string ImageAppInventorySha256,
    IReadOnlyList<Story115ProvisionerDriverFileAttestation> DriverFiles,
    IReadOnlyList<string> MountIdentities,
    string? ResultSha256);

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

internal sealed record Story115ObservedClaim(
    IReadOnlyList<Story115ObservedCitation> Citations);

internal sealed record Story115ObservedCitation(
    string RepositoryRelativePath,
    int LineStart,
    int LineEnd,
    bool DetailsOperated);

internal sealed record Story115DatabaseEvidence(
    string Name,
    bool Dropped,
    bool AbsenceObserved);

internal sealed record Story115ProvisionerResult(
    string SchemaIdentity,
    string RunNonceHash,
    string MigrationOutcome,
    string ProductStageOutcome,
    Guid SystemId,
    Guid AccountRepositoryId,
    Guid ChannelRepositoryId,
    Guid AccountScanRunId,
    Guid ChannelScanRunId,
    Guid AccountScanExecutionId,
    Guid ChannelScanExecutionId,
    string AccountAlias,
    string AccountRef,
    string AccountCommit,
    string ChannelAlias,
    string ChannelRef,
    string ChannelCommit,
    string ProfileFingerprint,
    string RuntimeSupportFingerprint,
    int OrdinaryPass1DispatchCount,
    int Pass2DispatchCount,
    int Pass2CommitCount,
    string CodeGraphContextVersion,
    string CodeGraphRequirementsLockSha256,
    DateTimeOffset CompletedAtUtc,
    string ContentHash);

internal sealed record Story115ProvisionerDriverFileAttestation(
    string Name,
    string Sha256,
    string MountIdentity);

internal sealed record Story115ProvisionerDriverAttestation(
    string CandidateCommit,
    string DriverInventorySha256,
    string ImageAppInventorySha256,
    IReadOnlyList<Story115ProvisionerDriverFileAttestation> Files);

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
    internal static TimeSpan ProvisionerProcessTimeout { get; } = TimeSpan.FromMinutes(510);

    internal static Story115ExternalCommand CreateRepositoryAcquisitionCommand(
        Uri source,
        PreparedRepositoryIdentity expected,
        string destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        const string tagPrefix = "refs/tags/";
        string branch = expected.Ref.StartsWith(tagPrefix, StringComparison.Ordinal)
            ? expected.Ref[tagPrefix.Length..]
            : expected.Ref;
        if (string.IsNullOrWhiteSpace(branch))
        {
            throw new InvalidDataException(
                "The prepared Repository ref cannot select a Git branch or tag.");
        }

        return new(
            "repository-acquire-" + expected.Alias,
            "git",
            ["clone", "--quiet", "--branch", branch, "--single-branch", source.AbsoluteUri, destination]);
    }

    private const string ResourcePrefix = "vulgata-story115-";
    internal const string ImmutableImageIdPlaceholder = "{{FINAL_IMAGE_ID}}";
    internal static IReadOnlyList<string> ProvisionerDriverFileNames { get; } =
        Array.AsReadOnly(
        [
            "Vulgata.Story115.Provisioner.dll",
            "Vulgata.Story115.Provisioner.deps.json",
            "Vulgata.Story115.Provisioner.runtimeconfig.json",
        ]);

    internal static void ValidateObservedAnswer(
        IReadOnlyCollection<Story115ObservedClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        if (claims.Count == 0 || claims.Any(claim => claim.Citations.Count == 0))
        {
            throw new InvalidOperationException(
                "Every observed material claim must contain a nested Document Citation.");
        }

        foreach (Story115ObservedCitation citation in claims.SelectMany(claim => claim.Citations))
        {
            if (!citation.DetailsOperated ||
                !IsSafeRepositoryRelativePath(citation.RepositoryRelativePath) ||
                citation.LineStart <= 0 ||
                citation.LineEnd < citation.LineStart)
            {
                throw new InvalidOperationException(
                    "Every observed citation must expose operated, authorized source details.");
            }
        }
    }

    private static bool IsSafeRepositoryRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        return !normalized.StartsWith('/') &&
            !(normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':') &&
            !Uri.TryCreate(normalized, UriKind.Absolute, out _) &&
            segments.All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    internal const string ReleaseEvidencePath =
        "tests/Vulgata.Tests/TestResults/story-11-5-release-evidence.json";

    private static readonly JsonSerializerOptions _evidenceJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
    private static readonly JsonSerializerOptions _provisionerResultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
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
            ResourcePrefix + nonce + "-provider",
            ResourcePrefix + nonce + "-provisioner",
            ResourcePrefix + nonce + "-app",
            ResourcePrefix + nonce + "-network");
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

    internal static Story115ExternalCommand CreateDockerDesktopPostgreSqlPreflightCommand(
        Story115RunIdentity identity,
        int port)
    {
        RequireOwnedResourceName(identity.ProviderContainer);
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        string script =
            "import socket; host='host.docker.internal'; port=" +
            port.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            "; socket.getaddrinfo(host, port); s=socket.create_connection((host, port), 5); s.close()";
        return new(
            "provider-docker-desktop-postgres-preflight",
            "docker",
            ["exec", identity.ProviderContainer, "python", "-c", script]);
    }

    internal static Story115ExternalCommand BindImmutableImageId(
        Story115ExternalCommand command,
        string imageId)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!ImmutableImageIdRegex().IsMatch(imageId))
        {
            throw new InvalidOperationException(
                "The final application image must be bound by immutable sha256 ID.");
        }

        int tokenCount = command.Arguments.Count(argument =>
            StringComparer.Ordinal.Equals(argument, ImmutableImageIdPlaceholder));
        if (tokenCount != 1)
        {
            throw new InvalidOperationException(
                "The image-running command must contain exactly one immutable-ID token.");
        }

        return command with
        {
            Arguments = Array.AsReadOnly(
            [
                .. command.Arguments.Select(argument =>
                    StringComparer.Ordinal.Equals(argument, ImmutableImageIdPlaceholder)
                        ? imageId
                        : argument),
            ]),
        };
    }

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
                runDirectory,
                keyRingDirectory,
                appEnvironmentFile),
            ports);
        await operations.ExecuteAsync(plan, cancellationToken);
    }

    private static IReadOnlyList<Story115ExternalCommand> CreateExternalCommands(
        Story115FinalEnvironment environment,
        Story115RunIdentity identity,
        IReadOnlyList<int> ports,
        string runDirectory,
        string keyRingDirectory,
        string appEnvironmentFile)
    {
        string imageName = ResourcePrefix + identity.Nonce + "-image";
        string providerFixture = Contract.ProviderFixturePath.Replace('\\', '/');
        string provisionerRoot = Path.Combine(runDirectory, "driver");
        string provisionerProject = Path.Combine(
            "tests", "Vulgata.Tests", "ReleaseGate", "Provisioner",
            "Vulgata.Story115.Provisioner.csproj");
        string provisionerSourceRoot = Path.Combine(
            "tests", "Vulgata.Tests", "ReleaseGate", "Provisioner");
        string accountSource = Path.Combine(runDirectory, "sources", "mbs_account");
        string channelSource = Path.Combine(runDirectory, "sources", "mbs_channel");
        string configFile = Path.Combine(runDirectory, "config", "input.json");
        string outputDirectory = Path.Combine(runDirectory, "output");
        string workDirectory = Path.Combine(runDirectory, "work");
        return Array.AsReadOnly<Story115ExternalCommand>(
        [
            new(
                "candidate-commit-inspect",
                "git",
                ["rev-parse", "HEAD"]),
            new(
                "candidate-tree-inspect",
                "git",
                ["rev-parse", "HEAD^{tree}"]),
            new(
                "candidate-driver-diff-inspect",
                "git",
                ["diff", "--quiet", environment.CandidateCommit, "--", provisionerSourceRoot]),
            new(
                "candidate-untracked-driver-inspect",
                "git",
                ["ls-files", "--others", "--exclude-standard", "--", provisionerSourceRoot]),
            new(
                "candidate-worktree-inspect",
                "git",
                ["status", "--porcelain=v1", "--untracked-files=all"]),
            new(
                "provisioner-driver-publish",
                "dotnet",
                [
                    "publish", provisionerProject,
                    "--configuration", "Release",
                    "--no-restore",
                    "--output", provisionerRoot,
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
                "image-app-inventory-inspect",
                "docker",
                [
                    "run", "--rm", "--entrypoint", "/bin/sh",
                    ImmutableImageIdPlaceholder,
                    "-c", "find /app -maxdepth 1 -type f -printf '%f\\n' | LC_ALL=C sort",
                ]),
            new(
                "network-create",
                "docker",
                ["network", "create", identity.Network]),
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
                "provisioner-run",
                "docker",
                [
                    "run", "--name", identity.ProvisionerContainer,
                    "--network", $"container:{identity.ProviderContainer}",
                    "--entrypoint", "dotnet",
                    "--mount", $"type=bind,source={Path.Combine(provisionerRoot, "Vulgata.Story115.Provisioner.dll")},target=/app/Vulgata.Story115.Provisioner.dll,readonly",
                    "--mount", $"type=bind,source={Path.Combine(provisionerRoot, "Vulgata.Story115.Provisioner.deps.json")},target=/app/Vulgata.Story115.Provisioner.deps.json,readonly",
                    "--mount", $"type=bind,source={Path.Combine(provisionerRoot, "Vulgata.Story115.Provisioner.runtimeconfig.json")},target=/app/Vulgata.Story115.Provisioner.runtimeconfig.json,readonly",
                    "--mount", $"type=bind,source={accountSource},target=/vulgata/sources/mbs_account,readonly",
                    "--mount", $"type=bind,source={channelSource},target=/vulgata/sources/mbs_channel,readonly",
                    "--mount", $"type=bind,source={configFile},target=/vulgata/run/config/input.json,readonly",
                    "--mount", $"type=bind,source={outputDirectory},target=/vulgata/run/output",
                    "--mount", $"type=bind,source={workDirectory},target=/var/lib/vulgata-codegraph",
                    "--mount", $"type=bind,source={keyRingDirectory},target=/var/lib/vulgata-dpkeys",
                    ImmutableImageIdPlaceholder,
                    "/app/Vulgata.Story115.Provisioner.dll",
                    "--input", "/vulgata/run/config/input.json",
                    "--output", "/vulgata/run/output/result.json",
                ]),
            new(
                "provisioner-image-inspect",
                "docker",
                ["inspect", "--format", "{{.Image}}", identity.ProvisionerContainer]),
            new(
                "provisioner-remove",
                "docker",
                ["rm", identity.ProvisionerContainer]),
            new(
                "provisioner-absence-inspect",
                "docker",
                ["container", "inspect", identity.ProvisionerContainer]),
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
                    ImmutableImageIdPlaceholder,
                ]),
            new(
                "app-image-inspect",
                "docker",
                ["inspect", "--format", "{{.Image}}", identity.AppContainer]),
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

    internal static Task StartProviderAtReservedPortBoundaryAsync(
        Story115PortReservation reservation,
        Func<CancellationToken, Task> startProviderAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(startProviderAsync);
        cancellationToken.ThrowIfCancellationRequested();
        reservation.Release();
        return startProviderAsync(cancellationToken);
    }

    internal static void RequirePostgreSql17ServerVersion(int serverVersionNumber)
    {
        if (serverVersionNumber / 10_000 != 17)
        {
            throw new InvalidOperationException(
                "The supplied PostgreSQL administrator endpoint must use major version 17.");
        }
    }

    internal static async Task WriteCurrentUserOnlyFileAsync(
        string path,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The Story 11.5 final release harness requires Windows ACLs.");
        }

        string fullPath = Path.GetFullPath(path);
        await using (FileStream empty = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            await empty.FlushAsync(cancellationToken);
        }

        try
        {
            using WindowsIdentity current = WindowsIdentity.GetCurrent();
            SecurityIdentifier currentUser = current.User ??
                throw new InvalidOperationException("The current Windows user SID is unavailable.");
            var security = new FileSecurity();
            security.SetOwner(currentUser);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            new FileInfo(fullPath).SetAccessControl(security);
            if (!HasCurrentUserOnlyAccess(fullPath))
            {
                throw new UnauthorizedAccessException(
                    "The Story 11.5 app environment file ACL was not current-user-only.");
            }

            await File.WriteAllTextAsync(fullPath, content, cancellationToken);
        }
        catch
        {
            File.Delete(fullPath);
            throw;
        }
    }

    internal static bool HasCurrentUserOnlyAccess(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!OperatingSystem.IsWindows() || !File.Exists(path))
        {
            return false;
        }

        using WindowsIdentity current = WindowsIdentity.GetCurrent();
        SecurityIdentifier? currentUser = current.User;
        FileSecurity security = new FileInfo(Path.GetFullPath(path)).GetAccessControl();
        if (currentUser is null ||
            !security.AreAccessRulesProtected ||
            !currentUser.Equals(security.GetOwner(typeof(SecurityIdentifier))))
        {
            return false;
        }

        AuthorizationRuleCollection rules = security.GetAccessRules(
            includeExplicit: true,
            includeInherited: true,
            targetType: typeof(SecurityIdentifier));
        FileSystemAccessRule[] accessRules =
            [.. rules.OfType<FileSystemAccessRule>()];
        return accessRules.Length > 0 &&
            accessRules.All(rule =>
                currentUser.Equals(rule.IdentityReference) &&
                rule.AccessControlType == AccessControlType.Allow) &&
            accessRules.Any(rule =>
                (rule.FileSystemRights & (FileSystemRights.Read | FileSystemRights.Write)) ==
                (FileSystemRights.Read | FileSystemRights.Write));
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

    internal static async Task DeleteOwnedRunDirectoryAsync(
        string runDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runDirectory);
        string fullPath = Path.GetFullPath(runDirectory);
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(temporaryRoot, comparison) ||
            !OwnedRunDirectoryNameRegex().IsMatch(Path.GetFileName(fullPath)))
        {
            throw new InvalidOperationException(
                "Refusing to delete a directory outside this Story 11.5 nonce run root.");
        }

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!Directory.Exists(fullPath))
                {
                    return;
                }

                foreach (string file in Directory.EnumerateFiles(
                    fullPath,
                    "*",
                    new EnumerationOptions
                    {
                        AttributesToSkip = FileAttributes.ReparsePoint,
                        IgnoreInaccessible = false,
                        RecurseSubdirectories = true,
                    }))
                {
                    File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                }

                Directory.Delete(fullPath, recursive: true);
                return;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                if (attempt < 5)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                }
            }
        }

        throw new IOException("The Story 11.5 nonce run directory could not be removed.");
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
        bool databaseAbsenceProvenBeforeCreate = false;
        bool databaseCreateOutcomeIndeterminate = false;
        bool databaseOwnershipProven = false;
        try
        {
            await operations.PrepareInfrastructureAsync(identity, cancellationToken);
            await operations.PreflightDockerDesktopPostgreSqlAsync(identity, cancellationToken);
            await operations.ProveDatabaseAbsentAsync(
                identity,
                afterDrop: false,
                cancellationToken);
            databaseAbsenceProvenBeforeCreate = true;
            databaseCreateOutcomeIndeterminate = true;
            await operations.CreateDatabaseAsync(identity, cancellationToken);
            databaseCreateOutcomeIndeterminate = false;
            databaseOwnershipProven = true;
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
                () => operations.RemoveProvisionerAsync(identity, CancellationToken.None),
                cleanupFailures);
            await CaptureCleanupFailureAsync(
                () => operations.RemoveAppAndProviderAsync(identity, CancellationToken.None),
                cleanupFailures);
            if (databaseAbsenceProvenBeforeCreate &&
                databaseCreateOutcomeIndeterminate &&
                !databaseOwnershipProven)
            {
                try
                {
                    databaseOwnershipProven = await operations
                        .ReconcileDatabaseExistsAfterAmbiguousCreateAsync(
                            identity,
                            CancellationToken.None);
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(exception);
                }
            }
            if (databaseOwnershipProven)
            {
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
            }
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

    internal static void ValidateSanitizedProvisionerResult(string json) =>
        ValidateSanitizedJson(json);

    internal static Story115ProvisionerResult ValidateProvisionerResult(
        string json,
        string runNonce)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(runNonce);
        if (Encoding.UTF8.GetByteCount(json) is <= 0 or > 65_536)
        {
            throw new InvalidDataException("The provisioner result contract is invalid.");
        }

        ValidateSanitizedProvisionerResult(json);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The provisioner result contract is invalid.",
                exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("The provisioner result contract is invalid.");
            }

        string[] expectedProperties =
        [
            "schemaIdentity",
            "runNonceHash",
            "migrationOutcome",
            "productStageOutcome",
            "systemId",
            "accountRepositoryId",
            "channelRepositoryId",
            "accountScanRunId",
            "channelScanRunId",
            "accountScanExecutionId",
            "channelScanExecutionId",
            "accountAlias",
            "accountRef",
            "accountCommit",
            "channelAlias",
            "channelRef",
            "channelCommit",
            "profileFingerprint",
            "runtimeSupportFingerprint",
            "ordinaryPass1DispatchCount",
            "pass2DispatchCount",
            "pass2CommitCount",
            "codeGraphContextVersion",
            "codeGraphRequirementsLockSha256",
            "completedAtUtc",
            "contentHash",
        ];
        string[] actualProperties = document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        if (actualProperties.Length != expectedProperties.Length ||
            actualProperties.Distinct(StringComparer.Ordinal).Count() != actualProperties.Length ||
                !actualProperties.All(expectedProperties.Contains))
            {
                throw new InvalidDataException("The provisioner result contract is invalid.");
            }
        }

        Story115ProvisionerResult result;
        try
        {
            result = JsonSerializer.Deserialize<Story115ProvisionerResult>(
                json,
                _provisionerResultJsonOptions) ??
                throw new InvalidDataException("The provisioner result contract is invalid.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The provisioner result contract is invalid.",
                exception);
        }

        Guid[] identities =
        [
            result.SystemId,
            result.AccountRepositoryId,
            result.ChannelRepositoryId,
            result.AccountScanRunId,
            result.ChannelScanRunId,
            result.AccountScanExecutionId,
            result.ChannelScanExecutionId,
        ];
        string expectedNonceHash = HashText(runNonce);
        if (!StringComparer.Ordinal.Equals(
                result.SchemaIdentity,
                "vulgata.story115.provisioner-result/1") ||
            !FixedTimeHexEquals(result.RunNonceHash, expectedNonceHash) ||
            !StringComparer.Ordinal.Equals(result.MigrationOutcome, "applied") ||
            !StringComparer.Ordinal.Equals(result.ProductStageOutcome, "completed") ||
            identities.Any(identity => identity == Guid.Empty) ||
            identities.Distinct().Count() != identities.Length ||
            !StringComparer.Ordinal.Equals(result.AccountAlias, Contract.AccountRepository.Alias) ||
            !StringComparer.Ordinal.Equals(result.AccountRef, Contract.AccountRepository.Ref) ||
            !StringComparer.Ordinal.Equals(result.AccountCommit, Contract.AccountRepository.Commit) ||
            !StringComparer.Ordinal.Equals(result.ChannelAlias, Contract.ChannelRepository.Alias) ||
            !StringComparer.Ordinal.Equals(result.ChannelRef, Contract.ChannelRepository.Ref) ||
            !StringComparer.Ordinal.Equals(result.ChannelCommit, Contract.ChannelRepository.Commit) ||
            !IsLowerHex(result.ProfileFingerprint, 64) ||
            !IsLowerHex(result.RuntimeSupportFingerprint, 64) ||
            result.OrdinaryPass1DispatchCount != 2 ||
            result.Pass2DispatchCount != 1 ||
            result.Pass2CommitCount != 1 ||
            !StringComparer.Ordinal.Equals(result.CodeGraphContextVersion, "0.5.1") ||
            !StringComparer.Ordinal.Equals(
                result.CodeGraphRequirementsLockSha256,
                "10f09b78b02c886688c726bb93030380cfe54afc23798db24f6f4fc4cc4c7d95") ||
            result.CompletedAtUtc == default ||
            result.CompletedAtUtc.Offset != TimeSpan.Zero ||
            !IsLowerHex(result.ContentHash, 64))
        {
            throw new InvalidDataException("The provisioner result contract is invalid.");
        }

        string expectedContentHash = HashText(JsonSerializer.Serialize(
            result with { ContentHash = string.Empty },
            _provisionerResultJsonOptions));
        if (!FixedTimeHexEquals(result.ContentHash, expectedContentHash))
        {
            throw new InvalidDataException("The provisioner result contract is invalid.");
        }

        return result;
    }

    internal static Story115ProvisionerDriverAttestation CreateProvisionerDriverAttestation(
        string driverRoot,
        string candidateCommit,
        IReadOnlyCollection<string> imageAppInventory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverRoot);
        ArgumentNullException.ThrowIfNull(imageAppInventory);
        if (!IsLowerHex(candidateCommit, 40))
        {
            throw new InvalidDataException("The provisioner driver candidate is invalid.");
        }

        string[] imageNames = ValidateImageAppInventory(imageAppInventory);
        if (ProvisionerDriverFileNames.Any(name => imageNames.Contains(name, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("The provisioner driver collides with the image inventory.");
        }

        string root = Path.GetFullPath(driverRoot);
        string[] unexpectedVulgataFiles = Directory.EnumerateFiles(root)
            .Select(Path.GetFileName)
            .Where(name => name is not null &&
                name.StartsWith("Vulgata.", StringComparison.Ordinal) &&
                !ProvisionerDriverFileNames.Contains(name, StringComparer.Ordinal))
            .ToArray()!;
        if (unexpectedVulgataFiles.Length != 0)
        {
            throw new InvalidDataException("The provisioner driver inventory is not closed.");
        }

        var files = new List<Story115ProvisionerDriverFileAttestation>(3);
        foreach (string name in ProvisionerDriverFileNames)
        {
            string path = Path.Combine(root, name);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is <= 0 or > 16 * 1024 * 1024 ||
                info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException("The provisioner driver inventory is invalid.");
            }

            string sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            files.Add(new(
                name,
                sha256,
                $"driver/{name}->/app/{name}:readonly"));
        }

        string imageInventorySha256 = HashClosedStringInventory(
            "VULGATA-STORY115-IMAGE-APP-INVENTORY",
            imageNames);
        using IncrementalHash inventory = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        inventory.AppendData(Encoding.ASCII.GetBytes("VULGATA-STORY115-PROVISIONER-DRIVERS\0\x01"));
        inventory.AppendData(Convert.FromHexString(candidateCommit));
        foreach (Story115ProvisionerDriverFileAttestation file in files)
        {
            inventory.AppendData(Convert.FromHexString(file.Sha256));
        }
        inventory.AppendData(Convert.FromHexString(imageInventorySha256));
        return new(
            candidateCommit,
            Convert.ToHexStringLower(inventory.GetHashAndReset()),
            imageInventorySha256,
            files.AsReadOnly());
    }

    internal static void ValidateProvisionerDriverAttestation(
        string driverRoot,
        string candidateCommit,
        IReadOnlyCollection<string> imageAppInventory,
        Story115ProvisionerDriverAttestation? attestation)
    {
        if (attestation is null)
        {
            throw new InvalidDataException("The provisioner driver attestation is missing.");
        }

        Story115ProvisionerDriverAttestation current = CreateProvisionerDriverAttestation(
            driverRoot,
            candidateCommit,
            imageAppInventory);
        if (!StringComparer.Ordinal.Equals(attestation.CandidateCommit, current.CandidateCommit) ||
            !FixedTimeHexEquals(attestation.DriverInventorySha256, current.DriverInventorySha256) ||
            !FixedTimeHexEquals(
                attestation.ImageAppInventorySha256,
                current.ImageAppInventorySha256) ||
            attestation.Files.Count != current.Files.Count ||
            !attestation.Files.SequenceEqual(current.Files))
        {
            throw new InvalidDataException("The provisioner driver attestation is stale.");
        }
    }

    private static string[] ValidateImageAppInventory(
        IReadOnlyCollection<string> imageAppInventory)
    {
        string[] names = imageAppInventory.Order(StringComparer.Ordinal).ToArray();
        if (names.Length == 0 ||
            names.Distinct(StringComparer.Ordinal).Count() != names.Length ||
            names.Any(name => string.IsNullOrWhiteSpace(name) ||
                Path.GetFileName(name) != name ||
                name.Contains('/') ||
                name.Contains('\\')))
        {
            throw new InvalidDataException("The image /app inventory is invalid.");
        }

        return names;
    }

    private static string HashClosedStringInventory(string domain, IReadOnlyList<string> values)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes(domain + "\0\x01"));
        Span<byte> length = stackalloc byte[4];
        foreach (string value in values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static bool IsLowerHex(string? value, int length) =>
        value is not null && value.Length == length && value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static bool FixedTimeHexEquals(string actual, string expected) =>
        IsLowerHex(actual, expected.Length) &&
        CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(actual),
            Convert.FromHexString(expected));

    private static string HashText(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static ReadOnlyCollection<string> ReadOnly(params string[] values) =>
        Array.AsReadOnly(values);

    [GeneratedRegex("^vulgata_story115_[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnedDatabaseNameRegex();

    [GeneratedRegex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ImmutableImageIdRegex();

    [GeneratedRegex(
        "^vulgata-story115-[0-9a-f]{32}-(postgres|provider|provisioner|app|network|volume)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex OwnedResourceNameRegex();

    [GeneratedRegex("^vulgata-story115-[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnedRunDirectoryNameRegex();
}
