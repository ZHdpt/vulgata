using Vulgata.Core.DomainServices;
using System.Globalization;

namespace Vulgata.Web.Components.Pages;

internal enum ChatTurnState
{
    Pending,
    Revealing,
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

    public IReadOnlyList<ChatClaimReveal> Claims { get; private set; } = [];

    public string? Message { get; private set; }

    public static ChatTurn CreatePending(string question, ChatMode mode) => new(question, mode);

    public void Complete(GroundedAnswer answer)
    {
        BeginReveal(answer);
        CompleteReveal();
    }

    public void BeginReveal(GroundedAnswer answer)
    {
        Answer = answer ?? throw new ArgumentNullException(nameof(answer));
        Claims = answer.MaterialClaims.Select(claim => new ChatClaimReveal(claim)).ToArray();
        Message = null;
        State = ChatTurnState.Revealing;
    }

    public void CompleteReveal()
    {
        if (State != ChatTurnState.Revealing)
        {
            throw new InvalidOperationException("Only a revealing turn can be completed.");
        }

        foreach (ChatClaimReveal claim in Claims)
        {
            claim.Complete();
        }

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
        Claims = [];
        Message = message;
        State = state;
    }
}

internal sealed class ChatClaimReveal
{
    private readonly string[] _textElements;
    private int _visibleCount;

    public ChatClaimReveal(MaterialClaim claim)
    {
        Claim = claim ?? throw new ArgumentNullException(nameof(claim));
        var elements = new List<string>();
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(claim.Text);
        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.GetTextElement());
        }

        _textElements = elements.ToArray();
    }

    public MaterialClaim Claim { get; }

    public string VisibleText => string.Concat(_textElements.AsSpan(0, _visibleCount));

    public bool IsComplete => _visibleCount == _textElements.Length;

    public bool RevealNext()
    {
        if (IsComplete)
        {
            return false;
        }

        _visibleCount++;
        return true;
    }

    public void Complete() => _visibleCount = _textElements.Length;
}
