using System.Diagnostics;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Git;

public sealed record GitWorkingCopyOptions(string RootPath);

public sealed class GitWorkingCopyService(GitWorkingCopyOptions options) : IGitWorkingCopyService
{
    private const int _maximumProfiledRevisionOutputCharacters = 66;

    public async Task<GitWorkingCopyResult> AcquireAsync(
        Repository repository,
        Guid scanRunId,
        CancellationToken cancellationToken = default)
    {
        string repositoryRoot = Path.GetFullPath(options.RootPath);
        string workingCopyPath = Path.Combine(repositoryRoot, repository.Id.ToString("N"));
        Directory.CreateDirectory(repositoryRoot);

        ProcessResult result;
        if (Directory.Exists(Path.Combine(workingCopyPath, ".git")))
        {
            ProcessResult cleanBeforePull = await RunGitAsync(
                ["-C", workingCopyPath, "status", "--porcelain=v1", "--untracked-files=all"],
                cancellationToken);
            if (cleanBeforePull.ExitCode != 0 || cleanBeforePull.StandardOutput.Length != 0)
            {
                return GitWorkingCopyResult.Failure("本地工作副本包含未提交更改。");
            }

            ProcessResult boundRemote = await RunGitAsync(
                ["-C", workingCopyPath, "remote", "get-url", "origin"],
                cancellationToken,
                maximumStandardOutputCharacters: 4_096,
                requireCompleteStandardOutput: true);
            result = boundRemote.ExitCode == 0 &&
                StringComparer.Ordinal.Equals(
                    boundRemote.StandardOutput.TrimEnd('\r', '\n'),
                    repository.GitUrl)
                ? await RunGitAsync(
                    ["-C", workingCopyPath, "pull", "--ff-only"],
                    cancellationToken)
                : await RecreateAsync(
                    repositoryRoot,
                    workingCopyPath,
                    repository.GitUrl,
                    cancellationToken);
        }
        else
        {
            if (Directory.Exists(workingCopyPath) && Directory.EnumerateFileSystemEntries(workingCopyPath).Any())
            {
                return GitWorkingCopyResult.Failure("本地工作副本目录不可用。");
            }

            result = await RunGitAsync(
                ["clone", "--no-tags", "--", repository.GitUrl, workingCopyPath],
                cancellationToken);
        }

        if (result.ExitCode != 0)
        {
            return GitWorkingCopyResult.Failure(SanitizeDiagnostic(result.StandardError));
        }

        ProcessResult clean = await RunGitAsync(
            ["-C", workingCopyPath, "status", "--porcelain=v1", "--untracked-files=all"],
            cancellationToken);
        if (clean.ExitCode != 0 || clean.StandardOutput.Length != 0)
        {
            return GitWorkingCopyResult.Failure("本地工作副本包含未提交更改。");
        }

        ProcessResult revision = await RunGitAsync(
            ["-C", workingCopyPath, "rev-parse", "--verify", "HEAD"],
            cancellationToken,
            _maximumProfiledRevisionOutputCharacters,
            requireCompleteStandardOutput: true);
        return revision.ExitCode == 0 &&
            TryParseProfiledRevision(revision.StandardOutput, out string? profiledRevision)
            ? GitWorkingCopyResult.Success(workingCopyPath, profiledRevision!)
            : GitWorkingCopyResult.Failure(SanitizeDiagnostic(revision.StandardError));
    }

    private static async Task<ProcessResult> RecreateAsync(
        string repositoryRoot,
        string workingCopyPath,
        string gitUrl,
        CancellationToken cancellationToken)
    {
        string suffix = Guid.NewGuid().ToString("N");
        string replacementPath = Path.Combine(repositoryRoot, $".rebind-{suffix}");
        string stalePath = Path.Combine(repositoryRoot, $".stale-{suffix}");
        ProcessResult clone = await RunGitAsync(
            ["clone", "--no-tags", "--", gitUrl, replacementPath],
            cancellationToken);
        if (clone.ExitCode != 0)
        {
            _ = TryDeleteDirectory(replacementPath);
            return clone;
        }

        try
        {
            Directory.Move(workingCopyPath, stalePath);
            try
            {
                Directory.Move(replacementPath, workingCopyPath);
            }
            catch
            {
                Directory.Move(stalePath, workingCopyPath);
                throw;
            }

            return TryDeleteDirectory(stalePath)
                ? clone
                : new ProcessResult(-1, string.Empty, "WorkingCopyCleanupFailed");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            _ = TryDeleteDirectory(replacementPath);
            return new ProcessResult(-1, string.Empty, exception.GetType().Name);
        }
    }

    private static bool TryDeleteDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return true;
            }

            var enumerationOptions = new EnumerationOptions
            {
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = false,
                RecurseSubdirectories = true,
            };
            foreach (string file in Directory.EnumerateFiles(path, "*", enumerationOptions))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(path, recursive: true);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static bool TryParseProfiledRevision(string output, out string? revision)
    {
        revision = output.TrimEnd('\r', '\n');
        if (revision.Length == 0 ||
            revision.Length > 64 ||
            output.AsSpan(revision.Length).ContainsAnyExcept('\r', '\n') ||
            revision.Any(static character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            revision = null;
            return false;
        }

        return true;
    }

    internal static async Task<ProcessResult> RunGitAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        int maximumStandardOutputCharacters = 4_096,
        bool requireCompleteStandardOutput = false)
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
            Task<string> stdoutTask = ReadBoundedAsync(
                process.StandardOutput,
                maximumStandardOutputCharacters,
                requireCompleteStandardOutput,
                CancellationToken.None);
            Task<string> stderrTask = ReadBoundedAsync(
                process.StandardError,
                4_096,
                requireComplete: false,
                CancellationToken.None);
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await TerminateProcessTreeAsync(process);
                await Task.WhenAll(stdoutTask, stderrTask);
                throw;
            }

            return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ProcessResult(-1, string.Empty, exception.GetType().Name);
        }
    }

    private static async Task TerminateProcessTreeAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }

        await process.WaitForExitAsync(CancellationToken.None);
    }

    internal static async Task<string> ReadBoundedAsync(
        StreamReader reader,
        int maximumCharacters,
        bool requireComplete,
        CancellationToken cancellationToken)
    {
        char[] buffer = new char[1_024];
        var retained = new System.Text.StringBuilder(Math.Min(maximumCharacters, 4_096));
        int totalCharacters = 0;
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            totalCharacters = checked(totalCharacters + read);
            if (retained.Length < maximumCharacters)
            {
                retained.Append(buffer, 0, Math.Min(read, maximumCharacters - retained.Length));
            }
        }

        if (requireComplete && totalCharacters > maximumCharacters)
        {
            throw new InvalidDataException("Git 输出超过安全限制。");
        }

        return retained.ToString();
    }

    internal static string SanitizeDiagnostic(string value) =>
        "Git 命令执行失败，请检查仓库地址和访问权限。";

    internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
