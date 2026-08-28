using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FluentValidation;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Shared.Scans;
using Vulgata.Web.Data;
using Xunit.Sdk;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T045 independent composition gate for the Story 12.4 execution boundary.
/// These checks resolve the production graph and exercise only the public HTTP
/// boundary with invalid identity; no provider or analysis runtime is invoked.
/// </summary>
public sealed class Pass2ProductionCompositionTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private static readonly SemaphoreSlim PreTransportTestGate = new(1, 1);
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public Pass2ProductionCompositionTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProductionContainer_ResolvesPass2RuntimeGraphWithoutEagerProviderActivityAsync()
    {
        using var baseFactory = new LoginLogoutTests.CustomWebApplicationFactory();
        var transport = new NoCallHandler();
        using WebApplicationFactory<Program> factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<HttpMessageHandler>();
                services.AddScoped<HttpMessageHandler>(_ => transport);
            }));

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.IsType<PreparedPass2Coordinator>(
            services.GetRequiredService<PreparedPass2Coordinator>());
        Assert.IsType<PreparedPass2ExecutionActivator>(
            services.GetRequiredService<PreparedPass2ExecutionActivator>());
        Assert.IsType<PreparedPass2JobRunner>(
            services.GetRequiredService<PreparedPass2JobRunner>());
        Assert.IsType<PreparedPass2Store>(
            services.GetRequiredService<PreparedPass2Store>());
        Assert.IsType<PreparedPass2DispatchRequestSource>(
            services.GetRequiredService<IPreparedPass2DispatchRequestSource>());
        Assert.IsType<PreparedPass2WorkerDispatcher>(
            services.GetRequiredService<IPreparedPass2WorkerDispatcher>());
        Assert.IsType<PreparedPass2JobExecutor>(
            services.GetRequiredService<PreparedPass2JobExecutor>());
        Assert.Equal(
            "OpenAiCompatiblePass2StreamingTransport",
            services.GetRequiredService<IPreparedPass2StreamingTransport>().GetType().Name);
        Assert.NotNull(services.GetRequiredService<IPass2CodeLogicRetrievalService>());
        Assert.NotNull(services.GetRequiredService<IPass2BusinessLogicDocumentCommitter>());
        Assert.IsType<StartPass2ExecutionRequestValidator>(
            services.GetRequiredService<IValidator<StartPass2ExecutionRequest>>());

        Assert.Contains(
            services.GetServices<IHostedService>(),
            service => service is PreparedPass2ExecutionHostedService);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public void Pass2RuntimeEntryPoints_DeriveAuthorityAndDoNotInjectAnalysisProviders()
    {
        Type[] entryPoints =
        [
            typeof(PreparedPass2Coordinator),
            typeof(PreparedPass2ExecutionHostedService),
            typeof(PreparedPass2JobRunner),
            typeof(PreparedPass2WorkerDispatcher),
            typeof(PreparedPass2JobExecutor),
            typeof(PreparedPass2DispatchRequestSource),
        ];
        Type[] forbidden =
        [
            typeof(RepositoryAccessScope),
            typeof(IJavaAnalysisSessionFactory),
            typeof(ICodeGraphAnalysisPort),
            typeof(IPreparedCodeGraphAnalysisPort),
        ];

        foreach (Type entryPoint in entryPoints)
        {
            foreach (Type parameterType in entryPoint.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType))
            {
                Assert.DoesNotContain(
                    forbidden,
                    forbiddenType => forbiddenType == parameterType);
            }
        }
    }

    [Fact]
    public void WebComposition_ExposesCorePass2SeamsWithoutEfInPublicEntryPoints()
    {
        Type webAssembly = typeof(Program).Assembly.GetType("Vulgata.Web.Program") ??
            typeof(Program);
        Type dbContextType = typeof(VulgataDbContext);
        IEnumerable<Type> publicEntryPointTypes = webAssembly
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SelectMany(method => new[] { method.ReturnType }
                .Concat(method.GetParameters().Select(parameter => parameter.ParameterType)));

        Assert.DoesNotContain(dbContextType, publicEntryPointTypes);
        Assert.NotNull(typeof(IPreparedPass2DispatchRequestSource));
        Assert.NotNull(typeof(IPass2CodeLogicRetrievalService));
        Assert.NotNull(typeof(IPass2BusinessLogicDocumentCommitter));
    }

    [Fact]
    public void WebPass2ProductionBoundary_DoesNotReferenceEfOrVulgataDbContext()
    {
        DirectoryInfo? repository = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (repository is not null &&
            !File.Exists(Path.Combine(repository.FullName, "Vulgata.slnx")))
        {
            repository = repository.Parent;
        }

        string repositoryRoot = repository?.FullName ?? Directory.GetCurrentDirectory();
        string webRoot = Path.Combine(repositoryRoot, "src", "dotnet", "Vulgata.Web");
        string[] productionSources =
        [
            Path.Combine(webRoot, "Data", "PreparedPass2Coordinator.cs"),
            Path.Combine(webRoot, "Data", "PreparedPass2ExecutionAuthorizationVerifier.cs"),
        ];
        foreach (string sourcePath in productionSources)
        {
            Assert.True(File.Exists(sourcePath), $"Expected Pass 2 production seam: {sourcePath}");
            string source = File.ReadAllText(sourcePath);
            Assert.DoesNotContain("VulgataDbContext", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("current-pointer")]
    [InlineData("supersession")]
    [InlineData("profiled-revision")]
    [InlineData("exact-edge")]
    [InlineData("contribution")]
    [InlineData("provider-evidence-set-fingerprint")]
    [InlineData("anchor-range")]
    [InlineData("anchor-hash")]
    [InlineData("trace")]
    [InlineData("plan-fingerprint")]
    [InlineData("membership-fingerprint")]
    [InlineData("flow-schema")]
    [InlineData("logical-key-schema")]
    [InlineData("logical-document-stable-key")]
    [InlineData("consumer-entry-identity")]
    [InlineData("consumer-scan-run")]
    [InlineData("provider-endpoint")]
    [InlineData("provider-repository")]
    [InlineData("provider-scan-run")]
    [InlineData("edge-direction")]
    [InlineData("provider-implementation-edge-type")]
    [InlineData("provider-implementation-edge-source")]
    [InlineData("provider-implementation-edge-target")]
    [InlineData("provider-implementation-edge-repository")]
    [InlineData("provider-implementation-edge-scan-run")]
    [InlineData("provider-implementation-edge-direction")]
    [InlineData("optional-edge-type")]
    [InlineData("optional-edge-source")]
    [InlineData("optional-edge-target")]
    [InlineData("optional-edge-repository")]
    [InlineData("optional-edge-scan-run")]
    [InlineData("optional-edge-direction")]
    [InlineData("execution-worker-prompt")]
    [InlineData("execution-submission-schema")]
    [InlineData("execution-tool-fingerprint")]
    public async Task PreTransportAuthorityMutation_ClosesIntegrityWithoutDispatchAsync(
        string mutation)
    {
        await PreTransportTestGate.WaitAsync();
        try
        {
            await PreTransportAuthorityMutationCoreAsync(mutation);
        }
        finally
        {
            PreTransportTestGate.Release();
        }
    }

    private async Task PreTransportAuthorityMutationCoreAsync(string mutation)
    {
        using var isolatedFactory = new LoginLogoutTests.CustomWebApplicationFactory();
        var transport = new BoundedFakeTransport();
        var dispatcher = new RecordingDispatcher();
        using AuthenticatedClient authenticated = await CreateAuthenticatedClientAsync(
            transport,
            baseFactory: isolatedFactory,
            dispatcher: dispatcher,
            disablePass2Worker: true);
        ApiPass2Seed seed = await SeedApiPairAsync(
            authenticated.UserId,
            baseFactory: isolatedFactory);
        using HttpResponseMessage created = await authenticated.Client.PostAsJsonAsync(
            "/api/pass2-executions",
            new { pass1PairExecutionId = seed.PairExecutionId });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Pass2ExecutionStartResponse start = await created.Content.ReadFromJsonAsync<
            Pass2ExecutionStartResponse>() ??
            throw new XunitException("The authority-mutation execution was unreadable.");

        await SeedOrdinaryMemberAuthoritiesAsync(
            authenticated.Services,
            start.Pass2ExecutionId);

        if (mutation.StartsWith("optional-edge-", StringComparison.Ordinal))
        {
            await AddCanonicalOptionalSupportingEdgeAsync(
                authenticated.Services,
                start.Pass2ExecutionId);
        }

        await MutatePreTransportAuthorityAsync(
            authenticated.Services,
            start.Pass2ExecutionId,
            mutation);

        var activator = new PreparedPass2ExecutionActivator();
        activator.Signal(start.Pass2ExecutionId);
        PreparedPass2ExecutionActivator.ActivationSlot slot =
            activator.GetSlot(start.Pass2ExecutionId) ??
            throw new XunitException("The manual activation slot was unavailable.");
        await using AsyncServiceScope runnerScope = authenticated.Services.CreateAsyncScope();
        PreparedPass2JobRunner runner = runnerScope.ServiceProvider
            .GetRequiredService<PreparedPass2JobRunner>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await runner.RunExecutionAsync(slot, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }

        await using AsyncServiceScope verification = authenticated.Services.CreateAsyncScope();
        VulgataDbContext context = verification.ServiceProvider
            .GetRequiredService<VulgataDbContext>();
        Assert.Equal(0, dispatcher.CallCount);
        Assert.Equal(
            Pass2ExecutionState.FailedIntegrity,
            await context.Pass2Executions
                .Where(value => value.Id == start.Pass2ExecutionId)
                .Select(value => value.State)
                .SingleAsync());
        Assert.All(
            await context.Pass2JobExecutions
                .Where(value => value.Pass2ExecutionId == start.Pass2ExecutionId)
                .ToArrayAsync(),
            value => Assert.NotEqual(Pass2JobExecutionState.Completed, value.State));
    }

    [Fact]
    public async Task RuntimeMembers_BindExactOrdinaryPass1AndCompletedScanJobIdentitiesAsync()
    {
        await using RuntimeHydrationFixture fixture = await RuntimeHydrationFixture.CreateAsync(this);
        IReadOnlyDictionary<Guid, OrdinaryMemberAuthority> authorities =
            await SeedOrdinaryMemberAuthoritiesAsync(fixture.Services, fixture.ExecutionId);
        await AssertCanonicalOrdinaryMemberAuthoritiesAsync(
            fixture.Services,
            fixture.ExecutionId,
            authorities);

        PreparedPass2DispatchHydrationResult hydration = await fixture.HydrateAsync();

        Assert.Equal(PreparedPass2DispatchHydrationOutcome.Hydrated, hydration.Outcome);
        PreparedPass2DispatchRequest request = Assert.IsType<PreparedPass2DispatchRequest>(
            hydration.Request);
        await using (AsyncServiceScope scope = fixture.Services.CreateAsyncScope())
        {
            VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            Pass2Execution execution = await context.Pass2Executions.AsNoTracking()
                .SingleAsync(value => value.Id == fixture.ExecutionId);
            Assert.Equal(execution.WorkerPromptIdentity, request.PromptIdentity);
            Assert.Equal(execution.SubmissionSchemaIdentity, request.SubmissionSchemaIdentity);
            Assert.Matches("^[0-9a-f]{64}$", execution.ToolDeclarationFingerprint);
            Assert.Equal("pass2-strict-tools/1.0", request.ToolDeclarationIdentity);
        }
        Assert.All(request.RuntimeBinding.Members, member =>
        {
            OrdinaryMemberAuthority authority = authorities[member.DocumentVersionId];
            Assert.Equal(authority.OrdinaryJobId, member.JobId);
            Assert.NotEqual(authority.ScanJobExecutionId, member.JobId);
        });
    }

    [Fact]
    public async Task RuntimeBinding_PropagatesPersistedManifestRequestedRefAsync()
    {
        await using RuntimeHydrationFixture fixture = await RuntimeHydrationFixture.CreateAsync(this);
        await SeedOrdinaryMemberAuthoritiesAsync(fixture.Services, fixture.ExecutionId);

        PreparedPass2DispatchHydrationResult hydration = await fixture.HydrateAsync();
        Assert.Equal(PreparedPass2DispatchHydrationOutcome.Hydrated, hydration.Outcome);
        PreparedPass2DispatchRequest request = Assert.IsType<PreparedPass2DispatchRequest>(
            hydration.Request);

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        Pass2Execution execution = await context.Pass2Executions.AsNoTracking()
            .SingleAsync(value => value.Id == fixture.ExecutionId);
        TwoRepositoryScanCoordination coordination = await context.TwoRepositoryScanCoordinations
            .AsNoTracking()
            .SingleAsync(value => value.Id == execution.CoordinationId);
        PreparedScanManifest[] manifests = await context.PreparedScanManifests.AsNoTracking()
            .Where(value => value.CoordinationId == coordination.Id)
            .ToArrayAsync();
        Assert.Equal(2, manifests.Length);

        PreparedScanManifest consumerManifest = manifests.Single(value =>
            value.RepositoryId == coordination.RepositoryOneId);
        PreparedScanManifest providerManifest = manifests.Single(value =>
            value.RepositoryId == coordination.RepositoryTwoId);
        Assert.Equal(consumerManifest.RequestedRef, request.RuntimeBinding.Consumer.ExpectedRef);
        Assert.Equal(providerManifest.RequestedRef, request.RuntimeBinding.Provider.ExpectedRef);
    }

    [Fact]
    public async Task RuntimeBinding_CarriesActualPromptAndSubmissionSchemaArtifactHashesAsync()
    {
        await using RuntimeHydrationFixture fixture = await RuntimeHydrationFixture.CreateAsync(this);
        await SeedOrdinaryMemberAuthoritiesAsync(fixture.Services, fixture.ExecutionId);
        PreparedPass2DispatchHydrationResult hydration = await fixture.HydrateAsync();
        PreparedPass2DispatchRequest request = Assert.IsType<PreparedPass2DispatchRequest>(
            hydration.Request);

        Assembly agentsAssembly = typeof(WorkerAgentDispatcher).Assembly;
        string resourceName = Assert.Single(agentsAssembly.GetManifestResourceNames()
            .Where(value => value.EndsWith(
                "pass2-business-logic-worker.v1.md",
                StringComparison.Ordinal)));
        using Stream promptStream = agentsAssembly.GetManifestResourceStream(resourceName)!;
        using var promptBuffer = new MemoryStream();
        await promptStream.CopyToAsync(promptBuffer);
        string promptHash = Convert.ToHexStringLower(SHA256.HashData(promptBuffer.ToArray()));
        DirectoryInfo? repository = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (repository is not null &&
            !File.Exists(Path.Combine(repository.FullName, "Vulgata.slnx")))
        {
            repository = repository.Parent;
        }
        string schemaPath = Path.Combine(
            repository?.FullName ?? Directory.GetCurrentDirectory(),
            "specs", "013-exact-cross-repository-flow-pass2", "contracts",
            "business-logic-flow-v1.schema.json");
        string schemaHash = Convert.ToHexStringLower(SHA256.HashData(
            await File.ReadAllBytesAsync(Path.GetFullPath(schemaPath))));

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        Pass2Execution execution = await context.Pass2Executions.AsNoTracking()
            .SingleAsync(value => value.Id == fixture.ExecutionId);
        Assert.Equal("pass2-business-logic-worker.v1", execution.WorkerPromptIdentity);
        Assert.Equal(promptHash, execution.WorkerPromptFingerprint);
        Assert.Equal("business-logic/flow-v1", execution.SubmissionSchemaIdentity);
        Assert.Equal(schemaHash, execution.SubmissionSchemaFingerprint);
        Assert.Equal(execution.WorkerPromptIdentity, request.PromptIdentity);
        Assert.Equal(promptHash, request.PromptFingerprint);
        Assert.Equal(execution.SubmissionSchemaIdentity, request.SubmissionSchemaIdentity);
        Assert.Equal(schemaHash, request.SubmissionSchemaFingerprint);
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("mutated")]
    public async Task V2Authority_LegacyExactEvidenceIsIrrelevantToHydrationAsync(string legacyState)
    {
        await using RuntimeHydrationFixture fixture = await RuntimeHydrationFixture.CreateAsync(this);
        await SeedOrdinaryMemberAuthoritiesAsync(fixture.Services, fixture.ExecutionId);
        await using (AsyncServiceScope scope = fixture.Services.CreateAsyncScope())
        {
            VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            if (legacyState == "absent")
            {
                Assert.Empty(await context.ExactCrossRepositoryEdgeEvidence.ToArrayAsync());
            }
            else
            {
                await AddIrrelevantLegacyEvidenceAsync(context);
            }
            await context.SaveChangesAsync();
        }

        PreparedPass2DispatchHydrationResult hydration = await fixture.HydrateAsync();

        Assert.Equal(PreparedPass2DispatchHydrationOutcome.Hydrated, hydration.Outcome);
        Assert.NotNull(hydration.Request);
        Assert.Equal(0, fixture.Transport.CallCount);
    }

    [Fact]
    public void Pass2Execution_ExposesPersistedAcceptedWorkerContractIdentities()
    {
        string[] required =
        [
            "WorkerPromptIdentity",
            "SubmissionSchemaIdentity",
            "ToolDeclarationFingerprint",
        ];

        Assert.All(required, name => Assert.NotNull(typeof(Pass2Execution).GetProperty(name)));
    }

    [Fact]
    public async Task V2Authority_SharedReferencesEdge_HydratesEachDecisionContributionAsync()
    {
        using var baseFactory = new LoginLogoutTests.CustomWebApplicationFactory();
        var transport = new BoundedFakeTransport();
        using AuthenticatedClient authenticated = await CreateAuthenticatedClientAsync(
            transport,
            baseFactory: baseFactory,
            disablePass2Worker: true);
        ApiPass2Seed seed = await SeedApiPairAsync(
            authenticated.UserId,
            baseFactory: baseFactory,
            addSharedV2Decision: true);
        using HttpResponseMessage created = await authenticated.Client.PostAsJsonAsync(
            "/api/pass2-executions",
            new { pass1PairExecutionId = seed.PairExecutionId });
        Pass2ExecutionStartResponse start = await created.Content.ReadFromJsonAsync<Pass2ExecutionStartResponse>()
            ?? throw new XunitException("The shared-edge execution was unreadable.");
        await SeedOrdinaryMemberAuthoritiesAsync(authenticated.Services, start.Pass2ExecutionId);
        await using AsyncServiceScope scope = authenticated.Services.CreateAsyncScope();
        VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        context.ExactCrossRepositoryEdgeEvidence.RemoveRange(
            await context.ExactCrossRepositoryEdgeEvidence.ToArrayAsync());
        Pass2JobExecution[] jobs = await context.Pass2JobExecutions
            .Where(value => value.Pass2ExecutionId == start.Pass2ExecutionId)
            .OrderBy(value => value.GroupOrder)
            .ToArrayAsync();
        Assert.Equal(2, jobs.Length);
        var leases = new Dictionary<Guid, Guid>();
        foreach (Pass2JobExecution job in jobs)
        {
            Guid lease = Guid.NewGuid();
            job.AcquireLease(lease, DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow);
            leases.Add(job.Id, lease);
        }
        await context.SaveChangesAsync();
        IPreparedPass2DispatchRequestSource source = scope.ServiceProvider
            .GetRequiredService<IPreparedPass2DispatchRequestSource>();
        var hydrated = new List<PreparedPass2DispatchRequest>();
        foreach (Pass2JobExecution job in jobs)
        {
            PreparedPass2DispatchHydrationResult result = await source.HydrateAsync(
                start.Pass2ExecutionId,
                job.Id,
                job.StateVersion,
                leases[job.Id]);
            Assert.Equal(PreparedPass2DispatchHydrationOutcome.Hydrated, result.Outcome);
            hydrated.Add(Assert.IsType<PreparedPass2DispatchRequest>(result.Request));
        }
        Assert.Single(hydrated.Select(value => value.ExactResolutionEdgeId).Distinct());
        Assert.Equal(2, hydrated.Select(value => value.ExactResolutionDecisionId).Distinct().Count());
        Assert.Equal(2, hydrated.Select(value => value.ProviderEvidenceFingerprint).Distinct().Count());
        Assert.Equal(0, transport.CallCount);
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("committed-document-mismatch")]
    public async Task RuntimeMembers_AbsentOrMismatchedCompletedScanJob_RefusesHydrationAsync(
        string mutation)
    {
        await using RuntimeHydrationFixture fixture = await RuntimeHydrationFixture.CreateAsync(this);
        IReadOnlyDictionary<Guid, OrdinaryMemberAuthority> authorities =
            await SeedOrdinaryMemberAuthoritiesAsync(fixture.Services, fixture.ExecutionId);
        await using (AsyncServiceScope scope = fixture.Services.CreateAsyncScope())
        {
            VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            OrdinaryMemberAuthority authority = authorities.Values.First();
            ScanJobExecution job = await context.ScanJobExecutions
                .SingleAsync(value => value.Id == authority.ScanJobExecutionId);
            if (mutation == "absent")
            {
                context.Remove(job);
                await context.SaveChangesAsync();
            }
            else
            {
                context.Entry(job).Property(nameof(ScanJobExecution.ManifestOrdinal))
                    .CurrentValue = 999;
                await context.SaveChangesAsync();
            }
        }

        PreparedPass2DispatchHydrationResult hydration = await fixture.HydrateAsync();
        Assert.Equal(PreparedPass2DispatchHydrationOutcome.IntegrityRefused, hydration.Outcome);
        Assert.Null(hydration.Request);
    }

    private static async Task AssertCanonicalOrdinaryMemberAuthoritiesAsync(
        IServiceProvider services,
        Guid executionId,
        IReadOnlyDictionary<Guid, OrdinaryMemberAuthority> authorities)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        Pass2Execution execution = await context.Pass2Executions.SingleAsync(value => value.Id == executionId);
        TwoRepositoryScanCoordination coordination = await context.TwoRepositoryScanCoordinations
            .SingleAsync(value => value.Id == execution.CoordinationId);
        foreach (OrdinaryMemberAuthority authority in authorities.Values)
        {
            DocumentVersion document = await context.DocumentVersions
                .SingleAsync(value => value.Id == authority.DocumentVersionId);
            SourceAnchor anchor = await context.SourceAnchors
                .FirstAsync(value => value.DocumentVersionId == document.Id);
            PreparedScanManifestEntry entry = await context.PreparedScanManifestEntries
                .SingleAsync(value =>
                    value.CoordinationId == coordination.Id &&
                    value.FileCodeUnitId == anchor.CodeUnitId);
            ScanJobExecution scanJob = await context.ScanJobExecutions
                .SingleAsync(value => value.Id == authority.ScanJobExecutionId);
            Guid scanExecutionId = document.RepositoryId == coordination.RepositoryOneId
                ? coordination.ScanExecutionOneId
                : coordination.ScanExecutionTwoId;
            OrdinaryPass1JobIdentity identity = OrdinaryPass1JobIdentity.Create(
                scanExecutionId,
                document.ScanRunId,
                document.RepositoryId,
                anchor.CodeUnitId,
                document.ProfiledRevision);
            Assert.Equal(authority.OrdinaryJobId, identity.JobId);
            Assert.Equal(document.Id, OrdinaryPass1JobIdentity.CreateDocumentVersionId(identity.JobId));
            Assert.Equal(entry.ManifestOrdinal, scanJob.ManifestOrdinal);
            Assert.Equal(document.Id, scanJob.CommittedDocumentVersionId);
            Assert.Equal(ScanJobExecutionState.Completed, scanJob.State);
            LogicalDocument logical = await context.LogicalDocuments
                .SingleAsync(value => value.Id == document.LogicalDocumentId);
            CodeUnit codeUnit = await context.CodeUnits.SingleAsync(value => value.Id == anchor.CodeUnitId);
            Assert.Equal(document.Id, logical.CurrentVersionId);
            Assert.Null(document.SupersededById);
            Assert.Equal(codeUnit.RepositoryId, anchor.RepositoryId);
            Assert.Equal(codeUnit.ScanRunId, anchor.ScanRunId);
            Assert.Equal(codeUnit.FilePath, anchor.RepositoryRelativePath);
            Assert.Equal(codeUnit.SourceHash, anchor.SourceHash);
            Assert.Equal(codeUnit.LineStart, anchor.LineStart);
            Assert.Equal(codeUnit.LineEnd, anchor.LineEnd);
        }

    }

    [Theory]
    [InlineData(RoleNames.SystemOwner)]
    [InlineData(RoleNames.Administrator)]
    public async Task PostClaimAuthorizationRevocation_FailsSecurityWithoutTransportAsync(
        string roleName)
    {
        await using RuntimeHydrationFixture fixture = await RuntimeHydrationFixture.CreateAsync(
            this,
            roleName,
            addSystemOwnerAssignment: roleName == RoleNames.SystemOwner);
        await using (AsyncServiceScope scope = fixture.Services.CreateAsyncScope())
        {
            if (roleName == RoleNames.SystemOwner)
            {
                VulgataDbContext context = scope.ServiceProvider
                    .GetRequiredService<VulgataDbContext>();
                SystemOwnerAssignment assignment = await context.SystemOwnerAssignments
                    .SingleAsync(value =>
                        value.SystemId == fixture.SystemId &&
                        value.UserId == fixture.UserId);
                context.Remove(assignment);
                Pass2JobExecution job = await context.Pass2JobExecutions
                    .SingleAsync(value => value.Id == fixture.JobId);
                context.Entry(job).Property(nameof(Pass2JobExecution.LeaseExpiresAt))
                    .CurrentValue = DateTimeOffset.UtcNow.AddSeconds(-1);
                await context.SaveChangesAsync();
            }
            else
            {
                UserManager<ApplicationUser> userManager = scope.ServiceProvider
                    .GetRequiredService<UserManager<ApplicationUser>>();
                ApplicationUser user = await userManager.FindByIdAsync(fixture.UserId) ??
                    throw new XunitException("The Administrator fixture user was unavailable.");
                IdentityResult removal = await userManager.RemoveFromRoleAsync(
                    user,
                    RoleNames.Administrator);
                Assert.True(removal.Succeeded);
                VulgataDbContext context = scope.ServiceProvider
                    .GetRequiredService<VulgataDbContext>();
                Pass2JobExecution job = await context.Pass2JobExecutions
                    .SingleAsync(value => value.Id == fixture.JobId);
                context.Entry(job).Property(nameof(Pass2JobExecution.LeaseExpiresAt))
                    .CurrentValue = DateTimeOffset.UtcNow.AddSeconds(-1);
                await context.SaveChangesAsync();
            }
        }

        var activator = new PreparedPass2ExecutionActivator();
        activator.Signal(fixture.ExecutionId);
        await using AsyncServiceScope runnerScope = fixture.Services.CreateAsyncScope();
        PreparedPass2JobRunner runner = runnerScope.ServiceProvider
            .GetRequiredService<PreparedPass2JobRunner>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await runner.RunExecutionAsync(
                activator.GetSlot(fixture.ExecutionId)!,
                cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }

        await using AsyncServiceScope verification = fixture.Services.CreateAsyncScope();
        VulgataDbContext verifyContext = verification.ServiceProvider
            .GetRequiredService<VulgataDbContext>();
        Assert.Equal(0, fixture.Transport.CallCount);
        Assert.Equal(
            Pass2ExecutionState.FailedSecurity,
            await verifyContext.Pass2Executions
                .Where(value => value.Id == fixture.ExecutionId)
                .Select(value => value.State)
                .SingleAsync());
    }

    [Fact]
    public async Task InvalidIdentityApiBoundary_Returns401WithoutProviderOrNetworkActivityAsync()
    {
        using var baseFactory = new LoginLogoutTests.CustomWebApplicationFactory();
        var transport = new NoCallHandler();
        using WebApplicationFactory<Program> factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<HttpMessageHandler>();
                services.AddScoped<HttpMessageHandler>(_ => transport);
            }));

        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        using HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/pass2-executions",
            new { pass1PairExecutionId = Guid.NewGuid() });
        using HttpResponseMessage status = await client.GetAsync(
            $"/api/pass2-executions/{Guid.NewGuid():D}");
        using HttpResponseMessage resume = await client.PostAsync(
            $"/api/pass2-executions/{Guid.NewGuid():D}/resume",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, resume.StatusCode);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task AuthenticatedStartStatusResume_HostedPathUsesPersistedAuthorityAndRevocationFailsClosedAsync()
    {
        var transport = new BoundedFakeTransport();
        using AuthenticatedClient authenticated =
            await CreateAuthenticatedClientAsync(transport, disablePass2Worker: true);
        ApiPass2Seed seed = await SeedApiPairAsync(authenticated.UserId);

        using HttpResponseMessage created = await authenticated.Client.PostAsJsonAsync(
            "/api/pass2-executions",
            new { pass1PairExecutionId = seed.PairExecutionId });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Pass2ExecutionStartResponse start = await created.Content.ReadFromJsonAsync<
            Pass2ExecutionStartResponse>() ??
            throw new XunitException("The hosted Pass 2 start response was not readable.");
        await SeedOrdinaryMemberAuthoritiesAsync(authenticated.Services, start.Pass2ExecutionId);
        await RunManualExecutionAsync(authenticated.Services, start.Pass2ExecutionId);

        using HttpResponseMessage status = await authenticated.Client.GetAsync(
            $"/api/pass2-executions/{start.Pass2ExecutionId:D}");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Pass2ExecutionStatusResponse statusBody = await status.Content.ReadFromJsonAsync<
            Pass2ExecutionStatusResponse>() ??
            throw new XunitException("The hosted Pass 2 status response was not readable.");
        Assert.Equal(start.Pass2ExecutionId, statusBody.Pass2ExecutionId);

        await WaitUntilAsync(
            () => transport.CallCount > 0,
            TimeSpan.FromSeconds(5));
        Assert.Equal(start.Pass2ExecutionId, transport.LastExecutionId);
        Assert.NotEqual(Guid.Empty, transport.LastJobId);

        using HttpResponseMessage resumed = await authenticated.Client.PostAsync(
            $"/api/pass2-executions/{start.Pass2ExecutionId:D}/resume",
            content: null);
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);

        await RevokeSystemOwnerAssignmentAsync(seed.SystemId, authenticated.UserId);

        using HttpResponseMessage revokedStatus = await authenticated.Client.GetAsync(
            $"/api/pass2-executions/{start.Pass2ExecutionId:D}");
        using HttpResponseMessage revokedResume = await authenticated.Client.PostAsync(
            $"/api/pass2-executions/{start.Pass2ExecutionId:D}/resume",
            content: null);
        Assert.Equal(HttpStatusCode.NotFound, revokedStatus.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, revokedResume.StatusCode);
        Assert.True(transport.CallCount >= 1);
    }

    [Fact]
    public async Task AdministratorStart_HostedPathUsesCurrentRoleAndRoleRemovalFailsClosedAsync()
    {
        using var isolatedFactory = new LoginLogoutTests.CustomWebApplicationFactory();
        var transport = new BoundedFakeTransport();
        using AuthenticatedClient authenticated = await CreateAuthenticatedClientAsync(
            transport,
            RoleNames.Administrator,
            isolatedFactory,
            disablePass2Worker: true);
        ApiPass2Seed seed = await SeedApiPairAsync(
            authenticated.UserId,
            addSystemOwnerAssignment: false,
            baseFactory: isolatedFactory);

        await using (AsyncServiceScope beforeScope = authenticated.Services.CreateAsyncScope())
        {
            IPass2ExecutionAuthorizationVerifier verifier = beforeScope.ServiceProvider
                .GetRequiredService<IPass2ExecutionAuthorizationVerifier>();
            Assert.True(await verifier.HasCurrentAccessAsync(
                authenticated.UserId,
                seed.SystemId,
                seed.RepositoryIds));
            Assert.False(await verifier.HasCurrentAccessAsync(
                authenticated.UserId,
                seed.SystemId,
                [seed.RepositoryIds[0], Guid.NewGuid()]));
        }

        using HttpResponseMessage created = await authenticated.Client.PostAsJsonAsync(
            "/api/pass2-executions",
            new { pass1PairExecutionId = seed.PairExecutionId });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Pass2ExecutionStartResponse start = await created.Content.ReadFromJsonAsync<
            Pass2ExecutionStartResponse>() ??
            throw new XunitException("The Administrator Pass 2 response was not readable.");
        await SeedOrdinaryMemberAuthoritiesAsync(authenticated.Services, start.Pass2ExecutionId);
        await RunManualExecutionAsync(authenticated.Services, start.Pass2ExecutionId);

        await WaitUntilAsync(() => transport.CallCount > 0, TimeSpan.FromSeconds(5));
        Assert.Equal(start.Pass2ExecutionId, transport.LastExecutionId);

        await using (AsyncServiceScope removalScope = authenticated.Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> userManager = removalScope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = await userManager.FindByIdAsync(authenticated.UserId) ??
                throw new XunitException("The Administrator user was not readable.");
            IdentityResult removal = await userManager.RemoveFromRoleAsync(
                user,
                RoleNames.Administrator);
            Assert.True(
                removal.Succeeded,
                string.Join(", ", removal.Errors.Select(error => error.Description)));
        }

        await using AsyncServiceScope afterScope = authenticated.Services.CreateAsyncScope();
        IPass2ExecutionAuthorizationVerifier afterVerifier = afterScope.ServiceProvider
            .GetRequiredService<IPass2ExecutionAuthorizationVerifier>();
        Assert.False(await afterVerifier.HasCurrentAccessAsync(
            authenticated.UserId,
            seed.SystemId,
            seed.RepositoryIds));
    }

    [Fact]
    public async Task HostedFailureLog_UsesClosedCategoryAndCodeWithoutExceptionAtomsAsync()
    {
        Guid executionId = Guid.NewGuid();
        var activator = new PreparedPass2ExecutionActivator();
        var logs = new CapturingLoggerProvider();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        var service = new PreparedPass2ExecutionHostedService(
            activator,
            new ThrowingScopeFactory(
                new InvalidOperationException(
                    "C:\\secret\\source\\path; provider=https://provider.example/chat/completions; source payload text")),
            loggerFactory.CreateLogger<PreparedPass2ExecutionHostedService>());

        await service.StartAsync(CancellationToken.None);
        activator.Signal(executionId);
        await WaitUntilAsync(() => logs.Entries.Count > 0, TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        string output = string.Join('\n', logs.Entries);
        Assert.Contains(executionId.ToString("D"), output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ScanJobFailureCategory.Unclassified.ToString(), output, StringComparison.Ordinal);
        Assert.Contains("worker-failed", output, StringComparison.Ordinal);
        foreach (string forbidden in new[]
        {
            "C:\\secret\\source\\path",
            "provider.example",
            "source payload text",
            "InvalidOperationException",
        })
        {
            Assert.DoesNotContain(forbidden, output, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class NoCallHandler : HttpMessageHandler
    {
        internal int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException(
                "T045 composition test must not invoke provider or network transport.");
        }
    }

    private async Task<AuthenticatedClient> CreateAuthenticatedClientAsync(
        BoundedFakeTransport transport,
        string roleName = RoleNames.SystemOwner,
        LoginLogoutTests.CustomWebApplicationFactory? baseFactory = null,
        IPreparedPass2WorkerDispatcher? dispatcher = null,
        bool disablePass2Worker = false)
    {
        string email = $"pass2-t045-{Guid.NewGuid():N}@example.com";
        const string password = "Valid1!Pass";
        string keyRingPath = Path.Combine(
            Path.GetTempPath(),
            "vulgata-tests",
            $"t045-dataprotection-{Guid.NewGuid():N}");
        Directory.CreateDirectory(keyRingPath);

        WebApplicationFactory<Program> host = (baseFactory ?? _factory).WithWebHostBuilder(builder =>
            builder
                .UseSetting("DataProtection:KeyRingPath", keyRingPath)
                .UseSetting("DataProtection:ApplicationName", "Vulgata.Tests.T045")
                .ConfigureTestServices(services =>
                {
                    services.AddDataProtection()
                        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                        .SetApplicationName("Vulgata.Tests.T045");
                    services.RemoveAll<IPreparedPass2StreamingTransport>();
                    services.AddScoped<IPreparedPass2StreamingTransport>(_ => transport);
                    services.RemoveAll<IPreparedPass1ProviderAvailabilityProbe>();
                    services.AddScoped<IPreparedPass1ProviderAvailabilityProbe>(
                        _ => new AlwaysAvailableProviderProbe());
                    if (dispatcher is not null)
                    {
                        services.RemoveAll<IPreparedPass2WorkerDispatcher>();
                        services.AddSingleton(dispatcher);
                    }

                    if (disablePass2Worker)
                    {
                        foreach (ServiceDescriptor descriptor in services
                            .Where(value => value.ServiceType == typeof(IHostedService) &&
                                value.ImplementationType == typeof(PreparedPass2ExecutionHostedService))
                            .ToArray())
                        {
                            services.Remove(descriptor);
                        }
                    }
                }));

        try
        {
            await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
            {
                UserManager<ApplicationUser> userManager = scope.ServiceProvider
                    .GetRequiredService<UserManager<ApplicationUser>>();
                IdentityResult result = await userManager.CreateAsync(
                    new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                    },
                    password);
                Assert.True(
                    result.Succeeded,
                    string.Join(", ", result.Errors.Select(error => error.Description)));
                ApplicationUser user = await userManager.FindByEmailAsync(email) ??
                    throw new XunitException("The T045 user was not persisted.");
                IdentityResult roleResult = await userManager.AddToRoleAsync(
                    user,
                    roleName);
                Assert.True(
                    roleResult.Succeeded,
                    string.Join(", ", roleResult.Errors.Select(error => error.Description)));
            }

            HttpClient client = host.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            });
            using HttpResponseMessage loginPage = await client.GetAsync("/Account/Login");
            loginPage.EnsureSuccessStatusCode();
            Dictionary<string, string> fields = ExtractFormFields(
                await loginPage.Content.ReadAsStringAsync());
            fields["Input.Email"] = email;
            fields["Input.Password"] = password;
            fields["Input.RememberMe"] = "false";
            using var login = new FormUrlEncodedContent(fields);
            using HttpResponseMessage loginResponse = await client.PostAsync(
                "/Account/Login",
                login);
            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

            await using AsyncServiceScope userScope = host.Services.CreateAsyncScope();
            string userId = (await userScope.ServiceProvider
                    .GetRequiredService<UserManager<ApplicationUser>>()
                    .FindByEmailAsync(email))?.Id ??
                throw new XunitException("The authenticated T045 user was not readable.");
            return new AuthenticatedClient(host, client, keyRingPath, userId);
        }
        catch
        {
            host.Dispose();
            TryDeleteDirectory(keyRingPath);
            throw;
        }
    }

    private async Task<ApiPass2Seed> SeedApiPairAsync(
        string userId,
        bool addSystemOwnerAssignment = true,
        LoginLogoutTests.CustomWebApplicationFactory? baseFactory = null,
        bool addSharedV2Decision = false)
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        await using VulgataDbContext context = new(
            new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite((baseFactory ?? _factory).ConnectionString)
                .Options);
        CanonicalPass2Seed seed = await SeedCanonicalPass2Async(context, fixture);
        if (addSharedV2Decision)
        {
            await AddSharedV2DecisionAsync(context);
        }
        Pass1PairExecution pair = seed.Pair;
        TwoRepositoryScanCoordination coordination = await context
            .TwoRepositoryScanCoordinations
            .SingleAsync(value => value.Id == pair.CoordinationId);
        coordination.BeginPass2(coordination.StateVersion, coordination.UpdatedAt.AddSeconds(1));
        if (addSystemOwnerAssignment)
        {
            context.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(
                pair.SystemId,
                userId,
                coordination.UpdatedAt));
        }
        await context.SaveChangesAsync();
        return new(
            pair.Id,
            pair.SystemId,
            seed.Consumer.DocumentVersion.Id,
            [seed.Consumer.DocumentVersion.RepositoryId, seed.ProviderXml.DocumentVersion.RepositoryId]);
    }

    internal static async Task AddSharedV2DecisionAsync(VulgataDbContext context)
    {
        CrossRepositoryResolutionDecision[] existingDecisions = await context
            .CrossRepositoryResolutionDecisions.ToArrayAsync();
        CrossRepositoryResolutionDecision original = existingDecisions
            .Where(value => value.Outcome == "ExactMatch")
            .OrderBy(value => value.CreatedAt)
            .First();
        ExactResolutionProviderEvidenceContribution originalContribution = await context
            .ExactResolutionProviderEvidenceContributions.FirstAsync(value =>
                value.ResolutionDecisionId == original.Id);
        CrossRepositoryResolutionDecision decision = (CrossRepositoryResolutionDecision)
            Activator.CreateInstance(
                typeof(CrossRepositoryResolutionDecision),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: null,
                culture: null)!;
        foreach (PropertyInfo property in typeof(CrossRepositoryResolutionDecision).GetProperties()
            .Where(property => property.CanRead))
        {
            property.SetValue(decision, property.GetValue(original));
        }
        Type canonical = typeof(PreparedPass2DispatchRequestSource).Assembly.GetType(
            "Vulgata.Infrastructure.Data.ExactResolutionCanonicalAuthority")!;
        string decisionFingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"shared-edge-decision/{existingDecisions.Length + 1}")));
        Guid decisionId = (Guid)canonical.GetMethod(
            "DecisionId",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(
                null,
                [decisionFingerprint])!;
        SetProperty(decision, nameof(CrossRepositoryResolutionDecision.Id), decisionId);
        SetProperty(decision, nameof(CrossRepositoryResolutionDecision.ConsumerCandidateId), Guid.NewGuid());
        SetProperty(decision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryIdentity), "shared-edge-second-entry");
        SetProperty(decision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryStableKey), new string('8', 64));
        SetProperty(decision, nameof(CrossRepositoryResolutionDecision.DecisionFingerprint), decisionFingerprint);
        SetProperty(decision, nameof(CrossRepositoryResolutionDecision.EvidenceFingerprint), decisionFingerprint);
        SetProperty(decision, nameof(CrossRepositoryResolutionDecision.CreatedAt), original.CreatedAt.AddTicks(1));
        context.CrossRepositoryResolutionDecisions.Add(decision);

        ExactResolutionProviderEvidenceContribution contribution =
            (ExactResolutionProviderEvidenceContribution)Activator.CreateInstance(
                typeof(ExactResolutionProviderEvidenceContribution),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: null,
                culture: null)!;
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.ResolutionDecisionId), decisionId);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderIdentity), originalContribution.ProviderIdentity);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderDocumentVersionId), originalContribution.ProviderDocumentVersionId);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.RepositoryId), originalContribution.RepositoryId);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.ScanRunId), originalContribution.ScanRunId);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProfiledRevision), originalContribution.ProfiledRevision);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.CommunicationClassificationId), originalContribution.CommunicationClassificationId);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.SourceAnchorId), originalContribution.SourceAnchorId);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceLocator), originalContribution.EvidenceLocator);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.ClassificationFingerprint), originalContribution.ClassificationFingerprint);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.CatalogRuleIdentity), originalContribution.CatalogRuleIdentity);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.NormalizationRuleIdentity), originalContribution.NormalizationRuleIdentity);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderOrdinal), originalContribution.ProviderOrdinal);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceOrdinal), originalContribution.EvidenceOrdinal);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.CreatedAt), originalContribution.CreatedAt.AddTicks(1));
        string contributionFingerprint = (string)canonical.GetMethod(
            "PersistedProviderContributionFingerprint",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(
                null,
                [decisionFingerprint, contribution])!;
        Guid contributionId = (Guid)canonical.GetMethod(
            "ProviderContributionId",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(
                null,
                [contributionFingerprint])!;
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.Id), contributionId);
        SetProperty(contribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceFingerprint), contributionFingerprint);
        SetProperty(decision, nameof(CrossRepositoryResolutionDecision.ProviderEvidenceSetFingerprint),
            canonical.GetMethod(
                "ProviderEvidenceSetFingerprint",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(
                    null,
                    [new[] { contributionFingerprint }])!);
        context.ExactResolutionProviderEvidenceContributions.Add(contribution);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task AddIrrelevantLegacyEvidenceAsync(VulgataDbContext context)
    {
        CrossRepositoryResolutionDecision decision = await context
            .CrossRepositoryResolutionDecisions
            .SingleAsync(value => value.Outcome == "ExactMatch");
        ExactResolutionProviderEvidenceContribution contribution = await context
            .ExactResolutionProviderEvidenceContributions.SingleAsync();
        SourceAnchor consumerAnchor = await context.SourceAnchors.SingleAsync(value =>
            value.DocumentVersionId == decision.ConsumerDocumentVersionId);
        SourceAnchor providerAnchor = await context.SourceAnchors.SingleAsync(value =>
            value.Id == contribution.SourceAnchorId);
        context.ExactCrossRepositoryEdgeEvidence.Add(new ExactCrossRepositoryEdgeEvidence(
            decision.DocumentEdgeId!.Value,
            consumerAnchor.Id,
            decision.ConsumerDocumentVersionId,
            decision.ConsumerRepositoryId,
            decision.ConsumerScanRunId,
            providerAnchor.Id,
            contribution.ProviderDocumentVersionId,
            decision.ProviderRepositoryId!.Value,
            decision.ProviderScanRunId!.Value,
            "legacy-consumer",
            "legacy-provider",
            decision.NormalizedEndpointIdentifier,
            decision.Protocol,
            "ExactConfigured",
            1.0000m,
            "1.0",
            "legacy-profile",
            "0.9",
            new string('8', 64),
            "legacy-occurrence",
            "legacy-consumer#line=1",
            "legacy-provider#line=1",
            new string('9', 64),
            decision.CreatedAt));
    }

    private static async Task<CanonicalPass2Seed> SeedCanonicalPass2Async(
        VulgataDbContext context,
        ExactCrossRepositoryTestFixture fixture)
    {
        ExactPass1DocumentFixture consumer = CanonicalizePass1Document(
            fixture.CreatePass1Document(ExactPass1DocumentKind.Consumer));
        ExactPass1DocumentFixture providerImplementation = CanonicalizePass1Document(
            fixture.CreatePass1Document(ExactPass1DocumentKind.Provider));
        MethodInfo seedPair = typeof(ExactCrossRepositoryResolutionTests).GetMethod(
            "SeedCurrentPairAsync",
            BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("The accepted Pass 1 pair seeder is unavailable.");
        await Assert.IsAssignableFrom<Task>(seedPair.Invoke(
            null,
            [context, fixture, consumer, providerImplementation])!);
        context.ChangeTracker.Clear();

        ExactPass1DocumentFixture providerXml = await SeedCanonicalProviderXmlAsync(
            context,
            providerImplementation);
        ExactPass1DocumentFixture unsupported =
            fixture.CreatePass1Document(ExactPass1DocumentKind.UnsupportedConsumer);
        MethodInfo seedUnsupported = typeof(Pass2FlowPlanningPostgreSqlTests).GetMethod(
            "SeedUnsupportedDocumentAsync",
            BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("The accepted unsupported-document seeder is unavailable.");
        await Assert.IsAssignableFrom<Task>(seedUnsupported.Invoke(null, [context, unsupported, default(CancellationToken)])!);
        context.ChangeTracker.Clear();

        context.DocumentEdges.Add(new DocumentEdge(
            Guid.Parse("11040000-0000-0000-0000-000000000915"),
            providerXml.DocumentVersion.Id,
            providerXml.DocumentVersion.RepositoryId,
            providerXml.DocumentVersion.ScanRunId,
            providerImplementation.DocumentVersion.Id,
            providerImplementation.DocumentVersion.RepositoryId,
            providerImplementation.DocumentVersion.ScanRunId,
            DocumentEdgeType.References,
            ExactCrossRepositoryTestValues.Pass1CompletedAt));
        await context.SaveChangesAsync();

        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        MethodInfo buildBatch = typeof(Pass2FlowPlanningPostgreSqlTests).GetMethod(
            "BuildResolutionBatch",
            BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("The accepted exact-resolution batch builder is unavailable.");
        ExactResolutionDecisionBatch batch = Assert.IsType<ExactResolutionDecisionBatch>(
            buildBatch.Invoke(null, [profile, occurrence, consumer, providerXml, unsupported]));
        ExactResolutionDecision exact = Assert.Single(
            batch.Decisions,
            value => value.Kind == ExactResolutionDecisionKind.ExactMatch);
        batch = ExactResolutionDecisionBatch.Create(
            profile,
            batch.Decisions.Select(value =>
                    value.Kind == ExactResolutionDecisionKind.UnsupportedObservation
                        ? value with { Owner = exact.Owner }
                        : value)
                .OrderBy(value => value.Identity, StringComparer.Ordinal));
        ExactResolutionBatchOutcome resolution = await new ExactCrossRepositoryResolutionStore(context)
            .ApplyAsync(
                batch,
                new RepositoryAccessScope([
                    consumer.DocumentVersion.RepositoryId,
                    providerXml.DocumentVersion.RepositoryId,
                ]));
        Assert.Equal(ExactResolutionOutcomeKind.Applied, resolution.Kind);
        CrossRepositoryResolutionDecision decision = await context
            .CrossRepositoryResolutionDecisions
            .SingleAsync(value => value.Outcome == "ExactMatch");
        Pass1PairExecution pair = await context.Pass1PairExecutions.SingleAsync();
        Assert.Equal(pair.Id, decision.Pass1PairExecutionId);
        Assert.Equal(pair.CoordinationId, decision.CoordinationId);
        Assert.Equal(pair.SystemId, decision.SystemId);
        context.ChangeTracker.Clear();
        return new(pair, consumer, providerXml, providerImplementation);
    }

    private static ExactPass1DocumentFixture CanonicalizePass1Document(
        ExactPass1DocumentFixture original)
    {
        OrdinaryPass1JobIdentity identity = OrdinaryPass1JobIdentity.Create(
            original.ScanExecution.Id,
            original.ScanRun.Id,
            original.DocumentVersion.RepositoryId,
            original.CodeUnit.Id,
            original.DocumentVersion.ProfiledRevision);
        Guid documentVersionId = OrdinaryPass1JobIdentity.CreateDocumentVersionId(identity.JobId);
        LogicalDocument logical = LogicalDocument.Create(
            original.LogicalDocument.Id,
            original.LogicalDocument.RepositoryId,
            original.LogicalDocument.DocumentType,
            original.LogicalDocument.StableKey,
            original.LogicalDocument.CreatedAt);
        DocumentVersion document = DocumentVersion.Create(
            documentVersionId,
            logical.Id,
            original.DocumentVersion.RepositoryId,
            original.DocumentVersion.SystemId,
            original.DocumentVersion.ScanRunId,
            original.DocumentVersion.ProfiledRevision,
            original.DocumentVersion.VersionNumber,
            original.DocumentVersion.Title,
            original.DocumentVersion.RepositoryRelativePath,
            original.DocumentVersion.SchemaIdentity,
            original.DocumentVersion.ContentJson,
            original.DocumentVersion.ContentMarkdown,
            original.DocumentVersion.ContentFingerprint,
            original.DocumentVersion.CommitFingerprint,
            original.DocumentVersion.CreatedAt);
        SourceAnchor anchor = new(
            original.SourceAnchor.Id,
            documentVersionId,
            original.SourceAnchor.RepositoryId,
            original.SourceAnchor.ScanRunId,
            original.SourceAnchor.CodeUnitId,
            original.SourceAnchor.ProfiledRevision,
            original.SourceAnchor.RepositoryRelativePath,
            original.SourceAnchor.EvidenceLocator,
            original.SourceAnchor.LineStart,
            original.SourceAnchor.LineEnd,
            original.SourceAnchor.SourceHash,
            original.SourceAnchor.CreatedAt);
        document.SourceAnchors.Add(anchor);
        logical.SetInitialCurrentVersion(document);
        return new(original.ScanRun, original.ScanExecution, original.CodeUnit, logical, document, anchor);
    }

    private static async Task<ExactPass1DocumentFixture> SeedCanonicalProviderXmlAsync(
        VulgataDbContext context,
        ExactPass1DocumentFixture provider)
    {
        Guid logicalDocumentId = Guid.Parse("11040000-0000-0000-0000-000000000911");
        Guid sourceAnchorId = Guid.Parse("11040000-0000-0000-0000-000000000913");
        Guid codeUnitId = Guid.Parse("11040000-0000-0000-0000-000000000914");
        var codeUnit = new CodeUnit(
            provider.ScanRun.Id,
            provider.DocumentVersion.RepositoryId,
            "src/main/resources/disgram/in/cust/updateSession.xml",
            ExactCrossRepositoryTestValues.SourceHash,
            ExactCrossRepositoryTestValues.SourceHash,
            1,
            2,
            "in/cust/updateSession",
            CodeUnitType.File,
            RangeConfidence.Exact,
            "xml");
        ExactCrossRepositoryReflection.SetId(codeUnit, codeUnitId);
        OrdinaryPass1JobIdentity identity = OrdinaryPass1JobIdentity.Create(
            provider.ScanExecution.Id,
            provider.ScanRun.Id,
            provider.DocumentVersion.RepositoryId,
            codeUnitId,
            provider.DocumentVersion.ProfiledRevision);
        Guid documentVersionId = OrdinaryPass1JobIdentity.CreateDocumentVersionId(identity.JobId);
        LogicalDocument logical = LogicalDocument.Create(
            logicalDocumentId,
            provider.DocumentVersion.RepositoryId,
            DocumentType.CodeLogic,
            "exact-pass2/provider-xml",
            ExactCrossRepositoryTestValues.CreatedAt);
        DocumentVersion document = DocumentVersion.Create(
            documentVersionId,
            logicalDocumentId,
            provider.DocumentVersion.RepositoryId,
            ExactCrossRepositoryTestValues.SystemId,
            provider.ScanRun.Id,
            provider.DocumentVersion.ProfiledRevision,
            1,
            "in/cust/updateSession",
            "src/main/resources/disgram/in/cust/updateSession.xml",
            KnowledgeDocumentLimits.SchemaIdentity,
            "{\"kind\":\"xml\",\"name\":\"in/cust/updateSession\"}",
            "# in/cust/updateSession",
            ExactCrossRepositoryTestValues.ContentFingerprint,
            ExactCrossRepositoryTestValues.CommitFingerprint,
            ExactCrossRepositoryTestValues.Pass1CompletedAt);
        SourceAnchor anchor = new(
            sourceAnchorId,
            documentVersionId,
            provider.DocumentVersion.RepositoryId,
            provider.ScanRun.Id,
            codeUnitId,
            provider.DocumentVersion.ProfiledRevision,
            document.RepositoryRelativePath,
            "provider.xml#line=2;rule=exact-xml-root-attribute-v1",
            1,
            2,
            ExactCrossRepositoryTestValues.SourceHash,
            ExactCrossRepositoryTestValues.Pass1CompletedAt);
        document.SourceAnchors.Add(anchor);
        context.CodeUnits.Add(codeUnit);
        context.LogicalDocuments.Add(logical);
        context.Entry(logical).Property(nameof(LogicalDocument.CurrentVersionId)).CurrentValue = null;
        await context.SaveChangesAsync();
        context.DocumentVersions.Add(document);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {documentVersionId} WHERE \"Id\" = {logicalDocumentId}");
        context.ChangeTracker.Clear();
        return new(provider.ScanRun, provider.ScanExecution, codeUnit, logical, document, anchor);
    }

    private async Task RevokeSystemOwnerAssignmentAsync(Guid systemId, string userId)
    {
        await using VulgataDbContext context = new(
            new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite(_factory.ConnectionString)
                .Options);
        SystemOwnerAssignment? assignment = await context.SystemOwnerAssignments
            .SingleOrDefaultAsync(value => value.SystemId == systemId && value.UserId == userId);
        Assert.NotNull(assignment);
        context.Remove(assignment);
        await context.SaveChangesAsync();
    }

    private static async Task AddCanonicalOptionalSupportingEdgeAsync(
        IServiceProvider services,
        Guid executionId)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        FlowGroup group = await context.FlowGroups
            .SingleAsync(value => value.Pass2ExecutionId == executionId);
        FlowTrace trace = await context.FlowTraces
            .SingleAsync(value => value.FlowGroupId == group.Id);
        FlowTraceStep[] existingSteps = await context.FlowTraceSteps
            .Where(value => value.FlowTraceId == trace.Id)
            .OrderBy(value => value.StepOrder)
            .ToArrayAsync();
        Assert.Equal(5, existingSteps.Length);
        Guid implementationEdgeId = Assert.IsType<Guid>(existingSteps[3].EdgeId);
        DocumentVersion providerXml = await context.DocumentVersions
            .SingleAsync(value => value.Id == group.ProviderXmlDocumentVersionId);
        DocumentVersion providerImplementation = await context.DocumentVersions
            .SingleAsync(value => value.Id == group.ProviderImplementationDocumentVersionId);
        Guid optionalEdgeId = Guid.NewGuid();
        DateTimeOffset createdAt = providerImplementation.CreatedAt.AddTicks(1);
        context.DocumentEdges.Add(new DocumentEdge(
            optionalEdgeId,
            providerImplementation.Id,
            providerImplementation.RepositoryId,
            providerImplementation.ScanRunId,
            providerXml.Id,
            providerXml.RepositoryId,
            providerXml.ScanRunId,
            DocumentEdgeType.References,
            createdAt));
        context.FlowTraceSteps.Add(new FlowTraceStep(
            Guid.NewGuid(),
            trace.Id,
            6,
            providerImplementation.Id,
            providerImplementation.RepositoryId,
            providerImplementation.ScanRunId,
            optionalEdgeId,
            FlowTraceStepKind.Relationship,
            "Same-Repository supporting relationship",
            createdAt));
        context.FlowTraceSteps.Add(new FlowTraceStep(
            Guid.NewGuid(),
            trace.Id,
            7,
            providerXml.Id,
            providerXml.RepositoryId,
            providerXml.ScanRunId,
            null,
            FlowTraceStepKind.Document,
            "Supporting Document",
            createdAt));
        CrossRepositoryResolutionDecision decision = await context
            .CrossRepositoryResolutionDecisions
            .SingleAsync(value => value.Id == group.ExactResolutionDecisionId);
        ExactResolutionProviderEvidenceContribution[] contributions = await context
            .ExactResolutionProviderEvidenceContributions
            .Where(value => value.ResolutionDecisionId == decision.Id)
            .OrderBy(value => value.ProviderOrdinal)
            .ThenBy(value => value.EvidenceOrdinal)
            .ToArrayAsync();
        string membershipFingerprint = PreparedPass2FlowPlanner.ComputeMembershipFingerprint(
            group.ConsumerDocumentVersionId!.Value,
            group.ExactResolutionEdgeId!.Value,
            group.ProviderXmlDocumentVersionId!.Value,
            implementationEdgeId,
            group.ProviderImplementationDocumentVersionId,
            [(optionalEdgeId, providerXml.Id)]);
        string planFingerprint = PreparedPass2FlowPlanner.ComputePlanFingerprint(
            decision.Id,
            decision.EvidenceFingerprint,
            "Complete",
            membershipFingerprint,
            contributions);
        SetProperty(group, nameof(FlowGroup.MembershipFingerprint), membershipFingerprint);
        SetProperty(group, nameof(FlowGroup.PlanFingerprint), planFingerprint);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task MutatePreTransportAuthorityAsync(
        IServiceProvider services,
        Guid executionId,
        string mutation)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        FlowGroup group = await context.FlowGroups
            .SingleAsync(value => value.Pass2ExecutionId == executionId);
        DocumentVersion consumer = await context.DocumentVersions
            .SingleAsync(value => value.Id == group.ConsumerDocumentVersionId);
        bool disableForeignKeys = false;
        Guid? bypassEdgeId = null;
        string? bypassProperty = null;
        switch (mutation)
        {
            case "current-pointer":
                LogicalDocument logical = await context.LogicalDocuments
                    .SingleAsync(value => value.Id == consumer.LogicalDocumentId);
                context.Entry(logical).Property(nameof(LogicalDocument.CurrentVersionId))
                    .CurrentValue = null;
                break;
            case "supersession":
                DocumentVersion successor = DocumentVersion.Create(
                    Guid.NewGuid(),
                    consumer.LogicalDocumentId,
                    consumer.RepositoryId,
                    consumer.SystemId,
                    consumer.ScanRunId,
                    consumer.ProfiledRevision,
                    consumer.VersionNumber + 1,
                    consumer.Title,
                    consumer.RepositoryRelativePath,
                    consumer.SchemaIdentity,
                    consumer.ContentJson,
                    consumer.ContentMarkdown,
                    consumer.ContentFingerprint,
                    new string('3', 64),
                    consumer.CreatedAt.AddTicks(1));
                context.DocumentVersions.Add(successor);
                consumer.SupersedeWith(successor.Id);
                break;
            case "profiled-revision":
                context.Entry(consumer).Property(nameof(DocumentVersion.ProfiledRevision))
                    .CurrentValue = new string('d', 40);
                break;
            case "exact-edge":
                DocumentEdge edge = await context.DocumentEdges
                    .SingleAsync(value => value.Id == group.ExactResolutionEdgeId);
                context.Entry(edge).Property(nameof(DocumentEdge.EdgeType))
                    .CurrentValue = DocumentEdgeType.GeneratedFrom;
                break;
            case "contribution":
                ExactResolutionProviderEvidenceContribution contribution = await context
                    .ExactResolutionProviderEvidenceContributions
                    .OrderBy(value => value.ProviderOrdinal)
                    .ThenBy(value => value.EvidenceOrdinal)
                    .FirstAsync(value => value.ResolutionDecisionId == group.ExactResolutionDecisionId);
                context.Entry(contribution)
                    .Property(nameof(ExactResolutionProviderEvidenceContribution.EvidenceFingerprint))
                    .CurrentValue = new string('e', 64);
                break;
            case "provider-evidence-set-fingerprint":
                CrossRepositoryResolutionDecision providerEvidenceDecision = await context
                    .CrossRepositoryResolutionDecisions.SingleAsync(value =>
                        value.Id == group.ExactResolutionDecisionId);
                context.Entry(providerEvidenceDecision)
                    .Property(nameof(CrossRepositoryResolutionDecision.ProviderEvidenceSetFingerprint))
                    .CurrentValue = new string('a', 64);
                break;
            case "anchor-range":
            case "anchor-hash":
                SourceAnchor anchor = await context.SourceAnchors
                    .FirstAsync(value => value.DocumentVersionId == consumer.Id);
                if (mutation == "anchor-range")
                {
                    context.Entry(anchor).Property(nameof(SourceAnchor.LineStart))
                        .CurrentValue = anchor.LineStart < anchor.LineEnd
                            ? anchor.LineStart + 1
                            : anchor.LineStart;
                }
                else
                {
                    context.Entry(anchor).Property(nameof(SourceAnchor.SourceHash))
                        .CurrentValue = new string('f', 64);
                }
                break;
            case "trace":
                Guid[] traceIds = await context.FlowTraces
                    .Where(value => value.FlowGroupId == group.Id)
                    .Select(value => value.Id)
                    .ToArrayAsync();
                FlowTraceStep step = await context.FlowTraceSteps
                    .OrderBy(value => value.StepOrder)
                    .FirstAsync(value => traceIds.Contains(value.FlowTraceId));
                context.Entry(step).Property(nameof(FlowTraceStep.Description))
                    .CurrentValue = "mutated trace semantics";
                break;
            case "plan-fingerprint":
                context.Entry(group).Property(nameof(FlowGroup.PlanFingerprint))
                    .CurrentValue = new string('1', 64);
                break;
            case "membership-fingerprint":
                context.Entry(group).Property(nameof(FlowGroup.MembershipFingerprint))
                    .CurrentValue = new string('2', 64);
                break;
            case "flow-schema":
                const string driftedFlowSchema = "endpoint-flow-group/0.9";
                await context.Database.ExecuteSqlRawAsync("PRAGMA ignore_check_constraints = ON");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"FlowGroups\" SET \"SchemaIdentity\" = {driftedFlowSchema} WHERE \"Id\" = {group.Id}");
                await context.Database.ExecuteSqlRawAsync("PRAGMA ignore_check_constraints = OFF");
                return;
            case "logical-key-schema":
                const string driftedLogicalKeySchema = "business-flow-logical-key/0.9";
                await context.Database.ExecuteSqlRawAsync("PRAGMA ignore_check_constraints = ON");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"FlowGroups\" SET \"LogicalKeySchemaIdentity\" = {driftedLogicalKeySchema} WHERE \"Id\" = {group.Id}");
                await context.Database.ExecuteSqlRawAsync("PRAGMA ignore_check_constraints = OFF");
                return;
            case "logical-document-stable-key":
                context.Entry(group).Property(nameof(FlowGroup.LogicalDocumentStableKey))
                    .CurrentValue = "business-flow/v1/drifted";
                break;
            case "consumer-entry-identity":
                context.Entry(group).Property(nameof(FlowGroup.ConsumerEntryIdentity))
                    .CurrentValue = "drifted-consumer-entry";
                break;
            case "consumer-scan-run":
                DocumentVersion provider = await context.DocumentVersions.SingleAsync(value =>
                    value.Id == group.ProviderXmlDocumentVersionId);
                CrossRepositoryResolutionDecision decision = await context
                    .CrossRepositoryResolutionDecisions.SingleAsync(value =>
                        value.Id == group.ExactResolutionDecisionId);
                context.Entry(decision)
                    .Property(nameof(CrossRepositoryResolutionDecision.ConsumerScanRunId))
                    .CurrentValue = provider.ScanRunId;
                break;
            case "provider-endpoint":
                context.Entry(group).Property(nameof(FlowGroup.NormalizedEndpointIdentifier))
                    .CurrentValue = "drifted.endpoint";
                break;
            case "provider-repository":
                CrossRepositoryResolutionDecision providerRepositoryDecision = await context
                    .CrossRepositoryResolutionDecisions.SingleAsync(value =>
                        value.Id == group.ExactResolutionDecisionId);
                context.Entry(providerRepositoryDecision)
                    .Property(nameof(CrossRepositoryResolutionDecision.ProviderRepositoryId))
                    .CurrentValue = consumer.RepositoryId;
                break;
            case "provider-scan-run":
                CrossRepositoryResolutionDecision providerScanDecision = await context
                    .CrossRepositoryResolutionDecisions.SingleAsync(value =>
                        value.Id == group.ExactResolutionDecisionId);
                context.Entry(providerScanDecision)
                    .Property(nameof(CrossRepositoryResolutionDecision.ProviderScanRunId))
                    .CurrentValue = consumer.ScanRunId;
                break;
            case "edge-direction":
                DocumentEdge directionEdge = await context.DocumentEdges.SingleAsync(value =>
                    value.Id == group.ExactResolutionEdgeId);
                Guid originalSourceDocumentVersionId = directionEdge.SourceDocumentVersionId;
                Guid originalSourceRepositoryId = directionEdge.SourceRepositoryId;
                Guid originalSourceScanRunId = directionEdge.SourceScanRunId;
                context.Entry(directionEdge)
                    .Property(nameof(DocumentEdge.SourceDocumentVersionId))
                    .CurrentValue = directionEdge.TargetDocumentVersionId;
                context.Entry(directionEdge)
                    .Property(nameof(DocumentEdge.TargetDocumentVersionId))
                    .CurrentValue = originalSourceDocumentVersionId;
                context.Entry(directionEdge)
                    .Property(nameof(DocumentEdge.SourceRepositoryId))
                    .CurrentValue = directionEdge.TargetRepositoryId;
                context.Entry(directionEdge)
                    .Property(nameof(DocumentEdge.TargetRepositoryId))
                    .CurrentValue = originalSourceRepositoryId;
                context.Entry(directionEdge)
                    .Property(nameof(DocumentEdge.SourceScanRunId))
                    .CurrentValue = directionEdge.TargetScanRunId;
                context.Entry(directionEdge)
                    .Property(nameof(DocumentEdge.TargetScanRunId))
                    .CurrentValue = originalSourceScanRunId;
                break;
            case "provider-implementation-edge-type":
            case "provider-implementation-edge-source":
            case "provider-implementation-edge-target":
            case "provider-implementation-edge-repository":
            case "provider-implementation-edge-scan-run":
            case "provider-implementation-edge-direction":
            {
                FlowTraceStep implementationStep = await context.FlowTraceSteps
                    .Where(value => value.FlowTraceId == context.FlowTraces
                        .Where(trace => trace.FlowGroupId == group.Id)
                        .Select(trace => trace.Id)
                        .Single())
                    .SingleAsync(value => value.StepOrder == 4);
                DocumentEdge implementationEdge = await context.DocumentEdges
                    .SingleAsync(value => value.Id == implementationStep.EdgeId);
                DocumentVersion implementationSource = await context.DocumentVersions
                    .SingleAsync(value => value.Id == implementationEdge.SourceDocumentVersionId);
                DocumentVersion implementationTarget = await context.DocumentVersions
                    .SingleAsync(value => value.Id == implementationEdge.TargetDocumentVersionId);
                switch (mutation)
                {
                    case "provider-implementation-edge-type":
                        context.Entry(implementationEdge).Property(nameof(DocumentEdge.EdgeType))
                            .CurrentValue = DocumentEdgeType.GeneratedFrom;
                        break;
                    case "provider-implementation-edge-source":
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.SourceDocumentVersionId))
                            .CurrentValue = implementationTarget.Id;
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.SourceRepositoryId))
                            .CurrentValue = implementationTarget.RepositoryId;
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.SourceScanRunId))
                            .CurrentValue = implementationTarget.ScanRunId;
                        break;
                    case "provider-implementation-edge-target":
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.TargetDocumentVersionId))
                            .CurrentValue = group.ConsumerDocumentVersionId!.Value;
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.TargetRepositoryId))
                            .CurrentValue = consumer.RepositoryId;
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.TargetScanRunId))
                            .CurrentValue = consumer.ScanRunId;
                        break;
                    case "provider-implementation-edge-repository":
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.SourceRepositoryId))
                            .CurrentValue = group.RepositoryId;
                        disableForeignKeys = true;
                        bypassEdgeId = implementationEdge.Id;
                        bypassProperty = nameof(DocumentEdge.SourceRepositoryId);
                        break;
                    case "provider-implementation-edge-scan-run":
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.SourceScanRunId))
                            .CurrentValue = group.ScanRunId;
                        disableForeignKeys = true;
                        bypassEdgeId = implementationEdge.Id;
                        bypassProperty = nameof(DocumentEdge.SourceScanRunId);
                        break;
                    case "provider-implementation-edge-direction":
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.SourceDocumentVersionId))
                            .CurrentValue = implementationTarget.Id;
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.TargetDocumentVersionId))
                            .CurrentValue = implementationSource.Id;
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.SourceRepositoryId))
                            .CurrentValue = implementationTarget.RepositoryId;
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.TargetRepositoryId))
                            .CurrentValue = implementationSource.RepositoryId;
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.SourceScanRunId))
                            .CurrentValue = implementationTarget.ScanRunId;
                        context.Entry(implementationEdge)
                            .Property(nameof(DocumentEdge.TargetScanRunId))
                            .CurrentValue = implementationSource.ScanRunId;
                        break;
                }
                break;
            }
            case "optional-edge-type":
            case "optional-edge-source":
            case "optional-edge-target":
            case "optional-edge-repository":
            case "optional-edge-scan-run":
            case "optional-edge-direction":
            {
                FlowGroup optionalGroup = group;
                Guid traceId = await context.FlowTraces
                    .Where(trace => trace.FlowGroupId == optionalGroup.Id)
                    .Select(trace => trace.Id)
                    .SingleAsync();
                FlowTraceStep optionalStep = await context.FlowTraceSteps
                    .SingleAsync(value => value.FlowTraceId == traceId && value.StepOrder == 6);
                DocumentEdge optionalEdge = await context.DocumentEdges
                    .SingleAsync(value => value.Id == optionalStep.EdgeId);
                DocumentVersion optionalSource = await context.DocumentVersions
                    .SingleAsync(value => value.Id == optionalEdge.SourceDocumentVersionId);
                DocumentVersion optionalTarget = await context.DocumentVersions
                    .SingleAsync(value => value.Id == optionalEdge.TargetDocumentVersionId);
                switch (mutation)
                {
                    case "optional-edge-type":
                        context.Entry(optionalEdge).Property(nameof(DocumentEdge.EdgeType))
                            .CurrentValue = DocumentEdgeType.GeneratedFrom;
                        break;
                    case "optional-edge-source":
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.SourceDocumentVersionId))
                            .CurrentValue = optionalTarget.Id;
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.SourceRepositoryId))
                            .CurrentValue = optionalTarget.RepositoryId;
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.SourceScanRunId))
                            .CurrentValue = optionalTarget.ScanRunId;
                        break;
                    case "optional-edge-target":
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.TargetDocumentVersionId))
                            .CurrentValue = optionalGroup.ConsumerDocumentVersionId!.Value;
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.TargetRepositoryId))
                            .CurrentValue = optionalGroup.RepositoryId;
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.TargetScanRunId))
                            .CurrentValue = optionalGroup.ScanRunId;
                        break;
                    case "optional-edge-repository":
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.SourceRepositoryId))
                            .CurrentValue = optionalGroup.RepositoryId;
                        disableForeignKeys = true;
                        bypassEdgeId = optionalEdge.Id;
                        bypassProperty = nameof(DocumentEdge.SourceRepositoryId);
                        break;
                    case "optional-edge-scan-run":
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.SourceScanRunId))
                            .CurrentValue = optionalGroup.ScanRunId;
                        disableForeignKeys = true;
                        bypassEdgeId = optionalEdge.Id;
                        bypassProperty = nameof(DocumentEdge.SourceScanRunId);
                        break;
                    case "optional-edge-direction":
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.SourceDocumentVersionId))
                            .CurrentValue = optionalTarget.Id;
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.TargetDocumentVersionId))
                            .CurrentValue = optionalGroup.ConsumerDocumentVersionId!.Value;
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.SourceRepositoryId))
                            .CurrentValue = optionalTarget.RepositoryId;
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.TargetRepositoryId))
                            .CurrentValue = optionalGroup.RepositoryId;
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.SourceScanRunId))
                            .CurrentValue = optionalTarget.ScanRunId;
                        context.Entry(optionalEdge)
                            .Property(nameof(DocumentEdge.TargetScanRunId))
                            .CurrentValue = optionalGroup.ScanRunId;
                        break;
                }
                break;
            }
            case "execution-worker-prompt":
            case "execution-submission-schema":
            case "execution-tool-fingerprint":
                Pass2Execution execution = await context.Pass2Executions
                    .SingleAsync(value => value.Id == executionId);
                string propertyName = mutation switch
                {
                    "execution-worker-prompt" => nameof(Pass2Execution.WorkerPromptIdentity),
                    "execution-submission-schema" => nameof(Pass2Execution.SubmissionSchemaIdentity),
                    _ => nameof(Pass2Execution.ToolDeclarationFingerprint),
                };
                string driftedValue = mutation == "execution-tool-fingerprint"
                    ? new string('0', 64)
                    : $"drifted-{mutation}";
                context.Entry(execution).Property(propertyName).CurrentValue = driftedValue;
                break;
            default:
                throw new XunitException($"Unknown authority mutation '{mutation}'.");
        }

        if (disableForeignKeys)
        {
            Assert.True(bypassEdgeId.HasValue);
            Assert.NotNull(bypassProperty);
            await context.Database.OpenConnectionAsync();
            try
            {
                await using (System.Data.Common.DbCommand pragma =
                    context.Database.GetDbConnection().CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys = OFF";
                    await pragma.ExecuteNonQueryAsync();
                }
                switch (bypassProperty)
                {
                    case nameof(DocumentEdge.SourceRepositoryId):
                        await context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE \"DocumentEdges\" SET \"SourceRepositoryId\" = {group.RepositoryId} WHERE \"Id\" = {bypassEdgeId.Value}");
                        break;
                    case nameof(DocumentEdge.SourceScanRunId):
                        await context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE \"DocumentEdges\" SET \"SourceScanRunId\" = {group.ScanRunId} WHERE \"Id\" = {bypassEdgeId.Value}");
                        break;
                    default:
                        throw new XunitException($"Unexpected edge bypass property '{bypassProperty}'.");
                }
                await using (System.Data.Common.DbCommand pragma =
                    context.Database.GetDbConnection().CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys = ON";
                    await pragma.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
            context.ChangeTracker.Clear();
            return;
        }

        await context.SaveChangesAsync();
    }

    private static async Task<IReadOnlyDictionary<Guid, OrdinaryMemberAuthority>>
        SeedOrdinaryMemberAuthoritiesAsync(IServiceProvider services, Guid executionId)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        Pass2Execution execution = await context.Pass2Executions
            .SingleAsync(value => value.Id == executionId);
        TwoRepositoryScanCoordination coordination = await context.TwoRepositoryScanCoordinations
            .SingleAsync(value => value.Id == execution.CoordinationId);
        FlowGroup[] groups = await context.FlowGroups
            .Where(value => value.Pass2ExecutionId == executionId)
            .ToArrayAsync();
        Guid[] traceIds = await context.FlowTraces
            .Where(value => groups.Select(group => group.Id).Contains(value.FlowGroupId))
            .Select(value => value.Id)
            .ToArrayAsync();
        Guid[] documentIds = groups.SelectMany(group => new[]
            {
                group.ConsumerDocumentVersionId!.Value,
                group.ProviderXmlDocumentVersionId!.Value,
                group.ProviderImplementationDocumentVersionId!.Value,
            })
            .Concat(await context.FlowTraceSteps
                .Where(value => traceIds.Contains(value.FlowTraceId))
                .Select(value => value.DocumentVersionId)
                .ToArrayAsync())
            .Distinct()
            .ToArray();
        DocumentVersion[] documents = await context.DocumentVersions
            .Where(value => documentIds.Contains(value.Id))
            .ToArrayAsync();
        SourceAnchor[] anchors = await context.SourceAnchors
            .Where(value => documentIds.Contains(value.DocumentVersionId))
            .OrderBy(value => value.Id)
            .ToArrayAsync();
        var result = new Dictionary<Guid, OrdinaryMemberAuthority>();
        var manifestOrdinals = new Dictionary<Guid, int>();
        int pairSelectionOrdinal = 0;
        foreach (DocumentVersion document in documents.OrderBy(value => value.Id))
        {
            SourceAnchor anchor = anchors.First(value => value.DocumentVersionId == document.Id);
            Guid scanExecutionId = document.RepositoryId == coordination.RepositoryOneId
                ? coordination.ScanExecutionOneId
                : coordination.ScanExecutionTwoId;
            int manifestOrdinal = manifestOrdinals.TryGetValue(document.RepositoryId, out int prior)
                ? prior + 1
                : 1;
            manifestOrdinals[document.RepositoryId] = manifestOrdinal;
            PreparedScanManifest manifest = await context.PreparedScanManifests.SingleAsync(value =>
                value.CoordinationId == coordination.Id &&
                value.RepositoryId == document.RepositoryId);
            PreparedScanManifestEntry entry = (PreparedScanManifestEntry)Activator.CreateInstance(
                typeof(PreparedScanManifestEntry),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: null,
                culture: null)!;
            SetProperty(entry, nameof(PreparedScanManifestEntry.FileCodeUnitId), anchor.CodeUnitId);
            SetProperty(entry, nameof(PreparedScanManifestEntry.PreparedScanManifestId), manifest.Id);
            SetProperty(entry, nameof(PreparedScanManifestEntry.CoordinationId), coordination.Id);
            SetProperty(
                entry,
                nameof(PreparedScanManifestEntry.FileCodeUnitIdentityFingerprint),
                Convert.ToHexStringLower(SHA256.HashData(anchor.CodeUnitId.ToByteArray())));
            SetProperty(entry, nameof(PreparedScanManifestEntry.DirectBusinessScore), 100);
            SetProperty(entry, nameof(PreparedScanManifestEntry.FinalBusinessScore), 100);
            SetProperty(entry, nameof(PreparedScanManifestEntry.SelectionReason), PreparedSelectionReason.DirectBusinessEvidence);
            SetProperty(entry, nameof(PreparedScanManifestEntry.ManifestOrdinal), manifestOrdinal);
            SetProperty(entry, nameof(PreparedScanManifestEntry.PairSelectionOrdinal), ++pairSelectionOrdinal);
            context.PreparedScanManifestEntries.Add(entry);
            ScanJobExecution scanJob = ScanJobExecution.Create(
                scanExecutionId,
                document.RepositoryId,
                document.ScanRunId,
                anchor.CodeUnitId,
                manifestOrdinal,
                DateTimeOffset.UtcNow.AddMinutes(-3));
            Guid lease = Guid.NewGuid();
            scanJob.AcquireLease(
                lease,
                DateTimeOffset.UtcNow.AddMinutes(5),
                DateTimeOffset.UtcNow.AddMinutes(-2));
            scanJob.Complete(
                scanJob.StateVersion,
                lease,
                document.Id,
                DateTimeOffset.UtcNow.AddMinutes(-1));
            context.ScanJobExecutions.Add(scanJob);
            OrdinaryPass1JobIdentity ordinary = OrdinaryPass1JobIdentity.Create(
                scanExecutionId,
                document.ScanRunId,
                document.RepositoryId,
                anchor.CodeUnitId,
                document.ProfiledRevision);
            result.Add(document.Id, new(document.Id, scanJob.Id, ordinary.JobId));
        }

        await context.SaveChangesAsync();
        return result;
    }

    private static void SetProperty<T>(object target, string propertyName, T value) =>
        target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);

    private static Dictionary<string, string> ExtractFormFields(string html)
    {
        Match form = Regex.Match(
            html,
            "<form\\b(?=[^>]*\\bmethod\\s*=\\s*[\\\"']post[\\\"'])[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(form.Success, "Expected a POST login form.");
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(
            form.Groups["content"].Value,
            "<input\\b(?<attributes>[^>]*)>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            string attributes = input.Groups["attributes"].Value;
            Match name = Regex.Match(
                attributes,
                "\\bname\\s*=\\s*[\\\"'](?<value>[^\\\"']+)[\\\"']",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!name.Success)
            {
                continue;
            }

            Match value = Regex.Match(
                attributes,
                "\\bvalue\\s*=\\s*[\\\"'](?<value>[^\\\"']*)[\\\"']",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            fields[name.Groups["value"].Value] = value.Success
                ? WebUtility.HtmlDecode(value.Groups["value"].Value)
                : string.Empty;
        }

        return fields;
    }

    private static T Property<T>(object source, string name) =>
        (T)(source.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(source) ??
            throw new InvalidOperationException($"Seed property '{name}' is unavailable."));

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.True(condition(), "The hosted Pass 2 worker did not reach the expected transport call.");
    }

    private static async Task RunManualExecutionAsync(IServiceProvider services, Guid executionId)
    {
        var activator = new PreparedPass2ExecutionActivator();
        activator.Signal(executionId);
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        PreparedPass2JobRunner runner = scope.ServiceProvider.GetRequiredService<PreparedPass2JobRunner>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await runner.RunExecutionAsync(activator.GetSlot(executionId)!, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class AuthenticatedClient(
        WebApplicationFactory<Program> host,
        HttpClient client,
        string keyRingPath,
        string userId) : IDisposable
    {
        internal HttpClient Client { get; } = client;
        internal string UserId { get; } = userId;
        internal IServiceProvider Services { get; } = host.Services;

        public void Dispose()
        {
            Client.Dispose();
            host.Dispose();
            TryDeleteDirectory(keyRingPath);
        }
    }

    private sealed record ApiPass2Seed(
        Guid PairExecutionId,
        Guid SystemId,
        Guid ConsumerDocumentVersionId,
        IReadOnlyList<Guid> RepositoryIds);

    private sealed record CanonicalPass2Seed(
        Pass1PairExecution Pair,
        ExactPass1DocumentFixture Consumer,
        ExactPass1DocumentFixture ProviderXml,
        ExactPass1DocumentFixture ProviderImplementation);

    private sealed record OrdinaryMemberAuthority(
        Guid DocumentVersionId,
        Guid ScanJobExecutionId,
        Guid OrdinaryJobId);

    private sealed class RuntimeHydrationFixture(
        LoginLogoutTests.CustomWebApplicationFactory baseFactory,
        AuthenticatedClient authenticated,
        BoundedFakeTransport transport,
        Guid executionId,
        Guid systemId,
        PreparedPass2ClaimResult claim) : IAsyncDisposable
    {
        internal IServiceProvider Services => authenticated.Services;
        internal Guid ExecutionId => executionId;
        internal Guid SystemId => systemId;
        internal string UserId => authenticated.UserId;
        internal Guid JobId => Assert.IsType<Guid>(claim.JobId);
        internal BoundedFakeTransport Transport => transport;

        internal static async Task<RuntimeHydrationFixture> CreateAsync(
            Pass2ProductionCompositionTests owner,
            string roleName = RoleNames.SystemOwner,
            bool addSystemOwnerAssignment = true)
        {
            var baseFactory = new LoginLogoutTests.CustomWebApplicationFactory();
            var transport = new BoundedFakeTransport();
            AuthenticatedClient authenticated = await owner.CreateAuthenticatedClientAsync(
                transport,
                roleName,
                baseFactory: baseFactory,
                disablePass2Worker: true);
            ApiPass2Seed seed = await owner.SeedApiPairAsync(
                authenticated.UserId,
                addSystemOwnerAssignment,
                baseFactory: baseFactory);
            using HttpResponseMessage created = await authenticated.Client.PostAsJsonAsync(
                "/api/pass2-executions",
                new { pass1PairExecutionId = seed.PairExecutionId });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Pass2ExecutionStartResponse start = await created.Content.ReadFromJsonAsync<
                Pass2ExecutionStartResponse>() ??
                throw new XunitException("The hydration fixture execution was unreadable.");
            await using AsyncServiceScope scope = authenticated.Services.CreateAsyncScope();
            PreparedPass2Store store = scope.ServiceProvider.GetRequiredService<PreparedPass2Store>();
            PreparedPass2ClaimResult claim = await store.ClaimNextAsync(
                start.Pass2ExecutionId,
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(5));
            Assert.Equal(PreparedPass2ClaimOutcome.Claimed, claim.Outcome);
            return new(
                baseFactory,
                authenticated,
                transport,
                start.Pass2ExecutionId,
                seed.SystemId,
                claim);
        }

        internal async Task<PreparedPass2DispatchHydrationResult> HydrateAsync()
        {
            await using AsyncServiceScope scope = Services.CreateAsyncScope();
            IPreparedPass2DispatchRequestSource source = scope.ServiceProvider
                .GetRequiredService<IPreparedPass2DispatchRequestSource>();
            return await source.HydrateAsync(
                claim.ExecutionId,
                Assert.IsType<Guid>(claim.JobId),
                claim.JobStateVersion,
                Assert.IsType<Guid>(claim.LeaseToken));
        }

        public ValueTask DisposeAsync()
        {
            authenticated.Dispose();
            baseFactory.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class AlwaysAvailableProviderProbe : IPreparedPass1ProviderAvailabilityProbe
    {
        public Task<bool> IsAvailableAsync(
            Guid pairExecutionId,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class BoundedFakeTransport : IPreparedPass2StreamingTransport
    {
        internal int CallCount { get; private set; }
        internal Guid LastExecutionId { get; private set; }
        internal Guid LastJobId { get; private set; }

        public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(
            PreparedPass2ProviderRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastExecutionId = ReadGuid(request.PacketUtf8.Span, "pass2ExecutionId");
            LastJobId = ReadGuid(request.PacketUtf8.Span, "jobId");
            CallCount++;
            await Task.Yield();
            yield return Encoding.UTF8.GetBytes("{\"toolCalls\":[]}");
        }

        private static Guid ReadGuid(ReadOnlySpan<byte> packet, string propertyName)
        {
            using JsonDocument document = JsonDocument.Parse(packet.ToArray());
            string? value = document.RootElement.GetProperty(propertyName).GetString();
            return Guid.Parse(value);
        }
    }

    private sealed class RecordingDispatcher : IPreparedPass2WorkerDispatcher
    {
        internal int CallCount { get; private set; }

        public Task<PreparedPass2DispatchResult> DispatchAsync(
            PreparedPass2DispatchRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new PreparedPass2DispatchResult(false, "invalid-binding"));
        }
    }

    private sealed class ThrowingScopeFactory(Exception exception) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new ThrowingScope(exception);
    }

    private sealed class ThrowingScope(Exception exception) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new ThrowingServiceProvider(exception);

        public void Dispose()
        {
        }
    }

    private sealed class ThrowingServiceProvider(Exception exception) : IServiceProvider
    {
        public object? GetService(Type serviceType) => throw exception;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        internal ConcurrentQueue<string> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(formatter(state, exception));
            if (exception is not null)
            {
                entries.Enqueue(exception.ToString());
            }
        }

        private sealed class NullScope : IDisposable
        {
            internal static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
