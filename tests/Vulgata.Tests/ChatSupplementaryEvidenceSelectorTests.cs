using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class ChatSupplementaryEvidenceSelectorTests
{
    [Fact]
    public void Select_LoginQuestion_ReturnsRelevantDistinctDocumentsInDeterministicOrder()
    {
        SupplementaryChatSectionCandidate[] candidates =
        [
            Candidate(1, 11, "用户注册流程", "注册", "客户提交注册信息并创建账户。"),
            Candidate(2, 12, "登录密码校验", "密码校验", "系统校验登录密码，失败时返回提示。"),
            Candidate(3, 13, "登录验证码处理", "验证码", "登录失败后可发送验证码完成身份确认。"),
            Candidate(2, 14, "登录密码校验", "其他步骤", "记录审计信息。"),
        ];

        IReadOnlyList<SupplementaryChatSectionCandidate> selected =
            ChatSupplementaryEvidenceSelector.Select(
                "登录失败后为什么收不到验证码？",
                candidates,
                2);

        Assert.Equal([Id(3), Id(2)], selected.Select(item => item.DocumentVersionId));
        Assert.Equal(2, selected.Select(item => item.DocumentVersionId).Distinct().Count());
    }

    [Fact]
    public void Select_UnrelatedQuestion_ReturnsNoSupplementaryEvidence()
    {
        SupplementaryChatSectionCandidate[] candidates =
        [
            Candidate(1, 11, "登录密码校验", "密码校验", "系统校验登录密码。"),
        ];

        Assert.Empty(ChatSupplementaryEvidenceSelector.Select("北京明天天气怎么样？", candidates, 2));
    }

    [Fact]
    public void Select_PasswordFailureQuestion_PrefersRuleFromRelevantDocument()
    {
        SupplementaryChatSectionCandidate[] candidates =
        [
            Candidate(1, 11, "校验登录密码操作类 CheckLoginPwdOp", "概述", "该操作负责完成登录密码校验。"),
            Candidate(1, 12, "校验登录密码操作类 CheckLoginPwdOp", "校验规则", "客户密码不匹配时校验失败，结果设置为零。"),
            Candidate(2, 13, "登录密码认证流程", "处理步骤", "进入登录密码认证总流程。"),
        ];

        SupplementaryChatSectionCandidate selected = Assert.Single(
            ChatSupplementaryEvidenceSelector.Select("登录密码校验失败后会怎样？", candidates, 1));

        Assert.Equal(Id(12), selected.StableSectionId);
    }

    [Fact]
    public void Select_ExactEndpointHeading_PrecedesIncidentalTitleMatch()
    {
        SupplementaryChatSectionCandidate[] candidates =
        [
            Candidate(1, 11, "orders 相关业务说明", "概述", "说明订单提交后的业务处理。"),
            Candidate(2, 12, "订单服务", "POST /orders", "接口接收订单提交请求。"),
        ];

        SupplementaryChatSectionCandidate selected = Assert.Single(
            ChatSupplementaryEvidenceSelector.Select("orders 如何提交？", candidates, 1));

        Assert.Equal(Id(12), selected.StableSectionId);
    }

    [Fact]
    public void Select_HardDocumentBound_DropsTheExactPlusOneDocument()
    {
        SupplementaryChatSectionCandidate[] candidates = Enumerable.Range(1, 6)
            .Select(index => Candidate(
                index,
                index + 10,
                $"订单流程 {index}",
                "订单处理",
                "订单处理规则。"))
            .ToArray();

        IReadOnlyList<SupplementaryChatSectionCandidate> selected =
            ChatSupplementaryEvidenceSelector.Select("订单处理规则", candidates, 6);

        Assert.Equal(5, selected.Count);
        Assert.Equal(5, selected.Select(item => item.DocumentVersionId).Distinct().Count());
    }

    [Fact]
    public void Select_RepositoryNameTerm_SelectsAuthorizedRepositoryCandidate()
    {
        SupplementaryChatSectionCandidate[] candidates =
        [
            new(
                Id(1),
                Id(11),
                "提供端业务流程",
                "会话更新",
                "提供端处理会话更新。",
                LogicalDocumentId: Id(101),
                RepositoryId: Id(201),
                RepositoryName: "fixture-provider",
                SearchText: "会话更新"),
            new(
                Id(2),
                Id(12),
                "消费端业务流程",
                "会话更新",
                "消费端发起会话更新。",
                LogicalDocumentId: Id(102),
                RepositoryId: Id(202),
                RepositoryName: "fixture-consumer",
                SearchText: "会话更新"),
        ];

        SupplementaryChatSectionCandidate selected = Assert.Single(
            ChatSupplementaryEvidenceSelector.Select("fixture-provider", candidates, 1));

        Assert.Equal(Id(1), selected.DocumentVersionId);
    }

    [Fact]
    public void Select_StrongestSectionPerDocument_AddsAtMostFourExtraSectionsInScoreOrder()
    {
        SupplementaryChatSectionCandidate[] candidates = Enumerable.Range(1, 6)
            .Select(index => Candidate(
                1,
                index,
                "登录密码校验",
                index == 1 ? "密码校验" : $"密码规则 {index}",
                index == 1 ? "登录密码校验失败处理。" : "登录密码校验规则。"))
            .ToArray();

        IReadOnlyList<SupplementaryChatSectionCandidate> selected =
            ChatSupplementaryEvidenceSelector.Select("登录密码校验", candidates, 12);

        Assert.Equal(4, selected.Count);
        Assert.Equal(Id(1), selected[0].StableSectionId);
        Assert.Equal(4, selected.Select(item => item.StableSectionId).Distinct().Count());
    }

    [Fact]
    public void Select_RandomizedCanonicalTieOrder_IsStable()
    {
        SupplementaryChatSectionCandidate[] candidates =
        [
            new(Id(1), Id(11), "相同标题", "相同标题", "相同内容", LogicalDocumentId: Id(101), RepositoryId: Id(202)),
            new(Id(2), Id(12), "相同标题", "相同标题", "相同内容", LogicalDocumentId: Id(102), RepositoryId: Id(201)),
            new(Id(3), Id(13), "相同标题", "相同标题", "相同内容", LogicalDocumentId: Id(103), RepositoryId: Id(203)),
        ];

        string[] expected = [Id(2).ToString(), Id(1).ToString(), Id(3).ToString()];
        foreach (SupplementaryChatSectionCandidate[] permutation in Permutations(candidates))
        {
            IReadOnlyList<SupplementaryChatSectionCandidate> selected =
                ChatSupplementaryEvidenceSelector.Select("相同标题", permutation, 3);

            Assert.Equal(expected, selected.Select(item => item.DocumentVersionId.ToString()));
        }
    }

    private static IEnumerable<SupplementaryChatSectionCandidate[]> Permutations(
        SupplementaryChatSectionCandidate[] values)
    {
        for (int first = 0; first < values.Length; first++)
        {
            for (int second = 0; second < values.Length; second++)
            {
                if (second == first)
                {
                    continue;
                }

                int third = 3 - first - second;
                yield return [values[first], values[second], values[third]];
            }
        }
    }

    private static SupplementaryChatSectionCandidate Candidate(
        int document,
        int section,
        string title,
        string heading,
        string content) =>
        new(Id(document), Id(section), title, heading, content);

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
