using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Source;
using Vulgata.Tests.PreparedSource;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web;

namespace Vulgata.Tests.AnalysisRuntime;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class PreparedJavaNavigationCompositionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ComposeAsync_WhenCancelledAfterRuntimeOpen_SettlesSessionAndLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantRequest sourceGrant = new(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedSourceGrantLimits.Default);
        fixture.Authority.Approve(fixture.Approval);
        PreparedSourceIssueResult sourceIssue = await new PreparedSourceContextIssuer(fixture.Authority)
            .IssueAsync(sourceGrant);
        Assert.Equal(PreparedSourceIssueStatus.Issued, sourceIssue.Status);
        IPreparedSourceReadContext readContext =
            Assert.IsAssignableFrom<IPreparedSourceReadContext>(sourceIssue.Context);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(stateRoot);
        using var cancellationSource = new CancellationTokenSource();
        var runtime = new CancellingNavigationRuntime(cancellationSource);
        var workspaceIssuer = new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            new ControlledToolUseAuthorizer(fixture.Approval));
        var navigationFactory = PreparedJavaNavigationOwnershipFixture.CreateFactory(
            fixture,
            new PreparedJavaNavigationProfile(
                stateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime);
        PreparedJavaWorkerToolComposition? composition = null;

        try
        {
            using ServiceProvider services = BuildWebServiceProvider(workspaceIssuer, navigationFactory);
            using IServiceScope scope = services.CreateScope();
            PreparedJavaWorkerToolComposer composer =
                scope.ServiceProvider.GetRequiredService<PreparedJavaWorkerToolComposer>();
            var selectedGrant = new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10));

            composition = await composer.ComposeAsync(
                readContext,
                sourceGrant,
                selectedGrant,
                cancellationSource.Token);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Cancelled, composition.NavigationStatus);
            Assert.Equal(
                ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                composition.WorkerFunctions.Select(function => function.Name));
            Assert.Empty(composition.NavigationFunctions);
            Assert.Equal(1, runtime.OpenCallCount);
            Assert.Equal(1, runtime.Session.DisposeCallCount);
            Assert.Equal(PreparedToolWorkspaceLifetime.Disposed, runtime.LastLease!.Lifetime);
            Assert.Equal(PreparedSourceContextLifetime.Open, readContext.Lifetime);
        }
        finally
        {
            if (composition is not null)
            {
                await composition.DisposeAsync();
            }

            await readContext.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ComposeAsync_WhenControlledRuntimeOpenFails_DisposesIssuedLeaseAndKeepsFiles()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantRequest sourceGrant = new(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedSourceGrantLimits.Default);
        fixture.Authority.Approve(fixture.Approval);
        PreparedSourceIssueResult sourceIssue = await new PreparedSourceContextIssuer(fixture.Authority)
            .IssueAsync(sourceGrant);
        Assert.Equal(PreparedSourceIssueStatus.Issued, sourceIssue.Status);
        IPreparedSourceReadContext readContext =
            Assert.IsAssignableFrom<IPreparedSourceReadContext>(sourceIssue.Context);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(stateRoot);
        var runtime = new FailedOpenNavigationRuntime();
        var workspaceIssuer = new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            new ControlledToolUseAuthorizer(fixture.Approval));
        var navigationFactory = PreparedJavaNavigationOwnershipFixture.CreateFactory(
            fixture,
            new PreparedJavaNavigationProfile(
                stateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime);
        PreparedJavaWorkerToolComposition? composition = null;

        try
        {
            using ServiceProvider services = BuildWebServiceProvider(workspaceIssuer, navigationFactory);
            using IServiceScope scope = services.CreateScope();
            PreparedJavaWorkerToolComposer composer =
                scope.ServiceProvider.GetRequiredService<PreparedJavaWorkerToolComposer>();
            var selectedGrant = new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10));

            composition = await composer.ComposeAsync(readContext, sourceGrant, selectedGrant);

            Assert.Equal(PreparedJavaNavigationOpenStatus.ReadinessFailed, composition.NavigationStatus);
            Assert.Equal(
                ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                composition.SourceFunctions.Select(function => function.Name));
            Assert.Empty(composition.NavigationFunctions);
            Assert.Equal(1, runtime.OpenCallCount);
            Assert.Equal(PreparedToolWorkspaceLifetime.Disposed, runtime.LastLease!.Lifetime);
            Assert.Equal(PreparedSourceContextLifetime.Open, readContext.Lifetime);
        }
        finally
        {
            if (composition is not null)
            {
                await composition.DisposeAsync();
            }

            await readContext.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("approval-identity")]
    [InlineData("root-binding-identity")]
    [InlineData("physical-root")]
    public async Task ComposeAsync_WhenSameRepositoryGrantIdentityOrRootDiffers_DeniesBeforeIssue(
        string mismatch)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantRequest sourceGrant = new(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedSourceGrantLimits.Default);
        fixture.Authority.Approve(fixture.Approval);
        PreparedSourceIssueResult sourceIssue = await new PreparedSourceContextIssuer(fixture.Authority)
            .IssueAsync(sourceGrant);
        Assert.Equal(PreparedSourceIssueStatus.Issued, sourceIssue.Status);
        IPreparedSourceReadContext readContext =
            Assert.IsAssignableFrom<IPreparedSourceReadContext>(sourceIssue.Context);

        PreparedSourceApproval selectedApproval = mismatch switch
        {
            "approval-identity" => fixture.Approval with
            {
                ApprovalIdentity = fixture.Approval.ApprovalIdentity + "-changed",
            },
            "root-binding-identity" => fixture.Approval with
            {
                RootBindingIdentity = fixture.Approval.RootBindingIdentity + "-changed",
            },
            "physical-root" => fixture.Approval,
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch, null),
        };
        string selectedRoot = mismatch == "physical-root"
            ? fixture.SiblingPath
            : fixture.PreparedRootPath;

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(stateRoot);
        var runtime = new ControlledNavigationRuntime(new PreparedJdtLsRawReply([], false, null));
        var workspaceIssuer = new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            new ControlledToolUseAuthorizer(fixture.Approval));
        var navigationFactory = new PreparedJavaNavigationContextFactory(
            new PreparedJavaNavigationProfile(
                stateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime);
        PreparedJavaWorkerToolComposition? composition = null;

        try
        {
            using ServiceProvider services = BuildWebServiceProvider(workspaceIssuer, navigationFactory);
            using IServiceScope scope = services.CreateScope();
            PreparedJavaWorkerToolComposer composer =
                scope.ServiceProvider.GetRequiredService<PreparedJavaWorkerToolComposer>();
            var mismatchedGrant = new PreparedToolWorkspaceGrantRequest(
                selectedApproval,
                selectedRoot,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10));

            composition = await composer.ComposeAsync(readContext, sourceGrant, mismatchedGrant);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Denied, composition.NavigationStatus);
            Assert.Equal(
                ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                composition.SourceFunctions.Select(function => function.Name));
            Assert.Empty(composition.NavigationFunctions);
            Assert.Equal(
                ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                composition.WorkerFunctions.Select(function => function.Name));
            Assert.Equal(0, runtime.OpenCallCount);
            Assert.Equal(fixture.Approval, fixture.Authority.LastRequestedApproval);
        }
        finally
        {
            if (composition is not null)
            {
                await composition.DisposeAsync();
            }

            await readContext.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ComposeAsync_WhenSelectedApprovalBelongsToAnotherRepository_DeniesBeforeIssue()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantRequest sourceGrant = new(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedSourceGrantLimits.Default);
        fixture.Authority.Approve(fixture.Approval);
        PreparedSourceIssueResult sourceIssue = await new PreparedSourceContextIssuer(fixture.Authority)
            .IssueAsync(sourceGrant);
        Assert.Equal(PreparedSourceIssueStatus.Issued, sourceIssue.Status);
        IPreparedSourceReadContext readContext =
            Assert.IsAssignableFrom<IPreparedSourceReadContext>(sourceIssue.Context);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(stateRoot);
        var runtime = new ControlledNavigationRuntime(new PreparedJdtLsRawReply([], false, null));
        var workspaceIssuer = new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            new ControlledToolUseAuthorizer(fixture.Approval));
        var navigationFactory = new PreparedJavaNavigationContextFactory(
            new PreparedJavaNavigationProfile(
                stateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime);
        PreparedJavaWorkerToolComposition? composition = null;

        try
        {
            using ServiceProvider services = BuildWebServiceProvider(workspaceIssuer, navigationFactory);
            using IServiceScope scope = services.CreateScope();
            PreparedJavaWorkerToolComposer composer =
                scope.ServiceProvider.GetRequiredService<PreparedJavaWorkerToolComposer>();
            var foreignRepositoryGrant = new PreparedToolWorkspaceGrantRequest(
                fixture.Approval with { RepositoryId = Guid.NewGuid() },
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10));

            composition = await composer.ComposeAsync(readContext, sourceGrant, foreignRepositoryGrant);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Denied, composition.NavigationStatus);
            Assert.Equal(
                ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                composition.SourceFunctions.Select(function => function.Name));
            Assert.Empty(composition.NavigationFunctions);
            Assert.Equal(
                ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                composition.WorkerFunctions.Select(function => function.Name));
            Assert.Equal(0, runtime.OpenCallCount);
            Assert.Equal(fixture.Approval, fixture.Authority.LastRequestedApproval);
        }
        finally
        {
            if (composition is not null)
            {
                await composition.DisposeAsync();
            }

            await readContext.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ComposeAsync_WhenSelectedToolPurposeIsNotNavigation_DeniesBeforeRuntimeOpen()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantRequest sourceGrant = new(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedSourceGrantLimits.Default);
        fixture.Authority.Approve(fixture.Approval);
        PreparedSourceIssueResult sourceIssue = await new PreparedSourceContextIssuer(fixture.Authority)
            .IssueAsync(sourceGrant);
        Assert.Equal(PreparedSourceIssueStatus.Issued, sourceIssue.Status);
        IPreparedSourceReadContext readContext =
            Assert.IsAssignableFrom<IPreparedSourceReadContext>(sourceIssue.Context);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(stateRoot);
        var runtime = new ControlledNavigationRuntime(new PreparedJdtLsRawReply([], false, null));
        var workspaceIssuer = new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            new ControlledToolUseAuthorizer(fixture.Approval));
        var navigationFactory = new PreparedJavaNavigationContextFactory(
            new PreparedJavaNavigationProfile(
                stateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime);
        PreparedJavaWorkerToolComposition? composition = null;

        try
        {
            using ServiceProvider services = BuildWebServiceProvider(workspaceIssuer, navigationFactory);
            using IServiceScope scope = services.CreateScope();
            PreparedJavaWorkerToolComposer composer =
                scope.ServiceProvider.GetRequiredService<PreparedJavaWorkerToolComposer>();
            var wrongPurposeGrant = new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.CodeGraphPreparation,
                TimeSpan.FromSeconds(10));

            composition = await composer.ComposeAsync(readContext, sourceGrant, wrongPurposeGrant);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Denied, composition.NavigationStatus);
            Assert.Equal(
                ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                composition.SourceFunctions.Select(function => function.Name));
            Assert.Empty(composition.NavigationFunctions);
            Assert.Equal(
                ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                composition.WorkerFunctions.Select(function => function.Name));
            Assert.Equal(0, runtime.OpenCallCount);
        }
        finally
        {
            if (composition is not null)
            {
                await composition.DisposeAsync();
            }

            await readContext.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ComposeAsync_WhenWorkspaceAuthorityIsUnavailable_KeepsFileFunctionsUsable()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantRequest sourceGrant = new(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedSourceGrantLimits.Default);
        fixture.Authority.Approve(fixture.Approval);
        PreparedSourceIssueResult sourceIssue = await new PreparedSourceContextIssuer(fixture.Authority)
            .IssueAsync(sourceGrant);
        Assert.Equal(PreparedSourceIssueStatus.Issued, sourceIssue.Status);
        IPreparedSourceReadContext readContext =
            Assert.IsAssignableFrom<IPreparedSourceReadContext>(sourceIssue.Context);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(stateRoot);
        var runtime = new ControlledNavigationRuntime(new PreparedJdtLsRawReply([], false, null));
        var workspaceIssuer = new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            new ControlledToolUseAuthorizer(
                fixture.Approval,
                PreparedToolUseAuthorityStatus.Unavailable));
        var navigationFactory = new PreparedJavaNavigationContextFactory(
            new PreparedJavaNavigationProfile(
                stateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime);
        PreparedJavaWorkerToolComposition? composition = null;

        try
        {
            using ServiceProvider services = BuildWebServiceProvider(workspaceIssuer, navigationFactory);
            using IServiceScope scope = services.CreateScope();
            PreparedJavaWorkerToolComposer composer =
                scope.ServiceProvider.GetRequiredService<PreparedJavaWorkerToolComposer>();
            var selectedGrant = new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10));

            composition = await composer.ComposeAsync(readContext, sourceGrant, selectedGrant);

            Assert.Equal(
                PreparedJavaNavigationOpenStatus.AuthorityUnavailable,
                composition.NavigationStatus);
            Assert.Equal(
                ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                composition.SourceFunctions.Select(function => function.Name));
            Assert.Empty(composition.NavigationFunctions);
            Assert.Equal(
                ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                composition.WorkerFunctions.Select(function => function.Name));
            Assert.Equal(0, runtime.OpenCallCount);

            AIFunction readFunction = Assert.Single(composition.SourceFunctions.Where(
                function => function.Name == "read_prepared_source"));
            object? rawResult = await readFunction.InvokeAsync(
                new AIFunctionArguments(new Dictionary<string, object?>
                {
                    ["relativePath"] = "notes.oddlangx",
                    ["byteOffset"] = 0L,
                    ["byteCount"] = 64,
                }),
                CancellationToken.None);
            PreparedSourceResult readResult = Assert.IsType<PreparedSourceResult>(rawResult);
            Assert.Equal(PreparedSourceOutcome.Complete, readResult.Outcome);
            Assert.Equal("café\nsource fixture\n", readResult.Text);
        }
        finally
        {
            if (composition is not null)
            {
                await composition.DisposeAsync();
            }

            await readContext.DisposeAsync();
        }

        Assert.Equal(PreparedSourceContextLifetime.Disposed, readContext.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ComposeAsync_WhenTrustedOwnershipBindingIsMissing_LeavesNavigationUnavailable()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantRequest sourceGrant = new(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedSourceGrantLimits.Default);
        fixture.Authority.Approve(fixture.Approval);
        PreparedSourceIssueResult sourceIssue = await new PreparedSourceContextIssuer(fixture.Authority)
            .IssueAsync(sourceGrant);
        Assert.Equal(PreparedSourceIssueStatus.Issued, sourceIssue.Status);
        IPreparedSourceReadContext readContext =
            Assert.IsAssignableFrom<IPreparedSourceReadContext>(sourceIssue.Context);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(stateRoot);
        var runtime = new ControlledNavigationRuntime(new PreparedJdtLsRawReply([], false, null));
        var workspaceIssuer = new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            new ControlledToolUseAuthorizer(fixture.Approval));
        var navigationFactory = new PreparedJavaNavigationContextFactory(
            new PreparedJavaNavigationProfile(
                stateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime);
        PreparedToolWorkspaceGrantRequest selectedGrant = new(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(10));
        PreparedToolWorkspaceIssueResult leaseIssue = await workspaceIssuer.IssueAsync(selectedGrant);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, leaseIssue.Status);
        await Assert.IsAssignableFrom<PreparedToolWorkspaceLease>(leaseIssue.Lease).DisposeAsync();

        try
        {
            using ServiceProvider services = BuildWebServiceProvider(workspaceIssuer, navigationFactory);
            using IServiceScope scope = services.CreateScope();
            PreparedJavaWorkerToolComposer composer =
                scope.ServiceProvider.GetRequiredService<PreparedJavaWorkerToolComposer>();

            PreparedJavaWorkerToolComposition composition = await composer.ComposeAsync(
                readContext,
                sourceGrant,
                selectedGrant);
            try
            {
                Assert.Equal(
                    PreparedJavaNavigationOpenStatus.ConfigurationUnavailable,
                    composition.NavigationStatus);
                Assert.Equal(
                    ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                    composition.WorkerFunctions.Select(function => function.Name));
                Assert.Empty(composition.NavigationFunctions);
                Assert.Equal(0, runtime.OpenCallCount);
            }
            finally
            {
                await composition.DisposeAsync();
            }
        }
        finally
        {
            await readContext.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ComposeAsync_WhenCurrentBindingIsVerifiedUnowned_OpensNavigation()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantRequest sourceGrant = new(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedSourceGrantLimits.Default);
        fixture.Authority.Approve(fixture.Approval);
        PreparedSourceIssueResult sourceIssue = await new PreparedSourceContextIssuer(fixture.Authority)
            .IssueAsync(sourceGrant);
        Assert.Equal(PreparedSourceIssueStatus.Issued, sourceIssue.Status);
        IPreparedSourceReadContext readContext =
            Assert.IsAssignableFrom<IPreparedSourceReadContext>(sourceIssue.Context);

        PreparedJavaNavigationOwnershipFixture ownershipFixture =
            PreparedJavaNavigationOwnershipFixture.Create(
                fixture.Authority,
                fixture.Approval,
                fixture.PreparedRootPath);
        PreparedCodeGraphOwnershipViewResult ownershipView =
            await ownershipFixture.GetCurrentOwnershipViewAsync(fixture.Approval);
        Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedUnowned, ownershipView.Status);
        Assert.Null(ownershipView.Record);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(stateRoot);
        var runtime = new ControlledNavigationRuntime(new PreparedJdtLsRawReply([], false, null));
        var workspaceIssuer = new PreparedToolWorkspaceIssuer(
            fixture.Authority,
            new ControlledToolUseAuthorizer(fixture.Approval));
        PreparedToolWorkspaceGrantRequest selectedGrant = new(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(10));
        PreparedToolWorkspaceIssueResult leaseIssue = await workspaceIssuer.IssueAsync(selectedGrant);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, leaseIssue.Status);
        await Assert.IsAssignableFrom<PreparedToolWorkspaceLease>(leaseIssue.Lease).DisposeAsync();

        var navigationFactory = ownershipFixture.CreateFactory(
            new PreparedJavaNavigationProfile(
                stateRoot,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                MaximumResponseBytes: 64 * 1024),
            runtime);

        try
        {
            using ServiceProvider services = BuildWebServiceProvider(workspaceIssuer, navigationFactory);
            using IServiceScope scope = services.CreateScope();
            PreparedJavaWorkerToolComposer composer =
                scope.ServiceProvider.GetRequiredService<PreparedJavaWorkerToolComposer>();

            PreparedJavaWorkerToolComposition composition = await composer.ComposeAsync(
                readContext,
                sourceGrant,
                selectedGrant);
            try
            {
                Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, composition.NavigationStatus);
                Assert.Equal(
                    ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                    composition.SourceFunctions.Select(function => function.Name));
                Assert.Equal(
                    ["navigate_java_definition", "navigate_java_references"],
                    composition.NavigationFunctions.Select(function => function.Name));
                Assert.Equal(5, composition.WorkerFunctions.Count);
                Assert.Equal(1, runtime.OpenCallCount);
            }
            finally
            {
                await composition.DisposeAsync();
            }
        }
        finally
        {
            await readContext.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ComposeAsync_WhenNavigationOpens_KeepsFiveFunctionsAfterLimitedCall()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaPath = Path.Combine(fixture.PreparedRootPath, "Example.java");
        await File.WriteAllTextAsync(javaPath, "class Example {}\n");

        PreparedSourceGrantRequest sourceGrant = new(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedSourceGrantLimits.Default);
        fixture.Authority.Approve(fixture.Approval);
        PreparedSourceIssueResult sourceIssue = await new PreparedSourceContextIssuer(fixture.Authority)
            .IssueAsync(sourceGrant);
        Assert.Equal(PreparedSourceIssueStatus.Issued, sourceIssue.Status);
        IPreparedSourceReadContext readContext =
            Assert.IsAssignableFrom<IPreparedSourceReadContext>(sourceIssue.Context);

        string stateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(stateRoot);
        var profile = new PreparedJavaNavigationProfile(
            stateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledNavigationRuntime(new PreparedJdtLsRawReply(
            [new PreparedJdtLsRawLocation(new Uri(javaPath).AbsoluteUri, 1, 1, 1, 6)],
            LimitReached: true,
            CapabilityLimitation: "controlled fixture limit"));
        var toolAuthorization = new ControlledToolUseAuthorizer(fixture.Approval);
        var workspaceIssuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var navigationFactory = PreparedJavaNavigationOwnershipFixture.CreateFactory(
            fixture,
            profile,
            runtime);
        PreparedJavaWorkerToolComposition? composition = null;

        try
        {
            using ServiceProvider services = BuildWebServiceProvider(workspaceIssuer, navigationFactory);
            using IServiceScope scope = services.CreateScope();
            PreparedJavaWorkerToolComposer composer =
                scope.ServiceProvider.GetRequiredService<PreparedJavaWorkerToolComposer>();
            PreparedToolWorkspaceGrantRequest selectedGrant = new(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10));

            composition = await composer.ComposeAsync(readContext, sourceGrant, selectedGrant);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, composition.NavigationStatus);
            Assert.Equal(
                ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                composition.SourceFunctions.Select(function => function.Name));
            Assert.Equal(
                ["navigate_java_definition", "navigate_java_references"],
                composition.NavigationFunctions.Select(function => function.Name));
            Assert.Equal(
                [
                    "list_prepared_source",
                    "read_prepared_source",
                    "search_prepared_source",
                    "navigate_java_definition",
                    "navigate_java_references",
                ],
                composition.WorkerFunctions.Select(function => function.Name));

            AIFunction definition = Assert.Single(composition.NavigationFunctions.Where(
                function => function.Name == "navigate_java_definition"));
            object? rawResult = await definition.InvokeAsync(
                new AIFunctionArguments(new Dictionary<string, object?>
                {
                    ["relativePath"] = "Example.java",
                    ["line"] = 1,
                    ["character"] = 1,
                    ["maximumLocations"] = 10,
                }),
                CancellationToken.None);
            PreparedJavaNavigationResult result = Assert.IsType<PreparedJavaNavigationResult>(rawResult);
            Assert.Equal(PreparedJavaNavigationOutcome.Limited, result.Outcome);
            Assert.Single(result.Locations);
            Assert.Equal("Example.java", Assert.Single(result.Locations).Source.RelativePath);
            Assert.Equal(1, runtime.OpenCallCount);
            Assert.Equal(profile, runtime.LastProfile);
            Assert.Equal(PreparedToolWorkspaceLifetime.Open, runtime.LastWorkspaceLease!.Lifetime);
            Assert.Equal(PreparedSourceContextLifetime.Open, readContext.Lifetime);

            Assert.Equal(5, composition.WorkerFunctions.Count);
        }
        finally
        {
            if (composition is not null)
            {
                await composition.DisposeAsync();
            }

            await readContext.DisposeAsync();
        }

        Assert.Equal(1, runtime.Session.DisposeCallCount);
        Assert.Equal(PreparedToolWorkspaceLifetime.Open, runtime.Session.LeaseLifetimeWhenDisposed);
        Assert.Equal(PreparedToolWorkspaceLifetime.Disposed, runtime.LastWorkspaceLease!.Lifetime);
        Assert.Equal(PreparedSourceContextLifetime.Disposed, readContext.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ComposeAsync_WhenNavigationDependenciesAreClosed_KeepsFileFunctionsUsable()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantRequest sourceGrant = new(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedSourceGrantLimits.Default);
        fixture.Authority.Approve(fixture.Approval);
        PreparedSourceIssueResult sourceIssue = await new PreparedSourceContextIssuer(fixture.Authority)
            .IssueAsync(sourceGrant);
        Assert.Equal(PreparedSourceIssueStatus.Issued, sourceIssue.Status);
        IPreparedSourceReadContext readContext =
            Assert.IsAssignableFrom<IPreparedSourceReadContext>(sourceIssue.Context);

        try
        {
            using ServiceProvider services = BuildWebServiceProvider();
            using IServiceScope scope = services.CreateScope();
            Assert.Null(scope.ServiceProvider.GetService<PreparedToolWorkspaceIssuer>());
            Assert.Null(scope.ServiceProvider.GetService<PreparedJavaNavigationContextFactory>());
            Assert.Null(scope.ServiceProvider.GetService<IPreparedJdtLsRuntime>());

            PreparedJavaWorkerToolComposer composer =
                scope.ServiceProvider.GetRequiredService<PreparedJavaWorkerToolComposer>();
            PreparedToolWorkspaceGrantRequest applicationSelectedJdtGrant = new(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5));
            PreparedJavaWorkerToolComposition composition = await composer.ComposeAsync(
                readContext,
                sourceGrant,
                applicationSelectedJdtGrant);

            try
            {
                Assert.Equal(
                    PreparedJavaNavigationOpenStatus.ConfigurationUnavailable,
                    composition.NavigationStatus);
                Assert.Equal(
                    ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                    composition.SourceFunctions.Select(function => function.Name));
                Assert.Empty(composition.NavigationFunctions);
                Assert.Equal(
                    ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
                    composition.WorkerFunctions.Select(function => function.Name));

                AIFunction readFunction = Assert.Single(
                    composition.SourceFunctions.Where(function => function.Name == "read_prepared_source"));
                object? rawResult = await readFunction.InvokeAsync(
                    new AIFunctionArguments(new Dictionary<string, object?>
                    {
                        ["relativePath"] = "notes.oddlangx",
                        ["byteOffset"] = 0L,
                        ["byteCount"] = 64,
                    }),
                    CancellationToken.None);
                PreparedSourceResult readResult = Assert.IsType<PreparedSourceResult>(rawResult);
                Assert.Equal(PreparedSourceOutcome.Complete, readResult.Outcome);
                Assert.Equal("café\nsource fixture\n", readResult.Text);
            }
            finally
            {
                await composition.DisposeAsync();
            }

            Assert.Equal(PreparedSourceContextLifetime.Open, readContext.Lifetime);
        }
        finally
        {
            await readContext.DisposeAsync();
        }
    }

    private sealed class ControlledToolUseAuthorizer(
        PreparedSourceApproval approval,
        PreparedToolUseAuthorityStatus currentStatus = PreparedToolUseAuthorityStatus.Current)
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
                    ? currentStatus
                    : PreparedToolUseAuthorityStatus.Denied;
            return Task.FromResult(new PreparedToolUseAuthorityResult(status));
        }
    }

    private static ServiceProvider BuildWebServiceProvider(
        PreparedToolWorkspaceIssuer? workspaceIssuer = null,
        PreparedJavaNavigationContextFactory? navigationFactory = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration["ConnectionStrings:DefaultConnection"] =
            "Host=127.0.0.1;Port=1;Database=vulgata_t012_disposable;Username=placeholder;Password=placeholder";
        _ = VulgataWebComposition.ConfigureServices(builder);
        if (workspaceIssuer is not null)
        {
            builder.Services.AddSingleton(workspaceIssuer);
        }

        if (navigationFactory is not null)
        {
            builder.Services.AddSingleton(navigationFactory);
        }

        return builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class ControlledNavigationRuntime(PreparedJdtLsRawReply reply) : IPreparedJdtLsRuntime
    {
        public RecordingNavigationSession Session { get; } = new(reply);

        public int OpenCallCount { get; private set; }

        public PreparedJavaNavigationProfile? LastProfile { get; private set; }

        public PreparedToolWorkspaceLease? LastWorkspaceLease { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            LastProfile = profile;
            LastWorkspaceLease = workspaceLease;
            Session.AttachWorkspaceLease(workspaceLease);
            return Task.FromResult<IPreparedJdtLsSession>(Session);
        }
    }

    private sealed class FailedOpenNavigationRuntime : IPreparedJdtLsRuntime
    {
        public int OpenCallCount { get; private set; }

        public PreparedToolWorkspaceLease? LastLease { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            LastLease = workspaceLease;
            throw new PreparedJdtLsOpenFailureException(
                PreparedJavaNavigationOpenStatus.ReadinessFailed,
                cleanupConfirmed: true);
        }
    }

    private sealed class CancellingNavigationRuntime(CancellationTokenSource cancellationSource)
        : IPreparedJdtLsRuntime
    {
        public RecordingNavigationSession Session { get; } = new(new PreparedJdtLsRawReply([], false, null));

        public int OpenCallCount { get; private set; }

        public PreparedToolWorkspaceLease? LastLease { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            LastLease = workspaceLease;
            Session.AttachWorkspaceLease(workspaceLease);
            cancellationSource.Cancel();
            return Task.FromResult<IPreparedJdtLsSession>(Session);
        }
    }

    private sealed class RecordingNavigationSession(PreparedJdtLsRawReply reply) : IPreparedJdtLsSession
    {
        private PreparedToolWorkspaceLease? _workspaceLease;

        public int DisposeCallCount { get; private set; }

        public PreparedToolWorkspaceLifetime? LeaseLifetimeWhenDisposed { get; private set; }

        public void AttachWorkspaceLease(PreparedToolWorkspaceLease workspaceLease) =>
            _workspaceLease = workspaceLease;

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
            LeaseLifetimeWhenDisposed = _workspaceLease?.Lifetime;
            return ValueTask.CompletedTask;
        }
    }
}
