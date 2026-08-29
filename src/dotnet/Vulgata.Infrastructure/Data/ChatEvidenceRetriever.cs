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

public sealed class ChatEvidenceRetriever : IChatEvidenceRetriever
{
    private readonly VulgataDbContext _context;
    private readonly IPreparedChatScopeResolver _preparedScopeResolver;
    private readonly IChatLexicalEvidenceProjector _lexicalProjector;
    private readonly IChatRelationshipProjector _relationshipProjector;
    private readonly ChatEvidencePacketBuilder _packetBuilder;
    private readonly ILogger<ChatEvidenceRetriever>? _logger;

    public ChatEvidenceRetriever(
        VulgataDbContext context,
        IPreparedChatScopeResolver? preparedScopeResolver,
        IChatLexicalEvidenceProjector lexicalProjector,
        IChatRelationshipProjector relationshipProjector,
        ChatEvidencePacketBuilder packetBuilder,
        ILogger<ChatEvidenceRetriever>? logger)
    {
        _context = context;
        _preparedScopeResolver = preparedScopeResolver ?? new PreparedChatScopeResolver(context);
        _lexicalProjector = lexicalProjector;
        _relationshipProjector = relationshipProjector;
        _packetBuilder = packetBuilder;
        _logger = logger;
    }

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
                    accessScope,
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
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        Repository[] repositories = await _context.Repositories.AsNoTracking()
            .Where(repository => repositoryIds.Contains(repository.Id)).Take(3)
            .ToArrayAsync(cancellationToken);
        if (repositories.Length != 2 ||
            repositories.Any(repository => repository.SystemId != request.SystemId))
        {
            return ChatEvidenceResult.NotFoundOrUnauthorized();
        }

        AuthorizedChatSnapshot authorizedSnapshot = await LoadAuthorizedSnapshotAsync(
            request,
            repositoryIds,
            accessScope,
            cancellationToken);
        ChatRetrievalStageResult<ChatLexicalPrimaryProjection> lexicalResult =
            await _lexicalProjector.ProjectAsync(
                authorizedSnapshot,
                request.Question,
                cancellationToken);
        if (lexicalResult.Kind != ChatRetrievalStageResultKind.Succeeded)
        {
            return lexicalResult.Kind == ChatRetrievalStageResultKind.NotFoundOrUnauthorized
                ? ChatEvidenceResult.NotFoundOrUnauthorized()
                : ChatEvidenceResult.IncompleteOrStale();
        }

        ChatLexicalPrimaryProjection primary = lexicalResult.Value!;
        ChatRetrievalStageResult<ChatRelationshipExpansionProjection> expansionResult =
            await _relationshipProjector.ProjectAsync(
                authorizedSnapshot,
                primary,
                cancellationToken);
        if (expansionResult.Kind != ChatRetrievalStageResultKind.Succeeded)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        ChatRelationshipExpansionProjection expansion = expansionResult.Value!;
        Dictionary<string, int> optionalRelationshipUnitRanks = expansion.Relationships
            .Select((relationship, index) => (relationship.RelationshipHandle, Rank: index + 1))
            .ToDictionary(item => item.RelationshipHandle, item => item.Rank, StringComparer.Ordinal);
        var seenPrimaryDocumentHandles = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, int> lexicalExtraSectionRanks = primary.PrimarySections
            .Select((section, index) => (Section: section, Rank: index + 1))
            .Where(item => !seenPrimaryDocumentHandles.Add(item.Section.DocumentHandle))
            .ToDictionary(
                item => item.Section.SectionHandle,
                item => item.Rank,
                StringComparer.Ordinal);
        string[] optionalSectionHandles = expansion.SupportSections
            .Select(section => section.SectionHandle)
            .Concat(lexicalExtraSectionRanks.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        EvidencePacket packet = _packetBuilder.Build(new ChatEvidenceAssembly(
            ChatEvidencePacketBuildRequest.Create(
                request.RequestId,
                request.Mode,
                request.SystemId,
                repositoryIds,
                [.. primary.PrimaryDocuments, .. expansion.AddedDocuments],
                [.. primary.PrimarySections, .. expansion.SupportSections],
                [.. primary.PrimarySourceAnchors, .. expansion.SupportSourceAnchors],
                expansion.Relationships,
                expansion.FlowEvidence,
                expansion.FlowGroups,
                request.AdvisoryContext.ComposedText,
                request.Conversation,
                optionalSectionHandles,
                request.AdvisoryContext.AuthorityFingerprint,
                primary.PrimaryDocuments.Select(document => document.DocumentHandle).ToArray(),
                primary.PrimarySections.Select(section => section.SectionHandle).ToArray(),
                primary.ExpansionSeedDocumentVersionIds.Select(documentVersionId =>
                    primary.PrimaryDocuments.Single(document =>
                        document.DocumentVersionId == documentVersionId).DocumentHandle).ToArray(),
                optionalRelationshipUnitRanks,
                lexicalExtraSectionRanks)));
        return ChatEvidenceResult.Found(packet);
    }

