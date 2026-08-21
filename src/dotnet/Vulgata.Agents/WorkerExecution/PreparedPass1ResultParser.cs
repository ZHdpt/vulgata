using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Performs the T014 schema/identity preflight. T025 adds the complete v2
/// semantic materialization contract; this parser already rejects a wrong
/// grammar, unknown/duplicate root fields, and Worker-authored binding drift.
/// </summary>
public sealed class PreparedPass1ResultParser
{
    private const int _maximumResultBytes = 1_048_576;
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

        using JsonDocument document = JsonDocument.Parse(
            utf8Json,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
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

        if (root.GetProperty("filePurpose").ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Prepared Pass 1 filePurpose must be an object.");
        }

        return new PreparedPass1ParsedWorkerResult(
            "code-logic/file-v2",
            utf8Json.ToArray());
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
