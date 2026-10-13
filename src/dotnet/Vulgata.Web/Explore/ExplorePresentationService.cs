using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.Explore;
using Vulgata.Shared.OctoberKnowledge;

namespace Vulgata.Web.Explore;

public sealed record ExploreContext(ReadSelectionRequest Selection, bool IsControlledFixture = false);

/// <summary>
/// Integration supplies trusted circuit context and emits ContextChanged whenever subject, mode,
/// purpose, scope or publication changes. Browser parameters never create this authority.
/// </summary>
public interface IExploreContextProvider
{
    event Action? ContextChanged;
    Task<ExploreContext?> GetCurrentAsync(CancellationToken cancellationToken = default);
}

/// <summary>Read-only presentation adapter. All contributor decisions remain in the Knowledge owner.</summary>
public sealed partial class ExplorePresentationService : IExplorePresentationService, IExploreGraphService, IDisposable
{
    private readonly IOctoberKnowledgeReader? _reader;
    private readonly IExploreContextProvider _contextProvider;
    private readonly AuthenticationStateProvider? _authentication;
    private readonly ClaimsIdentityOptions _claims;
    private readonly IOctoberKnowledgeDisclosedReader? _disclosedReader;
    private readonly IOctoberKnowledgeDependencyAuthority? _dependencyAuthority;
    private readonly TimeProvider _timeProvider;
    private long _contextGeneration;
    private bool _disposed;

    public ExplorePresentationService(IOctoberKnowledgeReader? reader, IExploreContextProvider contextProvider,
        AuthenticationStateProvider? authenticationStateProvider, IOptions<IdentityOptions>? identityOptions = null,
        IOctoberKnowledgeDisclosedReader? disclosedReader = null,
        IOctoberKnowledgeDependencyAuthority? dependencyAuthority = null, TimeProvider? timeProvider = null)
    {
        _reader = reader;
        _contextProvider = contextProvider;
        _authentication = authenticationStateProvider;
        _claims = identityOptions?.Value.ClaimsIdentity ?? new ClaimsIdentityOptions();
        _disclosedReader = disclosedReader;
        _dependencyAuthority = dependencyAuthority;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _contextProvider.ContextChanged += InvalidateContext;
        if (_authentication is not null)
        {
            _authentication.AuthenticationStateChanged += OnAuthenticationChanged;
        }
    }

    public event Action? ContextChanged;

    public async Task<ExploreReadResult> OpenDocumentAsync(DocumentHandle document, CancellationToken cancellationToken = default)
    {
        try
        {
            if (document is null || !new ExploreDocumentHandleValidator().Validate(document).IsValid)
            {
                return ReadFailure(KnowledgeReadStatus.Unavailable);
            }

            var generation = Volatile.Read(ref _contextGeneration);
            var context = await ObserveContextAsync(cancellationToken);
            if (context is null || _reader is null || !Matches(document, context.Selection) || generation != Volatile.Read(ref _contextGeneration))
            {
                return ReadFailure(KnowledgeReadStatus.Unavailable);
            }

            var opened = await _reader.OpenReadSelectionAsync(context.Selection, cancellationToken);
            if (opened.Status != KnowledgeReadStatus.Success || string.IsNullOrWhiteSpace(opened.Selection?.Token) ||
                !await IsCurrentAsync(context, generation, cancellationToken))
            {
                return ReadFailure(KnowledgeReadStatus.Unavailable);
            }

            return await ReadAsync(context, generation, opened.Selection, document, null, cancellationToken);
        }
        catch (OperationCanceledException) { return ReadFailure(KnowledgeReadStatus.Cancelled); }
        catch (Exception) { return ReadFailure(KnowledgeReadStatus.UnavailableService); }
    }

