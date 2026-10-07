using System.Collections.Immutable;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

public sealed record ResolutionObservationRef(DocumentVersionReference Document, string ObservationKey);
public sealed record ResolutionDocument(DocumentVersionReference Version, Guid RepositoryId,
    ImmutableArray<DocumentSection> Sections, ImmutableArray<CommunicationObservation> Observations,
    ImmutableArray<Contributor> Contributors);
public sealed record ResolutionCorpus(CorpusHandle Handle, ImmutableArray<ResolutionDocument> Documents);
public enum ResolutionOutcomeKind { Supported, Unresolved, Failed, NotEvaluated }
public sealed record ResolutionItemDisposition(ResolutionObservationRef Observation, ResolutionOutcomeKind Kind, string Reason);
public sealed record ResolutionInquiry(string Fingerprint, ResolutionObservationRef Caller,
    ImmutableArray<ResolutionObservationRef> Candidates, ImmutableArray<Contributor> Contributors,
    ImmutableArray<string> Limitations);
public sealed record ResolutionPlan(ImmutableArray<ResolutionInquiry> Inquiries,
    ImmutableArray<ResolutionItemDisposition> Dispositions);
public enum ResolutionAssertionKind { CallerAction, ProviderRoleAndOperation, ConnectingBinding, AliasOrConfigurationMapping, Opposing }
public sealed record ResolutionEvidenceAssertion(ResolutionAssertionKind Kind, DocumentVersionReference Document,
    string SectionId, string ClaimGroupId, EvidenceReference Evidence,
    string ExactAcceptedText, string Interpretation);

/// <summary>A proposed interpretation of accepted evidence, never authority to create a relationship.</summary>
public sealed record ResolutionWitness(ResolutionObservationRef Caller, ResolutionObservationRef Provider,
    string CallerAction, string ProviderRole, string ProviderOperation, string Mechanism,
    string NamespaceInterpretation, string BindingInterpretation, string? AliasOrConfigurationMapping,
    ImmutableArray<ResolutionEvidenceAssertion> SupportingAssertions,
    ImmutableArray<ResolutionEvidenceAssertion> OpposingAssertions, string ConnectionExplanation,
    ImmutableArray<string> Limitations, bool HasUnresolvedDecisiveOpposition, bool ClaimsDeploymentCertainty);
public sealed record ResolutionOutcome(string Fingerprint, ResolutionOutcomeKind Kind,
    AcceptedRelationship? Relationship, Uncertainty? Uncertainty, ImmutableArray<Contributor> Contributors);

public interface IOctoberResolutionDisclosureAuthority
{
    Task<DisclosureStatus> CheckAsync(CorpusHandle corpus, ImmutableArray<Contributor> contributors,
        CancellationToken cancellationToken = default);
}

/// <summary>Finite accepted-evidence planning and structural validation of proposed static linkage.</summary>
public static class OctoberResolutionPolicy
{
    public const string PolicyVersion = "october-resolution-v1";

