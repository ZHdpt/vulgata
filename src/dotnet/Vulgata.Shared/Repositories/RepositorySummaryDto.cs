namespace Vulgata.Shared.Repositories;

public class RepositorySummaryDto
{
    public Guid Id { get; set; }

    public Guid SystemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ScanStatus { get; set; } = "未扫描";

    public DateTimeOffset? LastScannedAt { get; set; }

    public int DocumentCount { get; set; }
}