    public async Task<ExploreReadResult> ContinueDocumentAsync(ExploreReadSession session, CancellationToken cancellationToken = default)
    {
        try
        {
            if (session is not ReaderSession captured || !ReferenceEquals(captured.Issuer, this) ||
                Interlocked.Exchange(ref captured.Consumed, 1) != 0 ||
                !await IsCurrentAsync(captured.Context, captured.Generation, cancellationToken))
            {
                return ReadFailure(KnowledgeReadStatus.InvalidContinuation);
            }

            return await ReadAsync(captured.Context, captured.Generation, captured.Selection,
                captured.Document, captured.Next, cancellationToken);
        }
        catch (OperationCanceledException) { return ReadFailure(KnowledgeReadStatus.Cancelled); }
        catch (Exception) { return ReadFailure(KnowledgeReadStatus.UnavailableService); }
    }

    public async Task<ExploreBrowseResult> BrowseAsync(ExploreBrowseSession? continuation = null, CancellationToken cancellationToken = default, string titleQuery = "")
    {
        try
        {
            titleQuery = titleQuery.Trim();
            ExploreContext context;
            ReadSelection selection;
            ContinuationToken? cursor;
            long generation;
            if (continuation is null)
            {
                generation = Volatile.Read(ref _contextGeneration);
                var observed = await ObserveContextAsync(cancellationToken);
                if (observed is null || _reader is null || generation != Volatile.Read(ref _contextGeneration))
                {
                    return BrowseFailure(KnowledgeReadStatus.Unavailable);
                }

                context = observed;
                var opened = await _reader.OpenReadSelectionAsync(context.Selection, cancellationToken);
                if (opened.Status != KnowledgeReadStatus.Success || string.IsNullOrWhiteSpace(opened.Selection?.Token) ||
                    !await IsCurrentAsync(context, generation, cancellationToken))
                {
                    return BrowseFailure(KnowledgeReadStatus.Unavailable);
                }

                selection = opened.Selection;
                cursor = null;
            }
            else if (continuation is BrowseSession captured && ReferenceEquals(captured.Issuer, this) &&
                Interlocked.Exchange(ref captured.Consumed, 1) == 0 &&
                await IsCurrentAsync(captured.Context, captured.Generation, cancellationToken))
            {
                context = captured.Context;
                generation = captured.Generation;
                selection = captured.Selection;
                cursor = captured.Next;
                titleQuery = captured.TitleQuery;
            }
            else
            {
                return BrowseFailure(KnowledgeReadStatus.InvalidContinuation);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                var page = await _reader!.SearchAsync(new(selection, "", KnowledgeContract.DefaultPageSize, cursor), cancellationToken);
                if (!await IsCurrentAsync(context, generation, cancellationToken))
                {
                    return BrowseFailure(KnowledgeReadStatus.Unavailable);
                }

                if (page.Status != KnowledgeReadStatus.Success)
                {
                    return BrowseFailure(page.Status);
                }

                if (page.Documents.IsDefault || page.Documents.Length > KnowledgeContract.DefaultPageSize ||
                    page.Documents.Any(document => !ValidSummary(document, context.Selection)) ||
                    !ValidNext(page.Next, cursor) || !ValidCoverage(page.Coverage))
                {
                    return BrowseFailure(KnowledgeReadStatus.UnavailableService);
                }

                page = page with { Documents = page.Documents.Where(document => document.Title.Contains(titleQuery, StringComparison.OrdinalIgnoreCase)).ToImmutableArray() };
                if (page.Documents.IsEmpty && page.Next is not null && titleQuery.Length != 0)
                {
                    if (!seen.Add(page.Next.Value)) return BrowseFailure(KnowledgeReadStatus.InvalidContinuation);
                    cursor = page.Next;
                    continue;
                }
                return new(page, page.Next is null ? null : new BrowseSession(this, context, generation, selection, page.Next, titleQuery),
                    context.Selection.Mode, context.IsControlledFixture);
            }
        }
        catch (OperationCanceledException) { return BrowseFailure(KnowledgeReadStatus.Cancelled); }
        catch (Exception) { return BrowseFailure(KnowledgeReadStatus.UnavailableService); }
    }

