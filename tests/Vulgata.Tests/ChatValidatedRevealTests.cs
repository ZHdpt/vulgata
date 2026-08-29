using System.Reflection;
using Vulgata.Agents.ChatExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Web.Components.Pages;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

/// <summary>
/// T036 presentation-boundary characterization and RED coverage.
/// Provider stream signals are server-only; the browser receives only a
/// complete GroundedAnswer after the validation barrier.
/// </summary>
public sealed class ChatValidatedRevealTests
{
    [Fact]
    public void ChatPage_RequiresAcceptedPublicationCommitBeforeClientReveal()
    {
        Type? commitType = Type.GetType(
            "Vulgata.Web.Data.AcceptedPublicationCommit, Vulgata.Web");

        Assert.NotNull(commitType);
        Assert.Contains(
            typeof(PreparedChatResult).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
            property => property.PropertyType == commitType);
    }

    [Fact]
    public void RejectedProviderAssembly_ExposesNoRenderablePartialAnswer()
    {
        ChatProviderStreamAssemblyResult result = ChatProviderStreamAssemblyResult.RejectedResult(
            "invalid-json-event",
            answerDeltaCount: 1,
            answerDeltaByteCount: 3,
            assembledAnswerByteCount: 3,
            terminalCount: 0,
            rawBodyByteCount: 64);

        Assert.False(result.Accepted);
        Assert.Null(result.Answer);
        Assert.Null(result.PartialAnswer);
    }

    [Fact]
    public void PendingTurn_ContainsNoAcceptedAnswerOrEvidenceBeforeValidation()
    {
        ChatTurn turn = ChatTurn.CreatePending("尚未验证的问题", ChatMode.Business);

        Assert.Equal(ChatTurnState.Pending, turn.State);
        Assert.Null(turn.Answer);
        Assert.Empty(turn.Claims);
        Assert.Null(turn.Message);
    }

    [Fact]
    public void AcceptedTurn_Characterization_RevealsOnlyUnicodeTextElementsAfterAcceptance()
    {
        GroundedAnswer answer = CreateAnswer("e\u0301👨‍👩‍👧‍👦已验证");
        ChatTurn turn = ChatTurn.CreatePending("已验证的问题", ChatMode.Business);

        turn.BeginReveal(answer);
        ChatClaimReveal claim = Assert.Single(turn.Claims);
        Assert.Equal(ChatTurnState.Revealing, turn.State);
        Assert.False(claim.IsComplete);
        Assert.Equal(string.Empty, claim.VisibleText);

        turn.CompleteReveal();

        Assert.Equal(ChatTurnState.Complete, turn.State);
        Assert.Equal("e\u0301👨‍👩‍👧‍👦已验证", claim.VisibleText);
        Assert.True(claim.IsComplete);
    }

    [Fact]
    public void ProgressiveRevealDirective_BindsUnicodeTextElementCostAndFixedSafetyMargin()
    {
        Type directiveType = typeof(PreparedChatRevealDirective);
        PropertyInfo? scheduleCost = directiveType.GetProperty("ScheduleCostMilliseconds");
        PropertyInfo? safetyMargin = directiveType.GetProperty("SafetyMarginMilliseconds");

        Assert.NotNull(scheduleCost);
        Assert.NotNull(safetyMargin);
        PreparedChatRevealDirective directive = PreparedChatRevealDirective.ProgressiveAccepted(29_000);

        Assert.False(scheduleCost!.CanWrite);
        Assert.True(Convert.ToInt64(scheduleCost.GetValue(directive)) >= 0);
        Assert.True(Convert.ToInt64(safetyMargin!.GetValue(directive)) > 0);
    }

    private static GroundedAnswer CreateAnswer(string text)
    {
        const string citationId = "c1";
        return GroundedAnswer.Create(
            ChatMode.Business,
            [MaterialClaim.Create("claim1", 1, text, [citationId])],
            [DocumentCitation.Create(
                citationId,
                ["claim1"],
                "已验证文档",
                DocumentType.BusinessLogic,
                "mbs_account",
                "src/PaymentFlow.cs",
                20,
                24)],
            DateTimeOffset.UtcNow);
    }
}
