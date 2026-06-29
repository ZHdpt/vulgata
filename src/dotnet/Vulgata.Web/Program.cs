using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Git;
using Vulgata.Shared;
using Vulgata.Shared.Repositories;
using Vulgata.Shared.Systems;
using Vulgata.Shared.Validators.Repositories;
using Vulgata.Shared.Validators.Systems;
using Vulgata.Web.Components;
using Vulgata.Web.Components.Account;
using Vulgata.Web.Data;
using Vulgata.Web.Validators;
using SystemEntity = Vulgata.Core.Entities.System;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddProblemDetails();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsHistoryTable("__IdentityMigrationsHistory", "identity");
        npgsqlOptions.EnableRetryOnFailure(3);
    }));
builder.Services.AddDbContext<VulgataDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(3)));
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
builder.Services.AddScoped<IRepositoryManagementCoordinator, RepositoryManagementCoordinator>();
builder.Services.AddScoped<IGitRemoteValidationService, GitRemoteValidationService>();
builder.Services.AddScoped<IScanStateService, NoOpScanStateService>();
builder.Services.AddScoped<IContextCompositionService, ContextCompositionService>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateSystemRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<AssignSystemOwnerRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateRepositoryRequestValidator>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, BcryptPasswordHasher<ApplicationUser>>();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var startupLogger = loggerFactory.CreateLogger("Startup");

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
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
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

// System CRUD API endpoints
RouteGroupBuilder systemsApi = app.MapGroup("/api/systems")
    .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

systemsApi.MapGet("/", async (
    ISystemRepository repository,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    bool isAdministrator = authResult.Succeeded;

    IReadOnlyList<SystemEntity> systems = await repository.ListVisibleAsync(userId, isAdministrator, cancellationToken);

    return Results.Ok(systems.Select(s => new
    {
        id = s.Id,
        name = s.Name,
        description = s.Description,
        context = s.Context,
        createdAt = s.CreatedAt,
        updatedAt = s.UpdatedAt,
    }));
});

systemsApi.MapPost("/", async (
    CreateSystemRequest request,
    IValidator<CreateSystemRequest> validator,
    ISystemRepository repository,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    if (!authResult.Succeeded)
    {
        return Results.Problem(
            detail: "只有管理员可以修改系统。",
            statusCode: StatusCodes.Status403Forbidden);
    }

    FluentValidation.Results.ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    bool nameExists = await repository.NameExistsAsync(request.Name, cancellationToken: cancellationToken);
    if (nameExists)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Name"] = ["系统名称已存在。"],
        });
    }

    SystemEntity newSystem = new(
        request.Name,
        request.Description,
        request.Context,
        DateTimeOffset.UtcNow);

    await repository.AddAsync(newSystem, cancellationToken);
    await repository.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/systems/{newSystem.Id}", new
    {
        id = newSystem.Id,
        name = newSystem.Name,
        description = newSystem.Description,
        context = newSystem.Context,
        createdAt = newSystem.CreatedAt,
        updatedAt = newSystem.UpdatedAt,
    });
});

systemsApi.MapPut("/{id:guid}", async (
    Guid id,
    UpdateSystemRequest request,
    IValidator<UpdateSystemRequest> validator,
    ISystemRepository repository,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    if (!authResult.Succeeded)
    {
        return Results.Problem(
            detail: "只有管理员可以修改系统。",
            statusCode: StatusCodes.Status403Forbidden);
    }

    FluentValidation.Results.ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    bool nameExists = await repository.NameExistsAsync(request.Name, id, cancellationToken);
    if (nameExists)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Name"] = ["系统名称已存在。"],
        });
    }

    SystemEntity? existing = await repository.GetByIdAsync(id, cancellationToken);
    if (existing is null)
    {
        return Results.Problem(
            detail: "系统不存在。",
            statusCode: StatusCodes.Status404NotFound);
    }

    existing.UpdateDetails(request.Name, request.Description, request.Context, DateTimeOffset.UtcNow);
    await repository.SaveChangesAsync(cancellationToken);

    return Results.Ok(new
    {
        id = existing.Id,
        name = existing.Name,
        description = existing.Description,
        context = existing.Context,
        createdAt = existing.CreatedAt,
        updatedAt = existing.UpdatedAt,
    });
});

