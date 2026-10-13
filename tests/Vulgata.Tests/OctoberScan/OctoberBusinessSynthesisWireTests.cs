using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.Core.Entities;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Real SDK serialization through an in-memory HTTP handler; no provider or network access.</summary>
public sealed class OctoberBusinessSynthesisWireTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReferencedSourceWire_OnlyAllowedToolAndExactReasoningReplayThenToolsDisabledJsonAsync()
    {
        using var transport = new ControlledHttp { ToolFirst = true };
        using var client = await Client(transport);
        var synthesis = Assert.IsAssignableFrom<IOctoberBusinessSynthesisClient>(client);
        var tool = AIFunctionFactory.Create((string sourceReference, int byteOffset) => "Host execution only", "read_referenced_source");
        var options = new ChatOptions { MaxOutputTokens = 40, Tools = [tool], ToolMode = ChatToolMode.Auto, ResponseFormat = null };
        ChatMessage[] original = [new(ChatRole.System, "Only exact cited source; do not invent facts."), new(ChatRole.User, "Controlled opaque source handle packet")];
        var investigation = await Send(synthesis, original, options, BusinessSynthesisAttemptStrategy.Configured);
        var call = Assert.Single(investigation.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>());
        Assert.Equal("controlled-source", call.CallId); Assert.Equal("read_referenced_source", call.Name);
        Assert.Equal("controlled-推理\nexact", Assert.Single(investigation.Messages.SelectMany(message => message.Contents).OfType<TextReasoningContent>()).Text);
        var history = original.Concat(investigation.Messages).Append(new(ChatRole.Tool,
            [new FunctionResultContent(call.CallId, "{\"status\":\"Complete\",\"text\":\"controlled cited source\"}")]))
            .Append(new(ChatRole.User, "Return only final JSON using the accepted CL claims.")).ToArray();
        string unchanged = JsonSerializer.Serialize(history);
        _ = await Send(synthesis, history, new() { MaxOutputTokens = 40, Tools = [], ToolMode = ChatToolMode.None,
            ResponseFormat = ChatResponseFormat.Json }, BusinessSynthesisAttemptStrategy.Configured);
        Assert.Equal(2, transport.Requests.Count);
        var initial = transport.Requests[0]; var final = transport.Requests[1];
        Assert.Equal("read_referenced_source", Assert.Single(initial.GetProperty("tools").EnumerateArray()).GetProperty("function").GetProperty("name").GetString());
        Assert.False(initial.TryGetProperty("response_format", out _));
        Assert.False(final.TryGetProperty("tools", out var tools) && tools.GetArrayLength() > 0);
        Assert.Equal("json_object", final.GetProperty("response_format").GetProperty("type").GetString());
        var assistant = Assert.Single(final.GetProperty("messages").EnumerateArray().Where(message => message.GetProperty("role").GetString() == "assistant"));
        Assert.Equal("controlled-推理\nexact", assistant.GetProperty("reasoning_content").GetString());
        Assert.Equal("controlled-source", Assert.Single(assistant.GetProperty("tool_calls").EnumerateArray()).GetProperty("id").GetString());
        Assert.Equal(unchanged, JsonSerializer.Serialize(history));
        Assert.Same(tool, Assert.Single(options.Tools!)); Assert.Null(options.ResponseFormat); Assert.Equal(ChatToolMode.Auto, options.ToolMode);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("read_prepared_source")]
    [InlineData("search_source")]
    [InlineData("write_database")]
    public async Task ReferencedSourceWire_ForeignToolIsRefusedBeforePhysicalSendAsync(string toolName)
    {
        using var transport = new ControlledHttp();
        using var client = await Client(transport);
        var synthesis = Assert.IsAssignableFrom<IOctoberBusinessSynthesisClient>(client);
        var tool = AIFunctionFactory.Create((string sourceReference, int byteOffset) => "must not run", toolName);
        await using var lease = new Lease();
        await Assert.ThrowsAsync<InvalidOperationException>(() => synthesis.GetSynthesisResponseAsync([new(ChatRole.User, "packet")],
            new() { MaxOutputTokens = 40, Tools = [tool], ToolMode = ChatToolMode.Auto, ResponseFormat = null }, lease,
            BusinessSynthesisAttemptStrategy.Configured, _ => Assert.Fail("No dispatch permitted")));
        Assert.Empty(transport.Requests); Assert.Equal(0, lease.Dispatches);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SynthesisStrategies_ActualWirePreservesCapAndReasoningReplayWithoutMutatingCallerAsync()
    {
        using var transport = new ControlledHttp();
        using var client = await Client(transport);
        var synthesis = Assert.IsAssignableFrom<IOctoberBusinessSynthesisClient>(client);
        Assert.True(synthesis.SupportsSynthesisCorrections);
        var reasoning = new ReasoningOptions { Effort = ReasoningEffort.High };
        var options = new ChatOptions { MaxOutputTokens = 393216, ResponseFormat = ChatResponseFormat.Json, Tools = [], Reasoning = reasoning };
        ChatMessage[] history = [new(ChatRole.System, "Original instructions"), new(ChatRole.User, "Exact accepted evidence packet")];
        string originalHistory = JsonSerializer.Serialize(history);
        var response = await Send(synthesis, history, options, BusinessSynthesisAttemptStrategy.Configured);
        Assert.Equal("controlled-推理\nexact", Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<TextReasoningContent>()).Text);
        var correctionHistory = history.Concat(response.Messages).Append(new(ChatRole.User, "Structural correction only")).ToArray();
        string originalCorrectionHistory = JsonSerializer.Serialize(correctionHistory);
        foreach (var strategy in new[] { BusinessSynthesisAttemptStrategy.Configured, BusinessSynthesisAttemptStrategy.ThinkingDisabled,
                     BusinessSynthesisAttemptStrategy.CompactedThinkingDisabled, BusinessSynthesisAttemptStrategy.Configured })
            await Send(synthesis, correctionHistory, options, strategy);

        Assert.Equal(5, transport.Requests.Count);
        foreach (var body in transport.Requests)
        {
            Assert.Equal(393216, body.GetProperty("max_tokens").GetInt32());
            Assert.False(body.TryGetProperty("max_completion_tokens", out _));
            Assert.Equal("json_object", body.GetProperty("response_format").GetProperty("type").GetString());
            Assert.False(body.TryGetProperty("tools", out var tools) && tools.GetArrayLength() > 0);
            Assert.False(body.TryGetProperty("stream", out var stream) && stream.GetBoolean());
            Assert.Equal("Original instructions", body.GetProperty("messages")[0].GetProperty("content").GetString());
            Assert.Equal("Exact accepted evidence packet", body.GetProperty("messages")[1].GetProperty("content").GetString());
        }
        foreach (int index in new[] { 0, 1, 4 })
        {
            Assert.Equal("low", transport.Requests[index].GetProperty("reasoning_effort").GetString());
            Assert.False(transport.Requests[index].TryGetProperty("thinking", out _));
        }
        foreach (int index in new[] { 2, 3 })
        {
            Assert.False(transport.Requests[index].TryGetProperty("reasoning_effort", out _));
            Assert.Equal("disabled", transport.Requests[index].GetProperty("thinking").GetProperty("type").GetString());
        }
        foreach (var body in transport.Requests.Skip(1))
        {
            var assistant = Assert.Single(body.GetProperty("messages").EnumerateArray().Where(message => message.GetProperty("role").GetString() == "assistant"));
            Assert.Equal("controlled-推理\nexact", assistant.GetProperty("reasoning_content").GetString());
            Assert.Equal("{\"controlled\":true}", assistant.GetProperty("content").GetString());
        }
        Assert.Equal(originalHistory, JsonSerializer.Serialize(history));
        Assert.Equal(originalCorrectionHistory, JsonSerializer.Serialize(correctionHistory));
        Assert.Same(reasoning, options.Reasoning);
        Assert.Equal(ReasoningEffort.High, options.Reasoning.Effort);
        Assert.Equal(393216, options.MaxOutputTokens);
        Assert.Empty(options.Tools!);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynthesisPort_TransportFailureHasOnePhysicalCallAndDoesNotSettleCallersLeaseAsync(bool throwTransport)
    {
        using var transport = new ControlledHttp { Fail = true, ThrowTransport = throwTransport };
        using var client = await Client(transport);
        var synthesis = Assert.IsAssignableFrom<IOctoberBusinessSynthesisClient>(client);
        await using var lease = new Lease();
        int callbacks = 0;
        await Assert.ThrowsAnyAsync<Exception>(() => synthesis.GetSynthesisResponseAsync([new(ChatRole.User, "packet")],
            new() { MaxOutputTokens = 393216, ResponseFormat = ChatResponseFormat.Json, Tools = [] }, lease,
            BusinessSynthesisAttemptStrategy.Configured, _ => callbacks++));
        Assert.Single(transport.Requests);
        Assert.Equal(1, callbacks);
        Assert.Equal(1, lease.Dispatches);
        Assert.Equal(0, lease.Settlements);
        Assert.Equal(0, lease.Disposals);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("tools")]
    [InlineData("format")]
    [InlineData("strategy")]
    [InlineData("endpoint")]
    public async Task SynthesisPort_InvalidCorrectionRefusesBeforePhysicalDispatchAsync(string invalid)
    {
        using var transport = new ControlledHttp();
        using var client = await Client(transport, deepSeek: invalid != "endpoint");
        var synthesis = Assert.IsAssignableFrom<IOctoberBusinessSynthesisClient>(client);
        var options = new ChatOptions { MaxOutputTokens = 393216, ResponseFormat = ChatResponseFormat.Json, Tools = [] };
        if (invalid == "tools") options.Tools = [AIFunctionFactory.Create(() => "must not execute", "forbidden")];
        if (invalid == "format") options.ResponseFormat = ChatResponseFormat.Text;
        var strategy = invalid == "strategy" ? (BusinessSynthesisAttemptStrategy)999 : BusinessSynthesisAttemptStrategy.ThinkingDisabled;
        await using var lease = new Lease();
        await Assert.ThrowsAsync<InvalidOperationException>(() => synthesis.GetSynthesisResponseAsync([new(ChatRole.User, "packet")], options, lease, strategy, _ => Assert.Fail("No dispatch permitted")));
        Assert.Empty(transport.Requests);
        Assert.Equal(0, lease.Dispatches);
    }

    private static async Task<ChatResponse> Send(IOctoberBusinessSynthesisClient client, IEnumerable<ChatMessage> messages,
        ChatOptions options, BusinessSynthesisAttemptStrategy strategy)
    {
        await using var lease = new Lease();
        int callbacks = 0;
        var response = await client.GetSynthesisResponseAsync(messages, options, lease, strategy, _ => callbacks++);
        Assert.Equal(1, callbacks);
        Assert.Equal(1, lease.Dispatches);
        Assert.Equal(0, lease.Settlements);
        Assert.Equal(0, lease.Disposals);
        return response;
    }

    private static Task<IChatClient> Client(HttpMessageHandler transport, bool deepSeek = true) =>
        new OctoberWorkerClientFactory(new Secret(), transport, deepSeekWorkerReasoningEffort: deepSeek ? "low" : null).CreateAsync(
            new("controlled-source", "only-source", new("selected-profile", ApiType.ChatCompletions,
                deepSeek ? "https://api.deepseek.com" : "https://never-contacted.invalid", "deepseek-flash", "credential://controlled"), "synthetic-protected"));

    private sealed class Secret : ISecretEncryptionService
    {
        public Task<string> EncryptAsync(string purpose, string plaintext, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> DecryptAsync(string purpose, string ciphertext, CancellationToken cancellationToken = default)
        {
            Assert.Equal("Vulgata.LlmProviders.ApiKey", purpose);
            Assert.Equal("synthetic-protected", ciphertext);
            return Task.FromResult("synthetic-token");
        }
    }

    private sealed class Lease : IOctoberProviderAttemptLease, IOctoberWorkerDispatchObserver
    {
        internal int Dispatches, Settlements, Disposals;
        public Guid AttemptId { get; } = Guid.NewGuid();
        public CancellationToken CancellationToken => CancellationToken.None;
        public Task<ProviderAdmissionStatus> CheckBeforeSendAsync(CancellationToken cancellationToken = default) => Task.FromResult(ProviderAdmissionStatus.Granted);
        public void RecordDispatch() => Dispatches++;
        public void CompleteDispatchObservation() { }
        public Task SettleAsync(ProviderAttemptUsage usage, CancellationToken cancellationToken = default) { Settlements++; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }

    private sealed class ControlledHttp : HttpMessageHandler
    {
        internal bool Fail, ThrowTransport, ToolFirst;
        internal List<JsonElement> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Requests.Add(body.RootElement.Clone());
            if (ThrowTransport) throw new HttpRequestException("Controlled physical failure");
            if (Fail) return new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{\"error\":{\"message\":\"controlled\"}}", Encoding.UTF8, "application/json") };
            if (ToolFirst && Requests.Count == 1)
            {
                string tool = JsonSerializer.Serialize(new { id = "controlled-source-tool", @object = "chat.completion", created = 1, model = "deepseek-flash",
                    choices = new[] { new { index = 0, message = new { role = "assistant", content = "", reasoning_content = "controlled-推理\nexact",
                        tool_calls = new[] { new { id = "controlled-source", type = "function", function = new { name = "read_referenced_source", arguments = "{\"sourceReference\":\"controlled-handle\",\"byteOffset\":0}" } } } }, finish_reason = "tool_calls" } },
                    usage = new { prompt_tokens = 20, completion_tokens = 10, total_tokens = 30 } });
                return new(HttpStatusCode.OK) { Content = new StringContent(tool, Encoding.UTF8, "application/json") };
            }
            string json = JsonSerializer.Serialize(new { id = "controlled-synthesis", @object = "chat.completion", created = 1, model = "deepseek-flash",
                choices = new[] { new { index = 0, message = new { role = "assistant", content = "{\"controlled\":true}", reasoning_content = "controlled-推理\nexact" }, finish_reason = "stop" } },
                usage = new { prompt_tokens = 20, completion_tokens = 10, total_tokens = 30 } });
            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
