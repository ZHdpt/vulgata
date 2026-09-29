using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;
using Xunit.Sdk;

using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.PreparedSource;

public sealed class PreparedSourceContainmentTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadTextRangeAsync_SiblingTraversalPath_IsDeniedWithoutDisclosingOrChangingSentinel()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string sentinelBefore = await File.ReadAllTextAsync(fixture.SentinelPath);
        DateTime sentinelWriteTimeBefore = File.GetLastWriteTimeUtc(fixture.SentinelPath);
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest(
                "../outside/sentinel.txt",
                ByteOffset: 0,
                ByteCount: 4096));

        AssertDeniedWithoutDisclosure(result, fixture);
        Assert.Equal(sentinelBefore, await File.ReadAllTextAsync(fixture.SentinelPath));
        Assert.Equal(sentinelWriteTimeBefore, File.GetLastWriteTimeUtc(fixture.SentinelPath));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("dot-segment")]
    [InlineData("duplicate-separator")]
    [InlineData("backslash")]
    [InlineData("absolute")]
    [InlineData("drive")]
    [InlineData("unc")]
    [InlineData("alternate-stream")]
    [InlineData("device-name")]
    [InlineData("trailing-dot-alias")]
    [InlineData("trailing-space-alias")]
    [InlineData("extended-device-path")]
    public async Task ReadTextRangeAsync_NonCanonicalOrAliasedPath_IsDeniedWithoutDisclosure(
        string pathKind)
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string sentinelBefore = await File.ReadAllTextAsync(fixture.SentinelPath);
        string requestedPath = GetNonCanonicalPath(fixture, pathKind);
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest(requestedPath, 0, 4096));

        AssertDeniedWithoutDisclosure(result, fixture);
        Assert.Equal(sentinelBefore, await File.ReadAllTextAsync(fixture.SentinelPath));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task IssueAsync_RootWithReparseParent_IsRejected()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string linkedParent = Path.Combine(fixture.SiblingPath, "prepared-parent-link");
        CreateDirectorySymbolicLinkOrSkip(linkedParent, fixture.SandboxPath);
        string linkedRootPath = Path.Combine(linkedParent, "prepared");
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);

        PreparedSourceIssueResult result = await issuer.IssueAsync(
            new PreparedSourceGrantRequest(
                fixture.Approval,
                linkedRootPath,
                PreparedSourceGrantLimits.Default));

        try
        {
            Assert.Equal(PreparedSourceIssueStatus.Denied, result.Status);
            Assert.Null(result.Context);
            string serialized = JsonSerializer.Serialize(result);
            Assert.DoesNotContain(linkedRootPath, serialized, StringComparison.Ordinal);
            Assert.DoesNotContain(fixture.SentinelPath, serialized, StringComparison.Ordinal);
            Assert.Equal("outside the approved prepared root", await File.ReadAllTextAsync(fixture.SentinelPath));
        }
        finally
        {
            if (result.Context is not null)
            {
                await result.Context.DisposeAsync();
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadTextRangeAsync_InRootAndEscapingLinks_AreNeverTraversed()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string internalDirectory = Path.Combine(fixture.PreparedRootPath, "internal");
        Directory.CreateDirectory(internalDirectory);
        string internalFile = Path.Combine(internalDirectory, "payload.txt");
        await File.WriteAllTextAsync(internalFile, "authorized in-root bytes");

        string internalFileLink = Path.Combine(fixture.PreparedRootPath, "internal-file-link.txt");
        string escapingFileLink = Path.Combine(fixture.PreparedRootPath, "escaping-file-link.txt");
        string internalDirectoryLink = Path.Combine(fixture.PreparedRootPath, "internal-directory-link");
        string escapingDirectoryLink = Path.Combine(fixture.PreparedRootPath, "escaping-directory-link");
        CreateFileSymbolicLinkOrSkip(internalFileLink, fixture.UnfamiliarTextPath);
        CreateFileSymbolicLinkOrSkip(escapingFileLink, fixture.SentinelPath);
        CreateDirectorySymbolicLinkOrSkip(internalDirectoryLink, internalDirectory);
        CreateDirectorySymbolicLinkOrSkip(escapingDirectoryLink, fixture.SiblingPath);

        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);
        string sentinelBefore = await File.ReadAllTextAsync(fixture.SentinelPath);
        string[] linkedPaths =
        [
            "internal-file-link.txt",
            "escaping-file-link.txt",
            "internal-directory-link/payload.txt",
            "escaping-directory-link/sentinel.txt",
        ];

        foreach (string relativePath in linkedPaths)
        {
            PreparedSourceResult result = await context.ReadTextRangeAsync(
                new PreparedSourceReadTextRangeRequest(relativePath, 0, 4096));

            AssertDeniedWithoutDisclosure(result, fixture);
            Assert.DoesNotContain("authorized in-root bytes", JsonSerializer.Serialize(result), StringComparison.Ordinal);
        }

        Assert.Equal(sentinelBefore, await File.ReadAllTextAsync(fixture.SentinelPath));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadTextRangeAsync_RootPathReplacedWithSiblingLink_UsesOnlyHeldRoot()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        const string relativeFile = "retargeted.txt";
        const string authorizedText = "held-root-authorized-content";
        const string outsideText = "outside-root-secret-with-distinct-length";
        string sourceFile = Path.Combine(fixture.PreparedRootPath, relativeFile);
        string outsideFile = Path.Combine(fixture.SiblingPath, relativeFile);
        await File.WriteAllTextAsync(sourceFile, authorizedText);
        await File.WriteAllTextAsync(outsideFile, outsideText);
        string sentinelBefore = await File.ReadAllTextAsync(fixture.SentinelPath);
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        string movedRoot = Path.Combine(fixture.SandboxPath, "prepared-held-root");
        try
        {
            Directory.Move(fixture.PreparedRootPath, movedRoot);
        }
        catch (IOException)
        {
            throw SkipException.ForSkip(
                "The host could not replace the owned root while the issued context was open; root-retarget setup is unqualified.");
        }

        CreateDirectorySymbolicLinkOrSkip(fixture.PreparedRootPath, fixture.SiblingPath);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest(relativeFile, 0, 4096));

        Assert.NotEqual(outsideText, result.Text);
        if (result.Text is not null)
        {
            Assert.Equal(authorizedText, result.Text);
        }

        if (result.ObservedFileByteLength is not null)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(authorizedText), result.ObservedFileByteLength);
        }

        if (result.ReadLocation is not null)
        {
            Assert.Equal(fixture.Approval.RepositoryId, result.ReadLocation.RepositoryId);
            Assert.Equal(relativeFile, result.ReadLocation.RelativePath);
        }

        string serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(outsideText, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.SiblingPath, serialized, StringComparison.Ordinal);
        Assert.Equal(sentinelBefore, await File.ReadAllTextAsync(fixture.SentinelPath));
    }

    private static async Task<IPreparedSourceReadContext> IssueContextAsync(
        PreparedSourceFixture fixture)
    {
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        PreparedSourceIssueResult issue = await issuer.IssueAsync(
            new PreparedSourceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedSourceGrantLimits.Default));

        Assert.Equal(PreparedSourceIssueStatus.Issued, issue.Status);
        Assert.NotNull(issue.Context);
        return issue.Context;
    }

    private static void AssertDeniedWithoutDisclosure(
        PreparedSourceResult result,
        PreparedSourceFixture fixture)
    {
        Assert.Equal(PreparedSourceOutcome.Denied, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
        Assert.Null(result.ObservedFileByteLength);
        Assert.Empty(result.Entries);
        Assert.Empty(result.Matches);
        Assert.Empty(result.Limitations);
        Assert.Null(result.Continuation);

        string serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("outside the approved prepared root", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.SiblingPath, serialized, StringComparison.Ordinal);
    }

    private static void RequireWindowsHost()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw SkipException.ForSkip(
                "Windows handle-relative containment is not qualified on this platform.");
        }
    }

    private static string GetNonCanonicalPath(
        PreparedSourceFixture fixture,
        string pathKind) =>
        pathKind switch
        {
            "dot-segment" => "./notes.oddlangx",
            "duplicate-separator" => "internal//payload.txt",
            "backslash" => "internal\\payload.txt",
            "absolute" => fixture.SentinelPath,
            "drive" => Path.GetPathRoot(fixture.SentinelPath) + "outside\\sentinel.txt",
            "unc" => @"\\127.0.0.1\share\sentinel.txt",
            "alternate-stream" => "notes.oddlangx:stream",
            "device-name" => "CON.txt",
            "trailing-dot-alias" => "notes.oddlangx.",
            "trailing-space-alias" => "notes.oddlangx ",
            "extended-device-path" => @"\\?\C:\outside\sentinel.txt",
            _ => throw new ArgumentOutOfRangeException(nameof(pathKind)),
        };

    private static void CreateDirectorySymbolicLinkOrSkip(string linkPath, string targetPath)
    {
        if (Directory.Exists(linkPath) || File.Exists(linkPath))
        {
            throw new InvalidOperationException("The isolated symbolic-link fixture path already exists.");
        }

        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (UnauthorizedAccessException)
        {
            throw SkipException.ForSkip(
                "Directory symbolic-link setup requires privilege that is unavailable on this Windows host.");
        }
        catch (PlatformNotSupportedException)
        {
            throw SkipException.ForSkip(
                "Directory symbolic links are unsupported on this Windows host.");
        }
        catch (IOException exception) when (IsSymlinkPrivilegeFailure(exception))
        {
            throw SkipException.ForSkip(
                "Directory symbolic-link privilege is unavailable on this Windows host.");
        }
    }

    private static void CreateFileSymbolicLinkOrSkip(string linkPath, string targetPath)
    {
        if (Directory.Exists(linkPath) || File.Exists(linkPath))
        {
            throw new InvalidOperationException("The isolated symbolic-link fixture path already exists.");
        }

        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (UnauthorizedAccessException)
        {
            throw SkipException.ForSkip(
                "File symbolic-link setup requires privilege that is unavailable on this Windows host.");
        }
        catch (PlatformNotSupportedException)
        {
            throw SkipException.ForSkip(
                "File symbolic links are unsupported on this Windows host.");
        }
        catch (IOException exception) when (IsSymlinkPrivilegeFailure(exception))
        {
            throw SkipException.ForSkip(
                "File symbolic-link privilege is unavailable on this Windows host.");
        }
    }

    private static bool IsSymlinkPrivilegeFailure(IOException exception) =>
        exception.HResult is unchecked((int)0x80070522) or unchecked((int)0x80070005);
}
