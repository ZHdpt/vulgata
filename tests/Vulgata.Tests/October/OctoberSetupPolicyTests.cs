using Vulgata.Core.DomainServices;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October;

public sealed class OctoberSetupPolicyTests
{
    private static readonly Guid BatchId = Guid.Parse("27c3fdb2-17f5-4c28-8475-65fbd6fb8a8d");
    private static readonly Guid RepositoryId = Guid.Parse("b02b505e-1fb2-4f01-9bb4-8087882190bb");
    private const string TargetId = "october-target-east";
    private const string SubjectId = "identity-subject-42";
    private const string CanonicalDatabaseIdentity = "postgres://cluster-east/october-2026-10";
    private const string SelectionDigest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string RootBindingIdentity = "prepared-root-v3";
    private const string OperatorLabel = "october-bootstrap";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAsync_UsesTrustedExactSelection_AndKeepsStandaloneRepositoryStandalone()
    {
        OctoberSetupRequest request = new(
            TargetId,
            BatchId,
            [new OctoberRepositorySelection(
                RepositoryId,
                SystemId: null,
                RootBindingIdentity: RootBindingIdentity,
                OperatorLabel: OperatorLabel)],
            ContextSelections: [],
            SelectionDigest: SelectionDigest);
        OctoberAuthorizedSelection authority = new(
            TargetId,
            BatchId,
            SubjectId,
            CanonicalDatabaseIdentity,
            [RepositoryId],
            SelectionDigest);
        CapturingSetupWriter writer = new();
        OctoberBatchSetupPolicy policy = new(writer);

        OctoberPrepareResult result = await policy.PrepareAsync(request, authority);

        Assert.Equal(OctoberPrepareStatus.Created, result.Status);
        Assert.Equal(
            new OctoberCommittedBatch(TargetId, BatchId, CanonicalDatabaseIdentity, SelectionDigest),
            result.CommittedBatch);
        Assert.NotNull(writer.Request);
        Assert.Equal(TargetId, writer.Request!.TargetId);
        Assert.Equal(BatchId, writer.Request.BatchId);
        Assert.Equal(SelectionDigest, writer.Request.SelectionDigest);
        Assert.Empty(writer.Request.ContextSelections);
        OctoberRepositorySelection repository = Assert.Single(writer.Request!.Repositories);
        Assert.Equal(RepositoryId, repository.RepositoryId);
        Assert.Null(repository.SystemId);
        Assert.Equal(RootBindingIdentity, repository.RootBindingIdentity);
        Assert.Equal(OperatorLabel, repository.OperatorLabel);
        Assert.NotNull(writer.Authority);
        Assert.Equal(TargetId, writer.Authority!.TargetId);
        Assert.Equal(BatchId, writer.Authority.BatchId);
        Assert.Equal(SubjectId, writer.Authority.SubjectId);
        Assert.Equal(CanonicalDatabaseIdentity, writer.Authority.CanonicalDatabaseIdentity);
        Assert.Equal(SelectionDigest, writer.Authority.SelectionDigest);
        Assert.Equal([RepositoryId], writer.Authority.RepositoryIds);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("target")]
    [InlineData("batch")]
    [InlineData("repositories")]
    [InlineData("digest")]
    [InlineData("subject")]
    [InlineData("database-identity")]
    public async Task PrepareAsync_MismatchedOrMalformedAuthority_DeniesBeforeWriter(string mismatch)
    {
        OctoberSetupRequest request = CreateRequest();
        OctoberAuthorizedSelection authority = mismatch switch
        {
            "target" => CreateAuthority() with { TargetId = "october-target-west" },
            "batch" => CreateAuthority() with { BatchId = Guid.Parse("c01991bc-321d-4e7e-859d-734608a5e195") },
            "repositories" => CreateAuthority() with { RepositoryIds = [] },
            "digest" => CreateAuthority() with { SelectionDigest = new string('b', 64) },
            "subject" => CreateAuthority() with { SubjectId = " " },
            "database-identity" => CreateAuthority() with { CanonicalDatabaseIdentity = "" },
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch, "Unknown setup mismatch scenario."),
        };
        CapturingSetupWriter writer = new();
        OctoberBatchSetupPolicy policy = new(writer);

        OctoberPrepareResult result = await policy.PrepareAsync(request, authority);

