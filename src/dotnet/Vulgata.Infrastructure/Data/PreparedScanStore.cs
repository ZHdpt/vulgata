using System.Data;
using Microsoft.EntityFrameworkCore;
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

        var repository = new PreparedScanRepository(context);
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
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
            await transaction.CommitAsync(cancellationToken);
            return new(PreparedScanCommitOutcome.Committed, pairPackage.PairPackageFingerprint, null);
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
        catch (InvalidOperationException)
        {
            context.ChangeTracker.Clear();
            return Result(PreparedScanCommitOutcome.Failed, pairPackage.PairPackageFingerprint,
                PreparedScanFailureReason.PersistenceFailure, "prepared-scan.persistence-failure",
                "The pair transaction could not be completed.");
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
}
