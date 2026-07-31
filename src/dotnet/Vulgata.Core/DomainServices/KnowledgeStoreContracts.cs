using System.Text;
using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public sealed record RepositoryAccessScope
{
    public RepositoryAccessScope(IReadOnlyCollection<Guid> authorizedRepositoryIds)
    {
        ArgumentNullException.ThrowIfNull(authorizedRepositoryIds);
        Guid[] ids = authorizedRepositoryIds.ToArray();
        if (ids.Length is < 1 or > 256 ||
            ids.Any(id => id == Guid.Empty) ||
            ids.Distinct().Count() != ids.Length)
        {
            throw new KnowledgeStoreContractException(KnowledgeRejectionCode.InvalidBound);
        }

        AuthorizedRepositoryIds = ids;
    }

    public IReadOnlyCollection<Guid> AuthorizedRepositoryIds { get; }

    public bool Contains(Guid repositoryId) => AuthorizedRepositoryIds.Contains(repositoryId);
}

public sealed record SourceAnchorDraft(
    Guid Id,
    Guid DocumentVersionId,
    Guid RepositoryId,
    Guid ScanRunId,
    Guid CodeUnitId,
    string ProfiledRevision,
    string RepositoryRelativePath,
    string EvidenceLocator,
    int LineStart,
    int LineEnd,
    string SourceHash);

public sealed record EdgeDraft(
    Guid Id,
    Guid SourceDocumentVersionId,
    Guid SourceRepositoryId,
    Guid SourceScanRunId,
    Guid TargetDocumentVersionId,
    Guid TargetRepositoryId,
    Guid TargetScanRunId,
    DocumentEdgeType EdgeType);

public sealed record FlowTraceStepDraft(
    Guid Id,
    int StepOrder,
    Guid DocumentVersionId,
    Guid DocumentRepositoryId,
    Guid DocumentScanRunId,
    Guid? EdgeId,
    FlowTraceStepKind StepKind,
    string Description);

public sealed record FlowTraceDraft(
    Guid Id,
    int TraceOrder,
    IReadOnlyList<FlowTraceStepDraft> Steps);

public sealed record FlowGroupDraft(
    Guid Id,
    int GroupOrder,
    string Title,
    IReadOnlyList<FlowTraceDraft> Traces);

public sealed record KnowledgeCommit(
    Guid OperationId,
    Guid LogicalDocumentId,
    Guid? ExpectedCurrentVersionId,
    Guid NewDocumentVersionId,
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    CanonicalDocumentContent Content,
    IReadOnlyList<SourceAnchorDraft> SourceAnchors,
    IReadOnlyList<EdgeDraft> Edges,
    IReadOnlyList<FlowGroupDraft> FlowGroups,
    DateTimeOffset CommittedAt);

public sealed record KnowledgeCommitResult(
    KnowledgeCommitStatus Status,
    Guid? LogicalDocumentId = null,
    Guid? DocumentVersionId = null,
    KnowledgeRejectionCode? RejectionCode = null)
{
    public static KnowledgeCommitResult Committed(Guid logicalDocumentId, Guid documentVersionId) =>
        new(KnowledgeCommitStatus.Committed, logicalDocumentId, documentVersionId);

    public static KnowledgeCommitResult Conflict() => new(KnowledgeCommitStatus.Conflict);

    public static KnowledgeCommitResult Rejected(KnowledgeRejectionCode code) =>
        new(KnowledgeCommitStatus.Rejected, RejectionCode: code);
}

public enum RebuildProjectionStatus
{
    Rebuilt,
    NotFoundOrUnauthorized,
    Rejected,
}

public sealed record CurrentKnowledge(
    Guid LogicalDocumentId,
    Guid DocumentVersionId,
    Guid RepositoryId,
    DocumentType DocumentType,
    string Title,
    string RepositoryRelativePath,
    string ContentJson,
    string ContentMarkdown,
    IReadOnlyList<CurrentSourceAnchor> SourceAnchors,
    IReadOnlyList<CurrentRelationship> Relationships,
    IReadOnlyList<CurrentFlowGroup> FlowGroups,
    CurrentIndexEntry IndexEntry,
    IReadOnlyList<CurrentDocumentSection> Sections);

