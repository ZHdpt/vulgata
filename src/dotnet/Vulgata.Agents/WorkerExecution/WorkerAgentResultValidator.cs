using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Agents.WorkerExecution;

internal static class WorkerAgentResultValidator
{
    internal static WorkerAgentResultEnvelope ValidatePass1(
        WorkerAgentJobPacket packet,
        WorkerAgentPass1ResponseDto response,
        DateTimeOffset dispatchedAt,
        DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(response.Content);
        ArgumentNullException.ThrowIfNull(response.Content.Sections);
        ArgumentNullException.ThrowIfNull(response.SourceAnchors);
        ArgumentNullException.ThrowIfNull(response.ReferenceDocumentVersionIds);
        ArgumentNullException.ThrowIfNull(response.CommunicationEvidenceIds);

        CanonicalSection[] sections = response.Content.Sections
            .Select(section => new CanonicalSection(
                section.StableSectionId,
                section.Order,
                section.Kind,
                section.Heading,
                section.Markdown,
                section.SearchText,
                section.SourceAnchorIds,
                section.DocumentVersionIds))
            .ToArray();
        var content = new CanonicalDocumentContent(
            response.Content.SchemaIdentity,
            response.Content.DocumentType,
            response.Content.StableKey,
            response.Content.Title,
            response.Content.RepositoryRelativePath,
            response.Content.Summary,
            response.Content.KeySymbols,
            response.Content.BusinessTerms,
            response.Content.TechnicalTerms,
            sections);
        WorkerAgentSourceAnchor[] anchors = response.SourceAnchors
            .Select(anchor => WorkerAgentSourceAnchor.Create(
                anchor.Id,
                anchor.RepositoryRelativePath,
                anchor.EvidenceLocator,
                anchor.LineStart,
                anchor.LineEnd,
                anchor.SourceHash))
            .ToArray();
        Pass1CodeLogicDocumentResultPayload payload =
            Pass1CodeLogicDocumentResultPayload.Create(
                response.JobId,
                response.SystemId,
                response.RepositoryId,
                response.ScanRunId,
                response.CodeUnitId,
                response.ProfiledRevision,
                response.RepositoryRelativePath,
                response.LineStart,
                response.LineEnd,
                response.SourceHash,
                response.FileIdentityHash,
                content,
                anchors,
                response.ReferenceDocumentVersionIds,
                response.CommunicationEvidenceIds);

        return WorkerAgentResultEnvelope.CreateForJob(
            packet,
            WorkerAgentResultOutcome.Valid,
            boundedReason: null,
            dispatchedAt,
            receivedAt,
            payload);
    }

    internal static WorkerAgentResultEnvelope ValidatePass2(
        WorkerAgentJobPacket packet,
        WorkerAgentPass2ResponseDto response,
        DateTimeOffset dispatchedAt,
        DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(response.MainSteps);
        ArgumentNullException.ThrowIfNull(response.Content);
        ArgumentNullException.ThrowIfNull(response.Content.Sections);
        ArgumentNullException.ThrowIfNull(response.SourceAnchors);
        ArgumentNullException.ThrowIfNull(response.TraceEvidenceOrders);
        ArgumentNullException.ThrowIfNull(response.GeneratedFromDocumentVersionIds);

        WorkerAgentBusinessMainStep[] mainSteps = response.MainSteps
            .Select(step => WorkerAgentBusinessMainStep.Create(
                step.Order,
                step.Description,
                step.TraceEvidenceOrders))
            .ToArray();
        CanonicalSection[] sections = response.Content.Sections
            .Select(section => new CanonicalSection(
                section.StableSectionId,
                section.Order,
                section.Kind,
                section.Heading,
                section.Markdown,
                section.SearchText,
                section.SourceAnchorIds,
                section.DocumentVersionIds))
            .ToArray();
        var content = new CanonicalDocumentContent(
            response.Content.SchemaIdentity,
            response.Content.DocumentType,
            response.Content.StableKey,
            response.Content.Title,
            response.Content.RepositoryRelativePath,
            response.Content.Summary,
            response.Content.KeySymbols,
            response.Content.BusinessTerms,
            response.Content.TechnicalTerms,
            sections);
        WorkerAgentSourceAnchor[] anchors = response.SourceAnchors
            .Select(anchor => WorkerAgentSourceAnchor.Create(
                anchor.Id,
                anchor.RepositoryRelativePath,
                anchor.EvidenceLocator,
                anchor.LineStart,
                anchor.LineEnd,
                anchor.SourceHash))
            .ToArray();
        Pass2BusinessLogicDocumentResultPayload payload =
            Pass2BusinessLogicDocumentResultPayload.Create(
                response.JobId,
                response.SystemId,
                response.RepositoryId,
                response.ScanRunId,
                response.ProfiledRevision,
                response.FlowId,
                response.Purpose,
                mainSteps,
                response.Outcome,
                content,
                anchors,
                response.TraceEvidenceOrders,
                response.GeneratedFromDocumentVersionIds);

        return WorkerAgentResultEnvelope.CreateForJob(
            packet,
            WorkerAgentResultOutcome.Valid,
            boundedReason: null,
            dispatchedAt,
            receivedAt,
            payload);
    }
}
