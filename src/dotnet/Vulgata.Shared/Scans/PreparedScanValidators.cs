using FluentValidation;

namespace Vulgata.Shared.Scans;

public sealed class PreparedScanRequestValidator : AbstractValidator<PreparedScanRequest>
{
    public PreparedScanRequestValidator()
    {
        RuleFor(request => request.Repositories)
            .NotNull().WithMessage("必须提供仓库修订绑定。")
            .Must(repositories => repositories is { Count: 2 })
            .WithMessage("必须恰好提供两个仓库修订绑定。")
            .Must(HaveDistinctRepositories)
            .WithMessage("两个仓库修订绑定必须指向不同仓库。");

        RuleForEach(request => request.Repositories)
            .SetValidator(new PreparedScanRepositoryRevisionBindingValidator());
    }

    private static bool HaveDistinctRepositories(
        IReadOnlyList<PreparedScanRepositoryRevisionBinding>? repositories) =>
        repositories is not null &&
        repositories.Select(repository => repository.RepositoryId).Distinct().Count() ==
        repositories.Count;
}

public sealed class PreparedScanRepositoryRevisionBindingValidator :
    AbstractValidator<PreparedScanRepositoryRevisionBinding>
{
    public PreparedScanRepositoryRevisionBindingValidator()
    {
        RuleFor(binding => binding.RepositoryId)
            .NotEmpty().WithMessage("仓库标识无效。");

        RuleFor(binding => binding.GitRef)
            .NotEmpty().WithMessage("Git 引用不能为空。")
            .Must(IsSafeFullRef).WithMessage("Git 引用必须是安全的完整分支或标签引用。");

        RuleFor(binding => binding.ProfiledRevision)
            .Must(IsExactCommit)
            .WithMessage("Profiled Revision 必须是 40 位小写十六进制提交。");
    }

    private static bool IsExactCommit(string? value) =>
        value is { Length: 40 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSafeFullRef(string? value)
    {
        const string heads = "refs/heads/";
        const string tags = "refs/tags/";
        if (string.IsNullOrEmpty(value) || value.Length > 256 ||
            (!value.StartsWith(heads, StringComparison.Ordinal) &&
             !value.StartsWith(tags, StringComparison.Ordinal)) ||
            value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains("@{", StringComparison.Ordinal) ||
            value.Contains("//", StringComparison.Ordinal) ||
            value.EndsWith("/", StringComparison.Ordinal) ||
            value.EndsWith(".", StringComparison.Ordinal) ||
            value.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) ||
            value.IndexOfAny(['~', '^', ':', '?', '*', '[', '\\', '+']) >= 0)
        {
            return false;
        }

        string suffix = value.StartsWith(heads, StringComparison.Ordinal)
            ? value[heads.Length..]
            : value[tags.Length..];
        return suffix.Length > 0;
    }
}
