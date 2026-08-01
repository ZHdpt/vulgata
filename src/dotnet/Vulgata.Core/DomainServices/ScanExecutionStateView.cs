using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Bounded read model returned by
/// <see cref="SequentialScanRunner.ReadStateAsync"/>. Carries enough
/// persisted state to distinguish <see cref="ScanExecutionPhase.Pass1Running"/>
/// from <see cref="ScanExecutionPhase.Completed"/>,
/// <see cref="ScanExecutionPhase.Failed"/>, and
/// <see cref="ScanExecutionPhase.Cancelled"/> using persisted state alone.
/// </summary>
public sealed class ScanExecutionStateView
{
    public ScanExecutionStateView(
        Guid scanExecutionId,
        Guid repositoryId,
        Guid scanRunId,
        ScanExecutionPhase phase,
        Guid? inFlightJobId,
        int completedJobCount,
        int totalJobCount,
        int restartIndicator,
        string? terminalMessage,
        ScanExecutionFailureKind? terminalReasonKind,
        DateTimeOffset startedAt,
        DateTimeOffset? terminalAt)
    {
        ScanExecutionId = scanExecutionId;
        RepositoryId = repositoryId;
        ScanRunId = scanRunId;
        Phase = phase;
        InFlightJobId = inFlightJobId;
        CompletedJobCount = completedJobCount;
        TotalJobCount = totalJobCount;
        RestartIndicator = restartIndicator;
        TerminalMessage = terminalMessage;
        TerminalReasonKind = terminalReasonKind;
        StartedAt = startedAt;
        TerminalAt = terminalAt;
    }

    public Guid ScanExecutionId { get; }
    public Guid RepositoryId { get; }
    public Guid ScanRunId { get; }
    public ScanExecutionPhase Phase { get; }
    public Guid? InFlightJobId { get; }
    public int CompletedJobCount { get; }
    public int TotalJobCount { get; }
    public int RestartIndicator { get; }
    public string? TerminalMessage { get; }
    public ScanExecutionFailureKind? TerminalReasonKind { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? TerminalAt { get; }
}
