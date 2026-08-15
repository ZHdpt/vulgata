using System.Text;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.ChatExecution;

public static class ChatCandidateRanker
{
    private const int _maximumCandidates = 12;
    private const int _maximumGroundedFallbackCandidates = 2;
    private const int _minimumStrongCjkScore = 3;
    private const int _minimumIdentifierLength = 8;

    private static readonly HashSet<string> _ignoredTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "如何", "哪些", "什么", "主要", "完整", "流程", "系统", "客户", "用户", "进行", "可以", "需要", "之后",
    };

    public static IReadOnlyList<CandidateClaim> Rank(
        string question,
        ChatMode mode,
        IReadOnlyCollection<CandidateClaim> candidates)
    {
        string[] terms = ExtractTerms(question).ToArray();
        var scored = candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = terms.Count(term =>
                    candidate.Text.Contains(term, StringComparison.OrdinalIgnoreCase)),
            })
            .ToArray();
        var relevant = scored.Where(item => item.Score > 0);
        if (mode == ChatMode.Business)
        {
            var relevantBusiness = relevant
                .Where(item => !item.Candidate.AllowsBusinessNarrative)
                .ToArray();
            var relevantCodeLogic = relevant
                .Where(item => item.Candidate.AllowsBusinessNarrative)
                .ToArray();
            if (relevantBusiness.Length > 0 &&
                (relevantCodeLogic.Length == 0 ||
                 relevantBusiness.Max(item => item.Score) >=
                 relevantCodeLogic.Max(item => item.Score)))
            {
                relevant = relevantBusiness;
            }
        }

        var ranked = relevant.Any()
            ? relevant
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Candidate.CandidateHandle, StringComparer.Ordinal)
                .Take(_maximumCandidates)
                .Select(item => item.Candidate)
                .ToArray()
            : candidates.Take(_maximumCandidates).ToArray();
        return Array.AsReadOnly(ranked);
    }

    public static IReadOnlyList<CandidateClaim> SelectStrongGroundedMatches(
        string question,
        IReadOnlyCollection<CandidateClaim> candidates)
    {
        string[] terms = ExtractTerms(question).ToArray();
        CandidateClaim[] selected = candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = terms.Count(term =>
                    candidate.Text.Contains(term, StringComparison.OrdinalIgnoreCase)),
                HasIdentifierMatch = terms.Any(term =>
                    term.Length >= _minimumIdentifierLength &&
                    term.All(character => char.IsAsciiLetterOrDigit(character) || character == '_') &&
                    candidate.Text.Contains(term, StringComparison.OrdinalIgnoreCase)),
            })
            .Where(item => item.HasIdentifierMatch || item.Score >= _minimumStrongCjkScore)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Candidate.AllowsBusinessNarrative)
            .ThenBy(item => item.Candidate.CandidateHandle, StringComparer.Ordinal)
            .Take(_maximumGroundedFallbackCandidates)
            .Select(item => item.Candidate)
            .ToArray();
        return Array.AsReadOnly(selected);
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
            else if (character is >= '\u3400' and <= '\u4dbf' or >= '\u4e00' and <= '\u9fff')
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
}
