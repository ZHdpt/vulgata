using System.Diagnostics;
using System.Text;
using ModelContextProtocol.Client;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Source;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.PreparedSource;
using Vulgata.Tests.TestInfrastructure;
using Xunit.Abstractions;

namespace Vulgata.Tests.OctoberScan;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class OctoberScanToolRuntimeQualificationTests(ITestOutputHelper output)
{
    private const string CodeGraphCommand = @"G:\packages\npm\prefix\codegraph.ps1";
    private const string JdtRuntimeRoot = @"G:\source\repos\eclipse.jdt.ls\org.eclipse.jdt.ls.product\target\repository";

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OctoberRuntime_PinnedNativeTools_MultiFileNavigationRenewalAndCleanupAsync()
    {
        Assert.True(OperatingSystem.IsWindows());
        Assert.True(File.Exists(CodeGraphCommand));
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var fixture = await PreparedSourceFixture.CreateAsync();
        OctoberToolRuntime? runtime = null;
        var cleanupConfirmed = false;
        var fixtureRoot = fixture.SandboxPath;
        var phase = "fixture-setup";
        var elapsed = Stopwatch.StartNew();
        try
        {
            var utf8 = new UTF8Encoding(false);
            const string callerRelative = "src/main/java/fixture/Caller.java";
            const string targetRelative = "src/main/java/fixture/Target.java";
            const string callerText = "package fixture;\npublic class Caller {\n    public void call() { new Target().targetMethod(); }\n}\n";
            const string targetText = "package fixture;\npublic class Target {\n    public void targetMethod() {}\n}\n";
            var sources = new Dictionary<string, string>
            {
                [callerRelative] = callerText,
                [targetRelative] = targetText,
                ["Beacon.cs"] = "public static class OctoberNativeBeacon { public static int LocateOctober() => 14; }\n",
                ["BeaconCaller.cs"] = "public static class OctoberNativeCaller { public static int Invoke() => OctoberNativeBeacon.LocateOctober(); }\n",
                [".project"] = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><projectDescription><name>october-s1-owned-fixture</name><buildSpec><buildCommand><name>org.eclipse.jdt.core.javabuilder</name></buildCommand></buildSpec><natures><nature>org.eclipse.jdt.core.javanature</nature></natures></projectDescription>",
                [".classpath"] = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><classpath><classpathentry kind=\"src\" path=\"src/main/java\"/><classpathentry kind=\"con\" path=\"org.eclipse.jdt.launching.JRE_CONTAINER\"/><classpathentry kind=\"output\" path=\"bin\"/></classpath>",
                [".settings/org.eclipse.jdt.core.prefs"] = "eclipse.preferences.version=1\norg.eclipse.jdt.core.compiler.source=21\norg.eclipse.jdt.core.compiler.compliance=21\norg.eclipse.jdt.core.compiler.codegen.targetPlatform=21\n",
            };
            foreach (var (relative, content) in sources)
            {
                var path = Path.Combine(fixture.PreparedRootPath, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, content, utf8);
            }
            var before = await SnapshotSourceAsync(fixture.PreparedRootPath);
            var stateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
            Directory.CreateDirectory(stateRoot);
            var marker = Path.Combine(stateRoot, "caller-owned-marker.txt");
            await File.WriteAllTextAsync(marker, "preserve caller-owned state\n", utf8);
            var profile = new PreparedJavaNavigationProfile(stateRoot, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(15), MaximumResponseBytes: 2 * 1024 * 1024);
            var javaFactory = new PreparedJavaNavigationContextFactory(profile,
                new PreparedJdtLsRuntime(new PreparedJdtCommandResolver(),
                    new LifecycleTraceLauncher(output), new LifecycleTraceProtocolFactory(output), CreatePinnedHostPolicy()));
            fixture.Authority.Approve(fixture.Approval);
            var sourceIssuer = new PreparedSourceContextIssuer(fixture.Authority);
            var binding = new PreparedSourceBinding(sourceIssuer, "october-s1-owned-target", [new("october-s1-owned-target", fixture.Approval.BatchId, fixture.Approval.RepositoryId, fixture.Approval.RootBindingIdentity, fixture.PreparedRootPath)]);
            runtime = new OctoberToolRuntime(fixture.Approval, binding, new PreparedToolWorkspaceIssuer(fixture.Authority, new FixtureToolAuthorizer(fixture.Approval)), javaFactory, new PreparedCodeGraphConnectionFactory(CodeGraphCommand), TimeSpan.FromMinutes(8));
            output.WriteLine("Owned fixture={0}; default dedicated JDT lease=30 seconds; JDT state is external to source. No provider or database is used.", fixtureRoot);
            phase = "codegraph.prepare";
            var ready = await runtime.PrepareAsync();
            output.WriteLine("phase={0}; elapsedMs={1}; status={2}; issue={3}", phase, elapsed.ElapsedMilliseconds, ready.Status, ready.IssueCode);
            Assert.True(ready.IsReady, $"Native CodeGraph readiness failed: {ready.Status}/{ready.IssueCode}");
            var descriptor = Assert.IsType<PreparedCodeGraphConnection>(runtime.PreparedConnection);
            Assert.Equal(fixture.Approval.RepositoryId, descriptor.RepositoryId);
            Assert.Equal(Path.GetFullPath(fixture.PreparedRootPath), descriptor.WorkingDirectory);
            Assert.Equal("1", descriptor.Environment["CODEGRAPH_NO_DOWNLOAD"]);

            phase = "source.list";
            var listing = await runtime.ListAsync(new("", 1));
            output.WriteLine("phase={0}; status={1}; entries={2}; limitations={3}", phase, listing.Outcome, listing.Entries.Count, listing.Limitations.Count);
            Assert.Equal(PreparedSourceOutcome.Complete, listing.Outcome);
            Assert.True(listing.Entries.Count > 1);
            Assert.DoesNotContain(listing.Entries, entry => entry.Location.RelativePath == ".codegraph");
            Assert.Null(listing.Continuation);
            phase = "source.read";
            var read = await runtime.ReadTextRangeAsync(new(callerRelative, 0, 512));
            output.WriteLine("phase={0}; status={1}", phase, read.Outcome);
            Assert.Equal(PreparedSourceOutcome.Complete, read.Outcome);
            Assert.Equal(callerText, read.Text);
            phase = "source.search";
            var searched = await runtime.SearchTextAsync(new("src/main/java/fixture", "targetMethod", 1));
            output.WriteLine("phase={0}; status={1}; matches={2}", phase, searched.Outcome, searched.Matches.Count);
            Assert.Equal(PreparedSourceOutcome.Complete, searched.Outcome);
            Assert.Contains(searched.Matches, match => match.Location.RelativePath == callerRelative);
            Assert.Contains(searched.Matches, match => match.Location.RelativePath == targetRelative);
            Assert.Null(searched.Continuation);

            phase = "codegraph.first";
            var firstExplore = await runtime.ExploreAsync("OctoberNativeBeacon LocateOctober", 4);
            output.WriteLine("phase={0}; status={1}; issue={2}; excerpts={3}", phase, firstExplore.Status, firstExplore.IssueCode, firstExplore.Excerpts.Count);
            Assert.Equal(CodeGraphToolInvocationStatus.Complete, firstExplore.Status);
            Assert.Contains(firstExplore.Excerpts, excerpt => excerpt.Location.RelativePath.Equals("Beacon.cs", StringComparison.OrdinalIgnoreCase) && excerpt.Text.Contains("LocateOctober", StringComparison.Ordinal));
            Assert.Single(descriptor.NativeCompletions);
            phase = "jdt.definition";
            output.WriteLine("phase={0}; startMs={1}; existingLeases={2}", phase, elapsed.ElapsedMilliseconds, runtime.JdtLeases.Count);
            var definition = await runtime.NavigateAsync(PreparedJavaNavigationOperation.Definition,
                new(callerRelative, 3, callerText.Split('\n')[2].IndexOf("targetMethod", StringComparison.Ordinal) + 1, 10));
            output.WriteLine("phase={0}; endMs={1}; outcome={2}; locations={3}; leases={4}", phase, elapsed.ElapsedMilliseconds, definition.Outcome, definition.Locations.Count, DescribeLeases(runtime));
            Assert.True(definition.Outcome == PreparedJavaNavigationOutcome.Observed, $"Default 30-second JDT definition failed: {definition.Outcome}");
            Assert.Contains(definition.Locations, location => location.Source.RepositoryId == fixture.Approval.RepositoryId && location.Source.RelativePath == targetRelative && location.StartLine == 3);
            phase = "jdt.references";
            output.WriteLine("phase={0}; startMs={1}; existingLeases={2}", phase, elapsed.ElapsedMilliseconds, runtime.JdtLeases.Count);
            var references = await runtime.NavigateAsync(PreparedJavaNavigationOperation.References,
                new(targetRelative, 3, targetText.Split('\n')[2].IndexOf("targetMethod", StringComparison.Ordinal) + 1, 10));
            output.WriteLine("phase={0}; endMs={1}; outcome={2}; locations={3}; leases={4}", phase, elapsed.ElapsedMilliseconds, references.Outcome, references.Locations.Count, DescribeLeases(runtime));
            Assert.True(references.Outcome == PreparedJavaNavigationOutcome.Observed, $"Default 30-second JDT references failed: {references.Outcome}");
            Assert.Contains(references.Locations, location => location.Source.RepositoryId == fixture.Approval.RepositoryId && location.Source.RelativePath == callerRelative && location.StartLine == 3);
            phase = "codegraph.second";
            var secondExplore = await runtime.ExploreAsync("OctoberNativeBeacon LocateOctober", 4);
            output.WriteLine("phase={0}; status={1}; issue={2}; excerpts={3}", phase, secondExplore.Status, secondExplore.IssueCode, secondExplore.Excerpts.Count);
            Assert.Equal(CodeGraphToolInvocationStatus.Complete, secondExplore.Status);
            Assert.Same(descriptor, runtime.PreparedConnection);
            Assert.Equal(2, descriptor.NativeCompletions.Count);
            Assert.All(secondExplore.Excerpts, excerpt => Assert.Equal(fixture.Approval.RepositoryId, excerpt.Location.RepositoryId));

            phase = "cancellation-and-revocation";
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            var cancelledRead = await runtime.ReadTextRangeAsync(new(callerRelative, 0, 512), cancelled.Token);
            Assert.Equal(PreparedSourceOutcome.Cancelled, cancelledRead.Outcome);
            Assert.Null(cancelledRead.Text);
            fixture.Authority.Revoke();
            var denied = await runtime.ReadTextRangeAsync(new(callerRelative, 0, 512));
            Assert.Equal(PreparedSourceOutcome.Denied, denied.Outcome);
            Assert.Null(denied.Text);
            var deniedExplore = await runtime.ExploreAsync("OctoberNativeBeacon", 4);
            Assert.NotEqual(CodeGraphToolInvocationStatus.Complete, deniedExplore.Status);
            Assert.Empty(deniedExplore.Excerpts);

            phase = "normal-cleanup";
            output.WriteLine("phase={0}; elapsedMs={1}; leases={2}", phase, elapsed.ElapsedMilliseconds, DescribeLeases(runtime));
            await runtime.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            await VerifyCleanupAsync(runtime);
            cleanupConfirmed = true;
            Assert.True(runtime.JdtLeases.Count >= 2);
            Assert.Equal("preserve caller-owned state\n", await File.ReadAllTextAsync(marker));
            Assert.Equal(new[] { marker }, Directory.EnumerateFiles(stateRoot, "*", SearchOption.AllDirectories).ToArray());
            Assert.Empty(Directory.EnumerateDirectories(stateRoot));
            var after = await SnapshotSourceAsync(fixture.PreparedRootPath);
            Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
            foreach (var (path, bytes) in before)
                Assert.Equal(bytes, after[path]);
            Assert.Equal(originalPath, Environment.GetEnvironmentVariable("PATH"));
            output.WriteLine("Combined native proof: multi-page source list/search, contained read, JDT definition+reference, retained CodeGraph descriptor with two native sessions, revoked disclosure, pre-cancelled read, all leases/processes settled. No active-call cancellation or provider proof is claimed here.");
        }
        catch (Exception exception)
        {
            output.WriteLine("Primary qualification failure: phase={0}; elapsedMs={1}; type={2}; HResult=0x{3:X8}; leases={4}",
                phase, elapsed.ElapsedMilliseconds, exception.GetType().Name, exception.HResult, runtime is null ? "none" : DescribeLeases(runtime));
            throw;
        }
        finally
        {
            if (runtime is not null && !cleanupConfirmed)
            {
                try
                {
                    output.WriteLine("Failure cleanup begins: primaryPhase={0}; elapsedMs={1}; leases={2}", phase, elapsed.ElapsedMilliseconds, DescribeLeases(runtime));
                    await runtime.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
                    await VerifyCleanupAsync(runtime);
                    cleanupConfirmed = true;
                }
                catch
                {
                    output.WriteLine("Cleanup unconfirmed: primaryPhase={0}; elapsedMs={1}; leases={2}; retaining owned fixture={3}", phase, elapsed.ElapsedMilliseconds, DescribeLeases(runtime), fixtureRoot);
                    throw;
                }
            }
            if (runtime is null || cleanupConfirmed)
            {
                await fixture.DisposeAsync();
                Assert.False(Directory.Exists(fixtureRoot));
            }
        }
    }

