using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public sealed record CodeLogicFileV2Projection(
    string ContentJson,
    string ContentMarkdown,
    ReadOnlyMemory<byte> ContentFingerprint,
    IReadOnlyList<CodeLogicFileV2SourceAnchorProjection> SourceAnchors,
    IReadOnlyList<CodeLogicFileV2SectionProjection> Sections,
    CodeLogicFileV2IndexEntryProjection IndexEntry,
    IReadOnlyList<CodeLogicFileV2ReferenceEdgeProjection> DirectReferenceEdges,
    ReadOnlyMemory<byte> CommitFingerprint);

public sealed record CodeLogicFileV2SourceAnchorProjection(
    Guid Id,
    string Key,
    Guid FileCodeUnitId,
    string RepositoryRelativePath,
    ulong ByteStart,
    ulong ByteEnd,
    ulong LineStart,
    ulong LineEnd,
    ReadOnlyMemory<byte> SourceHash,
    string EvidenceLocator,
    ReadOnlyMemory<byte> Fingerprint);

public sealed record CodeLogicFileV2SectionProjection(
    Guid StableSectionId,
    int SectionOrder,
    DocumentSectionKind SectionKind,
    string Heading,
    string Markdown,
    string SearchText,
    IReadOnlyList<Guid> SourceAnchorIds,
    IReadOnlyList<Guid> AuthorizedDocumentVersionIds,
    ReadOnlyMemory<byte> ProjectionFingerprint);

public sealed record CodeLogicFileV2IndexEntryProjection(
    Guid Id,
    string Title,
    string Summary,
    IReadOnlyList<string> KeySymbols,
    IReadOnlyList<string> BusinessTerms,
    IReadOnlyList<string> TechnicalTerms,
    string SearchText,
    ReadOnlyMemory<byte> ProjectionFingerprint);

public sealed record CodeLogicFileV2ReferenceEdgeProjection(
    Guid Id,
    DocumentEdgeType EdgeType,
    Guid SourceDocumentVersionId,
    Guid TargetDocumentVersionId,
    IReadOnlyList<Guid> SourceAnchorIds,
    string SourceClaimLocator,
    ReadOnlyMemory<byte> Fingerprint);

