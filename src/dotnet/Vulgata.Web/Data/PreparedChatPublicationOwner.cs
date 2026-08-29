using System.Threading;
using Vulgata.Core.DomainServices;

namespace Vulgata.Web.Data;

public enum PreparedChatPublicationState
{
    Pending,
    AcceptedPublicationCommit,
    TimedOutBeforePublication,
    CancelledBeforePublication,
}

public enum PreparedChatRevealMode
{
    ImmediateAccepted,
    ProgressiveAccepted,
}

public sealed class PreparedChatRevealDirective
{
    public const long TextElementDelayMilliseconds = 25;
    public const long FixedSafetyMarginMilliseconds = 1_000;

    private readonly bool _progressiveRequested;
    private int _mode;

    private PreparedChatRevealDirective(
        PreparedChatRevealMode mode,
        long absoluteDeadlineUnixTimeMilliseconds,
        long scheduleCostMilliseconds,
        long preparedRemainingBudgetMilliseconds)
    {
        if (scheduleCostMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scheduleCostMilliseconds));
        }

        if (preparedRemainingBudgetMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(preparedRemainingBudgetMilliseconds));
        }

        _mode = (int)mode;
        _progressiveRequested = mode == PreparedChatRevealMode.ProgressiveAccepted;
        AbsoluteDeadlineUnixTimeMilliseconds = absoluteDeadlineUnixTimeMilliseconds;
        ScheduleCostMilliseconds = scheduleCostMilliseconds;
        PreparedRemainingBudgetMilliseconds = preparedRemainingBudgetMilliseconds;
    }

    public PreparedChatRevealMode Mode =>
        (PreparedChatRevealMode)Volatile.Read(ref _mode);

    public long AbsoluteDeadlineUnixTimeMilliseconds { get; }

    public long ScheduleCostMilliseconds { get; }

    public long SafetyMarginMilliseconds => FixedSafetyMarginMilliseconds;

    internal long PreparedRemainingBudgetMilliseconds { get; }

    public static PreparedChatRevealDirective ImmediateAccepted(
        long absoluteDeadline,
        long scheduleCostMilliseconds = 0) =>
        new(
            PreparedChatRevealMode.ImmediateAccepted,
            absoluteDeadline,
            scheduleCostMilliseconds,
            long.MaxValue);

    public static PreparedChatRevealDirective ProgressiveAccepted(
        long absoluteDeadline,
        long scheduleCostMilliseconds = 0) =>
        new(
            PreparedChatRevealMode.ProgressiveAccepted,
            absoluteDeadline,
            scheduleCostMilliseconds,
            long.MaxValue);

    internal static PreparedChatRevealDirective ProgressiveAccepted(
        long absoluteDeadline,
        long scheduleCostMilliseconds,
        long preparedRemainingBudgetMilliseconds) =>
        new(
            PreparedChatRevealMode.ProgressiveAccepted,
            absoluteDeadline,
            scheduleCostMilliseconds,
            preparedRemainingBudgetMilliseconds);

    internal void SelectForRemainingBudget(long remainingBudgetMilliseconds)
    {
        bool scheduleStrictlyFits =
            _progressiveRequested &&
            ScheduleCostMilliseconds < remainingBudgetMilliseconds &&
            SafetyMarginMilliseconds <
                remainingBudgetMilliseconds - ScheduleCostMilliseconds;
        Volatile.Write(
            ref _mode,
            (int)(scheduleStrictlyFits
                ? PreparedChatRevealMode.ProgressiveAccepted
                : PreparedChatRevealMode.ImmediateAccepted));
    }
}

public sealed record PreparedChatCompletionSummaryTemplate(
    string CompletionSummaryId,
    string RequestId,
    IReadOnlyList<string> RuntimeObservationIds,
    bool SyntaxRepairUsed,
    IReadOnlyList<string> AttemptValidationOutcomes,
    string FinalValidationOutcome);

public sealed class PreparedChatCommittedCompletionSummary
{
    private long _totalAcceptedAnswerDurationMilliseconds = -1;

