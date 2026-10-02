using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Text.RegularExpressions;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Shared;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;
using Xunit.Abstractions;

namespace Vulgata.Tests.October;

public sealed class OctoberPostgreSqlIsolationTests(ITestOutputHelper output)
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PostgreSqlTargets_KeepSchemasAndBatchesIsolatedAcrossAuthorities()
    {
        await using OctoberHostSelectionTests.GeneratorPrepareFixture targetA =
            await OctoberHostSelectionTests.GeneratorPrepareFixture.CreateAsync(output);
        await using OctoberHostSelectionTests.GeneratorPrepareFixture targetB =
            await OctoberHostSelectionTests.GeneratorPrepareFixture.CreateAsync(output);
        Assert.NotEqual(targetA.ConnectionString, targetB.ConnectionString);
        Assert.NotEqual(targetA.BatchId, targetB.BatchId);

        await AssertOrdinaryContextsExcludeOctoberAsync(targetA);
        await AssertOrdinaryContextsExcludeOctoberAsync(targetB);
        await AssertForeignAuthorityDecisionAsync(
            targetA, OctoberAuthorityDecisionStatus.Unavailable);
        await targetA.AssertOctoberSchemaAbsentAsync();
        await PrepareOwnedTargetAsync(targetA);
        await PrepareOwnedTargetAsync(targetB);

        OctoberBoundSetupSnapshot snapshotA = await targetA.ReadBoundSnapshotAsync();
        OctoberBoundSetupSnapshot snapshotB = await targetB.ReadBoundSnapshotAsync();
        AssertBoundSnapshotBelongsToTarget(snapshotA, targetA);
        AssertBoundSnapshotBelongsToTarget(snapshotB, targetB);
        Assert.NotEqual(snapshotA.Request.SelectionDigest, snapshotB.Request.SelectionDigest);

        var octoberStateA = await targetA.CaptureOctoberStateAsync();
        var octoberStateB = await targetB.CaptureOctoberStateAsync();
        Assert.Equal(octoberStateA.AppliedMigrations, octoberStateB.AppliedMigrations);
        Assert.Equal(
            NormalizePostgreSqlGeneratedNames(octoberStateA.SchemaDefinition),
            NormalizePostgreSqlGeneratedNames(octoberStateB.SchemaDefinition));
        Assert.Equal(octoberStateA.RowCounts, octoberStateB.RowCounts);

        await AssertForeignAuthorityDecisionAsync(
            targetA, OctoberAuthorityDecisionStatus.Denied);
        await AssertOctoberStateUnchangedAsync(targetA, octoberStateA);
        await AssertTargetBDoesNotSelectTargetABatchAsync(targetA, targetB, snapshotA);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PostgreSqlDeniedAuthorityAndInvalidProviderLeaveOctoberUnmigrated()
    {
        await using OctoberHostSelectionTests.GeneratorPrepareFixture fixture =
            await OctoberHostSelectionTests.GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = System.Text.Json.JsonSerializer.Serialize(request);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOwnedProviderDocumentBindsAsync();
        await AssertForeignAuthorityDecisionAsync(
            fixture, OctoberAuthorityDecisionStatus.Unavailable);
        await fixture.AssertOctoberSchemaAbsentAsync();

        fixture.WriteMalformedProviderDocument();
        using Process process = fixture.StartGenerator("prepare", requestJson);
        int processId = process.Id;
        (int exitCode, string standardOutput, string standardError) =
            await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"invalid-provider-generator-process-id={processId}");
        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, exitCode);
        Assert.Contains("InvalidConfiguration", childOutput, StringComparison.Ordinal);
        await fixture.AssertOctoberSchemaAbsentAsync();
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PostgreSqlSelectedWebStartup_UsesOwnedDatabaseFactoriesWithoutMigrationOrSeeding()
    {
        await using OctoberHostSelectionTests.GeneratorPrepareFixture targetA =
            await OctoberHostSelectionTests.GeneratorPrepareFixture.CreateAsync(output);
        await using OctoberHostSelectionTests.GeneratorPrepareFixture targetB =
            await OctoberHostSelectionTests.GeneratorPrepareFixture.CreateAsync(output);
        await PrepareOwnedTargetAsync(targetA);
        await PrepareOwnedTargetAsync(targetB);

        SelectedDataState selectedStateA = await CaptureSelectedDataStateAsync(targetA);
        SelectedDataState selectedStateB = await CaptureSelectedDataStateAsync(targetB);
        var octoberStateA = await targetA.CaptureOctoberStateAsync();
        var octoberStateB = await targetB.CaptureOctoberStateAsync();
        AssertEquivalentSelectedDataShape(selectedStateA, selectedStateB);

        await AssertSelectedWebCompositionAsync(targetA, output);
        await AssertSelectedWebCompositionAsync(targetB, output);
        await AssertOctoberStateUnchangedAsync(targetA, octoberStateA);
        await AssertOctoberStateUnchangedAsync(targetB, octoberStateB);
        AssertEquivalentSelectedDataShape(selectedStateA, await CaptureSelectedDataStateAsync(targetA));
        AssertEquivalentSelectedDataShape(selectedStateB, await CaptureSelectedDataStateAsync(targetB));
    }

    private static async Task AssertOrdinaryContextsExcludeOctoberAsync(
        OctoberHostSelectionTests.GeneratorPrepareFixture fixture)
    {
        await fixture.AssertOctoberSchemaAbsentAsync();
        TestResourceScope.ValidateOwnedTarget(fixture.ConnectionString);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ApplicationDbContext identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AssertTargetDatabase(identity.Database.GetDbConnection().ConnectionString, fixture);
        Assert.DoesNotContain(identity.Database.GetMigrations(), migration =>
            migration.Contains("October", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(identity.Model.GetEntityTypes(), entity =>
            string.Equals(entity.GetSchema(), "october", StringComparison.Ordinal) ||
            entity.ClrType.Namespace?.Contains(".October", StringComparison.Ordinal) == true);

        IDbContextFactory<VulgataDbContext> factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext management = await factory.CreateDbContextAsync();
        AssertTargetDatabase(management.Database.GetDbConnection().ConnectionString, fixture);
        Assert.DoesNotContain(management.Database.GetMigrations(), migration =>
            migration.Contains("October", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(management.Model.GetEntityTypes(), entity =>
            string.Equals(entity.GetSchema(), "october", StringComparison.Ordinal) ||
            entity.ClrType.Namespace?.Contains(".October", StringComparison.Ordinal) == true);
        Assert.Null(management.Model.FindEntityType(typeof(OctoberBatchRecord)));

        await using OctoberDbContext october = fixture.CreateOctoberContext();
        AssertTargetDatabase(october.Database.GetDbConnection().ConnectionString, fixture);
    }

    private async Task PrepareOwnedTargetAsync(
        OctoberHostSelectionTests.GeneratorPrepareFixture fixture)
    {
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOwnedProviderDocumentBindsAsync();
        await fixture.AssertOctoberSchemaAbsentAsync();

        using Process process = fixture.StartGenerator(System.Text.Json.JsonSerializer.Serialize(request));
        int processId = process.Id;
        (int exitCode, string standardOutput, string standardError) =
            await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"postgresql-generator-process-id={processId}");
        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, exitCode);
        Assert.Contains("Prepared", childOutput, StringComparison.Ordinal);
        Assert.Contains(fixture.BatchId.ToString("D"), childOutput, StringComparison.OrdinalIgnoreCase);
        await fixture.AssertOctoberSchemaPresentAsync();
    }

    private static async Task AssertSelectedWebCompositionAsync(
        OctoberHostSelectionTests.GeneratorPrepareFixture fixture,
        ITestOutputHelper output)
    {
        string logPath = Path.Combine(fixture.OwnedRoot, "selected-web.log");
        WebApplicationFactory<Program> parentFactory = new();
        WebApplicationFactory<Program> factory = parentFactory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString,
                        ["October:Web:Mode"] = "selected-data",
                        ["October:Web:TargetId"] = fixture.TargetId,
                        ["October:Web:BatchId"] = fixture.BatchId.ToString("D"),
                        ["October:Authority:Id"] = fixture.AuthorityId,
                        ["October:Targets:0:TargetId"] = fixture.TargetId,
                        ["October:Targets:0:CanonicalDatabaseIdentity"] =
                            fixture.CanonicalDatabaseIdentity,
                        ["October:Operator:CredentialSource"] = "protected-file",
                        ["October:Operator:CredentialFile"] = fixture.OperatorCredentialPath,
                        ["October:Providers:ConfigurationFile"] = fixture.ProviderDocumentPath,
                        ["DataProtection:KeyRingPath"] = fixture.KeyRingPath,
                        ["DataProtection:ApplicationName"] = fixture.ProtectionApplicationName,
                        ["Logging:LocalFile:Path"] = logPath,
                        ["SourceStorage:RootPath"] = fixture.OwnedSourceRoot,
                        ["CodeGraph:DataRootPath"] = Path.Combine(fixture.OwnedRoot, "codegraph"),
                        ["AnalysisRuntime:JdtLsWorkspaceRoot"] =
                            Path.Combine(fixture.OwnedRoot, "jdt-workspace")
                    }));
            });

        try
        {
            await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            {
                IServiceProvider services = scope.ServiceProvider;
                OctoberWebStartupSelection selection = services.GetRequiredService<OctoberWebStartupSelection>();
                Assert.Equal(OctoberWebMode.SelectedData, selection.Mode);
                Assert.Equal(fixture.TargetId, selection.TargetId);
                Assert.Equal(fixture.BatchId, selection.BatchId);

                IOctoberBoundSetupData bound = services.GetRequiredService<IOctoberBoundSetupData>();
                Assert.Equal(fixture.TargetId, bound.TargetId);
                Assert.Equal(fixture.BatchId, bound.BatchId);

                OctoberAuthorityConfiguration authority =
                    services.GetRequiredService<OctoberAuthorityConfiguration>();
                Assert.Equal(fixture.ConnectionString, authority.DataConnectionString);
                Assert.Equal(fixture.ConnectionString, authority.IdentityConnectionString);

                ApplicationDbContext identity = services.GetRequiredService<ApplicationDbContext>();
                AssertTargetDatabase(identity.Database.GetDbConnection().ConnectionString, fixture);
                VulgataDbContext scopedManagement = services.GetRequiredService<VulgataDbContext>();
                AssertTargetDatabase(scopedManagement.Database.GetDbConnection().ConnectionString, fixture);
                await using VulgataDbContext factoryManagement = await services
                    .GetRequiredService<IDbContextFactory<VulgataDbContext>>()
                    .CreateDbContextAsync();
                AssertTargetDatabase(factoryManagement.Database.GetDbConnection().ConnectionString, fixture);
                OctoberDbContext october = services.GetRequiredService<OctoberDbContext>();
                AssertTargetDatabase(october.Database.GetDbConnection().ConnectionString, fixture);

                using HttpClient anonymousBrowser = factory.CreateClient();
                using HttpResponseMessage systems = await anonymousBrowser.GetAsync("/api/systems/");
                Assert.Equal(System.Net.HttpStatusCode.Unauthorized, systems.StatusCode);
            }

            output.WriteLine($"selected-web-log-exclusive-open-before-disposal={ProbeExclusiveLogOpen(logPath)}");
        }
        finally
        {
            await factory.DisposeAsync();
            string afterDerivedDisposal = ProbeExclusiveLogOpen(logPath);
            output.WriteLine($"selected-web-log-exclusive-open-after-derived-disposal={afterDerivedDisposal}");

            await parentFactory.DisposeAsync();
            string afterParentDisposal = ProbeExclusiveLogOpen(logPath);
            output.WriteLine($"selected-web-log-exclusive-open-after-parent-disposal={afterParentDisposal}");

            if (afterParentDisposal.StartsWith("blocked-", StringComparison.Ordinal))
            {
                output.WriteLine($"selected-web-log-restart-manager-owners={GetRestartManagerOwnerPids(logPath)}");
            }
        }
    }

    private static string ProbeExclusiveLogOpen(string logPath)
    {
        try
        {
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return "opened";
        }
        catch (IOException exception)
        {
            return $"blocked-io-hresult-0x{exception.HResult:X8}";
        }
        catch (Exception exception)
        {
            return $"probe-error-{exception.GetType().Name}-0x{exception.HResult:X8}";
        }
    }

    private static string GetRestartManagerOwnerPids(string logPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return "unsupported-non-windows";
        }

        const int errorMoreData = 234;
        const int maximumOwners = 32;
        StringBuilder sessionKey = new(33);
        int status = RmStartSession(out uint session, 0, sessionKey);
        if (status != 0)
        {
            return $"start-failed-{status}";
        }

        try
        {
            status = RmRegisterResources(session, 1, [logPath], 0, null, 0, IntPtr.Zero);
            if (status != 0)
            {
                return $"register-failed-{status}";
            }

            uint needed = 0;
            uint count = 0;
            uint rebootReasons = 0;
            status = RmGetList(session, out needed, ref count, null, ref rebootReasons);
            if (status == 0 && needed == 0)
            {
                return "none-reported";
            }

            if (status != errorMoreData || needed > maximumOwners)
            {
                return $"list-failed-{status}-needed-{needed}";
            }

            count = needed;
            RmProcessInfo[] owners = new RmProcessInfo[count];
            status = RmGetList(session, out needed, ref count, owners, ref rebootReasons);
            if (status != 0)
            {
                return $"list-failed-{status}-needed-{needed}";
            }

            return string.Join(',', owners.Take((int)count).Select(owner => owner.Process.ProcessId));
        }
        catch (Exception exception)
        {
            return $"query-failed-{exception.GetType().Name}-0x{exception.HResult:X8}";
        }
        finally
        {
            _ = RmEndSession(session);
        }
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint sessionHandle, int sessionFlags, StringBuilder sessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint sessionHandle,
        uint fileCount,
        [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] fileNames,
        uint applicationCount,
        [In] RmUniqueProcess[]? applications,
        uint serviceCount,
        IntPtr services);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(
        uint sessionHandle,
        out uint processInfoNeeded,
        ref uint processInfoCount,
        [In, Out] RmProcessInfo[]? affectedApplications,
        ref uint rebootReasons);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint sessionHandle);

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public uint ProcessId;
        public FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ApplicationName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ServiceShortName;

        public int ApplicationType;
        public uint ApplicationStatus;
        public uint TerminalServicesSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;
    }

    private static async Task AssertForeignAuthorityDecisionAsync(
        OctoberHostSelectionTests.GeneratorPrepareFixture fixture,
        OctoberAuthorityDecisionStatus expectedStatus)
    {
        string foreignAuthorityId = $"foreign-management-authority-{Guid.NewGuid():N}";
        await using ServiceProvider provider =
            fixture.CreateAuthorityWithDifferentManagementAuthority(foreignAuthorityId);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = scope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult authentication = await authority.AuthenticateAsync(CancellationToken.None);
        Assert.Equal(OctoberAuthenticationStatus.Authenticated, authentication.Status);
        await using IOctoberOperatorSession session =
            Assert.IsAssignableFrom<IOctoberOperatorSession>(authentication.Session);

        OctoberAuthoritySelectionResult result = await authority.AuthorizeSelectionAsync(
            session,
            new OctoberAuthoritySelectionRequest(fixture.TargetId, fixture.BatchId, PrepareRequest: null),
            CancellationToken.None);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Authority);
        Assert.Null(result.Request);
    }

    private static async Task AssertTargetBDoesNotSelectTargetABatchAsync(
        OctoberHostSelectionTests.GeneratorPrepareFixture targetA,
        OctoberHostSelectionTests.GeneratorPrepareFixture targetB,
        OctoberBoundSetupSnapshot snapshotA)
    {
        TestResourceScope.ValidateOwnedTarget(targetB.ConnectionString);
        await using OctoberDbContext octoberB = targetB.CreateOctoberContext();
        OctoberBatchSelector selectorB = new(
            octoberB, targetB.TargetId, targetB.CanonicalDatabaseIdentity);
        OctoberSelectionResult result = await selectorB.BindAsync(
            new OctoberStartupSelection(targetA.TargetId, targetA.BatchId, snapshotA.Authority),
            CancellationToken.None);

        Assert.Equal(OctoberSelectionStatus.Denied, result.Status);
        Assert.Null(result.BoundSetupData);
    }

    private static void AssertBoundSnapshotBelongsToTarget(
        OctoberBoundSetupSnapshot snapshot,
        OctoberHostSelectionTests.GeneratorPrepareFixture fixture)
    {
        Assert.Equal(fixture.TargetId, snapshot.Request.TargetId);
        Assert.Equal(fixture.BatchId, snapshot.Request.BatchId);
        Assert.Equal(fixture.TargetId, snapshot.Authority.TargetId);
        Assert.Equal(fixture.BatchId, snapshot.Authority.BatchId);
        Assert.Equal(fixture.SubjectId, snapshot.Authority.SubjectId);
        Assert.Equal(fixture.CanonicalDatabaseIdentity, snapshot.Authority.CanonicalDatabaseIdentity);
        Assert.Equal(fixture.AuthorityId, snapshot.Authority.SelectedContext!.ManagementAuthorityId);
        Assert.Equal(TimeSpan.Zero, snapshot.SelectedAtUtc.Offset);
    }

    private static string[] NormalizePostgreSqlGeneratedNames(IEnumerable<string> schemaDefinition) =>
        schemaDefinition.Select(line => Regex.Replace(
            line,
            @"\b\d+_\d+_\d+_[A-Za-z0-9_]+\b",
            "<postgres-generated-name>",
            RegexOptions.CultureInvariant)).ToArray();

    private static async Task AssertOctoberStateUnchangedAsync(
        OctoberHostSelectionTests.GeneratorPrepareFixture fixture,
        (string[] AppliedMigrations, string[] SchemaDefinition, int[] RowCounts) expected)
    {
        var actual = await fixture.CaptureOctoberStateAsync();
        Assert.Equal(expected.AppliedMigrations, actual.AppliedMigrations);
        Assert.Equal(expected.SchemaDefinition, actual.SchemaDefinition);
        Assert.Equal(expected.RowCounts, actual.RowCounts);
    }

    private static async Task<SelectedDataState> CaptureSelectedDataStateAsync(
        OctoberHostSelectionTests.GeneratorPrepareFixture fixture)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ApplicationDbContext identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string[] identityMigrations = (await identity.Database.GetAppliedMigrationsAsync()).ToArray();
        int roleCount = await identity.Roles.CountAsync();

        IDbContextFactory<VulgataDbContext> managementFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext management = await managementFactory.CreateDbContextAsync();
        string[] managementMigrations =
            (await management.Database.GetAppliedMigrationsAsync()).ToArray();
        int systemCount = await management.Systems.CountAsync();
        int repositoryCount = await management.Repositories.CountAsync();
        int globalContextCount = await management.GlobalContexts.CountAsync();

        await using OctoberDbContext october = fixture.CreateOctoberContext();
        string[] octoberMigrations = (await october.Database.GetAppliedMigrationsAsync()).ToArray();
        int batchCount = await october.Batches.CountAsync();
        int selectionCount = await october.RepositorySelections.CountAsync();
        int contextCount = await october.ContextReferences.CountAsync();
        int entryCount = await october.AdvisoryEntries.CountAsync();
        int differenceCount = await october.ContextDifferences.CountAsync();
        return new SelectedDataState(identityMigrations, managementMigrations, octoberMigrations,
            roleCount, systemCount, repositoryCount, globalContextCount, batchCount,
            selectionCount, contextCount, entryCount, differenceCount);
    }

    private static void AssertEquivalentSelectedDataShape(
        SelectedDataState expected,
        SelectedDataState actual)
    {
        Assert.Equal(expected.IdentityMigrations, actual.IdentityMigrations);
        Assert.Equal(expected.ManagementMigrations, actual.ManagementMigrations);
        Assert.Equal(expected.OctoberMigrations, actual.OctoberMigrations);
        Assert.Equal(expected.RoleCount, actual.RoleCount);
        Assert.Equal(expected.SystemCount, actual.SystemCount);
        Assert.Equal(expected.RepositoryCount, actual.RepositoryCount);
        Assert.Equal(expected.GlobalContextCount, actual.GlobalContextCount);
        Assert.Equal(expected.BatchCount, actual.BatchCount);
        Assert.Equal(expected.SelectionCount, actual.SelectionCount);
        Assert.Equal(expected.ContextCount, actual.ContextCount);
        Assert.Equal(expected.EntryCount, actual.EntryCount);
        Assert.Equal(expected.DifferenceCount, actual.DifferenceCount);
    }

    private static void AssertTargetDatabase(
        string connectionString,
        OctoberHostSelectionTests.GeneratorPrepareFixture fixture)
    {
        NpgsqlConnectionStringBuilder actual = new(connectionString);
        NpgsqlConnectionStringBuilder expected = new(fixture.ConnectionString);
        Assert.Equal(expected.Host, actual.Host);
        Assert.Equal(expected.Port, actual.Port);
        Assert.Equal(expected.Database, actual.Database);
        Assert.Equal(expected.Username, actual.Username);
    }

    private sealed record SelectedDataState(
        string[] IdentityMigrations,
        string[] ManagementMigrations,
        string[] OctoberMigrations,
        int RoleCount,
        int SystemCount,
        int RepositoryCount,
        int GlobalContextCount,
        int BatchCount,
        int SelectionCount,
        int ContextCount,
        int EntryCount,
        int DifferenceCount);
}
