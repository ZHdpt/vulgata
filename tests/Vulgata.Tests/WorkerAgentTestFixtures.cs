using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests;

internal static class WorkerAgentTestFixtures
{
    internal const string ProfiledRevision = "0123456789abcdef0123456789abcdef01234567";
    internal const string SourceHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    internal const string FileIdentityHash = "a3f15bb08b2bd1684e84c4b2e2d673282c0e31f27a6a2e56aeb894ecff0c10f4";
    internal const string RepositoryRelativePath = "src/Orders/OrderService.cs";

    internal static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    internal static Pass1CodeLogicJobPayload CreatePass1JobPayload(
        IReadOnlyList<WorkerAgentStructuralContext>? neighboringStructuralContext = null,
        IReadOnlyList<WorkerAgentCommunicationEvidence>? approvedCommunicationEvidence = null,
        IReadOnlyList<Guid>? allowedReferenceDocumentVersionIds = null,
        IReadOnlyList<WorkerAgentPreparedSourceAnchor>? preparedSourceAnchors = null) =>
        Pass1CodeLogicJobPayload.Create(
            codeUnitId: Id(6),
            repositoryRelativePath: RepositoryRelativePath,
            lineStart: 12,
            lineEnd: 24,
            symbolName: "CalculateTotal",
            unitType: CodeUnitType.Method,
            language: "csharp",
            sourceHash: SourceHash,
            fileIdentityHash: FileIdentityHash,
            neighboringStructuralContext: neighboringStructuralContext ??
            [
                WorkerAgentStructuralContext.Create(
                    Id(7),
                    "src/Orders/Order.cs",
                    "Order"),
            ],
            approvedCommunicationEvidence: approvedCommunicationEvidence ??
            [
                WorkerAgentCommunicationEvidence.Create(
                    Id(8),
                    "HTTP",
                    "订单服务调用库存接口。"),
            ],
            allowedReferenceDocumentVersionIds:
                allowedReferenceDocumentVersionIds ?? [Id(9)],
            preparedSourceAnchors: preparedSourceAnchors);

    internal static WorkerAgentJobPacket CreatePass1Packet(
        Pass1CodeLogicJobPayload? payload = null) =>
        WorkerAgentJobPacket.Create(
            jobId: Id(1),
            kind: WorkerAgentJobKind.Pass1CodeLogicDocument,
            scanExecutionId: Id(2),
            repositoryId: Id(3),
            systemId: Id(4),
            scanRunId: Id(5),
            profiledRevision: ProfiledRevision,
            schemaVersion: WorkerAgentContractLimits.SupportedSchemaVersion,
            payload: payload ?? CreatePass1JobPayload());

    internal static Pass1CodeLogicDocumentResultPayload CreatePass1ResultPayload(
        WorkerAgentJobPacket packet,
        Guid? jobId = null,
        Guid? systemId = null,
        Guid? repositoryId = null,
        Guid? scanRunId = null,
        Guid? codeUnitId = null,
        string? profiledRevision = null,
        string? repositoryRelativePath = null,
        int? lineStart = null,
        int? lineEnd = null,
        string? sourceHash = null,
        string? fileIdentityHash = null,
        CanonicalDocumentContent? content = null,
        IReadOnlyList<WorkerAgentSourceAnchor>? sourceAnchors = null,
        IReadOnlyList<Guid>? referenceDocumentVersionIds = null,
        IReadOnlyList<Guid>? communicationEvidenceIds = null)
    {
        var pass1 = Assert.IsType<Pass1CodeLogicJobPayload>(packet.Payload);
        Guid defaultAnchorId = pass1.PreparedSourceAnchors.Count > 0
            ? pass1.PreparedSourceAnchors[0].Id
            : Id(11);
        CanonicalDocumentContent defaultContent = new(
            KnowledgeDocumentLimits.SchemaIdentity,
            DocumentType.CodeLogic,
            "orders.calculate-total",
            "订单总额计算",
            pass1.RepositoryRelativePath,
            "根据订单行计算订单总额。",
            ["CalculateTotal"],
            ["订单总额"],
            ["OrderService"],
            [
                new CanonicalSection(
                    Id(10),
                    1,
                    DocumentSectionKind.TechnicalDetail,
                    "计算逻辑",
                    "汇总订单行金额。",
                    "订单 总额 计算",
                    [defaultAnchorId],
                    [Id(9)]),
            ]);

        return Pass1CodeLogicDocumentResultPayload.Create(
            jobId: jobId ?? packet.JobId,
            systemId: systemId ?? packet.SystemId,
            repositoryId: repositoryId ?? packet.RepositoryId,
            scanRunId: scanRunId ?? packet.ScanRunId,
            codeUnitId: codeUnitId ?? pass1.CodeUnitId,
            profiledRevision: profiledRevision ?? packet.ProfiledRevision,
            repositoryRelativePath: repositoryRelativePath ?? pass1.RepositoryRelativePath,
            lineStart: lineStart ?? pass1.LineStart,
            lineEnd: lineEnd ?? pass1.LineEnd,
            sourceHash: sourceHash ?? pass1.SourceHash,
            fileIdentityHash: fileIdentityHash ?? pass1.FileIdentityHash,
            content: content ?? defaultContent,
            sourceAnchors: sourceAnchors ??
            [
                WorkerAgentSourceAnchor.Create(
                    defaultAnchorId,
                    pass1.RepositoryRelativePath,
                    "OrderService.CalculateTotal",
                    pass1.LineStart,
                    pass1.LineEnd,
                    pass1.SourceHash),
            ],
            referenceDocumentVersionIds: referenceDocumentVersionIds ?? [Id(9)],
            communicationEvidenceIds: communicationEvidenceIds ?? [Id(8)]);
    }
}
