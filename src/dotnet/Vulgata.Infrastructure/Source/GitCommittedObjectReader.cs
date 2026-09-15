using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Infrastructure.Source;

internal enum CommittedSourceEntryKind
{
    RegularFile,
    ExecutableFile,
    SymbolicLink,
    Gitlink,
}

internal sealed record CommittedSourceEntry(
    byte[] PathBytes,
    string? RelativePath,
    CommittedSourceEntryKind Kind,
    string GitMode,
    string GitObjectId,
    long? ByteLength,
    string? ContentSha256,
    string? StagedContentPath,
    bool IsStrictUtf8);

internal sealed record CommittedSourceCapture(
    string ProfiledRevision,
    string GitObjectFormat,
    IReadOnlyList<CommittedSourceEntry> Entries);

internal enum GitCommittedObjectFailure
{
    None,
    InvalidRef,
    AcquisitionFailed,
    UnsupportedEntry,
    CaptureLimitExceeded,
}

internal sealed record GitCommittedObjectResult(
    CommittedSourceCapture? Capture,
    GitCommittedObjectFailure Failure)
{
    internal static GitCommittedObjectResult Success(CommittedSourceCapture capture) =>
        new(capture, GitCommittedObjectFailure.None);

    internal static GitCommittedObjectResult Failed(GitCommittedObjectFailure failure) =>
        new(null, failure);
}

