using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.RetainedSource;

public sealed class RetainedSourceReadFailureTests
{
    [Fact]
    public async Task OpenReadContextAsync_WhenAuthorizationInfrastructureThrows_ReturnsSanitizedStorageFailure()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture captureApplication =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        IRetainedSourceApplication capture = captureApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        SourceCaptureResult captured = await capture.AcquireAsync(
            fixture.RepositoryId,
            await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));
        Assert.Equal(SourceCaptureStatus.Completed, captured.Status);
        string revision = captured.ProfiledRevision ??
                          throw new InvalidOperationException("The source capture did not return a revision.");

        await using RetainedSourceAttachedApplication failingApplication =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                captureApplication.Database.ConnectionString,
                fixture.SourceStorageRoot,
                captureApplication.UserId,
                configureServices: ConfigureThrowingAuthorization);
        IRetainedSourceApplication application = failingApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();

        OpenSourceReadContextResult? opened = null;
        Exception? propagated = null;
        try
        {
            opened = await application.OpenReadContextAsync(
                fixture.RepositoryId,
                revision,
                new SourceReadGrantRequest());
        }
        catch (Exception exception)
        {
            propagated = exception;
        }

        Assert.Null(propagated);
        Assert.NotNull(opened);
        Assert.Equal(SourceReadStatus.StorageFailure, opened!.Status);
        Assert.Equal("source_storage_unavailable", opened.Code);
        Assert.Equal(fixture.RepositoryId, opened.RepositoryId);
        Assert.Equal(revision, opened.ProfiledRevision);
        Assert.Null(opened.ManifestSha256);
        Assert.Null(opened.Context);
        Assert.DoesNotContain(
            ThrowingSourceRepositoryAuthorizationVerifier.Marker,
            opened.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenReadContextAsync_WhenSnapshotRepositoryThrows_ReturnsSanitizedStorageFailure()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture captureApplication =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        IRetainedSourceApplication capture = captureApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        SourceCaptureResult captured = await capture.AcquireAsync(
            fixture.RepositoryId,
            await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));
        Assert.Equal(SourceCaptureStatus.Completed, captured.Status);
        string revision = captured.ProfiledRevision ??
                          throw new InvalidOperationException("The source capture did not return a revision.");

        await using RetainedSourceAttachedApplication failingApplication =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                captureApplication.Database.ConnectionString,
                fixture.SourceStorageRoot,
                captureApplication.UserId,
                configureServices: ConfigureThrowingSnapshotRepository);
        IRetainedSourceApplication application = failingApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();

        OpenSourceReadContextResult opened = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            new SourceReadGrantRequest());

        Assert.Equal(SourceReadStatus.StorageFailure, opened.Status);
        Assert.Equal("source_storage_unavailable", opened.Code);
        Assert.Equal(fixture.RepositoryId, opened.RepositoryId);
        Assert.Equal(revision, opened.ProfiledRevision);
        Assert.Null(opened.ManifestSha256);
        Assert.Null(opened.Context);
        Assert.DoesNotContain(
            ThrowingRetainedSourceRepository.Marker,
            opened.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAsync_WhenLiveAuthorizationInfrastructureThrows_ReturnsSanitizedStorageFailure()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture captureApplication =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        IRetainedSourceApplication capture = captureApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        SourceCaptureResult captured = await capture.AcquireAsync(
            fixture.RepositoryId,
            await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));
        Assert.Equal(SourceCaptureStatus.Completed, captured.Status);
        string revision = captured.ProfiledRevision ??
                          throw new InvalidOperationException("The source capture did not return a revision.");

        await using RetainedSourceAttachedApplication failingApplication =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                captureApplication.Database.ConnectionString,
                fixture.SourceStorageRoot,
                captureApplication.UserId,
                configureServices: ConfigureLiveAuthorizationFailure);
        IRetainedSourceApplication application = failingApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();

        OpenSourceReadContextResult opened = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            new SourceReadGrantRequest());
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        Assert.NotNull(opened.Context);
        await using ISourceReadContext context = opened.Context;

        SourceListResult? listed = null;
        Exception? propagated = null;
        try
        {
            listed = await context.ListAsync("src", 10, null);
        }
        catch (Exception exception)
        {
            propagated = exception;
        }

        Assert.Null(propagated);
        Assert.NotNull(listed);
        Assert.Equal(SourceReadStatus.StorageFailure, listed!.Status);
        Assert.Equal("source_storage_unavailable", listed.Code);
        Assert.Equal(fixture.RepositoryId, listed.RepositoryId);
        Assert.Equal(revision, listed.ProfiledRevision);
        Assert.Null(listed.ManifestSha256);
        Assert.Empty(listed.Entries);
        Assert.Empty(listed.Limitations);
        Assert.Null(listed.NextCursor);
        Assert.DoesNotContain(
            ThrowingLiveAuthorizationVerifier.Marker,
            listed.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadBytesAsync_WhenLiveSnapshotRepositoryThrows_ReturnsSanitizedStorageFailure()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture captureApplication =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        IRetainedSourceApplication capture = captureApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        SourceCaptureResult captured = await capture.AcquireAsync(
            fixture.RepositoryId,
            await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));
        Assert.Equal(SourceCaptureStatus.Completed, captured.Status);
        string revision = captured.ProfiledRevision ??
                          throw new InvalidOperationException("The source capture did not return a revision.");

        await using RetainedSourceAttachedApplication failingApplication =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                captureApplication.Database.ConnectionString,
                fixture.SourceStorageRoot,
                captureApplication.UserId,
                configureServices: ConfigureLiveSnapshotRepositoryFailure);
        IRetainedSourceApplication application = failingApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();

        OpenSourceReadContextResult opened = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            new SourceReadGrantRequest());
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        Assert.NotNull(opened.Context);
        await using ISourceReadContext context = opened.Context;

        SourceBytesResult? read = null;
        Exception? propagated = null;
        try
        {
            read = await context.ReadBytesAsync(
                "src/retained-source.txt",
                0,
                1024);
        }
        catch (Exception exception)
        {
            propagated = exception;
        }

        Assert.Null(propagated);
        Assert.NotNull(read);
        Assert.Equal(SourceReadStatus.StorageFailure, read!.Status);
        Assert.Equal("source_storage_unavailable", read.Code);
        Assert.Equal(fixture.RepositoryId, read.RepositoryId);
        Assert.Equal(revision, read.ProfiledRevision);
        Assert.Null(read.ManifestSha256);
        Assert.Null(read.RelativePath);
        Assert.True(read.Content.IsEmpty);
        Assert.Null(read.ByteOffset);
        Assert.Null(read.ByteLength);
        Assert.Null(read.EntryByteLength);
        Assert.Null(read.EntrySha256);
        Assert.Null(read.LimitationCode);
        Assert.DoesNotContain(
            ThrowingLiveSnapshotRepository.Marker,
            read.ToString(),
            StringComparison.Ordinal);
    }

    private static void ConfigureThrowingAuthorization(IServiceCollection services)
    {
        ServiceDescriptor descriptor = services.Last(candidate =>
            candidate.ServiceType == typeof(ISourceRepositoryAuthorizationVerifier));
        services.Remove(descriptor);
        services.AddScoped<ISourceRepositoryAuthorizationVerifier>(_ => new ThrowingSourceRepositoryAuthorizationVerifier());
    }

    private static void ConfigureThrowingSnapshotRepository(IServiceCollection services)
    {
        ServiceDescriptor descriptor = services.Last(candidate =>
            candidate.ServiceType == typeof(IRetainedSourceRepository));
        services.Remove(descriptor);
        services.AddScoped<IRetainedSourceRepository>(serviceProvider =>
            new ThrowingRetainedSourceRepository(ResolveSnapshotRepository(serviceProvider, descriptor)));
    }

    private static void ConfigureLiveAuthorizationFailure(IServiceCollection services)
    {
        ServiceDescriptor descriptor = services.Last(candidate =>
            candidate.ServiceType == typeof(ISourceRepositoryAuthorizationVerifier));
        services.Remove(descriptor);
        services.AddScoped<ISourceRepositoryAuthorizationVerifier>(serviceProvider =>
            new ThrowingLiveAuthorizationVerifier(ResolveAuthorizationVerifier(serviceProvider, descriptor)));
    }

    private static void ConfigureLiveSnapshotRepositoryFailure(IServiceCollection services)
    {
        ServiceDescriptor descriptor = services.Last(candidate =>
            candidate.ServiceType == typeof(IRetainedSourceRepository));
        services.Remove(descriptor);
        services.AddScoped<IRetainedSourceRepository>(serviceProvider =>
            new ThrowingLiveSnapshotRepository(ResolveSnapshotRepository(serviceProvider, descriptor)));
    }

    private static ISourceRepositoryAuthorizationVerifier ResolveAuthorizationVerifier(
        IServiceProvider services,
        ServiceDescriptor descriptor)
    {
        object implementation = descriptor.ImplementationInstance ??
                                descriptor.ImplementationFactory?.Invoke(services) ??
                                (descriptor.ImplementationType is not null
                                    ? ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType)
                                    : throw new InvalidOperationException(
                                        "The source authorization verifier registration has no implementation."));
        return (ISourceRepositoryAuthorizationVerifier)implementation;
    }

    private static IRetainedSourceRepository ResolveSnapshotRepository(
        IServiceProvider services,
        ServiceDescriptor descriptor)
    {
        object implementation = descriptor.ImplementationInstance ??
                                descriptor.ImplementationFactory?.Invoke(services) ??
                                (descriptor.ImplementationType is not null
                                    ? ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType)
                                    : throw new InvalidOperationException(
                                        "The retained-source repository registration has no implementation."));
        return (IRetainedSourceRepository)implementation;
    }

    private sealed class ThrowingSourceRepositoryAuthorizationVerifier
        : ISourceRepositoryAuthorizationVerifier
    {
        internal const string Marker = "synthetic_authorization_failure";

        public Task<AuthorizedSourceRepository?> AuthorizeAsync(
            SourceAuthorizationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Marker);
    }

    private sealed class ThrowingRetainedSourceRepository(
        IRetainedSourceRepository inner) : IRetainedSourceRepository
    {
        internal const string Marker = "synthetic_snapshot_repository_failure";

        public Task<bool> HasForRepositoryAsync(
            Guid repositoryId,
            CancellationToken cancellationToken = default) =>
            inner.HasForRepositoryAsync(repositoryId, cancellationToken);

        public Task<SourceSnapshot?> GetAsync(
            Guid repositoryId,
            string profiledRevision,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Marker);

        public Task PublishAsync(
            SourceSnapshot sourceSnapshot,
            CancellationToken cancellationToken = default) =>
            inner.PublishAsync(sourceSnapshot, cancellationToken);
    }

    private sealed class ThrowingLiveAuthorizationVerifier(
        ISourceRepositoryAuthorizationVerifier inner) : ISourceRepositoryAuthorizationVerifier
    {
        private int _authorizationCount;

        internal const string Marker = "synthetic_live_authorization_failure";

        public Task<AuthorizedSourceRepository?> AuthorizeAsync(
            SourceAuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _authorizationCount) == 3)
            {
                throw new InvalidOperationException(Marker);
            }

            return inner.AuthorizeAsync(request, cancellationToken);
        }
    }

    private sealed class ThrowingLiveSnapshotRepository(
        IRetainedSourceRepository inner) : IRetainedSourceRepository
    {
        private int _getCount;

        internal const string Marker = "synthetic_live_snapshot_repository_failure";

        public Task<bool> HasForRepositoryAsync(
            Guid repositoryId,
            CancellationToken cancellationToken = default) =>
            inner.HasForRepositoryAsync(repositoryId, cancellationToken);

        public Task<SourceSnapshot?> GetAsync(
            Guid repositoryId,
            string profiledRevision,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _getCount) == 2)
            {
                throw new InvalidOperationException(Marker);
            }

            return inner.GetAsync(repositoryId, profiledRevision, cancellationToken);
        }

        public Task PublishAsync(
            SourceSnapshot sourceSnapshot,
            CancellationToken cancellationToken = default) =>
            inner.PublishAsync(sourceSnapshot, cancellationToken);
    }
}