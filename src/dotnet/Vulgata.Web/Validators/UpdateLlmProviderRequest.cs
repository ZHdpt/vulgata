using FluentValidation;
using Vulgata.Core.Entities;

namespace Vulgata.Web.Validators;

public sealed class UpdateLlmProviderRequest
{
    public string Name { get; set; } = string.Empty;

    public string BaseEndpointUrl { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public ApiTypeFlags SupportedApiTypes { get; set; }

    public AgentType DefaultAgentType { get; set; }
}

public sealed class UpdateLlmProviderRequestValidator : AbstractValidator<UpdateLlmProviderRequest>
{
    private const ApiTypeFlags _supportedApiTypeMask =
        ApiTypeFlags.ChatCompletions | ApiTypeFlags.Responses | ApiTypeFlags.Messages;

    public UpdateLlmProviderRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("名称不能为空。")
            .MaximumLength(200).WithMessage("名称最长 200 个字符。");

        RuleFor(request => request.BaseEndpointUrl)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("基础地址不能为空。")
            .MaximumLength(500).WithMessage("基础地址最长 500 个字符。")
            .Must(BeValidAbsoluteHttpUrl).WithMessage("基础地址必须是有效的 http 或 https 绝对 URL。");

        RuleFor(request => request.ApiKey)
            .MaximumLength(500).WithMessage("API 密钥最长 500 个字符。");

        RuleFor(request => request.SupportedApiTypes)
            .Must(value => value != ApiTypeFlags.None).WithMessage("至少选择一种 API 类型。")
            .Must(value => (value & ~_supportedApiTypeMask) == 0).WithMessage("支持的 API 类型无效。");

        RuleFor(request => request.DefaultAgentType)
            .IsInEnum().WithMessage("默认代理角色无效。");
    }

    private static bool BeValidAbsoluteHttpUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }
}
