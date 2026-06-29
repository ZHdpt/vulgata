using FluentValidation;

namespace Vulgata.Web.Validators;

public sealed class UpdateSystemRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Context { get; set; }
}

public sealed class UpdateSystemRequestValidator : AbstractValidator<UpdateSystemRequest>
{
    public UpdateSystemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("系统名称不能为空。")
            .MaximumLength(200).WithMessage("系统名称最长 200 个字符。");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("系统描述最长 2000 个字符。");

        RuleFor(x => x.Context)
            .MaximumLength(10000).WithMessage("系统上下文最长 10000 个字符。");
    }
}
