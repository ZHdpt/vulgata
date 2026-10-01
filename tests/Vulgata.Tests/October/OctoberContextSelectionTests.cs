using System.Collections.Immutable;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October;

public sealed class OctoberContextSelectionTests
{
    private const string TargetId = "october-context-target-a";
    private const string SubjectId = "october-context-operator";
    private const string CanonicalDatabaseIdentity = "sqlite://owned/october-context-a";
    private const string ManagementAuthorityId = "context-management-authority";
    private static readonly Guid BatchId = Guid.Parse("71f1bce6-2308-4f2f-8e9b-7e80e7e48c81");
    private static readonly Guid MemberRepositoryId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid MemberSystemId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid StandaloneRepositoryId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid GlobalContextId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly DateTimeOffset SelectedAtUtc =
        new(2026, 10, 1, 11, 30, 0, TimeSpan.Zero);

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_ProjectsCompleteMemberAndStandaloneHistoryAfterAdvisoryEdits()
    {
        await using ContextFixture fixture = await ContextFixture.CreateAsync();
        SelectionBundle original = CreateSelection();
        OctoberPrepareResult committed = await fixture.Writer.CommitAsync(
            original.Request, original.Authority, CancellationToken.None);
        Assert.Equal(OctoberPrepareStatus.Created, committed.Status);
        Assert.Equal(SelectedAtUtc, committed.CommittedBatch!.SelectedAtUtc);

        OctoberSelectedAdvisoryContext originalContext = original.Authority.SelectedContext!;
        OctoberSelectedAdvisoryContext currentContext = originalContext with
        {
            Entries = originalContext.Entries
                .Select(entry => entry with
                {
                    Value = (entry.RepositoryId, entry.Scope) switch
                    {
                        (var id, OctoberContextScope.Global) when id == MemberRepositoryId => "Updated global advisory",
                        (var id, OctoberContextScope.Global) when id == StandaloneRepositoryId => "Updated global advisory",
                        (var id, OctoberContextScope.System) when id == MemberRepositoryId => "Updated system advisory",
                        (var id, OctoberContextScope.Repository) when id == MemberRepositoryId => "Updated member advisory",
                        (var id, OctoberContextScope.Repository) when id == StandaloneRepositoryId => "Updated standalone advisory",
                        _ => entry.Value,
                    },
                })
                .ToImmutableArray(),
        };
        string currentDigest = OctoberSelectionDigest.Compute(original.Request, currentContext);
        OctoberAuthorizedSelection freshAuthority = original.Authority with
        {
            SelectionDigest = currentDigest,
            SelectedContext = currentContext,
        };

        OctoberSelectionResult result = await fixture.Selector.BindAsync(
            new OctoberStartupSelection(TargetId, BatchId, freshAuthority), CancellationToken.None);

        Assert.Equal(OctoberSelectionStatus.Selected, result.Status);
        IOctoberHistoricalBoundSetupData bound =
            Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(result.BoundSetupData);
        OctoberBoundSetupSnapshot snapshot = bound.Snapshot;
        Assert.Equal(TargetId, snapshot.Request.TargetId);
        Assert.Equal(BatchId, snapshot.Request.BatchId);
        Assert.Equal(committed.CommittedBatch.SelectionDigest, snapshot.Request.SelectionDigest);
        Assert.Equal(committed.CommittedBatch.SelectionDigest, snapshot.Authority.SelectionDigest);
        Assert.NotEqual(currentDigest, snapshot.Request.SelectionDigest);
        Assert.Equal(SubjectId, snapshot.Authority.SubjectId);
        Assert.Equal(CanonicalDatabaseIdentity, snapshot.Authority.CanonicalDatabaseIdentity);
        Assert.Equal(ManagementAuthorityId, snapshot.Authority.SelectedContext!.ManagementAuthorityId);
        Assert.Equal(SelectedAtUtc, snapshot.SelectedAtUtc);

        Assert.Equal(
            new[]
            {
                new OctoberRepositorySelection(MemberRepositoryId, MemberSystemId, "member-root-v1", "member label"),
                new OctoberRepositorySelection(StandaloneRepositoryId, null, "standalone-root-v1", "standalone label"),
            },
            snapshot.Request.Repositories);
        Assert.Equal(new[] { MemberRepositoryId, StandaloneRepositoryId }, snapshot.Authority.RepositoryIds);
        Assert.Equal(
            new[]
            {
                new OctoberContextSelection(OctoberContextScope.Global, GlobalContextId),
                new OctoberContextSelection(OctoberContextScope.System, MemberSystemId),
                new OctoberContextSelection(OctoberContextScope.Repository, MemberRepositoryId),
                new OctoberContextSelection(OctoberContextScope.Repository, StandaloneRepositoryId),
            },
            snapshot.Request.ContextSelections);

        OctoberSelectedAdvisoryContext historical = snapshot.Authority.SelectedContext;
        Assert.Equal(
            new[]
            {
                Entry(MemberRepositoryId, OctoberContextScope.Global, OctoberContextApplicability.Selected,
                    GlobalContextId, "Global advisory"),
                Entry(MemberRepositoryId, OctoberContextScope.System, OctoberContextApplicability.Selected,
                    MemberSystemId, "global advisory"),
                Entry(MemberRepositoryId, OctoberContextScope.Repository, OctoberContextApplicability.Selected,
                    MemberRepositoryId, null),
                Entry(StandaloneRepositoryId, OctoberContextScope.Global, OctoberContextApplicability.Selected,
                    GlobalContextId, "Global advisory"),
                Entry(StandaloneRepositoryId, OctoberContextScope.System, OctoberContextApplicability.NotApplicable,
                    null, null),
                Entry(StandaloneRepositoryId, OctoberContextScope.Repository, OctoberContextApplicability.Selected,
                    StandaloneRepositoryId, string.Empty),
            },
            historical.Entries);
        Assert.Equal(
            new[]
            {
                new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.Global, OctoberContextScope.System),
                new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.Global, OctoberContextScope.Repository),
                new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.System, OctoberContextScope.Repository),
                new OctoberContextDifference(StandaloneRepositoryId, OctoberContextScope.Global, OctoberContextScope.Repository),
            },
            historical.Differences);
        Assert.NotEqual(currentContext.Entries.ToArray(), historical.Entries.ToArray());
    }

    private static SelectionBundle CreateSelection()
    {
        OctoberRepositorySelection member = new(
            MemberRepositoryId, MemberSystemId, "member-root-v1", "member label");
        OctoberRepositorySelection standalone = new(
            StandaloneRepositoryId, null, "standalone-root-v1", "standalone label");
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
            string.Empty);
        OctoberSelectedAdvisoryContext context = new(
            ManagementAuthorityId,
            ImmutableArray.Create(member, standalone),
            ImmutableArray.Create(
                Entry(MemberRepositoryId, OctoberContextScope.Global, OctoberContextApplicability.Selected,
                    GlobalContextId, "Global advisory"),
                Entry(MemberRepositoryId, OctoberContextScope.System, OctoberContextApplicability.Selected,
                    MemberSystemId, "global advisory"),
                Entry(MemberRepositoryId, OctoberContextScope.Repository, OctoberContextApplicability.Selected,
                    MemberRepositoryId, null),
                Entry(StandaloneRepositoryId, OctoberContextScope.Global, OctoberContextApplicability.Selected,
                    GlobalContextId, "Global advisory"),
                Entry(StandaloneRepositoryId, OctoberContextScope.System, OctoberContextApplicability.NotApplicable,
                    null, null),
                Entry(StandaloneRepositoryId, OctoberContextScope.Repository, OctoberContextApplicability.Selected,
                    StandaloneRepositoryId, string.Empty)),
            ImmutableArray.Create(
                new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.Global, OctoberContextScope.System),
                new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.Global, OctoberContextScope.Repository),
                new OctoberContextDifference(MemberRepositoryId, OctoberContextScope.System, OctoberContextScope.Repository),
                new OctoberContextDifference(StandaloneRepositoryId, OctoberContextScope.Global, OctoberContextScope.Repository)));
        string digest = OctoberSelectionDigest.Compute(request, context);
        request = request with { SelectionDigest = digest };
        OctoberAuthorizedSelection authority = new(
            TargetId,
            BatchId,
            SubjectId,
            CanonicalDatabaseIdentity,
            [MemberRepositoryId, StandaloneRepositoryId],
            digest,
            context);
        return new SelectionBundle(request, authority);
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

    private sealed record SelectionBundle(
        OctoberSetupRequest Request,
        OctoberAuthorizedSelection Authority);

    private sealed class ContextFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly OctoberDbContext _database;

        private ContextFixture(SqliteConnection connection, OctoberDbContext database)
        {
            _connection = connection;
            _database = database;
            Writer = new OctoberSetupStore(database, new FixedTimeProvider(SelectedAtUtc));
            Selector = new OctoberBatchSelector(database, TargetId, CanonicalDatabaseIdentity);
        }

        public IOctoberSetupWriter Writer { get; }

        public IOctoberBatchSelector Selector { get; }

        public static async Task<ContextFixture> CreateAsync()
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            DbContextOptions<OctoberDbContext> options = new DbContextOptionsBuilder<OctoberDbContext>()
                .UseSqlite(connection)
                .Options;
            OctoberDbContext database = new(options);
            await database.Database.EnsureCreatedAsync();
            return new ContextFixture(connection, database);
        }

        public async ValueTask DisposeAsync()
        {
            await _database.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
