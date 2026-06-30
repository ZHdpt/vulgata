using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Web.Api.Context;

internal static class ContextApiEndpoints
{
    public static void MapContextApiEndpoints(this WebApplication app)
    {
        RouteGroupBuilder settingsApi = app.MapGroup("/api/settings")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        settingsApi.MapGet("/global-context", async (
            IDbContextFactory<VulgataDbContext> dbFactory,
            IScanStateService scanState,
            CancellationToken cancellationToken) =>
        {
            await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
            GlobalContext? global = await db.GlobalContexts.FirstOrDefaultAsync(cancellationToken);

            PendingContextChange? pending = await db.PendingContextChanges
                .Where(p => p.ScopeType == ContextScopeType.Global && p.ScopeKey == "global")
                .FirstOrDefaultAsync(cancellationToken);

            return Results.Ok(new ContextStateResponse
            {
                CurrentContext = global?.Context,
                PendingContext = pending?.Context,
                Queued = pending is not null,
                StatusMessage = pending is not null ? "上下文修改将在当前扫描完成后生效" : null,
            });
        }).RequireAuthorization(AuthorizationPolicyNames.AdministratorOnly);

        settingsApi.MapPut("/global-context", async (
            HttpRequest request,
            IDbContextFactory<VulgataDbContext> dbFactory,
            IScanStateService scanState,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            if (!authResult.Succeeded)
            {
                return Results.Forbid();
            }

            ContextSaveRequest? body = await request.ReadFromJsonAsync<ContextSaveRequest>(cancellationToken);
            if (body is null)
            {
                return Results.Problem(detail: "请求格式无效。", statusCode: StatusCodes.Status400BadRequest);
            }

            string normalizedContext = string.IsNullOrWhiteSpace(body.Context) ? string.Empty : body.Context.Trim();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

            bool scanRunning = await scanState.IsAnyScanRunningAsync(cancellationToken);

            if (scanRunning)
            {
                PendingContextChange? pending = await db.PendingContextChanges
                    .Where(p => p.ScopeType == ContextScopeType.Global && p.ScopeKey == "global")
                    .FirstOrDefaultAsync(cancellationToken);

                if (pending is null)
                {
                    db.PendingContextChanges.Add(new PendingContextChange(
                        ContextScopeType.Global, "global", string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now));
                }
                else
                {
                    pending.UpdateContext(string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now);
                }

                await db.SaveChangesAsync(cancellationToken);

                GlobalContext? currentGlobal = await db.GlobalContexts.FirstOrDefaultAsync(cancellationToken);

                return Results.Ok(new ContextStateResponse
                {
                    CurrentContext = currentGlobal?.Context,
                    PendingContext = string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext,
                    Queued = true,
                    StatusMessage = "上下文修改将在当前扫描完成后生效",
                });
            }

            // No scan running — apply immediately
            GlobalContext? existing = await db.GlobalContexts.FirstOrDefaultAsync(cancellationToken);

            if (existing is null)
            {
                db.GlobalContexts.Add(new GlobalContext(string.IsNullOrEmpty(normalizedContext) ? string.Empty : normalizedContext, now));
            }
            else
            {
                existing.UpdateContext(normalizedContext, now);
            }

            // Clear any pending changes for global scope
            PendingContextChange? existingPending = await db.PendingContextChanges
                .Where(p => p.ScopeType == ContextScopeType.Global && p.ScopeKey == "global")
                .FirstOrDefaultAsync(cancellationToken);
            if (existingPending is not null)
            {
                db.PendingContextChanges.Remove(existingPending);
            }

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new ContextStateResponse
            {
                CurrentContext = string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext,
                PendingContext = null,
                Queued = false,
                StatusMessage = null,
            });
        }).RequireAuthorization(AuthorizationPolicyNames.AdministratorOnly);

