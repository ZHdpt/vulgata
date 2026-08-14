# Transient Chat History and Progressive Reveal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Retain an in-page Chat timeline until **新对话** is clicked and progressively reveal each validated Grounded Answer without sending history to the coordinator or provider.

**Architecture:** Replace the page's single published result fields with page-local `ChatTurn` records. Keep `PreparedChatCoordinator.AskAsync` atomic and unchanged; after it returns a complete Grounded Answer, drive a Unicode-text-element reveal loop in the Blazor component and expose citations only when their claim is complete.

**Tech Stack:** .NET 10, Blazor interactive server rendering, C# `StringInfo`, bUnit/xUnit, Razor scoped CSS, collocated JavaScript module for `prefers-reduced-motion`.

**Spec:** `docs/superpowers/specs/2026-08-14-transient-chat-history-progressive-reveal-design.md`

## Global Constraints

- Chat history is component-lifetime UI state only; do not persist it in PostgreSQL, browser storage, or server session state.
- Every coordinator request contains exactly the current normalized question, Chat Mode, System, and two Repository IDs; never add prior turns to `PreparedChatQuestionRequest`, `ChatQuestionRequest`, or provider JSON.
- Do not modify Core, Infrastructure, evidence retrieval, Candidate Claim selection, currentness validation, authorization, or Grounded Answer composition.
- Preserve the existing complete-answer safety contract: no text is published before `PreparedChatCoordinator.AskAsync` returns a successful validated Grounded Answer.
- Chinese UI copy remains Simplified Chinese.
- Reveal one Unicode text element every 25 ms; newly revealed text uses a 150 ms fade-in effect.
- A claim's Document Citations remain absent until that claim is completely revealed.
- Unsupported, failure, and cancellation messages appear immediately.
- Reduced-motion users receive the complete answer and citations immediately.
- Never commit a failing test.

---

## File Structure

- Create `src/dotnet/Vulgata.Web/Components/Pages/ChatTurn.cs`: page-local turn state and Unicode-safe per-claim reveal state.
- Modify `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs`: timeline ownership, request-to-turn mapping, reset/cancellation, and reveal loop.
- Modify `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`: render every retained turn, pending state, revealed claims, and completed citations.
- Modify `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.css`: timeline/reveal styles, fade-in keyframes, and reduced-motion override.
- Create `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.js`: one `matchMedia('(prefers-reduced-motion: reduce)')` query.
- Create `src/dotnet/Vulgata.Web/Components/Pages/ChatRevealDelay.cs`: production 25 ms presentation-delay boundary.
- Modify `src/dotnet/Vulgata.Web/Program.cs`: register the scoped presentation-delay implementation.
- Modify `src/dotnet/Vulgata.Web/Vulgata.Web.csproj`: expose Web internals to `Vulgata.Tests` so the small presentation state can be unit-tested without making it a public product API.
- Create `tests/Vulgata.Tests/ChatTurnTests.cs`: exact Unicode reveal-state tests.
- Modify `tests/Vulgata.Tests/ChatPageTests.cs`: timeline retention, isolated requests, reset, progressive reveal, reduced motion, cancellation, and accessibility assertions.

---

### Task 1: Retain Chat Turns and Make New Conversation the Sole Reset

**Files:**
- Create: `src/dotnet/Vulgata.Web/Components/Pages/ChatTurn.cs`
- Modify: `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs`
- Modify: `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`
- Modify: `tests/Vulgata.Tests/ChatPageTests.cs`

**Interfaces:**
- Consumes: existing `PreparedChatCoordinator.AskAsync(PreparedChatQuestionRequest, CancellationToken)` and `PreparedChatResult`.
- Produces: `ChatTurn.CreatePending(string question, ChatMode mode)`, turn state transitions, and `IReadOnlyList<ChatTurn> _turns` rendered by the page. Task 2 adds reveal details to the same type without changing coordinator contracts.

- [ ] **Step 1: Replace the old replacement-semantics tests with failing timeline tests**

In `ChatPageTests.cs`, replace `ChatPage_NewRequest_ReplacesAnswerOnlyAfterCompleteResult` with:

