using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared;
using Vulgata.Shared.Explore;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.ViewModels.Explore;

namespace Vulgata.Tests.Explore;

public sealed class KnowledgeGraphViewModelTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RefreshAsync_PendingRefresh_ClearsPriorGraphThenReplacesAtomicallyAsync()
    {
        var service = new GraphViewModelServiceProbe();
        using var vm = new KnowledgeGraphViewModel(service);
        await vm.LoadAsync();
        Assert.Equal(LoadState.Loaded, vm.LoadState);
        var pending = new TaskCompletionSource<ExploreGraphResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Response = _ => pending.Task;
        var refresh = vm.RefreshAsync();
        Assert.Null(vm.Graph);
        Assert.Contains(vm.LoadState, new[] { LoadState.Loading, LoadState.Refreshing });
        var replacement = service.Result(2);
        pending.SetResult(replacement);
        await refresh;
        Assert.Same(replacement.Graph, vm.Graph);
        Assert.Equal(LoadState.Loaded, vm.LoadState);
        Assert.Equal(2, service.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RefreshAsync_OlderRequestFinishesLast_DoesNotRestoreOldGraphAsync()
    {
        var service = new GraphViewModelServiceProbe();
        var pending = new TaskCompletionSource<ExploreGraphResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Response = _ => service.Calls == 1 ? pending.Task : Task.FromResult(service.Result(2));
        using var vm = new KnowledgeGraphViewModel(service);
        var first = vm.LoadAsync();
        await vm.RefreshAsync();
        Assert.True(service.Tokens[0].IsCancellationRequested);
        pending.SetResult(service.Result(1));
        await first;
        Assert.Equal("节点2", Assert.Single(vm.Graph!.Nodes).Title);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContextChanged_VisibleOrPendingGraph_ClearsAndRequiresFreshLoadAsync(bool pendingAtChange)
    {
        var service = new GraphViewModelServiceProbe();
        using var vm = new KnowledgeGraphViewModel(service);
        await vm.LoadAsync();
        var pending = new TaskCompletionSource<ExploreGraphResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? refresh = null;
        if (pendingAtChange)
        {
            service.Response = _ => pending.Task;
            refresh = vm.RefreshAsync();
        }
        service.NotifyContextChanged();
        Assert.Null(vm.Graph);
        Assert.Null(vm.Mode);
        Assert.False(vm.IsControlledFixture);
        if (refresh is not null)
        {
            Assert.True(service.Tokens[^1].IsCancellationRequested);
            pending.SetResult(service.Result(1));
            await refresh;
            Assert.Null(vm.Graph);
        }
        service.Response = _ => Task.FromResult(service.Result(2, KnowledgeMode.Business));
        await vm.LoadAsync();
        Assert.Equal("节点2", Assert.Single(vm.Graph!.Nodes).Title);
        Assert.Equal(KnowledgeMode.Business, vm.Mode);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Cancel_LateSuccessfulResponse_CannotRestoreGraphAsync()
    {
        var service = new GraphViewModelServiceProbe();
        var pending = new TaskCompletionSource<ExploreGraphResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Response = _ => pending.Task;
        using var vm = new KnowledgeGraphViewModel(service);
        var loading = vm.LoadAsync();
        vm.Cancel();
        Assert.True(service.Tokens[0].IsCancellationRequested);
        pending.SetResult(service.Result(1));
        await loading;
        Assert.Null(vm.Graph);
        Assert.Equal(LoadState.Idle, vm.LoadState);
        Assert.Contains("取消", vm.StatusText, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeReadStatus.Unavailable)]
    [InlineData(KnowledgeReadStatus.UnavailableService)]
    [InlineData(KnowledgeReadStatus.ReadBudgetExceeded)]
    [InlineData(KnowledgeReadStatus.Cancelled)]
    public async Task RefreshAsync_FailedResult_RemovesGraphInsteadOfShowingStaleDataAsync(KnowledgeReadStatus status)
    {
        var service = new GraphViewModelServiceProbe();
        using var vm = new KnowledgeGraphViewModel(service);
        await vm.LoadAsync();
        service.Response = _ => Task.FromResult(new ExploreGraphResult(status, null));
        await vm.RefreshAsync();
        Assert.Null(vm.Graph);
        Assert.Null(vm.Mode);
        Assert.NotEqual(LoadState.Loaded, vm.LoadState);
        Assert.DoesNotContain("节点", vm.StatusText, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Dispose_PendingRequest_UnsubscribesAndFreshAttachmentLoadsAgainAsync()
    {
        var service = new GraphViewModelServiceProbe();
        var pending = new TaskCompletionSource<ExploreGraphResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Response = _ => pending.Task;
        var vm = new KnowledgeGraphViewModel(service);
        var loading = vm.LoadAsync();
        Assert.Equal(1, service.Subscribers);
        vm.Dispose();
        Assert.Equal(0, service.Subscribers);
        Assert.True(service.Tokens[0].IsCancellationRequested);
        pending.SetResult(service.Result(1));
        await loading;
        Assert.Null(vm.Graph);
        await vm.RefreshAsync();
        Assert.Equal(1, service.Calls);
        service.Response = _ => Task.FromResult(service.Result(2));
        using var reattached = new KnowledgeGraphViewModel(service);
        await reattached.LoadAsync();
        Assert.Equal(2, service.Calls);
        Assert.Equal("节点2", Assert.Single(reattached.Graph!.Nodes).Title);
    }
}

/// <summary>Presentation port responses only; no owner authority or Knowledge codec is modeled here.</summary>
internal sealed class GraphViewModelServiceProbe : IExploreGraphService
{
    private Action? _contextChanged;
    public int Subscribers { get; private set; }
    public int Calls { get; private set; }
    public List<CancellationToken> Tokens { get; } = [];
    public Func<CancellationToken, Task<ExploreGraphResult>>? Response { get; set; }
    public event Action? ContextChanged
    {
        add { _contextChanged += value; Subscribers++; }
        remove { _contextChanged -= value; Subscribers--; }
    }
    public void NotifyContextChanged() => _contextChanged?.Invoke();
    public Task<ExploreGraphResult> LoadGraphAsync(CancellationToken cancellationToken = default)
    {
        Calls++;
        Tokens.Add(cancellationToken);
        return Response?.Invoke(cancellationToken) ?? Task.FromResult(Result(1));
    }
    public ExploreGraphResult Result(int id, KnowledgeMode mode = KnowledgeMode.IT)
    {
        var fixture = new ControlledGraphFixture();
        return new(KnowledgeReadStatus.Success,
            new([fixture.Node(id, mode == KnowledgeMode.Business ? DocumentKind.BusinessLogic : DocumentKind.CodeLogic)], [], [], false, new(false, [])), mode, true);
    }
}
