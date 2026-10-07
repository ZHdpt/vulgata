using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>One immutable batch admission. A stored receipt never grants dispatch or restart authority.</summary>
public sealed class OctoberGenerateAdmissionStore : IOctoberGenerateAdmissionStore
{
    private readonly IDbContextFactory<OctoberDbContext> _contexts;
    private readonly IOctoberGenerateAdmissionAuthority _authority;
    private readonly IOctoberCommitSubmissionGuard _guard;
    private readonly IOctoberKnowledgeOperationLog _log;

    public OctoberGenerateAdmissionStore(IDbContextFactory<OctoberDbContext> contexts,
        IOctoberGenerateAdmissionAuthority authority, IOctoberCommitSubmissionGuard guard,
        IOctoberKnowledgeOperationLog log)
    {
        _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<KnowledgeWriteResult> StartFreshExecutionAsync(GenerateAdmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var operation = OctoberGenerateAdmissionPolicy.OperationFor(request.Binding);
        if (!Valid(request)) return new(KnowledgeWriteStatus.InvalidProposal, operation);
        bool submitted = false;
        Task<CommitSettlement>? settlement = null;
        OctoberDbContext? ownedContext = null;
        IDbContextTransaction? ownedTransaction = null;
        object submissionGate = new();
        bool submissionClosed = false;
        bool submissionObserved = false;
        Task<CommitSettlement>? observedSettlement = null;
        void CloseSubmission()
        {
            lock (submissionGate)
            {
                submissionClosed = true;
                submissionObserved = submitted;
                observedSettlement = settlement;
            }
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var token = deadline.Token;
        try
        {
            var authority = await AuthorizeAsync(request, token);
            if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
            await using (var initial = await OpenAsync(token))
            {
                if (!await BindingMatchesAsync(initial, request.Binding, token)) return new(KnowledgeWriteStatus.Denied, operation);
                var prior = await FindAsync(initial, operation, token);
                if (prior is not null) return await DiscloseAsync(request, prior, false, token);
            }
            var attempt = new CommitAttempt(operation, request.RunId, request.CanonicalPayloadDigest);
            var entry = await _guard.CheckBeforeTransactionAsync(attempt, token).WaitAsync(token);
            if (entry != CommitEligibilityStatus.Eligible) return new(EntryStatus(entry), operation);
            ownedContext = await OpenAsync(token);
            var context = ownedContext;
            {
                ownedTransaction = await context.Database.BeginTransactionAsync(token);
                var transaction = ownedTransaction;
                context.Database.AutoSavepointsEnabled = false;
                if (context.Database.IsNpgsql())
                    await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM october.\"OctoberBatches\" WHERE \"BatchId\" = {operation.BatchId} FOR UPDATE", token);
                if (!await BindingMatchesAsync(context, request.Binding, token)) return new(KnowledgeWriteStatus.Denied, operation);
                authority = await AuthorizeAsync(request, token);
                if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
                var prior = await FindAsync(context, operation, token);
                if (prior is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return await DiscloseAsync(request, prior, false, token);
                }
                if (await context.KnowledgeOperations.AnyAsync(x => x.BatchId == operation.BatchId, token) ||
                    await context.AcceptedFileManifests.AnyAsync(x => x.BatchId == operation.BatchId, token) ||
                    await context.CorpusSelections.AnyAsync(x => x.BatchId == operation.BatchId, token))
                    return new(KnowledgeWriteStatus.Conflict, operation);
                var result = new KnowledgeWriteResult(KnowledgeWriteStatus.Committed, operation);
                context.KnowledgeOperations.Add(new()
                {
                    BatchId = operation.BatchId, Kind = operation.Kind, OperationId = operation.OperationId,
                    PayloadDigest = request.CanonicalPayloadDigest, CommittedResultJson = OctoberKnowledgePolicy.SerializeCanonical(result),
                    ContributorsJson = "[]", CommittedAtUtc = DateTimeOffset.UtcNow
                });
                await context.SaveChangesAsync(token);
                authority = await AuthorizeAsync(request, token);
                if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
                CommitGuardResult guarded;
                try
                {
                    Task<CommitGuardResult> guardCall = _guard.SubmitAsync(attempt, commitToken =>
                    {
                        lock (submissionGate)
                        {
                            token.ThrowIfCancellationRequested();
                            commitToken.ThrowIfCancellationRequested();
                            if (submissionClosed) throw new OperationCanceledException("Execution admission submission is closed.");
                            submitted = true;
                            // Physical provider submission occurs inside the guard, before its callback returns.
                            Task commit = transaction.GetDbTransaction().CommitAsync(commitToken);
                            settlement = SettleAsync(commit);
                            return new SubmittedCommit(settlement);
                        }
                    }, token);
                    _ = ObserveAsync(guardCall);
                    guarded = await guardCall.WaitAsync(token);
                }
                finally
                {
                    // Arbitrate with a callback already inside the gate before choosing any exit.
                    CloseSubmission();
                }
                if (!submissionObserved) return new(SubmissionStatus(guarded.Status), operation);
                if (observedSettlement is not null) await observedSettlement.WaitAsync(token);
            }
        }
        catch (Exception error)
        {
            // Do not classify in exception filters: filters run before inner finally blocks.
            CloseSubmission();
            if (!submissionObserved)
            {
                if (error is OperationCanceledException)
                    return new(cancellationToken.IsCancellationRequested ? KnowledgeWriteStatus.CancelledBeforeCommit : KnowledgeWriteStatus.StorageUnavailable, operation);
                if (error is DbUpdateException)
                    return await LookupBoundedAsync(request, false, CancellationToken.None);
                return new(KnowledgeWriteStatus.StorageUnavailable, operation);
            }
            // A submitted commit is reconciled, never retried as new execution.
        }
        finally
        {
            CloseSubmission();
            if (observedSettlement is { IsCompleted: false })
                _ = DisposeAfterSettlementAsync(observedSettlement, ownedTransaction, ownedContext);
            else
                await DisposeOwnedAsync(ownedTransaction, ownedContext);
        }
        if (observedSettlement is { IsCompleted: false }) return new(KnowledgeWriteStatus.CommitIndeterminate, operation);
        return await LookupBoundedAsync(request, observedSettlement?.Result == CommitSettlement.Committed, CancellationToken.None);
    }

    public Task<KnowledgeWriteResult> LookupAsync(GenerateAdmissionRequest request,
        CancellationToken cancellationToken = default) =>
        LookupBoundedAsync(request, false, cancellationToken);

    private async Task<KnowledgeWriteResult> LookupBoundedAsync(GenerateAdmissionRequest request, bool knownCommit, CancellationToken cancellationToken)
    {
        var operation = OctoberGenerateAdmissionPolicy.OperationFor(request.Binding);
        if (!Valid(request)) return new(KnowledgeWriteStatus.InvalidProposal, operation);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var authority = await AuthorizeAsync(request, deadline.Token);
            if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
            await using var context = await OpenAsync(deadline.Token);
            if (!await BindingMatchesAsync(context, request.Binding, deadline.Token)) return new(KnowledgeWriteStatus.Denied, operation);
            var prior = await FindAsync(context, operation, deadline.Token);
            if (prior is not null) return await DiscloseAsync(request, prior, knownCommit, deadline.Token);
            // Even a successful read/lock cannot exclude another request waiting before transaction entry.
            authority = await AuthorizeAsync(request, deadline.Token);
            return new(authority == DisclosureStatus.Allowed ? KnowledgeWriteStatus.CommitIndeterminate : AuthorityStatus(authority), operation);
        }
        catch (Exception) { return new(KnowledgeWriteStatus.CommitIndeterminate, operation); }
    }

