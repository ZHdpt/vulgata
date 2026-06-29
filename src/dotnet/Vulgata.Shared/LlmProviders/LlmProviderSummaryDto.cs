namespace Vulgata.Shared.LlmProviders;

public sealed class LlmProviderSummaryDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string BaseEndpointUrl { get; set; } = string.Empty;

    public int SupportedApiTypes { get; set; }

    public int DefaultAgentType { get; set; }

    public bool HasApiKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class LlmProviderConnectionTestResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;
}
