using System.Collections.Immutable;
using System.Text;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

public sealed record ResolutionPacketDocument(DocumentVersionReference Version, Guid RepositoryId,
    ImmutableArray<DocumentSection> Sections, ImmutableArray<CommunicationObservation> Observations);

/// <summary>Exact endpoint evidence only; the complete authorization ledger stays owner-held.</summary>
public sealed record ResolutionEvidencePacket(CorpusHandle Corpus, string ResolutionPolicyVersion,
    string InquiryFingerprint, ResolutionObservationRef Caller,
    ImmutableArray<ResolutionObservationRef> Candidates, ImmutableArray<ResolutionPacketDocument> Documents,
    ImmutableArray<string> Limitations);

/// <summary>
/// Static declared resource references from retained source-qualified evidence. Projected endpoints
/// identify exact artifacts, never proven runtime routing or callable service implementations.
/// </summary>
public static class OctoberRetainedResolutionPolicy
{
    public const string ArtifactRole = "accepted-flow-artifact";
    public const string EndpointKeyPrefix = "__retained_flow_artifact/";
    public const string RuntimeLimitation = "Static declared resource reference only; runtime resource mapping and entrypoint dispatch are not proved.";
    public const int MaximumPacketUtf8Bytes = 1024 * 1024;

    public static ResolutionCorpus ProjectEndpoints(ResolutionCorpus corpus)
    {
        ValidateCorpus(corpus);
        return corpus with
        {
            Documents = corpus.Documents.Select(document => Artifact(document) is { } artifact
                ? document with { Observations = document.Observations.Add(artifact.Observation) } : document).ToImmutableArray()
        };
    }

    public static CommunicationObservation? ResolveObservation(ResolutionCorpus corpus, ResolutionObservationRef reference)
    {
        var projected = ProjectEndpoints(corpus);
        return projected.Documents.SingleOrDefault(document => document.Version == reference.Document)?.Observations
            .SingleOrDefault(observation => observation.ObservationKey == reference.ObservationKey);
    }

