using Vulgata.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Xunit.Abstractions;

namespace Vulgata.Tests.RetainedSource;

[Collection(RetainedSourceProcessGlobalCollection.Name)]
public sealed class RetainedSourceRootMappingTests
{
    private readonly ITestOutputHelper _output;

    public RetainedSourceRootMappingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task OpenReadContextAsync_VolumeRootConfiguration_DoesNotResolveCwdAliasAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string currentDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());
        string volumeRoot = Path.GetPathRoot(currentDirectory) ??
            throw new InvalidOperationException("The current test directory has no volume root.");
        if (string.Equals(
                Path.GetPathRoot(Path.GetTempPath()),
                volumeRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The root-mapping test requires an owned temporary path on a different drive.");
        }

        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture captureFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true);
        IRetainedSourceApplication captureApplication = captureFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await captureApplication.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);

        SourceSnapshot snapshot = await LoadSnapshotAsync(
            captureFixture,
            fixture.RepositoryId,
            capture.ProfiledRevision!);
        string sourceObject = Path.Combine(
            fixture.SourceStorageRoot,
            snapshot.InternalStorageKey.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(Directory.Exists(sourceObject));

        string aliasGuard = Path.Combine(
            currentDirectory,
            $"root-mapping-alias-{Guid.NewGuid():N}");
        string aliasObject = Path.Combine(
            currentDirectory,
            snapshot.InternalStorageKey.Replace('/', Path.DirectorySeparatorChar));
        string aliasParent = Path.GetDirectoryName(aliasObject) ??
            throw new InvalidOperationException("The root-mapping alias has no parent.");
        if (Directory.Exists(aliasParent) || File.Exists(aliasParent))
        {
            throw new InvalidOperationException(
                "The guarded cwd-relative root-mapping alias already exists.");
        }

        Directory.CreateDirectory(aliasGuard);
        Directory.CreateDirectory(aliasParent);
        try
        {
            CopyDirectory(sourceObject, aliasObject);
            string keyRingPath = RetainedSourceOwnedPaths.ResolveChild(
                fixture.RootPath,
                "root-mapping-keyring");
            string codeGraphRoot = RetainedSourceOwnedPaths.ResolveChild(
                fixture.RootPath,
                "root-mapping-codegraph");
            string jdtLsRoot = RetainedSourceOwnedPaths.ResolveChild(
                fixture.RootPath,
                "root-mapping-jdtls");
            Directory.CreateDirectory(codeGraphRoot);
            Directory.CreateDirectory(jdtLsRoot);
            IReadOnlyDictionary<string, string?> configuration =
                new Dictionary<string, string?>
                {
                    ["CodeGraph:DataRootPath"] = codeGraphRoot,
                    ["AnalysisRuntime:JdtLsWorkspaceRoot"] = jdtLsRoot,
                };
            await using RetainedSourceAttachedApplication readApplication =
                RetainedSourceCaptureApplicationFixture.CreateAttached(
                    captureFixture.Database.ConnectionString,
                    volumeRoot,
                    captureFixture.UserId,
                    configuration,
                    dataProtectionKeyRingPath: keyRingPath);
            IRetainedSourceApplication reader = readApplication.Scope.ServiceProvider
                .GetRequiredService<IRetainedSourceApplication>();

            OpenSourceReadContextResult reopened = await reader.OpenReadContextAsync(
                fixture.RepositoryId,
                snapshot.ProfiledRevision,
                new SourceReadGrantRequest(),
                testCancellation.Token);
            string volumeObject = Path.Combine(
                volumeRoot,
                snapshot.InternalStorageKey.Replace('/', Path.DirectorySeparatorChar));
            _ = File.GetAttributes(aliasObject);
            bool volumeSnapshotExists = Directory.Exists(volumeObject);
            _output.WriteLine(
                $"capture={capture.Status}/{capture.Code}; reopened={reopened.Status}/{reopened.Code}; " +
                $"context={reopened.Context is not null}; aliasPresent={Directory.Exists(aliasObject)}; " +
                $"volumeSnapshotExists={volumeSnapshotExists}; guardCreated=True");

            Assert.NotEqual(SourceReadStatus.Succeeded, reopened.Status);
            Assert.Null(reopened.Context);
            Assert.False(volumeSnapshotExists);
        }
        finally
        {
            DeleteCreatedDirectory(aliasObject, aliasParent, currentDirectory);
            DeleteCreatedDirectory(aliasGuard, aliasGuard, currentDirectory);
        }
    }

    private static async Task<SourceSnapshot> LoadSnapshotAsync(
        RetainedSourceCaptureApplicationFixture fixture,
        Guid repositoryId,
        string profiledRevision)
    {
        IDbContextFactory<VulgataDbContext> factory = fixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        return await context.SourceSnapshots.SingleAsync(snapshot =>
            snapshot.RepositoryId == repositoryId &&
            snapshot.ProfiledRevision == profiledRevision);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)));
        }

        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        }
    }

    private static void DeleteCreatedDirectory(
        string path,
        string parent,
        string currentDirectory)
    {
        string fullPath = Path.GetFullPath(path);
        string fullParent = Path.GetFullPath(parent);
        string prefix = currentDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !fullParent.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, Path.GetFullPath(currentDirectory), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The root-mapping cleanup path is not guarded.");
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }

        if (!string.Equals(fullParent, fullPath, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(fullParent) &&
            !Directory.EnumerateFileSystemEntries(fullParent).Any())
        {
            Directory.Delete(fullParent);
        }
    }
}
