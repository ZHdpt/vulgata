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

public enum OctoberWorkerCorrectionStrategy { Configured, ThinkingDisabled, CompactedThinkingDisabled }

/// <summary>Host-selected, per-call finalization behavior; never a provider/model fallback.</summary>
public interface IOctoberWorkerCorrectionClient : IOctoberWorkerAttemptClient
{
    bool SupportsFinalizationStrategy { get; }
    IAsyncEnumerable<ChatResponseUpdate> GetCorrectionStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options, IOctoberProviderAttemptLease lease,
        OctoberWorkerCorrectionStrategy strategy, CancellationToken cancellationToken = default,
        Action<long>? onDispatched = null);
}

public interface IOctoberWorkerHttpObservationClient
{
    void BeginLeaf(Guid runId, IOctoberProviderAttemptLease lease);
    OctoberWorkerHttpObservationSnapshot? CompleteParseFailure(Guid runId, IOctoberProviderAttemptLease lease, string parseInput);
    void Discard(Guid runId);
}

public enum OctoberWorkerHttpObservationStatus { Complete, Incomplete, LimitExceeded, Unsupported, Failed }
public enum OctoberWorkerTokenCapKey { MaxTokens, MaxCompletionTokens, Missing, Ambiguous }
public enum OctoberWorkerObservedFinish { Stop, Length, ToolCalls, Other }
public enum OctoberWorkerJsonObservationKind
{
    ValidObject, ValidNonObject, MissingClosingDelimiters, UnterminatedString,
    MismatchedDelimiter, InvalidSyntax, DepthExceeded
}
public sealed record OctoberWorkerJsonObservation(OctoberWorkerJsonObservationKind? Kind = null,
    int? OpenObjects = null, int? OpenArrays = null, bool? AtEof = null);
public sealed record OctoberWorkerHttpObservationSnapshot(
    OctoberWorkerHttpObservationStatus? Status = null,
    OctoberWorkerTokenCapKey? CapKey = null, long? CapValue = null,
    OctoberWorkerObservedFinish? HttpFinish = null, OctoberWorkerObservedFinish? SdkFinish = null,
    bool? DoneSeen = null, bool? EofSeen = null, bool? ContentAfterFinish = null, bool? ContentAfterDone = null,
    long? WireBytes = null, int? HttpTextUtf8Bytes = null, int? SdkTextUtf8Bytes = null, int? ParseInputUtf8Bytes = null,
    bool? HttpEqualsSdk = null, bool? SdkEqualsParse = null,
    OctoberWorkerJsonObservation? HttpJson = null, OctoberWorkerJsonObservation? SdkJson = null,
    OctoberWorkerJsonObservation? ParseJson = null, long? HttpReasoningTokens = null, long? SdkReasoningTokens = null);

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
    int MaximumContinuationWindows = 4,
    int MaximumOutputTokensPerRequest = 8192,
    int MaximumJsonCorrectionAttempts = 0,
    int? AdvisoryToolCalls = null,
    bool UseTextForJsonCorrection = false,
    bool HardRetryMode = false);

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
    OctoberFileWorkerLimits Limits)
{
    public OctoberFileToolBudget? ToolBudget { get; init; }
    public OctoberRunCorrectionBudget? HardRetryBudget { get; init; }
}

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
public enum OctoberWorkerBudgetFailure { OutputBytes, ToolCalls, StreamUpdates, InputBytes, RequestTokens, ProviderRounds, ProviderAdmission }
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
public enum OctoberWorkerFieldFailure { Missing, Unexpected, Duplicate }
public sealed record OctoberWorkerParseDiagnosticSnapshot(OctoberWorkerParseFailure? Category, OctoberWorkerParseSlot? Slot,
    OctoberWorkerFieldFailure? FieldFailure = null, OctoberWorkerParseSlot? Field = null, int? UnitIndex = null,
    long? LineNumber = null, long? BytePositionInLine = null);
public enum OctoberWorkerTerminalFailure
{
    ContentAfterFinish, ConflictingFinish, NonAssistantRole, ToolCallError, MissingToolCallId,
    UnknownTool, RepeatedToolCallId, MissingFinish, UnsupportedFinish, ToolFinishWithoutCalls,
    StopWithCalls, IncompleteTerminal, IncompleteCoverage, ToolsDisabled
}