    internal PreparedChatCommittedCompletionSummary(
        PreparedChatCompletionSummaryTemplate template)
    {
        CompletionSummaryId = template.CompletionSummaryId;
        RequestId = template.RequestId;
        RuntimeObservationIds = Array.AsReadOnly(template.RuntimeObservationIds.ToArray());
        SyntaxRepairUsed = template.SyntaxRepairUsed;
        AttemptValidationOutcomes = Array.AsReadOnly(
            template.AttemptValidationOutcomes.ToArray());
        FinalValidationOutcome = template.FinalValidationOutcome;
    }

    public string CompletionSummaryId { get; }

    public string RequestId { get; }

    public IReadOnlyList<string> RuntimeObservationIds { get; }

    public bool SyntaxRepairUsed { get; }

    public IReadOnlyList<string> AttemptValidationOutcomes { get; }

    public string FinalValidationOutcome { get; }

    public long? TotalAcceptedAnswerDurationMilliseconds
    {
        get
        {
            long duration = Volatile.Read(ref _totalAcceptedAnswerDurationMilliseconds);
            return duration < 0 ? null : duration;
        }
    }

    internal void SetAcceptedDuration(long durationMilliseconds) =>
        Volatile.Write(
            ref _totalAcceptedAnswerDurationMilliseconds,
            durationMilliseconds);
}

public sealed class PreparedChatPublicationSnapshot
{
    internal PreparedChatPublicationSnapshot(
        long generation,
        AcceptedPublicationCommit? currentCommit,
        PreparedChatPublicationAuthority authority)
    {
        Generation = generation;
        CurrentCommit = currentCommit?.Published == true ? currentCommit : null;
        Authority = authority;
    }

    public long Generation { get; }

    public AcceptedPublicationCommit? CurrentCommit { get; }

    internal PreparedChatPublicationAuthority Authority { get; }
}

internal sealed class PreparedChatPublicationAuthority
{
    private long _generation;
    private AcceptedPublicationCommit? _currentCommit;

    internal object Gate { get; } = new();

    internal AcceptedPublicationCommit? CurrentCommit
    {
        get
        {
            lock (Gate)
            {
                return _currentCommit?.Published == true ? _currentCommit : null;
            }
        }
    }

    internal PreparedChatPublicationSnapshot Capture()
    {
        lock (Gate)
        {
            return new PreparedChatPublicationSnapshot(
                _generation,
                _currentCommit,
                this);
        }
    }

    internal bool IsCurrentGenerationUnderLock(long generation) =>
        _generation == generation;

    internal void SetCurrentUnderLock(AcceptedPublicationCommit commit) =>
        _currentCommit = commit;

    internal void Reset()
    {
        lock (Gate)
        {
            _generation = unchecked(_generation + 1);
            _currentCommit = null;
        }
    }
}

public sealed class PreparedChatPublicationCandidate
{
    private int _state;
    private long _publicationTimestamp;

    public PreparedChatPublicationCandidate(
        GroundedAnswer acceptedAnswer,
        ChatConversationState proposedConversation,
        PreparedChatCompletionSummaryTemplate completionSummary,
        PreparedChatRevealDirective reveal,
        long startTimestamp,
        long deadlineTimestamp,
        CancellationToken deadlineCancellationToken,
        string packetFingerprint,
        string advisoryAuthorityFingerprint)
        : this(
            acceptedAnswer,
            proposedConversation,
            completionSummary,
            reveal,
            startTimestamp,
            deadlineTimestamp,
            deadlineCancellationToken,
            packetFingerprint,
            advisoryAuthorityFingerprint,
            new PreparedChatPublicationAuthority().Capture())
    {
    }

    internal PreparedChatPublicationCandidate(
        GroundedAnswer acceptedAnswer,
        ChatConversationState proposedConversation,
        PreparedChatCompletionSummaryTemplate completionSummary,
        PreparedChatRevealDirective reveal,
        long startTimestamp,
        long deadlineTimestamp,
        CancellationToken deadlineCancellationToken,
        string packetFingerprint,
        string advisoryAuthorityFingerprint,
        PreparedChatPublicationAuthority publicationAuthority)
        : this(
            acceptedAnswer,
            proposedConversation,
            completionSummary,
            reveal,
            startTimestamp,
            deadlineTimestamp,
            deadlineCancellationToken,
            packetFingerprint,
            advisoryAuthorityFingerprint,
            publicationAuthority?.Capture() ??
                throw new ArgumentNullException(nameof(publicationAuthority)))
    {
    }

