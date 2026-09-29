using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Git;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// Story 12.2 T041 executable RED slice. These tests enter through the public
/// pair-commit port and verify durable visibility in an isolated EF database.
/// PostgreSQL-specific migration/concurrency qualification remains T050.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class PreparedScanAtomicReplayTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedRepositoryPackage_CarriesContextSourceValuesAndFileLanguage()
    {
        PropertyInfo[] package = typeof(PreparedRepositoryPackage)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(package, property => property.Name is "GlobalContext" or "ContextGlobal");
        Assert.Contains(package, property => property.Name is "SystemContext" or "ContextSystem");
        Assert.Contains(package, property => property.Name is "RepositoryContext" or "ContextRepository");

        PropertyInfo[] unit = typeof(PreparedFileCodeUnitDraft)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(unit, property => property.Name == "Language");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PreparedStore_InvalidPair_IsRejectedAndLeavesNoPreparedRowsAsync()
    {
        await using IsolatedPreparedDatabase database = await IsolatedPreparedDatabase.CreateAsync();
        IPreparedScanStore store = CreateStore(database.Context);

        PreparedScanCommitResult result = await store.CommitPairAsync(
            AuthorizationScope(database.Package, database.Principals.SystemId),
            EmptyPairPackage(),
            expectedCoordinationVersion: 0,
            CancellationToken.None);

        Assert.Equal(PreparedScanCommitOutcome.Rejected, result.Outcome);
        await AssertNoPreparedRowsAsync(database.Context);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PreparedStore_CancelledBeforeCommit_ReturnsCancelledAndRollsBackAllRowsAsync()
    {
        await using IsolatedPreparedDatabase database = await IsolatedPreparedDatabase.CreateAsync();
        IPreparedScanStore store = CreateStore(database.Context);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        PreparedScanCommitResult result = await store.CommitPairAsync(
            AuthorizationScope(database.Package, database.Principals.SystemId),
            database.Package,
            expectedCoordinationVersion: 0,
            cancellation.Token);

        Assert.Equal(PreparedScanCommitOutcome.Cancelled, result.Outcome);
        await AssertNoPreparedRowsAsync(database.Context);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PreparedStore_ExactReplayAndChangedAuditPreimageUsePublicCommitSeamAsync()
    {
        await using IsolatedPreparedDatabase database = await IsolatedPreparedDatabase.CreateAsync();
        PreparedScanPairPackage package = database.Package;
        Assert.NotEqual(
            package.Repositories[0].ManifestFingerprint,
            package.Repositories[1].ManifestFingerprint);
        Assert.All(
            package.Repositories,
            repository =>
            {
                Assert.Matches("^[0-9a-f]{64}$", repository.SnapshotIdentity);
                Assert.Matches("^[0-9a-f]{64}$", repository.PreparedScanContextIdentityHash);
                Assert.Matches("^[0-9a-f]{64}$", repository.ContextContentHash);
                Assert.Equal("jdt-language-server-1.61.0-202608141332", repository.JavaReadiness.JdtLsRelease);
            });
        Assert.Equal("global test context", database.Principals.FirstContextSnapshot!.GlobalContext);
        Assert.Equal("prepared system context", database.Principals.FirstContextSnapshot.SystemContext);
        IPreparedScanStore store = CreateStore(database.Context);

        PreparedScanCommitResult first = await store.CommitPairAsync(
            AuthorizationScope(package, database.Principals.SystemId), package, expectedCoordinationVersion: 0, CancellationToken.None);
        DurablePreparedRows committedRows = await CaptureDurableRowsAsync(database.Context);
        PreparedScanCommitResult replay = await store.CommitPairAsync(
            AuthorizationScope(package, database.Principals.SystemId), package, expectedCoordinationVersion: 0, CancellationToken.None);
        PreparedScanCommitResult changed = await store.CommitPairAsync(
            AuthorizationScope(package, database.Principals.SystemId),
            package with { PairSelectionFingerprint = new string('b', 64) },
            expectedCoordinationVersion: 0,
            CancellationToken.None);

        Assert.Equal(PreparedScanCommitOutcome.Committed, first.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Replay, replay.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Conflict, changed.Outcome);
        AssertDurablePairCounts(committedRows, package);
        AssertDurableRowsEqual(committedRows, await CaptureDurableRowsAsync(database.Context));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PreparedStore_AuditPreimageMutationCannotReplayAndCannotPartiallyPersistAsync()
    {
        await using IsolatedPreparedDatabase database = await IsolatedPreparedDatabase.CreateAsync();
        IPreparedScanStore store = CreateStore(database.Context);
        PreparedScanPairPackage package = database.Package;
        PreparedRepositoryPackage firstRepository = package.Repositories[0];
        PreparedScanAuditDraft audit = firstRepository.Audits[0];

        PropertyInfo[] auditProperties = typeof(PreparedScanAuditDraft)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.All(
            ["RepositoryId", "ScanRunId", "RelativePath", "Outcome", "Reason", "FileHash",
                "RawByteCount", "LogicalLineCount", "FileCodeUnitIdentityFingerprint",
                "PairSelectionOrdinal"],
            name => Assert.Contains(auditProperties, property => property.Name == name));

        PreparedScanCommitResult first = await store.CommitPairAsync(
            AuthorizationScope(package, database.Principals.SystemId), package, expectedCoordinationVersion: 0, CancellationToken.None);
        DurablePreparedRows committedRows = await CaptureDurableRowsAsync(database.Context);
        PreparedRepositoryPackage mutatedRepository = firstRepository with
        {
            Audits = firstRepository.Audits
                .Select((candidate, index) => index == 0
                    ? candidate with { FileHash = new string('f', 64) }
                    : candidate)
                .ToArray(),
        };
        PreparedScanPairPackage mutated = package with
        {
            Repositories = [mutatedRepository, package.Repositories[1]],
        };
        PreparedScanCommitResult result = await store.CommitPairAsync(
            AuthorizationScope(package, database.Principals.SystemId),
            mutated,
            expectedCoordinationVersion: 0,
            CancellationToken.None);

        Assert.Equal(PreparedScanCommitOutcome.Committed, first.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Conflict, result.Outcome);
        Assert.Equal("src/entry-00.java", audit.RelativePath);
        AssertDurableRowsEqual(committedRows, await CaptureDurableRowsAsync(database.Context));
    }

    private static PreparedScanAuthorizationScope AuthorizationScope(
        PreparedScanPairPackage package,
        Guid systemId) =>
        new(
            Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
            systemId,
            package.Repositories.Select(repository => repository.RepositoryId).ToArray());

    private static PreparedScanPairPackage EmptyPairPackage() =>
        new(
            PreparedScanTestHost.CoordinationId,
            new string('a', 64),
            new string('c', 64),
            [],
            0);

    private static PreparedScanPairPackage CompletePairPackage(PreparedPrincipalSet principals)
    {
        PreparedRepositoryPackage first = CreateRepositoryPackage(
            principals.FirstRepositoryId,
            principals.FirstScanRunId,
            principals.CoordinationId,
            0,
            principals.FirstContextSnapshot!);
        PreparedRepositoryPackage second = CreateRepositoryPackage(
            principals.SecondRepositoryId,
            principals.SecondScanRunId,
            principals.CoordinationId,
            25,
            principals.SecondContextSnapshot!);
        string pairSelectionFingerprint = CreatePairSelectionFingerprint(
            principals.CoordinationId,
            [first, second]);
        first = ApplyManifestIdentity(first, pairSelectionFingerprint);
        second = ApplyManifestIdentity(second, pairSelectionFingerprint);
        string pairPackageFingerprint = CreatePairPackageFingerprint(
            principals.CoordinationId,
            pairSelectionFingerprint,
            [first, second],
            first.FileCodeUnits.Count + second.FileCodeUnits.Count);
        return new(
            principals.CoordinationId,
            pairSelectionFingerprint,
            pairPackageFingerprint,
            [first, second],
            first.FileCodeUnits.Count + second.FileCodeUnits.Count);
    }

    private static PreparedRepositoryPackage CreateRepositoryPackage(
        Guid repositoryId,
        Guid scanRunId,
        Guid coordinationId,
        int pairOrdinalOffset,
        ScanContextSnapshot contextSnapshot)
    {
        const string revision = "0123456789abcdef0123456789abcdef01234567";
        string snapshot = Sha256Hex($"snapshot-binding:{repositoryId:D}:{revision}");
        string runtimeHash = Sha256Hex($"codegraph-runtime:{repositoryId:D}");
        string contextHash = contextSnapshot.ContentHash;
        string contextIdentity = contextSnapshot.PreparedScanContextIdentityHash ??
            throw new InvalidOperationException("The prepared test context must have a stable identity hash.");
        List<PreparedFileCodeUnitDraft> units = [];
        List<PreparedScanAuditDraft> audits = [];
        for (int index = 0; index < 25; index++)
        {
            int pairOrdinal = pairOrdinalOffset + index + 1;
            string path = $"src/entry-{index:D2}.java";
            string fileHash = Sha256Hex($"file:{repositoryId:D}:{path}");
            string sourceHash = Sha256Hex($"source:{repositoryId:D}:{path}");
            PreparedFileCodeUnitInput input = new(
                repositoryId,
                scanRunId,
                revision,
                path,
                fileHash,
                0,
                40,
                1,
                4,
                1,
                1,
                sourceHash,
                [PreparedBusinessEvidence.EndpointOrApplicationEntry]);
            PreparedFileCodeUnitConstruction construction = PreparedScanPlanner.BuildFileCodeUnit(input, "java");
            PreparedFileCodeUnitDraft unit = construction.Draft with
            {
                ManifestOrdinal = index + 1,
                PairSelectionOrdinal = pairOrdinal,
            };
            units.Add(unit);
            audits.Add(PreparedScanAuditFactory.Create(
                repositoryId,
                scanRunId,
                path,
                PreparedScanAuditOutcome.Selected,
                PreparedScanAuditReason.SelectedForPreparation,
                fileHash,
                40,
                4,
                unit.IdentityFingerprint,
                pairOrdinal,
                index + 1,
                unit.Id));
        }

        CodeGraphCompletenessProof graph = new(
            repositoryId,
            scanRunId,
            revision,
            snapshot,
            Sha256Hex($"codegraph-snapshot-root:{repositoryId:D}:{revision}"),
            runtimeHash,
            $"index-{repositoryId:D}",
            "codegraph-completeness-proof/1.0",
            true,
            CodeGraphIndexState.Complete,
            0,
            0,
            0,
            false,
            false,
            0,
            25,
            $"project-{repositoryId:D}",
            $"index-{repositoryId:D}",
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            Sha256Hex($"codegraph-proof-observation:{repositoryId:D}"),
            0);
        string graphIdentityHash = PreparedScanIdentityBuilders.CreateCodeGraphProof(graph).Fingerprint;
        graph = graph with { IdentityHash = graphIdentityHash };
        JavaReadinessProof java = new(
            repositoryId,
            scanRunId,
            revision,
            snapshot,
            runtimeHash,
            Sha256Hex($"jdtls-workspace:{repositoryId:D}:{revision}"),
            "java-readiness/1.0",
            JavaReadinessOutcome.Ready,
            "jdt-language-server-1.61.0-202608141332",
            Sha256Hex("jdt-language-server-1.61.0-202608141332:archive"),
            21,
            "2.25.29",
            AnalysisTransport.Stdio,
            true,
            "src/entry-00.java",
            true,
            true,
            JavaImportState.Usable,
            25,
            true,
            true,
            true,
            true,
            true,
            Sha256Hex($"java-readiness-observation:{repositoryId:D}"),
            0);
        string javaIdentityHash = PreparedScanIdentityBuilders.CreateJavaReadiness(java).Fingerprint;
        java = java with { IdentityHash = javaIdentityHash };
        CommunicationPatternCatalog catalog = new(
            "communication-pattern-catalog/1.0",
            "story-12-2-test-catalog",
            "1.0",
            Sha256Hex("story-12-2-test-catalog:1.0"),
            []);
        return new(
            repositoryId,
            scanRunId,
            coordinationId,
            revision,
            $"refs/tags/test-{repositoryId:D}",
            snapshot,
            runtimeHash,
            graph,
            java,
            catalog,
            contextSnapshot.Id,
            contextIdentity,
            contextHash,
            new string('0', 64),
            units,
            audits,
            [],
            0,
            0,
            0,
            false,
            PreparedScanTestHost.FixedUtcNow,
            PreparedScanTestHost.FixedUtcNow);
    }

    private static PreparedRepositoryPackage ApplyManifestIdentity(
        PreparedRepositoryPackage package,
        string pairSelectionFingerprint)
    {
        string codeGraphIdentity = PreparedScanIdentityBuilders.CreateCodeGraphProof(package.CodeGraphProof).Fingerprint;
        string javaIdentity = PreparedScanIdentityBuilders.CreateJavaReadiness(package.JavaReadiness).Fingerprint;
        PreparedCanonicalArtifact manifest = PreparedScanIdentityBuilders.Create(
            "vulgata/story-12.2/prepared-scan-manifest/1.0",
            [
                new(1, CanonicalEnvelope.Uuid(package.RepositoryId)),
                new(2, CanonicalEnvelope.Uuid(package.ScanRunId)),
                new(3, CanonicalEnvelope.Uuid(package.CoordinationId)),
                new(4, CanonicalEnvelope.String(package.ProfiledRevision)),
                new(5, CanonicalEnvelope.String(package.RequestedRef)),
                new(6, CanonicalEnvelope.String(PreparedScanManifest.CurrentSchemaIdentity)),
                new(7, CanonicalEnvelope.Bytes(HexBytes(package.SnapshotIdentity))),
                new(8, CanonicalEnvelope.Bytes(HexBytes(package.RuntimeIdentityHash))),
                new(9, CanonicalEnvelope.Bytes(HexBytes(codeGraphIdentity))),
                new(10, CanonicalEnvelope.Bytes(HexBytes(javaIdentity))),
                new(11, CanonicalEnvelope.String(package.Catalog.SchemaIdentity)),
                new(12, CanonicalEnvelope.Bytes(HexBytes(package.Catalog.ResourceHash))),
                new(13, CanonicalEnvelope.Bytes(HexBytes(package.PreparedScanContextIdentityHash))),
                new(14, CanonicalEnvelope.Bytes(HexBytes(package.ContextContentHash))),
                new(15, CanonicalEnvelope.UInt64((ulong)package.FileCodeUnits.Count)),
                new(16, CanonicalEnvelope.UInt64((ulong)package.FileCodeUnits.Count)),
                new(17, CanonicalEnvelope.UInt64(0)),
                new(18, CanonicalEnvelope.UInt64(0)),
                new(19, CanonicalEnvelope.UInt64((ulong)package.FileCodeUnits.Count)),
                new(20, CanonicalEnvelope.UInt64((ulong)package.TotalConfirmedEvidenceCount)),
                new(21, CanonicalEnvelope.UInt64((ulong)package.Classifications.Count)),
                new(22, CanonicalEnvelope.Boolean(package.EvidenceTruncated)),
                new(23, CanonicalEnvelope.Bytes(HexBytes(pairSelectionFingerprint))),
                new(24, CanonicalEnvelope.List(package.FileCodeUnits.Select(unit => CanonicalEnvelope.Object([
                    new(1, CanonicalEnvelope.Bytes(HexBytes(unit.IdentityFingerprint))),
                    new(2, CanonicalEnvelope.Int64(unit.DirectBusinessScore)),
                    new(3, CanonicalEnvelope.Int64(unit.FinalBusinessScore)),
                    new(4, CanonicalEnvelope.Enum(unit.SelectionReason.ToString())),
                    new(5, CanonicalEnvelope.UInt64((ulong)unit.ManifestOrdinal)),
                    new(6, CanonicalEnvelope.UInt64((ulong)unit.PairSelectionOrdinal)),
                ])))),
                new(25, CanonicalEnvelope.List(package.Audits.OrderBy(audit => audit.Ordinal).Select(audit => CanonicalEnvelope.Object([
                    new(1, CanonicalEnvelope.UInt64((ulong)audit.Ordinal)),
                    new(2, CanonicalEnvelope.Bytes(HexBytes(audit.DetailFingerprint))),
                ])))),
                new(26, CanonicalEnvelope.List([])),
                new(27, CanonicalEnvelope.Enum(PreparedScanManifestApprovalState.Approved.ToString())),
            ]);
        return package with { ManifestFingerprint = manifest.Fingerprint };
    }

    private static string CreatePairSelectionFingerprint(
        Guid coordinationId,
        IReadOnlyList<PreparedRepositoryPackage> packages)
    {
        PreparedCanonicalArtifact artifact = PreparedScanIdentityBuilders.Create(
            "vulgata/story-12.2/pair-selection/1.0",
            [
                new(1, CanonicalEnvelope.Uuid(coordinationId)),
                new(2, CanonicalEnvelope.List(packages
                    .SelectMany(package => package.FileCodeUnits)
                    .OrderBy(unit => unit.PairSelectionOrdinal)
                    .Select(unit => CanonicalEnvelope.Object([
                        new(1, CanonicalEnvelope.Uuid(unit.RepositoryId)),
                        new(2, CanonicalEnvelope.Bytes(HexBytes(unit.IdentityFingerprint))),
                        new(3, CanonicalEnvelope.Int64(unit.FinalBusinessScore)),
                        new(4, CanonicalEnvelope.Enum(unit.SelectionReason.ToString())),
                        new(5, CanonicalEnvelope.UInt64((ulong)unit.ManifestOrdinal)),
                        new(6, CanonicalEnvelope.UInt64((ulong)unit.PairSelectionOrdinal)),
                    ])))),
            ]);
        return artifact.Fingerprint;
    }

    private static string CreatePairPackageFingerprint(
        Guid coordinationId,
        string pairSelectionFingerprint,
        IReadOnlyList<PreparedRepositoryPackage> packages,
        int selectedCount)
    {
        PreparedCanonicalArtifact artifact = PreparedScanIdentityBuilders.Create(
            "vulgata/story-12.2/prepared-scan-pair-package/1.0",
            [
                new(1, CanonicalEnvelope.Uuid(coordinationId)),
                new(2, CanonicalEnvelope.Bytes(HexBytes(pairSelectionFingerprint))),
                new(3, CanonicalEnvelope.List(packages
                    .OrderBy(package => package.RepositoryId.ToString("D"), StringComparer.Ordinal)
                    .Select(package => CanonicalEnvelope.Object([
                        new(1, CanonicalEnvelope.Uuid(package.RepositoryId)),
                        new(2, CanonicalEnvelope.Bytes(HexBytes(package.ManifestFingerprint))),
                    ])))),
                new(4, CanonicalEnvelope.UInt64((ulong)selectedCount)),
                new(5, CanonicalEnvelope.String("prepared-scan-pair-package/1.0")),
            ]);
        return artifact.Fingerprint;
    }

    private static byte[] HexBytes(string hash) => Convert.FromHexString(hash);

    private static string Sha256Hex(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private static IPreparedScanStore CreateStore(VulgataDbContext context)
    {
        Type? storeType = typeof(PreparedScanCandidateEnumerator).Assembly.GetType(
            "Vulgata.Infrastructure.Data.PreparedScanStore",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(storeType);
        Assert.True(typeof(IPreparedScanStore).IsAssignableFrom(storeType));

        ConstructorInfo? constructor = storeType!.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(candidate =>
                candidate.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType == typeof(VulgataDbContext));
        Assert.True(
            constructor is not null,
            "PreparedScanStore must expose a public VulgataDbContext constructor for the infrastructure seam.");
        return Assert.IsAssignableFrom<IPreparedScanStore>(constructor!.Invoke([context]));
    }

    private static async Task AssertNoPreparedRowsAsync(VulgataDbContext context)
    {
        Assert.Equal(0, await context.PreparedScanManifests.CountAsync());
        Assert.Equal(0, await context.PreparedScanManifestEntries.CountAsync());
        Assert.Equal(0, await context.PreparedScanAudits.CountAsync());
        Assert.Equal(2, await context.PreScanResults.CountAsync());
        Assert.Equal(0, await context.ScanContextSnapshots.CountAsync());
        Assert.Equal(0, await context.CodeUnits.CountAsync());
    }

    private static async Task<DurablePreparedRows> CaptureDurableRowsAsync(VulgataDbContext context)
    {
        return new(
            await context.PreparedScanManifests.CountAsync(),
            await context.PreparedScanManifestEntries.CountAsync(),
            await context.CodeUnits.CountAsync(),
            await context.PreparedScanAudits.CountAsync(),
            await context.PreScanResults.CountAsync(),
            await context.ScanContextSnapshots.CountAsync(),
            (await context.PreparedScanManifests.Select(manifest => manifest.Id).ToListAsync())
                .OrderBy(id => id).ToArray(),
            (await context.PreparedScanManifestEntries.Select(entry => entry.FileCodeUnitId).ToListAsync())
                .OrderBy(id => id).ToArray(),
            (await context.CodeUnits.Select(unit => unit.Id).ToListAsync())
                .OrderBy(id => id).ToArray(),
            (await context.PreparedScanAudits.Select(audit => audit.Id).ToListAsync())
                .OrderBy(id => id).ToArray(),
            (await context.PreScanResults.Select(result => result.Id).ToListAsync())
                .OrderBy(id => id).ToArray(),
            (await context.ScanContextSnapshots.Select(snapshot => snapshot.Id).ToListAsync())
                .OrderBy(id => id).ToArray());
    }

    private static void AssertDurablePairCounts(
        DurablePreparedRows rows,
        PreparedScanPairPackage package)
    {
        Assert.Equal(package.Repositories.Count, rows.ManifestCount);
        Assert.Equal(package.SelectedCount, rows.ManifestEntryCount);
        Assert.Equal(package.SelectedCount, rows.CodeUnitCount);
        Assert.Equal(package.Repositories.Sum(repository => repository.Audits.Count), rows.AuditCount);
        Assert.Equal(package.Repositories.Count, rows.PreScanResultCount);
        Assert.Equal(package.Repositories.Count, rows.ContextSnapshotCount);
    }

    private static void AssertDurableRowsEqual(
        DurablePreparedRows expected,
        DurablePreparedRows actual)
    {
        Assert.Equal(expected.ManifestCount, actual.ManifestCount);
        Assert.Equal(expected.ManifestEntryCount, actual.ManifestEntryCount);
        Assert.Equal(expected.CodeUnitCount, actual.CodeUnitCount);
        Assert.Equal(expected.AuditCount, actual.AuditCount);
        Assert.Equal(expected.PreScanResultCount, actual.PreScanResultCount);
        Assert.Equal(expected.ContextSnapshotCount, actual.ContextSnapshotCount);
        Assert.Equal(expected.ManifestIds, actual.ManifestIds);
        Assert.Equal(expected.ManifestEntryIds, actual.ManifestEntryIds);
        Assert.Equal(expected.CodeUnitIds, actual.CodeUnitIds);
        Assert.Equal(expected.AuditIds, actual.AuditIds);
        Assert.Equal(expected.PreScanResultIds, actual.PreScanResultIds);
        Assert.Equal(expected.ContextSnapshotIds, actual.ContextSnapshotIds);
    }

    private sealed record DurablePreparedRows(
        int ManifestCount,
        int ManifestEntryCount,
        int CodeUnitCount,
        int AuditCount,
        int PreScanResultCount,
        int ContextSnapshotCount,
        IReadOnlyList<Guid> ManifestIds,
        IReadOnlyList<Guid> ManifestEntryIds,
        IReadOnlyList<Guid> CodeUnitIds,
        IReadOnlyList<Guid> AuditIds,
        IReadOnlyList<Guid> PreScanResultIds,
        IReadOnlyList<Guid> ContextSnapshotIds);

    private sealed record PreparedPrincipalSet(
        Guid SystemId,
        Guid FirstRepositoryId,
        Guid SecondRepositoryId,
        Guid FirstScanRunId,
        Guid SecondScanRunId,
        Guid CoordinationId,
        ScanContextSnapshot? FirstContextSnapshot = null,
        ScanContextSnapshot? SecondContextSnapshot = null);

    private sealed record SeededPreparedData(
        PreparedPrincipalSet Principals,
        PreparedScanPairPackage Package);

    private sealed class IsolatedPreparedDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private IsolatedPreparedDatabase(SqliteConnection connection, VulgataDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public VulgataDbContext Context { get; }
        public PreparedPrincipalSet Principals { get; private set; } = null!;
        public PreparedScanPairPackage Package { get; private set; } = null!;

        public static async Task<IsolatedPreparedDatabase> CreateAsync()
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite(connection)
                .Options;
            VulgataDbContext context = new(options);
            await context.Database.EnsureCreatedAsync();
            IsolatedPreparedDatabase database = new(connection, context);
            SeededPreparedData seeded = await SeedPrincipalsAndCreatePackageAsync(context);
            database.Principals = seeded.Principals;
            database.Package = seeded.Package;
            return database;
        }

        private static async Task<SeededPreparedData> SeedPrincipalsAndCreatePackageAsync(
            VulgataDbContext context)
        {
            DateTimeOffset now = PreparedScanTestHost.FixedUtcNow;
            const string revision = "0123456789abcdef0123456789abcdef01234567";
            SystemEntity system = new("Prepared Story 12.2", "test system", "global test context", now);
            Repository firstRepository = system.AddRepository(
                "prepared-one", "https://example.invalid/prepared-one.git", null, "repository one context", now);
            Repository secondRepository = system.AddRepository(
                "prepared-two", "https://example.invalid/prepared-two.git", null, "repository two context", now);
            ScanRun firstRun = ScanRun.Start(firstRepository.Id, 1_800, now);
            firstRun.BindProfiledRevision(revision);
            ScanRun secondRun = ScanRun.Start(secondRepository.Id, 1_800, now);
            secondRun.BindProfiledRevision(revision);
            ScanContextSnapshot firstContextSnapshot = ScanContextSnapshot.CapturePrepared(
                firstRun,
                system.Context,
                "prepared system context",
                firstRepository.Context,
                now);
            ScanContextSnapshot secondContextSnapshot = ScanContextSnapshot.CapturePrepared(
                secondRun,
                system.Context,
                "prepared system context",
                secondRepository.Context,
                now);
            PreScanResult firstPreScan = PreScanResult.CreateApproved(
                firstRepository.Id,
                firstRun.Id,
                revision,
                now,
                new string('a', 64),
                "预扫描已批准——继续进入顺序演示运行器",
                ConfirmedCommunicationKinds.None,
                0,
                0,
                0,
                false,
                []);
            PreScanResult secondPreScan = PreScanResult.CreateApproved(
                secondRepository.Id,
                secondRun.Id,
                revision,
                now,
                new string('b', 64),
                "预扫描已批准——继续进入顺序演示运行器",
                ConfirmedCommunicationKinds.None,
                0,
                0,
                0,
                false,
                []);
            ScanExecution firstExecution = ScanExecution.Start(
                Guid.NewGuid(), firstRepository.Id, system.Id, firstRun.Id, firstPreScan.Id, revision, now);
            ScanExecution secondExecution = ScanExecution.Start(
                Guid.NewGuid(), secondRepository.Id, system.Id, secondRun.Id, secondPreScan.Id, revision, now);
            TwoRepositoryScanCoordination coordination = TwoRepositoryScanCoordination.Create(
                firstExecution,
                secondExecution,
                now);
            context.Systems.Add(system);
            context.ScanRuns.AddRange(firstRun, secondRun);
            context.PreScanResults.AddRange(firstPreScan, secondPreScan);
            context.ScanExecutions.AddRange(firstExecution, secondExecution);
            context.TwoRepositoryScanCoordinations.Add(coordination);
            await context.SaveChangesAsync();
            PreparedPrincipalSet principals = new(
                system.Id,
                firstRepository.Id,
                secondRepository.Id,
                firstRun.Id,
                secondRun.Id,
                coordination.Id,
                firstContextSnapshot,
                secondContextSnapshot);
            return new(principals, CompletePairPackage(principals));
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