    private static string DescribeLeases(OctoberToolRuntime runtime) => string.Join(",", runtime.JdtLeases.Select((lease, index) => $"{index}:{lease.Lifetime}/settled={lease.OwnedResourcesSettled}"));

    // Transparent lifecycle observation: all process operations delegate to the same real launcher/process.
    private sealed class LifecycleTraceLauncher(ITestOutputHelper output) : IPreparedJdtProcessLauncher
    {
        private int _sequence;
        public async Task<IJdtLsProcess> LaunchAsync(PreparedToolWorkspaceLease workspaceLease, PreparedJdtLaunchRequest request, CancellationToken cancellationToken)
        {
            var sequence = ++_sequence;
            var timer = Stopwatch.StartNew();
            Trace(output, $"JDT process {sequence}: launch begin; lease={workspaceLease.Lifetime}; cancelled={cancellationToken.IsCancellationRequested}");
            OctoberScanOwnedProcessObservation? observation = null;
            var launcher = new PreparedJdtProcessLauncher(start =>
            {
                var actual = Process.Start(start);
                if (actual is not null)
                {
                    try { observation = new OctoberScanOwnedProcessObservation(actual, message => Trace(output, $"JDT process {sequence}: {message}")); }
                    catch (Exception exception) { Trace(output, $"JDT process {sequence}: ancestry observation unavailable; type={exception.GetType().Name}; HResult=0x{exception.HResult:X8}"); }
                }
                return actual;
            });
            try
            {
                var process = await launcher.LaunchAsync(workspaceLease, request, cancellationToken);
                Trace(output, $"JDT process {sequence}: launch returned; elapsedMs={timer.ElapsedMilliseconds}");
                return new LifecycleTraceProcess(process, output, sequence, Path.GetDirectoryName(request.DataPath)!, observation);
            }
            catch (Exception exception)
            {
                observation?.Dispose();
                Trace(output, $"JDT process {sequence}: launch failed; elapsedMs={timer.ElapsedMilliseconds}; type={exception.GetType().Name}; HResult=0x{exception.HResult:X8}");
                throw;
            }
        }
    }

