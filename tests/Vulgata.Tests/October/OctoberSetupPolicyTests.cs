using System.Collections.Immutable;
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
    private const string SelectionDigest = "2e958b9215aff6bbc22469d06d57476982f7c23e61ff068d79d7c6b7aabb34a5";
    private const string FullSelectionDigest = "18c7776d24646a2f8f9e756315dd637ffc02ba30a6db58a84c86efb6043f11bc";
    private const string RootBindingIdentity = "prepared-root-v3";
    private const string OperatorLabel = "october-bootstrap";
    private static readonly DateTimeOffset SelectedAtUtc = new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid MemberRepositoryId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid MemberSystemId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid StandaloneRepositoryId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid GlobalContextId = Guid.Parse("44444444-4444-4444-8444-444444444444");

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
            ContextSelections: [new OctoberContextSelection(OctoberContextScope.Repository, RepositoryId)],
            SelectionDigest: SelectionDigest);
        OctoberAuthorizedSelection authority = new(
            TargetId,
            BatchId,
            SubjectId,
            CanonicalDatabaseIdentity,
            [RepositoryId],
            SelectionDigest,
            CreateStandaloneContext());
        CapturingSetupWriter writer = new();
        OctoberBatchSetupPolicy policy = new(writer);

        OctoberPrepareResult result = await policy.PrepareAsync(request, authority);

        Assert.Equal(OctoberPrepareStatus.Created, result.Status);
        Assert.Equal(
            new OctoberCommittedBatch(TargetId, BatchId, CanonicalDatabaseIdentity, SelectionDigest, SelectedAtUtc),
            result.CommittedBatch);
        Assert.NotNull(writer.Request);
        Assert.Equal(TargetId, writer.Request!.TargetId);
        Assert.Equal(BatchId, writer.Request.BatchId);
        Assert.Equal(SelectionDigest, writer.Request.SelectionDigest);
        Assert.Equal(
            [new OctoberContextSelection(OctoberContextScope.Repository, RepositoryId)],
            writer.Request.ContextSelections);
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
        AssertStandaloneSnapshot(writer.Authority.SelectedContext);
        Assert.Equal(SelectedAtUtc, result.CommittedBatch!.SelectedAtUtc);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OctoberSelectionDigest_UsesCanonicalVersionTwoEncoding()
    {
        (OctoberSetupRequest request, OctoberSelectedAdvisoryContext context) = CreateFullSelection();

        string digest = OctoberSelectionDigest.Compute(request, context);

        Assert.Equal(FullSelectionDigest, digest);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OctoberSelectionDigest_IsStableAcrossOrderingAndIgnoresSuppliedDigest()
    {
        (OctoberSetupRequest request, OctoberSelectedAdvisoryContext context) = CreateFullSelection();
        OctoberSetupRequest reordered = request with
        {
            SelectionDigest = "caller-supplied-value-is-not-an-input",
            Repositories = request.Repositories.Reverse().ToArray(),
            ContextSelections = request.ContextSelections.Reverse().ToArray(),
        };
        OctoberSelectedAdvisoryContext reorderedContext = context with
        {
            Repositories = context.Repositories.Reverse().ToImmutableArray(),
            Entries = context.Entries.Reverse().ToImmutableArray(),
            Differences = context.Differences.Reverse().ToImmutableArray(),
        };

        Assert.Equal(FullSelectionDigest, OctoberSelectionDigest.Compute(reordered, reorderedContext));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("default-repositories")]
    [InlineData("default-entries")]
    [InlineData("duplicate-entry")]
    [InlineData("unknown-scope")]
    [InlineData("unpaired-surrogate")]
    public void OctoberSelectionDigest_RejectsStructurallyInvalidPayload(string invalidCase)
    {
        (OctoberSetupRequest request, OctoberSelectedAdvisoryContext context) = CreateFullSelection();
        context = invalidCase switch
        {
            "default-repositories" => context with { Repositories = default },
            "default-entries" => context with { Entries = default },
            "duplicate-entry" => context with { Entries = context.Entries.Add(context.Entries[0]) },
            "unknown-scope" => context with
            {
                Entries = context.Entries.SetItem(0, context.Entries[0] with { Scope = (OctoberContextScope)99 }),
            },
            "unpaired-surrogate" => context with
            {
                Entries = context.Entries.SetItem(0, context.Entries[0] with { Value = "\uD800" }),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(invalidCase), invalidCase, "Unknown invalid digest scenario."),
        };

        Assert.Throws<ArgumentException>(() => OctoberSelectionDigest.Compute(request, context));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing")]
    [InlineData("default-repositories")]
    [InlineData("default-entries")]
    [InlineData("repository-membership")]
    [InlineData("system-membership")]
    [InlineData("missing-slot")]
    [InlineData("duplicate-slot")]
    [InlineData("unknown-applicability")]
    [InlineData("wrong-origin")]
    [InlineData("wrong-evidence-kind")]
    [InlineData("missing-difference")]
    [InlineData("invented-difference")]
    [InlineData("context-reference-mismatch")]
    [InlineData("context-value-digest-mismatch")]
    public async Task PrepareAsync_InvalidTrustedContext_DeniesBeforeWriter(string invalidCase)
    {
        (OctoberSetupRequest request, OctoberAuthorizedSelection authority) = CreateFullSelectionAndAuthority();
        (request, authority) = ApplyInvalidContext(request, authority, invalidCase);
        CapturingSetupWriter writer = new()
        {
            Response = CreateWriterResponse(OctoberPrepareStatus.Created, "exact", FullSelectionDigest),
        };
        OctoberBatchSetupPolicy policy = new(writer);

        OctoberPrepareResult result = await policy.PrepareAsync(request, authority);

        Assert.Equal(OctoberPrepareStatus.Denied, result.Status);
        Assert.Null(result.CommittedBatch);
        Assert.Equal(0, writer.CallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAsync_ValidContext_RecomputesDigestAndForwardsCompleteSnapshotAndFrozenInputs()
    {
        (OctoberSetupRequest request, OctoberAuthorizedSelection authority) = CreateFullSelectionAndAuthority();
        List<OctoberRepositorySelection> callerRepositories = request.Repositories.ToList();
        List<OctoberContextSelection> callerReferences = request.ContextSelections.ToList();
        List<Guid> callerAuthorityRepositoryIds = authority.RepositoryIds.ToList();
        request = request with { Repositories = callerRepositories, ContextSelections = callerReferences };
        authority = authority with { RepositoryIds = callerAuthorityRepositoryIds };
        CapturingSetupWriter writer = new()
        {
            Response = CreateWriterResponse(OctoberPrepareStatus.Created, "exact", FullSelectionDigest),
            OnCommit = () =>
            {
                callerRepositories.Clear();
                callerReferences.Clear();
                callerAuthorityRepositoryIds.Clear();
            },
        };
        OctoberBatchSetupPolicy policy = new(writer);

        OctoberPrepareResult result = await policy.PrepareAsync(request, authority);

        Assert.Equal(OctoberPrepareStatus.Created, result.Status);
        Assert.Equal(SelectedAtUtc, result.CommittedBatch!.SelectedAtUtc);
        Assert.NotNull(writer.Request);
        Assert.Equal(2, writer.Request!.Repositories.Count);
        Assert.Equal([MemberRepositoryId, StandaloneRepositoryId],
            writer.Request.Repositories.Select(repository => repository.RepositoryId));
        Assert.Equal(4, writer.Request.ContextSelections.Count);
        Assert.Equal(2, writer.Authority!.RepositoryIds.Count);
        Assert.Equal([MemberRepositoryId, StandaloneRepositoryId], writer.Authority.RepositoryIds);
        AssertFullSnapshot(writer.Request, writer.Authority.SelectedContext);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("request-digest")]
    [InlineData("authority-digest")]
    [InlineData("context-value")]
    public async Task PrepareAsync_RecomputedV2DigestMismatch_DeniesBeforeWriter(string mismatch)
    {
        (OctoberSetupRequest request, OctoberAuthorizedSelection authority) = CreateFullSelectionAndAuthority();
        request = mismatch == "request-digest"
            ? request with { SelectionDigest = new string('f', 64) }
            : request;
        authority = mismatch == "authority-digest"
            ? authority with { SelectionDigest = new string('f', 64) }
            : authority;
        authority = mismatch == "context-value"
            ? authority with
            {
                SelectedContext = authority.SelectedContext! with
                {
                    Entries = authority.SelectedContext.Entries.SetItem(0,
                        authority.SelectedContext.Entries[0] with { Value = "changed after authorization" }),
                },
            }
            : authority;
        CapturingSetupWriter writer = new()
        {
            Response = CreateWriterResponse(OctoberPrepareStatus.Created, "exact", FullSelectionDigest),
        };
        OctoberBatchSetupPolicy policy = new(writer);

        OctoberPrepareResult result = await policy.PrepareAsync(request, authority);

        Assert.Equal(OctoberPrepareStatus.Denied, result.Status);
        Assert.Null(result.CommittedBatch);
        Assert.Equal(0, writer.CallCount);
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
    [InlineData(OctoberPrepareStatus.Created, "missing-time", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.AlreadyIdentical, "missing-time", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.Created, "non-utc-time", OctoberPrepareStatus.StorageFailure, false)]
    [InlineData(OctoberPrepareStatus.AlreadyIdentical, "non-utc-time", OctoberPrepareStatus.StorageFailure, false)]
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
                new OctoberCommittedBatch(TargetId, BatchId, CanonicalDatabaseIdentity, SelectionDigest, SelectedAtUtc),
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

    private static (OctoberSetupRequest Request, OctoberSelectedAdvisoryContext Context) CreateFullSelection()
    {
        OctoberRepositorySelection member = new(
            MemberRepositoryId, MemberSystemId, "member-root-v1", "operator-member");
        OctoberRepositorySelection standalone = new(
            StandaloneRepositoryId, null, "standalone-root-v1", "operator-standalone");
        OctoberSetupRequest request = new(
            TargetId,
            BatchId,
            [member, standalone],
            [
                new OctoberContextSelection(OctoberContextScope.Global, GlobalContextId),
                new OctoberContextSelection(OctoberContextScope.System, MemberSystemId),
                new OctoberContextSelection(OctoberContextScope.Repository, MemberRepositoryId),
                new OctoberContextSelection(OctoberContextScope.Repository, StandaloneRepositoryId),
            ],
            FullSelectionDigest);
        OctoberSelectedAdvisoryContext context = new(
            "ordinary-management-a",
            ImmutableArray.Create(member, standalone),
            ImmutableArray.Create(
                Entry(MemberRepositoryId, OctoberContextScope.Global, OctoberContextApplicability.Selected,
                    GlobalContextId, "global advisory"),
                Entry(MemberRepositoryId, OctoberContextScope.System, OctoberContextApplicability.Selected,
                    MemberSystemId, "system advisory"),
                Entry(MemberRepositoryId, OctoberContextScope.Repository, OctoberContextApplicability.Selected,
                    MemberRepositoryId, null),
                Entry(StandaloneRepositoryId, OctoberContextScope.Global, OctoberContextApplicability.Selected,
                    GlobalContextId, "global advisory"),
                Entry(StandaloneRepositoryId, OctoberContextScope.System, OctoberContextApplicability.NotApplicable,
                    null, null),
                Entry(StandaloneRepositoryId, OctoberContextScope.Repository, OctoberContextApplicability.Selected,
                    StandaloneRepositoryId, string.Empty)
            ),
            ImmutableArray.Create(
                new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.Global, OctoberContextScope.System),
                new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.Global, OctoberContextScope.Repository),
                new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.System, OctoberContextScope.Repository),
                new OctoberContextDifference(StandaloneRepositoryId, OctoberContextScope.Global, OctoberContextScope.Repository)
            ));
        return (request, context);
    }

    private static (OctoberSetupRequest Request, OctoberAuthorizedSelection Authority) CreateFullSelectionAndAuthority()
    {
        (OctoberSetupRequest request, OctoberSelectedAdvisoryContext context) = CreateFullSelection();
        OctoberAuthorizedSelection authority = new(
            TargetId,
            BatchId,
            SubjectId,
            CanonicalDatabaseIdentity,
            [MemberRepositoryId, StandaloneRepositoryId],
            FullSelectionDigest,
            context);
        return (request, authority);
    }

    private static OctoberSelectedAdvisoryContext CreateStandaloneContext()
    {
        OctoberRepositorySelection repository = new(
            RepositoryId, null, RootBindingIdentity, OperatorLabel);
        return new OctoberSelectedAdvisoryContext(
            "ordinary-management-a",
            ImmutableArray.Create(repository),
            ImmutableArray.Create(
                Entry(RepositoryId, OctoberContextScope.Global, OctoberContextApplicability.NotSelected, null, null),
                Entry(RepositoryId, OctoberContextScope.System, OctoberContextApplicability.NotApplicable, null, null),
                Entry(RepositoryId, OctoberContextScope.Repository, OctoberContextApplicability.Selected,
                    RepositoryId, string.Empty)
            ),
            ImmutableArray<OctoberContextDifference>.Empty);
    }

    private static OctoberAdvisoryContextEntry Entry(
        Guid repositoryId,
        OctoberContextScope scope,
        OctoberContextApplicability applicability,
        Guid? originRecordId,
        string? value) => new(
            repositoryId,
            scope,
            applicability,
            originRecordId,
            value,
            OctoberContextEvidenceKind.AdvisoryNotSourceProof);

    private static (OctoberSetupRequest Request, OctoberAuthorizedSelection Authority) ApplyInvalidContext(
        OctoberSetupRequest request,
        OctoberAuthorizedSelection authority,
        string invalidCase)
    {
        OctoberSelectedAdvisoryContext context = authority.SelectedContext!;
        OctoberAdvisoryContextEntry[] entries = context.Entries.ToArray();
        switch (invalidCase)
        {
            case "missing":
                return (request, authority with { SelectedContext = null });
            case "default-repositories":
                context = context with { Repositories = default };
                break;
            case "default-entries":
                context = context with { Entries = default };
                break;
            case "repository-membership":
                context = context with
                {
                    Repositories = context.Repositories.SetItem(1,
                        context.Repositories[1] with { RepositoryId = Guid.Parse("55555555-5555-4555-8555-555555555555") }),
                };
                break;
            case "system-membership":
                context = context with
                {
                    Repositories = context.Repositories.SetItem(0,
                        context.Repositories[0] with { SystemId = Guid.Parse("66666666-6666-4666-8666-666666666666") }),
                };
                break;
            case "missing-slot":
                context = context with { Entries = entries.Skip(1).ToImmutableArray() };
                break;
            case "duplicate-slot":
                context = context with { Entries = context.Entries.Add(context.Entries[0]) };
                break;
            case "unknown-applicability":
                context = context with
                {
                    Entries = context.Entries.SetItem(0,
                        context.Entries[0] with { Applicability = (OctoberContextApplicability)99 }),
                };
                break;
            case "wrong-origin":
                context = context with
                {
                    Entries = context.Entries.SetItem(0,
                        context.Entries[0] with { OriginRecordId = StandaloneRepositoryId }),
                };
                break;
            case "wrong-evidence-kind":
                context = context with
                {
                    Entries = context.Entries.SetItem(0,
                        context.Entries[0] with { EvidenceKind = (OctoberContextEvidenceKind)99 }),
                };
                break;
            case "missing-difference":
                context = context with { Differences = context.Differences.RemoveAt(0) };
                break;
            case "invented-difference":
                context = context with
                {
                    Differences = context.Differences.Add(new OctoberContextDifference(
                        StandaloneRepositoryId, OctoberContextScope.Global, OctoberContextScope.System)),
                };
                break;
            case "context-reference-mismatch":
                request = request with { ContextSelections = request.ContextSelections.Take(1).ToArray() };
                break;
            case "context-value-digest-mismatch":
                context = context with
                {
                    Entries = context.Entries.SetItem(0,
                        context.Entries[0] with { Value = "changed after authorization" }),
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidCase), invalidCase, "Unknown invalid-context scenario.");
        }

        return (request, authority with { SelectedContext = context });
    }

    private static void AssertStandaloneSnapshot(OctoberSelectedAdvisoryContext? context)
    {
        Assert.NotNull(context);
        Assert.Equal("ordinary-management-a", context!.ManagementAuthorityId);
        OctoberRepositorySelection repository = Assert.Single(context.Repositories);
        Assert.Equal(RepositoryId, repository.RepositoryId);
        Assert.Null(repository.SystemId);
        Assert.Equal(3, context.Entries.Length);
        Assert.Equal(OctoberContextApplicability.NotSelected, context.Entries[0].Applicability);
        Assert.Null(context.Entries[0].OriginRecordId);
        Assert.Null(context.Entries[0].Value);
        Assert.Equal(OctoberContextApplicability.NotApplicable, context.Entries[1].Applicability);
        Assert.Null(context.Entries[1].OriginRecordId);
        Assert.Null(context.Entries[1].Value);
        Assert.Equal(OctoberContextApplicability.Selected, context.Entries[2].Applicability);
        Assert.Equal(RepositoryId, context.Entries[2].OriginRecordId);
        Assert.Equal(string.Empty, context.Entries[2].Value);
        Assert.All(context.Entries, entry =>
            Assert.Equal(OctoberContextEvidenceKind.AdvisoryNotSourceProof, entry.EvidenceKind));
        Assert.Empty(context.Differences);
    }

    private static void AssertFullSnapshot(
        OctoberSetupRequest request,
        OctoberSelectedAdvisoryContext? context)
    {
        Assert.NotNull(context);
        Assert.Equal("ordinary-management-a", context!.ManagementAuthorityId);
        Assert.Equal(request.Repositories.OrderBy(repository => repository.RepositoryId), context.Repositories);
        Assert.Equal(6, context.Entries.Length);
        OctoberContextDifference[] expectedDifferences =
        [
            new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.Global, OctoberContextScope.System),
            new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.Global, OctoberContextScope.Repository),
            new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.System, OctoberContextScope.Repository),
            new OctoberContextDifference(StandaloneRepositoryId, OctoberContextScope.Global, OctoberContextScope.Repository),
        ];
        Assert.Equal(expectedDifferences.Length, context.Differences.Length);
        for (int index = 0; index < expectedDifferences.Length; index++)
        {
            Assert.Equal(expectedDifferences[index], context.Differences[index]);
        }
        OctoberAdvisoryContextEntry memberRepository = Assert.Single(context.Entries.Where(entry =>
            entry.RepositoryId == MemberRepositoryId && entry.Scope == OctoberContextScope.Repository));
        Assert.Equal(OctoberContextApplicability.Selected, memberRepository.Applicability);
        Assert.Equal(MemberRepositoryId, memberRepository.OriginRecordId);
        Assert.Null(memberRepository.Value);
        OctoberAdvisoryContextEntry standaloneGlobal = Assert.Single(context.Entries.Where(entry =>
            entry.RepositoryId == StandaloneRepositoryId && entry.Scope == OctoberContextScope.Global));
        Assert.Equal(OctoberContextApplicability.Selected, standaloneGlobal.Applicability);
        Assert.Equal(GlobalContextId, standaloneGlobal.OriginRecordId);
        Assert.Equal("global advisory", standaloneGlobal.Value);
        OctoberAdvisoryContextEntry standaloneSystem = Assert.Single(context.Entries.Where(entry =>
            entry.RepositoryId == StandaloneRepositoryId && entry.Scope == OctoberContextScope.System));
        Assert.Equal(OctoberContextApplicability.NotApplicable, standaloneSystem.Applicability);
        Assert.Null(standaloneSystem.OriginRecordId);
        Assert.Null(standaloneSystem.Value);
        OctoberAdvisoryContextEntry standaloneRepository = Assert.Single(context.Entries.Where(entry =>
            entry.RepositoryId == StandaloneRepositoryId && entry.Scope == OctoberContextScope.Repository));
        Assert.Equal(OctoberContextApplicability.Selected, standaloneRepository.Applicability);
        Assert.Equal(StandaloneRepositoryId, standaloneRepository.OriginRecordId);
        Assert.Equal(string.Empty, standaloneRepository.Value);
        OctoberAdvisoryContextEntry memberGlobal = Assert.Single(context.Entries.Where(entry =>
            entry.RepositoryId == MemberRepositoryId && entry.Scope == OctoberContextScope.Global));
        Assert.Equal(GlobalContextId, memberGlobal.OriginRecordId);
        Assert.Equal("global advisory", memberGlobal.Value);
        OctoberAdvisoryContextEntry memberSystem = Assert.Single(context.Entries.Where(entry =>
            entry.RepositoryId == MemberRepositoryId && entry.Scope == OctoberContextScope.System));
        Assert.Equal(MemberSystemId, memberSystem.OriginRecordId);
        Assert.Equal("system advisory", memberSystem.Value);
        Assert.All(context.Entries, entry =>
            Assert.Equal(OctoberContextEvidenceKind.AdvisoryNotSourceProof, entry.EvidenceKind));
    }

    private sealed class CapturingSetupWriter : IOctoberSetupWriter
    {
        public int CallCount { get; private set; }
        public OctoberSetupRequest? Request { get; private set; }
        public OctoberAuthorizedSelection? Authority { get; private set; }
        public OctoberPrepareResult Response { get; set; } = new(
            OctoberPrepareStatus.Created,
            new OctoberCommittedBatch(TargetId, BatchId, CanonicalDatabaseIdentity, SelectionDigest, SelectedAtUtc));
        public CancellationToken CancellationToken { get; private set; }
        public Action? OnCommit { get; init; }

        public Task<OctoberPrepareResult> CommitAsync(
            OctoberSetupRequest request,
            OctoberAuthorizedSelection authority,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Request = request;
            Authority = authority;
            CancellationToken = cancellationToken;
            OnCommit?.Invoke();
            return Task.FromResult(Response);
        }
    }

    private static OctoberPrepareResult CreateWriterResponse(
        OctoberPrepareStatus status,
        string identityShape,
        string selectionDigest = SelectionDigest)
    {
        OctoberCommittedBatch exactIdentity = new(TargetId, BatchId, CanonicalDatabaseIdentity, selectionDigest, SelectedAtUtc);
        OctoberCommittedBatch? identity = identityShape switch
        {
            "exact" => exactIdentity,
            "null" => null,
            "missing-time" => exactIdentity with { SelectedAtUtc = null },
            "non-utc-time" => exactIdentity with
            {
                SelectedAtUtc = new DateTimeOffset(2026, 9, 30, 1, 0, 0, TimeSpan.FromHours(1)),
            },
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
        ContextSelections: [new OctoberContextSelection(OctoberContextScope.Repository, RepositoryId)],
        SelectionDigest: SelectionDigest);

    private static OctoberAuthorizedSelection CreateAuthority() => new(
        TargetId,
        BatchId,
        SubjectId,
        CanonicalDatabaseIdentity,
        [RepositoryId],
        SelectionDigest,
        CreateStandaloneContext());
}
