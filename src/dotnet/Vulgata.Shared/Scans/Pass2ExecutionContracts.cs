using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vulgata.Shared.Scans;

public sealed record StartPass2ExecutionRequest(Guid Pass1PairExecutionId)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement> AdditionalProperties { get; init; } =
        new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
}

public sealed record Pass2ExecutionStartResponse(
    Guid Pass2ExecutionId,
    int FlowGroupCount,
    int OmissionCount,
    int JobCount,
    bool IsReplay);

public sealed record Pass2ExecutionWarningResponse(
    Guid GroupIdentity,
    string GroupFingerprint,
    string ReasonCode);

public sealed record Pass2ExecutionStatusResponse(
    Guid Pass2ExecutionId,
    string State,
    long StateVersion,
    int CandidateCount,
    int PublishedCount,
    int FailedCount,
    int IncompleteCount,
    int BoundExcludedCount,
    int OmissionCount,
    int PendingCount,
    int RunningCount,
    int RetryScheduledCount,
    IReadOnlyList<Pass2ExecutionWarningResponse> Warnings);

public sealed record Pass2ExecutionResumeResponse(
    Guid Pass2ExecutionId,
    string State,
    long StateVersion,
    int RecoveredJobCount,
    int FailedJobCount,
    bool IsReplay);
