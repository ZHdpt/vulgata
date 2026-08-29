using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Shared.Chat;
using Vulgata.Web.Data;

namespace Vulgata.Web.Components.Pages;

public partial class ChatPage : IAsyncDisposable
{
    private const long _minimumProgressiveRevealBudgetMilliseconds = 1_000;
    private const string _validationMessage = "请选择业务模式，并输入 1 至 2000 个字符的问题。";
    private const string _unexpectedFailureMessage = "暂时无法生成回答，请稍后重试。";

    // DEMO ONLY:
    // The repository picker currently controls presentation state only.
    // Backend analysis is intentionally fixed to mbs_account + mbs_channel
    // for the competition demo.
    // TODO: Replace these fixed repositories with the repositories selected
    // in the UI when dynamic repository selection is connected to the backend.
    private static readonly IReadOnlyList<string> DemoBackendRepositoryNames =
        ["mbs_account", "mbs_channel"];

    // DEMO UI DATA:
    // Temporary repository catalog used by the competition prototype.
    // Replace with repository/system metadata from the real backend.
    private static readonly IReadOnlyList<DemoRepositorySystem> DemoRepositoryCatalog =
    [
        new("demobank", "DemoBank", ["mbs_account", "mbs_channel"]),
    ];

    private IReadOnlyList<PreparedChatScopeOption> _scopes = [];
    private readonly PreparedChatQuestionRequest _formRequest = new()
    {
        Mode = PreparedChatMode.Business,
    };
    private EditContext _editContext = null!;
    private ElementReference _questionElement;
    private ElementReference _conversationElement;
    private readonly HashSet<string> _selectedUiRepositories = new(StringComparer.Ordinal);
    private string _activeRepositorySystemId = "demobank";
    private bool _isRepositoryPickerOpen;
    private ChatMode? _selectedMode = ChatMode.Business;
    private string _question = string.Empty;
    private string? _message;
    private string? _conversationNotice;
    private readonly List<ChatTurn> _turns = [];
    private CancellationTokenSource? _requestCancellation;
    private CancellationTokenSource? _revealCancellation;
    private IJSObjectReference? _chatModule;
    private bool _scrollConversationAfterRender;
    private bool _isBusy;
    private bool _isStartingNewConversation;
    private int _conversationVersion;
    private LoadState _loadState = LoadState.Idle;

    private string PageModeClass => _selectedMode switch
    {
        ChatMode.Business => "chat-page--business",
        ChatMode.It => "chat-page--it",
        _ => "chat-page--unselected",
    };

    private string ThemeName => _selectedMode == ChatMode.It ? "dark" : "light";

    private string ConversationStateClass => _isStartingNewConversation ?
        "chat-page--starting-conversation" :
        string.Empty;

    private string WelcomeMessage => _selectedMode == ChatMode.It ?
        "你好，需要追踪哪段逻辑？" :
        "你好，想了解什么业务流程？";

    private string QuestionPlaceholder => _selectedMode == ChatMode.It ?
        "输入技术问题…" :
        "描述你想了解的业务流程…";

    private string ActiveStatusMessage => _loadState == LoadState.Cancelling ?
        "正在取消本次提问…" :
        "正在查找相关文档…";

    private DemoRepositorySystem? ActiveRepositorySystem =>
        DemoRepositoryCatalog.SingleOrDefault(system => system.Id == _activeRepositorySystemId);

    private string RepositoryPickerTriggerClass => _isRepositoryPickerOpen
        ? "repository-picker__trigger repository-picker__trigger--open"
        : "repository-picker__trigger";

    protected override async Task OnInitializedAsync()
    {
        _editContext = new EditContext(_formRequest);
        _loadState = LoadState.Loading;
        try
        {
            _scopes = await ChatCoordinator.ListScopesAsync();
            DemoBackendScope? effectiveScope = GetEffectiveRepositoriesForRequest();
            if (effectiveScope is null)
            {
                _loadState = _scopes.Count == 0 ? LoadState.Empty : LoadState.Error;
                _message = _scopes.Count == 0 ? null : "演示所需的固定仓库范围不可用。";
                return;
            }

            _formRequest.SystemId = effectiveScope.SystemId;
            _formRequest.RepositoryIds = effectiveScope.RepositoryIds;
            _loadState = LoadState.Loaded;
        }
        catch (Exception)
        {
            _scopes = [];
            _loadState = LoadState.Error;
        }
    }

    private void ToggleRepositoryPicker()
    {
        _isRepositoryPickerOpen = !_isRepositoryPickerOpen;
    }

    private void CloseRepositoryPicker()
    {
        _isRepositoryPickerOpen = false;
    }

    private void ActivateRepositorySystem(string systemId)
    {
        _activeRepositorySystemId = systemId;
    }

