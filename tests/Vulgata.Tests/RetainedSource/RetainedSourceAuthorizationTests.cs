using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.RetainedSource;

public sealed class RetainedSourceAuthorizationTests
{
    [Fact]
    public async Task AcquireAndOpen_UnauthenticatedSubjectIsDeniedWithoutSourceDisclosure()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                authenticated: false,
                administrator: false,
                systemOwner: false);
        IRetainedSourceApplication application =
            appFixture.Scope.ServiceProvider.GetRequiredService<IRetainedSourceApplication>();

        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));
        OpenSourceReadContextResult opened = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            new string('0', 40),
            new SourceReadGrantRequest());

        Assert.Equal(SourceCaptureStatus.Denied, capture.Status);
        Assert.Equal("source_denied", capture.Code);
        Assert.Null(capture.ProfiledRevision);
        Assert.Null(capture.ManifestSha256);
        Assert.Null(capture.EntryCount);
        Assert.Null(capture.TotalBlobBytes);
        Assert.Equal(SourceReadStatus.Denied, opened.Status);
        Assert.Equal("source_denied", opened.Code);
        Assert.Null(opened.ManifestSha256);
        Assert.Null(opened.Context);
    }

    [Fact]
    public async Task AcquireAndOpen_AnonymousKnownIdentifierCannotBorrowAdministratorAuthority()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        IRetainedSourceApplication authorizedApplication =
            appFixture.Scope.ServiceProvider.GetRequiredService<IRetainedSourceApplication>();
        SourceCaptureResult authorizedCapture = await authorizedApplication.AcquireAsync(
            fixture.RepositoryId,
            permittedRef);
        Assert.Equal(SourceCaptureStatus.Completed, authorizedCapture.Status);

        IHttpContextAccessor accessor =
            appFixture.Application.Services.GetRequiredService<IHttpContextAccessor>();
        var anonymousIdentity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, appFixture.UserId)]);
        Assert.False(anonymousIdentity.IsAuthenticated);
        accessor.HttpContext = new DefaultHttpContext
        {
            RequestServices = appFixture.Application.Services,
            User = new ClaimsPrincipal(anonymousIdentity),
        };
        using IServiceScope freshScope = appFixture.Application.Services.CreateScope();
        IRetainedSourceApplication anonymousApplication =
            freshScope.ServiceProvider.GetRequiredService<IRetainedSourceApplication>();

        SourceCaptureResult capture = await anonymousApplication.AcquireAsync(
            fixture.RepositoryId,
            permittedRef);
        OpenSourceReadContextResult opened = await anonymousApplication.OpenReadContextAsync(
            fixture.RepositoryId,
            authorizedCapture.ProfiledRevision!,
            new SourceReadGrantRequest());

        Assert.True(
            capture.Status == SourceCaptureStatus.Denied && opened.Status == SourceReadStatus.Denied,
            $"Anonymous source access was not denied: capture={capture.Status}/{capture.Code}, " +
            $"open={opened.Status}/{opened.Code}");
        Assert.Equal("source_denied", capture.Code);
        Assert.Null(capture.ProfiledRevision);
        Assert.Null(capture.ManifestSha256);
        Assert.Equal(SourceReadStatus.Denied, opened.Status);
        Assert.Equal("source_denied", opened.Code);
        Assert.Null(opened.ManifestSha256);
        Assert.Null(opened.Context);
    }

    [Fact]
    public async Task AcquireAndOpen_AuthenticatedIdentityWithoutIdentifierCannotBorrowAuxiliaryAnonymousId()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        IRetainedSourceApplication authorizedApplication =
            appFixture.Scope.ServiceProvider.GetRequiredService<IRetainedSourceApplication>();
        SourceCaptureResult authorizedCapture = await authorizedApplication.AcquireAsync(
            fixture.RepositoryId,
            permittedRef);
        Assert.Equal(SourceCaptureStatus.Completed, authorizedCapture.Status);

        ClaimsIdentity authenticatedWithoutIdentifier = new([], "Test");
        ClaimsIdentity anonymousKnownIdentifier = new(
            [new Claim(ClaimTypes.NameIdentifier, appFixture.UserId)]);
        IHttpContextAccessor accessor =
            appFixture.Application.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            RequestServices = appFixture.Application.Services,
            User = new ClaimsPrincipal(
                [authenticatedWithoutIdentifier, anonymousKnownIdentifier]),
        };
        using IServiceScope freshScope = appFixture.Application.Services.CreateScope();
        IRetainedSourceApplication anonymousApplication =
            freshScope.ServiceProvider.GetRequiredService<IRetainedSourceApplication>();

        SourceCaptureResult capture = await anonymousApplication.AcquireAsync(
            fixture.RepositoryId,
            permittedRef);
        OpenSourceReadContextResult opened = await anonymousApplication.OpenReadContextAsync(
            fixture.RepositoryId,
            authorizedCapture.ProfiledRevision!,
            new SourceReadGrantRequest());

        Assert.Equal(SourceCaptureStatus.Denied, capture.Status);
        Assert.Equal("source_denied", capture.Code);
        Assert.Null(capture.ProfiledRevision);
        Assert.Equal(SourceReadStatus.Denied, opened.Status);
        Assert.Equal("source_denied", opened.Code);
        Assert.Null(opened.ManifestSha256);
        Assert.Null(opened.Context);
    }

    [Fact]
    public async Task AcquireAndOpen_AuthenticatedIdentifierWinsOverEarlierAnonymousIdentity()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        IRetainedSourceApplication authorizedApplication =
            appFixture.Scope.ServiceProvider.GetRequiredService<IRetainedSourceApplication>();
        SourceCaptureResult authorizedCapture = await authorizedApplication.AcquireAsync(
            fixture.RepositoryId,
            permittedRef);
        Assert.Equal(SourceCaptureStatus.Completed, authorizedCapture.Status);

        ClaimsIdentity anonymousKnownIdentifier = new(
            [new Claim(ClaimTypes.NameIdentifier, "anonymous-auxiliary-id")]);
        ClaimsIdentity authenticatedIdentifier = new(
            [new Claim(ClaimTypes.NameIdentifier, appFixture.UserId)],
            "Test");
        IHttpContextAccessor accessor =
            appFixture.Application.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            RequestServices = appFixture.Application.Services,
            User = new ClaimsPrincipal(
                [anonymousKnownIdentifier, authenticatedIdentifier]),
        };
        using IServiceScope freshScope = appFixture.Application.Services.CreateScope();
        IRetainedSourceApplication application =
            freshScope.ServiceProvider.GetRequiredService<IRetainedSourceApplication>();

        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef);
        OpenSourceReadContextResult opened = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            authorizedCapture.ProfiledRevision!,
            new SourceReadGrantRequest());

        Assert.Equal(SourceCaptureStatus.Replay, capture.Status);
        Assert.Equal("source_replay", capture.Code);
        Assert.Equal(authorizedCapture.ProfiledRevision, capture.ProfiledRevision);
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        Assert.Equal("source_context_opened", opened.Code);
        await opened.Context!.DisposeAsync();
    }

    [Fact]
    public async Task ReadContext_AfterSystemOwnerAndRoleRevocation_DeniesWithoutPayload()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                administrator: false,
                systemOwner: true);
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest(maximumOperations: 10, maximumReturnedBytes: 64));
        await using ISourceReadContext context = opened.Context!;

        await RetainedSourceCaptureApplicationFixture.RevokeAuthorityAsync(
            appFixture.Database.ConnectionString,
            appFixture.SystemId,
            appFixture.UserId);

        SourceEntryResult entry = await context.GetEntryAsync("src/retained-source.txt");
        SourceBytesResult bytes = await context.ReadBytesAsync("src/retained-source.txt", 0, 8);

        Assert.Equal(SourceReadStatus.Denied, entry.Status);
        Assert.Equal("source_denied", entry.Code);
        Assert.Null(entry.ManifestSha256);
        Assert.Null(entry.Entry);
        Assert.Equal(SourceReadStatus.Denied, bytes.Status);
        Assert.Equal("source_denied", bytes.Code);
        Assert.Null(bytes.ManifestSha256);
        Assert.Empty(bytes.Content.ToArray());
        Assert.Null(bytes.RelativePath);
        Assert.Null(bytes.EntrySha256);
    }

    [Fact]
    public async Task ReadContext_AfterOriginRebind_DeniesWithoutPayload()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest(maximumOperations: 10, maximumReturnedBytes: 64));
        await using ISourceReadContext context = opened.Context!;

        await RetainedSourceCaptureApplicationFixture.SetOriginAsync(
            appFixture.Database.ConnectionString,
            fixture.RepositoryId,
            "https://example.invalid/rebound.git");

        SourceEntryResult entry = await context.GetEntryAsync("src/retained-source.txt");
        SourceTextResult text = await context.ReadTextAsync("src/retained-source.txt", 0, 8);

        Assert.Equal(SourceReadStatus.Denied, entry.Status);
        Assert.Equal("source_denied", entry.Code);
        Assert.Null(entry.Entry);
        Assert.Equal(SourceReadStatus.Denied, text.Status);
        Assert.Equal("source_denied", text.Code);
        Assert.Null(text.Content);
        Assert.Null(text.RelativePath);
        Assert.Null(text.EntrySha256);
    }

    [Fact]
    public async Task ReadContext_AfterSystemRebind_DeniesSystemOwnerWithoutPayload()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                administrator: false,
                systemOwner: true);
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest(maximumOperations: 10, maximumMetadataEntries: 10));
        await using ISourceReadContext context = opened.Context!;

        await RetainedSourceCaptureApplicationFixture.RebindSystemAsync(
            appFixture.Database.ConnectionString,
            fixture.RepositoryId,
            Guid.NewGuid());

        SourceListResult list = await context.ListAsync(string.Empty, 1, null);

        Assert.Equal(SourceReadStatus.Denied, list.Status);
        Assert.Equal("source_denied", list.Code);
        Assert.Empty(list.Entries);
        Assert.Empty(list.Limitations);
        Assert.Null(list.NextCursor);
    }

    [Fact]
    public async Task OpenReadContext_RepositoryAndRevisionScopeMismatchDoesNotDiscloseSource()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);
        IRetainedSourceApplication application =
            appFixture.Scope.ServiceProvider.GetRequiredService<IRetainedSourceApplication>();

        OpenSourceReadContextResult wrongRepository = await application.OpenReadContextAsync(
            Guid.NewGuid(),
            revision,
            new SourceReadGrantRequest());
        OpenSourceReadContextResult wrongRevision = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            new string('0', revision.Length),
            new SourceReadGrantRequest());

        Assert.Equal(SourceReadStatus.Denied, wrongRepository.Status);
        Assert.Equal("source_denied", wrongRepository.Code);
        Assert.Null(wrongRepository.ManifestSha256);
        Assert.Null(wrongRepository.Context);
        Assert.Equal(SourceReadStatus.NotFoundOrUnavailable, wrongRevision.Status);
        Assert.Equal("source_not_found_or_unavailable", wrongRevision.Code);
        Assert.Null(wrongRevision.ManifestSha256);
        Assert.Null(wrongRevision.Context);
    }

    [Fact]
    public async Task ReadContext_AfterGrantExpiry_DeniesMetadataWithoutDisclosure()
    {
        var time = new ControlledRetainedSourceTimeProvider();
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                timeProvider: time);
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest(
                maximumOperations: 4,
                maximumMetadataEntries: 4,
                deadline: TimeSpan.FromSeconds(1)));
        await using ISourceReadContext context = opened.Context!;
        time.Advance(TimeSpan.FromSeconds(2));

        SourceEntryResult entry = await context.GetEntryAsync("src/retained-source.txt");

        Assert.Equal(SourceReadStatus.DeadlineExceeded, entry.Status);
        Assert.Equal("source_deadline_exceeded", entry.Code);
        Assert.Null(entry.ManifestSha256);
        Assert.Null(entry.Entry);
    }

    [Theory]
    [InlineData("/absolute")]
    [InlineData("src\\file.txt")]
    [InlineData("src/./file.txt")]
    [InlineData("src/\u0000file.txt")]
    [InlineData("src/\u0001file.txt")]
    [InlineData("../outside")]
    [InlineData("C:file.txt")]
    public async Task ReadContext_InvalidPathIsDeniedWithoutSourceDisclosure(string path)
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest(maximumOperations: 10, maximumReturnedBytes: 64));
        await using ISourceReadContext context = opened.Context!;

        SourceEntryResult entry = await context.GetEntryAsync(path);
        SourceBytesResult bytes = await context.ReadBytesAsync(path, 0, 8);
        SourceTextResult text = await context.ReadTextAsync(path, 0, 8);

        Assert.Equal(SourceReadStatus.Denied, entry.Status);
        Assert.Equal("source_denied", entry.Code);
        Assert.Null(entry.ManifestSha256);
        Assert.Null(entry.Entry);
        Assert.Equal(SourceReadStatus.Denied, bytes.Status);
        Assert.Equal("source_denied", bytes.Code);
        Assert.Empty(bytes.Content.ToArray());
        Assert.Null(bytes.ManifestSha256);
        Assert.Null(bytes.RelativePath);
        Assert.Equal(SourceReadStatus.Denied, text.Status);
        Assert.Equal("source_denied", text.Code);
        Assert.Null(text.Content);
        Assert.Null(text.ManifestSha256);
        Assert.Null(text.RelativePath);
    }

    private static async Task<string> CaptureAsync(
        RetainedSourceFixture fixture,
        RetainedSourceCaptureApplicationFixture appFixture)
    {
        IRetainedSourceApplication application =
            appFixture.Scope.ServiceProvider.GetRequiredService<IRetainedSourceApplication>();
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        return capture.ProfiledRevision!;
    }

    private static async Task<OpenSourceReadContextResult> OpenAsync(
        RetainedSourceFixture fixture,
        RetainedSourceCaptureApplicationFixture appFixture,
        string revision,
        SourceReadGrantRequest grant)
    {
        IRetainedSourceApplication application =
            appFixture.Scope.ServiceProvider.GetRequiredService<IRetainedSourceApplication>();
        OpenSourceReadContextResult opened = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            grant);
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        Assert.NotNull(opened.Context);
        return opened;
    }
}
