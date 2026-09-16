using System.Security.Claims;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Web.Data;

namespace Vulgata.Tests.RetainedSource;

/// <summary>
/// Composes one production Web service graph over an owned PostgreSQL database,
/// then seeds the real Identity/domain records used by source authorization. The
/// scope is created only after migrations and seeding, and is disposed before the
/// database is dropped.
/// </summary>
internal sealed class RetainedSourceCaptureApplicationFixture : IAsyncDisposable
{
    private RetainedSourceCaptureApplicationFixture(
        RetainedSourceFixture source,
        RetainedSourcePostgreSqlDatabase database,
        WebApplication application,
        IServiceScope scope,
        string userId,
        Guid systemId)
    {
        Source = source;
        Database = database;
        Application = application;
        Scope = scope;
        UserId = userId;
        SystemId = systemId;
        RetainedSourceApplication = scope.ServiceProvider.GetService<IRetainedSourceApplication>();
    }

    internal RetainedSourceFixture Source { get; }

    internal RetainedSourcePostgreSqlDatabase Database { get; }

    internal WebApplication Application { get; }

    internal IServiceScope Scope { get; }

    internal string UserId { get; }

    internal Guid SystemId { get; }

    internal IRetainedSourceApplication? RetainedSourceApplication { get; }

    internal static async Task<RetainedSourceCaptureApplicationFixture> CreateAsync(
        RetainedSourceFixture source,
        bool standalone = false,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
        bool authenticated = true,
        bool administrator = true,
        bool systemOwner = false,
        TimeProvider? timeProvider = null,
        Func<string, Task>? mutateBeforeComposition = null,
        Func<string, Task>? mutateAfterFirstAuthorization = null,
        int mutateAfterAuthorizationCount = 1,
        Func<SourceAuthorizationRequest, CancellationToken, Task>? authorizationWait = null,
        Func<SourceAuthorizationRequest, CancellationToken, Task>? beforeAuthorization = null,
        Action<IServiceCollection>? configureServices = null,
        string? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        RetainedSourcePostgreSqlDatabase database =
            await RetainedSourcePostgreSqlDatabase.CreateAsync(cancellationToken);
        string userId = subjectId ?? $"retained-source-{Guid.NewGuid():N}";
        Guid systemId = Guid.Parse("13000000-0000-4000-8000-000000000201");
        try
        {
            await SeedDatabaseAsync(
                database.ConnectionString,
                source,
                userId,
                systemId,
                standalone,
                administrator,
                systemOwner,
                cancellationToken);
            if (mutateBeforeComposition is not null)
            {
                await mutateBeforeComposition(database.ConnectionString);
            }
            WebApplication application = BuildApplication(
                source.SourceStorageRoot,
                database.ConnectionString,
                userId,
                configurationOverrides,
                authenticated,
                administrator,
                systemOwner,
                timeProvider,
                mutateAfterFirstAuthorization is null
                    ? null
                    : () => mutateAfterFirstAuthorization(database.ConnectionString),
                mutateAfterAuthorizationCount,
                authorizationWait,
                beforeAuthorization,
                configureServices);
            IServiceScope scope = application.Services.CreateScope();
            IHttpContextAccessor accessor =
                application.Services.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = CreateHttpContext(
                application.Services,
                userId,
                authenticated,
                administrator,
                systemOwner);
            return new(source, database, application, scope, userId, systemId);
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    internal static RetainedSourceAttachedApplication CreateAttached(
        string connectionString,
        string sourceStorageRoot,
        string userId,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
        Func<SourceAuthorizationRequest, CancellationToken, Task>? authorizationWait = null,
        string? dataProtectionKeyRingPath = null,
        TimeProvider? timeProvider = null,
        bool createAuxiliaryRoots = true,
        Action<IServiceCollection>? configureServices = null)
    {
        WebApplication application = BuildApplication(
            sourceStorageRoot,
            connectionString,
            userId,
            configurationOverrides,
            authenticated: true,
            administrator: true,
            systemOwner: false,
            timeProvider: timeProvider,
            mutateAfterFirstAuthorization: null,
            mutateAfterAuthorizationCount: 1,
            authorizationWait: authorizationWait,
            beforeAuthorization: null,
            configureServices: configureServices,
            dataProtectionKeyRingPath: dataProtectionKeyRingPath,
            createAuxiliaryRoots: createAuxiliaryRoots);
        IServiceScope scope = application.Services.CreateScope();
        IHttpContextAccessor accessor =
            application.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = CreateHttpContext(
            application.Services,
            userId,
            authenticated: true,
            administrator: true,
            systemOwner: false);
        return new(application, scope);
    }

    public async ValueTask DisposeAsync()
    {
        Scope.Dispose();
        await Application.DisposeAsync();
        await Database.DisposeAsync();
        await Source.DisposeAsync();
    }

    private static WebApplication BuildApplication(
        string sourceStorageRoot,
        string connectionString,
        string userId,
        IReadOnlyDictionary<string, string?>? configurationOverrides,
        bool authenticated,
        bool administrator,
        bool systemOwner,
        TimeProvider? timeProvider,
        Func<Task>? mutateAfterFirstAuthorization,
        int mutateAfterAuthorizationCount,
        Func<SourceAuthorizationRequest, CancellationToken, Task>? authorizationWait,
        Func<SourceAuthorizationRequest, CancellationToken, Task>? beforeAuthorization,
        Action<IServiceCollection>? configureServices,
        string? dataProtectionKeyRingPath = null,
        bool createAuxiliaryRoots = true)
    {
        var options = new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            ContentRootPath = Directory.GetCurrentDirectory(),
            EnvironmentName = "Testing",
        };
        WebApplicationBuilder builder = WebApplication.CreateBuilder(options);
        builder.Logging.ClearProviders();
        string keyRingPath = dataProtectionKeyRingPath ?? Path.Combine(
            Directory.GetParent(sourceStorageRoot)!.FullName,
            ".test-keyring");
        string fixtureOwnerRoot = Directory.GetParent(keyRingPath)!.FullName;
        string codeGraphRoot = Path.Combine(fixtureOwnerRoot, ".test-codegraph");
        string jdtLsRoot = Path.Combine(fixtureOwnerRoot, ".test-jdtls");
        if (createAuxiliaryRoots)
        {
            Directory.CreateDirectory(codeGraphRoot);
            Directory.CreateDirectory(jdtLsRoot);
            Directory.CreateDirectory(keyRingPath);
        }
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["SourceStorage:RootPath"] = sourceStorageRoot,
            ["DataProtection:KeyRingPath"] = keyRingPath,
            ["DataProtection:ApplicationName"] = "Vulgata.Tests.RetainedSource",
            ["CodeGraph:DataRootPath"] = codeGraphRoot,
            ["AnalysisRuntime:JdtLsWorkspaceRoot"] = jdtLsRoot,
            ["SourceStorage:MaximumEntries"] = "10000",
            ["SourceStorage:MaximumBlobBytes"] = "8388608",
            ["SourceStorage:MaximumAggregateBlobBytes"] = "67108864",
            ["SourceStorage:CaptureDeadlineSeconds"] = "600",
            ["SourceStorage:OperationStaleAfterMinutes"] = "1440",
            ["SourceStorage:VerificationBufferBytes"] = "65536",
        };
        if (configurationOverrides is not null)
        {
            foreach ((string key, string? value) in configurationOverrides)
            {
                settings[key] = value;
            }
        }

        builder.Configuration.AddInMemoryCollection(settings);
        VulgataWebComposition.ConfigureServices(builder);
        configureServices?.Invoke(builder.Services);
        builder.Services.AddSingleton<NavigationManager, RetainedSourceTestNavigationManager>();
        if (beforeAuthorization is not null)
        {
            ServiceDescriptor? verifier = builder.Services.LastOrDefault(
                descriptor => descriptor.ServiceType == typeof(ISourceRepositoryAuthorizationVerifier));
            if (verifier is not null)
            {
                builder.Services.Remove(verifier);
                builder.Services.AddScoped<ISourceRepositoryAuthorizationVerifier>(services =>
                    new RetainedSourceAuthorizationBeforeDecorator(
                        ResolveVerifier(services, verifier),
                        beforeAuthorization));
            }
        }
        else if (mutateAfterFirstAuthorization is not null)
        {
            ServiceDescriptor? verifier = builder.Services.LastOrDefault(
                descriptor => descriptor.ServiceType == typeof(ISourceRepositoryAuthorizationVerifier));
            if (verifier is not null)
            {
                builder.Services.Remove(verifier);
                builder.Services.AddScoped<ISourceRepositoryAuthorizationVerifier>(services =>
                    new RetainedSourceAuthorizationMutationDecorator(
                        ResolveVerifier(services, verifier),
                        mutateAfterFirstAuthorization,
                        mutateAfterAuthorizationCount));
            }
        }
        else if (authorizationWait is not null)
        {
            ServiceDescriptor? waitVerifier = builder.Services.LastOrDefault(
                descriptor => descriptor.ServiceType == typeof(ISourceRepositoryAuthorizationVerifier));
            if (waitVerifier is not null)
            {
                builder.Services.Remove(waitVerifier);
                builder.Services.AddScoped<ISourceRepositoryAuthorizationVerifier>(services =>
                    new RetainedSourceAuthorizationWaitDecorator(
                        ResolveVerifier(services, waitVerifier),
                        authorizationWait));
            }
        }
        if (timeProvider is not null)
        {
            builder.Services.RemoveAll<TimeProvider>();
            builder.Services.AddSingleton(timeProvider);
        }
        builder.Services.AddSingleton(new RetainedSourceAuthenticatedSubject(userId));
        return builder.Build();
    }

