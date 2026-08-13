using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BackupBDemo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.BackupB;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Git;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Shared.LlmProviders;
using Vulgata.Web.Data;
using Vulgata.Web.RequestPipeline;
using Vulgata.Web.Validators;

string repositoryRoot = FindRepositoryRoot();
await using ServiceProvider serviceProvider = CreateServiceProvider(repositoryRoot);
var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("BackupBDemo");

logger.LogInformation("=== Backup B Demo - Starting ===");
logger.LogInformation(
    "Evidence label: prepared-scope LLM demonstration; this run does not claim T072 or whole-Repository CGC success.");

await MigrateAndSeedAsync(serviceProvider, logger);

BackupBProvisioningRequest request = await LoadProvisioningRequestAsync(
    repositoryRoot,
    logger,
    CancellationToken.None);

BackupBProvisioningResult provisioningResult = await ProvisionAsync(
    serviceProvider,
    request,
    logger,
    CancellationToken.None);

// Populate the local working-copy map so the demo IGitWorkingCopyService
// returns the pre-existing local git repos instead of cloning from fake URLs.
var repositoryMap = serviceProvider.GetRequiredService<Dictionary<Guid, (string Path, string Revision)>>();
foreach (var (repoAlias, repoId) in provisioningResult.RepositoryIds)
{
    var repoInput = request.Repositories[repoAlias];
    repositoryMap[repoId] = (repoInput.LocalGitPath, repoInput.ExpectedCommit);
}
logger.LogInformation("Local working-copy map populated for {Count} repositories", repositoryMap.Count);

if (!await EnsureWorkerProviderReadyAsync(
        serviceProvider,
        provisioningResult.SystemId,
        logger,
        CancellationToken.None))
{
    logger.LogInformation("=== Backup B Demo - Provisioning Complete ===");
    logger.LogInformation("Database: vulgata_demo_screenshots");
    logger.LogInformation(
        "Provisioning succeeded, but Pass 1 / Pass 2 are waiting for a Worker Agent provider.");
    logger.LogInformation(
        "Set VULGATA_BACKUP_B_WORKER_MODEL, VULGATA_BACKUP_B_WORKER_ENDPOINT, and VULGATA_BACKUP_B_WORKER_API_KEY to continue.");
    Environment.ExitCode = 2;
    return;
}

if (StringComparer.OrdinalIgnoreCase.Equals(
        Environment.GetEnvironmentVariable("VULGATA_BACKUP_B_DIAGNOSE_PASS1_COMMIT"),
        "1"))
{
    await RunPass1CommitDiagnosticAsync(
        serviceProvider,
        provisioningResult,
        logger,
        CancellationToken.None);
    return;
}

await RunPipelineAsync(serviceProvider, provisioningResult, logger, CancellationToken.None);
await LogPipelineEvidenceAsync(serviceProvider, logger, CancellationToken.None);

logger.LogInformation("=== Backup B Demo - Complete ===");

return;

static string FindRepositoryRoot()
{
    string[] rootsToTry =
    [
        AppContext.BaseDirectory,
        Directory.GetCurrentDirectory(),
    ];

    foreach (string root in rootsToTry)
    {
        string? candidate = FindRepositoryRootFrom(root);
        if (candidate is not null)
        {
            return candidate;
        }
    }

    throw new InvalidOperationException("Unable to locate the Vulgata repository root.");
}

static string? FindRepositoryRootFrom(string startPath)
{
    DirectoryInfo? current = new(startPath);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "Vulgata.slnx")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    return null;
}

static ServiceProvider CreateServiceProvider(string repositoryRoot)
{
    string webRoot = Path.Combine(repositoryRoot, "src", "dotnet", "Vulgata.Web");
    WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        ApplicationName = typeof(VulgataWebComposition).Assembly.GetName().Name,
        EnvironmentName = Environments.Production,
        ContentRootPath = webRoot,
    });

    builder.Configuration.AddJsonFile(
        "appsettings.DemoMigration.json",
        optional: false,
        reloadOnChange: false);

    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Information);

    _ = VulgataWebComposition.ConfigureServices(builder);
    builder.Services.AddSingleton<HttpMessageHandler, DeterministicWorkerAgentLoopbackHandler>();
    bool forceDeterministic = StringComparer.OrdinalIgnoreCase.Equals(
            Environment.GetEnvironmentVariable("VULGATA_BACKUP_B_FORCE_DETERMINISTIC"),
            "1");
    if (forceDeterministic)
    {
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWorkerAgentDispatcher, DeterministicWorkerAgentDispatcher>());
    }
    else
    {
        builder.Services.AddScoped<WorkerAgentDispatcher>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IWorkerAgentDispatcher>(
            services => new BoundedRetryWorkerAgentDispatcher(
                services.GetRequiredService<WorkerAgentDispatcher>(),
                maximumAttempts: 3)));
    }

    // Replace the production GitWorkingCopyService with a demo-specific
    // implementation that uses pre-existing local git working copies.
    var repositoryMap = new Dictionary<Guid, (string Path, string Revision)>();
    builder.Services.AddSingleton(repositoryMap);
    builder.Services.Replace(ServiceDescriptor.Scoped<IGitWorkingCopyService>(
        sp => new BackupBLocalGitWorkingCopyService(
            sp.GetRequiredService<Dictionary<Guid, (string Path, string Revision)>>())));

        // Replace the production ExactGitRevisionSnapshotService (which fetches from
        // remote clone URLs) with a demo-specific implementation that creates snapshots
        // from pre-existing local git working copies.
        builder.Services.Replace(ServiceDescriptor.Scoped<IExactGitRevisionSnapshotService>(
            sp => new BackupBExactGitRevisionSnapshotService(
                sp.GetRequiredService<Dictionary<Guid, (string Path, string Revision)>>())));

        return builder.Services.BuildServiceProvider(new ServiceProviderOptions
    {
        ValidateScopes = true,
    });
}

