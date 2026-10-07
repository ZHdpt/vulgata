using System.Collections.Immutable;
using System.Text;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

/// <summary>
/// Business-owned versioned canonical encoding. Uses the existing canonical JSON and SHA-256 rules.
/// Invalid shapes will throw ArgumentException; helpers do not verify storage or live authority.
/// </summary>
public static class OctoberBusinessFinalizationPolicy
{
    public const int SchemaVersion = 1;
    public const int MaximumRoots = 256;
    private static readonly UTF8Encoding _utf8 = new(false, true);

    public static string ComputeProviderFingerprint(BusinessProviderConfigurationProjection configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Text(configuration.SourceIdentity);
        Text(configuration.ProfileName);
        Text(configuration.ApiType);
        Text(configuration.Endpoint);
        Text(configuration.Model);
        Text(configuration.CredentialReferenceId);
        // Exact configured spelling is intentional: URI normalization, profile aliases or a
        // credential hash would create a different cross-lane identity contract.
        return Hash(new { domain = "october/business-provider-configuration/1", schemaVersion = SchemaVersion, configuration });
    }

    /// <summary>Excludes ExecutionScope and CanonicalPayloadDigest; explicit empty roots remain valid.</summary>
    public static string ComputeRequestDigest(FinalizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var selection = ValidateSelection(request.Operation, request.Corpus, request.Selection);
        return RequestDigest(request.Operation, request.Corpus, selection);
    }

    /// <summary>Binds request digest, original proposals and dispositions; excludes its own digest field.</summary>
    public static string ComputeOutcomeDigest(FlowFinalizationPublicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var selection = ValidateSelection(request.Operation, request.Corpus, request.Selection);
        if (request.CanonicalPayloadDigest != RequestDigest(request.Operation, request.Corpus, selection))
            throw new ArgumentException("The outcome must bind the exact original request digest.", nameof(request));
        if (request.Completed.IsDefault || request.Dispositions.IsDefault ||
            request.Completed.Length + request.Dispositions.Length != selection.Roots.Length)
            throw new ArgumentException("Every selected root needs exactly one explicit terminal outcome.", nameof(request));
        var remaining = selection.Roots.ToHashSet();
        foreach (var completed in request.Completed)
        {
            if (completed is null || completed.Root is null || !remaining.Remove(completed.Root) || completed.GroupVersionId == Guid.Empty)
                throw new ArgumentException("A completion must pin one selected exact root and group.", nameof(request));
            ValidateProposal(completed.Proposal);
        }
        foreach (var disposition in request.Dispositions)
        {
            if (disposition is null || disposition.Root is null || !remaining.Remove(disposition.Root) ||
                !Enum.IsDefined(disposition.Kind) || disposition.GroupVersionId == Guid.Empty)
                throw new ArgumentException("A disposition must pin one selected root and a valid terminal kind.", nameof(request));
            Text(disposition.Reason);
        }
        if (remaining.Count != 0)
            throw new ArgumentException("Selected roots cannot be omitted from the terminal set.", nameof(request));
        return Hash(new
        {
            domain = "october/business-finalization-outcome/1", schemaVersion = SchemaVersion,
            request.Operation, request.Corpus, request.CanonicalPayloadDigest, selection,
            completed = request.Completed.OrderBy(x => RootKey(x.Root), StringComparer.Ordinal),
            dispositions = request.Dispositions.OrderBy(x => RootKey(x.Root), StringComparer.Ordinal)
        });
    }

