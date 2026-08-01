using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Hydrates one exact Code Unit slice from a trusted committed Repository
/// snapshot. The mutable working copy is used only to acquire the trusted lease.
/// </summary>
public sealed class RevisionBoundWorkerContextHydrator(
    ICommittedRepositorySnapshotService snapshotService)
{
    private static readonly Encoding _strictUtf8 =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public async Task<WorkerAgentExecutionContext> HydrateAsync(
        WorkerAgentJobPacket packet,
        Repository repository,
        GitWorkingCopyResult workingCopy,
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(workingCopy);
        ArgumentNullException.ThrowIfNull(accessScope);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Pass1CodeLogicJobPayload pass1 = RequireAuthorizedPacket(
                packet,
                repository,
                workingCopy,
                accessScope);
            await using ICommittedRepositorySnapshotLease lease =
                await snapshotService.AcquireAsync(
                    repository,
                    packet.ScanRunId,
                    workingCopy,
                    cancellationToken);
            RequireLeaseIdentity(lease, packet);

            string sourcePath = ResolveSnapshotFile(
                lease.RootPath,
                pass1.RepositoryRelativePath);
            (string sourceText, int sourceLineCount, byte[] fileIdentityHash) =
                await ReadExactSliceAsync(
                    sourcePath,
                    pass1.LineStart,
                    pass1.LineEnd,
                    cancellationToken);
            byte[] sourceHash = SHA256.HashData(_strictUtf8.GetBytes(sourceText));
            RequireHash(fileIdentityHash, pass1.FileIdentityHash);
            RequireHash(sourceHash, pass1.SourceHash);

            await lease.VerifyIdentityAsync(cancellationToken);
            RequireLeaseIdentity(lease, packet);
            return WorkerAgentExecutionContext.Create(
                packet,
                sourceText,
                sourceLineCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidDataException("Worker Agent 源上下文校验失败。");
        }
    }

    private static Pass1CodeLogicJobPayload RequireAuthorizedPacket(
        WorkerAgentJobPacket packet,
        Repository repository,
        GitWorkingCopyResult workingCopy,
        RepositoryAccessScope accessScope)
    {
        if (!accessScope.Contains(packet.RepositoryId) ||
            repository.Id != packet.RepositoryId ||
            repository.SystemId != packet.SystemId ||
            !workingCopy.Succeeded ||
            string.IsNullOrWhiteSpace(workingCopy.WorkingCopyPath) ||
            !StringComparer.Ordinal.Equals(
                workingCopy.ProfiledRevision,
                packet.ProfiledRevision) ||
            packet.Payload is not Pass1CodeLogicJobPayload pass1 ||
            pass1.CodeUnitId != packet.CodeUnitId ||
            !ReferenceEquals(pass1.SourceSlice, packet.SourceSlice))
        {
            throw new InvalidDataException("Worker Agent 源上下文校验失败。");
        }

        return pass1;
    }

    private static void RequireLeaseIdentity(
        ICommittedRepositorySnapshotLease lease,
        WorkerAgentJobPacket packet)
    {
        if (lease.RepositoryId != packet.RepositoryId ||
            lease.ScanRunId != packet.ScanRunId ||
            !StringComparer.Ordinal.Equals(
                lease.ProfiledRevision,
                packet.ProfiledRevision))
        {
            throw new InvalidOperationException(
                "Committed Repository snapshot identity does not match the Worker Agent job.");
        }
    }

    private static string ResolveSnapshotFile(string snapshotRoot, string relativePath)
    {
        string normalizedRoot = Path.GetFullPath(snapshotRoot);
        if (!Directory.Exists(normalizedRoot) ||
            (File.GetAttributes(normalizedRoot) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Committed Repository snapshot root is unavailable.");
        }

        string[] segments = relativePath.Split('/');
        if (Path.IsPathFullyQualified(relativePath) ||
            (relativePath.Length >= 2 &&
                char.IsAsciiLetter(relativePath[0]) &&
                relativePath[1] == ':') ||
            relativePath.Contains('\\', StringComparison.Ordinal) ||
            segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new InvalidDataException("Repository-relative source path is invalid.");
        }

        string sourcePath = Path.GetFullPath(
            Path.Combine(normalizedRoot, Path.Combine(segments)));
        string rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        if (!sourcePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Repository-relative source path escapes its snapshot.");
        }

        string? current = sourcePath;
        while (current is not null &&
            !StringComparer.OrdinalIgnoreCase.Equals(current, normalizedRoot))
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException(
                        "Committed Repository snapshot source contains a reparse point.");
                }
            }

            current = Path.GetDirectoryName(current);
        }

        if (!File.Exists(sourcePath))
        {
            throw new InvalidDataException("Committed Repository snapshot source is unavailable.");
        }

        return sourcePath;
    }

    private static async Task<(string SourceText, int LineCount, byte[] FileIdentityHash)>
        ReadExactSliceAsync(
            string sourcePath,
            int lineStart,
            int lineEnd,
            CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] identityPrefix = new byte[Math.Min(4_096, checked((int)Math.Min(stream.Length, 4_096)))];
        int prefixRead = 0;
        while (prefixRead < identityPrefix.Length)
        {
            int read = await stream.ReadAsync(
                identityPrefix.AsMemory(prefixRead),
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            prefixRead += read;
        }

        byte[] fileIdentityHash = SHA256.HashData(identityPrefix.AsSpan(0, prefixRead));
        stream.Position = 0;
        using var reader = new StreamReader(
            stream,
            _strictUtf8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4_096,
            leaveOpen: true);
        var source = new StringBuilder();
        char[] buffer = new char[4_096];
        int currentLine = 1;
        bool previousWasCarriageReturn = false;
        bool endedWithLineBreak = false;
        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }

            for (int index = 0; index < read; index++)
            {
                char character = buffer[index];
                if (previousWasCarriageReturn)
                {
                    previousWasCarriageReturn = false;
                    if (character == '\n')
                    {
                        if (currentLine > lineStart && currentLine <= lineEnd)
                        {
                            source.Append(character);
                        }

                        endedWithLineBreak = true;
                        continue;
                    }
                }

                if (character is '\r' or '\n')
                {
                    if (currentLine == lineEnd)
                    {
                        return CompleteSlice(source, lineStart, lineEnd, fileIdentityHash);
                    }

                    if (currentLine >= lineStart)
                    {
                        source.Append(character);
                    }

                    currentLine++;
                    previousWasCarriageReturn = character == '\r';
                    endedWithLineBreak = true;
                }
                else
                {
                    if (currentLine >= lineStart && currentLine <= lineEnd)
                    {
                        source.Append(character);
                        if (source.Length > WorkerAgentContractLimits.MaximumSourceSliceUtf8Bytes ||
                            ((source.Length % 256 == 0) &&
                                _strictUtf8.GetByteCount(source.ToString()) >
                                    WorkerAgentContractLimits.MaximumSourceSliceUtf8Bytes))
                        {
                            throw new InvalidDataException(
                                "Hydrated source exceeds its UTF-8 byte bound.");
                        }
                    }

                    endedWithLineBreak = false;
                }
            }
        }

        int availableLineCount = endedWithLineBreak
            ? Math.Max(1, currentLine - 1)
            : currentLine;
        if (lineEnd > availableLineCount)
        {
            throw new InvalidDataException("Requested source range is outside the committed file.");
        }

        return CompleteSlice(source, lineStart, lineEnd, fileIdentityHash);
    }

    private static (string SourceText, int LineCount, byte[] FileIdentityHash) CompleteSlice(
        StringBuilder source,
        int lineStart,
        int lineEnd,
        byte[] fileIdentityHash)
    {
        string sourceText = source.ToString();
        if (_strictUtf8.GetByteCount(sourceText) >
            WorkerAgentContractLimits.MaximumSourceSliceUtf8Bytes)
        {
            throw new InvalidDataException("Hydrated source exceeds its UTF-8 byte bound.");
        }

        return (sourceText, lineEnd - lineStart + 1, fileIdentityHash);
    }

    private static void RequireHash(byte[] actual, string expectedHex)
    {
        byte[] expected = Convert.FromHexString(expectedHex);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            throw new InvalidDataException("Committed source identity does not match the Worker Agent job.");
        }
    }
}
