using Vulgata.Tests.TestInfrastructure;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Git;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T017 RED coverage for immutable raw artifacts.  The test asserts the
/// accepted public seam before T018 supplies the implementation; this avoids
/// introducing a test-owned hashing helper that could mask production drift.
/// </summary>
public sealed class PreparedRawArtifactTests
{
    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void PreparedRawArtifactReader_ExposesRawByteBoundedReadPort()
    {
        Type reader = RequirePublicType(
            typeof(ExactGitRevisionSnapshotService).Assembly,
            "Vulgata.Infrastructure.Git.PreparedScanRawArtifactReader");

        MethodInfo read = Assert.Single(
            reader.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "ReadAsync");

        Assert.True(typeof(Task).IsAssignableFrom(read.ReturnType));
        Assert.Contains(
            read.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));
        Assert.Contains(
            read.GetParameters(),
            parameter => parameter.ParameterType == typeof(string));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void PreparedRawArtifactReader_ReturnModelCarriesBothExactHashesAndSnapshotBinding()
    {
        Type reader = RequirePublicType(
            typeof(ExactGitRevisionSnapshotService).Assembly,
            "Vulgata.Infrastructure.Git.PreparedScanRawArtifactReader");
        MethodInfo read = Assert.Single(
            reader.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "ReadAsync");
        Type resultType = UnwrapTask(read.ReturnType);

        Assert.Contains(
            resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == "FileHash");
        Assert.Contains(
            resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == "SourceHash");
        Assert.Contains(
            resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "ProfiledRevision" or "SnapshotIdentity");
        Assert.DoesNotContain(
            resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("AbsolutePath", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedSnapshotSource_BinaryEligibleArtifact_RecordsClosedOutcomeAndContinuesSafeFiles()
    {
        string fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            $"vulgata-story122-t057-{Guid.NewGuid():N}");
        string origin = Path.Combine(fixtureRoot, "origin");
        string storage = Path.Combine(fixtureRoot, "storage");
        Directory.CreateDirectory(origin);
        try
        {
            await RunProcessAsync("git", ["init", "--quiet", "--initial-branch=main"], origin);
            string safePath = Path.Combine(origin, "src", "main", "java", "Safe.java");
            string binaryPath = Path.Combine(origin, "src", "main", "java", "Binary.java");
            Directory.CreateDirectory(Path.GetDirectoryName(safePath)!);
            await File.WriteAllTextAsync(
                safePath,
                "package main;\npublic final class Safe { public int value() { return 1; } }\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await File.WriteAllBytesAsync(binaryPath, [0x00, 0x01, 0x02, 0x03]);
            await RunProcessAsync("git", ["add", "--all"], origin);
            await RunProcessAsync(
                "git",
                [
                    "-c", "user.name=Vulgata Qualification",
                    "-c", "user.email=qualification@invalid",
                    "commit", "--quiet", "-m", "t057",
                ],
                origin);
            string revision = (await RunProcessAsync(
                "git", ["rev-parse", "refs/heads/main"], origin)).Trim();

            await using var acquirer = new PreparedScanRevisionAcquirer(
                new ExactGitRevisionSnapshotService(new GitWorkingCopyOptions(storage)));
            IPreparedRevisionAcquirer revisionAcquirer = acquirer;
            PreparedRevisionAcquisitionResult acquisition = await revisionAcquirer.AcquireAsync(
                new PreparedRevisionRequest(
                    Guid.Parse("12000000-0000-4000-8000-000000000057"),
                    Guid.Parse("22000000-0000-4000-8000-000000000057"),
                    origin,
                    "refs/heads/main",
                    revision),
                CancellationToken.None);
            Assert.Null(acquisition.Failure);
            PreparedRevisionSnapshot snapshot = Assert.IsType<PreparedRevisionSnapshot>(
                acquisition.Snapshot);

            var source = new PreparedSnapshotSource(
                acquirer,
                new PreparedScanCandidateEnumerator(),
                new PreparedScanRawArtifactReader());
            PreparedSnapshotReadResult result = await source.ReadAsync(
                snapshot,
                CancellationToken.None);

            Assert.Null(result.Failure);
            Assert.Equal(2, result.Files.Count);
            PreparedSnapshotFile safe = Assert.Single(
                result.Files,
                file => file.RelativePath == "src/main/java/Safe.java");
            Assert.True(safe.IsEligible);
            Assert.Equal("EligibleProductionSource", safe.Outcome);
            Assert.NotNull(safe.FileHash);
            Assert.NotNull(safe.SourceHash);

            PreparedSnapshotFile rejected = Assert.Single(
                result.Files,
                file => file.RelativePath == "src/main/java/Binary.java");
            Assert.False(rejected.IsEligible);
            Assert.Equal("BinaryJava", rejected.Outcome);
            Assert.Null(rejected.FileHash);
            Assert.Null(rejected.SourceHash);
            Assert.Empty(rejected.RawBytes.ToArray());
        }
        finally
        {
            DeleteDirectory(fixtureRoot);
        }
    }

    private static Type UnwrapTask(Type returnType)
    {
        Assert.True(returnType.IsGenericType);
        Assert.Equal(typeof(Task<>), returnType.GetGenericTypeDefinition());
        return returnType.GetGenericArguments()[0];
    }

    private static Type RequirePublicType(Assembly assembly, string fullName)
    {
        Type? type = assembly.GetType(fullName);
        Assert.NotNull(type);
        Assert.True(type!.IsPublic, $"{fullName} must be a public raw-artifact seam.");
        Assert.False(type.IsAbstract);
        return type;
    }

    private static async Task<string> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"{fileName} did not start.");
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"{fileName} exited {process.ExitCode}: {error}");
        return output;
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        for (int attempt = 0; attempt < 10 && Directory.Exists(path); attempt++)
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(
                         path,
                         "*",
                         SearchOption.AllDirectories))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
                }
            }

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (IOException) when (attempt < 9)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
            catch (UnauthorizedAccessException) when (attempt < 9)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }

        Assert.False(Directory.Exists(path));
    }
}