    private static DefaultHttpContext CreateHttpContext(
        IServiceProvider services,
        string userId,
        bool authenticated,
        bool administrator,
        bool systemOwner)
    {
        var identity = new ClaimsIdentity(
            authenticated
                ? new[]
                {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userId),
                }
                    .Concat(administrator
                        ? new[] { new Claim(ClaimTypes.Role, RoleNames.Administrator) }
                        : Array.Empty<Claim>())
                    .Concat(systemOwner
                        ? new[] { new Claim(ClaimTypes.Role, RoleNames.SystemOwner) }
                        : Array.Empty<Claim>())
                    .ToArray()
                : Array.Empty<Claim>(),
            authenticationType: "RetainedSourceTest");
        return new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(identity),
        };
    }

    private static async Task SeedDatabaseAsync(
        string connectionString,
        RetainedSourceFixture source,
        string userId,
        Guid systemId,
        bool standalone,
        bool administrator,
        bool systemOwner,
        CancellationToken cancellationToken)
    {
        DbContextOptionsBuilder<ApplicationDbContext> identityOptions = new();
        identityOptions
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        await using (var identity = new ApplicationDbContext(identityOptions.Options))
        {
            await identity.Database.MigrateAsync(cancellationToken);
            Dictionary<string, string> roleIds =
                RoleNames.SeededRoles.ToDictionary(
                    role => role,
                    _ => Guid.NewGuid().ToString("N"),
                    StringComparer.Ordinal);
            identity.Roles.AddRange(roleIds.Select(pair => new IdentityRole
            {
                Id = pair.Value,
                Name = pair.Key,
                NormalizedName = pair.Key.ToUpperInvariant(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
            }));
            identity.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = userId,
                NormalizedUserName = userId.ToUpperInvariant(),
                Email = $"{userId}@example.invalid",
                NormalizedEmail = $"{userId}@EXAMPLE.INVALID".ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
            });
            if (administrator)
            {
                identity.UserRoles.Add(new IdentityUserRole<string>
                {
                    UserId = userId,
                    RoleId = roleIds[RoleNames.Administrator],
                });
            }

            if (systemOwner)
            {
                identity.UserRoles.Add(new IdentityUserRole<string>
                {
                    UserId = userId,
                    RoleId = roleIds[RoleNames.SystemOwner],
                });
            }
            await identity.SaveChangesAsync(cancellationToken);
        }

        DbContextOptionsBuilder<VulgataDbContext> domainOptions = new();
        domainOptions
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        await using var domain = new VulgataDbContext(domainOptions.Options);
        await domain.Database.MigrateAsync(cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Repository repository;
        if (standalone)
        {
            repository = Repository.CreateStandalone(
                "retained-source-standalone",
                source.Repository.RootPath,
                null,
                null,
                now);
        }
        else
        {
            var system = new Vulgata.Core.Entities.System(
                "retained-source-system",
                null,
                null,
                now);
            SetId(system, systemId);
            repository = system.AddRepository(
                "retained-source-system-repository",
                source.Repository.RootPath,
                null,
                null,
                now);
            domain.Systems.Add(system);
            domain.SystemOwnerAssignments.Add(
                SystemOwnerAssignment.Create(systemId, userId, now));
        }

        SetId(repository, source.RepositoryId);
        domain.Repositories.Add(repository);
        await domain.SaveChangesAsync(cancellationToken);
    }

    private static void SetId<T>(T entity, Guid id)
    {
        typeof(T).GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
            .SetValue(entity, id);
    }

    private static ISourceRepositoryAuthorizationVerifier ResolveVerifier(
        IServiceProvider services,
        ServiceDescriptor descriptor)
    {
        object implementation = descriptor.ImplementationInstance ??
            descriptor.ImplementationFactory?.Invoke(services) ??
            (descriptor.ImplementationType is not null
                ? ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType)
                : throw new InvalidOperationException(
                    "The source authorization verifier registration has no implementation."));
        return (ISourceRepositoryAuthorizationVerifier)implementation;
    }

    internal static async Task RebindOriginAsync(string connectionString, Guid repositoryId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE \"vulgata\".\"Repositories\" SET \"GitUrl\" = @origin " +
            "WHERE \"Id\" = @repositoryId;",
            connection);
        command.Parameters.AddWithValue("origin", "https://example.invalid/rebound.git");
        command.Parameters.AddWithValue("repositoryId", repositoryId);
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task SetOriginAsync(
        string connectionString,
        Guid repositoryId,
        string origin)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE \"vulgata\".\"Repositories\" SET \"GitUrl\" = @origin " +
            "WHERE \"Id\" = @repositoryId;",
            connection);
        command.Parameters.AddWithValue("origin", origin);
        command.Parameters.AddWithValue("repositoryId", repositoryId);
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task RebindSystemAsync(
        string connectionString,
        Guid repositoryId,
        Guid systemId)
    {
        DbContextOptionsBuilder<VulgataDbContext> options = new();
        options.UseNpgsql(connectionString);
        await using (var context = new VulgataDbContext(options.Options))
        {
            var system = new Vulgata.Core.Entities.System(
                "retained-source-rebound-system",
                null,
                null,
                DateTimeOffset.UtcNow);
            SetId(system, systemId);
            context.Systems.Add(system);
            await context.SaveChangesAsync();
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE \"vulgata\".\"Repositories\" SET \"SystemId\" = @systemId " +
            "WHERE \"Id\" = @repositoryId;",
            connection);
        command.Parameters.AddWithValue("systemId", systemId);
        command.Parameters.AddWithValue("repositoryId", repositoryId);
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task RevokeAuthorityAsync(
        string connectionString,
        Guid systemId,
        string userId)
    {
        await using (var domain = new NpgsqlConnection(connectionString))
        {
            await domain.OpenAsync();
            await using var command = new NpgsqlCommand(
                "DELETE FROM \"vulgata\".\"SystemOwnerAssignments\" " +
                "WHERE \"SystemId\" = @systemId AND \"UserId\" = @userId;",
                domain);
            command.Parameters.AddWithValue("systemId", systemId);
            command.Parameters.AddWithValue("userId", userId);
            await command.ExecuteNonQueryAsync();
        }

        await using var identity = new NpgsqlConnection(connectionString);
        await identity.OpenAsync();
        await using var role = new NpgsqlCommand(
            "DELETE FROM \"identity\".\"AspNetUserRoles\" WHERE \"UserId\" = @userId;",
            identity);
        role.Parameters.AddWithValue("userId", userId);
        await role.ExecuteNonQueryAsync();
    }

    internal static async Task RevokeSystemAssignmentAsync(
        string connectionString,
        Guid systemId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "DELETE FROM \"vulgata\".\"SystemOwnerAssignments\" WHERE \"SystemId\" = @systemId;",
            connection);
        command.Parameters.AddWithValue("systemId", systemId);
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task DeleteAuxiliaryTestRootsAsync(string fixtureRoot)
    {
        RetainedSourceOwnedPaths.EnsureOwnedPath(fixtureRoot);
        string[] auxiliaryRootNames = [".test-codegraph", ".test-jdtls", ".test-keyring"];
        foreach (string name in auxiliaryRootNames)
        {
            string path = RetainedSourceOwnedPaths.ResolveChild(fixtureRoot, name);
            await RetainedSourceOwnedPaths.DeleteAsync(path);
        }
    }
}

