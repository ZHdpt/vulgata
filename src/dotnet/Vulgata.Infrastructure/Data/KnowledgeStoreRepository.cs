using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class KnowledgeStoreRepository(
    VulgataDbContext context,
    KnowledgeDocumentMaterializer materializer) : IKnowledgeStoreRepository
{
    private readonly VulgataDbContext _context = context;
    private readonly KnowledgeDocumentMaterializer _materializer = materializer;

    public async Task<KnowledgeCommitResult> CommitAsync(
        KnowledgeCommit request,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        try
        {
            KnowledgeStoreContractValidator.ValidateCommit(request);
        }
        catch (KnowledgeStoreContractException exception)
        {
            _context.ChangeTracker.Clear();
            return KnowledgeCommitResult.Rejected(exception.RejectionCode);
        }

        if (!scope.Contains(request.RepositoryId))
        {
            return RejectAndClear(KnowledgeRejectionCode.UnauthorizedEndpoint);
        }

        IExecutionStrategy strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                KnowledgeCommitResult? verifiedReplay =
                    await VerifyCommittedReplayAsync(request, cancellationToken);
                if (verifiedReplay is not null)
                {
                    _context.ChangeTracker.Clear();
                    return verifiedReplay;
                }

                bool isSqlite = StringComparer.Ordinal.Equals(
                    _context.Database.ProviderName,
                    "Microsoft.EntityFrameworkCore.Sqlite");
                await using IDbContextTransaction transaction =
                    await _context.Database.BeginTransactionAsync(
                        isSqlite ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted,
                        cancellationToken);
                try
                {
                    if (isSqlite)
                    {
                        await _context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE \"Repositories\" SET \"Id\" = \"Id\" WHERE \"Id\" = {request.RepositoryId}",
                            cancellationToken);
                    }
                    else if (StringComparer.Ordinal.Equals(
                            _context.Database.ProviderName,
                            "Npgsql.EntityFrameworkCore.PostgreSQL"))
                    {
                        await AcquirePostgreSqlOwnerLocksAsync(request, cancellationToken);
                    }

                    KnowledgeCommitResult? rejected =
                        await ValidateDurableOwnershipAsync(request, scope, cancellationToken);
                    if (rejected is not null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _context.ChangeTracker.Clear();
                        return rejected;
                    }

                    Repository? owner = await _context.Repositories
                        .AsNoTracking()
                        .SingleOrDefaultAsync(
                            repository => repository.Id == request.RepositoryId,
                            cancellationToken);
                    if (owner is null)
                    {
                        return await RollbackAndRejectAsync(
                            transaction,
                            KnowledgeRejectionCode.InvalidOwnership,
                            cancellationToken);
                    }

                    KnowledgeDocumentMaterialization projection = _materializer.Materialize(
                        request.LogicalDocumentId,
                        request.NewDocumentVersionId,
                        request.RepositoryId,
                        owner.SystemId,
                        request.Content,
                        request.CommittedAt);
                    LogicalDocument logicalDocument;
                    int versionNumber;
                    if (request.ExpectedCurrentVersionId is null)
                    {
                        logicalDocument = LogicalDocument.Create(
                            request.LogicalDocumentId,
                            request.RepositoryId,
                            request.Content.DocumentType,
                            request.Content.StableKey,
                            request.CommittedAt);
                        _context.Set<LogicalDocument>().Add(logicalDocument);
                        await _context.SaveChangesAsync(cancellationToken);
                        versionNumber = 1;
                    }
                    else
                    {
                        logicalDocument = await _context.Set<LogicalDocument>()
                            .Include(document => document.CurrentVersion)
                            .SingleAsync(
                                document =>
                                    document.Id == request.LogicalDocumentId &&
                                    document.RepositoryId == request.RepositoryId,
                                cancellationToken);
                        versionNumber = logicalDocument.CurrentVersion!.VersionNumber + 1;
                    }

                    var version = DocumentVersion.Create(
                        request.NewDocumentVersionId,
                        request.LogicalDocumentId,
                        request.RepositoryId,
                        owner.SystemId,
                        request.ScanRunId,
                        request.ProfiledRevision,
                        versionNumber,
                        projection.Content.Title,
                        projection.Content.RepositoryRelativePath,
                        projection.Content.SchemaIdentity,
                        projection.ContentJson,
                        projection.ContentMarkdown,
                        projection.ContentFingerprint,
                        request.CommittedAt);
                    if (request.ExpectedCurrentVersionId is null)
                    {
                        _context.Set<DocumentVersion>().Add(version);
                    }
                    else
                    {
                        logicalDocument.MoveCurrentVersion(
                            request.ExpectedCurrentVersionId.Value,
                            version);
                    }

                    AddEvidence(request);
                    _context.Set<IndexEntry>().Add(projection.IndexEntry);
                    _context.Set<DocumentSection>().AddRange(projection.Sections);
                    await _context.SaveChangesAsync(cancellationToken);

                    if (request.ExpectedCurrentVersionId is null)
                    {
                        logicalDocument.SetInitialCurrentVersion(version);
                        await _context.SaveChangesAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                    return KnowledgeCommitResult.Committed(
                        request.LogicalDocumentId,
                        request.NewDocumentVersionId);
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    _context.ChangeTracker.Clear();
                    throw;
                }
            });
        }
        catch (KnowledgeStoreContractException exception)
        {
            return RejectAndClear(exception.RejectionCode);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConflictAndClear();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return RejectAndClear(KnowledgeRejectionCode.InvalidOwnership);
        }
    }

    private async Task<KnowledgeCommitResult?> VerifyCommittedReplayAsync(
        KnowledgeCommit request,
        CancellationToken cancellationToken)
    {
        DocumentVersion? version = await _context.Set<DocumentVersion>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == request.NewDocumentVersionId,
                cancellationToken);
        if (version is null)
        {
            return null;
        }

        LogicalDocument? logicalDocument = await _context.Set<LogicalDocument>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == request.LogicalDocumentId,
                cancellationToken);
        if (logicalDocument is null ||
            logicalDocument.RepositoryId != request.RepositoryId ||
            logicalDocument.DocumentType != request.Content.DocumentType ||
            !StringComparer.Ordinal.Equals(logicalDocument.StableKey, request.Content.StableKey) ||
            logicalDocument.CurrentVersionId != request.NewDocumentVersionId ||
            version.LogicalDocumentId != request.LogicalDocumentId ||
            version.RepositoryId != request.RepositoryId ||
            version.ScanRunId != request.ScanRunId ||
            !StringComparer.Ordinal.Equals(version.ProfiledRevision, request.ProfiledRevision))
        {
            return null;
        }

        KnowledgeDocumentMaterialization expected = _materializer.Materialize(
            request.LogicalDocumentId,
            request.NewDocumentVersionId,
            request.RepositoryId,
            version.SystemId,
            request.Content,
            version.CreatedAt);
        if (!StringComparer.Ordinal.Equals(version.Title, expected.Content.Title) ||
            !StringComparer.Ordinal.Equals(
                version.RepositoryRelativePath,
                expected.Content.RepositoryRelativePath) ||
            !StringComparer.Ordinal.Equals(version.SchemaIdentity, expected.Content.SchemaIdentity) ||
            !StringComparer.Ordinal.Equals(version.ContentMarkdown, expected.ContentMarkdown) ||
            !StringComparer.Ordinal.Equals(version.ContentFingerprint, expected.ContentFingerprint))
        {
            return null;
        }

        if (request.ExpectedCurrentVersionId is null)
        {
            if (version.VersionNumber != 1)
            {
                return null;
            }
        }
        else
        {
            bool predecessorMatches = await _context.Set<DocumentVersion>()
                .AsNoTracking()
                .AnyAsync(
                    candidate =>
                        candidate.Id == request.ExpectedCurrentVersionId.Value &&
                        candidate.LogicalDocumentId == request.LogicalDocumentId &&
                        candidate.RepositoryId == request.RepositoryId &&
                        candidate.SupersededById == request.NewDocumentVersionId,
                    cancellationToken);
            if (!predecessorMatches)
            {
                return null;
            }
        }

        return KnowledgeCommitResult.Committed(
            request.LogicalDocumentId,
            request.NewDocumentVersionId);
    }

    public async Task<RebuildProjectionStatus> RebuildProjectionAsync(
        Guid documentVersionId,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (documentVersionId == Guid.Empty)
        {
            return RebuildProjectionStatus.Rejected;
        }

        IExecutionStrategy strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken);
                try
                {
                    Guid[] authorizedRepositoryIds = scope.AuthorizedRepositoryIds.ToArray();
                    DocumentVersion? version = await _context.Set<DocumentVersion>()
                        .AsNoTracking()
                        .SingleOrDefaultAsync(
                            candidate =>
                                candidate.Id == documentVersionId &&
                                authorizedRepositoryIds.Contains(candidate.RepositoryId),
                            cancellationToken);
                    if (version is null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _context.ChangeTracker.Clear();
                        return RebuildProjectionStatus.NotFoundOrUnauthorized;
                    }

                    KnowledgeDocumentMaterialization projection = _materializer.Materialize(
                        version.LogicalDocumentId,
                        version.Id,
                        version.RepositoryId,
                        version.SystemId,
                        version.ContentJson,
                        version.CreatedAt);
                    if (!StringComparer.Ordinal.Equals(
                            projection.ContentJson,
                            version.ContentJson) ||
                        !StringComparer.Ordinal.Equals(
                            projection.ContentMarkdown,
                            version.ContentMarkdown) ||
                        !StringComparer.Ordinal.Equals(
                            projection.ContentFingerprint,
                            version.ContentFingerprint))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _context.ChangeTracker.Clear();
                        return RebuildProjectionStatus.Rejected;
                    }

                    await _context.Set<DocumentSection>()
                        .Where(section => section.DocumentVersionId == version.Id)
                        .ExecuteDeleteAsync(cancellationToken);
                    await _context.Set<IndexEntry>()
                        .Where(entry => entry.DocumentVersionId == version.Id)
                        .ExecuteDeleteAsync(cancellationToken);
                    _context.Set<IndexEntry>().Add(projection.IndexEntry);
                    _context.Set<DocumentSection>().AddRange(projection.Sections);
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                    return RebuildProjectionStatus.Rebuilt;
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    _context.ChangeTracker.Clear();
                    throw;
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _context.ChangeTracker.Clear();
            return RebuildProjectionStatus.Rejected;
        }
    }

    private async Task AcquirePostgreSqlOwnerLocksAsync(
        KnowledgeCommit request,
        CancellationToken cancellationToken)
    {
        Guid[] endpointVersionIds = request.Edges
            .Select(edge => edge.TargetDocumentVersionId)
            .Concat(
                request.FlowGroups
                    .SelectMany(group => group.Traces)
                    .SelectMany(trace => trace.Steps)
                    .Where(step => step.DocumentVersionId != request.NewDocumentVersionId)
                    .Select(step => step.DocumentVersionId))
            .Distinct()
            .ToArray();
        Guid[] targetOwnerIds = endpointVersionIds.Length == 0
            ? []
            : await _context.Set<DocumentVersion>()
                .AsNoTracking()
                .Where(version => endpointVersionIds.Contains(version.Id))
                .Select(version => version.LogicalDocumentId)
                .Distinct()
                .ToArrayAsync(cancellationToken);
        Guid[] sourceOwnerIds = await _context.Set<LogicalDocument>()
            .AsNoTracking()
            .Where(document => document.Id == request.LogicalDocumentId)
            .Select(document => document.Id)
            .ToArrayAsync(cancellationToken);
        Guid[] ownerIds = sourceOwnerIds
            .Concat(targetOwnerIds)
            .Distinct()
            .ToArray();
        if (ownerIds.Length == 0)
        {
            return;
        }

        _ = await _context.Set<LogicalDocument>()
            .FromSqlInterpolated(
                $"SELECT * FROM \"vulgata\".\"LogicalDocuments\" WHERE \"Id\" = ANY ({ownerIds}) ORDER BY \"Id\" FOR UPDATE")
            .AsNoTracking()
            .Select(document => document.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<CurrentKnowledge?> GetCurrentAsync(
        Guid logicalDocumentId,
        RepositoryAccessScope scope,
        IReadOnlyCollection<Guid> requestedStableSectionIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        KnowledgeStoreContractValidator.ValidateRequestedStableSectionIds(
            requestedStableSectionIds);

        Guid[] authorizedRepositoryIds = scope.AuthorizedRepositoryIds.ToArray();
        var source = await _context.Set<LogicalDocument>()
            .AsNoTracking()
            .Where(document =>
                document.Id == logicalDocumentId &&
                authorizedRepositoryIds.Contains(document.RepositoryId))
            .Select(document => new
            {
                document.Id,
                document.RepositoryId,
                document.DocumentType,
                document.CurrentVersionId,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (source?.CurrentVersionId is not Guid currentVersionId)
        {
            return null;
        }

        DocumentVersion? version = await _context.Set<DocumentVersion>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == currentVersionId &&
                    candidate.LogicalDocumentId == source.Id &&
                    candidate.RepositoryId == source.RepositoryId,
                cancellationToken);
        if (version is null)
        {
            return null;
        }

        IndexEntry index = await _context.Set<IndexEntry>()
            .AsNoTracking()
            .SingleAsync(
                entry =>
                    entry.DocumentVersionId == version.Id &&
                    entry.LogicalDocumentId == source.Id &&
                    entry.RepositoryId == source.RepositoryId,
                cancellationToken);
        Guid[] requestedIds = requestedStableSectionIds.ToArray();
        DocumentSection[] sections = await _context.Set<DocumentSection>()
            .AsNoTracking()
            .Where(section =>
                section.DocumentVersionId == version.Id &&
                section.LogicalDocumentId == source.Id &&
                section.RepositoryId == source.RepositoryId &&
                requestedIds.Contains(section.StableSectionId))
            .OrderBy(section => section.SectionOrder)
            .ToArrayAsync(cancellationToken);
        string[] allSectionDocumentReferences = await _context.Set<DocumentSection>()
            .AsNoTracking()
            .Where(section =>
                section.DocumentVersionId == version.Id &&
                section.LogicalDocumentId == source.Id &&
                section.RepositoryId == source.RepositoryId)
            .Select(section => section.DocumentVersionIdsJson)
            .ToArrayAsync(cancellationToken);
        CurrentSourceAnchor[] anchors = await _context.Set<SourceAnchor>()
            .AsNoTracking()
            .Where(anchor =>
                anchor.DocumentVersionId == version.Id &&
                anchor.RepositoryId == source.RepositoryId)
            .OrderBy(anchor => anchor.Id)
            .Select(anchor => new CurrentSourceAnchor(
                anchor.Id,
                anchor.CodeUnitId,
                anchor.RepositoryRelativePath,
                anchor.EvidenceLocator,
                anchor.LineStart,
                anchor.LineEnd,
                anchor.SourceHash))
            .Take(KnowledgeDocumentLimits.MaximumAnchors)
            .ToArrayAsync(cancellationToken);
        DocumentEdge[] edges = await _context.Set<DocumentEdge>()
            .AsNoTracking()
            .Where(edge =>
                edge.SourceDocumentVersionId == version.Id &&
                edge.SourceRepositoryId == source.RepositoryId)
            .OrderBy(edge => edge.Id)
            .Take(KnowledgeDocumentLimits.MaximumEdges)
            .ToArrayAsync(cancellationToken);
        CurrentRelationship[] relationships = edges
            .Select<DocumentEdge, CurrentRelationship>(edge =>
                scope.Contains(edge.TargetRepositoryId)
                    ? new AuthorizedRelationship(
                        edge.Id,
                        edge.EdgeType,
                        edge.SourceDocumentVersionId,
                        edge.TargetDocumentVersionId,
                        edge.TargetRepositoryId)
                    : new RestrictedRelationship())
            .ToArray();
        IReadOnlyList<CurrentFlowGroup> flowGroups = await LoadFlowGroupsAsync(
            version.Id,
            source.RepositoryId,
            scope,
            cancellationToken);
        Guid[] referencedDocumentVersionIds = allSectionDocumentReferences
            .SelectMany(DeserializeGuids)
            .Distinct()
            .ToArray();
        Guid[] authorizedDocumentVersionIds = referencedDocumentVersionIds.Length == 0
            ? []
            : await _context.Set<DocumentVersion>()
                .AsNoTracking()
                .Where(candidate =>
                    referencedDocumentVersionIds.Contains(candidate.Id) &&
                    authorizedRepositoryIds.Contains(candidate.RepositoryId))
                .Select(candidate => candidate.Id)
                .ToArrayAsync(cancellationToken);
        var authorizedDocumentVersionIdSet = authorizedDocumentVersionIds.ToHashSet();

        return new CurrentKnowledge(
            source.Id,
            version.Id,
            source.RepositoryId,
            source.DocumentType,
            version.Title,
            version.RepositoryRelativePath,
            RedactDocumentReferences(
                version.ContentJson,
                authorizedDocumentVersionIdSet),
            version.ContentMarkdown,
            anchors,
            relationships,
            flowGroups,
            new CurrentIndexEntry(
                index.Summary,
                DeserializeStrings(index.KeySymbolsJson),
                DeserializeStrings(index.BusinessTermsJson),
                DeserializeStrings(index.TechnicalTermsJson),
                index.SearchText),
            sections.Select(section =>
            {
                IReadOnlyList<Guid> documentVersionIds =
                    DeserializeGuids(section.DocumentVersionIdsJson);
                return new CurrentDocumentSection(
                    section.StableSectionId,
                    section.SectionOrder,
                    section.SectionKind,
                    section.Heading,
                    section.ContentMarkdown,
                    section.SearchText,
                    DeserializeGuids(section.SourceAnchorIdsJson),
                    documentVersionIds
                        .Where(authorizedDocumentVersionIdSet.Contains)
                        .ToArray(),
                    documentVersionIds.Any(id =>
                        !authorizedDocumentVersionIdSet.Contains(id)));
            }).ToArray());
    }

    private async Task<IReadOnlyList<CurrentFlowGroup>> LoadFlowGroupsAsync(
        Guid documentVersionId,
        Guid repositoryId,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken)
    {
        FlowGroup[] groups = await _context.Set<FlowGroup>()
            .AsNoTracking()
            .Where(group =>
                group.BusinessLogicDocumentVersionId == documentVersionId &&
                group.RepositoryId == repositoryId)
            .OrderBy(group => group.GroupOrder)
            .Take(KnowledgeDocumentLimits.MaximumFlowGroups)
            .ToArrayAsync(cancellationToken);
        var currentGroups = new List<CurrentFlowGroup>(groups.Length);
        foreach (FlowGroup group in groups)
        {
            FlowTrace[] traces = await _context.Set<FlowTrace>()
                .AsNoTracking()
                .Where(trace => trace.FlowGroupId == group.Id)
                .OrderBy(trace => trace.TraceOrder)
                .Take(KnowledgeDocumentLimits.MaximumTracesPerGroup)
                .ToArrayAsync(cancellationToken);
            var currentTraces = new List<CurrentFlowTrace>(traces.Length);
            foreach (FlowTrace trace in traces)
            {
                FlowTraceStep[] steps = await _context.Set<FlowTraceStep>()
                    .AsNoTracking()
                    .Where(step => step.FlowTraceId == trace.Id)
                    .OrderBy(step => step.StepOrder)
                    .Take(KnowledgeDocumentLimits.MaximumStepsPerTrace)
                    .ToArrayAsync(cancellationToken);
                CurrentFlowTraceStep[] currentSteps = steps.Select(step =>
                {
                    bool isRestricted = !scope.Contains(step.DocumentRepositoryId);
                    return new CurrentFlowTraceStep(
                        step.Id,
                        step.StepOrder,
                        step.StepKind,
                        isRestricted ? null : step.DocumentVersionId,
                        isRestricted ? null : step.Description,
                        isRestricted);
                }).ToArray();
                currentTraces.Add(
                    new CurrentFlowTrace(trace.Id, trace.TraceOrder, currentSteps));
            }

            currentGroups.Add(
                new CurrentFlowGroup(group.Id, group.GroupOrder, group.Title, currentTraces));
        }

        return currentGroups;
    }

    private static IReadOnlyList<string> DeserializeStrings(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? [];

    private static IReadOnlyList<Guid> DeserializeGuids(string json) =>
        JsonSerializer.Deserialize<Guid[]>(json) ?? [];

    private static string RedactDocumentReferences(
        string contentJson,
        HashSet<Guid> authorizedDocumentVersionIds)
    {
        JsonNode root = JsonNode.Parse(contentJson)
            ?? throw new JsonException("Canonical knowledge content must be a JSON value.");
        if (root["sections"] is not JsonArray sections)
        {
            return contentJson;
        }

        foreach (JsonNode? section in sections)
        {
            if (section?["documentVersionIds"] is not JsonArray documentVersionIds)
            {
                continue;
            }

            for (int index = documentVersionIds.Count - 1; index >= 0; index--)
            {
                string? value = documentVersionIds[index]?.GetValue<string>();
                if (!Guid.TryParse(value, out Guid id) ||
                    !authorizedDocumentVersionIds.Contains(id))
                {
                    documentVersionIds.RemoveAt(index);
                }
            }
        }

        return root.ToJsonString();
    }

    private async Task<KnowledgeCommitResult?> ValidateDurableOwnershipAsync(
        KnowledgeCommit request,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken)
    {
        var document = await _context.Set<LogicalDocument>()
            .AsNoTracking()
            .Where(
                document =>
                    document.Id == request.LogicalDocumentId ||
                    (document.RepositoryId == request.RepositoryId &&
                        document.DocumentType == request.Content.DocumentType &&
                        document.StableKey == request.Content.StableKey))
            .Select(document => new
            {
                document.Id,
                document.RepositoryId,
                document.DocumentType,
                document.StableKey,
                document.CurrentVersionId,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (request.ExpectedCurrentVersionId is null)
        {
            if (document is not null)
            {
                return KnowledgeCommitResult.Conflict();
            }
        }
        else if (document is null ||
            document.Id != request.LogicalDocumentId ||
            document.RepositoryId != request.RepositoryId ||
            document.DocumentType != request.Content.DocumentType ||
            !StringComparer.Ordinal.Equals(document.StableKey, request.Content.StableKey) ||
            document.CurrentVersionId != request.ExpectedCurrentVersionId)
        {
            return KnowledgeCommitResult.Conflict();
        }

        ScanRun? scanRun = await _context.ScanRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == request.ScanRunId &&
                    candidate.RepositoryId == request.RepositoryId,
                cancellationToken);
        if (scanRun is null ||
            !StringComparer.Ordinal.Equals(scanRun.ProfiledRevision, request.ProfiledRevision))
        {
            return KnowledgeCommitResult.Rejected(KnowledgeRejectionCode.InvalidOwnership);
        }

        foreach (SourceAnchorDraft anchor in request.SourceAnchors)
        {
            CodeUnit? codeUnit = await _context.CodeUnits
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    unit =>
                        unit.Id == anchor.CodeUnitId &&
                        unit.ScanRunId == anchor.ScanRunId &&
                        unit.RepositoryId == anchor.RepositoryId,
                    cancellationToken);
            if (codeUnit is null ||
                !StringComparer.Ordinal.Equals(
                    codeUnit.FilePath.Replace('\\', '/'),
                    anchor.RepositoryRelativePath.Replace('\\', '/')) ||
                anchor.LineStart < codeUnit.LineStart ||
                anchor.LineEnd > codeUnit.LineEnd)
            {
                return KnowledgeCommitResult.Rejected(KnowledgeRejectionCode.InvalidAnchor);
            }
        }

        var flowEndpoints = request.FlowGroups
            .SelectMany(group => group.Traces)
            .SelectMany(trace => trace.Steps)
            .Where(step => step.DocumentVersionId != request.NewDocumentVersionId)
            .Select(step => (
                step.DocumentVersionId,
                step.DocumentRepositoryId,
                step.DocumentScanRunId))
            .Distinct()
            .ToArray();
        foreach (var (DocumentVersionId, DocumentRepositoryId, DocumentScanRunId) in flowEndpoints)
        {
            if (!scope.Contains(DocumentRepositoryId))
            {
                return KnowledgeCommitResult.Rejected(
                    KnowledgeRejectionCode.UnauthorizedEndpoint);
            }

            DocumentVersion? target = await _context.Set<DocumentVersion>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    version =>
                        version.Id == DocumentVersionId &&
                        version.RepositoryId == DocumentRepositoryId &&
                        version.ScanRunId == DocumentScanRunId,
                    cancellationToken);
            if (target is null)
            {
                return KnowledgeCommitResult.Rejected(KnowledgeRejectionCode.MissingEndpoint);
            }

            bool current = await _context.Set<LogicalDocument>()
                .AsNoTracking()
                .AnyAsync(
                    document =>
                        document.Id == target.LogicalDocumentId &&
                        document.RepositoryId == target.RepositoryId &&
                        document.CurrentVersionId == target.Id,
                    cancellationToken);
            if (!current)
            {
                return KnowledgeCommitResult.Rejected(KnowledgeRejectionCode.NonCurrentEndpoint);
            }
        }

        var endpoints = request.Edges
            .Select(edge => (
                edge.TargetDocumentVersionId,
                edge.TargetRepositoryId,
                edge.TargetScanRunId,
                edge.EdgeType))
            .ToArray();
        foreach (var (TargetDocumentVersionId, TargetRepositoryId, TargetScanRunId, EdgeType) in endpoints)
        {
            if (!scope.Contains(TargetRepositoryId))
            {
                return KnowledgeCommitResult.Rejected(
                    KnowledgeRejectionCode.UnauthorizedEndpoint);
            }

            DocumentVersion? target = await _context.Set<DocumentVersion>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    version =>
                        version.Id == TargetDocumentVersionId &&
                        version.RepositoryId == TargetRepositoryId &&
                        version.ScanRunId == TargetScanRunId,
                    cancellationToken);
            if (target is null)
            {
                return KnowledgeCommitResult.Rejected(KnowledgeRejectionCode.MissingEndpoint);
            }

            bool current = await _context.Set<LogicalDocument>()
                .AsNoTracking()
                .AnyAsync(
                    document =>
                        document.Id == target.LogicalDocumentId &&
                        document.RepositoryId == target.RepositoryId &&
                        document.CurrentVersionId == target.Id,
                    cancellationToken);
            if (!current)
            {
                return KnowledgeCommitResult.Rejected(KnowledgeRejectionCode.NonCurrentEndpoint);
            }

            if (EdgeType == DocumentEdgeType.GeneratedFrom)
            {
                DocumentType targetType = await _context.Set<LogicalDocument>()
                    .Where(document => document.Id == target.LogicalDocumentId)
                    .Select(document => document.DocumentType)
                    .SingleAsync(cancellationToken);
                if (targetType != DocumentType.CodeLogic)
                {
                    return KnowledgeCommitResult.Rejected(
                        KnowledgeRejectionCode.InvalidEdgeSemantic);
                }
            }
        }

        return null;
    }

    private void AddEvidence(KnowledgeCommit request)
    {
        _context.Set<SourceAnchor>().AddRange(request.SourceAnchors.Select(anchor =>
            new SourceAnchor(
                anchor.Id,
                anchor.DocumentVersionId,
                anchor.RepositoryId,
                anchor.ScanRunId,
                anchor.CodeUnitId,
                anchor.ProfiledRevision,
                anchor.RepositoryRelativePath,
                anchor.EvidenceLocator,
                anchor.LineStart,
                anchor.LineEnd,
                anchor.SourceHash,
                request.CommittedAt)));
        _context.Set<DocumentEdge>().AddRange(request.Edges.Select(edge =>
            new DocumentEdge(
                edge.Id,
                edge.SourceDocumentVersionId,
                edge.SourceRepositoryId,
                edge.SourceScanRunId,
                edge.TargetDocumentVersionId,
                edge.TargetRepositoryId,
                edge.TargetScanRunId,
                edge.EdgeType,
                request.CommittedAt)));
        foreach (FlowGroupDraft groupDraft in request.FlowGroups)
        {
            var group = new FlowGroup(
                groupDraft.Id,
                request.NewDocumentVersionId,
                request.RepositoryId,
                request.ScanRunId,
                groupDraft.GroupOrder,
                groupDraft.Title,
                request.CommittedAt);
            _context.Set<FlowGroup>().Add(group);
            foreach (FlowTraceDraft traceDraft in groupDraft.Traces)
            {
                var trace = new FlowTrace(
                    traceDraft.Id,
                    groupDraft.Id,
                    traceDraft.TraceOrder,
                    request.CommittedAt);
                _context.Set<FlowTrace>().Add(trace);
                _context.Set<FlowTraceStep>().AddRange(traceDraft.Steps.Select(step =>
                    new FlowTraceStep(
                        step.Id,
                        traceDraft.Id,
                        step.StepOrder,
                        step.DocumentVersionId,
                        step.DocumentRepositoryId,
                        step.DocumentScanRunId,
                        step.EdgeId,
                        step.StepKind,
                        step.Description,
                        request.CommittedAt)));
            }
        }
    }

    private async Task<KnowledgeCommitResult> RollbackAndRejectAsync(
        IDbContextTransaction transaction,
        KnowledgeRejectionCode code,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return RejectAndClear(code);
    }

    private KnowledgeCommitResult RejectAndClear(KnowledgeRejectionCode code)
    {
        _context.ChangeTracker.Clear();
        return KnowledgeCommitResult.Rejected(code);
    }

    private KnowledgeCommitResult ConflictAndClear()
    {
        _context.ChangeTracker.Clear();
        return KnowledgeCommitResult.Conflict();
    }
}
