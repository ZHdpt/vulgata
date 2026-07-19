using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vulgata.Tests.CodeGraphQualification;

[Trait("Category", "CodeGraphQualificationSynthetic")]
public sealed class QualificationAttemptLedgerTests
{
    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Claim_WhenAuthorizationMatchesBoundary_CreatesAndFlushesOneStartAsync()
    {
        using TemporaryQualificationRoot temporary = new();
        const string token = "synthetic-random-token";
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.FromHours(8));
        QualificationExecutionBoundary boundary = await temporary.WriteBoundaryAsync();
        await temporary.WriteAuthorizationAsync(boundary, token, now.AddMinutes(5));
        QualificationAttemptLedger ledger = temporary.CreateLedger(boundary, now);

        ClaimedProviderAuthorization claim = await ledger.ClaimAsync(
            boundary.AttemptId,
            token,
            boundary.ExactCommand);

        string startedPath = Path.Combine(temporary.AttemptDirectory, "started.json");
        Assert.True(File.Exists(startedPath));
        using FileStream exclusiveRead = new(startedPath, FileMode.Open, FileAccess.Read, FileShare.None);
        using JsonDocument started = await JsonDocument.ParseAsync(exclusiveRead);
        Assert.Equal("attempt-0003", started.RootElement.GetProperty("attemptId").GetString());
        Assert.Equal(boundary.ExactCommand, started.RootElement.GetProperty("exactCommand").GetString());
        Assert.Equal(boundary.RawSha256, started.RootElement.GetProperty("executionBoundarySha256").GetString());
        Assert.Equal(boundary.PlanReviewSha256, started.RootElement.GetProperty("planReviewSha256").GetString());
        using ProviderExecutionLease? lease = ProviderExecutionGuard.Issue(claim);
        Assert.NotNull(lease);
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Complete_WhenClaimIsValid_CreatesOneRedactedTerminalRecordAsync()
    {
        using TemporaryQualificationRoot temporary = new();
        const string token = "synthetic-terminal-token";
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.FromHours(8));
        QualificationExecutionBoundary boundary = await temporary.WriteBoundaryAsync();
        await temporary.WriteAuthorizationAsync(boundary, token, now.AddMinutes(5));
        QualificationAttemptLedger ledger = temporary.CreateLedger(boundary, now);
        ClaimedProviderAuthorization claim = await ledger.ClaimAsync(
            boundary.AttemptId,
            token,
            boundary.ExactCommand);

        await ledger.CompleteAsync(
            claim,
            "failed",
            "unsafe-evidence",
            "specs/001-codegraph-extraction/qualification/evidence/final-gate.md");

        string terminalPath = Path.Combine(temporary.AttemptDirectory, "terminal.json");
        string terminalJson = await File.ReadAllTextAsync(terminalPath);
        Assert.DoesNotContain(token, terminalJson, StringComparison.Ordinal);
        using JsonDocument terminal = JsonDocument.Parse(terminalJson);
        Assert.Equal("failed", terminal.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("unsafe-evidence", terminal.RootElement.GetProperty("category").GetString());
        await ledger.CompleteAsync(
            claim,
            "failed",
            "later-writer",
            "specs/001-codegraph-extraction/qualification/evidence/final-gate.md");
        Assert.Equal(terminalJson, await File.ReadAllTextAsync(terminalPath));
    }

