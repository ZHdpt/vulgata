using Vulgata.Tests.TestInfrastructure;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Xunit.Abstractions;

namespace Vulgata.Tests.RetainedSource;

/// <summary>
/// Builds the exact existing-project child invocation used by the later
/// separate-process qualification. The parent owns the fixture and delegates
/// capture and reopen to fresh testhost processes.
/// </summary>
internal static class RetainedSourceSeparateProcessRunner
{
    internal const string ProjectPath =
        "tests/Vulgata.Tests/Vulgata.Tests.csproj";

    internal const string ChildFilter =
        "FullyQualifiedName~RetainedSourceSeparateProcessChild";

    internal static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, ProjectPath)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "The retained-source test repository root could not be located.");
    }

    internal static ProcessStartInfo CreateStartInfo(
        string repositoryRoot,
        IReadOnlyDictionary<string, string?>? environment = null)
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
        startInfo.ArgumentList.Add("--settings");
        startInfo.ArgumentList.Add(Path.Combine(fullRepositoryRoot, "tests", "Vulgata.Tests", "explicit-suites.runsettings"));
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add(ChildFilter);
        foreach (string name in RetainedSourceSeparateProcessChild.ContractVariables)
        {
            startInfo.Environment.Remove(name);
        }
        startInfo.Environment[RetainedSourceSeparateProcessChild.ModeVariable] =
            RetainedSourceSeparateProcessChild.ModeValue;
        // A child receives a target database connection, never the administrator
        // credential used by the parent to create and dispose that database.
        foreach (string administratorVariable in new[]
        {
            "VULGATA_STORY131_POSTGRES_ADMIN", "VULGATA_TEST_POSTGRES_ADMIN",
            "VULGATA_TEST_ADMIN_CONNECTION", "VULGATA_TEST_POSTGRES", "VULGATA_TEST_OWNED_ADMIN",
        })
        {
            startInfo.Environment.Remove(administratorVariable);
        }
        if (environment is not null)
        {
            foreach ((string name, string? value) in environment)
            {
                if (value is null)
                {
                    startInfo.Environment.Remove(name);
                }
                else
                {
                    startInfo.Environment[name] = value;
                }
            }
        }
        return startInfo;
    }

    internal static async Task<RetainedSourceProcessResult> RunAsync(
        string repositoryRoot,
        IReadOnlyDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo(repositoryRoot, environment),
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
            process.Id,
            process.ExitCode,
            await outputTask,
            await errorTask);
    }
}

