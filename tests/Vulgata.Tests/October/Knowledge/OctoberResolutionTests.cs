using System.Collections.Immutable;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using ApiType = Vulgata.Core.Entities.ApiType;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberResolutionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RetainedV2_OutputOnlyBudgetCannotAffordInput_DeniesBeforeAttemptOrClientSendAsync()
    {
        var f = new RetainedResolutionCase();
        var inquiry = Assert.Single(OctoberResolutionPolicy.Plan(f.Corpus).Inquiries);
        Guid run = Guid.NewGuid();
        var operation = new OperationKey(f.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        await using var host = new OctoberExecutionScope(run, "selected-profile", new()
        {
            OutputStage = "cl-and-bl", WorkerCap = 1, MaximumTotalTokens = 100,
            SourceRules = [new(f.Corpus.Documents[0].RepositoryId, "flows", "selected", "Controlled budget check")]
        }, (_, _) => Task.FromResult(ProviderAdmissionStatus.Granted), operation);
        var scope = new RetainedReservationScope(host);
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(new ChatResponse(
            new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(f.Witness(inquiry))))
        { FinishReason = ChatFinishReason.Stop, Usage = new() { InputTokenCount = 10, OutputTokenCount = 5 } }));
        var result = await new OctoberResolutionExecutor(client, "Resolve static references only; instructions also consume input budget.")
            .ExecuteAsync(f.Corpus, inquiry, operation, run, "selected-profile", 40, scope, new ResolutionDisclosure());
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, result.AdmissionFailure);
        Assert.Null(result.Outcome);
        Assert.Empty(result.Attempts);
        Assert.Equal(0, client.Calls);
        Assert.True(Assert.Single(scope.Requests).MaximumTokens > 100);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void RetainedV2_OneObservationWithFortyExactTargets_RetainsThirtyTwoCapAndExplicitOmissions()
    {
        var f = new RetainedResolutionCase();
        var caller = f.Corpus.Documents[0];
        var sourceClaim = caller.Sections[0].Claims[0];
        var paths = Enumerable.Range(0, 40).Select(index => "flows/candidate" + index.ToString("D2") + ".flw").ToImmutableArray();
        sourceClaim = sourceClaim with { Text = string.Join(" ", paths.Select(path => "<flowEmbedOp flow=\"" + path + "\" />")) };
        caller = caller with
        {
            Sections = [caller.Sections[0] with { Claims = [sourceClaim] }],
            Observations = [caller.Observations[0] with { Operation = "declared flow references", LiteralsOrExpressions = paths }]
        };
        var targets = paths.Select((path, index) =>
        {
            var claim = f.Target.Sections[0].Claims[0];
            var evidence = claim.Evidence[0];
            var origin = evidence.Source!.Origin with { RelativePath = path };
            return f.Target with
            {
                Version = new(f.Target.Version.BatchId, ResolutionCase.Id(7500 + index), ResolutionCase.Id(7600 + index)),
                Sections = [f.Target.Sections[0] with { Claims = [claim with
                { Evidence = [evidence with { Source = evidence.Source with { Origin = origin } }] }] }]
            };
        }).ToImmutableArray();
        var corpus = f.Corpus with { Documents = targets.Insert(0, caller) };
        var plan = OctoberResolutionPolicy.Plan(corpus);
        Assert.Equal(32, plan.Inquiries.Length);
        Assert.All(plan.Inquiries, inquiry => Assert.Equal(f.Caller, inquiry.Caller));
        var selected = plan.Inquiries.SelectMany(inquiry => inquiry.Candidates).Select(reference => reference.Document).ToHashSet();
        Assert.Equal(32, selected.Count);
        var omitted = targets.Where(target => !selected.Contains(target.Version)).ToArray();
        Assert.Equal(8, omitted.Length);
        Assert.All(omitted, target => Assert.Contains(plan.Dispositions, disposition =>
            disposition.Observation.Document == target.Version && disposition.Kind == ResolutionOutcomeKind.NotEvaluated && !string.IsNullOrWhiteSpace(disposition.Reason)));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(plan), OctoberKnowledgePolicy.SerializeCanonical(
            OctoberResolutionPolicy.Plan(corpus with { Documents = corpus.Documents.Reverse().ToImmutableArray() })));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedV2_EndpointPacket_IsBoundedWithoutDroppingOwnerDependenciesAsync(bool oversizedEndpoint)
    {
        var f = new RetainedResolutionCase();
        var hidden = new Contributor(ContributorKind.GlobalAdvisory, null, null, null, ResolutionCase.Id(7390), new string('d', 64));
        var target = f.Target;
        var claim = target.Sections[0].Claims[0];
        var hugeClaim = claim with { Text = claim.Text + "UNRELATED_OR_OVERSIZED_MARKER" + new string('x', 2 * 1024 * 1024) };
        var corpus = f.Corpus;
        if (oversizedEndpoint)
            corpus = corpus with { Documents = corpus.Documents.SetItem(1, target with { Sections = [target.Sections[0] with { Claims = [hugeClaim] }] }) };
        else
        {
            var evidence = hugeClaim.Evidence[0];
            var origin = evidence.Source!.Origin with { RelativePath = "src/unrelated.cs" };
            var unrelated = target with
            {
                Version = target.Version with { LogicalDocumentId = ResolutionCase.Id(7391), DocumentVersionId = ResolutionCase.Id(7392) },
                Sections = [target.Sections[0] with { Claims = [hugeClaim with { Evidence = [evidence with { Source = evidence.Source with { Origin = origin } }] }] }],
                Contributors = target.Contributors.Add(hidden)
            };
            corpus = corpus with { Documents = corpus.Documents.Add(unrelated) };
        }
        var inquiry = Assert.Single(OctoberResolutionPolicy.Plan(corpus).Inquiries);
        Guid run = Guid.NewGuid();
        var operation = new OperationKey(corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        int grants = 0;
        await using var scope = new OctoberExecutionScope(run, "selected-profile", new()
        {
            OutputStage = "cl-and-bl", WorkerCap = 1, MaximumTotalTokens = 1_000_000,
            SourceRules = [new(corpus.Documents[0].RepositoryId, "flows", "selected", "Controlled retained evidence")]
        }, (_, _) => { grants++; return Task.FromResult(ProviderAdmissionStatus.Granted); }, operation);
        var recordingScope = new RetainedReservationScope(scope);
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(new ChatResponse(
            new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(f.Witness(inquiry))))
        { FinishReason = ChatFinishReason.Stop, Usage = new() { InputTokenCount = 10, OutputTokenCount = 5 } }));
        var authority = new PacketLedgerAuthority(corpus.Documents.SelectMany(document => document.Contributors).Distinct().ToImmutableArray());
        var result = await new OctoberResolutionExecutor(client, "Only accepted exact static artifact evidence.")
            .ExecuteAsync(corpus, inquiry, operation, run, "selected-profile", 40, recordingScope, authority);
        if (oversizedEndpoint)
        {
            Assert.ThrowsAny<ArgumentException>(() => OctoberRetainedResolutionPolicy.CreateEvidencePacket(corpus, inquiry));
            Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, result.AdmissionFailure);
            Assert.Equal(0, grants);
            Assert.Equal(0, client.Calls);
            Assert.Empty(recordingScope.Requests);
            Assert.Null(result.Outcome);
        }
        else
        {
            Assert.Equal(ResolutionOutcomeKind.Supported, result.Outcome!.Kind);
            Assert.Contains(hidden, result.Outcome.Contributors);
            Assert.True(authority.Calls > 0);
            var sent = Assert.Single(client.Requests);
            string payload = Assert.Single(sent.Messages.Where(message => message.Role == ChatRole.User)).Text!;
            string instructions = Assert.Single(sent.Messages.Where(message => message.Role == ChatRole.System)).Text!;
            Assert.True(Assert.Single(recordingScope.Requests).MaximumTokens >=
                Encoding.UTF8.GetByteCount(instructions) + Encoding.UTF8.GetByteCount(payload) + 40L);
            Assert.Equal(40, client.LastMaximumOutputTokens);
            Assert.True(Encoding.UTF8.GetByteCount(payload) <= 1024 * 1024);
            Assert.DoesNotContain("UNRELATED_OR_OVERSIZED_MARKER", payload, StringComparison.Ordinal);
            using var parsed = JsonDocument.Parse(payload);
            Assert.Equal(2, parsed.RootElement.GetProperty("Documents").GetArrayLength());
            Assert.False(parsed.RootElement.TryGetProperty("Contributors", out _));
        }
    }

    private sealed class PacketLedgerAuthority(ImmutableArray<Contributor> expected) : IOctoberResolutionDisclosureAuthority
    {
        public int Calls { get; private set; }
        public Task<DisclosureStatus> CheckAsync(CorpusHandle corpus, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default)
        {
            Calls++;
            Assert.True(expected.ToHashSet().SetEquals(contributors));
            return Task.FromResult(DisclosureStatus.Allowed);
        }
    }

    private sealed class RetainedReservationScope(OctoberExecutionScope host) : IOctoberExecutionScope, IOctoberBusinessHandoffLifetime
    {
        public List<ProviderAttemptRequest> Requests { get; } = [];
        public CancellationToken HandoffCancellation => ((IOctoberBusinessHandoffLifetime)host).HandoffCancellation;
        public ProviderAdmissionStatus CheckHandoffEligibility(Guid runId, OperationKey operation, string profile) =>
            ((IOctoberBusinessHandoffLifetime)host).CheckHandoffEligibility(runId, operation, profile);
        public Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return host.AcquireProviderAttemptAsync(request, cancellationToken);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void RetainedV2_NullProfileAndBindingToken_PreserveExplicitLegacyCanonicalBytes()
    {
        var f = new ResolutionCase();
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(new { f.Corpus.Handle, f.Corpus.Documents }),
            OctoberKnowledgePolicy.SerializeCanonical(f.Corpus));
        var witness = f.Witness();
        var legacy = new { witness.Caller, witness.Provider, witness.CallerAction, witness.ProviderRole,
            witness.ProviderOperation, witness.Mechanism, witness.NamespaceInterpretation, witness.BindingInterpretation,
            witness.AliasOrConfigurationMapping, witness.SupportingAssertions, witness.OpposingAssertions,
            witness.ConnectionExplanation, witness.Limitations, witness.HasUnresolvedDecisiveOpposition, witness.ClaimsDeploymentCertainty };
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(legacy), OctoberKnowledgePolicy.SerializeCanonical(witness));
        string digest = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new { f.Inquiry().Fingerprint, witness = legacy }));
        Assert.Equal(new Guid(Convert.FromHexString(digest[..32])), OctoberResolutionPolicy.Validate(f.Corpus, f.Inquiry(), witness).Relationship!.RelationshipVersionId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void RetainedV2_ExactSourceDeclaredFlowReference_ProjectsArtifactAndPreservesStaticLimitation()
    {
        var f = new RetainedResolutionCase();
        string before = OctoberKnowledgePolicy.SerializeCanonical(f.Corpus);
        var projected = OctoberRetainedResolutionPolicy.ProjectEndpoints(f.Corpus);
        var target = projected.Documents.Single(document => document.Version == f.Target.Version);
        var artifact = Assert.Single(target.Observations.Where(observation => observation.Role == OctoberRetainedResolutionPolicy.ArtifactRole));
        Assert.StartsWith(OctoberRetainedResolutionPolicy.EndpointKeyPrefix, artifact.ObservationKey, StringComparison.Ordinal);
        Assert.Empty(f.Target.Observations);
        var inquiry = Assert.Single(OctoberResolutionPolicy.Plan(f.Corpus).Inquiries);
        Assert.Equal(f.Caller, inquiry.Caller);
        Assert.Equal(new ResolutionObservationRef(f.Target.Version, artifact.ObservationKey), Assert.Single(inquiry.Candidates));
        var outcome = OctoberResolutionPolicy.Validate(f.Corpus, inquiry, f.Witness(inquiry));
        Assert.Equal(ResolutionOutcomeKind.Supported, outcome.Kind);
        Assert.Equal(RelationshipKind.References, outcome.Relationship!.Kind);
        Assert.Equal(f.Caller.Document, outcome.Relationship.From);
        Assert.Equal(f.Target.Version, outcome.Relationship.To);
        Assert.Contains(OctoberRetainedResolutionPolicy.RuntimeLimitation, outcome.Uncertainty!.Limitations);
        Assert.True(f.Corpus.Documents.SelectMany(document => document.Contributors).ToHashSet().SetEquals(outcome.Contributors));
        Assert.Equal(before, OctoberKnowledgePolicy.SerializeCanonical(f.Corpus));
        Assert.Empty(OctoberResolutionPolicy.Plan(f.Corpus with { ResolutionPolicyVersion = null }).Inquiries);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("advisory")]
    [InlineData("name-only")]
    [InlineData("expression")]
    [InlineData("wrong-root")]
    [InlineData("wrong-repository")]
    [InlineData("ambiguous-artifact")]
    [InlineData("ambiguous-multi-origin")]
    [InlineData("role-only")]
    public void RetainedV2_UnsupportedOrAmbiguousEvidence_DoesNotCreateEligibleReference(string mutation)
    {
        var f = new RetainedResolutionCase();
        var corpus = f.Corpus;
        var caller = corpus.Documents[0];
        var observation = caller.Observations[0];
        if (mutation == "advisory") caller = caller with { Observations = [observation with { Qualification = ClaimQualification.Advisory }] };
        if (mutation == "name-only") caller = caller with { Observations = [observation with { Operation = "loginPwdCheck", LiteralsOrExpressions = ["loginPwdCheck"] }] };
        if (mutation == "expression") caller = caller with { Observations = [observation with { Operation = "${selectedFlow}", LiteralsOrExpressions = ["${selectedFlow}"] }] };
        if (mutation == "role-only") caller = caller with { Observations = [observation with { Role = "caller", Mechanism = "unclassified" }] };
        corpus = corpus with { Documents = corpus.Documents.SetItem(0, caller) };
        if (mutation is "wrong-root" or "wrong-repository")
        {
            var target = corpus.Documents[1];
            var claim = target.Sections[0].Claims[0];
            var evidence = claim.Evidence[0];
            var origin = evidence.Source!.Origin with
            {
                RootBindingIdentity = mutation == "wrong-root" ? "other-root" : evidence.Source.Origin.RootBindingIdentity,
                RepositoryId = mutation == "wrong-repository" ? Guid.NewGuid() : target.RepositoryId
            };
            corpus = corpus with { Documents = corpus.Documents.SetItem(1, target with
            {
                RepositoryId = origin.RepositoryId,
                Sections = [target.Sections[0] with { Claims = [claim with { Evidence = [evidence with { Source = evidence.Source with { Origin = origin } }] }] }],
                Contributors = [new(ContributorKind.Source, origin.RepositoryId, null, origin.RootBindingIdentity, null, origin.SourceDigest)]
            }) };
        }
        if (mutation == "ambiguous-artifact") corpus = corpus with
        { Documents = corpus.Documents.Add(f.Target with { Version = f.Target.Version with { LogicalDocumentId = Guid.NewGuid(), DocumentVersionId = Guid.NewGuid() } }) };
        if (mutation == "ambiguous-multi-origin")
        {
            var targetClaim = f.Target.Sections[0].Claims[0];
            var evidence = targetClaim.Evidence[0];
            var otherOrigin = evidence.Source!.Origin with { RelativePath = "flows/another-origin.flw" };
            var secondClaim = targetClaim with { ClaimGroupId = "other-origin",
                Evidence = [evidence with { Source = evidence.Source with { Origin = otherOrigin } }] };
            var competing = f.Target with
            {
                Version = f.Target.Version with { LogicalDocumentId = Guid.NewGuid(), DocumentVersionId = Guid.NewGuid() },
                Sections = [f.Target.Sections[0] with { Claims = [targetClaim, secondClaim] }]
            };
            corpus = corpus with { Documents = corpus.Documents.Add(competing) };
        }
        Assert.Empty(OctoberResolutionPolicy.Plan(corpus).Inquiries);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing-token")]
    [InlineData("foreign-token")]
    [InlineData("reverse")]
    [InlineData("opposition")]
    [InlineData("runtime-certainty")]
    public void RetainedV2_WitnessCannotUpgradeWrongBindingDirectionOrOpposition(string mutation)
    {
        var f = new RetainedResolutionCase();
        var inquiry = Assert.Single(OctoberResolutionPolicy.Plan(f.Corpus).Inquiries);
        var witness = f.Witness(inquiry);
        witness = mutation switch
        {
            "missing-token" => witness with { BindingToken = null },
            "foreign-token" => witness with { BindingToken = "flows/other.flw" },
            "reverse" => witness with { Caller = witness.Provider, Provider = witness.Caller },
            "opposition" => witness with { HasUnresolvedDecisiveOpposition = true },
            "runtime-certainty" => witness with { ClaimsDeploymentCertainty = true },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        var outcome = OctoberResolutionPolicy.Validate(f.Corpus, inquiry, witness);
        Assert.Equal(ResolutionOutcomeKind.Unresolved, outcome.Kind);
        Assert.Null(outcome.Relationship);
        Assert.NotNull(outcome.Uncertainty);
    }

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
        Assert.Throws<ArgumentException>(() => OctoberResolutionPolicy.Plan(fixture.Corpus, "reviewed-policy-v2"));
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

/// <summary>Controlled source-qualified CL, not actual source execution or model evidence.</summary>
internal sealed class RetainedResolutionCase
{
    public const string Token = "flows/login/loginPwdCheck.flw";
    public ResolutionCorpus Corpus { get; }
    public ResolutionDocument Target => Corpus.Documents[1];
    public ResolutionObservationRef Caller => new(Corpus.Documents[0].Version, "declared-flow");
    public RetainedResolutionCase()
    {
        Guid repository = ResolutionCase.Id(7300);
        ResolutionDocument Document(int id, string path, string text, bool caller)
        {
            var version = new DocumentVersionReference(ResolutionCase.Id(1), ResolutionCase.Id(id), ResolutionCase.Id(id + 1));
            var origin = new PreparedOrigin(repository, "retained-controlled-root", path, new string('c', 64), "retained-v1");
            var evidence = new EvidenceReference(null, null, new(origin, 1, 3), null, origin.SourceDigest);
            var claim = new QualifiedClaimGroup("declared", text, ClaimQualification.SourceSupported, [evidence], []);
            ImmutableArray<CommunicationObservation> observations = caller
                ? [new("declared-flow", "flow-root", "嵌入子流程", "flowEmbedOp", Token, null, [Token], ClaimQualification.SourceSupported, [evidence])]
                : [];
            return new(version, repository, [new("flow", 0, "Declared flow", [claim])], observations,
                [new(ContributorKind.Source, repository, null, origin.RootBindingIdentity, null, origin.SourceDigest)]);
        }
        Corpus = new(new(new("ControlledFixture/retained-v2", "controlled-target", ResolutionCase.Id(1), new string('a', 64)),
            ResolutionCase.Id(2), new string('b', 64)),
            [Document(7310, "flows/login/main.flw", "<flowEmbedOp flow=\"flows/login/loginPwdCheck.flw\" />", true),
             Document(7320, Token, "<flow id=\"loginPwdCheck\"><node id=\"check-password\" /></flow>", false)])
        { ResolutionPolicyVersion = OctoberResolutionPolicy.RetainedPolicyVersion };
    }
    public ResolutionWitness Witness(ResolutionInquiry inquiry)
    {
        var provider = Assert.Single(inquiry.Candidates);
        var endpoint = OctoberResolutionPolicy.ResolveObservation(Corpus, provider)!;
        var caller = Corpus.Documents[0].Observations[0];
        ResolutionEvidenceAssertion Assertion(ResolutionAssertionKind kind, ResolutionDocument document)
        {
            var claim = document.Sections[0].Claims[0];
            return new(kind, document.Version, "flow", claim.ClaimGroupId, claim.Evidence[0], claim.Text,
                "Accepted declaration identifies this exact static resource reference.");
        }
        return new(Caller, provider, caller.Operation, endpoint.Role, endpoint.Operation, caller.Mechanism,
            "Same retained repository/root", "The declaration names the exact retained flow artifact.", null,
            [Assertion(ResolutionAssertionKind.CallerAction, Corpus.Documents[0]),
             Assertion(ResolutionAssertionKind.ProviderRoleAndOperation, Target),
             Assertion(ResolutionAssertionKind.ConnectingBinding, Corpus.Documents[0])],
            [], "The caller declares a static reference to the retained flow artifact.", [], false, false)
        { BindingToken = Token };
    }
}
