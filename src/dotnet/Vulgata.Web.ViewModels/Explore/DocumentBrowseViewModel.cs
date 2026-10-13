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

    public int PageNumber { get; private set; } = 1;
    public string TitleQuery { get; private set; } = "";
    public bool CanPrevious => !_disposed && _pending is null && PageNumber > 1;
    public SearchPage? Page { get; private set; }
    public ExploreBrowseSession? Session { get; private set; }
    public LoadState LoadState { get; private set; }
    public string StatusText { get; private set; } = "";
    public KnowledgeMode? Mode { get; private set; }
    public bool IsControlledFixture { get; private set; }
    public bool CanContinue => !_disposed && _pending is null && Session is not null;
    public event Action? Changed;

    public Task LoadAsync()
    {
        lock (_gate) { return _disposed ? Task.CompletedTask : ReadAsync(null, 1); }
    }

    public Task NextAsync()
    {
        lock (_gate) { return CanContinue ? ReadAsync(Session, PageNumber + 1) : Task.CompletedTask; }
    }

    public Task RetryAsync() => LoadAsync();

    public Task SearchAsync(string query)
    {
        lock (_gate)
        {
            TitleQuery = query.Trim();
            return _disposed ? Task.CompletedTask : ReadAsync(null, 1);
        }
    }

    public Task PreviousAsync()
    {
        lock (_gate) { return CanPrevious ? ReadAsync(null, PageNumber - 1) : Task.CompletedTask; }
    }

    private async Task ReadAsync(ExploreBrowseSession? session, int targetPage)
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
            var result = await _service.BrowseAsync(session, pending.Token, TitleQuery);
            // Continuations are single-use. Re-read earlier pages with fresh authorization.
            if (session is null)
            {
                for (var number = 1; number < targetPage && result.Session is not null; number++)
                    result = await _service.BrowseAsync(result.Session, pending.Token, TitleQuery);
            }
            lock (_gate)
            {
                if (_disposed || generation != _generation || pending.IsCancellationRequested) { return; }
                if (result.Page.Status == KnowledgeReadStatus.Success)
                {
                    PageNumber = targetPage;
                    Page = result.Page;
                    Session = result.Session;
                    Mode = result.Mode;
                    IsControlledFixture = result.IsControlledFixture;
                    LoadState = result.Page.Documents.IsEmpty ? LoadState.Empty : LoadState.Loaded;
                    StatusText = result.Page.Documents.IsEmpty
                        ? (Mode == KnowledgeMode.Business ? "当前没有可读取的业务逻辑文档。" : "当前没有可读取的文档。")
                        : (result.Page.Next is null ? "已加载当前文档列表。" : "已加载部分文档，可查看下一页。");
                    if (result.Page.Coverage?.IsPartial == true) { StatusText += " 知识覆盖仍为部分。"; }
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
