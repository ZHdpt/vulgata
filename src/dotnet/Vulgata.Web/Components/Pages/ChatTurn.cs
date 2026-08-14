using Vulgata.Core.DomainServices;

namespace Vulgata.Web.Components.Pages;

internal enum ChatTurnState
{
    Pending,
    Complete,
    Unsupported,
    Error,
    Cancelled,
}

internal sealed class ChatTurn
{
    private ChatTurn(string question, ChatMode mode)
    {
        Id = Guid.NewGuid();
        Question = question;
        Mode = mode;
    }

    public Guid Id { get; }

    public string Question { get; }

    public ChatMode Mode { get; }

    public ChatTurnState State { get; private set; } = ChatTurnState.Pending;

    public GroundedAnswer? Answer { get; private set; }

    public string? Message { get; private set; }

    public static ChatTurn CreatePending(string question, ChatMode mode) => new(question, mode);

    public void Complete(GroundedAnswer answer)
    {
        Answer = answer ?? throw new ArgumentNullException(nameof(answer));
        Message = null;
        State = ChatTurnState.Complete;
    }

    public void Finish(ChatTurnState state, string message)
    {
        if (state is ChatTurnState.Pending or ChatTurnState.Complete)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A terminal turn requires a message.", nameof(message));
        }

        Answer = null;
        Message = message;
        State = state;
    }
}
