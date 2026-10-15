using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

public sealed record OctoberPublishedDocument(
    DocumentVersionReference Reference,
    DocumentKind Kind,
    string Title,
    string ContentJson,
    ImmutableArray<DocumentSection> Sections,
    ImmutableArray<Contributor> Contributors);

public sealed record OctoberPublishedCoverage(FileCoverage Coverage, ImmutableArray<Contributor> Contributors);

public sealed record OctoberPublishedUncertainty(
    Uncertainty Value,
    ImmutableArray<Contributor> Contributors,
    ImmutableArray<DocumentVersionReference> Owners);

public sealed record OctoberPublishedRelationship(AcceptedRelationship Relationship, ImmutableArray<Contributor> Contributors);

public sealed record OctoberPublishedFinalizationCoverage(CoverageProjection Coverage, ImmutableArray<Contributor> Contributors);

public sealed record OctoberPublishedKnowledge(
    PublicationBinding Publication,
    ImmutableArray<OctoberPublishedDocument> Documents,
    ImmutableArray<OctoberPublishedCoverage> Coverage,
    ImmutableArray<OctoberPublishedRelationship> Relationships,
    ImmutableArray<OctoberPublishedUncertainty> Uncertainties)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public OctoberPublishedFinalizationCoverage? FinalizationCoverage { get; init; }
}

/// <summary>Only exact immutable published membership; null never triggers a latest or legacy fallback.</summary>
public interface IOctoberPublishedKnowledgeSource
{
    Task<OctoberPublishedKnowledge?> LoadAsync(PublicationBinding publication, CancellationToken cancellationToken = default);
}

public interface IOctoberPublishedKnowledgeIndexSource : IOctoberPublishedKnowledgeSource
{
    Task<OctoberPublishedKnowledge?> LoadIndexAsync(PublicationBinding publication, CancellationToken cancellationToken = default);
    Task<OctoberPublishedDocument?> LoadDocumentAsync(PublicationBinding publication, OctoberPublishedDocument document, CancellationToken cancellationToken = default);
}

public sealed class OctoberKnowledgeReadBudgetException : Exception
{
    public OctoberKnowledgeReadBudgetException() : base("The bounded Knowledge read budget was exhausted.")
    {
    }
}

/// <summary>Bounded immutable read projections. Selection and cursor tokens never replace live authorization.</summary>
public sealed class OctoberKnowledgeReader : IOctoberKnowledgeReader, IOctoberKnowledgeDisclosedReader
{
    private const int MaximumDocuments = 10_000;
    private const int MaximumMaterializedBytes = 32 * 1024 * 1024;
    private readonly IOctoberPublishedKnowledgeSource _source;
    private readonly PublicationBinding? _configured;
    private readonly IOctoberKnowledgeDisclosurePolicy _policy;
    private readonly OctoberKnowledgeCursorProtector _cursors;
    private readonly IOctoberKnowledgeReadLog _log;
    private readonly OctoberKnowledgeDependencyAuthority? _dependencyAuthority;

    public OctoberKnowledgeReader(IOctoberPublishedKnowledgeSource source, PublicationBinding? configured,
        IOctoberKnowledgeDisclosurePolicy policy, OctoberKnowledgeCursorProtector cursors, IOctoberKnowledgeReadLog log)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _configured = configured;
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _cursors = cursors ?? throw new ArgumentNullException(nameof(cursors));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public OctoberKnowledgeReader(IOctoberPublishedKnowledgeSource source, PublicationBinding? configured,
        IOctoberKnowledgeDisclosurePolicy policy, OctoberKnowledgeCursorProtector cursors, IOctoberKnowledgeReadLog log,
        OctoberKnowledgeDependencyAuthority dependencyAuthority) : this(source, configured, policy, cursors, log)
    {
        _dependencyAuthority = dependencyAuthority ?? throw new ArgumentNullException(nameof(dependencyAuthority));
    }

