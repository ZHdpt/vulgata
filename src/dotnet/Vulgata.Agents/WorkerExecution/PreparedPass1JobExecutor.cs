using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared;

namespace Vulgata.Agents.WorkerExecution;

internal static class PreparedPass1WireBudget
{
    internal const int MaximumPreparedInitialMessageBytes = 131_072;
    internal const int MaximumFunctionDeclarationsBytes = 81_920;
    internal const int MaximumRawResponseBytes = 16_777_216;
    internal const int MaximumAssembledResponseBytes = 16_777_216;
    internal const int MaximumAssistantContributionBytes = 16_777_216;
    internal const int MaximumAssistantContributionAggregateBytes = 16_777_216;
    internal const int MaximumAssistantContributionCount = 9;
    internal const int MaximumToolResultAggregateBytes = 393_216;
    internal const int FixedRequestEnvelopeMarginBytes = 32_768;
    internal const int MaximumOutgoingBodyBytes =
        MaximumPreparedInitialMessageBytes
        + MaximumFunctionDeclarationsBytes
        + MaximumAssistantContributionAggregateBytes
        + MaximumToolResultAggregateBytes
        + FixedRequestEnvelopeMarginBytes;

    internal static bool IsWithinLimit(
        int preparedInitialMessageBytes,
        int functionDeclarationsBytes,
        int rawResponseBytes,
        int assembledResponseBytes,
        IReadOnlyCollection<int> assistantContributionBytes,
        int toolResultAggregateBytes,
        int outgoingBodyBytes)
    {
        if (preparedInitialMessageBytes is < 0 or > MaximumPreparedInitialMessageBytes ||
            functionDeclarationsBytes is < 0 or > MaximumFunctionDeclarationsBytes ||
            rawResponseBytes is < 0 or > MaximumRawResponseBytes ||
            assembledResponseBytes is < 0 or > MaximumAssembledResponseBytes ||
            assistantContributionBytes.Count > MaximumAssistantContributionCount ||
            toolResultAggregateBytes is < 0 or > MaximumToolResultAggregateBytes ||
            outgoingBodyBytes is < 0 or > MaximumOutgoingBodyBytes)
        {
            return false;
        }

        long assistantAggregateBytes = 0;
        try
        {
            foreach (int contributionBytes in assistantContributionBytes)
            {
                if (contributionBytes is < 0 or > MaximumAssistantContributionBytes)
                {
                    return false;
                }

                assistantAggregateBytes = checked(assistantAggregateBytes + contributionBytes);
            }
        }
        catch (OverflowException)
        {
            return false;
        }

        return assistantAggregateBytes <= MaximumAssistantContributionAggregateBytes;
    }
}

/// <summary>
/// Closed, sanitized categories for the execution-local wire bound that ended
/// one Worker execution.
/// </summary>
public enum PreparedPass1WireBudgetDiagnosticKind
{
    PreparedInitialMessage,
    OutgoingRequest,
    RawResponse,
    AssembledResponse,
}

/// <summary>
/// Payload-free reason a prepared outgoing provider request was rejected locally.
/// </summary>
public enum PreparedPass1OutgoingRequestRejectionKind
{
    TurnLimit,
    BodySize,
    Normalization,
    NormalizationJson,
    NormalizationParameters,
    NormalizationMessages,
    NormalizationMessageEnvelope,
    NormalizationAssistantToolContentArray,
    NormalizationAssistantToolContentOther,
    NormalizationSize,
    StrictShape,
    ContributionBudget,
}

/// <summary>
/// Observes only the closed wire-bound category for the current execution.
/// Implementations must remain in memory and must not retain wire content.
/// </summary>
public interface IPreparedPass1WireBudgetDiagnosticObserver
{
    IReadOnlyList<PreparedPass1WireBudgetDiagnosticKind> Categories => [];

    PreparedPass1OutgoingRequestRejectionKind? OutgoingRequestRejectionKind => null;

    void Observe(PreparedPass1WireBudgetDiagnosticKind category);

    void ObserveOutgoingRequestRejection(PreparedPass1OutgoingRequestRejectionKind kind) =>
        Observe(PreparedPass1WireBudgetDiagnosticKind.OutgoingRequest);
}

/// <summary>
/// Execution-scope in-memory observer used by provider qualification.
/// </summary>
public sealed class PreparedPass1WireBudgetDiagnosticObserver
    : IPreparedPass1WireBudgetDiagnosticObserver
{
    private readonly object _sync = new();
    private readonly List<PreparedPass1WireBudgetDiagnosticKind> _categories = [];
    private PreparedPass1OutgoingRequestRejectionKind? _outgoingRequestRejectionKind;

    public IReadOnlyList<PreparedPass1WireBudgetDiagnosticKind> Categories
    {
        get
        {
            lock (_sync)
            {
                return [.. _categories];
            }
        }
    }

    public PreparedPass1OutgoingRequestRejectionKind? OutgoingRequestRejectionKind
    {
        get
        {
            lock (_sync)
            {
                return _outgoingRequestRejectionKind;
            }
        }
    }

    public void Observe(PreparedPass1WireBudgetDiagnosticKind category)
    {
        lock (_sync)
        {
            _categories.Add(category);
        }
    }

    public void ObserveOutgoingRequestRejection(PreparedPass1OutgoingRequestRejectionKind kind)
    {
        lock (_sync)
        {
            _categories.Add(PreparedPass1WireBudgetDiagnosticKind.OutgoingRequest);
            _outgoingRequestRejectionKind = kind;
        }
    }
}

/// <summary>
/// Closed, sanitized categories for an execution-local provider or function-call
/// failure that otherwise remains externally unclassified.
/// </summary>
public enum PreparedPass1ExecutionDiagnosticKind
{
    ProviderClientRequestRejected,
    IllegalFunctionCall,
    UnexpectedException,
}

/// <summary>
/// Closed, payload-free subtype for an illegal function-call shape.
/// </summary>
public enum PreparedPass1IllegalFunctionCallSubtype
{
    UnknownFunction,
    MultipleFunctions,
    MissingFunction,
    InvalidCallIdentity,
    TurnIneligible,
    MixedFunctions,
    MultipleSubmissions,
    NavigationUnavailable,
    NavigationBudgetExceeded,
    TerminalTurnRequiresSubmission,
    TerminalMultipleSubmissions,
    TerminalNavigation,
    TerminalMultipleNavigations,
    TerminalMixedFunctions,
    SubmissionUnavailable,
    PostTurnLimit,
}

/// <summary>
/// Observes only the closed failure category for the current execution.
/// Implementations must remain in memory and must not retain failure details.
/// </summary>
public interface IPreparedPass1ExecutionDiagnosticObserver
{
    IReadOnlyList<PreparedPass1ExecutionDiagnosticKind> Categories => [];

    PreparedPass1IllegalFunctionCallSubtype? FunctionCallSubtype => null;

    void Observe(PreparedPass1ExecutionDiagnosticKind category);

    void ObserveIllegalFunctionCall(PreparedPass1IllegalFunctionCallSubtype subtype) =>
        Observe(PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall);
}

/// <summary>
/// Execution-scope in-memory observer used by provider qualification.
/// </summary>
public sealed class PreparedPass1ExecutionDiagnosticObserver
    : IPreparedPass1ExecutionDiagnosticObserver
{
    private readonly object _sync = new();
    private readonly List<PreparedPass1ExecutionDiagnosticKind> _categories = [];
    private PreparedPass1IllegalFunctionCallSubtype? _functionCallSubtype;

    public IReadOnlyList<PreparedPass1ExecutionDiagnosticKind> Categories
    {
        get
        {
            lock (_sync)
            {
                return [.. _categories];
            }
        }
    }

    public PreparedPass1IllegalFunctionCallSubtype? FunctionCallSubtype
    {
        get
        {
            lock (_sync)
            {
                return _functionCallSubtype;
            }
        }
    }

    public void Observe(PreparedPass1ExecutionDiagnosticKind category)
    {
        lock (_sync)
        {
            _categories.Add(category);
        }
    }

    public void ObserveIllegalFunctionCall(PreparedPass1IllegalFunctionCallSubtype subtype)
    {
        lock (_sync)
        {
            _categories.Add(PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall);
            _functionCallSubtype = subtype;
        }
    }
}

