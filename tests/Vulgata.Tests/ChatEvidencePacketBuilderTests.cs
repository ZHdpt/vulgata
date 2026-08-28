using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class ChatEvidencePacketBuilderTests
{
    [Fact]
    public void Create_V2Packet_UsesGroundedChatSchemaAndRetainsOpaqueEvidenceHandles()
    {
        EvidencePacket packet = CreatePacket();

        Assert.Equal("grounded-chat-evidence/2.0", packet.SchemaVersion);
        Assert.All(packet.Documents, document => Assert.Matches("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", document.DocumentHandle));
        Assert.All(packet.Sections, section =>
        {
            Assert.Matches("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", section.SectionHandle);
            Assert.NotEmpty(section.AnchorHandles);
        });
        Assert.All(packet.SourceAnchors, anchor =>
        {
            Assert.Matches("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", anchor.AnchorHandle);
            Assert.True(anchor.LineStart > 0);
            Assert.True(anchor.LineEnd >= anchor.LineStart);
        });
        Assert.All(packet.Relationships, relationship =>
            Assert.Matches("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", relationship.RelationshipHandle));
        Assert.All(packet.FlowEvidence, step => Assert.True(step.StepOrder > 0));
    }

    [Fact]
    public void Create_V2Packet_CanonicalFingerprint_IsStableAcrossInputOrdering()
    {
        EvidencePacket first = CreatePacket(reverseCollections: false);
        EvidencePacket second = CreatePacket(reverseCollections: true);

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Matches("^[0-9a-f]{64}$", first.Fingerprint);
    }

    [Fact]
    public void Create_V2Packet_CanonicalFingerprint_BindsDocumentTitle()
    {
        EvidencePacket original = CreatePacket();
        EvidencePacket changed = CreatePacket(document1Title: "改名后的订单处理");

        Assert.NotEqual(original.Fingerprint, changed.Fingerprint);
    }

    [Fact]
    public void Create_V2Packet_CanonicalFingerprint_BindsSectionHeading()
    {
        EvidencePacket original = CreatePacket();
        EvidencePacket changed = CreatePacket(section1Heading: "改名后的业务规则");

        Assert.NotEqual(original.Fingerprint, changed.Fingerprint);
    }

    [Fact]
    public void Create_V2Packet_CanonicalFingerprint_BindsFlowStepDescription()
    {
        EvidencePacket original = CreatePacket();
        EvidencePacket changed = CreatePacket(flowDescription: "改名后的订单处理流程");

        Assert.NotEqual(original.Fingerprint, changed.Fingerprint);
    }

    [Fact]
    public void Create_V2Packet_CanonicalFingerprint_BindsRelationshipIdentity()
    {
        EvidencePacket original = CreatePacket();
        EvidencePacket changed = CreatePacket(relationshipIdentity: new string('d', 64));

        Assert.NotEqual(original.Fingerprint, changed.Fingerprint);
    }

    [Fact]
    public void Create_V2Packet_RejectsEvidenceBodyBeyondUtf8ByteBound()
    {
        ChatContractException exception = Assert.Throws<ChatContractException>(() =>
            CreatePacket(new string('中', 22_000)));

        Assert.Equal(ChatContractFailureCode.InvalidEvidence, exception.FailureCode);
    }

    [Fact]
    public void Create_V2Packet_DoesNotExposeRawGraphOrProviderAccess()
    {
        EvidencePacket packet = CreatePacket();
        string serialized = JsonSerializer.Serialize(packet);

        Assert.DoesNotContain("DbContext", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Provider", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Graph", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_V2Packet_AdvisoryContext_ExactUtf8BoundSucceedsAndPlusOneFails()
    {
        ChatEvidencePacketBuildRequest exactRequest = CreateBuildRequest(
            advisoryContext: new string('a', 8_192));

        EvidencePacket exactPacket = new ChatEvidencePacketBuilder().Build(exactRequest);

        Assert.NotNull(exactPacket);
        ChatContractException exception = Assert.Throws<ChatContractException>(() =>
            new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
                advisoryContext: new string('a', 8_193))));
        Assert.Equal(ChatContractFailureCode.InvalidEvidence, exception.FailureCode);
    }

    [Fact]
    public void Build_V2Packet_CanonicalFingerprint_BindsAdvisoryContext()
    {
        EvidencePacket baseline = new ChatEvidencePacketBuilder().Build(CreateBuildRequest());
        EvidencePacket changed = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            advisoryContext: "仅限当前发布版本。"));

        Assert.NotEqual(baseline.Fingerprint, changed.Fingerprint);
    }

    [Fact]
    public void Build_V2Packet_CanonicalFingerprint_BindsConversationState()
    {
        EvidencePacket baseline = new ChatEvidencePacketBuilder().Build(CreateBuildRequest());
        ChatConversationScope scope = ChatConversationScope.Create(
            ChatMode.Business,
            Id(1),
            [Id(2), Id(3)]);
        ChatConversationState conversation = ChatConversationState.Empty(scope).AppendValidatedTurn(
            ValidatedConversationTurn.Create(
                "上一问",
                "上一答",
                baseline.Fingerprint,
                ["a1"],
                [Id(10)]));

        EvidencePacket changed = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            conversation: conversation));

        Assert.NotEqual(baseline.Fingerprint, changed.Fingerprint);
    }

    [Fact]
    public void Build_V2Packet_OverBound_DropsOptionalSectionAsWholeUnitWithUnusedSupport()
    {
        EvidencePacket packet = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            includeOversizedOptionalSection: true,
            optionalSectionHandles: ["s3"]));

        Assert.DoesNotContain(packet.Sections, section => section.SectionHandle == "s3");
        Assert.DoesNotContain(packet.SourceAnchors, anchor => anchor.AnchorHandle == "a3");
        Assert.DoesNotContain(packet.Documents, document => document.DocumentHandle == "d3");
        EvidenceSection required = Assert.Single(packet.Sections, section => section.SectionHandle == "s1");
        Assert.Equal("处理订单请求。", required.ContentMarkdown);
    }

    [Fact]
    public void Build_V2Packet_OverBoundRequiredEvidence_RejectsWithoutTextTruncation()
    {
        ChatContractException exception = Assert.Throws<ChatContractException>(() =>
            new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
                sectionContent: new string('中', 22_000))));

        Assert.Equal(ChatContractFailureCode.InvalidEvidence, exception.FailureCode);
    }

    [Fact]
    public void Build_V2Packet_CanonicalFingerprint_BindsFlowGroupAuthority()
    {
        EvidencePacket baseline = new ChatEvidencePacketBuilder().Build(CreateBuildRequest());
        EvidencePacket changed = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            flowGroups: [CreateFlowGroupAuthority()]));

        Assert.NotEqual(baseline.Fingerprint, changed.Fingerprint);
    }

    private static ChatEvidencePacketBuildRequest CreateBuildRequest(
        string sectionContent = "处理订单请求。",
        string advisoryContext = "",
        ChatConversationState? conversation = null,
        bool includeOversizedOptionalSection = false,
        IReadOnlyCollection<string>? optionalSectionHandles = null,
        IReadOnlyCollection<EvidenceFlowGroupAuthority>? flowGroups = null)
    {
        Guid systemId = Id(1);
        Guid repository1 = Id(2);
        Guid repository2 = Id(3);
        var document1 = new EvidenceDocument(
            "d1", Id(10), Id(11), repository1, DocumentType.BusinessLogic, "订单处理", "abcdef1");
        var document2 = new EvidenceDocument(
            "d2", Id(12), Id(13), repository2, DocumentType.CodeLogic, "订单服务", "abcdef2");
        var documents = new List<EvidenceDocument> { document1, document2 };
        var section1 = new EvidenceSection(
            "s1", "d1", Id(20), 1, DocumentSectionKind.BusinessRule, "业务规则", sectionContent, ["a1"], ["d2"]);
        var section2 = new EvidenceSection(
            "s2", "d2", Id(21), 1, DocumentSectionKind.Overview, "概览", "处理订单。", ["a2"], []);
        var sections = new List<EvidenceSection> { section1, section2 };
        var anchor1 = new EvidenceAnchor(
            "a1", Id(30), "d1", Id(11), repository1, "src/Orders.cs", 10, 12, new string('a', 64));
        var anchor2 = new EvidenceAnchor(
            "a2", Id(31), "d2", Id(13), repository2, "src/OrderService.cs", 20, 22, new string('b', 64));
        var anchors = new List<EvidenceAnchor> { anchor1, anchor2 };
        if (includeOversizedOptionalSection)
        {
            documents.Add(new EvidenceDocument(
                "d3", Id(14), Id(15), repository1, DocumentType.CodeLogic, "可选支持", "abcdef3"));
            sections.Add(new EvidenceSection(
                "s3", "d3", Id(22), 1, DocumentSectionKind.TechnicalDetail, "可选详情", new string('x', 70_000), ["a3"], []));
            anchors.Add(new EvidenceAnchor(
                "a3", Id(32), "d3", Id(15), repository1, "src/Optional.cs", 30, 32, new string('e', 64)));
        }

        return ChatEvidencePacketBuildRequest.Create(
            "request-1",
            ChatMode.Business,
            systemId,
            [repository1, repository2],
            documents,
            sections,
            anchors,
            [new EvidenceRelationship(
                "r1", "edge-1", ChatRelationshipType.References, "d1", "d2", new string('c', 64))],
            [new EvidenceFlowStep(1, "d1", "r1", "订单处理流程")],
            flowGroups ?? [],
            advisoryContext,
            conversation ?? ChatConversationState.Empty(ChatConversationScope.Create(
                ChatMode.Business,
                systemId,
                [repository1, repository2])),
            optionalSectionHandles ?? []);
    }

    private static EvidenceFlowGroupAuthority CreateFlowGroupAuthority() =>
        new(
            Id(90),
            "endpoint-flow-group/1.0",
            "Published",
            Id(11),
            Id(91),
            Id(92),
            new string('d', 64),
            new string('e', 64),
            Id(93),
            1,
            [new EvidenceFlowStepAuthority(
                Id(94),
                1,
                FlowTraceStepKind.Document,
                "订单处理流程",
                Id(11),
                Id(2),
                Id(95),
                null,
                null)]);

    private static EvidencePacket CreatePacket(
        string sectionContent = "处理订单请求。",
        bool reverseCollections = false,
        string document1Title = "订单处理",
        string section1Heading = "业务规则",
        string flowDescription = "订单处理流程",
        string relationshipIdentity = "edge-1")
    {
        Guid systemId = Id(1);
        Guid repository1 = Id(2);
        Guid repository2 = Id(3);
        var document1 = new EvidenceDocument(
            "d1", Id(10), Id(11), repository1, DocumentType.BusinessLogic, document1Title, "abcdef1");
        var document2 = new EvidenceDocument(
            "d2", Id(12), Id(13), repository2, DocumentType.CodeLogic, "订单服务", "abcdef2");
        var section1 = new EvidenceSection(
            "s1", "d1", Id(20), 1, DocumentSectionKind.BusinessRule, section1Heading, sectionContent, ["a1"], ["d2"]);
        var section2 = new EvidenceSection(
            "s2", "d2", Id(21), 1, DocumentSectionKind.Overview, "概览", "处理订单。", ["a2"], []);
        var anchor1 = new EvidenceAnchor(
            "a1", Id(30), "d1", Id(11), repository1, "src/Orders.cs", 10, 12, new string('a', 64));
        var anchor2 = new EvidenceAnchor(
            "a2", Id(31), "d2", Id(13), repository2, "src/OrderService.cs", 20, 22, new string('b', 64));
        var relationship = new EvidenceRelationship(
            "r1", relationshipIdentity, ChatRelationshipType.References, "d1", "d2", new string('c', 64));
        var flowStep = new EvidenceFlowStep(1, "d1", "r1", flowDescription);

        return EvidencePacket.Create(
            "request-1",
            ChatMode.Business,
            systemId,
            reverseCollections ? [repository2, repository1] : [repository1, repository2],
            reverseCollections ? [document2, document1] : [document1, document2],
            reverseCollections ? [section2, section1] : [section1, section2],
            reverseCollections ? [anchor2, anchor1] : [anchor1, anchor2],
            [relationship],
            [flowStep]);
    }

    private static Guid Id(int value) =>
        Guid.Parse($"{value:x8}-0000-0000-0000-000000000000");
}
