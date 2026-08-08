using System.Reflection;
using System.Text;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.ChatExecution;

public sealed class ChatPromptCatalog
{
    internal const string BusinessResourceName =
        "Vulgata.Agents.Prompts.chat-business.v1.md";
    internal const string ItResourceName =
        "Vulgata.Agents.Prompts.chat-it.v1.md";
    private const int _maximumPromptUtf8Bytes = 65_536;
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    public string GetPrompt(ChatMode mode) =>
        GetPrompt(mode, typeof(ChatPromptCatalog).Assembly);

    internal static string GetPrompt(ChatMode mode, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ChatPromptResource[] resources = assembly.GetManifestResourceNames()
            .Select(name => new ChatPromptResource(
                name,
                () => assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException(
                        "The fixed Chat Agent prompt resource is unavailable.")))
            .ToArray();
        return GetPrompt(mode, resources);
    }

    internal static string GetPrompt(
        ChatMode mode,
        IReadOnlyList<ChatPromptResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        string resourceName = mode switch
        {
            ChatMode.Business => BusinessResourceName,
            ChatMode.It => ItResourceName,
            _ => throw new InvalidOperationException("The Chat Agent mode is not supported."),
        };
        ChatPromptResource[] matches = resources
            .Where(resource => StringComparer.Ordinal.Equals(resource.Name, resourceName))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "The fixed Chat Agent prompt resource is unavailable.");
        }

        using Stream stream = matches[0].Open();
        if (!stream.CanRead || stream.Length is < 1 or > _maximumPromptUtf8Bytes)
        {
            throw new InvalidOperationException("The fixed Chat Agent prompt resource is invalid.");
        }

        using var reader = new StreamReader(
            stream,
            _strictUtf8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false);
        string prompt = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(prompt) ||
            _strictUtf8.GetByteCount(prompt) > _maximumPromptUtf8Bytes)
        {
            throw new InvalidOperationException("The fixed Chat Agent prompt resource is invalid.");
        }

        return prompt;
    }
}

internal sealed record ChatPromptResource(string Name, Func<Stream> Open);
