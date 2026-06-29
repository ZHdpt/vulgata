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

    public static SystemOwnerAssignment Create(Guid systemId, string userId, DateTimeOffset now)
    {
        if (systemId == Guid.Empty)
        {
            throw new ArgumentException("系统标识不能为空。", nameof(systemId));
        }

        string normalizedUserId = (userId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedUserId))
        {
            throw new ArgumentException("用户标识不能为空。", nameof(userId));
        }

        return new SystemOwnerAssignment
        {
            Id = Guid.NewGuid(),
            SystemId = systemId,
            UserId = normalizedUserId,
            AssignedAt = now,
        };
    }
}
