using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared;
using Vulgata.Shared.Explore;

namespace Vulgata.Web.ViewModels.Explore;

public sealed class DocumentReaderViewModel : IDisposable
{
    private readonly IExplorePresentationService _service;
    private readonly object _gate = new();
    private CancellationTokenSource? _pending;
    private DocumentHandle? _target;
    private long _generation;
    private bool _disposed;

    public DocumentReaderViewModel(IExplorePresentationService service)
    {
        _service = service;
        _service.ContextChanged += Clear;
    }

    public ContentPage? Page { get; private set; }
    public ExploreReadSession? Session { get; private set; }
    public LoadState LoadState { get; private set; }
    public string StatusText { get; private set; } = "";
    public KnowledgeMode? Mode { get; private set; }
    public bool IsControlledFixture { get; private set; }
    public bool CanContinue => !_disposed && _pending is null && Session is not null;
    public event Action? Changed;

    public Task OpenAsync(DocumentHandle document)
    {
        lock (_gate)
        {
            if (_disposed) { return Task.CompletedTask; }
            _target = document;
            return LoadAsync(token => _service.OpenDocumentAsync(document, token));
        }
    }

    public Task NextAsync()
    {
        lock (_gate)
        {
            if (!CanContinue) { return Task.CompletedTask; }
            var session = Session!;
            return LoadAsync(token => _service.ContinueDocumentAsync(session, token));
        }
    }

    public Task RetryAsync()
    {
        lock (_gate)
        {
            return _target is null || _disposed ? Task.CompletedTask : OpenAsync(_target);
        }
    }

    private async Task LoadAsync(Func<CancellationToken, Task<ExploreReadResult>> readAsync)
    {
        CancelPending();
        var generation = ++_generation;
        var pending = new CancellationTokenSource();
        _pending = pending;
        ResetContent();
        LoadState = LoadState.Loading;
        StatusText = "正在加载文档…";
        Changed?.Invoke();
        try
        {
            var result = await readAsync(pending.Token);
            lock (_gate)
            {
                if (_disposed || generation != _generation || pending.IsCancellationRequested) { return; }
                if (result.Page.Status == KnowledgeReadStatus.Success)
                {
                    Page = result.Page;
                    Session = result.Session;
                    Mode = result.Mode;
                    IsControlledFixture = result.IsControlledFixture;
                    LoadState = LoadState.Loaded;
                    StatusText = result.Page.Next is not null
                        ? (_target?.SectionId is null ? "已加载部分内容" : "本节尚未加载完，已加载部分内容")
                        : (_target?.SectionId is null ? "已读完文档" : "已读完本节");
                    if (result.Page.Coverage?.IsPartial == true) { StatusText += "；知识覆盖仍为部分"; }
                }
                else
                {
                    SetFailure(result.Page.Status);
                }
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
        StatusText = status == KnowledgeReadStatus.Cancelled ? "已取消读取，请重新打开。" : "无法读取此文档，服务或目标不可用。请重新打开。";
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
            StatusText = "已取消读取，请重新打开。";
            Changed?.Invoke();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Cancel();
            _target = null;
            StatusText = "读取上下文已更改，请重新打开文档。";
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
