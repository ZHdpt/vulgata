using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class Pass2CodeLogicRetrievalService : IPass2CodeLogicRetrievalService
{
    private const int _maximumQueryUtf8Bytes = 512;
    private readonly VulgataDbContext _context;
    private readonly RepositoryAccessScope _accessScope;
    private readonly IRevisionBoundSourceReader _sourceReader;

    public Pass2CodeLogicRetrievalService(
        VulgataDbContext context,
        RepositoryAccessScope accessScope,
        IRevisionBoundSourceReader sourceReader)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _accessScope = accessScope ?? throw new ArgumentNullException(nameof(accessScope));
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
    }

    public async Task<Pass2CodeLogicRetrievalResult> RetrieveAsync(
        Guid pass2JobExecutionId,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (pass2JobExecutionId == Guid.Empty)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        RetrievalAuthority? authority = await LoadAuthorityAsync(
            pass2JobExecutionId,
            cancellationToken).ConfigureAwait(false);
        if (authority is null ||
            !await _accessScope.HasCurrentAccessAsync(
                authority.RepositoryIds,
                cancellationToken).ConfigureAwait(false))
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        return request.Operation switch
        {
            Pass2CodeLogicRetrievalOperation.Search => await SearchAsync(
                authority,
                request,
                cancellationToken).ConfigureAwait(false),
            Pass2CodeLogicRetrievalOperation.Sections => await SectionsAsync(
                authority,
                request,
                cancellationToken).ConfigureAwait(false),
            Pass2CodeLogicRetrievalOperation.SourceAnchors => await SourceAnchorsAsync(
                authority,
                request,
                cancellationToken).ConfigureAwait(false),
            Pass2CodeLogicRetrievalOperation.SourceExcerpt => await SourceExcerptAsync(
                authority,
                request,
                cancellationToken).ConfigureAwait(false),
            Pass2CodeLogicRetrievalOperation.RelatedDocuments => await RelatedDocumentsAsync(
                authority,
                request,
                cancellationToken).ConfigureAwait(false),
            _ => Pass2CodeLogicRetrievalResult.Refused(),
        };
    }

    private async Task<Pass2CodeLogicRetrievalResult> SearchAsync(
        RetrievalAuthority authority,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        string? query = NormalizeQuery(request.QueryText);
        if (query is null || request.OpaqueHandles.Count != 0 ||
            request.Direction is not null || request.ContainedByteOffset is not null ||
            request.ContainedByteLength is not null)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        DocumentSection[] sections = await LoadSectionsAsync(authority, cancellationToken)
            .ConfigureAwait(false);
        IndexEntry[] indexes = await _context.IndexEntries.AsNoTracking()
            .Where(value => authority.DocumentVersionIds.Contains(value.DocumentVersionId))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (!await RevalidateAsync(authority, cancellationToken).ConfigureAwait(false))
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        IEnumerable<RankedItem> documents = indexes
            .Where(value =>
                value.SystemId == authority.SystemId &&
                value.DocumentType == DocumentType.CodeLogic &&
                (value.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    value.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    value.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    value.RepositoryRelativePath.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(value => value.DocumentVersionId)
            .Select(grouping => grouping.OrderBy(value => value.Id).First())
            .Select(value => new RankedItem(
                IndexMatchRank(value, query),
                value.DocumentVersionId,
                Guid.Empty,
                CreateDocumentItem(authority.JobId, value.DocumentVersionId)));
        IEnumerable<RankedItem> sectionItems = sections
            .Where(section =>
                section.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                section.Heading.Contains(query, StringComparison.OrdinalIgnoreCase))
            .GroupBy(section => section.Id)
            .Select(grouping => grouping.Single())
            .Select(section => new RankedItem(
                MatchRank(section, query),
                section.DocumentVersionId,
                section.Id,
                CreateSectionItem(authority.JobId, section, searchDescriptor: true)));
        Pass2CodeLogicRetrievalItem[] items = documents.Concat(sectionItems)
            .OrderBy(value => value.Rank)
            .ThenBy(value => value.DocumentVersionId.ToString("N"), StringComparer.Ordinal)
            .ThenBy(value => value.CoordinateId.ToString("N"), StringComparer.Ordinal)
            .Select(value => value.Item)
            .Take(Pass2RetrievalContracts.MaxResults)
            .ToArray();
        return items.Length == 0
            ? Pass2CodeLogicRetrievalResult.NoResults()
            : Pass2CodeLogicRetrievalResult.Accepted(items);
    }

    private async Task<Pass2CodeLogicRetrievalResult> SectionsAsync(
        RetrievalAuthority authority,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsHandleRequest(request) || request.OpaqueHandles.Count > Pass2RetrievalContracts.MaxResults)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        DocumentSection[] sections = await LoadSectionsAsync(authority, cancellationToken)
            .ConfigureAwait(false);
        var selected = new List<DocumentSection>();
        foreach (string handle in request.OpaqueHandles)
        {
            Guid? documentId = ResolveDocumentHandle(authority, handle);
            DocumentSection? section = sections.SingleOrDefault(value =>
                StringComparer.Ordinal.Equals(
                    CreateSectionHandle(authority.JobId, value),
                    handle));
            if (documentId is null && section is null)
            {
                return Pass2CodeLogicRetrievalResult.Refused();
            }

            if (section is not null)
            {
                selected.Add(section);
            }
            else
            {
                selected.AddRange(sections
                    .Where(value => value.DocumentVersionId == documentId)
                    .OrderBy(value => value.SectionOrder)
                    .ThenBy(value => value.Id)
                    .Take(Pass2RetrievalContracts.MaxSectionsPerDocument));
            }
        }

        if (!await RevalidateAsync(authority, cancellationToken).ConfigureAwait(false))
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        Pass2CodeLogicRetrievalItem[] items = selected
            .GroupBy(value => value.Id)
            .Select(grouping => grouping.Single())
            .OrderBy(value => value.DocumentVersionId)
            .ThenBy(value => value.SectionOrder)
            .ThenBy(value => value.Id)
            .GroupBy(value => value.DocumentVersionId)
            .SelectMany(grouping => grouping.Take(Pass2RetrievalContracts.MaxSectionsPerDocument))
            .Take(Pass2RetrievalContracts.MaxResults)
            .Select(value => CreateSectionItem(authority.JobId, value, searchDescriptor: false))
            .ToArray();
        return items.Length == 0
            ? Pass2CodeLogicRetrievalResult.NoResults()
            : Pass2CodeLogicRetrievalResult.Accepted(items);
    }

    private async Task<Pass2CodeLogicRetrievalResult> SourceAnchorsAsync(
        RetrievalAuthority authority,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsHandleRequest(request) || request.OpaqueHandles.Count > Pass2RetrievalContracts.MaxResults)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        DocumentSection[] sections = await LoadSectionsAsync(authority, cancellationToken)
            .ConfigureAwait(false);
        SourceAnchor[] anchors = await LoadAnchorsAsync(authority, cancellationToken)
            .ConfigureAwait(false);
        var selectedIds = new HashSet<Guid>();
        foreach (string handle in request.OpaqueHandles)
        {
            Guid? documentId = ResolveDocumentHandle(authority, handle);
            DocumentSection? section = sections.SingleOrDefault(value =>
                StringComparer.Ordinal.Equals(CreateSectionHandle(authority.JobId, value), handle));
            if (documentId is null && section is null)
            {
                return Pass2CodeLogicRetrievalResult.Refused();
            }

            if (section is not null)
            {
                Guid[] sectionAnchorIds = ReadGuidArray(section.SourceAnchorIdsJson);
                foreach (Guid id in sectionAnchorIds)
                {
                    if (anchors.Any(value =>
                            value.Id == id &&
                            value.DocumentVersionId == section.DocumentVersionId))
                    {
                        selectedIds.Add(id);
                    }
                }
            }
            else
            {
                foreach (Guid id in anchors
                    .Where(value => value.DocumentVersionId == documentId)
                    .Select(value => value.Id))
                {
                    selectedIds.Add(id);
                }
            }
        }

        if (!await RevalidateAsync(authority, cancellationToken).ConfigureAwait(false))
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        Pass2CodeLogicRetrievalItem[] items = anchors
            .Where(value => selectedIds.Contains(value.Id))
            .OrderBy(value => value.DocumentVersionId)
            .ThenBy(value => value.Id)
            .Take(Pass2RetrievalContracts.MaxResults)
            .Select(value => CreateAnchorItem(authority.JobId, value))
            .ToArray();
        return items.Length == 0
            ? Pass2CodeLogicRetrievalResult.NoResults()
            : Pass2CodeLogicRetrievalResult.Accepted(items);
    }

    private async Task<Pass2CodeLogicRetrievalResult> SourceExcerptAsync(
        RetrievalAuthority authority,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OpaqueHandles.Count != 1 || request.QueryText is not null ||
            request.Direction is not null || request.ContainedByteOffset is null ||
            request.ContainedByteLength is null || request.ContainedByteOffset < 0 ||
            request.ContainedByteLength <= 0 ||
            request.ContainedByteLength > Pass2RetrievalContracts.MaxReturnedUtf8Bytes)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        SourceAnchor[] anchors = await LoadAnchorsAsync(authority, cancellationToken)
            .ConfigureAwait(false);
        SourceAnchor? anchor = anchors.SingleOrDefault(value => StringComparer.Ordinal.Equals(
            CreateAnchorHandle(authority.JobId, value),
            request.OpaqueHandles[0]));
        if (anchor is null)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        long byteStart = request.ContainedByteOffset.Value;
        long byteEnd;
        try
        {
            byteEnd = checked(byteStart + request.ContainedByteLength.Value);
        }
        catch (OverflowException)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        if (anchor.ByteStart is not null && byteStart < anchor.ByteStart ||
            anchor.ByteEnd is not null && byteEnd > anchor.ByteEnd)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        PreparedScanManifest? manifest = await _context.PreparedScanManifests.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.CoordinationId == authority.CoordinationId &&
                value.RepositoryId == anchor.RepositoryId &&
                value.ScanRunId == anchor.ScanRunId &&
                value.ProfiledRevision == anchor.ProfiledRevision,
                cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        var scope = new AnalysisScope(
            anchor.RepositoryId,
            anchor.ScanRunId,
            anchor.ProfiledRevision,
            "pass2-retrieval",
            manifest.RuntimeIdentityHash,
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
        var sourceRequest = new RevisionBoundSourceRequest(
            anchor.RepositoryRelativePath,
            new AnalysisLineRange(anchor.LineStart, anchor.LineEnd),
            anchor.SourceHash,
            request.ContainedByteLength.Value,
            byteStart,
            byteEnd);
        SourceExcerptResult source = await _sourceReader.ReadAsync(
            scope,
            sourceRequest,
            cancellationToken).ConfigureAwait(false);

        SourceAnchor? currentAnchor = (await LoadAnchorsAsync(authority, cancellationToken)
            .ConfigureAwait(false)).SingleOrDefault(value => value.Id == anchor.Id);
        if (currentAnchor is null || !AnchorMatches(anchor, currentAnchor) ||
            !await RevalidateAsync(authority, cancellationToken).ConfigureAwait(false) ||
            source.Status != AnalysisOperationStatus.Succeeded || source.Failure is not null ||
            source.RepositoryId != anchor.RepositoryId ||
            !StringComparer.Ordinal.Equals(source.ProfiledRevision, anchor.ProfiledRevision) ||
            !StringComparer.Ordinal.Equals(source.RelativePath, anchor.RepositoryRelativePath) ||
            !StringComparer.Ordinal.Equals(source.SourceSha256, anchor.SourceHash) ||
            source.Range.StartLine != anchor.LineStart || source.Range.EndLine != anchor.LineEnd ||
            source.ByteCount != Encoding.UTF8.GetByteCount(source.Excerpt) ||
            source.ByteCount > request.ContainedByteLength.Value)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        return Pass2CodeLogicRetrievalResult.Accepted(
        [
            CreateExcerptItem(authority.JobId, anchor, byteStart, byteEnd, source.ByteCount),
        ]);
    }

    private async Task<Pass2CodeLogicRetrievalResult> RelatedDocumentsAsync(
        RetrievalAuthority authority,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OpaqueHandles.Count != 1 || request.QueryText is not null ||
            request.Direction is null || request.ContainedByteOffset is not null ||
            request.ContainedByteLength is not null)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        Guid? documentId = ResolveDocumentHandle(authority, request.OpaqueHandles[0]);
        if (documentId is null)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        DocumentEdge[] edges = await _context.DocumentEdges.AsNoTracking()
            .Where(value =>
                value.EdgeType == DocumentEdgeType.References &&
                authority.DocumentVersionIds.Contains(value.SourceDocumentVersionId) &&
                authority.DocumentVersionIds.Contains(value.TargetDocumentVersionId))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<(DocumentEdge Edge, Guid RelatedId)> related = request.Direction ==
            Pass2RelatedDocumentDirection.Outgoing
            ? edges.Where(value => value.SourceDocumentVersionId == documentId)
                .Select(value => (value, value.TargetDocumentVersionId))
            : edges.Where(value => value.TargetDocumentVersionId == documentId)
                .Select(value => (value, value.SourceDocumentVersionId));
        if (!await RevalidateAsync(authority, cancellationToken).ConfigureAwait(false))
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        Pass2CodeLogicRetrievalItem[] items = related
            .GroupBy(value => value.Edge.Id)
            .Select(grouping => grouping.Single())
            .OrderBy(value => value.RelatedId)
            .ThenBy(value => value.Edge.Id)
            .Take(Pass2RetrievalContracts.MaxResults)
            .Select(value => CreateRelationshipItem(
                authority.JobId,
                value.Edge,
                value.RelatedId))
            .ToArray();
        return items.Length == 0
            ? Pass2CodeLogicRetrievalResult.NoResults()
            : Pass2CodeLogicRetrievalResult.Accepted(items);
    }

    private async Task<RetrievalAuthority?> LoadAuthorityAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        Pass2JobExecution? job = await _context.Pass2JobExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == jobId, cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
        {
            return null;
        }

        Pass2Execution? execution = await _context.Pass2Executions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == job.Pass2ExecutionId, cancellationToken)
            .ConfigureAwait(false);
        FlowGroup? group = await _context.FlowGroups.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == job.FlowGroupId, cancellationToken)
            .ConfigureAwait(false);
        if (execution is null || group is null ||
            execution.State != Pass2ExecutionState.Running ||
            group.Pass2ExecutionId != execution.Id ||
            group.SystemId != execution.SystemId ||
            group.State != "Complete" ||
            group.ExactResolutionDecisionId is null ||
            group.ExactResolutionEdgeId is null)
        {
            return null;
        }

        Pass1PairExecution? pair = await _context.Pass1PairExecutions.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == execution.Pass1PairExecutionId,
                cancellationToken).ConfigureAwait(false);
        TwoRepositoryScanCoordination? coordination = await _context
            .TwoRepositoryScanCoordinations.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == execution.CoordinationId,
                cancellationToken).ConfigureAwait(false);
        if (pair is null || coordination is null ||
            pair.Phase != Pass1PairExecutionPhase.ReadyForResolution ||
            !pair.TerminalAt.HasValue ||
            pair.CoordinationId != coordination.Id ||
            pair.SystemId != execution.SystemId ||
            coordination.SystemId != execution.SystemId ||
            coordination.RepositoryOneId == coordination.RepositoryTwoId)
        {
            return null;
        }

        PreparedScanManifest[] manifests = await _context.PreparedScanManifests.AsNoTracking()
            .Where(value => value.Id == pair.ManifestOneId || value.Id == pair.ManifestTwoId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] repositoryIds =
        [
            coordination.RepositoryOneId,
            coordination.RepositoryTwoId,
        ];
        if (manifests.Length != 2 ||
            manifests.Select(value => value.Id).Distinct().Count() != 2 ||
            repositoryIds.Any(value => !_accessScope.Contains(value)) ||
            manifests.Any(value =>
                value.ApprovalState != PreparedScanManifestApprovalState.Approved ||
                value.CoordinationId != coordination.Id ||
                !ManifestMatches(value, coordination)))
        {
            return null;
        }

        Guid[] traceIds = await _context.FlowTraces.AsNoTracking()
            .Where(value => value.FlowGroupId == group.Id)
            .Select(value => value.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        FlowTraceStep[] steps = await _context.FlowTraceSteps.AsNoTracking()
            .Where(value => traceIds.Contains(value.FlowTraceId))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] documentVersionIds = steps
            .Where(value => value.DocumentVersionId != Guid.Empty)
            .Select(value => value.DocumentVersionId)
            .Distinct()
            .ToArray();
        Guid[] requiredCoreDocuments =
        [
            group.ConsumerDocumentVersionId ?? Guid.Empty,
            group.ProviderXmlDocumentVersionId ?? Guid.Empty,
            group.ProviderImplementationDocumentVersionId ?? Guid.Empty,
        ];
        if (traceIds.Length == 0 || documentVersionIds.Length == 0 ||
            requiredCoreDocuments.Any(value =>
                value == Guid.Empty || !documentVersionIds.Contains(value)))
        {
            return null;
        }

        DocumentVersion[] documents = await _context.DocumentVersions.AsNoTracking()
            .Where(value => documentVersionIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] logicalDocumentIds = documents.Select(value => value.LogicalDocumentId)
            .Distinct().ToArray();
        LogicalDocument[] logicalDocuments = await _context.LogicalDocuments.AsNoTracking()
            .Where(value => logicalDocumentIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        SourceAnchor[] anchors = await _context.SourceAnchors.AsNoTracking()
            .Where(value => documentVersionIds.Contains(value.DocumentVersionId))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (documents.Length != documentVersionIds.Length ||
            logicalDocuments.Length != logicalDocumentIds.Length ||
            documents.Any(document =>
                document.SystemId != execution.SystemId ||
                document.SupersededById is not null ||
                !DocumentMatches(document, coordination) ||
                !logicalDocuments.Any(logical =>
                    logical.Id == document.LogicalDocumentId &&
                    logical.RepositoryId == document.RepositoryId &&
                    logical.DocumentType == DocumentType.CodeLogic &&
                    logical.CurrentVersionId == document.Id) ||
                !anchors.Any(anchor =>
                    anchor.DocumentVersionId == document.Id &&
                    anchor.RepositoryId == document.RepositoryId &&
                    anchor.ScanRunId == document.ScanRunId &&
                    StringComparer.Ordinal.Equals(
                        anchor.ProfiledRevision,
                        document.ProfiledRevision))))
        {
            return null;
        }

        CrossRepositoryResolutionDecision? decision = await _context
            .CrossRepositoryResolutionDecisions.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == group.ExactResolutionDecisionId,
                cancellationToken).ConfigureAwait(false);
        DocumentEdge? exactEdge = await _context.DocumentEdges.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == group.ExactResolutionEdgeId,
                cancellationToken).ConfigureAwait(false);
        if (decision is null || exactEdge is null ||
            decision.Pass1PairExecutionId != pair.Id ||
            decision.CoordinationId != coordination.Id ||
            decision.SystemId != execution.SystemId ||
            decision.Outcome != "ExactMatch" ||
            decision.ConsumerDocumentVersionId != group.ConsumerDocumentVersionId ||
            decision.ProviderDocumentVersionId != group.ProviderXmlDocumentVersionId ||
            decision.DocumentEdgeId != exactEdge.Id ||
            exactEdge.EdgeType != DocumentEdgeType.References ||
            exactEdge.SourceDocumentVersionId != group.ConsumerDocumentVersionId ||
            exactEdge.TargetDocumentVersionId != group.ProviderXmlDocumentVersionId)
        {
            return null;
        }

        Array.Sort(repositoryIds);
        Array.Sort(documentVersionIds);
        return new(
            job.Id,
            execution.Id,
            group.Id,
            pair.Id,
            coordination.Id,
            execution.SystemId,
            Array.AsReadOnly(repositoryIds),
            Array.AsReadOnly(documentVersionIds));
    }

    private static bool ManifestMatches(
        PreparedScanManifest manifest,
        TwoRepositoryScanCoordination coordination) =>
        manifest.RepositoryId == coordination.RepositoryOneId &&
            manifest.ScanRunId == coordination.ScanRunOneId &&
            StringComparer.Ordinal.Equals(
                manifest.ProfiledRevision,
                coordination.ProfiledRevisionOne) ||
        manifest.RepositoryId == coordination.RepositoryTwoId &&
            manifest.ScanRunId == coordination.ScanRunTwoId &&
            StringComparer.Ordinal.Equals(
                manifest.ProfiledRevision,
                coordination.ProfiledRevisionTwo);

    private static bool DocumentMatches(
        DocumentVersion document,
        TwoRepositoryScanCoordination coordination) =>
        document.RepositoryId == coordination.RepositoryOneId &&
            document.ScanRunId == coordination.ScanRunOneId &&
            StringComparer.Ordinal.Equals(
                document.ProfiledRevision,
                coordination.ProfiledRevisionOne) ||
        document.RepositoryId == coordination.RepositoryTwoId &&
            document.ScanRunId == coordination.ScanRunTwoId &&
            StringComparer.Ordinal.Equals(
                document.ProfiledRevision,
                coordination.ProfiledRevisionTwo);

    private static string? NormalizeQuery(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim().Normalize(NormalizationForm.FormC);
        return Encoding.UTF8.GetByteCount(normalized) <= _maximumQueryUtf8Bytes &&
            !normalized.Any(character => char.IsControl(character))
            ? normalized
            : null;
    }

    private static int MatchRank(DocumentSection section, string query) =>
        StringComparer.OrdinalIgnoreCase.Equals(section.SearchText, query) ||
            StringComparer.OrdinalIgnoreCase.Equals(section.Heading, query)
            ? 0
            : 1;

    private static int IndexMatchRank(IndexEntry entry, string query) =>
        StringComparer.OrdinalIgnoreCase.Equals(entry.SearchText, query) ||
            StringComparer.OrdinalIgnoreCase.Equals(entry.Title, query) ||
            StringComparer.OrdinalIgnoreCase.Equals(entry.RepositoryRelativePath, query)
            ? 0
            : 1;

    private async Task<DocumentSection[]> LoadSectionsAsync(
        RetrievalAuthority authority,
        CancellationToken cancellationToken) =>
        await _context.DocumentSections.AsNoTracking()
            .Where(value => authority.DocumentVersionIds.Contains(value.DocumentVersionId))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

    private async Task<SourceAnchor[]> LoadAnchorsAsync(
        RetrievalAuthority authority,
        CancellationToken cancellationToken) =>
        await _context.SourceAnchors.AsNoTracking()
            .Where(value => authority.DocumentVersionIds.Contains(value.DocumentVersionId))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

    private async Task<bool> RevalidateAsync(
        RetrievalAuthority authority,
        CancellationToken cancellationToken)
    {
        RetrievalAuthority? current = await LoadAuthorityAsync(
            authority.JobId,
            cancellationToken).ConfigureAwait(false);
        return current is not null && AuthorityMatches(authority, current) &&
            await _accessScope.HasCurrentAccessAsync(
                current.RepositoryIds,
                cancellationToken).ConfigureAwait(false);
    }

    private static bool IsHandleRequest(Pass2CodeLogicRetrievalRequest request) =>
        request.OpaqueHandles.Count > 0 &&
        request.OpaqueHandles.All(value => !string.IsNullOrWhiteSpace(value)) &&
        request.QueryText is null && request.Direction is null &&
        request.ContainedByteOffset is null && request.ContainedByteLength is null;

    private static Guid? ResolveDocumentHandle(
        RetrievalAuthority authority,
        string handle)
    {
        foreach (Guid documentVersionId in authority.DocumentVersionIds)
        {
            if (StringComparer.Ordinal.Equals(
                CreateDocumentHandle(authority.JobId, documentVersionId),
                handle))
            {
                return documentVersionId;
            }
        }

        return null;
    }

    private static Guid[] ReadGuidArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Guid[]>(json) ?? Array.Empty<Guid>();
        }
        catch (JsonException)
        {
            return Array.Empty<Guid>();
        }
    }

    private static bool AnchorMatches(SourceAnchor expected, SourceAnchor current) =>
        expected.Id == current.Id &&
        expected.DocumentVersionId == current.DocumentVersionId &&
        expected.RepositoryId == current.RepositoryId &&
        expected.ScanRunId == current.ScanRunId &&
        expected.CodeUnitId == current.CodeUnitId &&
        StringComparer.Ordinal.Equals(expected.ProfiledRevision, current.ProfiledRevision) &&
        StringComparer.Ordinal.Equals(
            expected.RepositoryRelativePath,
            current.RepositoryRelativePath) &&
        StringComparer.Ordinal.Equals(expected.EvidenceLocator, current.EvidenceLocator) &&
        expected.LineStart == current.LineStart && expected.LineEnd == current.LineEnd &&
        StringComparer.Ordinal.Equals(expected.SourceHash, current.SourceHash) &&
        expected.ByteStart == current.ByteStart && expected.ByteEnd == current.ByteEnd;

    private static bool AuthorityMatches(
        RetrievalAuthority expected,
        RetrievalAuthority current) =>
        expected.JobId == current.JobId &&
        expected.ExecutionId == current.ExecutionId &&
        expected.FlowGroupId == current.FlowGroupId &&
        expected.PairId == current.PairId &&
        expected.CoordinationId == current.CoordinationId &&
        expected.SystemId == current.SystemId &&
        expected.RepositoryIds.SequenceEqual(current.RepositoryIds) &&
        expected.DocumentVersionIds.SequenceEqual(current.DocumentVersionIds);

    private static string CreateOpaqueHandle(
        Guid jobId,
        Pass2CodeLogicRetrievalItemKind kind,
        Guid documentVersionId,
        Guid? coordinateId = null,
        long? byteStart = null,
        long? byteEnd = null)
    {
        byte[] value = Encoding.UTF8.GetBytes(string.Join(
            '\n',
            "pass2-retrieval-handle/1.0",
            jobId.ToString("D"),
            kind.ToString(),
            documentVersionId.ToString("D"),
            coordinateId?.ToString("D") ?? string.Empty,
            byteStart?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            byteEnd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));
        return Convert.ToHexStringLower(SHA256.HashData(value));
    }

    private static string CreateDocumentHandle(Guid jobId, Guid documentVersionId) =>
        CreateOpaqueHandle(jobId, Pass2CodeLogicRetrievalItemKind.Document, documentVersionId);

    private static string CreateSectionHandle(Guid jobId, DocumentSection section) =>
        CreateOpaqueHandle(
            jobId,
            Pass2CodeLogicRetrievalItemKind.Section,
            section.DocumentVersionId,
            section.Id);

    private static string CreateAnchorHandle(Guid jobId, SourceAnchor anchor) =>
        CreateOpaqueHandle(
            jobId,
            Pass2CodeLogicRetrievalItemKind.SourceAnchor,
            anchor.DocumentVersionId,
            anchor.Id);

    private static Pass2CodeLogicRetrievalItem CreateDocumentItem(
        Guid jobId,
        Guid documentVersionId)
    {
        string handle = CreateDocumentHandle(jobId, documentVersionId);
        return new(
            handle,
            Pass2CodeLogicRetrievalItemKind.Document,
            documentVersionId,
            DescriptorBytes(handle, "Document", documentVersionId));
    }

    private static Pass2CodeLogicRetrievalItem CreateSectionItem(
        Guid jobId,
        DocumentSection section,
        bool searchDescriptor)
    {
        string handle = CreateSectionHandle(jobId, section);
        const Pass2CodeLogicRetrievalItemKind kind =
            Pass2CodeLogicRetrievalItemKind.Section;
        int returnedUtf8Bytes = searchDescriptor
            ? DescriptorBytes(handle, kind.ToString(), section.DocumentVersionId)
            : Encoding.UTF8.GetByteCount(section.ContentMarkdown);
        return new(
            handle,
            kind,
            section.DocumentVersionId,
            returnedUtf8Bytes,
            sectionId: section.Id);
    }

    private static Pass2CodeLogicRetrievalItem CreateAnchorItem(
        Guid jobId,
        SourceAnchor anchor)
    {
        string handle = CreateAnchorHandle(jobId, anchor);
        int bytes = Encoding.UTF8.GetByteCount(string.Join(
            '\n',
            anchor.RepositoryRelativePath,
            anchor.LineStart,
            anchor.LineEnd,
            anchor.SourceHash));
        return new(
            handle,
            Pass2CodeLogicRetrievalItemKind.SourceAnchor,
            anchor.DocumentVersionId,
            bytes,
            sourceAnchorId: anchor.Id);
    }

    private static Pass2CodeLogicRetrievalItem CreateExcerptItem(
        Guid jobId,
        SourceAnchor anchor,
        long byteStart,
        long byteEnd,
        int returnedUtf8Bytes)
    {
        string handle = CreateOpaqueHandle(
            jobId,
            Pass2CodeLogicRetrievalItemKind.SourceExcerpt,
            anchor.DocumentVersionId,
            anchor.Id,
            byteStart,
            byteEnd);
        return new(
            handle,
            Pass2CodeLogicRetrievalItemKind.SourceExcerpt,
            anchor.DocumentVersionId,
            returnedUtf8Bytes,
            sourceAnchorId: anchor.Id,
            byteStart: byteStart,
            byteEnd: byteEnd);
    }

    private static Pass2CodeLogicRetrievalItem CreateRelationshipItem(
        Guid jobId,
        DocumentEdge edge,
        Guid relatedDocumentVersionId)
    {
        string handle = CreateOpaqueHandle(
            jobId,
            Pass2CodeLogicRetrievalItemKind.Relationship,
            relatedDocumentVersionId,
            edge.Id);
        return new(
            handle,
            Pass2CodeLogicRetrievalItemKind.Relationship,
            relatedDocumentVersionId,
            DescriptorBytes(handle, "Relationship", relatedDocumentVersionId),
            edgeId: edge.Id);
    }

    private static int DescriptorBytes(string handle, string kind, Guid documentVersionId) =>
        Encoding.UTF8.GetByteCount(string.Join(
            '\n',
            handle,
            kind,
            documentVersionId.ToString("D")));

    private sealed record RankedItem(
        int Rank,
        Guid DocumentVersionId,
        Guid CoordinateId,
        Pass2CodeLogicRetrievalItem Item);

    private sealed record RetrievalAuthority(
        Guid JobId,
        Guid ExecutionId,
        Guid FlowGroupId,
        Guid PairId,
        Guid CoordinationId,
        Guid SystemId,
        IReadOnlyList<Guid> RepositoryIds,
        IReadOnlyList<Guid> DocumentVersionIds);
}
