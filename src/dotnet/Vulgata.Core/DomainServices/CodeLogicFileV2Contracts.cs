using System.Text;
using System.Text.Json;
using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Closed validator and canonical writer for the authoritative
/// <c>code-logic/file-v2</c> Worker result. Durable authorization is repeated by
/// the document committer; this boundary rejects values that are not bound to
/// the prepared Worker packet before any projection is attempted.
/// </summary>
public static class CodeLogicFileV2Contract
{
    public const int MaximumUtf8Bytes = 65_536;
    public const int MaximumJsonDepth = 32;

    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private static readonly string[] _root =
    [
        "schemaIdentity", "binding", "sourceAnchors", "filePurpose", "internalUnits",
        "executionFlow", "dataFlow", "rules", "dataAccess", "communicationEvidence",
        "knownReferences", "deferredObservations", "assumptions",
    ];
    private static readonly string[] _binding =
    [
        "jobId", "scanExecutionId", "systemId", "repositoryId", "scanRunId",
        "profiledRevision", "fileCodeUnitId", "fileCodeUnitIdentityFingerprint",
        "preparedScanManifestId", "preparedScanManifestFingerprint", "pairPackageFingerprint",
        "repositoryRelativePath", "byteStart", "byteEnd", "lineStart", "lineEnd",
        "segmentOrdinal", "segmentCount", "fileHash", "sourceHash", "promptIdentity",
        "documentSchemaIdentity", "analysisRuntimeIdentityHash",
    ];

