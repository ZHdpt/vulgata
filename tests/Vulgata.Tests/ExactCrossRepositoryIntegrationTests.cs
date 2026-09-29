using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Git;
using Vulgata.Web.Data;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Tests;

public sealed class ExactCrossRepositoryIntegrationTests
{
    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void ResolveAfterPass1Async_ProductionCoordinator_ExposesBoundedContract()
    {
        Type coordinator = typeof(ExactCrossRepositoryResolutionCoordinator);

        var method = coordinator.GetMethod("ResolveAfterPass1Async");

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<ExactResolutionOutcome>), method.ReturnType);
        Assert.Equal(
            [
                typeof(ExactResolutionRuntimeRequest),
                typeof(RepositoryAccessScope),
                typeof(CancellationToken),
            ],
            method.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ResolveAfterPass1Async_OneExecutionNotReady_WaitsBeforeGitSourceOrWriteAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ExactCrossRepositoryManagedFixture managed = fixture.CreateManagedRepositories();
        await using VulgataDbContext context = fixture.Database.CreateContext();
        context.Systems.Add(managed.System);
        await context.SaveChangesAsync();
        ScanExecution account = ReadyExecution(
            ExactCrossRepositoryTestValues.ConsumerScanExecutionId,
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ConsumerScanRunId,
            ProfileCommit(profile, "mbs_account"));
        ScanExecution channel = ScanExecution.Start(
            ExactCrossRepositoryTestValues.ProviderScanExecutionId,
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.SystemId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            ExactCrossRepositoryTestValues.ProviderPreScanResultId,
            ProfileCommit(profile, "mbs_channel"),
            ExactCrossRepositoryTestValues.CreatedAt);
        channel.Plan(1, ExactCrossRepositoryTestValues.CreatedAt);
        var executionStore = new StubScanExecutionStore(account, channel);
        var snapshotService = new ProbeSnapshotService();
        var resolver = new ProbeResolver();
        var store = new ProbeStore();
        var coordinator = new ExactCrossRepositoryResolutionCoordinator(
            new ExactResolutionProfileCatalog(),
            new StubUserContext(),
            new SystemRepository(context),
            new RepositoryRepository(context),
            executionStore,
            new FixedAuthorizationVerifier(true),
            snapshotService,
            new ExactCandidateExtractors(context),
            resolver,
            store,
            context);

        ExactResolutionOutcome outcome = await coordinator.ResolveAfterPass1Async(
            Request(profile),
            fixture.Access.AuthorizedScope);

        Assert.Equal(ExactResolutionOutcomeKind.WaitingForPass1, outcome.Kind);
        Assert.Equal(0, snapshotService.CallCount);
        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, store.CallCount);
        Assert.Equal(2, executionStore.GetCallCount);
        Assert.Empty(context.DocumentEdges);
        Assert.Empty(context.ExactCrossRepositoryEdgeEvidence);
        Assert.Empty(context.Uncertainties);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ResolveAfterPass1Async_OneEndpointUnauthorized_RefusesBeforeBindingOrAccessAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        await using VulgataDbContext context = fixture.Database.CreateContext(fixture.Access);
        var executionStore = new StubScanExecutionStore();
        var snapshotService = new ProbeSnapshotService();
        var resolver = new ProbeResolver();
        var store = new ProbeStore();
        var coordinator = new ExactCrossRepositoryResolutionCoordinator(
            new ExactResolutionProfileCatalog(),
            new StubUserContext(),
            new SystemRepository(context),
            new RepositoryRepository(context),
            executionStore,
            new FixedAuthorizationVerifier(false),
            snapshotService,
            new ExactCandidateExtractors(context),
            resolver,
            store,
            context);
        fixture.Access.ResetDataAccessCounters();

        ExactResolutionOutcome outcome = await coordinator.ResolveAfterPass1Async(
            Request(profile),
            fixture.Access.AuthorizedScope);

        Assert.Equal(ExactResolutionOutcomeKind.Refused, outcome.Kind);
        Assert.Equal("refused", outcome.Code);
        Assert.True(fixture.Access.Snapshot().IsZero);
        Assert.Equal(0, executionStore.GetCallCount);
        Assert.Equal(0, snapshotService.CallCount);
        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, store.CallCount);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ResolveAfterPass1Async_CancelledBeforeAccess_ReturnsBoundedCancelledAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        await using VulgataDbContext context = fixture.Database.CreateContext(fixture.Access);
        var executionStore = new StubScanExecutionStore();
        var snapshotService = new ProbeSnapshotService();
        var resolver = new ProbeResolver();
        var store = new ProbeStore();
        var coordinator = new ExactCrossRepositoryResolutionCoordinator(
            new ExactResolutionProfileCatalog(),
            new StubUserContext(),
            new SystemRepository(context),
            new RepositoryRepository(context),
            executionStore,
            new FixedAuthorizationVerifier(true),
            snapshotService,
            new ExactCandidateExtractors(context),
            resolver,
            store,
            context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        fixture.Access.ResetDataAccessCounters();

        ExactResolutionOutcome outcome = await coordinator.ResolveAfterPass1Async(
            Request(profile),
            fixture.Access.AuthorizedScope,
            cancellation.Token);

        Assert.Equal(ExactResolutionOutcomeKind.Cancelled, outcome.Kind);
        Assert.Equal("cancelled", outcome.Code);
        Assert.True(fixture.Access.Snapshot().IsZero);
        Assert.Equal(0, executionStore.GetCallCount);
        Assert.Equal(0, snapshotService.CallCount);
        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, store.CallCount);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void ProductionComposition_ExactServicesResolve_OrdinaryGitRegistrationIsUnchanged()
    {
        using var databaseFactory = new LoginLogoutTests.CustomWebApplicationFactory();
        using WebApplicationFactory<Program> factory = databaseFactory.WithWebHostBuilder(_ => { });
        using IServiceScope scope = factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.IsType<ExactResolutionProfileCatalog>(
            services.GetRequiredService<ExactResolutionProfileCatalog>());
        Assert.IsType<ExactGitRevisionSnapshotService>(
            services.GetRequiredService<IExactGitRevisionSnapshotService>());
        Assert.IsType<ExactCandidateExtractors>(
            services.GetRequiredService<ExactCandidateExtractors>());
        Assert.IsType<ExactCrossRepositoryResolver>(
            services.GetRequiredService<IExactCrossRepositoryResolver>());
        Assert.IsType<ExactCrossRepositoryResolutionStore>(
            services.GetRequiredService<IExactCrossRepositoryResolutionStore>());
        Assert.IsType<RepositoryAuthorizationVerifier>(
            services.GetRequiredService<IRepositoryAuthorizationVerifier>());
        Assert.IsType<ExactCrossRepositoryResolutionCoordinator>(
            services.GetRequiredService<ExactCrossRepositoryResolutionCoordinator>());
        Assert.IsType<GitWorkingCopyService>(
            services.GetRequiredService<IGitWorkingCopyService>());
        Assert.Single(services.GetServices<IGitWorkingCopyService>());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ResolveAfterPass1Async_ReviewedBoundary_AppliesExactAndUnsupportedOnceAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Consumer,
            ProfileCommit(profile, "mbs_account"));
        ExactPass1DocumentFixture provider = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Provider,
            ProfileCommit(profile, "mbs_channel"));
        ExactPass1DocumentFixture unsupported = CreateUnsupportedOnConsumerScanRun(consumer);
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentBoundaryAsync(
            context,
            fixture,
            consumer,
            provider,
            unsupported);
        PendingContextChange queuedContext = await context.PendingContextChanges
            .AsNoTracking()
            .SingleAsync();
        var snapshotService = new FixtureSnapshotService(fixture);
        var coordinator = new ExactCrossRepositoryResolutionCoordinator(
            new ExactResolutionProfileCatalog(),
            new StubUserContext(),
            new SystemRepository(context),
            new RepositoryRepository(context),
            new StubScanExecutionStore(consumer.ScanExecution, provider.ScanExecution),
            new FixedAuthorizationVerifier(true),
            snapshotService,
            new ExactCandidateExtractors(context),
            new ExactCrossRepositoryResolver(),
            new ExactCrossRepositoryResolutionStore(context),
            context);

        ExactResolutionOutcome first = await coordinator.ResolveAfterPass1Async(
            Request(profile),
            fixture.Access.AuthorizedScope);
        ExactResolutionOutcome replay = await coordinator.ResolveAfterPass1Async(
            Request(profile),
            fixture.Access.AuthorizedScope);

        Assert.Equal(ExactResolutionOutcomeKind.Applied, first.Kind);
        Assert.Equal(ExactResolutionOutcomeKind.AlreadyApplied, replay.Kind);
        Assert.Equal(4, snapshotService.CallCount);
        Assert.Equal(1, await context.DocumentEdges.CountAsync());
        Assert.Equal(1, await context.ExactCrossRepositoryEdgeEvidence.CountAsync());
        Assert.Equal(1, await context.Uncertainties.CountAsync());
        DocumentEdge edge = await context.DocumentEdges.AsNoTracking().SingleAsync();
        Assert.Equal(DocumentEdgeType.References, edge.EdgeType);
        Assert.Equal(consumer.DocumentVersion.Id, edge.SourceDocumentVersionId);
        Assert.Equal(provider.DocumentVersion.Id, edge.TargetDocumentVersionId);
        ExactCrossRepositoryEdgeEvidence evidence =
            await context.ExactCrossRepositoryEdgeEvidence.AsNoTracking().SingleAsync();
        Assert.Equal("cust/updateSession", evidence.ConsumerRawIdentifier);
        Assert.Equal("in/cust/updateSession", evidence.ProviderRawIdentifier);
        Assert.Equal("cust/updateSession", evidence.NormalizedEndpointIdentifier);
        Uncertainty uncertainty = await context.Uncertainties.AsNoTracking().SingleAsync();
        Assert.Equal("unresolved", uncertainty.Status);
        Assert.Equal("unknown_target", uncertainty.SubStatus);
        Assert.Equal("unsupported_kind", uncertainty.ReasonCode);
        Assert.Equal("file_storage", uncertainty.ObservationKind);
        PendingContextChange unchangedContext = await context.PendingContextChanges
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(queuedContext.Id, unchangedContext.Id);
        Assert.Equal(queuedContext.Context, unchangedContext.Context);
        Assert.Equal(queuedContext.UpdatedAt, unchangedContext.UpdatedAt);
        Assert.DoesNotContain(
            await context.DocumentEdges.AsNoTracking().ToArrayAsync(),
            value => value.SourceDocumentVersionId == unsupported.DocumentVersion.Id);

        var knowledge = new KnowledgeStoreRepository(
            context,
            new KnowledgeDocumentMaterializer());
        CurrentKnowledge authorized = Assert.IsType<CurrentKnowledge>(
            await knowledge.GetCurrentAsync(
                consumer.LogicalDocument.Id,
                fixture.Access.AuthorizedScope,
                []));
        Assert.IsType<AuthorizedExactRelationship>(Assert.Single(authorized.Relationships));
        CurrentKnowledge restricted = Assert.IsType<CurrentKnowledge>(
            await knowledge.GetCurrentAsync(
                consumer.LogicalDocument.Id,
                fixture.Access.ConsumerOnlyScope,
                []));
        Assert.IsType<RestrictedRelationship>(Assert.Single(restricted.Relationships));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ResolverComposition_ReverseDirectionAndSameNameRemainOccurrenceLocalAsync()
    {
        ExactResolutionProfile reviewed = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(reviewed.Occurrences);
        ExactResolutionProfile reverse = SyntheticProfile(
            reviewed,
            new ResolutionOccurrence(
                "channel-to-account-synthetic-v1",
                occurrence.Protocol,
                "mbs_channel",
                "mbs_account",
                occurrence.Consumer,
                occurrence.Provider,
                occurrence.Normalization,
                occurrence.Comparison));
        ResolutionOccurrence reverseOccurrence = Assert.Single(reverse.Occurrences);
        ExactResolutionOwner channelConsumer = new(
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
            ExactCrossRepositoryTestValues.ProviderSourceAnchorId,
            ProfileCommit(reverse, "mbs_channel"));
        ExactResolutionOwner accountProvider = new(
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ConsumerScanRunId,
            ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
            ExactCrossRepositoryTestValues.ConsumerSourceAnchorId,
            ProfileCommit(reverse, "mbs_account"));
        var consumer = new ConsumerDispatchCandidate(
            reverseOccurrence.OccurrenceId,
            channelConsumer,
            reverseOccurrence.Consumer.Invocation.Form,
            reverseOccurrence.Consumer.Invocation.ReceiverSymbol,
            reverseOccurrence.Consumer.Invocation.MemberName,
            reverseOccurrence.Consumer.Invocation.EndpointArgumentIndex,
            reverseOccurrence.Consumer.Invocation.Line,
            reverseOccurrence.Consumer.Invocation.OccurrenceOrdinalOnLine,
            "cust/updateSession",
            "cust/updateSession",
            "channel.java#line=45;rule=exact-java-direct-literal-v1");
        var configuredProvider = new ProviderEndpointCandidate(
            reverseOccurrence.OccurrenceId,
            accountProvider,
            "in/cust/updateSession",
            "cust/updateSession",
            "account.xml#line=2;rule=exact-xml-root-attribute-v1");
        var sameNameConsumerSideDecoy = new ProviderEndpointCandidate(
            reverseOccurrence.OccurrenceId,
            channelConsumer,
            "in/cust/updateSession",
            "cust/updateSession",
            "channel.xml#line=2;rule=exact-xml-root-attribute-v1");
        var unsupported = new ExactResolutionDecision(
            Assert.Single(reverse.UnsupportedObservations).ObservationId,
            ExactResolutionDecisionKind.UnsupportedObservation,
            accountProvider,
            null,
            "unsupported_kind",
            "account.java#line=66;rule=unsupported-member-invocation-v1");

        ExactResolutionDecisionBatch batch = await new ExactCrossRepositoryResolver()
            .ResolveAsync(
                reverse,
                [consumer],
                [sameNameConsumerSideDecoy, configuredProvider],
                [unsupported]);

        ExactResolutionDecision exact = Assert.Single(
            batch.Decisions,
            decision => decision.Kind == ExactResolutionDecisionKind.ExactMatch);
        Assert.Equal(channelConsumer.RepositoryId, exact.Owner.RepositoryId);
        Assert.Equal(accountProvider.RepositoryId, exact.Target?.RepositoryId);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [InlineData("malformed_provider")]
    [InlineData("unmatched")]
    [InlineData("normalization_mismatch")]
    [InlineData("multiple_providers")]
    public async Task ResolveAfterPass1Async_RealProviderMatrix_PersistsExpectedReplayStableOutcomeAsync(
        string reason)
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactResolutionProfile reviewed = new ExactResolutionProfileCatalog().Load();
        ExactResolutionProfile profile = ProviderNegativeProfile(reviewed, reason);
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        MutateProviderNegativeSource(fixture, occurrence, reason);
        ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Consumer,
            ProfileCommit(profile, "mbs_account"));
        ExactPass1DocumentFixture provider = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Provider,
            ProfileCommit(profile, "mbs_channel"));
        ExactPass1DocumentFixture unsupported = CreateUnsupportedOnConsumerScanRun(consumer);
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentBoundaryAsync(context, fixture, consumer, provider, unsupported);
        ExactCrossRepositoryResolutionCoordinator coordinator = CoordinatorWithProfile(
            profile,
            fixture,
            consumer,
            provider,
            context);

        ExactResolutionOutcome first = await coordinator.ResolveAfterPass1Async(
            Request(profile),
            fixture.Access.AuthorizedScope);
        ExactResolutionOutcome replay = await coordinator.ResolveAfterPass1Async(
            Request(profile),
            fixture.Access.AuthorizedScope);

        Assert.Equal(ExactResolutionOutcomeKind.Applied, first.Kind);
        Assert.Equal(ExactResolutionOutcomeKind.AlreadyApplied, replay.Kind);
        Uncertainty[] uncertainties = await context.Uncertainties.AsNoTracking().ToArrayAsync();

        if (reason == "multiple_providers")
        {
            // The mutated fixture has two evidence observations, but both are
            // owned by the same current provider Document Version. Provider
            // multiplicity is defined over distinct current Document Versions,
            // so this is an exact result plus the unrelated unsupported row.
            Assert.Single(await context.DocumentEdges.AsNoTracking().ToArrayAsync());
            Assert.Single(await context.ExactCrossRepositoryEdgeEvidence.AsNoTracking().ToArrayAsync());
            Uncertainty unsupportedDiagnostic = Assert.Single(uncertainties);
            Assert.Equal("unsupported_kind", unsupportedDiagnostic.ReasonCode);
            Assert.Equal("unresolved", unsupportedDiagnostic.Status);
            Assert.Equal("unknown_target", unsupportedDiagnostic.SubStatus);
            return;
        }

        Assert.Empty(context.DocumentEdges);
        Assert.Empty(context.ExactCrossRepositoryEdgeEvidence);
        Uncertainty firstDiagnostic = await context.Uncertainties.AsNoTracking()
            .SingleAsync(uncertainty => uncertainty.OccurrenceId == occurrence.OccurrenceId);
        Uncertainty diagnostic = Assert.Single(
            uncertainties,
            uncertainty => uncertainty.OccurrenceId == occurrence.OccurrenceId);
        Assert.Equal(firstDiagnostic.Id, diagnostic.Id);
        Assert.Equal(firstDiagnostic.CreatedAt, diagnostic.CreatedAt);
        Assert.Equal(consumer.DocumentVersion.Id, diagnostic.DocumentVersionId);
        Assert.Equal(consumer.SourceAnchor.Id, diagnostic.SourceAnchorId);
        Assert.Equal("unresolved", diagnostic.Status);
        Assert.Equal(
            reason == "multiple_providers" ? "ambiguous" : "unknown_target",
            diagnostic.SubStatus);
        Assert.Equal(reason, diagnostic.ReasonCode);
        Assert.Null(diagnostic.TargetRepositoryId);
        Assert.StartsWith("src/main/", diagnostic.EvidenceLocator, StringComparison.Ordinal);
        Assert.DoesNotContain(
            fixture.ConsumerRepository.RootPath,
            diagnostic.EvidenceLocator,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            fixture.ProviderRepository.RootPath,
            diagnostic.EvidenceLocator,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, uncertainties.Length);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [InlineData("zero")]
    [InlineData("multiple")]
    public async Task ResolveAfterPass1Async_StructurallyInvalidProviderWithoutUniqueCurrentOwner_RefusesWithoutWriteAsync(
        string ownerMapping)
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        fixture.ProviderRepository.Write(
            Assert.Single(occurrence.Provider.Files),
            "<?xml version=\"1.0\"?>\n<trans/>");
        ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Consumer,
            ProfileCommit(profile, "mbs_account"));
        ExactPass1DocumentFixture provider = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Provider,
            ProfileCommit(profile, "mbs_channel"));
        if (ownerMapping == "multiple")
        {
            provider.DocumentVersion.SourceAnchors.Add(new SourceAnchor(
                ExactCrossRepositoryTestValues.Id(799),
                provider.DocumentVersion.Id,
                provider.DocumentVersion.RepositoryId,
                provider.DocumentVersion.ScanRunId,
                provider.CodeUnit.Id,
                provider.DocumentVersion.ProfiledRevision,
                provider.SourceAnchor.RepositoryRelativePath,
                "duplicate-provider-anchor#line=1",
                provider.SourceAnchor.LineStart,
                provider.SourceAnchor.LineEnd,
                ExactCrossRepositoryTestValues.SourceHash,
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
        }

        ExactPass1DocumentFixture unsupported = CreateUnsupportedOnConsumerScanRun(consumer);
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentBoundaryAsync(context, fixture, consumer, provider, unsupported);
        if (ownerMapping == "zero")
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = NULL WHERE \"Id\" = {provider.LogicalDocument.Id}");
            context.ChangeTracker.Clear();
        }

        var coordinator = new ExactCrossRepositoryResolutionCoordinator(
            new ExactResolutionProfileCatalog(),
            new StubUserContext(),
            new SystemRepository(context),
            new RepositoryRepository(context),
            new StubScanExecutionStore(consumer.ScanExecution, provider.ScanExecution),
            new FixedAuthorizationVerifier(true),
            new FixtureSnapshotService(fixture),
            new ExactCandidateExtractors(context),
            new ExactCrossRepositoryResolver(),
            new ExactCrossRepositoryResolutionStore(context),
            context);

        ExactResolutionOutcome first = await coordinator.ResolveAfterPass1Async(
            Request(profile),
            fixture.Access.AuthorizedScope);
        ExactResolutionOutcome replay = await coordinator.ResolveAfterPass1Async(
            Request(profile),
            fixture.Access.AuthorizedScope);

        Assert.Equal(ExactResolutionOutcomeKind.Refused, first.Kind);
        Assert.Equal(ExactResolutionOutcomeKind.Refused, replay.Kind);
        Assert.Empty(context.DocumentEdges);
        Assert.Empty(context.ExactCrossRepositoryEdgeEvidence);
        Assert.Empty(context.Uncertainties);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [InlineData("variable_target")]
    [InlineData("disallowed_invocation")]
    [InlineData("malformed_provider")]
    [InlineData("provider_out_of_root")]
    public async Task ResolveAfterPass1Async_RealEligibleSourceFailure_PersistsOneIdempotentOwnerAnchoredUncertaintyAsync(
        string reason)
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        MutateEligibleSourceFailure(fixture, occurrence, reason);
        ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Consumer,
            ProfileCommit(profile, "mbs_account"));
        ExactPass1DocumentFixture provider = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Provider,
            ProfileCommit(profile, "mbs_channel"));
        ExactPass1DocumentFixture unsupported = CreateUnsupportedOnConsumerScanRun(consumer);
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentBoundaryAsync(context, fixture, consumer, provider, unsupported);
        var coordinator = new ExactCrossRepositoryResolutionCoordinator(
            new ExactResolutionProfileCatalog(),
            new StubUserContext(),
            new SystemRepository(context),
            new RepositoryRepository(context),
            new StubScanExecutionStore(consumer.ScanExecution, provider.ScanExecution),
            new FixedAuthorizationVerifier(true),
            new FixtureSnapshotService(fixture),
            new ExactCandidateExtractors(context),
            new ExactCrossRepositoryResolver(),
            new ExactCrossRepositoryResolutionStore(context),
            context);

        ExactResolutionOutcome first = await coordinator.ResolveAfterPass1Async(
            Request(profile),
            fixture.Access.AuthorizedScope);
        ExactResolutionOutcome replay = await coordinator.ResolveAfterPass1Async(
            Request(profile),
            fixture.Access.AuthorizedScope);

        Assert.Equal(ExactResolutionOutcomeKind.Applied, first.Kind);
        Assert.Equal(ExactResolutionOutcomeKind.AlreadyApplied, replay.Kind);
        Assert.Empty(context.DocumentEdges);
        Assert.Empty(context.ExactCrossRepositoryEdgeEvidence);
        Uncertainty[] uncertainties = await context.Uncertainties.AsNoTracking().ToArrayAsync();
        Uncertainty diagnostic = Assert.Single(
            uncertainties,
            uncertainty => uncertainty.OccurrenceId == occurrence.OccurrenceId);
        Assert.Equal(consumer.DocumentVersion.Id, diagnostic.DocumentVersionId);
        Assert.Equal(consumer.SourceAnchor.Id, diagnostic.SourceAnchorId);
        Assert.Equal("unresolved", diagnostic.Status);
        Assert.Equal("unknown_target", diagnostic.SubStatus);
        Assert.Equal(reason, diagnostic.ReasonCode);
        Assert.Null(diagnostic.TargetRepositoryId);
        Assert.StartsWith("src/main/", diagnostic.EvidenceLocator, StringComparison.Ordinal);
        Assert.DoesNotContain(
            fixture.ConsumerRepository.RootPath,
            diagnostic.EvidenceLocator,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            fixture.ProviderRepository.RootPath,
            diagnostic.EvidenceLocator,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, uncertainties.Length);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ResolveAfterPass1Async_OneLeaseCleanupFails_AttemptsEveryLeaseAndReturnsBoundedFailureAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Consumer,
            ProfileCommit(profile, "mbs_account"));
        ExactPass1DocumentFixture provider = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Provider,
            ProfileCommit(profile, "mbs_channel"));
        ExactPass1DocumentFixture unsupported = CreateUnsupportedOnConsumerScanRun(consumer);
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentBoundaryAsync(context, fixture, consumer, provider, unsupported);
        var snapshotService = new CleanupProbeSnapshotService(fixture);
        var coordinator = new ExactCrossRepositoryResolutionCoordinator(
            new ExactResolutionProfileCatalog(),
            new StubUserContext(),
            new SystemRepository(context),
            new RepositoryRepository(context),
            new StubScanExecutionStore(consumer.ScanExecution, provider.ScanExecution),
            new FixedAuthorizationVerifier(true),
            snapshotService,
            new ExactCandidateExtractors(context),
            new ExactCrossRepositoryResolver(),
            new ExactCrossRepositoryResolutionStore(context),
            context);
        ExactResolutionOutcome? outcome = null;

        Exception? exception = await Record.ExceptionAsync(async () =>
        {
            outcome = await coordinator.ResolveAfterPass1Async(
                Request(profile),
                fixture.Access.AuthorizedScope);
        });

        Assert.Null(exception);
        Assert.NotNull(outcome);
        Assert.Equal(ExactResolutionOutcomeKind.Failed, outcome.Kind);
        Assert.Equal("cleanup_failed", outcome.Code);
        Assert.Equal(2, snapshotService.DisposeAttempts);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ProductionGitComposition_ExactAndOrdinaryAcquisitionRemainConcurrentAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        RepositoryEntity repository = fixture.CreateManagedRepositories().ConsumerRepository;
        string storageRoot = ExactCrossRepositoryCleanup.CreateUniqueRoot("g11-concurrent");
        try
        {
            var options = new GitWorkingCopyOptions(storageRoot);
            var ordinaryService = new GitWorkingCopyService(options);
            var exactService = new ExactGitRevisionSnapshotService(options);

            Task<GitWorkingCopyResult> ordinaryTask = ordinaryService.AcquireAsync(
                repository,
                Guid.NewGuid());
            Task<ICommittedRepositorySnapshotLease> exactTask = exactService.AcquireAsync(
                repository,
                Guid.NewGuid(),
                ExactCrossRepositoryTestValues.ExpectedRef,
                fixture.ConsumerRepository.Commit);
            GitWorkingCopyResult ordinary = await ordinaryTask;
            await using ICommittedRepositorySnapshotLease exact = await exactTask;

            Assert.True(ordinary.Succeeded);
            Assert.Equal(fixture.ConsumerRepository.Commit, ordinary.ProfiledRevision);
            Assert.Equal(fixture.ConsumerRepository.Commit, exact.ProfiledRevision);
            Assert.NotEqual(ordinary.WorkingCopyPath, exact.RootPath);
            GitWorkingCopyResult replay = await ordinaryService.AcquireAsync(
                repository,
                Guid.NewGuid());
            Assert.True(replay.Succeeded);
            Assert.Equal(ordinary.WorkingCopyPath, replay.WorkingCopyPath);
        }
        finally
        {
            await ExactCrossRepositoryCleanup.DeleteAsync(storageRoot);
        }
    }

    private static ExactResolutionRuntimeRequest Request(ExactResolutionProfile profile) =>
        ExactResolutionRuntimeRequest.Create(
            ExactCrossRepositoryTestValues.SystemId,
            profile,
            [
                new(
                    "mbs_account",
                    "mbs_account",
                    ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                    ExactCrossRepositoryTestValues.ConsumerScanRunId,
                    ExactCrossRepositoryTestValues.ConsumerScanExecutionId),
                new(
                    "mbs_channel",
                    "mbs_channel",
                    ExactCrossRepositoryTestValues.ProviderRepositoryId,
                    ExactCrossRepositoryTestValues.ProviderScanRunId,
                    ExactCrossRepositoryTestValues.ProviderScanExecutionId),
            ]);

    private static ScanExecution ReadyExecution(
        Guid executionId,
        Guid repositoryId,
        Guid scanRunId,
        string revision)
    {
        ScanExecution execution = ScanExecution.Start(
            executionId,
            repositoryId,
            ExactCrossRepositoryTestValues.SystemId,
            scanRunId,
            ExactCrossRepositoryTestValues.ConsumerPreScanResultId,
            revision,
            ExactCrossRepositoryTestValues.CreatedAt);
        Guid jobId = ExactCrossRepositoryTestValues.ConsumerPass1JobId;
        execution.Plan(1, ExactCrossRepositoryTestValues.CreatedAt);
        execution.Advance(jobId);
        execution.RecordValid(jobId);
        execution.CompletePass1();
        return execution;
    }

    private static ExactPass1DocumentFixture CreateUnsupportedOnConsumerScanRun(
        ExactPass1DocumentFixture consumer)
    {
        const string path =
            "src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java";
        const string symbol = "cn.com.yitong.actions.cust.UploadOcrToOssOp";
        var codeUnit = new CodeUnit(
            consumer.ScanRun.Id,
            consumer.ScanRun.RepositoryId,
            path,
            ExactCrossRepositoryTestValues.SourceHash,
            ExactCrossRepositoryTestValues.SourceHash,
            60,
            70,
            symbol,
            CodeUnitType.Class,
            RangeConfidence.Exact,
            "java");
        ExactCrossRepositoryReflection.SetId(
            codeUnit,
            ExactCrossRepositoryTestValues.UnsupportedCodeUnitId);
        consumer.ScanRun.CodeUnits.Add(codeUnit);
        LogicalDocument logicalDocument = LogicalDocument.Create(
            ExactCrossRepositoryTestValues.UnsupportedLogicalDocumentId,
            consumer.ScanRun.RepositoryId,
            DocumentType.CodeLogic,
            "exact-pass1/unsupported-consumer",
            ExactCrossRepositoryTestValues.CreatedAt);
        DocumentVersion version = DocumentVersion.Create(
            ExactCrossRepositoryTestValues.UnsupportedDocumentVersionId,
            logicalDocument.Id,
            consumer.ScanRun.RepositoryId,
            ExactCrossRepositoryTestValues.SystemId,
            consumer.ScanRun.Id,
            consumer.ScanRun.ProfiledRevision!,
            1,
            symbol,
            path,
            KnowledgeDocumentLimits.SchemaIdentity,
            "{}",
            $"# {symbol}",
            ExactCrossRepositoryTestValues.ContentFingerprint,
            ExactCrossRepositoryTestValues.CommitFingerprint,
            ExactCrossRepositoryTestValues.Pass1CompletedAt);
        var anchor = new SourceAnchor(
            ExactCrossRepositoryTestValues.UnsupportedSourceAnchorId,
            version.Id,
            consumer.ScanRun.RepositoryId,
            consumer.ScanRun.Id,
            codeUnit.Id,
            consumer.ScanRun.ProfiledRevision!,
            path,
            "UploadOcrToOssOp.java#line=66;ordinal=0",
            60,
            70,
            ExactCrossRepositoryTestValues.SourceHash,
            ExactCrossRepositoryTestValues.Pass1CompletedAt);
        version.SourceAnchors.Add(anchor);
        logicalDocument.SetInitialCurrentVersion(version);
        return new(
            consumer.ScanRun,
            consumer.ScanExecution,
            codeUnit,
            logicalDocument,
            version,
            anchor);
    }

    private static async Task SeedCurrentBoundaryAsync(
        VulgataDbContext context,
        ExactCrossRepositoryTestFixture fixture,
        ExactPass1DocumentFixture consumer,
        ExactPass1DocumentFixture provider,
        ExactPass1DocumentFixture unsupported)
    {
        ExactCrossRepositoryManagedFixture managed = fixture.CreateManagedRepositories();
        context.Systems.Add(managed.System);
        context.PendingContextChanges.Add(new PendingContextChange(
            ContextScopeType.System,
            ExactCrossRepositoryTestValues.SystemId.ToString(),
            "queued-system-context-must-remain-advisory",
            ExactCrossRepositoryTestValues.CreatedAt));
        context.ScanRuns.AddRange(consumer.ScanRun, provider.ScanRun);
        await context.SaveChangesAsync();
        context.LogicalDocuments.AddRange(
            LogicalDocument.Create(
                consumer.LogicalDocument.Id,
                consumer.DocumentVersion.RepositoryId,
                DocumentType.CodeLogic,
                "exact-pass1/consumer",
                ExactCrossRepositoryTestValues.CreatedAt),
            LogicalDocument.Create(
                provider.LogicalDocument.Id,
                provider.DocumentVersion.RepositoryId,
                DocumentType.CodeLogic,
                "exact-pass1/provider",
                ExactCrossRepositoryTestValues.CreatedAt),
            LogicalDocument.Create(
                unsupported.LogicalDocument.Id,
                unsupported.DocumentVersion.RepositoryId,
                DocumentType.CodeLogic,
                "exact-pass1/unsupported-consumer",
                ExactCrossRepositoryTestValues.CreatedAt));
        await context.SaveChangesAsync();
        context.DocumentVersions.AddRange(
            consumer.DocumentVersion,
            provider.DocumentVersion,
            unsupported.DocumentVersion);
        context.IndexEntries.Add(IndexEntry.Create(
            ExactCrossRepositoryTestValues.Id(10_001),
            consumer.LogicalDocument.Id,
            consumer.DocumentVersion.Id,
            consumer.DocumentVersion.RepositoryId,
            ExactCrossRepositoryTestValues.SystemId,
            DocumentType.CodeLogic,
            "exact relationship",
            "safe summary",
            consumer.DocumentVersion.RepositoryRelativePath,
            "[]",
            "[]",
            "[]",
            "safe summary",
            ExactCrossRepositoryTestValues.CreatedAt));
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {consumer.DocumentVersion.Id} WHERE \"Id\" = {consumer.LogicalDocument.Id}");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {provider.DocumentVersion.Id} WHERE \"Id\" = {provider.LogicalDocument.Id}");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {unsupported.DocumentVersion.Id} WHERE \"Id\" = {unsupported.LogicalDocument.Id}");
        context.ChangeTracker.Clear();
    }

    private static ExactResolutionProfile SyntheticProfile(
        ExactResolutionProfile reviewed,
        ResolutionOccurrence occurrence)
    {
        byte[] json = Encoding.UTF8.GetBytes("{\"synthetic\":true}");
        string fingerprint = Convert.ToHexString(SHA256.HashData(json)).ToLowerInvariant();
        return new(
            reviewed.SchemaVersion,
            "synthetic-reverse-profile",
            reviewed.ProfileVersion,
            fingerprint,
            reviewed.Repositories,
            [occurrence],
            reviewed.UnsupportedObservations,
            json);
    }

    private static ExactResolutionProfile ProviderNegativeProfile(
        ExactResolutionProfile reviewed,
        string reason)
    {
        ResolutionOccurrence occurrence = Assert.Single(reviewed.Occurrences);
        IReadOnlyList<string> files = reason == "malformed_provider"
            ? occurrence.Provider.Files
            :
            [
                .. occurrence.Provider.Files,
                "src/main/resources/disgram/in/cust/updateSessionDuplicate.xml",
            ];
        var provider = new ExactProviderSelector(
            occurrence.Provider.Root,
            files,
            occurrence.Provider.Declaration,
            occurrence.Provider.ImplementationDocument);
        var derivedOccurrence = new ResolutionOccurrence(
            occurrence.OccurrenceId,
            occurrence.Protocol,
            occurrence.ConsumerAlias,
            occurrence.ProviderAlias,
            occurrence.Consumer,
            provider,
            occurrence.Normalization,
            occurrence.Comparison);
        byte[] json = Encoding.UTF8.GetBytes($"{{\"provider-negative\":\"{reason}\"}}");
        string fingerprint = Convert.ToHexString(SHA256.HashData(json)).ToLowerInvariant();
        return new(
            reviewed.SchemaVersion,
            reviewed.ProfileId,
            reviewed.ProfileVersion,
            fingerprint,
            reviewed.Repositories,
            [derivedOccurrence],
            reviewed.UnsupportedObservations,
            json);
    }

    private static void MutateProviderNegativeSource(
        ExactCrossRepositoryTestFixture fixture,
        ResolutionOccurrence occurrence,
        string reason)
    {
        string xml = reason switch
        {
            "malformed_provider" => "<?xml version=\"1.0\"?>\n<trans/>",
            "unmatched" => "<?xml version=\"1.0\"?>\n<trans name=\"in/otherEndpoint\"/>",
            "normalization_mismatch" =>
                "<?xml version=\"1.0\"?>\n<trans name=\"cust/updateSession\"/>",
            "multiple_providers" =>
                "<?xml version=\"1.0\"?>\n<trans name=\"in/cust/updateSession\"/>",
            _ => throw new ArgumentOutOfRangeException(nameof(reason)),
        };
        foreach (string file in occurrence.Provider.Files)
        {
            fixture.ProviderRepository.Write(file, xml);
        }
    }

    private static ExactCrossRepositoryResolutionCoordinator CoordinatorWithProfile(
        ExactResolutionProfile profile,
        ExactCrossRepositoryTestFixture fixture,
        ExactPass1DocumentFixture consumer,
        ExactPass1DocumentFixture provider,
        VulgataDbContext context)
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(ExactCrossRepositoryResolutionCoordinator)
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            candidate => candidate.GetParameters().Length == 12 &&
                candidate.GetParameters()[^1].ParameterType == typeof(Func<ExactResolutionProfile>));
        return Assert.IsType<ExactCrossRepositoryResolutionCoordinator>(constructor.Invoke(
        [
            new ExactResolutionProfileCatalog(),
            new StubUserContext(),
            new SystemRepository(context),
            new RepositoryRepository(context),
            new StubScanExecutionStore(consumer.ScanExecution, provider.ScanExecution),
            new FixedAuthorizationVerifier(true),
            new FixtureSnapshotService(fixture),
            new ExactCandidateExtractors(context),
            new ExactCrossRepositoryResolver(),
            new ExactCrossRepositoryResolutionStore(context),
            context,
            (Func<ExactResolutionProfile>)(() => profile),
        ]));
    }

    private static string ProfileCommit(ExactResolutionProfile profile, string alias) =>
        Assert.Single(profile.Repositories, repository =>
            StringComparer.Ordinal.Equals(repository.Alias, alias)).ExpectedCommit;

    private static void MutateEligibleSourceFailure(
        ExactCrossRepositoryTestFixture fixture,
        ResolutionOccurrence occurrence,
        string reason)
    {
        switch (reason)
        {
            case "variable_target":
                fixture.ConsumerRepository.Write(
                    Assert.Single(occurrence.Consumer.Files),
                    new string('\n', 44) + "netTools.execute(ctx, transCode);\n");
                break;
            case "disallowed_invocation":
                fixture.ConsumerRepository.Write(
                    Assert.Single(occurrence.Consumer.Files),
                    new string('\n', 44) +
                    "this.netTools.execute(ctx, \"cust/updateSession\");\n");
                break;
            case "malformed_provider":
                fixture.ProviderRepository.Write(
                    Assert.Single(occurrence.Provider.Files),
                    "<?xml version=\"1.0\"?>\n<trans name=\"in/cust/updateSession\"");
                break;
            case "provider_out_of_root":
                string providerFile = Assert.Single(occurrence.Provider.Files);
                string configuredPath = ExactCrossRepositoryCleanup.ResolveRelativePath(
                    fixture.ProviderRepository.RootPath,
                    providerFile);
                string configuredDirectory = Path.GetDirectoryName(configuredPath)!;
                string outsideDirectory = ExactCrossRepositoryCleanup.ResolveRelativePath(
                    fixture.ProviderRepository.RootPath,
                    "outside-provider-root");
                fixture.ProviderRepository.Write(
                    "outside-provider-root/updateSession.xml",
                    "<trans name=\"in/cust/updateSession\"/>\n");
                File.Delete(configuredPath);
                Directory.Delete(configuredDirectory);
                Directory.CreateSymbolicLink(configuredDirectory, outsideDirectory);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(reason));
        }
    }

    private sealed class StubUserContext : IUserContext
    {
        public string UserId => "system-owner";
        public bool IsAdministrator => true;
        public SystemOwnerFilter ToSystemOwnerFilter() => SystemOwnerFilter.AllSystems();
    }

    private sealed class FixedAuthorizationVerifier(bool allowed) :
        IRepositoryAuthorizationVerifier
    {
        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.InRange(requiredRepositoryIds.Count, 1, 2);
            return ValueTask.FromResult(allowed);
        }
    }

    private sealed class StubScanExecutionStore(params ScanExecution[] executions) :
        IScanExecutionStore
    {
        private readonly IReadOnlyDictionary<Guid, ScanExecution> _executions =
            executions.ToDictionary(execution => execution.Id);

        internal int GetCallCount { get; private set; }

        public Task<ScanExecution?> GetAsync(
            Guid scanExecutionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetCallCount++;
            _executions.TryGetValue(scanExecutionId, out ScanExecution? execution);
            return Task.FromResult(execution);
        }

        public Task<ScanExecution?> FindActiveAsync(
            Guid repositoryId,
            Guid scanRunId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task PersistAsync(
            ScanExecution execution,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ScanExecutionStateView> ReadStateAsync(
            Guid scanExecutionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ProbeSnapshotService : IExactGitRevisionSnapshotService
    {
        internal int CallCount { get; private set; }

        public Task<ICommittedRepositorySnapshotLease> AcquireAsync(
            RepositoryEntity repository,
            Guid scanRunId,
            string expectedRef,
            string expectedCommit,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("The readiness gate allowed Git access.");
        }
    }

    private sealed class FixtureSnapshotService(ExactCrossRepositoryTestFixture fixture) :
        IExactGitRevisionSnapshotService
    {
        internal int CallCount { get; private set; }

        public Task<ICommittedRepositorySnapshotLease> AcquireAsync(
            RepositoryEntity repository,
            Guid scanRunId,
            string expectedRef,
            string expectedCommit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            string root = repository.Id == ExactCrossRepositoryTestValues.ConsumerRepositoryId
                ? fixture.ConsumerRepository.RootPath
                : fixture.ProviderRepository.RootPath;
            return Task.FromResult<ICommittedRepositorySnapshotLease>(
                new FixtureSnapshotLease(repository.Id, scanRunId, expectedCommit, root));
        }
    }

    private sealed class CleanupProbeSnapshotService(ExactCrossRepositoryTestFixture fixture) :
        IExactGitRevisionSnapshotService
    {
        internal int DisposeAttempts { get; private set; }

        public Task<ICommittedRepositorySnapshotLease> AcquireAsync(
            RepositoryEntity repository,
            Guid scanRunId,
            string expectedRef,
            string expectedCommit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string root = repository.Id == ExactCrossRepositoryTestValues.ConsumerRepositoryId
                ? fixture.ConsumerRepository.RootPath
                : fixture.ProviderRepository.RootPath;
            bool cleanupFails =
                repository.Id == ExactCrossRepositoryTestValues.ProviderRepositoryId;
            return Task.FromResult<ICommittedRepositorySnapshotLease>(
                new CleanupProbeSnapshotLease(
                    repository.Id,
                    scanRunId,
                    expectedCommit,
                    root,
                    cleanupFails,
                    () => DisposeAttempts++));
        }
    }

    private sealed class FixtureSnapshotLease(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string rootPath) : ICommittedRepositorySnapshotLease
    {
        public Guid RepositoryId => repositoryId;
        public Guid ScanRunId => scanRunId;
        public string ProfiledRevision => profiledRevision;
        public string RootPath => rootPath;
        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CleanupProbeSnapshotLease(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string rootPath,
        bool cleanupFails,
        Action recordDispose) : ICommittedRepositorySnapshotLease
    {
        public Guid RepositoryId => repositoryId;
        public Guid ScanRunId => scanRunId;
        public string ProfiledRevision => profiledRevision;
        public string RootPath => rootPath;

        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            recordDispose();
            return cleanupFails
                ? ValueTask.FromException(
                    new InvalidOperationException("fixture cleanup failure"))
                : ValueTask.CompletedTask;
        }
    }

    private sealed class ProbeResolver : IExactCrossRepositoryResolver
    {
        internal int CallCount { get; private set; }

        public Task<ExactResolutionDecisionBatch> ResolveAsync(
            ExactResolutionProfile profile,
            IReadOnlyList<ConsumerDispatchCandidate> consumerCandidates,
            IReadOnlyList<ProviderEndpointCandidate> providerCandidates,
            IReadOnlyList<ExactResolutionDecision> unsupportedDecisions,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("The readiness gate allowed resolution.");
        }
    }

    private sealed class ProbeStore : IExactCrossRepositoryResolutionStore
    {
        internal int CallCount { get; private set; }

        public Task<ExactResolutionBatchOutcome> ApplyAsync(
            ExactResolutionDecisionBatch decisions,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("The readiness gate allowed a write.");
        }
    }
}
