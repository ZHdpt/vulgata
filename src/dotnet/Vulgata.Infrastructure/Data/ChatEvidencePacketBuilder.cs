using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Data;

public sealed class ChatEvidencePacketBuilder
{
    public EvidencePacket Build(ChatEvidencePacketBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EvidenceSection[] retainedSections = request.Sections.ToArray();
        ChatContractException? lastFailure = null;
        foreach (string? optionalHandle in new string?[] { null }.Concat(
                     request.OptionalSectionHandles.OrderDescending(StringComparer.Ordinal)))
        {
            if (optionalHandle is not null)
            {
                retainedSections = retainedSections
                    .Where(section => !string.Equals(
                        section.SectionHandle,
                        optionalHandle,
                        StringComparison.Ordinal))
                    .ToArray();
            }

            PackedEvidence packed = PackCompleteUnits(request, retainedSections);
            try
            {
                return EvidencePacket.Create(
                    request.RequestId,
                    request.Mode,
                    request.SystemId,
                    request.RepositoryIds,
                    packed.Documents,
                    packed.Sections,
                    packed.SourceAnchors,
                    request.Relationships,
                    request.FlowEvidence,
                    request.FlowGroups,
                    request.AdvisoryContext,
                    request.Conversation);
            }
            catch (ChatContractException exception) when (
                exception.FailureCode == ChatContractFailureCode.InvalidEvidence)
            {
                lastFailure = exception;
            }
        }

        throw lastFailure ?? new ChatContractException(ChatContractFailureCode.InvalidEvidence);
    }

    private static PackedEvidence PackCompleteUnits(
        ChatEvidencePacketBuildRequest request,
        EvidenceSection[] retainedSections)
    {
        HashSet<string> retainedAnchorHandles = retainedSections
            .SelectMany(section => section.AnchorHandles)
            .ToHashSet(StringComparer.Ordinal);
        EvidenceAnchor[] retainedAnchors = request.SourceAnchors
            .Where(anchor => retainedAnchorHandles.Contains(anchor.AnchorHandle))
            .ToArray();

        HashSet<string> retainedDocumentHandles = retainedSections
            .Select(section => section.DocumentHandle)
            .Concat(retainedSections.SelectMany(section => section.SupportingDocumentHandles))
            .Concat(request.Relationships.SelectMany(relationship => new[]
            {
                relationship.SourceDocumentHandle,
                relationship.TargetDocumentHandle,
            }))
            .Concat(request.FlowEvidence.Select(step => step.DocumentHandle))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<Guid> authorityDocumentVersionIds = request.FlowGroups
            .Select(group => group.BusinessLogicDocumentVersionId)
            .Concat(request.FlowGroups.SelectMany(group =>
                group.Steps.Select(step => step.DocumentVersionId)))
            .ToHashSet();
        EvidenceDocument[] retainedDocuments = request.Documents
            .Where(document =>
                retainedDocumentHandles.Contains(document.DocumentHandle) ||
                authorityDocumentVersionIds.Contains(document.DocumentVersionId))
            .ToArray();

        return new PackedEvidence(
            retainedDocuments,
            retainedSections,
            retainedAnchors);
    }

    private sealed record PackedEvidence(
        IReadOnlyCollection<EvidenceDocument> Documents,
        IReadOnlyCollection<EvidenceSection> Sections,
        IReadOnlyCollection<EvidenceAnchor> SourceAnchors);
}