public sealed record CurrentSourceAnchor(
    Guid Id,
    Guid CodeUnitId,
    string RepositoryRelativePath,
    string EvidenceLocator,
    int LineStart,
    int LineEnd,
    string SourceHash);

public abstract record CurrentRelationship;

public sealed record AuthorizedRelationship(
    Guid EdgeId,
    DocumentEdgeType EdgeType,
    Guid SourceDocumentVersionId,
    Guid TargetDocumentVersionId,
    Guid TargetRepositoryId) : CurrentRelationship;

public sealed record RestrictedRelationship : CurrentRelationship
{
    public bool IsRestrictedRelationship => true;
}

public sealed record CurrentFlowGroup(
    Guid Id,
    int GroupOrder,
    string Title,
    IReadOnlyList<CurrentFlowTrace> Traces);

public sealed record CurrentFlowTrace(
    Guid Id,
    int TraceOrder,
    IReadOnlyList<CurrentFlowTraceStep> Steps);

public sealed record CurrentFlowTraceStep(
    Guid Id,
    int StepOrder,
    FlowTraceStepKind StepKind,
    Guid? DocumentVersionId,
    string? Description,
    bool IsRestrictedRelationship);

public sealed record CurrentIndexEntry(
    string Summary,
    IReadOnlyList<string> KeySymbols,
    IReadOnlyList<string> BusinessTerms,
    IReadOnlyList<string> TechnicalTerms,
    string SearchText);

public sealed record CurrentDocumentSection(
    Guid StableSectionId,
    int SectionOrder,
    DocumentSectionKind SectionKind,
    string Heading,
    string ContentMarkdown,
    string SearchText,
    IReadOnlyList<Guid> SourceAnchorIds,
    IReadOnlyList<Guid> AuthorizedDocumentVersionIds,
    bool HasRestrictedDocumentReference);

public static class KnowledgeStoreContractValidator
{
    public static void ValidateRequestedStableSectionIds(
        IReadOnlyCollection<Guid> requestedStableSectionIds)
    {
        ArgumentNullException.ThrowIfNull(requestedStableSectionIds);
        Guid[] ids = requestedStableSectionIds.ToArray();
        if (ids.Length > 100 ||
            ids.Any(id => id == Guid.Empty) ||
            ids.Distinct().Count() != ids.Length)
        {
            Reject(KnowledgeRejectionCode.InvalidBound);
        }
    }

    public static void ValidateCommit(KnowledgeCommit request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireIds(
            request.OperationId,
            request.LogicalDocumentId,
            request.NewDocumentVersionId,
            request.RepositoryId,
            request.ScanRunId);
        if (request.ExpectedCurrentVersionId == Guid.Empty ||
            !IsLowercaseGitObjectId(request.ProfiledRevision))
        {
            Reject(KnowledgeRejectionCode.InvalidOwnership);
        }

        ArgumentNullException.ThrowIfNull(request.Content);
        ArgumentNullException.ThrowIfNull(request.SourceAnchors);
        ArgumentNullException.ThrowIfNull(request.Edges);
        ArgumentNullException.ThrowIfNull(request.FlowGroups);
        if (request.SourceAnchors.Count > KnowledgeDocumentLimits.MaximumAnchors ||
            request.Edges.Count > KnowledgeDocumentLimits.MaximumEdges ||
            request.FlowGroups.Count > KnowledgeDocumentLimits.MaximumFlowGroups)
        {
            Reject(KnowledgeRejectionCode.InvalidBound);
        }

        ValidateAnchors(request);
        ValidateEdges(request);
        ValidateFlows(request);
        ValidateSectionReferences(request);
    }

