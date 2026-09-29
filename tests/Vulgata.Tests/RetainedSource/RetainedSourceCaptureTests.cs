using Vulgata.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.RetainedSource;

public sealed class RetainedSourceCaptureTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_AuthorizedSystemRepository_ResolvesFullRefAndReturnsCompletedIdentity()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, await PermittedRefAsync(fixture));

        Assert.Equal(SourceCaptureStatus.Completed, result.Status);
        Assert.Equal(fixture.RepositoryId, result.RepositoryId);
        Assert.Equal(await fixture.Repository.ResolveRevisionAsync(), result.ProfiledRevision);
        Assert.NotNull(result.ManifestSha256);
        Assert.NotNull(result.CompletedAt);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_AuthorizedStandaloneRepository_DoesNotRequireSystemOrPairIdentity()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture, standalone: true);

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, await PermittedRefAsync(fixture));

        Assert.Equal(SourceCaptureStatus.Completed, result.Status);
        Assert.Equal(fixture.RepositoryId, result.RepositoryId);
        Assert.Equal("sha1", result.GitObjectFormat);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_AssignedSystemOwner_CapturesSystemRepositoryWithoutAdministratorRole()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                administrator: false,
                systemOwner: true);

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, await PermittedRefAsync(fixture));

        Assert.Equal(SourceCaptureStatus.Completed, result.Status);
        Assert.Equal(fixture.RepositoryId, result.RepositoryId);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_AfterRefAndWorkingCopyMutation_RetainsOriginalRevisionIdentity()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string originalRevision = await fixture.Repository.ResolveRevisionAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, await PermittedRefAsync(fixture));

        fixture.Repository.WriteText("src/retained-source.txt", "changed working copy\n");
        await fixture.Repository.CommitAsync("changed ref fixture");

        Assert.Equal(SourceCaptureStatus.Completed, result.Status);
        Assert.Equal(originalRevision, result.ProfiledRevision);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("SourceStorage:RootPath", null)]
    [InlineData("SourceStorage:RootPath", "")]
    [InlineData("SourceStorage:MaximumEntries", "0")]
    [InlineData("SourceStorage:MaximumBlobBytes", "0")]
    [InlineData("SourceStorage:MaximumAggregateBlobBytes", "0")]
    [InlineData("SourceStorage:CaptureDeadlineSeconds", "0")]
    [InlineData("SourceStorage:OperationStaleAfterMinutes", "0")]
    [InlineData("SourceStorage:VerificationBufferBytes", "1023")]
    [InlineData("SourceStorage:MaximumAggregateBlobBytes", null)]
    [InlineData("SourceStorage:CaptureDeadlineSeconds", null)]
    public async Task Composition_InvalidSourceStorageConfiguration_FailsClosedBeforeCapture(
        string key,
        string? value)
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();

        Exception? exception = null;
        try
        {
            await using RetainedSourceCaptureApplicationFixture appFixture =
                await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                configurationOverrides: new Dictionary<string, string?> { [key] = value });
        }
        catch (Exception caught)
        {
            exception = caught;
        }
        Assert.NotNull(exception);
        Assert.Contains("source_configuration_invalid", exception!.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("entry", 1L, 8L, 16L)]
    [InlineData("blob", 10_000L, 1L, 16L)]
    [InlineData("aggregate", 10_000L, 8L, 12L)]
    public async Task AcquireAsync_ServerOwnedCaptureCapExhaustion_ReturnsDistinctSanitizedLimit(
        string exhaustedDimension,
        long maximumEntries,
        long maximumBlobBytes,
        long maximumAggregateBlobBytes)
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync(
                new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
                {
                    ["src/retained-source.txt"] = new byte[8],
                    ["src/second.txt"] = new byte[8],
                });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
            fixture,
            configurationOverrides: new Dictionary<string, string?>
            {
                ["SourceStorage:MaximumEntries"] = maximumEntries.ToString(),
                ["SourceStorage:MaximumBlobBytes"] = maximumBlobBytes.ToString(),
                ["SourceStorage:MaximumAggregateBlobBytes"] = maximumAggregateBlobBytes.ToString(),
            });

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, await PermittedRefAsync(fixture));

        Assert.Equal(SourceCaptureStatus.StorageFailure, result.Status);
        Assert.Equal("source_capture_limit_exceeded", result.Code);
        Assert.Null(result.ManifestSha256);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_ServerOwnedDeadlineExpiry_ReturnsDistinctDeadlineOutcome()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
            fixture,
            configurationOverrides: new Dictionary<string, string?>
            {
                ["SourceStorage:CaptureDeadlineSeconds"] = "1",
            },
            timeProvider: new AdvancingRetainedSourceTimeProvider(TimeSpan.FromSeconds(2)));

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, await PermittedRefAsync(fixture));

        Assert.Equal(SourceCaptureStatus.DeadlineExceeded, result.Status);
        Assert.Equal("source_deadline_exceeded", result.Code);
        Assert.Null(result.ManifestSha256);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_UnauthorizedSubject_DeniesBeforeGitResolution()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                authenticated: false);

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, "missing-ref");

        Assert.Equal(SourceCaptureStatus.Denied, result.Status);
        Assert.Equal("source_denied", result.Code);
        Assert.Null(result.ProfiledRevision);
        Assert.Null(result.ManifestSha256);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("missing-ref")]
    [InlineData("refs/heads/does-not-exist")]
    public async Task AcquireAsync_InvalidPermittedRef_ReturnsSanitizedAcquisitionFailure(
        string permittedRef)
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, permittedRef);

        Assert.Equal(SourceCaptureStatus.AcquisitionFailure, result.Status);
        Assert.Equal("source_ref_invalid", result.Code);
        Assert.Null(result.ManifestSha256);
        Assert.DoesNotContain(fixture.Repository.RootPath, result.Code, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_GitTransportFailure_ReturnsSanitizedAcquisitionFailure()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string permittedRef = await PermittedRefAsync(fixture);
        await fixture.Repository.DisposeAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, permittedRef);

        Assert.Equal(SourceCaptureStatus.AcquisitionFailure, result.Status);
        Assert.Equal("source_acquisition_failed", result.Code);
        Assert.DoesNotContain("example.invalid", result.Code, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.ManifestSha256);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_OriginRebindBeforePublication_DeniesWithoutCompletion()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        int mutationCount = 0;
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                mutateAfterFirstAuthorization: connectionString =>
                {
                    Interlocked.Increment(ref mutationCount);
                    return RetainedSourceCaptureApplicationFixture.RebindOriginAsync(
                        connectionString,
                        fixture.RepositoryId);
                });

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, await PermittedRefAsync(fixture));

        Assert.Equal(SourceCaptureStatus.Denied, result.Status);
        Assert.Equal("source_denied", result.Code);
        Assert.Null(result.ManifestSha256);
        Assert.Null(result.CompletedAt);
        Assert.Equal(1, mutationCount);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_SystemRebindBeforePublication_DeniesWithoutCompletion()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        Guid reboundSystemId = Guid.Parse("13000000-0000-4000-8000-000000000202");
        int mutationCount = 0;
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                mutateAfterFirstAuthorization: connectionString =>
                {
                    Interlocked.Increment(ref mutationCount);
                    return RetainedSourceCaptureApplicationFixture.RebindSystemAsync(
                        connectionString,
                        fixture.RepositoryId,
                        reboundSystemId);
                });

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, await PermittedRefAsync(fixture));

        Assert.Equal(SourceCaptureStatus.Denied, result.Status);
        Assert.Equal("source_denied", result.Code);
        Assert.Null(result.ManifestSha256);
        Assert.Null(result.CompletedAt);
        Assert.Equal(1, mutationCount);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_AuthorityRevokedBeforePublication_DeniesWithoutCompletion()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        int mutationCount = 0;
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                administrator: false,
                systemOwner: true,
                mutateAfterFirstAuthorization: connectionString =>
                {
                    Interlocked.Increment(ref mutationCount);
                    return RetainedSourceCaptureApplicationFixture.RevokeSystemAssignmentAsync(
                        connectionString,
                        Guid.Parse("13000000-0000-4000-8000-000000000201"));
                });

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, await PermittedRefAsync(fixture));

        Assert.Equal(SourceCaptureStatus.Denied, result.Status);
        Assert.Equal("source_denied", result.Code);
        Assert.Null(result.ManifestSha256);
        Assert.Null(result.CompletedAt);
        Assert.Equal(1, mutationCount);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_CancelledBeforeVerification_ReturnsCancelledWithoutCompletion()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        int mutationCount = 0;
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                mutateAfterFirstAuthorization: _ =>
                {
                    Interlocked.Increment(ref mutationCount);
                    cancellation.Cancel();
                    return Task.CompletedTask;
                });

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(
                fixture.RepositoryId,
                await PermittedRefAsync(fixture),
                cancellation.Token);

        Assert.Equal(SourceCaptureStatus.Cancelled, result.Status);
        Assert.Equal("source_cancelled", result.Code);
        Assert.Null(result.ManifestSha256);
        Assert.Null(result.CompletedAt);
        Assert.Equal(1, mutationCount);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_StorageRootUnavailable_ReturnsSanitizedStorageFailureWithoutPartialIdentity()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string storageFile = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "storage-file");
        File.WriteAllText(storageFile, "not a directory");
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
            fixture,
            configurationOverrides: new Dictionary<string, string?>
            {
                ["SourceStorage:RootPath"] = storageFile,
            });

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, await PermittedRefAsync(fixture));

        Assert.Equal(SourceCaptureStatus.StorageFailure, result.Status);
        Assert.Equal("source_storage_unavailable", result.Code);
        Assert.Null(result.ManifestSha256);
        Assert.DoesNotContain(storageFile, result.Code, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.SourceStorageRoot));
    }

    private static Task<string> PermittedRefAsync(RetainedSourceFixture fixture) =>
        fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);

}
