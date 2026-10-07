using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberChat;
using Vulgata.Shared.OctoberKnowledge;

namespace Vulgata.Agents.ChatExecution.October;

internal sealed record OctoberChatToolInvocation(OctoberChatToolResult Result, ImmutableArray<byte> OutputUtf8);

/// <summary>
/// Controlled mapping seam only. No model/host registration until the Knowledge owner supplies
/// all-contributor disclosure dependencies and durable admission/composition prerequisites exist.
/// </summary>
public sealed class OctoberWikiTools
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly JsonSerializerOptions _outputJsonOptions = CreateOutputOptions();
    private readonly IOctoberKnowledgeReader _reader;
    private readonly IOctoberKnowledgeDisclosedReader? _disclosedReader;
    private readonly OctoberChatKnowledgeOutputEncoder _outputEncoder = new();
    private readonly OctoberChatTurnBinding _binding;
    private readonly OctoberChatBudget _budget;
    private readonly OctoberTurnReadLedger _ledger;

    internal OctoberChatBudget Budget => _budget;
    internal OctoberChatDisclosureSession? DisclosureSession { get; }

    internal OctoberWikiTools(IOctoberKnowledgeDisclosedReader reader, OctoberChatTurnBinding binding,
        OctoberChatBudget budget, OctoberChatDisclosureSession disclosure)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(disclosure);
        if (!ReferenceEquals(disclosure.Budget, budget) || disclosure.Binding != binding)
            throw new ArgumentException("The canonical facade requires its original binding and budget.", nameof(disclosure));
        _reader = null!;
        _disclosedReader = reader;
        _ledger = null!;
        _binding = binding;
        _budget = budget;
        DisclosureSession = disclosure;
    }

    internal async Task<OctoberChatToolInvocation> InvokeForModelAsync(string toolName, string arguments,
        CancellationToken cancellationToken = default)
    {
        if (DisclosureSession is null || _disclosedReader is null)
            throw new InvalidOperationException("Canonical Chat execution is unavailable.");
        int argumentBytes;
        try { argumentBytes = _strictUtf8.GetByteCount(arguments); }
        catch (ArgumentException) { argumentBytes = -1; }
        if (!_budget.TryStartToolCall(argumentBytes, out IDisposable? lease))
            return new(Result(OctoberChatToolStatus.BudgetExceeded), []);
        using (lease)
        using (var deadline = _budget.CreateDeadlineCancellation())
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _budget.OriginalCancellationToken, deadline.Token))
        {
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                object? parsed;
                try
                {
                    using JsonDocument input = JsonDocument.Parse(arguments, new JsonDocumentOptions { MaxDepth = 16 });
                    parsed = input.RootElement.ValueKind != JsonValueKind.Object || HasDuplicateProperties(input.RootElement) ? null : toolName switch
                    {
                        "SearchWiki" or "BrowseWiki" => input.RootElement.Deserialize<SearchArguments>(_jsonOptions),
                        "ReadWiki" => input.RootElement.Deserialize<ReadArguments>(_jsonOptions),
                        "GetRelatedDocuments" => input.RootElement.Deserialize<RelatedArguments>(_jsonOptions),
                        "ReportAnswerDocuments" => input.RootElement.Deserialize<ReportArguments>(_jsonOptions),
                        _ => null,
                    };
                }
                catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
                {
                    parsed = null;
                }

                int pageSize = Math.Min(_budget.Limits.PageSize, KnowledgeContract.DefaultPageSize);
                if (parsed is SearchArguments search)
                {
                    pageSize = search.PageSize ?? pageSize;
                    if (pageSize > _budget.Limits.PageSize || !new SearchKnowledgeRequestValidator().Validate(
                        new SearchKnowledgeRequest(_binding.Selection.Token, search.Query, pageSize, search.Continuation)).IsValid)
                        return await GenericAsync(OctoberChatToolStatus.InvalidArguments, linked.Token);
                    var response = await _disclosedReader.SearchAsync(new(_binding.Selection, search.Query, pageSize, Token(search.Continuation)),
                        OutputBudget(false), linked.Token).WaitAsync(linked.Token);
                    return await AcceptAsync(response, page => new(OctoberChatToolStatus.Succeeded, Search: page, References: []),
                        page => page.Status == KnowledgeReadStatus.Success && !page.Documents.IsDefault && page.Documents.Length <= pageSize &&
                            page.Documents.All(item => IsSelected(item.Handle) && (_binding.Request.Mode != KnowledgeMode.Business || item.Kind == DocumentKind.BusinessLogic)),
                        null, false, linked.Token);
                }
                if (parsed is ReadArguments read)
                {
                    if (read.Document is null || !new ReadKnowledgeRequestValidator().Validate(
                        new ReadKnowledgeRequest(_binding.Selection.Token, read.Document, read.Continuation)).IsValid)
                        return await GenericAsync(OctoberChatToolStatus.InvalidArguments, linked.Token);
                    if (!IsSelected(read.Document)) return await GenericAsync(OctoberChatToolStatus.Unavailable, linked.Token);
                    var response = await _disclosedReader.ReadExactAsync(new(_binding.Selection, read.Document, Token(read.Continuation)),
                        OutputBudget(true), linked.Token).WaitAsync(linked.Token);
                    return await AcceptAsync(response, page => new(OctoberChatToolStatus.Succeeded, Content: page, References: []),
                        page => page.Status == KnowledgeReadStatus.Success && page.Document?.Handle == read.Document && page.Content is not null &&
                            (_binding.Request.Mode != KnowledgeMode.Business || page.Document.Kind == DocumentKind.BusinessLogic),
                        read.Document, true, linked.Token);
                }
                if (parsed is RelatedArguments related)
                {
                    pageSize = related.PageSize ?? pageSize;
                    if (related.Document is null || pageSize > _budget.Limits.PageSize || !new RelatedKnowledgeRequestValidator().Validate(
                        new RelatedKnowledgeRequest(_binding.Selection.Token, related.Document, pageSize, related.Continuation)).IsValid)
                        return await GenericAsync(OctoberChatToolStatus.InvalidArguments, linked.Token);
                    if (!IsSelected(related.Document)) return await GenericAsync(OctoberChatToolStatus.Unavailable, linked.Token);
                    var response = await _disclosedReader.GetRelatedAsync(new(_binding.Selection, related.Document, pageSize, Token(related.Continuation)),
                        OutputBudget(false), linked.Token).WaitAsync(linked.Token);
                    return await AcceptAsync(response, page => new(OctoberChatToolStatus.Succeeded, Related: page, References: []),
                        page => page.Status == KnowledgeReadStatus.Success && !page.Relationships.IsDefault && page.Relationships.Length <= pageSize &&
                            page.Relationships.All(item => IsSelected(item.From) && IsSelected(item.To)), null, false, linked.Token);
                }
                if (parsed is ReportArguments report && report.Documents is not null && report.Documents.Length <= _budget.Limits.ToolCalls)
                {
                    OctoberChatToolInvocation? reported = await DisclosureSession.ReportEncodedAsync(report.Documents, linked.Token);
                    return reported ?? await GenericAsync(OctoberChatToolStatus.InvalidReport, linked.Token);
                }
                return await GenericAsync(OctoberChatToolStatus.InvalidArguments, linked.Token);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                _budget.StopExecution();
                return new(Result(cancellationToken.IsCancellationRequested || _budget.OriginalCancellationToken.IsCancellationRequested
                    ? OctoberChatToolStatus.Cancelled : OctoberChatToolStatus.BudgetExceeded), []);
            }
            catch (Exception)
            {
                // An explicit dependency failure already has an exact host-only cause.
                if (DisclosureSession.FailureStatus is not null) throw;
                throw DisclosureSession.Reject(KnowledgeDependencyStatus.AuthorityUnavailable);
            }
        }
    }

    private KnowledgeReadOutputBudget OutputBudget(bool content)
    {
        long remaining = _budget.Limits.ToolResultBytes - _budget.ToolResultBytes;
        if (remaining <= 0) throw DisclosureSession!.Reject(KnowledgeDependencyStatus.BudgetExceeded);
        int maximum = (int)Math.Min(int.MaxValue, remaining);
        if (content) maximum = Math.Min(maximum, Math.Min(_budget.Limits.ContentBytes, KnowledgeContract.MaximumContentBytes));
        return new(maximum, remaining, _outputEncoder);
    }

    private async Task<OctoberChatToolInvocation> AcceptAsync<T>(KnowledgeDisclosureReadResult<T> response,
        Func<T, OctoberChatToolResult> wrap, Func<T, bool> validPage, DocumentHandle? target, bool content, CancellationToken token) where T : class
    {
        token.ThrowIfCancellationRequested();
        if (response.Status != KnowledgeReadStatus.Success)
        {
            if (response.Disclosure is not null) throw DisclosureSession!.Reject(KnowledgeDependencyStatus.InvalidProof);
            if (response.Status is KnowledgeReadStatus.ReadBudgetExceeded or KnowledgeReadStatus.Cancelled)
            {
                _budget.StopExecution();
                return new(Result(response.Status == KnowledgeReadStatus.Cancelled ? OctoberChatToolStatus.Cancelled : OctoberChatToolStatus.BudgetExceeded), []);
            }
            return await GenericAsync(OctoberChatToolStatus.Unavailable, token);
        }
        KnowledgeDisclosedPage<T>? page = response.Disclosure;
        if (page?.Page is null || !OctoberChatDisclosureSession.ValidHandle(page.Dependencies) || page.OutputUtf8.IsDefaultOrEmpty ||
            page.OutputProfileId != _outputEncoder.ProfileId || !validPage(page.Page))
            throw DisclosureSession!.Reject(KnowledgeDependencyStatus.InvalidProof);
        OctoberChatToolResult result = wrap(page.Page);
        ImmutableArray<byte> expected = SerializeResultUtf8(result);
        if (page.PageDigest != OctoberKnowledgePolicy.Hash("october.knowledge.page.v1\n" + typeof(T).Name + "\n" + OctoberKnowledgePolicy.SerializeCanonical(page.Page)) ||
            page.OutputDigest != Convert.ToHexString(SHA256.HashData(page.OutputUtf8.AsSpan())) || !expected.AsSpan().SequenceEqual(page.OutputUtf8.AsSpan()))
            throw DisclosureSession!.Reject(KnowledgeDependencyStatus.InvalidProof);
        if (page.OutputUtf8.Length > _budget.Limits.ToolResultBytes - _budget.ToolResultBytes ||
            content && page.OutputUtf8.Length > Math.Min(_budget.Limits.ContentBytes, KnowledgeContract.MaximumContentBytes))
            throw DisclosureSession!.Reject(KnowledgeDependencyStatus.BudgetExceeded);
        await DisclosureSession!.AdmitEncodedPageAsync(page.Dependencies, target, page.OutputUtf8.Length, content, token);
        return new(result, page.OutputUtf8);
    }

    private async Task<OctoberChatToolInvocation> GenericAsync(OctoberChatToolStatus status, CancellationToken token)
    {
        await DisclosureSession!.ReauthorizeAsync(token);
        token.ThrowIfCancellationRequested();
        return EncodeGeneric(Result(status));
    }

    private OctoberChatToolInvocation EncodeGeneric(OctoberChatToolResult result)
    {
        ImmutableArray<byte> bytes = SerializeResultUtf8(result);
        return _budget.TryAddToolResultBytes(bytes.Length) ? new(result, bytes) : new(Result(OctoberChatToolStatus.BudgetExceeded), []);
    }

    internal bool TrySerializeArguments(object arguments, out string json)
    {
        using var stream = new BoundedArgumentStream(_budget.Limits.ToolArgumentBytes);
        try
        {
            JsonSerializer.Serialize(stream, arguments, _jsonOptions);
            json = _strictUtf8.GetString(stream.ToArray());
            return true;
        }
        catch (ArgumentLimitException)
        {
            json = string.Empty;
            return false;
        }
    }

    internal OctoberChatToolResult RejectOversizedArguments()
    {
        _budget.TryStartToolCall(-1, out IDisposable? lease);
        lease?.Dispose();
        return Result(OctoberChatToolStatus.BudgetExceeded);
    }

    internal static JsonElement SerializeResult(OctoberChatToolResult result) =>
        JsonSerializer.SerializeToElement(result, _outputJsonOptions);

    internal static ImmutableArray<byte> SerializeResultUtf8(OctoberChatToolResult result) =>
        JsonSerializer.SerializeToUtf8Bytes(result, _outputJsonOptions).ToImmutableArray();

    private static JsonSerializerOptions CreateOutputOptions()
    {
        var options = new JsonSerializerOptions(_jsonOptions);
        options.Converters.Add(new StrictOutputStringConverter());
        return options;
    }

    private sealed class StrictOutputStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetString();
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            _ = _strictUtf8.GetByteCount(value);
            writer.WriteStringValue(value);
        }
    }

    internal async Task<OctoberChatToolResult> RejectCallAsync(string arguments, CancellationToken cancellationToken) =>
        DisclosureSession is null ? await InvokeAsync(string.Empty, arguments, cancellationToken)
            : (await InvokeForModelAsync(string.Empty, arguments, cancellationToken)).Result;

    public OctoberWikiTools(IOctoberKnowledgeReader reader, OctoberChatTurnBinding binding,
        OctoberChatBudget budget, OctoberTurnReadLedger ledger)
    {
        if (ledger.Binding != binding || binding.ConversationId == Guid.Empty || binding.ContextId == Guid.Empty ||
            binding.TurnId == Guid.Empty || string.IsNullOrWhiteSpace(binding.Selection.Token) ||
            !new OctoberChatRequestValidator().Validate(binding.Request).IsValid)
        {
            throw new ArgumentException("The controlled tool and ledger require the same valid host-bound turn.", nameof(binding));
        }

        _reader = reader;
        _binding = binding;
        _budget = budget;
        _ledger = ledger;
    }

    public async Task<OctoberChatToolResult> InvokeAsync(string toolName, string arguments,
        CancellationToken cancellationToken = default)
    {
        if (DisclosureSession is not null) throw new InvalidOperationException("Canonical Chat execution is unavailable.");
        int argumentBytes;
        try { argumentBytes = _strictUtf8.GetByteCount(arguments); }
        catch (ArgumentException) { argumentBytes = -1; }
        if (!_budget.TryStartToolCall(argumentBytes, out IDisposable? lease)) return Result(OctoberChatToolStatus.BudgetExceeded);
        using (lease)
        using (CancellationTokenSource deadline = _budget.CreateDeadlineCancellation())
        using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _budget.OriginalCancellationToken, deadline.Token))
        {
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                object? parsed;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(arguments, new JsonDocumentOptions { MaxDepth = 16 });
                    if (document.RootElement.ValueKind != JsonValueKind.Object || HasDuplicateProperties(document.RootElement))
                    {
                        return Account(Result(OctoberChatToolStatus.InvalidArguments));
                    }

                    parsed = toolName switch
                    {
                        "SearchWiki" or "BrowseWiki" => document.RootElement.Deserialize<SearchArguments>(_jsonOptions),
                        "ReadWiki" => document.RootElement.Deserialize<ReadArguments>(_jsonOptions),
                        "GetRelatedDocuments" => document.RootElement.Deserialize<RelatedArguments>(_jsonOptions),
                        "ReportAnswerDocuments" => document.RootElement.Deserialize<ReportArguments>(_jsonOptions),
                        _ => null,
                    };
                }
                catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
                {
                    return Account(Result(OctoberChatToolStatus.InvalidArguments));
                }

                return parsed switch
                {
                    SearchArguments search => await SearchAsync(search, linked.Token),
                    ReadArguments read => await ReadAsync(read, linked.Token),
                    RelatedArguments related => await RelatedAsync(related, linked.Token),
                    ReportArguments report => Report(report),
                    _ => Account(Result(OctoberChatToolStatus.InvalidArguments)),
                };
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                // A non-cooperative owner task can no longer earn credit. Stop this turn so
                // abandoning a cancelled wait cannot create a new concurrency allowance.
                _budget.StopExecution();
                return Result(cancellationToken.IsCancellationRequested || _budget.OriginalCancellationToken.IsCancellationRequested
                    ? OctoberChatToolStatus.Cancelled : OctoberChatToolStatus.BudgetExceeded);
            }
            catch (Exception)
            {
                return Account(Result(OctoberChatToolStatus.Unavailable));
            }
        }
    }

    private async Task<OctoberChatToolResult> SearchAsync(SearchArguments arguments, CancellationToken cancellationToken)
    {
        int pageSize = arguments.PageSize ?? Math.Min(_budget.Limits.PageSize, KnowledgeContract.DefaultPageSize);
        var request = new SearchKnowledgeRequest(_binding.Selection.Token, arguments.Query, pageSize, arguments.Continuation);
        if (pageSize > _budget.Limits.PageSize || !new SearchKnowledgeRequestValidator().Validate(request).IsValid)
        {
            return Account(Result(OctoberChatToolStatus.InvalidArguments));
        }

        SearchPage page = await _reader.SearchAsync(new(_binding.Selection, arguments.Query, pageSize,
            Token(arguments.Continuation)), cancellationToken).WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (page.Status != KnowledgeReadStatus.Success || page.Documents.IsDefault ||
            page.Documents.Length > pageSize || page.Documents.Any(item => !IsSelected(item.Handle) ||
                _binding.Request.Mode == KnowledgeMode.Business && item.Kind != DocumentKind.BusinessLogic))
        {
            return Account(Result(OctoberChatToolStatus.Unavailable));
        }

        return Account(new(OctoberChatToolStatus.Succeeded, Search: page, References: []));
    }

    private async Task<OctoberChatToolResult> ReadAsync(ReadArguments arguments, CancellationToken cancellationToken)
    {
        if (arguments.Document is null || !new ReadKnowledgeRequestValidator().Validate(
            new ReadKnowledgeRequest(_binding.Selection.Token, arguments.Document, arguments.Continuation)).IsValid)
        {
            return Account(Result(OctoberChatToolStatus.InvalidArguments));
        }

        if (!IsSelected(arguments.Document)) return Account(Result(OctoberChatToolStatus.Unavailable));
        ContinuationToken? continuation = Token(arguments.Continuation);
        ContentPage page = await _reader.ReadExactAsync(new(_binding.Selection, arguments.Document, continuation),
            cancellationToken).WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (page.Status != KnowledgeReadStatus.Success || page.Document?.Handle != arguments.Document ||
            page.Content is null || _binding.Request.Mode == KnowledgeMode.Business && page.Document.Kind != DocumentKind.BusinessLogic)
        {
            return Account(Result(OctoberChatToolStatus.Unavailable));
        }

        OctoberChatToolResult result = Account(new(OctoberChatToolStatus.Succeeded, Content: page, References: []));
        if (result.Status != OctoberChatToolStatus.Succeeded) return result;
        cancellationToken.ThrowIfCancellationRequested();
        if (!_ledger.Record(arguments.Document, continuation, page)) return Result(OctoberChatToolStatus.Unavailable);
        return result;
    }

    private async Task<OctoberChatToolResult> RelatedAsync(RelatedArguments arguments, CancellationToken cancellationToken)
    {
        if (arguments.Document is null) return Account(Result(OctoberChatToolStatus.InvalidArguments));
        int pageSize = arguments.PageSize ?? Math.Min(_budget.Limits.PageSize, KnowledgeContract.DefaultPageSize);
        if (pageSize > _budget.Limits.PageSize || !new RelatedKnowledgeRequestValidator().Validate(
            new RelatedKnowledgeRequest(_binding.Selection.Token, arguments.Document, pageSize, arguments.Continuation)).IsValid)
        {
            return Account(Result(OctoberChatToolStatus.InvalidArguments));
        }

        if (!IsSelected(arguments.Document)) return Account(Result(OctoberChatToolStatus.Unavailable));
        RelatedPage page = await _reader.GetRelatedAsync(new(_binding.Selection, arguments.Document, pageSize,
            Token(arguments.Continuation)), cancellationToken).WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (page.Status != KnowledgeReadStatus.Success || page.Relationships.IsDefault ||
            page.Relationships.Length > pageSize || page.Relationships.Any(item => !IsSelected(item.From) || !IsSelected(item.To)))
        {
            return Account(Result(OctoberChatToolStatus.Unavailable));
        }

        return Account(new(OctoberChatToolStatus.Succeeded, Related: page, References: []));
    }

    private OctoberChatToolResult Report(ReportArguments arguments)
    {
        if (arguments.Documents is null || arguments.Documents.Length > _budget.Limits.ToolCalls)
        {
            return Account(Result(OctoberChatToolStatus.InvalidArguments));
        }

        // This checks consumed-read evidence only. It must not be wired to user/model
        // disclosure until T019 has the owner's full inference-lineage authority contract.
        ImmutableArray<OctoberChatReference>? references = _ledger.Report(arguments.Documents);
        return references is null ? Account(Result(OctoberChatToolStatus.InvalidReport))
            : Account(new(OctoberChatToolStatus.Succeeded, References: references.Value));
    }

    private OctoberChatToolResult Account(OctoberChatToolResult result)
    {
        // Reject malformed text rather than allowing JSON serialization to replace it.
        if (result.Content?.Content is { } content) _ = _strictUtf8.GetByteCount(content);
        long serializedBytes = JsonSerializer.SerializeToUtf8Bytes(result, _outputJsonOptions).LongLength;
        // The content allowance covers the complete returned projection, including its
        // claims, qualifications, coverage and framing, not merely Content's text bytes.
        return _budget.TryAddToolResultBytes(serializedBytes, content: result.Content is not null)
            ? result : Result(OctoberChatToolStatus.BudgetExceeded);
    }

    private bool IsSelected(DocumentHandle handle) => handle.BatchId == _binding.Request.Publication.Knowledge.BatchId &&
        handle.PublicationId == _binding.Request.Publication.PublicationId;

    private static ContinuationToken? Token(string? value) => value is null ? null : new(value);
    private static OctoberChatToolResult Result(OctoberChatToolStatus status) => new(status, References: []);

    private static bool HasDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value)) return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (HasDuplicateProperties(item)) return true;
            }
        }

        return false;
    }

    private sealed record SearchArguments(string Query = "", int? PageSize = null, string? Continuation = null);
    private sealed record ReadArguments(DocumentHandle? Document = null, string? Continuation = null);
    private sealed record RelatedArguments(DocumentHandle? Document = null, int? PageSize = null, string? Continuation = null);
    private sealed record ReportArguments(DocumentHandle[]? Documents = null);

    private sealed class ArgumentLimitException : IOException;

    private sealed class BoundedArgumentStream(int limit) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            Check(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Check(buffer.Length);
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            Check(1);
            base.WriteByte(value);
        }

        private void Check(int length)
        {
            if (length > limit - Length) throw new ArgumentLimitException();
        }
    }
}
