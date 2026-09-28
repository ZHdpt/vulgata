using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;

namespace Vulgata.Tests.PreparedSource;

public sealed class PreparedSourceGrantTests
{
    [Fact]
    public void Constructor_MissingAuthorityVerifier_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PreparedSourceContextIssuer(null!));
    }

    [Fact]
    public async Task IssueAsync_NullRequest_ThrowsArgumentNullException()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => issuer.IssueAsync(null!));
    }

    [Fact]
    public async Task IssueAsync_ExactApprovalTuple_ReturnsBoundContext()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);

        PreparedSourceIssueResult result = await issuer.IssueAsync(CreateRequest(fixture));

        Assert.Equal(PreparedSourceIssueStatus.Issued, result.Status);
        Assert.NotNull(result.Context);
        Assert.Equal(fixture.Approval.RepositoryId, result.Context.RepositoryId);
        Assert.Equal(fixture.Approval, fixture.Authority.LastRequestedApproval);
        AssertNoVersionIdentitySurface(result.Context);
        await result.Context.DisposeAsync();
    }

    [Theory]
    [InlineData("batch")]
    [InlineData("repository")]
    [InlineData("subject")]
    [InlineData("purpose")]
    [InlineData("approval")]
    [InlineData("root-binding")]
    public async Task IssueAsync_ChangedApprovalBinding_DeniesWithoutContext(string changedField)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        PreparedSourceApproval changedApproval = ChangeField(fixture.Approval, changedField);

        PreparedSourceIssueResult result = await issuer.IssueAsync(
            CreateRequest(fixture, approval: changedApproval));

        Assert.Equal(PreparedSourceIssueStatus.Denied, result.Status);
        Assert.Null(result.Context);
        AssertFailureIsSanitized(result, fixture);
    }

    [Theory]
    [InlineData("absent", PreparedSourceIssueStatus.Denied)]
    [InlineData("revoked", PreparedSourceIssueStatus.Denied)]
    [InlineData("unavailable", PreparedSourceIssueStatus.AuthorityUnavailable)]
    public async Task IssueAsync_NoCurrentApproval_FailsClosedAndSanitized(
        string authorityState,
        PreparedSourceIssueStatus expectedStatus)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        switch (authorityState)
        {
            case "absent":
                fixture.Authority.ApproveMissing();
                break;
            case "revoked":
                fixture.Authority.Revoke();
                break;
            case "unavailable":
                fixture.Authority.MakeUnavailable();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(authorityState));
        }

        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        PreparedSourceIssueResult result = await issuer.IssueAsync(CreateRequest(fixture));

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Context);
        AssertFailureIsSanitized(result, fixture);
    }

    [Fact]
    public async Task IssueAsync_LabelRemainsDescriptiveAndDoesNotBecomeVersionIdentity()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);

        PreparedSourceIssueResult result = await issuer.IssueAsync(CreateRequest(fixture));

        Assert.Equal(PreparedSourceIssueStatus.Issued, result.Status);
        Assert.NotNull(result.Context);
        Assert.Equal(fixture.Approval.OperatorLabel, fixture.Authority.LastRequestedApproval?.OperatorLabel);
        AssertNoVersionIdentitySurface(result.Context);
        await result.Context.DisposeAsync();
    }

    [Fact]
    public async Task IssueAsync_NonDirectoryRoot_FailsWithoutLeakingFileHandle()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string invalidRootPath = Path.Combine(fixture.SiblingPath, "not-a-directory");
        await File.WriteAllTextAsync(invalidRootPath, "not a directory");
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);

        PreparedSourceIssueResult result = await issuer.IssueAsync(
            CreateRequest(fixture, physicalRootPath: invalidRootPath));

        Assert.Equal(PreparedSourceIssueStatus.RootUnavailable, result.Status);
        Assert.Null(result.Context);
        AssertFailureIsSanitized(result, fixture, invalidRootPath);

        string renamedRootPath = invalidRootPath + ".released";
        File.Move(invalidRootPath, renamedRootPath);
        File.Delete(renamedRootPath);
        Assert.False(File.Exists(invalidRootPath));
        Assert.Equal("outside the approved prepared root", await File.ReadAllTextAsync(fixture.SentinelPath));
    }

    [Fact]
    public async Task IssueAsync_PreCancelledRequest_ReturnsCancelledWithoutContext()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        PreparedSourceIssueResult result = await issuer.IssueAsync(
            CreateRequest(fixture),
            cancellation.Token);

        Assert.Equal(PreparedSourceIssueStatus.Cancelled, result.Status);
        Assert.Null(result.Context);
        AssertFailureIsSanitized(result, fixture);
    }

    [Fact]
    public async Task IssueAsync_ExpiredGrant_ReturnsDeadlineExceededWithoutContext()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        PreparedSourceGrantLimits expiredLimits = PreparedSourceGrantLimits.Default with
        {
            Deadline = TimeSpan.Zero,
        };

        PreparedSourceIssueResult result = await issuer.IssueAsync(
            CreateRequest(fixture, limits: expiredLimits));

        Assert.Equal(PreparedSourceIssueStatus.DeadlineExceeded, result.Status);
        Assert.Null(result.Context);
        AssertFailureIsSanitized(result, fixture);
    }

    private static PreparedSourceGrantRequest CreateRequest(
        PreparedSourceFixture fixture,
        PreparedSourceApproval? approval = null,
        string? physicalRootPath = null,
        PreparedSourceGrantLimits? limits = null) =>
        new(
            approval ?? fixture.Approval,
            physicalRootPath ?? fixture.PreparedRootPath,
            limits ?? PreparedSourceGrantLimits.Default);

    private static PreparedSourceApproval ChangeField(
        PreparedSourceApproval approval,
        string field) =>
        field switch
        {
            "batch" => approval with { BatchId = Guid.NewGuid() },
            "repository" => approval with { RepositoryId = Guid.NewGuid() },
            "subject" => approval with { SubjectId = "another-subject" },
            "purpose" => approval with { Purpose = (PreparedSourcePurpose)int.MaxValue },
            "approval" => approval with { ApprovalIdentity = "another-approval" },
            "root-binding" => approval with { RootBindingIdentity = "another-root-binding" },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

    private static void AssertFailureIsSanitized(
        PreparedSourceIssueResult result,
        PreparedSourceFixture fixture,
        string? additionalSecret = null)
    {
        string serialized = JsonSerializer.Serialize(result);
        string[] forbiddenValues =
        [
            fixture.PreparedRootPath,
            fixture.Approval.SubjectId,
            fixture.Approval.ApprovalIdentity,
            fixture.Approval.RootBindingIdentity,
            fixture.Approval.OperatorLabel,
        ];

        foreach (string forbiddenValue in forbiddenValues)
        {
            Assert.False(
                serialized.Contains(forbiddenValue, StringComparison.Ordinal),
                $"Sanitized failure exposed a protected value: {forbiddenValue}");
        }

        if (additionalSecret is not null)
        {
            Assert.False(
                serialized.Contains(additionalSecret, StringComparison.Ordinal),
                "Sanitized failure exposed the supplied physical root path.");
        }
    }

    private static void AssertNoVersionIdentitySurface(IPreparedSourceReadContext context)
    {
        string[] forbiddenPropertyNames =
        [
            "ProfiledRevision",
            "SourceSnapshot",
            "SourceSnapshotId",
            "GitCommit",
            "CommitSha",
        ];
        string[] publicPropertyNames = context.GetType()
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        foreach (string forbiddenPropertyName in forbiddenPropertyNames)
        {
            Assert.DoesNotContain(forbiddenPropertyName, publicPropertyNames);
        }

        string serialized = JsonSerializer.Serialize(context);
        Assert.DoesNotContain("ProfiledRevision", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceSnapshot", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("GitCommit", serialized, StringComparison.Ordinal);
    }
}
