using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using Vulgata.Agents.ChatExecution;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Agents;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.BackupB;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CodeGraph;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Analysis.PreparedNavigation;
using Vulgata.Infrastructure.Analysis.Source;
using Vulgata.Infrastructure.CommunicationDetection;
using Vulgata.Infrastructure.ContextManagement;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.FrameworkDetection;
using Vulgata.Infrastructure.Git;
using Vulgata.Infrastructure.Git.BackupB;
using Vulgata.Infrastructure;
using Vulgata.Shared;
using Vulgata.Shared.Chat;
using Vulgata.Shared.Repositories;
using Vulgata.Shared.Scans;
using Vulgata.Shared.Validators.Repositories;
using Vulgata.Shared.Validators.Systems;
using Vulgata.Web.Api;
using Vulgata.Web.Components;
using Vulgata.Web.Components.Account;
using Vulgata.Web.Components.Pages;
using Vulgata.Web.Data;
using Vulgata.Web.Logging;
using Vulgata.Web.RequestPipeline;
using Vulgata.Web.Validators;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.Services.AddSingleton<ILoggerProvider>(services =>
{
    IConfiguration configuration = services.GetRequiredService<IConfiguration>();
    IHostEnvironment environment = services.GetRequiredService<IHostEnvironment>();
    string localLogPath = configuration["Logging:LocalFile:Path"] ?? "logs/vulgata.log";
    if (!Path.IsPathRooted(localLogPath))
    {
        localLogPath = Path.Combine(environment.ContentRootPath, localLogPath);
    }

    return new LocalFileLoggerProvider(localLogPath);
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddProblemDetails();
FrameworkCatalog frameworkCatalog = VulgataWebComposition.ConfigureServices(builder);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Events.OnRedirectToLogin = context =>
    {
        if (IsApiRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (IsApiRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

var app = builder.Build();

IAnalysisRuntimeHealthProbe analysisHealthProbe =
    app.Services.GetRequiredService<IAnalysisRuntimeHealthProbe>();
AnalysisRuntimeReadiness analysisReadiness =
    app.Services.GetRequiredService<AnalysisRuntimeReadiness>();
AnalysisHealthProbeResult startupAnalysisHealth;
try
{
    startupAnalysisHealth = await analysisHealthProbe.CheckConfiguredIntegrityAsync(
        AnalysisRuntimeStartupScope.Create(),
        CancellationToken.None);
}
catch (Exception)
{
    startupAnalysisHealth = AnalysisRuntimeReadiness.NotReady(
        "analysis_runtime_probe_failed",
        "analysis runtime integrity verification failed");
}

analysisReadiness.Update(startupAnalysisHealth);

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var startupLogger = loggerFactory.CreateLogger("Startup");
startupLogger.LogInformation(
    "Framework catalog version {CatalogVersion} loaded with SHA-256 {CatalogSha256}.",
    frameworkCatalog.Version,
    frameworkCatalog.Sha256);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseWhen(
    context => !IsApiRequest(context.Request),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

try
{
    using var scope = app.Services.CreateScope();
    var identityDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var vulgataDb = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();

    startupLogger.LogInformation("Applying Identity database migrations...");
    await identityDb.Database.MigrateAsync();
    startupLogger.LogInformation("Identity migrations applied successfully.");

    startupLogger.LogInformation("Applying Vulgata domain database migrations...");
    await vulgataDb.Database.MigrateAsync();
    startupLogger.LogInformation("Vulgata domain migrations applied successfully.");

    var roleSeeder = scope.ServiceProvider.GetRequiredService<RoleSeeder>();
    startupLogger.LogInformation("Seeding identity roles...");
    await roleSeeder.SeedAsync();
    startupLogger.LogInformation("Identity role seeding completed.");
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Database migration failed. The application cannot start.");
    throw;
}

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapVulgataApiEndpoints();

await app.RunAsync();
return;

static bool IsApiRequest(HttpRequest request) =>
    request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

public static class VulgataWebComposition
{
    public static FrameworkCatalog ConfigureServices(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        IDataProtectionBuilder dataProtection = builder.Services.AddDataProtection();
        string? keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
        string? applicationName = builder.Configuration["DataProtection:ApplicationName"];
        if (!string.IsNullOrWhiteSpace(keyRingPath) &&
            !string.IsNullOrWhiteSpace(applicationName))
        {
            dataProtection
                .PersistKeysToFileSystem(new DirectoryInfo(
                    Environment.ExpandEnvironmentVariables(keyRingPath.Trim())))
                .SetApplicationName(applicationName.Trim());
        }

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUserContext>(services =>
        {
            IHttpContextAccessor accessor = services.GetRequiredService<IHttpContextAccessor>();
            IAuthorizationService authorization = services.GetRequiredService<IAuthorizationService>();
            ClaimsPrincipal principal = accessor.HttpContext?.User ?? new ClaimsPrincipal();
            return new UserContext(principal, authorization);
        });
        builder.Services.AddHttpClient(
            "LlmProviderConnectionTest",
            client => client.Timeout = TimeSpan.FromSeconds(10));
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<IdentityRedirectManager>();
        builder.Services.AddScoped<
            AuthenticationStateProvider,
            IdentityRevalidatingAuthenticationStateProvider>();
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        }).AddIdentityCookies();

        string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                                  throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__IdentityMigrationsHistory", "identity");
                npgsqlOptions.EnableRetryOnFailure(3);
            }));
        builder.Services.AddDbContext<VulgataDbContext>(
            options => options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                npgsqlOptions.EnableRetryOnFailure(3);
            }),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);
        builder.Services.AddDbContextFactory<VulgataDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                npgsqlOptions.EnableRetryOnFailure(3);
            }));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();
        builder.Services.AddIdentityCore<ApplicationUser>(IdentityOptionsConfiguration.Configure)
            .AddRoles<IdentityRole>()
            .AddErrorDescriber<ChineseIdentityErrorDescriber>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicyNames.AdministratorOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new AdministratorOnlyRequirement());
            });
            options.AddPolicy(AuthorizationPolicyNames.ManagementAccess, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new ManagementAccessRequirement());
            });
        });
        builder.Services.AddScoped<IAuthorizationHandler, AdministratorOnlyHandler>();
        builder.Services.AddScoped<IAuthorizationHandler, ManagementAccessHandler>();
        builder.Services.AddScoped<RoleSeeder>();
        builder.Services.AddScoped<IAdministratorRoleCoordinator, AdministratorRoleCoordinator>();
        builder.Services.AddScoped<ISystemOwnershipCoordinator, SystemOwnershipCoordinator>();
        builder.Services.AddScoped<ISystemRepository, SystemRepository>();
        builder.Services.AddScoped<IRepositoryRepository, RepositoryRepository>();
        builder.Services.AddScoped<KnowledgeDocumentMaterializer>();
        builder.Services.AddScoped<IKnowledgeStoreRepository, KnowledgeStoreRepository>();
        builder.Services.AddScoped<DurableScanRepository>();
        builder.Services.AddScoped<IDurableScanStore>(services =>
            services.GetRequiredService<DurableScanRepository>());
        builder.Services.AddScoped<ITwoRepositoryScanCoordinationStore>(services =>
            services.GetRequiredService<DurableScanRepository>());
        builder.Services.AddScoped<IScanRecoveryCoordinator>(services =>
            new DurableScanRecoveryCoordinator(
                services.GetRequiredService<IDurableScanStore>(),
                services.GetRequiredService<IScanRecoveryRevalidator>(),
                services.GetRequiredService<TimeProvider>()));
        builder.Services.AddScoped<IProductionScanStatusReader, ProductionScanStatusReader>();
        builder.Services.AddScoped<ProductionScanStatusCoordinator>();
        builder.Services.AddScoped<IScanRunRepository, ScanRunRepository>();
        builder.Services.AddScoped<IApprovedPreScanHandoffSource, ApprovedPreScanHandoffSource>();
        builder.Services.AddScoped<IPreparedScanStore, PreparedScanStore>();
        builder.Services.AddScoped<IPreparedScanHandoffSource, PreparedScanHandoffSource>();
        builder.Services.AddScoped<IPreparedScanInitiationStore, PreparedScanInitiationStore>();
        builder.Services.AddScoped<IPreparedScanInitiationAuthorizationRechecker,
            PreparedScanInitiationAuthorizationRechecker>();
        builder.Services.AddScoped<PreparedScanInitiationCoordinator>();
        builder.Services.AddScoped<IPreparedScanPairBuilder, PreparedScanPairBuilder>();
        builder.Services.AddScoped<PreparedScanCoordinator>();
        builder.Services.AddScoped<ICodeUnitSource, CodeUnitSource>();
        builder.Services.AddScoped<IScanExecutionStore, ScanExecutionRepository>();
        builder.Services.AddScoped<IRepositoryAuthorizationVerifier, RepositoryAuthorizationVerifier>();
        builder.Services.AddScoped<IChatAdvisoryContextReader, ChatAdvisoryContextReader>();
        builder.Services.AddScoped<IChatLexicalEvidenceProjector, ChatLexicalEvidenceProjector>();
        builder.Services.AddScoped<IChatRelationshipProjector, ChatRelationshipEvidenceProjector>();
        builder.Services.AddScoped<ChatEvidencePacketBuilder>();
        builder.Services.AddScoped<IChatEvidenceRetriever, ChatEvidenceRetriever>();
        builder.Services.AddScoped<IPreparedChatScopeResolver, PreparedChatScopeResolver>();
        builder.Services.AddScoped<IChatEvidenceCurrentnessValidator, ChatEvidenceCurrentnessValidator>();
        builder.Services.AddScoped<ChatCandidateExtractor>();
        builder.Services.AddScoped<ChatAgentProviderResolver>();
        builder.Services.AddScoped<ChatPromptCatalog>();
        builder.Services.AddScoped<IChatAgentDispatcher, ChatAgentDispatcher>();
        builder.Services.AddScoped<ChatAgentResultValidator>();
        builder.Services.AddScoped<IPreparedChatPublicationOwner, PreparedChatPublicationOwner>();
        builder.Services.AddScoped(services => new PreparedChatCoordinator(
            services.GetRequiredService<AuthenticationStateProvider>(),
            services.GetRequiredService<ISystemRepository>(),
            services.GetRequiredService<IRepositoryRepository>(),
            services.GetRequiredService<IRepositoryAuthorizationVerifier>(),
            services.GetRequiredService<IChatAdvisoryContextReader>(),
            services.GetRequiredService<IChatEvidenceRetriever>(),
            services.GetRequiredService<ChatCandidateExtractor>(),
            services.GetRequiredService<IChatAgentDispatcher>(),
            services.GetRequiredService<IChatEvidenceCurrentnessValidator>(),
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ChatAgentResultValidator>(),
            services.GetRequiredService<IPreparedChatScopeResolver>(),
            services.GetRequiredService<IValidator<PreparedChatQuestionRequest>>(),
            services.GetRequiredService<IChatStreamCompletionSummaryRecorder>(),
            services.GetRequiredService<IPreparedChatPublicationOwner>(),
            services.GetRequiredService<ILogger<PreparedChatCoordinator>>()));
        builder.Services.AddScoped<IChatRevealDelay, ChatRevealDelay>();
        builder.Services.AddScoped<DemoScanUiState>();
        builder.Services.AddSingleton<ExactResolutionProfileCatalog>();
        builder.Services.AddScoped<ExactGitRevisionSnapshotService>();
        builder.Services.AddScoped<IExactGitRevisionSnapshotService>(services =>
            services.GetRequiredService<ExactGitRevisionSnapshotService>());
        builder.Services.AddScoped<PreparedScanRevisionAcquirer>();
        builder.Services.AddScoped<IPreparedRevisionAcquirer>(services =>
            services.GetRequiredService<PreparedScanRevisionAcquirer>());
        builder.Services.AddSingleton<PreparedPass1RevisionBindingRegistry>();
        builder.Services.AddSingleton<IPreparedPass1RevisionBindingRegistry>(services =>
            services.GetRequiredService<PreparedPass1RevisionBindingRegistry>());
        builder.Services.AddScoped<IPreparedRevisionLeaseResolver>(services =>
            new CompositePreparedRevisionLeaseResolver(
                services.GetRequiredService<PreparedScanRevisionAcquirer>(),
                services.GetRequiredService<PreparedPass1RevisionBindingRegistry>()));
        builder.Services.AddScoped<PreparedScanCandidateEnumerator>();
        builder.Services.AddScoped<PreparedScanRawArtifactReader>();
        builder.Services.AddScoped<IPreparedSnapshotSource, PreparedSnapshotSource>();
        builder.Services.AddSingleton<IPreparedCommunicationCatalogSource,
            EmbeddedPreparedCommunicationCatalogSource>();
        builder.Services.AddScoped<IPreparedJavaReadinessPort, PreparedJavaReadinessService>();
        builder.Services.AddScoped<IPreparedTopLevelSymbolSource, PreparedJavaRangeSource>();
        builder.Services.AddSingleton<PreparedJavaRangeProvider>();
        builder.Services.AddScoped<ExactCandidateExtractors>();
        builder.Services.AddScoped<IExactCrossRepositoryResolver, ExactCrossRepositoryResolver>();
        builder.Services.AddScoped<IExactCrossRepositoryResolutionStore, ExactCrossRepositoryResolutionStore>();
        builder.Services.AddScoped<ExactCrossRepositoryResolutionCoordinator>();
        builder.Services.AddScoped<IPreparedDemoPass2FlowPlanner, PreparedDemoPass2FlowPlanner>();
        builder.Services.AddScoped<RevisionBoundWorkerContextHydrator>();
        builder.Services.AddScoped<IWorkerAgentExecutionContextSource, RevisionBoundWorkerExecutionContextSource>();
        builder.Services.AddScoped<WorkerAgentProviderResolver>();
        builder.Services.AddScoped<
            IScanRecoveryDispatchIdentitySource,
            WorkerAgentDispatchIdentitySource>();
        builder.Services.AddScoped<WorkerAgentDispatcher>();
        builder.Services.AddScoped<IWorkerAgentDispatcher>(services =>
            new RuntimeGatedWorkerAgentDispatcher(
                services.GetRequiredService<WorkerAgentDispatcher>(),
                services.GetRequiredService<AnalysisRuntimeReadiness>()));
        builder.Services.AddScoped<WorkerAgentResultCommitter>();
        builder.Services.AddScoped<IWorkerAgentResultCommitter>(services =>
            services.GetRequiredService<WorkerAgentResultCommitter>());
        builder.Services.AddScoped<IScanJobResultCommitter>(services =>
            services.GetRequiredService<WorkerAgentResultCommitter>());
        builder.Services.AddScoped<SequentialScanRunner>();
        builder.Services.AddScoped<IBackupBDirectProvisioner, BackupBDirectProvisioner>();
        builder.Services.AddScoped<HttpMessageHandler, SocketsHttpHandler>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(new AnalysisRuntimeOptions(
            builder.Configuration["AnalysisRuntime:CodeGraphCommandPath"] ?? "codegraph.ps1",
            builder.Configuration["AnalysisRuntime:JdtLsInstallationRoot"] ??
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local",
                    "jdt-language-server-1.61.0-202608141332"),
            builder.Configuration["AnalysisRuntime:JavaHome"] ??
                @"C:\Program Files\Java\latest\jdk-21"));
        builder.Services.AddSingleton<AnalysisRuntimeReadiness>();
        builder.Services.AddSingleton<IPreparedScanRuntimeIdentityResolver,
            ConfiguredPreparedScanRuntimeIdentityResolver>();
        builder.Services.AddSingleton<IAnalysisRuntimeHealthProbe, ConfiguredAnalysisRuntimeHealthProbe>();
        builder.Services.AddScoped<CodeGraphAnalysisAdapter>(services => new CodeGraphAnalysisAdapter(
            services.GetRequiredService<AnalysisRuntimeOptions>(),
            services.GetRequiredService<IPreparedRevisionLeaseResolver>()));
        builder.Services.AddScoped<ICodeGraphAnalysisPort>(services =>
            services.GetRequiredService<CodeGraphAnalysisAdapter>());
        builder.Services.AddScoped<IPreparedCodeGraphAnalysisPort>(services =>
            services.GetRequiredService<CodeGraphAnalysisAdapter>());
        builder.Services.AddScoped<PreparedPass1Store>();
        builder.Services.AddScoped<IPreparedPass1Store>(services =>
            services.GetRequiredService<PreparedPass1Store>());
        builder.Services.AddScoped<IPreparedPass1ProviderTransitionStore>(services =>
            services.GetRequiredService<PreparedPass1Store>());
        builder.Services.AddScoped<IPreparedPass1NavigationScopeSource>(services =>
            services.GetRequiredService<PreparedPass1Store>());
        builder.Services.AddScoped<IPreparedPass1CurrentDocumentEvidenceSource>(services =>
            services.GetRequiredService<PreparedPass1Store>());
        builder.Services.AddScoped<IPreparedPass1NavigationReceiptSink>(services =>
            services.GetRequiredService<PreparedPass1Store>());
        builder.Services.AddScoped<IPass1NavigationAttemptReservationStore>(services =>
            services.GetRequiredService<PreparedPass1Store>());
        builder.Services.AddScoped<IPass1NavigationCallsMarkerRepairStore>(services =>
            services.GetRequiredService<PreparedPass1Store>());
        builder.Services.AddScoped<IPreparedPass1ClaimedPacketSource,
            PreparedPass1ClaimedPacketSource>();
        builder.Services.AddScoped<IPreparedPass1JobExecutor, PreparedPass1JobExecutor>();
        builder.Services.AddScoped<IPreparedPass1WorkerDispatcher,
            PreparedPass1WorkerDispatcher>();
        builder.Services.AddScoped<IPass1CodeLogicDocumentCommitter,
            Pass1CodeLogicDocumentCommitter>();
        builder.Services.AddScoped<IPreparedPass1ProviderHealthProbe>(services =>
            new PreparedPass1ProviderHealthProbe(
                services.GetRequiredService<HttpMessageHandler>(),
                services.GetRequiredService<TimeProvider>()));
        builder.Services.AddScoped<PreparedPass1ProbeDriver>();
        builder.Services.AddScoped<IPreparedPass1ProviderAvailabilityProbe,
            PreparedPass1ProviderAvailabilityProbe>();
        builder.Services.AddSingleton<PreparedPass1ExecutionActivator>();
        builder.Services.AddHostedService<PreparedPass1ExecutionHostedService>();
        builder.Services.AddScoped<PreparedPass1JobRunner>();
        builder.Services.AddScoped<PreparedPass1DispatchWorker>();
        builder.Services.AddScoped<PreparedPass1Coordinator>();
        builder.Services.AddScoped<PreparedPass1NavigationService>();
        builder.Services.AddScoped<IPass1NavigationService>(services =>
            services.GetRequiredService<PreparedPass1NavigationService>());
        builder.Services.AddScoped<PreparedNavigateCodeFunction>();
        builder.Services.AddScoped<PreparedSubmitCodeLogicFunction>();
        builder.Services.AddPreparedPass2Infrastructure();
        builder.Services.AddPreparedPass2Agents();
        builder.Services.AddScoped<IPass2ExecutionAuthorizationVerifier,
            PreparedPass2ExecutionAuthorizationVerifier>();
        builder.Services.AddSingleton<PreparedPass2ExecutionActivator>();
        builder.Services.AddHostedService<PreparedPass2ExecutionHostedService>();
        builder.Services.AddScoped<PreparedPass2JobRunner>();
        builder.Services.AddScoped<PreparedPass2Coordinator>();
        builder.Services.AddScoped<IJavaAnalysisSessionFactory>(services =>
        {
            const string release = "jdt-language-server-1.61.0-202608141332";
            const string archiveSha256 =
                "bbb2297d69a69a8216e17b799744b9ec5454974bbb5e498d45ad3505d8b4fa0f";
            AnalysisRuntimeOptions options = services.GetRequiredService<AnalysisRuntimeOptions>();
            GitWorkingCopyOptions git = services.GetRequiredService<GitWorkingCopyOptions>();
            string archivePath = builder.Configuration["AnalysisRuntime:JdtLsDistributionArchivePath"] ??
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads",
                    release + ".tar.gz");
            string runtimeWorkspace = builder.Configuration["AnalysisRuntime:JdtLsWorkspaceRoot"] ??
                Path.Combine(git.RootPath, ".jdtls-runtime");
            return new JdtLsAnalysisSessionFactory(
                new JdtLsAdapterOptions(
                    Path.Combine(options.JavaHome, "bin", "java.exe"),
                    options.JdtLsInstallationRoot,
                    runtimeWorkspace,
                    InitializationTimeout: TimeSpan.FromSeconds(30),
                    RequestTimeout: TimeSpan.FromSeconds(30),
                    ShutdownTimeout: TimeSpan.FromSeconds(10),
                    ResponseByteLimit: 1_048_576),
                archivePath,
                archiveSha256,
                services.GetRequiredService<IPreparedRevisionLeaseResolver>());
        });
        builder.Services.AddScoped<IRevisionBoundSourceReader, PreparedRevisionSourceReader>();
        builder.Services.AddScoped<IScanRecoveryBindingProbe, RuntimeScanRecoveryBindingProbe>();
        builder.Services.AddScoped<IScanRecoveryRevalidator, RuntimeScanRecoveryRevalidator>();
        builder.Services.AddScoped<IDatabaseConnectionRepository, DatabaseConnectionRepository>();
        builder.Services.AddScoped<ILlmProviderRepository, LlmProviderRepository>();
        builder.Services.AddScoped<ISystemLlmProviderOverrideRepository, SystemLlmProviderOverrideRepository>();
        builder.Services.AddScoped<IRepositoryManagementCoordinator, RepositoryManagementCoordinator>();
        builder.Services.AddScoped<IPreScanHandoffService, PreScanHandoffService>();
        builder.Services.AddScoped<IRepositoryDatabaseConnectionCoordinator, RepositoryDatabaseConnectionCoordinator>();
        builder.Services.AddScoped<ILlmProviderConnectionTestService, LlmProviderConnectionTestService>();
        builder.Services.AddScoped<IDatabaseConnectionTestService, DatabaseConnectionTestService>();
        builder.Services.AddScoped<ILlmProviderManagementCoordinator, LlmProviderManagementCoordinator>();
        builder.Services.AddScoped<ISystemLlmProviderOverrideCoordinator, SystemLlmProviderOverrideCoordinator>();
        builder.Services.AddScoped<IGitRemoteValidationService, GitRemoteValidationService>();
        FrameworkCatalog frameworkCatalog = FrameworkCatalog.LoadEmbedded();
        builder.Services.AddSingleton(frameworkCatalog);
        builder.Services.AddScoped<IFrameworkEvidenceDetector, FrameworkEvidenceDetector>();
        CommunicationEvidenceRuleCatalog communicationCatalog =
            CommunicationEvidenceRuleCatalog.LoadEmbedded();
        builder.Services.AddSingleton(communicationCatalog);
        builder.Services.AddScoped<IPreScanCommunicationDetector, CommunicationKindDetector>();
        string profilingDataRoot = builder.Configuration["CodeGraph:DataRootPath"] ??
                                   (OperatingSystem.IsLinux()
                                       ? "/var/lib/vulgata-codegraph"
                                       : Path.Combine(builder.Environment.ContentRootPath, "data", "profiling"));
        builder.Services.AddSingleton(new CodeGraphOptions(
            Path.Combine(profilingDataRoot, "codegraph"),
            builder.Configuration.GetValue(
                "CodeGraph:WholeRepositoryProcessTimeoutSeconds",
                1_800)));
        builder.Services.AddSingleton(new GitWorkingCopyOptions(
            Path.Combine(profilingDataRoot, "repositories")));
        builder.Services.AddScoped<IGitWorkingCopyService, GitWorkingCopyService>();
        builder.Services.AddScoped<ICommittedRepositorySnapshotService, CommittedRepositorySnapshotService>();
        builder.Services.AddScoped<IScanCandidateEnumerator, ScanCandidateEnumerator>();
        builder.Services.AddScoped<IScanStateService, NoOpScanStateService>();
        builder.Services.AddScoped<IContextCompositionService, ContextCompositionService>();
        builder.Services.AddScoped<IContextManagementCoordinator, ContextManagementCoordinator>();
        builder.Services.AddSingleton<ISecretEncryptionService, SecretEncryptionService>();
        builder.Services.AddValidatorsFromAssemblyContaining<CreateSystemRequestValidator>();
        builder.Services.AddValidatorsFromAssemblyContaining<AssignSystemOwnerRequestValidator>();
        builder.Services.AddValidatorsFromAssemblyContaining<CreateRepositoryRequestValidator>();
        builder.Services.AddValidatorsFromAssemblyContaining<CreateLlmProviderRequestValidator>();
        builder.Services.AddValidatorsFromAssemblyContaining<UpsertSystemLlmProviderOverrideRequestValidator>();
        builder.Services.AddValidatorsFromAssemblyContaining<UpsertDatabaseConnectionRequestValidator>();
        builder.Services.AddScoped<IValidator<PreparedScanRequest>, PreparedScanRequestValidator>();
        builder.Services.AddScoped<IValidator<StartPass1ExecutionRequest>,
            StartPass1ExecutionRequestValidator>();
        builder.Services.AddScoped<IValidator<UpdatePass1ConcurrencyRequest>,
            UpdatePass1ConcurrencyRequestValidator>();
        builder.Services.AddScoped<IValidator<StartPass2ExecutionRequest>,
            StartPass2ExecutionRequestValidator>();
        builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, BcryptPasswordHasher<ApplicationUser>>();
        builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
        return frameworkCatalog;
    }
}

