using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class WorkerAgentResultCommitter : IWorkerAgentResultCommitter
{
    private readonly IKnowledgeStoreRepository _knowledgeStore;
    private readonly PreparedPass2Flow? _preparedPass2Flow;

    public WorkerAgentResultCommitter(IKnowledgeStoreRepository knowledgeStore)
    {
        _knowledgeStore = knowledgeStore ??
            throw new ArgumentNullException(nameof(knowledgeStore));
    }

    public WorkerAgentResultCommitter(
        IKnowledgeStoreRepository knowledgeStore,
        PreparedPass2Flow preparedPass2Flow)
        : this(knowledgeStore)
    {
        _preparedPass2Flow = preparedPass2Flow ??
            throw new ArgumentNullException(nameof(preparedPass2Flow));
    }

    public async Task<WorkerAgentResultCommitOutcome> CommitAsync(
        WorkerAgentJobPacket packet,
        WorkerAgentResultEnvelope validResult,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(validResult);
        ArgumentNullException.ThrowIfNull(scope);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (packet.Kind == WorkerAgentJobKind.Pass2BusinessLogicDocument)
            {
                return await CommitPass2Async(
                    packet,
                    validResult,
                    scope,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!TryValidatePass1(
                    packet,
                    validResult,
                    scope,
                    out Pass1CodeLogicJobPayload job,
                    out Pass1CodeLogicDocumentResultPayload result))
            {
                return WorkerAgentResultCommitOutcome.Rejected;
            }

            Guid logicalDocumentId;
            if (_preparedPass2Flow is null)
            {
                logicalDocumentId = CreateStableId(
                    "pass1-logical-document",
                    packet.RepositoryId.ToString("D"),
                    result.Content.StableKey);
            }
            else
            {
                PreparedPass2FlowMember? member = _preparedPass2Flow.Members
                    .SingleOrDefault(candidate => candidate.CodeUnitId == packet.CodeUnitId);
                if (_preparedPass2Flow.SystemId != packet.SystemId ||
                    _preparedPass2Flow.RepositoryId != packet.RepositoryId ||
                    member is null ||
                    member.JobId != packet.JobId)
                {
                    return WorkerAgentResultCommitOutcome.Rejected;
                }

                logicalDocumentId = member.LogicalDocumentId;
            }
            Guid documentVersionId = _preparedPass2Flow is null
                ? CreateStableId(
                    "pass1-document-version",
                    packet.JobId.ToString("D"))
                : packet.JobId;
            Guid operationId = CreateStableId(
                "pass1-operation",
                packet.JobId.ToString("D"));
            CurrentKnowledge? current = await _knowledgeStore.GetCurrentAsync(
                logicalDocumentId,
                scope,
                result.Content.Sections.Select(section => section.StableSectionId).ToArray(),
                cancellationToken).ConfigureAwait(false);
            if (current?.DocumentVersionId == documentVersionId)
            {
                return CurrentMatchesReplay(current, packet, result)
                    ? WorkerAgentResultCommitOutcome.Committed
                    : WorkerAgentResultCommitOutcome.Conflict;
            }

            Guid? expectedCurrentVersionId = current?.DocumentVersionId;

            SourceAnchorDraft[] anchors = result.SourceAnchors
                .Select(anchor => new SourceAnchorDraft(
                    anchor.Id,
                    documentVersionId,
                    packet.RepositoryId,
                    packet.ScanRunId,
                    packet.CodeUnitId,
                    packet.ProfiledRevision,
                    anchor.RepositoryRelativePath,
                    anchor.EvidenceLocator,
                    anchor.LineStart,
                    anchor.LineEnd,
                    anchor.SourceHash))
                .ToArray();
            EdgeDraft[] edges = result.ReferenceDocumentVersionIds
                .Select(targetDocumentVersionId => new EdgeDraft(
                    CreateStableId(
                        "pass1-reference-edge",
                        documentVersionId.ToString("D"),
                        targetDocumentVersionId.ToString("D")),
                    documentVersionId,
                    packet.RepositoryId,
                    packet.ScanRunId,
                    targetDocumentVersionId,
                    packet.RepositoryId,
                    packet.ScanRunId,
                    DocumentEdgeType.References))
                .ToArray();
            KnowledgeCommit commit = new(
                operationId,
                logicalDocumentId,
                expectedCurrentVersionId,
                documentVersionId,
                packet.RepositoryId,
                packet.ScanRunId,
                packet.ProfiledRevision,
                result.Content,
                anchors,
                edges,
                [],
                validResult.ReceivedAt);

            KnowledgeCommitResult committed = await _knowledgeStore.CommitAsync(
                commit,
                scope,
                cancellationToken).ConfigureAwait(false);
            return committed.Status switch
            {
                KnowledgeCommitStatus.Committed => WorkerAgentResultCommitOutcome.Committed,
                KnowledgeCommitStatus.Conflict => WorkerAgentResultCommitOutcome.Conflict,
                KnowledgeCommitStatus.Rejected => WorkerAgentResultCommitOutcome.Rejected,
                _ => WorkerAgentResultCommitOutcome.Failed,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return WorkerAgentResultCommitOutcome.Cancelled;
        }
        catch (Exception)
        {
            return WorkerAgentResultCommitOutcome.Failed;
        }
    }

    private async Task<WorkerAgentResultCommitOutcome> CommitPass2Async(
        WorkerAgentJobPacket packet,
        WorkerAgentResultEnvelope envelope,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken)
    {
        if (!TryValidatePass2(
                packet,
                envelope,
                scope,
                out Pass2BusinessLogicJobPayload job,
                out Pass2BusinessLogicDocumentResultPayload result))
        {
            return WorkerAgentResultCommitOutcome.Rejected;
        }

        var currentInputs = new CurrentKnowledge[job.CodeLogicDocumentVersions.Count];
        for (int index = 0; index < job.CodeLogicDocumentVersions.Count; index++)
        {
            WorkerAgentCodeLogicDocumentVersionProjection projection =
                job.CodeLogicDocumentVersions[index];
            CurrentKnowledge? current = await _knowledgeStore.GetCurrentAsync(
                projection.LogicalDocumentId,
                scope,
                projection.Sections.Select(section => section.StableSectionId).ToArray(),
                cancellationToken).ConfigureAwait(false);
            if (current is null || !CurrentMatchesPass2Input(current, packet, projection))
            {
                return WorkerAgentResultCommitOutcome.Rejected;
            }

            currentInputs[index] = current;
        }

        Guid logicalDocumentId = CreateStableId(
            "pass2-logical-document",
            packet.RepositoryId.ToString("D"),
            job.FlowId.ToString("D"));
        Guid documentVersionId = CreateStableId(
            "pass2-document-version",
            packet.JobId.ToString("D"));
        Guid operationId = CreateStableId(
            "pass2-operation",
            packet.JobId.ToString("D"));
        CurrentKnowledge? currentBusiness = await _knowledgeStore.GetCurrentAsync(
            logicalDocumentId,
            scope,
            result.Content.Sections.Select(section => section.StableSectionId).ToArray(),
            cancellationToken).ConfigureAwait(false);
        if (currentBusiness?.DocumentVersionId == documentVersionId)
        {
            return CurrentMatchesPass2Replay(currentBusiness, packet, result)
                ? WorkerAgentResultCommitOutcome.Committed
                : WorkerAgentResultCommitOutcome.Conflict;
        }

        Dictionary<Guid, CurrentSourceAnchor> inputAnchors = currentInputs
            .SelectMany(current => current.SourceAnchors)
            .ToDictionary(anchor => anchor.Id);
        Dictionary<Guid, Guid> durableAnchorIds = result.SourceAnchors
            .ToDictionary(
                anchor => anchor.Id,
                anchor => CreateStableId(
                    "pass2-source-anchor",
                    documentVersionId.ToString("D"),
                    anchor.Id.ToString("D")));
        SourceAnchorDraft[] anchors = result.SourceAnchors
            .Select(anchor => new SourceAnchorDraft(
                durableAnchorIds[anchor.Id],
                documentVersionId,
                packet.RepositoryId,
                packet.ScanRunId,
                inputAnchors[anchor.Id].CodeUnitId,
                packet.ProfiledRevision,
                anchor.RepositoryRelativePath,
                anchor.EvidenceLocator,
                anchor.LineStart,
                anchor.LineEnd,
                anchor.SourceHash))
            .ToArray();
        EdgeDraft[] edges = result.GeneratedFromDocumentVersionIds
            .Select(targetDocumentVersionId => new EdgeDraft(
                CreateStableId(
                    "pass2-generated-from-edge",
                    documentVersionId.ToString("D"),
                    targetDocumentVersionId.ToString("D")),
                documentVersionId,
                packet.RepositoryId,
                packet.ScanRunId,
                targetDocumentVersionId,
                packet.RepositoryId,
                packet.ScanRunId,
                DocumentEdgeType.GeneratedFrom))
            .ToArray();
        Dictionary<Guid, EdgeDraft> edgesByTarget =
            edges.ToDictionary(edge => edge.TargetDocumentVersionId);
        FlowTraceDraft[] traces = job.TraceEvidence
            .Select(evidence =>
            {
                WorkerAgentCodeLogicDocumentVersionProjection target =
                    job.CodeLogicDocumentVersions.Single(projection =>
                        projection.SourceAnchors.Any(anchor =>
                            inputAnchors[anchor.Id].CodeUnitId == evidence.CodeUnitId));
                EdgeDraft edge = edgesByTarget[target.DocumentVersionId];
                return new FlowTraceDraft(
                    CreateStableId(
                        "pass2-flow-trace",
                        documentVersionId.ToString("D"),
                        evidence.Order.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    evidence.Order,
                    [
                        new FlowTraceStepDraft(
                            CreateStableId(
                                "pass2-flow-step-source",
                                documentVersionId.ToString("D"),
                                evidence.Order.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                            1,
                            documentVersionId,
                            packet.RepositoryId,
                            packet.ScanRunId,
                            null,
                            FlowTraceStepKind.Document,
                            result.Purpose),
                        new FlowTraceStepDraft(
                            CreateStableId(
                                "pass2-flow-step-evidence",
                                documentVersionId.ToString("D"),
                                evidence.Order.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                            2,
                            target.DocumentVersionId,
                            packet.RepositoryId,
                            packet.ScanRunId,
                            edge.Id,
                            FlowTraceStepKind.Relationship,
                            $"{evidence.Label}: {evidence.Locator}"),
                    ]);
            })
            .ToArray();
        CanonicalDocumentContent durableContent = result.Content with
        {
            Sections = result.Content.Sections
                .Select(section => section with
                {
                    SourceAnchorIds = section.SourceAnchorIds
                        .Select(id => durableAnchorIds[id])
                        .Order()
                        .ToArray(),
                })
                .ToArray(),
        };
        FlowGroupDraft[] flowGroups =
        [
            new FlowGroupDraft(
                CreateStableId(
                    "pass2-flow-group",
                    documentVersionId.ToString("D"),
                    job.FlowId.ToString("D")),
                1,
                result.Content.Title,
                traces),
        ];
        KnowledgeCommit commit = new(
            operationId,
            logicalDocumentId,
            currentBusiness?.DocumentVersionId,
            documentVersionId,
            packet.RepositoryId,
            packet.ScanRunId,
            packet.ProfiledRevision,
            durableContent,
            anchors,
            edges,
            flowGroups,
            envelope.ReceivedAt);
        KnowledgeCommitResult committed = await _knowledgeStore.CommitAsync(
            commit,
            scope,
            cancellationToken).ConfigureAwait(false);
        return committed.Status switch
        {
            KnowledgeCommitStatus.Committed => WorkerAgentResultCommitOutcome.Committed,
            KnowledgeCommitStatus.Conflict => WorkerAgentResultCommitOutcome.Conflict,
            KnowledgeCommitStatus.Rejected => WorkerAgentResultCommitOutcome.Rejected,
            _ => WorkerAgentResultCommitOutcome.Failed,
        };
    }

    private static bool TryValidatePass2(
        WorkerAgentJobPacket packet,
        WorkerAgentResultEnvelope envelope,
        RepositoryAccessScope scope,
        out Pass2BusinessLogicJobPayload job,
        out Pass2BusinessLogicDocumentResultPayload result)
    {
        job = packet.Payload as Pass2BusinessLogicJobPayload ?? null!;
        result = envelope.Payload as Pass2BusinessLogicDocumentResultPayload ?? null!;
        if (packet.Kind != WorkerAgentJobKind.Pass2BusinessLogicDocument ||
            envelope.Outcome != WorkerAgentResultOutcome.Valid ||
            envelope.Kind != packet.Kind ||
            envelope.JobId != packet.JobId ||
            envelope.ScanExecutionId != packet.ScanExecutionId ||
            job is null ||
            result is null ||
            !scope.Contains(packet.RepositoryId) ||
            result.JobId != packet.JobId ||
            result.SystemId != packet.SystemId ||
            result.RepositoryId != packet.RepositoryId ||
            result.ScanRunId != packet.ScanRunId ||
            !StringComparer.Ordinal.Equals(result.ProfiledRevision, packet.ProfiledRevision) ||
            result.FlowId != job.FlowId ||
            result.Content.DocumentType != DocumentType.BusinessLogic ||
            !StringComparer.Ordinal.Equals(
                result.Content.SchemaIdentity,
                KnowledgeDocumentLimits.SchemaIdentity))
        {
            return false;
        }

        Guid[] expectedTargets = job.CodeLogicDocumentVersions
            .Select(document => document.DocumentVersionId)
            .ToArray();
        int[] expectedEvidenceOrders = job.TraceEvidence
            .Select(evidence => evidence.Order)
            .ToArray();
        WorkerAgentSourceAnchor[] expectedAnchors = job.CodeLogicDocumentVersions
            .SelectMany(document => document.SourceAnchors)
            .OrderBy(anchor => anchor.Id)
            .ToArray();
        if (!result.GeneratedFromDocumentVersionIds.SequenceEqual(expectedTargets) ||
            !result.TraceEvidenceOrders.SequenceEqual(expectedEvidenceOrders) ||
            result.SourceAnchors.Count != expectedAnchors.Length ||
            result.SourceAnchors.Where((anchor, index) =>
                !SourceAnchorEquals(anchor, expectedAnchors[index])).Any() ||
            result.MainSteps.Count == 0 ||
            !result.MainSteps.Select(step => step.Order)
                .SequenceEqual(Enumerable.Range(1, result.MainSteps.Count)) ||
            !result.MainSteps.SelectMany(step => step.TraceEvidenceOrders)
                .Distinct()
                .Order()
                .SequenceEqual(expectedEvidenceOrders))
        {
            return false;
        }

        HashSet<Guid> anchorIds = result.SourceAnchors.Select(anchor => anchor.Id).ToHashSet();
        HashSet<Guid> targetIds = expectedTargets.ToHashSet();
        HashSet<Guid> sectionIds = [];
        for (int index = 0; index < result.Content.Sections.Count; index++)
        {
            CanonicalSection section = result.Content.Sections[index];
            if (!sectionIds.Add(section.StableSectionId) ||
                section.Order != index + 1 ||
                !IsStrictlyAscending(section.SourceAnchorIds) ||
                !IsStrictlyAscending(section.DocumentVersionIds) ||
                section.SourceAnchorIds.Any(id => !anchorIds.Contains(id)) ||
                section.DocumentVersionIds.Any(id => !targetIds.Contains(id)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SourceAnchorEquals(
        WorkerAgentSourceAnchor left,
        WorkerAgentSourceAnchor right) =>
        left.Id == right.Id &&
        StringComparer.Ordinal.Equals(left.RepositoryRelativePath, right.RepositoryRelativePath) &&
        StringComparer.Ordinal.Equals(left.EvidenceLocator, right.EvidenceLocator) &&
        left.LineStart == right.LineStart &&
        left.LineEnd == right.LineEnd &&
        StringComparer.Ordinal.Equals(left.SourceHash, right.SourceHash);

    private static bool CurrentMatchesPass2Input(
        CurrentKnowledge current,
        WorkerAgentJobPacket packet,
        WorkerAgentCodeLogicDocumentVersionProjection projection)
    {
        if (current.LogicalDocumentId != projection.LogicalDocumentId ||
            current.DocumentVersionId != projection.DocumentVersionId ||
            current.RepositoryId != packet.RepositoryId ||
            current.DocumentType != DocumentType.CodeLogic ||
            !StringComparer.Ordinal.Equals(current.Title, projection.Title) ||
            !StringComparer.Ordinal.Equals(
                current.RepositoryRelativePath,
                projection.RepositoryRelativePath) ||
            !StringComparer.Ordinal.Equals(current.ContentMarkdown, projection.ContentMarkdown) ||
            current.SourceAnchors.Count != projection.SourceAnchors.Count ||
            current.Sections.Count != projection.Sections.Count)
        {
            return false;
        }

        Dictionary<Guid, CurrentSourceAnchor> currentAnchors =
            current.SourceAnchors.ToDictionary(anchor => anchor.Id);
        if (projection.SourceAnchors.Any(anchor =>
                !currentAnchors.TryGetValue(anchor.Id, out CurrentSourceAnchor? durable) ||
                !StringComparer.Ordinal.Equals(
                    durable.RepositoryRelativePath,
                    anchor.RepositoryRelativePath) ||
                !StringComparer.Ordinal.Equals(durable.EvidenceLocator, anchor.EvidenceLocator) ||
                durable.LineStart != anchor.LineStart ||
                durable.LineEnd != anchor.LineEnd ||
                !StringComparer.Ordinal.Equals(durable.SourceHash, anchor.SourceHash)))
        {
            return false;
        }

        Dictionary<Guid, CurrentDocumentSection> currentSections =
            current.Sections.ToDictionary(section => section.StableSectionId);
        return projection.Sections.All(section =>
            currentSections.TryGetValue(section.StableSectionId, out CurrentDocumentSection? durable) &&
            durable.SectionOrder == section.SectionOrder &&
            durable.SectionKind == section.SectionKind &&
            StringComparer.Ordinal.Equals(durable.Heading, section.Heading) &&
            StringComparer.Ordinal.Equals(durable.ContentMarkdown, section.ContentMarkdown) &&
            StringComparer.Ordinal.Equals(durable.SearchText, section.SearchText) &&
            durable.SourceAnchorIds.SequenceEqual(section.SourceAnchorIds) &&
            durable.AuthorizedDocumentVersionIds.SequenceEqual(section.AuthorizedDocumentVersionIds) &&
            durable.HasRestrictedDocumentReference == section.HasRestrictedDocumentReference);
    }

    private static bool CurrentMatchesPass2Replay(
        CurrentKnowledge current,
        WorkerAgentJobPacket packet,
        Pass2BusinessLogicDocumentResultPayload result)
    {
        Guid[] durableTargets = current.Relationships
            .OfType<AuthorizedRelationship>()
            .Where(relationship => relationship.EdgeType == DocumentEdgeType.GeneratedFrom)
            .Select(relationship => relationship.TargetDocumentVersionId)
            .Order()
            .ToArray();
        return current.RepositoryId == packet.RepositoryId &&
            current.DocumentType == DocumentType.BusinessLogic &&
            StringComparer.Ordinal.Equals(current.Title, result.Content.Title) &&
            StringComparer.Ordinal.Equals(
                current.RepositoryRelativePath,
                result.Content.RepositoryRelativePath) &&
            current.SourceAnchors.Count == result.SourceAnchors.Count &&
            current.Sections.Count == result.Content.Sections.Count &&
            current.FlowGroups.Count == 1 &&
            durableTargets.SequenceEqual(result.GeneratedFromDocumentVersionIds);
    }

    private static bool TryValidatePass1(
        WorkerAgentJobPacket packet,
        WorkerAgentResultEnvelope envelope,
        RepositoryAccessScope scope,
        out Pass1CodeLogicJobPayload job,
        out Pass1CodeLogicDocumentResultPayload result)
    {
        job = packet.Payload as Pass1CodeLogicJobPayload ?? null!;
        result = envelope.Payload as Pass1CodeLogicDocumentResultPayload ?? null!;
        if (packet.Kind != WorkerAgentJobKind.Pass1CodeLogicDocument ||
            envelope.Outcome != WorkerAgentResultOutcome.Valid ||
            envelope.Kind != packet.Kind ||
            envelope.JobId != packet.JobId ||
            envelope.ScanExecutionId != packet.ScanExecutionId ||
            job is null ||
            result is null ||
            !scope.Contains(packet.RepositoryId) ||
            result.JobId != packet.JobId ||
            result.SystemId != packet.SystemId ||
            result.RepositoryId != packet.RepositoryId ||
            result.ScanRunId != packet.ScanRunId ||
            result.CodeUnitId != packet.CodeUnitId ||
            !StringComparer.Ordinal.Equals(result.ProfiledRevision, packet.ProfiledRevision) ||
            !StringComparer.Ordinal.Equals(result.RepositoryRelativePath, job.RepositoryRelativePath) ||
            result.LineStart != job.LineStart ||
            result.LineEnd != job.LineEnd ||
            !StringComparer.Ordinal.Equals(result.SourceHash, job.SourceHash) ||
            !StringComparer.Ordinal.Equals(result.FileIdentityHash, job.FileIdentityHash))
        {
            return false;
        }

        if (!ValidatePass1Collections(job, result))
        {
            return false;
        }

        HashSet<Guid> anchorIds = result.SourceAnchors.Select(anchor => anchor.Id).ToHashSet();
        Dictionary<Guid, WorkerAgentCommunicationEvidence> approvedEvidence =
            job.ApprovedCommunicationEvidence.ToDictionary(evidence => evidence.EvidenceId);
        CanonicalSection[] evidenceSections = result.Content.Sections
            .Where(section => section.Kind == DocumentSectionKind.Evidence)
            .ToArray();
        if (evidenceSections.Length != result.CommunicationEvidenceIds.Count ||
            result.CommunicationEvidenceIds.Any(id => !approvedEvidence.ContainsKey(id)) ||
            evidenceSections.Select(section => section.StableSectionId).ToHashSet()
                .SetEquals(result.CommunicationEvidenceIds) is false)
        {
            return false;
        }

        foreach (CanonicalSection section in evidenceSections)
        {
            WorkerAgentCommunicationEvidence evidence = approvedEvidence[section.StableSectionId];
            if (!StringComparer.Ordinal.Equals(section.Heading, evidence.Kind) ||
                !StringComparer.Ordinal.Equals(section.Markdown, evidence.Description) ||
                !StringComparer.Ordinal.Equals(section.SearchText, evidence.Description) ||
                section.SourceAnchorIds.Any(id => !anchorIds.Contains(id)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidatePass1Collections(
        Pass1CodeLogicJobPayload job,
        Pass1CodeLogicDocumentResultPayload result)
    {
        if (!StringComparer.Ordinal.Equals(
                result.Content.SchemaIdentity,
                KnowledgeDocumentLimits.SchemaIdentity) ||
            result.Content.DocumentType != DocumentType.CodeLogic ||
            !StringComparer.Ordinal.Equals(
                result.Content.RepositoryRelativePath,
                job.RepositoryRelativePath) ||
            !IsStrictlyAscending(result.SourceAnchors.Select(anchor => anchor.Id)) ||
            result.SourceAnchors.Any(anchor =>
                !StringComparer.Ordinal.Equals(
                    anchor.RepositoryRelativePath,
                    job.RepositoryRelativePath) ||
                anchor.LineStart < job.LineStart ||
                anchor.LineEnd > job.LineEnd ||
                anchor.LineEnd < anchor.LineStart ||
                !StringComparer.Ordinal.Equals(anchor.SourceHash, job.SourceHash)) ||
            !IsStrictlyAscending(result.ReferenceDocumentVersionIds) ||
            result.ReferenceDocumentVersionIds.Any(
                id => !job.AllowedReferenceDocumentVersionIds.Contains(id)) ||
            !IsStrictlyAscending(result.CommunicationEvidenceIds))
        {
            return false;
        }

        HashSet<Guid> anchorIds = result.SourceAnchors.Select(anchor => anchor.Id).ToHashSet();
        HashSet<Guid> referenceIds = result.ReferenceDocumentVersionIds.ToHashSet();
        HashSet<Guid> sectionIds = [];
        for (int index = 0; index < result.Content.Sections.Count; index++)
        {
            CanonicalSection section = result.Content.Sections[index];
            if (!sectionIds.Add(section.StableSectionId) ||
                section.Order != index + 1 ||
                !IsStrictlyAscending(section.SourceAnchorIds) ||
                !IsStrictlyAscending(section.DocumentVersionIds) ||
                section.SourceAnchorIds.Any(id => !anchorIds.Contains(id)) ||
                section.DocumentVersionIds.Any(id => !referenceIds.Contains(id)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStrictlyAscending(IEnumerable<Guid> identifiers)
    {
        Guid? previous = null;
        foreach (Guid identifier in identifiers)
        {
            if (identifier == Guid.Empty ||
                previous.HasValue && previous.Value.CompareTo(identifier) >= 0)
            {
                return false;
            }

            previous = identifier;
        }

        return true;
    }

    private static bool CurrentMatchesReplay(
        CurrentKnowledge current,
        WorkerAgentJobPacket packet,
        Pass1CodeLogicDocumentResultPayload result)
    {
        if (current.RepositoryId != packet.RepositoryId ||
            current.DocumentType != result.Content.DocumentType ||
            !StringComparer.Ordinal.Equals(current.Title, result.Content.Title) ||
            !StringComparer.Ordinal.Equals(
                current.RepositoryRelativePath,
                result.Content.RepositoryRelativePath) ||
            !StringComparer.Ordinal.Equals(current.IndexEntry.Summary, result.Content.Summary) ||
            !current.IndexEntry.KeySymbols.SequenceEqual(result.Content.KeySymbols) ||
            !current.IndexEntry.BusinessTerms.SequenceEqual(result.Content.BusinessTerms) ||
            !current.IndexEntry.TechnicalTerms.SequenceEqual(result.Content.TechnicalTerms) ||
            current.SourceAnchors.Count != result.SourceAnchors.Count ||
            current.Relationships.Count != result.ReferenceDocumentVersionIds.Count ||
            current.Sections.Count != result.Content.Sections.Count)
        {
            return false;
        }

        Dictionary<Guid, CurrentSourceAnchor> currentAnchors =
            current.SourceAnchors.ToDictionary(anchor => anchor.Id);
        if (result.SourceAnchors.Any(anchor =>
                !currentAnchors.TryGetValue(anchor.Id, out CurrentSourceAnchor? durable) ||
                durable.CodeUnitId != packet.CodeUnitId ||
                !StringComparer.Ordinal.Equals(
                    durable.RepositoryRelativePath,
                    anchor.RepositoryRelativePath) ||
                !StringComparer.Ordinal.Equals(durable.EvidenceLocator, anchor.EvidenceLocator) ||
                durable.LineStart != anchor.LineStart ||
                durable.LineEnd != anchor.LineEnd ||
                !StringComparer.Ordinal.Equals(durable.SourceHash, anchor.SourceHash)))
        {
            return false;
        }

        Guid[] durableTargets = current.Relationships
            .OfType<AuthorizedRelationship>()
            .Where(relationship => relationship.EdgeType == DocumentEdgeType.References)
            .Select(relationship => relationship.TargetDocumentVersionId)
            .Order()
            .ToArray();
        if (!durableTargets.SequenceEqual(result.ReferenceDocumentVersionIds))
        {
            return false;
        }

        Dictionary<Guid, CurrentDocumentSection> currentSections =
            current.Sections.ToDictionary(section => section.StableSectionId);
        return result.Content.Sections.All(section =>
            currentSections.TryGetValue(section.StableSectionId, out CurrentDocumentSection? durable) &&
            durable.SectionOrder == section.Order &&
            durable.SectionKind == section.Kind &&
            StringComparer.Ordinal.Equals(durable.Heading, section.Heading) &&
            StringComparer.Ordinal.Equals(durable.ContentMarkdown, section.Markdown) &&
            StringComparer.Ordinal.Equals(durable.SearchText, section.SearchText) &&
            durable.SourceAnchorIds.SequenceEqual(section.SourceAnchorIds) &&
            durable.AuthorizedDocumentVersionIds.SequenceEqual(section.DocumentVersionIds) &&
            !durable.HasRestrictedDocumentReference);
    }

    private static Guid CreateStableId(params string[] parts)
    {
        byte[] value = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts)));
        return new Guid(value.AsSpan(0, 16));
    }
}
