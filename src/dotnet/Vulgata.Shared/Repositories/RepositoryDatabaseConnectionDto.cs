namespace Vulgata.Shared.Repositories;

public sealed class RepositoryDatabaseConnectionSummaryDto
{
    public Guid RepositoryId { get; set; }

    public int? DatabaseType { get; set; }

    public bool IsConfigured { get; set; }

    public bool HasConnectionString { get; set; }

    public bool HasUsername { get; set; }

    public bool HasPassword { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class RepositoryDatabaseConnectionTestResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;
}