internal static class AnalysisRuntimeStartupScope
{
    internal static AnalysisScope Create() => new(
        Guid.Parse("12000000-0000-4000-8000-000000000001"),
        Guid.Parse("12000000-0000-4000-8000-000000000002"),
        "0000000000000000000000000000000000000000",
        "startup-integrity",
        new string('0', 64),
        new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
}

internal sealed class AnalysisRuntimeReadiness
{
    private AnalysisHealthProbeResult _current = NotReady(
        "analysis_runtime_not_checked",
        "analysis runtime integrity has not been checked");

    internal AnalysisHealthProbeResult Current => Volatile.Read(ref _current);

    internal void Update(AnalysisHealthProbeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Volatile.Write(ref _current, result);
    }

    internal static AnalysisHealthProbeResult NotReady(string code, string diagnostic) =>
        new(
            AnalysisRuntimeHealthStatus.NotReady,
            code,
            diagnostic,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            [new AnalysisHealthCheck("analysis-runtime", AnalysisRuntimeHealthStatus.NotReady)]);
}

internal sealed class ConfiguredAnalysisRuntimeHealthProbe(
    IPreparedScanRuntimeIdentityResolver runtimeIdentityResolver) : IAnalysisRuntimeHealthProbe
{
    public async Task<AnalysisHealthProbeResult> CheckConfiguredIntegrityAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        PreparedScanRuntimeIdentityResolution resolution =
            await runtimeIdentityResolver.ResolveAsync(
                scope.RepositoryId,
                scope.ProfiledRevision,
                cancellationToken);
        return resolution.Health;
    }
}
internal sealed class RuntimeGatedWorkerAgentDispatcher(
    WorkerAgentDispatcher inner,
    AnalysisRuntimeReadiness readiness) : IWorkerAgentDispatcher
{
    public Task<WorkerAgentResultEnvelope> DispatchAsync(
        WorkerAgentJobPacket packet,
        CancellationToken cancellationToken = default)
    {
        EnsureReady();
        return inner.DispatchAsync(packet, cancellationToken);
    }

    public Task<WorkerAgentResultEnvelope> DispatchAsync(
        WorkerAgentJobPacket packet,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        EnsureReady();
        return inner.DispatchAsync(packet, scope, cancellationToken);
    }

    private void EnsureReady()
    {
        if (readiness.Current.Status != AnalysisRuntimeHealthStatus.Ready)
        {
            throw new InvalidOperationException("Analysis Runtime is NotReady.");
        }
    }
}

