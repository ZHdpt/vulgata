using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.Migrations;
using Xunit.Abstractions;

namespace Vulgata.Tests.PreparedPass2;

public sealed class ExactCrossRepositoryPostgreSqlTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ApplyAsync_ExactBatchPersistsV2DecisionContributionsAndOneReusableEdgeAsync()
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            output.WriteLine(
                "Qualification not executed: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
            return;
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        string databaseName = database.DatabaseName;
        output.WriteLine("Owned disposable PostgreSQL database: {0}", databaseName);
        try
        {
            await using ExactCrossRepositoryTestFixture fixture =
                await ExactCrossRepositoryTestFixture.CreateAsync();
            await using VulgataDbContext context = CreateContext(database.ConnectionString);
            await context.Database.MigrateAsync();

            ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
            ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
            ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(
                ExactPass1DocumentKind.Consumer,
                Commit(profile, occurrence.ConsumerAlias));
            ExactPass1DocumentFixture provider = fixture.CreatePass1Document(
                ExactPass1DocumentKind.Provider,
                Commit(profile, occurrence.ProviderAlias));
            await SeedPass1Async(context, fixture, consumer, provider);

            ExactResolutionBatchOutcome outcome = await new ExactCrossRepositoryResolutionStore(
                context).ApplyAsync(
                await BuildBatchAsync(profile, occurrence, consumer, provider),
                new RepositoryAccessScope(
                    [consumer.DocumentVersion.RepositoryId, provider.DocumentVersion.RepositoryId]));

            Assert.Equal(ExactResolutionOutcomeKind.Applied, outcome.Kind);
            CrossRepositoryResolutionDecision decision =
                await context.CrossRepositoryResolutionDecisions.SingleAsync();
            Assert.Equal(consumer.DocumentVersion.Id, decision.ConsumerDocumentVersionId);
            Assert.Equal(provider.DocumentVersion.Id, decision.ProviderDocumentVersionId);
            Assert.NotEqual(Guid.Empty, decision.DocumentEdgeId);
            Assert.Single(await context.ExactResolutionProviderEvidenceContributions.ToArrayAsync());
            Assert.Single(await context.DocumentEdges.ToArrayAsync());
            Uncertainty legacyUncertainty = await context.Uncertainties.SingleAsync();
            Assert.Equal(consumer.DocumentVersion.Id, legacyUncertainty.DocumentVersionId);
            Assert.Equal(consumer.SourceAnchor.Id, legacyUncertainty.SourceAnchorId);
            Assert.Equal("unsupported_communication", legacyUncertainty.Type);
            Assert.Equal("unresolved", legacyUncertainty.Status);
            Assert.Equal("unsupported_kind", legacyUncertainty.ReasonCode);
            Assert.Equal("file_storage", legacyUncertainty.ObservationKind);
            Assert.Null(legacyUncertainty.ResolvedAt);
            Assert.Null(legacyUncertainty.ResolvedByDocumentVersionId);
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertDatabaseDroppedAsync(administratorConnectionString, databaseName);
    }

    [Fact]
    public async Task ApplyAsync_ExactReplayAfterEvidenceDriftConflictsWithoutAdditionalV2WritesAsync()
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            output.WriteLine(
                "Qualification not executed: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
            return;
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        string databaseName = database.DatabaseName;
        output.WriteLine("Owned disposable PostgreSQL database: {0}", databaseName);
        try
        {
            await using ExactCrossRepositoryTestFixture fixture =
                await ExactCrossRepositoryTestFixture.CreateAsync();
            await using VulgataDbContext context = CreateContext(database.ConnectionString);
            await context.Database.MigrateAsync();

            ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
            ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
            ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(
                ExactPass1DocumentKind.Consumer,
                Commit(profile, occurrence.ConsumerAlias));
            ExactPass1DocumentFixture provider = fixture.CreatePass1Document(
                ExactPass1DocumentKind.Provider,
                Commit(profile, occurrence.ProviderAlias));
            await SeedPass1Async(context, fixture, consumer, provider);
            ExactResolutionBatchOutcome first = await new ExactCrossRepositoryResolutionStore(
                context).ApplyAsync(
                await BuildBatchAsync(profile, occurrence, consumer, provider),
                new RepositoryAccessScope(
                    [consumer.DocumentVersion.RepositoryId, provider.DocumentVersion.RepositoryId]));
            Assert.Equal(ExactResolutionOutcomeKind.Applied, first.Kind);

            const string driftedEvidenceLocator = "drifted.java#line=45";
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"vulgata\".\"ExactCrossRepositoryEdgeEvidence\" SET \"ConsumerEvidenceLocator\" = {driftedEvidenceLocator};");

            ExactResolutionBatchOutcome replay = await new ExactCrossRepositoryResolutionStore(
                context).ApplyAsync(
                await BuildBatchAsync(profile, occurrence, consumer, provider),
                new RepositoryAccessScope(
                    [consumer.DocumentVersion.RepositoryId, provider.DocumentVersion.RepositoryId]));

            Assert.Equal(ExactResolutionOutcomeKind.Conflict, replay.Kind);
            Assert.Single(await context.CrossRepositoryResolutionDecisions.ToArrayAsync());
            Assert.Single(await context.ExactResolutionProviderEvidenceContributions.ToArrayAsync());
            Assert.Single(await context.DocumentEdges.ToArrayAsync());
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertDatabaseDroppedAsync(administratorConnectionString, databaseName);
    }

    private static VulgataDbContext CreateContext(string connectionString)
    {
        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                "public"))
            .Options;
        return new VulgataDbContext(options);
    }

    private static async Task SeedPass1Async(
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
        await context.SaveChangesAsync();
        await context.LogicalDocuments
            .Where(document => document.Id == consumer.LogicalDocument.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                document => document.CurrentVersionId,
                consumer.DocumentVersion.Id));
        await context.LogicalDocuments
            .Where(document => document.Id == provider.LogicalDocument.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                document => document.CurrentVersionId,
                provider.DocumentVersion.Id));
        context.ChangeTracker.Clear();
    }

    private static Task<ExactResolutionDecisionBatch> BuildBatchAsync(
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

    private static ExactResolutionOwner Owner(ExactPass1DocumentFixture document) =>
        new(
            document.DocumentVersion.RepositoryId,
            document.DocumentVersion.ScanRunId,
            document.DocumentVersion.Id,
            document.SourceAnchor.Id,
            document.DocumentVersion.ProfiledRevision);

    private static string Commit(ExactResolutionProfile profile, string alias) =>
        Assert.Single(profile.Repositories, value => value.Alias == alias).ExpectedCommit;

    private static async Task AssertDatabaseDroppedAsync(
        string administratorConnectionString,
        string databaseName)
    {
        await using NpgsqlConnection connection = new(administratorConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            connection);
        command.Parameters.AddWithValue("name", databaseName);
        Assert.False(Assert.IsType<bool>(await command.ExecuteScalarAsync()));
    }
}