internal sealed record RetainedSourceProcessResult(
    int LauncherProcessId,
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal sealed record RetainedSourceChildResult(
    string Operation,
    int ProcessId,
    string Status,
    string Code,
    string? ProfiledRevision,
    string? ContentSha256,
    int? ContentByteLength,
    string? DeniedStatus,
    string? DeniedCode,
    string? DeniedOpenStatus,
    string? DeniedOpenCode);

[Collection(RetainedSourceProcessGlobalCollection.Name)]
public sealed class RetainedSourceSeparateProcessTests
{
    private readonly ITestOutputHelper _output;

    public RetainedSourceSeparateProcessTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CaptureThenReopen_WhenRemoteBecomesUnavailable_UsesFreshProductionComposition()
    {
        await using RetainedSourceCaptureApplicationFixture applicationFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                await RetainedSourceFixture.CreateAsync());
        RetainedSourceFixture source = applicationFixture.Source;
        string permittedRef = await source.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        string repositoryRoot = source.Repository.RootPath;
        string captureMarker = RetainedSourceOwnedPaths.ResolveChild(
            source.RootPath,
            "capture-child-result.json");
        string reopenMarker = RetainedSourceOwnedPaths.ResolveChild(
            source.RootPath,
            "reopen-child-result.json");
        byte[] expectedBytes = System.Text.Encoding.UTF8.GetBytes("retained-source-fixture\n");
        string expectedSha256 = Convert.ToHexString(SHA256.HashData(expectedBytes));

        RetainedSourceProcessResult captureProcess =
            await RetainedSourceSeparateProcessRunner.RunAsync(
                RetainedSourceSeparateProcessRunner.FindRepositoryRoot(),
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [RetainedSourceSeparateProcessChild.OperationVariable] =
                        RetainedSourceSeparateProcessChild.CaptureOperation,
                    [RetainedSourceSeparateProcessChild.ConnectionVariable] =
                        applicationFixture.Database.ConnectionString,
                    [RetainedSourceSeparateProcessChild.StorageRootVariable] =
                        source.SourceStorageRoot,
                    [RetainedSourceSeparateProcessChild.UserVariable] =
                        applicationFixture.UserId,
                    [RetainedSourceSeparateProcessChild.RepositoryVariable] =
                        source.RepositoryId.ToString("D"),
                    [RetainedSourceSeparateProcessChild.RefVariable] = permittedRef,
                    [RetainedSourceSeparateProcessChild.ResultVariable] = captureMarker,
                },
                new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
        Assert.Equal(0, captureProcess.ExitCode);
        RetainedSourceChildResult capture = ReadResult(captureMarker);
        Assert.Equal(RetainedSourceSeparateProcessChild.CaptureOperation, capture.Operation);
        Assert.Equal(SourceCaptureStatus.Completed.ToString(), capture.Status);
        Assert.Equal("source_completed", capture.Code);
        Assert.False(IsProcessRunning(capture.ProcessId));
        Assert.False(string.IsNullOrWhiteSpace(capture.ProfiledRevision));

        await RetainedSourceCaptureApplicationFixture.DeleteAuxiliaryTestRootsAsync(
            source.RootPath);

        string unavailableRoot = await source.Repository.MakeRemoteUnavailableAsync();
        Assert.False(Directory.Exists(repositoryRoot));
        Assert.True(Directory.Exists(unavailableRoot));
        _output.WriteLine(
            $"captureLauncherPid={captureProcess.LauncherProcessId}; " +
            $"captureTestHostPid={capture.ProcessId}; " +
            $"captureLauncherExited={captureProcess.ExitCode == 0}; " +
            $"captureTestHostExited={!IsProcessRunning(capture.ProcessId)}; " +
            $"remoteRootAbsent={!Directory.Exists(repositoryRoot)}; " +
            $"remoteUnavailableRootOwned={Directory.Exists(unavailableRoot)}; " +
            $"auxiliaryRootsAbsent={AreAuxiliaryTestRootsAbsent(source.RootPath)}");

        RetainedSourceProcessResult reopenProcess =
            await RetainedSourceSeparateProcessRunner.RunAsync(
                RetainedSourceSeparateProcessRunner.FindRepositoryRoot(),
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [RetainedSourceSeparateProcessChild.OperationVariable] =
                        RetainedSourceSeparateProcessChild.ReopenOperation,
                    [RetainedSourceSeparateProcessChild.ConnectionVariable] =
                        applicationFixture.Database.ConnectionString,
                    [RetainedSourceSeparateProcessChild.StorageRootVariable] =
                        source.SourceStorageRoot,
                    [RetainedSourceSeparateProcessChild.UserVariable] =
                        applicationFixture.UserId,
                    [RetainedSourceSeparateProcessChild.RepositoryVariable] =
                        source.RepositoryId.ToString("D"),
                    [RetainedSourceSeparateProcessChild.RefVariable] =
                        capture.ProfiledRevision,
                    [RetainedSourceSeparateProcessChild.ExpectedSha256Variable] =
                        expectedSha256,
                    [RetainedSourceSeparateProcessChild.ExpectedByteLengthVariable] =
                        expectedBytes.Length.ToString(),
                    [RetainedSourceSeparateProcessChild.ResultVariable] = reopenMarker,
                },
                new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
        Assert.Equal(0, reopenProcess.ExitCode);
        RetainedSourceChildResult reopen = ReadResult(reopenMarker);
        Assert.Equal(RetainedSourceSeparateProcessChild.ReopenOperation, reopen.Operation);
        Assert.Equal(SourceReadStatus.Succeeded.ToString(), reopen.Status);
        Assert.Equal("source_bytes_succeeded", reopen.Code);
        Assert.Equal(expectedSha256, reopen.ContentSha256);
        Assert.Equal(expectedBytes.Length, reopen.ContentByteLength);
        Assert.Equal(SourceReadStatus.Denied.ToString(), reopen.DeniedStatus);
        Assert.Equal("source_denied", reopen.DeniedCode);
        Assert.Equal(SourceReadStatus.Denied.ToString(), reopen.DeniedOpenStatus);
        Assert.Equal("source_denied", reopen.DeniedOpenCode);
        Assert.False(IsProcessRunning(reopen.ProcessId));
        _output.WriteLine(
            $"reopenLauncherPid={reopenProcess.LauncherProcessId}; " +
            $"reopenTestHostPid={reopen.ProcessId}; " +
            $"reopenLauncherExited={reopenProcess.ExitCode == 0}; " +
            $"reopenTestHostExited={!IsProcessRunning(reopen.ProcessId)}; " +
            $"remoteRootAbsent={!Directory.Exists(repositoryRoot)}; " +
            $"auxiliaryRootsAbsent={AreAuxiliaryTestRootsAbsent(source.RootPath)}; " +
            $"retainedSha256={reopen.ContentSha256}; " +
            $"retainedByteLength={reopen.ContentByteLength}; " +
            $"freshOpenDenied={reopen.DeniedOpenStatus}/{reopen.DeniedOpenCode}");
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CaptureThenOpen_WhenRetainedBlobIsCorrupt_DeniesWithoutRepair()
    {
        await using RetainedSourceCaptureApplicationFixture applicationFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                await RetainedSourceFixture.CreateAsync());
        RetainedSourceFixture source = applicationFixture.Source;
        string permittedRef = await source.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        string repositoryRoot = source.Repository.RootPath;
        string captureMarker = RetainedSourceOwnedPaths.ResolveChild(
            source.RootPath,
            "corruption-capture-child-result.json");
        string corruptionMarker = RetainedSourceOwnedPaths.ResolveChild(
            source.RootPath,
            "corruption-reopen-child-result.json");

        RetainedSourceProcessResult captureProcess =
            await RetainedSourceSeparateProcessRunner.RunAsync(
                RetainedSourceSeparateProcessRunner.FindRepositoryRoot(),
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [RetainedSourceSeparateProcessChild.OperationVariable] =
                        RetainedSourceSeparateProcessChild.CaptureOperation,
                    [RetainedSourceSeparateProcessChild.ConnectionVariable] =
                        applicationFixture.Database.ConnectionString,
                    [RetainedSourceSeparateProcessChild.StorageRootVariable] =
                        source.SourceStorageRoot,
                    [RetainedSourceSeparateProcessChild.UserVariable] =
                        applicationFixture.UserId,
                    [RetainedSourceSeparateProcessChild.RepositoryVariable] =
                        source.RepositoryId.ToString("D"),
                    [RetainedSourceSeparateProcessChild.RefVariable] = permittedRef,
                    [RetainedSourceSeparateProcessChild.ResultVariable] = captureMarker,
                },
                new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
        Assert.Equal(0, captureProcess.ExitCode);
        RetainedSourceChildResult capture = ReadResult(captureMarker);
        Assert.Equal(SourceCaptureStatus.Completed.ToString(), capture.Status);
        Assert.False(IsProcessRunning(capture.ProcessId));
        string revision = capture.ProfiledRevision ??
            throw new InvalidOperationException("The capture child did not record a revision.");

        await RetainedSourceCaptureApplicationFixture.DeleteAuxiliaryTestRootsAsync(
            source.RootPath);
        string unavailableRoot = await source.Repository.MakeRemoteUnavailableAsync();
        Assert.False(Directory.Exists(repositoryRoot));
        Assert.True(Directory.Exists(unavailableRoot));

        string identityRoot = Path.Combine(
            source.SourceStorageRoot,
            "snapshots",
            source.RepositoryId.ToString("N"),
            revision);
        string objectRoot = Assert.Single(Directory.GetDirectories(identityRoot));
        string blobRoot = Path.Combine(objectRoot, "blobs");
        string blobPath = Assert.Single(Directory.GetFiles(blobRoot));
        byte[] originalBlob = await File.ReadAllBytesAsync(blobPath);
        Assert.NotEmpty(originalBlob);
        byte[] corruptedBlob = [.. originalBlob];
        corruptedBlob[0] ^= 0xFF;
        await File.WriteAllBytesAsync(blobPath, corruptedBlob);

        RetainedSourceProcessResult corruptionProcess =
            await RetainedSourceSeparateProcessRunner.RunAsync(
                RetainedSourceSeparateProcessRunner.FindRepositoryRoot(),
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [RetainedSourceSeparateProcessChild.OperationVariable] =
                        RetainedSourceSeparateProcessChild.CorruptionOperation,
                    [RetainedSourceSeparateProcessChild.ConnectionVariable] =
                        applicationFixture.Database.ConnectionString,
                    [RetainedSourceSeparateProcessChild.StorageRootVariable] =
                        source.SourceStorageRoot,
                    [RetainedSourceSeparateProcessChild.UserVariable] =
                        applicationFixture.UserId,
                    [RetainedSourceSeparateProcessChild.RepositoryVariable] =
                        source.RepositoryId.ToString("D"),
                    [RetainedSourceSeparateProcessChild.RefVariable] = revision,
                    [RetainedSourceSeparateProcessChild.ResultVariable] = corruptionMarker,
                },
                new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
        Assert.Equal(0, corruptionProcess.ExitCode);
        RetainedSourceChildResult corruption = ReadResult(corruptionMarker);
        Assert.Equal(
            RetainedSourceSeparateProcessChild.CorruptionOperation,
            corruption.Operation);
        Assert.Equal(SourceReadStatus.IntegrityFailure.ToString(), corruption.Status);
        Assert.Equal("source_integrity_failed", corruption.Code);
        Assert.False(IsProcessRunning(corruption.ProcessId));
        byte[] finalBlob = await File.ReadAllBytesAsync(blobPath);
        Assert.Equal(corruptedBlob, finalBlob);
        _output.WriteLine(
            $"corruptionCaptureLauncherPid={captureProcess.LauncherProcessId}; " +
            $"corruptionCaptureTestHostPid={capture.ProcessId}; " +
            $"corruptionCaptureExited={captureProcess.ExitCode == 0 && !IsProcessRunning(capture.ProcessId)}; " +
            $"remoteRootAbsent={!Directory.Exists(repositoryRoot)}; " +
            $"auxiliaryRootsAbsent={AreAuxiliaryTestRootsAbsent(source.RootPath)}");
        _output.WriteLine(
            $"corruptionReopenLauncherPid={corruptionProcess.LauncherProcessId}; " +
            $"corruptionReopenTestHostPid={corruption.ProcessId}; " +
            $"corruptionReopenExited={corruptionProcess.ExitCode == 0 && !IsProcessRunning(corruption.ProcessId)}; " +
            $"corruptOpen={corruption.Status}/{corruption.Code}; " +
            $"corruptedBytesPreserved={corruptedBlob.SequenceEqual(finalBlob)}");
    }

    private static RetainedSourceChildResult ReadResult(string path)
    {
        Assert.True(File.Exists(path), $"Expected child result marker: {path}");
        return JsonSerializer.Deserialize<RetainedSourceChildResult>(
                File.ReadAllText(path)) ??
            throw new InvalidOperationException("The child result marker was empty.");
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool AreAuxiliaryTestRootsAbsent(string fixtureRoot)
    {
        string[] names = [".test-codegraph", ".test-jdtls", ".test-keyring"];
        return names.All(name => !Directory.Exists(
            RetainedSourceOwnedPaths.ResolveChild(fixtureRoot, name)));
    }
}
