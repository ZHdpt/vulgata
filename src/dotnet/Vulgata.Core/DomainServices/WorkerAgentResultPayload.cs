using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Closed allowlist reserved for the payload of a <see cref="WorkerAgentResultEnvelope"/>
/// with <see cref="WorkerAgentResultOutcome.Valid"/>. Story 11.2 validates shape only
/// and does not consume any payload field; Story 11.3 supplies the LLM-backed content.
/// Ad-hoc fields are rejected; adding fields in Story 11.3 must reopen the allowlist
/// deliberately.
/// </summary>
public abstract class WorkerAgentResultPayload
{
    private protected WorkerAgentResultPayload()
    {
    }

    public abstract WorkerAgentJobKind? Kind { get; }

    /// <summary>
    /// Reserved marker. Story 11.2 sets this to a fixed empty shape; Story 11.3
    /// will introduce typed payload fields by reopening the closed allowlist.
    /// </summary>
    public static WorkerAgentResultPayload Reserved { get; } = new ReservedPayload();

    private sealed class ReservedPayload : WorkerAgentResultPayload
    {
        public override WorkerAgentJobKind? Kind => null;
    }
}

/// <summary>
/// Typed Pass 1 result returned by a Worker Agent before knowledge commit.
/// </summary>
public sealed class Pass1CodeLogicDocumentResultPayload : WorkerAgentResultPayload
{
    private Pass1CodeLogicDocumentResultPayload(
        Guid jobId,
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        Guid codeUnitId,
        string profiledRevision,
        string repositoryRelativePath,
        int lineStart,
        int lineEnd,
        string sourceHash,
        string fileIdentityHash,
        CanonicalDocumentContent content,
        IReadOnlyList<WorkerAgentSourceAnchor> sourceAnchors,
        IReadOnlyList<Guid> referenceDocumentVersionIds,
        IReadOnlyList<Guid> communicationEvidenceIds)
    {
        JobId = jobId;
        SystemId = systemId;
        RepositoryId = repositoryId;
        ScanRunId = scanRunId;
        CodeUnitId = codeUnitId;
        ProfiledRevision = profiledRevision;
        RepositoryRelativePath = repositoryRelativePath;
        LineStart = lineStart;
        LineEnd = lineEnd;
        SourceHash = sourceHash;
        FileIdentityHash = fileIdentityHash;
        Content = content;
        SourceAnchors = sourceAnchors;
        ReferenceDocumentVersionIds = referenceDocumentVersionIds;
        CommunicationEvidenceIds = communicationEvidenceIds;
    }

    public override WorkerAgentJobKind? Kind => WorkerAgentJobKind.Pass1CodeLogicDocument;
    public Guid JobId { get; }
    public Guid SystemId { get; }
    public Guid RepositoryId { get; }
    public Guid ScanRunId { get; }
    public Guid CodeUnitId { get; }
    public string ProfiledRevision { get; }
    public string RepositoryRelativePath { get; }
    public int LineStart { get; }
    public int LineEnd { get; }
    public string SourceHash { get; }
    public string FileIdentityHash { get; }
    public CanonicalDocumentContent Content { get; }
    public IReadOnlyList<WorkerAgentSourceAnchor> SourceAnchors { get; }
    public IReadOnlyList<Guid> ReferenceDocumentVersionIds { get; }
    public IReadOnlyList<Guid> CommunicationEvidenceIds { get; }

    public static Pass1CodeLogicDocumentResultPayload Create(
        Guid jobId,
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        Guid codeUnitId,
        string profiledRevision,
        string repositoryRelativePath,
        int lineStart,
        int lineEnd,
        string sourceHash,
        string fileIdentityHash,
        CanonicalDocumentContent content,
        IReadOnlyList<WorkerAgentSourceAnchor> sourceAnchors,
        IReadOnlyList<Guid> referenceDocumentVersionIds,
        IReadOnlyList<Guid> communicationEvidenceIds)
    {
        if (new[] { jobId, systemId, repositoryId, scanRunId, codeUnitId }
            .Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Pass 1 result identifiers must be non-empty.");
        }

        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(sourceAnchors);
        ArgumentNullException.ThrowIfNull(referenceDocumentVersionIds);
        ArgumentNullException.ThrowIfNull(communicationEvidenceIds);

        return new(
            jobId,
            systemId,
            repositoryId,
            scanRunId,
            codeUnitId,
            profiledRevision,
            repositoryRelativePath,
            lineStart,
            lineEnd,
            sourceHash,
            fileIdentityHash,
            content,
            Array.AsReadOnly(sourceAnchors.ToArray()),
            Array.AsReadOnly(referenceDocumentVersionIds.ToArray()),
            Array.AsReadOnly(communicationEvidenceIds.ToArray()));
    }
}

