using System.Collections.Immutable;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

internal sealed record PreparedPublicationMutation(KnowledgeWriteResult Result, ImmutableArray<Contributor> Contributors);

/// <summary>Shared atomic commit mechanics for the two fixed-selection mutations; no scheduler or durable receipt.</summary>
internal sealed class OctoberKnowledgePublicationCommitter
{
    private readonly IDbContextFactory<OctoberDbContext> _contexts;
    private readonly IOctoberKnowledgeProducerAuthorization _authorization;
    private readonly IOctoberCommitSubmissionGuard _guard;
    private readonly IOctoberKnowledgeOperationLog _log;
    private readonly OctoberKnowledgeOperationStore _operations;

    public OctoberKnowledgePublicationCommitter(IDbContextFactory<OctoberDbContext> contexts,
        IOctoberKnowledgeProducerAuthorization authorization, IOctoberCommitSubmissionGuard guard, IOctoberKnowledgeOperationLog log)
    {
        _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _operations = new(contexts);
    }

    public async Task<KnowledgeWriteResult> ExecuteAsync(KnowledgeBinding binding, OperationKey operation, OperationKind kind,
        string digest, Func<string> computeDigest, Func<CancellationToken, Task<KnowledgeWriteStatus?>> beforeWrite,
        Func<OctoberDbContext, CancellationToken, Task<PreparedPublicationMutation>> prepare, CancellationToken cancellationToken)
    {
        if (operation.BatchId != binding.BatchId || operation.Kind != kind || operation.OperationId == Guid.Empty)
            return new(KnowledgeWriteStatus.InvalidProposal, operation);
        bool submitted = false;
        Task<CommitSettlement>? actualSettlement = null;
        try
        {
            await using (var check = await _contexts.CreateDbContextAsync(cancellationToken))
                if (!await BindingMatchesAsync(check, binding, cancellationToken)) return new(KnowledgeWriteStatus.Denied, operation);
            var prior = await _operations.FindAsync(operation, cancellationToken);
            if (prior is not null) return await DiscloseAsync(binding, operation, digest, computeDigest, prior, false, cancellationToken);
            if (digest.Length != 64 || computeDigest() != digest) return new(KnowledgeWriteStatus.InvalidProposal, operation);
            var eligibility = await beforeWrite(cancellationToken);
            if (eligibility is { } rejected) return new(rejected, operation);
            var attempt = new CommitAttempt(operation, operation.OperationId, digest);
            await using (var context = await _contexts.CreateDbContextAsync(cancellationToken))
            {
                var entry = await _guard.CheckBeforeTransactionAsync(attempt, cancellationToken);
                if (entry != CommitEligibilityStatus.Eligible) return new(EntryStatus(entry), operation);
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                context.Database.AutoSavepointsEnabled = false;
                if (context.Database.IsNpgsql())
                {
                    if (kind == OperationKind.FreezeCorpus)
                        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM october.\"OctoberBatches\" WHERE \"BatchId\" = {binding.BatchId} FOR UPDATE", cancellationToken);
                    else
                        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM october.\"OctoberBatches\" WHERE \"BatchId\" = {binding.BatchId} FOR SHARE", cancellationToken);
                }
                if (!await BindingMatchesAsync(context, binding, cancellationToken)) return new(KnowledgeWriteStatus.Denied, operation);
                // A concurrent identical freeze can finish while this transaction waits on the batch lock.
                prior = await _operations.FindAsync(operation, cancellationToken);
                if (prior is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return await DiscloseAsync(binding, operation, digest, computeDigest, prior, false, cancellationToken);
                }
                eligibility = await beforeWrite(cancellationToken);
                if (eligibility is { } ineligible) return new(ineligible, operation);
                var mutation = await prepare(context, cancellationToken);
                if (mutation.Result.Status != KnowledgeWriteStatus.Committed) return mutation.Result;
                var authority = await AuthorizeAsync(binding, mutation.Contributors, cancellationToken);
                if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
                context.KnowledgeOperations.Add(new()
                {
                    BatchId = binding.BatchId, Kind = operation.Kind, OperationId = operation.OperationId,
                    PayloadDigest = digest, CommittedResultJson = OctoberKnowledgePolicy.SerializeCanonical(mutation.Result),
                    ContributorsJson = OctoberKnowledgePolicy.SerializeCanonical(mutation.Contributors), CommittedAtUtc = DateTimeOffset.UtcNow
                });
                await context.SaveChangesAsync(cancellationToken);
                authority = await AuthorizeAsync(binding, mutation.Contributors, cancellationToken);
                if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
                var guarded = await _guard.SubmitAsync(attempt, token =>
                {
                    submitted = true;
                    Task commit = transaction.GetDbTransaction().CommitAsync(token);
                    actualSettlement = SettleAsync(commit);
                    return new SubmittedCommit(actualSettlement);
                }, cancellationToken);
                if (!submitted) return new(SubmissionStatus(guarded.Status), operation);
                if (actualSettlement is not null) await actualSettlement;
            }
            return await ReconcileAsync(binding, operation, digest, actualSettlement?.Result == CommitSettlement.Committed);
        }
        catch (OperationCanceledException)
        {
            if (!submitted) return new(KnowledgeWriteStatus.CancelledBeforeCommit, operation);
        }
        catch (DbUpdateException exception) when (!submitted)
        {
            if (IsCollision(exception)) return await ReconcileAsync(binding, operation, digest, false);
            return new(KnowledgeWriteStatus.StorageUnavailable, operation);
        }
        catch (Exception error) when (!submitted && (error is ArgumentException or JsonException))
        {
            return new(KnowledgeWriteStatus.InvalidProposal, operation);
        }
        catch (Exception)
        {
            if (!submitted) return new(KnowledgeWriteStatus.StorageUnavailable, operation);
        }
        if (actualSettlement is not null) await actualSettlement;
        return await ReconcileAsync(binding, operation, digest, actualSettlement?.Result == CommitSettlement.Committed);
    }

    public async Task<DisclosureStatus> AuthorizeAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken)
    {
        try
        {
            return await _authorization.CheckAsync(binding, contributors, cancellationToken).WaitAsync(cancellationToken);
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
            await using var context = await _contexts.CreateDbContextAsync(deadline.Token);
            if (!await BindingMatchesAsync(context, binding, deadline.Token)) return new(KnowledgeWriteStatus.Denied, operation);
            for (int retry = 0; retry < 5; retry++)
            {
                var prior = await _operations.FindAsync(operation, deadline.Token);
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
