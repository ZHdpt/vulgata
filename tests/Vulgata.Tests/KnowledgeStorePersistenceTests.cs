using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class KnowledgeStorePersistenceTests
{
    private static readonly Guid _anchorId =
        Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid _targetAnchorId =
        Guid.Parse("80000000-0000-0000-0000-000000000002");
    private static readonly Guid _targetLogicalDocumentId =
        Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid _targetCodeUnitId =
        Guid.Parse("40000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task CommitAsync_ValidCodeAndBusinessLogic_PersistsCompleteOwnedAggregates()
    {
        var capture = new ParameterCaptureInterceptor();
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync(capture);
        await SeedRepositoryEvidenceAsync(session.Context);
        var repository = CreateRepository(session.Context);

        KnowledgeCommitResult codeResult = await repository.CommitAsync(
            CreateCodeCommit(
                _targetLogicalDocumentId,
                KnowledgeStoreTestFixtures.TargetDocumentVersionId,
                KnowledgeStoreTestFixtures.TargetRepositoryId,
                KnowledgeStoreTestFixtures.TargetScanRunId,
                _targetCodeUnitId),
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.TargetRepositoryId]));

        KnowledgeDocumentMaterialization businessMaterialization = Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            "BusinessLogic");
        KnowledgeCommit businessCommit =
            KnowledgeStoreTestFixtures.CreateBusinessKnowledgeCommit(businessMaterialization);
        KnowledgeCommitResult businessResult = await repository.CommitAsync(
            businessCommit,
            new RepositoryAccessScope(
            [
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.TargetRepositoryId,
            ]));

        Assert.Equal(KnowledgeCommitStatus.Committed, codeResult.Status);
        Assert.Equal(KnowledgeCommitStatus.Committed, businessResult.Status);
        Assert.Equal(2, await session.Context.Set<LogicalDocument>().CountAsync());
        Assert.Equal(2, await session.Context.Set<DocumentVersion>().CountAsync());
        Assert.Equal(1, await session.Context.Set<SourceAnchor>().CountAsync());
        DocumentEdge edge = Assert.Single(await session.Context.Set<DocumentEdge>().ToListAsync());
        Assert.Equal(KnowledgeStoreTestFixtures.DocumentVersionId, edge.SourceDocumentVersionId);
        Assert.Equal(KnowledgeStoreTestFixtures.TargetDocumentVersionId, edge.TargetDocumentVersionId);
        Assert.Equal(DocumentEdgeType.GeneratedFrom, edge.EdgeType);
        FlowTrace trace = Assert.Single(await session.Context.Set<FlowTrace>().ToListAsync());
        FlowTraceStep[] steps = await session.Context.Set<FlowTraceStep>()
            .Where(step => step.FlowTraceId == trace.Id)
            .OrderBy(step => step.StepOrder)
            .ToArrayAsync();
        Assert.Equal([1, 2], steps.Select(step => step.StepOrder));
        Assert.Null(steps[0].EdgeId);
        Assert.Equal(edge.Id, steps[1].EdgeId);
        Assert.Equal(2, await session.Context.Set<IndexEntry>().CountAsync());
        Assert.Equal(4, await session.Context.Set<DocumentSection>().CountAsync());
        Assert.DoesNotContain(
            capture.Commands,
            command => command.CommandText.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CommitAsync_ExpectedCurrentVersion_InsertsImmutableSuccessorAndMovesCurrentPointer()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedRepositoryEvidenceAsync(session.Context);
        var repository = CreateRepository(session.Context);
        KnowledgeCommit initial = CreateCodeCommit(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.SourceCodeUnitId);
        KnowledgeCommitResult initialResult = await repository.CommitAsync(
            initial,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]));
        Assert.Equal(KnowledgeCommitStatus.Committed, initialResult.Status);

        DocumentVersion predecessorBefore = await session.Context.Set<DocumentVersion>()
            .AsNoTracking()
            .SingleAsync(version => version.Id == KnowledgeStoreTestFixtures.DocumentVersionId);
        Guid successorVersionId = Guid.Parse("60000000-0000-0000-0000-000000000011");
        Guid successorAnchorId = Guid.Parse("80000000-0000-0000-0000-000000000011");
        KnowledgeDocumentMaterialization successorMaterialization = Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            successorVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            "CodeLogic",
            successorAnchorId);
        KnowledgeCommit successor = new(
            Guid.Parse("90000000-0000-0000-0000-000000000011"),
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            successorVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.ProfiledRevision,
            successorMaterialization.Content,
            [
                new SourceAnchorDraft(
                    successorAnchorId,
                    successorVersionId,
                    KnowledgeStoreTestFixtures.SourceRepositoryId,
                    KnowledgeStoreTestFixtures.SourceScanRunId,
                    KnowledgeStoreTestFixtures.SourceCodeUnitId,
                    KnowledgeStoreTestFixtures.ProfiledRevision,
                    "src/orders.cs",
                    "OrderService:1-10",
                    1,
                    10,
                    KnowledgeStoreTestFixtures.SourceHash),
            ],
            [],
            [],
            KnowledgeStoreTestFixtures.CreatedAt.AddMinutes(1));

        KnowledgeCommitResult result = await repository.CommitAsync(
            successor,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]));

        Assert.Equal(KnowledgeCommitStatus.Committed, result.Status);
        LogicalDocument logicalDocument = await session.Context.Set<LogicalDocument>()
            .AsNoTracking()
            .SingleAsync(document => document.Id == KnowledgeStoreTestFixtures.LogicalDocumentId);
        Assert.Equal(successorVersionId, logicalDocument.CurrentVersionId);
        DocumentVersion[] versions = await session.Context.Set<DocumentVersion>()
            .AsNoTracking()
            .OrderBy(version => version.VersionNumber)
            .ToArrayAsync();
        Assert.Equal(2, versions.Length);
        Assert.Equal(successorVersionId, versions[0].SupersededById);
        Assert.Equal(2, versions[1].VersionNumber);
        Assert.Null(versions[1].SupersededById);
        Assert.Equal(predecessorBefore.ContentJson, versions[0].ContentJson);
        Assert.Equal(predecessorBefore.ContentMarkdown, versions[0].ContentMarkdown);
        Assert.Equal(predecessorBefore.ContentFingerprint, versions[0].ContentFingerprint);
        Assert.Equal(2, await session.Context.Set<SourceAnchor>().CountAsync());
        Assert.Equal(2, await session.Context.Set<IndexEntry>().CountAsync());
        Assert.Equal(4, await session.Context.Set<DocumentSection>().CountAsync());
    }

    [Fact]
    public async Task RebuildProjectionAsync_ExistingVersion_ReplacesOnlyDerivedRowsRepeatably()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedRepositoryEvidenceAsync(session.Context);
        var repository = CreateRepository(session.Context);
        await CommitCodeAsync(
            repository,
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.SourceCodeUnitId);
        await CommitCodeAsync(
            repository,
            _targetLogicalDocumentId,
            KnowledgeStoreTestFixtures.TargetDocumentVersionId,
            KnowledgeStoreTestFixtures.TargetRepositoryId,
            KnowledgeStoreTestFixtures.TargetScanRunId,
            _targetCodeUnitId);

        DocumentVersion canonicalBefore = await session.Context.Set<DocumentVersion>()
            .AsNoTracking()
            .SingleAsync(version => version.Id == KnowledgeStoreTestFixtures.DocumentVersionId);
        IndexEntry sourceIndexBefore = await session.Context.Set<IndexEntry>()
            .AsNoTracking()
            .SingleAsync(entry =>
                entry.DocumentVersionId == KnowledgeStoreTestFixtures.DocumentVersionId);
        DocumentSection[] sourceSectionsBefore = await session.Context.Set<DocumentSection>()
            .AsNoTracking()
            .Where(section =>
                section.DocumentVersionId == KnowledgeStoreTestFixtures.DocumentVersionId)
            .OrderBy(section => section.SectionOrder)
            .ToArrayAsync();
        IndexEntry targetIndexBefore = await session.Context.Set<IndexEntry>()
            .AsNoTracking()
            .SingleAsync(entry =>
                entry.DocumentVersionId == KnowledgeStoreTestFixtures.TargetDocumentVersionId);
        await session.Context.Set<DocumentSection>()
            .Where(section =>
                section.DocumentVersionId == KnowledgeStoreTestFixtures.DocumentVersionId)
            .ExecuteDeleteAsync();
        await session.Context.Set<IndexEntry>()
            .Where(entry =>
                entry.DocumentVersionId == KnowledgeStoreTestFixtures.DocumentVersionId)
            .ExecuteDeleteAsync();
        session.Context.ChangeTracker.Clear();

        RepositoryAccessScope scope =
            new([KnowledgeStoreTestFixtures.SourceRepositoryId]);
        RebuildProjectionStatus first =
            await repository.RebuildProjectionAsync(
                KnowledgeStoreTestFixtures.DocumentVersionId,
                scope);
        RebuildProjectionStatus second =
            await repository.RebuildProjectionAsync(
                KnowledgeStoreTestFixtures.DocumentVersionId,
                scope);

        Assert.Equal(RebuildProjectionStatus.Rebuilt, first);
        Assert.Equal(RebuildProjectionStatus.Rebuilt, second);
        IndexEntry rebuiltIndex = await session.Context.Set<IndexEntry>()
            .AsNoTracking()
            .SingleAsync(entry =>
                entry.DocumentVersionId == KnowledgeStoreTestFixtures.DocumentVersionId);
        DocumentSection[] rebuiltSections = await session.Context.Set<DocumentSection>()
            .AsNoTracking()
            .Where(section =>
                section.DocumentVersionId == KnowledgeStoreTestFixtures.DocumentVersionId)
            .OrderBy(section => section.SectionOrder)
            .ToArrayAsync();
        IndexEntry targetIndexAfter = await session.Context.Set<IndexEntry>()
            .AsNoTracking()
            .SingleAsync(entry =>
                entry.DocumentVersionId == KnowledgeStoreTestFixtures.TargetDocumentVersionId);
        DocumentVersion canonicalAfter = await session.Context.Set<DocumentVersion>()
            .AsNoTracking()
            .SingleAsync(version => version.Id == KnowledgeStoreTestFixtures.DocumentVersionId);
        Assert.Equal(sourceIndexBefore.Id, rebuiltIndex.Id);
        Assert.Equal(sourceIndexBefore.SearchText, rebuiltIndex.SearchText);
        Assert.Equal(
            sourceSectionsBefore.Select(section => section.Id),
            rebuiltSections.Select(section => section.Id));
        Assert.Equal(
            sourceSectionsBefore.Select(section => section.ContentMarkdown),
            rebuiltSections.Select(section => section.ContentMarkdown));
        Assert.Equal(targetIndexBefore.Id, targetIndexAfter.Id);
        Assert.Equal(targetIndexBefore.SearchText, targetIndexAfter.SearchText);
        Assert.Equal(canonicalBefore.ContentJson, canonicalAfter.ContentJson);
        Assert.Equal(canonicalBefore.ContentMarkdown, canonicalAfter.ContentMarkdown);
        Assert.Equal(canonicalBefore.ContentFingerprint, canonicalAfter.ContentFingerprint);
        Assert.Equal(2, await session.Context.Set<IndexEntry>().CountAsync());
        Assert.Equal(4, await session.Context.Set<DocumentSection>().CountAsync());
    }

    [Fact]
    public async Task CommitAsync_StaleExpectedCurrentVersion_ReturnsConflictWithoutLosingRows()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedRepositoryEvidenceAsync(session.Context);
        var repository = CreateRepository(session.Context);
        KnowledgeCommit initial = CreateCodeCommit(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.SourceCodeUnitId);
        Assert.Equal(
            KnowledgeCommitStatus.Committed,
            (await repository.CommitAsync(
                initial,
                new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]))).Status);
        Guid winningVersionId = Guid.Parse("60000000-0000-0000-0000-000000000021");
        KnowledgeCommit winner = CreateSuccessorCodeCommit(
            winningVersionId,
            Guid.Parse("80000000-0000-0000-0000-000000000021"),
            Guid.Parse("90000000-0000-0000-0000-000000000021"));
        Assert.Equal(
            KnowledgeCommitStatus.Committed,
            (await repository.CommitAsync(
                winner,
                new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]))).Status);
        Guid losingVersionId = Guid.Parse("60000000-0000-0000-0000-000000000022");
        Guid losingAnchorId = Guid.Parse("80000000-0000-0000-0000-000000000022");
        KnowledgeCommit loser = CreateSuccessorCodeCommit(
            losingVersionId,
            losingAnchorId,
            Guid.Parse("90000000-0000-0000-0000-000000000022"));

        KnowledgeCommitResult result = await repository.CommitAsync(
            loser,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]));

        Assert.Equal(KnowledgeCommitStatus.Conflict, result.Status);
        Assert.Null(result.RejectionCode);
        Assert.Equal(2, await session.Context.Set<DocumentVersion>().CountAsync());
        Assert.False(await session.Context.Set<DocumentVersion>()
            .AnyAsync(version => version.Id == losingVersionId));
        Assert.False(await session.Context.Set<SourceAnchor>()
            .AnyAsync(anchor => anchor.Id == losingAnchorId));
        Assert.False(await session.Context.Set<IndexEntry>()
            .AnyAsync(entry => entry.DocumentVersionId == losingVersionId));
        Assert.False(await session.Context.Set<DocumentSection>()
            .AnyAsync(section => section.DocumentVersionId == losingVersionId));
        LogicalDocument logicalDocument = await session.Context.Set<LogicalDocument>()
            .AsNoTracking()
            .SingleAsync(document => document.Id == KnowledgeStoreTestFixtures.LogicalDocumentId);
        Assert.Equal(winningVersionId, logicalDocument.CurrentVersionId);
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Fact]
    public async Task CommitAsync_BusinessSuccessor_PreservesHistoricalEdgeAndFlowVersionEndpoints()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedRepositoryEvidenceAsync(session.Context);
        var repository = CreateRepository(session.Context);
        await CommitCodeAsync(
            repository,
            _targetLogicalDocumentId,
            KnowledgeStoreTestFixtures.TargetDocumentVersionId,
            KnowledgeStoreTestFixtures.TargetRepositoryId,
            KnowledgeStoreTestFixtures.TargetScanRunId,
            _targetCodeUnitId);
        KnowledgeDocumentMaterialization initialMaterialization = Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            "BusinessLogic");
        KnowledgeCommit initial =
            KnowledgeStoreTestFixtures.CreateBusinessKnowledgeCommit(initialMaterialization);
        RepositoryAccessScope scope = new(
        [
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.TargetRepositoryId,
        ]);
        Assert.Equal(
            KnowledgeCommitStatus.Committed,
            (await repository.CommitAsync(initial, scope)).Status);
        DocumentVersion initialVersionBefore = await session.Context.Set<DocumentVersion>()
            .AsNoTracking()
            .SingleAsync(version => version.Id == KnowledgeStoreTestFixtures.DocumentVersionId);
        Guid successorVersionId = Guid.Parse("60000000-0000-0000-0000-000000000051");
        Guid successorEdgeId = Guid.Parse("a0000000-0000-0000-0000-000000000051");
        KnowledgeDocumentMaterialization successorMaterialization = Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            successorVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            "BusinessLogic");
        KnowledgeCommit successor = new(
            Guid.Parse("e0000000-0000-0000-0000-000000000051"),
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            successorVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.ProfiledRevision,
            successorMaterialization.Content,
            [],
            [
                new EdgeDraft(
                    successorEdgeId,
                    successorVersionId,
                    KnowledgeStoreTestFixtures.SourceRepositoryId,
                    KnowledgeStoreTestFixtures.SourceScanRunId,
                    KnowledgeStoreTestFixtures.TargetDocumentVersionId,
                    KnowledgeStoreTestFixtures.TargetRepositoryId,
                    KnowledgeStoreTestFixtures.TargetScanRunId,
                    DocumentEdgeType.GeneratedFrom),
            ],
            [
                new FlowGroupDraft(
                    Guid.Parse("b0000000-0000-0000-0000-000000000051"),
                    1,
                    "订单流更新",
                    [
                        new FlowTraceDraft(
                            Guid.Parse("c0000000-0000-0000-0000-000000000051"),
                            1,
                            [
                                new FlowTraceStepDraft(
                                    Guid.Parse("d0000000-0000-0000-0000-000000000051"),
                                    1,
                                    successorVersionId,
                                    KnowledgeStoreTestFixtures.SourceRepositoryId,
                                    KnowledgeStoreTestFixtures.SourceScanRunId,
                                    null,
                                    FlowTraceStepKind.Document,
                                    "更新业务逻辑"),
                                new FlowTraceStepDraft(
                                    Guid.Parse("d0000000-0000-0000-0000-000000000052"),
                                    2,
                                    KnowledgeStoreTestFixtures.TargetDocumentVersionId,
                                    KnowledgeStoreTestFixtures.TargetRepositoryId,
                                    KnowledgeStoreTestFixtures.TargetScanRunId,
                                    successorEdgeId,
                                    FlowTraceStepKind.Relationship,
                                    "原代码证据"),
                            ]),
                    ]),
            ],
            KnowledgeStoreTestFixtures.CreatedAt.AddMinutes(1));

        Assert.Equal(
            KnowledgeCommitStatus.Committed,
            (await repository.CommitAsync(successor, scope)).Status);

        Assert.Equal(2, await session.Context.Set<DocumentEdge>().CountAsync());
        DocumentEdge historicalEdge = await session.Context.Set<DocumentEdge>()
            .AsNoTracking()
            .SingleAsync(edge =>
                edge.Id == Guid.Parse("a0000000-0000-0000-0000-000000000001"));
        DocumentEdge successorEdge = await session.Context.Set<DocumentEdge>()
            .AsNoTracking()
            .SingleAsync(edge => edge.Id == successorEdgeId);
        Assert.Equal(
            (KnowledgeStoreTestFixtures.DocumentVersionId,
                KnowledgeStoreTestFixtures.TargetDocumentVersionId),
            (historicalEdge.SourceDocumentVersionId, historicalEdge.TargetDocumentVersionId));
        Assert.Equal(
            (successorVersionId, KnowledgeStoreTestFixtures.TargetDocumentVersionId),
            (successorEdge.SourceDocumentVersionId, successorEdge.TargetDocumentVersionId));
        FlowTraceStep[] historicalSteps = await session.Context.Set<FlowTraceStep>()
            .AsNoTracking()
            .Where(step =>
                step.FlowTraceId == Guid.Parse("c0000000-0000-0000-0000-000000000001"))
            .OrderBy(step => step.StepOrder)
            .ToArrayAsync();
        Assert.Equal(
            [
                KnowledgeStoreTestFixtures.DocumentVersionId,
                KnowledgeStoreTestFixtures.TargetDocumentVersionId,
            ],
            historicalSteps.Select(step => step.DocumentVersionId));
        Assert.Equal(historicalEdge.Id, historicalSteps[1].EdgeId);
        DocumentVersion predecessorAfter = await session.Context.Set<DocumentVersion>()
            .AsNoTracking()
            .SingleAsync(version => version.Id == KnowledgeStoreTestFixtures.DocumentVersionId);
        Assert.Equal(initialVersionBefore.ContentJson, predecessorAfter.ContentJson);
        Assert.Equal(initialVersionBefore.ContentMarkdown, predecessorAfter.ContentMarkdown);
        Assert.Equal(initialVersionBefore.ContentFingerprint, predecessorAfter.ContentFingerprint);
        Assert.Equal(successorVersionId, predecessorAfter.SupersededById);
    }

    [Fact]
    public async Task RebuildProjectionAsync_SaveFails_RollsBackCompleteProjectionAndClearsTracker()
    {
        var failure = new SaveChangesFailureInterceptor(5);
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync(failure);
        await SeedRepositoryEvidenceAsync(session.Context);
        var repository = CreateRepository(session.Context);
        await CommitCodeAsync(
            repository,
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.SourceCodeUnitId);
        IndexEntry indexBefore = await session.Context.Set<IndexEntry>()
            .AsNoTracking()
            .SingleAsync();
        DocumentSection[] sectionsBefore = await session.Context.Set<DocumentSection>()
            .AsNoTracking()
            .OrderBy(section => section.SectionOrder)
            .ToArrayAsync();

        RebuildProjectionStatus status = await repository.RebuildProjectionAsync(
            KnowledgeStoreTestFixtures.DocumentVersionId,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]));

        Assert.Equal(RebuildProjectionStatus.Rejected, status);
        IndexEntry indexAfter = await session.Context.Set<IndexEntry>()
            .AsNoTracking()
            .SingleAsync();
        DocumentSection[] sectionsAfter = await session.Context.Set<DocumentSection>()
            .AsNoTracking()
            .OrderBy(section => section.SectionOrder)
            .ToArrayAsync();
        Assert.Equal(indexBefore.Id, indexAfter.Id);
        Assert.Equal(indexBefore.SearchText, indexAfter.SearchText);
        Assert.Equal(
            sectionsBefore.Select(section => section.Id),
            sectionsAfter.Select(section => section.Id));
        Assert.Equal(
            sectionsBefore.Select(section => section.ContentMarkdown),
            sectionsAfter.Select(section => section.ContentMarkdown));
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Fact]
    public async Task CommitAsync_InvalidAnchorOwnership_RejectsWithoutPartialRows()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedRepositoryEvidenceAsync(session.Context);
        var repository = CreateRepository(session.Context);
        KnowledgeCommit valid = CreateCodeCommit(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.SourceCodeUnitId);
        SourceAnchorDraft invalidAnchor = valid.SourceAnchors[0] with
        {
            RepositoryId = KnowledgeStoreTestFixtures.TargetRepositoryId,
        };

        KnowledgeCommitResult result = await repository.CommitAsync(
            valid with { SourceAnchors = [invalidAnchor] },
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]));

        Assert.Equal(KnowledgeCommitStatus.Rejected, result.Status);
        Assert.Equal(KnowledgeRejectionCode.InvalidAnchor, result.RejectionCode);
        await AssertNoKnowledgeRowsAsync(session.Context);
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Fact]
    public async Task CommitAsync_UnauthorizedFlowDocumentEndpoint_RejectsBeforePersistence()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedRepositoryEvidenceAsync(session.Context);
        var repository = CreateRepository(session.Context);
        KnowledgeDocumentMaterialization materialization = Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            "BusinessLogic");
        KnowledgeCommit commit = KnowledgeStoreTestFixtures.CreateBusinessKnowledgeCommit(
            materialization,
            edges: [],
            flowGroups:
            [
                new(
                    Guid.Parse("b0000000-0000-0000-0000-000000000010"),
                    1,
                    "跨仓库流程",
                    [
                        new(
                            Guid.Parse("c0000000-0000-0000-0000-000000000010"),
                            1,
                            [
                                new(
                                    Guid.Parse("d0000000-0000-0000-0000-000000000010"),
                                    1,
                                    KnowledgeStoreTestFixtures.TargetDocumentVersionId,
                                    KnowledgeStoreTestFixtures.TargetRepositoryId,
                                    KnowledgeStoreTestFixtures.TargetScanRunId,
                                    null,
                                    FlowTraceStepKind.Document,
                                    "受限目标"),
                            ]),
                    ]),
            ]);

        KnowledgeCommitResult result = await repository.CommitAsync(
            commit,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]));

        Assert.Equal(KnowledgeCommitStatus.Rejected, result.Status);
        Assert.Equal(KnowledgeRejectionCode.UnauthorizedEndpoint, result.RejectionCode);
        await AssertNoKnowledgeRowsAsync(session.Context);
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task CommitAsync_SaveBoundaryFails_RollsBackAndClearsTracker(int failureOrdinal)
    {
        var failure = new SaveChangesFailureInterceptor(failureOrdinal);
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync(failure);
        await SeedRepositoryEvidenceAsync(session.Context);
        var repository = CreateRepository(session.Context);

        KnowledgeCommitResult result = await repository.CommitAsync(
            CreateCodeCommit(
                KnowledgeStoreTestFixtures.LogicalDocumentId,
                KnowledgeStoreTestFixtures.DocumentVersionId,
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.SourceScanRunId,
                KnowledgeStoreTestFixtures.SourceCodeUnitId),
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]));

        Assert.Equal(KnowledgeCommitStatus.Rejected, result.Status);
        await AssertNoKnowledgeRowsAsync(session.Context);
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Fact]
    public async Task CompositeForeignKeys_CrossOwnerEdgeTuple_IsRejectedByDatabase()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedRepositoryEvidenceAsync(session.Context);
        var repository = CreateRepository(session.Context);
        KnowledgeCommitResult source = await repository.CommitAsync(
            CreateCodeCommit(
                KnowledgeStoreTestFixtures.LogicalDocumentId,
                KnowledgeStoreTestFixtures.DocumentVersionId,
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.SourceScanRunId,
                KnowledgeStoreTestFixtures.SourceCodeUnitId),
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]));
        Assert.Equal(KnowledgeCommitStatus.Committed, source.Status);

        session.Context.ChangeTracker.Clear();
        session.Context.Set<DocumentEdge>().Add(new DocumentEdge(
            Guid.Parse("a0000000-0000-0000-0000-000000000099"),
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.TargetRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            DocumentEdgeType.References,
            KnowledgeStoreTestFixtures.CreatedAt));

        await Assert.ThrowsAsync<DbUpdateException>(() => session.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task CompositeForeignKeys_CrossOwnerCurrentAndSuccessorPointers_AreRejectedByDatabase()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedRepositoryEvidenceAsync(session.Context);
        var repository = CreateRepository(session.Context);
        await CommitCodeAsync(
            repository,
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.SourceCodeUnitId);
        await CommitCodeAsync(
            repository,
            _targetLogicalDocumentId,
            KnowledgeStoreTestFixtures.TargetDocumentVersionId,
            KnowledgeStoreTestFixtures.TargetRepositoryId,
            KnowledgeStoreTestFixtures.TargetScanRunId,
            _targetCodeUnitId);

        session.Context.ChangeTracker.Clear();
        LogicalDocument sourceDocument = await session.Context.Set<LogicalDocument>()
            .SingleAsync(document =>
                document.Id == KnowledgeStoreTestFixtures.LogicalDocumentId);
        session.Context.Entry(sourceDocument)
            .Property(document => document.CurrentVersionId)
            .CurrentValue = KnowledgeStoreTestFixtures.TargetDocumentVersionId;
        await Assert.ThrowsAsync<DbUpdateException>(() => session.Context.SaveChangesAsync());

        session.Context.ChangeTracker.Clear();
        DocumentVersion sourceVersion = await session.Context.Set<DocumentVersion>()
            .SingleAsync(version =>
                version.Id == KnowledgeStoreTestFixtures.DocumentVersionId);
        session.Context.Entry(sourceVersion)
            .Property(version => version.SupersededById)
            .CurrentValue = KnowledgeStoreTestFixtures.TargetDocumentVersionId;
        await Assert.ThrowsAsync<DbUpdateException>(() => session.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task CompositeForeignKeys_CrossOwnerAnchorTuple_IsRejectedByDatabase()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedRepositoryEvidenceAsync(session.Context);
        var repository = CreateRepository(session.Context);
        await CommitCodeAsync(
            repository,
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.SourceCodeUnitId);

        session.Context.ChangeTracker.Clear();
        session.Context.Set<SourceAnchor>().Add(new SourceAnchor(
            Guid.Parse("80000000-0000-0000-0000-000000000099"),
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.TargetRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.SourceCodeUnitId,
            KnowledgeStoreTestFixtures.ProfiledRevision,
            "src/orders.cs",
            "OrderService:1-10",
            1,
            10,
            KnowledgeStoreTestFixtures.SourceHash,
            KnowledgeStoreTestFixtures.CreatedAt));

        await Assert.ThrowsAsync<DbUpdateException>(() => session.Context.SaveChangesAsync());
    }

    [Fact]
    public void Model_OwnerSafeRelationshipsAndRestrictiveDeletes_AreConfigured()
    {
        using var context = CreateModelOnlyContext();

        AssertCompositeForeignKey<DocumentEdge>(
            context,
            [nameof(DocumentEdge.SourceDocumentVersionId), nameof(DocumentEdge.SourceRepositoryId), nameof(DocumentEdge.SourceScanRunId)]);
        AssertCompositeForeignKey<DocumentEdge>(
            context,
            [nameof(DocumentEdge.TargetDocumentVersionId), nameof(DocumentEdge.TargetRepositoryId), nameof(DocumentEdge.TargetScanRunId)]);
        AssertCompositeForeignKey<SourceAnchor>(
            context,
            [nameof(SourceAnchor.DocumentVersionId), nameof(SourceAnchor.RepositoryId), nameof(SourceAnchor.ScanRunId)]);
        AssertCompositeForeignKey<SourceAnchor>(
            context,
            [nameof(SourceAnchor.CodeUnitId), nameof(SourceAnchor.ScanRunId), nameof(SourceAnchor.RepositoryId)]);
        AssertCompositeForeignKey<LogicalDocument>(
            context,
            [nameof(LogicalDocument.CurrentVersionId), nameof(LogicalDocument.Id), nameof(LogicalDocument.RepositoryId)]);
        AssertCompositeForeignKey<DocumentVersion>(
            context,
            [nameof(DocumentVersion.SupersededById), nameof(DocumentVersion.LogicalDocumentId), nameof(DocumentVersion.RepositoryId)]);

        Assert.All(
            new[]
            {
                typeof(LogicalDocument),
                typeof(DocumentVersion),
                typeof(SourceAnchor),
                typeof(DocumentEdge),
                typeof(FlowGroup),
                typeof(FlowTrace),
                typeof(FlowTraceStep),
                typeof(IndexEntry),
                typeof(DocumentSection),
            }.SelectMany(type => context.Model.FindEntityType(type)!.GetForeignKeys()),
            foreignKey => Assert.NotEqual(DeleteBehavior.Cascade, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void Model_ClosedPersistedValuesAndVersionIntegrityChecks_AreConfigured()
    {
        using var context = CreateModelOnlyContext();
        IModel model = context.GetService<IDesignTimeModel>().Model;

        AssertCheckConstraint<DocumentVersion>(
            model,
            "CK_DocumentVersions_SupersededBy_NotSelf",
            "\"SupersededById\" IS NULL OR \"SupersededById\" <> \"Id\"");
        AssertCheckConstraint<DocumentVersion>(
            model,
            "CK_DocumentVersions_ContentFingerprint_Length",
            "length(\"ContentFingerprint\") = 64");
        AssertCheckConstraint<DocumentVersion>(
            model,
            "CK_DocumentVersions_SchemaIdentity_Closed",
            "\"SchemaIdentity\" = 'vulgata.knowledge-document/1.0'");
        AssertCheckConstraint<LogicalDocument>(
            model,
            "CK_LogicalDocuments_DocumentType_Closed",
            "\"DocumentType\" IN ('CodeLogic', 'BusinessLogic')");
        AssertCheckConstraint<DocumentEdge>(
            model,
            "CK_DocumentEdges_EdgeType_Closed",
            "\"EdgeType\" IN ('References', 'GeneratedFrom')");
        AssertCheckConstraint<FlowTraceStep>(
            model,
            "CK_FlowTraceSteps_StepKind_Closed",
            "\"StepKind\" IN ('Document', 'Relationship', 'Termination')");
        AssertCheckConstraint<DocumentSection>(
            model,
            "CK_DocumentSections_SectionKind_Closed",
            "\"SectionKind\" IN ('Overview', 'BusinessRule', 'ProcessStep', 'TechnicalDetail', 'Evidence', 'Other')");
    }

    private static KnowledgeStoreRepository CreateRepository(VulgataDbContext context) =>
        new(context, new KnowledgeDocumentMaterializer());

    private static KnowledgeCommit CreateCodeCommit(
        Guid logicalDocumentId,
        Guid documentVersionId,
        Guid repositoryId,
        Guid scanRunId,
        Guid codeUnitId)
    {
        Guid anchorId = repositoryId == KnowledgeStoreTestFixtures.TargetRepositoryId
            ? _targetAnchorId
            : _anchorId;
        KnowledgeDocumentMaterialization materialization = Materialize(
            logicalDocumentId,
            documentVersionId,
            repositoryId,
            "CodeLogic",
            anchorId);
        var anchor = new SourceAnchorDraft(
            anchorId,
            documentVersionId,
            repositoryId,
            scanRunId,
            codeUnitId,
            KnowledgeStoreTestFixtures.ProfiledRevision,
            "src/orders.cs",
            "OrderService:1-10",
            1,
            10,
            KnowledgeStoreTestFixtures.SourceHash);
        return new(
            Guid.NewGuid(),
            logicalDocumentId,
            null,
            documentVersionId,
            repositoryId,
            scanRunId,
            KnowledgeStoreTestFixtures.ProfiledRevision,
            materialization.Content,
            [anchor],
            [],
            [],
            KnowledgeStoreTestFixtures.CreatedAt);
    }

    private static KnowledgeCommit CreateSuccessorCodeCommit(
        Guid documentVersionId,
        Guid anchorId,
        Guid operationId)
    {
        KnowledgeDocumentMaterialization materialization = Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            documentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            "CodeLogic",
            anchorId);
        return new(
            operationId,
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            documentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.ProfiledRevision,
            materialization.Content,
            [
                new SourceAnchorDraft(
                    anchorId,
                    documentVersionId,
                    KnowledgeStoreTestFixtures.SourceRepositoryId,
                    KnowledgeStoreTestFixtures.SourceScanRunId,
                    KnowledgeStoreTestFixtures.SourceCodeUnitId,
                    KnowledgeStoreTestFixtures.ProfiledRevision,
                    "src/orders.cs",
                    "OrderService:1-10",
                    1,
                    10,
                    KnowledgeStoreTestFixtures.SourceHash),
            ],
            [],
            [],
            KnowledgeStoreTestFixtures.CreatedAt.AddMinutes(1));
    }

    private static async Task CommitCodeAsync(
        KnowledgeStoreRepository repository,
        Guid logicalDocumentId,
        Guid documentVersionId,
        Guid repositoryId,
        Guid scanRunId,
        Guid codeUnitId)
    {
        KnowledgeCommitResult result = await repository.CommitAsync(
            CreateCodeCommit(
                logicalDocumentId,
                documentVersionId,
                repositoryId,
                scanRunId,
                codeUnitId),
            new RepositoryAccessScope([repositoryId]));
        Assert.Equal(KnowledgeCommitStatus.Committed, result.Status);
    }

    private static KnowledgeDocumentMaterialization Materialize(
        Guid logicalDocumentId,
        Guid documentVersionId,
        Guid repositoryId,
        string documentType,
        Guid? sourceAnchorId = null)
    {
        IReadOnlyList<Guid> anchorIds = sourceAnchorId is null ? [] : [sourceAnchorId.Value];
        string json = KnowledgeStoreTestFixtures.CreateCanonicalJson(
            documentType: documentType,
            sections:
            [
                new(
                    KnowledgeStoreTestFixtures.FirstSectionId,
                    1,
                    "Overview",
                    "概览",
                    "处理订单请求。",
                    "处理订单请求",
                    anchorIds,
                    []),
                new(
                    KnowledgeStoreTestFixtures.SecondSectionId,
                    2,
                    "TechnicalDetail",
                    "实现",
                    "调用 API。",
                    "API orders",
                    [],
                    []),
            ]);
        return new KnowledgeDocumentMaterializer().Materialize(
            logicalDocumentId,
            documentVersionId,
            repositoryId,
            null,
            json,
            KnowledgeStoreTestFixtures.CreatedAt);
    }

    private static async Task SeedRepositoryEvidenceAsync(VulgataDbContext context)
    {
        context.Repositories.AddRange(
            CreateStandaloneRepository(
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                "source"),
            CreateStandaloneRepository(
                KnowledgeStoreTestFixtures.TargetRepositoryId,
                "target"));
        context.ScanRuns.AddRange(
            KnowledgeStoreTestFixtures.CreateCompletedScanRun(
                KnowledgeStoreTestFixtures.SourceScanRunId,
                KnowledgeStoreTestFixtures.SourceRepositoryId),
            KnowledgeStoreTestFixtures.CreateCompletedScanRun(
                KnowledgeStoreTestFixtures.TargetScanRunId,
                KnowledgeStoreTestFixtures.TargetRepositoryId));
        context.CodeUnits.AddRange(
            KnowledgeStoreTestFixtures.CreateCodeUnit(
                KnowledgeStoreTestFixtures.SourceCodeUnitId,
                KnowledgeStoreTestFixtures.SourceScanRunId,
                KnowledgeStoreTestFixtures.SourceRepositoryId),
            KnowledgeStoreTestFixtures.CreateCodeUnit(
                _targetCodeUnitId,
                KnowledgeStoreTestFixtures.TargetScanRunId,
                KnowledgeStoreTestFixtures.TargetRepositoryId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static Repository CreateStandaloneRepository(
        Guid id,
        string name)
    {
        Repository repository =
            Repository.CreateStandalone(
                name,
                $"https://example.invalid/{name}.git",
                null,
                null,
                KnowledgeStoreTestFixtures.CreatedAt);
        typeof(Repository)
            .GetProperty(nameof(Repository.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(repository, id);
        return repository;
    }

    private static async Task AssertNoKnowledgeRowsAsync(VulgataDbContext context)
    {
        Assert.Equal(0, await context.Set<LogicalDocument>().CountAsync());
        Assert.Equal(0, await context.Set<DocumentVersion>().CountAsync());
        Assert.Equal(0, await context.Set<SourceAnchor>().CountAsync());
        Assert.Equal(0, await context.Set<DocumentEdge>().CountAsync());
        Assert.Equal(0, await context.Set<FlowGroup>().CountAsync());
        Assert.Equal(0, await context.Set<FlowTrace>().CountAsync());
        Assert.Equal(0, await context.Set<FlowTraceStep>().CountAsync());
        Assert.Equal(0, await context.Set<IndexEntry>().CountAsync());
        Assert.Equal(0, await context.Set<DocumentSection>().CountAsync());
    }

    private static VulgataDbContext CreateModelOnlyContext()
    {
        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new VulgataDbContext(options);
    }

    private static void AssertCompositeForeignKey<TEntity>(
        VulgataDbContext context,
        IReadOnlyList<string> propertyNames)
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);
        Assert.Contains(
            entityType.GetForeignKeys(),
            foreignKey =>
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual(propertyNames) &&
                foreignKey.DeleteBehavior is DeleteBehavior.Restrict or DeleteBehavior.NoAction);
    }

    private static void AssertCheckConstraint<TEntity>(
        IModel model,
        string name,
        string sql)
    {
        var entityType = model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);
        var constraint = Assert.Single(
            entityType.GetCheckConstraints(),
            candidate => StringComparer.Ordinal.Equals(candidate.Name, name));
        Assert.Equal(sql, constraint.Sql);
    }
}