    private static void ValidateAnchors(KnowledgeCommit request)
    {
        foreach (SourceAnchorDraft anchor in request.SourceAnchors)
        {
            RequireIds(
                anchor.Id,
                anchor.DocumentVersionId,
                anchor.RepositoryId,
                anchor.ScanRunId,
                anchor.CodeUnitId);
            if (anchor.DocumentVersionId != request.NewDocumentVersionId ||
                anchor.RepositoryId != request.RepositoryId ||
                anchor.ScanRunId != request.ScanRunId ||
                !StringComparer.Ordinal.Equals(anchor.ProfiledRevision, request.ProfiledRevision) ||
                anchor.LineStart < 1 ||
                anchor.LineEnd < anchor.LineStart ||
                !IsSha256(anchor.SourceHash) ||
                !IsBoundedText(anchor.EvidenceLocator, KnowledgeDocumentLimits.MaximumShortTextBytes) ||
                !IsSafeRepositoryRelativePath(anchor.RepositoryRelativePath))
            {
                Reject(KnowledgeRejectionCode.InvalidAnchor);
            }
        }

        if (request.SourceAnchors.Select(anchor => anchor.Id).Distinct().Count() !=
            request.SourceAnchors.Count)
        {
            Reject(KnowledgeRejectionCode.InvalidAnchor);
        }
    }

    private static void ValidateEdges(KnowledgeCommit request)
    {
        foreach (EdgeDraft edge in request.Edges)
        {
            RequireIds(
                edge.Id,
                edge.SourceDocumentVersionId,
                edge.SourceRepositoryId,
                edge.SourceScanRunId,
                edge.TargetDocumentVersionId,
                edge.TargetRepositoryId,
                edge.TargetScanRunId);
            if (edge.SourceDocumentVersionId != request.NewDocumentVersionId ||
                edge.SourceRepositoryId != request.RepositoryId ||
                edge.SourceScanRunId != request.ScanRunId)
            {
                Reject(KnowledgeRejectionCode.InvalidOwnership);
            }

            if (edge.EdgeType is not DocumentEdgeType.References and
                    not DocumentEdgeType.GeneratedFrom ||
                edge.SourceDocumentVersionId == edge.TargetDocumentVersionId ||
                (edge.EdgeType == DocumentEdgeType.GeneratedFrom &&
                    request.Content.DocumentType != DocumentType.BusinessLogic))
            {
                Reject(KnowledgeRejectionCode.InvalidEdgeSemantic);
            }
        }

        if (request.Edges.Select(edge => edge.Id).Distinct().Count() != request.Edges.Count ||
            request.Edges
                .Select(edge => (
                    edge.SourceDocumentVersionId,
                    edge.TargetDocumentVersionId,
                    edge.EdgeType))
                .Distinct()
                .Count() != request.Edges.Count)
        {
            Reject(KnowledgeRejectionCode.InvalidEdgeSemantic);
        }
    }

    private static void ValidateFlows(KnowledgeCommit request)
    {
        if (request.FlowGroups.Count > 0 &&
            request.Content.DocumentType != DocumentType.BusinessLogic)
        {
            Reject(KnowledgeRejectionCode.InvalidFlow);
        }

        for (int groupIndex = 0; groupIndex < request.FlowGroups.Count; groupIndex++)
        {
            FlowGroupDraft group = request.FlowGroups[groupIndex];
            RequireIds(group.Id);
            if (group.GroupOrder != groupIndex + 1 ||
                !IsBoundedText(group.Title, KnowledgeDocumentLimits.MaximumShortTextBytes) ||
                group.Traces.Count is < 1 or > KnowledgeDocumentLimits.MaximumTracesPerGroup)
            {
                Reject(KnowledgeRejectionCode.InvalidFlow);
            }

            for (int traceIndex = 0; traceIndex < group.Traces.Count; traceIndex++)
            {
                FlowTraceDraft trace = group.Traces[traceIndex];
                RequireIds(trace.Id);
                if (trace.TraceOrder != traceIndex + 1 ||
                    trace.Steps.Count is < 1 or > KnowledgeDocumentLimits.MaximumStepsPerTrace)
                {
                    Reject(KnowledgeRejectionCode.InvalidFlow);
                }

                ValidateSteps(request, trace);
            }
        }
    }

