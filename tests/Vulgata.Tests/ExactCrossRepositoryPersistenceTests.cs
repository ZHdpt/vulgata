using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class ExactCrossRepositoryPersistenceTests
{
    [Fact]
    public async Task ApplyAsync_ReviewedUnsupportedObservation_PersistsCanonicalNonterminalUncertaintyAsync()
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
        ExactPass1DocumentFixture unsupported = fixture.CreatePass1Document(
            ExactPass1DocumentKind.UnsupportedConsumer,
            ProfileCommit(profile, occurrence.ConsumerAlias));
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedPass1Async(context, fixture, consumer, provider, unsupported);
        ExactResolutionDecisionBatch batch = await ExactBatchAsync(
            profile,
            occurrence,
            consumer,
            provider,
            unsupportedDocument: unsupported);

        ExactResolutionBatchOutcome outcome = await new ExactCrossRepositoryResolutionStore(
            context).ApplyAsync(
                batch,
                new RepositoryAccessScope(
                    [consumer.DocumentVersion.RepositoryId, provider.DocumentVersion.RepositoryId]));

        Assert.Equal(ExactResolutionOutcomeKind.Applied, outcome.Kind);
        Assert.Contains(
            outcome.Items,
            item => item.Identity == Assert.Single(profile.UnsupportedObservations).ObservationId &&
                item.Kind == ExactResolutionOutcomeKind.UncertaintyRecorded);
        Uncertainty uncertainty = await context.Uncertainties.AsNoTracking().SingleAsync();
        Assert.Equal(unsupported.DocumentVersion.Id, uncertainty.DocumentVersionId);
        Assert.Equal(unsupported.SourceAnchor.Id, uncertainty.SourceAnchorId);
        Assert.Equal("unsupported_communication", uncertainty.Type);
        Assert.Equal("unresolved", uncertainty.Status);
        Assert.Equal("unknown_target", uncertainty.SubStatus);
        Assert.Equal("unsupported_kind", uncertainty.ReasonCode);
        Assert.Equal("file_storage", uncertainty.ObservationKind);
        Assert.Null(uncertainty.ResolvedAt);
        Assert.Null(uncertainty.ResolvedByDocumentVersionId);
        Assert.Single(await context.DocumentEdges.AsNoTracking().ToArrayAsync());
        Assert.Single(await context.ExactCrossRepositoryEdgeEvidence.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task ApplyAsync_EligibleNonExactReasons_UseOnlyCanonicalUnresolvedSubStatusesAsync()
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
        ExactPass1DocumentFixture unsupported = fixture.CreatePass1Document(
            ExactPass1DocumentKind.UnsupportedConsumer,
            ProfileCommit(profile, occurrence.ConsumerAlias));
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedPass1Async(context, fixture, consumer, provider, unsupported);
        var store = new ExactCrossRepositoryResolutionStore(context);
        RepositoryAccessScope scope = new(
            [consumer.DocumentVersion.RepositoryId, provider.DocumentVersion.RepositoryId]);
        (string Reason, bool HasTarget, string SubStatus)[] cases =
        [
            ("variable_target", true, "cross_repo"),
            ("disallowed_invocation", false, "unknown_target"),
            ("malformed_provider", false, "unknown_target"),
            ("provider_out_of_root", false, "unknown_target"),
            ("unmatched", false, "unknown_target"),
            ("multiple_providers", true, "ambiguous"),
            ("normalization_mismatch", false, "unknown_target"),
        ];

        foreach ((string reason, bool hasTarget, _) in cases)
        {
            ExactResolutionBatchOutcome outcome = await store.ApplyAsync(
                NonExactBatch(
                    profile,
                    occurrence,
                    consumer,
                    provider,
                    unsupported,
                    reason,
                    hasTarget),
                scope);

            Assert.Equal(ExactResolutionOutcomeKind.Applied, outcome.Kind);
            Assert.Contains(
                outcome.Items,
                item => item.Identity == occurrence.OccurrenceId &&
                    item.Kind == ExactResolutionOutcomeKind.UncertaintyRecorded);
        }

        Uncertainty[] uncertainties =
        [
            .. await context.Uncertainties.AsNoTracking()
                .Where(uncertainty => uncertainty.Type == "cross_repository")
                .ToArrayAsync(),
        ];
        Assert.Equal(cases.Length, uncertainties.Length);
        Assert.All(uncertainties, uncertainty => Assert.Equal("unresolved", uncertainty.Status));
        Assert.Equal(
            cases.Select(value => (value.Reason, value.SubStatus)).Order(),
            uncertainties.Select(value => (value.ReasonCode, value.SubStatus)).Order());
        Assert.DoesNotContain(uncertainties, uncertainty => uncertainty.Status == "wontfix");
        Assert.Empty(await context.DocumentEdges.AsNoTracking().ToArrayAsync());
        Assert.Empty(await context.ExactCrossRepositoryEdgeEvidence.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task ApplyAsync_UnsupportedReplayAndDurableDrift_IsExactAndConflictSafeAsync()
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
        ExactPass1DocumentFixture unsupported = fixture.CreatePass1Document(
            ExactPass1DocumentKind.UnsupportedConsumer,
            ProfileCommit(profile, occurrence.ConsumerAlias));
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedPass1Async(context, fixture, consumer, provider, unsupported);
        ExactResolutionDecisionBatch batch = await ExactBatchAsync(
            profile,
            occurrence,
            consumer,
            provider,
            unsupportedDocument: unsupported);
        var store = new ExactCrossRepositoryResolutionStore(context);
        RepositoryAccessScope scope = new(
            [consumer.DocumentVersion.RepositoryId, provider.DocumentVersion.RepositoryId]);

        _ = await store.ApplyAsync(batch, scope);
        Uncertainty original = await context.Uncertainties.AsNoTracking().SingleAsync();
        ExactResolutionBatchOutcome replay = await store.ApplyAsync(batch, scope);

        Assert.Equal(ExactResolutionOutcomeKind.AlreadyApplied, replay.Kind);
        Assert.Contains(
            replay.Items,
            item => item.Identity == Assert.Single(profile.UnsupportedObservations).ObservationId &&
                item.Kind == ExactResolutionOutcomeKind.AlreadyRecorded);
        Uncertainty replayed = await context.Uncertainties.AsNoTracking().SingleAsync();
        Assert.Equal(original.Id, replayed.Id);
        Assert.Equal(original.CreatedAt, replayed.CreatedAt);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Uncertainties\" SET \"EvidenceLocator\" = {'x' + original.EvidenceLocator} WHERE \"Id\" = {original.Id}");
        ExactResolutionBatchOutcome conflict = await store.ApplyAsync(batch, scope);

        Assert.Equal(ExactResolutionOutcomeKind.Conflict, conflict.Kind);
        Assert.Single(await context.Uncertainties.AsNoTracking().ToArrayAsync());
        Assert.Equal(
            'x' + original.EvidenceLocator,
            (await context.Uncertainties.AsNoTracking().SingleAsync()).EvidenceLocator);
    }

    [Fact]
    public async Task Model_Uncertainty_RejectsUniqueForeignKeyAndClosedCheckViolationsAsync()
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
        ExactPass1DocumentFixture unsupported = fixture.CreatePass1Document(
            ExactPass1DocumentKind.UnsupportedConsumer,
            ProfileCommit(profile, occurrence.ConsumerAlias));
        await using VulgataDbContext context = fixture.Database.CreateContext();
        await SeedPass1Async(context, fixture, consumer, provider, unsupported);
        _ = await new ExactCrossRepositoryResolutionStore(context).ApplyAsync(
            await ExactBatchAsync(
                profile,
                occurrence,
                consumer,
                provider,
                unsupportedDocument: unsupported),
            new RepositoryAccessScope(
                [consumer.DocumentVersion.RepositoryId, provider.DocumentVersion.RepositoryId]));
        Uncertainty original = await context.Uncertainties.AsNoTracking().SingleAsync();

        context.Uncertainties.Add(CopyUncertainty(
            original,
            Guid.NewGuid(),
            original.DocumentVersionId,
            original.EvidenceFingerprint));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        context.Uncertainties.Add(CopyUncertainty(
            original,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('a', 64),
            includeSourceAnchor: false));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        Uncertainty checkFailure = CopyUncertainty(
            original,
            Guid.NewGuid(),
            original.DocumentVersionId,
            new string('b', 64));
        context.Uncertainties.Add(checkFailure);
        context.Entry(checkFailure).Property(value => value.SubStatus).CurrentValue = "terminal";
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        Assert.Single(await context.Uncertainties.AsNoTracking().ToArrayAsync());
        Assert.Single(await context.DocumentEdges.AsNoTracking().ToArrayAsync());
        Assert.Single(await context.ExactCrossRepositoryEdgeEvidence.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task ApplyAsync_UnauthorizedScope_RefusesBeforeAnyDatabaseAccessAsync()
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
        ExactPass1DocumentFixture unsupported = fixture.CreatePass1Document(
            ExactPass1DocumentKind.UnsupportedConsumer,
            ProfileCommit(profile, occurrence.ConsumerAlias));
        await using (VulgataDbContext seedContext = fixture.Database.CreateContext())
        {
            await SeedPass1Async(seedContext, fixture, consumer, provider, unsupported);
        }

        fixture.Access.ResetDataAccessCounters();
        await using VulgataDbContext guardedContext = fixture.Database.CreateContext(fixture.Access);
        ExactResolutionBatchOutcome outcome = await new ExactCrossRepositoryResolutionStore(
            guardedContext).ApplyAsync(
                await ExactBatchAsync(
                    profile,
                    occurrence,
                    consumer,
                    provider,
                    unsupportedDocument: unsupported),
                fixture.Access.ConsumerOnlyScope);

        Assert.Equal(ExactResolutionOutcomeKind.Refused, outcome.Kind);
        Assert.True(fixture.Access.Snapshot().IsZero);
    }

    [Fact]
    public async Task ApplyAsync_IdenticalReplayAndDrift_PreservesImmutableTruthAsync()
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
        await SeedPass1Async(context, fixture, consumer, provider);
        ExactResolutionDecisionBatch batch = await ExactBatchAsync(
            profile,
            occurrence,
            consumer,
            provider);
        var store = new ExactCrossRepositoryResolutionStore(context);
        RepositoryAccessScope scope = new(
            [consumer.DocumentVersion.RepositoryId, provider.DocumentVersion.RepositoryId]);

        ExactResolutionBatchOutcome first = await store.ApplyAsync(batch, scope);
        DocumentEdge firstEdge = await context.DocumentEdges.AsNoTracking().SingleAsync();
        ExactCrossRepositoryEdgeEvidence firstEvidence = await context
            .ExactCrossRepositoryEdgeEvidence.AsNoTracking().SingleAsync();
        ExactResolutionBatchOutcome replay = await store.ApplyAsync(batch, scope);
        ExactResolutionDecisionBatch drift = await ExactBatchAsync(
            profile,
            occurrence,
            consumer,
            provider,
            " cust/updateSession ");
        ExactResolutionBatchOutcome conflict = await store.ApplyAsync(drift, scope);

        Assert.Equal(ExactResolutionOutcomeKind.Applied, first.Kind);
        Assert.Equal(ExactResolutionOutcomeKind.AlreadyApplied, replay.Kind);
        Assert.Contains(
            replay.Items,
            item => item.Identity == occurrence.OccurrenceId &&
                item.Kind == ExactResolutionOutcomeKind.AlreadyApplied);
        Assert.Contains(
            replay.Items,
            item => item.Identity == Assert.Single(profile.UnsupportedObservations).ObservationId &&
                item.Kind == ExactResolutionOutcomeKind.AlreadyRecorded);
        Assert.Equal(ExactResolutionOutcomeKind.Conflict, conflict.Kind);
        DocumentEdge durableEdge = await context.DocumentEdges.AsNoTracking().SingleAsync();
        ExactCrossRepositoryEdgeEvidence durableEvidence = await context
            .ExactCrossRepositoryEdgeEvidence.AsNoTracking().SingleAsync();
        Assert.Equal(firstEdge.Id, durableEdge.Id);
        Assert.Equal(firstEdge.CreatedAt, durableEdge.CreatedAt);
        Assert.Equal(firstEvidence.EvidenceFingerprint, durableEvidence.EvidenceFingerprint);
        Assert.Equal(firstEvidence.CreatedAt, durableEvidence.CreatedAt);
    }

    [Fact]
    public async Task ApplyAsync_StaleCurrentPointerOrEvidenceFailure_RollsBackWholeWriteAsync()
    {
        await using ExactCrossRepositoryTestFixture staleFixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactPass1DocumentFixture staleConsumer = staleFixture.CreatePass1Document(
            ExactPass1DocumentKind.Consumer,
            ProfileCommit(profile, occurrence.ConsumerAlias));
        ExactPass1DocumentFixture staleProvider = staleFixture.CreatePass1Document(
            ExactPass1DocumentKind.Provider,
            ProfileCommit(profile, occurrence.ProviderAlias));
        await using (VulgataDbContext staleContext = staleFixture.Database.CreateContext())
        {
            await SeedPass1Async(staleContext, staleFixture, staleConsumer, staleProvider);
            ExactResolutionDecisionBatch staleBatch = await ExactBatchAsync(
                profile,
                occurrence,
                staleConsumer,
                staleProvider);
            await staleContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = NULL WHERE \"Id\" = {staleProvider.LogicalDocument.Id}");

            ExactResolutionBatchOutcome stale = await new ExactCrossRepositoryResolutionStore(
                staleContext).ApplyAsync(
                    staleBatch,
                    new RepositoryAccessScope(
                        [staleConsumer.DocumentVersion.RepositoryId, staleProvider.DocumentVersion.RepositoryId]));

            Assert.Equal(ExactResolutionOutcomeKind.Conflict, stale.Kind);
            Assert.Empty(await staleContext.DocumentEdges.AsNoTracking().ToArrayAsync());
            Assert.Empty(await staleContext.ExactCrossRepositoryEdgeEvidence.AsNoTracking()
                .ToArrayAsync());
            Assert.Empty(await staleContext.Uncertainties.AsNoTracking().ToArrayAsync());
        }

        await using ExactCrossRepositoryTestFixture rollbackFixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactPass1DocumentFixture rollbackConsumer = rollbackFixture.CreatePass1Document(
            ExactPass1DocumentKind.Consumer,
            ProfileCommit(profile, occurrence.ConsumerAlias));
        ExactPass1DocumentFixture rollbackProvider = rollbackFixture.CreatePass1Document(
            ExactPass1DocumentKind.Provider,
            ProfileCommit(profile, occurrence.ProviderAlias));
        await using VulgataDbContext rollbackContext = rollbackFixture.Database.CreateContext();
        await SeedPass1Async(
            rollbackContext,
            rollbackFixture,
            rollbackConsumer,
            rollbackProvider);
        await rollbackContext.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER FailExactEvidence BEFORE INSERT ON \"ExactCrossRepositoryEdgeEvidence\" " +
            "BEGIN SELECT RAISE(ABORT, 'bounded-test-failure'); END;");
        ExactResolutionDecisionBatch rollbackBatch = await ExactBatchAsync(
            profile,
            occurrence,
            rollbackConsumer,
            rollbackProvider);

        ExactResolutionBatchOutcome rolledBack = await new ExactCrossRepositoryResolutionStore(
            rollbackContext).ApplyAsync(
                rollbackBatch,
                new RepositoryAccessScope(
                    [rollbackConsumer.DocumentVersion.RepositoryId, rollbackProvider.DocumentVersion.RepositoryId]));

        Assert.Equal(ExactResolutionOutcomeKind.Conflict, rolledBack.Kind);
        Assert.Empty(await rollbackContext.DocumentEdges.AsNoTracking().ToArrayAsync());
        Assert.Empty(await rollbackContext.ExactCrossRepositoryEdgeEvidence.AsNoTracking()
            .ToArrayAsync());
        Assert.Empty(await rollbackContext.Uncertainties.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task ApplyAsync_ExactDecision_AtomicallyPersistsEdgeAndEvidenceAsync()
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
        await SeedPass1Async(context, fixture, consumer, provider);
        ExactResolutionDecisionBatch batch = await ExactBatchAsync(
            profile,
            occurrence,
            consumer,
            provider);
        var store = new ExactCrossRepositoryResolutionStore(context);

        ExactResolutionBatchOutcome outcome = await store.ApplyAsync(
            batch,
            new RepositoryAccessScope(
                [consumer.DocumentVersion.RepositoryId, provider.DocumentVersion.RepositoryId]));

        Assert.Equal(ExactResolutionOutcomeKind.Applied, outcome.Kind);
        ExactResolutionBatchItemOutcome item = Assert.Single(
            outcome.Items,
            item => item.Identity == occurrence.OccurrenceId);
        Assert.Equal(occurrence.OccurrenceId, item.Identity);
        Assert.Equal(ExactResolutionOutcomeKind.Applied, item.Kind);
        context.ChangeTracker.Clear();
        DocumentEdge edge = await context.DocumentEdges.SingleAsync();
        ExactCrossRepositoryEdgeEvidence evidence =
            await context.ExactCrossRepositoryEdgeEvidence.SingleAsync();
        Assert.Equal(edge.Id, evidence.EdgeId);
        Assert.Equal(DocumentEdgeType.References, edge.EdgeType);
        Assert.Equal(consumer.DocumentVersion.Id, edge.SourceDocumentVersionId);
        Assert.Equal(provider.DocumentVersion.Id, edge.TargetDocumentVersionId);
        Assert.Equal(consumer.SourceAnchor.Id, evidence.ConsumerSourceAnchorId);
        Assert.Equal(provider.SourceAnchor.Id, evidence.ProviderSourceAnchorId);
        Assert.Equal("cust/updateSession", evidence.ConsumerRawIdentifier.Trim());
        Assert.Equal("in/cust/updateSession", evidence.ProviderRawIdentifier.Trim());
        Assert.Equal("cust/updateSession", evidence.NormalizedEndpointIdentifier);
        Assert.Equal(ExactResolutionProtocol.CustomRpc, evidence.Protocol);
        Assert.Equal("ExactConfigured", evidence.ResolutionKind);
        Assert.Equal(1.0000m, evidence.Confidence);
        Assert.Equal("1.0", evidence.ProfileSchemaVersion);
        Assert.Equal(profile.ProfileId, evidence.ProfileId);
        Assert.Equal(profile.ProfileVersion, evidence.ProfileVersion);
        Assert.Equal(profile.ProfileFingerprint, evidence.ProfileFingerprint);
        Assert.Equal(occurrence.OccurrenceId, evidence.OccurrenceId);
        Assert.Matches("^[0-9a-f]{64}$", evidence.EvidenceFingerprint);
        Assert.Equal(edge.CreatedAt, evidence.CreatedAt);
    }

    [Fact]
    public void Model_ExactEdgeEvidence_RequiresOwnerSafeRelationalShape()
    {
        DbContextOptions<VulgataDbContext> options =
            new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
        using var context = new VulgataDbContext(options);

        IModel model = context.GetService<IDesignTimeModel>().Model;
        IEntityType evidence = Assert.IsAssignableFrom<IEntityType>(
            model.FindEntityType(typeof(ExactCrossRepositoryEdgeEvidence)));

        Assert.Equal(nameof(ExactCrossRepositoryEdgeEvidence.EdgeId),
            Assert.Single(evidence.FindPrimaryKey()!.Properties).Name);
        Assert.Equal("ExactCrossRepositoryEdgeEvidence", evidence.GetTableName());
        Assert.Contains(
            evidence.GetForeignKeys(),
            foreignKey =>
                foreignKey.IsUnique &&
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
                foreignKey.PrincipalEntityType.ClrType == typeof(DocumentEdge));
        Assert.Contains(
            evidence.GetForeignKeys(),
            foreignKey => PropertyNames(foreignKey.Properties) ==
                "ConsumerSourceAnchorId,ConsumerDocumentVersionId,ConsumerRepositoryId,ConsumerScanRunId" &&
                PropertyNames(foreignKey.PrincipalKey.Properties) ==
                "Id,DocumentVersionId,RepositoryId,ScanRunId" &&
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(
            evidence.GetForeignKeys(),
            foreignKey => PropertyNames(foreignKey.Properties) ==
                "ProviderSourceAnchorId,ProviderDocumentVersionId,ProviderRepositoryId,ProviderScanRunId" &&
                PropertyNames(foreignKey.PrincipalKey.Properties) ==
                "Id,DocumentVersionId,RepositoryId,ScanRunId" &&
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(
            evidence.GetIndexes(),
            index => index.IsUnique &&
                PropertyNames(index.Properties) == nameof(ExactCrossRepositoryEdgeEvidence.EvidenceFingerprint));

        string[] checkNames =
        [
            .. evidence.GetCheckConstraints()
                .Select(constraint => constraint.Name!)
                .Order(StringComparer.Ordinal),
        ];
        Assert.Equal(
            [
                "CK_ExactCrossRepositoryEdgeEvidence_Confidence_Exact",
                "CK_ExactCrossRepositoryEdgeEvidence_Hashes_Closed",
                "CK_ExactCrossRepositoryEdgeEvidence_Protocol_Closed",
                "CK_ExactCrossRepositoryEdgeEvidence_Repositories_Distinct",
                "CK_ExactCrossRepositoryEdgeEvidence_ResolutionKind_Closed",
                "CK_ExactCrossRepositoryEdgeEvidence_SchemaVersion_Closed",
            ],
            checkNames);

        IEntityType anchor = model.FindEntityType(typeof(SourceAnchor))!;
        Assert.Contains(
            anchor.GetKeys(),
            key => PropertyNames(key.Properties) == "Id,DocumentVersionId,RepositoryId,ScanRunId");
    }

    private static string PropertyNames(IEnumerable<IProperty> properties) =>
        string.Join(',', properties.Select(property => property.Name));

    private static async Task SeedPass1Async(
        VulgataDbContext context,
        ExactCrossRepositoryTestFixture fixture,
        ExactPass1DocumentFixture consumer,
        ExactPass1DocumentFixture provider,
        ExactPass1DocumentFixture? unsupported = null)
    {
        ExactCrossRepositoryManagedFixture managed = fixture.CreateManagedRepositories();
        context.Systems.Add(managed.System);
        context.ScanRuns.AddRange(consumer.ScanRun, provider.ScanRun);
        if (unsupported is not null)
        {
            context.ScanRuns.Add(unsupported.ScanRun);
        }

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
        if (unsupported is not null)
        {
            context.LogicalDocuments.Add(LogicalDocument.Create(
                unsupported.LogicalDocument.Id,
                unsupported.DocumentVersion.RepositoryId,
                DocumentType.CodeLogic,
                "exact-pass1/unsupported-consumer",
                ExactCrossRepositoryTestValues.CreatedAt));
        }

        await context.SaveChangesAsync();
        context.DocumentVersions.AddRange(
            consumer.DocumentVersion,
            provider.DocumentVersion);
        if (unsupported is not null)
        {
            context.DocumentVersions.Add(unsupported.DocumentVersion);
        }

        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {consumer.DocumentVersion.Id} WHERE \"Id\" = {consumer.LogicalDocument.Id}");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {provider.DocumentVersion.Id} WHERE \"Id\" = {provider.LogicalDocument.Id}");
        if (unsupported is not null)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {unsupported.DocumentVersion.Id} WHERE \"Id\" = {unsupported.LogicalDocument.Id}");
        }

        context.ChangeTracker.Clear();
    }

    private static Task<ExactResolutionDecisionBatch> ExactBatchAsync(
        ExactResolutionProfile profile,
        ResolutionOccurrence occurrence,
        ExactPass1DocumentFixture consumer,
        ExactPass1DocumentFixture provider,
        string consumerRawIdentifier = "cust/updateSession",
        ExactPass1DocumentFixture? unsupportedDocument = null)
    {
        var consumerOwner = new ExactResolutionOwner(
            consumer.DocumentVersion.RepositoryId,
            consumer.DocumentVersion.ScanRunId,
            consumer.DocumentVersion.Id,
            consumer.SourceAnchor.Id,
            consumer.DocumentVersion.ProfiledRevision);
        var providerOwner = new ExactResolutionOwner(
            provider.DocumentVersion.RepositoryId,
            provider.DocumentVersion.ScanRunId,
            provider.DocumentVersion.Id,
            provider.SourceAnchor.Id,
            provider.DocumentVersion.ProfiledRevision);
        var consumerCandidate = new ConsumerDispatchCandidate(
            occurrence.OccurrenceId,
            consumerOwner,
            occurrence.Consumer.Invocation.Form,
            occurrence.Consumer.Invocation.ReceiverSymbol,
            occurrence.Consumer.Invocation.MemberName,
            occurrence.Consumer.Invocation.EndpointArgumentIndex,
            occurrence.Consumer.Invocation.Line,
            occurrence.Consumer.Invocation.OccurrenceOrdinalOnLine,
            consumerRawIdentifier,
            consumerRawIdentifier.Trim(),
            "consumer.java#line=45;rule=exact-java-direct-literal-v1");
        var providerCandidate = new ProviderEndpointCandidate(
            occurrence.OccurrenceId,
            providerOwner,
            "in/cust/updateSession",
            "cust/updateSession",
            "provider.xml#line=2;rule=exact-xml-root-attribute-v1");
        ExactPass1DocumentFixture unsupportedOwner = unsupportedDocument ?? consumer;
        var unsupportedOwnerIdentity = new ExactResolutionOwner(
            unsupportedOwner.DocumentVersion.RepositoryId,
            unsupportedOwner.DocumentVersion.ScanRunId,
            unsupportedOwner.DocumentVersion.Id,
            unsupportedOwner.SourceAnchor.Id,
            unsupportedOwner.DocumentVersion.ProfiledRevision);
        var unsupported = new ExactResolutionDecision(
            Assert.Single(profile.UnsupportedObservations).ObservationId,
            ExactResolutionDecisionKind.UnsupportedObservation,
            unsupportedOwnerIdentity,
            null,
            "unsupported_kind",
            "unsupported.java#line=66;rule=unsupported-member-invocation-v1");
        return new ExactCrossRepositoryResolver().ResolveAsync(
            profile,
            [consumerCandidate],
            [providerCandidate],
            [unsupported]);
    }

    private static ExactResolutionDecisionBatch NonExactBatch(
        ExactResolutionProfile profile,
        ResolutionOccurrence occurrence,
        ExactPass1DocumentFixture consumer,
        ExactPass1DocumentFixture provider,
        ExactPass1DocumentFixture unsupported,
        string reasonCode,
        bool hasTarget)
    {
        ExactResolutionOwner consumerOwner = Owner(consumer);
        ExactResolutionOwner providerOwner = Owner(provider);
        var uncertainty = new ExactResolutionDecision(
            occurrence.OccurrenceId,
            ExactResolutionDecisionKind.Uncertainty,
            consumerOwner,
            hasTarget ? providerOwner : null,
            reasonCode,
            $"consumer.java#line=45;rule={reasonCode}");
        var unsupportedDecision = new ExactResolutionDecision(
            Assert.Single(profile.UnsupportedObservations).ObservationId,
            ExactResolutionDecisionKind.UnsupportedObservation,
            Owner(unsupported),
            null,
            "unsupported_kind",
            "unsupported.java#line=66;rule=unsupported-member-invocation-v1");
        return ExactResolutionDecisionBatch.Create(
            profile,
            [unsupportedDecision, uncertainty]);
    }

    private static ExactResolutionOwner Owner(ExactPass1DocumentFixture document) =>
        new(
            document.DocumentVersion.RepositoryId,
            document.DocumentVersion.ScanRunId,
            document.DocumentVersion.Id,
            document.SourceAnchor.Id,
            document.DocumentVersion.ProfiledRevision);

    private static Uncertainty CopyUncertainty(
        Uncertainty source,
        Guid id,
        Guid documentVersionId,
        string evidenceFingerprint,
        bool includeSourceAnchor = true) =>
        new(
            id,
            documentVersionId,
            source.RepositoryId,
            source.ScanRunId,
            includeSourceAnchor ? source.SourceAnchorId : null,
            source.Type,
            source.SubStatus,
            source.ReasonCode,
            source.ObservationKind,
            source.EndpointIdentifier,
            source.EndpointIdentifierNormalized,
            source.TargetRepositoryId,
            source.ProfileSchemaVersion,
            source.ProfileId,
            source.ProfileVersion,
            source.ProfileFingerprint,
            source.OccurrenceId,
            source.EvidenceLocator,
            evidenceFingerprint,
            source.CreatedAt);

    private static string ProfileCommit(ExactResolutionProfile profile, string alias) =>
        Assert.Single(profile.Repositories, repository => repository.Alias == alias)
            .ExpectedCommit;
}
