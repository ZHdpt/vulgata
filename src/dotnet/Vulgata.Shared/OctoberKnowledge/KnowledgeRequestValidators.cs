using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using FluentValidation;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Shared.OctoberKnowledge;

public sealed class OpenKnowledgeSelectionRequestValidator : AbstractValidator<OpenKnowledgeSelectionRequest>
{
    public OpenKnowledgeSelectionRequestValidator()
    {
        RuleFor(request => request.BatchId).NotEmpty().WithMessage("批次标识不能为空。");
        RuleFor(request => request.PublicationId).NotEmpty().WithMessage("发布标识不能为空。");
        RuleFor(request => request.Mode).IsInEnum().WithMessage("知识模式无效。");
        RuleFor(request => request.RepositoryScope).Must(KnowledgeRequestValidation.IsValidScope)
            .WithMessage("仓库范围必须明确，标识不能为空或重复。");
    }
}

public sealed class SearchKnowledgeRequestValidator : AbstractValidator<SearchKnowledgeRequest>
{
    public SearchKnowledgeRequestValidator()
    {
        RuleFor(request => request.SelectionToken).NotEmpty().WithMessage("读取选择不能为空。");
        RuleFor(request => request.Query).Must(KnowledgeRequestValidation.IsValidQuery)
            .WithMessage("搜索词必须是有效文本，且不能超过 512 个 Unicode 字符。");
        RuleFor(request => request.PageSize).InclusiveBetween(1, KnowledgeContract.MaximumPageSize)
            .WithMessage("每页数量必须在 1 到 100 之间。");
        RuleFor(request => request.Continuation).Must(KnowledgeRequestValidation.IsValidOptionalToken)
            .WithMessage("续读标记不能是空白文本。");
    }
}

public sealed class ReadKnowledgeRequestValidator : AbstractValidator<ReadKnowledgeRequest>
{
    public ReadKnowledgeRequestValidator()
    {
        RuleFor(request => request.SelectionToken).NotEmpty().WithMessage("读取选择不能为空。");
        RuleFor(request => request.Document).Must(KnowledgeRequestValidation.IsValidDocument)
            .WithMessage("文档精确标识或章节标识无效。");
        RuleFor(request => request.Continuation).Must(KnowledgeRequestValidation.IsValidOptionalToken)
            .WithMessage("续读标记不能是空白文本。");
    }
}

public sealed class RelatedKnowledgeRequestValidator : AbstractValidator<RelatedKnowledgeRequest>
{
    public RelatedKnowledgeRequestValidator()
    {
        RuleFor(request => request.SelectionToken).NotEmpty().WithMessage("读取选择不能为空。");
        RuleFor(request => request.Document).Must(KnowledgeRequestValidation.IsValidDocument)
            .WithMessage("文档精确标识或章节标识无效。");
        RuleFor(request => request.PageSize).InclusiveBetween(1, KnowledgeContract.MaximumPageSize)
            .WithMessage("每页数量必须在 1 到 100 之间。");
        RuleFor(request => request.Continuation).Must(KnowledgeRequestValidation.IsValidOptionalToken)
            .WithMessage("续读标记不能是空白文本。");
    }
}

internal static class KnowledgeRequestValidation
{
    internal static bool IsValidScope(ImmutableArray<Guid> scope) =>
        !scope.IsDefault && scope.All(id => id != Guid.Empty) && scope.Distinct().Count() == scope.Length;

    internal static bool IsValidOptionalToken(string? token) => token is null || !string.IsNullOrWhiteSpace(token);

    internal static bool IsValidDocument(DocumentHandle? document) =>
        document is not null && document.BatchId != Guid.Empty && document.PublicationId != Guid.Empty &&
        document.LogicalDocumentId != Guid.Empty && document.DocumentVersionId != Guid.Empty &&
        IsValidOptionalToken(document.SectionId);

    // Count Unicode scalars, not UTF-16 code units. Do not silently replace malformed surrogates.
    internal static bool IsValidQuery(string? query)
    {
        if (query is null)
        {
            return false;
        }

        ReadOnlySpan<char> remaining = query.AsSpan();
        int characters = 0;
        while (!remaining.IsEmpty)
        {
            if (++characters > KnowledgeContract.MaximumQueryCharacters ||
                Rune.DecodeFromUtf16(remaining, out _, out int consumed) != OperationStatus.Done)
            {
                return false;
            }

            remaining = remaining[consumed..];
        }

        return true;
    }
}
