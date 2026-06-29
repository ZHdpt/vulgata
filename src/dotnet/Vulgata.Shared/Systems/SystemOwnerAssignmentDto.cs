namespace Vulgata.Shared.Systems;

public sealed record SystemOwnerAssignmentDto(
    string UserId,
    string DisplayName,
    string Email,
    DateTimeOffset AssignedAt);
