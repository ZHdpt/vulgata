using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Data;

public sealed class PreparedScanStore(VulgataDbContext context) : IPreparedScanStore
{
    public async Task<PreparedScanCommitResult> CommitPairAsync(
        PreparedScanAuthorizationScope authorizationScope,
        PreparedScanPairPackage pairPackage,
        long expectedCoordinationVersion,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result(PreparedScanCommitOutcome.Cancelled, pairPackage.PairPackageFingerprint,
                PreparedScanFailureReason.Cancelled, "prepared-scan.cancelled", "Preparation was cancelled before commit.");
        }

        IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(() => CommitPairInTransactionAsync(
                authorizationScope,
                pairPackage,
                expectedCoordinationVersion,
                cancellationToken));
        }
        catch (OperationCanceledException)
        {
            context.ChangeTracker.Clear();
            return Result(PreparedScanCommitOutcome.Cancelled, pairPackage.PairPackageFingerprint,
                PreparedScanFailureReason.Cancelled, "prepared-scan.cancelled", "Preparation was cancelled.");
        }
        catch (DbUpdateException)
        {
            return await ResolveConcurrentWriteAsync(
                authorizationScope, pairPackage, expectedCoordinationVersion, cancellationToken);
        }
        catch (NpgsqlException)
        {
            return await ResolveConcurrentWriteAsync(
                authorizationScope, pairPackage, expectedCoordinationVersion, cancellationToken);
        }
        catch (CommitAcknowledgementException)
        {
            return await ResolveConcurrentWriteAsync(
                authorizationScope, pairPackage, expectedCoordinationVersion, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            context.ChangeTracker.Clear();
            return Result(PreparedScanCommitOutcome.Failed, pairPackage.PairPackageFingerprint,
                PreparedScanFailureReason.PersistenceFailure, "prepared-scan.persistence-failure",
                "The pair transaction could not be completed.");
        }
    }

    private async Task<PreparedScanCommitResult> CommitPairInTransactionAsync(
        PreparedScanAuthorizationScope authorizationScope,
        PreparedScanPairPackage pairPackage,
        long expectedCoordinationVersion,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? transaction = null;
        bool committed = false;
        bool commitAttempted = false;
        try
        {
            transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var repository = new PreparedScanRepository(context);
            PreparedScanCommitOutcome? inspected = await repository.InspectAsync(
                authorizationScope, pairPackage, expectedCoordinationVersion, cancellationToken);
            if (inspected is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return inspected == PreparedScanCommitOutcome.Replay
                    ? new(inspected.Value, pairPackage.PairPackageFingerprint, null)
                    : Result(inspected.Value, pairPackage.PairPackageFingerprint,
                        inspected == PreparedScanCommitOutcome.Rejected
                            ? PreparedScanFailureReason.SelectionBounds
                            : PreparedScanFailureReason.CanonicalConflict,
                        inspected == PreparedScanCommitOutcome.Rejected
                            ? "prepared-scan.invalid-package"
                            : "prepared-scan.conflict",
                        "The pair package is invalid or conflicts with durable state.");
            }

            await repository.PersistAsync(pairPackage, cancellationToken);
            commitAttempted = true;
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return new(PreparedScanCommitOutcome.Committed, pairPackage.PairPackageFingerprint, null);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested &&
            !commitAttempted)
        {
            await RollbackAndClearAsync(transaction, committed);
            throw;
        }
        catch (Exception exception)
        {
            await RollbackAndClearAsync(transaction, committed);
            if (commitAttempted)
            {
                throw new CommitAcknowledgementException(exception);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static PreparedScanCommitResult Result(
        PreparedScanCommitOutcome outcome,
        string? fingerprint,
        PreparedScanFailureReason reason,
        string code,
        string diagnostic) => new(outcome, fingerprint, new(reason, code, diagnostic));

    private async Task<PreparedScanCommitResult> ResolveConcurrentWriteAsync(
        PreparedScanAuthorizationScope authorizationScope,
        PreparedScanPairPackage pairPackage,
        long expectedCoordinationVersion,
        CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var repository = new PreparedScanRepository(context);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            PreparedScanCommitOutcome? inspected = await repository.InspectAsync(
                authorizationScope, pairPackage, expectedCoordinationVersion, cancellationToken);
            if (inspected == PreparedScanCommitOutcome.Replay)
            {
                return new(PreparedScanCommitOutcome.Replay, pairPackage.PairPackageFingerprint, null);
            }

            if (inspected is PreparedScanCommitOutcome.Conflict or PreparedScanCommitOutcome.Rejected)
            {
                break;
            }

            await Task.Yield();
        }

        return Result(PreparedScanCommitOutcome.Conflict, pairPackage.PairPackageFingerprint,
            PreparedScanFailureReason.CanonicalConflict, "prepared-scan.persistence-conflict",
            "Durable state rejected the pair package.");
    }

    private async Task RollbackAndClearAsync(
        IDbContextTransaction? transaction,
        bool committed)
    {
        if (!committed && transaction is not null)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception)
            {
                // A commit acknowledgement can fail after PostgreSQL has committed.
            }
        }

        context.ChangeTracker.Clear();
    }

    private sealed class CommitAcknowledgementException(Exception innerException) :
        Exception("The Prepared Scan commit acknowledgement was not observed.", innerException);
}
