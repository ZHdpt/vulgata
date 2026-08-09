using System.Text;

namespace Vulgata.Shared.Chat;

public enum PreparedChatMode
{
    Business,
    It,
}

public sealed class PreparedChatQuestionRequest
{
    public PreparedChatMode? Mode { get; set; }

    public Guid SystemId { get; set; }

    public IReadOnlyCollection<Guid> RepositoryIds { get; set; } = [];

    public string? Question { get; set; }

    public string NormalizedQuestion =>
        TryNormalizeQuestion(Question, out string normalized) ? normalized : string.Empty;

    public static bool TryNormalizeQuestion(string? question, out string normalized)
    {
        normalized = string.Empty;
        if (question is null)
        {
            return false;
        }

        string formC = question.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(formC.Length);
        bool whitespacePending = false;
        foreach (char character in formC)
        {
            if (char.IsWhiteSpace(character))
            {
                whitespacePending = builder.Length > 0;
                continue;
            }

            if (char.IsControl(character))
            {
                return false;
            }

            if (whitespacePending)
            {
                _ = builder.Append(' ');
                whitespacePending = false;
            }

            _ = builder.Append(character);
        }

        normalized = builder.ToString();
        return true;
    }
}
