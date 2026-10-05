using System.Diagnostics;
using System.Reflection;
using System.Text;
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
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
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
                    Path.Combine(@"C:\Program Files\Java\jdk-27", "bin", "java.exe"),
                })
                {
                    output.WriteLine("No-follow executable/runtime input {0}: {1}.",
                        path, TryDescribeInput(path));
                }

                DirectoryInfo javaParent = new(Path.GetDirectoryName(javaPath)!);
                output.WriteLine("First-PATH Java parent attributes={0}; linkTarget={1}.",
                    javaParent.Attributes, javaParent.LinkTarget ?? "<none>");
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

        try
        {
            unrelatedSentinel = StartUnrelatedWaitingProcess(hostPolicy.FixedCmdAbsolutePath);
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.False(unrelatedSentinel.HasExited,
                "The unrelated test-owned sibling process must be alive before JDT starts.");

            PreparedJavaNavigationOpenResult opened = await factory.OpenDedicatedAsync(lease)
                .WaitAsync(profile.InitializationTimeout + TimeSpan.FromSeconds(10));
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

            output.WriteLine("Real JDT-LS dedicated navigation qualified through the public host-policy factory.");
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
        finally
        {
            try
            {
                if (context is not null)
                {
                    await context.DisposeAsync().AsTask().WaitAsync(profile.ShutdownTimeout + TimeSpan.FromSeconds(5));
                    contextSettled = true;
                }

                if (unrelatedSentinel is not null)
                {
                    bool sentinelSurvivedJdtShutdown = contextSettled && !unrelatedSentinel.HasExited;
                    if (!unrelatedSentinel.HasExited)
                    {
                        unrelatedSentinel.Kill(entireProcessTree: true);
                        await unrelatedSentinel.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }

                    unrelatedSentinel.Dispose();
                    if (contextSettled)
                    {
                        Assert.True(sentinelSurvivedJdtShutdown,
                            "Stopping the owned JDT process tree must leave an unrelated sibling process running.");
                    }
                }

                if (contextSettled)
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
            }
            finally
            {
                await lease.DisposeAsync();
            }
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
            Assert.Equal(expectedBytes, actual[relativePath]);
        }
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

    private static (string HostPath, PreparedJdtHostPolicy Policy, string RuntimeRoot,
        string JavaPath, string PythonPath, string JdtCommandPath) CreateCurrentHostPolicy()
    {
        string hostPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] entries = hostPath.Split(Path.PathSeparator);
        string jdtCommandPath = FindFirstOnPath(entries, "jdtls.bat");
        string javaPath = FindFirstOnPath(entries, "java.exe");
        string pythonPath = FindFirstOnPath(entries, "python.exe");
        string approvedJdtCommand = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "jdtls.bat"));
        string approvedPython = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "python.exe"));
        string approvedJava = @"C:\Program Files\Common Files\Oracle\Java\javapath\java.exe";
        Assert.Equal(approvedJdtCommand, jdtCommandPath);
        Assert.Equal(approvedPython, pythonPath);
        Assert.Equal(Path.GetFullPath(approvedJava), javaPath);
        string javaAliasDirectory = Path.GetDirectoryName(javaPath)!;
        string javaPhysicalDirectory = Path.GetFullPath(
            new DirectoryInfo(javaAliasDirectory).ResolveLinkTarget(returnFinalTarget: true)?.FullName ??
            throw new InvalidOperationException("approved_first_java_parent_link_unavailable"));
        Assert.Equal(
            Path.GetFullPath(@"C:\Program Files\Common Files\Oracle\Java\javapath_target_776515"),
            javaPhysicalDirectory);
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

        string jdkRuntimeRoot = @"C:\Program Files\Java\jdk-27";
        Assert.True(Directory.Exists(jdkRuntimeRoot),
            "The Java 27 runtime recorded in the approved native preflight must remain available.");
        string[] approvedDirectories =
        [
            Path.GetDirectoryName(jdtCommandPath)!,
            javaAliasDirectory,
            javaPhysicalDirectory,
            Path.GetDirectoryName(pythonPath)!,
            Path.GetDirectoryName(cmdPath)!,
            Path.Combine(jdkRuntimeRoot, "bin"),
        ];
        string[] fixedChildPath = approvedDirectories
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var policy = new PreparedJdtHostPolicy(
            fixedChildPath,
            [runtimeRoot, jdkRuntimeRoot],
            cmdPath,
            fixedChildPath);
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
