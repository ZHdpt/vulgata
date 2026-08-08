using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// The canonical identity of one newly dispatched ordinary Pass 1 Worker job.
/// This is an operation identity, not a persisted entity or retry grant.
/// </summary>
public sealed class OrdinaryPass1JobIdentity
{
    private const string Magic = "VULGATA-ORDINARY-PASS1-JOB";

    private OrdinaryPass1JobIdentity(byte[] canonicalEnvelope, byte[] digest, Guid jobId)
    {
        CanonicalEnvelope = canonicalEnvelope;
        Digest = digest;
        JobId = jobId;
    }

    public ReadOnlyMemory<byte> CanonicalEnvelope { get; }

    public ReadOnlyMemory<byte> Digest { get; }

    public Guid JobId { get; }

    public static Guid CreateDocumentVersionId(Guid jobId)
    {
        EnsureIdentifier(jobId, nameof(jobId));
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            '\n',
            "pass1-document-version",
            jobId.ToString("D"))));
        return new Guid(hash.AsSpan(0, 16));
    }

    public static OrdinaryPass1JobIdentity Create(
        Guid scanExecutionId,
        Guid scanRunId,
        Guid repositoryId,
        Guid codeUnitId,
        string profiledRevision)
    {
        EnsureIdentifier(scanExecutionId, nameof(scanExecutionId));
        EnsureIdentifier(scanRunId, nameof(scanRunId));
        EnsureIdentifier(repositoryId, nameof(repositoryId));
        EnsureIdentifier(codeUnitId, nameof(codeUnitId));
        byte[] revision = DecodeProfiledRevision(profiledRevision);

        using var stream = new MemoryStream(142);
        stream.Write(Encoding.ASCII.GetBytes(Magic));
        stream.WriteByte(0);
        stream.WriteByte(1);
        WriteField(stream, 1, scanExecutionId.ToByteArray(bigEndian: true));
        WriteField(stream, 2, scanRunId.ToByteArray(bigEndian: true));
        WriteField(stream, 3, repositoryId.ToByteArray(bigEndian: true));
        WriteField(stream, 4, codeUnitId.ToByteArray(bigEndian: true));
        WriteField(stream, 5, revision);

        byte[] envelope = stream.ToArray();
        byte[] digest = SHA256.HashData(envelope);
        byte[] uuid = digest[..16];
        uuid[6] = (byte)((uuid[6] & 0x0f) | 0x80);
        uuid[8] = (byte)((uuid[8] & 0x3f) | 0x80);
        return new(envelope, digest, new Guid(uuid, bigEndian: true));
    }

    private static void WriteField(Stream stream, ushort tag, ReadOnlySpan<byte> value)
    {
        Span<byte> header = stackalloc byte[6];
        BinaryPrimitives.WriteUInt16BigEndian(header, tag);
        BinaryPrimitives.WriteUInt32BigEndian(header[2..], checked((uint)value.Length));
        stream.Write(header);
        stream.Write(value);
    }

    private static byte[] DecodeProfiledRevision(string value)
    {
        if (value is not { Length: 40 or 64 } ||
            value.Any(character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "The Profiled Revision must be a full lowercase Git object ID.",
                nameof(value));
        }

        return Convert.FromHexString(value);
    }

    private static void EnsureIdentifier(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("The identity must be non-empty.", parameterName);
        }
    }
}

public sealed record PreparedPass2RuntimeRepositoryBinding(
    string Alias,
    string StableGitIdentity,
    Guid RepositoryId,
    Guid ScanRunId,
    Guid ScanExecutionId,
    string ExpectedRef,
    string FullCommit,
    string ProfiledRevision);

public sealed record PreparedPass2RuntimeMember(
    int Order,
    Guid JobId,
    Guid CodeUnitId,
    Guid LogicalDocumentId,
    Guid RepositoryId,
    Guid DocumentVersionId,
    IReadOnlyList<Guid> SourceAnchorIds);

/// <summary>
/// Server-owned binding between the accepted exact-resolution profile and the
/// current product-created evidence used for one prepared Pass 2 dispatch.
/// </summary>
public sealed class PreparedPass2RuntimeBinding
{
    private const string Direction = "consumer-to-provider";

