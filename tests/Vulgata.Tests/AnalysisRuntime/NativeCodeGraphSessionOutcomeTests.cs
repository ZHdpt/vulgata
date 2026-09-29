using System.Reflection;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.AnalysisRuntime;

/// <summary>
/// Verifies finite binding-scoped native work at the accepted public session seam
/// with a controlled stdio MCP server in an owned fixture.
/// </summary>
public sealed class NativeCodeGraphSessionOutcomeTests
{
    private static readonly Guid RepositoryId =
        Guid.Parse("18300000-0000-4000-8000-000000000184");

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task NativeSession_UnpreparedWorkspaceDoesNotStartNativeProcess()
    {
        string fixture = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story183-unprepared-workspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        string serverPath = Path.Combine(fixture, "must-not-start.ps1");
        await File.WriteAllTextAsync(serverPath, UnpreparedWorkspaceServerScript, new UTF8Encoding(false));
        string processStartedPath = Path.Combine(fixture, "process-started.txt");

        try
        {
            await using ICodeGraphWorkerToolSession session = new NativeCodeGraphMcpClient(
                RepositoryId,
                serverPath,
                fixture);

            CodeGraphToolReadiness readiness = await session.DiscoverAsync(CancellationToken.None);

            Assert.Equal(CodeGraphToolReadinessStatus.Unprepared, readiness.Status);
            Assert.Equal(RepositoryId, readiness.RepositoryId);
            Assert.Equal(CodeGraphToolStage.Preparation, readiness.Stage);
            Assert.Equal(CodeGraphToolIssueCode.SourceUnprepared, readiness.IssueCode);
            Assert.False(File.Exists(processStartedPath), "An unprepared workspace must be rejected before launching the native process.");
        }
        finally
        {
            if (Directory.Exists(fixture))
            {
                Directory.Delete(fixture, recursive: true);
            }

            Assert.False(Directory.Exists(fixture), "The unprepared-workspace fixture must be removable after session disposal.");
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task NativeSession_ConnectionDeadlineReturnsSanitizedFailure()
    {
        string fixture = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story183-connection-deadline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        Directory.CreateDirectory(Path.Combine(fixture, ".codegraph"));
        string serverPath = Path.Combine(fixture, "slow-connection-server.ps1");
        await File.WriteAllTextAsync(serverPath, SlowConnectionMcpServerScript, new UTF8Encoding(false));
        string initializeStartedPath = Path.Combine(fixture, "initialize-started.txt");

        try
        {
            await using ICodeGraphWorkerToolSession session = new NativeCodeGraphMcpClient(
                RepositoryId,
                serverPath,
                fixture,
                TimeProvider.System,
                TimeSpan.FromSeconds(2));

            CodeGraphToolReadiness readiness = await session.DiscoverAsync(CancellationToken.None);

            Assert.True(
                File.Exists(initializeStartedPath),
                "The controlled server must receive initialize before the connection deadline is evaluated.");
            Assert.Equal(CodeGraphToolReadinessStatus.ConnectionFailed, readiness.Status);
            Assert.Equal(RepositoryId, readiness.RepositoryId);
            Assert.Equal(CodeGraphToolStage.Connection, readiness.Stage);
            Assert.Equal(CodeGraphToolIssueCode.DeadlineExceeded, readiness.IssueCode);
        }
        finally
        {
            if (Directory.Exists(fixture))
            {
                Directory.Delete(fixture, recursive: true);
            }

            Assert.False(Directory.Exists(fixture), "The connection-timeout fixture must be removable after session disposal.");
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task NativeSession_NativeToolErrorReturnsSanitizedFailure()
    {
        string fixture = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story183-native-tool-error-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        Directory.CreateDirectory(Path.Combine(fixture, ".codegraph"));
        await File.WriteAllTextAsync(
            Path.Combine(fixture, "QuotaProbe.cs"),
            "public static class Story183QuotaProbe { public static int Value() => 184; }\n",
            new UTF8Encoding(false));
        string serverPath = Path.Combine(fixture, "error-server.ps1");
        await File.WriteAllTextAsync(serverPath, ControlledMcpServerScript, new UTF8Encoding(false));
        await File.WriteAllTextAsync(Path.Combine(fixture, "native-error.txt"), "enabled", new UTF8Encoding(false));
        string callCountPath = Path.Combine(fixture, "call-count.txt");

        try
        {
            await using ICodeGraphWorkerToolSession session = new NativeCodeGraphMcpClient(
                RepositoryId,
                serverPath,
                fixture);

            CodeGraphToolReadiness readiness = await session.DiscoverAsync(CancellationToken.None);
            Assert.True(readiness.IsReady, $"Native discovery was not ready: {readiness.Status}/{readiness.IssueCode}.");

            CodeGraphToolInvocationResult result = await session.ExploreAsync(
                "Story183QuotaProbe Value",
                maxFiles: 4,
                CancellationToken.None);

            Assert.Equal("1", await File.ReadAllTextAsync(callCountPath));
            Assert.Equal(CodeGraphToolInvocationStatus.Failed, result.Status);
            Assert.Equal(CodeGraphToolIssueCode.InvocationFailed, result.IssueCode);
            Assert.Equal(RepositoryId, result.RepositoryId);
            Assert.Contains(CodeGraphToolIssueCode.InvocationFailed, result.Limitations);
            Assert.Null(result.Text);
            Assert.Empty(result.Excerpts);
            Assert.Empty(result.Locations);
        }
        finally
        {
            if (Directory.Exists(fixture))
            {
                Directory.Delete(fixture, recursive: true);
            }

            Assert.False(Directory.Exists(fixture), "The native-tool-error fixture must be removable after session disposal.");
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task NativeSession_OverBoundNativeResultIsLimitedWithoutDisclosure()
    {
        string fixture = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story183-result-limit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        Directory.CreateDirectory(Path.Combine(fixture, ".codegraph"));
        await File.WriteAllTextAsync(
            Path.Combine(fixture, "QuotaProbe.cs"),
            "public static class Story183QuotaProbe { public static int Value() => 184; }\n",
            new UTF8Encoding(false));
        string serverPath = Path.Combine(fixture, "large-result-server.ps1");
        await File.WriteAllTextAsync(serverPath, ControlledMcpServerScript, new UTF8Encoding(false));
        await File.WriteAllTextAsync(Path.Combine(fixture, "large-result.txt"), "enabled", new UTF8Encoding(false));
        string callCountPath = Path.Combine(fixture, "call-count.txt");

        try
        {
            await using ICodeGraphWorkerToolSession session = new NativeCodeGraphMcpClient(
                RepositoryId,
                serverPath,
                fixture);

            CodeGraphToolReadiness readiness = await session.DiscoverAsync(CancellationToken.None);
            Assert.True(readiness.IsReady, $"Native discovery was not ready: {readiness.Status}/{readiness.IssueCode}.");

            CodeGraphToolInvocationResult result = await session.ExploreAsync(
                "Story183QuotaProbe Value",
                maxFiles: 4,
                CancellationToken.None);

            Assert.Equal("1", await File.ReadAllTextAsync(callCountPath));
            Assert.Equal(CodeGraphToolInvocationStatus.Limited, result.Status);
            Assert.Equal(CodeGraphToolIssueCode.ResultLimit, result.IssueCode);
            Assert.Equal(RepositoryId, result.RepositoryId);
            Assert.Contains(CodeGraphToolIssueCode.ResultLimit, result.Limitations);
            Assert.Null(result.Text);
            Assert.Empty(result.Excerpts);
            Assert.Empty(result.Locations);
        }
        finally
        {
            if (Directory.Exists(fixture))
            {
                Directory.Delete(fixture, recursive: true);
            }

            Assert.False(Directory.Exists(fixture), "The over-bound-result fixture must be removable after session disposal.");
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void NativeSession_HasControlledClockConstructionSeamForFiniteLifetimeBehavior()
    {
        ConstructorInfo? constructor = typeof(NativeCodeGraphMcpClient).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(Guid), typeof(string), typeof(string), typeof(TimeProvider)],
            modifiers: null);

        Assert.True(
            constructor is not null,
            "Native session lifetime must be testable with an injected TimeProvider without waiting two minutes or changing the Worker seam.");
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task NativeSession_DiscoveryDeadlineReturnsSanitizedFailure()
    {
        string fixture = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story183-discovery-deadline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        Directory.CreateDirectory(Path.Combine(fixture, ".codegraph"));
        string serverPath = Path.Combine(fixture, "slow-discovery-server.ps1");
        await File.WriteAllTextAsync(serverPath, SlowDiscoveryMcpServerScript, new UTF8Encoding(false));
        string toolsListStartedPath = Path.Combine(fixture, "tools-list-started.txt");

        try
        {
            await using ICodeGraphWorkerToolSession session = new NativeCodeGraphMcpClient(
                RepositoryId,
                serverPath,
                fixture,
                TimeProvider.System,
                TimeSpan.FromSeconds(2));

            CodeGraphToolReadiness readiness = await session.DiscoverAsync(CancellationToken.None);

            Assert.True(
                File.Exists(toolsListStartedPath),
                "The controlled server must receive tools/list before discovery deadline is evaluated.");
            Assert.False(readiness.IsReady);
            Assert.Equal(CodeGraphToolReadinessStatus.DiscoveryFailed, readiness.Status);
            Assert.Equal(RepositoryId, readiness.RepositoryId);
            Assert.Equal(CodeGraphToolStage.Discovery, readiness.Stage);
            Assert.Equal(CodeGraphToolIssueCode.DeadlineExceeded, readiness.IssueCode);
        }
        finally
        {
            if (Directory.Exists(fixture))
            {
                Directory.Delete(fixture, recursive: true);
            }

            Assert.False(Directory.Exists(fixture), "The discovery-timeout fixture must be removable after session disposal.");
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task NativeSession_OperationDeadlineWithholdsLateNativeResult()
    {
        ConstructorInfo? constructor = typeof(NativeCodeGraphMcpClient).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(Guid), typeof(string), typeof(string), typeof(TimeProvider), typeof(TimeSpan)],
            modifiers: null);

        Assert.True(
            constructor is not null,
            "Native operations need a trusted lower timeout seam so the 30-second deadline can be verified without a 30-second wait.");

        string fixture = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story183-operation-deadline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        Directory.CreateDirectory(Path.Combine(fixture, ".codegraph"));
        await File.WriteAllTextAsync(
            Path.Combine(fixture, "QuotaProbe.cs"),
            "public static class Story183QuotaProbe { public static int Value() => 184; }\n",
            new UTF8Encoding(false));
        string serverPath = Path.Combine(fixture, "slow-server.ps1");
        await File.WriteAllTextAsync(serverPath, SlowMcpServerScript, new UTF8Encoding(false));
        string callStartedPath = Path.Combine(fixture, "call-started.txt");

        try
        {
            await using ICodeGraphWorkerToolSession session = (ICodeGraphWorkerToolSession)constructor!.Invoke(
                [RepositoryId, serverPath, fixture, TimeProvider.System, TimeSpan.FromSeconds(2)]);

            CodeGraphToolReadiness readiness = await session.DiscoverAsync(CancellationToken.None);
            Assert.True(readiness.IsReady, $"Native discovery was not ready: {readiness.Status}/{readiness.IssueCode}.");

            CodeGraphToolInvocationResult result = await session.ExploreAsync(
                "Story183QuotaProbe Value",
                maxFiles: 4,
                CancellationToken.None);

            Assert.True(File.Exists(callStartedPath), "The controlled native server must receive the delayed tools/call request.");
            Assert.Equal(CodeGraphToolInvocationStatus.TimedOut, result.Status);
            Assert.Equal(CodeGraphToolIssueCode.DeadlineExceeded, result.IssueCode);
            Assert.Equal(RepositoryId, result.RepositoryId);
            Assert.Null(result.Text);
            Assert.Empty(result.Excerpts);
            Assert.Empty(result.Locations);
        }
        finally
        {
            if (Directory.Exists(fixture))
            {
                Directory.Delete(fixture, recursive: true);
            }

            Assert.False(Directory.Exists(fixture), "The timed-out native-session fixture must be removable after disposal.");
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task NativeSession_CallerCancellationWithholdsLateNativeResult()
    {
        string fixture = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story183-caller-cancellation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        Directory.CreateDirectory(Path.Combine(fixture, ".codegraph"));
        await File.WriteAllTextAsync(
            Path.Combine(fixture, "QuotaProbe.cs"),
            "public static class Story183QuotaProbe { public static int Value() => 184; }\n",
            new UTF8Encoding(false));
        string serverPath = Path.Combine(fixture, "slow-server.ps1");
        await File.WriteAllTextAsync(serverPath, SlowMcpServerScript, new UTF8Encoding(false));
        string callStartedPath = Path.Combine(fixture, "call-started.txt");

        try
        {
            await using ICodeGraphWorkerToolSession session = new NativeCodeGraphMcpClient(
                RepositoryId,
                serverPath,
                fixture,
                TimeProvider.System,
                TimeSpan.FromSeconds(10));

            CodeGraphToolReadiness readiness = await session.DiscoverAsync(CancellationToken.None);
            Assert.True(readiness.IsReady, $"Native discovery was not ready: {readiness.Status}/{readiness.IssueCode}.");

            using var cancellation = new CancellationTokenSource();
            Task<CodeGraphToolInvocationResult> pendingCall = session.ExploreAsync(
                "Story183QuotaProbe Value",
                maxFiles: 4,
                cancellation.Token);

            bool callStarted = await WaitForFileAsync(callStartedPath, TimeSpan.FromSeconds(3));
            Assert.True(callStarted, "The controlled native server must receive tools/call before caller cancellation.");
            cancellation.Cancel();

            CodeGraphToolInvocationResult cancelled = await pendingCall.WaitAsync(TimeSpan.FromSeconds(3));

            Assert.Equal(CodeGraphToolInvocationStatus.Cancelled, cancelled.Status);
            Assert.Equal(CodeGraphToolIssueCode.Cancelled, cancelled.IssueCode);
            Assert.Equal(RepositoryId, cancelled.RepositoryId);
            Assert.Null(cancelled.Text);
            Assert.Empty(cancelled.Excerpts);
            Assert.Empty(cancelled.Locations);
        }
        finally
        {
            if (Directory.Exists(fixture))
            {
                Directory.Delete(fixture, recursive: true);
            }

            Assert.False(Directory.Exists(fixture), "The cancelled native-session fixture must be removable after disposal.");
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task NativeSession_ExpiresAfterTwoMinutesWithoutDispatchingOrDisclosingBufferedSource()
    {
        string fixture = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story183-session-lifetime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        Directory.CreateDirectory(Path.Combine(fixture, ".codegraph"));
        await File.WriteAllTextAsync(
            Path.Combine(fixture, "QuotaProbe.cs"),
            "public static class Story183QuotaProbe { public static int Value() => 184; }\n",
            new UTF8Encoding(false));
        await File.WriteAllTextAsync(
            Path.Combine(fixture, "quota-server.ps1"),
            ControlledMcpServerScript,
            new UTF8Encoding(false));
        string callCountPath = Path.Combine(fixture, "call-count.txt");
        var timeProvider = new AdvancingTimeProvider(new DateTimeOffset(2026, 9, 29, 0, 0, 0, TimeSpan.Zero));

        try
        {
            await using ICodeGraphWorkerToolSession session = new NativeCodeGraphMcpClient(
                RepositoryId,
                Path.Combine(fixture, "quota-server.ps1"),
                fixture,
                timeProvider);

            CodeGraphToolReadiness readiness = await session.DiscoverAsync(CancellationToken.None);
            Assert.True(readiness.IsReady, $"Native discovery was not ready: {readiness.Status}/{readiness.IssueCode}.");

            CodeGraphToolInvocationResult beforeExpiry = await session.ExploreAsync(
                "Story183QuotaProbe Value",
                maxFiles: 4,
                CancellationToken.None);
            Assert.Equal(CodeGraphToolInvocationStatus.Complete, beforeExpiry.Status);
            Assert.Contains(beforeExpiry.Excerpts, excerpt => excerpt.Location.RelativePath == "QuotaProbe.cs");
            Assert.Equal("1", await File.ReadAllTextAsync(callCountPath));

            timeProvider.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromTicks(1));

            CodeGraphToolInvocationResult expired = await session.ExploreAsync(
                "Story183QuotaProbe Value",
                maxFiles: 4,
                CancellationToken.None);

            Assert.Equal(CodeGraphToolInvocationStatus.Expired, expired.Status);
            Assert.Equal(CodeGraphToolIssueCode.SessionExpired, expired.IssueCode);
            Assert.Equal(RepositoryId, expired.RepositoryId);
            Assert.Null(expired.Text);
            Assert.Empty(expired.Excerpts);
            Assert.Empty(expired.Locations);
            Assert.Equal("1", await File.ReadAllTextAsync(callCountPath));
        }
        finally
        {
            if (Directory.Exists(fixture))
            {
                Directory.Delete(fixture, recursive: true);
            }

            Assert.False(Directory.Exists(fixture), "The expired-session fixture must be removed after disposal.");
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task NativeSession_StopsAfterTenCallsAndWithholdsTheEleventhResult()
    {
        string fixture = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story183-session-quota-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        Directory.CreateDirectory(Path.Combine(fixture, ".codegraph"));
        await File.WriteAllTextAsync(
            Path.Combine(fixture, "QuotaProbe.cs"),
            "public static class Story183QuotaProbe { public static int Value() => 184; }\n",
            new UTF8Encoding(false));
        await File.WriteAllTextAsync(
            Path.Combine(fixture, "quota-server.ps1"),
            ControlledMcpServerScript,
            new UTF8Encoding(false));
        string callCountPath = Path.Combine(fixture, "call-count.txt");

        try
        {
            await using ICodeGraphWorkerToolSession session = new NativeCodeGraphMcpClient(
                RepositoryId,
                Path.Combine(fixture, "quota-server.ps1"),
                fixture);

            CodeGraphToolReadiness readiness = await session.DiscoverAsync(CancellationToken.None);
            Assert.True(readiness.IsReady, $"Native discovery was not ready: {readiness.Status}/{readiness.IssueCode}.");
            Assert.Equal(RepositoryId, readiness.RepositoryId);

            for (int call = 1; call <= 10; call++)
            {
                CodeGraphToolInvocationResult allowed = await session.ExploreAsync(
                    "Story183QuotaProbe Value",
                    maxFiles: 4,
                    CancellationToken.None);

                Assert.True(
                    allowed.Status == CodeGraphToolInvocationStatus.Complete,
                    $"Expected useful native result for allowed call {call}, received {allowed.Status}/{allowed.IssueCode}.");
                Assert.Equal(RepositoryId, allowed.RepositoryId);
                Assert.Contains(
                    allowed.Excerpts,
                    excerpt => excerpt.Location.RelativePath == "QuotaProbe.cs" &&
                        excerpt.Text.Contains("Story183QuotaProbe", StringComparison.Ordinal));
            }

            Assert.Equal("10", await File.ReadAllTextAsync(callCountPath));

            CodeGraphToolInvocationResult overBudget = await session.ExploreAsync(
                "Story183QuotaProbe Value",
                maxFiles: 4,
                CancellationToken.None);

            Assert.Equal(CodeGraphToolInvocationStatus.Limited, overBudget.Status);
            Assert.Equal(CodeGraphToolIssueCode.ResultLimit, overBudget.IssueCode);
            Assert.Equal(RepositoryId, overBudget.RepositoryId);
            Assert.Contains(CodeGraphToolIssueCode.ResultLimit, overBudget.Limitations);
            Assert.Null(overBudget.Text);
            Assert.Empty(overBudget.Excerpts);
            Assert.Empty(overBudget.Locations);
            Assert.Equal("10", await File.ReadAllTextAsync(callCountPath));
        }
        finally
        {
            if (Directory.Exists(fixture))
            {
                Directory.Delete(fixture, recursive: true);
            }

            Assert.False(Directory.Exists(fixture), "The owned native-session fixture must be removed after disposal.");
        }
    }

    private const string ControlledMcpServerScript = """
        $ErrorActionPreference = 'Stop'
        [Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $callCountPath = Join-Path $PSScriptRoot 'call-count.txt'
        $callCount = 0
        $source = 'public static class Story183QuotaProbe { public static int Value() => 184; }'

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
                        serverInfo = @{ name = 'owned-quota-server'; version = '1.0.0' }
                    }
                }
            }
            elseif ($request.method -eq 'tools/list') {
                $response = @{
                    jsonrpc = '2.0'
                    id = $request.id
                    result = @{
                        tools = @(@{
                            name = 'codegraph_explore'
                            description = 'Owned test tool'
                            inputSchema = @{
                                type = 'object'
                                properties = @{
                                    query = @{ type = 'string' }
                                    maxFiles = @{ type = 'number' }
                                    projectPath = @{ type = 'string' }
                                }
                                required = @('query')
                            }
                        })
                    }
                }
            }
            elseif ($request.method -eq 'tools/call') {
                $callCount++
                [System.IO.File]::WriteAllText($callCountPath, [string]$callCount, [System.Text.Encoding]::ASCII)
                if (Test-Path (Join-Path $PSScriptRoot 'native-error.txt')) {
                    $response = @{
                        jsonrpc = '2.0'
                        id = $request.id
                        result = @{
                            isError = $true
                            content = @(@{ type = 'text'; text = 'controlled native diagnostic must not escape' })
                        }
                    }
                }
                elseif (Test-Path (Join-Path $PSScriptRoot 'large-result.txt')) {
                    $text = 'x' * 262145
                    $response = @{
                        jsonrpc = '2.0'
                        id = $request.id
                        result = @{
                            content = @(@{ type = 'text'; text = $text })
                        }
                    }
                }
                else {
                    $text = '**Source Code**' + [Environment]::NewLine + '**`QuotaProbe.cs`**' + [Environment]::NewLine + '```csharp' +
                        [Environment]::NewLine + '1' + "`t" + $source + [Environment]::NewLine + '```'
                    $response = @{
                        jsonrpc = '2.0'
                        id = $request.id
                        result = @{
                            content = @(@{ type = 'text'; text = $text })
                        }
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

    private const string UnpreparedWorkspaceServerScript = """
        $processStartedPath = Join-Path $PSScriptRoot 'process-started.txt'
        [System.IO.File]::WriteAllText($processStartedPath, 'started', [System.Text.Encoding]::ASCII)
        """;

    private const string SlowMcpServerScript = """
        $ErrorActionPreference = 'Stop'
        [Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $callStartedPath = Join-Path $PSScriptRoot 'call-started.txt'
        $callFinishedPath = Join-Path $PSScriptRoot 'call-finished.txt'
        $source = 'public static class Story183QuotaProbe { public static int Value() => 184; }'

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
                        serverInfo = @{ name = 'owned-slow-server'; version = '1.0.0' }
                    }
                }
            }
            elseif ($request.method -eq 'tools/list') {
                $response = @{
                    jsonrpc = '2.0'
                    id = $request.id
                    result = @{
                        tools = @(@{
                            name = 'codegraph_explore'
                            description = 'Owned test tool'
                            inputSchema = @{
                                type = 'object'
                                properties = @{
                                    query = @{ type = 'string' }
                                    maxFiles = @{ type = 'number' }
                                    projectPath = @{ type = 'string' }
                                }
                                required = @('query')
                            }
                        })
                    }
                }
            }
            elseif ($request.method -eq 'tools/call') {
                [System.IO.File]::WriteAllText($callStartedPath, 'started', [System.Text.Encoding]::ASCII)
                Start-Sleep -Milliseconds 4000
                [System.IO.File]::WriteAllText($callFinishedPath, 'finished', [System.Text.Encoding]::ASCII)
                $text = '**Source Code**' + [Environment]::NewLine + '**`QuotaProbe.cs`**' + [Environment]::NewLine + '```csharp' +
                    [Environment]::NewLine + '1' + "`t" + $source + [Environment]::NewLine + '```'
                $response = @{
                    jsonrpc = '2.0'
                    id = $request.id
                    result = @{
                        content = @(@{ type = 'text'; text = $text })
                    }
                }
            }

            if ($null -ne $response) {
                [Console]::Out.WriteLine(($response | ConvertTo-Json -Depth 16 -Compress))
                [Console]::Out.Flush()
            }
        }
        """;

    private const string SlowDiscoveryMcpServerScript = """
        $ErrorActionPreference = 'Stop'
        [Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $toolsListStartedPath = Join-Path $PSScriptRoot 'tools-list-started.txt'

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
                        serverInfo = @{ name = 'owned-slow-discovery-server'; version = '1.0.0' }
                    }
                }
            }
            elseif ($request.method -eq 'tools/list') {
                [System.IO.File]::WriteAllText($toolsListStartedPath, 'started', [System.Text.Encoding]::ASCII)
                Start-Sleep -Milliseconds 4000
                $response = @{
                    jsonrpc = '2.0'
                    id = $request.id
                    result = @{
                        tools = @(@{
                            name = 'codegraph_explore'
                            description = 'Owned test tool'
                            inputSchema = @{
                                type = 'object'
                                properties = @{
                                    query = @{ type = 'string' }
                                    maxFiles = @{ type = 'number' }
                                    projectPath = @{ type = 'string' }
                                }
                                required = @('query')
                            }
                        })
                    }
                }
            }

            if ($null -ne $response) {
                [Console]::Out.WriteLine(($response | ConvertTo-Json -Depth 16 -Compress))
                [Console]::Out.Flush()
            }
        }
        """;

    private const string SlowConnectionMcpServerScript = """
        $ErrorActionPreference = 'Stop'
        [Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $initializeStartedPath = Join-Path $PSScriptRoot 'initialize-started.txt'

        while ($null -ne ($line = [Console]::In.ReadLine())) {
            $request = $line | ConvertFrom-Json
            if ($request.method -eq 'initialize') {
                [System.IO.File]::WriteAllText($initializeStartedPath, 'started', [System.Text.Encoding]::ASCII)
                Start-Sleep -Milliseconds 4000
                $response = @{
                    jsonrpc = '2.0'
                    id = $request.id
                    result = @{
                        protocolVersion = $request.params.protocolVersion
                        capabilities = @{ tools = @{} }
                        serverInfo = @{ name = 'owned-slow-connection-server'; version = '1.0.0' }
                    }
                }
                [Console]::Out.WriteLine(($response | ConvertTo-Json -Depth 16 -Compress))
                [Console]::Out.Flush()
            }
        }
        """;

    private static async Task<bool> WaitForFileAsync(string path, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        return File.Exists(path);
    }

    private sealed class AdvancingTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private long _ticks = initialUtcNow.UtcTicks;

        public override DateTimeOffset GetUtcNow() =>
            new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref _ticks, elapsed.Ticks);
    }
}
