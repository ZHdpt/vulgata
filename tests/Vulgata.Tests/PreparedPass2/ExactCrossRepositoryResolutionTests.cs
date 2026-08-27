using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public async Task ResolveAsync_ExactDuplicateProviderObservation_IsDeduplicatedAsync()
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
        ProviderEndpointCandidate observation = Provider(
            occurrence,
            provider,
            "provider.xml#line=2;ordinal=0");

        ExactResolutionDecisionBatch batch = await new ExactCrossRepositoryResolver().ResolveAsync(
            profile,
            [Consumer(occurrence, consumer)],
            [observation, observation],
            [Unsupported(Assert.Single(profile.UnsupportedObservations).ObservationId, consumer)]);

        ExactResolutionDecision decision = Assert.Single(
            batch.Decisions,
            value => value.Identity == occurrence.OccurrenceId);
        Assert.Equal(ExactResolutionDecisionKind.ExactMatch, decision.Kind);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<ProviderEndpointCandidate>>(
            decision.ProviderObservations));
    }

    [Fact]
    public async Task ResolveAsync_SameLocatorDifferentClassification_IsRetainedInCanonicalOrderAsync()
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
        Guid firstId = ExactCrossRepositoryTestValues.Id(20_001);
        Guid secondId = ExactCrossRepositoryTestValues.Id(20_002);
        ProviderEndpointCandidate first = new(
            occurrence.OccurrenceId, provider, "in/cust/updateSession", "cust/updateSession",
            "provider.xml#line=2;ordinal=0", firstId, new string('a', 64),
            "catalog-a", "normalizer-a");
        ProviderEndpointCandidate second = new(
            occurrence.OccurrenceId, provider, "in/cust/updateSession", "cust/updateSession",
            "provider.xml#line=2;ordinal=0", secondId, new string('b', 64),
            "catalog-b", "normalizer-b");

        ExactResolutionDecisionBatch batch = await new ExactCrossRepositoryResolver().ResolveAsync(
            profile,
            [Consumer(occurrence, consumer)],
            [second, first],
            [Unsupported(Assert.Single(profile.UnsupportedObservations).ObservationId, consumer)]);

        ExactResolutionDecision decision = Assert.Single(
            batch.Decisions,
            value => value.Identity == occurrence.OccurrenceId);
        Assert.Equal([firstId, secondId], decision.ProviderObservations!
            .Select(value => value.CommunicationClassificationId));
    }

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
                parameter.ParameterType == typeof(VulgataDbContext) ||
                    parameter.ParameterType == typeof(IRevisionBoundSourceReader),
                $"The test harness has no approved fixture dependency for {parameter.ParameterType.FullName}."));
        object source = constructor.Invoke(
            constructor.GetParameters().Select(parameter =>
                parameter.ParameterType == typeof(VulgataDbContext)
                    ? (object)context
                    : new FixtureRevisionBoundSourceReader(fixture)).ToArray());

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

    [Fact]
    public async Task CurrentCommunicationCandidateSource_RequiresTerminalExecutionAndPreparedPairAsync()
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
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentPairAsync(context, fixture, consumer, provider);

        CurrentCommunicationCandidateSource source = new(context);
        ExactResolutionRuntimeRequest request = Request(
            profile,
            consumerScanExecutionId: Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidDataException>(
            () => source.ReconstructAsync(request, fixture.Access.AuthorizedScope));
    }

    [Fact]
    public async Task CurrentCommunicationCandidateSource_RequiresCommunicationEvidenceAndCrossRepositoryObservationAsync()
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
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentPairAsync(context, fixture, consumer, provider);

        JsonObject consumerDocument = JsonNode.Parse(consumer.DocumentVersion.ContentJson)!.AsObject();
        consumerDocument["communicationEvidence"] = new JsonArray();
        consumerDocument["deferredObservations"] = new JsonArray();
        Set(consumer.DocumentVersion, nameof(DocumentVersion.ContentJson), consumerDocument.ToJsonString());
        await context.SaveChangesAsync();

        // Remove exactly the persisted evidence pair while retaining the
        // terminal execution, manifests, current anchors, and classifications.
        CurrentCommunicationCandidateSource source = new(context);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => source.ReconstructAsync(Request(profile), fixture.Access.AuthorizedScope));
    }

    [Fact]
    public async Task CurrentCommunicationCandidateSource_RereadsRevisionBoundSourceAndChecksAnchorHashAsync()
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
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentPairAsync(context, fixture, consumer, provider);

        ConstructorInfo? constructor = typeof(CurrentCommunicationCandidateSource)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .SingleOrDefault(value => value.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(VulgataDbContext)));
        Assert.NotNull(constructor);
        if (constructor is null)
        {
            return;
        }

        ParameterInfo[] parameters = constructor.GetParameters();
        Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(IRevisionBoundSourceReader));
        object source = constructor.Invoke(parameters.Select(parameter =>
            parameter.ParameterType == typeof(VulgataDbContext)
                ? (object)context
                : new MismatchingRevisionBoundSourceReader()).ToArray());
        MethodInfo reconstruct = typeof(CurrentCommunicationCandidateSource).GetMethod(
            "ReconstructAsync",
            BindingFlags.Instance | BindingFlags.Public)!;

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await InvokeReconstructAsync(
                source,
                reconstruct,
                Request(profile),
                fixture.Access.AuthorizedScope));
    }

    [Fact]
    public async Task CurrentCommunicationCandidateSource_ReevaluatesEmbeddedCatalogIdentityAndRuleAsync()
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
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentPairAsync(context, fixture, consumer, provider);
        CommunicationKindClassification classification =
            await context.CommunicationKindClassifications.SingleAsync(value =>
                value.PreScanResultId == consumer.ScanRun.Id);
        typeof(CommunicationKindClassification)
            .GetProperty(nameof(CommunicationKindClassification.CatalogRuleIdentity))!
            .SetValue(classification, "not-an-embedded-rule");
        await context.SaveChangesAsync();

        CurrentCommunicationCandidateSource source = new(context);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => source.ReconstructAsync(Request(profile), fixture.Access.AuthorizedScope));
    }

    [Fact]
    public async Task CurrentCommunicationCandidateSource_UsesChangedDeferredTargetForConsumerIdentityAsync()
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
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentPairAsync(context, fixture, consumer, provider);

        JsonObject consumerDocument = JsonNode.Parse(consumer.DocumentVersion.ContentJson)!.AsObject();
        consumerDocument["deferredObservations"]![0]![
            "targetSymbol"] = "cust/newSession";
        DocumentVersion storedConsumer = await context.DocumentVersions.SingleAsync(
            value => value.Id == consumer.DocumentVersion.Id);
        Set(storedConsumer, nameof(DocumentVersion.ContentJson), consumerDocument.ToJsonString());
        await context.SaveChangesAsync();

        CurrentCommunicationCandidateSource source = new(
            context,
            new FixtureRevisionBoundSourceReader(fixture));
        CurrentCommunicationCandidateSet candidates = await source.ReconstructAsync(
            Request(profile),
            fixture.Access.AuthorizedScope);
        ConsumerDispatchCandidate candidate = Assert.Single(candidates.Consumers);

        Assert.Equal("cust/newSession", candidate.RawIdentifier);
        Assert.Equal("cust/newSession", candidate.NormalizedIdentifier);
    }

    [Fact]
    public async Task CurrentCommunicationCandidateSource_UsesReproducedProviderSourceIdentityAsync()
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
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentPairAsync(context, fixture, consumer, provider);

        const string changedXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<trans name=\"in/cust/newSession\"/>\n";
        string changedHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(changedXml)));
        SourceAnchor storedAnchor = await context.SourceAnchors.SingleAsync(
            value => value.DocumentVersionId == provider.DocumentVersion.Id);
        Set(storedAnchor, nameof(SourceAnchor.SourceHash), changedHash);
        Set(storedAnchor, nameof(SourceAnchor.LineStart), 1);
        Set(storedAnchor, nameof(SourceAnchor.LineEnd), 3);
        CodeUnit storedCodeUnit = await context.CodeUnits.SingleAsync(
            value => value.Id == provider.CodeUnit.Id);
        Set(storedCodeUnit, nameof(CodeUnit.SourceHash), changedHash);
        Set(storedCodeUnit, nameof(CodeUnit.LineStart), 1);
        Set(storedCodeUnit, nameof(CodeUnit.LineEnd), 3);
        DocumentVersion storedProvider = await context.DocumentVersions.SingleAsync(
            value => value.Id == provider.DocumentVersion.Id);
        JsonObject providerDocument = JsonNode.Parse(storedProvider.ContentJson)!.AsObject();
        providerDocument["binding"]!["sourceHash"] = changedHash;
        providerDocument["sourceAnchors"]![0]!["sourceHash"] = changedHash;
        Set(storedProvider, nameof(DocumentVersion.ContentJson), providerDocument.ToJsonString());
        await context.SaveChangesAsync();

        CurrentCommunicationCandidateSource source = new(
            context,
            new ChangedProviderSourceReader(fixture, changedXml, changedHash));
        CurrentCommunicationCandidateSet candidates = await source.ReconstructAsync(
            Request(profile),
            fixture.Access.AuthorizedScope);
        ProviderEndpointCandidate candidate = Assert.Single(candidates.Providers);

        Assert.Equal("in/cust/newSession", candidate.RawIdentifier);
        Assert.Equal("cust/newSession", candidate.NormalizedIdentifier);
    }

    [Fact]
    public async Task CurrentCommunicationCandidateSource_RejectsInvalidManifestProofBindingAsync()
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
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentPairAsync(context, fixture, consumer, provider);

        PreparedScanManifest manifest = await context.PreparedScanManifests.SingleAsync(
            value => value.RepositoryId == consumer.DocumentVersion.RepositoryId);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphRuntimeBindingMatched), false);
        await context.SaveChangesAsync();

        CurrentCommunicationCandidateSource source = new(
            context,
            new FixtureRevisionBoundSourceReader(fixture));
        await Assert.ThrowsAsync<InvalidDataException>(
            () => source.ReconstructAsync(Request(profile), fixture.Access.AuthorizedScope));
    }

    private static ExactResolutionRuntimeRequest Request(
        ExactResolutionProfile profile,
        Guid? consumerScanExecutionId = null,
        Guid? providerScanExecutionId = null) =>
        ExactResolutionRuntimeRequest.Create(
            ExactCrossRepositoryTestValues.SystemId,
            profile,
            [
                new(
                    "mbs_account",
                    "mbs_account",
                    ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                    ExactCrossRepositoryTestValues.ConsumerScanRunId,
                    consumerScanExecutionId ?? ExactCrossRepositoryTestValues.ConsumerScanExecutionId),
                new(
                    "mbs_channel",
                    "mbs_channel",
                    ExactCrossRepositoryTestValues.ProviderRepositoryId,
                    ExactCrossRepositoryTestValues.ProviderScanRunId,
                    providerScanExecutionId ?? ExactCrossRepositoryTestValues.ProviderScanExecutionId),
            ]);

    private static string ProfileCommit(ExactResolutionProfile profile, string alias) =>
        Assert.Single(profile.Repositories, repository => repository.Alias == alias).ExpectedCommit;

    internal static Task SeedAcceptedV2PairAsync(
        VulgataDbContext context,
        ExactCrossRepositoryTestFixture fixture,
        ExactPass1DocumentFixture consumer,
        ExactPass1DocumentFixture provider) =>
        SeedCurrentPairAsync(context, fixture, consumer, provider);

    private static async Task SeedCurrentPairAsync(
        VulgataDbContext context,
        ExactCrossRepositoryTestFixture fixture,
        ExactPass1DocumentFixture consumer,
        ExactPass1DocumentFixture provider)
    {
        string consumerSourceHash = ComputeSourceHash(
            fixture.ConsumerRepository.RootPath,
            consumer.DocumentVersion.RepositoryRelativePath);
        string providerSourceHash = ComputeSourceHash(
            fixture.ProviderRepository.RootPath,
            provider.DocumentVersion.RepositoryRelativePath);
        SourceAnchor consumerAnchor = ValidAnchor(consumer.SourceAnchor, consumerSourceHash);
        SourceAnchor providerAnchor = ValidAnchor(provider.SourceAnchor, providerSourceHash);
        Set(consumer.CodeUnit, nameof(CodeUnit.FileIdentityHash), consumerSourceHash);
        Set(consumer.CodeUnit, nameof(CodeUnit.SourceHash), consumerSourceHash);
        Set(provider.CodeUnit, nameof(CodeUnit.FileIdentityHash), providerSourceHash);
        Set(provider.CodeUnit, nameof(CodeUnit.SourceHash), providerSourceHash);
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
                "rpc.dubbo.service-annotation");
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
                "rpc.dubbo.service-annotation");
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

        context.ScanExecutions.AddRange(consumer.ScanExecution, provider.ScanExecution);
        await context.SaveChangesAsync();
        TwoRepositoryScanCoordination coordination = TwoRepositoryScanCoordination.Create(
            consumer.ScanExecution,
            provider.ScanExecution,
            ExactCrossRepositoryTestValues.Pass1CompletedAt);
        coordination.BeginPass1(0, ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(1));
        coordination.BeginResolving(1, ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(2));
        context.TwoRepositoryScanCoordinations.Add(coordination);
        ScanContextSnapshot consumerSnapshot = ScanContextSnapshot.CapturePrepared(
            consumer.ScanRun,
            "global fixture context",
            "system fixture context",
            "consumer fixture context",
            ExactCrossRepositoryTestValues.Pass1CompletedAt);
        ScanContextSnapshot providerSnapshot = ScanContextSnapshot.CapturePrepared(
            provider.ScanRun,
            "global fixture context",
            "system fixture context",
            "provider fixture context",
            ExactCrossRepositoryTestValues.Pass1CompletedAt);
        context.ScanContextSnapshots.AddRange(consumerSnapshot, providerSnapshot);
        await context.SaveChangesAsync();

        Vulgata.Infrastructure.CommunicationDetection.CommunicationPatternCatalog catalog =
            CommunicationCatalogLoader.LoadEmbedded();
        const string pairSelectionFingerprint =
            "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        const string pairPackageFingerprint =
            "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
        PreparedScanManifest consumerManifest = CreatePreparedManifest(
            consumer,
            coordination.Id,
            consumerSnapshot,
            catalog,
            "1111111111111111111111111111111111111111111111111111111111111111",
            pairSelectionFingerprint);
        PreparedScanManifest providerManifest = CreatePreparedManifest(
            provider,
            coordination.Id,
            providerSnapshot,
            catalog,
            "2222222222222222222222222222222222222222222222222222222222222222",
            pairSelectionFingerprint);
        context.PreparedScanManifests.AddRange(consumerManifest, providerManifest);
        Set(consumerManifest, nameof(PreparedScanManifest.ApprovalState), PreparedScanManifestApprovalState.Approved);
        Set(providerManifest, nameof(PreparedScanManifest.ApprovalState), PreparedScanManifestApprovalState.Approved);
        SetPreparedBinding(context, consumer, consumerManifest, consumerSnapshot, pairPackageFingerprint);
        SetPreparedBinding(context, provider, providerManifest, providerSnapshot, pairPackageFingerprint);
        Pass1PairExecution pair = Pass1PairExecution.Create(
            ExactCrossRepositoryTestValues.Id(12_001),
            coordination.Id,
            ExactCrossRepositoryTestValues.SystemId,
            pairPackageFingerprint,
            pairSelectionFingerprint,
            consumerManifest.Id,
            providerManifest.Id,
            "3333333333333333333333333333333333333333333333333333333333333333",
            "prompt-one",
            "prompt-two",
            "code-logic/file-v2",
            "4444444444444444444444444444444444444444444444444444444444444444",
            "5555555555555555555555555555555555555555555555555555555555555555",
            2,
            50,
            1,
            ExactCrossRepositoryTestValues.Pass1CompletedAt);
        Set(pair, nameof(Pass1PairExecution.Phase), Pass1PairExecutionPhase.ReadyForResolution);
        Set(pair, nameof(Pass1PairExecution.TerminalAt), ExactCrossRepositoryTestValues.Pass1CompletedAt);
        context.Pass1PairExecutions.Add(pair);
        await context.SaveChangesAsync();

        Set(consumer.DocumentVersion, nameof(DocumentVersion.SchemaIdentity), "code-logic/file-v2");
        Set(provider.DocumentVersion, nameof(DocumentVersion.SchemaIdentity), "code-logic/file-v2");
        Set(
            consumer.DocumentVersion,
            nameof(DocumentVersion.ContentJson),
            BuildCodeLogicJson(
                consumer,
                consumerClassification,
                consumerAnchor,
                consumerManifest,
                pairPackageFingerprint,
                File.ReadAllBytes(Path.Combine(
                    fixture.ConsumerRepository.RootPath,
                    consumer.DocumentVersion.RepositoryRelativePath.Replace('/', Path.DirectorySeparatorChar))).Length,
                provider,
                providerSourceHash));
        Set(
            provider.DocumentVersion,
            nameof(DocumentVersion.ContentJson),
            BuildCodeLogicJson(
                provider,
                providerClassification,
                providerAnchor,
                providerManifest,
                pairPackageFingerprint,
                File.ReadAllBytes(Path.Combine(
                    fixture.ProviderRepository.RootPath,
                    provider.DocumentVersion.RepositoryRelativePath.Replace('/', Path.DirectorySeparatorChar))).Length,
                consumer,
                consumerSourceHash));

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

    private static PreparedScanManifest CreatePreparedManifest(
        ExactPass1DocumentFixture document,
        Guid coordinationId,
        ScanContextSnapshot snapshot,
        Vulgata.Infrastructure.CommunicationDetection.CommunicationPatternCatalog catalog,
        string fingerprint,
        string pairSelectionFingerprint)
    {
        PreparedScanManifest manifest = (PreparedScanManifest)Activator.CreateInstance(
            typeof(PreparedScanManifest),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: null,
            culture: null)!;
        Guid manifestId = document.DocumentVersion.RepositoryId ==
            ExactCrossRepositoryTestValues.ConsumerRepositoryId
            ? ExactCrossRepositoryTestValues.Id(11_001)
            : ExactCrossRepositoryTestValues.Id(11_002);
        string hash = new('a', 64);
        Set(manifest, nameof(PreparedScanManifest.Id), manifestId);
        Set(manifest, nameof(PreparedScanManifest.RepositoryId), document.DocumentVersion.RepositoryId);
        Set(manifest, nameof(PreparedScanManifest.ScanRunId), document.ScanRun.Id);
        Set(manifest, nameof(PreparedScanManifest.CoordinationId), coordinationId);
        Set(manifest, nameof(PreparedScanManifest.ProfiledRevision), document.DocumentVersion.ProfiledRevision);
        Set(manifest, nameof(PreparedScanManifest.RequestedRef), ExactCrossRepositoryTestValues.ExpectedRef);
        Set(manifest, nameof(PreparedScanManifest.SchemaIdentity), PreparedScanManifest.CurrentSchemaIdentity);
        Set(manifest, nameof(PreparedScanManifest.SnapshotIdentity), hash);
        Set(manifest, nameof(PreparedScanManifest.RuntimeIdentityHash), hash);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphProofIdentityHash), hash);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessIdentityHash), hash);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphRepositoryId), document.DocumentVersion.RepositoryId);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphScanRunId), document.ScanRun.Id);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphProfiledRevision), document.DocumentVersion.ProfiledRevision);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphSnapshotIdentity), hash);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphSnapshotRootBindingHash), hash);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphRuntimeIdentityHash), hash);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphIndexIdentity), "fixture-index");
        Set(manifest, nameof(PreparedScanManifest.CodeGraphProofSchemaIdentity), "fixture-codegraph-proof/1.0");
        Set(manifest, nameof(PreparedScanManifest.CodeGraphInitialized), true);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphIndexState), CodeGraphIndexState.Complete);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphLogicalProjectBinding), "fixture-project");
        Set(manifest, nameof(PreparedScanManifest.CodeGraphLogicalIndexBinding), "fixture-index");
        Set(manifest, nameof(PreparedScanManifest.CodeGraphRepositoryBindingMatched), true);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphScanRunBindingMatched), true);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphRevisionBindingMatched), true);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphRuntimeBindingMatched), true);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphSnapshotBindingMatched), true);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphProjectPathTrusted), true);
        Set(manifest, nameof(PreparedScanManifest.CodeGraphIndexPathTrusted), true);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessRepositoryId), document.DocumentVersion.RepositoryId);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessScanRunId), document.ScanRun.Id);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessProfiledRevision), document.DocumentVersion.ProfiledRevision);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessSnapshotIdentity), hash);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessRuntimeIdentityHash), hash);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessWorkspaceBindingHash), hash);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessSchemaIdentity), "fixture-java-readiness/1.0");
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessOutcome), JavaReadinessOutcome.Ready);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessJdtLsRelease), "fixture-jdtls");
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessJdtLsDistributionArchiveHash), hash);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessJavaMajor), 21);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessStreamJsonRpcVersion), "2.0");
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessTransport), AnalysisTransport.Stdio);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessWorkspaceExclusive), true);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessInitializeCompleted), true);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessInitializedNotificationSent), true);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessImportState), JavaImportState.Usable);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessRepositoryBindingMatched), true);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessScanRunBindingMatched), true);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessRevisionBindingMatched), true);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessRuntimeBindingMatched), true);
        Set(manifest, nameof(PreparedScanManifest.JavaReadinessSnapshotBindingMatched), true);
        Set(manifest, nameof(PreparedScanManifest.CatalogSchemaIdentity), catalog.SchemaIdentity);
        Set(manifest, nameof(PreparedScanManifest.CatalogHash), catalog.CatalogSha256);
        Set(manifest, nameof(PreparedScanManifest.CandidateCount), 1);
        Set(manifest, nameof(PreparedScanManifest.EligibleCount), 1);
        Set(manifest, nameof(PreparedScanManifest.SelectedCount), 1);
        Set(manifest, nameof(PreparedScanManifest.TotalConfirmedEvidenceCount), 1);
        Set(manifest, nameof(PreparedScanManifest.RetainedClassificationCount), 1);
        Set(manifest, nameof(PreparedScanManifest.ContextSnapshotId), snapshot.Id);
        Set(manifest, nameof(PreparedScanManifest.PreparedScanContextIdentityHash), snapshot.PreparedScanContextIdentityHash!);
        Set(manifest, nameof(PreparedScanManifest.ContextContentHash), snapshot.ContentHash);
        Set(manifest, nameof(PreparedScanManifest.PairSelectionFingerprint), pairSelectionFingerprint);
        Set(manifest, nameof(PreparedScanManifest.Fingerprint), fingerprint);
        Set(manifest, nameof(PreparedScanManifest.CreatedAt), ExactCrossRepositoryTestValues.Pass1CompletedAt);
        Set(manifest, nameof(PreparedScanManifest.ApprovedAt), ExactCrossRepositoryTestValues.Pass1CompletedAt);
        return manifest;
    }

    private static void SetPreparedBinding(
        VulgataDbContext context,
        ExactPass1DocumentFixture document,
        PreparedScanManifest manifest,
        ScanContextSnapshot snapshot,
        string pairPackageFingerprint)
    {
        PreScanResult result = context.PreScanResults.Local.Single(value =>
            value.Id == document.ScanRun.Id && value.RepositoryId == document.DocumentVersion.RepositoryId);
        Set(result, nameof(PreScanResult.SchemaVersion), PreScanResult.PreparedSchemaVersion);
        Set(result, nameof(PreScanResult.PreparedScanManifestId), manifest.Id);
        Set(result, nameof(PreScanResult.PreparedScanManifestFingerprint), manifest.Fingerprint);
        Set(result, nameof(PreScanResult.ContextSnapshotId), snapshot.Id);
        Set(result, nameof(PreScanResult.PreparedScanContextIdentityHash), snapshot.PreparedScanContextIdentityHash);
        Set(result, nameof(PreScanResult.ContextContentHash), snapshot.ContentHash);
        Set(result, nameof(PreScanResult.HandoffSchemaIdentity), PreScanResult.PreparedHandoffSchemaIdentity);
        Set(result, nameof(PreScanResult.PairPackageFingerprint), pairPackageFingerprint);
    }

    private static void Set<T>(object target, string propertyName, T value) =>
        target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);

    private static SourceAnchor ValidAnchor(SourceAnchor anchor, string sourceHash) =>
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
            sourceHash,
            anchor.CreatedAt);

    private static string ComputeSourceHash(string rootPath, string relativePath)
    {
        string path = Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    }

    private static string BuildCodeLogicJson(
        ExactPass1DocumentFixture document,
        CommunicationKindClassification classification,
        SourceAnchor anchor,
        PreparedScanManifest manifest,
        string pairPackageFingerprint,
        int sourceByteLength,
        ExactPass1DocumentFixture target,
        string targetSourceHash)
    {
        string sourceHash = anchor.SourceHash;
        string catalogRuleIdentity = classification.CatalogRuleIdentity!;
        string anchorKey = "a001";
        Guid receiptId = ExactCrossRepositoryTestValues.Id(13_001);
        object payload = new
        {
            schemaIdentity = "code-logic/file-v2",
            binding = new
            {
                jobId = (document.DocumentVersion.RepositoryId == ExactCrossRepositoryTestValues.ConsumerRepositoryId
                    ? ExactCrossRepositoryTestValues.ConsumerPass1JobId
                    : ExactCrossRepositoryTestValues.ProviderPass1JobId).ToString("D"),
                scanExecutionId = document.ScanExecution.Id.ToString("D"),
                systemId = ExactCrossRepositoryTestValues.SystemId.ToString("D"),
                repositoryId = document.DocumentVersion.RepositoryId.ToString("D"),
                scanRunId = document.ScanRun.Id.ToString("D"),
                profiledRevision = document.DocumentVersion.ProfiledRevision,
                fileCodeUnitId = document.CodeUnit.Id.ToString("D"),
                fileCodeUnitIdentityFingerprint = sourceHash,
                preparedScanManifestId = manifest.Id.ToString("D"),
                preparedScanManifestFingerprint = manifest.Fingerprint,
                pairPackageFingerprint,
                repositoryRelativePath = document.DocumentVersion.RepositoryRelativePath,
                byteStart = 0,
                byteEnd = sourceByteLength,
                lineStart = document.SourceAnchor.LineStart,
                lineEnd = document.SourceAnchor.LineEnd,
                segmentOrdinal = 1,
                segmentCount = 1,
                fileHash = sourceHash,
                sourceHash,
                promptIdentity = "fixture-prompt",
                documentSchemaIdentity = "code-logic/file-v2",
                analysisRuntimeIdentityHash = new string('a', 64),
            },
            sourceAnchors = new[]
            {
                new
                {
                    key = anchorKey,
                    repositoryRelativePath = anchor.RepositoryRelativePath,
                    byteStart = 0,
                    byteEnd = sourceByteLength,
                    lineStart = anchor.LineStart,
                    lineEnd = anchor.LineEnd,
                    sourceHash,
                    evidenceLocator = anchor.EvidenceLocator,
                },
            },
            filePurpose = new { summary = "fixture", sourceAnchorRefs = new[] { anchorKey }, responsibilities = new[] { "fixture" } },
            internalUnits = Array.Empty<object>(),
            executionFlow = Array.Empty<object>(),
            dataFlow = Array.Empty<object>(),
            rules = Array.Empty<object>(),
            dataAccess = Array.Empty<object>(),
            communicationEvidence = new[]
            {
                new
                {
                    order = 1,
                    communicationKind = "Rpc",
                    observation = "fixture communication",
                    classificationEvidence = new[]
                    {
                        new
                        {
                            classificationId = classification.Id.ToString("D"),
                            classificationFingerprint = classification.Fingerprint,
                            communicationKind = "Rpc",
                            level = classification.Level.ToString(),
                            confidence = classification.Confidence.ToString(),
                            evidenceRule = classification.Rule.ToString(),
                            provenance = classification.Provenance.ToString(),
                            catalogRuleIdentity,
                            evidenceLocator = anchor.EvidenceLocator,
                        },
                    },
                    navigationReceiptIds = Array.Empty<Guid>(),
                    sourceAnchorRefs = new[] { anchorKey },
                },
            },
            knownReferences = Array.Empty<object>(),
            deferredObservations = new[]
            {
                new
                {
                    order = 1,
                    relationshipKind = "References",
                    outcome = "CrossRepository",
                    sourceClaimLocator = anchor.EvidenceLocator,
                    targetSymbol = document.DocumentVersion.RepositoryId ==
                        ExactCrossRepositoryTestValues.ConsumerRepositoryId
                        ? "cust/updateSession"
                        : target.DocumentVersion.Title,
                    targetRepositoryRelativePath = target.DocumentVersion.RepositoryRelativePath,
                    targetSourceHash,
                    navigationOperation = "ExactSourceEvidence",
                    navigationDepth = 1,
                    navigationReceiptIds = new[] { receiptId },
                    sourceAnchorRefs = new[] { anchorKey },
                },
            },
            assumptions = Array.Empty<object>(),
        };
        return JsonSerializer.Serialize(payload);
    }

    private static async Task InvokeReconstructAsync(
        object source,
        MethodInfo reconstruct,
        ExactResolutionRuntimeRequest request,
        RepositoryAccessScope scope)
    {
        try
        {
            Task operation = (Task)reconstruct.Invoke(
                source,
                [request, scope, CancellationToken.None])!;
            await operation;
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException ?? exception;
        }
    }

    private sealed class FixtureRevisionBoundSourceReader(ExactCrossRepositoryTestFixture fixture)
        : IRevisionBoundSourceReader
    {
        public Task<SourceExcerptResult> ReadAsync(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken)
        {
            string root = scope.RepositoryId == ExactCrossRepositoryTestValues.ConsumerRepositoryId
                ? fixture.ConsumerRepository.RootPath
                : fixture.ProviderRepository.RootPath;
            string path = Path.Combine(root, request.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            byte[] bytes = File.ReadAllBytes(path);
            string sourceHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!StringComparer.Ordinal.Equals(request.ExpectedSourceSha256, sourceHash))
            {
                return Task.FromResult(new SourceExcerptResult(
                    AnalysisOperationStatus.Failed,
                    scope.RepositoryId,
                    scope.ProfiledRevision,
                    request.RelativePath,
                    request.Range,
                    sourceHash,
                    string.Empty,
                    0,
                    new AnalysisPortFailure(
                        AnalysisPortFailureReason.IntegrityMismatch,
                        "source_hash_mismatch",
                        "source hash mismatch")));
            }
            return Task.FromResult(new SourceExcerptResult(
                AnalysisOperationStatus.Succeeded,
                scope.RepositoryId,
                scope.ProfiledRevision,
                request.RelativePath,
                request.Range,
                sourceHash,
                Encoding.UTF8.GetString(bytes),
                bytes.Length,
                null));
        }
    }

    private sealed class MismatchingRevisionBoundSourceReader : IRevisionBoundSourceReader
    {
        public Task<SourceExcerptResult> ReadAsync(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SourceExcerptResult(
                AnalysisOperationStatus.Succeeded,
                scope.RepositoryId,
                scope.ProfiledRevision,
                request.RelativePath,
                request.Range,
                new string('f', 64),
                "revision-bound mismatch",
                24,
                null));
    }

    private sealed class ChangedProviderSourceReader(
        ExactCrossRepositoryTestFixture fixture,
        string changedXml,
        string changedHash) : IRevisionBoundSourceReader
    {
        public Task<SourceExcerptResult> ReadAsync(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken)
        {
            if (scope.RepositoryId == ExactCrossRepositoryTestValues.ProviderRepositoryId)
            {
                Assert.Equal(1, request.Range.StartLine);
                Assert.Equal(3, request.Range.EndLine);
                return Task.FromResult(new SourceExcerptResult(
                    AnalysisOperationStatus.Succeeded,
                    scope.RepositoryId,
                    scope.ProfiledRevision,
                    request.RelativePath,
                    request.Range,
                    changedHash,
                    changedXml,
                    Encoding.UTF8.GetByteCount(changedXml),
                    null));
            }

            string path = Path.Combine(
                fixture.ConsumerRepository.RootPath,
                request.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            byte[] bytes = File.ReadAllBytes(path);
            return Task.FromResult(new SourceExcerptResult(
                AnalysisOperationStatus.Succeeded,
                scope.RepositoryId,
                scope.ProfiledRevision,
                request.RelativePath,
                request.Range,
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                Encoding.UTF8.GetString(bytes),
                bytes.Length,
                null));
        }
    }

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
