using System.Collections.Immutable;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October;

public sealed class OctoberSetupStoreTests
{
    private const string TargetId = "october-target-east";
    private const string SubjectId = "identity-subject-42";
    private const string CanonicalDatabaseIdentity = "postgres://cluster-east/october-2026-10";
    private const string ManagementAuthorityId = "ordinary-management-a";
    private const string InitialDigest = "18c7776d24646a2f8f9e756315dd637ffc02ba30a6db58a84c86efb6043f11bc";
    private static readonly Guid BatchId = Guid.Parse("27c3fdb2-17f5-4c28-8475-65fbd6fb8a8d");
    private static readonly Guid MemberRepositoryId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid MemberSystemId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid StandaloneRepositoryId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid GlobalContextId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly DateTimeOffset FirstCommitUtc = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommitAsync_PersistsCompleteMultiRepositorySelection_AndIdenticalRetryKeepsFirstUtcTimeAsync()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        (OctoberSetupRequest request, OctoberAuthorizedSelection authority) = CreateSelection();

        OctoberPrepareResult created = await fixture.Writer.CommitAsync(request, authority);
        OctoberAuthorizedSelection freshAuthority = CreateFreshEquivalentAuthority(authority);
        Assert.NotSame(authority, freshAuthority);
        Assert.Equal(authority.SelectionDigest, freshAuthority.SelectionDigest);
        OctoberPrepareResult replay = await fixture.Writer.CommitAsync(request, freshAuthority);

