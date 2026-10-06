using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

public sealed record KnowledgeOperationLogEntry(
    KnowledgeBinding Binding,
    OperationKey Operation,
    KnowledgeWriteStatus Status);

/// <summary>Diagnostics are not commit authority; false leaves exact committed results recoverable.</summary>
public interface IOctoberKnowledgeOperationLog
{
    Task<bool> AppendAsync(KnowledgeOperationLogEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Host-owned log.md. No model or browser selects the path. An exclusive file handle
/// serializes cooperating writers across processes; the log never authorizes a mutation.
/// </summary>
public sealed class OctoberKnowledgeOperationLog : IOctoberKnowledgeOperationLog, IOctoberKnowledgeReadLog
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(20);
    private readonly string _directory;
    private readonly string _path;
    private readonly TimeProvider _clock;

    public OctoberKnowledgeOperationLog(string ownedDirectory, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedDirectory);
        if (!Path.IsPathFullyQualified(ownedDirectory) || !Directory.Exists(ownedDirectory))
        {
            throw new ArgumentException("An existing absolute host-owned directory is required.", nameof(ownedDirectory));
        }

        _directory = Path.GetFullPath(ownedDirectory);
        _path = Path.Combine(_directory, "log.md");
        _clock = timeProvider ?? TimeProvider.System;
    }

    public async Task<bool> AppendAsync(KnowledgeOperationLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry?.Binding is null || entry.Operation is null ||
            entry.Operation.BatchId == Guid.Empty || entry.Operation.OperationId == Guid.Empty ||
            entry.Binding.BatchId != entry.Operation.BatchId ||
            !Enum.IsDefined(entry.Operation.Kind) || !Enum.IsDefined(entry.Status) ||
            string.IsNullOrWhiteSpace(entry.Binding.TargetId) ||
            string.IsNullOrWhiteSpace(entry.Binding.CanonicalDatabaseIdentity) ||
            cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        // No raw target/database text, selection digest, content, exception message or host path.
        string line = string.Create(CultureInfo.InvariantCulture,
            $"- {_clock.GetUtcNow().ToUniversalTime():O} batch={entry.Operation.BatchId:D} operation={entry.Operation.OperationId:D} kind={entry.Operation.Kind} target-sha256={Token(entry.Binding.TargetId)} database-sha256={Token(entry.Binding.CanonicalDatabaseIdentity)} outcome={entry.Status}\n");
        return await AppendLineAsync(line, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> AppendLineAsync(string line, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(line);
        var elapsed = Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested && elapsed.Elapsed < LockTimeout)
        {
            FileStream stream;
            try
            {
                if (Directory.Exists(_path) ||
                    (File.GetAttributes(_directory) & FileAttributes.ReparsePoint) != 0 ||
                    (File.Exists(_path) && (File.GetAttributes(_path) & FileAttributes.ReparsePoint) != 0))
                {
                    return false;
                }

                stream = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                    FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                // Contention is bounded by a monotonic timeout, never by the diagnostic wall clock.
                try
                {
                    await Task.Delay(LockRetryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                continue;
            }

            try
            {
                await using (stream.ConfigureAwait(false))
                {
                    long previousLength = stream.Length;
                    if (previousLength > 0)
                    {
                        stream.Seek(-1, SeekOrigin.End);
                        if (stream.ReadByte() != '\n')
                        {
                            // Never append onto a pre-existing incomplete record or rewrite its history.
                            return false;
                        }
                    }

                    stream.Seek(0, SeekOrigin.End);
                    try
                    {
                        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                        stream.Flush(flushToDisk: true);
                        return true;
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OperationCanceledException)
                    {
                        // While exclusively held, undo only bytes from this failed candidate append.
                        // An I/O failure can also prevent this cleanup; the caller still receives failure.
                        try
                        {
                            stream.SetLength(previousLength);
                            stream.Flush(flushToDisk: true);
                        }
                        catch (Exception cleanupFailure) when (cleanupFailure is IOException or UnauthorizedAccessException)
                        {
                        }
                        return false;
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        return false;
    }

    public Task<bool> AppendAsync(KnowledgeReadLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry?.Publication?.Knowledge is not { } binding || binding.BatchId == Guid.Empty ||
            entry.Publication.PublicationId == Guid.Empty || entry.CorrelationId == Guid.Empty ||
            !Enum.IsDefined(entry.Operation) || !Enum.IsDefined(entry.Status) ||
            string.IsNullOrWhiteSpace(binding.TargetId) || string.IsNullOrWhiteSpace(binding.CanonicalDatabaseIdentity) ||
            cancellationToken.IsCancellationRequested)
            return Task.FromResult(false);

        string line = string.Create(CultureInfo.InvariantCulture,
            $"- {_clock.GetUtcNow().ToUniversalTime():O} batch={binding.BatchId:D} publication={entry.Publication.PublicationId:D} correlation={entry.CorrelationId:D} read={entry.Operation} target-sha256={Token(binding.TargetId)} database-sha256={Token(binding.CanonicalDatabaseIdentity)} outcome={entry.Status}\n");
        return AppendLineAsync(line, cancellationToken);
    }

    private static string Token(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
