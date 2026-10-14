using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared;
using Vulgata.Shared.Explore;

namespace Vulgata.Web.ViewModels.Explore;

public sealed class DocumentBrowseViewModel : IDisposable
{
    private readonly IExplorePresentationService _service;
    private readonly object _gate = new();
    private CancellationTokenSource? _pending;
    private long _generation;
    private bool _disposed;

    public DocumentBrowseViewModel(IExplorePresentationService service)
    {
        _service = service;
        _service.ContextChanged += Clear;
    }

    public int TotalItems { get; private set; }
    public int TotalPages => (TotalItems + PageSize - 1) / PageSize;
    public int PageSize { get; private set; } = KnowledgeContract.DefaultPageSize;
    public int PageNumber { get; private set; } = 1;
    public string TitleQuery { get; private set; } = "";
    public bool CanPrevious => !_disposed && _pending is null && PageNumber > 1;
    public SearchPage? Page { get; private set; }
    public ExploreBrowseSession? Session { get; private set; }
    public LoadState LoadState { get; private set; }
    public string StatusText { get; private set; } = "";
    public KnowledgeMode? Mode { get; private set; }
    public bool IsControlledFixture { get; private set; }
    public bool CanContinue => !_disposed && _pending is null && PageNumber < TotalPages;
    public event Action? Changed;

    public Task LoadAsync(int? pageSize = null)
    {
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;
            if (pageSize is > 0) PageSize = Math.Clamp(pageSize.Value, 1, 200);
            return ReadAsync(1);
        }
    }

    public Task NextAsync()
    {
        lock (_gate) { return CanContinue ? ReadAsync(PageNumber + 1) : Task.CompletedTask; }
    }

    public Task RetryAsync() => LoadAsync();

    public Task SearchAsync(string query)
    {
        lock (_gate)
        {
            TitleQuery = query.Trim();
            return _disposed ? Task.CompletedTask : ReadAsync(1);
        }
    }

    public Task PreviousAsync()
    {
        lock (_gate) { return CanPrevious ? ReadAsync(PageNumber - 1) : Task.CompletedTask; }
    }

    public Task GoToPageAsync(int page)
    {
        lock (_gate) { return !_disposed && _pending is null && page >= 1 && page <= TotalPages ? ReadAsync(page) : Task.CompletedTask; }
    }

    private async Task ReadAsync(int targetPage)
    {
        CancelPending();
        var generation = ++_generation;
        var pending = new CancellationTokenSource();
        _pending = pending;
        ResetContent();
        LoadState = LoadState.Loading;
        StatusText = "正在加载文档列表…";
        Changed?.Invoke();
        try
        {
            var query = TitleQuery;
            var result = await _service.BrowseAsync(null, pending.Token, query);
            var documents = ImmutableArray.CreateBuilder<DocumentSummary>();
            var seen = new HashSet<DocumentHandle>();
            var gaps = new HashSet<string>(StringComparer.Ordinal);
            var partial = false;
            while (result.Page.Status == KnowledgeReadStatus.Success)
            {
                foreach (var document in result.Page.Documents)
                    if (seen.Add(document.Handle)) documents.Add(document);
                if (result.Page.Coverage is { } coverage)
                {
                    partial |= coverage.IsPartial;
                    gaps.UnionWith(coverage.PermittedGaps);
                }
                if (result.Session is null) break;
                result = await _service.BrowseAsync(result.Session, pending.Token, query);
            }
            lock (_gate)
            {
                if (_disposed || generation != _generation || pending.IsCancellationRequested) { return; }
                if (result.Page.Status == KnowledgeReadStatus.Success)
                {
                    TotalItems = documents.Count;
                    PageNumber = Math.Clamp(targetPage, 1, Math.Max(1, TotalPages));
                    Page = result.Page with
                    {
                        Documents = documents.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToImmutableArray(),
                        Next = null,
                        Coverage = new(partial, gaps.ToImmutableArray())
                    };
                    Session = result.Session;
                    Mode = result.Mode;
                    IsControlledFixture = result.IsControlledFixture;
                    LoadState = TotalItems == 0 ? LoadState.Empty : LoadState.Loaded;
                    StatusText = TotalItems == 0 ? "没有匹配的文档。" : $"共 {TotalItems} 个文档，每页 {PageSize} 个。";
                    if (partial) StatusText += " 知识覆盖仍为部分。";
                }
                else { SetFailure(result.Page.Status); }
            }
        }
        catch (OperationCanceledException)
        {
            lock (_gate) { if (generation == _generation && !_disposed) { SetFailure(KnowledgeReadStatus.Cancelled); } }
        }
        catch (Exception)
        {
            lock (_gate) { if (generation == _generation && !_disposed) { SetFailure(KnowledgeReadStatus.UnavailableService); } }
        }
        finally
        {
            lock (_gate)
            {
                if (generation == _generation) { _pending = null; Changed?.Invoke(); }
                pending.Dispose();
            }
        }
    }

    private void SetFailure(KnowledgeReadStatus status)
    {
        ResetContent();
        LoadState = status == KnowledgeReadStatus.Cancelled ? LoadState.Idle : LoadState.Error;
        StatusText = status == KnowledgeReadStatus.Cancelled ? "已取消读取。" : "文档列表不可用，请重试。";
    }

    private void ResetContent()
    {
        TotalItems = 0;
        Page = null;
        Session = null;
        Mode = null;
        IsControlledFixture = false;
    }

    private void CancelPending()
    {
        _pending?.Cancel();
        _pending = null;
    }

    public void Cancel()
    {
        lock (_gate)
        {
            ++_generation;
            CancelPending();
            ResetContent();
            LoadState = LoadState.Idle;
            StatusText = "已取消读取。";
            Changed?.Invoke();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Cancel();
            StatusText = "读取上下文已更改，请重新加载列表。";
            Changed?.Invoke();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) { return; }
            _disposed = true;
            _service.ContextChanged -= Clear;
            Clear();
        }
    }
}