    private PreparedPass2RuntimeBinding(
        PreparedPass2Flow flow,
        string profileSchemaVersion,
        string profileId,
        string profileVersion,
        string profileFingerprint,
        string occurrenceId,
        PreparedPass2RuntimeRepositoryBinding consumer,
        PreparedPass2RuntimeRepositoryBinding provider,
        Guid relationshipId,
        IReadOnlyList<Guid> relationshipEvidenceIds,
        IReadOnlyList<PreparedPass2RuntimeMember> members,
        string supportFingerprint,
        byte[] manifestEnvelope,
        byte[] flowEnvelope,
        byte[] supportEnvelope)
    {
        Flow = flow;
        ProfileSchemaVersion = profileSchemaVersion;
        ProfileId = profileId;
        ProfileVersion = profileVersion;
        ProfileFingerprint = profileFingerprint;
        OccurrenceId = occurrenceId;
        Consumer = consumer;
        Provider = provider;
        RelationshipId = relationshipId;
        RelationshipEvidenceIds = relationshipEvidenceIds;
        Members = members;
        SupportingRepositoryIds = Array.AsReadOnly(
            new[] { consumer.RepositoryId, provider.RepositoryId });
        DocumentVersionIds = Array.AsReadOnly(
            members.Select(member => member.DocumentVersionId).ToArray());
        SupportFingerprint = supportFingerprint;
        ManifestEnvelope = manifestEnvelope;
        FlowEnvelope = flowEnvelope;
        SupportEnvelope = supportEnvelope;
    }

    public PreparedPass2Flow Flow { get; }
    public string ProfileSchemaVersion { get; }
    public string ProfileId { get; }
    public string ProfileVersion { get; }
    public string ProfileFingerprint { get; }
    public string OccurrenceId { get; }
    public PreparedPass2RuntimeRepositoryBinding Consumer { get; }
    public PreparedPass2RuntimeRepositoryBinding Provider { get; }
    public Guid RelationshipId { get; }
    public IReadOnlyList<Guid> RelationshipEvidenceIds { get; }
    public IReadOnlyList<PreparedPass2RuntimeMember> Members { get; }
    public IReadOnlyList<Guid> SupportingRepositoryIds { get; }
    public IReadOnlyList<Guid> DocumentVersionIds { get; }
    public string SupportFingerprint { get; }
    public ReadOnlyMemory<byte> ManifestEnvelope { get; }
    public ReadOnlyMemory<byte> FlowEnvelope { get; }
    public ReadOnlyMemory<byte> SupportEnvelope { get; }

    public static PreparedPass2RuntimeBinding Create(
        string profileSchemaVersion,
        string profileId,
        string profileVersion,
        string profileFingerprint,
        string occurrenceId,
        Guid systemId,
        PreparedPass2RuntimeRepositoryBinding consumer,
        PreparedPass2RuntimeRepositoryBinding provider,
        Guid relationshipId,
        IReadOnlyList<Guid> relationshipEvidenceIds,
        IReadOnlyList<PreparedPass2RuntimeMember> members,
        IReadOnlyList<PreparedPass2TraceEvidence> traceEvidence)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(relationshipEvidenceIds);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(traceEvidence);
        if (!StringComparer.Ordinal.Equals(profileSchemaVersion, "1.0") ||
            systemId == Guid.Empty || relationshipId == Guid.Empty ||
            consumer.RepositoryId == provider.RepositoryId ||
            StringComparer.Ordinal.Equals(consumer.Alias, provider.Alias))
        {
            throw new PreparedPass2FlowContractException();
        }

        PreparedPass2CanonicalCodec.RequireText(profileId);
        PreparedPass2CanonicalCodec.RequireText(profileVersion);
        PreparedPass2CanonicalCodec.RequireText(occurrenceId);
        _ = PreparedPass2CanonicalCodec.DecodeHex(profileFingerprint, 32);
        ValidateRepositoryBinding(consumer);
        ValidateRepositoryBinding(provider);

