using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Sole Story 12.3 canonical identity codec. It preserves the accepted Story 12.2
/// CENV grammar without extending or changing the Story 12.2 domain registry.
/// </summary>
public static class PreparedPass1CanonicalCodec
{
    private const string _schemaIdentity = "code-logic/file-v2";
    private const string _uuidDomain = "vulgata/story-12.3/deterministic-uuid/1.0";
    private static readonly byte[] _magic = [0x56, 0x47, 0x43, 0x45, 0x01];
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private static readonly HashSet<string> _domains = new(StringComparer.Ordinal)
    {
        "vulgata/story-12.3/pass1-pair-execution/1.0",
        "vulgata/story-12.3/pass1-concurrency-change/1.0",
        "vulgata/story-12.3/code-logic-logical-document/1.0",
        "vulgata/story-12.3/code-logic-document-version/1.0",
        "vulgata/story-12.3/source-anchor/1.0",
        "vulgata/story-12.3/navigate-code-request/1.0",
        "vulgata/story-12.3/navigate-code-receipt/1.0",
        "vulgata/story-12.3/reference-observation/1.0",
        "vulgata/story-12.3/document-section/1.0",
        "vulgata/story-12.3/index-entry/1.0",
        "vulgata/story-12.3/references-edge/1.0",
        "vulgata/story-12.3/code-logic-commit/1.0",
        _uuidDomain,
    };

    public static PreparedPass1CanonicalArtifact CreatePairExecution(
        PreparedPass1PairIdentityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequireId(input.CoordinationId, nameof(input.CoordinationId));
        RequireId(input.SystemId, nameof(input.SystemId));
        RequireCount(input.ManifestBindings, 2, nameof(input.ManifestBindings));
        RequireCount(input.PromptIdentityBindings, 2, nameof(input.PromptIdentityBindings));
        RequireCount(input.AnalysisRuntimeIdentityHashBindings, 2, nameof(input.AnalysisRuntimeIdentityHashBindings));
        if (!string.Equals(input.DocumentSchemaIdentity, _schemaIdentity, StringComparison.Ordinal))
        {
            throw new ArgumentException("The authoritative Document schema identity is required.", nameof(input));
        }

        CanonicalValue[] bindings = input.ManifestBindings.Select(ManifestBinding).ToArray();
        return Identity(
            "vulgata/story-12.3/pass1-pair-execution/1.0",
            "Pass1PairExecution",
            F(1, U(input.CoordinationId)), F(2, U(input.SystemId)),
            F(3, H(input.PairPackageFingerprint)), F(4, H(input.PairSelectionFingerprint)),
            F(5, L(bindings)), F(6, L(input.PromptIdentityBindings.Select(S))),
            F(7, S(input.DocumentSchemaIdentity)),
            F(8, L(input.AnalysisRuntimeIdentityHashBindings.Select(H))));
    }

    public static PreparedPass1CanonicalArtifact CreateConcurrencyChange(
        PreparedPass1ConcurrencyChangeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequireId(input.PairExecutionId, nameof(input.PairExecutionId));
        if (input.PreviousLimit is < 1 or > 4 || input.EffectiveLimit is < 1 or > 4 ||
            input.PreviousLimit == input.EffectiveLimit ||
            input.ResultingStateVersion != input.ExpectedStateVersion + 1)
        {
            throw new ArgumentException("The versioned concurrency transition is invalid.", nameof(input));
        }

        return Identity("vulgata/story-12.3/pass1-concurrency-change/1.0", "Pass1ConcurrencyChange",
            F(1, U(input.PairExecutionId)), F(2, Q(input.ExpectedStateVersion)),
            F(3, Q(input.PreviousLimit)), F(4, Q(input.EffectiveLimit)),
            F(5, Q(input.ResultingStateVersion)), F(6, Q(input.ResultingConcurrencyRevision)),
            F(7, S(input.AuthorizedPrincipalIdentity)));
    }

    public static PreparedPass1CanonicalArtifact CreateLogicalDocument(
        PreparedPass1LogicalDocumentInput input) =>
        Identity("vulgata/story-12.3/code-logic-logical-document/1.0", "CodeLogicLogicalDocument",
            F(1, U(input.RepositoryId)), F(2, S(input.CanonicalPath)), F(3, Q(input.SegmentOrdinal)));

    public static PreparedPass1CanonicalArtifact CreateDocumentVersion(
        PreparedPass1DocumentVersionInput input)
    {
        if (!string.Equals(input.SchemaIdentity, _schemaIdentity, StringComparison.Ordinal))
        {
            throw new ArgumentException("The authoritative Document schema identity is required.", nameof(input));
        }

        return Identity("vulgata/story-12.3/code-logic-document-version/1.0", "CodeLogicDocumentVersion",
            F(1, U(input.ScanJobExecutionId)), F(2, H(input.FileCodeUnitIdentityFingerprint)),
            F(3, S(input.SchemaIdentity)));
    }

