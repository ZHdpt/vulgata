using FluentValidation;

namespace Vulgata.Shared.Scans;

public sealed class StartPass2ExecutionRequestValidator :
    AbstractValidator<StartPass2ExecutionRequest>
{
    public StartPass2ExecutionRequestValidator()
    {
        RuleFor(request => request.Pass1PairExecutionId)
            .NotEmpty()
            .WithMessage("Pass 1 配对执行标识无效。");
        RuleFor(request => request.AdditionalProperties)
            .Empty()
            .WithMessage("请求不得包含调用方提供的权限范围或运行配置。");
    }
}
