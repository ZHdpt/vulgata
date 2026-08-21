using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.PreparedScanning;
using Xunit.Sdk;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T030 PostgreSQL RED slice. The fixture enters through the
/// accepted Story 12.2 pair and Story 12.1 job/lease seams, then calls the
/// concrete Story 12.3 committer. PostgreSQL triggers are the fault injector;
/// no test-local transaction or committer is used.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class Pass1DocumentPostgreSqlAtomicTests
{
    private static readonly Guid TargetDocumentVersionId =
        Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
    private static readonly Guid TargetLogicalDocumentId =
        Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff");
    private static readonly Guid TargetAnchorId =
        Guid.Parse("cccccccc-dddd-4eee-8fff-aaaaaaaaaaaa");
    private static readonly Guid NavigationReceiptId =
        Guid.Parse("dddddddd-eeee-4fff-8000-bbbbbbbbbbbb");

    public static TheoryData<string, string, string, string> DocumentWriteBoundaries => new()
    {
        { "before-logical-document", "vulgata.\"LogicalDocuments\"", "INSERT", "BEFORE" },
        { "after-logical-document", "vulgata.\"LogicalDocuments\"", "INSERT", "AFTER" },
        { "before-document-version", "vulgata.\"DocumentVersions\"", "INSERT", "BEFORE" },
        { "after-document-version", "vulgata.\"DocumentVersions\"", "INSERT", "AFTER" },
        { "before-source-anchor", "vulgata.\"SourceAnchors\"", "INSERT", "BEFORE" },
        { "after-source-anchor", "vulgata.\"SourceAnchors\"", "INSERT", "AFTER" },
        { "before-document-section", "vulgata.\"DocumentSections\"", "INSERT", "BEFORE" },
        { "after-document-section", "vulgata.\"DocumentSections\"", "INSERT", "AFTER" },
        { "before-index-entry", "vulgata.\"IndexEntries\"", "INSERT", "BEFORE" },
        { "after-index-entry", "vulgata.\"IndexEntries\"", "INSERT", "AFTER" },
        { "before-reference-edge", "vulgata.\"DocumentEdges\"", "INSERT", "BEFORE" },
        { "after-reference-edge", "vulgata.\"DocumentEdges\"", "INSERT", "AFTER" },
        { "before-job-completion", "vulgata.\"ScanJobExecutions\"", "UPDATE", "BEFORE" },
        { "after-job-completion", "vulgata.\"ScanJobExecutions\"", "UPDATE", "AFTER" },
    };

    [Theory]
    [MemberData(nameof(DocumentWriteBoundaries))]
    public async Task PostgreSql17_CommitFailureAtEveryWriteBoundary_RollsBackDocumentGraphAndJob(
        string boundary,
        string table,
        string operation,
        string timing)
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        await InstallFailingTriggerAsync(fixture.ConnectionString, boundary, table, operation, timing);
        GraphCounts before = await GraphCounts.ReadAsync(fixture.ConnectionString);

        Exception? failure = null;
        Pass1DocumentCommitResult? result = null;
        try
        {
            result = await fixture.Committer.CommitAsync(fixture.Request, CancellationToken.None);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (failure is null)
        {
            Assert.Fail($"Commit returned {result?.Status} ({result?.ReasonCode}) instead of reaching {boundary}.");
        }
        Assert.Contains("T030 injected document boundary", FlattenMessages(failure!), StringComparison.Ordinal);
        Assert.Equal(before, await GraphCounts.ReadAsync(fixture.ConnectionString));
        Assert.Equal(ScanJobExecutionState.Running, await fixture.ReadJobStateAsync());
    }

    [Fact]
    public async Task PostgreSql17_ConcurrentExactAndDifferentCommits_HaveOneWinnerAndMutationFreeConflict()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        await using VulgataDbContext secondContext = PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        IPass1CodeLogicDocumentCommitter second = new Pass1CodeLogicDocumentCommitter(secondContext);

        Task<Pass1DocumentCommitResult> first = fixture.Committer.CommitAsync(fixture.Request, CancellationToken.None);
        Task<Pass1DocumentCommitResult> exact = second.CommitAsync(fixture.Request, CancellationToken.None);
        Pass1DocumentCommitResult[] results = await Task.WhenAll(first, exact);

        Assert.Contains(results, value => value.Status == Pass1DocumentCommitStatus.Committed);
        Assert.Contains(results, value => value.Status is Pass1DocumentCommitStatus.Replayed or Pass1DocumentCommitStatus.Conflict);
        // The fixture includes one immutable target version for the REFERENCES edge;
        // the commit must add exactly one source version and no duplicate.
        Assert.Equal(2, await CountAsync(fixture.ConnectionString, "\"DocumentVersions\""));
        // The seeded target and committed source are distinct logical documents.
        Assert.Equal(2, await CountAsync(fixture.ConnectionString, "\"LogicalDocuments\""));
        Assert.Equal(1, await CountAsync(fixture.ConnectionString, "\"ScanJobExecutions\"", "\"State\" = 'Completed'"));
    }

    [Fact]
    public async Task PostgreSql17_StaleLeaseAndCancellation_LeaveRunningJobAndZeroGraphRows()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        GraphCounts before = await GraphCounts.ReadAsync(fixture.ConnectionString);

        Pass1DocumentCommitResult stale = await fixture.Committer.CommitAsync(
            fixture.Request with { ExpectedLeaseToken = Guid.NewGuid() }, CancellationToken.None);
        Assert.Equal(Pass1DocumentCommitStatus.Rejected, stale.Status);
        Assert.Equal(before, await GraphCounts.ReadAsync(fixture.ConnectionString));

        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Committer.CommitAsync(fixture.Request, cancellation.Token));
        Assert.Equal(before, await GraphCounts.ReadAsync(fixture.ConnectionString));
        Assert.Equal(ScanJobExecutionState.Running, await fixture.ReadJobStateAsync());
    }

    [Fact]
    public async Task PostgreSql17_ExactReplay_IsMutationFreeAndReconstructsScalarsWithoutArrayDependence()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        Pass1DocumentCommitResult first = await fixture.Committer.CommitAsync(fixture.Request, CancellationToken.None);
        Assert.True(first.Status == Pass1DocumentCommitStatus.Committed,
            $"first commit returned {first.Status}: {first.ReasonCode}");
        GraphCounts afterFirst = await GraphCounts.ReadAsync(fixture.ConnectionString);

        Pass1DocumentCommitResult replay = await fixture.Committer.CommitAsync(fixture.Request, CancellationToken.None);

        Assert.True(first.Status == Pass1DocumentCommitStatus.Committed,
            $"first commit returned {first.Status}: {first.ReasonCode}");
        Assert.Equal(Pass1DocumentCommitStatus.Replayed, replay.Status);
        Assert.Equal(first.DocumentVersionId, replay.DocumentVersionId);
        Assert.Equal(first.CommitFingerprint, replay.CommitFingerprint);
        Assert.Equal(afterFirst, await GraphCounts.ReadAsync(fixture.ConnectionString));
        // One seeded target version plus exactly one committed source version.
        Assert.Equal(2, await CountAsync(fixture.ConnectionString, "\"DocumentVersions\""));
        Assert.Equal(1, await CountAsync(fixture.ConnectionString, "\"ScanJobExecutions\"", "\"State\" = 'Completed'"));
    }

    [Fact]
    public async Task PostgreSql17_DifferentReplay_PreservesImmutableVersionChainAndPreviousSupersededPointer()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        Pass1DocumentCommitResult first = await fixture.Committer.CommitAsync(fixture.Request, CancellationToken.None);

        Pass1DocumentCommitResult changed = await fixture.Committer.CommitAsync(
            fixture.Request with { CanonicalCodeLogicJson = fixture.ChangedCanonicalJson },
            CancellationToken.None);

        Assert.True(first.Status == Pass1DocumentCommitStatus.Committed,
            $"first commit returned {first.Status}: {first.ReasonCode}");
        Assert.Equal(Pass1DocumentCommitStatus.Conflict, changed.Status);
        // One seeded target version plus exactly one committed source version.
        Assert.Equal(2, await CountAsync(fixture.ConnectionString, "\"DocumentVersions\""));
        Assert.Equal(0, await CountAsync(fixture.ConnectionString, "\"DocumentVersions\"", "\"SupersededById\" IS NOT NULL"));
    }

    private static string GetAdministratorConnectionStringOrSkip()
    {
        string? value = Environment.GetEnvironmentVariable("VULGATA_TEST_ADMIN_CONNECTION") ??
                        Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(value))
        {
            throw (SkipException)(Activator.CreateInstance(
                typeof(SkipException),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: ["T030 PostgreSQL qualification skipped: set VULGATA_TEST_ADMIN_CONNECTION."],
                culture: null)
                ?? throw new InvalidOperationException("xUnit SkipException constructor is unavailable."));
        }

        return value;
    }

    private static async Task InstallFailingTriggerAsync(
        string connectionString,
        string boundary,
        string table,
        string operation,
        string timing)
    {
        string suffix = boundary.Replace('-', '_');
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new($"""
            CREATE OR REPLACE FUNCTION t123_fail_{suffix}() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'T030 injected document boundary: {boundary}';
            END $$;
            CREATE TRIGGER t123_fail_{suffix}_trigger
            {timing} {operation} ON {table}
            FOR EACH ROW EXECUTE FUNCTION t123_fail_{suffix}();
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string FlattenMessages(Exception exception) =>
        string.Join(" | ", EnumerateExceptions(exception).Select(value => value.Message));

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    private static async Task<int> CountAsync(string connectionString, string table, string? predicate = null)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        string where = predicate is null ? string.Empty : $" WHERE {predicate}";
        await using NpgsqlCommand command = new($"SELECT count(*)::integer FROM vulgata.{table}{where};", connection);
        return (int)(await command.ExecuteScalarAsync() ?? 0);
    }

    private sealed class CommitFixture : IAsyncDisposable
    {
        private CommitFixture(
            PreparedScanPostgreSqlFixture prepared,
            IPass1CodeLogicDocumentCommitter committer,
            Pass1DocumentCommitRequest request,
            byte[] changedCanonicalJson)
        {
            Prepared = prepared;
            Committer = committer;
            Request = request;
            ChangedCanonicalJson = changedCanonicalJson;
        }

        internal PreparedScanPostgreSqlFixture Prepared { get; }
        internal string ConnectionString => Prepared.ConnectionString;
        internal IPass1CodeLogicDocumentCommitter Committer { get; }
        internal Pass1DocumentCommitRequest Request { get; }
        internal byte[] ChangedCanonicalJson { get; }

        internal static async Task<CommitFixture> CreateAsync(string administrator)
        {
            PreparedScanPostgreSqlFixture prepared = await PreparedScanPostgreSqlFixture.CreateAsync(administrator);
            try
            {
                await CommitPreparedPairAsync(prepared);
                await PromoteFixtureExecutionsAsync(prepared);
                IPreparedPass1Store store = new PreparedPass1Store(prepared.Context);
                PreparedPass1StartResult started = await store
                    .StartAsync(prepared.Authorization, CreateHandoff(prepared.Package), 2, CancellationToken.None);
                Assert.Equal(PreparedPass1StartOutcome.Created, started.Outcome);
                Pass1PairExecution pair = await prepared.Context.Pass1PairExecutions.SingleAsync();
                PreparedPass1ClaimResult claim = await store
                    .ClaimNextAsync(pair.Id, PreparedScanTestHost.FixedUtcNow, TimeSpan.FromMinutes(5), CancellationToken.None);
                Assert.Equal(PreparedPass1ClaimOutcome.Claimed, claim.Outcome);
                ScanJobExecution job = await prepared.Context.ScanJobExecutions.SingleAsync(value => value.Id == claim.JobId);
                CodeUnit unit = await prepared.Context.CodeUnits.SingleAsync(value => value.Id == job.FileCodeUnitId);
                CodeUnit targetUnit = await prepared.Context.CodeUnits
                    .Where(value => value.RepositoryId == unit.RepositoryId && value.Id != unit.Id)
                    .OrderBy(value => value.FilePath)
                    .FirstAsync();
                byte[] source = Encoding.UTF8.GetBytes("package neutral;\nclass OrderPolicy { }\n");
                string sourceHash = Convert.ToHexStringLower(SHA256.HashData(source));
                string fileHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("t030-file")));
                await prepared.Context.CodeUnits.Where(value => value.Id == unit.Id).ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.FileHash, fileHash)
                    .SetProperty(value => value.SourceHash, sourceHash)
                    .SetProperty(value => value.ByteStart, 0L)
                    .SetProperty(value => value.ByteEndExclusive, (long)source.Length)
                    .SetProperty(value => value.SegmentOrdinal, 1)
                    .SetProperty(value => value.SegmentCount, 1)
                    .SetProperty(value => value.LineStart, 1)
                    .SetProperty(value => value.LineEnd, 3));
                prepared.Context.ChangeTracker.Clear();
                unit = await prepared.Context.CodeUnits.AsNoTracking().SingleAsync(value => value.Id == unit.Id);
                ScanExecution execution = await prepared.Context.ScanExecutions.AsNoTracking().SingleAsync(value => value.Id == job.ScanExecutionId);
                PreparedScanManifest manifest = await prepared.Context.PreparedScanManifests.AsNoTracking().SingleAsync(value => value.RepositoryId == unit.RepositoryId);
                PreparedScanManifestEntry entry = await prepared.Context.PreparedScanManifestEntries.AsNoTracking()
                    .SingleAsync(value => value.PreparedScanManifestId == manifest.Id && value.FileCodeUnitId == unit.Id);
                PreparedRepositoryPackage repository = prepared.Package.Repositories.Single(value => value.RepositoryId == unit.RepositoryId);
                await SeedTargetGraphAsync(prepared.Context, execution, pair, job, targetUnit);
                PreparedPass1WorkerPacket packet = new(
                    pair.Id, job.Id, execution.Id, prepared.SystemId, unit.RepositoryId, unit.ScanRunId,
                    execution.ProfiledRevision, unit.Id, unit.FileCodeUnitIdentityFingerprint!, manifest.Id,
                    manifest.Fingerprint, pair.PairPackageFingerprint, unit.FilePath, unit.ByteStart!.Value,
                    unit.ByteEndExclusive!.Value, unit.LineStart, unit.LineEnd, unit.SegmentOrdinal!.Value,
                    unit.SegmentCount!.Value, unit.FileHash!, unit.SourceHash, execution.PromptIdentity!,
                    execution.DocumentSchemaIdentity!, execution.AnalysisRuntimeIdentityHash!, entry.ManifestOrdinal,
                    entry.PairSelectionOrdinal, source,
                    new PreparedPass1ContextSnapshotProjection(
                        repository.ContextSnapshotId, repository.RepositoryId, repository.ScanRunId,
                        repository.ProfiledRevision, repository.PreparedScanContextIdentityHash,
                        repository.ContextContentHash, repository.GlobalContext, repository.SystemContext,
                        repository.RepositoryContext), []);
                PreparedPass1ParsedWorkerResult parsed = PreparedPass1ResultParser.Parse(
                    packet, CreateCanonicalJson(packet, targetUnit.FilePath, targetUnit.SourceHash));
                Guid logicalId = PreparedPass1CanonicalCodec.CreateLogicalDocument(new(packet.RepositoryId, packet.RepositoryRelativePath, (ulong)packet.SegmentOrdinal)).Id!.Value;
                Guid versionId = PreparedPass1CanonicalCodec.CreateDocumentVersion(new(packet.JobId, Convert.FromHexString(packet.FileCodeUnitIdentityFingerprint), packet.DocumentSchemaIdentity)).Id!.Value;
                Pass1DocumentCommitRequest request = new(packet, parsed.CanonicalCandidateJson, logicalId, versionId, job.StateVersion, claim.LeaseToken, PreparedScanTestHost.FixedUtcNow.AddMinutes(1));
                JsonObject changed = JsonNode.Parse(Encoding.UTF8.GetString(parsed.CanonicalCandidateJson.Span))!.AsObject();
                changed["filePurpose"]!["summary"] = "changed replay";
                return new(prepared, new Pass1CodeLogicDocumentCommitter(prepared.Context), request,
                    Encoding.UTF8.GetBytes(changed.ToJsonString(new JsonSerializerOptions { WriteIndented = false })));
            }
            catch
            {
                await prepared.DisposeAsync();
                throw;
            }
        }

        internal async Task<ScanJobExecutionState> ReadJobStateAsync() =>
            await Prepared.Context.ScanJobExecutions.AsNoTracking()
                .Where(value => value.Id == Request.Packet.JobId)
                .Select(value => value.State)
                .SingleAsync();

        public async ValueTask DisposeAsync() => await Prepared.DisposeAsync();
    }

    private sealed record GraphCounts(
        int LogicalDocuments,
        int DocumentVersions,
        int SourceAnchors,
        int DocumentSections,
        int IndexEntries,
        int DocumentEdges,
        int CompletedJobs)
    {
        internal static async Task<GraphCounts> ReadAsync(string connectionString) => new(
            await CountAsync(connectionString, "\"LogicalDocuments\""),
            await CountAsync(connectionString, "\"DocumentVersions\""),
            await CountAsync(connectionString, "\"SourceAnchors\""),
            await CountAsync(connectionString, "\"DocumentSections\""),
            await CountAsync(connectionString, "\"IndexEntries\""),
            await CountAsync(connectionString, "\"DocumentEdges\""),
            await CountAsync(connectionString, "\"ScanJobExecutions\"", "\"State\" = 'Completed'"));
    }

    private static byte[] CreateCanonicalJson(
        PreparedPass1WorkerPacket packet,
        string targetPath,
        string targetSourceHash)
    {
        MethodInfo method = typeof(CodeLogicFileV2SchemaTests).GetMethod("BaseJson", BindingFlags.NonPublic | BindingFlags.Static)!;
        JsonObject root = JsonNode.Parse((string)method.Invoke(null, null)!)!.AsObject();
        JsonObject binding = root["binding"]!.AsObject();
        binding["jobId"] = packet.JobId.ToString("D");
        binding["scanExecutionId"] = packet.ScanExecutionId.ToString("D");
        binding["systemId"] = packet.SystemId.ToString("D");
        binding["repositoryId"] = packet.RepositoryId.ToString("D");
        binding["scanRunId"] = packet.ScanRunId.ToString("D");
        binding["profiledRevision"] = packet.ProfiledRevision;
        binding["fileCodeUnitId"] = packet.FileCodeUnitId.ToString("D");
        binding["fileCodeUnitIdentityFingerprint"] = packet.FileCodeUnitIdentityFingerprint;
        binding["preparedScanManifestId"] = packet.PreparedScanManifestId.ToString("D");
        binding["preparedScanManifestFingerprint"] = packet.PreparedScanManifestFingerprint;
        binding["pairPackageFingerprint"] = packet.PairPackageFingerprint;
        binding["repositoryRelativePath"] = packet.RepositoryRelativePath;
        binding["byteEnd"] = packet.ByteEnd;
        binding["lineEnd"] = packet.LineEnd;
        binding["fileHash"] = packet.FileHash;
        binding["sourceHash"] = packet.SourceHash;
        binding["promptIdentity"] = packet.PromptIdentity;
        binding["analysisRuntimeIdentityHash"] = packet.AnalysisRuntimeIdentityHash;
        JsonObject anchor = root["sourceAnchors"]![0]!.AsObject();
        anchor["repositoryRelativePath"] = packet.RepositoryRelativePath;
        anchor["byteEnd"] = packet.ByteEnd;
        anchor["lineEnd"] = packet.LineEnd;
        anchor["sourceHash"] = packet.SourceHash;
        root["communicationEvidence"] = new JsonArray();
        JsonObject reference = root["knownReferences"]![0]!.AsObject();
        reference["targetDocumentVersionId"] = TargetDocumentVersionId.ToString("D");
        reference["targetRepositoryRelativePath"] = targetPath;
        reference["targetSourceHash"] = targetSourceHash;
        reference["navigationReceiptId"] = NavigationReceiptId.ToString("D");
        JsonObject deferred = root["deferredObservations"]![0]!.AsObject();
        deferred["targetRepositoryRelativePath"] = targetPath;
        deferred["targetSourceHash"] = targetSourceHash;
        deferred["navigationReceiptIds"] = new JsonArray(NavigationReceiptId.ToString("D"));
        return Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
    }

    private static async Task SeedTargetGraphAsync(
        VulgataDbContext context,
        ScanExecution execution,
        Pass1PairExecution pair,
        ScanJobExecution job,
        CodeUnit targetUnit)
    {
        DateTimeOffset now = PreparedScanTestHost.FixedUtcNow;
        LogicalDocument logical = LogicalDocument.Create(
            TargetLogicalDocumentId,
            targetUnit.RepositoryId,
            DocumentType.CodeLogic,
            $"{targetUnit.FilePath}#segment=1",
            now);
        DocumentVersion version = DocumentVersion.Create(
            TargetDocumentVersionId,
            TargetLogicalDocumentId,
            targetUnit.RepositoryId,
            execution.SystemId,
            execution.ScanRunId,
            execution.ProfiledRevision,
            1,
            targetUnit.FilePath,
            targetUnit.FilePath,
            "code-logic/file-v2",
            "{}",
            "# target",
            new string('1', 64),
            new string('2', 64),
            now);
        context.LogicalDocuments.Add(logical);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        context.DocumentVersions.Add(version);
        context.SourceAnchors.Add(new SourceAnchor(
            TargetAnchorId,
            TargetDocumentVersionId,
            targetUnit.RepositoryId,
            execution.ScanRunId,
            targetUnit.Id,
            execution.ProfiledRevision,
            targetUnit.FilePath,
            $"{targetUnit.FilePath}#L1-L4",
            targetUnit.LineStart,
            targetUnit.LineEnd,
            targetUnit.SourceHash,
            now));
        context.Pass1NavigationReceipts.Add(Pass1NavigationReceipt.Create(
            NavigationReceiptId,
            job.Id,
            pair.Id,
            1,
            1,
            Pass1NavigationOperation.Definition,
            1,
            Pass1NavigationOutcomeKind.DocumentEvidence,
            null,
            new string('3', 64),
            new string('4', 64),
            1,
            32,
            "{}",
            now));
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {TargetDocumentVersionId} WHERE \"Id\" = {TargetLogicalDocumentId} AND \"RepositoryId\" = {targetUnit.RepositoryId}");
        context.ChangeTracker.Clear();
    }

    private static async Task CommitPreparedPairAsync(PreparedScanPostgreSqlFixture fixture)
    {
        PreparedScanCommitResult result = await PreparedScanPostgreSqlFixture.CreateStore(fixture.Context)
            .CommitPairAsync(fixture.Authorization, fixture.Package, 0, CancellationToken.None);
        Assert.Equal(PreparedScanCommitOutcome.Committed, result.Outcome);
    }

    private static async Task PromoteFixtureExecutionsAsync(PreparedScanPostgreSqlFixture fixture)
    {
        const string runtime = "{\"runtime\":\"story-12-3-test\"}";
        string runtimeHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(runtime)));
        Guid[] repositories = fixture.Package.Repositories.Select(value => value.RepositoryId).OrderBy(value => value).ToArray();
        for (int index = 0; index < repositories.Length; index++)
        {
            await fixture.Context.ScanExecutions.Where(value => value.RepositoryId == repositories[index]).ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.PreScanResultId, Guid.Empty)
                .SetProperty(value => value.Phase, ScanExecutionPhase.Prepared)
                .SetProperty(value => value.PromptIdentity, $"story-12-3-test-prompt-{index + 1}")
                .SetProperty(value => value.DocumentSchemaIdentity, "code-logic/file-v2")
                .SetProperty(value => value.AnalysisRuntimeIdentityJson, runtime)
                .SetProperty(value => value.AnalysisRuntimeIdentityHash, runtimeHash)
                .SetProperty(value => value.UpdatedAt, PreparedScanTestHost.FixedUtcNow));
        }

        await fixture.Context.PreparedScanManifests.ExecuteUpdateAsync(setters => setters
            .SetProperty(value => value.RuntimeIdentityHash, runtimeHash));

        fixture.Context.ChangeTracker.Clear();
    }

    private static PreparedScanHandoff CreateHandoff(PreparedScanPairPackage package) => new(
        "prepared-scan-handoff/1.0", package.CoordinationId, package.PairSelectionFingerprint,
        package.PairPackageFingerprint,
        package.Repositories.OrderBy(value => value.RepositoryId).Select(repository => new PreparedScanManifestBinding(
            repository.RepositoryId, repository.ScanRunId, repository.ProfiledRevision,
            PreparedScanIdentityBuilders.CreateDeterministicIdentity("PreparedScanManifest", repository.ManifestFingerprint),
            repository.ManifestFingerprint,
            PreparedScanIdentityBuilders.CreateCodeGraphProof(repository.CodeGraphProof).Fingerprint,
            PreparedScanIdentityBuilders.CreateJavaReadiness(repository.JavaReadiness).Fingerprint,
            repository.PreparedScanContextIdentityHash)).ToArray(),
        package.Repositories.SelectMany(value => value.FileCodeUnits).ToArray(), package.SelectedCount);
}