static async Task MigrateAndSeedAsync(
    ServiceProvider serviceProvider,
    ILogger logger)
{
    logger.LogInformation("=== Applying migrations ===");

    using IServiceScope scope = serviceProvider.CreateScope();
    ApplicationDbContext identityDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    VulgataDbContext vulgataDb = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();

    await identityDb.Database.MigrateAsync();
    await vulgataDb.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<RoleSeeder>().SeedAsync();

    logger.LogInformation("Identity and Vulgata migrations applied successfully");
}

static async Task<BackupBProvisioningRequest> LoadProvisioningRequestAsync(
    string repositoryRoot,
    ILogger logger,
    CancellationToken cancellationToken)
{
    string manifestPath = Path.Combine(
        repositoryRoot,
        "demos",
        "backup-b-direct-llm",
        "demo-manifest.json");

    logger.LogInformation("Loading manifest from: {Path}", manifestPath);

    string manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
    BackupBProvisioningRequest? request = JsonSerializer.Deserialize<BackupBProvisioningRequest>(
        manifestJson,
        new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

    if (request is null)
    {
        throw new InvalidOperationException(
            "Failed to deserialize demo-manifest.json as BackupBProvisioningRequest.");
    }

    request = request with
    {
        Repositories = request.Repositories.ToDictionary(
            pair => pair.Key,
            pair => pair.Value with
            {
                LocalGitPath = Path.GetFullPath(pair.Value.LocalGitPath, repositoryRoot),
            },
            StringComparer.Ordinal),
    };

    request.Validate();
    logger.LogInformation("Manifest loaded: {RepositoryCount} repositories", request.Repositories.Count);
    return request;
}

static async Task<BackupBProvisioningResult> ProvisionAsync(
    ServiceProvider serviceProvider,
    BackupBProvisioningRequest request,
    ILogger logger,
    CancellationToken cancellationToken)
{
    logger.LogInformation("=== Step 1: Running Provisioner ===");

    using IServiceScope scope = serviceProvider.CreateScope();
    IBackupBDirectProvisioner provisioner =
        scope.ServiceProvider.GetRequiredService<IBackupBDirectProvisioner>();
    BackupBProvisioningResult result =
        await provisioner.ProvisionAsync(request, cancellationToken);

    logger.LogInformation("Provisioner completed successfully");
    logger.LogInformation("  System ID: {SystemId}", result.SystemId);
    logger.LogInformation("  Repository IDs: {@RepositoryIds}", result.RepositoryIds);
    logger.LogInformation("  ScanRun IDs: {@ScanRunIds}", result.ScanRunIds);
    logger.LogInformation("  CodeUnit count: {Count}", result.CodeUnitIds.Count);
    logger.LogInformation("  PreScanResult IDs: {@PreScanResultIds}", result.PreScanResultIds);

    return result;
}

static async Task<bool> EnsureWorkerProviderReadyAsync(
    ServiceProvider serviceProvider,
    Guid systemId,
    ILogger logger,
    CancellationToken cancellationToken)
{
    using IServiceScope scope = serviceProvider.CreateScope();
    WorkerAgentProviderResolver resolver =
        scope.ServiceProvider.GetRequiredService<WorkerAgentProviderResolver>();

    if (await TryResolveWorkerProviderAsync(resolver, systemId, cancellationToken))
    {
        logger.LogInformation("Using existing Worker Agent provider configuration.");
        return true;
    }

    WorkerProviderEnvironmentConfiguration? environmentConfiguration =
        ReadWorkerProviderEnvironmentConfiguration();
    if (environmentConfiguration is not null)
    {
        await UpsertWorkerProviderOverrideAsync(
            scope.ServiceProvider,
            systemId,
            environmentConfiguration,
            logger,
            cancellationToken);

        _ = await resolver.ResolveAsync(systemId, cancellationToken);
        logger.LogInformation("Worker Agent provider override configured for system {SystemId}.", systemId);
        return true;
    }

    if (StringComparer.OrdinalIgnoreCase.Equals(
            Environment.GetEnvironmentVariable("VULGATA_BACKUP_B_FORCE_DETERMINISTIC"),
            "1"))
    {
        await UpsertWorkerProviderOverrideAsync(
            scope.ServiceProvider,
            systemId,
            new WorkerProviderEnvironmentConfiguration(
                "backup-b-loopback-worker",
                "http://127.0.0.1/v1",
                "backup-b-loopback-key"),
            logger,
            cancellationToken);
        _ = await resolver.ResolveAsync(systemId, cancellationToken);
        logger.LogInformation(
            "Using the explicitly requested deterministic Worker Agent provider for Backup B.");
        return true;
    }

    return false;
}

static async Task<bool> TryResolveWorkerProviderAsync(
    WorkerAgentProviderResolver resolver,
    Guid systemId,
    CancellationToken cancellationToken)
{
    try
    {
        _ = await resolver.ResolveAsync(systemId, cancellationToken);
        return true;
    }
    catch (InvalidOperationException)
    {
        return false;
    }
}

static WorkerProviderEnvironmentConfiguration? ReadWorkerProviderEnvironmentConfiguration()
{
    string? model = Environment.GetEnvironmentVariable("VULGATA_BACKUP_B_WORKER_MODEL");
    string? endpoint = Environment.GetEnvironmentVariable("VULGATA_BACKUP_B_WORKER_ENDPOINT");
    string? apiKey = Environment.GetEnvironmentVariable("VULGATA_BACKUP_B_WORKER_API_KEY");

    bool anyValueProvided =
        !string.IsNullOrWhiteSpace(model) ||
        !string.IsNullOrWhiteSpace(endpoint) ||
        !string.IsNullOrWhiteSpace(apiKey);
    bool allValuesProvided =
        !string.IsNullOrWhiteSpace(model) &&
        !string.IsNullOrWhiteSpace(endpoint) &&
        !string.IsNullOrWhiteSpace(apiKey);

    if (!anyValueProvided)
    {
        return null;
    }

    if (!allValuesProvided)
    {
        throw new InvalidOperationException(
            "Backup B Worker Agent configuration is incomplete. Set VULGATA_BACKUP_B_WORKER_MODEL, VULGATA_BACKUP_B_WORKER_ENDPOINT, and VULGATA_BACKUP_B_WORKER_API_KEY together.");
    }

    return new(
        model!.Trim(),
        NormalizeWorkerProviderEndpoint(endpoint!.Trim()),
        apiKey!.Trim());
}

static async Task UpsertWorkerProviderOverrideAsync(
    IServiceProvider services,
    Guid systemId,
    WorkerProviderEnvironmentConfiguration configuration,
    ILogger logger,
    CancellationToken cancellationToken)
{
    ILlmProviderManagementCoordinator providerCoordinator =
        services.GetRequiredService<ILlmProviderManagementCoordinator>();
    ILlmProviderRepository providerRepository =
        services.GetRequiredService<ILlmProviderRepository>();
    ISystemLlmProviderOverrideCoordinator overrideCoordinator =
        services.GetRequiredService<ISystemLlmProviderOverrideCoordinator>();
    IUserContext automationUser = CreateAutomationUser();

    LlmProviderSummaryDto? existingProvider = (await providerCoordinator.ListAsync(cancellationToken))
        .FirstOrDefault(provider =>
            StringComparer.Ordinal.Equals(provider.Name, configuration.Model));

    LlmProviderSummaryDto providerSummary;
    if (existingProvider is null)
    {
        LlmProviderMutationResult<LlmProviderSummaryDto> created =
            await providerCoordinator.CreateAsync(
                new CreateLlmProviderRequest
                {
                    Name = configuration.Model,
                    BaseEndpointUrl = configuration.Endpoint,
                    ApiKey = configuration.ApiKey,
                    SupportedApiTypes = ApiType.ChatCompletions,
                    DefaultAgentType = AgentType.Worker,
                },
                cancellationToken);

        if (created.Outcome != LlmProviderMutationOutcome.Success || created.Value is null)
        {
            throw new InvalidOperationException("Failed to create the Backup B Worker Agent provider.");
        }

        providerSummary = created.Value;
        logger.LogInformation(
            "Created Backup B Worker Agent provider {ProviderName} from environment configuration.",
            providerSummary.Name);
    }
    else
    {
        LlmProviderMutationResult<LlmProviderSummaryDto> updated =
            await providerCoordinator.UpdateAsync(
                existingProvider.Id,
                new UpdateLlmProviderRequest
                {
                    Name = configuration.Model,
                    BaseEndpointUrl = configuration.Endpoint,
                    ApiKey = configuration.ApiKey,
                    SupportedApiTypes = ApiType.ChatCompletions,
                    DefaultAgentType = AgentType.Worker,
                },
                cancellationToken);

        if (updated.Outcome != LlmProviderMutationOutcome.Success || updated.Value is null)
        {
            throw new InvalidOperationException("Failed to update the Backup B Worker Agent provider.");
        }

        providerSummary = updated.Value;
        logger.LogInformation(
            "Updated Backup B Worker Agent provider {ProviderName} from environment configuration.",
            providerSummary.Name);
    }

    SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto> overrideResult =
        await overrideCoordinator.UpsertAsync(
            systemId,
            null,
            new UpsertSystemLlmProviderOverrideRequest
            {
                SystemId = systemId,
                LlmProviderId = providerSummary.Id,
                AgentType = AgentType.Worker,
            },
            automationUser,
            cancellationToken);

    if (overrideResult.Outcome != SystemLlmProviderOverrideMutationOutcome.Success)
    {
        throw new InvalidOperationException("Failed to assign the Backup B Worker Agent provider override.");
    }

    if (StringComparer.Ordinal.Equals(configuration.Model, "backup-b-loopback-worker") &&
        IsLoopbackEndpoint(configuration.Endpoint))
    {
        logger.LogInformation("Skipping connection test for the built-in Backup B loopback provider.");
        return;
    }

    LlmProviderConnectionTestResult testResult = await providerCoordinator.TestConnectionAsync(
        providerSummary.Id,
        cancellationToken);
    if (testResult.Outcome != LlmProviderConnectionTestOutcome.Success)
    {
        LlmProvider? persisted = await providerRepository.GetByIdAsync(providerSummary.Id, cancellationToken);
        if (persisted is not null)
        {
            providerRepository.Remove(persisted);
            await providerRepository.SaveChangesAsync(cancellationToken);
        }

        throw new InvalidOperationException(
            $"The configured Backup B Worker Agent provider failed the connection test: {testResult.Message}");
    }
}

static IUserContext CreateAutomationUser()
{
    ClaimsPrincipal principal = new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, "backup-b-demo"),
    ]));
    return new UserContext(principal, isAdministrator: true);
}

