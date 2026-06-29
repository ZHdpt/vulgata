using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Web.Components;
using Vulgata.Web.Components.Account;
using Vulgata.Web.Data;
using Vulgata.Web.Validators;
using SystemEntity = Vulgata.Core.Entities.System;

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

builder.Services.AddScoped<ISystemRepository, SystemRepository>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateSystemRequestValidator>();

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

await app.RunAsync();

public partial class Program
{
}
