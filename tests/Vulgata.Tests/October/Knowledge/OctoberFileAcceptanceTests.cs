using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberFileAcceptanceTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_WholeFile_PersistsAtomicCanonicalKnowledgeAndExactResultAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var submission = fixture.Seal();
        var result = await fixture.Writer().AcceptCompletedFileAsync(submission);
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        Assert.NotNull(result.AcceptedFile);
        Assert.Equal(2, result.AcceptedFile.Documents.Length);
        Assert.Null(result.Publication);
        Assert.Equal(KnowledgeWriteStatus.Committed, Assert.Single(fixture.Log.Entries).Status);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync());
        Assert.Equal(1, await context.AcceptedFileManifests.CountAsync());
        Assert.Equal(2, await context.CodeUnits.CountAsync());
        Assert.Equal(2, await context.LogicalDocuments.CountAsync());
        Assert.Equal(2, await context.DocumentVersions.CountAsync());
        Assert.Equal(2, await context.DocumentSections.CountAsync());
        Assert.Equal(3, await context.ClaimGroups.CountAsync());
        Assert.Equal(2, await context.KnowledgeContributors.CountAsync());
        Assert.Equal(1, await context.CommunicationObservations.CountAsync());
        Assert.Equal(1, await context.KnowledgeUncertainties.CountAsync());
        var manifest = await context.AcceptedFileManifests.SingleAsync();
        Assert.Equal(submission.Proposal.CanonicalProposalDigest, manifest.ProposalDigest);
        Assert.Equal(fixture.Origin.PreparedVersion, manifest.PreparedVersion);
        var units = await context.CodeUnits.ToListAsync();
        Assert.Equal(units.Single(u => u.SemanticSelector == "operation:order").UnitId, units.Single(u => u.SemanticSelector.EndsWith("/helper", StringComparison.Ordinal)).ParentUnitId);
        foreach (var version in await context.DocumentVersions.ToListAsync())
        {
            using var json = JsonDocument.Parse(version.ContentJson);
            Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(1, json.RootElement.GetProperty("sections").GetArrayLength());
            Assert.Contains("Acquires the order lock.", version.ContentJson, StringComparison.Ordinal);
        }
        Assert.Contains(await context.DocumentVersions.Select(v => v.ContentJson).ToListAsync(), json => json.Contains("AdvisoryNotSourceProof", StringComparison.Ordinal));
        Assert.Equal(1, fixture.Guard.Submissions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_ExplicitZeroUnits_PersistsCoverageWithoutFakeDocumentAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var proposal = fixture.Proposal with { Units = [], Observations = [], Uncertainties = [], Coverage = fixture.Proposal.Coverage with { Disposition = CoverageDisposition.CompletedZeroUnit, Reason = "Complete file has no semantic units" } };
        var result = await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        Assert.Empty(result.AcceptedFile!.Documents);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal(1, await context.AcceptedFileManifests.CountAsync());
        Assert.Empty(await context.CodeUnits.ToListAsync());
        Assert.Empty(await context.DocumentVersions.ToListAsync());
        Assert.Equal(CoverageDisposition.CompletedZeroUnit, result.AcceptedFile.Coverage.Disposition);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("forged")]
    [InlineData("digest")]
    [InlineData("missing-source")]
    [InlineData("wrong-source")]
    [InlineData("wrong-binding")]
    [InlineData("partial")]
    public async Task AcceptCompletedFileAsync_UntrustedOrIncompleteInput_RejectsWithoutRowsAsync(string mutation)
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var submission = fixture.Seal();
        switch (mutation)
        {
            case "forged": submission = submission with { Proof = new ForgedCompletedFileProof(submission.Proposal) }; break;
            case "digest": submission = submission with { Proposal = submission.Proposal with { CanonicalProposalDigest = new string('e', 64) } }; break;
            case "missing-source": fixture.Source.Result = null; break;
            case "wrong-source": fixture.Source.Result = new(fixture.Origin with { SourceDigest = new string('d', 64) }, 100); break;
            case "wrong-binding": submission = fixture.Seal(fixture.Proposal with { Binding = fixture.Binding with { CanonicalDatabaseIdentity = "foreign-database" } }); break;
            case "partial": submission = fixture.Seal(fixture.Proposal with { Coverage = fixture.Proposal.Coverage with { Disposition = CoverageDisposition.Failed, Reason = "Incomplete analysis" } }); break;
        }
        var result = await fixture.Writer().AcceptCompletedFileAsync(submission);
        Assert.Contains(result.Status, new[] { KnowledgeWriteStatus.InvalidProposal, KnowledgeWriteStatus.NotCompleted, KnowledgeWriteStatus.Denied });
        Assert.Null(result.AcceptedFile);
        Assert.Equal(0, fixture.Guard.Submissions);
        await fixture.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_SealThenExpiry_RejectsBeforeTransactionEntryAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var submission = fixture.Seal();
        fixture.Guard.Clock = fixture.Guard.Deadline;
        var result = await fixture.Writer().AcceptCompletedFileAsync(submission);
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, result.Status);
        Assert.Equal(0, fixture.Transactions.Starts);
        Assert.Equal(0, fixture.Guard.Submissions);
        await fixture.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("expiry", KnowledgeWriteStatus.StaleOrFenced)]
    [InlineData("hard-stop", KnowledgeWriteStatus.StaleOrFenced)]
    [InlineData("denied", KnowledgeWriteStatus.Denied)]
    [InlineData("outage", KnowledgeWriteStatus.AuthorityUnavailable)]
    public async Task AcceptCompletedFileAsync_EligibilityChangesDuringStaging_RollsBackEveryRowAsync(string change, KnowledgeWriteStatus expected)
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        fixture.Guard.BeforeSubmission = () =>
        {
            if (change == "expiry") fixture.Guard.Clock = fixture.Guard.Deadline;
            if (change == "hard-stop") fixture.Guard.Fenced = true;
            if (change == "denied") fixture.Authorization.Status = DisclosureStatus.Denied;
            if (change == "outage") fixture.Authorization.Status = DisclosureStatus.AuthorityUnavailable;
        };
        Assert.Equal(expected, (await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal())).Status);
        Assert.Equal(1, fixture.Transactions.Starts);
        Assert.Equal(0, fixture.Guard.Submissions);
        await fixture.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_InsertFailure_RollsBackOperationManifestAndContentAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        await using (var context = fixture.Factory.CreateDbContext())
            await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectKnowledgeDocument BEFORE INSERT ON OctoberDocumentVersions BEGIN SELECT RAISE(ABORT, 'controlled insert failure'); END;");
        Assert.Equal(KnowledgeWriteStatus.StorageUnavailable, (await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal())).Status);
        await fixture.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_ExactReplayAndConflict_PreservesVersionIdsWithoutProofRegenerationAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var submission = fixture.Seal();
        var first = await fixture.Writer().AcceptCompletedFileAsync(submission);
        Assert.Equal(KnowledgeWriteStatus.Committed, first.Status);
        fixture.Completion.Status = DisclosureStatus.Denied;
        fixture.Guard.Fenced = true;
        var replay = await fixture.Writer().AcceptCompletedFileAsync(submission);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, fixture.Log.Entries[^1].Status);
        Assert.Equal(first.AcceptedFile!.ManifestId, replay.AcceptedFile!.ManifestId);
        Assert.Equal(first.AcceptedFile.Documents.Select(d => d.DocumentVersionId), replay.AcceptedFile.Documents.Select(d => d.DocumentVersionId));
        var changed = fixture.Proposal with { Units = fixture.Proposal.Units.SetItem(0, fixture.Proposal.Units[0] with { Title = "Changed payload" }) };
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal(changed))).Status);
        Assert.Equal(1, fixture.Completion.Calls);
        Assert.Equal(1, fixture.Guard.Submissions);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync());
        Assert.Equal(2, await context.DocumentVersions.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LookupOperationAsync_LostCommitAcknowledgement_ReturnsExactCommittedResultAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        fixture.Guard.LoseAcknowledgement = true;
        var submission = fixture.Seal();
        var result = await fixture.Writer().AcceptCompletedFileAsync(submission);
        Assert.Contains(result.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted });
        Assert.NotNull(result.AcceptedFile);
        var lookup = await fixture.Writer().LookupOperationAsync(fixture.Binding, submission.Operation, submission.Proposal.CanonicalProposalDigest);
        Assert.Equal(result.AcceptedFile.ManifestId, lookup.AcceptedFile!.ManifestId);
        Assert.Equal(1, fixture.Guard.Submissions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LookupOperationAsync_AbsentOperation_CannotProveOriginalTransactionWillNotCommitAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var submission = fixture.Seal();
        var lookup = await fixture.Writer().LookupOperationAsync(fixture.Binding, submission.Operation, submission.Proposal.CanonicalProposalDigest);
        Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, lookup.Status);
        Assert.Null(lookup.AcceptedFile);
        Assert.Equal(0, fixture.Completion.Calls);
        Assert.Equal(0, fixture.Source.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DisclosureStatus.Denied, KnowledgeWriteStatus.Denied)]
    [InlineData(DisclosureStatus.AuthorityUnavailable, KnowledgeWriteStatus.AuthorityUnavailable)]
    public async Task LookupOperationAsync_StoredUncitedContributorDenied_HidesHistoryBeforeConflictAsync(DisclosureStatus authority, KnowledgeWriteStatus expected)
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var uncited = fixture.UncitedContributor;
        var submission = fixture.Seal(fixture.Proposal with { Contributors = fixture.Proposal.Contributors.Add(uncited) });
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Writer().AcceptCompletedFileAsync(submission)).Status);
        fixture.Authorization.Status = authority;
        var result = await fixture.Writer().LookupOperationAsync(fixture.Binding, submission.Operation, "different-digest");
        Assert.Equal(expected, result.Status);
        Assert.Null(result.AcceptedFile);
        Assert.Contains(uncited, fixture.Authorization.LastContributors);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal(1, await context.AcceptedFileManifests.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_LogFailureAfterCommit_PreservesExactLookupWithoutRegenerationAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        fixture.Log.Succeeds = false;
        var submission = fixture.Seal();
        var result = await fixture.Writer().AcceptCompletedFileAsync(submission);
        Assert.Equal(KnowledgeWriteStatus.LogUnavailable, result.Status);
        Assert.NotNull(result.AcceptedFile);
        fixture.Log.Succeeds = true;
        fixture.Completion.Status = DisclosureStatus.Denied;
        var lookup = await fixture.Writer().LookupOperationAsync(fixture.Binding, submission.Operation, submission.Proposal.CanonicalProposalDigest);
        Assert.NotNull(lookup.AcceptedFile);
        Assert.Equal(result.AcceptedFile.ManifestId, lookup.AcceptedFile.ManifestId);
        Assert.Equal(1, fixture.Guard.Submissions);
        Assert.Equal(1, fixture.Completion.Calls);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LookupOperationAsync_AmbiguousAcknowledgementAndLookupOutage_RemainsIndeterminateUntilRecoveryAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var submission = fixture.Seal();
        fixture.Guard.LoseAcknowledgement = true;
        fixture.Guard.AfterSettlement = () => fixture.Factory.Available = false;
        KnowledgeWriteResult result;
        try { result = await fixture.Writer().AcceptCompletedFileAsync(submission); }
        finally { fixture.Factory.Available = true; }
        Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, result.Status);
        Assert.Null(result.AcceptedFile);
        var lookup = await fixture.Writer().LookupOperationAsync(fixture.Binding, submission.Operation, submission.Proposal.CanonicalProposalDigest);
        Assert.NotNull(lookup.AcceptedFile);
        Assert.Equal(1, fixture.Guard.Submissions);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_AuthorityLostAfterPhysicalCommit_PreservesHistoryWithoutDisclosureAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        fixture.Guard.AfterSettlement = () => fixture.Authorization.Status = DisclosureStatus.Denied;
        var result = await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal());
        Assert.Equal(KnowledgeWriteStatus.Denied, result.Status);
        Assert.Null(result.AcceptedFile);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal(1, await context.AcceptedFileManifests.CountAsync());
        Assert.Equal(2, await context.DocumentVersions.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_AttemptAndAnchorRangesChange_PreservesBoundSemanticIdentityAsync()
    {
        await using var first = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        await using var second = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await first.Writer().AcceptCompletedFileAsync(first.Seal())).Status);
        Guid admission = Guid.NewGuid();
        var changed = second.Proposal with { AdmissionId = admission, AttemptId = Guid.NewGuid(), Coverage = second.Proposal.Coverage with { AdmissionId = admission }, Units = second.Proposal.Units.SetItem(0, second.Proposal.Units[0] with { Anchors = [new(second.Origin, 9, 21)] }) };
        Assert.Equal(KnowledgeWriteStatus.Committed, (await second.Writer().AcceptCompletedFileAsync(second.Seal(changed))).Status);
        await using var left = first.Factory.CreateDbContext();
        await using var right = second.Factory.CreateDbContext();
        var leftUnits = await left.CodeUnits.ToListAsync();
        var rightUnits = await right.CodeUnits.ToListAsync();
        foreach (var unit in leftUnits) Assert.Equal(unit.UnitId, rightUnits.Single(other => other.SemanticSelector == unit.SemanticSelector).UnitId);
        var leftDocuments = await left.LogicalDocuments.ToListAsync();
        var rightDocuments = await right.LogicalDocuments.ToListAsync();
        foreach (var document in leftDocuments) Assert.Equal(document.LogicalDocumentId, rightDocuments.Single(other => other.UnitId == document.UnitId).LogicalDocumentId);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_ChangedReplayRetainsOldDigest_RejectsAfterStoredContributorAuthorizationAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var original = fixture.Seal();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Writer().AcceptCompletedFileAsync(original)).Status);
        var unit = original.Proposal.Units[0];
        var changed = OctoberKnowledgeAcceptanceFixture.Unit(unit.SemanticSelector, unit.ParentSelector, "Changed narrative", unit.Anchors, unit.Sections);
        var replay = original with { Proposal = original.Proposal with { Units = original.Proposal.Units.SetItem(0, changed) } };
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await fixture.Writer().AcceptCompletedFileAsync(replay)).Status);
        fixture.Authorization.Status = DisclosureStatus.Denied;
        Assert.Equal(KnowledgeWriteStatus.Denied, (await fixture.Writer().AcceptCompletedFileAsync(replay)).Status);
        Assert.Equal(1, fixture.Guard.Submissions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_AdvisoryEvidenceAndContributorShareForgedDigest_RejectsAgainstPersistedSelectedSnapshotAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var proposal = fixture.Proposal;
        var unit = proposal.Units[0];
        var section = unit.Sections[0];
        var advisory = section.Claims[1];
        string forgedDigest = new string('f', 64);
        advisory = advisory with { Evidence = [advisory.Evidence[0] with { ContentDigest = forgedDigest }] };
        section = section with { Claims = section.Claims.SetItem(1, advisory) };
        unit = OctoberKnowledgeAcceptanceFixture.Unit(unit.SemanticSelector, unit.ParentSelector, unit.Title, unit.Anchors, [section]);
        proposal = proposal with { Units = proposal.Units.SetItem(0, unit), Contributors = proposal.Contributors.SetItem(1, fixture.AdvisoryContributor with { ContentDigest = forgedDigest }) };
        var result = await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(KnowledgeWriteStatus.Denied, result.Status);
        Assert.Null(result.AcceptedFile);
        Assert.Equal(0, fixture.Guard.Submissions);
        await fixture.AssertNoKnowledgeAsync();
    }
}
