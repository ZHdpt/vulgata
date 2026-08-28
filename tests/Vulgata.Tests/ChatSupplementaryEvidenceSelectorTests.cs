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
