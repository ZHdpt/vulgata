using System.Text;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Transient, revision-bound input for one Worker Agent invocation. Source text
/// is deliberately excluded from diagnostics and has no persistence surface.
/// </summary>
public sealed class WorkerAgentExecutionContext
{
    private const int _requestFramingAllowanceUtf8Bytes = 4_096;

    private WorkerAgentExecutionContext(
        WorkerAgentJobPacket packet,
        string sourceText,
        int sourceLineCount,
        int sourceUtf8ByteCount,
        int requestUtf8ByteCount,
        IReadOnlyList<WorkerAgentStructuralContext> neighboringStructuralContext,
        IReadOnlyList<WorkerAgentCommunicationEvidence> approvedCommunicationEvidence,
        IReadOnlyList<Guid> allowedReferenceDocumentVersionIds)
    {
        Packet = packet;
        SourceText = sourceText;
        SourceLineCount = sourceLineCount;
        SourceUtf8ByteCount = sourceUtf8ByteCount;
        RequestUtf8ByteCount = requestUtf8ByteCount;
        NeighboringStructuralContext = neighboringStructuralContext;
        ApprovedCommunicationEvidence = approvedCommunicationEvidence;
        AllowedReferenceDocumentVersionIds = allowedReferenceDocumentVersionIds;
    }

    public WorkerAgentJobPacket Packet { get; }
    public string SourceText { get; }
    public int SourceLineCount { get; }
    public int SourceUtf8ByteCount { get; }
    public int RequestUtf8ByteCount { get; }
    public IReadOnlyList<WorkerAgentStructuralContext> NeighboringStructuralContext { get; }
    public IReadOnlyList<WorkerAgentCommunicationEvidence> ApprovedCommunicationEvidence { get; }
    public IReadOnlyList<Guid> AllowedReferenceDocumentVersionIds { get; }

    internal static WorkerAgentExecutionContext Create(
        WorkerAgentJobPacket packet,
        string sourceText,
        int sourceLineCount)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(sourceText);
        if (packet.Payload is not Pass1CodeLogicJobPayload pass1)
        {
            throw new ArgumentException(
                "Only the Pass 1 Worker Agent variant can carry hydrated source.",
                nameof(packet));
        }

        int sourceBytes = Encoding.UTF8.GetByteCount(sourceText);
        if (sourceLineCount is < 1 or > WorkerAgentContractLimits.MaximumSourceSliceLines ||
            sourceBytes > WorkerAgentContractLimits.MaximumSourceSliceUtf8Bytes)
        {
            throw new InvalidDataException("Hydrated source exceeds its finite bound.");
        }

        long requestBytes = _requestFramingAllowanceUtf8Bytes + sourceBytes;
        requestBytes += CountUtf8Bytes(packet.ProfiledRevision);
        requestBytes += CountUtf8Bytes(pass1.RepositoryRelativePath);
        requestBytes += CountUtf8Bytes(packet.SourceSlice.SymbolName);
        requestBytes += CountUtf8Bytes(packet.SourceSlice.Language);
        requestBytes += 36L * 7;
        requestBytes += 30;
        foreach (WorkerAgentStructuralContext item in pass1.NeighboringStructuralContext)
        {
            requestBytes += 36;
            requestBytes += CountUtf8Bytes(item.RepositoryRelativePath);
            requestBytes += CountUtf8Bytes(item.SymbolName);
        }

        foreach (WorkerAgentCommunicationEvidence item in pass1.ApprovedCommunicationEvidence)
        {
            requestBytes += 36;
            requestBytes += CountUtf8Bytes(item.Kind);
            requestBytes += CountUtf8Bytes(item.Description);
        }

        requestBytes += 36L * pass1.AllowedReferenceDocumentVersionIds.Count;
        if (requestBytes > WorkerAgentContractLimits.MaximumProviderRequestUtf8Bytes)
        {
            throw new InvalidDataException("Hydrated Worker Agent request exceeds its finite bound.");
        }

        return new(
            packet,
            sourceText,
            sourceLineCount,
            sourceBytes,
            checked((int)requestBytes),
            Array.AsReadOnly(pass1.NeighboringStructuralContext.ToArray()),
            Array.AsReadOnly(pass1.ApprovedCommunicationEvidence.ToArray()),
            Array.AsReadOnly(pass1.AllowedReferenceDocumentVersionIds.ToArray()));
    }

    public override string ToString() =>
        $"WorkerAgentExecutionContext {{ JobId = {Packet.JobId:D}, SourceText = [REDACTED] }}";

    private static int CountUtf8Bytes(string? value) =>
        value is null ? 0 : Encoding.UTF8.GetByteCount(value);
}
