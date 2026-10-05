using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Source;

using Vulgata.Tests.PreparedSource;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.AnalysisRuntime;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class JdtLsPreparedNavigationTests
{
    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [InlineData(9)]
    [InlineData(10)]
    public async Task GetDefinitionAsync_WhenPurposeIsDeniedAfterMappingOrBeforeDisclosure_RetiresSession(
        int denyOnVerification)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "Caller.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "class Caller {}\n");

        fixture.Authority.Approve(fixture.Approval);
        // Issue and successful factory open perform six tool checks. Navigation then checks
        // before the request, before and after the contained callback, and before disclosure.
        var toolAuthorization = new DenyingToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation,
            denyOnVerification);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var runtime = new ControlledPreparedJdtLsRuntime();
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            [new PreparedJdtLsRawLocation(new Uri(callerPath).AbsoluteUri, 2, 1, 2, 8)],
            LimitReached: true,
            CapabilityLimitation: "controlled limit detail");
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(
            fixture,
            new PreparedJavaNavigationProfile(
                disposableStateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime);
        PreparedJavaNavigationOpenResult? open = null;

        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);
            Assert.Equal(6, toolAuthorization.VerificationCount);

            PreparedJavaNavigationResult result = await context.GetDefinitionAsync(
                new PreparedJavaNavigationRequest("src/main/java/Caller.java", 1, 1, 10));

            Assert.Equal(PreparedJavaNavigationOutcome.Denied, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(1, runtime.Session.NavigateCallCount);
            Assert.Equal(1, runtime.Session.DisposeCallCount);
            Assert.Equal(denyOnVerification, toolAuthorization.VerificationCount);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
            string serializedResult = JsonSerializer.Serialize(result);
            Assert.DoesNotContain(callerPath, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(disposableStateRoot, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("controlled limit detail", serializedResult, StringComparison.Ordinal);
        }
        finally
        {
            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [InlineData(PreparedSourceAuthorityStatus.Revoked, PreparedJavaNavigationOutcome.Denied)]
    [InlineData(PreparedSourceAuthorityStatus.Unavailable, PreparedJavaNavigationOutcome.AuthorityUnavailable)]
    public async Task GetDefinitionAsync_WhenSourceAuthorityIsLostBeforeDisclosure_DiscardsBufferedPayload(
        PreparedSourceAuthorityStatus loss,
        PreparedJavaNavigationOutcome expectedOutcome)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "Caller.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "class Caller {}\n");

        fixture.Authority.Approve(fixture.Approval);
        // The ownership-view check joins the six successful issue/open checks before navigation;
        // lose source authority on the final pre-disclosure check.
        var sourceAuthority = new DenyingSourceAuthority(
            fixture.Authority,
            denyOnVerification: 11,
            loss: loss);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(sourceAuthority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var runtime = new ControlledPreparedJdtLsRuntime();
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            [new PreparedJdtLsRawLocation(new Uri(callerPath).AbsoluteUri, 2, 1, 2, 8)],
            LimitReached: true,
            CapabilityLimitation: "controlled limit detail");
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(
            fixture,
            new PreparedJavaNavigationProfile(
                disposableStateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime,
            sourceAuthority);
        PreparedJavaNavigationOpenResult? open = null;

        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);
            Assert.Equal(7, sourceAuthority.VerificationCount);

            PreparedJavaNavigationResult result = await context.GetDefinitionAsync(
                new PreparedJavaNavigationRequest("src/main/java/Caller.java", 1, 1, 10));

            Assert.Equal(expectedOutcome, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(1, runtime.Session.NavigateCallCount);
            Assert.Equal(1, runtime.Session.DisposeCallCount);
            Assert.Equal(11, sourceAuthority.VerificationCount);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
            string serializedResult = JsonSerializer.Serialize(result);
            Assert.DoesNotContain(callerPath, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(disposableStateRoot, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("controlled limit detail", serializedResult, StringComparison.Ordinal);
        }
        finally
        {
            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task GetDefinitionAsync_WhenAuthorityLossCannotSettleSession_QuarantinesRepository()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "Caller.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "class Caller {}\n");
        fixture.Authority.Approve(fixture.Approval);

        var toolAuthorization = new DenyingToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation,
            denyOnVerification: 10);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var runtime = new UnconfirmedDisposeNavigationRuntime(new PreparedJdtLsRawReply(
            [new PreparedJdtLsRawLocation(new Uri(callerPath).AbsoluteUri, 2, 1, 2, 8)],
            LimitReached: true,
            CapabilityLimitation: "controlled limit detail"));
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(
            fixture,
            new PreparedJavaNavigationProfile(
                disposableStateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime);
        PreparedJavaNavigationOpenResult? open = null;
        PreparedToolWorkspaceLease? retryLease = null;
        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);

            PreparedJavaNavigationResult result = await context.GetDefinitionAsync(
                new PreparedJavaNavigationRequest("src/main/java/Caller.java", 1, 1, 10));

            Assert.Equal(PreparedJavaNavigationOutcome.ConfigurationUnavailable, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(1, runtime.OpenCallCount);
            Assert.Equal(1, runtime.Session.DisposeCallCount);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
            Assert.DoesNotContain("controlled limit detail", JsonSerializer.Serialize(result), StringComparison.Ordinal);

            var retryAuthorization = new ControlledPreparedToolUseAuthorizer(
                fixture.Approval,
                PreparedToolPurpose.JdtNavigation);
            PreparedToolWorkspaceIssueResult retryIssue = await new PreparedToolWorkspaceIssuer(
                fixture.Authority,
                retryAuthorization).IssueAsync(
                    new PreparedToolWorkspaceGrantRequest(
                        fixture.Approval,
                        fixture.PreparedRootPath,
                        PreparedToolPurpose.JdtNavigation,
                        TimeSpan.FromSeconds(10)));
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, retryIssue.Status);
            retryLease = Assert.IsType<PreparedToolWorkspaceLease>(retryIssue.Lease);

            PreparedJavaNavigationOpenResult retry = await factory.OpenAsync(retryLease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, retry.Status);
            Assert.Null(retry.Context);
            Assert.Equal(1, runtime.OpenCallCount);
        }
        finally
        {
            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            if (retryLease is not null)
            {
                await retryLease.DisposeAsync();
            }

            await lease.DisposeAsync();
        }

        Assert.Equal(1, runtime.Session.DisposeCallCount);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [InlineData(@"C:\outside\Secret.java")]
    [InlineData("../outside/Secret.java")]
    [InlineData(@"src\main\java\Secret.java")]
    public async Task DefinitionFunction_WithNonCanonicalRepositoryPath_DeniesWithoutSessionCall(
        string relativePath)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string callerDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example");
        Directory.CreateDirectory(callerDirectory);
        await File.WriteAllTextAsync(Path.Combine(callerDirectory, "Caller.java"), "class Caller {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var runtime = new ControlledPreparedJdtLsRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(
            fixture,
            new PreparedJavaNavigationProfile(
                disposableStateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime);
        PreparedJavaNavigationOpenResult? open = null;

        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);
            AIFunction definitionFunction = Assert.Single(
                new PreparedJavaNavigationFunctions(context).Create().Where(
                    function => function.Name == "navigate_java_definition"));

            object? rawResult = await definitionFunction.InvokeAsync(
                new AIFunctionArguments(new Dictionary<string, object?>
                {
                    ["relativePath"] = relativePath,
                    ["line"] = 1,
                    ["character"] = 1,
                    ["maximumLocations"] = 5,
                }),
                CancellationToken.None);

            PreparedJavaNavigationResult result = Assert.IsType<PreparedJavaNavigationResult>(rawResult);
            Assert.Equal(PreparedJavaNavigationOutcome.Denied, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(0, runtime.Session.NavigateCallCount);
            string serializedResult = JsonSerializer.Serialize(result);
            Assert.DoesNotContain(fixture.PreparedRootPath, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(relativePath, serializedResult, StringComparison.Ordinal);
        }
        finally
        {
            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_WithCurrentJdtLease_PassesFixedProfileAndReturnsOneContext()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledPreparedJdtLsRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;
        bool contextDisposed = false;

        try
        {
            open = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);
            Assert.Equal(1, runtime.OpenCallCount);
            Assert.Same(lease, runtime.LastWorkspaceLease);
            Assert.Equal(profile, runtime.LastProfile);
            Assert.True(runtime.LastCancellationToken.CanBeCanceled);
            Assert.Equal(PreparedToolPurpose.JdtNavigation, lease.ToolPurpose);

            await context.DisposeAsync();
            contextDisposed = true;

            Assert.Equal(1, runtime.Session.DisposeCallCount);
            Assert.Equal(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);
        }
        finally
        {
            if (!contextDisposed && open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenDedicatedAsync_WithCurrentJdtLease_ExposesBothFunctionsAndKeepsCallerLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example");
        Directory.CreateDirectory(javaDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(javaDirectory, "Caller.java"),
            "package example; class Caller { void call() {} }\n");
        string targetPath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(targetPath, "package example; class Target { void target() {} }\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "dedicated-jdt-state");
        Directory.CreateDirectory(stateRoot);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledPreparedJdtLsRuntime();
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            [new PreparedJdtLsRawLocation(new Uri(targetPath).AbsoluteUri, 7, 2, 7, 13)],
            LimitReached: false,
            CapabilityLimitation: null);
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? opened = null;
        IPreparedJavaNavigationContext? context = null;
        bool contextDisposed = false;

        try
        {
            opened = await factory.OpenDedicatedAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);
            Assert.Equal(1, runtime.OpenCallCount);
            Assert.Same(lease, runtime.LastWorkspaceLease);
            Assert.Equal(profile, runtime.LastProfile);
            Assert.True(runtime.LastCancellationToken.CanBeCanceled);

            string? grantedRoot = null;
            bool stateRootIsSeparate = false;
            PreparedToolWorkspaceCheckStatus leaseStatus = await lease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    grantedRoot = scope.GetPhysicalRootPathForTrustedToolBinding();
                    stateRootIsSeparate = scope.IsSeparateDisposableStateRoot(profile.DisposableStateRoot);
                    return ValueTask.CompletedTask;
                });
            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, leaseStatus);
            Assert.Equal(Path.GetFullPath(fixture.PreparedRootPath), grantedRoot);
            Assert.True(stateRootIsSeparate);

            AIFunction[] functions = new PreparedJavaNavigationFunctions(context).Create().ToArray();
            Assert.Equal(
                new[] { "navigate_java_definition", "navigate_java_references" },
                functions.Select(function => function.Name));
            var arguments = new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["relativePath"] = "src/main/java/example/Caller.java",
                ["line"] = 3,
                ["character"] = 4,
                ["maximumLocations"] = 5,
            });

            object? rawDefinition = await functions[0].InvokeAsync(arguments, CancellationToken.None);
            PreparedJavaNavigationResult definition = Assert.IsType<PreparedJavaNavigationResult>(rawDefinition);
            Assert.Equal(fixture.Approval.RepositoryId, definition.RepositoryId);
            Assert.Equal(PreparedJavaNavigationOperation.Definition, definition.Operation);
            Assert.Equal(PreparedJavaNavigationOutcome.Observed, definition.Outcome);
            Assert.Equal(
                new PreparedJavaLocation(
                    new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/example/Target.java"),
                    7,
                    2,
                    7,
                    13),
                Assert.Single(definition.Locations));
            Assert.Equal(PreparedJavaNavigationOperation.Definition, runtime.Session.LastOperation);
            Assert.Equal(
                new PreparedJavaNavigationRequest("src/main/java/example/Caller.java", 3, 4, 5),
                runtime.Session.LastRequest);

            object? rawReferences = await functions[1].InvokeAsync(arguments, CancellationToken.None);
            PreparedJavaNavigationResult references = Assert.IsType<PreparedJavaNavigationResult>(rawReferences);
            Assert.Equal(fixture.Approval.RepositoryId, references.RepositoryId);
            Assert.Equal(PreparedJavaNavigationOperation.References, references.Operation);
            Assert.Equal(PreparedJavaNavigationOutcome.Observed, references.Outcome);
            Assert.Equal(
                new PreparedJavaLocation(
                    new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/example/Target.java"),
                    7,
                    2,
                    7,
                    13),
                Assert.Single(references.Locations));
            Assert.Equal(2, runtime.Session.NavigateCallCount);
            Assert.Equal(PreparedJavaNavigationOperation.References, runtime.Session.LastOperation);
            Assert.Equal(
                new PreparedJavaNavigationRequest("src/main/java/example/Caller.java", 3, 4, 5),
                runtime.Session.LastRequest);

            PreparedJavaNavigationOpenResult competingOpen = await factory.OpenDedicatedAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.DeadlineExceeded, competingOpen.Status);
            Assert.Null(competingOpen.Context);
            Assert.Equal(1, runtime.OpenCallCount);

            await context.DisposeAsync();
            contextDisposed = true;
            context = null;
            Assert.Equal(1, runtime.Session.DisposeCallCount);
            Assert.Equal(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);

            PreparedJavaNavigationOpenResult guardedOpen = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable, guardedOpen.Status);
            Assert.Null(guardedOpen.Context);
            Assert.Equal(1, runtime.OpenCallCount);
        }
        finally
        {
            if (!contextDisposed && context is not null)
            {
                await context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [InlineData("wrong-purpose")]
    [InlineData("invalid-profile")]
    public async Task OpenDedicatedAsync_WhenPurposeOrProfileIsInvalid_ReturnsFailureWithoutRuntime(
        string invalidInput)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedToolPurpose purpose = invalidInput == "wrong-purpose"
            ? PreparedToolPurpose.CodeGraphPreparation
            : PreparedToolPurpose.JdtNavigation;
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(fixture.Approval, purpose);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    purpose,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "dedicated-jdt-invalid-state");
        Directory.CreateDirectory(stateRoot);
        var profile = new PreparedJavaNavigationProfile(
            invalidInput == "invalid-profile" ? "relative-state-root" : stateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledPreparedJdtLsRuntime();
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);

        try
        {
            PreparedJavaNavigationOpenResult result = await factory.OpenDedicatedAsync(lease);

            Assert.Equal(
                invalidInput == "wrong-purpose"
                    ? PreparedJavaNavigationOpenStatus.Denied
                    : PreparedJavaNavigationOpenStatus.ConfigurationUnavailable,
                result.Status);
            Assert.Null(result.Context);
            Assert.Equal(0, runtime.OpenCallCount);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenDedicatedAsync_WhenFunctionReplyContainsHostileUris_WithholdsEveryLocation()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example");
        Directory.CreateDirectory(javaDirectory);
        string callerPath = Path.Combine(javaDirectory, "Caller.java");
        await File.WriteAllTextAsync(callerPath, "package example; class Caller { void call() {} }\n");
        string targetPath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(targetPath, "package example; class Target { void target() {} }\n");
        string symlinkPath = Path.Combine(javaDirectory, "EscapesToSentinel.java");
        File.CreateSymbolicLink(symlinkPath, fixture.SentinelPath);

        string rootUri = new Uri(
            Path.GetFullPath(fixture.PreparedRootPath).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar).AbsoluteUri;
        string driveRoot = Path.GetPathRoot(targetPath)!;
        string relativeToDrive = Path.GetRelativePath(driveRoot, targetPath).Replace('\\', '/');
        string rawDevicePath = $"\\\\?\\{targetPath}";
        string rawDeviceNamespace = $"\\\\.\\{targetPath}";
        string[] untrustedUris =
        [
            $"file:///?/{driveRoot.Replace('\\', '/')}{relativeToDrive}",
            "file://server/share/Secret.java",
            rootUri + "%2e%2e/outside/sentinel.txt",
            rootUri + "src/main/java%2FTarget.java",
            new Uri(fixture.SentinelPath).AbsoluteUri,
            new Uri(symlinkPath).AbsoluteUri,
            rawDevicePath,
            rawDeviceNamespace,
        ];
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "dedicated-jdt-hostile-state");
        Directory.CreateDirectory(stateRoot);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledPreparedJdtLsRuntime();
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            untrustedUris.Select(uri => new PreparedJdtLsRawLocation(uri, 1, 1, 1, 3)).ToArray(),
            LimitReached: false,
            CapabilityLimitation: null);
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? opened = null;

        try
        {
            opened = await factory.OpenDedicatedAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);
            AIFunction definition = Assert.Single(
                new PreparedJavaNavigationFunctions(context).Create().Where(function =>
                    function.Name == "navigate_java_definition"));
            object? rawResult = await definition.InvokeAsync(
                new AIFunctionArguments(new Dictionary<string, object?>
                {
                    ["relativePath"] = "src/main/java/example/Caller.java",
                    ["line"] = 1,
                    ["character"] = 1,
                    ["maximumLocations"] = 100,
                }),
                CancellationToken.None);
            PreparedJavaNavigationResult result = Assert.IsType<PreparedJavaNavigationResult>(rawResult);

            Assert.Equal(PreparedJavaNavigationOutcome.NoResultObserved, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Equal(untrustedUris.Length, result.Limitations.Count);
            Assert.All(result.Limitations, limitation =>
            {
                Assert.Equal("UnmappableLocation", limitation.Code);
                Assert.Null(limitation.Source);
            });
            string serializedResult = JsonSerializer.Serialize(result);
            foreach (string uri in untrustedUris)
            {
                Assert.DoesNotContain(uri, serializedResult, StringComparison.OrdinalIgnoreCase);
            }

            Assert.DoesNotContain(fixture.PreparedRootPath, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(fixture.SentinelPath, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, runtime.Session.NavigateCallCount);
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
    [SuiteTheory("LocalTools")]
    [InlineData(true, PreparedSourceAuthorityStatus.Revoked, PreparedJavaNavigationOutcome.Denied)]
    [InlineData(true, PreparedSourceAuthorityStatus.Unavailable, PreparedJavaNavigationOutcome.AuthorityUnavailable)]
    [InlineData(false, PreparedSourceAuthorityStatus.Revoked, PreparedJavaNavigationOutcome.Denied)]
    [InlineData(false, PreparedSourceAuthorityStatus.Unavailable, PreparedJavaNavigationOutcome.AuthorityUnavailable)]
    public async Task OpenDedicatedAsync_WhenSourceAuthorityIsLostBeforeRequestOrAfterReply_DiscardsPayload(
        bool loseBeforeRequest,
        PreparedSourceAuthorityStatus loss,
        PreparedJavaNavigationOutcome expectedOutcome)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example");
        Directory.CreateDirectory(javaDirectory);
        string callerPath = Path.Combine(javaDirectory, "Caller.java");
        await File.WriteAllTextAsync(callerPath, "package example; class Caller { void call() {} }\n");
        string targetPath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(targetPath, "package example; class Target { void target() {} }\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "dedicated-jdt-authority-loss-state");
        Directory.CreateDirectory(stateRoot);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledPreparedJdtLsRuntime();
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            [new PreparedJdtLsRawLocation(new Uri(targetPath).AbsoluteUri, 4, 1, 4, 8)],
            LimitReached: true,
            CapabilityLimitation: "private capability detail");
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? opened = null;

        try
        {
            opened = await factory.OpenDedicatedAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);
            if (loseBeforeRequest)
            {
                SetSourceAuthorityLoss(fixture, loss);
            }
            else
            {
                runtime.Session.AfterNavigate = () => SetSourceAuthorityLoss(fixture, loss);
            }

            AIFunction definition = Assert.Single(
                new PreparedJavaNavigationFunctions(context).Create().Where(function =>
                    function.Name == "navigate_java_definition"));
            object? rawResult = await definition.InvokeAsync(
                new AIFunctionArguments(new Dictionary<string, object?>
                {
                    ["relativePath"] = "src/main/java/example/Caller.java",
                    ["line"] = 1,
                    ["character"] = 1,
                    ["maximumLocations"] = 5,
                }),
                CancellationToken.None);
            PreparedJavaNavigationResult result = Assert.IsType<PreparedJavaNavigationResult>(rawResult);

            Assert.Equal(expectedOutcome, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(loseBeforeRequest ? 0 : 1, runtime.Session.NavigateCallCount);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
            string serializedResult = JsonSerializer.Serialize(result);
            Assert.DoesNotContain(callerPath, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(targetPath, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(new Uri(targetPath).AbsoluteUri, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(stateRoot, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("private capability detail", serializedResult, StringComparison.Ordinal);
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

    private static void SetSourceAuthorityLoss(
        PreparedSourceFixture fixture,
        PreparedSourceAuthorityStatus loss)
    {
        if (loss == PreparedSourceAuthorityStatus.Revoked)
        {
            fixture.Authority.Revoke();
        }
        else
        {
            fixture.Authority.MakeUnavailable();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_WithCodeGraphPurposeLease_DeniesWithoutInvokingRuntime()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.CodeGraphPreparation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.CodeGraphPreparation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledPreparedJdtLsRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);

        try
        {
            PreparedJavaNavigationOpenResult open = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Denied, open.Status);
            Assert.Null(open.Context);
            Assert.Equal(0, runtime.OpenCallCount);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_WithDisposedJdtLease_DeniesWithoutInvokingRuntime()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledPreparedJdtLsRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);

        try
        {
            await lease.DisposeAsync();
            PreparedJavaNavigationOpenResult open = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Denied, open.Status);
            Assert.Null(open.Context);
            Assert.Equal(0, runtime.OpenCallCount);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_WithInvalidTrustedProfile_ReturnsConfigurationUnavailableWithoutRuntime()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        var invalidProfile = new PreparedJavaNavigationProfile(
            "relative-jdt-state",
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledPreparedJdtLsRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, invalidProfile, runtime);

        try
        {
            PreparedJavaNavigationOpenResult open = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable, open.Status);
            Assert.Null(open.Context);
            Assert.Equal(0, runtime.OpenCallCount);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_WithStateRootInsidePreparedRoot_RejectsBeforeRuntime()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string overlappingStateRoot = Path.Combine(fixture.PreparedRootPath, "jdt-state");
        Directory.CreateDirectory(overlappingStateRoot);
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        var profile = new PreparedJavaNavigationProfile(
            overlappingStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledPreparedJdtLsRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;
        try
        {
            open = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable, open.Status);
            Assert.Null(open.Context);
            Assert.Equal(0, runtime.OpenCallCount);
        }
        finally
        {
            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_WhenAuthorityIsRevokedAfterSessionReturn_DisposesSessionAndReleasesSlot()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult firstIssue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, firstIssue.Status);
        PreparedToolWorkspaceLease firstLease = Assert.IsType<PreparedToolWorkspaceLease>(firstIssue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new RevokingSourceOnceRuntime(fixture.Authority);
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? firstOpen = null;
        PreparedToolWorkspaceLease? retryLease = null;
        PreparedJavaNavigationOpenResult? retryOpen = null;

        try
        {
            firstOpen = await factory.OpenAsync(firstLease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Denied, firstOpen.Status);
            Assert.Null(firstOpen.Context);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, firstLease.Lifetime);
            Assert.Equal(1, runtime.OpenCallCount);
            Assert.Equal(1, Assert.Single(runtime.Sessions).DisposeCallCount);

            fixture.Authority.Approve(fixture.Approval);
            PreparedToolWorkspaceIssueResult retryIssue = await issuer.IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, retryIssue.Status);
            retryLease = Assert.IsType<PreparedToolWorkspaceLease>(retryIssue.Lease);

            retryOpen = await factory.OpenAsync(retryLease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, retryOpen.Status);
            Assert.NotNull(retryOpen.Context);
            Assert.Equal(2, runtime.OpenCallCount);
        }
        finally
        {
            if (retryOpen?.Context is not null)
            {
                await retryOpen.Context.DisposeAsync();
            }

            if (retryLease is not null)
            {
                await retryLease.DisposeAsync();
            }

            if (firstOpen?.Context is not null)
            {
                await firstOpen.Context.DisposeAsync();
            }

            await firstLease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task GetDefinitionAsync_WhenPathIsInsideVerifiedOwnedState_DeniesBeforeProtocol()
    {
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
        PreparedCodeGraphOwnershipViewResult ownershipView =
            await ownershipFixture.GetCurrentOwnershipViewAsync(fixture.Approval);
        Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, ownershipView.Status);
        Assert.Equal(ownershipFixture.ApplicationHeldBinding, ownershipView.Record);
        Assert.NotNull(ownershipView.Record?.StateEntryObjectIdentity);

        var authorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, authorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var runtime = new ControlledPreparedJdtLsRuntime();
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

            PreparedJavaNavigationResult result = await context.GetDefinitionAsync(
                new PreparedJavaNavigationRequest(".codegraph/Owned.java", 1, 1, 10));

            Assert.Equal(0, runtime.Session.NavigateCallCount);
            Assert.Equal(PreparedJavaNavigationOutcome.Denied, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
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
    public async Task GetDefinitionAsync_WhenSameNamePathIsVerifiedUnowned_AllowsProtocolAndMapping()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        string sourcePath = Path.Combine(codeGraphPath, "Ordinary.java");
        await File.WriteAllTextAsync(sourcePath, "class Ordinary {}\n");

        PreparedJavaNavigationOwnershipFixture ownershipFixture =
            PreparedJavaNavigationOwnershipFixture.Create(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedCodeGraphOwnershipViewResult ownershipView =
            await ownershipFixture.GetCurrentOwnershipViewAsync(fixture.Approval);
        Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedUnowned, ownershipView.Status);
        Assert.Null(ownershipView.Record);
        Assert.True(File.Exists(sourcePath), "The same-name entry must be present as ordinary Repository source.");

        var authorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, authorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var runtime = new ControlledPreparedJdtLsRuntime();
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            [new PreparedJdtLsRawLocation(new Uri(sourcePath).AbsoluteUri, 1, 1, 1, 7)],
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

            var request = new PreparedJavaNavigationRequest(".codegraph/Ordinary.java", 1, 1, 10);
            PreparedJavaNavigationResult result = await context.GetDefinitionAsync(request);

            Assert.Equal(1, runtime.Session.NavigateCallCount);
            Assert.Equal(request, runtime.Session.LastRequest);
            Assert.Equal(PreparedJavaNavigationOutcome.Observed, result.Outcome);
            PreparedJavaLocation location = Assert.Single(result.Locations);
            Assert.Equal(".codegraph/Ordinary.java", location.Source.RelativePath);
            Assert.Equal(1, location.StartLine);
            Assert.Equal(1, location.StartCharacter);
            Assert.Equal(1, location.EndLine);
            Assert.Equal(7, location.EndCharacter);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
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
    public async Task GetDefinitionAsync_WithJdtOnlyRegistryGrant_ClassifiesSameNameAbsentStateAsOrdinarySource()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        string sourcePath = Path.Combine(codeGraphPath, "Ordinary.java");
        await File.WriteAllTextAsync(sourcePath, "class Ordinary { void target() {} }\n");

        PreparedJavaNavigationOwnershipFixture bindingFixture =
            PreparedJavaNavigationOwnershipFixture.Create(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedCodeGraphWorkspaceOwnership binding = bindingFixture.ApplicationHeldBinding;

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
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
        RetainedSourceCapturePolicy sourceStoragePolicy = RetainedSourceCapturePolicy.Create(
            configuration,
            fixture.SandboxPath,
            fixture.PreparedRootPath);
        Assert.Equal(
            PreparedCodeGraphStoreProvisionStatus.Provisioned,
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(sourceStoragePolicy));

        var ownershipStore = new FilePreparedCodeGraphOwnershipStore(sourceStoragePolicy);
        PreparedCodeGraphOwnershipLoadResult initialRecord = await ownershipStore.LoadAsync(binding);
        Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent, initialRecord.Status);
        Assert.Null(initialRecord.Record);
        Assert.True(File.Exists(sourcePath),
            "The same-name .codegraph entry must already exist as ordinary Repository source.");

        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var hostProtection = new JdtOnlyRegistryHostVerifier(binding);
        var registry = new PreparedWorkspaceUseRegistry(
            fixture.Authority,
            toolAuthorization,
            ownershipStore,
            sourceStoragePolicy,
            hostProtection,
            new JdtOnlyPreviousGenerationVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(2),
            dispositionDeadline: TimeSpan.FromSeconds(2));
        PreparedToolWorkspaceLease? lease = null;
        PreparedJavaNavigationOpenResult? opened = null;

        try
        {
            PreparedWorkspaceAdmissionResult admission = await registry.AdmitAsync(
                binding,
                PreparedToolPurpose.JdtNavigation);
            Assert.Equal(PreparedWorkspaceAdmissionStatus.Opened, admission.Status);

            PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
                fixture.Authority,
                toolAuthorization,
                binding,
                registry).IssueAsync(
                    new PreparedToolWorkspaceGrantRequest(
                        fixture.Approval,
                        fixture.PreparedRootPath,
                        PreparedToolPurpose.JdtNavigation,
                        TimeSpan.FromSeconds(10)));
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
            lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

            string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
            Directory.CreateDirectory(disposableStateRoot);
            var runtime = new ControlledPreparedJdtLsRuntime();
            runtime.Session.Reply = new PreparedJdtLsRawReply(
                [new PreparedJdtLsRawLocation(new Uri(sourcePath).AbsoluteUri, 1, 1, 1, 7)],
                LimitReached: false,
                CapabilityLimitation: null);
            var coordinator = new PreparedCodeGraphStateCoordinator(
                fixture.Authority,
                toolAuthorization,
                registry,
                ownershipStore);
            var factory = new PreparedJavaNavigationContextFactory(
                new PreparedJavaNavigationProfile(
                    disposableStateRoot,
                    TimeSpan.FromSeconds(3),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(1),
                    MaximumResponseBytes: 64 * 1024),
                runtime,
                coordinator,
                _ => ValueTask.FromResult<PreparedCodeGraphWorkspaceOwnership?>(binding));

            opened = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);

            var request = new PreparedJavaNavigationRequest(".codegraph/Ordinary.java", 1, 1, 10);
            PreparedJavaNavigationResult result = await context.GetDefinitionAsync(request);

            Assert.Equal(1, runtime.Session.NavigateCallCount);
            Assert.Equal(request, runtime.Session.LastRequest);
            Assert.Equal(PreparedJavaNavigationOutcome.Observed, result.Outcome);
            PreparedJavaLocation location = Assert.Single(result.Locations);
            Assert.Equal(".codegraph/Ordinary.java", location.Source.RelativePath);
            Assert.Equal(1, location.StartLine);
            Assert.Equal(1, location.StartCharacter);
            Assert.Equal(1, location.EndLine);
            Assert.Equal(7, location.EndCharacter);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);

            PreparedToolWorkspaceIssueResult codeGraphIssue = await new PreparedToolWorkspaceIssuer(
                fixture.Authority,
                toolAuthorization,
                binding,
                registry).IssueAsync(
                    new PreparedToolWorkspaceGrantRequest(
                        fixture.Approval,
                        fixture.PreparedRootPath,
                        PreparedToolPurpose.CodeGraphPreparation,
                        TimeSpan.FromSeconds(10)));
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Denied, codeGraphIssue.Status);
            Assert.Null(codeGraphIssue.Lease);
        }
        finally
        {
            if (opened?.Context is not null)
            {
                await opened.Context.DisposeAsync();
            }

            if (lease is not null)
            {
                await lease.DisposeAsync();
            }

            await registry.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CloseForDispositionAsync_WhenCallerLeaseIsDisposedButJdtSessionRemainsOpen_StaysActiveUntilContextDisposal()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        PreparedJavaNavigationOwnershipFixture bindingFixture =
            PreparedJavaNavigationOwnershipFixture.Create(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedCodeGraphWorkspaceOwnership binding = bindingFixture.ApplicationHeldBinding;

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
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
        RetainedSourceCapturePolicy sourceStoragePolicy = RetainedSourceCapturePolicy.Create(
            configuration,
            fixture.SandboxPath,
            fixture.PreparedRootPath);
        Assert.Equal(
            PreparedCodeGraphStoreProvisionStatus.Provisioned,
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(sourceStoragePolicy));

        var ownershipStore = new FilePreparedCodeGraphOwnershipStore(sourceStoragePolicy);
        PreparedCodeGraphOwnershipLoadResult initialRecord = await ownershipStore.LoadAsync(binding);
        Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent, initialRecord.Status);
        Assert.Null(initialRecord.Record);

        var toolAuthorization = new JdtAndCodeGraphToolUseAuthorizer(fixture.Approval);
        var registry = new PreparedWorkspaceUseRegistry(
            fixture.Authority,
            toolAuthorization,
            ownershipStore,
            sourceStoragePolicy,
            new JdtOnlyRegistryHostVerifier(binding),
            new JdtOnlyPreviousGenerationVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(2),
            dispositionDeadline: TimeSpan.FromSeconds(2));
        PreparedToolWorkspaceLease? lease = null;
        IPreparedJavaNavigationContext? context = null;
        try
        {
            PreparedWorkspaceAdmissionResult admission = await registry.AdmitAsync(
                binding,
                PreparedToolPurpose.JdtNavigation);
            Assert.Equal(PreparedWorkspaceAdmissionStatus.Opened, admission.Status);

            var issuer = new PreparedToolWorkspaceIssuer(
                fixture.Authority,
                toolAuthorization,
                binding,
                registry);
            PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
            lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

            var coordinator = new PreparedCodeGraphStateCoordinator(
                fixture.Authority,
                toolAuthorization,
                registry,
                ownershipStore);
            var runtime = new ControlledPreparedJdtLsRuntime();
            string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
            Directory.CreateDirectory(disposableStateRoot);
            var factory = new PreparedJavaNavigationContextFactory(
                new PreparedJavaNavigationProfile(
                    disposableStateRoot,
                    TimeSpan.FromSeconds(3),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(1),
                    MaximumResponseBytes: 64 * 1024),
                runtime,
                coordinator,
                _ => ValueTask.FromResult<PreparedCodeGraphWorkspaceOwnership?>(binding),
                registry);

            PreparedJavaNavigationOpenResult opened = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);
            Assert.Equal(1, runtime.OpenCallCount);
            Assert.Equal(0, runtime.Session.DisposeCallCount);

            await lease.DisposeAsync();
            lease = null;
            Assert.Equal(0, runtime.Session.DisposeCallCount);

            PreparedWorkspaceSettlementStatus whileSessionIsLive =
                await registry.CloseForDispositionAsync(binding);
            Assert.Equal(PreparedWorkspaceSettlementStatus.Active, whileSessionIsLive);

            PreparedToolWorkspaceIssueResult lateIssue = await issuer.IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
            Assert.NotEqual(PreparedToolWorkspaceIssueStatus.Issued, lateIssue.Status);
            Assert.Null(lateIssue.Lease);

            await context.DisposeAsync();
            context = null;
            Assert.Equal(1, runtime.Session.DisposeCallCount);
            Assert.Equal(
                PreparedWorkspaceSettlementStatus.Settled,
                await registry.CloseForDispositionAsync(binding));
        }
        finally
        {
            if (context is not null)
            {
                await context.DisposeAsync();
            }

            if (lease is not null)
            {
                await lease.DisposeAsync();
            }

            await registry.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CloseForDispositionAsync_WhenTrackedJdtSessionDisposalTimesOut_RemainsUnknownAfterLateCompletion()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        PreparedJavaNavigationOwnershipFixture bindingFixture =
            PreparedJavaNavigationOwnershipFixture.Create(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedCodeGraphWorkspaceOwnership binding = bindingFixture.ApplicationHeldBinding;

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
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
        RetainedSourceCapturePolicy sourceStoragePolicy = RetainedSourceCapturePolicy.Create(
            configuration,
            fixture.SandboxPath,
            fixture.PreparedRootPath);
        Assert.Equal(
            PreparedCodeGraphStoreProvisionStatus.Provisioned,
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(sourceStoragePolicy));

        var ownershipStore = new FilePreparedCodeGraphOwnershipStore(sourceStoragePolicy);
        PreparedCodeGraphOwnershipLoadResult initialRecord = await ownershipStore.LoadAsync(binding);
        Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent, initialRecord.Status);
        Assert.Null(initialRecord.Record);

        var toolAuthorization = new JdtAndCodeGraphToolUseAuthorizer(fixture.Approval);
        var registry = new PreparedWorkspaceUseRegistry(
            fixture.Authority,
            toolAuthorization,
            ownershipStore,
            sourceStoragePolicy,
            new JdtOnlyRegistryHostVerifier(binding),
            new JdtOnlyPreviousGenerationVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(2),
            dispositionDeadline: TimeSpan.FromSeconds(2));
        PreparedToolWorkspaceLease? lease = null;
        IPreparedJavaNavigationContext? context = null;
        Task? pendingContextDisposal = null;
        var runtime = new StalledDisposePreparedJdtLsRuntime();
        try
        {
            PreparedWorkspaceAdmissionResult admission = await registry.AdmitAsync(
                binding,
                PreparedToolPurpose.JdtNavigation);
            Assert.Equal(PreparedWorkspaceAdmissionStatus.Opened, admission.Status);

            var issuer = new PreparedToolWorkspaceIssuer(
                fixture.Authority,
                toolAuthorization,
                binding,
                registry);
            PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
            lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

            var coordinator = new PreparedCodeGraphStateCoordinator(
                fixture.Authority,
                toolAuthorization,
                registry,
                ownershipStore);
            string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
            Directory.CreateDirectory(disposableStateRoot);
            var factory = new PreparedJavaNavigationContextFactory(
                new PreparedJavaNavigationProfile(
                    disposableStateRoot,
                    TimeSpan.FromSeconds(3),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromMilliseconds(100),
                    MaximumResponseBytes: 64 * 1024),
                runtime,
                coordinator,
                _ => ValueTask.FromResult<PreparedCodeGraphWorkspaceOwnership?>(binding),
                registry);

            PreparedJavaNavigationOpenResult opened = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);
            await lease.DisposeAsync();
            lease = null;

            pendingContextDisposal = context.DisposeAsync().AsTask();
            await runtime.Session.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await pendingContextDisposal.WaitAsync(TimeSpan.FromSeconds(2));

            PreparedWorkspaceSettlementStatus whileDisposeIsPending =
                await registry.CloseForDispositionAsync(binding);
            Assert.Equal(PreparedWorkspaceSettlementStatus.Unknown, whileDisposeIsPending);

            PreparedToolWorkspaceIssueResult issueAfterUnconfirmedCleanup = await issuer.IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
            Assert.NotEqual(PreparedToolWorkspaceIssueStatus.Issued, issueAfterUnconfirmedCleanup.Status);
            Assert.Null(issueAfterUnconfirmedCleanup.Lease);

            runtime.Session.ReleaseDispose();
            await runtime.Session.DisposeCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await context.DisposeAsync();
            Assert.Equal(
                PreparedWorkspaceSettlementStatus.Unknown,
                await registry.CloseForDispositionAsync(binding));
        }
        finally
        {
            runtime.Session.ReleaseDispose();
            if (runtime.Session.DisposeStarted.Task.IsCompleted)
            {
                await runtime.Session.DisposeCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            }

            if (pendingContextDisposal is not null)
            {
                await pendingContextDisposal.WaitAsync(TimeSpan.FromSeconds(2));
            }

            if (context is not null)
            {
                await context.DisposeAsync();
            }

            if (lease is not null)
            {
                await lease.DisposeAsync();
            }

            await registry.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task GetDefinitionAsync_WhenAbsentWorkspaceBecomesReadyDuringBufferedReply_DiscardsEntireReply()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string callerPath = Path.Combine(fixture.PreparedRootPath, "Caller.java");
        await File.WriteAllTextAsync(callerPath, "class Caller { void target() {} }\n");
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Assert.False(Directory.Exists(codeGraphPath),
            "The positively absent workspace must not contain a CodeGraph entry before admission.");

        byte[] sourceBefore = await File.ReadAllBytesAsync(fixture.UnfamiliarTextPath);
        byte[] siblingBefore = await File.ReadAllBytesAsync(fixture.SentinelPath);
        PreparedJavaNavigationOwnershipFixture bindingFixture =
            PreparedJavaNavigationOwnershipFixture.Create(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedCodeGraphWorkspaceOwnership absent = bindingFixture.ApplicationHeldBinding;

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
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
        RetainedSourceCapturePolicy sourceStoragePolicy = RetainedSourceCapturePolicy.Create(
            configuration,
            fixture.SandboxPath,
            fixture.PreparedRootPath);
        Assert.Equal(
            PreparedCodeGraphStoreProvisionStatus.Provisioned,
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(sourceStoragePolicy));

        var ownershipStore = new FilePreparedCodeGraphOwnershipStore(sourceStoragePolicy);
        PreparedCodeGraphOwnershipLoadResult initialLoad = await ownershipStore.LoadAsync(absent);
        Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent, initialLoad.Status);
        Assert.Null(initialLoad.Record);

        var toolAuthorization = new JdtAndCodeGraphToolUseAuthorizer(fixture.Approval);
        var hostProtection = new ProgressiveRegistryHostVerifier(absent);
        var registry = new PreparedWorkspaceUseRegistry(
            fixture.Authority,
            toolAuthorization,
            ownershipStore,
            sourceStoragePolicy,
            hostProtection,
            new JdtOnlyPreviousGenerationVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(2),
            dispositionDeadline: TimeSpan.FromSeconds(2));
        PreparedToolWorkspaceLease? navigationLease = null;
        PreparedToolWorkspaceLease? codeGraphLease = null;
        PreparedJavaNavigationOpenResult? opened = null;
        try
        {
            PreparedWorkspaceAdmissionResult admission = await registry.AdmitAsync(
                absent,
                PreparedToolPurpose.JdtNavigation);
            Assert.Equal(PreparedWorkspaceAdmissionStatus.Opened, admission.Status);

            var issuer = new PreparedToolWorkspaceIssuer(
                fixture.Authority,
                toolAuthorization,
                absent,
                registry);
            PreparedToolWorkspaceIssueResult navigationIssue = await issuer.IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
            navigationLease = navigationIssue.Lease;
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, navigationIssue.Status);
            PreparedToolWorkspaceLease currentNavigationLease =
                Assert.IsType<PreparedToolWorkspaceLease>(navigationLease);

            PreparedToolWorkspaceIssueResult codeGraphIssue = await issuer.IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.CodeGraphPreparation,
                    TimeSpan.FromSeconds(10)));
            codeGraphLease = codeGraphIssue.Lease;
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, codeGraphIssue.Status);
            PreparedToolWorkspaceLease currentCodeGraphLease =
                Assert.IsType<PreparedToolWorkspaceLease>(codeGraphLease);

            var coordinator = new PreparedCodeGraphStateCoordinator(
                fixture.Authority,
                toolAuthorization,
                registry,
                ownershipStore);
            var ownedReply = new PreparedJdtLsRawReply(
                [
                    new PreparedJdtLsRawLocation(new Uri(callerPath).AbsoluteUri, 1, 1, 1, 7),
                    new PreparedJdtLsRawLocation(
                        new Uri(Path.Combine(codeGraphPath, "Generated.java")).AbsoluteUri,
                        1,
                        1,
                        1,
                        7),
                ],
                LimitReached: true,
                CapabilityLimitation: "controlled capability detail");
            PreparedToolWorkspaceCheckStatus? stateCreationStatus = null;
            string? generatedPath = null;
            var runtime = new OwnedStateTransitionPreparedJdtLsRuntime(
                async token =>
                {
                    stateCreationStatus = await coordinator.UseOwnedStateAsync(
                        currentCodeGraphLease,
                        absent,
                        async (scope, operationToken) =>
                        {
                            generatedPath = Path.Combine(
                                scope.GetPhysicalStatePathForTrustedToolBinding(),
                                "Generated.java");
                            await File.WriteAllTextAsync(
                                generatedPath,
                                "class Generated { void target() {} }\n",
                                operationToken);
                        },
                        token);
                },
                ownedReply);
            string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
            Directory.CreateDirectory(disposableStateRoot);
            var factory = new PreparedJavaNavigationContextFactory(
                new PreparedJavaNavigationProfile(
                    disposableStateRoot,
                    TimeSpan.FromSeconds(3),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(1),
                    MaximumResponseBytes: 64 * 1024),
                runtime,
                coordinator,
                _ => ValueTask.FromResult<PreparedCodeGraphWorkspaceOwnership?>(absent));

            opened = await factory.OpenAsync(currentNavigationLease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);

            PreparedJavaNavigationResult result = await context.GetDefinitionAsync(
                new PreparedJavaNavigationRequest("Caller.java", 1, 1, 10));

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, stateCreationStatus);
            Assert.NotNull(generatedPath);
            Assert.True(File.Exists(generatedPath));
            PreparedCodeGraphOwnershipLoadResult currentLoad = await ownershipStore.LoadAsync(absent);
            Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.Found, currentLoad.Status);
            PreparedCodeGraphWorkspaceOwnership ready =
                Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(currentLoad.Record);
            Assert.Equal(PreparedCodeGraphOwnershipPhase.Ready, ready.Phase);
            Assert.Equal(2, ready.Version);
            Assert.Equal(absent.OwnershipId, ready.OwnershipId);
            Assert.Equal(absent.Approval, ready.Approval);
            Assert.Equal(absent.PhysicalRootPath, ready.PhysicalRootPath);
            Assert.Equal(absent.RootObjectIdentity, ready.RootObjectIdentity);
            Assert.Equal(absent.WorkspaceOwnerIdentity, ready.WorkspaceOwnerIdentity);
            Assert.NotNull(ready.StateEntryObjectIdentity);
            var physicalEntry = PreparedSourceTraversal.OpenRoot(codeGraphPath);
            Assert.Equal(PreparedSourceTraversal.OpenStatus.Opened, physicalEntry.Status);
            using (physicalEntry.Handle)
            {
                PreparedSourceTraversal.DirectoryIdentity observed = Assert.IsType<PreparedSourceTraversal.DirectoryIdentity>(
                    physicalEntry.Handle is null
                        ? null
                        : PreparedSourceTraversal.ObserveDirectory(physicalEntry.Handle));
                Assert.Equal(
                    new PreparedToolDirectoryObjectIdentity(
                        observed.VolumeSerialNumber,
                        observed.FileIndexHigh,
                        observed.FileIndexLow),
                    ready.StateEntryObjectIdentity);
            }

            PreparedCodeGraphOwnershipViewResult finalView = await coordinator.GetOwnershipViewAsync(
                absent,
                fixture.Approval);
            Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, finalView.Status);
            Assert.Equal(ready, finalView.Record);
            Assert.Equal(1, runtime.Session.NavigateCallCount);
            Assert.Equal(1, runtime.Session.DisposeCallCount);
            Assert.Equal(PreparedJavaNavigationOutcome.ConfigurationUnavailable, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(sourceBefore, await File.ReadAllBytesAsync(fixture.UnfamiliarTextPath));
            Assert.Equal(siblingBefore, await File.ReadAllBytesAsync(fixture.SentinelPath));
        }
        finally
        {
            if (opened?.Context is not null)
            {
                await opened.Context.DisposeAsync();
            }

            if (codeGraphLease is not null)
            {
                await codeGraphLease.DisposeAsync();
            }

            if (navigationLease is not null)
            {
                await navigationLease.DisposeAsync();
            }

            await registry.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task GetDefinitionAsync_WhenReplyIncludesVerifiedOwnedState_FiltersOnlyOwnedLocation()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        string ownedJavaPath = Path.Combine(codeGraphPath, "Owned.java");
        string sourceJavaPath = Path.Combine(fixture.PreparedRootPath, "Source.java");
        string callerJavaPath = Path.Combine(fixture.PreparedRootPath, "Caller.java");
        await File.WriteAllTextAsync(ownedJavaPath, "class Owned { void target() {} }\n");
        await File.WriteAllTextAsync(sourceJavaPath, "class Source { void target() {} }\n");
        await File.WriteAllTextAsync(callerJavaPath, "class Caller { void call() {} }\n");

        PreparedJavaNavigationOwnershipFixture ownershipFixture =
            PreparedJavaNavigationOwnershipFixture.CreateVerifiedOwned(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedCodeGraphOwnershipViewResult ownershipView =
            await ownershipFixture.GetCurrentOwnershipViewAsync(fixture.Approval);
        Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, ownershipView.Status);
        Assert.Equal(ownershipFixture.ApplicationHeldBinding, ownershipView.Record);
        Assert.NotNull(ownershipView.Record?.StateEntryObjectIdentity);

        var authorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, authorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var runtime = new ControlledPreparedJdtLsRuntime();
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            [
                new PreparedJdtLsRawLocation(new Uri(ownedJavaPath).AbsoluteUri, 1, 1, 1, 7),
                new PreparedJdtLsRawLocation(new Uri(sourceJavaPath).AbsoluteUri, 1, 1, 1, 7),
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
            PreparedJavaLocation ordinarySourceLocation = Assert.Single(result.Locations);
            Assert.Equal("Source.java", ordinarySourceLocation.Source.RelativePath);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
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
    public async Task GetDefinitionAsync_WhenOwnershipViewBecomesUnavailableBeforeDisclosure_DiscardsBufferedReply()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        string ownedJavaPath = Path.Combine(codeGraphPath, "Owned.java");
        string sourceJavaPath = Path.Combine(fixture.PreparedRootPath, "Source.java");
        string callerJavaPath = Path.Combine(fixture.PreparedRootPath, "Caller.java");
        await File.WriteAllTextAsync(ownedJavaPath, "class Owned { void target() {} }\n");
        await File.WriteAllTextAsync(sourceJavaPath, "class Source { void target() {} }\n");
        await File.WriteAllTextAsync(callerJavaPath, "class Caller { void call() {} }\n");

        PreparedJavaNavigationOwnershipFixture bindingFixture =
            PreparedJavaNavigationOwnershipFixture.CreateVerifiedOwned(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedCodeGraphWorkspaceOwnership binding = bindingFixture.ApplicationHeldBinding;
        var ownershipStore = new AvailabilityChangingOwnershipStore(binding);
        var ownershipCoordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            new ControlledPreparedToolUseAuthorizer(
                fixture.Approval,
                PreparedToolPurpose.JdtNavigation),
            new MatchingNavigationOwnershipVerifier(binding),
            ownershipStore);
        PreparedCodeGraphOwnershipViewResult beforeNavigation =
            await ownershipCoordinator.GetOwnershipViewAsync(binding, fixture.Approval);
        Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, beforeNavigation.Status);
        Assert.Equal(binding, beforeNavigation.Record);

        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var runtime = new ControlledPreparedJdtLsRuntime();
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            [
                new PreparedJdtLsRawLocation(new Uri(ownedJavaPath).AbsoluteUri, 1, 1, 1, 7),
                new PreparedJdtLsRawLocation(new Uri(sourceJavaPath).AbsoluteUri, 1, 1, 1, 7),
            ],
            LimitReached: true,
            CapabilityLimitation: "controlled limitation");
        runtime.Session.AfterNavigate = ownershipStore.MakeUnavailable;
        var factory = new PreparedJavaNavigationContextFactory(
            new PreparedJavaNavigationProfile(
                disposableStateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime,
            ownershipCoordinator,
            _ => ValueTask.FromResult<PreparedCodeGraphWorkspaceOwnership?>(binding));
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
            Assert.Equal(
                PreparedToolWorkspaceCheckStatus.Current,
                await lease.CheckCurrentAsync());
            Assert.Equal(
                PreparedToolUseAuthorityStatus.Current,
                (await toolAuthorization.VerifyCurrentAsync(
                    fixture.Approval,
                    PreparedToolPurpose.JdtNavigation)).Status);
            Assert.Equal(PreparedSourceAuthorityStatus.Current,
                (await fixture.Authority.VerifyCurrentAsync(fixture.Approval)).Status);
            Assert.Equal(PreparedJavaNavigationOutcome.ConfigurationUnavailable, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(1, runtime.Session.DisposeCallCount);
            Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.Unavailable, ownershipStore.CurrentStatus);
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
    public async Task GetDefinitionAsync_WhenReadyVersionChangesBeforeDisclosure_DiscardsBufferedReply()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        string ownedJavaPath = Path.Combine(codeGraphPath, "Owned.java");
        string sourceJavaPath = Path.Combine(fixture.PreparedRootPath, "Source.java");
        string callerJavaPath = Path.Combine(fixture.PreparedRootPath, "Caller.java");
        await File.WriteAllTextAsync(ownedJavaPath, "class Owned { void target() {} }\n");
        await File.WriteAllTextAsync(sourceJavaPath, "class Source { void target() {} }\n");
        await File.WriteAllTextAsync(callerJavaPath, "class Caller { void call() {} }\n");

        PreparedJavaNavigationOwnershipFixture bindingFixture =
            PreparedJavaNavigationOwnershipFixture.CreateVerifiedOwned(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedCodeGraphWorkspaceOwnership binding = bindingFixture.ApplicationHeldBinding;
        var ownershipStore = new AvailabilityChangingOwnershipStore(binding);
        var ownershipCoordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            new ControlledPreparedToolUseAuthorizer(
                fixture.Approval,
                PreparedToolPurpose.JdtNavigation),
            new MatchingNavigationOwnershipVerifier(binding),
            ownershipStore);
        PreparedCodeGraphOwnershipViewResult beforeNavigation =
            await ownershipCoordinator.GetOwnershipViewAsync(binding, fixture.Approval);
        Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, beforeNavigation.Status);
        Assert.Equal(binding, beforeNavigation.Record);
        Assert.NotNull(beforeNavigation.Record?.StateEntryObjectIdentity);

        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var runtime = new ControlledPreparedJdtLsRuntime();
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            [
                new PreparedJdtLsRawLocation(new Uri(ownedJavaPath).AbsoluteUri, 1, 1, 1, 7),
                new PreparedJdtLsRawLocation(new Uri(sourceJavaPath).AbsoluteUri, 1, 1, 1, 7),
            ],
            LimitReached: true,
            CapabilityLimitation: "controlled limitation");
        runtime.Session.AfterNavigate = ownershipStore.AdvanceReadyVersion;
        var factory = new PreparedJavaNavigationContextFactory(
            new PreparedJavaNavigationProfile(
                disposableStateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime,
            ownershipCoordinator,
            _ => ValueTask.FromResult<PreparedCodeGraphWorkspaceOwnership?>(binding));
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
            Assert.Equal(
                PreparedToolWorkspaceCheckStatus.Current,
                await lease.CheckCurrentAsync());
            Assert.Equal(
                PreparedToolUseAuthorityStatus.Current,
                (await toolAuthorization.VerifyCurrentAsync(
                    fixture.Approval,
                    PreparedToolPurpose.JdtNavigation)).Status);
            Assert.Equal(PreparedSourceAuthorityStatus.Current,
                (await fixture.Authority.VerifyCurrentAsync(fixture.Approval)).Status);

            PreparedCodeGraphOwnershipViewResult afterVersionChange =
                await ownershipCoordinator.GetOwnershipViewAsync(binding, fixture.Approval);
            Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, afterVersionChange.Status);
            Assert.NotNull(afterVersionChange.Record);
            Assert.Equal(binding.OwnershipId, afterVersionChange.Record.OwnershipId);
            Assert.Equal(binding.Version + 1, afterVersionChange.Record.Version);
            Assert.Equal(binding.StateEntryObjectIdentity, afterVersionChange.Record.StateEntryObjectIdentity);
            Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.Found, ownershipStore.CurrentStatus);

            Assert.Equal(PreparedJavaNavigationOutcome.ConfigurationUnavailable, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(1, runtime.Session.DisposeCallCount);
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
    public async Task DefinitionFunction_WithContainedRawFileLocation_ReturnsRepositoryRelativeLocation()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example");
        Directory.CreateDirectory(javaDirectory);
        await File.WriteAllTextAsync(Path.Combine(javaDirectory, "Caller.java"), "package example; class Caller {}\n");
        string targetFilePath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(targetFilePath, "package example; class Target {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledPreparedJdtLsRuntime();
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            [new PreparedJdtLsRawLocation(new Uri(targetFilePath).AbsoluteUri, 4, 9, 4, 21)],
            LimitReached: false,
            CapabilityLimitation: null);
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;

        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);

            AIFunction definitionFunction = Assert.Single(
                new PreparedJavaNavigationFunctions(context).Create().Where(
                    function => function.Name == "navigate_java_definition"));
            object? rawWorkerResult = await definitionFunction.InvokeAsync(
                new AIFunctionArguments(new Dictionary<string, object?>
                {
                    ["relativePath"] = "src/main/java/example/Caller.java",
                    ["line"] = 1,
                    ["character"] = 1,
                    ["maximumLocations"] = 5,
                }),
                CancellationToken.None);
            PreparedJavaNavigationResult result = Assert.IsType<PreparedJavaNavigationResult>(rawWorkerResult);

            Assert.Equal(fixture.Approval.RepositoryId, result.RepositoryId);
            Assert.Equal(PreparedJavaNavigationOperation.Definition, result.Operation);
            Assert.Equal(PreparedJavaNavigationOutcome.Observed, result.Outcome);
            Assert.Equal(
                new PreparedJavaLocation(
                    new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/example/Target.java"),
                    4,
                    9,
                    4,
                    21),
                Assert.Single(result.Locations));
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(1, runtime.Session.NavigateCallCount);
        }
        finally
        {
            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task DefinitionFunction_WithJsonDecodedUntrustedUrisWithholdsPathsAndMapsContainedControl()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example");
        Directory.CreateDirectory(javaDirectory);
        await File.WriteAllTextAsync(Path.Combine(javaDirectory, "Caller.java"), "package example; class Caller {}\n");
        string targetFilePath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(targetFilePath, "package example; class Target {}\n");

        string rawDevicePath = $"""\\?\{targetFilePath}""";
        string rawDeviceNamespace = $"""\\.\{targetFilePath}""";
        string symlinkPath = Path.Combine(javaDirectory, "EscapesToSentinel.java");
        File.CreateSymbolicLink(symlinkPath, fixture.SentinelPath);
        string rootUri = new Uri(fixture.PreparedRootPath + Path.DirectorySeparatorChar).AbsoluteUri;
        string driveRoot = Path.GetPathRoot(targetFilePath)!;
        string fileRelativeToDrive = Path.GetRelativePath(driveRoot, targetFilePath).Replace('\\', '/');
        string[] rawUntrustedUris =
        [
            rawDevicePath,
            rawDeviceNamespace,
            $"file:///?/{driveRoot.Replace('\\', '/')}{fileRelativeToDrive}",
            "https://example.invalid/Target.java",
            new Uri(fixture.SentinelPath).AbsoluteUri,
            new Uri(symlinkPath).AbsoluteUri,
            rootUri + "%2e%2e/outside/sentinel.txt",
            rootUri + "src/main/java%2FTarget.java",
            "file://server/share/Secret.java",
            new Uri(targetFilePath).AbsoluteUri + "?token=private",
            new Uri(targetFilePath).AbsoluteUri + "#private-fragment",
        ];
        using JsonDocument protocolReply = JsonDocument.Parse(JsonSerializer.Serialize(rawUntrustedUris));
        string[] decodedRawUris = protocolReply.RootElement.EnumerateArray()
            .Select(element => Assert.IsType<string>(element.GetString()))
            .ToArray();
        Assert.Equal(rawUntrustedUris, decodedRawUris);
        string canonicalContainedUri = new Uri(targetFilePath).AbsoluteUri;

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledPreparedJdtLsRuntime();
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            decodedRawUris.Select(rawUri => new PreparedJdtLsRawLocation(rawUri, 2, 3, 2, 12))
                .Append(new PreparedJdtLsRawLocation(canonicalContainedUri, 4, 9, 4, 21))
                .ToArray(),
            LimitReached: false,
            CapabilityLimitation: null);
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;

        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);

            AIFunction definitionFunction = Assert.Single(
                new PreparedJavaNavigationFunctions(context).Create().Where(
                    function => function.Name == "navigate_java_definition"));
            object? rawWorkerResult = await definitionFunction.InvokeAsync(
                new AIFunctionArguments(new Dictionary<string, object?>
                {
                    ["relativePath"] = "src/main/java/example/Caller.java",
                    ["line"] = 1,
                    ["character"] = 1,
                    ["maximumLocations"] = decodedRawUris.Length + 1,
                }),
                CancellationToken.None);
            PreparedJavaNavigationResult result = Assert.IsType<PreparedJavaNavigationResult>(rawWorkerResult);

            Assert.Equal(PreparedJavaNavigationOutcome.Observed, result.Outcome);
            Assert.Equal(
                new PreparedJavaLocation(
                    new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/example/Target.java"),
                    4,
                    9,
                    4,
                    21),
                Assert.Single(result.Locations));
            Assert.Equal(decodedRawUris.Length, result.Limitations.Count);
            Assert.All(result.Limitations, limitation =>
            {
                Assert.Equal("UnmappableLocation", limitation.Code);
                Assert.Null(limitation.Source);
            });
            string serializedResult = JsonSerializer.Serialize(result);
            foreach (string rawUri in decodedRawUris)
            {
                Assert.DoesNotContain(rawUri, serializedResult, StringComparison.Ordinal);
            }

            Assert.DoesNotContain(fixture.SentinelPath, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(fixture.PreparedRootPath, serializedResult, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task GetDefinitionAsync_WhenActiveWorkerIsCancelled_ReturnsCancelledAndRetiresSession()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example", "Caller.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "package example; class Caller {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new BlockingNavigationRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;
        Task<PreparedJavaNavigationResult>? navigationTask = null;
        using var cancellation = new CancellationTokenSource();

        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);

            navigationTask = context.GetDefinitionAsync(
                new PreparedJavaNavigationRequest("src/main/java/example/Caller.java", 1, 1, 5),
                cancellation.Token);
            await runtime.Session.NavigationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            PreparedJavaNavigationResult cancelled = await navigationTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(PreparedJavaNavigationOutcome.Cancelled, cancelled.Outcome);
            Assert.Empty(cancelled.Locations);
            Assert.Empty(cancelled.Limitations);
            Assert.Null(cancelled.StoppingLimit);
            await runtime.Session.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Equal(1, runtime.Session.DisposeCallCount);
        }
        finally
        {
            runtime.Session.ReleaseNavigation.TrySetResult(true);
            if (navigationTask is not null)
            {
                await navigationTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task GetDefinitionAsync_WhenSessionExceedsRequestTimeout_ReturnsDeadlineExceededAndRetiresSession()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string callerDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example");
        Directory.CreateDirectory(callerDirectory);
        await File.WriteAllTextAsync(Path.Combine(callerDirectory, "Caller.java"), "package example; class Caller {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new BlockingNavigationRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;
        Task<PreparedJavaNavigationResult>? navigationTask = null;

        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);

            navigationTask = context.GetDefinitionAsync(
                new PreparedJavaNavigationRequest("src/main/java/example/Caller.java", 1, 1, 5));
            await runtime.Session.NavigationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task completed = await Task.WhenAny(navigationTask, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.Same(navigationTask, completed);
            PreparedJavaNavigationResult result = await navigationTask;

            Assert.Equal(PreparedJavaNavigationOutcome.DeadlineExceeded, result.Outcome);
            Assert.Equal(PreparedJavaNavigationOperation.Definition, result.Operation);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            await runtime.Session.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Equal(1, runtime.Session.DisposeCallCount);
        }
        finally
        {
            runtime.Session.ReleaseNavigation.TrySetResult(true);
            if (navigationTask is not null)
            {
                await navigationTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task GetDefinitionAsync_WhenProtocolDoesNotSettleBeforeShutdown_QuarantinesRepositoryAndDiscardsLateReply()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example", "Caller.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "package example; class Caller {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-unsettled-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(150),
            TimeSpan.FromMilliseconds(200),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new UnsettledNavigationRuntime(new PreparedJdtLsRawReply(
            [new PreparedJdtLsRawLocation(new Uri(callerPath).AbsoluteUri, 1, 1, 1, 10)],
            LimitReached: false,
            CapabilityLimitation: null));
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;
        PreparedJavaNavigationResult? result = null;

        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);

            Task<PreparedJavaNavigationResult> navigationTask = context.GetDefinitionAsync(
                new PreparedJavaNavigationRequest("src/main/java/example/Caller.java", 1, 1, 5));
            await runtime.Session.NavigationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task completed = await Task.WhenAny(navigationTask, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.Same(navigationTask, completed);
            result = await navigationTask;
            Assert.Equal(PreparedJavaNavigationOutcome.ConfigurationUnavailable, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.False(runtime.Session.NavigationSettled.Task.IsCompleted);
            Assert.Equal(1, runtime.Session.DisposeCallCount);

            PreparedJavaNavigationOpenResult retry = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, retry.Status);
            Assert.Null(retry.Context);
            Assert.Equal(1, runtime.OpenCallCount);
            Assert.Empty(result.Locations);
        }
        finally
        {
            runtime.Session.ReleaseNavigation.TrySetResult(true);
            runtime.Session.ReleaseDispose.TrySetResult(true);
            await runtime.Session.NavigationSettled.Task.WaitAsync(TimeSpan.FromSeconds(5));

            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }

        Assert.NotNull(result);
        Assert.Equal(PreparedJavaNavigationOutcome.ConfigurationUnavailable, result.Outcome);
        Assert.Empty(result.Locations);
        Assert.Empty(result.Limitations);
        Assert.Null(result.StoppingLimit);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task GetDefinitionAsync_WhenClasspathCapabilityIsUnavailable_ReturnsLimitedWithoutLeakingRawDetail()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example", "Caller.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "package example; class Caller {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        const string rawCapabilityDetail = "Missing classpath at /controlled/private-classpath-sentinel/secret.jar";
        var runtime = new ControlledPreparedJdtLsRuntime();
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            [],
            LimitReached: false,
            CapabilityLimitation: rawCapabilityDetail);
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;

        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);
            PreparedJavaNavigationResult result = await context.GetDefinitionAsync(
                new PreparedJavaNavigationRequest("src/main/java/example/Caller.java", 1, 1, 5));

            Assert.Equal(PreparedJavaNavigationOutcome.Limited, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.NotEmpty(result.Limitations);
            Assert.All(result.Limitations, limitation =>
                Assert.False(string.IsNullOrWhiteSpace(limitation.Code)));
            Assert.Null(result.StoppingLimit);

            string serialized = JsonSerializer.Serialize(result);
            Assert.DoesNotContain(rawCapabilityDetail, serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("secret.jar", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("Caller.java", serialized, StringComparison.Ordinal);
        }
        finally
        {
            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task GetDefinitionAsync_WhenProtocolThrows_RetiresSessionBeforeExplicitRetry()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example", "Caller.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "package example; class Caller {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ProtocolFailureThenSuccessRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? firstOpen = null;
        PreparedJavaNavigationOpenResult? retryOpen = null;
        Task<PreparedJavaNavigationResult>? navigationTask = null;

        try
        {
            firstOpen = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, firstOpen.Status);
            IPreparedJavaNavigationContext firstContext =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(firstOpen.Context);

            navigationTask = firstContext.GetDefinitionAsync(
                new PreparedJavaNavigationRequest("src/main/java/example/Caller.java", 1, 1, 5));
            await Task.WhenAny(
                runtime.FailingSession.DisposeStarted.Task,
                navigationTask,
                Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.True(
                runtime.FailingSession.DisposeStarted.Task.IsCompleted,
                "A protocol failure must begin retiring the unhealthy session before returning its result.");
            Assert.False(
                navigationTask.IsCompleted,
                "ProtocolFailed must wait for confirmed session cleanup before releasing the Repository slot.");

            runtime.FailingSession.ReleaseDisposeForTest();
            await runtime.FailingSession.DisposeSettled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            PreparedJavaNavigationResult result = await navigationTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(PreparedJavaNavigationOutcome.ProtocolFailed, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(1, runtime.FailingSession.NavigateCallCount);
            Assert.DoesNotContain(
                ProtocolFailureNavigationSession.FailureMessage,
                JsonSerializer.Serialize(result),
                StringComparison.Ordinal);
            Assert.Equal(1, runtime.OpenCallCount);

            retryOpen = await factory.OpenAsync(lease).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, retryOpen.Status);
            Assert.NotNull(retryOpen.Context);
            Assert.Equal(2, runtime.OpenCallCount);
        }
        finally
        {
            runtime.FailingSession.ReleaseDisposeForTest();
            if (navigationTask is not null)
            {
                await navigationTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (firstOpen?.Context is not null)
            {
                await firstOpen.Context.DisposeAsync();
            }

            if (retryOpen?.Context is not null)
            {
                await retryOpen.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task GetDefinitionAsync_WhenProtocolReplyIsNull_RetiresSessionBeforeExplicitRetry()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example", "Caller.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "package example; class Caller {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new NullReplyThenSuccessRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? firstOpen = null;
        PreparedJavaNavigationOpenResult? retryOpen = null;
        Task<PreparedJavaNavigationResult>? navigationTask = null;

        try
        {
            firstOpen = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, firstOpen.Status);
            IPreparedJavaNavigationContext firstContext =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(firstOpen.Context);

            navigationTask = firstContext.GetDefinitionAsync(
                new PreparedJavaNavigationRequest("src/main/java/example/Caller.java", 1, 1, 5));
            await Task.WhenAny(
                runtime.MalformedReplySession.DisposeStarted.Task,
                navigationTask,
                Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.True(
                runtime.MalformedReplySession.DisposeStarted.Task.IsCompleted,
                "A malformed protocol reply must begin retiring the unhealthy session before returning its result.");
            Assert.False(
                navigationTask.IsCompleted,
                "ProtocolFailed must wait for confirmed session cleanup before releasing the Repository slot.");

            runtime.MalformedReplySession.ReleaseDisposeForTest();
            await runtime.MalformedReplySession.DisposeSettled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            PreparedJavaNavigationResult result = await navigationTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(PreparedJavaNavigationOutcome.ProtocolFailed, result.Outcome);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(1, runtime.MalformedReplySession.NavigateCallCount);
            string serializedResult = JsonSerializer.Serialize(result);
            Assert.DoesNotContain("Caller.java", serializedResult, StringComparison.Ordinal);
            Assert.DoesNotContain(disposableStateRoot, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, runtime.OpenCallCount);

            retryOpen = await factory.OpenAsync(lease).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, retryOpen.Status);
            Assert.NotNull(retryOpen.Context);
            Assert.Equal(2, runtime.OpenCallCount);
        }
        finally
        {
            runtime.MalformedReplySession.ReleaseDisposeForTest();
            if (navigationTask is not null)
            {
                await navigationTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (firstOpen?.Context is not null)
            {
                await firstOpen.Context.DisposeAsync();
            }

            if (retryOpen?.Context is not null)
            {
                await retryOpen.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task GetDefinitionAsync_WhenProtocolFailureCleanupIsUnconfirmed_QuarantinesRepository()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example", "Caller.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "package example; class Caller {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(200),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ProtocolFailureThenSuccessRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;
        Task<PreparedJavaNavigationResult>? navigationTask = null;

        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);
            navigationTask = context.GetDefinitionAsync(
                new PreparedJavaNavigationRequest("src/main/java/example/Caller.java", 1, 1, 5));

            await runtime.FailingSession.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            PreparedJavaNavigationResult failed = await navigationTask.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(PreparedJavaNavigationOutcome.ConfigurationUnavailable, failed.Outcome);
            Assert.Empty(failed.Locations);
            Assert.Empty(failed.Limitations);
            Assert.Null(failed.StoppingLimit);
            Assert.DoesNotContain(
                ProtocolFailureNavigationSession.FailureMessage,
                JsonSerializer.Serialize(failed),
                StringComparison.Ordinal);
            Assert.Equal(1, runtime.FailingSession.NavigateCallCount);
            Assert.Equal(1, runtime.FailingSession.DisposeCallCount);
            Assert.False(runtime.FailingSession.DisposeSettled.Task.IsCompleted);

            PreparedJavaNavigationOpenResult retry = await factory.OpenAsync(lease)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, retry.Status);
            Assert.Null(retry.Context);
            Assert.Equal(1, runtime.OpenCallCount);
        }
        finally
        {
            runtime.FailingSession.ReleaseDisposeForTest();
            if (runtime.FailingSession.DisposeCallCount > 0)
            {
                await runtime.FailingSession.DisposeSettled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (navigationTask is not null)
            {
                await navigationTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_WhenReturnedSessionCleanupExceedsShutdownTimeout_QuarantinesRepository()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(200),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new LateReturningBlockedDisposeRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        Task<PreparedJavaNavigationOpenResult>? openingTask = null;
        PreparedJavaNavigationOpenResult? open = null;

        try
        {
            openingTask = factory.OpenAsync(lease);
            await runtime.OpenStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await runtime.InitializationDeadlineObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            runtime.ReturnSessionAfterDeadline();
            await runtime.Session.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Task completed = await Task.WhenAny(openingTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.True(
                ReferenceEquals(openingTask, completed),
                "Factory cleanup of a returned session must stop waiting at ShutdownTimeout.");

            open = await openingTask;
            Assert.Equal(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, open.Status);
            Assert.Null(open.Context);
            Assert.Equal(1, runtime.Session.DisposeCallCount);

            PreparedJavaNavigationOpenResult retry = await factory.OpenAsync(lease)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, retry.Status);
            Assert.Null(retry.Context);
            Assert.Equal(1, runtime.OpenCallCount);
        }
        finally
        {
            runtime.ReturnSessionAfterDeadline();
            runtime.Session.ReleaseDisposeForTest();
            if (runtime.Session.DisposeCallCount > 0)
            {
                await runtime.Session.DisposeSettled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (openingTask is not null)
            {
                open ??= await openingTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_WhenRuntimeReturnsAfterInitializationDeadline_DisposesSessionBeforeDeadlineResult()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new LateReturningReadinessRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        Task<PreparedJavaNavigationOpenResult>? openingTask = null;
        PreparedJavaNavigationOpenResult? open = null;

        try
        {
            openingTask = factory.OpenAsync(lease);
            await runtime.OpenStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await runtime.InitializationDeadlineObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            runtime.ReturnSessionAfterDeadline();
            open = await openingTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(PreparedJavaNavigationOpenStatus.DeadlineExceeded, open.Status);
            Assert.Null(open.Context);
            Assert.Equal(1, runtime.Session.DisposeCallCount);
            Assert.Equal(1, runtime.OpenCallCount);
        }
        finally
        {
            runtime.ReturnSessionAfterDeadline();
            if (openingTask is not null)
            {
                open ??= await openingTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_WhenRuntimeReadinessExceedsInitializationTimeout_SettlesBeforeReturningDeadlineExceeded()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new StalledReadinessRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        Task<PreparedJavaNavigationOpenResult>? openingTask = null;
        PreparedJavaNavigationOpenResult? open = null;

        try
        {
            openingTask = factory.OpenAsync(lease);
            await runtime.OpenStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task completed = await Task.WhenAny(
                openingTask,
                Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.True(
                ReferenceEquals(openingTask, completed),
                "InitializationTimeout must bound runtime readiness before a session is returned.");

            open = await openingTask;
            Assert.Equal(PreparedJavaNavigationOpenStatus.DeadlineExceeded, open.Status);
            Assert.Null(open.Context);
            Assert.True(runtime.DeadlineCancellationObserved);
            Assert.True(runtime.CleanupConfirmed);
            Assert.False(runtime.StartupResourceActive);
            Assert.Equal(1, runtime.OpenCallCount);
        }
        finally
        {
            runtime.ReleaseReadinessForTest();
            if (openingTask is not null)
            {
                open ??= await openingTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_AfterUnconfirmedCleanup_QuarantinesRepositoryWithoutRetryingRuntime()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new UnconfirmedCleanupRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);

        try
        {
            PreparedJavaNavigationOpenResult first = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, first.Status);
            Assert.Null(first.Context);

            PreparedJavaNavigationOpenResult retry = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, retry.Status);
            Assert.Null(retry.Context);
            Assert.Equal(1, runtime.OpenCallCount);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_AfterConfirmedPreReturnCleanup_AllowsSameRepositoryRetry()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ConfirmedCleanupThenSuccessRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? retry = null;

        try
        {
            PreparedJavaNavigationOpenResult failedOpen = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.ReadinessFailed, failedOpen.Status);
            Assert.Null(failedOpen.Context);
            Assert.Equal(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);
            Assert.Equal(1, runtime.OpenCallCount);

            retry = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, retry.Status);
            Assert.NotNull(retry.Context);
            Assert.Equal(2, runtime.OpenCallCount);
            Assert.Single(runtime.Sessions);
        }
        finally
        {
            if (retry?.Context is not null)
            {
                await retry.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_AfterUnexpectedRuntimeException_QuarantinesRepository()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new UnexpectedExceptionRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? retry = null;

        try
        {
            PreparedJavaNavigationOpenResult first = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, first.Status);
            Assert.Null(first.Context);

            retry = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, retry.Status);
            Assert.Null(retry.Context);
            Assert.Equal(1, runtime.OpenCallCount);
        }
        finally
        {
            if (retry?.Context is not null)
            {
                await retry.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task GetDefinitionAsync_WhenWorkersOverlap_SerializesProtocolCalls()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example", "Caller.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "package example; class Caller {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new BlockingNavigationRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;
        Task<PreparedJavaNavigationResult>? definitionTask = null;
        Task<PreparedJavaNavigationResult>? referencesTask = null;

        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);
            var request = new PreparedJavaNavigationRequest("src/main/java/example/Caller.java", 1, 1, 5);

            definitionTask = context.GetDefinitionAsync(request);
            await runtime.Session.NavigationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            referencesTask = context.GetReferencesAsync(request);

            Assert.Equal(1, runtime.Session.NavigationCallCount);
            Assert.False(referencesTask.IsCompleted);

            runtime.Session.ReleaseNavigation.TrySetResult(true);
            PreparedJavaNavigationResult definition = await definitionTask.WaitAsync(TimeSpan.FromSeconds(5));
            PreparedJavaNavigationResult references = await referencesTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(PreparedJavaNavigationOutcome.NoResultObserved, definition.Outcome);
            Assert.Equal(PreparedJavaNavigationOperation.Definition, definition.Operation);
            Assert.Equal(PreparedJavaNavigationOutcome.NoResultObserved, references.Outcome);
            Assert.Equal(PreparedJavaNavigationOperation.References, references.Operation);
            Assert.Equal(2, runtime.Session.NavigationCallCount);
            Assert.Equal(0, runtime.Session.DisposeCallCount);
        }
        finally
        {
            runtime.Session.ReleaseNavigation.TrySetResult(true);
            if (definitionTask is not null)
            {
                await definitionTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (referencesTask is not null)
            {
                await referencesTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task GetDefinitionAsync_WhenWorkerIsCancelledWhileWaitingForSession_StopsBeforeProtocolCall()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example", "Caller.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "package example; class Caller {}\n");

        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new BlockingNavigationRuntime();
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;
        Task<PreparedJavaNavigationResult>? activeRequest = null;
        Task<PreparedJavaNavigationResult>? cancelledWaitingRequest = null;

        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);

            var request = new PreparedJavaNavigationRequest("src/main/java/example/Caller.java", 1, 1, 5);
            activeRequest = context.GetDefinitionAsync(request);
            await runtime.Session.NavigationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            using var cancellation = new CancellationTokenSource();
            cancelledWaitingRequest = context.GetDefinitionAsync(request, cancellation.Token);
            cancellation.Cancel();

            Task completed = await Task.WhenAny(
                cancelledWaitingRequest,
                Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.True(
                ReferenceEquals(cancelledWaitingRequest, completed),
                "A cancelled Worker waiting for the serialized session must finish before the active request releases it.");

            PreparedJavaNavigationResult cancelled = await cancelledWaitingRequest;
            Assert.Equal(PreparedJavaNavigationOutcome.Cancelled, cancelled.Outcome);
            Assert.Empty(cancelled.Locations);
            Assert.Empty(cancelled.Limitations);
            Assert.Equal(1, runtime.Session.NavigationCallCount);
            Assert.False(activeRequest.IsCompleted);
            Assert.Equal(0, runtime.Session.DisposeCallCount);

            runtime.Session.ReleaseNavigation.TrySetResult(true);
            PreparedJavaNavigationResult active = await activeRequest.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(PreparedJavaNavigationOutcome.NoResultObserved, active.Outcome);
            Assert.Equal(0, runtime.Session.DisposeCallCount);
        }
        finally
        {
            runtime.Session.ReleaseNavigation.TrySetResult(true);
            if (activeRequest is not null)
            {
                await activeRequest.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (cancelledWaitingRequest is not null)
            {
                await cancelledWaitingRequest.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_WhenSameRepositorySlotRemainsBusy_ExpiresBoundedWaitWithoutSecondRuntimeOpen()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(10));
        PreparedToolWorkspaceIssueResult firstIssue = await issuer.IssueAsync(request);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, firstIssue.Status);
        PreparedToolWorkspaceLease firstLease = Assert.IsType<PreparedToolWorkspaceLease>(firstIssue.Lease);

        PreparedToolWorkspaceLease? secondLease = null;
        PreparedJavaNavigationOpenResult? firstOpen = null;
        try
        {
            PreparedToolWorkspaceIssueResult secondIssue = await issuer.IssueAsync(request);
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, secondIssue.Status);
            secondLease = Assert.IsType<PreparedToolWorkspaceLease>(secondIssue.Lease);
            Assert.Equal(firstLease.RepositoryId, secondLease.RepositoryId);

            string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
            Directory.CreateDirectory(disposableStateRoot);
            var profile = new PreparedJavaNavigationProfile(
                disposableStateRoot,
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024);
            var runtime = new ControlledPreparedJdtLsRuntime();
            var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);

            firstOpen = await factory.OpenAsync(firstLease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, firstOpen.Status);
            Assert.NotNull(firstOpen.Context);

            PreparedJavaNavigationOpenResult queuedOpen = await factory.OpenAsync(secondLease)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(PreparedJavaNavigationOpenStatus.DeadlineExceeded, queuedOpen.Status);
            Assert.Null(queuedOpen.Context);
            Assert.Equal(1, runtime.OpenCallCount);
        }
        finally
        {
            if (firstOpen?.Context is not null)
            {
                await firstOpen.Context.DisposeAsync();
            }

            if (secondLease is not null)
            {
                await secondLease.DisposeAsync();
            }

            await firstLease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task OpenAsync_WhenSameRepositorySlotIsBusy_CancelledWaitDoesNotInvokeRuntimeAgain()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(10));
        PreparedToolWorkspaceIssueResult firstIssue = await issuer.IssueAsync(request);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, firstIssue.Status);
        PreparedToolWorkspaceLease firstLease = Assert.IsType<PreparedToolWorkspaceLease>(firstIssue.Lease);

        PreparedToolWorkspaceLease? secondLease = null;
        Task<PreparedJavaNavigationOpenResult>? firstOpenTask = null;
        Task<PreparedJavaNavigationOpenResult>? secondOpenTask = null;
        PreparedJavaNavigationOpenResult? firstOpen = null;
        PreparedJavaNavigationOpenResult? secondOpen = null;
        var runtime = new ReservedSlotRuntime();
        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var factory = PreparedJavaNavigationOwnershipFixture.CreateFactory(fixture, profile, runtime);
        using var waitingCancellation = new CancellationTokenSource();

        try
        {
            PreparedToolWorkspaceIssueResult secondIssue = await issuer.IssueAsync(request);
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, secondIssue.Status);
            secondLease = Assert.IsType<PreparedToolWorkspaceLease>(secondIssue.Lease);
            Assert.Equal(firstLease.RepositoryId, secondLease.RepositoryId);

            firstOpenTask = factory.OpenAsync(firstLease);
            await runtime.FirstOpenStarted.WaitAsync(TimeSpan.FromSeconds(5));

            secondOpenTask = factory.OpenAsync(secondLease, waitingCancellation.Token);
            waitingCancellation.Cancel();
            secondOpen = await secondOpenTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(PreparedJavaNavigationOpenStatus.Cancelled, secondOpen.Status);
            Assert.Null(secondOpen.Context);
            Assert.Equal(1, runtime.OpenCallCount);

            runtime.ReleaseFirstOpen.TrySetResult(true);
            firstOpen = await firstOpenTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, firstOpen.Status);
            Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(firstOpen.Context);
            Assert.Equal(1, runtime.OpenCallCount);
        }
        finally
        {
            runtime.ReleaseFirstOpen.TrySetResult(true);
            if (firstOpenTask is not null)
            {
                firstOpen ??= await firstOpenTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (secondOpenTask is not null)
            {
                secondOpen ??= await secondOpenTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (secondOpen?.Context is not null)
            {
                await secondOpen.Context.DisposeAsync();
            }

            if (firstOpen?.Context is not null)
            {
                await firstOpen.Context.DisposeAsync();
            }

            if (secondLease is not null)
            {
                await secondLease.DisposeAsync();
            }

            await firstLease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedJdtLsRuntime_WhenLeaseAndPinnedCommandsAreCurrent_InitializesForGrantedRootWithOfflineSettings()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-runtime-state");
        string toolRoot = Path.Combine(fixture.SiblingPath, "controlled-jdt-tools");
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(toolRoot);
        PreparedToolDirectoryObjectIdentity? rootIdentity = null;
        string? canonicalRoot = null;
        PreparedToolWorkspaceCheckStatus leaseStatus = await lease.UseContainedRootAsync(
            PreparedToolPurpose.JdtNavigation,
            (scope, _) =>
            {
                canonicalRoot = scope.GetPhysicalRootPathForTrustedToolBinding();
                rootIdentity = scope.ObserveRootObjectIdentity();
                return ValueTask.CompletedTask;
            });
        Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, leaseStatus);
        Assert.Equal(Path.GetFullPath(fixture.PreparedRootPath), canonicalRoot);
        Assert.NotNull(rootIdentity);

        string jdtScriptPath = Path.Combine(toolRoot, "jdtls.bat");
        string javaPath = Path.Combine(toolRoot, "java.exe");
        string cmdPath = Path.Combine(toolRoot, "cmd.exe");
        await File.WriteAllTextAsync(jdtScriptPath, "@echo off\r\n");
        await File.WriteAllTextAsync(javaPath, "controlled java executable fixture\n");
        await File.WriteAllTextAsync(cmdPath, "controlled command interpreter fixture\n");
        PreparedJdtPinnedFile jdtCommand = await ObservePinnedFileAsync(jdtScriptPath, 0);
        PreparedJdtPinnedFile javaCommand = await ObservePinnedFileAsync(javaPath, 0);
        PreparedJdtPinnedFile fixedInterpreter = await ObservePinnedFileAsync(cmdPath, null);
        var hostPolicy = new PreparedJdtHostPolicy(
            [toolRoot],
            [toolRoot],
            cmdPath,
            [toolRoot]);
        var resolver = new RecordingPreparedJdtCommandResolver(
            new PreparedJdtPinnedCommands(jdtCommand, javaCommand, fixedInterpreter));
        var launcher = new RecordingPreparedJdtProcessLauncher();
        var protocols = new RecordingPreparedJdtProtocolFactory();
        var runtime = new PreparedJdtLsRuntime(resolver, launcher, protocols, hostPolicy);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            MaximumResponseBytes: 64 * 1024);

        IPreparedJdtLsSession? session = null;
        try
        {
            PreparedJdtLsOpenFailureException? openFailure = null;
            try
            {
                session = await runtime.OpenAsync(lease, profile);
            }
            catch (PreparedJdtLsOpenFailureException exception)
            {
                openFailure = exception;
            }

            Assert.Null(openFailure);
            Assert.NotNull(session);
            Assert.Equal(1, resolver.CallCount);
            Assert.Equal(1, launcher.CallCount);
            Assert.Equal(1, protocols.CallCount);
            await session.NavigateAsync(
                PreparedJavaNavigationOperation.Definition,
                new PreparedJavaNavigationRequest("src/main/java/Probe.java", 1, 1, 1));
            Assert.Equal(1, protocols.Session.NavigateCallCount);

            PreparedJdtCommandRequest commandRequest = Assert.IsType<PreparedJdtCommandRequest>(resolver.LastRequest);
            Assert.Equal(Path.GetFullPath(fixture.PreparedRootPath), commandRequest.PreparedRoot);
            Assert.Equal(Path.GetFullPath(stateRoot), commandRequest.DisposableStateRoot);
            AssertHostPolicyValues(hostPolicy, commandRequest.FixedHostPolicy);

            PreparedJdtLaunchRequest launchRequest = Assert.IsType<PreparedJdtLaunchRequest>(launcher.LastRequest);
            Assert.Equal(Path.GetFullPath(fixture.PreparedRootPath), launchRequest.CanonicalPreparedRoot);
            Assert.Equal(rootIdentity, launchRequest.RootIdentity);
            string expectedRootUri = new Uri(
                Path.GetFullPath(fixture.PreparedRootPath).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar).AbsoluteUri;
            Assert.Equal(expectedRootUri, launchRequest.RootUri);
            Assert.Equal(jdtScriptPath, launchRequest.Commands.JdtCommand.CanonicalAbsolutePath);
            Assert.Equal(javaPath, launchRequest.Commands.JavaCommand.CanonicalAbsolutePath);
            Assert.Equal(fixedInterpreter, launchRequest.Commands.FixedInterpreter);
            Assert.Equal(new[] { "-data", launchRequest.DataPath }, launchRequest.FixedArguments.Take(2));
            Assert.True(Path.IsPathFullyQualified(launchRequest.DataPath));
            Assert.True(Path.IsPathFullyQualified(launchRequest.ConfigurationPath));
            Assert.True(IsPathWithin(stateRoot, launchRequest.DataPath));
            Assert.True(IsPathWithin(stateRoot, launchRequest.ConfigurationPath));
            Assert.False(IsPathWithin(fixture.PreparedRootPath, launchRequest.DataPath));
            Assert.False(IsPathWithin(fixture.PreparedRootPath, launchRequest.ConfigurationPath));
            AssertHostPolicyValues(hostPolicy, launchRequest.FixedHostPolicy);
            Assert.Equal(string.Join(Path.PathSeparator, hostPolicy.FixedChildPathEntries),
                launchRequest.FixedChildEnvironment["PATH"]);
            foreach (string variable in new[] { "TEMP", "TMP", "USERPROFILE" })
            {
                Assert.True(launchRequest.FixedChildEnvironment.TryGetValue(variable, out string? ownedPath));
                Assert.True(Path.IsPathFullyQualified(ownedPath));
                Assert.True(IsPathWithin(stateRoot, ownedPath));
                Assert.False(IsPathWithin(fixture.PreparedRootPath, ownedPath));
            }

            PreparedJdtInitializeRequest initialize = Assert.IsType<PreparedJdtInitializeRequest>(protocols.LastRequest);
            Assert.Equal(expectedRootUri, initialize.RootUri);
            Assert.Equal(expectedRootUri, initialize.WorkspaceFolderUri);
            Assert.Equal("repository", initialize.WorkspaceFolderName);
            Assert.Equal(profile.MaximumResponseBytes, initialize.MaximumResponseBytes);
            AssertFixedOfflineSettings(initialize.FixedSettings);
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedJdtLsRuntime_WithPreparedStdioFactory_SerializesFixedInitializeHandshakeBeforeOpened()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "prepared-stdio-state");
        string toolRoot = Path.Combine(fixture.SiblingPath, "prepared-stdio-tools");
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(toolRoot);
        string jdtScriptPath = Path.Combine(toolRoot, "jdtls.bat");
        string javaPath = Path.Combine(toolRoot, "java.exe");
        string cmdPath = Path.Combine(toolRoot, "cmd.exe");
        await File.WriteAllTextAsync(jdtScriptPath, "@echo off\r\n");
        await File.WriteAllTextAsync(javaPath, "controlled java executable fixture\n");
        await File.WriteAllTextAsync(cmdPath, "controlled command interpreter fixture\n");
        PreparedJdtPinnedFile jdtCommand = await ObservePinnedFileAsync(jdtScriptPath, 0);
        PreparedJdtPinnedFile javaCommand = await ObservePinnedFileAsync(javaPath, 0);
        PreparedJdtPinnedFile fixedInterpreter = await ObservePinnedFileAsync(cmdPath, null);
        var hostPolicy = new PreparedJdtHostPolicy(
            [toolRoot],
            [toolRoot],
            cmdPath,
            [toolRoot]);
        var resolver = new RecordingPreparedJdtCommandResolver(
            new PreparedJdtPinnedCommands(jdtCommand, javaCommand, fixedInterpreter));
        var process = new PreparedJdtStdioPeerProcess();
        var launcher = new ReturningTrackedPreparedJdtProcessLauncher(process);
        var runtime = new PreparedJdtLsRuntime(
            resolver,
            launcher,
            new PreparedJdtProtocolFactory(),
            hostPolicy);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            MaximumResponseBytes: 64 * 1024);
        PreparedJavaNavigationOwnershipFixture ownershipFixture =
            PreparedJavaNavigationOwnershipFixture.Create(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedJavaNavigationContextFactory factory =
            ownershipFixture.CreateFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? opened = null;
        IPreparedJavaNavigationContext? context = null;
        try
        {
            opened = await factory.OpenAsync(lease);
            Assert.Equal(1, launcher.CallCount);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);

            await process.HandshakeObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
            JsonElement[] messages = process.ObservedMessages.ToArray();
            string[] methods = messages.Select(message => message.GetProperty("method").GetString()!)
                .ToArray();
            Assert.Equal(
                new[] { "initialize", "initialized", "workspace/didChangeConfiguration" },
                methods);

            string expectedRootUri = new Uri(
                Path.GetFullPath(fixture.PreparedRootPath).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar).AbsoluteUri;
            JsonElement initializeParameters = messages[0].GetProperty("params");
            Assert.Equal(expectedRootUri, initializeParameters.GetProperty("rootUri").GetString());
            JsonElement folders = initializeParameters.GetProperty("workspaceFolders");
            Assert.Equal(1, folders.GetArrayLength());
            Assert.Equal(expectedRootUri, folders[0].GetProperty("uri").GetString());
            Assert.Equal("repository", folders[0].GetProperty("name").GetString());

            JsonElement initializeSettings = initializeParameters
                .GetProperty("initializationOptions")
                .GetProperty("settings");
            JsonElement configurationSettings = messages[2]
                .GetProperty("params")
                .GetProperty("settings");
            Assert.True(
                AreEquivalentJsonValues(initializeSettings, configurationSettings),
                "Initialization and configuration notifications must carry the same settings values.");
            AssertFixedOfflineWireSettings(initializeSettings);
            Assert.True(messages[0].TryGetProperty("id", out _));
            Assert.False(messages[1].TryGetProperty("id", out _));
            Assert.False(messages[2].TryGetProperty("id", out _));
        }
        finally
        {
            if (context is not null)
            {
                await context.DisposeAsync();
            }

            await lease.DisposeAsync();
            await process.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedJdtLsRuntime_WithPreparedStdioFactory_NavigatesDefinitionAndMapsRawProtocolLocation()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "Caller.java");
        string targetPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "Target.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "class Caller { void call() {} }\n");
        await File.WriteAllTextAsync(targetPath, "class Target { void target() {} }\n");
        byte[] callerSourceBytesBefore = await File.ReadAllBytesAsync(callerPath);
        byte[] targetSourceBytesBefore = await File.ReadAllBytesAsync(targetPath);

        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "prepared-stdio-navigation-state");
        string toolRoot = Path.Combine(fixture.SiblingPath, "prepared-stdio-navigation-tools");
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(toolRoot);
        string jdtScriptPath = Path.Combine(toolRoot, "jdtls.bat");
        string javaPath = Path.Combine(toolRoot, "java.exe");
        string cmdPath = Path.Combine(toolRoot, "cmd.exe");
        await File.WriteAllTextAsync(jdtScriptPath, "@echo off\r\n");
        await File.WriteAllTextAsync(javaPath, "controlled java executable fixture\n");
        await File.WriteAllTextAsync(cmdPath, "controlled command interpreter fixture\n");
        PreparedJdtPinnedFile jdtCommand = await ObservePinnedFileAsync(jdtScriptPath, 0);
        PreparedJdtPinnedFile javaCommand = await ObservePinnedFileAsync(javaPath, 0);
        PreparedJdtPinnedFile fixedInterpreter = await ObservePinnedFileAsync(cmdPath, null);
        var hostPolicy = new PreparedJdtHostPolicy(
            [toolRoot],
            [toolRoot],
            cmdPath,
            [toolRoot]);
        var resolver = new RecordingPreparedJdtCommandResolver(
            new PreparedJdtPinnedCommands(jdtCommand, javaCommand, fixedInterpreter));
        var process = new PreparedJdtStdioPeerProcess();
        using (JsonDocument definitionReply = JsonDocument.Parse(JsonSerializer.Serialize(
                   new[]
                   {
                       new
                       {
                           uri = new Uri(targetPath).AbsoluteUri,
                           range = new
                           {
                               start = new { line = 5, character = 2 },
                               end = new { line = 5, character = 8 },
                           },
                       },
                   })))
        {
            process.DefinitionReply = definitionReply.RootElement.Clone();
        }

        var launcher = new ReturningTrackedPreparedJdtProcessLauncher(process);
        var runtime = new PreparedJdtLsRuntime(
            resolver,
            launcher,
            new PreparedJdtProtocolFactory(),
            hostPolicy);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            MaximumResponseBytes: 64 * 1024);
        PreparedJavaNavigationOwnershipFixture ownershipFixture =
            PreparedJavaNavigationOwnershipFixture.Create(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedJavaNavigationContextFactory factory =
            ownershipFixture.CreateFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? opened = null;
        IPreparedJavaNavigationContext? context = null;
        try
        {
            opened = await factory.OpenAsync(lease);
            Assert.Equal(1, launcher.CallCount);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);

            var request = new PreparedJavaNavigationRequest(
                "src/main/java/Caller.java",
                Line: 3,
                Character: 4,
                MaximumLocations: 5);
            PreparedJavaNavigationResult result = await context.GetDefinitionAsync(request);

            Assert.Equal(PreparedJavaNavigationOutcome.Observed, result.Outcome);
            Assert.Equal(fixture.Approval.RepositoryId, result.RepositoryId);
            Assert.Equal(PreparedJavaNavigationOperation.Definition, result.Operation);
            PreparedJavaLocation location = Assert.Single(result.Locations);
            Assert.Equal("src/main/java/Target.java", location.Source.RelativePath);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            string serializedResult = JsonSerializer.Serialize(result);
            Assert.DoesNotContain(fixture.PreparedRootPath, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(new Uri(targetPath).AbsoluteUri, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("class Target", serializedResult, StringComparison.Ordinal);
            Assert.Equal(callerSourceBytesBefore, await File.ReadAllBytesAsync(callerPath));
            Assert.Equal(targetSourceBytesBefore, await File.ReadAllBytesAsync(targetPath));

            await process.DefinitionObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
            JsonElement definitionMessage = Assert.Single(process.ObservedMessages.Where(message =>
                message.TryGetProperty("method", out JsonElement method) &&
                string.Equals(method.GetString(), "textDocument/definition", StringComparison.Ordinal)));
            JsonElement parameters = definitionMessage.GetProperty("params");
            Assert.Equal(
                new Uri(callerPath).AbsoluteUri,
                parameters.GetProperty("textDocument").GetProperty("uri").GetString());
            JsonElement position = parameters.GetProperty("position");
            Assert.Equal(2, position.GetProperty("line").GetInt32());
            Assert.Equal(3, position.GetProperty("character").GetInt32());
        }
        finally
        {
            if (context is not null)
            {
                await context.DisposeAsync();
            }

            await lease.DisposeAsync();
            await process.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedJdtLsRuntime_WithPreparedStdioFactory_NavigatesReferencesAndMapsRawProtocolLocation()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "Caller.java");
        string targetPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "Target.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "class Caller { void call() {} }\n");
        await File.WriteAllTextAsync(targetPath, "class Target { void target() {} }\n");
        byte[] callerSourceBytesBefore = await File.ReadAllBytesAsync(callerPath);
        byte[] targetSourceBytesBefore = await File.ReadAllBytesAsync(targetPath);

        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "prepared-stdio-references-state");
        string toolRoot = Path.Combine(fixture.SiblingPath, "prepared-stdio-references-tools");
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(toolRoot);
        string jdtScriptPath = Path.Combine(toolRoot, "jdtls.bat");
        string javaPath = Path.Combine(toolRoot, "java.exe");
        string cmdPath = Path.Combine(toolRoot, "cmd.exe");
        await File.WriteAllTextAsync(jdtScriptPath, "@echo off\r\n");
        await File.WriteAllTextAsync(javaPath, "controlled java executable fixture\n");
        await File.WriteAllTextAsync(cmdPath, "controlled command interpreter fixture\n");
        PreparedJdtPinnedFile jdtCommand = await ObservePinnedFileAsync(jdtScriptPath, 0);
        PreparedJdtPinnedFile javaCommand = await ObservePinnedFileAsync(javaPath, 0);
        PreparedJdtPinnedFile fixedInterpreter = await ObservePinnedFileAsync(cmdPath, null);
        var hostPolicy = new PreparedJdtHostPolicy(
            [toolRoot],
            [toolRoot],
            cmdPath,
            [toolRoot]);
        var resolver = new RecordingPreparedJdtCommandResolver(
            new PreparedJdtPinnedCommands(jdtCommand, javaCommand, fixedInterpreter));
        var process = new PreparedJdtStdioPeerProcess();
        using (JsonDocument referencesReply = JsonDocument.Parse(JsonSerializer.Serialize(
                   new[]
                   {
                       new
                       {
                           uri = new Uri(targetPath).AbsoluteUri,
                           range = new
                           {
                               start = new { line = 7, character = 1 },
                               end = new { line = 7, character = 9 },
                           },
                       },
                   })))
        {
            process.ReferencesReply = referencesReply.RootElement.Clone();
        }

        var launcher = new ReturningTrackedPreparedJdtProcessLauncher(process);
        var runtime = new PreparedJdtLsRuntime(
            resolver,
            launcher,
            new PreparedJdtProtocolFactory(),
            hostPolicy);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            MaximumResponseBytes: 64 * 1024);
        PreparedJavaNavigationOwnershipFixture ownershipFixture =
            PreparedJavaNavigationOwnershipFixture.Create(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedJavaNavigationContextFactory factory =
            ownershipFixture.CreateFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? opened = null;
        IPreparedJavaNavigationContext? context = null;
        try
        {
            opened = await factory.OpenAsync(lease);
            Assert.Equal(1, launcher.CallCount);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);

            var request = new PreparedJavaNavigationRequest(
                "src/main/java/Caller.java",
                Line: 3,
                Character: 4,
                MaximumLocations: 5);
            PreparedJavaNavigationResult result = await context.GetReferencesAsync(request);

            Assert.Equal(PreparedJavaNavigationOutcome.Observed, result.Outcome);
            Assert.Equal(fixture.Approval.RepositoryId, result.RepositoryId);
            Assert.Equal(PreparedJavaNavigationOperation.References, result.Operation);
            PreparedJavaLocation location = Assert.Single(result.Locations);
            Assert.Equal("src/main/java/Target.java", location.Source.RelativePath);
            Assert.Equal(8, location.StartLine);
            Assert.Equal(2, location.StartCharacter);
            Assert.Equal(8, location.EndLine);
            Assert.Equal(10, location.EndCharacter);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            string serializedResult = JsonSerializer.Serialize(result);
            Assert.DoesNotContain(fixture.PreparedRootPath, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(new Uri(targetPath).AbsoluteUri, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("class Target", serializedResult, StringComparison.Ordinal);
            Assert.Equal(callerSourceBytesBefore, await File.ReadAllBytesAsync(callerPath));
            Assert.Equal(targetSourceBytesBefore, await File.ReadAllBytesAsync(targetPath));

            await process.ReferencesObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
            JsonElement referencesMessage = Assert.Single(process.ObservedMessages.Where(message =>
                message.TryGetProperty("method", out JsonElement method) &&
                string.Equals(method.GetString(), "textDocument/references", StringComparison.Ordinal)));
            JsonElement parameters = referencesMessage.GetProperty("params");
            Assert.Equal(
                new Uri(callerPath).AbsoluteUri,
                parameters.GetProperty("textDocument").GetProperty("uri").GetString());
            JsonElement position = parameters.GetProperty("position");
            Assert.Equal(2, position.GetProperty("line").GetInt32());
            Assert.Equal(3, position.GetProperty("character").GetInt32());
            Assert.True(parameters.TryGetProperty("context", out JsonElement referencesContext));
            Assert.True(referencesContext.TryGetProperty("includeDeclaration", out JsonElement includeDeclaration));
            Assert.True(
                includeDeclaration.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "The references request must send the protocol-required boolean without exposing a caller policy flag.");
        }
        finally
        {
            if (context is not null)
            {
                await context.DisposeAsync();
            }

            await lease.DisposeAsync();
            await process.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedJdtLsRuntime_WithOversizedStdioReferencesReply_ReturnsByteLimitedResultWithoutContinuation()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "Caller.java");
        string targetPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "Target.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "class Caller { void call() {} }\n");
        await File.WriteAllTextAsync(targetPath, "class Target { void target() {} }\n");
        byte[] callerSourceBytesBefore = await File.ReadAllBytesAsync(callerPath);
        byte[] targetSourceBytesBefore = await File.ReadAllBytesAsync(targetPath);

        const int responseByteLimit = 128;
        const int maximumLocations = 5;
        var returnedLocations = new[]
        {
            new
            {
                uri = new Uri(targetPath).AbsoluteUri,
                range = new
                {
                    start = new { line = 7, character = 1 },
                    end = new { line = 7, character = 9 },
                },
            },
        };
        byte[] oversizedReplyBytes = JsonSerializer.SerializeToUtf8Bytes(returnedLocations);
        Assert.True(oversizedReplyBytes.Length > responseByteLimit, "The controlled protocol reply must exceed the trusted byte cap.");
        Assert.True(returnedLocations.Length < maximumLocations, "The controlled reply must remain below the requested location count cap.");

        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "prepared-stdio-references-byte-limit-state");
        string toolRoot = Path.Combine(fixture.SiblingPath, "prepared-stdio-references-byte-limit-tools");
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(toolRoot);
        string jdtScriptPath = Path.Combine(toolRoot, "jdtls.bat");
        string javaPath = Path.Combine(toolRoot, "java.exe");
        string cmdPath = Path.Combine(toolRoot, "cmd.exe");
        await File.WriteAllTextAsync(jdtScriptPath, "@echo off\r\n");
        await File.WriteAllTextAsync(javaPath, "controlled java executable fixture\n");
        await File.WriteAllTextAsync(cmdPath, "controlled command interpreter fixture\n");
        PreparedJdtPinnedFile jdtCommand = await ObservePinnedFileAsync(jdtScriptPath, 0);
        PreparedJdtPinnedFile javaCommand = await ObservePinnedFileAsync(javaPath, 0);
        PreparedJdtPinnedFile fixedInterpreter = await ObservePinnedFileAsync(cmdPath, null);
        var hostPolicy = new PreparedJdtHostPolicy(
            [toolRoot],
            [toolRoot],
            cmdPath,
            [toolRoot]);
        var resolver = new RecordingPreparedJdtCommandResolver(
            new PreparedJdtPinnedCommands(jdtCommand, javaCommand, fixedInterpreter));
        var process = new PreparedJdtStdioPeerProcess();
        using (JsonDocument referencesReply = JsonDocument.Parse(oversizedReplyBytes))
        {
            process.ReferencesReply = referencesReply.RootElement.Clone();
        }

        var launcher = new ReturningTrackedPreparedJdtProcessLauncher(process);
        var runtime = new PreparedJdtLsRuntime(
            resolver,
            launcher,
            new PreparedJdtProtocolFactory(),
            hostPolicy);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            MaximumResponseBytes: responseByteLimit);
        PreparedJavaNavigationOwnershipFixture ownershipFixture =
            PreparedJavaNavigationOwnershipFixture.Create(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedJavaNavigationContextFactory factory =
            ownershipFixture.CreateFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? opened = null;
        IPreparedJavaNavigationContext? context = null;
        try
        {
            opened = await factory.OpenAsync(lease);
            Assert.Equal(1, launcher.CallCount);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);

            var request = new PreparedJavaNavigationRequest(
                "src/main/java/Caller.java",
                Line: 3,
                Character: 4,
                MaximumLocations: maximumLocations);
            PreparedJavaNavigationResult result = await context.GetReferencesAsync(request);
            await process.ReferencesObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(PreparedJavaNavigationOutcome.Limited, result.Outcome);
            Assert.Equal(fixture.Approval.RepositoryId, result.RepositoryId);
            Assert.Equal(PreparedJavaNavigationOperation.References, result.Operation);
            Assert.Equal(PreparedJavaStoppingLimit.ResponseBytes, result.StoppingLimit);
            Assert.True(result.Locations.Count <= request.MaximumLocations);
            string serializedResult = JsonSerializer.Serialize(result);
            Assert.DoesNotContain(fixture.PreparedRootPath, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(new Uri(targetPath).AbsoluteUri, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("class Target", serializedResult, StringComparison.Ordinal);
            Assert.Equal(callerSourceBytesBefore, await File.ReadAllBytesAsync(callerPath));
            Assert.Equal(targetSourceBytesBefore, await File.ReadAllBytesAsync(targetPath));

            JsonElement referencesMessage = Assert.Single(process.ObservedMessages.Where(message =>
                message.TryGetProperty("method", out JsonElement method) &&
                string.Equals(method.GetString(), "textDocument/references", StringComparison.Ordinal)));
            JsonElement parameters = referencesMessage.GetProperty("params");
            Assert.Equal(
                new Uri(callerPath).AbsoluteUri,
                parameters.GetProperty("textDocument").GetProperty("uri").GetString());
            JsonElement position = parameters.GetProperty("position");
            Assert.Equal(2, position.GetProperty("line").GetInt32());
            Assert.Equal(3, position.GetProperty("character").GetInt32());
            Assert.True(parameters.TryGetProperty("context", out JsonElement referencesContext));
            Assert.True(referencesContext.TryGetProperty("includeDeclaration", out JsonElement includeDeclaration));
            Assert.True(
                includeDeclaration.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "The references request must send the protocol-required boolean without exposing a caller policy flag.");
            Assert.Equal(1, process.ObservedMessages.Count(message =>
                message.TryGetProperty("method", out JsonElement method) &&
                string.Equals(method.GetString(), "textDocument/references", StringComparison.Ordinal)));
        }
        finally
        {
            if (context is not null)
            {
                await context.DisposeAsync();
            }

            await lease.DisposeAsync();
            await process.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedJdtLsRuntime_WithEmptyStdioReferencesReply_ReturnsNoResultObserved()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string callerPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "Caller.java");
        string targetPath = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "Target.java");
        Directory.CreateDirectory(Path.GetDirectoryName(callerPath)!);
        await File.WriteAllTextAsync(callerPath, "class Caller { void call() {} }\n");
        await File.WriteAllTextAsync(targetPath, "class Target { void target() {} }\n");
        byte[] callerSourceBytesBefore = await File.ReadAllBytesAsync(callerPath);
        byte[] targetSourceBytesBefore = await File.ReadAllBytesAsync(targetPath);

        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "prepared-stdio-empty-references-state");
        string toolRoot = Path.Combine(fixture.SiblingPath, "prepared-stdio-empty-references-tools");
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(toolRoot);
        string jdtScriptPath = Path.Combine(toolRoot, "jdtls.bat");
        string javaPath = Path.Combine(toolRoot, "java.exe");
        string cmdPath = Path.Combine(toolRoot, "cmd.exe");
        await File.WriteAllTextAsync(jdtScriptPath, "@echo off\r\n");
        await File.WriteAllTextAsync(javaPath, "controlled java executable fixture\n");
        await File.WriteAllTextAsync(cmdPath, "controlled command interpreter fixture\n");
        PreparedJdtPinnedFile jdtCommand = await ObservePinnedFileAsync(jdtScriptPath, 0);
        PreparedJdtPinnedFile javaCommand = await ObservePinnedFileAsync(javaPath, 0);
        PreparedJdtPinnedFile fixedInterpreter = await ObservePinnedFileAsync(cmdPath, null);
        var hostPolicy = new PreparedJdtHostPolicy(
            [toolRoot],
            [toolRoot],
            cmdPath,
            [toolRoot]);
        var resolver = new RecordingPreparedJdtCommandResolver(
            new PreparedJdtPinnedCommands(jdtCommand, javaCommand, fixedInterpreter));
        var process = new PreparedJdtStdioPeerProcess();
        using (JsonDocument emptyReferencesReply = JsonDocument.Parse("[]"))
        {
            process.ReferencesReply = emptyReferencesReply.RootElement.Clone();
        }

        var launcher = new ReturningTrackedPreparedJdtProcessLauncher(process);
        var runtime = new PreparedJdtLsRuntime(
            resolver,
            launcher,
            new PreparedJdtProtocolFactory(),
            hostPolicy);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            MaximumResponseBytes: 64 * 1024);
        PreparedJavaNavigationOwnershipFixture ownershipFixture =
            PreparedJavaNavigationOwnershipFixture.Create(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedJavaNavigationContextFactory factory =
            ownershipFixture.CreateFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? opened = null;
        IPreparedJavaNavigationContext? context = null;
        try
        {
            opened = await factory.OpenAsync(lease);
            Assert.Equal(1, launcher.CallCount);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, opened.Status);
            context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(opened.Context);

            var request = new PreparedJavaNavigationRequest(
                "src/main/java/Caller.java",
                Line: 3,
                Character: 4,
                MaximumLocations: 5);
            PreparedJavaNavigationResult result = await context.GetReferencesAsync(request);
            await process.ReferencesObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(PreparedJavaNavigationOutcome.NoResultObserved, result.Outcome);
            Assert.Equal(fixture.Approval.RepositoryId, result.RepositoryId);
            Assert.Equal(PreparedJavaNavigationOperation.References, result.Operation);
            Assert.Empty(result.Locations);
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            string serializedResult = JsonSerializer.Serialize(result);
            Assert.DoesNotContain(fixture.PreparedRootPath, serializedResult, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("class Caller", serializedResult, StringComparison.Ordinal);
            Assert.Equal(callerSourceBytesBefore, await File.ReadAllBytesAsync(callerPath));
            Assert.Equal(targetSourceBytesBefore, await File.ReadAllBytesAsync(targetPath));

            JsonElement referencesMessage = Assert.Single(process.ObservedMessages.Where(message =>
                message.TryGetProperty("method", out JsonElement method) &&
                string.Equals(method.GetString(), "textDocument/references", StringComparison.Ordinal)));
            JsonElement parameters = referencesMessage.GetProperty("params");
            Assert.Equal(
                new Uri(callerPath).AbsoluteUri,
                parameters.GetProperty("textDocument").GetProperty("uri").GetString());
            JsonElement position = parameters.GetProperty("position");
            Assert.Equal(2, position.GetProperty("line").GetInt32());
            Assert.Equal(3, position.GetProperty("character").GetInt32());
            Assert.True(parameters.TryGetProperty("context", out JsonElement referencesContext));
            Assert.True(referencesContext.TryGetProperty("includeDeclaration", out JsonElement includeDeclaration));
            Assert.True(
                includeDeclaration.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "The references request must send the protocol-required boolean without exposing a caller policy flag.");
        }
        finally
        {
            if (context is not null)
            {
                await context.DisposeAsync();
            }

            await lease.DisposeAsync();
            await process.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedJdtCommandResolver_WhenFirstJdtMatchIsInsidePreparedRoot_RejectsResolution()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string sourceMarkerPath = Path.Combine(fixture.PreparedRootPath, "ResolverMarker.java");
        byte[] sourceMarker = "class ResolverMarker {}\n"u8.ToArray();
        await File.WriteAllBytesAsync(sourceMarkerPath, sourceMarker);

        string forbiddenFirstPath = Path.Combine(fixture.PreparedRootPath, "jdtls.bat");
        await File.WriteAllTextAsync(forbiddenFirstPath, "controlled forbidden first-match fixture\n");
        string laterCandidateRoot = Path.Combine(fixture.SiblingPath, "resolver-later-candidate");
        Directory.CreateDirectory(laterCandidateRoot);
        string laterJdtPath = Path.Combine(laterCandidateRoot, "jdtls.bat");
        string laterJavaPath = Path.Combine(laterCandidateRoot, "java.exe");
        string fixedCmdPath = Path.Combine(laterCandidateRoot, "cmd.exe");
        await File.WriteAllTextAsync(laterJdtPath, "controlled later JDT candidate fixture\n");
        await File.WriteAllTextAsync(laterJavaPath, "controlled later Java candidate fixture\n");
        await File.WriteAllTextAsync(fixedCmdPath, "controlled interpreter fixture\n");

        Assert.True(File.Exists(forbiddenFirstPath));
        Assert.True(File.Exists(laterJdtPath));
        var hostPolicy = new PreparedJdtHostPolicy(
            [laterCandidateRoot],
            [laterCandidateRoot],
            fixedCmdPath,
            [laterCandidateRoot]);
        var request = new PreparedJdtCommandRequest(
            string.Join(Path.PathSeparator, fixture.PreparedRootPath, laterCandidateRoot),
            fixture.PreparedRootPath,
            fixture.SiblingPath,
            hostPolicy);

        PreparedJdtCommandResolution resolution = await new PreparedJdtCommandResolver().ResolveAsync(
            request,
            CancellationToken.None);

        Assert.Equal(PreparedJdtCommandResolutionStatus.Rejected, resolution.Status);
        Assert.Null(resolution.Commands);
        Assert.Equal(sourceMarker, await File.ReadAllBytesAsync(sourceMarkerPath));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedJdtCommandResolver_WhenFirstJdtMatchIsInsideDisposableStateRoot_RejectsResolution()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string sourceMarkerPath = Path.Combine(fixture.PreparedRootPath, "ResolverStateMarker.java");
        byte[] sourceMarker = "class ResolverStateMarker {}\n"u8.ToArray();
        await File.WriteAllBytesAsync(sourceMarkerPath, sourceMarker);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "resolver-disposable-state-root");
        string laterCandidateRoot = Path.Combine(fixture.SiblingPath, "resolver-later-state-candidate");
        Directory.CreateDirectory(disposableStateRoot);
        Directory.CreateDirectory(laterCandidateRoot);
        string forbiddenFirstPath = Path.Combine(disposableStateRoot, "jdtls.bat");
        string laterCandidatePath = Path.Combine(laterCandidateRoot, "jdtls.bat");
        await File.WriteAllTextAsync(forbiddenFirstPath, "controlled state-root first-match fixture\n");
        await File.WriteAllTextAsync(laterCandidatePath, "controlled later JDT candidate fixture\n");

        Assert.True(File.Exists(forbiddenFirstPath));
        Assert.True(File.Exists(laterCandidatePath));
        // This is only the request's policy descriptor; it does not establish OS protection or make state executable.
        var hostPolicy = new PreparedJdtHostPolicy(
            [disposableStateRoot, laterCandidateRoot],
            [laterCandidateRoot],
            Path.Combine(laterCandidateRoot, "cmd.exe"),
            [laterCandidateRoot]);
        var request = new PreparedJdtCommandRequest(
            string.Join(Path.PathSeparator, disposableStateRoot, laterCandidateRoot),
            fixture.PreparedRootPath,
            disposableStateRoot,
            hostPolicy);

        PreparedJdtCommandResolution resolution = await new PreparedJdtCommandResolver().ResolveAsync(
            request,
            CancellationToken.None);

        Assert.Equal(PreparedJdtCommandResolutionStatus.Rejected, resolution.Status);
        Assert.Null(resolution.Commands);
        Assert.Equal(sourceMarker, await File.ReadAllBytesAsync(sourceMarkerPath));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedJdtCommandResolver_WhenApprovedPythonBootstrapIsFirstOnPath_PinsTheCommandChain()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string stateRoot = Path.Combine(fixture.SiblingPath, "resolver-python-state");
        string jdtCommandDirectory = Path.Combine(fixture.SiblingPath, "resolver-python-jdt");
        string javaDirectory = Path.Combine(fixture.SiblingPath, "resolver-python-java");
        string pythonDirectory = Path.Combine(fixture.SiblingPath, "resolver-python-python");
        string cmdDirectory = Path.Combine(fixture.SiblingPath, "resolver-python-cmd");
        string runtimeRoot = Path.Combine(fixture.SiblingPath, "resolver-python-runtime");
        string bootstrapDirectory = Path.Combine(runtimeRoot, "bin");
        string pluginDirectory = Path.Combine(runtimeRoot, "plugins");
        foreach (string directory in new[]
        {
            stateRoot,
            jdtCommandDirectory,
            javaDirectory,
            pythonDirectory,
            cmdDirectory,
            bootstrapDirectory,
            Path.Combine(runtimeRoot, "config_win"),
            pluginDirectory,
        })
        {
            Directory.CreateDirectory(directory);
        }

        string bootstrapPath = Path.Combine(bootstrapDirectory, "jdtls");
        string pythonModulePath = Path.Combine(bootstrapDirectory, "jdtls.py");
        string configurationPath = Path.Combine(runtimeRoot, "config_win", "config.ini");
        string launcherModulePath = Path.Combine(pluginDirectory, "org.eclipse.equinox.launcher_1.0.0.jar");
        string jdtCommandPath = Path.Combine(jdtCommandDirectory, "jdtls.bat");
        string javaPath = Path.Combine(javaDirectory, "java.exe");
        string pythonPath = Path.Combine(pythonDirectory, "python.exe");
        string cmdPath = Path.Combine(cmdDirectory, "cmd.exe");
        await File.WriteAllTextAsync(
            jdtCommandPath,
            $"@echo off\r\npython \"{bootstrapPath}\" %*\r\n");
        await File.WriteAllTextAsync(bootstrapPath, "controlled JDT bootstrap fixture\n");
        await File.WriteAllTextAsync(pythonModulePath, "controlled Python module fixture\n");
        await File.WriteAllTextAsync(configurationPath, "controlled Equinox configuration fixture\n");
        await File.WriteAllTextAsync(launcherModulePath, "controlled launcher module fixture\n");
        await File.WriteAllTextAsync(javaPath, "controlled Java executable fixture\n");
        await File.WriteAllTextAsync(pythonPath, "controlled Python executable fixture\n");
        await File.WriteAllTextAsync(cmdPath, "controlled command interpreter fixture\n");

        var hostPolicy = new PreparedJdtHostPolicy(
            [jdtCommandDirectory, javaDirectory, pythonDirectory, cmdDirectory],
            [runtimeRoot],
            cmdPath,
            [jdtCommandDirectory, javaDirectory, pythonDirectory, cmdDirectory]);
        var request = new PreparedJdtCommandRequest(
            string.Join(
                Path.PathSeparator,
                jdtCommandDirectory,
                javaDirectory,
                pythonDirectory,
                cmdDirectory),
            fixture.PreparedRootPath,
            stateRoot,
            hostPolicy);

        PreparedJdtCommandResolution resolution = await new PreparedJdtCommandResolver().ResolveAsync(
            request,
            CancellationToken.None);

        Assert.Equal(PreparedJdtCommandResolutionStatus.Pinned, resolution.Status);
        PreparedJdtPinnedCommands commands = Assert.IsType<PreparedJdtPinnedCommands>(resolution.Commands);
        await AssertPinnedCommandAsync(commands.JdtCommand, jdtCommandPath, expectedPathIndex: 0);
        await AssertPinnedCommandAsync(commands.JavaCommand, javaPath, expectedPathIndex: 1);
        await AssertPinnedCommandAsync(commands.FixedInterpreter, cmdPath, expectedPathIndex: null);
        await AssertPinnedCommandAsync(commands.PythonCommand, pythonPath, expectedPathIndex: 2);
        await AssertPinnedCommandAsync(commands.BootstrapScript, bootstrapPath, expectedPathIndex: null);
        IReadOnlyList<PreparedJdtPinnedFile> runtimeInputs = Assert.IsAssignableFrom<
            IReadOnlyList<PreparedJdtPinnedFile>>(commands.RuntimeInputs);
        foreach (string runtimeInputPath in new[]
        {
            pythonModulePath,
            configurationPath,
            launcherModulePath,
        })
        {
            PreparedJdtPinnedFile runtimeInput = Assert.Single(runtimeInputs.Where(pin =>
                string.Equals(
                    pin.CanonicalAbsolutePath,
                    Path.GetFullPath(runtimeInputPath),
                    StringComparison.OrdinalIgnoreCase)));
            await AssertPinnedCommandAsync(runtimeInput, runtimeInputPath, expectedPathIndex: null);
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [InlineData("first-java")]
    [InlineData("first-python")]
    [InlineData("bootstrap-outside-runtime-root")]
    public async Task PreparedJdtCommandResolver_WhenFirstChainInputIsUnapproved_RejectsWithoutFallback(
        string unsafeInput)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string stateRoot = Path.Combine(fixture.SiblingPath, "resolver-unsafe-state");
        string jdtDirectory = Path.Combine(fixture.SiblingPath, "resolver-unsafe-jdt");
        string approvedJavaDirectory = Path.Combine(fixture.SiblingPath, "resolver-approved-java");
        string unapprovedJavaDirectory = Path.Combine(fixture.SiblingPath, "resolver-unapproved-java");
        string approvedPythonDirectory = Path.Combine(fixture.SiblingPath, "resolver-approved-python");
        string unapprovedPythonDirectory = Path.Combine(fixture.SiblingPath, "resolver-unapproved-python");
        string cmdDirectory = Path.Combine(fixture.SiblingPath, "resolver-unsafe-cmd");
        string runtimeRoot = Path.Combine(fixture.SiblingPath, "resolver-unsafe-runtime");
        string externalBootstrapRoot = Path.Combine(fixture.SiblingPath, "resolver-escaped-bootstrap");
        string bootstrapPath = Path.Combine(runtimeRoot, "bin", "jdtls");
        string externalBootstrapPath = Path.Combine(externalBootstrapRoot, "jdtls");
        string selectedBootstrap = unsafeInput == "bootstrap-outside-runtime-root"
            ? externalBootstrapPath
            : bootstrapPath;

        foreach (string directory in new[]
        {
            stateRoot,
            jdtDirectory,
            approvedJavaDirectory,
            unapprovedJavaDirectory,
            approvedPythonDirectory,
            unapprovedPythonDirectory,
            cmdDirectory,
            Path.GetDirectoryName(bootstrapPath)!,
            Path.Combine(runtimeRoot, "config_win"),
            Path.Combine(runtimeRoot, "plugins"),
            externalBootstrapRoot,
        })
        {
            Directory.CreateDirectory(directory);
        }

        string jdtCommandPath = Path.Combine(jdtDirectory, "jdtls.bat");
        string approvedJavaPath = Path.Combine(approvedJavaDirectory, "java.exe");
        string unapprovedJavaPath = Path.Combine(unapprovedJavaDirectory, "java.exe");
        string approvedPythonPath = Path.Combine(approvedPythonDirectory, "python.exe");
        string unapprovedPythonPath = Path.Combine(unapprovedPythonDirectory, "python.exe");
        string cmdPath = Path.Combine(cmdDirectory, "cmd.exe");
        await File.WriteAllTextAsync(jdtCommandPath, $"@echo off\r\npython \"{selectedBootstrap}\" %*\r\n");
        await File.WriteAllTextAsync(bootstrapPath, "controlled declared bootstrap fixture\n");
        await File.WriteAllTextAsync(externalBootstrapPath, "controlled escaped bootstrap fixture\n");
        await File.WriteAllTextAsync(Path.Combine(runtimeRoot, "bin", "jdtls.py"), "controlled module fixture\n");
        await File.WriteAllTextAsync(Path.Combine(runtimeRoot, "config_win", "config.ini"), "controlled config fixture\n");
        await File.WriteAllTextAsync(
            Path.Combine(runtimeRoot, "plugins", "org.eclipse.equinox.launcher_1.0.0.jar"),
            "controlled plugin fixture\n");
        await File.WriteAllTextAsync(approvedJavaPath, "approved Java candidate fixture\n");
        await File.WriteAllTextAsync(unapprovedJavaPath, "unapproved first Java fixture\n");
        await File.WriteAllTextAsync(approvedPythonPath, "approved Python candidate fixture\n");
        await File.WriteAllTextAsync(unapprovedPythonPath, "unapproved first Python fixture\n");
        await File.WriteAllTextAsync(cmdPath, "approved command interpreter fixture\n");

        var pathEntries = new List<string> { jdtDirectory };
        if (unsafeInput == "first-java")
        {
            pathEntries.Add(unapprovedJavaDirectory);
        }

        pathEntries.Add(approvedJavaDirectory);
        if (unsafeInput == "first-python")
        {
            pathEntries.Add(unapprovedPythonDirectory);
        }

        pathEntries.Add(approvedPythonDirectory);
        pathEntries.Add(cmdDirectory);
        var hostPolicy = new PreparedJdtHostPolicy(
            [jdtDirectory, approvedJavaDirectory, approvedPythonDirectory, cmdDirectory],
            [runtimeRoot],
            cmdPath,
            [jdtDirectory, approvedJavaDirectory, approvedPythonDirectory, cmdDirectory]);
        var request = new PreparedJdtCommandRequest(
            string.Join(Path.PathSeparator, pathEntries),
            fixture.PreparedRootPath,
            stateRoot,
            hostPolicy);

        PreparedJdtCommandResolution resolution = await new PreparedJdtCommandResolver().ResolveAsync(
            request,
            CancellationToken.None);

        Assert.Equal(PreparedJdtCommandResolutionStatus.Rejected, resolution.Status);
        Assert.Null(resolution.Commands);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [InlineData("changed-bytes")]
    [InlineData("same-bytes-new-identity")]
    public async Task PreparedJdtLsRuntime_WhenPinnedRuntimeInputChangesBeforeOpen_DoesNotLaunch(
        string replacementKind)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-runtime-input-change-state");
        string commandRoot = Path.Combine(fixture.SiblingPath, "jdt-runtime-input-change-commands");
        string runtimeRoot = Path.Combine(fixture.SiblingPath, "jdt-runtime-input-change-installation");
        string runtimeBin = Path.Combine(runtimeRoot, "bin");
        string runtimeConfig = Path.Combine(runtimeRoot, "config_win");
        string runtimePlugins = Path.Combine(runtimeRoot, "plugins");
        foreach (string directory in new[]
        {
            stateRoot,
            commandRoot,
            runtimeBin,
            runtimeConfig,
            runtimePlugins,
        })
        {
            Directory.CreateDirectory(directory);
        }

        string jdtCommandPath = Path.Combine(commandRoot, "jdtls.bat");
        string javaPath = Path.Combine(commandRoot, "java.exe");
        string pythonPath = Path.Combine(commandRoot, "python.exe");
        string cmdPath = Path.Combine(commandRoot, "cmd.exe");
        string bootstrapPath = Path.Combine(runtimeBin, "jdtls");
        string pythonModulePath = Path.Combine(runtimeBin, "jdtls.py");
        string configurationPath = Path.Combine(runtimeConfig, "config.ini");
        string priorConfigurationPath = Path.Combine(runtimeConfig, "prior-config.ini");
        string launcherModulePath = Path.Combine(runtimePlugins, "org.eclipse.equinox.launcher_1.0.0.jar");
        const string configurationContents = "controlled Equinox configuration fixture\n";
        await File.WriteAllTextAsync(jdtCommandPath, $"@echo off\r\npython \"{bootstrapPath}\" %*\r\n");
        await File.WriteAllTextAsync(javaPath, "controlled Java executable fixture\n");
        await File.WriteAllTextAsync(pythonPath, "controlled Python executable fixture\n");
        await File.WriteAllTextAsync(cmdPath, "controlled command interpreter fixture\n");
        await File.WriteAllTextAsync(bootstrapPath, "controlled JDT bootstrap fixture\n");
        await File.WriteAllTextAsync(pythonModulePath, "controlled Python module fixture\n");
        await File.WriteAllTextAsync(configurationPath, configurationContents);
        await File.WriteAllTextAsync(launcherModulePath, "controlled launcher module fixture\n");

        PreparedJdtPinnedFile jdtCommand = await ObservePinnedFileAsync(jdtCommandPath, 0);
        PreparedJdtPinnedFile javaCommand = await ObservePinnedFileAsync(javaPath, 1);
        PreparedJdtPinnedFile fixedInterpreter = await ObservePinnedFileAsync(cmdPath, null);
        PreparedJdtPinnedFile pythonCommand = await ObservePinnedFileAsync(pythonPath, 2);
        PreparedJdtPinnedFile bootstrap = await ObservePinnedFileAsync(bootstrapPath, null);
        PreparedJdtPinnedFile pythonModule = await ObservePinnedFileAsync(pythonModulePath, null);
        PreparedJdtPinnedFile configuration = await ObservePinnedFileAsync(configurationPath, null);
        PreparedJdtPinnedFile launcherModule = await ObservePinnedFileAsync(launcherModulePath, null);
        var commands = new PreparedJdtPinnedCommands(
            jdtCommand,
            javaCommand,
            fixedInterpreter,
            PythonCommand: pythonCommand,
            BootstrapScript: bootstrap,
            RuntimeInputs: [pythonModule, configuration, launcherModule]);
        if (replacementKind == "same-bytes-new-identity")
        {
            File.Move(configurationPath, priorConfigurationPath);
            await File.WriteAllTextAsync(configurationPath, configurationContents);
            PreparedJdtPinnedFile replacedConfiguration = await ObservePinnedFileAsync(configurationPath, null);
            Assert.Equal(configuration.Sha256, replacedConfiguration.Sha256);
            Assert.NotEqual(configuration.FileIdentity, replacedConfiguration.FileIdentity);
        }
        else
        {
            await File.AppendAllTextAsync(configurationPath, "changed after command resolution\n");
            PreparedJdtPinnedFile changedConfiguration = await ObservePinnedFileAsync(configurationPath, null);
            Assert.NotEqual(configuration.Sha256, changedConfiguration.Sha256);
        }

        var hostPolicy = new PreparedJdtHostPolicy(
            [commandRoot],
            [runtimeRoot],
            cmdPath,
            [commandRoot]);
        var resolver = new RecordingPreparedJdtCommandResolver(commands);
        var launcher = new RecordingPreparedJdtProcessLauncher();
        var protocols = new RecordingPreparedJdtProtocolFactory();
        var runtime = new PreparedJdtLsRuntime(resolver, launcher, protocols, hostPolicy);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            MaximumResponseBytes: 64 * 1024);

        try
        {
            PreparedJdtLsOpenFailureException failure = await Assert.ThrowsAsync<
                PreparedJdtLsOpenFailureException>(async () => await runtime.OpenAsync(lease, profile));

            Assert.Equal(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable, failure.Status);
            Assert.True(failure.CleanupConfirmed);
            Assert.Equal(1, resolver.CallCount);
            Assert.Equal(0, launcher.CallCount);
            Assert.Equal(0, protocols.CallCount);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedJdtLsRuntime_WhenCancelledBeforeCommandResolution_DoesNotLaunch()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-cancelled-before-resolution-state");
        Directory.CreateDirectory(stateRoot);
        var hostPolicy = new PreparedJdtHostPolicy([], [], string.Empty, []);
        var launcher = new RecordingPreparedJdtProcessLauncher();
        var protocols = new RecordingPreparedJdtProtocolFactory();
        var runtime = new PreparedJdtLsRuntime(
            new PreparedJdtCommandResolver(),
            launcher,
            protocols,
            hostPolicy);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            MaximumResponseBytes: 64 * 1024);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            PreparedJdtLsOpenFailureException failure = await Assert.ThrowsAsync<
                PreparedJdtLsOpenFailureException>(async () =>
                    await runtime.OpenAsync(lease, profile, cancellation.Token));

            Assert.Equal(PreparedJavaNavigationOpenStatus.Cancelled, failure.Status);
            Assert.True(failure.CleanupConfirmed);
            Assert.Equal(0, launcher.CallCount);
            Assert.Equal(0, protocols.CallCount);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void PreparedJdtOwnedStateDirectories_WhenCreatedDirectoryIsReplaced_RefusesCleanupAndPreservesReplacement()
    {
        string stateRoot = Path.Combine(Path.GetTempPath(), $"vulgata-jdt-owned-state-{Guid.NewGuid():N}");
        string dataPath = Path.Combine(stateRoot, "data");
        string configurationPath = Path.Combine(stateRoot, "configuration");
        string tempPath = Path.Combine(stateRoot, "temp");
        string profilePath = Path.Combine(stateRoot, "profile");
        string displacedDataPath = Path.Combine(Path.GetTempPath(), $"vulgata-jdt-displaced-data-{Guid.NewGuid():N}");
        string replacementMarkerPath = Path.Combine(dataPath, "replacement-marker.txt");
        const string replacementMarker = "leave replacement state untouched";
        var ownedState = new PreparedJdtOwnedStateDirectories(stateRoot);

        try
        {
            ownedState.Prepare(dataPath, configurationPath, tempPath, profilePath);
            Directory.Move(dataPath, displacedDataPath);
            Directory.CreateDirectory(dataPath);
            File.WriteAllText(replacementMarkerPath, replacementMarker);

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                ownedState.CleanupAfterConfirmedExit);

            Assert.Equal("jdt_state_cleanup_unconfirmed", failure.Message);
            Assert.True(Directory.Exists(dataPath));
            Assert.Equal(replacementMarker, File.ReadAllText(replacementMarkerPath));
        }
        finally
        {
            if (Directory.Exists(stateRoot))
            {
                Directory.Delete(stateRoot, recursive: true);
            }

            if (Directory.Exists(displacedDataPath))
            {
                Directory.Delete(displacedDataPath, recursive: true);
            }
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void PreparedJdtOwnedStateDirectories_WhenStateRootAlreadyExists_PreservesCallerOwnedContent()
    {
        string stateRoot = Path.Combine(Path.GetTempPath(), $"vulgata-jdt-existing-state-{Guid.NewGuid():N}");
        string markerPath = Path.Combine(stateRoot, "caller-owned-marker.txt");
        string marker = "preserve caller-owned state";
        string dataPath = Path.Combine(stateRoot, "data");
        string configurationPath = Path.Combine(stateRoot, "configuration");
        string tempPath = Path.Combine(stateRoot, "temp");
        string profilePath = Path.Combine(stateRoot, "profile");
        Directory.CreateDirectory(stateRoot);
        File.WriteAllText(markerPath, marker);
        var ownedState = new PreparedJdtOwnedStateDirectories(stateRoot);

        try
        {
            ownedState.Prepare(dataPath, configurationPath, tempPath, profilePath);
            ownedState.CleanupAfterConfirmedExit();

            Assert.True(Directory.Exists(stateRoot));
            Assert.Equal(marker, File.ReadAllText(markerPath));
            Assert.False(Directory.Exists(dataPath));
            Assert.False(Directory.Exists(configurationPath));
            Assert.False(Directory.Exists(tempPath));
            Assert.False(Directory.Exists(profilePath));
        }
        finally
        {
            if (Directory.Exists(stateRoot))
            {
                Directory.Delete(stateRoot, recursive: true);
            }
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void PreparedJdtOwnedStateDirectories_WhenDataDirectoryAlreadyExists_RejectsReuseAndPreservesMarker()
    {
        string stateRoot = Path.Combine(Path.GetTempPath(), $"vulgata-jdt-existing-data-{Guid.NewGuid():N}");
        string dataPath = Path.Combine(stateRoot, "data");
        string configurationPath = Path.Combine(stateRoot, "configuration");
        string tempPath = Path.Combine(stateRoot, "temp");
        string profilePath = Path.Combine(stateRoot, "profile");
        string markerPath = Path.Combine(dataPath, "previous-session-marker.txt");
        const string marker = "do not adopt previous JDT data";
        Directory.CreateDirectory(dataPath);
        File.WriteAllText(markerPath, marker);
        var ownedState = new PreparedJdtOwnedStateDirectories(stateRoot);

        try
        {
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                ownedState.Prepare(dataPath, configurationPath, tempPath, profilePath));

            Assert.Equal("jdt_state_freshness_unavailable", failure.Message);
            Assert.True(Directory.Exists(dataPath));
            Assert.Equal(marker, File.ReadAllText(markerPath));
            Assert.False(Directory.Exists(configurationPath));
            Assert.False(Directory.Exists(tempPath));
            Assert.False(Directory.Exists(profilePath));
        }
        finally
        {
            if (Directory.Exists(stateRoot))
            {
                Directory.Delete(stateRoot, recursive: true);
            }
        }
    }

    private static async Task AssertPinnedCommandAsync(
        PreparedJdtPinnedFile? pinned,
        string expectedPath,
        int? expectedPathIndex)
    {
        PreparedJdtPinnedFile actual = Assert.IsType<PreparedJdtPinnedFile>(pinned);
        PreparedJdtPinnedFile expected = await ObservePinnedFileAsync(expectedPath, expectedPathIndex);
        Assert.Equal(expected.CanonicalAbsolutePath, actual.CanonicalAbsolutePath);
        Assert.Equal(expected.FileIdentity, actual.FileIdentity);
        Assert.Equal(expected.Sha256, actual.Sha256);
        Assert.Equal(expectedPathIndex, actual.FirstPathEntryIndex);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedJdtLsRuntime_WhenSourceAuthorityIsRevokedDuringLaunchRequestPreparation_DoesNotLaunch()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-revocation-state");
        string toolRoot = Path.Combine(fixture.SiblingPath, "controlled-jdt-revocation-tools");
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(toolRoot);
        string jdtScriptPath = Path.Combine(toolRoot, "jdtls.bat");
        string javaPath = Path.Combine(toolRoot, "java.exe");
        string cmdPath = Path.Combine(toolRoot, "cmd.exe");
        await File.WriteAllTextAsync(jdtScriptPath, "@echo off\r\n");
        await File.WriteAllTextAsync(javaPath, "controlled java executable fixture\n");
        await File.WriteAllTextAsync(cmdPath, "controlled command interpreter fixture\n");
        PreparedJdtPinnedFile jdtCommand = await ObservePinnedFileAsync(jdtScriptPath, 0);
        PreparedJdtPinnedFile javaCommand = await ObservePinnedFileAsync(javaPath, 0);
        PreparedJdtPinnedFile fixedInterpreter = await ObservePinnedFileAsync(cmdPath, null);

        Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, await lease.CheckCurrentAsync());
        Assert.Equal(PreparedSourceAuthorityStatus.Current,
            (await fixture.Authority.VerifyCurrentAsync(fixture.Approval)).Status);

        var pathEntries = new RevokingPathEntries(toolRoot, fixture.Authority.Revoke);
        var hostPolicy = new PreparedJdtHostPolicy(
            [toolRoot],
            [toolRoot],
            cmdPath,
            pathEntries);
        var resolver = new RecordingPreparedJdtCommandResolver(
            new PreparedJdtPinnedCommands(jdtCommand, javaCommand, fixedInterpreter));
        var launcher = new RecordingPreparedJdtProcessLauncher();
        var protocols = new RecordingPreparedJdtProtocolFactory();
        var runtime = new PreparedJdtLsRuntime(
            resolver,
            launcher,
            protocols,
            hostPolicy);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            MaximumResponseBytes: 64 * 1024);

        IPreparedJdtLsSession? session = null;
        PreparedJdtLsOpenFailureException? openFailure = null;
        try
        {
            try
            {
                session = await runtime.OpenAsync(lease, profile);
            }
            catch (PreparedJdtLsOpenFailureException exception)
            {
                openFailure = exception;
            }

            Assert.True(pathEntries.EnumerationCount > 0,
                "The controlled PATH source must revoke authority during request construction.");
            Assert.Equal(toolRoot, pathEntries.SingleValue);
            Assert.Equal(PreparedSourceAuthorityStatus.Revoked,
                (await fixture.Authority.VerifyCurrentAsync(fixture.Approval)).Status);
            Assert.Null(session);
            Assert.NotNull(openFailure);
            Assert.Equal(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable, openFailure.Status);
            Assert.True(openFailure.CleanupConfirmed);
            Assert.Equal(1, resolver.CallCount);
            Assert.Equal(0, launcher.CallCount);
            Assert.Equal(0, protocols.CallCount);

            foreach (string pinnedPath in new[] { jdtScriptPath, javaPath, cmdPath })
            {
                using var releasedPin = new FileStream(
                    pinnedPath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);
                Assert.True(releasedPin.CanRead);
                Assert.True(releasedPin.CanWrite);
            }
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedJdtLsRuntime_WhenLauncherReturnsProcessAfterCancellation_SettlesLateProcessWithoutConfirmingCleanup()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-late-launch-state");
        string toolRoot = Path.Combine(fixture.SiblingPath, "controlled-jdt-late-launch-tools");
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(toolRoot);
        string jdtScriptPath = Path.Combine(toolRoot, "jdtls.bat");
        string javaPath = Path.Combine(toolRoot, "java.exe");
        string cmdPath = Path.Combine(toolRoot, "cmd.exe");
        await File.WriteAllTextAsync(jdtScriptPath, "@echo off\r\n");
        await File.WriteAllTextAsync(javaPath, "controlled java executable fixture\n");
        await File.WriteAllTextAsync(cmdPath, "controlled command interpreter fixture\n");
        PreparedJdtPinnedFile jdtCommand = await ObservePinnedFileAsync(jdtScriptPath, 0);
        PreparedJdtPinnedFile javaCommand = await ObservePinnedFileAsync(javaPath, 0);
        PreparedJdtPinnedFile fixedInterpreter = await ObservePinnedFileAsync(cmdPath, null);
        var hostPolicy = new PreparedJdtHostPolicy(
            [toolRoot],
            [toolRoot],
            cmdPath,
            [toolRoot]);
        var resolver = new RecordingPreparedJdtCommandResolver(
            new PreparedJdtPinnedCommands(jdtCommand, javaCommand, fixedInterpreter));
        var process = new TrackedLatePreparedJdtProcess();
        var launcher = new LateReturningPreparedJdtProcessLauncher(process);
        var protocols = new RecordingPreparedJdtProtocolFactory();
        var runtime = new PreparedJdtLsRuntime(resolver, launcher, protocols, hostPolicy);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            MaximumResponseBytes: 64 * 1024);
        using var cancellation = new CancellationTokenSource();
        Task<IPreparedJdtLsSession>? openTask = null;

        try
        {
            Task<IPreparedJdtLsSession> opening = runtime.OpenAsync(lease, profile, cancellation.Token);
            openTask = opening;
            await launcher.LaunchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            PreparedJdtLsOpenFailureException failure = await Assert.ThrowsAsync<PreparedJdtLsOpenFailureException>(
                async () => await opening.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Equal(PreparedJavaNavigationOpenStatus.Cancelled, failure.Status);
            Assert.False(failure.CleanupConfirmed,
                "An unresolved launcher task may still return a process the runtime owns.");
            Assert.Equal(1, launcher.CallCount);
            Assert.Equal(0, protocols.CallCount);

            launcher.ReturnProcess();
            await process.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(process.KillCallCount > 0);
            Assert.True(process.WaitCallCount > 0);
            Assert.True(process.DisposeCallCount > 0);
        }
        finally
        {
            cancellation.Cancel();
            launcher.ReturnProcess();
            if (openTask is not null && !openTask.IsCompleted)
            {
                try
                {
                    await openTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (PreparedJdtLsOpenFailureException)
                {
                }
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PreparedJdtLsRuntime_WhenProtocolInitializationReturnsSessionAfterCancellation_SettlesLateSession()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        PreparedToolWorkspaceIssueResult issue = await new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            toolAuthorization).IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-late-protocol-state");
        string toolRoot = Path.Combine(fixture.SiblingPath, "controlled-jdt-late-protocol-tools");
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(toolRoot);
        string jdtScriptPath = Path.Combine(toolRoot, "jdtls.bat");
        string javaPath = Path.Combine(toolRoot, "java.exe");
        string cmdPath = Path.Combine(toolRoot, "cmd.exe");
        await File.WriteAllTextAsync(jdtScriptPath, "@echo off\r\n");
        await File.WriteAllTextAsync(javaPath, "controlled java executable fixture\n");
        await File.WriteAllTextAsync(cmdPath, "controlled command interpreter fixture\n");
        PreparedJdtPinnedFile jdtCommand = await ObservePinnedFileAsync(jdtScriptPath, 0);
        PreparedJdtPinnedFile javaCommand = await ObservePinnedFileAsync(javaPath, 0);
        PreparedJdtPinnedFile fixedInterpreter = await ObservePinnedFileAsync(cmdPath, null);
        var hostPolicy = new PreparedJdtHostPolicy(
            [toolRoot],
            [toolRoot],
            cmdPath,
            [toolRoot]);
        var resolver = new RecordingPreparedJdtCommandResolver(
            new PreparedJdtPinnedCommands(jdtCommand, javaCommand, fixedInterpreter));
        var process = new TrackedLatePreparedJdtProcess();
        var launcher = new ReturningTrackedPreparedJdtProcessLauncher(process);
        var protocols = new LateReturningPreparedJdtProtocolFactory();
        var runtime = new PreparedJdtLsRuntime(resolver, launcher, protocols, hostPolicy);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        using var cancellation = new CancellationTokenSource();
        Task<IPreparedJdtLsSession>? openTask = null;

        try
        {
            Task<IPreparedJdtLsSession> opening = runtime.OpenAsync(lease, profile, cancellation.Token);
            openTask = opening;
            await protocols.InitializeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            PreparedJdtLsOpenFailureException failure = await Assert.ThrowsAsync<PreparedJdtLsOpenFailureException>(
                async () => await opening.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Equal(PreparedJavaNavigationOpenStatus.Cancelled, failure.Status);
            Assert.False(failure.CleanupConfirmed,
                "The protocol initialization task is still responsible for any session it later returns.");
            Assert.True(process.KillCallCount > 0);
            Assert.True(process.WaitCallCount > 0);
            Assert.True(process.DisposeCallCount > 0);
            Assert.Equal(1, launcher.CallCount);
            Assert.Equal(1, protocols.CallCount);

            protocols.ReturnSession();
            await protocols.Session.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(protocols.Session.DisposeCallCount > 0);
            foreach (string pinnedPath in new[] { jdtScriptPath, javaPath, cmdPath })
            {
                await AssertPinnedFileReleasedAsync(pinnedPath);
            }
        }
        finally
        {
            cancellation.Cancel();
            protocols.ReturnSession();
            if (openTask is not null && !openTask.IsCompleted)
            {
                try
                {
                    await openTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (PreparedJdtLsOpenFailureException)
                {
                }
            }

            await lease.DisposeAsync();
        }
    }

    private static async Task<PreparedJdtPinnedFile> ObservePinnedFileAsync(
        string path,
        int? firstPathEntryIndex)
    {
        using Microsoft.Win32.SafeHandles.SafeFileHandle handle = File.OpenHandle(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        PreparedSourceTraversal.FileIdentity identity = Assert.IsType<PreparedSourceTraversal.FileIdentity>(
            PreparedSourceTraversal.ObserveFile(handle));
        string digest = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(path)));
        return new PreparedJdtPinnedFile(
            Path.GetFullPath(path),
            new PreparedJdtFileIdentity(
                identity.VolumeSerialNumber,
                identity.FileIndexHigh,
                identity.FileIndexLow),
            digest,
            firstPathEntryIndex);
    }

    private static bool IsPathWithin(string root, string candidate)
    {
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return Path.GetFullPath(candidate).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertPinnedFileReleasedAsync(string path)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            if (timeout.IsCancellationRequested)
            {
                throw new TimeoutException("The runtime did not release a pinned command file.");
            }

            try
            {
                using var releasedPin = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);
                Assert.True(releasedPin.CanRead);
                Assert.True(releasedPin.CanWrite);
                return;
            }
            catch (IOException exception) when (IsWindowsShareOrLockViolation(exception))
            {
                await Task.Yield();
            }
        }
    }

    private static bool IsWindowsShareOrLockViolation(IOException exception) =>
        (exception.HResult & 0xFFFF) is 32 or 33;

    private static void AssertHostPolicyValues(
        PreparedJdtHostPolicy expected,
        PreparedJdtHostPolicy actual)
    {
        Assert.Equal(expected.ApprovedExecutableDirectories.ToArray(),
            actual.ApprovedExecutableDirectories.ToArray());
        Assert.Equal(expected.DeclaredRuntimeReadRoots.ToArray(),
            actual.DeclaredRuntimeReadRoots.ToArray());
        Assert.Equal(expected.FixedCmdAbsolutePath, actual.FixedCmdAbsolutePath);
        Assert.Equal(expected.FixedChildPathEntries.ToArray(),
            actual.FixedChildPathEntries.ToArray());
    }

    private static void AssertFixedOfflineSettings(IReadOnlyDictionary<string, object> settings)
    {
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(settings));
        JsonElement java = json.RootElement.GetProperty("java");
        Assert.False(java.GetProperty("import").GetProperty("maven").GetProperty("enabled").GetBoolean());
        Assert.False(java.GetProperty("import").GetProperty("gradle").GetProperty("enabled").GetBoolean());
        Assert.True(java.GetProperty("import").GetProperty("maven").GetProperty("offline").GetProperty("enabled").GetBoolean());
        Assert.True(java.GetProperty("import").GetProperty("gradle").GetProperty("offline").GetProperty("enabled").GetBoolean());
        Assert.False(java.GetProperty("import").GetProperty("gradle").GetProperty("wrapper").GetProperty("enabled").GetBoolean());
        Assert.False(java.GetProperty("autobuild").GetProperty("enabled").GetBoolean());
        Assert.Equal("disabled", java.GetProperty("configuration").GetProperty("updateBuildConfiguration").GetString());
        Assert.False(java.GetProperty("maven").GetProperty("downloadSources").GetBoolean());
        Assert.False(java.GetProperty("maven").GetProperty("updateSnapshots").GetBoolean());
    }

    private static void AssertFixedOfflineWireSettings(JsonElement settings)
    {
        JsonElement java = settings.GetProperty("java");
        Assert.False(java.GetProperty("import").GetProperty("maven").GetProperty("enabled").GetBoolean());
        Assert.False(java.GetProperty("import").GetProperty("gradle").GetProperty("enabled").GetBoolean());
        Assert.True(java.GetProperty("import").GetProperty("maven").GetProperty("offline").GetProperty("enabled").GetBoolean());
        Assert.True(java.GetProperty("import").GetProperty("gradle").GetProperty("offline").GetProperty("enabled").GetBoolean());
        Assert.False(java.GetProperty("import").GetProperty("gradle").GetProperty("wrapper").GetProperty("enabled").GetBoolean());
        Assert.False(java.GetProperty("autobuild").GetProperty("enabled").GetBoolean());
        Assert.Equal("disabled", java.GetProperty("configuration").GetProperty("updateBuildConfiguration").GetString());
        Assert.False(java.GetProperty("maven").GetProperty("downloadSources").GetBoolean());
        Assert.False(java.GetProperty("maven").GetProperty("updateSnapshots").GetBoolean());
    }

    private static bool AreEquivalentJsonValues(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            return false;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                JsonProperty[] expectedProperties = expected.EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .ToArray();
                JsonProperty[] actualProperties = actual.EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .ToArray();
                if (expectedProperties.Length != actualProperties.Length)
                {
                    return false;
                }

                for (int index = 0; index < expectedProperties.Length; index++)
                {
                    if (!string.Equals(
                            expectedProperties[index].Name,
                            actualProperties[index].Name,
                            StringComparison.Ordinal) ||
                        !AreEquivalentJsonValues(
                            expectedProperties[index].Value,
                            actualProperties[index].Value))
                    {
                        return false;
                    }
                }

                return true;

            case JsonValueKind.Array:
                if (expected.GetArrayLength() != actual.GetArrayLength())
                {
                    return false;
                }

                for (int index = 0; index < expected.GetArrayLength(); index++)
                {
                    if (!AreEquivalentJsonValues(expected[index], actual[index]))
                    {
                        return false;
                    }
                }

                return true;

            case JsonValueKind.String:
                return string.Equals(expected.GetString(), actual.GetString(), StringComparison.Ordinal);

            case JsonValueKind.Number:
                return string.Equals(expected.GetRawText(), actual.GetRawText(), StringComparison.Ordinal);

            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;

            default:
                return false;
        }
    }

    private sealed class RecordingPreparedJdtCommandResolver(PreparedJdtPinnedCommands commands)
        : IPreparedJdtCommandResolver
    {
        public int CallCount { get; private set; }

        public PreparedJdtCommandRequest? LastRequest { get; private set; }

        public Task<PreparedJdtCommandResolution> ResolveAsync(
            PreparedJdtCommandRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            return Task.FromResult(new PreparedJdtCommandResolution(
                PreparedJdtCommandResolutionStatus.Pinned,
                commands));
        }
    }

    private sealed class RevokingPathEntries(
        string singleValue,
        Action revokeAuthority) : IReadOnlyList<string>
    {
        private int _enumerationCount;

        public string SingleValue { get; } = singleValue;

        public int EnumerationCount => Volatile.Read(ref _enumerationCount);

        public int Count => 1;

        public string this[int index] => index == 0
            ? SingleValue
            : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<string> GetEnumerator()
        {
            Interlocked.Increment(ref _enumerationCount);
            revokeAuthority();
            yield return SingleValue;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class RecordingPreparedJdtProcessLauncher : IPreparedJdtProcessLauncher
    {
        public int CallCount { get; private set; }

        public PreparedJdtLaunchRequest? LastRequest { get; private set; }

        public Task<IJdtLsProcess> LaunchAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJdtLaunchRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            return Task.FromResult<IJdtLsProcess>(new RecordingPreparedJdtProcess());
        }
    }

    private sealed class LateReturningPreparedJdtProcessLauncher(
        IJdtLsProcess lateProcess) : IPreparedJdtProcessLauncher
    {
        private readonly TaskCompletionSource<IJdtLsProcess> _returnProcess =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> LaunchStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public Task<IJdtLsProcess> LaunchAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJdtLaunchRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LaunchStarted.TrySetResult(true);
            return _returnProcess.Task;
        }

        public void ReturnProcess() => _returnProcess.TrySetResult(lateProcess);
    }

    private sealed class ReturningTrackedPreparedJdtProcessLauncher(
        IJdtLsProcess process) : IPreparedJdtProcessLauncher
    {
        public int CallCount { get; private set; }

        public Task<IJdtLsProcess> LaunchAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJdtLaunchRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(process);
        }
    }

    private sealed class LateReturningPreparedJdtProtocolFactory : IPreparedJdtProtocolFactory
    {
        private readonly TaskCompletionSource<IPreparedJdtLsSession> _returnSession =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TrackedLatePreparedJdtLsSession Session { get; } = new();

        public TaskCompletionSource<bool> InitializeStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public Task<IPreparedJdtLsSession> InitializeAsync(
            IJdtLsProcess process,
            PreparedJdtInitializeRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            InitializeStarted.TrySetResult(true);
            return _returnSession.Task;
        }

        public void ReturnSession() => _returnSession.TrySetResult(Session);
    }

    private sealed class TrackedLatePreparedJdtLsSession : IPreparedJdtLsSession
    {
        private int _disposeCallCount;

        public TaskCompletionSource<bool> Disposed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DisposeCallCount => Volatile.Read(ref _disposeCallCount);

        public Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PreparedJdtLsRawReply([], false, null));

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCallCount);
            Disposed.TrySetResult(true);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TrackedLatePreparedJdtProcess : IJdtLsProcess
    {
        private int _killCallCount;
        private int _waitCallCount;
        private int _disposeCallCount;

        public TaskCompletionSource<bool> Disposed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int KillCallCount => Volatile.Read(ref _killCallCount);

        public int WaitCallCount => Volatile.Read(ref _waitCallCount);

        public int DisposeCallCount => Volatile.Read(ref _disposeCallCount);

        public Stream StandardInput => Stream.Null;

        public Stream StandardOutput => Stream.Null;

        public Task<string> ReadBoundedStandardErrorAsync(
            int byteLimit,
            CancellationToken cancellationToken) => Task.FromResult(string.Empty);

        public Task<int> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _waitCallCount);
            return Task.FromResult(0);
        }

        public Task KillTreeAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _killCallCount);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCallCount);
            Disposed.TrySetResult(true);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingPreparedJdtProtocolFactory : IPreparedJdtProtocolFactory
    {
        public RecordingPreparedJdtLsSession Session { get; } = new();

        public int CallCount { get; private set; }

        public PreparedJdtInitializeRequest? LastRequest { get; private set; }

        public Task<IPreparedJdtLsSession> InitializeAsync(
            IJdtLsProcess process,
            PreparedJdtInitializeRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            return Task.FromResult<IPreparedJdtLsSession>(Session);
        }
    }

    private sealed class RecordingPreparedJdtProcess : IJdtLsProcess
    {
        public Stream StandardInput => Stream.Null;

        public Stream StandardOutput => Stream.Null;

        public Task<string> ReadBoundedStandardErrorAsync(int byteLimit, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task<int> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task KillTreeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class PreparedJdtStdioPeerProcess : IJdtLsProcess
    {
        private const int MaximumFrameBytes = 64 * 1024;
        private readonly Pipe _clientToPeer = new();
        private readonly Pipe _peerToClient = new();
        private readonly Stream _clientInput;
        private readonly Stream _clientOutput;
        private readonly Stream _peerInput;
        private readonly Stream _peerOutput;
        private readonly ConcurrentQueue<JsonElement> _observedMessages = new();
        private readonly TaskCompletionSource<bool> _handshakeObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _definitionObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _referencesObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _processExited =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task _peerTask;
        private int _disposed;

        internal PreparedJdtStdioPeerProcess()
        {
            _clientInput = _clientToPeer.Writer.AsStream(leaveOpen: true);
            _clientOutput = _peerToClient.Reader.AsStream(leaveOpen: true);
            _peerInput = _clientToPeer.Reader.AsStream(leaveOpen: true);
            _peerOutput = _peerToClient.Writer.AsStream(leaveOpen: true);
            _peerTask = RunPeerAsync();
        }

        public Stream StandardInput => _clientInput;

        public Stream StandardOutput => _clientOutput;

        internal TaskCompletionSource<bool> HandshakeObserved => _handshakeObserved;

        internal TaskCompletionSource<bool> DefinitionObserved => _definitionObserved;

        internal TaskCompletionSource<bool> ReferencesObserved => _referencesObserved;

        internal ConcurrentQueue<JsonElement> ObservedMessages => _observedMessages;

        internal JsonElement? DefinitionReply { get; set; }

        internal JsonElement? ReferencesReply { get; set; }

        public Task<string> ReadBoundedStandardErrorAsync(
            int byteLimit,
            CancellationToken cancellationToken) => Task.FromResult(string.Empty);

        public async Task<int> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            await _processExited.Task.WaitAsync(timeout, cancellationToken);
            return 0;
        }

        public async Task KillTreeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _processExited.TrySetResult(true);
            await CompleteWriterAsync(_clientToPeer.Writer);
            await CompleteWriterAsync(_peerToClient.Writer);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _processExited.TrySetResult(true);
            await CompleteWriterAsync(_clientToPeer.Writer);
            await CompleteWriterAsync(_peerToClient.Writer);
            await _peerTask.WaitAsync(TimeSpan.FromSeconds(2));
            await _clientToPeer.Reader.CompleteAsync();
            await _peerToClient.Reader.CompleteAsync();
        }

        private async Task RunPeerAsync()
        {
            while (await ReadFrameAsync(_peerInput) is { } frame)
            {
                using JsonDocument document = JsonDocument.Parse(frame);
                JsonElement message = document.RootElement.Clone();
                _observedMessages.Enqueue(message);
                string? method = message.TryGetProperty("method", out JsonElement methodValue)
                    ? methodValue.GetString()
                    : null;

                if (string.Equals(method, "workspace/didChangeConfiguration", StringComparison.Ordinal))
                {
                    _handshakeObserved.TrySetResult(true);
                }

                if (string.Equals(method, "textDocument/definition", StringComparison.Ordinal))
                {
                    _definitionObserved.TrySetResult(true);
                }

                if (string.Equals(method, "textDocument/references", StringComparison.Ordinal))
                {
                    _referencesObserved.TrySetResult(true);
                }

                if (string.Equals(method, "exit", StringComparison.Ordinal))
                {
                    _processExited.TrySetResult(true);
                    return;
                }

                if (message.TryGetProperty("id", out JsonElement requestId))
                {
                    object? result = string.Equals(method, "initialize", StringComparison.Ordinal)
                        ? new { capabilities = new { } }
                        : string.Equals(method, "textDocument/definition", StringComparison.Ordinal) &&
                          DefinitionReply is JsonElement definitionReply
                            ? definitionReply.Clone()
                            : string.Equals(method, "textDocument/references", StringComparison.Ordinal) &&
                              ReferencesReply is JsonElement referencesReply
                                ? referencesReply.Clone()
                            : null;
                    var response = new Dictionary<string, object?>
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = requestId.Clone(),
                        ["result"] = result,
                    };
                    await WriteFrameAsync(_peerOutput, JsonSerializer.SerializeToUtf8Bytes(response));
                }
            }
        }

        private static async Task<byte[]?> ReadFrameAsync(Stream stream)
        {
            var header = new List<byte>();
            byte[] next = new byte[1];
            while (true)
            {
                int read = await stream.ReadAsync(next.AsMemory());
                if (read == 0)
                {
                    if (header.Count == 0)
                    {
                        return null;
                    }

                    throw new EndOfStreamException("Prepared stdio header ended before its frame boundary.");
                }

                header.Add(next[0]);
                if (header.Count > 4096)
                {
                    throw new InvalidDataException("Prepared stdio header exceeded its test bound.");
                }

                int count = header.Count;
                if (count >= 4 && header[count - 4] == (byte)'\r' && header[count - 3] == (byte)'\n' &&
                    header[count - 2] == (byte)'\r' && header[count - 1] == (byte)'\n')
                {
                    break;
                }
            }

            string headerText = Encoding.ASCII.GetString(header.ToArray());
            string? lengthHeader = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            if (lengthHeader is null ||
                !int.TryParse(lengthHeader[(lengthHeader.IndexOf(':') + 1)..].Trim(), out int length) ||
                length is <= 0 or > MaximumFrameBytes)
            {
                throw new InvalidDataException("Prepared stdio frame length was missing or outside its test bound.");
            }

            byte[] body = new byte[length];
            int offset = 0;
            while (offset < body.Length)
            {
                int read = await stream.ReadAsync(body.AsMemory(offset));
                if (read == 0)
                {
                    throw new EndOfStreamException("Prepared stdio body ended before Content-Length bytes were read.");
                }

                offset += read;
            }

            return body;
        }

        private static async Task WriteFrameAsync(Stream stream, byte[] body)
        {
            byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
            await stream.WriteAsync(header.AsMemory());
            await stream.WriteAsync(body.AsMemory());
            await stream.FlushAsync();
        }

        private static async Task CompleteWriterAsync(PipeWriter writer)
        {
            try
            {
                await writer.CompleteAsync();
            }
            catch (InvalidOperationException)
            {
                // The peer or process cleanup already completed this owned pipe end.
            }
        }
    }

    private sealed class ControlledPreparedJdtLsRuntime : IPreparedJdtLsRuntime
    {
        public RecordingPreparedJdtLsSession Session { get; } = new();

        public int OpenCallCount { get; private set; }

        public PreparedToolWorkspaceLease? LastWorkspaceLease { get; private set; }

        public PreparedJavaNavigationProfile? LastProfile { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            LastWorkspaceLease = workspaceLease;
            LastProfile = profile;
            LastCancellationToken = cancellationToken;
            return Task.FromResult<IPreparedJdtLsSession>(Session);
        }
    }

    private sealed class StalledDisposePreparedJdtLsRuntime : IPreparedJdtLsRuntime
    {
        internal StalledDisposePreparedJdtLsSession Session { get; } = new();

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IPreparedJdtLsSession>(Session);
        }
    }

    private sealed class StalledDisposePreparedJdtLsSession : IPreparedJdtLsSession
    {
        private readonly TaskCompletionSource<bool> _releaseDispose =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<bool> DisposeStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<bool> DisposeCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PreparedJdtLsRawReply([], false, null));
        }

        public ValueTask DisposeAsync()
        {
            DisposeStarted.TrySetResult(true);
            return new ValueTask(CompleteDisposeAsync());
        }

        internal void ReleaseDispose() => _releaseDispose.TrySetResult(true);

        private async Task CompleteDisposeAsync()
        {
            await _releaseDispose.Task;
            DisposeCompleted.TrySetResult(true);
        }
    }

    private sealed class BlockingNavigationRuntime : IPreparedJdtLsRuntime
    {
        public BlockingNavigationSession Session { get; } = new();

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IPreparedJdtLsSession>(Session);
        }
    }

    private sealed class BlockingNavigationSession : IPreparedJdtLsSession
    {
        private int _disposeCallCount;
        private int _navigationCallCount;

        public TaskCompletionSource<bool> NavigationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseNavigation { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DisposeCallCount => Volatile.Read(ref _disposeCallCount);

        public int NavigationCallCount => Volatile.Read(ref _navigationCallCount);

        public async Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _navigationCallCount);
            using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
                () => CancellationObserved.TrySetResult(true));
            NavigationStarted.TrySetResult(true);
            try
            {
                await ReleaseNavigation.Task.WaitAsync(cancellationToken);
                return new PreparedJdtLsRawReply([], false, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved.TrySetResult(true);
                throw;
            }
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCallCount);
            ReleaseNavigation.TrySetResult(true);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class UnsettledNavigationRuntime(PreparedJdtLsRawReply lateReply) : IPreparedJdtLsRuntime
    {
        public UnsettledNavigationSession Session { get; } = new(lateReply);

        public int OpenCallCount { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            return Task.FromResult<IPreparedJdtLsSession>(Session);
        }
    }

    private sealed class UnsettledNavigationSession(PreparedJdtLsRawReply lateReply) : IPreparedJdtLsSession
    {
        private int _disposeCallCount;

        public TaskCompletionSource<bool> NavigationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> NavigationSettled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseNavigation { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseDispose { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DisposeCallCount => Volatile.Read(ref _disposeCallCount);

        public async Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            NavigationStarted.TrySetResult(true);
            try
            {
                // Deliberately model a protocol call that ignores request cancellation.
                await ReleaseNavigation.Task;
                return lateReply;
            }
            finally
            {
                NavigationSettled.TrySetResult(true);
            }
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCallCount);
            return new ValueTask(ReleaseDispose.Task);
        }
    }

    private sealed class RevokingSourceOnceRuntime(
        ControlledPreparedSourceAuthority sourceAuthority) : IPreparedJdtLsRuntime
    {
        public List<RecordingPreparedJdtLsSession> Sessions { get; } = [];

        public int OpenCallCount { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            if (OpenCallCount == 1)
            {
                sourceAuthority.Revoke();
            }

            var session = new RecordingPreparedJdtLsSession();
            Sessions.Add(session);
            return Task.FromResult<IPreparedJdtLsSession>(session);
        }
    }

    private sealed class UnconfirmedCleanupRuntime : IPreparedJdtLsRuntime
    {
        public int OpenCallCount { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            if (OpenCallCount == 1)
            {
                throw new PreparedJdtLsOpenFailureException(
                    PreparedJavaNavigationOpenStatus.ReadinessFailed,
                    cleanupConfirmed: false);
            }

            throw new InvalidOperationException("Unexpected runtime retry after unconfirmed cleanup.");
        }
    }

    private sealed class StalledReadinessRuntime : IPreparedJdtLsRuntime
    {
        private readonly TaskCompletionSource<bool> _releaseReadinessForTest =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> OpenStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool StartupResourceActive { get; private set; }

        public bool DeadlineCancellationObserved { get; private set; }

        public bool CleanupConfirmed { get; private set; }

        public int OpenCallCount { get; private set; }

        public async Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            OpenCallCount++;
            StartupResourceActive = true;
            OpenStarted.TrySetResult(true);

            try
            {
                await _releaseReadinessForTest.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                DeadlineCancellationObserved = true;
                ConfirmCleanup();
                throw new PreparedJdtLsOpenFailureException(
                    PreparedJavaNavigationOpenStatus.DeadlineExceeded,
                    cleanupConfirmed: true);
            }

            ConfirmCleanup();
            throw new PreparedJdtLsOpenFailureException(
                PreparedJavaNavigationOpenStatus.ReadinessFailed,
                cleanupConfirmed: true);
        }

        public void ReleaseReadinessForTest() => _releaseReadinessForTest.TrySetResult(true);

        private void ConfirmCleanup()
        {
            StartupResourceActive = false;
            CleanupConfirmed = true;
        }
    }

    private sealed class ProtocolFailureThenSuccessRuntime : IPreparedJdtLsRuntime
    {
        public ProtocolFailureNavigationSession FailingSession { get; } = new();

        public RecordingPreparedJdtLsSession RetrySession { get; } = new();

        public int OpenCallCount { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            return Task.FromResult<IPreparedJdtLsSession>(OpenCallCount == 1
                ? FailingSession
                : RetrySession);
        }
    }

    private sealed class ProtocolFailureNavigationSession : IPreparedJdtLsSession
    {
        public const string FailureMessage = "controlled protocol failure detail";

        private readonly TaskCompletionSource<bool> _releaseDispose =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _navigateCallCount;
        private int _disposeCallCount;

        public TaskCompletionSource<bool> DisposeStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> DisposeSettled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int NavigateCallCount => Volatile.Read(ref _navigateCallCount);

        public int DisposeCallCount => Volatile.Read(ref _disposeCallCount);

        public Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _navigateCallCount);
            return Task.FromException<PreparedJdtLsRawReply>(
                new InvalidOperationException(FailureMessage));
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCallCount);
            DisposeStarted.TrySetResult(true);
            return new ValueTask(SettleDisposeAsync());
        }

        public void ReleaseDisposeForTest() => _releaseDispose.TrySetResult(true);

        private async Task SettleDisposeAsync()
        {
            await _releaseDispose.Task;
            DisposeSettled.TrySetResult(true);
        }
    }

    private sealed class NullReplyThenSuccessRuntime : IPreparedJdtLsRuntime
    {
        public NullReplyNavigationSession MalformedReplySession { get; } = new();

        public RecordingPreparedJdtLsSession RetrySession { get; } = new();

        public int OpenCallCount { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            return Task.FromResult<IPreparedJdtLsSession>(OpenCallCount == 1
                ? MalformedReplySession
                : RetrySession);
        }
    }

    private sealed class NullReplyNavigationSession : IPreparedJdtLsSession
    {
        private readonly TaskCompletionSource<bool> _releaseDispose =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _navigateCallCount;
        private int _disposeCallCount;

        public TaskCompletionSource<bool> DisposeStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> DisposeSettled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int NavigateCallCount => Volatile.Read(ref _navigateCallCount);

        public Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _navigateCallCount);
            return Task.FromResult<PreparedJdtLsRawReply>(null!);
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCallCount);
            DisposeStarted.TrySetResult(true);
            return new ValueTask(SettleDisposeAsync());
        }

        public void ReleaseDisposeForTest() => _releaseDispose.TrySetResult(true);

        private async Task SettleDisposeAsync()
        {
            await _releaseDispose.Task;
            DisposeSettled.TrySetResult(true);
        }
    }

    private sealed class LateReturningReadinessRuntime : IPreparedJdtLsRuntime
    {
        private readonly TaskCompletionSource<bool> _returnSession =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RecordingPreparedJdtLsSession Session { get; } = new();

        public TaskCompletionSource<bool> OpenStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> InitializationDeadlineObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int OpenCallCount { get; private set; }

        public async Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            OpenCallCount++;
            OpenStarted.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                InitializationDeadlineObserved.TrySetResult(true);
            }

            await _returnSession.Task;
            return Session;
        }

        public void ReturnSessionAfterDeadline() => _returnSession.TrySetResult(true);
    }

    private sealed class LateReturningBlockedDisposeRuntime : IPreparedJdtLsRuntime
    {
        private readonly TaskCompletionSource<bool> _returnSession =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingDisposeNavigationSession Session { get; } = new();

        public TaskCompletionSource<bool> OpenStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> InitializationDeadlineObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int OpenCallCount { get; private set; }

        public async Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            OpenCallCount++;
            if (OpenCallCount > 1)
            {
                throw new PreparedJdtLsOpenFailureException(
                    PreparedJavaNavigationOpenStatus.ReadinessFailed,
                    cleanupConfirmed: true);
            }

            OpenStarted.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                InitializationDeadlineObserved.TrySetResult(true);
            }

            await _returnSession.Task;
            return Session;
        }

        public void ReturnSessionAfterDeadline() => _returnSession.TrySetResult(true);
    }

    private sealed class BlockingDisposeNavigationSession : IPreparedJdtLsSession
    {
        private readonly TaskCompletionSource<bool> _releaseDispose =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _disposeCallCount;

        public TaskCompletionSource<bool> DisposeStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> DisposeSettled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DisposeCallCount => Volatile.Read(ref _disposeCallCount);

        public Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PreparedJdtLsRawReply([], false, null));

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCallCount);
            DisposeStarted.TrySetResult(true);
            return new ValueTask(SettleDisposeAsync());
        }

        public void ReleaseDisposeForTest() => _releaseDispose.TrySetResult(true);

        private async Task SettleDisposeAsync()
        {
            await _releaseDispose.Task;
            DisposeSettled.TrySetResult(true);
        }
    }

    private sealed class ConfirmedCleanupThenSuccessRuntime : IPreparedJdtLsRuntime
    {
        public int OpenCallCount { get; private set; }

        public List<RecordingPreparedJdtLsSession> Sessions { get; } = [];

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            if (OpenCallCount == 1)
            {
                throw new PreparedJdtLsOpenFailureException(
                    PreparedJavaNavigationOpenStatus.ReadinessFailed,
                    cleanupConfirmed: true);
            }

            var session = new RecordingPreparedJdtLsSession();
            Sessions.Add(session);
            return Task.FromResult<IPreparedJdtLsSession>(session);
        }
    }

    private sealed class UnexpectedExceptionRuntime : IPreparedJdtLsRuntime
    {
        public int OpenCallCount { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            if (OpenCallCount == 1)
            {
                throw new InvalidOperationException("Controlled runtime open failed without cleanup attestation.");
            }

            return Task.FromResult<IPreparedJdtLsSession>(new RecordingPreparedJdtLsSession());
        }
    }

    private sealed class ReservedSlotRuntime : IPreparedJdtLsRuntime
    {
        private readonly TaskCompletionSource<bool> _firstOpenStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _openCallCount;

        public Task FirstOpenStarted => _firstOpenStarted.Task;

        public TaskCompletionSource<bool> ReleaseFirstOpen { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int OpenCallCount => Volatile.Read(ref _openCallCount);

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            int callNumber = Interlocked.Increment(ref _openCallCount);
            if (callNumber == 1)
            {
                _firstOpenStarted.TrySetResult(true);
                return ReturnFirstSessionAfterReleaseAsync(cancellationToken);
            }

            return Task.FromResult<IPreparedJdtLsSession>(new RecordingPreparedJdtLsSession());
        }

        private async Task<IPreparedJdtLsSession> ReturnFirstSessionAfterReleaseAsync(
            CancellationToken cancellationToken)
        {
            await ReleaseFirstOpen.Task.WaitAsync(cancellationToken);
            return new RecordingPreparedJdtLsSession();
        }
    }

    private sealed class RecordingPreparedJdtLsSession : IPreparedJdtLsSession
    {
        public PreparedJdtLsRawReply Reply { get; set; } = new([], false, null);

        public Action? AfterNavigate { get; set; }

        public int NavigateCallCount { get; private set; }

        public PreparedJavaNavigationOperation? LastOperation { get; private set; }

        public PreparedJavaNavigationRequest? LastRequest { get; private set; }

        public int DisposeCallCount { get; private set; }

        public Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NavigateCallCount++;
            LastOperation = operation;
            LastRequest = request;
            AfterNavigate?.Invoke();
            return Task.FromResult(Reply);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ControlledPreparedToolUseAuthorizer(
        PreparedSourceApproval currentApproval,
        PreparedToolPurpose currentPurpose) : IPreparedToolUseAuthorizer
    {
        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval approval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreparedToolUseAuthorityStatus status = approval == currentApproval &&
                toolPurpose == currentPurpose
                    ? PreparedToolUseAuthorityStatus.Current
                    : PreparedToolUseAuthorityStatus.Denied;
            return Task.FromResult(new PreparedToolUseAuthorityResult(status));
        }
    }

    private sealed class JdtOnlyRegistryHostVerifier(
        PreparedCodeGraphWorkspaceOwnership expectedBinding) : IPreparedManagedWorkspaceHostVerifier
    {
        internal JdtOnlyRegistryHostHold Hold { get; } = new(expectedBinding);

        public Task<PreparedManagedWorkspaceHostObservation> ObserveAsync(
            PreparedSourceGrantRequest applicationHeldSourceGrant,
            RetainedSourceCapturePolicy sourceStoragePolicy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PreparedManagedWorkspaceHostObservation(
                PreparedManagedWorkspaceHostStatus.Observed,
                expectedBinding.RootObjectIdentity,
                expectedBinding.WorkspaceOwnerIdentity));
        }

        public Task<PreparedManagedWorkspaceHostHoldResult> VerifyAndHoldAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            RetainedSourceCapturePolicy sourceStoragePolicy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(binding == expectedBinding
                ? new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Protected, Hold)
                : new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Denied, null));
        }
    }

    private sealed class JdtOnlyRegistryHostHold(
        PreparedCodeGraphWorkspaceOwnership expectedBinding) : IPreparedManagedWorkspaceHostHold
    {
        private bool _disposed;

        public ValueTask<PreparedManagedWorkspaceHostStatus> CheckCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(!_disposed && binding == expectedBinding
                ? PreparedManagedWorkspaceHostStatus.Protected
                : PreparedManagedWorkspaceHostStatus.Unavailable);
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class JdtOnlyPreviousGenerationVerifier : IPreparedWorkspacePreviousGenerationVerifier
    {
        public ValueTask<PreparedWorkspaceSettlementStatus> VerifySettledAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(PreparedWorkspaceSettlementStatus.Settled);
        }
    }

    private sealed class JdtAndCodeGraphToolUseAuthorizer(PreparedSourceApproval expectedApproval)
        : IPreparedToolUseAuthorizer
    {
        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval approval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool allowed = approval == expectedApproval &&
                toolPurpose is PreparedToolPurpose.JdtNavigation or PreparedToolPurpose.CodeGraphPreparation;
            return Task.FromResult(new PreparedToolUseAuthorityResult(
                allowed ? PreparedToolUseAuthorityStatus.Current : PreparedToolUseAuthorityStatus.Denied));
        }
    }

    private sealed class ProgressiveRegistryHostVerifier(
        PreparedCodeGraphWorkspaceOwnership absentBinding) : IPreparedManagedWorkspaceHostVerifier
    {
        internal ProgressiveRegistryHostHold Hold { get; } = new(absentBinding);

        public Task<PreparedManagedWorkspaceHostObservation> ObserveAsync(
            PreparedSourceGrantRequest applicationHeldSourceGrant,
            RetainedSourceCapturePolicy sourceStoragePolicy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PreparedManagedWorkspaceHostObservation(
                PreparedManagedWorkspaceHostStatus.Observed,
                absentBinding.RootObjectIdentity,
                absentBinding.WorkspaceOwnerIdentity));
        }

        public Task<PreparedManagedWorkspaceHostHoldResult> VerifyAndHoldAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            RetainedSourceCapturePolicy sourceStoragePolicy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(binding == absentBinding
                ? new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Protected, Hold)
                : new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Denied, null));
        }
    }

    private sealed class ProgressiveRegistryHostHold(
        PreparedCodeGraphWorkspaceOwnership absentBinding) : IPreparedManagedWorkspaceHostHold
    {
        private bool _disposed;

        public ValueTask<PreparedManagedWorkspaceHostStatus> CheckCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(!_disposed && IsSameImmutableKey(absentBinding, binding) &&
                IsAllowedSameProcessProgression(absentBinding, binding)
                    ? PreparedManagedWorkspaceHostStatus.Protected
                    : PreparedManagedWorkspaceHostStatus.Unavailable);
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }

        private static bool IsSameImmutableKey(
            PreparedCodeGraphWorkspaceOwnership expected,
            PreparedCodeGraphWorkspaceOwnership actual) =>
            expected.OwnershipId == actual.OwnershipId &&
            expected.Approval == actual.Approval &&
            string.Equals(expected.PhysicalRootPath, actual.PhysicalRootPath, StringComparison.OrdinalIgnoreCase) &&
            expected.RootObjectIdentity == actual.RootObjectIdentity &&
            expected.WorkspaceOwnerIdentity == actual.WorkspaceOwnerIdentity;

        private static bool IsAllowedSameProcessProgression(
            PreparedCodeGraphWorkspaceOwnership absent,
            PreparedCodeGraphWorkspaceOwnership current) =>
            (current.Phase, current.Version, current.StateEntryObjectIdentity) switch
            {
                (PreparedCodeGraphOwnershipPhase.Absent, var version, null) => version == absent.Version,
                (PreparedCodeGraphOwnershipPhase.Creating, var version, null) =>
                    absent.Version < long.MaxValue && version == absent.Version + 1,
                (PreparedCodeGraphOwnershipPhase.Ready, var version, not null) =>
                    absent.Version < long.MaxValue - 1 && version == absent.Version + 2,
                _ => false,
            };
    }

    private sealed class OwnedStateTransitionPreparedJdtLsRuntime(
        Func<CancellationToken, Task> commitOwnedState,
        PreparedJdtLsRawReply reply) : IPreparedJdtLsRuntime
    {
        internal OwnedStateTransitionPreparedJdtLsSession Session { get; } =
            new(commitOwnedState, reply);

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IPreparedJdtLsSession>(Session);
        }
    }

    private sealed class OwnedStateTransitionPreparedJdtLsSession(
        Func<CancellationToken, Task> commitOwnedState,
        PreparedJdtLsRawReply reply) : IPreparedJdtLsSession
    {
        public int NavigateCallCount { get; private set; }

        public int DisposeCallCount { get; private set; }

        public async Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NavigateCallCount++;
            await commitOwnedState(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return reply;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MatchingNavigationOwnershipVerifier(
        PreparedCodeGraphWorkspaceOwnership expected) : IPreparedCodeGraphWorkspaceOwnershipVerifier
    {
        public ValueTask<bool> VerifyCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership ownership,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ownership == expected);
        }

        public ValueTask<bool> VerifyDispositionReadyAsync(
            PreparedCodeGraphWorkspaceOwnership ownership,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(false);
        }
    }

    private sealed class AvailabilityChangingOwnershipStore(
        PreparedCodeGraphWorkspaceOwnership record) : IPreparedCodeGraphOwnershipStore
    {
        private int _unavailable;
        private PreparedCodeGraphWorkspaceOwnership _record = record;

        public PreparedCodeGraphOwnershipLoadStatus CurrentStatus =>
            Volatile.Read(ref _unavailable) == 0
                ? PreparedCodeGraphOwnershipLoadStatus.Found
                : PreparedCodeGraphOwnershipLoadStatus.Unavailable;

        public void MakeUnavailable() => Interlocked.Exchange(ref _unavailable, 1);

        public void AdvanceReadyVersion()
        {
            PreparedCodeGraphWorkspaceOwnership current = Volatile.Read(ref _record);
            Volatile.Write(ref _record, current with { Version = checked(current.Version + 1) });
        }

        public Task<PreparedCodeGraphOwnershipLoadResult> LoadAsync(
            PreparedCodeGraphWorkspaceOwnership expectedBinding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreparedCodeGraphOwnershipLoadStatus status = CurrentStatus;
            PreparedCodeGraphWorkspaceOwnership current = Volatile.Read(ref _record);
            return Task.FromResult(new PreparedCodeGraphOwnershipLoadResult(
                status,
                status == PreparedCodeGraphOwnershipLoadStatus.Found ? current : null));
        }

        public Task<PreparedCodeGraphOwnershipTransitionResult> BeginCreateAsync(
            PreparedCodeGraphWorkspaceOwnership absent,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The navigation drift fixture does not provision state.");

        public Task<PreparedCodeGraphOwnershipTransitionResult> CommitCreatedAsync(
            PreparedCodeGraphWorkspaceOwnership creating,
            PreparedToolDirectoryObjectIdentity createdEntry,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The navigation drift fixture does not provision state.");

        public Task<PreparedCodeGraphOwnershipTransitionResult> BeginDispositionAsync(
            PreparedCodeGraphWorkspaceOwnership ready,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The navigation drift fixture does not dispose state.");

        public Task<PreparedCodeGraphOwnershipTransitionResult> CommitRemovedAsync(
            PreparedCodeGraphWorkspaceOwnership disposing,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The navigation drift fixture does not dispose state.");

        public Task<PreparedCodeGraphOwnershipTransitionResult> QuarantineAsync(
            PreparedCodeGraphWorkspaceOwnership current,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The navigation drift fixture does not quarantine state.");
    }

    private sealed class DenyingToolUseAuthorizer(
        PreparedSourceApproval approval,
        PreparedToolPurpose purpose,
        int denyOnVerification) : IPreparedToolUseAuthorizer
    {
        public int VerificationCount { get; private set; }

        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval requestedApproval,
            PreparedToolPurpose requestedPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerificationCount++;
            PreparedToolUseAuthorityStatus status = requestedApproval != approval ||
                requestedPurpose != purpose
                    ? PreparedToolUseAuthorityStatus.Denied
                    : VerificationCount >= denyOnVerification
                        ? PreparedToolUseAuthorityStatus.Denied
                        : PreparedToolUseAuthorityStatus.Current;
            return Task.FromResult(new PreparedToolUseAuthorityResult(status));
        }
    }

    private sealed class DenyingSourceAuthority(
        IPreparedSourceCurrentAuthorityVerifier inner,
        int denyOnVerification,
        PreparedSourceAuthorityStatus loss) : IPreparedSourceCurrentAuthorityVerifier
    {
        public int VerificationCount { get; private set; }

        public Task<PreparedSourceAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval requestedApproval,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerificationCount++;
            return VerificationCount >= denyOnVerification
                ? Task.FromResult(new PreparedSourceAuthorityResult(loss, null))
                : inner.VerifyCurrentAsync(requestedApproval, cancellationToken);
        }
    }

    private sealed class UnconfirmedDisposeNavigationRuntime(PreparedJdtLsRawReply reply)
        : IPreparedJdtLsRuntime
    {
        public UnconfirmedDisposeNavigationSession Session { get; } = new(reply);

        public int OpenCallCount { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            return Task.FromResult<IPreparedJdtLsSession>(Session);
        }
    }

    private sealed class UnconfirmedDisposeNavigationSession(PreparedJdtLsRawReply reply)
        : IPreparedJdtLsSession
    {
        public int DisposeCallCount { get; private set; }

        public Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(reply);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            throw new InvalidOperationException("Controlled session cleanup is unconfirmed.");
        }
    }
}
