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
        var factory = new PreparedJavaNavigationContextFactory(
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
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
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
            Assert.Equal(CancellationToken.None, runtime.LastCancellationToken);
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
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);

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
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);

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
        var factory = new PreparedJavaNavigationContextFactory(invalidProfile, runtime);

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
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
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
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
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
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
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
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
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
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
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
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
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
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);

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
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
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
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
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
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
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

        public TaskCompletionSource<bool> NavigationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseNavigation { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DisposeCallCount => Volatile.Read(ref _disposeCallCount);

        public async Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
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
}
