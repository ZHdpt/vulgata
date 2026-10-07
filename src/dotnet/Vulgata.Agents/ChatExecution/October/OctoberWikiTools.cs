using System.Collections.Immutable;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberChat;
using Vulgata.Shared.OctoberKnowledge;

namespace Vulgata.Agents.ChatExecution.October;

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
    private readonly IOctoberKnowledgeReader _reader;
    private readonly OctoberChatTurnBinding _binding;
    private readonly OctoberChatBudget _budget;
    private readonly OctoberTurnReadLedger _ledger;

    internal OctoberChatBudget Budget => _budget;

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
        JsonSerializer.SerializeToElement(result, _jsonOptions);

    internal Task<OctoberChatToolResult> RejectCallAsync(string arguments, CancellationToken cancellationToken) =>
        InvokeAsync(string.Empty, arguments, cancellationToken);

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
        long serializedBytes = JsonSerializer.SerializeToUtf8Bytes(result, _jsonOptions).LongLength;
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
