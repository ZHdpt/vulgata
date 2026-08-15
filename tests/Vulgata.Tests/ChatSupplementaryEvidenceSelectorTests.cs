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
