using Vulgata.Tests.TestInfrastructure;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Security.Cryptography;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Source;
using Vulgata.Tests.PreparedSource;

namespace Vulgata.Tests.AnalysisRuntime;

/// <summary>
/// T040 post-GREEN qualification against the retained JDT-LS distribution.
/// The test deliberately uses the supplied archive bytes and filename; it
/// must not copy, rename, or extract the operator-supplied artifact.
/// </summary>
public sealed class JdtLsRealQualificationTests
{
    private const string JavaHome = @"C:\Program Files\Java\latest\jdk-21";
    private const string JdtLsRoot =
        @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332";
    private const string ArchivePath =
        @"C:\Users\13728\Downloads\jdt-language-server-1.61.0-202608141332.tar.gz";
    private const string ArchiveName = "jdt-language-server-1.61.0-202608141332.tar.gz";
    private const string ArchiveSha256 =
        "bbb2297d69a69a8216e17b799744b9ec5454974bbb5e498d45ad3505d8b4fa0f";
    private const string ReleaseIdentity = "jdt-language-server-1.61.0-202608141332";
    private const string ProfiledRevision =
        "0123456789abcdef0123456789abcdef01234567";
    private const string RuntimeIdentityHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string CodeGraphHash =
        "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03";
    private const string NodeHash =
        "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3";
    private const string NpmShimHash =
        "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877";

