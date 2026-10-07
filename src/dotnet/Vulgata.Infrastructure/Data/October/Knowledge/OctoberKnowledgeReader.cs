using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

public sealed record OctoberPublishedDocument(DocumentVersionReference Reference, DocumentKind Kind, string Title,
    string ContentJson, ImmutableArray<DocumentSection> Sections, ImmutableArray<Contributor> Contributors);
public sealed record OctoberPublishedCoverage(FileCoverage Coverage, ImmutableArray<Contributor> Contributors);
public sealed record OctoberPublishedUncertainty(Uncertainty Value, ImmutableArray<Contributor> Contributors,
    ImmutableArray<DocumentVersionReference> Owners);
public sealed record OctoberPublishedRelationship(AcceptedRelationship Relationship, ImmutableArray<Contributor> Contributors);
public sealed record OctoberPublishedKnowledge(PublicationBinding Publication,
    ImmutableArray<OctoberPublishedDocument> Documents, ImmutableArray<OctoberPublishedCoverage> Coverage,
    ImmutableArray<OctoberPublishedRelationship> Relationships, ImmutableArray<OctoberPublishedUncertainty> Uncertainties);

/// <summary>Only exact immutable published membership; null never triggers a latest or legacy fallback.</summary>
public interface IOctoberPublishedKnowledgeSource
{
    Task<OctoberPublishedKnowledge?> LoadAsync(PublicationBinding publication, CancellationToken cancellationToken = default);
}
public sealed class OctoberKnowledgeReadBudgetException : Exception
{
    public OctoberKnowledgeReadBudgetException() : base("The bounded Knowledge read budget was exhausted.") { }
}

/// <summary>Bounded immutable read projections. Selection and cursor tokens never replace live authorization.</summary>
public sealed class OctoberKnowledgeReader : IOctoberKnowledgeReader
{
    private const int MaximumDocuments = 10_000;
    private const int MaximumMaterializedBytes = 32 * 1024 * 1024;
    private readonly IOctoberPublishedKnowledgeSource _source;
    private readonly PublicationBinding? _configured;
    private readonly IOctoberKnowledgeDisclosurePolicy _policy;
    private readonly OctoberKnowledgeCursorProtector _cursors;
    private readonly IOctoberKnowledgeReadLog _log;

