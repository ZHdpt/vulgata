using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Actual executor, Scan scope and SQLite Knowledge handoff; provider, source and commit authority remain controlled.</summary>
public sealed class OctoberResolutionHandoffTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAcceptPublishAsync_OriginalWitnessCrossesHandoffWithoutReconstructionAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        string clBefore = await fixture.ClSnapshotAsync();
        var store = fixture.Store();
        var loaded = await store.LoadCorpusAsync(fixture.Corpus.Handle);
        Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
        var corpus = Assert.IsType<ResolutionCorpus>(loaded.Corpus);
        var inquiry = Assert.Single(OctoberResolutionPolicy.Plan(corpus).Inquiries);
        var request = fixture.Request();
        var original = request.Witness! with
        {
            ConnectionExplanation = "Original provider interpretation: the exact accepted configuration connects these two versions.",
            Limitations = ["Original provider qualifier: this static connection does not establish deployed availability."]
        };
        Guid run = Guid.NewGuid();
        var finalization = new OperationKey(request.Operation.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        await using var scope = Scope(fixture, run, finalization);
        using var client = new ResolutionClient((_, _) => Task.FromResult(Response(JsonSerializer.Serialize(original))));
        var executor = new OctoberResolutionExecutor(client, "Controlled inquiry: retain accepted facts, exact evidence and qualifications.");

        var execution = await executor.ExecuteAsync(corpus, inquiry, finalization, run, "controlled-profile", 40,
            scope, new ResolutionDisclosure());

        Assert.Null(execution.AdmissionFailure);
        Assert.Equal(ResolutionOutcomeKind.Supported, execution.Outcome!.Kind);
        Assert.Equal(1, client.Calls);
        var handedOff = Assert.IsType<ResolutionWitness>(execution.Witness);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(original), OctoberKnowledgePolicy.SerializeCanonical(handedOff));
        Assert.All(handedOff.SupportingAssertions, assertion =>
        {
            Assert.Null(assertion.Evidence.Document);
            Assert.Null(assertion.Evidence.ClaimGroupId);
            Assert.NotNull(assertion.Evidence.Source);
            var document = corpus.Documents.Single(value => value.Version == assertion.Document);
            var claim = document.Sections.Single(value => value.SectionId == assertion.SectionId)
                .Claims.Single(value => value.ClaimGroupId == assertion.ClaimGroupId);
            Assert.Equal(claim.Text, assertion.ExactAcceptedText);
            Assert.Contains(assertion.Evidence, claim.Evidence);
        });

        // Pass the returned original witness directly; the accepted outcome is recomputed by Knowledge.
        request = ResolutionPersistenceFixture.Digest(request with { Witness = handedOff });
        var accepted = await store.AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var outcome = Assert.IsType<ResolutionOutcome>(accepted.Outcome);
        Assert.Equal(ResolutionOutcomeKind.Supported, outcome.Kind);
        Assert.Equal(inquiry.Caller.Document, outcome.Relationship!.From);
        Assert.Equal(handedOff.Provider!.Document, outcome.Relationship.To);
        Assert.Contains(original.Limitations[0], outcome.Uncertainty!.Limitations);
        Assert.Contains(ResolutionPersistenceFixture.SourceLimit, outcome.Uncertainty.Limitations);
        await using (var context = fixture.Acceptance.Factory.CreateDbContext())
        {
            var stored = await context.ResolutionItems.AsNoTracking().SingleAsync();
            var retained = JsonSerializer.Deserialize<ResolutionWitness>(stored.WitnessJson!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(original), OctoberKnowledgePolicy.SerializeCanonical(retained));
        }
        var publication = await fixture.Acceptance.Writer().PublishSelectionAsync(fixture.Publication([outcome]));
        Assert.Equal(KnowledgeWriteStatus.Committed, publication.Status);
        Assert.NotEqual(fixture.B0Publication.PublicationId, publication.Publication!.PublicationId);
        var related = await fixture.RelatedAsync(publication.Publication, inquiry.Caller.Document);
        Assert.Equal(KnowledgeReadStatus.Success, related.Status);
        Assert.Equal(outcome.Relationship.RelationshipVersionId, Assert.Single(related.Relationships).RelationshipVersionId);
        Assert.Contains(related.PermittedUncertainties, value => value.Key == outcome.Uncertainty.Key);
        Assert.Empty((await fixture.RelatedAsync(fixture.B0Publication, inquiry.Caller.Document)).Relationships);
        Assert.Equal(clBefore, await fixture.ClSnapshotAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("malformed")]
    [InlineData("final-denial")]
    [InlineData("hard-stop")]
    public async Task ExecuteAsync_UnsuccessfulResponse_DoesNotHandOffWitnessAsync(string failure)
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var original = fixture.Request().Witness!;
        Assert.Equal(ResolutionOutcomeKind.Supported, OctoberResolutionPolicy.Validate(fixture.Corpus, fixture.Inquiry, original).Kind);
        Guid run = Guid.NewGuid();
        var finalization = new OperationKey(fixture.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        await using var scope = Scope(fixture, run, finalization);
        var disclosure = new ResolutionDisclosure();
        using var client = new ResolutionClient((_, _) =>
        {
            if (failure == "final-denial") disclosure.Status = DisclosureStatus.Denied;
            if (failure == "hard-stop") scope.HardStop();
            return Task.FromResult(Response(failure == "malformed" ? "not-json" : JsonSerializer.Serialize(original)));
        });

        var result = await new OctoberResolutionExecutor(client, "Controlled accepted-evidence inquiry.")
            .ExecuteAsync(fixture.Corpus, fixture.Inquiry, finalization, run, "controlled-profile", 40, scope, disclosure);

        Assert.Equal(1, client.Calls);
        Assert.Single(result.Attempts);
        Assert.Null(result.Outcome);
        Assert.Null(result.Witness);
        Assert.Equal(failure switch
        {
            "final-denial" => ProviderAdmissionStatus.Denied,
            "hard-stop" => ProviderAdmissionStatus.HardStopped,
            _ => ProviderAdmissionStatus.AuthorityUnavailable
        }, result.AdmissionFailure);
    }

    private static OctoberExecutionScope Scope(ResolutionPersistenceFixture fixture, Guid run, OperationKey finalization) =>
        new(run, "controlled-profile", new OctoberScanPolicy
        {
            OutputStage = "cl-and-bl", WorkerCap = 1, MaximumTotalTokens = 100,
            SourceRules = [new(fixture.Corpus.Documents[0].RepositoryId, "src", "selected", "Controlled component qualification")]
        }, (_, _) => Task.FromResult(ProviderAdmissionStatus.Granted), finalization);

    private static ChatResponse Response(string text) => new(new ChatMessage(ChatRole.Assistant, text))
    {
        FinishReason = ChatFinishReason.Stop,
        Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 }
    };
}
