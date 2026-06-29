using FluentValidation;
using Vulgata.Shared.Systems;

namespace Vulgata.Shared.Validators.Systems;

public sealed class AssignSystemOwnerRequestValidator : AbstractValidator<AssignSystemOwnerRequest>
{
    public AssignSystemOwnerRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("用户标识不能为空。")
            .MaximumLength(450).WithMessage("用户标识长度无效。");
    }
}