static async Task RunPipelineAsync(
    ServiceProvider serviceProvider,
    BackupBProvisioningResult provisioningResult,
    ILogger logger,
    CancellationToken cancellationToken)
{
    logger.LogInformation("=== Step 2: Running Pass 1 / Exact Resolution / Pass 2 ===");

    using IServiceScope scope = serviceProvider.CreateScope();
    IServiceProvider services = scope.ServiceProvider;
    IUserContext automationUser = CreateAutomationUser();
    var authorizationVerifier = ActivatorUtilities.CreateInstance<RepositoryAuthorizationVerifier>(
        services,
        automationUser);
    var accessScope = new RepositoryAccessScope(
        provisioningResult.RepositoryIds.Values.ToArray(),
        authorizationVerifier);
    SequentialScanRunner runner = services.GetRequiredService<SequentialScanRunner>();
    ExactResolutionProfile profile =
        services.GetRequiredService<ExactResolutionProfileCatalog>().Load();

    Dictionary<string, Guid> executionIds = new(StringComparer.Ordinal);
    foreach (ProfileRepository repository in profile.Repositories)
    {
        Guid repositoryId = provisioningResult.RepositoryIds[repository.Alias];
        Guid scanRunId = provisioningResult.ScanRunIds[repository.Alias];
        Guid executionId = await runner.StartAsync(
            repositoryId,
            scanRunId,
            provisioningResult.SystemId,
            cancellationToken);

        await runner.RunPass1Async(executionId, accessScope, cancellationToken);
        executionIds[repository.Alias] = executionId;

        ScanExecutionStateView pass1State = await runner.ReadStateAsync(
            executionId,
            cancellationToken);
        if (pass1State.Phase != ScanExecutionPhase.Pass1Completed)
        {
            throw new InvalidOperationException(
                $"Pass 1 did not complete for {repository.Alias}: " +
                $"phase={pass1State.Phase}, " +
                $"terminalReason={pass1State.TerminalReasonKind}, " +
                $"message={pass1State.TerminalMessage ?? "(null)"}");
        }

        logger.LogInformation(
            "Pass 1 completed for {RepositoryAlias} ({RepositoryId}) with execution {ExecutionId}.",
            repository.Alias,
            repositoryId,
            executionId);
    }

    ExactResolutionRuntimeRequest runtimeRequest = ExactResolutionRuntimeRequest.Create(
        provisioningResult.SystemId,
        profile,
        profile.Repositories.Select(repository => new ExactResolutionRuntimeBinding(
            repository.Alias,
            repository.StableGitIdentity,
            provisioningResult.RepositoryIds[repository.Alias],
            provisioningResult.ScanRunIds[repository.Alias],
            executionIds[repository.Alias]))
            .ToArray());

    var exactCoordinator = ActivatorUtilities.CreateInstance<ExactCrossRepositoryResolutionCoordinator>(
        services,
            automationUser,
            authorizationVerifier);
    ExactResolutionOutcome exactOutcome = await exactCoordinator.ResolveAfterPass1Async(
        runtimeRequest,
        accessScope,
        cancellationToken);

    if (exactOutcome.Kind is not (
            ExactResolutionOutcomeKind.Applied or
            ExactResolutionOutcomeKind.AlreadyApplied or
            ExactResolutionOutcomeKind.UncertaintyRecorded or
            ExactResolutionOutcomeKind.AlreadyRecorded))
    {
        throw new InvalidOperationException(
            $"Exact Cross-Repository Resolution failed with outcome {exactOutcome.Kind}.");
    }

    logger.LogInformation("Exact Cross-Repository Resolution outcome: {Outcome}", exactOutcome.Kind);

    ResolvedPreparedPass2Flow resolved = await services
        .GetRequiredService<IPreparedDemoPass2FlowPlanner>()
        .PlanAsync(profile, runtimeRequest, accessScope, cancellationToken) ??
        throw new InvalidOperationException("The runtime prepared Pass 2 flow is unavailable.");

    if (resolved.RuntimeBinding is null || resolved.RuntimeBinding.SupportingRepositoryIds.Count != 2)
    {
        throw new InvalidOperationException("The runtime prepared Pass 2 flow is invalid.");
    }

    ResolutionOccurrence occurrence = profile.Occurrences.Single();
    Guid consumerExecutionId = executionIds[occurrence.ConsumerAlias];
    await runner.RunPreparedPass2Async(
        consumerExecutionId,
        resolved,
        accessScope,
        cancellationToken);

    ScanExecution? consumerExecution = await services
        .GetRequiredService<IScanExecutionStore>()
        .GetAsync(consumerExecutionId, cancellationToken);
    if (consumerExecution is not null &&
        consumerExecution.Phase == ScanExecutionPhase.Failed)
    {
        throw new InvalidOperationException(
            $"Pass 2 failed for occurrence {occurrence.OccurrenceId}: " +
            $"phase={consumerExecution.Phase}, " +
            $"terminalReason={consumerExecution.TerminalReasonKind}, " +
            $"message={consumerExecution.TerminalMessage ?? "(null)"}");
    }

    logger.LogInformation(
        "Pass 2 completed for occurrence {OccurrenceId}.",
        occurrence.OccurrenceId);
}

