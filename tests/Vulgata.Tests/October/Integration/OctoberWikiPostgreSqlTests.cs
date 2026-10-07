using Microsoft.EntityFrameworkCore;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Tests.Migrations;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Xunit.Abstractions;

namespace Vulgata.Tests.October.Integration;

public sealed class OctoberWikiPostgreSqlTests(ITestOutputHelper output)
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task OwnedDatabase_AcceptFreezePublishReadThroughExplore_ProducesEphemeralPublicationHandoffAsync()
    {
        await using var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
        TestResourceScope.ValidateOwnedTarget(database.ConnectionString);
        var options = new DbContextOptionsBuilder<OctoberDbContext>().UseNpgsql(database.ConnectionString,
            postgres => postgres.MigrationsHistoryTable("__OctoberMigrationsHistory", "october")).Options;
        await using (var context = new OctoberDbContext(options)) await context.Database.MigrateAsync();
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options);
        var publication = await OctoberWikiPublishedReaderTests.VerifyAsync(fixture);
        output.WriteLine("Controlled semantic data; real PostgreSQL acceptance/freeze/publication/Explore read; no real Scan/provider evidence.");
        output.WriteLine("Ephemeral runner-owned database (deleted after test): {0}", database.DatabaseName);
        output.WriteLine("Batch: {0}; Publication: {1}; Repository: {2}", publication.Knowledge.BatchId, publication.PublicationId, fixture.Origin.RepositoryId);
        await using var verification = fixture.Factory.CreateDbContext();
        foreach (var document in await verification.DocumentVersions.ToListAsync())
            output.WriteLine("Exact document version: {0}", document.VersionId);
        Assert.Single(await verification.KnowledgePublications.ToListAsync());
    }
}
