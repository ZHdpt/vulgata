using System.Diagnostics;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Tests.PreparedSource;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Real test-owned process exit with controlled diagnostic input; no JDT/server/provider is started.</summary>
[Collection(PreparedSourceTestCollection.Name)]
public sealed class OctoberScanJdtProcessSettlementTests
{
    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task WaitForExitAsync_ProcessExitedButDiagnosticReadBlocked_ReturnsActualExitCodeAsync()
    {
        await using var harness = await ExitedProcessHarness.CreateAsync();
        Assert.True(harness.Owner.HasExited);
        Assert.False(harness.Owner.StderrPumpCompleted);
        var exit = await harness.Owner.WaitForExitAsync(TimeSpan.FromMilliseconds(250), CancellationToken.None);
        Assert.Equal(7, exit);
        Assert.False(harness.Reader.ReadCompleted.IsCompleted,
            "The controlled diagnostic stream is deliberately unfinished when the process exit is acknowledged.");
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task DisposeAsync_ExitedProcessWithBlockedDiagnosticRead_SettlesReaderBeforeRemovingOwnedStateAsync()
    {
        await using var harness = await ExitedProcessHarness.CreateAsync();
        Assert.True(harness.Owner.HasExited);
        Assert.False(harness.Owner.StderrPumpCompleted);
        await harness.DisposeOwnerAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(harness.Reader.CancellationObserved || harness.Reader.Disposed,
            "Disposal must stop the diagnostic reader rather than leave it waiting indefinitely.");
        Assert.True(harness.Reader.ReadCompleted.IsCompleted);
        Assert.True(harness.Owner.StderrPumpCompleted);
        Assert.Empty(Directory.EnumerateFileSystemEntries(harness.StateRoot));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitForExitAsync_CapturedChildOutlivesParent_RemainsUnsettledUntilChildExitAsync(bool cancelWait)
    {
        await using var harness = await ParentChildHarness.CreateAsync();
        Assert.True(harness.Parent.HasExited);
        Assert.False(harness.Child.HasExited);
        using var cancellation = new CancellationTokenSource();
        if (cancelWait)
        {
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Owner.WaitForExitAsync(TimeSpan.FromSeconds(2), cancellation.Token));
        }
        else
            await Assert.ThrowsAsync<TimeoutException>(() => harness.Owner.WaitForExitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None));
        Assert.False(harness.Child.HasExited);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.DisposeOwnerAsync().AsTask());
        Assert.True(File.Exists(harness.StateMarker), "Unknown child settlement must preserve owned state.");
        await harness.Owner.KillTreeAsync(CancellationToken.None);
        Assert.Equal(7, await harness.Owner.WaitForExitAsync(TimeSpan.FromSeconds(3), CancellationToken.None));
        Assert.True(harness.Parent.WaitForExit(0), "Successful owner wait must confirm the parent kernel signal.");
        Assert.True(harness.Child.WaitForExit(0), "Successful owner wait must confirm the captured child kernel signal.");
        Assert.True(harness.Child.HasExited);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task KillTreeAsync_CapturedParentAlreadyExited_TerminatesOnlyRetainedOwnedChildAsync()
    {
        await using var harness = await ParentChildHarness.CreateAsync();
        Assert.True(harness.Parent.HasExited);
        Assert.False(harness.Child.HasExited);
        await harness.Owner.KillTreeAsync(CancellationToken.None);
        Assert.True(harness.Child.WaitForExit(3000), "Owned fixture process must reach its kernel exit signal within the existing deadline.");
        Assert.Equal(7, await harness.Owner.WaitForExitAsync(TimeSpan.FromSeconds(3), CancellationToken.None));
        Assert.True(harness.Parent.WaitForExit(0), "Successful owner wait must confirm the parent kernel signal.");
        Assert.True(harness.Child.WaitForExit(0), "Successful owner wait must confirm the captured child kernel signal.");
        await harness.DisposeOwnerAsync();
        Assert.False(File.Exists(harness.StateMarker));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task KillTreeAsync_CaptureFailureStillTerminatesKnownHandles_WithoutClaimingCleanupAsync()
    {
        await using var harness = await ParentChildHarness.CreateAsync(releaseParent: false);
        Assert.False(harness.Parent.HasExited);
        Assert.False(harness.Child.HasExited);
        var captureFailure = typeof(PreparedJdtOwnedProcess).GetField("_captureFailure",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(captureFailure);
        captureFailure.SetValue(harness.Owner, new InvalidOperationException("controlled_capture_failure"));
        try
        {
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => harness.Owner.KillTreeAsync(CancellationToken.None));
            Assert.Contains("controlled_capture_failure", failure.ToString(), StringComparison.Ordinal);
            Assert.True(harness.Parent.WaitForExit(3000), "Owned fixture process must reach its kernel exit signal within the existing deadline.");
            Assert.True(harness.Child.WaitForExit(3000), "Owned fixture process must reach its kernel exit signal within the existing deadline.");
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Owner.WaitForExitAsync(TimeSpan.FromSeconds(1), CancellationToken.None));
                await Assert.ThrowsAsync<InvalidOperationException>(() => harness.DisposeOwnerAsync().AsTask());
                Assert.True(File.Exists(harness.StateMarker), "Capture uncertainty must not authorize state deletion even after known processes exit.");
            }
        }
        finally
        {
            // Remove only this injected fault so the fixture can settle its exact owned handles.
            // Harness cleanup releases both gates and confirms exit before disposing the owner.
            captureFailure.SetValue(harness.Owner, null);
        }
    }

    private sealed class ParentChildHarness : IAsyncDisposable
    {
        private readonly PreparedSourceFixture _fixture;
        private readonly string _parentRelease;
        private readonly string _childRelease;
        private bool _ownerDisposed;
        private ParentChildHarness(PreparedSourceFixture fixture, Process parent, Process child, PreparedJdtOwnedProcess owner, string parentRelease, string childRelease, string marker)
        {
            _fixture = fixture; Parent = parent; Child = child; Owner = owner;
            _parentRelease = parentRelease; _childRelease = childRelease; StateMarker = marker;
        }
        internal Process Parent { get; }
        internal Process Child { get; }
        internal PreparedJdtOwnedProcess Owner { get; }
        internal string StateMarker { get; }

        internal static async Task<ParentChildHarness> CreateAsync(bool releaseParent = true)
        {
            Assert.True(OperatingSystem.IsWindows());
            var fixture = await PreparedSourceFixture.CreateAsync();
            var gates = Path.Combine(fixture.SiblingPath, "process-gates");
            Directory.CreateDirectory(gates);
            var parentRelease = Path.Combine(gates, "parent-release");
            var childRelease = Path.Combine(gates, "child-release");
            var childReady = Path.Combine(gates, "child-ready");
            var childPid = Path.Combine(gates, "child-pid");
            var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            Assert.True(File.Exists(shell));
            var childScript = $"[IO.File]::WriteAllText('{Quote(childReady)}','ready'); while(-not [IO.File]::Exists('{Quote(childRelease)}')) {{ Start-Sleep -Milliseconds 20 }}; exit 9";
            var childEncoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(childScript));
            var parentScript = $"$s=New-Object Diagnostics.ProcessStartInfo; $s.FileName='{Quote(shell)}'; $s.Arguments='-NoProfile -NonInteractive -EncodedCommand {childEncoded}'; $s.UseShellExecute=$false; $s.CreateNoWindow=$true; $c=[Diagnostics.Process]::Start($s); [IO.File]::WriteAllText('{Quote(childPid)}',[string]$c.Id); while(-not [IO.File]::Exists('{Quote(parentRelease)}')) {{ Start-Sleep -Milliseconds 20 }}; exit 7";
            var start = new ProcessStartInfo(shell) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-EncodedCommand");
            start.ArgumentList.Add(Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(parentScript)));
            Process? parent = null;
            Process? child = null;
            PreparedJdtOwnedProcess? owner = null;
            try
            {
                var stateRoot = Path.Combine(fixture.SiblingPath, "owned-process-state");
                Directory.CreateDirectory(stateRoot);
                var state = new PreparedJdtOwnedStateDirectories(stateRoot);
                state.Prepare(Path.Combine(stateRoot, "data"), Path.Combine(stateRoot, "configuration"), Path.Combine(stateRoot, "temp"), Path.Combine(stateRoot, "profile"));
                var marker = Path.Combine(stateRoot, "data", "must-preserve.txt");
                await File.WriteAllTextAsync(marker, "Owned child has not settled");
                parent = Process.Start(start) ?? throw new InvalidOperationException("Owned parent failed to start");
                owner = new PreparedJdtOwnedProcess(parent, state, new StringReader(string.Empty));
                var readyDeadline = Stopwatch.StartNew();
                while (!File.Exists(childPid) || !File.Exists(childReady))
                {
                    if (parent.HasExited || readyDeadline.Elapsed > TimeSpan.FromSeconds(10))
                        throw new InvalidOperationException("Owned child readiness was not established");
                    await Task.Delay(20);
                }
                child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(childPid)));
                _ = child.SafeHandle;
                Assert.False(child.HasExited);
                owner.CaptureDescendantsForTermination();
                if (releaseParent)
                {
                    await File.WriteAllTextAsync(parentRelease, "release parent only");
                    Assert.True(parent.WaitForExit(3000), "Owned fixture process must reach its kernel exit signal within the existing deadline.");
                    Assert.Equal(7, parent.ExitCode);
                }
                Assert.False(child.HasExited);
                return new(fixture, parent, child, owner, parentRelease, childRelease, marker);
            }
            catch
            {
                await File.WriteAllTextAsync(childRelease, "release owned child during failed setup");
                await File.WriteAllTextAsync(parentRelease, "release owned parent during failed setup");
                if (child is null && File.Exists(childPid)) child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(childPid)));
                if (child is not null) Assert.True(child.WaitForExit(5000), "Owned fixture process must reach its kernel exit signal within the existing deadline.");
                if (parent is not null) Assert.True(parent.WaitForExit(5000), "Owned fixture process must reach its kernel exit signal within the existing deadline.");
                if (owner is not null) await owner.DisposeAsync();
                child?.Dispose(); parent?.Dispose();
                await fixture.DisposeAsync();
                throw;
            }
        }
        private static string Quote(string value) => value.Replace("'", "''", StringComparison.Ordinal);
        internal async ValueTask DisposeOwnerAsync()
        {
            if (_ownerDisposed) return;
            await Owner.DisposeAsync();
            _ownerDisposed = true;
        }
        public async ValueTask DisposeAsync()
        {
            // Gates target only these test-owned processes; release them even when a RED assertion fails.
            await File.WriteAllTextAsync(_childRelease, "release owned child");
            Assert.True(Child.WaitForExit(5000), "Owned fixture process must reach its kernel exit signal within the existing deadline.");
            if (!_ownerDisposed)
            {
                await File.WriteAllTextAsync(_parentRelease, "release owned parent");
                Assert.True(Parent.WaitForExit(5000), "Owned fixture process must reach its kernel exit signal within the existing deadline.");
                await DisposeOwnerAsync();
            }
            Child.Dispose(); Parent.Dispose();
            await _fixture.DisposeAsync();
        }
    }
    private sealed class ExitedProcessHarness : IAsyncDisposable
    {
        private readonly PreparedSourceFixture _fixture;
        private readonly Process _process;
        private bool _ownerDisposed;
        private ExitedProcessHarness(PreparedSourceFixture fixture, Process process, PreparedJdtOwnedStateDirectories state, string stateRoot)
        {
            _fixture = fixture;
            _process = process;
            StateRoot = stateRoot;
            Reader = new BlockingDiagnosticReader();
            Owner = new PreparedJdtOwnedProcess(process, state, Reader);
        }
        internal PreparedJdtOwnedProcess Owner { get; }
        internal BlockingDiagnosticReader Reader { get; }
        internal string StateRoot { get; }
        internal static async Task<ExitedProcessHarness> CreateAsync()
        {
            Assert.True(OperatingSystem.IsWindows());
            var fixture = await PreparedSourceFixture.CreateAsync();
            Process? process = null;
            ExitedProcessHarness? harness = null;
            try
            {
                var stateRoot = Path.Combine(fixture.SiblingPath, "diagnostic-state");
                Directory.CreateDirectory(stateRoot);
                var state = new PreparedJdtOwnedStateDirectories(stateRoot);
                state.Prepare(Path.Combine(stateRoot, "data"), Path.Combine(stateRoot, "configuration"), Path.Combine(stateRoot, "temp"), Path.Combine(stateRoot, "profile"));
                var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                start.ArgumentList.Add("/d");
                start.ArgumentList.Add("/c");
                start.ArgumentList.Add("exit /b 7");
                process = Process.Start(start) ?? throw new InvalidOperationException("Owned exit process failed to start");
                harness = new(fixture, process, state, stateRoot);
                await harness.Reader.ReadStarted.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.True(process.WaitForExit(3000), "Owned fixture process must reach its kernel exit signal within the existing deadline.");
                Assert.Equal(7, process.ExitCode);
                return harness;
            }
            catch
            {
                if (process is not null && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    Assert.True(process.WaitForExit(3000), "Owned fixture process must reach its kernel exit signal within the existing deadline.");
                }
                if (harness is not null) await harness.DisposeAsync();
                else { process?.Dispose(); await fixture.DisposeAsync(); }
                throw;
            }
        }
        internal async ValueTask DisposeOwnerAsync()
        {
            if (_ownerDisposed) return;
            await Owner.DisposeAsync();
            _ownerDisposed = true;
        }
        public async ValueTask DisposeAsync()
        {
            // This explicit release belongs only to the controlled test reader and runs even when RED assertions fail.
            Reader.ReleaseForTestCleanup();
            await Reader.ReadCompleted.WaitAsync(TimeSpan.FromSeconds(3));
            await DisposeOwnerAsync();
            Reader.Dispose();
            _process.Dispose();
            await _fixture.DisposeAsync();
        }
    }

    private sealed class BlockingDiagnosticReader : TextReader
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task ReadStarted => _started.Task;
        internal Task ReadCompleted => _completed.Task;
        internal bool CancellationObserved { get; private set; }
        internal bool Disposed { get; private set; }
        public override Task<int> ReadAsync(char[] buffer, int index, int count) => ReadCoreAsync(CancellationToken.None);
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default) => new(ReadCoreAsync(cancellationToken));
        private async Task<int> ReadCoreAsync(CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            try
            {
                await _release.Task.WaitAsync(cancellationToken);
                return 0;
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }
            finally { _completed.TrySetResult(); }
        }
        internal void ReleaseForTestCleanup() => _release.TrySetResult();
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            _release.TrySetResult();
            base.Dispose(disposing);
        }
    }
}
