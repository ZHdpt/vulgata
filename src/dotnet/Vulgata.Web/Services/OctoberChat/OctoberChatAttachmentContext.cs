using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Web.Explore;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>Originating circuit context, selected only after a host operation validates its exact binding.</summary>
internal sealed class OctoberChatAttachmentContext : IOctoberChatContextProvider, IDisposable
{
    private readonly object _gate = new();
    private readonly OctoberWikiSelection _selection;
    private readonly AuthenticationStateProvider _authentication;
    private readonly IOptions<IdentityOptions> _options;
    private readonly IOctoberKnowledgeDisclosurePolicy _policy;
    private OctoberChatContextProvider _current;
    private Action _changed;
    private bool _disposed;

    internal OctoberChatAttachmentContext(OctoberWikiSelection selection, AuthenticationStateProvider authentication,
        IOptions<IdentityOptions> options, IOctoberKnowledgeDisclosurePolicy policy)
    {
        _selection = selection;
        _authentication = authentication;
        _options = options;
        _policy = policy;
        _current = new(selection, authentication, options, policy);
        _changed = Forward(_current);
        _current.ContextChanged += _changed;
    }

    public event Action? ContextChanged;

    public async Task<ReadSelectionRequest?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OctoberChatContextProvider captured;
        lock (_gate) { if (_disposed) return null; captured = _current; }
        var result = await captured.GetCurrentAsync(cancellationToken);
        lock (_gate) return !_disposed && ReferenceEquals(captured, _current) ? result : null;
    }

    internal void BindOperation(ReadSelectionRequest request)
    {
        if (request.Publication != _selection.Publication || request.Subject is null
            || request.Purpose != KnowledgePurpose.Chat || request.Mode is not (KnowledgeMode.Business or KnowledgeMode.IT)
            || request.RepositoryScope.IsDefault || request.RepositoryScope.Length != _selection.RepositoryIds.Length
            || request.RepositoryScope.Distinct().Count() != request.RepositoryScope.Length
            || !request.RepositoryScope.Order().SequenceEqual(_selection.RepositoryIds.Order()))
            throw new InvalidOperationException("October Chat attachment selection is unavailable.");
        lock (_gate)
        {
            if (_disposed) throw new InvalidOperationException("October Chat attachment is closed.");
            _current.ContextChanged -= _changed;
            _current.Dispose();
            _current = new(_selection with { Mode = request.Mode }, _authentication, _options, _policy, request.Subject);
            _changed = Forward(_current);
            _current.ContextChanged += _changed;
        }
        // This is deliberate host selection, not an identity change. App Open has
        // cleared old display and requires prior execution settlement before this call.
    }

    private Action Forward(OctoberChatContextProvider source) => () =>
    {
        Action? changed;
        lock (_gate) changed = !_disposed && ReferenceEquals(source, _current) ? ContextChanged : null;
        changed?.Invoke();
    };

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _current.ContextChanged -= _changed;
            _current.Dispose();
        }
    }
}
