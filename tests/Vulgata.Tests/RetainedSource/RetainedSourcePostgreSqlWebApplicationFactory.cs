using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Source;
using Vulgata.Web.Data;

namespace Vulgata.Tests.RetainedSource;

/// <summary>
/// Test-only production WebApplicationFactory for retained-source HTTP coverage.
/// It keeps Program's routing, middleware, Identity cookie authentication, and
/// startup path while redirecting the two EF contexts to one owned PostgreSQL DB.
/// </summary>
internal sealed class RetainedSourcePostgreSqlWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _sourceStorageRoot;
    private readonly string _ownerRoot;
    private readonly string _keyRingRoot;
    private readonly string _codeGraphRoot;
    private readonly string _jdtLsRoot;

    internal RetainedSourcePostgreSqlWebApplicationFactory(
        string connectionString,
        string sourceStorageRoot)
    {
        _connectionString = connectionString;
        _sourceStorageRoot = sourceStorageRoot;
        _ownerRoot = Path.Combine(
            Directory.GetParent(sourceStorageRoot)?.FullName ??
            throw new InvalidOperationException("The source storage parent is unavailable."),
            "http-e2e-" + Guid.NewGuid().ToString("N"));
        _keyRingRoot = Path.Combine(_ownerRoot, "keyring");
        _codeGraphRoot = Path.Combine(_ownerRoot, "codegraph");
        _jdtLsRoot = Path.Combine(_ownerRoot, "jdtls");
        Directory.CreateDirectory(_keyRingRoot);
        Directory.CreateDirectory(_codeGraphRoot);
        Directory.CreateDirectory(_jdtLsRoot);
    }

    internal string OwnerRoot => _ownerRoot;

    internal string KeyRingRoot => _keyRingRoot;

    internal string CodeGraphRoot => _codeGraphRoot;

    internal string JdtLsRoot => _jdtLsRoot;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["SourceStorage:RootPath"] = _sourceStorageRoot,
                ["DataProtection:KeyRingPath"] = _keyRingRoot,
                ["DataProtection:ApplicationName"] = "Vulgata.Tests.RetainedSource.HttpE2E",
                ["CodeGraph:DataRootPath"] = _codeGraphRoot,
                ["AnalysisRuntime:JdtLsWorkspaceRoot"] = _jdtLsRoot,
            }));

        builder.ConfigureServices(services =>
        {
            IConfiguration policyConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SourceStorage:RootPath"] = _sourceStorageRoot,
                    ["SourceStorage:MaximumEntries"] = "10000",
                    ["SourceStorage:MaximumBlobBytes"] = "8388608",
                    ["SourceStorage:MaximumAggregateBlobBytes"] = "67108864",
                    ["SourceStorage:CaptureDeadlineSeconds"] = "600",
                    ["SourceStorage:OperationStaleAfterMinutes"] = "1440",
                    ["SourceStorage:VerificationBufferBytes"] = "65536",
                })
                .Build();
            RetainedSourceCapturePolicy capturePolicy = RetainedSourceCapturePolicy.Create(
                policyConfiguration,
                Directory.GetCurrentDirectory(),
                _codeGraphRoot,
                _jdtLsRoot);
            services.RemoveAll<RetainedSourceCapturePolicy>();
            services.AddSingleton(capturePolicy);

            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(_keyRingRoot))
                .SetApplicationName("Vulgata.Tests.RetainedSource.HttpE2E");

            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<VulgataDbContext>>();
            services.RemoveAll<IDbContextFactory<VulgataDbContext>>();
            services.RemoveAll<IConfigureOptions<DbContextOptions<ApplicationDbContext>>>();
            services.RemoveAll<IConfigureOptions<DbContextOptions<VulgataDbContext>>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<VulgataDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<VulgataDbContext>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options
                    .UseNpgsql(_connectionString, npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                        npgsql.MigrationsHistoryTable("__IdentityMigrationsHistory", "identity");
                    })
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));
            services.AddDbContext<VulgataDbContext>(options =>
                    options
                        .UseNpgsql(_connectionString, npgsql => { npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"); })
                        .ConfigureWarnings(warnings =>
                            warnings.Ignore(RelationalEventId.PendingModelChangesWarning)),
                contextLifetime: ServiceLifetime.Scoped,
                optionsLifetime: ServiceLifetime.Singleton);
            services.AddDbContextFactory<VulgataDbContext>(options =>
                options
                    .UseNpgsql(_connectionString, npgsql => { npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"); })
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_ownerRoot))
        {
            Directory.Delete(_ownerRoot, recursive: true);
        }
    }
}