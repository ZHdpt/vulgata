using System.Collections.Immutable;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

internal sealed record PreparedPublicationMutation(KnowledgeWriteResult Result, ImmutableArray<Contributor> Contributors);

/// <summary>Shared atomic commit mechanics for fixed selections and resolution acceptance; no scheduler or durable receipt.</summary>
internal sealed class OctoberKnowledgePublicationCommitter
{
    private readonly IDbContextFactory<OctoberDbContext> _contexts;
    private readonly IOctoberKnowledgeProducerAuthorization _authorization;
    private readonly IOctoberCommitSubmissionGuard _guard;
    private readonly IOctoberKnowledgeOperationLog _log;

    public OctoberKnowledgePublicationCommitter(IDbContextFactory<OctoberDbContext> contexts,
        IOctoberKnowledgeProducerAuthorization authorization, IOctoberCommitSubmissionGuard guard, IOctoberKnowledgeOperationLog log)
    {
        _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<KnowledgeWriteResult> ExecuteAsync(KnowledgeBinding binding, OperationKey operation, OperationKind kind,
        string digest, Func<string> computeDigest, Func<CancellationToken, Task<KnowledgeWriteStatus?>> beforeWrite,
        Func<OctoberDbContext, CancellationToken, Task<PreparedPublicationMutation>> prepare, CancellationToken cancellationToken, Guid? attemptId = null)
    {
        if (operation.BatchId != binding.BatchId || operation.Kind != kind || operation.OperationId == Guid.Empty)
            return new(KnowledgeWriteStatus.InvalidProposal, operation);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var ct = deadline.Token;
        var submissionGate = new object();
        bool callbackOpen = false;
        bool submitted = false;
        Task<CommitSettlement>? actualSettlement = null;
        bool submissionObserved = false;
        Task<CommitSettlement>? observedSettlement = null;
        void CloseSubmission()
        {
            lock (submissionGate)
            {
                callbackOpen = false;
                submissionObserved = submitted;
                observedSettlement = actualSettlement;
            }
        }
        OctoberDbContext? context = null;
        IDbContextTransaction? transaction = null;
        try
        {
            try
            {
                await using (var check = await OpenContextAsync(ct))
                    if (!await BindingMatchesAsync(check, binding, ct)) return new(KnowledgeWriteStatus.Denied, operation);
                var prior = await FindAsync(operation, ct);
                if (prior is not null) return await DiscloseAsync(binding, operation, digest, computeDigest, prior, false, ct);
                if (digest.Length != 64 || computeDigest() != digest) return new(KnowledgeWriteStatus.InvalidProposal, operation);
                var eligibility = await beforeWrite(ct).WaitAsync(ct);
                ct.ThrowIfCancellationRequested();
                if (eligibility is { } rejected) return new(rejected, operation);
                var attempt = new CommitAttempt(operation, attemptId ?? operation.OperationId, digest);
                context = await OpenContextAsync(ct);
                var entry = await _guard.CheckBeforeTransactionAsync(attempt, ct).WaitAsync(ct);
                ct.ThrowIfCancellationRequested();
                if (entry != CommitEligibilityStatus.Eligible) return new(EntryStatus(entry), operation);
                transaction = await context.Database.BeginTransactionAsync(ct);
                context.Database.AutoSavepointsEnabled = false;
                if (context.Database.IsNpgsql())
                {
                    if (kind is OperationKind.FreezeCorpus or OperationKind.BeginBusinessFromCl or OperationKind.CompleteBusinessFromCl or OperationKind.AbandonBusinessFromCl)
                        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM october.\"OctoberBatches\" WHERE \"BatchId\" = {binding.BatchId} FOR UPDATE", ct);
                    else
                        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM october.\"OctoberBatches\" WHERE \"BatchId\" = {binding.BatchId} FOR SHARE", ct);
                }
                if (!await BindingMatchesAsync(context, binding, ct)) return new(KnowledgeWriteStatus.Denied, operation);
                prior = await FindAsync(operation, ct);
                if (prior is not null)
                {
                    await transaction.RollbackAsync(ct);
                    return await DiscloseAsync(binding, operation, digest, computeDigest, prior, false, ct);
                }
                eligibility = await beforeWrite(ct).WaitAsync(ct);
                ct.ThrowIfCancellationRequested();
                if (eligibility is { } ineligible) return new(ineligible, operation);
                var mutation = await prepare(context, ct);
                ct.ThrowIfCancellationRequested();
                if (mutation.Result.Status != KnowledgeWriteStatus.Committed) return mutation.Result;
                var authority = await AuthorizeAsync(binding, mutation.Contributors, ct);
                if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
                context.KnowledgeOperations.Add(new()
                {
                    BatchId = binding.BatchId, Kind = operation.Kind, OperationId = operation.OperationId,
                    PayloadDigest = digest, CommittedResultJson = OctoberKnowledgePolicy.SerializeCanonical(mutation.Result),
                    ContributorsJson = OctoberKnowledgePolicy.SerializeCanonical(mutation.Contributors), CommittedAtUtc = DateTimeOffset.UtcNow
                });
                ct.ThrowIfCancellationRequested();
                await context.SaveChangesAsync(ct);
                authority = await AuthorizeAsync(binding, mutation.Contributors, ct);
                if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
                ct.ThrowIfCancellationRequested();
                lock (submissionGate) callbackOpen = true;
                CommitGuardResult guarded;
                try
                {
                    var guardCall = _guard.SubmitAsync(attempt, callbackToken =>
                    {
                        // The small local lock owns callback closure, not the Scan fence or a second scheduler.
                        lock (submissionGate)
                        {
                            if (!callbackOpen || submitted) throw new OperationCanceledException(ct);
                            ct.ThrowIfCancellationRequested();
                            cancellationToken.ThrowIfCancellationRequested();
                            callbackToken.ThrowIfCancellationRequested();
                            callbackOpen = false;
                            submitted = true;
                            try
                            {
                                // No await, queued work or interceptor prelude before the actual provider API.
                                Task commit = transaction.GetDbTransaction().CommitAsync(callbackToken);
                                actualSettlement = SettleAsync(commit);
                            }
                            catch
                            {
                                actualSettlement = Task.FromResult(CommitSettlement.Indeterminate);
                                throw;
                            }
                            return new SubmittedCommit(actualSettlement);
                        }
                    }, ct);
                    _ = ObserveAsync(guardCall);
                    guarded = await guardCall.WaitAsync(ct);
                }
                finally
                {
                    CloseSubmission();
                }
                if (!submissionObserved) return new(SubmissionStatus(guarded.Status), operation);
            }
            catch (Exception error)
            {
                // Exception filters run before inner finally blocks. Arbitrate first, then classify.
                CloseSubmission();
                if (!submissionObserved)
                {
                    if (error is OperationCanceledException) return new(KnowledgeWriteStatus.CancelledBeforeCommit, operation);
                    if (error is DbUpdateException update && IsCollision(update)) return await ReconcileAsync(binding, operation, digest, false);
                    if (error is ArgumentException or JsonException) return new(KnowledgeWriteStatus.InvalidProposal, operation);
                    return new(KnowledgeWriteStatus.StorageUnavailable, operation);
                }
            }
            if (observedSettlement is null) return new(KnowledgeWriteStatus.CommitIndeterminate, operation);
            if (!observedSettlement.IsCompleted)
            {
                try { await observedSettlement.WaitAsync(ct); }
                catch (OperationCanceledException) { return new(KnowledgeWriteStatus.CommitIndeterminate, operation); }
            }
            return await ReconcileAsync(binding, operation, digest, observedSettlement.Result == CommitSettlement.Committed);
        }
        finally
        {
            CloseSubmission();
            if (observedSettlement is { IsCompleted: false })
            {
                // Retain the producing context and physical transaction until the submitted task settles.
                _ = DisposeAfterSettlementAsync(observedSettlement, transaction, context);
            }
            else await DisposeOwnedAsync(transaction, context);
        }
    }

    public async Task<DisclosureStatus> AuthorizeAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _authorization.CheckAsync(binding, contributors, cancellationToken).WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return DisclosureStatus.AuthorityUnavailable;
        }
    }

    private async Task<KnowledgeWriteResult> ReconcileAsync(KnowledgeBinding binding, OperationKey operation, string digest, bool knownCommit)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await using var context = await OpenContextAsync(deadline.Token);
            if (!await BindingMatchesAsync(context, binding, deadline.Token)) return new(KnowledgeWriteStatus.Denied, operation);
            for (int retry = 0; retry < 5; retry++)
            {
                var prior = await FindAsync(operation, deadline.Token);
                if (prior is not null) return await DiscloseAsync(binding, operation, digest, () => digest, prior, knownCommit, deadline.Token);
                await Task.Delay(20, deadline.Token);
            }
            return new(KnowledgeWriteStatus.CommitIndeterminate, operation);
        }
        catch (Exception)
        {
            return new(KnowledgeWriteStatus.CommitIndeterminate, operation);
        }
    }

    private async Task<KnowledgeWriteResult> DiscloseAsync(KnowledgeBinding binding, OperationKey operation, string digest,
        Func<string> computeDigest, KnowledgeOperation prior, bool knownCommit, CancellationToken cancellationToken)
    {
        var contributors = OctoberKnowledgeOperationStore.Contributors(prior);
        var authority = await AuthorizeAsync(binding, contributors, cancellationToken);
        if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
        if (prior.PayloadDigest != digest || computeDigest() != digest) return new(KnowledgeWriteStatus.Conflict, operation);
        var result = OctoberKnowledgeOperationStore.Result(prior) with
        {
            Status = knownCommit ? KnowledgeWriteStatus.Committed : KnowledgeWriteStatus.AlreadyCommitted
        };
        bool logged;
        try
        {
            logged = await _log.AppendAsync(new(binding, operation, result.Status), cancellationToken).WaitAsync(cancellationToken);
        }
        catch (Exception)
        {
            logged = false;
        }
        authority = await AuthorizeAsync(binding, contributors, cancellationToken);
        if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
        return logged ? result : result with { Status = KnowledgeWriteStatus.LogUnavailable };
    }

    private static async Task<bool> BindingMatchesAsync(OctoberDbContext context, KnowledgeBinding binding, CancellationToken cancellationToken) =>
        await context.Batches.AsNoTracking().AnyAsync(x => x.BatchId == binding.BatchId && x.TargetId == binding.TargetId &&
            x.CanonicalDatabaseIdentity == binding.CanonicalDatabaseIdentity && x.SelectionDigest == binding.SelectionDigest, cancellationToken);

    private async Task<KnowledgeOperation?> FindAsync(OperationKey operation, CancellationToken ct)
    {
        await using var context = await OpenContextAsync(ct);
        return await context.KnowledgeOperations.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == operation.BatchId &&
            x.Kind == operation.Kind && x.OperationId == operation.OperationId, ct);
    }

    private async Task<OctoberDbContext> OpenContextAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Task<OctoberDbContext> pending = _contexts.CreateDbContextAsync(ct);
        OctoberDbContext? context = null;
        try
        {
            context = await pending.WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            return context;
        }
        catch
        {
            if (context is not null) await DisposeOwnedAsync(null, context);
            else _ = DisposeLateContextAsync(pending);
            throw;
        }
    }

    private static async Task DisposeLateContextAsync(Task<OctoberDbContext> pending)
    {
        try { await DisposeOwnedAsync(null, await pending); }
        catch (Exception) { /* Failed creation owns no returned resource. */ }
    }

    private static async Task ObserveAsync(Task pending)
    {
        try { await pending; }
        catch (Exception) { /* The original bounded caller already handled the guard result. */ }
    }

    private static async Task DisposeAfterSettlementAsync(Task<CommitSettlement> settlement,
        IDbContextTransaction? transaction, OctoberDbContext? context)
    {
        try { await settlement; }
        finally { await DisposeOwnedAsync(transaction, context); }
    }

    private static async Task DisposeOwnedAsync(IDbContextTransaction? transaction, OctoberDbContext? context)
    {
        try { if (transaction is not null) await transaction.DisposeAsync(); }
        catch (Exception) { /* Cleanup cannot rewrite a durable or indeterminate outcome. */ }
        try { if (context is not null) await context.DisposeAsync(); }
        catch (Exception) { /* Observe cleanup failure without an unhandled detached task. */ }
    }

    private static async Task<CommitSettlement> SettleAsync(Task commit)
    {
        try
        {
            await commit; return CommitSettlement.Committed;
        }
        catch (Exception)
        {
            return CommitSettlement.Indeterminate;
        }
    }

    private static bool IsCollision(DbUpdateException exception) => exception.InnerException switch
    {
        Npgsql.PostgresException { SqlState: "23505" } => true,
        DbException error when error.GetType().Name == "SqliteException" && error.GetType().GetProperty("SqliteErrorCode")?.GetValue(error) is int code && code is 5 or 6 => true,
        DbException error when error.GetType().Name == "SqliteException" && error.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(error) is 1555 or 2067 => true,
        _ => false
    };
    internal static KnowledgeWriteStatus AuthorityStatus(DisclosureStatus authority) => authority == DisclosureStatus.Denied ? KnowledgeWriteStatus.Denied : KnowledgeWriteStatus.AuthorityUnavailable;
    private static KnowledgeWriteStatus EntryStatus(CommitEligibilityStatus status) => status switch
    {
        CommitEligibilityStatus.Denied => KnowledgeWriteStatus.Denied,
        CommitEligibilityStatus.AuthorityUnavailable => KnowledgeWriteStatus.AuthorityUnavailable,
        CommitEligibilityStatus.Cancelled => KnowledgeWriteStatus.CancelledBeforeCommit,
        _ => KnowledgeWriteStatus.StaleOrFenced
    };
    private static KnowledgeWriteStatus SubmissionStatus(CommitSubmissionStatus status) => status switch
    {
        CommitSubmissionStatus.Denied => KnowledgeWriteStatus.Denied,
        CommitSubmissionStatus.AuthorityUnavailable => KnowledgeWriteStatus.AuthorityUnavailable,
        CommitSubmissionStatus.CancelledBeforeSubmission => KnowledgeWriteStatus.CancelledBeforeCommit,
        _ => KnowledgeWriteStatus.StaleOrFenced
    };
}
