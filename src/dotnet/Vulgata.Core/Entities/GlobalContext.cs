namespace Vulgata.Core.Entities;

public sealed class GlobalContext
{
    private GlobalContext()
    {
    }

    public GlobalContext(string context, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Context = context;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Context { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateContext(string context, DateTimeOffset now)
    {
        Context = context ?? string.Empty;
        UpdatedAt = now;
    }
}
