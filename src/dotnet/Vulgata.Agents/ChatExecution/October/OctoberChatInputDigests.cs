using System.Text;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.ChatExecution.October;

/// <summary>Exact embedded prompt and initial question binding; history is merged separately by its owner.</summary>
public sealed record OctoberChatInputDigests(string PromptDigest, string InitialInputDigest)
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    public static async Task<OctoberChatInputDigests?> CreateAsync(KnowledgeMode mode, string question,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mode is not (KnowledgeMode.Business or KnowledgeMode.IT) || string.IsNullOrWhiteSpace(question)
            || question.Length > 2 * OctoberDialogueLimits.MaximumQuestionScalars) return null;
        try
        {
            if (_strictUtf8.GetByteCount(question) > OctoberDialogueLimits.MaximumQuestionUtf8Bytes
                || question.EnumerateRunes().Count() > OctoberDialogueLimits.MaximumQuestionScalars) return null;
        }
        catch (EncoderFallbackException) { return null; }

        string? prompt = await new OctoberChatPromptCatalog().LoadAsync(mode, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (prompt is null) return null;
        string promptDigest = OctoberKnowledgePolicy.Hash(prompt);
        string initialDigest = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(
            new object[] { "october-chat-initial-v1", (int)mode, promptDigest, question }));
        return new(promptDigest, initialDigest);
    }
}
