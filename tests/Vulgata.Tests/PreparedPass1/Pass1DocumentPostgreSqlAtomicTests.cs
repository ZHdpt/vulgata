using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.PreparedScanning;
using Vulgata.Tests.PreparedScanning.TestDoubles;
using Xunit.Sdk;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T030 PostgreSQL RED slice. The fixture enters through the
/// accepted Story 12.2 pair and Story 12.1 job/lease seams, then calls the
/// concrete Story 12.3 committer. PostgreSQL triggers are the fault injector;
/// no test-local transaction or committer is used.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class Pass1DocumentPostgreSqlAtomicTests
{
    [Fact]
    public void PostgreSqlReservationQualification_RequiresTheDurableReservationPort()
    {
        Type? reservationStore = typeof(Pass1NavigationBudget).Assembly.GetType(
            "Vulgata.Core.DomainServices.IPass1NavigationAttemptReservationStore");
        Assert.NotNull(reservationStore);

        Assert.Contains(
            reservationStore!.GetMethods(),
            method => method.Name == "ReserveNextAsync");
    }

    [Fact]
    public async Task PostgreSql17_ReservationFoundationPersistsAcrossContextRecreationWithoutSchemaDeltaAsync()
    {
        Type? reservationStore = typeof(Pass1NavigationBudget).Assembly.GetType(
            "Vulgata.Core.DomainServices.IPass1NavigationAttemptReservationStore");
        Assert.NotNull(reservationStore);
        Assert.Contains(reservationStore!.GetMethods(), method => method.Name == "ReserveNextAsync");

        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        Console.WriteLine($"T105 disposable database: {fixture.Prepared.DatabaseName}");
        await ResetReservationQualificationFixtureAsync(fixture);
        Guid jobId = fixture.Request.Packet.JobId;
        Pass1NavigationBudget before = await fixture.Prepared.Context.Pass1NavigationBudgets
            .AsNoTracking()
            .SingleAsync(value => value.ScanJobExecutionId == jobId);
        Assert.Equal(0, before.ToolAttemptCount);
        Assert.Equal(0, before.StateVersion);
        Assert.DoesNotContain(
            fixture.Prepared.Context.Model.GetEntityTypes(),
            entity => entity.ClrType.Name.Contains("NavigationAttempt", StringComparison.Ordinal) ||
                entity.ClrType.Name.Contains("Reservation", StringComparison.Ordinal));

        await using VulgataDbContext recreated =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        Pass1NavigationBudget after = await recreated.Pass1NavigationBudgets
            .AsNoTracking()
            .SingleAsync(value => value.ScanJobExecutionId == jobId);
        Assert.Equal(before.ToolAttemptCount, after.ToolAttemptCount);
        Assert.Equal(before.StateVersion, after.StateVersion);
        Assert.Empty(await recreated.Pass1NavigationReceipts
            .AsNoTracking()
            .Where(value => value.ScanJobExecutionId == jobId)
            .ToArrayAsync());
    }

    [Fact]
    public async Task PostgreSql17_ReservationReceiptAndMarkerRepairAreDurableAndIdempotentAsync()
    {
        Type? reservationPort = typeof(Pass1NavigationBudget).Assembly.GetType(
            "Vulgata.Core.DomainServices.IPass1NavigationAttemptReservationStore");
        Assert.NotNull(reservationPort);
        Assert.Contains(reservationPort!.GetMethods(), method => method.Name == "ReserveNextAsync");

        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        Console.WriteLine($"T105 disposable database: {fixture.Prepared.DatabaseName}");
        await ResetReservationQualificationFixtureAsync(fixture);
        (PreparedPass1NavigationScope scope, PreparedPass1NavigationRequestInput request) =
            await CreateNavigationBindingAsync(fixture);
        PreparedPass1Store store = CreateReservationStore(fixture);
        MethodInfo reserve = Assert.Single(
            typeof(PreparedPass1Store).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == "ReserveNavigationAttemptAsync"));

        object latest = await InvokeStoreAsync(store, reserve, scope, request, cursor: null, draft: null);
        Assert.Equal(1, ReadInt(latest, "ToolAttemptOrdinal", "ReservedOrdinal", "Ordinal"));
        Assert.Equal(1, ReadInt(latest, "ToolAttemptCount", "AttemptHighWater", "HighWater"));
        Assert.Equal(1L, ReadLong(latest, "StateVersion", "BudgetStateVersion", "CursorStateVersion"));

        await using VulgataDbContext afterReservation =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        Pass1NavigationBudget reservedBudget = await afterReservation.Pass1NavigationBudgets
            .AsNoTracking()
            .SingleAsync(value => value.ScanJobExecutionId == request.JobId);
        Assert.Equal(1, reservedBudget.ToolAttemptCount);
        Assert.Equal(1, reservedBudget.StateVersion);
        Assert.Empty(await afterReservation.Pass1NavigationReceipts
            .AsNoTracking()
            .Where(value => value.ScanJobExecutionId == request.JobId)
            .ToArrayAsync());

        PreparedPass1NavigationReceiptDraft draft = new(
            SHA256.HashData(Encoding.UTF8.GetBytes("t105-ordinary-receipt")),
            PreparedPass1NavigationOperation.Definition,
            1,
            PreparedPass1NavigationOutcome.Unavailable,
            null,
            [],
            0,
            0,
            ReadOnlyMemory<byte>.Empty);
        MethodInfo commit = Assert.Single(
            typeof(PreparedPass1Store).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == "CommitReservedNavigationReceiptAsync"));
        object replacement = await InvokeStoreAsync(store, commit, scope, request, latest, draft);
        Assert.Equal(1, ReadInt(replacement, "ToolAttemptCount", "AttemptHighWater", "HighWater"));
        Assert.Equal(2L, ReadLong(replacement, "StateVersion", "BudgetStateVersion", "CursorStateVersion"));
        latest = replacement;

        request = request with { ToolAttemptOrdinal = 2 };
        for (int ordinal = 2; ordinal <= 8; ordinal++)
        {
            PreparedPass1NavigationScope currentScope = scope with
            {
                ToolAttemptCount = ordinal - 1,
                AcceptedCallCount = 0,
                EvidenceBytes = 0,
            };
            request = request with { ToolAttemptOrdinal = (ulong)ordinal };
            latest = await InvokeStoreAsync(store, reserve, currentScope, request, latest, draft: null);
            Assert.Equal(ordinal, ReadInt(latest, "ToolAttemptOrdinal", "ReservedOrdinal", "Ordinal"));
            Assert.Equal(ordinal, ReadInt(latest, "ToolAttemptCount", "AttemptHighWater", "HighWater"));
        }

        MethodInfo marker = Assert.Single(
            typeof(PreparedPass1Store).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == "EnsureNavigationCallsMarkerAsync"));
        object markerCursor = await InvokeStoreAsync(store, marker, scope, request, latest, draft: null);
        Assert.Equal(8, ReadInt(markerCursor, "ToolAttemptCount", "AttemptHighWater", "HighWater"));
        await InvokeStoreAsync(store, marker, scope, request, markerCursor, draft: null);

        await using VulgataDbContext afterMarker =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        Pass1NavigationBudget finalBudget = await afterMarker.Pass1NavigationBudgets
            .AsNoTracking()
            .SingleAsync(value => value.ScanJobExecutionId == request.JobId);
        Assert.Equal(8, finalBudget.ToolAttemptCount);
        Assert.Equal(1, await afterMarker.Pass1NavigationReceipts.CountAsync(value =>
            value.ScanJobExecutionId == request.JobId && value.ReceiptOrdinal == 9 &&
            value.ToolAttemptOrdinal == null && value.BoundKind == Pass1NavigationBoundKind.Calls));

        int receiptsBeforeConflict = await afterMarker.Pass1NavigationReceipts.CountAsync(
            value => value.ScanJobExecutionId == request.JobId);
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await InvokeStoreAsync(store, commit, scope, request with { ToolAttemptOrdinal = 9 }, latest, draft));
        await using VulgataDbContext afterConflict =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        Assert.Equal(receiptsBeforeConflict, await afterConflict.Pass1NavigationReceipts.CountAsync(
            value => value.ScanJobExecutionId == request.JobId));
    }

    [Fact]
    public async Task PostgreSql17_RetryingExecutionStrategy_ReservesAndCommitsNavigationReceiptWithoutTransactionRejectionAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        await ResetReservationQualificationFixtureAsync(fixture);
        (PreparedPass1NavigationScope scope, PreparedPass1NavigationRequestInput request) =
            await CreateNavigationBindingAsync(fixture);
        DbContextOptions<VulgataDbContext> options =
            new DbContextOptionsBuilder<VulgataDbContext>()
                .UseNpgsql(fixture.ConnectionString, npgsql => npgsql.EnableRetryOnFailure(3))
                .Options;
        await using VulgataDbContext retryingContext = new(options);
        PreparedPass1Store store = new(
            retryingContext,
            new FixedTimeProvider(PreparedScanTestHost.FixedUtcNow.AddMinutes(1)));

        PreparedPass1NavigationReservationTicket? reserved =
            await store.ReserveNavigationAttemptAsync(
                scope,
                request,
                cancellationToken: CancellationToken.None);
        Assert.NotNull(reserved);

        PreparedPass1NavigationReservationTicket? committed =
            await store.CommitReservedNavigationReceiptAsync(
                scope,
                request,
                reserved!,
                new(
                    SHA256.HashData(Encoding.UTF8.GetBytes("run-03-retry-strategy-receipt")),
                    PreparedPass1NavigationOperation.Definition,
                    1,
                    PreparedPass1NavigationOutcome.Unavailable,
                    null,
                    [],
                    0,
                    0,
                    ReadOnlyMemory<byte>.Empty),
                CancellationToken.None);

        Assert.NotNull(committed);
        Assert.NotNull(committed!.ReceiptId);
        Assert.Equal(1, await retryingContext.Pass1NavigationReceipts.CountAsync(value =>
            value.ScanJobExecutionId == request.JobId));
    }

    [Fact]
    public async Task PostgreSql17_RetryingExecutionStrategy_CommitsPass1DocumentWithoutTransactionRejectionAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        DbContextOptions<VulgataDbContext> options =
            new DbContextOptionsBuilder<VulgataDbContext>()
                .UseNpgsql(fixture.ConnectionString, npgsql => npgsql.EnableRetryOnFailure(3))
                .Options;
        await using VulgataDbContext retryingContext = new(options);
        IPass1CodeLogicDocumentCommitter committer =
            new Pass1CodeLogicDocumentCommitter(retryingContext);

        Pass1DocumentCommitResult committed = await committer.CommitAsync(
            fixture.Request,
            CancellationToken.None);

        Assert.Equal(Pass1DocumentCommitStatus.Committed, committed.Status);
        Assert.Equal(fixture.Request.DocumentVersionId, committed.DocumentVersionId);
        Assert.Equal(1, await retryingContext.DocumentVersions.CountAsync(value =>
            value.Id == fixture.Request.DocumentVersionId));
        Assert.Equal(ScanJobExecutionState.Completed, await retryingContext.ScanJobExecutions
            .Where(value => value.Id == fixture.Request.Packet.JobId)
            .Select(value => value.State)
            .SingleAsync());
    }

    private static async Task<(PreparedPass1NavigationScope Scope, PreparedPass1NavigationRequestInput Request)>
        CreateNavigationBindingAsync(CommitFixture fixture)
    {
        ScanJobExecution job = await fixture.Prepared.Context.ScanJobExecutions
            .AsNoTracking()
            .SingleAsync(value => value.Id == fixture.Request.Packet.JobId);
        Pass1PairExecution pair = await fixture.Prepared.Context.Pass1PairExecutions
            .AsNoTracking()
            .SingleAsync(value => value.Id == fixture.Request.Packet.PairExecutionId);
        PreparedPass1NavigationRequestInput request = new(
            job.Id,
            1,
            PreparedPass1NavigationOperation.Definition,
            job.FileCodeUnitId,
            new(null, null, null, null, null),
            1,
            null,
            pair.Id,
            job.StateVersion,
            pair.StateVersion,
            job.LeaseToken!.Value,
            job.LeaseExpiresAt!.Value,
            pair.Phase.ToString(),
            pair.CircuitState,
            job.State,
            PreparedPass1AuthorizationBinding.Create(fixture.Prepared.Authorization));
        PreparedPass1NavigationScope? scope = await CreateReservationStore(fixture)
            .GetAsync(request, CancellationToken.None);
        Assert.NotNull(scope);
        return (scope!, request);
    }

    private static async Task<object> InvokeStoreAsync(
        object target,
        MethodInfo method,
        PreparedPass1NavigationScope scope,
        PreparedPass1NavigationRequestInput request,
        object? cursor,
        PreparedPass1NavigationReceiptDraft? draft)
    {
        ParameterInfo[] parameters = method.GetParameters();
        object?[] arguments = new object?[parameters.Length];
        foreach (ParameterInfo parameter in parameters)
        {
            Type rawType = parameter.ParameterType;
            Type type = Nullable.GetUnderlyingType(rawType) ?? rawType;
            string name = parameter.Name ?? string.Empty;
            arguments[parameter.Position] =
                rawType == typeof(PreparedPass1NavigationScope) ? scope :
                rawType == typeof(PreparedPass1NavigationRequestInput) ? request :
                rawType == typeof(PreparedPass1NavigationReservationRequest)
                    ? new PreparedPass1NavigationReservationRequest(
                        scope,
                        request,
                        cursor as PreparedPass1NavigationReservationTicket) :
                rawType == typeof(PreparedPass1NavigationReceiptDraft) ? draft :
                cursor is not null && rawType.IsInstanceOfType(cursor) ? cursor :
                rawType == typeof(CancellationToken) ? CancellationToken.None :
                type == typeof(Guid) ? GuidArgument(name, request) :
                type == typeof(long) ? LongArgument(name, request, cursor) :
                type == typeof(int) ? IntArgument(name, request, cursor) :
                type == typeof(ulong) ? ULongArgument(name, request, cursor) :
                type == typeof(DateTimeOffset) ? DateTimeOffset.UtcNow :
                type == typeof(string) ? StringArgument(name, request) :
                type == typeof(PreparedPass1ServerAuthorization) ? new PreparedPass1ServerAuthorization(
                    request.PairExecutionId,
                    request.JobStateVersion,
                    request.PairStateVersion,
                    request.LeaseToken,
                    request.LeaseExpiresAt,
                    request.PairState,
                    request.CircuitState,
                    request.JobState,
                    request.AuthorizationBinding) :
                type == typeof(ReadOnlyMemory<byte>) ? ReadOnlyMemory<byte>.Empty :
                type == typeof(IReadOnlyList<PreparedPass1EvidenceDescriptor>)
                    ? Array.Empty<PreparedPass1EvidenceDescriptor>() :
                type.IsEnum ? EnumArgument(type, name, request) :
                parameter.HasDefaultValue ? parameter.DefaultValue :
                throw new XunitException(
                    $"No T105 fixture binding for {method.Name} parameter {parameter.ParameterType} {parameter.Name}.");
        }

        object? invocation;
        try
        {
            invocation = method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }

        if (invocation is Task task)
        {
            await task.ConfigureAwait(false);
            PropertyInfo? result = task.GetType().GetProperty("Result");
            return result?.GetValue(task) ?? new object();
        }

        return invocation ?? throw new XunitException($"{method.Name} returned null.");
    }

    private static PreparedPass1Store CreateReservationStore(CommitFixture fixture) =>
        new(
            fixture.Prepared.Context,
            new FixedTimeProvider(PreparedScanTestHost.FixedUtcNow.AddMinutes(1)));

    private static async Task ResetReservationQualificationFixtureAsync(CommitFixture fixture)
    {
        await fixture.Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "vulgata"."Pass1NavigationReceipts"
            WHERE "ScanJobExecutionId" = {fixture.Request.Packet.JobId};
            """);
        fixture.Prepared.Context.ChangeTracker.Clear();
        Assert.Equal(0, await fixture.Prepared.Context.Pass1NavigationReceipts
            .AsNoTracking()
            .CountAsync(value => value.ScanJobExecutionId == fixture.Request.Packet.JobId));
    }

    private static Guid GuidArgument(string name, PreparedPass1NavigationRequestInput request) =>
        name.Contains("pair", StringComparison.OrdinalIgnoreCase) ? request.PairExecutionId :
        name.Contains("lease", StringComparison.OrdinalIgnoreCase) ? request.LeaseToken :
        request.JobId;

    private static long LongArgument(string name, PreparedPass1NavigationRequestInput request, object? cursor) =>
        name.Contains("attempt", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("high", StringComparison.OrdinalIgnoreCase)
            ? cursor is null ? 0 : ReadLong(cursor, "ToolAttemptCount", "AttemptHighWater", "HighWater")
            : request.JobStateVersion;

    private static int IntArgument(string name, PreparedPass1NavigationRequestInput request, object? cursor) =>
        name.Contains("ordinal", StringComparison.OrdinalIgnoreCase)
            ? checked((int)request.ToolAttemptOrdinal)
            : checked((int)LongArgument(name, request, cursor));

    private static ulong ULongArgument(string name, PreparedPass1NavigationRequestInput request, object? cursor) =>
        name.Contains("ordinal", StringComparison.OrdinalIgnoreCase)
            ? request.ToolAttemptOrdinal
            : checked((ulong)LongArgument(name, request, cursor));

    private static string StringArgument(string name, PreparedPass1NavigationRequestInput request) =>
        name.Contains("binding", StringComparison.OrdinalIgnoreCase) ? request.AuthorizationBinding :
        name.Contains("pair", StringComparison.OrdinalIgnoreCase) ? request.PairState :
        string.Empty;

    private static object EnumArgument(
        Type type,
        string name,
        PreparedPass1NavigationRequestInput request) =>
        name.Contains("circuit", StringComparison.OrdinalIgnoreCase) && type == typeof(Pass1CircuitState)
            ? request.CircuitState
            : name.Contains("job", StringComparison.OrdinalIgnoreCase) && type == typeof(ScanJobExecutionState)
                ? request.JobState
                : Enum.GetValues(type).GetValue(0)!;

    private static int ReadInt(object value, params string[] names) =>
        Convert.ToInt32(ReadMember(value, names));

    private static long ReadLong(object value, params string[] names) =>
        Convert.ToInt64(ReadMember(value, names));

    private static object ReadMember(object value, string[] names)
    {
        foreach (string name in names)
        {
            PropertyInfo? property = value.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property is not null)
            {
                return property.GetValue(value) ?? throw new XunitException($"{name} was null.");
            }
        }

        PropertyInfo? cursor = value.GetType().GetProperty("Cursor", BindingFlags.Public | BindingFlags.Instance);
        if (cursor?.GetValue(value) is { } nested)
        {
            return ReadMember(nested, names);
        }

        throw new XunitException(
            $"{value.GetType().FullName} did not expose any of: {string.Join(", ", names)}.");
    }

    private static readonly Guid TargetDocumentVersionId =
        Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
    private static readonly Guid TargetLogicalDocumentId =
        Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff");
    private static readonly Guid TargetAnchorId =
        Guid.Parse("cccccccc-dddd-4eee-8fff-aaaaaaaaaaaa");
    private static readonly Guid NavigationReceiptId =
        Guid.Parse("dddddddd-eeee-4fff-8000-bbbbbbbbbbbb");
    private static readonly Guid ExtraAnchorId =
        Guid.Parse("eeeeeeee-1111-4222-8333-444444444444");
    private static readonly Guid ExtraSectionId =
        Guid.Parse("eeeeeeee-2222-4333-8444-555555555555");
    private static readonly Guid ExtraEdgeId =
        Guid.Parse("eeeeeeee-3333-4444-8555-666666666666");

    public static TheoryData<string, string, string, string> DocumentWriteBoundaries => new()
    {
        { "before-logical-document", "vulgata.\"LogicalDocuments\"", "INSERT", "BEFORE" },
        { "after-logical-document", "vulgata.\"LogicalDocuments\"", "INSERT", "AFTER" },
        { "before-document-version", "vulgata.\"DocumentVersions\"", "INSERT", "BEFORE" },
        { "after-document-version", "vulgata.\"DocumentVersions\"", "INSERT", "AFTER" },
        { "before-source-anchor", "vulgata.\"SourceAnchors\"", "INSERT", "BEFORE" },
        { "after-source-anchor", "vulgata.\"SourceAnchors\"", "INSERT", "AFTER" },
        { "before-document-section", "vulgata.\"DocumentSections\"", "INSERT", "BEFORE" },
        { "after-document-section", "vulgata.\"DocumentSections\"", "INSERT", "AFTER" },
        { "before-index-entry", "vulgata.\"IndexEntries\"", "INSERT", "BEFORE" },
        { "after-index-entry", "vulgata.\"IndexEntries\"", "INSERT", "AFTER" },
        { "before-reference-edge", "vulgata.\"DocumentEdges\"", "INSERT", "BEFORE" },
        { "after-reference-edge", "vulgata.\"DocumentEdges\"", "INSERT", "AFTER" },
        { "before-job-completion", "vulgata.\"ScanJobExecutions\"", "UPDATE", "BEFORE" },
        { "after-job-completion", "vulgata.\"ScanJobExecutions\"", "UPDATE", "AFTER" },
    };

    public static TheoryData<string> CanonicalGraphCorruptions => new()
    {
        "source-anchor-remove",
        "source-anchor-mutate",
        "source-anchor-reorder",
        "source-anchor-duplicate",
        "source-anchor-cardinality",
        "document-section-remove",
        "document-section-mutate",
        "document-section-reorder",
        "document-section-duplicate",
        "document-section-cardinality",
        "index-entry-remove",
        "index-entry-mutate",
        "index-entry-reorder",
        "index-entry-duplicate",
        "index-entry-cardinality",
        "direct-reference-edge-remove",
        "direct-reference-edge-mutate",
        "direct-reference-edge-reorder",
        "direct-reference-edge-duplicate",
        "direct-reference-edge-cardinality",
    };

    public static TheoryData<string> ReconciledEdgeUnionCorruptions => new()
    {
        "reconciled-edge-remove",
        "reconciled-edge-mutate",
        "reconciled-edge-reorder",
        "reconciled-edge-duplicate",
        "reconciled-edge-cardinality",
    };

    /// <summary>
    /// T060 RED: a completed replay must reconstruct every persisted canonical
    /// graph row.  The current committer only compares DocumentVersion scalar
    /// fields, so each corruption is expected to return Replayed until T061
    /// adds the complete graph comparison.
    /// </summary>
    [Theory]
    [MemberData(nameof(CanonicalGraphCorruptions))]
    public async Task PostgreSql17_CompletedReplay_RejectsCanonicalGraphCorruptionWithoutMutation(
        string corruption)
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        Pass1DocumentCommitResult first = await fixture.Committer.CommitAsync(
            fixture.Request,
            CancellationToken.None);
        Assert.Equal(Pass1DocumentCommitStatus.Committed, first.Status);

        bool duplicateProtected = await fixture.ApplyCanonicalGraphCorruptionAsync(corruption);
        if (duplicateProtected)
        {
            Assert.EndsWith("-duplicate", corruption, StringComparison.Ordinal);
            return;
        }
        GraphSnapshot beforeReplay = await fixture.ReadGraphSnapshotAsync();

        Pass1DocumentCommitResult? replay = null;
        Exception? failure = null;
        try
        {
            replay = await fixture.Committer.CommitAsync(fixture.Request, CancellationToken.None);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        Assert.True(
            failure is not null || replay?.Status != Pass1DocumentCommitStatus.Replayed,
            $"T060 RED: {corruption} returned Replayed after canonical graph corruption.");
        Assert.Equal(beforeReplay, await fixture.ReadGraphSnapshotAsync());
    }

    /// <summary>
    /// T060 RED: reconciliation replay must validate the exact direct-plus-
    /// reconciled REFERENCES Edge union, not just its durable fingerprint and
    /// row count.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReconciledEdgeUnionCorruptions))]
    public async Task PostgreSql17_ReconciliationReplay_RejectsEdgeUnionCorruptionWithoutMutation(
        string corruption)
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        await fixture.PrepareForReconciliationAsync();
        PreparedPass1ReconciliationResult first = await fixture.ReconcileAsync();
        Assert.Equal(PreparedPass1ReconciliationOutcome.Reconciled, first.Outcome);

        bool duplicateProtected = await fixture.ApplyReconciledEdgeCorruptionAsync(corruption);
        if (duplicateProtected)
        {
            Assert.EndsWith("-duplicate", corruption, StringComparison.Ordinal);
            return;
        }
        string beforeReplay = await fixture.ReadReferencesEdgeDigestAsync();

        PreparedPass1ReconciliationResult replay = await fixture.ReconcileAsync(
            expectedReconciliationFingerprint: first.ReconciliationFingerprint);

        Assert.NotEqual(PreparedPass1ReconciliationOutcome.Replay, replay.Outcome);
        Assert.Equal(beforeReplay, await fixture.ReadReferencesEdgeDigestAsync());
    }

    [Theory]
    [MemberData(nameof(DocumentWriteBoundaries))]
    public async Task PostgreSql17_CommitFailureAtEveryWriteBoundary_RollsBackDocumentGraphAndJob(
        string boundary,
        string table,
        string operation,
        string timing)
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        await InstallFailingTriggerAsync(fixture.ConnectionString, boundary, table, operation, timing);
        GraphCounts before = await GraphCounts.ReadAsync(fixture.ConnectionString);

        Exception? failure = null;
        Pass1DocumentCommitResult? result = null;
        try
        {
            result = await fixture.Committer.CommitAsync(fixture.Request, CancellationToken.None);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (failure is null)
        {
            Assert.Fail($"Commit returned {result?.Status} ({result?.ReasonCode}) instead of reaching {boundary}.");
        }
        Assert.Contains("T030 injected document boundary", FlattenMessages(failure!), StringComparison.Ordinal);
        Assert.Equal(before, await GraphCounts.ReadAsync(fixture.ConnectionString));
        Assert.Equal(ScanJobExecutionState.Running, await fixture.ReadJobStateAsync());
    }

    [Fact]
    public async Task PostgreSql17_PolicyDrain_AcceptsLeasedSuccessButTerminalFailureRejectsLateCommit()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using (CommitFixture draining = await CommitFixture.CreateAsync(administrator))
        {
            await SetPairPhaseAsync(
                draining,
                Pass1PairExecutionPhase.Running,
                Pass1CircuitState.Paused,
                Pass1PairReasonCategory.Pass1Policy,
                "pass1-policy-failed",
                terminalAt: null);

            Pass1DocumentCommitResult accepted = await draining.Committer.CommitAsync(
                draining.Request,
                CancellationToken.None);
            Assert.Equal(Pass1DocumentCommitStatus.Committed, accepted.Status);
        }

        await using CommitFixture terminal = await CommitFixture.CreateAsync(administrator);
        await SetPairPhaseAsync(
            terminal,
            Pass1PairExecutionPhase.FailedPass1,
            Pass1CircuitState.Paused,
            Pass1PairReasonCategory.Pass1Policy,
            "pass1-policy-failed",
            PreparedScanTestHost.FixedUtcNow.AddMinutes(2));
        GraphCounts before = await GraphCounts.ReadAsync(terminal.ConnectionString);

        Pass1DocumentCommitResult rejected = await terminal.Committer.CommitAsync(
            terminal.Request,
            CancellationToken.None);

        Assert.Equal(Pass1DocumentCommitStatus.Rejected, rejected.Status);
        Assert.Equal("durable-binding-mismatch", rejected.ReasonCode);
        Assert.Null(rejected.LogicalDocumentId);
        Assert.Null(rejected.DocumentVersionId);
        Assert.Null(rejected.CommitFingerprint);
        Assert.Equal(before, await GraphCounts.ReadAsync(terminal.ConnectionString));
        Assert.Equal(ScanJobExecutionState.Running, await terminal.ReadJobStateAsync());
    }

    [Fact]
    public async Task PostgreSql17_ProviderProbeDrain_AcceptsAlreadyLeasedSuccessBeforeTerminalRejection()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        await SetPairPhaseAsync(
            fixture,
            Pass1PairExecutionPhase.ProbingProvider,
            Pass1CircuitState.Probing,
            Pass1PairReasonCategory.ProviderUnavailable,
            "provider-unavailable",
            terminalAt: null);
        GraphCounts before = await GraphCounts.ReadAsync(fixture.ConnectionString);

        Pass1DocumentCommitResult accepted = await fixture.Committer.CommitAsync(
            fixture.Request,
            CancellationToken.None);

        Assert.True(
            accepted.Status == Pass1DocumentCommitStatus.Committed,
            $"Expected Committed, actual {accepted.Status}/{accepted.ReasonCode}.");
        Assert.Equal(2, await CountAsync(fixture.ConnectionString, "\"DocumentVersions\""));
        Assert.Equal(2, await CountAsync(fixture.ConnectionString, "\"LogicalDocuments\""));
        Assert.NotEqual(before, await GraphCounts.ReadAsync(fixture.ConnectionString));
        Assert.Equal(ScanJobExecutionState.Completed, await fixture.ReadJobStateAsync());
    }

    [Fact]
    public async Task PostgreSql17_ConcurrentExactAndDifferentCommits_HaveOneWinnerAndMutationFreeConflict()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        await using VulgataDbContext secondContext = PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        IPass1CodeLogicDocumentCommitter second = new Pass1CodeLogicDocumentCommitter(secondContext);

        Task<Pass1DocumentCommitResult> first = fixture.Committer.CommitAsync(fixture.Request, CancellationToken.None);
        Task<Pass1DocumentCommitResult> exact = second.CommitAsync(fixture.Request, CancellationToken.None);
        Pass1DocumentCommitResult[] results = await Task.WhenAll(first, exact);

        Assert.Contains(results, value => value.Status == Pass1DocumentCommitStatus.Committed);
        Assert.Contains(results, value => value.Status is Pass1DocumentCommitStatus.Replayed or Pass1DocumentCommitStatus.Conflict);
        // The fixture includes one immutable target version for the REFERENCES edge;
        // the commit must add exactly one source version and no duplicate.
        Assert.Equal(2, await CountAsync(fixture.ConnectionString, "\"DocumentVersions\""));
        // The seeded target and committed source are distinct logical documents.
        Assert.Equal(2, await CountAsync(fixture.ConnectionString, "\"LogicalDocuments\""));
        Assert.Equal(1, await CountAsync(fixture.ConnectionString, "\"ScanJobExecutions\"", "\"State\" = 'Completed'"));
    }

    [Fact]
    public async Task PostgreSql17_StaleLeaseAndCancellation_LeaveRunningJobAndZeroGraphRows()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        GraphCounts before = await GraphCounts.ReadAsync(fixture.ConnectionString);

        Pass1DocumentCommitResult stale = await fixture.Committer.CommitAsync(
            fixture.Request with { ExpectedLeaseToken = Guid.NewGuid() }, CancellationToken.None);
        Assert.Equal(Pass1DocumentCommitStatus.Rejected, stale.Status);
        Assert.Equal(before, await GraphCounts.ReadAsync(fixture.ConnectionString));

        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Committer.CommitAsync(fixture.Request, cancellation.Token));
        Assert.Equal(before, await GraphCounts.ReadAsync(fixture.ConnectionString));
        Assert.Equal(ScanJobExecutionState.Running, await fixture.ReadJobStateAsync());
    }

    [Fact]
    public async Task PostgreSql17_LocalAuthoritativeRejection_LeavesGraphAndJobUnchanged()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        GraphSnapshot graphBefore = await fixture.ReadGraphSnapshotAsync();
        GraphCounts countsBefore = await GraphCounts.ReadAsync(fixture.ConnectionString);

        Pass1DocumentCommitResult rejected = await fixture.Committer.CommitAsync(
            fixture.Request with
            {
                CanonicalCodeLogicJson = Encoding.UTF8.GetBytes("{}"),
            },
            CancellationToken.None);

        Assert.Equal(Pass1DocumentCommitStatus.Rejected, rejected.Status);
        Assert.Equal("invalid-authoritative-result", rejected.ReasonCode);
        Assert.Null(rejected.LogicalDocumentId);
        Assert.Null(rejected.DocumentVersionId);
        Assert.Null(rejected.CommitFingerprint);
        Assert.Equal(graphBefore, await fixture.ReadGraphSnapshotAsync());
        Assert.Equal(countsBefore, await GraphCounts.ReadAsync(fixture.ConnectionString));
        Assert.Equal(ScanJobExecutionState.Running, await fixture.ReadJobStateAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PostgreSql17_ClassificationCitedSubsetUsesCompleteSuppliedAuthorizationSetAsync(
        int citedClassificationCount)
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        (Pass1DocumentCommitRequest request, IReadOnlyList<CommunicationKindClassification> classifications) =
            await fixture.CreateClassificationRequestAsync(citedClassificationCount);

        Pass1DocumentCommitResult result = await fixture.Committer.CommitAsync(
            request,
            CancellationToken.None);

        Assert.True(
            result.Status == Pass1DocumentCommitStatus.Committed,
            $"supplied=2 cited={citedClassificationCount} returned {result.Status}: {result.ReasonCode}");

        if (citedClassificationCount == 2)
        {
            Pass1DocumentCommitResult replay = await fixture.Committer.CommitAsync(
                request,
                CancellationToken.None);
            Assert.Equal(Pass1DocumentCommitStatus.Replayed, replay.Status);
            Assert.Equal(result.DocumentVersionId, replay.DocumentVersionId);
            Assert.Equal(result.CommitFingerprint, replay.CommitFingerprint);
        }

        Assert.Equal(2, classifications.Count);
    }

    [Theory]
    [InlineData("missing-current-pre-scan-result")]
    [InlineData("wrong-current-pre-scan-result")]
    [InlineData("field-mismatched-uncited-supplied")]
    public async Task PostgreSql17_InvalidUncitedSuppliedClassificationBindingIsMutationFreeAsync(
        string mutation)
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        (Pass1DocumentCommitRequest request, IReadOnlyList<CommunicationKindClassification> classifications) =
            await fixture.CreateClassificationRequestAsync(0);
        await fixture.MutateClassificationBindingAsync(mutation, classifications[0].Id);
        GraphSnapshot graphBefore = await fixture.ReadGraphSnapshotAsync();
        GraphCounts countsBefore = await GraphCounts.ReadAsync(fixture.ConnectionString);

        Pass1DocumentCommitResult result = await fixture.Committer.CommitAsync(
            request,
            CancellationToken.None);

        Assert.Equal(Pass1DocumentCommitStatus.Rejected, result.Status);
        Assert.Equal("durable-binding-mismatch", result.ReasonCode);
        Assert.Equal(graphBefore, await fixture.ReadGraphSnapshotAsync());
        Assert.Equal(countsBefore, await GraphCounts.ReadAsync(fixture.ConnectionString));
        Assert.Equal(ScanJobExecutionState.Running, await fixture.ReadJobStateAsync());
    }

    [Fact]
    public async Task PostgreSql17_CitedUnsuppliedClassificationIsInvalidAuthoritativeResultAndMutationFreeAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        (Pass1DocumentCommitRequest request, _) = await fixture.CreateClassificationRequestAsync(1);
        JsonObject root = JsonNode.Parse(
            Encoding.UTF8.GetString(request.CanonicalCodeLogicJson.ToArray()))!.AsObject();
        root["communicationEvidence"]![0]!["classificationEvidence"]![0]!["classificationId"] =
            Guid.NewGuid().ToString("D");
        Pass1DocumentCommitRequest invalid = request with
        {
            CanonicalCodeLogicJson = Encoding.UTF8.GetBytes(
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = false })),
        };
        GraphSnapshot graphBefore = await fixture.ReadGraphSnapshotAsync();
        GraphCounts countsBefore = await GraphCounts.ReadAsync(fixture.ConnectionString);

        Pass1DocumentCommitResult result = await fixture.Committer.CommitAsync(
            invalid,
            CancellationToken.None);

        Assert.Equal(Pass1DocumentCommitStatus.Rejected, result.Status);
        Assert.Equal("invalid-authoritative-result", result.ReasonCode);
        Assert.Equal(graphBefore, await fixture.ReadGraphSnapshotAsync());
        Assert.Equal(countsBefore, await GraphCounts.ReadAsync(fixture.ConnectionString));
        Assert.Equal(ScanJobExecutionState.Running, await fixture.ReadJobStateAsync());
    }

    [Fact]
    public async Task PostgreSql17_ExactReplay_IsMutationFreeAndReconstructsScalarsWithoutArrayDependence()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        Pass1DocumentCommitResult first = await fixture.Committer.CommitAsync(fixture.Request, CancellationToken.None);
        Assert.True(first.Status == Pass1DocumentCommitStatus.Committed,
            $"first commit returned {first.Status}: {first.ReasonCode}");
        GraphCounts afterFirst = await GraphCounts.ReadAsync(fixture.ConnectionString);

        Pass1DocumentCommitResult replay = await fixture.Committer.CommitAsync(fixture.Request, CancellationToken.None);

        Assert.True(first.Status == Pass1DocumentCommitStatus.Committed,
            $"first commit returned {first.Status}: {first.ReasonCode}");
        Assert.Equal(Pass1DocumentCommitStatus.Replayed, replay.Status);
        Assert.Equal(first.DocumentVersionId, replay.DocumentVersionId);
        Assert.Equal(first.CommitFingerprint, replay.CommitFingerprint);
        Assert.Equal(afterFirst, await GraphCounts.ReadAsync(fixture.ConnectionString));
        // One seeded target version plus exactly one committed source version.
        Assert.Equal(2, await CountAsync(fixture.ConnectionString, "\"DocumentVersions\""));
        Assert.Equal(1, await CountAsync(fixture.ConnectionString, "\"ScanJobExecutions\"", "\"State\" = 'Completed'"));
    }

    [Fact]
    public async Task PostgreSql17_ExactReplay_WithSubMicrosecondCommittedAt_IsReplayed()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        Pass1DocumentCommitRequest request = fixture.Request with
        {
            CommittedAt = fixture.Request.CommittedAt.AddTicks(1),
        };

        Pass1DocumentCommitResult first = await fixture.Committer.CommitAsync(
            request,
            CancellationToken.None);
        Pass1DocumentCommitResult replay = await fixture.Committer.CommitAsync(
            request,
            CancellationToken.None);

        Assert.Equal(Pass1DocumentCommitStatus.Committed, first.Status);
        Assert.Equal(Pass1DocumentCommitStatus.Replayed, replay.Status);
        Assert.Equal(first.DocumentVersionId, replay.DocumentVersionId);
        Assert.Equal(first.CommitFingerprint, replay.CommitFingerprint);
    }

    [Fact]
    public async Task PostgreSql17_DifferentReplay_PreservesImmutableVersionChainAndPreviousSupersededPointer()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        Pass1DocumentCommitResult first = await fixture.Committer.CommitAsync(fixture.Request, CancellationToken.None);

        Pass1DocumentCommitResult changed = await fixture.Committer.CommitAsync(
            fixture.Request with { CanonicalCodeLogicJson = fixture.ChangedCanonicalJson },
            CancellationToken.None);

        Assert.True(first.Status == Pass1DocumentCommitStatus.Committed,
            $"first commit returned {first.Status}: {first.ReasonCode}");
        Assert.Equal(Pass1DocumentCommitStatus.Conflict, changed.Status);
        // One seeded target version plus exactly one committed source version.
        Assert.Equal(2, await CountAsync(fixture.ConnectionString, "\"DocumentVersions\""));
        Assert.Equal(0, await CountAsync(fixture.ConnectionString, "\"DocumentVersions\"", "\"SupersededById\" IS NOT NULL"));
    }

    public static TheoryData<string, string, string, string> ReconciliationWriteBoundaries => new()
    {
        { "before-edge", "vulgata.\"DocumentEdges\"", "INSERT", "BEFORE" },
        { "after-edge", "vulgata.\"DocumentEdges\"", "INSERT", "AFTER" },
        { "before-outcome", "vulgata.\"Pass1Outcomes\"", "INSERT", "BEFORE" },
        { "after-outcome", "vulgata.\"Pass1Outcomes\"", "INSERT", "AFTER" },
        { "before-warning-inventory", "vulgata.\"Pass1FailureInventoryEntries\"", "INSERT", "BEFORE" },
        { "after-warning-inventory", "vulgata.\"Pass1FailureInventoryEntries\"", "INSERT", "AFTER" },
        { "before-child-status", "vulgata.\"ScanExecutions\"", "UPDATE", "BEFORE" },
        { "after-child-status", "vulgata.\"ScanExecutions\"", "UPDATE", "AFTER" },
    };

    [Theory]
    [MemberData(nameof(ReconciliationWriteBoundaries))]
    public async Task PostgreSql17_ReconciliationFailureAtEveryWriteBoundary_RollsBackPairOutcomeChildrenWarningsAndEdges(
        string boundary,
        string table,
        string operation,
        string timing)
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        await fixture.PrepareForReconciliationAsync();
        await InstallReconciliationFailingTriggerAsync(fixture.ConnectionString, boundary, table, operation, timing);
        ReconciliationVisibility before = await fixture.ReadReconciliationVisibilityAsync();

        PreparedPass1ReconciliationResult result = await fixture.ReconcileAsync();

        Assert.Equal(PreparedPass1ReconciliationOutcome.Failed, result.Outcome);
        Assert.Equal(before, await fixture.ReadReconciliationVisibilityAsync());
    }

    [Fact]
    public async Task PostgreSql17_ReconciliationStaleWriter_IsConflictAndMutationFree()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        await fixture.PrepareForReconciliationAsync();
        ReconciliationVisibility before = await fixture.ReadReconciliationVisibilityAsync();

        PreparedPass1ReconciliationResult result = await fixture.ReconcileAsync(
            expectedPairStateVersion: await fixture.GetPairStateVersionAsync() + 1);

        Assert.Equal(PreparedPass1ReconciliationOutcome.Conflict, result.Outcome);
        Assert.Equal(before, await fixture.ReadReconciliationVisibilityAsync());
    }

    [Fact]
    public async Task PostgreSql17_ReconciliationCancellation_IsMutationFree()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        await fixture.PrepareForReconciliationAsync();
        ReconciliationVisibility before = await fixture.ReadReconciliationVisibilityAsync();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        PreparedPass1ReconciliationResult result = await fixture.ReconcileAsync(
            cancellationToken: cancellation.Token);

        Assert.Equal(PreparedPass1ReconciliationOutcome.Cancelled, result.Outcome);
        Assert.Equal(before, await fixture.ReadReconciliationVisibilityAsync());
    }

    [Fact]
    public async Task PostgreSql17_ReconciliationExactReplay_IsIdempotentAndChangedReplayConflicts()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        await fixture.PrepareForReconciliationAsync();

        PreparedPass1ReconciliationResult first = await fixture.ReconcileAsync();
        Assert.Equal(PreparedPass1ReconciliationOutcome.Reconciled, first.Outcome);
        ReconciliationVisibility afterFirst = await fixture.ReadReconciliationVisibilityAsync();

        PreparedPass1ReconciliationResult replay = await fixture.ReconcileAsync(
            expectedReconciliationFingerprint: first.ReconciliationFingerprint);
        Assert.Equal(PreparedPass1ReconciliationOutcome.Replay, replay.Outcome);
        Assert.Equal(first.ReconciliationFingerprint, replay.ReconciliationFingerprint);
        Assert.Equal(afterFirst, await fixture.ReadReconciliationVisibilityAsync());

        PreparedPass1ReconciliationResult changed = await fixture.ReconcileAsync(
            expectedReconciliationFingerprint: new string('0', 64));
        Assert.Equal(PreparedPass1ReconciliationOutcome.Conflict, changed.Outcome);
        Assert.Equal(afterFirst, await fixture.ReadReconciliationVisibilityAsync());
    }

    [Fact]
    public async Task PostgreSql17_ConcurrentReconciliation_HasDeterministicSingleEdgeAndNoPartialVisibility()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(administrator);
        await fixture.PrepareForReconciliationAsync();
        await using VulgataDbContext secondContext = PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        IPreparedPass1Store secondStore = new PreparedPass1Store(secondContext);
        PreparedPass1ReconciliationRequest request = await fixture.CreateReconciliationRequestAsync();

        Task<PreparedPass1ReconciliationResult> first = new PreparedPass1Store(fixture.Prepared.Context)
            .ReconcileAsync(fixture.PairExecutionId, request);
        Task<PreparedPass1ReconciliationResult> second = secondStore
            .ReconcileAsync(fixture.PairExecutionId, request);
        PreparedPass1ReconciliationResult[] results = await Task.WhenAll(first, second);

        Assert.Contains(results, value => value.Outcome == PreparedPass1ReconciliationOutcome.Reconciled);
        Assert.Contains(results, value => value.Outcome is
            PreparedPass1ReconciliationOutcome.Replay or PreparedPass1ReconciliationOutcome.Conflict);
        Assert.Equal(1, await CountAsync(fixture.ConnectionString, "\"DocumentEdges\""));
        Assert.Equal(1, await CountAsync(fixture.ConnectionString, "\"Pass1Outcomes\""));
        Assert.Equal(0, await CountAsync(fixture.ConnectionString, "\"Pass1FailureInventoryEntries\""));
    }

    private static async Task InstallReconciliationFailingTriggerAsync(
        string connectionString,
        string boundary,
        string table,
        string operation,
        string timing)
    {
        string suffix = boundary.Replace('-', '_');
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new($"""
            CREATE OR REPLACE FUNCTION t123_reconcile_fail_{suffix}() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'T042 injected reconciliation boundary: {boundary}';
            END $$;
            CREATE TRIGGER t123_reconcile_fail_{suffix}_trigger
            {timing} {operation} ON {table}
            FOR EACH ROW EXECUTE FUNCTION t123_reconcile_fail_{suffix}();
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string GetAdministratorConnectionStringOrSkip()
    {
        string? value = Environment.GetEnvironmentVariable("VULGATA_TEST_ADMIN_CONNECTION") ??
                        Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(value))
        {
            throw (SkipException)(Activator.CreateInstance(
                typeof(SkipException),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: ["T030 PostgreSQL qualification skipped: set VULGATA_TEST_ADMIN_CONNECTION."],
                culture: null)
                ?? throw new InvalidOperationException("xUnit SkipException constructor is unavailable."));
        }

        return value;
    }

    private static async Task InstallFailingTriggerAsync(
        string connectionString,
        string boundary,
        string table,
        string operation,
        string timing)
    {
        string suffix = boundary.Replace('-', '_');
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new($"""
            CREATE OR REPLACE FUNCTION t123_fail_{suffix}() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'T030 injected document boundary: {boundary}';
            END $$;
            CREATE TRIGGER t123_fail_{suffix}_trigger
            {timing} {operation} ON {table}
            FOR EACH ROW EXECUTE FUNCTION t123_fail_{suffix}();
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string FlattenMessages(Exception exception) =>
        string.Join(" | ", EnumerateExceptions(exception).Select(value => value.Message));

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    private static async Task SetPairPhaseAsync(
        CommitFixture fixture,
        Pass1PairExecutionPhase phase,
        Pass1CircuitState circuitState,
        Pass1PairReasonCategory reasonCategory,
        string reasonCode,
        DateTimeOffset? terminalAt)
    {
        await fixture.Prepared.Context.Pass1PairExecutions
            .Where(value => value.Id == fixture.PairExecutionId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Phase, phase)
                .SetProperty(value => value.CircuitState, circuitState)
                .SetProperty(value => value.ReasonCategory, reasonCategory)
                .SetProperty(value => value.ReasonCode, reasonCode)
                .SetProperty(value => value.StateVersion, value => value.StateVersion + 1)
                .SetProperty(value => value.UpdatedAt, PreparedScanTestHost.FixedUtcNow.AddMinutes(2))
                .SetProperty(value => value.TerminalAt, terminalAt));
        fixture.Prepared.Context.ChangeTracker.Clear();
    }

    private static async Task<int> CountAsync(string connectionString, string table, string? predicate = null)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        string where = predicate is null ? string.Empty : $" WHERE {predicate}";
        await using NpgsqlCommand command = new($"SELECT count(*)::integer FROM vulgata.{table}{where};", connection);
        return (int)(await command.ExecuteScalarAsync() ?? 0);
    }

    private sealed class CommitFixture : IAsyncDisposable
    {
        private CommitFixture(
            PreparedScanPostgreSqlFixture prepared,
            IPass1CodeLogicDocumentCommitter committer,
            Pass1DocumentCommitRequest request,
            byte[] changedCanonicalJson)
        {
            Prepared = prepared;
            Committer = committer;
            Request = request;
            ChangedCanonicalJson = changedCanonicalJson;
        }

        internal PreparedScanPostgreSqlFixture Prepared { get; }
        internal string ConnectionString => Prepared.ConnectionString;
        internal IPass1CodeLogicDocumentCommitter Committer { get; }
        internal Pass1DocumentCommitRequest Request { get; }
        internal byte[] ChangedCanonicalJson { get; }
        internal Guid PairExecutionId => Request.Packet.PairExecutionId;

        internal async Task<
            (Pass1DocumentCommitRequest Request, IReadOnlyList<CommunicationKindClassification> Classifications)>
            CreateClassificationRequestAsync(int citedClassificationCount)
        {
            if (citedClassificationCount is < 0 or > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(citedClassificationCount));
            }

            PreparedPass1WorkerPacket basePacket = Request.Packet;
            ScanExecution execution = await Prepared.Context.ScanExecutions.AsNoTracking()
                .SingleAsync(value => value.Id == basePacket.ScanExecutionId);
            Guid currentPreScanResultId = await Prepared.Context.PreScanResults.AsNoTracking()
                .Where(value => value.RepositoryId == basePacket.RepositoryId)
                .Select(value => value.Id)
                .SingleAsync();
            await Prepared.Context.ScanExecutions
                .Where(value => value.Id == execution.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    value => value.PreScanResultId,
                    currentPreScanResultId));
            CommunicationKindClassification first = CreateTestClassification(
                currentPreScanResultId,
                basePacket,
                new string('a', 64),
                $"{basePacket.RepositoryRelativePath}#L{basePacket.LineStart}",
                "t172-first");
            CommunicationKindClassification second = CreateTestClassification(
                currentPreScanResultId,
                basePacket,
                new string('b', 64),
                $"{basePacket.RepositoryRelativePath}#L{basePacket.LineStart + 1}",
                "t172-second");
            Prepared.Context.CommunicationKindClassifications.AddRange(first, second);
            await Prepared.Context.SaveChangesAsync();
            Prepared.Context.ChangeTracker.Clear();
            ScanExecution persistedExecution = await Prepared.Context.ScanExecutions.AsNoTracking()
                .SingleAsync(value => value.Id == basePacket.ScanExecutionId);
            CommunicationKindClassification[] persistedClassifications =
                await Prepared.Context.CommunicationKindClassifications.AsNoTracking()
                    .Where(value => value.Id == first.Id || value.Id == second.Id)
                    .ToArrayAsync();
            Assert.Equal(currentPreScanResultId, persistedExecution.PreScanResultId);
            Assert.Equal(2, persistedClassifications.Length);
            Assert.All(persistedClassifications, value => Assert.Equal(currentPreScanResultId, value.PreScanResultId));

            PreparedPass1CommunicationClassificationProjection[] supplied =
            [
                ToProjection(first, basePacket),
                ToProjection(second, basePacket),
            ];
            Assert.All(supplied, projection =>
            {
                CommunicationKindClassification durable = persistedClassifications.Single(
                    value => value.Id == projection.ClassificationId);
                Assert.Equal(durable.Fingerprint, projection.ClassificationFingerprint);
                Assert.Equal(durable.Kind, projection.Kind);
                Assert.Equal(durable.Level, projection.Level);
                Assert.Equal(durable.Confidence, projection.Confidence);
                Assert.Equal(durable.Rule, projection.Rule);
                Assert.Equal(durable.Provenance, projection.Provenance);
                Assert.Equal(durable.CatalogRuleIdentity, projection.CatalogRuleIdentity);
                Assert.Equal(durable.RelativePath, projection.RepositoryRelativePath);
                Assert.Equal(durable.Locator, projection.EvidenceLocator);
            });
            PreparedPass1WorkerPacket packet = basePacket with
            {
                CommunicationClassifications = supplied,
            };
            JsonObject root = JsonNode.Parse(
                Encoding.UTF8.GetString(Request.CanonicalCodeLogicJson.ToArray()))!.AsObject();
            JsonArray communicationEvidence = [];
            if (citedClassificationCount > 0)
            {
                JsonArray classificationEvidence = [];
                for (int index = 0; index < citedClassificationCount; index++)
                {
                    PreparedPass1CommunicationClassificationProjection classification = supplied[index];
                    classificationEvidence.Add(new JsonObject
                    {
                        ["classificationId"] = classification.ClassificationId.ToString("D"),
                        ["classificationFingerprint"] = classification.ClassificationFingerprint,
                        ["communicationKind"] = classification.Kind!.Value.ToString(),
                        ["level"] = classification.Level.ToString(),
                        ["confidence"] = classification.Confidence.ToString(),
                        ["evidenceRule"] = classification.Rule.ToString(),
                        ["provenance"] = classification.Provenance.ToString(),
                        ["catalogRuleIdentity"] = classification.CatalogRuleIdentity,
                        ["evidenceLocator"] = classification.EvidenceLocator,
                    });
                }

                communicationEvidence.Add(new JsonObject
                {
                    ["order"] = 1,
                    ["communicationKind"] = "Http",
                    ["observation"] = "The supplied classification is cited.",
                    ["classificationEvidence"] = classificationEvidence,
                    ["navigationReceiptIds"] = new JsonArray(),
                    ["sourceAnchorRefs"] = new JsonArray("a001"),
                });
            }

            root["communicationEvidence"] = communicationEvidence;
            byte[] candidate = Encoding.UTF8.GetBytes(
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
            PreparedPass1ParsedWorkerResult parsed = PreparedPass1ResultParser.Parse(packet, candidate);
            using (JsonDocument parsedDocument = JsonDocument.Parse(parsed.CanonicalCandidateJson.ToArray()))
            {
                Guid[] parsedIds = parsedDocument.RootElement.GetProperty("communicationEvidence")
                    .EnumerateArray()
                    .SelectMany(value => value.GetProperty("classificationEvidence").EnumerateArray())
                    .Select(value => value.GetProperty("classificationId").GetGuid())
                    .ToArray();
                Assert.Equal(citedClassificationCount, parsedIds.Length);
                Assert.All(parsedIds, id => Assert.Contains(supplied, value => value.ClassificationId == id));
            }
            return (Request with
            {
                Packet = packet,
                CanonicalCodeLogicJson = parsed.CanonicalCandidateJson,
            }, [first, second]);
        }

        internal async Task MutateClassificationBindingAsync(string mutation, Guid classificationId)
        {
            switch (mutation)
            {
                case "missing-current-pre-scan-result":
                    await Prepared.Context.ScanExecutions
                        .Where(value => value.Id == Request.Packet.ScanExecutionId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(
                            value => value.PreScanResultId,
                            Guid.Empty));
                    break;
                case "wrong-current-pre-scan-result":
                    Guid wrongPreScanResultId = await Prepared.Context.PreScanResults.AsNoTracking()
                        .Where(value => value.RepositoryId != Request.Packet.RepositoryId)
                        .Select(value => value.Id)
                        .SingleAsync();
                    await Prepared.Context.CommunicationKindClassifications
                        .Where(value => value.Id == classificationId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(
                            value => value.PreScanResultId,
                            wrongPreScanResultId));
                    break;
                case "field-mismatched-uncited-supplied":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."CommunicationKindClassifications"
                        SET "Fingerprint" = {new string('c', 64)}
                        WHERE "Id" = {classificationId};
                        """);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }

            Prepared.Context.ChangeTracker.Clear();
        }

        internal async Task PrepareForReconciliationAsync()
        {
            Pass1DocumentCommitResult committed = await Committer.CommitAsync(
                Request,
                CancellationToken.None);
            Assert.Equal(Pass1DocumentCommitStatus.Committed, committed.Status);
            await SeedRemainingCompletedJobsAsync();
            await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM "vulgata"."DocumentEdges"
                WHERE "SourceDocumentVersionId" = {Request.DocumentVersionId};
                """);
            Prepared.Context.ChangeTracker.Clear();
        }

        private async Task SeedRemainingCompletedJobsAsync()
        {
            Prepared.Context.ChangeTracker.Clear();
            ScanJobExecution[] remainingJobs = await Prepared.Context.ScanJobExecutions
                .AsNoTracking()
                .Where(value => value.Id != Request.Packet.JobId)
                .OrderBy(value => value.Id)
                .ToArrayAsync();
            if (remainingJobs.Length == 0)
            {
                return;
            }

            DocumentVersion source = await Prepared.Context.DocumentVersions
                .AsNoTracking()
                .SingleAsync(value => value.Id == Request.DocumentVersionId);
            List<(ScanJobExecution Job, LogicalDocument Logical, DocumentVersion Version)> seeded = [];
            foreach (ScanJobExecution job in remainingJobs)
            {
                CodeUnit unit = await Prepared.Context.CodeUnits
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == job.FileCodeUnitId);
                Guid logicalId = StableGuid($"t042-logical:{job.Id:D}");
                Guid versionId = StableGuid($"t042-version:{job.Id:D}");
                LogicalDocument logical = LogicalDocument.Create(
                    logicalId,
                    job.RepositoryId,
                    DocumentType.CodeLogic,
                    $"{unit.FilePath}#segment=1#t042={job.Id:D}",
                    PreparedScanTestHost.FixedUtcNow.AddMinutes(1));
                DocumentVersion version = DocumentVersion.Create(
                    versionId,
                    logicalId,
                    job.RepositoryId,
                    Prepared.SystemId,
                    job.ScanRunId,
                    source.ProfiledRevision,
                    1,
                    unit.FilePath,
                    unit.FilePath,
                    "code-logic/file-v2",
                    source.ContentJson,
                    source.ContentMarkdown,
                    source.ContentFingerprint,
                    source.CommitFingerprint,
                    PreparedScanTestHost.FixedUtcNow.AddMinutes(1));
                seeded.Add((job, logical, version));
                Prepared.Context.LogicalDocuments.Add(logical);
                Prepared.Context.DocumentVersions.Add(version);
            }

            await Prepared.Context.SaveChangesAsync();
            foreach ((_, LogicalDocument logical, DocumentVersion version) in seeded)
            {
                logical.SetInitialCurrentVersion(version);
            }

            await Prepared.Context.SaveChangesAsync();
            foreach ((ScanJobExecution job, _, DocumentVersion version) in seeded)
            {
                await Prepared.Context.ScanJobExecutions
                    .Where(value => value.Id == job.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(value => value.State, ScanJobExecutionState.Completed)
                        .SetProperty(value => value.StateVersion, 1L)
                        .SetProperty(value => value.LeaseToken, (Guid?)null)
                        .SetProperty(value => value.LeaseExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(value => value.NextAttemptAt, (DateTimeOffset?)null)
                        .SetProperty(value => value.FailureCategory, (ScanJobFailureCategory?)null)
                        .SetProperty(value => value.FailureCode, (string?)null)
                        .SetProperty(value => value.CommittedDocumentVersionId, version.Id)
                        .SetProperty(value => value.StateChangedAt, PreparedScanTestHost.FixedUtcNow.AddMinutes(1)));
            }

            Prepared.Context.ChangeTracker.Clear();
        }

        internal async Task<long> GetPairStateVersionAsync() =>
            await Prepared.Context.Pass1PairExecutions.AsNoTracking()
                .Where(value => value.Id == PairExecutionId)
                .Select(value => value.StateVersion)
                .SingleAsync();

        internal async Task<bool> ApplyCanonicalGraphCorruptionAsync(string corruption)
        {
            try
            {
                switch (corruption)
                {
                case "source-anchor-remove":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        DELETE FROM "vulgata"."SourceAnchors"
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId};
                        """);
                    break;
                case "source-anchor-mutate":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."SourceAnchors"
                        SET "EvidenceLocator" = "EvidenceLocator" || '#t060-mutated'
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId};
                        """);
                    break;
                case "source-anchor-reorder":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO "vulgata"."SourceAnchors" (
                            "Id", "DocumentVersionId", "RepositoryId", "ScanRunId", "CodeUnitId",
                            "ProfiledRevision", "RepositoryRelativePath", "EvidenceLocator",
                            "LineStart", "LineEnd", "SourceHash", "ByteStart", "ByteEnd", "CreatedAt")
                        SELECT {ExtraAnchorId}, "DocumentVersionId", "RepositoryId", "ScanRunId", "CodeUnitId",
                            "ProfiledRevision", "RepositoryRelativePath", "EvidenceLocator" || '#t060-reorder',
                            "LineStart", "LineEnd", "SourceHash", "ByteStart", "ByteEnd", "CreatedAt"
                        FROM "vulgata"."SourceAnchors"
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId}
                        LIMIT 1;
                        UPDATE "vulgata"."SourceAnchors"
                        SET "LineStart" = 2, "LineEnd" = 3
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId}
                          AND "Id" <> {ExtraAnchorId};
                        UPDATE "vulgata"."SourceAnchors"
                        SET "LineStart" = 1, "LineEnd" = 2
                        WHERE "Id" = {ExtraAnchorId};
                        """);
                    break;
                case "source-anchor-duplicate":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO "vulgata"."SourceAnchors" (
                            "Id", "DocumentVersionId", "RepositoryId", "ScanRunId", "CodeUnitId",
                            "ProfiledRevision", "RepositoryRelativePath", "EvidenceLocator",
                            "LineStart", "LineEnd", "SourceHash", "ByteStart", "ByteEnd", "CreatedAt")
                        SELECT "Id", "DocumentVersionId", "RepositoryId", "ScanRunId", "CodeUnitId",
                            "ProfiledRevision", "RepositoryRelativePath", "EvidenceLocator",
                            "LineStart", "LineEnd", "SourceHash", "ByteStart", "ByteEnd", "CreatedAt"
                        FROM "vulgata"."SourceAnchors"
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId}
                        LIMIT 1;
                        """);
                    break;
                case "source-anchor-cardinality":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        DELETE FROM "vulgata"."SourceAnchors"
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId};
                        """);
                    break;
                case "document-section-remove":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        DELETE FROM "vulgata"."DocumentSections"
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId}
                          AND "SectionOrder" = 1;
                        """);
                    break;
                case "document-section-mutate":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."DocumentSections"
                        SET "Heading" = "Heading" || '（T060）'
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId}
                          AND "SectionOrder" = 1;
                        """);
                    break;
                case "document-section-reorder":
                    Guid[] sectionIds = await Prepared.Context.DocumentSections.AsNoTracking()
                        .Where(value => value.DocumentVersionId == Request.DocumentVersionId)
                        .OrderBy(value => value.SectionOrder)
                        .Select(value => value.Id)
                        .Take(2)
                        .ToArrayAsync();
                    Assert.Equal(2, sectionIds.Length);
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."DocumentSections" SET "SectionOrder" = 1000 WHERE "Id" = {sectionIds[0]};
                        UPDATE "vulgata"."DocumentSections" SET "SectionOrder" = 1 WHERE "Id" = {sectionIds[1]};
                        UPDATE "vulgata"."DocumentSections" SET "SectionOrder" = 2 WHERE "Id" = {sectionIds[0]};
                        """);
                    break;
                case "document-section-duplicate":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO "vulgata"."DocumentSections" (
                            "Id", "LogicalDocumentId", "DocumentVersionId", "RepositoryId", "StableSectionId",
                            "SectionOrder", "SectionKind", "Heading", "ContentMarkdown", "SearchText",
                            "SourceAnchorIdsJson", "DocumentVersionIdsJson", "CreatedAt")
                        SELECT "Id", "LogicalDocumentId", "DocumentVersionId", "RepositoryId", "StableSectionId",
                            "SectionOrder", "SectionKind", "Heading", "ContentMarkdown", "SearchText",
                            "SourceAnchorIdsJson", "DocumentVersionIdsJson", "CreatedAt"
                        FROM "vulgata"."DocumentSections"
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId}
                        LIMIT 1;
                        """);
                    break;
                case "document-section-cardinality":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO "vulgata"."DocumentSections" (
                            "Id", "LogicalDocumentId", "DocumentVersionId", "RepositoryId", "StableSectionId",
                            "SectionOrder", "SectionKind", "Heading", "ContentMarkdown", "SearchText",
                            "SourceAnchorIdsJson", "DocumentVersionIdsJson", "CreatedAt")
                        SELECT {ExtraSectionId}, "LogicalDocumentId", "DocumentVersionId", "RepositoryId", {ExtraSectionId},
                            999, "SectionKind", "Heading" || ' extra', "ContentMarkdown", "SearchText",
                            "SourceAnchorIdsJson", "DocumentVersionIdsJson", "CreatedAt"
                        FROM "vulgata"."DocumentSections"
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId}
                        LIMIT 1;
                        """);
                    break;
                case "index-entry-remove":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        DELETE FROM "vulgata"."IndexEntries"
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId};
                        """);
                    break;
                case "index-entry-mutate":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."IndexEntries"
                        SET "SearchText" = "SearchText" || ' t060-mutated'
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId};
                    """);
                    break;
                case "index-entry-reorder":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."IndexEntries"
                        SET "Id" = {ExtraEdgeId}
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId};
                        """);
                    break;
                case "index-entry-duplicate":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO "vulgata"."IndexEntries" (
                            "Id", "LogicalDocumentId", "DocumentVersionId", "RepositoryId", "SystemId",
                            "DocumentType", "Title", "Summary", "RepositoryRelativePath", "KeySymbolsJson",
                            "BusinessTermsJson", "TechnicalTermsJson", "SearchText", "CreatedAt")
                        SELECT "Id", "LogicalDocumentId", "DocumentVersionId", "RepositoryId", "SystemId",
                            "DocumentType", "Title", "Summary", "RepositoryRelativePath", "KeySymbolsJson",
                            "BusinessTermsJson", "TechnicalTermsJson", "SearchText", "CreatedAt"
                        FROM "vulgata"."IndexEntries"
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId};
                        """);
                    break;
                case "index-entry-cardinality":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        DELETE FROM "vulgata"."IndexEntries"
                        WHERE "DocumentVersionId" = {Request.DocumentVersionId};
                        """);
                    break;
                case "direct-reference-edge-remove":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        DELETE FROM "vulgata"."DocumentEdges"
                        WHERE "SourceDocumentVersionId" = {Request.DocumentVersionId}
                          AND "EdgeType" = 'References';
                        """);
                    break;
                case "direct-reference-edge-mutate":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."DocumentEdges"
                        SET "EdgeType" = 'GeneratedFrom'
                        WHERE "SourceDocumentVersionId" = {Request.DocumentVersionId}
                          AND "EdgeType" = 'References';
                        """);
                    break;
                case "direct-reference-edge-reorder":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."DocumentEdges"
                        SET "Id" = {ExtraEdgeId}
                        WHERE "SourceDocumentVersionId" = {Request.DocumentVersionId}
                          AND "EdgeType" = 'References';
                        """);
                    break;
                case "direct-reference-edge-duplicate":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO "vulgata"."DocumentEdges" (
                            "Id", "SourceDocumentVersionId", "SourceRepositoryId", "SourceScanRunId",
                            "TargetDocumentVersionId", "TargetRepositoryId", "TargetScanRunId", "EdgeType", "CreatedAt")
                        SELECT "Id", "SourceDocumentVersionId", "SourceRepositoryId", "SourceScanRunId",
                            "TargetDocumentVersionId", "TargetRepositoryId", "TargetScanRunId", "EdgeType", "CreatedAt"
                        FROM "vulgata"."DocumentEdges"
                        WHERE "SourceDocumentVersionId" = {Request.DocumentVersionId}
                          AND "EdgeType" = 'References'
                        LIMIT 1;
                        """);
                    break;
                case "direct-reference-edge-cardinality":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO "vulgata"."DocumentEdges" (
                            "Id", "SourceDocumentVersionId", "SourceRepositoryId", "SourceScanRunId",
                            "TargetDocumentVersionId", "TargetRepositoryId", "TargetScanRunId", "EdgeType", "CreatedAt")
                        SELECT {ExtraEdgeId}, "Id", "RepositoryId", "ScanRunId",
                            "Id", "RepositoryId", "ScanRunId", 'References', {PreparedScanTestHost.FixedUtcNow}
                        FROM "vulgata"."DocumentVersions"
                        WHERE "Id" = {Request.DocumentVersionId};
                        """);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(corruption), corruption, null);
                }
            }
            catch (PostgresException exception) when (corruption.EndsWith("-duplicate", StringComparison.Ordinal))
            {
                Assert.Equal("23505", exception.SqlState);
                Prepared.Context.ChangeTracker.Clear();
                return true;
            }

            Prepared.Context.ChangeTracker.Clear();
            return false;
        }

        internal async Task<bool> ApplyReconciledEdgeCorruptionAsync(string corruption)
        {
            try
            {
                switch (corruption)
                {
                case "reconciled-edge-remove":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        DELETE FROM "vulgata"."DocumentEdges"
                        WHERE "SourceDocumentVersionId" = {Request.DocumentVersionId}
                          AND "EdgeType" = 'References';
                        """);
                    break;
                case "reconciled-edge-mutate":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."DocumentEdges"
                        SET "EdgeType" = 'GeneratedFrom'
                        WHERE "SourceDocumentVersionId" = {Request.DocumentVersionId}
                          AND "EdgeType" = 'References';
                    """);
                    break;
                case "reconciled-edge-reorder":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."DocumentEdges"
                        SET "Id" = {ExtraEdgeId}
                        WHERE "SourceDocumentVersionId" = {Request.DocumentVersionId}
                          AND "EdgeType" = 'References';
                        """);
                    break;
                case "reconciled-edge-duplicate":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO "vulgata"."DocumentEdges" (
                            "Id", "SourceDocumentVersionId", "SourceRepositoryId", "SourceScanRunId",
                            "TargetDocumentVersionId", "TargetRepositoryId", "TargetScanRunId", "EdgeType", "CreatedAt")
                        SELECT "Id", "SourceDocumentVersionId", "SourceRepositoryId", "SourceScanRunId",
                            "TargetDocumentVersionId", "TargetRepositoryId", "TargetScanRunId", "EdgeType", "CreatedAt"
                        FROM "vulgata"."DocumentEdges"
                        WHERE "SourceDocumentVersionId" = {Request.DocumentVersionId}
                          AND "EdgeType" = 'References'
                        LIMIT 1;
                        """);
                    break;
                case "reconciled-edge-cardinality":
                    await Prepared.Context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO "vulgata"."DocumentEdges" (
                            "Id", "SourceDocumentVersionId", "SourceRepositoryId", "SourceScanRunId",
                            "TargetDocumentVersionId", "TargetRepositoryId", "TargetScanRunId", "EdgeType", "CreatedAt")
                        SELECT {ExtraEdgeId}, "Id", "RepositoryId", "ScanRunId",
                            "Id", "RepositoryId", "ScanRunId", 'References', {PreparedScanTestHost.FixedUtcNow}
                        FROM "vulgata"."DocumentVersions"
                        WHERE "Id" = {Request.DocumentVersionId};
                    """);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(corruption), corruption, null);
                }
            }
            catch (PostgresException exception) when (corruption.EndsWith("-duplicate", StringComparison.Ordinal))
            {
                Assert.Equal("23505", exception.SqlState);
                Prepared.Context.ChangeTracker.Clear();
                return true;
            }

            Prepared.Context.ChangeTracker.Clear();
            return false;
        }

        internal async Task<GraphSnapshot> ReadGraphSnapshotAsync()
        {
            Prepared.Context.ChangeTracker.Clear();
            return new(
                await ReadRowsDigestAsync(ConnectionString, "SourceAnchors", Request.DocumentVersionId),
                await ReadRowsDigestAsync(ConnectionString, "DocumentSections", Request.DocumentVersionId),
                await ReadRowsDigestAsync(ConnectionString, "IndexEntries", Request.DocumentVersionId),
                await ReadRowsDigestAsync(ConnectionString, "DocumentEdges", Request.DocumentVersionId),
                await Prepared.Context.ScanJobExecutions.AsNoTracking()
                    .Where(value => value.Id == Request.Packet.JobId)
                    .Select(value => value.State + ":" + value.StateVersion + ":" + value.CommittedDocumentVersionId)
                    .SingleAsync());
        }

        internal Task<string> ReadReferencesEdgeDigestAsync() =>
            ReadRowsDigestAsync(ConnectionString, "DocumentEdges", Request.DocumentVersionId, referencesOnly: true);

        internal async Task<PreparedPass1ReconciliationRequest> CreateReconciliationRequestAsync(
            long? expectedPairStateVersion = null,
            string? expectedReconciliationFingerprint = null) =>
            new(
                Prepared.Authorization,
                expectedPairStateVersion ?? await GetPairStateVersionAsync(),
                expectedReconciliationFingerprint,
                PreparedScanTestHost.FixedUtcNow.AddMinutes(2));

        internal async Task<PreparedPass1ReconciliationResult> ReconcileAsync(
            long? expectedPairStateVersion = null,
            string? expectedReconciliationFingerprint = null,
            CancellationToken cancellationToken = default)
        {
            PreparedPass1ReconciliationRequest request = await CreateReconciliationRequestAsync(
                expectedPairStateVersion,
                expectedReconciliationFingerprint);
            return await new PreparedPass1Store(Prepared.Context)
                .ReconcileAsync(PairExecutionId, request, cancellationToken);
        }

        internal async Task<ReconciliationVisibility> ReadReconciliationVisibilityAsync()
        {
            Prepared.Context.ChangeTracker.Clear();
            GraphCounts graph = await GraphCounts.ReadAsync(ConnectionString);
            int outcomes = await CountAsync(ConnectionString, "\"Pass1Outcomes\"");
            int failureInventory = await CountAsync(ConnectionString, "\"Pass1FailureInventoryEntries\"");
            string jobs = string.Join(
                "|",
                await Prepared.Context.ScanJobExecutions.AsNoTracking()
                    .OrderBy(value => value.Id)
                    .Select(value => value.Id.ToString() + ":" + value.State + ":" + value.StateVersion + ":" + value.CommittedDocumentVersionId)
                    .ToArrayAsync());
            string children = string.Join(
                "|",
                await Prepared.Context.ScanExecutions.AsNoTracking()
                    .OrderBy(value => value.Id)
                    .Select(value => value.Id.ToString() + ":" + value.Phase + ":" + value.StateVersion)
                    .ToArrayAsync());
            Pass1PairExecution pair = await Prepared.Context.Pass1PairExecutions.AsNoTracking()
                .SingleAsync(value => value.Id == PairExecutionId);
            TwoRepositoryScanCoordination coordination = await Prepared.Context.TwoRepositoryScanCoordinations
                .AsNoTracking()
                .SingleAsync(value => value.Id == pair.CoordinationId);
            return new(
                graph,
                outcomes,
                failureInventory,
                jobs,
                children,
                pair.Phase + ":" + pair.StateVersion,
                coordination.Phase + ":" + coordination.StateVersion);
        }

        internal static async Task<CommitFixture> CreateAsync(string administrator)
        {
            PreparedScanPostgreSqlFixture prepared = await PreparedScanPostgreSqlFixture.CreateAsync(administrator);
            try
            {
                await CommitPreparedPairAsync(prepared);
                await PromoteFixtureExecutionsAsync(prepared);
                IPreparedPass1Store store = new PreparedPass1Store(prepared.Context);
                PreparedPass1StartResult started = await store
                    .StartAsync(prepared.Authorization, CreateHandoff(prepared.Package), 2, CancellationToken.None);
                Assert.Equal(PreparedPass1StartOutcome.Created, started.Outcome);
                Pass1PairExecution pair = await prepared.Context.Pass1PairExecutions.SingleAsync();
                PreparedPass1ClaimResult claim = await store
                    .ClaimNextAsync(pair.Id, PreparedScanTestHost.FixedUtcNow, TimeSpan.FromMinutes(5), CancellationToken.None);
                Assert.Equal(PreparedPass1ClaimOutcome.Claimed, claim.Outcome);
                ScanJobExecution job = await prepared.Context.ScanJobExecutions.SingleAsync(value => value.Id == claim.JobId);
                CodeUnit unit = await prepared.Context.CodeUnits.SingleAsync(value => value.Id == job.FileCodeUnitId);
                CodeUnit targetUnit = await prepared.Context.CodeUnits
                    .Where(value => value.RepositoryId == unit.RepositoryId && value.Id != unit.Id)
                    .OrderBy(value => value.FilePath)
                    .FirstAsync();
                byte[] source = Encoding.UTF8.GetBytes("package neutral;\nclass OrderPolicy { }\n");
                string sourceHash = Convert.ToHexStringLower(SHA256.HashData(source));
                string fileHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("t030-file")));
                await prepared.Context.CodeUnits.Where(value => value.Id == unit.Id).ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.FileHash, fileHash)
                    .SetProperty(value => value.SourceHash, sourceHash)
                    .SetProperty(value => value.ByteStart, 0L)
                    .SetProperty(value => value.ByteEndExclusive, (long)source.Length)
                    .SetProperty(value => value.SegmentOrdinal, 1)
                    .SetProperty(value => value.SegmentCount, 1)
                    .SetProperty(value => value.LineStart, 1)
                    .SetProperty(value => value.LineEnd, 3));
                prepared.Context.ChangeTracker.Clear();
                unit = await prepared.Context.CodeUnits.AsNoTracking().SingleAsync(value => value.Id == unit.Id);
                ScanExecution execution = await prepared.Context.ScanExecutions.AsNoTracking().SingleAsync(value => value.Id == job.ScanExecutionId);
                PreparedScanManifest manifest = await prepared.Context.PreparedScanManifests.AsNoTracking().SingleAsync(value => value.RepositoryId == unit.RepositoryId);
                PreparedScanManifestEntry entry = await prepared.Context.PreparedScanManifestEntries.AsNoTracking()
                    .SingleAsync(value => value.PreparedScanManifestId == manifest.Id && value.FileCodeUnitId == unit.Id);
                PreparedRepositoryPackage repository = prepared.Package.Repositories.Single(value => value.RepositoryId == unit.RepositoryId);
                await SeedTargetGraphAsync(prepared.Context, execution, pair, job, targetUnit);
                PreparedPass1WorkerPacket packet = new(
                    pair.Id, job.Id, execution.Id, prepared.SystemId, unit.RepositoryId, unit.ScanRunId,
                    execution.ProfiledRevision, unit.Id, unit.FileCodeUnitIdentityFingerprint!, manifest.Id,
                    manifest.Fingerprint, pair.PairPackageFingerprint, unit.FilePath, unit.ByteStart!.Value,
                    unit.ByteEndExclusive!.Value, unit.LineStart, unit.LineEnd, unit.SegmentOrdinal!.Value,
                    unit.SegmentCount!.Value, unit.FileHash!, unit.SourceHash, execution.PromptIdentity!,
                    execution.DocumentSchemaIdentity!, execution.AnalysisRuntimeIdentityHash!, entry.ManifestOrdinal,
                    entry.PairSelectionOrdinal, source,
                    new PreparedPass1ContextSnapshotProjection(
                        repository.ContextSnapshotId, repository.RepositoryId, repository.ScanRunId,
                        repository.ProfiledRevision, repository.PreparedScanContextIdentityHash,
                        repository.ContextContentHash, repository.GlobalContext, repository.SystemContext,
                        repository.RepositoryContext), []);
                PreparedPass1ParsedWorkerResult parsed = PreparedPass1ResultParser.Parse(
                    packet, CreateCanonicalJson(packet, targetUnit.FilePath, targetUnit.SourceHash));
                Guid logicalId = PreparedPass1CanonicalCodec.CreateLogicalDocument(new(packet.RepositoryId, packet.RepositoryRelativePath, (ulong)packet.SegmentOrdinal)).Id!.Value;
                Guid versionId = PreparedPass1CanonicalCodec.CreateDocumentVersion(new(packet.JobId, Convert.FromHexString(packet.FileCodeUnitIdentityFingerprint), packet.DocumentSchemaIdentity)).Id!.Value;
                Pass1DocumentCommitRequest request = new(packet, parsed.CanonicalCandidateJson, logicalId, versionId, job.StateVersion, claim.LeaseToken, PreparedScanTestHost.FixedUtcNow.AddMinutes(1));
                JsonObject changed = JsonNode.Parse(Encoding.UTF8.GetString(parsed.CanonicalCandidateJson.Span))!.AsObject();
                changed["filePurpose"]!["summary"] = "changed replay";
                return new(prepared, new Pass1CodeLogicDocumentCommitter(prepared.Context), request,
                    Encoding.UTF8.GetBytes(changed.ToJsonString(new JsonSerializerOptions { WriteIndented = false })));
            }
            catch
            {
                await prepared.DisposeAsync();
                throw;
            }
        }

        internal async Task<ScanJobExecutionState> ReadJobStateAsync() =>
            await Prepared.Context.ScanJobExecutions.AsNoTracking()
                .Where(value => value.Id == Request.Packet.JobId)
                .Select(value => value.State)
                .SingleAsync();

        public async ValueTask DisposeAsync() => await Prepared.DisposeAsync();
    }

    private sealed record GraphSnapshot(
        string SourceAnchors,
        string DocumentSections,
        string IndexEntries,
        string DocumentEdges,
        string Job);

    private static async Task<string> ReadRowsDigestAsync(
        string connectionString,
        string table,
        Guid documentVersionId,
        bool referencesOnly = false)
    {
        string edgeFilter = referencesOnly ? " AND \"EdgeType\" = 'References'" : string.Empty;
        string versionColumn = table == "DocumentEdges"
            ? "SourceDocumentVersionId"
            : "DocumentVersionId";
        string sql = $"""
            SELECT COALESCE(string_agg(row_to_json(rows)::text, '|' ORDER BY rows."Id"), '')
            FROM (
                SELECT * FROM vulgata."{table}"
                WHERE "{versionColumn}" = @documentVersionId{edgeFilter}
            ) rows;
            """;
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("documentVersionId", documentVersionId);
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private sealed record GraphCounts(
        int LogicalDocuments,
        int DocumentVersions,
        int SourceAnchors,
        int DocumentSections,
        int IndexEntries,
        int DocumentEdges,
        int CompletedJobs)
    {
        internal static async Task<GraphCounts> ReadAsync(string connectionString) => new(
            await CountAsync(connectionString, "\"LogicalDocuments\""),
            await CountAsync(connectionString, "\"DocumentVersions\""),
            await CountAsync(connectionString, "\"SourceAnchors\""),
            await CountAsync(connectionString, "\"DocumentSections\""),
            await CountAsync(connectionString, "\"IndexEntries\""),
            await CountAsync(connectionString, "\"DocumentEdges\""),
            await CountAsync(connectionString, "\"ScanJobExecutions\"", "\"State\" = 'Completed'"));
    }

    private sealed record ReconciliationVisibility(
        GraphCounts Graph,
        int OutcomeCount,
        int FailureInventoryCount,
        string Jobs,
        string Children,
        string Pair,
        string Coordination);

    private static CommunicationKindClassification CreateTestClassification(
        Guid preScanResultId,
        PreparedPass1WorkerPacket packet,
        string fingerprint,
        string locator,
        string catalogRuleIdentity) =>
        CommunicationKindClassification.CreatePrepared(
            preScanResultId,
            CommunicationKind.Http,
            ClassificationLevel.Confirmed,
            ConfidenceBand.High,
            EvidenceRule.HttpClientAnnotation,
            EvidenceProvenance.StructuralAnnotation,
            packet.RepositoryRelativePath,
            locator,
            1,
            fingerprint,
            catalogRuleIdentity);

    private static PreparedPass1CommunicationClassificationProjection ToProjection(
        CommunicationKindClassification classification,
        PreparedPass1WorkerPacket packet) =>
        new(
            packet.FileCodeUnitId,
            classification.Id,
            packet.PreparedScanManifestId,
            packet.RepositoryId,
            packet.ScanRunId,
            packet.ProfiledRevision,
            classification.Fingerprint,
            classification.Kind,
            classification.Level,
            classification.Confidence,
            classification.Rule,
            classification.Provenance,
            classification.CatalogRuleIdentity!,
            classification.RelativePath,
            classification.Locator);

    private static byte[] CreateCanonicalJson(
        PreparedPass1WorkerPacket packet,
        string targetPath,
        string targetSourceHash)
    {
        MethodInfo method = typeof(CodeLogicFileV2SchemaTests).GetMethod("BaseJson", BindingFlags.NonPublic | BindingFlags.Static)!;
        JsonObject root = JsonNode.Parse((string)method.Invoke(null, null)!)!.AsObject();
        JsonObject binding = root["binding"]!.AsObject();
        binding["jobId"] = packet.JobId.ToString("D");
        binding["scanExecutionId"] = packet.ScanExecutionId.ToString("D");
        binding["systemId"] = packet.SystemId.ToString("D");
        binding["repositoryId"] = packet.RepositoryId.ToString("D");
        binding["scanRunId"] = packet.ScanRunId.ToString("D");
        binding["profiledRevision"] = packet.ProfiledRevision;
        binding["fileCodeUnitId"] = packet.FileCodeUnitId.ToString("D");
        binding["fileCodeUnitIdentityFingerprint"] = packet.FileCodeUnitIdentityFingerprint;
        binding["preparedScanManifestId"] = packet.PreparedScanManifestId.ToString("D");
        binding["preparedScanManifestFingerprint"] = packet.PreparedScanManifestFingerprint;
        binding["pairPackageFingerprint"] = packet.PairPackageFingerprint;
        binding["repositoryRelativePath"] = packet.RepositoryRelativePath;
        binding["byteStart"] = packet.ByteStart;
        binding["byteEnd"] = packet.ByteEnd;
        binding["lineStart"] = packet.LineStart;
        binding["lineEnd"] = packet.LineEnd;
        binding["fileHash"] = packet.FileHash;
        binding["sourceHash"] = packet.SourceHash;
        binding["promptIdentity"] = packet.PromptIdentity;
        binding["analysisRuntimeIdentityHash"] = packet.AnalysisRuntimeIdentityHash;
        JsonObject anchor = root["sourceAnchors"]![0]!.AsObject();
        anchor["repositoryRelativePath"] = packet.RepositoryRelativePath;
        anchor["byteStart"] = packet.ByteStart;
        anchor["byteEnd"] = packet.ByteEnd;
        anchor["lineStart"] = packet.LineStart;
        anchor["lineEnd"] = packet.LineEnd;
        anchor["sourceHash"] = packet.SourceHash;
        anchor["evidenceLocator"] =
            $"{packet.RepositoryRelativePath}#L{packet.LineStart}-L{packet.LineEnd}";
        root["communicationEvidence"] = new JsonArray();
        int sourceClaimLine = packet.LineStart < packet.LineEnd
            ? packet.LineStart + 1
            : packet.LineStart;
        JsonObject reference = root["knownReferences"]![0]!.AsObject();
        reference["targetDocumentVersionId"] = TargetDocumentVersionId.ToString("D");
        reference["targetRepositoryRelativePath"] = targetPath;
        reference["targetSourceHash"] = targetSourceHash;
        reference["navigationReceiptId"] = NavigationReceiptId.ToString("D");
        reference["sourceClaimLocator"] =
            $"{packet.RepositoryRelativePath}#L{sourceClaimLine}";
        JsonObject deferred = root["deferredObservations"]![0]!.AsObject();
        deferred["targetRepositoryRelativePath"] = targetPath;
        deferred["targetSourceHash"] = targetSourceHash;
        deferred["navigationReceiptIds"] = new JsonArray(NavigationReceiptId.ToString("D"));
        deferred["sourceClaimLocator"] =
            $"{packet.RepositoryRelativePath}#L{sourceClaimLine}";
        return Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
    }

    private static Guid StableGuid(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16], bigEndian: true);
    }

    private static async Task SeedTargetGraphAsync(
        VulgataDbContext context,
        ScanExecution execution,
        Pass1PairExecution pair,
        ScanJobExecution job,
        CodeUnit targetUnit)
    {
        DateTimeOffset now = PreparedScanTestHost.FixedUtcNow;
        LogicalDocument logical = LogicalDocument.Create(
            TargetLogicalDocumentId,
            targetUnit.RepositoryId,
            DocumentType.CodeLogic,
            $"{targetUnit.FilePath}#segment=1",
            now);
        DocumentVersion version = DocumentVersion.Create(
            TargetDocumentVersionId,
            TargetLogicalDocumentId,
            targetUnit.RepositoryId,
            execution.SystemId,
            execution.ScanRunId,
            execution.ProfiledRevision,
            1,
            targetUnit.FilePath,
            targetUnit.FilePath,
            "code-logic/file-v2",
            "{}",
            "# target",
            new string('1', 64),
            new string('2', 64),
            now);
        context.LogicalDocuments.Add(logical);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        context.DocumentVersions.Add(version);
        context.SourceAnchors.Add(new SourceAnchor(
            TargetAnchorId,
            TargetDocumentVersionId,
            targetUnit.RepositoryId,
            execution.ScanRunId,
            targetUnit.Id,
            execution.ProfiledRevision,
            targetUnit.FilePath,
            $"{targetUnit.FilePath}#L1-L4",
            targetUnit.LineStart,
            targetUnit.LineEnd,
            targetUnit.SourceHash,
            now));
        context.Pass1NavigationReceipts.Add(Pass1NavigationReceipt.Create(
            NavigationReceiptId,
            job.Id,
            pair.Id,
            1,
            1,
            Pass1NavigationOperation.Definition,
            1,
            Pass1NavigationOutcomeKind.DocumentEvidence,
            null,
            new string('3', 64),
            new string('4', 64),
            1,
            32,
            "{}",
            now));
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {TargetDocumentVersionId} WHERE \"Id\" = {TargetLogicalDocumentId} AND \"RepositoryId\" = {targetUnit.RepositoryId}");
        context.ChangeTracker.Clear();
    }

    private static async Task CommitPreparedPairAsync(PreparedScanPostgreSqlFixture fixture)
    {
        PreparedScanCommitResult result = await PreparedScanPostgreSqlFixture.CreateStore(fixture.Context)
            .CommitPairAsync(fixture.Authorization, fixture.Package, 0, CancellationToken.None);
        Assert.Equal(PreparedScanCommitOutcome.Committed, result.Outcome);
    }

    private static async Task PromoteFixtureExecutionsAsync(PreparedScanPostgreSqlFixture fixture)
    {
        const string runtime = "{\"runtime\":\"story-12-3-test\"}";
        string runtimeHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(runtime)));
        Guid[] repositories = fixture.Package.Repositories.Select(value => value.RepositoryId).OrderBy(value => value).ToArray();
        for (int index = 0; index < repositories.Length; index++)
        {
            await fixture.Context.ScanExecutions.Where(value => value.RepositoryId == repositories[index]).ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.PreScanResultId, Guid.Empty)
                .SetProperty(value => value.Phase, ScanExecutionPhase.Prepared)
                .SetProperty(value => value.PromptIdentity, $"story-12-3-test-prompt-{index + 1}")
                .SetProperty(value => value.DocumentSchemaIdentity, "code-logic/file-v2")
                .SetProperty(value => value.AnalysisRuntimeIdentityJson, runtime)
                .SetProperty(value => value.AnalysisRuntimeIdentityHash, runtimeHash)
                .SetProperty(value => value.UpdatedAt, PreparedScanTestHost.FixedUtcNow));
        }

        await fixture.Context.PreparedScanManifests.ExecuteUpdateAsync(setters => setters
            .SetProperty(value => value.RuntimeIdentityHash, runtimeHash));

        fixture.Context.ChangeTracker.Clear();
    }

    private static PreparedScanHandoff CreateHandoff(PreparedScanPairPackage package) => new(
        PreScanResult.PreparedHandoffSchemaIdentity, package.CoordinationId, package.PairSelectionFingerprint,
        package.PairPackageFingerprint,
        package.Repositories.OrderBy(value => value.RepositoryId).Select(repository => new PreparedScanManifestBinding(
            repository.RepositoryId, repository.ScanRunId, repository.ProfiledRevision,
            PreparedScanIdentityBuilders.CreateDeterministicIdentity("PreparedScanManifest", repository.ManifestFingerprint),
            repository.ManifestFingerprint,
            PreparedScanIdentityBuilders.CreateCodeGraphProof(repository.CodeGraphProof).Fingerprint,
            PreparedScanIdentityBuilders.CreateJavaReadiness(repository.JavaReadiness).Fingerprint,
            repository.PreparedScanContextIdentityHash)).ToArray(),
        package.Repositories.SelectMany(value => value.FileCodeUnits).ToArray(), package.SelectedCount);
}