    private sealed class LifecycleTraceProcess(IJdtLsProcess inner, ITestOutputHelper output, int sequence, string ownedStateRoot, OctoberScanOwnedProcessObservation? observation) : IJdtLsProcess
    {
        internal string DescribeState()
        {
            if (inner is not PreparedJdtOwnedProcess owned) return "process-observer=unavailable";
            try { return $"parentExited={owned.HasExited}; stderrPumpCompleted={owned.StderrPumpCompleted}"; }
            catch (InvalidOperationException) { return $"parentObserver=disposed; stderrPumpCompleted={owned.StderrPumpCompleted}"; }
        }
        public Stream StandardInput => inner.StandardInput;
        public Stream StandardOutput => inner.StandardOutput;
        public Task<string> ReadBoundedStandardErrorAsync(int byteLimit, CancellationToken cancellationToken) => inner.ReadBoundedStandardErrorAsync(byteLimit, cancellationToken);
        public async Task<int> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var timer = Stopwatch.StartNew();
            Trace(output, $"JDT process {sequence}: wait begin; timeoutMs={timeout.TotalMilliseconds}; cancelled={cancellationToken.IsCancellationRequested}; {DescribeState()}");
            try
            {
                var exit = await inner.WaitForExitAsync(timeout, cancellationToken);
                observation?.Report("after-parent-wait");
                Assert.NotNull(observation);
                Assert.True(observation.AllRetainedProcessesKernelSignaled, "Successful native wait must confirm kernel signals for the root and every captured descendant.");
                Trace(output, $"JDT process {sequence}: wait complete; elapsedMs={timer.ElapsedMilliseconds}; exit={exit}; {DescribeState()}");
                return exit;
            }
            catch (Exception exception)
            {
                observation?.Report("parent-wait-failed");
                Trace(output, $"JDT process {sequence}: wait failed; elapsedMs={timer.ElapsedMilliseconds}; type={exception.GetType().Name}; HResult=0x{exception.HResult:X8}; {DescribeState()}");
                throw;
            }
        }
        public async Task KillTreeAsync(CancellationToken cancellationToken)
        {
            try { observation?.CaptureBeforeKill(); }
            catch (Exception exception) { Trace(output, $"JDT ancestry capture failed without changing kill behavior: {exception.GetType().Name}"); }
            Trace(output, $"JDT process {sequence}: kill begin; cancelled={cancellationToken.IsCancellationRequested}; {DescribeState()}");
            try { await inner.KillTreeAsync(cancellationToken); Trace(output, $"JDT process {sequence}: kill complete"); }
            catch (Exception exception) { Trace(output, $"JDT process {sequence}: kill failed; type={exception.GetType().Name}; HResult=0x{exception.HResult:X8}"); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            Trace(output, $"JDT process {sequence}: dispose begin");
            try { await inner.DisposeAsync(); Trace(output, $"JDT process {sequence}: dispose complete"); }
            catch (Exception exception)
            {
                Trace(output, $"JDT process {sequence}: dispose failed; type={exception.GetType().Name}; HResult=0x{exception.HResult:X8}; ownedStateDiagnostic={SanitizeOwnedStateMessage(exception.Message, ownedStateRoot)}");
                foreach (var frame in new StackTrace(exception, fNeedFileInfo: true).GetFrames().Where(frame => frame.GetMethod()?.DeclaringType?.Namespace == "Vulgata.Infrastructure.Analysis.Java").Take(6))
                    Trace(output, $"JDT owned cleanup frame: {frame.GetMethod()!.DeclaringType!.Name}.{frame.GetMethod()!.Name} at {Path.GetFileName(frame.GetFileName())}:{frame.GetFileLineNumber()}");
                if (observation is not null)
                {
                    try { await observation.ObserveFailureAsync(); }
                    catch (Exception diagnostic) { Trace(output, $"JDT child observation failed without replacing cleanup failure: {diagnostic.GetType().Name}"); }
                }
                throw;
            }
            finally { observation?.Dispose(); }
        }
    }