    private async Task<KnowledgeWriteResult> DiscloseAsync(GenerateAdmissionRequest request, KnowledgeOperation prior,
        bool knownCommit, CancellationToken token)
    {
        var operation = OctoberGenerateAdmissionPolicy.OperationFor(request.Binding);
        var authority = await AuthorizeAsync(request, token);
        if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
        if (prior.PayloadDigest != request.CanonicalPayloadDigest) return new(KnowledgeWriteStatus.Conflict, operation);
        var stored = OctoberKnowledgeOperationStore.Result(prior);
        if (stored.Operation != operation || stored.AcceptedFile is not null || stored.Corpus is not null || stored.Publication is not null ||
            !OctoberKnowledgeOperationStore.Contributors(prior).IsEmpty)
            return new(KnowledgeWriteStatus.CommitIndeterminate, operation);
        var result = new KnowledgeWriteResult(knownCommit ? KnowledgeWriteStatus.Committed : KnowledgeWriteStatus.AlreadyCommitted, operation);
        bool logged;
        try { logged = await _log.AppendAsync(new(request.Binding, operation, result.Status), token).WaitAsync(token); }
        catch (Exception) { logged = false; }
        authority = await AuthorizeAsync(request, token);
        if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
        return logged ? result : result with { Status = KnowledgeWriteStatus.LogUnavailable };
    }

