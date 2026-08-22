using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Analysis.Source;

public sealed class RevisionBoundSourceReader(
    ICommittedRepositorySnapshotLease snapshotLease,
    string workingCopyBindingToken) : IRevisionBoundSourceReader
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private readonly ICommittedRepositorySnapshotLease _snapshotLease =
        snapshotLease ?? throw new ArgumentNullException(nameof(snapshotLease));
    private readonly string _workingCopyBindingToken =
        string.IsNullOrWhiteSpace(workingCopyBindingToken)
            ? throw new ArgumentException(
                "A working-copy binding token is required.",
                nameof(workingCopyBindingToken))
            : workingCopyBindingToken;

    public async Task<SourceExcerptResult> ReadAsync(
        AnalysisScope scope,
        RevisionBoundSourceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Failed(scope, request, AnalysisPortFailureReason.Cancelled, "source_read_cancelled");
        }

        if (scope.RepositoryId != _snapshotLease.RepositoryId ||
            scope.ScanRunId != _snapshotLease.ScanRunId)
        {
            return Failed(scope, request, AnalysisPortFailureReason.Rejected, "source_scope_rejected");
        }

        if (!string.Equals(
                scope.ProfiledRevision,
                _snapshotLease.ProfiledRevision,
                StringComparison.Ordinal))
        {
            return Failed(
                scope,
                request,
                AnalysisPortFailureReason.IntegrityMismatch,
                "source_revision_mismatch");
        }

        if (!string.Equals(
                scope.WorkingCopyBindingToken,
                _workingCopyBindingToken,
                StringComparison.Ordinal))
        {
            return Failed(scope, request, AnalysisPortFailureReason.Rejected, "source_binding_rejected");
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > scope.Deadline.UnixTimeMilliseconds)
        {
            return Failed(scope, request, AnalysisPortFailureReason.TimedOut, "source_read_timed_out");
        }

        try
        {
            await _snapshotLease.VerifyIdentityAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed(scope, request, AnalysisPortFailureReason.Cancelled, "source_read_cancelled");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException)
        {
            return Failed(
                scope,
                request,
                AnalysisPortFailureReason.IntegrityMismatch,
                "source_snapshot_stale");
        }

        string fullPath;
        try
        {
            string root = Path.GetFullPath(_snapshotLease.RootPath);
            if (!Directory.Exists(root) || HasReparsePoint(root, root))
            {
                return Failed(
                    scope,
                    request,
                    AnalysisPortFailureReason.IntegrityMismatch,
                    "source_snapshot_stale");
            }

            fullPath = Path.GetFullPath(Path.Combine(
                root,
                request.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            StringComparison pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!fullPath.StartsWith(rootPrefix, pathComparison))
            {
                return Failed(scope, request, AnalysisPortFailureReason.Rejected, "source_path_rejected");
            }

            if (!File.Exists(fullPath))
            {
                return Failed(scope, request, AnalysisPortFailureReason.Unavailable, "source_file_unavailable");
            }

            if (HasReparsePoint(root, fullPath))
            {
                return Failed(scope, request, AnalysisPortFailureReason.Rejected, "source_path_rejected");
            }

            FileAttributes attributes = File.GetAttributes(fullPath);
            if ((attributes & (FileAttributes.Directory | FileAttributes.Device |
                    FileAttributes.ReparsePoint)) != 0)
            {
                return Failed(scope, request, AnalysisPortFailureReason.Rejected, "source_path_rejected");
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException or
                IOException or UnauthorizedAccessException)
        {
            return Failed(scope, request, AnalysisPortFailureReason.Unavailable, "source_file_unavailable");
        }

        try
        {
            byte[] sourceBytes = await File.ReadAllBytesAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);
            string sourceHash = Convert.ToHexStringLower(SHA256.HashData(sourceBytes));
            if (!string.Equals(
                    sourceHash,
                    request.ExpectedSourceSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Failed(
                    scope,
                    request,
                    AnalysisPortFailureReason.IntegrityMismatch,
                    "source_hash_mismatch");
            }

            string sourceText = _strictUtf8.GetString(sourceBytes);
            if (!TryExtractRange(sourceText, request.Range, out string excerpt))
            {
                return Failed(scope, request, AnalysisPortFailureReason.Rejected, "source_range_rejected");
            }

            int byteCount = _strictUtf8.GetByteCount(excerpt);
            if (byteCount > request.ByteLimit)
            {
                return Failed(scope, request, AnalysisPortFailureReason.Rejected, "source_result_too_large");
            }

            return new SourceExcerptResult(
                AnalysisOperationStatus.Succeeded,
                scope.RepositoryId,
                scope.ProfiledRevision,
                request.RelativePath,
                request.Range,
                sourceHash,
                excerpt,
                byteCount,
                failure: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed(scope, request, AnalysisPortFailureReason.Cancelled, "source_read_cancelled");
        }
        catch (DecoderFallbackException)
        {
            return Failed(scope, request, AnalysisPortFailureReason.Rejected, "source_encoding_rejected");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return Failed(scope, request, AnalysisPortFailureReason.Unavailable, "source_file_unavailable");
        }
    }

    private static bool HasReparsePoint(string root, string path)
    {
        string relativePath = Path.GetRelativePath(root, path);
        string current = root;
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
        {
            return true;
        }

        if (relativePath == ".")
        {
            return false;
        }

        foreach (string segment in relativePath.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractRange(
        string sourceText,
        AnalysisLineRange range,
        out string excerpt)
    {
        string[] lines = sourceText.Split('\n');
        if (range.EndLine > lines.Length)
        {
            excerpt = string.Empty;
            return false;
        }

        string[] selectedLines = lines[(range.StartLine - 1)..range.EndLine];
        if (selectedLines.Length > 0 && selectedLines[^1].EndsWith('\r'))
        {
            selectedLines[^1] = selectedLines[^1][..^1];
        }

        excerpt = string.Join('\n', selectedLines);
        return !string.IsNullOrWhiteSpace(excerpt);
    }

    private static SourceExcerptResult Failed(
        AnalysisScope scope,
        RevisionBoundSourceRequest request,
        AnalysisPortFailureReason reason,
        string failureCode)
    {
        const string unavailableExcerpt = "analysis_unavailable";
        return new SourceExcerptResult(
            AnalysisOperationStatus.Failed,
            scope.RepositoryId,
            scope.ProfiledRevision,
            request.RelativePath,
            request.Range,
            request.ExpectedSourceSha256,
            unavailableExcerpt,
            Encoding.UTF8.GetByteCount(unavailableExcerpt),
            new AnalysisPortFailure(reason, failureCode, failureCode));
    }
}
