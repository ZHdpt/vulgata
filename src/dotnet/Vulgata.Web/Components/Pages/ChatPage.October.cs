using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Components.Routing;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Web.ViewModels.OctoberChat;
using Vulgata.Web.Services.OctoberChat;

namespace Vulgata.Web.Components.Pages;

public partial class ChatPage
{
    /// <summary>Trusted host composition; absent application keeps the October composer unavailable.</summary>
    [Parameter]
    public OctoberChatApplication? OctoberApplication { get; set; }

    private bool _octoberApplicationSelected;
    private OctoberChatApplication? _boundOctoberApplication;
    private Guid? _openedOctoberConversation;
    private bool _octoberPageDisposed;
    private bool _octoberLocatorSupplied;
    private bool _octoberLocatorInvalid;
    private Guid _octoberLocatorConversation;
    private Guid _octoberLocatorContext;
    private bool _openingOctober;
    private string? _lastOctoberQuery;
    private string? _ownOctoberNavigation;
    private long _octoberLocatorVersion;
    private bool _pendingOctoberLocator;

    [Inject] private IServiceProvider OctoberServices { get; set; } = null!;
    [Inject] private NavigationManager OctoberNavigation { get; set; } = null!;

    private bool OctoberComposerDisabled => OctoberEntry?.Conversation is null ||
        _boundOctoberApplication?.IsReady != true;

    private string OctoberComposerPlaceholder
    {
        get
        {
            if (!OctoberComposerDisabled)
                return _boundOctoberEntry?.ThemeName == "dark" ? "输入技术问题…" : "描述你想了解的业务流程…";
            if (_octoberLocatorInvalid) return "对话链接暂不可用";
            if (_openingOctober || _boundOctoberEntry?.LoadState == Vulgata.Shared.LoadState.Loading)
                return "正在准备对话…";
            if (_boundOctoberApplication?.IsSaveUncertain == true)
                return _boundOctoberApplication.StatusMessage ?? "对话状态待确认。";
            if (_boundOctoberApplication?.IsBusy == true) return "正在处理当前问题…";
            return "对话服务暂不可用";
        }
    }

    private async Task ResolveOctoberCompositionAsync()
    {
        ReadOctoberLocator();
        _lastOctoberQuery = new Uri(OctoberNavigation.Uri).Query;
        OctoberNavigation.LocationChanged += OctoberLocationChanged;
        if (OctoberApplication is not null) return;
        OctoberApplication = OctoberServices.GetService<OctoberChatApplication>();
        if (OctoberApplication is null) return;
        OctoberEntry ??= OctoberServices.GetService<OctoberChatEntryViewModel>();
        if (OctoberSelection is null && OctoberServices.GetService<IOctoberChatContextProvider>() is { } context)
        {
            try { OctoberSelection = await context.GetCurrentAsync(); }
            catch (Exception) { OctoberSelection = null; }
        }
    }

    private async Task BindOctoberApplicationAsync()
    {
        if (!ReferenceEquals(_boundOctoberApplication, OctoberApplication))
        {
            if (_boundOctoberApplication is not null)
            {
                _boundOctoberApplication.StateChanged -= OctoberApplicationChanged;
                _boundOctoberApplication.Detach();
            }
            _boundOctoberApplication = OctoberApplication;
            _openedOctoberConversation = null;
            if (_boundOctoberApplication is not null)
            {
                _boundOctoberApplication.Attach(DispatchOctoberAsync);
                _boundOctoberApplication.StateChanged += OctoberApplicationChanged;
            }
        }
        if (_boundOctoberApplication is not { } application) return;
        if (_octoberLocatorInvalid)
        {
            application.Detach();
            return;
        }
        if (OctoberEntry?.Conversation is not { } conversation)
        {
            application.Detach();
            _openedOctoberConversation = null;
            return;
        }
        if (_openedOctoberConversation != conversation.ConversationId && !application.IsBusy)
        {
            long locatorVersion = _octoberLocatorVersion;
            _pendingOctoberLocator = false;
            _openedOctoberConversation = conversation.ConversationId;
            _openingOctober = true;
            try
            {
                application.Attach(DispatchOctoberAsync);
                bool opened = _octoberLocatorSupplied
                    ? await application.OpenExistingAsync(conversation with
                    {
                        ConversationId = _octoberLocatorConversation,
                        ContextId = _octoberLocatorContext
                    })
                    : await application.OpenAsync(conversation);
                if (locatorVersion != _octoberLocatorVersion)
                {
                    _openedOctoberConversation = null;
                    application.Detach();
                }
                else if (opened && application.Conversation is { } durable) PublishOctoberLocator(durable);
            }
            finally
            {
                _openingOctober = false;
                await ConsumePendingOctoberLocatorAsync();
            }
        }
        UpdateOctoberWorkState();
    }

    private Task DispatchOctoberAsync(Func<ValueTask> callback) => InvokeAsync(async () =>
    {
        if (!_octoberPageDisposed) await callback();
    });

    private void OctoberApplicationChanged()
    {
        if (_octoberPageDisposed) return;
        _ = InvokeAsync(async () =>
        {
            if (_octoberPageDisposed) return;
            if (!_openingOctober && _openedOctoberConversation is not null &&
                _boundOctoberApplication?.Conversation is null && !_octoberLocatorInvalid)
                ClearOctoberLocator();
            await ConsumePendingOctoberLocatorAsync();
            UpdateOctoberWorkState();
            StateHasChanged();
        });
    }

