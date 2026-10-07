using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Shared.OctoberChat;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Services.OctoberChat;
using Vulgata.Web.ViewModels.OctoberChat;

namespace Vulgata.Tests.October.Chat;

/// <summary>Real Business SQLite receipts; controlled host authority and subscriber timing, not browser or runtime qualification.</summary>
public sealed class OctoberChatPresentationTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeDependencyStatus.Denied)]
    [InlineData(KnowledgeDependencyStatus.AuthorityUnavailable)]
    public async Task Delivery_RechecksCompleteUncitedUnionAfterDequeueAsync(KnowledgeDependencyStatus refusal)
    {
        await using Fixture f = await Fixture.OpenAsync();
        var started = Signal();
        var release = Signal();
        var delivered = new List<OctoberChatEvent>();
        KnowledgeDependencyHandle union = f.Owner.Dependencies.Extend(f.Owner.Root, "uncited-search-dependency");
        await using OctoberChatTurnPresentation attachment = f.Attach(async (item, _) =>
        {
            delivered.Add(item);
            if (item.Kind == OctoberChatEventKind.TurnStarted) { started.TrySetResult(); await release.Task; }
        });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(attachment.TryPublishText("不能泄露的排队正文", union));
            f.Owner.Dependencies.Status = refusal;
            release.TrySetResult();
            await attachment.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(attachment.IsDetached);
            Assert.DoesNotContain(delivered, item => item.Kind == OctoberChatEventKind.AnswerDelta);
            Assert.Contains(f.Dependencies.Checks, caps => caps.Length == 1 && caps[0] == union);
            Assert.False(attachment.TryPublishText("不得重新附加", union));
        }
        finally { release.TrySetResult(); attachment.Detach(); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Delivery_ContextChangesWhileAuthorityAwaitsSuppressLateHandoffAsync()
    {
        await using Fixture f = await Fixture.OpenAsync();
        var authorizing = Signal();
        var release = Signal();
        f.Dependencies.Check = async (selection, caps, token) =>
        {
            authorizing.TrySetResult();
            await release.Task; // Deliberately ignores cancellation; context must still be rechecked.
            return await f.Owner.Dependencies.ReauthorizeAsync(selection, caps, CancellationToken.None);
        };
        var delivered = new List<OctoberChatEvent>();
        await using OctoberChatTurnPresentation attachment = f.Attach((item, _) => { delivered.Add(item); return ValueTask.CompletedTask; });
        try
        {
            await authorizing.Task.WaitAsync(TimeSpan.FromSeconds(10));
            f.Context.Current = f.Context.Current! with { Subject = f.Owner.Subject with { AuthenticationBinding = "replacement-authentication" } };
            release.TrySetResult();
            await attachment.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(attachment.IsDetached);
            Assert.Empty(delivered);
        }
        finally { release.TrySetResult(); attachment.Detach(); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeDependencyStatus.Denied)]
    [InlineData(KnowledgeDependencyStatus.AuthorityUnavailable)]
    public async Task Delivery_RevokedQueuedDeltaSuppressesDisplayedPrefixWithoutClaimingSavedAsync(KnowledgeDependencyStatus refusal)
    {
        await using Fixture f = await Fixture.OpenAsync();
        var prefixSeen = Signal();
        var release = Signal();
        var delivered = new List<OctoberChatEvent>();
        var model = new OctoberChatTurnViewModel(f.Binding);
        OctoberChatTurnPresentation? attachment = null;
        attachment = f.Attach(async (item, token) =>
        {
            token.ThrowIfCancellationRequested();
            if (attachment?.IsDetached == true) return;
            delivered.Add(item);
            Assert.True(model.Apply(item));
            if (item.Kind == OctoberChatEventKind.AnswerDelta)
            {
                prefixSeen.TrySetResult();
                await release.Task;
            }
        });
        await using (attachment)
        {
            try
            {
                Assert.True(attachment.TryPublishText("撤销前已经显示的正文", f.Owner.Root));
                await prefixSeen.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal("撤销前已经显示的正文", model.AnswerText);
                Assert.True(attachment.TryPublishText("撤销后不得显示的排队正文", f.Owner.Root));
                f.Owner.Dependencies.Status = refusal;
                release.TrySetResult();
                await attachment.Completion.WaitAsync(TimeSpan.FromSeconds(10));

                OctoberChatEvent suppressed = Assert.Single(delivered.Where(item => item.Kind == OctoberChatEventKind.DisclosureSuppressed));
                Assert.True(string.IsNullOrEmpty(suppressed.Text));
                Assert.True(suppressed.References.IsDefaultOrEmpty);
                Assert.Null(suppressed.SavedOutcome);
                Assert.Single(delivered.Where(item => item.Kind == OctoberChatEventKind.AnswerDelta));
                Assert.DoesNotContain(delivered, item => item.Kind is OctoberChatEventKind.TurnSaved or OctoberChatEventKind.TurnSaveUncertain);
                Assert.Equal(string.Empty, model.AnswerText);
                Assert.Empty(model.References);
                Assert.False(model.IsTerminal);
                Assert.False(model.IsSaveUncertain);
                Assert.True(attachment.IsDetached);
                Assert.Equal(f.Binding.TurnId, (await f.Owner.OpenAsync()).Conversation!.ActiveTurnId);

                Assert.False(model.Apply(suppressed with { Kind = OctoberChatEventKind.AnswerDelta,
                    Sequence = suppressed.Sequence + 1, Text = "迟到旧正文" }));
                var document = f.Owner.Document;
                var reference = new OctoberChatReference(new(document.BatchId, f.Binding.Request.Publication.PublicationId,
                    document.LogicalDocumentId, document.DocumentVersionId), true, 1, 100);
                Assert.False(model.Apply(suppressed with { Kind = OctoberChatEventKind.ReferencesUpdated,
                    Sequence = suppressed.Sequence + 2, References = [reference] }));
                Assert.Equal(string.Empty, model.AnswerText);
                Assert.Empty(model.References);
            }
            finally { release.TrySetResult(); attachment.Detach(); }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Delivery_OverflowDetachesWithoutWaitingForUncooperativeSubscriberAsync()
    {
        await using Fixture f = await Fixture.OpenAsync();
        var entered = Signal();
        var release = Signal();
        int delivered = 0;
        await using OctoberChatTurnPresentation attachment = f.Attach(async (_, _) =>
        {
            delivered++;
            entered.TrySetResult();
            await release.Task;
        }, capacity: 2);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            for (int i = 0; i < 4 && !attachment.Overflowed; i++) attachment.TryPublishText("有限队列", f.Owner.Root);
            Assert.True(attachment.Overflowed);
            Assert.True(attachment.IsDetached);
            Assert.False(attachment.Completion.IsCompleted);
            Assert.False(attachment.TryPublishText("不再接收", f.Owner.Root));
            Assert.Equal(1, delivered);
        }
        finally { release.TrySetResult(); attachment.Detach(); }
        await attachment.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, delivered);
        Assert.Equal(0, attachment.RetainedBytes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Delivery_ByteBoundIncludesActiveItemAndProtectedCapsuleWithSpareCountAsync()
    {
        await using Fixture f = await Fixture.OpenAsync();
        const long byteLimit = 4096;
        var entered = Signal();
        var release = Signal();
        int delivered = 0;
        await using OctoberChatTurnPresentation attachment = f.Attach(async (_, _) =>
        {
            delivered++;
            entered.TrySetResult();
            await release.Task;
        }, capacity: 64, maximumRetainedBytes: byteLimit);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            long activeBytes = attachment.RetainedBytes;
            Assert.True(activeBytes >= sizeof(char) * (f.Owner.Root.ProtectedPayload.Length + f.Owner.Root.BindingDigest.Length));
            Assert.InRange(activeBytes, 1, byteLimit - 1);
            // Its text alone exceeds the remaining retained budget, with 63 count slots unused.
            string pending = new('界', checked((int)((byteLimit - activeBytes) / sizeof(char) + 1)));
            Assert.False(attachment.TryPublishText(pending, f.Owner.Root));
            Assert.True(attachment.Overflowed);
            Assert.True(attachment.IsDetached);
            Assert.False(attachment.Completion.IsCompleted);
            Assert.Equal(activeBytes, attachment.RetainedBytes);
            Assert.Equal(1, delivered);
        }
        finally { release.TrySetResult(); attachment.Detach(); }
        await attachment.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, attachment.RetainedBytes);
        Assert.Equal(1, delivered);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Delivery_SubscriberThrowDoesNotEscapePublisherOrPermitReattachmentAsync()
    {
        await using Fixture f = await Fixture.OpenAsync();
        await using OctoberChatTurnPresentation attachment = f.Attach((_, _) => throw new InvalidOperationException("Controlled subscriber failure."));
        await attachment.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(attachment.IsDetached);
        Assert.False(attachment.TryPublishText("不会抛回执行线程", f.Owner.Root));
        Assert.False(attachment.TryPublishProgress(new(Guid.NewGuid(), Vulgata.Agents.ChatExecution.October.OctoberChatToolProgressKind.Started), f.Owner.Root));
        Assert.Equal(0, attachment.RetainedBytes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Delivery_OnlyExactConfirmedOwnerProjectionReplacesProvisionalTextOnceAsync()
    {
        await using Fixture f = await Fixture.OpenAsync();
        var uncertainSeen = Signal();
        var started = Signal();
        var delivered = new List<OctoberChatEvent>();
        var model = new OctoberChatTurnViewModel(f.Binding);
        await using OctoberChatTurnPresentation attachment = f.Attach((item, token) =>
        {
            token.ThrowIfCancellationRequested();
            delivered.Add(item);
            Assert.True(model.Apply(item));
            if (item.Kind == OctoberChatEventKind.TurnStarted) started.TrySetResult();
            if (item.Kind == OctoberChatEventKind.TurnSaveUncertain) uncertainSeen.TrySetResult();
            return ValueTask.CompletedTask;
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(attachment.TryPublishText("临时前缀", f.Owner.Root));
        Assert.True(attachment.TryPublishResult(new(f.Begin, DialogueTurnState.SaveIndeterminate,
            new(DialogueStatus.Indeterminate, null, null), null)));
        await uncertainSeen.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(model.IsTerminal);
        Assert.DoesNotContain(delivered, item => item.Kind == OctoberChatEventKind.TurnSaved);

        DialogueTerminalResult saved = await f.SaveAsync();
        var confirmed = new OctoberChatCoordinationResult(f.Begin, DialogueTurnState.Completed, saved, null);
        Assert.False(attachment.TryPublishResult(confirmed with { Terminal = saved with { Receipt = saved.Receipt! with { Fence = saved.Receipt.Fence + 1 } } }));
        Assert.False(attachment.TryPublishResult(confirmed with { Terminal = saved with { Projection = null } }));
        Assert.True(attachment.TryPublishResult(confirmed));
        Assert.False(attachment.TryPublishResult(confirmed));
        await attachment.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(delivered.Where(item => item.Kind == OctoberChatEventKind.TurnSaved));
        Assert.True(model.IsTerminal);
        Assert.Equal(saved.Projection!.AnswerText, model.AnswerText);
        Assert.Equal(saved.Projection.References, model.References);
        Assert.DoesNotContain("临时前缀", model.AnswerText, StringComparison.Ordinal);

        // A separate real owner turn establishes the exceptional null-projection closure.
        await using Fixture revoked = await Fixture.OpenAsync();
        var prefixSeen = Signal();
        var revokedModel = new OctoberChatTurnViewModel(revoked.Binding);
        await using OctoberChatTurnPresentation revokedAttachment = revoked.Attach((item, token) =>
        {
            token.ThrowIfCancellationRequested();
            Assert.True(revokedModel.Apply(item));
            if (item.Kind == OctoberChatEventKind.AnswerDelta) prefixSeen.TrySetResult();
            return ValueTask.CompletedTask;
        });
        Assert.True(revokedAttachment.TryPublishText("撤销前的可见内容", revoked.Owner.Root));
        await prefixSeen.Task.WaitAsync(TimeSpan.FromSeconds(10));
        revoked.Owner.Dependencies.Status = KnowledgeDependencyStatus.Denied;
        DialogueTerminalResult sanitized = await revoked.SaveAsync(sanitized: true);
        Assert.Null(sanitized.Projection);
        Assert.True(revokedAttachment.TryPublishResult(new(revoked.Begin, DialogueTurnState.Failed, sanitized, null)));
        await revokedAttachment.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(revokedModel.IsTerminal);
        Assert.Equal(string.Empty, revokedModel.AnswerText);
        Assert.Empty(revokedModel.References);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(DialoguePersistenceFixture owner, DialogueAdmission running)
        {
            Owner = owner;
            Begin = new(DialogueStatus.Succeeded, running with { State = DialogueTurnState.Admitted }, true);
            Context = new(new(owner.Identity.Publication, owner.Subject, owner.Identity.Mode, KnowledgePurpose.Chat, owner.Identity.RepositoryScope));
            Binding = new(owner.Identity.ConversationId, owner.Identity.ContextId, running.TurnId, Context.Current!, new("controlled-presentation-selection"));
            Dependencies = new(owner.Dependencies);
        }
        public DialoguePersistenceFixture Owner { get; }
        public DialogueBeginResult Begin { get; }
        public OctoberChatTurnBinding Binding { get; }
        public MutableContext Context { get; }
        public ObservedDependencies Dependencies { get; }
        public Guid SaveId { get; } = Guid.NewGuid();
        public static async Task<Fixture> OpenAsync()
        {
            DialoguePersistenceFixture owner = await DialoguePersistenceFixture.CreateAsync();
            try { return new(owner, await owner.AdmitAsync()); }
            catch { await owner.DisposeAsync(); throw; }
        }
        public OctoberChatTurnPresentation Attach(Func<OctoberChatEvent, CancellationToken, ValueTask> deliver, int capacity = 64,
            long maximumRetainedBytes = OctoberChatTurnPresentation.DefaultMaximumRetainedBytes) =>
            new(Binding, Begin.Admission!, SaveId, Context, Dependencies, deliver, Owner.Clock, capacity, maximumRetainedBytes);
        public async Task<DialogueTerminalResult> SaveAsync(bool sanitized = false)
        {
            DialogueAdmission admission = Begin.Admission!;
            if (!sanitized)
            {
                DialogueTransitionResult saving = await Owner.Store.TransitionTurnAsync(new(new(Binding.ConversationId,
                    Binding.ContextId, Binding.TurnId, admission.Request.Execution, admission.Fence), DialogueTurnState.Running, DialogueTurnState.Saving), Owner.Subject);
                Assert.Equal(DialogueStatus.Succeeded, saving.Status);
            }
            var codec = new OctoberDialoguePayloadCodec();
            DialogueTerminalSaveRequest request = Owner.TerminalRequest(admission) with { SaveOperationId = SaveId };
            if (sanitized) request = request with { TerminalState = DialogueTerminalState.Failed,
                Reason = DialogueTerminalReason.AuthorizationDenied, AnswerText = string.Empty,
                References = [], ConsumedReads = [], InferenceDependencies = null };
            request = request with { OutcomeDigest = codec.ComputeOutcomeDigest(request) };
            DialogueTerminalResult result = await Owner.Store.SaveTerminalAsync(request, Owner.Subject);
            Assert.Equal(DialogueStatus.Succeeded, result.Status);
            Assert.NotNull(result.Receipt);
            if (!sanitized) Assert.NotNull(result.Projection);
            return result;
        }
        public ValueTask DisposeAsync() => Owner.DisposeAsync();
    }

    private sealed class MutableContext(ReadSelectionRequest current) : IOctoberChatContextProvider
    {
        public ReadSelectionRequest? Current { get; set; } = current;
        public event Action? ContextChanged { add { } remove { } }
        public Task<ReadSelectionRequest?> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
    }

    /// <summary>Only gates and records the canonical owner port; it does not reconstruct lineage or replace owner decisions.</summary>
    private sealed class ObservedDependencies(IOctoberKnowledgeDependencyAuthority inner) : IOctoberKnowledgeDependencyAuthority
    {
        public List<ImmutableArray<KnowledgeDependencyHandle>> Checks { get; } = [];
        public Func<ReadSelectionRequest, ImmutableArray<KnowledgeDependencyHandle>, CancellationToken, Task<KnowledgeDependencyStatus>>? Check { get; set; }
        public Task<KnowledgeDependencyResult> CreateNoKnowledgeRootAsync(ReadSelectionRequest selection, string digest, CancellationToken token = default) => inner.CreateNoKnowledgeRootAsync(selection, digest, token);
        public Task<KnowledgeDependencyResult> MergeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> handles, CancellationToken token = default) => inner.MergeAsync(selection, handles, token);
        public Task<KnowledgeDependencyStatus> ReauthorizeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> handles, CancellationToken token = default)
        { Checks.Add(handles); return Check?.Invoke(selection, handles, token) ?? inner.ReauthorizeAsync(selection, handles, token); }
        public Task<KnowledgeConsumedContentResult> BuildConsumedContentAsync(ReadSelectionRequest selection, DocumentHandle document, ImmutableArray<KnowledgeDependencyHandle> pages, CancellationToken token = default) => inner.BuildConsumedContentAsync(selection, document, pages, token);
        public Task<KnowledgeConsumedContentResult> VerifyConsumedContentAsync(ReadSelectionRequest selection, KnowledgeConsumedContentHandle proof, KnowledgeDependencyHandle inference, CancellationToken token = default) => inner.VerifyConsumedContentAsync(selection, proof, inference, token);
    }
}