        RouteGroupBuilder systemContextApi = app.MapGroup("/api/systems/{systemId:guid}/context")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        systemContextApi.MapGet("/", async (
            Guid systemId,
            ISystemRepository systemRepository,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            IDbContextFactory<VulgataDbContext> dbFactory,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            SystemEntity? system = await systemRepository.GetVisibleByIdAsync(systemId, userId, isAdministrator, cancellationToken);
            if (system is null)
            {
                return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
            PendingContextChange? pending = await db.PendingContextChanges
                .Where(p => p.ScopeType == ContextScopeType.System && p.ScopeKey == systemId.ToString())
                .FirstOrDefaultAsync(cancellationToken);

            return Results.Ok(new ContextStateResponse
            {
                CurrentContext = system.Context,
                PendingContext = pending?.Context,
                Queued = pending is not null,
                StatusMessage = pending is not null ? "上下文修改将在当前扫描完成后生效" : null,
            });
        });

        systemContextApi.MapPut("/", async (
            Guid systemId,
            HttpRequest request,
            ISystemRepository systemRepository,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            IDbContextFactory<VulgataDbContext> dbFactory,
            IScanStateService scanState,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            SystemEntity? system = await systemRepository.GetVisibleByIdAsync(systemId, userId, isAdministrator, cancellationToken);
            if (system is null)
            {
                return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            ContextSaveRequest? body = await request.ReadFromJsonAsync<ContextSaveRequest>(cancellationToken);
            if (body is null)
            {
                return Results.Problem(detail: "请求格式无效。", statusCode: StatusCodes.Status400BadRequest);
            }

            string normalizedContext = string.IsNullOrWhiteSpace(body.Context) ? string.Empty : body.Context.Trim();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
            bool scanRunning = await scanState.IsSystemScanRunningAsync(systemId, cancellationToken);

            if (scanRunning)
            {
                PendingContextChange? pending = await db.PendingContextChanges
                    .Where(p => p.ScopeType == ContextScopeType.System && p.ScopeKey == systemId.ToString())
                    .FirstOrDefaultAsync(cancellationToken);

                if (pending is null)
                {
                    db.PendingContextChanges.Add(new PendingContextChange(
                        ContextScopeType.System, systemId.ToString(), string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now));
                }
                else
                {
                    pending.UpdateContext(string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now);
                }

                await db.SaveChangesAsync(cancellationToken);

                return Results.Ok(new ContextStateResponse
                {
                    CurrentContext = system.Context,
                    PendingContext = string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext,
                    Queued = true,
                    StatusMessage = "上下文修改将在当前扫描完成后生效",
                });
            }

            // No scan — apply immediately
            SystemEntity? trackedSystem = await systemRepository.GetByIdAsync(systemId, cancellationToken);
            if (trackedSystem is null)
            {
                return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            trackedSystem.UpdateDetails(trackedSystem.Name, trackedSystem.Description, string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now);
            await systemRepository.SaveChangesAsync(cancellationToken);

            // Clear any pending
            PendingContextChange? existingPending = await db.PendingContextChanges
                .Where(p => p.ScopeType == ContextScopeType.System && p.ScopeKey == systemId.ToString())
                .FirstOrDefaultAsync(cancellationToken);
            if (existingPending is not null)
            {
                db.PendingContextChanges.Remove(existingPending);
                await db.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(new ContextStateResponse
            {
                CurrentContext = string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext,
                PendingContext = null,
                Queued = false,
                StatusMessage = null,
            });
        });

        RouteGroupBuilder repoContextApi = app.MapGroup("/api/repositories/{repositoryId:guid}/context")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        repoContextApi.MapGet("/", async (
            Guid repositoryId,
            IRepositoryRepository repositoryRepository,
            ISystemRepository systemRepository,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            IDbContextFactory<VulgataDbContext> dbFactory,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            RepositoryEntity? repo = await repositoryRepository.GetByIdAsync(repositoryId, cancellationToken);
            if (repo is null)
            {
                return Results.Problem(detail: "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            if (repo.SystemId.HasValue && !isAdministrator)
            {
                SystemEntity? visibleSystem = await systemRepository.GetVisibleByIdAsync(
                    repo.SystemId.Value,
                    userId,
                    isAdministrator,
                    cancellationToken);

                if (visibleSystem is null)
                {
                    return Results.Problem(detail: "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
                }
            }

            await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
            PendingContextChange? pending = await db.PendingContextChanges
                .Where(p => p.ScopeType == ContextScopeType.Repository && p.ScopeKey == repositoryId.ToString())
                .FirstOrDefaultAsync(cancellationToken);

            return Results.Ok(new ContextStateResponse
            {
                CurrentContext = repo.Context,
                PendingContext = pending?.Context,
                Queued = pending is not null,
                StatusMessage = pending is not null ? "上下文修改将在当前扫描完成后生效" : null,
            });
        });

        repoContextApi.MapPut("/", async (
            Guid repositoryId,
            HttpRequest request,
            IRepositoryRepository repositoryRepository,
            ISystemRepository systemRepository,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            IDbContextFactory<VulgataDbContext> dbFactory,
            IScanStateService scanState,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            RepositoryEntity? repo = await repositoryRepository.GetByIdAsync(repositoryId, cancellationToken);
            if (repo is null)
            {
                return Results.Problem(detail: "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            if (repo.SystemId.HasValue && !isAdministrator)
            {
                SystemEntity? visibleSystem = await systemRepository.GetVisibleByIdAsync(
                    repo.SystemId.Value,
                    userId,
                    isAdministrator,
                    cancellationToken);

                if (visibleSystem is null)
                {
                    return Results.Problem(detail: "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
                }
            }

            ContextSaveRequest? body = await request.ReadFromJsonAsync<ContextSaveRequest>(cancellationToken);
            if (body is null)
            {
                return Results.Problem(detail: "请求格式无效。", statusCode: StatusCodes.Status400BadRequest);
            }

            string normalizedContext = string.IsNullOrWhiteSpace(body.Context) ? string.Empty : body.Context.Trim();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            await using VulgataDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
            bool scanRunning = await scanState.IsRepositoryScanRunningAsync(repositoryId, cancellationToken);

            if (scanRunning)
            {
                PendingContextChange? pending = await db.PendingContextChanges
                    .Where(p => p.ScopeType == ContextScopeType.Repository && p.ScopeKey == repositoryId.ToString())
                    .FirstOrDefaultAsync(cancellationToken);

                if (pending is null)
                {
                    db.PendingContextChanges.Add(new PendingContextChange(
                        ContextScopeType.Repository, repositoryId.ToString(), string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now));
                }
                else
                {
                    pending.UpdateContext(string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now);
                }

                await db.SaveChangesAsync(cancellationToken);

                return Results.Ok(new ContextStateResponse
                {
                    CurrentContext = repo.Context,
                    PendingContext = string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext,
                    Queued = true,
                    StatusMessage = "上下文修改将在当前扫描完成后生效",
                });
            }

            // No scan — apply immediately
            repo.UpdateDetails(repo.Name, repo.GitUrl, repo.Description, string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext, now);
            await repositoryRepository.SaveChangesAsync(cancellationToken);

            PendingContextChange? existingPending = await db.PendingContextChanges
                .Where(p => p.ScopeType == ContextScopeType.Repository && p.ScopeKey == repositoryId.ToString())
                .FirstOrDefaultAsync(cancellationToken);
            if (existingPending is not null)
            {
                db.PendingContextChanges.Remove(existingPending);
                await db.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(new ContextStateResponse
            {
                CurrentContext = string.IsNullOrEmpty(normalizedContext) ? null : normalizedContext,
                PendingContext = null,
                Queued = false,
                StatusMessage = null,
            });
        });
    }
}
