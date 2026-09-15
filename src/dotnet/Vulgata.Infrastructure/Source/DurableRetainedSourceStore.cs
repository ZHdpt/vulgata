using System.Security.Cryptography;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Source;

internal sealed class RetainedSourceStorageException : Exception
{
}

internal sealed class RetainedSourceIntegrityException : Exception
{
}

internal sealed class RetainedSourceStaging : IAsyncDisposable
{
    internal RetainedSourceStaging(
        string operationRoot,
        string objectRoot,
        RetainedSourceManifestDocument manifest)
    {
        OperationRoot = operationRoot;
        ObjectRoot = objectRoot;
        Manifest = manifest;
    }

    internal string OperationRoot { get; }
    internal string ObjectRoot { get; }
    internal RetainedSourceManifestDocument Manifest { get; }

    public ValueTask DisposeAsync()
    {
        DurableRetainedSourceStore.TryDeleteOwnedOperation(OperationRoot);
        return ValueTask.CompletedTask;
    }
}

internal sealed record SealedRetainedSource(string InternalStorageKey, bool AlreadyExisted);

internal sealed record VerifiedRetainedSourceObject(
    SourceSnapshot Snapshot,
    RetainedSourceManifestDocument Manifest);

internal sealed record VerifiedRetainedSourceRange(byte[] Content);

internal sealed class DurableRetainedSourceStore(RetainedSourceCapturePolicy policy)
{
    internal async Task<RetainedSourceStaging> StageAsync(
        Guid repositoryId,
        CommittedSourceCapture capture,
        string operationRoot,
        CancellationToken cancellationToken)
    {
        string objectRoot = Path.Combine(operationRoot, "object");
        try
        {
            Directory.CreateDirectory(objectRoot);
            RetainedSourceManifestDocument manifest = RetainedSourceManifestCodec.Create(
                repositoryId,
                capture);
            foreach (RetainedSourceManifestEntry entry in manifest.Entries)
            {
                if (entry.StagedContentPath is null || entry.StorageBlobKey is null)
                {
                    continue;
                }

                string blobPath = ResolveContained(objectRoot, entry.StorageBlobKey);
                Directory.CreateDirectory(Path.GetDirectoryName(blobPath)!);
                if (!File.Exists(blobPath))
                {
                    File.Move(entry.StagedContentPath, blobPath);
                }
            }

            await File.WriteAllBytesAsync(
                ResolveContained(objectRoot, "manifest.json"),
                manifest.Bytes,
                cancellationToken);
            await VerifyObjectAsync(objectRoot, manifest, cancellationToken);
            return new(operationRoot, objectRoot, manifest);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RetainedSourceIntegrityException)
        {
            throw;
        }
        catch
        {
            throw new RetainedSourceStorageException();
        }
    }

