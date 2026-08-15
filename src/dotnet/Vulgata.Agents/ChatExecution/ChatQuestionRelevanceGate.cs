namespace Vulgata.Agents.ChatExecution;

public static class ChatQuestionRelevanceGate
{
    private static readonly string[] _externalTopicMarkers =
    [
        "天气", "新闻", "体育比分", "旅游攻略", "菜谱", "星座运势",
    ];

    public static bool IsDefinitelyUnrelated(string question)
    {
        string normalized = question.Trim();
        if (_externalTopicMarkers.Any(marker =>
                normalized.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (normalized is "你好" or "您好" or "谢谢" or "hello" or "hi")
        {
            return true;
        }

        return normalized.Length >= 8 &&
            normalized.All(char.IsAsciiLetterOrDigit) &&
            !ContainsVowel(normalized);
    }

    private static bool ContainsVowel(string value) =>
        value.Contains('a', StringComparison.OrdinalIgnoreCase) ||
        value.Contains('e', StringComparison.OrdinalIgnoreCase) ||
        value.Contains('i', StringComparison.OrdinalIgnoreCase) ||
        value.Contains('o', StringComparison.OrdinalIgnoreCase) ||
        value.Contains('u', StringComparison.OrdinalIgnoreCase);
}