    private async Task<ExploreReadResult> ReadAsync(ExploreContext context, long generation, ReadSelection selection,
        DocumentHandle document, ContinuationToken? cursor, CancellationToken cancellationToken)
    {
        var page = await _reader!.ReadExactAsync(new(selection, document, cursor), cancellationToken);
        if (!await IsCurrentAsync(context, generation, cancellationToken))
        {
            return ReadFailure(KnowledgeReadStatus.Unavailable);
        }

        if (page.Status != KnowledgeReadStatus.Success)
        {
            return ReadFailure(page.Status);
        }

        // Conservatively bound the complete canonical projection, including title, claims and coverage.
        // Default System.Text.Json escaping is intentional; never truncate content to satisfy the cap.
        if (page.Document?.Handle != document || !ValidSummary(page.Document, context.Selection) ||
            !ExplorePresentationText.IsWellFormed(page.Content) || !ValidClaims(page.Claims) ||
            !ValidNext(page.Next, cursor) || !ValidCoverage(page.Coverage) ||
            JsonSerializer.SerializeToUtf8Bytes(page).Length > KnowledgeContract.MaximumContentBytes)
        {
            return ReadFailure(KnowledgeReadStatus.UnavailableService);
        }

        return new(page, page.Next is null ? null : new ReaderSession(this, context, generation, selection, document, page.Next),
            context.Selection.Mode, context.IsControlledFixture);
    }

