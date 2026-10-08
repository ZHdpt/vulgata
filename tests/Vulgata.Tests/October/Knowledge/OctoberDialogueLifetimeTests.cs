using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Real SQLite store with controlled factory/guard/cleanup gates; no provider or PostgreSQL qualification.</summary>
public sealed class OctoberDialogueLifetimeTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeAsync_PendingPhysicalCommit_DrainsGuardTransactionAndProducingContextAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        var admission = await f.AdmitAsync();
        var request = f.TerminalRequest(admission);
        var physical = new AdmissionPhysicalCommitProbe();
        await using var reference = f.Factory.CreateDbContext();
        var options = new DbContextOptionsBuilder<OctoberDbContext>().UseSqlite(reference.Database.GetConnectionString())
            .AddInterceptors(physical, new AdmissionCommandAdapter()).Options;
        var factory = new AdmissionLifetimeFactory(options);
        var guard = new LifetimeGuard { SubmitBeforePause = true };
        var store = Store(f, factory, guard);
        var lifetime = Assert.IsAssignableFrom<IAsyncDisposable>((object)store);
        using var cancellation = new CancellationTokenSource();
        Task? disposal = null;
        var call = store.SaveTerminalAsync(request, f.Subject, cancellation.Token);
        try
        {
            await physical.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.Equal(DialogueStatus.Indeterminate, (await call.WaitAsync(TimeSpan.FromSeconds(2))).Status);
            disposal = lifetime.DisposeAsync().AsTask();
            Assert.Same(disposal, lifetime.DisposeAsync().AsTask());
            Assert.False(disposal.IsCompleted);
            Assert.False(physical.Disposed.Task.IsCompleted);
            var producing = Assert.IsType<OctoberDbContext>(physical.ProducingContext);
            var connection = producing.Database.GetDbConnection();
            Assert.Null(Record.Exception(() => producing.ChangeTracker.Entries().ToArray()));

            physical.Release.TrySetResult();
            await guard.SettlementObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(disposal.IsCompleted);
            // Actual commit has settled, but ignored-cancellation guard cleanup remains owned.
            guard.Release.TrySetResult();
            await guard.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(physical.Disposed.Task.IsCompleted);
            Assert.Equal(ConnectionState.Closed, connection.State);
            Assert.Throws<ObjectDisposedException>(() => producing.ChangeTracker.Entries().ToArray());
        }
        finally
        {
            physical.Release.TrySetResult(); guard.Release.TrySetResult();
            await call.WaitAsync(TimeSpan.FromSeconds(5));
            if (disposal is not null) await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        }
        var lookup = await f.Store.LookupTerminalAsync(new(f.Identity.ConversationId, f.Identity.ContextId,
            admission.TurnId, request.SaveOperationId, admission.Fence, request.OutcomeDigest), f.Subject);
        Assert.Equal(DialogueStatus.Existing, lookup.Status);
        Assert.Equal(request.OutcomeDigest, lookup.Receipt!.OutcomeDigest);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeAsync_GuardIgnoresCancellation_DrainsLateCallbackWithoutSubmittingAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        var admission = await f.AdmitAsync();
        await using var reference = f.Factory.CreateDbContext();
        var factory = new AdmissionLifetimeFactory(new DbContextOptionsBuilder<OctoberDbContext>()
            .UseSqlite(reference.Database.GetConnectionString()).Options);
        var guard = new LifetimeGuard();
        var store = Store(f, factory, guard);
        var lifetime = Assert.IsAssignableFrom<IAsyncDisposable>((object)store);
        using var cancellation = new CancellationTokenSource();
        Task? disposal = null;
        var call = store.SaveTerminalAsync(f.TerminalRequest(admission), f.Subject, cancellation.Token);
        try
        {
            await guard.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.Equal(DialogueStatus.Indeterminate, (await call.WaitAsync(TimeSpan.FromSeconds(2))).Status);
            disposal = lifetime.DisposeAsync().AsTask();
            Assert.False(disposal.IsCompleted);
            guard.Release.TrySetResult();
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(guard.Completed.Task.IsCompleted);
            Assert.True(guard.CallbackAttempted);
            Assert.False(guard.SubmissionReturned);
            Assert.All(factory.CreatedContexts, context =>
                Assert.Throws<ObjectDisposedException>(() => context.ChangeTracker.Entries().ToArray()));
        }
        finally
        {
            guard.Release.TrySetResult();
            await call.WaitAsync(TimeSpan.FromSeconds(5));
            if (disposal is not null) await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeAsync_LateFactoryArrival_IsOwnedAcrossActiveCallRetirementAsync(bool disposeWhileActive)
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        await f.RequireOpenAsync();
        var factory = Factory(f);
        factory.DelayFactory = true;
        factory.GateEveryCleanup = true;
        var store = Store(f, factory);
        var lifetime = Assert.IsAssignableFrom<IAsyncDisposable>((object)store);
        using var cancellation = new CancellationTokenSource();
        Task? disposal = null;
        var call = store.ReadHistoryAsync(f.HistoryRequest, f.Subject, cancellation.Token);
        try
        {
            await factory.FactoryEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (disposeWhileActive) disposal = lifetime.DisposeAsync().AsTask();
            cancellation.Cancel();
            Assert.Equal(DialogueStatus.Indeterminate, (await call.WaitAsync(TimeSpan.FromSeconds(2))).Status);
            disposal ??= lifetime.DisposeAsync().AsTask();
            Assert.Same(disposal, lifetime.DisposeAsync().AsTask());
            Assert.False(disposal.IsCompleted);
            factory.ReleaseFactory.TrySetResult();
            var connection = Assert.Single(factory.Connections);
            await connection.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(disposal.IsCompleted);
            factory.ReleaseCleanup.TrySetResult();
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(connection.CleanupCompleted.Task.IsCompleted);
            Assert.Equal(1, connection.DisposeCalls);
            Assert.Throws<ObjectDisposedException>(() => connection.Context!.ChangeTracker.Entries().ToArray());
        }
        finally
        {
            factory.ReleaseAll();
            await call.WaitAsync(TimeSpan.FromSeconds(5));
            if (disposal is not null) await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeAsync_DeferredCleanupFault_IsSanitizedAndRepeatedDisposalObservesSameFailureAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        var factory = Factory(f);
        factory.DelayFactory = true;
        factory.GateEveryCleanup = true;
        factory.FailCleanup = true;
        var store = Store(f, factory);
        var lifetime = Assert.IsAssignableFrom<IAsyncDisposable>((object)store);
        using var cancellation = new CancellationTokenSource();
        Task? disposal = null;
        var call = store.ReadHistoryAsync(f.HistoryRequest, f.Subject, cancellation.Token);
        try
        {
            await factory.FactoryEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.Equal(DialogueStatus.Indeterminate, (await call.WaitAsync(TimeSpan.FromSeconds(2))).Status);
            disposal = lifetime.DisposeAsync().AsTask();
            factory.ReleaseFactory.TrySetResult();
            await Assert.Single(factory.Connections).CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(disposal.IsCompleted);
            factory.ReleaseCleanup.TrySetResult();
            var failure = await Record.ExceptionAsync(() => disposal.WaitAsync(TimeSpan.FromSeconds(5)));
            AssertSanitized(failure);
            var repeat = lifetime.DisposeAsync().AsTask();
            Assert.Same(disposal, repeat);
            Assert.Same(failure, await Record.ExceptionAsync(() => repeat));
            Assert.Equal(1, Assert.Single(factory.Connections).DisposeCalls);
        }
        finally
        {
            factory.ReleaseAll();
            await call.WaitAsync(TimeSpan.FromSeconds(5));
            if (disposal is not null) await Record.ExceptionAsync(() => disposal.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeAsync_InlineAwaitUsingCleanupFault_RemainsObservableAfterBoundedPublicResponseAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        await f.RequireOpenAsync();
        var factory = Factory(f);
        factory.FailCleanup = true;
        var store = Store(f, factory);
        var lifetime = Assert.IsAssignableFrom<IAsyncDisposable>((object)store);
        var response = await store.ReadHistoryAsync(f.HistoryRequest, f.Subject).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(DialogueStatus.Indeterminate, response.Status);
        Assert.Empty(response.Turns);
        var connection = Assert.Single(factory.Connections);
        Assert.True(connection.CleanupEntered.Task.IsCompleted);
        var disposal = lifetime.DisposeAsync().AsTask();
        var failure = await Record.ExceptionAsync(() => disposal.WaitAsync(TimeSpan.FromSeconds(5)));
        AssertSanitized(failure);
        Assert.Same(disposal, lifetime.DisposeAsync().AsTask());
        Assert.Equal(1, connection.DisposeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeAsync_CompletedStore_RejectsFurtherCallsBeforeCreatingContextsAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        var factory = Factory(f);
        var store = Store(f, factory);
        var lifetime = Assert.IsAssignableFrom<IAsyncDisposable>((object)store);
        await lifetime.DisposeAsync();
        var result = await store.ReadHistoryAsync(f.HistoryRequest, f.Subject);
        Assert.Equal(DialogueStatus.Indeterminate, result.Status);
        Assert.Empty(result.Turns);
        Assert.Null(result.Dependencies);
        Assert.Null(result.ProjectionDigest);
        Assert.Empty(factory.Connections);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeAsync_ControlledTransactionCleanupFault_StillDisposesContextsAndRetainsSanitizedFailureAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        var admission = await f.AdmitAsync();
        await using var reference = f.Factory.CreateDbContext();
        var options = new DbContextOptionsBuilder<OctoberDbContext>().UseSqlite(reference.Database.GetConnectionString())
            .ReplaceService<IRelationalTransactionFactory, CleanupFaultTransactionFactory>().Options;
        var factory = new AdmissionLifetimeFactory(options);
        var store = Store(f, factory);
        var lifetime = Assert.IsAssignableFrom<IAsyncDisposable>((object)store);
        var response = await store.SaveTerminalAsync(f.TerminalRequest(admission), f.Subject).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(DialogueStatus.Indeterminate, response.Status);
        Assert.Null(response.Projection);
        var disposal = lifetime.DisposeAsync().AsTask();
        var failure = await Record.ExceptionAsync(() => disposal.WaitAsync(TimeSpan.FromSeconds(5)));
        AssertSanitized(failure);
        Assert.Same(disposal, lifetime.DisposeAsync().AsTask());
        Assert.Same(failure, await Record.ExceptionAsync(() => lifetime.DisposeAsync().AsTask()));
        Assert.NotEmpty(factory.CreatedContexts);
        Assert.All(factory.CreatedContexts, context =>
            Assert.Throws<ObjectDisposedException>(() => context.ChangeTracker.Entries().ToArray()));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeAsync_AuthorityIgnoresCancellation_DrainsActualAuthorityTaskBeforeCompletingAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        await f.RequireOpenAsync();
        var factory = Factory(f);
        var authority = new HeldDisclosure();
        var store = Store(f, factory, disclosure: authority);
        var lifetime = Assert.IsAssignableFrom<IAsyncDisposable>((object)store);
        using var cancellation = new CancellationTokenSource();
        Task? disposal = null;
        var call = store.ReadHistoryAsync(f.HistoryRequest, f.Subject, cancellation.Token);
        try
        {
            await authority.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            var result = await call.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(DialogueStatus.Indeterminate, result.Status);
            Assert.Empty(result.Turns);
            Assert.Null(result.Dependencies);
            disposal = lifetime.DisposeAsync().AsTask();
            Assert.False(disposal.IsCompleted);
            Assert.False(authority.Completed.Task.IsCompleted);
            authority.Release.TrySetResult();
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(authority.Completed.Task.IsCompleted);
            Assert.All(factory.Connections, connection => Assert.True(connection.CleanupCompleted.Task.IsCompleted));
        }
        finally
        {
            authority.Release.TrySetResult(); factory.ReleaseAll();
            await call.WaitAsync(TimeSpan.FromSeconds(5));
            if (disposal is not null) await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static void AssertSanitized(Exception? failure)
    {
        Assert.NotNull(failure);
        Assert.IsNotType<TimeoutException>(failure);
        Assert.DoesNotContain(LifetimeFactory.PrivateFailure, failure.ToString(), StringComparison.Ordinal);
    }
    private static LifetimeFactory Factory(DialoguePersistenceFixture f)
    {
        using var reference = f.Factory.CreateDbContext();
        return new(reference.Database.GetConnectionString()!);
    }
    private static OctoberDialogueStore Store(DialoguePersistenceFixture f, IDbContextFactory<OctoberDbContext> factory,
        IOctoberDialogueCommitGuard? guard = null, IOctoberKnowledgeDisclosurePolicy? disclosure = null) =>
        new(factory, f.Identity.Publication.Knowledge, disclosure ?? f.Disclosure, f.Dependencies, f.Execution,
            guard ?? f.Guard, new OctoberDialoguePayloadCodec(), f.Clock);

    private sealed class HeldDisclosure : IOctoberKnowledgeDisclosurePolicy
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<DisclosureStatus> CheckAsync(DisclosureRequest request, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            try { await Release.Task; return DisclosureStatus.Allowed; }
            finally { Completed.TrySetResult(); }
        }
    }

    /// <summary>Controlled exception after actual base transaction cleanup, not a native provider cleanup-failure claim.</summary>
    private sealed class CleanupFaultTransactionFactory(RelationalTransactionFactoryDependencies dependencies) : RelationalTransactionFactory(dependencies)
    {
        public override RelationalTransaction Create(IRelationalConnection connection, DbTransaction transaction, Guid transactionId,
            IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger, bool transactionOwned) =>
            new CleanupFaultTransaction(connection, transaction, transactionId, logger, transactionOwned, Dependencies.SqlGenerationHelper);
    }
    private sealed class CleanupFaultTransaction(IRelationalConnection connection, DbTransaction transaction, Guid transactionId,
        IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger, bool transactionOwned, ISqlGenerationHelper helper)
        : RelationalTransaction(connection, transaction, transactionId, logger, transactionOwned, helper)
    {
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            throw new IOException(LifetimeFactory.PrivateFailure);
        }
    }

    /// <summary>Each context explicitly owns its actual SQLite connection, including observable async cleanup.</summary>
    private sealed class LifetimeFactory(string connectionString) : IDbContextFactory<OctoberDbContext>
    {
        public const string PrivateFailure = "private-cleanup-credential-and-path-sentinel";
        public bool DelayFactory { get; set; }
        public bool GateEveryCleanup { get; set; }
        public bool FailCleanup { get; set; }
        public List<LifetimeConnection> Connections { get; } = [];
        public TaskCompletionSource FactoryEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFactory { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseCleanup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool GateCleanup(OctoberDbContext context) => GateEveryCleanup;
        public OctoberDbContext CreateDbContext()
        {
            var connection = new LifetimeConnection(new SqliteConnection(connectionString), this);
            var builder = new DbContextOptionsBuilder<OctoberDbContext>().UseSqlite(connection, contextOwnsConnection: true);
            var context = new OctoberDbContext(builder.Options);
            connection.Context = context;
            // A late factory must actually initialize the provider-owned connection whose cleanup we gate.
            // Disposing a never-used context need not create EF's lazy relational services.
            Assert.Same(connection, context.Database.GetDbConnection());
            Connections.Add(connection);
            return context;
        }
        public async Task<OctoberDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            var context = CreateDbContext();
            FactoryEntered.TrySetResult();
            if (DelayFactory) await ReleaseFactory.Task;
            return context;
        }
        public void ReleaseAll() { ReleaseFactory.TrySetResult(); ReleaseCleanup.TrySetResult(); }
    }

    private sealed class LifetimeConnection(SqliteConnection inner, LifetimeFactory owner) : DbConnection
    {
        public OctoberDbContext? Context { get; set; }
        public int DisposeCalls { get; private set; }
        public TaskCompletionSource CleanupEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CleanupCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override string ConnectionString { get => inner.ConnectionString; set => inner.ConnectionString = value; }
        public override string Database => inner.Database;
        public override string DataSource => inner.DataSource;
        public override string ServerVersion => inner.ServerVersion;
        public override ConnectionState State => inner.State;
        public override int ConnectionTimeout => inner.ConnectionTimeout;
        public override void ChangeDatabase(string databaseName) => inner.ChangeDatabase(databaseName);
        public override void Open() => inner.Open();
        public override Task OpenAsync(CancellationToken cancellationToken) => inner.OpenAsync(cancellationToken);
        public override void Close() => inner.Close();
        public override Task CloseAsync() => inner.CloseAsync();
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => inner.BeginTransaction(isolationLevel);
        protected override DbCommand CreateDbCommand() => inner.CreateCommand();
        public override async ValueTask DisposeAsync()
        {
            DisposeCalls++;
            await inner.DisposeAsync();
            CleanupEntered.TrySetResult();
            if (owner.GateCleanup(Context!)) await owner.ReleaseCleanup.Task;
            CleanupCompleted.TrySetResult();
            if (owner.FailCleanup) throw new IOException(LifetimeFactory.PrivateFailure);
            GC.SuppressFinalize(this);
        }
        protected override void Dispose(bool disposing)
        { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }

    private sealed class LifetimeGuard : IOctoberDialogueCommitGuard
    {
        public bool SubmitBeforePause { get; init; }
        public bool CallbackAttempted { get; private set; }
        public bool SubmissionReturned { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SettlementObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(DialogueCommitAttempt attempt, CancellationToken cancellationToken = default) => Task.FromResult(CommitEligibilityStatus.Eligible);
        public async Task<CommitGuardResult> SubmitAsync(DialogueCommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            try
            {
                if (!SubmitBeforePause) await Release.Task;
                CallbackAttempted = true;
                var submitted = submitAsync(CancellationToken.None);
                SubmissionReturned = true;
                var settlement = await submitted.Settlement;
                SettlementObserved.TrySetResult();
                if (SubmitBeforePause) await Release.Task;
                return new(CommitSubmissionStatus.Submitted, settlement);
            }
            catch (OperationCanceledException) { return new(CommitSubmissionStatus.CancelledBeforeSubmission, null); }
            finally { Completed.TrySetResult(); }
        }
    }
}
