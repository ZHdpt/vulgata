using FluentValidation;
using Vulgata.Core.Entities;

namespace Vulgata.Web.Validators;

public sealed class UpsertDatabaseConnectionRequest
{
    public DatabaseType DatabaseType { get; set; }

    public string ConnectionString { get; set; } = string.Empty;

    public string? Username { get; set; }

    public string? Password { get; set; }
}

public sealed class UpsertDatabaseConnectionRequestValidator : AbstractValidator<UpsertDatabaseConnectionRequest>
{
    public UpsertDatabaseConnectionRequestValidator()
    {
        RuleFor(request => request.DatabaseType)
            .IsInEnum()
            .WithMessage("数据库类型无效。")
            .Must(type => Enum.IsDefined(type))
            .WithMessage("数据库类型无效。");

        RuleFor(request => request.ConnectionString)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("连接字符串不能为空。")
            .MaximumLength(4000).WithMessage("连接字符串最长 4000 个字符。");

        RuleFor(request => request.Username)
            .MaximumLength(500).WithMessage("用户名最长 500 个字符。");

        RuleFor(request => request.Password)
            .MaximumLength(500).WithMessage("密码最长 500 个字符。");
    }
}