        Assert.Equal(OctoberPrepareStatus.Denied, result.Status);
        Assert.Null(result.CommittedBatch);
        Assert.Equal(0, writer.CallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(OctoberPrepareStatus.Created, "exact", OctoberPrepareStatus.Created, true)]
    [InlineData(OctoberPrepareStatus.AlreadyIdentical, "exact", OctoberPrepareStatus.AlreadyIdentical, true)]
    [InlineData(OctoberPrepareStatus.Conflict, "null", OctoberPrepareStatus.Conflict, false)]
    [InlineData(OctoberPrepareStatus.Denied, "null", OctoberPrepareStatus.Denied, false)]
    [InlineData(OctoberPrepareStatus.AuthorityUnavailable, "null", OctoberPrepareStatus.AuthorityUnavailable, false)]
    [InlineData(OctoberPrepareStatus.Cancelled, "null", OctoberPrepareStatus.Cancelled, false)]
    [InlineData(OctoberPrepareStatus.StorageFailure, "null", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.Conflict, "wrong-target", OctoberPrepareStatus.Conflict, false)]
    [InlineData(OctoberPrepareStatus.Denied, "wrong-batch", OctoberPrepareStatus.Denied, false)]
    [InlineData(OctoberPrepareStatus.AuthorityUnavailable, "wrong-database", OctoberPrepareStatus.AuthorityUnavailable, false)]
    [InlineData(OctoberPrepareStatus.Cancelled, "wrong-digest", OctoberPrepareStatus.Cancelled, false)]
    [InlineData(OctoberPrepareStatus.StorageFailure, "exact", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.Created, "null", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.AlreadyIdentical, "null", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.Created, "wrong-target", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.Created, "wrong-batch", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.Created, "wrong-database", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.Created, "wrong-digest", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.AlreadyIdentical, "wrong-target", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.AlreadyIdentical, "wrong-batch", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.AlreadyIdentical, "wrong-database", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.AlreadyIdentical, "wrong-digest", OctoberPrepareStatus.StorageFailure, false)]
    public async Task PrepareAsync_ClosedWriterResult_ExposesOnlyExactSuccessfulIdentity(
        OctoberPrepareStatus writerStatus,
        string identityShape,
        OctoberPrepareStatus expectedStatus,
        bool expectCommittedBatch)
    {
        OctoberSetupRequest request = CreateRequest();
        OctoberAuthorizedSelection authority = CreateAuthority();
        CapturingSetupWriter writer = new()
        {
            Response = CreateWriterResponse(writerStatus, identityShape),
        };
        OctoberBatchSetupPolicy policy = new(writer);

        OctoberPrepareResult result = await policy.PrepareAsync(request, authority);

        Assert.Equal(expectedStatus, result.Status);
        if (expectCommittedBatch)
        {
            Assert.Equal(
                new OctoberCommittedBatch(TargetId, BatchId, CanonicalDatabaseIdentity, SelectionDigest),
                result.CommittedBatch);
        }
        else
        {
            Assert.Null(result.CommittedBatch);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAsync_ForwardsCancellationTokenToSetupWriter()
    {
        OctoberSetupRequest request = CreateRequest();
        OctoberAuthorizedSelection authority = CreateAuthority();
        using CancellationTokenSource cancellation = new();
        CapturingSetupWriter writer = new();
        OctoberBatchSetupPolicy policy = new(writer);

        await policy.PrepareAsync(request, authority, cancellation.Token);

        Assert.Equal(cancellation.Token, writer.CancellationToken);
    }

    private sealed class CapturingSetupWriter : IOctoberSetupWriter
    {
        public int CallCount { get; private set; }
        public OctoberSetupRequest? Request { get; private set; }
        public OctoberAuthorizedSelection? Authority { get; private set; }
        public OctoberPrepareResult Response { get; set; } = new(
            OctoberPrepareStatus.Created,
            new OctoberCommittedBatch(TargetId, BatchId, CanonicalDatabaseIdentity, SelectionDigest));
        public CancellationToken CancellationToken { get; private set; }

        public Task<OctoberPrepareResult> CommitAsync(
            OctoberSetupRequest request,
            OctoberAuthorizedSelection authority,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Request = request;
            Authority = authority;
            CancellationToken = cancellationToken;
            return Task.FromResult(Response);
        }
    }

    private static OctoberPrepareResult CreateWriterResponse(
        OctoberPrepareStatus status,
        string identityShape)
    {
        OctoberCommittedBatch exactIdentity = new(TargetId, BatchId, CanonicalDatabaseIdentity, SelectionDigest);
        OctoberCommittedBatch? identity = identityShape switch
        {
            "exact" => exactIdentity,
            "null" => null,
            "wrong-target" => exactIdentity with { TargetId = "october-target-west" },
            "wrong-batch" => exactIdentity with { BatchId = Guid.Parse("c01991bc-321d-4e7e-859d-734608a5e195") },
            "wrong-database" => exactIdentity with { CanonicalDatabaseIdentity = "postgres://cluster-west/wrong-database" },
            "wrong-digest" => exactIdentity with { SelectionDigest = new string('b', 64) },
            _ => throw new ArgumentOutOfRangeException(nameof(identityShape), identityShape, "Unknown committed identity scenario."),
        };

        return new OctoberPrepareResult(status, identity);
    }

    private static OctoberSetupRequest CreateRequest() => new(
        TargetId,
        BatchId,
        [new OctoberRepositorySelection(
            RepositoryId,
            SystemId: null,
            RootBindingIdentity: RootBindingIdentity,
            OperatorLabel: OperatorLabel)],
        ContextSelections: [],
        SelectionDigest: SelectionDigest);

    private static OctoberAuthorizedSelection CreateAuthority() => new(
        TargetId,
        BatchId,
        SubjectId,
        CanonicalDatabaseIdentity,
        [RepositoryId],
        SelectionDigest);
}