public enum OctoberWorkerUnsupportedFinish { Length, ContentFilter, Other }
public enum OctoberJsonCorrectionPhase { CandidateRejected, RetryRequested, ResponseReceived, WorkerFinished }
/// <summary>Bounded diagnostics only; Completed means a validated proposal, never publication.</summary>
public sealed record OctoberJsonCorrectionEvent(OctoberJsonCorrectionPhase Phase, int RetryOrdinal,
    int WindowOrdinal, int ProviderRounds, long ElapsedMilliseconds, int CandidateUtf8Bytes,
    OctoberWorkerParseDiagnosticSnapshot? Diagnostic = null, OctoberFileWorkerStatus? Status = null,
    OctoberWorkerFailureStage? FailureStage = null, OctoberWorkerBudgetFailure? BudgetFailure = null,
    OctoberWorkerTerminalFailure? TerminalFailure = null, OctoberWorkerObservedFinish? FinishReason = null,
    OctoberWorkerCorrectionStrategy Strategy = OctoberWorkerCorrectionStrategy.Configured, int RemovedMessages = 0);
public sealed record OctoberWorkerTerminalObservation(
    OctoberWorkerUnsupportedFinish? FinishReason = null, int? MaximumOutputTokens = null,
    long? InputTokens = null, long? OutputTokens = null, long? ReasoningTokens = null);

