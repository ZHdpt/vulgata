using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Core.DomainServices;

public interface IPass2BusinessLogicDocumentCommitter
{
    Task<Pass2BusinessLogicCommitResult> CommitAsync(
        Pass2BusinessLogicCommitRequest request,
        CancellationToken cancellationToken = default);
}

public enum Pass2BusinessLogicCommitStatus
{
    Validated,
    Committed,
    Replayed,
    Rejected,
    Conflict,
}

public sealed class Pass2BusinessLogicCommitRequest
{
    public Pass2BusinessLogicCommitRequest(
        Guid pass2JobExecutionId,
        Guid leaseToken,
        int attemptOrdinal,
        JsonElement candidate,
        IReadOnlyList<Pass2BusinessLogicEvidenceBinding>? initialEvidence = null,
        string promptIdentity = "pass2-business-logic-worker.v1")
    {
        Pass2JobExecutionId = pass2JobExecutionId;
        LeaseToken = leaseToken;
        AttemptOrdinal = attemptOrdinal;
        Candidate = candidate.Clone();
        InitialEvidence = Array.AsReadOnly((initialEvidence ?? []).ToArray());
        PromptIdentity = promptIdentity;
    }

    public Guid Pass2JobExecutionId { get; }
    public Guid LeaseToken { get; }
    public int AttemptOrdinal { get; }
    public JsonElement Candidate { get; }
    public IReadOnlyList<Pass2BusinessLogicEvidenceBinding> InitialEvidence { get; }
    public string PromptIdentity { get; }
}

public sealed record Pass2BusinessLogicEvidenceBinding(
    string OpaqueHandle,
    Guid DocumentVersionId,
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    Guid? SectionId = null,
    Guid? SourceAnchorId = null,
    Guid? EdgeId = null,
    long? ByteStart = null,
    long? ByteEnd = null);

/// <summary>
/// Creates the opaque identity for evidence projected into the initial Pass 2 packet.
/// The identity binds the evidence to one server-claimed job attempt and carries no
/// decodable authority.
/// </summary>
public static class Pass2InitialEvidenceHandle
{
    public static string Create(
        Guid jobId,
        int attemptOrdinal,
        Pass2CodeLogicRetrievalItemKind kind,
        Guid documentVersionId,
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        Guid? sectionId = null,
        Guid? sourceAnchorId = null,
        Guid? edgeId = null,
        long? byteStart = null,
        long? byteEnd = null,
        string? sourceHash = null)
    {
        if (jobId == Guid.Empty || attemptOrdinal is < 1 or > 2 ||
            documentVersionId == Guid.Empty || repositoryId == Guid.Empty || scanRunId == Guid.Empty ||
            string.IsNullOrWhiteSpace(profiledRevision))
        {
            throw new ArgumentException("A complete Pass 2 initial evidence binding is required.");
        }

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            '\n',
            "pass2-initial-evidence-handle/1.0",
            jobId.ToString("D"),
            attemptOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
            kind.ToString(),
            documentVersionId.ToString("D"),
            repositoryId.ToString("D"),
            scanRunId.ToString("D"),
            profiledRevision,
            sectionId?.ToString("D") ?? string.Empty,
            sourceAnchorId?.ToString("D") ?? string.Empty,
            edgeId?.ToString("D") ?? string.Empty,
            byteStart?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            byteEnd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            sourceHash ?? string.Empty)));
        byte[] uuid = digest[..16];
        uuid[6] = (byte)((uuid[6] & 0x0f) | 0x80);
        uuid[8] = (byte)((uuid[8] & 0x3f) | 0x80);
        return new Guid(uuid, bigEndian: true).ToString("D");
    }
}

public sealed record Pass2BusinessLogicCommitResult(
    Pass2BusinessLogicCommitStatus Status,
    Guid? DocumentVersionId,
    IReadOnlyList<Guid> UsedDocumentVersionIds,
    string Code)
{
    public static Pass2BusinessLogicCommitResult Validated(IReadOnlyList<Guid> usedIds) =>
        new(
            Pass2BusinessLogicCommitStatus.Validated,
            null,
            Array.AsReadOnly(usedIds.ToArray()),
            "pass2-candidate-validated");

    public static Pass2BusinessLogicCommitResult Rejected() =>
        new(
            Pass2BusinessLogicCommitStatus.Rejected,
            null,
            Array.Empty<Guid>(),
            "pass2-candidate-rejected");

    public static Pass2BusinessLogicCommitResult Conflict() =>
        new(
            Pass2BusinessLogicCommitStatus.Conflict,
            null,
            Array.Empty<Guid>(),
            "pass2-commit-conflict");
}
