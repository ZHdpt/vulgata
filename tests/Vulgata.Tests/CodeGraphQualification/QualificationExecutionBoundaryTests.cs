using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Tests.CodeGraphQualification;

[Trait("Category", "CodeGraphQualificationSynthetic")]
public sealed class QualificationExecutionBoundaryTests
{
    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Load_WhenRawUtf8BytesMatch_UsesOneClosedAttempt0003BoundaryAsync()
    {
        using TemporaryBoundaryDirectory temporary = new();
        byte[] bytes = Encoding.UTF8.GetBytes(CreateBoundaryJson());
        string path = Path.Combine(temporary.Path, "attempt-0003.json");
        await File.WriteAllBytesAsync(path, bytes);
        string expectedSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));

        QualificationExecutionBoundary boundary =
            await QualificationExecutionBoundary.LoadAsync(path, expectedSha256);

        Assert.Equal("attempt-0003", boundary.AttemptId);
        Assert.Equal(ExpectedCommand, boundary.ExactCommand);
        Assert.Equal(ExpectedProviderFact, boundary.ProviderFactIdentity);
        Assert.Equal("CodeGraphQualificationProvider", boundary.ProviderCategory);
        Assert.True(boundary.NoRetry);
        Assert.Equal(expectedSha256, boundary.RawSha256);
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Load_WhenEquivalentJsonBytesDrift_RejectsBeforeLedgerClaimAsync()
    {
        using TemporaryBoundaryDirectory temporary = new();
        byte[] approvedBytes = Encoding.UTF8.GetBytes(CreateBoundaryJson());
        string path = Path.Combine(temporary.Path, "attempt-0003.json");
        await File.WriteAllBytesAsync(path, approvedBytes);
        string approvedSha256 = Convert.ToHexStringLower(SHA256.HashData(approvedBytes));
        await File.AppendAllTextAsync(path, Environment.NewLine);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => QualificationExecutionBoundary.LoadAsync(path, approvedSha256));

        Assert.Equal("The qualification execution boundary bytes do not match the approved SHA-256.", exception.Message);
        Assert.False(Directory.Exists(Path.Combine(temporary.Path, "attempts", "attempt-0003")));
    }

    [Theory]
    [InlineData("""
        {
          "schemaVersion": "1.0",
          "schemaVersion": "1.0",
          "attemptId": "attempt-0003",
          "exactCommand": "synthetic",
          "correctivePlanCommit": "266f3b9591e022c6da2d4af58f7cef048e43b583",
          "planReviewPath": "specs/synthetic/review.json",
          "planReviewSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          "executionApprovalGateId": "synthetic-gate",
          "providerFactIdentity": "Synthetic.ProviderFact",
          "providerCategory": "CodeGraphQualificationProvider",
          "noRetry": true,
          "qualificationScopeId": "synthetic-scope"
        }
        """)]
    [InlineData("""
        {
          "schemaVersion": "1.0",
          "attemptId": "attempt-0003",
          "exactCommand": "synthetic",
          "correctivePlanCommit": "266f3b9591e022c6da2d4af58f7cef048e43b583",
          "planReviewPath": "specs/synthetic/review.json",
          "planReviewSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          "executionApprovalGateId": "synthetic-gate",
          "providerFactIdentity": "Synthetic.ProviderFact",
          "providerCategory": "CodeGraphQualificationProvider",
          "noRetry": true,
          "qualificationScopeId": "synthetic-scope",
          "unexpected": true
        }
        """)]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Load_WhenSchemaIsDuplicateOrOpen_RejectsAsync(string json)
    {
        using TemporaryBoundaryDirectory temporary = new();
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        string path = Path.Combine(temporary.Path, "attempt-0003.json");
        await File.WriteAllBytesAsync(path, bytes);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            QualificationExecutionBoundary.LoadAsync(
                path,
                Convert.ToHexStringLower(SHA256.HashData(bytes))));
    }

    internal const string ExpectedCommand =
        "pwsh -NoProfile -File tests/Vulgata.Tests/CodeGraphQualification/Invoke-CodeGraphQualificationProvider.ps1 -AttemptId attempt-0003";

    internal const string ExpectedProviderFact =
        "Vulgata.Tests.CodeGraphQualification.CgcProviderQualificationTests.Attempt0003_WhenAuthorizedAndUnclaimed_RecordsSchema12EvidenceAsync";

    internal static string CreateBoundaryJson(
        string planReviewPath = "specs/synthetic/plan-review.json",
        string planReviewSha256 = "f73f877b3c7d37b3c739ee123867e39e3b71769e4abd1d8bbe5bd2f51806b408") =>
        $$"""
        {
          "schemaVersion": "1.0",
          "attemptId": "attempt-0003",
          "exactCommand": "{{ExpectedCommand}}",
          "correctivePlanCommit": "266f3b9591e022c6da2d4af58f7cef048e43b583",
          "planReviewPath": "{{planReviewPath}}",
          "planReviewSha256": "{{planReviewSha256}}",
          "executionApprovalGateId": "story-4.1-attempt-0003-execution",
          "providerFactIdentity": "{{ExpectedProviderFact}}",
          "providerCategory": "CodeGraphQualificationProvider",
          "noRetry": true,
          "qualificationScopeId": "codegraphcontext-0.5.1-http-json"
        }
        """;

    private sealed class TemporaryBoundaryDirectory : IDisposable
    {
        internal TemporaryBoundaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"vulgata-execution-boundary-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