    private void ToggleUiRepository(string repositoryName)
    {
        if (!_selectedUiRepositories.Add(repositoryName))
        {
            _ = _selectedUiRepositories.Remove(repositoryName);
        }
    }

    private void RemoveUiRepository(string repositoryName)
    {
        _ = _selectedUiRepositories.Remove(repositoryName);
    }

    private void HandleRepositoryPickerKeyDown(KeyboardEventArgs args)
    {
        if (StringComparer.Ordinal.Equals(args.Key, "Escape"))
        {
            CloseRepositoryPicker();
        }
    }

    private void SelectMode(ChatMode mode)
    {
        _selectedMode = mode;
        _formRequest.Mode = mode == ChatMode.Business
            ? PreparedChatMode.Business
            : PreparedChatMode.It;
        ResetTransientInteraction(clearQuestion: true);
    }

    private async Task StartNewConversationAsync()
    {
        _conversationVersion++;
        _requestCancellation?.Cancel();
        _revealCancellation?.Cancel();
        _requestCancellation = null;
        _isBusy = false;
        _isStartingNewConversation = true;
        _conversationNotice = null;
        _message = null;
        await InvokeAsync(StateHasChanged);

        await Task.Delay(TimeSpan.FromMilliseconds(180));

        _turns.Clear();
        _question = string.Empty;
        _formRequest.Question = string.Empty;
        _loadState = ReadyState;
        _isStartingNewConversation = false;
        _conversationNotice = "新对话已开始";
        await InvokeAsync(StateHasChanged);
        await _questionElement.FocusAsync();
    }

    private bool IsModeSelected(ChatMode mode) => _selectedMode == mode;

    private string ModeAriaPressed(ChatMode mode) => IsModeSelected(mode) ? "true" : "false";

    private Appearance ModeAppearance(ChatMode mode) =>
        IsModeSelected(mode) ? Appearance.Accent : Appearance.Neutral;

    private void OnQuestionChanged()
    {
        _formRequest.Question = _question;
        if (_loadState == LoadState.Error)
        {
            _message = null;
            _loadState = ReadyState;
        }
    }

    private async Task HandleQuestionKeyDownAsync(KeyboardEventArgs args)
    {
        if (StringComparer.Ordinal.Equals(args.Key, "Escape") && _isBusy)
        {
            CancelRequest();
            return;
        }

        if (StringComparer.Ordinal.Equals(args.Key, "Enter") && !args.ShiftKey)
        {
            if (_editContext.Validate())
            {
                await SendAsync();
            }
            else
            {
                HandleInvalidSubmit();
            }
        }
    }

    private async Task SendAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _message = null;
        _conversationNotice = null;
        DemoBackendScope? effectiveScope = GetEffectiveRepositoriesForRequest();
        if (effectiveScope is null)
        {
            _loadState = LoadState.Error;
            _message = "演示所需的固定仓库范围不可用。";
            return;
        }

        var request = new PreparedChatQuestionRequest
        {
            Mode = _formRequest.Mode,
            SystemId = effectiveScope.SystemId,
            RepositoryIds = effectiveScope.RepositoryIds,
            Question = _formRequest.Question,
        };
        string question = request.NormalizedQuestion;
        ChatTurn turn = ChatTurn.CreatePending(question, _selectedMode!.Value);

        var cancellation = new CancellationTokenSource();
        int conversationVersion = _conversationVersion;
        _requestCancellation = cancellation;
        _isBusy = true;
        _loadState = LoadState.Loading;
        _turns.Add(turn);
        _question = string.Empty;
        _formRequest.Question = string.Empty;
        await RenderConversationAtEndAsync();

