using System.Text;

namespace Vulgata.Core.Entities;

/// <summary>
/// One durable record per deadline Scan run. Created by
/// <see cref="DomainServices.SequentialScanRunner.StartAsync"/>; mutated only
/// through atomic phase transitions inside
/// <c>ScanExecutionRepository</c>. Terminal phases are immutable.
/// </summary>
public sealed class ScanExecution
{
    /// <summary>
    /// Bounded upper limit for <see cref="RestartIndicator"/>. The indicator
    /// never overflows; once this limit is reached, subsequent restart
    /// re-reads keep the value clamped.
    /// </summary>
    public const int MaxRestartIndicator = 32;

    private const int MaximumProfiledRevisionLength = 64;
    private const int MaximumTerminalMessageUtf8Bytes = 512;

    // EF Core requires a parameterless constructor. It can be private.
    private ScanExecution()
    {
        ProfiledRevision = string.Empty;
    }

    private ScanExecution(
        Guid id,
        Guid repositoryId,
        Guid systemId,
        Guid scanRunId,
        Guid preScanResultId,
        string profiledRevision,
        DateTimeOffset startedAt)
    {
        Id = id;
        RepositoryId = repositoryId;
        SystemId = systemId;
        ScanRunId = scanRunId;
        PreScanResultId = preScanResultId;
        ProfiledRevision = profiledRevision;
        StartedAt = startedAt;
        Phase = ScanExecutionPhase.Pass1Planning;
        InFlightJobId = null;
        CompletedJobCount = 0;
        TotalJobCount = 0;
        RestartIndicator = 0;
        TerminalMessage = null;
        TerminalReasonKind = null;
        TerminalAt = null;
    }

    public Guid Id { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid SystemId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public Guid PreScanResultId { get; private set; }
    public string ProfiledRevision { get; private set; }
    public ScanExecutionPhase Phase { get; private set; }
    public Guid? InFlightJobId { get; private set; }
    public int CompletedJobCount { get; private set; }
    public int TotalJobCount { get; private set; }
    public int RestartIndicator { get; private set; }
    public string? TerminalMessage { get; private set; }
    public ScanExecutionFailureKind? TerminalReasonKind { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? TerminalAt { get; private set; }

    /// <summary>
    /// Start a new Scan execution from an approved Pre-Scan Handoff. Validates
    /// every identity field is non-empty, the Profiled Revision is a bounded
    /// lowercase Git object ID, and creates the record in the
    /// <see cref="ScanExecutionPhase.Pass1Planning"/> phase. Throws
    /// <see cref="ArgumentException"/> on rejection.
    /// </summary>
    public static ScanExecution Start(
        Guid id,
        Guid repositoryId,
        Guid systemId,
        Guid scanRunId,
        Guid preScanResultId,
        string profiledRevision,
        DateTimeOffset startedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "The Scan execution identifier must be non-empty.",
                nameof(id));
        }

        if (repositoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Repository identifier must be non-empty.",
                nameof(repositoryId));
        }

        if (systemId == Guid.Empty)
        {
            throw new ArgumentException(
                "The System identifier must be non-empty.",
                nameof(systemId));
        }

        if (scanRunId == Guid.Empty)
        {
            throw new ArgumentException(
                "The ScanRun identifier must be non-empty.",
                nameof(scanRunId));
        }

