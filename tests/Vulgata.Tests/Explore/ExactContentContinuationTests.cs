using System.Collections.Immutable;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.Explore;

public sealed class ExactContentContinuationTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(null)]
    [InlineData("overview")]
    public async Task NextAsync_OversizedDocumentOrSection_ReplacesBoundedQualifiedChunksUntilOwnerTerminalAsync(string? section)
    {
        var host = new ExploreFixtureHost();
        var handle = host.Fixture.Handle(host.Fixture.ClA, section: section);
        var text = string.Concat(Enumerable.Repeat("订单😀", 1800));
        var chunks = Enumerable.Range(0, 6).Select(index => $"片段{index}:{text}").ToArray();
        Assert.True(chunks.Sum(chunk => Encoding.UTF8.GetByteCount(chunk)) > KnowledgeContract.MaximumContentBytes);
        host.Reader.ReadResponse = (request, _) =>
        {
            var index = host.Reader.Reads.Count - 1;
            Assert.Equal(handle, request.Document);
            Assert.Equal(index == 0 ? null : $"owner-cursor-{index}", request.Continuation?.Value);
            var page = host.Content(handle, chunks[index], index == chunks.Length - 1 ? null : $"owner-cursor-{index + 1}");
            Assert.InRange(JsonSerializer.SerializeToUtf8Bytes(page).Length, 1, KnowledgeContract.MaximumContentBytes);
            return Task.FromResult(page);
        };
        using var vm = host.CreateReader();
        var observed = new StringBuilder();
        await vm.OpenAsync(handle);
        for (var index = 0; index < chunks.Length; index++)
        {
            Assert.Equal(index + 1, host.Reader.Reads.Count);
            Assert.Equal(LoadState.Loaded, vm.LoadState);
            Assert.Equal(chunks[index], vm.Page!.Content);
            Assert.Equal(handle, vm.Page.Document!.Handle);
            Assert.Equal(index < chunks.Length - 1, vm.CanContinue);
            Assert.Equal(host.Fixture.Claims.Select(claim => claim.Qualification), vm.Page.Claims.Select(claim => claim.Qualification));
            Assert.Contains(vm.Page.Claims, claim => claim.Qualification == ClaimQualification.Advisory && !claim.Limitations.IsEmpty);
            observed.Append(vm.Page.Content);
            if (index < chunks.Length - 1)
            {
                await vm.NextAsync();
            }
        }
        Assert.Equal(string.Concat(chunks), observed.ToString());
        await vm.NextAsync();
        Assert.Equal(chunks.Length, host.Reader.Reads.Count);
        Assert.Null(vm.Session);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task NextAsync_DuplicateClick_ClearsOldChunkAndMakesOnePendingRequestAsync()
    {
        var host = new ExploreFixtureHost();
        var handle = host.Fixture.Handle(host.Fixture.ClA);
        var pending = new TaskCompletionSource<ContentPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Reader.ReadResponse = (_, _) => host.Reader.Reads.Count == 1
            ? Task.FromResult(host.Content(handle, "first", "next")) : pending.Task;
        using var vm = host.CreateReader();
        await vm.OpenAsync(handle);
        var next = vm.NextAsync();
        Assert.Null(vm.Page);
        Assert.False(vm.CanContinue);
        await vm.NextAsync();
        Assert.Equal(2, host.Reader.Reads.Count);
        pending.SetResult(host.Content(handle, "second"));
        await next;
        Assert.Equal("second", vm.Page!.Content);
        Assert.Null(vm.Session);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeReadStatus.InvalidContinuation)]
    [InlineData(KnowledgeReadStatus.Unavailable)]
    [InlineData(KnowledgeReadStatus.UnavailableService)]
    [InlineData(KnowledgeReadStatus.Cancelled)]
    public async Task NextAsync_OwnerRejectsContinuation_ClearsChunksAndRequiresExplicitFreshRestartAsync(KnowledgeReadStatus status)
    {
        var host = new ExploreFixtureHost();
        var handle = host.Fixture.Handle(host.Fixture.ClA, section: "overview");
        host.Reader.ReadResponse = (request, _) => Task.FromResult(request.Continuation is null
            ? host.Content(handle, "first", "opaque-next") : ExploreFixtureHost.Failure(status));
        using var vm = host.CreateReader();
        await vm.OpenAsync(handle);
        await vm.NextAsync();
        Assert.Null(vm.Page);
        Assert.Null(vm.Session);
        Assert.False(vm.CanContinue);
        Assert.Equal(2, host.Reader.Reads.Count);
        await vm.RetryAsync();
        Assert.Equal(2, host.Reader.Selections.Count);
        Assert.Equal(handle, host.Reader.Reads[^1].Document);
        Assert.Null(host.Reader.Reads[^1].Continuation);
        Assert.Equal("first", vm.Page!.Content);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task NextAsync_RepeatedOwnerCursor_FailsWithoutLoopOrRetainedContentAsync()
    {
        var host = new ExploreFixtureHost();
        var handle = host.Fixture.Handle(host.Fixture.ClA);
        host.Reader.ReadResponse = (_, _) => Task.FromResult(host.Content(handle, "fragment", "same-token"));
        using var vm = host.CreateReader();
        await vm.OpenAsync(handle);
        await vm.NextAsync();
        Assert.Null(vm.Page);
        Assert.False(vm.CanContinue);
        await vm.NextAsync();
        Assert.Equal(2, host.Reader.Reads.Count);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_OversizedUtf8OrProjection_RejectsWithoutTruncationAsync()
    {
        var host = new ExploreFixtureHost();
        var handle = host.Fixture.Handle(host.Fixture.ClA);
        using var vm = host.CreateReader();
        var oversizedText = string.Concat(Enumerable.Repeat("😀", 17000));
        Assert.True(oversizedText.Length < KnowledgeContract.MaximumContentBytes);
        Assert.True(Encoding.UTF8.GetByteCount(oversizedText) > KnowledgeContract.MaximumContentBytes);
        var oversizedClaims = host.Content(handle, "small") with
        {
            Claims = [new("claim", new string('x', KnowledgeContract.MaximumContentBytes), ClaimQualification.Advisory, [], [])],
        };
        foreach (var page in new[] { host.Content(handle, oversizedText), oversizedClaims })
        {
            Assert.True(JsonSerializer.SerializeToUtf8Bytes(page).Length > KnowledgeContract.MaximumContentBytes);
            host.Reader.ReadResponse = (_, _) => Task.FromResult(page);
            await vm.OpenAsync(handle);
            Assert.Null(vm.Page);
            Assert.Null(vm.Session);
            Assert.False(vm.CanContinue);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_LateResponseAfterNavigation_CannotRestorePreviousExactTargetAsync()
    {
        var host = new ExploreFixtureHost();
        var firstHandle = host.Fixture.Handle(host.Fixture.ClA, section: "overview");
        var secondHandle = host.Fixture.Handle(host.Fixture.ClB);
        var pending = new TaskCompletionSource<ContentPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Reader.ReadResponse = (request, _) => request.Document == firstHandle
            ? pending.Task : Task.FromResult(host.Content(secondHandle, "current"));
        using var vm = host.CreateReader();
        var first = vm.OpenAsync(firstHandle);
        await vm.OpenAsync(secondHandle);
        Assert.True(host.Reader.ReadTokens[0].IsCancellationRequested);
        pending.SetResult(host.Content(firstHandle, "old-private", "old-next"));
        await first;
        Assert.Equal(secondHandle, vm.Page!.Document!.Handle);
        Assert.Equal("current", vm.Page.Content);
        Assert.Null(vm.Session);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Cancel_PendingContinuation_IgnoresLateResponseAndClearsStateAsync()
    {
        var host = new ExploreFixtureHost();
        var handle = host.Fixture.Handle(host.Fixture.ClA);
        var pending = new TaskCompletionSource<ContentPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Reader.ReadResponse = (_, _) => host.Reader.Reads.Count == 1
            ? Task.FromResult(host.Content(handle, "first", "next")) : pending.Task;
        using var vm = host.CreateReader();
        await vm.OpenAsync(handle);
        var next = vm.NextAsync();
        vm.Cancel();
        Assert.True(host.Reader.ReadTokens[^1].IsCancellationRequested);
        pending.SetResult(host.Content(handle, "late-private", "late-next"));
        await next;
        Assert.Null(vm.Page);
        Assert.Null(vm.Session);
        Assert.False(vm.CanContinue);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenAsync_ContextOrSubjectChangedDuringAwait_DiscardsResponseAsync(bool changeSubject)
    {
        var host = new ExploreFixtureHost();
        var handle = host.Fixture.Handle(host.Fixture.ClA);
        var pending = new TaskCompletionSource<ContentPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Reader.ReadResponse = (_, _) => pending.Task;
        using var vm = host.CreateReader();
        var loading = vm.OpenAsync(handle);
        if (changeSubject)
        {
            host.Authentication.User = new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "other-user")], "ControlledFixture"));
        }
        else
        {
            host.Context.Current = new(host.Fixture.Selection(KnowledgeMode.Business), true);
        }
        pending.SetResult(host.Content(handle, "late-private", "next"));
        await loading;
        Assert.Null(vm.Page);
        Assert.Null(vm.Session);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task NextAsync_ContextChangesBeforeCall_DoesNotReuseOldSelectionAsync()
    {
        var host = new ExploreFixtureHost();
        using var vm = host.CreateReader();
        await vm.OpenAsync(host.Fixture.Handle(host.Fixture.ClA));
        host.Context.Current = new(host.Fixture.Selection(KnowledgeMode.Business), true);
        await vm.NextAsync();
        Assert.Single(host.Reader.Reads);
        Assert.Null(vm.Page);
        Assert.Null(vm.Session);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task NextAsync_EquivalentScopeArray_ContinuesWithoutFalseContextChangeAsync()
    {
        var host = new ExploreFixtureHost();
        using var vm = host.CreateReader();
        await vm.OpenAsync(host.Fixture.Handle(host.Fixture.ClA));
        var context = host.Context.Current!;
        host.Context.Current = context with { Selection = context.Selection with { RepositoryScope = context.Selection.RepositoryScope.ToArray().ToImmutableArray() } };
        await vm.NextAsync();
        Assert.Equal(2, host.Reader.Reads.Count);
        Assert.Equal(LoadState.Loaded, vm.LoadState);
        Assert.Equal(host.Fixture.Documents[0].SecondChunk, vm.Page!.Content);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContextChanged_DisplayedContent_ClearsSynchronouslyWithoutAnotherReadAsync(bool authenticationChanged)
    {
        var host = new ExploreFixtureHost();
        using var reader = host.CreateReader();
        using var browse = host.CreateBrowse();
        await reader.OpenAsync(host.Fixture.Handle(host.Fixture.ClA));
        await browse.LoadAsync();
        Assert.NotNull(reader.Page);
        Assert.NotNull(browse.Page);
        if (authenticationChanged)
        {
            host.Authentication.User = new(new ClaimsIdentity());
            host.Authentication.NotifyChanged();
        }
        else
        {
            host.Context.Current = new(host.Fixture.Selection(KnowledgeMode.Business), true);
            host.Context.NotifyChanged();
        }
        Assert.Null(reader.Page);
        Assert.Null(reader.Session);
        Assert.Null(browse.Page);
        Assert.Null(browse.Session);
        Assert.Single(host.Reader.Reads);
    }
}
