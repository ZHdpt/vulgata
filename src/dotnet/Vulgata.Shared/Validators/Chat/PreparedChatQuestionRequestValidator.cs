using FluentValidation;
using Vulgata.Shared.Chat;

namespace Vulgata.Shared.Validators.Chat;

public sealed class PreparedChatQuestionRequestValidator :
    AbstractValidator<PreparedChatQuestionRequest>
{
    public PreparedChatQuestionRequestValidator()
    {
        RuleFor(request => request.Mode)
            .NotNull().WithMessage("请选择对话模式。")
            .Must(mode => mode is null || Enum.IsDefined(mode.Value))
            .WithMessage("对话模式无效。");
        RuleFor(request => request.SystemId)
            .NotEmpty().WithMessage("请选择系统。");
        RuleFor(request => request.RepositoryIds)
            .NotNull().WithMessage("请选择两个仓库。")
            .Must(repositoryIds => repositoryIds is not null &&
                repositoryIds.Count == 2 &&
                repositoryIds.All(repositoryId => repositoryId != Guid.Empty) &&
                repositoryIds.Distinct().Count() == 2)
            .WithMessage("请选择两个不同的仓库。");
        RuleFor(request => request.Question)
            .Custom((question, context) =>
            {
                if (!PreparedChatQuestionRequest.TryNormalizeQuestion(
                        question,
                        out string normalized) ||
                    normalized.EnumerateRunes().Count() is < 1 or > 2_000)
                {
                    context.AddFailure("问题需为 1 至 2000 个字符。");
                }
            });
    }
}
