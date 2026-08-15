using Vulgata.Agents.ChatExecution;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests;

public sealed class ChatCandidateRankerTests
{
    [Fact]
    public void Rank_BusinessQuestionWithoutRelevantBusinessLogic_UsesRelevantCodeLogicFallback()
    {
        CandidateClaim unrelatedBusiness = Candidate("k1", "客户提交付款后完成订单。", false);
        CandidateClaim relevantCode = Candidate("k2", "系统校验登录密码，失败时返回提示。", true);

        IReadOnlyList<CandidateClaim> ranked = ChatCandidateRanker.Rank(
            "登录密码校验失败后会怎样？",
            ChatMode.Business,
            [unrelatedBusiness, relevantCode]);

        Assert.Equal(["k2"], ranked.Select(candidate => candidate.CandidateHandle));
    }

    [Fact]
    public void Rank_BusinessQuestionWithRelevantBusinessLogic_PrefersBusinessLogic()
    {
        CandidateClaim business = Candidate("k1", "客户登录后，系统更新会话。", false);
        CandidateClaim code = Candidate("k2", "登录请求会更新并保存会话。", true);

        IReadOnlyList<CandidateClaim> ranked = ChatCandidateRanker.Rank(
            "客户登录后如何更新会话？",
            ChatMode.Business,
            [code, business]);

        Assert.Equal(["k1"], ranked.Select(candidate => candidate.CandidateHandle));
    }

    private static CandidateClaim Candidate(string handle, string text, bool narrativeAllowed) =>
        CandidateClaim.Create(
            handle,
            new string('a', 64),
            ChatMode.Business,
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse($"00000000-0000-0000-0000-00000000000{handle[1]}"),
            0,
            text,
            ["a1"],
            [],
            narrativeAllowed);
}
