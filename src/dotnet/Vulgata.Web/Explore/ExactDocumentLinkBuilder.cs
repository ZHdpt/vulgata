using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.Explore;
using System.Text;

namespace Vulgata.Web.Explore;

public static class ExactDocumentLinkBuilder
{
    public static string Build(DocumentHandle document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!new ExploreDocumentHandleValidator().Validate(document).IsValid)
        {
            throw new ArgumentException("文档精确标识无效。", nameof(document));
        }

        var link = $"/knowledge/documents/read?batch={document.BatchId:D}&publication={document.PublicationId:D}&logical={document.LogicalDocumentId:D}&version={document.DocumentVersionId:D}";
        return document.SectionId is null ? link : link + "&section=" + Uri.EscapeDataString(document.SectionId);
    }

    public static string GetSectionAnchor(string sectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        if (!ExplorePresentationText.IsWellFormed(sectionId))
        {
            throw new ArgumentException("章节标识必须是有效文本。", nameof(sectionId));
        }
        return "section-" + Convert.ToHexString(Encoding.UTF8.GetBytes(sectionId));
    }
}
