using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class ChatEvidenceRetriever(
    VulgataDbContext context,
    IPreparedChatScopeResolver? preparedScopeResolver = null,
    ILogger<ChatEvidenceRetriever>? logger = null) : IChatEvidenceRetriever
{
    private readonly VulgataDbContext _context = context;
    private readonly IPreparedChatScopeResolver _preparedScopeResolver =
        preparedScopeResolver ?? new PreparedChatScopeResolver(
            context);
    private readonly ILogger<ChatEvidenceRetriever>? _logger = logger;

    public async Task<ChatEvidenceResult> RetrieveAsync(
        ChatRetrievalRequest request,
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        if (request is null || accessScope is null)
        {
            return ChatEvidenceResult.InvalidRequest();
        }

        Guid[] repositoryIds = request.RepositoryIds.Order().ToArray();
        if (repositoryIds.Length != 2 ||
            repositoryIds.Any(repositoryId => !accessScope.Contains(repositoryId)) ||
            !await accessScope.HasCurrentAccessAsync(repositoryIds, cancellationToken))
        {
            return ChatEvidenceResult.NotFoundOrUnauthorized();
        }

        IExecutionStrategy strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
            try
            {
                if (await _preparedScopeResolver.ResolveAsync(
                        request.SystemId,
                        repositoryIds,
                        accessScope,
                        cancellationToken) is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                    return ChatEvidenceResult.IncompleteOrStale();
                }

                ChatEvidenceResult result = await RetrieveInsideTransactionAsync(
                    request,
                    repositoryIds,
                    cancellationToken);
                if (result.Kind != ChatEvidenceResultKind.Found)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                    return result;
                }

                if (!await accessScope.HasCurrentAccessAsync(repositoryIds, cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                    return ChatEvidenceResult.NotFoundOrUnauthorized();
                }

                await transaction.CommitAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                return result;
            }
            catch (Exception exception) when (
                exception is ChatContractException or KnowledgeStoreContractException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                return ChatEvidenceResult.IncompleteOrStale();
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private async Task<ChatEvidenceResult> RetrieveInsideTransactionAsync(
        ChatRetrievalRequest request,
        Guid[] repositoryIds,
        CancellationToken cancellationToken)
    {
        Repository[] repositories = await _context.Repositories
            .AsNoTracking()
            .Where(repository => repositoryIds.Contains(repository.Id))
            .Take(3)
            .ToArrayAsync(cancellationToken);
        if (repositories.Length != 2 ||
            repositories.Any(repository => repository.SystemId != request.SystemId))
        {
            return ChatEvidenceResult.NotFoundOrUnauthorized();
        }

        ExactRelationshipRow[] exactRelationships = await (
            from decision in _context.CrossRepositoryResolutionDecisions.AsNoTracking()
            join edge in _context.DocumentEdges.AsNoTracking()
                on decision.DocumentEdgeId equals edge.Id
            join source in _context.LogicalDocuments.AsNoTracking()
                on edge.SourceDocumentVersionId equals source.CurrentVersionId
            join target in _context.LogicalDocuments.AsNoTracking()
                on edge.TargetDocumentVersionId equals target.CurrentVersionId
            where decision.ResolutionSchemaIdentity == "exact-cross-repository-resolution/2.0" &&
                decision.SystemId == request.SystemId &&
                decision.OutcomeKind == "ExactMatch" &&
                decision.ReasonCode == "unique_provider" &&
                decision.DocumentEdgeId.HasValue &&
                decision.ProviderDocumentVersionId.HasValue &&
                decision.ProviderRepositoryId.HasValue &&
                decision.ProviderScanRunId.HasValue &&
                edge.EdgeType == DocumentEdgeType.References &&
                repositoryIds.Contains(edge.SourceRepositoryId) &&
                repositoryIds.Contains(edge.TargetRepositoryId) &&
                edge.SourceRepositoryId != edge.TargetRepositoryId
            orderby edge.Id
            select new ExactRelationshipRow(
                decision.Id,
                edge.Id,
                edge.SourceDocumentVersionId,
                edge.SourceRepositoryId,
                edge.SourceScanRunId,
                edge.TargetDocumentVersionId,
                edge.TargetRepositoryId,
                edge.TargetScanRunId,
                decision.ConsumerDocumentVersionId,
                decision.ConsumerRepositoryId,
                decision.ConsumerScanRunId,
                decision.ConsumerProfiledRevision,
                decision.ProviderDocumentVersionId.Value,
                decision.ProviderRepositoryId.Value,
                decision.ProviderScanRunId.Value,
                decision.ProviderProfiledRevision!,
                decision.NormalizedEndpointIdentifier,
                decision.ProviderEvidenceSetFingerprint,
                decision.EvidenceFingerprint))
            .Take(2)
            .ToArrayAsync(cancellationToken);
        if (exactRelationships.Length == 0)
        {
            if (RequiresCrossRepositoryEvidence(request.Question))
            {
                return ChatEvidenceResult.IncompleteOrStale();
            }

            return await RetrieveProjectionOnlyAsync(
                request,
                repositoryIds,
                cancellationToken);
        }

        if (exactRelationships.Length != 1)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        ExactRelationshipRow exactRelationship = exactRelationships[0];
        bool sourceScanRunIsCurrent = await _context.ScanRuns.AsNoTracking().AnyAsync(
            scanRun =>
                scanRun.Id == exactRelationship.SourceScanRunId &&
                scanRun.RepositoryId == exactRelationship.SourceRepositoryId &&
                scanRun.Status == ScanRunStatus.Completed &&
                scanRun.ProfiledRevision == exactRelationship.ConsumerProfiledRevision,
            cancellationToken);
        bool targetScanRunIsCurrent = await _context.ScanRuns.AsNoTracking().AnyAsync(
            scanRun =>
                scanRun.Id == exactRelationship.TargetScanRunId &&
                scanRun.RepositoryId == exactRelationship.TargetRepositoryId &&
                scanRun.Status == ScanRunStatus.Completed &&
                scanRun.ProfiledRevision == exactRelationship.ProviderProfiledRevision,
            cancellationToken);
        if (!sourceScanRunIsCurrent || !targetScanRunIsCurrent)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        ExactResolutionProviderEvidenceContribution[] exactContributions = await _context
            .ExactResolutionProviderEvidenceContributions
            .AsNoTracking()
            .Where(contribution => contribution.ResolutionDecisionId == exactRelationship.DecisionId)
            .OrderBy(contribution => contribution.ProviderOrdinal)
            .ThenBy(contribution => contribution.EvidenceOrdinal)
            .Take(257)
            .ToArrayAsync(cancellationToken);
        CrossRepositoryResolutionDecision exactDecision = await _context
            .CrossRepositoryResolutionDecisions
            .AsNoTracking()
            .SingleAsync(
                decision => decision.Id == exactRelationship.DecisionId,
                cancellationToken);
        DocumentEdge exactEdge = await _context.DocumentEdges
            .AsNoTracking()
            .SingleAsync(edge => edge.Id == exactRelationship.EdgeId, cancellationToken);
        if (!exactRelationship.MatchesDirection() ||
            exactContributions.Length is < 1 or > 256 ||
            exactContributions.Any(contribution =>
                contribution.ProviderDocumentVersionId != exactRelationship.TargetDocumentVersionId ||
                contribution.RepositoryId != exactRelationship.TargetRepositoryId ||
                contribution.ScanRunId != exactRelationship.TargetScanRunId ||
                !StringComparer.Ordinal.Equals(
                    contribution.ProfiledRevision,
                    exactRelationship.ProviderProfiledRevision) ||
                contribution.SourceAnchorId == Guid.Empty ||
                !StringComparer.Ordinal.Equals(
                    contribution.NormalizedEndpointIdentifier,
                    exactRelationship.NormalizedEndpointIdentifier)) ||
            !ExactResolutionCanonicalAuthority.MatchesPersistedExactAuthority(
                exactDecision,
                exactEdge,
                exactContributions))
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        ProvenanceRow[] provenance = await (
            from edge in _context.DocumentEdges.AsNoTracking()
            join source in _context.LogicalDocuments.AsNoTracking()
                on edge.SourceDocumentVersionId equals source.CurrentVersionId
            join target in _context.LogicalDocuments.AsNoTracking()
                on edge.TargetDocumentVersionId equals target.CurrentVersionId
            where edge.EdgeType == DocumentEdgeType.GeneratedFrom &&
                source.DocumentType == DocumentType.BusinessLogic &&
                target.DocumentType == DocumentType.CodeLogic &&
                repositoryIds.Contains(edge.SourceRepositoryId) &&
                repositoryIds.Contains(edge.TargetRepositoryId) &&
                (edge.TargetDocumentVersionId == exactRelationship.SourceDocumentVersionId ||
                    edge.TargetDocumentVersionId == exactRelationship.TargetDocumentVersionId)
            orderby edge.SourceDocumentVersionId, edge.TargetDocumentVersionId, edge.Id
            select new ProvenanceRow(
                edge.Id,
                edge.SourceDocumentVersionId,
                edge.SourceRepositoryId,
                edge.TargetDocumentVersionId,
                edge.TargetRepositoryId))
            .Take(32)
            .ToArrayAsync(cancellationToken);
        if (provenance.Length is < 1 or > 31)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        Guid[] baseDocumentVersionIds = provenance
            .SelectMany(relationship => new[]
            {
                relationship.SourceDocumentVersionId,
                relationship.TargetDocumentVersionId,
            })
            .Append(exactRelationship.SourceDocumentVersionId)
            .Append(exactRelationship.TargetDocumentVersionId)
            .Distinct()
            .ToArray();
        if (baseDocumentVersionIds.Length is < 1 or > 5)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        DocumentSection[] baseSections = await _context.DocumentSections
            .AsNoTracking()
            .Where(section => baseDocumentVersionIds.Contains(section.DocumentVersionId))
            .OrderBy(section => section.DocumentVersionId)
            .ThenBy(section => section.SectionOrder)
            .ThenBy(section => section.StableSectionId)
            .Take(21)
            .ToArrayAsync(cancellationToken);
        if (baseSections.Length is < 1 or > 20 ||
            baseDocumentVersionIds.Any(documentVersionId =>
                baseSections.All(section => section.DocumentVersionId != documentVersionId)))
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        int supplementaryLimit = Math.Min(
            2,
            Math.Min(5 - baseDocumentVersionIds.Length, 20 - baseSections.Length));
        SupplementaryChatSectionCandidate[] supplementaryCandidates = supplementaryLimit == 0
            ? []
            : await (
                from logical in _context.LogicalDocuments.AsNoTracking()
                join version in _context.DocumentVersions.AsNoTracking()
                    on logical.CurrentVersionId equals version.Id
                join section in _context.DocumentSections.AsNoTracking()
                    on version.Id equals section.DocumentVersionId
                join indexEntry in _context.IndexEntries.AsNoTracking()
                    on version.Id equals indexEntry.DocumentVersionId into indexEntries
                from indexEntry in indexEntries.DefaultIfEmpty()
                where logical.DocumentType == DocumentType.CodeLogic &&
                    !baseDocumentVersionIds.Contains(version.Id) &&
                    repositoryIds.Contains(version.RepositoryId) &&
                    version.SystemId == request.SystemId &&
                    version.SupersededById == null &&
                    (section.SectionKind == DocumentSectionKind.Overview ||
                        section.SectionKind == DocumentSectionKind.BusinessRule ||
                        section.SectionKind == DocumentSectionKind.ProcessStep)
                orderby version.Id, section.SectionOrder, section.StableSectionId
                select new SupplementaryChatSectionCandidate(
                    version.Id,
                    section.StableSectionId,
                    version.Title,
                    section.Heading,
                    section.ContentMarkdown,
                    logical.Id,
                    indexEntry == null ? string.Empty : indexEntry.RepositoryRelativePath,
                    indexEntry == null ? string.Empty : indexEntry.KeySymbolsJson,
                    indexEntry == null ? string.Empty : indexEntry.BusinessTermsJson,
                    indexEntry == null ? string.Empty : indexEntry.TechnicalTermsJson,
                    indexEntry == null ? string.Empty : indexEntry.SearchText))
                .Take(256)
                .ToArrayAsync(cancellationToken);
        Guid[] currentLogicalDocumentIds = await (
            from logical in _context.LogicalDocuments.AsNoTracking()
            join version in _context.DocumentVersions.AsNoTracking()
                on logical.CurrentVersionId equals version.Id
            where repositoryIds.Contains(version.RepositoryId) &&
                version.SystemId == request.SystemId &&
                version.SupersededById == null
            select logical.Id)
            .Take(257)
            .ToArrayAsync(cancellationToken);
        if (currentLogicalDocumentIds.Length > 256)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        ChatConversationRetrievalContext conversationContext =
            request.Conversation.BuildRetrievalContext(
                currentLogicalDocumentIds,
                currentLogicalDocumentIds);
        string retrievalQuestion = conversationContext.IsMissingEquivalent
            ? request.Question
            : string.Join(
                ' ',
                request.Conversation.Turns
                    .Where(turn => turn.LogicalDocumentIds.Any(
                        conversationContext.CurrentDocumentHints.Contains))
                    .Select(turn => turn.Question)
                    .Append(request.Question));
        IReadOnlyList<SupplementaryChatSectionCandidate> selectedSupplementary =
            ChatSupplementaryEvidenceSelector.Select(
                retrievalQuestion,
                supplementaryCandidates,
                supplementaryLimit,
                conversationContext.CurrentDocumentHints);
        Guid[] supplementarySectionIds = selectedSupplementary
            .Select(item => item.StableSectionId)
            .ToArray();
        DocumentSection[] supplementarySections = supplementarySectionIds.Length == 0
            ? []
            : await _context.DocumentSections
                .AsNoTracking()
                .Where(section => supplementarySectionIds.Contains(section.StableSectionId))
                .ToArrayAsync(cancellationToken);
        if (supplementarySections.Length != supplementarySectionIds.Length)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        Guid[] selectedDocumentVersionIds = baseDocumentVersionIds
            .Concat(selectedSupplementary.Select(item => item.DocumentVersionId))
            .Distinct()
            .ToArray();
        Guid[] existingRelationshipEdgeIds = provenance
            .Select(item => item.EdgeId)
            .Append(exactRelationship.EdgeId)
            .ToArray();
        int relatedDocumentLimit = Math.Min(
            5 - selectedDocumentVersionIds.Length,
            20 - baseSections.Length - supplementarySections.Length);
        ChatRelationshipProjection relationshipProjection =
            await new ChatRelationshipEvidenceProjector(_context).ProjectAsync(
                request.SystemId,
                repositoryIds,
                baseDocumentVersionIds,
                selectedDocumentVersionIds,
                existingRelationshipEdgeIds,
                relatedDocumentLimit,
                32 - existingRelationshipEdgeIds.Length,
                cancellationToken);

        DocumentSection[] sections =
            [.. baseSections, .. supplementarySections, .. relationshipProjection.RelatedSections];
        Guid[] documentVersionIds = selectedDocumentVersionIds
            .Concat(relationshipProjection.Relationships.Select(
                item => item.TargetDocumentVersionId))
            .Distinct()
            .ToArray();

        CurrentDocumentRow[] documents = await (
            from logical in _context.LogicalDocuments.AsNoTracking()
            join version in _context.DocumentVersions.AsNoTracking()
                on logical.CurrentVersionId equals version.Id
            where documentVersionIds.Contains(version.Id) &&
                repositoryIds.Contains(version.RepositoryId) &&
                version.SystemId == request.SystemId &&
                version.SupersededById == null
            select new CurrentDocumentRow(
                logical.Id,
                version.Id,
                version.RepositoryId,
                version.ScanRunId,
                logical.DocumentType,
                version.Title,
                version.ProfiledRevision))
            .Take(6)
            .ToArrayAsync(cancellationToken);
        if (documents.Length != documentVersionIds.Length ||
            documents.Select(document => document.DocumentVersionId).Distinct().Count() !=
                documentVersionIds.Length)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        CurrentDocumentRow[] canonicalDocuments = documents
            .OrderByDescending(document => document.DocumentType)
            .ThenBy(document => document.RepositoryId)
            .ThenBy(document => document.LogicalDocumentId)
            .ToArray();
        Dictionary<Guid, string> documentHandles = canonicalDocuments
            .Select((document, index) => (document.DocumentVersionId, Handle: $"d{index + 1}"))
            .ToDictionary(item => item.DocumentVersionId, item => item.Handle);

        ParsedSection[] parsedSections = sections.Select(ParseSection).ToArray();
        if (parsedSections.Any(section =>
                section.SupportedDocumentVersionIds.Any(documentVersionId =>
                    !documentHandles.ContainsKey(documentVersionId))))
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        Guid[] anchorIds = parsedSections
            .SelectMany(section => section.SourceAnchorIds)
            .Distinct()
            .ToArray();
        if (anchorIds.Length is < 1 or > 32)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        SourceAnchor[] anchors = await _context.SourceAnchors
            .AsNoTracking()
            .Where(anchor => anchorIds.Contains(anchor.Id))
            .Take(33)
            .ToArrayAsync(cancellationToken);
        bool hasInvalidAnchor = anchors.Any(anchor =>
                !documentHandles.ContainsKey(anchor.DocumentVersionId) ||
                !repositoryIds.Contains(anchor.RepositoryId) ||
                !IsSafeRepositoryRelativePath(anchor.RepositoryRelativePath) ||
                anchor.LineStart < 1 ||
                anchor.LineEnd < anchor.LineStart ||
                !IsLowerHexSha256(anchor.SourceHash));
        bool isMissingConsumerAnchor = parsedSections
            .Where(section => section.Section.DocumentVersionId == exactRelationship.SourceDocumentVersionId)
            .SelectMany(section => section.SourceAnchorIds)
            .All(anchorId => !anchorIds.Contains(anchorId));
        bool isMissingProviderAnchor = exactContributions
            .Any(contribution => !anchorIds.Contains(contribution.SourceAnchorId));
        if (anchors.Length != anchorIds.Length ||
            hasInvalidAnchor ||
            isMissingConsumerAnchor ||
            isMissingProviderAnchor)
        {
            _logger?.LogWarning(
                "Prepared Chat source-anchor validation failed. Expected {ExpectedAnchorCount}, loaded {LoadedAnchorCount}, invalid {HasInvalidAnchor}, missing consumer {MissingConsumerAnchor}, missing provider {MissingProviderAnchor}.",
                anchorIds.Length,
                anchors.Length,
                hasInvalidAnchor,
                isMissingConsumerAnchor,
                isMissingProviderAnchor);
            return ChatEvidenceResult.IncompleteOrStale();
        }

        SourceAnchor[] canonicalAnchors = anchors
            .OrderBy(anchor => documentHandles[anchor.DocumentVersionId], StringComparer.Ordinal)
            .ThenBy(anchor => anchor.RepositoryRelativePath, StringComparer.Ordinal)
            .ThenBy(anchor => anchor.LineStart)
            .ThenBy(anchor => anchor.Id)
            .ToArray();
        Dictionary<Guid, string> anchorHandles = canonicalAnchors
            .Select((anchor, index) => (anchor.Id, Handle: $"a{index + 1}"))
            .ToDictionary(item => item.Id, item => item.Handle);

        List<(Guid EdgeId, EvidenceRelationship Relationship)> relationships =
        [
            (
                exactRelationship.EdgeId,
                new EvidenceRelationship(
                    "r1",
                    exactRelationship.EvidenceFingerprint,
                    ChatRelationshipType.References,
                    documentHandles[exactRelationship.SourceDocumentVersionId],
                    documentHandles[exactRelationship.TargetDocumentVersionId],
                    exactRelationship.ProfileFingerprint)),
        ];
        relationships.AddRange(provenance.Select((relationship, index) =>
            (
                relationship.EdgeId,
                new EvidenceRelationship(
                    $"r{index + 2}",
                    StableIdentity(relationship.EdgeId),
                    ChatRelationshipType.GeneratedFrom,
                    documentHandles[relationship.SourceDocumentVersionId],
                    documentHandles[relationship.TargetDocumentVersionId],
                    null))));
        relationships.AddRange(relationshipProjection.Relationships.Select((relationship, index) =>
            (
                relationship.EdgeId,
                new EvidenceRelationship(
                    $"r{provenance.Length + index + 2}",
                    relationship.ExactEvidenceFingerprint ?? StableIdentity(relationship.EdgeId),
                    relationship.EdgeType == DocumentEdgeType.References
                        ? ChatRelationshipType.References
                        : ChatRelationshipType.GeneratedFrom,
                    documentHandles[relationship.SourceDocumentVersionId],
                    documentHandles[relationship.TargetDocumentVersionId],
                    relationship.ExactProviderEvidenceSetFingerprint))));
        Dictionary<Guid, string> relationshipHandles = relationships.ToDictionary(
            item => item.EdgeId,
            item => item.Relationship.RelationshipHandle);

        FlowRow[] flowRows = await (
            from flowGroup in _context.FlowGroups.AsNoTracking()
            join trace in _context.FlowTraces.AsNoTracking()
                on flowGroup.Id equals trace.FlowGroupId
            join step in _context.FlowTraceSteps.AsNoTracking()
                on trace.Id equals step.FlowTraceId
            where flowGroup.State == "Published" &&
                flowGroup.SchemaIdentity == "endpoint-flow-group/1.0" &&
                flowGroup.ExactResolutionDecisionId == exactRelationship.DecisionId &&
                flowGroup.ExactResolutionEdgeId == exactRelationship.EdgeId &&
                flowGroup.BusinessLogicDocumentVersionId.HasValue &&
                flowGroup.MembershipFingerprint != null &&
                documentVersionIds.Contains(flowGroup.BusinessLogicDocumentVersionId.Value)
            orderby flowGroup.GroupOrder, trace.TraceOrder, step.StepOrder, step.Id
            select new FlowRow(
                flowGroup.Id,
                flowGroup.SchemaIdentity!,
                flowGroup.State,
                flowGroup.BusinessLogicDocumentVersionId.Value,
                flowGroup.ExactResolutionDecisionId.Value,
                flowGroup.ExactResolutionEdgeId.Value,
                flowGroup.PlanFingerprint,
                flowGroup.MembershipFingerprint!,
                trace.Id,
                trace.TraceOrder,
                step.Id,
                step.StepOrder,
                step.StepKind,
                step.DocumentVersionId,
                step.DocumentRepositoryId,
                step.DocumentScanRunId,
                step.EdgeId,
                step.ExactResolutionDecisionId,
                step.Description))
            .Take(13)
            .ToArrayAsync(cancellationToken);
        EvidenceFlowGroupAuthority[] flowGroups = flowRows
            .GroupBy(row => new
            {
                row.FlowGroupId,
                row.SchemaIdentity,
                row.State,
                row.BusinessLogicDocumentVersionId,
                row.ExactResolutionDecisionId,
                row.ExactResolutionEdgeId,
                row.PlanFingerprint,
                row.MembershipFingerprint,
                row.FlowTraceId,
                row.FlowTraceOrder,
            })
            .OrderBy(group => group.Key.FlowGroupId)
            .Select(group => new EvidenceFlowGroupAuthority(
                group.Key.FlowGroupId,
                group.Key.SchemaIdentity,
                group.Key.State,
                group.Key.BusinessLogicDocumentVersionId,
                group.Key.ExactResolutionDecisionId,
                group.Key.ExactResolutionEdgeId,
                group.Key.PlanFingerprint,
                group.Key.MembershipFingerprint,
                group.Key.FlowTraceId,
                group.Key.FlowTraceOrder,
                group.OrderBy(row => row.StepOrder)
                    .ThenBy(row => row.FlowTraceStepId)
                    .Select(row => new EvidenceFlowStepAuthority(
                        row.FlowTraceStepId,
                        row.StepOrder,
                        row.StepKind,
                        row.Description,
                        row.DocumentVersionId,
                        row.DocumentRepositoryId,
                        row.DocumentScanRunId,
                        row.EdgeId,
                        row.StepExactResolutionDecisionId))
                    .ToArray()))
            .ToArray();
        if (flowRows.Length is < 1 or > 12 ||
            flowGroups.Length is < 1 or > 10 ||
            flowRows.Any(row =>
                !documentHandles.ContainsKey(row.DocumentVersionId) ||
                canonicalDocuments.All(document =>
                    document.DocumentVersionId != row.DocumentVersionId ||
                    document.RepositoryId != row.DocumentRepositoryId ||
                    document.ScanRunId != row.DocumentScanRunId) ||
                row.EdgeId is Guid edgeId && !relationshipHandles.ContainsKey(edgeId)) ||
            flowGroups.Any(group =>
                group.FlowTraceOrder != 1 ||
                !IsLowerHexSha256(group.PlanFingerprint) ||
                !IsLowerHexSha256(group.MembershipFingerprint) ||
                !group.Steps.Select(step => step.StepOrder).SequenceEqual(
                    Enumerable.Range(1, group.Steps.Count))))
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        foreach (CurrentDocumentRow document in canonicalDocuments)
        {
            KnowledgeTextSafety.EnsureSafe(document.Title);
        }

        foreach (DocumentSection section in sections)
        {
            KnowledgeTextSafety.EnsureSafe(section.Heading);
            KnowledgeTextSafety.EnsureSafe(section.ContentMarkdown);
        }

        foreach (FlowRow flowRow in flowRows)
        {
            KnowledgeTextSafety.EnsureSafe(flowRow.Description);
        }

        EvidenceDocument[] evidenceDocuments = canonicalDocuments
            .Select(document => new EvidenceDocument(
                documentHandles[document.DocumentVersionId],
                document.LogicalDocumentId,
                document.DocumentVersionId,
                document.RepositoryId,
                document.DocumentType,
                document.Title,
                document.ProfiledRevision))
            .ToArray();
        ParsedSection[] canonicalSections = parsedSections
            .OrderBy(section => documentHandles[section.Section.DocumentVersionId], StringComparer.Ordinal)
            .ThenBy(section => section.Section.SectionOrder)
            .ThenBy(section => section.Section.StableSectionId)
            .ToArray();
        EvidenceSection[] evidenceSections = canonicalSections
            .Select((section, index) => new EvidenceSection(
                $"s{index + 1}",
                documentHandles[section.Section.DocumentVersionId],
                section.Section.StableSectionId,
                section.Section.SectionOrder,
                section.Section.SectionKind,
                section.Section.Heading,
                section.Section.ContentMarkdown,
                section.SourceAnchorIds.Select(anchorId => anchorHandles[anchorId]).ToArray(),
                section.SupportedDocumentVersionIds
                    .Select(documentVersionId => documentHandles[documentVersionId])
                    .ToArray()))
            .ToArray();
        EvidenceAnchor[] evidenceAnchors = canonicalAnchors
            .Select(anchor => new EvidenceAnchor(
                anchorHandles[anchor.Id],
                anchor.Id,
                documentHandles[anchor.DocumentVersionId],
                anchor.DocumentVersionId,
                anchor.RepositoryId,
                anchor.RepositoryRelativePath.Replace('\\', '/'),
                anchor.LineStart,
                anchor.LineEnd,
                anchor.SourceHash))
            .ToArray();
        EvidenceFlowStep[] flowEvidence = flowRows
            .Select((row, index) => new EvidenceFlowStep(
                index + 1,
                documentHandles[row.DocumentVersionId],
                row.EdgeId is Guid edgeId ? relationshipHandles[edgeId] : null,
                row.Description))
            .ToArray();
        HashSet<Guid> optionalSupplementarySectionIds = supplementarySectionIds.ToHashSet();
        string[] optionalSectionHandles = evidenceSections
            .Where(section => optionalSupplementarySectionIds.Contains(section.StableSectionId))
            .Select(section => section.SectionHandle)
            .ToArray();

        EvidencePacket packet = new ChatEvidencePacketBuilder().Build(
            ChatEvidencePacketBuildRequest.Create(
                request.RequestId,
                request.Mode,
                request.SystemId,
                repositoryIds,
                evidenceDocuments,
                evidenceSections,
                evidenceAnchors,
                relationships.Select(item => item.Relationship).ToArray(),
                flowEvidence,
                flowGroups,
                string.Empty,
                request.Conversation,
                optionalSectionHandles));
        return ChatEvidenceResult.Found(packet);
    }

    private async Task<ChatEvidenceResult> RetrieveProjectionOnlyAsync(
        ChatRetrievalRequest request,
        Guid[] repositoryIds,
        CancellationToken cancellationToken)
    {
        ProjectionSectionRow[] candidates = await (
            from logical in _context.LogicalDocuments.AsNoTracking()
            join version in _context.DocumentVersions.AsNoTracking()
                on logical.CurrentVersionId equals version.Id
            join section in _context.DocumentSections.AsNoTracking()
                on version.Id equals section.DocumentVersionId
            join indexEntry in _context.IndexEntries.AsNoTracking()
                on version.Id equals indexEntry.DocumentVersionId into indexEntries
            from indexEntry in indexEntries.DefaultIfEmpty()
            where repositoryIds.Contains(version.RepositoryId) &&
                version.SystemId == request.SystemId &&
                version.SupersededById == null &&
                (section.SectionKind == DocumentSectionKind.Overview ||
                    section.SectionKind == DocumentSectionKind.BusinessRule ||
                    section.SectionKind == DocumentSectionKind.ProcessStep)
            orderby version.RepositoryId, logical.Id, section.SectionOrder, section.StableSectionId
            select new ProjectionSectionRow(
                logical.Id,
                version.Id,
                version.RepositoryId,
                version.ScanRunId,
                logical.DocumentType,
                version.Title,
                version.ProfiledRevision,
                section,
                indexEntry == null ? string.Empty : indexEntry.RepositoryRelativePath,
                indexEntry == null ? string.Empty : indexEntry.KeySymbolsJson,
                indexEntry == null ? string.Empty : indexEntry.BusinessTermsJson,
                indexEntry == null ? string.Empty : indexEntry.TechnicalTermsJson,
                indexEntry == null ? string.Empty : indexEntry.SearchText))
            .Take(257)
            .ToArrayAsync(cancellationToken);
        if (candidates.Length is < 1 or > 256)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        Guid[] currentLogicalDocumentIds = candidates
            .Select(candidate => candidate.LogicalDocumentId)
            .Distinct()
            .ToArray();
        ChatConversationRetrievalContext conversationContext =
            request.Conversation.BuildRetrievalContext(
                currentLogicalDocumentIds,
                currentLogicalDocumentIds);
        string retrievalQuestion = conversationContext.IsMissingEquivalent
            ? request.Question
            : string.Join(
                ' ',
                request.Conversation.Turns
                    .Where(turn => turn.LogicalDocumentIds.Any(
                        conversationContext.CurrentDocumentHints.Contains))
                    .Select(turn => turn.Question)
                    .Append(request.Question));
        IReadOnlyList<SupplementaryChatSectionCandidate> selected =
            ChatSupplementaryEvidenceSelector.Select(
                retrievalQuestion,
                candidates.Select(candidate => new SupplementaryChatSectionCandidate(
                    candidate.DocumentVersionId,
                    candidate.Section.StableSectionId,
                    candidate.Title,
                    candidate.Section.Heading,
                    candidate.Section.ContentMarkdown,
                    candidate.LogicalDocumentId,
                    candidate.RepositoryRelativePath,
                    candidate.KeySymbolsJson,
                    candidate.BusinessTermsJson,
                    candidate.TechnicalTermsJson,
                    candidate.SearchText)).ToArray(),
                5,
                conversationContext.CurrentDocumentHints);
        if (selected.Count == 0)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        HashSet<Guid> selectedSectionIds = selected
            .Select(candidate => candidate.StableSectionId)
            .ToHashSet();
        ProjectionSectionRow[] selectedRows = candidates
            .Where(candidate => selectedSectionIds.Contains(candidate.Section.StableSectionId))
            .ToArray();
        ParsedSection[] parsedSections = selectedRows
            .Select(candidate => ParseSection(candidate.Section))
            .ToArray();
        Guid[] documentVersionIds = selectedRows
            .Select(candidate => candidate.DocumentVersionId)
            .Concat(parsedSections.SelectMany(section => section.SupportedDocumentVersionIds))
            .Distinct()
            .ToArray();
        if (documentVersionIds.Length is < 1 or > 5)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        CurrentDocumentRow[] documents = await (
            from logical in _context.LogicalDocuments.AsNoTracking()
            join version in _context.DocumentVersions.AsNoTracking()
                on logical.CurrentVersionId equals version.Id
            where documentVersionIds.Contains(version.Id) &&
                repositoryIds.Contains(version.RepositoryId) &&
                version.SystemId == request.SystemId &&
                version.SupersededById == null
            select new CurrentDocumentRow(
                logical.Id,
                version.Id,
                version.RepositoryId,
                version.ScanRunId,
                logical.DocumentType,
                version.Title,
                version.ProfiledRevision))
            .Take(6)
            .ToArrayAsync(cancellationToken);
        if (documents.Length != documentVersionIds.Length)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        CurrentDocumentRow[] canonicalDocuments = documents
            .OrderByDescending(document => document.DocumentType)
            .ThenBy(document => document.RepositoryId)
            .ThenBy(document => document.LogicalDocumentId)
            .ToArray();
        Dictionary<Guid, string> documentHandles = canonicalDocuments
            .Select((document, index) => (document.DocumentVersionId, Handle: $"d{index + 1}"))
            .ToDictionary(item => item.DocumentVersionId, item => item.Handle);
        Guid[] anchorIds = parsedSections
            .SelectMany(section => section.SourceAnchorIds)
            .Distinct()
            .ToArray();
        if (anchorIds.Length is < 1 or > 32)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        SourceAnchor[] anchors = await _context.SourceAnchors
            .AsNoTracking()
            .Where(anchor => anchorIds.Contains(anchor.Id))
            .Take(33)
            .ToArrayAsync(cancellationToken);
        if (anchors.Length != anchorIds.Length || anchors.Any(anchor =>
                !documentHandles.ContainsKey(anchor.DocumentVersionId) ||
                !repositoryIds.Contains(anchor.RepositoryId) ||
                !IsSafeRepositoryRelativePath(anchor.RepositoryRelativePath) ||
                anchor.LineStart < 1 ||
                anchor.LineEnd < anchor.LineStart ||
                !IsLowerHexSha256(anchor.SourceHash)))
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        SourceAnchor[] canonicalAnchors = anchors
            .OrderBy(anchor => documentHandles[anchor.DocumentVersionId], StringComparer.Ordinal)
            .ThenBy(anchor => anchor.RepositoryRelativePath, StringComparer.Ordinal)
            .ThenBy(anchor => anchor.LineStart)
            .ThenBy(anchor => anchor.Id)
            .ToArray();
        Dictionary<Guid, string> anchorHandles = canonicalAnchors
            .Select((anchor, index) => (anchor.Id, Handle: $"a{index + 1}"))
            .ToDictionary(item => item.Id, item => item.Handle);
        EvidenceDocument[] evidenceDocuments = canonicalDocuments
            .Select(document => new EvidenceDocument(
                documentHandles[document.DocumentVersionId],
                document.LogicalDocumentId,
                document.DocumentVersionId,
                document.RepositoryId,
                document.DocumentType,
                document.Title,
                document.ProfiledRevision))
            .ToArray();
        EvidenceSection[] evidenceSections = parsedSections
            .OrderBy(section => documentHandles[section.Section.DocumentVersionId], StringComparer.Ordinal)
            .ThenBy(section => section.Section.SectionOrder)
            .ThenBy(section => section.Section.StableSectionId)
            .Select((section, index) => new EvidenceSection(
                $"s{index + 1}",
                documentHandles[section.Section.DocumentVersionId],
                section.Section.StableSectionId,
                section.Section.SectionOrder,
                section.Section.SectionKind,
                section.Section.Heading,
                section.Section.ContentMarkdown,
                section.SourceAnchorIds.Select(anchorId => anchorHandles[anchorId]).ToArray(),
                section.SupportedDocumentVersionIds
                    .Select(documentVersionId => documentHandles[documentVersionId])
                    .ToArray()))
            .ToArray();
        EvidenceAnchor[] evidenceAnchors = canonicalAnchors
            .Select(anchor => new EvidenceAnchor(
                anchorHandles[anchor.Id],
                anchor.Id,
                documentHandles[anchor.DocumentVersionId],
                anchor.DocumentVersionId,
                anchor.RepositoryId,
                anchor.RepositoryRelativePath.Replace('\\', '/'),
                anchor.LineStart,
                anchor.LineEnd,
                anchor.SourceHash))
            .ToArray();

        EvidencePacket packet = new ChatEvidencePacketBuilder().Build(
            ChatEvidencePacketBuildRequest.Create(
                request.RequestId,
                request.Mode,
                request.SystemId,
                repositoryIds,
                evidenceDocuments,
                evidenceSections,
                evidenceAnchors,
                [],
                [],
                [],
                string.Empty,
                request.Conversation,
                []));
        return ChatEvidenceResult.Found(packet);
    }

    private static ParsedSection ParseSection(DocumentSection section)
    {
        Guid[] anchorIds = ParseGuidArray(section.SourceAnchorIdsJson);
        Guid[] supportedDocumentVersionIds = ParseGuidArray(section.DocumentVersionIdsJson);
        if (anchorIds.Length < 1 ||
            anchorIds.Distinct().Count() != anchorIds.Length ||
            supportedDocumentVersionIds.Distinct().Count() != supportedDocumentVersionIds.Length)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        return new ParsedSection(section, anchorIds, supportedDocumentVersionIds);
    }

    private static Guid[] ParseGuidArray(string json)
    {
        Guid[]? values;
        try
        {
            values = JsonSerializer.Deserialize<Guid[]>(json);
        }
        catch (JsonException)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        if (values is null || values.Any(value => value == Guid.Empty))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        return values;
    }

    private static bool IsLowerHexSha256(string value) =>
        value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool RequiresCrossRepositoryEvidence(string question) =>
        question.Contains("跨仓库", StringComparison.OrdinalIgnoreCase) ||
        question.Contains("cross-repository", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeRepositoryRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        return !normalized.StartsWith('/') &&
            !(normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':') &&
            !Uri.TryCreate(normalized, UriKind.Absolute, out _) &&
            segments.All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    private static string StableIdentity(Guid edgeId) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(edgeId.ToString("D"))));

    private sealed record ExactRelationshipRow(
        Guid DecisionId,
        Guid EdgeId,
        Guid SourceDocumentVersionId,
        Guid SourceRepositoryId,
        Guid SourceScanRunId,
        Guid TargetDocumentVersionId,
        Guid TargetRepositoryId,
        Guid TargetScanRunId,
        Guid ConsumerDocumentVersionId,
        Guid ConsumerRepositoryId,
        Guid ConsumerScanRunId,
        string ConsumerProfiledRevision,
        Guid ProviderDocumentVersionId,
        Guid ProviderRepositoryId,
        Guid ProviderScanRunId,
        string ProviderProfiledRevision,
        string NormalizedEndpointIdentifier,
        string ProfileFingerprint,
        string EvidenceFingerprint)
    {
        internal bool MatchesDirection() =>
            SourceDocumentVersionId == ConsumerDocumentVersionId &&
            SourceRepositoryId == ConsumerRepositoryId &&
            SourceScanRunId == ConsumerScanRunId &&
            TargetDocumentVersionId == ProviderDocumentVersionId &&
            TargetRepositoryId == ProviderRepositoryId &&
            TargetScanRunId == ProviderScanRunId &&
            !string.IsNullOrWhiteSpace(ConsumerProfiledRevision) &&
            !string.IsNullOrWhiteSpace(ProviderProfiledRevision) &&
            IsLowerHexSha256(ProfileFingerprint) &&
            IsLowerHexSha256(EvidenceFingerprint);
    }

    private sealed record ProvenanceRow(
        Guid EdgeId,
        Guid SourceDocumentVersionId,
        Guid SourceRepositoryId,
        Guid TargetDocumentVersionId,
        Guid TargetRepositoryId);

    private sealed record CurrentDocumentRow(
        Guid LogicalDocumentId,
        Guid DocumentVersionId,
        Guid RepositoryId,
        Guid ScanRunId,
        DocumentType DocumentType,
        string Title,
        string ProfiledRevision);

    private sealed record ParsedSection(
        DocumentSection Section,
        IReadOnlyList<Guid> SourceAnchorIds,
        IReadOnlyList<Guid> SupportedDocumentVersionIds);

    private sealed record ProjectionSectionRow(
        Guid LogicalDocumentId,
        Guid DocumentVersionId,
        Guid RepositoryId,
        Guid ScanRunId,
        DocumentType DocumentType,
        string Title,
        string ProfiledRevision,
        DocumentSection Section,
        string RepositoryRelativePath,
        string KeySymbolsJson,
        string BusinessTermsJson,
        string TechnicalTermsJson,
        string SearchText);

    private sealed record FlowRow(
        Guid FlowGroupId,
        string SchemaIdentity,
        string State,
        Guid BusinessLogicDocumentVersionId,
        Guid ExactResolutionDecisionId,
        Guid ExactResolutionEdgeId,
        string PlanFingerprint,
        string MembershipFingerprint,
        Guid FlowTraceId,
        int FlowTraceOrder,
        Guid FlowTraceStepId,
        int StepOrder,
        FlowTraceStepKind StepKind,
        Guid DocumentVersionId,
        Guid DocumentRepositoryId,
        Guid DocumentScanRunId,
        Guid? EdgeId,
        Guid? StepExactResolutionDecisionId,
        string Description);
}
