using System.Collections.Immutable;
using System.Text.Json;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using ApiType = Vulgata.Core.Entities.ApiType;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberResolutionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("plain")]
    [InlineData("answer-and-reasoning")]
    [InlineData("reasoning-only")]
    [InlineData("invalid-answer")]
    public async Task ExecuteAsync_ActualSelectedSdkReasoning_IsNotAcceptedWitnessContentAsync(string shape)
    {
        var f = new ResolutionCase();
        using var transport = new SynthesisSdkResponseHandler(JsonSerializer.Serialize(f.Witness()), shape);
        using var client = await new OctoberWorkerClientFactory(new SynthesisSdkSecret(), transport).CreateAsync(
            new("controlled-sdk-response", "only-source", new("selected-profile", ApiType.ChatCompletions,
                "https://never-contacted.invalid/v1", "controlled-model", "credential://controlled-sdk"), "controlled-protected"));
        Guid run = Guid.NewGuid();
        var operation = new OperationKey(f.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        await using var scope = new OctoberExecutionScope(run, "selected-profile", new()
        {
            OutputStage = "cl-and-bl", WorkerCap = 1, MaximumTotalTokens = 100,
            SourceRules = [new(f.Corpus.Documents[0].RepositoryId, "src", "selected", "Controlled accepted-input qualification")]
        }, (_, _) => Task.FromResult(ProviderAdmissionStatus.Granted), operation);
        var result = await new OctoberResolutionExecutor(client, "Controlled accepted-input instructions.")
            .ExecuteAsync(f.Corpus, f.Inquiry(), operation, run, "selected-profile", 40, scope, new ResolutionDisclosure());
        Assert.Equal(1, transport.Calls);
        Assert.Equal(10, Assert.Single(result.Attempts).InputTokens);
        Assert.Equal(5, result.Attempts[0].OutputTokens);
        if (shape is "reasoning-only" or "invalid-answer")
        {
            Assert.Null(result.Outcome); Assert.Null(result.Witness);
        }
        else
        {
            Assert.NotNull(result.Outcome); Assert.Equal(ResolutionOutcomeKind.Supported, result.Outcome.Kind);
            Assert.NotNull(result.Witness); Assert.Null(result.AdmissionFailure);
            Assert.True(string.Equals(OctoberKnowledgePolicy.SerializeCanonical(f.Witness()),
                OctoberKnowledgePolicy.SerializeCanonical(result.Witness), StringComparison.Ordinal));
            Assert.False(OctoberKnowledgePolicy.SerializeCanonical(result.Witness).Contains(SynthesisSdkResponseHandler.ReasoningMarker, StringComparison.Ordinal));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Plan_InputOrderChanges_RetainsExactInquiryFingerprintAndContributorUnion()
    {
        var fixture = new ResolutionCase();
        var first = Assert.Single(OctoberResolutionPolicy.Plan(fixture.Corpus).Inquiries);
        var reversed = fixture.Corpus with { Documents = fixture.Corpus.Documents.Reverse().ToImmutableArray() };
        var second = Assert.Single(OctoberResolutionPolicy.Plan(reversed).Inquiries);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.Caller, second.Caller);
        Assert.Equal(first.Candidates.ToArray(), second.Candidates.ToArray());
        Assert.Equal(fixture.Caller, first.Caller);
        Assert.Contains(fixture.Provider, first.Candidates);
        Assert.True(fixture.Corpus.Documents.SelectMany(x => x.Contributors).ToHashSet().SetEquals(first.Contributors));
        Assert.False(string.IsNullOrWhiteSpace(first.Fingerprint));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Plan_CorpusEvidenceOrPolicyChanges_InvalidatesPriorFingerprint()
    {
        var fixture = new ResolutionCase();
        string original = Assert.Single(OctoberResolutionPolicy.Plan(fixture.Corpus).Inquiries).Fingerprint;
        var changedCorpus = fixture.Corpus with { Handle = fixture.Corpus.Handle with { CorpusDigest = new string('e', 64) } };
        Assert.NotEqual(original, Assert.Single(OctoberResolutionPolicy.Plan(changedCorpus).Inquiries).Fingerprint);
        var provider = fixture.Corpus.Documents[1];
        var observation = provider.Observations[0] with { LogicalNamespace = "different-namespace" };
        var changedEvidence = fixture.Corpus with { Documents = fixture.Corpus.Documents.SetItem(1, provider with { Observations = [observation] }) };
        Assert.NotEqual(original, Assert.Single(OctoberResolutionPolicy.Plan(changedEvidence).Inquiries).Fingerprint);
        Assert.NotEqual(original, Assert.Single(OctoberResolutionPolicy.Plan(fixture.Corpus, "reviewed-policy-v2").Inquiries).Fingerprint);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Plan_ObservationAndCandidateBounds_RecordsExcludedWorkAsNotEvaluated()
    {
        var fixture = new ResolutionCase();
        var callers = Enumerable.Range(10, 65).Select(id => ResolutionCase.Document(id, "caller")).ToImmutableArray();
        var providers = Enumerable.Range(100, 33).Select(id => ResolutionCase.Document(id, "provider")).ToImmutableArray();
        var corpus = fixture.Corpus with { Documents = callers.AddRange(providers) };
        var plan = OctoberResolutionPolicy.Plan(corpus);
        Assert.Equal(64, plan.Inquiries.Length);
        Assert.All(plan.Inquiries, inquiry => Assert.InRange(inquiry.Candidates.Length, 1, 32));
        Assert.Equal(plan.Inquiries.Length, plan.Inquiries.Select(x => x.Fingerprint).Distinct().Count());
        var examined = plan.Inquiries.Select(x => x.Caller).ToHashSet();
        foreach (var document in callers)
        {
            var observation = new ResolutionObservationRef(document.Version, document.Observations[0].ObservationKey);
            Assert.True(examined.Contains(observation) || plan.Dispositions.Any(x => x.Observation == observation && x.Kind == ResolutionOutcomeKind.NotEvaluated));
        }
        Assert.NotEmpty(plan.Dispositions);
        Assert.All(plan.Dispositions, item => Assert.False(string.IsNullOrWhiteSpace(item.Reason)));
        foreach (var inquiry in plan.Inquiries)
        {
            var omitted = providers.Select(document => new ResolutionObservationRef(document.Version, document.Observations[0].ObservationKey))
                .Except(inquiry.Candidates);
            Assert.All(omitted, observation => Assert.Contains(plan.Dispositions, item => item.Observation == observation && item.Kind == ResolutionOutcomeKind.NotEvaluated));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_QualifiedConnectingInterpretation_AcceptsDirectedStaticRelationshipAndRetainsProvenance()
    {
        var fixture = new ResolutionCase();
        var inquiry = fixture.Inquiry();
        var result = OctoberResolutionPolicy.Validate(fixture.Corpus, inquiry, fixture.Witness());
        Assert.Equal(ResolutionOutcomeKind.Supported, result.Kind);
        var edge = Assert.IsType<AcceptedRelationship>(result.Relationship);
        Assert.Equal(RelationshipKind.References, edge.Kind);
        Assert.Equal(fixture.Caller.Document, edge.From);
        Assert.Equal(fixture.Provider.Document, edge.To);
        Assert.NotEqual(Guid.Empty, edge.RelationshipVersionId);
        Assert.Equal(inquiry.Fingerprint, result.Fingerprint);
        Assert.True(inquiry.Contributors.ToHashSet().SetEquals(result.Contributors));
        Assert.True(fixture.Witness().SupportingAssertions.Select(x => x.Evidence).ToHashSet().IsSubsetOf(edge.SupportingEvidence.ToHashSet()));
        Assert.All(fixture.Corpus.Documents.SelectMany(x => x.Sections).SelectMany(x => x.Claims).SelectMany(x => x.Evidence), evidence =>
        {
            Assert.Null(evidence.Document);
            Assert.Null(evidence.ClaimGroupId);
            Assert.NotNull(evidence.Source);
        });
        Assert.Contains("Deployment is unverified.", Assert.IsType<Uncertainty>(result.Uncertainty).Limitations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing-binding")]
    [InlineData("citation-only")]
    [InlineData("invented-quote")]
    [InlineData("invented-group")]
    [InlineData("foreign-endpoint")]
    [InlineData("wrong-mechanism")]
    [InlineData("wrong-provider-role")]
    [InlineData("deployment-overclaim")]
    [InlineData("decisive-opposition")]
    public void Validate_UnsupportedOrUncheckableWitness_RemainsExplicitUncertainty(string mutation)
    {
        var fixture = new ResolutionCase();
        var witness = fixture.Witness();
        witness = mutation switch
        {
            "missing-binding" => witness with { SupportingAssertions = witness.SupportingAssertions.Where(x => x.Kind != ResolutionAssertionKind.ConnectingBinding).ToImmutableArray() },
            "citation-only" => witness with { BindingInterpretation = "", ConnectionExplanation = "" },
            "invented-quote" => witness with { SupportingAssertions = witness.SupportingAssertions.SetItem(2, witness.SupportingAssertions[2] with { ExactAcceptedText = "This quote was never accepted." }) },
            "invented-group" => witness with { SupportingAssertions = witness.SupportingAssertions.SetItem(2, witness.SupportingAssertions[2] with { ClaimGroupId = "invented-binding" }) },
            "foreign-endpoint" => witness with { Provider = fixture.Provider with { Document = fixture.Provider.Document with { BatchId = Guid.NewGuid() } } },
            "wrong-mechanism" => witness with { Mechanism = "invented-transport" },
            "wrong-provider-role" => witness with { ProviderRole = "caller" },
            "deployment-overclaim" => witness with { ClaimsDeploymentCertainty = true },
            _ => witness with { HasUnresolvedDecisiveOpposition = true, Limitations = ["The opposing namespace evidence is unresolved."] }
        };
        AssertUnresolved(OctoberResolutionPolicy.Validate(fixture.Corpus, fixture.Inquiry(), witness));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_AdvisoryBindingCannotUpgradeSourceClaims_RemainsUnresolved()
    {
        var fixture = new ResolutionCase();
        var caller = fixture.Corpus.Documents[0];
        var section = caller.Sections[0];
        fixture.Corpus = fixture.Corpus with
        {
            Documents = fixture.Corpus.Documents.SetItem(0, caller with
            {
                Sections = [section with { Claims = section.Claims.SetItem(1, section.Claims[1] with { Qualification = ClaimQualification.Advisory }) }]
            })
        };
        AssertUnresolved(OctoberResolutionPolicy.Validate(fixture.Corpus, fixture.Inquiry(), fixture.Witness()));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_SameNamesLiteralsAndCitationCountButNoConnectingFact_DoesNotInferLinkFromCandidateCount()
    {
        var fixture = new ResolutionCase();
        var supported = fixture.Witness();
        var caller = fixture.Corpus.Documents[0];
        var section = caller.Sections[0];
        const string missing = "The shared value is an audit payload. Source does not identify the runtime destination or a connecting binding.";
        fixture.Corpus = fixture.Corpus with
        {
            Documents = fixture.Corpus.Documents.SetItem(0, caller with
            {
                Sections = [section with { Claims = section.Claims.SetItem(1, section.Claims[1] with { Text = missing }) }]
            })
        };
        // The controlled semantic interpreter explicitly identifies the missing binding. This
        // tests preservation/grounding of that judgment, not an invented arbitrary-prose parser.
        var witness = fixture.Witness() with
        {
            BindingInterpretation = "The common literal is audit data, not a routing binding.",
            ConnectionExplanation = "No accepted fact connects the caller target to this candidate provider.",
            HasUnresolvedDecisiveOpposition = true,
            Limitations = ["Missing binding; one candidate is not proof."]
        };
        Assert.Equal(supported.SupportingAssertions.Length, witness.SupportingAssertions.Length);
        Assert.Equal(caller.Observations, fixture.Corpus.Documents[0].Observations);
        Assert.Single(fixture.Inquiry().Candidates);
        AssertUnresolved(OctoberResolutionPolicy.Validate(fixture.Corpus, fixture.Inquiry(), witness));
        // Keeping the old purported proof cannot evade the changed accepted meaning either.
        AssertUnresolved(OctoberResolutionPolicy.Validate(fixture.Corpus, fixture.Inquiry(), supported));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_OpposingNamespaceEvidence_PreservesContradictionInUncertainty()
    {
        var fixture = new ResolutionCase();
        var provider = fixture.Corpus.Documents[1];
        var section = provider.Sections[0];
        var opposition = section.Claims[0] with { ClaimGroupId = "namespace-opposition", Text = "This provider belongs to tenant-b; no mapping from tenant-a is present." };
        fixture.Corpus = fixture.Corpus with { Documents = fixture.Corpus.Documents.SetItem(1, provider with
        {
            Sections = [section with { Claims = section.Claims.Add(opposition) }],
            Observations = [provider.Observations[0] with { LogicalNamespace = "tenant-b" }]
        }) };
        var witness = fixture.Witness() with
        {
            OpposingAssertions = [new(ResolutionAssertionKind.Opposing, provider.Version, section.SectionId, opposition.ClaimGroupId,
                opposition.Evidence[0], opposition.Text, "The namespaces disagree and no alias/configuration mapping resolves them.")],
            HasUnresolvedDecisiveOpposition = true,
            Limitations = ["Decisive namespace mismatch"]
        };
        var result = OctoberResolutionPolicy.Validate(fixture.Corpus, fixture.Inquiry(), witness);
        AssertUnresolved(result);
        Assert.Contains(opposition.Evidence[0], result.Uncertainty!.Evidence);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_AliasInterpretationWithoutAcceptedMappingAssertion_RemainsUnresolved()
    {
        var fixture = new ResolutionCase();
        var witness = fixture.Witness() with { AliasOrConfigurationMapping = "Assume alias invoice-route maps to this provider." };
        AssertUnresolved(OctoberResolutionPolicy.Validate(fixture.Corpus, fixture.Inquiry(), witness));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_B0RawSourceEvidence_CannotReplaceItWithFabricatedDocumentReference()
    {
        var fixture = new ResolutionCase();
        var witness = fixture.Witness();
        var assertion = witness.SupportingAssertions[0];
        Assert.Null(assertion.Evidence.Document);
        Assert.Null(assertion.Evidence.ClaimGroupId);
        Assert.Equal(fixture.Caller.Document, assertion.Document);
        var invented = assertion with { Evidence = assertion.Evidence with { Document = assertion.Document, ClaimGroupId = assertion.ClaimGroupId } };
        AssertUnresolved(OctoberResolutionPolicy.Validate(fixture.Corpus, fixture.Inquiry(),
            witness with { SupportingAssertions = witness.SupportingAssertions.SetItem(0, invented) }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_SupportedWithCutoffAndSourceLimitations_RetainsQualifiersDespiteCallerOmission()
    {
        var fixture = new ResolutionCase();
        var caller = fixture.Corpus.Documents[0];
        var section = caller.Sections[0];
        const string sourceLimit = "Static configuration can be replaced by runtime overrides.";
        caller = caller with { Sections = [section with { Claims = section.Claims.SetItem(1,
            section.Claims[1] with { Limitations = [sourceLimit] }) }] };
        fixture.Corpus = fixture.Corpus with { Documents = fixture.Corpus.Documents.SetItem(0, caller)
            .AddRange(Enumerable.Range(100, 32).Select(id => ResolutionCase.Document(id, "provider"))) };
        var planned = fixture.Inquiry();
        Assert.Equal(32, planned.Candidates.Length);
        Assert.NotEmpty(planned.Limitations);
        var result = OctoberResolutionPolicy.Validate(fixture.Corpus, planned with { Limitations = [] }, fixture.Witness());
        Assert.Equal(ResolutionOutcomeKind.Supported, result.Kind);
        Assert.NotNull(result.Relationship);
        var retained = Assert.IsType<Uncertainty>(result.Uncertainty);
        Assert.Contains(sourceLimit, retained.Limitations);
        Assert.Contains("Deployment is unverified.", retained.Limitations);
        Assert.All(planned.Limitations, limitation => Assert.Contains(limitation, retained.Limitations));
    }

    internal static void AssertUnresolved(ResolutionOutcome result)
    {
        Assert.Equal(ResolutionOutcomeKind.Unresolved, result.Kind);
        Assert.Null(result.Relationship);
        var uncertainty = Assert.IsType<Uncertainty>(result.Uncertainty);
        Assert.False(string.IsNullOrWhiteSpace(uncertainty.Explanation));
        Assert.NotEmpty(uncertainty.Limitations);
    }
}

/// <summary>Controlled accepted-corpus semantics; these records are not real provider/source qualification.</summary>
internal sealed class ResolutionCase
{
    public ResolutionCorpus Corpus { get; set; } = new(
        new(new("ControlledFixture/resolution", "controlled-target", Id(1), new string('a', 64)), Id(2), new string('b', 64)),
        [Document(3, "caller"), Document(4, "provider")]);
    public ResolutionObservationRef Caller => new(Corpus.Documents[0].Version, Corpus.Documents[0].Observations[0].ObservationKey);
    public ResolutionObservationRef Provider => new(Corpus.Documents[1].Version, Corpus.Documents[1].Observations[0].ObservationKey);
    public ResolutionInquiry Inquiry() => Assert.Single(OctoberResolutionPolicy.Plan(Corpus).Inquiries);
    public ResolutionWitness Witness()
    {
        var action = Corpus.Documents[0].Sections[0].Claims[0];
        var provider = Corpus.Documents[1].Sections[0].Claims[0];
        var binding = Corpus.Documents[0].Sections[0].Claims[1];
        return new(Caller, Provider, "SubmitInvoice", "provider", "SubmitInvoice", "message", "tenant-a",
            "The accepted configuration connects invoice-route to the provider's invoice-handler.", null,
            [new(ResolutionAssertionKind.CallerAction, Caller.Document, Corpus.Documents[0].Sections[0].SectionId, action.ClaimGroupId,
                action.Evidence[0], action.Text, "Caller sends the operation."),
             new(ResolutionAssertionKind.ProviderRoleAndOperation, Provider.Document, Corpus.Documents[1].Sections[0].SectionId, provider.ClaimGroupId,
                provider.Evidence[0], provider.Text, "Provider receives the operation."),
             new(ResolutionAssertionKind.ConnectingBinding, Caller.Document, Corpus.Documents[0].Sections[0].SectionId, binding.ClaimGroupId,
                binding.Evidence[0], binding.Text, "The binding configuration links the caller route and receiving handler.")],
            [], "The static configured route connects this caller action to the provider operation; no deployment claim follows.",
            ["Deployment is unverified."], false, false);
    }
    public static ResolutionDocument Document(int id, string role)
    {
        var version = new DocumentVersionReference(Id(1), Id(id * 10), Id(id * 10 + 1));
        Guid repository = Id(1000 + id);
        var origin = new PreparedOrigin(repository, "controlled-root-" + id, "src/Operation.cs", new string('c', 64), "controlled-prepared-v1");
        EvidenceReference Evidence(int start) => new(null, null, new(origin, start, start + 3), null, origin.SourceDigest);
        string action = role == "caller"
            ? "Caller sends SubmitInvoice using message transport to tenant-a invoice-route."
            : "Provider invoice-handler receives SubmitInvoice using message transport in tenant-a.";
        var actionEvidence = Evidence(10);
        var bindingEvidence = Evidence(20);
        ImmutableArray<QualifiedClaimGroup> claims =
        [
            new("action", action, ClaimQualification.SourceSupported, [actionEvidence], []),
            new("binding", "The accepted configuration binds tenant-a invoice-route to provider invoice-handler for SubmitInvoice.", ClaimQualification.SourceSupported, [bindingEvidence], [])
        ];
        return new(version, repository, [new("operation", 0, "Operation", claims)],
            [new("observation-" + id, "operation-" + id, role, "message", "SubmitInvoice", "tenant-a", ["invoice-route", "same-audit-payload"], ClaimQualification.SourceSupported, [actionEvidence, bindingEvidence])],
            [new(ContributorKind.Source, repository, null, origin.RootBindingIdentity, null, origin.SourceDigest)]);
    }
    public static Guid Id(int id) => Guid.Parse($"00000000-0000-0000-0000-{id:000000000000}");
}
