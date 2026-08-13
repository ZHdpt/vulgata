using System.Text;
using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Closed outcome allowlist for a <see cref="WorkerAgentResultEnvelope"/>.
/// </summary>
public enum WorkerAgentResultOutcome
{
    /// <summary>The job returned a schema-valid result; the runner records
    /// <c>RecordValid</c> and continues.</summary>
    Valid,

    /// <summary>The result failed schema validation; the runner records
    /// <c>RecordFailure</c> with
    /// <see cref="Entities.ScanExecutionFailureKind.SchemaInvalid"/> and
    /// stops.</summary>
    SchemaInvalid,

    /// <summary>The job returned an unhandled error; the runner records
    /// <c>RecordFailure</c> with
    /// <see cref="Entities.ScanExecutionFailureKind.Error"/> and
    /// stops.</summary>
    Error,

    /// <summary>The result exceeded a finite bound; the runner records
    /// <c>RecordFailure</c> with
    /// <see cref="Entities.ScanExecutionFailureKind.Unbounded"/> and
    /// stops.</summary>
    Unbounded,
}

/// <summary>
/// The schema-validated output of one Worker Agent job, returned to the
/// Sequential Scan Runner by <see cref="IWorkerAgentDispatcher.DispatchAsync"/>.
/// Story 11.2 defines the contract; Story 11.3 supplies the LLM-backed
/// implementation. Immutable; constructed through <see cref="Create"/>, which
/// validates every invariant and applies the Story 11.1 sensitive-content
/// guard. A rejected value throws <see cref="ArgumentException"/>; the
/// rejected value and any provider diagnostic do not cross the contract.
/// </summary>
public sealed class WorkerAgentResultEnvelope
{
    private const int MaximumBoundedReasonUtf8Bytes = 512;

    private WorkerAgentResultEnvelope(
        Guid jobId,
        Guid scanExecutionId,
        WorkerAgentJobKind kind,
        WorkerAgentResultOutcome outcome,
        string? boundedReason,
        DateTimeOffset dispatchedAt,
        DateTimeOffset receivedAt,
        WorkerAgentResultPayload? payload)
    {
        JobId = jobId;
        ScanExecutionId = scanExecutionId;
        Kind = kind;
        Outcome = outcome;
        BoundedReason = boundedReason;
        DispatchedAt = dispatchedAt;
        ReceivedAt = receivedAt;
        Payload = payload;
    }

    public Guid JobId { get; }
    public Guid ScanExecutionId { get; }
    public WorkerAgentJobKind Kind { get; }
    public WorkerAgentResultOutcome Outcome { get; }
    public string? BoundedReason { get; }
    public DateTimeOffset DispatchedAt { get; }
    public DateTimeOffset ReceivedAt { get; }
    public WorkerAgentResultPayload? Payload { get; }