    public static ResolutionPlan Plan(ResolutionCorpus corpus)
    {
        ValidateCorpus(corpus);
        var documents = corpus.Documents.OrderBy(document => Key(document.Version), StringComparer.Ordinal).ToArray();
        var artifacts = documents.Select(Artifact).Where(artifact => artifact is not null).Cast<ArtifactEndpoint>().ToArray();
        var contributors = Contributors(corpus);
        string evidenceDigest = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(documents.Select(document => new
        {
            document.Version, document.RepositoryId,
            sections = document.Sections.OrderBy(section => section.Ordinal).ThenBy(section => section.SectionId, StringComparer.Ordinal),
            observations = document.Observations.OrderBy(observation => observation.ObservationKey, StringComparer.Ordinal),
            contributors = document.Contributors.OrderBy(OctoberKnowledgePolicy.SerializeCanonical, StringComparer.Ordinal)
        })));
        var inquiries = ImmutableArray.CreateBuilder<ResolutionInquiry>();
        var dispositions = ImmutableArray.CreateBuilder<ResolutionItemDisposition>();
        foreach (var document in documents)
        foreach (var observation in document.Observations.OrderBy(value => value.ObservationKey, StringComparer.Ordinal))
        {
            var caller = new ResolutionObservationRef(document.Version, observation.ObservationKey);
            var pairs = Candidates(document, observation, artifacts, documents)
                .OrderBy(value => Key(value.Artifact.Document.Version), StringComparer.Ordinal).ToArray();
            if (pairs.Length == 0)
            {
                dispositions.Add(new(caller, ResolutionOutcomeKind.NotEvaluated,
                    "No unique same-repository/root source-declared retained flow artifact was established."));
                continue;
            }
            foreach (var omitted in pairs.Skip(32))
                dispositions.Add(new(new(omitted.Artifact.Document.Version, omitted.Artifact.Observation.ObservationKey),
                    ResolutionOutcomeKind.NotEvaluated, "Candidate limit reached for " + Key(caller.Document) + "/" + caller.ObservationKey + "."));
            ImmutableArray<string> inquiryLimitations = pairs.Length > 32
                ? [RuntimeLimitation, "Additional declared resource candidates were not evaluated."] : [RuntimeLimitation];
            foreach (var pair in pairs.Take(32))
            {
                var target = new ResolutionObservationRef(pair.Artifact.Document.Version, pair.Artifact.Observation.ObservationKey);
                if (inquiries.Count == 64)
                {
                    dispositions.Add(new(caller, ResolutionOutcomeKind.NotEvaluated,
                        "Retained reference inquiry limit reached for target " + Key(target.Document) + "."));
                    continue;
                }
                string fingerprint = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new
                {
                    domain = "october/retained-resource-inquiry/2", corpus.Handle,
                    policyVersion = OctoberResolutionPolicy.RetainedPolicyVersion, evidenceDigest, caller, target,
                    bindingTokens = pair.Tokens,
                    question = "Assess only the declared static resource reference to this exact retained artifact."
                }));
                inquiries.Add(new(fingerprint, caller, [target], contributors, inquiryLimitations));
            }
            if (InvocationTokens(observation).Any(token => !pairs.Any(pair => pair.Tokens.Contains(token))))
                dispositions.Add(new(caller, ResolutionOutcomeKind.NotEvaluated,
                    "Additional declared paths lack a unique retained artifact binding; no runtime routing was inferred."));
        }
        return new(inquiries.ToImmutable(), dispositions.Distinct().ToImmutableArray());
    }

    public static ResolutionOutcome Validate(ResolutionCorpus corpus, ResolutionInquiry inquiry, ResolutionWitness witness)
    {
        ArgumentNullException.ThrowIfNull(inquiry);
        ArgumentNullException.ThrowIfNull(witness);
        var actual = Plan(corpus).Inquiries.SingleOrDefault(value => value.Fingerprint == inquiry.Fingerprint && value.Caller == inquiry.Caller);
        var supporting = witness.SupportingAssertions.IsDefault ? [] : witness.SupportingAssertions;
        var opposing = witness.OpposingAssertions.IsDefault ? [] : witness.OpposingAssertions;
        var grounded = supporting.Concat(opposing).Where(assertion => Grounded(corpus, assertion)).ToArray();
        var evidence = grounded.Select(assertion => assertion.Evidence).Distinct().ToImmutableArray();
        var limitations = (witness.Limitations.IsDefault ? ImmutableArray<string>.Empty : witness.Limitations)
            .Concat(actual?.Limitations ?? [RuntimeLimitation])
            .Concat(grounded.SelectMany(assertion => Claim(corpus, assertion)!.Limitations))
            .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToImmutableArray();
        var contributors = Contributors(corpus);
        ResolutionOutcome Unknown(string reason) => new(inquiry.Fingerprint, ResolutionOutcomeKind.Unresolved, null,
            new("resolution/" + inquiry.Fingerprint, "Static resource linkage remains unresolved.", evidence,
                limitations.Add(reason)), contributors);
        if (actual is null || witness.Caller != actual.Caller || !actual.Candidates.Contains(witness.Provider))
            return Unknown("The witness does not pin this exact directed resource inquiry.");
        var callerDocument = corpus.Documents.Single(document => document.Version == actual.Caller.Document);
        var caller = callerDocument.Observations.Single(observation => observation.ObservationKey == actual.Caller.ObservationKey);
        var artifacts = corpus.Documents.Select(Artifact).Where(value => value is not null).Cast<ArtifactEndpoint>().ToArray();
        var pair = Candidates(callerDocument, caller, artifacts, corpus.Documents).SingleOrDefault(candidate =>
            candidate.Artifact.Document.Version == witness.Provider.Document && candidate.Artifact.Observation.ObservationKey == witness.Provider.ObservationKey);
        if (pair is null || witness.BindingToken is null || !pair.Tokens.Contains(witness.BindingToken) ||
            witness.CallerAction != caller.Operation || witness.Mechanism != caller.Mechanism ||
            witness.ProviderRole != ArtifactRole || witness.ProviderOperation != pair.Artifact.Observation.Operation)
            return Unknown("The declared binding token and exact retained endpoints do not support this witness.");
        if (witness.HasUnresolvedDecisiveOpposition || witness.ClaimsDeploymentCertainty ||
            !string.IsNullOrWhiteSpace(witness.AliasOrConfigurationMapping) ||
            new[] { witness.CallerAction, witness.Mechanism, witness.NamespaceInterpretation, witness.BindingInterpretation,
                witness.ConnectionExplanation }.Any(string.IsNullOrWhiteSpace))
            return Unknown("Missing interpretation, inferred aliases, decisive opposition or runtime certainty prevents acceptance.");
        if (supporting.IsEmpty || supporting.Any(assertion => assertion.Kind == ResolutionAssertionKind.Opposing || !Grounded(corpus, assertion)) ||
            opposing.Any(assertion => assertion.Kind != ResolutionAssertionKind.Opposing || !Grounded(corpus, assertion)))
            return Unknown("Every assertion must preserve an exact source-qualified accepted claim and evidence locator.");
        bool CallerAssertion(ResolutionEvidenceAssertion assertion) => assertion.Document == callerDocument.Version &&
            caller.Evidence.Any(value => Overlaps(value.Source, assertion.Evidence.Source));
        if (!supporting.Any(assertion => assertion.Kind == ResolutionAssertionKind.CallerAction && CallerAssertion(assertion)) ||
            !supporting.Any(assertion => assertion.Kind == ResolutionAssertionKind.ProviderRoleAndOperation &&
                assertion.Document == pair.Artifact.Document.Version && SameOrigin(assertion.Evidence.Source!.Origin, pair.Artifact.Origin)) ||
            !supporting.Any(assertion => assertion.Kind == ResolutionAssertionKind.ConnectingBinding && CallerAssertion(assertion) &&
                ContainsToken(assertion.ExactAcceptedText, witness.BindingToken)))
            return Unknown("Caller action, exact artifact evidence and explicit connecting literal require grounded endpoint assertions.");
        string identity = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new
        { domain = "october/retained-resource-reference/2", inquiry.Fingerprint, witness }));
        var relationship = new AcceptedRelationship(new Guid(Convert.FromHexString(identity[..32])), RelationshipKind.References,
            witness.Caller.Document, witness.Provider.Document,
            "Declared static resource reference to the retained flow artifact; runtime mapping is unverified.",
            supporting.Select(assertion => assertion.Evidence).Distinct().ToImmutableArray(),
            opposing.Select(assertion => assertion.Evidence).Distinct().ToImmutableArray());
        return new(inquiry.Fingerprint, ResolutionOutcomeKind.Supported, relationship,
            new("resolution/" + inquiry.Fingerprint, RuntimeLimitation, evidence, limitations), contributors);
    }

    public static ResolutionEvidencePacket CreateEvidencePacket(ResolutionCorpus corpus, ResolutionInquiry inquiry)
    {
        var actual = Plan(corpus).Inquiries.SingleOrDefault(value => value.Fingerprint == inquiry.Fingerprint && value.Caller == inquiry.Caller)
            ?? throw new ArgumentException("An exact retained resource inquiry is required.", nameof(inquiry));
        var selected = actual.Candidates.Select(candidate => candidate.Document).Append(actual.Caller.Document).ToHashSet();
        var projected = ProjectEndpoints(corpus);
        var packet = new ResolutionEvidencePacket(corpus.Handle, OctoberResolutionPolicy.RetainedPolicyVersion, actual.Fingerprint,
            actual.Caller, actual.Candidates, projected.Documents.Where(document => selected.Contains(document.Version))
                .OrderBy(document => Key(document.Version), StringComparer.Ordinal)
                .Select(document => new ResolutionPacketDocument(document.Version, document.RepositoryId,
                    document.Sections, document.Observations)).ToImmutableArray(), actual.Limitations);
        if (Encoding.UTF8.GetByteCount(OctoberKnowledgePolicy.SerializeCanonical(packet)) > MaximumPacketUtf8Bytes)
            throw new ArgumentException("Exact endpoint evidence exceeds the bounded resolution packet.", nameof(corpus));
        return packet;
    }

    private sealed record ArtifactEndpoint(ResolutionDocument Document, PreparedOrigin Origin, CommunicationObservation Observation);
    private sealed record Candidate(ArtifactEndpoint Artifact, ImmutableArray<string> Tokens);

    private static ArtifactEndpoint? Artifact(ResolutionDocument document)
    {
        var evidence = document.Sections.SelectMany(section => section.Claims)
            .Where(claim => claim.Qualification == ClaimQualification.SourceSupported)
            .SelectMany(claim => claim.Evidence).Where(value => SourceEvidence(document, value)).Distinct().ToImmutableArray();
        var origins = evidence.Select(value => Normalized(value.Source!.Origin)).Distinct().ToArray();
        if (origins.Length != 1 || !ExactRelativeFlowPath(origins[0].RelativePath)) return null;
        var origin = origins[0];
        string key = EndpointKeyPrefix + OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new
            { document.Version, origin }));
        var observation = new CommunicationObservation(key, key, ArtifactRole, "retained-flow-artifact",
            origin.RelativePath, origin.RootBindingIdentity, [origin.RelativePath], ClaimQualification.SourceSupported, evidence);
        return new(document, origin, observation);
    }

    private static IEnumerable<Candidate> Candidates(ResolutionDocument document, CommunicationObservation observation,
        ArtifactEndpoint[] artifacts, IEnumerable<ResolutionDocument> retainedDocuments)
    {
        if (observation.Qualification != ClaimQualification.SourceSupported || observation.Evidence.IsDefaultOrEmpty ||
            !ExplicitInvocation(observation.Mechanism) || string.IsNullOrWhiteSpace(observation.Operation)) return [];
        var origins = observation.Evidence.Where(evidence => SourceEvidence(document, evidence))
            .Select(evidence => Normalized(evidence.Source!.Origin)).Distinct().ToArray();
        if (origins.Length != 1 || observation.Evidence.Any(evidence => !SourceEvidence(document, evidence))) return [];
        var matches = new List<(ArtifactEndpoint Artifact, string Token)>();
        foreach (string token in InvocationTokens(observation))
        {
            if (!document.Sections.SelectMany(section => section.Claims).Any(claim =>
                claim.Qualification == ClaimQualification.SourceSupported && ContainsToken(claim.Text, token) &&
                claim.Evidence.Any(evidence => SourceEvidence(document, evidence) &&
                    observation.Evidence.Any(source => Overlaps(source.Source, evidence.Source))))) continue;
            string path = token.EndsWith(".flw", StringComparison.Ordinal) ? token : "src/main/resources/flow/" + token + ".flw";
            // Inspect all accepted source claims before filtering to projectable endpoints.
            // A competing document with several source origins is still evidence of ambiguity,
            // even though it cannot itself become one unambiguous artifact endpoint.
            var bindings = retainedDocuments.Where(owner => owner.RepositoryId == document.RepositoryId)
                .SelectMany(owner => owner.Sections.SelectMany(section => section.Claims)
                    .Where(claim => claim.Qualification == ClaimQualification.SourceSupported)
                    .SelectMany(claim => claim.Evidence).Where(evidence => SourceEvidence(owner, evidence))
                    .Select(evidence => new { owner.Version, Origin = Normalized(evidence.Source!.Origin) }))
                .Where(binding => binding.Origin.RootBindingIdentity == origins[0].RootBindingIdentity && binding.Origin.RelativePath == path)
                .Distinct().Take(2).ToArray();
            if (bindings.Length != 1) continue;
            var targets = artifacts.Where(artifact => artifact.Document.RepositoryId == document.RepositoryId &&
                artifact.Origin.RootBindingIdentity == origins[0].RootBindingIdentity && artifact.Origin.RelativePath == path).ToArray();
            if (targets.Length != 1 || targets[0].Document.Version == document.Version ||
                targets[0].Document.Version != bindings[0].Version || targets[0].Origin != bindings[0].Origin) continue;
            matches.Add((targets[0], token));
        }
        return matches.GroupBy(match => match.Artifact.Document.Version).Select(group => new Candidate(group.First().Artifact,
            group.Select(match => match.Token).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray()));
    }

    private static IEnumerable<string> InvocationTokens(CommunicationObservation observation) =>
        observation.LiteralsOrExpressions.IsDefault ? [] : observation.LiteralsOrExpressions
            .Where(value => value is not null)
            .Select(value => value.StartsWith("*flowPath=", StringComparison.Ordinal) ? value[10..] : value)
            .Where(ValidToken).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    private static bool ExplicitInvocation(string value) => !string.IsNullOrWhiteSpace(value) &&
        (ContainsToken(value, "flowEmbedOp") || ContainsToken(value, "flowEmbedOp.execute") || ContainsToken(value, "IFlowTool.execute"));

    private static bool ValidToken(string value) => value.Length <= 512 && value.Contains('/') &&
        !value.StartsWith('/') && !value.EndsWith('/') &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '/' or '_' or '-' or '.') &&
        value.Split('/').All(segment => segment.Length != 0 && segment is not ("." or ".."));

    private static bool ExactRelativeFlowPath(string path) => ValidToken(path) && path.EndsWith(".flw", StringComparison.Ordinal);

    private static bool ContainsToken(string text, string token)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token)) return false;
        static bool Identifier(char value) => char.IsAsciiLetterOrDigit(value) || value is '_' or '/' or '.' or '-';
        int start = 0;
        while ((start = text.IndexOf(token, start, StringComparison.Ordinal)) >= 0)
        {
            int end = start + token.Length;
            if ((start == 0 || !Identifier(text[start - 1])) && (end == text.Length || !Identifier(text[end]))) return true;
            start++;
        }
        return false;
    }

    private static bool SourceEvidence(ResolutionDocument document, EvidenceReference evidence) => evidence is not null &&
        evidence.Source is { Origin: not null } source && evidence.AdvisoryRecordId is null && source.StartLine > 0 && source.EndLine >= source.StartLine &&
        source.Origin.RepositoryId == document.RepositoryId && evidence.ContentDigest == source.Origin.SourceDigest &&
        document.Contributors.Any(contributor => contributor.Kind == ContributorKind.Source && contributor.RepositoryId == document.RepositoryId &&
            contributor.RootBindingIdentity == source.Origin.RootBindingIdentity && contributor.ContentDigest == source.Origin.SourceDigest);

    private static bool Overlaps(SourceAnchor? left, SourceAnchor? right) => left is not null && right is not null &&
        SameOrigin(left.Origin, right.Origin) && left.StartLine <= right.EndLine && right.StartLine <= left.EndLine;
    private static PreparedOrigin Normalized(PreparedOrigin origin) => origin with { RelativePath = OctoberKnowledgePolicy.NormalizePath(origin.RelativePath) };
    private static bool SameOrigin(PreparedOrigin left, PreparedOrigin right) => Normalized(left) == Normalized(right);
    private static QualifiedClaimGroup? Claim(ResolutionCorpus corpus, ResolutionEvidenceAssertion assertion) =>
        corpus.Documents.SingleOrDefault(document => document.Version == assertion.Document)?.Sections
            .SingleOrDefault(section => section.SectionId == assertion.SectionId)?.Claims.SingleOrDefault(claim => claim.ClaimGroupId == assertion.ClaimGroupId);
    private static bool Grounded(ResolutionCorpus corpus, ResolutionEvidenceAssertion assertion)
    {
        if (assertion is null || !Enum.IsDefined(assertion.Kind) || string.IsNullOrWhiteSpace(assertion.Interpretation)) return false;
        var document = corpus.Documents.SingleOrDefault(value => value.Version == assertion.Document);
        var claim = Claim(corpus, assertion);
        return document is not null && claim is not null && claim.Qualification == ClaimQualification.SourceSupported &&
            claim.Text == assertion.ExactAcceptedText && claim.Evidence.Contains(assertion.Evidence) && SourceEvidence(document, assertion.Evidence);
    }
    private static ImmutableArray<Contributor> Contributors(ResolutionCorpus corpus) => corpus.Documents.SelectMany(document => document.Contributors)
        .Distinct().OrderBy(OctoberKnowledgePolicy.SerializeCanonical, StringComparer.Ordinal).ToImmutableArray();
    private static string Key(DocumentVersionReference version) => OctoberKnowledgePolicy.SerializeCanonical(version);
    private static void ValidateCorpus(ResolutionCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        if (corpus.ResolutionPolicyVersion != OctoberResolutionPolicy.RetainedPolicyVersion || corpus.Handle?.Binding is null ||
            corpus.Documents.IsDefault || corpus.Documents.Any(document => document is null || document.Version is null ||
                document.Version.BatchId != corpus.Handle.Binding.BatchId || document.RepositoryId == Guid.Empty ||
                document.Observations.IsDefault || document.Sections.IsDefault || document.Contributors.IsDefault ||
                document.Contributors.Any(contributor => contributor is null) ||
                document.Observations.Any(observation => observation is null || string.IsNullOrWhiteSpace(observation.ObservationKey) ||
                    observation.ObservationKey.StartsWith(EndpointKeyPrefix, StringComparison.Ordinal)) ||
                document.Observations.Select(observation => observation.ObservationKey).Distinct().Count() != document.Observations.Length ||
                document.Sections.Any(section => section is null || section.Claims.IsDefault || section.Claims.Any(claim => claim is null ||
                    claim.Evidence.IsDefault || claim.Limitations.IsDefault))) ||
            corpus.Documents.Select(document => document.Version).Distinct().Count() != corpus.Documents.Length)
            throw new ArgumentException("An exact retained v2 corpus is required.", nameof(corpus));
    }
}
