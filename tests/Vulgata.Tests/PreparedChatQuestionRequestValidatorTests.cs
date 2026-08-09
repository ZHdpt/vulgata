using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Shared.Chat;
using Vulgata.Shared.Validators.Chat;

namespace Vulgata.Tests;

public sealed class PreparedChatQuestionRequestValidatorTests
{
    [Fact]
    public async Task ValidateAsync_NormalizedTwoRepositoryRequest_IsValidAndNormalizesQuestion()
    {
        var request = new PreparedChatQuestionRequest
        {
            Mode = PreparedChatMode.Business,
            SystemId = Guid.NewGuid(),
            RepositoryIds = [Guid.NewGuid(), Guid.NewGuid()],
            Question = "  cafe\u0301   如何更新？ \n",
        };

        var validator = new PreparedChatQuestionRequestValidator();
        FluentValidation.Results.ValidationResult result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
        Assert.Equal("café 如何更新？", request.NormalizedQuestion);
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("system")]
    [InlineData("repository-count")]
    [InlineData("repository-duplicate")]
    [InlineData("question-empty")]
    [InlineData("question-2001-runes")]
    public async Task ValidateAsync_InvalidBoundaryShape_IsInvalid(string mutation)
    {
        Guid repositoryId = Guid.NewGuid();
        var request = new PreparedChatQuestionRequest
        {
            Mode = PreparedChatMode.It,
            SystemId = Guid.NewGuid(),
            RepositoryIds = [repositoryId, Guid.NewGuid()],
            Question = "接口如何实现？",
        };
        switch (mutation)
        {
            case "mode": request.Mode = null; break;
            case "system": request.SystemId = Guid.Empty; break;
            case "repository-count": request.RepositoryIds = [repositoryId]; break;
            case "repository-duplicate": request.RepositoryIds = [repositoryId, repositoryId]; break;
            case "question-empty": request.Question = " \n "; break;
            case "question-2001-runes": request.Question = string.Concat(Enumerable.Repeat("😀", 2_001)); break;
        }

        var validator = new PreparedChatQuestionRequestValidator();
        FluentValidation.Results.ValidationResult result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_AssemblyRegistration_ResolvesPreparedChatBoundaryValidator()
    {
        var services = new ServiceCollection();
        services.AddValidatorsFromAssemblyContaining<PreparedChatQuestionRequestValidator>();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<PreparedChatQuestionRequestValidator>(
            provider.GetRequiredService<IValidator<PreparedChatQuestionRequest>>());
    }
}