static async Task LogPipelineEvidenceAsync(
    ServiceProvider serviceProvider,
    ILogger logger,
    CancellationToken cancellationToken)
{
    using IServiceScope scope = serviceProvider.CreateScope();
    VulgataDbContext dbContext = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();

    int codeLogicDocuments = await dbContext.LogicalDocuments.AsNoTracking()
        .CountAsync(document => document.DocumentType == DocumentType.CodeLogic, cancellationToken);
    int businessLogicDocuments = await dbContext.LogicalDocuments.AsNoTracking()
        .CountAsync(document => document.DocumentType == DocumentType.BusinessLogic, cancellationToken);
    int referenceEdges = await dbContext.DocumentEdges.AsNoTracking()
        .CountAsync(edge => edge.EdgeType == DocumentEdgeType.References, cancellationToken);
    int generatedFromEdges = await dbContext.DocumentEdges.AsNoTracking()
        .CountAsync(edge => edge.EdgeType == DocumentEdgeType.GeneratedFrom, cancellationToken);

    logger.LogInformation("Persisted Code Logic Documents: {Count}", codeLogicDocuments);
    logger.LogInformation("Persisted Business Logic Documents: {Count}", businessLogicDocuments);
    logger.LogInformation("Persisted REFERENCES Edges: {Count}", referenceEdges);
    logger.LogInformation("Persisted GENERATED_FROM Edges: {Count}", generatedFromEdges);
}

