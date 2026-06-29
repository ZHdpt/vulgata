using FluentValidation;
using Vulgata.Shared.Repositories;

namespace Vulgata.Shared.Validators.Repositories;

public sealed class CreateRepositoryRequestValidator : AbstractValidator<CreateRepositoryRequest>
{
    public CreateRepositoryRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("仓库名称不能为空。")
            .MaximumLength(200).WithMessage("仓库名称最长 200 个字符。");

        RuleFor(request => request.GitUrl)
            .NotEmpty().WithMessage("Git 远程地址不能为空。")
            .MaximumLength(2000).WithMessage("Git 远程地址最长 2000 个字符。");

        RuleFor(request => request.Description)
            .MaximumLength(2000).WithMessage("仓库描述最长 2000 个字符。");

        RuleFor(request => request.Context)
            .MaximumLength(10000).WithMessage("仓库补充上下文最长 10000 个字符。");
    }
}
