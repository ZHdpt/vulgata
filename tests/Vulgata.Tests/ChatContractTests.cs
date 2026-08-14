using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests;

public sealed class ChatContractTests
{
    [Fact]
    public void Create_ValidQuestion_NormalizesTextAndCanonicalizesRepositoryIds()
    {
        Guid systemId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid firstRepositoryId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid secondRepositoryId = Guid.Parse("10000000-0000-0000-0000-000000000002");

        ChatQuestionRequest request = ChatQuestionRequest.Create(
            "request-1",
            ChatMode.Business,
            systemId,
            [firstRepositoryId, secondRepositoryId],
            "  Cafe\u0301\r\n处理\t请求  ");

        Assert.Equal("Café 处理 请求", request.Question);
        Assert.Equal(
            [secondRepositoryId, firstRepositoryId],
            request.RepositoryIds);
    }

    [Fact]
    public void Create_InvalidQuestionRequest_RejectsWithClosedFailureCode()
    {
        Guid systemId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid repositoryId = Guid.Parse("20000000-0000-0000-0000-000000000001");

        ChatContractException duplicateScope = Assert.Throws<ChatContractException>(() =>
            ChatQuestionRequest.Create(
                "request-1",
                ChatMode.It,
                systemId,
                [repositoryId, repositoryId],
                "问题"));
        ChatContractException overBound = Assert.Throws<ChatContractException>(() =>
            ChatQuestionRequest.Create(
                "request-1",
                ChatMode.It,
                systemId,
                [repositoryId, Guid.Parse("30000000-0000-0000-0000-000000000001")],
                new string('问', 2_001)));

        Assert.Equal(ChatContractFailureCode.InvalidRequest, duplicateScope.FailureCode);
        Assert.Equal(ChatContractFailureCode.InvalidRequest, overBound.FailureCode);
    }

    [Fact]
    public void Create_CandidateClaim_CanonicalizesSupportAndProducesDeterministicHashes()
    {
        Guid documentVersionId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        Guid stableSectionId = Guid.Parse("40000000-0000-0000-0000-000000000001");

        CandidateClaim first = CandidateClaim.Create(
            "k1",
            new string('a', 64),
            ChatMode.Business,
            documentVersionId,
            stableSectionId,
            12,
            "处理订单请求。",
            ["a2", "a1"],
            ["r2", "r1"]);
        CandidateClaim second = CandidateClaim.Create(
            "k1",
            new string('a', 64),
            ChatMode.Business,
            documentVersionId,
            stableSectionId,
            12,
            "处理订单请求。",
            ["a1", "a2"],
            ["r1", "r2"]);

        Assert.Equal(["a1", "a2"], first.AnchorHandles);
        Assert.Equal(["r1", "r2"], first.RelationshipHandles);
        Assert.Equal(first.TextSha256, second.TextSha256);
        Assert.Equal(first.SupportSignature, second.SupportSignature);
        Assert.Matches("^[0-9a-f]{64}$", first.TextSha256);
        Assert.Matches("^[0-9a-f]{64}$", first.SupportSignature);
    }

    [Fact]
    public void Create_EvidencePacket_CanonicalizesCollectionsAndFingerprintsDeterministically()
    {
        Guid systemId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid repository1 = Guid.Parse("10000000-0000-0000-0000-000000000002");
        Guid repository2 = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var document1 = new EvidenceDocument(
            "d1", Id(1), Id(2), repository1, DocumentType.CodeLogic, "订单入口", "abc123");
        var document2 = new EvidenceDocument(
            "d2", Id(3), Id(4), repository2, DocumentType.CodeLogic, "渠道服务", "def456");
        var section = new EvidenceSection(
            "s1", "d1", Id(5), 1, DocumentSectionKind.Overview, "概览", "处理订单。", ["a1"], []);
        var anchor = new EvidenceAnchor(
            "a1", Id(6), "d1", Id(2), repository1, "src/Orders.cs", 10, 12, new string('b', 64));
        var relationship = new EvidenceRelationship(
            "r1", "edge-1", ChatRelationshipType.References, "d1", "d2", new string('c', 64));
        var flowStep = new EvidenceFlowStep(1, "d1", null, "接收订单请求。");

        EvidencePacket first = EvidencePacket.Create(
            "request-1", ChatMode.It, systemId, [repository2, repository1],
            [document2, document1], [section], [anchor], [relationship], [flowStep]);
        EvidencePacket second = EvidencePacket.Create(
            "request-1", ChatMode.It, systemId, [repository1, repository2],
            [document1, document2], [section], [anchor], [relationship], [flowStep]);

        Assert.Equal([repository1, repository2], first.RepositoryIds);
        Assert.Equal(["d1", "d2"], first.Documents.Select(document => document.DocumentHandle));
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Matches("^[0-9a-f]{64}$", first.Fingerprint);
    }

