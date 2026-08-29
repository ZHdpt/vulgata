using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

/// <summary>
/// T065 RED boundary for the single accepted grounded-chat-evidence/2.2 packet.
/// These tests deliberately inspect only the public packet serialization and
/// builder seam; the 2.0 runtime shape must not remain a parallel authority.
/// </summary>
public sealed class ChatEvidencePacketV22ContractTests
{
    [Fact]
    public void Build_UsesGroundedChatEvidence22AndNotRuntime20Shape()
    {
        JsonElement root = Serialize(BuildPacket());

        Assert.Equal("grounded-chat-evidence/2.2", root.GetProperty("schemaVersion").GetString());
        JsonElement relationship = Assert.Single(root.GetProperty("relationships").EnumerateArray());
        Assert.DoesNotContain("exactDecisionId", PropertyNames(relationship));
        Assert.DoesNotContain("flowGroupId", PropertyNames(relationship));
    }

    [Fact]
    public void Build_SerializedRuntimePacket_ConformsToAccepted22Schema()
    {
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "specs",
                "014-grounded-llm-wiki-chat-release-evidence",
                "contracts",
                "chat-evidence-packet-v2.schema.json")));

        Type validatorType = typeof(Vulgata.Tests.ReleaseGate.Story125EvaluationContractTests).Assembly
            .GetType("Vulgata.Tests.ReleaseGate.Story125EvaluationContractTests+SchemaValidator")
            ?? throw new InvalidOperationException("The Story 12.5 schema validator was not found.");
        System.Reflection.MethodInfo validate = validatorType.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("The Story 12.5 schema validator entry point was not found.");

        IReadOnlyList<string> errors = (IReadOnlyList<string>)validate.Invoke(
            null,
            [Serialize(BuildPacket()), schema.RootElement])!;

        Assert.Empty(errors);
    }

    [Fact]
    public void Build_SerializesSelectionPolicyPrimaryAndExpansionSeedIdentities()
    {
        JsonElement selection = Serialize(BuildPacket()).GetProperty("selection");

        Assert.Equal("lexical-current-v2/1.0", selection.GetProperty("policy").GetString());
        Assert.Equal(256, selection.GetProperty("candidateWindow").GetInt32());
        Assert.Equal(12, selection.GetProperty("primarySectionTarget").GetInt32());
        Assert.Equal(["d1", "d2"], Strings(selection.GetProperty("primaryDocumentHandles")));
        Assert.Equal(["s1", "s2"], Strings(selection.GetProperty("primarySectionHandles")));
        Assert.Equal(["d1", "d2"], Strings(selection.GetProperty("expansionSeedDocumentHandles")));
    }

    [Fact]
    public void Build_SerializesAdvisoryAuthorityFingerprint()
    {
        JsonElement root = Serialize(BuildPacket());

        string? fingerprint = root.GetProperty("advisoryContextAuthorityFingerprint").GetString();
        Assert.Matches("^[0-9a-f]{64}$", fingerprint);
    }

    [Fact]
    public void Build_SerializesSelectedDecisionSetAndMultipleDecisionOwnedFlowGroups()
    {
        JsonElement root = Serialize(BuildPacket());
        JsonElement relationship = Assert.Single(root.GetProperty("relationships").EnumerateArray());

        Assert.Equal(Id(81), relationship.GetProperty("selectedExactDecisionId").GetGuid());
        Assert.Equal(2, relationship.GetProperty("exactDecisionCount").GetInt32());
        Assert.Equal(new string('d', 64), relationship.GetProperty("exactDecisionSetFingerprint").GetString());
        Assert.Equal(["fg-1", "fg-2"], Strings(relationship.GetProperty("flowGroupHandles")));

        JsonElement[] groups = root.GetProperty("flowGroups").EnumerateArray().ToArray();
        Assert.Equal(2, groups.Length);
        Assert.Equal("r1", groups[0].GetProperty("relationshipHandle").GetString());
        Assert.Equal("r1", groups[1].GetProperty("relationshipHandle").GetString());
        Assert.Equal(1, groups[0].GetProperty("decisionRank").GetInt32());
        Assert.Equal(2, groups[1].GetProperty("decisionRank").GetInt32());
        Assert.Equal("fg-1", groups[0].GetProperty("handle").GetString());
        Assert.Equal("fg-2", groups[1].GetProperty("handle").GetString());
    }

    [Fact]
    public void Build_SerializesFlowStepGroupBinding()
    {
        JsonElement flowStep = Assert.Single(Serialize(BuildPacket()).GetProperty("flowSteps").EnumerateArray());

        Assert.Equal("fg-1", flowStep.GetProperty("flowGroupHandle").GetString());
        Assert.Equal("r1", flowStep.GetProperty("relationshipHandle").GetString());
    }

    [Fact]
    public void Build_SerializesConversationPriorLogicalDocumentIdsAndBindsThemInFingerprint()
    {
        ChatConversationScope scope = ChatConversationScope.Create(ChatMode.Business, Id(1), [Id(2), Id(3)]);
        ChatConversationState firstConversation = ChatConversationState.Empty(scope).AppendValidatedTurn(
            ValidatedConversationTurn.Create("上一问", "上一答", new string('f', 64), ["a1"], [Id(10)]));
        ChatConversationState secondConversation = ChatConversationState.Empty(scope).AppendValidatedTurn(
            ValidatedConversationTurn.Create("上一问", "上一答", new string('f', 64), ["a1"], [Id(12)]));

        EvidencePacket first = BuildPacket(firstConversation);
        EvidencePacket second = BuildPacket(secondConversation);
        JsonElement turn = Assert.Single(Serialize(first).GetProperty("conversation").GetProperty("turns").EnumerateArray());

        Assert.Equal([Id(10)], Guids(turn.GetProperty("priorLogicalDocumentIds")));
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
    }

    private static EvidencePacket BuildPacket(ChatConversationState? conversation = null)
    {
        Guid systemId = Id(1);
        Guid repository1 = Id(2);
        Guid repository2 = Id(3);
        var documents = new[]
        {
            new EvidenceDocument("d1", Id(10), Id(11), repository1, DocumentType.BusinessLogic, "订单处理", "abcdef1"),
            new EvidenceDocument("d2", Id(12), Id(13), repository2, DocumentType.CodeLogic, "订单服务", "abcdef2"),
        };
        var sections = new[]
        {
            new EvidenceSection("s1", "d1", Id(20), 1, DocumentSectionKind.BusinessRule, "业务规则", "处理订单请求。", ["a1"], ["d2"]),
            new EvidenceSection("s2", "d2", Id(21), 1, DocumentSectionKind.Overview, "概览", "处理订单。", ["a2"], []),
        };
        var anchors = new[]
        {
            new EvidenceAnchor("a1", Id(30), "d1", Id(11), repository1, "src/Orders.cs", 10, 12, new string('a', 64)),
            new EvidenceAnchor("a2", Id(31), "d2", Id(13), repository2, "src/OrderService.cs", 20, 22, new string('b', 64)),
        };
        var relationship = new EvidenceRelationship(
            "r1", Id(80), ChatRelationshipType.References, "d1", "d2", 1, Id(81), 2,
            new string('d', 64), Id(90), new string('c', 64));
        var flowGroups = new[]
        {
            CreateFlowGroup("fg-1", Id(90), Id(81), Id(93), Id(94)),
            CreateFlowGroup("fg-2", Id(91), Id(84), Id(96), Id(97)),
        };
        ChatConversationState validConversation = conversation ?? ChatConversationState.Empty(
            ChatConversationScope.Create(ChatMode.Business, systemId, [repository1, repository2]));

        ChatEvidencePacketBuildRequest request = ChatEvidencePacketBuildRequest.Create(
            "request-1",
            ChatMode.Business,
            systemId,
            [repository1, repository2],
            documents,
            sections,
            anchors,
            [relationship],
            [new EvidenceFlowStep(1, "d1", "r1", "订单处理流程")],
            flowGroups,
            "已应用的系统上下文",
            validConversation,
            []);
        return new ChatEvidencePacketBuilder().Build(new ChatEvidenceAssembly(request));
    }

    private static EvidenceFlowGroupAuthority CreateFlowGroup(
        string handle,
        Guid flowGroupId,
        Guid decisionId,
        Guid flowTraceId,
        Guid stepId) =>
        new(
            flowGroupId,
            "endpoint-flow-group/1.0",
            "Published",
            Id(11),
            decisionId,
            Id(80),
            new string('d', 64),
            new string('e', 64),
            flowTraceId,
            decisionId == Id(81) ? 1 : 2,
            [new EvidenceFlowStepAuthority(
                stepId,
                1,
                FlowTraceStepKind.Document,
                "订单处理流程",
                Id(11),
                Id(2),
                Id(95),
                null,
                null)]);

    private static JsonElement Serialize(EvidencePacket packet) =>
        JsonSerializer.SerializeToDocument(packet).RootElement.Clone();

    private static string[] Strings(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static Guid[] Guids(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetGuid()).ToArray();

    private static string[] PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).ToArray();

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The Vulgata repository root could not be located.");
    }

    private static Guid Id(int value) =>
        Guid.Parse($"{value:x8}-0000-0000-0000-000000000000");
}
