using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.PreparedScanning;
using Vulgata.Web.Data;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T052 RED slice.
///
/// The fixture enters through an approved Prepared Scan Manifest pair, claims
/// one durable job, and invokes the future packet-source port through DI and
/// reflection. Reflection is used only so this behavioral suite compiles
/// before T053 adds the Core contract and Infrastructure implementation.
/// T055 executor/dispatcher behavior is deliberately outside this file.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class Pass1ClaimedPacketExecutionTests
{
    [Fact]
    public async Task CurrentClaim_HydratesOnlyTheClaimedUnitWithExactSourceAndServerBindingsAsync()
    {
        RequireClaimedPacketSourceContract();
        await using ClaimedFixture fixture = await CreateClaimedFixtureAsync();
        RecordingSourceReader reader = new(fixture.SourceText);
        using WebApplicationFactory<Program> host = CreateHost(fixture.Prepared.ConnectionString, reader);

        object? result = await InvokeHydrateAsync(host, fixture.PairId, fixture.Claim);
        PreparedPass1WorkerPacket packet = Assert.IsType<PreparedPass1WorkerPacket>(result);

        Assert.Equal(fixture.Claim.JobId, packet.JobId);
        Assert.Equal(fixture.UnitId, packet.FileCodeUnitId);
        Assert.Equal(fixture.RepositoryId, packet.RepositoryId);
        Assert.Equal(fixture.ScanRunId, packet.ScanRunId);
        Assert.Equal(fixture.ProfiledRevision, packet.ProfiledRevision);
        Assert.Equal(fixture.RelativePath, packet.RepositoryRelativePath);
        Assert.Equal(fixture.SourceText, Encoding.UTF8.GetString(packet.SourceUtf8.Span));
        Assert.Equal(fixture.SourceHash, packet.SourceHash);
        Assert.Equal(fixture.FileHash, packet.FileHash);
        Assert.Equal(fixture.ByteStart, packet.ByteStart);
        Assert.Equal(fixture.ByteEnd, packet.ByteEnd);
        Assert.Equal(fixture.LineStart, packet.LineStart);
        Assert.Equal(fixture.LineEnd, packet.LineEnd);
        Assert.Equal(1, reader.ReadCount);
    }

    [Theory]
    [InlineData("replaced-owner-token")]
    [InlineData("job-version")]
    [InlineData("expired-lease")]
    [InlineData("pair-version")]
    [InlineData("pair-not-running")]
    public async Task StaleClaimOrPairState_ReturnsNoPacketWithoutSourceDisclosureOrMutationAsync(
        string drift)
    {
        RequireClaimedPacketSourceContract();
        await using ClaimedFixture fixture = await CreateClaimedFixtureAsync();
        RecordingSourceReader reader = new(fixture.SourceText);
        int jobsBefore = await fixture.Prepared.Context.ScanJobExecutions.CountAsync();
        int documentsBefore = await fixture.Prepared.Context.DocumentVersions.CountAsync();
        await ApplyDriftAsync(fixture, drift);
        using WebApplicationFactory<Program> host = CreateHost(fixture.Prepared.ConnectionString, reader);

        object? result = await InvokeHydrateAsync(host, fixture.PairId, fixture.Claim);

        Assert.Null(result);
        Assert.Equal(0, reader.ReadCount);
        Assert.Equal(jobsBefore, await fixture.Prepared.Context.ScanJobExecutions.CountAsync());
        Assert.Equal(documentsBefore, await fixture.Prepared.Context.DocumentVersions.CountAsync());
        ScanJobExecution job = await fixture.Prepared.Context.ScanJobExecutions
            .AsNoTracking().SingleAsync(value => value.Id == fixture.Claim.JobId);
        Assert.Equal(ScanJobExecutionState.Running, job.State);
    }

    [Theory]
    [InlineData("authorization")]
    [InlineData("repository")]
    [InlineData("profiled-revision")]
    [InlineData("path")]
    [InlineData("source-hash")]
    [InlineData("range")]
    public async Task BindingDrift_ReturnsNoPacketWithoutSourceDisclosureOrCommitAsync(string drift)
    {
        RequireClaimedPacketSourceContract();
        await using ClaimedFixture fixture = await CreateClaimedFixtureAsync();
        RecordingSourceReader reader = new(fixture.SourceText);
        int jobsBefore = await fixture.Prepared.Context.ScanJobExecutions.CountAsync();
        int documentsBefore = await fixture.Prepared.Context.DocumentVersions.CountAsync();
        await ApplyDriftAsync(fixture, drift);
        using WebApplicationFactory<Program> host = CreateHost(fixture.Prepared.ConnectionString, reader);

        object? result = await InvokeHydrateAsync(host, fixture.PairId, fixture.Claim);

        Assert.Null(result);
        Assert.Equal(0, reader.ReadCount);
        Assert.Equal(jobsBefore, await fixture.Prepared.Context.ScanJobExecutions.CountAsync());
        Assert.Equal(documentsBefore, await fixture.Prepared.Context.DocumentVersions.CountAsync());
    }

    [Fact]
    public async Task Cancellation_ReturnsNoPacketAndDoesNotReadOrCommitAsync()
    {
        RequireClaimedPacketSourceContract();
        await using ClaimedFixture fixture = await CreateClaimedFixtureAsync();
        RecordingSourceReader reader = new(fixture.SourceText);
        int jobsBefore = await fixture.Prepared.Context.ScanJobExecutions.CountAsync();
        int documentsBefore = await fixture.Prepared.Context.DocumentVersions.CountAsync();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        using WebApplicationFactory<Program> host = CreateHost(fixture.Prepared.ConnectionString, reader);

        object? result = await InvokeHydrateAsync(host, fixture.PairId, fixture.Claim, cancellation.Token);

        Assert.Null(result);
        Assert.Equal(0, reader.ReadCount);
        Assert.Equal(jobsBefore, await fixture.Prepared.Context.ScanJobExecutions.CountAsync());
        Assert.Equal(documentsBefore, await fixture.Prepared.Context.DocumentVersions.CountAsync());
    }

    [Fact]
    public void WorkerPacket_DoesNotExposeLeaseOrVersionAuthorization()
    {
        // Characterization only: no RED is expected for this already-correct
        // provider-visible boundary. T053 must preserve it while moving packet
        // construction behind current claim revalidation.
        PropertyInfo[] properties = typeof(PreparedPass1WorkerPacket)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(properties, property => property.Name is
            "LeaseToken" or "LeaseExpiresAt" or "LeaseExpiry" or
            "StateVersion" or "JobStateVersion" or "PairStateVersion");
    }

    private static async Task<object?> InvokeHydrateAsync(
        WebApplicationFactory<Program> host,
        Guid pairExecutionId,
        PreparedPass1ClaimResult claim,
        CancellationToken cancellationToken = default)
    {
        Type contract = RequireClaimedPacketSourceContract();
        Type implementation = RequireClaimedPacketSourceImplementation();
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        object source;
        try
        {
            // T053 owns the implementation, while final composition is owned
            // by T055/T063. Construct it directly from this scoped provider so
            // this slice does not require a Program.cs registration.
            source = ActivatorUtilities.CreateInstance(scope.ServiceProvider, implementation);
        }
        catch (Exception exception)
        {
            throw new Xunit.Sdk.XunitException(
                "T052 RED: PreparedPass1ClaimedPacketSource cannot be constructed from the scoped production dependencies.",
                exception);
        }
        MethodInfo contractMethod = FindHydrateMethod(contract)
            ?? throw new Xunit.Sdk.XunitException(
                "T052 RED: IPreparedPass1ClaimedPacketSource must expose one async hydration method.");
        InterfaceMapping mapping = source.GetType().GetInterfaceMap(contract);
        int index = Array.IndexOf(mapping.InterfaceMethods, contractMethod);
        Assert.True(index >= 0, "T052 RED: the resolved production source does not implement the approved hydration port.");
        MethodInfo method = mapping.TargetMethods[index];
        object?[] arguments = method.GetParameters()
            .Select(parameter => ResolveArgument(parameter, pairExecutionId, claim, cancellationToken))
            .ToArray();
        object invocation = method.Invoke(source, arguments)
            ?? throw new Xunit.Sdk.XunitException("T052 RED: packet hydration returned no asynchronous result.");
        if (invocation is not Task task)
        {
            throw new Xunit.Sdk.XunitException("T052 RED: packet hydration must return Task<T>.");
        }

        await task.ConfigureAwait(false);
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }

    private static object? ResolveArgument(
        ParameterInfo parameter,
        Guid pairExecutionId,
        PreparedPass1ClaimResult claim,
        CancellationToken cancellationToken)
    {
        if (parameter.ParameterType == typeof(Guid))
        {
            return parameter.Name?.Contains("job", StringComparison.OrdinalIgnoreCase) == true
                ? claim.JobId
                : pairExecutionId;
        }

        if (parameter.ParameterType == typeof(PreparedPass1ClaimResult))
        {
            return claim;
        }

        if (parameter.ParameterType == typeof(CancellationToken))
        {
            return cancellationToken;
        }

        if (parameter.ParameterType == typeof(DateTimeOffset))
        {
            return PreparedScanTestHost.FixedUtcNow;
        }

        return parameter.HasDefaultValue
            ? parameter.DefaultValue
            : parameter.ParameterType.IsValueType
                ? Activator.CreateInstance(parameter.ParameterType)
                : null;
    }

    private static Type RequireClaimedPacketSourceContract()
    {
        Type? contract = typeof(IPreparedPass1Store).Assembly.GetType(
            "Vulgata.Core.DomainServices.IPreparedPass1ClaimedPacketSource",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(
            contract is not null,
            "T052 RED: IPreparedPass1ClaimedPacketSource is absent; T053 must add the claimed-packet source contract.");
        return contract!;
    }

    private static Type RequireClaimedPacketSourceImplementation()
    {
        Type? implementation = typeof(PreparedPass1Store).Assembly.GetType(
            "Vulgata.Infrastructure.Data.PreparedPass1ClaimedPacketSource",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(
            implementation is not null,
            "T052 RED: PreparedPass1ClaimedPacketSource is absent; T053 must add the infrastructure implementation.");
        return implementation!;
    }

    private static MethodInfo? FindHydrateMethod(Type contract) =>
        contract.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(method => method.Name is "HydrateAsync" or "LoadAsync" or "CreateAsync" or "GetAsync");

    private static async Task<ClaimedFixture> CreateClaimedFixtureAsync()
    {
        string administrator = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN")
            ?? throw new InvalidOperationException(
                "T052 requires VULGATA_TEST_POSTGRES_ADMIN for an isolated PostgreSQL 17 database.");
        PreparedScanPostgreSqlFixture prepared =
            await PreparedScanPostgreSqlFixture.CreateAsync(administrator);
        try
        {
            PreparedScanCommitResult committed = await PreparedScanPostgreSqlFixture
                .CreateStore(prepared.Context)
                .CommitPairAsync(prepared.Authorization, prepared.Package, 0, CancellationToken.None);
            Assert.Equal(PreparedScanCommitOutcome.Committed, committed.Outcome);
            string runtime = "{\"runtime\":\"story-12-3-t052\"}";
            string runtimeHash = Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(runtime)));
            await prepared.Context.ScanExecutions.ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.PreScanResultId, Guid.Empty)
                .SetProperty(value => value.Phase, ScanExecutionPhase.Prepared)
                .SetProperty(value => value.PromptIdentity, "story-12-3-t052-prompt")
                .SetProperty(value => value.DocumentSchemaIdentity, "code-logic/file-v2")
                .SetProperty(value => value.AnalysisRuntimeIdentityJson, runtime)
                .SetProperty(value => value.AnalysisRuntimeIdentityHash, runtimeHash)
                .SetProperty(value => value.UpdatedAt, PreparedScanTestHost.FixedUtcNow));
            prepared.Context.ChangeTracker.Clear();
            PreparedPass1StartResult started = await new PreparedPass1Store(prepared.Context)
                .StartAsync(prepared.Authorization, CreateHandoff(prepared.Package), 2, CancellationToken.None);
            Assert.Equal(PreparedPass1StartOutcome.Created, started.Outcome);
            Pass1PairExecution pair = await prepared.Context.Pass1PairExecutions.SingleAsync();
            PreparedPass1ClaimResult claim = await new PreparedPass1Store(prepared.Context)
                .ClaimNextAsync(pair.Id, PreparedScanTestHost.FixedUtcNow, TimeSpan.FromMinutes(5), CancellationToken.None);
            Assert.Equal(PreparedPass1ClaimOutcome.Claimed, claim.Outcome);
            ScanJobExecution job = await prepared.Context.ScanJobExecutions
                .AsNoTracking().SingleAsync(value => value.Id == claim.JobId);
            ScanExecution execution = await prepared.Context.ScanExecutions
                .AsNoTracking().SingleAsync(value => value.Id == job.ScanExecutionId);
            CodeUnit unit = await prepared.Context.CodeUnits
                .AsNoTracking().SingleAsync(value => value.Id == job.FileCodeUnitId);
            return new(
                prepared,
                pair.Id,
                claim,
                unit.Id,
                unit.RepositoryId,
                execution.Id,
                execution.ScanRunId,
                execution.ProfiledRevision,
                unit.FilePath,
                unit.SourceHash,
                unit.FileHash!,
                unit.ByteStart!.Value,
                unit.ByteEndExclusive!.Value,
                unit.LineStart,
                unit.LineEnd,
                "package prepared;\nclass ClaimedPacket { }\n");
        }
        catch
        {
            await prepared.DisposeAsync();
            throw;
        }
    }

    private static async Task ApplyDriftAsync(ClaimedFixture fixture, string drift)
    {
        switch (drift)
        {
            // ScanJobExecution has no separate owner column: the opaque lease
            // token is the durable claim-owner identity.
            case "replaced-owner-token":
                await fixture.Prepared.Context.ScanJobExecutions.Where(value => value.Id == fixture.Claim.JobId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.LeaseToken, Guid.NewGuid()));
                break;
            case "job-version":
                await fixture.Prepared.Context.ScanJobExecutions.Where(value => value.Id == fixture.Claim.JobId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.StateVersion, value => value.StateVersion + 1));
                break;
            case "expired-lease":
                await fixture.Prepared.Context.ScanJobExecutions.Where(value => value.Id == fixture.Claim.JobId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.LeaseExpiresAt, PreparedScanTestHost.FixedUtcNow.AddSeconds(-1)));
                break;
            case "pair-version":
                await fixture.Prepared.Context.Pass1PairExecutions.Where(value => value.Id == fixture.PairId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.StateVersion, value => value.StateVersion + 1));
                break;
            case "pair-not-running":
                await fixture.Prepared.Context.Pass1PairExecutions.Where(value => value.Id == fixture.PairId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Phase, Pass1PairExecutionPhase.PausedInspection));
                break;
            case "authorization":
                await fixture.Prepared.Context.SystemOwnerAssignments.ExecuteDeleteAsync();
                break;
            case "repository":
                await fixture.Prepared.Context.ScanJobExecutions.Where(value => value.Id == fixture.Claim.JobId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.RepositoryId, Guid.NewGuid()));
                break;
            case "profiled-revision":
                await fixture.Prepared.Context.ScanExecutions.Where(value => value.Id == fixture.ScanExecutionId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.ProfiledRevision, new string('b', 40)));
                break;
            case "path":
                await fixture.Prepared.Context.CodeUnits.Where(value => value.Id == fixture.UnitId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.FilePath, "src/drifted.java"));
                break;
            case "source-hash":
                await fixture.Prepared.Context.CodeUnits.Where(value => value.Id == fixture.UnitId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.SourceHash, new string('c', 64)));
                break;
            case "range":
                await fixture.Prepared.Context.CodeUnits.Where(value => value.Id == fixture.UnitId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.ByteEndExclusive, fixture.ByteEnd + 1));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(drift));
        }

        fixture.Prepared.Context.ChangeTracker.Clear();
    }

    private static WebApplicationFactory<Program> CreateHost(
        string connectionString,
        RecordingSourceReader reader) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            string keyRingPath = Path.Combine(Path.GetTempPath(), "vulgata-tests", "t052", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(keyRingPath);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["DataProtection:KeyRingPath"] = keyRingPath,
                    ["DataProtection:ApplicationName"] = "Vulgata.Tests.T052." + Guid.NewGuid().ToString("N"),
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<VulgataDbContext>>();
                services.RemoveAll<IDbContextFactory<VulgataDbContext>>();
                services.RemoveAll<IConfigureOptions<DbContextOptions<ApplicationDbContext>>>();
                services.RemoveAll<IConfigureOptions<DbContextOptions<VulgataDbContext>>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<VulgataDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<VulgataDbContext>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(
                    connectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__IdentityMigrationsHistory", "identity")));
                services.AddDbContext<VulgataDbContext>(options => options.UseNpgsql(
                    connectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public")));
                services.AddDbContextFactory<VulgataDbContext>(options => options.UseNpgsql(
                    connectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public")));
                services.RemoveAll<IRevisionBoundSourceReader>();
                services.AddSingleton<IRevisionBoundSourceReader>(reader);
            });
        });

    private static PreparedScanHandoff CreateHandoff(PreparedScanPairPackage package) =>
        new(
            "prepared-scan-handoff/1.0",
            package.CoordinationId,
            package.PairSelectionFingerprint,
            package.PairPackageFingerprint,
            package.Repositories.OrderBy(value => value.RepositoryId).Select(repository => new PreparedScanManifestBinding(
                repository.RepositoryId,
                repository.ScanRunId,
                repository.ProfiledRevision,
                PreparedScanIdentityBuilders.CreateDeterministicIdentity("PreparedScanManifest", repository.ManifestFingerprint),
                repository.ManifestFingerprint,
                PreparedScanIdentityBuilders.CreateCodeGraphProof(repository.CodeGraphProof).Fingerprint,
                PreparedScanIdentityBuilders.CreateJavaReadiness(repository.JavaReadiness).Fingerprint,
                repository.PreparedScanContextIdentityHash)).ToArray(),
            package.Repositories.SelectMany(value => value.FileCodeUnits).ToArray(),
            package.SelectedCount);

    private sealed class RecordingSourceReader(string sourceText) : IRevisionBoundSourceReader
    {
        public int ReadCount { get; private set; }

        public Task<SourceExcerptResult> ReadAsync(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return Task.FromResult(new SourceExcerptResult(
                AnalysisOperationStatus.Succeeded,
                scope.RepositoryId,
                scope.ProfiledRevision,
                request.RelativePath,
                request.Range,
                request.ExpectedSourceSha256,
                sourceText,
                Encoding.UTF8.GetByteCount(sourceText),
                null));
        }
    }

    private sealed record ClaimedFixture(
        PreparedScanPostgreSqlFixture Prepared,
        Guid PairId,
        PreparedPass1ClaimResult Claim,
        Guid UnitId,
        Guid RepositoryId,
        Guid ScanExecutionId,
        Guid ScanRunId,
        string ProfiledRevision,
        string RelativePath,
        string SourceHash,
        string FileHash,
        long ByteStart,
        long ByteEnd,
        int LineStart,
        int LineEnd,
        string SourceText) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Prepared.DisposeAsync();
    }
}