/// <summary>Worker observation only; this is not a Knowledge completion capability.</summary>
public sealed record OctoberFileWorkerResult(
    OctoberFileWorkerStatus Status,
    CompletedFileProposal? Proposal = null,
    int CoveredUtf8Bytes = 0,
    int ProviderRounds = 0,
    int SettledToolCalls = 0)
{
    public int JsonCorrectionAttempts { get; init; }
    public ImmutableArray<OctoberJsonCorrectionEvent> JsonCorrections { get; init; } = [];
    public OctoberWorkerRetryDisposition RetryDisposition { get; init; }
    public TimeSpan? RetryAfter { get; init; }
    public OctoberWorkerFailureStage? FailureStage { get; init; }
    public OctoberWorkerTerminalFailure? TerminalFailure { get; init; }
    public OctoberWorkerTerminalObservation? TerminalObservation { get; init; }
    public OctoberWorkerBudgetFailure? BudgetFailure { get; init; }
    public OctoberWorkerParseDiagnosticSnapshot? ParseFailure { get; init; }
    public OctoberWorkerCaptureStatus? CaptureStatus { get; init; }
    public OctoberWorkerHttpObservationSnapshot? HttpObservation { get; init; }
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
        long started = Stopwatch.GetTimestamp();
        int correctionAttempts = 0, currentWindow = 0;
        OctoberWorkerCorrectionStrategy correctionStrategy = OctoberWorkerCorrectionStrategy.Configured;
        int removedMessages = 0;
        ChatMessage[]? correctionBase = null;
        bool stagedCorrections = _client is IOctoberWorkerCorrectionClient { SupportsFinalizationStrategy: true };
        var correctionEvents = ImmutableArray.CreateBuilder<OctoberJsonCorrectionEvent>();
        int covered = 0;
        OctoberWorkerFailureStage? stage = null;
        OctoberWorkerTerminalFailure? terminalFailure = null;
        OctoberWorkerCaptureStatus? captureStatus = null;
        OctoberWorkerHttpObservationSnapshot? httpObservation = null;
        IOctoberWorkerHttpObservationClient? httpObserver = null;
        try { httpObserver = _client.GetService(typeof(IOctoberWorkerHttpObservationClient)) as IOctoberWorkerHttpObservationClient; }
        catch (Exception) { }
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
            if (request.HardRetryBudget is { } correctionBudget && !correctionBudget.Register(request))
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

            var toolBudget = request.ToolBudget ?? new OctoberFileToolBudget(request.RunId,
                request.FileWorkstreamId, request.AdmissionId, request.Limits.AdvisoryToolCalls, request.Limits.MaximumToolCalls);
            adapter = new WorkerClient(_client, _scope, request, tools, observations, toolBudget);
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
                    MaxOutputTokens = request.Limits.MaximumOutputTokensPerRequest
                }
            });
            var history = new List<ChatMessage> { new(ChatRole.System, prompt) };
            int sourceLine = 1, sourceColumn = 1;
            bool previousCarriageReturn = false;
            for (int index = 0; index < windows.Count; index++)
            {
                currentWindow = index + 1;
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
                for (int correction = 0; ; correction++)
                {
                    stage = null;
                    AgentResponse response = await observations.RunAsync(async () => await agent.RunStreamingAsync(history, cancellationToken: cancellationToken)
                        .ToAgentResponseAsync(cancellationToken), OctoberWorkerFailureStage.AgentOrchestration);
                    if (correction > 0) RecordCorrection(OctoberJsonCorrectionPhase.ResponseReceived);
                    if (adapter.Failure is { } failure) throw new WorkerFailure(failure);
                    stage = OctoberWorkerFailureStage.StreamTerminal;
                    if (adapter.Finish != ChatFinishReason.Stop || adapter.PendingCalls != 0)
                        throw new WorkerFailure(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.IncompleteTerminal);
                    // Preserve complete assistant/tool/reasoning history. A correction is another
                    // admitted leaf in this workstream, never a new run or a locally patched proposal.
                    history.AddRange(response.Messages);
                    if (index != windows.Count - 1) break;
                    CorrectionFeedbackData? feedback = CorrectionFeedback(adapter.LastText, request);
                    if (feedback is null) break;
                    RecordCorrection(OctoberJsonCorrectionPhase.CandidateRejected, feedback.Failure);
                    bool extraCorrection = correction >= request.Limits.MaximumJsonCorrectionAttempts;
                    if (extraCorrection && !request.Limits.HardRetryMode) break;
                    // Reserve only an eligible extra candidate. Existing admission may still
                    // refuse it; neither refusal nor source/runtime renewal refunds the slot.
                    if (extraCorrection && !request.HardRetryBudget!.TryReserve(request)) break;
                    if (stagedCorrections && correction == 0)
                    {
                        // Keep the complete investigation, including all source windows and
                        // settled tool exchanges. Only the terminal rejected proposal is excluded.
                        if (response.Messages.Count == 0 && adapter.LastText.Length == 0)
                            correctionBase = history.ToArray();
                        else if (response.Messages.Count > 0 && response.Messages[^1] is { Role: var role } candidate &&
                            role == ChatRole.Assistant && !candidate.Contents.OfType<FunctionCallContent>().Any() &&
                            string.Concat(candidate.Contents.OfType<TextContent>().Select(item => item.Text)) == adapter.LastText)
                            correctionBase = history.Take(history.Count - 1).ToArray();
                        else
                            throw new WorkerFailure(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.IncompleteTerminal);
                    }
                    correctionAttempts++;
                    correctionStrategy = stagedCorrections && correctionAttempts >= 2
                        ? correctionAttempts == 3 ? OctoberWorkerCorrectionStrategy.CompactedThinkingDisabled : OctoberWorkerCorrectionStrategy.ThinkingDisabled
                        : OctoberWorkerCorrectionStrategy.Configured;
                    if (correctionStrategy == OctoberWorkerCorrectionStrategy.CompactedThinkingDisabled)
                    {
                        if (correctionBase is null)
                            throw new WorkerFailure(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.IncompleteTerminal);
                        removedMessages = history.Count - correctionBase.Length;
                        history.Clear();
                        history.AddRange(correctionBase);
                    }
                    RecordCorrection(OctoberJsonCorrectionPhase.RetryRequested, feedback.Failure);
                    adapter.BeginCorrection(extraCorrection, correctionStrategy);
                    history.Add(new ChatMessage(ChatRole.User,
                        LoadPrompt("Vulgata.Agents.Prompts.OctoberFileWorkerCorrection.md") + "\n" +
                        feedback.ToJson(request.HardRetryBudget?.Remaining(request) ??
                            Math.Max(0, request.Limits.MaximumJsonCorrectionAttempts - correctionAttempts))));
                }
                stage = null;
                if (!await observations.RunAsync(() => request.Source.VerifyCurrentAsync(cancellationToken), OctoberWorkerFailureStage.SourceVerification))
                    throw new WorkerFailure(OctoberFileWorkerStatus.SourceChanged);
                covered = checked(covered + Utf8.GetByteCount(windows[index]));
            }
            stage = OctoberWorkerFailureStage.StreamTerminal;
            if (covered != source.Length) throw new WorkerFailure(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.IncompleteCoverage);
            stage = OctoberWorkerFailureStage.ProposalParse;
            CompletedFileProposal proposal;
            try { proposal = ParseProposal(adapter.LastText, request, ref stage, parseObservation); }
            catch (Exception) when (stage == OctoberWorkerFailureStage.ProposalParse)
            {
                captureStatus = await CaptureParseFailureAsync(adapter.LastText, request.Source, cancellationToken);
                if (httpObserver is not null && adapter.LastLease is { } lastLease)
                {
                    // Consume bounded raw references even when fresh disclosure eligibility is lost.
                    try
                    {
                        bool current = !cancellationToken.IsCancellationRequested && await request.Source.VerifyCurrentAsync(cancellationToken);
                        var observed = httpObserver.CompleteParseFailure(request.RunId, lastLease, adapter.LastText);
                        if (current && !cancellationToken.IsCancellationRequested) httpObservation = observed;
                    }
                    catch (Exception) { }
                }
                throw;
            }
            return FinishResult(new(OctoberFileWorkerStatus.Completed, proposal, covered, adapter.Rounds, adapter.SettledTools));
        }
        catch (OperationCanceledException exception)
        {
            return Failed(OctoberFileWorkerStatus.Cancelled, exception);
        }
        catch (WorkerFailure failure) { terminalFailure = failure.TerminalFailure; return Failed(failure.Status, failure); }
        catch (Exception exception) when (stage == OctoberWorkerFailureStage.ProposalParse && parseObservation.First is not null &&
            exception is InvalidOperationException or FormatException or KeyNotFoundException)
        {
            return Failed(OctoberFileWorkerStatus.InvalidOutput, exception);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidDataException or OverflowException)
        {
            return Failed(adapter?.Failure ?? OctoberFileWorkerStatus.InvalidOutput, exception);
        }
        catch (Exception exception) { return Failed(adapter?.Failure ?? OctoberFileWorkerStatus.ProviderFailed, exception); }

        finally
        {
            try { httpObserver?.Discard(request?.RunId ?? Guid.Empty); } catch (Exception) { }
        }

        OctoberFileWorkerResult Failed(OctoberFileWorkerStatus status, Exception? exception = null)
            => FinishResult(new(status, null, covered, adapter?.Rounds ?? 0, adapter?.SettledTools ?? 0)
            {
                RetryDisposition = status == OctoberFileWorkerStatus.ProviderFailed && correctionAttempts == 0
                    ? adapter?.RetryDisposition ?? OctoberWorkerRetryDisposition.None : OctoberWorkerRetryDisposition.None,
                FailureStage = observations.For(exception) ?? stage ?? adapter?.FailureStage,
                TerminalFailure = terminalFailure ?? adapter?.TerminalFailure,
                TerminalObservation = status == OctoberFileWorkerStatus.InvalidOutput &&
                    (observations.For(exception) ?? stage ?? adapter?.FailureStage) == OctoberWorkerFailureStage.StreamTerminal &&
                    adapter?.TerminalFailure == OctoberWorkerTerminalFailure.UnsupportedFinish
                    ? adapter.TerminalObservation : null,
                BudgetFailure = status == OctoberFileWorkerStatus.BudgetExceeded ? adapter?.BudgetFailure : null,
                ParseFailure = Enum.IsDefined(status) && status != OctoberFileWorkerStatus.Completed &&
                    (observations.For(exception) ?? stage ?? adapter?.FailureStage) == OctoberWorkerFailureStage.ProposalParse
                    ? parseObservation.First : null,
                CaptureStatus = Enum.IsDefined(status) && status != OctoberFileWorkerStatus.Completed &&
                    (observations.For(exception) ?? stage ?? adapter?.FailureStage) == OctoberWorkerFailureStage.ProposalParse
                    ? captureStatus : null,
                HttpObservation = Enum.IsDefined(status) && status != OctoberFileWorkerStatus.Completed &&
                    (observations.For(exception) ?? stage ?? adapter?.FailureStage) == OctoberWorkerFailureStage.ProposalParse
                    ? httpObservation : null
            });

        void RecordCorrection(OctoberJsonCorrectionPhase phase, OctoberWorkerParseDiagnosticSnapshot? diagnostic = null,
            OctoberFileWorkerResult? result = null)
        {
            // At most four rejected candidates, three requests/responses and one terminal
            // event. No provider strings or raw content are retained in this diagnostic ledger.
            if (correctionEvents.Count >= 11) return;
            correctionEvents.Add(new(phase, correctionAttempts, currentWindow, adapter?.Rounds ?? 0,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Encoding.UTF8.GetByteCount(adapter?.LastText ?? string.Empty), diagnostic, result?.Status,
                result?.FailureStage, result?.BudgetFailure, result?.TerminalFailure, adapter?.DiagnosticFinish,
                correctionStrategy, removedMessages));
        }

        OctoberFileWorkerResult FinishResult(OctoberFileWorkerResult result)
        {
            if (correctionEvents.Count > 0) RecordCorrection(OctoberJsonCorrectionPhase.WorkerFinished, result.ParseFailure, result);
            return result with { JsonCorrectionAttempts = correctionAttempts, JsonCorrections = correctionEvents.ToImmutable() };
        }
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
            limits.AdvisoryToolCalls is { } advisory && (advisory <= 0 || advisory >= limits.MaximumToolCalls) ||
            request.ToolBudget is { } budget && !budget.Matches(request) ||
            limits.MaximumWindowUtf8Bytes is < 4 or > 1048576 || limits.MaximumInputUtf8Bytes is < 1 or > 16777216 ||
            limits.MaximumOutputUtf8Bytes is < 1 or > 1048576 || limits.MaximumTokensPerRequest is < 1 or > 1048576 ||
            limits.MaximumOutputTokensPerRequest is < 1 or > 393216 ||
            limits.MaximumJsonCorrectionAttempts is < 0 or > 3 ||
            limits.HardRetryMode != (request.HardRetryBudget is not null) ||
            limits.HardRetryMode && limits.MaximumJsonCorrectionAttempts != 1 ||
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

    private static CorrectionFeedbackData? CorrectionFeedback(string text, OctoberFileWorkerRequest request)
    {
        var observation = new ParseObservation();
        OctoberWorkerFailureStage? stage = OctoberWorkerFailureStage.ProposalParse;
        try { _ = ParseProposal(text, request, ref stage, observation); }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or
            ArgumentException or InvalidDataException or OverflowException or KeyNotFoundException)
        {
            // Only parser-defined structural errors are eligible. Evidence and semantic
            // validation failures are not permission to invent data or alter authority.
            if (stage == OctoberWorkerFailureStage.ProposalParse && observation.First is { } failure &&
                failure.Category is OctoberWorkerParseFailure.Syntax or OctoberWorkerParseFailure.RootShape or
                    OctoberWorkerParseFailure.ClosedFields or OctoberWorkerParseFailure.ValueShape or OctoberWorkerParseFailure.Qualification)
                return new(failure, observation.ExpectedFields);
        }
        return null;
    }

    private sealed record CorrectionFeedbackData(OctoberWorkerParseDiagnosticSnapshot Failure, string[]? ExpectedFields)
    {
        internal string ToJson(int remainingCorrections) => JsonSerializer.Serialize(new
        {
            type = "validation_error", remainingCorrections,
            category = Failure.Category.ToString(), slot = Failure.Slot.ToString(),
            fieldFailure = Failure.FieldFailure?.ToString(),
            field = Failure.Field is { } field ? SchemaName(field) : null,
            unitIndex = Failure.UnitIndex, expectedFields = ExpectedFields,
            lineNumber = Failure.LineNumber, bytePositionInLine = Failure.BytePositionInLine
        });
    }

    private static string SchemaName(OctoberWorkerParseSlot slot)
    {
        string name = slot.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static OctoberWorkerParseSlot? KnownField(string name)
        => Enum.TryParse(name, ignoreCase: true, out OctoberWorkerParseSlot field) && Enum.IsDefined(field) &&
            SchemaName(field) == name ? field : null;

    private static string LoadPrompt(string name = "Vulgata.Agents.Prompts.OctoberFileWorker.md")
    {
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
        OctoberFileWorkerRequest request, IReadOnlyList<AIFunction> tools, ExceptionObservation observations,
        OctoberFileToolBudget toolBudget) : DelegatingChatClient(client)
    {
        private readonly Dictionary<string, FunctionCallContent> _pending = new(StringComparer.Ordinal);
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        private int _inputBytes;
        private int _outputBytes;
        private bool _allUsageKnown = true;
        private bool _correctionOnly;
        private bool _extraCorrectionPending;
        private OctoberWorkerCorrectionStrategy _correctionStrategy;
        internal OctoberWorkerRetryDisposition RetryDisposition { get; private set; }
        internal int Rounds { get; private set; }
        internal int SettledTools { get; private set; }
        internal int PendingCalls => _pending.Count;
        internal ChatFinishReason? Finish { get; private set; }
        internal OctoberWorkerObservedFinish? DiagnosticFinish { get; private set; }
        internal string LastText { get; private set; } = string.Empty;
        internal IOctoberProviderAttemptLease? LastLease { get; private set; }
        internal OctoberFileWorkerStatus? Failure { get; private set; }
        internal OctoberWorkerFailureStage? FailureStage { get; private set; }
        internal OctoberWorkerTerminalFailure? TerminalFailure { get; private set; }
        internal OctoberWorkerTerminalObservation? TerminalObservation { get; private set; }
        internal OctoberWorkerBudgetFailure? BudgetFailure { get; private set; }
        private OctoberWorkerFailureStage? _observedStage;

        internal void BeginCorrection(bool extraCorrection, OctoberWorkerCorrectionStrategy strategy)
        {
            if (_pending.Count != 0 || Finish != ChatFinishReason.Stop)
                throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.IncompleteTerminal);
            _correctionOnly = true;
            _extraCorrectionPending = extraCorrection;
            _correctionStrategy = strategy;
        }

        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw Fail(OctoberFileWorkerStatus.InvalidInput);

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            DiagnosticFinish = null;
            _observedStage = OctoberWorkerFailureStage.StreamTerminal;
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null || _pending.Count != 0)
                throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.IncompleteTerminal);
            _observedStage = OctoberWorkerFailureStage.Admission;
            if (Rounds >= request.Limits.MaximumProviderRounds) throw Fail(OctoberFileWorkerStatus.BudgetExceeded, budgetFailure: OctoberWorkerBudgetFailure.ProviderRounds);
            _observedStage = null;
            if (!await observations.RunAsync(() => request.Source.VerifyCurrentAsync(cancellationToken), OctoberWorkerFailureStage.SourceVerification)) throw Fail(OctoberFileWorkerStatus.SourceChanged);
            _observedStage = OctoberWorkerFailureStage.Admission;
            var budget = toolBudget.Read();
            var currentMessages = messages.ToList();
            // This fresh system metadata is not appended to persistent history: old counts
            // must not accumulate across tool/correction rounds or obscure the current grant.
            currentMessages.Insert(0, new ChatMessage(ChatRole.System, JsonSerializer.Serialize(new
            {
                type = "file_tool_budget", scope = "primary_file",
                advisoryLimit = budget.AdvisoryLimit, maximumLimit = budget.MaximumLimit,
                requestedCalls = budget.RequestedCalls, admittedCalls = budget.AdmittedCalls,
                executedCalls = budget.ExecutedCalls, remainingCalls = budget.RemainingCalls,
                advisoryReached = budget.AdvisoryReached
            })));
            ChatMessage[] snapshot = currentMessages.ToArray();
            int inputBytes = MeasureInputBytes(snapshot, options);
            _inputBytes = checked(_inputBytes + inputBytes);
            if (_inputBytes > request.Limits.MaximumInputUtf8Bytes)
                throw Fail(OctoberFileWorkerStatus.BudgetExceeded, budgetFailure: OctoberWorkerBudgetFailure.InputBytes);

            // Retain the existing byte-based input reservation policy. This is a
            // proxy, not a provider tokenizer or a proven context upper bound.
            long inputTokenReservation = inputBytes;
            int outputTokens = request.Limits.MaximumOutputTokensPerRequest;
            long requiredTokens = checked(inputTokenReservation + outputTokens);
            if (requiredTokens > request.Limits.MaximumTokensPerRequest)
                throw Fail(OctoberFileWorkerStatus.BudgetExceeded, budgetFailure: OctoberWorkerBudgetFailure.RequestTokens);
            ChatOptions bounded = options?.Clone() ?? new();
            bounded.MaxOutputTokens = outputTokens;
            // Keep declarations/history intact, but this existing correction is
            // finalization only. Host enforcement below is independent of the provider.
            if (_correctionOnly)
            {
                bounded.ToolMode = ChatToolMode.None;
                // Proposals and their corrections require JSON. Retain the legacy
                // experiment flag as input compatibility only; it cannot select text.
                bounded.ResponseFormat = ChatResponseFormat.Json;
            }
            var admissionRequest = new OctoberWorkerProviderAttemptRequest(request.RunId, request.FileWorkstreamId,
                Guid.NewGuid(), request.ProviderProfileId, request.Limits.RequestTimeout ?? TimeSpan.FromMinutes(2),
                requiredTokens);
            long waitStarted = Stopwatch.GetTimestamp();
            ProviderAdmission admission = await scope.AcquireWorkerAttemptAsync(admissionRequest, cancellationToken);
            if (admission.Status != ProviderAdmissionStatus.Granted || admission.Lease is null)
                throw Fail(admission.Status is ProviderAdmissionStatus.BudgetExhausted or ProviderAdmissionStatus.DeadlineExceeded
                    ? OctoberFileWorkerStatus.BudgetExceeded : OctoberFileWorkerStatus.Denied,
                    budgetFailure: admission.Status is ProviderAdmissionStatus.BudgetExhausted or ProviderAdmissionStatus.DeadlineExceeded
                        ? OctoberWorkerBudgetFailure.ProviderAdmission : null);
            IOctoberProviderAttemptLease lease = admission.Lease;
            try { (client.GetService(typeof(IOctoberWorkerHttpObservationClient)) as IOctoberWorkerHttpObservationClient)?.BeginLeaf(request.RunId, lease); }
            catch (Exception) { }
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
            long? reasoningTokens = null;
            string? lastResponseId = null;
            int nonProgressUpdates = 0;
            var calls = new List<FunctionCallContent>();
            var reasoningHistory = new List<TextReasoningContent>();
            var plainReasoning = new StringBuilder();
            bool hasPlainReasoning = false;
            var text = new StringBuilder();
            ChatFinishReason? finish = null;

            void RetainReasoning(TextReasoningContent reasoning)
            {
                // Protected segments (even empty ones) can carry wire semantics.
                // Bound retained objects independently of transport fragmentation.
                if (reasoningHistory.Count >= 8192)
                    throw Fail(OctoberFileWorkerStatus.BudgetExceeded, budgetFailure: OctoberWorkerBudgetFailure.StreamUpdates);
                reasoningHistory.Add(reasoning);
            }

            void FlushPlainReasoning()
            {
                if (!hasPlainReasoning) return;
                RetainReasoning(new TextReasoningContent(plainReasoning.ToString()));
                plainReasoning.Clear();
                hasPlainReasoning = false;
            }

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
                if (_extraCorrectionPending)
                {
                    request.HardRetryBudget!.RecordProviderCallStarted(request);
                    _extraCorrectionPending = false;
                }
                IAsyncEnumerable<ChatResponseUpdate> updates = _correctionStrategy != OctoberWorkerCorrectionStrategy.Configured &&
                    client is IOctoberWorkerCorrectionClient correctionClient
                    ? correctionClient.GetCorrectionStreamingResponseAsync(snapshot, bounded, lease, _correctionStrategy, linked.Token, timing.MarkDispatched)
                    : client is IOctoberWorkerAttemptClient admittedClient
                    ? admittedClient.GetAttemptStreamingResponseAsync(snapshot, bounded, lease, linked.Token, timing.MarkDispatched)
                    : client.GetStreamingResponseAsync(snapshot, bounded, linked.Token);
                iterator = updates.GetAsyncEnumerator(linked.Token);
                while (true)
                {
                    _observedStage = OctoberWorkerFailureStage.Transport;
                    linked.Token.ThrowIfCancellationRequested();
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
                    linked.Token.ThrowIfCancellationRequested();
                    if (!next) break;
                    _observedStage = OctoberWorkerFailureStage.StreamTerminal;
                    ChatResponseUpdate update = iterator.Current;
                    if (update.FinishReason is { } observedFinish)
                        DiagnosticFinish = observedFinish == ChatFinishReason.Stop ? OctoberWorkerObservedFinish.Stop :
                            observedFinish == ChatFinishReason.Length ? OctoberWorkerObservedFinish.Length :
                            observedFinish == ChatFinishReason.ToolCalls ? OctoberWorkerObservedFinish.ToolCalls : OctoberWorkerObservedFinish.Other;
                    int outputBytesBefore = _outputBytes;
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
                        linked.Token.ThrowIfCancellationRequested();
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
                            // Keep provider reasoning in conversation history, never in
                            // LastText (the proposal/capture input). Do not carry opaque
                            // raw SDK objects into the next request.
                            if (reasoning.ProtectedData is null)
                            {
                                plainReasoning.Append(reasoning.Text);
                                hasPlainReasoning = true;
                            }
                            else
                            {
                                FlushPlainReasoning();
                                RetainReasoning(new TextReasoningContent(reasoning.Text)
                                {
                                    ProtectedData = reasoning.ProtectedData
                                });
                            }
                        }
                        else if (content is FunctionCallContent call && !call.InformationalOnly)
                        {
                            ChargeOutput(MeasureOutput(call.Arguments));
                            if (_correctionOnly)
                                throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.ToolsDisabled);
                            if (call.Exception is not null)
                                throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.ToolCallError);
                            if (string.IsNullOrWhiteSpace(call.CallId))
                                throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.MissingToolCallId);
                            if (!tools.Any(tool => tool.Name == call.Name))
                                throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.UnknownTool);
                            if (!_seen.Add(call.CallId))
                                throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.RepeatedToolCallId);
                            if (!toolBudget.TryAdmit())
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
                            reasoningTokens = usage.Details.ReasoningTokenCount;
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
                    // Positive-byte updates are bounded by the existing byte cap.
                    // Empty/usage-only updates share one cumulative allowance; a
                    // tiny payload never renews an empty-stream flood budget.
                    if (_outputBytes == outputBytesBefore && ++nonProgressUpdates > 8192)
                        throw Fail(OctoberFileWorkerStatus.BudgetExceeded, budgetFailure: OctoberWorkerBudgetFailure.StreamUpdates);
                    lastResponseId = update.ResponseId;
                }
                _observedStage = OctoberWorkerFailureStage.StreamTerminal;
                if (finish is null)
                    throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.MissingFinish);
                if (finish != ChatFinishReason.Stop && finish != ChatFinishReason.ToolCalls)
                {
                    // Only scalar observations from this rejected leaf. Unknown usage
                    // stays unknown; the requested cap is not proof of enforcement.
                    TerminalObservation = new(
                        finish == ChatFinishReason.Length ? OctoberWorkerUnsupportedFinish.Length :
                        finish == ChatFinishReason.ContentFilter ? OctoberWorkerUnsupportedFinish.ContentFilter :
                        OctoberWorkerUnsupportedFinish.Other,
                        outputTokens, inputTokens is >= 0 ? inputTokens : null,
                        actualOutputTokens is >= 0 ? actualOutputTokens : null,
                        reasoningTokens is >= 0 ? reasoningTokens : null);
                    throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.UnsupportedFinish);
                }
                if (finish == ChatFinishReason.ToolCalls && calls.Count == 0)
                    throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.ToolFinishWithoutCalls);
                if (finish == ChatFinishReason.Stop && calls.Count != 0)
                    throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.StopWithCalls);
                FlushPlainReasoning();
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
            LastLease = lease;
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                ResponseId = lastResponseId,
                Contents = [.. reasoningHistory, new TextContent(LastText), .. calls],
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
            if (_correctionOnly)
                throw Fail(OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerTerminalFailure.ToolsDisabled);
            if (!_pending.TryGetValue(context.CallContent.CallId, out FunctionCallContent? call) || call.Name != context.Function.Name)
                throw Fail(OctoberFileWorkerStatus.InvalidOutput);
            if (!await observations.RunAsync(() => request.Source.VerifyCurrentAsync(token), OctoberWorkerFailureStage.SourceVerification)) throw Fail(OctoberFileWorkerStatus.SourceChanged);
            AIFunction function = tools.Single(tool => tool.Name == call.Name);
            bool unsupported = function.JsonSchema.TryGetProperty("properties", out JsonElement properties) &&
                call.Arguments?.Keys.Any(key => !properties.TryGetProperty(key, out _)) == true;
            object? result;
            if (unsupported) result = new { status = "InvalidArguments" };
            else result = await observations.RunAsync(async () =>
            {
                var arguments = new AIFunctionArguments(call.Arguments ?? new Dictionary<string, object?>());
                toolBudget.RecordExecution();
                return await function.InvokeAsync(arguments, token);
            }, OctoberWorkerFailureStage.ToolInvocation);
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

        private static int MeasureInputBytes(IEnumerable<ChatMessage> messages, ChatOptions? options)
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
        ImmutableArray<SemanticUnitProposal> units = Array(root, "units", parse, OctoberWorkerParseSlot.Units).Select((unit, index) =>
        {
            parse.UnitIndex = index;
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
        parse.UnitIndex = null;
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
        {
            if (!names.Contains(property.Name, StringComparer.Ordinal))
            {
                // Unknown names may contain sensitive/model-controlled text; never echo them.
                parse.RecordField(slot, OctoberWorkerFieldFailure.Unexpected, null, names);
                throw new InvalidDataException("Unknown proposal field.");
            }
            if (!seen.Add(property.Name))
            {
                parse.RecordField(slot, OctoberWorkerFieldFailure.Duplicate, KnownField(property.Name), names);
                throw new InvalidDataException("Repeated proposal field.");
            }
        }
        foreach (string name in names)
        {
            if (seen.Contains(name)) continue;
            parse.RecordField(slot, OctoberWorkerFieldFailure.Missing, KnownField(name), names);
            throw new InvalidDataException("Missing proposal field.");
        }
    }

    private sealed class ParseObservation
    {
        internal OctoberWorkerParseDiagnosticSnapshot? First { get; private set; }
        internal int? UnitIndex { get; set; }
        internal string[]? ExpectedFields { get; private set; }
        internal void Record(OctoberWorkerParseFailure category, OctoberWorkerParseSlot slot)
            => First ??= new(category, slot, Field: category == OctoberWorkerParseFailure.ValueShape ? slot : null, UnitIndex: UnitIndex);
        internal void RecordField(OctoberWorkerParseSlot slot, OctoberWorkerFieldFailure failure, OctoberWorkerParseSlot? field, string[] expected)
        {
            if (First is not null) return;
            First = new(OctoberWorkerParseFailure.ClosedFields, slot, failure, field, UnitIndex);
            ExpectedFields = expected.ToArray();
        }
        internal T Read<T>(Func<T> operation, OctoberWorkerParseFailure category, OctoberWorkerParseSlot slot)
        {
            try { return operation(); }
            catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or
                ArgumentException or InvalidDataException or OverflowException or KeyNotFoundException)
            {
                Record(category, slot);
                if (category == OctoberWorkerParseFailure.Syntax && error is JsonException syntax && First is { } first)
                    First = first with
                    {
                        LineNumber = SafePosition(syntax.LineNumber),
                        BytePositionInLine = SafePosition(syntax.BytePositionInLine)
                    };
                throw;
            }
        }

        // Numeric zero-based positions only: never expose exception text or JSON paths.
        private static long? SafePosition(long? value) => value is >= 0 and <= 262144 ? value : null;
    }
}
