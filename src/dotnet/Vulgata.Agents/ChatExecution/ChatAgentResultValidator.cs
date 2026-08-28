using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.ChatExecution;

public sealed class ChatAgentResultValidation
{
    private ChatAgentResultValidation(
        bool isValid,
        IReadOnlyList<MaterialClaim> materialClaims,
        IReadOnlyList<CandidateClaim> selectedCandidates,
        bool isAnswerable)
    {
        IsValid = isValid;
        MaterialClaims = materialClaims;
        SelectedCandidates = selectedCandidates;
        IsAnswerable = isAnswerable;
    }

    public bool IsValid { get; }

    public IReadOnlyList<MaterialClaim> MaterialClaims { get; }

    public IReadOnlyList<CandidateClaim> SelectedCandidates { get; }

    public bool IsAnswerable { get; }

    internal static ChatAgentResultValidation Valid(
        IReadOnlyList<MaterialClaim> materialClaims,
        IReadOnlyList<CandidateClaim> selectedCandidates) =>
        new(true, materialClaims, selectedCandidates, true);

    internal static ChatAgentResultValidation Unsupported() =>
        new(true, [], [], false);

    internal static ChatAgentResultValidation Invalid() =>
        new(false, [], [], false);
}

public sealed class ChatAgentResultValidator(ChatCandidateExtractor candidateExtractor)
{
    private readonly ChatCandidateExtractor _candidateExtractor = candidateExtractor;

    public ChatAgentResultValidation Validate(
        EvidencePacket packet,
        ChatSynthesisRequest request,
        ChatSynthesisOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(outcome);

        if (outcome.RawSelectionJson is null)
        {
            return ChatAgentResultValidation.Invalid();
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(outcome.RawSelectionJson);
            JsonElement root = document.RootElement;
            if (!HasExactProperties(
                    root,
                    "schemaVersion",
                    "requestId",
                    "mode",
                    "evidenceFingerprint",
                    "answerability",
                    "selections") ||
                root.GetProperty("schemaVersion").ValueKind != JsonValueKind.String ||
                !IsCompatibleSchemaVersion(root.GetProperty("schemaVersion").GetString()) ||
                root.GetProperty("requestId").ValueKind != JsonValueKind.String ||
                !string.Equals(root.GetProperty("requestId").GetString(), request.RequestId, StringComparison.Ordinal) ||
                root.GetProperty("mode").ValueKind != JsonValueKind.String ||
                !string.Equals(root.GetProperty("mode").GetString(), ToProviderMode(request.Mode), StringComparison.Ordinal) ||
                root.GetProperty("evidenceFingerprint").ValueKind != JsonValueKind.String ||
                !string.Equals(
                    root.GetProperty("evidenceFingerprint").GetString(),
                    request.EvidenceFingerprint,
                    StringComparison.Ordinal) ||
                root.GetProperty("answerability").ValueKind != JsonValueKind.String ||
                root.GetProperty("selections").ValueKind != JsonValueKind.Array ||
                packet.RequestId != request.RequestId ||
                packet.Mode != request.Mode ||
                packet.SystemId != request.SystemId ||
                !string.Equals(packet.Fingerprint, request.EvidenceFingerprint, StringComparison.Ordinal))
            {
                return ChatAgentResultValidation.Invalid();
            }

            string? answerability = root.GetProperty("answerability").GetString();
            int selectionCount = root.GetProperty("selections").GetArrayLength();
            if (string.Equals(answerability, "unsupported", StringComparison.Ordinal))
            {
                return selectionCount == 0 &&
                    outcome.Result?.Answerability == ChatAnswerability.Unsupported
                        ? ChatAgentResultValidation.Unsupported()
                        : ChatAgentResultValidation.Invalid();
            }

            if (!string.Equals(answerability, "supported", StringComparison.Ordinal) ||
                outcome.Result?.Answerability != ChatAnswerability.Supported)
            {
                return ChatAgentResultValidation.Invalid();
            }

            CandidateClaim[] retainedCandidates = _candidateExtractor.Extract(packet).ToArray();
            Dictionary<string, CandidateClaim> allowlist = retainedCandidates.ToDictionary(
                candidate => candidate.CandidateHandle,
                StringComparer.Ordinal);
            Dictionary<string, CandidateClaim> dispatchedCandidates = request.CandidateClaims.ToDictionary(
                candidate => candidate.CandidateHandle,
                StringComparer.Ordinal);
            var unmatchedTypedHandles = outcome.Result.Selections
                .Select(selection => selection.CandidateHandle)
                .ToHashSet(StringComparer.Ordinal);
            var selected = new List<CandidateClaim>();
            var claims = new List<MaterialClaim>();
            JsonElement.ArrayEnumerator selections = root.GetProperty("selections").EnumerateArray();
            int expectedOrder = 1;
            var selectedHandles = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement selection in selections)
            {
                bool hasNarrative = selection.TryGetProperty("narrativeText", out JsonElement narrativeElement);
                if (expectedOrder > 12 ||
                    !(HasExactProperties(selection, "candidateHandle", "order") ||
                        HasExactProperties(selection, "candidateHandle", "order", "narrativeText")) ||
                    selection.GetProperty("candidateHandle").ValueKind != JsonValueKind.String ||
                    selection.GetProperty("order").ValueKind != JsonValueKind.Number ||
                    (hasNarrative && narrativeElement.ValueKind is not (
                        JsonValueKind.String or JsonValueKind.Null)) ||
                    !selection.GetProperty("order").TryGetInt32(out int order) ||
                    order != expectedOrder)
                {
                    return ChatAgentResultValidation.Invalid();
                }

                string handle = selection.GetProperty("candidateHandle").GetString() ?? string.Empty;
                if (!selectedHandles.Add(handle) ||
                    !allowlist.TryGetValue(handle, out CandidateClaim? candidate) ||
                    !dispatchedCandidates.TryGetValue(handle, out CandidateClaim? dispatchedCandidate) ||
                    !HasExactRetainedState(candidate, dispatchedCandidate))
                {
                    return ChatAgentResultValidation.Invalid();
                }

                selected.Add(candidate);
                unmatchedTypedHandles.Remove(handle);
                string displayText = hasNarrative &&
                    candidate.AllowsBusinessNarrative &&
                    BusinessNarrativeSafety.IsSafeRewrite(
                        narrativeElement.GetString(),
                        candidate.Text)
                            ? narrativeElement.GetString()!
                            : candidate.Text;
                claims.Add(MaterialClaim.Create($"claim{order}", order, displayText, candidate.AnchorHandles));
                expectedOrder++;
            }

            if (selected.Count == 0 || unmatchedTypedHandles.Count > 0)
            {
                return ChatAgentResultValidation.Invalid();
            }

            return ChatAgentResultValidation.Valid(
                Array.AsReadOnly(claims.ToArray()),
                Array.AsReadOnly(selected.ToArray()));
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ChatContractException)
        {
            return ChatAgentResultValidation.Invalid();
        }
    }

    private static bool HasExactProperties(JsonElement element, params string[] expectedProperties)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var remaining = expectedProperties.ToHashSet(StringComparer.Ordinal);
        int count = 0;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            count++;
            if (!remaining.Remove(property.Name))
            {
                return false;
            }
        }

        return count == expectedProperties.Length && remaining.Count == 0;
    }

    private static bool HasExactRetainedState(CandidateClaim retained, CandidateClaim dispatched) =>
        string.Equals(retained.CandidateHandle, dispatched.CandidateHandle, StringComparison.Ordinal) &&
        string.Equals(retained.EvidenceFingerprint, dispatched.EvidenceFingerprint, StringComparison.Ordinal) &&
        retained.Mode == dispatched.Mode &&
        retained.DocumentVersionId == dispatched.DocumentVersionId &&
        retained.StableSectionId == dispatched.StableSectionId &&
        retained.SpanStart == dispatched.SpanStart &&
        retained.SpanLength == dispatched.SpanLength &&
        string.Equals(retained.Text, dispatched.Text, StringComparison.Ordinal) &&
        string.Equals(retained.TextSha256, dispatched.TextSha256, StringComparison.Ordinal) &&
        retained.AnchorHandles.SequenceEqual(dispatched.AnchorHandles, StringComparer.Ordinal) &&
        retained.RelationshipHandles.SequenceEqual(dispatched.RelationshipHandles, StringComparer.Ordinal) &&
        retained.AllowsBusinessNarrative == dispatched.AllowsBusinessNarrative &&
        string.Equals(retained.SupportSignature, dispatched.SupportSignature, StringComparison.Ordinal);

    private static bool IsCompatibleSchemaVersion(string? value) =>
        string.Equals(
            value,
            ChatSynthesisResult.SupportedSchemaVersion,
            StringComparison.Ordinal) ||
        string.Equals(
            value,
            ChatSynthesisResult.CompatibleProviderEchoSchemaVersion,
            StringComparison.Ordinal);

    private static string ToProviderMode(ChatMode mode) => mode switch
    {
        ChatMode.Business => "business",
        ChatMode.It => "it",
        _ => string.Empty,
    };
}

