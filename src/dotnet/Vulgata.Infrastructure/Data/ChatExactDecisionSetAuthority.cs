using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

internal static class ChatExactDecisionSetAuthority
{
    private const string _schemaIdentity = "vulgata.chat-exact-decision-set/1";

    internal static bool TryCreate(
        DocumentEdge edge,
        IReadOnlyCollection<CrossRepositoryResolutionDecision> decisions,
        IReadOnlyCollection<ExactResolutionProviderEvidenceContribution> contributions,
        out ChatExactDecisionSet decisionSet)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(contributions);

        decisionSet = ChatExactDecisionSet.Empty;
        if (edge.EdgeType != DocumentEdgeType.References ||
            edge.SourceRepositoryId == edge.TargetRepositoryId ||
            decisions.Count == 0)
        {
            return false;
        }

        CrossRepositoryResolutionDecision[] ordered = decisions
            .OrderBy(decision => decision, DecisionComparer.Instance)
            .ToArray();
        if (ordered.Select(decision => decision.Id).Distinct().Count() != ordered.Length ||
            ordered.GroupBy(DecisionSlotKey.Create).Any(group => group.Count() != 1))
        {
            return false;
        }

        foreach (CrossRepositoryResolutionDecision decision in ordered)
        {
            ExactResolutionProviderEvidenceContribution[] ownedContributions = contributions
                .Where(contribution => contribution.ResolutionDecisionId == decision.Id)
                .OrderBy(contribution => contribution.ProviderOrdinal)
                .ThenBy(contribution => contribution.EvidenceOrdinal)
                .ToArray();
            if (decision.DocumentEdgeId != edge.Id ||
                !ExactResolutionCanonicalAuthority.MatchesPersistedExactAuthority(
                    decision,
                    edge,
                    ownedContributions))
            {
                return false;
            }
        }

        if (contributions.Any(contribution =>
                ordered.All(decision => decision.Id != contribution.ResolutionDecisionId)))
        {
            return false;
        }

        decisionSet = new ChatExactDecisionSet(
            Array.AsReadOnly(ordered),
            ordered[0],
            ordered.Length,
            ComputeFingerprint(edge.Id, ordered));
        return true;
    }

    private static string ComputeFingerprint(
        Guid edgeId,
        IReadOnlyCollection<CrossRepositoryResolutionDecision> decisions)
    {
        using var stream = new MemoryStream();
        WriteString(stream, _schemaIdentity);
        WriteGuid(stream, edgeId);
        foreach (CrossRepositoryResolutionDecision decision in decisions)
        {
            WriteGuid(stream, decision.Id);
            WriteString(stream, decision.ResolutionSchemaIdentity);
            WriteGuid(stream, decision.Pass1PairExecutionId);
            WriteGuid(stream, decision.CoordinationId);
            WriteGuid(stream, decision.SystemId);
            WriteGuid(stream, decision.ConsumerCandidateId);
            WriteString(stream, decision.ConsumerEntryIdentity);
            WriteString(stream, decision.ConsumerEntryStableKey);
            WriteGuid(stream, decision.ConsumerDocumentVersionId);
            WriteGuid(stream, decision.ConsumerRepositoryId);
            WriteGuid(stream, decision.ConsumerScanRunId);
            WriteString(stream, decision.ConsumerProfiledRevision);
            WriteInt32(stream, (int)decision.Protocol);
            WriteString(stream, decision.NormalizedEndpointIdentifier);
            WriteString(stream, decision.NormalizationRuleIdentity);
            WriteString(stream, decision.Outcome);
            WriteString(stream, decision.OutcomeKind);
            WriteString(stream, decision.ReasonCode);
            WriteNullableGuid(stream, decision.ProviderIdentity);
            WriteNullableGuid(stream, decision.ProviderDocumentVersionId);
            WriteNullableGuid(stream, decision.ProviderRepositoryId);
            WriteNullableGuid(stream, decision.ProviderScanRunId);
            WriteNullableString(stream, decision.ProviderProfiledRevision);
            WriteNullableGuid(stream, decision.DocumentEdgeId);
            WriteNullableGuid(stream, decision.UncertaintyId);
            WriteNullableGuid(stream, decision.ResolvedUncertaintyId);
            WriteString(stream, decision.ConsumerEvidenceFingerprint);
            WriteString(stream, decision.ProviderEvidenceSetFingerprint);
            WriteString(stream, decision.DecisionFingerprint);
            WriteString(stream, decision.EvidenceFingerprint);
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteGuid(Stream stream, Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes, bigEndian: true, out _);
        WritePresentValue(stream, bytes);
    }

    private static void WriteNullableGuid(Stream stream, Guid? value)
    {
        if (!value.HasValue)
        {
            stream.WriteByte(0);
            return;
        }

        WriteGuid(stream, value.Value);
    }

    private static void WriteString(Stream stream, string value) =>
        WritePresentValue(stream, Encoding.UTF8.GetBytes(value));

    private static void WriteNullableString(Stream stream, string? value)
    {
        if (value is null)
        {
            stream.WriteByte(0);
            return;
        }

        WriteString(stream, value);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        WritePresentValue(stream, bytes);
    }

    private static void WritePresentValue(Stream stream, ReadOnlySpan<byte> value)
    {
        stream.WriteByte(1);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        stream.Write(length);
        stream.Write(value);
    }

    private sealed record DecisionSlotKey(
        Guid Pass1PairExecutionId,
        Guid ConsumerCandidateId,
        string ConsumerEntryStableKey,
        int Protocol,
        string NormalizedEndpointIdentifier)
    {
        internal static DecisionSlotKey Create(CrossRepositoryResolutionDecision decision) => new(
            decision.Pass1PairExecutionId,
            decision.ConsumerCandidateId,
            decision.ConsumerEntryStableKey,
            (int)decision.Protocol,
            decision.NormalizedEndpointIdentifier);
    }

    private sealed class DecisionComparer : IComparer<CrossRepositoryResolutionDecision>
    {
        internal static DecisionComparer Instance { get; } = new();

        public int Compare(
            CrossRepositoryResolutionDecision? left,
            CrossRepositoryResolutionDecision? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            int comparison = CompareGuid(left.Pass1PairExecutionId, right.Pass1PairExecutionId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = ((int)left.Protocol).CompareTo((int)right.Protocol);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(
                left.ConsumerEntryStableKey,
                right.ConsumerEntryStableKey);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(
                left.NormalizedEndpointIdentifier,
                right.NormalizedEndpointIdentifier);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareGuid(left.ConsumerCandidateId, right.ConsumerCandidateId);
            return comparison != 0 ? comparison : CompareGuid(left.Id, right.Id);
        }

        private static int CompareGuid(Guid left, Guid right)
        {
            Span<byte> leftBytes = stackalloc byte[16];
            Span<byte> rightBytes = stackalloc byte[16];
            left.TryWriteBytes(leftBytes, bigEndian: true, out _);
            right.TryWriteBytes(rightBytes, bigEndian: true, out _);
            return leftBytes.SequenceCompareTo(rightBytes);
        }
    }
}

internal sealed record ChatExactDecisionSet(
    IReadOnlyList<CrossRepositoryResolutionDecision> Decisions,
    CrossRepositoryResolutionDecision SelectedDecision,
    int Count,
    string Fingerprint)
{
    internal static ChatExactDecisionSet Empty { get; } = new(
        [],
        null!,
        0,
        string.Empty);
}