    private void UpdateOctoberWorkState() => OctoberEntry?.SetWorkState(
        _boundOctoberApplication?.IsSaveUncertain == true ? OctoberChatWorkState.SaveIndeterminate :
        _boundOctoberApplication?.IsBusy == true ? OctoberChatWorkState.Active : OctoberChatWorkState.Settled);

    private async Task SendOctoberAsync()
    {
        if (OctoberComposerDisabled || OctoberEntry is null || _boundOctoberApplication is null) return;
        string question = OctoberEntry.Question;
        OctoberEntry.Question = string.Empty;
        await _boundOctoberApplication.SubmitAsync(question);
        UpdateOctoberWorkState();
    }

    private void CancelOctober() => _boundOctoberApplication?.Cancel();

    private async Task ReconcileOctoberAsync()
    {
        if (_boundOctoberApplication is null) return;
        await _boundOctoberApplication.ReconcileAsync();
        UpdateOctoberWorkState();
    }

    private void DetachOctoberApplication()
    {
        _octoberPageDisposed = true;
        _pendingOctoberLocator = false;
        OctoberNavigation.LocationChanged -= OctoberLocationChanged;
        if (_boundOctoberApplication is null) return;
        _boundOctoberApplication.StateChanged -= OctoberApplicationChanged;
        _boundOctoberApplication.Detach();
    }

    private void ReadOctoberLocator()
    {
        _octoberLocatorInvalid = false;
        _octoberLocatorConversation = Guid.Empty;
        _octoberLocatorContext = Guid.Empty;
        var query = QueryHelpers.ParseQuery(new Uri(OctoberNavigation.Uri).Query);
        _octoberLocatorSupplied = query.ContainsKey("conversationId") || query.ContainsKey("contextId");
        if (!_octoberLocatorSupplied) return;
        _octoberLocatorInvalid = !query.TryGetValue("conversationId", out var conversation) || conversation.Count != 1 ||
            !query.TryGetValue("contextId", out var context) || context.Count != 1 ||
            !Guid.TryParse(conversation.ToString(), out _octoberLocatorConversation) || _octoberLocatorConversation == Guid.Empty ||
            !Guid.TryParse(context.ToString(), out _octoberLocatorContext) || _octoberLocatorContext == Guid.Empty;
    }

    private void PublishOctoberLocator(OctoberChatConversation conversation)
    {
        _octoberLocatorSupplied = true;
        _octoberLocatorConversation = conversation.ConversationId;
        _octoberLocatorContext = conversation.ContextId;
        string uri = OctoberNavigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["conversationId"] = conversation.ConversationId.ToString("D"),
            ["contextId"] = conversation.ContextId.ToString("D")
        });
        NavigateOctoberAsyncState(uri);
    }

    private void ClearOctoberLocator()
    {
        _pendingOctoberLocator = false;
        _octoberLocatorSupplied = false;
        _octoberLocatorInvalid = false;
        _octoberLocatorConversation = Guid.Empty;
        _octoberLocatorContext = Guid.Empty;
        _openedOctoberConversation = null;
        string uri = OctoberNavigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["conversationId"] = null,
            ["contextId"] = null
        });
        NavigateOctoberAsyncState(uri);
    }

    private void NavigateOctoberAsyncState(string uri)
    {
        _lastOctoberQuery = new Uri(uri).Query;
        if (uri == OctoberNavigation.Uri) return;
        _ownOctoberNavigation = uri;
        OctoberNavigation.NavigateTo(uri, new NavigationOptions { ReplaceHistoryEntry = true });
    }

    private void OctoberLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (_octoberPageDisposed) return;
        string query = new Uri(args.Location).Query;
        if (_ownOctoberNavigation == args.Location)
        {
            _ownOctoberNavigation = null;
            _lastOctoberQuery = query;
            return;
        }
        if (_lastOctoberQuery == query) return;
        _lastOctoberQuery = query;
        bool hadLocator = _octoberLocatorSupplied;
        ReadOctoberLocator();
        // Back/forward removal is not an instruction to create another dialogue.
        if (hadLocator && !_octoberLocatorSupplied) _octoberLocatorInvalid = true;
        _octoberApplicationSelected |= _octoberLocatorSupplied || _octoberLocatorInvalid;
        long version = ++_octoberLocatorVersion;
        _pendingOctoberLocator = true;
        _openedOctoberConversation = null;
        _boundOctoberApplication?.Detach();
        _ = InvokeAsync(async () =>
        {
            try
            {
                if (!_octoberPageDisposed && version == _octoberLocatorVersion) await BindOctoberApplicationAsync();
            }
            finally
            {
                // Only the actual Bind/Open owns _openingOctober. A navigation
                // callback must not release an older open's continuation barrier.
                await ConsumePendingOctoberLocatorAsync();
                if (!_octoberPageDisposed) { UpdateOctoberWorkState(); StateHasChanged(); }
            }
        });
    }

    private async Task ConsumePendingOctoberLocatorAsync()
    {
        if (!_pendingOctoberLocator || _octoberPageDisposed || _openingOctober) return;
        if (_octoberLocatorInvalid || !_octoberLocatorSupplied)
        {
            _pendingOctoberLocator = false;
            return;
        }
        // IsBusy remains true through actual owner and physical cleanup. This is
        // driven by settlement notifications, never a poll or a generation retry.
        if (_boundOctoberApplication is not { IsBusy: false } || OctoberEntry?.Conversation is null) return;
        _pendingOctoberLocator = false;
        await BindOctoberApplicationAsync();
    }
}