```csharp
[Fact]
public async Task ChatPage_SecondRequest_RetainsFirstTurnWhilePendingAndAfterCompletion()
{
    IRenderedComponent<ChatPage> cut = Render<ChatPage>();
    cut.WaitForState(() => cut.Markup.Contains("支付系统", StringComparison.Ordinal));
    PrepareValidSelection(cut, ChatMode.Business);

    _retriever.ClaimText = "第一份完整回答。";
    await SendQuestionAsync(cut, "第一个问题");

    _retriever.ClaimText = "第二份完整回答。";
    _dispatcher.BlockNextCall();
    cut.Find("#chat-question").Input("第二个问题");
    Task sendTask = cut.Find("#chat-question").KeyDownAsync(
        new KeyboardEventArgs { Key = "Enter" });
    await _dispatcher.Entered.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.Contains("第一个问题", cut.Markup, StringComparison.Ordinal);
    Assert.Contains("第一份完整回答。", cut.Markup, StringComparison.Ordinal);
    Assert.Contains("第二个问题", cut.Markup, StringComparison.Ordinal);
    Assert.Single(cut.FindAll(".complete-answer"));

    _dispatcher.ReleaseBlockedCall();
    await sendTask;

    Assert.Equal(2, cut.FindAll("[data-chat-turn]").Count);
    Assert.Contains("第一份完整回答。", cut.Markup, StringComparison.Ordinal);
    Assert.Contains("第二份完整回答。", cut.Markup, StringComparison.Ordinal);
    Assert.Equal(2, cut.FindAll(".complete-answer").Count);
}
```

Replace `ChatPage_ModeScopeAndFailureChanges_ClearPriorTransientAnswer` with a test that sends one successful Business turn, switches to IT Mode, changes both Repository selections, and sends a provider-failure turn. Assert that the first question, first answer, first citation, and failure message all remain visible. Add this request-isolation assertion by recording requests in the retriever:

```csharp
public List<ChatQuestionRequest> Requests { get; } = [];

public Task<ChatEvidenceResult> RetrieveAsync(
    ChatQuestionRequest request,
    RepositoryAccessScope accessScope,
    CancellationToken cancellationToken = default)
{
    Requests.Add(request);
    // Keep the existing packet construction unchanged.
}
```

After two sends, assert:

```csharp
Assert.Equal(["第一个问题", "第二个问题"],
    _retriever.Requests.Select(request => request.Question));
Assert.All(_retriever.Requests, request =>
    Assert.DoesNotContain("第一个问题\n第二个问题", request.Question, StringComparison.Ordinal));
```

Update `ChatPage_StaleRevokedAndInvalidRequests_ClearAllPriorClaimsAndCitationsAsync` so stale, revoked, and invalid outcomes append bounded messages while the earlier complete turn and authorized citation remain present. Keep the existing assertions that failure turns publish no new claim or citation.

- [ ] **Step 2: Run the focused tests and observe the expected failures**

Run:

```powershell
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ChatPageTests" --verbosity quiet
```

Expected: the new retention assertions fail because `SendAsync`, `SelectMode`, and scope-change handlers currently call `ClearPublishedInteraction` and the Razor page renders only one result.

- [ ] **Step 3: Add the minimal page-local turn model**

Create `ChatTurn.cs`:

```csharp
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
        State = ChatTurnState.Pending;
    }

    public Guid Id { get; }
    public string Question { get; }
    public ChatMode Mode { get; }
    public ChatTurnState State { get; private set; }
    public GroundedAnswer? Answer { get; private set; }
    public string? Message { get; private set; }

    public static ChatTurn CreatePending(string question, ChatMode mode) =>
        new(question, mode);

    public void Complete(GroundedAnswer answer)
    {
        Answer = answer ?? throw new ArgumentNullException(nameof(answer));
        Message = null;
        State = ChatTurnState.Complete;
    }

    public void Finish(ChatTurnState state, string message)
    {
        if (state is ChatTurnState.Pending or ChatTurnState.Complete ||
            string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A terminal non-success state and message are required.");
        }

        Answer = null;
        Message = message;
        State = state;
    }
}
```

