using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.FluentUI.AspNetCore.Components;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Shared.Chat;
using Vulgata.Web.Data;

namespace Vulgata.Web.Components.Pages;

public partial class ChatPage : IDisposable
{
    private const string _validationMessage = "请选择业务模式、系统和两个不同的仓库，并输入 1 至 2000 个字符的问题。";
    private const string _unexpectedFailureMessage = "暂时无法生成回答，请稍后重试。";

    private IReadOnlyList<PreparedChatScopeOption> _scopes = [];
    private readonly PreparedChatQuestionRequest _formRequest = new();
    private EditContext _editContext = null!;
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
        _editContext = new EditContext(_formRequest);
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
        _formRequest.SystemId = _selectedSystemId.GetValueOrDefault();
        _formRequest.RepositoryIds = [];
        ResetTransientInteraction(clearQuestion: false);
    }

    private void OnFirstRepositoryChanged(ChangeEventArgs args)
    {
        _firstRepositoryId = ParseId(args.Value);
        SyncRepositoryIds();
        ResetTransientInteraction(clearQuestion: false);
    }

    private void OnSecondRepositoryChanged(ChangeEventArgs args)
    {
        _secondRepositoryId = ParseId(args.Value);
        SyncRepositoryIds();
        ResetTransientInteraction(clearQuestion: false);
    }

    private void SelectMode(ChatMode mode)
    {
        _selectedMode = mode;
        _formRequest.Mode = mode == ChatMode.Business
            ? PreparedChatMode.Business
            : PreparedChatMode.It;
        ResetTransientInteraction(clearQuestion: true);
    }

    private bool IsModeSelected(ChatMode mode) => _selectedMode == mode;

    private string ModeAriaPressed(ChatMode mode) => IsModeSelected(mode) ? "true" : "false";

    private Appearance ModeAppearance(ChatMode mode) =>
        IsModeSelected(mode) ? Appearance.Accent : Appearance.Neutral;

    private void OnQuestionInput(ChangeEventArgs args)
    {
        _question = args.Value?.ToString() ?? string.Empty;
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

        _answer = null;
        _submittedQuestion = null;
        _message = null;
        var request = new PreparedChatQuestionRequest
        {
            Mode = _formRequest.Mode,
            SystemId = _formRequest.SystemId,
            RepositoryIds = _formRequest.RepositoryIds.ToArray(),
            Question = _formRequest.Question,
        };
        string question = request.NormalizedQuestion;

        var cancellation = new CancellationTokenSource();
        _requestCancellation = cancellation;
        _isBusy = true;
        _loadState = LoadState.Loading;
        _submittedQuestion = question;
        _question = string.Empty;
        _formRequest.Question = string.Empty;
        await InvokeAsync(StateHasChanged);

        try
        {
            PreparedChatResult result = await ChatCoordinator.AskAsync(request, cancellation.Token);
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

    private void HandleInvalidSubmit()
    {
        _answer = null;
        _submittedQuestion = null;
        _loadState = LoadState.Error;
        _message = _validationMessage;
    }

    private void SyncRepositoryIds() =>
        _formRequest.RepositoryIds = new[] { _firstRepositoryId, _secondRepositoryId }
            .Where(repositoryId => repositoryId.HasValue)
            .Select(repositoryId => repositoryId!.Value)
            .ToArray();

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
            _formRequest.Question = string.Empty;
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
