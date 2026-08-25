using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

public enum PreparedPass1ValidationFailureKind
{
    Shape,
    Binding,
    Evidence,
    Semantics,
}

public sealed record PreparedPass1ValidationResult(
    ReadOnlyMemory<byte>? CanonicalJson,
    PreparedPass1ValidationFailureKind? FailureKind)
{
    public bool IsValid => CanonicalJson is not null && FailureKind is null;
}

/// <summary>
/// Performs the T014 schema/identity preflight. T025 adds the complete v2
/// semantic materialization contract; this parser already rejects a wrong
/// grammar, unknown/duplicate root fields, and Worker-authored binding drift.
/// </summary>
public sealed class PreparedPass1ResultParser
{
    private const int _maximumResultBytes = CodeLogicFileV2Contract.MaximumUtf8Bytes;
    private static readonly string[] _rootProperties =
    [
        "schemaIdentity", "binding", "sourceAnchors", "filePurpose",
        "internalUnits", "executionFlow", "dataFlow", "rules", "dataAccess",
        "communicationEvidence", "knownReferences", "deferredObservations",
        "assumptions",
    ];
    private static readonly string[] _bindingProperties =
    [
        "jobId", "scanExecutionId", "systemId", "repositoryId", "scanRunId",
        "profiledRevision", "fileCodeUnitId", "fileCodeUnitIdentityFingerprint",
        "preparedScanManifestId", "preparedScanManifestFingerprint",
        "pairPackageFingerprint", "repositoryRelativePath", "byteStart", "byteEnd",
        "lineStart", "lineEnd", "segmentOrdinal", "segmentCount", "fileHash",
        "sourceHash", "promptIdentity", "documentSchemaIdentity",
        "analysisRuntimeIdentityHash",
    ];

    public static PreparedPass1ParsedWorkerResult Parse(
        PreparedPass1WorkerPacket packet,
        ReadOnlyMemory<byte> utf8Json)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (utf8Json.IsEmpty || utf8Json.Length > _maximumResultBytes)
        {
            throw new InvalidDataException("Prepared Pass 1 result exceeds its bounded JSON shape.");
        }

        if (IsServerDeferredCallsMarker(utf8Json))
        {
            utf8Json = CreateDeferredCallsFallback(packet).Utf8Json;
        }

        byte[] canonicalJson = CodeLogicFileV2Contract.ValidateAndCanonicalize(packet, utf8Json);

