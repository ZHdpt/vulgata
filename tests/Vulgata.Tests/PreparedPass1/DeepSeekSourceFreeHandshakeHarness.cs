using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.PreparedPass1;

internal enum DeepSeekSourceFreeHandshakeOutcome
{
    Available,
    ProviderConfiguration,
    ProviderUnavailable,
}

internal sealed record DeepSeekSourceFreeHandshakeEvidence(
    DeepSeekSourceFreeHandshakeOutcome Outcome,
    Uri Endpoint,
    string Model,
    string ProjectionAlgorithmIdentity,
    string RawNavigateCodeSchemaSha256,
    int RawNavigateCodeSchemaBytes,
    string RawSubmitCodeSchemaSha256,
    int RawSubmitCodeSchemaBytes,
    string RawDeclarationsSha256,
    int RawDeclarationsBytes,
    string LedgerIdentity,
    int LedgerRowCount,
    string ProjectedNavigateCodeSchemaSha256,
    int ProjectedNavigateCodeSchemaBytes,
    string ProjectedSubmitCodeSchemaSha256,
    int ProjectedSubmitCodeSchemaBytes,
    string ProjectedDeclarationsSha256,
    int ProjectedDeclarationsBytes,
    string TerminalDeclarationsSha256,
    int TerminalDeclarationsBytes,
    string AuthoritativeSchemaSha256,
    int AuthoritativeSchemaBytes,
    string ProjectedSchemaSha256,
    int ProjectedSchemaBytes,
    string NavigateCodeSchemaSha256,
    int NavigateCodeSchemaBytes,
    string DeclarationsSha256,
    int DeclarationsBytes,
    string RequestSha256,
    int RequestBytes,
    string OrdinaryRequestSha256,
    int OrdinaryRequestBytes,
    string TerminalRequestSha256,
    int TerminalRequestBytes,
    bool Streaming,
    bool Strict,
    bool ThinkingDisabled,
    bool ToolChoiceRequired,
    bool ToolCallObserved,
    bool DoneObserved,
    bool OrdinaryStreaming,
    bool OrdinaryStrict,
    bool OrdinaryThinkingDisabled,
    bool OrdinaryToolChoiceRequired,
    bool OrdinaryToolCallObserved,
    bool OrdinaryDoneObserved,
    bool TerminalStreaming,
    bool TerminalStrict,
    bool TerminalThinkingDisabled,
    bool TerminalToolChoiceRequired,
    bool TerminalToolCallObserved,
    bool TerminalDoneObserved,
    bool TerminalSelectedFunctionNameMatched,
    int TerminalMessageCount,
    bool TerminalResultPresent,
    bool TerminalHistoricalProtocolAbsent,
    string TerminalPromptSha256,
    int TerminalPromptBytes,
    string TerminalContextSha256,
    int TerminalContextBytes,
    DateTimeOffset ObservedAt);