        Guid[] evidenceIds = PreparedPass2CanonicalCodec.RequireSortedIds(
            relationshipEvidenceIds,
            requireNonEmpty: true);
        PreparedPass2RuntimeMember[] boundedMembers = members.ToArray();
        if (boundedMembers.Length is < 2 or > PreparedPass2FlowLimits.MaximumMembers ||
            traceEvidence.Count is < 1 or > PreparedPass2FlowLimits.MaximumTraceEvidence)
        {
            throw new PreparedPass2FlowContractException();
        }

        HashSet<Guid> repositoryIds = [consumer.RepositoryId, provider.RepositoryId];
        for (int index = 0; index < boundedMembers.Length; index++)
        {
            PreparedPass2RuntimeMember member = boundedMembers[index];
            if (member.Order != index + 1 ||
                member.JobId == Guid.Empty || member.CodeUnitId == Guid.Empty ||
                member.LogicalDocumentId == Guid.Empty ||
                member.DocumentVersionId == Guid.Empty ||
                !repositoryIds.Contains(member.RepositoryId))
            {
                throw new PreparedPass2FlowContractException();
            }

            _ = PreparedPass2CanonicalCodec.RequireSortedIds(
                member.SourceAnchorIds,
                requireNonEmpty: true);
        }

        if (boundedMembers.Select(member => member.JobId).Distinct().Count() != boundedMembers.Length ||
            boundedMembers.Select(member => member.CodeUnitId).Distinct().Count() != boundedMembers.Length ||
            boundedMembers.Select(member => member.LogicalDocumentId).Distinct().Count() != boundedMembers.Length ||
            boundedMembers.Select(member => member.DocumentVersionId).Distinct().Count() != boundedMembers.Length ||
            !repositoryIds.SetEquals(boundedMembers.Select(member => member.RepositoryId)))
        {
            throw new PreparedPass2FlowContractException();
        }

        byte[] manifestEnvelope = PreparedPass2CanonicalCodec.Envelope(
            "VULGATA-PREPARED-PASS2-MANIFEST",
            PreparedPass2CanonicalCodec.TextField(1, PreparedPass2FlowLimits.SupportedSchemaIdentity),
            PreparedPass2CanonicalCodec.TextField(2, profileId),
            PreparedPass2CanonicalCodec.TextField(3, profileVersion),
            PreparedPass2CanonicalCodec.HexField(4, profileFingerprint, 32));
        Guid manifestId = PreparedPass2CanonicalCodec.Uuid8Sha256(manifestEnvelope);
        byte[] consumerRecord = PreparedPass2CanonicalCodec.RepositoryRecord(consumer);
        byte[] providerRecord = PreparedPass2CanonicalCodec.RepositoryRecord(provider);
        byte[] flowEnvelope = PreparedPass2CanonicalCodec.Envelope(
            "VULGATA-PREPARED-PASS2-FLOW",
            PreparedPass2CanonicalCodec.UuidField(1, manifestId),
            PreparedPass2CanonicalCodec.TextField(2, occurrenceId),
            PreparedPass2CanonicalCodec.TextField(3, Direction),
            PreparedPass2CanonicalCodec.UuidField(4, systemId),
            PreparedPass2CanonicalCodec.Field(5, consumerRecord),
            PreparedPass2CanonicalCodec.Field(6, providerRecord),
            PreparedPass2CanonicalCodec.UuidField(7, relationshipId));
        Guid flowId = PreparedPass2CanonicalCodec.Uuid8Sha256(flowEnvelope);

