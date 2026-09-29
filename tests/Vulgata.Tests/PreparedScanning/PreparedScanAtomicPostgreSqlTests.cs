using Vulgata.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CommunicationDetection;
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

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
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

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PostgreSql17_TamperedStoredCodeGraphProofPreimageCannotMasqueradeAsReplayAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);
        Assert.Equal(PreparedScanCommitOutcome.Committed, (await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None)).Outcome);
        DurableCounts before = await ReadCountsAsync(fixture.ConnectionString);

        Guid manifestId = PreparedScanIdentityBuilders.CreateDeterministicIdentity(
            "PreparedScanManifest", fixture.Package.Repositories[0].ManifestFingerprint);
        TypedProofStorageTarget? target = await FindTypedProofStorageTargetAsync(
            fixture.ConnectionString, "logicalindexbinding");
        Assert.True(
            target is not null,
            "Durable CodeGraph typed proof preimage field LogicalIndexBinding is absent; opaque proof hashes are insufficient.");
        await ExecuteSqlAsync(fixture.ConnectionString, target!.CreateMutationSql(manifestId));
        Assert.Equal(1, await ScalarAsync<int>(
            fixture.ConnectionString, target.CreateRowCountSql(manifestId)));

        PreparedScanCommitResult result = await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None);

        output.WriteLine("Tampered stored CodeGraph proof preimage outcome: {0}", result.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Conflict, result.Outcome);
        Assert.Equal(before, await ReadCountsAsync(fixture.ConnectionString));
        Assert.Equal(1, await ScalarAsync<int>(
            fixture.ConnectionString, target.CreateRowCountSql(manifestId)));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PostgreSql17_TamperedStoredJavaReadinessProofPreimageCannotMasqueradeAsReplayAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);
        Assert.Equal(PreparedScanCommitOutcome.Committed, (await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None)).Outcome);
        DurableCounts before = await ReadCountsAsync(fixture.ConnectionString);

        Guid manifestId = PreparedScanIdentityBuilders.CreateDeterministicIdentity(
            "PreparedScanManifest", fixture.Package.Repositories[0].ManifestFingerprint);
        TypedProofStorageTarget? target = await FindTypedProofStorageTargetAsync(
            fixture.ConnectionString, "workspacebindinghash");
        Assert.True(
            target is not null,
            "Durable Java typed proof preimage field WorkspaceBindingHash is absent; opaque proof hashes are insufficient.");
        await ExecuteSqlAsync(fixture.ConnectionString, target!.CreateMutationSql(manifestId));
        Assert.Equal(1, await ScalarAsync<int>(
            fixture.ConnectionString, target.CreateRowCountSql(manifestId)));

        PreparedScanCommitResult result = await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None);

        output.WriteLine("Tampered stored Java readiness proof preimage outcome: {0}", result.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Conflict, result.Outcome);
        Assert.Equal(before, await ReadCountsAsync(fixture.ConnectionString));
        Assert.Equal(1, await ScalarAsync<int>(
            fixture.ConnectionString, target.CreateRowCountSql(manifestId)));
    }

    public static TheoryData<string> OmittedManifestScalarColumns => new()
    {
        "CandidateCount",
        "EligibleCount",
        "FilteredCount",
        "SkippedCount",
    };

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PostgreSql17_TamperedStoredManifestSchemaIdentityCannotReplayAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);
        Assert.Equal(PreparedScanCommitOutcome.Committed, (await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None)).Outcome);
        DurableCounts before = await ReadCountsAsync(fixture.ConnectionString);
        string manifestId = PreparedScanIdentityBuilders.CreateDeterministicIdentity(
            "PreparedScanManifest", fixture.Package.Repositories[0].ManifestFingerprint).ToString("D");

        await ExecuteSqlAsync(fixture.ConnectionString, $"""
            ALTER TABLE vulgata."PreparedScanManifests"
                DROP CONSTRAINT "CK_PreparedScanManifests_SchemaIdentity";
            UPDATE vulgata."PreparedScanManifests"
            SET "SchemaIdentity" = 'prepared-scan-manifest/tampered'
            WHERE "Id" = '{manifestId}';
            """);

        PreparedScanCommitResult result = await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None);

        output.WriteLine("Tampered stored manifest schema identity outcome: {0}", result.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Conflict, result.Outcome);
        Assert.Equal(before, await ReadCountsAsync(fixture.ConnectionString));
        Assert.Equal("prepared-scan-manifest/tampered", await ScalarAsync<string>(
            fixture.ConnectionString,
            $"SELECT \"SchemaIdentity\" FROM vulgata.\"PreparedScanManifests\" WHERE \"Id\" = '{manifestId}';"));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [MemberData(nameof(OmittedManifestScalarColumns))]
    public async Task PostgreSql17_TamperedStoredManifestScalarCannotReplayAsync(string column)
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);
        Assert.Equal(PreparedScanCommitOutcome.Committed, (await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None)).Outcome);
        DurableCounts before = await ReadCountsAsync(fixture.ConnectionString);
        string manifestId = PreparedScanIdentityBuilders.CreateDeterministicIdentity(
            "PreparedScanManifest", fixture.Package.Repositories[0].ManifestFingerprint).ToString("D");

        await ExecuteSqlAsync(fixture.ConnectionString, $"""
            UPDATE vulgata."PreparedScanManifests"
            SET "{column}" = "{column}" + 1
            WHERE "Id" = '{manifestId}';
            """);

        PreparedScanCommitResult result = await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 0, CancellationToken.None);

        output.WriteLine("Tampered stored manifest scalar {0} outcome: {1}", column, result.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Conflict, result.Outcome);
        Assert.Equal(before, await ReadCountsAsync(fixture.ConnectionString));
        Assert.Equal(1, await ScalarAsync<int>(fixture.ConnectionString, $"""
            SELECT count(*)::integer FROM vulgata."PreparedScanManifests"
            WHERE "Id" = '{manifestId}' AND "{column}" > 0;
            """));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
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

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
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

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
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

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PostgreSql17_StaleWriterConflictsAndLeavesZeroPreparedRowsAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);

        PreparedScanCommitResult result = await store.CommitPairAsync(
            fixture.Authorization, fixture.Package, 1, CancellationToken.None);

        Assert.Equal(PreparedScanCommitOutcome.Conflict, result.Outcome);
        Assert.Equal(DurableCounts.PrincipalOnly, await ReadCountsAsync(fixture.ConnectionString));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
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

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
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

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
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

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
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

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PostgreSql17_RestartExactRetryUsesNormativeClassificationOrderAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanPairPackage classified = AddTwoCanonicalClassifications(fixture.Package);
        PreparedRepositoryPackage first = classified.Repositories[0];
        string[] normativeOrder = first.Classifications
            .Select(classification => classification.Fingerprint)
            .ToArray();
        string[] fingerprintOrder = first.Classifications
            .OrderBy(classification => classification.Fingerprint, StringComparer.Ordinal)
            .Select(classification => classification.Fingerprint)
            .ToArray();
        Assert.False(normativeOrder.SequenceEqual(fingerprintOrder));
        output.WriteLine("Normative classification order: {0}", string.Join(", ", normativeOrder));
        output.WriteLine("Fingerprint classification order: {0}", string.Join(", ", fingerprintOrder));

        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);
        PreparedScanCommitResult committed = await store.CommitPairAsync(
            fixture.Authorization, classified, 0, CancellationToken.None);
        Assert.Equal(PreparedScanCommitOutcome.Committed, committed.Outcome);
        DurableCounts beforeReplay = await ReadCountsAsync(fixture.ConnectionString);
        fixture.Context.ChangeTracker.Clear();

        await using VulgataDbContext restartedContext =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        PreparedScanCommitResult replay = await PreparedScanPostgreSqlFixture.CreateStore(restartedContext)
            .CommitPairAsync(fixture.Authorization, classified, 0, CancellationToken.None);

        output.WriteLine("Restart exact-retry outcome: {0}", replay.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Replay, replay.Outcome);
        Assert.Equal(beforeReplay, await ReadCountsAsync(fixture.ConnectionString));
        Assert.Equal(normativeOrder, classified.Repositories[0].Classifications
            .Select(classification => classification.Fingerprint).ToArray());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PostgreSql17_NoncanonicalClassificationOrderConflictsBeforeFirstCommitAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedScanPairPackage canonical = AddTwoCanonicalClassifications(fixture.Package);
        PreparedRepositoryPackage canonicalFirst = canonical.Repositories[0];
        string[] normativeOrder = canonicalFirst.Classifications
            .Select(classification => classification.Fingerprint)
            .ToArray();
        CommunicationKindClassification[] reversed = canonicalFirst.Classifications
            .Reverse()
            .ToArray();
        string[] suppliedOrder = reversed.Select(classification => classification.Fingerprint).ToArray();
        Assert.False(normativeOrder.SequenceEqual(suppliedOrder));

        PreparedRepositoryPackage noncanonicalFirst = canonicalFirst with
        {
            Classifications = reversed,
        };
        PreparedCanonicalArtifact manifest = PreparedScanIdentityBuilders.CreateManifest(
            noncanonicalFirst, canonical.PairSelectionFingerprint);
        noncanonicalFirst = noncanonicalFirst with { ManifestFingerprint = manifest.Fingerprint };
        PreparedRepositoryPackage[] repositories = [noncanonicalFirst, canonical.Repositories[1]];
        PreparedCanonicalArtifact pair = PreparedScanIdentityBuilders.CreatePairPackage(
            canonical.CoordinationId,
            canonical.PairSelectionFingerprint,
            repositories.Select(repository => new PreparedManifestFingerprintBinding(
                repository.RepositoryId, repository.ManifestFingerprint)),
            canonical.SelectedCount);
        PreparedScanPairPackage selfConsistentNoncanonical = canonical with
        {
            Repositories = repositories,
            PairPackageFingerprint = pair.Fingerprint,
        };

        Assert.Equal(manifest.Fingerprint, selfConsistentNoncanonical.Repositories[0].ManifestFingerprint);
        Assert.Equal(pair.Fingerprint, selfConsistentNoncanonical.PairPackageFingerprint);
        output.WriteLine("Normative classification order: {0}", string.Join(", ", normativeOrder));
        output.WriteLine("Supplied noncanonical order: {0}", string.Join(", ", suppliedOrder));

        PreparedScanCommitResult result = await PreparedScanPostgreSqlFixture.CreateStore(fixture.Context)
            .CommitPairAsync(fixture.Authorization, selfConsistentNoncanonical, 0, CancellationToken.None);

        output.WriteLine("Noncanonical first-commit outcome: {0}", result.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Conflict, result.Outcome);
        Assert.Equal(DurableCounts.PrincipalOnly, await ReadCountsAsync(fixture.ConnectionString));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PostgreSql17_OverBoundDetectorPackageCommitsReplaysAndRejectsExternalReorderAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        PreparedCommunicationClassificationResult detection = CreateOverBoundClassificationResult(
            fixture.Package.Repositories[0].ScanRunId);
        PreparedScanPairPackage detectorPackage = AddOverBoundClassifications(fixture.Package, detection);
        string[] detectorOrder = detectorPackage.Repositories[0].Classifications
            .Select(classification => classification.Fingerprint)
            .ToArray();
        string[] normativeOrder = PreparedScanIdentityBuilders
            .OrderCommunicationClassifications(detectorPackage.Repositories[0].Classifications)
            .Select(classification => classification.Fingerprint)
            .ToArray();

        Assert.Equal(257, detection.TotalClassificationCount);
        Assert.Equal(257, detection.TotalWarningCount);
        Assert.Equal(256, detection.RetainedClassificationCount);
        Assert.True(detection.EvidenceTruncated);
        Assert.Equal(256, detectorPackage.Repositories[0].Classifications.Count);
        output.WriteLine("Detector order equals normative order: {0}", detectorOrder.SequenceEqual(normativeOrder));
        output.WriteLine("BoundExceeded position: {0}", detectorPackage.Repositories[0].Classifications
            .Select((classification, index) => (classification, index))
            .Single(value => value.classification.Rule == EvidenceRule.BoundExceeded).index);

        PreparedScanStore store = PreparedScanPostgreSqlFixture.CreateStore(fixture.Context);
        PreparedScanCommitResult committed = await store.CommitPairAsync(
            fixture.Authorization, detectorPackage, 0, CancellationToken.None);
        Assert.Equal(PreparedScanCommitOutcome.Committed, committed.Outcome);
        DurableCounts afterCommit = await ReadCountsAsync(fixture.ConnectionString);

        await using VulgataDbContext restartedContext =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        PreparedScanCommitResult replay = await PreparedScanPostgreSqlFixture.CreateStore(restartedContext)
            .CommitPairAsync(fixture.Authorization, detectorPackage, 0, CancellationToken.None);
        Assert.Equal(PreparedScanCommitOutcome.Replay, replay.Outcome);
        Assert.Equal(afterCommit, await ReadCountsAsync(fixture.ConnectionString));

        CommunicationKindClassification[] reversed = detectorPackage.Repositories[0].Classifications
            .Reverse()
            .ToArray();
        PreparedRepositoryPackage reorderedFirst = detectorPackage.Repositories[0] with
        {
            Classifications = reversed,
        };
        reorderedFirst = reorderedFirst with
        {
            ManifestFingerprint = PreparedScanIdentityBuilders.CreateManifest(
                reorderedFirst, detectorPackage.PairSelectionFingerprint).Fingerprint,
        };
        PreparedRepositoryPackage[] reorderedRepositories =
            [reorderedFirst, detectorPackage.Repositories[1]];
        PreparedCanonicalArtifact reorderedPair = PreparedScanIdentityBuilders.CreatePairPackage(
            detectorPackage.CoordinationId,
            detectorPackage.PairSelectionFingerprint,
            reorderedRepositories.Select(repository => new PreparedManifestFingerprintBinding(
                repository.RepositoryId, repository.ManifestFingerprint)),
            detectorPackage.SelectedCount);
        PreparedScanPairPackage externallyReordered = detectorPackage with
        {
            Repositories = reorderedRepositories,
            PairPackageFingerprint = reorderedPair.Fingerprint,
        };
        PreparedScanCommitResult reordered = await PreparedScanPostgreSqlFixture.CreateStore(restartedContext)
            .CommitPairAsync(fixture.Authorization, externallyReordered, 0, CancellationToken.None);

        Assert.Equal(PreparedScanCommitOutcome.Conflict, reordered.Outcome);
        Assert.Equal(afterCommit, await ReadCountsAsync(fixture.ConnectionString));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
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

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
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

    private static PreparedScanPairPackage AddTwoCanonicalClassifications(
        PreparedScanPairPackage package)
    {
        PreparedRepositoryPackage first = package.Repositories[0];
        CommunicationKindClassification[] classifications = CreateNormativeClassificationPair(first.ScanRunId);
        first = first with
        {
            Classifications = classifications,
            TotalConfirmedEvidenceCount = 2,
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

    private static PreparedCommunicationClassificationResult CreateOverBoundClassificationResult(
        Guid scanRunId)
    {
        PreparedCommunicationObservation[] observations = Enumerable.Range(0, 257)
            .Select(index => new PreparedCommunicationObservation(
                null,
                ClassificationLevel.Warning,
                ConfidenceBand.None,
                EvidenceRule.MalformedEvidence,
                EvidenceProvenance.Detector,
                $"src/malformed-{index:D3}.java",
                $"src/malformed-{index:D3}.java#warning",
                "qualification.over-bound"))
            .ToArray();
        return PreparedCommunicationDetector.Detect(scanRunId, observations);
    }

    private static PreparedScanPairPackage AddOverBoundClassifications(
        PreparedScanPairPackage package,
        PreparedCommunicationClassificationResult detection)
    {
        PreparedRepositoryPackage first = package.Repositories[0] with
        {
            Classifications = detection.Classifications,
            TotalConfirmedEvidenceCount = detection.TotalConfirmedCount,
            TotalHintCount = detection.TotalHintCount,
            TotalWarningCount = detection.TotalWarningCount,
            EvidenceTruncated = detection.EvidenceTruncated,
        };
        first = first with
        {
            ManifestFingerprint = PreparedScanIdentityBuilders.CreateManifest(
                first, package.PairSelectionFingerprint).Fingerprint,
        };
        PreparedRepositoryPackage second = package.Repositories[1] with
        {
            ManifestFingerprint = PreparedScanIdentityBuilders.CreateManifest(
                package.Repositories[1], package.PairSelectionFingerprint).Fingerprint,
        };
        PreparedRepositoryPackage[] repositories = [first, second];
        PreparedCanonicalArtifact pair = PreparedScanIdentityBuilders.CreatePairPackage(
            package.CoordinationId,
            package.PairSelectionFingerprint,
            repositories.Select(repository => new PreparedManifestFingerprintBinding(
                repository.RepositoryId, repository.ManifestFingerprint)),
            package.SelectedCount);
        return package with
        {
            Repositories = repositories,
            PairPackageFingerprint = pair.Fingerprint,
        };
    }

    private static CommunicationKindClassification[] CreateNormativeClassificationPair(Guid scanRunId)
    {
        for (int suffix = 0; suffix < 256; suffix++)
        {
            CommunicationKindClassification rpc = CreateClassification(
                scanRunId,
                CommunicationKind.Rpc,
                EvidenceRule.RpcDirectAnnotation,
                $"src/rpc-{suffix:D3}.java",
                $"src/rpc-{suffix:D3}.java#rpc");
            CommunicationKindClassification http = CreateClassification(
                scanRunId,
                CommunicationKind.Http,
                EvidenceRule.HttpControllerAnnotation,
                $"src/http-{suffix:D3}.java",
                $"src/http-{suffix:D3}.java#http");

            CommunicationKindClassification[] normative = [rpc, http];
            if (!normative.OrderBy(classification => classification.Fingerprint, StringComparer.Ordinal)
                    .SequenceEqual(normative))
            {
                return normative;
            }
        }

        throw new InvalidOperationException(
            "The classification fixture could not produce distinct normative and fingerprint orderings.");
    }

    private static CommunicationKindClassification CreateClassification(
        Guid scanRunId,
        CommunicationKind kind,
        EvidenceRule rule,
        string relativePath,
        string locator)
    {
        const string catalogRule = "qualification.ordering";
        PreparedCanonicalArtifact identity = PreparedScanIdentityBuilders.CreateCommunicationClassification(
            scanRunId,
            kind,
            ClassificationLevel.Confirmed,
            ConfidenceBand.High,
            rule,
            EvidenceProvenance.StructuralAnnotation,
            relativePath,
            locator,
            1,
            catalogRule);
        CommunicationKindClassification classification = CommunicationKindClassification.Create(
            scanRunId,
            kind,
            ClassificationLevel.Confirmed,
            ConfidenceBand.High,
            rule,
            EvidenceProvenance.StructuralAnnotation,
            relativePath,
            locator,
            1,
            identity.Fingerprint);
        typeof(CommunicationKindClassification).GetProperty(
            nameof(CommunicationKindClassification.CatalogRuleIdentity),
            BindingFlags.Public | BindingFlags.Instance)!.SetValue(classification, catalogRule);
        return classification;
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

    private static async Task<TypedProofStorageTarget?> FindTypedProofStorageTargetAsync(
        string connectionString,
        string canonicalFieldFragment)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("""
            SELECT table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = 'vulgata'
            ORDER BY table_name, ordinal_position;
            """, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        Dictionary<string, List<string>> columnsByTable = new(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            string table = reader.GetString(0);
            string column = reader.GetString(1);
            columnsByTable.TryGetValue(table, out List<string>? columns);
            (columns ??= []).Add(column);
            columnsByTable[table] = columns;
        }

        foreach ((string table, List<string> columns) in columnsByTable)
        {
            string? proofColumn = columns.FirstOrDefault(column =>
                column.Replace("_", string.Empty, StringComparison.Ordinal)
                    .Contains(canonicalFieldFragment, StringComparison.OrdinalIgnoreCase));
            if (proofColumn is null)
            {
                continue;
            }

            string? manifestKey = string.Equals(table, "PreparedScanManifests", StringComparison.Ordinal)
                ? columns.FirstOrDefault(column => string.Equals(column, "Id", StringComparison.Ordinal))
                : columns.FirstOrDefault(column =>
                    string.Equals(column, "PreparedScanManifestId", StringComparison.Ordinal) ||
                    string.Equals(column, "ManifestId", StringComparison.Ordinal));
            if (manifestKey is not null)
            {
                return new(table, proofColumn, manifestKey);
            }
        }

        return null;
    }

    private sealed record TypedProofStorageTarget(
        string TableName,
        string ColumnName,
        string ManifestKeyColumn)
    {
        internal string CreateMutationSql(Guid manifestId) => $"""
            UPDATE vulgata.{QuoteIdentifier(TableName)}
            SET {QuoteIdentifier(ColumnName)} = CASE
                WHEN left({QuoteIdentifier(ColumnName)}::text, 1) = 'e'
                    THEN 'f' || substring({QuoteIdentifier(ColumnName)}::text FROM 2)
                ELSE 'e' || substring({QuoteIdentifier(ColumnName)}::text FROM 2)
            END
            WHERE {QuoteIdentifier(ManifestKeyColumn)} = '{manifestId:D}';
            """;

        internal string CreateRowCountSql(Guid manifestId) => $"""
            SELECT count(*)::integer
            FROM vulgata.{QuoteIdentifier(TableName)}
            WHERE {QuoteIdentifier(ManifestKeyColumn)} = '{manifestId:D}';
            """;

        private static string QuoteIdentifier(string identifier) =>
            $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
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