internal sealed class RuntimeScanRecoveryRevalidator : IScanRecoveryRevalidator
{
    private const int _maximumOwnedRepositoryCount = 1_024;
    private readonly AnalysisRuntimeReadiness _readiness;
    private readonly IAnalysisRuntimeHealthProbe _configuredHealthProbe;
    private readonly IScanRecoveryBindingProbe _bindingProbe;

    public RuntimeScanRecoveryRevalidator(
        AnalysisRuntimeReadiness readiness,
        IScanRecoveryBindingProbe bindingProbe)
        : this(readiness, new ReadinessHealthProbe(readiness), bindingProbe)
    {
    }

    public RuntimeScanRecoveryRevalidator(
        AnalysisRuntimeReadiness readiness,
        IAnalysisRuntimeHealthProbe configuredHealthProbe,
        IScanRecoveryBindingProbe bindingProbe)
    {
        _readiness = readiness;
        _configuredHealthProbe = configuredHealthProbe;
        _bindingProbe = bindingProbe;
    }

    public async Task<ScanRecoveryRevalidationResult> RevalidateAsync(
        ScanRecoverySnapshot snapshot,
        DurableScanAuthorizationScope authorizationScope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(authorizationScope);
        cancellationToken.ThrowIfCancellationRequested();
        if (!authorizationScope.OwnedSystemIds.Contains(snapshot.SystemId) ||
            authorizationScope.OwnedRepositoryIds is not { Count: > 0 } ownedRepositoryIds ||
            ownedRepositoryIds.Count > _maximumOwnedRepositoryCount ||
            ownedRepositoryIds.Any(value => value == Guid.Empty) ||
            ownedRepositoryIds.Distinct().Count() != ownedRepositoryIds.Count ||
            !ownedRepositoryIds.Contains(snapshot.RepositoryId))
        {
            return new ScanRecoveryRevalidationResult(
                false,
                DurableScanReason.Unauthorized);
        }

        if (_readiness.Current.Status != AnalysisRuntimeHealthStatus.Ready)
        {
            return new ScanRecoveryRevalidationResult(
                false,
                DurableScanReason.InvalidIdentity);
        }

        ScanRecoveryObservedBindings? observed = await _bindingProbe.ObserveAsync(
            snapshot,
            authorizationScope,
            cancellationToken);
        if (observed is null)
        {
            return new ScanRecoveryRevalidationResult(false, DurableScanReason.InvalidIdentity);
        }

        if (!observed.RepositoryAccessible)
        {
            return new ScanRecoveryRevalidationResult(false, DurableScanReason.Unauthorized);
        }

        if (!observed.ProviderAvailable)
        {
            return new ScanRecoveryRevalidationResult(false, DurableScanReason.ProviderUnavailable);
        }

        if (!HasValidIdentity(snapshot) || !HasValidIdentity(observed))
        {
            return new ScanRecoveryRevalidationResult(false, DurableScanReason.IntegrityViolation);
        }

        if (!string.Equals(snapshot.ProfiledRevision, observed.ProfiledRevision, StringComparison.Ordinal) ||
            !string.Equals(snapshot.PromptIdentity, observed.PromptIdentity, StringComparison.Ordinal) ||
            !string.Equals(snapshot.DocumentSchemaIdentity, observed.DocumentSchemaIdentity, StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.AnalysisRuntimeIdentityHash,
                observed.AnalysisRuntimeIdentityHash,
                StringComparison.Ordinal))
        {
            return new ScanRecoveryRevalidationResult(false, DurableScanReason.InvalidIdentity);
        }

        ScanRecoveryFileBinding[] expectedFiles = snapshot.AffectedFiles
            .OrderBy(value => value.FileCodeUnitId)
            .ToArray();
        ScanRecoveryFileBinding[] observedFiles = observed.AffectedFiles
            .OrderBy(value => value.FileCodeUnitId)
            .ToArray();
        if (!expectedFiles.SequenceEqual(observedFiles))
        {
            return new ScanRecoveryRevalidationResult(false, DurableScanReason.IntegrityViolation);
        }

        AnalysisHealthProbeResult configuredHealth;
        try
        {
            configuredHealth = await _configuredHealthProbe.CheckConfiguredIntegrityAsync(
                CreateRecoveryAnalysisScope(snapshot),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new ScanRecoveryRevalidationResult(false, DurableScanReason.InvalidIdentity);
        }

        if (configuredHealth.Status != AnalysisRuntimeHealthStatus.Ready ||
            !string.Equals(
                configuredHealth.RuntimeIdentityHash,
                snapshot.AnalysisRuntimeIdentityHash,
                StringComparison.Ordinal))
        {
            return new ScanRecoveryRevalidationResult(false, DurableScanReason.InvalidIdentity);
        }

        return new ScanRecoveryRevalidationResult(true, DurableScanReason.None);
    }

    private static AnalysisScope CreateRecoveryAnalysisScope(ScanRecoverySnapshot snapshot) =>
        new(
            snapshot.RepositoryId,
            snapshot.ScanExecutionId,
            snapshot.ProfiledRevision,
            "scan-recovery",
            snapshot.AnalysisRuntimeIdentityHash,
            new AnalysisDeadline(
                DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));

    private static bool HasValidIdentity(ScanRecoverySnapshot snapshot) =>
        IsProfiledRevision(snapshot.ProfiledRevision) &&
        IsIdentity(snapshot.PromptIdentity) &&
        IsIdentity(snapshot.DocumentSchemaIdentity) &&
        IsSha256(snapshot.AnalysisRuntimeIdentityHash) &&
        HasValidFiles(snapshot.AffectedFiles);

    private static bool HasValidIdentity(ScanRecoveryObservedBindings observed) =>
        IsProfiledRevision(observed.ProfiledRevision) &&
        IsIdentity(observed.PromptIdentity) &&
        IsIdentity(observed.DocumentSchemaIdentity) &&
        IsSha256(observed.AnalysisRuntimeIdentityHash) &&
        HasValidFiles(observed.AffectedFiles);

    private static bool HasValidFiles(IReadOnlyList<ScanRecoveryFileBinding>? files) =>
        files is not null &&
        files.All(value => value is not null &&
            value.FileCodeUnitId != Guid.Empty &&
            IsSha256(value.SourceHash)) &&
        files.Select(value => value.FileCodeUnitId).Distinct().Count() == files.Count;

    private static bool IsProfiledRevision(string? value) =>
        value is not null && value.Length is >= 7 and <= 64 && IsLowerHex(value);

    private static bool IsIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128;

    private static bool IsSha256(string? value) =>
        value is not null && value.Length == 64 && IsLowerHex(value);

    private static bool IsLowerHex(string value) =>
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed class ReadinessHealthProbe(AnalysisRuntimeReadiness readiness)
        : IAnalysisRuntimeHealthProbe
    {
        public Task<AnalysisHealthProbeResult> CheckConfiguredIntegrityAsync(
            AnalysisScope scope,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(scope);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(readiness.Current);
        }
    }
}

internal sealed class RuntimeScanRecoveryBindingProbe(
    IRepositoryRepository repositoryRepository,
    IScanRunRepository scanRunRepository,
    IScanRecoveryDispatchIdentitySource dispatchIdentitySource,
    WorkerAgentProviderResolver providerResolver) : IScanRecoveryBindingProbe
{
    public async Task<ScanRecoveryObservedBindings?> ObserveAsync(
        ScanRecoverySnapshot snapshot,
        DurableScanAuthorizationScope authorizationScope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(authorizationScope);
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshot.SystemId == Guid.Empty ||
            snapshot.RepositoryId == Guid.Empty ||
            snapshot.ScanRunId == Guid.Empty ||
            !authorizationScope.OwnedSystemIds.Contains(snapshot.SystemId) ||
            authorizationScope.OwnedRepositoryIds is not { Count: > 0 } ownedRepositoryIds ||
            !ownedRepositoryIds.Contains(snapshot.RepositoryId))
        {
            return null;
        }

        Repository? repository = await repositoryRepository.GetBySystemAndIdAsync(
            snapshot.SystemId,
            snapshot.RepositoryId,
            cancellationToken);
        if (repository?.SystemId != snapshot.SystemId)
        {
            return null;
        }

        ScanRun? scanRun = await scanRunRepository.GetAsync(
            snapshot.RepositoryId,
            snapshot.ScanRunId,
            cancellationToken);
        if (scanRun is null ||
            scanRun.Id != snapshot.ScanRunId ||
            scanRun.RepositoryId != snapshot.RepositoryId ||
            scanRun.Status is ScanRunStatus.Failed or ScanRunStatus.Aborted ||
            !IsProfiledRevision(scanRun.ProfiledRevision))
        {
            return null;
        }

        Guid[] affectedFileIds = snapshot.AffectedFiles?
            .Select(binding => binding.FileCodeUnitId)
            .ToArray() ?? [];
        if (affectedFileIds.Any(value => value == Guid.Empty) ||
            affectedFileIds.Distinct().Count() != affectedFileIds.Length)
        {
            return null;
        }

        CodeUnit[] currentFiles = scanRun.CodeUnits
            .Where(unit => affectedFileIds.Contains(unit.Id))
            .ToArray();
        if (currentFiles.Length != affectedFileIds.Length ||
            currentFiles.Select(unit => unit.Id).Distinct().Count() != currentFiles.Length ||
            currentFiles.Any(unit =>
                unit.ScanRunId != scanRun.Id ||
                unit.RepositoryId != snapshot.RepositoryId ||
                unit.UnitType != CodeUnitType.File ||
                !IsSha256(unit.SourceHash)))
        {
            return null;
        }

        ScanRecoveryDispatchIdentity? dispatchIdentity =
            await dispatchIdentitySource.GetAsync(snapshot.SystemId, cancellationToken);
        if (dispatchIdentity is null ||
            !IsIdentity(dispatchIdentity.PromptIdentity) ||
            !IsIdentity(dispatchIdentity.DocumentSchemaIdentity))
        {
            return null;
        }

        bool providerAvailable;
        try
        {
            _ = await providerResolver.ResolveAsync(snapshot.SystemId, cancellationToken);
            providerAvailable = true;
        }
        catch (InvalidOperationException)
        {
            providerAvailable = false;
        }

        return new ScanRecoveryObservedBindings(
            true,
            scanRun.ProfiledRevision!,
            currentFiles
                .OrderBy(unit => unit.Id)
                .Select(unit => new ScanRecoveryFileBinding(unit.Id, unit.SourceHash))
                .ToArray(),
            dispatchIdentity.PromptIdentity,
            dispatchIdentity.DocumentSchemaIdentity,
            providerAvailable,
            snapshot.AnalysisRuntimeIdentityHash);
    }