    private sealed class LifecycleTraceProtocolFactory(ITestOutputHelper output) : IPreparedJdtProtocolFactory
    {
        private readonly PreparedJdtProtocolFactory _inner = new();
        private int _sequence;
        public async Task<IPreparedJdtLsSession> InitializeAsync(IJdtLsProcess process, PreparedJdtInitializeRequest request, CancellationToken cancellationToken)
        {
            var sequence = ++_sequence;
            var timer = Stopwatch.StartNew();
            Trace(output, $"JDT protocol {sequence}: initialize begin; {ProcessState(process)}");
            try
            {
                var session = await _inner.InitializeAsync(process, request, cancellationToken);
                Trace(output, $"JDT protocol {sequence}: initialize complete; elapsedMs={timer.ElapsedMilliseconds}; {ProcessState(process)}");
                return session is IPreparedJdtGracefulShutdownSession graceful
                    ? new LifecycleTraceGracefulSession(graceful, output, sequence, process)
                    : new LifecycleTraceSession(session, output, sequence, process);
            }
            catch (Exception exception)
            {
                Trace(output, $"JDT protocol {sequence}: initialize failed; elapsedMs={timer.ElapsedMilliseconds}; type={exception.GetType().Name}; HResult=0x{exception.HResult:X8}");
                throw;
            }
        }
    }

