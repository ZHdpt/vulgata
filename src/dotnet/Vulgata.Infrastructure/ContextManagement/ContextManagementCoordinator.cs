using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Infrastructure.ContextManagement;

/// <summary>
/// Write-side implementation of Context management. Owns the pending/immediate branching,
/// visibility filter, and clear-on-apply contract that was previously inlined across 6 endpoint handlers.
/// </summary>
public sealed class ContextManagementCoordinator(
    IDbContextFactory<VulgataDbContext> dbFactory,
    IScanStateService scanState,
    ISystemRepository systemRepository) : IContextManagementCoordinator
{
    public async Task<Result> SaveAsync(
        ContextScope scope,
        string value,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        switch (scope)
        {
            case ContextScope.GlobalScope:
                return await SaveGlobalAsync(value, user, cancellationToken);

            case ContextScope.SystemScope systemScope:
                return await SaveSystemAsync(systemScope.SystemId, value, user, cancellationToken);

            case ContextScope.RepositoryScope repositoryScope:
                return await SaveRepositoryAsync(repositoryScope.RepositoryId, value, user, cancellationToken);

            default:
                throw new InvalidOperationException($"Unknown scope type: {scope}");
        }
    }

    public async Task<ContextState> GetStateAsync(
        ContextScope scope,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        switch (scope)
        {
            case ContextScope.GlobalScope:
                return await GetGlobalStateAsync(user, cancellationToken);

            case ContextScope.SystemScope systemScope:
                return await GetSystemStateAsync(systemScope.SystemId, user, cancellationToken);

            case ContextScope.RepositoryScope repositoryScope:
                return await GetRepositoryStateAsync(repositoryScope.RepositoryId, user, cancellationToken);

            default:
                throw new InvalidOperationException($"Unknown scope type: {scope}");
        }
    }

    private async Task<Result> SaveGlobalAsync(string value, IUserContext user, CancellationToken cancellationToken)
    {
        if (!user.IsAdministrator)
        {
            return Result.Forbidden();
        }

        string normalized = Normalize(value);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (await scanState.IsAnyScanRunningAsync(cancellationToken))
        {
            await UpsertPendingAsync(db, ContextScopeType.Global, "global", normalized, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Ok();
        }

        await ApplyGlobalImmediateAsync(db, normalized, now, cancellationToken);
        await ClearPendingAsync(db, ContextScopeType.Global, "global", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private async Task<Result> SaveSystemAsync(Guid systemId, string value, IUserContext user, CancellationToken cancellationToken)
    {
        SystemEntity? system = await systemRepository.GetVisibleByIdAsync(
            systemId, user.ToSystemOwnerFilter(), cancellationToken);

        if (system is null)
        {
            return Result.SystemNotFound();
        }

        string normalized = Normalize(value);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (await scanState.IsSystemScanRunningAsync(systemId, cancellationToken))
        {
            await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
            await UpsertPendingAsync(db, ContextScopeType.System, systemId.ToString(), normalized, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Ok();
        }

        SystemEntity? tracked = await systemRepository.GetByIdAsync(systemId, cancellationToken);
        if (tracked is null)
        {
            return Result.SystemNotFound();
        }

        tracked.UpdateDetails(tracked.Name, tracked.Description, string.IsNullOrEmpty(normalized) ? null : normalized, now);
        await systemRepository.SaveChangesAsync(cancellationToken);

        await using VulgataDbContext clearDb = await dbFactory.CreateDbContextAsync(cancellationToken);
        await ClearPendingAsync(clearDb, ContextScopeType.System, systemId.ToString(), cancellationToken);
        await clearDb.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }

    private async Task<Result> SaveRepositoryAsync(Guid repositoryId, string value, IUserContext user, CancellationToken cancellationToken)
    {
        await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        Repository? repo = await db.Repositories.FirstOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);
        if (repo is null)
        {
            return Result.RepositoryNotFound();
        }

        if (repo.SystemId.HasValue && !user.IsAdministrator)
        {
            SystemEntity? visibleSystem = await systemRepository.GetVisibleByIdAsync(
                repo.SystemId.Value, user.ToSystemOwnerFilter(), cancellationToken);

            if (visibleSystem is null)
            {
                return Result.RepositoryNotFound();
            }
        }

        string normalized = Normalize(value);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (await scanState.IsRepositoryScanRunningAsync(repositoryId, cancellationToken))
        {
            await UpsertPendingAsync(db, ContextScopeType.Repository, repositoryId.ToString(), normalized, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Ok();
        }

        repo.UpdateDetails(repo.Name, repo.GitUrl, repo.Description, string.IsNullOrEmpty(normalized) ? null : normalized, now);
        await db.SaveChangesAsync(cancellationToken);

        await ClearPendingAsync(db, ContextScopeType.Repository, repositoryId.ToString(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }

    private async Task<ContextState> GetGlobalStateAsync(IUserContext user, CancellationToken cancellationToken)
    {
        if (!user.IsAdministrator)
        {
            return new ContextState(CurrentValue: null, PendingValue: null, IsScanRunning: false);
        }

        await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        GlobalContext? global = await db.GlobalContexts.FirstOrDefaultAsync(cancellationToken);
        PendingContextChange? pending = await GetPendingAsync(db, ContextScopeType.Global, "global", cancellationToken);
        bool scanRunning = await scanState.IsAnyScanRunningAsync(cancellationToken);

        return new ContextState(global?.Context, pending?.Context, scanRunning);
    }

    private async Task<ContextState> GetSystemStateAsync(Guid systemId, IUserContext user, CancellationToken cancellationToken)
    {
        SystemEntity? system = await systemRepository.GetVisibleByIdAsync(
            systemId, user.ToSystemOwnerFilter(), cancellationToken);

        if (system is null)
        {
            return new ContextState(CurrentValue: null, PendingValue: null, IsScanRunning: false);
        }

        await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
        PendingContextChange? pending = await GetPendingAsync(db, ContextScopeType.System, systemId.ToString(), cancellationToken);
        bool scanRunning = await scanState.IsSystemScanRunningAsync(systemId, cancellationToken);

        return new ContextState(system.Context, pending?.Context, scanRunning);
    }

    private async Task<ContextState> GetRepositoryStateAsync(Guid repositoryId, IUserContext user, CancellationToken cancellationToken)
    {
        await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
        Repository? repo = await db.Repositories.FirstOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);

        if (repo is null)
        {
            return new ContextState(CurrentValue: null, PendingValue: null, IsScanRunning: false);
        }

        if (repo.SystemId.HasValue && !user.IsAdministrator)
        {
            SystemEntity? visibleSystem = await systemRepository.GetVisibleByIdAsync(
                repo.SystemId.Value, user.ToSystemOwnerFilter(), cancellationToken);

            if (visibleSystem is null)
            {
                return new ContextState(CurrentValue: null, PendingValue: null, IsScanRunning: false);
            }
        }

        PendingContextChange? pending = await GetPendingAsync(db, ContextScopeType.Repository, repositoryId.ToString(), cancellationToken);
        bool scanRunning = await scanState.IsRepositoryScanRunningAsync(repositoryId, cancellationToken);

        return new ContextState(repo.Context, pending?.Context, scanRunning);
    }

    private static async Task ApplyGlobalImmediateAsync(
        VulgataDbContext db,
        string normalized,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        GlobalContext? existing = await db.GlobalContexts.FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            db.GlobalContexts.Add(new GlobalContext(string.IsNullOrEmpty(normalized) ? string.Empty : normalized, now));
        }
        else
        {
            existing.UpdateContext(normalized, now);
        }
    }

    private static async Task UpsertPendingAsync(
        VulgataDbContext db,
        ContextScopeType scopeType,
        string scopeKey,
        string normalized,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        PendingContextChange? pending = await db.PendingContextChanges
            .Where(p => p.ScopeType == scopeType && p.ScopeKey == scopeKey)
            .FirstOrDefaultAsync(cancellationToken);

        string? pendingValue = string.IsNullOrEmpty(normalized) ? null : normalized;

        if (pending is null)
        {
            db.PendingContextChanges.Add(new PendingContextChange(scopeType, scopeKey, pendingValue, now));
        }
        else
        {
            pending.UpdateContext(pendingValue, now);
        }
    }

    private static async Task ClearPendingAsync(
        VulgataDbContext db,
        ContextScopeType scopeType,
        string scopeKey,
        CancellationToken cancellationToken)
    {
        PendingContextChange? existingPending = await db.PendingContextChanges
            .Where(p => p.ScopeType == scopeType && p.ScopeKey == scopeKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingPending is not null)
        {
            db.PendingContextChanges.Remove(existingPending);
        }
    }

    private static async Task<PendingContextChange?> GetPendingAsync(
        VulgataDbContext db,
        ContextScopeType scopeType,
        string scopeKey,
        CancellationToken cancellationToken)
    {
        return await db.PendingContextChanges
            .Where(p => p.ScopeType == scopeType && p.ScopeKey == scopeKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
