using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Web.Components;
using Vulgata.Web.Components.Account;
using Vulgata.Web.Data;

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

await app.RunAsync();

public partial class Program
{
}
