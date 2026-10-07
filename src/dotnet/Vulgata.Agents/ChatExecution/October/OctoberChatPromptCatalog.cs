using Vulgata.Core.DomainServices.OctoberKnowledge;
using System.Text;

namespace Vulgata.Agents.ChatExecution.October;

internal sealed record OctoberChatPromptResource(string Name, Func<Stream> Open);

/// <summary>Fixed prompt resource names. Shared project resource registration remains integration-owned.</summary>
internal sealed class OctoberChatPromptCatalog
{
    internal const string BusinessResourceName = "Vulgata.Agents.Prompts.OctoberChatBusiness.txt";
    internal const string ItResourceName = "Vulgata.Agents.Prompts.OctoberChatIT.txt";
    internal const int MaximumPromptBytes = 32 * 1024;
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private readonly IReadOnlyList<OctoberChatPromptResource> _resources;

    public OctoberChatPromptCatalog(IReadOnlyList<OctoberChatPromptResource>? resources = null)
    {
        if (resources is not null)
        {
            _resources = resources.ToArray();
            return;
        }

        var assembly = typeof(OctoberChatPromptCatalog).Assembly;
        _resources = assembly.GetManifestResourceNames().Select(name =>
            new OctoberChatPromptResource(name, () => assembly.GetManifestResourceStream(name) ??
                throw new InvalidOperationException("The selected prompt is unavailable."))).ToArray();
    }

    public async Task<string?> LoadAsync(KnowledgeMode mode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? name = mode switch
        {
            KnowledgeMode.Business => BusinessResourceName,
            KnowledgeMode.IT => ItResourceName,
            _ => null,
        };
        if (name is null) return null;

        OctoberChatPromptResource[] matches = _resources.Where(resource =>
            StringComparer.Ordinal.Equals(resource.Name, name)).ToArray();
        if (matches.Length != 1) return null;

        try
        {
            await using Stream stream = matches[0].Open();
            // The extra byte distinguishes a complete exact-limit stream from an
            // oversized one without seeking, truncating or reading it without bounds.
            byte[] bytes = new byte[MaximumPromptBytes + 1];
            int length = 0;
            while (length < bytes.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(bytes.AsMemory(length), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (read == 0)
                {
                    string prompt = _strictUtf8.GetString(bytes.AsSpan(0, length));
                    return string.IsNullOrWhiteSpace(prompt) ? null : prompt;
                }

                length += read;
            }

            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Resource names, paths and raw failures never become a fallback prompt.
            return null;
        }
    }
}
