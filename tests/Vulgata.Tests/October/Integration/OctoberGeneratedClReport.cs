using System.Collections.Immutable;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Tests.October.Integration;

/// <summary>Strict clean one-file CL qualification projection, not a general Scan report parser or proof of storage.</summary>
internal sealed record OctoberGeneratedClReport(PublicationBinding Publication, ImmutableArray<DocumentHandle> Documents)
{
    internal static OctoberGeneratedClReport Parse(int processExitCode, string stdout,
        KnowledgeBinding expected, PreparedOrigin expectedOrigin)
    {
        try
        {
            Require(processExitCode == 0 && expected is not null && expectedOrigin is not null);
            Require(!string.IsNullOrWhiteSpace(expected.CanonicalDatabaseIdentity) && !string.IsNullOrWhiteSpace(expected.TargetId)
                && expected.BatchId != Guid.Empty && Digest(expected.SelectionDigest));
            Require(expectedOrigin.RepositoryId != Guid.Empty && SafePath(expectedOrigin.RelativePath)
                && Digest(expectedOrigin.SourceDigest) && Opaque(expectedOrigin.RootBindingIdentity) && Opaque(expectedOrigin.PreparedVersion));
            using var parsed = JsonDocument.Parse(stdout);
            var root = parsed.RootElement;
            UniqueProperties(root);
            Shape(root, "requestedStage", "completedStage", "coverage", "execution", "publicationStatus", "matching", "businessLogic",
                "publication", "documents", "acceptedFiles", "terminalCoverage", "inventory", "gaps", "timings", "usage", "exitCode");
            Require(Text(root, "requestedStage") == "cl-only" && Text(root, "completedStage") == "cl-only"
                && Text(root, "coverage") == "Complete" && Text(root, "execution") == "Completed"
                && Text(root, "publicationStatus") == "Confirmed" && Text(root, "matching") == "not-requested"
                && Text(root, "businessLogic") == "not-requested" && root.GetProperty("exitCode").GetInt32() == 0);
            Require(root.GetProperty("gaps").GetArrayLength() == 0);
            var publication = root.GetProperty("publication");
            Shape(publication, "targetId", "batchId", "selectionDigest", "publicationId");
            Require(Text(publication, "targetId") == expected.TargetId && Id(publication, "batchId") == expected.BatchId
                && Text(publication, "selectionDigest") == expected.SelectionDigest);
            var binding = new PublicationBinding(expected, Id(publication, "publicationId"));
            var documents = ImmutableArray.CreateBuilder<DocumentHandle>();
            foreach (var item in root.GetProperty("documents").EnumerateArray())
            {
                Shape(item, "batchId", "publicationId", "logicalDocumentId", "documentVersionId", "sectionId");
                Require(Id(item, "batchId") == expected.BatchId && Id(item, "publicationId") == binding.PublicationId
                    && item.GetProperty("sectionId").ValueKind == JsonValueKind.Null);
                documents.Add(new(expected.BatchId, binding.PublicationId, Id(item, "logicalDocumentId"), Id(item, "documentVersionId")));
            }
            Require(documents.Count > 0 && documents.Distinct().Count() == documents.Count
                && documents.Select(x => x.LogicalDocumentId).Distinct().Count() == documents.Count
                && documents.Select(x => x.DocumentVersionId).Distinct().Count() == documents.Count);
            var files = root.GetProperty("acceptedFiles"); Require(files.GetArrayLength() == 1);
            var file = files[0]; Shape(file, "manifestId", "admissionId", "proposalDigest", "documents", "coverage");
            _ = Id(file, "manifestId"); var admission = Id(file, "admissionId"); Require(Digest(Text(file, "proposalDigest")));
            var versions = new List<DocumentVersionReference>();
            foreach (var item in file.GetProperty("documents").EnumerateArray())
            {
                Shape(item, "batchId", "logicalDocumentId", "documentVersionId");
                Require(Id(item, "batchId") == expected.BatchId);
                versions.Add(new(expected.BatchId, Id(item, "logicalDocumentId"), Id(item, "documentVersionId")));
            }
            Require(versions.Count == documents.Count && versions.Distinct().Count() == versions.Count
                && versions.ToHashSet().SetEquals(documents.Select(x => new DocumentVersionReference(x.BatchId, x.LogicalDocumentId, x.DocumentVersionId))));
            Coverage(file.GetProperty("coverage"), admission, expectedOrigin);
            var terminal = root.GetProperty("terminalCoverage"); Require(terminal.GetArrayLength() == 1);
            Coverage(terminal[0], admission, expectedOrigin);
            int selected = 0;
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.GetProperty("inventory").EnumerateArray())
            {
                Shape(item, "repositoryId", "relativePath", "kind", "disposition", "reason", "priority", "observedByteLength");
                string path = Text(item, "relativePath");
                Require(Id(item, "repositoryId") == expectedOrigin.RepositoryId && SafePath(path) && paths.Add(path));
                string disposition = Text(item, "disposition");
                if (disposition == "selected")
                {
                    selected++;
                    Require(path == expectedOrigin.RelativePath && Text(item, "kind") == "RegularFile"
                        && Text(item, "reason") == "configured-selected-source");
                }
                else Require(disposition is "supporting" or "excluded" or "unsupported");
            }
            Require(selected == 1);
            Require(root.GetProperty("timings").ValueKind == JsonValueKind.Object && root.GetProperty("usage").ValueKind == JsonValueKind.Object);
            return new(binding, documents.ToImmutable());
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException
            or FormatException or ArgumentException or OverflowException)
        {
            // Neither provider diagnostics nor untrusted report text are included in errors.
            throw new ArgumentException("The output is not an exact successful one-file CL qualification report.");
        }
    }

    private static void Coverage(JsonElement value, Guid admission, PreparedOrigin expected)
    {
        Shape(value, "admissionId", "origin", "disposition", "reason");
        Require(Id(value, "admissionId") == admission && Text(value, "disposition") == "Accepted"
            && value.GetProperty("reason").ValueKind == JsonValueKind.Null);
        var origin = value.GetProperty("origin");
        Shape(origin, "repositoryId", "rootBindingIdentity", "relativePath", "sourceDigest", "preparedVersion");
        Require(Id(origin, "repositoryId") == expected.RepositoryId && Text(origin, "rootBindingIdentity") == expected.RootBindingIdentity
            && Text(origin, "relativePath") == expected.RelativePath && Text(origin, "sourceDigest") == expected.SourceDigest
            && Text(origin, "preparedVersion") == expected.PreparedVersion);
    }

    private static void Shape(JsonElement value, params string[] names)
    {
        Require(value.ValueKind == JsonValueKind.Object);
        Require(value.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(names));
    }

    private static void UniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject()) { Require(names.Add(property.Name)); UniqueProperties(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) UniqueProperties(item);
    }

    private static Guid Id(JsonElement value, string name) { var id = value.GetProperty(name).GetGuid(); Require(id != Guid.Empty); return id; }
    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? throw new ArgumentException();
    private static bool Digest(string value) => value.Length == 64 && value.All(char.IsAsciiHexDigit);
    private static bool SafePath(string value) => value.Length is > 0 and <= 2048 && !value.Contains('\\') && !value.Contains(':')
        && !value.Any(char.IsControl) && value.Split('/').All(part => part is not ("" or "." or ".."));
    private static bool Opaque(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 4096 && value == value.Trim()
        && !value.Any(char.IsControl) && !Path.IsPathFullyQualified(value) && !value.Contains(":/", StringComparison.Ordinal) && !value.Contains(":\\", StringComparison.Ordinal);
    private static void Require(bool condition) { if (!condition) throw new ArgumentException(); }
}