/// <summary>
/// Executes one prepared File Code Unit against the configured Worker Agent.
/// The provider receives exactly two bounded functions and no server-owned
/// lease or version authorization.
/// </summary>
public sealed class PreparedPass1JobExecutor(
    WorkerAgentProviderResolver providerResolver,
    ISecretEncryptionService encryptionService,
    PreparedNavigateCodeFunction navigateCodeFunction,
    HttpMessageHandler httpMessageHandler,
    PreparedSubmitCodeLogicFunction? submitCodeLogicFunction = null,
    IPreparedPass1WireBudgetDiagnosticObserver? wireBudgetDiagnosticObserver = null,
    IPreparedPass1ExecutionDiagnosticObserver? executionDiagnosticObserver = null,
    IPass1NavigationAttemptReservationStore? navigationReservationStore = null)
    : IPreparedPass1JobExecutor
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    internal static readonly TimeSpan ProviderInactivityTimeout = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan ProviderAbsoluteTimeout = TimeSpan.FromMinutes(25);

    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly TimeSpan _inactivityTimeout = ProviderInactivityTimeout;
    private readonly TimeSpan _absoluteTimeout = ProviderAbsoluteTimeout;

    internal PreparedPass1JobExecutor(
        WorkerAgentProviderResolver providerResolver,
        ISecretEncryptionService encryptionService,
        PreparedNavigateCodeFunction navigateCodeFunction,
        HttpMessageHandler httpMessageHandler,
        TimeProvider timeProvider,
        TimeSpan inactivityTimeout,
        TimeSpan absoluteTimeout,
        IPreparedPass1SubmissionDiagnosticObserver? diagnosticObserver = null,
        IPreparedPass1WireBudgetDiagnosticObserver? wireBudgetDiagnosticObserver = null,
        IPreparedPass1ExecutionDiagnosticObserver? executionDiagnosticObserver = null,
        IPass1NavigationAttemptReservationStore? navigationReservationStore = null)
        : this(
            providerResolver,
            encryptionService,
            navigateCodeFunction,
            httpMessageHandler,
            new PreparedSubmitCodeLogicFunction(diagnosticObserver),
            wireBudgetDiagnosticObserver,
            executionDiagnosticObserver,
            navigationReservationStore)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(inactivityTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(absoluteTimeout, TimeSpan.Zero);

        _timeProvider = timeProvider;
        _inactivityTimeout = inactivityTimeout;
        _absoluteTimeout = absoluteTimeout;
    }

    public async Task<PreparedPass1RawWorkerResult> ExecuteAsync(
        PreparedPass1WorkerPacket packet,
        string systemPrompt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        cancellationToken.ThrowIfCancellationRequested();

        PreparedPass1ServerAuthorization? authorization = packet.ServerAuthorization;
        if (authorization is null ||
            authorization.PairExecutionId == Guid.Empty ||
            authorization.JobStateVersion <= 0 ||
            authorization.PairStateVersion < 0 ||
            authorization.LeaseToken == Guid.Empty ||
            authorization.LeaseExpiresAt == default ||
            !string.Equals(authorization.PairState, nameof(Pass1PairExecutionPhase.Running), StringComparison.Ordinal) ||
            authorization.CircuitState != Pass1CircuitState.Closed ||
            authorization.JobState != ScanJobExecutionState.Running ||
            string.IsNullOrWhiteSpace(authorization.AuthorizationBinding))
        {
            return new(
                PreparedPass1RawWorkerOutcome.Rejected,
                ReadOnlyMemory<byte>.Empty,
                "security-failure");
        }

        ResolvedWorkerAgentProvider provider;
        string apiKey;
        try
        {
            provider = await providerResolver.ResolveAsync(packet.SystemId, cancellationToken);
            apiKey = await encryptionService.DecryptAsync(
                SecretPurposes.LlmApiKey,
                provider.EncryptedApiKey,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return ProviderConfiguration();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            return ProviderConfiguration();
        }

        string requestJson;
        try
        {
            requestJson = CreateRequestJson(packet);
        }
        catch (Exception exception) when (
            exception is DecoderFallbackException or InvalidDataException or ArgumentException)
        {
            return new(
                PreparedPass1RawWorkerOutcome.Rejected,
                ReadOnlyMemory<byte>.Empty,
                "integrity-failure");
        }

        long preparedInitialMessageBytes =
            (long)_strictUtf8.GetByteCount(systemPrompt) +
            _strictUtf8.GetByteCount(requestJson);
        if (preparedInitialMessageBytes >
            PreparedPass1WireBudget.MaximumPreparedInitialMessageBytes)
        {
            wireBudgetDiagnosticObserver?.Observe(
                PreparedPass1WireBudgetDiagnosticKind.PreparedInitialMessage);
            return ProviderRequestBoundExceeded();
        }

        PreparedPass1NavigationBudgetProjection budget = packet.NavigationBudget ?? new(
            8 - packet.RemainingNavigationBudget,
            0,
            0,
            0);
        int initialToolAttemptCount = budget.ToolAttemptCount;
        ulong toolAttemptOrdinal = checked((ulong)(initialToolAttemptCount + 1));
        var reservationScope = new PreparedPass1NavigationScope(
            packet.JobId,
            packet.RepositoryId,
            packet.ScanRunId,
            packet.ProfiledRevision,
            packet.FileCodeUnitId,
            new AnalysisScope(
                packet.RepositoryId,
                packet.ScanRunId,
                packet.ProfiledRevision,
                "reservation-runtime",
                packet.AnalysisRuntimeIdentityHash,
                new AnalysisDeadline(authorization.LeaseExpiresAt.ToUnixTimeMilliseconds())),
            [new PreparedPass1AuthorizedSource(
                packet.FileCodeUnitId,
                packet.RepositoryRelativePath,
                packet.SourceHash,
                new PreparedPass1LineRange(
                    checked((ulong)packet.LineStart),
                    checked((ulong)packet.LineEnd)),
                new PreparedPass1ByteRange(
                    checked((ulong)packet.ByteStart),
                    checked((ulong)packet.ByteEnd)),
                packet.SegmentOrdinal,
                $"src://{packet.RepositoryRelativePath}#L{packet.LineStart}-L{packet.LineEnd}")],
            initialToolAttemptCount,
            budget.AcceptedCallCount,
            budget.EvidenceBytes,
            [],
            authorization.PairExecutionId,
            authorization.JobStateVersion,
            authorization.PairStateVersion,
            authorization.LeaseToken,
            authorization.LeaseExpiresAt,
            authorization.PairState,
            authorization.CircuitState,
            authorization.JobState,
            authorization.AuthorizationBinding);
        var receiptProvenance = new PreparedPass1ReceiptProvenanceLedger();
        PreparedNavigateCodeSession navigateSession = navigateCodeFunction.CreateSession(new(
            packet.JobId,
            toolAttemptOrdinal,
            packet.FileCodeUnitId,
            authorization.PairExecutionId,
            authorization.JobStateVersion,
            authorization.PairStateVersion,
            authorization.LeaseToken,
            authorization.LeaseExpiresAt,
            authorization.PairState,
            authorization.CircuitState,
            authorization.JobState,
            authorization.AuthorizationBinding,
            reservationScope,
            initialToolAttemptCount == 0
                ? null
                : new PreparedPass1NavigationReservationTicket(
                    packet.JobId,
                    authorization.PairExecutionId,
                    authorization.LeaseToken,
                    initialToolAttemptCount,
                    initialToolAttemptCount,
                    budget.StateVersion)), navigationReservationStore, receiptProvenance);
        AIFunction navigateCode = navigateSession.Function;
        PreparedSubmitCodeLogicSession submitSession =
            (submitCodeLogicFunction ?? new PreparedSubmitCodeLogicFunction()).CreateSession(
                packet,
                receiptProvenance);
        DeepSeekStrictToolDeclarations acceptedDeclarations =
            DeepSeekStrictToolDeclarations.Create(navigateCode, submitSession.Function);
        PreparedPass1IllegalFunctionCallSubtype? illegalFunctionCallSubtype = null;
        PreparedPass1TerminalContextCorrection? firstSubmissionCorrection = null;
        using var terminalContext = new PreparedPass1TerminalContextAccumulator();
        ReadOnlyMemory<byte> originalPacketUtf8 = _strictUtf8.GetBytes(requestJson);
        string projectionPromptResource;
        try
        {
            projectionPromptResource = CreateProjectionPromptResource(systemPrompt);
            _ = PreparedPass1TerminalContextProjection.Create(new(
                projectionPromptResource,
                packet.PromptIdentity,
                originalPacketUtf8,
                10,
                [],
                BoundPromptIdentity: packet.PromptIdentity,
                BoundOriginalPacketUtf8: originalPacketUtf8));
        }
        catch (InvalidDataException exception) when (
            PreparedPass1TerminalFailure.Is(
                exception,
                PreparedPass1TerminalFailureKind.Prompt))
        {
            return ProviderConfiguration();
        }
        catch (DecoderFallbackException)
        {
            return ProviderConfiguration();
        }
        catch (InvalidDataException exception) when (
            PreparedPass1TerminalFailure.Is(
                exception,
                PreparedPass1TerminalFailureKind.Bound))
        {
            wireBudgetDiagnosticObserver?.Observe(
                PreparedPass1WireBudgetDiagnosticKind.OutgoingRequest);
            return ProviderRequestBoundExceeded();
        }
        catch (InvalidDataException exception) when (
            PreparedPass1TerminalFailure.Is(
                exception,
                PreparedPass1TerminalFailureKind.Integrity))
        {
            return IntegrityFailure();
        }

        var rawReadActivity = new RawReadActivityClock();

        using var absoluteCancellation = new CancellationTokenSource(
            _absoluteTimeout,
            _timeProvider);
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            absoluteCancellation.Token);
        using var wireBudget = new WireBudgetState(
            executionCancellation,
            wireBudgetDiagnosticObserver);
        using var boundedTransport = new WireBudgetTransportHandler(
            httpMessageHandler,
            wireBudget,
            rawReadActivity,
            acceptedDeclarations,
            requestOrdinal => requestOrdinal is 9 or 10 || !submitSession.NavigationAllowed);
        using var httpClient = new HttpClient(boundedTransport, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = provider.Endpoint,
            NetworkTimeout = _absoluteTimeout,
            RetryPolicy = new ClientRetryPolicy(0),
            Transport = new HttpClientPipelineTransport(httpClient, false, null),
            EnableDistributedTracing = false,
            ClientLoggingOptions = new ClientLoggingOptions
            {
                EnableLogging = false,
                EnableMessageLogging = false,
                EnableMessageContentLogging = false,
            },
        };
        var chatClient = new ChatClient(
            provider.Model,
            new ApiKeyCredential(apiKey),
            clientOptions);
        ChatClientAgent agent = chatClient.AsAIAgent(
            instructions: projectionPromptResource,
            name: "prepared-pass1-code-logic-worker",
            clientFactory: innerClient =>
            {
                var admission = new CompleteProviderTurnAdmission();
                var invokingClient = new FunctionInvokingChatClient(
                    new CompleteProviderTurnPreflightChatClient(
                        new ReasoningReplayChatClient(innerClient),
                        packet.RemainingNavigationBudget,
                        submitSession,
                        admission,
                        projectionPromptResource,
                        packet.PromptIdentity,
                        originalPacketUtf8,
                        terminalContext,
                        () => firstSubmissionCorrection,
                        subtype => illegalFunctionCallSubtype ??= subtype))
                {
                    AllowConcurrentInvocation = false,
                    IncludeDetailedErrors = false,
                    MaximumIterationsPerRequest = 11,
                    TerminateOnUnknownCalls = true,
                };
                invokingClient.FunctionInvoker = async (context, token) =>
                {
                    int providerTurn;
                    try
                    {
                        providerTurn = checked(context.Iteration + 1);
                    }
                    catch (OverflowException)
                    {
                        context.Terminate = true;
                        return "invalid-or-repeated-tool-call";
                    }

                    if (providerTurn > 10)
                    {
                        context.Terminate = true;
                        return "invalid-or-repeated-tool-call";
                    }

                    if (!admission.TryMatch(
                        providerTurn,
                        context.FunctionCount,
                        context.FunctionCallIndex,
                        out bool invokeNavigation))
                    {
                        context.Terminate = true;
                        return "invalid-or-repeated-tool-call";
                    }

                    if (string.Equals(
                            context.Function.Name,
                            "navigate_code",
                            StringComparison.Ordinal) &&
                        !invokeNavigation)
                    {
                        terminalContext.Add(
                            PreparedPass1TerminalContextResult.FromNavigationBudgetExhausted(
                                terminalContext.Count + 1));
                        return "navigation-budget-exhausted";
                    }

                    if (!submitSession.NavigationAllowed &&
                        string.Equals(context.Function.Name, "navigate_code", StringComparison.Ordinal))
                    {
                        context.Terminate = true;
                        return "invalid-or-repeated-tool-call";
                    }

                    object? result = await context.Function.InvokeAsync(context.Arguments, token);
                    if (string.Equals(
                            context.Function.Name,
                            "navigate_code",
                            StringComparison.Ordinal) &&
                        result is PreparedPass1NavigationResponse navigationResponse)
                    {
                        terminalContext.Add(
                            PreparedPass1TerminalContextResult.FromNavigationResponse(
                                terminalContext.Count + 1,
                                navigationResponse,
                                context.Function.JsonSerializerOptions));
                    }
                    else if (string.Equals(
                                 context.Function.Name,
                                 "submit_code_logic",
                                 StringComparison.Ordinal) &&
                             firstSubmissionCorrection is null &&
                             TryMapSubmissionCorrection(
                                 result,
                                 out PreparedPass1SubmissionRejectionKind rejectionKind))
                    {
                        firstSubmissionCorrection = new(
                            1,
                            rejectionKind,
                            GetRepairCode(rejectionKind));
                    }

                    if (providerTurn == 10 ||
                        submitSession.ShouldTerminate ||
                        navigateSession.ShouldTerminate())
                    {
                        context.Terminate = true;
                    }

                    return result;
                };
                return invokingClient;
            });
        var runOptions = new ChatClientAgentRunOptions(new ChatOptions
        {
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Json,
            AllowMultipleToolCalls = false,
            MaxOutputTokens = 32_768,
            Tools = [navigateCode, submitSession.Function],
        });

        AgentResponse response;
        try
        {
            IAsyncEnumerable<AgentResponseUpdate> updates = agent.RunStreamingAsync(
                requestJson,
                session: null,
                options: runOptions,
                executionCancellation.Token);
            response = await WithInactivityTimeoutAsync(
                    updates,
                    executionCancellation,
                    wireBudget,
                    rawReadActivity,
                    executionCancellation.Token)
                .ToAgentResponseAsync(executionCancellation.Token);
            cancellationToken.ThrowIfCancellationRequested();
            if (executionCancellation.IsCancellationRequested)
            {
                return ProviderUnavailable();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (Exception) when (wireBudget.Exceeded)
        {
            return ProviderRequestBoundExceeded();
        }
        catch (OperationCanceledException)
        {
            return ProviderUnavailable();
        }
        catch (TimeoutException)
        {
            return ProviderUnavailable();
        }
        catch (Exception) when (executionCancellation.IsCancellationRequested)
        {
            return ProviderUnavailable();
        }
        catch (InvalidDataException exception) when (
            PreparedPass1TerminalFailure.Is(
                exception,
                PreparedPass1TerminalFailureKind.Prompt))
        {
            return ProviderConfiguration();
        }
        catch (InvalidDataException exception) when (
            PreparedPass1TerminalFailure.Is(
                exception,
                PreparedPass1TerminalFailureKind.Bound))
        {
            wireBudgetDiagnosticObserver?.Observe(
                PreparedPass1WireBudgetDiagnosticKind.OutgoingRequest);
            return ProviderRequestBoundExceeded();
        }
        catch (InvalidDataException exception) when (
            PreparedPass1TerminalFailure.Is(
                exception,
                PreparedPass1TerminalFailureKind.Integrity))
        {
            return IntegrityFailure();
        }
        catch (ClientResultException exception) when (exception.Status is 401 or 402 or 403)
        {
            return ProviderConfiguration();
        }
        catch (ClientResultException exception) when (
            exception.Status is 408 or 429 || exception.Status >= 500)
        {
            return ProviderUnavailable();
        }
        catch (ClientResultException)
        {
            executionDiagnosticObserver?.Observe(
                PreparedPass1ExecutionDiagnosticKind.ProviderClientRequestRejected);
            return new(
                PreparedPass1RawWorkerOutcome.Rejected,
                ReadOnlyMemory<byte>.Empty,
                "unclassified-systemic");
        }
        catch (HttpRequestException)
        {
            return ProviderUnavailable();
        }
        catch (Exception)
        {
            executionDiagnosticObserver?.Observe(
                PreparedPass1ExecutionDiagnosticKind.UnexpectedException);
            return new(
                PreparedPass1RawWorkerOutcome.Rejected,
                ReadOnlyMemory<byte>.Empty,
                "unclassified-systemic");
        }

        FunctionCallContent[][] functionCallGroups = response.Messages
            .Select(message => message.Contents.OfType<FunctionCallContent>().ToArray())
            .Where(calls => calls.Length > 0)
            .ToArray();
        if (illegalFunctionCallSubtype is null && functionCallGroups
            .SelectMany(calls => calls)
            .Any(call =>
                !string.Equals(call.Name, "navigate_code", StringComparison.Ordinal) &&
                !string.Equals(call.Name, "submit_code_logic", StringComparison.Ordinal)))
        {
            illegalFunctionCallSubtype =
                PreparedPass1IllegalFunctionCallSubtype.UnknownFunction;
        }
        if (illegalFunctionCallSubtype is not null)
        {
            executionDiagnosticObserver?.ObserveIllegalFunctionCall(
                illegalFunctionCallSubtype.Value);
            if (illegalFunctionCallSubtype !=
                    PreparedPass1IllegalFunctionCallSubtype.MissingFunction &&
                illegalFunctionCallSubtype !=
                    PreparedPass1IllegalFunctionCallSubtype.NavigationUnavailable)
            {
                return new(
                    PreparedPass1RawWorkerOutcome.Rejected,
                    ReadOnlyMemory<byte>.Empty,
                    "unclassified-systemic");
            }
        }

        byte[]? capturedCanonicalJson = submitSession.TakeCapturedCanonicalJson();
        if (capturedCanonicalJson is not null)
        {
            return new(PreparedPass1RawWorkerOutcome.Completed, capturedCanonicalJson, null);
        }

        if (submitSession.HasSubmission)
        {
            return new(
                PreparedPass1RawWorkerOutcome.Rejected,
                ReadOnlyMemory<byte>.Empty,
                "worker-result-rejected");
        }

        submitSession.ReportMissingSubmission();
        return new(
            PreparedPass1RawWorkerOutcome.Rejected,
            ReadOnlyMemory<byte>.Empty,
            "worker-result-rejected");
    }

    private async IAsyncEnumerable<AgentResponseUpdate> WithInactivityTimeoutAsync(
        IAsyncEnumerable<AgentResponseUpdate> updates,
        CancellationTokenSource executionCancellation,
        WireBudgetState wireBudget,
        RawReadActivityClock rawReadActivity,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using IAsyncEnumerator<AgentResponseUpdate> enumerator =
            updates.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool hasNext;
            try
            {
                Task<bool> moveNext = enumerator.MoveNextAsync().AsTask();
                while (!moveNext.IsCompleted)
                {
                    (long generation, Task pulse) = rawReadActivity.Observe();
                    using var timeoutCancellation =
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    Task timeout = Task.Delay(
                        _inactivityTimeout,
                        _timeProvider,
                        timeoutCancellation.Token);
                    Task completed = await Task.WhenAny(moveNext, pulse, timeout);
                    if (completed == moveNext)
                    {
                        await timeoutCancellation.CancelAsync();
                        break;
                    }

                    if (completed == pulse || rawReadActivity.HasAdvanced(generation))
                    {
                        await timeoutCancellation.CancelAsync();
                        continue;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    await executionCancellation.CancelAsync();
                    throw new TimeoutException();
                }

                hasNext = await moveNext;
            }
            catch (TimeoutException)
            {
                await executionCancellation.CancelAsync();
                throw;
            }

            if (!hasNext)
            {
                yield break;
            }

            if (!wireBudget.TryAddAssembled(enumerator.Current))
            {
                throw new InvalidDataException("Provider request bound exceeded.");
            }

            yield return enumerator.Current;
        }
    }

    private sealed class WireBudgetState(
        CancellationTokenSource executionCancellation,
        IPreparedPass1WireBudgetDiagnosticObserver? diagnosticObserver)
        : IDisposable
    {
        private readonly object _sync = new();
        private string? _responseId;
        private int _assembledResponseBytes;
        private int _exceeded;
        private CancellationTokenSource? _transportCancellation;

        internal bool Exceeded => Volatile.Read(ref _exceeded) != 0;

        internal CancellationToken BindTransportCancellation(CancellationToken token)
        {
            lock (_sync)
            {
                _transportCancellation?.Dispose();
                _transportCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
                return _transportCancellation.Token;
            }
        }

        internal bool TryAddAssembled(AgentResponseUpdate update)
        {
            if (!string.Equals(_responseId, update.ResponseId, StringComparison.Ordinal))
            {
                _responseId = update.ResponseId;
                _assembledResponseBytes = 0;
            }

            int updateBytes = 0;
            foreach (AIContent content in update.Contents)
            {
                updateBytes = checked(updateBytes + content switch
                {
                    TextReasoningContent reasoning =>
                        _strictUtf8.GetByteCount(reasoning.Text) + 24,
                    TextContent text => _strictUtf8.GetByteCount(text.Text) + 16,
                    FunctionCallContent call =>
                        _strictUtf8.GetByteCount(call.CallId) +
                        _strictUtf8.GetByteCount(call.Name) +
                        JsonSerializer.SerializeToUtf8Bytes(call.Arguments).Length +
                        64,
                    _ => 0,
                });
            }

            _assembledResponseBytes = checked(_assembledResponseBytes + updateBytes);
            if (_assembledResponseBytes <=
                PreparedPass1WireBudget.MaximumAssembledResponseBytes)
            {
                return true;
            }

            Exceed(PreparedPass1WireBudgetDiagnosticKind.AssembledResponse);
            return false;
        }

        internal void Exceed(PreparedPass1WireBudgetDiagnosticKind category)
        {
            bool firstExceeded = Interlocked.Exchange(ref _exceeded, 1) == 0;
            if (firstExceeded)
            {
                diagnosticObserver?.Observe(category);
            }

            lock (_sync)
            {
                _transportCancellation?.Cancel();
            }

            executionCancellation.Cancel();
        }

        internal void ExceedOutgoingRequest(PreparedPass1OutgoingRequestRejectionKind kind)
        {
            bool firstExceeded = Interlocked.Exchange(ref _exceeded, 1) == 0;
            if (firstExceeded)
            {
                diagnosticObserver?.ObserveOutgoingRequestRejection(kind);
            }

            lock (_sync)
            {
                _transportCancellation?.Cancel();
            }

            executionCancellation.Cancel();
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _transportCancellation?.Dispose();
                _transportCancellation = null;
            }
        }
    }

    private sealed class WireBudgetTransportHandler(
        HttpMessageHandler innerHandler,
        WireBudgetState wireBudget,
        RawReadActivityClock rawReadActivity,
        DeepSeekStrictToolDeclarations acceptedDeclarations,
        Func<int, bool> isTerminalRequest) : DelegatingHandler(innerHandler)
    {
        private int _requestOrdinal;

        protected override void Dispose(bool disposing)
        {
            // The caller owns the injected transport handler; this execution-local
            // decorator has no independent disposable state.
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            int requestOrdinal = Interlocked.Increment(ref _requestOrdinal);
            if (requestOrdinal > 10)
            {
                throw RejectOutgoingRequest(PreparedPass1OutgoingRequestRejectionKind.TurnLimit);
            }

            byte[] outgoingBody = await ReadBoundedRequestBodyAsync(
                request.Content,
                cancellationToken);
            DeepSeekToolChoiceShape toolChoiceShape = isTerminalRequest(requestOrdinal)
                ? DeepSeekToolChoiceShape.Terminal
                : DeepSeekToolChoiceShape.Ordinary;
            outgoingBody = NormalizeAndValidateDeepSeekRequestBody(
                    outgoingBody,
                    acceptedDeclarations,
                    toolChoiceShape) ??
                throw RejectOutgoingRequest(ClassifyNormalizationFailure(outgoingBody));
            ValidateOutgoingContributions(
                outgoingBody,
                toolChoiceShape);
            ReplaceRequestContent(request, outgoingBody);

            CancellationToken transportCancellation =
                wireBudget.BindTransportCancellation(cancellationToken);
            HttpResponseMessage response = await base.SendAsync(
                request,
                transportCancellation);
            if (response.Content is not null)
            {
                Stream rawStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var boundedContent = new StreamContent(new RawResponseBoundedStream(
                    rawStream,
                    wireBudget,
                    rawReadActivity));
                foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
                {
                    boundedContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                response.Content = boundedContent;
            }

            return response;
        }

        private InvalidDataException RejectOutgoingRequest(
            PreparedPass1OutgoingRequestRejectionKind kind)
        {
            wireBudget.ExceedOutgoingRequest(kind);
            return new InvalidDataException("Provider request bound exceeded.");
        }

        private async Task<byte[]> ReadBoundedRequestBodyAsync(
            HttpContent? content,
            CancellationToken cancellationToken)
        {
            if (content is null)
            {
                return [];
            }

            if (content.Headers.ContentLength >
                PreparedPass1WireBudget.MaximumOutgoingBodyBytes)
            {
                wireBudget.ExceedOutgoingRequest(
                    PreparedPass1OutgoingRequestRejectionKind.BodySize);
                throw new InvalidDataException("Provider request bound exceeded.");
            }

            await using Stream source = await content.ReadAsStreamAsync(cancellationToken);
            using var destination = new MemoryStream();
            byte[] buffer = new byte[16_384];
            while (true)
            {
                int read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    return destination.ToArray();
                }

                if (destination.Length + read >
                    PreparedPass1WireBudget.MaximumOutgoingBodyBytes)
                {
                    wireBudget.ExceedOutgoingRequest(
                        PreparedPass1OutgoingRequestRejectionKind.BodySize);
                    throw new InvalidDataException("Provider request bound exceeded.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }

        private void ValidateOutgoingContributions(
            byte[] body,
            DeepSeekToolChoiceShape toolChoiceShape)
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (!HasExactFinalDeepSeekShape(
                root,
                acceptedDeclarations,
                toolChoiceShape))
            {
                throw RejectOutgoingRequest(
                    PreparedPass1OutgoingRequestRejectionKind.StrictShape);
            }

            int declarationsBytes = root.TryGetProperty("tools", out JsonElement tools)
                ? _strictUtf8.GetByteCount(tools.GetRawText())
                : 0;
            int initialBytes = 0;
            int toolResultBytes = 0;
            List<int> assistantBytes = [];
            if (root.TryGetProperty("messages", out JsonElement messages))
            {
                foreach (JsonElement message in messages.EnumerateArray())
                {
                    int contributionBytes = _strictUtf8.GetByteCount(message.GetRawText());
                    string? role = message.GetProperty("role").GetString();
                    if (string.Equals(role, "assistant", StringComparison.Ordinal))
                    {
                        assistantBytes.Add(contributionBytes);
                    }
                    else if (string.Equals(role, "tool", StringComparison.Ordinal))
                    {
                        toolResultBytes = checked(toolResultBytes + contributionBytes);
                    }
                    else
                    {
                        initialBytes = checked(initialBytes + contributionBytes);
                    }
                }
            }

            if (!PreparedPass1WireBudget.IsWithinLimit(
                initialBytes,
                declarationsBytes,
                rawResponseBytes: 0,
                assembledResponseBytes: 0,
                assistantBytes,
                toolResultBytes,
                body.Length))
            {
                throw RejectOutgoingRequest(
                    PreparedPass1OutgoingRequestRejectionKind.ContributionBudget);
            }
        }

        private static PreparedPass1OutgoingRequestRejectionKind ClassifyNormalizationFailure(
            byte[] sourceBody)
        {
            if (sourceBody.Length > PreparedPass1WireBudget.MaximumOutgoingBodyBytes)
            {
                return PreparedPass1OutgoingRequestRejectionKind.NormalizationSize;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(sourceBody, new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64,
                });
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !HasUniquePropertyNames(root))
                {
                    return PreparedPass1OutgoingRequestRejectionKind.NormalizationJson;
                }

                if (root.TryGetProperty("messages", out JsonElement messages))
                {
                    using var sink = new MemoryStream();
                    using var writer = new Utf8JsonWriter(sink);
                    if (!WriteMessagesWithCompleteAssistantContent(writer, messages))
                    {
                        return ClassifyMessageNormalizationFailure(messages);
                    }
                }

                return PreparedPass1OutgoingRequestRejectionKind.NormalizationParameters;
            }
            catch (Exception exception) when (
                exception is JsonException or InvalidOperationException or OverflowException)
            {
                return PreparedPass1OutgoingRequestRejectionKind.NormalizationJson;
            }
        }

        private static PreparedPass1OutgoingRequestRejectionKind
            ClassifyMessageNormalizationFailure(JsonElement messages)
        {
            if (messages.ValueKind != JsonValueKind.Array)
            {
                return PreparedPass1OutgoingRequestRejectionKind.NormalizationMessageEnvelope;
            }

            foreach (JsonElement message in messages.EnumerateArray())
            {
                if (message.ValueKind != JsonValueKind.Object ||
                    !message.TryGetProperty("role", out JsonElement role) ||
                    role.ValueKind != JsonValueKind.String)
                {
                    return PreparedPass1OutgoingRequestRejectionKind.NormalizationMessageEnvelope;
                }

                bool isAssistantToolCall =
                    string.Equals(role.GetString(), "assistant", StringComparison.Ordinal) &&
                    message.TryGetProperty("tool_calls", out _);
                if (isAssistantToolCall &&
                    message.TryGetProperty("content", out JsonElement content) &&
                    content.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                {
                    return content.ValueKind == JsonValueKind.Array
                        ? PreparedPass1OutgoingRequestRejectionKind
                            .NormalizationAssistantToolContentArray
                        : PreparedPass1OutgoingRequestRejectionKind
                            .NormalizationAssistantToolContentOther;
                }
            }

            return PreparedPass1OutgoingRequestRejectionKind.NormalizationMessages;
        }

        private static bool HasExactFinalDeepSeekShape(
            JsonElement root,
            DeepSeekStrictToolDeclarations acceptedDeclarations,
            DeepSeekToolChoiceShape toolChoiceShape)
        {
            byte[] expectedDeclarations = toolChoiceShape == DeepSeekToolChoiceShape.Terminal
                ? acceptedDeclarations.ProjectedSubmitOnlyUtf8
                : acceptedDeclarations.ProjectedUtf8;
            int expectedDeclarationCount = toolChoiceShape == DeepSeekToolChoiceShape.Terminal
                ? 1
                : 2;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("max_tokens", out JsonElement maxTokens) ||
                maxTokens.ValueKind != JsonValueKind.Number ||
                !string.Equals(maxTokens.GetRawText(), "32768", StringComparison.Ordinal) ||
                root.TryGetProperty("max_completion_tokens", out _) ||
                !root.TryGetProperty("thinking", out JsonElement thinking) ||
                !IsExactDisabledThinking(thinking) ||
                !root.TryGetProperty("tool_choice", out JsonElement toolChoice) ||
                !IsExactToolChoice(toolChoice, toolChoiceShape) ||
                root.TryGetProperty("reasoning_effort", out _) ||
                root.TryGetProperty("parallel_tool_calls", out _) ||
                root.TryGetProperty("response_format", out _) ||
                root.TryGetProperty("temperature", out _) ||
                root.TryGetProperty("top_p", out _) ||
                root.TryGetProperty("presence_penalty", out _) ||
                root.TryGetProperty("frequency_penalty", out _) ||
                !root.TryGetProperty("stream", out JsonElement stream) ||
                stream.ValueKind != JsonValueKind.True ||
                !root.TryGetProperty("tools", out JsonElement tools) ||
                tools.ValueKind != JsonValueKind.Array ||
                tools.GetArrayLength() != expectedDeclarationCount ||
                !root.TryGetProperty("messages", out JsonElement messages) ||
                messages.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return _strictUtf8.GetBytes(tools.GetRawText())
                    .AsSpan()
                    .SequenceEqual(expectedDeclarations) &&
                HasCompleteToolContinuity(messages);
        }

        private static bool HasCompleteToolContinuity(JsonElement messages)
        {
            HashSet<string>? pendingToolCallIds = null;
            foreach (JsonElement message in messages.EnumerateArray())
            {
                if (message.ValueKind != JsonValueKind.Object ||
                    !message.TryGetProperty("role", out JsonElement roleElement) ||
                    roleElement.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                string? role = roleElement.GetString();
                if (string.Equals(role, "assistant", StringComparison.Ordinal))
                {
                    if (pendingToolCallIds is { Count: > 0 })
                    {
                        return false;
                    }

                    if (!message.TryGetProperty("tool_calls", out JsonElement toolCalls))
                    {
                        continue;
                    }

                    if (!message.TryGetProperty("content", out JsonElement content) ||
                        content.ValueKind != JsonValueKind.String ||
                        toolCalls.ValueKind != JsonValueKind.Array ||
                        toolCalls.GetArrayLength() == 0)
                    {
                        return false;
                    }

                    pendingToolCallIds = new(StringComparer.Ordinal);
                    foreach (JsonElement toolCall in toolCalls.EnumerateArray())
                    {
                        if (toolCall.ValueKind != JsonValueKind.Object ||
                            !toolCall.TryGetProperty("id", out JsonElement id) ||
                            id.ValueKind != JsonValueKind.String ||
                            string.IsNullOrEmpty(id.GetString()) ||
                            !pendingToolCallIds.Add(id.GetString()!))
                        {
                            return false;
                        }
                    }
                }
                else if (string.Equals(role, "tool", StringComparison.Ordinal))
                {
                    if (pendingToolCallIds is null ||
                        !message.TryGetProperty("tool_call_id", out JsonElement toolCallId) ||
                        toolCallId.ValueKind != JsonValueKind.String ||
                        !pendingToolCallIds.Remove(toolCallId.GetString()!))
                    {
                        return false;
                    }

                    if (pendingToolCallIds.Count == 0)
                    {
                        pendingToolCallIds = null;
                    }
                }
                else if (pendingToolCallIds is { Count: > 0 })
                {
                    return false;
                }
            }

            return pendingToolCallIds is null;
        }

        private static void ReplaceRequestContent(HttpRequestMessage request, byte[] body)
        {
            HttpContent? original = request.Content;
            var replacement = new ByteArrayContent(body);
            if (original is not null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in original.Headers)
                {
                    if (string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            request.Content = replacement;
        }
    }

    private sealed class RawResponseBoundedStream(
        Stream inner,
        WireBudgetState wireBudget,
        RawReadActivityClock rawReadActivity) : Stream
    {
        private int _readBytes;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _readBytes;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int read = await inner.ReadAsync(buffer, cancellationToken);
            if (_readBytes + read > PreparedPass1WireBudget.MaximumRawResponseBytes)
            {
                wireBudget.Exceed(PreparedPass1WireBudgetDiagnosticKind.RawResponse);
                throw new InvalidDataException("Provider request bound exceeded.");
            }

            _readBytes += read;
            if (read > 0)
            {
                rawReadActivity.Pulse();
            }

            return read;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ReasoningReplayChatClient(IChatClient innerClient)
        : DelegatingChatClient(innerClient)
    {
        public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(messages);

            List<Microsoft.Extensions.AI.ChatMessage> replayMessages = messages.ToList();
            foreach (Microsoft.Extensions.AI.ChatMessage message in replayMessages)
            {
                if (message.Role != ChatRole.Assistant)
                {
                    continue;
                }

                string reasoning = string.Concat(
                    message.Contents
                        .OfType<TextReasoningContent>()
                        .Select(content => content.Text));
                if (reasoning.Length == 0)
                {
                    continue;
                }

                OpenAI.Chat.ChatMessage rawMessage = new[] { message }
                    .AsOpenAIChatMessages(new ChatOptions())
                    .Single();
#pragma warning disable SCME0001
                rawMessage.Patch.Set("$.reasoning_content"u8, reasoning);
#pragma warning restore SCME0001
                message.RawRepresentation = rawMessage;
            }

            return base.GetStreamingResponseAsync(
                replayMessages,
                options,
                cancellationToken);
        }
    }

    private sealed class CompleteProviderTurnPreflightChatClient(
        IChatClient innerClient,
        int remainingNavigationBudget,
        PreparedSubmitCodeLogicSession submitSession,
        CompleteProviderTurnAdmission admission,
        string promptResource,
        string promptIdentity,
        ReadOnlyMemory<byte> originalPacketUtf8,
        PreparedPass1TerminalContextAccumulator terminalContext,
        Func<PreparedPass1TerminalContextCorrection?> getFirstSubmissionCorrection,
        Action<PreparedPass1IllegalFunctionCallSubtype> reject)
        : DelegatingChatClient(innerClient)
    {
        private int _providerTurn;
        private int _remainingNavigationBudget = remainingNavigationBudget;

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(messages);

            ChatOptions? requestOptions = options;
            int providerTurn = checked(_providerTurn + 1);
            PreparedPass1TerminalContextCorrection? correction =
                getFirstSubmissionCorrection();
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> requestMessages = messages;
            if (PreparedPass1TerminalContextProjection.ShouldProject(providerTurn, correction))
            {
                requestOptions = options?.Clone() ?? new ChatOptions();
                requestOptions.Instructions = null;
                requestOptions.ToolMode = ChatToolMode.RequireSpecific("submit_code_logic");
                requestOptions.Tools = [submitSession.Function];
                PreparedPass1TerminalContextProjectionResult projection =
                    PreparedPass1TerminalContextProjection.Create(new(
                        promptResource,
                        promptIdentity,
                        originalPacketUtf8,
                        providerTurn,
                        terminalContext.Snapshot(),
                        correction,
                        promptIdentity,
                        originalPacketUtf8));
                requestMessages = projection.Messages;
            }

            List<ChatResponseUpdate> updates = [];
            await foreach (ChatResponseUpdate update in base
                .GetStreamingResponseAsync(requestMessages, requestOptions, cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                updates.Add(update);
            }

            _providerTurn++;
            FunctionCallContent[] calls = updates
                .SelectMany(update => update.Contents.OfType<FunctionCallContent>())
                .ToArray();
            if (calls.Length == 0)
            {
                reject(PreparedPass1IllegalFunctionCallSubtype.MissingFunction);
                yield break;
            }

            if (!TryAccept(
                calls,
                out PreparedPass1IllegalFunctionCallSubtype subtype,
                out bool classify,
                out int admittedPrefixLength))
            {
                if (classify)
                {
                    reject(subtype);
                }

                yield break;
            }

            admission.Admit(_providerTurn, calls.Length, admittedPrefixLength);
            foreach (ChatResponseUpdate update in updates)
            {
                yield return update;
            }
        }

        private bool TryAccept(
            IReadOnlyList<FunctionCallContent> calls,
            out PreparedPass1IllegalFunctionCallSubtype subtype,
            out bool classify,
            out int admittedPrefixLength)
        {
            subtype = PreparedPass1IllegalFunctionCallSubtype.InvalidCallIdentity;
            classify = true;
            admittedPrefixLength = 0;

            bool hasUniqueNonblankCallIds = calls.All(call =>
                    !string.IsNullOrWhiteSpace(call.CallId)) &&
                calls.Select(call => call.CallId).Distinct(StringComparer.Ordinal).Count() == calls.Count;
            if (!hasUniqueNonblankCallIds)
            {
                return false;
            }

            bool hasUnknownFunction = calls.Any(call =>
                !string.Equals(call.Name, "navigate_code", StringComparison.Ordinal) &&
                !string.Equals(call.Name, "submit_code_logic", StringComparison.Ordinal));
            if (hasUnknownFunction)
            {
                subtype = PreparedPass1IllegalFunctionCallSubtype.UnknownFunction;
                return false;
            }

            bool soleSubmission = calls.Count == 1 &&
                string.Equals(calls[0].Name, "submit_code_logic", StringComparison.Ordinal);
            if (_providerTurn is 9 or 10)
            {
                if (soleSubmission)
                {
                    bool submissionAvailable = !submitSession.ShouldTerminate &&
                        (_providerTurn == 9 || submitSession.HasSubmission);
                    if (submissionAvailable)
                    {
                        return true;
                    }

                    subtype = PreparedPass1IllegalFunctionCallSubtype.SubmissionUnavailable;
                    return false;
                }

                if (calls.Count > 1 && calls.All(call =>
                        string.Equals(
                            call.Name,
                            "submit_code_logic",
                            StringComparison.Ordinal)))
                {
                    subtype = PreparedPass1IllegalFunctionCallSubtype.TerminalMultipleSubmissions;
                    return false;
                }

                bool terminalAllNavigation = calls.All(call =>
                    string.Equals(call.Name, "navigate_code", StringComparison.Ordinal));
                if (calls.Count == 1 && terminalAllNavigation)
                {
                    subtype = PreparedPass1IllegalFunctionCallSubtype.TerminalNavigation;
                    return false;
                }

                if (calls.Count > 1 && terminalAllNavigation)
                {
                    subtype = PreparedPass1IllegalFunctionCallSubtype.TerminalMultipleNavigations;
                    return false;
                }

                bool terminalMixedFunctions = calls.Any(call =>
                        string.Equals(call.Name, "navigate_code", StringComparison.Ordinal)) &&
                    calls.Any(call =>
                        string.Equals(call.Name, "submit_code_logic", StringComparison.Ordinal));
                subtype = terminalMixedFunctions
                    ? PreparedPass1IllegalFunctionCallSubtype.TerminalMixedFunctions
                    : PreparedPass1IllegalFunctionCallSubtype.TerminalTurnRequiresSubmission;
                return false;
            }

            if (_providerTurn > 10)
            {
                subtype = PreparedPass1IllegalFunctionCallSubtype.PostTurnLimit;
                return false;
            }

            if (soleSubmission)
            {
                if (!submitSession.ShouldTerminate)
                {
                    return true;
                }

                subtype = PreparedPass1IllegalFunctionCallSubtype.SubmissionUnavailable;
                return false;
            }

            bool allNavigation = calls.All(call =>
                string.Equals(call.Name, "navigate_code", StringComparison.Ordinal));
            bool hasNavigation = calls.Any(call =>
                string.Equals(call.Name, "navigate_code", StringComparison.Ordinal));
            bool hasSubmission = calls.Any(call =>
                string.Equals(call.Name, "submit_code_logic", StringComparison.Ordinal));
            if (hasNavigation && hasSubmission)
            {
                subtype = PreparedPass1IllegalFunctionCallSubtype.MixedFunctions;
                return false;
            }

            if (hasSubmission)
            {
                subtype = PreparedPass1IllegalFunctionCallSubtype.MultipleSubmissions;
                return false;
            }

            if (allNavigation && !submitSession.NavigationAllowed)
            {
                subtype = PreparedPass1IllegalFunctionCallSubtype.NavigationUnavailable;
                return false;
            }

            if (allNavigation && calls.Count > 8)
            {
                subtype = PreparedPass1IllegalFunctionCallSubtype.NavigationBudgetExceeded;
                return false;
            }

            if (allNavigation)
            {
                admittedPrefixLength = Math.Min(calls.Count, _remainingNavigationBudget);
                _remainingNavigationBudget -= admittedPrefixLength;
                return true;
            }

            subtype = PreparedPass1IllegalFunctionCallSubtype.MultipleFunctions;
            return false;
        }
    }

    private sealed class CompleteProviderTurnAdmission
    {
        private readonly object _sync = new();
        private int _providerTurn;
        private int _functionCount;
        private int _admittedPrefixLength;

        internal void Admit(
            int providerTurn,
            int functionCount,
            int admittedPrefixLength)
        {
            lock (_sync)
            {
                _providerTurn = providerTurn;
                _functionCount = functionCount;
                _admittedPrefixLength = admittedPrefixLength;
            }
        }

        internal bool TryMatch(
            int providerTurn,
            int functionCount,
            int functionCallIndex,
            out bool invokeNavigation)
        {
            lock (_sync)
            {
                bool matches =
                    providerTurn == _providerTurn &&
                    functionCount == _functionCount &&
                    functionCallIndex >= 0 &&
                    functionCallIndex < _functionCount;
                invokeNavigation = matches &&
                    functionCallIndex < _admittedPrefixLength;
                return matches;
            }
        }
    }

    private sealed class RawReadActivityClock
    {
        private readonly object _sync = new();
        private long _generation;
        private TaskCompletionSource<bool> _nextPulse = NewPulse();

        internal void Pulse()
        {
            TaskCompletionSource<bool> observed;
            lock (_sync)
            {
                _generation++;
                observed = _nextPulse;
                _nextPulse = NewPulse();
            }

            observed.TrySetResult(true);
        }

        internal (long Generation, Task Pulse) Observe()
        {
            lock (_sync)
            {
                return (_generation, _nextPulse.Task);
            }
        }

        internal bool HasAdvanced(long generation) =>
            Volatile.Read(ref _generation) != generation;

        private static TaskCompletionSource<bool> NewPulse() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static byte[]? NormalizeAndValidateDeepSeekRequestBody(byte[] sourceBody)
        => NormalizeAndValidateDeepSeekRequestBody(
            sourceBody,
            acceptedDeclarations: null,
            DeepSeekToolChoiceShape.Ordinary);

    private static byte[]? NormalizeAndValidateDeepSeekRequestBody(
        byte[] sourceBody,
        int providerTurn)
    {
        if (providerTurn is < 1 or > 10)
        {
            return null;
        }

        return NormalizeAndValidateDeepSeekRequestBody(
            sourceBody,
            acceptedDeclarations: null,
            providerTurn is 9 or 10
                ? DeepSeekToolChoiceShape.Terminal
                : DeepSeekToolChoiceShape.Ordinary);
    }

    private static byte[]? NormalizeAndValidateDeepSeekRequestBody(
        byte[] sourceBody,
        DeepSeekStrictToolDeclarations? acceptedDeclarations,
        DeepSeekToolChoiceShape toolChoiceShape)
    {
        ArgumentNullException.ThrowIfNull(sourceBody);
        if (sourceBody.Length > PreparedPass1WireBudget.MaximumOutgoingBodyBytes)
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(sourceBody, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !HasUniquePropertyNames(root))
            {
                return null;
            }

            int maxTokensCount = 0;
            int maxCompletionTokensCount = 0;
            int thinkingCount = 0;
            int toolChoiceCount = 0;
            int streamCount = 0;
            int toolsCount = 0;
            foreach (JsonProperty property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "temperature":
                    case "top_p":
                    case "presence_penalty":
                    case "frequency_penalty":
                    case "reasoning_effort":
                        return null;
                    case "max_tokens":
                        maxTokensCount++;
                        if (!IsExactMaximumTokenValue(property.Value))
                        {
                            return null;
                        }

                        break;
                    case "max_completion_tokens":
                        maxCompletionTokensCount++;
                        if (!IsExactMaximumTokenValue(property.Value))
                        {
                            return null;
                        }

                        break;
                    case "thinking":
                        thinkingCount++;
                        if (!IsExactDisabledThinking(property.Value))
                        {
                            return null;
                        }

                        break;
                    case "stream":
                        streamCount++;
                        if (property.Value.ValueKind != JsonValueKind.True)
                        {
                            return null;
                        }

                        break;
                    case "tool_choice":
                        toolChoiceCount++;
                        if (toolChoiceShape == DeepSeekToolChoiceShape.Ordinary
                                ? property.Value.ValueKind != JsonValueKind.String ||
                                  property.Value.GetString() is not ("auto" or "required")
                                : !IsExactNamedSubmitToolChoice(property.Value))
                        {
                            return null;
                        }

                        break;
                    case "parallel_tool_calls":
                        if (property.Value.ValueKind != JsonValueKind.False)
                        {
                            return null;
                        }

                        break;
                    case "response_format":
                        if (!IsExactJsonObjectResponseFormat(property.Value))
                        {
                            return null;
                        }

                        break;
                    case "tools":
                        toolsCount++;
                        if (property.Value.ValueKind != JsonValueKind.Array)
                        {
                            return null;
                        }

                        break;
                }
            }

            if (maxTokensCount + maxCompletionTokensCount != 1 ||
                thinkingCount > 1 ||
                toolChoiceCount > 1 ||
                (toolChoiceShape == DeepSeekToolChoiceShape.Terminal &&
                    toolChoiceCount != 1) ||
                streamCount != 1 ||
                (acceptedDeclarations is not null && toolsCount != 1))
            {
                return null;
            }

            using var destination = new MemoryStream(Math.Min(
                sourceBody.Length + 48,
                PreparedPass1WireBudget.MaximumOutgoingBodyBytes));
            using (var writer = new Utf8JsonWriter(destination))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (property.Name is "thinking" or
                        "tool_choice" or
                        "parallel_tool_calls" or
                        "response_format")
                    {
                        continue;
                    }

                    writer.WritePropertyName(
                        string.Equals(
                            property.Name,
                            "max_completion_tokens",
                            StringComparison.Ordinal)
                            ? "max_tokens"
                            : property.Name);
                    if (acceptedDeclarations is not null &&
                        string.Equals(property.Name, "tools", StringComparison.Ordinal))
                    {
                        byte[] projectedDeclarations =
                            toolChoiceShape == DeepSeekToolChoiceShape.Terminal
                                ? acceptedDeclarations.ProjectedSubmitOnlyUtf8
                                : acceptedDeclarations.ProjectedUtf8;
                        writer.WriteRawValue(
                            projectedDeclarations,
                            skipInputValidation: false);
                    }
                    else if (string.Equals(property.Name, "messages", StringComparison.Ordinal))
                    {
                        if (!WriteMessagesWithCompleteAssistantContent(writer, property.Value))
                        {
                            return null;
                        }
                    }
                    else
                    {
                        property.Value.WriteTo(writer);
                    }
                }

                writer.WritePropertyName("thinking");
                writer.WriteStartObject();
                writer.WriteString("type", "disabled");
                writer.WriteEndObject();
                if (toolChoiceShape == DeepSeekToolChoiceShape.Ordinary)
                {
                    writer.WriteString("tool_choice", "required");
                }
                else
                {
                    writer.WriteStartObject("tool_choice");
                    writer.WriteString("type", "function");
                    writer.WriteStartObject("function");
                    writer.WriteString("name", "submit_code_logic");
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            if (destination.Length > PreparedPass1WireBudget.MaximumOutgoingBodyBytes)
            {
                return null;
            }

            return destination.ToArray();
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or OverflowException)
        {
            return null;
        }
    }

    private static bool IsExactMaximumTokenValue(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number &&
        string.Equals(value.GetRawText(), "32768", StringComparison.Ordinal);

    private static bool IsExactToolChoice(
        JsonElement value,
        DeepSeekToolChoiceShape toolChoiceShape) =>
        toolChoiceShape == DeepSeekToolChoiceShape.Ordinary
            ? value.ValueKind == JsonValueKind.String &&
              string.Equals(value.GetString(), "required", StringComparison.Ordinal)
            : IsExactNamedSubmitToolChoice(value);

    private static bool IsExactNamedSubmitToolChoice(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        JsonProperty[] properties = value.EnumerateObject().ToArray();
        if (properties.Length != 2 ||
            !value.TryGetProperty("type", out JsonElement type) ||
            type.ValueKind != JsonValueKind.String ||
            !string.Equals(type.GetString(), "function", StringComparison.Ordinal) ||
            !value.TryGetProperty("function", out JsonElement function) ||
            function.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        JsonProperty[] functionProperties = function.EnumerateObject().ToArray();
        return functionProperties.Length == 1 &&
            function.TryGetProperty("name", out JsonElement name) &&
            name.ValueKind == JsonValueKind.String &&
            string.Equals(name.GetString(), "submit_code_logic", StringComparison.Ordinal);
    }

    private enum DeepSeekToolChoiceShape
    {
        Ordinary,
        Terminal,
    }

    private static bool IsExactJsonObjectResponseFormat(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        JsonProperty[] properties = value.EnumerateObject().ToArray();
        return properties.Length == 1 &&
            string.Equals(properties[0].Name, "type", StringComparison.Ordinal) &&
            properties[0].Value.ValueKind == JsonValueKind.String &&
            string.Equals(
                properties[0].Value.GetString(),
                "json_object",
                StringComparison.Ordinal);
    }

    private static bool WriteMessagesWithCompleteAssistantContent(
        Utf8JsonWriter writer,
        JsonElement messages)
    {
        if (messages.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        writer.WriteStartArray();
        foreach (JsonElement message in messages.EnumerateArray())
        {
            if (message.ValueKind != JsonValueKind.Object ||
                !message.TryGetProperty("role", out JsonElement role) ||
                role.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            bool isAssistantToolCall =
                string.Equals(role.GetString(), "assistant", StringComparison.Ordinal) &&
                message.TryGetProperty("tool_calls", out _);
            if (!isAssistantToolCall)
            {
                message.WriteTo(writer);
                continue;
            }

            bool hasContent = message.TryGetProperty("content", out JsonElement content);
            if (!TryNormalizeAssistantToolContent(
                hasContent ? content : default,
                hasContent,
                out string? normalizedContent,
                out bool rewriteContent))
            {
                return false;
            }

            writer.WriteStartObject();
            foreach (JsonProperty property in message.EnumerateObject())
            {
                if (rewriteContent &&
                    string.Equals(property.Name, "content", StringComparison.Ordinal))
                {
                    continue;
                }

                property.WriteTo(writer);
            }

            if (rewriteContent)
            {
                writer.WriteString("content", normalizedContent);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        return true;
    }

    private static bool TryNormalizeAssistantToolContent(
        JsonElement content,
        bool hasContent,
        out string normalizedContent,
        out bool rewriteContent)
    {
        normalizedContent = string.Empty;
        rewriteContent = !hasContent;
        if (!hasContent || content.ValueKind == JsonValueKind.Null)
        {
            rewriteContent = true;
            return true;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            normalizedContent = content.GetString() ?? string.Empty;
            return true;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var text = new StringBuilder();
        foreach (JsonElement part in content.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.Object ||
                part.EnumerateObject().Count() != 2 ||
                !part.TryGetProperty("type", out JsonElement type) ||
                type.ValueKind != JsonValueKind.String ||
                !string.Equals(type.GetString(), "text", StringComparison.Ordinal) ||
                !part.TryGetProperty("text", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            text.Append(value.GetString());
        }

        normalizedContent = text.ToString();
        rewriteContent = true;
        return true;
    }

    private static bool IsExactDisabledThinking(JsonElement thinking)
    {
        if (thinking.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        JsonProperty[] properties = thinking.EnumerateObject().ToArray();
        return properties.Length == 1 &&
            string.Equals(properties[0].Name, "type", StringComparison.Ordinal) &&
            properties[0].Value.ValueKind == JsonValueKind.String &&
            string.Equals(properties[0].Value.GetString(), "disabled", StringComparison.Ordinal);
    }

    private static bool HasUniquePropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || !HasUniquePropertyNames(property.Value))
                {
                    return false;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (!HasUniquePropertyNames(item))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryMapSubmissionCorrection(
        object? result,
        out PreparedPass1SubmissionRejectionKind rejectionKind)
    {
        string? value = result switch
        {
            string text => text,
            JsonElement element when element.ValueKind == JsonValueKind.String =>
                element.GetString(),
            _ => null,
        };
        rejectionKind = value switch
        {
            "invalid-shape" => PreparedPass1SubmissionRejectionKind.InvalidShape,
            "invalid-binding" => PreparedPass1SubmissionRejectionKind.InvalidBinding,
            "invalid-evidence" => PreparedPass1SubmissionRejectionKind.InvalidEvidence,
            "invalid-semantics" => PreparedPass1SubmissionRejectionKind.InvalidSemantics,
            _ => default,
        };
        return value is "invalid-shape" or
            "invalid-binding" or
            "invalid-evidence" or
            "invalid-semantics";
    }

    private static PreparedPass1SubmissionRepairCode GetRepairCode(
        PreparedPass1SubmissionRejectionKind rejectionKind) =>
        rejectionKind switch
        {
            PreparedPass1SubmissionRejectionKind.InvalidShape =>
                PreparedPass1SubmissionRepairCode.UseMinimalValidDocument,
            PreparedPass1SubmissionRejectionKind.InvalidSemantics =>
                PreparedPass1SubmissionRepairCode.UseMinimalValidDocument,
            PreparedPass1SubmissionRejectionKind.InvalidBinding =>
                PreparedPass1SubmissionRepairCode.CopyPacketBinding,
            PreparedPass1SubmissionRejectionKind.InvalidEvidence =>
                PreparedPass1SubmissionRepairCode.UseAuthorizedFullRangeAnchor,
            _ => throw PreparedPass1TerminalFailure.Integrity(
                "The first rejected submission has no closed repair code."),
        };

    private static string CreateProjectionPromptResource(string systemPrompt)
    {
        const string resourceName =
            "Vulgata.Agents.Prompts.pass1-code-logic-worker.v2.md";
        System.Reflection.Assembly assembly = typeof(PreparedPass1JobExecutor).Assembly;
        string[] matches = assembly.GetManifestResourceNames()
            .Where(name => string.Equals(name, resourceName, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw PreparedPass1TerminalFailure.Prompt(
                "The terminal prompt resource is unavailable or ambiguous.");
        }

        using Stream stream = assembly.GetManifestResourceStream(matches[0])
            ?? throw PreparedPass1TerminalFailure.Prompt(
                "The terminal prompt resource is unavailable.");
        using var reader = new StreamReader(stream, _strictUtf8, true);
        string authoritativePrompt = reader.ReadToEnd();
        if (systemPrompt.Contains(
                "<!-- PASS1_TERMINAL_PREAMBLE_BEGIN -->",
                StringComparison.Ordinal) &&
            !string.Equals(systemPrompt, authoritativePrompt, StringComparison.Ordinal))
        {
            throw PreparedPass1TerminalFailure.Prompt(
                "The terminal prompt resource binding changed.");
        }

        return authoritativePrompt;
    }

    private static string CreateRequestJson(PreparedPass1WorkerPacket packet)
    {
        string source = _strictUtf8.GetString(packet.SourceUtf8.Span);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            binding = new
            {
                jobId = packet.JobId,
                scanExecutionId = packet.ScanExecutionId,
                systemId = packet.SystemId,
                repositoryId = packet.RepositoryId,
                scanRunId = packet.ScanRunId,
                profiledRevision = packet.ProfiledRevision,
                fileCodeUnitId = packet.FileCodeUnitId,
                fileCodeUnitIdentityFingerprint = packet.FileCodeUnitIdentityFingerprint,
                preparedScanManifestId = packet.PreparedScanManifestId,
                preparedScanManifestFingerprint = packet.PreparedScanManifestFingerprint,
                pairPackageFingerprint = packet.PairPackageFingerprint,
                repositoryRelativePath = packet.RepositoryRelativePath,
                byteStart = packet.ByteStart,
                byteEnd = packet.ByteEnd,
                lineStart = packet.LineStart,
                lineEnd = packet.LineEnd,
                segmentOrdinal = packet.SegmentOrdinal,
                segmentCount = packet.SegmentCount,
                fileHash = packet.FileHash,
                sourceHash = packet.SourceHash,
                promptIdentity = packet.PromptIdentity,
                documentSchemaIdentity = packet.DocumentSchemaIdentity,
                analysisRuntimeIdentityHash = packet.AnalysisRuntimeIdentityHash,
            },
            source,
            contextSnapshot = packet.ContextSnapshot,
            communicationClassifications = packet.CommunicationClassifications,
        });
        if (json.Length > WorkerAgentContractLimits.MaximumProviderRequestUtf8Bytes)
        {
            throw new InvalidDataException("Prepared Pass 1 provider request exceeds its bound.");
        }

        return _strictUtf8.GetString(json);
    }

    private static PreparedPass1RawWorkerResult ProviderConfiguration() =>
        new(
            PreparedPass1RawWorkerOutcome.ProviderUnavailable,
            ReadOnlyMemory<byte>.Empty,
            "provider-configuration");

    private static PreparedPass1RawWorkerResult IntegrityFailure() =>
        new(
            PreparedPass1RawWorkerOutcome.Rejected,
            ReadOnlyMemory<byte>.Empty,
            "integrity-failure");

    private static PreparedPass1RawWorkerResult ProviderUnavailable() =>
        new(
            PreparedPass1RawWorkerOutcome.ProviderUnavailable,
            ReadOnlyMemory<byte>.Empty,
            "provider-unavailable");

    private static PreparedPass1RawWorkerResult ProviderRequestBoundExceeded() =>
        new(
            PreparedPass1RawWorkerOutcome.Rejected,
            ReadOnlyMemory<byte>.Empty,
            "provider-request-bound-exceeded");
}