        Assert.Equal(OctoberPrepareStatus.Created, created.Status);
        Assert.Equal(
            new OctoberCommittedBatch(TargetId, BatchId, CanonicalDatabaseIdentity, InitialDigest, FirstCommitUtc),
            created.CommittedBatch);
        Assert.Equal(OctoberPrepareStatus.AlreadyIdentical, replay.Status);
        Assert.Equal(created.CommittedBatch, replay.CommittedBatch);
        Assert.Equal(FirstCommitUtc, replay.CommittedBatch!.SelectedAtUtc);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("root-binding")]
    [InlineData("advisory-value-and-differences")]
    [InlineData("context-origin")]
    [InlineData("applicability")]
    [InlineData("management-authority")]
    public async Task CommitAsync_ChangedImmutableSetupContent_ReturnsConflictWithoutReplacingOriginalAsync(
        string changedPart)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        (OctoberSetupRequest request, OctoberAuthorizedSelection authority) = CreateSelection();
        OctoberPrepareResult created = await fixture.Writer.CommitAsync(request, authority);
        (OctoberSetupRequest changedRequest, OctoberAuthorizedSelection changedAuthority) =
            ChangeSemanticContent(request, authority, changedPart);
        OctoberAuthorizedSelection freshAuthority = CreateFreshEquivalentAuthority(authority);

        OctoberPrepareResult conflict = await fixture.Writer.CommitAsync(changedRequest, changedAuthority);
        OctoberPrepareResult originalReplay = await fixture.Writer.CommitAsync(request, freshAuthority);

        Assert.Equal(OctoberPrepareStatus.Created, created.Status);
        Assert.Equal(OctoberPrepareStatus.Conflict, conflict.Status);
        Assert.Null(conflict.CommittedBatch);
        Assert.Equal(OctoberPrepareStatus.AlreadyIdentical, originalReplay.Status);
        Assert.Equal(created.CommittedBatch, originalReplay.CommittedBatch);
        Assert.Equal(FirstCommitUtc, originalReplay.CommittedBatch!.SelectedAtUtc);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommitAsync_PreCommitCancellationLeavesNoBatchOrCommittedIdentityAsync()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        (OctoberSetupRequest request, OctoberAuthorizedSelection authority) = CreateSelection();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        OctoberPrepareResult cancelled = await fixture.Writer.CommitAsync(request, authority, cancellation.Token);
        OctoberPrepareResult firstCommit = await fixture.Writer.CommitAsync(request, authority);

        Assert.Equal(OctoberPrepareStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.CommittedBatch);
        Assert.Equal(OctoberPrepareStatus.Created, firstCommit.Status);
        Assert.Equal(FirstCommitUtc, firstCommit.CommittedBatch!.SelectedAtUtc);
    }

    private static (OctoberSetupRequest Request, OctoberAuthorizedSelection Authority) CreateSelection()
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
            InitialDigest);
        OctoberSelectedAdvisoryContext context = new(
            ManagementAuthorityId,
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
                    StandaloneRepositoryId, string.Empty)),
            ImmutableArray.Create(
                new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.Global, OctoberContextScope.System),
                new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.Global, OctoberContextScope.Repository),
                new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.System, OctoberContextScope.Repository),
                new OctoberContextDifference(StandaloneRepositoryId, OctoberContextScope.Global, OctoberContextScope.Repository)));
        Assert.Equal(InitialDigest, OctoberSelectionDigest.Compute(request, context));

        OctoberAuthorizedSelection authority = new(
            TargetId,
            BatchId,
            SubjectId,
            CanonicalDatabaseIdentity,
            [MemberRepositoryId, StandaloneRepositoryId],
            InitialDigest,
            context);
        return (request, authority);
    }

    private static (OctoberSetupRequest Request, OctoberAuthorizedSelection Authority) ChangeSemanticContent(
        OctoberSetupRequest request,
        OctoberAuthorizedSelection authority,
        string changedPart)
    {
        OctoberSelectedAdvisoryContext context = authority.SelectedContext!;
        OctoberRepositorySelection[] repositories = request.Repositories.ToArray();
        OctoberContextSelection[] references = request.ContextSelections.ToArray();

        switch (changedPart)
        {
            case "root-binding":
                repositories[0] = repositories[0] with { RootBindingIdentity = "member-root-v2" };
                context = context with
                {
                    Repositories = context.Repositories.SetItem(0, repositories[0]),
                };
                break;
            case "advisory-value-and-differences":
                OctoberAdvisoryContextEntry[] valueEntries = context.Entries.ToArray();
                valueEntries[0] = valueEntries[0] with { Value = "system advisory" };
                valueEntries[3] = valueEntries[3] with { Value = "system advisory" };
                context = context with
                {
                    Entries = valueEntries.ToImmutableArray(),
                    Differences = context.Differences.Remove(
                        new OctoberContextDifference(MemberRepositoryId,
                            OctoberContextScope.Global, OctoberContextScope.System)),
                };
                break;
            case "context-origin":
                Guid replacementGlobalId = Guid.Parse("55555555-5555-4555-8555-555555555555");
                references[0] = references[0] with { RecordId = replacementGlobalId };
                OctoberAdvisoryContextEntry[] originEntries = context.Entries.ToArray();
                originEntries[0] = originEntries[0] with { OriginRecordId = replacementGlobalId };
                originEntries[3] = originEntries[3] with { OriginRecordId = replacementGlobalId };
                context = context with { Entries = originEntries.ToImmutableArray() };
                break;
            case "applicability":
                references = references.Where(reference =>
                    reference is not { Scope: OctoberContextScope.Repository, RecordId: var id } ||
                    id != MemberRepositoryId).ToArray();
                OctoberAdvisoryContextEntry[] applicabilityEntries = context.Entries.ToArray();
                applicabilityEntries[2] = applicabilityEntries[2] with
                {
                    Applicability = OctoberContextApplicability.NotSelected,
                    OriginRecordId = null,
                    Value = null,
                };
                context = context with
                {
                    Entries = applicabilityEntries.ToImmutableArray(),
                    Differences = context.Differences
                        .Where(difference => difference.RepositoryId != MemberRepositoryId ||
                            (difference.LeftScope != OctoberContextScope.Repository &&
                                difference.RightScope != OctoberContextScope.Repository))
                        .ToImmutableArray(),
                };
                break;
            case "management-authority":
                context = context with { ManagementAuthorityId = "ordinary-management-b" };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(changedPart), changedPart, "Unknown fixture mutation.");
        }

        OctoberSetupRequest changedRequest = request with
        {
            Repositories = repositories,
            ContextSelections = references,
        };
        string changedDigest = OctoberSelectionDigest.Compute(changedRequest, context);
        changedRequest = changedRequest with { SelectionDigest = changedDigest };
        OctoberAuthorizedSelection changedAuthority = authority with
        {
            RepositoryIds = repositories.Select(repository => repository.RepositoryId).ToArray(),
            SelectionDigest = changedDigest,
            SelectedContext = context,
        };
        return (changedRequest, changedAuthority);
    }

    private static OctoberAuthorizedSelection CreateFreshEquivalentAuthority(
        OctoberAuthorizedSelection original)
    {
        OctoberSelectedAdvisoryContext context = original.SelectedContext!;
        OctoberSelectedAdvisoryContext freshContext = context with
        {
            Repositories = context.Repositories.Select(repository => repository with { }).ToImmutableArray(),
            Entries = context.Entries.Select(entry => entry with { }).ToImmutableArray(),
            Differences = context.Differences.Select(difference => difference with { }).ToImmutableArray(),
        };
        return original with
        {
            RepositoryIds = original.RepositoryIds.ToArray(),
            SelectedContext = freshContext,
        };
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

    private sealed class StoreFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private StoreFixture(SqliteConnection connection, OctoberDbContext database, IOctoberSetupWriter writer)
        {
            _connection = connection;
            Database = database;
            Writer = writer;
        }

        public OctoberDbContext Database { get; }

        public IOctoberSetupWriter Writer { get; }

        public static async Task<StoreFixture> CreateAsync()
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            DbContextOptions<OctoberDbContext> options = new DbContextOptionsBuilder<OctoberDbContext>()
                .UseSqlite(connection)
                .Options;
            OctoberDbContext database = new(options);
            await database.Database.EnsureCreatedAsync();
            IOctoberSetupWriter writer = new OctoberSetupStore(database, new FixedTimeProvider(FirstCommitUtc));
            return new StoreFixture(connection, database, writer);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
