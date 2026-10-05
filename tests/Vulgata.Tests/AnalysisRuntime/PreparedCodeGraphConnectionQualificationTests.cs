using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.AnalysisRuntime;

/// <summary>Native selected-host proof through the application-returned descriptor.</summary>
public sealed class PreparedCodeGraphConnectionQualificationTests
{
    private const string CodeGraphCommandPath = @"G:\packages\npm\prefix\codegraph.ps1";
    private static readonly Guid RepositoryId =
        Guid.Parse("18300000-0000-4000-8000-000000000185");

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CancelledPreparation_WaitsForOwnedInitProcessExit()
    {
        string fixture = Path.Combine(Path.GetTempPath(),
            "vulgata-story183-cancelled-init-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        string script = Path.Combine(fixture, "owned-init.ps1");
        string processIdPath = Path.Combine(fixture, "process-id.txt");
        await File.WriteAllTextAsync(script,
            "[System.IO.File]::WriteAllText((Join-Path $PSScriptRoot 'process-id.txt'), [string]$PID); Start-Sleep -Seconds 30\n");
        using var cancellation = new CancellationTokenSource();
        var runner = new CodeGraphProcessRunner();
        var request = new CodeGraphProcessRequest(script, [], fixture,
            TimeSpan.FromSeconds(40), 1024, 1024, closeStandardInput: true);
        Task<CodeGraphProcessResult> pending = runner.RunAsync(request, cancellation.Token);

        try
        {
            for (int attempt = 0; attempt < 60 && !File.Exists(processIdPath); attempt++)
            {
                await Task.Delay(50);
            }

            Assert.True(File.Exists(processIdPath), "The owned init process must start before cancellation.");
            int processId = int.Parse(await File.ReadAllTextAsync(processIdPath));
            using Process ownedProcess = Process.GetProcessById(processId);
            Assert.False(ownedProcess.HasExited);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.True(ownedProcess.HasExited,
                "Preparation cancellation must wait for the owned init process to exit.");
        }
        finally
        {
            cancellation.Cancel();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (OperationCanceledException) { }
            Directory.Delete(fixture, recursive: true);
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedConnection_ReturnedDescriptor_InitializesDiscoversAndInvokesNativeTool()
    {
        Assert.True(File.Exists(CodeGraphCommandPath),
            "The selected host must contain the declared pinned CodeGraph shim.");
        string fixture = Path.Combine(Path.GetTempPath(),
            "vulgata-story183-returned-descriptor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        string source = Path.Combine(fixture, "Beacon.cs");
        string caller = Path.Combine(fixture, "BeaconCaller.cs");
        string sourceText = "public static class Story183Beacon { public static int LocateMe() => 183; }\n";
        string callerText = "public static class Story183BeaconCaller { public static int Invoke() => Story183Beacon.LocateMe(); }\n";
        await File.WriteAllTextAsync(source, sourceText, new UTF8Encoding(false));
        await File.WriteAllTextAsync(caller, callerText, new UTF8Encoding(false));

        try
        {
            var factory = new PreparedCodeGraphConnectionFactory(CodeGraphCommandPath);
            await using PreparedCodeGraphConnection connection = await factory.PrepareAsync(
                RepositoryId, fixture, CancellationToken.None);
            Assert.Equal(Path.GetFullPath(fixture), connection.WorkingDirectory);
            Assert.Contains(Path.GetFullPath(fixture), connection.Arguments);
            Assert.Equal("1", connection.Environment["CODEGRAPH_NO_DOWNLOAD"]);
            ICodeGraphWorkerToolSession session = connection.OpenSession();
            CodeGraphToolRegistration registration = await new CodeGraphFunctions(session)
                .CreateAsync(CancellationToken.None);
            Assert.True(registration.Readiness.IsReady,
                $"Native discovery failed: {registration.Readiness.Status}/{registration.Readiness.IssueCode}");
            AIFunction tool = Assert.Single(registration.Functions);
            Assert.Equal("codegraph_explore", tool.Name);

            object? deniedRaw = await tool.InvokeAsync(new AIFunctionArguments(
                new Dictionary<string, object?>
                {
                    ["query"] = "Story183Beacon LocateMe",
                    ["projectPath"] = Path.GetTempPath(),
                }));
            Assert.Equal(CodeGraphToolInvocationStatus.Denied,
                Assert.IsType<CodeGraphToolInvocationResult>(deniedRaw).Status);

            object? raw = await tool.InvokeAsync(new AIFunctionArguments(
                new Dictionary<string, object?>
                {
                    ["query"] = "Story183Beacon LocateMe",
                    ["maxFiles"] = 4,
                }));
            CodeGraphToolInvocationResult result = Assert.IsType<CodeGraphToolInvocationResult>(raw);
            Assert.Equal(CodeGraphToolInvocationStatus.Complete, result.Status);
            Assert.Equal(RepositoryId, result.RepositoryId);
            CodeGraphSourceExcerpt beacon = Assert.Single(result.Excerpts.Where(excerpt =>
                excerpt.Location.RelativePath.Equals("Beacon.cs", StringComparison.OrdinalIgnoreCase)));
            Assert.Contains("Story183Beacon", beacon.Text, StringComparison.Ordinal);

            Task<ClientCompletionDetails> completion = Assert.IsType<NativeCodeGraphMcpClient>(session)
                .Completion ?? throw new Xunit.Sdk.XunitException("The owned native session has no completion task.");
            await connection.DisposeAsync();
            StdioClientCompletionDetails exit = Assert.IsType<StdioClientCompletionDetails>(
                await completion.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.NotNull(exit.ProcessId);
            Assert.NotNull(exit.ExitCode);
            Assert.Equal(CodeGraphToolReadinessStatus.Expired,
                (await session.DiscoverAsync(CancellationToken.None)).Status);
            Assert.Equal(sourceText, await File.ReadAllTextAsync(source));
            Assert.Equal(callerText, await File.ReadAllTextAsync(caller));
            Assert.False(File.Exists(Path.Combine(fixture, "codegraph.json")));
            Assert.False(Directory.Exists(Path.Combine(fixture, ".git")));
            Assert.True(Directory.Exists(Path.Combine(fixture, ".codegraph")));
            Assert.All(Directory.EnumerateFileSystemEntries(fixture), entry =>
                Assert.Contains(Path.GetFileName(entry), new[] { "Beacon.cs", "BeaconCaller.cs", ".codegraph" }));
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }
}