    private static BusinessFinalizationSelection ValidateSelection(OperationKey operation, CorpusHandle corpus,
        BusinessFinalizationSelection? selection)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(corpus.Binding);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(selection.Limits);
        if (operation.Kind != OperationKind.FinalizeCorpus || operation.OperationId == Guid.Empty ||
            operation.BatchId == Guid.Empty || corpus.Binding.BatchId != operation.BatchId || corpus.CorpusId == Guid.Empty)
            throw new ArgumentException("Finalization requires exact operation and corpus identities.");
        Text(corpus.Binding.CanonicalDatabaseIdentity);
        Text(corpus.Binding.TargetId);
        Digest(corpus.Binding.SelectionDigest);
        Digest(corpus.CorpusDigest);
        if (selection.Roots.IsDefault || selection.Roots.Length > MaximumRoots || selection.RunId == Guid.Empty || selection.MaximumTokens <= 0 ||
            selection.Limits.MaxNodes is < 1 or > OctoberFlowPlanner.MaximumNodes ||
            selection.Limits.MaxEdges is < 0 or > OctoberFlowPlanner.MaximumEdges ||
            selection.Limits.MaxDepth is < 0 or > OctoberFlowPlanner.MaximumDepth)
            throw new ArgumentException("Finalization selection must be explicit and within the fixed limits.");
        Text(selection.ProviderProfileId);
        Digest(selection.ProviderConfigurationFingerprint);
        Digest(selection.PromptDigest);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var unitVersions = new Dictionary<Guid, DocumentVersionReference>();
        foreach (var root in selection.Roots)
        {
            if (root is null || root.Document is null || root.UnitId == Guid.Empty || root.Document.BatchId != operation.BatchId ||
                root.Document.LogicalDocumentId == Guid.Empty || root.Document.DocumentVersionId == Guid.Empty || !Enum.IsDefined(root.Kind))
                throw new ArgumentException("Each selected root must have an exact same-batch unit and document identity.");
            Text(root.Selector);
            if (!identities.Add(RootKey(root)) || unitVersions.TryGetValue(root.UnitId, out var version) && version != root.Document)
                throw new ArgumentException("Duplicate root identities or conflicting unit versions are invalid.");
            unitVersions[root.UnitId] = root.Document;
        }
        return selection with { Roots = selection.Roots.OrderBy(RootKey, StringComparer.Ordinal).ToImmutableArray() };
    }

    private static string RequestDigest(OperationKey operation, CorpusHandle corpus, BusinessFinalizationSelection selection) =>
        Hash(new { domain = "october/business-finalization-request/1", schemaVersion = SchemaVersion, operation, corpus, selection });

    private static string RootKey(FlowRootSelection root) => OctoberKnowledgePolicy.SerializeCanonical(new
    {
        batchId = root.Document.BatchId, root.UnitId, root.Kind, root.Selector
    });

    private static void ValidateProposal(BusinessSynthesisProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        Text(proposal.Title);
        if (proposal.Sections.IsDefaultOrEmpty) throw new ArgumentException("A completed proposal needs explicit sections.");
        foreach (var section in proposal.Sections)
        {
            if (section is null || section.Ordinal < 0 || section.Claims.IsDefaultOrEmpty)
                throw new ArgumentException("A completed proposal needs ordered sections and claims.");
            Text(section.SectionId);
            Text(section.Title);
            foreach (var claim in section.Claims)
            {
                if (claim is null || !Enum.IsDefined(claim.Qualification) || claim.Inputs.IsDefault || claim.Limitations.IsDefault)
                    throw new ArgumentException("A proposal claim must retain explicit input and limitation arrays.");
                Text(claim.ClaimGroupId);
                Text(claim.Text);
                foreach (var input in claim.Inputs)
                {
                    if (input is null || input.Document is null || input.Document.BatchId == Guid.Empty ||
                        input.Document.LogicalDocumentId == Guid.Empty || input.Document.DocumentVersionId == Guid.Empty)
                        throw new ArgumentException("Claim inputs require exact document identities.");
                    Text(input.SectionId);
                    Text(input.ClaimGroupId);
                }
                foreach (var limitation in claim.Limitations) Text(limitation);
            }
        }
        if (_utf8.GetByteCount(OctoberKnowledgePolicy.SerializeCanonical(proposal)) > OctoberBusinessSynthesisPolicy.MaximumPayloadUtf8Bytes)
            throw new ArgumentException("The synthesis proposal exceeds its canonical payload limit.");
    }

    private static void Text(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Canonical text fields must be nonempty.");
        try { _utf8.GetByteCount(value); }
        catch (EncoderFallbackException error) { throw new ArgumentException("Canonical text must be valid Unicode.", error); }
    }

    private static void Digest(string value)
    {
        if (value is null || value.Length != 64 || !value.All(char.IsAsciiHexDigit))
            throw new ArgumentException("An exact SHA-256 hexadecimal digest is required.");
    }

    private static string Hash<T>(T value) => OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(value));
}