    public static PreparedPass1CanonicalArtifact CreateSourceAnchor(
        PreparedPass1SourceAnchorInput input) =>
        Identity("vulgata/story-12.3/source-anchor/1.0", "SourceAnchor",
            F(1, U(input.DocumentVersionId)), F(2, U(input.FileCodeUnitId)),
            F(3, S(input.CanonicalPath)), F(4, Q(input.ByteStart)), F(5, Q(input.ByteEnd)),
            F(6, Q(input.LineStart)), F(7, Q(input.LineEnd)), F(8, H(input.SourceHash)),
            F(9, S(input.EvidenceLocator)));

    public static PreparedPass1CanonicalArtifact CreateNavigationRequest(
        PreparedPass1NavigationRequestInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ToolAttemptOrdinal is < 1 or > 8 || input.RequestedDepth is < 1 or > 4)
        {
            throw new ArgumentException("Navigation attempt or depth is outside the accepted bound.", nameof(input));
        }

        return Digest("vulgata/story-12.3/navigate-code-request/1.0",
            F(1, U(input.JobId)), F(2, Q(input.ToolAttemptOrdinal)), F(3, E(input.Operation)),
            F(4, U(input.SourceFileCodeUnitId)), F(5, NavigationTarget(input.Target)),
            F(6, Q(input.RequestedDepth)), F(7, NullableHash(input.ParentObservationIdentity)));
    }

    public static PreparedPass1CanonicalArtifact CreateNavigationReceipt(
        PreparedPass1NavigationReceiptInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Identity("vulgata/story-12.3/navigate-code-receipt/1.0", "NavigationReceipt",
            F(1, NullableHash(input.RequestDigest)), F(2, U(input.JobId)), F(3, NullableQ(input.ReceiptOrdinal)),
            F(4, NullableQ(input.ToolAttemptOrdinal)), F(5, NullableE(input.Operation)),
            F(6, NullableQ(input.Depth)), F(7, E(input.Outcome)), F(8, NullableE(input.BoundKind)),
            F(9, L(input.Descriptors.Select(EvidenceDescriptor))), F(10, Q(input.ResultCount)),
            F(11, Q(input.ReturnedEvidenceByteCount)), F(12, Q(input.AcceptedCallCountAfter)),
            F(13, Q(input.EvidenceBytesAfter)));
    }

    public static PreparedPass1CanonicalArtifact CreateReferenceObservation(
        PreparedPass1ReferenceObservationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.NormalizedTargetSymbol is null && input.TargetCanonicalPath is null)
        {
            throw new ArgumentException("A target symbol or path is required.", nameof(input));
        }

        return Identity("vulgata/story-12.3/reference-observation/1.0", "ReferenceObservation",
            F(1, U(input.SourceDocumentVersionId)), F(2, U(input.SourceFileCodeUnitId)),
            F(3, E("References")), F(4, S(input.SourceClaimLocator)),
            F(5, NullableS(input.NormalizedTargetSymbol)), F(6, NullableS(input.TargetCanonicalPath)),
            F(7, NullableHash(input.TargetSourceHash)), F(8, NullableLineRange(input.TargetLineRange)));
    }

    public static PreparedPass1CanonicalArtifact CreateDocumentSection(
        Guid documentVersionId,
        PreparedPass1DocumentSectionGroup group) =>
        Identity("vulgata/story-12.3/document-section/1.0", "DocumentSection",
            F(1, U(documentVersionId)), F(2, E(group)));

    public static PreparedPass1CanonicalArtifact CreateIndexEntry(Guid documentVersionId) =>
        Identity("vulgata/story-12.3/index-entry/1.0", "IndexEntry",
            F(1, U(documentVersionId)), F(2, E("CodeLogic")));

    public static PreparedPass1CanonicalArtifact CreateReferencesEdge(
        Guid sourceDocumentVersionId,
        Guid targetDocumentVersionId) =>
        Identity("vulgata/story-12.3/references-edge/1.0", "ReferencesEdge",
            F(1, U(sourceDocumentVersionId)), F(2, U(targetDocumentVersionId)), F(3, E("References")));

    public static PreparedPass1CanonicalArtifact CreateCommit(PreparedPass1CommitInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Digest("vulgata/story-12.3/code-logic-commit/1.0",
            F(1, U(input.PairExecutionId)), F(2, U(input.JobId)), F(3, U(input.LogicalDocumentId)),
            F(4, U(input.DocumentVersionId)), F(5, H(input.ContentFingerprint)),
            F(6, L(input.SourceAnchorFingerprints.Select(H))),
            F(7, L(input.DirectReferenceTargetDocumentVersionIds.Select(U))),
            F(8, L(input.DocumentSectionProjectionFingerprints.Select(H))),
            F(9, H(input.IndexEntryProjectionFingerprint)),
            F(10, L(input.NavigationReceiptFingerprints.Select(H))),
            F(11, L(input.CommunicationClassificationFingerprints.Select(H))));
    }

    public static byte[] Encode(string domain, params CanonicalField[] fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        if (!_domains.Contains(domain))
        {
            throw new ArgumentException("Unknown Story 12.3 canonical domain.", nameof(domain));
        }

        using var stream = new MemoryStream();
        stream.Write(_magic);
        WriteValue(stream, S(domain));
        WriteValue(stream, CanonicalEnvelope.Object(fields));
        return stream.ToArray();
    }

    public static Guid DeriveUuid(string kind, ReadOnlySpan<byte> fingerprint)
    {
        RequireHash(fingerprint, nameof(fingerprint));
        byte[] digest = SHA256.HashData(Encode(_uuidDomain, F(1, E(kind)), F(2, CanonicalEnvelope.Bytes(fingerprint))));
        digest[6] = (byte)((digest[6] & 0x0f) | 0x80);
        digest[8] = (byte)((digest[8] & 0x3f) | 0x80);
        return new Guid(digest.AsSpan(0, 16), bigEndian: true);
    }

    private static PreparedPass1CanonicalArtifact Identity(string domain, string kind, params CanonicalField[] fields)
    {
        PreparedPass1CanonicalArtifact artifact = Digest(domain, fields);
        return artifact with { Id = DeriveUuid(kind, artifact.Fingerprint.Span) };
    }

    private static PreparedPass1CanonicalArtifact Digest(string domain, params CanonicalField[] fields)
    {
        byte[] envelope = Encode(domain, fields);
        return new(envelope, SHA256.HashData(envelope), null);
    }

    private static CanonicalValue ManifestBinding(PreparedPass1ManifestBinding input)
    {
        RequireRevision(input.ProfiledRevision.Span, nameof(input.ProfiledRevision));
        return CanonicalEnvelope.Object([
            F(1, U(input.RepositoryId)), F(2, U(input.ScanRunId)), F(3, U(input.ScanExecutionId)),
            F(4, U(input.ManifestId)), F(5, H(input.ManifestFingerprint)),
            F(6, CanonicalEnvelope.Bytes(input.ProfiledRevision.Span)),
            F(7, H(input.PreparedScanContextIdentityHash))]);
    }

    private static CanonicalValue NavigationTarget(PreparedPass1NavigationTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return CanonicalEnvelope.Object([
            F(1, NullableS(target.CanonicalPath)), F(2, NullableS(target.Symbol)),
            F(3, target.Position is null ? CanonicalEnvelope.Null() : CanonicalEnvelope.Object([
                F(1, Q(target.Position.Line)), F(2, Q(target.Position.Utf16Character))])),
            F(4, NullableLineRange(target.ExactLineRange)), F(5, NullableHash(target.ExpectedSourceHash))]);
    }

    private static CanonicalValue EvidenceDescriptor(PreparedPass1EvidenceDescriptor descriptor) => descriptor switch
    {
        PreparedPass1DocumentEvidenceDescriptor document => CanonicalEnvelope.Object([
            F(1, E("Document")), F(2, U(document.LogicalDocumentId)), F(3, U(document.DocumentVersionId)),
            F(4, S(document.Path)), F(5, H(document.TargetSourceHash)),
            F(6, L(document.SectionIds.Select(U))), F(7, L(document.SourceAnchorIds.Select(U)))]),
        PreparedPass1SourceEvidenceDescriptor source => CanonicalEnvelope.Object([
            F(1, E("Source")), F(2, S(source.Path)), F(3, NullableS(source.Symbol)),
            F(4, LineRange(source.LineRange)), F(5, ByteRange(source.ByteRange)),
            F(6, H(source.SourceHash)), F(7, S(source.EvidenceLocator))]),
        _ => throw new ArgumentException("Unknown evidence descriptor.", nameof(descriptor)),
    };

    private static CanonicalValue LineRange(PreparedPass1LineRange range) =>
        CanonicalEnvelope.Object([F(1, Q(range.Start)), F(2, Q(range.End))]);

    private static CanonicalValue ByteRange(PreparedPass1ByteRange range) =>
        CanonicalEnvelope.Object([F(1, Q(range.Start)), F(2, Q(range.End))]);

    private static CanonicalValue NullableLineRange(PreparedPass1LineRange? value) =>
        value is null ? CanonicalEnvelope.Null() : LineRange(value);

    private static CanonicalValue NullableHash(ReadOnlyMemory<byte>? value) =>
        value is null ? CanonicalEnvelope.Null() : H(value.Value);

    private static CanonicalValue NullableQ(ulong? value) =>
        value is null ? CanonicalEnvelope.Null() : Q(value.Value);

    private static CanonicalValue NullableS(string? value) =>
        value is null ? CanonicalEnvelope.Null() : S(value);

    private static CanonicalValue NullableE<T>(T? value) where T : struct, Enum =>
        value is null ? CanonicalEnvelope.Null() : E(value.Value);

    private static CanonicalField F(ushort tag, CanonicalValue value) => new(tag, value);
    private static CanonicalValue U(Guid value)
    {
        RequireId(value, nameof(value));
        return CanonicalEnvelope.Uuid(value);
    }

    private static CanonicalValue Q(ulong value) => CanonicalEnvelope.UInt64(value);
    private static CanonicalValue E<T>(T value) where T : Enum => E(value.ToString());
    private static CanonicalValue E(string value) => CanonicalEnvelope.Enum(value);
    private static CanonicalValue S(string value) => CanonicalEnvelope.String(RequireText(value).Normalize(NormalizationForm.FormC));
    private static CanonicalValue H(ReadOnlyMemory<byte> value)
    {
        RequireHash(value.Span, nameof(value));
        return CanonicalEnvelope.Bytes(value.Span);
    }

    private static CanonicalValue L(IEnumerable<CanonicalValue> values) => CanonicalEnvelope.List(values);

    private static void WriteValue(Stream stream, CanonicalValue value)
    {
        switch (value)
        {
            case CanonicalValue.NullCanonicalValue:
                stream.WriteByte(0);
                break;
            case CanonicalValue.UInt64CanonicalValue unsigned:
                stream.WriteByte(3);
                WriteUInt64(stream, unsigned.Value);
                break;
            case CanonicalValue.UuidCanonicalValue uuid:
                stream.WriteByte(4);
                Span<byte> uuidBytes = stackalloc byte[16];
                _ = uuid.Value.TryWriteBytes(uuidBytes, bigEndian: true, out _);
                stream.Write(uuidBytes);
                break;
            case CanonicalValue.EnumCanonicalValue member:
                WriteBytes(stream, 5, Encoding.ASCII.GetBytes(member.Value));
                break;
            case CanonicalValue.StringCanonicalValue text:
                WriteBytes(stream, 6, _strictUtf8.GetBytes(text.Value.Normalize(NormalizationForm.FormC)));
                break;
            case CanonicalValue.BytesCanonicalValue bytes:
                WriteBytes(stream, 7, bytes.Value);
                break;
            case CanonicalValue.ListCanonicalValue list:
                stream.WriteByte(8);
                WriteUInt32(stream, checked((uint)list.Value.Count));
                foreach (CanonicalValue item in list.Value)
                {
                    WriteValue(stream, item);
                }
                break;
            case CanonicalValue.ObjectCanonicalValue @object:
                stream.WriteByte(9);
                WriteUInt32(stream, checked((uint)@object.Value.Count));
                foreach (CanonicalField field in @object.Value)
                {
                    WriteUInt16(stream, field.Tag);
                    WriteValue(stream, field.Value);
                }
                break;
            default:
                throw new ArgumentException("Unsupported Story 12.3 canonical value.", nameof(value));
        }
    }

    private static void WriteBytes(Stream stream, byte type, ReadOnlySpan<byte> value)
    {
        stream.WriteByte(type);
        WriteUInt32(stream, checked((uint)value.Length));
        stream.Write(value);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt64(Stream stream, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void RequireId(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identity must be non-empty.", name);
        }
    }

    private static string RequireText(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _ = _strictUtf8.GetByteCount(value);
        return value;
    }

    private static void RequireHash(ReadOnlySpan<byte> value, string name)
    {
        if (value.Length != 32)
        {
            throw new ArgumentException("A SHA-256 fingerprint is required.", name);
        }
    }

    private static void RequireRevision(ReadOnlySpan<byte> value, string name)
    {
        if (value.Length is not (20 or 32))
        {
            throw new ArgumentException("A full Git object ID is required.", name);
        }
    }

    private static void RequireCount<T>(IReadOnlyList<T> values, int count, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        if (values.Count != count)
        {
            throw new ArgumentException($"Exactly {count} values are required.", name);
        }
    }
}
