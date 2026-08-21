using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Git;

public sealed class PreparedSnapshotSource(
    PreparedScanRevisionAcquirer revisionAcquirer,
    PreparedScanCandidateEnumerator candidateEnumerator,
    PreparedScanRawArtifactReader artifactReader) : IPreparedSnapshotSource
{
    public async Task<PreparedSnapshotReadResult> ReadAsync(
        PreparedRevisionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        try
        {
            ICommittedRepositorySnapshotLease lease = revisionAcquirer.RequireLease(
                snapshot.ContainedRootToken);
            await lease.VerifyIdentityAsync(cancellationToken);
            IReadOnlyList<PreparedScanCandidate> candidates =
                await candidateEnumerator.EnumerateAsync(lease.RootPath, cancellationToken);
            var files = new List<PreparedSnapshotFile>(candidates.Count);
            foreach (PreparedScanCandidate candidate in candidates)
            {
                if (!candidate.IsEligible)
                {
                    files.Add(new(
                        candidate.RelativePath,
                        candidate.Language,
                        false,
                        candidate.Outcome,
                        null,
                        null,
                        null,
                        null,
                        null,
                        ReadOnlyMemory<byte>.Empty));
                    continue;
                }

                try
                {
                    PreparedRawArtifact artifact = await artifactReader.ReadAsync(
                        lease.RootPath,
                        candidate.RelativePath,
                        snapshot.ProfiledRevision,
                        snapshot.SnapshotIdentity,
                        cancellationToken);
                    files.Add(new(
                        artifact.RelativePath,
                        candidate.Language,
                        true,
                        candidate.Outcome,
                        artifact.FileHash,
                        artifact.SourceHash,
                        artifact.RawByteCount,
                        artifact.LogicalLineCount,
                        artifact.EncodingIdentity,
                        artifact.RawBytes));
                }
                catch (PreparedRawArtifactRejectedException rejection)
                {
                    files.Add(new(
                        candidate.RelativePath,
                        candidate.Language,
                        false,
                        rejection.Outcome.ToString(),
                        null,
                        null,
                        rejection.RawByteCount,
                        null,
                        null,
                        ReadOnlyMemory<byte>.Empty));
                }
            }

            await lease.VerifyIdentityAsync(cancellationToken);
            return new(files, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidDataException or InvalidOperationException or
                IOException or UnauthorizedAccessException)
        {
            return new(
                [],
                new PreparedScanFailure(
                    PreparedScanFailureReason.UnsafeInput,
                    "prepared-snapshot-read-failed",
                    "The acquired snapshot could not be enumerated and read safely."));
        }
    }
}