static async Task RunPass1CommitDiagnosticAsync(
    ServiceProvider serviceProvider,
    BackupBProvisioningResult provisioningResult,
    ILogger logger,
    CancellationToken cancellationToken)
{
    using IServiceScope scope = serviceProvider.CreateScope();
    IServiceProvider services = scope.ServiceProvider;
    IUserContext automationUser = CreateAutomationUser();
    var authorizationVerifier = ActivatorUtilities.CreateInstance<RepositoryAuthorizationVerifier>(
        services,
        automationUser);
    var accessScope = new RepositoryAccessScope(
        provisioningResult.RepositoryIds.Values.ToArray(),
        authorizationVerifier);
    SequentialScanRunner runner = services.GetRequiredService<SequentialScanRunner>();
    IScanExecutionStore executionStore = services.GetRequiredService<IScanExecutionStore>();
    ICodeUnitSource codeUnitSource = services.GetRequiredService<ICodeUnitSource>();
    IWorkerAgentDispatcher dispatcher = services.GetRequiredService<IWorkerAgentDispatcher>();
    IWorkerAgentResultCommitter committer = services.GetRequiredService<IWorkerAgentResultCommitter>();

    string repoAlias = provisioningResult.RepositoryIds.Keys.OrderBy(static value => value, StringComparer.Ordinal)
        .First();
    Guid repositoryId = provisioningResult.RepositoryIds[repoAlias];
    Guid scanRunId = provisioningResult.ScanRunIds[repoAlias];
    Guid executionId = await runner.StartAsync(
        repositoryId,
        scanRunId,
        provisioningResult.SystemId,
        cancellationToken);
    ScanExecution execution = await executionStore.GetAsync(executionId, cancellationToken) ??
        throw new InvalidOperationException("Diagnostic execution was not persisted.");
    IReadOnlyList<CodeUnit> codeUnits = await codeUnitSource.GetPreparedAsync(
        scanRunId,
        repositoryId,
        cancellationToken);
    CodeUnit firstCodeUnit = codeUnits
        .OrderBy(unit => unit.FilePath, StringComparer.Ordinal)
        .ThenBy(unit => unit.LineStart)
        .ThenBy(unit => unit.Id)
        .First();

    MethodInfo buildPacket = typeof(SequentialScanRunner).GetMethod(
        "BuildPacket",
        BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Could not locate BuildPacket for diagnostics.");
    WorkerAgentJobPacket packet = (WorkerAgentJobPacket)buildPacket.Invoke(
        runner,
        [execution, firstCodeUnit])!;

    WorkerAgentResultEnvelope result = await dispatcher.DispatchAsync(
        packet,
        accessScope,
        cancellationToken);
    WorkerAgentResultCommitOutcome commitOutcome = await committer.CommitAsync(
        packet,
        result,
        accessScope,
        cancellationToken);

    logger.LogInformation(
        "Pass 1 diagnostic outcome for {RepositoryAlias}: dispatch={DispatchOutcome}, commit={CommitOutcome}",
        repoAlias,
        result.Outcome,
        commitOutcome);
}

static bool IsLoopbackEndpoint(string baseEndpointUrl)
{
    if (!Uri.TryCreate(baseEndpointUrl, UriKind.Absolute, out Uri? endpoint))
    {
        return true;
    }

    return endpoint.IsLoopback ||
        StringComparer.OrdinalIgnoreCase.Equals(endpoint.Host, "localhost");
}

static string NormalizeWorkerProviderEndpoint(string endpoint)
{
    if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? parsed))
    {
        return endpoint;
    }

    const string chatCompletionsSuffix = "/chat/completions";
    string path = parsed.AbsolutePath;
    if (path.EndsWith(chatCompletionsSuffix, StringComparison.OrdinalIgnoreCase))
    {
        string trimmedPath = path[..^chatCompletionsSuffix.Length].TrimEnd('/');
        var builder = new UriBuilder(parsed)
        {
            Path = string.IsNullOrEmpty(trimmedPath) ? "/" : trimmedPath,
            Query = string.Empty,
            Fragment = string.Empty,
        };
        return builder.Uri.ToString().TrimEnd('/');
    }

    return parsed.ToString().TrimEnd('/');
}

file sealed record WorkerProviderEnvironmentConfiguration(
    string Model,
    string Endpoint,
    string ApiKey);

