namespace Vulgata.Shared.Repositories;

public sealed class RepositoryDetailDto : RepositorySummaryDto
{
    public string GitUrl { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Context { get; set; }

    public RepositoryDatabaseConnectionSummaryDto? DatabaseConnection { get; set; }
}
