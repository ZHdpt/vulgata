using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>A real transport receives the exact leaf lease explicitly; unbound sends are forbidden.</summary>
public interface IOctoberWorkerAttemptClient
{
    string ProviderProfileId { get; }
    IAsyncEnumerable<ChatResponseUpdate> GetAttemptStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options,
        IOctoberProviderAttemptLease lease, CancellationToken cancellationToken = default,
        Action<long>? onDispatched = null);
}

/// <summary>Application-injected contained source; never constructed from provider JSON.</summary>
public interface IOctoberVerifiedFileFrame
{
    PreparedOrigin Origin { get; }
    string Text { get; }
    long ByteLength { get; }
    int LineCount { get; }
    string FileIdentity { get; }
    Task<bool> VerifyCurrentAsync(CancellationToken cancellationToken = default);
}

public sealed record OctoberWorkerAdvisory(Contributor Contributor, string Text);

public sealed record OctoberFileWorkerLimits(
    int MaximumProviderRounds = 8,
    int MaximumToolCalls = 16,
    int MaximumWindowUtf8Bytes = 32768,
    int MaximumInputUtf8Bytes = 1048576,
    int MaximumOutputUtf8Bytes = 262144,
    TimeSpan? RequestTimeout = null,
    long MaximumTokensPerRequest = 32768,
    int MaximumContinuationWindows = 4);

public sealed record OctoberFileWorkerRequest(
    KnowledgeBinding Binding,
    Guid RunId,
    Guid FileWorkstreamId,
    Guid AdmissionId,
    Guid AttemptId,
    string ProviderProfileId,
    IOctoberVerifiedFileFrame Source,
    ImmutableArray<Contributor> Contributors,
    ImmutableArray<OctoberWorkerAdvisory> Advisory,
    OctoberFileWorkerLimits Limits);

public enum OctoberFileWorkerStatus
{
    Completed, InvalidInput, InvalidOutput, ProviderFailed, BudgetExceeded,
    Cancelled, SourceChanged, Denied
}

public enum OctoberWorkerRetryDisposition { None, KnownSafeTransient }

/// <summary>Worker observation only; this is not a Knowledge completion capability.</summary>
public sealed record OctoberFileWorkerResult(
    OctoberFileWorkerStatus Status,
    CompletedFileProposal? Proposal = null,
    int CoveredUtf8Bytes = 0,
    int ProviderRounds = 0,
    int SettledToolCalls = 0)
{
    public OctoberWorkerRetryDisposition RetryDisposition { get; init; }
    public TimeSpan? RetryAfter { get; init; }
}

