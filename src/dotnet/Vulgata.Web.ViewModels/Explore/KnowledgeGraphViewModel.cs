using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared;
using Vulgata.Shared.Explore;

namespace Vulgata.Web.ViewModels.Explore;

public sealed class KnowledgeGraphViewModel : IDisposable
{
    private readonly IExploreGraphService _service;
    private readonly object _gate = new();
    private CancellationTokenSource? _pending;
    private long _generation;
    private bool _disposed;

    public KnowledgeGraphViewModel(IExploreGraphService service)
    {
        _service = service;
        _service.ContextChanged += Clear;
    }

    public ExploreGraph? Graph { get; private set; }
    public KnowledgeMode? Mode { get; private set; }
    public bool IsControlledFixture { get; private set; }
    public LoadState LoadState { get; private set; }
    public string StatusText { get; private set; } = "";
    public event Action? Changed;
    public Task LoadAsync() => RefreshAsync();

    public Task RefreshAsync()
    {
        lock (_gate) { return _disposed ? Task.CompletedTask : ReadAsync(); }
    }

    private async Task ReadAsync()
    {
        var refreshing = Graph is not null;
        CancelPending();
        var generation = ++_generation;
        var pending = new CancellationTokenSource();
        _pending = pending;
        ResetGraph();
        LoadState = refreshing ? LoadState.Refreshing : LoadState.Loading;
        StatusText = refreshing ? "正在刷新图谱…" : "正在加载图谱…";
        Changed?.Invoke();
        try
        {
            var result = await _service.LoadGraphAsync(pending.Token);
            lock (_gate)
            {
                if (_disposed || generation != _generation || pending.IsCancellationRequested) { return; }
                if (result.Status == KnowledgeReadStatus.Success && result.Graph is not null)
                {
                    Graph = result.Graph;
                    Mode = result.Mode;
                    IsControlledFixture = result.IsControlledFixture;
                    LoadState = Graph.Nodes.IsEmpty ? LoadState.Empty : LoadState.Loaded;
                    StatusText = Graph.Nodes.IsEmpty ? "当前范围没有可显示的文档。" : "图谱已加载；需要更新时请手动刷新。";
                }
                else { SetFailure(result.Status); }
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
                if (generation == _generation && !_disposed) { _pending = null; Changed?.Invoke(); }
                pending.Dispose();
            }
        }
    }

    private void SetFailure(KnowledgeReadStatus status)
    {
        ResetGraph();
        LoadState = status == KnowledgeReadStatus.Cancelled ? LoadState.Idle : LoadState.Error;
        StatusText = status switch
        {
            KnowledgeReadStatus.Cancelled => "已取消加载图谱。",
            KnowledgeReadStatus.ReadBudgetExceeded => "图谱读取已超时或达到读取限制，请重试。",
            _ => "图谱暂不可用，请重新加载。",
        };
    }

    private void ResetGraph()
    {
        Graph = null;
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
            ResetGraph();
            LoadState = LoadState.Idle;
            StatusText = "已取消加载图谱。";
            Changed?.Invoke();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Cancel();
            StatusText = "读取上下文已更改，请重新加载图谱。";
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
            ++_generation;
            CancelPending();
            ResetGraph();
        }
    }
}