internal sealed class RetainedSourceAttachedApplication(
    WebApplication application,
    IServiceScope scope) : IAsyncDisposable
{
    internal WebApplication Application { get; } = application;

    internal IServiceScope Scope { get; } = scope;

    public async ValueTask DisposeAsync()
    {
        Scope.Dispose();
        await Application.DisposeAsync();
    }
}

internal sealed record RetainedSourceAuthenticatedSubject(string UserId);

internal sealed class RetainedSourceAuthorizationBeforeDecorator(
    ISourceRepositoryAuthorizationVerifier inner,
    Func<SourceAuthorizationRequest, CancellationToken, Task> beforeAuthorization)
    : ISourceRepositoryAuthorizationVerifier
{
    public async Task<AuthorizedSourceRepository?> AuthorizeAsync(
        SourceAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        await beforeAuthorization(request, cancellationToken);
        return await inner.AuthorizeAsync(request, cancellationToken);
    }
}

internal sealed class RetainedSourceAuthorizationMutationDecorator(
    ISourceRepositoryAuthorizationVerifier inner,
    Func<Task> mutation,
    int mutationAuthorizationCount) : ISourceRepositoryAuthorizationVerifier
{
    private int _mutationStarted;
    private int _successfulAuthorizations;

    internal int MutationCount => Volatile.Read(ref _mutationStarted);

    public async Task<AuthorizedSourceRepository?> AuthorizeAsync(
        SourceAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        AuthorizedSourceRepository? result = await inner.AuthorizeAsync(request, cancellationToken);
        if (result is not null &&
            Interlocked.Increment(ref _successfulAuthorizations) == mutationAuthorizationCount &&
            Interlocked.Exchange(ref _mutationStarted, 1) == 0)
        {
            await mutation();
        }

        return result;
    }
}