public sealed class OctoberFileWorkerExecutor
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly HashSet<string> ToolNames = new(StringComparer.Ordinal)
    {
        "list_prepared_source", "read_prepared_source", "search_prepared_source",
        "navigate_java_definition", "navigate_java_references", "codegraph_explore"
    };
    private readonly IChatClient _client;
    private readonly IOctoberWorkerExecutionScope _scope;
    private int _used;

    public OctoberFileWorkerExecutor(IChatClient client, IOctoberWorkerExecutionScope scope)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    public async Task<OctoberFileWorkerResult> ExecuteAsync(OctoberFileWorkerRequest request,
        IReadOnlyList<AIFunction> tools, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _used, 1) != 0)
            return new(OctoberFileWorkerStatus.InvalidInput);
        WorkerClient? adapter = null;
        int covered = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ValidRequest(request, tools) || _client.GetService(typeof(FunctionInvokingChatClient)) is not null)
                return new(OctoberFileWorkerStatus.InvalidInput);
            if (_client is IOctoberWorkerAttemptClient selectedClient &&
                !string.Equals(selectedClient.ProviderProfileId, request.ProviderProfileId, StringComparison.Ordinal))
                return new(OctoberFileWorkerStatus.InvalidInput);
            string prompt = LoadPrompt();
            byte[] source = Utf8.GetBytes(request.Source.Text);
            if (source.LongLength != request.Source.ByteLength ||
                !string.Equals(Convert.ToHexStringLower(SHA256.HashData(source)), request.Source.Origin.SourceDigest, StringComparison.OrdinalIgnoreCase) ||
                CountLines(request.Source.Text) != request.Source.LineCount)
                return new(OctoberFileWorkerStatus.InvalidInput);
            if (!await request.Source.VerifyCurrentAsync(cancellationToken))
                return new(OctoberFileWorkerStatus.SourceChanged);
            List<string> windows = SplitWindows(source, request.Limits.MaximumWindowUtf8Bytes);
            if (windows.Count > request.Limits.MaximumContinuationWindows)
                return new(OctoberFileWorkerStatus.BudgetExceeded);

            adapter = new WorkerClient(_client, _scope, request, tools);
            using var invoking = new FunctionInvokingChatClient(adapter)
            {
                AllowConcurrentInvocation = false,
                IncludeDetailedErrors = false,
                MaximumConsecutiveErrorsPerRequest = 0,
                MaximumIterationsPerRequest = request.Limits.MaximumProviderRounds + 1,
                TerminateOnUnknownCalls = true,
                FunctionInvoker = adapter.InvokeAsync
            };
            var agent = new ChatClientAgent(invoking, new ChatClientAgentOptions
            {
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    ResponseFormat = ChatResponseFormat.Json,
                    Tools = tools.Cast<AITool>().ToList(),
                    AllowMultipleToolCalls = false,
                    MaxOutputTokens = 8192
                }
            });
            var history = new List<ChatMessage> { new(ChatRole.System, prompt) };
            for (int index = 0; index < windows.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string input = JsonSerializer.Serialize(new
                {
                    windowOrdinal = index + 1,
                    windowCount = windows.Count,
                    byteStart = covered,
                    sourceText = windows[index],
                    finalWindow = index == windows.Count - 1,
                    advisory = request.Advisory.Select((item, ordinal) => new { advisoryIndex = ordinal, text = item.Text })
                });
                history.Add(new ChatMessage(ChatRole.User, input));
                AgentResponse response = await agent.RunStreamingAsync(history, cancellationToken: cancellationToken)
                    .ToAgentResponseAsync(cancellationToken);
                if (adapter.Failure is { } failure) throw new WorkerFailure(failure);
                if (adapter.Finish != ChatFinishReason.Stop || adapter.PendingCalls != 0)
                    throw new WorkerFailure(OctoberFileWorkerStatus.InvalidOutput);
                if (!await request.Source.VerifyCurrentAsync(cancellationToken))
                    throw new WorkerFailure(OctoberFileWorkerStatus.SourceChanged);
                covered = checked(covered + Utf8.GetByteCount(windows[index]));
                history.AddRange(response.Messages);
            }
            if (covered != source.Length) throw new WorkerFailure(OctoberFileWorkerStatus.InvalidOutput);
            CompletedFileProposal proposal = ParseProposal(adapter.LastText, request);
            return new(OctoberFileWorkerStatus.Completed, proposal, covered, adapter.Rounds, adapter.SettledTools);
        }
        catch (OperationCanceledException)
        {
            return Failed(OctoberFileWorkerStatus.Cancelled);
        }
        catch (WorkerFailure failure) { return Failed(failure.Status); }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidDataException or OverflowException)
        {
            return Failed(adapter?.Failure ?? OctoberFileWorkerStatus.InvalidOutput);
        }
        catch (Exception) { return Failed(adapter?.Failure ?? OctoberFileWorkerStatus.ProviderFailed); }

        OctoberFileWorkerResult Failed(OctoberFileWorkerStatus status)
            => new(status, null, covered, adapter?.Rounds ?? 0, adapter?.SettledTools ?? 0)
            {
                RetryDisposition = status == OctoberFileWorkerStatus.ProviderFailed
                    ? adapter?.RetryDisposition ?? OctoberWorkerRetryDisposition.None : OctoberWorkerRetryDisposition.None
            };
    }

    private static bool ValidRequest(OctoberFileWorkerRequest request, IReadOnlyList<AIFunction> tools)
    {
        if (request?.Source is null || request.Limits is null || request.Binding is null || tools is null ||
            request.RunId == Guid.Empty || request.FileWorkstreamId == Guid.Empty || request.AdmissionId == Guid.Empty ||
            request.AttemptId == Guid.Empty || string.IsNullOrWhiteSpace(request.ProviderProfileId) ||
            string.IsNullOrWhiteSpace(request.Source.FileIdentity) || request.Source.Text is null ||
            request.Source.Origin is null || request.Contributors.IsDefaultOrEmpty || request.Advisory.IsDefault ||
            tools.Any(tool => tool is null || !ToolNames.Contains(tool.Name)) ||
            tools.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count() != tools.Count)
            return false;
        OctoberFileWorkerLimits limits = request.Limits;
        if (limits.MaximumProviderRounds is < 1 or > 100 || limits.MaximumToolCalls is < 0 or > 1000 ||
            limits.MaximumWindowUtf8Bytes is < 4 or > 1048576 || limits.MaximumInputUtf8Bytes is < 1 or > 16777216 ||
            limits.MaximumOutputUtf8Bytes is < 1 or > 1048576 || limits.MaximumTokensPerRequest is < 1 or > 1048576 ||
            limits.MaximumContinuationWindows is < 1 or > 100 ||
            limits.RequestTimeout is { } duration && (duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(10)))
            return false;
        if (request.Source.ByteLength < 0 || request.Source.ByteLength > limits.MaximumInputUtf8Bytes)
            return false;
        return request.Advisory.All(item => item is not null && item.Contributor.Kind != ContributorKind.Source &&
            request.Contributors.Contains(item.Contributor) && item.Text is not null &&
            string.Equals(Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(item.Text))), item.Contributor.ContentDigest, StringComparison.OrdinalIgnoreCase)) &&
            request.Contributors.Where(item => item.Kind != ContributorKind.Source)
                .All(item => request.Advisory.Any(advisory => advisory.Contributor == item));
    }

    private static string LoadPrompt()
    {
        const string name = "Vulgata.Agents.Prompts.OctoberFileWorker.md";
        var assembly = typeof(OctoberFileWorkerExecutor).Assembly;
        if (assembly.GetManifestResourceNames().Count(value => value == name) != 1)
            throw new InvalidDataException("The fixed Worker prompt is unavailable.");
        using Stream stream = assembly.GetManifestResourceStream(name)!;
        byte[] bytes = new byte[32769];
        int length = 0;
        while (length < bytes.Length)
        {
            int read = stream.Read(bytes, length, bytes.Length - length);
            if (read == 0) break;
            length += read;
        }
        if (length is 0 or > 32768) throw new InvalidDataException("The fixed Worker prompt is invalid.");
        return Utf8.GetString(bytes, 0, length);
    }

    private static List<string> SplitWindows(byte[] bytes, int maximum)
    {
        var windows = new List<string>();
        for (int offset = 0; offset < bytes.Length;)
        {
            int count = Math.Min(maximum, bytes.Length - offset);
            while (offset + count < bytes.Length && (bytes[offset + count] & 0xc0) == 0x80) count--;
            if (count == 0) throw new InvalidDataException("Invalid source window.");
            windows.Add(Utf8.GetString(bytes, offset, count));
            offset += count;
        }
        if (windows.Count == 0) windows.Add(string.Empty);
        return windows;
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0) return 0;
        int lines = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n' || text[i] == '\r')
            {
                lines++;
                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
        }
        return lines;
    }

    private sealed class WorkerFailure(OctoberFileWorkerStatus status) : Exception("Worker execution stopped.")
    {
        internal OctoberFileWorkerStatus Status { get; } = status;
    }

    private sealed class WorkerClient(IChatClient client, IOctoberWorkerExecutionScope scope,
        OctoberFileWorkerRequest request, IReadOnlyList<AIFunction> tools) : DelegatingChatClient(client)
    {
        private readonly Dictionary<string, FunctionCallContent> _pending = new(StringComparer.Ordinal);
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        private int _inputBytes;
        private int _outputBytes;
        private int _toolCount;
        private bool _allUsageKnown = true;
        internal OctoberWorkerRetryDisposition RetryDisposition { get; private set; }
        internal int Rounds { get; private set; }
        internal int SettledTools { get; private set; }
        internal int PendingCalls => _pending.Count;
        internal ChatFinishReason? Finish { get; private set; }
        internal string LastText { get; private set; } = string.Empty;
        internal OctoberFileWorkerStatus? Failure { get; private set; }

        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw Fail(OctoberFileWorkerStatus.InvalidInput);

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null || _pending.Count != 0) throw Fail(OctoberFileWorkerStatus.InvalidOutput);
            if (Rounds >= request.Limits.MaximumProviderRounds) throw Fail(OctoberFileWorkerStatus.BudgetExceeded);
            if (!await request.Source.VerifyCurrentAsync(cancellationToken)) throw Fail(OctoberFileWorkerStatus.SourceChanged);
            ChatMessage[] snapshot = messages.ToArray();
            int input = MeasureInput(snapshot, options);
            _inputBytes = checked(_inputBytes + input);
            if (_inputBytes > request.Limits.MaximumInputUtf8Bytes || input >= request.Limits.MaximumTokensPerRequest)
                throw Fail(OctoberFileWorkerStatus.BudgetExceeded);
            ChatOptions bounded = options?.Clone() ?? new();
            int outputTokens = (int)Math.Min(8192, request.Limits.MaximumTokensPerRequest - input);
            bounded.MaxOutputTokens = outputTokens;
            var admissionRequest = new OctoberWorkerProviderAttemptRequest(request.RunId, request.FileWorkstreamId,
                Guid.NewGuid(), request.ProviderProfileId, request.Limits.RequestTimeout ?? TimeSpan.FromMinutes(2),
                checked(input + outputTokens));
            long waitStarted = Stopwatch.GetTimestamp();
            ProviderAdmission admission = await scope.AcquireWorkerAttemptAsync(admissionRequest, cancellationToken);
            if (admission.Status != ProviderAdmissionStatus.Granted || admission.Lease is null)
                throw Fail(admission.Status is ProviderAdmissionStatus.BudgetExhausted or ProviderAdmissionStatus.DeadlineExceeded
                    ? OctoberFileWorkerStatus.BudgetExceeded : OctoberFileWorkerStatus.Denied);
            IOctoberProviderAttemptLease lease = admission.Lease;
            TimeSpan wait = Stopwatch.GetElapsedTime(waitStarted);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.CancellationToken);
            linked.CancelAfter(request.Limits.RequestTimeout ?? TimeSpan.FromMinutes(2));
            var timing = new AttemptTiming();
            bool success = false;
            bool transient = false;
            bool deferred = false;
            IAsyncEnumerator<ChatResponseUpdate>? iterator = null;
            Task<bool>? pendingMove = null;
            long? inputTokens = null;
            long? actualOutputTokens = null;
            var buffered = new List<ChatResponseUpdate>();
            var calls = new List<FunctionCallContent>();
            var text = new StringBuilder();
            ChatFinishReason? finish = null;
            try
            {
                if (client is not IOctoberWorkerAttemptClient &&
                    await lease.CheckBeforeSendAsync(linked.Token) != ProviderAdmissionStatus.Granted)
                    throw Fail(OctoberFileWorkerStatus.Denied);
                if (!await request.Source.VerifyCurrentAsync(linked.Token)) throw Fail(OctoberFileWorkerStatus.SourceChanged);
                Rounds++;
                IAsyncEnumerable<ChatResponseUpdate> updates = client is IOctoberWorkerAttemptClient admittedClient
                    ? admittedClient.GetAttemptStreamingResponseAsync(snapshot, bounded, lease, linked.Token, timing.MarkDispatched)
                    : client.GetStreamingResponseAsync(snapshot, bounded, linked.Token);
                if (client is not IOctoberWorkerAttemptClient) timing.MarkDispatched(Stopwatch.GetTimestamp());
                iterator = updates.GetAsyncEnumerator(linked.Token);
                while (true)
                {
                    pendingMove = iterator.MoveNextAsync().AsTask();
                    bool next;
                    try { next = await pendingMove.WaitAsync(linked.Token); }
                    catch (OperationCanceledException) when (!pendingMove.IsCompleted)
                    {
                        deferred = true;
                        await QuarantineAsync(lease);
                        _ = SettleLateAsync(pendingMove, iterator, lease, wait, timing);
                        throw;
                    }
                    if (!next) break;
                    ChatResponseUpdate update = iterator.Current;
                    if (finish is not null && (update.Contents.Any(content => content is not UsageContent) ||
                        update.FinishReason is { } repeated && repeated != finish)) throw Fail(OctoberFileWorkerStatus.InvalidOutput);
                    if (update.Role is { } role && role != ChatRole.Assistant) throw Fail(OctoberFileWorkerStatus.InvalidOutput);
                    foreach (AIContent content in update.Contents)
                    {
                        if (content is TextContent value)
                        {
                            ChargeOutput(value.Text);
                            text.Append(value.Text);
                        }
                        else if (content is FunctionCallContent call && !call.InformationalOnly)
                        {
                            ChargeOutput(JsonSerializer.Serialize(call.Arguments));
                            if (call.Exception is not null || string.IsNullOrWhiteSpace(call.CallId) ||
                                !tools.Any(tool => tool.Name == call.Name) || !_seen.Add(call.CallId))
                                throw Fail(OctoberFileWorkerStatus.InvalidOutput);
                            if (++_toolCount > request.Limits.MaximumToolCalls) throw Fail(OctoberFileWorkerStatus.BudgetExceeded);
                            var arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                JsonSerializer.Serialize(call.Arguments ?? new Dictionary<string, object?>()))!;
                            var copy = new FunctionCallContent(call.CallId, call.Name, arguments);
                            calls.Add(copy);
                        }
                        else if (content is UsageContent usage)
                        {
                            inputTokens = usage.Details.InputTokenCount;
                            actualOutputTokens = usage.Details.OutputTokenCount;
                        }
                        else if (content is not FunctionCallContent)
                            ChargeOutput(JsonSerializer.Serialize(content));
                    }
                    if (update.FinishReason is { } currentFinish) finish = currentFinish;
                    if (buffered.Count >= 8192) throw Fail(OctoberFileWorkerStatus.BudgetExceeded);
                    buffered.Add(update);
                }
                if (finish != ChatFinishReason.Stop && finish != ChatFinishReason.ToolCalls ||
                    finish == ChatFinishReason.ToolCalls && calls.Count == 0 || finish == ChatFinishReason.Stop && calls.Count != 0)
                    throw Fail(OctoberFileWorkerStatus.InvalidOutput);
                if (!await request.Source.VerifyCurrentAsync(linked.Token)) throw Fail(OctoberFileWorkerStatus.SourceChanged);
                success = true;
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                transient = true;
                throw;
            }
            finally
            {
                if (!deferred)
                {
                    if (iterator is not null)
                    {
                        Task? cleanup = null;
                        try
                        {
                            cleanup = iterator.DisposeAsync().AsTask();
                            await cleanup.WaitAsync(TimeSpan.FromSeconds(5));
                        }
                        catch (TimeoutException)
                        {
                            deferred = true;
                            await QuarantineAsync(lease);
                            if (cleanup is not null) _ = SettleAfterCleanupAsync(cleanup, lease, wait, timing);
                            Failure = OctoberFileWorkerStatus.ProviderFailed;
                        }
                        catch (Exception)
                        {
                            deferred = true;
                            await QuarantineAsync(lease);
                            Failure = OctoberFileWorkerStatus.ProviderFailed;
                        }
                    }
                    if (!deferred)
                    {
                        await lease.SettleAsync(new(inputTokens, actualOutputTokens, null, wait,
                            timing.Elapsed, success ? ProviderAttemptOutcome.Completed :
                            linked.IsCancellationRequested ? ProviderAttemptOutcome.Cancelled : ProviderAttemptOutcome.Failed), CancellationToken.None);
                        await lease.DisposeAsync();
                        _allUsageKnown &= inputTokens is >= 0 && actualOutputTokens is >= 0 &&
                            inputTokens.Value <= admissionRequest.MaximumTokens &&
                            actualOutputTokens.Value <= admissionRequest.MaximumTokens - inputTokens.Value;
                        if (transient && _allUsageKnown && _pending.Count == 0 && calls.Count == 0 &&
                            Failure is null && !linked.IsCancellationRequested &&
                            await request.Source.VerifyCurrentAsync(CancellationToken.None))
                            RetryDisposition = OctoberWorkerRetryDisposition.KnownSafeTransient;
                    }
                }
            }
            if (deferred) throw Fail(OctoberFileWorkerStatus.ProviderFailed);
            foreach (FunctionCallContent call in calls) _pending.Add(call.CallId, call);
            Finish = finish;
            LastText = text.ToString();
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                ResponseId = buffered.LastOrDefault()?.ResponseId,
                Contents = [new TextContent(LastText), .. calls],
                FinishReason = finish
            };
        }

        internal async ValueTask<object?> InvokeAsync(FunctionInvocationContext context, CancellationToken token)
        {
            if (!_pending.TryGetValue(context.CallContent.CallId, out FunctionCallContent? call) || call.Name != context.Function.Name)
                throw Fail(OctoberFileWorkerStatus.InvalidOutput);
            if (!await request.Source.VerifyCurrentAsync(token)) throw Fail(OctoberFileWorkerStatus.SourceChanged);
            AIFunction function = tools.Single(tool => tool.Name == call.Name);
            if (function.JsonSchema.TryGetProperty("properties", out JsonElement properties) &&
                call.Arguments?.Keys.Any(key => !properties.TryGetProperty(key, out _)) == true)
                throw Fail(OctoberFileWorkerStatus.InvalidOutput);
            object? result = await function.InvokeAsync(new AIFunctionArguments(call.Arguments ?? new Dictionary<string, object?>()), token);
            ChargeOutput(JsonSerializer.Serialize(result));
            if (!await request.Source.VerifyCurrentAsync(token)) throw Fail(OctoberFileWorkerStatus.SourceChanged);
            _pending.Remove(call.CallId);
            SettledTools++;
            return result;
        }

        private void ChargeOutput(string value)
        {
            _outputBytes = checked(_outputBytes + Utf8.GetByteCount(value));
            if (_outputBytes > request.Limits.MaximumOutputUtf8Bytes) throw Fail(OctoberFileWorkerStatus.BudgetExceeded);
        }

        private static int MeasureInput(IEnumerable<ChatMessage> messages, ChatOptions? options)
        {
            int bytes = 256;
            foreach (ChatMessage message in messages)
            {
                bytes = checked(bytes + 64);
                foreach (AIContent content in message.Contents)
                    bytes = checked(bytes + Utf8.GetByteCount(content is TextContent text ? text.Text : JsonSerializer.Serialize(content, content.GetType())));
            }
            foreach (AITool tool in options?.Tools ?? [])
                bytes = checked(bytes + Utf8.GetByteCount(tool.Name) + Utf8.GetByteCount(tool.Description ?? string.Empty) +
                    (tool is AIFunction function ? Utf8.GetByteCount(function.JsonSchema.GetRawText()) : 0));
            return bytes;
        }

        private WorkerFailure Fail(OctoberFileWorkerStatus status)
        {
            Failure = status;
            return new(status);
        }

        private static bool IsTransient(Exception exception)
        {
            int? status = exception switch
            {
                HttpRequestException { StatusCode: { } code } => (int)code,
                ClientResultException clientError => clientError.Status,
                _ => null
            };
            return status is 408 or 429 or >= 500 and <= 599;
        }

        protected override void Dispose(bool disposing) { }

        private static async Task QuarantineAsync(IOctoberProviderAttemptLease lease)
        {
            // The host lease blocks the shared scope when a sent transport is not settled.
            // Disposal is not settlement and must never refund/release that sent permit.
            try { await lease.DisposeAsync(); }
            catch (Exception) { /* Expected fail-closed host quarantine; no sensitive exception escapes. */ }
        }

        private static async Task SettleLateAsync(Task<bool> pending, IAsyncEnumerator<ChatResponseUpdate> iterator,
            IOctoberProviderAttemptLease lease, TimeSpan wait, AttemptTiming timing)
        {
            try
            {
                try { await pending; } catch (Exception) { }
                await iterator.DisposeAsync();
                await lease.SettleAsync(new(null, null, null, wait, timing.Elapsed, ProviderAttemptOutcome.Cancelled));
                await lease.DisposeAsync();
            }
            catch (Exception) { /* An unconfirmed cleanup retains its permit and cannot authorize more work. */ }
        }

        private static async Task SettleAfterCleanupAsync(Task cleanup, IOctoberProviderAttemptLease lease,
            TimeSpan wait, AttemptTiming timing)
        {
            try
            {
                await cleanup;
                await lease.SettleAsync(new(null, null, null, wait, timing.Elapsed, ProviderAttemptOutcome.Cancelled));
                await lease.DisposeAsync();
            }
            catch (Exception) { /* Fail closed: never release an unsettled transport. */ }
        }

        private sealed class AttemptTiming
        {
            private long _dispatched;
            internal void MarkDispatched(long timestamp) => Interlocked.CompareExchange(ref _dispatched, timestamp, 0);
            internal TimeSpan Elapsed => Interlocked.Read(ref _dispatched) is var timestamp && timestamp > 0
                ? Stopwatch.GetElapsedTime(timestamp) : TimeSpan.Zero;
        }
    }

    private static CompletedFileProposal ParseProposal(string json, OctoberFileWorkerRequest request)
    {
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        JsonElement root = document.RootElement;
        Fields(root, "units", "observations", "uncertainties");
        ImmutableArray<SemanticUnitProposal> units = Array(root, "units").Select(unit =>
        {
            Fields(unit, "semanticSelector", "parentSelector", "title", "anchors", "sections");
            ImmutableArray<DocumentSection> sections = Array(unit, "sections").Select(section =>
            {
                Fields(section, "sectionId", "ordinal", "title", "claims");
                ImmutableArray<QualifiedClaimGroup> claims = Array(section, "claims").Select(claim =>
                {
                    Fields(claim, "claimGroupId", "text", "qualification", "evidence", "limitations");
                    return new QualifiedClaimGroup(Text(claim, "claimGroupId"), Text(claim, "text"),
                        Qualification(claim), Evidence(claim, request), Strings(claim, "limitations"));
                }).ToImmutableArray();
                return new DocumentSection(Text(section, "sectionId"), section.GetProperty("ordinal").GetInt32(), Text(section, "title"), claims);
            }).ToImmutableArray();
            string title = Text(unit, "title");
            return new SemanticUnitProposal(Text(unit, "semanticSelector"), NullableText(unit, "parentSelector"), title,
                OctoberKnowledgePolicy.CreateContentJson(title, sections),
                Array(unit, "anchors").Select(anchor => Anchor(anchor, request)).ToImmutableArray(), sections);
        }).ToImmutableArray();
        ImmutableArray<CommunicationObservation> observations = Array(root, "observations").Select(observation =>
        {
            Fields(observation, "observationKey", "ownerSelector", "role", "mechanism", "operation", "logicalNamespace", "literalsOrExpressions", "qualification", "evidence");
            return new CommunicationObservation(Text(observation, "observationKey"), Text(observation, "ownerSelector"),
                Text(observation, "role"), Text(observation, "mechanism"), Text(observation, "operation"), NullableText(observation, "logicalNamespace"),
                Strings(observation, "literalsOrExpressions"), Qualification(observation), Evidence(observation, request));
        }).ToImmutableArray();
        ImmutableArray<Uncertainty> uncertainties = Array(root, "uncertainties").Select(uncertainty =>
        {
            Fields(uncertainty, "key", "explanation", "evidence", "limitations");
            return new Uncertainty(Text(uncertainty, "key"), Text(uncertainty, "explanation"), Evidence(uncertainty, request), Strings(uncertainty, "limitations"));
        }).ToImmutableArray();
        if (units.IsEmpty && uncertainties.IsEmpty) throw new InvalidDataException("Zero-unit analysis requires a reason.");
        var proposal = new CompletedFileProposal(request.Binding, request.AdmissionId, request.AttemptId, request.Source.Origin,
            string.Empty, units, observations, uncertainties, request.Contributors,
            new FileCoverage(request.AdmissionId, request.Source.Origin, units.IsEmpty ? CoverageDisposition.CompletedZeroUnit : CoverageDisposition.Accepted,
                units.IsEmpty ? uncertainties[0].Explanation : null));
        proposal = proposal with { CanonicalProposalDigest = OctoberKnowledgePolicy.ComputeProposalDigest(proposal) };
        if (!OctoberKnowledgePolicy.Validate(proposal, new(request.Source.Origin, request.Source.LineCount)))
            throw new InvalidDataException("Worker proposal validation failed.");
        return proposal;
    }

    private static ImmutableArray<EvidenceReference> Evidence(JsonElement parent, OctoberFileWorkerRequest request)
        => Array(parent, "evidence").Select(value =>
        {
            if (value.TryGetProperty("advisoryIndex", out JsonElement index))
            {
                Fields(value, "advisoryIndex");
                int ordinal = index.GetInt32();
                if (ordinal < 0 || ordinal >= request.Advisory.Length) throw new InvalidDataException("Invalid advisory reference.");
                Contributor contributor = request.Advisory[ordinal].Contributor;
                return new EvidenceReference(null, null, null, contributor.AdvisoryRecordId, contributor.ContentDigest);
            }
            SourceAnchor anchor = Anchor(value, request);
            return new EvidenceReference(null, null, anchor, null, request.Source.Origin.SourceDigest);
        }).ToImmutableArray();

    private static SourceAnchor Anchor(JsonElement value, OctoberFileWorkerRequest request)
    {
        Fields(value, "startLine", "endLine");
        return new(request.Source.Origin, value.GetProperty("startLine").GetInt32(), value.GetProperty("endLine").GetInt32());
    }

    private static ClaimQualification Qualification(JsonElement value)
    {
        string text = Text(value, "qualification");
        if (!Enum.TryParse(text, ignoreCase: false, out ClaimQualification result) || result == ClaimQualification.Mixed ||
            result.ToString() != text) throw new InvalidDataException("Invalid claim qualification.");
        return result;
    }

    private static IEnumerable<JsonElement> Array(JsonElement value, string name) => value.GetProperty(name).EnumerateArray();
    private static ImmutableArray<string> Strings(JsonElement value, string name) => Array(value, name).Select(item => item.GetString() ?? throw new InvalidDataException("Missing text.")).ToImmutableArray();
    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? throw new InvalidDataException("Missing text.");
    private static string? NullableText(JsonElement value, string name) => value.GetProperty(name).ValueKind == JsonValueKind.Null ? null : Text(value, name);

    private static void Fields(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected an object.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
            if (!names.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
                throw new InvalidDataException("Unknown or repeated proposal field.");
        if (seen.Count != names.Length) throw new InvalidDataException("Missing proposal field.");
    }
}