    /// <summary>
    /// Construct a schema-validated <see cref="WorkerAgentResultEnvelope"/>.
    /// Validates per <c>contracts/worker-agent-result-envelope.md</c>: closed
    /// outcome enum, identity Guids non-empty, <see cref="BoundedReason"/>
    /// required on non-<see cref="WorkerAgentResultOutcome.Valid"/> outcomes
    /// (optional on <c>Valid</c>), Chinese ≤512 UTF-8 bytes non-sensitive,
    /// <see cref="ReceivedAt"/> &gt;= <see cref="DispatchedAt"/>,
    /// <see cref="Payload"/> present only on <c>Valid</c>. The Story 11.1
    /// sensitive-content guard is applied to every string. Throws
    /// <see cref="ArgumentException"/> on rejection.
    /// </summary>
    public static WorkerAgentResultEnvelope Create(
        Guid jobId,
        Guid scanExecutionId,
        WorkerAgentJobKind kind,
        WorkerAgentResultOutcome outcome,
        string? boundedReason,
        DateTimeOffset dispatchedAt,
        DateTimeOffset receivedAt,
        WorkerAgentResultPayload? payload = null)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Worker Agent job identifier must be non-empty.",
                nameof(jobId));
        }

        if (scanExecutionId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Scan execution identifier must be non-empty.",
                nameof(scanExecutionId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentException(
                "The Worker Agent job kind is not on the closed allowlist.",
                nameof(kind));
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentException(
                "The outcome is not on the closed allowlist.",
                nameof(outcome));
        }

        if (receivedAt < dispatchedAt)
        {
            throw new ArgumentException(
                "ReceivedAt must be greater than or equal to DispatchedAt.",
                nameof(receivedAt));
        }

        string? reason = null;
        if (outcome != WorkerAgentResultOutcome.Valid)
        {
            reason = RequireBoundedReason(boundedReason, nameof(boundedReason));
        }
        else if (!string.IsNullOrWhiteSpace(boundedReason))
        {
            reason = RequireBoundedReason(boundedReason, nameof(boundedReason));
        }

        WorkerAgentResultPayload? resolvedPayload = null;
        if (outcome == WorkerAgentResultOutcome.Valid)
        {
            resolvedPayload = payload ?? WorkerAgentResultPayload.Reserved;
        }
        else if (payload is not null)
        {
            throw new ArgumentException(
                "Payload must be absent on non-Valid outcomes.",
                nameof(payload));
        }

        return new(
            jobId,
            scanExecutionId,
            kind,
            outcome,
            reason,
            dispatchedAt,
            receivedAt,
            resolvedPayload);
    }

    /// <summary>
    /// Constructs a strict typed result for the exact dispatched packet.
    /// This additive entry point preserves the Story 11.2 placeholder factory
    /// while requiring a complete matching payload for Story 11.3 execution.
    /// </summary>
    public static WorkerAgentResultEnvelope CreateForJob(
        WorkerAgentJobPacket packet,
        WorkerAgentResultOutcome outcome,
        string? boundedReason,
        DateTimeOffset dispatchedAt,
        DateTimeOffset receivedAt,
        WorkerAgentResultPayload? payload = null)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (outcome == WorkerAgentResultOutcome.Valid &&
            packet.Payload is Pass2BusinessLogicJobPayload pass2Job)
        {
            ValidatePass2(packet, pass2Job, payload);
        }
        else if (outcome == WorkerAgentResultOutcome.Valid)
        {
            if (payload is not Pass1CodeLogicDocumentResultPayload pass1 ||
                pass1.Kind != packet.Kind ||
                pass1.JobId != packet.JobId ||
                pass1.SystemId != packet.SystemId ||
                pass1.RepositoryId != packet.RepositoryId ||
                pass1.ScanRunId != packet.ScanRunId ||
                pass1.CodeUnitId != packet.CodeUnitId ||
                !StringComparer.Ordinal.Equals(pass1.ProfiledRevision, packet.ProfiledRevision) ||
                !StringComparer.Ordinal.Equals(pass1.RepositoryRelativePath, packet.SourceSlice.FilePath) ||
                pass1.LineStart != packet.SourceSlice.LineStart ||
                pass1.LineEnd != packet.SourceSlice.LineEnd ||
                !StringComparer.Ordinal.Equals(pass1.SourceHash, packet.SourceSlice.SourceHash) ||
                !StringComparer.Ordinal.Equals(pass1.FileIdentityHash, packet.SourceSlice.FileIdentityHash))
            {
                throw new ArgumentException(
                    "A Valid result must carry exactly one matching typed payload.",
                    nameof(payload));
            }

            var job = (Pass1CodeLogicJobPayload)packet.Payload;
            if (pass1.ReferenceDocumentVersionIds.Count >
                    WorkerAgentContractLimits.MaximumReferenceDocumentVersionIds ||
                pass1.ReferenceDocumentVersionIds.Any(id => id == Guid.Empty) ||
                !IsStrictlyAscending(pass1.ReferenceDocumentVersionIds) ||
                pass1.ReferenceDocumentVersionIds.Any(
                    id => !job.AllowedReferenceDocumentVersionIds.Contains(id)))
            {
                throw new ArgumentException(
                    "Pass 1 reference requests must be bounded, canonical, and allowlisted.",
                    nameof(payload));
            }


            if (pass1.SourceAnchors.Count is < 1 or > KnowledgeDocumentLimits.MaximumAnchors ||
                !IsStrictlyAscending(pass1.SourceAnchors, anchor => anchor.Id) ||
                pass1.SourceAnchors.Any(anchor =>
                    !StringComparer.Ordinal.Equals(
                        anchor.RepositoryRelativePath,
                        job.RepositoryRelativePath) ||
                    anchor.LineStart < job.LineStart ||
                    anchor.LineEnd > job.LineEnd ||
                    anchor.LineEnd < anchor.LineStart ||
                    !StringComparer.Ordinal.Equals(anchor.SourceHash, job.SourceHash)))
            {
                throw new ArgumentException(
                    "Pass 1 Source Anchors must be bounded, canonical, and inside the exact source slice.",
                    nameof(payload));
            }

            if (job.PreparedSourceAnchors.Count > 0 &&
                pass1.SourceAnchors.Any(anchor => !MatchesPreparedAnchor(anchor, job.PreparedSourceAnchors)))
            {
                throw new ArgumentException(
                    "Pass 1 Source Anchor identifiers must be echoed verbatim from the prepared anchor set.",
                    nameof(payload));
            }

            Guid[] approvedEvidenceIds = job.ApprovedCommunicationEvidence
                .Select(item => item.EvidenceId)
                .ToArray();
            if (pass1.CommunicationEvidenceIds.Count >
                    WorkerAgentContractLimits.MaximumCommunicationEvidenceItems ||
                pass1.CommunicationEvidenceIds.Any(id => id == Guid.Empty) ||
                !IsStrictlyAscending(pass1.CommunicationEvidenceIds) ||
                !pass1.CommunicationEvidenceIds.SequenceEqual(approvedEvidenceIds))
            {
                throw new ArgumentException(
                    "Pass 1 communication evidence must exactly represent the prepared evidence set.",
                    nameof(payload));
            }


            if (!StringComparer.Ordinal.Equals(
                    pass1.Content.SchemaIdentity,
                    KnowledgeDocumentLimits.SchemaIdentity) ||
                pass1.Content.DocumentType != DocumentType.CodeLogic ||
                !StringComparer.Ordinal.Equals(
                    pass1.Content.RepositoryRelativePath,
                    job.RepositoryRelativePath))
            {
                throw new ArgumentException(
                    "Pass 1 canonical content must be Code Logic content for the exact Repository-relative path.",
                    nameof(payload));
            }


            if (!IsCanonicalContent(pass1))
            {
                throw new ArgumentException(
                    "Pass 1 canonical content collections or bounds are invalid.",
                    nameof(payload));
            }
        }

        return Create(
            packet.JobId,
            packet.ScanExecutionId,
            packet.Kind,
            outcome,
            boundedReason,
            dispatchedAt,
            receivedAt,
            payload);
    }

    private static void ValidatePass2(
        WorkerAgentJobPacket packet,
        Pass2BusinessLogicJobPayload job,
        WorkerAgentResultPayload? payload)
    {
        if (payload is not Pass2BusinessLogicDocumentResultPayload pass2 ||
            pass2.Kind != packet.Kind ||
            pass2.JobId != packet.JobId ||
            pass2.SystemId != packet.SystemId ||
            pass2.RepositoryId != packet.RepositoryId ||
            pass2.ScanRunId != packet.ScanRunId ||
            !StringComparer.Ordinal.Equals(pass2.ProfiledRevision, packet.ProfiledRevision) ||
            pass2.FlowId != job.FlowId)
        {
            throw new ArgumentException(
                "A Valid result must carry exactly one matching typed payload.",
                nameof(payload));
        }

        int[] expectedEvidenceOrders = job.TraceEvidence
            .Select(evidence => evidence.Order)
            .ToArray();
        Guid[] expectedVersionIds = job.CodeLogicDocumentVersions
            .Select(document => document.DocumentVersionId)
            .ToArray();
        if (pass2.MainSteps.Count is < 1 or > WorkerAgentContractLimits.MaximumBusinessMainSteps ||
            !pass2.MainSteps.Select(step => step.Order)
                .SequenceEqual(Enumerable.Range(1, pass2.MainSteps.Count)) ||
            pass2.MainSteps.Any(step =>
                step.TraceEvidenceOrders.Count == 0 ||
                step.TraceEvidenceOrders.Any(order =>
                    !expectedEvidenceOrders.Contains(order))) ||
            !pass2.MainSteps.SelectMany(step => step.TraceEvidenceOrders)
                .Distinct()
                .Order()
                .SequenceEqual(expectedEvidenceOrders) ||
            !pass2.TraceEvidenceOrders.SequenceEqual(expectedEvidenceOrders) ||
            !pass2.GeneratedFromDocumentVersionIds.SequenceEqual(expectedVersionIds))
        {
            throw new ArgumentException(
                "Pass 2 steps, evidence, and GENERATED_FROM targets must exactly match the prepared synthesis input.",
                nameof(payload));
        }

        WorkerAgentSourceAnchor[] allowedAnchors = job.CodeLogicDocumentVersions
            .SelectMany(document => document.SourceAnchors)
            .OrderBy(anchor => anchor.Id)
            .ToArray();
        if (pass2.SourceAnchors.Count > KnowledgeDocumentLimits.MaximumAnchors ||
            !IsStrictlyAscending(pass2.SourceAnchors, anchor => anchor.Id) ||
            pass2.SourceAnchors.Count != allowedAnchors.Length ||
            pass2.SourceAnchors.Where((anchor, index) =>
                !SourceAnchorEquals(anchor, allowedAnchors[index])).Any())
        {
            throw new ArgumentException(
                "Pass 2 Source Anchors must exactly match the prepared evidence projection.",
                nameof(payload));
        }

        HashSet<Guid> consumerAnchorIds = job.CodeLogicDocumentVersions
            .Where(document => document.RepositoryId == packet.RepositoryId)
            .SelectMany(document => document.SourceAnchors)
            .Select(anchor => anchor.Id)
            .ToHashSet();
        if (consumerAnchorIds.Count == 0 ||
            pass2.Content.Sections.Any(section =>
                section.SourceAnchorIds.Any(id => !consumerAnchorIds.Contains(id))))
        {
            throw new ArgumentException(
                "Pass 2 Business Logic sections may reference only consumer-owned Source Anchors.",
                nameof(payload));
        }

        if (!StringComparer.Ordinal.Equals(
                pass2.Content.SchemaIdentity,
                KnowledgeDocumentLimits.SchemaIdentity) ||
            pass2.Content.DocumentType != DocumentType.BusinessLogic ||
            !IsCanonicalPass2Content(pass2))
        {
            throw new ArgumentException(
                "Pass 2 canonical content must be bounded, evidence-linked Business Logic content.",
                nameof(payload));
        }
    }

    private static bool SourceAnchorEquals(
        WorkerAgentSourceAnchor left,
        WorkerAgentSourceAnchor right) =>
        left.Id == right.Id &&
        StringComparer.Ordinal.Equals(
            left.RepositoryRelativePath,
            right.RepositoryRelativePath) &&
        StringComparer.Ordinal.Equals(left.EvidenceLocator, right.EvidenceLocator) &&
        left.LineStart == right.LineStart &&
        left.LineEnd == right.LineEnd &&
        StringComparer.Ordinal.Equals(left.SourceHash, right.SourceHash);

    private static bool MatchesPreparedAnchor(
        WorkerAgentSourceAnchor anchor,
        IReadOnlyList<WorkerAgentPreparedSourceAnchor> preparedAnchors) =>
        preparedAnchors.Any(prepared =>
            anchor.Id == prepared.Id &&
            StringComparer.Ordinal.Equals(
                anchor.RepositoryRelativePath,
                prepared.RepositoryRelativePath) &&
            anchor.LineStart == prepared.LineStart &&
            anchor.LineEnd == prepared.LineEnd &&
            StringComparer.Ordinal.Equals(anchor.SourceHash, prepared.SourceHash));

    private static bool IsStrictlyAscending(IReadOnlyList<Guid> values)
    {
        for (int index = 1; index < values.Count; index++)
        {
            if (values[index - 1].CompareTo(values[index]) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStrictlyAscending<T>(
        IReadOnlyList<T> values,
        Func<T, Guid> identifierSelector)
    {
        Guid? previous = null;
        foreach (T value in values)
        {
            Guid identifier = identifierSelector(value);
            if (identifier == Guid.Empty ||
                previous.HasValue && previous.Value.CompareTo(identifier) >= 0)
            {
                return false;
            }

            previous = identifier;
        }

        return true;
    }

    private static bool IsCanonicalContent(
        Pass1CodeLogicDocumentResultPayload payload)
    {
        CanonicalDocumentContent content = payload.Content;
        if (content.KeySymbols is null ||
            content.BusinessTerms is null ||
            content.TechnicalTerms is null ||
            content.Sections is null ||
            content.Sections.Count is < 1 or > KnowledgeDocumentLimits.MaximumSections ||
            !IsSafeBoundedText(content.StableKey, KnowledgeDocumentLimits.MaximumShortTextBytes) ||
            !IsSafeBoundedText(content.Title, KnowledgeDocumentLimits.MaximumShortTextBytes) ||
            !IsSafeBoundedText(content.Summary, WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes) ||
            !IsCanonicalTextCollection(content.KeySymbols) ||
            !IsCanonicalTextCollection(content.BusinessTerms) ||
            !IsCanonicalTextCollection(content.TechnicalTerms))
        {
            return false;
        }

        HashSet<Guid> anchorIds = payload.SourceAnchors.Select(anchor => anchor.Id).ToHashSet();
        HashSet<Guid> referenceIds = payload.ReferenceDocumentVersionIds.ToHashSet();
        HashSet<Guid> sectionIds = [];
        int contentBytes = Encoding.UTF8.GetByteCount(content.StableKey) +
            Encoding.UTF8.GetByteCount(content.Title) +
            Encoding.UTF8.GetByteCount(content.Summary);
        for (int index = 0; index < content.Sections.Count; index++)
        {
            CanonicalSection section = content.Sections[index];
            if (section.StableSectionId == Guid.Empty ||
                !sectionIds.Add(section.StableSectionId) ||
                section.Order != index + 1 ||
                !Enum.IsDefined(section.Kind) ||
                section.SourceAnchorIds is null ||
                section.DocumentVersionIds is null ||
                section.SourceAnchorIds.Count == 0 ||
                section.SourceAnchorIds.Any(id => id == Guid.Empty) ||
                section.DocumentVersionIds.Any(id => id == Guid.Empty) ||
                !IsStrictlyAscending(section.SourceAnchorIds) ||
                !IsStrictlyAscending(section.DocumentVersionIds) ||
                section.SourceAnchorIds.Any(id => !anchorIds.Contains(id)) ||
                section.DocumentVersionIds.Any(id => !referenceIds.Contains(id)) ||
                !IsSafeBoundedText(section.Heading, KnowledgeDocumentLimits.MaximumShortTextBytes) ||
                !IsSafeBoundedLayoutText(
                    section.Markdown,
                    KnowledgeDocumentLimits.MaximumSectionMarkdownBytes) ||
                !IsSafeBoundedText(
                    section.SearchText,
                    KnowledgeDocumentLimits.MaximumSectionSearchTextBytes))
            {
                return false;
            }

            contentBytes += Encoding.UTF8.GetByteCount(section.Heading) +
                Encoding.UTF8.GetByteCount(section.Markdown) +
                Encoding.UTF8.GetByteCount(section.SearchText);
        }

        contentBytes += content.KeySymbols.Sum(Encoding.UTF8.GetByteCount) +
            content.BusinessTerms.Sum(Encoding.UTF8.GetByteCount) +
            content.TechnicalTerms.Sum(Encoding.UTF8.GetByteCount);
        return contentBytes <= WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes;
    }

    private static bool IsCanonicalPass2Content(
        Pass2BusinessLogicDocumentResultPayload payload)
    {
        CanonicalDocumentContent content = payload.Content;
        if (content.KeySymbols is null ||
            content.BusinessTerms is null ||
            content.TechnicalTerms is null ||
            content.Sections is null ||
            content.Sections.Count is < 1 or > KnowledgeDocumentLimits.MaximumSections ||
            !IsSafeBoundedText(content.StableKey, KnowledgeDocumentLimits.MaximumShortTextBytes) ||
            !IsSafeBoundedText(content.Title, KnowledgeDocumentLimits.MaximumShortTextBytes) ||
            !IsSafeBoundedText(content.RepositoryRelativePath, KnowledgeDocumentLimits.MaximumRepositoryPathBytes) ||
            !IsSafeBoundedText(content.Summary, WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes) ||
            !IsSafeBoundedText(payload.Purpose, KnowledgeDocumentLimits.MaximumSectionMarkdownBytes) ||
            !IsSafeBoundedText(payload.Outcome, KnowledgeDocumentLimits.MaximumSectionMarkdownBytes) ||
            !IsCanonicalTextCollection(content.KeySymbols) ||
            !IsCanonicalTextCollection(content.BusinessTerms) ||
            !IsCanonicalTextCollection(content.TechnicalTerms))
        {
            return false;
        }

        HashSet<Guid> anchorIds = payload.SourceAnchors.Select(anchor => anchor.Id).ToHashSet();
        HashSet<Guid> versionIds = payload.GeneratedFromDocumentVersionIds.ToHashSet();
        HashSet<Guid> sectionIds = [];
        int contentBytes = Encoding.UTF8.GetByteCount(content.StableKey) +
            Encoding.UTF8.GetByteCount(content.Title) +
            Encoding.UTF8.GetByteCount(content.RepositoryRelativePath) +
            Encoding.UTF8.GetByteCount(content.Summary) +
            Encoding.UTF8.GetByteCount(payload.Purpose) +
            Encoding.UTF8.GetByteCount(payload.Outcome);
        for (int index = 0; index < content.Sections.Count; index++)
        {
            CanonicalSection section = content.Sections[index];
            if (section.StableSectionId == Guid.Empty ||
                !sectionIds.Add(section.StableSectionId) ||
                section.Order != index + 1 ||
                !Enum.IsDefined(section.Kind) ||
                section.SourceAnchorIds is null ||
                section.DocumentVersionIds is null ||
                section.SourceAnchorIds.Count == 0 ||
                section.DocumentVersionIds.Count == 0 ||
                !IsStrictlyAscending(section.SourceAnchorIds) ||
                !IsStrictlyAscending(section.DocumentVersionIds) ||
                section.SourceAnchorIds.Any(id => !anchorIds.Contains(id)) ||
                section.DocumentVersionIds.Any(id => !versionIds.Contains(id)) ||
                !IsSafeBoundedText(section.Heading, KnowledgeDocumentLimits.MaximumShortTextBytes) ||
                !IsSafeBoundedLayoutText(section.Markdown, KnowledgeDocumentLimits.MaximumSectionMarkdownBytes) ||
                !IsSafeBoundedText(section.SearchText, KnowledgeDocumentLimits.MaximumSectionSearchTextBytes))
            {
                return false;
            }

            contentBytes += Encoding.UTF8.GetByteCount(section.Heading) +
                Encoding.UTF8.GetByteCount(section.Markdown) +
                Encoding.UTF8.GetByteCount(section.SearchText);
        }

        contentBytes += payload.MainSteps.Sum(step =>
            Encoding.UTF8.GetByteCount(step.Description));
        contentBytes += content.KeySymbols.Sum(Encoding.UTF8.GetByteCount) +
            content.BusinessTerms.Sum(Encoding.UTF8.GetByteCount) +
            content.TechnicalTerms.Sum(Encoding.UTF8.GetByteCount);
        return contentBytes <= WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes;
    }

    private static bool IsCanonicalTextCollection(IReadOnlyList<string> values)
    {
        if (values.Count > KnowledgeDocumentLimits.MaximumTerms)
        {
            return false;
        }

        string? previous = null;
        foreach (string value in values)
        {
            if (!IsSafeBoundedText(value, KnowledgeDocumentLimits.MaximumShortTextBytes) ||
                previous is not null && StringComparer.Ordinal.Compare(previous, value) >= 0)
            {
                return false;
            }

            previous = value;
        }

        return true;
    }

    private static bool IsSafeBoundedText(string? value, int maximumUtf8Bytes)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !StringComparer.Ordinal.Equals(value, value.Normalize(NormalizationForm.FormC)) ||
            value.Any(character =>
                character <= '\u001f' ||
                character is >= '\u007f' and <= '\u009f') ||
            Encoding.UTF8.GetByteCount(value) > maximumUtf8Bytes)
        {
            return false;
        }

        try
        {
            KnowledgeTextSafety.EnsureSafe(value);
            return true;
        }
        catch (KnowledgeStoreContractException)
        {
            return false;
        }
    }

    private static bool IsSafeBoundedLayoutText(string? value, int maximumUtf8Bytes)
    {
        try
        {
            _ = WorkerAgentContractText.RequireBoundedSafeLayoutText(
                value!,
                maximumUtf8Bytes,
                nameof(value));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string RequireBoundedReason(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Any(character =>
                character <= '\u001f' ||
                character is >= '\u007f' and <= '\u009f'))
        {
            throw new ArgumentException(
                "The bounded reason contains forbidden control characters.",
                parameterName);
        }

        int byteCount = Encoding.UTF8.GetByteCount(normalized);
        if (byteCount > MaximumBoundedReasonUtf8Bytes)
        {
            throw new ArgumentException(
                "The bounded reason exceeds the finite UTF-8 byte limit.",
                parameterName);
        }

        try
        {
            KnowledgeTextSafety.EnsureSafe(normalized);
        }
        catch (KnowledgeStoreContractException ex)
        {
            throw new ArgumentException(
                "The bounded reason was rejected by the sensitive-content guard.",
                parameterName,
                ex);
        }

        return normalized;
    }
}