- [ ] **Step 4: Convert `ChatPage` from single-result fields to `_turns`**

In `ChatPage.razor.cs`:

```csharp
private readonly List<ChatTurn> _turns = [];
private ChatTurn? _activeTurn;
```

Remove `_submittedQuestion` and `_answer`. At the start of `SendAsync`, append instead of clearing:

```csharp
ChatTurn turn = ChatTurn.CreatePending(question, _selectedMode!.Value);
_turns.Add(turn);
_activeTurn = turn;
```

Map results only onto `turn`:

```csharp
if (result.Kind == PreparedChatResultKind.Succeeded && result.Answer is not null)
{
    turn.Complete(result.Answer);
    _message = null;
    _loadState = LoadState.Loaded;
}
else if (result.Kind == PreparedChatResultKind.Unsupported)
{
    turn.Finish(ChatTurnState.Unsupported, result.Message ?? _unexpectedFailureMessage);
    _loadState = LoadState.Empty;
}
else
{
    turn.Finish(
        result.Kind == PreparedChatResultKind.Cancelled
            ? ChatTurnState.Cancelled
            : ChatTurnState.Error,
        result.Message ?? _unexpectedFailureMessage);
    _loadState = result.Kind == PreparedChatResultKind.Cancelled
        ? ReadyState
        : LoadState.Error;
}
```

In the exception handlers, update only the captured `turn`. In `finally`, clear `_activeTurn` only when it still references `turn`.

Change `SelectMode`, `OnSystemChanged`, `OnFirstRepositoryChanged`, and `OnSecondRepositoryChanged` to clear only `_message` and `_conversationNotice`; they must not remove `_turns`. Preserve the existing draft-question behavior.

Change `StartNewConversationAsync` to call `_turns.Clear()` after cancellation and the 180 ms transition. Keep selected mode/System/Repositories intact. Keep `_conversationVersion` checks so late completion cannot recreate a turn.

Change `HandleInvalidSubmit` to set the transient validation `_message` without clearing `_turns`.

- [ ] **Step 5: Render the retained timeline**

Replace the single question/answer/message block in `ChatPage.razor` with:

```razor
@foreach (ChatTurn turn in _turns)
{
    <div class="chat-turn" data-chat-turn="@turn.Id">
        <div class="chat-message chat-message--user">@turn.Question</div>

        @if (turn.State == ChatTurnState.Pending)
        {
            <div class="chat-thinking" role="status">
                <span class="chat-thinking__spark" aria-hidden="true">✦</span>
                <span class="chat-thinking__copy">
                    <span>@ActiveStatusMessage</span>
                    <small>AI 正在分析跨仓库上下文</small>
                </span>
                <span class="chat-thinking__dots" aria-hidden="true"><i></i><i></i><i></i></span>
            </div>
        }
        else if (turn.Answer is not null)
        {
            <article class="chat-message chat-message--assistant complete-answer"
                     data-answer-state="complete">
                @foreach (MaterialClaim claim in turn.Answer.MaterialClaims)
                {
                    <section class="answer-claim">
                        <p class="material-claim">@claim.Text</p>
                        @foreach (DocumentCitation citation in turn.Answer.DocumentCitations.Where(
                            citation => claim.CitationIds.Contains(citation.CitationId, StringComparer.Ordinal)))
                        {
                            @RenderCitation(turn.Answer.Mode, citation)
                        }
                    </section>
                }
            </article>
        }
        else if (turn.Message is not null)
        {
            <p class="chat-message chat-message--assistant"
               data-answer-state="@TurnAnswerState(turn)">@turn.Message</p>
        }
    </div>
}
```

Extract the existing citation markup into a private Razor `RenderFragment` helper or a small local `RenderCitation` fragment without changing Business/IT locator rules. Show the welcome block only when `_turns.Count == 0` and no transient message is active.

- [ ] **Step 6: Run focused tests and verify retention is green**

Run the same `ChatPageTests` command from Step 2.

Expected: all `ChatPageTests` pass, including two retained complete answers, retained history through mode/scope changes, isolated per-question requests, and **新对话** reset.

- [ ] **Step 7: Commit the timeline slice**