    [Theory]
    [InlineData("tokenSha256", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("expiresAt", "2026-07-18T11:59:59+08:00")]
    [InlineData("executionBoundaryPath", "specs/other-boundary.json")]
    [InlineData("executionBoundarySha256", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Claim_WhenAuthorizationBoundaryDiffers_RejectsBeforeStartAsync(
        string propertyName,
        string value)
    {
        using TemporaryQualificationRoot temporary = new();
        const string token = "synthetic-rejection-token";
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.FromHours(8));
        QualificationExecutionBoundary boundary = await temporary.WriteBoundaryAsync();
        await temporary.WriteAuthorizationAsync(boundary, token, now.AddMinutes(5));
        string authorizationPath = Path.Combine(temporary.AttemptDirectory, "authorized.json");
        JsonObject authorization = JsonNode.Parse(await File.ReadAllTextAsync(authorizationPath))!.AsObject();
        authorization[propertyName] = value;
        await File.WriteAllTextAsync(authorizationPath, authorization.ToJsonString());
        QualificationAttemptLedger ledger = temporary.CreateLedger(boundary, now);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.ClaimAsync(
            boundary.AttemptId,
            token,
            boundary.ExactCommand));

        Assert.False(File.Exists(Path.Combine(temporary.AttemptDirectory, "started.json")));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Claim_WhenAttemptHasExistingStart_RejectsReuseWithoutOverwriteAsync()
    {
        using TemporaryQualificationRoot temporary = new();
        const string token = "synthetic-reuse-token";
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.FromHours(8));
        QualificationExecutionBoundary boundary = await temporary.WriteBoundaryAsync();
        await temporary.WriteAuthorizationAsync(boundary, token, now.AddMinutes(5));
        QualificationAttemptLedger ledger = temporary.CreateLedger(boundary, now);
        using ClaimedProviderAuthorization claim =
            await ledger.ClaimAsync(boundary.AttemptId, token, boundary.ExactCommand);
        string startedPath = Path.Combine(temporary.AttemptDirectory, "started.json");
        string original = await File.ReadAllTextAsync(startedPath);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ledger.ClaimAsync(boundary.AttemptId, token, boundary.ExactCommand));

        Assert.Equal(original, await File.ReadAllTextAsync(startedPath));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Reconcile_WhenAuthorizationExistsWithoutStart_PublishesInterruptedBeforeClaimAsync()
    {
        using TemporaryQualificationRoot temporary = new();
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.FromHours(8));
        QualificationExecutionBoundary boundary = await temporary.WriteBoundaryAsync();
        await temporary.WriteAuthorizationAsync(boundary, "unused-token", now.AddMinutes(5));
        QualificationAttemptLedger ledger = temporary.CreateLedger(boundary, now);

        await ledger.ReconcileAsync("attempt-0003");

        Assert.False(File.Exists(Path.Combine(temporary.AttemptDirectory, "started.json")));
        Assert.Equal("interrupted-before-claim", await ReadTerminalCategoryAsync(temporary.AttemptDirectory));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Reconcile_WhenStartExistsWithoutTerminal_PublishesInterruptedAfterClaimWithoutRetryAsync()
    {
        using TemporaryQualificationRoot temporary = new();
        const string token = "synthetic-interrupted-token";
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.FromHours(8));
        QualificationExecutionBoundary boundary = await temporary.WriteBoundaryAsync();
        await temporary.WriteAuthorizationAsync(boundary, token, now.AddMinutes(5));
        QualificationAttemptLedger ledger = temporary.CreateLedger(boundary, now);
        ClaimedProviderAuthorization claim =
            await ledger.ClaimAsync(boundary.AttemptId, token, boundary.ExactCommand);
        string startedPath = Path.Combine(temporary.AttemptDirectory, "started.json");
        string originalStart = await File.ReadAllTextAsync(startedPath);
        claim.Dispose();

        await ledger.ReconcileAsync("attempt-0003");

        Assert.Equal(originalStart, await File.ReadAllTextAsync(startedPath));
        Assert.Equal("interrupted-after-claim", await ReadTerminalCategoryAsync(temporary.AttemptDirectory));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task CompleteAndReconcile_WhenTerminalWritersRace_FirstCompleteValidPublicationWinsAsync()
    {
        using TemporaryQualificationRoot temporary = new();
        const string token = "synthetic-race-token";
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.FromHours(8));
        QualificationExecutionBoundary boundary = await temporary.WriteBoundaryAsync();
        await temporary.WriteAuthorizationAsync(boundary, token, now.AddMinutes(5));
        QualificationAttemptLedger ledger = temporary.CreateLedger(boundary, now);
        ClaimedProviderAuthorization claim = await ledger.ClaimAsync(
            boundary.AttemptId,
            token,
            boundary.ExactCommand);

        await Task.WhenAll(
            ledger.CompleteAsync(
                claim,
                "failed",
                "provider-contract-failure",
                "specs/001-codegraph-extraction/qualification/evidence/verification.md"),
            ledger.ReconcileAsync("attempt-0003"));

        string category = await ReadTerminalCategoryAsync(temporary.AttemptDirectory);
        Assert.Contains(category, new[] { "provider-contract-failure", "interrupted-after-claim" });
        Assert.Single(Directory.GetFiles(temporary.AttemptDirectory, "terminal.json"));
        Assert.Empty(Directory.GetFiles(temporary.AttemptDirectory, ".terminal.*.tmp"));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Reconcile_WhenPublishedTerminalIsInvalid_BlocksAsCorruptionWithoutRepairAsync()
    {
        using TemporaryQualificationRoot temporary = new();
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.FromHours(8));
        QualificationExecutionBoundary boundary = await temporary.WriteBoundaryAsync();
        await temporary.WriteAuthorizationAsync(boundary, "unused-token", now.AddMinutes(5));
        string terminalPath = Path.Combine(temporary.AttemptDirectory, "terminal.json");
        await File.WriteAllTextAsync(
            terminalPath,
            "{\"schemaVersion\":\"1.0\",\"attemptId\":\"attempt-0003\"");
        byte[] corruptBytes = await File.ReadAllBytesAsync(terminalPath);
        QualificationAttemptLedger ledger = temporary.CreateLedger(boundary, now);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ledger.ReconcileAsync("attempt-0003"));

