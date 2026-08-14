using System.Text.Json;
using Vulgata.Agents.ChatExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests;

public sealed class ChatAgentResultValidatorTests
{
    [Fact]
    public void Validate_ExactSelection_MaterializesRetainedTextAndFixedSupport()
    {
        ValidationFixture fixture = CreateFixture(ChatMode.It, "服务器保留的技术事实。");
        string rawJson = CreateRawJson(fixture.Request, [("k1", 1)]);
        ChatSynthesisOutcome providerOutcome = ChatSynthesisOutcome.Generated(
            ChatSynthesisResult.Create(
                fixture.Request.RequestId,
                fixture.Request.Mode,
                fixture.Request.EvidenceFingerprint,
                [new ChatSynthesisSelection("k1", 1)]),
            rawJson);
        var validator = new ChatAgentResultValidator(new ChatCandidateExtractor());

        ChatAgentResultValidation result = validator.Validate(
            fixture.Packet,
            fixture.Request,
            providerOutcome);

        Assert.True(result.IsValid);
        MaterialClaim claim = Assert.Single(result.MaterialClaims);
        Assert.Equal("claim1", claim.ClaimId);
        Assert.Equal(1, claim.Order);
        Assert.Equal("服务器保留的技术事实。", claim.Text);
        Assert.Equal(["a1"], claim.CitationIds);
        CandidateClaim retained = Assert.Single(result.SelectedCandidates);
        Assert.Equal("k1", retained.CandidateHandle);
        Assert.Equal(fixture.Request.CandidateClaims[0].SupportSignature, retained.SupportSignature);
    }

    [Fact]
    public void Validate_NonClosedOrMismatchedRawJson_RejectsWholeResult()
    {
        ValidationFixture fixture = CreateFixture(ChatMode.It, "服务器保留的技术事实。");
        string valid = CreateRawJson(fixture.Request, [("k1", 1)]);
        string fingerprint = fixture.Request.EvidenceFingerprint;
        string[] invalidResults =
        [
            valid.Replace("{", "{\"diagnostic\":\"provider detail\",", StringComparison.Ordinal),
            valid.Replace("\"order\":1", "\"order\":1,\"citation\":\"a1\"", StringComparison.Ordinal),
            valid.Replace("\"schemaVersion\":\"prepared-chat-answer-v1\",", string.Empty, StringComparison.Ordinal),
            valid.Replace(",\"order\":1", string.Empty, StringComparison.Ordinal),
            valid.Replace("{", "{\"schemaVersion\":\"prepared-chat-answer-v1\",", StringComparison.Ordinal),
            valid.Replace("\"order\":1", "\"order\":1,\"order\":1", StringComparison.Ordinal),
            valid.Replace("prepared-chat-answer-v1", "prepared-chat-answer-v2", StringComparison.Ordinal),
            valid.Replace("request-1", "request-foreign", StringComparison.Ordinal),
            valid.Replace("\"mode\":\"it\"", "\"mode\":\"business\"", StringComparison.Ordinal),
            valid.Replace(fingerprint, new string('f', 64), StringComparison.Ordinal),
            CreateRawJson(fixture.Request, [("k1", 2)]),
            CreateRawJson(fixture.Request, [("k1", 13)]),
            CreateRawJson(fixture.Request, [("k1", 1), ("k1", 2)]),
            CreateRawJson(fixture.Request, [("foreign", 1)]),
            valid.Replace("\"selections\":[", "\"selections\":[] ,\"ignored\":[", StringComparison.Ordinal),
            "not-json",
        ];
        var validator = new ChatAgentResultValidator(new ChatCandidateExtractor());

        foreach (string rawJson in invalidResults)
        {
            ChatAgentResultValidation result = validator.Validate(
                fixture.Packet,
                fixture.Request,
                CreateOutcome(fixture.Request, rawJson));

            Assert.False(result.IsValid);
            Assert.Empty(result.MaterialClaims);
            Assert.Empty(result.SelectedCandidates);
        }
    }