    private class LifecycleTraceSession(IPreparedJdtLsSession inner, ITestOutputHelper output, int sequence, IJdtLsProcess process) : IPreparedJdtLsSession
    {
        public async Task<PreparedJdtLsRawReply> NavigateAsync(PreparedJavaNavigationOperation operation, PreparedJavaNavigationRequest request, CancellationToken cancellationToken = default)
        {
            var timer = Stopwatch.StartNew();
            Trace(output, $"JDT protocol {sequence}: navigation begin; operation={operation}; cancelled={cancellationToken.IsCancellationRequested}; {ProcessState(process)}");
            try
            {
                var result = await inner.NavigateAsync(operation, request, cancellationToken);
                Trace(output, $"JDT protocol {sequence}: navigation complete; elapsedMs={timer.ElapsedMilliseconds}; locations={result.Locations.Count}; limited={result.LimitReached}; {ProcessState(process)}");
                return result;
            }
            catch (Exception exception)
            {
                Trace(output, $"JDT protocol {sequence}: navigation failed; elapsedMs={timer.ElapsedMilliseconds}; type={exception.GetType().Name}; HResult=0x{exception.HResult:X8}");
                throw;
            }
        }
        public async ValueTask DisposeAsync()
        {
            var timer = Stopwatch.StartNew();
            Trace(output, $"JDT protocol {sequence}: dispose begin; exitSent={(inner as IPreparedJdtGracefulShutdownSession)?.ExitSent}");
            try
            {
                await inner.DisposeAsync();
                Trace(output, $"JDT protocol {sequence}: dispose complete; elapsedMs={timer.ElapsedMilliseconds}; exitSent={(inner as IPreparedJdtGracefulShutdownSession)?.ExitSent}");
            }
            catch (Exception exception)
            {
                Trace(output, $"JDT protocol {sequence}: dispose failed; elapsedMs={timer.ElapsedMilliseconds}; exitSent={(inner as IPreparedJdtGracefulShutdownSession)?.ExitSent}; type={exception.GetType().Name}; HResult=0x{exception.HResult:X8}");
                throw;
            }
        }
    }

