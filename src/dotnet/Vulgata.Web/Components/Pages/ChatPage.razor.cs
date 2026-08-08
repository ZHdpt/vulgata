using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.FluentUI.AspNetCore.Components;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Web.Data;

namespace Vulgata.Web.Components.Pages;

public partial class ChatPage : IDisposable
{
    private const string _validationMessage = "请选择业务模式、系统和两个不同的仓库，并输入 1 至 2000 个字符的问题。";
    private const string _unexpectedFailureMessage = "暂时无法生成回答，请稍后重试。";

    private IReadOnlyList<PreparedChatScopeOption> _scopes = [];
    private Guid? _selectedSystemId;
    private Guid? _firstRepositoryId;
    private Guid? _secondRepositoryId;
    private ChatMode? _selectedMode;
    private string _question = string.Empty;
    private string? _submittedQuestion;
    private string? _message;
    private GroundedAnswer? _answer;
    private CancellationTokenSource? _requestCancellation;
    private bool _isBusy;
    private LoadState _loadState = LoadState.Idle;

    private string PageModeClass => _selectedMode switch
    {
        ChatMode.Business => "chat-page--business",
        ChatMode.It => "chat-page--it",
        _ => "chat-page--unselected",
    };

    private string ThemeName => _selectedMode == ChatMode.It ? "dark" : "light";

    private string WelcomeMessage => _selectedMode == ChatMode.It ?
        "你好，需要追踪哪段逻辑？" :
        "你好，想了解什么业务流程？";

    private string QuestionPlaceholder => _selectedMode == ChatMode.It ?
        "输入技术问题…" :
        "描述你想了解的业务流程…";

    private string ActiveStatusMessage => _loadState == LoadState.Cancelling ?
        "正在取消本次提问…" :
        "正在查找相关文档…";

    private IReadOnlyList<PreparedChatRepositoryOption> SelectedRepositories =>
        _scopes.SingleOrDefault(scope => scope.SystemId == _selectedSystemId)?.Repositories ?? [];

    protected override async Task OnInitializedAsync()
    {
        _loadState = LoadState.Loading;
        try
        {
            _scopes = await ChatCoordinator.ListScopesAsync();
            _loadState = _scopes.Count == 0 ? LoadState.Empty : LoadState.Loaded;
        }
        catch (Exception)
        {
            _scopes = [];
            _loadState = LoadState.Error;
        }
    }

    private void OnSystemChanged(ChangeEventArgs args)
    {
        _selectedSystemId = ParseId(args.Value);
        _firstRepositoryId = null;
        _secondRepositoryId = null;
        ResetTransientInteraction(clearQuestion: false);
    }

    private void OnFirstRepositoryChanged(ChangeEventArgs args)
    {
        _firstRepositoryId = ParseId(args.Value);
        ResetTransientInteraction(clearQuestion: false);
    }

    private void OnSecondRepositoryChanged(ChangeEventArgs args)
    {
        _secondRepositoryId = ParseId(args.Value);
        ResetTransientInteraction(clearQuestion: false);
    }

    private void SelectMode(ChatMode mode)
    {
        _selectedMode = mode;
        ResetTransientInteraction(clearQuestion: true);
    }

    private bool IsModeSelected(ChatMode mode) => _selectedMode == mode;

    private string ModeAriaPressed(ChatMode mode) => IsModeSelected(mode) ? "true" : "false";

    private Appearance ModeAppearance(ChatMode mode) =>
        IsModeSelected(mode) ? Appearance.Accent : Appearance.Neutral;

    private void OnQuestionInput(ChangeEventArgs args)
    {
        _question = args.Value?.ToString() ?? string.Empty;
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
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _answer = null;
        _submittedQuestion = null;
        _message = null;
        if (!TryGetValidRequest(out ChatMode mode, out Guid systemId, out Guid[] repositoryIds, out string question))
        {
            _loadState = LoadState.Error;
            _message = _validationMessage;
            return;
        }

        var cancellation = new CancellationTokenSource();
        _requestCancellation = cancellation;
        _isBusy = true;
        _loadState = LoadState.Loading;
        _submittedQuestion = question;
        _question = string.Empty;
        await InvokeAsync(StateHasChanged);

        try
        {
            PreparedChatResult result = await ChatCoordinator.AskAsync(
                mode,
                systemId,
                repositoryIds,
                question,
                cancellation.Token);
            if (result.Kind == PreparedChatResultKind.Succeeded && result.Answer is not null)
            {
                _answer = result.Answer;
                _message = null;
                _loadState = LoadState.Loaded;
            }
            else
            {
                ClearPublishedInteraction();
                _message = result.Message ?? _unexpectedFailureMessage;
                _loadState = result.Kind == PreparedChatResultKind.Cancelled ?
                    ReadyState :
                    LoadState.Error;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            ClearPublishedInteraction();
            _message = "已取消本次提问。";
            _loadState = ReadyState;
        }
        catch (Exception)
        {
            ClearPublishedInteraction();
            _message = _unexpectedFailureMessage;
            _loadState = LoadState.Error;
        }
        finally
        {
            _isBusy = false;
            if (ReferenceEquals(_requestCancellation, cancellation))
            {
                _requestCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private bool TryGetValidRequest(
        out ChatMode mode,
        out Guid systemId,
        out Guid[] repositoryIds,
        out string question)
    {
        question = _question.Trim();
        mode = _selectedMode.GetValueOrDefault();
        systemId = _selectedSystemId.GetValueOrDefault();
        repositoryIds = [];
        if (_selectedMode is null ||
            systemId == Guid.Empty ||
            _firstRepositoryId is not Guid firstRepositoryId ||
            _secondRepositoryId is not Guid secondRepositoryId ||
            firstRepositoryId == secondRepositoryId ||
            question.Length is < 1 or > 2_000 ||
            !SelectedRepositories.Any(repository => repository.RepositoryId == firstRepositoryId) ||
            !SelectedRepositories.Any(repository => repository.RepositoryId == secondRepositoryId))
        {
            return false;
        }

        repositoryIds = [firstRepositoryId, secondRepositoryId];
        return true;
    }

    private void CancelRequest()
    {
        if (!_isBusy || _requestCancellation is null)
        {
            return;
        }

        _loadState = LoadState.Cancelling;
        _message = null;
        _requestCancellation.Cancel();
    }

    private void ResetTransientInteraction(bool clearQuestion)
    {
        ClearPublishedInteraction();
        _message = null;
        _loadState = ReadyState;
        if (clearQuestion)
        {
            _question = string.Empty;
        }
    }

    private void ClearPublishedInteraction()
    {
        _answer = null;
        _submittedQuestion = null;
    }

    private LoadState ReadyState => _scopes.Count == 0 ? LoadState.Empty : LoadState.Loaded;

    public void Dispose()
    {
        _requestCancellation?.Cancel();
        _requestCancellation?.Dispose();
    }

    private static Guid? ParseId(object? value) =>
        Guid.TryParse(value?.ToString(), out Guid id) && id != Guid.Empty ? id : null;
}
