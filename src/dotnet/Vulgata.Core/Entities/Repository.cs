namespace Vulgata.Core.Entities;

public sealed class Repository
{
    private Repository()
    {
    }

    public Guid Id { get; private set; }

    public Guid? SystemId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string GitUrl { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public System? System { get; private set; }
}