internal sealed class GitCommittedObjectReader(RetainedSourceCapturePolicy policy)
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    internal async Task<GitCommittedObjectResult> CaptureAsync(
        string gitOrigin,
        string permittedRef,
        string gitDirectory,
        CancellationToken cancellationToken)
    {
        if (!IsPermittedFullRef(permittedRef))
        {
            return GitCommittedObjectResult.Failed(GitCommittedObjectFailure.InvalidRef);
        }

        ProcessResult advertised = await RunTextAsync(
            ["ls-remote", "--exit-code", gitOrigin, permittedRef],
            cancellationToken);
        if (advertised.ExitCode != 0)
        {
            return GitCommittedObjectResult.Failed(
                advertised.ExitCode == 2
                    ? GitCommittedObjectFailure.InvalidRef
                    : GitCommittedObjectFailure.AcquisitionFailed);
        }

        string advertisedObjectId = advertised.StandardOutput
            .Split(['\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0];
        string advertisedObjectFormat = advertisedObjectId.Length == 40
            ? "sha1"
            : advertisedObjectId.Length == 64
                ? "sha256"
                : string.Empty;
        if (!IsLowercaseHex(advertisedObjectId, advertisedObjectId.Length) ||
            advertisedObjectFormat.Length == 0)
        {
            return GitCommittedObjectResult.Failed(GitCommittedObjectFailure.AcquisitionFailed);
        }

        Directory.CreateDirectory(gitDirectory);
        ProcessResult initialized = await RunTextAsync(
            ["init", "--bare", $"--object-format={advertisedObjectFormat}", gitDirectory],
            cancellationToken);
        if (initialized.ExitCode != 0)
        {
            return GitCommittedObjectResult.Failed(GitCommittedObjectFailure.AcquisitionFailed);
        }

        ProcessResult remote = await RunTextAsync(
            ["-C", gitDirectory, "remote", "add", "origin", gitOrigin],
            cancellationToken);
        if (remote.ExitCode != 0)
        {
            return GitCommittedObjectResult.Failed(GitCommittedObjectFailure.AcquisitionFailed);
        }

        const string captureRef = "refs/vulgata/source-capture";
        ProcessResult fetched = await RunTextAsync(
            ["-C", gitDirectory, "fetch", "--no-tags", "--depth=1", "origin",
                $"+{permittedRef}:{captureRef}"],
            cancellationToken);
        if (fetched.ExitCode != 0)
        {
            return GitCommittedObjectResult.Failed(GitCommittedObjectFailure.AcquisitionFailed);
        }

        ProcessResult revisionResult = await RunTextAsync(
            ["-C", gitDirectory, "rev-parse", "--verify", $"{captureRef}^{{commit}}"],
            cancellationToken);
        ProcessResult formatResult = await RunTextAsync(
            ["-C", gitDirectory, "rev-parse", "--show-object-format"],
            cancellationToken);
        string revision = revisionResult.StandardOutput.Trim();
        string objectFormat = formatResult.StandardOutput.Trim();
        int expectedObjectIdLength = objectFormat == "sha1" ? 40 : objectFormat == "sha256" ? 64 : 0;
        if (revisionResult.ExitCode != 0 || formatResult.ExitCode != 0 ||
            !IsLowercaseHex(revision, expectedObjectIdLength))
        {
            return GitCommittedObjectResult.Failed(GitCommittedObjectFailure.AcquisitionFailed);
        }

        BinaryProcessResult tree = await RunBinaryAsync(
            ["-C", gitDirectory, "ls-tree", "-rz", "--full-tree", revision],
            maximumBytes: checked((int)Math.Min(67_108_864, policy.MaximumEntries * 8_192)),
            cancellationToken);
        if (tree.ExitCode != 0)
        {
            return GitCommittedObjectResult.Failed(GitCommittedObjectFailure.AcquisitionFailed);
        }

        List<TreeEntry>? treeEntries = ParseTree(tree.StandardOutput, expectedObjectIdLength);
        if (treeEntries is null)
        {
            return GitCommittedObjectResult.Failed(GitCommittedObjectFailure.UnsupportedEntry);
        }

        if (treeEntries.Count > policy.MaximumEntries)
        {
            return GitCommittedObjectResult.Failed(GitCommittedObjectFailure.CaptureLimitExceeded);
        }

        var entries = new List<CommittedSourceEntry>(treeEntries.Count);
        string blobStagingRoot = Path.Combine(Path.GetDirectoryName(gitDirectory)!, "git-blobs");
        Directory.CreateDirectory(blobStagingRoot);
        var capturedBlobs = new Dictionary<string, StreamedBlobResult>(StringComparer.Ordinal);
        long aggregateBytes = 0;
        foreach (TreeEntry treeEntry in treeEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CommittedSourceEntryKind? kind = treeEntry.Mode switch
            {
                "100644" => CommittedSourceEntryKind.RegularFile,
                "100755" => CommittedSourceEntryKind.ExecutableFile,
                "120000" => CommittedSourceEntryKind.SymbolicLink,
                "160000" => CommittedSourceEntryKind.Gitlink,
                _ => null,
            };
            if (kind is null)
            {
                return GitCommittedObjectResult.Failed(GitCommittedObjectFailure.UnsupportedEntry);
            }

            StreamedBlobResult? content = null;
            if (kind != CommittedSourceEntryKind.Gitlink)
            {
                if (!capturedBlobs.TryGetValue(treeEntry.ObjectId, out content))
                {
                    content = await RunBlobToFileAsync(
                        ["-C", gitDirectory, "cat-file", "blob", treeEntry.ObjectId],
                        Path.Combine(blobStagingRoot, treeEntry.ObjectId),
                        policy.MaximumBlobBytes,
                        cancellationToken);
                    capturedBlobs.Add(treeEntry.ObjectId, content);
                }

                if (content.ExceededBound)
                {
                    return GitCommittedObjectResult.Failed(
                        GitCommittedObjectFailure.CaptureLimitExceeded);
                }

                if (content.ExitCode != 0)
                {
                    return GitCommittedObjectResult.Failed(
                        GitCommittedObjectFailure.AcquisitionFailed);
                }

                aggregateBytes = checked(aggregateBytes + content.ByteLength);
                if (aggregateBytes > policy.MaximumAggregateBlobBytes)
                {
                    return GitCommittedObjectResult.Failed(
                        GitCommittedObjectFailure.CaptureLimitExceeded);
                }
            }

            entries.Add(new(
                treeEntry.PathBytes,
                TryDecodeSafePath(treeEntry.PathBytes),
                kind.Value,
                treeEntry.Mode,
                treeEntry.ObjectId,
                content?.ByteLength,
                content?.Sha256,
                content?.Path,
                content?.IsStrictUtf8 ?? false));
        }

        entries.Sort(static (left, right) => left.PathBytes.AsSpan().SequenceCompareTo(right.PathBytes));
        return GitCommittedObjectResult.Success(new(revision, objectFormat, entries));
    }

    private static bool IsPermittedFullRef(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.StartsWith("refs/", StringComparison.Ordinal) &&
        value.Length <= 1_024 &&
        !value.Contains("..", StringComparison.Ordinal) &&
        !value.Contains("@{", StringComparison.Ordinal) &&
        !value.EndsWith('.') &&
        !value.Any(static character => char.IsControl(character) ||
            char.IsWhiteSpace(character) || character is '~' or '^' or ':' or '?' or '*' or '[' or '\\');

    private static List<TreeEntry>? ParseTree(byte[] bytes, int objectIdLength)
    {
        var entries = new List<TreeEntry>();
        int start = 0;
        while (start < bytes.Length)
        {
            int end = Array.IndexOf(bytes, (byte)0, start);
            if (end < 0)
            {
                return null;
            }

            ReadOnlySpan<byte> record = bytes.AsSpan(start, end - start);
            int firstSpace = record.IndexOf((byte)' ');
            int secondSpace = firstSpace < 0 ? -1 : record[(firstSpace + 1)..].IndexOf((byte)' ');
            if (secondSpace >= 0)
            {
                secondSpace += firstSpace + 1;
            }
            int tab = record.IndexOf((byte)'\t');
            if (firstSpace != 6 || secondSpace < 0 || tab <= secondSpace + 1)
            {
                return null;
            }

            string mode = Encoding.ASCII.GetString(record[..firstSpace]);
            string type = Encoding.ASCII.GetString(record[(firstSpace + 1)..secondSpace]);
            string objectId = Encoding.ASCII.GetString(record[(secondSpace + 1)..tab]);
            if (type is not ("blob" or "commit") ||
                !IsLowercaseHex(objectId, objectIdLength) ||
                tab == record.Length - 1)
            {
                return null;
            }

            entries.Add(new(mode, objectId, record[(tab + 1)..].ToArray()));
            start = end + 1;
        }

        return entries;
    }

    private static string? TryDecodeSafePath(byte[] pathBytes)
    {
        try
        {
            string path = _strictUtf8.GetString(pathBytes);
            string[] segments = path.Split('/');
            return path.Length is > 0 and <= 4_096 &&
                !path.StartsWith('/') &&
                !path.Contains('\\', StringComparison.Ordinal) &&
                !path.Contains(':', StringComparison.Ordinal) &&
                !path.Any(char.IsControl) &&
                segments.All(static segment => segment is not ("" or "." or ".."))
                    ? path
                    : null;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool IsLowercaseHex(string value, int expectedLength) =>
        expectedLength > 0 && value.Length == expectedLength && value.All(static character =>
            char.IsAsciiHexDigit(character) && !char.IsAsciiLetterUpper(character));

    private static async Task<ProcessResult> RunTextAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        BinaryProcessResult result = await RunBinaryAsync(arguments, 16_384, cancellationToken);
        return new(result.ExitCode, Encoding.UTF8.GetString(result.StandardOutput));
    }

    private static async Task<BinaryProcessResult> RunBinaryAsync(
        IReadOnlyList<string> arguments,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
            Task<byte[]> stderr = ReadBoundedAsync(process.StandardError.BaseStream, 4_096);
            Task<(byte[] Bytes, bool Exceeded)> stdout = ReadBoundedWithOverflowAsync(
                process.StandardOutput.BaseStream,
                Math.Max(1, maximumBytes));
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                await process.WaitForExitAsync(CancellationToken.None);
                await Task.WhenAll(stderr, stdout);
                throw;
            }

            (byte[] output, bool exceeded) = await stdout;
            _ = await stderr;
            return new(process.ExitCode, output, exceeded);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new(-1, [], false);
        }
    }

    private async Task<StreamedBlobResult> RunBlobToFileAsync(
        IReadOnlyList<string> arguments,
        string destinationPath,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
            Task<byte[]> stderr = ReadBoundedAsync(process.StandardError.BaseStream, 4_096);
            long byteLength = 0;
            bool exceeded = false;
            bool strictUtf8 = true;
            Decoder decoder = _strictUtf8.GetDecoder();
            byte[] buffer = new byte[policy.VerificationBufferBytes];
            char[] characters = new char[_strictUtf8.GetMaxCharCount(buffer.Length)];
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                buffer.Length,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                int read;
                while ((read = await process.StandardOutput.BaseStream.ReadAsync(
                           buffer.AsMemory(),
                           cancellationToken)) > 0)
                {
                    byteLength = checked(byteLength + read);
                    if (byteLength > maximumBytes)
                    {
                        exceeded = true;
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                        break;
                    }

                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    if (strictUtf8)
                    {
                        try
                        {
                            decoder.Convert(
                                buffer,
                                0,
                                read,
                                characters,
                                0,
                                characters.Length,
                                flush: false,
                                out _,
                                out _,
                                out _);
                        }
                        catch (DecoderFallbackException)
                        {
                            strictUtf8 = false;
                        }
                    }
                }

                if (strictUtf8 && !exceeded)
                {
                    try
                    {
                        decoder.Convert(
                            [],
                            0,
                            0,
                            characters,
                            0,
                            characters.Length,
                            flush: true,
                            out _,
                            out _,
                            out _);
                    }
                    catch (DecoderFallbackException)
                    {
                        strictUtf8 = false;
                    }
                }
            }

            await process.WaitForExitAsync(CancellationToken.None);
            _ = await stderr;
            if (exceeded || process.ExitCode != 0)
            {
                File.Delete(destinationPath);
                return new(process.ExitCode, exceeded, byteLength, null, null, false);
            }

            string sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
            return new(process.ExitCode, false, byteLength, sha256, destinationPath, strictUtf8);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync(CancellationToken.None);
            TryDeleteFile(destinationPath);
            throw;
        }
        catch
        {
            TryDeleteFile(destinationPath);
            return new(-1, false, 0, null, null, false);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int maximumBytes)
    {
        (byte[] bytes, _) = await ReadBoundedWithOverflowAsync(stream, maximumBytes);
        return bytes;
    }

    private static async Task<(byte[] Bytes, bool Exceeded)> ReadBoundedWithOverflowAsync(
        Stream stream,
        int maximumBytes)
    {
        using var output = new MemoryStream(Math.Min(maximumBytes, 65_536));
        byte[] buffer = new byte[8_192];
        bool exceeded = false;
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            int retained = Math.Min(read, Math.Max(0, maximumBytes - checked((int)output.Length)));
            if (retained > 0)
            {
                output.Write(buffer, 0, retained);
            }
            exceeded |= retained != read;
        }
        return (output.ToArray(), exceeded);
    }

    private sealed record TreeEntry(string Mode, string ObjectId, byte[] PathBytes);
    private sealed record ProcessResult(int ExitCode, string StandardOutput);
    private sealed record BinaryProcessResult(int ExitCode, byte[] StandardOutput, bool ExceededBound);
    private sealed record StreamedBlobResult(
        int ExitCode,
        bool ExceededBound,
        long ByteLength,
        string? Sha256,
        string? Path,
        bool IsStrictUtf8);
}
