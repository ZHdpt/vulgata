using System.Collections.Immutable;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October;

public sealed class OctoberBatchSelectorTests
{
    private const string TargetId = "october-selector-target-a";
    private const string OtherTargetId = "october-selector-target-b";
    private const string SubjectId = "october-selector-subject";
    private const string CanonicalDatabaseIdentity = "sqlite://owned/october-selector-a";
    private const string OtherCanonicalDatabaseIdentity = "sqlite://owned/october-selector-b";
    private const string ManagementAuthorityA = "ordinary-management-authority-a";
    private const string ManagementAuthorityB = "ordinary-management-authority-b";
    private const string RootBinding = "prepared-root-version-a";
    private const string OperatorLabel = "selector-fixture-operator";
    private const string GlobalValue = "original global advisory";
    private const string SystemValue = "original system advisory";
    private const string RepositoryValue = "original repository advisory";
    private static readonly Guid BatchId = Guid.Parse("9a84f31c-cf84-4eb8-a458-a936c202f066");
    private static readonly Guid OtherBatchId = Guid.Parse("3a873811-9967-46ba-a414-48595684873e");
    private static readonly Guid RepositoryId = Guid.Parse("c0a8bbd5-31e7-4e88-b10d-9cab2cd5c617");
    private static readonly Guid SystemId = Guid.Parse("925b562f-b5fc-4f6e-93b4-6b4ceff04717");
    private static readonly Guid GlobalContextId = Guid.Parse("226cb9bf-ddce-4b47-ae6c-8e47a9d9eabf");
    private static readonly Guid SystemContextId = Guid.Parse("8b31b46a-fc54-4a32-8a98-6c4c0b771c34");
    private static readonly DateTimeOffset SelectedAtUtc =
        new(2026, 9, 30, 9, 45, 0, TimeSpan.Zero);

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_ExactSelectableBatch_ReturnsStoredSnapshotWithoutMutation()
    {
        await using SelectorFixture fixture = await SelectorFixture.CreateAsync();
        SelectionBundle original = CreateSelection();
        await fixture.PrepareAsync(original);
        DatabaseCounts before = await fixture.ReadCountsAsync();

        OctoberSelectionResult result = await fixture.Selector.BindAsync(ToStartupSelection(original));

        Assert.Equal(OctoberSelectionStatus.Selected, result.Status);
        Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(result.BoundSetupData);
        IOctoberHistoricalBoundSetupData selected =
            Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(result.BoundSetupData);
        AssertSnapshotEquals(original, SelectedAtUtc, selected.Snapshot);
        Assert.Equal(before, await fixture.ReadCountsAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_UnknownTarget_ReturnsMissingTargetWithoutCapability()
    {
        await using SelectorFixture fixture = await SelectorFixture.CreateAsync();
        SelectionBundle original = CreateSelection();
        await fixture.PrepareAsync(original);
        DatabaseCounts before = await fixture.ReadCountsAsync();
        OctoberAuthorizedSelection foreignAuthority = original.Authority with
        {
            TargetId = OtherTargetId,
            CanonicalDatabaseIdentity = OtherCanonicalDatabaseIdentity,
        };

        OctoberSelectionResult result = await fixture.Selector.BindAsync(
            new OctoberStartupSelection(OtherTargetId, BatchId, foreignAuthority));

        AssertRejected(result, OctoberSelectionStatus.MissingTarget);
        Assert.Equal(before, await fixture.ReadCountsAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_NoExistingBatch_ReturnsMissingBatchAndDoesNotCreateOne()
    {
        await using SelectorFixture fixture = await SelectorFixture.CreateAsync();
        SelectionBundle selection = CreateSelection();
        DatabaseCounts before = await fixture.ReadCountsAsync();

        OctoberSelectionResult result = await fixture.Selector.BindAsync(ToStartupSelection(selection));

        AssertRejected(result, OctoberSelectionStatus.MissingBatch);
        Assert.Equal(before, await fixture.ReadCountsAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_DifferentBatchDoesNotFallBackToExistingOrLatestBatch()
    {
        await using SelectorFixture fixture = await SelectorFixture.CreateAsync();
        SelectionBundle original = CreateSelection();
        await fixture.PrepareAsync(original);
        DatabaseCounts before = await fixture.ReadCountsAsync();
        SelectionBundle request = CreateSelection(batchId: OtherBatchId);

        OctoberSelectionResult result = await fixture.Selector.BindAsync(ToStartupSelection(request));

        AssertRejected(result, OctoberSelectionStatus.BatchMismatch);
        Assert.Equal(before, await fixture.ReadCountsAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_ExistingNotSelectableBatch_ReturnsNoCapabilityWithoutMutation()
    {
        await using SelectorFixture fixture = await SelectorFixture.CreateAsync();
        SelectionBundle selection = CreateSelection();
        await fixture.PrepareAsync(selection);
        await fixture.MarkNotSelectableAsync(BatchId);
        DatabaseCounts before = await fixture.ReadCountsAsync();

        OctoberSelectionResult result = await fixture.Selector.BindAsync(ToStartupSelection(selection));

        AssertRejected(result, OctoberSelectionStatus.Denied);
        Assert.Equal(before, await fixture.ReadCountsAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("canonical-database")]
    [InlineData("membership")]
    [InlineData("root-binding")]
    [InlineData("context-origin")]
    public async Task BindAsync_ChangedTargetOrStableSelection_ReturnsDeniedWithoutMutation(string changedPart)
    {
        await using SelectorFixture fixture = await SelectorFixture.CreateAsync();
        SelectionBundle original = CreateSelection();
        await fixture.PrepareAsync(original);
        DatabaseCounts before = await fixture.ReadCountsAsync();
        SelectionBundle substituted = ChangeStableSelection(original, changedPart);

        OctoberSelectionResult result = await fixture.Selector.BindAsync(ToStartupSelection(substituted));

        AssertRejected(result, OctoberSelectionStatus.Denied);
        Assert.Equal(before, await fixture.ReadCountsAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_CollidingFreshGrantFromAuthorityB_DeniesStoredAuthorityA()
    {
        await using SelectorFixture fixture = await SelectorFixture.CreateAsync();
        SelectionBundle storedA = CreateSelection();
        await fixture.PrepareAsync(storedA);
        DatabaseCounts before = await fixture.ReadCountsAsync();
        SelectionBundle freshB = CreateSelection(managementAuthorityId: ManagementAuthorityB);

        OctoberSelectionResult result = await fixture.Selector.BindAsync(ToStartupSelection(freshB));

        AssertRejected(result, OctoberSelectionStatus.Denied);
        Assert.Equal(before, await fixture.ReadCountsAsync());
        OctoberSelectionResult sameAuthority = await fixture.Selector.BindAsync(ToStartupSelection(storedA));
        Assert.Equal(OctoberSelectionStatus.Selected, sameAuthority.Status);
        AssertSnapshotEquals(storedA, SelectedAtUtc,
            Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(sameAuthority.BoundSetupData).Snapshot);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_SameAuthorityAfterAdvisoryEdits_ReturnsOriginalStoredHistory()
    {
        await using SelectorFixture fixture = await SelectorFixture.CreateAsync();
        SelectionBundle stored = CreateSelection();
        await fixture.PrepareAsync(stored);
        SelectionBundle fresh = ChangeAdvisoryValues(stored);
        DatabaseCounts before = await fixture.ReadCountsAsync();

        OctoberSelectionResult result = await fixture.Selector.BindAsync(ToStartupSelection(fresh));

        Assert.Equal(OctoberSelectionStatus.Selected, result.Status);
        IOctoberHistoricalBoundSetupData selected =
            Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(result.BoundSetupData);
        AssertSnapshotEquals(stored, SelectedAtUtc, selected.Snapshot);
        Assert.NotEqual(fresh.Authority.SelectionDigest, selected.Snapshot.Request.SelectionDigest);
        Assert.Equal(before, await fixture.ReadCountsAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_MissingTrustedIdentity_ReturnsDeniedWithoutCapability()
    {
        await using SelectorFixture fixture = await SelectorFixture.CreateAsync();
        SelectionBundle stored = CreateSelection();
        await fixture.PrepareAsync(stored);
        DatabaseCounts before = await fixture.ReadCountsAsync();
        OctoberAuthorizedSelection missingIdentity = stored.Authority with
        {
            SelectedContext = null,
        };

        OctoberSelectionResult result = await fixture.Selector.BindAsync(
            new OctoberStartupSelection(TargetId, BatchId, missingIdentity));

        AssertRejected(result, OctoberSelectionStatus.Denied);
        Assert.Equal(before, await fixture.ReadCountsAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_UnavailableOctoberStore_ReturnsAuthorityUnavailable()
    {
        await using SelectorFixture fixture = await SelectorFixture.CreateAsync();
        SelectionBundle stored = CreateSelection();
        await fixture.PrepareAsync(stored);
        fixture.MakeUnavailable();

        OctoberSelectionResult result = await fixture.Selector.BindAsync(ToStartupSelection(stored));

        AssertRejected(result, OctoberSelectionStatus.AuthorityUnavailable);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_CancelledRequest_ReturnsCancelledWithoutCapability()
    {
        await using SelectorFixture fixture = await SelectorFixture.CreateAsync();
        SelectionBundle stored = CreateSelection();
        await fixture.PrepareAsync(stored);
        DatabaseCounts before = await fixture.ReadCountsAsync();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        OctoberSelectionResult result = await fixture.Selector.BindAsync(
            ToStartupSelection(stored), cancellation.Token);

        AssertRejected(result, OctoberSelectionStatus.Cancelled);
        Assert.Equal(before, await fixture.ReadCountsAsync());
    }

    private static void AssertRejected(OctoberSelectionResult result, OctoberSelectionStatus expected)
    {
        Assert.Equal(expected, result.Status);
        Assert.Null(result.BoundSetupData);
    }

    private static OctoberStartupSelection ToStartupSelection(SelectionBundle selection) =>
        new(selection.Request.TargetId, selection.Request.BatchId, selection.Authority);

    private static SelectionBundle ChangeStableSelection(SelectionBundle original, string changedPart)
    {
        OctoberSetupRequest request = original.Request;
        OctoberAuthorizedSelection authority = original.Authority;
        OctoberSelectedAdvisoryContext context = authority.SelectedContext!;
        switch (changedPart)
        {
            case "canonical-database":
                authority = authority with { CanonicalDatabaseIdentity = OtherCanonicalDatabaseIdentity };
                break;
            case "membership":
                Guid foreignRepository = Guid.Parse("662389b2-438a-4788-b8d8-319bc8e9d095");
                OctoberRepositorySelection changedRepository = request.Repositories[0] with
                {
                    RepositoryId = foreignRepository,
                };
                request = request with { Repositories = [changedRepository] };
                OctoberAdvisoryContextEntry[] membershipEntries = context.Entries
                    .Select(entry => entry with
                    {
                        RepositoryId = foreignRepository,
                        OriginRecordId = entry.Scope == OctoberContextScope.Repository
                            ? foreignRepository
                            : entry.OriginRecordId,
                    }).ToArray();
                OctoberContextDifference[] membershipDifferences = context.Differences
                    .Select(difference => difference with { RepositoryId = foreignRepository }).ToArray();
                OctoberContextSelection[] membershipReferences = request.ContextSelections
                    .Select(reference => reference.Scope == OctoberContextScope.Repository
                        ? reference with { RecordId = foreignRepository }
                        : reference).ToArray();
                request = request with { ContextSelections = membershipReferences };
                context = context with
                {
                    Repositories = [changedRepository],
                    Entries = membershipEntries.ToImmutableArray(),
                    Differences = membershipDifferences.ToImmutableArray(),
                };
                authority = authority with
                {
                    RepositoryIds = [foreignRepository],
                    SelectedContext = context,
                };
                break;
            case "root-binding":
                OctoberRepositorySelection changedRoot = request.Repositories[0] with
                {
                    RootBindingIdentity = "prepared-root-version-b",
                };
                request = request with { Repositories = [changedRoot] };
                context = context with { Repositories = [changedRoot] };
                authority = authority with { SelectedContext = context };
                break;
            case "context-origin":
                Guid replacementGlobalId = Guid.Parse("99442ca1-d1ab-443a-a02d-0a377fe31cee");
                OctoberContextSelection[] references = request.ContextSelections.ToArray();
                references[0] = references[0] with { RecordId = replacementGlobalId };
                request = request with { ContextSelections = references };
                OctoberAdvisoryContextEntry[] entries = context.Entries.ToArray();
                entries[0] = entries[0] with { OriginRecordId = replacementGlobalId };
                context = context with { Entries = entries.ToImmutableArray() };
                authority = authority with { SelectedContext = context };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(changedPart), changedPart, null);
        }

        return WithFreshDigest(request, authority with { SelectedContext = context });
    }

    private static SelectionBundle ChangeAdvisoryValues(SelectionBundle original)
    {
        OctoberSelectedAdvisoryContext context = original.Authority.SelectedContext!;
        OctoberAdvisoryContextEntry[] entries = context.Entries.ToArray();
        entries[0] = entries[0] with { Value = "fresh global text" };
        entries[1] = entries[1] with { Value = "fresh system text" };
        entries[2] = entries[2] with { Value = "fresh repository text" };
        OctoberSelectedAdvisoryContext freshContext = context with
        {
            Entries = entries.ToImmutableArray(),
        };
        return WithFreshDigest(original.Request, original.Authority with
        {
            SelectedContext = freshContext,
        });
    }

    private static SelectionBundle WithFreshDigest(
        OctoberSetupRequest request,
        OctoberAuthorizedSelection authority)
    {
        string digest = OctoberSelectionDigest.Compute(request, authority.SelectedContext!);
        OctoberSetupRequest withDigest = request with { SelectionDigest = digest };
        return new SelectionBundle(
            withDigest,
            authority with { SelectionDigest = digest });
    }

    private static SelectionBundle CreateSelection(
        Guid? batchId = null,
        string managementAuthorityId = ManagementAuthorityA)
    {
        OctoberRepositorySelection repository = new(
            RepositoryId, SystemId, RootBinding, OperatorLabel);
        OctoberSetupRequest request = new(
            TargetId,
            batchId ?? BatchId,
            [repository],
            [
                new OctoberContextSelection(OctoberContextScope.Global, GlobalContextId),
                new OctoberContextSelection(OctoberContextScope.System, SystemContextId),
                new OctoberContextSelection(OctoberContextScope.Repository, RepositoryId),
            ],
            string.Empty);
        OctoberSelectedAdvisoryContext context = new(
            managementAuthorityId,
            ImmutableArray.Create(repository),
            ImmutableArray.Create(
                Entry(OctoberContextScope.Global, GlobalContextId, GlobalValue),
                Entry(OctoberContextScope.System, SystemContextId, SystemValue),
                Entry(OctoberContextScope.Repository, RepositoryId, RepositoryValue)),
            ImmutableArray.Create(
                Difference(OctoberContextScope.Global, OctoberContextScope.System),
                Difference(OctoberContextScope.Global, OctoberContextScope.Repository),
                Difference(OctoberContextScope.System, OctoberContextScope.Repository)));
        return WithFreshDigest(request, new OctoberAuthorizedSelection(
            TargetId,
            request.BatchId,
            SubjectId,
            CanonicalDatabaseIdentity,
            [RepositoryId],
            string.Empty,
            context));
    }

    private static OctoberAdvisoryContextEntry Entry(
        OctoberContextScope scope,
        Guid origin,
        string value) => new(
            RepositoryId,
            scope,
            OctoberContextApplicability.Selected,
            origin,
            value,
            OctoberContextEvidenceKind.AdvisoryNotSourceProof);

    private static OctoberContextDifference Difference(
        OctoberContextScope left,
        OctoberContextScope right) => new(RepositoryId, left, right);

    private static void AssertSnapshotEquals(
        SelectionBundle expected,
        DateTimeOffset expectedSelectedAtUtc,
        OctoberBoundSetupSnapshot actual)
    {
        Assert.Equal(expected.Request.TargetId, actual.Request.TargetId);
        Assert.Equal(expected.Request.BatchId, actual.Request.BatchId);
        Assert.Equal(expected.Request.SelectionDigest, actual.Request.SelectionDigest);
        Assert.Equal(expected.Request.Repositories.ToArray(), actual.Request.Repositories.ToArray());
        Assert.Equal(expected.Request.ContextSelections.ToArray(), actual.Request.ContextSelections.ToArray());
        Assert.Equal(expected.Authority.TargetId, actual.Authority.TargetId);
        Assert.Equal(expected.Authority.BatchId, actual.Authority.BatchId);
        Assert.Equal(expected.Authority.SubjectId, actual.Authority.SubjectId);
        Assert.Equal(expected.Authority.CanonicalDatabaseIdentity, actual.Authority.CanonicalDatabaseIdentity);
        Assert.Equal(expected.Authority.RepositoryIds.ToArray(), actual.Authority.RepositoryIds.ToArray());
        Assert.Equal(expected.Authority.SelectionDigest, actual.Authority.SelectionDigest);
        Assert.NotNull(actual.Authority.SelectedContext);
        OctoberSelectedAdvisoryContext expectedContext = expected.Authority.SelectedContext!;
        OctoberSelectedAdvisoryContext actualContext = actual.Authority.SelectedContext!;
        Assert.Equal(expectedContext.ManagementAuthorityId, actualContext.ManagementAuthorityId);
        Assert.Equal(expectedContext.Repositories.ToArray(), actualContext.Repositories.ToArray());
        Assert.Equal(expectedContext.Entries.ToArray(), actualContext.Entries.ToArray());
        Assert.Equal(expectedContext.Differences.ToArray(), actualContext.Differences.ToArray());
        Assert.Equal(expectedSelectedAtUtc, actual.SelectedAtUtc);
    }

    private sealed record SelectionBundle(
        OctoberSetupRequest Request,
        OctoberAuthorizedSelection Authority);

    private readonly record struct DatabaseCounts(
        int Batches,
        int Repositories,
        int References,
        int AdvisoryEntries,
        int Differences);

    private sealed class SelectorFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly OctoberSetupStore _writer;
        private readonly OctoberDbContext _database;
        private readonly FixedTimeProvider _clock = new(SelectedAtUtc);

        private SelectorFixture(
            SqliteConnection connection,
            OctoberDbContext database)
        {
            _connection = connection;
            _database = database;
            _writer = new OctoberSetupStore(database, _clock);
            Selector = new OctoberBatchSelector(
                database, TargetId, CanonicalDatabaseIdentity);
        }

        internal IOctoberBatchSelector Selector { get; }

        internal static async Task<SelectorFixture> CreateAsync()
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            DbContextOptions<OctoberDbContext> options = new DbContextOptionsBuilder<OctoberDbContext>()
                .UseSqlite(connection)
                .Options;
            OctoberDbContext database = new(options);
            await database.Database.EnsureCreatedAsync();
            return new SelectorFixture(connection, database);
        }

        internal async Task PrepareAsync(SelectionBundle selection)
        {
            OctoberPrepareResult result = await _writer.CommitAsync(selection.Request, selection.Authority);
            Assert.Equal(OctoberPrepareStatus.Created, result.Status);
            Assert.Equal(SelectedAtUtc, result.CommittedBatch!.SelectedAtUtc);
        }

        internal async Task MarkNotSelectableAsync(Guid batchId)
        {
            OctoberBatchRecord batch = await _database.Batches.SingleAsync(value => value.BatchId == batchId);
            batch.State = OctoberBatchState.NotSelectable;
            await _database.SaveChangesAsync();
        }

        internal async Task<DatabaseCounts> ReadCountsAsync() => new(
            await _database.Batches.CountAsync(),
            await _database.RepositorySelections.CountAsync(),
            await _database.ContextReferences.CountAsync(),
            await _database.AdvisoryEntries.CountAsync(),
            await _database.ContextDifferences.CountAsync());

        internal void MakeUnavailable() => _connection.Dispose();

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
