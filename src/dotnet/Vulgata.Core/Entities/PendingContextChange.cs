namespace Vulgata.Core.Entities;

public sealed class PendingContextChange
{
    private PendingContextChange()
    {
    }

    public PendingContextChange(ContextScopeType scopeType, string scopeKey, string? context, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        ScopeType = scopeType;
        ScopeKey = scopeKey;
        Context = context;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public ContextScopeType ScopeType { get; private set; }

    public string ScopeKey { get; private set; } = string.Empty;

    public string? Context { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateContext(string? context, DateTimeOffset now)
    {
        Context = context;
        UpdatedAt = now;
    }
}

public enum ContextScopeType
{
    Global = 0,
    System = 1,
    Repository = 2,
}
