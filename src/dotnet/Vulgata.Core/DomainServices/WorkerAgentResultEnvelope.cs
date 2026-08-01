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

        if (!Enum.IsDefined(kind) || kind != WorkerAgentJobKind.Pass1CodeLogicDocument)
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