    [Fact]
    public void Create_ValidatedSelectionAndGroundedAnswer_PreservesFixedClaimCitationMapping()
    {
        string fingerprint = new('a', 64);
        ChatSynthesisResult synthesis = ChatSynthesisResult.Create(
            "request-1",
            ChatMode.Business,
            fingerprint,
            [new ChatSynthesisSelection("k1", 1)]);
        var claim = MaterialClaim.Create("claim-1", 1, "处理订单请求。", ["citation-1"]);
        var citation = DocumentCitation.Create(
            "citation-1", ["claim-1"], "订单入口", DocumentType.BusinessLogic,
            "账户服务", "src/Orders.cs", 10, 12, "概览");

        GroundedAnswer answer = GroundedAnswer.Create(
            ChatMode.Business,
            [claim],
            [citation],
            new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("prepared-chat-answer-v1", synthesis.SchemaVersion);
        Assert.Single(answer.MaterialClaims);
        Assert.Single(answer.DocumentCitations);
        Assert.Equal(["citation-1"], answer.MaterialClaims[0].CitationIds);
        Assert.Equal(["claim-1"], answer.DocumentCitations[0].ClaimIds);

        ChatContractException invalidOrder = Assert.Throws<ChatContractException>(() =>
            ChatSynthesisResult.Create(
                "request-1", ChatMode.Business, fingerprint,
                [new ChatSynthesisSelection("k1", 2)]));
        Assert.Equal(ChatContractFailureCode.InvalidSelection, invalidOrder.FailureCode);
    }

    [Fact]
    public void Generated_ValidRawSelectionJson_RetainsExactProviderContent()
    {
        string rawSelectionJson = "{\r\n  \"schemaVersion\": \"prepared-chat-answer-v1\",\r\n  \"unknown\": 1,\r\n  \"unknown\": 2\r\n}";
        ChatSynthesisResult result = ChatSynthesisResult.Create(
            "request-1",
            ChatMode.Business,
            new string('a', 64),
            [new ChatSynthesisSelection("k1", 1)]);

        ChatSynthesisOutcome outcome = ChatSynthesisOutcome.Generated(result, rawSelectionJson);

        Assert.Same(result, outcome.Result);
        Assert.Equal(rawSelectionJson, outcome.RawSelectionJson);
        Assert.Null(outcome.FailureCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Generated_BlankRawSelectionJson_ThrowsInvalidSelection(string rawSelectionJson)
    {
        ChatSynthesisResult result = ChatSynthesisResult.Create(
            "request-1",
            ChatMode.Business,
            new string('a', 64),
            [new ChatSynthesisSelection("k1", 1)]);

        ChatContractException exception = Assert.Throws<ChatContractException>(() =>
            ChatSynthesisOutcome.Generated(result, rawSelectionJson));

        Assert.Equal(ChatContractFailureCode.InvalidSelection, exception.FailureCode);
    }

    [Fact]
    public void Generated_OverBoundRawSelectionJson_ThrowsInvalidSelection()
    {
        ChatSynthesisResult result = ChatSynthesisResult.Create(
            "request-1",
            ChatMode.Business,
            new string('a', 64),
            [new ChatSynthesisSelection("k1", 1)]);

        ChatContractException exception = Assert.Throws<ChatContractException>(() =>
            ChatSynthesisOutcome.Generated(result, new string('x', 65_537)));

        Assert.Equal(ChatContractFailureCode.InvalidSelection, exception.FailureCode);
    }

    [Theory]
    [InlineData(ChatContractFailureCode.InvalidSelection)]
    [InlineData(ChatContractFailureCode.ProviderFailure)]
    [InlineData(ChatContractFailureCode.TimedOut)]
    [InlineData(ChatContractFailureCode.Cancelled)]
    public void Failed_AllowedFailureCode_RetainsNoRawProviderContent(
        ChatContractFailureCode failureCode)
    {
        ChatSynthesisOutcome outcome = ChatSynthesisOutcome.Failed(failureCode);

        Assert.Null(outcome.Result);
        Assert.Null(outcome.RawSelectionJson);
        Assert.Equal(failureCode, outcome.FailureCode);
    }

    [Fact]
    public void Create_SynthesisRequest_RequiresAndRetainsExactSystemId()
    {
        Guid systemId = Id(30);
        string fingerprint = new('a', 64);
        CandidateClaim candidate = CandidateClaim.Create(
            "k1", fingerprint, ChatMode.Business, Id(31), Id(32), 0,
            "处理订单请求。", ["a1"], ["r1"]);

        ChatSynthesisRequest request = ChatSynthesisRequest.Create(
            "request-1", ChatMode.Business, systemId, "如何处理订单？", fingerprint, [candidate]);

        Assert.Equal(systemId, request.SystemId);

        ChatContractException emptySystem = Assert.Throws<ChatContractException>(() =>
            ChatSynthesisRequest.Create(
                "request-1", ChatMode.Business, Guid.Empty, "如何处理订单？", fingerprint, [candidate]));
        Assert.Equal(ChatContractFailureCode.InvalidSelection, emptySystem.FailureCode);
    }

    [Fact]
    public void Create_CurrentnessRequest_RequiresCanonicalScopeAndPositiveSafeSourceAnchors()
    {
        Guid repository1 = Id(20);
        Guid repository2 = Id(21);
        var document = new ChatCurrentDocument(Id(1), Id(2));
        var section = new ChatCurrentSection(Id(3), Id(2), 0, 5, new string('b', 64));
        var anchor = new ChatCurrentSourceAnchor(
            Id(4), Id(2), repository1, "src/Orders.cs", 10, 12, new string('c', 64));
        var relationship = new ChatCurrentRelationship(
            "edge-1", ChatRelationshipType.References, Id(2), Id(5),
            repository1, repository2, new string('d', 64));

        ChatCurrentnessValidationRequest request = ChatCurrentnessValidationRequest.Create(
            "request-1", new string('a', 64), Id(10), [repository2, repository1],
            [document], [section], [anchor], [relationship]);

        Assert.Equal([repository1, repository2], request.RepositoryIds);
        Assert.Equal(ChatCurrentnessValidationOutcome.Current, ChatCurrentnessValidationOutcome.Current);

        ChatContractException unsafeAnchor = Assert.Throws<ChatContractException>(() =>
            ChatCurrentnessValidationRequest.Create(
                "request-1", new string('a', 64), Id(10), [repository1, repository2],
                [document], [section],
                [anchor with { RepositoryRelativePath = "../secret.cs" }],
                [relationship]));
        Assert.Equal(ChatContractFailureCode.InvalidCurrentness, unsafeAnchor.FailureCode);
    }

    [Fact]
    public void ServiceInterfaces_ExposeExactProviderIndependentClosedContracts()
    {
        AssertMethodSignature(
            typeof(IChatEvidenceRetriever),
            "RetrieveAsync",
            typeof(Task<ChatEvidenceResult>),
            typeof(ChatQuestionRequest),
            typeof(RepositoryAccessScope),
            typeof(CancellationToken));
        AssertMethodSignature(
            typeof(IChatEvidenceCurrentnessValidator),
            "ValidateCurrentAsync",
            typeof(Task<ChatCurrentnessValidationOutcome>),
            typeof(ChatCurrentnessValidationRequest),
            typeof(RepositoryAccessScope),
            typeof(CancellationToken));
        AssertMethodSignature(
            typeof(IChatAgentDispatcher),
            "GenerateAsync",
            typeof(Task<ChatSynthesisOutcome>),
            typeof(ChatSynthesisRequest),
            typeof(CancellationToken));

        Assert.Equal(
            ["Found", "NotFoundOrUnauthorized", "InvalidRequest", "IncompleteOrStale"],
            Enum.GetNames<ChatEvidenceResultKind>());
    }

    private static void AssertMethodSignature(
        Type serviceType,
        string methodName,
        Type returnType,
        params Type[] parameterTypes)
    {
        System.Reflection.MethodInfo? method = serviceType.GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(returnType, method.ReturnType);
        Assert.Equal(parameterTypes, method.GetParameters().Select(parameter => parameter.ParameterType));
    }

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
