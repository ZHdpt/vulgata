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

    [Fact]
    public void Rank_BusinessQuestionWithStrongerCodeLogicMatch_PreservesCodeLogicFallback()
    {
        CandidateClaim weakBusiness = Candidate("k1", "系统记录认证错误后更新客户会话。", false);
        CandidateClaim exactCode = Candidate(
            "k2",
            "AuthTimesCheckOp 检查登录认证错误次数是否超过上限。",
            true);

        IReadOnlyList<CandidateClaim> ranked = ChatCandidateRanker.Rank(
            "认证错误次数检查操作类 AuthTimesCheckOp 负责什么？",
            ChatMode.Business,
            [weakBusiness, exactCode]);

        Assert.Equal("k2", ranked[0].CandidateHandle);
        Assert.Contains(ranked, candidate => candidate.CandidateHandle == "k1");
    }

    [Theory]
    [InlineData("移动端消费方和渠道提供方在会话更新中各自负责什么？", "k1")]
    [InlineData("认证错误次数检查操作类负责什么？", "k2")]
    [InlineData("登录认证错误次数超过上限会怎样？", "k2")]
    [InlineData("UpdateChannelSessionOp 如何通过跨仓库调用 UpdateSessionOp 更新渠道会话？", "k1")]
    [InlineData("UpdateChannelSessionOp 调用 UpdateSessionOp 时设置了哪些 SOFA 参数？", "k3")]
    [InlineData("UpdateSessionOp 如何构造 Redis key、写入 token 并提交会话？", "k4")]
    public void SelectStrongGroundedMatches_DemoQuestion_SelectsExpectedEvidence(
        string question,
        string expectedHandle)
    {
        CandidateClaim[] candidates =
        [
            Candidate("k1", "移动端消费方通过 UpdateChannelSessionOp 调用渠道提供方 UpdateSessionOp 更新渠道会话。", false),
            Candidate("k2", "AuthTimesCheckOp 检查登录认证错误次数是否超过允许上限。", true),
            Candidate("k3", "UpdateChannelSessionOp 设置 transCode、interfaceId、methodName 和 uniqueId 等 SOFA 参数。", true),
            Candidate("k4", "UpdateSessionOp 构造 Redis key、写入 token 并提交会话。", true),
        ];

        IReadOnlyList<CandidateClaim> selected =
            ChatCandidateRanker.SelectStrongGroundedMatches(question, candidates);

        Assert.Contains(selected, candidate => candidate.CandidateHandle == expectedHandle);
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
