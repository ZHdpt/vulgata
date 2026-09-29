using Vulgata.Tests.TestInfrastructure;
using System.Text;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests;

public sealed class ChatConversationStateTests
{
    private static readonly Guid SystemId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid FirstRepositoryId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondRepositoryId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid OtherRepositoryId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly Guid FirstDocumentId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondDocumentId = Guid.Parse("30000000-0000-0000-0000-000000000002");

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AppendValidatedTurn_InitialAndTwoFollowUps_RetainsOnlyCompleteValidatedTurnsInOrder()
    {
        ChatConversationState state = ChatConversationState.Empty(CreateScope(ChatMode.Business));

        state = state.AppendValidatedTurn(Turn("初始问题", "完整答案一", "h1", "c1", FirstDocumentId));
        state = state.AppendValidatedTurn(Turn("追问一", "完整答案二", "h2", "c2", FirstDocumentId));
        state = state.AppendValidatedTurn(Turn("追问二", "完整答案三", "h3", "c3", SecondDocumentId));

        Assert.Equal(["追问一", "追问二"], state.Turns.Select(turn => turn.Question));
        Assert.Equal(["完整答案二", "完整答案三"], state.Turns.Select(turn => turn.AnswerText));
        Assert.DoesNotContain(state.Turns, turn => turn.AnswerText.Contains("partial", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AppendValidatedTurn_FourthTurn_EvictsOldestAndNeverExceedsTwoTurns()
    {
        ChatConversationState state = ChatConversationState.Empty(CreateScope(ChatMode.It));
        for (int index = 1; index <= 4; index++)
        {
            state = state.AppendValidatedTurn(Turn($"问题{index}", $"答案{index}", $"h{index}", $"c{index}", FirstDocumentId));
        }

        Assert.Equal(2, state.Turns.Count);
        Assert.Equal(["问题3", "问题4"], state.Turns.Select(turn => turn.Question));
        Assert.Equal(["h3", "h4"], state.Turns.Select(turn => turn.PacketFingerprint));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ResetForScopeOrModeChange_ClearsPriorTurnsBeforeNextQuestion()
    {
        ChatConversationState state = ChatConversationState.Empty(CreateScope(ChatMode.Business))
            .AppendValidatedTurn(Turn("业务问题", "业务答案", "h1", "c1", FirstDocumentId));

        ChatConversationState changedMode = state.ResetForScopeChange(CreateScope(ChatMode.It));
        ChatConversationState changedRepositories = changedMode.ResetForScopeChange(
            CreateScope(ChatMode.It, OtherRepositoryId));

        Assert.Empty(changedMode.Turns);
        Assert.Empty(changedRepositories.Turns);
        Assert.Equal(ChatMode.It, changedRepositories.Scope.Mode);
        Assert.Equal([FirstRepositoryId, OtherRepositoryId], changedRepositories.Scope.RepositoryIds);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Reset_ClearsTurnsAndStartsAnEmptyBoundedConversation()
    {
        ChatConversationScope scope = CreateScope(ChatMode.Business);
        ChatConversationState state = ChatConversationState.Empty(scope)
            .AppendValidatedTurn(Turn("问题", "答案", "h1", "c1", FirstDocumentId));

        ChatConversationState reset = state.Reset();

        Assert.Empty(reset.Turns);
        Assert.Equal(scope, reset.Scope);
        Assert.InRange(reset.EncodedUtf8Bytes, 0, 4_096);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AppendValidatedTurn_ConversationEncodingOverFourKiB_IsRejectedWithoutTruncatingAnswer()
    {
        ChatConversationState state = ChatConversationState.Empty(CreateScope(ChatMode.Business));
        string oversizedAnswer = new('中', 3_000);

        ChatContractException exception = Assert.Throws<ChatContractException>(() =>
            state.AppendValidatedTurn(Turn("问题", oversizedAnswer, "h1", "c1", FirstDocumentId)));

        Assert.Equal(ChatContractFailureCode.InvalidEvidence, exception.FailureCode);
        Assert.Empty(state.Turns);
        Assert.True(Encoding.UTF8.GetByteCount(oversizedAnswer) > 4_096);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AppendValidatedTurn_ExactFourKiBConversationState_IsAcceptedAndPlusOneIsRejected()
    {
        ChatConversationScope scope = CreateScope(ChatMode.Business);
        ChatConversationState empty = ChatConversationState.Empty(scope);
        int answerLength = FindAnswerLengthForEncodedState(empty, 4_096);

        ChatConversationState exact = empty.AppendValidatedTurn(
            Turn("问题", new('x', answerLength), "h1", "c1", FirstDocumentId));

        Assert.Equal(4_096, exact.EncodedUtf8Bytes);
        ChatContractException exception = Assert.Throws<ChatContractException>(() =>
            empty.AppendValidatedTurn(
                Turn("问题", new('x', answerLength + 1), "h1", "c1", FirstDocumentId)));

        Assert.Equal(ChatContractFailureCode.InvalidEvidence, exception.FailureCode);
        Assert.Empty(empty.Turns);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PriorLogicalDocumentHints_ExposeOnlyBoundedCitationAndDocumentHints_NotPriorEvidenceBody()
    {
        ChatConversationState state = ChatConversationState.Empty(CreateScope(ChatMode.Business))
            .AppendValidatedTurn(Turn("问题", "答案中不能作为证据的旧文本", "h1", "c1", FirstDocumentId))
            .AppendValidatedTurn(Turn("追问", "第二个完整答案", "h2", "c2", SecondDocumentId));

        Assert.Equal([FirstDocumentId, SecondDocumentId], state.PriorLogicalDocumentHints);
        Assert.Equal(["c1", "c2"], state.PriorCitationHandles);
        Assert.DoesNotContain("旧文本", state.RetrievalContext, StringComparison.Ordinal);
        Assert.DoesNotContain("第二个完整答案", state.RetrievalContext, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BuildRetrievalContext_ReFetchedHintLoss_ClearsConversationAndFailsClosed()
    {
        ChatConversationState state = ChatConversationState.Empty(CreateScope(ChatMode.Business))
            .AppendValidatedTurn(Turn("问题", "答案", "h1", "c1", FirstDocumentId))
            .AppendValidatedTurn(Turn("追问", "答案二", "h2", "c2", SecondDocumentId));

        ChatConversationRetrievalContext context = state.BuildRetrievalContext(
            currentAuthorizedDocumentIds: [FirstDocumentId],
            currentDocumentVersions: [FirstDocumentId]);

        Assert.True(context.IsMissingEquivalent);
        Assert.Empty(context.CurrentDocumentHints);
        Assert.Empty(context.CitationHandles);
        Assert.Empty(state.Turns);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BuildRetrievalContext_AuthorizationLoss_ClearsConversationAndReturnsMissingEquivalentContext()
    {
        ChatConversationState state = ChatConversationState.Empty(CreateScope(ChatMode.Business))
            .AppendValidatedTurn(Turn("问题", "答案", "h1", "c1", FirstDocumentId));

        ChatConversationRetrievalContext context = state.BuildRetrievalContext(
            currentAuthorizedDocumentIds: [],
            currentDocumentVersions: []);

        Assert.Empty(context.CurrentDocumentHints);
        Assert.Empty(context.CitationHandles);
        Assert.True(context.IsMissingEquivalent);
        Assert.Empty(state.Turns);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BuildRetrievalContext_PartialAuthorizationRevocation_ClearsAllPriorHintsAndFailsClosed()
    {
        ChatConversationState state = ChatConversationState.Empty(CreateScope(ChatMode.Business))
            .AppendValidatedTurn(Turn("问题", "答案", "h1", "c1", FirstDocumentId))
            .AppendValidatedTurn(Turn("追问", "答案二", "h2", "c2", SecondDocumentId));

        ChatConversationRetrievalContext context = state.BuildRetrievalContext(
            currentAuthorizedDocumentIds: [FirstDocumentId],
            currentDocumentVersions: [FirstDocumentId, SecondDocumentId]);

        Assert.True(context.IsMissingEquivalent);
        Assert.Empty(context.CurrentDocumentHints);
        Assert.Empty(context.CitationHandles);
        Assert.Empty(state.Turns);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BuildRetrievalContext_PartialCurrentnessMutation_ClearsAllPriorHintsAndFailsClosed()
    {
        ChatConversationState state = ChatConversationState.Empty(CreateScope(ChatMode.Business))
            .AppendValidatedTurn(Turn("问题", "答案", "h1", "c1", FirstDocumentId))
            .AppendValidatedTurn(Turn("追问", "答案二", "h2", "c2", SecondDocumentId));

        ChatConversationRetrievalContext context = state.BuildRetrievalContext(
            currentAuthorizedDocumentIds: [FirstDocumentId, SecondDocumentId],
            currentDocumentVersions: [FirstDocumentId]);

        Assert.True(context.IsMissingEquivalent);
        Assert.Empty(context.CurrentDocumentHints);
        Assert.Empty(context.CitationHandles);
        Assert.Empty(state.Turns);
    }

    private static int FindAnswerLengthForEncodedState(
        ChatConversationState empty,
        int targetBytes)
    {
        for (int answerLength = 1; answerLength <= targetBytes; answerLength++)
        {
            ChatConversationState candidate = empty.AppendValidatedTurn(
                Turn("问题", new('x', answerLength), "h1", "c1", FirstDocumentId));
            if (candidate.EncodedUtf8Bytes == targetBytes)
            {
                return answerLength;
            }
        }

        throw new Xunit.Sdk.XunitException($"No answer length produced {targetBytes} encoded bytes.");
    }

    private static ChatConversationScope CreateScope(
        ChatMode mode,
        Guid? alternateRepositoryId = null) =>
        ChatConversationScope.Create(
            mode,
            SystemId,
            [FirstRepositoryId, alternateRepositoryId ?? SecondRepositoryId]);

    private static ValidatedConversationTurn Turn(
        string question,
        string answer,
        string fingerprint,
        string citationHandle,
        Guid logicalDocumentId) =>
        ValidatedConversationTurn.Create(
            question,
            answer,
            fingerprint,
            [citationHandle],
            [logicalDocumentId]);
}