    private static readonly Guid RepositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000012");

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void RuntimeIdentity_AcceptsRetainedTarGzArchiveAsConfiguredIdentity()
    {
        AnalysisRuntimeIdentity identity = AnalysisRuntimeIdentity.Create(
            new AnalysisRuntimeOptions(
                CodeGraphCommandPath: @"G:\packages\npm\prefix\codegraph.ps1",
                JdtLsInstallationRoot: JdtLsRoot,
                JavaHome: JavaHome),
            RepositoryId,
            ProfiledRevision,
            "@colbymchenry/codegraph",
            "1.5.0",
            "https://github.com/colbymchenry/codegraph",
            "codegraph",
            "@colbymchenry/codegraph-win32-x64@1.5.0",
            [
                new("lib/dist/bin/codegraph.js", CodeGraphHash),
                new("node.exe", NodeHash),
                new("npm-shim.js", NpmShimHash),
            ],
            ReleaseIdentity,
            ArchiveName,
            ArchiveSha256,
            21,
            "StreamJsonRpc",
            "2.25.29",
            "stdio");

        Assert.Equal(ArchiveName, identity.JdtLsDistributionArchiveName);
        Assert.Equal(ArchiveSha256, identity.JdtLsDistributionArchiveSha256);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OwnedIncomingPath_UsingNativeShortDirectoryAlias_IsDeniedBeforeProtocol()
    {
        Assert.True(OperatingSystem.IsWindows(),
            "Native alias qualification is blocked because GetShortPathNameW requires Windows.");

        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        string ownedJavaPath = Path.Combine(codeGraphPath, "Owned.java");
        await File.WriteAllTextAsync(ownedJavaPath, "class Owned { void target() {} }\n");

        PreparedJavaNavigationOwnershipFixture ownershipFixture =
            PreparedJavaNavigationOwnershipFixture.CreateVerifiedOwned(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedCodeGraphWorkspaceOwnership binding = ownershipFixture.ApplicationHeldBinding;
        PreparedCodeGraphOwnershipViewResult ownershipView =
            await ownershipFixture.GetCurrentOwnershipViewAsync(fixture.Approval);
        Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, ownershipView.Status);
        Assert.Equal(binding, ownershipView.Record);
        Assert.NotNull(binding.StateEntryObjectIdentity);

        string shortDirectoryPath = GetNativeShortPath(codeGraphPath);
        string aliasLeaf = Path.GetFileName(shortDirectoryPath);
        Assert.False(aliasLeaf.Equals(".codegraph", StringComparison.OrdinalIgnoreCase),
            "Native alias qualification is blocked: GetShortPathNameW returned the original .codegraph leaf; no alias behavior was tested.");

        string aliasDirectoryPath = Path.GetFullPath(Path.Combine(fixture.PreparedRootPath, aliasLeaf));
        Assert.True(string.Equals(
                Path.GetFullPath(fixture.PreparedRootPath),
                Path.GetDirectoryName(aliasDirectoryPath),
                StringComparison.OrdinalIgnoreCase),
            "The test must use only the returned alias leaf under the original granted root.");
        Assert.True(File.Exists(Path.Combine(aliasDirectoryPath, "Owned.java")),
            "The native alias leaf must resolve the existing owned Java file.");

        (PreparedSourceTraversal.OpenStatus rootStatus, SafeFileHandle? rootHandle) =
            PreparedSourceTraversal.OpenRoot(fixture.PreparedRootPath);
        Assert.Equal(PreparedSourceTraversal.OpenStatus.Opened, rootStatus);
        using SafeFileHandle heldRoot = rootHandle ??
            throw new InvalidOperationException("The prepared root handle was unavailable for alias qualification.");
        (PreparedSourceTraversal.OpenStatus aliasStatus, SafeFileHandle? aliasHandle) =
            PreparedSourceTraversal.OpenRelativeDirectory(heldRoot, aliasLeaf);
        Assert.Equal(PreparedSourceTraversal.OpenStatus.Opened, aliasStatus);
        using SafeFileHandle heldAlias = aliasHandle ??
            throw new InvalidOperationException("The native alias could not be opened relative to the granted root.");
        PreparedSourceTraversal.DirectoryIdentity observedAliasIdentity =
            PreparedSourceTraversal.ObserveDirectory(heldAlias) ??
            throw new InvalidOperationException("The native alias did not resolve to an ordinary physical directory.");
        Assert.Equal(
            binding.StateEntryObjectIdentity,
            new PreparedToolDirectoryObjectIdentity(
                observedAliasIdentity.VolumeSerialNumber,
                observedAliasIdentity.FileIndexHigh,
                observedAliasIdentity.FileIndexLow));

        var toolAuthorization = new CurrentNavigationToolAuthorizer(fixture.Approval);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-alias-state");
        Directory.CreateDirectory(disposableStateRoot);
        var runtime = new AliasQualificationRuntime();
        var factory = ownershipFixture.CreateFactory(
            new PreparedJavaNavigationProfile(
                disposableStateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime);
        PreparedJavaNavigationOpenResult? opened = null;

        try
        {
            opened = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);
            string requestPath = $"{aliasLeaf}/Owned.java";
            Assert.NotEqual(".codegraph/Owned.java", requestPath);
            Assert.True(PreparedSourceTraversal.IsCanonicalRelativePath(requestPath, allowEmpty: false));

            PreparedJavaNavigationResult result = await context.GetDefinitionAsync(
                new PreparedJavaNavigationRequest(requestPath, 1, 1, 10));

            Assert.Equal(PreparedJavaNavigationOutcome.Denied, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(0, runtime.Session.NavigateCallCount);
            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, await lease.CheckCurrentAsync());
            Assert.Equal(
                PreparedToolUseAuthorityStatus.Current,
                (await toolAuthorization.VerifyCurrentAsync(
                    fixture.Approval,
                    PreparedToolPurpose.JdtNavigation)).Status);
        }
        finally
        {
            if (opened?.Context is not null)
            {
                await opened.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ReturnedOwnedLocation_UsingNativeShortDirectoryAlias_IsFiltered()
    {
        Assert.True(OperatingSystem.IsWindows(),
            "Native alias qualification is blocked because GetShortPathNameW requires Windows.");

        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        string ownedJavaPath = Path.Combine(codeGraphPath, "Owned.java");
        string ordinaryJavaPath = Path.Combine(fixture.PreparedRootPath, "Source.java");
        string callerJavaPath = Path.Combine(fixture.PreparedRootPath, "Caller.java");
        await File.WriteAllTextAsync(ownedJavaPath, "class Owned { void target() {} }\n");
        await File.WriteAllTextAsync(ordinaryJavaPath, "class Source { void target() {} }\n");
        await File.WriteAllTextAsync(callerJavaPath, "class Caller { void call() {} }\n");

        PreparedJavaNavigationOwnershipFixture ownershipFixture =
            PreparedJavaNavigationOwnershipFixture.CreateVerifiedOwned(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedCodeGraphWorkspaceOwnership binding = ownershipFixture.ApplicationHeldBinding;
        PreparedCodeGraphOwnershipViewResult ownershipView =
            await ownershipFixture.GetCurrentOwnershipViewAsync(fixture.Approval);
        Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, ownershipView.Status);
        Assert.Equal(binding, ownershipView.Record);
        Assert.NotNull(binding.StateEntryObjectIdentity);

        string shortDirectoryPath = GetNativeShortPath(codeGraphPath);
        string aliasLeaf = Path.GetFileName(shortDirectoryPath);
        Assert.False(aliasLeaf.Equals(".codegraph", StringComparison.OrdinalIgnoreCase),
            "Native alias qualification is blocked: GetShortPathNameW returned the original .codegraph leaf; no alias behavior was tested.");

        string aliasDirectoryPath = Path.Combine(fixture.PreparedRootPath, aliasLeaf);
        Assert.True(Path.IsPathFullyQualified(fixture.PreparedRootPath),
            "The original prepared root must already be fully qualified.");
        Assert.True(string.Equals(
                fixture.PreparedRootPath,
                Path.GetDirectoryName(aliasDirectoryPath),
                StringComparison.OrdinalIgnoreCase),
            "The test must use only the returned alias leaf under the original granted root.");
        Assert.True(File.Exists(Path.Combine(aliasDirectoryPath, "Owned.java")),
            "The native alias leaf must resolve the existing owned Java file.");

        (PreparedSourceTraversal.OpenStatus rootStatus, SafeFileHandle? rootHandle) =
            PreparedSourceTraversal.OpenRoot(fixture.PreparedRootPath);
        Assert.Equal(PreparedSourceTraversal.OpenStatus.Opened, rootStatus);
        using SafeFileHandle heldRoot = rootHandle ??
            throw new InvalidOperationException("The prepared root handle was unavailable for alias qualification.");
        (PreparedSourceTraversal.OpenStatus aliasStatus, SafeFileHandle? aliasHandle) =
            PreparedSourceTraversal.OpenRelativeDirectory(heldRoot, aliasLeaf);
        Assert.Equal(PreparedSourceTraversal.OpenStatus.Opened, aliasStatus);
        using SafeFileHandle heldAlias = aliasHandle ??
            throw new InvalidOperationException("The native alias could not be opened relative to the granted root.");
        PreparedSourceTraversal.DirectoryIdentity observedAliasIdentity =
            PreparedSourceTraversal.ObserveDirectory(heldAlias) ??
            throw new InvalidOperationException("The native alias did not resolve to an ordinary physical directory.");
        Assert.Equal(
            binding.StateEntryObjectIdentity,
            new PreparedToolDirectoryObjectIdentity(
                observedAliasIdentity.VolumeSerialNumber,
                observedAliasIdentity.FileIndexHigh,
                observedAliasIdentity.FileIndexLow));

        var toolAuthorization = new CurrentNavigationToolAuthorizer(fixture.Approval);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-alias-result-state");
        Directory.CreateDirectory(disposableStateRoot);
        var runtime = new AliasQualificationRuntime();
        string rootPathWithSeparator = Path.EndsInDirectorySeparator(fixture.PreparedRootPath)
            ? fixture.PreparedRootPath
            : fixture.PreparedRootPath + Path.DirectorySeparatorChar;
        var originalRootUri = new Uri(rootPathWithSeparator, UriKind.Absolute);
        string rawAliasOwnedFileUri = new Uri(
            originalRootUri,
            $"{aliasLeaf}/Owned.java").AbsoluteUri;
        string rawAliasSegment = $"/{aliasLeaf}/Owned.java";
        Assert.True(rawAliasOwnedFileUri.Contains(rawAliasSegment, StringComparison.OrdinalIgnoreCase),
            "The controlled raw protocol location must preserve the qualified native alias leaf.");
        Assert.True(originalRootUri.IsBaseOf(new Uri(rawAliasOwnedFileUri, UriKind.Absolute)),
            "The raw alias URI must remain inside the original granted root.");
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            [
                new PreparedJdtLsRawLocation(
                    rawAliasOwnedFileUri,
                    1,
                    1,
                    1,
                    7),
                new PreparedJdtLsRawLocation(new Uri(ordinaryJavaPath).AbsoluteUri, 1, 1, 1, 7),
            ],
            LimitReached: false,
            CapabilityLimitation: null);
        var factory = ownershipFixture.CreateFactory(
            new PreparedJavaNavigationProfile(
                disposableStateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime);
        PreparedJavaNavigationOpenResult? opened = null;

        try
        {
            opened = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);
            var request = new PreparedJavaNavigationRequest("Caller.java", 1, 1, 10);

            PreparedJavaNavigationResult result = await context.GetDefinitionAsync(request);

            Assert.Equal(1, runtime.Session.NavigateCallCount);
            Assert.Equal(request, runtime.Session.LastRequest);
            Assert.Equal(PreparedJavaNavigationOutcome.Observed, result.Outcome);
            Assert.Equal(
                ["Source.java"],
                result.Locations.Select(location => location.Source.RelativePath));
            PreparedJavaLocation ordinaryLocation = Assert.Single(result.Locations);
            Assert.Equal("Source.java", ordinaryLocation.Source.RelativePath);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, await lease.CheckCurrentAsync());
            Assert.Equal(
                PreparedToolUseAuthorityStatus.Current,
                (await toolAuthorization.VerifyCurrentAsync(
                    fixture.Approval,
                    PreparedToolPurpose.JdtNavigation)).Status);
        }
        finally
        {
            if (opened?.Context is not null)
            {
                await opened.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task RetainedArchive_RealJava21Session_QualifiesNavigationAndBoundedCleanup()
    {
        Assert.True(File.Exists(ArchivePath));
        Assert.Equal(ArchiveName, Path.GetFileName(ArchivePath));
        Assert.Equal(ArchiveSha256, await HashAsync(ArchivePath));
        Assert.True(Directory.Exists(JdtLsRoot));
        Assert.Equal(ReleaseIdentity, new DirectoryInfo(JdtLsRoot).Name);
        Assert.True(File.Exists(Path.Combine(JdtLsRoot, "config_win", "config.ini")));
        Assert.Single(Directory.GetFiles(
            Path.Combine(JdtLsRoot, "plugins"),
            "org.eclipse.equinox.launcher_*.jar",
            SearchOption.TopDirectoryOnly));

        string javaExecutable = Path.Combine(
            JavaHome,
            "bin",
            OperatingSystem.IsWindows() ? "java.exe" : "java");
        Assert.True(File.Exists(javaExecutable));
        Assert.Contains("version \"21.", await ReadJavaVersionAsync(javaExecutable));

        string workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story121-jdt-real-" + Guid.NewGuid().ToString("N"));
        string workspacePath = Path.Combine(
            workspaceRoot,
            RepositoryId.ToString("N"),
            ProfiledRevision);
        Directory.CreateDirectory(workspacePath);
        CopyDirectory(FindFixtureRepository(), workspacePath);

        try
        {
            JdtLsAdapterOptions options = new(
                javaExecutable,
                JdtLsRoot,
                workspaceRoot,
                InitializationTimeout: TimeSpan.FromSeconds(30),
                RequestTimeout: TimeSpan.FromSeconds(30),
                ShutdownTimeout: TimeSpan.FromSeconds(10),
                ResponseByteLimit: 256 * 1024);
            JdtLsAnalysisSessionFactory factory = new(
                options,
                ArchivePath,
                ArchiveSha256);
            AnalysisScope scope = new(
                RepositoryId,
                Guid.NewGuid(),
                ProfiledRevision,
                "t040-" + Guid.NewGuid().ToString("N"),
                RuntimeIdentityHash,
                new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()));
            AnalysisOperationBounds bounds = new(itemLimit: 16, byteLimit: 64 * 1024);
            string relativePath =
                "src/main/java/example/analysis/GreetingService.java";
            JavaNavigationRequest definitionNavigation = new(
                relativePath,
                new AnalysisSourcePosition(12, 24),
                bounds);
            JavaNavigationRequest formatterInterfaceNavigation = new(
                "src/main/java/example/analysis/GreetingFormatter.java",
                new AnalysisSourcePosition(3, 21),
                bounds);

            await using (IJavaAnalysisSession session =
                await factory.OpenAsync(scope, CancellationToken.None))
            {
                JavaNavigationResult symbols = await session.GetDocumentSymbolsAsync(
                    scope,
                    new JavaNavigationRequest(relativePath, null, bounds),
                    CancellationToken.None);
                JavaNavigationResult definition = await session.GetDefinitionAsync(
                    scope, definitionNavigation, CancellationToken.None);
                JavaNavigationResult references = await session.GetReferencesAsync(
                    scope, formatterInterfaceNavigation, CancellationToken.None);
                JavaNavigationResult implementations = await session.GetImplementationsAsync(
                    scope, formatterInterfaceNavigation, CancellationToken.None);

                AssertNavigationSucceeded(symbols);
                AssertNavigationSucceeded(definition);
                AssertNavigationSucceeded(references);
                AssertNavigationSucceeded(implementations);
                Assert.All(
                    symbols.Evidence
                        .Concat(definition.Evidence)
                        .Concat(references.Evidence)
                        .Concat(implementations.Evidence),
                    evidence =>
                    {
                        Assert.DoesNotContain("..", evidence.RelativePath);
                        Assert.False(Path.IsPathRooted(evidence.RelativePath));
                        Assert.InRange(evidence.Range.StartLine, 1, 100);
                        Assert.InRange(
                            evidence.Range.EndLine,
                            evidence.Range.StartLine,
                            100);
                    });

                using CancellationTokenSource cancelled = new();
                cancelled.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    session.GetDefinitionAsync(scope, definitionNavigation, cancelled.Token));

                AnalysisScope otherRevision = new(
                    scope.RepositoryId,
                    scope.ScanRunId,
                    "fedcba9876543210fedcba9876543210fedcba98",
                    scope.WorkingCopyBindingToken,
                    scope.RuntimeIdentityHash,
                    new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()));
                JavaNavigationResult conflict = await session.GetDefinitionAsync(
                    otherRevision, definitionNavigation, CancellationToken.None);
                Assert.Equal(AnalysisOperationStatus.Failed, conflict.Status);
                Assert.Equal(
                    AnalysisPortFailureReason.WorkspaceConflict,
                    conflict.Failure?.Reason);
                Assert.Equal("jdt_scope_mismatch", conflict.Failure?.FailureCode);
            }
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }

            Assert.False(Directory.Exists(workspaceRoot));
        }
    }

    private static string GetNativeShortPath(string longPath)
    {
        uint required = GetShortPathNameW(longPath, null, 0);
        if (required == 0)
        {
            throw new InvalidOperationException(
                "Native alias qualification is blocked: GetShortPathNameW failed: " +
                new Win32Exception(Marshal.GetLastWin32Error()).Message);
        }

        if (required > 32768)
        {
            throw new InvalidOperationException(
                "Native alias qualification is blocked: the returned path exceeds the documented Windows path buffer bound.");
        }

        var output = new StringBuilder(checked((int)required));
        uint written = GetShortPathNameW(longPath, output, required);
        if (written == 0 || written >= required)
        {
            throw new InvalidOperationException(
                "Native alias qualification is blocked: GetShortPathNameW could not return the complete path: " +
                new Win32Exception(Marshal.GetLastWin32Error()).Message);
        }

        return output.ToString();
    }

    [DllImport("kernel32.dll", EntryPoint = "GetShortPathNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathNameW(
        string longPath,
        [Out] StringBuilder? shortPath,
        uint bufferLength);

    private sealed class CurrentNavigationToolAuthorizer(PreparedSourceApproval approval)
        : IPreparedToolUseAuthorizer
    {
        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval requestedApproval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreparedToolUseAuthorityStatus status = requestedApproval == approval &&
                toolPurpose == PreparedToolPurpose.JdtNavigation
                    ? PreparedToolUseAuthorityStatus.Current
                    : PreparedToolUseAuthorityStatus.Denied;
            return Task.FromResult(new PreparedToolUseAuthorityResult(status));
        }
    }

    private sealed class AliasQualificationRuntime : IPreparedJdtLsRuntime
    {
        public AliasQualificationSession Session { get; } = new();

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IPreparedJdtLsSession>(Session);
        }
    }

