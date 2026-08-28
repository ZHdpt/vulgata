using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

internal sealed record SupplementaryChatSectionCandidate(
    Guid DocumentVersionId,
    Guid StableSectionId,
    string Title,
    string Heading,
    string ContentMarkdown,
    Guid LogicalDocumentId = default,
    string RepositoryRelativePath = "",
    string KeySymbolsJson = "",
    string BusinessTermsJson = "",
    string TechnicalTermsJson = "",
    string SearchText = "",
    DocumentType DocumentType = DocumentType.CodeLogic,
    DocumentSectionKind SectionKind = DocumentSectionKind.Overview,
    Guid RepositoryId = default,
    int SectionOrder = 0,
    string RepositoryName = "");

internal static class ChatSupplementaryEvidenceSelector
{
    private const int _maximumDocuments = 5;
    private const int _maximumRankedCandidates = 256;

    private sealed record ScoredCandidate(
        SupplementaryChatSectionCandidate Candidate,
        int Score);

    private static readonly HashSet<string> _ignoredTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "如何", "哪些", "什么", "主要", "完整", "流程", "系统", "客户", "用户", "进行", "可以", "需要", "之后",
    };

    public static IReadOnlyList<SupplementaryChatSectionCandidate> Select(
        string question,
        IReadOnlyCollection<SupplementaryChatSectionCandidate> candidates,
        int maximumCount,
        IReadOnlyCollection<Guid>? preferredLogicalDocumentIds = null,
        ChatMode mode = ChatMode.Business)
    {
        if (maximumCount <= 0 || candidates.Count == 0)
        {
            return [];
        }

        string[] terms = ExtractTerms(question).ToArray();
        if (terms.Length == 0)
        {
            return [];
        }

        HashSet<Guid> preferred = preferredLogicalDocumentIds?.ToHashSet() ?? [];
        ScoredCandidate[] ranked = candidates
            .Select(candidate => TryScore(candidate, terms, mode, out int score) && score > 0
                ? new ScoredCandidate(
                    candidate,
                    score + (preferred.Contains(candidate.LogicalDocumentId) ? 1 : 0))
                : null)
            .Where(item => item is not null)
            .Select(item => item!)
            .GroupBy(item => new
            {
                item.Candidate.DocumentVersionId,
                item.Candidate.StableSectionId,
            })
            .Select(group => group
                .OrderByDescending(item => item.Score)
                .ThenBy(item => ModePreferenceRank(item.Candidate, mode))
                .ThenBy(item => item.Candidate.RepositoryId)
                .ThenBy(item => item.Candidate.LogicalDocumentId)
                .ThenBy(item => item.Candidate.SectionOrder)
                .ThenBy(item => item.Candidate.StableSectionId)
                .First())
            .OrderByDescending(item => item.Score)
            .ThenBy(item => ModePreferenceRank(item.Candidate, mode))
            .ThenBy(item => item.Candidate.RepositoryId)
            .ThenBy(item => item.Candidate.LogicalDocumentId)
            .ThenBy(item => item.Candidate.SectionOrder)
            .ThenBy(item => item.Candidate.StableSectionId)
            .Take(_maximumRankedCandidates)
            .ToArray();
        var primary = ranked
            .GroupBy(item => item.Candidate.DocumentVersionId)
            .Select(group => group
                .OrderByDescending(item => item.Score)
                .ThenBy(item => ModePreferenceRank(item.Candidate, mode))
                .ThenBy(item => item.Candidate.RepositoryId)
                .ThenBy(item => item.Candidate.LogicalDocumentId)
                .ThenBy(item => item.Candidate.SectionOrder)
                .ThenBy(item => item.Candidate.StableSectionId)
                .First())
            .OrderByDescending(item => item.Score)
            .ThenBy(item => ModePreferenceRank(item.Candidate, mode))
            .ThenBy(item => item.Candidate.RepositoryId)
            .ThenBy(item => item.Candidate.LogicalDocumentId)
            .ThenBy(item => item.Candidate.SectionOrder)
            .ThenBy(item => item.Candidate.StableSectionId)
            .Take(Math.Min(maximumCount, _maximumDocuments))
            .ToArray();
        HashSet<Guid> selectedDocumentIds = primary
            .Select(item => item.Candidate.DocumentVersionId)
            .ToHashSet();
        HashSet<Guid> primarySectionIds = primary
            .Select(item => item.Candidate.StableSectionId)
            .ToHashSet();
        var extras = ranked
            .Where(item =>
                selectedDocumentIds.Contains(item.Candidate.DocumentVersionId) &&
                !primarySectionIds.Contains(item.Candidate.StableSectionId))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => ModePreferenceRank(item.Candidate, mode))
            .ThenBy(item => item.Candidate.RepositoryId)
            .ThenBy(item => item.Candidate.LogicalDocumentId)
            .ThenBy(item => item.Candidate.SectionOrder)
            .ThenBy(item => item.Candidate.StableSectionId)
            .GroupBy(item => item.Candidate.DocumentVersionId)
            .SelectMany(group => group.Take(3))
            .Take(Math.Max(0, maximumCount - primary.Length));
        SupplementaryChatSectionCandidate[] selected = primary
            .Concat(extras)
            .Select(item => item.Candidate)
            .ToArray();
        return Array.AsReadOnly(selected);
    }

    private static int ModePreferenceRank(
        SupplementaryChatSectionCandidate candidate,
        ChatMode mode) => mode switch
        {
            ChatMode.Business when candidate.DocumentType == DocumentType.BusinessLogic => 0,
            ChatMode.Business => 1,
            ChatMode.It when candidate.DocumentType == DocumentType.CodeLogic &&
                candidate.SectionKind is DocumentSectionKind.TechnicalDetail or
                    DocumentSectionKind.Evidence => 0,
            ChatMode.It when candidate.DocumentType == DocumentType.CodeLogic => 1,
            ChatMode.It => 2,
            _ => 3,
        };

    private static bool TryScore(
        SupplementaryChatSectionCandidate candidate,
        string[] terms,
        ChatMode mode,
        out int score)
    {
        score = 0;
        if (!TryParseTokens(candidate.KeySymbolsJson, out HashSet<string> keySymbols) ||
            !TryParseTokens(candidate.BusinessTermsJson, out HashSet<string> businessTerms) ||
            !TryParseTokens(candidate.TechnicalTermsJson, out HashSet<string> technicalTerms))
        {
            return false;
        }

        HashSet<string> activeModeTerms = mode == ChatMode.Business
            ? businessTerms
            : technicalTerms;
        score =
            (ContainsAny(candidate.RepositoryName, terms) ? 50 : 0) +
            (keySymbols.Overlaps(terms) ? 40 : 0) +
            (ContainsAny(candidate.RepositoryRelativePath, terms) ? 30 : 0) +
            (activeModeTerms.Overlaps(terms) ? 25 : 0) +
            (ContainsAny(candidate.SearchText, terms) ? 20 : 0) +
            (ContainsAny(candidate.Heading, terms) ? 5 : 0) +
            (ContainsAny(candidate.Title, terms) ? 3 : 0) +
            (ContainsAny(candidate.ContentMarkdown, terms) ? 1 : 0);
        return true;
    }

    private static bool ContainsAny(string value, IReadOnlyCollection<string> terms)
    {
        string normalized = value.Normalize(NormalizationForm.FormC);
        return terms.Any(term => normalized.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParseTokens(string json, out HashSet<string> tokens)
    {
        tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String || element.GetString() is not { } value)
                {
                    return false;
                }

                _ = tokens.Add(value.Normalize(NormalizationForm.FormC));
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IEnumerable<string> ExtractTerms(string value)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ascii = new StringBuilder();
        var cjk = new StringBuilder();

        void FlushAscii()
        {
            if (ascii.Length >= 2)
            {
                _ = terms.Add(ascii.ToString());
            }

            _ = ascii.Clear();
        }

        void FlushCjk()
        {
            for (int index = 0; index < cjk.Length - 1; index++)
            {
                string term = cjk.ToString(index, 2);
                if (!_ignoredTerms.Contains(term))
                {
                    _ = terms.Add(term);
                }
            }

            _ = cjk.Clear();
        }

        foreach (char character in value.Normalize(NormalizationForm.FormC))
        {
            if (char.IsAsciiLetterOrDigit(character) || character == '_')
            {
                FlushCjk();
                _ = ascii.Append(char.ToLowerInvariant(character));
            }
            else if (IsCjk(character))
            {
                FlushAscii();
                _ = cjk.Append(character);
            }
            else
            {
                FlushAscii();
                FlushCjk();
            }
        }

        FlushAscii();
        FlushCjk();
        return terms.Order(StringComparer.Ordinal);
    }

    private static bool IsCjk(char value) =>
        value is >= '\u3400' and <= '\u4dbf' or >= '\u4e00' and <= '\u9fff';
}
