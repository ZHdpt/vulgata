using System.Text;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Chat;

public sealed class OctoberChatInputDigestsTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeMode.Business)]
    [InlineData(KnowledgeMode.IT)]
    public async Task CreateAsync_UsesExactEmbeddedPromptAndVersionedInitialProjectionAsync(KnowledgeMode mode)
    {
        const string question = "  注册与登录\n\"条件\" \\ 路径 😀 e\u0301  ";
        string? prompt = await new OctoberChatPromptCatalog().LoadAsync(mode);
        Assert.False(string.IsNullOrWhiteSpace(prompt));
        string expectedPromptDigest = OctoberKnowledgePolicy.Hash(prompt!);
        string expectedInitialDigest = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(
            new object[] { "october-chat-initial-v1", (int)mode, expectedPromptDigest, question }));

        var result = await OctoberChatInputDigests.CreateAsync(mode, question, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedPromptDigest, result.PromptDigest);
        Assert.Equal(expectedInitialDigest, result.InitialInputDigest);
        Assert.Equal(64, result.PromptDigest.Length);
        Assert.Equal(64, result.InitialInputDigest.Length);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CreateAsync_ExactQuestionAndMode_AreDistinctWithoutTrimmingOrNormalizationAsync()
    {
        var plain = await OctoberChatInputDigests.CreateAsync(KnowledgeMode.IT, "é");
        var spaced = await OctoberChatInputDigests.CreateAsync(KnowledgeMode.IT, " é ");
        var decomposed = await OctoberChatInputDigests.CreateAsync(KnowledgeMode.IT, "e\u0301");
        var business = await OctoberChatInputDigests.CreateAsync(KnowledgeMode.Business, "é");
        var repeated = await OctoberChatInputDigests.CreateAsync(KnowledgeMode.IT, "é");

        Assert.NotNull(plain);
        Assert.NotNull(spaced);
        Assert.NotNull(decomposed);
        Assert.NotNull(business);
        Assert.NotNull(repeated);
        Assert.Equal(plain.PromptDigest, spaced.PromptDigest);
        Assert.Equal(plain.PromptDigest, decomposed.PromptDigest);
        Assert.NotEqual(plain.PromptDigest, business.PromptDigest);
        Assert.NotEqual(plain.InitialInputDigest, spaced.InitialInputDigest);
        Assert.NotEqual(plain.InitialInputDigest, decomposed.InitialInputDigest);
        Assert.NotEqual(plain.InitialInputDigest, business.InitialInputDigest);
        Assert.Equal(plain.PromptDigest, repeated.PromptDigest);
        Assert.Equal(plain.InitialInputDigest, repeated.InitialInputDigest);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task CreateAsync_UndefinedMode_DoesNotFallBackAsync(int mode) =>
        Assert.Null(await OctoberChatInputDigests.CreateAsync((KnowledgeMode)mode, "登录"));

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    [InlineData("\u3000")]
    public async Task CreateAsync_MissingOrWhitespaceQuestion_IsRejectedAsync(string? question) =>
        Assert.Null(await OctoberChatInputDigests.CreateAsync(KnowledgeMode.IT, question!));

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateAsync_UnpairedSurrogate_IsRejectedWithoutReplacementAsync(bool high)
    {
        string question = new(new[] { '问', high ? '\uD800' : '\uDC00', '题' });
        Assert.Null(await OctoberChatInputDigests.CreateAsync(KnowledgeMode.IT, question));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAsync_ExactScalarAndUtf8Ceilings_AcceptsWithoutUtf16MiscountAsync(bool supplementary)
    {
        string question = supplementary
            ? string.Concat(Enumerable.Repeat("😀", OctoberDialogueLimits.MaximumQuestionScalars))
            : new string('问', OctoberDialogueLimits.MaximumQuestionScalars);
        Assert.Equal(OctoberDialogueLimits.MaximumQuestionScalars, question.EnumerateRunes().Count());
        Assert.True(Encoding.UTF8.GetByteCount(question) <= OctoberDialogueLimits.MaximumQuestionUtf8Bytes);
        if (supplementary)
        {
            Assert.Equal(OctoberDialogueLimits.MaximumQuestionUtf8Bytes, Encoding.UTF8.GetByteCount(question));
        }

        var result = await OctoberChatInputDigests.CreateAsync(KnowledgeMode.IT, question);
        Assert.NotNull(result);
        Assert.Equal(OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(
            new object[] { "october-chat-initial-v1", (int)KnowledgeMode.IT, result.PromptDigest, question })),
            result.InitialInputDigest);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAsync_OverScalarOrUtf8Ceiling_RejectsRatherThanTruncatingAsync(bool supplementary)
    {
        string question = supplementary
            ? string.Concat(Enumerable.Repeat("😀", OctoberDialogueLimits.MaximumQuestionScalars + 1))
            : new string('a', OctoberDialogueLimits.MaximumQuestionScalars + 1);
        // With valid Unicode, 8,000 scalars imply at most 32,000 UTF-8 bytes. The
        // supplementary case exceeds both; the ASCII case isolates the scalar bound.
        Assert.Equal(supplementary,
            Encoding.UTF8.GetByteCount(question) > OctoberDialogueLimits.MaximumQuestionUtf8Bytes);
        Assert.Null(await OctoberChatInputDigests.CreateAsync(KnowledgeMode.IT, question));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CreateAsync_CancelledToken_ThrowsInsteadOfReturningDigestOrNullAsync()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            OctoberChatInputDigests.CreateAsync(KnowledgeMode.IT, "登录", cancellation.Token));
    }
}
