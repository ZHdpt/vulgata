using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Tests.PreparedPass1.Fixtures;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T074 RED slice: the prepared Worker prompt and Agent composition are one
/// closed public contract.  These tests load the prompt through the real
/// dispatcher and inspect only the production function seams; they never call
/// a provider, host, database, or analysis tool.
/// </summary>
public sealed class Pass1DeepSeekPromptCompositionTests
    : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private const string PromptResourceName =
        "Vulgata.Agents.Prompts.pass1-code-logic-worker.v2.md";

    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public Pass1DeepSeekPromptCompositionTests(
        LoginLogoutTests.CustomWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task PreparedDispatcher_UsesOneEmbeddedPromptWithAuthoritativeSubmissionGuidanceAsync()
    {
        RecordingExecutor executor = new();
        PreparedPass1FileCodeUnitAssignment assignment = CreateAssignment();
        var dispatcher = new PreparedPass1WorkerDispatcher(executor);

        PreparedPass1DispatchResult result = await dispatcher.DispatchAsync(
            new(
                [assignment],
                [CreateContext(assignment)],
                [],
                1));

        Assert.Single(result.Results);
        string prompt = Assert.Single(executor.Prompts);
        Assembly agentsAssembly = typeof(PreparedPass1WorkerDispatcher).Assembly;
        string[] resources = agentsAssembly.GetManifestResourceNames()
            .Where(name => name.EndsWith("pass1-code-logic-worker.v2.md", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal([PromptResourceName], resources);
        using Stream stream = agentsAssembly.GetManifestResourceStream(PromptResourceName)
            ?? throw new Xunit.Sdk.XunitException("The authoritative Worker prompt resource is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        Assert.Equal(reader.ReadToEnd(), prompt);

        Assert.Contains("code-logic/file-v2", prompt, StringComparison.Ordinal);
        Assert.Contains("schemaIdentity", prompt, StringComparison.Ordinal);
        Assert.Contains("binding", prompt, StringComparison.Ordinal);
        Assert.Contains("sourceAnchors", prompt, StringComparison.Ordinal);
        Assert.Contains("communicationEvidence", prompt, StringComparison.Ordinal);
        Assert.Contains("deferredObservations", prompt, StringComparison.Ordinal);
        Assert.Contains("navigate_code", prompt, StringComparison.Ordinal);
        Assert.Contains("submit_code_logic", prompt, StringComparison.Ordinal);
        Assert.Contains("finalization", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("free-form", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never authoritative", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("single registered `navigate_code`", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreparedDispatcher_Prompt_BindsSoleSubmissionAndPreflightedNavigationBatchRulesAsync()
    {
        RecordingExecutor executor = new();
        PreparedPass1FileCodeUnitAssignment assignment = CreateAssignment();
        var dispatcher = new PreparedPass1WorkerDispatcher(executor);

        await dispatcher.DispatchAsync(
            new(
                [assignment],
                [CreateContext(assignment)],
                [],
                1));

        string prompt = Assert.Single(executor.Prompts);

        // T101 defines the public prompt contract for the AD-21 provider-turn
        // shape. These are semantic markers, not implementation details: one
        // submission, an all-navigation batch only after complete preflight,
        // and the server-owned durable budget must be visible to the Worker.
        Assert.Contains("exactly one", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sole `submit_code_logic`", prompt, StringComparison.Ordinal);
        Assert.Contains("all-`navigate_code` batch", prompt, StringComparison.Ordinal);
        Assert.Contains("preflight", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("durable navigation-attempt budget", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not claim API zero-retention or training guarantees.", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("zero-retention is guaranteed", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreparedDispatcher_PromptContainsOneAuthoritativeTerminalMarkerSetAsync()
    {
        RecordingExecutor executor = new();
        PreparedPass1FileCodeUnitAssignment assignment = CreateAssignment();
        var dispatcher = new PreparedPass1WorkerDispatcher(executor);

        await dispatcher.DispatchAsync(
            new(
                [assignment],
                [CreateContext(assignment)],
                [],
                1));

        string prompt = Assert.Single(executor.Prompts);
        string[] markers =
        [
            "<!-- PASS1_TERMINAL_PREAMBLE_BEGIN -->",
            "<!-- PASS1_TERMINAL_PREAMBLE_END -->",
            "<!-- PASS1_TERMINAL_COMMON_BEGIN -->",
            "<!-- PASS1_TERMINAL_COMMON_END -->",
        ];
        Assert.All(markers, marker => Assert.Equal(1, Count(prompt, marker)));
        Assert.DoesNotContain(
            "navigate_code",
            ExtractMarkerInterior(prompt, markers[0], markers[1]) +
            ExtractMarkerInterior(prompt, markers[2], markers[3]),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        "InvalidShape",
        "UseMinimalValidDocument",
        "smallest truthful closed document")]
    [InlineData(
        "InvalidSemantics",
        "UseMinimalValidDocument",
        "smallest truthful locally valid document")]
    [InlineData(
        "InvalidBinding",
        "CopyPacketBinding",
        "packet binding property-for-property")]
    [InlineData(
        "InvalidEvidence",
        "UseAuthorizedFullRangeAnchor",
        "full-range `a001` anchor")]
    public async Task PreparedDispatcher_TerminalPromptDefinesEachClosedRepairCodeAsync(
        string category,
        string repairCode,
        string instruction)
    {
        RecordingExecutor executor = new();
        PreparedPass1FileCodeUnitAssignment assignment = CreateAssignment();
        var dispatcher = new PreparedPass1WorkerDispatcher(executor);

        await dispatcher.DispatchAsync(
            new(
                [assignment],
                [CreateContext(assignment)],
                [],
                1));

        string prompt = Assert.Single(executor.Prompts);
        Assert.Contains(category, prompt, StringComparison.Ordinal);
        Assert.Contains(repairCode, prompt, StringComparison.Ordinal);
        Assert.Contains(instruction, prompt, StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string text, string value) =>
        text.Split(value, StringSplitOptions.None).Length - 1;

    private static string ExtractMarkerInterior(string text, string begin, string end)
    {
        int start = text.IndexOf(begin, StringComparison.Ordinal);
        int finish = text.IndexOf(end, StringComparison.Ordinal);
        return start < 0 || finish <= start
            ? string.Empty
            : text[(start + begin.Length)..finish];
    }

    [Fact]
    public void PreparedExecutor_ProductionCompositionExposesExactlyTwoClosedFunctions()
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(PreparedPass1JobExecutor).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Type[] functionParameters = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Where(parameter => parameter == typeof(PreparedNavigateCodeFunction) ||
                                parameter == typeof(PreparedSubmitCodeLogicFunction))
            .ToArray();

        Assert.Equal(
            [typeof(PreparedNavigateCodeFunction), typeof(PreparedSubmitCodeLogicFunction)],
            functionParameters);

        PreparedPass1WorkerPacket packet = CreatePacket();
        var navigation = new NoOpNavigationService();
        AIFunction navigate = new PreparedNavigateCodeFunction(navigation).Create(
            new(
                packet.JobId,
                1,
                packet.FileCodeUnitId));
        AIFunction submit = new PreparedSubmitCodeLogicFunction().CreateSession(packet).Function;

        Assert.Equal("navigate_code", navigate.Name);
        Assert.Equal("submit_code_logic", submit.Name);
        Assert.NotEqual(navigate.Name, submit.Name);
    }

    [Fact]
    public async Task ProductionWebScope_ResolvesPreparedExecutorAndBothClosedFunctionFactoriesAsync()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        Assert.IsType<PreparedPass1JobExecutor>(
            scope.ServiceProvider.GetRequiredService<IPreparedPass1JobExecutor>());
        Assert.IsType<PreparedNavigateCodeFunction>(
            scope.ServiceProvider.GetRequiredService<PreparedNavigateCodeFunction>());
        Assert.IsType<PreparedSubmitCodeLogicFunction>(
            scope.ServiceProvider.GetRequiredService<PreparedSubmitCodeLogicFunction>());
    }

    [Fact]
    public async Task ProductionStyleScope_NoSubmission_ReportsMissingSubmissionThroughSameObserverAsync()
    {
        NoSubmissionStreamingHandler transport = new();
        ServiceCollection services = new();
        services.AddScoped(_ => CreateResolver());
        services.AddScoped<ISecretEncryptionService>(_ => new PlaintextEncryptionService());
        services.AddScoped(_ => new PreparedNavigateCodeFunction(new NoOpNavigationService()));
        services.AddScoped<HttpMessageHandler>(_ => transport);
        services.AddScoped<PreparedPass1SubmissionDiagnosticObserver>();
        services.AddScoped<IPreparedPass1SubmissionDiagnosticObserver>(provider =>
            provider.GetRequiredService<PreparedPass1SubmissionDiagnosticObserver>());
        services.AddScoped<PreparedSubmitCodeLogicFunction>(provider =>
            new PreparedSubmitCodeLogicFunction(
                provider.GetRequiredService<IPreparedPass1SubmissionDiagnosticObserver>()));
        services.AddScoped<IPreparedPass1JobExecutor, PreparedPass1JobExecutor>();

        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        PreparedPass1SubmissionDiagnosticObserver observer = scope.ServiceProvider
            .GetRequiredService<PreparedPass1SubmissionDiagnosticObserver>();
        Assert.Same(
            observer,
            scope.ServiceProvider.GetRequiredService<IPreparedPass1SubmissionDiagnosticObserver>());

        PreparedPass1WorkerPacket packet = CreatePacket();
        packet = packet with
        {
            ServerAuthorization = new PreparedPass1ServerAuthorization(
                packet.PairExecutionId,
                1,
                1,
                Guid.Parse("70000000-0000-4000-8000-000000000007"),
                DateTimeOffset.UtcNow.AddMinutes(5),
                nameof(Pass1PairExecutionPhase.Running),
                Pass1CircuitState.Closed,
                ScanJobExecutionState.Running,
                PreparedPass1AuthorizationBinding.Create(new(
                    Guid.Parse("80000000-0000-4000-8000-000000000008"),
                    packet.SystemId,
                    [packet.RepositoryId])))
        };
        PreparedPass1RawWorkerResult result = await scope.ServiceProvider
            .GetRequiredService<IPreparedPass1JobExecutor>()
            .ExecuteAsync(packet, "Return only through submit_code_logic.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("worker-result-rejected", result.ReasonCode);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Equal(
            [PreparedPass1SubmissionRejectionKind.MissingSubmission],
            observer.Categories);
        Assert.Equal(1, transport.RequestCount);
    }

    private static PreparedPass1FileCodeUnitAssignment CreateAssignment()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        return new(
            packet.PairExecutionId, packet.JobId, packet.ScanExecutionId, packet.SystemId,
            packet.RepositoryId, packet.ScanRunId, packet.ProfiledRevision, packet.FileCodeUnitId,
            packet.FileCodeUnitIdentityFingerprint, packet.PreparedScanManifestId,
            packet.PreparedScanManifestFingerprint, packet.PairPackageFingerprint,
            packet.RepositoryRelativePath, packet.ByteStart, packet.ByteEnd, packet.LineStart,
            packet.LineEnd, packet.SegmentOrdinal, packet.SegmentCount, packet.FileHash,
            packet.SourceHash, packet.PromptIdentity, packet.DocumentSchemaIdentity,
            packet.AnalysisRuntimeIdentityHash, packet.ContextSnapshot.ContextSnapshotId,
            packet.ManifestOrdinal, packet.PairSelectionOrdinal, packet.SourceUtf8);
    }

    private static PreparedPass1WorkerPacket CreatePacket()
    {
        ApprovedPreparedManifestPairSeed pair = PreparedPass1ManifestPairFixtures.CreateApprovedPair();
        PreparedManifestSeed manifest = pair.Manifests[0];
        PreparedFileCodeUnitSeed file = manifest.FileCodeUnits[0];
        Guid systemId = Guid.Parse("50000000-0000-4000-8000-000000000005");
        Guid pairExecutionId = Guid.Parse("60000000-0000-4000-8000-000000000006");
        Guid scanExecutionId = GuidFromHash($"execution:{manifest.RepositoryId:D}");
        Guid manifestId = GuidFromHash($"manifest:{manifest.RepositoryId:D}");
        Guid contextId = GuidFromHash($"context:{manifest.RepositoryId:D}");
        Guid fileCodeUnitId = GuidFromHash($"unit:{manifest.RepositoryId:D}:{file.RelativePath}");
        Guid jobId = ScanJobExecution.Create(
            scanExecutionId, manifest.RepositoryId, manifest.ScanRunId, fileCodeUnitId,
            file.ManifestOrdinal, DateTimeOffset.UnixEpoch).Id;
        byte[] source = Encoding.UTF8.GetBytes(file.Source);
        string contextHash = Hash($"context:{manifest.RepositoryId:D}");
        return new(
            pairExecutionId, jobId, scanExecutionId, systemId, manifest.RepositoryId,
            manifest.ScanRunId, manifest.ProfiledRevision, fileCodeUnitId, file.IdentityFingerprint,
            manifestId, manifest.ManifestFingerprint, pair.PairFingerprint, file.RelativePath,
            0, source.Length, 1, Math.Max(1, file.Source.Count(value => value == '\n') + 1),
            1, 1, file.FileHash, file.SourceHash, "story-12-3-prompt-composition",
            "code-logic/file-v2", Hash("runtime:story-12-3"), file.ManifestOrdinal,
            file.PairSelectionOrdinal, source,
            new(contextId, manifest.RepositoryId, manifest.ScanRunId, manifest.ProfiledRevision,
                contextHash, contextHash, null, null, null), []);
    }

    private static PreparedPass1ContextSnapshotProjection CreateContext(
        PreparedPass1FileCodeUnitAssignment assignment) =>
        new(assignment.ContextSnapshotId, assignment.RepositoryId, assignment.ScanRunId,
            assignment.ProfiledRevision, Hash("context"), Hash("context"), null, null, null);

    private static Guid GuidFromHash(string value) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static WorkerAgentProviderResolver CreateResolver()
    {
        LlmProvider provider = new(
            "deepseek-v4-flash",
            "https://deepseek.test/v1",
            "ciphertext",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        return new(new NoOverrideRepository(), new SingleProviderRepository(provider));
    }

    private sealed class RecordingExecutor : IPreparedPass1JobExecutor
    {
        public ConcurrentQueue<string> Prompts { get; } = new();

        public Task<PreparedPass1RawWorkerResult> ExecuteAsync(
            PreparedPass1WorkerPacket packet,
            string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            Prompts.Enqueue(systemPrompt);
            return Task.FromResult(new PreparedPass1RawWorkerResult(
                PreparedPass1RawWorkerOutcome.Rejected,
                ReadOnlyMemory<byte>.Empty,
                "worker-result-rejected"));
        }
    }

    private sealed class NoOpNavigationService : IPass1NavigationService
    {
        public Task<PreparedPass1NavigationResponse> NavigateAsync(
            PreparedPass1NavigationRequestInput request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The prompt composition test must not invoke navigation.");
    }

    private sealed class NoSubmissionStreamingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            const string events =
                "data: {\"id\":\"no-submit\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"not authoritative\"},\"finish_reason\":\"stop\"}]}\n\n" +
                "data: [DONE]\n\n";
            ByteArrayContent content = new(Encoding.UTF8.GetBytes(events));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class NoOverrideRepository : ISystemLlmProviderOverrideRepository
    {
        public Task<SystemLlmProviderOverride?> GetBySystemAndAgentTypeAsync(
            Guid systemId,
            AgentType agentType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemLlmProviderOverride?>(null);

        public Task<IReadOnlyList<SystemLlmProviderOverride>> ListBySystemAsync(
            Guid systemId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SystemLlmProviderOverride>>([]);

        public Task<SystemLlmProviderOverride?> GetBySystemAndIdAsync(
            Guid systemId,
            Guid overrideId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemLlmProviderOverride?>(null);

        public Task<SystemLlmProviderOverride> AddAsync(
            SystemLlmProviderOverride overrideEntry,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveAsync(
            Guid systemId,
            Guid overrideId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SingleProviderRepository(LlmProvider provider) : ILlmProviderRepository
    {
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LlmProvider>>([provider]);

        public Task<LlmProvider?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<LlmProvider?>(id == provider.Id ? provider : null);

        public Task<bool> NameExistsAsync(
            string name,
            Guid? excludeProviderId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LlmProvider> AddAsync(
            LlmProvider value,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Remove(LlmProvider value) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class PlaintextEncryptionService : ISecretEncryptionService
    {
        public Task<string> EncryptAsync(
            string purpose,
            string plain,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string> DecryptAsync(
            string purpose,
            string cipher,
            CancellationToken cancellationToken = default) =>
            Task.FromResult("test-api-key");
    }
}
