namespace Vulgata.Core.DomainServices;

public enum Pass1DocumentCommitStatus
{
    Committed,
    Replayed,
    Rejected,
    Conflict,
}

public sealed record Pass1DocumentCommitRequest(
    PreparedPass1WorkerPacket Packet,
    ReadOnlyMemory<byte> CanonicalCodeLogicJson,
    Guid LogicalDocumentId,
    Guid DocumentVersionId,
    long ExpectedJobStateVersion,
    Guid ExpectedLeaseToken,
    DateTimeOffset CommittedAt);

public sealed record Pass1DocumentCommitResult(
    Pass1DocumentCommitStatus Status,
    Guid? LogicalDocumentId,
    Guid? DocumentVersionId,
    string? CommitFingerprint,
    string? ReasonCode);

/// <summary>
/// Sole public atomic commit boundary for an authoritative
/// <c>code-logic/file-v2</c> result and its prepared Pass 1 job.
/// </summary>
public interface IPass1CodeLogicDocumentCommitter
{
    Task<Pass1DocumentCommitResult> CommitAsync(
        Pass1DocumentCommitRequest request,
        CancellationToken cancellationToken = default);
}