    public static ResolutionPlan Plan(ResolutionCorpus corpus, string policyVersion = PolicyVersion)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyVersion);
        if (corpus.Documents.IsDefault || corpus.Documents.Any(x => x.Observations.IsDefault || x.Sections.IsDefault || x.Contributors.IsDefault) ||
            corpus.Documents.Select(x => x.Version).Distinct().Count() != corpus.Documents.Length ||
            corpus.Documents.Any(x => x.Version.BatchId != corpus.Handle.Binding.BatchId ||
                x.Observations.Select(o => o.ObservationKey).Distinct(StringComparer.Ordinal).Count() != x.Observations.Length))
            throw new ArgumentException("An exact, unambiguous accepted corpus is required.", nameof(corpus));

        var ordered = corpus.Documents.OrderBy(x => Key(x.Version), StringComparer.Ordinal).ToArray();
        var facts = ordered.SelectMany(document => document.Observations.OrderBy(x => x.ObservationKey, StringComparer.Ordinal)
            .Select(observation => (Document: document, Observation: observation,
                Reference: new ResolutionObservationRef(document.Version, observation.ObservationKey)))).ToArray();
        var contributors = ordered.SelectMany(x => x.Contributors).Distinct()
            .OrderBy(OctoberKnowledgePolicy.SerializeCanonical, StringComparer.Ordinal).ToImmutableArray();
        string evidenceDigest = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(ordered.Select(x => new
        {
            x.Version, x.RepositoryId,
            sections = x.Sections.OrderBy(s => s.Ordinal).ThenBy(s => s.SectionId, StringComparer.Ordinal),
            observations = x.Observations.OrderBy(o => o.ObservationKey, StringComparer.Ordinal),
            contributors = x.Contributors.OrderBy(OctoberKnowledgePolicy.SerializeCanonical, StringComparer.Ordinal)
        })));
        var inquiries = ImmutableArray.CreateBuilder<ResolutionInquiry>();
        var dispositions = ImmutableArray.CreateBuilder<ResolutionItemDisposition>();
        foreach (var caller in facts.Where(x => x.Observation.Role == "caller"))
        {
            if (inquiries.Count == 64)
            {
                dispositions.Add(new(caller.Reference, ResolutionOutcomeKind.NotEvaluated, "Observation limit reached."));
                continue;
            }
            var candidates = facts.Where(x => x.Observation.Role == "provider" && x.Document.Version != caller.Document.Version).ToArray();
            var selected = candidates.Take(32).Select(x => x.Reference).ToImmutableArray();
            foreach (var omitted in candidates.Skip(32))
                dispositions.Add(new(omitted.Reference, ResolutionOutcomeKind.NotEvaluated, "Candidate limit reached for " + caller.Observation.ObservationKey + "."));
            ImmutableArray<string> limits = candidates.Length > 32 ? ["Additional candidates were not evaluated."] : [];
            string fingerprint = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new
            {
                corpus.Handle, policyVersion, evidenceDigest, caller = caller.Reference, candidates = selected,
                question = "Assess source-supported static caller-to-provider linkage and opposing evidence."
            }));
            inquiries.Add(new(fingerprint, caller.Reference, selected, contributors, limits));
        }
        foreach (var other in facts.Where(x => x.Observation.Role is not ("caller" or "provider")))
            dispositions.Add(new(other.Reference, ResolutionOutcomeKind.NotEvaluated, "Observation role is not supported by this bounded inquiry."));
        return new(inquiries.ToImmutable(), dispositions.Distinct().ToImmutableArray());
    }

    public static ResolutionOutcome Validate(ResolutionCorpus corpus, ResolutionInquiry inquiry,
        ResolutionWitness witness)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(inquiry);
        ArgumentNullException.ThrowIfNull(witness);
        var plan = Plan(corpus);
        var actual = plan.Inquiries.SingleOrDefault(x => x.Fingerprint == inquiry.Fingerprint && x.Caller == inquiry.Caller);
        var contributors = actual?.Contributors ?? corpus.Documents.SelectMany(x => x.Contributors).Distinct().ToImmutableArray();
        var supporting = witness.SupportingAssertions.IsDefault ? [] : witness.SupportingAssertions;
        var opposing = witness.OpposingAssertions.IsDefault ? [] : witness.OpposingAssertions;
        var validEvidence = supporting.Concat(opposing).Where(x => Grounded(corpus, x)).Select(x => x.Evidence).Distinct().ToImmutableArray();
        var limitations = (witness.Limitations.IsDefault ? ImmutableArray<string>.Empty : witness.Limitations)
            .Concat(actual?.Limitations ?? [])
            .Concat(supporting.Concat(opposing).Where(x => Grounded(corpus, x))
                .SelectMany(x => ResolveClaim(corpus, x)!.Limitations.IsDefault ? [] : ResolveClaim(corpus, x)!.Limitations))
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToImmutableArray();
        ResolutionOutcome Unknown(string reason) => new(inquiry.Fingerprint, ResolutionOutcomeKind.Unresolved, null,
            new("resolution/" + inquiry.Fingerprint, "Static linkage remains unresolved.", validEvidence,
                limitations.Append(reason).Distinct().ToImmutableArray()), contributors);

        if (actual is null || witness.Caller != actual.Caller || !actual.Candidates.Contains(witness.Provider))
            return Unknown("The witness does not identify this exact accepted inquiry.");
        var caller = Observation(corpus, witness.Caller);
        var provider = Observation(corpus, witness.Provider);
        if (caller is null || provider is null || caller.Qualification != ClaimQualification.SourceSupported ||
            provider.Qualification != ClaimQualification.SourceSupported || caller.Role != "caller" || provider.Role != "provider" ||
            witness.ProviderRole != provider.Role || witness.CallerAction != caller.Operation || witness.ProviderOperation != provider.Operation ||
            witness.Mechanism != caller.Mechanism || witness.Mechanism != provider.Mechanism)
            return Unknown("Accepted endpoint roles, actions or mechanisms do not support the proposed direction.");
        if (new[] { witness.CallerAction, witness.ProviderRole, witness.ProviderOperation, witness.Mechanism,
                witness.NamespaceInterpretation, witness.BindingInterpretation, witness.ConnectionExplanation }.Any(string.IsNullOrWhiteSpace))
            return Unknown("A concrete connecting interpretation is missing.");
        if (witness.HasUnresolvedDecisiveOpposition || witness.ClaimsDeploymentCertainty)
            return Unknown("Decisive opposition or unsupported deployment certainty prevents static acceptance.");
        if (supporting.IsEmpty || supporting.Any(x => x.Kind == ResolutionAssertionKind.Opposing || !Grounded(corpus, x)) ||
            opposing.Any(x => x.Kind != ResolutionAssertionKind.Opposing || !Grounded(corpus, x)))
            return Unknown("Every proposed evidence assertion must resolve to exact source-qualified accepted text.");
        bool Has(ResolutionAssertionKind kind, DocumentVersionReference? owner = null) => supporting.Any(x =>
            x.Kind == kind && (owner is null || x.Document == owner));
        if (!Has(ResolutionAssertionKind.CallerAction, witness.Caller.Document) ||
            !Has(ResolutionAssertionKind.ProviderRoleAndOperation, witness.Provider.Document) ||
            !supporting.Any(x => x.Kind == ResolutionAssertionKind.ConnectingBinding &&
                (x.Document == witness.Caller.Document || x.Document == witness.Provider.Document)))
            return Unknown("Caller, provider and connecting-binding assertions require exact endpoint evidence.");
        bool needsMapping = !string.IsNullOrWhiteSpace(witness.AliasOrConfigurationMapping) ||
            !StringComparer.Ordinal.Equals(caller.LogicalNamespace, provider.LogicalNamespace) ||
            !StringComparer.Ordinal.Equals(caller.Operation, provider.Operation);
        if (needsMapping && (string.IsNullOrWhiteSpace(witness.AliasOrConfigurationMapping) || !Has(ResolutionAssertionKind.AliasOrConfigurationMapping)))
            return Unknown("Different namespaces, operations or aliases require accepted explicit mapping evidence.");
        if (string.IsNullOrWhiteSpace(caller.LogicalNamespace) || string.IsNullOrWhiteSpace(provider.LogicalNamespace))
            return Unknown("The accepted namespace binding is incomplete.");

        // Structural grounding does not mechanically prove the interpretation's meaning.
        // Source/provider usefulness qualification remains separate from this bounded acceptance.
        string identity = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new { inquiry.Fingerprint, witness }));
        var relationship = new AcceptedRelationship(new Guid(Convert.FromHexString(identity[..32])), RelationshipKind.References,
            witness.Caller.Document, witness.Provider.Document, witness.ConnectionExplanation,
            supporting.Select(x => x.Evidence).Distinct().ToImmutableArray(), opposing.Select(x => x.Evidence).Distinct().ToImmutableArray());
        Uncertainty? qualification = limitations.IsEmpty ? null : new("resolution/" + inquiry.Fingerprint,
            "The supported static relationship retains evidence limitations; it does not establish deployment certainty.",
            validEvidence, limitations);
        return new(inquiry.Fingerprint, ResolutionOutcomeKind.Supported, relationship, qualification, contributors);
    }

    private static CommunicationObservation? Observation(ResolutionCorpus corpus, ResolutionObservationRef reference) =>
        corpus.Documents.SingleOrDefault(x => x.Version == reference.Document)?.Observations
            .SingleOrDefault(x => x.ObservationKey == reference.ObservationKey);

    private static bool Grounded(ResolutionCorpus corpus, ResolutionEvidenceAssertion assertion)
    {
        if (!Enum.IsDefined(assertion.Kind) || string.IsNullOrWhiteSpace(assertion.ExactAcceptedText) ||
            string.IsNullOrWhiteSpace(assertion.Interpretation) ||
            assertion.Evidence.Source is null || assertion.Evidence.AdvisoryRecordId is not null) return false;
        var document = corpus.Documents.SingleOrDefault(x => x.Version == assertion.Document);
        if (document is null) return false;
        var claim = ResolveClaim(corpus, assertion);
        return claim is not null && claim.Qualification == ClaimQualification.SourceSupported &&
            claim.Text == assertion.ExactAcceptedText && claim.Evidence.Contains(assertion.Evidence) &&
            assertion.Evidence.Source.Origin.RepositoryId == document.RepositoryId;
    }

    private static QualifiedClaimGroup? ResolveClaim(ResolutionCorpus corpus, ResolutionEvidenceAssertion assertion)
    {
        var document = corpus.Documents.SingleOrDefault(x => x.Version == assertion.Document);
        if (document is null) return null;
        var sections = document.Sections.Where(x => x.SectionId == assertion.SectionId).ToArray();
        if (sections.Length != 1 || sections[0].Claims.IsDefault) return null;
        var claims = sections[0].Claims.Where(x => x.ClaimGroupId == assertion.ClaimGroupId).ToArray();
        return claims.Length == 1 ? claims[0] : null;
    }

    private static string Key(DocumentVersionReference version) => version.BatchId.ToString("D") + "/" +
        version.LogicalDocumentId.ToString("D") + "/" + version.DocumentVersionId.ToString("D");
}