internal sealed class RetainedSourceAuthorizationWaitDecorator(
    ISourceRepositoryAuthorizationVerifier inner,
    Func<SourceAuthorizationRequest, CancellationToken, Task> wait)
    : ISourceRepositoryAuthorizationVerifier
{
    public async Task<AuthorizedSourceRepository?> AuthorizeAsync(
        SourceAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        AuthorizedSourceRepository? result = await inner.AuthorizeAsync(request, cancellationToken);
        if (result is not null)
        {
            await wait(request, cancellationToken);
        }

        return result;
    }
}

internal sealed class RetainedSourceTestNavigationManager : NavigationManager
{
    public RetainedSourceTestNavigationManager()
    {
        Initialize("https://retained-source.test/", "https://retained-source.test/");
    }

    protected override void NavigateToCore(string uri, NavigationOptions options)
    {
    }
}

internal sealed class RetainedSourceCaptureTestHost : IAsyncLifetime
{
    private string? _previousTargetConnection;
    private RetainedSourceCaptureApplicationFixture? _application;

    internal RetainedSourceFixture Source =>
        _application?.Source ?? throw new InvalidOperationException("The capture host is not initialized.");

    internal RetainedSourcePostgreSqlDatabase Database =>
        _application?.Database ?? throw new InvalidOperationException("The capture host is not initialized.");

