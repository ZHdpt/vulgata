using Vulgata.Tests.TestInfrastructure;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Web.Components.Pages;

namespace Vulgata.Tests;

public sealed class ChatTurnTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ChatClaimReveal_RevealNext_AdvancesByUnicodeTextElement()
    {
        MaterialClaim claim = MaterialClaim.Create("claim-1", 1, "你👨‍👩‍👧‍👦e\u0301", ["citation-1"]);
        var reveal = new ChatClaimReveal(claim);

        Assert.True(reveal.RevealNext());
        Assert.Equal("你", reveal.VisibleText);
        Assert.True(reveal.RevealNext());
        Assert.Equal("你👨‍👩‍👧‍👦", reveal.VisibleText);
        Assert.True(reveal.RevealNext());
        Assert.Equal("你👨‍👩‍👧‍👦e\u0301", reveal.VisibleText);
        Assert.True(reveal.IsComplete);
        Assert.False(reveal.RevealNext());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ChatClaimReveal_Complete_PublishesWholeClaimWithoutPartialUnicode()
    {
        MaterialClaim claim = MaterialClaim.Create("claim-1", 1, "业务✅", ["citation-1"]);
        var reveal = new ChatClaimReveal(claim);

        reveal.Complete();

        Assert.Equal(claim.Text, reveal.VisibleText);
        Assert.True(reveal.IsComplete);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ChatTurn_BeginAndCompleteReveal_TransitionsValidatedAnswerState()
    {
        GroundedAnswer answer = CreateAnswer("第一条。", "第二条。");
        ChatTurn turn = ChatTurn.CreatePending("问题", ChatMode.Business);

        turn.BeginReveal(answer);

        Assert.Equal(ChatTurnState.Revealing, turn.State);
        Assert.Equal(2, turn.Claims.Count);
        Assert.All(turn.Claims, claim => Assert.False(claim.IsComplete));

        turn.CompleteReveal();

        Assert.Equal(ChatTurnState.Complete, turn.State);
        Assert.All(turn.Claims, claim => Assert.True(claim.IsComplete));
    }

    private static GroundedAnswer CreateAnswer(params string[] texts)
    {
        MaterialClaim[] claims = texts.Select((text, index) =>
            MaterialClaim.Create($"claim-{index + 1}", index + 1, text, [$"citation-{index + 1}"]))
            .ToArray();
        DocumentCitation[] citations = claims.Select((claim, index) =>
            DocumentCitation.Create(
                $"citation-{index + 1}",
                [claim.ClaimId],
                $"文档 {index + 1}",
                DocumentType.BusinessLogic,
                "示例仓库",
                $"src/File{index + 1}.cs",
                index + 1,
                index + 1))
            .ToArray();
        return GroundedAnswer.Create(ChatMode.Business, claims, citations, DateTimeOffset.UtcNow);
    }
}
