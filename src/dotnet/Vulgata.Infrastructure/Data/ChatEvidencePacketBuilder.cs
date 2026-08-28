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
                    packed.Relationships,
                    packed.FlowEvidence,
                    packed.FlowGroups,
                    request.AdvisoryContext,
                    request.Conversation,
                    request.AdvisoryContextAuthorityFingerprint,
                    request.PrimaryDocumentHandles,
                    request.PrimarySectionHandles.Where(handle => retainedSections.Any(
                        section => string.Equals(
                            section.SectionHandle,
                            handle,
                            StringComparison.Ordinal))).ToArray(),
                    request.ExpansionSeedDocumentHandles);
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
            .ToHashSet(StringComparer.Ordinal);

        EvidenceRelationship[] retainedRelationships = request.Relationships
            .Where(relationship =>
                retainedDocumentHandles.Contains(relationship.SourceDocumentHandle) &&
                retainedDocumentHandles.Contains(relationship.TargetDocumentHandle))
            .ToArray();
        HashSet<string> retainedRelationshipHandles = retainedRelationships
            .Select(relationship => relationship.RelationshipHandle)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<Guid> droppedRelationshipEdgeIds = request.Relationships
            .Where(relationship => !retainedRelationshipHandles.Contains(relationship.RelationshipHandle))
            .Select(relationship => relationship.EdgeId)
            .ToHashSet();
        EvidenceFlowGroupAuthority[] retainedFlowGroups = request.FlowGroups
            .Where(group => !droppedRelationshipEdgeIds.Contains(group.ExactResolutionEdgeId))
            .ToArray();

        HashSet<Guid> authorityDocumentVersionIds = retainedFlowGroups
            .Select(group => group.BusinessLogicDocumentVersionId)
            .Concat(retainedFlowGroups.SelectMany(group =>
                group.Steps.Select(step => step.DocumentVersionId)))
            .ToHashSet();
        EvidenceDocument[] retainedDocuments = request.Documents
            .Where(document =>
                retainedDocumentHandles.Contains(document.DocumentHandle) ||
                authorityDocumentVersionIds.Contains(document.DocumentVersionId))
            .ToArray();
        HashSet<string> packedDocumentHandles = retainedDocuments
            .Select(document => document.DocumentHandle)
            .ToHashSet(StringComparer.Ordinal);
        EvidenceFlowStep[] retainedFlowEvidence = request.FlowEvidence
            .Where(step =>
                packedDocumentHandles.Contains(step.DocumentHandle) &&
                (step.RelationshipHandle is null || retainedRelationshipHandles.Contains(step.RelationshipHandle)))
            .Select((step, index) => step with { StepOrder = index + 1 })
            .ToArray();

        return new PackedEvidence(
            retainedDocuments,
            retainedSections,
            retainedAnchors,
            retainedRelationships,
            retainedFlowEvidence,
            retainedFlowGroups);
    }

    private sealed record PackedEvidence(
        IReadOnlyCollection<EvidenceDocument> Documents,
        IReadOnlyCollection<EvidenceSection> Sections,
        IReadOnlyCollection<EvidenceAnchor> SourceAnchors,
        IReadOnlyCollection<EvidenceRelationship> Relationships,
        IReadOnlyCollection<EvidenceFlowStep> FlowEvidence,
        IReadOnlyCollection<EvidenceFlowGroupAuthority> FlowGroups);
}
