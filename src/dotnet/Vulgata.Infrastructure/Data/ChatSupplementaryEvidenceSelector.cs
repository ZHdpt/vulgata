using System.Text;

namespace Vulgata.Infrastructure.Data;

internal sealed record SupplementaryChatSectionCandidate(
    Guid DocumentVersionId,
    Guid StableSectionId,
    string Title,
    string Heading,
    string ContentMarkdown);

internal static class ChatSupplementaryEvidenceSelector
{
    private static readonly HashSet<string> _ignoredTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "如何", "哪些", "什么", "主要", "完整", "流程", "系统", "客户", "用户", "进行", "可以", "需要", "之后",
    };

    public static IReadOnlyList<SupplementaryChatSectionCandidate> Select(
        string question,
        IReadOnlyCollection<SupplementaryChatSectionCandidate> candidates,
        int maximumCount)
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

        SupplementaryChatSectionCandidate[] selected = candidates
            .Select(candidate => new { Candidate = candidate, Score = Score(candidate, terms) })
            .Where(item => item.Score > 0)
            .GroupBy(item => item.Candidate.DocumentVersionId)
            .Select(group => group
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Candidate.StableSectionId)
                .First())
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Candidate.DocumentVersionId)
            .Take(maximumCount)
            .Select(item => item.Candidate)
            .ToArray();
        return Array.AsReadOnly(selected);
    }

    private static int Score(SupplementaryChatSectionCandidate candidate, string[] terms) =>
        terms.Sum(term =>
            (candidate.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ? 5 : 0) +
            (candidate.Heading.Contains(term, StringComparison.OrdinalIgnoreCase) ? 3 : 0) +
            (candidate.ContentMarkdown.Contains(term, StringComparison.OrdinalIgnoreCase) ? 1 : 0));

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
