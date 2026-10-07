using FluentValidation;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Shared.OctoberChat;

public sealed class OctoberChatRequestValidator : AbstractValidator<ReadSelectionRequest>
{
    public OctoberChatRequestValidator()
    {
        RuleFor(request => request.Subject).NotNull().WithMessage("对话身份不可用。");
        RuleFor(request => request.Subject == null ? null : request.Subject.SubjectId)
            .NotEmpty().WithMessage("对话身份不可用。");
        RuleFor(request => request.Subject == null ? null : request.Subject.AuthenticationBinding)
            .NotEmpty().WithMessage("对话认证不可用。");
        RuleFor(request => request.Purpose).Equal(KnowledgePurpose.Chat).WithMessage("对话用途无效。");
        RuleFor(request => request.Mode).IsInEnum().WithMessage("对话模式无效。");
        RuleFor(request => request.RepositoryScope)
            .Must(scope => !scope.IsDefaultOrEmpty && scope.All(id => id != Guid.Empty) &&
                scope.Distinct().Count() == scope.Length)
            .WithMessage("请选择有效且不重复的仓库范围。");
        RuleFor(request => request.Publication).Must(publication =>
                publication is { PublicationId: var publicationId, Knowledge: not null } &&
                publicationId != Guid.Empty && publication.Knowledge.BatchId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(publication.Knowledge.CanonicalDatabaseIdentity) &&
                !string.IsNullOrWhiteSpace(publication.Knowledge.TargetId) &&
                !string.IsNullOrWhiteSpace(publication.Knowledge.SelectionDigest))
            .WithMessage("批次或发布选择无效。");
    }
}