internal static class BusinessNarrativeSafety
{
    private const int _maximumLength = 500;

    public static bool IsSafeRewrite(string? narrative, string source)
    {
        if (string.IsNullOrWhiteSpace(narrative) ||
            narrative.Length > _maximumLength ||
            narrative.Contains('`') ||
            narrative.Contains('/') ||
            narrative.Contains('\\') ||
            narrative.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
            narrative.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
            narrative.Contains(" line ", StringComparison.OrdinalIgnoreCase) ||
            ContainsDottedIdentifier(narrative) ||
            ContainsUnknownAsciiOrNumericToken(narrative, source))
        {
            return false;
        }

        return !narrative.Any(character =>
            char.IsControl(character) && character is not ('\r' or '\n'));
    }

    private static bool ContainsDottedIdentifier(string value)
    {
        for (int index = 1; index < value.Length - 1; index++)
        {
            if (value[index] == '.' &&
                IsIdentifierPart(value[index - 1]) &&
                IsIdentifierStart(value[index + 1]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsUnknownAsciiOrNumericToken(string narrative, string source)
    {
        foreach (string token in ExtractAsciiOrNumericTokens(narrative))
        {
            if (!source.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> ExtractAsciiOrNumericTokens(string value)
    {
        int index = 0;
        while (index < value.Length)
        {
            if (!IsIdentifierStart(value[index]) && !char.IsAsciiDigit(value[index]))
            {
                index++;
                continue;
            }

            int start = index++;
            while (index < value.Length && IsIdentifierPart(value[index]))
            {
                index++;
            }

            yield return value[start..index];
        }
    }

    private static bool IsIdentifierPart(char value) =>
        IsIdentifierStart(value) || char.IsAsciiDigit(value);

    private static bool IsIdentifierStart(char value) =>
        char.IsAsciiLetter(value) || value == '_';
}
