using System.Text.Json;
using Microsoft.Extensions.AI;
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
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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