    public Task<KnowledgeDisclosureReadResult<SearchPage>> SearchAsync(KnowledgeSearchRequest request,
        KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
        DisclosedAsync(budget, token => SearchCoreAsync(request, budget, token), KnowledgeReadOperation.Search, cancellationToken);

    public Task<KnowledgeDisclosureReadResult<ContentPage>> ReadExactAsync(ExactReadRequest request,
        KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
        DisclosedAsync(budget, token => ExactCoreAsync(request, budget, token), KnowledgeReadOperation.ReadExact, cancellationToken);

    public Task<KnowledgeDisclosureReadResult<RelatedPage>> GetRelatedAsync(RelatedReadRequest request,
        KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
        DisclosedAsync(budget, token => RelatedCoreAsync(request, budget, token), KnowledgeReadOperation.GetRelated, cancellationToken);

    public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken = default) => RunAsync(async deadlineToken =>
    {
        ValidateSelection(request);
        await RequireAsync(new(request, [], null), deadlineToken);
        var source = await LoadAsync(request.Publication, deadlineToken);
        var context = new ReadContext(new(request, _cursors.Now + KnowledgeContract.ContinuationLifetime, "opening"), source);
        await FinishAsync(context, KnowledgeReadOperation.OpenSelection, deadlineToken);
        return new ReadSelectionResult(KnowledgeReadStatus.Success, _cursors.Issue(request));
    }, status => new ReadSelectionResult(status, null), cancellationToken);

    public Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        var result = await SearchCoreAsync(request, null, token);
        await FinishAsync(result.Context, KnowledgeReadOperation.Search, token);
        return result.Page;
    }, status => new SearchPage(status, [], null, null), cancellationToken);