systemsApi.MapDelete("/{id:guid}", async (
    Guid id,
    ISystemRepository repository,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    if (!authResult.Succeeded)
    {
        return Results.Problem(
            detail: "只有管理员可以修改系统。",
            statusCode: StatusCodes.Status403Forbidden);
    }

    SystemDeleteResult result = await repository.DeleteIfNoDependenciesAsync(id, cancellationToken);

    if (result.Outcome == SystemDeleteOutcome.Deleted)
    {
        await repository.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    if (result.Outcome == SystemDeleteOutcome.NotFound)
    {
        return Results.Problem(
            detail: "系统不存在。",
            statusCode: StatusCodes.Status404NotFound);
    }

    return Results.Problem(
        detail: $"请先移除依赖（{result.RepositoryCount} 个仓库、{result.OwnerAssignmentCount} 个所有者分配），再删除该系统。",
        statusCode: StatusCodes.Status409Conflict);
});

systemsApi.MapGet("/{systemId:guid}/owners", async (
    Guid systemId,
    ISystemRepository repository,
    ISystemOwnershipCoordinator ownershipCoordinator,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    if (!authResult.Succeeded)
    {
        return Results.Problem(
            detail: "只有管理员可以管理系统所有者。",
            statusCode: StatusCodes.Status403Forbidden);
    }

    if (await repository.GetByIdAsync(systemId, cancellationToken) is null)
    {
        return Results.Problem(
            detail: "系统不存在。",
            statusCode: StatusCodes.Status404NotFound);
    }

    IReadOnlyList<SystemOwnerAssignmentDto> owners = await ownershipCoordinator.ListOwnersAsync(systemId, cancellationToken);
    return Results.Ok(owners);
});

systemsApi.MapGet("/{systemId:guid}/owner-candidates", async (
    Guid systemId,
    string? keyword,
    ISystemRepository repository,
    ISystemOwnershipCoordinator ownershipCoordinator,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    if (!authResult.Succeeded)
    {
        return Results.Problem(
            detail: "只有管理员可以管理系统所有者。",
            statusCode: StatusCodes.Status403Forbidden);
    }

    if (await repository.GetByIdAsync(systemId, cancellationToken) is null)
    {
        return Results.Problem(
            detail: "系统不存在。",
            statusCode: StatusCodes.Status404NotFound);
    }

    IReadOnlyList<SystemOwnerCandidateDto> candidates =
        await ownershipCoordinator.ListOwnerCandidatesAsync(systemId, keyword, cancellationToken);

    return Results.Ok(candidates);
});

systemsApi.MapPost("/{systemId:guid}/owners", async (
    Guid systemId,
    AssignSystemOwnerRequest request,
    IValidator<AssignSystemOwnerRequest> validator,
    ISystemOwnershipCoordinator ownershipCoordinator,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    if (!authResult.Succeeded)
    {
        return Results.Problem(
            detail: "只有管理员可以管理系统所有者。",
            statusCode: StatusCodes.Status403Forbidden);
    }

    FluentValidation.Results.ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    SystemOwnershipAssignmentResult result = await ownershipCoordinator.AssignOwnerAsync(
        systemId,
        request.UserId,
        cancellationToken);

    if (result.Outcome == SystemOwnershipAssignmentOutcome.Assigned)
    {
        return Results.NoContent();
    }

    if (result.Outcome == SystemOwnershipAssignmentOutcome.SystemNotFound)
    {
        return Results.Problem(
            detail: "系统不存在。",
            statusCode: StatusCodes.Status404NotFound);
    }

    if (result.Outcome == SystemOwnershipAssignmentOutcome.UserNotFound)
    {
        return Results.Problem(
            detail: "用户不存在。",
            statusCode: StatusCodes.Status404NotFound);
    }

    if (result.Outcome == SystemOwnershipAssignmentOutcome.UserIsAdministrator)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["UserId"] = ["管理员已具备全局权限，无需分配系统所有者。"],
        });
    }

    if (result.Outcome == SystemOwnershipAssignmentOutcome.AlreadyAssigned)
    {
        return Results.Problem(
            detail: "该用户已是该系统的所有者。",
            statusCode: StatusCodes.Status409Conflict);
    }

    return Results.Problem(
        detail: BuildIdentityErrorMessage(result.IdentityResult?.Errors),
        statusCode: StatusCodes.Status500InternalServerError);
});

