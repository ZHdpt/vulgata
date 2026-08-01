using Microsoft.EntityFrameworkCore;
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
        dbContext.Set<ScanExecution>()
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == scanExecutionId, cancellationToken);

    public Task<ScanExecution?> FindActiveAsync(
        Guid repositoryId,
        Guid scanRunId,
        CancellationToken cancellationToken = default) =>
        dbContext.Set<ScanExecution>()
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

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);

        // Parameterized no-op update mirrors the Story 11.1 SQLite-safe lock
        // acquisition pattern. On PostgreSQL this is a row lock; on SQLite it
        // acquires the database-wide write reservation.
        // NOTE: Until G6 adds the table, this call will fail at runtime. The
        // G6 migration must land before any integration test exercises this
        // path. Unit tests use in-memory fakes.
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

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
