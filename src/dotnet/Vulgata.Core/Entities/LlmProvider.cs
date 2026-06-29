namespace Vulgata.Core.Entities;

public sealed class LlmProvider
{
    private LlmProvider()
    {
    }

    public LlmProvider(
        string name,
        string baseEndpointUrl,
        string encryptedApiKey,
        ApiTypeFlags supportedApiTypes,
        AgentType defaultAgentType,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        CreatedAt = now;
        UpdatedAt = now;
        UpdateDetails(name, baseEndpointUrl, encryptedApiKey, supportedApiTypes, defaultAgentType, now);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string BaseEndpointUrl { get; private set; } = string.Empty;

    public string EncryptedApiKey { get; private set; } = string.Empty;

    public ApiTypeFlags SupportedApiTypes { get; private set; }

    public AgentType DefaultAgentType { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateDetails(
        string name,
        string baseEndpointUrl,
        string encryptedApiKey,
        ApiTypeFlags supportedApiTypes,
        AgentType defaultAgentType,
        DateTimeOffset now)
    {
        string trimmedName = (name ?? string.Empty).Trim();

        Name = trimmedName;
        NormalizedName = NormalizeName(trimmedName);
        BaseEndpointUrl = NormalizeBaseEndpointUrl(baseEndpointUrl);
        EncryptedApiKey = NormalizeRequired(encryptedApiKey);
        SupportedApiTypes = supportedApiTypes;
        DefaultAgentType = defaultAgentType;
        UpdatedAt = now;
    }

    public static string NormalizeName(string name) => (name ?? string.Empty).Trim().ToUpperInvariant();

    public static string NormalizeBaseEndpointUrl(string baseEndpointUrl)
    {
        string trimmedValue = (baseEndpointUrl ?? string.Empty).Trim();

        if (!Uri.TryCreate(trimmedValue, UriKind.Absolute, out Uri? uri))
        {
            return trimmedValue;
        }

        return uri.ToString().TrimEnd('/');
    }

    private static string NormalizeRequired(string value) => (value ?? string.Empty).Trim();
}
