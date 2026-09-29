using Vulgata.Tests.TestInfrastructure;
using Vulgata.Agents.ChatExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests;

public sealed class ChatCandidateExtractorTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Extract_PublicService_CanBeConstructedAndInvokedByExternalConsumer()
    {
        EvidencePacket packet = CreateSimplePacket(ChatMode.It, "可注册服务保留现有提取行为。");
        var extractor = new ChatCandidateExtractor();

        CandidateClaim candidate = Assert.Single(extractor.Extract(packet));

        Assert.Equal("可注册服务保留现有提取行为。", candidate.Text);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Extract_UnicodeParagraphsAndListItems_PreservesExactOffsetsAndCanonicalSupport()
    {
        const string firstMarkdown = "Cafe\u0301处理😀请求。\r\n\r\n- 后续😀步骤。";
        const string secondMarkdown = "最终步骤。";
        EvidencePacket packet = CreatePacket(
            ChatMode.Business,
            [
                Document("d1", 1, DocumentType.BusinessLogic),
                Document("d2", 2, DocumentType.CodeLogic, Repository2),
            ],
            [
                Section("s2", "d1", 12, 2, DocumentSectionKind.ProcessStep, secondMarkdown, ["a2"], ["d2"]),
                Section("s1", "d1", 11, 1, DocumentSectionKind.Overview, firstMarkdown, ["a2", "a1"], ["d2"]),
            ],
            [Anchor("a2", 22, "d1", 1), Anchor("a1", 21, "d1", 1)],
            [Relationship("r2", "d1", "d2", ChatRelationshipType.GeneratedFrom)]);

        IReadOnlyList<CandidateClaim> candidates = _extractor.Extract(packet);

        Assert.Equal(["k1", "k2", "k3"], candidates.Select(candidate => candidate.CandidateHandle));
        Assert.Equal(["Cafe\u0301处理😀请求。", "后续😀步骤。", secondMarkdown], candidates.Select(candidate => candidate.Text));
        Assert.Equal(0, candidates[0].SpanStart);
        Assert.Equal(firstMarkdown.IndexOf("后续😀步骤。", StringComparison.Ordinal), candidates[1].SpanStart);
        Assert.Equal("Cafe\u0301处理😀请求。".Length, candidates[0].SpanLength);
        Assert.Equal(["a1", "a2"], candidates[0].AnchorHandles);
        Assert.Equal(["r2"], candidates[0].RelationshipHandles);

        CandidateClaim expected = CandidateClaim.Create(
            "k1",
            packet.Fingerprint,
            ChatMode.Business,
            DocumentVersionId(1),
            Id(11),
            0,
            "Cafe\u0301处理😀请求。",
            ["a1", "a2"],
            ["r2"]);
        Assert.Equal(expected.TextSha256, candidates[0].TextSha256);
        Assert.Equal(expected.SupportSignature, candidates[0].SupportSignature);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Extract_SoftWrappedUnicodeParagraphAndListItem_PreservesExactContiguousSpans()
    {
        const string markdown = "第一段😀保留。\r\n  继续 Cafe\u0301，仍属于同一段。\r\n\r\n- 列表😀开始\n  延续 Cafe\u0301，仍属于列表。";
        const string expectedParagraph = "第一段😀保留。\r\n  继续 Cafe\u0301，仍属于同一段。";
        const string expectedListItem = "列表😀开始\n  延续 Cafe\u0301，仍属于列表。";
        EvidencePacket packet = CreateSimplePacket(ChatMode.It, markdown);

        IReadOnlyList<CandidateClaim> candidates = _extractor.Extract(packet);

        Assert.Equal([expectedParagraph, expectedListItem], candidates.Select(candidate => candidate.Text));
        Assert.Equal(markdown.IndexOf(expectedParagraph, StringComparison.Ordinal), candidates[0].SpanStart);
        Assert.Equal(expectedParagraph.Length, candidates[0].SpanLength);
        Assert.Equal(expectedParagraph, markdown.Substring(candidates[0].SpanStart, candidates[0].SpanLength));
        Assert.Equal(markdown.IndexOf(expectedListItem, StringComparison.Ordinal), candidates[1].SpanStart);
        Assert.Equal(expectedListItem.Length, candidates[1].SpanLength);
        Assert.Equal(expectedListItem, markdown.Substring(candidates[1].SpanStart, candidates[1].SpanLength));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Extract_ModeAndSectionKinds_EmitsOnlyEligibleDocumentSections()
    {
        EvidenceDocument businessDocument = Document("d1", 1, DocumentType.BusinessLogic);
        EvidenceDocument codeDocument = Document("d2", 2, DocumentType.CodeLogic, Repository2);
        EvidenceRelationship relationship = Relationship("r1", "d1", "d2", ChatRelationshipType.GeneratedFrom);
        EvidenceAnchor businessAnchor = Anchor("a1", 21, "d1", 1);
        EvidenceAnchor codeAnchor = Anchor("a2", 22, "d2", 2, Repository2);
        EvidenceSection[] sections =
        [
            Section("s1", "d1", 11, 1, DocumentSectionKind.Overview, "业务概览。", ["a1"]),
            Section("s2", "d1", 12, 2, DocumentSectionKind.ProcessStep, "业务步骤。", ["a1"]),
            Section("s3", "d1", 13, 3, DocumentSectionKind.BusinessRule, "业务规则。", ["a1"]),
            Section("s4", "d2", 14, 1, DocumentSectionKind.Overview, "技术概览。", ["a2"]),
            Section("s5", "d2", 15, 2, DocumentSectionKind.ProcessStep, "技术步骤。", ["a2"]),
            Section("s6", "d2", 16, 3, DocumentSectionKind.TechnicalDetail, "技术细节。", ["a2"]),
            Section("s7", "d2", 17, 4, DocumentSectionKind.Evidence, "技术证据。", ["a2"]),
        ];

        EvidencePacket businessPacket = CreatePacket(
            ChatMode.Business,
            [businessDocument, codeDocument],
            sections,
            [businessAnchor, codeAnchor],
            [relationship]);
        EvidencePacket itPacket = CreatePacket(
            ChatMode.It,
            [businessDocument, codeDocument],
            sections,
            [businessAnchor, codeAnchor],
            [relationship]);

        Assert.Equal(
            ["业务概览。", "业务步骤。", "技术概览。", "技术步骤。"],
            _extractor.Extract(businessPacket).Select(candidate => candidate.Text));
        Assert.Equal(
            [false, false, true, true],
            _extractor.Extract(businessPacket).Select(candidate => candidate.AllowsBusinessNarrative));
        Assert.Equal(
            ["技术概览。", "技术步骤。", "技术细节。", "技术证据。"],
            _extractor.Extract(itPacket).Select(candidate => candidate.Text));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Extract_BusinessBareDottedLocator_ExcludesCodeReferenceWithoutRejectingChinesePunctuation()
    {
        const string markdown = "客户提交订单后，系统完成审核。\n\nOrderService.Process 完成处理。\n\nsrc/Orders.cs:42 处理请求。\n\nOrders.cs 第 42 行处理请求。";
        EvidencePacket packet = CreateSimplePacket(ChatMode.Business, markdown);

        CandidateClaim candidate = Assert.Single(_extractor.Extract(packet));

        Assert.Equal("客户提交订单后，系统完成审核。", candidate.Text);
        Assert.DoesNotContain("OrderService.Process", candidate.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(".cs", candidate.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Extract_BusinessCodeLogicFallback_AllowsInlineCodeMarkupForNarrativeRewrite()
    {
        const string markdown = "`CheckLoginPwdOp` 负责完成登录密码校验，并写入 `checkResult` 表示校验结果。";
        EvidencePacket packet = CreatePacket(
            ChatMode.Business,
            [
                Document("d1", 1, DocumentType.CodeLogic),
                Document("d2", 2, DocumentType.CodeLogic, Repository2),
            ],
            [Section("s1", "d1", 11, 1, DocumentSectionKind.Overview, markdown, ["a1"])],
            [Anchor("a1", 21, "d1", 1)],
            [Relationship("r1", "d1", "d2", ChatRelationshipType.References)]);

        CandidateClaim candidate = Assert.Single(_extractor.Extract(packet));

        Assert.Equal(markdown, candidate.Text);
        Assert.True(candidate.AllowsBusinessNarrative);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Extract_BusinessCodeLogicFallback_IncludesBusinessRuleEvidence()
    {
        const string markdown = "密码不匹配时设置 `checkResult` 为 `\"0\"`。";
        EvidencePacket packet = CreatePacket(
            ChatMode.Business,
            [
                Document("d1", 1, DocumentType.CodeLogic),
                Document("d2", 2, DocumentType.CodeLogic, Repository2),
            ],
            [Section("s1", "d1", 11, 1, DocumentSectionKind.BusinessRule, markdown, ["a1"])],
            [Anchor("a1", 21, "d1", 1)],
            [Relationship("r1", "d1", "d2", ChatRelationshipType.References)]);

        CandidateClaim candidate = Assert.Single(_extractor.Extract(packet));

        Assert.Equal(markdown, candidate.Text);
        Assert.True(candidate.AllowsBusinessNarrative);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Extract_NonPlainMarkdown_ExcludesFencesTablesHtmlAndLinks()
    {
        const string markdown = "保留的普通段落。\n\n```csharp\nCall();\n```\n\n| 列一 | 列二 |\n| --- | --- |\n| 值一 | 值二 |\n\n<div>原始 HTML</div>\n\n[链接](https://example.test)\n\nhttps://example.test/raw";
        EvidencePacket packet = CreateSimplePacket(ChatMode.It, markdown);

        CandidateClaim candidate = Assert.Single(_extractor.Extract(packet));

        Assert.Equal("保留的普通段落。", candidate.Text);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Extract_MissingAnchorOrRestrictedRelationshipSupport_ExcludesCandidate()
    {
        EvidencePacket packet = CreatePacket(
            ChatMode.It,
            [
                Document("d1", 1, DocumentType.CodeLogic),
                Document("d2", 2, DocumentType.CodeLogic, Repository2),
                Document("d3", 3, DocumentType.CodeLogic, Repository2),
            ],
            [
                Section("s1", "d1", 11, 1, DocumentSectionKind.Overview, "保留的本地支持。", ["a1"]),
                Section("s2", "d1", 12, 2, DocumentSectionKind.ProcessStep, "保留的固定关系支持。", ["a1"], ["d2"]),
                Section("s3", "d1", 13, 3, DocumentSectionKind.Evidence, "错误文档的锚点。", ["a2"]),
                Section("s4", "d1", 14, 4, DocumentSectionKind.TechnicalDetail, "受限或缺失的关系支持。", ["a1"], ["d3"]),
            ],
            [Anchor("a1", 21, "d1", 1), Anchor("a2", 22, "d2", 2, Repository2)],
            [new EvidenceRelationship(
                "r1",
                Id(401),
                ChatRelationshipType.References,
                "d1",
                "d2",
                1,
                Id(402),
                1,
                new string('d', 64),
                null,
                null)]);

        IReadOnlyList<CandidateClaim> candidates = _extractor.Extract(packet);

        Assert.Equal(["保留的本地支持。", "保留的固定关系支持。"], candidates.Select(candidate => candidate.Text));
        Assert.Empty(candidates[0].RelationshipHandles);
        Assert.Equal(["r1"], candidates[1].RelationshipHandles);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Extract_CrossRepositoryReferences_LegacyProfileFingerprintOnly_IsRejected()
    {
        EvidencePacket packet = CreatePacket(
            ChatMode.It,
            [
                Document("d1", 1, DocumentType.CodeLogic),
                Document("d2", 2, DocumentType.CodeLogic, Repository2),
            ],
            [Section(
                "s1",
                "d1",
                11,
                1,
                DocumentSectionKind.Overview,
                "跨仓库调用需要当前精确决策。",
                ["a1"],
                ["d2"])],
            [Anchor("a1", 21, "d1", 1)],
            [Relationship("r1", "d1", "d2", ChatRelationshipType.References)]);

        Assert.Empty(_extractor.Extract(packet));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Extract_CrossRepositoryReferences_IncompleteV2Authority_IsRejected()
    {
        EvidenceRelationship incompleteAuthority = new(
            "r1",
            Id(401),
            ChatRelationshipType.References,
            "d1",
            "d2",
            1,
            Id(402),
            Id(403),
            new string('c', 64));
        EvidencePacket packet = CreatePacket(
            ChatMode.It,
            [
                Document("d1", 1, DocumentType.CodeLogic),
                Document("d2", 2, DocumentType.CodeLogic, Repository2),
            ],
            [Section(
                "s1",
                "d1",
                11,
                1,
                DocumentSectionKind.Overview,
                "跨仓库调用需要完整决策集合授权。",
                ["a1"],
                ["d2"])],
            [Anchor("a1", 21, "d1", 1)],
            [incompleteAuthority]);

        Assert.Empty(_extractor.Extract(packet));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void EvidenceRelationship_V2Authority_PublicSeam_ExposesDecisionCountAndSetFingerprint()
    {
        Assert.NotNull(typeof(EvidenceRelationship).GetProperty("ExactDecisionCount"));
        Assert.NotNull(typeof(EvidenceRelationship).GetProperty("ExactDecisionSetFingerprint"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(ChatRelationshipType.References)]
    [InlineData(ChatRelationshipType.GeneratedFrom)]
    public void Extract_ReversedSupportingRelationship_ExcludesCandidate(ChatRelationshipType relationshipType)
    {
        EvidencePacket packet = CreatePacket(
            ChatMode.It,
            [Document("d1", 1, DocumentType.CodeLogic), Document("d2", 2, DocumentType.CodeLogic, Repository2)],
            [
                Section("s1", "d1", 11, 1, DocumentSectionKind.Overview, "保留的本地支持。", ["a1"]),
                Section("s2", "d1", 12, 2, DocumentSectionKind.Evidence, "反向关系不能提供支持。", ["a1"], ["d2"]),
            ],
            [Anchor("a1", 21, "d1", 1)],
            [Relationship("r1", "d2", "d1", relationshipType)]);

        CandidateClaim candidate = Assert.Single(_extractor.Extract(packet));

        Assert.Equal("保留的本地支持。", candidate.Text);
        Assert.Empty(candidate.RelationshipHandles);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Extract_OverBoundSpan_ExcludesCandidateWithoutTruncatingOrConcatenating()
    {
        string maximum = new('界', 2_000);
        string overBound = new('界', 2_001);
        EvidencePacket packet = CreatePacket(
            ChatMode.It,
            [Document("d1", 1, DocumentType.CodeLogic), Document("d2", 2, DocumentType.CodeLogic, Repository2)],
            [
                Section("s1", "d1", 11, 1, DocumentSectionKind.Overview, maximum, ["a1"]),
                Section("s2", "d1", 12, 2, DocumentSectionKind.Evidence, overBound, ["a1"]),
            ],
            [Anchor("a1", 21, "d1", 1)],
            [Relationship("r1", "d1", "d2", ChatRelationshipType.References)]);

        CandidateClaim candidate = Assert.Single(_extractor.Extract(packet));

        Assert.Equal(maximum, candidate.Text);
        Assert.Equal(2_000, candidate.SpanLength);
        Assert.NotEqual(overBound, candidate.Text);
    }

    private static EvidencePacket CreateSimplePacket(ChatMode mode, string markdown) =>
        CreatePacket(
            mode,
            [
                Document("d1", 1, mode == ChatMode.Business ? DocumentType.BusinessLogic : DocumentType.CodeLogic),
                Document("d2", 2, DocumentType.CodeLogic, Repository2),
            ],
            [Section("s1", "d1", 11, 1, DocumentSectionKind.Overview, markdown, ["a1"])],
            [Anchor("a1", 21, "d1", 1)],
            [Relationship("r1", "d1", "d2", ChatRelationshipType.References)]);

    private static EvidencePacket CreatePacket(
        ChatMode mode,
        IReadOnlyCollection<EvidenceDocument> documents,
        IReadOnlyCollection<EvidenceSection> sections,
        IReadOnlyCollection<EvidenceAnchor> anchors,
        IReadOnlyCollection<EvidenceRelationship> relationships) =>
        EvidencePacket.Create(
            "request-1",
            mode,
            SystemId,
            [Repository2, Repository1],
            documents,
            sections,
            anchors,
            relationships,
            []);

    private static EvidenceDocument Document(
        string handle,
        int id,
        DocumentType documentType,
        Guid? repositoryId = null) =>
        new(
            handle,
            Id(100 + id),
            DocumentVersionId(id),
            repositoryId ?? Repository1,
            documentType,
            $"文档 {handle}",
            new string((char)('a' + id), 40));

    private static EvidenceSection Section(
        string handle,
        string documentHandle,
        int stableId,
        int order,
        DocumentSectionKind kind,
        string markdown,
        IReadOnlyList<string> anchors,
        IReadOnlyList<string>? supportingDocuments = null) =>
        new(
            handle,
            documentHandle,
            Id(stableId),
            order,
            kind,
            $"章节 {handle}",
            markdown,
            anchors,
            supportingDocuments ?? []);

    private static EvidenceAnchor Anchor(
        string handle,
        int anchorId,
        string documentHandle,
        int documentId,
        Guid? repositoryId = null) =>
        new(
            handle,
            Id(anchorId),
            documentHandle,
            DocumentVersionId(documentId),
            repositoryId ?? Repository1,
            $"src/{documentHandle}.cs",
            10,
            12,
            new string('b', 64));

    private static EvidenceRelationship Relationship(
        string handle,
        string sourceDocumentHandle,
        string targetDocumentHandle,
        ChatRelationshipType relationshipType) =>
        new(
            handle,
            $"edge-{handle}",
            relationshipType,
            sourceDocumentHandle,
            targetDocumentHandle,
            relationshipType == ChatRelationshipType.References ? new string('c', 64) : null);

    private static Guid DocumentVersionId(int value) => Id(1_000 + value);

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private static readonly Guid SystemId = Id(1);
    private static readonly Guid Repository1 = Id(2);
    private static readonly Guid Repository2 = Id(3);
    private readonly ChatCandidateExtractor _extractor = new();
}