    public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        var result = await ExactCoreAsync(request, null, token);
        await FinishAsync(result.Context, KnowledgeReadOperation.ReadExact, token);
        return result.Page;
    }, status => new ContentPage(status, null, null, [], null, null), cancellationToken);

    public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        var result = await RelatedCoreAsync(request, null, token);
        await FinishAsync(result.Context, KnowledgeReadOperation.GetRelated, token);
        return result.Page;
    }, status => new RelatedPage(status, [], [], null, null), cancellationToken);

    private Task<KnowledgeDisclosureReadResult<T>> DisclosedAsync<T>(KnowledgeReadOutputBudget budget,
        Func<CancellationToken, Task<ReadMaterialization<T>>> materialize, KnowledgeReadOperation operation, CancellationToken caller) where T : class =>
        RunAsync(async token =>
        {
            if (_dependencyAuthority is null) throw new ReadFailure(KnowledgeReadStatus.UnavailableService);
            if (budget?.Encoder is null || budget.MaximumResultUtf8Bytes <= 0 || budget.RemainingAggregateUtf8Bytes <= 0)
                throw new OctoberKnowledgeReadBudgetException();
            string outputProfile = budget.Encoder.ProfileId;
            if (string.IsNullOrWhiteSpace(outputProfile)) throw new OctoberKnowledgeReadBudgetException();
            OctoberKnowledgeDisclosureCodec.LimitText(outputProfile, 1024);
            var result = await materialize(token);
            if (!string.Equals(outputProfile, budget.Encoder.ProfileId, StringComparison.Ordinal))
                throw new ReadFailure(KnowledgeReadStatus.UnavailableService);
            token.ThrowIfCancellationRequested();
            string pageDigest = OctoberKnowledgeDisclosureCodec.Hash("october.knowledge.page.v1\n" + typeof(T).Name + "\n" +
                                                                     OctoberKnowledgeDisclosureCodec.Canonical(result.Page));
            string outputDigest = OctoberKnowledgeDisclosureCodec.Hash(result.Output.AsSpan());
            string kind = operation switch { KnowledgeReadOperation.Search => "search", KnowledgeReadOperation.ReadExact => "content", _ => "related" };
            var dependencies = _dependencyAuthority.Issue(result.Context.Ticket.Selection, kind, pageDigest,
                outputProfile, outputDigest, result.Context.Disclosures, result.Content);
            // Protection/encoding can take time. The final post-log authorization is after both, not before them.
            await FinishAsync(result.Context, operation, token);
            return new KnowledgeDisclosureReadResult<T>(KnowledgeReadStatus.Success,
                new(result.Page, dependencies, pageDigest, outputProfile, outputDigest, result.Output));
        }, status => new KnowledgeDisclosureReadResult<T>(status, null), caller);

    private async Task<ReadMaterialization<SearchPage>> SearchCoreAsync(KnowledgeSearchRequest request,
        KnowledgeReadOutputBudget? budget, CancellationToken token)
    {
        var context = await ContextAsync(request.Selection, token);
        string query = NormalizeQuery(request.Query);
        ValidatePageSize(request.PageSize);
        var visible = await VisibleAsync(context, token);
        var matched = new List<OctoberPublishedDocument>();
        foreach (var item in visible)
        {
            if (query.Length == 0 || Matches(await ContentAsync(context, item, token), context.Ticket.Selection.Mode, query)) matched.Add(item);
        }

        var matches = matched.ToArray();
        string order = OrderDigest(matches.Select(document => Key(document.Reference)));
        int offset = Position(request.Selection, context.Ticket, request.Continuation, "search", query, request.PageSize, null, order, matches.Length);
        var coverage = await CoverageAsync(context, visible.Count != 0, token);
        int take = Math.Min(request.PageSize, matches.Length - offset);
        var selectedContent = new List<DocumentSummary>();
        foreach (var item in matches.Skip(offset).Take(take)) selectedContent.Add(Summary(context, await ContentAsync(context, item, token), null, true));
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var documents = selectedContent.Take(take).ToImmutableArray();
            int next = offset + documents.Length;
            var continuation = next < matches.Length ? Next(request.Selection, context.Ticket, "search", query, request.PageSize, null, order, next) : null;
            var page = new SearchPage(KnowledgeReadStatus.Success, documents, continuation, coverage);
            if (TryEncode(page, budget, token, out var output)) return new(page, context, null, output);
            if (take <= 1) throw new OctoberKnowledgeReadBudgetException();
            take /= 2;
        }
    }

    private async Task<ReadMaterialization<ContentPage>> ExactCoreAsync(ExactReadRequest request,
        KnowledgeReadOutputBudget? budget, CancellationToken token)
    {
        var context = await ContextAsync(request.Selection, token);
        var candidate = Exact(context, context.Source.Documents, request.Document with { SectionId = null });
        var visible = await VisibleAsync(context, token, [candidate]);
        var permitted = Exact(context, visible, request.Document with { SectionId = null });
        var document = Exact(context, [await ContentAsync(context, permitted, token)], request.Document);
        var projection = Project(document, context.Ticket.Selection.Mode, request.Document.SectionId);
        string order = OctoberKnowledgeDisclosureCodec.Hash(projection.Text);
        int offset = Position(request.Selection, context.Ticket, request.Continuation, "exact", "", 0, request.Document, order, projection.Text.Length);
        var coverage = await CoverageAsync(context, true, token);
        int end = Utf8End(projection.Text, offset, KnowledgeContract.MaximumContentBytes / 2);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var claims = projection.Spans.Where(span => span.End > offset && span.Start < end)
                .Select(span => span.Claim with { Text = projection.Text[Math.Max(offset, span.Start)..Math.Min(end, span.End)] }).ToImmutableArray();
            var continuation = end < projection.Text.Length ? Next(request.Selection, context.Ticket, "exact", "", 0, request.Document, order, end) : null;
            string content = projection.Text[offset..end];
            var page = new ContentPage(KnowledgeReadStatus.Success, Summary(context, document, request.Document.SectionId, false), content, claims, continuation, coverage);
            if (TryEncode(page, budget, token, out var output))
                return new(page, context, new(request.Document, order, offset, end, projection.Text.Length,
                    OctoberKnowledgeDisclosureCodec.Hash(content), OctoberKnowledgeDisclosureCodec.StrictUtf8.GetByteCount(content), coverage), output);
            int reduced = SafeEnd(projection.Text, offset + (end - offset) / 2);
            if (reduced <= offset) throw new OctoberKnowledgeReadBudgetException();
            end = reduced;
        }
    }

    private async Task<ReadMaterialization<RelatedPage>> RelatedCoreAsync(RelatedReadRequest request,
        KnowledgeReadOutputBudget? budget, CancellationToken token)
    {
        var context = await ContextAsync(request.Selection, token);
        ValidatePageSize(request.PageSize);
        var candidate = Exact(context, context.Source.Documents, request.Document with { SectionId = null });
        var endpoints = context.Source.Relationships.Select(item => item.Relationship)
            .Where(edge => edge.From == candidate.Reference || edge.To == candidate.Reference)
            .SelectMany(edge => new[] { edge.From, edge.To }).Append(candidate.Reference).ToHashSet();
        var visible = await VisibleAsync(context, token, context.Source.Documents.Where(item => endpoints.Contains(item.Reference)));
        var document = Exact(context, visible, request.Document with { SectionId = null });
        if (request.Document.SectionId is not null)
            document = Exact(context, [await ContentAsync(context, document, token)], request.Document);
        var references = visible.Select(item => item.Reference).ToHashSet();
        var items = new List<RelatedItem>();
        foreach (var item in context.Source.Relationships)
        {
            var edge = item.Relationship;
            if ((edge.From != document.Reference && edge.To != document.Reference) || !references.Contains(edge.From) || !references.Contains(edge.To)) continue;
            if (await PermitAsync(context, item.Contributors, document.Kind, token))
                items.Add(new("edge/" + edge.RelationshipVersionId.ToString("D"), edge, null));
        }

        if (context.Ticket.Selection.Mode == KnowledgeMode.IT)
        {
            foreach (var item in context.Source.Uncertainties)
            {
                if (item.Owners.IsDefaultOrEmpty || !item.Owners.Contains(document.Reference)) continue;
                if (await PermitAsync(context, item.Contributors, document.Kind, token))
                {
                    string ownerKey = string.Join("/", item.Owners.Select(Key).Order(StringComparer.Ordinal));
                    items.Add(new("uncertainty/" + ownerKey + "/" + item.Value.Key, null, item.Value));
                }
            }
        }

        items.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
        string order = OrderDigest(items.Select(item => item.Key));
        int offset = Position(request.Selection, context.Ticket, request.Continuation, "related", "", request.PageSize, request.Document, order, items.Count);
        var coverage = await CoverageAsync(context, true, token);
        int take = Math.Min(request.PageSize, items.Count - offset);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var selected = items.Skip(offset).Take(take).ToArray();
            var relationships = selected.Where(item => item.Relationship is not null).Select(item => item.Relationship!)
                .Select(edge => new RelatedDocument(edge.Kind, Handle(context, edge.From), Handle(context, edge.To),
                        context.Ticket.Selection.Mode == KnowledgeMode.Business
                            ? (edge.Kind == RelationshipKind.GeneratedFrom ? "业务逻辑来源" : "业务逻辑引用") : edge.Meaning)
                    { RelationshipVersionId = edge.RelationshipVersionId }).ToImmutableArray();
            var uncertainties = selected.Where(item => item.Uncertainty is not null).Select(item => item.Uncertainty!).ToImmutableArray();
            int next = offset + selected.Length;
            var continuation = next < items.Count ? Next(request.Selection, context.Ticket, "related", "", request.PageSize, request.Document, order, next) : null;
            var page = new RelatedPage(KnowledgeReadStatus.Success, relationships, uncertainties, continuation, coverage);
            if (TryEncode(page, budget, token, out var output)) return new(page, context, null, output);
            if (take <= 1) throw new OctoberKnowledgeReadBudgetException();
            take /= 2;
        }
    }

    private static bool TryEncode<T>(T page, KnowledgeReadOutputBudget? budget, CancellationToken token, out ImmutableArray<byte> output)
    {
        output = [];
        if (!WithinBudget(page, KnowledgeContract.MaximumContentBytes)) return false;
        if (budget is null) return true;
        token.ThrowIfCancellationRequested();
        output = page switch
        {
            SearchPage search => budget.Encoder.EncodeSearch(search),
            ContentPage content => budget.Encoder.EncodeContent(content),
            RelatedPage related => budget.Encoder.EncodeRelated(related),
            _ => throw new ReadFailure(KnowledgeReadStatus.UnavailableService),
        };
        token.ThrowIfCancellationRequested();
        if (output.IsDefaultOrEmpty || output.Length > KnowledgeContract.MaximumContentBytes ||
            output.Length > budget.MaximumResultUtf8Bytes || output.Length > budget.RemainingAggregateUtf8Bytes) return false;
        string json = OctoberKnowledgeDisclosureCodec.StrictUtf8.GetString(output.AsSpan());
        using var parsed = JsonDocument.Parse(json);
        if (parsed.RootElement.ValueKind != JsonValueKind.Object) throw new ReadFailure(KnowledgeReadStatus.UnavailableService);
        return true;
    }

    private void ValidateSelection(ReadSelectionRequest request)
    {
        if (request is null || _configured is null || request.Publication != _configured || request.Subject is null ||
            string.IsNullOrWhiteSpace(request.Subject.SubjectId) || string.IsNullOrWhiteSpace(request.Subject.AuthenticationBinding) ||
            !Enum.IsDefined(request.Mode) || !Enum.IsDefined(request.Purpose) || request.RepositoryScope.IsDefault ||
            request.RepositoryScope.Any(id => id == Guid.Empty) || request.RepositoryScope.Distinct().Count() != request.RepositoryScope.Length)
            throw new ReadFailure(KnowledgeReadStatus.Unavailable);
    }

    private async Task<ReadContext> ContextAsync(ReadSelection selection, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ticket = _cursors.Read(selection);
        ValidateSelection(ticket.Selection);
        await RequireAsync(new(ticket.Selection, [], null), cancellationToken);
        return new(ticket, await LoadAsync(ticket.Selection.Publication, cancellationToken));
    }

    private async Task<OctoberPublishedKnowledge> LoadAsync(PublicationBinding publication, CancellationToken cancellationToken)
    {
        var source = await (_source is IOctoberPublishedKnowledgeIndexSource index
            ? index.LoadIndexAsync(publication, cancellationToken)
            : _source.LoadAsync(publication, cancellationToken)).WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (source is null || source.Publication != publication) throw new ReadFailure(KnowledgeReadStatus.Unavailable);
        if (source.Documents.IsDefault || source.Coverage.IsDefault || source.Relationships.IsDefault || source.Uncertainties.IsDefault)
            throw new ReadFailure(KnowledgeReadStatus.UnavailableService);
        if (source.Documents.Length > MaximumDocuments || !WithinBudget(source, MaximumMaterializedBytes)) throw new OctoberKnowledgeReadBudgetException();
        if (source.Documents.Any(document => document.Reference.BatchId != publication.Knowledge.BatchId ||
                                             !Enum.IsDefined(document.Kind) || document.Contributors.IsDefaultOrEmpty || document.Sections.IsDefault) ||
            source.Documents.Select(document => document.Reference.LogicalDocumentId).Distinct().Count() != source.Documents.Length)
            throw new ReadFailure(KnowledgeReadStatus.UnavailableService);
        return source;
    }

    private async Task<OctoberPublishedDocument> ContentAsync(ReadContext context, OctoberPublishedDocument document, CancellationToken token)
    {
        if (_source is not IOctoberPublishedKnowledgeIndexSource index) return document;
        var loaded = await index.LoadDocumentAsync(context.Source.Publication, document, token).WaitAsync(token);
        if (loaded is null || loaded.Reference != document.Reference || loaded.Kind != document.Kind || loaded.Title != document.Title ||
            !loaded.Contributors.ToHashSet().SetEquals(document.Contributors) || loaded.Sections.IsDefault)
            throw new ReadFailure(KnowledgeReadStatus.UnavailableService);
        if (!WithinBudget(loaded, MaximumMaterializedBytes)) throw new OctoberKnowledgeReadBudgetException();
        return loaded;
    }

    private async Task<List<OctoberPublishedDocument>> VisibleAsync(ReadContext context, CancellationToken cancellationToken,
        IEnumerable<OctoberPublishedDocument>? candidates = null)
    {
        var result = new List<OctoberPublishedDocument>();
        if (context.Ticket.Selection.RepositoryScope.IsEmpty) return result;
        var groups = (candidates ?? context.Source.Documents)
            .Where(document => context.Ticket.Selection.Mode != KnowledgeMode.Business || document.Kind == DocumentKind.BusinessLogic)
            .GroupBy(document => document.Kind);
        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // The policy requires every contributor. Check their union once for this kind,
            // as the owner dependency capsules already do; never retain an access decision.
            var contributors = group.SelectMany(document => document.Contributors).Distinct().ToImmutableArray();
            if (await PermitAsync(context, contributors, group.Key, cancellationToken))
            {
                result.AddRange(group);
            }
            else if (group.Count() > 1)
            {
                // A denied union must not hide its individually permitted members.
                foreach (var document in group)
                {
                    if (await PermitAsync(context, document.Contributors, document.Kind, cancellationToken)) result.Add(document);
                }
            }
        }

        return result.OrderBy(document => document.Reference.LogicalDocumentId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(document => document.Reference.DocumentVersionId.ToString("D"), StringComparer.Ordinal).ToList();
    }

    private async Task<bool> PermitAsync(ReadContext context, ImmutableArray<Contributor> contributors, DocumentKind? kind, CancellationToken cancellationToken)
    {
        if (contributors.IsDefaultOrEmpty) return false;
        var request = new DisclosureRequest(context.Ticket.Selection, contributors, kind);
        var status = await _policy.CheckAsync(request, cancellationToken).WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (status == DisclosureStatus.AuthorityUnavailable) throw new ReadFailure(KnowledgeReadStatus.UnavailableService);
        if (status != DisclosureStatus.Allowed) return false;
        context.Disclosures.Add(request);
        return true;
    }

    private async Task RequireAsync(DisclosureRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var status = await _policy.CheckAsync(request, cancellationToken).WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (status != DisclosureStatus.Allowed) throw new ReadFailure(status == DisclosureStatus.Denied ? KnowledgeReadStatus.Unavailable : KnowledgeReadStatus.UnavailableService);
    }

    private async Task FinishAsync(ReadContext context, KnowledgeReadOperation operation, CancellationToken cancellationToken)
    {
        // Recheck after materialization, then log, then recheck again: a slow logger grants no stale disclosure.
        await RecheckAsync(context, cancellationToken);
        if (!await _log.AppendAsync(new(context.Ticket.Selection.Publication, Guid.NewGuid(), operation, KnowledgeReadStatus.Success), cancellationToken)
                .WaitAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new ReadFailure(KnowledgeReadStatus.UnavailableService);
        }

        await RecheckAsync(context, cancellationToken);
    }

    private async Task RecheckAsync(ReadContext context, CancellationToken cancellationToken)
    {
        if (context.Ticket.ExpiresAt <= _cursors.Now) throw new OctoberInvalidContinuationException();
        await RequireAsync(new(context.Ticket.Selection, [], null), cancellationToken);
        foreach (var group in OctoberKnowledgeDisclosureCodec.Groups(context.Disclosures))
            await RequireAsync(new(context.Ticket.Selection, group.Contributors, group.Kind), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<CoverageProjection> CoverageAsync(ReadContext context, bool hasVisibleDocument, CancellationToken cancellationToken)
    {
        if (!hasVisibleDocument || context.Ticket.Selection.RepositoryScope.IsEmpty) return new(false, []);
        var gaps = ImmutableArray.CreateBuilder<string>();
        if (context.Source.FinalizationCoverage is { Coverage.IsPartial: true } finalization &&
            await PermitAsync(context, finalization.Contributors, null, cancellationToken))
            gaps.Add("部分内容尚未完成。");
        foreach (var item in context.Source.Coverage)
        {
            if (item.Coverage.Disposition is CoverageDisposition.Accepted or CoverageDisposition.CompletedZeroUnit) continue;
            if (await PermitAsync(context, item.Contributors, null, cancellationToken))
                gaps.Add(context.Ticket.Selection.Mode == KnowledgeMode.Business ? "部分内容尚未完成。" :
                    $"{item.Coverage.Origin.RelativePath}: {item.Coverage.Disposition}{(string.IsNullOrWhiteSpace(item.Coverage.Reason) ? "" : ": " + item.Coverage.Reason)}");
        }

        // A Business projection must not encode the number of technical files as repeated placeholders.
        return new(gaps.Count != 0, context.Ticket.Selection.Mode == KnowledgeMode.Business && gaps.Count != 0 ? ["部分内容尚未完成。"] : gaps.ToImmutable());
    }

    private static OctoberPublishedDocument Exact(ReadContext context, IEnumerable<OctoberPublishedDocument> visible, DocumentHandle handle)
    {
        if (handle is null || handle.BatchId != context.Ticket.Selection.Publication.Knowledge.BatchId ||
            handle.PublicationId != context.Ticket.Selection.Publication.PublicationId) throw new ReadFailure(KnowledgeReadStatus.Unavailable);
        var document = visible.SingleOrDefault(item =>
            item.Reference.LogicalDocumentId == handle.LogicalDocumentId && item.Reference.DocumentVersionId == handle.DocumentVersionId);
        if (document is null || (handle.SectionId is not null && !document.Sections.Any(section => section.SectionId == handle.SectionId)))
            throw new ReadFailure(KnowledgeReadStatus.Unavailable);
        return document;
    }

    private static DocumentHandle Handle(ReadContext context, DocumentVersionReference reference, string? section = null) =>
        new(reference.BatchId, context.Ticket.Selection.Publication.PublicationId, reference.LogicalDocumentId, reference.DocumentVersionId, section);

    private static DocumentSummary Summary(ReadContext context, OctoberPublishedDocument document, string? section, bool includeExcerpt)
    {
        var excerpts = includeExcerpt ? document.Sections.OrderBy(item => item.Ordinal).SelectMany(item => item.Claims).Take(2)
            .Select(claim => ProjectClaim(claim, context.Ticket.Selection.Mode))
            .Select(claim => claim with
            {
                Text = Prefix(claim.Text, 256),
                Evidence = claim.Evidence.Take(2).ToImmutableArray(),
                Limitations = claim.Limitations.Take(2).Select(value => Prefix(value, 256)).ToImmutableArray()
            }).ToImmutableArray() : [];
        return new(Handle(context, document.Reference, section), document.Kind, document.Title, excerpts);
    }

    private static QualifiedClaimGroup ProjectClaim(QualifiedClaimGroup claim, KnowledgeMode mode) => mode == KnowledgeMode.Business ? claim with
    {
        ClaimGroupId = "claim-" + OctoberKnowledgeCursorProtector.Digest(claim.ClaimGroupId)[..16],
        Evidence = [], Limitations = claim.Limitations.IsDefaultOrEmpty ? [] : ["存在已记录的证据限制。"],
    } : claim;

    private static Projection Project(OctoberPublishedDocument document, KnowledgeMode mode, string? sectionId)
    {
        var text = new StringBuilder();
        var spans = ImmutableArray.CreateBuilder<ClaimSpan>();
        if (sectionId is null) text.Append(document.Title).Append('\n');
        foreach (var section in document.Sections.Where(section => sectionId is null || section.SectionId == sectionId).OrderBy(section => section.Ordinal))
        {
            text.Append(section.Title).Append('\n');
            foreach (var original in section.Claims)
            {
                var claim = ProjectClaim(original, mode);
                text.Append('[').Append(Label(claim.Qualification)).Append("] ");
                int start = text.Length;
                text.Append(claim.Text);
                spans.Add(new(start, text.Length, claim));
                text.Append('\n');
                foreach (string limitation in claim.Limitations) text.Append("限制：").Append(limitation).Append('\n');
            }
        }

        return new(text.ToString(), spans.ToImmutable());
    }

    private static string Label(ClaimQualification qualification) => qualification switch
    {
        ClaimQualification.SourceSupported => "来源支持", ClaimQualification.Advisory => "建议性上下文",
        ClaimQualification.Hypothesis => "假设", ClaimQualification.Uncertain => "不确定", ClaimQualification.Mixed => "混合依据",
        _ => throw new ReadFailure(KnowledgeReadStatus.UnavailableService),
    };

    private static bool Matches(OctoberPublishedDocument document, KnowledgeMode mode, string query)
    {
        if (query.Length == 0) return true;
        string text = Normalize(Project(document, mode, null).Text);
        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(term => text.Contains(term, StringComparison.Ordinal));
    }

    private static string NormalizeQuery(string? query)
    {
        if (query is null) throw new ReadFailure(KnowledgeReadStatus.Unavailable);
        int count = 0;
        var remaining = query.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out _, out int consumed) != System.Buffers.OperationStatus.Done || ++count > KnowledgeContract.MaximumQueryCharacters)
                throw new ReadFailure(KnowledgeReadStatus.Unavailable);
            remaining = remaining[consumed..];
        }

        return Normalize(query);
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Normalize(NormalizationForm.FormC).ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static void ValidatePageSize(int size)
    {
        if (size < 1 || size > KnowledgeContract.MaximumPageSize) throw new ReadFailure(KnowledgeReadStatus.Unavailable);
    }

    private int Position(ReadSelection selection, OctoberReadTicket ticket, ContinuationToken? token, string operation, string query,
        int pageSize, DocumentHandle? handle, string order, int length)
    {
        if (token is null) return 0;
        var position = _cursors.Read(token);
        if (position.SelectionDigest != OctoberKnowledgeCursorProtector.Digest(selection.Token) || position.Operation != operation ||
            position.Query != query || position.PageSize != pageSize || position.Handle != handle || position.OrderDigest != order ||
            position.ExpiresAt != ticket.ExpiresAt || position.Offset >= length) throw new OctoberInvalidContinuationException();
        return position.Offset;
    }

    private ContinuationToken Next(ReadSelection selection, OctoberReadTicket ticket, string operation, string query, int pageSize,
        DocumentHandle? handle, string order, int offset) => _cursors.Issue(new OctoberReadPosition(OctoberKnowledgeCursorProtector.Digest(selection.Token),
        operation, query, pageSize, handle, order, offset, ticket.ExpiresAt));

    private static string Key(DocumentVersionReference reference) => $"{reference.LogicalDocumentId:D}/{reference.DocumentVersionId:D}";
    private static string OrderDigest(IEnumerable<string> values) => OctoberKnowledgeCursorProtector.Digest(string.Join('\n', values));

    private static string Prefix(string value, int maximumRunes)
    {
        int offset = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (maximumRunes-- == 0) break;
            offset += rune.Utf16SequenceLength;
        }

        return value[..offset];
    }

    private static int Utf8End(string value, int start, int maximumBytes)
    {
        int offset = start;
        foreach (var rune in value.AsSpan(start).EnumerateRunes())
        {
            if (rune.Utf8SequenceLength > maximumBytes) break;
            maximumBytes -= rune.Utf8SequenceLength;
            offset += rune.Utf16SequenceLength;
        }

        return offset;
    }

    private static int SafeEnd(string value, int end) => end > 0 && end < value.Length && char.IsHighSurrogate(value[end - 1]) && char.IsLowSurrogate(value[end]) ? end - 1 : end;

    private static void EnsureResponseBudget<T>(T value)
    {
        if (!WithinBudget(value, KnowledgeContract.MaximumContentBytes)) throw new OctoberKnowledgeReadBudgetException();
    }

    private static bool WithinBudget<T>(T value, int maximum)
    {
        using var counter = new BudgetStream(maximum);
        try
        {
            JsonSerializer.Serialize(counter, value);
            return true;
        }
        catch (OctoberKnowledgeReadBudgetException)
        {
            return false;
        }
    }

    private static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> execute, Func<KnowledgeReadStatus, T> failure, CancellationToken callerCancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(callerCancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            return await execute(deadline.Token).WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            return failure(callerCancellation.IsCancellationRequested ? KnowledgeReadStatus.Cancelled : KnowledgeReadStatus.UnavailableService);
        }
        catch (OctoberInvalidContinuationException)
        {
            return failure(KnowledgeReadStatus.InvalidContinuation);
        }
        catch (OctoberKnowledgeReadBudgetException)
        {
            return failure(KnowledgeReadStatus.ReadBudgetExceeded);
        }
        catch (DisclosureFailure exception)
        {
            return failure(exception.Status == KnowledgeDependencyStatus.BudgetExceeded ? KnowledgeReadStatus.ReadBudgetExceeded : KnowledgeReadStatus.UnavailableService);
        }
        catch (ReadFailure exception)
        {
            return failure(exception.Status);
        }
        catch (Exception)
        {
            return failure(KnowledgeReadStatus.UnavailableService);
        }
    }

    private sealed class ReadContext(OctoberReadTicket ticket, OctoberPublishedKnowledge source)
    {
        public OctoberReadTicket Ticket { get; } = ticket;
        public OctoberPublishedKnowledge Source { get; } = source;
        public List<DisclosureRequest> Disclosures { get; } = [];
    }

    private sealed class ReadFailure(KnowledgeReadStatus status) : Exception
    {
        public KnowledgeReadStatus Status { get; } = status;
    }

    private sealed record ReadMaterialization<T>(T Page, ReadContext Context, DisclosureContent? Content, ImmutableArray<byte> Output);

    private sealed record RelatedItem(string Key, AcceptedRelationship? Relationship, Uncertainty? Uncertainty);

    private sealed record ClaimSpan(int Start, int End, QualifiedClaimGroup Claim);

    private sealed record Projection(string Text, ImmutableArray<ClaimSpan> Spans);

    private sealed class BudgetStream(int maximum) : Stream
    {
        private long _length;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;

        public override long Position
        {
            get => _length;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) => Add(count);
        public override void Write(ReadOnlySpan<byte> buffer) => Add(buffer.Length);

        private void Add(int count)
        {
            _length += count;
            if (_length > maximum) throw new OctoberKnowledgeReadBudgetException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
