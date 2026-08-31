namespace Vulgata.Agents.ChatExecution;

public sealed class ChatProviderReasoningOptions
{
    private static readonly HashSet<string> _supportedEfforts =
    [
        "none",
        "minimal",
        "low",
        "medium",
        "high",
        "xhigh",
        "max",
    ];

    public const string SectionName = "ChatProvider";

    public string? ReasoningEffort { get; set; }

    public static bool IsSupported(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        _supportedEfforts.Contains(value.Trim().ToLowerInvariant());

    internal static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim().ToLowerInvariant();
        return _supportedEfforts.Contains(normalized)
            ? normalized
            : throw new ArgumentException("The Chat provider reasoning effort is invalid.");
    }
}
