using System.Diagnostics;

namespace Vulgata.Tests.RetainedSource;

/// <summary>
/// Builds the exact existing-project child invocation used by the later
/// separate-process qualification. This phase only provides process ownership
/// and argument/environment scaffolding; it does not assert reopen behavior.
/// </summary>
internal static class RetainedSourceSeparateProcessRunner
{
    internal const string ProjectPath =
        "tests/Vulgata.Tests/Vulgata.Tests.csproj";

    internal const string ChildFilter =
        "FullyQualifiedName~RetainedSourceSeparateProcessChild";

    internal static ProcessStartInfo CreateStartInfo(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        string fullRepositoryRoot = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(fullRepositoryRoot))
        {
            throw new DirectoryNotFoundException(
                "The retained-source child process working directory does not exist.");
        }

        string projectPath = Path.Combine(fullRepositoryRoot, ProjectPath);
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException(
                "The retained-source child process project cannot be found.",
                projectPath);
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = fullRepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("test");
        startInfo.ArgumentList.Add(ProjectPath);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add(ChildFilter);
        startInfo.Environment[RetainedSourceSeparateProcessChild.ModeVariable] =
            RetainedSourceSeparateProcessChild.ModeValue;
        return startInfo;
    }

    internal static async Task<RetainedSourceProcessResult> RunAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo(repositoryRoot),
        };
        if (!process.Start())
        {
            throw new InvalidOperationException(
                "The retained-source child test process could not be started.");
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The owned child already exited while cancellation was observed.
            }

            throw;
        }

        return new(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }
}

internal sealed record RetainedSourceProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