    internal async Task<FileStream> AcquireIdentityLockAsync(
        Guid repositoryId,
        string profiledRevision,
        TimeProvider timeProvider,
        long startedTimestamp,
        CancellationToken cancellationToken)
    {
        string locksRoot = ResolveContained(policy.RootPath, ".locks");
        try
        {
            Directory.CreateDirectory(locksRoot);
            string lockPath = ResolveContained(
                locksRoot,
                $"{repositoryId:N}-{profiledRevision}.lock");
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (timeProvider.GetElapsedTime(startedTimestamp) >= policy.CaptureDeadline)
                {
                    throw new TimeoutException();
                }

                try
                {
                    return new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        1,
                        FileOptions.Asynchronous);
                }
                catch (IOException)
                {
                    await Task.Delay(25, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch
        {
            throw new RetainedSourceStorageException();
        }
    }

    internal async Task<SealedRetainedSource> SealAsync(
        Guid repositoryId,
        string profiledRevision,
        RetainedSourceStaging staging,
        CancellationToken cancellationToken)
    {
        string internalKey =
            $"snapshots/{repositoryId:N}/{profiledRevision}/{staging.Manifest.Sha256}";
        string destination = ResolveContained(policy.RootPath, internalKey);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (Directory.Exists(destination))
            {
                await VerifyObjectAsync(destination, staging.Manifest, cancellationToken);
                return new(internalKey, true);
            }

            Directory.Move(staging.ObjectRoot, destination);
            return new(internalKey, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RetainedSourceIntegrityException)
        {
            throw;
        }
        catch
        {
            throw new RetainedSourceStorageException();
        }
    }

    internal string CreateOperationRoot()
    {
        try
        {
            string operationsRoot = ResolveContained(policy.RootPath, ".operations");
            Directory.CreateDirectory(operationsRoot);
            string operationRoot = ResolveContained(operationsRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(operationRoot);
            return operationRoot;
        }
        catch
        {
            throw new RetainedSourceStorageException();
        }
    }

    internal async Task VerifySealedAsync(
        string internalStorageKey,
        RetainedSourceManifestDocument expected,
        CancellationToken cancellationToken) =>
        await VerifyObjectAsync(
            ResolveContained(policy.RootPath, internalStorageKey),
            expected,
            cancellationToken);

    internal async Task<VerifiedRetainedSourceObject> OpenVerifiedAsync(
        SourceSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        try
        {
            string objectRoot = ResolveContained(policy.RootPath, snapshot.InternalStorageKey);
            string manifestPath = ResolveContained(objectRoot, "manifest.json");
            if (!File.Exists(manifestPath) ||
                !await HasExpectedHashAsync(
                    manifestPath,
                    snapshot.ManifestSha256,
                    expectedLength: null,
                    cancellationToken))
            {
                throw new RetainedSourceIntegrityException();
            }

            await using var manifestStream = new FileStream(
                manifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                policy.VerificationBufferBytes,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            RetainedSourceManifestDocument manifest = await RetainedSourceManifestCodec.ParseAsync(
                manifestStream,
                cancellationToken);
            if (manifest.RepositoryId != snapshot.RepositoryId ||
                !StringComparer.Ordinal.Equals(manifest.ProfiledRevision, snapshot.ProfiledRevision) ||
                !StringComparer.Ordinal.Equals(manifest.GitObjectFormat, snapshot.GitObjectFormat) ||
                manifest.EntryCount != snapshot.EntryCount ||
                manifest.TotalBlobBytes != snapshot.TotalBlobBytes)
            {
                throw new RetainedSourceIntegrityException();
            }

            foreach (RetainedSourceManifestEntry entry in manifest.Entries)
            {
                if (entry.StorageBlobKey is null || entry.ContentSha256 is null)
                {
                    continue;
                }

                string blobPath = ResolveContained(objectRoot, entry.StorageBlobKey);
                if (!File.Exists(blobPath) || entry.ByteLength is null ||
                    !await HasExpectedHashAsync(
                        blobPath,
                        entry.ContentSha256,
                        entry.ByteLength.Value,
                        cancellationToken))
                {
                    throw new RetainedSourceIntegrityException();
                }
            }

            return new(snapshot, manifest);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RetainedSourceIntegrityException)
        {
            throw;
        }
        catch
        {
            throw new RetainedSourceStorageException();
        }
    }

    internal async Task VerifyManifestBindingAsync(
        SourceSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        try
        {
            string objectRoot = ResolveContained(policy.RootPath, snapshot.InternalStorageKey);
            string manifestPath = ResolveContained(objectRoot, "manifest.json");
            if (!File.Exists(manifestPath) ||
                !await HasExpectedHashAsync(
                    manifestPath,
                    snapshot.ManifestSha256,
                    expectedLength: null,
                    cancellationToken))
            {
                throw new RetainedSourceIntegrityException();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RetainedSourceIntegrityException)
        {
            throw;
        }
        catch
        {
            throw new RetainedSourceStorageException();
        }
    }

    internal async Task<VerifiedRetainedSourceRange> ReadVerifiedRangeAsync(
        SourceSnapshot snapshot,
        RetainedSourceManifestEntry entry,
        long byteOffset,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            if (entry.StorageBlobKey is null || entry.ContentSha256 is null ||
                entry.ByteLength is null || byteOffset < 0 || maximumBytes <= 0 ||
                byteOffset > entry.ByteLength.Value)
            {
                throw new RetainedSourceIntegrityException();
            }

            int resultLength = checked((int)Math.Min(
                maximumBytes,
                entry.ByteLength.Value - byteOffset));
            byte[] content = new byte[resultLength];
            byte[] buffer = new byte[policy.VerificationBufferBytes];
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            string objectRoot = ResolveContained(policy.RootPath, snapshot.InternalStorageKey);
            string blobPath = ResolveContained(objectRoot, entry.StorageBlobKey);
            await using var stream = new FileStream(
                blobPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                buffer.Length,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            long position = 0;
            int copied = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                long chunkEnd = checked(position + read);
                long requestedEnd = checked(byteOffset + resultLength);
                long copyStart = Math.Max(position, byteOffset);
                long copyEnd = Math.Min(chunkEnd, requestedEnd);
                if (copyStart < copyEnd)
                {
                    int sourceOffset = checked((int)(copyStart - position));
                    int copyLength = checked((int)(copyEnd - copyStart));
                    Buffer.BlockCopy(buffer, sourceOffset, content, copied, copyLength);
                    copied += copyLength;
                }
                position = chunkEnd;
                if (position > entry.ByteLength.Value)
                {
                    throw new RetainedSourceIntegrityException();
                }
            }

            if (position != entry.ByteLength.Value || copied != resultLength ||
                !CryptographicOperations.FixedTimeEquals(
                    hash.GetHashAndReset(),
                    Convert.FromHexString(entry.ContentSha256)))
            {
                throw new RetainedSourceIntegrityException();
            }

            return new(content);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RetainedSourceIntegrityException)
        {
            throw;
        }
        catch
        {
            throw new RetainedSourceStorageException();
        }
    }

    private async Task VerifyObjectAsync(
        string objectRoot,
        RetainedSourceManifestDocument manifest,
        CancellationToken cancellationToken)
    {
        string manifestPath = ResolveContained(objectRoot, "manifest.json");
        if (!File.Exists(manifestPath) ||
            !await HasExpectedHashAsync(
                manifestPath,
                manifest.Sha256,
                manifest.Bytes.LongLength,
                cancellationToken))
        {
            throw new RetainedSourceIntegrityException();
        }

        foreach (RetainedSourceManifestEntry entry in manifest.Entries)
        {
            if (entry.StorageBlobKey is null || entry.ContentSha256 is null)
            {
                continue;
            }

            string blobPath = ResolveContained(objectRoot, entry.StorageBlobKey);
            if (!File.Exists(blobPath) || entry.ByteLength is null ||
                !await HasExpectedHashAsync(
                    blobPath,
                    entry.ContentSha256,
                    entry.ByteLength.Value,
                    cancellationToken))
            {
                throw new RetainedSourceIntegrityException();
            }
        }
    }

    private async Task<bool> HasExpectedHashAsync(
        string path,
        string expectedSha256,
        long? expectedLength,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[policy.VerificationBufferBytes];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            buffer.Length,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        long length = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            length = checked(length + read);
            if (expectedLength is not null && length > expectedLength.Value)
            {
                return false;
            }
            hash.AppendData(buffer, 0, read);
        }

        return (expectedLength is null || length == expectedLength.Value) &&
            CryptographicOperations.FixedTimeEquals(
            hash.GetHashAndReset(),
            Convert.FromHexString(expectedSha256));
    }

    private static string ResolveContained(string root, string relativePath)
    {
        string normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string candidate = Path.GetFullPath(Path.Combine(
            normalizedRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = normalizedRoot + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!candidate.StartsWith(prefix, comparison))
        {
            throw new RetainedSourceStorageException();
        }
        return candidate;
    }

    internal static void TryDeleteOwnedOperation(string operationRoot)
    {
        try
        {
            if (Directory.Exists(operationRoot))
            {
                Directory.Delete(operationRoot, recursive: true);
            }
        }
        catch
        {
        }
    }
}
