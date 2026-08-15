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
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.BackupB;
using Vulgata.Infrastructure.CodeGraph;
using Vulgata.Infrastructure.CommunicationDetection;
using Vulgata.Infrastructure.ContextManagement;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.FrameworkDetection;
using Vulgata.Infrastructure.Git;
using Vulgata.Infrastructure.Git.BackupB;
using Vulgata.Shared;
using Vulgata.Shared.Validators.Repositories;
using Vulgata.Shared.Validators.Systems;
using Vulgata.Web.Api;
using Vulgata.Web.Components;
using Vulgata.Web.Components.Account;
using Vulgata.Web.Components.Pages;
using Vulgata.Web.Data;
using Vulgata.Web.RequestPipeline;
using Vulgata.Web.Validators;

var builder = WebApplication.CreateBuilder(args);

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
        builder.Services.AddHttpClient<ICodeGraphProvider, CodeGraphContextProvider>(client =>
            client.Timeout = Timeout.InfiniteTimeSpan);
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
        builder.Services.AddScoped<IScanRunRepository, ScanRunRepository>();
        builder.Services.AddScoped<IApprovedPreScanHandoffSource, ApprovedPreScanHandoffSource>();
        builder.Services.AddScoped<ICodeUnitSource, CodeUnitSource>();
        builder.Services.AddScoped<IScanExecutionStore, ScanExecutionRepository>();
        builder.Services.AddScoped<IRepositoryAuthorizationVerifier, RepositoryAuthorizationVerifier>();
        builder.Services.AddScoped<IChatEvidenceRetriever, ChatEvidenceRetriever>();
        builder.Services.AddScoped<IPreparedChatScopeResolver, PreparedChatScopeResolver>();
        builder.Services.AddScoped<IChatEvidenceCurrentnessValidator, ChatEvidenceCurrentnessValidator>();
        builder.Services.AddScoped<ChatCandidateExtractor>();
        builder.Services.AddScoped<ChatAgentProviderResolver>();
        builder.Services.AddScoped<ChatPromptCatalog>();
        builder.Services.AddScoped<IChatAgentDispatcher, ChatAgentDispatcher>();
        builder.Services.AddScoped<ChatAgentResultValidator>();
        builder.Services.AddScoped<PreparedChatCoordinator>();
        builder.Services.AddScoped<IChatRevealDelay, ChatRevealDelay>();
        builder.Services.AddScoped<DemoScanUiState>();
        builder.Services.AddSingleton<ExactResolutionProfileCatalog>();
        builder.Services.AddScoped<IExactGitRevisionSnapshotService, ExactGitRevisionSnapshotService>();
        builder.Services.AddScoped<ExactCandidateExtractors>();
        builder.Services.AddScoped<IExactCrossRepositoryResolver, ExactCrossRepositoryResolver>();
        builder.Services.AddScoped<IExactCrossRepositoryResolutionStore, ExactCrossRepositoryResolutionStore>();
        builder.Services.AddScoped<ExactCrossRepositoryResolutionCoordinator>();
        builder.Services.AddScoped<IPreparedDemoPass2FlowPlanner, PreparedDemoPass2FlowPlanner>();
        builder.Services.AddScoped<RevisionBoundWorkerContextHydrator>();
        builder.Services.AddScoped<IWorkerAgentExecutionContextSource, RevisionBoundWorkerExecutionContextSource>();
        builder.Services.AddScoped<WorkerAgentProviderResolver>();
        builder.Services.AddScoped<IWorkerAgentDispatcher, WorkerAgentDispatcher>();
        builder.Services.AddScoped<IWorkerAgentResultCommitter, WorkerAgentResultCommitter>();
        builder.Services.AddScoped<SequentialScanRunner>();
        builder.Services.AddScoped<IBackupBDirectProvisioner, BackupBDirectProvisioner>();
        builder.Services.AddScoped<HttpMessageHandler, SocketsHttpHandler>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IDatabaseConnectionRepository, DatabaseConnectionRepository>();
        builder.Services.AddScoped<ILlmProviderRepository, LlmProviderRepository>();
        builder.Services.AddScoped<ISystemLlmProviderOverrideRepository, SystemLlmProviderOverrideRepository>();
        builder.Services.AddScoped<IRepositoryManagementCoordinator, RepositoryManagementCoordinator>();
        builder.Services.AddScoped<IRepositoryProfilingCoordinator, RepositoryProfilingCoordinator>();
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
        builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, BcryptPasswordHasher<ApplicationUser>>();
        builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
        return frameworkCatalog;
    }
}