    [Fact]
    public void Validate_TamperedRetainedCandidateState_RejectsWholeResult()
    {
        ValidationFixture fixture = CreateFixture(ChatMode.It, "服务器保留的技术事实。");
        CandidateClaim retained = Assert.Single(fixture.Request.CandidateClaims);
        CandidateClaim[] tamperedCandidates =
        [
            CandidateClaim.Create(
                retained.CandidateHandle,
                fixture.Packet.Fingerprint,
                fixture.Packet.Mode,
                DocumentVersionId(99),
                Id(99),
                0,
                "来自其他 Evidence Packet 的无关事实。",
                ["a1"],
                []),
            CandidateClaim.Create(
                retained.CandidateHandle,
                fixture.Packet.Fingerprint,
                fixture.Packet.Mode,
                retained.DocumentVersionId,
                retained.StableSectionId,
                retained.SpanStart,
                "与服务器保留事实相反。",
                retained.AnchorHandles,
                retained.RelationshipHandles),
            CandidateClaim.Create(
                retained.CandidateHandle,
                fixture.Packet.Fingerprint,
                fixture.Packet.Mode,
                retained.DocumentVersionId,
                retained.StableSectionId,
                retained.SpanStart + 1,
                retained.Text,
                retained.AnchorHandles,
                retained.RelationshipHandles),
            CandidateClaim.Create(
                retained.CandidateHandle,
                fixture.Packet.Fingerprint,
                fixture.Packet.Mode,
                retained.DocumentVersionId,
                retained.StableSectionId,
                retained.SpanStart,
                retained.Text,
                ["a2"],
                retained.RelationshipHandles),
        ];
        var validator = new ChatAgentResultValidator(new ChatCandidateExtractor());

        foreach (CandidateClaim tampered in tamperedCandidates)
        {
            ChatSynthesisRequest request = ChatSynthesisRequest.Create(
                fixture.Packet.RequestId,
                fixture.Packet.Mode,
                fixture.Packet.SystemId,
                "如何处理请求？",
                fixture.Packet.Fingerprint,
                [tampered]);
            ChatAgentResultValidation result = validator.Validate(
                fixture.Packet,
                request,
                CreateOutcome(request, CreateRawJson(request, [("k1", 1)])));

            Assert.False(result.IsValid);
            Assert.Empty(result.MaterialClaims);
            Assert.Empty(result.SelectedCandidates);
        }
    }

    [Fact]
    public void Validate_RestrictedSupportOrBusinessLocatorText_RejectsWholeResult()
    {
        EvidencePacket businessPacket = CreateUnsupportedPacket(
            ChatMode.Business,
            "src/OrderService.cs:42 处理请求。",
            supportingDocumentHandle: null);
        EvidencePacket restrictedPacket = CreateUnsupportedPacket(
            ChatMode.It,
            "不得使用缺失的关系支持。",
            supportingDocumentHandle: "d3");
        var validator = new ChatAgentResultValidator(new ChatCandidateExtractor());

        foreach (EvidencePacket packet in new[] { businessPacket, restrictedPacket })
        {
            CandidateClaim forged = CandidateClaim.Create(
                "k1",
                packet.Fingerprint,
                packet.Mode,
                DocumentVersionId(1),
                Id(11),
                0,
                packet.Sections[0].ContentMarkdown,
                ["a1"],
                []);
            ChatSynthesisRequest request = ChatSynthesisRequest.Create(
                packet.RequestId,
                packet.Mode,
                packet.SystemId,
                "如何处理请求？",
                packet.Fingerprint,
                [forged]);

            ChatAgentResultValidation result = validator.Validate(
                packet,
                request,
                CreateOutcome(request, CreateRawJson(request, [("k1", 1)])));

            Assert.False(result.IsValid);
            Assert.Empty(result.MaterialClaims);
            Assert.Empty(result.SelectedCandidates);
        }
    }

    [Fact]
    public void Validate_MixedValidAndInvalidSelections_ReturnsZeroMaterialization()
    {
        ValidationFixture fixture = CreateFixture(ChatMode.It, "第一项。\n\n第二项。");
        string rawJson = CreateRawJson(fixture.Request, [("k1", 1), ("foreign", 2)]);
        var validator = new ChatAgentResultValidator(new ChatCandidateExtractor());

        ChatAgentResultValidation result = validator.Validate(
            fixture.Packet,
            fixture.Request,
            CreateOutcome(fixture.Request, rawJson));

        Assert.False(result.IsValid);
        Assert.Empty(result.MaterialClaims);
        Assert.Empty(result.SelectedCandidates);
    }

