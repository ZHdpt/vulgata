using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Source;
using Vulgata.Tests.PreparedSource;
using Vulgata.Tests.TestInfrastructure;
using Xunit.Abstractions;

namespace Vulgata.Tests.AnalysisRuntime;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class PreparedJdtLsDedicatedQualificationTests(ITestOutputHelper output)
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenDedicatedAsync_WithPinnedHostJdt_ReturnsUsefulFunctionResultsAndSettlesOwnedProcess()
    {
        PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        bool preserveFixture = true;
        Exception? primaryFailure = null;
        try
        {
            string callerRelativePath = "src/main/java/fixture/Caller.java";
            string targetRelativePath = "src/main/java/fixture/Target.java";
            string callerPath = Path.Combine(fixture.PreparedRootPath, callerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string targetPath = Path.Combine(fixture.PreparedRootPath, targetRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string projectPath = Path.Combine(fixture.PreparedRootPath, ".project");
            string classpathPath = Path.Combine(fixture.PreparedRootPath, ".classpath");
            string compilerSettingsPath = Path.Combine(
                fixture.PreparedRootPath, ".settings", "org.eclipse.jdt.core.prefs");
            string gitHeadPath = Path.Combine(fixture.PreparedRootPath, ".git", "HEAD");
            string gitConfigPath = Path.Combine(fixture.PreparedRootPath, ".git", "config");
            string hookPath = Path.Combine(fixture.PreparedRootPath, ".git", "hooks", "pre-commit");

            string callerText =
                "package fixture;\n" +
                "public class Caller {\n" +
                "    public void call() { new Target().targetMethod(); }\n" +
                "}\n";
            string targetText =
                "package fixture;\n" +
                "public class Target {\n" +
                "    public void targetMethod() {}\n" +
                "}\n";
            string[] sourceDirectories = [Path.GetDirectoryName(callerPath)!, Path.GetDirectoryName(targetPath)!];
            foreach (string directory in sourceDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(directory);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(compilerSettingsPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(hookPath)!);

            await File.WriteAllTextAsync(callerPath, callerText, Utf8);
            await File.WriteAllTextAsync(targetPath, targetText, Utf8);
            await File.WriteAllTextAsync(projectPath, """
                <?xml version="1.0" encoding="UTF-8"?>
                <projectDescription>
                  <name>story18-2-dedicated-fixture</name>
                  <buildSpec><buildCommand><name>org.eclipse.jdt.core.javabuilder</name></buildCommand></buildSpec>
                  <natures><nature>org.eclipse.jdt.core.javanature</nature></natures>
                </projectDescription>
                """, Utf8);
            await File.WriteAllTextAsync(classpathPath, """
                <?xml version="1.0" encoding="UTF-8"?>
                <classpath>
                  <classpathentry kind="src" path="src/main/java"/>
                  <classpathentry kind="con" path="org.eclipse.jdt.launching.JRE_CONTAINER"/>
                  <classpathentry kind="output" path="bin"/>
                </classpath>
                """, Utf8);
            await File.WriteAllTextAsync(compilerSettingsPath, """
                eclipse.preferences.version=1
                org.eclipse.jdt.core.compiler.source=21
                org.eclipse.jdt.core.compiler.compliance=21
                org.eclipse.jdt.core.compiler.codegen.targetPlatform=21
                """, Utf8);
            await File.WriteAllTextAsync(gitHeadPath, "ref: refs/heads/fixture\n", Utf8);
            await File.WriteAllTextAsync(gitConfigPath,
                "[core]\n\trepositoryformatversion = 0\n\tbare = false\n", Utf8);
            await File.WriteAllTextAsync(hookPath, "# marker: JDT qualification must not alter or invoke this hook\n", Utf8);

            string[] protectedPaths =
            [callerPath, targetPath, projectPath, classpathPath, compilerSettingsPath,
                gitHeadPath, gitConfigPath, hookPath];
            byte[][] protectedBytes = await Task.WhenAll(
                protectedPaths.Select(path => File.ReadAllBytesAsync(path)));
            IReadOnlyDictionary<string, byte[]> sourceTreeBefore = await SnapshotFilesAsync(fixture.PreparedRootPath);

            string stateRoot = Path.Combine(fixture.SiblingPath, "dedicated-jdt-state");
            Directory.CreateDirectory(stateRoot);
            string callerStateMarker = Path.Combine(stateRoot, "caller-owned-marker.txt");
            await File.WriteAllTextAsync(callerStateMarker, "preserve caller-owned state\n", Utf8);
            var (hostPath, hostPolicy, runtimeRoot, javaPath, pythonPath, jdtCommandPath) =
                CreateCurrentHostPolicy();
            Assert.Equal(hostPath, Environment.GetEnvironmentVariable("PATH"));

            using (var preflightDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
            {
                PreparedJdtCommandResolution preflight = await new PreparedJdtCommandResolver().ResolveAsync(
                    new PreparedJdtCommandRequest(
                        hostPath,
                        fixture.PreparedRootPath,
                        stateRoot,
                        hostPolicy),
                    preflightDeadline.Token);
                int pinCount = preflight.Commands is { } commands
                    ? new[]
                        {
                            commands.JdtCommand,
                            commands.JavaCommand,
                            commands.FixedInterpreter,
                            commands.PythonCommand,
                            commands.BootstrapScript,
                        }.Count(pin => pin is not null) + (commands.RuntimeInputs?.Count ?? 0)
                    : 0;
                output.WriteLine("Read-only current-host resolver preflight: {0}; pinned input count={1}.",
                    preflight.Status, pinCount);
                if (preflight.Status != PreparedJdtCommandResolutionStatus.Pinned)
                {
                    string bootstrap = Regex.Match(
                        File.ReadAllText(jdtCommandPath),
                        "(?im)^\\s*python\\s+\\\"(?<bootstrap>[^\\\"]+)\\\"\\s+%")
                        .Groups["bootstrap"].Value;
                    string runtimeBin = Path.GetDirectoryName(bootstrap)!;
                    string plugin = Directory.GetFiles(
                        Path.Combine(runtimeRoot, "plugins"), "org.eclipse.equinox.launcher_*.jar").Single();
                    string jdtServer = Directory.GetFiles(
                        Path.Combine(runtimeRoot, "plugins"), "org.eclipse.jdt.ls.core_*.jar").FirstOrDefault() ??
                        Path.Combine(runtimeRoot, "plugins", "org.eclipse.jdt.ls.core_*.jar");
                    foreach (string path in new[]
                    {
                        jdtCommandPath,
                        pythonPath,
                        javaPath,
                        hostPolicy.FixedCmdAbsolutePath,
                        bootstrap,
                        Path.Combine(runtimeBin, "jdtls.py"),
                        Path.Combine(runtimeRoot, "config_win", "config.ini"),
                        plugin,
                        jdtServer,
                        javaPath,
                    })
                    {
                        output.WriteLine("No-follow executable/runtime input {0}: {1}.",
                            path, TryDescribeInput(path));
                    }

                    DirectoryInfo javaBin = new(Path.GetDirectoryName(javaPath)!);
                    output.WriteLine("Configured Java bin attributes={0}; linkTarget={1}.",
                        javaBin.Attributes, javaBin.LinkTarget ?? "<none>");
                }

                Assert.Equal(PreparedJdtCommandResolutionStatus.Pinned, preflight.Status);
                Assert.True(pinCount > 0, "The resolver must verify the approved first-PATH launch chain and runtime inputs.");
            }

            fixture.Authority.Approve(fixture.Approval);
            var toolAuthorization = new FixtureToolUseAuthorizer(fixture.Approval);
            PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
                fixture.Authority,
                toolAuthorization).IssueAsync(
                    new PreparedToolWorkspaceGrantRequest(
                        fixture.Approval,
                        fixture.PreparedRootPath,
                        PreparedToolPurpose.JdtNavigation,
                        TimeSpan.FromSeconds(30)));
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
            PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

            var profile = new PreparedJavaNavigationProfile(
                stateRoot,
                TimeSpan.FromSeconds(20),
                TimeSpan.FromSeconds(6),
                TimeSpan.FromSeconds(15),
                MaximumResponseBytes: 2 * 1024 * 1024);
            var factory = new PreparedJavaNavigationContextFactory(profile, hostPolicy);
            IPreparedJavaNavigationContext? context = null;
            Process? unrelatedSentinel = null;
            bool contextSettled = false;
            bool openAttemptCompleted = false;
            PreparedJavaNavigationOpenResult? opened = null;
            Exception? cleanupFailure = null;

            try
            {
                unrelatedSentinel = StartUnrelatedWaitingProcess(hostPolicy.FixedCmdAbsolutePath);
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                Assert.False(unrelatedSentinel.HasExited,
                    "The unrelated test-owned sibling process must be alive before JDT starts.");

                opened = await factory.OpenDedicatedAsync(lease)
                    .WaitAsync(profile.InitializationTimeout + TimeSpan.FromSeconds(10));
                openAttemptCompleted = true;
                Assert.True(opened.Status == PreparedJavaNavigationOpenStatus.Opened,
                    $"The configured first-PATH JDT server must initialize on the complete prepared root; status={opened.Status}, " +
                    $"jdt={jdtCommandPath}, java={javaPath}, python={pythonPath}, runtime={runtimeRoot}.");
                context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);

                AIFunction[] functions = new PreparedJavaNavigationFunctions(context).Create().ToArray();
                Assert.Equal(
                    new[] { "navigate_java_definition", "navigate_java_references" },
                    functions.Select(function => function.Name));

                string callerLine = callerText.Split('\n')[2];
                string targetLine = targetText.Split('\n')[2];
                AIFunctionArguments definitionArguments = Arguments(
                    callerRelativePath,
                    line: 3,
                    character: callerLine.IndexOf("targetMethod", StringComparison.Ordinal) + 1);
                object? rawDefinition = await functions[0].InvokeAsync(definitionArguments, CancellationToken.None)
                    .AsTask().WaitAsync(profile.RequestTimeout + TimeSpan.FromSeconds(5));
                PreparedJavaNavigationResult definition = Assert.IsType<PreparedJavaNavigationResult>(rawDefinition);
                Assert.Equal(PreparedJavaNavigationOutcome.Observed, definition.Outcome);
                Assert.Contains(definition.Locations, location =>
                    location.Source.RepositoryId == fixture.Approval.RepositoryId &&
                    location.Source.RelativePath == targetRelativePath &&
                    location.StartLine == 3);

                AIFunctionArguments referenceArguments = Arguments(
                    targetRelativePath,
                    line: 3,
                    character: targetLine.IndexOf("targetMethod", StringComparison.Ordinal) + 1);
                object? rawReferences = await functions[1].InvokeAsync(referenceArguments, CancellationToken.None)
                    .AsTask().WaitAsync(profile.RequestTimeout + TimeSpan.FromSeconds(5));
                PreparedJavaNavigationResult references = Assert.IsType<PreparedJavaNavigationResult>(rawReferences);
                Assert.Equal(PreparedJavaNavigationOutcome.Observed, references.Outcome);
                Assert.Contains(references.Locations, location =>
                    location.Source.RepositoryId == fixture.Approval.RepositoryId &&
                    location.Source.RelativePath == callerRelativePath &&
                    location.StartLine == 3);

                output.WriteLine("Real JDT-LS navigation observed through the public host-policy factory; awaiting owned-process and fixture-settlement verification.");
                output.WriteLine("Host PATH chain: JDT command {0}; Python {1}; Java {2}; runtime {3}.",
                    jdtCommandPath, pythonPath, javaPath, runtimeRoot);
                string[] serverBundles = Directory.GetFiles(
                    Path.Combine(runtimeRoot, "plugins"), "org.eclipse.jdt.ls.core_*.jar");
                output.WriteLine("Server bundle(s): {0}.", string.Join(", ", serverBundles.Select(Path.GetFileName)));
                output.WriteLine("Settings contract: Maven/Gradle import, autobuild, build-configuration update, source downloads, snapshots and dependency restoration disabled; this is not OS network isolation.");
                output.WriteLine("Definition: {0}:{1}; references: {2}:{3}.",
                    targetRelativePath,
                    Assert.Single(definition.Locations.Where(location => location.Source.RelativePath == targetRelativePath)).StartLine,
                    callerRelativePath,
                    Assert.Single(references.Locations.Where(location => location.Source.RelativePath == callerRelativePath)).StartLine);
            }
            catch (Exception exception)
            {
                primaryFailure = exception;
                throw;
            }
            finally
            {
                try
                {
                    if (context is not null)
                    {
                        try
                        {
                            await context.DisposeAsync().AsTask().WaitAsync(profile.ShutdownTimeout + TimeSpan.FromSeconds(5));
                            contextSettled = true;
                        }
                        catch (Exception exception)
                        {
                            cleanupFailure = exception;
                            output.WriteLine("Owned navigation context disposal did not confirm settlement: {0}: {1}",
                                exception.GetType().FullName, exception.Message);
                        }
                    }

                    bool repositoryQuarantined = IsRepositorySlotQuarantined(lease.RepositoryId);
                    if (repositoryQuarantined)
                    {
                        cleanupFailure ??= new InvalidOperationException("jdt_context_slot_quarantined_after_disposal");
                        output.WriteLine("The owned Repository slot remains quarantined after context disposal.");
                    }

                    if (unrelatedSentinel is not null)
                    {
                        bool sentinelSurvivedJdtShutdown = contextSettled && !repositoryQuarantined && !unrelatedSentinel.HasExited;
                        if (!unrelatedSentinel.HasExited)
                        {
                            unrelatedSentinel.Kill(entireProcessTree: true);
                            await unrelatedSentinel.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                        }

                        unrelatedSentinel.Dispose();
                        if (contextSettled && !repositoryQuarantined)
                        {
                            if (!sentinelSurvivedJdtShutdown)
                            {
                                cleanupFailure ??= new InvalidOperationException(
                                    "jdt_shutdown_affected_unrelated_sentinel");
                            }
                        }
                    }

                    if (contextSettled && !repositoryQuarantined && cleanupFailure is null)
                    {
                        try
                        {
                            Assert.Equal("preserve caller-owned state\n", await File.ReadAllTextAsync(callerStateMarker));
                            Assert.Equal(new[] { callerStateMarker }, Directory.EnumerateFiles(stateRoot).ToArray());
                            Assert.Empty(Directory.EnumerateDirectories(stateRoot));
                            await AssertFileTreeUnchangedAsync(fixture.PreparedRootPath, sourceTreeBefore);
                            for (int i = 0; i < protectedPaths.Length; i++)
                            {
                                Assert.Equal(protectedBytes[i], await File.ReadAllBytesAsync(protectedPaths[i]));
                            }

                            output.WriteLine("Shutdown completed within {0}; owned state removal followed the launcher's process-exited check, and only the preexisting caller marker remains.",
                                profile.ShutdownTimeout + TimeSpan.FromSeconds(5));
                            output.WriteLine("All fixture source, Eclipse configuration and hook marker bytes are unchanged.");
                        }
                        catch (Exception exception)
                        {
                            cleanupFailure = exception;
                            output.WriteLine("Post-shutdown source/state verification failed: {0}: {1}",
                                exception.GetType().FullName, exception.Message);
                        }
                    }
                    else
                    {
                        cleanupFailure ??= new InvalidOperationException(
                            openAttemptCompleted ? "jdt_cleanup_unconfirmed" : "jdt_open_cleanup_unverified");
                    }
                }
                catch (Exception exception)
                {
                    cleanupFailure ??= exception;
                    output.WriteLine("Owned JDT teardown diagnostic failed: {0}: {1}",
                        exception.GetType().FullName, exception.Message);
                }

                try
                {
                    await lease.DisposeAsync();
                }
                catch (Exception exception)
                {
                    cleanupFailure ??= exception;
                    output.WriteLine("Prepared workspace lease disposal failed: {0}: {1}",
                        exception.GetType().FullName, exception.Message);
                }

                if (cleanupFailure is not null)
                {
                    preserveFixture = true;
                    output.WriteLine("Cleanup remains unconfirmed; preserving fixture at {0}.", fixture.SandboxPath);
                    ReportRemainingFiles(output, "prepared source", fixture.PreparedRootPath);
                    ReportRemainingFiles(output, "external JDT state", stateRoot);
                }

                preserveFixture = cleanupFailure is not null;
                if (cleanupFailure is not null && primaryFailure is null)
                {
                    throw new InvalidOperationException("jdt_qualification_cleanup_unconfirmed", cleanupFailure);
                }
            }
        }
        finally
        {
            if (preserveFixture)
            {
                output.WriteLine("Cleanup is unconfirmed or setup failed; preserving fixture at {0}.", fixture.SandboxPath);
                ReportRemainingFiles(output, "fixture", fixture.SandboxPath);
            }
            else
            {
                await fixture.DisposeAsync();
            }
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task DiagnoseCurrentJdtRuntimeFailure_ReportsBoundedOwnedProcessAndProtocolDetails()
    {
        PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedToolWorkspaceLease? lease = null;
        bool preserveFixture = false;
        try
        {
            string sourcePath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "fixture", "Probe.java");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            await File.WriteAllTextAsync(sourcePath,
                "package fixture;\npublic class Probe { public void inspect() {} }\n", Utf8);

            string fixtureMarker = Guid.NewGuid().ToString("N");
            string stateRoot = Path.Combine(fixture.SiblingPath, $"dedicated-jdt-diagnostic-state-{fixtureMarker}");
            Directory.CreateDirectory(stateRoot);
            string callerMarker = Path.Combine(stateRoot, "caller-owned-marker.txt");
            await File.WriteAllTextAsync(callerMarker, "preserve caller state\n", Utf8);
            var (hostPath, hostPolicy, runtimeRoot, javaPath, pythonPath, jdtCommandPath) =
                CreateCurrentHostPolicy();
            Assert.Equal(hostPath, Environment.GetEnvironmentVariable("PATH"));
            output.WriteLine("Diagnostic host selection: JDT {0}; Python {1}; Java {2}; pinned runtime root {3}.",
                jdtCommandPath, pythonPath, javaPath, runtimeRoot);

            fixture.Authority.Approve(fixture.Approval);
            PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
                fixture.Authority,
                new FixtureToolUseAuthorizer(fixture.Approval)).IssueAsync(
                    new PreparedToolWorkspaceGrantRequest(
                        fixture.Approval,
                        fixture.PreparedRootPath,
                        PreparedToolPurpose.JdtNavigation,
                        TimeSpan.FromSeconds(30)));
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
            lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

            var profile = new PreparedJavaNavigationProfile(
                stateRoot,
                TimeSpan.FromSeconds(20),
                TimeSpan.FromSeconds(6),
                TimeSpan.FromSeconds(15),
                MaximumResponseBytes: 2 * 1024 * 1024);
            var processLauncher = new DiagnosticPreparedJdtProcessLauncher(output, fixtureMarker);
            var protocols = new DiagnosticPreparedJdtProtocolFactory(output);
            var runtime = new PreparedJdtLsRuntime(
                new PreparedJdtCommandResolver(),
                processLauncher,
                protocols,
                hostPolicy);
            IPreparedJdtLsSession? session = null;
            try
            {
                session = await runtime.OpenAsync(lease, profile, CancellationToken.None);
                output.WriteLine("Diagnostic runtime initialization succeeded; opening session for bounded disposal.");
            }
            catch (PreparedJdtLsOpenFailureException failure)
            {
                output.WriteLine("Runtime open failure: status={0}; cleanupConfirmed={1}.",
                    failure.Status, failure.CleanupConfirmed);
            }
            finally
            {
                Exception? sessionDisposeFailure = null;
                if (session is not null)
                {
                    try
                    {
                        await session.DisposeAsync().AsTask().WaitAsync(
                            profile.ShutdownTimeout + TimeSpan.FromSeconds(5));
                    }
                    catch (Exception exception)
                    {
                        sessionDisposeFailure = exception;
                        output.WriteLine("Diagnostic session disposal failed: {0}: {1}",
                            exception.GetType().FullName, exception.Message);
                    }
                }

                DiagnosticPreparedJdtOwnedProcess? ownedProcess = processLauncher.OwnedProcess;
                if (ownedProcess is not null)
                {
                    await ownedProcess.ReportCapturedStderrAsync();
                    preserveFixture = !ownedProcess.CleanupConfirmed;
                    output.WriteLine("Owned-process result: started={0}; killRequested={1}; waitCompleted={2}; exitCode={3}; disposeCompleted={4}.",
                        ownedProcess.StartCompleted,
                        ownedProcess.KillRequested,
                        ownedProcess.ExitObserved,
                        ownedProcess.ExitCode?.ToString() ?? "<unobserved>",
                        ownedProcess.CleanupConfirmed);
                }

                if (preserveFixture)
                {
                    output.WriteLine("Cleanup was not confirmed; preserving diagnostic source/state at {0}.",
                        fixture.SandboxPath);
                    Assert.False(preserveFixture,
                        "Unconfirmed owned-process cleanup must preserve diagnostic files and fail this qualification.");
                }
                else
                {
                    Assert.Equal("preserve caller state\n", await File.ReadAllTextAsync(callerMarker));
                    Assert.Equal(new[] { callerMarker }, Directory.EnumerateFiles(stateRoot).ToArray());
                    Assert.Empty(Directory.EnumerateDirectories(stateRoot));
                    output.WriteLine("Confirmed diagnostic state cleanup left only the caller-owned marker.");
                }

                if (sessionDisposeFailure is not null)
                {
                    throw new InvalidOperationException("diagnostic_session_disposal_failed", sessionDisposeFailure);
                }
            }
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync();
            }

            if (!preserveFixture)
            {
                await fixture.DisposeAsync();
            }
        }
    }

    private static bool IsRepositorySlotQuarantined(Guid repositoryId)
    {
        FieldInfo slotsField = Assert.IsAssignableFrom<FieldInfo>(typeof(PreparedJavaNavigationContextFactory)
            .GetField("RepositorySlots", BindingFlags.Static | BindingFlags.NonPublic));
        System.Collections.IDictionary slots = Assert.IsAssignableFrom<System.Collections.IDictionary>(
            slotsField.GetValue(null));
        if (!slots.Contains(repositoryId))
        {
            return false;
        }

        object slot = Assert.IsAssignableFrom<object>(slots[repositoryId]);
        PropertyInfo quarantined = Assert.IsAssignableFrom<PropertyInfo>(slot.GetType().GetProperty(
            "Quarantined", BindingFlags.Instance | BindingFlags.NonPublic));
        return Assert.IsType<bool>(quarantined.GetValue(slot));
    }

    private static void ReportRemainingFiles(ITestOutputHelper output, string label, string root)
    {
        try
        {
            if (!Directory.Exists(root))
            {
                output.WriteLine("Remaining {0}: root absent ({1}).", label, root);
                return;
            }

            string[] directories = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .Take(40)
                .ToArray();
            string[] files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .Take(40)
                .ToArray();
            output.WriteLine("Remaining {0} under {1}: directories=[{2}]; files=[{3}].",
                label, root, string.Join(", ", directories), string.Join(", ", files));
        }
        catch (Exception exception)
        {
            output.WriteLine("Remaining {0} listing failed at {1}: {2}: {3}.",
                label, root, exception.GetType().FullName, exception.Message);
        }
    }

    private static AIFunctionArguments Arguments(string relativePath, int line, int character) =>
        new(new Dictionary<string, object?>
        {
            ["relativePath"] = relativePath,
            ["line"] = line,
            ["character"] = character,
            ["maximumLocations"] = 10,
        });

    private static async Task<IReadOnlyDictionary<string, byte[]>> SnapshotFilesAsync(string root)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
            files.Add(relativePath, await File.ReadAllBytesAsync(path));
        }

        return files;
    }

    private static async Task AssertFileTreeUnchangedAsync(
        string root,
        IReadOnlyDictionary<string, byte[]> expected)
    {
        IReadOnlyDictionary<string, byte[]> actual = await SnapshotFilesAsync(root);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach ((string relativePath, byte[] expectedBytes) in expected)
        {
            byte[] actualBytes = actual[relativePath];
            int firstDifference = FirstDifference(expectedBytes, actualBytes);
            if (firstDifference >= 0)
            {
                Assert.True(false,
                    $"Prepared source file '{relativePath}' changed at byte {firstDifference}: " +
                    $"expected {DescribeByte(expectedBytes, firstDifference)}, actual {DescribeByte(actualBytes, firstDifference)}; " +
                    $"expected length={expectedBytes.Length}, actual length={actualBytes.Length}.");
            }
        }
    }

    private static int FirstDifference(byte[] expected, byte[] actual)
    {
        int sharedLength = Math.Min(expected.Length, actual.Length);
        for (int index = 0; index < sharedLength; index++)
        {
            if (expected[index] != actual[index])
            {
                return index;
            }
        }

        return expected.Length == actual.Length ? -1 : sharedLength;
    }

    private static string DescribeByte(byte[] bytes, int index)
    {
        if (index >= bytes.Length)
        {
            return "<end-of-file>";
        }

        byte value = bytes[index];
        return value switch
        {
            0x09 => "tab (0x09)",
            0x0A => "line-feed (0x0A)",
            0x0D => "carriage-return (0x0D)",
            0x20 => "space (0x20)",
            >= 0x21 and <= 0x7E => $"'{(char)value}' (0x{value:X2})",
            _ => $"0x{value:X2}",
        };
    }

    private static Process StartUnrelatedWaitingProcess(string commandInterpreter)
    {
        var start = new ProcessStartInfo
        {
            FileName = commandInterpreter,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("/d");
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("set /p story18_2_wait=");
        return Process.Start(start) ?? throw new InvalidOperationException("test_sentinel_start_failed");
    }

    private sealed class DiagnosticPreparedJdtProcessLauncher(ITestOutputHelper output, string fixtureMarker) : IPreparedJdtProcessLauncher
    {
        private readonly PreparedJdtProcessLauncher _inner = new();

        internal DiagnosticPreparedJdtOwnedProcess? OwnedProcess { get; private set; }

        public async Task<IJdtLsProcess> LaunchAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJdtLaunchRequest request,
            CancellationToken cancellationToken)
        {
            output.WriteLine("Process launch requested: python={0}; bootstrap={1}; Java={2}; data={3}; configuration={4}.",
                request.Commands.PythonCommand?.CanonicalAbsolutePath,
                request.Commands.BootstrapScript?.CanonicalAbsolutePath,
                request.Commands.JavaCommand.CanonicalAbsolutePath,
                request.DataPath,
                request.ConfigurationPath);
            IJdtLsProcess process = await _inner.LaunchAsync(workspaceLease, request, cancellationToken);
            OwnedProcess = new DiagnosticPreparedJdtOwnedProcess(
                process, request.DataPath, request.ConfigurationPath, fixtureMarker, output);
            output.WriteLine("Process launch completed and an owned process handle was returned.");
            return OwnedProcess;
        }
    }

    private sealed class DiagnosticPreparedJdtProtocolFactory(ITestOutputHelper output) : IPreparedJdtProtocolFactory
    {
        private readonly PreparedJdtProtocolFactory _inner = new();

        public async Task<IPreparedJdtLsSession> InitializeAsync(
            IJdtLsProcess process,
            PreparedJdtInitializeRequest request,
            CancellationToken cancellationToken)
        {
            var timer = Stopwatch.StartNew();
            output.WriteLine("LSP initialize requested: root={0}; workspace={1}; settings={2}.",
                request.RootUri,
                request.WorkspaceFolderUri,
                JsonSerializer.Serialize(request.FixedSettings));
            try
            {
                IPreparedJdtLsSession session = await _inner.InitializeAsync(process, request, cancellationToken);
                output.WriteLine("LSP initialize succeeded after {0} ms.", timer.ElapsedMilliseconds);
                return session;
            }
            catch (Exception exception)
            {
                output.WriteLine("LSP initialize failed after {0} ms: {1}: {2}",
                    timer.ElapsedMilliseconds, exception.GetType().FullName, exception.Message);
                throw;
            }
        }
    }

    private sealed class DiagnosticPreparedJdtOwnedProcess(
        IJdtLsProcess inner,
        string dataPath,
        string configurationPath,
        string fixtureMarker,
        ITestOutputHelper output) : IJdtLsProcess
    {
        private readonly Process _parentProcess = GetParentProcess(inner);
        private readonly Task _stderrPump = GetStderrPump(inner);

        internal bool StartCompleted { get; } = true;

        internal bool KillRequested { get; private set; }

        internal bool ExitObserved { get; private set; }

        internal int? ExitCode { get; private set; }

        internal bool CleanupConfirmed { get; private set; }

        public Stream StandardInput => inner.StandardInput;

        public Stream StandardOutput => inner.StandardOutput;

        public Task<string> ReadBoundedStandardErrorAsync(int byteLimit, CancellationToken cancellationToken) =>
            inner.ReadBoundedStandardErrorAsync(byteLimit, cancellationToken);

        public async Task<int> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            await ReportOwnedProcessesAsync("before-wait");
            ReportParent("before-wait");
            try
            {
                int exitCode = await inner.WaitForExitAsync(timeout, cancellationToken);
                ExitObserved = true;
                ExitCode = exitCode;
                output.WriteLine("Owned process wait completed with code {0}.", exitCode);
                ReportParent("after-wait");
                await ReportOwnedProcessesAsync("after-wait");
                return exitCode;
            }
            catch (Exception exception)
            {
                output.WriteLine("Owned process wait failed: {0}: {1}; stderrPumpCompleted={2}.",
                    exception.GetType().FullName, exception.Message, _stderrPump.IsCompleted);
                ReportParent("wait-failed");
                await ReportOwnedProcessesAsync("wait-failed");
                throw;
            }
        }

        public async Task KillTreeAsync(CancellationToken cancellationToken)
        {
            KillRequested = true;
            ReportParent("before-kill");
            await ReportOwnedProcessesAsync("before-kill");
            await inner.KillTreeAsync(cancellationToken);
            output.WriteLine("Owned process tree termination request completed; stderrPumpCompleted={0}.",
                _stderrPump.IsCompleted);
            ReportParent("after-kill");
            await ReportOwnedProcessesAsync("after-kill");
        }

        public async ValueTask DisposeAsync()
        {
            ReportParent("before-dispose");
            await ReportOwnedProcessesAsync("before-dispose");
            await ReportCapturedStderrAsync();
            string logPath = Path.Combine(dataPath, ".metadata", ".log");
            string? logTail = await ReadFileTailAsync(logPath, maximumBytes: 64 * 1024);
            if (logTail is not null)
            {
                output.WriteLine("JDT workspace log tail ({0} bytes max):\n{1}", 64 * 1024, logTail);
            }

            string eclipseLockPath = Path.Combine(configurationPath,
                "org.eclipse.core.runtime", ".manager", ".tmp74.instance");
            output.WriteLine("Eclipse runtime lock at disposal: path={0}; exists={1}.",
                eclipseLockPath, File.Exists(eclipseLockPath));
            try
            {
                await inner.DisposeAsync();
                CleanupConfirmed = true;
                output.WriteLine("Owned process wrapper disposal and confirmed state cleanup completed.");
            }
            catch (Exception exception)
            {
                output.WriteLine("Owned process wrapper disposal failed: {0}: {1}.",
                    exception.GetType().FullName, exception.Message);
                PreparedJdtLsDedicatedQualificationTests.ReportRemainingFiles(output, "owned data", dataPath);
                PreparedJdtLsDedicatedQualificationTests.ReportRemainingFiles(output, "owned configuration", configurationPath);
                throw;
            }
            finally
            {
                ReportParent("after-dispose");
                await ReportOwnedProcessesAsync("after-dispose");
            }
        }

        internal async Task ReportCapturedStderrAsync()
        {
            string stderr = await inner.ReadBoundedStandardErrorAsync(64 * 1024, CancellationToken.None);
            output.WriteLine("Bounded owned-process stderr (up to 64 KiB):\n{0}",
                string.IsNullOrWhiteSpace(stderr) ? "<empty>" : stderr);
        }

        private void ReportParent(string phase)
        {
            try
            {
                output.WriteLine("Python parent {0}: pid={1}; hasExited={2}; stderrPumpCompleted={3}.",
                    phase, _parentProcess.Id, _parentProcess.HasExited, _stderrPump.IsCompleted);
            }
            catch (Exception exception)
            {
                output.WriteLine("Python parent {0} status unavailable: {1}: {2}.",
                    phase, exception.GetType().FullName, exception.Message);
            }
        }

        private async Task ReportOwnedProcessesAsync(string phase)
        {
            try
            {
                string json = await ReadFixtureProcessesAsync(fixtureMarker);
                output.WriteLine("Java/Python processes for fixture {0} at {1}: {2}",
                    fixtureMarker, phase, string.IsNullOrWhiteSpace(json) ? "<none>" : json);
            }
            catch (Exception exception)
            {
                output.WriteLine("Owned Java/Python process query failed at {0}: {1}: {2}.",
                    phase, exception.GetType().FullName, exception.Message);
            }
        }

        private static Process GetParentProcess(IJdtLsProcess process)
        {
            FieldInfo field = Assert.IsAssignableFrom<FieldInfo>(typeof(PreparedJdtOwnedProcess).GetField(
                "_process", BindingFlags.Instance | BindingFlags.NonPublic));
            return Assert.IsType<Process>(field.GetValue(process));
        }

        private static Task GetStderrPump(IJdtLsProcess process)
        {
            FieldInfo field = Assert.IsAssignableFrom<FieldInfo>(typeof(PreparedJdtOwnedProcess).GetField(
                "_stderrPump", BindingFlags.Instance | BindingFlags.NonPublic));
            return Assert.IsAssignableFrom<Task>(field.GetValue(process));
        }

        private static async Task<string> ReadFixtureProcessesAsync(string fixtureMarker)
        {
            string query = $"SELECT ProcessId,Name,CommandLine FROM Win32_Process WHERE " +
                $"(Name='java.exe' OR Name='python.exe') AND CommandLine LIKE '%{fixtureMarker}%'";
            string script = "$q=[Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('" +
                Convert.ToBase64String(Encoding.Unicode.GetBytes(query)) +
                "')); Get-CimInstance -Query $q | Select-Object ProcessId,Name,CommandLine | ConvertTo-Json -Compress";
            string encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var start = new ProcessStartInfo
            {
                FileName = "pwsh",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-EncodedCommand");
            start.ArgumentList.Add(encodedScript);
            using Process queryProcess = Process.Start(start) ??
                throw new InvalidOperationException("owned_process_cim_query_start_failed");
            Task<string> stdout = queryProcess.StandardOutput.ReadToEndAsync();
            Task<string> stderr = queryProcess.StandardError.ReadToEndAsync();
            try
            {
                await queryProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                queryProcess.Kill(entireProcessTree: true);
                await queryProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
                throw new TimeoutException("owned_process_cim_query_timeout");
            }

            string result = await stdout.WaitAsync(TimeSpan.FromSeconds(1));
            string error = await stderr.WaitAsync(TimeSpan.FromSeconds(1));
            if (queryProcess.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"owned_process_cim_query_failed:{queryProcess.ExitCode}:{error.Trim()}");
            }

            return result.Length <= 16 * 1024 ? result : result[..(16 * 1024)];
        }

        private static async Task<string?> ReadFileTailAsync(string path, int maximumBytes)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long start = Math.Max(0, stream.Length - maximumBytes);
            stream.Position = start;
            byte[] bytes = new byte[(int)(stream.Length - start)];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = await stream.ReadAsync(bytes.AsMemory(offset));
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            return Utf8.GetString(bytes, 0, offset);
        }
    }

    private static (string HostPath, PreparedJdtHostPolicy Policy, string RuntimeRoot,
        string JavaPath, string PythonPath, string JdtCommandPath) CreateCurrentHostPolicy()
    {
        string hostPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] entries = hostPath.Split(Path.PathSeparator);
        string jdtCommandPath = FindFirstOnPath(entries, "jdtls.bat");
        string pythonPath = FindFirstOnPath(entries, "python.exe");
        string approvedJdtCommand = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "jdtls.bat"));
        string approvedPython = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "python.exe"));
        Assert.Equal(approvedJdtCommand, jdtCommandPath);
        Assert.Equal(approvedPython, pythonPath);
        string javaBinDirectory = Environment.GetEnvironmentVariable("VULGATA_JDT_JAVA_BIN") ?? string.Empty;
        Assert.True(Path.IsPathFullyQualified(javaBinDirectory),
            "The test caller must supply the human-approved physical Java bin directory.");
        javaBinDirectory = Path.GetFullPath(javaBinDirectory);
        Assert.Equal("bin", Path.GetFileName(javaBinDirectory.TrimEnd(Path.DirectorySeparatorChar)),
            ignoreCase: true);
        Assert.True(Directory.Exists(javaBinDirectory));
        Assert.True((File.GetAttributes(javaBinDirectory) & FileAttributes.ReparsePoint) == 0,
            "The configured Java bin itself must be the resolved physical directory.");
        string javaRuntimeRoot = Directory.GetParent(javaBinDirectory)?.FullName ??
            throw new InvalidOperationException("configured_java_runtime_root_unavailable");
        Assert.True(Directory.Exists(javaRuntimeRoot));
        Assert.True((File.GetAttributes(javaRuntimeRoot) & FileAttributes.ReparsePoint) == 0,
            "The configured Java runtime root must be physical.");
        string javaPath = Path.Combine(javaBinDirectory, "java.exe");
        Assert.True(File.Exists(javaPath), "The supplied Java 21 bin must contain java.exe.");
        string? bootstrapPath = Regex.Match(
            File.ReadAllText(jdtCommandPath),
            "(?im)^\\s*python\\s+\\\"(?<bootstrap>[^\\\"]+)\\\"\\s+%")
            .Groups["bootstrap"].Value;
        Assert.False(string.IsNullOrWhiteSpace(bootstrapPath),
            "The first host-PATH JDT command must be the human-approved Python bootstrap shape.");
        string runtimeRoot = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(bootstrapPath!)!, ".."));
        Assert.Equal(
            Path.GetFullPath(@"G:\source\repos\eclipse.jdt.ls\org.eclipse.jdt.ls.product\target\repository"),
            runtimeRoot);
        string cmdPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        Assert.True(File.Exists(cmdPath), "The approved fixed Windows command interpreter must exist.");

        string[] approvedDirectories =
        [
            Path.GetDirectoryName(jdtCommandPath)!,
            javaBinDirectory,
            Path.GetDirectoryName(pythonPath)!,
            Path.GetDirectoryName(cmdPath)!,
        ];
        string[] fixedChildPath = approvedDirectories
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var policy = new PreparedJdtHostPolicy(
            fixedChildPath,
            [runtimeRoot, javaRuntimeRoot],
            cmdPath,
            fixedChildPath,
            JavaBinDirectory: javaBinDirectory);
        return (hostPath, policy, runtimeRoot, javaPath, pythonPath, jdtCommandPath);
    }

    private static string FindFirstOnPath(IEnumerable<string> entries, string fileName)
    {
        foreach (string entry in entries)
        {
            if (!Path.IsPathFullyQualified(entry))
            {
                throw new InvalidOperationException("host_path_contains_unqualified_entry_before_tool");
            }

            string candidate = Path.Combine(entry, fileName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        throw new InvalidOperationException($"first_host_path_tool_missing:{fileName}");
    }

    private static bool TryDescribeInput(string path)
    {
        MethodInfo tryDescribe = Assert.IsAssignableFrom<MethodInfo>(
            typeof(PreparedJdtCommandResolver).GetMethod(
                "TryDescribe", BindingFlags.NonPublic | BindingFlags.Static));
        object?[] arguments = [path, null, null];
        return Assert.IsType<bool>(tryDescribe.Invoke(null, arguments));
    }

    private sealed class FixtureToolUseAuthorizer(PreparedSourceApproval approved) : IPreparedToolUseAuthorizer
    {
        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval approval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PreparedToolUseAuthorityResult(
                approval == approved && toolPurpose == PreparedToolPurpose.JdtNavigation
                    ? PreparedToolUseAuthorityStatus.Current
                    : PreparedToolUseAuthorityStatus.Denied));
        }
    }
}
