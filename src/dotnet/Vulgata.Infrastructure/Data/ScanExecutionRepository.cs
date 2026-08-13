using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

/// <summary>
/// EF Core implementation of <see cref="IScanExecutionStore"/>. Persists
/// <see cref="ScanExecution"/> rows in schema <c>vulgata</c> table
/// <c>ScanExecutions</c>. Every phase transition is one atomic transaction.
///
/// NOTE: This adapter depends on the <c>ScanExecutions</c> table and the
/// <see cref="VulgataDbContext"/> DbSet declaration, both of which are added
/// by G6 (integration-owner-gated migration). Until G6 lands, this adapter
/// compiles (EF Core's <see cref="DbContext.Set{TEntity}"/> works without a
/// declared DbSet) but cannot be exercised against a real database. The G4
/// unit tests use in-memory fakes; the G6 migration tests exercise the
/// relational shape.
///
/// For SQLite integration tests, this repository does not emit unsupported
/// <c>FOR UPDATE</c>; it begins a serializable transaction and acquires the
/// database-wide write reservation with a parameterized no-op update,
/// mirroring the Story 11.1 pattern.
/// </summary>
public sealed class ScanExecutionRepository(VulgataDbContext dbContext) : IScanExecutionStore
{
    public Task<ScanExecution?> GetAsync(
        Guid scanExecutionId,
        CancellationToken cancellationToken = default) =>
        dbContext.ScanExecutions
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == scanExecutionId, cancellationToken);

    public Task<ScanExecution?> FindActiveAsync(
        Guid repositoryId,
        Guid scanRunId,
        CancellationToken cancellationToken = default) =>
        dbContext.ScanExecutions
            .AsNoTracking()
            .Where(e =>
                e.RepositoryId == repositoryId &&
                e.ScanRunId == scanRunId &&
                e.Phase != ScanExecutionPhase.Completed &&
                e.Phase != ScanExecutionPhase.Failed &&
                e.Phase != ScanExecutionPhase.Cancelled)
            .OrderBy(e => e.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task PersistAsync(
        ScanExecution execution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);
        dbContext.ChangeTracker.Clear();

        bool isSqlite = StringComparer.Ordinal.Equals(
            dbContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.Sqlite");
        bool isPostgreSql = StringComparer.Ordinal.Equals(
            dbContext.Database.ProviderName,
            "Npgsql.EntityFrameworkCore.PostgreSQL");
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction = await dbContext.Database
                .BeginTransactionAsync(
                    isSqlite ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted,
                    cancellationToken)
                .ConfigureAwait(false);
            try
            {
                if (isSqlite)
                {
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"Repositories\" SET \"Id\" = \"Id\" WHERE \"Id\" = {execution.RepositoryId}",
                        cancellationToken).ConfigureAwait(false);
                }
                else if (isPostgreSql)
                {
                    await dbContext.Repositories
                        .FromSqlInterpolated(
                            $"SELECT * FROM \"vulgata\".\"Repositories\" WHERE \"Id\" = {execution.RepositoryId} FOR UPDATE")
                        .AsNoTracking()
                        .SingleOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                IQueryable<ScanExecution> durableQuery = isPostgreSql
                    ? dbContext.ScanExecutions.FromSqlInterpolated(
                        $"SELECT * FROM \"vulgata\".\"ScanExecutions\" WHERE \"Id\" = {execution.Id} FOR UPDATE")
                    : dbContext.ScanExecutions.Where(item => item.Id == execution.Id);
                ScanExecution? durable = await durableQuery
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (durable is null)
                {
                    await dbContext.ScanExecutions.AddAsync(execution, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    ApplyRequestedTransition(durable, execution);
                }

                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                dbContext.ChangeTracker.Clear();
                throw;
            }
        }).ConfigureAwait(false);
    }

    private static void ApplyRequestedTransition(
        ScanExecution durable,
        ScanExecution requested)
    {
        EnsureIdentityMatches(durable, requested);
        if (StateMatches(durable, requested))
        {
            return;
        }

        if (requested.Phase == ScanExecutionPhase.Cancelled &&
            requested.TerminalMessage is not null &&
            requested.TerminalAt is not null)
        {
            durable.Cancel(requested.TerminalMessage, requested.TerminalAt.Value);
        }
        else if (durable.Phase == ScanExecutionPhase.Pass1Planning &&
            requested.Phase == ScanExecutionPhase.Pass1Running &&
            requested.InFlightJobId is null)
        {
            durable.Plan(requested.TotalJobCount, requested.StartedAt);
        }
        else if (durable.Phase == ScanExecutionPhase.Pass1Running &&
            requested.Phase == ScanExecutionPhase.Pass1Running)
        {
            ApplyRunningTransition(durable, requested);
        }
        else if (durable.Phase == ScanExecutionPhase.Pass1Running &&
            requested.Phase == ScanExecutionPhase.Pass1Completed &&
            requested.InFlightJobId is null)
        {
            durable.CompletePass1();
        }
        else if (durable.Phase == ScanExecutionPhase.Pass1Completed)
        {
            ApplyPass1CompletedTransition(durable, requested);
        }
        else if (durable.Phase == ScanExecutionPhase.Pass1Running &&
            requested.Phase == ScanExecutionPhase.Failed &&
            requested.TerminalReasonKind is not null &&
            requested.TerminalMessage is not null &&
            requested.TerminalAt is not null)
        {
            if (durable.InFlightJobId is Guid jobId)
            {
                durable.RecordFailure(
                    jobId,
                    requested.TerminalReasonKind.Value,
                    requested.TerminalMessage,
                    requested.TerminalAt.Value);
            }
            else
            {
                durable.RecordBoundaryRejection(
                    requested.TerminalReasonKind.Value,
                    requested.TerminalMessage,
                    requested.TerminalAt.Value);
            }
        }
        else if (durable.Phase == ScanExecutionPhase.Pass1Running &&
            requested.Phase == ScanExecutionPhase.Completed &&
            requested.TerminalMessage is not null &&
            requested.TerminalAt is not null)
        {
            durable.Finalize(requested.TerminalMessage, requested.TerminalAt.Value);
        }
        else
        {
            throw new InvalidOperationException(
                "The Scan execution transition is stale or invalid.");
        }

        if (!StateMatches(durable, requested))
        {
            throw new InvalidOperationException(
                "The Scan execution transition no longer matches durable state.");
        }
    }

    private static void ApplyRunningTransition(
        ScanExecution durable,
        ScanExecution requested)
    {
        if (durable.InFlightJobId is null && requested.InFlightJobId is Guid jobId)
        {
            durable.Advance(jobId);
            return;
        }

        if (durable.InFlightJobId is Guid completedJobId &&
            requested.InFlightJobId is null &&
            requested.CompletedJobCount == durable.CompletedJobCount + 1)
        {
            durable.RecordValid(completedJobId);
            return;
        }

        if (requested.RestartIndicator == durable.RestartIndicator + 1)
        {
            durable.ObserveRestart();
            return;
        }

        throw new InvalidOperationException(
            "The Scan execution transition is stale or invalid.");
    }

    private static void ApplyPass1CompletedTransition(
        ScanExecution durable,
        ScanExecution requested)
    {
        if (requested.Phase == ScanExecutionPhase.Pass1Completed &&
            durable.InFlightJobId is null &&
            requested.InFlightJobId is Guid pass2JobId)
        {
            durable.BeginPass2(pass2JobId);
            return;
        }

        if (durable.InFlightJobId is not Guid inFlightJobId ||
            requested.InFlightJobId is not null ||
            requested.TerminalMessage is null ||
            requested.TerminalAt is null)
        {
            throw new InvalidOperationException(
                "The Scan execution transition is stale or invalid.");
        }

        if (requested.Phase == ScanExecutionPhase.Completed)
        {
            durable.CompletePass2(
                inFlightJobId,
                requested.TerminalMessage,
                requested.TerminalAt.Value);
            return;
        }

        if (requested.Phase == ScanExecutionPhase.Failed &&
            requested.TerminalReasonKind is ScanExecutionFailureKind reasonKind)
        {
            durable.FailPass2(
                inFlightJobId,
                reasonKind,
                requested.TerminalMessage,
                requested.TerminalAt.Value);
            return;
        }

        throw new InvalidOperationException(
            "The Scan execution transition is stale or invalid.");
    }

    private static void EnsureIdentityMatches(
        ScanExecution durable,
        ScanExecution requested)
    {
        if (durable.Id != requested.Id ||
            durable.RepositoryId != requested.RepositoryId ||
            durable.SystemId != requested.SystemId ||
            durable.ScanRunId != requested.ScanRunId ||
            durable.PreScanResultId != requested.PreScanResultId ||
            !StringComparer.Ordinal.Equals(
                durable.ProfiledRevision,
                requested.ProfiledRevision) ||
            durable.StartedAt != requested.StartedAt)
        {
            throw new InvalidOperationException(
                "The Scan execution identity does not match durable state.");
        }
    }

    private static bool StateMatches(
        ScanExecution left,
        ScanExecution right) =>
        left.Id == right.Id &&
        left.RepositoryId == right.RepositoryId &&
        left.SystemId == right.SystemId &&
        left.ScanRunId == right.ScanRunId &&
        left.PreScanResultId == right.PreScanResultId &&
        StringComparer.Ordinal.Equals(left.ProfiledRevision, right.ProfiledRevision) &&
        left.Phase == right.Phase &&
        left.InFlightJobId == right.InFlightJobId &&
        left.CompletedJobCount == right.CompletedJobCount &&
        left.TotalJobCount == right.TotalJobCount &&
        left.RestartIndicator == right.RestartIndicator &&
        StringComparer.Ordinal.Equals(left.TerminalMessage, right.TerminalMessage) &&
        left.TerminalReasonKind == right.TerminalReasonKind &&
        left.StartedAt == right.StartedAt &&
        left.TerminalAt == right.TerminalAt;

    public async Task<ScanExecutionStateView> ReadStateAsync(
        Guid scanExecutionId,
        CancellationToken cancellationToken = default)
    {
        ScanExecution? execution = await GetAsync(scanExecutionId, cancellationToken)
            .ConfigureAwait(false);
        if (execution is null)
        {
            throw new InvalidOperationException(
                $"找不到标识为 {scanExecutionId} 的扫描执行记录。");
        }

        return new ScanExecutionStateView(
            execution.Id,
            execution.RepositoryId,
            execution.ScanRunId,
            execution.Phase,
            execution.InFlightJobId,
            execution.CompletedJobCount,
            execution.TotalJobCount,
            execution.RestartIndicator,
            execution.TerminalMessage,
            execution.TerminalReasonKind,
            execution.StartedAt,
            execution.TerminalAt);
    }
}