        Assert.Contains("corruption", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(corruptBytes, await File.ReadAllBytesAsync(terminalPath));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Reconcile_WhenInterruptedTemporaryFileExists_PublishesCompleteTerminalWithoutUsingPartialAsync()
    {
        using TemporaryQualificationRoot temporary = new();
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.FromHours(8));
        QualificationExecutionBoundary boundary = await temporary.WriteBoundaryAsync();
        await temporary.WriteAuthorizationAsync(boundary, "unused-token", now.AddMinutes(5));
        string interruptedTemp = Path.Combine(temporary.AttemptDirectory, ".terminal.interrupted.tmp");
        await File.WriteAllTextAsync(interruptedTemp, "{\"schemaVersion\":\"1.0\"");
        QualificationAttemptLedger ledger = temporary.CreateLedger(boundary, now);

        await ledger.ReconcileAsync("attempt-0003");

        Assert.Equal("interrupted-before-claim", await ReadTerminalCategoryAsync(temporary.AttemptDirectory));
        Assert.Equal("{\"schemaVersion\":\"1.0\"", await File.ReadAllTextAsync(interruptedTemp));
    }

    private static async Task<string> ReadTerminalCategoryAsync(string attemptDirectory)
    {
        using JsonDocument terminal = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(attemptDirectory, "terminal.json")));
        return terminal.RootElement.GetProperty("category").GetString()!;
    }

    internal sealed class TemporaryQualificationRoot : IDisposable
    {
        private const string BoundaryRelativePath =
            "specs/001-codegraph-extraction/qualification/execution-boundaries/attempt-0003.json";

        internal TemporaryQualificationRoot()
        {
            Root = Path.Combine(Path.GetTempPath(), $"vulgata-attempt-ledger-{Guid.NewGuid():N}");
            AttemptsRoot = Path.Combine(
                Root,
                "specs",
                "001-codegraph-extraction",
                "qualification",
                "attempts");
            AttemptDirectory = Path.Combine(AttemptsRoot, "attempt-0003");
            Directory.CreateDirectory(AttemptDirectory);
        }

        internal string Root { get; }

        internal string AttemptsRoot { get; }

        internal string AttemptDirectory { get; }

        internal async Task<QualificationExecutionBoundary> WriteBoundaryAsync()
        {
            string path = Path.Combine(
                Root,
                BoundaryRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            byte[] bytes = Encoding.UTF8.GetBytes(QualificationExecutionBoundaryTests.CreateBoundaryJson());
            await File.WriteAllBytesAsync(path, bytes);
            return await QualificationExecutionBoundary.LoadAsync(
                path,
                Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }

        internal async Task WriteAuthorizationAsync(
            QualificationExecutionBoundary boundary,
            string token,
            DateTimeOffset expiresAt)
        {
            object authorization = new
            {
                schemaVersion = "1.0",
                attemptId = boundary.AttemptId,
                tokenSha256 = Sha256(token),
                expiresAt,
                executionBoundaryPath = BoundaryRelativePath,
                executionBoundarySha256 = boundary.RawSha256,
            };
            await File.WriteAllTextAsync(
                Path.Combine(AttemptDirectory, "authorized.json"),
                JsonSerializer.Serialize(authorization));
        }

        internal QualificationAttemptLedger CreateLedger(
            QualificationExecutionBoundary boundary,
            DateTimeOffset now) =>
            new(
                AttemptsRoot,
                new QualificationAttemptLedgerPolicy(boundary, BoundaryRelativePath, () => now));

        internal Dictionary<string, string?> CreateEnvironment(
            QualificationExecutionBoundary boundary,
            string token) =>
            new(StringComparer.Ordinal)
            {
                [ProviderQualificationEntry.AttemptIdEnvironmentVariable] = boundary.AttemptId,
                [ProviderQualificationEntry.TokenEnvironmentVariable] = token,
                [ProviderQualificationEntry.ExactCommandEnvironmentVariable] = boundary.ExactCommand,
                [ProviderQualificationEntry.BoundaryPathEnvironmentVariable] = BoundaryRelativePath,
                [ProviderQualificationEntry.BoundarySha256EnvironmentVariable] = boundary.RawSha256,
                [ProviderQualificationEntry.RepositoryRootEnvironmentVariable] = Root,
            };

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private static string Sha256(string value) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
