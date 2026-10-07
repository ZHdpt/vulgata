using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared;

namespace Vulgata.Web.ViewModels.OctoberChat;

public sealed class OctoberChatEntryViewModel(IOctoberChatSelectionService selectionService)
{
    private long _operationVersion;
    private KnowledgeMode _requestedMode = KnowledgeMode.Business;
    private ReadSelectionRequest? _openingSelection;

    public OctoberChatSelectionResult? Result { get; private set; }
    public OctoberChatConversation? Conversation => Result?.Conversation;
    public SearchPage? Catalog => Result?.Catalog;
    public LoadState LoadState { get; private set; } = LoadState.Idle;
    public OctoberChatWorkState WorkState { get; private set; } = OctoberChatWorkState.Settled;
    public bool RequiresConfirmation { get; private set; }
    public string Question { get; set; } = string.Empty;
    public string ThemeName => _requestedMode == KnowledgeMode.IT ? "dark" : "light";
    public string ModeLabel => _requestedMode == KnowledgeMode.IT ? "技术模式" : "业务模式";
    public string? StatusMessage { get; private set; }

    public Task InitializeAsync(ReadSelectionRequest? trustedSelection,
        CancellationToken cancellationToken = default)
    {
        if (Conversation is not null)
        {
            return RequestContextChangeAsync(trustedSelection, confirmed: false, cancellationToken);
        }

        return WorkState != OctoberChatWorkState.Settled
            ? Task.CompletedTask
            : OpenFreshAsync(trustedSelection, cancellationToken);
    }

    public Task RequestContextChangeAsync(ReadSelectionRequest? trustedSelection, bool confirmed,
        CancellationToken cancellationToken = default)
    {
        ReadSelectionRequest? current = Conversation?.Request ?? _openingSelection;
        // Changed authentication is never a reason to retain the previous user's catalog.
        // The replacement request is still host-issued and requires a fresh owner read.
        if (current is not null && current.Subject != trustedSelection?.Subject)
        {
            if (WorkState != OctoberChatWorkState.Settled)
            {
                ++_operationVersion;
                Result = null;
                _openingSelection = null;
                Question = string.Empty;
                RequiresConfirmation = false;
                LoadState = LoadState.Error;
                StatusMessage = "身份已变更，已隐藏当前对话；原对话状态尚待确认。";
                return Task.CompletedTask;
            }

            return OpenFreshAsync(trustedSelection, cancellationToken);
        }

        // No conversation has been admitted during initial selection. A newer host input
        // supersedes that pending read; its predecessor must not repopulate the page later.
        if (Conversation is null && LoadState == LoadState.Loading &&
            WorkState == OctoberChatWorkState.Settled)
        {
            return OpenFreshAsync(trustedSelection, cancellationToken);
        }

        if (!CanTransition())
        {
            return Task.CompletedTask;
        }

        if (SameContext(current, trustedSelection))
        {
            RequiresConfirmation = false;
            return Task.CompletedTask;
        }

        if (current is not null && !confirmed)
        {
            RequiresConfirmation = true;
            StatusMessage = "更改模式或仓库范围将开始空白对话，请确认。";
            return Task.CompletedTask;
        }

        return OpenFreshAsync(trustedSelection, cancellationToken);
    }

    public Task StartNewConversationAsync(bool confirmed,
        CancellationToken cancellationToken = default)
    {
        if (!CanTransition())
        {
            return Task.CompletedTask;
        }

        if (!confirmed)
        {
            RequiresConfirmation = true;
            StatusMessage = "开始新对话将清空当前草稿，请确认。";
            return Task.CompletedTask;
        }

        return OpenFreshAsync(Conversation?.Request, cancellationToken);
    }

    public void SetWorkState(OctoberChatWorkState state)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        WorkState = state;
    }

    private bool CanTransition()
    {
        if (WorkState != OctoberChatWorkState.Settled || LoadState == LoadState.Loading)
        {
            RequiresConfirmation = false;
            StatusMessage = "当前对话尚未结束或保存状态待确认，暂时无法切换。";
            return false;
        }

        return true;
    }

    private async Task OpenFreshAsync(ReadSelectionRequest? trustedSelection, CancellationToken cancellationToken)
    {
        long operation = ++_operationVersion;
        _openingSelection = trustedSelection;
        Result = null;
        Question = string.Empty;
        RequiresConfirmation = false;
        StatusMessage = null;
        _requestedMode = trustedSelection?.Mode ?? KnowledgeMode.Business;
        LoadState = LoadState.Loading;
        OctoberChatSelectionResult result;
        try
        {
            result = await selectionService.OpenAsync(trustedSelection, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (operation == _operationVersion)
            {
                _openingSelection = null;
                LoadState = LoadState.Error;
                StatusMessage = "已取消读取对话范围。";
            }

            throw;
        }

        if (operation != _operationVersion)
        {
            return;
        }

        Result = result;
        _openingSelection = null;
        LoadState = result.Status switch
        {
            OctoberChatEntryStatus.Ready => LoadState.Loaded,
            OctoberChatEntryStatus.Empty => LoadState.Empty,
            _ => LoadState.Error,
        };
        StatusMessage = result.Status switch
        {
            OctoberChatEntryStatus.Ready => null,
            OctoberChatEntryStatus.Empty => "所选范围暂无可用于当前模式的已发布知识。",
            OctoberChatEntryStatus.InvalidSelection => "对话范围无效，请重新选择。",
            _ => "所选知识暂时不可用，请稍后重试。",
        };
    }

    private static bool SameContext(ReadSelectionRequest? first, ReadSelectionRequest? second) =>
        first is not null && second is not null && first.Publication == second.Publication &&
        first.Subject == second.Subject && first.Mode == second.Mode && first.Purpose == second.Purpose &&
        !first.RepositoryScope.IsDefault && !second.RepositoryScope.IsDefault &&
        first.RepositoryScope.Order().SequenceEqual(second.RepositoryScope.Order());
}
