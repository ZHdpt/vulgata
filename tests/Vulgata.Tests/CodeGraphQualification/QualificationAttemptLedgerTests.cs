using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vulgata.Tests.CodeGraphQualification;

[Trait("Category", "CodeGraphQualificationSynthetic")]
public sealed class QualificationAttemptLedgerTests
{
    private const string PlanCommit = "84b0909b6713b410258d4db7ec37503cfe0c5980";
    private const string PlanReviewPath = "specs/001-codegraph-extraction/reviews/plan-review-8.json";
    private const string PlanReviewSha256 = "f73f877b3c7d37b3c739ee123867e39e3b71769e4abd1d8bbe5bd2f51806b408";
    private const string ExactCommand = "pwsh -NoProfile -File tests/Vulgata.Tests/CodeGraphQualification/Invoke-CodeGraphQualificationProvider.ps1 -AttemptId attempt-0002";
    private const string ApprovalObservedAt = "2026-07-16T18:10:00+08:00";
    private const string ApprovalSummary = "User explicitly approved the independently accepted corrective plan and one guarded attempt-0002.";

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Claim_WhenAuthorizationMatches_CreatesAndFlushesStartBeforeReachabilityAsync()
    {
        using TemporaryAttemptDirectory temporary = new();
        const string token = "synthetic-random-token";
        DateTimeOffset now = new(2026, 7, 16, 19, 45, 0, TimeSpan.FromHours(8));
        await WriteAuthorizationAsync(temporary.Path, token, now.AddMinutes(5));
        QualificationAttemptLedger ledger = CreateLedger(temporary.Path, now);

        ClaimedProviderAuthorization claim = await ledger.ClaimAsync("attempt-0002", token, ExactCommand);

        string startedPath = System.IO.Path.Combine(temporary.Path, "attempt-0002", "started.json");
        Assert.True(File.Exists(startedPath));
        string startedJson = await File.ReadAllTextAsync(startedPath);
        using FileStream exclusiveRead = new(startedPath, FileMode.Open, FileAccess.Read, FileShare.None);
        using JsonDocument started = await JsonDocument.ParseAsync(exclusiveRead);
        Assert.Equal("attempt-0002", started.RootElement.GetProperty("attemptId").GetString());
        Assert.Equal(ExactCommand, started.RootElement.GetProperty("exactCommand").GetString());
        Assert.Equal(PlanCommit, started.RootElement.GetProperty("correctivePlanCommit").GetString());
        Assert.Equal(PlanReviewSha256, started.RootElement.GetProperty("planReviewSha256").GetString());
        Assert.Equal(ApprovalObservedAt, started.RootElement.GetProperty("workflowApprovalObservedAt").GetString());
        Assert.DoesNotContain(token, startedJson, StringComparison.Ordinal);
        Assert.NotNull(ProviderExecutionGuard.Issue(claim));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Complete_WhenClaimIsValid_CreatesOneRedactedTerminalRecordAsync()
    {
        using TemporaryAttemptDirectory temporary = new();
        const string token = "synthetic-terminal-token";
        DateTimeOffset now = new(2026, 7, 16, 19, 45, 0, TimeSpan.FromHours(8));
        await WriteAuthorizationAsync(temporary.Path, token, now.AddMinutes(5));
        QualificationAttemptLedger ledger = CreateLedger(temporary.Path, now);
        ClaimedProviderAuthorization claim = await ledger.ClaimAsync("attempt-0002", token, ExactCommand);

        await ledger.CompleteAsync(
            claim,
            "failed",
            "unsafe-evidence",
            "specs/001-codegraph-extraction/qualification/evidence/final-gate.md");

        string terminalPath = System.IO.Path.Combine(temporary.Path, "attempt-0002", "terminal.json");
        string terminalJson = await File.ReadAllTextAsync(terminalPath);
        Assert.DoesNotContain(token, terminalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("provider output", terminalJson, StringComparison.OrdinalIgnoreCase);
        using JsonDocument terminal = JsonDocument.Parse(terminalJson);
        Assert.Equal("failed", terminal.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("unsafe-evidence", terminal.RootElement.GetProperty("category").GetString());
        await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.CompleteAsync(
            claim,
            "failed",
            "unsafe-evidence",
            "specs/001-codegraph-extraction/qualification/evidence/final-gate.md"));
    }

    [Theory]
    [InlineData("tokenSha256", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("expiresAt", "2026-07-16T19:44:59+08:00")]
    [InlineData("exactCommand", "dotnet test broad-filter")]
    [InlineData("correctivePlanCommit", "0000000000000000000000000000000000000000")]
    [InlineData("planReviewPath", "specs/001-codegraph-extraction/reviews/plan-review-7.json")]
    [InlineData("planReviewSha256", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("workflowApprovalObservedAt", "2026-07-16T18:09:59+08:00")]
    [InlineData("workflowApprovalSummarySha256", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Claim_WhenAuthorizationBoundaryDiffers_RejectsBeforeStartAsync(string propertyName, string value)
    {
        using TemporaryAttemptDirectory temporary = new();
        const string token = "synthetic-rejection-token";
        DateTimeOffset now = new(2026, 7, 16, 19, 45, 0, TimeSpan.FromHours(8));
        await WriteAuthorizationAsync(temporary.Path, token, now.AddMinutes(5));
        string authorizationPath = System.IO.Path.Combine(temporary.Path, "attempt-0002", "authorized.json");
        JsonObject authorization = JsonNode.Parse(await File.ReadAllTextAsync(authorizationPath))!.AsObject();
        authorization[propertyName] = value;
        await File.WriteAllTextAsync(authorizationPath, authorization.ToJsonString());
        QualificationAttemptLedger ledger = CreateLedger(temporary.Path, now);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ledger.ClaimAsync("attempt-0002", token, ExactCommand));

        Assert.False(File.Exists(System.IO.Path.Combine(temporary.Path, "attempt-0002", "started.json")));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Claim_WhenAttemptHasExistingState_RejectsReuseWithoutOverwriteAsync()
    {
        using TemporaryAttemptDirectory temporary = new();
        const string token = "synthetic-reuse-token";
        DateTimeOffset now = new(2026, 7, 16, 19, 45, 0, TimeSpan.FromHours(8));
        await WriteAuthorizationAsync(temporary.Path, token, now.AddMinutes(5));
        QualificationAttemptLedger ledger = CreateLedger(temporary.Path, now);
        await ledger.ClaimAsync("attempt-0002", token, ExactCommand);
        string startedPath = System.IO.Path.Combine(temporary.Path, "attempt-0002", "started.json");
        string original = await File.ReadAllTextAsync(startedPath);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ledger.ClaimAsync("attempt-0002", token, ExactCommand));

        Assert.Equal(original, await File.ReadAllTextAsync(startedPath));
    }

    private static QualificationAttemptLedger CreateLedger(string attemptsRoot, DateTimeOffset now) =>
        new(attemptsRoot, new QualificationAttemptLedgerPolicy(
            PlanCommit,
            PlanReviewPath,
            PlanReviewSha256,
            ApprovalObservedAt,
            Sha256(ApprovalSummary),
            () => now));

    private static async Task WriteAuthorizationAsync(string attemptsRoot, string token, DateTimeOffset expiresAt)
    {
        string directory = System.IO.Path.Combine(attemptsRoot, "attempt-0002");
        Directory.CreateDirectory(directory);
        object authorization = new
        {
            schemaVersion = "1.0",
            attemptId = "attempt-0002",
            tokenSha256 = Sha256(token),
            expiresAt,
            exactCommand = ExactCommand,
            correctivePlanCommit = PlanCommit,
            planReviewPath = PlanReviewPath,
            planReviewSha256 = PlanReviewSha256,
            workflowApprovalObservedAt = ApprovalObservedAt,
            workflowApprovalSummarySha256 = Sha256(ApprovalSummary),
        };
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(directory, "authorized.json"),
            JsonSerializer.Serialize(authorization));
    }

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class TemporaryAttemptDirectory : IDisposable
    {
        internal TemporaryAttemptDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"vulgata-attempt-ledger-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