```powershell
git add src/dotnet/Vulgata.Web/Components/Pages/ChatTurn.cs src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs tests/Vulgata.Tests/ChatPageTests.cs
git commit -m "feat(chat): retain transient conversation timeline"
```

---

### Task 2: Add a Unicode-Safe Claim Reveal State Machine

**Files:**
- Modify: `src/dotnet/Vulgata.Web/Vulgata.Web.csproj`
- Modify: `src/dotnet/Vulgata.Web/Components/Pages/ChatTurn.cs`
- Create: `tests/Vulgata.Tests/ChatTurnTests.cs`

**Interfaces:**
- Consumes: `MaterialClaim.Text` and `GroundedAnswer.MaterialClaims`.
- Produces: `ChatClaimReveal.VisibleText`, `ChatClaimReveal.IsComplete`, `ChatClaimReveal.RevealNext()`, `ChatClaimReveal.Complete()`, and `ChatTurn.BeginReveal(GroundedAnswer)` for Task 3.

- [ ] **Step 1: Expose Web internals to the test assembly**

Add to `Vulgata.Web.csproj`:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="Vulgata.Tests" />
</ItemGroup>
```

- [ ] **Step 2: Write failing exact-state and Unicode tests**

Create `ChatTurnTests.cs`:

```csharp
using Vulgata.Core.DomainServices;
using Vulgata.Web.Components.Pages;

namespace Vulgata.Tests;

