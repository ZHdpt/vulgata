using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared;

namespace Vulgata.Tests;

public sealed class WorkerAgentDispatcherTests
{
    private const string _pass1PromptResourceName =
        "Vulgata.Agents.Prompts.pass1-code-logic-worker.v1.md";
    private const string _pass2PromptResourceName =
        "Vulgata.Agents.Prompts.pass2-business-flow-worker.v1.md";

    [Fact]
    public async Task Pass1Prompt_FixedEmbeddedResource_IsFiniteAndNonEmpty()
    {
        var assembly = typeof(RevisionBoundWorkerContextHydrator).Assembly;
        string[] matches = assembly.GetManifestResourceNames()
            .Where(name => StringComparer.Ordinal.Equals(name, _pass1PromptResourceName))
            .ToArray();

        string resourceName = Assert.Single(matches);
        await using Stream stream = Assert.IsAssignableFrom<Stream>(
            assembly.GetManifestResourceStream(resourceName));
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);
        string prompt = await reader.ReadToEndAsync();

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.InRange(
            Encoding.UTF8.GetByteCount(prompt),
            1,
            WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes);
        Assert.Contains("Pass 1", prompt, StringComparison.Ordinal);
        Assert.Contains("Code Logic Document", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pass2Prompt_FixedEmbeddedResource_IsFiniteAndNonEmpty()
    {
        var assembly = typeof(RevisionBoundWorkerContextHydrator).Assembly;
        string[] matches = assembly.GetManifestResourceNames()
            .Where(name => StringComparer.Ordinal.Equals(name, _pass2PromptResourceName))
            .ToArray();

        string resourceName = Assert.Single(matches);
        await using Stream stream = Assert.IsAssignableFrom<Stream>(
            assembly.GetManifestResourceStream(resourceName));
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);
        string prompt = await reader.ReadToEndAsync();

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.InRange(
            Encoding.UTF8.GetByteCount(prompt),
            1,
            WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes);
        Assert.Contains("Pass 2", prompt, StringComparison.Ordinal);
        Assert.Contains("Business Logic Document", prompt, StringComparison.Ordinal);
        Assert.Contains("purpose", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("main steps", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("outcome", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no base REFERENCES", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true, "system.worker.invalid")]
    [InlineData(false, "global.worker.invalid")]
    public async Task DispatchAsync_WorkerAssignment_SendsOneMinimalStructuredPass1Call(
        bool useSystemOverride,
        string expectedHost)
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-g3-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src", "Orders"));
        try
        {
            const string source = "return order.Lines.Sum(line => line.Amount);";
            string sourcePath = Path.Combine(root, "src", "Orders", "OrderService.cs");
            await File.WriteAllTextAsync(sourcePath, source, new UTF8Encoding(false));

            Guid systemId = WorkerAgentTestFixtures.Id(4);
            Repository repository = Repository.Create(
                systemId,
                "Orders",
                "https://git.invalid/orders.git",
                null,
                null,
                DateTimeOffset.Parse("2026-08-01T00:00:00Z"));
            byte[] fileBytes = await File.ReadAllBytesAsync(sourcePath);
            string sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))
                .ToLowerInvariant();
            string fileIdentityHash = Convert.ToHexString(SHA256.HashData(fileBytes))
                .ToLowerInvariant();
            Pass1CodeLogicJobPayload payload = Pass1CodeLogicJobPayload.Create(
                WorkerAgentTestFixtures.Id(6),
                WorkerAgentTestFixtures.RepositoryRelativePath,
                1,
                1,
                "CalculateTotal",
                CodeUnitType.Method,
                "csharp",
                sourceHash,
                fileIdentityHash,
                [WorkerAgentStructuralContext.Create(
                    WorkerAgentTestFixtures.Id(7),
                    "src/Orders/Order.cs",
                    "Order")],
                [WorkerAgentCommunicationEvidence.Create(
                    WorkerAgentTestFixtures.Id(8),
                    "HTTP",
                    "订单服务调用库存接口。")],
                [WorkerAgentTestFixtures.Id(9)]);
            WorkerAgentJobPacket packet = WorkerAgentJobPacket.Create(
                WorkerAgentTestFixtures.Id(1),
                WorkerAgentJobKind.Pass1CodeLogicDocument,
                WorkerAgentTestFixtures.Id(2),
                repository.Id,
                systemId,
                WorkerAgentTestFixtures.Id(5),
                WorkerAgentTestFixtures.ProfiledRevision,
                WorkerAgentContractLimits.SupportedSchemaVersion,
                payload);
            var hydrator = new RevisionBoundWorkerContextHydrator(
                new FixedSnapshotService(root, packet));
            WorkerAgentExecutionContext context = await hydrator.HydrateAsync(
                packet,
                repository,
                GitWorkingCopyResult.Success(root, packet.ProfiledRevision),
                new RepositoryAccessScope([repository.Id]));

            var globalProvider = new LlmProvider(
                "global-worker-model",
                "https://global.worker.invalid/v1",
                "encrypted-global",
                ApiType.ChatCompletions,
                AgentType.Worker,
                DateTimeOffset.UtcNow);
            var overrideProvider = new LlmProvider(
                "system-worker-model",
                "https://system.worker.invalid/v1",
                "encrypted-system",
                ApiType.ChatCompletions,
                AgentType.Worker,
                DateTimeOffset.UtcNow);
            SystemLlmProviderOverride overrideEntry = SystemLlmProviderOverride.Create(
                systemId,
                overrideProvider.Id,
                AgentType.Worker,
                DateTimeOffset.UtcNow);
            var resolver = new WorkerAgentProviderResolver(
                new FakeOverrideRepository(useSystemOverride ? overrideEntry : null),
                new FakeProviderRepository([globalProvider, overrideProvider]));
            var encryption = new CapturingEncryptionService("loopback-secret");
            var capture = new OpenAiCaptureHandler(() => CreateChatResponse(packet));
            var dispatcher = new WorkerAgentDispatcher(
                resolver,
                encryption,
                capture,
                TimeProvider.System);

            WorkerAgentResultEnvelope result = await dispatcher.DispatchAsync(context);

            Assert.Equal(WorkerAgentResultOutcome.Valid, result.Outcome);
            Assert.Equal(packet.JobId, result.JobId);
            Assert.Equal(1, capture.CallCount);
            Assert.Equal(expectedHost, capture.RequestUri?.Host);
            Assert.EndsWith("/v1/chat/completions", capture.RequestUri?.AbsolutePath, StringComparison.Ordinal);
            Assert.Equal("Bearer", capture.AuthorizationScheme);
            Assert.Equal("loopback-secret", capture.AuthorizationParameter);
            Assert.Equal(1, encryption.DecryptCount);
            Assert.Equal(SecretPurposes.LlmApiKey, encryption.Purpose);
            using JsonDocument requestDocument = JsonDocument.Parse(capture.Body);
            JsonElement messages = requestDocument.RootElement.GetProperty("messages");
            string prompt = messages.EnumerateArray()
                .Single(message => message.GetProperty("role").GetString() == "system")
                .GetProperty("content")
                .GetString()!;
            string requestJson = messages.EnumerateArray()
                .Single(message => message.GetProperty("role").GetString() == "user")
                .GetProperty("content")
                .GetString()!;
            using JsonDocument contextDocument = JsonDocument.Parse(requestJson);
            JsonElement contextRoot = contextDocument.RootElement;
            Assert.Equal(packet.JobId, contextRoot.GetProperty("jobId").GetGuid());
            Assert.Equal(packet.SystemId, contextRoot.GetProperty("systemId").GetGuid());
            Assert.Equal(packet.RepositoryId, contextRoot.GetProperty("repositoryId").GetGuid());
            Assert.Equal(packet.ScanRunId, contextRoot.GetProperty("scanRunId").GetGuid());
            Assert.Equal(
                source,
                contextRoot.GetProperty("source").GetProperty("sourceText").GetString());
            Assert.Contains("Pass 1 Code Logic Document Worker Agent", prompt, StringComparison.Ordinal);
            Assert.Contains("Simplified Chinese", prompt, StringComparison.Ordinal);
            Assert.Contains("preserve source identifiers", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("response_format", capture.Body, StringComparison.Ordinal);
            Assert.Contains("json_object", capture.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("loopback-secret", capture.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("unrelated source", capture.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(root, capture.Body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DispatchAsync_PreparedPass2_SendsOneExactProjectionCallWithoutHydrationOrFallback()
    {
        WorkerAgentJobPacket packet = CreatePass2Packet();
        var provider = new LlmProvider(
            "pass2-worker-model",
            "https://pass2.worker.invalid/v1",
            "encrypted-pass2",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        var hydration = new ThrowingExecutionContextSource();
        var encryption = new CapturingEncryptionService("loopback-secret");
        var capture = new OpenAiCaptureHandler(
            () => CreateChatResponse(CreatePass2ResultJson(packet)));
        var dispatcher = new WorkerAgentDispatcher(
            new WorkerAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            encryption,
            hydration,
            capture,
            TimeProvider.System);

        WorkerAgentResultEnvelope result = await dispatcher.DispatchAsync(
            packet,
            new RepositoryAccessScope([packet.RepositoryId]),
            CancellationToken.None);

        Pass2BusinessLogicDocumentResultPayload payload = Assert.IsType<
            Pass2BusinessLogicDocumentResultPayload>(result.Payload);
        Assert.Equal(WorkerAgentResultOutcome.Valid, result.Outcome);
        Assert.Equal("帮助业务人员理解订单计价与库存预留。", payload.Purpose);
        Assert.Equal([1, 2], payload.MainSteps.Select(step => step.Order));
        Assert.Equal("订单完成计价并成功预留库存。", payload.Outcome);
        Assert.Equal(0, hydration.CallCount);
        Assert.Equal(1, capture.CallCount);
        Assert.Equal("pass2.worker.invalid", capture.RequestUri?.Host);
        Assert.EndsWith(
            "/v1/chat/completions",
            capture.RequestUri?.AbsolutePath,
            StringComparison.Ordinal);

        using JsonDocument requestDocument = JsonDocument.Parse(capture.Body);
        JsonElement messages = requestDocument.RootElement.GetProperty("messages");
        string prompt = messages.EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "system")
            .GetProperty("content")
            .GetString()!;
        string requestJson = messages.EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "user")
            .GetProperty("content")
            .GetString()!;
        using JsonDocument contextDocument = JsonDocument.Parse(requestJson);
        JsonElement contextRoot = contextDocument.RootElement;
        Assert.Equal(
            [
                "jobId",
                "systemId",
                "repositoryId",
                "scanRunId",
                "profiledRevision",
                "schemaVersion",
                "kind",
                "preparedFlowSchemaIdentity",
                "manifestId",
                "flowId",
                "traceEvidence",
                "codeLogicDocumentVersions",
            ],
            contextRoot.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [1, 2],
            contextRoot.GetProperty("traceEvidence")
                .EnumerateArray()
                .Select(item => item.GetProperty("order").GetInt32()));
        Assert.Equal(
            [WorkerAgentTestFixtures.Id(301), WorkerAgentTestFixtures.Id(302)],
            contextRoot.GetProperty("codeLogicDocumentVersions")
                .EnumerateArray()
                .Select(item => item.GetProperty("documentVersionId").GetGuid()));
        Assert.Equal(
            [packet.RepositoryId, packet.RepositoryId],
            contextRoot.GetProperty("codeLogicDocumentVersions")
                .EnumerateArray()
                .Select(item => item.GetProperty("repositoryId").GetGuid()));
        Assert.Contains("Pass 2 Business Logic Document Worker Agent", prompt, StringComparison.Ordinal);
        Assert.Contains("Consumer-owned section evidence", prompt, StringComparison.Ordinal);
        Assert.Contains("Simplified Chinese", prompt, StringComparison.Ordinal);
        Assert.Contains("preserve source identifiers", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "json_object",
            requestDocument.RootElement
                .GetProperty("response_format")
                .GetProperty("type")
                .GetString());
        Assert.Equal(
            JsonUnmappedMemberHandling.Disallow,
            WorkerAgentJsonContext.Default.Options.UnmappedMemberHandling);
        Assert.DoesNotContain("sourceText", requestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("query", requestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("discover", requestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cluster", requestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reference", requestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("loopback-secret", capture.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveAsync_WithoutSystemOverride_UsesExistingGlobalWorkerAssignmentOnly()
    {
        var orchestratorProvider = new LlmProvider(
            "orchestrator-model",
            "https://orchestrator.invalid/v1",
            "encrypted-orchestrator",
            ApiType.ChatCompletions,
            AgentType.Orchestrator,
            DateTimeOffset.UtcNow);
        var globalWorkerProvider = new LlmProvider(
            "global-worker-model",
            "https://global.worker.invalid/v1",
            "encrypted-global",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        var resolver = new WorkerAgentProviderResolver(
            new FakeOverrideRepository(null),
            new FakeProviderRepository([orchestratorProvider, globalWorkerProvider]));

        ResolvedWorkerAgentProvider result = await resolver.ResolveAsync(
            WorkerAgentTestFixtures.Id(4));

        Assert.Equal(globalWorkerProvider.Id, result.ProviderId);
        Assert.Equal(new Uri("https://global.worker.invalid/v1"), result.Endpoint);
        Assert.False(result.IsSystemOverride);
    }

    [Fact]
    public void Pass1Prompt_InvalidFixedResourceSet_FailsClosedWithoutRuntimeReplacement()
    {
        WorkerAgentPromptResource valid = new(
            _pass1PromptResourceName,
            () => new MemoryStream("fixed prompt"u8.ToArray(), writable: false));

        _ = Assert.Throws<InvalidOperationException>(() =>
            WorkerAgentDispatcher.LoadPass1Prompt([]));
        _ = Assert.Throws<InvalidOperationException>(() =>
            WorkerAgentDispatcher.LoadPass1Prompt([valid, valid]));
        _ = Assert.Throws<InvalidOperationException>(() =>
            WorkerAgentDispatcher.LoadPass1Prompt(
            [
                new(_pass1PromptResourceName, () => new MemoryStream()),
            ]));
        _ = Assert.Throws<InvalidOperationException>(() =>
            WorkerAgentDispatcher.LoadPass1Prompt(
            [
                new(
                    _pass1PromptResourceName,
                    () => new MemoryStream(
                        new byte[WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes + 1],
                        writable: false)),
            ]));

        Assert.DoesNotContain(
            typeof(WorkerAgentDispatcher).GetConstructors()
                .SelectMany(constructor => constructor.GetParameters()),
            parameter =>
                parameter.ParameterType == typeof(string) &&
                parameter.Name?.Contains("prompt", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task DispatchAsync_HttpError_ReturnsBoundedClosedErrorWithoutRawDiagnostic()
    {
        const string rawDiagnostic = "provider-secret-diagnostic";
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        WorkerAgentExecutionContext context = WorkerAgentExecutionContext.Create(packet, string.Empty, 1);
        var provider = new LlmProvider(
            "worker-model",
            "https://worker.invalid/v1",
            "encrypted-worker",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        var dispatcher = new WorkerAgentDispatcher(
            new WorkerAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("loopback-secret"),
            new HttpErrorHandler(rawDiagnostic),
            TimeProvider.System);

        WorkerAgentResultEnvelope result = await dispatcher.DispatchAsync(context);

        Assert.Equal(WorkerAgentResultOutcome.Error, result.Outcome);
        Assert.Equal("Worker Agent 调用失败。", result.BoundedReason);
        Assert.DoesNotContain(rawDiagnostic, result.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InvalidProviderOutput.Oversized)]
    [InlineData(InvalidProviderOutput.ExcessiveDepth)]
    [InlineData(InvalidProviderOutput.TrailingJson)]
    [InlineData(InvalidProviderOutput.DuplicateProperty)]
    [InlineData(InvalidProviderOutput.UnknownProperty)]
    [InlineData(InvalidProviderOutput.UnknownEnum)]
    [InlineData(InvalidProviderOutput.DuplicateValue)]
    [InlineData(InvalidProviderOutput.OutOfOrderValue)]
    [InlineData(InvalidProviderOutput.IdentityMismatch)]
    [InlineData(InvalidProviderOutput.AnchorOutOfRange)]
    [InlineData(InvalidProviderOutput.UnallowlistedTarget)]
    [InlineData(InvalidProviderOutput.Secret)]
    [InlineData(InvalidProviderOutput.AbsolutePath)]
    [InlineData(InvalidProviderOutput.RawSourceCarrier)]
    [InlineData(InvalidProviderOutput.RawDiagnostic)]
    public async Task DispatchAsync_InvalidProviderOutput_ReturnsClosedNonValidEnvelope(
        InvalidProviderOutput invalidOutput)
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        string valid = CreatePass1ResultJson(packet);
        string invalid = invalidOutput switch
        {
            InvalidProviderOutput.Oversized => new string('x',
                WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes + 1),
            InvalidProviderOutput.ExcessiveDepth =>
                new string('[', WorkerAgentContractLimits.MaximumJsonDepth + 1) +
                "0" +
                new string(']', WorkerAgentContractLimits.MaximumJsonDepth + 1),
            InvalidProviderOutput.TrailingJson => valid + "{}",
            InvalidProviderOutput.DuplicateProperty => valid.Insert(
                1,
                $"\"jobId\":\"{packet.JobId:D}\","),
            InvalidProviderOutput.UnknownProperty => valid.Insert(1, "\"unknown\":true,"),
            InvalidProviderOutput.UnknownEnum => valid.Replace(
                "\"documentType\":\"CodeLogic\"",
                "\"documentType\":\"Unknown\"",
                StringComparison.Ordinal),
            InvalidProviderOutput.DuplicateValue => valid.Replace(
                $"\"referenceDocumentVersionIds\":[\"{WorkerAgentTestFixtures.Id(9):D}\"]",
                $"\"referenceDocumentVersionIds\":[\"{WorkerAgentTestFixtures.Id(9):D}\",\"{WorkerAgentTestFixtures.Id(9):D}\"]",
                StringComparison.Ordinal),
            InvalidProviderOutput.OutOfOrderValue => valid.Replace(
                "\"keySymbols\":[\"CalculateTotal\"]",
                "\"keySymbols\":[\"Zulu\",\"Alpha\"]",
                StringComparison.Ordinal),
            InvalidProviderOutput.IdentityMismatch => valid.Replace(
                packet.SystemId.ToString("D"),
                WorkerAgentTestFixtures.Id(404).ToString("D"),
                StringComparison.Ordinal),
            InvalidProviderOutput.AnchorOutOfRange => valid.Replace(
                "\"lineStart\":12",
                "\"lineStart\":11",
                StringComparison.Ordinal),
            InvalidProviderOutput.UnallowlistedTarget => valid.Replace(
                WorkerAgentTestFixtures.Id(9).ToString("D"),
                WorkerAgentTestFixtures.Id(99).ToString("D"),
                StringComparison.Ordinal),
            InvalidProviderOutput.Secret => ReplaceSummary(
                valid,
                "api_key=provider-secret-value"),
            InvalidProviderOutput.AbsolutePath => valid.Replace(
                WorkerAgentTestFixtures.RepositoryRelativePath,
                "C:/host/Orders/OrderService.cs",
                StringComparison.Ordinal),
            InvalidProviderOutput.RawSourceCarrier => valid.Insert(
                1,
                "\"sourceText\":\"return secret;\","),
            InvalidProviderOutput.RawDiagnostic => ReplaceSummary(
                valid,
                "HTTP 500 Internal Server Error stack trace: at provider.client"),
            _ => throw new InvalidOperationException(),
        };
        WorkerAgentResultEnvelope result = await DispatchProviderContentAsync(packet, invalid);

        Assert.NotEqual(WorkerAgentResultOutcome.Valid, result.Outcome);
        Assert.NotNull(result.BoundedReason);
        Assert.InRange(Encoding.UTF8.GetByteCount(result.BoundedReason), 1, 512);
        Assert.DoesNotContain("provider-secret-value", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("stack trace", result.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:/host", result.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JsonPreflight_InvalidUtf8_IsRejectedBeforeDeserialization()
    {
        byte[] invalidUtf8 = [0x7b, 0x22, 0x78, 0x22, 0x3a, 0x22, 0xff, 0x22, 0x7d];

        _ = Assert.ThrowsAny<JsonException>(() => WorkerAgentJsonPreflight.Validate(invalidUtf8));
    }

    [Fact]
    public async Task DispatchAsync_Cancellation_PropagatesWithoutFabricatingResultAsync()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        WorkerAgentExecutionContext context = WorkerAgentExecutionContext.Create(packet, string.Empty, 1);
        var provider = new LlmProvider(
            "worker-model",
            "https://worker.invalid/v1",
            "encrypted-worker",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        var dispatcher = new WorkerAgentDispatcher(
            new WorkerAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("loopback-secret"),
            new OpenAiCaptureHandler(() => CreateChatResponse(packet)),
            TimeProvider.System);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync(context, cancellation.Token));
    }

    [Fact]
    public async Task DispatchAsync_Pass1PacketWithoutScope_RejectsBeforeHydrationAsync()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        var source = new ThrowingExecutionContextSource();
        var provider = new LlmProvider(
            "worker-model",
            "https://worker.invalid/v1",
            "encrypted-worker",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        var capture = new HttpErrorHandler("must-not-be-contacted");
        var dispatcher = new WorkerAgentDispatcher(
            new WorkerAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("must-not-be-decrypted"),
            source,
            capture,
            TimeProvider.System);

        InvalidOperationException exception = await Assert.ThrowsAsync<
            InvalidOperationException>(() => dispatcher.DispatchAsync(
                packet,
                CancellationToken.None));

        Assert.Equal(
            "A Repository access scope is required for Worker Agent dispatch.",
            exception.Message);
        Assert.Equal(0, source.CallCount);
    }

    [Fact]
    public async Task DispatchAsync_Pass2PacketWithoutScope_RejectsBeforeProviderTransmissionAsync()
    {
        WorkerAgentJobPacket packet = CreatePass2Packet();
        var provider = new LlmProvider(
            "worker-model",
            "https://worker.invalid/v1",
            "encrypted-worker",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        var capture = new OpenAiCaptureHandler(() => CreateChatResponse(packet));
        var dispatcher = new WorkerAgentDispatcher(
            new WorkerAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("must-not-be-decrypted"),
            capture,
            TimeProvider.System);

        InvalidOperationException exception = await Assert.ThrowsAsync<
            InvalidOperationException>(() => dispatcher.DispatchAsync(
                packet,
                CancellationToken.None));

        Assert.Equal(
            "A Repository access scope is required for Worker Agent dispatch.",
            exception.Message);
        Assert.Equal(0, capture.CallCount);
    }

    [Fact]
    public async Task DispatchAsync_Pass1PacketWithScope_PropagatesExactScopeToHydrationAsync()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        WorkerAgentExecutionContext context = WorkerAgentExecutionContext.Create(
            packet,
            string.Empty,
            1);
        var source = new CapturingExecutionContextSource(context);
        var provider = new LlmProvider(
            "worker-model",
            "https://worker.invalid/v1",
            "encrypted-worker",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        var capture = new OpenAiCaptureHandler(() => CreateChatResponse(packet));
        var dispatcher = new WorkerAgentDispatcher(
            new WorkerAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("loopback-secret"),
            source,
            capture,
            TimeProvider.System);
        var scope = new RepositoryAccessScope([packet.RepositoryId]);

        WorkerAgentResultEnvelope result = await dispatcher.DispatchAsync(
            packet,
            scope,
            CancellationToken.None);

        Assert.Equal(WorkerAgentResultOutcome.Valid, result.Outcome);
        Assert.Equal(1, source.CallCount);
        Assert.Same(scope, source.Scope);
        Assert.Equal(1, capture.CallCount);
    }

    private static async Task<WorkerAgentResultEnvelope> DispatchProviderContentAsync(
        WorkerAgentJobPacket packet,
        string resultJson)
    {
        WorkerAgentExecutionContext context = WorkerAgentExecutionContext.Create(packet, string.Empty, 1);
        var provider = new LlmProvider(
            "worker-model",
            "https://worker.invalid/v1",
            "encrypted-worker",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        var dispatcher = new WorkerAgentDispatcher(
            new WorkerAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("loopback-secret"),
            new OpenAiCaptureHandler(() => CreateChatResponse(resultJson)),
            TimeProvider.System);
        return await dispatcher.DispatchAsync(context);
    }

    private static WorkerAgentJobPacket CreatePass2Packet()
    {
        PreparedPass2Flow flow = PreparedPass2Flow.Create(
            PreparedPass2FlowLimits.SupportedSchemaIdentity,
            WorkerAgentTestFixtures.Id(200),
            WorkerAgentTestFixtures.Id(201),
            WorkerAgentTestFixtures.Id(4),
            WorkerAgentTestFixtures.Id(3),
            [
                new(1, WorkerAgentTestFixtures.Id(1), WorkerAgentTestFixtures.Id(6), WorkerAgentTestFixtures.Id(101)),
                new(2, WorkerAgentTestFixtures.Id(10), WorkerAgentTestFixtures.Id(11), WorkerAgentTestFixtures.Id(102)),
            ],
            [
                new(1, WorkerAgentTestFixtures.Id(1), WorkerAgentTestFixtures.Id(6), "订单总额计算", "src/Orders/OrderService.cs#L12-L24"),
                new(2, WorkerAgentTestFixtures.Id(10), WorkerAgentTestFixtures.Id(11), "库存预留", "src/Inventory/InventoryService.cs#L30-L44"),
            ]);
        CurrentKnowledge[] documents =
        [
            CreatePass2CurrentDocument(flow.Members[0], 301, 401),
            CreatePass2CurrentDocument(flow.Members[1], 302, 402),
        ];
        return WorkerAgentJobPacket.Create(
            WorkerAgentTestFixtures.Id(501),
            WorkerAgentJobKind.Pass2BusinessLogicDocument,
            WorkerAgentTestFixtures.Id(2),
            WorkerAgentTestFixtures.Id(3),
            WorkerAgentTestFixtures.Id(4),
            WorkerAgentTestFixtures.Id(5),
            WorkerAgentTestFixtures.ProfiledRevision,
            WorkerAgentContractLimits.SupportedSchemaVersion,
            Pass2BusinessLogicJobPayload.Create(flow, documents));
    }

    private static CurrentKnowledge CreatePass2CurrentDocument(
        PreparedPass2FlowMember member,
        int documentVersionId,
        int anchorId) =>
        new(
            member.LogicalDocumentId,
            WorkerAgentTestFixtures.Id(documentVersionId),
            WorkerAgentTestFixtures.Id(3),
            DocumentType.CodeLogic,
            $"Code Logic {member.Order}",
            $"src/Demo/Flow{member.Order}.cs",
            "{}",
            $"Code Logic {member.Order}",
            [new CurrentSourceAnchor(
                WorkerAgentTestFixtures.Id(anchorId),
                member.CodeUnitId,
                $"src/Demo/Flow{member.Order}.cs",
                $"Flow{member.Order}.Execute",
                1,
                2,
                new string('a', 64))],
            [],
            [],
            new CurrentIndexEntry($"Summary {member.Order}", [], [], [], $"flow {member.Order}"),
            []);

    private static string CreatePass2ResultJson(WorkerAgentJobPacket packet)
    {
        var pass2 = Assert.IsType<Pass2BusinessLogicJobPayload>(packet.Payload);
        return JsonSerializer.Serialize(new
        {
            jobId = packet.JobId,
            systemId = packet.SystemId,
            repositoryId = packet.RepositoryId,
            scanRunId = packet.ScanRunId,
            profiledRevision = packet.ProfiledRevision,
            flowId = pass2.FlowId,
            purpose = "帮助业务人员理解订单计价与库存预留。",
            mainSteps = new[]
            {
                new { order = 1, description = "计算订单总额。", traceEvidenceOrders = new[] { 1 } },
                new { order = 2, description = "预留对应库存。", traceEvidenceOrders = new[] { 2 } },
            },
            outcome = "订单完成计价并成功预留库存。",
            content = new
            {
                schemaIdentity = KnowledgeDocumentLimits.SchemaIdentity,
                documentType = nameof(DocumentType.BusinessLogic),
                stableKey = "orders.price-and-reserve",
                title = "订单计价与库存预留",
                repositoryRelativePath = "flows/orders-price-and-reserve",
                summary = "订单完成计价后预留库存。",
                keySymbols = Array.Empty<string>(),
                businessTerms = new[] { "库存", "订单" },
                technicalTerms = Array.Empty<string>(),
                sections = new[]
                {
                    new
                    {
                        stableSectionId = WorkerAgentTestFixtures.Id(601),
                        order = 1,
                        kind = nameof(DocumentSectionKind.Overview),
                        heading = "业务目的",
                        markdown = "帮助业务人员理解订单计价与库存预留。",
                        searchText = "订单 计价 库存 预留",
                        sourceAnchorIds = new[] { WorkerAgentTestFixtures.Id(401) },
                        documentVersionIds = new[] { WorkerAgentTestFixtures.Id(301) },
                    },
                    new
                    {
                        stableSectionId = WorkerAgentTestFixtures.Id(602),
                        order = 2,
                        kind = nameof(DocumentSectionKind.ProcessStep),
                        heading = "主要步骤",
                        markdown = "先计算订单总额，再预留对应库存。",
                        searchText = "订单 总额 库存",
                        sourceAnchorIds = new[]
                        {
                            WorkerAgentTestFixtures.Id(401),
                            WorkerAgentTestFixtures.Id(402),
                        },
                        documentVersionIds = new[]
                        {
                            WorkerAgentTestFixtures.Id(301),
                            WorkerAgentTestFixtures.Id(302),
                        },
                    },
                    new
                    {
                        stableSectionId = WorkerAgentTestFixtures.Id(603),
                        order = 3,
                        kind = nameof(DocumentSectionKind.Overview),
                        heading = "业务结果",
                        markdown = "订单完成计价并成功预留库存。",
                        searchText = "订单 结果 库存",
                        sourceAnchorIds = new[] { WorkerAgentTestFixtures.Id(402) },
                        documentVersionIds = new[] { WorkerAgentTestFixtures.Id(302) },
                    },
                },
            },
            sourceAnchors = pass2.CodeLogicDocumentVersions
                .SelectMany(document => document.SourceAnchors)
                .Select(anchor => new
                {
                    id = anchor.Id,
                    repositoryRelativePath = anchor.RepositoryRelativePath,
                    evidenceLocator = anchor.EvidenceLocator,
                    lineStart = anchor.LineStart,
                    lineEnd = anchor.LineEnd,
                    sourceHash = anchor.SourceHash,
                }),
            traceEvidenceOrders = new[] { 1, 2 },
            generatedFromDocumentVersionIds = new[]
            {
                WorkerAgentTestFixtures.Id(301),
                WorkerAgentTestFixtures.Id(302),
            },
        });
    }

    private static string CreateChatResponse(WorkerAgentJobPacket packet) =>
        CreateChatResponse(CreatePass1ResultJson(packet));

    private static string CreatePass1ResultJson(WorkerAgentJobPacket packet)
    {
        var pass1 = Assert.IsType<Pass1CodeLogicJobPayload>(packet.Payload);
        return JsonSerializer.Serialize(new
        {
            jobId = packet.JobId,
            systemId = packet.SystemId,
            repositoryId = packet.RepositoryId,
            scanRunId = packet.ScanRunId,
            codeUnitId = packet.CodeUnitId,
            profiledRevision = packet.ProfiledRevision,
            repositoryRelativePath = pass1.RepositoryRelativePath,
            lineStart = pass1.LineStart,
            lineEnd = pass1.LineEnd,
            sourceHash = pass1.SourceHash,
            fileIdentityHash = pass1.FileIdentityHash,
            content = new
            {
                schemaIdentity = KnowledgeDocumentLimits.SchemaIdentity,
                documentType = nameof(DocumentType.CodeLogic),
                stableKey = "orders.calculate-total",
                title = "订单总额计算",
                repositoryRelativePath = pass1.RepositoryRelativePath,
                summary = "根据订单行计算订单总额。",
                keySymbols = new[] { "CalculateTotal" },
                businessTerms = new[] { "订单总额" },
                technicalTerms = new[] { "OrderService" },
                sections = new[]
                {
                    new
                    {
                        stableSectionId = WorkerAgentTestFixtures.Id(10),
                        order = 1,
                        kind = nameof(DocumentSectionKind.TechnicalDetail),
                        heading = "计算逻辑",
                        markdown = "汇总订单行金额。",
                        searchText = "订单 总额 计算",
                        sourceAnchorIds = new[] { WorkerAgentTestFixtures.Id(11) },
                        documentVersionIds = new[] { WorkerAgentTestFixtures.Id(9) },
                    },
                },
            },
            sourceAnchors = new[]
            {
                new
                {
                    id = WorkerAgentTestFixtures.Id(11),
                    repositoryRelativePath = pass1.RepositoryRelativePath,
                    evidenceLocator = "OrderService.CalculateTotal",
                    lineStart = pass1.LineStart,
                    lineEnd = pass1.LineEnd,
                    sourceHash = pass1.SourceHash,
                },
            },
            referenceDocumentVersionIds = new[] { WorkerAgentTestFixtures.Id(9) },
            communicationEvidenceIds = new[] { WorkerAgentTestFixtures.Id(8) },
        });
    }

    private static string CreateChatResponse(string resultJson) =>
        JsonSerializer.Serialize(new
        {
            id = "chatcmpl-g3",
            @object = "chat.completion",
            created = 1_785_542_400,
            model = "system-worker-model",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = resultJson },
                    finish_reason = "stop",
                },
            },
            usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 },
        });

    private static string ReplaceSummary(string json, string summary)
    {
        JsonNode root = JsonNode.Parse(json)!;
        root["content"]!["summary"] = summary;
        return root.ToJsonString();
    }

    public enum InvalidProviderOutput
    {
        Oversized,
        ExcessiveDepth,
        TrailingJson,
        DuplicateProperty,
        UnknownProperty,
        UnknownEnum,
        DuplicateValue,
        OutOfOrderValue,
        IdentityMismatch,
        AnchorOutOfRange,
        UnallowlistedTarget,
        Secret,
        AbsolutePath,
        RawSourceCarrier,
        RawDiagnostic,
    }

    private sealed class ThrowingExecutionContextSource : IWorkerAgentExecutionContextSource
    {
        public int CallCount { get; private set; }

        public Task<WorkerAgentExecutionContext> GetAsync(
            WorkerAgentJobPacket packet,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Pass 2 must not hydrate source.");
        }
    }

    private sealed class CapturingExecutionContextSource(
        WorkerAgentExecutionContext context) : IWorkerAgentExecutionContextSource
    {
        public int CallCount { get; private set; }
        public RepositoryAccessScope? Scope { get; private set; }

        public Task<WorkerAgentExecutionContext> GetAsync(
            WorkerAgentJobPacket packet,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Scope = scope;
            return Task.FromResult(context);
        }
    }

    private sealed class FixedSnapshotService(string root, WorkerAgentJobPacket packet)
        : ICommittedRepositorySnapshotService
    {
        public Task<ICommittedRepositorySnapshotLease> AcquireAsync(
            Repository repository,
            Guid scanRunId,
            GitWorkingCopyResult workingCopy,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ICommittedRepositorySnapshotLease>(new FixedSnapshotLease(root, packet));
    }

    private sealed class FixedSnapshotLease(string root, WorkerAgentJobPacket packet)
        : ICommittedRepositorySnapshotLease
    {
        public Guid RepositoryId => packet.RepositoryId;
        public Guid ScanRunId => packet.ScanRunId;
        public string ProfiledRevision => packet.ProfiledRevision;
        public string RootPath => root;
        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeOverrideRepository(SystemLlmProviderOverride? overrideEntry)
        : ISystemLlmProviderOverrideRepository
    {
        public Task<SystemLlmProviderOverride?> GetBySystemAndAgentTypeAsync(
            Guid systemId,
            AgentType agentType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                overrideEntry is not null &&
                overrideEntry.SystemId == systemId &&
                overrideEntry.AgentType == agentType
                    ? overrideEntry
                    : null);

        public Task<IReadOnlyList<SystemLlmProviderOverride>> ListBySystemAsync(Guid systemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SystemLlmProviderOverride?> GetBySystemAndIdAsync(Guid systemId, Guid overrideId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SystemLlmProviderOverride> AddAsync(SystemLlmProviderOverride value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RemoveAsync(Guid systemId, Guid overrideId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeProviderRepository(IReadOnlyList<LlmProvider> providers)
        : ILlmProviderRepository
    {
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(providers);
        public Task<LlmProvider?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(providers.SingleOrDefault(provider => provider.Id == id));
        public Task<bool> NameExistsAsync(string name, Guid? excludeProviderId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LlmProvider> AddAsync(LlmProvider provider, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Remove(LlmProvider provider) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CapturingEncryptionService(string plaintext) : ISecretEncryptionService
    {
        public int DecryptCount { get; private set; }
        public string? Purpose { get; private set; }
        public Task<string> EncryptAsync(string purpose, string plain, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> DecryptAsync(string purpose, string cipher, CancellationToken cancellationToken = default)
        {
            Purpose = purpose;
            DecryptCount++;
            return Task.FromResult(plaintext);
        }
    }

    private sealed class OpenAiCaptureHandler(Func<string> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string Body { get; private set; } = string.Empty;
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseFactory(), Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class HttpErrorHandler(string rawDiagnostic) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(rawDiagnostic, Encoding.UTF8, "text/plain"),
            });
    }
}
