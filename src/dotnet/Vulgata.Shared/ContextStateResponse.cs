namespace Vulgata.Shared;

public sealed class ContextStateResponse
{
    public string? CurrentContext { get; set; }

    public string? PendingContext { get; set; }

    public bool Queued { get; set; }

    public string? StatusMessage { get; set; }
}
