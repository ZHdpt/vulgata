using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests;

public sealed class WorkerAgentContractTests
{
    [Fact]
    public void Pass1Contracts_CreateMatchingTypedAggregate_ReturnsClosedVariants()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        Pass1CodeLogicDocumentResultPayload result =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(packet);
        DateTimeOffset dispatchedAt = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        WorkerAgentResultEnvelope envelope = WorkerAgentResultEnvelope.CreateForJob(
            packet,
            WorkerAgentResultOutcome.Valid,
            boundedReason: null,
            dispatchedAt,
            dispatchedAt.AddSeconds(1),
            result);

        Assert.IsType<Pass1CodeLogicJobPayload>(packet.Payload);
        Assert.Same(result, Assert.IsType<Pass1CodeLogicDocumentResultPayload>(envelope.Payload));
        Assert.Equal(packet.JobId, result.JobId);
        Assert.Equal(packet.SystemId, result.SystemId);
        Assert.Equal(packet.RepositoryId, result.RepositoryId);
        Assert.Equal(packet.ScanRunId, result.ScanRunId);
        Assert.Equal(packet.ProfiledRevision, result.ProfiledRevision);
    }

    [Fact]
    public void Pass1JobPayload_NonCanonicalReferenceAllowlist_RejectsValue()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentTestFixtures.CreatePass1JobPayload(
                allowedReferenceDocumentVersionIds:
                [
                    WorkerAgentTestFixtures.Id(9),
                    WorkerAgentTestFixtures.Id(9),
                ]));

        Assert.Throws<ArgumentException>(() =>
            WorkerAgentTestFixtures.CreatePass1JobPayload(
                allowedReferenceDocumentVersionIds:
                [
                    WorkerAgentTestFixtures.Id(12),
                    WorkerAgentTestFixtures.Id(9),
                ]));
    }

    [Fact]
    public void Pass1JobPayload_NonCanonicalContextCollections_RejectsValue()
    {
        WorkerAgentStructuralContext context = WorkerAgentStructuralContext.Create(
            WorkerAgentTestFixtures.Id(7),
            "src/Orders/Order.cs",
            "Order");
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentTestFixtures.CreatePass1JobPayload(
                neighboringStructuralContext: [context, context]));

        Assert.Throws<ArgumentException>(() =>
            WorkerAgentTestFixtures.CreatePass1JobPayload(
                approvedCommunicationEvidence:
                [
                    WorkerAgentCommunicationEvidence.Create(
                        WorkerAgentTestFixtures.Id(12),
                        "HTTP",
                        "订单服务调用支付接口。"),
                    WorkerAgentCommunicationEvidence.Create(
                        WorkerAgentTestFixtures.Id(8),
                        "HTTP",
                        "订单服务调用库存接口。"),
                ]));
    }

    [Fact]
    public void Pass1JobPayload_SourceSliceExceedsNamedLineBound_RejectsValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Pass1CodeLogicJobPayload.Create(
                codeUnitId: WorkerAgentTestFixtures.Id(6),
                repositoryRelativePath: WorkerAgentTestFixtures.RepositoryRelativePath,
                lineStart: 1,
                lineEnd: WorkerAgentContractLimits.MaximumSourceSliceLines + 1,
                symbolName: "CalculateTotal",
                unitType: Vulgata.Core.Entities.CodeUnitType.Method,
                language: "csharp",
                sourceHash: WorkerAgentTestFixtures.SourceHash,
                fileIdentityHash: WorkerAgentTestFixtures.FileIdentityHash,
                neighboringStructuralContext: [],
                approvedCommunicationEvidence: [],
                allowedReferenceDocumentVersionIds: []));
    }

    [Fact]
    public void Pass1Result_ReferenceRequestsAreNotCanonicalOrAllowlisted_RejectsValue()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        DateTimeOffset now = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        Pass1CodeLogicDocumentResultPayload duplicate =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(
                packet,
                referenceDocumentVersionIds:
                [
                    WorkerAgentTestFixtures.Id(9),
                    WorkerAgentTestFixtures.Id(9),
                ]);
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.Valid,
                null,
                now,
                now,
                duplicate));

        Pass1CodeLogicDocumentResultPayload outsideAllowlist =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(
                packet,
                referenceDocumentVersionIds: [WorkerAgentTestFixtures.Id(99)]);
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.Valid,
                null,
                now,
                now,
                outsideAllowlist));
    }

    [Fact]
    public void Pass1Result_AnchorsAndCommunicationEvidenceAreNotCanonicalOrAuthorized_RejectsValue()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        var pass1 = Assert.IsType<Pass1CodeLogicJobPayload>(packet.Payload);
        DateTimeOffset now = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        WorkerAgentSourceAnchor anchor = WorkerAgentSourceAnchor.Create(
            WorkerAgentTestFixtures.Id(11),
            pass1.RepositoryRelativePath,
            "OrderService.CalculateTotal",
            pass1.LineStart,
            pass1.LineEnd,
            pass1.SourceHash);

        Pass1CodeLogicDocumentResultPayload duplicateAnchors =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(
                packet,
                sourceAnchors: [anchor, anchor]);
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.Valid,
                null,
                now,
                now,
                duplicateAnchors));

        Pass1CodeLogicDocumentResultPayload outsideEvidenceAllowlist =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(
                packet,
                communicationEvidenceIds: [WorkerAgentTestFixtures.Id(99)]);
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.Valid,
                null,
                now,
                now,
                outsideEvidenceAllowlist));
    }

    [Fact]
    public void Pass1Result_PreparedCommunicationEvidenceIsOmitted_RejectsValue()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        Pass1CodeLogicDocumentResultPayload omitted =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(
                packet,
                communicationEvidenceIds: []);
        DateTimeOffset now = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() =>
            WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.Valid,
                null,
                now,
                now,
                omitted));
    }

    [Fact]
    public void Pass1Result_ContentIsNotCodeLogic_RejectsValue()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        Pass1CodeLogicDocumentResultPayload baseline =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(packet);
        var businessContent = baseline.Content with
        {
            DocumentType = Vulgata.Core.Entities.DocumentType.BusinessLogic,
        };
        Pass1CodeLogicDocumentResultPayload invalid =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(
                packet,
                content: businessContent);
        DateTimeOffset now = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() =>
            WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.Valid,
                null,
                now,
                now,
                invalid));
    }

    [Fact]
    public void Pass1Result_ContentCollectionsAreNotCanonical_RejectsValue()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        Pass1CodeLogicDocumentResultPayload baseline =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(packet);
        var nonCanonicalContent = baseline.Content with
        {
            KeySymbols = ["Zeta", "Alpha"],
        };
        Pass1CodeLogicDocumentResultPayload invalid =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(
                packet,
                content: nonCanonicalContent);
        DateTimeOffset now = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() =>
            WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.Valid,
                null,
                now,
                now,
                invalid));
    }

    [Fact]
    public void Pass1Result_SectionMarkdownUsesNormalizedLfLayout_AcceptsValue()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        Pass1CodeLogicDocumentResultPayload baseline =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(packet);
        CanonicalSection section = baseline.Content.Sections.Single() with
        {
            Markdown = "第一行。\n第二行。",
        };
        Pass1CodeLogicDocumentResultPayload payload =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(
                packet,
                content: baseline.Content with { Sections = [section] });
        DateTimeOffset now = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        WorkerAgentResultEnvelope envelope = WorkerAgentResultEnvelope.CreateForJob(
            packet,
            WorkerAgentResultOutcome.Valid,
            null,
            now,
            now,
            payload);

        Assert.Same(payload, envelope.Payload);
    }

    [Fact]
    public void Pass1Result_HasNoSourceGrounding_RejectsValue()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        Pass1CodeLogicDocumentResultPayload baseline =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(packet);
        CanonicalSection section = baseline.Content.Sections.Single() with
        {
            SourceAnchorIds = [],
        };
        Pass1CodeLogicDocumentResultPayload payload =
            WorkerAgentTestFixtures.CreatePass1ResultPayload(
                packet,
                content: baseline.Content with { Sections = [section] },
                sourceAnchors: []);
        DateTimeOffset now = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => WorkerAgentResultEnvelope.CreateForJob(
            packet,
            WorkerAgentResultOutcome.Valid,
            null,
            now,
            now,
            payload));
    }

    [Fact]
    public void Pass1JobPayload_ContextContainsHostPathOrSecret_RejectsValue()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentStructuralContext.Create(
                WorkerAgentTestFixtures.Id(7),
                "C:/source/Order.cs",
                "Order"));

        Assert.Throws<ArgumentException>(() =>
            WorkerAgentCommunicationEvidence.Create(
                WorkerAgentTestFixtures.Id(8),
                "HTTP",
                "password=secret"));
    }

    [Fact]
    public void Pass2Contracts_ExactPreparedFlow_ReturnsSecondAndOnlyClosedVariant()
    {
        PreparedPass2Flow flow = CreatePreparedPass2Flow();
        CurrentKnowledge[] currentCodeLogicDocuments =
        [
            CreateCurrentCodeLogicDocument(flow.Members[0], 301, 401),
            CreateCurrentCodeLogicDocument(flow.Members[1], 302, 402),
        ];
        Pass2BusinessLogicJobPayload jobPayload =
            Pass2BusinessLogicJobPayload.Create(flow, currentCodeLogicDocuments);
        WorkerAgentJobPacket packet = WorkerAgentJobPacket.Create(
            jobId: WorkerAgentTestFixtures.Id(501),
            kind: WorkerAgentJobKind.Pass2BusinessLogicDocument,
            scanExecutionId: WorkerAgentTestFixtures.Id(2),
            repositoryId: WorkerAgentTestFixtures.Id(3),
            systemId: WorkerAgentTestFixtures.Id(4),
            scanRunId: WorkerAgentTestFixtures.Id(5),
            profiledRevision: WorkerAgentTestFixtures.ProfiledRevision,
            schemaVersion: WorkerAgentContractLimits.SupportedSchemaVersion,
            payload: jobPayload);
        CanonicalDocumentContent content = CreateBusinessLogicContent();
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
                content,
                currentCodeLogicDocuments.SelectMany(document => document.SourceAnchors)
                    .Select(anchor => WorkerAgentSourceAnchor.Create(
                        anchor.Id,
                        anchor.RepositoryRelativePath,
                        anchor.EvidenceLocator,
                        anchor.LineStart,
                        anchor.LineEnd,
                        anchor.SourceHash))
                    .ToArray(),
                [1, 2],
                [WorkerAgentTestFixtures.Id(301), WorkerAgentTestFixtures.Id(302)]);
        DateTimeOffset now = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        WorkerAgentResultEnvelope envelope = WorkerAgentResultEnvelope.CreateForJob(
            packet,
            WorkerAgentResultOutcome.Valid,
            null,
            now,
            now.AddSeconds(1),
            resultPayload);

        Assert.Equal(2, Enum.GetValues<WorkerAgentJobKind>().Length);
        Assert.Same(jobPayload, Assert.IsType<Pass2BusinessLogicJobPayload>(packet.Payload));
        Assert.Same(
            resultPayload,
            Assert.IsType<Pass2BusinessLogicDocumentResultPayload>(envelope.Payload));
        Assert.Equal([1, 2], resultPayload.MainSteps.Select(step => step.Order));
        Assert.Equal([1, 2], resultPayload.TraceEvidenceOrders);
        Assert.Equal(
            [WorkerAgentTestFixtures.Id(301), WorkerAgentTestFixtures.Id(302)],
            resultPayload.GeneratedFromDocumentVersionIds);
        Assert.DoesNotContain(
            typeof(Pass2BusinessLogicDocumentResultPayload).GetProperties(),
            property => property.Name.Contains("Reference", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Pass2Result_MainStepsOmitPreparedEvidence_RejectsValue()
    {
        PreparedPass2Flow flow = CreatePreparedPass2Flow();
        CurrentKnowledge[] documents =
        [
            CreateCurrentCodeLogicDocument(flow.Members[0], 301, 401),
            CreateCurrentCodeLogicDocument(flow.Members[1], 302, 402),
        ];
        WorkerAgentJobPacket packet = WorkerAgentJobPacket.Create(
            WorkerAgentTestFixtures.Id(501),
            WorkerAgentJobKind.Pass2BusinessLogicDocument,
            WorkerAgentTestFixtures.Id(2),
            WorkerAgentTestFixtures.Id(3),
            WorkerAgentTestFixtures.Id(4),
            WorkerAgentTestFixtures.Id(5),
            WorkerAgentTestFixtures.ProfiledRevision,
            WorkerAgentContractLimits.SupportedSchemaVersion,
            Pass2BusinessLogicJobPayload.Create(flow, documents));
        Pass2BusinessLogicDocumentResultPayload payload =
            Pass2BusinessLogicDocumentResultPayload.Create(
                packet.JobId,
                packet.SystemId,
                packet.RepositoryId,
                packet.ScanRunId,
                packet.ProfiledRevision,
                flow.FlowId,
                "帮助业务人员理解订单从计价到库存预留的处理过程。",
                [WorkerAgentBusinessMainStep.Create(1, "只解释订单总额计算。", [1])],
                "订单完成计价并成功预留库存。",
                CreateBusinessLogicContent(),
                documents.SelectMany(document => document.SourceAnchors)
                    .Select(anchor => WorkerAgentSourceAnchor.Create(
                        anchor.Id,
                        anchor.RepositoryRelativePath,
                        anchor.EvidenceLocator,
                        anchor.LineStart,
                        anchor.LineEnd,
                        anchor.SourceHash))
                    .ToArray(),
                [1, 2],
                [WorkerAgentTestFixtures.Id(301), WorkerAgentTestFixtures.Id(302)]);
        DateTimeOffset now = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => WorkerAgentResultEnvelope.CreateForJob(
            packet,
            WorkerAgentResultOutcome.Valid,
            null,
            now,
            now,
            payload));
    }

    private static PreparedPass2Flow CreatePreparedPass2Flow() =>
        PreparedPass2Flow.Create(
            PreparedPass2FlowLimits.SupportedSchemaIdentity,
            WorkerAgentTestFixtures.Id(200),
            WorkerAgentTestFixtures.Id(201),
            WorkerAgentTestFixtures.Id(4),
            WorkerAgentTestFixtures.Id(3),
            [
                new(1, WorkerAgentTestFixtures.Id(1), WorkerAgentTestFixtures.Id(6), WorkerAgentTestFixtures.Id(101)),
                new(2, WorkerAgentTestFixtures.Id(10), WorkerAgentTestFixtures.Id(11), WorkerAgentTestFixtures.Id(102)),
            ],
            [
                new(1, WorkerAgentTestFixtures.Id(1), WorkerAgentTestFixtures.Id(6), "订单总额计算", "src/Orders/OrderService.cs#L12-L24"),
                new(2, WorkerAgentTestFixtures.Id(10), WorkerAgentTestFixtures.Id(11), "库存预留", "src/Inventory/InventoryService.cs#L30-L44"),
            ]);

    private static CurrentKnowledge CreateCurrentCodeLogicDocument(
        PreparedPass2FlowMember member,
        int documentVersionId,
        int anchorId) =>
        new(
            member.LogicalDocumentId,
            WorkerAgentTestFixtures.Id(documentVersionId),
            WorkerAgentTestFixtures.Id(3),
            Vulgata.Core.Entities.DocumentType.CodeLogic,
            $"Code Logic {member.Order}",
            $"src/Demo/Flow{member.Order}.cs",
            "{}",
            $"Code Logic {member.Order}",
            [new CurrentSourceAnchor(
                WorkerAgentTestFixtures.Id(anchorId),
                member.CodeUnitId,
                $"src/Demo/Flow{member.Order}.cs",
                $"Flow{member.Order}.Execute",
                1,
                2,
                new string('a', 64))],
            [],
            [],
            new CurrentIndexEntry($"Summary {member.Order}", [], [], [], $"flow {member.Order}"),
            []);

    private static CanonicalDocumentContent CreateBusinessLogicContent() =>
        new(
            KnowledgeDocumentLimits.SchemaIdentity,
            Vulgata.Core.Entities.DocumentType.BusinessLogic,
            "orders.price-and-reserve",
            "订单计价与库存预留",
            "flows/orders-price-and-reserve",
            "订单完成计价后预留库存。",
            [],
            ["库存", "订单"],
            [],
            [
                new CanonicalSection(
                    WorkerAgentTestFixtures.Id(601),
                    1,
                    Vulgata.Core.Entities.DocumentSectionKind.Overview,
                    "业务目的",
                    "帮助业务人员理解订单计价与库存预留。",
                    "订单 计价 库存 预留",
                    [WorkerAgentTestFixtures.Id(401)],
                    [WorkerAgentTestFixtures.Id(301)]),
                new CanonicalSection(
                    WorkerAgentTestFixtures.Id(602),
                    2,
                    Vulgata.Core.Entities.DocumentSectionKind.ProcessStep,
                    "主要步骤",
                    "先计算订单总额，再预留对应库存。",
                    "订单 总额 库存",
                    [WorkerAgentTestFixtures.Id(401), WorkerAgentTestFixtures.Id(402)],
                    [WorkerAgentTestFixtures.Id(301), WorkerAgentTestFixtures.Id(302)]),
                new CanonicalSection(
                    WorkerAgentTestFixtures.Id(603),
                    3,
                    Vulgata.Core.Entities.DocumentSectionKind.Overview,
                    "业务结果",
                    "订单完成计价并成功预留库存。",
                    "订单 结果 库存",
                    [WorkerAgentTestFixtures.Id(402)],
                    [WorkerAgentTestFixtures.Id(302)]),
            ]);
}
