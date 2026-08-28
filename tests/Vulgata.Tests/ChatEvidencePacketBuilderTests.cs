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

        Assert.Equal("grounded-chat-evidence/2.2", packet.SchemaVersion);
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
    public void Create_V2Packet_CanonicalFingerprint_BindsRelationshipEdgeIdentity()
    {
        EvidencePacket original = CreatePacket();
        EvidencePacket changed = CreatePacket(relationshipEdgeId: Id(93));

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
            optionalSectionHandles: ["s3"],
            primarySectionHandles: ["s1", "s2", "s3"],
            lexicalExtraSectionRanks: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["s3"] = 3,
            }));

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
            flowGroups: [CreateFlowGroupAuthority(flowTraceId: Id(98))]));

        Assert.NotEqual(baseline.Fingerprint, changed.Fingerprint);
    }

    [Fact]
    public void Build_V2Packet_CanonicalFingerprint_BindsDecisionCountAndSetFingerprint()
    {
        EvidenceRelationship baselineRelationship = CreateV2Relationship(
            edgeId: Id(80),
            depth: 1,
            exactDecisionId: Id(81),
            flowGroupId: Id(90),
            exactDecisionCount: 2,
            exactDecisionSetFingerprint: new string('d', 64));
        EvidencePacket baseline = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: baselineRelationship,
            flowGroups: [CreateFlowGroupAuthority(Id(90), Id(81), Id(80))]));

        EvidencePacket countChanged = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: baselineRelationship with { ExactDecisionCount = 3 },
            flowGroups: [CreateFlowGroupAuthority(Id(90), Id(81), Id(80))]));
        EvidencePacket setChanged = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: baselineRelationship with { ExactDecisionSetFingerprint = new string('e', 64) },
            flowGroups: [CreateFlowGroupAuthority(Id(90), Id(81), Id(80))]));

        Assert.NotEqual(baseline.Fingerprint, countChanged.Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, setChanged.Fingerprint);
    }

    [Fact]
    public void Build_V2Packet_SharedEdgeWithTwoDecisionOwnedFlowGroups_RemainsDistinctAndCanonical()
    {
        EvidenceRelationship relationship = CreateV2Relationship(
            edgeId: Id(80),
            depth: 1,
            exactDecisionId: Id(81),
            flowGroupId: Id(90),
            exactDecisionCount: 2,
            exactDecisionSetFingerprint: new string('d', 64));
        EvidenceFlowGroupAuthority firstGroup = CreateFlowGroupAuthority(Id(90), Id(81), Id(80), Id(93), Id(94));
        EvidenceFlowGroupAuthority secondGroup = CreateFlowGroupAuthority(Id(91), Id(84), Id(80), Id(96), Id(97));

        EvidencePacket first = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: relationship,
            flowGroups: [secondGroup, firstGroup]));
        EvidencePacket second = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: relationship,
            flowGroups: [firstGroup, secondGroup]));

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal([Id(90), Id(91)], first.FlowGroups.Select(group => group.FlowGroupId));
        Assert.Equal([Id(81), Id(84)], first.FlowGroups.Select(group => group.ExactResolutionDecisionId));
        Assert.Equal([Id(94), Id(97)], first.FlowGroups.SelectMany(group => group.Steps)
            .Select(step => step.FlowTraceStepId));
        Assert.Equal(2, first.FlowGroups.Count);
    }

    [Fact]
    public void Build_V2Packet_CanonicalFingerprint_BindsEachDecisionOwnedFlowGroupAndStep()
    {
        EvidenceRelationship relationship = CreateV2Relationship(
            edgeId: Id(80),
            depth: 1,
            exactDecisionId: Id(81),
            flowGroupId: Id(90),
            exactDecisionCount: 2,
            exactDecisionSetFingerprint: new string('d', 64));
        EvidenceFlowGroupAuthority baselineGroup = CreateFlowGroupAuthority(Id(90), Id(81), Id(80), Id(93), Id(94));
        EvidencePacket baseline = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: relationship,
            flowGroups: [baselineGroup]));

        EvidencePacket decisionChanged = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: relationship with { ExactDecisionId = Id(84) },
            flowGroups: [baselineGroup with { ExactResolutionDecisionId = Id(84) }]));
        EvidencePacket edgeChanged = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: relationship with { EdgeId = Id(83) },
            flowGroups: [baselineGroup with { ExactResolutionEdgeId = Id(83) }]));
        EvidencePacket traceChanged = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: relationship,
            flowGroups: [baselineGroup with { FlowTraceId = Id(98) }]));
        EvidenceFlowStepAuthority stepChanged = baselineGroup.Steps[0] with
        {
            Description = "改名后的完整流程步骤",
        };
        EvidencePacket stepDescriptionChanged = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: relationship,
            flowDescription: "改名后的完整流程步骤",
            flowGroups: [baselineGroup with { Steps = [stepChanged] }]));

        Assert.NotEqual(baseline.Fingerprint, decisionChanged.Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, edgeChanged.Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, traceChanged.Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, stepDescriptionChanged.Fingerprint);
    }

    [Fact]
    public void Build_V2Packet_OverBoundOptionalRelationshipUnit_DropsItsCompleteSupport()
    {
        EvidencePacket packet = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            includeOptionalRelationshipUnit: true,
            optionalSectionHandles: ["s3"],
            optionalRelationshipUnitRanks: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["r3"] = 1,
            }));

        Assert.DoesNotContain(packet.Sections, section => section.SectionHandle == "s3");
        Assert.DoesNotContain(packet.Relationships, relationship => relationship.RelationshipHandle == "r3");
        Assert.DoesNotContain(packet.FlowGroups, group => group.FlowGroupId == Id(96));
        Assert.DoesNotContain(packet.FlowEvidence, step => step.DocumentHandle == "d3");
        Assert.DoesNotContain(packet.SourceAnchors, anchor => anchor.AnchorHandle == "a3");
        Assert.DoesNotContain(packet.Documents, document => document.DocumentHandle == "d3");
    }

    [Fact]
    public void Build_V2Packet_OverBoundOptionalRelationshipUnits_DropsLowestEvidenceRankBeforeHandleOrder()
    {
        EvidencePacket packet = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            includeRankedOptionalUnits: true,
            optionalUnitsHaveRelationships: true,
            optionalSectionHandles: ["s0", "sz"],
            primarySectionHandles: ["s1", "s2", "s0", "sz"],
            optionalRelationshipUnitRanks: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["r0"] = 2,
                ["rz"] = 1,
            }));

        Assert.DoesNotContain(packet.Relationships, relationship => relationship.RelationshipHandle == "r0");
        Assert.DoesNotContain(packet.FlowGroups, group => group.FlowGroupId == Id(96));
        Assert.DoesNotContain(packet.FlowEvidence, step => step.RelationshipHandle == "r0");
        Assert.DoesNotContain(packet.Sections, section => section.SectionHandle == "s0");
        Assert.DoesNotContain(packet.SourceAnchors, anchor => anchor.AnchorHandle == "a0");
        Assert.DoesNotContain(packet.Documents, document => document.DocumentHandle == "d0");

        Assert.Contains(packet.Relationships, relationship => relationship.RelationshipHandle == "rz");
        Assert.Contains(packet.FlowGroups, group => group.FlowGroupId == Id(106));
        Assert.Contains(packet.FlowEvidence, step => step.RelationshipHandle == "rz");
        Assert.Contains(packet.Sections, section => section.SectionHandle == "sz");
        Assert.Contains(packet.SourceAnchors, anchor => anchor.AnchorHandle == "az");
        Assert.Contains(packet.Documents, document => document.DocumentHandle == "dz");
    }

    [Fact]
    public void Build_V2Packet_OverBoundLexicalExtras_DropsLowestEvidenceRankBeforeHandleOrder()
    {
        EvidencePacket packet = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            includeRankedOptionalUnits: true,
            optionalSectionHandles: ["s0", "sz"],
            primarySectionHandles: ["s1", "s2", "s0", "sz"],
            lexicalExtraSectionRanks: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["s0"] = 3,
                ["sz"] = 2,
            }));

        Assert.DoesNotContain(packet.Sections, section => section.SectionHandle == "s0");
        Assert.DoesNotContain(packet.SourceAnchors, anchor => anchor.AnchorHandle == "a0");
        Assert.DoesNotContain(packet.Documents, document => document.DocumentHandle == "d0");

        Assert.Contains(packet.Sections, section => section.SectionHandle == "sz");
        Assert.Contains(packet.SourceAnchors, anchor => anchor.AnchorHandle == "az");
        Assert.Contains(packet.Documents, document => document.DocumentHandle == "dz");
    }

    [Fact]
    public void Build_V2Packet_SelectedRequiredFlowGroupWithoutSupport_Rejects()
    {
        ChatContractException exception = Assert.Throws<ChatContractException>(() =>
            new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
                relationship: CreateV2Relationship(Id(80), 1, Id(81), Id(90)),
                flowGroups: [CreateFlowGroupAuthority(
                    flowGroupId: Id(90),
                    decisionId: Id(81),
                    edgeId: Id(80),
                    businessLogicDocumentVersionId: Id(999),
                    stepDocumentVersionId: Id(999))])));

        Assert.Equal(ChatContractFailureCode.InvalidEvidence, exception.FailureCode);
    }

    [Fact]
    public void Build_V2Packet_SchemaSerialization_UsesExactConversationFieldNamesAndContents()
    {
        EvidencePacket packet = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            conversation: ChatConversationState.Empty(ChatConversationScope.Create(
                ChatMode.Business,
                Id(1),
                [Id(2), Id(3)])).AppendValidatedTurn(
                    ValidatedConversationTurn.Create(
                        "上一问",
                        "上一答",
                        new string('f', 64),
                        ["a1"],
                        [Id(10)]))));

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(
            packet,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        JsonElement root = document.RootElement;
        Assert.Equal(
            [
                "advisoryContext",
                "advisoryContextAuthorityFingerprint",
                "conversation",
                "documents",
                "fingerprint",
                "flowGroups",
                "flowSteps",
                "mode",
                "relationships",
                "repositoryIds",
                "requestId",
                "schemaVersion",
                "sections",
                "selection",
                "sourceAnchors",
                "systemId",
            ],
            root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));

        JsonElement conversation = root.GetProperty("conversation");
        Assert.Equal(["turns"], conversation.EnumerateObject().Select(property => property.Name));

        JsonElement turn = Assert.Single(conversation.GetProperty("turns").EnumerateArray());
        Assert.Equal(
            ["question", "validatedAnswer", "packetFingerprint", "priorCitationHandles", "priorLogicalDocumentIds"],
            turn.EnumerateObject().Select(property => property.Name));
        Assert.Equal("上一答", turn.GetProperty("validatedAnswer").GetString());
        Assert.Equal(["a1"], turn.GetProperty("priorCitationHandles").EnumerateArray()
            .Select(value => value.GetString()).ToArray());
        Assert.Equal([Id(10)], turn.GetProperty("priorLogicalDocumentIds").EnumerateArray()
            .Select(value => value.GetGuid()).ToArray());
    }

    [Fact]
    public void Build_V2Packet_SchemaSerialization_ExposesRequiredRelationshipAuthorityFields()
    {
        EvidencePacket packet = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: CreateV2Relationship(Id(80), 1, Id(81), Id(82))));

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(
            packet,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        JsonElement relationship = Assert.Single(document.RootElement.GetProperty("relationships").EnumerateArray());

        Assert.Equal(
            [
                "depth",
                "edgeId",
                "exactDecisionCount",
                "exactDecisionSetFingerprint",
                "flowGroupHandles",
                "handle",
                "selectedExactDecisionId",
                "sourceDocumentHandle",
                "targetDocumentHandle",
                "type",
            ],
            relationship.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(Id(80), relationship.GetProperty("edgeId").GetGuid());
        Assert.Equal(1, relationship.GetProperty("depth").GetInt32());
        Assert.Equal(1, relationship.GetProperty("exactDecisionCount").GetInt32());
        Assert.Equal(Id(81), relationship.GetProperty("selectedExactDecisionId").GetGuid());
        Assert.Equal(new string('d', 64), relationship.GetProperty("exactDecisionSetFingerprint").GetString());
        Assert.Equal(["fg-1"], relationship.GetProperty("flowGroupHandles").EnumerateArray()
            .Select(value => value.GetString()).ToArray());
    }

    [Fact]
    public void Build_V2Packet_CanonicalFingerprint_BindsEachRelationshipAuthorityField()
    {
        EvidenceRelationship baselineRelationship = CreateV2Relationship(
            edgeId: Id(80),
            depth: 1,
            exactDecisionId: Id(81),
            flowGroupId: Id(82));
        EvidencePacket baseline = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: baselineRelationship));

        EvidencePacket edgeChanged = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: CreateV2Relationship(Id(83), 1, Id(81), Id(82))));
        EvidencePacket depthChanged = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: CreateV2Relationship(Id(80), 0, Id(81), Id(82))));
        EvidencePacket decisionChanged = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: CreateV2Relationship(Id(80), 1, Id(84), Id(82))));
        EvidencePacket flowGroupChanged = new ChatEvidencePacketBuilder().Build(CreateBuildRequest(
            relationship: CreateV2Relationship(Id(80), 1, Id(81), Id(85))));

        Assert.NotEqual(baseline.Fingerprint, edgeChanged.Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, depthChanged.Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, decisionChanged.Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, flowGroupChanged.Fingerprint);
    }

    private static ChatEvidencePacketBuildRequest CreateBuildRequest(
        string sectionContent = "处理订单请求。",
        string flowDescription = "订单处理流程",
        string advisoryContext = "",
        ChatConversationState? conversation = null,
        bool includeOversizedOptionalSection = false,
        IReadOnlyCollection<string>? optionalSectionHandles = null,
        IReadOnlyCollection<EvidenceFlowGroupAuthority>? flowGroups = null,
        EvidenceRelationship? relationship = null,
        bool includeOptionalRelationshipUnit = false,
        bool includeRankedOptionalUnits = false,
        bool optionalUnitsHaveRelationships = false,
        IReadOnlyCollection<string>? primarySectionHandles = null,
        IReadOnlyDictionary<string, int>? optionalRelationshipUnitRanks = null,
        IReadOnlyDictionary<string, int>? lexicalExtraSectionRanks = null)
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
        EvidenceRelationship primaryRelationship = relationship ?? CreateV2Relationship(
            Id(92),
            1,
            Id(91),
            Id(90));
        var relationshipSnapshot = new List<EvidenceRelationship> { primaryRelationship };
        var flowSnapshot = new List<EvidenceFlowStep>
        {
            new(1, "d1", "r1", flowDescription),
        };
        if (includeOversizedOptionalSection)
        {
            documents.Add(new EvidenceDocument(
                "d3", Id(14), Id(15), repository1, DocumentType.CodeLogic, "可选支持", "abcdef3"));
            sections.Add(new EvidenceSection(
                "s3", "d1", Id(22), 2, DocumentSectionKind.TechnicalDetail, "可选详情", new string('x', 70_000), ["a3"], ["d3"]));
            anchors.Add(new EvidenceAnchor(
                "a3", Id(32), "d1", Id(11), repository1, "src/Optional.cs", 30, 32, new string('e', 64)));
        }
        if (includeOptionalRelationshipUnit)
        {
            if (!includeOversizedOptionalSection)
            {
                documents.Add(new EvidenceDocument(
                    "d3", Id(14), Id(15), repository1, DocumentType.CodeLogic, "可选支持", "abcdef3"));
                sections.Add(new EvidenceSection(
                    "s3", "d3", Id(22), 1, DocumentSectionKind.TechnicalDetail, "可选详情", new string('x', 70_000), ["a3"], []));
                anchors.Add(new EvidenceAnchor(
                    "a3", Id(32), "d3", Id(15), repository1, "src/Optional.cs", 30, 32, new string('e', 64)));
            }

            relationshipSnapshot.Add(CreateV2Relationship(Id(83), 1, Id(84), Id(96)) with
            {
                RelationshipHandle = "r3",
                SourceDocumentHandle = "d1",
                TargetDocumentHandle = "d3",
            });
            flowSnapshot.Add(new EvidenceFlowStep(2, "d3", "r3", "可选支持流程"));
        }
        if (includeRankedOptionalUnits)
        {
            documents.Add(new EvidenceDocument(
                "d0", Id(14), Id(15), repository1, DocumentType.CodeLogic, "低排名支持", "abcdef3"));
            documents.Add(new EvidenceDocument(
                "dz", Id(16), Id(17), repository2, DocumentType.CodeLogic, "高排名支持", "abcdef4"));
            sections.Add(new EvidenceSection(
                "s0", optionalUnitsHaveRelationships ? "d0" : "d1", Id(22), 2,
                DocumentSectionKind.TechnicalDetail, "低排名详情", new string('x', 35_000), ["a0"],
                optionalUnitsHaveRelationships ? [] : ["d0"]));
            sections.Add(new EvidenceSection(
                "sz", optionalUnitsHaveRelationships ? "dz" : "d2", Id(23), 2,
                DocumentSectionKind.TechnicalDetail, "高排名详情", new string('y', 35_000), ["az"],
                optionalUnitsHaveRelationships ? [] : ["dz"]));
            anchors.Add(new EvidenceAnchor(
                "a0", Id(32), optionalUnitsHaveRelationships ? "d0" : "d1",
                optionalUnitsHaveRelationships ? Id(15) : Id(11), repository1,
                "src/LowRank.cs", 30, 32, new string('e', 64)));
            anchors.Add(new EvidenceAnchor(
                "az", Id(33), optionalUnitsHaveRelationships ? "dz" : "d2",
                optionalUnitsHaveRelationships ? Id(17) : Id(13), repository2,
                "src/HighRank.cs", 40, 42, new string('f', 64)));
            if (optionalUnitsHaveRelationships)
            {
                relationshipSnapshot.Add(CreateV2Relationship(Id(83), 1, Id(84), Id(96)) with
                {
                    RelationshipHandle = "r0",
                    SourceDocumentHandle = "d1",
                    TargetDocumentHandle = "d0",
                });
                relationshipSnapshot.Add(CreateV2Relationship(Id(103), 1, Id(104), Id(106)) with
                {
                    RelationshipHandle = "rz",
                    SourceDocumentHandle = "d1",
                    TargetDocumentHandle = "dz",
                });
                flowSnapshot.Add(new EvidenceFlowStep(2, "d0", "r0", "低排名可选流程"));
                flowSnapshot.Add(new EvidenceFlowStep(3, "dz", "rz", "高排名可选流程"));
            }
        }

        IReadOnlyCollection<EvidenceFlowGroupAuthority> flowGroupSnapshot = flowGroups ??
            [CreateFlowGroupAuthority(
                primaryRelationship.FlowGroupId ?? Id(90),
                primaryRelationship.ExactDecisionId ?? Id(91),
                primaryRelationship.EdgeId)];
        if (includeOptionalRelationshipUnit)
        {
            flowGroupSnapshot = flowGroupSnapshot
                .Append(CreateFlowGroupAuthority(Id(96), Id(84), Id(83), Id(95), Id(99), Id(15)))
                .ToArray();
        }
        if (includeRankedOptionalUnits && optionalUnitsHaveRelationships)
        {
            flowGroupSnapshot = flowGroupSnapshot
                .Append(CreateFlowGroupAuthority(
                    Id(96), Id(84), Id(83), Id(95), Id(99), Id(15),
                    stepDescription: "低排名可选流程"))
                .Append(CreateFlowGroupAuthority(
                    Id(106), Id(104), Id(103), Id(105), Id(109), Id(17),
                    stepDescription: "高排名可选流程"))
                .ToArray();
        }

        return ChatEvidencePacketBuildRequest.Create(
            "request-1",
            ChatMode.Business,
            systemId,
            [repository1, repository2],
            documents,
            sections,
            anchors,
            relationshipSnapshot,
            flowSnapshot,
            flowGroupSnapshot,
            advisoryContext,
            conversation ?? ChatConversationState.Empty(ChatConversationScope.Create(
                ChatMode.Business,
                systemId,
                [repository1, repository2])),
            optionalSectionHandles ?? [],
            null,
            ["d1", "d2"],
            primarySectionHandles ?? ["s1", "s2"],
            ["d1", "d2"],
            optionalRelationshipUnitRanks: optionalRelationshipUnitRanks ??
                new Dictionary<string, int>(StringComparer.Ordinal),
            lexicalExtraSectionRanks: lexicalExtraSectionRanks ??
                new Dictionary<string, int>(StringComparer.Ordinal));
    }

    private static EvidenceRelationship CreateV2Relationship(
        Guid edgeId,
        int depth,
        Guid? exactDecisionId,
        Guid? flowGroupId,
        int exactDecisionCount = 1,
        string? exactDecisionSetFingerprint = null) =>
        new(
            "r1",
            edgeId,
            ChatRelationshipType.References,
            "d1",
            "d2",
            depth,
            exactDecisionId,
            exactDecisionCount,
            exactDecisionSetFingerprint ?? new string('d', 64),
            flowGroupId,
            new string('c', 64));

    private static EvidenceFlowGroupAuthority CreateFlowGroupAuthority(
        Guid? flowGroupId = null,
        Guid? decisionId = null,
        Guid? edgeId = null,
        Guid? flowTraceId = null,
        Guid? stepId = null,
        Guid? businessLogicDocumentVersionId = null,
        Guid? stepDocumentVersionId = null,
        string stepDescription = "订单处理流程") =>
        new(
            flowGroupId ?? Id(90),
            "endpoint-flow-group/1.0",
            "Published",
            businessLogicDocumentVersionId ?? Id(11),
            decisionId ?? Id(91),
            edgeId ?? Id(92),
            new string('d', 64),
            new string('e', 64),
            flowTraceId ?? Id(93),
            1,
            [new EvidenceFlowStepAuthority(
                stepId ?? Id(94),
                1,
                FlowTraceStepKind.Document,
                stepDescription,
                stepDocumentVersionId ?? businessLogicDocumentVersionId ?? Id(11),
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
        Guid? relationshipEdgeId = null)
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
        Guid edgeId = relationshipEdgeId ?? Id(92);
        var relationship = CreateV2Relationship(edgeId, 1, Id(91), Id(90));
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
            [flowStep],
            [CreateFlowGroupAuthority(Id(90), Id(91), edgeId, stepDescription: flowDescription)]);
    }

    private static Guid Id(int value) =>
        Guid.Parse($"{value:x8}-0000-0000-0000-000000000000");
}