internal sealed class DeepSeekSourceFreeHandshakeHarness(
    HttpMessageHandler transport,
    Uri endpoint,
    string model,
    string apiKey)
{
    private const string RequiredEndpoint = "https://api.deepseek.com/beta";
    private const string RequiredModel = "deepseek-v4-flash";
    private const int MaximumAuthoritativeSchemaBytes = 65_536;
    private const int MaximumProjectedSchemaBytes = 65_536;
    private const int MaximumDeclarationsBytes = 81_920;
    private const int MaximumRequestBytes = 1_818_624;
    private const int MaximumRawResponseBytes = 262_144;
    private const string FixedSourceFreePacket =
        "{\"sourceFreeSyntheticPacket\":\"fixed\",\"binding\":\"source-free\"}";

    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    internal async Task<DeepSeekSourceFreeHandshakeEvidence> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsConfigured())
        {
            return EmptyEvidence(DeepSeekSourceFreeHandshakeOutcome.ProviderConfiguration);
        }

        byte[] authoritativeSchema = LoadAuthoritativeSchema();
        try
        {
            DeepSeekStrictSchemaProjection projection = DeepSeekStrictSchemaProjector.Build();
            byte[] projectedSchema = _strictUtf8.GetBytes(projection.ProjectedSchemaJson);
            AIFunction navigateCode = CreateNavigateCodeFunction();
            AIFunction submitCodeLogic = CreateSubmitCodeLogicFunction();
            DeepSeekStrictToolDeclarations declarationAuthority =
                DeepSeekStrictToolDeclarations.Create(navigateCode, submitCodeLogic);
            byte[] declarations = declarationAuthority.Utf8;
            byte[] terminalDeclarations = declarationAuthority.ProjectedSubmitOnlyUtf8;
            PreparedPass1TerminalContextProjectionResult terminalProjection =
                CreateTerminalProjection();
            byte[] ordinaryRequest = CreateRequest(declarations, terminal: false);
            byte[] terminalRequest = CreateRequest(
                terminalDeclarations,
                terminal: true,
                terminalProjection);
            if (!HasCompleteSanitizedDeclarationEvidence(declarationAuthority))
            {
                return EmptyEvidence(DeepSeekSourceFreeHandshakeOutcome.ProviderConfiguration);
            }
            DeepSeekSourceFreeHandshakeEvidence evidence = CreateEvidence(
                DeepSeekSourceFreeHandshakeOutcome.ProviderUnavailable,
                authoritativeSchema,
                projectedSchema,
                declarationAuthority,
                ordinaryRequest,
                terminalRequest,
                terminalProjection);

            if (authoritativeSchema.Length > MaximumAuthoritativeSchemaBytes ||
                projectedSchema.Length > MaximumProjectedSchemaBytes ||
                declarations.Length > MaximumDeclarationsBytes ||
                terminalDeclarations.Length > MaximumDeclarationsBytes ||
                ordinaryRequest.Length > MaximumRequestBytes ||
                terminalRequest.Length > MaximumRequestBytes)
            {
                return evidence with
                {
                    Outcome = DeepSeekSourceFreeHandshakeOutcome.ProviderConfiguration,
                    ObservedAt = DateTimeOffset.UtcNow,
                };
            }

            using var client = new HttpClient(transport, disposeHandler: false)
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
            SourceFreeHandshakeAttempt ordinary = await SendRequestAsync(
                client,
                ordinaryRequest,
                requireSubmitFunction: false,
                cancellationToken);
            evidence = evidence with
            {
                Outcome = ordinary.Outcome,
                ToolCallObserved = ordinary.Observation.ToolCallObserved,
                DoneObserved = ordinary.Observation.DoneObserved,
                OrdinaryToolCallObserved = ordinary.Observation.ToolCallObserved,
                OrdinaryDoneObserved = ordinary.Observation.DoneObserved,
                ObservedAt = DateTimeOffset.UtcNow,
            };
            if (ordinary.Outcome != DeepSeekSourceFreeHandshakeOutcome.Available)
            {
                return evidence;
            }

            SourceFreeHandshakeAttempt terminal = await SendRequestAsync(
                client,
                terminalRequest,
                requireSubmitFunction: true,
                cancellationToken);
            bool terminalAccepted =
                terminal.Outcome == DeepSeekSourceFreeHandshakeOutcome.Available &&
                terminal.Observation.SelectedFunctionNameMatched;
            return evidence with
            {
                Outcome = terminalAccepted
                    ? DeepSeekSourceFreeHandshakeOutcome.Available
                    : terminal.Outcome == DeepSeekSourceFreeHandshakeOutcome.Available
                        ? DeepSeekSourceFreeHandshakeOutcome.ProviderConfiguration
                        : terminal.Outcome,
                TerminalToolCallObserved = terminal.Observation.ToolCallObserved,
                TerminalDoneObserved = terminal.Observation.DoneObserved,
                TerminalSelectedFunctionNameMatched =
                    terminal.Observation.SelectedFunctionNameMatched,
                ObservedAt = DateTimeOffset.UtcNow,
            };
        }
        catch (Exception exception) when (
            exception is InvalidDataException or JsonException or EncoderFallbackException)
        {
            return EmptyEvidence(DeepSeekSourceFreeHandshakeOutcome.ProviderConfiguration);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(authoritativeSchema);
        }
    }

    private bool IsConfigured() =>
        endpoint.IsAbsoluteUri &&
        string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) &&
        string.Equals(endpoint.AbsoluteUri.TrimEnd('/'), RequiredEndpoint, StringComparison.Ordinal) &&
        string.Equals(model, RequiredModel, StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(apiKey);

    private static byte[] LoadAuthoritativeSchema()
    {
        Assembly assembly = typeof(DeepSeekStrictSchemaProjector).Assembly;
        string resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(
                "code-logic-file-v2.schema.json",
                StringComparison.Ordinal));
        using Stream stream = assembly.GetManifestResourceStream(resource) ??
            throw new InvalidDataException("The authoritative schema is unavailable.");
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static AIFunction CreateNavigateCodeFunction() =>
        new PreparedNavigateCodeFunction(new UnavailableNavigationService())
            .Create(new PreparedNavigateCodeBinding(
                Guid.Parse("11111111-1111-4111-8111-111111111111"),
                1,
                Guid.Parse("22222222-2222-4222-8222-222222222222")));

    private static AIFunction CreateSubmitCodeLogicFunction()
    {
        PreparedPass1WorkerPacket neutralPacket = CreateNeutralQualificationPacket();
        return new PreparedSubmitCodeLogicFunction()
            .CreateSession(neutralPacket)
            .Function;
    }

    private static PreparedPass1TerminalContextProjectionResult CreateTerminalProjection()
    {
        const string promptResourceName =
            "Vulgata.Agents.Prompts.pass1-code-logic-worker.v2.md";
        Assembly assembly = typeof(PreparedPass1TerminalContextProjection).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(promptResourceName)
            ?? throw new InvalidDataException(
                "The source-free terminal prompt resource is unavailable.");
        using var reader = new StreamReader(
            stream,
            _strictUtf8,
            detectEncodingFromByteOrderMarks: false);
        string promptResource = reader.ReadToEnd();
        PreparedPass1NavigationResponse fixedResult = CreateFixedNavigationResult();

        // This source-free protocol fixture represents the history deliberately
        // excluded by the production terminal projection.
        Microsoft.Extensions.AI.ChatMessage[] syntheticHistory =
        [
            new(ChatRole.Assistant, "call-history navigate_code fixed-arguments"),
            new(ChatRole.Tool, PreparedPass1TerminalContextProjection.SerializeInvokedResult(fixedResult)),
        ];
        if (syntheticHistory.Length != 2)
        {
            throw new InvalidDataException("The source-free history fixture is incomplete.");
        }

        return PreparedPass1TerminalContextProjection.Create(
            new(
                promptResource,
                "source-free-history-bearing-prompt/v1",
                _strictUtf8.GetBytes(FixedSourceFreePacket),
                10,
                [PreparedPass1TerminalContextResult.FromNavigationResponse(1, fixedResult)],
                new PreparedPass1TerminalContextCorrection(
                    1,
                    PreparedPass1SubmissionRejectionKind.InvalidSemantics,
                    PreparedPass1SubmissionRepairCode.UseMinimalValidDocument)));
    }

    private static PreparedPass1NavigationResponse CreateFixedNavigationResult() =>
        new(
            PreparedPass1NavigationOutcome.DocumentEvidence,
            Guid.Parse("10000000-0000-4000-8000-000000000001"),
            PreparedPass1NavigationOperation.Definition,
            1,
            PreparedPass1NavigationBoundKind.Calls,
            1,
            32,
            [
                new PreparedPass1NavigationResult(
                    "src/OrderService.cs",
                    Guid.Parse("20000000-0000-4000-8000-000000000002"),
                    new byte[32],
                    [Guid.Parse("30000000-0000-4000-8000-000000000003")],
                    [Guid.Parse("40000000-0000-4000-8000-000000000004")],
                    "Process",
                    new PreparedPass1LineRange(10, 12),
                    new PreparedPass1ByteRange(100, 140),
                    "src/OrderService.cs:10-12"),
            ]);

    private static PreparedPass1WorkerPacket CreateNeutralQualificationPacket()
    {
        const string revision = "0123456789abcdef0123456789abcdef01234567";
        const string hash =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        Guid repositoryId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        Guid scanRunId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        var context = new PreparedPass1ContextSnapshotProjection(
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            repositoryId,
            scanRunId,
            revision,
            hash,
            hash,
            null,
            null,
            null);
        return new PreparedPass1WorkerPacket(
            Guid.Parse("66666666-6666-4666-8666-666666666666"),
            Guid.Parse("77777777-7777-4777-8777-777777777777"),
            Guid.Parse("88888888-8888-4888-8888-888888888888"),
            Guid.Parse("99999999-9999-4999-8999-999999999999"),
            repositoryId,
            scanRunId,
            revision,
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            hash,
            Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            hash,
            hash,
            "qualification/neutral.java",
            0,
            0,
            1,
            1,
            1,
            1,
            hash,
            hash,
            "story-12-3-worker-v2",
            "code-logic/file-v2",
            hash,
            1,
            1,
            ReadOnlyMemory<byte>.Empty,
            context,
            []);
    }

    private async Task<SourceFreeHandshakeAttempt> SendRequestAsync(
        HttpClient client,
        byte[] request,
        bool requireSubmitFunction,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/chat/completions"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Content = new ByteArrayContent(request);
        message.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/json");

        try
        {
            using HttpResponseMessage response = await client.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                SourceFreeResponseObservation observation =
                    await ObserveBoundedResponseAsync(
                        response.Content,
                        requireSubmitFunction,
                        cancellationToken);
                return new(
                    observation.ToolCallObserved && observation.DoneObserved
                        ? DeepSeekSourceFreeHandshakeOutcome.Available
                        : DeepSeekSourceFreeHandshakeOutcome.ProviderConfiguration,
                    observation);
            }

            DeepSeekSourceFreeHandshakeOutcome outcome = response.StatusCode is
                HttpStatusCode.BadRequest or
                HttpStatusCode.Unauthorized or
                HttpStatusCode.PaymentRequired or
                HttpStatusCode.Forbidden or
                HttpStatusCode.UnprocessableEntity
                    ? DeepSeekSourceFreeHandshakeOutcome.ProviderConfiguration
                    : DeepSeekSourceFreeHandshakeOutcome.ProviderUnavailable;
            return new(outcome, new(false, false, false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or OperationCanceledException)
        {
            return new(
                DeepSeekSourceFreeHandshakeOutcome.ProviderUnavailable,
                new(false, false, false));
        }
    }

    private byte[] CreateRequest(
        byte[] declarations,
        bool terminal,
        PreparedPass1TerminalContextProjectionResult? terminalProjection = null)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("model", model);
            writer.WriteStartObject("thinking");
            writer.WriteString("type", "disabled");
            writer.WriteEndObject();
            writer.WriteNumber("max_tokens", 16);
            writer.WriteStartArray("messages");
            if (!terminal)
            {
                writer.WriteStartObject();
                writer.WriteString("role", "user");
                writer.WriteString(
                    "content",
                    "Call exactly one declared function with empty compatibility arguments.");
                writer.WriteEndObject();
            }
            else
            {
                if (terminalProjection is null || terminalProjection.Messages.Count != 3)
                {
                    throw new InvalidDataException(
                        "The source-free terminal projection is incomplete.");
                }

                foreach (Microsoft.Extensions.AI.ChatMessage projected in terminalProjection.Messages)
                {
                    writer.WriteStartObject();
                    writer.WriteString(
                        "role",
                        projected.Role == ChatRole.System ? "system" : "user");
                    writer.WriteString("content", projected.Text);
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndArray();
            writer.WritePropertyName("tools");
            writer.WriteRawValue(declarations, skipInputValidation: false);
            if (!terminal)
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
            writer.WriteBoolean("stream", true);
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private static async Task<SourceFreeResponseObservation> ObserveBoundedResponseAsync(
        HttpContent? content,
        bool requireSubmitFunction,
        CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return new(false, false, false);
        }

        await using Stream source = await content.ReadAsStreamAsync(cancellationToken);
        await using var bounded = new SourceFreeBoundedResponseStream(source);
        using var reader = new StreamReader(
            bounded,
            _strictUtf8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4_096,
            leaveOpen: false);
        bool toolCallObserved = false;
        bool doneObserved = false;
        var submitNameMatcher = new SubmitFunctionNameMatcher();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            string payload = line[5..].TrimStart();
            if (string.Equals(payload, "[DONE]", StringComparison.Ordinal))
            {
                doneObserved = true;
            }
            else if (payload.Length > 0)
            {
                using JsonDocument document = JsonDocument.Parse(payload);
                toolCallObserved |= ObserveFunctionCalls(
                    document.RootElement,
                    requireSubmitFunction ? submitNameMatcher : null);
            }

            if (toolCallObserved && doneObserved)
            {
                return new(
                    true,
                    true,
                    !requireSubmitFunction || submitNameMatcher.Matched);
            }
        }

        return new(
            toolCallObserved,
            doneObserved,
            !requireSubmitFunction || submitNameMatcher.Matched);
    }

    private static bool ObserveFunctionCalls(
        JsonElement root,
        SubmitFunctionNameMatcher? submitNameMatcher)
    {
        bool observed = false;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("choices", out JsonElement choices) ||
            choices.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement choice in choices.EnumerateArray())
        {
            if (choice.ValueKind != JsonValueKind.Object ||
                !choice.TryGetProperty("delta", out JsonElement delta) ||
                delta.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (delta.TryGetProperty("tool_calls", out JsonElement toolCalls) &&
                toolCalls.ValueKind == JsonValueKind.Array)
            {
                observed |= toolCalls.GetArrayLength() > 0;
                foreach (JsonElement toolCall in toolCalls.EnumerateArray())
                {
                    ObserveFunctionName(toolCall, submitNameMatcher);
                }
            }

            if (delta.TryGetProperty("function_call", out JsonElement functionCall) &&
                functionCall.ValueKind == JsonValueKind.Object)
            {
                observed = true;
                ObserveFunctionName(functionCall, submitNameMatcher);
            }
        }

        return observed;
    }

    private static void ObserveFunctionName(
        JsonElement call,
        SubmitFunctionNameMatcher? submitNameMatcher)
    {
        if (submitNameMatcher is null)
        {
            return;
        }

        JsonElement function = call;
        if (call.TryGetProperty("function", out JsonElement nestedFunction) &&
            nestedFunction.ValueKind == JsonValueKind.Object)
        {
            function = nestedFunction;
        }

        if (function.TryGetProperty("name", out JsonElement name) &&
            name.ValueKind == JsonValueKind.String)
        {
            submitNameMatcher.Observe(name.GetString()!);
        }
    }

    private DeepSeekSourceFreeHandshakeEvidence CreateEvidence(
        DeepSeekSourceFreeHandshakeOutcome outcome,
        byte[] authoritativeSchema,
        byte[] projectedSchema,
        DeepSeekStrictToolDeclarations declarationAuthority,
        byte[] ordinaryRequest,
        byte[] terminalRequest,
        PreparedPass1TerminalContextProjectionResult terminalProjection) =>
        new(
            outcome,
            endpoint,
            model,
            declarationAuthority.ProjectionAlgorithmIdentity,
            declarationAuthority.RawNavigateCodeParametersSha256,
            declarationAuthority.RawNavigateCodeParametersUtf8.Length,
            declarationAuthority.RawSubmitCodeLogicParametersSha256,
            declarationAuthority.RawSubmitCodeLogicParametersUtf8.Length,
            declarationAuthority.RawSha256,
            declarationAuthority.RawUtf8.Length,
            declarationAuthority.LedgerIdentity,
            declarationAuthority.TransformLedgerRowCount,
            declarationAuthority.ProjectedNavigateCodeParametersSha256,
            declarationAuthority.ProjectedNavigateCodeParametersUtf8.Length,
            declarationAuthority.ProjectedSubmitCodeLogicParametersSha256,
            declarationAuthority.ProjectedSubmitCodeLogicParametersUtf8.Length,
            declarationAuthority.ProjectedSha256,
            declarationAuthority.ProjectedUtf8.Length,
            declarationAuthority.ProjectedSubmitOnlySha256,
            declarationAuthority.ProjectedSubmitOnlyUtf8.Length,
            Sha256(authoritativeSchema),
            authoritativeSchema.Length,
            Sha256(projectedSchema),
            projectedSchema.Length,
            declarationAuthority.ProjectedNavigateCodeParametersSha256,
            declarationAuthority.ProjectedNavigateCodeParametersUtf8.Length,
            declarationAuthority.ProjectedSha256,
            declarationAuthority.ProjectedUtf8.Length,
            Sha256(ordinaryRequest),
            ordinaryRequest.Length,
            Sha256(ordinaryRequest),
            ordinaryRequest.Length,
            Sha256(terminalRequest),
            terminalRequest.Length,
            Streaming: true,
            Strict: true,
            ThinkingDisabled: true,
            ToolChoiceRequired: true,
            ToolCallObserved: false,
            DoneObserved: false,
            OrdinaryStreaming: true,
            OrdinaryStrict: true,
            OrdinaryThinkingDisabled: true,
            OrdinaryToolChoiceRequired: true,
            OrdinaryToolCallObserved: false,
            OrdinaryDoneObserved: false,
            TerminalStreaming: true,
            TerminalStrict: true,
            TerminalThinkingDisabled: true,
            TerminalToolChoiceRequired: true,
            TerminalToolCallObserved: false,
            TerminalDoneObserved: false,
            TerminalSelectedFunctionNameMatched: false,
            TerminalMessageCount: terminalProjection.Messages.Count,
            TerminalResultPresent: HasTerminalResult(terminalProjection.ContextJson),
            TerminalHistoricalProtocolAbsent:
                HasNoHistoricalProtocol(terminalProjection.Messages),
            TerminalPromptSha256: Sha256(_strictUtf8.GetBytes(
                terminalProjection.TerminalInstruction)),
            TerminalPromptBytes: _strictUtf8.GetByteCount(
                terminalProjection.TerminalInstruction),
            TerminalContextSha256: Sha256(_strictUtf8.GetBytes(
                terminalProjection.ContextJson)),
            TerminalContextBytes: _strictUtf8.GetByteCount(
                terminalProjection.ContextJson),
            DateTimeOffset.UtcNow);

    private DeepSeekSourceFreeHandshakeEvidence EmptyEvidence(
        DeepSeekSourceFreeHandshakeOutcome outcome) =>
        new(
            outcome,
            endpoint,
            model,
            string.Empty,
            string.Empty,
            0,
            string.Empty,
            0,
            string.Empty,
            0,
            string.Empty,
            0,
            string.Empty,
            0,
            string.Empty,
            0,
            string.Empty,
            0,
            string.Empty,
            0,
            string.Empty,
            0,
            string.Empty,
            0,
            string.Empty,
            0,
            string.Empty,
            0,
            string.Empty,
            0,
            string.Empty,
            0,
            string.Empty,
            0,
            Streaming: true,
            Strict: true,
            ThinkingDisabled: false,
            ToolChoiceRequired: false,
            ToolCallObserved: false,
            DoneObserved: false,
            OrdinaryStreaming: true,
            OrdinaryStrict: true,
            OrdinaryThinkingDisabled: false,
            OrdinaryToolChoiceRequired: false,
            OrdinaryToolCallObserved: false,
            OrdinaryDoneObserved: false,
            TerminalStreaming: true,
            TerminalStrict: true,
            TerminalThinkingDisabled: false,
            TerminalToolChoiceRequired: false,
            TerminalToolCallObserved: false,
            TerminalDoneObserved: false,
            TerminalSelectedFunctionNameMatched: false,
            TerminalMessageCount: 0,
            TerminalResultPresent: false,
            TerminalHistoricalProtocolAbsent: false,
            TerminalPromptSha256: string.Empty,
            TerminalPromptBytes: 0,
            TerminalContextSha256: string.Empty,
            TerminalContextBytes: 0,
            DateTimeOffset.UtcNow);

    private static bool HasTerminalResult(string contextJson)
    {
        using JsonDocument context = JsonDocument.Parse(contextJson);
        return context.RootElement.TryGetProperty(
                "evidenceResults",
                out JsonElement results) &&
            results.ValueKind == JsonValueKind.Array &&
            results.GetArrayLength() == 1 &&
            results[0].TryGetProperty("result", out JsonElement result) &&
            result.ValueKind == JsonValueKind.Object;
    }

    private static bool HasNoHistoricalProtocol(
        IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        string projected = string.Join('\n', messages.Select(message => message.Text));
        return new[]
        {
            "assistant", "tool_calls", "tool_call_id", "function_call",
            "navigate_code", "call-history", "fixed-arguments",
        }.All(token => !projected.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record SourceFreeHandshakeAttempt(
        DeepSeekSourceFreeHandshakeOutcome Outcome,
        SourceFreeResponseObservation Observation);

    private sealed record SourceFreeResponseObservation(
        bool ToolCallObserved,
        bool DoneObserved,
        bool SelectedFunctionNameMatched);

    private sealed class SubmitFunctionNameMatcher
    {
        private const string Expected = "submit_code_logic";
        private int _matchedLength;
        private bool _mismatch;

        internal bool Matched => !_mismatch && _matchedLength == Expected.Length;

        internal void Observe(string fragment)
        {
            foreach (char character in fragment)
            {
                if (_mismatch ||
                    _matchedLength >= Expected.Length ||
                    character != Expected[_matchedLength])
                {
                    _mismatch = true;
                    return;
                }

                _matchedLength++;
            }
        }
    }

    private sealed class SourceFreeBoundedResponseStream(Stream inner) : Stream
    {
        private int _totalBytes;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _totalBytes;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int read = await inner.ReadAsync(buffer, cancellationToken);
            _totalBytes = checked(_totalBytes + read);
            if (_totalBytes > MaximumRawResponseBytes)
            {
                throw new IOException("The source-free response exceeded its bound.");
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

    private static bool HasCompleteSanitizedDeclarationEvidence(
        DeepSeekStrictToolDeclarations authority) =>
        authority.RawNavigateCodeParametersUtf8.Length is > 0 and <= MaximumDeclarationsBytes &&
        authority.RawSubmitCodeLogicParametersUtf8.Length is > 0 and <= MaximumDeclarationsBytes &&
        authority.RawUtf8.Length is > 0 and <= MaximumDeclarationsBytes &&
        authority.ProjectedNavigateCodeParametersUtf8.Length is > 0 and <= MaximumDeclarationsBytes &&
        authority.ProjectedSubmitCodeLogicParametersUtf8.Length is > 0 and <= MaximumProjectedSchemaBytes &&
        authority.ProjectedUtf8.Length is > 0 and <= MaximumDeclarationsBytes &&
        authority.ProjectedSubmitOnlyUtf8.Length is > 0 and <= MaximumDeclarationsBytes &&
        authority.TransformLedgerRowCount == 18 &&
        IsSha256(authority.RawNavigateCodeParametersSha256) &&
        IsSha256(authority.RawSubmitCodeLogicParametersSha256) &&
        IsSha256(authority.RawSha256) &&
        IsSha256(authority.LedgerIdentity) &&
        IsSha256(authority.ProjectedNavigateCodeParametersSha256) &&
        IsSha256(authority.ProjectedSubmitCodeLogicParametersSha256) &&
        IsSha256(authority.ProjectedSha256) &&
        IsSha256(authority.ProjectedSubmitOnlySha256);

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static string Sha256(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value));

    private sealed class UnavailableNavigationService : IPass1NavigationService
    {
        public Task<PreparedPass1NavigationResponse> NavigateAsync(
            PreparedPass1NavigationRequestInput request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