systemsApi.MapDelete("/{systemId:guid}/owners/{userId}", async (
    Guid systemId,
    string userId,
    ISystemOwnershipCoordinator ownershipCoordinator,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    if (!authResult.Succeeded)
    {
        return Results.Problem(
            detail: "只有管理员可以管理系统所有者。",
            statusCode: StatusCodes.Status403Forbidden);
    }

    SystemOwnershipRemovalResult result = await ownershipCoordinator.RemoveOwnerAsync(systemId, userId, cancellationToken);

    if (result.Outcome == SystemOwnershipRemovalOutcome.Removed)
    {
        return Results.NoContent();
    }

    if (result.Outcome == SystemOwnershipRemovalOutcome.SystemNotFound)
    {
        return Results.Problem(
            detail: "系统不存在。",
            statusCode: StatusCodes.Status404NotFound);
    }

    if (result.Outcome == SystemOwnershipRemovalOutcome.AssignmentNotFound)
    {
        return Results.Problem(
            detail: "系统所有者分配不存在。",
            statusCode: StatusCodes.Status404NotFound);
    }

    return Results.Problem(
        detail: BuildIdentityErrorMessage(result.IdentityResult?.Errors),
        statusCode: StatusCodes.Status500InternalServerError);
});

// Repository CRUD API endpoints (Story 2.3)
RouteGroupBuilder repoApi = app.MapGroup("/api/systems/{systemId:guid}/repositories")
    .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

repoApi.MapGet("/", async (
    Guid systemId,
    ISystemRepository systemRepository,
    IRepositoryRepository repositoryRepository,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    bool isAdministrator = authResult.Succeeded;
    string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    if (await systemRepository.GetVisibleByIdAsync(systemId, userId, isAdministrator, cancellationToken) is null)
    {
        return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
    }

    IReadOnlyList<RepositoryEntity> repos = await repositoryRepository.ListBySystemAsync(systemId, cancellationToken);

    return Results.Ok(repos.Select(r => new
    {
        id = r.Id,
        systemId = r.SystemId,
        name = r.Name,
        gitUrl = r.GitUrl,
        description = r.Description,
        context = r.Context,
        scanStatus = "未扫描",
        lastScannedAt = (DateTimeOffset?)null,
        documentCount = 0,
    }));
});

repoApi.MapPost("/", async (
    Guid systemId,
    CreateRepositoryRequest request,
    IValidator<CreateRepositoryRequest> validator,
    ISystemRepository systemRepository,
    IRepositoryRepository repositoryRepository,
    IGitRemoteValidationService gitValidation,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    bool isAdministrator = authResult.Succeeded;
    string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    if (await systemRepository.GetVisibleByIdAsync(systemId, userId, isAdministrator, cancellationToken) is null)
    {
        return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
    }

    FluentValidation.Results.ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    bool nameExists = await repositoryRepository.NameExistsAsync(systemId, request.Name, cancellationToken: cancellationToken);
    if (nameExists)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Name"] = ["仓库名称已存在。"],
        });
    }

    GitRemoteValidationResult gitResult = await gitValidation.ValidateAsync(request.GitUrl, cancellationToken);
    if (gitResult.Status == GitRemoteValidationStatus.AuthenticationRequired)
    {
        return Results.Problem(
            detail: $"Git URL 不可达：{gitResult.Message}",
            statusCode: StatusCodes.Status400BadRequest);
    }

    if (gitResult.Status == GitRemoteValidationStatus.Unreachable)
    {
        return Results.Problem(
            detail: $"Git URL 不可达：{gitResult.Message}",
            statusCode: StatusCodes.Status400BadRequest);
    }

    DateTimeOffset now = DateTimeOffset.UtcNow;
    RepositoryEntity newRepo = RepositoryEntity.Create(systemId, request.Name, request.GitUrl, request.Description, request.Context, now);

    await repositoryRepository.AddAsync(newRepo, cancellationToken);
    await repositoryRepository.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/systems/{systemId}/repositories/{newRepo.Id}", new
    {
        id = newRepo.Id,
        systemId = newRepo.SystemId,
        name = newRepo.Name,
        gitUrl = newRepo.GitUrl,
        description = newRepo.Description,
        context = newRepo.Context,
        scanStatus = "未扫描",
        lastScannedAt = (DateTimeOffset?)null,
        documentCount = 0,
    });
});

