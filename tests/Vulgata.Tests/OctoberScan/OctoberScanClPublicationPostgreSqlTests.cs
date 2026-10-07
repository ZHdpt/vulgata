using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.Migrations;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Actual owned PostgreSQL Begin/accept/freeze/publication/read; controlled model and native discovery. No live provider claim.</summary>
public sealed class OctoberScanClPublicationPostgreSqlTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OneFilePipeline_ActualPostgreSqlPublishesExactReadableCl_OrWithholdsUnacknowledgedAcceptanceAsync(bool lostAcceptanceReply)
    {
        await using var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
        TestResourceScope.ValidateOwnedTarget(database.ConnectionString);
        var options = new DbContextOptionsBuilder<OctoberDbContext>().UseNpgsql(database.ConnectionString,
            postgres => postgres.MigrationsHistoryTable("__OctoberMigrationsHistory", "october").CommandTimeout(30)).Options;
        await using (var schema = new OctoberDbContext(options)) await schema.Database.MigrateAsync();
        var fixture = await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options);
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync(fixture);
        harness.LoseAcceptanceAcknowledgement = lostAcceptanceReply;
        var result = await harness.Runner.ExecuteAsync(harness.Execution);
        Assert.Equal(1, harness.BeginCalls);
        Assert.Equal(2, harness.Client.Calls);
        Assert.True(harness.Client.SawToolResult);
        var report = Assert.IsType<Vulgata.OctoberGenerator.OctoberScanReport>(result.Report);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal(1, await context.AcceptedFileManifests.CountAsync());
        if (lostAcceptanceReply)
        {
            // Real commit, controlled lost acknowledgement and unavailable exact lookup: no claim of an in-flight PG commit.
            Assert.Equal(4, report.ExitCode);
            Assert.Null(report.Publication);
            Assert.Empty(report.Documents);
            Assert.Equal(0, harness.FreezeCalls);
            Assert.Equal(0, await context.KnowledgePublications.CountAsync());
            return;
        }
        Assert.Equal(0, report.ExitCode);
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
        var publication = Assert.IsType<PublicationBinding>(report.Publication);
        var handle = Assert.Single(report.Documents);
        var reader = new OctoberKnowledgeReader(new OctoberKnowledgePublishedSource(fixture.Factory), publication,
            new B0bReaderDisclosure { CurrentSubject = "ordinary-reader" },
            new OctoberKnowledgeCursorProtector(new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider()), new B0bReadClock()), new B0bReadLog());
        var selection = await reader.OpenReadSelectionAsync(new(publication, new("ordinary-reader", "separate-authentication"), KnowledgeMode.IT, KnowledgePurpose.Reader, [fixture.Origin.RepositoryId]));
        Assert.Equal(KnowledgeReadStatus.Success, selection.Status);
        var page = await reader.ReadExactAsync(new(selection.Selection!, handle));
        Assert.Equal(KnowledgeReadStatus.Success, page.Status);
        Assert.Equal(handle, page.Document!.Handle);
        Assert.NotEmpty(page.Content!);
        var replay = await harness.Runner.ExecuteAsync(harness.Execution);
        Assert.Equal(publication, replay.Report!.Publication);
        Assert.Equal(1, harness.BeginCalls);
        Assert.Equal(2, harness.Client.Calls);
    }
}
