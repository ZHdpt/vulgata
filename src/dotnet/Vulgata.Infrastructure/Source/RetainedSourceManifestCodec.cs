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
    IReadOnlyList<RetainedSourceManifestEntry> Entries);

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

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

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