/// <summary>
/// One ordered, evidence-linked non-technical step in a prepared business
/// flow. Evidence is referenced only by the prepared trace order.
/// </summary>
public sealed class WorkerAgentBusinessMainStep
{
    private WorkerAgentBusinessMainStep(
        int order,
        string description,
        IReadOnlyList<int> traceEvidenceOrders)
    {
        Order = order;
        Description = description;
        TraceEvidenceOrders = traceEvidenceOrders;
    }

    public int Order { get; }
    public string Description { get; }
    public IReadOnlyList<int> TraceEvidenceOrders { get; }

    public static WorkerAgentBusinessMainStep Create(
        int order,
        string description,
        IReadOnlyList<int> traceEvidenceOrders)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(order, 1, nameof(order));
        ArgumentNullException.ThrowIfNull(traceEvidenceOrders);
        if (traceEvidenceOrders.Count == 0 ||
            traceEvidenceOrders.Any(value => value < 1) ||
            !IsStrictlyAscending(traceEvidenceOrders))
        {
            throw new ArgumentException(
                "Business flow evidence orders must be non-empty and canonical.",
                nameof(traceEvidenceOrders));
        }

        string boundedDescription = WorkerAgentContractText.RequireBoundedSafeText(
            description,
            KnowledgeDocumentLimits.MaximumSectionMarkdownBytes,
            nameof(description));
        return new(
            order,
            boundedDescription,
            Array.AsReadOnly(traceEvidenceOrders.ToArray()));
    }

    private static bool IsStrictlyAscending(IReadOnlyList<int> values)
    {
        for (int index = 1; index < values.Count; index++)
        {
            if (values[index - 1] >= values[index])
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Typed prepared Pass 2 result. It can request only exact synthesis
/// provenance through <see cref="GeneratedFromDocumentVersionIds"/>; there is
/// deliberately no base REFERENCES request field.
/// </summary>
public sealed class Pass2BusinessLogicDocumentResultPayload : WorkerAgentResultPayload
{
    private Pass2BusinessLogicDocumentResultPayload(
        Guid jobId,
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        Guid flowId,
        string purpose,
        IReadOnlyList<WorkerAgentBusinessMainStep> mainSteps,
        string outcome,
        CanonicalDocumentContent content,
        IReadOnlyList<WorkerAgentSourceAnchor> sourceAnchors,
        IReadOnlyList<int> traceEvidenceOrders,
        IReadOnlyList<Guid> generatedFromDocumentVersionIds)
    {
        JobId = jobId;
        SystemId = systemId;
        RepositoryId = repositoryId;
        ScanRunId = scanRunId;
        ProfiledRevision = profiledRevision;
        FlowId = flowId;
        Purpose = purpose;
        MainSteps = mainSteps;
        Outcome = outcome;
        Content = content;
        SourceAnchors = sourceAnchors;
        TraceEvidenceOrders = traceEvidenceOrders;
        GeneratedFromDocumentVersionIds = generatedFromDocumentVersionIds;
    }

    public override WorkerAgentJobKind? Kind => WorkerAgentJobKind.Pass2BusinessLogicDocument;
    public Guid JobId { get; }
    public Guid SystemId { get; }
    public Guid RepositoryId { get; }
    public Guid ScanRunId { get; }
    public string ProfiledRevision { get; }
    public Guid FlowId { get; }
    public string Purpose { get; }
    public IReadOnlyList<WorkerAgentBusinessMainStep> MainSteps { get; }
    public string Outcome { get; }
    public CanonicalDocumentContent Content { get; }
    public IReadOnlyList<WorkerAgentSourceAnchor> SourceAnchors { get; }
    public IReadOnlyList<int> TraceEvidenceOrders { get; }
    public IReadOnlyList<Guid> GeneratedFromDocumentVersionIds { get; }

    public static Pass2BusinessLogicDocumentResultPayload Create(
        Guid jobId,
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        Guid flowId,
        string purpose,
        IReadOnlyList<WorkerAgentBusinessMainStep> mainSteps,
        string outcome,
        CanonicalDocumentContent content,
        IReadOnlyList<WorkerAgentSourceAnchor> sourceAnchors,
        IReadOnlyList<int> traceEvidenceOrders,
        IReadOnlyList<Guid> generatedFromDocumentVersionIds)
    {
        if (new[] { jobId, systemId, repositoryId, scanRunId, flowId }
            .Any(identifier => identifier == Guid.Empty))
        {
            throw new ArgumentException("Pass 2 result identifiers must be non-empty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(profiledRevision);
        ArgumentNullException.ThrowIfNull(mainSteps);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(sourceAnchors);
        ArgumentNullException.ThrowIfNull(traceEvidenceOrders);
        ArgumentNullException.ThrowIfNull(generatedFromDocumentVersionIds);
        string boundedPurpose = WorkerAgentContractText.RequireBoundedSafeText(
            purpose,
            KnowledgeDocumentLimits.MaximumSectionMarkdownBytes,
            nameof(purpose));
        string boundedOutcome = WorkerAgentContractText.RequireBoundedSafeText(
            outcome,
            KnowledgeDocumentLimits.MaximumSectionMarkdownBytes,
            nameof(outcome));

        return new(
            jobId,
            systemId,
            repositoryId,
            scanRunId,
            profiledRevision,
            flowId,
            boundedPurpose,
            Array.AsReadOnly(mainSteps.ToArray()),
            boundedOutcome,
            content,
            Array.AsReadOnly(sourceAnchors.ToArray()),
            Array.AsReadOnly(traceEvidenceOrders.ToArray()),
            Array.AsReadOnly(generatedFromDocumentVersionIds.ToArray()));
    }
}

/// <summary>
/// One bounded Source Anchor request in a Pass 1 result.
/// </summary>
public sealed record WorkerAgentSourceAnchor
{
    private WorkerAgentSourceAnchor(
        Guid id,
        string repositoryRelativePath,
        string evidenceLocator,
        int lineStart,
        int lineEnd,
        string sourceHash)
    {
        Id = id;
        RepositoryRelativePath = repositoryRelativePath;
        EvidenceLocator = evidenceLocator;
        LineStart = lineStart;
        LineEnd = lineEnd;
        SourceHash = sourceHash;
    }

    public Guid Id { get; }
    public string RepositoryRelativePath { get; }
    public string EvidenceLocator { get; }
    public int LineStart { get; }
    public int LineEnd { get; }
    public string SourceHash { get; }

    public static WorkerAgentSourceAnchor Create(
        Guid id,
        string repositoryRelativePath,
        string evidenceLocator,
        int lineStart,
        int lineEnd,
        string sourceHash)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The Source Anchor identifier must be non-empty.", nameof(id));
        }

        string path = WorkerAgentContractText.RequireRepositoryRelativePath(
            repositoryRelativePath,
            nameof(repositoryRelativePath));
        string locator = WorkerAgentContractText.RequireBoundedSafeText(
            evidenceLocator,
            KnowledgeDocumentLimits.MaximumShortTextBytes,
            nameof(evidenceLocator));
        ArgumentOutOfRangeException.ThrowIfLessThan(lineStart, 1, nameof(lineStart));
        ArgumentOutOfRangeException.ThrowIfLessThan(lineEnd, lineStart, nameof(lineEnd));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceHash);
        string hash = sourceHash.Trim().ToLowerInvariant();
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("The Source Anchor hash must be SHA-256.", nameof(sourceHash));
        }

        return new(id, path, locator, lineStart, lineEnd, hash);
    }
}