repoApi.MapDelete("/{repositoryId:guid}", async (
    Guid systemId,
    Guid repositoryId,
    ISystemRepository systemRepository,
    IRepositoryRepository repositoryRepository,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    bool isAdministrator = authResult.Succeeded;
    string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    if (await systemRepository.GetVisibleByIdAsync(systemId, userId, isAdministrator, cancellationToken) is null)
    {
        return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
    }

    RepositoryEntity? repo = await repositoryRepository.GetBySystemAndIdAsync(systemId, repositoryId, cancellationToken);
    if (repo is null)
    {
        return Results.Problem(detail: "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
    }

    repositoryRepository.Remove(repo);
    await repositoryRepository.SaveChangesAsync(cancellationToken);

    return Results.NoContent();
});

RouteGroupBuilder standaloneRepoApi = app.MapGroup("/api/repositories/standalone")
    .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

standaloneRepoApi.MapGet("/", async (
    IRepositoryManagementCoordinator repositoryCoordinator,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    bool isAdministrator = authResult.Succeeded;
    string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    IReadOnlyList<RepositorySummaryDto> repositories = await repositoryCoordinator.ListStandaloneVisibleAsync(
        userId,
        isAdministrator,
        cancellationToken);

    return Results.Ok(repositories);
});

standaloneRepoApi.MapPost("/", async (
    CreateRepositoryRequest request,
    IValidator<CreateRepositoryRequest> validator,
    IRepositoryManagementCoordinator repositoryCoordinator,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    bool isAdministrator = authResult.Succeeded;
    string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    FluentValidation.Results.ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    RepositoryMutationResult<RepositoryDetailDto> result = await repositoryCoordinator.CreateStandaloneAsync(
        request,
        userId,
        isAdministrator,
        cancellationToken);

    if (result.Outcome == RepositoryMutationOutcome.Success && result.Value is not null)
    {
        return Results.Created($"/api/repositories/standalone/{result.Value.Id}", result.Value);
    }

    if (result.Outcome == RepositoryMutationOutcome.DuplicateName)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Name"] = [result.Message ?? "独立仓库名称已存在。"],
        });
    }

    if (result.Outcome == RepositoryMutationOutcome.GitAuthenticationRequired
        || result.Outcome == RepositoryMutationOutcome.GitUnreachable)
    {
        return Results.Problem(
            detail: $"Git URL 不可达：{result.Message}",
            statusCode: StatusCodes.Status400BadRequest);
    }

    return Results.Problem(
        detail: "独立仓库创建失败。",
        statusCode: StatusCodes.Status500InternalServerError);
});

// Context management API endpoints (Story 2.5)
RouteGroupBuilder settingsApi = app.MapGroup("/api/settings")
    .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

settingsApi.MapGet("/global-context", async (
    IDbContextFactory<VulgataDbContext> dbFactory,
    IScanStateService scanState,
    CancellationToken cancellationToken) =>
{
    await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
    GlobalContext? global = await db.GlobalContexts
        .OrderByDescending(g => g.UpdatedAt)
        .FirstOrDefaultAsync(cancellationToken);

    PendingContextChange? pending = await db.PendingContextChanges
        .Where(p => p.ScopeType == ContextScopeType.Global && p.ScopeKey == "global")
        .FirstOrDefaultAsync(cancellationToken);

    return Results.Ok(new ContextStateResponse
    {
        CurrentContext = global?.Context,
        PendingContext = pending?.Context,
        Queued = pending is not null,
        StatusMessage = pending is not null ? "上下文修改将在当前扫描完成后生效" : null,
    });
}).RequireAuthorization(AuthorizationPolicyNames.AdministratorOnly);