public sealed class ChatTurnTests
{
    [Fact]
    public void RevealNext_ChineseAndEmoji_RevealsWholeTextElementsInOrder()
    {
        MaterialClaim claim = MaterialClaim.Create("claim-1", 1, "账户👨‍👩‍👧‍👦完成。", ["citation-1"]);
        var reveal = new ChatClaimReveal(claim);

        Assert.Equal(string.Empty, reveal.VisibleText);
        Assert.True(reveal.RevealNext());
        Assert.Equal("账", reveal.VisibleText);

        while (reveal.RevealNext())
        {
        }

        Assert.Equal(claim.Text, reveal.VisibleText);
        Assert.True(reveal.IsComplete);
        Assert.Contains("👨‍👩‍👧‍👦", reveal.VisibleText, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_PartiallyRevealedClaim_PublishesExactFinalText()
    {
        MaterialClaim claim = MaterialClaim.Create("claim-1", 1, "e\u0301账户", ["citation-1"]);
        var reveal = new ChatClaimReveal(claim);

        reveal.RevealNext();
        Assert.Equal("e\u0301", reveal.VisibleText);

        reveal.Complete();

        Assert.Equal(claim.Text, reveal.VisibleText);
        Assert.True(reveal.IsComplete);
    }
}
```

- [ ] **Step 3: Run the new tests and verify the type is missing**

```powershell
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ChatTurnTests" --verbosity quiet
```

Expected: compilation fails because `ChatClaimReveal` does not exist.

- [ ] **Step 4: Implement exact Unicode text-element reveal state**

Extend `ChatTurn.cs`:

```csharp
using System.Globalization;

internal sealed class ChatClaimReveal
{
    private readonly string[] _textElements;
    private int _visibleCount;

    public ChatClaimReveal(MaterialClaim claim)
    {
        Claim = claim ?? throw new ArgumentNullException(nameof(claim));
        _textElements = StringInfo.GetTextElementEnumerator(claim.Text)
            .AsEnumerable()
            .Select(element => element.GetTextElement())
            .ToArray();
    }

    public MaterialClaim Claim { get; }
    public string VisibleText => string.Concat(_textElements.Take(_visibleCount));
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
```

Because `TextElementEnumerator` does not provide a repository extension method, implement its conversion explicitly rather than inventing `AsEnumerable()`:

```csharp
private static string[] SplitTextElements(string text)
{
    var elements = new List<string>();
    TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text);
    while (enumerator.MoveNext())
    {
        elements.Add(enumerator.GetTextElement());
    }

    return elements.ToArray();
}
```

Add `ChatTurnState.Revealing`, `IReadOnlyList<ChatClaimReveal> Claims`, and:

```csharp
public void BeginReveal(GroundedAnswer answer)
{
    Answer = answer ?? throw new ArgumentNullException(nameof(answer));
    Claims = answer.MaterialClaims.Select(claim => new ChatClaimReveal(claim)).ToArray();
    Message = null;
    State = ChatTurnState.Revealing;
}

public void CompleteReveal()
{
    foreach (ChatClaimReveal claim in Claims)
    {
        claim.Complete();
    }

    State = ChatTurnState.Complete;
}
```

Initialize `Claims` to an empty read-only collection and clear it in terminal failure transitions.

- [ ] **Step 5: Run unit and component tests**

```powershell
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ChatTurnTests|FullyQualifiedName~ChatPageTests" --verbosity quiet
```

Expected: all selected tests pass; Task 1 behavior remains unchanged because `ChatPage` still completes successful turns immediately.

- [ ] **Step 6: Commit the reveal-state slice**

```powershell
git add src/dotnet/Vulgata.Web/Vulgata.Web.csproj src/dotnet/Vulgata.Web/Components/Pages/ChatTurn.cs tests/Vulgata.Tests/ChatTurnTests.cs
git commit -m "feat(chat): add unicode-safe answer reveal state"
```

---

### Task 3: Integrate Timed Reveal, Fade-In, Reduced Motion, and Cancellation

**Files:**
- Modify: `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs`
- Modify: `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`
- Modify: `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.css`
- Create: `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.js`
- Create: `src/dotnet/Vulgata.Web/Components/Pages/ChatRevealDelay.cs`
- Modify: `src/dotnet/Vulgata.Web/Program.cs`
- Modify: `tests/Vulgata.Tests/ChatPageTests.cs`

**Interfaces:**
- Consumes: Task 2's `ChatTurn.BeginReveal`, `ChatClaimReveal.RevealNext`, `ChatClaimReveal.IsComplete`, and `ChatTurn.CompleteReveal`.
- Produces: `IChatRevealDelay.DelayAsync(CancellationToken)`, `RevealAnswerAsync(ChatTurn, GroundedAnswer, int, CancellationToken)`, collocated JavaScript `prefersReducedMotion()`, and DOM states `data-answer-state="revealing|complete"`.

- [ ] **Step 1: Add failing progressive-reveal component tests**

In `ChatPageTests.cs`, configure the collocated module in the constructor:

```csharp
var module = JSInterop.SetupModule("./Components/Pages/ChatPage.razor.js");
module.Setup<bool>("prefersReducedMotion").SetResult(false);
```

Register a controllable `ManualRevealDelay` test double for the production
`IChatRevealDelay` boundary before constructing the coordinator. Render normally:

```csharp
private readonly ManualRevealDelay _revealDelay = new();

Services.AddSingleton<IChatRevealDelay>(_revealDelay);

private IRenderedComponent<ChatPage> RenderChatPage() => Render<ChatPage>();
```

Replace `Render<ChatPage>()` in this test class with `RenderChatPage()`. The override is internal presentation-test plumbing and must not be registered in production DI.

Add:

```csharp
[Fact]
public async Task ChatPage_SuccessfulAnswer_RevealsFirstElementThenCompletesWithCitations()
{
    IRenderedComponent<ChatPage> cut = RenderChatPage();
    cut.WaitForState(() => cut.Markup.Contains("支付系统", StringComparison.Ordinal));
    PrepareValidSelection(cut, ChatMode.Business);
    _retriever.ClaimText = "账户完成。";

    Task sendTask = BeginQuestionAsync(cut, "如何完成？");
    cut.WaitForState(() => cut.FindAll("[data-answer-state='revealing']").Count == 1);

    Assert.Equal("账", cut.Find(".material-claim__revealed").TextContent);
    Assert.Empty(cut.FindAll(".document-citation"));

    _revealDelay.ReleaseAll();
    await sendTask;

    Assert.Equal("账户完成。", cut.Find(".material-claim").TextContent);
    Assert.Single(cut.FindAll(".document-citation"));
    Assert.Equal("complete", cut.Find(".complete-answer").GetAttribute("data-answer-state"));
}
```

Add tests that:

- click **新对话** while reveal is paused and prove no later character/citation reappears;
- submit text containing `e\u0301` and `👨‍👩‍👧‍👦`, advance one tick at a time, and assert no partial grapheme is rendered;
- configure `prefersReducedMotion` to return `true`, send a question, and assert the complete answer and citation are present without any delay request;
- assert a revealing article uses `aria-live="off"`, has an accessible full-answer label, and its changing visual span has `aria-hidden="true"`.

The manual delay implementation is:

```csharp
private sealed class ManualRevealDelay
{
    private readonly Queue<TaskCompletionSource> _pending = new();
    private bool _releaseImmediately;
    public int CallCount { get; private set; }

    public Task DelayAsync(CancellationToken cancellationToken)
    {
        CallCount++;
        if (_releaseImmediately)
        {
            return Task.CompletedTask;
        }

        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => source.TrySetCanceled(cancellationToken));
        _pending.Enqueue(source);
        return source.Task;
    }

    public void ReleaseNext() => _pending.Dequeue().TrySetResult();

    public void ReleaseAll()
    {
        _releaseImmediately = true;
        while (_pending.TryDequeue(out TaskCompletionSource? source))
        {
            source.TrySetResult();
        }
    }
}
```

Make `BeginQuestionAsync` return the outstanding keydown task rather than awaiting it, so tests can inspect reveal state before completion.

- [ ] **Step 2: Run the component tests and verify reveal assertions fail**

```powershell
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ChatPageTests" --verbosity quiet
```

Expected: compilation fails because `IChatRevealDelay`, revealing DOM state, and the collocated module do not exist.

- [ ] **Step 3: Add the reduced-motion JavaScript query**

Create `ChatPage.razor.js`:

```javascript
export function prefersReducedMotion() {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}
```

Inject `IJSRuntime` into the page, cache the imported `IJSObjectReference`, and use:

```csharp
private async ValueTask<bool> PrefersReducedMotionAsync(CancellationToken cancellationToken)
{
    _chatModule ??= await JsRuntime.InvokeAsync<IJSObjectReference>(
        "import",
        cancellationToken,
        "./Components/Pages/ChatPage.razor.js");
    return await _chatModule.InvokeAsync<bool>(
        "prefersReducedMotion",
        cancellationToken);
}
```

If JavaScript is temporarily unavailable, fail to the non-reduced reveal path; do not fail the answer.

- [ ] **Step 4: Implement the reveal loop after atomic success**

Create `ChatRevealDelay.cs`:

```csharp
namespace Vulgata.Web.Components.Pages;

internal interface IChatRevealDelay
{
    Task DelayAsync(CancellationToken cancellationToken);
}

internal sealed class ChatRevealDelay : IChatRevealDelay
{
    private static readonly TimeSpan _interval = TimeSpan.FromMilliseconds(25);