    private async Task<DisclosureStatus> AuthorizeAsync(GenerateAdmissionRequest request, CancellationToken token)
    {
        try { return await _authority.CheckAsync(request, token).WaitAsync(token); }
        catch (Exception) { return DisclosureStatus.AuthorityUnavailable; }
    }

    private async Task<OctoberDbContext> OpenAsync(CancellationToken token)
    {
        Task<OctoberDbContext> pending = _contexts.CreateDbContextAsync(token);
        OctoberDbContext? context = null;
        try
        {
            context = await pending.WaitAsync(token);
            token.ThrowIfCancellationRequested();
            return context;
        }
        catch
        {
            if (context is not null) await context.DisposeAsync();
            else _ = DisposeLateContextAsync(pending);
            throw;
        }
    }

    private static async Task DisposeLateContextAsync(Task<OctoberDbContext> pending)
    {
        try { await (await pending).DisposeAsync(); }
        catch (Exception) { }
    }

    private static async Task ObserveAsync(Task pending)
    {
        try { await pending; }
        catch (Exception) { }
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
        catch (Exception) { }
        try { if (context is not null) await context.DisposeAsync(); }
        catch (Exception) { }
    }

    private static Task<KnowledgeOperation?> FindAsync(OctoberDbContext context, OperationKey operation, CancellationToken token) =>
        context.KnowledgeOperations.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == operation.BatchId &&
            x.Kind == operation.Kind && x.OperationId == operation.OperationId, token);

    private static Task<bool> BindingMatchesAsync(OctoberDbContext context, KnowledgeBinding binding, CancellationToken token) =>
        context.Batches.AsNoTracking().AnyAsync(x => x.BatchId == binding.BatchId && x.TargetId == binding.TargetId &&
            x.CanonicalDatabaseIdentity == binding.CanonicalDatabaseIdentity && x.SelectionDigest == binding.SelectionDigest &&
            x.State == OctoberBatchState.Selectable, token);

    private static bool Valid(GenerateAdmissionRequest request)
    {
        try
        {
            return request.Binding.BatchId != Guid.Empty && request.RunId != Guid.Empty && request.AdmissionOperationId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(request.SubjectId) && !string.IsNullOrWhiteSpace(request.Binding.TargetId) &&
                !string.IsNullOrWhiteSpace(request.Binding.CanonicalDatabaseIdentity) && !string.IsNullOrWhiteSpace(request.Binding.SelectionDigest) &&
                request.CanonicalPayloadDigest == OctoberGenerateAdmissionPolicy.ComputePayloadDigest(request);
        }
        catch (ArgumentException) { return false; }
    }

    private static async Task<CommitSettlement> SettleAsync(Task commit)
    {
        try { await commit; return CommitSettlement.Committed; }
        catch (Exception) { return CommitSettlement.Indeterminate; }
    }

    private static KnowledgeWriteStatus AuthorityStatus(DisclosureStatus status) => status == DisclosureStatus.Denied
        ? KnowledgeWriteStatus.Denied : KnowledgeWriteStatus.AuthorityUnavailable;
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
