using FluentValidation;
using System.Buffers;
using System.Text;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberKnowledge;

namespace Vulgata.Shared.Explore;

/// <summary>Uses the owner's exact identity rules; presentation never mints a replacement handle.</summary>
public sealed class ExploreDocumentHandleValidator : AbstractValidator<DocumentHandle>
{
    public ExploreDocumentHandleValidator()
    {
        var owner = new ReadKnowledgeRequestValidator();
        RuleFor(document => document).Must(document => owner.Validate(new ReadKnowledgeRequest("presentation-validation", document)).IsValid)
            .WithMessage("文档精确标识或章节标识无效。");
        RuleFor(document => document.SectionId).Must(section => section is null || ExplorePresentationText.IsWellFormed(section))
            .WithMessage("章节标识必须是有效文本。");
    }
}

/// <summary>Reject invalid UTF-16 rather than silently replacing it during UTF-8 or DOM projection.</summary>
public static class ExplorePresentationText
{
    public static bool IsWellFormed(string? value)
    {
        if (value is null) { return false; }
        var remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out _, out var consumed) != OperationStatus.Done) { return false; }
            remaining = remaining[consumed..];
        }

        return true;
    }
}
