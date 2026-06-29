using FluentValidation;
using Vulgata.Core.Entities;

namespace Vulgata.Web.Validators;

public sealed class UpsertSystemLlmProviderOverrideRequest
{
    public Guid SystemId { get; set; }

    public Guid LlmProviderId { get; set; }

    public AgentType AgentType { get; set; }
}

public sealed class UpsertSystemLlmProviderOverrideRequestValidator : AbstractValidator<UpsertSystemLlmProviderOverrideRequest>
{
    public UpsertSystemLlmProviderOverrideRequestValidator()
    {
        RuleFor(request => request.SystemId)
            .NotEmpty()
            .WithMessage("系统标识不能为空。");

        RuleFor(request => request.LlmProviderId)
            .NotEmpty()
            .WithMessage("LLM 提供商标识不能为空。");

        RuleFor(request => request.AgentType)
            .IsInEnum()
            .WithMessage("默认代理角色无效。");
    }
}
