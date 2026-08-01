using System.Text;
using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public static class PreparedPass2FlowLimits
{
    public const string SupportedSchemaIdentity = "vulgata.prepared-pass2-flow/1.0";
    public const int MaximumManifestUtf8Bytes = 32_768;
    public const int MaximumJsonDepth = 16;
    public const int MaximumMembers = 32;
    public const int MaximumTraceEvidence = 64;
    public const int MaximumEvidenceTextUtf8Bytes = 512;
}

public sealed record PreparedPass2FlowMember(
    int Order,
    Guid JobId,
    Guid CodeUnitId,
    Guid LogicalDocumentId);

public sealed record PreparedPass2TraceEvidence(
    int Order,
    Guid MemberJobId,
    Guid CodeUnitId,
    string Label,
    string Locator);

public sealed record PreparedPass2Flow
{
    private PreparedPass2Flow(
        string schemaIdentity,
        Guid manifestId,
        Guid flowId,
        Guid systemId,
        Guid repositoryId,
        IReadOnlyList<PreparedPass2FlowMember> members,
        IReadOnlyList<PreparedPass2TraceEvidence> traceEvidence)
    {
        SchemaIdentity = schemaIdentity;
        ManifestId = manifestId;
        FlowId = flowId;
        SystemId = systemId;
        RepositoryId = repositoryId;
        Members = members;
        TraceEvidence = traceEvidence;
    }

    public string SchemaIdentity { get; }
    public Guid ManifestId { get; }
    public Guid FlowId { get; }
    public Guid SystemId { get; }
    public Guid RepositoryId { get; }
    public IReadOnlyList<PreparedPass2FlowMember> Members { get; }
    public IReadOnlyList<PreparedPass2TraceEvidence> TraceEvidence { get; }

    public bool IsExactMatch(PreparedPass2Flow? other) =>
        other is not null &&
        StringComparer.Ordinal.Equals(SchemaIdentity, other.SchemaIdentity) &&
        ManifestId == other.ManifestId &&
        FlowId == other.FlowId &&
        SystemId == other.SystemId &&
        RepositoryId == other.RepositoryId &&
        Members.SequenceEqual(other.Members) &&
        TraceEvidence.SequenceEqual(other.TraceEvidence);

    public static PreparedPass2Flow Create(
        string schemaIdentity,
        Guid manifestId,
        Guid flowId,
        Guid systemId,
        Guid repositoryId,
        IReadOnlyList<PreparedPass2FlowMember> members,
        IReadOnlyList<PreparedPass2TraceEvidence> traceEvidence)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(traceEvidence);
        if (!StringComparer.Ordinal.Equals(
                schemaIdentity,
                PreparedPass2FlowLimits.SupportedSchemaIdentity) ||
            manifestId == Guid.Empty ||
            flowId == Guid.Empty ||
            systemId == Guid.Empty ||
            repositoryId == Guid.Empty ||
            members.Count is < 1 or > PreparedPass2FlowLimits.MaximumMembers ||
            traceEvidence.Count is < 1 or > PreparedPass2FlowLimits.MaximumTraceEvidence)
        {
            throw new PreparedPass2FlowContractException();
        }

        PreparedPass2FlowMember[] boundedMembers = members.ToArray();
        for (int index = 0; index < boundedMembers.Length; index++)
        {
            PreparedPass2FlowMember member = boundedMembers[index];
            if (member.Order != index + 1 ||
                member.JobId == Guid.Empty ||
                member.CodeUnitId == Guid.Empty ||
                member.LogicalDocumentId == Guid.Empty)
            {
                throw new PreparedPass2FlowContractException();
            }
        }

        if (HasDuplicates(boundedMembers.Select(member => member.JobId)) ||
            HasDuplicates(boundedMembers.Select(member => member.CodeUnitId)) ||
            HasDuplicates(boundedMembers.Select(member => member.LogicalDocumentId)))
        {
            throw new PreparedPass2FlowContractException();
        }

        Dictionary<Guid, PreparedPass2FlowMember> membersByJobId =
            boundedMembers.ToDictionary(member => member.JobId);
        PreparedPass2TraceEvidence[] boundedTraceEvidence = traceEvidence.ToArray();
        for (int index = 0; index < boundedTraceEvidence.Length; index++)
        {
            PreparedPass2TraceEvidence evidence = boundedTraceEvidence[index];
            if (evidence.Order != index + 1 ||
                !membersByJobId.TryGetValue(
                    evidence.MemberJobId,
                    out PreparedPass2FlowMember? member) ||
                member.CodeUnitId != evidence.CodeUnitId)
            {
                throw new PreparedPass2FlowContractException();
            }

            EnsureSafeEvidence(evidence.Label);
            EnsureSafeEvidence(evidence.Locator);
        }

        return new(
            schemaIdentity,
            manifestId,
            flowId,
            systemId,
            repositoryId,
            boundedMembers,
            boundedTraceEvidence);
    }

    private static bool HasDuplicates(IEnumerable<Guid> identifiers) =>
        identifiers.Distinct().Count() != identifiers.Count();

    private static void EnsureSafeEvidence(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            Encoding.UTF8.GetByteCount(value) >
                PreparedPass2FlowLimits.MaximumEvidenceTextUtf8Bytes ||
            value.Any(character => character <= '\u001f' ||
                character is >= '\u007f' and <= '\u009f') ||
            ContainsDisallowedCapability(value))
        {
            throw new PreparedPass2FlowContractException();
        }

        try
        {
            KnowledgeTextSafety.EnsureSafe(value);
        }
        catch (KnowledgeStoreContractException)
        {
            throw new PreparedPass2FlowContractException();
        }
    }

    private static bool ContainsDisallowedCapability(string value)
    {
        string[] markers =
        [
            "query",
            "select ",
            "match ",
            "traversal",
            "discover",
            "cluster",
            "rawsource",
            "raw source",
            "return ",
            "public class",
            "private class",
            "=>",
            "../",
            "..\\",
        ];
        return markers.Any(marker =>
            value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class PreparedPass2FlowContractException : Exception
{
    public PreparedPass2FlowContractException()
        : base("The prepared Pass 2 flow is invalid.")
    {
    }
}

public sealed record PreparedPass2CodeLogicCandidate(
    Guid JobId,
    Guid CodeUnitId,
    Guid SystemId,
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    Guid CommittedDocumentVersionId,
    CurrentKnowledge CurrentProjection);

public sealed record ResolvedPreparedPass2Flow(
    PreparedPass2Flow Flow,
    IReadOnlyList<CurrentKnowledge> CodeLogicDocuments);
