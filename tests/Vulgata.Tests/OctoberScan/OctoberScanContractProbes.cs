using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberKnowledge;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Scan consumer probes of the owner's ControlledFixture, not production persistence or authorization qualification.</summary>
public sealed class OctoberScanContractProbes
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenReadSelectionAsync_AnyBindingMismatch_RejectsWithoutFallbackAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        foreach (var binding in new[]
        {
            fixture.Binding with { CanonicalDatabaseIdentity = "different-database" },
            fixture.Binding with { TargetId = "different-target" },
            fixture.Binding with { BatchId = OctoberKnowledgeFixture.Id(901) },
            fixture.Binding with { SelectionDigest = "different-selection" },
        })
        {
            var result = await reader.OpenReadSelectionAsync(fixture.Selection() with { Publication = new(binding, fixture.PublicationId) });
            Assert.Equal(KnowledgeReadStatus.Unavailable, result.Status);
            Assert.Null(result.Selection);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_MalformedExactHandlesAndSelection_RejectsMissingIdentities()
    {
        var fixture = new OctoberKnowledgeFixture();
        var validator = new ReadKnowledgeRequestValidator();
        var handle = fixture.Handle(fixture.ClA);
        Assert.True(validator.Validate(new ReadKnowledgeRequest("controlled-selection", handle)).IsValid);
        foreach (var malformed in new[]
        {
            handle with { BatchId = Guid.Empty }, handle with { PublicationId = Guid.Empty },
            handle with { LogicalDocumentId = Guid.Empty }, handle with { DocumentVersionId = Guid.Empty },
            handle with { SectionId = " " },
        })
            Assert.False(validator.Validate(new ReadKnowledgeRequest("controlled-selection", malformed)).IsValid);
        Assert.False(new OpenKnowledgeSelectionRequestValidator().Validate(new OpenKnowledgeSelectionRequest(fixture.Binding.BatchId, fixture.PublicationId, KnowledgeMode.IT, [fixture.RepositoryA, fixture.RepositoryA])).IsValid);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_ProofMismatch_RejectsBeforeTransactionAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var model = new ControlledKnowledgeWriter(fixture, new());
        IOctoberKnowledgeWriter writer = model;
        var proposal = fixture.MultiUnit;
        var result = await writer.AcceptCompletedFileAsync(new(new(fixture.Binding.BatchId, OperationKind.AcceptFile, OctoberKnowledgeFixture.Id(902)), proposal, new ControlledCompletedProof(proposal with { CanonicalProposalDigest = "different-proof" })));
        Assert.Equal(KnowledgeWriteStatus.Denied, result.Status);
        Assert.Null(result.AcceptedFile);
        Assert.Equal(0, model.TransactionsStarted);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptCompletedFileAsync_MultiOrZeroUnitReplayAndConflict_PreservesOwnerResultAsync(bool zeroUnit)
    {
        var fixture = new OctoberKnowledgeFixture();
        var model = new ControlledKnowledgeWriter(fixture, new());
        IOctoberKnowledgeWriter writer = model;
        var proposal = zeroUnit ? fixture.ZeroUnit : fixture.MultiUnit;
        var submission = new CompletedFileSubmission(new(fixture.Binding.BatchId, OperationKind.AcceptFile, OctoberKnowledgeFixture.Id(903)), proposal, new ControlledCompletedProof(proposal));
        var accepted = await writer.AcceptCompletedFileAsync(submission);
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        Assert.Equal(proposal.AdmissionId, accepted.AcceptedFile!.AdmissionId);
        Assert.Equal(proposal.Coverage, accepted.AcceptedFile.Coverage);
        Assert.Equal(zeroUnit ? 0 : 2, accepted.AcceptedFile.Documents.Length);
        var replay = await writer.AcceptCompletedFileAsync(submission);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(accepted.AcceptedFile, replay.AcceptedFile);
        var changed = proposal with { CanonicalProposalDigest = "different-payload" };
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await writer.AcceptCompletedFileAsync(submission with { Proposal = changed, Proof = new ControlledCompletedProof(changed) })).Status);
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await writer.LookupOperationAsync(fixture.Binding, submission.Operation, "different-payload")).Status);
        Assert.Equal(1, model.AcceptedRows);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadExactAsync_PublicationAndVersionMismatch_ReturnsNoMetadataAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection(publication: fixture.EarlyPublicationId))).Selection!;
        var handle = fixture.Handle(fixture.ClA, fixture.EarlyPublicationId);
        Assert.Equal(KnowledgeReadStatus.Success, (await reader.ReadExactAsync(new(selection, handle))).Status);
        foreach (var mismatch in new[] { handle with { PublicationId = fixture.PublicationId }, handle with { DocumentVersionId = OctoberKnowledgeFixture.Id(904) }, fixture.Handle(fixture.BlA, fixture.EarlyPublicationId) })
        {
            var result = await reader.ReadExactAsync(new(selection, mismatch));
            Assert.Equal(KnowledgeReadStatus.Unavailable, result.Status);
            Assert.Null(result.Document);
            Assert.Null(result.Content);
            Assert.Empty(result.Claims);
            Assert.Null(result.Next);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadExactAsync_OrdinaryITThenRevokedOrBusiness_ExcludesClMetadataAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var policy = new ControlledDisclosurePolicy();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, policy);
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection())).Selection!;
        Assert.Equal(KnowledgeReadStatus.Success, (await reader.ReadExactAsync(new(selection, fixture.Handle(fixture.ClA)))).Status);
        policy.RevokedRepositories.Add(fixture.RepositoryA);
        var revoked = await reader.ReadExactAsync(new(selection, fixture.Handle(fixture.ClA)));
        Assert.Equal(KnowledgeReadStatus.Unavailable, revoked.Status);
        Assert.Null(revoked.Document);
        Assert.Null(revoked.Content);
        policy.RevokedRepositories.Clear();
        var business = (await reader.OpenReadSelectionAsync(fixture.Selection(KnowledgeMode.Business))).Selection!;
        Assert.Equal(KnowledgeReadStatus.Unavailable, (await reader.ReadExactAsync(new(business, fixture.Handle(fixture.ClA)))).Status);
        var page = await reader.SearchAsync(new(business, ""));
        Assert.All(page.Documents, document => Assert.Equal(DocumentKind.BusinessLogic, document.Kind));
        Assert.DoesNotContain(page.Documents, document => document.Handle.DocumentVersionId == fixture.BlHidden.DocumentVersionId);
    }
}
