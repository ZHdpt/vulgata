using System.Data;
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

        ChatEvidenceResult projectionResult = await RetrieveProjectionOnlyAsync(
            request,
            repositoryIds,
            cancellationToken);
        if (projectionResult.Kind != ChatEvidenceResultKind.Found ||
            !RequiresCrossRepositoryEvidence(request.Question))
        {
            return projectionResult;
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
            return ChatEvidenceResult.IncompleteOrStale();
        }

        EvidencePacket projectionPacket = projectionResult.Packet!;
        HashSet<Guid> projectionDocumentVersionIds = projectionPacket.Documents
            .Select(document => document.DocumentVersionId)
            .ToHashSet();
        ExactRelationshipRow exactRelationship = exactRelationships.FirstOrDefault(relationship =>
                projectionDocumentVersionIds.Contains(relationship.SourceDocumentVersionId) &&
                projectionDocumentVersionIds.Contains(relationship.TargetDocumentVersionId)) ??
            exactRelationships[0];
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
        if (provenance.Length == 0)
        {
            bool hasFlowExpansion = await _context.FlowGroups.AsNoTracking().AnyAsync(
                flowGroup =>
                    flowGroup.ExactResolutionDecisionId == exactRelationship.DecisionId &&
                    flowGroup.ExactResolutionEdgeId == exactRelationship.EdgeId,
                cancellationToken);
            return hasFlowExpansion
                ? ChatEvidenceResult.IncompleteOrStale()
                : projectionResult;
        }

        if (provenance.Length > 31)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        Guid[] expansionAuthorityDocumentVersionIds = provenance
            .SelectMany(relationship => new[]
            {
                relationship.SourceDocumentVersionId,
                relationship.TargetDocumentVersionId,
            })
            .Append(exactRelationship.SourceDocumentVersionId)
            .Append(exactRelationship.TargetDocumentVersionId)
            .Distinct()
            .ToArray();
        if (expansionAuthorityDocumentVersionIds.Length > 5)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        int expansionAuthoritySectionCount = await _context.DocumentSections.AsNoTracking()
            .Where(section =>
                expansionAuthorityDocumentVersionIds.Contains(section.DocumentVersionId))
            .Take(21)
            .CountAsync(cancellationToken);
        if (expansionAuthoritySectionCount > 20)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        Guid[] baseDocumentVersionIds = projectionPacket.Documents
            .Select(document => document.DocumentVersionId)
            .Distinct()
            .ToArray();
        if (baseDocumentVersionIds.Length is < 1 or > 5)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        Guid[] primarySectionIds = projectionPacket.Sections
            .Select(section => section.StableSectionId)
            .ToArray();
        DocumentSection[] baseSections = await _context.DocumentSections
            .AsNoTracking()
            .Where(section => primarySectionIds.Contains(section.StableSectionId))
            .OrderBy(section => section.DocumentVersionId)
            .ThenBy(section => section.SectionOrder)
            .ThenBy(section => section.StableSectionId)
            .Take(21)
            .ToArrayAsync(cancellationToken);
        if (baseSections.Length != primarySectionIds.Length || baseSections.Length > 20)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        Guid[] selectedDocumentVersionIds = baseDocumentVersionIds;
        Guid[] supplementarySectionIds = [];
        DocumentSection[] supplementarySections = [];
        Guid[] existingRelationshipEdgeIds = provenance
            .Select(item => item.EdgeId)
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
        if (relationshipProjection.IsInconsistent)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        Guid[] documentVersionIds = selectedDocumentVersionIds
            .Concat(relationshipProjection.Relationships.Select(
                item => item.TargetDocumentVersionId))
            .Distinct()
            .ToArray();
        HashSet<Guid> sectionDocumentIds = baseSections
            .Concat(relationshipProjection.RelatedSections)
            .Select(section => section.DocumentVersionId)
            .ToHashSet();
        Guid[] missingExactEndpointSectionDocumentIds = new[]
            {
                exactRelationship.SourceDocumentVersionId,
                exactRelationship.TargetDocumentVersionId,
            }
            .Where(documentVersionId =>
                documentVersionIds.Contains(documentVersionId) &&
                !sectionDocumentIds.Contains(documentVersionId))
            .ToArray();
        DocumentSection[] exactEndpointSections = [];
        foreach (Guid documentVersionId in missingExactEndpointSectionDocumentIds)
        {
            DocumentSection? section = await _context.DocumentSections.AsNoTracking()
                .Where(item => item.DocumentVersionId == documentVersionId)
                .OrderBy(item => item.SectionOrder)
                .ThenBy(item => item.StableSectionId)
                .FirstOrDefaultAsync(cancellationToken);
            if (section is null)
            {
                return ChatEvidenceResult.IncompleteOrStale();
            }

            exactEndpointSections = [.. exactEndpointSections, section];
        }

        DocumentSection[] sections =
            [
                .. baseSections,
                .. supplementarySections,
                .. relationshipProjection.RelatedSections,
                .. exactEndpointSections,
            ];
        if (sections.Length > 20)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        CurrentDocumentRow[] documents = await (
            from logical in _context.LogicalDocuments.AsNoTracking()
            join version in _context.DocumentVersions.AsNoTracking()
                on logical.CurrentVersionId equals version.Id
            join scanRun in _context.ScanRuns.AsNoTracking()
                on version.ScanRunId equals scanRun.Id
            where documentVersionIds.Contains(version.Id) &&
                repositoryIds.Contains(version.RepositoryId) &&
                version.SystemId == request.SystemId &&
                version.SupersededById == null &&
                scanRun.RepositoryId == version.RepositoryId &&
                scanRun.Status == ScanRunStatus.Completed &&
                scanRun.ProfiledRevision == version.ProfiledRevision
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
                canonicalDocuments.All(document =>
                    document.DocumentVersionId != anchor.DocumentVersionId ||
                    document.RepositoryId != anchor.RepositoryId ||
                    document.ScanRunId != anchor.ScanRunId ||
                    !string.Equals(
                        document.ProfiledRevision,
                        anchor.ProfiledRevision,
                        StringComparison.Ordinal)) ||
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

        var relationshipRows = new List<RelationshipPacketRow>();
        if (documentHandles.ContainsKey(exactRelationship.SourceDocumentVersionId) &&
            documentHandles.ContainsKey(exactRelationship.TargetDocumentVersionId))
        {
            relationshipRows.Add(new RelationshipPacketRow(
                exactRelationship.EdgeId,
                ChatRelationshipType.References,
                exactRelationship.SourceDocumentVersionId,
                exactRelationship.TargetDocumentVersionId,
                exactRelationship.DecisionId,
                exactRelationship.ProfileFingerprint));
        }

        relationshipRows.AddRange(provenance
            .Where(relationship =>
                documentHandles.ContainsKey(relationship.SourceDocumentVersionId) &&
                documentHandles.ContainsKey(relationship.TargetDocumentVersionId))
            .Select(relationship => new RelationshipPacketRow(
                relationship.EdgeId,
                ChatRelationshipType.GeneratedFrom,
                relationship.SourceDocumentVersionId,
                relationship.TargetDocumentVersionId,
                null,
                null)));

        Guid[] projectedExactEdgeIds = relationshipProjection.Relationships
            .Where(relationship => relationship.ExactEvidenceFingerprint is not null)
            .Select(relationship => relationship.EdgeId)
            .Distinct()
            .ToArray();
        var projectedExactDecisionRows = projectedExactEdgeIds.Length == 0
            ? []
            : await _context.CrossRepositoryResolutionDecisions.AsNoTracking()
                .Where(decision =>
                    decision.SystemId == request.SystemId &&
                    decision.DocumentEdgeId.HasValue &&
                    projectedExactEdgeIds.Contains(decision.DocumentEdgeId.Value) &&
                    decision.ResolutionSchemaIdentity == "exact-cross-repository-resolution/2.0" &&
                    decision.OutcomeKind == "ExactMatch" &&
                    decision.ReasonCode == "unique_provider")
                .OrderBy(decision => decision.DocumentEdgeId)
                .Select(decision => new ExactDecisionBindingRow(
                    decision.DocumentEdgeId!.Value,
                    decision.Id))
                .Take(33)
                .ToArrayAsync(cancellationToken);
        Dictionary<Guid, Guid> projectedExactDecisionIds = projectedExactDecisionRows
            .GroupBy(binding => binding.EdgeId)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().DecisionId);
        relationshipRows.AddRange(relationshipProjection.Relationships
            .Where(relationship => relationshipRows.All(row => row.EdgeId != relationship.EdgeId))
            .Select(relationship => new RelationshipPacketRow(
                relationship.EdgeId,
                relationship.EdgeType == DocumentEdgeType.References
                    ? ChatRelationshipType.References
                    : ChatRelationshipType.GeneratedFrom,
                relationship.SourceDocumentVersionId,
                relationship.TargetDocumentVersionId,
                projectedExactDecisionIds.GetValueOrDefault(relationship.EdgeId) is Guid decisionId &&
                    decisionId != Guid.Empty
                    ? decisionId
                    : null,
                relationship.ExactProviderEvidenceSetFingerprint)));

        Guid[] relationshipEdgeIds = relationshipRows.Select(row => row.EdgeId).ToArray();
        var flowBindingRows = relationshipEdgeIds.Length == 0
            ? []
            : await _context.FlowGroups.AsNoTracking()
                .Where(flowGroup =>
                    flowGroup.State == "Published" &&
                    flowGroup.SchemaIdentity == "endpoint-flow-group/1.0" &&
                    flowGroup.SystemId == request.SystemId &&
                    flowGroup.ExactResolutionEdgeId.HasValue &&
                    relationshipEdgeIds.Contains(flowGroup.ExactResolutionEdgeId.Value))
                .OrderBy(flowGroup => flowGroup.GroupOrder)
                .ThenBy(flowGroup => flowGroup.Id)
                .Select(flowGroup => new FlowGroupBindingRow(
                    flowGroup.ExactResolutionEdgeId!.Value,
                    flowGroup.Id))
                .Take(33)
                .ToArrayAsync(cancellationToken);
        Dictionary<Guid, Guid> flowGroupIds = flowBindingRows
            .GroupBy(binding => binding.EdgeId)
            .ToDictionary(group => group.Key, group => group.First().FlowGroupId);
        List<(Guid EdgeId, EvidenceRelationship Relationship)> relationships = relationshipRows
            .OrderBy(row => row.RelationshipType == ChatRelationshipType.References ? 0 : 1)
            .ThenBy(row => row.EdgeId)
            .Select((row, index) =>
                (
                    row.EdgeId,
                    new EvidenceRelationship(
                        $"r{index + 1}",
                        row.EdgeId,
                        row.RelationshipType,
                        documentHandles[row.SourceDocumentVersionId],
                        documentHandles[row.TargetDocumentVersionId],
                        1,
                        row.ExactDecisionId,
                        flowGroupIds.GetValueOrDefault(row.EdgeId) is Guid flowGroupId &&
                            flowGroupId != Guid.Empty
                            ? flowGroupId
                            : null,
                        row.ProfileFingerprint)))
            .ToList();
        Dictionary<Guid, string> relationshipHandles = relationships.ToDictionary(
            item => item.EdgeId,
            item => item.Relationship.RelationshipHandle);
        Guid[] selectedFlowGroupIds = flowBindingRows
            .Select(binding => binding.FlowGroupId)
            .Distinct()
            .ToArray();

        FlowRow[] flowRows = await (
            from flowGroup in _context.FlowGroups.AsNoTracking()
            join trace in _context.FlowTraces.AsNoTracking()
                on flowGroup.Id equals trace.FlowGroupId
            join step in _context.FlowTraceSteps.AsNoTracking()
                on trace.Id equals step.FlowTraceId
            where flowGroup.State == "Published" &&
                flowGroup.SchemaIdentity == "endpoint-flow-group/1.0" &&
                flowGroup.SystemId == request.SystemId &&
                selectedFlowGroupIds.Contains(flowGroup.Id) &&
                flowGroup.ExactResolutionEdgeId.HasValue &&
                flowGroup.BusinessLogicDocumentVersionId.HasValue &&
                flowGroup.MembershipFingerprint != null &&
                documentVersionIds.Contains(flowGroup.BusinessLogicDocumentVersionId.Value)
            orderby flowGroup.GroupOrder, trace.TraceOrder, step.StepOrder, step.Id
            select new FlowRow(
                flowGroup.Id,
                flowGroup.SchemaIdentity!,
                flowGroup.State,
                flowGroup.BusinessLogicDocumentVersionId.Value,
                flowGroup.ExactResolutionDecisionId,
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
        if (flowRows.Length == 0)
        {
            bool hasFlowExpansion = await _context.FlowGroups.AsNoTracking().AnyAsync(
                flowGroup =>
                    flowGroup.ExactResolutionEdgeId.HasValue &&
                    relationshipEdgeIds.Contains(flowGroup.ExactResolutionEdgeId.Value),
                cancellationToken);
            return hasFlowExpansion
                ? ChatEvidenceResult.IncompleteOrStale()
                : projectionResult;
        }

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
        bool hasInvalidFlowGroupAuthority = flowGroups.Any(group =>
        {
            RelationshipPacketRow[] matchingRelationships = relationshipRows
                .Where(relationship => relationship.EdgeId == group.ExactResolutionEdgeId)
                .ToArray();
            if (matchingRelationships.Length != 1 ||
                matchingRelationships[0].ExactDecisionId != group.ExactResolutionDecisionId)
            {
                return true;
            }

            if (group.ExactResolutionDecisionId.HasValue)
            {
                return false;
            }

            RelationshipPacketRow relationship = matchingRelationships[0];
            CurrentDocumentRow source = canonicalDocuments.Single(document =>
                document.DocumentVersionId == relationship.SourceDocumentVersionId);
            CurrentDocumentRow target = canonicalDocuments.Single(document =>
                document.DocumentVersionId == relationship.TargetDocumentVersionId);
            return relationship.RelationshipType != ChatRelationshipType.References ||
                   source.RepositoryId != target.RepositoryId;
        });
        if (flowRows.Length > 12 ||
            flowGroups.Length is < 1 or > 10 ||
            hasInvalidFlowGroupAuthority ||
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
            join repository in _context.Repositories.AsNoTracking()
                on version.RepositoryId equals repository.Id
            join scanRun in _context.ScanRuns.AsNoTracking()
                on version.ScanRunId equals scanRun.Id
            join section in _context.DocumentSections.AsNoTracking()
                on version.Id equals section.DocumentVersionId
            join indexEntry in _context.IndexEntries.AsNoTracking()
                on version.Id equals indexEntry.DocumentVersionId into indexEntries
            from indexEntry in indexEntries.DefaultIfEmpty()
            where repositoryIds.Contains(version.RepositoryId) &&
                version.SystemId == request.SystemId &&
                version.SupersededById == null &&
                repository.SystemId == request.SystemId &&
                scanRun.RepositoryId == version.RepositoryId &&
                scanRun.Status == ScanRunStatus.Completed &&
                scanRun.ProfiledRevision == version.ProfiledRevision &&
                (section.SectionKind == DocumentSectionKind.Overview ||
                    section.SectionKind == DocumentSectionKind.BusinessRule ||
                    section.SectionKind == DocumentSectionKind.ProcessStep ||
                    section.SectionKind == DocumentSectionKind.TechnicalDetail ||
                    section.SectionKind == DocumentSectionKind.Evidence)
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
                indexEntry == null ? string.Empty : indexEntry.SearchText,
                repository.Name))
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
                    candidate.SearchText,
                    candidate.DocumentType,
                    candidate.Section.SectionKind,
                    candidate.RepositoryId,
                    candidate.Section.SectionOrder,
                    candidate.RepositoryName)).ToArray(),
                12,
                conversationContext.CurrentDocumentHints,
                request.Mode);
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
            join scanRun in _context.ScanRuns.AsNoTracking()
                on version.ScanRunId equals scanRun.Id
            where documentVersionIds.Contains(version.Id) &&
                repositoryIds.Contains(version.RepositoryId) &&
                version.SystemId == request.SystemId &&
                version.SupersededById == null &&
                scanRun.RepositoryId == version.RepositoryId &&
                scanRun.Status == ScanRunStatus.Completed &&
                scanRun.ProfiledRevision == version.ProfiledRevision
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
                canonicalDocuments.All(document =>
                    document.DocumentVersionId != anchor.DocumentVersionId ||
                    document.RepositoryId != anchor.RepositoryId ||
                    document.ScanRunId != anchor.ScanRunId ||
                    !string.Equals(
                        document.ProfiledRevision,
                        anchor.ProfiledRevision,
                        StringComparison.Ordinal)) ||
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

    private sealed record RelationshipPacketRow(
        Guid EdgeId,
        ChatRelationshipType RelationshipType,
        Guid SourceDocumentVersionId,
        Guid TargetDocumentVersionId,
        Guid? ExactDecisionId,
        string? ProfileFingerprint);

    private sealed record ExactDecisionBindingRow(Guid EdgeId, Guid DecisionId);

    private sealed record FlowGroupBindingRow(Guid EdgeId, Guid FlowGroupId);

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
        string SearchText,
        string RepositoryName);

    private sealed record FlowRow(
        Guid FlowGroupId,
        string SchemaIdentity,
        string State,
        Guid BusinessLogicDocumentVersionId,
        Guid? ExactResolutionDecisionId,
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