    internal PreparedChatPublicationCandidate(
        GroundedAnswer acceptedAnswer,
        ChatConversationState proposedConversation,
        PreparedChatCompletionSummaryTemplate completionSummary,
        PreparedChatRevealDirective reveal,
        long startTimestamp,
        long deadlineTimestamp,
        CancellationToken deadlineCancellationToken,
        string packetFingerprint,
        string advisoryAuthorityFingerprint,
        PreparedChatPublicationSnapshot publicationSnapshot)
    {
        AcceptedAnswer = acceptedAnswer ?? throw new ArgumentNullException(nameof(acceptedAnswer));
        ProposedConversation = proposedConversation ??
            throw new ArgumentNullException(nameof(proposedConversation));
        CompletionSummary = completionSummary ??
            throw new ArgumentNullException(nameof(completionSummary));
        Reveal = reveal ?? throw new ArgumentNullException(nameof(reveal));
        if (deadlineTimestamp <= startTimestamp)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deadlineTimestamp),
                "The publication deadline must follow the start token.");
        }

        StartTimestamp = startTimestamp;
        DeadlineTimestamp = deadlineTimestamp;
        DeadlineCancellationToken = deadlineCancellationToken;
        PacketFingerprint = packetFingerprint ?? throw new ArgumentNullException(nameof(packetFingerprint));
        AdvisoryAuthorityFingerprint = advisoryAuthorityFingerprint ??
            throw new ArgumentNullException(nameof(advisoryAuthorityFingerprint));
        PublicationSnapshot = publicationSnapshot ??
            throw new ArgumentNullException(nameof(publicationSnapshot));
        CommittedCompletionSummary = new PreparedChatCommittedCompletionSummary(
            completionSummary);
        AcceptedCommit = new AcceptedPublicationCommit(this);
        TimedOutCommit = new AcceptedPublicationCommit(this);
        CancelledCommit = new AcceptedPublicationCommit(this);
    }

    public GroundedAnswer AcceptedAnswer { get; }

    public ChatConversationState ProposedConversation { get; }

    public PreparedChatCompletionSummaryTemplate CompletionSummary { get; }

    public PreparedChatRevealDirective Reveal { get; }

    public long StartTimestamp { get; }

    public long DeadlineTimestamp { get; }

    public CancellationToken DeadlineCancellationToken { get; }

    public string PacketFingerprint { get; }

    public string AdvisoryAuthorityFingerprint { get; }

    public long ConversationGeneration => PublicationSnapshot.Generation;

    public PreparedChatPublicationState State =>
        (PreparedChatPublicationState)Volatile.Read(ref _state);

    internal AcceptedPublicationCommit AcceptedCommit { get; }

    internal AcceptedPublicationCommit TimedOutCommit { get; }

    internal AcceptedPublicationCommit CancelledCommit { get; }

    internal PreparedChatCommittedCompletionSummary CommittedCompletionSummary { get; }

    internal PreparedChatPublicationSnapshot PublicationSnapshot { get; }

    internal PreparedChatPublicationAuthority PublicationAuthority =>
        PublicationSnapshot.Authority;

    internal void SetAcceptedPrimitives(long sampledTimestamp, long acceptedDurationMilliseconds)
    {
        _publicationTimestamp = sampledTimestamp;
        CommittedCompletionSummary.SetAcceptedDuration(acceptedDurationMilliseconds);
    }

    internal bool TryTransition(PreparedChatPublicationState terminalState) =>
        Interlocked.CompareExchange(
            ref _state,
            (int)terminalState,
            (int)PreparedChatPublicationState.Pending) ==
        (int)PreparedChatPublicationState.Pending;

    internal long PublicationTimestamp => Volatile.Read(ref _publicationTimestamp);

    internal AcceptedPublicationCommit TerminalCommit => State switch
    {
        PreparedChatPublicationState.AcceptedPublicationCommit => AcceptedCommit,
        PreparedChatPublicationState.TimedOutBeforePublication => TimedOutCommit,
        PreparedChatPublicationState.CancelledBeforePublication => CancelledCommit,
        _ => throw new InvalidOperationException("The publication candidate is not terminal."),
    };
}

