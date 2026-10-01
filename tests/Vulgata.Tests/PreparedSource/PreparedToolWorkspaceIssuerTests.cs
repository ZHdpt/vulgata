using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;

using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.PreparedSource;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class PreparedToolWorkspaceIssuerTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_WithCurrentJdtLease_MapsContainedFileAndKeepsLeaseOpen()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java");
        Directory.CreateDirectory(javaDirectory);
        string javaFilePath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(javaFilePath, "package example; class Target {}\n");
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        int callbackCount = 0;
        bool? fileWasContained = null;
        PreparedSourceLocation? mappedLocation = null;
        CancellationToken callbackCancellationToken = default;

        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, cancellationToken) =>
                {
                    callbackCount++;
                    callbackCancellationToken = cancellationToken;
                    fileWasContained = scope.TryMapContainedFileUri(
                        new Uri(javaFilePath),
                        out mappedLocation);
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, status);
            Assert.Equal(1, callbackCount);
            Assert.True(fileWasContained);
            Assert.Equal(
                new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/Target.java"),
                mappedLocation);
            Assert.False(callbackCancellationToken.IsCancellationRequested);
            Assert.Equal(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_MapsCanonicalFileUriForFilenameWithSpace()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java");
        Directory.CreateDirectory(javaDirectory);
        string javaFilePath = Path.Combine(javaDirectory, "Greeter Service.java");
        await File.WriteAllTextAsync(javaFilePath, "package example; class GreeterService {}\n");
        Uri escapedJavaFileUri = new(new Uri(javaFilePath).AbsoluteUri, UriKind.Absolute);
        Assert.Contains("%20", escapedJavaFileUri.OriginalString, StringComparison.OrdinalIgnoreCase);

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        bool? mapped = null;
        PreparedSourceLocation? location = null;
        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    mapped = scope.TryMapContainedFileUri(escapedJavaFileUri, out location);
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, status);
            Assert.True(mapped);
            Assert.Equal(
                new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/Greeter Service.java"),
                location);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_MapsCanonicalFileUriForUnicodeFilename()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java");
        Directory.CreateDirectory(javaDirectory);
        string javaFilePath = Path.Combine(javaDirectory, "服务.java");
        await File.WriteAllTextAsync(javaFilePath, "package example; class Service {}\n");
        Uri escapedJavaFileUri = new(new Uri(javaFilePath).AbsoluteUri, UriKind.Absolute);
        Assert.Contains("%E6%9C%8D%E5%8A%A1", escapedJavaFileUri.OriginalString, StringComparison.OrdinalIgnoreCase);

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        bool? mapped = null;
        PreparedSourceLocation? location = null;
        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    mapped = scope.TryMapContainedFileUri(escapedJavaFileUri, out location);
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, status);
            Assert.True(mapped);
            Assert.Equal(
                new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/服务.java"),
                location);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_RejectsEncodedTraversalUriOutsidePreparedRoot()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string outsideFilePath = Path.Combine(fixture.SandboxPath, "outside.java");
        await File.WriteAllTextAsync(outsideFilePath, "class Outside {}\n");
        string preparedRootUri = new Uri(fixture.PreparedRootPath + Path.DirectorySeparatorChar).AbsoluteUri;
        var outsideUri = new Uri(
            preparedRootUri + "src/main/java/%2e%2e/%2e%2e/%2e%2e/%2e%2e/outside.java",
            UriKind.Absolute);

        Assert.Contains("%2e%2e", outsideUri.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.GetFullPath(outsideFilePath), Path.GetFullPath(outsideUri.LocalPath));

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        bool? mapped = null;
        PreparedSourceLocation? location = null;
        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    mapped = scope.TryMapContainedFileUri(outsideUri, out location);
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, status);
            Assert.False(mapped);
            Assert.Null(location);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_RejectsJsonDecodedExtendedLengthPathBeforeUriNormalization()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java");
        Directory.CreateDirectory(javaDirectory);
        string javaFilePath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(javaFilePath, "package example; class Target {}\n");

        string rawDevicePath = $"""\\?\{javaFilePath}""";
        using JsonDocument protocolReply = JsonDocument.Parse(
            JsonSerializer.Serialize(new { uri = rawDevicePath }));
        string decodedRawUri = Assert.IsType<string>(protocolReply.RootElement.GetProperty("uri").GetString());
        Assert.Equal(rawDevicePath, decodedRawUri);
        string canonicalContainedUri = new Uri(javaFilePath).AbsoluteUri;

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        bool? devicePathMapped = null;
        PreparedSourceLocation? devicePathLocation = null;
        bool? canonicalFileMapped = null;
        PreparedSourceLocation? canonicalFileLocation = null;
        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    devicePathMapped = scope.TryMapContainedFileUri(decodedRawUri, out devicePathLocation);
                    canonicalFileMapped = scope.TryMapContainedFileUri(
                        canonicalContainedUri,
                        out canonicalFileLocation);
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, status);
            Assert.False(devicePathMapped);
            Assert.Null(devicePathLocation);
            Assert.True(canonicalFileMapped);
            Assert.Equal(
                new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/Target.java"),
                canonicalFileLocation);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_RejectsUntrustedJsonDecodedRawUriStringsBeforeNormalization()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java");
        Directory.CreateDirectory(javaDirectory);
        string javaFilePath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(javaFilePath, "package example; class Target {}\n");

        string rootUri = new Uri(fixture.PreparedRootPath + Path.DirectorySeparatorChar).AbsoluteUri;
        string symlinkPath = Path.Combine(javaDirectory, "EscapesToSentinel.java");
        File.CreateSymbolicLink(symlinkPath, fixture.SentinelPath);
        string driveRoot = Path.GetPathRoot(javaFilePath)!;
        string fileRelativeToDrive = Path.GetRelativePath(driveRoot, javaFilePath).Replace('\\', '/');
        string rawDevicePath = $"\\\\?\\{javaFilePath}";
        string rawDeviceNamespace = $"\\\\.\\{javaFilePath}";
        string rawExtendedLengthUri = $"file:///?/{driveRoot.Replace('\\', '/')}{fileRelativeToDrive}";
        string[] adversarialStrings =
        [
            "https://example.invalid/Target.java",
            new Uri(fixture.SentinelPath).AbsoluteUri,
            new Uri(symlinkPath).AbsoluteUri,
            rootUri + "%2e%2e/outside/sentinel.txt",
            rootUri + "src/main/java%2FTarget.java",
            "file://server/share/Target.java",
            rawDevicePath,
            rawDeviceNamespace,
            rawExtendedLengthUri,
            new Uri(javaFilePath).AbsoluteUri + "?token=private",
            new Uri(javaFilePath).AbsoluteUri + "#private-fragment",
        ];
        using JsonDocument protocolReply = JsonDocument.Parse(JsonSerializer.Serialize(adversarialStrings));
        string[] decodedStrings = protocolReply.RootElement.EnumerateArray()
            .Select(element => Assert.IsType<string>(element.GetString()))
            .ToArray();
        Assert.Equal(adversarialStrings, decodedStrings);

        string canonicalContainedUri = new Uri(javaFilePath).AbsoluteUri;
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        var rejectedResults = new List<(bool Mapped, PreparedSourceLocation? Location)>();
        bool? canonicalMapped = null;
        PreparedSourceLocation? canonicalLocation = null;
        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    foreach (string rawUri in decodedStrings)
                    {
                        bool mapped = scope.TryMapContainedFileUri(rawUri, out PreparedSourceLocation? location);
                        rejectedResults.Add((mapped, location));
                    }

                    canonicalMapped = scope.TryMapContainedFileUri(
                        canonicalContainedUri,
                        out canonicalLocation);
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, status);
            Assert.Equal(decodedStrings.Length, rejectedResults.Count);
            foreach ((bool mapped, PreparedSourceLocation? location) in rejectedResults)
            {
                Assert.False(mapped);
                Assert.Null(location);
            }

            Assert.True(canonicalMapped);
            Assert.Equal(
                new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/Target.java"),
                canonicalLocation);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_RejectsNonFileOutsideAndSpecialFileUris()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();

        string nestedDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "Section");
        Directory.CreateDirectory(nestedDirectory);
        string nestedTargetPath = Path.Combine(nestedDirectory, "Target.java");
        await File.WriteAllTextAsync(nestedTargetPath, "package example; class Target {}\n");
        string siblingFilePath = Path.Combine(fixture.SiblingPath, "Outside.java");
        await File.WriteAllTextAsync(siblingFilePath, "class Outside {}\n");

        string preparedRootUri = new Uri(fixture.PreparedRootPath + Path.DirectorySeparatorChar).AbsoluteUri;
        var encodedSeparatorUri = new Uri(
            preparedRootUri + "src/main/java/Section%2FTarget.java",
            UriKind.Absolute);
        var siblingFileUri = new Uri(siblingFilePath);
        var nonFileUri = new Uri("https://example.invalid/Target.java", UriKind.Absolute);
        var uncFileUri = new Uri("file://127.0.0.1/share/sentinel.java", UriKind.Absolute);

        Assert.Contains("%2F", encodedSeparatorUri.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.GetFullPath(nestedTargetPath), Path.GetFullPath(encodedSeparatorUri.LocalPath));
        Assert.Equal(Path.GetFullPath(siblingFilePath), Path.GetFullPath(siblingFileUri.LocalPath));
        Assert.False(nonFileUri.IsFile);
        Assert.True(uncFileUri.IsFile);
        Assert.True(uncFileUri.IsUnc);

        (string Description, Uri Uri)[] rejectedInputs =
        [
            ("non-file URI", nonFileUri),
            ("existing sibling file URI", siblingFileUri),
            ("encoded separator resolving to an in-root file", encodedSeparatorUri),
            ("UNC file URI", uncFileUri),
        ];

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        var observations = new List<(string Description, bool Mapped, PreparedSourceLocation? Location)>();
        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    foreach ((string description, Uri uri) in rejectedInputs)
                    {
                        bool mapped = scope.TryMapContainedFileUri(uri, out PreparedSourceLocation? location);
                        observations.Add((description, mapped, location));
                    }

                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, status);
            Assert.Equal(rejectedInputs.Length, observations.Count);
            foreach ((string description, bool mapped, PreparedSourceLocation? location) in observations)
            {
                Assert.False(mapped, $"Expected {description} to be withheld.");
                Assert.Null(location);
            }
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_WhenDeadlineExpiresDuringCallback_ClosesLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(2)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        var callbackEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackDeadlineObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<PreparedToolWorkspaceCheckStatus>? useTask = null;
        try
        {
            useTask = lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                async (_, callbackCancellationToken) =>
                {
                    callbackEntered.TrySetResult(true);
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, callbackCancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        callbackDeadlineObserved.TrySetResult(true);
                        throw;
                    }
                });

            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await callbackDeadlineObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            PreparedToolWorkspaceCheckStatus status = await useTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(PreparedToolWorkspaceCheckStatus.DeadlineExceeded, status);
            Assert.Equal(PreparedToolWorkspaceLifetime.ExpiredOrCancelled, lease.Lifetime);
        }
        finally
        {
            if (useTask is not null)
            {
                await useTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_RequiresMatchingPurposeForSeparateToolLeases()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        var jdtAuthorization = new ControlledPreparedToolUseAuthorizer();
        jdtAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var jdtIssuer = new PreparedToolWorkspaceIssuer(fixture.Authority, jdtAuthorization);
        PreparedToolWorkspaceIssueResult jdtIssue = await jdtIssuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, jdtIssue.Status);
        PreparedToolWorkspaceLease jdtLease = Assert.IsType<PreparedToolWorkspaceLease>(jdtIssue.Lease);

        var codeGraphAuthorization = new ControlledPreparedToolUseAuthorizer();
        codeGraphAuthorization.Allow(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation);
        var codeGraphIssuer = new PreparedToolWorkspaceIssuer(fixture.Authority, codeGraphAuthorization);
        PreparedToolWorkspaceIssueResult codeGraphIssue = await codeGraphIssuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.CodeGraphPreparation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, codeGraphIssue.Status);
        PreparedToolWorkspaceLease codeGraphLease = Assert.IsType<PreparedToolWorkspaceLease>(codeGraphIssue.Lease);

        int jdtCallbackCount = 0;
        int codeGraphCallbackCount = 0;
        int mismatchedCallbackCount = 0;
        try
        {
            PreparedToolWorkspaceCheckStatus jdtStatus = await jdtLease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (_, _) =>
                {
                    jdtCallbackCount++;
                    return ValueTask.CompletedTask;
                });
            PreparedToolWorkspaceCheckStatus codeGraphStatus = await codeGraphLease.UseContainedRootAsync(
                PreparedToolPurpose.CodeGraphPreparation,
                (_, _) =>
                {
                    codeGraphCallbackCount++;
                    return ValueTask.CompletedTask;
                });
            PreparedToolWorkspaceCheckStatus mismatchedStatus = await jdtLease.UseContainedRootAsync(
                PreparedToolPurpose.CodeGraphPreparation,
                (_, _) =>
                {
                    mismatchedCallbackCount++;
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolPurpose.JdtNavigation, jdtLease.ToolPurpose);
            Assert.Equal(PreparedToolPurpose.CodeGraphPreparation, codeGraphLease.ToolPurpose);
            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, jdtStatus);
            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, codeGraphStatus);
            Assert.Equal(PreparedToolWorkspaceCheckStatus.Denied, mismatchedStatus);
            Assert.Equal(1, jdtCallbackCount);
            Assert.Equal(1, codeGraphCallbackCount);
            Assert.Equal(0, mismatchedCallbackCount);
            Assert.Equal(PreparedToolWorkspaceLifetime.Open, jdtLease.Lifetime);
            Assert.Equal(PreparedToolWorkspaceLifetime.Open, codeGraphLease.Lifetime);
        }
        finally
        {
            await jdtLease.DisposeAsync();
            await codeGraphLease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_InvalidatesScopeAfterCallbackReturns()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java");
        Directory.CreateDirectory(javaDirectory);
        string javaFilePath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(javaFilePath, "package example; class Target {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        PreparedToolRootScope? savedScope = null;
        bool mappedWhileCallbackWasActive = false;
        PreparedSourceLocation? liveLocation = null;
        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    savedScope = scope;
                    mappedWhileCallbackWasActive = scope.TryMapContainedFileUri(
                        new Uri(javaFilePath),
                        out liveLocation);
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, status);
            Assert.True(mappedWhileCallbackWasActive);
            Assert.Equal(
                new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/Target.java"),
                liveLocation);
            Assert.NotNull(savedScope);

            bool mappedAfterCallback = savedScope!.TryMapContainedFileUri(
                new Uri(javaFilePath),
                out PreparedSourceLocation? staleLocation);

            Assert.False(mappedAfterCallback);
            Assert.Null(staleLocation);
            Assert.Equal(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_WhenSourceApprovalIsRevokedDuringCallback_RejectsMappedCandidate()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java");
        Directory.CreateDirectory(javaDirectory);
        string javaFilePath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(javaFilePath, "package example; class Target {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        bool mappedInsideCallback = false;
        PreparedSourceLocation? candidateLocation = null;
        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    mappedInsideCallback = scope.TryMapContainedFileUri(
                        new Uri(javaFilePath),
                        out candidateLocation);
                    fixture.Authority.Revoke();
                    return ValueTask.CompletedTask;
                });

            Assert.True(mappedInsideCallback);
            Assert.Equal(
                new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/Target.java"),
                candidateLocation);
            Assert.Equal(PreparedToolWorkspaceCheckStatus.Denied, status);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_WhenToolPurposeIsRevokedDuringCallback_ClosesAndInvalidatesScope()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java");
        Directory.CreateDirectory(javaDirectory);
        string javaFilePath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(javaFilePath, "package example; class Target {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        PreparedToolRootScope? savedScope = null;
        bool mappedInsideCallback = false;
        PreparedSourceLocation? candidateLocation = null;
        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    savedScope = scope;
                    mappedInsideCallback = scope.TryMapContainedFileUri(
                        new Uri(javaFilePath),
                        out candidateLocation);
                    toolAuthorization.SetStatus(PreparedToolUseAuthorityStatus.Denied);
                    return ValueTask.CompletedTask;
                });

            Assert.True(mappedInsideCallback);
            Assert.Equal(
                new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/Target.java"),
                candidateLocation);
            Assert.Equal(PreparedToolWorkspaceCheckStatus.Denied, status);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
            Assert.NotNull(savedScope);
            Assert.False(savedScope!.TryMapContainedFileUri(
                new Uri(javaFilePath),
                out PreparedSourceLocation? staleLocation));
            Assert.Null(staleLocation);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_WhenOwnedCodeGraphEntryChangesRootMetadata_KeepsStableRootLeaseCurrent()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.CodeGraphPreparation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        var (rootOpenStatus, heldRoot) = PreparedSourceTraversal.OpenRoot(fixture.PreparedRootPath);
        Assert.Equal(PreparedSourceTraversal.OpenStatus.Opened, rootOpenStatus);
        Assert.NotNull(heldRoot);
        using (heldRoot)
        {
            PreparedSourceTraversal.DirectoryIdentity initialRootIdentity =
                Assert.IsType<PreparedSourceTraversal.DirectoryIdentity>(
                    PreparedSourceTraversal.ObserveDirectory(heldRoot));
            string ownedEntryPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
            int callbackCount = 0;
            bool rootObjectStayedTheSame = false;
            bool nativeRootWriteTimeChanged = false;
            string? callbackObservationFailure = null;
            try
            {
                PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                    PreparedToolPurpose.CodeGraphPreparation,
                    (_, _) =>
                    {
                        callbackCount++;
                        Directory.CreateDirectory(ownedEntryPath);

                        PreparedSourceTraversal.DirectoryIdentity? afterEntryCreation =
                            PreparedSourceTraversal.ObserveDirectory(heldRoot);
                        if (afterEntryCreation is null)
                        {
                            callbackObservationFailure = "The held root handle could not be observed after entry creation.";
                            return ValueTask.CompletedTask;
                        }

                        if (afterEntryCreation.LastWriteTime == initialRootIdentity.LastWriteTime)
                        {
                            try
                            {
                                DateTime controlledWriteTime = DateTime.UtcNow.AddDays(-1);
                                Directory.SetLastWriteTimeUtc(fixture.PreparedRootPath, controlledWriteTime);
                            }
                            catch (Exception exception)
                            {
                                callbackObservationFailure =
                                    $"Controlled directory timestamp update failed: {exception.GetType().Name}.";
                                return ValueTask.CompletedTask;
                            }

                            afterEntryCreation = PreparedSourceTraversal.ObserveDirectory(heldRoot);
                            if (afterEntryCreation is null)
                            {
                                callbackObservationFailure = "The held root handle could not be observed after timestamp update.";
                                return ValueTask.CompletedTask;
                            }
                        }

                        rootObjectStayedTheSame =
                            afterEntryCreation.VolumeSerialNumber == initialRootIdentity.VolumeSerialNumber &&
                            afterEntryCreation.FileIndexHigh == initialRootIdentity.FileIndexHigh &&
                            afterEntryCreation.FileIndexLow == initialRootIdentity.FileIndexLow;
                        nativeRootWriteTimeChanged =
                            afterEntryCreation.LastWriteTime != initialRootIdentity.LastWriteTime;
                        return ValueTask.CompletedTask;
                    });

                Assert.Equal(1, callbackCount);
                Assert.True(Directory.Exists(ownedEntryPath));
                Assert.True(callbackObservationFailure is null, callbackObservationFailure);
                Assert.True(rootObjectStayedTheSame, "The same held root object must remain after metadata changes.");
                Assert.True(nativeRootWriteTimeChanged);
                Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, status);
                Assert.Equal(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);
                Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, await lease.CheckCurrentAsync());
            }
            finally
            {
                await lease.DisposeAsync();
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_WhenPreparedRootIsReplacedBeforeCallback_DoesNotInvokeCallback()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        int callbackCount = 0;
        try
        {
            string movedOriginalPath = Path.Combine(fixture.SandboxPath, "renamed-prepared-original");
            Directory.Move(fixture.PreparedRootPath, movedOriginalPath);
            Directory.CreateDirectory(fixture.PreparedRootPath);

            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (_, _) =>
                {
                    callbackCount++;
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.RootUnavailable, status);
            Assert.Equal(0, callbackCount);
            Assert.NotEqual(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_WhenCancelledDuringCallback_ClosesLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        using var cancellation = new CancellationTokenSource();
        var callbackEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            Task<PreparedToolWorkspaceCheckStatus> useTask = lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                async (_, callbackCancellationToken) =>
                {
                    callbackEntered.TrySetResult(true);
                    await Task.Delay(Timeout.InfiniteTimeSpan, callbackCancellationToken);
                },
                cancellation.Token);

            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();
            PreparedToolWorkspaceCheckStatus status = await useTask.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Cancelled, status);
            Assert.Equal(PreparedToolWorkspaceLifetime.ExpiredOrCancelled, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_SeparatesDisposableStateFromPreparedRoot()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string descendantStateRoot = Path.Combine(fixture.PreparedRootPath, "tool-state");
        Directory.CreateDirectory(descendantStateRoot);
        string siblingStateRoot = Path.Combine(fixture.SandboxPath, "tool-state");
        Directory.CreateDirectory(siblingStateRoot);

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        bool preparedRootRejected = false;
        bool descendantRejected = false;
        bool siblingAccepted = false;
        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    preparedRootRejected = scope.IsSeparateDisposableStateRoot(fixture.PreparedRootPath);
                    descendantRejected = scope.IsSeparateDisposableStateRoot(descendantStateRoot);
                    siblingAccepted = scope.IsSeparateDisposableStateRoot(siblingStateRoot);
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, status);
            Assert.False(preparedRootRejected);
            Assert.False(descendantRejected);
            Assert.True(siblingAccepted);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_DoesNotMapFileSymlinkToSiblingTarget()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string outsideFilePath = Path.Combine(fixture.SiblingPath, "outside.java");
        await File.WriteAllTextAsync(outsideFilePath, "class Outside {}\n");
        string linkPath = Path.Combine(fixture.PreparedRootPath, "Linked.java");
        File.CreateSymbolicLink(linkPath, outsideFilePath);
        Uri linkUri = new(new Uri(linkPath).AbsoluteUri, UriKind.Absolute);

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        bool? mapped = null;
        PreparedSourceLocation? location = null;
        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    mapped = scope.TryMapContainedFileUri(linkUri, out location);
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, status);
            Assert.False(mapped);
            Assert.Null(location);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_RejectsDisposableStateSymlinkIntoPreparedRoot()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string preparedTargetPath = Path.Combine(fixture.PreparedRootPath, "state-target");
        Directory.CreateDirectory(preparedTargetPath);
        string redirectedStatePath = Path.Combine(fixture.SandboxPath, "redirected-tool-state");
        Directory.CreateSymbolicLink(redirectedStatePath, preparedTargetPath);

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        bool? acceptedAsDisposableState = null;
        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    acceptedAsDisposableState = scope.IsSeparateDisposableStateRoot(redirectedStatePath);
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, status);
            Assert.False(acceptedAsDisposableState);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_WhenPreparedRootIsReplacedDuringCallback_RejectsMappedCandidate()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java");
        Directory.CreateDirectory(javaDirectory);
        string javaFilePath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(javaFilePath, "package example; class Target {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        bool? mapped = null;
        PreparedSourceLocation? candidateLocation = null;
        try
        {
            PreparedToolWorkspaceCheckStatus status = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    mapped = scope.TryMapContainedFileUri(new Uri(javaFilePath), out candidateLocation);
                    string movedOriginalPath = Path.Combine(fixture.SandboxPath, "renamed-prepared-original");
                    Directory.Move(fixture.PreparedRootPath, movedOriginalPath);
                    Directory.CreateDirectory(fixture.PreparedRootPath);
                    return ValueTask.CompletedTask;
                });

            Assert.True(mapped);
            Assert.Equal(
                new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/Target.java"),
                candidateLocation);
            Assert.Equal(PreparedToolWorkspaceCheckStatus.RootUnavailable, status);
            Assert.NotEqual(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseContainedRootAsync_DisposalWaitsForCallbackAndInvalidatesScopeAfterward()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java");
        Directory.CreateDirectory(javaDirectory);
        string javaFilePath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(javaFilePath, "package example; class Target {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        var callbackEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackExited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<PreparedToolWorkspaceCheckStatus>? useTask = null;
        Task? disposeTask = null;
        int callbackCount = 0;
        bool? mappedDuringCallback = null;
        PreparedSourceLocation? location = null;
        PreparedToolRootScope? savedScope = null;
        try
        {
            useTask = lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                async (scope, cancellationToken) =>
                {
                    callbackCount++;
                    savedScope = scope;
                    callbackEntered.TrySetResult(true);
                    await continueCallback.Task.WaitAsync(cancellationToken);
                    mappedDuringCallback = scope.TryMapContainedFileUri(new Uri(javaFilePath), out location);
                    callbackExited.TrySetResult(true);
                });

            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            disposeTask = lease.DisposeAsync().AsTask();
            Assert.False(disposeTask.IsCompleted);
            Assert.Equal(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);

            continueCallback.TrySetResult(true);
            await callbackExited.Task.WaitAsync(TimeSpan.FromSeconds(5));
            PreparedToolWorkspaceCheckStatus status = await useTask.WaitAsync(TimeSpan.FromSeconds(5));
            await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(mappedDuringCallback);
            Assert.Equal(
                new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/Target.java"),
                location);
            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, status);
            Assert.Equal(1, callbackCount);
            Assert.Equal(PreparedToolWorkspaceLifetime.Disposed, lease.Lifetime);
            Assert.True(callbackExited.Task.IsCompleted);
            Assert.NotNull(savedScope);

            bool mappedAfterDisposal = savedScope!.TryMapContainedFileUri(
                new Uri(javaFilePath),
                out PreparedSourceLocation? disposedScopeLocation);

            Assert.False(mappedAfterDisposal);
            Assert.Null(disposedScopeLocation);
        }
        finally
        {
            continueCallback.TrySetResult(true);
            if (useTask is not null)
            {
                await useTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (disposeTask is not null)
            {
                await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenLeaseDeadlinePasses_ClosesLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(2)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(2_200));

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(PreparedToolWorkspaceCheckStatus.DeadlineExceeded, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.ExpiredOrCancelled, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_AfterDisposal_ReturnsDisposedWithoutReopening()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        await lease.DisposeAsync();
        PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

        Assert.Equal(PreparedToolWorkspaceCheckStatus.Disposed, check);
        Assert.Equal(PreparedToolWorkspaceLifetime.Disposed, lease.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenApprovalRootBindingChanges_ClosesLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            fixture.Authority.Approve(fixture.Approval with
            {
                RootBindingIdentity = "replacement-root-binding",
            });

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Denied, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
            Assert.Equal(fixture.Approval, fixture.Authority.LastRequestedApproval);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenBothAuthoritiesAndRootRemainCurrent_LeavesLeaseOpen()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);
            Assert.Equal(fixture.Approval, fixture.Authority.LastRequestedApproval);
            Assert.Equal(fixture.Approval, toolAuthorization.LastApproval);
            Assert.Equal(PreparedToolPurpose.JdtNavigation, toolAuthorization.LastPurpose);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenSourceVerifierBecomesUnavailable_ClosesLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            fixture.Authority.MakeUnavailable();

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(PreparedToolWorkspaceCheckStatus.AuthorityUnavailable, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task IssueAsync_WhenAlreadyCancelledOrExpired_ReturnsNoLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        PreparedToolWorkspaceIssueResult cancelled =
            await issuer.IssueAsync(request, cancellation.Token);
        PreparedToolWorkspaceIssueResult expired =
            await issuer.IssueAsync(request with { Deadline = TimeSpan.Zero });

        Assert.Equal(PreparedToolWorkspaceIssueStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.Lease);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.DeadlineExceeded, expired.Status);
        Assert.Null(expired.Lease);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenCancelled_ClosesLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync(cancellation.Token);

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Cancelled, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.ExpiredOrCancelled, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Constructor_RequiresBothCurrentAuthorityVerifiers()
    {
        var sourceAuthority = new ControlledPreparedSourceAuthority();
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();

        Assert.Throws<ArgumentNullException>(
            () => new PreparedToolWorkspaceIssuer(null!, toolAuthorization));
        Assert.Throws<ArgumentNullException>(
            () => new PreparedToolWorkspaceIssuer(sourceAuthority, null!));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(
        PreparedToolUseAuthorityStatus.Denied,
        PreparedToolWorkspaceIssueStatus.Denied)]
    [InlineData(
        PreparedToolUseAuthorityStatus.Unavailable,
        PreparedToolWorkspaceIssueStatus.AuthorityUnavailable)]
    public async Task IssueAsync_WhenToolPurposeIsNotCurrent_ReturnsNoLease(
        PreparedToolUseAuthorityStatus toolStatus,
        PreparedToolWorkspaceIssueStatus expectedStatus)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        toolAuthorization.SetStatus(toolStatus);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(5));

        PreparedToolWorkspaceIssueResult result = await issuer.IssueAsync(request);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Lease);
        Assert.Equal(fixture.Approval, toolAuthorization.LastApproval);
        Assert.Equal(PreparedToolPurpose.JdtNavigation, toolAuthorization.LastPurpose);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(
        PreparedToolUseAuthorityStatus.Denied,
        PreparedToolWorkspaceCheckStatus.Denied)]
    [InlineData(
        PreparedToolUseAuthorityStatus.Unavailable,
        PreparedToolWorkspaceCheckStatus.AuthorityUnavailable)]
    public async Task CheckCurrentAsync_WhenToolPurposeIsNoLongerCurrent_ClosesLease(
        PreparedToolUseAuthorityStatus toolStatus,
        PreparedToolWorkspaceCheckStatus expectedCheck)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(5));

        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(request);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            toolAuthorization.SetStatus(toolStatus);

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(expectedCheck, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
            Assert.Equal(fixture.Approval, toolAuthorization.LastApproval);
            Assert.Equal(PreparedToolPurpose.JdtNavigation, toolAuthorization.LastPurpose);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenSourceApprovalIsRevoked_ClosesLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(5));

        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(request);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            fixture.Authority.Revoke();

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Denied, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenPreparedRootIsReplaced_ClosesOriginalRootLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(5));

        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(request);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            string movedOriginalPath = Path.Combine(fixture.SandboxPath, "renamed-prepared-original");
            Directory.Move(fixture.PreparedRootPath, movedOriginalPath);
            Directory.CreateDirectory(fixture.PreparedRootPath);

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(PreparedToolWorkspaceCheckStatus.RootUnavailable, check);
            Assert.NotEqual(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task IssueAsync_CurrentExactApprovalAndJdtPurpose_ReturnsBoundTrustedLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(5));

        PreparedToolWorkspaceIssueResult result = await issuer.IssueAsync(request);

        if (result.Lease is not null)
        {
            await result.Lease.DisposeAsync();
        }

        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, result.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(result.Lease);
        Assert.Equal(fixture.Approval.RepositoryId, lease.RepositoryId);
        Assert.Equal(PreparedToolPurpose.JdtNavigation, lease.ToolPurpose);
        Assert.Equal(PreparedToolWorkspaceLifetime.Disposed, lease.Lifetime);
        Assert.Equal(fixture.Approval, fixture.Authority.LastRequestedApproval);
        Assert.Equal(fixture.Approval, toolAuthorization.LastApproval);
        Assert.Equal(PreparedToolPurpose.JdtNavigation, toolAuthorization.LastPurpose);

        string serializedLease = JsonSerializer.Serialize(lease);
        Assert.DoesNotContain(fixture.PreparedRootPath, serializedLease, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalRootPath", serializedLease, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task IssueAsync_CodeGraphPreparationLeaseRemainsTrackedUntilContainedUseDisposes()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        var binding = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 0,
            Phase: PreparedCodeGraphOwnershipPhase.Absent,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{Guid.NewGuid():N}",
            StateEntryObjectIdentity: null);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation);
        var ownershipStore = new ControlledProgressingOwnershipStore(binding);
        var hostProtection = new ControlledRegistryHostVerifier(binding);
        var registry = new PreparedWorkspaceUseRegistry(
            fixture.Authority,
            toolAuthorization,
            ownershipStore,
            CreateRegistryPolicy(fixture.SandboxPath, fixture.PreparedRootPath, sourceStorageRoot),
            hostProtection,
            new SettledPreviousGenerationVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(5),
            dispositionDeadline: TimeSpan.FromSeconds(5));
        var issuer = new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization,
            binding,
            registry);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.CodeGraphPreparation,
            TimeSpan.FromSeconds(5));
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PreparedToolWorkspaceLease? lease = null;
        PreparedToolWorkspaceLease? issueAfterClosingLease = null;
        Task<PreparedToolWorkspaceCheckStatus>? containedUse = null;
        PreparedWorkspaceAdmissionResult? admission = null;
        PreparedToolWorkspaceIssueResult? issue = null;
        PreparedWorkspaceSettlementStatus? whileContainedUseActive = null;
        PreparedToolWorkspaceIssueResult? issueAfterClosing = null;
        PreparedToolWorkspaceCheckStatus? containedUseResult = null;
        PreparedWorkspaceSettlementStatus? afterLeaseDisposed = null;

        try
        {
            admission = await registry.AdmitAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation);
            if (admission.Status == PreparedWorkspaceAdmissionStatus.Opened)
            {
                issue = await issuer.IssueAsync(request);
                lease = issue.Lease;
            }

            if (lease is not null)
            {
                containedUse = lease.UseContainedRootAsync(
                    PreparedToolPurpose.CodeGraphPreparation,
                    async (scope, cancellationToken) =>
                    {
                        Assert.Equal(
                            Path.GetFullPath(fixture.PreparedRootPath),
                            scope.GetPhysicalRootPathForTrustedToolBinding());
                        callbackStarted.TrySetResult();
                        await releaseCallback.Task.WaitAsync(cancellationToken);
                    });
                await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

                whileContainedUseActive = await registry.CloseForDispositionAsync(binding);
                issueAfterClosing = await issuer.IssueAsync(request);
                issueAfterClosingLease = issueAfterClosing.Lease;

                releaseCallback.TrySetResult();
                containedUseResult = await containedUse.WaitAsync(TimeSpan.FromSeconds(2));
                await lease.DisposeAsync();
                lease = null;
                afterLeaseDisposed = await registry.CloseForDispositionAsync(binding);
            }
        }
        finally
        {
            releaseCallback.TrySetResult();
            if (containedUse is not null)
            {
                try
                {
                    await containedUse.WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch
                {
                    // Ensure a controlled callback cannot outlive fixture cleanup.
                }
            }

            if (issueAfterClosingLease is not null)
            {
                await issueAfterClosingLease.DisposeAsync();
            }

            if (lease is not null)
            {
                await lease.DisposeAsync();
            }

            await registry.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(PreparedWorkspaceAdmissionStatus.Opened, admission?.Status);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue?.Status);
        Assert.True(whileContainedUseActive == PreparedWorkspaceSettlementStatus.Active,
            "A tracked CodeGraph preparation lease stays active while its contained root operation is live.");
        Assert.True(issueAfterClosing?.Lease is null &&
            issueAfterClosing?.Status != PreparedToolWorkspaceIssueStatus.Issued,
            "Once disposition closes issuance, the tracked issuer cannot publish another lease.");
        Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, containedUseResult);
        Assert.Equal(PreparedWorkspaceSettlementStatus.Settled, afterLeaseDisposed);
        Assert.Equal(1, hostProtection.Hold.DisposeCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(PreparedToolPurpose.CodeGraphPreparation)]
    [InlineData(PreparedToolPurpose.JdtNavigation)]
    public async Task IssueAsync_AfterSameKeyReadyProgressionReloadsCurrentBinding(
        PreparedToolPurpose leasePurpose)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(sourceStorageRoot);
        var absent = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 0,
            Phase: PreparedCodeGraphOwnershipPhase.Absent,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{Guid.NewGuid():N}",
            StateEntryObjectIdentity: null);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(
            fixture.Approval,
            PreparedToolPurpose.CodeGraphPreparation,
            PreparedToolPurpose.JdtNavigation);
        var ownershipStore = new ControlledProgressingOwnershipStore(absent);
        var hostProtection = new ControlledRegistryHostVerifier(absent);
        var registry = new PreparedWorkspaceUseRegistry(
            fixture.Authority,
            toolAuthorization,
            ownershipStore,
            CreateRegistryPolicy(fixture.SandboxPath, fixture.PreparedRootPath, sourceStorageRoot),
            hostProtection,
            new SettledPreviousGenerationVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(5),
            dispositionDeadline: TimeSpan.FromSeconds(5));
        var issuer = new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization,
            absent,
            registry);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            leasePurpose,
            TimeSpan.FromSeconds(5));
        PreparedToolWorkspaceLease? initialLease = null;
        PreparedToolWorkspaceLease? progressedLease = null;
        PreparedWorkspaceAdmissionResult? admission = null;
        PreparedToolWorkspaceIssueResult? initialIssue = null;
        PreparedToolWorkspaceIssueResult? progressedIssue = null;

        try
        {
            admission = await registry.AdmitAsync(absent, PreparedToolPurpose.CodeGraphPreparation);
            if (admission.Status == PreparedWorkspaceAdmissionStatus.Opened)
            {
                initialIssue = await issuer.IssueAsync(request);
                initialLease = initialIssue.Lease;
            }

            if (initialLease is not null)
            {
                await initialLease.DisposeAsync();
                initialLease = null;
            }

            PreparedCodeGraphOwnershipTransitionResult creatingCommit =
                await ownershipStore.BeginCreateAsync(absent);
            PreparedCodeGraphWorkspaceOwnership? creating = creatingCommit.Record;
            if (creating is { Phase: PreparedCodeGraphOwnershipPhase.Creating })
            {
                Directory.CreateDirectory(codeGraphPath);
                PreparedToolDirectoryObjectIdentity createdEntry = ObserveDirectoryIdentity(codeGraphPath);
                PreparedCodeGraphOwnershipTransitionResult readyCommit = await ownershipStore.CommitCreatedAsync(
                    creating,
                    createdEntry);
                if (readyCommit.Record is { Phase: PreparedCodeGraphOwnershipPhase.Ready })
                {
                    int loadsBeforeReadyIssue = ownershipStore.LoadCount;
                    progressedIssue = await issuer.IssueAsync(request);
                    progressedLease = progressedIssue.Lease;

                    Assert.True(ownershipStore.LoadCount > loadsBeforeReadyIssue &&
                        ownershipStore.LastReadbackRecord == readyCommit.Record,
                        "The same issuer must use the exact fresh Ready v2 readback, not its constructor's Absent v0 copy.");
                    Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, progressedIssue.Status);
                    Assert.True(hostProtection.VerifyAndHoldCount == 1 &&
                        hostProtection.Hold.LastCheckedBinding == readyCommit.Record,
                        "The registry rechecks the original retained host hold against the current Ready record.");
                    Assert.Equal(leasePurpose, progressedLease?.ToolPurpose);
                    Assert.Equal(PreparedToolWorkspaceCheckStatus.Current,
                        await progressedLease!.CheckCurrentAsync());
                }
            }
        }
        finally
        {
            if (progressedLease is not null)
            {
                await progressedLease.DisposeAsync();
            }

            if (initialLease is not null)
            {
                await initialLease.DisposeAsync();
            }

            await registry.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(PreparedWorkspaceAdmissionStatus.Opened, admission?.Status);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, initialIssue?.Status);
        Assert.True(ownershipStore.CurrentBinding.Phase == PreparedCodeGraphOwnershipPhase.Ready &&
            ownershipStore.CurrentBinding.Version == 2 &&
            ownershipStore.CurrentBinding.StateEntryObjectIdentity is not null,
            "The controlled coordinator commits one same-key Creating v1 then Ready v2 record.");
        Assert.Equal(1, hostProtection.Hold.DisposeCount);
    }

    private static RetainedSourceCapturePolicy CreateRegistryPolicy(
        string sandboxPath,
        string preparedRootPath,
        string sourceStorageRoot)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceStorage:RootPath"] = sourceStorageRoot,
                ["SourceStorage:MaximumEntries"] = "10000",
                ["SourceStorage:MaximumBlobBytes"] = "8388608",
                ["SourceStorage:MaximumAggregateBlobBytes"] = "67108864",
                ["SourceStorage:CaptureDeadlineSeconds"] = "600",
                ["SourceStorage:OperationStaleAfterMinutes"] = "1440",
                ["SourceStorage:VerificationBufferBytes"] = "65536",
                ["SourceStorage:Read:DeadlineSeconds"] = "60",
            })
            .Build();

        return RetainedSourceCapturePolicy.Create(configuration, sandboxPath, preparedRootPath);
    }

    private static PreparedToolDirectoryObjectIdentity ObserveDirectoryIdentity(string path)
    {
        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? handle) =
            PreparedSourceTraversal.OpenRoot(path);
        using (handle)
        {
            Assert.Equal(PreparedSourceTraversal.OpenStatus.Opened, status);
            PreparedSourceTraversal.DirectoryIdentity observed = Assert.IsType<PreparedSourceTraversal.DirectoryIdentity>(
                handle is null ? null : PreparedSourceTraversal.ObserveDirectory(handle));
            return new PreparedToolDirectoryObjectIdentity(
                observed.VolumeSerialNumber,
                observed.FileIndexHigh,
                observed.FileIndexLow);
        }
    }

    private static bool SameImmutableKey(
        PreparedCodeGraphWorkspaceOwnership left,
        PreparedCodeGraphWorkspaceOwnership right) =>
        left.OwnershipId == right.OwnershipId &&
        left.Approval == right.Approval &&
        left.PhysicalRootPath == right.PhysicalRootPath &&
        left.RootObjectIdentity == right.RootObjectIdentity &&
        string.Equals(left.WorkspaceOwnerIdentity, right.WorkspaceOwnerIdentity, StringComparison.Ordinal);

    private sealed class ControlledProgressingOwnershipStore(
        PreparedCodeGraphWorkspaceOwnership initialAbsent) : IPreparedCodeGraphOwnershipStore
    {
        private PreparedCodeGraphWorkspaceOwnership _currentBinding = initialAbsent;

        internal PreparedCodeGraphWorkspaceOwnership? LastLoadedBinding { get; private set; }

        internal PreparedCodeGraphWorkspaceOwnership? LastReadbackRecord { get; private set; }

        internal PreparedCodeGraphWorkspaceOwnership CurrentBinding => _currentBinding;

        internal int LoadCount { get; private set; }

        public Task<PreparedCodeGraphOwnershipLoadResult> LoadAsync(
            PreparedCodeGraphWorkspaceOwnership expectedBinding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            LastLoadedBinding = expectedBinding;
            LastReadbackRecord = null;
            if (!SameImmutableKey(expectedBinding, _currentBinding))
            {
                return Task.FromResult(new PreparedCodeGraphOwnershipLoadResult(
                    PreparedCodeGraphOwnershipLoadStatus.Unavailable,
                    null));
            }

            if (_currentBinding.Phase == PreparedCodeGraphOwnershipPhase.Absent)
            {
                return Task.FromResult(new PreparedCodeGraphOwnershipLoadResult(
                    PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent,
                    null));
            }

            LastReadbackRecord = _currentBinding;
            return Task.FromResult(_currentBinding.Phase == PreparedCodeGraphOwnershipPhase.Ready
                ? new PreparedCodeGraphOwnershipLoadResult(
                    PreparedCodeGraphOwnershipLoadStatus.Found,
                    _currentBinding)
                : new PreparedCodeGraphOwnershipLoadResult(
                    PreparedCodeGraphOwnershipLoadStatus.Unavailable,
                    null));
        }

        public Task<PreparedCodeGraphOwnershipTransitionResult> BeginCreateAsync(
            PreparedCodeGraphWorkspaceOwnership absent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (absent != _currentBinding || absent.Phase != PreparedCodeGraphOwnershipPhase.Absent ||
                absent.StateEntryObjectIdentity is not null || absent.Version == long.MaxValue)
            {
                return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                    PreparedCodeGraphOwnershipTransitionStatus.Conflict,
                    _currentBinding));
            }

            _currentBinding = absent with
            {
                Version = absent.Version + 1,
                Phase = PreparedCodeGraphOwnershipPhase.Creating,
            };
            return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                PreparedCodeGraphOwnershipTransitionStatus.Committed,
                _currentBinding));
        }

        public Task<PreparedCodeGraphOwnershipTransitionResult> CommitCreatedAsync(
            PreparedCodeGraphWorkspaceOwnership creating,
            PreparedToolDirectoryObjectIdentity createdEntry,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (creating != _currentBinding || creating.Phase != PreparedCodeGraphOwnershipPhase.Creating ||
                creating.Version == long.MaxValue)
            {
                return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                    PreparedCodeGraphOwnershipTransitionStatus.Conflict,
                    _currentBinding));
            }

            _currentBinding = creating with
            {
                Version = creating.Version + 1,
                Phase = PreparedCodeGraphOwnershipPhase.Ready,
                StateEntryObjectIdentity = createdEntry,
            };
            return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                PreparedCodeGraphOwnershipTransitionStatus.Committed,
                _currentBinding));
        }

        public Task<PreparedCodeGraphOwnershipTransitionResult> BeginDispositionAsync(
            PreparedCodeGraphWorkspaceOwnership ready,
            CancellationToken cancellationToken = default) => UnsupportedTransitionAsync(cancellationToken);

        public Task<PreparedCodeGraphOwnershipTransitionResult> CommitRemovedAsync(
            PreparedCodeGraphWorkspaceOwnership disposing,
            CancellationToken cancellationToken = default) => UnsupportedTransitionAsync(cancellationToken);

        public Task<PreparedCodeGraphOwnershipTransitionResult> QuarantineAsync(
            PreparedCodeGraphWorkspaceOwnership current,
            CancellationToken cancellationToken = default) => UnsupportedTransitionAsync(cancellationToken);

        private Task<PreparedCodeGraphOwnershipTransitionResult> UnsupportedTransitionAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                PreparedCodeGraphOwnershipTransitionStatus.Unavailable,
                _currentBinding));
        }
    }

    private sealed class ControlledRegistryHostVerifier(
        PreparedCodeGraphWorkspaceOwnership binding) : IPreparedManagedWorkspaceHostVerifier
    {
        internal ControlledRegistryHostHold Hold { get; } = new(binding);

        internal int VerifyAndHoldCount { get; private set; }

        public Task<PreparedManagedWorkspaceHostObservation> ObserveAsync(
            PreparedSourceGrantRequest applicationHeldSourceGrant,
            RetainedSourceCapturePolicy sourceStoragePolicy,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PreparedManagedWorkspaceHostObservation(
                PreparedManagedWorkspaceHostStatus.Observed,
                binding.RootObjectIdentity,
                binding.WorkspaceOwnerIdentity));

        public Task<PreparedManagedWorkspaceHostHoldResult> VerifyAndHoldAsync(
            PreparedCodeGraphWorkspaceOwnership requestedBinding,
            RetainedSourceCapturePolicy sourceStoragePolicy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerifyAndHoldCount++;
            return Task.FromResult(requestedBinding == binding
                ? new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Protected, Hold)
                : new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Denied, null));
        }
    }

    private sealed class ControlledRegistryHostHold(
        PreparedCodeGraphWorkspaceOwnership binding) : IPreparedManagedWorkspaceHostHold
    {
        internal int DisposeCount { get; private set; }

        internal PreparedCodeGraphWorkspaceOwnership? LastCheckedBinding { get; private set; }

        private bool _disposed;

        public ValueTask<PreparedManagedWorkspaceHostStatus> CheckCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership requestedBinding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCheckedBinding = requestedBinding;
            return ValueTask.FromResult(!_disposed && SameImmutableKey(requestedBinding, binding) &&
                (requestedBinding == binding ||
                 requestedBinding.Phase == PreparedCodeGraphOwnershipPhase.Ready &&
                 requestedBinding.Version == binding.Version + 2 &&
                 requestedBinding.StateEntryObjectIdentity is not null)
                ? PreparedManagedWorkspaceHostStatus.Protected
                : PreparedManagedWorkspaceHostStatus.Unavailable);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SettledPreviousGenerationVerifier : IPreparedWorkspacePreviousGenerationVerifier
    {
        public ValueTask<PreparedWorkspaceSettlementStatus> VerifySettledAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(PreparedWorkspaceSettlementStatus.Settled);
        }
    }

    private sealed class ControlledPreparedToolUseAuthorizer : IPreparedToolUseAuthorizer
    {
        private PreparedSourceApproval? _allowedApproval;
        private readonly HashSet<PreparedToolPurpose> _allowedPurposes = [];
        private PreparedToolUseAuthorityStatus? _currentStatus;

        internal PreparedSourceApproval? LastApproval { get; private set; }

        internal PreparedToolPurpose? LastPurpose { get; private set; }

        internal void Allow(PreparedSourceApproval approval, params PreparedToolPurpose[] purposes)
        {
            _allowedApproval = approval;
            _allowedPurposes.Clear();
            _allowedPurposes.UnionWith(purposes);
            _currentStatus = PreparedToolUseAuthorityStatus.Current;
        }

        internal void SetStatus(PreparedToolUseAuthorityStatus status)
        {
            _currentStatus = status;
        }

        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval approval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastApproval = approval;
            LastPurpose = toolPurpose;
            PreparedToolUseAuthorityStatus status = approval == _allowedApproval &&
                _allowedPurposes.Contains(toolPurpose)
                    ? _currentStatus ?? PreparedToolUseAuthorityStatus.Unavailable
                    : PreparedToolUseAuthorityStatus.Denied;
            return Task.FromResult(new PreparedToolUseAuthorityResult(status));
        }
    }
}
