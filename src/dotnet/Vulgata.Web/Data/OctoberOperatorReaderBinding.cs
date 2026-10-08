using System.Text.Json.Serialization;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Web.Data;

/// <summary>Host-only original-session reader lifetime; never a serializable permission or session replacement.</summary>
public sealed class OctoberOperatorReaderBinding : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly IAsyncDisposable _resources;
    private readonly HashSet<Task> _calls = [];
    private Task? _disposal;
    private Task<ReadSelectionResult>? _opening;
    private ReadSelectionResult? _issued;
    private bool _closed;

    internal OctoberOperatorReaderBinding(KnowledgeSubject subject, ReadSelectionRequest selection,
        IAsyncDisposable resources, Func<Func<bool>, IOctoberKnowledgeReader> createReader)
    {
        Subject = subject;
        Selection = selection;
        _resources = resources;
        Reader = new RetainedReader(this, createReader(() => IsOpen));
    }

    [JsonIgnore] public KnowledgeSubject Subject { get; }
    [JsonIgnore] public ReadSelectionRequest Selection { get; }
    [JsonIgnore] public IOctoberKnowledgeReader Reader { get; }
    private bool IsOpen { get { lock (_gate) return !_closed; } }
    public override string ToString() => nameof(OctoberOperatorReaderBinding);

    private bool Owns(ReadSelection? selection)
    {
        lock (_gate) return !_closed && selection is not null && _issued?.Selection is { } issued &&
            string.Equals(selection.Token, issued.Token, StringComparison.Ordinal);
    }

    private Task<ReadSelectionResult> OpenAsync(IOctoberKnowledgeReader reader, ReadSelectionRequest request, CancellationToken token)
    {
        lock (_gate)
        {
            if (_closed) return Task.FromResult(new ReadSelectionResult(KnowledgeReadStatus.Unavailable, null));
            if (token.IsCancellationRequested) return Task.FromCanceled<ReadSelectionResult>(token);
            // One successful opaque ticket per binding. Reuse never renews its original expiry;
            // an expired ticket needs a new binding. Every content read still checks live authority.
            if (_issued is not null) return Task.FromResult(_issued);
            if (_opening is not null) return _opening.WaitAsync(token);
            var completion = new TaskCompletionSource<ReadSelectionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _opening = completion.Task;
            _calls.Add(completion.Task);
            _ = CompleteOpenAsync(completion);
            return completion.Task;
        }

        async Task CompleteOpenAsync(TaskCompletionSource<ReadSelectionResult> completion)
        {
            try
            {
                var result = await reader.OpenReadSelectionAsync(request, token);
                lock (_gate)
                {
                    if (_closed) result = new(KnowledgeReadStatus.Unavailable, null);
                    if (result is { Status: KnowledgeReadStatus.Success, Selection: { Token.Length: > 0 } })
                        _issued = result;
                    _opening = null;
                    completion.TrySetResult(result);
                }
            }
            catch (OperationCanceledException error)
            { lock (_gate) { _opening = null; completion.TrySetCanceled(error.CancellationToken); } }
            catch (Exception error)
            { lock (_gate) { _opening = null; completion.TrySetException(error); } }
            finally
            {
                lock (_gate)
                {
                    _calls.Remove(completion.Task);
                }
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposal is not null) return new(_disposal);
            _closed = true;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposal = completion.Task;
            _ = DisposeResourcesAsync(_calls.ToArray(), completion);
            return new(_disposal);
        }
    }

    private async Task DisposeResourcesAsync(Task[] calls, TaskCompletionSource completion)
    {
        await Task.Yield();
        try
        {
            try { await Task.WhenAll(calls); }
            catch (Exception) { /* Each caller retains its own read failure. */ }
            await _resources.DisposeAsync();
            completion.TrySetResult();
        }
        catch (Exception error) { completion.TrySetException(error); }
    }

    private Task<T> InvokeAsync<T>(Func<Task<T>> action, T unavailable)
    {
        TaskCompletionSource<T> completion;
        lock (_gate)
        {
            if (_closed) return Task.FromResult(unavailable);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _calls.Add(completion.Task);
        }
        _ = CompleteAsync();
        return completion.Task;

        async Task CompleteAsync()
        {
            try { completion.TrySetResult(await action()); }
            catch (OperationCanceledException error) { completion.TrySetCanceled(error.CancellationToken); }
            catch (Exception error) { completion.TrySetException(error); }
            finally { lock (_gate) _calls.Remove(completion.Task); }
        }
    }

    private sealed class RetainedReader(OctoberOperatorReaderBinding owner, IOctoberKnowledgeReader reader)
        : IOctoberKnowledgeReader
    {
        public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken = default) =>
            request is not null && request.Publication == owner.Selection.Publication && request.Subject == owner.Subject &&
                request.Mode == KnowledgeMode.IT && request.Purpose == KnowledgePurpose.Reader && !request.RepositoryScope.IsDefault &&
                request.RepositoryScope.SequenceEqual(owner.Selection.RepositoryScope)
                ? owner.OpenAsync(reader, request, cancellationToken)
                : Task.FromResult(new ReadSelectionResult(KnowledgeReadStatus.Unavailable, null));
        public Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default) =>
            owner.Owns(request?.Selection)
                ? owner.InvokeAsync(() => reader.SearchAsync(request!, cancellationToken), new SearchPage(KnowledgeReadStatus.Unavailable, [], null, null))
                : Task.FromResult(new SearchPage(KnowledgeReadStatus.Unavailable, [], null, null));
        public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken = default) =>
            owner.Owns(request?.Selection)
                ? owner.InvokeAsync(() => reader.ReadExactAsync(request!, cancellationToken), new ContentPage(KnowledgeReadStatus.Unavailable, null, null, [], null, null))
                : Task.FromResult(new ContentPage(KnowledgeReadStatus.Unavailable, null, null, [], null, null));
        public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken = default) =>
            owner.Owns(request?.Selection)
                ? owner.InvokeAsync(() => reader.GetRelatedAsync(request!, cancellationToken), new RelatedPage(KnowledgeReadStatus.Unavailable, [], [], null, null))
                : Task.FromResult(new RelatedPage(KnowledgeReadStatus.Unavailable, [], [], null, null));
    }
}
