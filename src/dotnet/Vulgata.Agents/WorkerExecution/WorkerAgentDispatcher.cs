using System.ClientModel;
using System.ClientModel.Primitives;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;

namespace Vulgata.Agents.WorkerExecution;

public interface IWorkerAgentExecutionContextSource
{
    Task<WorkerAgentExecutionContext> GetAsync(
        WorkerAgentJobPacket packet,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default);
}

public sealed class WorkerAgentDispatcher : IWorkerAgentDispatcher
{
    private const string _pass1PromptResourceName =
        "Vulgata.Agents.Prompts.pass1-code-logic-worker.v1.md";
    private const string _pass2PromptResourceName =
        "Vulgata.Agents.Prompts.pass2-business-flow-worker.v1.md";
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private readonly WorkerAgentProviderResolver _providerResolver;
    private readonly ISecretEncryptionService _encryptionService;
    private readonly IWorkerAgentExecutionContextSource? _executionContextSource;
    private readonly HttpMessageHandler _httpMessageHandler;
    private readonly TimeProvider _timeProvider;

    public WorkerAgentDispatcher(
        WorkerAgentProviderResolver providerResolver,
        ISecretEncryptionService encryptionService,
        HttpMessageHandler httpMessageHandler,
        TimeProvider timeProvider)
    {
        _providerResolver = providerResolver;
        _encryptionService = encryptionService;
        _httpMessageHandler = httpMessageHandler;
        _timeProvider = timeProvider;
    }

    public WorkerAgentDispatcher(
        WorkerAgentProviderResolver providerResolver,
        ISecretEncryptionService encryptionService,
        IWorkerAgentExecutionContextSource executionContextSource,
        HttpMessageHandler httpMessageHandler,
        TimeProvider timeProvider)
        : this(providerResolver, encryptionService, httpMessageHandler, timeProvider)
    {
        _executionContextSource = executionContextSource;
    }

