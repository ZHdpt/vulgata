using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Owned temporary file exchange and controlled configuration only; no external operator, native tool or Identity qualification.</summary>
public sealed class OctoberScanOperatorExchangeTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AtomicResponse_UsesExactOriginalRequestAndExplicitFiniteSelection_ThenCleansAsync(bool empty)
    {
        using var files = new Exchange(); using var cancellation = new CancellationTokenSource();
        var request = Request(); var pending = new OctoberOperatorRootSelection(new("operator", files.Path, 5)).InteractAsync(request, cancellation.Token);
        try
        {
            await files.AwaitRequestAsync(pending);
            var written = JsonSerializer.Deserialize<OctoberOperatorRootSelectionRequest>(await File.ReadAllTextAsync(files.RequestPath), Json)!;
            Assert.Equal(request.RequestId, written.RequestId); Assert.Equal(request.RunId, written.RunId); Assert.Equal(request.CorpusId, written.CorpusId);
            Assert.Equal(request.Candidates.ToArray(), written.Candidates.ToArray());
            var selected = empty ? ImmutableArray<Guid>.Empty : ImmutableArray.Create(request.Candidates[0].UnitId);
            string temporary = System.IO.Path.Combine(files.Path, "operator-response.tmp");
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new OctoberOperatorRootSelectionResponse(1, request.RequestId, request.RunId, selected), Json), new UTF8Encoding(false, true));
            await Task.WhenAny(pending, Task.Delay(TimeSpan.FromMilliseconds(150)));
            Assert.False(pending.IsCompleted); Assert.False(File.Exists(System.IO.Path.Combine(files.Path, "root-selection.response.json")));
            File.Move(temporary, System.IO.Path.Combine(files.Path, "root-selection.response.json"));
            var response = Assert.IsType<OctoberOperatorRootSelectionResponse>(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(selected.ToArray(), response.SelectedUnitIds.ToArray());
            Assert.False(Directory.Exists(files.Path));
        }
        finally { cancellation.Cancel(); try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { } }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("stale-request")]
    [InlineData("foreign-run")]
    [InlineData("foreign-unit")]
    [InlineData("duplicate-unit")]
    [InlineData("missing-selection")]
    [InlineData("null-selection")]
    [InlineData("unknown-member")]
    [InlineData("duplicate-member")]
    [InlineData("version")]
    [InlineData("oversized")]
    public async Task InvalidResponse_IsNotAnEmptyOrFallbackSelection_AndCleansAsync(string fault)
    {
        using var files = new Exchange(); using var cancellation = new CancellationTokenSource();
        var request = Request(); var pending = new OctoberOperatorRootSelection(new("operator", files.Path, 5)).InteractAsync(request, cancellation.Token);
        try
        {
            await files.AwaitRequestAsync(pending);
            var response = new OctoberOperatorRootSelectionResponse(fault == "version" ? 2 : 1,
                fault == "stale-request" ? Guid.NewGuid() : request.RequestId, fault == "foreign-run" ? Guid.NewGuid() : request.RunId,
                fault == "foreign-unit" ? [Guid.NewGuid()] : fault == "duplicate-unit" ? [request.Candidates[0].UnitId, request.Candidates[0].UnitId] : [request.Candidates[0].UnitId]);
            var node = JsonSerializer.SerializeToNode(response, Json)!.AsObject();
            if (fault == "missing-selection") node.Remove("selectedUnitIds");
            if (fault == "null-selection") node["selectedUnitIds"] = null;
            if (fault == "unknown-member") node["fallback"] = true;
            string text = node.ToJsonString();
            if (fault == "duplicate-member") text = "{\"version\":1," + text[1..];
            if (fault == "oversized") text += new string(' ', 65536);
            await files.RespondAsync(text);
            OctoberOperatorRootSelectionResponse? result = null;
            try { result = await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
            Assert.Null(result); Assert.True(pending.IsCompleted);
            Assert.False(Directory.Exists(files.Path));
        }
        finally { cancellation.Cancel(); try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { } }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbsentResponse_TimeoutOrCancellationIsNotExplicitEmpty_AndCleansAsync(bool cancel)
    {
        using var files = new Exchange(); using var cancellation = new CancellationTokenSource();
        var pending = new OctoberOperatorRootSelection(new("operator", files.Path, 1)).InteractAsync(Request(), cancellation.Token);
        try
        {
            await files.AwaitRequestAsync(pending);
            if (cancel) cancellation.Cancel();
            OctoberOperatorRootSelectionResponse? result = null;
            try { result = await pending.WaitAsync(TimeSpan.FromSeconds(4)); } catch (Exception) { }
            Assert.Null(result); Assert.True(pending.IsCompleted); Assert.False(Directory.Exists(files.Path));
        }
        finally { cancellation.Cancel(); try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { } }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PreexistingExchange_IsNeverAdoptedOrDeletedAsync()
    {
        using var files = new Exchange(); Directory.CreateDirectory(files.Path);
        string marker = System.IO.Path.Combine(files.Path, "caller-owned.txt"); await File.WriteAllTextAsync(marker, "preserve");
        await Assert.ThrowsAnyAsync<Exception>(() => new OctoberOperatorRootSelection(new("operator", files.Path, 1)).InteractAsync(Request()));
        Assert.Equal("preserve", await File.ReadAllTextAsync(marker)); Assert.False(File.Exists(files.RequestPath));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExchangeReplacement_IsPreventedOrFailsClosedWithoutFollowingForeignFilesAsync()
    {
        using var files = new Exchange(); using var cancellation = new CancellationTokenSource();
        var request = Request(); var pending = new OctoberOperatorRootSelection(new("operator", files.Path, 5)).InteractAsync(request, cancellation.Token);
        bool renamed = false; string moved = files.Path + "-moved";
        try
        {
            await files.AwaitRequestAsync(pending);
            try { Directory.Move(files.Path, moved); renamed = true; } catch (IOException) { }
            if (renamed)
            {
                Directory.CreateDirectory(files.Path); string marker = System.IO.Path.Combine(files.Path, "foreign.txt"); await File.WriteAllTextAsync(marker, "preserve");
                cancellation.Cancel();
                OctoberOperatorRootSelectionResponse? result = null; try { result = await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
                Assert.Null(result); Assert.Equal("preserve", await File.ReadAllTextAsync(marker));
            }
            else
            {
                await files.RespondAsync(JsonSerializer.Serialize(new OctoberOperatorRootSelectionResponse(1, request.RequestId, request.RunId, [request.Candidates[0].UnitId]), Json));
                Assert.NotNull(await pending.WaitAsync(TimeSpan.FromSeconds(5))); Assert.False(Directory.Exists(files.Path));
            }
        }
        finally { cancellation.Cancel(); try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { } }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogBounds_RejectBeforePublishingAnyCandidateFileAsync(bool bytes)
    {
        using var files = new Exchange(); var request = Request();
        request = bytes ? request with { Candidates = [request.Candidates[0] with { SemanticSelector = new string('x', 1048576) }] }
            : request with { Candidates = [.. Enumerable.Range(0, 1025).Select(i => request.Candidates[0] with { UnitId = Guid.NewGuid(), SemanticSelector = "unit-" + i })] };
        await Assert.ThrowsAnyAsync<Exception>(() => new OctoberOperatorRootSelection(new("operator", files.Path, 1)).InteractAsync(request));
        Assert.False(Directory.Exists(files.Path));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("cl-only")]
    [InlineData("exact-settings")]
    [InlineData("operator")]
    public void ExplicitOptions_KeepClOnlyAndExactCompatibilityWithoutOpeningExchange(string mode)
    {
        using var files = new Exchange();
        var runtime = Runtime(files.Parent);
        var values = new Dictionary<string, string?> { ["October:BusinessRootSelectionMode"] = mode == "cl-only" ? "invalid-ignored" : mode,
            ["October:BusinessRootSelectionDirectory"] = files.Path, ["October:BusinessRootSelectionTimeoutSeconds"] = "5" };
        if (mode == "exact-settings") values.Clear();
        var options = OctoberGeneratorEntryPoint.ReadOperatorRootSelectionOptions(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new() { OutputStage = mode == "cl-only" ? "cl-only" : "cl-and-bl", MaximumTotalTokens = 32768, SourceRules = [new(runtime.PreparedRoots[0].RepositoryId, "src", "selected", "Controlled explicit source")] }, new([], new(), 32768), runtime);
        if (mode == "cl-only") Assert.Null(options);
        else { Assert.Equal(mode, options!.Mode); if (mode == "operator") { Assert.Equal(files.Path, options.Directory); Assert.Equal(5, options.TimeoutSeconds); } }
        Assert.False(Directory.Exists(files.Path));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("unknown-mode")]
    [InlineData("missing-directory")]
    [InlineData("relative-directory")]
    [InlineData("timeout-zero")]
    [InlineData("timeout-large")]
    [InlineData("nonempty-roots")]
    [InlineData("source-overlap")]
    [InlineData("log-overlap")]
    [InlineData("state-overlap")]
    [InlineData("preexisting")]
    public void InvalidOperatorOptions_CannotFallbackOrOverlapOwnedResources(string fault)
    {
        using var files = new Exchange(); var runtime = Runtime(files.Parent);
        var values = new Dictionary<string, string?> { ["October:BusinessRootSelectionMode"] = fault == "unknown-mode" ? "infer" : "operator",
            ["October:BusinessRootSelectionDirectory"] = fault switch { "missing-directory" => null, "relative-directory" => "relative", "source-overlap" => System.IO.Path.Combine(runtime.PreparedRoots[0].PhysicalRootPath, "exchange"), "log-overlap" => System.IO.Path.Combine(runtime.LogDirectory, "exchange"), "state-overlap" => System.IO.Path.Combine(runtime.JavaNavigation.DisposableStateRoot, "exchange"), _ => files.Path },
            ["October:BusinessRootSelectionTimeoutSeconds"] = fault == "timeout-zero" ? "0" : fault == "timeout-large" ? "301" : "5" };
        if (fault == "preexisting") Directory.CreateDirectory(files.Path);
        var settings = new OctoberBusinessSettings(fault == "nonempty-roots" ? [new(Guid.NewGuid(), "src/Order.cs", "file", FlowRootKind.SelectedCodeUnit)] : [], new(), 32768);
        Assert.ThrowsAny<Exception>(() => OctoberGeneratorEntryPoint.ReadOperatorRootSelectionOptions(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new() { OutputStage = "cl-and-bl", MaximumTotalTokens = 32768, SourceRules = [new(runtime.PreparedRoots[0].RepositoryId, "src", "selected", "Controlled explicit source")] }, settings, runtime));
    }

    private static OctoberRuntimeConfiguration Runtime(string parent) => new(
        [new("target", Guid.NewGuid(), Guid.NewGuid(), "root", System.IO.Path.Combine(parent, "source"))], System.IO.Path.Combine(parent, "tools", "codegraph.ps1"),
        new(System.IO.Path.Combine(parent, "state"), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(15), 2 * 1024 * 1024),
        new([System.IO.Path.Combine(parent, "tools")], [System.IO.Path.Combine(parent, "jdk")], System.IO.Path.Combine(parent, "system", "cmd.exe"), [System.IO.Path.Combine(parent, "tools")], System.IO.Path.Combine(parent, "jdk", "bin")), System.IO.Path.Combine(parent, "log"));
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task MoreThan256OfferedSelections_AreRejectedWithoutTruncationAsync()
    {
        using var files = new Exchange(); using var cancellation = new CancellationTokenSource();
        var request = Request(); request = request with { Candidates = [.. Enumerable.Range(0, 257).Select(i => request.Candidates[0] with { UnitId = Guid.NewGuid(), SemanticSelector = "unit-" + i })] };
        var pending = new OctoberOperatorRootSelection(new("operator", files.Path, 5)).InteractAsync(request, cancellation.Token);
        try
        {
            await files.AwaitRequestAsync(pending);
            await files.RespondAsync(JsonSerializer.Serialize(new OctoberOperatorRootSelectionResponse(1, request.RequestId, request.RunId, [.. request.Candidates.Select(item => item.UnitId)]), Json));
            OctoberOperatorRootSelectionResponse? result = null; try { result = await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
            Assert.True(pending.IsCompleted); Assert.Null(result); Assert.False(Directory.Exists(files.Path));
        }
        finally { cancellation.Cancel(); try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { } }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReparseAncestorOrResponse_IsNotFollowedAndPreservesOwnedForeignTargetAsync(bool ancestor)
    {
        using var files = new Exchange(); using var cancellation = new CancellationTokenSource();
        var request = Request();
        if (ancestor)
        {
            string target = System.IO.Path.Combine(files.Parent, "foreign-directory"); Directory.CreateDirectory(target);
            string marker = System.IO.Path.Combine(target, "marker.txt"); await File.WriteAllTextAsync(marker, "preserve");
            string link = System.IO.Path.Combine(files.Parent, "linked-parent"); Directory.CreateSymbolicLink(link, target);
            await Assert.ThrowsAnyAsync<Exception>(() => new OctoberOperatorRootSelection(new("operator", System.IO.Path.Combine(link, "exchange"), 1)).InteractAsync(request));
            Assert.Equal("preserve", await File.ReadAllTextAsync(marker)); Assert.False(Directory.Exists(System.IO.Path.Combine(target, "exchange")));
            Directory.Delete(link); // Remove only the test-created link, never traverse its target.
            return;
        }
        var pending = new OctoberOperatorRootSelection(new("operator", files.Path, 5)).InteractAsync(request, cancellation.Token);
        try
        {
            await files.AwaitRequestAsync(pending);
            string target = System.IO.Path.Combine(files.Parent, "foreign-response.json");
            string payload = JsonSerializer.Serialize(new OctoberOperatorRootSelectionResponse(1, request.RequestId, request.RunId, [request.Candidates[0].UnitId]), Json);
            await File.WriteAllTextAsync(target, payload);
            File.CreateSymbolicLink(System.IO.Path.Combine(files.Path, "root-selection.response.json"), target);
            OctoberOperatorRootSelectionResponse? result = null; try { result = await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
            Assert.True(pending.IsCompleted); Assert.Null(result); Assert.Equal(payload, await File.ReadAllTextAsync(target));
        }
        finally { cancellation.Cancel(); try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { } }
    }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static OctoberOperatorRootSelectionRequest Request() => new(1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30),
        [new(Guid.NewGuid(), new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid(), "src/Order.cs", "file")]);
    private sealed class Exchange : IDisposable
    {
        internal string Parent { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "october-operator-test-" + Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(Parent, "exchange");
        internal string RequestPath => System.IO.Path.Combine(Path, "root-selection.request.json");
        internal Exchange() => Directory.CreateDirectory(Parent);
        internal async Task AwaitRequestAsync(Task<OctoberOperatorRootSelectionResponse?> pending)
        {
            var elapsed = Stopwatch.StartNew();
            while (!File.Exists(RequestPath))
            {
                if (pending.IsCompleted) { await pending; throw new InvalidOperationException("Exchange returned before publishing its request"); }
                if (elapsed.Elapsed > TimeSpan.FromSeconds(3)) throw new TimeoutException("Request publication did not complete");
                await Task.Delay(10);
            }
        }
        internal async Task RespondAsync(string text)
        {
            string temporary = System.IO.Path.Combine(Path, "operator-response.tmp");
            await File.WriteAllTextAsync(temporary, text, new UTF8Encoding(false, true));
            File.Move(temporary, System.IO.Path.Combine(Path, "root-selection.response.json"));
        }
        public void Dispose() => Directory.Delete(Parent, true);
    }
}
