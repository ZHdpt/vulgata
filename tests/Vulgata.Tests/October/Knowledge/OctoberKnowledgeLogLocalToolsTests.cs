using System.Diagnostics;
using System.Globalization;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberKnowledgeLogLocalToolsTests
{
    private const string ChildDirectory = "VULGATA_KNOWLEDGE_LOG_CHILD_DIRECTORY";
    private const string ChildIndex = "VULGATA_KNOWLEDGE_LOG_CHILD_INDEX";
    private const string ChildNonce = "VULGATA_KNOWLEDGE_LOG_CHILD_NONCE";

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task AppendAsync_TwoOwnedProcesses_PreserveEveryCompleteRecordAsync()
    {
        string? childDirectory = Environment.GetEnvironmentVariable(ChildDirectory);
        if (childDirectory is not null)
        {
            string nonce = Environment.GetEnvironmentVariable(ChildNonce) ?? "";
            Assert.Equal(nonce, await File.ReadAllTextAsync(Path.Combine(childDirectory, "owner.marker")));
            int index = int.Parse(Environment.GetEnvironmentVariable(ChildIndex)!, CultureInfo.InvariantCulture);
            await File.WriteAllTextAsync(Path.Combine(childDirectory, $"ready-{index}"), nonce);
            await WaitForFileAsync(Path.Combine(childDirectory, "go"));
            IOctoberKnowledgeOperationLog log = new OctoberKnowledgeOperationLog(childDirectory);
            for (int i = 0; i < 24; i++)
            {
                var binding = new KnowledgeBinding("controlled-process-database", "controlled-process-target", Operation(99), "controlled-digest");
                var append = log.AppendAsync(new(binding, new(binding.BatchId, OperationKind.AcceptFile, Operation(index * 24 + i)), KnowledgeWriteStatus.Committed));
                if (i == 0)
                {
                    // The parent still holds the exclusive log handle. Signal only after proving contention.
                    Assert.False(append.IsCompleted);
                    await File.WriteAllTextAsync(Path.Combine(childDirectory, $"attempted-{index}"), nonce);
                }
                Assert.True(await append);
            }
            return;
        }

        string directory = Path.Combine(Path.GetTempPath(), "vulgata-log-process-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var children = new List<Process>();
        try
        {
            string nonce = Guid.NewGuid().ToString("N");
            await File.WriteAllTextAsync(Path.Combine(directory, "owner.marker"), nonce);
            string root = FindRepositoryRoot();
            for (int index = 0; index < 2; index++)
            {
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
                // Direct guarded testhost on this already-built assembly, never the repository launcher or a recursive build.
                start.ArgumentList.Add("vstest");
                start.ArgumentList.Add(typeof(OctoberKnowledgeLogLocalToolsTests).Assembly.Location);
                start.ArgumentList.Add("--Settings:" + Path.Combine(root, "tests", "Vulgata.Tests", "explicit-suites.runsettings"));
                start.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName=Vulgata.Tests.October.Knowledge.OctoberKnowledgeLogLocalToolsTests.AppendAsync_TwoOwnedProcesses_PreserveEveryCompleteRecordAsync");
                start.Environment[ChildDirectory] = directory;
                start.Environment[ChildIndex] = index.ToString(CultureInfo.InvariantCulture);
                start.Environment[ChildNonce] = nonce;
                start.Environment["VULGATA_TEST_SUITE"] = "LocalTools";
                var process = Process.Start(start) ?? throw new InvalidOperationException("Owned log child did not start.");
                children.Add(process);
            }
            var outputs = children.Select(process => Task.WhenAll(process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync())).ToArray();
            await Task.WhenAll(Enumerable.Range(0, 2).Select(index => WaitForFileAsync(Path.Combine(directory, $"ready-{index}"))));
            await using (var heldLog = new FileStream(Path.Combine(directory, "log.md"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                await File.WriteAllTextAsync(Path.Combine(directory, "go"), nonce);
                await Task.WhenAll(Enumerable.Range(0, 2).Select(index => WaitForFileAsync(Path.Combine(directory, $"attempted-{index}"))));
            }
            await Task.WhenAll(children.Select(process => process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60))));
            for (int index = 0; index < children.Count; index++)
            {
                string output = string.Join('\n', await outputs[index]);
                Assert.True(children[index].ExitCode == 0, $"Owned child failed: {output}");
            }
            string[] lines = await File.ReadAllLinesAsync(Path.Combine(directory, "log.md"));
            Assert.Equal(48, lines.Count(line => line.Contains(nameof(OperationKind.AcceptFile), StringComparison.Ordinal)));
            for (int id = 0; id < 48; id++)
                Assert.Single(lines.Where(line => line.Contains(Operation(id).ToString(), StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            foreach (var child in children)
            {
                if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); }
                child.Dispose();
            }
            Directory.Delete(directory, true);
        }
    }
    private static Guid Operation(int id) => Guid.Parse($"11111111-1111-1111-1111-{id:000000000000}");
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "tests", "Vulgata.Tests", "explicit-suites.runsettings"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository test settings missing.");
    }
    private static async Task WaitForFileAsync(string path)
    {
        var timeout = Stopwatch.StartNew();
        while (!File.Exists(path))
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Owned log child rendezvous timed out.");
            await Task.Delay(20);
        }
    }
}