    private async Task<AuthorizedChatSnapshot> LoadAuthorizedSnapshotAsync(
        ChatRetrievalRequest request,
        Guid[] repositoryIds,
        RepositoryAccessScope accessScope,
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
            .ToArrayAsync(cancellationToken);
        if (candidates.Length < 1 ||
            candidates.Select(candidate => candidate.RepositoryId).Distinct().Count() != 2)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidCurrentness);
        }

        CurrentDocumentRow[] documents = await (
            from logical in _context.LogicalDocuments.AsNoTracking()
            join version in _context.DocumentVersions.AsNoTracking()
                on logical.CurrentVersionId equals version.Id
            join scanRun in _context.ScanRuns.AsNoTracking()
                on version.ScanRunId equals scanRun.Id
            where repositoryIds.Contains(version.RepositoryId) &&
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
            .ToArrayAsync(cancellationToken);
        ParsedSection[] parsedSections = candidates
            .Select(candidate => new ParsedSection(
                candidate.Section,
                ParseGuidArrayForSnapshot(candidate.Section.SourceAnchorIdsJson),
                ParseGuidArrayForSnapshot(candidate.Section.DocumentVersionIdsJson)))
            .ToArray();
        Guid[] anchorIds = parsedSections
            .SelectMany(section => section.SourceAnchorIds)
            .Distinct()
            .ToArray();
        SourceAnchor[] anchors = await _context.SourceAnchors.AsNoTracking()
            .Where(anchor => anchorIds.Contains(anchor.Id))
            .ToArrayAsync(cancellationToken);
        AuthorizedChatDocument[] snapshotDocuments = documents
            .Select(document => new AuthorizedChatDocument(
                document.LogicalDocumentId,
                document.DocumentVersionId,
                document.RepositoryId,
                document.ScanRunId,
                document.DocumentType,
                document.Title,
                document.ProfiledRevision))
            .ToArray();
        AuthorizedChatSection[] snapshotSections = candidates
            .Zip(parsedSections)
            .Select(item => new AuthorizedChatSection(
                item.First.DocumentVersionId,
                item.First.Section.StableSectionId,
                item.First.Section.SectionOrder,
                item.First.Section.SectionKind,
                item.First.Section.Heading,
                item.First.Section.ContentMarkdown,
                item.Second.SourceAnchorIds,
                item.Second.SupportedDocumentVersionIds,
                item.First.RepositoryRelativePath,
                item.First.KeySymbolsJson,
                item.First.BusinessTermsJson,
                item.First.TechnicalTermsJson,
                item.First.SearchText,
                item.First.RepositoryName))
            .ToArray();
        AuthorizedChatSourceAnchor[] snapshotAnchors = anchors
            .Select(anchor => new AuthorizedChatSourceAnchor(
                anchor.Id,
                anchor.DocumentVersionId,
                anchor.RepositoryId,
                anchor.ScanRunId,
                anchor.ProfiledRevision,
                anchor.RepositoryRelativePath,
                anchor.LineStart,
                anchor.LineEnd,
                anchor.SourceHash))
            .ToArray();
        return AuthorizedChatSnapshot.Create(
            new AuthorizedChatScope(request.SystemId, repositoryIds, accessScope),
            request.AdvisoryContext,
            snapshotDocuments,
            snapshotSections,
            snapshotAnchors,
            request.Conversation);
    }

    internal static async Task<ChatRetrievalStageResult<ChatRelationshipExpansionProjection>>
        MaterializeExpansionAsync(
            VulgataDbContext context,
            AuthorizedChatSnapshot snapshot,
            ChatLexicalPrimaryProjection primary,
            ChatRelationshipProjection projected,
            CancellationToken cancellationToken)
    {
        Guid systemId = snapshot.Scope.SystemId;
        Guid[] repositoryIds = snapshot.Scope.RepositoryIds.ToArray();
        HashSet<Guid> primaryDocumentIds = primary.PrimaryDocuments
            .Select(document => document.DocumentVersionId).ToHashSet();
        Guid[] addedDocumentIds = projected.Relationships
            .Select(relationship => relationship.TargetDocumentVersionId)
            .Where(documentVersionId => !primaryDocumentIds.Contains(documentVersionId))
            .Distinct().ToArray();
        if (primary.PrimaryDocuments.Count + addedDocumentIds.Length > 5)
        {
            return ChatRetrievalStageResult<ChatRelationshipExpansionProjection>.InconsistentCurrentEvidence();
        }

        CurrentDocumentRow[] addedRows = addedDocumentIds.Length == 0
            ? []
            : await (
                from logical in context.LogicalDocuments.AsNoTracking()
                join version in context.DocumentVersions.AsNoTracking()
                    on logical.CurrentVersionId equals version.Id
                join scanRun in context.ScanRuns.AsNoTracking()
                    on version.ScanRunId equals scanRun.Id
                where addedDocumentIds.Contains(version.Id) &&
                    repositoryIds.Contains(version.RepositoryId) &&
                    version.SystemId == systemId &&
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
                .ToArrayAsync(cancellationToken);
        if (addedRows.Length != addedDocumentIds.Length)
        {
            return ChatRetrievalStageResult<ChatRelationshipExpansionProjection>.InconsistentCurrentEvidence();
        }

        var documentHandles = primary.PrimaryDocuments.ToDictionary(
            document => document.DocumentVersionId,
            document => document.DocumentHandle);
        EvidenceDocument[] addedDocuments = addedRows
            .OrderByDescending(document => document.DocumentType)
            .ThenBy(document => document.RepositoryId)
            .ThenBy(document => document.LogicalDocumentId)
            .Select((document, index) => new EvidenceDocument(
                $"d{primary.PrimaryDocuments.Count + index + 1}",
                document.LogicalDocumentId,
                document.DocumentVersionId,
                document.RepositoryId,
                document.DocumentType,
                document.Title,
                document.ProfiledRevision))
            .ToArray();
        foreach (EvidenceDocument document in addedDocuments)
        {
            documentHandles.Add(document.DocumentVersionId, document.DocumentHandle);
        }

        HashSet<Guid> primarySectionIds = primary.PrimarySections
            .Select(section => section.StableSectionId)
            .ToHashSet();
        ParsedSection[] parsedSupport = projected.RelatedSections
            .Where(section => !primarySectionIds.Contains(section.StableSectionId))
            .Select(ParseSection)
            .ToArray();
        if (primary.PrimarySections.Count + parsedSupport.Length > 20 ||
            parsedSupport.Any(section =>
                !documentHandles.ContainsKey(section.Section.DocumentVersionId) ||
                section.SupportedDocumentVersionIds.Any(id => !documentHandles.ContainsKey(id))))
        {
            return ChatRetrievalStageResult<ChatRelationshipExpansionProjection>.InconsistentCurrentEvidence();
        }

        HashSet<Guid> primaryAnchorIds = primary.PrimarySourceAnchors
            .Select(anchor => anchor.AnchorId)
            .ToHashSet();
        Guid[] supportAnchorIds = parsedSupport.SelectMany(section => section.SourceAnchorIds)
            .Where(anchorId => !primaryAnchorIds.Contains(anchorId))
            .Distinct().ToArray();
        if (primary.PrimarySourceAnchors.Count + supportAnchorIds.Length > 32)
        {
            return ChatRetrievalStageResult<ChatRelationshipExpansionProjection>.InconsistentCurrentEvidence();
        }

        SourceAnchor[] supportAnchorRows = supportAnchorIds.Length == 0
            ? []
            : await context.SourceAnchors.AsNoTracking()
                .Where(anchor => supportAnchorIds.Contains(anchor.Id))
                .ToArrayAsync(cancellationToken);
        if (supportAnchorRows.Length != supportAnchorIds.Length ||
            supportAnchorRows.Any(anchor =>
                !documentHandles.ContainsKey(anchor.DocumentVersionId) ||
                !repositoryIds.Contains(anchor.RepositoryId) ||
                !IsSafeRepositoryRelativePath(anchor.RepositoryRelativePath) ||
                anchor.LineStart < 1 ||
                anchor.LineEnd < anchor.LineStart ||
                !IsLowerHexSha256(anchor.SourceHash)))
        {
            return ChatRetrievalStageResult<ChatRelationshipExpansionProjection>.InconsistentCurrentEvidence();
        }

        var anchorHandles = primary.PrimarySourceAnchors.ToDictionary(
            anchor => anchor.AnchorId,
            anchor => anchor.AnchorHandle);
        EvidenceAnchor[] supportAnchors = supportAnchorRows
            .OrderBy(anchor => documentHandles[anchor.DocumentVersionId], StringComparer.Ordinal)
            .ThenBy(anchor => anchor.RepositoryRelativePath, StringComparer.Ordinal)
            .ThenBy(anchor => anchor.LineStart)
            .ThenBy(anchor => anchor.Id)
            .Select((anchor, index) => new EvidenceAnchor(
                $"a{primary.PrimarySourceAnchors.Count + index + 1}",
                anchor.Id,
                documentHandles[anchor.DocumentVersionId],
                anchor.DocumentVersionId,
                anchor.RepositoryId,
                anchor.RepositoryRelativePath.Replace('\\', '/'),
                anchor.LineStart,
                anchor.LineEnd,
                anchor.SourceHash))
            .ToArray();
        foreach (EvidenceAnchor anchor in supportAnchors)
        {
            anchorHandles.Add(anchor.AnchorId, anchor.AnchorHandle);
        }

        EvidenceSection[] supportSections = parsedSupport
            .OrderBy(section => documentHandles[section.Section.DocumentVersionId], StringComparer.Ordinal)
            .ThenBy(section => section.Section.SectionOrder)
            .ThenBy(section => section.Section.StableSectionId)
            .Select((section, index) => new EvidenceSection(
                $"s{primary.PrimarySections.Count + index + 1}",
                documentHandles[section.Section.DocumentVersionId],
                section.Section.StableSectionId,
                section.Section.SectionOrder,
                section.Section.SectionKind,
                section.Section.Heading,
                section.Section.ContentMarkdown,
                section.SourceAnchorIds.Select(id => anchorHandles[id]).ToArray(),
                section.SupportedDocumentVersionIds.Select(id => documentHandles[id]).ToArray()))
            .ToArray();

        EvidenceRelationship[] relationships = projected.Relationships
            .OrderBy(item => item.EdgeType == DocumentEdgeType.References ? 0 : 1)
            .ThenBy(item => item.EdgeId)
            .Select((item, index) => new EvidenceRelationship(
                $"r{index + 1}",
                item.EdgeId,
                item.EdgeType == DocumentEdgeType.References
                    ? ChatRelationshipType.References
                    : ChatRelationshipType.GeneratedFrom,
                documentHandles[item.SourceDocumentVersionId],
                documentHandles[item.TargetDocumentVersionId],
                1,
                item.SelectedExactDecisionId,
                item.ExactDecisionCount,
                item.ExactDecisionSetFingerprint,
                item.FlowGroupId,
                item.ExactProviderEvidenceSetFingerprint))
            .ToArray();
        Dictionary<Guid, string> relationshipHandles = relationships.ToDictionary(
            relationship => relationship.EdgeId,
            relationship => relationship.RelationshipHandle);
        EvidenceFlowStep[] flowEvidence = projected.FlowGroups
            .SelectMany(group => group.Steps)
            .OrderBy(step => step.StepOrder)
            .ThenBy(step => step.FlowTraceStepId)
            .Select((step, index) => new EvidenceFlowStep(
                index + 1,
                documentHandles[step.DocumentVersionId],
                step.EdgeId is Guid edgeId ? relationshipHandles[edgeId] : null,
                step.Description))
            .ToArray();

        return ChatRetrievalStageResult<ChatRelationshipExpansionProjection>.Succeeded(
            ChatRelationshipExpansionProjection.Create(
                primary,
                addedDocuments,
                supportSections,
                supportAnchors,
                relationships,
                flowEvidence,
                projected.FlowGroups));
    }
    internal static ChatRetrievalStageResult<ChatLexicalPrimaryProjection>
        RetrieveLexicalPrimaryAsync(
        AuthorizedChatSnapshot snapshot,
        string question,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Guid[] repositoryIds = snapshot.Scope.RepositoryIds.ToArray();
        ChatMode mode = snapshot.Conversation.Scope.Mode;
        Dictionary<Guid, AuthorizedChatDocument> documentsById = snapshot.Documents
            .ToDictionary(document => document.DocumentVersionId);
        SnapshotSectionRow[] candidates = snapshot.Sections
            .Select(section => new SnapshotSectionRow(
                documentsById.GetValueOrDefault(section.DocumentVersionId),
                section))
            .ToArray();
        if (candidates.Length < 1 ||
            candidates.Any(candidate => candidate.Document is null) ||
            candidates.Select(candidate => candidate.Document!.RepositoryId).Distinct().Count() != 2)
        {
            return ChatRetrievalStageResult<ChatLexicalPrimaryProjection>.InconsistentCurrentEvidence();
        }

        Guid[] currentLogicalDocumentIds = candidates
            .Select(candidate => candidate.Document!.LogicalDocumentId)
            .Distinct()
            .ToArray();
        ChatConversationRetrievalContext conversationContext =
            snapshot.Conversation.BuildRetrievalContext(
                currentLogicalDocumentIds,
                currentLogicalDocumentIds);
        if (conversationContext.IsMissingEquivalent)
        {
            return ChatRetrievalStageResult<ChatLexicalPrimaryProjection>
                .NotFoundOrUnauthorized();
        }

        string retrievalQuestion = string.Join(
            ' ',
            snapshot.Conversation.Turns
                .Where(turn => turn.LogicalDocumentIds.Any(
                    conversationContext.CurrentDocumentHints.Contains))
                .Select(turn => turn.Question)
                .Append(question));
        SupplementaryChatSectionCandidate[] selectionCandidates = candidates
            .Select(candidate => new SupplementaryChatSectionCandidate(
                candidate.Document!.DocumentVersionId,
                candidate.Section.StableSectionId,
                candidate.Document.Title,
                candidate.Section.Heading,
                candidate.Section.ContentMarkdown,
                candidate.Document.LogicalDocumentId,
                candidate.Section.RepositoryRelativePath,
                candidate.Section.KeySymbolsJson,
                candidate.Section.BusinessTermsJson,
                candidate.Section.TechnicalTermsJson,
                candidate.Section.SearchText,
                candidate.Document.DocumentType,
                candidate.Section.SectionKind,
                candidate.Document.RepositoryId,
                candidate.Section.SectionOrder,
                candidate.Section.RepositoryName))
            .ToArray();
        IReadOnlyList<SupplementaryChatSectionCandidate> selected =
            ChatSupplementaryEvidenceSelector.Select(
                retrievalQuestion,
                selectionCandidates,
                12,
                conversationContext.CurrentDocumentHints,
                mode);
        if (selected.Count == 0)
        {
            string fallbackQuestion = string.Join(
                ' ',
                snapshot.AdvisoryContext.Terms.Prepend(retrievalQuestion));
            SupplementaryChatSectionCandidate[] businessSummaries = selectionCandidates
                .Where(candidate =>
                    candidate.DocumentType == DocumentType.BusinessLogic &&
                    candidate.SectionKind == DocumentSectionKind.Overview)
                .Select(candidate => candidate with
                {
                    RepositoryRelativePath = string.Empty,
                    KeySymbolsJson = string.Empty,
                    BusinessTermsJson = string.Empty,
                    TechnicalTermsJson = string.Empty,
                    RepositoryName = string.Empty,
                })
                .ToArray();
            selected = ChatSupplementaryEvidenceSelector.Select(
                fallbackQuestion,
                businessSummaries,
                12,
                conversationContext.CurrentDocumentHints,
                mode);
        }

        if (selected.Count == 0)
        {
            return ChatRetrievalStageResult<ChatLexicalPrimaryProjection>.InsufficientEvidence();
        }

        HashSet<Guid> selectedSectionIds = selected
            .Select(candidate => candidate.StableSectionId)
            .ToHashSet();
        SnapshotSectionRow[] selectedRows = candidates
            .Where(candidate => selectedSectionIds.Contains(candidate.Section.StableSectionId))
            .ToArray();
        SnapshotParsedSection[] parsedSections = selectedRows
            .Select(candidate => new SnapshotParsedSection(
                candidate.Section,
                candidate.Section.SourceAnchorIds,
                candidate.Section.SupportedDocumentVersionIds))
            .ToArray();
        if (parsedSections.Any(section =>
                section.SourceAnchorIds.Count < 1 ||
                section.SourceAnchorIds.Any(id => id == Guid.Empty) ||
                section.SourceAnchorIds.Distinct().Count() != section.SourceAnchorIds.Count ||
                section.SupportedDocumentVersionIds.Any(id => id == Guid.Empty) ||
                section.SupportedDocumentVersionIds.Distinct().Count() !=
                    section.SupportedDocumentVersionIds.Count))
        {
            return ChatRetrievalStageResult<ChatLexicalPrimaryProjection>.InconsistentCurrentEvidence();
        }

        Guid[] documentVersionIds = selectedRows
            .Select(candidate => candidate.Document!.DocumentVersionId)
            .Concat(parsedSections.SelectMany(section => section.SupportedDocumentVersionIds))
            .Distinct()
            .ToArray();
        if (documentVersionIds.Length is < 1 or > 5)
        {
            return ChatRetrievalStageResult<ChatLexicalPrimaryProjection>.InconsistentCurrentEvidence();
        }

        AuthorizedChatDocument[] documents = snapshot.Documents
            .Where(document => documentVersionIds.Contains(document.DocumentVersionId))
            .ToArray();
        if (documents.Length != documentVersionIds.Length)
        {
            return ChatRetrievalStageResult<ChatLexicalPrimaryProjection>.InconsistentCurrentEvidence();
        }

        AuthorizedChatDocument[] canonicalDocuments = documents
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
            return ChatRetrievalStageResult<ChatLexicalPrimaryProjection>.InconsistentCurrentEvidence();
        }

        AuthorizedChatSourceAnchor[] anchors = snapshot.SourceAnchors
            .Where(anchor => anchorIds.Contains(anchor.AnchorId))
            .ToArray();
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
            return ChatRetrievalStageResult<ChatLexicalPrimaryProjection>.InconsistentCurrentEvidence();
        }

        AuthorizedChatSourceAnchor[] canonicalAnchors = anchors
            .OrderBy(anchor => documentHandles[anchor.DocumentVersionId], StringComparer.Ordinal)
            .ThenBy(anchor => anchor.RepositoryRelativePath, StringComparer.Ordinal)
            .ThenBy(anchor => anchor.LineStart)
            .ThenBy(anchor => anchor.AnchorId)
            .ToArray();
        Dictionary<Guid, string> anchorHandles = canonicalAnchors
            .Select((anchor, index) => (anchor.AnchorId, Handle: $"a{index + 1}"))
            .ToDictionary(item => item.AnchorId, item => item.Handle);
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
                anchorHandles[anchor.AnchorId],
                anchor.AnchorId,
                documentHandles[anchor.DocumentVersionId],
                anchor.DocumentVersionId,
                anchor.RepositoryId,
                anchor.RepositoryRelativePath.Replace('\\', '/'),
                anchor.LineStart,
                anchor.LineEnd,
                anchor.SourceHash))
            .ToArray();

        foreach (EvidenceDocument document in evidenceDocuments)
        {
            KnowledgeTextSafety.EnsureSafe(document.Title);
        }

        foreach (EvidenceSection section in evidenceSections)
        {
            KnowledgeTextSafety.EnsureSafe(section.Heading);
            KnowledgeTextSafety.EnsureSafe(section.ContentMarkdown);
        }

        string primaryBindingFingerprint = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
                '|',
                evidenceDocuments.Select(document => document.DocumentVersionId.ToString("D"))
                    .Concat(evidenceSections.Select(section => section.StableSectionId.ToString("D")))
                    .Concat(evidenceAnchors.Select(anchor => anchor.AnchorId.ToString("D")))))));
        return ChatRetrievalStageResult<ChatLexicalPrimaryProjection>.Succeeded(
            ChatLexicalPrimaryProjection.Create(
                evidenceDocuments,
                evidenceSections,
                evidenceAnchors,
                primaryBindingFingerprint));
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

    private static Guid[] ParseGuidArrayForSnapshot(string json)
    {
        try
        {
            Guid[]? values = JsonSerializer.Deserialize<Guid[]>(json);
            return values is null || values.Any(value => value == Guid.Empty)
                ? [Guid.Empty]
                : values;
        }
        catch (JsonException)
        {
            return [Guid.Empty];
        }
    }

    private static bool IsLowerHexSha256(string value) =>
        value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

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

    private sealed record SnapshotParsedSection(
        AuthorizedChatSection Section,
        IReadOnlyList<Guid> SourceAnchorIds,
        IReadOnlyList<Guid> SupportedDocumentVersionIds);

    private sealed record SnapshotSectionRow(
        AuthorizedChatDocument? Document,
        AuthorizedChatSection Section);

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

}