        using JsonDocument document = JsonDocument.Parse(
            utf8Json,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = CodeLogicFileV2Contract.MaximumJsonDepth,
            });
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Prepared Pass 1 result must be one JSON object.");
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!names.Add(property.Name) ||
                !_rootProperties.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new InvalidDataException("Prepared Pass 1 result contains an unknown or duplicate root property.");
            }
        }

        if (names.Count != _rootProperties.Length ||
            _rootProperties.Any(name => !names.Contains(name)))
        {
            throw new InvalidDataException("Prepared Pass 1 result omits a required root property.");
        }

        RequireString(root, "schemaIdentity", "code-logic/file-v2");
        JsonElement binding = root.GetProperty("binding");
        if (binding.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Prepared Pass 1 result binding must be an object.");
        }

        RequireExactObjectShape(binding, _bindingProperties, "binding");

        RequireString(binding, "jobId", packet.JobId.ToString("D"));
        RequireString(binding, "scanExecutionId", packet.ScanExecutionId.ToString("D"));
        RequireString(binding, "systemId", packet.SystemId.ToString("D"));
        RequireString(binding, "repositoryId", packet.RepositoryId.ToString("D"));
        RequireString(binding, "scanRunId", packet.ScanRunId.ToString("D"));
        RequireString(binding, "profiledRevision", packet.ProfiledRevision);
        RequireString(binding, "fileCodeUnitId", packet.FileCodeUnitId.ToString("D"));
        RequireString(binding, "fileCodeUnitIdentityFingerprint", packet.FileCodeUnitIdentityFingerprint);
        RequireString(binding, "preparedScanManifestId", packet.PreparedScanManifestId.ToString("D"));
        RequireString(binding, "preparedScanManifestFingerprint", packet.PreparedScanManifestFingerprint);
        RequireString(binding, "pairPackageFingerprint", packet.PairPackageFingerprint);
        RequireString(binding, "repositoryRelativePath", packet.RepositoryRelativePath);
        RequireInteger(binding, "byteStart", packet.ByteStart);
        RequireInteger(binding, "byteEnd", packet.ByteEnd);
        RequireInteger(binding, "lineStart", packet.LineStart);
        RequireInteger(binding, "lineEnd", packet.LineEnd);
        RequireInteger(binding, "segmentOrdinal", packet.SegmentOrdinal);
        RequireInteger(binding, "segmentCount", packet.SegmentCount);
        RequireString(binding, "fileHash", packet.FileHash);
        RequireString(binding, "sourceHash", packet.SourceHash);
        RequireString(binding, "promptIdentity", packet.PromptIdentity);
        RequireString(binding, "documentSchemaIdentity", packet.DocumentSchemaIdentity);
        RequireString(binding, "analysisRuntimeIdentityHash", packet.AnalysisRuntimeIdentityHash);

        foreach (string arrayName in new[]
        {
            "sourceAnchors", "internalUnits", "executionFlow", "dataFlow", "rules",
            "dataAccess", "communicationEvidence", "knownReferences",
            "deferredObservations", "assumptions",
        })
        {
            if (root.GetProperty(arrayName).ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"Prepared Pass 1 result property must be an array: {arrayName}.");
            }
        }

        ValidateSourceAnchors(packet, root.GetProperty("sourceAnchors"));

        if (root.GetProperty("filePurpose").ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Prepared Pass 1 filePurpose must be an object.");
        }

        return new PreparedPass1ParsedWorkerResult(
            "code-logic/file-v2",
            canonicalJson);
    }

    public static PreparedPass1ValidationResult Validate(
        PreparedPass1WorkerPacket packet,
        ReadOnlyMemory<byte> utf8Json)
    {
        try
        {
            PreparedPass1ParsedWorkerResult parsed = Parse(packet, utf8Json);
            return new(parsed.CanonicalCandidateJson, null);
        }
        catch (Exception exception) when (
            exception is InvalidDataException or ArgumentException or JsonException or
                OverflowException)
        {
            return new(null, ClassifyFailure(packet, utf8Json));
        }
    }

    private static PreparedPass1ValidationFailureKind ClassifyFailure(
        PreparedPass1WorkerPacket packet,
        ReadOnlyMemory<byte> utf8Json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                utf8Json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = CodeLogicFileV2Contract.MaximumJsonDepth,
                });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return PreparedPass1ValidationFailureKind.Shape;
            }

            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!names.Add(property.Name) ||
                    !_rootProperties.Contains(property.Name, StringComparer.Ordinal))
                {
                    return PreparedPass1ValidationFailureKind.Shape;
                }
            }

            if (names.Count != _rootProperties.Length ||
                _rootProperties.Any(name => !names.Contains(name)) ||
                !root.TryGetProperty("binding", out JsonElement binding) ||
                binding.ValueKind != JsonValueKind.Object)
            {
                return PreparedPass1ValidationFailureKind.Shape;
            }

            try
            {
                RequireExactObjectShape(binding, _bindingProperties, "binding");
                RequireString(binding, "jobId", packet.JobId.ToString("D"));
                RequireString(binding, "scanExecutionId", packet.ScanExecutionId.ToString("D"));
                RequireString(binding, "systemId", packet.SystemId.ToString("D"));
                RequireString(binding, "repositoryId", packet.RepositoryId.ToString("D"));
                RequireString(binding, "scanRunId", packet.ScanRunId.ToString("D"));
                RequireString(binding, "profiledRevision", packet.ProfiledRevision);
                RequireString(binding, "fileCodeUnitId", packet.FileCodeUnitId.ToString("D"));
                RequireString(binding, "fileCodeUnitIdentityFingerprint", packet.FileCodeUnitIdentityFingerprint);
                RequireString(binding, "preparedScanManifestId", packet.PreparedScanManifestId.ToString("D"));
                RequireString(binding, "preparedScanManifestFingerprint", packet.PreparedScanManifestFingerprint);
                RequireString(binding, "pairPackageFingerprint", packet.PairPackageFingerprint);
                RequireString(binding, "repositoryRelativePath", packet.RepositoryRelativePath);
                RequireInteger(binding, "byteStart", packet.ByteStart);
                RequireInteger(binding, "byteEnd", packet.ByteEnd);
                RequireInteger(binding, "lineStart", packet.LineStart);
                RequireInteger(binding, "lineEnd", packet.LineEnd);
                RequireInteger(binding, "segmentOrdinal", packet.SegmentOrdinal);
                RequireInteger(binding, "segmentCount", packet.SegmentCount);
                RequireString(binding, "fileHash", packet.FileHash);
                RequireString(binding, "sourceHash", packet.SourceHash);
                RequireString(binding, "promptIdentity", packet.PromptIdentity);
                RequireString(binding, "documentSchemaIdentity", packet.DocumentSchemaIdentity);
                RequireString(binding, "analysisRuntimeIdentityHash", packet.AnalysisRuntimeIdentityHash);
            }
            catch (InvalidDataException)
            {
                return PreparedPass1ValidationFailureKind.Binding;
            }

            try
            {
                ValidateSourceAnchors(packet, root.GetProperty("sourceAnchors"));
            }
            catch (InvalidDataException)
            {
                return PreparedPass1ValidationFailureKind.Evidence;
            }

            return PreparedPass1ValidationFailureKind.Semantics;
        }
        catch (JsonException)
        {
            return PreparedPass1ValidationFailureKind.Shape;
        }
    }

    public static PreparedPass1RawWorkerResult CreateDeferredCallsFallback(
        PreparedPass1WorkerPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        string evidenceLocator = $"{packet.RepositoryRelativePath}#L{packet.LineStart}-L{packet.LineEnd}";
        PreparedPass1CanonicalArtifact receipt = PreparedPass1CanonicalCodec.CreateNavigationReceipt(
            new PreparedPass1NavigationReceiptInput(
                null,
                packet.JobId,
                1,
                8,
                PreparedPass1NavigationOperation.Definition,
                1,
                PreparedPass1NavigationOutcome.BoundExceeded,
                PreparedPass1NavigationBoundKind.Calls,
                [],
                0,
                0,
                8,
                0));
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                schemaIdentity = "code-logic/file-v2",
                binding = new
                {
                    jobId = packet.JobId.ToString("D"),
                    scanExecutionId = packet.ScanExecutionId.ToString("D"),
                    systemId = packet.SystemId.ToString("D"),
                    repositoryId = packet.RepositoryId.ToString("D"),
                    scanRunId = packet.ScanRunId.ToString("D"),
                    profiledRevision = packet.ProfiledRevision,
                    fileCodeUnitId = packet.FileCodeUnitId.ToString("D"),
                    fileCodeUnitIdentityFingerprint = packet.FileCodeUnitIdentityFingerprint,
                    preparedScanManifestId = packet.PreparedScanManifestId.ToString("D"),
                    preparedScanManifestFingerprint = packet.PreparedScanManifestFingerprint,
                    pairPackageFingerprint = packet.PairPackageFingerprint,
                    repositoryRelativePath = packet.RepositoryRelativePath,
                    byteStart = packet.ByteStart,
                    byteEnd = packet.ByteEnd,
                    lineStart = packet.LineStart,
                    lineEnd = packet.LineEnd,
                    segmentOrdinal = packet.SegmentOrdinal,
                    segmentCount = packet.SegmentCount,
                    fileHash = packet.FileHash,
                    sourceHash = packet.SourceHash,
                    promptIdentity = packet.PromptIdentity,
                    documentSchemaIdentity = packet.DocumentSchemaIdentity,
                    analysisRuntimeIdentityHash = packet.AnalysisRuntimeIdentityHash,
                },
                sourceAnchors = new[]
                {
                    new
                    {
                        key = "a001",
                        repositoryRelativePath = packet.RepositoryRelativePath,
                        byteStart = packet.ByteStart,
                        byteEnd = packet.ByteEnd,
                        lineStart = packet.LineStart,
                        lineEnd = packet.LineEnd,
                        sourceHash = packet.SourceHash,
                        evidenceLocator,
                    },
                },
                filePurpose = new
                {
                    summary = "deferred navigation bound",
                    sourceAnchorRefs = new[] { "a001" },
                    responsibilities = new[]
                    {
                        new
                        {
                            description = "Records the bounded File Code Unit pending further navigation.",
                            sourceAnchorRefs = new[] { "a001" },
                        },
                    },
                },
                internalUnits = Array.Empty<object>(),
                executionFlow = Array.Empty<object>(),
                dataFlow = Array.Empty<object>(),
                rules = Array.Empty<object>(),
                dataAccess = Array.Empty<object>(),
                communicationEvidence = Array.Empty<object>(),
                knownReferences = Array.Empty<object>(),
                deferredObservations = new[]
                {
                    new
                    {
                        order = 1,
                        relationshipKind = "References",
                        outcome = "BoundExceeded",
                        sourceClaimLocator = evidenceLocator,
                        targetSymbol = "navigation-Calls-bound",
                        targetRepositoryRelativePath = (string?)null,
                        targetSourceHash = (string?)null,
                        navigationOperation = "Definition",
                        navigationDepth = 1,
                        navigationReceiptIds = new[] { receipt.Id!.Value },
                        sourceAnchorRefs = new[] { "a001" },
                    },
                },
                assumptions = Array.Empty<object>(),
            });
        return new PreparedPass1RawWorkerResult(
            PreparedPass1RawWorkerOutcome.Completed,
            json,
            "navigation-calls-bound-exceeded");
    }

    private static bool IsServerDeferredCallsMarker(ReadOnlyMemory<byte> utf8Json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8Json);
            JsonElement root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("sourceAnchors", out JsonElement anchors) &&
                anchors.ValueKind == JsonValueKind.Array && anchors.GetArrayLength() == 0 &&
                root.TryGetProperty("filePurpose", out JsonElement purpose) &&
                purpose.ValueKind == JsonValueKind.Object && purpose.EnumerateObject().Count() == 1 &&
                purpose.TryGetProperty("summary", out JsonElement summary) &&
                summary.GetString() == "deferred navigation bound" &&
                root.TryGetProperty("deferredObservations", out JsonElement observations) &&
                observations.ValueKind == JsonValueKind.Array && observations.GetArrayLength() == 1 &&
                observations[0].ValueKind == JsonValueKind.Object &&
                observations[0].EnumerateObject().Count() == 2 &&
                observations[0].TryGetProperty("outcome", out JsonElement outcome) &&
                outcome.GetString() == "BoundExceeded" &&
                observations[0].TryGetProperty("boundKind", out JsonElement boundKind) &&
                boundKind.GetString() == "Calls";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void RequireString(JsonElement owner, string name, string expected)
    {
        if (!owner.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String ||
            !string.Equals(value.GetString(), expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Prepared Pass 1 result binding mismatch: {name}.");
        }
    }

    private static void ValidateSourceAnchors(
        PreparedPass1WorkerPacket packet,
        JsonElement anchors)
    {
        if (anchors.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Prepared Pass 1 sourceAnchors must be an array.");
        }

        foreach (JsonElement anchor in anchors.EnumerateArray())
        {
            if (anchor.ValueKind != JsonValueKind.Object ||
                !anchor.TryGetProperty("repositoryRelativePath", out JsonElement pathValue) ||
                pathValue.ValueKind != JsonValueKind.String ||
                !anchor.TryGetProperty("sourceHash", out JsonElement sourceHashValue) ||
                sourceHashValue.ValueKind != JsonValueKind.String ||
                !anchor.TryGetProperty("byteStart", out JsonElement byteStartValue) ||
                !byteStartValue.TryGetInt64(out long byteStart) ||
                !anchor.TryGetProperty("byteEnd", out JsonElement byteEndValue) ||
                !byteEndValue.TryGetInt64(out long byteEnd) ||
                !anchor.TryGetProperty("lineStart", out JsonElement lineStartValue) ||
                !lineStartValue.TryGetInt64(out long lineStart) ||
                !anchor.TryGetProperty("lineEnd", out JsonElement lineEndValue) ||
                !lineEndValue.TryGetInt64(out long lineEnd) ||
                !anchor.TryGetProperty("evidenceLocator", out JsonElement locatorValue) ||
                locatorValue.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("Prepared Pass 1 source anchor has an invalid evidence shape.");
            }

            string? path = pathValue.GetString();
            string expectedLocator = $"{path}#L{lineStart}-L{lineEnd}";
            if (!string.Equals(path, packet.RepositoryRelativePath, StringComparison.Ordinal) ||
                !string.Equals(sourceHashValue.GetString(), packet.SourceHash, StringComparison.Ordinal) ||
                byteStart < packet.ByteStart ||
                byteEnd > packet.ByteEnd ||
                byteStart >= byteEnd ||
                lineStart < packet.LineStart ||
                lineEnd > packet.LineEnd ||
                lineStart > lineEnd ||
                !string.Equals(locatorValue.GetString(), expectedLocator, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Prepared Pass 1 source anchor is outside authorized evidence.");
            }
        }
    }

    private static void RequireInteger(JsonElement owner, string name, long expected)
    {
        if (!owner.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out long actual) ||
            actual != expected)
        {
            throw new InvalidDataException($"Prepared Pass 1 result binding mismatch: {name}.");
        }
    }

    private static void RequireExactObjectShape(
        JsonElement owner,
        string[] expectedNames,
        string shapeName)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty property in owner.EnumerateObject())
        {
            if (!names.Add(property.Name) || !expectedNames.Contains(property.Name))
            {
                throw new InvalidDataException(
                    $"Prepared Pass 1 result contains an unknown or duplicate {shapeName} property.");
            }
        }

        if (names.Count != expectedNames.Length || expectedNames.Any(name => !names.Contains(name)))
        {
            throw new InvalidDataException($"Prepared Pass 1 result omits a required {shapeName} property.");
        }
    }
}