    public async Task<WorkerAgentResultEnvelope> DispatchAsync(
        WorkerAgentJobPacket packet,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packet);
        await Task.CompletedTask;
        throw new InvalidOperationException(
            "A Repository access scope is required for Worker Agent dispatch.");
    }

    public async Task<WorkerAgentResultEnvelope> DispatchAsync(
        WorkerAgentJobPacket packet,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(scope);
        if (!scope.Contains(packet.RepositoryId))
        {
            throw new UnauthorizedAccessException(
                "The Worker Agent Repository is outside the authorized scope.");
        }

        if (packet.Payload is Pass2BusinessLogicJobPayload)
        {
            return await DispatchPass2Async(packet, cancellationToken);
        }

        IWorkerAgentExecutionContextSource source = _executionContextSource ??
            throw new InvalidOperationException(
                "A revision-bound Worker Agent execution context source is required.");
        WorkerAgentExecutionContext context = await source.GetAsync(
            packet,
            scope,
            cancellationToken);
        if (!ReferenceEquals(context.Packet, packet))
        {
            throw new InvalidOperationException(
                "The hydrated Worker Agent context does not match the dispatched packet.");
        }

        return await DispatchAsync(context, cancellationToken);
    }

    public async Task<WorkerAgentResultEnvelope> DispatchAsync(
        WorkerAgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        WorkerAgentJobPacket packet = context.Packet;
        if (packet.Kind != WorkerAgentJobKind.Pass1CodeLogicDocument ||
            packet.Payload is not Pass1CodeLogicJobPayload)
        {
            throw new InvalidOperationException("The Worker Agent job variant is not supported.");
        }

        ResolvedWorkerAgentProvider provider =
            await _providerResolver.ResolveAsync(packet.SystemId, cancellationToken);
        string prompt = LoadPass1Prompt(typeof(WorkerAgentDispatcher).Assembly);
        string requestJson = JsonSerializer.Serialize(
            WorkerAgentPass1RequestDto.From(context),
            WorkerAgentJsonContext.Default.WorkerAgentPass1RequestDto);
        if (_strictUtf8.GetByteCount(requestJson) >
            WorkerAgentContractLimits.MaximumProviderRequestUtf8Bytes)
        {
            throw new InvalidDataException("Worker Agent request exceeds its finite byte bound.");
        }

        using JsonDocument schema = JsonDocument.Parse(
            WorkerAgentJsonContext.Pass1ResponseSchemaJson);
        Microsoft.Extensions.AI.ChatResponseFormat responseFormat =
            Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema(
            schema.RootElement,
            "pass1_code_logic_document",
            "Closed Pass 1 Code Logic Document result.");
        DateTimeOffset dispatchedAt = _timeProvider.GetUtcNow();

        // The plaintext is obtained only after all pre-call validation and is
        // consumed immediately by the one configured client construction.
        string apiKey = await _encryptionService.DecryptAsync(
            SecretPurposes.LlmApiKey,
            provider.EncryptedApiKey,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("The Worker Agent credential is unavailable.");
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
            name: "pass1-code-logic-worker");
        var runOptions = new ChatClientAgentRunOptions(
            new ChatOptions { ResponseFormat = responseFormat });
        AgentResponse response;
        try
        {
            response = await agent.RunAsync(
                requestJson,
                session: null,
                options: runOptions,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.Error,
                "Worker Agent 调用失败。",
                dispatchedAt,
                _timeProvider.GetUtcNow());
        }

        DateTimeOffset receivedAt = _timeProvider.GetUtcNow();

        try
        {
            if (response.Text.Length >
                    WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes ||
                _strictUtf8.GetByteCount(response.Text) >
                    WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes)
            {
                return WorkerAgentResultEnvelope.CreateForJob(
                    packet,
                    WorkerAgentResultOutcome.Unbounded,
                    "Worker Agent 返回结果超出有限边界。",
                    dispatchedAt,
                    receivedAt);
            }

            byte[] responseBytes = _strictUtf8.GetBytes(response.Text);
            WorkerAgentJsonPreflight.Validate(responseBytes);
            WorkerAgentPass1ResponseDto? parsed = JsonSerializer.Deserialize(
                responseBytes,
                WorkerAgentJsonContext.Default.WorkerAgentPass1ResponseDto);
            if (parsed is null)
            {
                throw new JsonException("Worker Agent result is empty.");
            }

            return WorkerAgentResultValidator.ValidatePass1(
                packet,
                parsed,
                dispatchedAt,
                receivedAt);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or ArgumentException)
        {
            return WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.SchemaInvalid,
                "Worker Agent 返回了无效结果。",
                dispatchedAt,
                receivedAt);
        }
    }

    private async Task<WorkerAgentResultEnvelope> DispatchPass2Async(
        WorkerAgentJobPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (packet.Kind != WorkerAgentJobKind.Pass2BusinessLogicDocument ||
            packet.Payload is not Pass2BusinessLogicJobPayload)
        {
            throw new InvalidOperationException("The Worker Agent job variant is not supported.");
        }

        ResolvedWorkerAgentProvider provider =
            await _providerResolver.ResolveAsync(packet.SystemId, cancellationToken);
        string prompt = LoadPass2Prompt(typeof(WorkerAgentDispatcher).Assembly);
        string requestJson = JsonSerializer.Serialize(
            WorkerAgentPass2RequestDto.From(packet),
            WorkerAgentJsonContext.Default.WorkerAgentPass2RequestDto);
        if (_strictUtf8.GetByteCount(requestJson) >
            WorkerAgentContractLimits.MaximumProviderRequestUtf8Bytes)
        {
            throw new InvalidDataException("Worker Agent request exceeds its finite byte bound.");
        }

        using JsonDocument schema = JsonDocument.Parse(
            WorkerAgentJsonContext.Pass2ResponseSchemaJson);
        Microsoft.Extensions.AI.ChatResponseFormat responseFormat =
            Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema(
                schema.RootElement,
                "pass2_business_logic_document",
                "Closed Pass 2 Business Logic Document result.");
        DateTimeOffset dispatchedAt = _timeProvider.GetUtcNow();
        string apiKey = await _encryptionService.DecryptAsync(
            SecretPurposes.LlmApiKey,
            provider.EncryptedApiKey,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("The Worker Agent credential is unavailable.");
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
            name: "pass2-business-flow-worker");
        var runOptions = new ChatClientAgentRunOptions(
            new ChatOptions { ResponseFormat = responseFormat });
        AgentResponse response;
        try
        {
            response = await agent.RunAsync(
                requestJson,
                session: null,
                options: runOptions,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.Error,
                "Worker Agent 调用失败。",
                dispatchedAt,
                _timeProvider.GetUtcNow());
        }

        DateTimeOffset receivedAt = _timeProvider.GetUtcNow();
        try
        {
            if (response.Text.Length >
                    WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes ||
                _strictUtf8.GetByteCount(response.Text) >
                    WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes)
            {
                return WorkerAgentResultEnvelope.CreateForJob(
                    packet,
                    WorkerAgentResultOutcome.Unbounded,
                    "Worker Agent 返回结果超出有限边界。",
                    dispatchedAt,
                    receivedAt);
            }

            byte[] responseBytes = _strictUtf8.GetBytes(response.Text);
            WorkerAgentJsonPreflight.Validate(responseBytes);
            WorkerAgentPass2ResponseDto? parsed = JsonSerializer.Deserialize(
                responseBytes,
                WorkerAgentJsonContext.Default.WorkerAgentPass2ResponseDto);
            if (parsed is null)
            {
                throw new JsonException("Worker Agent result is empty.");
            }

            return WorkerAgentResultValidator.ValidatePass2(
                packet,
                parsed,
                dispatchedAt,
                receivedAt);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or ArgumentException)
        {
            return WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.SchemaInvalid,
                "Worker Agent 返回了无效结果。",
                dispatchedAt,
                receivedAt);
        }
    }

    internal static string LoadPass2Prompt(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        WorkerAgentPromptResource[] resources = assembly.GetManifestResourceNames()
            .Select(name => new WorkerAgentPromptResource(
                name,
                () => assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException(
                        "The fixed Pass 2 prompt resource is unavailable.")))
            .ToArray();
        return LoadPass2Prompt(resources);
    }

    internal static string LoadPass2Prompt(
        IReadOnlyList<WorkerAgentPromptResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        WorkerAgentPromptResource[] matches = resources
            .Where(resource => StringComparer.Ordinal.Equals(
                resource.Name,
                _pass2PromptResourceName))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException("The fixed Pass 2 prompt resource is unavailable.");
        }

        using Stream stream = matches[0].Open();
        if (stream.Length is < 1 or > WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes)
        {
            throw new InvalidOperationException("The fixed Pass 2 prompt resource is outside its finite bound.");
        }

        using var reader = new StreamReader(
            stream,
            _strictUtf8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false);
        string prompt = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(prompt) ||
            _strictUtf8.GetByteCount(prompt) >
                WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes)
        {
            throw new InvalidOperationException("The fixed Pass 2 prompt resource is invalid.");
        }

        return prompt;
    }

    internal static string LoadPass1Prompt(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        WorkerAgentPromptResource[] resources = assembly.GetManifestResourceNames()
            .Select(name => new WorkerAgentPromptResource(
                name,
                () => assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException(
                        "The fixed Pass 1 prompt resource is unavailable.")))
            .ToArray();
        return LoadPass1Prompt(resources);
    }

    internal static string LoadPass1Prompt(
        IReadOnlyList<WorkerAgentPromptResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        WorkerAgentPromptResource[] matches = resources
            .Where(resource => StringComparer.Ordinal.Equals(
                resource.Name,
                _pass1PromptResourceName))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException("The fixed Pass 1 prompt resource is unavailable.");
        }

        using Stream stream = matches[0].Open();
        if (stream.Length is < 1 or > WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes)
        {
            throw new InvalidOperationException("The fixed Pass 1 prompt resource is outside its finite bound.");
        }

        using var reader = new StreamReader(
            stream,
            _strictUtf8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false);
        string prompt = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(prompt) ||
            _strictUtf8.GetByteCount(prompt) >
                WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes)
        {
            throw new InvalidOperationException("The fixed Pass 1 prompt resource is invalid.");
        }

        return prompt;
    }
}

internal sealed record WorkerAgentPromptResource(
    string Name,
    Func<Stream> Open);
