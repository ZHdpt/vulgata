using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;

namespace Vulgata.Agents.ChatExecution;

public sealed class ChatAgentDispatcher : IChatAgentDispatcher
{
    internal static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(30);
    private const string _requestSchemaVersion = "prepared-chat-selection-request-v1";
    private readonly ChatAgentProviderResolver _providerResolver;
    private readonly ISecretEncryptionService _encryptionService;
    private readonly ChatPromptCatalog _promptCatalog;
    private readonly HttpMessageHandler _httpMessageHandler;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ChatAgentDispatcher>? _logger;

    public ChatAgentDispatcher(
        ChatAgentProviderResolver providerResolver,
        ISecretEncryptionService encryptionService,
        ChatPromptCatalog promptCatalog,
        HttpMessageHandler httpMessageHandler,
        TimeProvider timeProvider,
        ILogger<ChatAgentDispatcher>? logger = null)
    {
        _providerResolver = providerResolver;
        _encryptionService = encryptionService;
        _promptCatalog = promptCatalog;
        _httpMessageHandler = httpMessageHandler;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ChatSynthesisOutcome> GenerateAsync(
        ChatSynthesisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return ChatSynthesisOutcome.Failed(ChatContractFailureCode.Cancelled);
        }

        try
        {
            ResolvedChatAgentProvider provider = await _providerResolver.ResolveAsync(
                request.SystemId,
                cancellationToken);
            string prompt = _promptCatalog.GetPrompt(request.Mode);
            string requestJson = JsonSerializer.Serialize(new
            {
                schemaVersion = _requestSchemaVersion,
                requestId = request.RequestId,
                mode = ToProviderMode(request.Mode),
                question = request.Question,
                evidenceFingerprint = request.EvidenceFingerprint,
                candidates = request.CandidateClaims.Select(candidate => new
                {
                    candidateHandle = candidate.CandidateHandle,
                    text = candidate.Text,
                    narrativeAllowed = candidate.AllowsBusinessNarrative,
                }),
            });
            string apiKey = await _encryptionService.DecryptAsync(
                SecretPurposes.LlmApiKey,
                provider.EncryptedApiKey,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return ChatSynthesisOutcome.Failed(ChatContractFailureCode.ProviderFailure);
            }

            using var httpClient = new HttpClient(_httpMessageHandler, disposeHandler: false);
            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = provider.Endpoint,
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
                instructions: prompt,
                name: "prepared-chat-selection");
            // DeepSeek and other OpenAI-compatible endpoints reject the
            // json_schema response format; ChatAgentResultValidator enforces
            // the selection contract after the call.
            Microsoft.Extensions.AI.ChatResponseFormat responseFormat =
                Microsoft.Extensions.AI.ChatResponseFormat.Json;
            var runOptions = new ChatClientAgentRunOptions(
                new ChatOptions { ResponseFormat = responseFormat });
            using var timeout = new CancellationTokenSource(ProviderTimeout, _timeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);
            AgentResponse response;
            try
            {
                response = await agent.RunAsync(
                    requestJson,
                    session: null,
                    options: runOptions,
                    linked.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return ChatSynthesisOutcome.Failed(ChatContractFailureCode.Cancelled);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                return ChatSynthesisOutcome.Failed(ChatContractFailureCode.TimedOut);
            }
            catch (Exception)
            {
                return ChatSynthesisOutcome.Failed(ChatContractFailureCode.ProviderFailure);
            }

            return ParseResult(response.Text);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ChatSynthesisOutcome.Failed(ChatContractFailureCode.Cancelled);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException or JsonException)
        {
            return ChatSynthesisOutcome.Failed(ChatContractFailureCode.ProviderFailure);
        }
    }

    private ChatSynthesisOutcome ParseResult(string responseText)
    {
        try
        {
            string responseJson = UnwrapJsonCodeFence(responseText);
            using JsonDocument document = JsonDocument.Parse(responseJson);
            JsonElement root = document.RootElement;
            string requestId = root.GetProperty("requestId").GetString() ?? string.Empty;
            ChatMode mode = root.GetProperty("mode").GetString() switch
            {
                "business" => ChatMode.Business,
                "it" => ChatMode.It,
                _ => throw new JsonException("Unsupported Chat mode."),
            };
            string fingerprint = root.GetProperty("evidenceFingerprint").GetString() ?? string.Empty;
            ChatAnswerability answerability = root.GetProperty("answerability").GetString() switch
            {
                "supported" => ChatAnswerability.Supported,
                "unsupported" => ChatAnswerability.Unsupported,
                _ => throw new JsonException("Unsupported answerability value."),
            };
            string? schemaVersion = root.GetProperty("schemaVersion").GetString();
            if (!StringComparer.Ordinal.Equals(
                    schemaVersion,
                    ChatSynthesisResult.SupportedSchemaVersion) &&
                !StringComparer.Ordinal.Equals(
                    schemaVersion,
                    ChatSynthesisResult.CompatibleProviderEchoSchemaVersion))
            {
                throw new JsonException("Unsupported Chat result schema.");
            }

            ChatSynthesisSelection[] selections = root.GetProperty("selections")
                .EnumerateArray()
                .Select(selection => new ChatSynthesisSelection(
                    selection.GetProperty("candidateHandle").GetString() ?? string.Empty,
                    selection.GetProperty("order").GetInt32(),
                    selection.TryGetProperty("narrativeText", out JsonElement narrative) &&
                        narrative.ValueKind == JsonValueKind.String
                            ? narrative.GetString()
                            : null))
                .ToArray();
            return ChatSynthesisOutcome.Generated(ChatSynthesisResult.Create(
                requestId,
                mode,
                fingerprint,
                answerability,
                selections), responseJson);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ChatContractException)
        {
            _logger?.LogWarning(
                "Prepared Chat provider result parsing failed with {ErrorType}: {ErrorMessage}",
                exception.GetType().Name,
                exception.Message);
            return ChatSynthesisOutcome.Failed(ChatContractFailureCode.InvalidSelection);
        }
    }

    private static string UnwrapJsonCodeFence(string responseText)
    {
        string trimmed = responseText.Trim();
        const string jsonFence = "```json";
        const string plainFence = "```";
        string openingFence = trimmed.StartsWith(jsonFence, StringComparison.OrdinalIgnoreCase)
            ? trimmed[..jsonFence.Length]
            : trimmed.StartsWith(plainFence, StringComparison.Ordinal)
                ? plainFence
                : string.Empty;
        if (openingFence.Length == 0 ||
            !trimmed.EndsWith(plainFence, StringComparison.Ordinal))
        {
            return responseText;
        }

        return trimmed[openingFence.Length..^plainFence.Length].Trim();
    }

    private static string ToProviderMode(ChatMode mode) => mode switch
    {
        ChatMode.Business => "business",
        ChatMode.It => "it",
        _ => throw new InvalidOperationException("The Chat Agent mode is not supported."),
    };
}