file sealed class DeterministicWorkerAgentLoopbackHandler : HttpMessageHandler
{
    private readonly HttpMessageInvoker _inner = new(new SocketsHttpHandler(), disposeHandler: true);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is null ||
            !(request.RequestUri.IsLoopback ||
                StringComparer.OrdinalIgnoreCase.Equals(request.RequestUri.Host, "localhost")))
        {
            return await _inner.SendAsync(request, cancellationToken);
        }

        string path = request.RequestUri.AbsolutePath;
        if (StringComparer.Ordinal.Equals(path, "/v1/models"))
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        @object = "list",
                        data = new[]
                        {
                            new
                            {
                                id = "backup-b-loopback-worker",
                                @object = "model",
                            },
                        },
                    }),
                    Encoding.UTF8,
                    "application/json"),
            };
        }

        if (StringComparer.Ordinal.Equals(path, "/v1/chat/completions"))
        {
            string requestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            string responseJson = CreateChatCompletionResponse(requestBody);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static string CreateChatCompletionResponse(string requestBody)
    {
        using JsonDocument outer = JsonDocument.Parse(requestBody);
        string workerRequestJson = outer.RootElement.GetProperty("messages")
            .EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "user")
            .GetProperty("content")
            .GetString()!;
        using JsonDocument workerRequest = JsonDocument.Parse(workerRequestJson);
        JsonElement root = workerRequest.RootElement;

        string responseJson = StringComparer.Ordinal.Equals(
            root.GetProperty("kind").GetString(),
            nameof(WorkerAgentJobKind.Pass1CodeLogicDocument))
            ? CreatePass1Response(root)
            : CreatePass2Response(root);

        return JsonSerializer.Serialize(new
        {
            id = "chatcmpl-backup-b",
            @object = "chat.completion",
            created = 1_785_542_400,
            model = "backup-b-loopback-worker",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = responseJson },
                    finish_reason = "stop",
                },
            },
            usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 },
        });
    }

    private static string CreatePass1Response(JsonElement root)
    {
        JsonElement source = root.GetProperty("source");
        Guid jobId = root.GetProperty("jobId").GetGuid();
        Guid codeUnitId = root.GetProperty("codeUnitId").GetGuid();
        Guid anchorId = root.TryGetProperty("preparedSourceAnchors", out JsonElement prepared) &&
            prepared.GetArrayLength() > 0
            ? prepared[0].GetProperty("id").GetGuid()
            : StableId("backup-b-anchor", codeUnitId);
        LoopbackEvidence[] communicationEvidence = root
            .GetProperty("approvedCommunicationEvidence")
            .EnumerateArray()
            .Select(evidence => new LoopbackEvidence(
                evidence.GetProperty("evidenceId").GetGuid(),
                evidence.GetProperty("kind").GetString()!,
                evidence.GetProperty("description").GetString()!))
            .OrderBy(evidence => evidence.Id)
            .ToArray();

        var sections = new List<object>
        {
            new
            {
                stableSectionId = StableId("backup-b-section", codeUnitId),
                order = 1,
                kind = nameof(DocumentSectionKind.TechnicalDetail),
                heading = "处理逻辑",
                markdown = "按已提交代码切片生成代码逻辑文档。",
                searchText = "提交 切片 代码逻辑",
                sourceAnchorIds = new[] { anchorId },
                documentVersionIds = Array.Empty<Guid>(),
            },
        };
        sections.AddRange(communicationEvidence.Select((evidence, index) => (object)new
        {
            stableSectionId = evidence.Id,
            order = index + 2,
            kind = nameof(DocumentSectionKind.Evidence),
            heading = evidence.Kind,
            markdown = evidence.Description,
            searchText = evidence.Description,
            sourceAnchorIds = new[] { anchorId },
            documentVersionIds = Array.Empty<Guid>(),
        }));

        return JsonSerializer.Serialize(new
        {
            jobId,
            systemId = root.GetProperty("systemId").GetGuid(),
            repositoryId = root.GetProperty("repositoryId").GetGuid(),
            scanRunId = root.GetProperty("scanRunId").GetGuid(),
            codeUnitId,
            profiledRevision = root.GetProperty("profiledRevision").GetString(),
            repositoryRelativePath = source.GetProperty("repositoryRelativePath").GetString(),
            lineStart = source.GetProperty("lineStart").GetInt32(),
            lineEnd = source.GetProperty("lineEnd").GetInt32(),
            sourceHash = source.GetProperty("sourceHash").GetString(),
            fileIdentityHash = source.GetProperty("fileIdentityHash").GetString(),
            content = new
            {
                schemaIdentity = KnowledgeDocumentLimits.SchemaIdentity,
                documentType = nameof(DocumentType.CodeLogic),
                stableKey = $"backup-b.code-unit.{codeUnitId:D}",
                title = $"代码逻辑 {codeUnitId:D}",
                repositoryRelativePath = source.GetProperty("repositoryRelativePath").GetString(),
                summary = "说明准备代码单元的确定性处理。",
                keySymbols = new[] { source.GetProperty("symbolName").GetString() },
                businessTerms = new[] { "订单" },
                technicalTerms = new[] { "Worker Agent" },
                sections,
            },
            sourceAnchors = new[]
            {
                new
                {
                    id = anchorId,
                    repositoryRelativePath = source.GetProperty("repositoryRelativePath").GetString(),
                    evidenceLocator = source.GetProperty("symbolName").GetString(),
                    lineStart = source.GetProperty("lineStart").GetInt32(),
                    lineEnd = source.GetProperty("lineEnd").GetInt32(),
                    sourceHash = source.GetProperty("sourceHash").GetString(),
                },
            },
            referenceDocumentVersionIds = Array.Empty<Guid>(),
            communicationEvidenceIds = communicationEvidence.Select(evidence => evidence.Id).ToArray(),
        });
    }

    private static string CreatePass2Response(JsonElement root)
    {
        JsonElement[] documents = root.GetProperty("codeLogicDocumentVersions")
            .EnumerateArray()
            .ToArray();
        Guid[] documentVersionIds = documents
            .Select(document => document.GetProperty("documentVersionId").GetGuid())
            .ToArray();
        Guid consumerRepositoryId = root.GetProperty("repositoryId").GetGuid();
        Guid[] consumerAnchorIds = documents
            .Where(document =>
                document.GetProperty("repositoryId").GetGuid() == consumerRepositoryId)
            .SelectMany(document => document.GetProperty("sourceAnchors").EnumerateArray())
            .Select(anchor => anchor.GetProperty("id").GetGuid())
            .Order()
            .ToArray();
        LoopbackAnchor[] anchors = documents
            .SelectMany(document => document.GetProperty("sourceAnchors").EnumerateArray())
            .Select(anchor => new LoopbackAnchor(
                anchor.GetProperty("id").GetGuid(),
                anchor.GetProperty("repositoryRelativePath").GetString()!,
                anchor.GetProperty("evidenceLocator").GetString()!,
                anchor.GetProperty("lineStart").GetInt32(),
                anchor.GetProperty("lineEnd").GetInt32(),
                anchor.GetProperty("sourceHash").GetString()!))
            .OrderBy(anchor => anchor.Id)
            .ToArray();

        return JsonSerializer.Serialize(new
        {
            jobId = root.GetProperty("jobId").GetGuid(),
            systemId = root.GetProperty("systemId").GetGuid(),
            repositoryId = root.GetProperty("repositoryId").GetGuid(),
            scanRunId = root.GetProperty("scanRunId").GetGuid(),
            profiledRevision = root.GetProperty("profiledRevision").GetString(),
            flowId = root.GetProperty("flowId").GetGuid(),
            purpose = "帮助业务人员理解订单处理流程。",
            mainSteps = new[]
            {
                new { order = 1, description = "计算订单总额。", traceEvidenceOrders = new[] { 1 } },
                new { order = 2, description = "预留对应库存。", traceEvidenceOrders = new[] { 2 } },
            },
            outcome = "订单完成计价并预留库存。",
            content = new
            {
                schemaIdentity = KnowledgeDocumentLimits.SchemaIdentity,
                documentType = nameof(DocumentType.BusinessLogic),
                stableKey = "backup-b.orders.prepared-flow",
                title = "订单处理流程",
                repositoryRelativePath = "flows/orders-prepared-flow",
                summary = "订单完成计价并预留库存。",
                keySymbols = Array.Empty<string>(),
                businessTerms = new[] { "库存", "订单" },
                technicalTerms = Array.Empty<string>(),
                sections = new[]
                {
                    new
                    {
                        stableSectionId = StableId("backup-b-business-section", root.GetProperty("flowId").GetGuid()),
                        order = 1,
                        kind = nameof(DocumentSectionKind.ProcessStep),
                        heading = "主要步骤",
                        markdown = "先计算订单总额，再预留对应库存。",
                        searchText = "订单 总额 库存 预留",
                        sourceAnchorIds = consumerAnchorIds,
                        documentVersionIds = documentVersionIds.Order().ToArray(),
                    },
                },
            },
            sourceAnchors = anchors.Select(anchor => new
            {
                id = anchor.Id,
                repositoryRelativePath = anchor.RepositoryRelativePath,
                evidenceLocator = anchor.EvidenceLocator,
                lineStart = anchor.LineStart,
                lineEnd = anchor.LineEnd,
                sourceHash = anchor.SourceHash,
            }),
            traceEvidenceOrders = new[] { 1, 2 },
            generatedFromDocumentVersionIds = documentVersionIds,
        });
    }

    private static Guid StableId(string prefix, Guid value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{prefix}\n{value:D}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private sealed record LoopbackEvidence(Guid Id, string Kind, string Description);

    private sealed record LoopbackAnchor(
        Guid Id,
        string RepositoryRelativePath,
        string EvidenceLocator,
        int LineStart,
        int LineEnd,
        string SourceHash);
}

