using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Write-side seam for Context (Global / System / Repository). Consolidates the 6 endpoint handlers
/// that previously inlined pending/immediate branching, visibility filter, and clear-on-apply logic.
/// </summary>
public interface IContextManagementCoordinator
{
    /// <summary>
    /// Persists the context value for the given scope. If a scan is running, the change is queued as
    /// a Pending Context Change; otherwise it is applied immediately and any existing pending row is cleared.
    /// </summary>
    Task<Result> SaveAsync(ContextScope scope, string value, IUserContext user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the current and pending context values for the given scope.
    /// </summary>
    Task<ContextState> GetStateAsync(ContextScope scope, IUserContext user, CancellationToken cancellationToken = default);
}

/// <summary>
/// Discriminated scope: Global, a single System, or a single Repository.
/// </summary>
public abstract record ContextScope
{
    private ContextScope() { }

    public static readonly ContextScope Global = new GlobalScope();

    public static ContextScope ForSystem(Guid systemId) => new SystemScope(systemId);

    public static ContextScope ForRepository(Guid repositoryId) => new RepositoryScope(repositoryId);

    public abstract ContextScopeType ScopeType { get; }

    public abstract string ScopeKey { get; }

    public sealed record GlobalScope : ContextScope
    {
        public override ContextScopeType ScopeType => ContextScopeType.Global;
        public override string ScopeKey => "global";
    }

    public sealed record SystemScope(Guid SystemId) : ContextScope
    {
        public override ContextScopeType ScopeType => ContextScopeType.System;
        public override string ScopeKey => SystemId.ToString();
    }

    public sealed record RepositoryScope(Guid RepositoryId) : ContextScope
    {
        public override ContextScopeType ScopeType => ContextScopeType.Repository;
        public override string ScopeKey => RepositoryId.ToString();
    }
}

/// <summary>
/// Snapshot of a scope's context: the current value (if any), a pending value queued for application
/// after the running scan completes (if any), and whether a scan is currently running.
/// </summary>
public sealed record ContextState(string? CurrentValue, string? PendingValue, bool IsScanRunning);

/// <summary>
/// Result of a SaveAsync call. <see cref="Outcome"/> distinguishes authorization and not-found failures
/// from success.
/// </summary>
public sealed record Result(bool IsSuccess, ContextSaveOutcome Outcome, string? Message = null)
{
    public static Result Ok() => new(true, ContextSaveOutcome.Success);
    public static Result Forbidden() => new(false, ContextSaveOutcome.Forbidden, "无权限。");
    public static Result SystemNotFound() => new(false, ContextSaveOutcome.SystemNotFound, "系统不存在。");
    public static Result RepositoryNotFound() => new(false, ContextSaveOutcome.RepositoryNotFound, "仓库不存在。");
}

public enum ContextSaveOutcome
{
    Success,
    Forbidden,
    SystemNotFound,
    RepositoryNotFound,
}
