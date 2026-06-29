namespace Vulgata.Core.Entities;

public sealed class SystemOwnerAssignment
{
    private SystemOwnerAssignment()
    {
    }

    public Guid Id { get; private set; }

    public Guid SystemId { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public DateTimeOffset AssignedAt { get; private set; }

    public System System { get; private set; } = null!;
}
