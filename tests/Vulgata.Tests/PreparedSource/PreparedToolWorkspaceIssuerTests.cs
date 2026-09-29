using System.Text.Json;
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

    private sealed class ControlledPreparedToolUseAuthorizer : IPreparedToolUseAuthorizer
    {
        private PreparedSourceApproval? _allowedApproval;
        private PreparedToolPurpose _allowedPurpose;
        private PreparedToolUseAuthorityStatus? _currentStatus;

        internal PreparedSourceApproval? LastApproval { get; private set; }

        internal PreparedToolPurpose? LastPurpose { get; private set; }

        internal void Allow(PreparedSourceApproval approval, PreparedToolPurpose purpose)
        {
            _allowedApproval = approval;
            _allowedPurpose = purpose;
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
                toolPurpose == _allowedPurpose
                    ? _currentStatus ?? PreparedToolUseAuthorityStatus.Unavailable
                    : PreparedToolUseAuthorityStatus.Denied;
            return Task.FromResult(new PreparedToolUseAuthorityResult(status));
        }
    }
}