    [Fact]
    public void Validate_OrderedSelections_UsesOnlyRetainedServerContentAndSupport()
    {
        ValidationFixture fixture = CreateFixture(ChatMode.It, "第一项。\n\n第二项。");
        string rawJson = CreateRawJson(fixture.Request, [("k2", 1), ("k1", 2)]);
        ChatSynthesisOutcome outcome = ChatSynthesisOutcome.Generated(
            ChatSynthesisResult.Create(
                fixture.Request.RequestId,
                fixture.Request.Mode,
                fixture.Request.EvidenceFingerprint,
                [new ChatSynthesisSelection("k1", 1)]),
            rawJson);
        var validator = new ChatAgentResultValidator(new ChatCandidateExtractor());

        ChatAgentResultValidation result = validator.Validate(
            fixture.Packet,
            fixture.Request,
            outcome);

        Assert.True(result.IsValid);
        Assert.Equal(["第二项。", "第一项。"], result.MaterialClaims.Select(claim => claim.Text));
        Assert.Equal(["a1"], result.MaterialClaims[0].CitationIds);
        Assert.Equal(["a1"], result.MaterialClaims[1].CitationIds);
        Assert.Equal(["k2", "k1"], result.SelectedCandidates.Select(candidate => candidate.CandidateHandle));
    }

    private static ValidationFixture CreateFixture(ChatMode mode, string markdown)
    {
        EvidencePacket packet = EvidencePacket.Create(
            "request-1",
            mode,
            SystemId,
            [Repository2, Repository1],
            [
                Document("d1", 1, mode == ChatMode.Business ? DocumentType.BusinessLogic : DocumentType.CodeLogic),
                Document("d2", 2, DocumentType.CodeLogic, Repository2),
            ],
            [new EvidenceSection("s1", "d1", Id(11), 1, DocumentSectionKind.Overview, "章节", markdown, ["a1"], [])],
            [new EvidenceAnchor("a1", Id(21), "d1", DocumentVersionId(1), Repository1, "src/d1.cs", 10, 12, new string('b', 64))],
            [new EvidenceRelationship("r1", "edge-r1", ChatRelationshipType.References, "d1", "d2", new string('c', 64))],
            [new EvidenceFlowStep(1, "d1", null, "准备回答。")]);
        IReadOnlyList<CandidateClaim> candidates = new ChatCandidateExtractor().Extract(packet);
        ChatSynthesisRequest request = ChatSynthesisRequest.Create(
            packet.RequestId,
            packet.Mode,
            packet.SystemId,
            "如何处理请求？",
            packet.Fingerprint,
            candidates);
        return new ValidationFixture(packet, request);
    }

    private static EvidencePacket CreateUnsupportedPacket(
        ChatMode mode,
        string markdown,
        string? supportingDocumentHandle) =>
        EvidencePacket.Create(
            "request-1",
            mode,
            SystemId,
            [Repository2, Repository1],
            [
                Document("d1", 1, mode == ChatMode.Business ? DocumentType.BusinessLogic : DocumentType.CodeLogic),
                Document("d2", 2, DocumentType.CodeLogic, Repository2),
                Document("d3", 3, DocumentType.CodeLogic, Repository2),
            ],
            [new EvidenceSection(
                "s1",
                "d1",
                Id(11),
                1,
                DocumentSectionKind.Overview,
                "章节",
                markdown,
                ["a1"],
                supportingDocumentHandle is null ? [] : [supportingDocumentHandle])],
            [new EvidenceAnchor("a1", Id(21), "d1", DocumentVersionId(1), Repository1, "src/d1.cs", 10, 12, new string('b', 64))],
            [new EvidenceRelationship("r1", "edge-r1", ChatRelationshipType.References, "d1", "d2", new string('c', 64))],
            [new EvidenceFlowStep(1, "d1", null, "准备回答。")]);

    private static string CreateRawJson(
        ChatSynthesisRequest request,
        IReadOnlyCollection<(string Handle, int Order)> selections) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = ChatSynthesisResult.SupportedSchemaVersion,
            requestId = request.RequestId,
            mode = request.Mode == ChatMode.Business ? "business" : "it",
            evidenceFingerprint = request.EvidenceFingerprint,
            answerability = "supported",
            selections = selections.Select(selection => new
            {
                candidateHandle = selection.Handle,
                order = selection.Order,
            }),
        });

    private static ChatSynthesisOutcome CreateOutcome(
        ChatSynthesisRequest request,
        string rawJson) =>
        ChatSynthesisOutcome.Generated(
            ChatSynthesisResult.Create(
                request.RequestId,
                request.Mode,
                request.EvidenceFingerprint,
                [new ChatSynthesisSelection("k1", 1)]),
            rawJson);

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

    private static Guid DocumentVersionId(int value) => Id(1_000 + value);

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private sealed record ValidationFixture(EvidencePacket Packet, ChatSynthesisRequest Request);

    private static readonly Guid SystemId = Id(1);
    private static readonly Guid Repository1 = Id(2);
    private static readonly Guid Repository2 = Id(3);
}