    public Task DelayAsync(CancellationToken cancellationToken) =>
        Task.Delay(_interval, cancellationToken);
}
```

Register `IChatRevealDelay` as scoped in `Program.cs`, inject it into
`ChatPage.razor`, and add `private CancellationTokenSource? _revealCancellation;`
to the code-behind.

On successful coordinator return, call:

```csharp
await RevealAnswerAsync(turn, result.Answer, conversationVersion, cancellation.Token);
```

Implement:

```csharp
private async Task RevealAnswerAsync(
    ChatTurn turn,
    GroundedAnswer answer,
    int conversationVersion,
    CancellationToken requestCancellation)
{
    turn.BeginReveal(answer);
    bool reducedMotion = await PrefersReducedMotionAsync(requestCancellation);
    if (reducedMotion)
    {
        turn.CompleteReveal();
        await InvokeAsync(StateHasChanged);
        return;
    }

    using var linked = CancellationTokenSource.CreateLinkedTokenSource(requestCancellation);
    _revealCancellation = linked;
    try
    {
        foreach (ChatClaimReveal claim in turn.Claims)
        {
            if (claim.RevealNext())
            {
                await InvokeAsync(StateHasChanged);
            }

            while (!claim.IsComplete)
            {
                await RevealDelay.DelayAsync(linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                if (conversationVersion != _conversationVersion)
                {
                    return;
                }

                claim.RevealNext();
                await InvokeAsync(StateHasChanged);
            }
        }

        turn.CompleteReveal();
        await InvokeAsync(StateHasChanged);
    }
    finally
    {
        if (ReferenceEquals(_revealCancellation, linked))
        {
            _revealCancellation = null;
        }
    }
}
```

`StartNewConversationAsync`, `CancelRequest`, and component disposal must cancel `_revealCancellation`. Replace `@implements IDisposable` with `@implements IAsyncDisposable`, then implement `DisposeAsync` so it cancels and disposes both cancellation sources before asynchronously disposing the cached JavaScript module.

- [ ] **Step 5: Render revealed text and gate citations**

For each `ChatClaimReveal` in `turn.Claims`, render:

```razor
<section class="answer-claim" data-claim-complete="@claim.IsComplete.ToString().ToLowerInvariant()">
    <p class="material-claim"
       aria-label="@claim.Claim.Text">
        <span class="material-claim__revealed" aria-hidden="true">@claim.VisibleText</span>
    </p>
    @if (claim.IsComplete)
    {
        @foreach (DocumentCitation citation in turn.Answer!.DocumentCitations.Where(
            citation => claim.Claim.CitationIds.Contains(citation.CitationId, StringComparer.Ordinal)))
        {
            @RenderCitation(turn.Answer.Mode, citation)
        }
    }
</section>
```

Set the answer article's `data-answer-state` to `revealing` or `complete`, and set `aria-live="off"` while revealing so the parent live region does not announce every mutation. The stable full answer remains available through each claim's `aria-label`.

- [ ] **Step 6: Add the transient fade and reduced-motion CSS**

Add to `ChatPage.razor.css`:

```css
.chat-turn {
    display: contents;
}

.material-claim__revealed {
    animation: claim-text-in 150ms ease-out both;
}

@keyframes claim-text-in {
    from { opacity: 0.35; }
    to { opacity: 1; }
}

@media (prefers-reduced-motion: reduce) {
    .material-claim__revealed {
        animation: none;
    }
}
```

Because Blazor replaces the span's text node, key the visible segment by its text-element count (`@key="claim.VisibleText"`) so the 150 ms animation restarts for each newly published element.

- [ ] **Step 7: Run reveal, timeline, and coordinator-isolation tests**

```powershell
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ChatTurnTests|FullyQualifiedName~ChatPageTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatAgentDispatcherTests" --verbosity quiet
```

Expected: all selected tests pass with exact final text, no split graphemes, citations gated until completion, reduced-motion immediate completion, retained older turns, and unchanged single-question dispatch.

- [ ] **Step 8: Run build and whitespace checks**

```powershell
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore --verbosity quiet
git diff --check
```

Expected: build exits 0 and `git diff --check` reports no errors. Existing package-source and EF version-conflict warnings may remain; do not introduce new warnings from the changed files.

- [ ] **Step 9: Commit the integrated presentation slice**

```powershell
git add src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.css src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.js src/dotnet/Vulgata.Web/Components/Pages/ChatRevealDelay.cs src/dotnet/Vulgata.Web/Program.cs tests/Vulgata.Tests/ChatPageTests.cs
git commit -m "feat(chat): progressively reveal validated answers"
```

---

## Final Verification

Run the complete focused Chat suite:

```powershell
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~Chat" --verbosity quiet
```

Then run the full test project when PostgreSQL and Docker-dependent gates are available:

```powershell
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --verbosity quiet
```

Browser acceptance sequence:

1. Select one System and exactly two Repositories.
2. Send a known-supported Business question and observe first-character reveal and fade-in.
3. Send a second question and confirm both question/answer pairs remain visible.
4. Switch to IT Mode and confirm both historical turns remain visible with their original citation rules.
5. Send an unsupported question and confirm its bounded response appends without erasing history.
6. Click **新对话** and confirm the timeline clears while mode and scope remain selected.
7. Enable operating-system reduced motion, reload, send a supported question, and confirm the complete answer and citations appear immediately.

Keep the Vulgata service stopped after browser verification unless the user explicitly asks to leave it running.