        PreparedPass2Flow flow = PreparedPass2Flow.Create(
            PreparedPass2FlowLimits.SupportedSchemaIdentity,
            manifestId,
            flowId,
            systemId,
            consumer.RepositoryId,
            boundedMembers.Select(member => new PreparedPass2FlowMember(
                member.Order,
                member.JobId,
                member.CodeUnitId,
                member.LogicalDocumentId)).ToArray(),
            traceEvidence);
        byte[] supportEnvelope = PreparedPass2CanonicalCodec.Envelope(
            "VULGATA-PREPARED-PASS2-SUPPORT",
            PreparedPass2CanonicalCodec.TextField(1, flow.SchemaIdentity),
            PreparedPass2CanonicalCodec.UuidField(2, manifestId),
            PreparedPass2CanonicalCodec.UuidField(3, flowId),
            PreparedPass2CanonicalCodec.TextField(4, profileId),
            PreparedPass2CanonicalCodec.TextField(5, profileVersion),
            PreparedPass2CanonicalCodec.HexField(6, profileFingerprint, 32),
            PreparedPass2CanonicalCodec.TextField(7, occurrenceId),
            PreparedPass2CanonicalCodec.TextField(8, Direction),
            PreparedPass2CanonicalCodec.UuidField(9, systemId),
            PreparedPass2CanonicalCodec.Field(10, consumerRecord),
            PreparedPass2CanonicalCodec.Field(11, providerRecord),
            PreparedPass2CanonicalCodec.UuidField(12, relationshipId),
            PreparedPass2CanonicalCodec.Field(13, PreparedPass2CanonicalCodec.UuidList(evidenceIds)),
            PreparedPass2CanonicalCodec.Field(14, PreparedPass2CanonicalCodec.MemberList(boundedMembers)),
            PreparedPass2CanonicalCodec.Field(15, PreparedPass2CanonicalCodec.TraceList(traceEvidence)));
        string supportFingerprint = Convert.ToHexStringLower(
            SHA256.HashData(supportEnvelope));
        return new(
            flow,
            profileSchemaVersion,
            profileId,
            profileVersion,
            profileFingerprint,
            occurrenceId,
            consumer,
            provider,
            relationshipId,
            Array.AsReadOnly(evidenceIds),
            Array.AsReadOnly(boundedMembers),
            supportFingerprint,
            manifestEnvelope,
            flowEnvelope,
            supportEnvelope);
    }

    public bool IsCanonical()
    {
        try
        {
            PreparedPass2RuntimeBinding recomputed = Create(
                ProfileSchemaVersion,
                ProfileId,
                ProfileVersion,
                ProfileFingerprint,
                OccurrenceId,
                Flow.SystemId,
                Consumer,
                Provider,
                RelationshipId,
                RelationshipEvidenceIds,
                Members,
                Flow.TraceEvidence);
            return Flow.IsExactMatch(recomputed.Flow) &&
                CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(SupportFingerprint),
                    Convert.FromHexString(recomputed.SupportFingerprint));
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException or
                PreparedPass2FlowContractException)
        {
            return false;
        }
    }

    private static void ValidateRepositoryBinding(
        PreparedPass2RuntimeRepositoryBinding binding)
    {
        PreparedPass2CanonicalCodec.RequireText(binding.Alias);
        PreparedPass2CanonicalCodec.RequireText(binding.StableGitIdentity);
        PreparedPass2CanonicalCodec.RequireText(binding.ExpectedRef);
        if (binding.RepositoryId == Guid.Empty || binding.ScanRunId == Guid.Empty ||
            binding.ScanExecutionId == Guid.Empty)
        {
            throw new PreparedPass2FlowContractException();
        }

        _ = PreparedPass2CanonicalCodec.DecodeGitObjectId(binding.FullCommit);
        _ = PreparedPass2CanonicalCodec.DecodeGitObjectId(binding.ProfiledRevision);
    }
}

