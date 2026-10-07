using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities;
using Vulgata.Tests.October.Chat;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Services.OctoberChat;

namespace Vulgata.Tests.October.Integration;

public sealed class OctoberChatClientSessionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CreateAsync_ControlledSdkStreamsUsingFrozenProfileAndOriginalBudgetWithoutEagerRequestAsync()
    {
        var fixture = await Fixture.CreateAsync();
        var physical = new PhysicalHandler();
        var admission = new OctoberChatRequestAdmission(1);
        await using var session = await fixture.SessionAsync(physical, admission);
        Assert.Equal(0, physical.Sends);
        Assert.Same(fixture.Budget, session.Budget);
        Assert.Equal("controlled answer", await RequestAsync(session));
        Assert.Equal(1, physical.Sends);
        Assert.Equal("https://controlled.invalid/v1/chat/completions", physical.Uri!.AbsoluteUri);
        using var body = JsonDocument.Parse(physical.Body!);
        Assert.Equal("controlled-model", body.RootElement.GetProperty("model").GetString());
        Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal(0, admission.ActiveRequests);
        Task settlement = session.SealAndWaitForSettlementAsync();
        Assert.Same(settlement, session.SealAndWaitForSettlementAsync());
        await settlement.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, physical.Disposals);
        Assert.NotNull(await Record.ExceptionAsync(() => RequestAsync(session)));
        Assert.Equal(1, physical.Sends);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("no-dispatch")]
    [InlineData("existing")]
    [InlineData("profile")]
    [InlineData("revision")]
    [InlineData("limits")]
    [InlineData("deadline")]
    [InlineData("authority")]
    public async Task CreateAsync_InvalidAdmission_RefusesWithoutSendingAndDisposesOwnedTransportAsync(string invalid)
    {
        var fixture = await Fixture.CreateAsync();
        var admitted = fixture.Admitted;
        if (invalid == "no-dispatch") admitted = admitted with { MayDispatch = false };
        if (invalid == "existing") admitted = admitted with { Status = DialogueStatus.Existing };
        if (invalid is "profile" or "revision" or "limits")
        {
            var request = admitted.Admission!.Request;
            request = invalid switch
            {
                "profile" => request with { ProviderProfileId = Guid.NewGuid().ToString("D") },
                "revision" => request with { ProviderProfileRevision = "different-admission" },
                _ => request with { Limits = request.Limits with { ToolCalls = request.Limits.ToolCalls + 1 } }
            };
            admitted = admitted with { Admission = admitted.Admission with { Request = request } };
        }
        if (invalid == "deadline") admitted = admitted with { Admission = admitted.Admission! with { DeadlineUtc = fixture.Clock.GetUtcNow() } };
        if (invalid == "authority") fixture.Authority.Status = DialogueExecutionStatus.NotVerified;
        fixture.Admitted = admitted;
        var physical = new PhysicalHandler();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.SessionAsync(physical, new OctoberChatRequestAdmission(1)));
        Assert.Equal(0, physical.Sends);
        Assert.Equal(1, physical.Disposals);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    public async Task Client_ProviderReturnsFailureOrRedirect_DoesNotRetryOrFollowPhysicalRequestAsync(HttpStatusCode status)
    {
        var fixture = await Fixture.CreateAsync();
        var physical = new PhysicalHandler { Status = status };
        await using var session = await fixture.SessionAsync(physical, new OctoberChatRequestAdmission(1));
        Assert.NotNull(await Record.ExceptionAsync(() => RequestAsync(session)));
        Assert.Equal(1, physical.Sends);
        Assert.Equal("https://controlled.invalid/v1/chat/completions", physical.Uri!.AbsoluteUri);
        await session.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("authorization")]
    [InlineData("provider")]
    [InlineData("deadline")]
    public async Task Client_GlobalPermitWait_RechecksAdmissionAfterWaitingBeforeAnyPhysicalSendAsync(string revoked)
    {
        var admission = new OctoberChatRequestAdmission(1);
        var first = await Fixture.CreateAsync();
        var second = await Fixture.CreateAsync();
        var held = new GatedBody();
        var firstPhysical = new PhysicalHandler { BodyStream = held };
        var secondPhysical = new PhysicalHandler();
        await using var firstSession = await first.SessionAsync(firstPhysical, admission);
        await using var secondSession = await second.SessionAsync(secondPhysical, admission);
        var firstRun = RequestAsync(firstSession);
        Task<string>? secondRun = null;
        try
        {
            await held.GateEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, admission.ActiveRequests);
            secondRun = RequestAsync(secondSession);
            Assert.False(secondRun.IsCompleted);
            Assert.Equal(0, secondPhysical.Sends);
            if (revoked == "authorization") second.Reauthorize = _ => throw new UnauthorizedAccessException("Controlled revocation");
            if (revoked == "provider") second.Row.UpdateDetails(second.Row.Name, second.Row.BaseEndpointUrl, "rotated-after-queue",
                second.Row.SupportedApiTypes, second.Row.DefaultAgentType, second.Row.UpdatedAt.AddSeconds(1));
            if (revoked == "deadline") second.Clock.Advance(second.Budget.Limits.Duration + TimeSpan.FromSeconds(1));
            held.Release.TrySetResult();
            await firstRun.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(await Record.ExceptionAsync(() => secondRun.WaitAsync(TimeSpan.FromSeconds(10))));
            Assert.Equal(0, secondPhysical.Sends);
        }
        finally
        {
            held.Release.TrySetResult();
            _ = await Record.ExceptionAsync(() => firstRun.WaitAsync(TimeSpan.FromSeconds(10)));
            if (secondRun is not null) _ = await Record.ExceptionAsync(() => secondRun.WaitAsync(TimeSpan.FromSeconds(10)));
            await firstSession.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await secondSession.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(0, admission.ActiveRequests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SealAndWait_CancelledNoncooperativePhysicalRead_RetainsPermitUntilActualSettlementAsync()
    {
        // Establish the runtime's distinction between one original HttpContent disposal and
        // underlying stream callbacks: cached ReadAsStream + StreamContent disposal calls both paths.
        var controlStream = new GatedBody();
        var controlContent = new CountedContent(controlStream);
        _ = await controlContent.ReadAsStreamAsync();
        controlContent.Dispose();
        Assert.Equal(1, controlContent.Disposals);
        Assert.Equal(2, controlStream.Disposals);
        using var original = new CancellationTokenSource();
        var fixture = await Fixture.CreateAsync(original.Token);
        var body = new GatedBody();
        var physical = new PhysicalHandler { BodyStream = body };
        var admission = new OctoberChatRequestAdmission(1);
        await using var session = await fixture.SessionAsync(physical, admission);
        var run = RequestAsync(session);
        try
        {
            await body.GateEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            original.Cancel();
            var settled = session.SealAndWaitForSettlementAsync();
            Assert.Same(settled, session.SealAndWaitForSettlementAsync());
            Assert.False(settled.IsCompleted);
            Assert.Equal(1, admission.ActiveRequests);
            Assert.False(body.DisposedWhileReading);
            using var observer = new CancellationTokenSource();
            observer.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => settled.WaitAsync(observer.Token));
            Assert.Equal(1, admission.ActiveRequests);
            Assert.False(settled.IsCompleted);
        }
        finally
        {
            body.Release.TrySetResult();
            _ = await Record.ExceptionAsync(() => run.WaitAsync(TimeSpan.FromSeconds(10)));
            await session.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(0, admission.ActiveRequests);
        Assert.False(body.DisposedWhileReading);
        Assert.Equal(controlStream.Disposals, body.Disposals);
        Assert.Equal(1, physical.ResponseContent!.Disposals);
        Assert.Equal(1, physical.Disposals);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SealAndWait_PhysicalCleanupFailure_RemainsFailureAndCannotReleaseUnprovenPermitAsync()
    {
        var fixture = await Fixture.CreateAsync();
        var body = new GatedBody { FailDispose = true };
        body.Release.TrySetResult();
        var physical = new PhysicalHandler { BodyStream = body };
        var admission = new OctoberChatRequestAdmission(1);
        var session = await fixture.SessionAsync(physical, admission);
        _ = await Record.ExceptionAsync(() => RequestAsync(session));
        Task settled = session.SealAndWaitForSettlementAsync();
        Assert.NotNull(await Record.ExceptionAsync(() => settled.WaitAsync(TimeSpan.FromSeconds(10))));
        Assert.True(settled.IsFaulted, "Cleanup failure must be terminal failure, not an endlessly pending fake drain.");
        Assert.Equal(1, admission.ActiveRequests);
        Assert.Same(settled, session.SealAndWaitForSettlementAsync());
        Assert.NotNull(await Record.ExceptionAsync(() => session.DisposeAsync().AsTask()));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Client_OriginalAdmissionDeadlineExpires_DoesNotResetItForFirstRequestAsync()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Admitted = fixture.Admitted with { Admission = fixture.Admitted.Admission! with { DeadlineUtc = fixture.Clock.GetUtcNow() + TimeSpan.FromSeconds(1) } };
        var physical = new PhysicalHandler();
        await using var session = await fixture.SessionAsync(physical, new OctoberChatRequestAdmission(1));
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.NotNull(await Record.ExceptionAsync(() => RequestAsync(session)));
        Assert.Equal(0, physical.Sends);
        await session.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Client_AuthorityAwaitsThenContextRevoked_ReauthorizesAgainBeforePhysicalSendAsync()
    {
        var fixture = await Fixture.CreateAsync();
        var physical = new PhysicalHandler();
        var admission = new OctoberChatRequestAdmission(1);
        await using var session = await fixture.SessionAsync(physical, admission);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Authority.BeforeVerify = async token => { entered.TrySetResult(); await release.Task.WaitAsync(token); };
        bool allowed = true;
        fixture.Reauthorize = _ => allowed ? Task.CompletedTask : Task.FromException(new UnauthorizedAccessException("Controlled changed context."));
        var run = RequestAsync(session);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            allowed = false;
            release.TrySetResult();
            Assert.NotNull(await Record.ExceptionAsync(() => run.WaitAsync(TimeSpan.FromSeconds(10))));
            Assert.Equal(0, physical.Sends);
        }
        finally
        {
            release.TrySetResult();
            _ = await Record.ExceptionAsync(() => run.WaitAsync(TimeSpan.FromSeconds(10)));
            await session.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(0, admission.ActiveRequests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("authority", false)]
    [InlineData("authority", true)]
    [InlineData("decrypt", false)]
    [InlineData("decrypt", true)]
    public async Task CreateAsync_CooperativeReadinessWait_IsBoundByOriginalTokenAndDeadlineAsync(string stage, bool deadline)
    {
        using var original = new CancellationTokenSource();
        var fixture = await Fixture.CreateAsync(original.Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        async Task Pause(CancellationToken token)
        {
            observed = token;
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        }
        if (stage == "authority") fixture.Authority.BeforeVerify = Pause;
        else fixture.Secrets.BeforeDecrypt = Pause;
        var physical = new PhysicalHandler();
        // Factory caller token deliberately defaults: the original turn lifetime still applies.
        var pending = fixture.SessionAsync(physical, new OctoberChatRequestAdmission(1));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (deadline) fixture.Clock.Advance(fixture.Budget.Limits.Duration + TimeSpan.FromSeconds(1));
            else original.Cancel();
            var error = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.NotNull(error);
            Assert.IsNotType<TimeoutException>(error);
            Assert.True(observed.IsCancellationRequested, "Actual cooperative readiness operation must receive original cancellation/deadline.");
            Assert.Equal(0, physical.Sends);
            Assert.Equal(1, physical.Disposals);
        }
        finally
        {
            release.TrySetResult();
            var accidental = await Record.ExceptionAsync(async () =>
            {
                var session = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                await session.DisposeAsync();
            });
            _ = accidental;
        }
    }

    private static async Task<string> RequestAsync(OctoberChatClientSession session)
    {
        session.Budget.TryStartProviderRound(out _);
        var text = new StringBuilder();
        await foreach (var update in session.Client.GetStreamingResponseAsync([new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "controlled question")]))
            text.Append(update.Text);
        return text.ToString();
    }

    private sealed class Fixture
    {
        public ControlledChatFixture.ManualClock Clock { get; } = new();
        public OctoberChatBudget Budget { get; private set; } = null!;
        public OctoberChatProviderBindingService Binding { get; private set; } = null!;
        public OctoberChatProviderSnapshot Snapshot { get; private set; } = null!;
        public LlmProvider Row { get; private set; } = null!;
        public DialogueBeginResult Admitted { get; set; } = null!;
        public ExecutionAuthority Authority { get; } = new();
        public Secret Secrets { get; } = new();
        public Func<CancellationToken, Task> Reauthorize { get; set; } = _ => Task.CompletedTask;
        public static async Task<Fixture> CreateAsync(CancellationToken originalToken = default)
        {
            var fixture = new Fixture();
            var row = new LlmProvider("controlled-model", "https://controlled.invalid/v1", "controlled-protected-key", ApiType.ChatCompletions, AgentType.Chat, DateTimeOffset.UnixEpoch);
            fixture.Row = row;
            fixture.Binding = new(row.Id, new Repository(row), fixture.Secrets);
            fixture.Snapshot = (await fixture.Binding.FreezeAsync())!;
            fixture.Budget = new(timeProvider: fixture.Clock, cancellationToken: originalToken);
            var conversation = new DialogueConversationIdentity(Guid.NewGuid(), Guid.NewGuid(), "ordinary-user",
                new(new("controlled-db", "controlled-target", Guid.NewGuid(), new string('a', 64)), Guid.NewGuid()), KnowledgeMode.IT, []);
            var request = new DialogueBeginRequest(conversation.ConversationId, conversation.ContextId, Guid.NewGuid(), "controlled question",
                new string('b', 64), row.Id.ToString("D"), fixture.Snapshot.Revision, new string('c', 64), [], new string('d', 64),
                new(Guid.NewGuid(), new string('e', 64), "controlled-host-capsule"), fixture.Budget.Limits, new(Guid.NewGuid(), Guid.NewGuid()));
            fixture.Admitted = new(DialogueStatus.Succeeded, new(conversation, request, Guid.NewGuid(), 1, 1, fixture.Clock.GetUtcNow(),
                fixture.Clock.GetUtcNow() + fixture.Budget.Limits.Duration, DialogueTurnState.Admitted), true);
            return fixture;
        }
        public Task<OctoberChatClientSession> SessionAsync(PhysicalHandler transport, OctoberChatRequestAdmission admission) =>
            OctoberChatClientSession.CreateAsync(Binding, Snapshot, Admitted, Budget, Authority, token => Reauthorize(token), admission, transport, Clock);
    }

    private sealed class ExecutionAuthority : IOctoberDialogueExecutionAuthority
    {
        public DialogueExecutionStatus Status { get; set; } = DialogueExecutionStatus.Verified;
        public Func<CancellationToken, Task>? BeforeVerify { get; set; }
        public async Task<DialogueExecutionStatus> VerifyAdmissionAsync(DialogueBeginRequest request, CancellationToken cancellationToken = default)
        {
            if (BeforeVerify is not null) await BeforeVerify(cancellationToken);
            return Status;
        }
        public Task<DialogueExecutionStatus> VerifyTerminalAsync(DialogueAdmission admission, DialogueTerminalSaveRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<DialogueExecutionStatus> VerifyOwnerDeathAsync(DialogueExecutionIdentity execution, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    private sealed class Repository(LlmProvider row) : ILlmProviderRepository
    {
        public Task<LlmProvider?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<LlmProvider?>(id == row.Id ? row : null);
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> NameExistsAsync(string name, Guid? excludeProviderId = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<LlmProvider> AddAsync(LlmProvider provider, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Remove(LlmProvider provider) => throw new InvalidOperationException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
    private sealed class Secret : ISecretEncryptionService
    {
        public Func<CancellationToken, Task>? BeforeDecrypt { get; set; }
        public Task<string> EncryptAsync(string purpose, string plain, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public async Task<string> DecryptAsync(string purpose, string cipher, CancellationToken cancellationToken = default)
        {
            if (BeforeDecrypt is not null) await BeforeDecrypt(cancellationToken);
            return "controlled-dummy-key";
        }
    }

    private sealed class PhysicalHandler : HttpMessageHandler
    {
        public int Sends { get; private set; }
        public int Disposals { get; private set; }
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public Uri? Uri { get; private set; }
        public string? Body { get; private set; }
        public Stream? BodyStream { get; init; }
        public CountedContent? ResponseContent { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sends++;
            Uri = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            ResponseContent = new CountedContent(BodyStream ?? new MemoryStream(Encoding.UTF8.GetBytes(Payload)));
            var response = new HttpResponseMessage(Status) { Content = ResponseContent };
            if (Status is HttpStatusCode.Found or HttpStatusCode.TemporaryRedirect)
                response.Headers.Location = new Uri("https://alternate-controlled.invalid/forbidden");
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return response;
        }
        protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
    }

    private sealed class CountedContent(Stream stream) : StreamContent(stream)
    {
        public int Disposals { get; private set; }
        protected override void Dispose(bool disposing)
        {
            if (disposing) Disposals++;
            base.Dispose(disposing);
        }
    }

    private const string Payload = "data: {\"id\":\"controlled-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"controlled-model\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"controlled answer\"},\"finish_reason\":null}]}\n\ndata: {\"id\":\"controlled-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"controlled-model\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";

    private sealed class GatedBody : MemoryStream
    {
        private bool _reading;
        public GatedBody() : base(Encoding.UTF8.GetBytes(Payload)) { }
        public TaskCompletionSource GateEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool FailDispose { get; init; }
        public int Disposals { get; private set; }
        public bool DisposedWhileReading { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _reading = true;
            try
            {
                if (Position == Length)
                {
                    GateEntered.TrySetResult();
                    await Release.Task; // Deliberately noncooperative; only the owned fixture releases physical I/O.
                    return 0;
                }
                return base.Read(buffer.Span);
            }
            finally { _reading = false; }
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
                DisposedWhileReading |= _reading;
                base.Dispose(disposing);
                if (FailDispose) throw new IOException("Controlled physical disposal failure.");
            }
            else base.Dispose(disposing);
        }
    }
}
