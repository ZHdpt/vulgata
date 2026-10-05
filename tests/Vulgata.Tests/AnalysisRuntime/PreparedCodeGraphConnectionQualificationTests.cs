using System.Text;
using Microsoft.Extensions.AI;
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

            await connection.DisposeAsync();
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
