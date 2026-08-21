using FluentValidation;

namespace Vulgata.Shared.Scans;

public sealed class StartPass1ExecutionRequestValidator : AbstractValidator<StartPass1ExecutionRequest>
{
    public StartPass1ExecutionRequestValidator()
    {
        RuleFor(request => request.FirstManifestId)
            .NotEmpty()
            .WithMessage("第一个已批准清单标识无效。");
        RuleFor(request => request.SecondManifestId)
            .NotEmpty()
            .WithMessage("第二个已批准清单标识无效。");
        RuleFor(request => request)
            .Must(request => request.FirstManifestId != request.SecondManifestId)
            .WithMessage("必须提供两个不同的已批准清单。");
        RuleFor(request => request.ConcurrencyLimit)
            .Must(limit => limit is null or >= 1 and <= 4)
            .WithMessage("并发数必须为 1 至 4。");
    }
}

public sealed class UpdatePass1ConcurrencyRequestValidator : AbstractValidator<UpdatePass1ConcurrencyRequest>
{
    public UpdatePass1ConcurrencyRequestValidator()
    {
        RuleFor(request => request.ExpectedVersion)
            .GreaterThanOrEqualTo(0)
            .WithMessage("预期状态版本无效。");
        RuleFor(request => request.ConcurrencyLimit)
            .InclusiveBetween(1, 4)
            .WithMessage("并发数必须为 1 至 4。");
        RuleFor(request => request.ActorId)
            .MaximumLength(128)
            .WithMessage("操作者标识无效。");
    }
}
