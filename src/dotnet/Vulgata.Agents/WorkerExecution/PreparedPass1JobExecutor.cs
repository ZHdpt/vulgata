using System.ClientModel;
using System.ClientModel.Primitives;
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

/// <summary>
/// Executes one prepared File Code Unit against the configured Worker Agent.
/// The provider receives exactly one bounded function and no server-owned
/// lease or version authorization.
/// </summary>
public sealed class PreparedPass1JobExecutor(
    WorkerAgentProviderResolver providerResolver,
    ISecretEncryptionService encryptionService,
    PreparedNavigateCodeFunction navigateCodeFunction,
    HttpMessageHandler httpMessageHandler) : IPreparedPass1JobExecutor
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    public async Task<PreparedPass1RawWorkerResult> ExecuteAsync(
        PreparedPass1WorkerPacket packet,
        string systemPrompt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        cancellationToken.ThrowIfCancellationRequested();

        if (packet.RemainingNavigationBudget <= 0)
        {
            return PreparedPass1ResultParser.CreateDeferredCallsFallback(packet);
        }

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

        ulong toolAttemptOrdinal = checked((ulong)(9 - packet.RemainingNavigationBudget));
        AIFunction navigateCode = navigateCodeFunction.Create(new(
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
            authorization.AuthorizationBinding));

        using var httpClient = new HttpClient(httpMessageHandler, disposeHandler: false);
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
            instructions: systemPrompt,
            name: "prepared-pass1-code-logic-worker");
        var runOptions = new ChatClientAgentRunOptions(new ChatOptions
        {
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Json,
            Tools = [navigateCode],
        });

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
        catch (ClientResultException exception) when (exception.Status is 401 or 403)
        {
            return ProviderConfiguration();
        }
        catch (ClientResultException exception) when (
            exception.Status is 408 or 429 || exception.Status >= 500)
        {
            return ProviderUnavailable();
        }
        catch (HttpRequestException)
        {
            return ProviderUnavailable();
        }
        catch (TaskCanceledException)
        {
            return ProviderUnavailable();
        }
        catch (Exception)
        {
            return new(
                PreparedPass1RawWorkerOutcome.Rejected,
                ReadOnlyMemory<byte>.Empty,
                "unclassified-systemic");
        }

        try
        {
            byte[] utf8 = _strictUtf8.GetBytes(response.Text);
            return utf8.Length is > 0 and <= CodeLogicFileV2Contract.MaximumUtf8Bytes
                ? new(PreparedPass1RawWorkerOutcome.Completed, utf8, null)
                : new(
                    PreparedPass1RawWorkerOutcome.Rejected,
                    ReadOnlyMemory<byte>.Empty,
                    "worker-result-rejected");
        }
        catch (EncoderFallbackException)
        {
            return new(
                PreparedPass1RawWorkerOutcome.Rejected,
                ReadOnlyMemory<byte>.Empty,
                "worker-result-rejected");
        }
    }

    private static string CreateRequestJson(PreparedPass1WorkerPacket packet)
    {
        string source = _strictUtf8.GetString(packet.SourceUtf8.Span);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            binding = new
            {
                packet.JobId,
                packet.ScanExecutionId,
                packet.SystemId,
                packet.RepositoryId,
                packet.ScanRunId,
                packet.ProfiledRevision,
                packet.FileCodeUnitId,
                packet.FileCodeUnitIdentityFingerprint,
                packet.PreparedScanManifestId,
                packet.PreparedScanManifestFingerprint,
                packet.PairPackageFingerprint,
                packet.RepositoryRelativePath,
                packet.ByteStart,
                packet.ByteEnd,
                packet.LineStart,
                packet.LineEnd,
                packet.SegmentOrdinal,
                packet.SegmentCount,
                packet.FileHash,
                packet.SourceHash,
                packet.PromptIdentity,
                packet.DocumentSchemaIdentity,
                packet.AnalysisRuntimeIdentityHash,
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

    private static PreparedPass1RawWorkerResult ProviderUnavailable() =>
        new(
            PreparedPass1RawWorkerOutcome.ProviderUnavailable,
            ReadOnlyMemory<byte>.Empty,
            "provider-unavailable");
}
