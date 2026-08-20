using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Infrastructure.Git;

public sealed record PreparedRawArtifact(
    string RelativePath,
    string ProfiledRevision,
    string SnapshotIdentity,
    string FileHash,
    string SourceHash,
    long RawByteCount,
    int LogicalLineCount,
    string EncodingIdentity,
    ReadOnlyMemory<byte> RawBytes);

public sealed class PreparedScanRawArtifactReader
{
    public const int MaximumRawBytes = 4_194_304;

    public async Task<PreparedRawArtifact> ReadAsync(
        string snapshotRoot,
        string relativePath,
        string profiledRevision,
        string snapshotIdentity,
        CancellationToken cancellationToken = default)
    {
        RequireRevision(profiledRevision);
        string fullPath = ResolveContainedPath(snapshotRoot, relativePath);
        FileInfo before = RequireRegularFile(fullPath);
        if (before.Length > MaximumRawBytes)
        {
            throw new InvalidDataException("The source artifact exceeds the hard audit bound.");
        }

        byte[] bytes = new byte[before.Length];
        await using (var stream = new FileStream(
                         fullPath,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.Read,
                         64 * 1024,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await stream.ReadExactlyAsync(bytes, cancellationToken);
        }

        FileInfo after = RequireRegularFile(fullPath);
        if (before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc)
        {
            throw new InvalidOperationException("The source artifact changed while it was read.");
        }

        string extension = Path.GetExtension(relativePath);
        string encoding = extension.Equals(".java", StringComparison.OrdinalIgnoreCase)
            ? ValidateJava(bytes)
            : extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
                ? ValidateXml(bytes)
                : throw new InvalidDataException("Only Java and XML artifacts are supported.");
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new(
            relativePath.Replace('\\', '/'),
            profiledRevision,
            snapshotIdentity,
            hash,
            hash,
            bytes.LongLength,
            CountLogicalLines(bytes, encoding),
            encoding,
            bytes);
    }

    private static FileInfo RequireRegularFile(string path)
    {
        var file = new FileInfo(path);
        file.Refresh();
        if (!file.Exists || (file.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException("The source artifact is not a regular contained file.");
        }

        return file;
    }

    private static string ResolveContainedPath(string rootPath, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string normalized = relativePath.Replace('\\', '/');
        if (Path.IsPathFullyQualified(normalized) || normalized[0] == '/' ||
            normalized.Any(char.IsControl) ||
            normalized.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new InvalidDataException("The source artifact path is unsafe.");
        }

        string root = Path.GetFullPath(rootPath);
        string full = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The source artifact escaped the snapshot.");
        }

        return full;
    }

    private static string ValidateJava(byte[] bytes)
    {
        if (bytes.AsSpan().Contains((byte)0))
        {
            throw new InvalidDataException("Java source cannot contain NUL bytes.");
        }

        _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(bytes);
        return "utf-8";
    }

    private static string ValidateXml(byte[] bytes)
    {
        string text;
        string identity;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            text = new UnicodeEncoding(false, true, true).GetString(bytes);
            identity = "utf-16le";
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            text = new UnicodeEncoding(true, true, true).GetString(bytes);
            identity = "utf-16be";
        }
        else
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
            identity = "utf-8";
        }

        ValidateXmlDeclarationEncoding(text, identity);
        return identity;
    }

    private static void ValidateXmlDeclarationEncoding(string text, string actualEncoding)
    {
        int declarationEnd = text.IndexOf("?>", StringComparison.Ordinal);
        if (declarationEnd < 0 || declarationEnd > 512 ||
            !text.AsSpan().TrimStart('\uFEFF').StartsWith("<?xml", StringComparison.Ordinal))
        {
            return;
        }

        ReadOnlySpan<char> declaration = text.AsSpan(0, declarationEnd);
        int encodingIndex = declaration.IndexOf("encoding", StringComparison.OrdinalIgnoreCase);
        if (encodingIndex < 0)
        {
            return;
        }

        ReadOnlySpan<char> suffix = declaration[(encodingIndex + "encoding".Length)..].TrimStart();
        if (suffix.IsEmpty || suffix[0] != '=')
        {
            throw new InvalidDataException("The XML encoding declaration is malformed.");
        }

        suffix = suffix[1..].TrimStart();
        if (suffix.Length < 3 || suffix[0] is not ('\'' or '"'))
        {
            throw new InvalidDataException("The XML encoding declaration is malformed.");
        }

        char quote = suffix[0];
        int close = suffix[1..].IndexOf(quote);
        if (close < 0)
        {
            throw new InvalidDataException("The XML encoding declaration is malformed.");
        }

        string declared = suffix.Slice(1, close).ToString();
        bool matches = declared.Equals("UTF-8", StringComparison.OrdinalIgnoreCase)
            ? actualEncoding == "utf-8"
            : declared.Equals("UTF-16", StringComparison.OrdinalIgnoreCase)
                ? actualEncoding is "utf-16le" or "utf-16be"
                : false;
        if (!matches)
        {
            throw new InvalidDataException("The XML encoding declaration is unsupported or conflicts with the bytes.");
        }
    }

    private static int CountLogicalLines(byte[] bytes, string encodingIdentity)
    {
        string text = encodingIdentity switch
        {
            "utf-16le" => new UnicodeEncoding(false, true, true).GetString(bytes),
            "utf-16be" => new UnicodeEncoding(true, true, true).GetString(bytes),
            _ => new UTF8Encoding(false, true).GetString(bytes),
        };
        int count = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r')
            {
                count++;
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }
            }
            else if (text[index] == '\n')
            {
                count++;
            }
        }

        if (text.Length > 0 && text[^1] is not '\r' and not '\n')
        {
            count++;
        }

        return count;
    }

    private static void RequireRevision(string revision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        if (revision.Length != 40 || revision.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("A full lowercase Profiled Revision is required.", nameof(revision));
        }
    }
}
