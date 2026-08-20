using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Xunit.Abstractions;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// Story 12.2 T050 qualification. Every case owns a uniquely named disposable
/// PostgreSQL 17 database and enters through the public pair-commit port.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class PreparedScanAtomicPostgreSqlTests(ITestOutputHelper output)
{
    public static TheoryData<string, string, string> InsertBoundaries => new()
    {
        { "context-snapshot", "vulgata.\"ScanContextSnapshots\"", "INSERT" },
        { "manifest", "vulgata.\"PreparedScanManifests\"", "INSERT" },
        { "code-unit", "vulgata.\"CodeUnits\"", "INSERT" },
        { "manifest-entry", "vulgata.\"PreparedScanManifestEntries\"", "INSERT" },
        { "audit", "vulgata.\"PreparedScanAudits\"", "INSERT" },
        { "pre-scan-result", "vulgata.\"PreScanResults\"", "UPDATE" },
    };

    [Fact]
    public async Task PostgreSql17_ExactReplayAllowsOnlyCaptureMetadataVarianceAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);

        PreparedScanCommitResult committed = await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None);
        PreparedScanPairPackage metadataVariant = fixture.Package with
        {
            Repositories = fixture.Package.Repositories.Select(repository => repository with
            {
                ContextSnapshotId = Guid.NewGuid(),
                CapturedAt = repository.CapturedAt.AddMinutes(5),
                ApprovedAt = repository.ApprovedAt.AddMinutes(5),
                CodeGraphProof = repository.CodeGraphProof with
                {
                    ObservedDurationMilliseconds = repository.CodeGraphProof.ObservedDurationMilliseconds + 37,
                },
                JavaReadiness = repository.JavaReadiness with
                {
                    ObservedDurationMilliseconds = repository.JavaReadiness.ObservedDurationMilliseconds + 41,
                },
            }).ToArray(),
        };
        PreparedScanCommitResult replay = await store.CommitPairAsync(
            fixture.Authorization, metadataVariant, 0, CancellationToken.None);

        Assert.Equal(PreparedScanCommitOutcome.Committed, committed.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Replay, replay.Outcome);
        await AssertCompletePairAsync(fixture.ConnectionString, fixture.Package);
    }

    [Fact]
    public async Task PostgreSql17_StableProofOrChildMutationConflictsWithoutMutationAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);
        Assert.Equal(PreparedScanCommitOutcome.Committed, (await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None)).Outcome);
        DurableCounts before = await ReadCountsAsync(fixture.ConnectionString);

        PreparedRepositoryPackage first = fixture.Package.Repositories[0];
        PreparedScanPairPackage proofMutation = fixture.Package with
        {
            Repositories =
            [
                first with
                {
                    CodeGraphProof = first.CodeGraphProof with
                    {
                        LogicalIndexBinding = first.CodeGraphProof.LogicalIndexBinding + "-changed",
                    },
                },
                fixture.Package.Repositories[1],
            ],
        };
        PreparedScanCommitResult proofResult = await store.CommitPairAsync(
            fixture.Authorization, proofMutation, 0, CancellationToken.None);

        PreparedFileCodeUnitDraft unit = first.FileCodeUnits[0];
        PreparedScanPairPackage childMutation = fixture.Package with
        {
            Repositories =
            [
                first with
                {
                    FileCodeUnits =
                    [unit with { DirectBusinessScore = unit.DirectBusinessScore + 1 }, .. first.FileCodeUnits.Skip(1)],
                },
                fixture.Package.Repositories[1],
            ],
        };
        PreparedScanCommitResult childResult = await store.CommitPairAsync(
            fixture.Authorization, childMutation, 0, CancellationToken.None);

        output.WriteLine("Stable proof mutation outcome: {0}", proofResult.Outcome);
        output.WriteLine("Stable child mutation outcome: {0}", childResult.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Conflict, proofResult.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Conflict, childResult.Outcome);
        Assert.Equal(before, await ReadCountsAsync(fixture.ConnectionString));
    }

    [Theory]
    [MemberData(nameof(InsertBoundaries))]
    public async Task PostgreSql17_ForcedPersistenceBoundary_RollsBackAllPreparedVisibilityAsync(
        string boundary,
        string table,
        string operation)
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await InstallFailingTriggerAsync(fixture.ConnectionString, table, operation, boundary);
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);

        PreparedScanCommitResult result = await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None);

        Assert.True(
            result.Outcome is PreparedScanCommitOutcome.Conflict or PreparedScanCommitOutcome.Failed,
            $"Unexpected forced-boundary outcome: {result.Outcome}.");
        Assert.Equal(DurableCounts.PrincipalOnly, await ReadCountsAsync(fixture.ConnectionString));
        await AssertNoDispatchablePreparedResultAsync(fixture.ConnectionString);
    }

    [Theory]
    [MemberData(nameof(InsertBoundaries))]
    public async Task PostgreSql17_CancellationAtPersistenceBoundary_RollsBackAllPreparedVisibilityAsync(
        string boundary,
        string table,
        string operation)
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await InstallSleepingTriggerAsync(fixture.ConnectionString, table, operation, boundary);
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(150));

        PreparedScanCommitResult result = await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, cancellation.Token);

        Assert.Equal(PreparedScanCommitOutcome.Cancelled, result.Outcome);
        Assert.Equal(DurableCounts.PrincipalOnly, await ReadCountsAsync(fixture.ConnectionString));
        await AssertNoDispatchablePreparedResultAsync(fixture.ConnectionString);
    }

    [Fact]
    public async Task PostgreSql17_StaleWriterConflictsAndLeavesZeroPreparedRowsAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);

        PreparedScanCommitResult result = await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 1, CancellationToken.None);

        Assert.Equal(PreparedScanCommitOutcome.Conflict, result.Outcome);
        Assert.Equal(DurableCounts.PrincipalOnly, await ReadCountsAsync(fixture.ConnectionString));
    }

    [Fact]
    public async Task PostgreSql17_ConcurrentIdenticalWriters_CommitOnceAndReplayOnceAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await using VulgataDbContext secondContext =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        PreparedScanStore first = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);
        PreparedScanStore second = PreparedScanPostgreSqlFixture.CreateStore(secondContext);

        PreparedScanCommitResult[] outcomes = await Task.WhenAll(
            first.CommitPairAsync(fixture.Authorization, fixture.Package, 0, CancellationToken.None),
            second.CommitPairAsync(fixture.Authorization, fixture.Package, 0, CancellationToken.None));

        output.WriteLine("Concurrent identical outcomes: {0}",
            string.Join(", ", outcomes.Select(result => result.Outcome)));
        Assert.Equal(1, outcomes.Count(result => result.Outcome == PreparedScanCommitOutcome.Committed));
        Assert.Equal(1, outcomes.Count(result => result.Outcome == PreparedScanCommitOutcome.Replay));
        await AssertCompletePairAsync(fixture.ConnectionString, fixture.Package);
    }

    [Fact]
    public async Task PostgreSql17_ConcurrentChangedWriter_ConflictsWithoutPartialVisibilityAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await using VulgataDbContext secondContext =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        PreparedScanPairPackage changed = AddCanonicalClassification(fixture.Package);

        PreparedScanCommitResult[] outcomes = await Task.WhenAll(
            PreparedScanPostgreSqlFixture.CreateStore(fixture.Context).CommitPairAsync(
                fixture.Authorization, fixture.Package, 0, CancellationToken.None),
            PreparedScanPostgreSqlFixture.CreateStore(secondContext).CommitPairAsync(
                fixture.Authorization, changed, 0, CancellationToken.None));

        output.WriteLine("Concurrent changed outcomes: {0}",
            string.Join(", ", outcomes.Select(result => result.Outcome)));
        Assert.Equal(1, outcomes.Count(result => result.Outcome == PreparedScanCommitOutcome.Committed));
        Assert.Equal(1, outcomes.Count(result => result.Outcome == PreparedScanCommitOutcome.Conflict));
        await AssertCompletePairAsync(fixture.ConnectionString, fixture.Package);
    }

    [Fact]
    public async Task PostgreSql17_AuditsReconstructTagsOneThroughTenAndSelectedAssociationAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);
        Assert.Equal(PreparedScanCommitOutcome.Committed, (await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None)).Outcome);

        await using VulgataDbContext verification =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        PreparedScanAudit[] stored = await verification.PreparedScanAudits.AsNoTracking()
            .OrderBy(audit => audit.RepositoryId).ThenBy(audit => audit.Ordinal).ToArrayAsync();
        PreparedScanAuditDraft[] supplied = fixture.Package.Repositories.SelectMany(repository => repository.Audits)
            .OrderBy(audit => audit.RepositoryId).ThenBy(audit => audit.Ordinal).ToArray();
        Assert.Equal(supplied.Length, stored.Length);
        for (int index = 0; index < stored.Length; index++)
        {
            PreparedScanAudit audit = stored[index];
            PreparedScanAuditDraft reconstructed = PreparedScanAuditFactory.Create(
                audit.RepositoryId, audit.ScanRunId, audit.RelativePath, audit.Outcome, audit.Reason,
                audit.FileHash, audit.RawByteCount, audit.LogicalLineCount,
                audit.FileCodeUnitIdentityFingerprint, audit.PairSelectionOrdinal,
                audit.Ordinal, audit.PreparedScanManifestEntryId);
            Assert.Equal(supplied[index], reconstructed);
            Assert.Equal(audit.Id, reconstructed.Id);
            Assert.Equal(audit.DetailFingerprint, reconstructed.DetailFingerprint);
            Assert.Equal(audit.PreparedScanManifestEntryId, reconstructed.PreparedScanManifestEntryId);
        }
    }

    [Fact]
    public async Task PostgreSql17_RetainedClassificationPersistsInsideThePairTransactionAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanPairPackage classified = AddCanonicalClassification(fixture.Package);
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);

        PreparedScanCommitResult result = await store.CommitPairAsync(
            fixture.Authorization, classified, 0, CancellationToken.None);

        Assert.Equal(PreparedScanCommitOutcome.Committed, result.Outcome);
        Assert.Equal(1, await ScalarAsync<int>(fixture.ConnectionString, """
            SELECT count(*)::integer FROM vulgata."CommunicationKindClassifications"
            WHERE "CatalogRuleIdentity" = 'qualification.rpc-direct';
            """));
        Assert.Equal(1, await ScalarAsync<int>(fixture.ConnectionString, """
            SELECT "TotalConfirmedEvidenceCount" FROM vulgata."PreScanResults"
            WHERE "SchemaVersion" = '2.0' AND "TotalConfirmedEvidenceCount" = 1;
            """));
    }

    [Fact]
    public async Task PostgreSql17_DigestAndCodeUnitFingerprintTamperingCannotReplayAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);
        Assert.Equal(PreparedScanCommitOutcome.Committed, (await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None)).Outcome);

        await ExecuteSqlAsync(fixture.ConnectionString, """
            UPDATE vulgata."CodeUnits"
            SET "FileCodeUnitIdentityFingerprint" = repeat('e', 64)
            WHERE "Id" = (SELECT "Id" FROM vulgata."CodeUnits" ORDER BY "Id" LIMIT 1);
            """);
        PreparedScanCommitResult codeUnitConflict = await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None);
        PreparedFileCodeUnitDraft firstUnit = fixture.Package.Repositories
            .SelectMany(repository => repository.FileCodeUnits)
            .OrderBy(unit => unit.Id)
            .First();
        await ExecuteSqlAsync(fixture.ConnectionString, $"""
            UPDATE vulgata."CodeUnits"
            SET "FileCodeUnitIdentityFingerprint" = '{firstUnit.IdentityFingerprint}'
            WHERE "Id" = '{firstUnit.Id:D}';
            """);

        await ExecuteSqlAsync(fixture.ConnectionString, """
            UPDATE vulgata."PreparedScanAudits"
            SET "DetailFingerprint" = repeat('f', 64)
            WHERE "Id" = (SELECT "Id" FROM vulgata."PreparedScanAudits" ORDER BY "Id" LIMIT 1);
            """);
        PreparedScanCommitResult digestConflict = await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None);

        output.WriteLine("Code Unit fingerprint tamper outcome: {0}", codeUnitConflict.Outcome);
        output.WriteLine("Audit digest tamper outcome: {0}", digestConflict.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Conflict, codeUnitConflict.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Conflict, digestConflict.Outcome);
    }

    [Fact]
    public async Task PostgreSql17_SelectedAuditNullShapeAndAssociationAreDatabaseEnforcedAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);
        Assert.Equal(PreparedScanCommitOutcome.Committed, (await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None)).Outcome);

        PostgresException nullShape = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(
            fixture.ConnectionString,
            """
            UPDATE vulgata."PreparedScanAudits"
            SET "PairSelectionOrdinal" = NULL
            WHERE "Id" = (SELECT "Id" FROM vulgata."PreparedScanAudits" ORDER BY "Id" LIMIT 1);
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, nullShape.SqlState);

        PostgresException association = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(
            fixture.ConnectionString,
            """
            UPDATE vulgata."PreparedScanAudits"
            SET "PreparedScanManifestEntryId" = 'ffffffff-ffff-4fff-8fff-ffffffffffff'
            WHERE "Id" = (SELECT "Id" FROM vulgata."PreparedScanAudits" ORDER BY "Id" LIMIT 1);
            """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, association.SqlState);
    }

    private async Task<PreparedScanPostgreSqlFixture> CreateFixtureAsync()
    {
        string administrator = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN")
            ?? throw new InvalidOperationException(
                "T050 requires VULGATA_TEST_POSTGRES_ADMIN for a disposable PostgreSQL 17 administrator database.");
        PreparedScanPostgreSqlFixture fixture = await PreparedScanPostgreSqlFixture.CreateAsync(administrator);
        output.WriteLine("Owned PostgreSQL 17 database: {0}", fixture.DatabaseName);
        return fixture;
    }

    private static PreparedScanPairPackage AddCanonicalClassification(PreparedScanPairPackage package)
    {
        PreparedRepositoryPackage first = package.Repositories[0];
        const string catalogRule = "qualification.rpc-direct";
        PreparedCanonicalArtifact identity = PreparedScanIdentityBuilders.CreateCommunicationClassification(
            first.ScanRunId,
            CommunicationKind.Rpc,
            ClassificationLevel.Confirmed,
            ConfidenceBand.High,
            EvidenceRule.RpcDirectAnnotation,
            EvidenceProvenance.StructuralAnnotation,
            "src/entry-00.java",
            "src/entry-00.java#qualification-rpc",
            1,
            catalogRule);
        CommunicationKindClassification classification = CommunicationKindClassification.Create(
            first.ScanRunId,
            CommunicationKind.Rpc,
            ClassificationLevel.Confirmed,
            ConfidenceBand.High,
            EvidenceRule.RpcDirectAnnotation,
            EvidenceProvenance.StructuralAnnotation,
            "src/entry-00.java",
            "src/entry-00.java#qualification-rpc",
            1,
            identity.Fingerprint);
        typeof(CommunicationKindClassification).GetProperty(
            nameof(CommunicationKindClassification.CatalogRuleIdentity),
            BindingFlags.Public | BindingFlags.Instance)!.SetValue(classification, catalogRule);

        first = first with
        {
            Classifications = [classification],
            TotalConfirmedEvidenceCount = 1,
        };
        first = ApplyManifestIdentity(first, package.PairSelectionFingerprint);
        PreparedRepositoryPackage second = ApplyManifestIdentity(
            package.Repositories[1], package.PairSelectionFingerprint);
        PreparedRepositoryPackage[] repositories = [first, second];

        MethodInfo pairBuilder = typeof(PreparedScanAtomicReplayTests).GetMethod(
            "CreatePairPackageFingerprint", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The accepted T041 pair fingerprint builder is unavailable.");
        string pairFingerprint = (string)(pairBuilder.Invoke(
            null,
            [package.CoordinationId, package.PairSelectionFingerprint, repositories, package.SelectedCount])
            ?? throw new InvalidOperationException("The pair fingerprint builder returned no fingerprint."));
        return package with
        {
            Repositories = repositories,
            PairPackageFingerprint = pairFingerprint,
        };
    }

    private static PreparedRepositoryPackage ApplyManifestIdentity(
        PreparedRepositoryPackage package,
        string pairSelectionFingerprint)
    {
        PreparedCanonicalArtifact manifest = PreparedScanIdentityBuilders.Create(
            "vulgata/story-12.2/prepared-scan-manifest/1.0",
            [
                new(1, CanonicalEnvelope.Uuid(package.RepositoryId)),
                new(2, CanonicalEnvelope.Uuid(package.ScanRunId)),
                new(3, CanonicalEnvelope.Uuid(package.CoordinationId)),
                new(4, CanonicalEnvelope.String(package.ProfiledRevision)),
                new(5, CanonicalEnvelope.String(package.RequestedRef)),
                new(6, CanonicalEnvelope.String(PreparedScanManifest.CurrentSchemaIdentity)),
                new(7, CanonicalEnvelope.Bytes(Convert.FromHexString(package.SnapshotIdentity))),
                new(8, CanonicalEnvelope.Bytes(Convert.FromHexString(package.RuntimeIdentityHash))),
                new(9, CanonicalEnvelope.Bytes(Convert.FromHexString(
                    PreparedScanIdentityBuilders.CreateCodeGraphProof(package.CodeGraphProof).Fingerprint))),
                new(10, CanonicalEnvelope.Bytes(Convert.FromHexString(
                    PreparedScanIdentityBuilders.CreateJavaReadiness(package.JavaReadiness).Fingerprint))),
                new(11, CanonicalEnvelope.String(package.Catalog.SchemaIdentity)),
                new(12, CanonicalEnvelope.Bytes(Convert.FromHexString(package.Catalog.ResourceHash))),
                new(13, CanonicalEnvelope.Bytes(Convert.FromHexString(package.PreparedScanContextIdentityHash))),
                new(14, CanonicalEnvelope.Bytes(Convert.FromHexString(package.ContextContentHash))),
                new(15, CanonicalEnvelope.UInt64((ulong)package.FileCodeUnits.Count)),
                new(16, CanonicalEnvelope.UInt64((ulong)package.FileCodeUnits.Count)),
                new(17, CanonicalEnvelope.UInt64(0)),
                new(18, CanonicalEnvelope.UInt64(0)),
                new(19, CanonicalEnvelope.UInt64((ulong)package.FileCodeUnits.Count)),
                new(20, CanonicalEnvelope.UInt64((ulong)package.TotalConfirmedEvidenceCount)),
                new(21, CanonicalEnvelope.UInt64((ulong)package.Classifications.Count)),
                new(22, CanonicalEnvelope.Boolean(package.EvidenceTruncated)),
                new(23, CanonicalEnvelope.Bytes(Convert.FromHexString(pairSelectionFingerprint))),
                new(24, CanonicalEnvelope.List(package.FileCodeUnits.Select(unit => CanonicalEnvelope.Object([
                    new(1, CanonicalEnvelope.Bytes(Convert.FromHexString(unit.IdentityFingerprint))),
                    new(2, CanonicalEnvelope.Int64(unit.DirectBusinessScore)),
                    new(3, CanonicalEnvelope.Int64(unit.FinalBusinessScore)),
                    new(4, CanonicalEnvelope.Enum(unit.SelectionReason.ToString())),
                    new(5, CanonicalEnvelope.UInt64((ulong)unit.ManifestOrdinal)),
                    new(6, CanonicalEnvelope.UInt64((ulong)unit.PairSelectionOrdinal)),
                ])))),
                new(25, CanonicalEnvelope.List(package.Audits.OrderBy(audit => audit.Ordinal).Select(audit =>
                    CanonicalEnvelope.Object([
                        new(1, CanonicalEnvelope.UInt64((ulong)audit.Ordinal)),
                        new(2, CanonicalEnvelope.Bytes(Convert.FromHexString(audit.DetailFingerprint))),
                    ])))),
                new(26, CanonicalEnvelope.List(package.Classifications.Select(classification =>
                    CanonicalEnvelope.Bytes(Convert.FromHexString(classification.Fingerprint))))),
                new(27, CanonicalEnvelope.Enum(PreparedScanManifestApprovalState.Approved.ToString())),
            ]);
        return package with { ManifestFingerprint = manifest.Fingerprint };
    }

    private static async Task InstallFailingTriggerAsync(
        string connectionString,
        string table,
        string operation,
        string boundary)
    {
        string function = "t050_fail_" + boundary.Replace('-', '_');
        await ExecuteSqlAsync(connectionString, $"""
            CREATE OR REPLACE FUNCTION {function}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'T050 forced boundary: {boundary}'; END $$;
            CREATE TRIGGER {function}_trigger BEFORE {operation} ON {table}
            FOR EACH ROW EXECUTE FUNCTION {function}();
            """);
    }

    private static async Task InstallSleepingTriggerAsync(
        string connectionString,
        string table,
        string operation,
        string boundary)
    {
        string function = "t050_sleep_" + boundary.Replace('-', '_');
        await ExecuteSqlAsync(connectionString, $"""
            CREATE OR REPLACE FUNCTION {function}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_sleep(5); RETURN NEW; END $$;
            CREATE TRIGGER {function}_trigger BEFORE {operation} ON {table}
            FOR EACH ROW EXECUTE FUNCTION {function}();
            """);
    }

    private static async Task AssertCompletePairAsync(
        string connectionString,
        PreparedScanPairPackage package)
    {
        DurableCounts counts = await ReadCountsAsync(connectionString);
        Assert.Equal(2, counts.Manifests);
        Assert.Equal(package.SelectedCount, counts.Entries);
        Assert.Equal(package.SelectedCount, counts.CodeUnits);
        Assert.Equal(package.Repositories.Sum(repository => repository.Audits.Count), counts.Audits);
        Assert.Equal(2, counts.ContextSnapshots);
        Assert.Equal(2, counts.PreparedResults);
    }

    private static async Task AssertNoDispatchablePreparedResultAsync(string connectionString)
    {
        Assert.Equal(0, await ScalarAsync<int>(connectionString, """
            SELECT count(*)::integer FROM vulgata."PreScanResults"
            WHERE "SchemaVersion" = '2.0' OR "HandoffSchemaIdentity" IS NOT NULL;
            """));
    }

    private static async Task<DurableCounts> ReadCountsAsync(string connectionString) => new(
        await ScalarAsync<int>(connectionString, "SELECT count(*)::integer FROM vulgata.\"PreparedScanManifests\";"),
        await ScalarAsync<int>(connectionString, "SELECT count(*)::integer FROM vulgata.\"PreparedScanManifestEntries\";"),
        await ScalarAsync<int>(connectionString, "SELECT count(*)::integer FROM vulgata.\"CodeUnits\";"),
        await ScalarAsync<int>(connectionString, "SELECT count(*)::integer FROM vulgata.\"PreparedScanAudits\";"),
        await ScalarAsync<int>(connectionString, "SELECT count(*)::integer FROM vulgata.\"ScanContextSnapshots\";"),
        await ScalarAsync<int>(connectionString, "SELECT count(*)::integer FROM vulgata.\"PreScanResults\" WHERE \"SchemaVersion\" = '2.0';"));

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)Convert.ChangeType(await command.ExecuteScalarAsync() ?? default(T)!, typeof(T));
    }

    private static async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record DurableCounts(
        int Manifests,
        int Entries,
        int CodeUnits,
        int Audits,
        int ContextSnapshots,
        int PreparedResults)
    {
        internal static DurableCounts PrincipalOnly => new(0, 0, 0, 0, 0, 0);
    }
}
