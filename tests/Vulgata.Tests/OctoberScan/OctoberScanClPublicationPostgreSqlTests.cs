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
    [SuiteFact("PostgreSql")]
    public async Task ExistingClBusiness_ConcurrentBeginSerializesAndSettledCompletionAllowsAnotherRunAsync()
    {
        await using var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
        TestResourceScope.ValidateOwnedTarget(database.ConnectionString);
        var options = new DbContextOptionsBuilder<OctoberDbContext>().UseNpgsql(database.ConnectionString,
            postgres => postgres.MigrationsHistoryTable("__OctoberMigrationsHistory", "october").CommandTimeout(30)).Options;
        await using (var schema = new OctoberDbContext(options)) await schema.Database.MigrateAsync();
        var fixture = await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options);
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync(fixture);

        var original = await harness.Runner.ExecuteAsync(harness.Execution);
        Assert.Equal(0, original.Report!.ExitCode);
        var publication = Assert.IsType<PublicationBinding>(original.Report.Publication);
        DocumentHandle[] originalHandles = original.Report.Documents.ToArray();
        await using var beforeContext = fixture.Factory.CreateDbContext();
        var originalMembers = await beforeContext.PublicationMembers.AsNoTracking()
            .Where(item => item.PublicationId == publication.PublicationId).OrderBy(item => item.Ordinal)
            .Select(item => new PgMemberSnapshot(item.BatchId, item.PublicationId, item.CorpusId,
                item.LogicalDocumentId, item.VersionId, item.Ordinal)).ToArrayAsync();

        BusinessFromClRequest firstRequest = Request(publication, harness.Execution.Session.SubjectId);
        BusinessFromClRequest competingRequest = Request(publication, harness.Execution.Session.SubjectId);
        var firstStore = Store(fixture);
        var competingStore = Store(fixture);
        Task<BusinessFromClResult> firstBegin = firstStore.BeginAsync(firstRequest);
        Task<BusinessFromClResult> competingBegin = competingStore.BeginAsync(competingRequest);
        BusinessFromClResult[] begins = await Task.WhenAll(firstBegin, competingBegin);

        int ownerIndex = Array.FindIndex(begins, result => result.Status == KnowledgeWriteStatus.Committed);
        Assert.True(ownerIndex >= 0);
        BusinessFromClResult ownerBegin = begins[ownerIndex];
        BusinessFromClResult blockedBegin = Assert.Single(begins.Where(result => result.Status != KnowledgeWriteStatus.Committed));
        Assert.Null(blockedBegin.Admission);
        var ownerStore = ownerIndex == 0 ? firstStore : competingStore;
        BusinessFromClRequest ownerRequest = ownerIndex == 0 ? firstRequest : competingRequest;
        var admission = Assert.IsAssignableFrom<BusinessFromClAdmission>(ownerBegin.Admission);
        var completionBase = new BusinessFromClCompletionRequest(ownerRequest, string.Empty);
        var completion = completionBase with
        { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeCompletionDigest(completionBase) };
        var completed = await ownerStore.CompleteAsync(completion, admission);
        Assert.Equal(KnowledgeWriteStatus.Committed, completed.Status);
        Assert.Null(completed.Publication);
        Assert.Null(completed.Corpus);

        var nextRequest = Request(publication, harness.Execution.Session.SubjectId);
        var nextBegin = await Store(fixture).BeginAsync(nextRequest);
        Assert.Equal(KnowledgeWriteStatus.Committed, nextBegin.Status);
        Assert.Equal(publication, Assert.IsType<BusinessFromClSnapshot>(nextBegin.Snapshot).Publication);
        Assert.NotNull(nextBegin.Admission);

        await using var afterContext = fixture.Factory.CreateDbContext();
        var finalMembers = await afterContext.PublicationMembers.AsNoTracking()
            .Where(item => item.PublicationId == publication.PublicationId).OrderBy(item => item.Ordinal)
            .Select(item => new PgMemberSnapshot(item.BatchId, item.PublicationId, item.CorpusId,
                item.LogicalDocumentId, item.VersionId, item.Ordinal)).ToArrayAsync();
        Assert.Equal(originalMembers, finalMembers);
        Assert.Equal(1, await afterContext.KnowledgePublications.CountAsync(item => item.PublicationId == publication.PublicationId));
        var reader = new OctoberKnowledgeReader(new OctoberKnowledgePublishedSource(fixture.Factory), publication,
            new B0bReaderDisclosure { CurrentSubject = "ordinary-reader" },
            new OctoberKnowledgeCursorProtector(new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider()), new B0bReadClock()), new B0bReadLog());
        var selection = await reader.OpenReadSelectionAsync(new(publication, new("ordinary-reader", "postgres-existing-cl-check"),
            KnowledgeMode.IT, KnowledgePurpose.Reader, [fixture.Origin.RepositoryId]));
        Assert.Equal(KnowledgeReadStatus.Success, selection.Status);
        foreach (DocumentHandle handle in originalHandles)
        {
            var page = await reader.ReadExactAsync(new(selection.Selection!, handle));
            Assert.Equal(KnowledgeReadStatus.Success, page.Status);
            Assert.Equal(handle, page.Document!.Handle);
            Assert.NotEmpty(page.Content!);
        }
    }

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

    private static OctoberBusinessFromClStore Store(OctoberKnowledgeAcceptanceFixture fixture) =>
        new(fixture.Factory, new AllowedBusinessFromClAuthority(), fixture.Guard, fixture.Guard,
            new SettledBusinessFromClAuthority(), fixture.Log);

    private static BusinessFromClRequest Request(PublicationBinding publication, string subject)
    {
        var unsigned = new BusinessFromClRequest(publication, subject, Guid.NewGuid(), string.Empty);
        return unsigned with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeBeginDigest(unsigned) };
    }

    private sealed record PgMemberSnapshot(Guid BatchId, Guid PublicationId, Guid CorpusId,
        Guid LogicalDocumentId, Guid VersionId, int Ordinal);

    private sealed class AllowedBusinessFromClAuthority : IOctoberBusinessFromClAuthority
    {
        public Task<DisclosureStatus> CheckAsync(BusinessFromClRequest request, System.Collections.Immutable.ImmutableArray<Contributor> contributors,
            CancellationToken cancellationToken = default) => Task.FromResult(DisclosureStatus.Allowed);
    }

    private sealed class SettledBusinessFromClAuthority : IOctoberBusinessFromClSettlementAuthority
    {
        public Task<BusinessFromClSettlement> VerifyAsync(BusinessFromClCompletionRequest request,
            BusinessFromClAdmission admission, CancellationToken cancellationToken = default) =>
            Task.FromResult(BusinessFromClSettlement.Settled);
    }
}