    // Preserve the exact graceful-shutdown capability used by the real runtime's settle branch.
    private sealed class LifecycleTraceGracefulSession(IPreparedJdtGracefulShutdownSession inner, ITestOutputHelper output, int sequence, IJdtLsProcess process)
        : LifecycleTraceSession(inner, output, sequence, process), IPreparedJdtGracefulShutdownSession
    {
        public bool ExitSent => inner.ExitSent;
    }
    private static string SanitizeOwnedStateMessage(string message, string ownedStateRoot)
    {
        var relative = message.Replace(Path.GetFullPath(ownedStateRoot), "<owned-state>", StringComparison.OrdinalIgnoreCase);
        // Only synthetic owned-state names are diagnostic output; redact any remaining absolute Windows path.
        relative = System.Text.RegularExpressions.Regex.Replace(relative, @"[A-Za-z]:\\[^'\r\n]*", "<outside-owned-state>");
        return relative.Length <= 512 ? relative : relative[..512];
    }
    private static string ProcessState(IJdtLsProcess process) => process is LifecycleTraceProcess traced ? traced.DescribeState() : "process-observer=unavailable";

    private static void Trace(ITestOutputHelper output, string message)
    {
        // Diagnostic output must never alter process settlement if a late cleanup outlives the test reporter.
        try { output.WriteLine(message); }
        catch (InvalidOperationException) { }
    }
    private async Task VerifyCleanupAsync(OctoberToolRuntime runtime)
    {
        if (runtime.PreparedConnection is { } descriptor)
        {
            foreach (var completion in descriptor.NativeCompletions)
            {
                var exit = Assert.IsType<StdioClientCompletionDetails>(await completion.WaitAsync(TimeSpan.FromSeconds(3)));
                Assert.NotNull(exit.ProcessId);
                Assert.NotNull(exit.ExitCode);
                output.WriteLine("Owned CodeGraph session settled: PID={0}, exit={1}.", exit.ProcessId, exit.ExitCode);
            }
        }
        Assert.All(runtime.JdtLeases, lease => Assert.True(lease.OwnedResourcesSettled,
            "Each dedicated JDT lease must confirm owned resource settlement before fixture deletion."));
    }
    private static PreparedJdtHostPolicy CreatePinnedHostPolicy()
    {
        var localBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
        Assert.True(File.Exists(Path.Combine(localBin, "jdtls.bat")));
        Assert.True(File.Exists(Path.Combine(localBin, "python.exe")));
        var javaBin = Environment.GetEnvironmentVariable("VULGATA_JDT_JAVA_BIN") ?? string.Empty;
        Assert.True(Path.IsPathFullyQualified(javaBin), "Caller must set the authorized physical JDK bin in this runner process only.");
        javaBin = Path.GetFullPath(javaBin);
        Assert.True(File.Exists(Path.Combine(javaBin, "java.exe")));
        Assert.False((File.GetAttributes(javaBin) & FileAttributes.ReparsePoint) != 0);
        var javaRoot = Directory.GetParent(javaBin)!.FullName;
        Assert.False((File.GetAttributes(javaRoot) & FileAttributes.ReparsePoint) != 0);
        Assert.True(Directory.Exists(JdtRuntimeRoot));
        var commandInterpreter = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        string[] directories = [localBin, javaBin, Path.GetDirectoryName(commandInterpreter)!];
        return new PreparedJdtHostPolicy(directories, [JdtRuntimeRoot, javaRoot], commandInterpreter, directories, JavaBinDirectory: javaBin);
    }

    private static async Task<Dictionary<string, byte[]>> SnapshotSourceAsync(string root)
    {
        var snapshot = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (!relative.StartsWith(".codegraph/", StringComparison.Ordinal))
                snapshot.Add(relative, await File.ReadAllBytesAsync(file));
        }
        return snapshot;
    }

    private sealed class FixtureToolAuthorizer(PreparedSourceApproval expected) : IPreparedToolUseAuthorizer
    {
        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(PreparedSourceApproval approval, PreparedToolPurpose toolPurpose, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PreparedToolUseAuthorityResult(approval == expected && toolPurpose is PreparedToolPurpose.JdtNavigation or PreparedToolPurpose.CodeGraphPreparation ? PreparedToolUseAuthorityStatus.Current : PreparedToolUseAuthorityStatus.Denied));
        }
    }
}
