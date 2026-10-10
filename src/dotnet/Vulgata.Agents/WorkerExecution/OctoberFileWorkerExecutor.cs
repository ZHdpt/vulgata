using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
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
public enum OctoberWorkerFailureStage
{
    Admission, StreamTerminal, ProposalParse, ProposalValidation, Transport, HostSealing,
    SourceVerification, ToolInvocation, ToolResultSerialization, AttemptSettlement, AgentOrchestration
}
public enum OctoberWorkerBudgetFailure { OutputBytes, ToolCalls, StreamUpdates }
public enum OctoberWorkerParseFailure { Syntax, RootShape, ClosedFields, ValueShape, Qualification, EvidenceIndex }
public enum OctoberWorkerCaptureStatus { Captured, TooLarge, Unencodable, Suppressed, Failed, AlreadyAttempted, CleanupUnconfirmed }
public enum OctoberWorkerParseSlot
{
    Root, Units, Sections, Claims, Anchors, Observations, Uncertainties, Evidence,
    SemanticSelector, ParentSelector, Title, SectionId, Ordinal, ClaimGroupId, Text,
    Qualification, Limitations, ObservationKey, OwnerSelector, Role, Mechanism,
    Operation, LogicalNamespace, LiteralsOrExpressions, Key, Explanation,
    AdvisoryIndex, StartLine, EndLine
}
public sealed record OctoberWorkerParseDiagnosticSnapshot(OctoberWorkerParseFailure? Category, OctoberWorkerParseSlot? Slot);
public enum OctoberWorkerTerminalFailure
{
    ContentAfterFinish, ConflictingFinish, NonAssistantRole, ToolCallError, MissingToolCallId,
    UnknownTool, RepeatedToolCallId, MissingFinish, UnsupportedFinish, ToolFinishWithoutCalls,
    StopWithCalls, IncompleteTerminal, IncompleteCoverage
}

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
    public OctoberWorkerFailureStage? FailureStage { get; init; }
    public OctoberWorkerTerminalFailure? TerminalFailure { get; init; }
    public OctoberWorkerBudgetFailure? BudgetFailure { get; init; }
    public OctoberWorkerParseDiagnosticSnapshot? ParseFailure { get; init; }
    public OctoberWorkerCaptureStatus? CaptureStatus { get; init; }
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
    private readonly Func<string, CancellationToken, Task<OctoberWorkerCaptureStatus>>? _captureParseFailure;
    private int _used;

    public OctoberFileWorkerExecutor(IChatClient client, IOctoberWorkerExecutionScope scope,
        Func<string, CancellationToken, Task<OctoberWorkerCaptureStatus>>? captureParseFailure = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _captureParseFailure = captureParseFailure;
    }

    public async Task<OctoberFileWorkerResult> ExecuteAsync(OctoberFileWorkerRequest request,
        IReadOnlyList<AIFunction> tools, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _used, 1) != 0)
            return new(OctoberFileWorkerStatus.InvalidInput) { FailureStage = OctoberWorkerFailureStage.Admission };
        WorkerClient? adapter = null;
        int covered = 0;
        OctoberWorkerFailureStage? stage = null;
        OctoberWorkerTerminalFailure? terminalFailure = null;
        OctoberWorkerCaptureStatus? captureStatus = null;
        var observations = new ExceptionObservation();
        var parseObservation = new ParseObservation();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ValidRequest(request, tools) || _client.GetService(typeof(FunctionInvokingChatClient)) is not null)
                return Failed(OctoberFileWorkerStatus.InvalidInput) with { FailureStage = OctoberWorkerFailureStage.Admission };
            if (_client is IOctoberWorkerAttemptClient selectedClient &&
                !string.Equals(selectedClient.ProviderProfileId, request.ProviderProfileId, StringComparison.Ordinal))
                return Failed(OctoberFileWorkerStatus.InvalidInput) with { FailureStage = OctoberWorkerFailureStage.Admission };
            string prompt = LoadPrompt();
            byte[] source = Utf8.GetBytes(request.Source.Text);
            if (source.LongLength != request.Source.ByteLength ||
                !string.Equals(Convert.ToHexStringLower(SHA256.HashData(source)), request.Source.Origin.SourceDigest, StringComparison.OrdinalIgnoreCase) ||
                CountLines(request.Source.Text) != request.Source.LineCount)
                return Failed(OctoberFileWorkerStatus.InvalidInput) with { FailureStage = OctoberWorkerFailureStage.Admission };
            stage = null;
            if (!await observations.RunAsync(() => request.Source.VerifyCurrentAsync(cancellationToken), OctoberWorkerFailureStage.SourceVerification))
                return Failed(OctoberFileWorkerStatus.SourceChanged);
            stage = OctoberWorkerFailureStage.Admission;
            List<string> windows = SplitWindows(source, request.Limits.MaximumWindowUtf8Bytes);
            if (windows.Count > request.Limits.MaximumContinuationWindows)
                return Failed(OctoberFileWorkerStatus.BudgetExceeded);

            adapter = new WorkerClient(_client, _scope, request, tools, observations);
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
            int sourceLine = 1, sourceColumn = 1;
            bool previousCarriageReturn = false;
            for (int index = 0; index < windows.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int windowStartLine = sourceLine, windowStartColumn = sourceColumn;
                foreach (Rune scalar in windows[index].EnumerateRunes())
                {
                    if (scalar.Value == '\r') { sourceLine++; sourceColumn = 1; previousCarriageReturn = true; }
                    else if (scalar.Value == '\n')
                    {
                        if (!previousCarriageReturn) sourceLine++;
                        sourceColumn = 1;
                        previousCarriageReturn = false;
                    }
                    else { sourceColumn++; previousCarriageReturn = false; }
                }
                string input = JsonSerializer.Serialize(new
                {
                    primaryRelativePath = request.Source.Origin.RelativePath,
                    wholeFileLineCount = request.Source.LineCount,
                    windowStartLine,
                    windowStartColumn,
                    windowEndLineExclusive = sourceLine,
                    windowEndColumnExclusive = sourceColumn,
                    windowOrdinal = index + 1,
                    windowCount = windows.Count,
                    byteStart = covered,
                    sourceText = windows[index],
                    finalWindow = index == windows.Count - 1,
                    advisory = request.Advisory.Select((item, ordinal) => new { advisoryIndex = ordinal, text = item.Text })
                });
                history.Add(new ChatMessage(ChatRole.User, input));
                stage = null;
                AgentResponse response = await observations.RunAsync(async () => await agent.RunStreamingAsync(history, cancellationToken: cancellationToken)
                    .ToAgentResponseAsync(cancellationToken), OctoberWorkerFailureStage.AgentOrchestration);
                if (adapter.Failure is { } failure) throw new WorkerFailure(failure);
                stage = OctoberWorkerFailureStage.StreamTerminal;
                if (adapter.Finish != ChatFinishReason.Stop || adapter.PendingCalls != 0)
                    throw new WorkerFailure(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.IncompleteTerminal);
                stage = null;
                if (!await observations.RunAsync(() => request.Source.VerifyCurrentAsync(cancellationToken), OctoberWorkerFailureStage.SourceVerification))
                    throw new WorkerFailure(OctoberFileWorkerStatus.SourceChanged);
                covered = checked(covered + Utf8.GetByteCount(windows[index]));
                history.AddRange(response.Messages);
            }
            stage = OctoberWorkerFailureStage.StreamTerminal;
            if (covered != source.Length) throw new WorkerFailure(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.IncompleteCoverage);
            stage = OctoberWorkerFailureStage.ProposalParse;
            CompletedFileProposal proposal;
            try { proposal = ParseProposal(adapter.LastText, request, ref stage, parseObservation); }
            catch (Exception) when (stage == OctoberWorkerFailureStage.ProposalParse)
            {
                captureStatus = await CaptureParseFailureAsync(adapter.LastText, request.Source, cancellationToken);
                throw;
            }
            return new(OctoberFileWorkerStatus.Completed, proposal, covered, adapter.Rounds, adapter.SettledTools);
        }
        catch (OperationCanceledException exception)
        {
            return Failed(OctoberFileWorkerStatus.Cancelled, exception);
        }
        catch (WorkerFailure failure) { terminalFailure = failure.TerminalFailure; return Failed(failure.Status, failure); }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidDataException or OverflowException)
        {
            return Failed(adapter?.Failure ?? OctoberFileWorkerStatus.InvalidOutput, exception);
        }
        catch (Exception exception) { return Failed(adapter?.Failure ?? OctoberFileWorkerStatus.ProviderFailed, exception); }

        OctoberFileWorkerResult Failed(OctoberFileWorkerStatus status, Exception? exception = null)
            => new(status, null, covered, adapter?.Rounds ?? 0, adapter?.SettledTools ?? 0)
            {
                RetryDisposition = status == OctoberFileWorkerStatus.ProviderFailed
                    ? adapter?.RetryDisposition ?? OctoberWorkerRetryDisposition.None : OctoberWorkerRetryDisposition.None,
                FailureStage = observations.For(exception) ?? stage ?? adapter?.FailureStage,
                TerminalFailure = terminalFailure ?? adapter?.TerminalFailure,
                BudgetFailure = status == OctoberFileWorkerStatus.BudgetExceeded ? adapter?.BudgetFailure : null,
                ParseFailure = Enum.IsDefined(status) && status != OctoberFileWorkerStatus.Completed &&
                    (observations.For(exception) ?? stage ?? adapter?.FailureStage) == OctoberWorkerFailureStage.ProposalParse
                    ? parseObservation.First : null,
                CaptureStatus = Enum.IsDefined(status) && status != OctoberFileWorkerStatus.Completed &&
                    (observations.For(exception) ?? stage ?? adapter?.FailureStage) == OctoberWorkerFailureStage.ProposalParse
                    ? captureStatus : null
            };
    }

    private async Task<OctoberWorkerCaptureStatus?> CaptureParseFailureAsync(string text,
        IOctoberVerifiedFileFrame source, CancellationToken token)
    {
        if (_captureParseFailure is null) return null;
        try
        {
            if (Utf8.GetByteCount(text) > 262144) return OctoberWorkerCaptureStatus.TooLarge;
        }
        catch (EncoderFallbackException) { return OctoberWorkerCaptureStatus.Unencodable; }
        catch (Exception) { return OctoberWorkerCaptureStatus.Failed; }
        try
        {
            if (token.IsCancellationRequested || !await source.VerifyCurrentAsync(token) || token.IsCancellationRequested)
                return OctoberWorkerCaptureStatus.Suppressed;
        }
        catch (Exception) { return OctoberWorkerCaptureStatus.Suppressed; }
        try
        {
            // The original Worker owns the actual sink task through settlement;
            // cancellation cannot abandon a callback that may still write.
            token.ThrowIfCancellationRequested();
            OctoberWorkerCaptureStatus status = await _captureParseFailure(text, token);
            return Enum.IsDefined(status) ? status : null;
        }
        catch (OperationCanceledException) { return OctoberWorkerCaptureStatus.Suppressed; }
        catch (Exception) { return OctoberWorkerCaptureStatus.Failed; }
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

    private sealed class WorkerFailure(OctoberFileWorkerStatus status, OctoberWorkerTerminalFailure? terminalFailure = null) : Exception("Worker execution stopped.")
    {
        internal OctoberFileWorkerStatus Status { get; } = status;
        internal OctoberWorkerTerminalFailure? TerminalFailure { get; } = terminalFailure;
    }

    private sealed class ExceptionObservation
    {
        private Exception? _exception;
        private OctoberWorkerFailureStage? _stage;

        internal OctoberWorkerFailureStage? For(Exception? exception) =>
            exception is not null && ReferenceEquals(exception, _exception) ? _stage : null;

        internal void Record(Exception exception, OctoberWorkerFailureStage? stage)
        {
            if (stage is null || stage == OctoberWorkerFailureStage.AgentOrchestration &&
                (ReferenceEquals(exception, _exception) || exception is WorkerFailure)) return;
            _exception = exception;
            _stage = stage;
        }

        internal T Run<T>(Func<T> operation, OctoberWorkerFailureStage stage)
        {
            try { return operation(); }
            catch (Exception exception) { Record(exception, stage); throw; }
        }

        internal async Task<T> RunAsync<T>(Func<Task<T>> operation, OctoberWorkerFailureStage stage)
        {
            try { return await operation(); }
            catch (Exception exception) { Record(exception, stage); throw; }
        }

        internal async Task RunAsync(Func<Task> operation, OctoberWorkerFailureStage stage)
        {
            try { await operation(); }
            catch (Exception exception) { Record(exception, stage); throw; }
        }
    }

    private sealed class WorkerClient(IChatClient client, IOctoberWorkerExecutionScope scope,
        OctoberFileWorkerRequest request, IReadOnlyList<AIFunction> tools, ExceptionObservation observations) : DelegatingChatClient(client)
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
        internal OctoberWorkerFailureStage? FailureStage { get; private set; }
        internal OctoberWorkerTerminalFailure? TerminalFailure { get; private set; }
        internal OctoberWorkerBudgetFailure? BudgetFailure { get; private set; }
        private OctoberWorkerFailureStage? _observedStage;

        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw Fail(OctoberFileWorkerStatus.InvalidInput);

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _observedStage = OctoberWorkerFailureStage.StreamTerminal;
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null || _pending.Count != 0)
                throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.IncompleteTerminal);
            _observedStage = OctoberWorkerFailureStage.Admission;
            if (Rounds >= request.Limits.MaximumProviderRounds) throw Fail(OctoberFileWorkerStatus.BudgetExceeded);
            _observedStage = null;
            if (!await observations.RunAsync(() => request.Source.VerifyCurrentAsync(cancellationToken), OctoberWorkerFailureStage.SourceVerification)) throw Fail(OctoberFileWorkerStatus.SourceChanged);
            _observedStage = OctoberWorkerFailureStage.Admission;
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
                _observedStage = null;
                if (!await observations.RunAsync(() => request.Source.VerifyCurrentAsync(linked.Token), OctoberWorkerFailureStage.SourceVerification)) throw Fail(OctoberFileWorkerStatus.SourceChanged);
                linked.Token.ThrowIfCancellationRequested();
                Rounds++;
                if (client is not IOctoberWorkerAttemptClient)
                {
                    (lease as IOctoberWorkerDispatchObserver)?.RecordDispatch();
                    timing.MarkDispatched(Stopwatch.GetTimestamp());
                }
                _observedStage = OctoberWorkerFailureStage.Transport;
                IAsyncEnumerable<ChatResponseUpdate> updates = client is IOctoberWorkerAttemptClient admittedClient
                    ? admittedClient.GetAttemptStreamingResponseAsync(snapshot, bounded, lease, linked.Token, timing.MarkDispatched)
                    : client.GetStreamingResponseAsync(snapshot, bounded, linked.Token);
                iterator = updates.GetAsyncEnumerator(linked.Token);
                while (true)
                {
                    _observedStage = OctoberWorkerFailureStage.Transport;
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
                    _observedStage = OctoberWorkerFailureStage.StreamTerminal;
                    ChatResponseUpdate update = iterator.Current;
                    // The qualified OpenAI adapter emits complete calls in one
                    // aggregate after its empty ToolCalls finish and usage updates.
                    // Validate that narrow envelope, then apply every call guard.
                    bool terminalCallAggregate = finish == ChatFinishReason.ToolCalls &&
                        update.FinishReason == ChatFinishReason.ToolCalls && calls.Count == 0 &&
                        update.Contents.Any(content => content is FunctionCallContent { InformationalOnly: false }) &&
                        update.Contents.All(content => content is UsageContent or FunctionCallContent { InformationalOnly: false });
                    if (finish is not null && update.Contents.Any(content => content is not UsageContent) && !terminalCallAggregate)
                        throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.ContentAfterFinish);
                    if (finish is not null && update.FinishReason is { } repeated && repeated != finish)
                        throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.ConflictingFinish);
                    if (update.Role is { } role && role != ChatRole.Assistant)
                        throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.NonAssistantRole);
                    foreach (AIContent content in update.Contents)
                    {
                        if (content is TextContent value)
                        {
                            ChargeOutput(value.Text);
                            ChargeMetadata(value);
                            text.Append(value.Text);
                        }
                        else if (content is TextReasoningContent reasoning && content.GetType() == typeof(TextReasoningContent))
                        {
                            ChargeOutput(reasoning.Text);
                            if (reasoning.ProtectedData is not null) ChargeOutput(reasoning.ProtectedData);
                            ChargeMetadata(reasoning);
                        }
                        else if (content is FunctionCallContent call && !call.InformationalOnly)
                        {
                            ChargeOutput(MeasureOutput(call.Arguments));
                            if (call.Exception is not null)
                                throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.ToolCallError);
                            if (string.IsNullOrWhiteSpace(call.CallId))
                                throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.MissingToolCallId);
                            if (!tools.Any(tool => tool.Name == call.Name))
                                throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.UnknownTool);
                            if (!_seen.Add(call.CallId))
                                throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.RepeatedToolCallId);
                            if (++_toolCount > request.Limits.MaximumToolCalls)
                                throw Fail(OctoberFileWorkerStatus.BudgetExceeded, budgetFailure: OctoberWorkerBudgetFailure.ToolCalls);
                            var arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                JsonSerializer.Serialize(call.Arguments ?? new Dictionary<string, object?>()))!;
                            var copy = new FunctionCallContent(call.CallId, call.Name, arguments);
                            calls.Add(copy);
                        }
                        else if (content is UsageContent usage)
                        {
                            inputTokens = usage.Details.InputTokenCount;
                            actualOutputTokens = usage.Details.OutputTokenCount;
                            if (content.GetType() != typeof(UsageContent) || usage.Details.GetType() != typeof(UsageDetails))
                                throw new NotSupportedException("Extended usage payload cannot be measured.");
                            ChargeMetadata(usage);
                            if (usage.Details.AdditionalCounts is { Count: > 0 } counts) ChargeOutput(MeasureOutput(counts));
                        }
                        else if (content is FunctionCallContent)
                            ChargeOutput(MeasureOutput(content));
                        else
                            throw new NotSupportedException("Unknown output payload cannot be measured.");
                    }
                    if (update.FinishReason is { } currentFinish) finish = currentFinish;
                    if (buffered.Count >= 8192)
                        throw Fail(OctoberFileWorkerStatus.BudgetExceeded, budgetFailure: OctoberWorkerBudgetFailure.StreamUpdates);
                    buffered.Add(update);
                }
                _observedStage = OctoberWorkerFailureStage.StreamTerminal;
                if (finish is null)
                    throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.MissingFinish);
                if (finish != ChatFinishReason.Stop && finish != ChatFinishReason.ToolCalls)
                    throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.UnsupportedFinish);
                if (finish == ChatFinishReason.ToolCalls && calls.Count == 0)
                    throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.ToolFinishWithoutCalls);
                if (finish == ChatFinishReason.Stop && calls.Count != 0)
                    throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.StopWithCalls);
                _observedStage = null;
                if (!await observations.RunAsync(() => request.Source.VerifyCurrentAsync(linked.Token), OctoberWorkerFailureStage.SourceVerification)) throw Fail(OctoberFileWorkerStatus.SourceChanged);
                success = true;
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                FailureStage ??= _observedStage;
                observations.Record(exception, _observedStage);
                transient = true;
                throw;
            }
            catch (Exception exception)
            {
                FailureStage ??= _observedStage;
                observations.Record(exception, _observedStage);
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
                            FailureStage = OctoberWorkerFailureStage.Transport;
                        }
                        catch (Exception)
                        {
                            deferred = true;
                            await QuarantineAsync(lease);
                            Failure = OctoberFileWorkerStatus.ProviderFailed;
                            FailureStage = OctoberWorkerFailureStage.Transport;
                        }
                    }
                    if (!deferred)
                    {
                        TimeSpan executionTime = timing.Elapsed;
                        ProviderAttemptOutcome outcome = success ? ProviderAttemptOutcome.Completed :
                            linked.IsCancellationRequested ? ProviderAttemptOutcome.Cancelled : ProviderAttemptOutcome.Failed;
                        bool retryCandidate = false;
                        _allUsageKnown &= inputTokens is >= 0 && actualOutputTokens is >= 0 &&
                            inputTokens.Value <= admissionRequest.MaximumTokens &&
                            actualOutputTokens.Value <= admissionRequest.MaximumTokens - inputTokens.Value;
                        if (transient && _allUsageKnown && _pending.Count == 0 && calls.Count == 0 &&
                            Failure is null && !linked.IsCancellationRequested)
                        {
                            Task<bool>? verification = null;
                            try
                            {
                                verification = request.Source.VerifyCurrentAsync(linked.Token);
                                bool current = await verification.WaitAsync(linked.Token);
                                if (current && !linked.IsCancellationRequested)
                                    retryCandidate = true;
                            }
                            catch (Exception)
                            {
                                if (verification is not null) _ = ObserveVerificationAsync(verification);
                            }
                        }
                        // Physical cleanup is confirmed. Preserve the original lease deadline through
                        // verification, then account observed facts even when verification was cancelled.
                        observations.Run(() =>
                        {
                            (lease as IOctoberWorkerDispatchObserver)?.CompleteDispatchObservation();
                            return true;
                        }, OctoberWorkerFailureStage.AttemptSettlement);
                        try
                        {
                            await observations.RunAsync(() => lease.SettleAsync(new(inputTokens, actualOutputTokens, null, wait,
                                executionTime, outcome), CancellationToken.None), OctoberWorkerFailureStage.AttemptSettlement);
                        }
                        finally { await observations.RunAsync(() => lease.DisposeAsync().AsTask(), OctoberWorkerFailureStage.AttemptSettlement); }
                        if (retryCandidate && !linked.IsCancellationRequested)
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

        private static async Task ObserveVerificationAsync(Task<bool> verification)
        {
            try { await verification; }
            catch (Exception) { }
        }

        internal async ValueTask<object?> InvokeAsync(FunctionInvocationContext context, CancellationToken token)
        {
            _observedStage = null;
            if (!_pending.TryGetValue(context.CallContent.CallId, out FunctionCallContent? call) || call.Name != context.Function.Name)
                throw Fail(OctoberFileWorkerStatus.InvalidOutput);
            if (!await observations.RunAsync(() => request.Source.VerifyCurrentAsync(token), OctoberWorkerFailureStage.SourceVerification)) throw Fail(OctoberFileWorkerStatus.SourceChanged);
            AIFunction function = tools.Single(tool => tool.Name == call.Name);
            bool unsupported = function.JsonSchema.TryGetProperty("properties", out JsonElement properties) &&
                call.Arguments?.Keys.Any(key => !properties.TryGetProperty(key, out _)) == true;
            object? result = unsupported ? new { status = "InvalidArguments" } :
                await observations.RunAsync(() => function.InvokeAsync(new AIFunctionArguments(call.Arguments ?? new Dictionary<string, object?>()), token).AsTask(), OctoberWorkerFailureStage.ToolInvocation);
            ChargeOutput(observations.Run(() => MeasureOutput(result), OctoberWorkerFailureStage.ToolResultSerialization));
            if (!await observations.RunAsync(() => request.Source.VerifyCurrentAsync(token), OctoberWorkerFailureStage.SourceVerification)) throw Fail(OctoberFileWorkerStatus.SourceChanged);
            _pending.Remove(call.CallId);
            SettledTools++;
            return result;
        }

        private void ChargeMetadata(AIContent content)
        {
            if (content.Annotations is { Count: > 0 } annotations) ChargeOutput(MeasureOutput(annotations));
            if (content.AdditionalProperties is { Count: > 0 } properties) ChargeOutput(MeasureOutput(properties));
        }

        private void ChargeOutput(string value) => ChargeOutput(Utf8.GetByteCount(value));

        private void ChargeOutput(int bytes)
        {
            if (bytes > request.Limits.MaximumOutputUtf8Bytes - _outputBytes)
                throw Fail(OctoberFileWorkerStatus.BudgetExceeded, budgetFailure: OctoberWorkerBudgetFailure.OutputBytes);
            _outputBytes += bytes;
        }

        private int MeasureOutput<T>(T value)
        {
            int remaining = request.Limits.MaximumOutputUtf8Bytes - _outputBytes;
            try
            {
                var destination = new OutputMeasurementBuffer(remaining);
                var resolver = new DefaultJsonTypeInfoResolver();
                resolver.Modifiers.Add(typeInfo =>
                {
                    // Measurement does not invoke host-defined converter Write
                    // methods. Metadata resolution itself remains trusted host code.
                    if (!SupportedOutputConverter(typeInfo.Converter) ||
                        typeInfo.Properties.Any(property => property.CustomConverter is { } converter && !SupportedOutputConverter(converter)))
                        throw new NotSupportedException("Custom output converters cannot be measured.");
                });
                var options = new JsonSerializerOptions { TypeInfoResolver = resolver };
                options.Converters.Add(new OutputStringConverter(remaining));
                options.Converters.Add(new OutputElementConverter(remaining));
                using var writer = new Utf8JsonWriter(destination);
                JsonSerializer.Serialize(writer, value, options);
                writer.Flush();
                return destination.Written;
            }
            catch (OutputMeasurementLimit)
            {
                // Keep the existing output-budget failure outside the serializer
                // observation; malformed JSON retains its serialization attribution.
                return remaining + 1;
            }
        }

        private static bool SupportedOutputConverter(JsonConverter converter) =>
            converter is OutputStringConverter or OutputElementConverter ||
            converter.GetType().Assembly == typeof(JsonSerializer).Assembly;

        private sealed class OutputMeasurementLimit : Exception;

        private sealed class OutputMeasurementBuffer(int allowance) : IBufferWriter<byte>
        {
            // Reservations can exceed actual JSON bytes (escaping/transcoding).
            // They are bounded scratch, never charged as payload.
            private readonly int _maximumReservation = checked(18 * allowance + 4096);
            private byte[] _buffer = [];
            internal int Written { get; private set; }

            public void Advance(int count)
            {
                if (count < 0 || count > _buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
                if (count > allowance - Written) throw new OutputMeasurementLimit();
                Written += count;
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                int size = Math.Max(sizeHint, 256);
                if (size > _maximumReservation) throw new OutputMeasurementLimit();
                if (_buffer.Length < size) _buffer = new byte[size];
                return _buffer;
            }

            public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
        }

        private sealed class OutputStringConverter(int allowance) : JsonConverter<string>
        {
            public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => throw new NotSupportedException();

            public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            {
                Check(value);
                writer.WriteStringValue(value);
            }

            public override void WriteAsPropertyName(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            {
                Check(value);
                writer.WritePropertyName(value);
            }

            private void Check(string value)
            {
                // Reject before Utf8JsonWriter rents an escaping buffer for a token.
                if (Utf8.GetByteCount(value) > allowance) throw new OutputMeasurementLimit();
            }
        }

        private sealed class OutputElementConverter : JsonConverter<JsonElement>
        {
            private readonly JsonSerializerOptions _validation;
            internal OutputElementConverter(int allowance)
            {
                _validation = new();
                _validation.Converters.Add(new OutputElementValidation(checked(6 * allowance + 4096)));
            }

            public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => throw new NotSupportedException();

            public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
            {
                // Deserialize reads the element's retained UTF-8 span. This probe
                // creates no DOM/string copy before bounding token scratch and work.
                _ = value.Deserialize<bool>(_validation);
                value.WriteTo(writer);
            }
        }

        private sealed class OutputElementValidation(int maximumRawBytes) : JsonConverter<bool>
        {
            public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                long start = reader.TokenStartIndex;
                int depth = reader.CurrentDepth;
                Check(ref reader, start);
                if (reader.TokenType is not (JsonTokenType.StartObject or JsonTokenType.StartArray)) return true;
                while (reader.Read())
                {
                    Check(ref reader, start);
                    if (reader.CurrentDepth == depth && reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
                        return true;
                }
                throw new JsonException("Incomplete output metadata.");
            }

            private void Check(ref Utf8JsonReader reader, long start)
            {
                long tokenBytes = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
                if (reader.BytesConsumed - start > maximumRawBytes || tokenBytes > maximumRawBytes)
                    throw new OutputMeasurementLimit();
            }

            public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
                => throw new NotSupportedException();
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

        private WorkerFailure Fail(OctoberFileWorkerStatus status, OctoberWorkerTerminalFailure? terminalFailure = null,
            OctoberWorkerBudgetFailure? budgetFailure = null)
        {
            Failure = status;
            FailureStage ??= _observedStage;
            TerminalFailure ??= terminalFailure;
            BudgetFailure ??= budgetFailure;
            var failure = new WorkerFailure(status, TerminalFailure);
            observations.Record(failure, _observedStage);
            return failure;
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
                (lease as IOctoberWorkerDispatchObserver)?.CompleteDispatchObservation();
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
                (lease as IOctoberWorkerDispatchObserver)?.CompleteDispatchObservation();
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

    private static CompletedFileProposal ParseProposal(string json, OctoberFileWorkerRequest request,
        ref OctoberWorkerFailureStage? stage, ParseObservation parse)
    {
        using JsonDocument document = parse.Read(() => JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 }),
            OctoberWorkerParseFailure.Syntax, OctoberWorkerParseSlot.Root);
        JsonElement root = document.RootElement;
        Fields(root, parse, OctoberWorkerParseSlot.Root, "units", "observations", "uncertainties");
        ImmutableArray<SemanticUnitProposal> units = Array(root, "units", parse, OctoberWorkerParseSlot.Units).Select(unit =>
        {
            Fields(unit, parse, OctoberWorkerParseSlot.Units, "semanticSelector", "parentSelector", "title", "anchors", "sections");
            ImmutableArray<DocumentSection> sections = Array(unit, "sections", parse, OctoberWorkerParseSlot.Sections).Select(section =>
            {
                Fields(section, parse, OctoberWorkerParseSlot.Sections, "sectionId", "ordinal", "title", "claims");
                ImmutableArray<QualifiedClaimGroup> claims = Array(section, "claims", parse, OctoberWorkerParseSlot.Claims).Select(claim =>
                {
                    Fields(claim, parse, OctoberWorkerParseSlot.Claims, "claimGroupId", "text", "qualification", "evidence", "limitations");
                    return new QualifiedClaimGroup(Text(claim, "claimGroupId", parse, OctoberWorkerParseSlot.ClaimGroupId),
                        Text(claim, "text", parse, OctoberWorkerParseSlot.Text),
                        Qualification(claim, parse), Evidence(claim, request, parse),
                        Strings(claim, "limitations", parse, OctoberWorkerParseSlot.Limitations));
                }).ToImmutableArray();
                return new DocumentSection(Text(section, "sectionId", parse, OctoberWorkerParseSlot.SectionId),
                    parse.Read(() => section.GetProperty("ordinal").GetInt32(), OctoberWorkerParseFailure.ValueShape, OctoberWorkerParseSlot.Ordinal),
                    Text(section, "title", parse, OctoberWorkerParseSlot.Title), claims);
            }).ToImmutableArray();
            string title = Text(unit, "title", parse, OctoberWorkerParseSlot.Title);
            return new SemanticUnitProposal(Text(unit, "semanticSelector", parse, OctoberWorkerParseSlot.SemanticSelector),
                NullableText(unit, "parentSelector", parse, OctoberWorkerParseSlot.ParentSelector), title,
                OctoberKnowledgePolicy.CreateContentJson(title, sections),
                Array(unit, "anchors", parse, OctoberWorkerParseSlot.Anchors)
                    .Select(anchor => Anchor(anchor, request, parse, OctoberWorkerParseSlot.Anchors)).ToImmutableArray(), sections);
        }).ToImmutableArray();
        ImmutableArray<CommunicationObservation> observations = Array(root, "observations", parse, OctoberWorkerParseSlot.Observations).Select(observation =>
        {
            Fields(observation, parse, OctoberWorkerParseSlot.Observations, "observationKey", "ownerSelector", "role", "mechanism", "operation", "logicalNamespace", "literalsOrExpressions", "qualification", "evidence");
            return new CommunicationObservation(Text(observation, "observationKey", parse, OctoberWorkerParseSlot.ObservationKey),
                Text(observation, "ownerSelector", parse, OctoberWorkerParseSlot.OwnerSelector),
                Text(observation, "role", parse, OctoberWorkerParseSlot.Role),
                Text(observation, "mechanism", parse, OctoberWorkerParseSlot.Mechanism),
                Text(observation, "operation", parse, OctoberWorkerParseSlot.Operation),
                NullableText(observation, "logicalNamespace", parse, OctoberWorkerParseSlot.LogicalNamespace),
                Strings(observation, "literalsOrExpressions", parse, OctoberWorkerParseSlot.LiteralsOrExpressions),
                Qualification(observation, parse), Evidence(observation, request, parse));
        }).ToImmutableArray();
        ImmutableArray<Uncertainty> uncertainties = Array(root, "uncertainties", parse, OctoberWorkerParseSlot.Uncertainties).Select(uncertainty =>
        {
            Fields(uncertainty, parse, OctoberWorkerParseSlot.Uncertainties, "key", "explanation", "evidence", "limitations");
            return new Uncertainty(Text(uncertainty, "key", parse, OctoberWorkerParseSlot.Key),
                Text(uncertainty, "explanation", parse, OctoberWorkerParseSlot.Explanation),
                Evidence(uncertainty, request, parse), Strings(uncertainty, "limitations", parse, OctoberWorkerParseSlot.Limitations));
        }).ToImmutableArray();
        stage = OctoberWorkerFailureStage.ProposalValidation;
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

    private static ImmutableArray<EvidenceReference> Evidence(JsonElement parent, OctoberFileWorkerRequest request, ParseObservation parse)
        => Array(parent, "evidence", parse, OctoberWorkerParseSlot.Evidence).Select(value =>
        {
            JsonElement index = default;
            if (parse.Read(() => value.TryGetProperty("advisoryIndex", out index), OctoberWorkerParseFailure.ValueShape, OctoberWorkerParseSlot.Evidence))
            {
                Fields(value, parse, OctoberWorkerParseSlot.Evidence, "advisoryIndex");
                int ordinal = parse.Read(() => index.GetInt32(), OctoberWorkerParseFailure.ValueShape, OctoberWorkerParseSlot.AdvisoryIndex);
                if (ordinal < 0 || ordinal >= request.Advisory.Length)
                {
                    parse.Record(OctoberWorkerParseFailure.EvidenceIndex, OctoberWorkerParseSlot.AdvisoryIndex);
                    throw new InvalidDataException("Invalid advisory reference.");
                }
                Contributor contributor = request.Advisory[ordinal].Contributor;
                return new EvidenceReference(null, null, null, contributor.AdvisoryRecordId, contributor.ContentDigest);
            }
            SourceAnchor anchor = Anchor(value, request, parse, OctoberWorkerParseSlot.Evidence);
            return new EvidenceReference(null, null, anchor, null, request.Source.Origin.SourceDigest);
        }).ToImmutableArray();

    private static SourceAnchor Anchor(JsonElement value, OctoberFileWorkerRequest request, ParseObservation parse, OctoberWorkerParseSlot container)
    {
        Fields(value, parse, container, "startLine", "endLine");
        return new(request.Source.Origin,
            parse.Read(() => value.GetProperty("startLine").GetInt32(), OctoberWorkerParseFailure.ValueShape, OctoberWorkerParseSlot.StartLine),
            parse.Read(() => value.GetProperty("endLine").GetInt32(), OctoberWorkerParseFailure.ValueShape, OctoberWorkerParseSlot.EndLine));
    }

    private static ClaimQualification Qualification(JsonElement value, ParseObservation parse)
    {
        string text = Text(value, "qualification", parse, OctoberWorkerParseSlot.Qualification);
        if (!Enum.TryParse(text, ignoreCase: false, out ClaimQualification result) || result == ClaimQualification.Mixed ||
            result.ToString() != text)
        {
            parse.Record(OctoberWorkerParseFailure.Qualification, OctoberWorkerParseSlot.Qualification);
            throw new InvalidDataException("Invalid claim qualification.");
        }
        return result;
    }

    private static IEnumerable<JsonElement> Array(JsonElement value, string name, ParseObservation parse, OctoberWorkerParseSlot slot)
        => parse.Read(() => value.GetProperty(name).EnumerateArray(), OctoberWorkerParseFailure.ValueShape, slot);
    private static ImmutableArray<string> Strings(JsonElement value, string name, ParseObservation parse, OctoberWorkerParseSlot slot)
        => Array(value, name, parse, slot).Select(item => parse.Read(() => item.GetString() ?? throw new InvalidDataException("Missing text."),
            OctoberWorkerParseFailure.ValueShape, slot)).ToImmutableArray();
    private static string Text(JsonElement value, string name, ParseObservation parse, OctoberWorkerParseSlot slot)
        => parse.Read(() => value.GetProperty(name).GetString() ?? throw new InvalidDataException("Missing text."), OctoberWorkerParseFailure.ValueShape, slot);
    private static string? NullableText(JsonElement value, string name, ParseObservation parse, OctoberWorkerParseSlot slot)
        => parse.Read(() => value.GetProperty(name).ValueKind == JsonValueKind.Null ? null : Text(value, name, parse, slot), OctoberWorkerParseFailure.ValueShape, slot);

    private static void Fields(JsonElement value, ParseObservation parse, OctoberWorkerParseSlot slot, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            parse.Record(slot == OctoberWorkerParseSlot.Root ? OctoberWorkerParseFailure.RootShape : OctoberWorkerParseFailure.ValueShape, slot);
            throw new InvalidDataException("Expected an object.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
            if (!names.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
            {
                parse.Record(OctoberWorkerParseFailure.ClosedFields, slot);
                throw new InvalidDataException("Unknown or repeated proposal field.");
            }
        if (seen.Count != names.Length)
        {
            parse.Record(OctoberWorkerParseFailure.ClosedFields, slot);
            throw new InvalidDataException("Missing proposal field.");
        }
    }

    private sealed class ParseObservation
    {
        internal OctoberWorkerParseDiagnosticSnapshot? First { get; private set; }
        internal void Record(OctoberWorkerParseFailure category, OctoberWorkerParseSlot slot) => First ??= new(category, slot);
        internal T Read<T>(Func<T> operation, OctoberWorkerParseFailure category, OctoberWorkerParseSlot slot)
        {
            try { return operation(); }
            catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or
                ArgumentException or InvalidDataException or OverflowException or KeyNotFoundException)
            {
                Record(category, slot);
                throw;
            }
        }
    }
}