    private static void ValidateSteps(KnowledgeCommit request, FlowTraceDraft trace)
    {
        for (int stepIndex = 0; stepIndex < trace.Steps.Count; stepIndex++)
        {
            FlowTraceStepDraft step = trace.Steps[stepIndex];
            RequireIds(
                step.Id,
                step.DocumentVersionId,
                step.DocumentRepositoryId,
                step.DocumentScanRunId);
            if (step.StepKind is not FlowTraceStepKind.Document and
                    not FlowTraceStepKind.Relationship and
                    not FlowTraceStepKind.Termination ||
                step.StepOrder != stepIndex + 1 ||
                !IsBoundedText(step.Description, KnowledgeDocumentLimits.MaximumShortTextBytes) ||
                (stepIndex == 0 && step.EdgeId is not null))
            {
                Reject(KnowledgeRejectionCode.InvalidFlow);
            }

            if (step.StepKind == FlowTraceStepKind.Relationship)
            {
                if (stepIndex == 0 || step.EdgeId is null)
                {
                    Reject(KnowledgeRejectionCode.InvalidFlow);
                }

                FlowTraceStepDraft previous = trace.Steps[stepIndex - 1];
                bool exactEdge = request.Edges.Any(edge =>
                    edge.Id == step.EdgeId &&
                    edge.SourceDocumentVersionId == previous.DocumentVersionId &&
                    edge.TargetDocumentVersionId == step.DocumentVersionId);
                if (!exactEdge)
                {
                    Reject(KnowledgeRejectionCode.InvalidFlow);
                }
            }
            else if (step.EdgeId is not null)
            {
                Reject(KnowledgeRejectionCode.InvalidFlow);
            }
        }
    }

    private static void ValidateSectionReferences(KnowledgeCommit request)
    {
        HashSet<Guid> anchorIds = request.SourceAnchors.Select(anchor => anchor.Id).ToHashSet();
        HashSet<Guid> permittedVersionIds =
        [
            request.NewDocumentVersionId,
            .. request.Edges.Select(edge => edge.SourceDocumentVersionId),
            .. request.Edges.Select(edge => edge.TargetDocumentVersionId),
        ];
        foreach (CanonicalSection section in request.Content.Sections)
        {
            if (section.SourceAnchorIds.Any(id => !anchorIds.Contains(id)) ||
                section.DocumentVersionIds.Any(id => !permittedVersionIds.Contains(id)))
            {
                Reject(KnowledgeRejectionCode.InvalidContent);
            }
        }
    }

    private static bool IsLowercaseGitObjectId(string value) =>
        value is { Length: > 0 and <= 64 } &&
        value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSha256(string value) =>
        value is { Length: 64 } &&
        value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSafeRepositoryRelativePath(string value)
    {
        if (!IsBoundedText(value, KnowledgeDocumentLimits.MaximumRepositoryPathBytes))
        {
            return false;
        }

        string normalized = value.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        return !normalized.StartsWith('/') &&
            !(normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) &&
                normalized[1] == ':') &&
            segments.All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    private static bool IsBoundedText(string value, int maximumBytes) =>
        !string.IsNullOrWhiteSpace(value) &&
        Encoding.UTF8.GetByteCount(value.Normalize(NormalizationForm.FormC)) <= maximumBytes;

    private static void RequireIds(params Guid[] ids)
    {
        if (ids.Any(id => id == Guid.Empty))
        {
            Reject(KnowledgeRejectionCode.InvalidOwnership);
        }
    }

    private static void Reject(KnowledgeRejectionCode code) =>
        throw new KnowledgeStoreContractException(code);
}