    private static bool IsProfiledRevision(string? value) =>
        value is not null &&
        value.Length is >= 7 and <= 64 &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128;

    private static bool IsSha256(string? value) =>
        value is not null &&
        value.Length == 64 &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

internal sealed class UnboundJavaAnalysisSessionFactory : IJavaAnalysisSessionFactory
{
    public Task<IJavaAnalysisSession> OpenAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken) =>
        Task.FromException<IJavaAnalysisSession>(
            new InvalidOperationException("java_analysis_repository_binding_unavailable"));
}

internal sealed class UnboundRevisionSourceReader : IRevisionBoundSourceReader
{
    public Task<SourceExcerptResult> ReadAsync(
        AnalysisScope scope,
        RevisionBoundSourceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(request);
        const string unavailable = "analysis_unavailable";
        return Task.FromResult(new SourceExcerptResult(
            AnalysisOperationStatus.Failed,
            scope.RepositoryId,
            scope.ProfiledRevision,
            request.RelativePath,
            request.Range,
            request.ExpectedSourceSha256,
            unavailable,
            System.Text.Encoding.UTF8.GetByteCount(unavailable),
            new AnalysisPortFailure(
                AnalysisPortFailureReason.NotReady,
                "source_binding_unavailable",
                "source binding unavailable")));
    }
}

internal sealed class RuntimeUnavailableRepositoryProfilingCoordinator
    : IRepositoryProfilingCoordinator
{
    public Task<RepositoryProfilingResult> StartSystemRepositoryAsync(
        Guid systemId,
        Guid repositoryId,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RepositoryProfilingResult(
            RepositoryProfilingOutcome.ProviderUnavailable,
            Message: "Analysis Runtime is NotReady."));
    }

    public Task<RepositoryProfilingResult> StartStandaloneRepositoryAsync(
        Guid repositoryId,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RepositoryProfilingResult(
            RepositoryProfilingOutcome.ProviderUnavailable,
            Message: "Analysis Runtime is NotReady."));
    }

    public Task<ScanRunSummaryDto?> GetSystemRepositoryRunAsync(
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ScanRunSummaryDto?>(null);
    }

    public Task<ScanRunSummaryDto?> GetStandaloneRepositoryRunAsync(
        Guid repositoryId,
        Guid scanRunId,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ScanRunSummaryDto?>(null);
    }
}