    private static readonly IReadOnlyDictionary<string, string[]> _shapes =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["sourceAnchor"] = ["key", "repositoryRelativePath", "byteStart", "byteEnd", "lineStart", "lineEnd", "sourceHash", "evidenceLocator"],
            ["filePurpose"] = ["summary", "sourceAnchorRefs", "responsibilities"],
            ["claim"] = ["description", "sourceAnchorRefs"],
            ["internalUnit"] = ["order", "kind", "qualifiedSymbol", "signature", "responsibility", "byteStart", "byteEnd", "lineStart", "lineEnd", "sourceAnchorRefs"],
            ["orderedClaim"] = ["order", "description", "sourceAnchorRefs"],
            ["dataFlow"] = ["order", "from", "to", "value", "description", "sourceAnchorRefs"],
            ["rule"] = ["order", "kind", "condition", "effect", "sourceAnchorRefs"],
            ["dataAccess"] = ["order", "mode", "subject", "description", "sourceAnchorRefs"],
            ["communicationEvidence"] = ["order", "communicationKind", "observation", "classificationEvidence", "navigationReceiptIds", "sourceAnchorRefs"],
            ["classificationEvidence"] = ["classificationId", "classificationFingerprint", "communicationKind", "level", "confidence", "evidenceRule", "provenance", "catalogRuleIdentity", "evidenceLocator"],
            ["knownReference"] = ["order", "relationshipKind", "targetDocumentVersionId", "targetRepositoryRelativePath", "targetSourceHash", "navigationReceiptId", "sourceClaimLocator", "sourceAnchorRefs"],
            ["deferredObservation"] = ["order", "relationshipKind", "outcome", "sourceClaimLocator", "targetSymbol", "targetRepositoryRelativePath", "targetSourceHash", "navigationOperation", "navigationDepth", "navigationReceiptIds", "sourceAnchorRefs"],
            ["assumption"] = ["order", "status", "description", "sourceAnchorRefs"],
        };

    public static byte[] ValidateAndCanonicalize(
        PreparedPass1WorkerPacket packet,
        ReadOnlyMemory<byte> utf8Json)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (utf8Json.IsEmpty || utf8Json.Length > MaximumUtf8Bytes ||
            (utf8Json.Length >= 3 && utf8Json.Span[0] == 0xef &&
             utf8Json.Span[1] == 0xbb && utf8Json.Span[2] == 0xbf))
        {
            throw Invalid("The authoritative result exceeds its UTF-8 bound or contains a BOM.");
        }

        try
        {
            _ = _strictUtf8.GetString(utf8Json.Span);
        }
        catch (DecoderFallbackException exception)
        {
            throw Invalid("The authoritative result is not valid UTF-8.", exception);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8Json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaximumJsonDepth,
            });
        }
        catch (JsonException exception)
        {
            throw Invalid("The authoritative result is not bounded valid JSON.", exception);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            RequireShape(root, _root, "root");
            RequireExact(root, "schemaIdentity", "code-logic/file-v2");
            ValidateBinding(packet, root.GetProperty("binding"));

            Dictionary<string, JsonElement> anchors = ValidateAnchors(packet, root.GetProperty("sourceAnchors"));
            ValidateFilePurpose(root.GetProperty("filePurpose"), anchors);
            ValidateOrderedClaims(root.GetProperty("internalUnits"), "internalUnit", anchors, packet, 64, hasRange: true);
            ValidateOrderedClaims(root.GetProperty("executionFlow"), "orderedClaim", anchors, packet, 32);
            ValidateOrderedClaims(root.GetProperty("dataFlow"), "dataFlow", anchors, packet, 32);
            ValidateOrderedClaims(root.GetProperty("rules"), "rule", anchors, packet, 32);
            ValidateOrderedClaims(root.GetProperty("dataAccess"), "dataAccess", anchors, packet, 32);

            HashSet<Guid> citedReceipts = [];
            ValidateCommunication(packet, root.GetProperty("communicationEvidence"), anchors, citedReceipts);
            ValidateKnownReferences(root.GetProperty("knownReferences"), anchors, citedReceipts);
            ValidateDeferred(root.GetProperty("deferredObservations"), anchors, citedReceipts);
            ValidateOrderedClaims(root.GetProperty("assumptions"), "assumption", anchors, packet, 16, anchorsMayBeEmpty: true);
            ValidateReceiptConsistency(root, citedReceipts);
            ValidateAllStrings(root);

            using var stream = new MemoryStream(utf8Json.Length);
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                WriteObject(writer, root, _root, ResolveRootShape);
            }

            return stream.ToArray();
        }
    }

    private static void ValidateBinding(PreparedPass1WorkerPacket packet, JsonElement binding)
    {
        RequireShape(binding, _binding, "binding");
        RequireExact(binding, "jobId", packet.JobId.ToString("D"));
        RequireExact(binding, "scanExecutionId", packet.ScanExecutionId.ToString("D"));
        RequireExact(binding, "systemId", packet.SystemId.ToString("D"));
        RequireExact(binding, "repositoryId", packet.RepositoryId.ToString("D"));
        RequireExact(binding, "scanRunId", packet.ScanRunId.ToString("D"));
        RequireExact(binding, "profiledRevision", packet.ProfiledRevision);
        RequireExact(binding, "fileCodeUnitId", packet.FileCodeUnitId.ToString("D"));
        RequireExact(binding, "fileCodeUnitIdentityFingerprint", packet.FileCodeUnitIdentityFingerprint);
        RequireExact(binding, "preparedScanManifestId", packet.PreparedScanManifestId.ToString("D"));
        RequireExact(binding, "preparedScanManifestFingerprint", packet.PreparedScanManifestFingerprint);
        RequireExact(binding, "pairPackageFingerprint", packet.PairPackageFingerprint);
        RequireExact(binding, "repositoryRelativePath", packet.RepositoryRelativePath);
        RequireExact(binding, "fileHash", packet.FileHash);
        RequireExact(binding, "sourceHash", packet.SourceHash);
        RequireExact(binding, "promptIdentity", packet.PromptIdentity);
        RequireExact(binding, "documentSchemaIdentity", packet.DocumentSchemaIdentity);
        RequireExact(binding, "analysisRuntimeIdentityHash", packet.AnalysisRuntimeIdentityHash);
        RequireExact(binding, "byteStart", packet.ByteStart);
        RequireExact(binding, "byteEnd", packet.ByteEnd);
        RequireExact(binding, "lineStart", packet.LineStart);
        RequireExact(binding, "lineEnd", packet.LineEnd);
        RequireExact(binding, "segmentOrdinal", packet.SegmentOrdinal);
        RequireExact(binding, "segmentCount", packet.SegmentCount);
        RequireRange(packet.ByteStart, packet.ByteEnd, packet.LineStart, packet.LineEnd, packet);
    }

    private static Dictionary<string, JsonElement> ValidateAnchors(
        PreparedPass1WorkerPacket packet,
        JsonElement array)
    {
        RequireArray(array, 1, 512, "sourceAnchors");
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        int ordinal = 0;
        long previousByteStart = -1;
        foreach (JsonElement anchor in array.EnumerateArray())
        {
            ordinal++;
            RequireShape(anchor, _shapes["sourceAnchor"], "sourceAnchor");
            string key = RequireString(anchor, "key");
            if (key != $"a{ordinal:000}" || !result.TryAdd(key, anchor))
            {
                throw Invalid("Source Anchor keys must be consecutive and unique.");
            }

            RequireExact(anchor, "repositoryRelativePath", packet.RepositoryRelativePath);
            RequireExact(anchor, "sourceHash", packet.SourceHash);
            long byteStart = RequireInt64(anchor, "byteStart");
            long byteEnd = RequireInt64(anchor, "byteEnd");
            int lineStart = RequireInt32(anchor, "lineStart");
            int lineEnd = RequireInt32(anchor, "lineEnd");
            RequireRange(byteStart, byteEnd, lineStart, lineEnd, packet);
            if (byteStart < previousByteStart)
            {
                throw Invalid("Source Anchors are not in canonical range order.");
            }

            previousByteStart = byteStart;
        }

        return result;
    }

    private static void ValidateFilePurpose(JsonElement purpose, IReadOnlyDictionary<string, JsonElement> anchors)
    {
        RequireShape(purpose, _shapes["filePurpose"], "filePurpose");
        _ = RequireString(purpose, "summary");
        ValidateAnchorRefs(purpose.GetProperty("sourceAnchorRefs"), anchors, false);
        JsonElement responsibilities = purpose.GetProperty("responsibilities");
        RequireArray(responsibilities, 1, 16, "responsibilities");
        foreach (JsonElement claim in responsibilities.EnumerateArray())
        {
            RequireShape(claim, _shapes["claim"], "claim");
            _ = RequireString(claim, "description");
            ValidateAnchorRefs(claim.GetProperty("sourceAnchorRefs"), anchors, false);
        }
    }

    private static void ValidateOrderedClaims(
        JsonElement array,
        string shape,
        IReadOnlyDictionary<string, JsonElement> anchors,
        PreparedPass1WorkerPacket packet,
        int maximum,
        bool hasRange = false,
        bool anchorsMayBeEmpty = false)
    {
        RequireArray(array, 0, maximum, shape);
        int order = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            order++;
            RequireShape(item, _shapes[shape], shape);
            RequireExact(item, "order", order);
            ValidateAnchorRefs(item.GetProperty("sourceAnchorRefs"), anchors, anchorsMayBeEmpty);
            if (hasRange)
            {
                RequireRange(RequireInt64(item, "byteStart"), RequireInt64(item, "byteEnd"),
                    RequireInt32(item, "lineStart"), RequireInt32(item, "lineEnd"), packet);
            }
        }
    }

    private static void ValidateCommunication(
        PreparedPass1WorkerPacket packet,
        JsonElement array,
        IReadOnlyDictionary<string, JsonElement> anchors,
        ISet<Guid> receipts)
    {
        RequireArray(array, 0, 32, "communicationEvidence");
        int order = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            RequireShape(item, _shapes["communicationEvidence"], "communicationEvidence");
            RequireExact(item, "order", ++order);
            string communicationKind = RequireEnum(item, "communicationKind", "Rpc", "Http");
            ValidateAnchorRefs(item.GetProperty("sourceAnchorRefs"), anchors, false);
            JsonElement classifications = item.GetProperty("classificationEvidence");
            RequireArray(classifications, 0, 32, "classificationEvidence");
            JsonElement receiptIds = item.GetProperty("navigationReceiptIds");
            RequireArray(receiptIds, 0, 8, "navigationReceiptIds");
            if (classifications.GetArrayLength() == 0 && receiptIds.GetArrayLength() == 0)
            {
                throw Invalid("Communication evidence must cite classification or navigation evidence.");
            }

            foreach (JsonElement evidence in classifications.EnumerateArray())
            {
                RequireShape(evidence, _shapes["classificationEvidence"], "classificationEvidence");
                Guid id = RequireGuid(evidence, "classificationId");
                PreparedPass1CommunicationClassificationProjection? supplied = packet.CommunicationClassifications
                    .SingleOrDefault(value => value.ClassificationId == id);
                if (supplied is null || supplied.Kind is null ||
                    !EqualsClassification(evidence, supplied, communicationKind))
                {
                    throw Invalid("Communication classification evidence is not an exact supplied binding.");
                }
            }

            AddReceipts(receiptIds, receipts);
        }
    }

    private static bool EqualsClassification(
        JsonElement evidence,
        PreparedPass1CommunicationClassificationProjection supplied,
        string enclosingKind) =>
        RequireString(evidence, "classificationFingerprint") == supplied.ClassificationFingerprint &&
        RequireString(evidence, "communicationKind") == enclosingKind &&
        enclosingKind == supplied.Kind!.Value.ToString() &&
        RequireString(evidence, "level") == supplied.Level.ToString() &&
        RequireString(evidence, "confidence") == supplied.Confidence.ToString() &&
        RequireString(evidence, "evidenceRule") == supplied.Rule.ToString() &&
        RequireString(evidence, "provenance") == supplied.Provenance.ToString() &&
        RequireString(evidence, "catalogRuleIdentity") == supplied.CatalogRuleIdentity &&
        RequireString(evidence, "evidenceLocator") == supplied.EvidenceLocator;

    private static void ValidateKnownReferences(
        JsonElement array,
        IReadOnlyDictionary<string, JsonElement> anchors,
        ISet<Guid> receipts)
    {
        RequireArray(array, 0, 64, "knownReferences");
        int order = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            RequireShape(item, _shapes["knownReference"], "knownReference");
            RequireExact(item, "order", ++order);
            RequireExact(item, "relationshipKind", "References");
            _ = RequireGuid(item, "targetDocumentVersionId");
            _ = RequireHash(item, "targetSourceHash");
            ValidatePath(RequireString(item, "targetRepositoryRelativePath"));
            ValidateAnchorRefs(item.GetProperty("sourceAnchorRefs"), anchors, false);
            receipts.Add(RequireGuid(item, "navigationReceiptId"));
        }
    }

    private static void ValidateDeferred(
        JsonElement array,
        IReadOnlyDictionary<string, JsonElement> anchors,
        ISet<Guid> receipts)
    {
        RequireArray(array, 0, 64, "deferredObservations");
        int order = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            RequireShape(item, _shapes["deferredObservation"], "deferredObservation");
            RequireExact(item, "order", ++order);
            RequireExact(item, "relationshipKind", "References");
            RequireEnum(item, "outcome", "Insufficient", "Ambiguous", "Stale", "Unsupported", "CrossRepository", "BoundExceeded");
            RequireEnum(item, "navigationOperation", "Definition", "References", "Implementations", "Callers", "Callees", "SymbolSearch", "RelatedFiles", "ExactSourceEvidence");
            int depth = RequireInt32(item, "navigationDepth");
            if (depth is < 1 or > 4)
            {
                throw Invalid("Navigation depth is outside the accepted bound.");
            }

            bool hasSymbol = IsNonemptyStringOrNull(item, "targetSymbol");
            bool hasPath = IsNonemptyStringOrNull(item, "targetRepositoryRelativePath", ValidatePath);
            _ = IsHashOrNull(item, "targetSourceHash");
            if (!hasSymbol && !hasPath)
            {
                throw Invalid("A deferred observation requires a target symbol or path.");
            }

            JsonElement receiptIds = item.GetProperty("navigationReceiptIds");
            RequireArray(receiptIds, 1, 8, "navigationReceiptIds");
            AddReceipts(receiptIds, receipts);
            ValidateAnchorRefs(item.GetProperty("sourceAnchorRefs"), anchors, false);
        }
    }

    private static void ValidateReceiptConsistency(JsonElement root, IReadOnlySet<Guid> allReceipts)
    {
        HashSet<Guid> deferredReceipts = root.GetProperty("deferredObservations").EnumerateArray()
            .SelectMany(value => value.GetProperty("navigationReceiptIds").EnumerateArray())
            .Select(value => ParseGuid(value.GetString()))
            .ToHashSet();
        foreach (JsonElement known in root.GetProperty("knownReferences").EnumerateArray())
        {
            if (!deferredReceipts.Contains(RequireGuid(known, "navigationReceiptId")))
            {
                throw Invalid("A known reference cites a receipt outside the accepted result evidence set.");
            }
        }

        if (allReceipts.Count > 8)
        {
            throw Invalid("The result exceeds the durable navigation receipt bound.");
        }
    }

    private static void ValidateAllStrings(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    ValidateAllStrings(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in value.EnumerateArray())
                {
                    ValidateAllStrings(item);
                }
                break;
            case JsonValueKind.String:
                string text = value.GetString()!;
                if (text != text.Normalize(NormalizationForm.FormC) || text.Contains('\r') ||
                    text.Any(character => char.IsControl(character) && character != '\n') ||
                    _strictUtf8.GetByteCount(text) > 4096)
                {
                    throw Invalid("The result contains unsafe or noncanonical text.");
                }
                break;
        }
    }

    private static void ValidateAnchorRefs(
        JsonElement refs,
        IReadOnlyDictionary<string, JsonElement> anchors,
        bool mayBeEmpty)
    {
        RequireArray(refs, mayBeEmpty ? 0 : 1, 32, "sourceAnchorRefs");
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (JsonElement value in refs.EnumerateArray())
        {
            string key = value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;
            if (!anchors.ContainsKey(key) || !unique.Add(key))
            {
                throw Invalid("A claim cites an unknown or duplicate Source Anchor.");
            }
        }
    }

    private static void RequireRange(
        long byteStart,
        long byteEnd,
        int lineStart,
        int lineEnd,
        PreparedPass1WorkerPacket packet)
    {
        if (byteStart < packet.ByteStart || byteStart >= byteEnd || byteEnd > packet.ByteEnd ||
            lineStart < packet.LineStart || lineStart > lineEnd || lineEnd > packet.LineEnd)
        {
            throw Invalid("A result range is empty, reversed, or outside the File Code Unit.");
        }
    }

    private static void RequireShape(JsonElement owner, IReadOnlyList<string> expected, string name)
    {
        if (owner.ValueKind != JsonValueKind.Object)
        {
            throw Invalid($"{name} must be an object.");
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty property in owner.EnumerateObject())
        {
            if (!names.Add(property.Name) || !expected.Contains(property.Name, StringComparer.Ordinal))
            {
                throw Invalid($"{name} contains an unknown or duplicate property.");
            }
        }

        if (names.Count != expected.Count || expected.Any(property => !names.Contains(property)))
        {
            throw Invalid($"{name} omits a required property.");
        }
    }

    private static void RequireArray(JsonElement value, int minimum, int maximum, string name)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() < minimum || value.GetArrayLength() > maximum)
        {
            throw Invalid($"{name} is outside its array bound.");
        }
    }

    private static string RequireString(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw Invalid($"{name} must be nonempty text.");
        }

        return value.GetString()!;
    }

    private static string RequireHash(JsonElement owner, string name)
    {
        string value = RequireString(owner, name);
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character) || char.IsUpper(character)))
        {
            throw Invalid($"{name} must be a lowercase SHA-256 value.");
        }

        return value;
    }

    private static Guid RequireGuid(JsonElement owner, string name) =>
        ParseGuid(RequireString(owner, name));

    private static Guid ParseGuid(string? value)
    {
        if (!Guid.TryParseExact(value, "D", out Guid id) || id == Guid.Empty || value != id.ToString("D"))
        {
            throw Invalid("A lowercase nonempty UUID is required.");
        }

        return id;
    }

    private static long RequireInt64(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long result))
        {
            throw Invalid($"{name} must be an integer.");
        }

        return result;
    }

    private static int RequireInt32(JsonElement owner, string name) => checked((int)RequireInt64(owner, name));

    private static string RequireEnum(JsonElement owner, string name, params string[] values)
    {
        string value = RequireString(owner, name);
        if (!values.Contains(value, StringComparer.Ordinal))
        {
            throw Invalid($"{name} is outside its closed vocabulary.");
        }

        return value;
    }

    private static void RequireExact(JsonElement owner, string name, string expected)
    {
        if (RequireString(owner, name) != expected)
        {
            throw Invalid($"The authoritative binding does not match: {name}.");
        }
    }

    private static void RequireExact(JsonElement owner, string name, long expected)
    {
        if (RequireInt64(owner, name) != expected)
        {
            throw Invalid($"The authoritative binding does not match: {name}.");
        }
    }

    private static bool IsNonemptyStringOrNull(JsonElement owner, string name, Action<string>? validate = null)
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw Invalid($"{name} must be null or nonempty text.");
        }

        validate?.Invoke(value.GetString()!);
        return true;
    }

    private static bool IsHashOrNull(JsonElement owner, string name)
    {
        if (owner.GetProperty(name).ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        _ = RequireHash(owner, name);
        return true;
    }

    private static void ValidatePath(string path)
    {
        if (path.Length > 2048 || path.StartsWith('/') || path.Contains('\\') ||
            (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':') ||
            path.Split('/').Any(segment => segment is "." or ".." or ""))
        {
            throw Invalid("A repository-relative canonical path is required.");
        }
    }

    private static void AddReceipts(JsonElement values, ISet<Guid> receipts)
    {
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (!receipts.Add(ParseGuid(value.GetString())))
            {
                continue;
            }
        }
    }

    private static string[]? ResolveRootShape(string property) => property switch
    {
        "binding" => _binding,
        "sourceAnchors" => _shapes["sourceAnchor"],
        "filePurpose" => _shapes["filePurpose"],
        "internalUnits" => _shapes["internalUnit"],
        "executionFlow" => _shapes["orderedClaim"],
        "dataFlow" => _shapes["dataFlow"],
        "rules" => _shapes["rule"],
        "dataAccess" => _shapes["dataAccess"],
        "communicationEvidence" => _shapes["communicationEvidence"],
        "knownReferences" => _shapes["knownReference"],
        "deferredObservations" => _shapes["deferredObservation"],
        "assumptions" => _shapes["assumption"],
        _ => null,
    };

    private static void WriteObject(
        Utf8JsonWriter writer,
        JsonElement value,
        IReadOnlyList<string> propertyOrder,
        Func<string, string[]?>? nestedShape = null)
    {
        writer.WriteStartObject();
        foreach (string name in propertyOrder)
        {
            writer.WritePropertyName(name);
            JsonElement child = value.GetProperty(name);
            WriteValue(writer, child, nestedShape?.Invoke(name));
        }
        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonElement value, string[]? objectShape)
    {
        if (value.ValueKind == JsonValueKind.Object && objectShape is not null)
        {
            WriteObject(writer, value, objectShape, ResolveNestedShape);
            return;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (JsonElement item in value.EnumerateArray())
            {
                WriteValue(writer, item, objectShape);
            }
            writer.WriteEndArray();
            return;
        }

        value.WriteTo(writer);
    }

    private static string[]? ResolveNestedShape(string property) => property switch
    {
        "responsibilities" => _shapes["claim"],
        "classificationEvidence" => _shapes["classificationEvidence"],
        _ => null,
    };

    private static InvalidDataException Invalid(string message, Exception? inner = null) =>
        new(message, inner);
}