internal static class PreparedPass2CanonicalCodec
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    public static byte[] Envelope(string magic, params byte[][] fields)
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes(magic));
        stream.WriteByte(0);
        stream.WriteByte(1);
        foreach (byte[] field in fields)
        {
            stream.Write(field);
        }

        return stream.ToArray();
    }

    public static byte[] Field(ushort tag, ReadOnlySpan<byte> value)
    {
        byte[] field = new byte[6 + value.Length];
        BinaryPrimitives.WriteUInt16BigEndian(field, tag);
        BinaryPrimitives.WriteUInt32BigEndian(field.AsSpan(2), checked((uint)value.Length));
        value.CopyTo(field.AsSpan(6));
        return field;
    }

    public static byte[] TextField(ushort tag, string value) =>
        Field(tag, Text(value));

    public static byte[] UuidField(ushort tag, Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new PreparedPass2FlowContractException();
        }

        return Field(tag, value.ToByteArray(bigEndian: true));
    }

    public static byte[] HexField(ushort tag, string value, int expectedBytes) =>
        Field(tag, DecodeHex(value, expectedBytes));

    public static byte[] RepositoryRecord(PreparedPass2RuntimeRepositoryBinding value) =>
        Record(
            TextField(1, value.Alias),
            TextField(2, value.StableGitIdentity),
            UuidField(3, value.RepositoryId),
            UuidField(4, value.ScanRunId),
            UuidField(5, value.ScanExecutionId),
            TextField(6, value.ExpectedRef),
            Field(7, DecodeGitObjectId(value.FullCommit)),
            Field(8, DecodeGitObjectId(value.ProfiledRevision)));

    public static byte[] MemberList(IReadOnlyList<PreparedPass2RuntimeMember> values) =>
        List(values.Select(value => Record(
            Field(1, UInt32(value.Order)),
            UuidField(2, value.JobId),
            UuidField(3, value.CodeUnitId),
            UuidField(4, value.LogicalDocumentId),
            UuidField(5, value.RepositoryId),
            UuidField(6, value.DocumentVersionId),
            Field(7, UuidList(RequireSortedIds(value.SourceAnchorIds, true))))));

    public static byte[] TraceList(IReadOnlyList<PreparedPass2TraceEvidence> values) =>
        List(values.Select(value => Record(
            Field(1, UInt32(value.Order)),
            UuidField(2, value.MemberJobId),
            UuidField(3, value.CodeUnitId),
            TextField(4, value.Label),
            TextField(5, value.Locator))));

    public static byte[] UuidList(IReadOnlyList<Guid> values) =>
        List(values.Select(value => value.ToByteArray(bigEndian: true)));

    public static Guid[] RequireSortedIds(
        IReadOnlyList<Guid> values,
        bool requireNonEmpty)
    {
        ArgumentNullException.ThrowIfNull(values);
        Guid[] ids = values.ToArray();
        Guid[] sorted = ids.OrderBy(
            value => Convert.ToHexString(value.ToByteArray(bigEndian: true)),
            StringComparer.Ordinal).ToArray();
        if (requireNonEmpty && ids.Length == 0 ||
            ids.Any(id => id == Guid.Empty) ||
            ids.Distinct().Count() != ids.Length ||
            !ids.SequenceEqual(sorted))
        {
            throw new PreparedPass2FlowContractException();
        }

        return ids;
    }

    public static Guid Uuid8Sha256(ReadOnlySpan<byte> envelope)
    {
        byte[] bytes = SHA256.HashData(envelope)[..16];
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    public static byte[] DecodeHex(string value, int expectedBytes)
    {
        if (value is null || value.Length != expectedBytes * 2 ||
            value.Any(character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new PreparedPass2FlowContractException();
        }

        return Convert.FromHexString(value);
    }

    public static byte[] DecodeGitObjectId(string value)
    {
        if (value is not { Length: 40 or 64 })
        {
            throw new PreparedPass2FlowContractException();
        }

        return DecodeHex(value, value.Length / 2);
    }

    public static void RequireText(string value) => _ = Text(value);

    private static byte[] Text(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.IsNormalized(NormalizationForm.FormC) ||
            value.Any(character =>
                character <= '\u001f' || character is >= '\u007f' and <= '\u009f'))
        {
            throw new PreparedPass2FlowContractException();
        }

        try
        {
            return _strictUtf8.GetBytes(value);
        }
        catch (EncoderFallbackException)
        {
            throw new PreparedPass2FlowContractException();
        }
    }

    private static byte[] Record(params byte[][] fields)
    {
        using var stream = new MemoryStream();
        foreach (byte[] field in fields)
        {
            stream.Write(field);
        }

        return stream.ToArray();
    }

    private static byte[] List(IEnumerable<byte[]> items)
    {
        byte[][] values = items.ToArray();
        using var stream = new MemoryStream();
        stream.Write(UInt32(values.Length));
        foreach (byte[] value in values)
        {
            stream.Write(UInt32(value.Length));
            stream.Write(value);
        }

        return stream.ToArray();
    }

    private static byte[] UInt32(int value)
    {
        if (value < 0)
        {
            throw new PreparedPass2FlowContractException();
        }

        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, checked((uint)value));
        return bytes;
    }
}