settingsApi.MapPut("/global-context", async (
    HttpRequest request,
    IDbContextFactory<VulgataDbContext> dbFactory,
    IScanStateService scanState,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    if (!authResult.Succeeded)
    {
        return Results.Forbid();
    }

    ContextSaveRequest? body = await request.ReadFromJsonAsync<ContextSaveRequest>(cancellationToken);
    if (body is null)
    {
        return Results.Problem(detail: "请求格式无效。", statusCode: StatusCodes.Status400BadRequest);
    }

    string normalizedContext = string.IsNullOrWhiteSpace(body.Context) ? string.Empty : body.Context.Trim();
    DateTimeOffset now = DateTimeOffset.UtcNow;

    await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

    bool scanRunning = await scanState.IsAnyScanRunningAsync(cancellationToken);

    if (scanRunning)
    {
        PendingContextChange? pending = await db.PendingContextChanges
            .Where(p => p.ScopeType == ContextScopeType.Global && p.ScopeKey == "global")
            .FirstOrDefaultAsync(cancellationToken);

        if (pending is null)
        {
            db.PendingContextChanges.Add(new PendingContextChange(
                ContextScopeType.Global, "global", string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now));
        }
        else
        {
            pending.UpdateContext(string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now);
        }

        await db.SaveChangesAsync(cancellationToken);

        GlobalContext? currentGlobal = await db.GlobalContexts
            .OrderByDescending(g => g.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Results.Ok(new ContextStateResponse
        {
            CurrentContext = currentGlobal?.Context,
            PendingContext = string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext,
            Queued = true,
            StatusMessage = "上下文修改将在当前扫描完成后生效",
        });
    }

    // No scan running — apply immediately
    GlobalContext? existing = await db.GlobalContexts
        .OrderByDescending(g => g.UpdatedAt)
        .FirstOrDefaultAsync(cancellationToken);

    if (existing is null)
    {
        db.GlobalContexts.Add(new GlobalContext(string.IsNullOrEmpty(normalizedContext) ? string.Empty : normalizedContext, now));
    }
    else
    {
        existing.UpdateContext(normalizedContext, now);
    }

    // Clear any pending changes for global scope
    PendingContextChange? existingPending = await db.PendingContextChanges
        .Where(p => p.ScopeType == ContextScopeType.Global && p.ScopeKey == "global")
        .FirstOrDefaultAsync(cancellationToken);
    if (existingPending is not null)
    {
        db.PendingContextChanges.Remove(existingPending);
    }

    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new ContextStateResponse
    {
        CurrentContext = string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext,
        PendingContext = null,
        Queued = false,
        StatusMessage = null,
    });
}).RequireAuthorization(AuthorizationPolicyNames.AdministratorOnly);

// System context endpoints
RouteGroupBuilder systemContextApi = app.MapGroup("/api/systems/{systemId:guid}/context")
    .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

systemContextApi.MapGet("/", async (
    Guid systemId,
    ISystemRepository systemRepository,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    IDbContextFactory<VulgataDbContext> dbFactory,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    bool isAdministrator = authResult.Succeeded;
    string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    SystemEntity? system = await systemRepository.GetVisibleByIdAsync(systemId, userId, isAdministrator, cancellationToken);
    if (system is null)
    {
        return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
    }

    await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
    PendingContextChange? pending = await db.PendingContextChanges
        .Where(p => p.ScopeType == ContextScopeType.System && p.ScopeKey == systemId.ToString())
        .FirstOrDefaultAsync(cancellationToken);

    return Results.Ok(new ContextStateResponse
    {
        CurrentContext = system.Context,
        PendingContext = pending?.Context,
        Queued = pending is not null,
        StatusMessage = pending is not null ? "上下文修改将在当前扫描完成后生效" : null,
    });
});

