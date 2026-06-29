namespace Vulgata.Shared.Systems;

public sealed class SystemOwnerAssignmentDto
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTimeOffset AssignedAt { get; set; }
}