/// <summary>
/// Pure projection of validated authoritative code-logic/file-v2 JSON. The
/// renderer has no provider or completion-order input and never adds prose.
/// </summary>
public static partial class CodeLogicFileV2Projector
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    /// <summary>
    /// Compares a persisted JSON array by scalar value, order, and
    /// cardinality without depending on storage-normalized whitespace.
    /// </summary>
    public static bool MatchesOrderedJsonArray<T>(
        string persistedJson,
        IReadOnlyList<T> expected)
    {
        ArgumentNullException.ThrowIfNull(persistedJson);
        ArgumentNullException.ThrowIfNull(expected);
        try
        {
            T[]? actual = JsonSerializer.Deserialize<T[]>(persistedJson);
            return actual is not null && actual.SequenceEqual(expected);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static CodeLogicFileV2Projection Project(
        string authoritativeJson,
        Guid pairExecutionId,
        Guid jobId,
        Guid logicalDocumentId,
        Guid documentVersionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authoritativeJson);
        using JsonDocument document = JsonDocument.Parse(authoritativeJson);
        JsonElement root = document.RootElement;
        if (!string.Equals(Text(root, "schemaIdentity"), "code-logic/file-v2", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The authoritative code-logic/file-v2 schema is required.");
        }

        JsonElement binding = Property(root, "binding");
        string path = Text(binding, "repositoryRelativePath");
        Guid fileCodeUnitId = GuidValue(binding, "fileCodeUnitId");
        ulong segmentOrdinal = UInt64(binding, "segmentOrdinal");
        ulong segmentCount = UInt64(binding, "segmentCount");
        byte[] contentBytes = _strictUtf8.GetBytes(authoritativeJson);
        byte[] contentFingerprint = SHA256.HashData(contentBytes);

        CodeLogicFileV2SourceAnchorProjection[] anchors = Array(root, "sourceAnchors")
            .OrderBy(item => Text(item, "key"), StringComparer.Ordinal)
            .Select(item => ProjectAnchor(item, documentVersionId, fileCodeUnitId))
            .ToArray();
        Dictionary<string, CodeLogicFileV2SourceAnchorProjection> anchorsByKey =
            anchors.ToDictionary(anchor => anchor.Key, StringComparer.Ordinal);

        List<GroupProjection> groups = BuildGroups(root, anchorsByKey);
        if (groups.Count is < 1 or > 9)
        {
            throw new InvalidDataException("The fixed Document Section group bound was exceeded.");
        }

        CodeLogicFileV2SectionProjection[] sections = groups.Select((group, index) =>
            ProjectSection(group, documentVersionId, index + 1, anchorsByKey)).ToArray();
        string contentMarkdown = $"# {EscapeMarkdown(path)}\n\n" +
            string.Join("\n\n", groups.Select(group => group.Markdown));

        EnsureByteBound(contentMarkdown, KnowledgeDocumentLimits.MaximumMarkdownBytes, "ContentMarkdown");

        CodeLogicFileV2ReferenceEdgeProjection[] edges = Array(root, "knownReferences")
            .Select(item => ProjectEdge(item, documentVersionId, anchorsByKey))
            .OrderBy(edge => edge.TargetDocumentVersionId)
            .ToArray();

        CodeLogicFileV2IndexEntryProjection indexEntry = ProjectIndex(
            root, path, segmentOrdinal, segmentCount, documentVersionId);

        ReadOnlyMemory<byte>[] navigationFingerprints = root.EnumerateObject()
            .Where(property => property.Name is "communicationEvidence" or "knownReferences" or "deferredObservations")
            .SelectMany(property => ReceiptIds(property.Value))
            .Distinct()
            .Order()
            .Select(id => (ReadOnlyMemory<byte>)SHA256.HashData(id.ToByteArray()))
            .ToArray();
        ReadOnlyMemory<byte>[] classificationFingerprints = Array(root, "communicationEvidence")
            .SelectMany(item => Array(item, "classificationEvidence"))
            .Select(item => Hex(Text(item, "classificationFingerprint")))
            .Distinct(ByteArrayComparer.Instance)
            .OrderBy(bytes => Convert.ToHexString(bytes), StringComparer.Ordinal)
            .Select(bytes => (ReadOnlyMemory<byte>)bytes)
            .ToArray();

        PreparedPass1CanonicalArtifact commit = PreparedPass1CanonicalCodec.CreateCommit(
            new PreparedPass1CommitInput(
                pairExecutionId,
                jobId,
                logicalDocumentId,
                documentVersionId,
                contentFingerprint,
                anchors.Select(anchor => anchor.Fingerprint).ToArray(),
                edges.Select(edge => edge.TargetDocumentVersionId).ToArray(),
                sections.Select(section => section.ProjectionFingerprint).ToArray(),
                indexEntry.ProjectionFingerprint,
                navigationFingerprints,
                classificationFingerprints));

        return new(
            authoritativeJson,
            contentMarkdown,
            contentFingerprint,
            anchors,
            sections,
            indexEntry,
            edges,
            commit.Fingerprint);
    }

    private static CodeLogicFileV2SourceAnchorProjection ProjectAnchor(
        JsonElement value,
        Guid documentVersionId,
        Guid fileCodeUnitId)
    {
        PreparedPass1CanonicalArtifact artifact = PreparedPass1CanonicalCodec.CreateSourceAnchor(
            new PreparedPass1SourceAnchorInput(
                documentVersionId,
                fileCodeUnitId,
                Text(value, "repositoryRelativePath"),
                UInt64(value, "byteStart"),
                UInt64(value, "byteEnd"),
                UInt64(value, "lineStart"),
                UInt64(value, "lineEnd"),
                Hex(Text(value, "sourceHash")),
                Text(value, "evidenceLocator")));

        return new(
            artifact.Id!.Value,
            Text(value, "key"),
            fileCodeUnitId,
            Text(value, "repositoryRelativePath"),
            UInt64(value, "byteStart"),
            UInt64(value, "byteEnd"),
            UInt64(value, "lineStart"),
            UInt64(value, "lineEnd"),
            Hex(Text(value, "sourceHash")),
            Text(value, "evidenceLocator"),
            artifact.Fingerprint);
    }

    private static List<GroupProjection> BuildGroups(
        JsonElement root,
        IReadOnlyDictionary<string, CodeLogicFileV2SourceAnchorProjection> anchors)
    {
        var groups = new List<GroupProjection>(9);
        JsonElement purpose = Property(root, "filePurpose");
        Add(groups, PreparedPass1DocumentSectionGroup.Overview, DocumentSectionKind.Overview,
            "文件用途", [Text(purpose, "summary"), .. Array(purpose, "responsibilities").Select(item => Text(item, "description"))],
            [.. Refs(purpose), .. Array(purpose, "responsibilities").SelectMany(Refs)], anchors);

        JsonElement[] units = Array(root, "internalUnits");
        Add(groups, PreparedPass1DocumentSectionGroup.InternalUnits, DocumentSectionKind.TechnicalDetail,
            "内部单元", units.Select(item => $"{Text(item, "kind")} {Text(item, "qualifiedSymbol")}: {Text(item, "signature")} — {Text(item, "responsibility")}"),
            units.SelectMany(Refs), anchors);

        JsonElement[] execution = Array(root, "executionFlow");
        JsonElement[] dataFlow = Array(root, "dataFlow");
        Add(groups, PreparedPass1DocumentSectionGroup.ExecutionAndDataFlow, DocumentSectionKind.ProcessStep,
            "执行与数据流",
            execution.Select(item => Text(item, "description")).Concat(dataFlow.Select(item =>
                $"{Text(item, "from")} → {Text(item, "to")}: {Text(item, "value")} — {Text(item, "description")}")),
            execution.SelectMany(Refs).Concat(dataFlow.SelectMany(Refs)), anchors);

        JsonElement[] rules = Array(root, "rules");
        Add(groups, PreparedPass1DocumentSectionGroup.BusinessAndCodeRules, DocumentSectionKind.BusinessRule,
            "业务与代码规则", rules.Select(item => $"{Text(item, "kind")}: {Text(item, "condition")} → {Text(item, "effect")}"),
            rules.SelectMany(Refs), anchors);

        JsonElement[] access = Array(root, "dataAccess");
        Add(groups, PreparedPass1DocumentSectionGroup.DataAccess, DocumentSectionKind.TechnicalDetail,
            "数据读写", access.Select(item => $"{Text(item, "mode")} {Text(item, "subject")}: {Text(item, "description")}"),
            access.SelectMany(Refs), anchors);

        JsonElement[] communications = Array(root, "communicationEvidence");
        Add(groups, PreparedPass1DocumentSectionGroup.CommunicationEvidence, DocumentSectionKind.Evidence,
            "通信证据", communications.Select(item => $"{Text(item, "communicationKind")}: {Text(item, "observation")}"),
            communications.SelectMany(Refs), anchors);

        JsonElement[] references = Array(root, "knownReferences");
        Add(groups, PreparedPass1DocumentSectionGroup.References, DocumentSectionKind.Evidence,
            "引用", references.Select(item => $"{Text(item, "sourceClaimLocator")} → {Text(item, "targetRepositoryRelativePath")} ({Text(item, "targetDocumentVersionId")})"),
            references.SelectMany(Refs), anchors,
            references.Select(item => GuidValue(item, "targetDocumentVersionId")));

        JsonElement[] deferred = Array(root, "deferredObservations");
        Add(groups, PreparedPass1DocumentSectionGroup.DeferredObservations, DocumentSectionKind.Other,
            "延后观察", deferred.Select(item => $"未决 {Text(item, "outcome")}: {Text(item, "sourceClaimLocator")} → {OptionalText(item, "targetSymbol") ?? OptionalText(item, "targetRepositoryRelativePath") ?? "未指定目标"}"),
            deferred.SelectMany(Refs), anchors);

        JsonElement[] assumptions = Array(root, "assumptions");
        Add(groups, PreparedPass1DocumentSectionGroup.AssumptionsAndUnresolvedEvidence, DocumentSectionKind.Other,
            "假设与未决证据", assumptions.Select(item => $"{Text(item, "status")}: {Text(item, "description")}"),
            assumptions.SelectMany(Refs), anchors);
        return groups;
    }

    private static void Add(
        ICollection<GroupProjection> groups,
        PreparedPass1DocumentSectionGroup group,
        DocumentSectionKind kind,
        string heading,
        IEnumerable<string> claims,
        IEnumerable<string> anchorKeys,
        IReadOnlyDictionary<string, CodeLogicFileV2SourceAnchorProjection> anchors,
        IEnumerable<Guid>? documentVersionIds = null)
    {
        string[] values = claims.ToArray();
        if (values.Length == 0)
        {
            return;
        }

        string[] keys = anchorKeys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        string citations = keys.Length == 0 ? string.Empty : " " + string.Join(' ', keys.Select(Citation));
        string body = string.Join('\n', values.Select(value => $"- {EscapeMarkdown(value)}{citations}"));
        groups.Add(new(group, kind, heading, $"## {heading}\n\n{body}", string.Join('\n', values), keys,
            (documentVersionIds ?? []).Distinct().Order().ToArray()));
    }

    private static CodeLogicFileV2SectionProjection ProjectSection(
        GroupProjection group,
        Guid documentVersionId,
        int order,
        IReadOnlyDictionary<string, CodeLogicFileV2SourceAnchorProjection> anchors)
    {
        EnsureByteBound(group.Markdown, KnowledgeDocumentLimits.MaximumSectionMarkdownBytes, "Section Markdown");
        EnsureByteBound(group.SearchText, KnowledgeDocumentLimits.MaximumSectionSearchTextBytes, "Section search text");
        PreparedPass1CanonicalArtifact identity = PreparedPass1CanonicalCodec.CreateDocumentSection(documentVersionId, group.Group);
        Guid[] anchorIds = group.AnchorKeys.Select(key => anchors[key].Id).Order().ToArray();
        byte[] fingerprint = Fingerprint(
            identity.FingerprintHex, order.ToString(System.Globalization.CultureInfo.InvariantCulture),
            group.Kind.ToString(), group.Heading, group.Markdown, group.SearchText,
            string.Join(',', anchorIds), string.Join(',', group.DocumentVersionIds));
        return new(identity.Id!.Value, order, group.Kind, group.Heading, group.Markdown, group.SearchText,
            anchorIds, group.DocumentVersionIds, fingerprint);
    }

    private static CodeLogicFileV2IndexEntryProjection ProjectIndex(
        JsonElement root,
        string path,
        ulong segmentOrdinal,
        ulong segmentCount,
        Guid documentVersionId)
    {
        JsonElement purpose = Property(root, "filePurpose");
        JsonElement[] units = Array(root, "internalUnits");
        JsonElement[] rules = Array(root, "rules");
        JsonElement[] access = Array(root, "dataAccess");
        JsonElement[] communications = Array(root, "communicationEvidence");
        JsonElement[] deferred = Array(root, "deferredObservations");
        string filename = path.Replace('\\', '/').Split('/').Last();
        string title = segmentCount > 1 ? $"{filename} ({segmentOrdinal}/{segmentCount})" : filename;
        string summary = Text(purpose, "summary");
        string[] keySymbols = CanonicalTerms(units.Select(item => Text(item, "qualifiedSymbol")));
        string[] businessTerms = CanonicalTerms(rules.SelectMany(item =>
            Tokens(Text(item, "condition")).Concat(Tokens(Text(item, "effect")))));
        string[] technicalTerms = CanonicalTerms(
            units.Select(item => Text(item, "kind"))
                .Concat(access.Select(item => Text(item, "subject")))
                .Concat(communications.Select(item => Text(item, "communicationKind")))
                .Concat(Array(root, "knownReferences").Select(item => Text(item, "targetRepositoryRelativePath"))));
        string searchText = string.Join('\n',
            new[] { summary }
                .Concat(Array(purpose, "responsibilities").Select(item => Text(item, "description")))
                .Concat(units.SelectMany(item => new[] { Text(item, "qualifiedSymbol"), Text(item, "responsibility") }))
                .Concat(rules.SelectMany(item => new[] { Text(item, "condition"), Text(item, "effect") }))
                .Concat(access.SelectMany(item => new[] { Text(item, "subject"), Text(item, "description") }))
                .Concat(communications.Select(item => Text(item, "observation")))
                .Concat(deferred.Select(item => OptionalText(item, "targetSymbol")).Where(value => value is not null)!));
        EnsureByteBound(searchText, KnowledgeDocumentLimits.MaximumIndexSearchTextBytes, "Index search text");
        PreparedPass1CanonicalArtifact identity = PreparedPass1CanonicalCodec.CreateIndexEntry(documentVersionId);
        byte[] fingerprint = Fingerprint(identity.FingerprintHex, title, summary, string.Join('\n', keySymbols),
            string.Join('\n', businessTerms), string.Join('\n', technicalTerms), searchText);
        return new(identity.Id!.Value, title, summary, keySymbols, businessTerms, technicalTerms, searchText, fingerprint);
    }

    private static CodeLogicFileV2ReferenceEdgeProjection ProjectEdge(
        JsonElement value,
        Guid documentVersionId,
        IReadOnlyDictionary<string, CodeLogicFileV2SourceAnchorProjection> anchors)
    {
        Guid targetId = GuidValue(value, "targetDocumentVersionId");
        PreparedPass1CanonicalArtifact artifact = PreparedPass1CanonicalCodec.CreateReferencesEdge(documentVersionId, targetId);
        Guid[] anchorIds = Refs(value).Select(key => anchors[key].Id).Distinct().Order().ToArray();
        return new(artifact.Id!.Value, DocumentEdgeType.References, documentVersionId, targetId,
            anchorIds, Text(value, "sourceClaimLocator"), artifact.Fingerprint);
    }

    private static IEnumerable<Guid> ReceiptIds(JsonElement value)
    {
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.TryGetProperty("navigationReceiptId", out JsonElement id) && id.ValueKind == JsonValueKind.String)
            {
                yield return id.GetGuid();
            }

            if (item.TryGetProperty("navigationReceiptIds", out JsonElement ids))
            {
                foreach (JsonElement candidate in ids.EnumerateArray())
                {
                    yield return candidate.GetGuid();
                }
            }
        }
    }

    private static string[] CanonicalTerms(IEnumerable<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .Take(KnowledgeDocumentLimits.MaximumTerms)
        .ToArray();

    private static IEnumerable<string> Tokens(string value) => TokenRegex().Matches(value)
        .Select(match => match.Value);

    private static string Citation(string key) => $"[{key.ToUpperInvariant()}]";

    private static string EscapeMarkdown(string value) => Regex.Replace(value, @"([\\`*_{\}\[\]<>#+!|])", @"\$1");

    private static byte[] Fingerprint(params string[] values) =>
        SHA256.HashData(_strictUtf8.GetBytes(string.Join("\u001f", values)));

    private static void EnsureByteBound(string value, int maximum, string name)
    {
        if (_strictUtf8.GetByteCount(value) > maximum)
        {
            throw new InvalidDataException($"{name} exceeded its byte bound.");
        }
    }

    private static byte[] Hex(string value)
    {
        try
        {
            return Convert.FromHexString(value);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("An authoritative SHA-256 value was invalid.", exception);
        }
    }

    private static JsonElement Property(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement property)
            ? property
            : throw new InvalidDataException($"Required authoritative property '{name}' was absent.");

    private static JsonElement[] Array(JsonElement value, string name) =>
        Property(value, name).EnumerateArray().Select(item => item.Clone()).ToArray();

    private static string Text(JsonElement value, string name) =>
        Property(value, name).GetString() ?? throw new InvalidDataException($"Authoritative property '{name}' was null.");

    private static string? OptionalText(JsonElement value, string name) =>
        Property(value, name).ValueKind == JsonValueKind.Null ? null : Property(value, name).GetString();

    private static Guid GuidValue(JsonElement value, string name) => Property(value, name).GetGuid();

    private static ulong UInt64(JsonElement value, string name) => Property(value, name).GetUInt64();

    private static IEnumerable<string> Refs(JsonElement value) =>
        Array(value, "sourceAnchorRefs").Select(item => item.GetString()!);

    [GeneratedRegex(@"[\p{L}\p{N}_.$:-]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    private sealed record GroupProjection(
        PreparedPass1DocumentSectionGroup Group,
        DocumentSectionKind Kind,
        string Heading,
        string Markdown,
        string SearchText,
        IReadOnlyList<string> AnchorKeys,
        IReadOnlyList<Guid> DocumentVersionIds);

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static ByteArrayComparer Instance { get; } = new();

        public bool Equals(byte[]? left, byte[]? right) =>
            ReferenceEquals(left, right) || left is not null && right is not null && left.AsSpan().SequenceEqual(right);

        public int GetHashCode(byte[] value)
        {
            var hash = new HashCode();
            hash.AddBytes(value);
            return hash.ToHashCode();
        }
    }
}
