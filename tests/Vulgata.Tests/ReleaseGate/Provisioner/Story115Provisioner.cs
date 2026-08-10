using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Shared.Repositories;
using Vulgata.Web.Data;
using Vulgata.Web.Validators;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Story115.Provisioner;

public static class Story115Provisioner
{
    public const int MaximumInputBytes = 65_536;
    public const int MaximumResultBytes = 65_536;
    public const int WholeRepositoryProcessTimeoutSeconds = 14_400;
    private const string _inputSchema = "vulgata.story115.provisioner-input/1";
    private const string _resultSchema = "vulgata.story115.provisioner-result/1";
    private const string _contractSchema = "vulgata.story115.provisioner-contract/1";
    private const string _accountAlias = "mbs_account";
    private const string _channelAlias = "mbs_channel";
    private const string _acceptedRef = "refs/tags/vulgata/August15th-sample";
    private const string _accountCommit = "19c60cd3f3976b7697c487c7bafd96142e3a7625";
    private const string _channelCommit = "47f6b627a7598f862873610413eec1e1e7bf46bf";
    private const string _codeGraphVersion = "0.5.1";
    private const string _codeGraphLockSha256 =
        "10f09b78b02c886688c726bb93030380cfe54afc23798db24f6f4fc4cc4c7d95";
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
    };
    private static readonly string[] _imageProductAssemblyAllowlist =
    [
        "Vulgata.Web",
        "Vulgata.Core",
        "Vulgata.Infrastructure",
        "Vulgata.Agents",
        "Vulgata.Shared",
        "Vulgata.Web.ViewModels",
    ];
    private static readonly string[] _mountedRepositorySafeDirectories =
    [
        "/vulgata/sources/mbs_account/.git",
        "/vulgata/sources/mbs_channel/.git",
    ];
    private static int _imageProductAssemblyResolverInstalled;

    public static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 1 &&
            StringComparer.Ordinal.Equals(arguments[0], "--describe-contract"))
        {
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(CreateContract(), _jsonOptions));
            return 0;
        }

        int failureExitCode = 10;
        try
        {
            (string inputPath, string outputPath) = ParseArguments(arguments);
            ProvisionerInput input = await ReadInputAsync(inputPath, cancellationToken);
            InstallImageProductAssemblyResolver();
            failureExitCode = 11;
            RequireImageRuntime();
            ConfigureMountedRepositorySafety();
            ProductRunState state = await ExecuteProductPathAsync(
                input,
                code => failureExitCode = code,
                cancellationToken);
            failureExitCode = 40;
            await WriteResultAsync(outputPath, input, state, cancellationToken);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // The process boundary must replace every product failure with one fixed diagnostic.
        catch (Exception)
        {
            await Console.Error.WriteLineAsync("Story 11.5 provisioning failed.");
            return failureExitCode;
        }
#pragma warning restore CA1031
    }

    private static object CreateContract() => new
    {
        SchemaIdentity = _contractSchema,
        ProductAssemblyRoot = "/app",
        ImageProductAssemblyAllowlist = _imageProductAssemblyAllowlist,
        MountedRepositorySafeDirectories = _mountedRepositorySafeDirectories,
        CodeGraphContextVersion = _codeGraphVersion,
        WholeRepositoryProcessTimeoutSeconds,
        MaximumInputBytes,
        MaximumResultBytes,
        InputSchemaIdentity = _inputSchema,
        InputProperties = new[]
        {
            nameof(ProvisionerInput.SchemaIdentity),
            nameof(ProvisionerInput.RunNonce),
            nameof(ProvisionerInput.ConnectionString),
            nameof(ProvisionerInput.AccountRepositoryUri),
            nameof(ProvisionerInput.ChannelRepositoryUri),
            nameof(ProvisionerInput.WorkerEndpoint),
            nameof(ProvisionerInput.ChatEndpoint),
            nameof(ProvisionerInput.ProviderApiKey),
            nameof(ProvisionerInput.DataProtectionApplicationName),
            nameof(ProvisionerInput.AdministratorEmail),
            nameof(ProvisionerInput.AdministratorPassword),
            nameof(ProvisionerInput.OwnerEmail),
            nameof(ProvisionerInput.OwnerPassword),
            nameof(ProvisionerInput.NegativeEmail),
            nameof(ProvisionerInput.NegativePassword),
        },
        ResultSchemaIdentity = _resultSchema,
        ResultPublication = "temp-file-atomic-rename-no-overwrite",
        ResultProperties = new[]
        {
            nameof(ProvisionerResult.SchemaIdentity),
            nameof(ProvisionerResult.RunNonceHash),
            nameof(ProvisionerResult.MigrationOutcome),
            nameof(ProvisionerResult.ProductStageOutcome),
            nameof(ProvisionerResult.SystemId),
            nameof(ProvisionerResult.AccountRepositoryId),
            nameof(ProvisionerResult.ChannelRepositoryId),
            nameof(ProvisionerResult.AccountScanRunId),
            nameof(ProvisionerResult.ChannelScanRunId),
            nameof(ProvisionerResult.AccountScanExecutionId),
            nameof(ProvisionerResult.ChannelScanExecutionId),
            nameof(ProvisionerResult.AccountAlias),
            nameof(ProvisionerResult.AccountRef),
            nameof(ProvisionerResult.AccountCommit),
            nameof(ProvisionerResult.ChannelAlias),
            nameof(ProvisionerResult.ChannelRef),
            nameof(ProvisionerResult.ChannelCommit),
            nameof(ProvisionerResult.ProfileFingerprint),
            nameof(ProvisionerResult.RuntimeSupportFingerprint),
            nameof(ProvisionerResult.OrdinaryPass1DispatchCount),
            nameof(ProvisionerResult.Pass2DispatchCount),
            nameof(ProvisionerResult.Pass2CommitCount),
            nameof(ProvisionerResult.CodeGraphContextVersion),
            nameof(ProvisionerResult.CodeGraphRequirementsLockSha256),
            nameof(ProvisionerResult.CompletedAtUtc),
            nameof(ProvisionerResult.ContentHash),
        },
        AcceptedProfiledRevisions = new[]
        {
            $"{_accountAlias}|{_acceptedRef}|{_accountCommit}",
            $"{_channelAlias}|{_acceptedRef}|{_channelCommit}",
        },
        CodeGraphRequirementsLockSha256 = _codeGraphLockSha256,
        OrdinaryPass1DispatchCount = 2,
        RuntimeFlowRepositoryCount = 2,
        Pass2DispatchCount = 1,
        Pass2CommitCount = 1,
        MountCandidates = new[]
        {
            "Vulgata.Story115.Provisioner.dll",
            "Vulgata.Story115.Provisioner.deps.json",
            "Vulgata.Story115.Provisioner.runtimeconfig.json",
        },
        RequiredProductServices = new[]
        {
            "UserManager<ApplicationUser>",
            "RoleSeeder",
            "IAdministratorRoleCoordinator",
            "ISystemRepository",
            "ISystemOwnershipCoordinator",
            "IRepositoryManagementCoordinator",
            "ILlmProviderManagementCoordinator",
            "ISystemLlmProviderOverrideCoordinator",
            "IRepositoryProfilingCoordinator",
            "SequentialScanRunner",
            "ExactCrossRepositoryResolutionCoordinator",
            "IPreparedDemoPass2FlowPlanner",
        },
        Pass2PayloadSlots = new[]
        {
            nameof(Pass2BusinessLogicJobPayload.PreparedFlowSchemaIdentity),
            nameof(Pass2BusinessLogicJobPayload.ManifestId),
            nameof(Pass2BusinessLogicJobPayload.FlowId),
            nameof(Pass2BusinessLogicJobPayload.SystemId),
            nameof(Pass2BusinessLogicJobPayload.RepositoryId),
            nameof(Pass2BusinessLogicJobPayload.TraceEvidence),
            nameof(Pass2BusinessLogicJobPayload.CodeLogicDocumentVersions),
        },
    };

    private static (string InputPath, string OutputPath) ParseArguments(
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 4 ||
            !StringComparer.Ordinal.Equals(arguments[0], "--input") ||
            !StringComparer.Ordinal.Equals(arguments[2], "--output"))
        {
            throw new InvalidDataException("The provisioner arguments are invalid.");
        }

        string input = Path.GetFullPath(arguments[1]);
        string output = Path.GetFullPath(arguments[3]);
        if (!StringComparer.Ordinal.Equals(Path.GetFileName(output), "result.json") ||
            StringComparer.Ordinal.Equals(input, output))
        {
            throw new InvalidDataException("The provisioner paths are invalid.");
        }

        return (input, output);
    }

    private static async Task<ProvisionerInput> ReadInputAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length is <= 0 or > MaximumInputBytes)
        {
            throw new InvalidDataException("The provisioner input is invalid.");
        }

        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The provisioner input is invalid.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw new InvalidDataException("The provisioner input is invalid.");
            }
        }

        ProvisionerInput input = JsonSerializer.Deserialize<ProvisionerInput>(bytes, _jsonOptions) ??
            throw new InvalidDataException("The provisioner input is invalid.");
        input.Validate();
        return input;
    }

    private static void RequireImageRuntime()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("The provisioner requires the qualified Linux image.");
        }

        string[] productAssemblies =
        [
            typeof(VulgataWebComposition).Assembly.Location,
            typeof(ISystemRepository).Assembly.Location,
            typeof(VulgataDbContext).Assembly.Location,
            typeof(Pass2BusinessLogicJobPayload).Assembly.Location,
            typeof(RoleNames).Assembly.Location,
        ];
        if (productAssemblies.Any(path =>
            !StringComparer.Ordinal.Equals(Path.GetDirectoryName(path), "/app")))
        {
            throw new InvalidOperationException("Product assemblies must resolve from /app.");
        }

        string lockPath = "/opt/codegraph/requirements.lock";
        if (!File.Exists(lockPath) ||
            !StringComparer.Ordinal.Equals(HashFile(lockPath), _codeGraphLockSha256))
        {
            throw new InvalidOperationException("The image CodeGraph lock is invalid.");
        }

        var startInfo = new ProcessStartInfo("cgc", "version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("The image CodeGraph runtime is unavailable.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 ||
            !(output + error).Contains(_codeGraphVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The image CodeGraph runtime identity is invalid.");
        }
    }

    private static void ConfigureMountedRepositorySafety()
    {
        foreach (string directory in _mountedRepositorySafeDirectories)
        {
            var startInfo = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("config");
            startInfo.ArgumentList.Add("--global");
            startInfo.ArgumentList.Add("--add");
            startInfo.ArgumentList.Add("safe.directory");
            startInfo.ArgumentList.Add(directory);
            using Process process = Process.Start(startInfo) ??
                throw new InvalidOperationException("The image Git runtime is unavailable.");
            if (!process.WaitForExit(5_000))
            {
                process.Kill(entireProcessTree: true);
                throw new InvalidOperationException("The image Git safety configuration timed out.");
            }

            _ = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("The image Git safety configuration failed.");
            }
        }
    }

    private static void InstallImageProductAssemblyResolver()
    {
        if (!OperatingSystem.IsLinux() ||
            Interlocked.Exchange(ref _imageProductAssemblyResolverInstalled, 1) != 0)
        {
            return;
        }

        System.Runtime.Loader.AssemblyLoadContext.Default.Resolving +=
            ResolveImageProductAssembly;
    }

    private static System.Reflection.Assembly? ResolveImageProductAssembly(
        System.Runtime.Loader.AssemblyLoadContext context,
        System.Reflection.AssemblyName assemblyName)
    {
        string? name = assemblyName.Name;
        if (name is null ||
            !_imageProductAssemblyAllowlist.Contains(name, StringComparer.Ordinal))
        {
            return null;
        }

        string path = Path.Combine("/app", name + ".dll");
        return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
    }

    private static async Task<ProductRunState> ExecuteProductPathAsync(
        ProvisionerInput input,
        Action<int> reportStage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reportStage);
        reportStage(20);
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(VulgataWebComposition).Assembly.GetName().Name,
            EnvironmentName = Environments.Production,
            ContentRootPath = "/app",
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = input.ConnectionString,
            ["CodeGraph:DataRootPath"] = "/var/lib/vulgata-codegraph",
            ["CodeGraph:WholeRepositoryProcessTimeoutSeconds"] =
                WholeRepositoryProcessTimeoutSeconds.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
            ["DataProtection:KeyRingPath"] = "/var/lib/vulgata-dpkeys",
            ["DataProtection:ApplicationName"] = input.DataProtectionApplicationName,
        });
        VulgataWebComposition.ConfigureServices(builder);
        await using ServiceProvider provider = builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await services.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<VulgataDbContext>().Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<RoleSeeder>().SeedAsync();

        reportStage(21);
        UserManager<ApplicationUser> users = services.GetRequiredService<UserManager<ApplicationUser>>();
        IAdministratorRoleCoordinator roles = services.GetRequiredService<IAdministratorRoleCoordinator>();
        ApplicationUser administrator = await CreateUserAsync(
            users, input.AdministratorEmail, input.AdministratorPassword, cancellationToken);
        RequireIdentity(await roles.AssignInitialRoleAsync(administrator, cancellationToken));
        ApplicationUser owner = await CreateUserAsync(
            users, input.OwnerEmail, input.OwnerPassword, cancellationToken);
        RequireIdentity(await roles.AssignInitialRoleAsync(owner, cancellationToken));
        ApplicationUser negative = await CreateUserAsync(
            users, input.NegativeEmail, input.NegativePassword, cancellationToken);
        RequireIdentity(await roles.AssignInitialRoleAsync(negative, cancellationToken));

        reportStage(22);
        ISystemRepository systemRepository = services.GetRequiredService<ISystemRepository>();
        var system = new SystemEntity(
            $"Story 11.5 {input.RunNonce}",
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
            throw new InvalidOperationException("System ownership provisioning failed.");
        }

        reportStage(23);
        var ownerContext = new ProvisionerUserContext(owner.Id, false);
        IRepositoryManagementCoordinator repositories =
            services.GetRequiredService<IRepositoryManagementCoordinator>();
        var account = await repositories.CreateAsync(system.Id, new CreateRepositoryRequest
        {
            Name = _accountAlias,
            GitUrl = input.AccountRepositoryUri.AbsoluteUri,
        }, ownerContext, cancellationToken);
        var channel = await repositories.CreateAsync(system.Id, new CreateRepositoryRequest
        {
            Name = _channelAlias,
            GitUrl = input.ChannelRepositoryUri.AbsoluteUri,
        }, ownerContext, cancellationToken);
        if (account.Outcome != RepositoryMutationOutcome.Success || account.Value is null ||
            channel.Outcome != RepositoryMutationOutcome.Success || channel.Value is null)
        {
            throw new InvalidOperationException("Repository provisioning failed.");
        }

        reportStage(24);
        ILlmProviderManagementCoordinator providerCoordinator =
            services.GetRequiredService<ILlmProviderManagementCoordinator>();
        var worker = await providerCoordinator.CreateAsync(
            ProviderRequest(input, "worker", input.WorkerEndpoint, AgentType.Worker), cancellationToken);
        var chat = await providerCoordinator.CreateAsync(
            ProviderRequest(input, "chat", input.ChatEndpoint, AgentType.Chat), cancellationToken);
        if (worker.Outcome != LlmProviderMutationOutcome.Success || worker.Value is null ||
            chat.Outcome != LlmProviderMutationOutcome.Success || chat.Value is null)
        {
            throw new InvalidOperationException("Provider provisioning failed.");
        }

        reportStage(25);
        ISystemLlmProviderOverrideCoordinator overrides =
            services.GetRequiredService<ISystemLlmProviderOverrideCoordinator>();
        await RequireOverrideAsync(overrides.UpsertAsync(
            system.Id, null, OverrideRequest(system.Id, worker.Value.Id, AgentType.Worker),
            ownerContext, cancellationToken));
        await RequireOverrideAsync(overrides.UpsertAsync(
            system.Id, null, OverrideRequest(system.Id, chat.Value.Id, AgentType.Chat),
            ownerContext, cancellationToken));

        reportStage(26);
        IRepositoryProfilingCoordinator profiling =
            services.GetRequiredService<IRepositoryProfilingCoordinator>();
        RepositoryProfilingResult accountProfile = await profiling.StartSystemRepositoryAsync(
            system.Id, account.Value.Id, ownerContext, cancellationToken);
        RepositoryProfilingResult channelProfile = await profiling.StartSystemRepositoryAsync(
            system.Id, channel.Value.Id, ownerContext, cancellationToken);
        if (accountProfile.Outcome != RepositoryProfilingOutcome.Success || accountProfile.Value is null ||
            channelProfile.Outcome != RepositoryProfilingOutcome.Success || channelProfile.Value is null)
        {
            throw new InvalidOperationException("Repository profiling failed.");
        }

        reportStage(27);
        IApprovedPreScanHandoffSource handoffs =
            services.GetRequiredService<IApprovedPreScanHandoffSource>();
        ApprovedPreScanHandoff accountHandoff = await handoffs.GetAsync(
            account.Value.Id, accountProfile.Value.Id, cancellationToken) ??
            throw new InvalidOperationException("The account Profiled Revision is unavailable.");
        ApprovedPreScanHandoff channelHandoff = await handoffs.GetAsync(
            channel.Value.Id, channelProfile.Value.Id, cancellationToken) ??
            throw new InvalidOperationException("The channel Profiled Revision is unavailable.");
        if (!StringComparer.Ordinal.Equals(accountHandoff.ProfiledRevision, _accountCommit) ||
            !StringComparer.Ordinal.Equals(channelHandoff.ProfiledRevision, _channelCommit))
        {
            throw new InvalidOperationException("The accepted Profiled Revisions drifted.");
        }

        reportStage(28);
        var verifier = ActivatorUtilities.CreateInstance<RepositoryAuthorizationVerifier>(
            services, ownerContext);
        var access = new RepositoryAccessScope(
            [account.Value.Id, channel.Value.Id], verifier);
        SequentialScanRunner runner = services.GetRequiredService<SequentialScanRunner>();
        Guid accountExecution = await runner.StartAsync(
            account.Value.Id, accountProfile.Value.Id, system.Id, cancellationToken);
        Guid channelExecution = await runner.StartAsync(
            channel.Value.Id, channelProfile.Value.Id, system.Id, cancellationToken);
        await runner.RunPass1Async(accountExecution, access, cancellationToken);
        await runner.RunPass1Async(channelExecution, access, cancellationToken);

        reportStage(29);
        ExactResolutionProfile profile = services.GetRequiredService<ExactResolutionProfileCatalog>().Load();
        ExactResolutionRuntimeRequest request = ExactResolutionRuntimeRequest.Create(system.Id, profile,
        [
            new(_accountAlias, profile.Repositories.Single(value => value.Alias == _accountAlias).StableGitIdentity,
                account.Value.Id, accountProfile.Value.Id, accountExecution),
            new(_channelAlias, profile.Repositories.Single(value => value.Alias == _channelAlias).StableGitIdentity,
                channel.Value.Id, channelProfile.Value.Id, channelExecution),
        ]);
        ExactCrossRepositoryResolutionCoordinator exact =
            ActivatorUtilities.CreateInstance<ExactCrossRepositoryResolutionCoordinator>(services, ownerContext);
        ExactResolutionOutcome exactOutcome = await exact.ResolveAfterPass1Async(
            request, access, cancellationToken);
        if (exactOutcome.Kind is not (ExactResolutionOutcomeKind.Applied or
            ExactResolutionOutcomeKind.AlreadyApplied or
            ExactResolutionOutcomeKind.UncertaintyRecorded or
            ExactResolutionOutcomeKind.AlreadyRecorded))
        {
            throw new InvalidOperationException("Exact Cross-Repository Resolution failed.");
        }

        reportStage(30);
        ResolvedPreparedPass2Flow resolved = await services
            .GetRequiredService<IPreparedDemoPass2FlowPlanner>()
            .PlanAsync(profile, request, access, cancellationToken) ??
            throw new InvalidOperationException("The runtime prepared flow is unavailable.");
        if (resolved.RuntimeBinding is null || resolved.RuntimeBinding.SupportingRepositoryIds.Count != 2)
        {
            throw new InvalidOperationException("The runtime prepared flow is invalid.");
        }

        reportStage(31);
        await runner.RunPreparedPass2Async(accountExecution, resolved, access, cancellationToken);
        return new(system.Id, account.Value.Id, channel.Value.Id,
            accountProfile.Value.Id, channelProfile.Value.Id,
            accountExecution, channelExecution,
            resolved.RuntimeBinding.ProfileFingerprint,
            resolved.RuntimeBinding.SupportFingerprint);
    }

    private static async Task WriteResultAsync(
        string outputPath,
        ProvisionerInput input,
        ProductRunState state,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        string nonceHash = HashText(input.RunNonce);
        var unsigned = new ProvisionerResult(
            _resultSchema, nonceHash, "applied", "completed",
            state.SystemId, state.AccountRepositoryId, state.ChannelRepositoryId,
            state.AccountScanRunId, state.ChannelScanRunId,
            state.AccountScanExecutionId, state.ChannelScanExecutionId,
            _accountAlias, _acceptedRef, _accountCommit,
            _channelAlias, _acceptedRef, _channelCommit,
            state.ProfileFingerprint, state.SupportFingerprint,
            2, 1, 1, _codeGraphVersion, _codeGraphLockSha256,
            DateTimeOffset.UtcNow, string.Empty);
        string contentHash = HashText(JsonSerializer.Serialize(unsigned, _jsonOptions));
        ProvisionerResult result = unsigned with { ContentHash = contentHash };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(result, _jsonOptions);
        if (bytes.Length > MaximumResultBytes || ContainsForbiddenResult(bytes))
        {
            throw new InvalidDataException("The provisioner result is invalid.");
        }

        string temporary = outputPath + ".tmp-" +
            Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            File.Move(temporary, outputPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static bool ContainsForbiddenResult(ReadOnlySpan<byte> bytes)
    {
        string json = Encoding.UTF8.GetString(bytes);
        string[] forbidden =
        [
            "password", "connectionString", "apiKey", "sourceText", "documentText",
            "rawPrompt", "rawResponse", "diagnostic", "C:\\", "G:\\",
        ];
        return forbidden.Any(token => json.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static CreateLlmProviderRequest ProviderRequest(
        ProvisionerInput input, string suffix, string endpoint, AgentType agentType) => new()
        {
            Name = $"story115-{input.RunNonce}-{suffix}",
            BaseEndpointUrl = endpoint,
            ApiKey = input.ProviderApiKey,
            SupportedApiTypes = ApiType.ChatCompletions,
            DefaultAgentType = agentType,
        };

    private static UpsertSystemLlmProviderOverrideRequest OverrideRequest(
        Guid systemId, Guid providerId, AgentType agentType) => new()
        {
            SystemId = systemId,
            LlmProviderId = providerId,
            AgentType = agentType,
        };

    private static async Task RequireOverrideAsync(
        Task<SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto>> operation)
    {
        var result = await operation;
        if (result.Outcome != SystemLlmProviderOverrideMutationOutcome.Success)
        {
            throw new InvalidOperationException("Provider override failed.");
        }
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> users,
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
        RequireIdentity(await users.CreateAsync(user, password));
        return user;
    }

    private static void RequireIdentity(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Identity provisioning failed.");
        }
    }

    private static string HashFile(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static string HashText(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record ProvisionerUserContext(string UserId, bool IsAdministrator) : IUserContext
    {
        public SystemOwnerFilter ToSystemOwnerFilter() => IsAdministrator
            ? SystemOwnerFilter.AllSystems()
            : SystemOwnerFilter.OwnedBy(UserId);
    }
}

public sealed record ProvisionerInput(
    string SchemaIdentity,
    string RunNonce,
    string ConnectionString,
    Uri AccountRepositoryUri,
    Uri ChannelRepositoryUri,
    string WorkerEndpoint,
    string ChatEndpoint,
    string ProviderApiKey,
    string DataProtectionApplicationName,
    string AdministratorEmail,
    string AdministratorPassword,
    string OwnerEmail,
    string OwnerPassword,
    string NegativeEmail,
    string NegativePassword)
{
    public void Validate()
    {
        if (!StringComparer.Ordinal.Equals(SchemaIdentity, "vulgata.story115.provisioner-input/1") ||
            RunNonce.Length != 32 || RunNonce.Any(character => !Uri.IsHexDigit(character) || char.IsUpper(character)) ||
            string.IsNullOrWhiteSpace(ConnectionString) ||
            !StringComparer.Ordinal.Equals(
                AccountRepositoryUri.AbsoluteUri,
                "file:///vulgata/sources/mbs_account") ||
            !StringComparer.Ordinal.Equals(
                ChannelRepositoryUri.AbsoluteUri,
                "file:///vulgata/sources/mbs_channel") ||
            !IsLoopbackEndpoint(WorkerEndpoint) || !IsLoopbackEndpoint(ChatEndpoint) ||
            StringComparer.Ordinal.Equals(WorkerEndpoint, ChatEndpoint) ||
            string.IsNullOrWhiteSpace(ProviderApiKey) ||
            string.IsNullOrWhiteSpace(DataProtectionApplicationName) ||
            new[] { AdministratorEmail, AdministratorPassword, OwnerEmail, OwnerPassword,
                NegativeEmail, NegativePassword }.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("The provisioner input is invalid.");
        }
    }

    private static bool IsLoopbackEndpoint(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme == Uri.UriSchemeHttp && uri.Host == "127.0.0.1" &&
        uri.AbsolutePath == "/v1" && uri.Port is > 0 and <= 65535;
}

public sealed record ProductRunState(
    Guid SystemId,
    Guid AccountRepositoryId,
    Guid ChannelRepositoryId,
    Guid AccountScanRunId,
    Guid ChannelScanRunId,
    Guid AccountScanExecutionId,
    Guid ChannelScanExecutionId,
    string ProfileFingerprint,
    string SupportFingerprint);

public sealed record ProvisionerResult(
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
