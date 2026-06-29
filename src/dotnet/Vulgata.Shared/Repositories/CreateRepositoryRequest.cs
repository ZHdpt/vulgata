namespace Vulgata.Shared.Repositories;

public sealed class CreateRepositoryRequest
{
    public string Name { get; set; } = string.Empty;

    public string GitUrl { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Context { get; set; }
}
