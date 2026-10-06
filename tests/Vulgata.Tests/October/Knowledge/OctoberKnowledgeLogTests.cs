using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberKnowledgeLogTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AppendAsync_TwoOutcomes_PreservesPriorBytesAndSafeUtcCorrelationAsync()
    {
        string directory = CreateOwnedDirectory();
        try
        {
            var instant = new DateTimeOffset(2026, 10, 6, 11, 22, 33, TimeSpan.Zero);
            IOctoberKnowledgeOperationLog log = new OctoberKnowledgeOperationLog(directory, new FixedLogTime(instant));
            var first = Entry(KnowledgeWriteStatus.Committed);
            Assert.True(await log.AppendAsync(first));
            string before = await File.ReadAllTextAsync(Path.Combine(directory, "log.md"));
            var second = first with { Status = KnowledgeWriteStatus.AlreadyCommitted };
            Assert.True(await log.AppendAsync(second));
            string after = await File.ReadAllTextAsync(Path.Combine(directory, "log.md"));
            Assert.StartsWith(before, after, StringComparison.Ordinal);
            Assert.Equal(2, after.Split('\n').Count(line => line.Contains(first.Operation.OperationId.ToString(), StringComparison.OrdinalIgnoreCase)));
            Assert.Contains(first.Operation.OperationId.ToString(), after, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(first.Operation.BatchId.ToString(), after, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(nameof(OperationKind.AcceptFile), after, StringComparison.Ordinal);
            Assert.Contains(nameof(KnowledgeWriteStatus.Committed), after, StringComparison.Ordinal);
            Assert.Contains(nameof(KnowledgeWriteStatus.AlreadyCommitted), after, StringComparison.Ordinal);
            var timestamp = Regex.Match(before, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+]00:00)");
            Assert.True(timestamp.Success);
            Assert.Equal(instant, DateTimeOffset.Parse(timestamp.Value, CultureInfo.InvariantCulture));
            Assert.DoesNotContain(first.Binding.TargetId, after, StringComparison.Ordinal);
            Assert.DoesNotContain(first.Binding.CanonicalDatabaseIdentity, after, StringComparison.Ordinal);
            Assert.DoesNotContain("secret-sentinel", after, StringComparison.Ordinal);
            Assert.DoesNotContain("injected-line", after, StringComparison.Ordinal);
            Assert.DoesNotContain("selection-sensitive", after, StringComparison.Ordinal);
            Assert.Contains(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first.Binding.TargetId))), after, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first.Binding.CanonicalDatabaseIdentity))), after, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AppendAsync_LogDestinationIsDirectory_ReturnsFailureWithoutChangingExistingDataAsync()
    {
        string directory = CreateOwnedDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "log.md"));
            await File.WriteAllTextAsync(Path.Combine(directory, "log.md", "owned-marker"), "preserve");
            IOctoberKnowledgeOperationLog log = new OctoberKnowledgeOperationLog(directory);
            Assert.False(await log.AppendAsync(Entry(KnowledgeWriteStatus.Committed)));
            Assert.Equal("preserve", await File.ReadAllTextAsync(Path.Combine(directory, "log.md", "owned-marker")));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AppendAsync_LockedLogAndCancellation_ExitsWithoutPartialRecordAsync()
    {
        string directory = CreateOwnedDirectory();
        try
        {
            string path = Path.Combine(directory, "log.md");
            await File.WriteAllTextAsync(path, "existing\n");
            IOctoberKnowledgeOperationLog log = new OctoberKnowledgeOperationLog(directory);
            await using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                Assert.False(await log.AppendAsync(Entry(KnowledgeWriteStatus.Committed), cancellation.Token).WaitAsync(TimeSpan.FromSeconds(10)));
            }
            Assert.Equal("existing\n", await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AppendAsync_ConcurrentInstances_ProduceCompleteNonInterleavedRecordsAsync()
    {
        string directory = CreateOwnedDirectory();
        try
        {
            var entries = Enumerable.Range(0, 24).Select(_ => Entry(KnowledgeWriteStatus.Committed)).ToArray();
            bool[] appended = await Task.WhenAll(entries.Select(entry => new OctoberKnowledgeOperationLog(directory).AppendAsync(entry)));
            Assert.All(appended, Assert.True);
            var lines = await File.ReadAllLinesAsync(Path.Combine(directory, "log.md"));
            foreach (var entry in entries)
                Assert.Single(lines.Where(line => line.Contains(entry.Operation.OperationId.ToString(), StringComparison.OrdinalIgnoreCase)));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AppendAsync_PreexistingIncompleteRecord_RefusesToMergeWithRecoveryRecordAsync()
    {
        string directory = CreateOwnedDirectory();
        try
        {
            string path = Path.Combine(directory, "log.md");
            await File.WriteAllTextAsync(path, "incomplete-prior-record");
            IOctoberKnowledgeOperationLog log = new OctoberKnowledgeOperationLog(directory);
            Assert.False(await log.AppendAsync(Entry(KnowledgeWriteStatus.AlreadyCommitted)));
            Assert.Equal("incomplete-prior-record", await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(directory, true); }
    }

    internal static string CreateOwnedDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "vulgata-knowledge-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
    internal static KnowledgeOperationLogEntry Entry(KnowledgeWriteStatus status)
    {
        var binding = new KnowledgeBinding("Host=secret-sentinel;Password=secret-sentinel\ninjected-line", "target-secret-sentinel\ninjected-line", Guid.NewGuid(), "selection-sensitive");
        return new(binding, new(binding.BatchId, OperationKind.AcceptFile, Guid.NewGuid()), status);
    }
    private sealed class FixedLogTime(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
