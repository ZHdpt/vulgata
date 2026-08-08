using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.ChatExecution;

public sealed class ChatAgentResultValidation
{
    private ChatAgentResultValidation(
        bool isValid,
        IReadOnlyList<MaterialClaim> materialClaims,
        IReadOnlyList<CandidateClaim> selectedCandidates)
    {
        IsValid = isValid;
        MaterialClaims = materialClaims;
        SelectedCandidates = selectedCandidates;
    }

    public bool IsValid { get; }

    public IReadOnlyList<MaterialClaim> MaterialClaims { get; }

    public IReadOnlyList<CandidateClaim> SelectedCandidates { get; }

    internal static ChatAgentResultValidation Valid(
        IReadOnlyList<MaterialClaim> materialClaims,
        IReadOnlyList<CandidateClaim> selectedCandidates) =>
        new(true, materialClaims, selectedCandidates);

    internal static ChatAgentResultValidation Invalid() =>
        new(false, [], []);
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
                    "selections") ||
                root.GetProperty("schemaVersion").ValueKind != JsonValueKind.String ||
                !string.Equals(
                    root.GetProperty("schemaVersion").GetString(),
                    ChatSynthesisResult.SupportedSchemaVersion,
                    StringComparison.Ordinal) ||
                root.GetProperty("requestId").ValueKind != JsonValueKind.String ||
                !string.Equals(root.GetProperty("requestId").GetString(), request.RequestId, StringComparison.Ordinal) ||
                root.GetProperty("mode").ValueKind != JsonValueKind.String ||
                !string.Equals(root.GetProperty("mode").GetString(), ToProviderMode(request.Mode), StringComparison.Ordinal) ||
                root.GetProperty("evidenceFingerprint").ValueKind != JsonValueKind.String ||
                !string.Equals(
                    root.GetProperty("evidenceFingerprint").GetString(),
                    request.EvidenceFingerprint,
                    StringComparison.Ordinal) ||
                root.GetProperty("selections").ValueKind != JsonValueKind.Array ||
                packet.RequestId != request.RequestId ||
                packet.Mode != request.Mode ||
                packet.SystemId != request.SystemId ||
                !string.Equals(packet.Fingerprint, request.EvidenceFingerprint, StringComparison.Ordinal))
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
            var selected = new List<CandidateClaim>();
            var claims = new List<MaterialClaim>();
            JsonElement.ArrayEnumerator selections = root.GetProperty("selections").EnumerateArray();
            int expectedOrder = 1;
            var selectedHandles = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement selection in selections)
            {
                if (expectedOrder > 12 ||
                    !HasExactProperties(selection, "candidateHandle", "order") ||
                    selection.GetProperty("candidateHandle").ValueKind != JsonValueKind.String ||
                    selection.GetProperty("order").ValueKind != JsonValueKind.Number ||
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
                claims.Add(MaterialClaim.Create($"claim{order}", order, candidate.Text, candidate.AnchorHandles));
                expectedOrder++;
            }

            if (selected.Count == 0)
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
        string.Equals(retained.SupportSignature, dispatched.SupportSignature, StringComparison.Ordinal);

    private static string ToProviderMode(ChatMode mode) => mode switch
    {
        ChatMode.Business => "business",
        ChatMode.It => "it",
        _ => string.Empty,
    };
}
