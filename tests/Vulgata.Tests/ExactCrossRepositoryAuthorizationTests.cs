using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class ExactCrossRepositoryAuthorizationTests
{
    [Fact]
    public void AuthorizedExactRelationship_SafeEvidence_ExposesOnlyOpaqueIdentityAndMetadata()
    {
        var relationship = new AuthorizedExactRelationship(
            "relationship-v1:opaque",
            DocumentEdgeType.References,
            new ExactCrossRepositoryEvidenceReadModel(
                "cust/updateSession",
                "in/cust/updateSession",
                "cust/updateSession",
                ExactResolutionProtocol.CustomRpc,
                "demo-exact-cross-repository-resolution",
                "1.0.0",
                new string('a', 64),
                "account-to-channel-cust-update-session-v1",
                1.0000m,
                "consumer.java#line=45;rule=exact-java-direct-literal-v1",
                "provider.xml#line=2;rule=exact-xml-root-attribute-v1"));

        string json = JsonSerializer.Serialize(relationship);

        Assert.Contains("relationship-v1:opaque", json, StringComparison.Ordinal);
        Assert.Contains("cust/updateSession", json, StringComparison.Ordinal);
        Assert.DoesNotContain("RepositoryId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ScanRunId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DocumentVersionId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SourceAnchorId", json, StringComparison.OrdinalIgnoreCase);

        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        string profileJson = Encoding.UTF8.GetString(profile.CanonicalUtf8Json.Span);
        Assert.DoesNotContain("RepositoryId", profileJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", profileJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", profileJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", profileJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCurrentAsync_ExactRelationship_ProjectsSafeEvidenceOnlyWhenBothEndpointsAuthorizedAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Consumer,
            ProfileCommit(profile, occurrence.ConsumerAlias));
        ExactPass1DocumentFixture provider = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Provider,
            ProfileCommit(profile, occurrence.ProviderAlias));

        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentDocumentsAsync(context, fixture, consumer, provider);
        ExactResolutionDecisionBatch batch = await ExactBatchAsync(
            profile,
            occurrence,
            consumer,
            provider);
        _ = await new ExactCrossRepositoryResolutionStore(context).ApplyAsync(
            batch,
            fixture.Access.AuthorizedScope);
        var repository = new KnowledgeStoreRepository(
            context,
            new KnowledgeDocumentMaterializer());

        CurrentKnowledge authorized = Assert.IsType<CurrentKnowledge>(
            await repository.GetCurrentAsync(
                consumer.LogicalDocument.Id,
                fixture.Access.AuthorizedScope,
                []));
        AuthorizedExactRelationship exact = Assert.IsType<AuthorizedExactRelationship>(
            Assert.Single(authorized.Relationships));
        Assert.Matches("^relationship-v1:[0-9a-f]{64}$", exact.RelationshipIdentity);
        Assert.DoesNotContain(
            (await context.DocumentEdges.AsNoTracking().SingleAsync()).Id.ToString(),
            exact.RelationshipIdentity,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DocumentEdgeType.References, exact.EdgeType);
        Assert.Equal("cust/updateSession", exact.ExactEvidence.ConsumerRawIdentifier);
        Assert.Equal("in/cust/updateSession", exact.ExactEvidence.ProviderRawIdentifier);
        Assert.Equal("cust/updateSession", exact.ExactEvidence.NormalizedIdentifier);
        Assert.Equal(1.0000m, exact.ExactEvidence.Confidence);
        ExactCrossRepositoryEdgeEvidence relationalEvidence =
            await context.ExactCrossRepositoryEdgeEvidence.AsNoTracking().SingleAsync();
        Assert.Equal(consumer.DocumentVersion.Id, relationalEvidence.ConsumerDocumentVersionId);
        Assert.Equal(provider.DocumentVersion.Id, relationalEvidence.ProviderDocumentVersionId);
        Assert.Equal(consumer.SourceAnchor.Id, relationalEvidence.ConsumerSourceAnchorId);
        Assert.Equal(provider.SourceAnchor.Id, relationalEvidence.ProviderSourceAnchorId);
        string exactJson = JsonSerializer.Serialize(exact);
        Assert.DoesNotContain("netTools.execute", exactJson, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", exactJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.ConsumerRepository.RootPath, exactJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.ProviderRepository.RootPath, exactJson, StringComparison.OrdinalIgnoreCase);
        AssertNoInternalIdentity(
            exactJson,
            relationalEvidence.EdgeId,
            relationalEvidence.ConsumerRepositoryId,
            relationalEvidence.ConsumerScanRunId,
            relationalEvidence.ConsumerDocumentVersionId,
            relationalEvidence.ConsumerSourceAnchorId,
            relationalEvidence.ProviderRepositoryId,
            relationalEvidence.ProviderScanRunId,
            relationalEvidence.ProviderDocumentVersionId,
            relationalEvidence.ProviderSourceAnchorId);

        CurrentKnowledge restricted = Assert.IsType<CurrentKnowledge>(
            await repository.GetCurrentAsync(
                consumer.LogicalDocument.Id,
                fixture.Access.ConsumerOnlyScope,
                []));
        RestrictedRelationship marker = Assert.IsType<RestrictedRelationship>(
            Assert.Single(restricted.Relationships));
        Assert.True(marker.IsRestrictedRelationship);
        Assert.Single(typeof(RestrictedRelationship).GetProperties());
        Assert.Equal(
            "{\"IsRestrictedRelationship\":true}",
            JsonSerializer.Serialize(marker));
    }

    [Fact]
    public async Task ApplyAsync_AuthorizationRevokedInsideTransaction_RefusesBeforeQueryOrWriteAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Consumer,
            ProfileCommit(profile, occurrence.ConsumerAlias));
        ExactPass1DocumentFixture provider = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Provider,
            ProfileCommit(profile, occurrence.ProviderAlias));
        ExactResolutionDecisionBatch batch = await ExactBatchAsync(
            profile,
            occurrence,
            consumer,
            provider);
        var verifier = new MutableRepositoryAuthorizationVerifier(true, false);
        var scope = new RepositoryAccessScope(
            [consumer.DocumentVersion.RepositoryId, provider.DocumentVersion.RepositoryId],
            verifier);
        await using VulgataDbContext context = fixture.Database.CreateContext(fixture.Access);
        fixture.Access.ResetDataAccessCounters();

        ExactResolutionBatchOutcome outcome =
            await new ExactCrossRepositoryResolutionStore(context).ApplyAsync(batch, scope);

        Assert.Equal(ExactResolutionOutcomeKind.Refused, outcome.Kind);
        Assert.Equal("refused", outcome.Code);
        Assert.Equal(2, verifier.CallCount);
        Assert.True(fixture.Access.Snapshot().IsZero);
        Assert.Empty(fixture.Logs.Messages);
        AssertNoInternalIdentity(
            JsonSerializer.Serialize(outcome),
            consumer.DocumentVersion.RepositoryId,
            provider.DocumentVersion.RepositoryId,
            consumer.DocumentVersion.Id,
            provider.DocumentVersion.Id,
            consumer.SourceAnchor.Id,
            provider.SourceAnchor.Id);
    }

    [Fact]
    public async Task GetCurrentAsync_AuthorizationRevokedBeforeReturn_IsIndistinguishableFromMissingAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Consumer,
            ProfileCommit(profile, occurrence.ConsumerAlias));
        ExactPass1DocumentFixture provider = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Provider,
            ProfileCommit(profile, occurrence.ProviderAlias));
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentDocumentsAsync(context, fixture, consumer, provider);
        _ = await new ExactCrossRepositoryResolutionStore(context).ApplyAsync(
            await ExactBatchAsync(profile, occurrence, consumer, provider),
            fixture.Access.AuthorizedScope);
        var verifier = new MutableRepositoryAuthorizationVerifier(true, false);
        var liveScope = new RepositoryAccessScope(
            [consumer.DocumentVersion.RepositoryId, provider.DocumentVersion.RepositoryId],
            verifier);
        var repository = new KnowledgeStoreRepository(
            context,
            new KnowledgeDocumentMaterializer());

        CurrentKnowledge? revoked = await repository.GetCurrentAsync(
            consumer.LogicalDocument.Id,
            liveScope,
            []);
        CurrentKnowledge? missing = await repository.GetCurrentAsync(
            ExactCrossRepositoryTestValues.Id(9999),
            fixture.Access.AuthorizedScope,
            []);

        Assert.Null(revoked);
        Assert.Null(missing);
        Assert.Equal(2, verifier.CallCount);
    }

    [Fact]
    public async Task GetUncertaintyAsync_MissingEitherEndpointAuthorization_ReturnsSameNullAsMissingAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Consumer,
            ProfileCommit(profile, occurrence.ConsumerAlias));
        ExactPass1DocumentFixture provider = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Provider,
            ProfileCommit(profile, occurrence.ProviderAlias));
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedCurrentDocumentsAsync(context, fixture, consumer, provider);
        _ = await new ExactCrossRepositoryResolutionStore(context).ApplyAsync(
            TargetedUncertaintyBatch(profile, occurrence, consumer, provider),
            fixture.Access.AuthorizedScope);
        Guid uncertaintyId = await context.Uncertainties.AsNoTracking()
            .Where(value => value.OccurrenceId == occurrence.OccurrenceId)
            .Select(value => value.Id)
            .SingleAsync();
        var repository = new KnowledgeStoreRepository(
            context,
            new KnowledgeDocumentMaterializer());

        AuthorizedUncertaintyReadModel authorized =
            Assert.IsType<AuthorizedUncertaintyReadModel>(
                await repository.GetUncertaintyAsync(
                    uncertaintyId,
                    fixture.Access.AuthorizedScope));
        AuthorizedUncertaintyReadModel? unauthorized =
            await repository.GetUncertaintyAsync(
                uncertaintyId,
                fixture.Access.ConsumerOnlyScope);
        AuthorizedUncertaintyReadModel? missing =
            await repository.GetUncertaintyAsync(
                ExactCrossRepositoryTestValues.Id(9998),
                fixture.Access.AuthorizedScope);

        Assert.Equal("cross_repository", authorized.Type);
        Assert.Equal("unresolved", authorized.Status);
        Assert.Equal("ambiguous", authorized.SubStatus);
        Assert.Equal("multiple_providers", authorized.ReasonCode);
        AssertNoInternalIdentity(
            JsonSerializer.Serialize(authorized),
            uncertaintyId,
            consumer.DocumentVersion.RepositoryId,
            provider.DocumentVersion.RepositoryId,
            consumer.DocumentVersion.Id,
            provider.DocumentVersion.Id,
            consumer.SourceAnchor.Id,
            provider.SourceAnchor.Id);
        Assert.Null(unauthorized);
        Assert.Null(missing);
    }

    private static async Task SeedCurrentDocumentsAsync(
        VulgataDbContext context,
        ExactCrossRepositoryTestFixture fixture,
        ExactPass1DocumentFixture consumer,
        ExactPass1DocumentFixture provider)
    {
        ExactCrossRepositoryManagedFixture managed = fixture.CreateManagedRepositories();
        context.Systems.Add(managed.System);
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
                ExactCrossRepositoryTestValues.CreatedAt));
        await context.SaveChangesAsync();

        context.DocumentVersions.AddRange(consumer.DocumentVersion, provider.DocumentVersion);
        context.IndexEntries.Add(IndexEntry.Create(
            ExactCrossRepositoryTestValues.Id(1001),
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
        context.ChangeTracker.Clear();
    }

    private static Task<ExactResolutionDecisionBatch> ExactBatchAsync(
        ExactResolutionProfile profile,
        ResolutionOccurrence occurrence,
        ExactPass1DocumentFixture consumer,
        ExactPass1DocumentFixture provider)
    {
        ExactResolutionOwner consumerOwner = Owner(consumer);
        ExactResolutionOwner providerOwner = Owner(provider);
        var consumerCandidate = new ConsumerDispatchCandidate(
            occurrence.OccurrenceId,
            consumerOwner,
            occurrence.Consumer.Invocation.Form,
            occurrence.Consumer.Invocation.ReceiverSymbol,
            occurrence.Consumer.Invocation.MemberName,
            occurrence.Consumer.Invocation.EndpointArgumentIndex,
            occurrence.Consumer.Invocation.Line,
            occurrence.Consumer.Invocation.OccurrenceOrdinalOnLine,
            "cust/updateSession",
            "cust/updateSession",
            "consumer.java#line=45;rule=exact-java-direct-literal-v1");
        var providerCandidate = new ProviderEndpointCandidate(
            occurrence.OccurrenceId,
            providerOwner,
            "in/cust/updateSession",
            "cust/updateSession",
            "provider.xml#line=2;rule=exact-xml-root-attribute-v1");
        var unsupported = new ExactResolutionDecision(
            Assert.Single(profile.UnsupportedObservations).ObservationId,
            ExactResolutionDecisionKind.UnsupportedObservation,
            consumerOwner,
            null,
            "unsupported_kind",
            "unsupported.java#line=66;rule=unsupported-member-invocation-v1");
        return new ExactCrossRepositoryResolver().ResolveAsync(
            profile,
            [consumerCandidate],
            [providerCandidate],
            [unsupported]);
    }

    private static ExactResolutionDecisionBatch TargetedUncertaintyBatch(
        ExactResolutionProfile profile,
        ResolutionOccurrence occurrence,
        ExactPass1DocumentFixture consumer,
        ExactPass1DocumentFixture provider)
    {
        var uncertainty = new ExactResolutionDecision(
            occurrence.OccurrenceId,
            ExactResolutionDecisionKind.Uncertainty,
            Owner(consumer),
            Owner(provider),
            "multiple_providers",
            "consumer.java#line=45;rule=multiple_providers");
        var unsupported = new ExactResolutionDecision(
            Assert.Single(profile.UnsupportedObservations).ObservationId,
            ExactResolutionDecisionKind.UnsupportedObservation,
            Owner(consumer),
            null,
            "unsupported_kind",
            "unsupported.java#line=66;rule=unsupported-member-invocation-v1");
        return ExactResolutionDecisionBatch.Create(
            profile,
            [unsupported, uncertainty]);
    }

    private static ExactResolutionOwner Owner(ExactPass1DocumentFixture document) =>
        new(
            document.DocumentVersion.RepositoryId,
            document.DocumentVersion.ScanRunId,
            document.DocumentVersion.Id,
            document.SourceAnchor.Id,
            document.DocumentVersion.ProfiledRevision);

    private static string ProfileCommit(ExactResolutionProfile profile, string alias) =>
        Assert.Single(
            profile.Repositories,
            repository => StringComparer.Ordinal.Equals(repository.Alias, alias)).ExpectedCommit;

    private static void AssertNoInternalIdentity(string serialized, params Guid[] ids)
    {
        foreach (Guid id in ids)
        {
            Assert.DoesNotContain(id.ToString("D"), serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(id.ToString("N"), serialized, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class MutableRepositoryAuthorizationVerifier(params bool[] decisions) :
        IRepositoryAuthorizationVerifier
    {
        private readonly Queue<bool> _decisions = new(decisions);

        internal int CallCount { get; private set; }

        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(2, requiredRepositoryIds.Count);
            CallCount++;
            return ValueTask.FromResult(_decisions.Count > 0 && _decisions.Dequeue());
        }
    }
}