systemContextApi.MapPut("/", async (
    Guid systemId,
    HttpRequest request,
    ISystemRepository systemRepository,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    IDbContextFactory<VulgataDbContext> dbFactory,
    IScanStateService scanState,
    CancellationToken cancellationToken) =>
{
    AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
    bool isAdministrator = authResult.Succeeded;
    string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    SystemEntity? system = await systemRepository.GetVisibleByIdAsync(systemId, userId, isAdministrator, cancellationToken);
    if (system is null)
    {
        return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
    }

    ContextSaveRequest? body = await request.ReadFromJsonAsync<ContextSaveRequest>(cancellationToken);
    if (body is null)
    {
        return Results.Problem(detail: "请求格式无效。", statusCode: StatusCodes.Status400BadRequest);
    }

    string normalizedContext = string.IsNullOrWhiteSpace(body.Context) ? string.Empty : body.Context.Trim();
    DateTimeOffset now = DateTimeOffset.UtcNow;

    await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
    bool scanRunning = await scanState.IsSystemScanRunningAsync(systemId, cancellationToken);

    if (scanRunning)
    {
        PendingContextChange? pending = await db.PendingContextChanges
            .Where(p => p.ScopeType == ContextScopeType.System && p.ScopeKey == systemId.ToString())
            .FirstOrDefaultAsync(cancellationToken);

        if (pending is null)
        {
            db.PendingContextChanges.Add(new PendingContextChange(
                ContextScopeType.System, systemId.ToString(), string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now));
        }
        else
        {
            pending.UpdateContext(string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ContextStateResponse
        {
            CurrentContext = system.Context,
            PendingContext = string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext,
            Queued = true,
            StatusMessage = "上下文修改将在当前扫描完成后生效",
        });
    }

    // No scan — apply immediately
    system.UpdateDetails(system.Name, system.Description, string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now);
    await systemRepository.SaveChangesAsync(cancellationToken);

    // Clear any pending
    PendingContextChange? existingPending = await db.PendingContextChanges
        .Where(p => p.ScopeType == ContextScopeType.System && p.ScopeKey == systemId.ToString())
        .FirstOrDefaultAsync(cancellationToken);
    if (existingPending is not null)
    {
        db.PendingContextChanges.Remove(existingPending);
        await db.SaveChangesAsync(cancellationToken);
    }

    return Results.Ok(new ContextStateResponse
    {
        CurrentContext = string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext,
        PendingContext = null,
        Queued = false,
        StatusMessage = null,
    });
});

// Repository context endpoints
RouteGroupBuilder repoContextApi = app.MapGroup("/api/repositories/{repositoryId:guid}/context")
    .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

repoContextApi.MapGet("/", async (
    Guid repositoryId,
    IRepositoryRepository repositoryRepository,
    IDbContextFactory<VulgataDbContext> dbFactory,
    CancellationToken cancellationToken) =>
{
    RepositoryEntity? repo = await repositoryRepository.GetByIdAsync(repositoryId, cancellationToken);
    if (repo is null)
    {
        return Results.Problem(detail: "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
    }

    await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
    PendingContextChange? pending = await db.PendingContextChanges
        .Where(p => p.ScopeType == ContextScopeType.Repository && p.ScopeKey == repositoryId.ToString())
        .FirstOrDefaultAsync(cancellationToken);

    return Results.Ok(new ContextStateResponse
    {
        CurrentContext = repo.Context,
        PendingContext = pending?.Context,
        Queued = pending is not null,
        StatusMessage = pending is not null ? "上下文修改将在当前扫描完成后生效" : null,
    });
});

repoContextApi.MapPut("/", async (
    Guid repositoryId,
    HttpRequest request,
    IRepositoryRepository repositoryRepository,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    IDbContextFactory<VulgataDbContext> dbFactory,
    IScanStateService scanState,
    CancellationToken cancellationToken) =>
{
    string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    RepositoryEntity? repo = await repositoryRepository.GetByIdAsync(repositoryId, cancellationToken);
    if (repo is null)
    {
        return Results.Problem(detail: "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
    }

    ContextSaveRequest? body = await request.ReadFromJsonAsync<ContextSaveRequest>(cancellationToken);
    if (body is null)
    {
        return Results.Problem(detail: "请求格式无效。", statusCode: StatusCodes.Status400BadRequest);
    }

    string normalizedContext = string.IsNullOrWhiteSpace(body.Context) ? string.Empty : body.Context.Trim();
    DateTimeOffset now = DateTimeOffset.UtcNow;

    await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
    bool scanRunning = await scanState.IsRepositoryScanRunningAsync(repositoryId, cancellationToken);

    if (scanRunning)
    {
        PendingContextChange? pending = await db.PendingContextChanges
            .Where(p => p.ScopeType == ContextScopeType.Repository && p.ScopeKey == repositoryId.ToString())
            .FirstOrDefaultAsync(cancellationToken);

        if (pending is null)
        {
            db.PendingContextChanges.Add(new PendingContextChange(
                ContextScopeType.Repository, repositoryId.ToString(), string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now));
        }
        else
        {
            pending.UpdateContext(string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ContextStateResponse
        {
            CurrentContext = repo.Context,
            PendingContext = string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext,
            Queued = true,
            StatusMessage = "上下文修改将在当前扫描完成后生效",
        });
    }

    // No scan — apply immediately
    repo.UpdateDetails(repo.Name, repo.GitUrl, repo.Description, string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now);
    await repositoryRepository.SaveChangesAsync(cancellationToken);

    PendingContextChange? existingPending = await db.PendingContextChanges
        .Where(p => p.ScopeType == ContextScopeType.Repository && p.ScopeKey == repositoryId.ToString())
        .FirstOrDefaultAsync(cancellationToken);
    if (existingPending is not null)
    {
        db.PendingContextChanges.Remove(existingPending);
        await db.SaveChangesAsync(cancellationToken);
    }

    return Results.Ok(new ContextStateResponse
    {
        CurrentContext = string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext,
        PendingContext = null,
        Queued = false,
        StatusMessage = null,
    });
});

static string BuildIdentityErrorMessage(IEnumerable<IdentityError>? errors)
{
    if (errors is null)
    {
        return "系统所有者角色变更失败，请稍后重试。";
    }

    string detail = string.Join("；", errors.Select(error => error.Description));
    return string.IsNullOrWhiteSpace(detail)
        ? "系统所有者角色变更失败，请稍后重试。"
        : detail;
}

await app.RunAsync();

public partial class Program
{
}
