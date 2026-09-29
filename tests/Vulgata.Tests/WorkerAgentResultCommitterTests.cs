using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class WorkerAgentResultCommitterTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Pass2Projection_RealCurrentKnowledgeMarkdown_AllowsNormalizedLfLayout()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        Pass2CurrentFixture currentFixture = await SeedPass2CurrentKnowledgeAsync(
            session.Context,
            knowledgeStore);

        Pass2BusinessLogicJobPayload payload = Pass2BusinessLogicJobPayload.Create(
            currentFixture.Flow,
            currentFixture.CurrentDocuments);

        Assert.All(
            payload.CodeLogicDocumentVersions,
            projection => Assert.Contains('\n', projection.ContentMarkdown));
        Assert.All(
            payload.CodeLogicDocumentVersions.SelectMany(projection => projection.Sections),
            section => Assert.Contains('\n', section.ContentMarkdown));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Pass2Projection_LayoutTextStillRejectsForbiddenControlsSecretsAndHostPaths()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        Pass2CurrentFixture currentFixture = await SeedPass2CurrentKnowledgeAsync(
            session.Context,
            knowledgeStore);

        foreach (string forbidden in new[]
        {
            "订单\r处理",
            "订单\0处理",
            "password=not-allowed",
            "/home/operator/source.cs",
        })
        {
            CurrentKnowledge changed = currentFixture.CurrentDocuments[0] with
            {
                ContentMarkdown = forbidden,
            };
            CurrentKnowledge[] documents =
                [changed, .. currentFixture.CurrentDocuments.Skip(1)];

            Assert.Throws<ArgumentException>(() =>
                Pass2BusinessLogicJobPayload.Create(currentFixture.Flow, documents));
        }

        CurrentKnowledge first = currentFixture.CurrentDocuments[0];
        CurrentDocumentSection changedSection = first.Sections[0] with
        {
            ContentMarkdown = "api_key=not-allowed",
        };
        CurrentKnowledge changedDocument = first with
        {
            Sections = [changedSection, .. first.Sections.Skip(1)],
        };
        CurrentKnowledge[] changedDocuments =
            [changedDocument, .. currentFixture.CurrentDocuments.Skip(1)];

        Assert.Throws<ArgumentException>(() =>
            Pass2BusinessLogicJobPayload.Create(currentFixture.Flow, changedDocuments));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommitAsync_ValidPass1Result_PersistsCompleteScopedAggregateAtomically()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        await SeedOwnershipAndReferenceAsync(session.Context, packet);
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        await SeedReferenceDocumentAsync(knowledgeStore, packet);

        WorkerAgentResultEnvelope result = CreateValidResult(packet);
        var committer = new WorkerAgentResultCommitter(knowledgeStore);
        RepositoryAccessScope scope = new([packet.RepositoryId]);

        WorkerAgentResultCommitOutcome outcome =
            await committer.CommitAsync(packet, result, scope, CancellationToken.None);

        Assert.Equal(WorkerAgentResultCommitOutcome.Committed, outcome);
        LogicalDocument source = await session.Context.Set<LogicalDocument>()
            .AsNoTracking()
            .SingleAsync(document => document.StableKey == "orders.calculate-total");
        DocumentVersion sourceVersion = await session.Context.Set<DocumentVersion>()
            .AsNoTracking()
            .SingleAsync(version => version.LogicalDocumentId == source.Id);
        SourceAnchor anchor = await session.Context.Set<SourceAnchor>()
            .AsNoTracking()
            .SingleAsync(value => value.DocumentVersionId == sourceVersion.Id);
        Assert.Equal(WorkerAgentTestFixtures.Id(11), anchor.Id);
        DocumentEdge edge = await session.Context.Set<DocumentEdge>()
            .AsNoTracking()
            .SingleAsync(value => value.SourceDocumentVersionId == sourceVersion.Id);
        Assert.Equal(DocumentEdgeType.References, edge.EdgeType);
        Assert.Equal(WorkerAgentTestFixtures.Id(9), edge.TargetDocumentVersionId);

        CurrentKnowledge current = Assert.IsType<CurrentKnowledge>(
            await knowledgeStore.GetCurrentAsync(
                source.Id,
                scope,
                [WorkerAgentTestFixtures.Id(8)]));
        CurrentDocumentSection evidence = Assert.Single(current.Sections);
        Assert.Equal(WorkerAgentTestFixtures.Id(8), evidence.StableSectionId);
        Assert.Equal(DocumentSectionKind.Evidence, evidence.SectionKind);
        Assert.Equal("HTTP", evidence.Heading);
        Assert.Equal("订单服务调用库存接口。", evidence.ContentMarkdown);
        Assert.Equal("订单服务调用库存接口。", evidence.SearchText);
        Assert.Equal([WorkerAgentTestFixtures.Id(11)], evidence.SourceAnchorIds);
        Assert.Contains("订单服务调用库存接口。", current.IndexEntry.SearchText);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommitAsync_IdenticalPass1Replay_IsIdempotentWithoutDuplicateTruth()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        await SeedOwnershipAndReferenceAsync(session.Context, packet);
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        await SeedReferenceDocumentAsync(knowledgeStore, packet);
        var committer = new WorkerAgentResultCommitter(knowledgeStore);
        WorkerAgentResultEnvelope result = CreateValidResult(packet);
        RepositoryAccessScope scope = new([packet.RepositoryId]);

        WorkerAgentResultCommitOutcome first =
            await committer.CommitAsync(packet, result, scope, CancellationToken.None);
        WorkerAgentResultCommitOutcome replay =
            await committer.CommitAsync(packet, result, scope, CancellationToken.None);

        Assert.Equal(WorkerAgentResultCommitOutcome.Committed, first);
        Assert.Equal(WorkerAgentResultCommitOutcome.Committed, replay);
        Assert.Equal(2, await session.Context.Set<LogicalDocument>().CountAsync());
        Assert.Equal(2, await session.Context.Set<DocumentVersion>().CountAsync());
        Assert.Equal(1, await session.Context.Set<SourceAnchor>().CountAsync());
        Assert.Equal(1, await session.Context.Set<DocumentEdge>().CountAsync());
        Assert.Equal(3, await session.Context.Set<DocumentSection>().CountAsync());
        Assert.Equal(2, await session.Context.Set<IndexEntry>().CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommitAsync_NewPass1VersionAndReplay_PreservesVersionChainIdempotently()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        WorkerAgentJobPacket firstPacket = WorkerAgentTestFixtures.CreatePass1Packet();
        await SeedOwnershipAndReferenceAsync(session.Context, firstPacket);
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        await SeedReferenceDocumentAsync(knowledgeStore, firstPacket);
        var committer = new WorkerAgentResultCommitter(knowledgeStore);
        RepositoryAccessScope scope = new([firstPacket.RepositoryId]);
        WorkerAgentResultEnvelope firstResult = CreateValidResult(firstPacket);

        Assert.Equal(
            WorkerAgentResultCommitOutcome.Committed,
            await committer.CommitAsync(firstPacket, firstResult, scope));
        DocumentVersion predecessorBefore = await session.Context.Set<DocumentVersion>()
            .AsNoTracking()
            .SingleAsync(version =>
                version.Title == "订单总额计算" &&
                version.RepositoryId == firstPacket.RepositoryId);

        WorkerAgentJobPacket successorPacket = WorkerAgentJobPacket.Create(
            jobId: WorkerAgentTestFixtures.Id(101),
            kind: firstPacket.Kind,
            scanExecutionId: firstPacket.ScanExecutionId,
            repositoryId: firstPacket.RepositoryId,
            systemId: firstPacket.SystemId,
            scanRunId: firstPacket.ScanRunId,
            profiledRevision: firstPacket.ProfiledRevision,
            schemaVersion: firstPacket.SchemaVersion,
            payload: firstPacket.Payload);
        WorkerAgentResultEnvelope successorResult = CreateValidResult(
            successorPacket,
            WorkerAgentTestFixtures.Id(12),
            "订单总额计算更新");

        WorkerAgentResultCommitOutcome successor =
            await committer.CommitAsync(successorPacket, successorResult, scope);
        WorkerAgentResultCommitOutcome replay =
            await committer.CommitAsync(successorPacket, successorResult, scope);

        Assert.Equal(WorkerAgentResultCommitOutcome.Committed, successor);
        Assert.Equal(WorkerAgentResultCommitOutcome.Committed, replay);
        LogicalDocument logicalDocument = await session.Context.Set<LogicalDocument>()
            .AsNoTracking()
            .SingleAsync(document => document.StableKey == "orders.calculate-total");
        DocumentVersion[] versions = await session.Context.Set<DocumentVersion>()
            .AsNoTracking()
            .Where(version => version.LogicalDocumentId == logicalDocument.Id)
            .OrderBy(version => version.VersionNumber)
            .ToArrayAsync();
        Assert.Equal(2, versions.Length);
        Assert.Equal(2, versions[1].VersionNumber);
        Assert.Equal(versions[1].Id, versions[0].SupersededById);
        Assert.Null(versions[1].SupersededById);
        Assert.Equal(versions[1].Id, logicalDocument.CurrentVersionId);
        Assert.Equal(predecessorBefore.ContentJson, versions[0].ContentJson);
        Assert.Equal(predecessorBefore.ContentMarkdown, versions[0].ContentMarkdown);
        Assert.Equal(predecessorBefore.ContentFingerprint, versions[0].ContentFingerprint);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommitAsync_ValidPass2Result_PersistsExactBusinessLogicAggregateAtomically()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        Pass2Fixture fixture = await CreatePass2FixtureAsync(session.Context, knowledgeStore);
        var recordingStore = new RecordingKnowledgeStore(knowledgeStore);
        var committer = new WorkerAgentResultCommitter(recordingStore, fixture.Flow);
        RepositoryAccessScope scope = new([fixture.Packet.RepositoryId]);

        WorkerAgentResultCommitOutcome outcome = await committer.CommitAsync(
            fixture.Packet,
            fixture.Result,
            scope,
            CancellationToken.None);

        Assert.True(
            outcome == WorkerAgentResultCommitOutcome.Committed,
            $"Commit outcome {outcome}; store rejection {recordingStore.LastResult?.RejectionCode}.");
        LogicalDocument document = await session.Context.Set<LogicalDocument>()
            .AsNoTracking()
            .SingleAsync(value => value.StableKey == "orders.price-and-reserve");
        DocumentVersion version = await session.Context.Set<DocumentVersion>()
            .AsNoTracking()
            .SingleAsync(value => value.LogicalDocumentId == document.Id);
        Assert.Equal(DocumentType.BusinessLogic, document.DocumentType);
        Assert.Equal(2, await session.Context.Set<SourceAnchor>()
            .CountAsync(value => value.DocumentVersionId == version.Id));
        DocumentEdge[] edges = await session.Context.Set<DocumentEdge>()
            .AsNoTracking()
            .Where(value => value.SourceDocumentVersionId == version.Id)
            .OrderBy(value => value.TargetDocumentVersionId)
            .ToArrayAsync();
        Assert.Equal(
            fixture.GeneratedFromDocumentVersionIds,
            edges.Select(edge => edge.TargetDocumentVersionId));
        Assert.All(edges, edge => Assert.Equal(DocumentEdgeType.GeneratedFrom, edge.EdgeType));
        Assert.DoesNotContain(edges, edge => edge.EdgeType == DocumentEdgeType.References);

        CurrentKnowledge current = Assert.IsType<CurrentKnowledge>(
            await knowledgeStore.GetCurrentAsync(
                document.Id,
                scope,
                fixture.SectionIds));
        Assert.Equal(DocumentType.BusinessLogic, current.DocumentType);
        Assert.Equal(fixture.SectionIds, current.Sections.Select(section => section.StableSectionId));
        Assert.Equal(
            fixture.GeneratedFromDocumentVersionIds,
            current.Relationships
                .OfType<AuthorizedRelationship>()
                .Select(relationship => relationship.TargetDocumentVersionId)
                .Order());
        CurrentFlowGroup flow = Assert.Single(current.FlowGroups);
        Assert.Equal(2, flow.Traces.Count);
        Assert.Equal(
            fixture.GeneratedFromDocumentVersionIds,
            flow.Traces.Select(trace => Assert.IsType<Guid>(trace.Steps[1].DocumentVersionId)));
        Assert.Contains("库存预留", current.ContentMarkdown);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommitAsync_CrossRepositoryPass2_PreservesGeneratedFromOwnershipAndConsumerAnchors()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        CrossRepositoryPass2Fixture fixture = await CreateCrossRepositoryPass2FixtureAsync(
            session.Context,
            knowledgeStore);
        var committer = new WorkerAgentResultCommitter(knowledgeStore);
        RepositoryAccessScope scope = new(
            [fixture.ConsumerRepositoryId, fixture.ProviderRepositoryId]);

        WorkerAgentResultCommitOutcome outcome = await committer.CommitAsync(
            fixture.Packet,
            fixture.Result,
            scope,
            fixture.Binding,
            CancellationToken.None);

        Assert.Equal(WorkerAgentResultCommitOutcome.Committed, outcome);
        DocumentEdge[] edges = await session.Context.Set<DocumentEdge>()
            .AsNoTracking()
            .Where(edge => edge.EdgeType == DocumentEdgeType.GeneratedFrom)
            .OrderBy(edge => edge.TargetDocumentVersionId)
            .ToArrayAsync();
        Assert.Collection(
            edges,
            consumer =>
            {
                Assert.Equal(fixture.ConsumerDocumentVersionId, consumer.TargetDocumentVersionId);
                Assert.Equal(fixture.ConsumerRepositoryId, consumer.TargetRepositoryId);
                Assert.Equal(fixture.ConsumerScanRunId, consumer.TargetScanRunId);
            },
            provider =>
            {
                Assert.Equal(fixture.ProviderDocumentVersionId, provider.TargetDocumentVersionId);
                Assert.Equal(fixture.ProviderRepositoryId, provider.TargetRepositoryId);
                Assert.Equal(fixture.ProviderScanRunId, provider.TargetScanRunId);
            });

        Guid businessLogicalDocumentId = await session.Context.Set<LogicalDocument>()
            .AsNoTracking()
            .Where(document => document.DocumentType == DocumentType.BusinessLogic)
            .Select(document => document.Id)
            .SingleAsync();
        DocumentVersion businessVersion = await session.Context.Set<DocumentVersion>()
            .AsNoTracking()
            .SingleAsync(version => version.LogicalDocumentId == businessLogicalDocumentId);
        SourceAnchor anchor = await session.Context.Set<SourceAnchor>()
            .AsNoTracking()
            .SingleAsync(value => value.DocumentVersionId == businessVersion.Id);
        Assert.Equal(fixture.ConsumerRepositoryId, anchor.RepositoryId);
        Assert.Equal(fixture.ConsumerScanRunId, anchor.ScanRunId);
        Assert.DoesNotContain(
            await session.Context.Set<SourceAnchor>()
                .AsNoTracking()
                .Where(value => value.DocumentVersionId == businessVersion.Id)
                .ToArrayAsync(),
            value => value.RepositoryId == fixture.ProviderRepositoryId);

        FlowTraceStep[] relationshipSteps = await session.Context.Set<FlowTraceStep>()
            .AsNoTracking()
            .Where(step => step.StepKind == FlowTraceStepKind.Relationship)
            .OrderBy(step => step.DocumentVersionId)
            .ToArrayAsync();
        Assert.Contains(
            relationshipSteps,
            step => step.DocumentVersionId == fixture.ProviderDocumentVersionId &&
                step.DocumentRepositoryId == fixture.ProviderRepositoryId &&
                step.DocumentScanRunId == fixture.ProviderScanRunId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task HasCommittedResultAsync_StablePass2Job_RecognizesOnlyDurableBusinessLogicVersion()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        Pass2Fixture fixture = await CreatePass2FixtureAsync(session.Context, knowledgeStore);
        var committer = new WorkerAgentResultCommitter(knowledgeStore, fixture.Flow);
        RepositoryAccessScope scope = new([fixture.Packet.RepositoryId]);

        Assert.False(await committer.HasCommittedResultAsync(fixture.Packet, scope));

        Assert.Equal(
            WorkerAgentResultCommitOutcome.Committed,
            await committer.CommitAsync(fixture.Packet, fixture.Result, scope));

        Assert.True(await committer.HasCommittedResultAsync(fixture.Packet, scope));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommitAsync_AlternateAuthoritativeFlow_RejectsSelfConsistentPass2Packet()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        Pass2Fixture fixture = await CreatePass2FixtureAsync(session.Context, knowledgeStore);
        PreparedPass2Flow alternate = PreparedPass2Flow.Create(
            fixture.Flow.SchemaIdentity,
            WorkerAgentTestFixtures.Id(820),
            WorkerAgentTestFixtures.Id(821),
            fixture.Flow.SystemId,
            fixture.Flow.RepositoryId,
            fixture.Flow.Members,
            fixture.Flow.TraceEvidence);
        var committer = new WorkerAgentResultCommitter(knowledgeStore, alternate);

        WorkerAgentResultCommitOutcome outcome = await committer.CommitAsync(
            fixture.Packet,
            fixture.Result,
            new RepositoryAccessScope([fixture.Packet.RepositoryId]));

        Assert.Equal(WorkerAgentResultCommitOutcome.Rejected, outcome);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommitAsync_AlreadyCommittedPass2HasChangedCanonicalContent_ReturnsConflict()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        Pass2Fixture fixture = await CreatePass2FixtureAsync(session.Context, knowledgeStore);
        var committer = new WorkerAgentResultCommitter(knowledgeStore, fixture.Flow);
        RepositoryAccessScope scope = new([fixture.Packet.RepositoryId]);
        Assert.Equal(
            WorkerAgentResultCommitOutcome.Committed,
            await committer.CommitAsync(fixture.Packet, fixture.Result, scope));
        var baseline = Assert.IsType<Pass2BusinessLogicDocumentResultPayload>(
            fixture.Result.Payload);
        Pass2BusinessLogicDocumentResultPayload changedPayload =
            Pass2BusinessLogicDocumentResultPayload.Create(
                baseline.JobId,
                baseline.SystemId,
                baseline.RepositoryId,
                baseline.ScanRunId,
                baseline.ProfiledRevision,
                baseline.FlowId,
                baseline.Purpose,
                baseline.MainSteps,
                baseline.Outcome,
                baseline.Content with { Summary = "已变更的业务摘要。" },
                baseline.SourceAnchors,
                baseline.TraceEvidenceOrders,
                baseline.GeneratedFromDocumentVersionIds);
        WorkerAgentResultEnvelope changed = WorkerAgentResultEnvelope.CreateForJob(
            fixture.Packet,
            WorkerAgentResultOutcome.Valid,
            null,
            fixture.Result.DispatchedAt,
            fixture.Result.ReceivedAt,
            changedPayload);

        WorkerAgentResultCommitOutcome replay = await committer.CommitAsync(
            fixture.Packet,
            changed,
            scope);

        Assert.Equal(WorkerAgentResultCommitOutcome.Conflict, replay);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitAsync_Pass2GeneratedFromSetIsMissingOrExtra_RejectsAtomically(
        bool addExtraTarget)
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        Pass2Fixture fixture = await CreatePass2FixtureAsync(session.Context, knowledgeStore);
        Pass2BusinessLogicDocumentResultPayload baseline =
            Assert.IsType<Pass2BusinessLogicDocumentResultPayload>(fixture.Result.Payload);
        Guid[] invalidTargets = addExtraTarget
            ? [.. fixture.GeneratedFromDocumentVersionIds, WorkerAgentTestFixtures.Id(399)]
            : [fixture.GeneratedFromDocumentVersionIds[0]];
        WorkerAgentResultEnvelope invalid = CreatePass2Envelope(
            fixture.Packet,
            baseline,
            invalidTargets);
        int documentsBefore = await session.Context.Set<LogicalDocument>().CountAsync();
        int versionsBefore = await session.Context.Set<DocumentVersion>().CountAsync();
        int anchorsBefore = await session.Context.Set<SourceAnchor>().CountAsync();
        int edgesBefore = await session.Context.Set<DocumentEdge>().CountAsync();

        WorkerAgentResultCommitOutcome outcome = await new WorkerAgentResultCommitter(
            knowledgeStore,
            fixture.Flow).CommitAsync(
                fixture.Packet,
                invalid,
                new RepositoryAccessScope([fixture.Packet.RepositoryId]));

        Assert.Equal(WorkerAgentResultCommitOutcome.Rejected, outcome);
        Assert.Equal(documentsBefore, await session.Context.Set<LogicalDocument>().CountAsync());
        Assert.Equal(versionsBefore, await session.Context.Set<DocumentVersion>().CountAsync());
        Assert.Equal(anchorsBefore, await session.Context.Set<SourceAnchor>().CountAsync());
        Assert.Equal(edgesBefore, await session.Context.Set<DocumentEdge>().CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommitAsync_Pass2SynthesisInputIsStale_RejectsAtomically()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        Pass2Fixture fixture = await CreatePass2FixtureAsync(session.Context, knowledgeStore);
        var job = Assert.IsType<Pass2BusinessLogicJobPayload>(fixture.Packet.Payload);
        WorkerAgentCodeLogicDocumentVersionProjection stale = job.CodeLogicDocumentVersions[0];
        Guid successorVersionId = WorkerAgentTestFixtures.Id(399);
        Guid successorAnchorId = WorkerAgentTestFixtures.Id(499);
        CanonicalDocumentContent successorContent = new(
            KnowledgeDocumentLimits.SchemaIdentity,
            DocumentType.CodeLogic,
            "orders.flow-1",
            "订单总额计算更新",
            stale.RepositoryRelativePath,
            "更新后的订单总额计算。",
            [],
            ["订单"],
            [],
            [
                new CanonicalSection(
                    WorkerAgentTestFixtures.Id(599),
                    1,
                    DocumentSectionKind.Overview,
                    "概览",
                    "更新后的订单总额计算。",
                    "订单 总额 更新",
                    [successorAnchorId],
                    []),
            ]);
        Assert.Equal(
            KnowledgeCommitStatus.Committed,
            (await knowledgeStore.CommitAsync(
                new KnowledgeCommit(
                    WorkerAgentTestFixtures.Id(799),
                    stale.LogicalDocumentId,
                    stale.DocumentVersionId,
                    successorVersionId,
                    fixture.Packet.RepositoryId,
                    fixture.Packet.ScanRunId,
                    fixture.Packet.ProfiledRevision,
                    successorContent,
                    [
                        new SourceAnchorDraft(
                            successorAnchorId,
                            successorVersionId,
                            fixture.Packet.RepositoryId,
                            fixture.Packet.ScanRunId,
                            WorkerAgentTestFixtures.Id(106),
                            fixture.Packet.ProfiledRevision,
                            stale.RepositoryRelativePath,
                            "OrderService.CalculateTotalUpdated",
                            12,
                            24,
                            WorkerAgentTestFixtures.SourceHash),
                    ],
                    [],
                    [],
                    KnowledgeStoreTestFixtures.CreatedAt.AddMinutes(1)),
                new RepositoryAccessScope([fixture.Packet.RepositoryId]))).Status);
        int documentsBefore = await session.Context.Set<LogicalDocument>().CountAsync();
        int versionsBefore = await session.Context.Set<DocumentVersion>().CountAsync();
        int edgesBefore = await session.Context.Set<DocumentEdge>().CountAsync();

        WorkerAgentResultCommitOutcome outcome = await new WorkerAgentResultCommitter(
            knowledgeStore,
            fixture.Flow).CommitAsync(
                fixture.Packet,
                fixture.Result,
                new RepositoryAccessScope([fixture.Packet.RepositoryId]));

        Assert.Equal(WorkerAgentResultCommitOutcome.Rejected, outcome);
        Assert.Equal(documentsBefore, await session.Context.Set<LogicalDocument>().CountAsync());
        Assert.Equal(versionsBefore, await session.Context.Set<DocumentVersion>().CountAsync());
        Assert.Equal(edgesBefore, await session.Context.Set<DocumentEdge>().CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommitAsync_OutOfRangeSourceAnchor_RejectsWithZeroVisibleRowsAsync()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        await SeedOwnershipAndReferenceAsync(session.Context, packet);
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        await SeedReferenceDocumentAsync(knowledgeStore, packet);
        WorkerAgentResultEnvelope invalidEnvelope = CreateOutOfRangeAnchorResult(packet);
        int documentsBefore = await session.Context.Set<LogicalDocument>().CountAsync();
        int versionsBefore = await session.Context.Set<DocumentVersion>().CountAsync();
        int anchorsBefore = await session.Context.Set<SourceAnchor>().CountAsync();
        int edgesBefore = await session.Context.Set<DocumentEdge>().CountAsync();
        int sectionsBefore = await session.Context.Set<DocumentSection>().CountAsync();
        int projectionsBefore = await session.Context.Set<IndexEntry>().CountAsync();
        var committer = new WorkerAgentResultCommitter(knowledgeStore);

        WorkerAgentResultCommitOutcome outcome = await committer.CommitAsync(
            packet,
            invalidEnvelope,
            new RepositoryAccessScope([packet.RepositoryId]));

        Assert.Equal(WorkerAgentResultCommitOutcome.Rejected, outcome);
        Assert.Equal(documentsBefore, await session.Context.Set<LogicalDocument>().CountAsync());
        Assert.Equal(versionsBefore, await session.Context.Set<DocumentVersion>().CountAsync());
        Assert.Equal(anchorsBefore, await session.Context.Set<SourceAnchor>().CountAsync());
        Assert.Equal(edgesBefore, await session.Context.Set<DocumentEdge>().CountAsync());
        Assert.Equal(sectionsBefore, await session.Context.Set<DocumentSection>().CountAsync());
        Assert.Equal(projectionsBefore, await session.Context.Set<IndexEntry>().CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommitAsync_OutOfRangeSourceAnchor_DefensiveRevalidationAvoidsStoreCallAsync()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        var store = new PermissiveKnowledgeStore();
        var committer = new WorkerAgentResultCommitter(store);

        WorkerAgentResultCommitOutcome outcome = await committer.CommitAsync(
            packet,
            CreateOutOfRangeAnchorResult(packet),
            new RepositoryAccessScope([packet.RepositoryId]));

        Assert.Equal(WorkerAgentResultCommitOutcome.Rejected, outcome);
        Assert.Equal(0, store.GetCurrentCount);
        Assert.Equal(0, store.CommitCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommitAsync_PreparedCommunicationEvidenceIsOmitted_RejectsBeforeStoreCallAsync()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        var store = new PermissiveKnowledgeStore();
        var committer = new WorkerAgentResultCommitter(store);

        WorkerAgentResultCommitOutcome outcome = await committer.CommitAsync(
            packet,
            CreateOmittedCommunicationEvidenceResult(packet),
            new RepositoryAccessScope([packet.RepositoryId]));

        Assert.Equal(WorkerAgentResultCommitOutcome.Rejected, outcome);
        Assert.Equal(0, store.GetCurrentCount);
        Assert.Equal(0, store.CommitCount);
    }

    private static WorkerAgentResultEnvelope CreateValidResult(
        WorkerAgentJobPacket packet,
        Guid? anchorId = null,
        string title = "订单总额计算")
    {
        Guid sourceAnchorId = anchorId ?? WorkerAgentTestFixtures.Id(11);
        CanonicalDocumentContent content = new(
            KnowledgeDocumentLimits.SchemaIdentity,
            DocumentType.CodeLogic,
            "orders.calculate-total",
            title,
            WorkerAgentTestFixtures.RepositoryRelativePath,
            "根据订单行计算订单总额。",
            ["CalculateTotal"],
            ["订单总额"],
            ["OrderService"],
            [
                new CanonicalSection(
                    WorkerAgentTestFixtures.Id(10),
                    1,
                    DocumentSectionKind.TechnicalDetail,
                    "计算逻辑",
                    "汇总订单行金额。",
                    "订单 总额 计算",
                    [sourceAnchorId],
                    [WorkerAgentTestFixtures.Id(9)]),
                new CanonicalSection(
                    WorkerAgentTestFixtures.Id(8),
                    2,
                    DocumentSectionKind.Evidence,
                    "HTTP",
                    "订单服务调用库存接口。",
                    "订单服务调用库存接口。",
                    [sourceAnchorId],
                    []),
            ]);
        Pass1CodeLogicDocumentResultPayload payload =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(
                packet,
                content: content,
                sourceAnchors:
                [
                    WorkerAgentSourceAnchor.Create(
                        sourceAnchorId,
                        WorkerAgentTestFixtures.RepositoryRelativePath,
                        "OrderService.CalculateTotal",
                        12,
                        24,
                        WorkerAgentTestFixtures.SourceHash),
                ]);
        DateTimeOffset dispatchedAt = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        return WorkerAgentResultEnvelope.CreateForJob(
            packet,
            WorkerAgentResultOutcome.Valid,
            null,
            dispatchedAt,
            dispatchedAt.AddSeconds(1),
            payload);
    }

    private static WorkerAgentResultEnvelope CreateOutOfRangeAnchorResult(
        WorkerAgentJobPacket packet)
    {
        Pass1CodeLogicDocumentResultPayload validPayload = Assert.IsType<Pass1CodeLogicDocumentResultPayload>(
            CreateValidResult(packet).Payload);
        Pass1CodeLogicDocumentResultPayload invalidPayload =
            Pass1CodeLogicDocumentResultPayload.Create(
                validPayload.JobId,
                validPayload.SystemId,
                validPayload.RepositoryId,
                validPayload.ScanRunId,
                validPayload.CodeUnitId,
                validPayload.ProfiledRevision,
                validPayload.RepositoryRelativePath,
                validPayload.LineStart,
                validPayload.LineEnd,
                validPayload.SourceHash,
                validPayload.FileIdentityHash,
                validPayload.Content,
                [
                    WorkerAgentSourceAnchor.Create(
                        WorkerAgentTestFixtures.Id(11),
                        validPayload.RepositoryRelativePath,
                        "OrderService.CalculateTotal",
                        validPayload.LineEnd + 1,
                        validPayload.LineEnd + 1,
                        validPayload.SourceHash),
                ],
                validPayload.ReferenceDocumentVersionIds,
                validPayload.CommunicationEvidenceIds);
        DateTimeOffset dispatchedAt = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        return WorkerAgentResultEnvelope.Create(
            packet.JobId,
            packet.ScanExecutionId,
            packet.Kind,
            WorkerAgentResultOutcome.Valid,
            boundedReason: null,
            dispatchedAt,
            dispatchedAt.AddSeconds(1),
            invalidPayload);
    }

    private static WorkerAgentResultEnvelope CreateOmittedCommunicationEvidenceResult(
        WorkerAgentJobPacket packet)
    {
        Pass1CodeLogicDocumentResultPayload valid = Assert.IsType<
            Pass1CodeLogicDocumentResultPayload>(CreateValidResult(packet).Payload);
        Pass1CodeLogicDocumentResultPayload omitted =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(
                packet,
                content: valid.Content with
                {
                    Sections = valid.Content.Sections
                        .Where(section => section.Kind != DocumentSectionKind.Evidence)
                        .ToArray(),
                },
                sourceAnchors: valid.SourceAnchors,
                referenceDocumentVersionIds: valid.ReferenceDocumentVersionIds,
                communicationEvidenceIds: []);
        DateTimeOffset dispatchedAt = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        return WorkerAgentResultEnvelope.Create(
            packet.JobId,
            packet.ScanExecutionId,
            packet.Kind,
            WorkerAgentResultOutcome.Valid,
            boundedReason: null,
            dispatchedAt,
            dispatchedAt.AddSeconds(1),
            omitted);
    }

    private static WorkerAgentResultEnvelope CreatePass2Envelope(
        WorkerAgentJobPacket packet,
        Pass2BusinessLogicDocumentResultPayload baseline,
        IReadOnlyList<Guid> generatedFromDocumentVersionIds)
    {
        Pass2BusinessLogicDocumentResultPayload payload =
            Pass2BusinessLogicDocumentResultPayload.Create(
                baseline.JobId,
                baseline.SystemId,
                baseline.RepositoryId,
                baseline.ScanRunId,
                baseline.ProfiledRevision,
                baseline.FlowId,
                baseline.Purpose,
                baseline.MainSteps,
                baseline.Outcome,
                baseline.Content,
                baseline.SourceAnchors,
                baseline.TraceEvidenceOrders,
                generatedFromDocumentVersionIds);
        return WorkerAgentResultEnvelope.Create(
            packet.JobId,
            packet.ScanExecutionId,
            packet.Kind,
            WorkerAgentResultOutcome.Valid,
            null,
            new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 12, 0, 1, TimeSpan.Zero),
            payload);
    }

    private sealed class PermissiveKnowledgeStore : IKnowledgeStoreRepository
    {
        public int GetCurrentCount { get; private set; }
        public int CommitCount { get; private set; }

        public Task<CurrentKnowledge?> GetCurrentAsync(
            Guid logicalDocumentId,
            RepositoryAccessScope scope,
            IReadOnlyCollection<Guid> requestedStableSectionIds,
            CancellationToken cancellationToken = default)
        {
            GetCurrentCount++;
            return Task.FromResult<CurrentKnowledge?>(null);
        }

        public Task<KnowledgeCommitResult> CommitAsync(
            KnowledgeCommit request,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.FromResult(KnowledgeCommitResult.Committed(
                request.LogicalDocumentId,
                request.NewDocumentVersionId));
        }

        public Task<RebuildProjectionStatus> RebuildProjectionAsync(
            Guid documentVersionId,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingKnowledgeStore(IKnowledgeStoreRepository inner)
        : IKnowledgeStoreRepository
    {
        public KnowledgeCommitResult? LastResult { get; private set; }

        public Task<CurrentKnowledge?> GetCurrentAsync(
            Guid logicalDocumentId,
            RepositoryAccessScope scope,
            IReadOnlyCollection<Guid> requestedStableSectionIds,
            CancellationToken cancellationToken = default) =>
            inner.GetCurrentAsync(
                logicalDocumentId,
                scope,
                requestedStableSectionIds,
                cancellationToken);

        public async Task<KnowledgeCommitResult> CommitAsync(
            KnowledgeCommit request,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default)
        {
            LastResult = await inner.CommitAsync(request, scope, cancellationToken);
            return LastResult;
        }

        public Task<RebuildProjectionStatus> RebuildProjectionAsync(
            Guid documentVersionId,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default) =>
            inner.RebuildProjectionAsync(documentVersionId, scope, cancellationToken);
    }

    private static async Task SeedOwnershipAndReferenceAsync(
        VulgataDbContext context,
        WorkerAgentJobPacket packet)
    {
        Repository repository = Repository.CreateStandalone(
            "orders",
            "https://example.invalid/orders.git",
            null,
            null,
            KnowledgeStoreTestFixtures.CreatedAt);
        SetId(repository, packet.RepositoryId);
        ScanRun scanRun = KnowledgeStoreTestFixtures.CreateCompletedScanRun(
            packet.ScanRunId,
            packet.RepositoryId);
        var pass1 = Assert.IsType<Pass1CodeLogicJobPayload>(packet.Payload);
        var codeUnit = new CodeUnit(
            packet.ScanRunId,
            packet.RepositoryId,
            pass1.RepositoryRelativePath,
            pass1.FileIdentityHash,
            pass1.SourceHash,
            pass1.LineStart,
            pass1.LineEnd,
            packet.SourceSlice.SymbolName,
            packet.SourceSlice.UnitType,
            RangeConfidence.Exact,
            packet.SourceSlice.Language);
        SetId(codeUnit, packet.CodeUnitId);
        context.Repositories.Add(repository);
        context.ScanRuns.Add(scanRun);
        context.CodeUnits.Add(codeUnit);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task SeedReferenceDocumentAsync(
        IKnowledgeStoreRepository knowledgeStore,
        WorkerAgentJobPacket packet)
    {
        CanonicalDocumentContent content = new(
            KnowledgeDocumentLimits.SchemaIdentity,
            DocumentType.CodeLogic,
            "orders.reference-target",
            "库存引用目标",
            WorkerAgentTestFixtures.RepositoryRelativePath,
            "提供库存引用目标。",
            ["Inventory"],
            ["库存"],
            ["HTTP"],
            [
                new CanonicalSection(
                    WorkerAgentTestFixtures.Id(90),
                    1,
                    DocumentSectionKind.Overview,
                    "概览",
                    "库存引用目标。",
                    "库存 引用",
                    [],
                    []),
            ]);
        KnowledgeCommit target = new(
            WorkerAgentTestFixtures.Id(91),
            WorkerAgentTestFixtures.Id(92),
            null,
            WorkerAgentTestFixtures.Id(9),
            packet.RepositoryId,
            packet.ScanRunId,
            packet.ProfiledRevision,
            content,
            [],
            [],
            [],
            KnowledgeStoreTestFixtures.CreatedAt);
        KnowledgeCommitResult committed = await knowledgeStore.CommitAsync(
            target,
            new RepositoryAccessScope([packet.RepositoryId]));
        Assert.Equal(KnowledgeCommitStatus.Committed, committed.Status);
    }

    private static async Task<Pass2Fixture> CreatePass2FixtureAsync(
        VulgataDbContext context,
        IKnowledgeStoreRepository knowledgeStore)
    {
        Guid repositoryId = WorkerAgentTestFixtures.Id(3);
        Guid systemId = WorkerAgentTestFixtures.Id(4);
        Guid scanRunId = WorkerAgentTestFixtures.Id(5);
        PreparedPass2Flow flow = PreparedPass2Flow.Create(
            PreparedPass2FlowLimits.SupportedSchemaIdentity,
            WorkerAgentTestFixtures.Id(200),
            WorkerAgentTestFixtures.Id(201),
            systemId,
            repositoryId,
            [
                new(1, WorkerAgentTestFixtures.Id(101), WorkerAgentTestFixtures.Id(106), WorkerAgentTestFixtures.Id(201)),
                new(2, WorkerAgentTestFixtures.Id(102), WorkerAgentTestFixtures.Id(107), WorkerAgentTestFixtures.Id(202)),
            ],
            [
                new(1, WorkerAgentTestFixtures.Id(101), WorkerAgentTestFixtures.Id(106), "订单总额计算", "src/Orders/OrderService.cs#L12-L24"),
                new(2, WorkerAgentTestFixtures.Id(102), WorkerAgentTestFixtures.Id(107), "库存预留", "src/Inventory/InventoryService.cs#L30-L44"),
            ]);

        Repository repository = Repository.CreateStandalone(
            "orders",
            "https://example.invalid/orders.git",
            null,
            null,
            KnowledgeStoreTestFixtures.CreatedAt);
        SetId(repository, repositoryId);
        context.Repositories.Add(repository);
        context.ScanRuns.Add(KnowledgeStoreTestFixtures.CreateCompletedScanRun(scanRunId, repositoryId));
        for (int index = 0; index < flow.Members.Count; index++)
        {
            PreparedPass2FlowMember member = flow.Members[index];
            string path = index == 0
                ? "src/Orders/OrderService.cs"
                : "src/Inventory/InventoryService.cs";
            var codeUnit = new CodeUnit(
                scanRunId,
                repositoryId,
                path,
                WorkerAgentTestFixtures.FileIdentityHash,
                WorkerAgentTestFixtures.SourceHash,
                index == 0 ? 12 : 30,
                index == 0 ? 24 : 44,
                index == 0 ? "CalculateTotal" : "ReserveStock",
                CodeUnitType.Method,
                RangeConfidence.Exact,
                "csharp");
            SetId(codeUnit, member.CodeUnitId);
            context.CodeUnits.Add(codeUnit);
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var currentDocuments = new List<CurrentKnowledge>();
        for (int index = 0; index < flow.Members.Count; index++)
        {
            PreparedPass2FlowMember member = flow.Members[index];
            Guid documentVersionId = WorkerAgentTestFixtures.Id(301 + index);
            Guid anchorId = WorkerAgentTestFixtures.Id(401 + index);
            string path = index == 0
                ? "src/Orders/OrderService.cs"
                : "src/Inventory/InventoryService.cs";
            int lineStart = index == 0 ? 12 : 30;
            int lineEnd = index == 0 ? 24 : 44;
            Guid sectionId = WorkerAgentTestFixtures.Id(501 + index);
            CanonicalDocumentContent content = new(
                KnowledgeDocumentLimits.SchemaIdentity,
                DocumentType.CodeLogic,
                $"orders.flow-{index + 1}",
                index == 0 ? "订单总额计算" : "库存预留",
                path,
                index == 0 ? "计算订单总额。" : "预留订单库存。",
                [],
                index == 0 ? ["订单"] : ["库存"],
                [],
                [
                    new CanonicalSection(
                        sectionId,
                        1,
                        DocumentSectionKind.Overview,
                        "概览",
                        index == 0 ? "计算订单总额。\n核对订单行。" : "预留订单库存。\n确认库存结果。",
                        index == 0 ? "订单 总额" : "订单 库存",
                        [anchorId],
                        []),
                ]);
            KnowledgeCommitResult committed = await knowledgeStore.CommitAsync(
                new KnowledgeCommit(
                    WorkerAgentTestFixtures.Id(701 + index),
                    member.LogicalDocumentId,
                    null,
                    documentVersionId,
                    repositoryId,
                    scanRunId,
                    WorkerAgentTestFixtures.ProfiledRevision,
                    content,
                    [
                        new SourceAnchorDraft(
                            anchorId,
                            documentVersionId,
                            repositoryId,
                            scanRunId,
                            member.CodeUnitId,
                            WorkerAgentTestFixtures.ProfiledRevision,
                            path,
                            index == 0 ? "OrderService.CalculateTotal" : "InventoryService.ReserveStock",
                            lineStart,
                            lineEnd,
                            WorkerAgentTestFixtures.SourceHash),
                    ],
                    [],
                    [],
                    KnowledgeStoreTestFixtures.CreatedAt),
                new RepositoryAccessScope([repositoryId]));
            Assert.Equal(KnowledgeCommitStatus.Committed, committed.Status);
            CurrentKnowledge current = Assert.IsType<CurrentKnowledge>(
                await knowledgeStore.GetCurrentAsync(
                    member.LogicalDocumentId,
                    new RepositoryAccessScope([repositoryId]),
                    [sectionId]));
            currentDocuments.Add(current);
        }

        Pass2BusinessLogicJobPayload job = Pass2BusinessLogicJobPayload.Create(
            flow,
            currentDocuments);
        WorkerAgentJobPacket packet = WorkerAgentJobPacket.Create(
            WorkerAgentTestFixtures.Id(801),
            WorkerAgentJobKind.Pass2BusinessLogicDocument,
            WorkerAgentTestFixtures.Id(2),
            repositoryId,
            systemId,
            scanRunId,
            WorkerAgentTestFixtures.ProfiledRevision,
            WorkerAgentContractLimits.SupportedSchemaVersion,
            job);
        Guid[] generatedFromIds = currentDocuments
            .Select(document => document.DocumentVersionId)
            .ToArray();
        Guid[] sectionIds =
        [
            WorkerAgentTestFixtures.Id(901),
            WorkerAgentTestFixtures.Id(902),
            WorkerAgentTestFixtures.Id(903),
        ];
        CanonicalDocumentContent businessContent = new(
            KnowledgeDocumentLimits.SchemaIdentity,
            DocumentType.BusinessLogic,
            "orders.price-and-reserve",
            "订单计价与库存预留",
            "flows/orders-price-and-reserve",
            "订单完成计价后预留库存，并说明它引用的业务背景。",
            [],
            ["库存", "订单"],
            [],
            [
                new CanonicalSection(sectionIds[0], 1, DocumentSectionKind.Overview, "业务目的", "帮助业务人员理解订单处理。", "订单 目的", [WorkerAgentTestFixtures.Id(401)], [generatedFromIds[0]]),
                new CanonicalSection(sectionIds[1], 2, DocumentSectionKind.ProcessStep, "主要步骤", "先计算订单总额，再预留对应库存。", "订单 总额 库存", [WorkerAgentTestFixtures.Id(401), WorkerAgentTestFixtures.Id(402)], generatedFromIds),
                new CanonicalSection(sectionIds[2], 3, DocumentSectionKind.Overview, "业务结果", "订单完成计价并成功预留库存。", "订单 结果 库存", [WorkerAgentTestFixtures.Id(402)], [generatedFromIds[1]]),
            ]);
        Pass2BusinessLogicDocumentResultPayload resultPayload =
            Pass2BusinessLogicDocumentResultPayload.Create(
                packet.JobId,
                packet.SystemId,
                packet.RepositoryId,
                packet.ScanRunId,
                packet.ProfiledRevision,
                flow.FlowId,
                "帮助业务人员理解订单从计价到库存预留的处理过程。",
                [
                    WorkerAgentBusinessMainStep.Create(1, "计算订单总额。", [1]),
                    WorkerAgentBusinessMainStep.Create(2, "预留对应库存。", [2]),
                ],
                "订单完成计价并成功预留库存。",
                businessContent,
                currentDocuments
                    .SelectMany(document => document.SourceAnchors)
                    .Select(anchor => WorkerAgentSourceAnchor.Create(
                        anchor.Id,
                        anchor.RepositoryRelativePath,
                        anchor.EvidenceLocator,
                        anchor.LineStart,
                        anchor.LineEnd,
                        anchor.SourceHash))
                    .ToArray(),
                [1, 2],
                generatedFromIds);
        DateTimeOffset dispatchedAt = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        WorkerAgentResultEnvelope result = WorkerAgentResultEnvelope.CreateForJob(
            packet,
            WorkerAgentResultOutcome.Valid,
            null,
            dispatchedAt,
            dispatchedAt.AddSeconds(1),
            resultPayload);
        return new(flow, packet, result, generatedFromIds, sectionIds);
    }

    private static async Task<CrossRepositoryPass2Fixture> CreateCrossRepositoryPass2FixtureAsync(
        VulgataDbContext context,
        IKnowledgeStoreRepository knowledgeStore)
    {
        Guid systemId = WorkerAgentTestFixtures.Id(1_000);
        Guid consumerRepositoryId = WorkerAgentTestFixtures.Id(1_001);
        Guid providerRepositoryId = WorkerAgentTestFixtures.Id(1_002);
        Guid consumerScanRunId = WorkerAgentTestFixtures.Id(1_003);
        Guid providerScanRunId = WorkerAgentTestFixtures.Id(1_004);
        Guid consumerExecutionId = WorkerAgentTestFixtures.Id(1_005);
        Guid providerExecutionId = WorkerAgentTestFixtures.Id(1_006);
        Guid consumerCodeUnitId = WorkerAgentTestFixtures.Id(1_007);
        Guid providerCodeUnitId = WorkerAgentTestFixtures.Id(1_008);
        Guid consumerLogicalDocumentId = WorkerAgentTestFixtures.Id(1_009);
        Guid providerLogicalDocumentId = WorkerAgentTestFixtures.Id(1_010);
        Guid consumerAnchorId = WorkerAgentTestFixtures.Id(1_011);
        Guid providerAnchorId = WorkerAgentTestFixtures.Id(1_012);
        string consumerRevision = new('a', 40);
        string providerRevision = new('b', 40);
        OrdinaryPass1JobIdentity consumerJob = OrdinaryPass1JobIdentity.Create(
            consumerExecutionId,
            consumerScanRunId,
            consumerRepositoryId,
            consumerCodeUnitId,
            consumerRevision);
        OrdinaryPass1JobIdentity providerJob = OrdinaryPass1JobIdentity.Create(
            providerExecutionId,
            providerScanRunId,
            providerRepositoryId,
            providerCodeUnitId,
            providerRevision);
        Guid consumerDocumentVersionId =
            OrdinaryPass1JobIdentity.CreateDocumentVersionId(consumerJob.JobId);
        Guid providerDocumentVersionId =
            OrdinaryPass1JobIdentity.CreateDocumentVersionId(providerJob.JobId);

        Repository consumerRepository = Repository.CreateStandalone(
            "consumer",
            "https://example.invalid/consumer.git",
            null,
            null,
            KnowledgeStoreTestFixtures.CreatedAt);
        Repository providerRepository = Repository.CreateStandalone(
            "provider",
            "https://example.invalid/provider.git",
            null,
            null,
            KnowledgeStoreTestFixtures.CreatedAt);
        SetId(consumerRepository, consumerRepositoryId);
        SetId(providerRepository, providerRepositoryId);
        context.Repositories.AddRange(consumerRepository, providerRepository);
        context.ScanRuns.AddRange(
            CompletedScanRun(consumerScanRunId, consumerRepositoryId, consumerRevision),
            CompletedScanRun(providerScanRunId, providerRepositoryId, providerRevision));
        context.CodeUnits.AddRange(
            CodeUnit(consumerCodeUnitId, consumerScanRunId, consumerRepositoryId,
                "src/Consumer.cs", "Consumer", 1, 10),
            CodeUnit(providerCodeUnitId, providerScanRunId, providerRepositoryId,
                "src/Provider.cs", "Provider", 20, 30));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        RepositoryAccessScope supportScope =
            new([consumerRepositoryId, providerRepositoryId]);
        CurrentKnowledge consumer = await CommitCodeLogicAsync(
            knowledgeStore,
            supportScope,
            WorkerAgentTestFixtures.Id(1_020),
            consumerLogicalDocumentId,
            consumerDocumentVersionId,
            consumerRepositoryId,
            consumerScanRunId,
            consumerRevision,
            consumerCodeUnitId,
            consumerAnchorId,
            WorkerAgentTestFixtures.Id(1_013),
            "src/Consumer.cs",
            "Consumer",
            1,
            10);
        CurrentKnowledge provider = await CommitCodeLogicAsync(
            knowledgeStore,
            supportScope,
            WorkerAgentTestFixtures.Id(1_021),
            providerLogicalDocumentId,
            providerDocumentVersionId,
            providerRepositoryId,
            providerScanRunId,
            providerRevision,
            providerCodeUnitId,
            providerAnchorId,
            WorkerAgentTestFixtures.Id(1_014),
            "src/Provider.cs",
            "Provider",
            20,
            30);

        var consumerBinding = new PreparedPass2RuntimeRepositoryBinding(
            "consumer", "git-consumer", consumerRepositoryId, consumerScanRunId,
            consumerExecutionId, "refs/tags/consumer", consumerRevision, consumerRevision);
        var providerBinding = new PreparedPass2RuntimeRepositoryBinding(
            "provider", "git-provider", providerRepositoryId, providerScanRunId,
            providerExecutionId, "refs/tags/provider", providerRevision, providerRevision);
        PreparedPass2RuntimeMember[] members =
        [
            new(1, consumerJob.JobId, consumerCodeUnitId, consumerLogicalDocumentId,
                consumerRepositoryId, consumerDocumentVersionId, [consumerAnchorId]),
            new(2, providerJob.JobId, providerCodeUnitId, providerLogicalDocumentId,
                providerRepositoryId, providerDocumentVersionId, [providerAnchorId]),
        ];
        PreparedPass2RuntimeBinding binding = PreparedPass2RuntimeBinding.Create(
            "1.0",
            "cross-repository-test",
            "1.0.0",
            new string('1', 64),
            "rpc-test",
            systemId,
            consumerBinding,
            providerBinding,
            WorkerAgentTestFixtures.Id(1_015),
            [WorkerAgentTestFixtures.Id(1_016)],
            members,
            [
                new(1, consumerJob.JobId, consumerCodeUnitId,
                    "Consumer invocation", "src/Consumer.cs#L1-L10"),
                new(2, providerJob.JobId, providerCodeUnitId,
                    "Provider implementation", "src/Provider.cs#L20-L30"),
            ]);
        ResolvedPreparedPass2Flow resolved = new(binding.Flow, [consumer, provider], binding);
        Pass2BusinessLogicJobPayload job = Pass2BusinessLogicJobPayload.Create(resolved);
        WorkerAgentJobPacket packet = WorkerAgentJobPacket.Create(
            WorkerAgentTestFixtures.Id(1_017),
            WorkerAgentJobKind.Pass2BusinessLogicDocument,
            consumerExecutionId,
            consumerRepositoryId,
            systemId,
            consumerScanRunId,
            consumerRevision,
            WorkerAgentContractLimits.SupportedSchemaVersion,
            job);
        Guid sectionId = WorkerAgentTestFixtures.Id(1_018);
        CanonicalDocumentContent content = new(
            KnowledgeDocumentLimits.SchemaIdentity,
            DocumentType.BusinessLogic,
            "consumer-to-provider",
            "Consumer to Provider",
            "flows/consumer-to-provider",
            "The consumer invokes the provider.",
            [],
            [],
            [],
            [
                new CanonicalSection(
                    sectionId,
                    1,
                    DocumentSectionKind.ProcessStep,
                    "Flow",
                    "The consumer invokes the provider.",
                    "consumer provider",
                    [consumerAnchorId],
                    [consumerDocumentVersionId, providerDocumentVersionId]),
            ]);
        WorkerAgentSourceAnchor[] anchors = new[] { consumer, provider }
            .SelectMany(document => document.SourceAnchors)
            .OrderBy(anchor => anchor.Id)
            .Select(anchor => WorkerAgentSourceAnchor.Create(
                anchor.Id,
                anchor.RepositoryRelativePath,
                anchor.EvidenceLocator,
                anchor.LineStart,
                anchor.LineEnd,
                anchor.SourceHash))
            .ToArray();
        Pass2BusinessLogicDocumentResultPayload resultPayload =
            Pass2BusinessLogicDocumentResultPayload.Create(
                packet.JobId,
                packet.SystemId,
                packet.RepositoryId,
                packet.ScanRunId,
                packet.ProfiledRevision,
                binding.Flow.FlowId,
                "Explain the consumer-to-provider flow.",
                [
                    WorkerAgentBusinessMainStep.Create(1, "Invoke the provider.", [1]),
                    WorkerAgentBusinessMainStep.Create(2, "Process the request.", [2]),
                ],
                "The provider processes the request.",
                content,
                anchors,
                [1, 2],
                [consumerDocumentVersionId, providerDocumentVersionId]);
        DateTimeOffset dispatchedAt = new(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);
        WorkerAgentResultEnvelope result = WorkerAgentResultEnvelope.CreateForJob(
            packet,
            WorkerAgentResultOutcome.Valid,
            null,
            dispatchedAt,
            dispatchedAt.AddSeconds(1),
            resultPayload);
        return new(
            binding,
            packet,
            result,
            consumerRepositoryId,
            providerRepositoryId,
            consumerScanRunId,
            providerScanRunId,
            consumerDocumentVersionId,
            providerDocumentVersionId);
    }

    private static ScanRun CompletedScanRun(Guid id, Guid repositoryId, string revision)
    {
        ScanRun scanRun = ScanRun.Start(repositoryId, 1_800, KnowledgeStoreTestFixtures.CreatedAt);
        SetId(scanRun, id);
        scanRun.BindProfiledRevision(revision);
        scanRun.SetCandidateCount(1);
        scanRun.Complete(1, 1, KnowledgeStoreTestFixtures.CreatedAt.AddSeconds(1));
        return scanRun;
    }

    private static CodeUnit CodeUnit(
        Guid id,
        Guid scanRunId,
        Guid repositoryId,
        string path,
        string symbol,
        int lineStart,
        int lineEnd)
    {
        var codeUnit = new CodeUnit(
            scanRunId,
            repositoryId,
            path,
            WorkerAgentTestFixtures.FileIdentityHash,
            WorkerAgentTestFixtures.SourceHash,
            lineStart,
            lineEnd,
            symbol,
            CodeUnitType.Class,
            RangeConfidence.Exact,
            "csharp");
        SetId(codeUnit, id);
        return codeUnit;
    }

    private static async Task<CurrentKnowledge> CommitCodeLogicAsync(
        IKnowledgeStoreRepository knowledgeStore,
        RepositoryAccessScope scope,
        Guid operationId,
        Guid logicalDocumentId,
        Guid documentVersionId,
        Guid repositoryId,
        Guid scanRunId,
        string revision,
        Guid codeUnitId,
        Guid anchorId,
        Guid sectionId,
        string path,
        string title,
        int lineStart,
        int lineEnd)
    {
        CanonicalDocumentContent content = new(
            KnowledgeDocumentLimits.SchemaIdentity,
            DocumentType.CodeLogic,
            title.ToLowerInvariant(),
            title,
            path,
            $"{title} summary.",
            [],
            [],
            [],
            [
                new CanonicalSection(
                    sectionId,
                    1,
                    DocumentSectionKind.Overview,
                    "Overview",
                    $"{title} summary.",
                    title,
                    [anchorId],
                    []),
            ]);
        KnowledgeCommitResult committed = await knowledgeStore.CommitAsync(
            new KnowledgeCommit(
                operationId,
                logicalDocumentId,
                null,
                documentVersionId,
                repositoryId,
                scanRunId,
                revision,
                content,
                [
                    new SourceAnchorDraft(
                        anchorId,
                        documentVersionId,
                        repositoryId,
                        scanRunId,
                        codeUnitId,
                        revision,
                        path,
                        title,
                        lineStart,
                        lineEnd,
                        WorkerAgentTestFixtures.SourceHash),
                ],
                [],
                [],
                KnowledgeStoreTestFixtures.CreatedAt),
            scope);
        Assert.Equal(KnowledgeCommitStatus.Committed, committed.Status);
        return Assert.IsType<CurrentKnowledge>(await knowledgeStore.GetCurrentAsync(
            logicalDocumentId,
            scope,
            [sectionId]));
    }

    private static async Task<Pass2CurrentFixture> SeedPass2CurrentKnowledgeAsync(
        VulgataDbContext context,
        IKnowledgeStoreRepository knowledgeStore)
    {
        Pass2Fixture fixture = await CreatePass2FixtureAsync(context, knowledgeStore);
        var job = Assert.IsType<Pass2BusinessLogicJobPayload>(fixture.Packet.Payload);
        PreparedPass2Flow flow = PreparedPass2Flow.Create(
            job.PreparedFlowSchemaIdentity,
            job.ManifestId,
            job.FlowId,
            job.SystemId,
            job.RepositoryId,
            [
                new(1, WorkerAgentTestFixtures.Id(101), WorkerAgentTestFixtures.Id(106), WorkerAgentTestFixtures.Id(201)),
                new(2, WorkerAgentTestFixtures.Id(102), WorkerAgentTestFixtures.Id(107), WorkerAgentTestFixtures.Id(202)),
            ],
            job.TraceEvidence);
        var currentDocuments = new List<CurrentKnowledge>();
        for (int index = 0; index < flow.Members.Count; index++)
        {
            currentDocuments.Add(Assert.IsType<CurrentKnowledge>(
                await knowledgeStore.GetCurrentAsync(
                    flow.Members[index].LogicalDocumentId,
                    new RepositoryAccessScope([job.RepositoryId]),
                    [WorkerAgentTestFixtures.Id(501 + index)])));
        }

        return new(flow, currentDocuments);
    }

    private sealed record Pass2Fixture(
        PreparedPass2Flow Flow,
        WorkerAgentJobPacket Packet,
        WorkerAgentResultEnvelope Result,
        IReadOnlyList<Guid> GeneratedFromDocumentVersionIds,
        IReadOnlyList<Guid> SectionIds);

    private sealed record Pass2CurrentFixture(
        PreparedPass2Flow Flow,
        IReadOnlyList<CurrentKnowledge> CurrentDocuments);

    private sealed record CrossRepositoryPass2Fixture(
        PreparedPass2RuntimeBinding Binding,
        WorkerAgentJobPacket Packet,
        WorkerAgentResultEnvelope Result,
        Guid ConsumerRepositoryId,
        Guid ProviderRepositoryId,
        Guid ConsumerScanRunId,
        Guid ProviderScanRunId,
        Guid ConsumerDocumentVersionId,
        Guid ProviderDocumentVersionId);

    private static void SetId<T>(T entity, Guid id)
    {
        PropertyInfo property = typeof(T).GetProperty(nameof(Repository.Id)) ??
            throw new InvalidOperationException("Fixture entity has no Id property.");
        property.SetValue(entity, id);
    }
}