        if (preScanResultId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Pre-Scan Result identifier must be non-empty.",
                nameof(preScanResultId));
        }

        string revision = RequireProfiledRevision(profiledRevision);

        return new(
            id,
            repositoryId,
            systemId,
            scanRunId,
            preScanResultId,
            revision,
            startedAt);
    }

    /// <summary>
    /// Transition from <see cref="ScanExecutionPhase.Pass1Planning"/> to
    /// <see cref="ScanExecutionPhase.Pass1Running"/> and set the total job
    /// count from the prepared Code Units.
    /// </summary>
    public void Plan(int totalJobCount, DateTimeOffset plannedAt)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Planning);
        ArgumentOutOfRangeException.ThrowIfNegative(totalJobCount, nameof(totalJobCount));
        if (totalJobCount == 0)
        {
            throw new ArgumentException(
                "The total job count must be positive for a Pass 1 dispatch.",
                nameof(totalJobCount));
        }

        Phase = ScanExecutionPhase.Pass1Running;
        TotalJobCount = totalJobCount;
    }

    /// <summary>
    /// Advance: dispatch one job. Sets <see cref="InFlightJobId"/> and
    /// requires <see cref="ScanExecutionPhase.Pass1Running"/> with no job
    /// already in flight.
    /// </summary>
    public void Advance(Guid jobId)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException(
                "The in-flight job identifier must be non-empty.",
                nameof(jobId));
        }

        EnsurePhase(ScanExecutionPhase.Pass1Running);
        if (InFlightJobId is not null)
        {
            throw new InvalidOperationException(
                "A job is already in flight; the runner dispatches one at a time.");
        }

        InFlightJobId = jobId;
    }

    /// <summary>
    /// Record a Valid result: clear <see cref="InFlightJobId"/> and increment
    /// <see cref="CompletedJobCount"/>. Requires the same job to be in flight.
    /// </summary>
    public void RecordValid(Guid jobId)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Running);
        EnsureInFlightMatch(jobId);
        InFlightJobId = null;
        CompletedJobCount++;
    }

    /// <summary>
    /// Mark Pass 1 durably complete while preserving the nonterminal boundary
    /// required before the prepared Pass 2 flow can begin.
    /// </summary>
    public void CompletePass1()
    {
        EnsurePhase(ScanExecutionPhase.Pass1Running);
        if (InFlightJobId is not null)
        {
            throw new InvalidOperationException(
                "Cannot complete Pass 1 while a job is in flight.");
        }

        if (CompletedJobCount != TotalJobCount)
        {
            throw new InvalidOperationException(
                "Cannot complete Pass 1 before every prepared job is valid.");
        }

        Phase = ScanExecutionPhase.Pass1Completed;
    }

    /// <summary>
    /// Begin the single prepared Pass 2 dispatch from the durable
    /// <see cref="ScanExecutionPhase.Pass1Completed"/> boundary.
    /// </summary>
    public void BeginPass2(Guid jobId)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Pass 2 job identifier must be non-empty.",
                nameof(jobId));
        }

        EnsurePhase(ScanExecutionPhase.Pass1Completed);
        if (InFlightJobId is not null)
        {
            throw new InvalidOperationException(
                "A Pass 2 job is already in flight.");
        }

        if (CompletedJobCount != TotalJobCount)
        {
            throw new InvalidOperationException(
                "Pass 2 cannot begin before every Pass 1 job is valid.");
        }

        InFlightJobId = jobId;
    }

    /// <summary>
    /// Complete the Scan only after the prepared Pass 2 result has committed.
    /// </summary>
    public void CompletePass2(
        Guid jobId,
        string successMessage,
        DateTimeOffset completedAt)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Completed);
        EnsureInFlightMatch(jobId);
        string message = RequireTerminalMessage(successMessage);
        if (completedAt < StartedAt)
        {
            throw new ArgumentException(
                "The terminal timestamp must be at or after the start timestamp.",
                nameof(completedAt));
        }

        Phase = ScanExecutionPhase.Completed;
        TerminalMessage = message;
        TerminalAt = completedAt;
        InFlightJobId = null;
    }

    /// <summary>
    /// Stop a non-committed Pass 2 outcome without exposing a successful Scan.
    /// </summary>
    public void FailPass2(
        Guid jobId,
        ScanExecutionFailureKind reasonKind,
        string terminalMessage,
        DateTimeOffset failedAt)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Completed);
        EnsureInFlightMatch(jobId);
        if (!Enum.IsDefined(reasonKind))
        {
            throw new ArgumentException(
                "The failure reason kind is not on the closed allowlist.",
                nameof(reasonKind));
        }

        string message = RequireTerminalMessage(terminalMessage);
        if (failedAt < StartedAt)
        {
            throw new ArgumentException(
                "The terminal timestamp must be at or after the start timestamp.",
                nameof(failedAt));
        }

        Phase = ScanExecutionPhase.Failed;
        TerminalReasonKind = reasonKind;
        TerminalMessage = message;
        TerminalAt = failedAt;
        InFlightJobId = null;
    }

    /// <summary>
    /// Record a failure: transition to <see cref="ScanExecutionPhase.Failed"/>
    /// with a bounded Chinese terminal message and closed failure kind. Clears
    /// <see cref="InFlightJobId"/>. The atomic transaction ensures no partial
    /// state is visible. No Document Version is committed by the runner.
    /// </summary>
    public void RecordFailure(
        Guid jobId,
        ScanExecutionFailureKind reasonKind,
        string terminalMessage,
        DateTimeOffset failedAt)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Running);
        EnsureInFlightMatch(jobId);
        if (!Enum.IsDefined(reasonKind))
        {
            throw new ArgumentException(
                "The failure reason kind is not on the closed allowlist.",
                nameof(reasonKind));
        }

        string message = RequireTerminalMessage(terminalMessage);
        if (failedAt < StartedAt)
        {
            throw new ArgumentException(
                "The terminal timestamp must be at or after the start timestamp.",
                nameof(failedAt));
        }

        Phase = ScanExecutionPhase.Failed;
        TerminalReasonKind = reasonKind;
        TerminalMessage = message;
        TerminalAt = failedAt;
        InFlightJobId = null;
    }

    /// <summary>
    /// Record a boundary rejection failure without an in-flight job. Used
    /// when the packet factory rejects forbidden/oversized content before
    /// dispatch. Transitions from <see cref="ScanExecutionPhase.Pass1Running"/>
    /// to <see cref="ScanExecutionPhase.Failed"/>.
    /// </summary>
    public void RecordBoundaryRejection(
        ScanExecutionFailureKind reasonKind,
        string terminalMessage,
        DateTimeOffset failedAt)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Running);
        if (InFlightJobId is not null)
        {
            throw new InvalidOperationException(
                "Boundary rejection must not be recorded while a job is in flight.");
        }

        if (!Enum.IsDefined(reasonKind))
        {
            throw new ArgumentException(
                "The failure reason kind is not on the closed allowlist.",
                nameof(reasonKind));
        }

        string message = RequireTerminalMessage(terminalMessage);
        if (failedAt < StartedAt)
        {
            throw new ArgumentException(
                "The terminal timestamp must be at or after the start timestamp.",
                nameof(failedAt));
        }

        Phase = ScanExecutionPhase.Failed;
        TerminalReasonKind = reasonKind;
        TerminalMessage = message;
        TerminalAt = failedAt;
    }

    /// <summary>
    /// Finalize: when <see cref="CompletedJobCount"/> equals
    /// <see cref="TotalJobCount"/>, transition through
    /// <see cref="ScanExecutionPhase.Pass1Completed"/> to
    /// <see cref="ScanExecutionPhase.Completed"/> with a bounded Chinese
    /// success message and the exact job count.
    /// </summary>
    public void Finalize(string successMessage, DateTimeOffset completedAt)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Running);
        if (InFlightJobId is not null)
        {
            throw new InvalidOperationException(
                "Cannot finalize while a job is in flight.");
        }

        if (CompletedJobCount != TotalJobCount)
        {
            throw new InvalidOperationException(
                "Cannot finalize: not all jobs have returned Valid results.");
        }

        string message = RequireTerminalMessage(successMessage);
        if (completedAt < StartedAt)
        {
            throw new ArgumentException(
                "The terminal timestamp must be at or after the start timestamp.",
                nameof(completedAt));
        }

        Phase = ScanExecutionPhase.Pass1Completed;
        Phase = ScanExecutionPhase.Completed;
        TerminalMessage = message;
        TerminalAt = completedAt;
    }

    /// <summary>
    /// Cancel: transition from <see cref="ScanExecutionPhase.Pass1Planning"/>
    /// or <see cref="ScanExecutionPhase.Pass1Running"/> to
    /// <see cref="ScanExecutionPhase.Cancelled"/> with a bounded Chinese
    /// message. No LLM resume.
    /// </summary>
    public void Cancel(string cancelMessage, DateTimeOffset cancelledAt)
    {
        if (Phase is not (ScanExecutionPhase.Pass1Planning or ScanExecutionPhase.Pass1Running))
        {
            throw new InvalidOperationException(
                "A Scan execution can only be cancelled from Pass1Planning or Pass1Running.");
        }

        string message = RequireTerminalMessage(cancelMessage);
        if (cancelledAt < StartedAt)
        {
            throw new ArgumentException(
                "The terminal timestamp must be at or after the start timestamp.",
                nameof(cancelledAt));
        }

        Phase = ScanExecutionPhase.Cancelled;
        TerminalMessage = message;
        TerminalAt = cancelledAt;
        InFlightJobId = null;
    }

    /// <summary>
    /// Record a restart re-read observation. Increments
    /// <see cref="RestartIndicator"/> (bounded 0–
    /// <see cref="MaxRestartIndicator"/>) only if a job is in flight. Does
    /// NOT resume the LLM job and does NOT fabricate a result. Does NOT
    /// mutate <see cref="Phase"/>.
    /// </summary>
    public void ObserveRestart()
    {
        if (Phase != ScanExecutionPhase.Pass1Running)
        {
            return;
        }

        if (InFlightJobId is null)
        {
            return;
        }

        if (RestartIndicator < MaxRestartIndicator)
        {
            RestartIndicator++;
        }
    }

    private void EnsurePhase(ScanExecutionPhase expected)
    {
        if (Phase != expected)
        {
            throw new InvalidOperationException(
                $"The Scan execution is in phase {Phase}; expected {expected}.");
        }
    }

    private void EnsureInFlightMatch(Guid jobId)
    {
        if (InFlightJobId is null || InFlightJobId != jobId)
        {
            throw new InvalidOperationException(
                "The in-flight job identifier does not match the recorded job.");
        }
    }

    private static string RequireProfiledRevision(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > MaximumProfiledRevisionLength ||
            normalized.Any(character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "The Profiled Revision must be a bounded lowercase Git object ID.",
                nameof(value));
        }

        return normalized;
    }

    private static string RequireTerminalMessage(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Any(character =>
                character <= '\u001f' ||
                character is >= '\u007f' and <= '\u009f'))
        {
            throw new ArgumentException(
                "The terminal message contains forbidden control characters.",
                nameof(value));
        }

        int byteCount = Encoding.UTF8.GetByteCount(normalized);
        if (byteCount > MaximumTerminalMessageUtf8Bytes)
        {
            throw new ArgumentException(
                "The terminal message exceeds the finite UTF-8 byte limit.",
                nameof(value));
        }

        return normalized;
    }
}
