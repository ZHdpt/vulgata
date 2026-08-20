using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared.Scans;
using Vulgata.Tests.PreparedScanning.TestDoubles;
using Vulgata.Web.Data;
using Xunit.Abstractions;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.PreparedScanning;

[Collection(PreparedScanningCollection.Name)]
public sealed class PreparedScanReleaseQualificationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ProductionPairBuilder_ThreeRunsIncludingReversedInputs_ProducesIdenticalSc001OutputsAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(output);

        PreparedScanPairBuildResult first = await harness.BuildAsync(reverseInputs: false);
        PreparedScanPairBuildResult second = await harness.BuildAsync(reverseInputs: false);
        PreparedScanPairBuildResult reversed = await harness.BuildAsync(reverseInputs: true);

        Assert.Null(first.Failure);
        Assert.NotNull(first.PairPackage);
        Assert.Null(second.Failure);
        Assert.NotNull(second.PairPackage);
        Assert.Null(reversed.Failure);
        Assert.NotNull(reversed.PairPackage);
        Assert.Equal(ReleaseProjection(first.PairPackage!), ReleaseProjection(second.PairPackage!));
        Assert.Equal(ReleaseProjection(first.PairPackage!), ReleaseProjection(reversed.PairPackage!));
        Assert.InRange(first.PairPackage!.SelectedCount, 50, 150);
        harness.AssertTimingBounds();
    }

    [Fact]
    public async Task ProductionPairBuilder_OversizedEligibleXml_IsSplitIntoBoundedFileCodeUnitsAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(output);

        PreparedScanPairBuildResult result = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(result.Failure);
        Assert.NotNull(result.PairPackage);
        PreparedFileCodeUnitDraft[] splitUnits =
        [
            .. result.PairPackage!.Repositories
                .SelectMany(repository => repository.FileCodeUnits)
                .Where(unit => unit.RelativePath == ReleaseHarness.OversizedXmlPath)
                .OrderBy(unit => unit.SegmentOrdinal),
        ];
        Assert.True(splitUnits.Length >= 2,
            "T031 requires an eligible oversized XML file to be split through SafeXmlDirectChildRangeSet; it must not be silently skipped.");
        Assert.All(splitUnits, unit =>
        {
            Assert.InRange(unit.ByteEndExclusive - unit.ByteStart, 1, PreparedSourceRangePlanner.MaximumRawBytes);
            Assert.InRange(unit.LineEnd - unit.LineStart + 1, 1, PreparedSourceRangePlanner.MaximumLogicalLines);
        });
    }

    [Fact]
    public async Task ProductionPairBuilder_MatchingXmlCatalogEvidence_ProducesClassificationsAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(output);

        PreparedScanPairBuildResult result = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(result.Failure);
        Assert.NotNull(result.PairPackage);
        CommunicationKindClassification[] classifications =
        [
            .. result.PairPackage!.Repositories.SelectMany(repository => repository.Classifications),
        ];
        Assert.NotEmpty(classifications);
        Assert.Contains(classifications, classification =>
            classification.Kind == CommunicationKind.Http &&
            classification.Level == ClassificationLevel.Confirmed &&
            classification.RelativePath.EndsWith("application.xml", StringComparison.Ordinal));
    }

    private static string ReleaseProjection(PreparedScanPairPackage package)
    {
        object projection = new
        {
            package.CoordinationId,
            package.PairSelectionFingerprint,
            package.PairPackageFingerprint,
            package.SelectedCount,
            Repositories = package.Repositories
                .OrderBy(repository => repository.RepositoryId.ToString("D"), StringComparer.Ordinal)
                .Select(repository => new
                {
                    repository.RepositoryId,
                    repository.ScanRunId,
                    repository.ProfiledRevision,
                    repository.SnapshotIdentity,
                    repository.RuntimeIdentityHash,
                    CodeGraphProofIdentity = repository.CodeGraphProof.IdentityHash,
                    JavaReadinessIdentity = repository.JavaReadiness.IdentityHash,
                    CatalogIdentity = repository.Catalog.ResourceHash,
                    repository.PreparedScanContextIdentityHash,
                    repository.ContextContentHash,
                    repository.ManifestFingerprint,
                    Units = repository.FileCodeUnits
                        .OrderBy(unit => unit.ManifestOrdinal)
                        .Select(unit => new
                        {
                            unit.RelativePath,
                            unit.FileHash,
                            unit.ByteStart,
                            unit.ByteEndExclusive,
                            unit.LineStart,
                            unit.LineEnd,
                            unit.SegmentOrdinal,
                            unit.SegmentCount,
                            unit.SourceHash,
                            unit.IdentityFingerprint,
                            unit.DirectBusinessScore,
                            unit.FinalBusinessScore,
                            unit.SelectionReason,
                            unit.ManifestOrdinal,
                            unit.PairSelectionOrdinal,
                        }),
                    Audits = repository.Audits
                        .OrderBy(audit => audit.Ordinal)
                        .Select(audit => new
                        {
                            audit.Ordinal,
                            audit.RelativePath,
                            audit.Outcome,
                            audit.Reason,
                            audit.FileHash,
                            audit.RawByteCount,
                            audit.LogicalLineCount,
                            audit.FileCodeUnitIdentityFingerprint,
                            audit.PairSelectionOrdinal,
                            audit.PreparedScanManifestEntryId,
                            audit.DetailFingerprint,
                        }),
                    Classifications = repository.Classifications
                        .OrderBy(classification => classification.Fingerprint, StringComparer.Ordinal)
                        .Select(classification => new
                        {
                            classification.Kind,
                            classification.Level,
                            classification.Confidence,
                            classification.Rule,
                            classification.Provenance,
                            classification.RelativePath,
                            classification.Locator,
                            classification.OccurrenceCount,
                            classification.Fingerprint,
                        }),
                }),
        };
        return JsonSerializer.Serialize(projection);
    }

    private sealed class ReleaseHarness : IAsyncDisposable
    {
        internal const string OversizedXmlPath = "src/release/oversized-application.xml";
        private readonly LoginLogoutTests.CustomWebApplicationFactory _baseFactory;
        private readonly WebApplicationFactory<Program> _factory;
        private readonly AsyncServiceScope _scope;
        private readonly IPreparedScanPairBuilder _builder;
        private readonly SystemEntity _system;
        private readonly RepositoryEntity[] _repositories;
        private readonly PreparedScanRepositoryRevisionBinding[] _bindings;
        private readonly ITestOutputHelper _output;
        private readonly Dictionary<Guid, List<long>> _codeGraphMilliseconds = [];
        private readonly Dictionary<Guid, List<long>> _javaMilliseconds = [];
        private readonly Dictionary<Guid, List<long>> _preScanMilliseconds = [];
        private bool _reverseCandidates;

        private ReleaseHarness(
            LoginLogoutTests.CustomWebApplicationFactory baseFactory,
            WebApplicationFactory<Program> factory,
            AsyncServiceScope scope,
            IPreparedScanPairBuilder builder,
            SystemEntity system,
            RepositoryEntity[] repositories,
            PreparedScanRepositoryRevisionBinding[] bindings,
            ITestOutputHelper output)
        {
            _baseFactory = baseFactory;
            _factory = factory;
            _scope = scope;
            _builder = builder;
            _system = system;
            _repositories = repositories;
            _bindings = bindings;
            _output = output;
        }

        internal static async Task<ReleaseHarness> CreateAsync(ITestOutputHelper output)
        {
            Dictionary<Guid, List<long>>? codeGraphTimings = null;
            Dictionary<Guid, List<long>>? javaTimings = null;
            var starts = new Dictionary<Guid, Stopwatch>();
            var baseFactory = new LoginLogoutTests.CustomWebApplicationFactory();
            var revisionAcquirer = new StubPreparedRevisionAcquirer();
            revisionAcquirer.Handler = (request, _) =>
            {
                starts[request.RepositoryId] = Stopwatch.StartNew();
                return Task.FromResult(new PreparedRevisionAcquisitionResult(
                    new PreparedRevisionSnapshot(
                        request.RepositoryId,
                        request.ScanRunId,
                        request.RequestedRef,
                        request.ProfiledRevision,
                        Hash($"snapshot:{request.RepositoryId:D}"),
                        $"snapshot-{request.RepositoryId:N}",
                        $"root-{request.RepositoryId:N}"),
                    null));
            };
            var snapshotSource = new StubPreparedSnapshotSource();
            ReleaseHarness? harness = null;
            snapshotSource.Handler = (snapshot, _) => Task.FromResult(new PreparedSnapshotReadResult(
                CreateReleaseFiles(snapshot, harness?._reverseCandidates == true),
                null));
            var codeGraph = new StubPreparedCodeGraphAnalysisPort();
            codeGraph.ProofHandler = (request, _) =>
            {
                Stopwatch timer = Stopwatch.StartNew();
                PreparedCodeGraphProofResult result = CompleteCodeGraph(request);
                timer.Stop();
                AddTiming(codeGraphTimings!, request.Scope.RepositoryId, timer.ElapsedMilliseconds);
                return Task.FromResult(result);
            };
            var java = new StubPreparedJavaReadinessPort();
            java.Handler = (request, _) =>
            {
                Stopwatch timer = Stopwatch.StartNew();
                PreparedJavaReadinessResult result = ReadyJava(request);
                timer.Stop();
                AddTiming(javaTimings!, request.Scope.RepositoryId, timer.ElapsedMilliseconds);
                return Task.FromResult(result);
            };
            var catalog = new StubPreparedCommunicationCatalogSource
            {
                Handler = _ => Task.FromResult(new PreparedCatalogLoadResult(CreateCatalog(), null)),
            };
            var runtimeProbe = new StubReadyAnalysisRuntimeHealthProbe();
            WebApplicationFactory<Program> factory = baseFactory.WithWebHostBuilder(web =>
                web.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IPreparedRevisionAcquirer>();
                    services.AddSingleton<IPreparedRevisionAcquirer>(revisionAcquirer);
                    services.RemoveAll<IPreparedSnapshotSource>();
                    services.AddSingleton<IPreparedSnapshotSource>(snapshotSource);
                    services.RemoveAll<IAnalysisRuntimeHealthProbe>();
                    services.AddSingleton<IAnalysisRuntimeHealthProbe>(runtimeProbe);
                    services.RemoveAll<IPreparedCommunicationCatalogSource>();
                    services.AddSingleton<IPreparedCommunicationCatalogSource>(catalog);
                    services.RemoveAll<IPreparedCodeGraphAnalysisPort>();
                    services.AddSingleton<IPreparedCodeGraphAnalysisPort>(codeGraph);
                    services.RemoveAll<IPreparedJavaReadinessPort>();
                    services.AddSingleton<IPreparedJavaReadinessPort>(java);
                }));
            AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            VulgataDbContext db = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            (SystemEntity system, RepositoryEntity[] repositories, PreparedScanRepositoryRevisionBinding[] bindings) =
                await SeedAsync(db);
            IPreparedScanPairBuilder builder = scope.ServiceProvider.GetRequiredService<IPreparedScanPairBuilder>();
            harness = new(baseFactory, factory, scope, builder, system, repositories, bindings, output);
            codeGraphTimings = harness._codeGraphMilliseconds;
            javaTimings = harness._javaMilliseconds;
            harness._starts = starts;
            return harness;
        }

        private Dictionary<Guid, Stopwatch> _starts = [];

        internal async Task<PreparedScanPairBuildResult> BuildAsync(bool reverseInputs)
        {
            _reverseCandidates = reverseInputs;
            RepositoryEntity[] repositories = reverseInputs ? [.. _repositories.Reverse()] : _repositories;
            PreparedScanRepositoryRevisionBinding[] bindings = reverseInputs ? [.. _bindings.Reverse()] : _bindings;
            Stopwatch total = Stopwatch.StartNew();
            PreparedScanPairBuildResult result = await _builder.BuildAsync(
                _system.Id,
                repositories,
                bindings,
                CancellationToken.None);
            total.Stop();
            foreach (RepositoryEntity repository in _repositories)
            {
                long elapsed = _starts.TryGetValue(repository.Id, out Stopwatch? timer)
                    ? timer.ElapsedMilliseconds
                    : total.ElapsedMilliseconds;
                AddTiming(_preScanMilliseconds, repository.Id, elapsed);
            }
            WriteTimings();
            return result;
        }

        internal void AssertTimingBounds()
        {
            Assert.All(_preScanMilliseconds.Values.SelectMany(values => values), value =>
                Assert.InRange(value, 0, 119_999));
            Assert.All(_codeGraphMilliseconds.Values.SelectMany(values => values), value =>
                Assert.InRange(value, 0, 29_999));
            Assert.All(_javaMilliseconds.Values.SelectMany(values => values), value =>
                Assert.InRange(value, 0, 59_999));
        }

        private void WriteTimings()
        {
            foreach (RepositoryEntity repository in _repositories.OrderBy(value => value.Id))
            {
                _output.WriteLine(
                    $"Repository={repository.Id:D}; PreScanMs={Last(_preScanMilliseconds, repository.Id)}; " +
                    $"CodeGraphMs={Last(_codeGraphMilliseconds, repository.Id)}; JdtLsMs={Last(_javaMilliseconds, repository.Id)}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            _factory.Dispose();
            _baseFactory.Dispose();
        }

        private static long Last(Dictionary<Guid, List<long>> values, Guid id) =>
            values.TryGetValue(id, out List<long>? timings) && timings.Count > 0 ? timings[^1] : -1;

        private static void AddTiming(Dictionary<Guid, List<long>> values, Guid id, long elapsed)
        {
            if (!values.TryGetValue(id, out List<long>? timings))
            {
                timings = [];
                values.Add(id, timings);
            }
            timings.Add(elapsed);
        }
    }

    private static IReadOnlyList<PreparedSnapshotFile> CreateReleaseFiles(
        PreparedRevisionSnapshot snapshot,
        bool reverse)
    {
        var files = new List<PreparedSnapshotFile>(61);
        for (int index = 0; index < 60; index++)
        {
            bool xml = index % 2 != 0;
            string path = xml ? $"src/release/config-{index:00}.xml" : $"src/release/Entry{index:00}.java";
            string source = xml
                ? $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><configuration><endpoint url=\"https://service-{index}.invalid/api\" /></configuration>"
                : $"package release; public final class Entry{index:00} {{ public static final int VALUE = {index}; }}";
            files.Add(File(path, xml ? "XML" : "Java", source));
        }
        var oversized = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><configuration>");
        for (int index = 0; index < 4_000; index++)
        {
            oversized.Append("<endpoint url=\"https://oversized.invalid/api/")
                .Append(index)
                .Append("\" />\n");
        }
        oversized.Append("</configuration>");
        files.Add(File(ReleaseHarness.OversizedXmlPath, "XML", oversized.ToString()));
        return reverse ? [.. files.AsEnumerable().Reverse()] : files;
    }

    private static PreparedSnapshotFile File(string path, string language, string source)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(source);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new(path, language, true, "EligibleProductionSource", hash, hash, bytes.LongLength,
            source.Count(character => character == '\n') + 1, "utf-8", bytes);
    }

    private static CommunicationPatternCatalog CreateCatalog() => new(
        "communication-pattern-catalog/1.0",
        "release-qualification-catalog",
        "1",
        Hash("release-qualification-catalog"),
        [
            new CommunicationCatalogRule(
                "http-endpoint",
                "src/release/*.xml",
                [new("", "endpoint")],
                null,
                [],
                new(CommunicationCatalogExtractionKind.Attribute, "", "url"),
                null,
                CommunicationKind.Http,
                EvidenceRule.HttpMatchedConfiguration,
                EvidenceProvenance.MatchedConfiguration),
        ]);

    private static PreparedCodeGraphProofResult CompleteCodeGraph(PreparedCodeGraphProofRequest request) => new(
        AnalysisOperationStatus.Succeeded,
        new CodeGraphCompletenessProof(
            request.Scope.RepositoryId, request.Scope.ScanRunId, request.Scope.ProfiledRevision,
            request.SnapshotIdentity, request.SnapshotRootBindingHash, request.Scope.RuntimeIdentityHash,
            "release-index", "codegraph-completeness-proof/1.0", true, CodeGraphIndexState.Complete,
            0, 0, 0, false, false, 0, 61, request.LogicalProjectBinding, request.LogicalIndexBinding,
            true, true, true, true, true, true, true, Hash("codegraph-proof"), 0),
        null);

    private static PreparedJavaReadinessResult ReadyJava(PreparedJavaReadinessRequest request) => new(
        AnalysisOperationStatus.Succeeded,
        new JavaReadinessProof(
            request.Scope.RepositoryId, request.Scope.ScanRunId, request.Scope.ProfiledRevision,
            request.SnapshotIdentity, request.Scope.RuntimeIdentityHash, request.WorkspaceBindingHash,
            "java-readiness/1.0", JavaReadinessOutcome.Ready,
            "jdt-language-server-1.61.0-202608141332", Hash("jdt-archive"), 21, "2.25.29",
            AnalysisTransport.Stdio, true, request.EligibleJavaRelativePaths.Order(StringComparer.Ordinal).First(),
            true, true, JavaImportState.Usable, 30, true, true, true, true, true, Hash("java-ready"), 0),
        null);

    private static async Task<(SystemEntity, RepositoryEntity[], PreparedScanRepositoryRevisionBinding[])> SeedAsync(
        VulgataDbContext db)
    {
        DateTimeOffset now = PreparedScanTestHost.FixedUtcNow;
        const string firstRevision = "0123456789abcdef0123456789abcdef01234567";
        const string secondRevision = "fedcba9876543210fedcba9876543210fedcba98";
        SystemEntity system = new("Release qualification system", "neutral", "global", now);
        RepositoryEntity first = system.AddRepository("release-one", "https://example.invalid/release-one.git", null, "one", now);
        RepositoryEntity second = system.AddRepository("release-two", "https://example.invalid/release-two.git", null, "two", now);
        ScanRun firstRun = ScanRun.Start(first.Id, 1_800, now);
        ScanRun secondRun = ScanRun.Start(second.Id, 1_800, now);
        firstRun.BindProfiledRevision(firstRevision);
        secondRun.BindProfiledRevision(secondRevision);
        ScanContextSnapshot firstContext = ScanContextSnapshot.CapturePrepared(firstRun, "global", "system", first.Context, now);
        ScanContextSnapshot secondContext = ScanContextSnapshot.CapturePrepared(secondRun, "global", "system", second.Context, now);
        PreScanResult firstPreScan = PreScanResult.CreateApproved(first.Id, firstRun.Id, firstRevision, now,
            new('f', 64), "预扫描已批准——继续进入顺序演示运行器", ConfirmedCommunicationKinds.None, 0, 0, 0, false, []);
        PreScanResult secondPreScan = PreScanResult.CreateApproved(second.Id, secondRun.Id, secondRevision, now,
            new('1', 64), "预扫描已批准——继续进入顺序演示运行器", ConfirmedCommunicationKinds.None, 0, 0, 0, false, []);
        ScanExecution firstExecution = ScanExecution.Start(Guid.NewGuid(), first.Id, system.Id, firstRun.Id, firstPreScan.Id, firstRevision, now);
        ScanExecution secondExecution = ScanExecution.Start(Guid.NewGuid(), second.Id, system.Id, secondRun.Id, secondPreScan.Id, secondRevision, now);
        TwoRepositoryScanCoordination coordination = TwoRepositoryScanCoordination.Create(firstExecution, secondExecution, now);
        db.Systems.Add(system);
        db.ScanRuns.AddRange(firstRun, secondRun);
        db.ScanContextSnapshots.AddRange(firstContext, secondContext);
        db.PreScanResults.AddRange(firstPreScan, secondPreScan);
        db.ScanExecutions.AddRange(firstExecution, secondExecution);
        db.TwoRepositoryScanCoordinations.Add(coordination);
        await db.SaveChangesAsync();
        return (system, [first, second],
        [
            new() { RepositoryId = first.Id, GitRef = "refs/tags/release-a", ProfiledRevision = firstRevision },
            new() { RepositoryId = second.Id, GitRef = "refs/tags/release-b", ProfiledRevision = secondRevision },
        ]);
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