    internal WebApplication Application =>
        _application?.Application ?? throw new InvalidOperationException("The capture host is not initialized.");

    internal IServiceScope Scope =>
        _application?.Scope ?? throw new InvalidOperationException("The capture host is not initialized.");

    public async Task InitializeAsync()
    {
        RetainedSourceFixture source = await RetainedSourceFixture.CreateAsync();
        try
        {
            _application = await RetainedSourceCaptureApplicationFixture.CreateAsync(source);
            _previousTargetConnection = Environment.GetEnvironmentVariable(
                "VULGATA_STORY131_POSTGRES_TARGET");
            Environment.SetEnvironmentVariable(
                "VULGATA_STORY131_POSTGRES_TARGET",
                _application.Database.ConnectionString);
        }
        catch
        {
            await source.DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable(
            "VULGATA_STORY131_POSTGRES_TARGET",
            _previousTargetConnection);
        if (_application is not null)
        {
            await _application.DisposeAsync();
        }
    }
}

internal sealed class AdvancingRetainedSourceTimeProvider(TimeSpan advancePerRead)
    : TimeProvider
{
    private readonly long _advanceTimestamp =
        Math.Max(1, (long)(advancePerRead.TotalSeconds * Stopwatch.Frequency));
    private long _timestamp;

    public override DateTimeOffset GetUtcNow() =>
        DateTimeOffset.UnixEpoch.AddTicks(
            checked(GetTimestamp() * TimeSpan.TicksPerSecond / Stopwatch.Frequency));

    public override long GetTimestamp() =>
        Interlocked.Add(ref _timestamp, _advanceTimestamp);
}

internal sealed class ControlledRetainedSourceTimeProvider : TimeProvider
{
    private long _timestamp = Stopwatch.GetTimestamp();

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;

    public override long GetTimestamp() => Volatile.Read(ref _timestamp);

    internal void Advance(TimeSpan duration)
    {
        long ticks = checked((long)(duration.TotalSeconds * Stopwatch.Frequency));
        Interlocked.Add(ref _timestamp, ticks);
    }
}
