using System.Text;

namespace Vulgata.Infrastructure.Git.BackupB;

/// <summary>
/// Reads source files directly from a pinned Git commit without invoking
/// CodeGraphContext. Computes file identity and source slice hashes.
/// </summary>
public sealed class BackupBGitSourceReader
{
    public async Task<BackupBGitSourceFile> ReadFileAsync(
            string repositoryPath,
            string exactCommit,
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath))
                throw new ArgumentException("Repository path cannot be empty", nameof(repositoryPath));
            if (string.IsNullOrWhiteSpace(exactCommit))
                throw new ArgumentException("Exact commit cannot be empty", nameof(exactCommit));
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("Relative path cannot be empty", nameof(relativePath));

            // Verify the commit exists
            var verifyResult = await RunGitAsync(
                repositoryPath,
                $"cat-file -t {exactCommit}",
                cancellationToken);

            if (verifyResult.ExitCode != 0 || verifyResult.Output.Trim() != "commit")
            {
                throw new InvalidOperationException(
                    $"Commit {exactCommit} not found in repository at {repositoryPath}");
            }

            // Read the file from the working tree. The demo repos have the exact
            // commit checked out as HEAD, and core.autocrlf normalizes line
            // endings the same way git archive does — so the bytes match what
            // CommittedRepositorySnapshotService produces for the hydrator.
            string workingTreePath = Path.Combine(
                repositoryPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(workingTreePath))
            {
                throw new InvalidOperationException(
                    $"File {relativePath} not found in working tree at {repositoryPath}");
            }

            byte[] fileBytes = await File.ReadAllBytesAsync(workingTreePath, cancellationToken);
            string content = Encoding.UTF8.GetString(fileBytes);
            var fileHash = ComputeSha256FromBytes(fileBytes);

            return new BackupBGitSourceFile(
                relativePath,
                exactCommit,
                content,
                fileHash);
        }

    public string ComputeSourceSliceHash(string content, int lineStart, int lineEnd)
    {
            // Match the hydrator's ReadExactSliceAsync behavior: extract lines
            // lineStart..lineEnd preserving original line endings between lines
            // (but not after the last line), then SHA-256 the UTF-8 bytes.
            var lines = content.Split('\n');
            if (lineStart < 1 || lineEnd > lines.Length || lineStart > lineEnd)
            {
                throw new ArgumentException(
                    $"Invalid line range [{lineStart}, {lineEnd}] for content with {lines.Length} lines");
            }

            // Reconstruct the slice the way the hydrator does: lines joined with
            // their original line endings. Since content may have CRLF (\r\n),
            // splitting on \n leaves \r at end of each line. The hydrator
            // includes \r\n between lines but not after the last line.
            var sliceLines = lines.Skip(lineStart - 1).Take(lineEnd - lineStart + 1).ToList();
            // Remove trailing \r from the last line (no line ending after last line)
            if (sliceLines.Count > 0 && sliceLines[^1].EndsWith('\r'))
            {
                sliceLines[^1] = sliceLines[^1][..^1];
            }
            var slice = string.Join("\n", sliceLines);
            return ComputeSha256(slice);
        }

        private static string ComputeSha256FromBytes(byte[] bytes)
        {
            // Match the hydrator: SHA-256 of the first 4096 bytes (or fewer
            // if the file is smaller).
            int prefixLength = Math.Min(bytes.Length, 4_096);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(bytes, 0, prefixLength);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

    private static string ComputeSha256(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<GitResult> RunGitAsync(
        string workingDirectory,
        string arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process();
        process.StartInfo = startInfo;
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new GitResult(process.ExitCode, output, error);
    }

    private sealed record GitResult(int ExitCode, string Output, string Error);
}

public sealed record BackupBGitSourceFile(
    string RelativePath,
    string Commit,
    string Content,
    string FileIdentityHash);
