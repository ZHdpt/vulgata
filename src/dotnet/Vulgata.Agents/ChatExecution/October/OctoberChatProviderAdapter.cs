using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices.OctoberChat;

namespace Vulgata.Agents.ChatExecution.October;

/// <summary>Controlled typed-SDK seam; raw HTTP/SSE framing and W1 admission remain separate prerequisites.</summary>
internal sealed class OctoberChatProviderAdapter : DelegatingChatClient
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    internal static readonly ImmutableArray<string> ToolNames =
        ["SearchWiki", "BrowseWiki", "ReadWiki", "GetRelatedDocuments", "ReportAnswerDocuments"];
    private readonly OctoberWikiTools _tools;
    private readonly OctoberChatBudget _budget;
    private readonly Action<OctoberChatToolProgress>? _onToolProgress;
    private readonly HashSet<string> _seenCallIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PendingCall> _pendingCalls = new(StringComparer.Ordinal);

    internal OctoberChatProviderAdapter(IChatClient client, OctoberWikiTools tools, OctoberChatBudget budget,
        Action<OctoberChatToolProgress>? onToolProgress = null)
        : base(new BorrowedChatClient(client))
    {
        _tools = tools;
        _budget = budget;
        _onToolProgress = onToolProgress;
    }

    internal ChatFinishReason? LastFinishReason { get; private set; }
    internal OctoberChatProviderStatus? FailureStatus { get; private set; }
    internal bool HasPendingCalls => _pendingCalls.Count != 0;

    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Only controlled streaming is supported.");

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_tools.DisclosureSession is { } disclosure) await disclosure.ReauthorizeAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_budget.TryStartProviderRound(out _)) throw Fail(OctoberChatProviderStatus.BudgetExceeded);
        if (HasPendingCalls) throw Fail(OctoberChatProviderStatus.Failed);
        LastFinishReason = null;
        ChatFinishReason? finish = null;
        string? responseId = null;
        string? messageId = null;
        var calls = new List<PendingCall>();

        await foreach (ChatResponseUpdate update in BoundedUpdatesAsync(
            InnerClient.GetStreamingResponseAsync(messages, options, cancellationToken), cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            responseId ??= update.ResponseId;
            messageId ??= update.MessageId;
            if (finish is not null)
            {
                bool usageOnly = update.Contents.All(content => content is UsageContent);
                if (usageOnly && (update.FinishReason is null || update.FinishReason == finish)) continue;
                // The installed OpenAI adapter emits ToolCalls finish metadata, then
                // one final aggregate containing the assembled native calls with the
                // same finish reason. Accept that exact shape, still withholding all
                // dispatch until the complete iterator has settled below.
                bool finalNativeAggregate = finish == ChatFinishReason.ToolCalls && update.FinishReason == finish &&
                    calls.Count == 0 && update.Contents.Any(content => content is FunctionCallContent { InformationalOnly: false }) &&
                    update.Contents.All(content => content is FunctionCallContent or UsageContent);
                if (!finalNativeAggregate)
                {
                    foreach (FunctionCallContent late in update.Contents.OfType<FunctionCallContent>().Where(call => !call.InformationalOnly))
                    {
                        await BufferCallAsync(late, calls, cancellationToken);
                    }

                    await RejectCallsAsync(calls, cancellationToken);
                    throw Fail(OctoberChatProviderStatus.Failed);
                }
            }

            if (update.Role is { } role && role != ChatRole.Assistant) throw Fail(OctoberChatProviderStatus.Failed);
            var texts = new List<AIContent>();
            foreach (AIContent content in update.Contents)
            {
                if (content is FunctionCallContent call)
                {
                    if (call.InformationalOnly) continue;
                    await BufferCallAsync(call, calls, cancellationToken);
                    if (call.Exception is not null || string.IsNullOrWhiteSpace(call.CallId) ||
                        !ToolNames.Contains(call.Name, StringComparer.Ordinal) || !_seenCallIds.Add(call.CallId))
                    {
                        await RejectCallsAsync(calls, cancellationToken);
                        throw Fail(OctoberChatProviderStatus.Failed);
                    }
                }
                else if (content is TextContent text)
                {
                    if (!_budget.TryAddOutput(text.Text)) throw Fail(OctoberChatProviderStatus.BudgetExceeded);
                    texts.Add(new TextContent(text.Text));
                }
                // Reasoning, usage and raw provider payloads do not become visible text.
            }

            if (texts.Count != 0)
            {
                yield return new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant, ResponseId = responseId, MessageId = messageId, Contents = texts,
                };
            }

            if (update.FinishReason is { } reason) finish = reason;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (finish is null || finish == ChatFinishReason.ToolCalls && calls.Count == 0 ||
            finish != ChatFinishReason.ToolCalls && calls.Count != 0 ||
            finish != ChatFinishReason.ToolCalls && finish != ChatFinishReason.Stop &&
            finish != ChatFinishReason.Length && finish != ChatFinishReason.ContentFilter)
        {
            await RejectCallsAsync(calls, cancellationToken);
            throw Fail(OctoberChatProviderStatus.Failed);
        }

        // No native function reaches the SDK dispatcher until its entire typed round
        // has passed duplicate/finish checks. Ordinary text above is not buffered.
        foreach (PendingCall call in calls) _pendingCalls.Add(call.Snapshot.CallId, call);
        LastFinishReason = finish;
        yield return new ChatResponseUpdate
        {
            Role = ChatRole.Assistant, ResponseId = responseId, MessageId = messageId,
            Contents = calls.Select(call => (AIContent)call.Snapshot).ToList(), FinishReason = finish,
        };
    }

    internal async ValueTask<object?> InvokeToolAsync(FunctionInvocationContext context, CancellationToken cancellationToken)
    {
        FunctionCallContent call = context.CallContent;
        if (!_pendingCalls.TryGetValue(call.CallId, out PendingCall? admitted) || admitted.Snapshot.Name != call.Name)
        {
            await _tools.RejectCallAsync("{}", cancellationToken);
            throw Fail(OctoberChatProviderStatus.Failed);
        }

        // Forward all original fields, including unknown fields, to the strict facade.
        // A reflection binder must not silently discard an attempted authority override.
        // Application activity uses its own opaque identifier, never the provider's
        // call ID, arguments or result. A throwing sink stops execution without a
        // recursive attempt to notify that same sink about its own failure.
        Guid operationId = Guid.NewGuid();
        _onToolProgress?.Invoke(new(operationId, OctoberChatToolProgressKind.Started));
        OctoberChatToolResult result;
        OctoberChatToolInvocation? invocation = null;
        try
        {
            if (_tools.DisclosureSession is not null)
            {
                invocation = await _tools.InvokeForModelAsync(call.Name, admitted.ArgumentsJson, cancellationToken);
                result = invocation.Result;
            }
            else result = await _tools.InvokeAsync(call.Name, admitted.ArgumentsJson, cancellationToken);
        }
        catch (Exception)
        {
            _onToolProgress?.Invoke(new(operationId, OctoberChatToolProgressKind.Failed));
            throw;
        }

        _pendingCalls.Remove(call.CallId);
        _onToolProgress?.Invoke(new(operationId, result.Status == OctoberChatToolStatus.Succeeded
            ? OctoberChatToolProgressKind.Completed : OctoberChatToolProgressKind.Failed));
        if (result.Status is OctoberChatToolStatus.BudgetExceeded or OctoberChatToolStatus.Cancelled or
            OctoberChatToolStatus.InvalidArguments or OctoberChatToolStatus.InvalidReport)
        {
            context.Terminate = true;
            throw Fail(result.Status switch
            {
                OctoberChatToolStatus.BudgetExceeded => OctoberChatProviderStatus.BudgetExceeded,
                OctoberChatToolStatus.Cancelled => OctoberChatProviderStatus.Cancelled,
                _ => OctoberChatProviderStatus.Failed,
            });
        }

        if (invocation is not null)
        {
            if (invocation.OutputUtf8.IsDefaultOrEmpty) throw Fail(OctoberChatProviderStatus.Failed);
            // The installed OpenAI adapter formats object/JsonElement results. Its
            // text-result path carries the already-encoded JSON as tool-message text.
            // Actual SDK request conformance must preserve these exact bytes and an
            // object JSON payload, with no second quoted-JSON representation.
            return _strictUtf8.GetString(invocation.OutputUtf8.AsSpan());
        }
        // Historical controlled typed-client mechanics retain their representation;
        // this path is never a canonical disclosed-reader fallback.
        return OctoberWikiTools.SerializeResult(result);
    }

    private async Task BufferCallAsync(FunctionCallContent call, List<PendingCall> calls, CancellationToken cancellationToken)
    {
        // Reserve no second tool counter. Compare against the original remaining
        // allowance; the facade alone charges invocations and bounded refusals.
        if (calls.Count >= _budget.Limits.ToolCalls - _budget.ToolCalls)
        {
            await RejectCallsAsync(calls, cancellationToken);
            await _tools.RejectCallAsync("{}", cancellationToken);
            throw Fail(OctoberChatProviderStatus.BudgetExceeded);
        }

        if (!_tools.TrySerializeArguments(call.Arguments ?? new Dictionary<string, object?>(), out string arguments))
        {
            await RejectCallsAsync(calls, cancellationToken);
            _tools.RejectOversizedArguments();
            throw Fail(OctoberChatProviderStatus.BudgetExceeded);
        }

        // Retain a bounded immutable serialization, not a provider-owned mutable
        // argument dictionary. SDK invocation receives a separate snapshot.
        var snapshotArguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments)!;
        calls.Add(new(new FunctionCallContent(call.CallId, call.Name, snapshotArguments), arguments));
    }

    private async Task RejectCallsAsync(IEnumerable<PendingCall> calls, CancellationToken cancellationToken)
    {
        foreach (PendingCall call in calls)
        {
            OctoberChatToolResult refusal = await _tools.RejectCallAsync(call.ArgumentsJson, cancellationToken);
            if (refusal.Status == OctoberChatToolStatus.BudgetExceeded) throw Fail(OctoberChatProviderStatus.BudgetExceeded);
        }
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> BoundedUpdatesAsync(
        IAsyncEnumerable<ChatResponseUpdate> updates, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IAsyncEnumerator<ChatResponseUpdate> iterator = updates.GetAsyncEnumerator(cancellationToken);
        bool cleanupDeferred = false;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Task<bool> pending = iterator.MoveNextAsync().AsTask();
                bool hasNext;
                try
                {
                    hasNext = await pending.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && !pending.IsCompleted)
                {
                    cleanupDeferred = true;
                    _ = ObserveAndDisposeAsync(iterator, pending);
                    throw;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!hasNext) yield break;
                yield return iterator.Current;
            }
        }
        finally
        {
            if (!cleanupDeferred)
            {
                // Start disposal exactly once. A borrowed implementation may ignore
                // cancellation during cleanup just as it may during MoveNextAsync.
                Task disposal = iterator.DisposeAsync().AsTask();
                try
                {
                    await disposal.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _ = ObserveDisposalAsync(disposal);
                    throw;
                }
            }
        }
    }

    private static async Task ObserveAndDisposeAsync(IAsyncEnumerator<ChatResponseUpdate> iterator, Task<bool> pending)
    {
        // This bounds the consumer wait, not opaque underlying I/O. Never race
        // DisposeAsync against MoveNextAsync, and never process a late Current value.
        // Future W1 admission must retain its request permit through this cleanup.
        try { await pending; }
        catch (Exception) { }
        try { await iterator.DisposeAsync(); }
        catch (Exception) { }
    }

    private static async Task ObserveDisposalAsync(Task disposal)
    {
        // Observe only the already-started cleanup; never call DisposeAsync twice.
        try { await disposal; }
        catch (Exception) { }
    }

    private sealed record PendingCall(FunctionCallContent Snapshot, string ArgumentsJson);

    private InvalidOperationException Fail(OctoberChatProviderStatus status)
    {
        FailureStatus ??= status;
        _budget.StopExecution();
        return new InvalidOperationException("The controlled Chat operation stopped.");
    }

    private sealed class BorrowedChatClient(IChatClient client) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => client.GetResponseAsync(messages, options, cancellationToken);
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            client.GetStreamingResponseAsync(messages, options, cancellationToken);
        public object? GetService(Type serviceType, object? serviceKey = null) => client.GetService(serviceType, serviceKey);
        public void Dispose() { } // The injecting caller owns the underlying client.
    }
}