    public OctoberKnowledgeReader(IOctoberPublishedKnowledgeSource source, PublicationBinding? configured,
        IOctoberKnowledgeDisclosurePolicy policy, OctoberKnowledgeCursorProtector cursors, IOctoberKnowledgeReadLog log)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _configured = configured;
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _cursors = cursors ?? throw new ArgumentNullException(nameof(cursors));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken = default) => RunAsync(async deadlineToken =>
    {
        ValidateSelection(request);
        await RequireAsync(new(request, [], null), deadlineToken);
        var source = await LoadAsync(request.Publication, deadlineToken);
        var context = new ReadContext(new(request, _cursors.Now + KnowledgeContract.ContinuationLifetime, "opening"), source);
        await FinishAsync(context, KnowledgeReadOperation.OpenSelection, deadlineToken);
        return new ReadSelectionResult(KnowledgeReadStatus.Success, _cursors.Issue(request));
    }, status => new ReadSelectionResult(status, null), cancellationToken);

    public Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default) => RunAsync(async deadlineToken =>
    {
        var context = await ContextAsync(request.Selection, deadlineToken);
        string query = NormalizeQuery(request.Query);
        ValidatePageSize(request.PageSize);
        var visible = await VisibleAsync(context, deadlineToken);
        var matches = visible.Where(document => Matches(document, context.Ticket.Selection.Mode, query)).ToArray();
        string order = OrderDigest(matches.Select(document => Key(document.Reference)));
        int offset = Position(request.Selection, context.Ticket, request.Continuation, "search", query, request.PageSize, null, order, matches.Length);
        var documents = matches.Skip(offset).Take(request.PageSize).Select(document => Summary(context, document, null, true)).ToImmutableArray();
        int next = offset + documents.Length;
        var continuation = next < matches.Length ? Next(request.Selection, context.Ticket, "search", query, request.PageSize, null, order, next) : null;
        var coverage = await CoverageAsync(context, visible.Count != 0, deadlineToken);
        var result = new SearchPage(KnowledgeReadStatus.Success, documents, continuation, coverage);
        EnsureResponseBudget(result);
        await FinishAsync(context, KnowledgeReadOperation.Search, deadlineToken);
        return result;
    }, status => new SearchPage(status, [], null, null), cancellationToken);

    public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken = default) => RunAsync(async deadlineToken =>
    {
        var context = await ContextAsync(request.Selection, deadlineToken);
        var visible = await VisibleAsync(context, deadlineToken);
        var document = Exact(context, visible, request.Document);
        var projection = Project(document, context.Ticket.Selection.Mode, request.Document.SectionId);
        string order = OctoberKnowledgeCursorProtector.Digest(projection.Text);
        int offset = Position(request.Selection, context.Ticket, request.Continuation, "exact", "", 0, request.Document, order, projection.Text.Length);
        var coverage = await CoverageAsync(context, true, deadlineToken);
        int end = Utf8End(projection.Text, offset, KnowledgeContract.MaximumContentBytes / 2);
        ContentPage page;
        while (true)
        {
            var claims = projection.Spans.Where(span => span.End > offset && span.Start < end)
                .Select(span => span.Claim with { Text = projection.Text[Math.Max(offset, span.Start)..Math.Min(end, span.End)] }).ToImmutableArray();
            var continuation = end < projection.Text.Length ? Next(request.Selection, context.Ticket, "exact", "", 0, request.Document, order, end) : null;
            page = new(KnowledgeReadStatus.Success, Summary(context, document, request.Document.SectionId, false),
                projection.Text[offset..end], claims, continuation, coverage);
            if (WithinBudget(page, KnowledgeContract.MaximumContentBytes)) break;
            int reduced = SafeEnd(projection.Text, offset + (end - offset) / 2);
            if (reduced <= offset) throw new OctoberKnowledgeReadBudgetException();
            end = reduced;
        }
        await FinishAsync(context, KnowledgeReadOperation.ReadExact, deadlineToken);
        return page;
    }, status => new ContentPage(status, null, null, [], null, null), cancellationToken);

    public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken = default) => RunAsync(async deadlineToken =>
    {
        var context = await ContextAsync(request.Selection, deadlineToken);
        ValidatePageSize(request.PageSize);
        var visible = await VisibleAsync(context, deadlineToken);
        var document = Exact(context, visible, request.Document);
        var references = visible.Select(item => item.Reference).ToHashSet();
        var items = new List<RelatedItem>();
        foreach (var item in context.Source.Relationships)
        {
            var edge = item.Relationship;
            if ((edge.From != document.Reference && edge.To != document.Reference) || !references.Contains(edge.From) || !references.Contains(edge.To)) continue;
            if (await PermitAsync(context, item.Contributors, document.Kind, deadlineToken))
                items.Add(new("edge/" + edge.RelationshipVersionId.ToString("D"), edge, null));
        }
        if (context.Ticket.Selection.Mode == KnowledgeMode.IT)
        {
            foreach (var item in context.Source.Uncertainties)
            {
                // Manifest ownership is distinct from cited evidence. A cited neighbor does not own the uncertainty.
                if (item.Owners.IsDefaultOrEmpty || !item.Owners.Contains(document.Reference)) continue;
                if (await PermitAsync(context, item.Contributors, null, deadlineToken))
                {
                    string ownerKey = string.Join("/", item.Owners.Select(Key).Order(StringComparer.Ordinal));
                    items.Add(new("uncertainty/" + ownerKey + "/" + item.Value.Key, null, item.Value));
                }
            }
        }
        items.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
        string order = OrderDigest(items.Select(item => item.Key));
        int offset = Position(request.Selection, context.Ticket, request.Continuation, "related", "", request.PageSize, request.Document, order, items.Count);
        var selected = items.Skip(offset).Take(request.PageSize).ToArray();
        var results = selected.Where(item => item.Relationship is not null).Select(item => item.Relationship!)
            .Select(edge => new RelatedDocument(edge.Kind, Handle(context, edge.From), Handle(context, edge.To),
                context.Ticket.Selection.Mode == KnowledgeMode.Business
                    ? (edge.Kind == RelationshipKind.GeneratedFrom ? "业务逻辑来源" : "业务逻辑引用")
                    : edge.Meaning)).ToImmutableArray();
        var uncertainties = selected.Where(item => item.Uncertainty is not null).Select(item => item.Uncertainty!).ToImmutableArray();
        int next = offset + selected.Length;
        var continuation = next < items.Count ? Next(request.Selection, context.Ticket, "related", "", request.PageSize, request.Document, order, next) : null;
        var page = new RelatedPage(KnowledgeReadStatus.Success, results, uncertainties, continuation,
            await CoverageAsync(context, true, deadlineToken));
        EnsureResponseBudget(page);
        await FinishAsync(context, KnowledgeReadOperation.GetRelated, deadlineToken);
        return page;
    }, status => new RelatedPage(status, [], [], null, null), cancellationToken);

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
        var source = await _source.LoadAsync(publication, cancellationToken).WaitAsync(cancellationToken);
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

    private async Task<List<OctoberPublishedDocument>> VisibleAsync(ReadContext context, CancellationToken cancellationToken)
    {
        var result = new List<OctoberPublishedDocument>();
        if (context.Ticket.Selection.RepositoryScope.IsEmpty) return result;
        foreach (var document in context.Source.Documents.OrderBy(document => document.Reference.LogicalDocumentId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(document => document.Reference.DocumentVersionId.ToString("D"), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Ticket.Selection.Mode == KnowledgeMode.Business && document.Kind != DocumentKind.BusinessLogic) continue;
            if (await PermitAsync(context, document.Contributors, document.Kind, cancellationToken)) result.Add(document);
        }
        return result;
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
        if (!await _log.AppendAsync(new(context.Ticket.Selection.Publication, Guid.NewGuid(), operation, KnowledgeReadStatus.Success), cancellationToken).WaitAsync(cancellationToken))
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
        foreach (var disclosure in context.Disclosures) await RequireAsync(disclosure, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<CoverageProjection> CoverageAsync(ReadContext context, bool hasVisibleDocument, CancellationToken cancellationToken)
    {
        if (!hasVisibleDocument || context.Ticket.Selection.RepositoryScope.IsEmpty) return new(false, []);
        var gaps = ImmutableArray.CreateBuilder<string>();
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

    private static OctoberPublishedDocument Exact(ReadContext context, List<OctoberPublishedDocument> visible, DocumentHandle handle)
    {
        if (handle is null || handle.BatchId != context.Ticket.Selection.Publication.Knowledge.BatchId ||
            handle.PublicationId != context.Ticket.Selection.Publication.PublicationId) throw new ReadFailure(KnowledgeReadStatus.Unavailable);
        var document = visible.SingleOrDefault(item => item.Reference.LogicalDocumentId == handle.LogicalDocumentId && item.Reference.DocumentVersionId == handle.DocumentVersionId);
        if (document is null || (handle.SectionId is not null && !document.Sections.Any(section => section.SectionId == handle.SectionId)))
            throw new ReadFailure(KnowledgeReadStatus.Unavailable);
        return document;
    }

    private static DocumentHandle Handle(ReadContext context, DocumentVersionReference reference, string? section = null) =>
        new(reference.BatchId, context.Ticket.Selection.Publication.PublicationId, reference.LogicalDocumentId, reference.DocumentVersionId, section);

    private static DocumentSummary Summary(ReadContext context, OctoberPublishedDocument document, string? section, bool includeExcerpt)
    {
        var excerpts = includeExcerpt ? document.Sections.OrderBy(item => item.Ordinal).SelectMany(item => item.Claims).Take(2)
            .Select(claim => ProjectClaim(claim, context.Ticket.Selection.Mode) with { Text = Prefix(claim.Text, 256) }).ToImmutableArray() : [];
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
    private static string Normalize(string value) => string.Join(' ', value.Normalize(NormalizationForm.FormC).ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
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
        try { JsonSerializer.Serialize(counter, value); return true; }
        catch (OctoberKnowledgeReadBudgetException) { return false; }
    }
    private static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> execute, Func<KnowledgeReadStatus, T> failure, CancellationToken callerCancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(callerCancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try { return await execute(deadline.Token).WaitAsync(deadline.Token); }
        catch (OperationCanceledException) { return failure(callerCancellation.IsCancellationRequested ? KnowledgeReadStatus.Cancelled : KnowledgeReadStatus.UnavailableService); }
        catch (OctoberInvalidContinuationException) { return failure(KnowledgeReadStatus.InvalidContinuation); }
        catch (OctoberKnowledgeReadBudgetException) { return failure(KnowledgeReadStatus.ReadBudgetExceeded); }
        catch (ReadFailure exception) { return failure(exception.Status); }
        catch (Exception) { return failure(KnowledgeReadStatus.UnavailableService); }
    }
    private sealed class ReadContext(OctoberReadTicket ticket, OctoberPublishedKnowledge source)
    {
        public OctoberReadTicket Ticket { get; } = ticket;
        public OctoberPublishedKnowledge Source { get; } = source;
        public List<DisclosureRequest> Disclosures { get; } = [];
    }
    private sealed class ReadFailure(KnowledgeReadStatus status) : Exception { public KnowledgeReadStatus Status { get; } = status; }
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
        public override long Position { get => _length; set => throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) => Add(count);
        public override void Write(ReadOnlySpan<byte> buffer) => Add(buffer.Length);
        private void Add(int count) { _length += count; if (_length > maximum) throw new OctoberKnowledgeReadBudgetException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
