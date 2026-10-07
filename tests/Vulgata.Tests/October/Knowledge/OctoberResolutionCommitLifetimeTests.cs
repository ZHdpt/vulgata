using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Real B1 transactions over owned SQLite with a controlled physical commit that ignores cancellation until released.</summary>
public sealed class OctoberResolutionCommitLifetimeTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false, "await-settlement")]
    [InlineData(false, "throw-after-submit")]
    [InlineData(false, "early-indeterminate")]
    [InlineData(true, "await-settlement")]
    [InlineData(true, "throw-after-submit")]
    [InlineData(true, "early-indeterminate")]
    public async Task SubmittedPhysicalCommit_CancellationOrEarlyGuardFailure_ReturnsBoundedIndeterminateWithoutDisposingTransactionAsync(bool publication, string reply)
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        await using var reference = fixture.Acceptance.Factory.CreateDbContext();
        var probe = new AdmissionPhysicalCommitProbe();
        var options = new DbContextOptionsBuilder<OctoberDbContext>().UseSqlite(reference.Database.GetConnectionString())
            .AddInterceptors(probe, new AdmissionCommandAdapter()).Options;
        var factory = new AdmissionLifetimeFactory(options);
        var guard = new ResolutionCommitReplyGuard(reply);
        var calls = await CallsAsync(fixture, factory, guard, publication);
        using var cancellation = new CancellationTokenSource();
        var pending = calls.Invoke(cancellation.Token);
        OctoberDbContext? producingContext = null;
        DbConnection? producingConnection = null;
        try
        {
            await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            producingContext = Assert.IsType<OctoberDbContext>(probe.ProducingContext);
            producingConnection = producingContext.Database.GetDbConnection();
            Assert.False(probe.Settled.Task.IsCompleted);
            Assert.False(probe.Disposed.Task.IsCompleted);
            cancellation.Cancel();
            Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2))));
            var result = await pending;
            Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, result.Status);
            Assert.False(result.Disclosed);
            Assert.False(probe.Settled.Task.IsCompleted);
            Assert.False(probe.Disposed.Task.IsCompleted);
            Assert.Null(Record.Exception(() => producingContext.ChangeTracker.Entries().ToArray()));
            var absent = await calls.Lookup();
            Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, absent.Status);
            Assert.False(absent.Disclosed);
            Assert.Equal(1, guard.Submissions);
        }
        finally
        {
            // Release/observe the real provider task even when the regression assertion fails.
            probe.Release.TrySetResult();
            await probe.Settled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            await probe.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (producingContext is not null)
                await RequireContextCleanupAsync(producingContext, producingConnection);
        }
        var retained = await calls.Lookup();
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, retained.Status);
        Assert.True(retained.Disclosed);
        var replay = await calls.Invoke(CancellationToken.None);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.True(replay.Disclosed);
        Assert.Equal(1, guard.Submissions);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(publication ? 2 : 1, await context.KnowledgePublications.CountAsync());
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync(x => x.Kind == OperationKind.AcceptResolution));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedGuardCallback_OriginalCallCancelled_FreshCallbackTokenCannotSubmitB1MutationAsync(bool publication)
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var guard = new AdmissionLateCallbackGuard();
        var calls = await CallsAsync(fixture, fixture.Acceptance.Factory, guard, publication);
        using var cancellation = new CancellationTokenSource();
        var pending = calls.Invoke(cancellation.Token);
        try
        {
            await guard.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2))));
            var result = await pending;
            Assert.DoesNotContain(result.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted });
            Assert.False(result.Disclosed);
        }
        finally
        {
            guard.Release.TrySetResult();
            await guard.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.True(guard.CallbackInvoked);
        Assert.False(guard.SubmissionReturned);
        var lookup = await calls.Lookup();
        Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, lookup.Status);
        Assert.False(lookup.Disclosed);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
        Assert.Equal(publication ? 1 : 0, await context.KnowledgeOperations.CountAsync(x => x.Kind == OperationKind.AcceptResolution));
    }

    private static async Task<(Func<CancellationToken, Task<CommitObservation>> Invoke, Func<Task<CommitObservation>> Lookup)> CallsAsync(
        ResolutionPersistenceFixture fixture, IDbContextFactory<OctoberDbContext> factory, IOctoberCommitSubmissionGuard guard, bool publication)
    {
        var acceptance = fixture.Request();
        if (!publication)
        {
            var store = new OctoberResolutionStore(factory, fixture.Authorization, guard, fixture.Acceptance.Log);
            return (async token =>
            {
                var result = await store.AcceptOutcomeAsync(acceptance, token);
                return new CommitObservation(result.Status, result.Outcome is not null);
            }, async () =>
            {
                var result = await fixture.Store().LookupOutcomeAsync(acceptance.Corpus, acceptance.Operation, acceptance.CanonicalPayloadDigest);
                return new CommitObservation(result.Status, result.Outcome is not null);
            });
        }
        var accepted = await fixture.Store().AcceptOutcomeAsync(acceptance);
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var request = fixture.Publication([accepted.Outcome!]);
        var writer = new OctoberKnowledgeWriter(factory, fixture.Acceptance.Completion, fixture.Acceptance.Source,
            fixture.Authorization, guard, fixture.Acceptance.Log, fixture.Acceptance.FreezeAuthority);
        return (async token =>
        {
            var result = await writer.PublishSelectionAsync(request, token);
            return new CommitObservation(result.Status, result.Publication is not null);
        }, async () =>
        {
            var result = await fixture.Acceptance.Writer().LookupOperationAsync(request.Corpus.Binding, request.Operation, request.CanonicalPayloadDigest);
            return new CommitObservation(result.Status, result.Publication is not null);
        });
    }

    private sealed record CommitObservation(KnowledgeWriteStatus Status, bool Disclosed);

    private static async Task RequireContextCleanupAsync(OctoberDbContext context, DbConnection? connection)
    {
        // The provider probe reports transaction disposal before the committer awaits context
        // disposal. Wait for both the EF owner and physical connection before deleting its file.
        for (int attempt = 0; attempt < 100; attempt++)
        {
            bool contextDisposed = Record.Exception(() => context.ChangeTracker.Entries().ToArray()) is ObjectDisposedException;
            if (contextDisposed && (connection is null || connection.State == ConnectionState.Closed)) return;
            await Task.Delay(20);
        }
        Assert.Fail("The producing context or its owned connection remained live after physical commit settlement.");
    }
}

/// <summary>Controls guard acknowledgement only; the reused provider probe independently holds actual SQLite commit settlement.</summary>
internal sealed class ResolutionCommitReplyGuard(string reply) : IOctoberCommitSubmissionGuard
{
    public int Submissions { get; private set; }
    public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt, CancellationToken cancellationToken = default) =>
        Task.FromResult(CommitEligibilityStatus.Eligible);
    public async Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync,
        CancellationToken cancellationToken = default)
    {
        var submitted = submitAsync(cancellationToken);
        Submissions++;
        if (reply == "throw-after-submit") throw new IOException("Controlled guard acknowledgement failure after physical submission.");
        if (reply == "early-indeterminate") return new(CommitSubmissionStatus.Submitted, CommitSettlement.Indeterminate);
        return new(CommitSubmissionStatus.Submitted, await submitted.Settlement);
    }
}
