using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;

namespace Vulgata.Infrastructure.Data;

public sealed class ChatEvidenceRetriever(
    VulgataDbContext context,
    ExactResolutionProfileCatalog? profileCatalog = null,
    IPreparedChatScopeResolver? preparedScopeResolver = null,
    ILogger<ChatEvidenceRetriever>? logger = null) : IChatEvidenceRetriever
{
    private readonly VulgataDbContext _context = context;
    private readonly ExactResolutionProfileCatalog _profileCatalog =
        profileCatalog ?? new ExactResolutionProfileCatalog();
    private readonly IPreparedChatScopeResolver _preparedScopeResolver =
        preparedScopeResolver ?? new PreparedChatScopeResolver(
            context,
            profileCatalog ?? new ExactResolutionProfileCatalog());
    private readonly ILogger<ChatEvidenceRetriever>? _logger = logger;

    public async Task<ChatEvidenceResult> RetrieveAsync(
        ChatQuestionRequest request,
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
        ChatQuestionRequest request,
        Guid[] repositoryIds,
        CancellationToken cancellationToken)
    {
        ExactResolutionProfile profile = _profileCatalog.Load();
        if (profile.Repositories.Count != 2 || profile.Occurrences.Count != 1)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        ResolutionOccurrence occurrence = profile.Occurrences[0];
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
            from edge in _context.DocumentEdges.AsNoTracking()
            join exact in _context.ExactCrossRepositoryEdgeEvidence.AsNoTracking()
                on edge.Id equals exact.EdgeId
            join source in _context.LogicalDocuments.AsNoTracking()
                on edge.SourceDocumentVersionId equals source.CurrentVersionId
            join target in _context.LogicalDocuments.AsNoTracking()
                on edge.TargetDocumentVersionId equals target.CurrentVersionId
            where edge.EdgeType == DocumentEdgeType.References &&
                repositoryIds.Contains(edge.SourceRepositoryId) &&
                repositoryIds.Contains(edge.TargetRepositoryId) &&
                edge.SourceRepositoryId != edge.TargetRepositoryId &&
                exact.ProfileSchemaVersion == profile.SchemaVersion &&
                exact.ProfileId == profile.ProfileId &&
                exact.ProfileVersion == profile.ProfileVersion &&
                exact.ProfileFingerprint == profile.ProfileFingerprint &&
                exact.OccurrenceId == occurrence.OccurrenceId
            orderby edge.Id
            select new ExactRelationshipRow(
                edge.Id,
                edge.SourceDocumentVersionId,
                edge.SourceRepositoryId,
                edge.TargetDocumentVersionId,
                edge.TargetRepositoryId,
                exact.ConsumerSourceAnchorId,
                exact.ConsumerDocumentVersionId,
                exact.ConsumerRepositoryId,
                exact.ProviderSourceAnchorId,
                exact.ProviderDocumentVersionId,
                exact.ProviderRepositoryId,
                exact.ProfileSchemaVersion,
                exact.ProfileId,
                exact.ProfileVersion,
                exact.ProfileFingerprint,
                exact.OccurrenceId,
                exact.EvidenceFingerprint))
            .Take(2)
            .ToArrayAsync(cancellationToken);
        if (exactRelationships.Length != 1)
        {
            return ChatEvidenceResult.IncompleteOrStale();
        }

        ExactRelationshipRow exactRelationship = exactRelationships[0];
        if (!exactRelationship.MatchesDirection(profile, occurrence))
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
                    section.ContentMarkdown))
                .Take(256)
                .ToArrayAsync(cancellationToken);
        IReadOnlyList<SupplementaryChatSectionCandidate> selectedSupplementary =
            ChatSupplementaryEvidenceSelector.Select(
                request.Question,
                supplementaryCandidates,
                supplementaryLimit);
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

        DocumentSection[] sections = [.. baseSections, .. supplementarySections];
        Guid[] documentVersionIds = baseDocumentVersionIds
            .Concat(selectedSupplementary.Select(item => item.DocumentVersionId))
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
        bool isMissingConsumerAnchor = !anchorIds.Contains(exactRelationship.ConsumerSourceAnchorId);
        bool isMissingProviderAnchor = !anchorIds.Contains(exactRelationship.ProviderSourceAnchorId);
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
        Dictionary<Guid, string> relationshipHandles = relationships.ToDictionary(
            item => item.EdgeId,
            item => item.Relationship.RelationshipHandle);

        FlowRow[] flowRows = await (
            from flowGroup in _context.FlowGroups.AsNoTracking()
            join trace in _context.FlowTraces.AsNoTracking()
                on flowGroup.Id equals trace.FlowGroupId
            join step in _context.FlowTraceSteps.AsNoTracking()
                on trace.Id equals step.FlowTraceId
            where flowGroup.BusinessLogicDocumentVersionId.HasValue &&
                documentVersionIds.Contains(flowGroup.BusinessLogicDocumentVersionId.Value) &&
                documentVersionIds.Contains(step.DocumentVersionId)
            orderby flowGroup.GroupOrder, trace.TraceOrder, step.StepOrder, step.Id
            select new FlowRow(
                step.DocumentVersionId,
                step.EdgeId,
                step.Description))
            .Take(13)
            .ToArrayAsync(cancellationToken);
        if (flowRows.Length is < 1 or > 12 ||
            flowRows.Any(row =>
                !documentHandles.ContainsKey(row.DocumentVersionId) ||
                row.EdgeId is Guid edgeId && !relationshipHandles.ContainsKey(edgeId)))
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

        EvidencePacket packet = EvidencePacket.Create(
            request.RequestId,
            request.Mode,
            request.SystemId,
            repositoryIds,
            evidenceDocuments,
            evidenceSections,
            evidenceAnchors,
            relationships.Select(item => item.Relationship).ToArray(),
            flowEvidence);
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
        Guid EdgeId,
        Guid SourceDocumentVersionId,
        Guid SourceRepositoryId,
        Guid TargetDocumentVersionId,
        Guid TargetRepositoryId,
        Guid ConsumerSourceAnchorId,
        Guid ConsumerDocumentVersionId,
        Guid ConsumerRepositoryId,
        Guid ProviderSourceAnchorId,
        Guid ProviderDocumentVersionId,
        Guid ProviderRepositoryId,
        string ProfileSchemaVersion,
        string ProfileId,
        string ProfileVersion,
        string ProfileFingerprint,
        string OccurrenceId,
        string EvidenceFingerprint)
    {
        internal bool MatchesDirection(
            ExactResolutionProfile profile,
            ResolutionOccurrence occurrence) =>
            SourceDocumentVersionId == ConsumerDocumentVersionId &&
            SourceRepositoryId == ConsumerRepositoryId &&
            TargetDocumentVersionId == ProviderDocumentVersionId &&
            TargetRepositoryId == ProviderRepositoryId &&
            StringComparer.Ordinal.Equals(ProfileSchemaVersion, profile.SchemaVersion) &&
            StringComparer.Ordinal.Equals(ProfileId, profile.ProfileId) &&
            StringComparer.Ordinal.Equals(ProfileVersion, profile.ProfileVersion) &&
            StringComparer.Ordinal.Equals(ProfileFingerprint, profile.ProfileFingerprint) &&
            StringComparer.Ordinal.Equals(OccurrenceId, occurrence.OccurrenceId) &&
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
        DocumentType DocumentType,
        string Title,
        string ProfiledRevision);

    private sealed record ParsedSection(
        DocumentSection Section,
        IReadOnlyList<Guid> SourceAnchorIds,
        IReadOnlyList<Guid> SupportedDocumentVersionIds);

    private sealed record FlowRow(
        Guid DocumentVersionId,
        Guid? EdgeId,
        string Description);
}
