using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CommunicationDetection;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests.PreparedPass2;

public sealed class ExactCrossRepositoryResolutionTests
{
    [Fact]
    public async Task ResolveAsync_DuplicateProviderObservationsForOneCurrentDocument_AggregateToOneExactProviderAsync()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactResolutionOwner consumer = Owner(
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ConsumerScanRunId,
            ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
            ExactCrossRepositoryTestValues.ConsumerSourceAnchorId,
            Commit(profile, occurrence.ConsumerAlias));
        ExactResolutionOwner provider = Owner(
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
            ExactCrossRepositoryTestValues.ProviderSourceAnchorId,
            Commit(profile, occurrence.ProviderAlias));

        ExactResolutionDecisionBatch batch = await new ExactCrossRepositoryResolver().ResolveAsync(
            profile,
            [Consumer(occurrence, consumer)],
            [
                Provider(occurrence, provider, "provider-a.xml#line=2;ordinal=0"),
                Provider(occurrence, provider, "provider-b.xml#line=4;ordinal=1"),
            ],
            [Unsupported(Assert.Single(profile.UnsupportedObservations).ObservationId, consumer)]);

        ExactResolutionDecision decision = Assert.Single(
            batch.Decisions,
            value => value.Identity == occurrence.OccurrenceId);
        Assert.Equal(ExactResolutionDecisionKind.ExactMatch, decision.Kind);
        Assert.Equal(provider, decision.Target);
        Assert.NotNull(decision.ExactEvidence);
    }

    [Fact]
    public async Task ResolveAsync_TenRandomizedObservationOrders_ProduceOneCanonicalBatchAsync()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactResolutionOwner consumer = Owner(
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ConsumerScanRunId,
            ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
            ExactCrossRepositoryTestValues.ConsumerSourceAnchorId,
            Commit(profile, occurrence.ConsumerAlias));
        ExactResolutionOwner provider = Owner(
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
            ExactCrossRepositoryTestValues.ProviderSourceAnchorId,
            Commit(profile, occurrence.ProviderAlias));
        var random = new Random(12_400);
        string? expected = null;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            ProviderEndpointCandidate[] observations =
            [
                Provider(occurrence, provider, "provider-a.xml#line=2;ordinal=0"),
                Provider(occurrence, provider, "provider-b.xml#line=4;ordinal=1"),
            ];
            Array.Sort(observations, (_, _) => random.Next(-1, 2));
            ExactResolutionDecisionBatch batch = await new ExactCrossRepositoryResolver().ResolveAsync(
                profile,
                [Consumer(occurrence, consumer)],
                observations,
                [Unsupported(Assert.Single(profile.UnsupportedObservations).ObservationId, consumer)]);
            string snapshot = string.Join(
                '|',
                batch.Decisions.Select(value =>
                    $"{value.Identity}:{value.Kind}:{value.Target?.DocumentVersionId}:{value.EvidenceLocator}"));
            expected ??= snapshot;
            Assert.Equal(expected, snapshot);
        }
    }

    [Fact]
    public async Task ResolveAsync_MultipleCurrentProviders_ReturnsClosedUncertaintyAsync()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactResolutionOwner consumer = Owner(
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ConsumerScanRunId,
            ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
            ExactCrossRepositoryTestValues.ConsumerSourceAnchorId,
            Commit(profile, occurrence.ConsumerAlias));
        ExactResolutionOwner firstProvider = Owner(
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
            ExactCrossRepositoryTestValues.ProviderSourceAnchorId,
            Commit(profile, occurrence.ProviderAlias));
        ExactResolutionOwner secondProvider = firstProvider with
        {
            DocumentVersionId = ExactCrossRepositoryTestValues.Id(6_102),
            SourceAnchorId = ExactCrossRepositoryTestValues.Id(7_102),
        };

        ExactResolutionDecisionBatch batch = await new ExactCrossRepositoryResolver().ResolveAsync(
            profile,
            [Consumer(occurrence, consumer)],
            [
                Provider(occurrence, firstProvider, "provider-a.xml#line=2;ordinal=0"),
                Provider(occurrence, secondProvider, "provider-b.xml#line=2;ordinal=0"),
            ],
            [Unsupported(Assert.Single(profile.UnsupportedObservations).ObservationId, consumer)]);

        ExactResolutionDecision decision = Assert.Single(
            batch.Decisions,
            value => value.Identity == occurrence.OccurrenceId);
        Assert.Equal(ExactResolutionDecisionKind.Uncertainty, decision.Kind);
        Assert.Equal("multiple_providers", decision.ReasonCode);
        Assert.Null(decision.Target);
    }

    [Fact]
    public async Task ResolveAsync_UnsafeHttpWithQueryAndFragment_ProducesClosedRejectionAsync()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactResolutionOwner consumer = Owner(
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ConsumerScanRunId,
            ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
            ExactCrossRepositoryTestValues.ConsumerSourceAnchorId,
            Commit(profile, occurrence.ConsumerAlias));
        ExactResolutionOwner provider = Owner(
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
            ExactCrossRepositoryTestValues.ProviderSourceAnchorId,
            Commit(profile, occurrence.ProviderAlias));

        ExactResolutionNonExactException exception = await Assert.ThrowsAsync<ExactResolutionNonExactException>(
            () => new ExactCrossRepositoryResolver().ResolveAsync(
                profile,
                [Consumer(occurrence, consumer, "https://example.test/v1/action?query=1#fragment")],
                [Provider(
                    occurrence,
                    provider,
                    "provider.xml#line=2;ordinal=0",
                    "in/https://example.test/v1/action?query=1#fragment")],
                [Unsupported(Assert.Single(profile.UnsupportedObservations).ObservationId, consumer)]));

        Assert.Equal("normalization_mismatch", exception.ReasonCode);
    }

    [Fact]
    public async Task CurrentCommunicationCandidateSource_ReconstructsCurrentPersistedCandidatesAsync()
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
        Type sourceType = typeof(CurrentCommunicationCandidateSource);
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentPairAsync(context, fixture, consumer, provider);

        // The source must be able to query the persisted pair and use the
        // revision-bound source services. A parameterless object cannot observe
        // this fixture, so require an injectable constructor before invoking it.
        ConstructorInfo? constructor = sourceType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .SingleOrDefault(value => value.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(VulgataDbContext)));
        Assert.NotNull(
            constructor);
        if (constructor is null)
        {
            return;
        }

        Assert.All(
            constructor.GetParameters(),
            parameter => Assert.True(
                parameter.ParameterType == typeof(VulgataDbContext),
                $"The test harness has no approved fixture dependency for {parameter.ParameterType.FullName}."));
        object source = constructor.Invoke([context]);

        MethodInfo? reconstruct = sourceType.GetMethod(
            "ReconstructAsync",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(reconstruct);
        if (reconstruct is null)
        {
            return;
        }

        Assert.Equal(
            [
                typeof(ExactResolutionRuntimeRequest),
                typeof(RepositoryAccessScope),
                typeof(CancellationToken),
            ],
            reconstruct.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.True(
            reconstruct.ReturnType.IsGenericType &&
                reconstruct.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
            "T013 must return a typed candidate set so callers can inspect reconstructed Consumer and Provider candidates.");

        object? invocation = reconstruct.Invoke(
            source,
            [Request(profile), fixture.Access.AuthorizedScope, CancellationToken.None]);
        Task operation = Assert.IsAssignableFrom<Task>(invocation);
        await operation;
        object? candidateSet = operation.GetType().GetProperty("Result")?.GetValue(operation);
        Assert.NotNull(candidateSet);
        if (candidateSet is null)
        {
            return;
        }

        object? consumers = candidateSet.GetType().GetProperty("Consumers")?.GetValue(candidateSet);
        object? providers = candidateSet.GetType().GetProperty("Providers")?.GetValue(candidateSet);
        Assert.NotNull(consumers);
        Assert.NotNull(providers);
        Assert.Contains(
            Assert.IsAssignableFrom<IEnumerable<ConsumerDispatchCandidate>>(consumers),
            candidate => candidate.Owner.DocumentVersionId == consumer.DocumentVersion.Id);
        Assert.Contains(
            Assert.IsAssignableFrom<IEnumerable<ProviderEndpointCandidate>>(providers),
            candidate => candidate.Owner.DocumentVersionId == provider.DocumentVersion.Id);
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

    private static string ProfileCommit(ExactResolutionProfile profile, string alias) =>
        Assert.Single(profile.Repositories, repository => repository.Alias == alias).ExpectedCommit;

    private static async Task SeedCurrentPairAsync(
        VulgataDbContext context,
        ExactCrossRepositoryTestFixture fixture,
        ExactPass1DocumentFixture consumer,
        ExactPass1DocumentFixture provider)
    {
        SourceAnchor consumerAnchor = ValidAnchor(consumer.SourceAnchor);
        SourceAnchor providerAnchor = ValidAnchor(provider.SourceAnchor);
        consumer.DocumentVersion.SourceAnchors.Clear();
        consumer.DocumentVersion.SourceAnchors.Add(consumerAnchor);
        provider.DocumentVersion.SourceAnchors.Clear();
        provider.DocumentVersion.SourceAnchors.Add(providerAnchor);
        ExactCrossRepositoryManagedFixture managed = fixture.CreateManagedRepositories();
        context.Systems.Add(managed.System);
        context.ScanRuns.AddRange(consumer.ScanRun, provider.ScanRun);
        await context.SaveChangesAsync();

        // Reuse the closed Story 12.3 catalog and retain one Confirmed
        // classification for each side of the pair. The source must still
        // revalidate these rows against the current document evidence.
        _ = CommunicationEvidenceRuleCatalog.LoadEmbedded();
        CommunicationKindClassification consumerClassification =
            CommunicationKindClassification.CreatePrepared(
                consumer.ScanRun.Id,
                CommunicationKind.Rpc,
                ClassificationLevel.Confirmed,
                ConfidenceBand.High,
                EvidenceRule.RpcDirectAnnotation,
                EvidenceProvenance.StructuralAnnotation,
                consumer.DocumentVersion.RepositoryRelativePath,
                consumerAnchor.EvidenceLocator,
                1,
                new string('a', 64),
                "qualification.ordering");
        CommunicationKindClassification providerClassification =
            CommunicationKindClassification.CreatePrepared(
                provider.ScanRun.Id,
                CommunicationKind.Rpc,
                ClassificationLevel.Confirmed,
                ConfidenceBand.High,
                EvidenceRule.RpcDirectAnnotation,
                EvidenceProvenance.StructuralAnnotation,
                provider.DocumentVersion.RepositoryRelativePath,
                providerAnchor.EvidenceLocator,
                1,
                new string('b', 64),
                "qualification.ordering");
        context.PreScanResults.AddRange(
            PreScanResult.CreateApproved(
                consumer.DocumentVersion.RepositoryId,
                consumer.ScanRun.Id,
                consumer.DocumentVersion.ProfiledRevision,
                ExactCrossRepositoryTestValues.Pass1CompletedAt,
                new string('c', 64),
                "fixture pair approved",
                ConfirmedCommunicationKinds.Rpc,
                1,
                0,
                0,
                false,
                [consumerClassification]),
            PreScanResult.CreateApproved(
                provider.DocumentVersion.RepositoryId,
                provider.ScanRun.Id,
                provider.DocumentVersion.ProfiledRevision,
                ExactCrossRepositoryTestValues.Pass1CompletedAt,
                new string('d', 64),
                "fixture pair approved",
                ConfirmedCommunicationKinds.Rpc,
                1,
                0,
                0,
                false,
                [providerClassification]));
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
                ExactCrossRepositoryTestValues.CreatedAt));
        await context.SaveChangesAsync();
        context.DocumentVersions.AddRange(consumer.DocumentVersion, provider.DocumentVersion);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {consumer.DocumentVersion.Id} WHERE \"Id\" = {consumer.LogicalDocument.Id}");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {provider.DocumentVersion.Id} WHERE \"Id\" = {provider.LogicalDocument.Id}");
        context.ChangeTracker.Clear();
    }

    private static SourceAnchor ValidAnchor(SourceAnchor anchor) =>
        new(
            anchor.Id,
            anchor.DocumentVersionId,
            anchor.RepositoryId,
            anchor.ScanRunId,
            anchor.CodeUnitId,
            anchor.ProfiledRevision,
            anchor.RepositoryRelativePath,
            $"{anchor.RepositoryRelativePath}#fixture",
            anchor.LineStart,
            anchor.LineEnd,
            anchor.SourceHash,
            anchor.CreatedAt);

    private static ConsumerDispatchCandidate Consumer(
        ResolutionOccurrence occurrence,
        ExactResolutionOwner owner,
        string rawIdentifier = "cust/updateSession") =>
        new(
            occurrence.OccurrenceId,
            owner,
            occurrence.Consumer.Invocation.Form,
            occurrence.Consumer.Invocation.ReceiverSymbol,
            occurrence.Consumer.Invocation.MemberName,
            occurrence.Consumer.Invocation.EndpointArgumentIndex,
            occurrence.Consumer.Invocation.Line,
            occurrence.Consumer.Invocation.OccurrenceOrdinalOnLine,
            rawIdentifier,
            rawIdentifier,
            "consumer.java#line=45;rule=exact-java-direct-literal-v1");

    private static ProviderEndpointCandidate Provider(
        ResolutionOccurrence occurrence,
        ExactResolutionOwner owner,
        string locator,
        string rawIdentifier = "in/cust/updateSession") =>
        new(
            occurrence.OccurrenceId,
            owner,
            rawIdentifier,
            "cust/updateSession",
            locator);

    private static ExactResolutionDecision Unsupported(
        string identity,
        ExactResolutionOwner owner) =>
        new(
            identity,
            ExactResolutionDecisionKind.UnsupportedObservation,
            owner,
            null,
            "unsupported_kind",
            "unsupported.java#line=66;rule=unsupported-member-invocation-v1");

    private static ExactResolutionOwner Owner(
        Guid repositoryId,
        Guid scanRunId,
        Guid documentVersionId,
        Guid sourceAnchorId,
        string profiledRevision) =>
        new(repositoryId, scanRunId, documentVersionId, sourceAnchorId, profiledRevision);

    private static string Commit(ExactResolutionProfile profile, string alias) =>
        Assert.Single(profile.Repositories, value => value.Alias == alias).ExpectedCommit;
}