    private sealed class AliasQualificationSession : IPreparedJdtLsSession
    {
        public PreparedJdtLsRawReply Reply { get; set; } = new([], LimitReached: false, CapabilityLimitation: null);

        public int NavigateCallCount { get; private set; }

        public PreparedJavaNavigationRequest? LastRequest { get; private set; }

        public Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NavigateCallCount++;
            LastRequest = request;
            return Task.FromResult(Reply);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static void AssertNavigationSucceeded(JavaNavigationResult result)
    {
        Assert.True(
            result.Status == AnalysisOperationStatus.Succeeded,
            result.Failure is null
                ? "JDT-LS navigation did not return a diagnostic."
                : $"{result.Failure.FailureCode}: {result.Failure.Diagnostic}");
        Assert.Null(result.Failure);
        Assert.NotEmpty(result.Evidence);
    }

    private static string FindFixtureRepository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException(
                "Repository root not found."),
            "tests",
            "Vulgata.Tests",
            "Fixtures",
            "AnalysisRuntime",
            "repository");
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string directory in Directory.GetDirectories(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.GetFiles(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            string target = Path.Combine(
                destination,
                Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static async Task<string> ReadJavaVersionAsync(string javaExecutable)
    {
        ProcessStartInfo startInfo = new(javaExecutable, "-version")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Java 21 process did not start.");
        string output = await process.StandardError.ReadToEndAsync();
        output += await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        return output;
    }

    private static async Task<string> HashAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(
            await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }
}
