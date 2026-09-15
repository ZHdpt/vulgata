using System.Security.Cryptography;
using System.Text.Json;

namespace Vulgata.Infrastructure.Source;

internal sealed record RetainedSourceManifestEntry(
    long Ordinal,
    byte[] PathBytes,
    string? RelativePath,
    string EntryKind,
    string GitMode,
    string GitObjectId,
    long? ByteLength,
    string? ContentSha256,
    string? StorageBlobKey,
    IReadOnlyList<string> Capabilities,
    string? LimitationCode,
    string? StagedContentPath);

internal sealed record RetainedSourceManifestDocument(
    byte[] Bytes,
    string Sha256,
    long EntryCount,
    long TotalBlobBytes,
    IReadOnlyList<RetainedSourceManifestEntry> Entries)
{
    internal Guid RepositoryId { get; init; }
    internal string ProfiledRevision { get; init; } = string.Empty;
    internal string GitObjectFormat { get; init; } = string.Empty;
}

internal static class RetainedSourceManifestCodec
{
    internal static RetainedSourceManifestDocument Create(
        Guid repositoryId,
        CommittedSourceCapture capture)
    {
        var entries = new List<RetainedSourceManifestEntry>(capture.Entries.Count);
        long totalBlobBytes = 0;
        for (int ordinal = 0; ordinal < capture.Entries.Count; ordinal++)
        {
            CommittedSourceEntry entry = capture.Entries[ordinal];
            string entryKind = entry.Kind switch
            {
                CommittedSourceEntryKind.RegularFile => "regularFile",
                CommittedSourceEntryKind.ExecutableFile => "executableFile",
                CommittedSourceEntryKind.SymbolicLink => "symbolicLink",
                CommittedSourceEntryKind.Gitlink => "gitlink",
                _ => throw new InvalidDataException("Unsupported retained source entry kind."),
            };
            long? byteLength = entry.ByteLength;
            string? contentSha256 = entry.ContentSha256;
            string? storageBlobKey = contentSha256 is null ? null : $"blobs/{contentSha256}";
            if (byteLength is not null)
            {
                totalBlobBytes = checked(totalBlobBytes + byteLength.Value);
            }

            bool safePath = entry.RelativePath is not null;
            bool regular = entry.Kind is CommittedSourceEntryKind.RegularFile or
                CommittedSourceEntryKind.ExecutableFile;
            bool strictUtf8 = regular && entry.IsStrictUtf8;
            IReadOnlyList<string> capabilities = !safePath || !regular
                ? ["metadata"]
                : strictUtf8
                    ? ["metadata", "byteRange", "strictUtf8Text"]
                    : ["metadata", "byteRange"];
            string? limitation = !safePath
                ? "source_path_unsupported"
                : entry.Kind == CommittedSourceEntryKind.SymbolicLink
                    ? "source_symbolic_link_unsupported"
                    : entry.Kind == CommittedSourceEntryKind.Gitlink
                        ? "source_gitlink_unsupported"
                        : strictUtf8
                            ? null
                            : "source_text_unsupported";
            entries.Add(new(
                ordinal,
                entry.PathBytes,
                entry.RelativePath,
                entryKind,
                entry.GitMode,
                entry.GitObjectId,
                byteLength,
                contentSha256,
                storageBlobKey,
                capabilities,
                limitation,
                entry.StagedContentPath));
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaIdentity", "retained-source-manifest/1.0");
            writer.WriteString("repositoryId", repositoryId.ToString("D"));
            writer.WriteString("profiledRevision", capture.ProfiledRevision);
            writer.WriteString("gitObjectFormat", capture.GitObjectFormat);
            writer.WriteNumber("entryCount", entries.Count);
            writer.WriteNumber("totalBlobBytes", totalBlobBytes);
            writer.WriteStartArray("entries");
            foreach (RetainedSourceManifestEntry entry in entries)
            {
                writer.WriteStartObject();
                writer.WriteNumber("ordinal", entry.Ordinal);
                writer.WriteString("pathBytesBase64", Convert.ToBase64String(entry.PathBytes));
                if (entry.RelativePath is null)
                {
                    writer.WriteNull("relativePath");
                }
                else
                {
                    writer.WriteString("relativePath", entry.RelativePath);
                }
                writer.WriteString("entryKind", entry.EntryKind);
                writer.WriteString("gitMode", entry.GitMode);
                writer.WriteString("gitObjectId", entry.GitObjectId);
                WriteNullableNumber(writer, "byteLength", entry.ByteLength);
                WriteNullableString(writer, "contentSha256", entry.ContentSha256);
                WriteNullableString(writer, "storageBlobKey", entry.StorageBlobKey);
                writer.WriteStartArray("capabilities");
                foreach (string capability in entry.Capabilities)
                {
                    writer.WriteStringValue(capability);
                }
                writer.WriteEndArray();
                WriteNullableString(writer, "limitationCode", entry.LimitationCode);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        byte[] bytes = buffer.ToArray();
        return new(bytes, Sha256(bytes), entries.Count, totalBlobBytes, entries);
    }

    internal static async Task<RetainedSourceManifestDocument> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using JsonDocument document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            root.GetProperty("schemaIdentity").GetString() != "retained-source-manifest/1.0")
        {
            throw new RetainedSourceIntegrityException();
        }

        Guid repositoryId = root.GetProperty("repositoryId").GetGuid();
        string profiledRevision = RequiredString(root, "profiledRevision");
        string gitObjectFormat = RequiredString(root, "gitObjectFormat");
        long entryCount = root.GetProperty("entryCount").GetInt64();
        long totalBlobBytes = root.GetProperty("totalBlobBytes").GetInt64();
        JsonElement entryArray = root.GetProperty("entries");
        if (entryArray.ValueKind != JsonValueKind.Array || entryArray.GetArrayLength() != entryCount)
        {
            throw new RetainedSourceIntegrityException();
        }

        var entries = new List<RetainedSourceManifestEntry>(entryArray.GetArrayLength());
        long computedBlobBytes = 0;
        foreach (JsonElement item in entryArray.EnumerateArray())
        {
            long ordinal = item.GetProperty("ordinal").GetInt64();
            byte[] pathBytes = Convert.FromBase64String(RequiredString(item, "pathBytesBase64"));
            string? relativePath = NullableString(item, "relativePath");
            string entryKind = RequiredString(item, "entryKind");
            string gitMode = RequiredString(item, "gitMode");
            string gitObjectId = RequiredString(item, "gitObjectId");
            long? byteLength = NullableInt64(item, "byteLength");
            string? contentSha256 = NullableString(item, "contentSha256");
            string? storageBlobKey = NullableString(item, "storageBlobKey");
            string[] capabilities = item.GetProperty("capabilities")
                .EnumerateArray()
                .Select(static value => value.GetString() ?? string.Empty)
                .ToArray();
            string? limitationCode = NullableString(item, "limitationCode");
            if (ordinal != entries.Count || pathBytes.Length == 0 ||
                entryKind is not ("regularFile" or "executableFile" or "symbolicLink" or "gitlink") ||
                gitMode is not ("100644" or "100755" or "120000" or "160000"))
            {
                throw new RetainedSourceIntegrityException();
            }

            if (entryKind == "gitlink")
            {
                if (byteLength is not null || contentSha256 is not null || storageBlobKey is not null)
                {
                    throw new RetainedSourceIntegrityException();
                }
            }
            else
            {
                if (byteLength is null or < 0 || contentSha256 is null ||
                    storageBlobKey != $"blobs/{contentSha256}")
                {
                    throw new RetainedSourceIntegrityException();
                }
                computedBlobBytes = checked(computedBlobBytes + byteLength.Value);
            }

            entries.Add(new(
                ordinal,
                pathBytes,
                relativePath,
                entryKind,
                gitMode,
                gitObjectId,
                byteLength,
                contentSha256,
                storageBlobKey,
                capabilities,
                limitationCode,
                null));
        }

        if (computedBlobBytes != totalBlobBytes)
        {
            throw new RetainedSourceIntegrityException();
        }

        return new([], string.Empty, entryCount, totalBlobBytes, entries)
        {
            RepositoryId = repositoryId,
            ProfiledRevision = profiledRevision,
            GitObjectFormat = gitObjectFormat,
        };
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string RequiredString(JsonElement owner, string name) =>
        owner.GetProperty(name).GetString() ?? throw new RetainedSourceIntegrityException();

    private static string? NullableString(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        return value.ValueKind == JsonValueKind.Null
            ? null
            : value.GetString() ?? throw new RetainedSourceIntegrityException();
    }

    private static long? NullableInt64(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetInt64();
    }

    private static void WriteNullableNumber(Utf8JsonWriter writer, string name, long? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteNumber(name, value.Value);
        }
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, value);
        }
    }
}