public sealed class AcceptedPublicationCommit
{
    private readonly PreparedChatPublicationCandidate _candidate;

    internal AcceptedPublicationCommit(PreparedChatPublicationCandidate candidate) =>
        _candidate = candidate;

    public PreparedChatPublicationState State => _candidate.State;

    public bool Published => State == PreparedChatPublicationState.AcceptedPublicationCommit;

    public GroundedAnswer? Answer => Published ? _candidate.AcceptedAnswer : null;

    public ChatConversationState? Conversation => Published ? _candidate.ProposedConversation : null;

    public PreparedChatRevealDirective? Reveal => Published ? _candidate.Reveal : null;

    public long? PublicationTimestamp =>
        Published ? _candidate.PublicationTimestamp : null;

    public PreparedChatCommittedCompletionSummary? CompletionSummary =>
        Published ? _candidate.CommittedCompletionSummary : null;
}

public interface IPreparedChatPublicationOwner
{
    PreparedChatPublicationSnapshot CaptureSnapshot();

    void Reset();

    AcceptedPublicationCommit Commit(
        PreparedChatPublicationCandidate candidate,
        CancellationToken cancellationToken);
}

public sealed class PreparedChatPublicationOwner(TimeProvider timeProvider) :
    IPreparedChatPublicationOwner
{
    private readonly PreparedChatPublicationAuthority _authority = new();

    public PreparedChatPublicationSnapshot CaptureSnapshot() => _authority.Capture();

    public void Reset() => _authority.Reset();

    public AcceptedPublicationCommit Commit(
        PreparedChatPublicationCandidate candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        lock (_authority.Gate)
        {
            if (candidate.State != PreparedChatPublicationState.Pending)
            {
                return candidate.TerminalCommit;
            }

            if (!ReferenceEquals(candidate.PublicationAuthority, _authority) ||
                !_authority.IsCurrentGenerationUnderLock(candidate.ConversationGeneration))
            {
                candidate.TryTransition(PreparedChatPublicationState.CancelledBeforePublication);
                return candidate.CancelledCommit;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                candidate.TryTransition(PreparedChatPublicationState.CancelledBeforePublication);
                return candidate.CancelledCommit;
            }

            if (candidate.DeadlineCancellationToken.IsCancellationRequested)
            {
                candidate.TryTransition(PreparedChatPublicationState.TimedOutBeforePublication);
                return candidate.TimedOutCommit;
            }

            long sampledTimestamp = timeProvider.GetTimestamp();
            if (sampledTimestamp >= candidate.DeadlineTimestamp)
            {
                candidate.TryTransition(PreparedChatPublicationState.TimedOutBeforePublication);
                return candidate.TimedOutCommit;
            }

            double elapsedTimestampUnits =
                (double)sampledTimestamp - candidate.StartTimestamp;
            double deadlineTimestampUnits =
                (double)candidate.DeadlineTimestamp - candidate.StartTimestamp;
            long acceptedDurationMilliseconds = (long)Math.Clamp(
                (elapsedTimestampUnits / deadlineTimestampUnits) * 29_000d,
                0d,
                29_000d);
            long remainingBudgetMilliseconds = 29_000 - acceptedDurationMilliseconds;
            candidate.Reveal.SelectForRemainingBudget(Math.Min(
                remainingBudgetMilliseconds,
                candidate.Reveal.PreparedRemainingBudgetMilliseconds));
            candidate.SetAcceptedPrimitives(sampledTimestamp, acceptedDurationMilliseconds);
            candidate.TryTransition(PreparedChatPublicationState.AcceptedPublicationCommit);
            _authority.SetCurrentUnderLock(candidate.AcceptedCommit);
            return candidate.AcceptedCommit;
        }
    }
}