file sealed class DeterministicWorkerAgentDispatcher(
    IWorkerAgentExecutionContextSource executionContextSource,
    TimeProvider timeProvider) : IWorkerAgentDispatcher
{
    public Task<WorkerAgentResultEnvelope> DispatchAsync(
        WorkerAgentJobPacket packet,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packet);
        return Task.FromException<WorkerAgentResultEnvelope>(
            new InvalidOperationException(
                "A Repository access scope is required for Worker Agent dispatch."));
    }

    public async Task<WorkerAgentResultEnvelope> DispatchAsync(
        WorkerAgentJobPacket packet,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(scope);
        if (!scope.Contains(packet.RepositoryId))
        {
            throw new UnauthorizedAccessException(
                "The Worker Agent Repository is outside the authorized scope.");
        }

        if (packet.Payload is Pass2BusinessLogicJobPayload pass2)
        {
            return CreatePass2Envelope(packet, pass2);
        }

        WorkerAgentExecutionContext context = await executionContextSource.GetAsync(
            packet,
            scope,
            cancellationToken);
        return await DispatchAsync(context, cancellationToken);
    }

    public Task<WorkerAgentResultEnvelope> DispatchAsync(
        WorkerAgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreatePass1Envelope(context));
    }

    private WorkerAgentResultEnvelope CreatePass1Envelope(WorkerAgentExecutionContext context)
    {
        WorkerAgentJobPacket packet = context.Packet;
        Pass1CodeLogicJobPayload pass1 = (Pass1CodeLogicJobPayload)packet.Payload;
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid anchorId = pass1.PreparedSourceAnchors.Count > 0
            ? pass1.PreparedSourceAnchors[0].Id
            : StableId("backup-b-anchor", packet.CodeUnitId);
        Guid[] referenceDocumentVersionIds = pass1.AllowedReferenceDocumentVersionIds
            .Order()
            .ToArray();
        CanonicalSection[] sections =
        [
            new(
                StableId("backup-b-section", packet.CodeUnitId),
                1,
                DocumentSectionKind.TechnicalDetail,
                "处理逻辑",
                "按已提交代码切片生成代码逻辑文档。",
                "提交 切片 代码逻辑",
                [anchorId],
                referenceDocumentVersionIds),
            .. context.ApprovedCommunicationEvidence.Select((evidence, index) => new CanonicalSection(
                evidence.EvidenceId,
                index + 2,
                DocumentSectionKind.Evidence,
                evidence.Kind,
                evidence.Description,
                evidence.Description,
                [anchorId],
                [])),
        ];

        CanonicalDocumentContent content = new(
            KnowledgeDocumentLimits.SchemaIdentity,
            DocumentType.CodeLogic,
            $"backup-b.code-unit.{packet.CodeUnitId:D}",
            $"代码逻辑 {packet.CodeUnitId:D}",
            pass1.RepositoryRelativePath,
            "说明准备代码单元的确定性处理。",
            [packet.SourceSlice.SymbolName],
            ["订单"],
            ["Worker Agent"],
            sections);
        WorkerAgentSourceAnchor[] anchors =
        [
            WorkerAgentSourceAnchor.Create(
                anchorId,
                pass1.RepositoryRelativePath,
                packet.SourceSlice.SymbolName,
                pass1.LineStart,
                pass1.LineEnd,
                pass1.SourceHash),
        ];

        Pass1CodeLogicDocumentResultPayload payload =
            Pass1CodeLogicDocumentResultPayload.Create(
                packet.JobId,
                packet.SystemId,
                packet.RepositoryId,
                packet.ScanRunId,
                packet.CodeUnitId,
                packet.ProfiledRevision,
                pass1.RepositoryRelativePath,
                pass1.LineStart,
                pass1.LineEnd,
                pass1.SourceHash,
                pass1.FileIdentityHash,
                content,
                anchors,
                referenceDocumentVersionIds,
                context.ApprovedCommunicationEvidence.Select(evidence => evidence.EvidenceId)
                    .ToArray());

        return WorkerAgentResultEnvelope.CreateForJob(
            packet,
            WorkerAgentResultOutcome.Valid,
            boundedReason: null,
            now,
            now,
            payload);
    }

    private WorkerAgentResultEnvelope CreatePass2Envelope(
        WorkerAgentJobPacket packet,
        Pass2BusinessLogicJobPayload pass2)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid[] generatedFrom = pass2.CodeLogicDocumentVersions
            .Select(document => document.DocumentVersionId)
            .ToArray();
        WorkerAgentSourceAnchor[] anchors = pass2.CodeLogicDocumentVersions
            .SelectMany(document => document.SourceAnchors)
            .OrderBy(anchor => anchor.Id)
            .ToArray();
        Guid[] consumerAnchorIds = pass2.CodeLogicDocumentVersions
            .Where(document => document.RepositoryId == packet.RepositoryId)
            .SelectMany(document => document.SourceAnchors)
            .Select(anchor => anchor.Id)
            .Order()
            .ToArray();
        CanonicalDocumentContent content = new(
            KnowledgeDocumentLimits.SchemaIdentity,
            DocumentType.BusinessLogic,
            "backup-b.orders.prepared-flow",
            "订单处理流程",
            "flows/orders-prepared-flow",
            "订单完成计价并预留库存。",
            [],
            ["库存", "订单"],
            [],
            [
                new CanonicalSection(
                    StableId("backup-b-business-section", pass2.FlowId),
                    1,
                    DocumentSectionKind.ProcessStep,
                    "主要步骤",
                    "先计算订单总额，再预留对应库存。",
                    "订单 总额 库存 预留",
                    consumerAnchorIds,
                    generatedFrom.Order().ToArray()),
            ]);
        WorkerAgentBusinessMainStep[] mainSteps =
        [
            WorkerAgentBusinessMainStep.Create(1, "计算订单总额。", [1]),
            WorkerAgentBusinessMainStep.Create(2, "预留对应库存。", [2]),
        ];
        Pass2BusinessLogicDocumentResultPayload payload =
            Pass2BusinessLogicDocumentResultPayload.Create(
                packet.JobId,
                packet.SystemId,
                packet.RepositoryId,
                packet.ScanRunId,
                packet.ProfiledRevision,
                pass2.FlowId,
                "帮助业务人员理解订单处理流程。",
                mainSteps,
                "订单完成计价并预留库存。",
                content,
                anchors,
                [1, 2],
                generatedFrom);

        return WorkerAgentResultEnvelope.CreateForJob(
            packet,
            WorkerAgentResultOutcome.Valid,
            boundedReason: null,
            now,
            now,
            payload);
    }

    private static Guid StableId(string prefix, Guid value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{prefix}\n{value:D}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}