    private async Task<ExploreContext?> ObserveContextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed || _authentication is null) { return null; }
        var context = await _contextProvider.GetCurrentAsync(cancellationToken);
        var principal = (await _authentication.GetAuthenticationStateAsync()).User;
        cancellationToken.ThrowIfCancellationRequested();
        var identities = principal.Identities.Where(identity => identity.IsAuthenticated).ToArray();
        if (context is null || identities.Length != 1 ||
            context.Selection.Purpose != KnowledgePurpose.Reader ||
            identities[0].FindFirst(_claims.UserIdClaimType)?.Value != context.Selection.Subject.SubjectId ||
            string.IsNullOrWhiteSpace(context.Selection.Subject.AuthenticationBinding))
        {
            return null;
        }

        // Bind both values to the same authenticated identity using the host's Identity configuration.
        var stamp = identities[0].FindFirst(_claims.SecurityStampClaimType)?.Value;
        if (stamp is not null && stamp != context.Selection.Subject.AuthenticationBinding) { return null; }
        var request = context.Selection;
        if (request.Publication.Knowledge.BatchId == Guid.Empty ||
            !new OpenKnowledgeSelectionRequestValidator().Validate(new OpenKnowledgeSelectionRequest(request.Publication.Knowledge.BatchId,
                request.Publication.PublicationId, request.Mode, request.RepositoryScope)).IsValid)
        {
            return null;
        }

        return context;
    }

    private async Task<bool> IsCurrentAsync(ExploreContext original, long generation, CancellationToken cancellationToken)
    {
        var current = await ObserveContextAsync(cancellationToken);
        return !_disposed && generation == Volatile.Read(ref _contextGeneration) && current is not null &&
            current.IsControlledFixture == original.IsControlledFixture &&
            current.Selection.Publication == original.Selection.Publication &&
            current.Selection.Subject == original.Selection.Subject && current.Selection.Mode == original.Selection.Mode &&
            current.Selection.Purpose == original.Selection.Purpose &&
            current.Selection.RepositoryScope.SequenceEqual(original.Selection.RepositoryScope);
    }

    private static bool Matches(DocumentHandle document, ReadSelectionRequest selection) =>
        document.BatchId == selection.Publication.Knowledge.BatchId && document.PublicationId == selection.Publication.PublicationId;

    private static bool ValidSummary(DocumentSummary? document, ReadSelectionRequest selection) => document is not null &&
        new ExploreDocumentHandleValidator().Validate(document.Handle).IsValid && Matches(document.Handle, selection) &&
        Enum.IsDefined(document.Kind) && (selection.Mode != KnowledgeMode.Business || document.Kind == DocumentKind.BusinessLogic) &&
        ExplorePresentationText.IsWellFormed(document.Title) && ValidClaims(document.QualifiedExcerpt);

    private static bool ValidClaims(System.Collections.Immutable.ImmutableArray<QualifiedClaimGroup> claims) =>
        !claims.IsDefault && claims.All(claim => claim is not null && !string.IsNullOrWhiteSpace(claim.ClaimGroupId) &&
            ExplorePresentationText.IsWellFormed(claim.Text) && Enum.IsDefined(claim.Qualification) && !claim.Evidence.IsDefault &&
            claim.Evidence.All(evidence => evidence is not null && (evidence.Source is null || ValidSource(evidence.Source))) &&
            !claim.Limitations.IsDefault && claim.Limitations.All(ExplorePresentationText.IsWellFormed));

    private static bool ValidCoverage(CoverageProjection? coverage) => coverage is null ||
        (!coverage.PermittedGaps.IsDefault && coverage.PermittedGaps.All(ExplorePresentationText.IsWellFormed));

    private static bool ValidSource(SourceAnchor source) => source.Origin is not null &&
        ExplorePresentationText.IsWellFormed(source.Origin.RelativePath) &&
        !string.IsNullOrWhiteSpace(source.Origin.RelativePath) &&
        !source.Origin.RelativePath.StartsWith('/') && !source.Origin.RelativePath.StartsWith('\\') &&
        !source.Origin.RelativePath.Contains(':') &&
        !source.Origin.RelativePath.Split('/', '\\').Any(segment => segment is ".." or ".") &&
        source.StartLine > 0 && source.EndLine >= source.StartLine;

    private static bool ValidNext(ContinuationToken? next, ContinuationToken? used) =>
        next is null || (!string.IsNullOrWhiteSpace(next.Value) && next != used);

    private static ExploreReadResult ReadFailure(KnowledgeReadStatus status) => new(new(status, null, null, [], null, null), null);
    private static ExploreBrowseResult BrowseFailure(KnowledgeReadStatus status) => new(new(status, [], null, null), null);

    private void OnAuthenticationChanged(Task<AuthenticationState> _) => InvalidateContext();
    private void InvalidateContext()
    {
        Interlocked.Increment(ref _contextGeneration);
        ContextChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        _contextProvider.ContextChanged -= InvalidateContext;
        if (_authentication is not null) { _authentication.AuthenticationStateChanged -= OnAuthenticationChanged; }
        InvalidateContext();
    }

    private sealed class ReaderSession(ExplorePresentationService issuer, ExploreContext context, long generation,
        ReadSelection selection, DocumentHandle document, ContinuationToken next) : ExploreReadSession
    {
        internal ExplorePresentationService Issuer { get; } = issuer;
        internal ExploreContext Context { get; } = context;
        internal long Generation { get; } = generation;
        internal ReadSelection Selection { get; } = selection;
        internal DocumentHandle Document { get; } = document;
        internal ContinuationToken Next { get; } = next;
        internal int Consumed;
    }

    private sealed class BrowseSession(ExplorePresentationService issuer, ExploreContext context, long generation,
        ReadSelection selection, ContinuationToken next, string titleQuery) : ExploreBrowseSession
    {
        internal ExplorePresentationService Issuer { get; } = issuer;
        internal ExploreContext Context { get; } = context;
        internal long Generation { get; } = generation;
        internal ReadSelection Selection { get; } = selection;
        internal ContinuationToken Next { get; } = next;
        internal string TitleQuery { get; } = titleQuery;
        internal int Consumed;
    }
}
