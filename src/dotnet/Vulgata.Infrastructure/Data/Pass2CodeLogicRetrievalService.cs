using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

internal sealed class Pass2RetrievalCallIdentity
{
    internal Pass2RetrievalCallIdentity(
        int attemptOrdinal,
        int callOrdinal,
        string toolCallIdFingerprint)
    {
        if (attemptOrdinal is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptOrdinal));
        }

        if (callOrdinal is < 1 or > Pass2RetrievalContracts.MaxCalls)
        {
            throw new ArgumentOutOfRangeException(nameof(callOrdinal));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(toolCallIdFingerprint);
        if (toolCallIdFingerprint.Length != 64 ||
            toolCallIdFingerprint.Any(value =>
                !char.IsAsciiHexDigit(value) || char.IsAsciiLetterUpper(value)))
        {
            throw new ArgumentException(
                "The Tool Call identity fingerprint must be lowercase SHA-256.",
                nameof(toolCallIdFingerprint));
        }

        AttemptOrdinal = attemptOrdinal;
        CallOrdinal = callOrdinal;
        ToolCallIdFingerprint = toolCallIdFingerprint;
    }

    internal int AttemptOrdinal { get; }
    internal int CallOrdinal { get; }
    internal string ToolCallIdFingerprint { get; }
}

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
        if (pass2JobExecutionId == Guid.Empty || !_accessScope.HasLiveAuthorizationVerifier)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        RetrievalAuthority? authority = await LoadAuthorityAsync(
            pass2JobExecutionId,
            cancellationToken).ConfigureAwait(false);
        if (authority is null ||
            !await _accessScope.HasCurrentAccessAsync(
                authority.RepositoryIds,
                cancellationToken).ConfigureAwait(false) ||
            !HasValidRequestShape(request))
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        bool hasDurableBudget = _context.Database.IsRelational();
        var store = new PreparedPass2Store(_context);
        Pass2RetrievalReservation? reservation = hasDurableBudget
            ? await store.ReserveRetrievalAsync(
                pass2JobExecutionId,
                request,
                cancellationToken).ConfigureAwait(false)
            : null;
        if (reservation is { IsReserved: false })
        {
            return Pass2CodeLogicRetrievalResult.BoundExceeded(reservation.RemainingBudget);
        }

        Pass2CodeLogicRetrievalResult candidate =
            await HasValidSelectorsAsync(authority, request, cancellationToken)
                .ConfigureAwait(false)
                ? await ExecuteAsync(authority, request, cancellationToken).ConfigureAwait(false)
                : Pass2CodeLogicRetrievalResult.Refused();
        if (reservation is not null)
        {
            reservation = reservation with
            {
                AdmissionAuthorityCheck = token =>
                    RevalidateAdmissionAsync(authority, candidate, token),
            };
        }
        return reservation is null
            ? candidate
            : await store.AdmitRetrievalAsync(
                pass2JobExecutionId,
                reservation,
                request,
                candidate,
                authority.InitialDocumentVersionIds,
                cancellationToken).ConfigureAwait(false);
    }

    internal async Task<Pass2CodeLogicRetrievalResult> RetrieveAsync(
        Guid pass2JobExecutionId,
        Pass2RetrievalCallIdentity callIdentity,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callIdentity);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_accessScope.HasLiveAuthorizationVerifier)
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }
        RetrievalAuthority? authority = await LoadAuthorityAsync(
            pass2JobExecutionId,
            cancellationToken).ConfigureAwait(false);
        if (authority is null ||
            !await _accessScope.HasCurrentAccessAsync(
                authority.RepositoryIds,
                cancellationToken).ConfigureAwait(false) ||
            !HasValidRequestShape(request))
        {
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        var store = new PreparedPass2Store(_context);
        Pass2ExplicitRetrievalResolution resolution = await store.ResolveExplicitRetrievalAsync(
            pass2JobExecutionId,
            callIdentity,
            request,
            cancellationToken).ConfigureAwait(false);
        if (resolution.CompletedResult is not null)
        {
            if (resolution.CompletedResult.Outcome != Pass2CodeLogicRetrievalOutcome.Accepted)
            {
                return resolution.CompletedResult;
            }

            Pass2CodeLogicRetrievalResult replayCandidate =
                await HasValidSelectorsAsync(authority, request, cancellationToken)
                    .ConfigureAwait(false)
                    ? await ExecuteAsync(authority, request, cancellationToken).ConfigureAwait(false)
                    : Pass2CodeLogicRetrievalResult.Refused();
            return ReplayMatches(resolution.CompletedResult, replayCandidate)
                ? replayCandidate.WithRemainingBudget(resolution.CompletedResult.RemainingBudget)
                : Pass2CodeLogicRetrievalResult.Refused();
        }

        Pass2RetrievalReservation reservation = resolution.Reservation ??
            throw new InvalidOperationException(
                "An executable explicit retrieval call requires a durable reservation.");
        Pass2CodeLogicRetrievalResult candidate =
            await HasValidSelectorsAsync(authority, request, cancellationToken)
                .ConfigureAwait(false)
                ? await ExecuteAsync(authority, request, cancellationToken).ConfigureAwait(false)
                : Pass2CodeLogicRetrievalResult.Refused();
        reservation = reservation with
        {
            AdmissionAuthorityCheck = token =>
                RevalidateAdmissionAsync(authority, candidate, token),
        };
        return await store.AdmitRetrievalAsync(
            pass2JobExecutionId,
            reservation,
            request,
            candidate,
            authority.InitialDocumentVersionIds,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool ReplayMatches(
        Pass2CodeLogicRetrievalResult persisted,
        Pass2CodeLogicRetrievalResult candidate) =>
        candidate.Outcome == Pass2CodeLogicRetrievalOutcome.Accepted &&
        persisted.Items.Count == candidate.Items.Count &&
        persisted.Items.Zip(candidate.Items).All(pair =>
            pair.First.OpaqueHandle == pair.Second.OpaqueHandle &&
            pair.First.Kind == pair.Second.Kind &&
            pair.First.DocumentVersionId == pair.Second.DocumentVersionId &&
            pair.First.ReturnedUtf8Bytes == pair.Second.ReturnedUtf8Bytes &&
            pair.First.SectionId == pair.Second.SectionId &&
            pair.First.SourceAnchorId == pair.Second.SourceAnchorId &&
            pair.First.EdgeId == pair.Second.EdgeId &&
            pair.First.ByteStart == pair.Second.ByteStart &&
            pair.First.ByteEnd == pair.Second.ByteEnd);

    private async Task<Pass2CodeLogicRetrievalResult> ExecuteAsync(
        RetrievalAuthority authority,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken) => request.Operation switch
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
                CreateDocumentItem(authority, value.DocumentVersionId)));
        IEnumerable<RankedItem> sectionItems = sections
            .Where(section =>
                section.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                section.Heading.Contains(query, StringComparison.OrdinalIgnoreCase))
            .GroupBy(section => section.Id)
            .Select(grouping => grouping.First())
            .Select(section => new RankedItem(
                MatchRank(section, query),
                section.DocumentVersionId,
                section.Id,
                CreateSectionItem(authority, section, searchDescriptor: true)));
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
                    CreateSectionHandle(authority, value, searchDescriptor: true), handle) ||
                StringComparer.Ordinal.Equals(
                    CreateSectionHandle(authority, value, searchDescriptor: false), handle));
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
            .Select(grouping => grouping.First())
            .OrderBy(value => value.DocumentVersionId)
            .ThenBy(value => value.SectionOrder)
            .ThenBy(value => value.Id)
            .GroupBy(value => value.DocumentVersionId)
            .SelectMany(grouping => grouping.Take(Pass2RetrievalContracts.MaxSectionsPerDocument))
            .Take(Pass2RetrievalContracts.MaxResults)
            .Select(value => CreateSectionItem(authority, value, searchDescriptor: false))
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
                StringComparer.Ordinal.Equals(
                    CreateSectionHandle(authority, value, searchDescriptor: true), handle) ||
                StringComparer.Ordinal.Equals(
                    CreateSectionHandle(authority, value, searchDescriptor: false), handle));
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
            .Select(value => CreateAnchorItem(authority, value))
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
            CreateAnchorHandle(authority, value),
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
            CreateExcerptItem(authority, anchor, byteStart, byteEnd, source),
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
                authority,
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
        DateTimeOffset authorityNow = DateTimeOffset.UtcNow;
        if (execution is null || group is null ||
            job.State != Pass2JobExecutionState.Running ||
            job.AttemptCount is < 1 or > 2 ||
            job.LeaseToken is null || job.LeaseExpiresAt is null ||
            job.LeaseExpiresAt <= authorityNow ||
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
        Guid[] initialDocumentVersionIds = steps
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
        if (traceIds.Length == 0 || initialDocumentVersionIds.Length == 0 ||
            requiredCoreDocuments.Any(value =>
                value == Guid.Empty || !initialDocumentVersionIds.Contains(value)))
        {
            return null;
        }

        DocumentVersion[] documents = await _context.DocumentVersions.AsNoTracking()
            .Where(value =>
                value.SystemId == execution.SystemId &&
                value.SupersededById == null &&
                (value.RepositoryId == coordination.RepositoryOneId &&
                    value.ScanRunId == coordination.ScanRunOneId &&
                    value.ProfiledRevision == coordination.ProfiledRevisionOne ||
                 value.RepositoryId == coordination.RepositoryTwoId &&
                    value.ScanRunId == coordination.ScanRunTwoId &&
                    value.ProfiledRevision == coordination.ProfiledRevisionTwo))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] logicalDocumentIds = documents.Select(value => value.LogicalDocumentId)
            .Distinct().ToArray();
        LogicalDocument[] logicalDocuments = await _context.LogicalDocuments.AsNoTracking()
            .Where(value => logicalDocumentIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        SourceAnchor[] anchors = await _context.SourceAnchors.AsNoTracking()
            .Where(value => documents.Select(document => document.Id)
                .Contains(value.DocumentVersionId))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] documentVersionIds = documents
            .Where(document =>
                logicalDocuments.Any(logical =>
                    logical.Id == document.LogicalDocumentId &&
                    logical.RepositoryId == document.RepositoryId &&
                    logical.DocumentType == DocumentType.CodeLogic &&
                    logical.CurrentVersionId == document.Id) &&
                anchors.Any(anchor =>
                    anchor.DocumentVersionId == document.Id &&
                    anchor.RepositoryId == document.RepositoryId &&
                    anchor.ScanRunId == document.ScanRunId &&
                    StringComparer.Ordinal.Equals(
                        anchor.ProfiledRevision,
                        document.ProfiledRevision)))
            .Select(document => document.Id)
            .ToArray();
        if (logicalDocuments.Length != logicalDocumentIds.Length ||
            initialDocumentVersionIds.Any(value => !documentVersionIds.Contains(value)))
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
            exactEdge.TargetDocumentVersionId != group.ProviderXmlDocumentVersionId ||
            group.ProviderRepositoryId is null ||
            !repositoryIds.Contains(group.ProviderRepositoryId.Value))
        {
            return null;
        }

        string authorityFingerprint = LowerSha256(JsonSerializer.Serialize(new
        {
            execution = new
            {
                execution.Id,
                execution.Pass1PairExecutionId,
                execution.CoordinationId,
                execution.SystemId,
                execution.State,
                execution.StateVersion,
            },
            group = new
            {
                group.Id,
                group.Pass2ExecutionId,
                group.SystemId,
                group.Protocol,
                group.NormalizedEndpointIdentifier,
                group.ConsumerEntryIdentity,
                group.ConsumerEntryStableKey,
                group.LogicalKeySchemaIdentity,
                group.ConsumerDocumentVersionId,
                group.ProviderRepositoryId,
                group.ProviderXmlDocumentVersionId,
                group.ProviderImplementationDocumentVersionId,
                group.ExactResolutionDecisionId,
                group.ExactResolutionEdgeId,
                group.State,
                group.PlanFingerprint,
                group.MembershipFingerprint,
                group.RepositoryId,
                group.ScanRunId,
                group.GroupOrder,
            },
            pair = new
            {
                pair.Id,
                pair.CoordinationId,
                pair.SystemId,
                pair.PairPackageFingerprint,
                pair.PairSelectionFingerprint,
                pair.ManifestOneId,
                pair.ManifestTwoId,
                pair.Pass1PairFingerprint,
                pair.Phase,
                pair.StateVersion,
                pair.TerminalAt,
            },
            coordination = new
            {
                coordination.Id,
                coordination.SystemId,
                coordination.RepositoryOneId,
                coordination.RepositoryTwoId,
                coordination.ScanRunOneId,
                coordination.ScanRunTwoId,
                coordination.ProfiledRevisionOne,
                coordination.ProfiledRevisionTwo,
                coordination.Phase,
                coordination.StateVersion,
                coordination.TerminalAt,
            },
            manifests = manifests.OrderBy(value => value.Id).Select(value => new
            {
                value.Id,
                value.RepositoryId,
                value.ScanRunId,
                value.ProfiledRevision,
                value.ApprovalState,
                value.RuntimeIdentityHash,
            }),
            decision = new
            {
                decision.Id,
                decision.Pass1PairExecutionId,
                decision.CoordinationId,
                decision.SystemId,
                decision.ConsumerCandidateId,
                decision.ConsumerEntryIdentity,
                decision.ConsumerEntryStableKey,
                decision.ConsumerDocumentVersionId,
                decision.Protocol,
                decision.NormalizedEndpointIdentifier,
                decision.Outcome,
                decision.ProviderDocumentVersionId,
                decision.DocumentEdgeId,
                decision.EvidenceFingerprint,
            },
            edge = new
            {
                exactEdge.Id,
                exactEdge.SourceDocumentVersionId,
                exactEdge.SourceRepositoryId,
                exactEdge.SourceScanRunId,
                exactEdge.TargetDocumentVersionId,
                exactEdge.TargetRepositoryId,
                exactEdge.TargetScanRunId,
                exactEdge.EdgeType,
            },
            documents = documents.OrderBy(value => value.Id).Select(value => new
            {
                value.Id,
                value.LogicalDocumentId,
                value.RepositoryId,
                value.ScanRunId,
                value.SystemId,
                value.ProfiledRevision,
                value.ContentFingerprint,
                value.SupersededById,
            }),
            anchors = anchors.OrderBy(value => value.Id).Select(value => new
            {
                value.Id,
                value.DocumentVersionId,
                value.RepositoryId,
                value.ScanRunId,
                value.ProfiledRevision,
                value.SourceHash,
                value.ByteStart,
                value.ByteEnd,
            }),
        }));

        Array.Sort(repositoryIds);
        Array.Sort(initialDocumentVersionIds);
        Array.Sort(documentVersionIds);
        AuthorizedDocumentBinding[] documentBindings = documents
            .Where(value => documentVersionIds.Contains(value.Id))
            .OrderBy(value => value.Id)
            .Select(value => new AuthorizedDocumentBinding(
                value.Id,
                value.RepositoryId,
                value.ScanRunId,
                value.ProfiledRevision,
                value.ContentFingerprint))
            .ToArray();
        return new(
            job.Id,
            execution.Id,
            group.Id,
            pair.Id,
            coordination.Id,
            execution.SystemId,
            job.AttemptCount,
            job.StateVersion,
            job.LeaseToken.Value,
            job.LeaseExpiresAt.Value,
            group.ProviderRepositoryId.Value,
            authorityFingerprint,
            Array.AsReadOnly(repositoryIds),
            Array.AsReadOnly(initialDocumentVersionIds),
            Array.AsReadOnly(documentVersionIds),
            Array.AsReadOnly(documentBindings));
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

    private async Task<bool> RevalidateAdmissionAsync(
        RetrievalAuthority authority,
        Pass2CodeLogicRetrievalResult candidate,
        CancellationToken cancellationToken)
    {
        if (!await RevalidateAsync(authority, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        foreach (Pass2CodeLogicRetrievalItem item in candidate.Items)
        {
            bool matches = item.Kind switch
            {
                Pass2CodeLogicRetrievalItemKind.Document =>
                    authority.DocumentVersionIds.Contains(item.DocumentVersionId) &&
                    DescriptorMatches(item, CreateDocumentItem(authority, item.DocumentVersionId)),
                Pass2CodeLogicRetrievalItemKind.Section =>
                    await CurrentSectionMatchesAsync(authority, item, cancellationToken)
                        .ConfigureAwait(false),
                Pass2CodeLogicRetrievalItemKind.SourceAnchor =>
                    await CurrentAnchorMatchesAsync(authority, item, cancellationToken)
                        .ConfigureAwait(false),
                Pass2CodeLogicRetrievalItemKind.SourceExcerpt =>
                    await CurrentExcerptMatchesAsync(authority, item, cancellationToken)
                        .ConfigureAwait(false),
                Pass2CodeLogicRetrievalItemKind.Relationship =>
                    await CurrentRelationshipMatchesAsync(authority, item, cancellationToken)
                        .ConfigureAwait(false),
                _ => false,
            };
            if (!matches)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> CurrentSectionMatchesAsync(
        RetrievalAuthority authority,
        Pass2CodeLogicRetrievalItem item,
        CancellationToken cancellationToken)
    {
        if (item.SectionId is null)
        {
            return false;
        }

        DocumentSection? section = await _context.DocumentSections.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == item.SectionId, cancellationToken)
            .ConfigureAwait(false);
        return section is not null &&
            authority.DocumentVersionIds.Contains(section.DocumentVersionId) &&
            DescriptorMatches(
                item,
                CreateSectionItem(
                    authority,
                    section,
                    searchDescriptor: item.ContentMarkdown is null));
    }

    private async Task<bool> CurrentAnchorMatchesAsync(
        RetrievalAuthority authority,
        Pass2CodeLogicRetrievalItem item,
        CancellationToken cancellationToken)
    {
        if (item.SourceAnchorId is null)
        {
            return false;
        }

        SourceAnchor? anchor = await _context.SourceAnchors.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == item.SourceAnchorId, cancellationToken)
            .ConfigureAwait(false);
        return anchor is not null &&
            authority.DocumentVersionIds.Contains(anchor.DocumentVersionId) &&
            DescriptorMatches(item, CreateAnchorItem(authority, anchor));
    }

    private async Task<bool> CurrentExcerptMatchesAsync(
        RetrievalAuthority authority,
        Pass2CodeLogicRetrievalItem item,
        CancellationToken cancellationToken)
    {
        if (item.SourceAnchorId is null || item.ByteStart is null || item.ByteEnd is null ||
            item.Excerpt is null)
        {
            return false;
        }

        SourceAnchor? anchor = await _context.SourceAnchors.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == item.SourceAnchorId, cancellationToken)
            .ConfigureAwait(false);
        if (anchor is null || anchor.DocumentVersionId != item.DocumentVersionId ||
            !authority.DocumentVersionIds.Contains(anchor.DocumentVersionId) ||
            anchor.ByteStart is not null && item.ByteStart < anchor.ByteStart ||
            anchor.ByteEnd is not null && item.ByteEnd > anchor.ByteEnd)
        {
            return false;
        }

        int returnedUtf8Bytes = CanonicalProviderVisibleUtf8Bytes(
            new string('0', 64),
            Pass2CodeLogicRetrievalItemKind.SourceExcerpt,
            anchor.DocumentVersionId,
            sourceAnchorId: anchor.Id,
            byteStart: item.ByteStart,
            byteEnd: item.ByteEnd,
            excerpt: item.Excerpt);
        string handle = CreateOpaqueHandle(
            authority,
            Pass2CodeLogicRetrievalItemKind.SourceExcerpt,
            anchor.DocumentVersionId,
            LowerSha256(string.Join(
                '\n',
                AnchorEvidenceFingerprint(anchor),
                item.ByteStart,
                item.ByteEnd,
                anchor.SourceHash)),
            returnedUtf8Bytes,
            anchor.Id,
            item.ByteStart,
            item.ByteEnd);
        return item.ReturnedUtf8Bytes == returnedUtf8Bytes &&
            StringComparer.Ordinal.Equals(item.OpaqueHandle, handle);
    }

    private async Task<bool> CurrentRelationshipMatchesAsync(
        RetrievalAuthority authority,
        Pass2CodeLogicRetrievalItem item,
        CancellationToken cancellationToken)
    {
        if (item.EdgeId is null)
        {
            return false;
        }

        DocumentEdge? edge = await _context.DocumentEdges.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == item.EdgeId, cancellationToken)
            .ConfigureAwait(false);
        return edge is not null && edge.EdgeType == DocumentEdgeType.References &&
            authority.DocumentVersionIds.Contains(edge.SourceDocumentVersionId) &&
            authority.DocumentVersionIds.Contains(edge.TargetDocumentVersionId) &&
            (item.DocumentVersionId == edge.SourceDocumentVersionId ||
                item.DocumentVersionId == edge.TargetDocumentVersionId) &&
            DescriptorMatches(item, CreateRelationshipItem(authority, edge, item.DocumentVersionId));
    }

    private static bool DescriptorMatches(
        Pass2CodeLogicRetrievalItem expected,
        Pass2CodeLogicRetrievalItem current) =>
        expected.OpaqueHandle == current.OpaqueHandle &&
        expected.Kind == current.Kind &&
        expected.DocumentVersionId == current.DocumentVersionId &&
        expected.ReturnedUtf8Bytes == current.ReturnedUtf8Bytes &&
        expected.SectionId == current.SectionId &&
        expected.SourceAnchorId == current.SourceAnchorId &&
        expected.EdgeId == current.EdgeId &&
        expected.ByteStart == current.ByteStart &&
        expected.ByteEnd == current.ByteEnd &&
        expected.ContentMarkdown == current.ContentMarkdown &&
        expected.Excerpt == current.Excerpt;

    private static bool IsHandleRequest(Pass2CodeLogicRetrievalRequest request) =>
        request.OpaqueHandles.Count > 0 &&
        request.OpaqueHandles.All(value => !string.IsNullOrWhiteSpace(value)) &&
        request.QueryText is null && request.Direction is null &&
        request.ContainedByteOffset is null && request.ContainedByteLength is null;

    private static bool HasValidRequestShape(Pass2CodeLogicRetrievalRequest request) =>
        request.Operation switch
        {
            Pass2CodeLogicRetrievalOperation.Search =>
                NormalizeQuery(request.QueryText) is not null &&
                request.OpaqueHandles.Count == 0 && request.Direction is null &&
                request.ContainedByteOffset is null && request.ContainedByteLength is null,
            Pass2CodeLogicRetrievalOperation.Sections or
            Pass2CodeLogicRetrievalOperation.SourceAnchors =>
                IsHandleRequest(request) &&
                request.OpaqueHandles.Count <= Pass2RetrievalContracts.MaxResults,
            Pass2CodeLogicRetrievalOperation.SourceExcerpt =>
                request.OpaqueHandles.Count == 1 &&
                !string.IsNullOrWhiteSpace(request.OpaqueHandles[0]) &&
                request.QueryText is null && request.Direction is null &&
                request.ContainedByteOffset >= 0 && request.ContainedByteLength > 0 &&
                request.ContainedByteLength <= Pass2RetrievalContracts.MaxReturnedUtf8Bytes,
            Pass2CodeLogicRetrievalOperation.RelatedDocuments =>
                request.OpaqueHandles.Count == 1 &&
                !string.IsNullOrWhiteSpace(request.OpaqueHandles[0]) &&
                request.QueryText is null && request.Direction is not null &&
                request.ContainedByteOffset is null && request.ContainedByteLength is null,
            _ => false,
        };

    private async Task<bool> HasValidSelectorsAsync(
        RetrievalAuthority authority,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Operation == Pass2CodeLogicRetrievalOperation.Search)
        {
            return true;
        }

        if (request.OpaqueHandles.Count == 0 ||
            request.OpaqueHandles.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        DocumentSection[] sections = await LoadSectionsAsync(authority, cancellationToken)
            .ConfigureAwait(false);
        SourceAnchor[] anchors = await LoadAnchorsAsync(authority, cancellationToken)
            .ConfigureAwait(false);
        return request.Operation switch
        {
            Pass2CodeLogicRetrievalOperation.Sections or
            Pass2CodeLogicRetrievalOperation.SourceAnchors => request.OpaqueHandles.All(handle =>
                ResolveDocumentHandle(authority, handle).HasValue ||
                sections.Any(section =>
                    StringComparer.Ordinal.Equals(
                        CreateSectionHandle(authority, section, searchDescriptor: true), handle) ||
                    StringComparer.Ordinal.Equals(
                        CreateSectionHandle(authority, section, searchDescriptor: false), handle))),
            Pass2CodeLogicRetrievalOperation.SourceExcerpt =>
                request.OpaqueHandles.Count == 1 && anchors.Any(anchor =>
                    StringComparer.Ordinal.Equals(CreateAnchorHandle(authority, anchor),
                        request.OpaqueHandles[0])),
            Pass2CodeLogicRetrievalOperation.RelatedDocuments =>
                request.OpaqueHandles.Count == 1 &&
                ResolveDocumentHandle(authority, request.OpaqueHandles[0]).HasValue,
            _ => false,
        };
    }

    private static Guid? ResolveDocumentHandle(
        RetrievalAuthority authority,
        string handle)
    {
        foreach (Guid documentVersionId in authority.DocumentVersionIds)
        {
            if (StringComparer.Ordinal.Equals(
                CreateDocumentHandle(authority, documentVersionId),
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
        expected.AttemptOrdinal == current.AttemptOrdinal &&
        expected.JobStateVersion == current.JobStateVersion &&
        expected.LeaseToken == current.LeaseToken &&
        expected.LeaseExpiresAt == current.LeaseExpiresAt &&
        expected.ProviderRepositoryId == current.ProviderRepositoryId &&
        StringComparer.Ordinal.Equals(
            expected.AuthorityFingerprint,
            current.AuthorityFingerprint) &&
        expected.RepositoryIds.SequenceEqual(current.RepositoryIds) &&
        expected.InitialDocumentVersionIds.SequenceEqual(current.InitialDocumentVersionIds) &&
        expected.DocumentVersionIds.SequenceEqual(current.DocumentVersionIds) &&
        expected.DocumentBindings.SequenceEqual(current.DocumentBindings);

    private static string CreateOpaqueHandle(
        RetrievalAuthority authority,
        Pass2CodeLogicRetrievalItemKind kind,
        Guid documentVersionId,
        string evidenceFingerprint,
        int returnedUtf8Bytes,
        Guid? coordinateId = null,
        long? byteStart = null,
        long? byteEnd = null)
    {
        AuthorizedDocumentBinding binding = authority.DocumentBindings.Single(
            value => value.DocumentVersionId == documentVersionId);
        byte[] value = Encoding.UTF8.GetBytes(string.Join(
            '\n',
            "pass2-retrieval-handle/1.0",
            authority.JobId.ToString("D"),
            authority.AttemptOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
            kind.ToString(),
            documentVersionId.ToString("D"),
            binding.RepositoryId.ToString("D"),
            binding.ScanRunId.ToString("D"),
            binding.ProfiledRevision,
            coordinateId?.ToString("D") ?? string.Empty,
            byteStart?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            byteEnd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            evidenceFingerprint,
            returnedUtf8Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return Convert.ToHexStringLower(SHA256.HashData(value));
    }

    private static string CreateDocumentHandle(
        RetrievalAuthority authority,
        Guid documentVersionId)
    {
        AuthorizedDocumentBinding binding = authority.DocumentBindings.Single(
            value => value.DocumentVersionId == documentVersionId);
        string evidence = LowerSha256(string.Join(
            '\n', "document/1.0", binding.ContentFingerprint));
        int bytes = CanonicalProviderVisibleUtf8Bytes(
            new string('0', 64),
            Pass2CodeLogicRetrievalItemKind.Document,
            documentVersionId);
        return CreateOpaqueHandle(
            authority,
            Pass2CodeLogicRetrievalItemKind.Document,
            documentVersionId,
            evidence,
            bytes);
    }

    private static string CreateSectionHandle(
        RetrievalAuthority authority,
        DocumentSection section,
        bool searchDescriptor)
    {
        string? content = searchDescriptor ? null : section.ContentMarkdown;
        string evidence = LowerSha256(string.Join(
            '\n',
            "section/1.0",
            section.Id.ToString("D"),
            section.SectionOrder.ToString(System.Globalization.CultureInfo.InvariantCulture),
            section.Heading,
            section.ContentMarkdown,
            section.SourceAnchorIdsJson));
        int bytes = CanonicalProviderVisibleUtf8Bytes(
            new string('0', 64),
            Pass2CodeLogicRetrievalItemKind.Section,
            section.DocumentVersionId,
            sectionId: section.Id,
            contentMarkdown: content);
        return CreateOpaqueHandle(
            authority,
            Pass2CodeLogicRetrievalItemKind.Section,
            section.DocumentVersionId,
            evidence,
            bytes,
            section.Id);
    }

    private static string CreateAnchorHandle(RetrievalAuthority authority, SourceAnchor anchor)
    {
        string evidence = AnchorEvidenceFingerprint(anchor);
        int bytes = CanonicalProviderVisibleUtf8Bytes(
            new string('0', 64),
            Pass2CodeLogicRetrievalItemKind.SourceAnchor,
            anchor.DocumentVersionId,
            sourceAnchorId: anchor.Id);
        return CreateOpaqueHandle(
            authority,
            Pass2CodeLogicRetrievalItemKind.SourceAnchor,
            anchor.DocumentVersionId,
            evidence,
            bytes,
            anchor.Id);
    }

    private static Pass2CodeLogicRetrievalItem CreateDocumentItem(
        RetrievalAuthority authority,
        Guid documentVersionId)
    {
        string handle = CreateDocumentHandle(authority, documentVersionId);
        int bytes = CanonicalProviderVisibleUtf8Bytes(
            handle,
            Pass2CodeLogicRetrievalItemKind.Document,
            documentVersionId);
        return new(
            handle,
            Pass2CodeLogicRetrievalItemKind.Document,
            documentVersionId,
            bytes);
    }

    private static Pass2CodeLogicRetrievalItem CreateSectionItem(
        RetrievalAuthority authority,
        DocumentSection section,
        bool searchDescriptor)
    {
        string handle = CreateSectionHandle(authority, section, searchDescriptor);
        const Pass2CodeLogicRetrievalItemKind kind =
            Pass2CodeLogicRetrievalItemKind.Section;
        string? content = searchDescriptor ? null : section.ContentMarkdown;
        int returnedUtf8Bytes = CanonicalProviderVisibleUtf8Bytes(
            handle,
            kind,
            section.DocumentVersionId,
            sectionId: section.Id,
            contentMarkdown: content);
        return new(
            handle,
            kind,
            section.DocumentVersionId,
            returnedUtf8Bytes,
            sectionId: section.Id,
            contentMarkdown: content);
    }

    private static Pass2CodeLogicRetrievalItem CreateAnchorItem(
        RetrievalAuthority authority,
        SourceAnchor anchor)
    {
        string handle = CreateAnchorHandle(authority, anchor);
        int bytes = CanonicalProviderVisibleUtf8Bytes(
            handle,
            Pass2CodeLogicRetrievalItemKind.SourceAnchor,
            anchor.DocumentVersionId,
            sourceAnchorId: anchor.Id);
        return new(
            handle,
            Pass2CodeLogicRetrievalItemKind.SourceAnchor,
            anchor.DocumentVersionId,
            bytes,
            sourceAnchorId: anchor.Id);
    }

    private static Pass2CodeLogicRetrievalItem CreateExcerptItem(
        RetrievalAuthority authority,
        SourceAnchor anchor,
        long byteStart,
        long byteEnd,
        SourceExcerptResult source)
    {
        int returnedUtf8Bytes = CanonicalProviderVisibleUtf8Bytes(
            new string('0', 64),
            Pass2CodeLogicRetrievalItemKind.SourceExcerpt,
            anchor.DocumentVersionId,
            sourceAnchorId: anchor.Id,
            byteStart: byteStart,
            byteEnd: byteEnd,
            excerpt: source.Excerpt);
        string handle = CreateOpaqueHandle(
            authority,
            Pass2CodeLogicRetrievalItemKind.SourceExcerpt,
            anchor.DocumentVersionId,
            LowerSha256(string.Join(
                '\n', AnchorEvidenceFingerprint(anchor), byteStart, byteEnd, source.SourceSha256)),
            returnedUtf8Bytes,
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
            byteEnd: byteEnd,
            excerpt: source.Excerpt);
    }

    private static Pass2CodeLogicRetrievalItem CreateRelationshipItem(
        RetrievalAuthority authority,
        DocumentEdge edge,
        Guid relatedDocumentVersionId)
    {
        int bytes = CanonicalProviderVisibleUtf8Bytes(
            new string('0', 64),
            Pass2CodeLogicRetrievalItemKind.Relationship,
            relatedDocumentVersionId,
            edgeId: edge.Id);
        string handle = CreateOpaqueHandle(
            authority,
            Pass2CodeLogicRetrievalItemKind.Relationship,
            relatedDocumentVersionId,
            LowerSha256(string.Join(
                '\n', "edge/1.0", edge.Id, edge.SourceDocumentVersionId,
                edge.TargetDocumentVersionId, edge.EdgeType)),
            bytes,
            edge.Id);
        bytes = CanonicalProviderVisibleUtf8Bytes(
            handle,
            Pass2CodeLogicRetrievalItemKind.Relationship,
            relatedDocumentVersionId,
            edgeId: edge.Id);
        return new(
            handle,
            Pass2CodeLogicRetrievalItemKind.Relationship,
            relatedDocumentVersionId,
            bytes,
            edgeId: edge.Id);
    }

    private static int CanonicalProviderVisibleUtf8Bytes(
        string opaqueHandle,
        Pass2CodeLogicRetrievalItemKind kind,
        Guid documentVersionId,
        Guid? sectionId = null,
        Guid? sourceAnchorId = null,
        Guid? edgeId = null,
        long? byteStart = null,
        long? byteEnd = null,
        string? contentMarkdown = null,
        string? excerpt = null)
    {
        var item = new Pass2CodeLogicRetrievalItem(
            opaqueHandle,
            kind,
            documentVersionId,
            0,
            sectionId,
            sourceAnchorId,
            edgeId,
            byteStart,
            byteEnd,
            contentMarkdown,
            excerpt);
        return Pass2CodeLogicRetrievalProviderCodec.Serialize(
            Pass2CodeLogicRetrievalProviderCodec.Project(item)).Length;
    }

    private static string AnchorEvidenceFingerprint(SourceAnchor anchor) =>
        LowerSha256(string.Join(
            '\n',
            "anchor/1.0",
            anchor.Id,
            anchor.DocumentVersionId,
            anchor.RepositoryId,
            anchor.ScanRunId,
            anchor.ProfiledRevision,
            anchor.RepositoryRelativePath,
            anchor.EvidenceLocator,
            anchor.LineStart,
            anchor.LineEnd,
            anchor.SourceHash,
            anchor.ByteStart,
            anchor.ByteEnd));

    private static string LowerSha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

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
        int AttemptOrdinal,
        long JobStateVersion,
        Guid LeaseToken,
        DateTimeOffset LeaseExpiresAt,
        Guid ProviderRepositoryId,
        string AuthorityFingerprint,
        IReadOnlyList<Guid> RepositoryIds,
        IReadOnlyList<Guid> InitialDocumentVersionIds,
        IReadOnlyList<Guid> DocumentVersionIds,
        IReadOnlyList<AuthorizedDocumentBinding> DocumentBindings);

    private sealed record AuthorizedDocumentBinding(
        Guid DocumentVersionId,
        Guid RepositoryId,
        Guid ScanRunId,
        string ProfiledRevision,
        string ContentFingerprint);
}
