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

public sealed record PreparedChatRevealDirective(
    PreparedChatRevealMode Mode,
    long AbsoluteDeadlineUnixTimeMilliseconds)
{
    public static PreparedChatRevealDirective ImmediateAccepted(long absoluteDeadline) =>
        new(PreparedChatRevealMode.ImmediateAccepted, absoluteDeadline);

    public static PreparedChatRevealDirective ProgressiveAccepted(long absoluteDeadline) =>
        new(PreparedChatRevealMode.ProgressiveAccepted, absoluteDeadline);
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

    public PreparedChatPublicationState State =>
        (PreparedChatPublicationState)Volatile.Read(ref _state);

    internal AcceptedPublicationCommit AcceptedCommit { get; }

    internal AcceptedPublicationCommit TimedOutCommit { get; }

    internal AcceptedPublicationCommit CancelledCommit { get; }

    internal PreparedChatCommittedCompletionSummary CommittedCompletionSummary { get; }

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
    AcceptedPublicationCommit Commit(
        PreparedChatPublicationCandidate candidate,
        CancellationToken cancellationToken);
}

public sealed class PreparedChatPublicationOwner(TimeProvider timeProvider) :
    IPreparedChatPublicationOwner
{
    private readonly object _gate = new();

    public AcceptedPublicationCommit Commit(
        PreparedChatPublicationCandidate candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        lock (_gate)
        {
            if (candidate.State != PreparedChatPublicationState.Pending)
            {
                return candidate.TerminalCommit;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                candidate.TryTransition(PreparedChatPublicationState.CancelledBeforePublication);
                return candidate.TerminalCommit;
            }

            if (candidate.DeadlineCancellationToken.IsCancellationRequested)
            {
                candidate.TryTransition(PreparedChatPublicationState.TimedOutBeforePublication);
                return candidate.TerminalCommit;
            }

            long sampledTimestamp = timeProvider.GetTimestamp();
            if (sampledTimestamp >= candidate.DeadlineTimestamp)
            {
                candidate.TryTransition(PreparedChatPublicationState.TimedOutBeforePublication);
                return candidate.TerminalCommit;
            }

            long acceptedDurationMilliseconds = Math.Max(
                0,
                (long)timeProvider.GetElapsedTime(candidate.StartTimestamp, sampledTimestamp)
                    .TotalMilliseconds);
            candidate.SetAcceptedPrimitives(sampledTimestamp, acceptedDurationMilliseconds);
            candidate.TryTransition(PreparedChatPublicationState.AcceptedPublicationCommit);
            return candidate.TerminalCommit;
        }
    }
}