        try
        {
            PreparedChatResult result = await ChatCoordinator.AskAsync(request, cancellation.Token);
            if (conversationVersion != _conversationVersion)
            {
                return;
            }

            if (result.Kind == PreparedChatResultKind.Succeeded && result.Answer is not null)
            {
                await RevealAnswerAsync(
                    turn,
                    result.Answer,
                    result.RemainingDeadlineMilliseconds,
                    conversationVersion,
                    cancellation.Token);
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
                    result.Kind == PreparedChatResultKind.Cancelled ? ChatTurnState.Cancelled : ChatTurnState.Error,
                    result.Message ?? _unexpectedFailureMessage);
                _loadState = result.Kind == PreparedChatResultKind.Cancelled ?
                    ReadyState :
                    LoadState.Error;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (conversationVersion != _conversationVersion)
            {
                return;
            }

            turn.Finish(ChatTurnState.Cancelled, "已取消本次提问。");
            _loadState = ReadyState;
        }
        catch (Exception)
        {
            if (conversationVersion != _conversationVersion)
            {
                return;
            }

            turn.Finish(ChatTurnState.Error, _unexpectedFailureMessage);
            _loadState = LoadState.Error;
        }
        finally
        {
            if (conversationVersion == _conversationVersion)
            {
                _isBusy = false;
                _scrollConversationAfterRender = true;
            }

            if (ReferenceEquals(_requestCancellation, cancellation))
            {
                _requestCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void HandleInvalidSubmit()
    {
        _loadState = LoadState.Error;
        _message = _validationMessage;
    }

    private void CancelRequest()
    {
        if (!_isBusy || _requestCancellation is null)
        {
            return;
        }

        _loadState = LoadState.Cancelling;
        _message = null;
        _revealCancellation?.Cancel();
        _requestCancellation.Cancel();
    }

    private void ResetTransientInteraction(bool clearQuestion)
    {
        _message = null;
        _conversationNotice = null;
        _loadState = ReadyState;
        if (clearQuestion)
        {
            _question = string.Empty;
            _formRequest.Question = string.Empty;
        }
    }

    private LoadState ReadyState => _scopes.Count == 0 ? LoadState.Empty : LoadState.Loaded;

    private DemoBackendScope? GetEffectiveRepositoriesForRequest()
    {
        foreach (PreparedChatScopeOption scope in _scopes)
        {
            var repositoryIds = new List<Guid>(DemoBackendRepositoryNames.Count);
            foreach (string repositoryName in DemoBackendRepositoryNames)
            {
                PreparedChatRepositoryOption? repository = scope.Repositories.SingleOrDefault(
                    option => StringComparer.Ordinal.Equals(option.DisplayName, repositoryName));
                if (repository is null)
                {
                    repositoryIds.Clear();
                    break;
                }

                repositoryIds.Add(repository.RepositoryId);
            }

            if (repositoryIds.Count == DemoBackendRepositoryNames.Count)
            {
                return new DemoBackendScope(scope.SystemId, repositoryIds);
            }
        }

        return null;
    }

    private async Task RevealAnswerAsync(
        ChatTurn turn,
        GroundedAnswer answer,
        long remainingDeadlineMilliseconds,
        int conversationVersion,
        CancellationToken requestCancellation)
    {
        turn.BeginReveal(answer);
        bool reducedMotion = await PrefersReducedMotionAsync(requestCancellation);
        long requiredDelayCount = turn.Claims.Sum(
            claim => Math.Max(0, claim.TextElementCount - 1));
        double requiredRevealMilliseconds =
            requiredDelayCount * RevealDelay.Interval.TotalMilliseconds;
        if (reducedMotion ||
            remainingDeadlineMilliseconds < _minimumProgressiveRevealBudgetMilliseconds ||
            requiredRevealMilliseconds > remainingDeadlineMilliseconds)
        {
            turn.CompleteReveal();
            await RenderConversationAtEndAsync();
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
                    await RenderConversationAtEndAsync();
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
                    await RenderConversationAtEndAsync();
                }
            }

            turn.CompleteReveal();
            await RenderConversationAtEndAsync();
        }
        finally
        {
            if (ReferenceEquals(_revealCancellation, linked))
            {
                _revealCancellation = null;
            }
        }
    }

    private async ValueTask<bool> PrefersReducedMotionAsync(CancellationToken cancellationToken)
    {
        try
        {
            IJSObjectReference module = await GetChatModuleAsync(cancellationToken);
            return await module.InvokeAsync<bool>("prefersReducedMotion", cancellationToken);
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException)
        {
            return false;
        }
    }

    private async Task ScrollConversationToEndAsync()
    {
        try
        {
            IJSObjectReference module = await GetChatModuleAsync(CancellationToken.None);
            await module.InvokeVoidAsync("scrollConversationToEnd", _conversationElement);
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException)
        {
        }
    }

    private async Task RenderConversationAtEndAsync()
    {
        _scrollConversationAfterRender = true;
        await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_scrollConversationAfterRender)
        {
            return;
        }

        _scrollConversationAfterRender = false;
        await ScrollConversationToEndAsync();
    }

    private async ValueTask<IJSObjectReference> GetChatModuleAsync(CancellationToken cancellationToken)
    {
        _chatModule ??= await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            cancellationToken,
            "./Components/Pages/ChatPage.razor.js");
        return _chatModule;
    }

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? reveal = Interlocked.Exchange(ref _revealCancellation, null);
        reveal?.Cancel();
        reveal?.Dispose();

        CancellationTokenSource? request = Interlocked.Exchange(ref _requestCancellation, null);
        request?.Cancel();
        request?.Dispose();

        if (_chatModule is not null)
        {
            try
            {
                await _chatModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    private sealed record DemoRepositorySystem(
        string Id,
        string Name,
        IReadOnlyList<string> Repositories);

    private sealed record DemoBackendScope(
        Guid SystemId,
        IReadOnlyCollection<Guid> RepositoryIds);
}
