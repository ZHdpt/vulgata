using System.Diagnostics;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.AnalysisRuntime;

/// <summary>
/// Exercises Infrastructure's native CodeGraph session at the accepted
/// ICodeGraphWorkerToolSession boundary with real and controlled owned fixtures.
/// </summary>
public sealed class NativeCodeGraphMcpClientTests
{
    private const string CodeGraphCommandPath = @"G:\packages\npm\prefix\codegraph.ps1";
    private static readonly Guid RepositoryId =
        Guid.Parse("18300000-0000-4000-8000-000000000183");

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task NativeMcpClient_UsesNativeDiscoveryAndExploreAgainstOwnedPreparedFixture()
    {
        Assert.True(File.Exists(CodeGraphCommandPath), "The configured local CodeGraph command must be present.");

        string fixture = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story183-native-mcp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        await File.WriteAllTextAsync(
            Path.Combine(fixture, "Beacon.cs"),
            "public static class Story183Beacon { public static int LocateMe() => 183; }\n",
            new UTF8Encoding(false));
        await File.WriteAllTextAsync(
            Path.Combine(fixture, "BeaconCaller.cs"),
            "public static class Story183BeaconCaller { public static int Invoke() => Story183Beacon.LocateMe(); }\n",
            new UTF8Encoding(false));

        try
        {
            await InitializeOwnedFixtureAsync(CodeGraphCommandPath, fixture);
            Assert.True(Directory.Exists(Path.Combine(fixture, ".codegraph")));

            await using NativeCodeGraphMcpClient client = new(
                RepositoryId,
                CodeGraphCommandPath,
                fixture);

            CodeGraphToolReadiness readiness = await client.DiscoverAsync(CancellationToken.None);

            Assert.True(readiness.IsReady, $"Native MCP discovery was not ready: {readiness.Status}/{readiness.IssueCode}.");
            Assert.Equal(CodeGraphToolReadinessStatus.Ready, readiness.Status);
            Assert.Equal(CodeGraphToolStage.Discovery, readiness.Stage);
            Assert.Equal(CodeGraphToolIssueCode.None, readiness.IssueCode);
            Assert.Equal(RepositoryId, readiness.RepositoryId);

            CodeGraphToolInvocationResult result = await client.ExploreAsync(
                "Story183Beacon LocateMe",
                maxFiles: 4,
                CancellationToken.None);

            Assert.Equal(CodeGraphToolInvocationStatus.Complete, result.Status);
            Assert.Equal(CodeGraphToolIssueCode.None, result.IssueCode);
            Assert.Equal(RepositoryId, result.RepositoryId);
            Assert.NotEmpty(result.Excerpts);
            CodeGraphSourceExcerpt beacon = Assert.Single(
                result.Excerpts.Where(excerpt => excerpt.Location.RelativePath == "Beacon.cs"));
            Assert.Equal(RepositoryId, beacon.Location.RepositoryId);
            Assert.False(Path.IsPathRooted(beacon.Location.RelativePath));
            Assert.Contains("Story183Beacon", beacon.Text, StringComparison.Ordinal);
            Assert.Contains("LocateMe", beacon.Text, StringComparison.Ordinal);
            Assert.All(result.Excerpts, excerpt =>
            {
                Assert.Equal(RepositoryId, excerpt.Location.RepositoryId);
                Assert.False(Path.IsPathRooted(excerpt.Location.RelativePath));
                Assert.DoesNotContain("..", excerpt.Location.RelativePath, StringComparison.Ordinal);
            });

            CodeGraphToolInvocationResult noMatch = await client.ExploreAsync(
                "Story183SymbolThatDoesNotExist",
                maxFiles: 4,
                CancellationToken.None);

            Assert.Equal(CodeGraphToolInvocationStatus.NoMatch, noMatch.Status);
            Assert.Equal(CodeGraphToolIssueCode.None, noMatch.IssueCode);
            Assert.Equal(RepositoryId, noMatch.RepositoryId);
            Assert.Empty(noMatch.Excerpts);
            Assert.Empty(noMatch.Locations);
            Assert.NotEqual(CodeGraphToolInvocationStatus.Limited, noMatch.Status);

            await client.DisposeAsync();
            CodeGraphToolInvocationResult afterDispose = await client.ExploreAsync(
                "Story183Beacon",
                maxFiles: 4,
                CancellationToken.None);
            Assert.Equal(CodeGraphToolInvocationStatus.Unavailable, afterDispose.Status);
            Assert.Equal(CodeGraphToolIssueCode.SessionExpired, afterDispose.IssueCode);
            Assert.Empty(afterDispose.Excerpts);
            Assert.Empty(afterDispose.Locations);
        }
        finally
        {
            if (Directory.Exists(fixture))
            {
                Directory.Delete(fixture, recursive: true);
            }

            Assert.False(Directory.Exists(fixture), "The owned native MCP fixture must be removed after session disposal.");
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [InlineData("missing-tool", CodeGraphToolReadinessStatus.DiscoveryFailed, CodeGraphToolIssueCode.ToolMissing)]
    [InlineData("changed-schema", CodeGraphToolReadinessStatus.DiscoveryFailed, CodeGraphToolIssueCode.SchemaChanged)]
    public async Task NativeMcpClient_DiscoveryReportsMissingToolAndChangedSchemaSeparately(
        string scenario,
        CodeGraphToolReadinessStatus expectedStatus,
        CodeGraphToolIssueCode expectedIssue)
    {
        string fixture = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story183-mcp-schema-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        Directory.CreateDirectory(Path.Combine(fixture, ".codegraph"));
        string commandPath = Path.Combine(fixture, scenario + ".ps1");
        await File.WriteAllTextAsync(commandPath, ControlledMcpServerScript, new UTF8Encoding(false));

        try
        {
            await using NativeCodeGraphMcpClient client = new(
                RepositoryId,
                commandPath,
                fixture);

            CodeGraphToolReadiness readiness = await client.DiscoverAsync(CancellationToken.None);

            Assert.Equal(expectedStatus, readiness.Status);
            Assert.Equal(CodeGraphToolStage.Discovery, readiness.Stage);
            Assert.Equal(expectedIssue, readiness.IssueCode);
            Assert.Equal(RepositoryId, readiness.RepositoryId);
            Assert.False(readiness.IsReady);
        }
        finally
        {
            if (Directory.Exists(fixture))
            {
                Directory.Delete(fixture, recursive: true);
            }

            Assert.False(Directory.Exists(fixture), "The controlled MCP fixture must be removed after client cleanup.");
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task NativeMcpClient_ReportsIncompleteNativeResultDistinctFromNoMatch()
    {
        string fixture = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story183-mcp-incomplete-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        Directory.CreateDirectory(Path.Combine(fixture, ".codegraph"));
        string commandPath = Path.Combine(fixture, "incomplete-result.ps1");
        await File.WriteAllTextAsync(commandPath, ControlledMcpServerScript, new UTF8Encoding(false));

        try
        {
            await using NativeCodeGraphMcpClient client = new(
                RepositoryId,
                commandPath,
                fixture);

            CodeGraphToolReadiness readiness = await client.DiscoverAsync(CancellationToken.None);
            Assert.True(readiness.IsReady);

            CodeGraphToolInvocationResult incomplete = await client.ExploreAsync(
                "Story183IncompleteResult",
                maxFiles: 4,
                CancellationToken.None);

            Assert.Equal(CodeGraphToolInvocationStatus.Limited, incomplete.Status);
            Assert.Equal(CodeGraphToolIssueCode.IncompleteIndex, incomplete.IssueCode);
            Assert.Equal(RepositoryId, incomplete.RepositoryId);
            Assert.Contains(CodeGraphToolIssueCode.IncompleteIndex, incomplete.Limitations);
            Assert.Null(incomplete.Text);
            Assert.Empty(incomplete.Excerpts);
            Assert.Empty(incomplete.Locations);
            Assert.NotEqual(CodeGraphToolInvocationStatus.NoMatch, incomplete.Status);
        }
        finally
        {
            if (Directory.Exists(fixture))
            {
                Directory.Delete(fixture, recursive: true);
            }

            Assert.False(Directory.Exists(fixture), "The incomplete-result MCP fixture must be removed after client cleanup.");
        }
    }

    private const string ControlledMcpServerScript = """
        $ErrorActionPreference = 'Stop'
        [Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $scenario = [System.IO.Path]::GetFileNameWithoutExtension($MyInvocation.MyCommand.Path)

        while ($null -ne ($line = [Console]::In.ReadLine())) {
            $request = $line | ConvertFrom-Json
            $response = $null
            if ($request.method -eq 'initialize') {
                $response = @{
                    jsonrpc = '2.0'
                    id = $request.id
                    result = @{
                        protocolVersion = $request.params.protocolVersion
                        capabilities = @{ tools = @{} }
                        serverInfo = @{ name = 'test-owned-mcp'; version = '1.0.0' }
                    }
                }
            }
            elseif ($request.method -eq 'tools/list') {
                $tools = @()
                if ($scenario -eq 'changed-schema' -or $scenario -eq 'incomplete-result') {
                    $queryType = if ($scenario -eq 'changed-schema') { 'integer' } else { 'string' }
                    $properties = [ordered]@{
                        query = @{ type = $queryType }
                        maxFiles = @{ type = 'number' }
                        projectPath = @{ type = 'string' }
                    }
                    $tools = @(@{
                        name = 'codegraph_explore'
                        description = 'Owned test tool'
                        inputSchema = @{
                            type = 'object'
                            properties = $properties
                            required = @('query')
                        }
                    })
                }

                $response = @{
                    jsonrpc = '2.0'
                    id = $request.id
                    result = @{ tools = $tools }
                }
            }
            elseif ($request.method -eq 'tools/call') {
                $response = @{
                    jsonrpc = '2.0'
                    id = $request.id
                    result = @{
                        content = @(@{
                            type = 'text'
                            text = 'A symbol was found, but the source section is incomplete.'
                        })
                    }
                }
            }
            elseif ($request.method -eq 'ping') {
                $response = @{ jsonrpc = '2.0'; id = $request.id; result = @{} }
            }

            if ($null -ne $response) {
                [Console]::Out.WriteLine(($response | ConvertTo-Json -Depth 16 -Compress))
                [Console]::Out.Flush()
            }
        }
        """;

    private static async Task InitializeOwnedFixtureAsync(string commandPath, string fixture)
    {
        ProcessStartInfo startInfo = CreateCodeGraphStartInfo(commandPath, ["init", fixture], fixture);
        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("The configured CodeGraph command did not start for the owned fixture.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw new TimeoutException("CodeGraph preparation exceeded the owned-fixture timeout.");
        }

        _ = await standardOutput;
        _ = await standardError;
        Assert.Equal(0, process.ExitCode);
    }

    private static ProcessStartInfo CreateCodeGraphStartInfo(
        string commandPath,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        bool isPowerShellScript = Path.GetExtension(commandPath).Equals(".ps1", StringComparison.OrdinalIgnoreCase);
        ProcessStartInfo startInfo = new(isPowerShellScript ? "pwsh.exe" : commandPath)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (isPowerShellScript)
        {
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(commandPath);
        }

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["CODEGRAPH_NO_DAEMON"] = "1";
        startInfo.Environment["CODEGRAPH_TELEMETRY"] = "0";
        startInfo.Environment["DO_NOT_TRACK"] = "1";
        return startInfo;
    }
}
