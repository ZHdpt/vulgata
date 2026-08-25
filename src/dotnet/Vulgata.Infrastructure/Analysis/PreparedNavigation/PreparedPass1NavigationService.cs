using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Analysis.PreparedNavigation;

public sealed class PreparedPass1NavigationService(
    IPreparedPass1NavigationScopeSource scopeSource,
    IPreparedPass1CurrentDocumentEvidenceSource documentSource,
    IPreparedPass1NavigationReceiptSink receiptSink,
    IJavaAnalysisSessionFactory javaSessionFactory,
    ICodeGraphAnalysisPort codeGraph,
    IRevisionBoundSourceReader sourceReader,
    TimeProvider? timeProvider = null) : IPass1NavigationService, IPass1ReservedNavigationService
{
    private const int _maximumCalls = 8;
    private const int _maximumResults = 20;
    private const int _maximumEvidenceBytes = 65_536;
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private readonly IPreparedPass1NavigationScopeSource _scopeSource =
        scopeSource ?? throw new ArgumentNullException(nameof(scopeSource));
    private readonly IPreparedPass1CurrentDocumentEvidenceSource _documentSource =
        documentSource ?? throw new ArgumentNullException(nameof(documentSource));
    private readonly IPreparedPass1NavigationReceiptSink _receiptSink =
        receiptSink ?? throw new ArgumentNullException(nameof(receiptSink));
    private readonly IJavaAnalysisSessionFactory _javaSessionFactory =
        javaSessionFactory ?? throw new ArgumentNullException(nameof(javaSessionFactory));
    private readonly ICodeGraphAnalysisPort _codeGraph =
        codeGraph ?? throw new ArgumentNullException(nameof(codeGraph));
    private readonly IRevisionBoundSourceReader _sourceReader =
        sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<PreparedPass1NavigationResponse> NavigateAsync(
        PreparedPass1NavigationRequestInput request,
        CancellationToken cancellationToken) =>
        await NavigateCoreAsync(request, null, cancellationToken).ConfigureAwait(false);

    public async Task<PreparedPass1NavigationResponse> NavigateReservedAsync(
        PreparedPass1NavigationRequestInput request,
        PreparedPass1NavigationReservationTicket reservation,
        CancellationToken cancellationToken) =>
        await NavigateCoreAsync(request, reservation, cancellationToken).ConfigureAwait(false);

    private async Task<PreparedPass1NavigationResponse> NavigateCoreAsync(
        PreparedPass1NavigationRequestInput request,
        PreparedPass1NavigationReservationTicket? reservation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        PreparedPass1NavigationScope? scope = await _scopeSource.GetAsync(
            request,
            cancellationToken).ConfigureAwait(false);
        if (scope is null || !ScopeMatches(scope, request, _timeProvider.GetUtcNow()))
        {
            return Empty(PreparedPass1NavigationOutcome.Unavailable, Guid.Empty, request, null);
        }

        byte[] requestDigest = CreateRequestDigest(request);
        byte[] targetIdentity = CreateTargetIdentity(request);

        if (request.ToolAttemptOrdinal is < 1 or > _maximumCalls ||
            scope.ToolAttemptCount > _maximumCalls ||
            reservation is null && scope.ToolAttemptCount >= _maximumCalls ||
            scope.AcceptedCallCount >= _maximumCalls)
        {
            return await CommitBoundAsync(
                scope,
                request,
                requestDigest,
                targetIdentity,
                PreparedPass1NavigationBoundKind.Calls,
                reservation,
                cancellationToken).ConfigureAwait(false);
        }

        if (!TryValidateDepth(scope, request, targetIdentity, out bool depthRefusal) ||
            !HasClosedTargetGrammar(request))
        {
            return Empty(PreparedPass1NavigationOutcome.Unavailable, Guid.Empty, request, null);
        }

        if (depthRefusal)
        {
            return await CommitBoundAsync(
                scope,
                request,
                requestDigest,
                targetIdentity,
                PreparedPass1NavigationBoundKind.Depth,
                reservation,
                cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<NavigationCandidate>? candidates = await ResolveCandidatesAsync(
            scope,
            request,
            cancellationToken).ConfigureAwait(false);
        if (candidates is null)
        {
            return await CommitAsync(
                scope,
                request,
                requestDigest,
                targetIdentity,
                PreparedPass1NavigationOutcome.Unavailable,
                null,
                [],
                0,
                reservation,
                cancellationToken).ConfigureAwait(false);
        }

        NavigationCandidate[] distinct = [.. candidates
            .DistinctBy(CandidateIdentity, StringComparer.Ordinal)
            .OrderBy(CandidateIdentity, StringComparer.Ordinal)];
        if (distinct.Length > _maximumResults)
        {
            return await CommitBoundAsync(
                scope,
                request,
                requestDigest,
                targetIdentity,
                PreparedPass1NavigationBoundKind.Results,
                reservation,
                cancellationToken).ConfigureAwait(false);
        }

        var descriptors = new List<PreparedPass1EvidenceDescriptor>(distinct.Length);
        int evidenceBytes = 0;
        bool usedDocument = distinct.Length > 0;
        foreach (NavigationCandidate candidate in distinct)
        {
            PreparedPass1AuthorizedSource? authorized = FindAuthorizedSource(scope, candidate);
            if (authorized is null)
            {
                continue;
            }

            PreparedPass1NavigationTarget candidateTarget = new(
                candidate.CanonicalPath,
                candidate.Symbol,
                null,
                candidate.LineRange,
                Convert.FromHexString(authorized.SourceHash));
            PreparedPass1CurrentDocumentEvidence? document = await _documentSource.FindAsync(
                scope,
                authorized,
                candidateTarget,
                cancellationToken).ConfigureAwait(false);
            if (IsCurrentDocument(scope, authorized, document))
            {
                descriptors.Add(document!.Descriptor);
                evidenceBytes = checked(evidenceBytes + Measure(document.Descriptor));
                continue;
            }

            usedDocument = false;
            SourceExcerptResult excerpt = await _sourceReader.ReadAsync(
                scope.AnalysisScope,
                new RevisionBoundSourceRequest(
                    authorized.CanonicalPath,
                    ToAnalysisRange(candidate.LineRange),
                    authorized.SourceHash,
                    _maximumEvidenceBytes),
                cancellationToken).ConfigureAwait(false);
            if (!IsCurrentSource(scope, authorized, candidate, excerpt))
            {
                continue;
            }

            descriptors.Add(new PreparedPass1SourceEvidenceDescriptor(
                authorized.CanonicalPath,
                candidate.Symbol,
                candidate.LineRange,
                authorized.ByteRange,
                Convert.FromHexString(authorized.SourceHash),
                authorized.EvidenceLocator));
            evidenceBytes = checked(evidenceBytes + excerpt.ByteCount);
        }

        if (descriptors.Count == 0)
        {
            return await CommitAsync(
                scope,
                request,
                requestDigest,
                targetIdentity,
                PreparedPass1NavigationOutcome.Unavailable,
                null,
                [],
                0,
                reservation,
                cancellationToken).ConfigureAwait(false);
        }

        if (evidenceBytes > _maximumEvidenceBytes - scope.EvidenceBytes)
        {
            return await CommitBoundAsync(
                scope,
                request,
                requestDigest,
                targetIdentity,
                PreparedPass1NavigationBoundKind.EvidenceBytes,
                reservation,
                cancellationToken).ConfigureAwait(false);
        }

        PreparedPass1NavigationOutcome outcome = usedDocument
            ? PreparedPass1NavigationOutcome.DocumentEvidence
            : PreparedPass1NavigationOutcome.SourceEvidence;
        return await CommitAsync(
            scope,
            request,
            requestDigest,
            targetIdentity,
            outcome,
            null,
            descriptors,
            evidenceBytes,
            reservation,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<NavigationCandidate>?> ResolveCandidatesAsync(
        PreparedPass1NavigationScope scope,
        PreparedPass1NavigationRequestInput request,
        CancellationToken cancellationToken)
    {
        var bounds = new AnalysisOperationBounds(_maximumResults + 1, _maximumEvidenceBytes);
        switch (request.Operation)
        {
            case PreparedPass1NavigationOperation.Definition:
            case PreparedPass1NavigationOperation.References:
            case PreparedPass1NavigationOperation.Implementations:
                {
                    await using IJavaAnalysisSession session = await _javaSessionFactory.OpenAsync(
                        scope.AnalysisScope,
                        cancellationToken).ConfigureAwait(false);
                    JavaNavigationRequest navigationRequest = new(
                        request.Target.CanonicalPath!,
                        new AnalysisSourcePosition(
                            checked((int)request.Target.Position!.Line),
                            checked((int)request.Target.Position.Utf16Character + 1)),
                        bounds);
                    JavaNavigationResult result = request.Operation switch
                    {
                        PreparedPass1NavigationOperation.Definition => await session.GetDefinitionAsync(
                            scope.AnalysisScope, navigationRequest, cancellationToken).ConfigureAwait(false),
                        PreparedPass1NavigationOperation.References => await session.GetReferencesAsync(
                            scope.AnalysisScope, navigationRequest, cancellationToken).ConfigureAwait(false),
                        PreparedPass1NavigationOperation.Implementations => await session.GetImplementationsAsync(
                            scope.AnalysisScope, navigationRequest, cancellationToken).ConfigureAwait(false),
                        PreparedPass1NavigationOperation.Callers => throw new NotImplementedException(),
                        PreparedPass1NavigationOperation.Callees => throw new NotImplementedException(),
                        PreparedPass1NavigationOperation.SymbolSearch => throw new NotImplementedException(),
                        PreparedPass1NavigationOperation.RelatedFiles => throw new NotImplementedException(),
                        PreparedPass1NavigationOperation.ExactSourceEvidence => throw new NotImplementedException(),
                        _ => await session.GetImplementationsAsync(
                                                    scope.AnalysisScope, navigationRequest, cancellationToken).ConfigureAwait(false),
                    };
                    return result.Status == AnalysisOperationStatus.Succeeded
                        ? result.Evidence.Select(ToCandidate).ToArray()
                        : null;
                }
            case PreparedPass1NavigationOperation.Callers:
            case PreparedPass1NavigationOperation.Callees:
                {
                    var symbolRequest = new AnalysisSymbolRequest(request.Target.Symbol!, bounds);
                    RelationshipResult result = request.Operation == PreparedPass1NavigationOperation.Callers
                        ? await _codeGraph.GetCallersAsync(
                            scope.AnalysisScope, symbolRequest, cancellationToken).ConfigureAwait(false)
                        : await _codeGraph.GetCalleesAsync(
                            scope.AnalysisScope, symbolRequest, cancellationToken).ConfigureAwait(false);
                    return result.Status == AnalysisOperationStatus.Succeeded
                        ? result.Relationships.Select(value => new NavigationCandidate(
                            value.RelativePath,
                            value.TargetSymbol,
                            ToPass1Range(value.Range))).ToArray()
                        : null;
                }
            case PreparedPass1NavigationOperation.SymbolSearch:
                {
                    SymbolSearchResult result = await _codeGraph.SearchSymbolsAsync(
                        scope.AnalysisScope,
                        new AnalysisSymbolQuery(request.Target.Symbol!, AnalysisSymbolKind.Method, bounds),
                        cancellationToken).ConfigureAwait(false);
                    return result.Status == AnalysisOperationStatus.Succeeded
                        ? result.Symbols.Select(ToCandidate).ToArray()
                        : null;
                }
            case PreparedPass1NavigationOperation.RelatedFiles:
                {
                    RelatedFilesResult result = await _codeGraph.GetRelatedFilesAsync(
                        scope.AnalysisScope,
                        new AnalysisSymbolRequest(request.Target.Symbol!, bounds),
                        cancellationToken).ConfigureAwait(false);
                    return result.Status == AnalysisOperationStatus.Succeeded
                        ? result.RelativePaths.SelectMany(path => scope.AuthorizedSources
                            .Where(source => StringComparer.Ordinal.Equals(source.CanonicalPath, path))
                            .Select(source => new NavigationCandidate(path, null, source.LineRange)))
                            .ToArray()
                        : null;
                }
            case PreparedPass1NavigationOperation.ExactSourceEvidence:
                return
                [
                    new NavigationCandidate(
                        request.Target.CanonicalPath!,
                        null,
                        request.Target.ExactLineRange!,
                        request.Target.ExpectedSourceHash),
                ];
            default:
                return null;
        }
    }

    private async Task<PreparedPass1NavigationResponse> CommitBoundAsync(
        PreparedPass1NavigationScope scope,
        PreparedPass1NavigationRequestInput request,
        ReadOnlyMemory<byte> requestDigest,
        ReadOnlyMemory<byte> targetIdentity,
        PreparedPass1NavigationBoundKind boundKind,
        PreparedPass1NavigationReservationTicket? reservation,
        CancellationToken cancellationToken) =>
        await CommitAsync(
            scope,
            request,
            requestDigest,
            targetIdentity,
            PreparedPass1NavigationOutcome.BoundExceeded,
            boundKind,
            [],
            0,
            reservation,
            cancellationToken).ConfigureAwait(false);

    private async Task<PreparedPass1NavigationResponse> CommitAsync(
        PreparedPass1NavigationScope scope,
        PreparedPass1NavigationRequestInput request,
        ReadOnlyMemory<byte> requestDigest,
        ReadOnlyMemory<byte> targetIdentity,
        PreparedPass1NavigationOutcome outcome,
        PreparedPass1NavigationBoundKind? boundKind,
        IReadOnlyList<PreparedPass1EvidenceDescriptor> results,
        int evidenceBytes,
        PreparedPass1NavigationReservationTicket? reservation,
        CancellationToken cancellationToken)
    {
        var draft = new PreparedPass1NavigationReceiptDraft(
            requestDigest,
            request.Operation,
            request.RequestedDepth,
            outcome,
            boundKind,
            results,
            results.Count,
            evidenceBytes,
            targetIdentity);
        PreparedPass1NavigationReservationTicket? replacement =
            reservation is not null
                ? await _receiptSink.CommitReservedAsync(
                    scope,
                    request,
                    reservation,
                    draft,
                    cancellationToken).ConfigureAwait(false)
                : null;
        Guid? receiptId = replacement?.ReceiptId;
        if (reservation is null)
        {
            receiptId = await _receiptSink.CommitAsync(
                scope,
                request,
                draft,
                cancellationToken).ConfigureAwait(false);
        }

        return receiptId is { } committed && committed != Guid.Empty
            ? new PreparedPass1NavigationResponse(
                outcome,
                committed,
                request.Operation,
                request.RequestedDepth,
                boundKind,
                results.Count,
                evidenceBytes,
                [.. results.Select(ToResult)],
                replacement)
            : Empty(PreparedPass1NavigationOutcome.Unavailable, Guid.Empty, request, null);
    }

    private static bool ScopeMatches(
        PreparedPass1NavigationScope scope,
        PreparedPass1NavigationRequestInput request,
        DateTimeOffset currentTime) =>
        scope.JobId == request.JobId &&
        scope.SourceFileCodeUnitId == request.SourceFileCodeUnitId &&
        AuthorizationMatches(scope, request, currentTime) &&
        scope.RepositoryId != Guid.Empty &&
        scope.ScanRunId != Guid.Empty &&
        scope.AnalysisScope.RepositoryId == scope.RepositoryId &&
        scope.AnalysisScope.ScanRunId == scope.ScanRunId &&
        StringComparer.OrdinalIgnoreCase.Equals(
            scope.AnalysisScope.ProfiledRevision,
            scope.ProfiledRevision) &&
        scope.ToolAttemptCount is >= 0 and <= _maximumCalls &&
        scope.AcceptedCallCount is >= 0 and <= _maximumCalls &&
        scope.AcceptedCallCount <= scope.ToolAttemptCount &&
        scope.EvidenceBytes is >= 0 and <= _maximumEvidenceBytes;

    private static bool AuthorizationMatches(
        PreparedPass1NavigationScope scope,
        PreparedPass1NavigationRequestInput request,
        DateTimeOffset currentTime) =>
            currentTime.Offset == TimeSpan.Zero &&
            scope.PairExecutionId != Guid.Empty &&
            scope.PairExecutionId == request.PairExecutionId &&
            scope.JobStateVersion > 0 && scope.JobStateVersion == request.JobStateVersion &&
            scope.PairStateVersion >= 0 && scope.PairStateVersion == request.PairStateVersion &&
            scope.LeaseToken != Guid.Empty && scope.LeaseToken == request.LeaseToken &&
            scope.LeaseExpiresAt > currentTime && request.LeaseExpiresAt > currentTime &&
            string.Equals(scope.PairState, nameof(Pass1PairExecutionPhase.Running), StringComparison.Ordinal) &&
            string.Equals(request.PairState, nameof(Pass1PairExecutionPhase.Running), StringComparison.Ordinal) &&
            scope.CircuitState == Pass1CircuitState.Closed &&
            request.CircuitState == Pass1CircuitState.Closed &&
            scope.JobState == ScanJobExecutionState.Running &&
            request.JobState == ScanJobExecutionState.Running &&
            !string.IsNullOrWhiteSpace(scope.AuthorizationBinding) &&
            string.Equals(scope.AuthorizationBinding, request.AuthorizationBinding, StringComparison.Ordinal);

    private static bool TryValidateDepth(
        PreparedPass1NavigationScope scope,
        PreparedPass1NavigationRequestInput request,
        ReadOnlyMemory<byte> targetIdentity,
        out bool refusal)
    {
        refusal = false;
        if (request.RequestedDepth is < 1 or > 4)
        {
            return false;
        }

        bool hasParent = request.ParentObservationIdentity is { } parent && !parent.IsEmpty;
        if (request.RequestedDepth == 1 != !hasParent)
        {
            return false;
        }

        if (hasParent && !scope.ObservedTargetIdentities.Any(value =>
                value.Span.SequenceEqual(request.ParentObservationIdentity!.Value.Span)))
        {
            return false;
        }

        refusal = request.RequestedDepth == 4 ||
            scope.ObservedTargetIdentities.Any(value => value.Span.SequenceEqual(targetIdentity.Span));
        return true;
    }

    private static bool HasClosedTargetGrammar(PreparedPass1NavigationRequestInput request)
    {
        PreparedPass1NavigationTarget target = request.Target;
        bool hasPath = IsCanonicalRelativePath(target.CanonicalPath);
        bool hasSymbol = !string.IsNullOrWhiteSpace(target.Symbol) && target.Symbol.Length <= 512;
        bool hasPosition = target.Position is
        {
            Line: > 0 and <= int.MaxValue,
            Utf16Character: < int.MaxValue,
        };
        bool hasRange = target.ExactLineRange is
        { Start: > 0 and <= int.MaxValue, End: <= int.MaxValue } range &&
            range.End >= range.Start;
        bool hasHash = target.ExpectedSourceHash is { } hash && hash.Length == 32;

        return request.Operation switch
        {
            PreparedPass1NavigationOperation.Definition or
            PreparedPass1NavigationOperation.References or
            PreparedPass1NavigationOperation.Implementations =>
                hasPath && hasPosition && !hasSymbol && !hasRange && !hasHash,
            PreparedPass1NavigationOperation.Callers or
            PreparedPass1NavigationOperation.Callees or
            PreparedPass1NavigationOperation.SymbolSearch or
            PreparedPass1NavigationOperation.RelatedFiles =>
                !hasPath && hasSymbol && !hasPosition && !hasRange && !hasHash,
            PreparedPass1NavigationOperation.ExactSourceEvidence =>
                hasPath && !hasSymbol && !hasPosition && hasRange && hasHash,
            _ => false,
        };
    }

    private static bool IsCanonicalRelativePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        path.Length <= 2_048 &&
        !Path.IsPathRooted(path) &&
        !path.Contains('\\', StringComparison.Ordinal) &&
        path.Split('/').All(segment => segment is not "" and not "." and not "..");

    private static PreparedPass1AuthorizedSource? FindAuthorizedSource(
        PreparedPass1NavigationScope scope,
        NavigationCandidate candidate) =>
        scope.AuthorizedSources
            .Where(source =>
                StringComparer.Ordinal.Equals(source.CanonicalPath, candidate.CanonicalPath) &&
                candidate.LineRange.Start >= source.LineRange.Start &&
                candidate.LineRange.End <= source.LineRange.End &&
                IsSha256(source.SourceHash) &&
                (candidate.ExpectedSourceHash is null ||
                    candidate.ExpectedSourceHash.Value.Span.SequenceEqual(
                        Convert.FromHexString(source.SourceHash))))
            .OrderBy(source => source.SegmentOrdinal)
            .FirstOrDefault();

    private static bool IsCurrentDocument(
        PreparedPass1NavigationScope scope,
        PreparedPass1AuthorizedSource source,
        PreparedPass1CurrentDocumentEvidence? document) =>
        document is not null &&
        document.RepositoryId == scope.RepositoryId &&
        document.ScanRunId == scope.ScanRunId &&
        StringComparer.OrdinalIgnoreCase.Equals(document.ProfiledRevision, scope.ProfiledRevision) &&
        document.SegmentOrdinal == source.SegmentOrdinal &&
        StringComparer.OrdinalIgnoreCase.Equals(document.SourceHash, source.SourceHash) &&
        StringComparer.Ordinal.Equals(document.Descriptor.CanonicalPath, source.CanonicalPath) &&
        document.Descriptor.DocumentVersionId != Guid.Empty &&
        document.Descriptor.LogicalDocumentId != Guid.Empty &&
        document.Descriptor.TargetSourceHash.Span.SequenceEqual(Convert.FromHexString(source.SourceHash));

    private static bool IsCurrentSource(
        PreparedPass1NavigationScope scope,
        PreparedPass1AuthorizedSource source,
        NavigationCandidate candidate,
        SourceExcerptResult excerpt) =>
        excerpt.Status == AnalysisOperationStatus.Succeeded &&
        excerpt.RepositoryId == scope.RepositoryId &&
        StringComparer.OrdinalIgnoreCase.Equals(excerpt.ProfiledRevision, scope.ProfiledRevision) &&
        StringComparer.Ordinal.Equals(excerpt.RelativePath, source.CanonicalPath) &&
        StringComparer.OrdinalIgnoreCase.Equals(excerpt.SourceSha256, source.SourceHash) &&
        excerpt.Range.StartLine == checked((int)candidate.LineRange.Start) &&
        excerpt.Range.EndLine == checked((int)candidate.LineRange.End) &&
        excerpt.ByteCount == _strictUtf8.GetByteCount(excerpt.Excerpt);

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static int Measure(PreparedPass1DocumentEvidenceDescriptor descriptor)
    {
        string stable = string.Join(
            '\n',
            descriptor.CanonicalPath,
            descriptor.LogicalDocumentId.ToString("D"),
            descriptor.DocumentVersionId.ToString("D"),
            Convert.ToHexStringLower(descriptor.TargetSourceHash.Span),
            string.Join(',', descriptor.SectionIds.Order()),
            string.Join(',', descriptor.SourceAnchorIds.Order()));
        return _strictUtf8.GetByteCount(stable);
    }

    private static PreparedPass1NavigationResult ToResult(
        PreparedPass1EvidenceDescriptor descriptor) => descriptor switch
        {
            PreparedPass1DocumentEvidenceDescriptor document => new(
                document.CanonicalPath,
                document.DocumentVersionId,
                document.TargetSourceHash,
                document.SectionIds,
                document.SourceAnchorIds,
                null,
                null,
                null,
                null),
            PreparedPass1SourceEvidenceDescriptor source => new(
                source.CanonicalPath,
                null,
                source.SourceHash,
                [],
                [],
                source.Symbol,
                source.LineRange,
                source.ByteRange,
                source.EvidenceLocator),
            _ => throw new InvalidOperationException("Unsupported prepared navigation evidence descriptor."),
        };

    private static NavigationCandidate ToCandidate(AnalysisSymbolEvidence value) =>
        new(value.RelativePath, value.Symbol, ToPass1Range(value.Range));

    private static PreparedPass1LineRange ToPass1Range(AnalysisLineRange value) =>
        new(checked((ulong)value.StartLine), checked((ulong)value.EndLine));

    private static AnalysisLineRange ToAnalysisRange(PreparedPass1LineRange value) =>
        new(checked((int)value.Start), checked((int)value.End));

    private static string CandidateIdentity(NavigationCandidate candidate) => string.Join(
        '\n',
        candidate.CanonicalPath,
        candidate.Symbol ?? string.Empty,
        candidate.LineRange.Start.ToString(CultureInfo.InvariantCulture),
        candidate.LineRange.End.ToString(CultureInfo.InvariantCulture));

    private static byte[] CreateRequestDigest(PreparedPass1NavigationRequestInput request) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            '\n',
            request.JobId.ToString("D"),
            request.ToolAttemptOrdinal.ToString(CultureInfo.InvariantCulture),
            request.Operation.ToString(),
            request.SourceFileCodeUnitId.ToString("D"),
            Convert.ToHexStringLower(CreateTargetIdentity(request)),
            request.RequestedDepth.ToString(CultureInfo.InvariantCulture),
            request.ParentObservationIdentity is { } parent
                ? Convert.ToHexStringLower(parent.Span)
                : string.Empty)));

    private static byte[] CreateTargetIdentity(PreparedPass1NavigationRequestInput request)
    {
        PreparedPass1NavigationTarget target = request.Target;
        return SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            '\n',
            request.Operation.ToString(),
            target.CanonicalPath ?? string.Empty,
            target.Symbol?.Normalize(NormalizationForm.FormC) ?? string.Empty,
            target.Position?.Line.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            target.Position?.Utf16Character.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            target.ExactLineRange?.Start.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            target.ExactLineRange?.End.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            target.ExpectedSourceHash is { } hash
                ? Convert.ToHexStringLower(hash.Span)
                : string.Empty)));
    }

    private static PreparedPass1NavigationResponse Empty(
        PreparedPass1NavigationOutcome outcome,
        Guid receiptId,
        PreparedPass1NavigationRequestInput request,
        PreparedPass1NavigationBoundKind? boundKind) =>
        new(outcome, receiptId, request.Operation, request.RequestedDepth, boundKind, 0, 0, []);

    private sealed record NavigationCandidate(
        string CanonicalPath,
        string? Symbol,
        PreparedPass1LineRange LineRange,
        ReadOnlyMemory<byte>? ExpectedSourceHash = null);
}
