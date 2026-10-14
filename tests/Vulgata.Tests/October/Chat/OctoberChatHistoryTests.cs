using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Services.OctoberChat;

namespace Vulgata.Tests.October.Chat;

/// <summary>Actual SQLite selection and canonical codec; live host authority remains the Business fixture's controlled port.</summary>
public sealed class OctoberChatHistoryTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Transcript_IndependentProofsStayVisibleWhenCombinedProofExceedsBudgetAsync()
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        await h.SeedAsync("first answer");
        await h.SeedAsync("second answer");
        h.Owner.Dependencies.MaximumCheckBatch = 1;
        await using var attempt = h.Create();
        await attempt.PrepareTranscriptAsync();
        OctoberChatTranscriptSnapshot? applied = null;
        await attempt.HandoffTranscriptAsync((snapshot, _) =>
        {
            applied = snapshot;
            return ValueTask.CompletedTask;
        }, _ => throw new InvalidOperationException("Authorized transcript must not be suppressed."));
        Assert.NotNull(applied);
        Assert.Equal(2, applied.Result.Turns.Length);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task History_PreservesOwnerTwelveTurnSelectionAndWholeByteBudgetOmissionAsync()
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        for (int i = 0; i < 13; i++) await h.SeedAsync("SourceSupported answer " + i);
        await using (var attempt = h.Create())
        {
            await attempt.PrepareHistoryAsync();
            var snapshot = await h.HistoryAsync(attempt);
            Assert.NotNull(snapshot);
            Assert.Equal(12, snapshot.Result.Turns.Length);
            Assert.True(snapshot.ContextOmitted);
            Assert.Same(h.Store.LastHistory, snapshot.Result);
            Assert.Equal(h.Codec.EncodeHistoryProjectionUtf8(snapshot.Result.Turns).ToArray(), snapshot.ProjectionUtf8.ToArray());
            Assert.Equal(h.Codec.ComputeHistoryProjectionDigest(snapshot.Result.Turns), snapshot.ProjectionDigest);
            Assert.Equal(snapshot.Result.Turns.Select(turn => turn.TurnId), snapshot.TurnIds);
            Assert.Equal(h.Owner.Identity, snapshot.Conversation);
            Assert.Equal(h.Context.Current, snapshot.ReadSelection);
            Assert.Equal(12, Assert.Single(h.Store.HistoryRequests).MaximumTurns);
            Assert.Equal(49_152, h.Store.HistoryRequests[0].MaximumUtf8Bytes);
        }
        await h.SeedAsync(new string('界', 18_000)); // A valid saved answer, too large for a complete inference-history turn.
        await using (var attempt = h.Create())
        {
            await attempt.PrepareHistoryAsync();
            var snapshot = await h.HistoryAsync(attempt);
            Assert.NotNull(snapshot);
            Assert.Empty(snapshot.Result.Turns);
            Assert.Empty(snapshot.TurnIds);
            Assert.Null(snapshot.Result.Dependencies);
            Assert.True(snapshot.ContextOmitted);
            Assert.Equal(h.Codec.EncodeHistoryProjectionUtf8([]).ToArray(), snapshot.ProjectionUtf8.ToArray());
        }
        Assert.Equal(0, h.Store.Mutations);
        Assert.Equal(0, h.Dependencies.MergeCalls);
        Assert.Equal(0, h.Dependencies.RootCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task History_PreservesOnlyCompletedContiguousSuffixWithoutImportingPartialTranscriptAsync()
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        await h.SeedAsync("old completed");
        await h.SeedAsync("interrupted display only", DialogueTerminalState.Interrupted);
        DialogueTerminalProjection newest = await h.SeedAsync("new complete with Advisory qualification");
        await using var attempt = h.Create();

        await attempt.PrepareHistoryAsync();
        var snapshot = await h.HistoryAsync(attempt);

        Assert.NotNull(snapshot);
        Assert.Equal(newest.TurnId, Assert.Single(snapshot.Result.Turns).TurnId);
        Assert.Equal(newest.AnswerText, snapshot.Result.Turns[0].AnswerText);
        Assert.True(snapshot.ContextOmitted);
        Assert.Empty(h.Store.TranscriptRequests);
        Assert.Equal(0, h.Store.Mutations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("ordering")]
    [InlineData("digest")]
    [InlineData("bytes")]
    public async Task History_MalformedOwnerResultIsRejectedWithoutRepairOrReselectionAsync(string malformed)
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        await h.SeedAsync("one");
        await h.SeedAsync("two");
        h.Store.History = async (request, subject, token) =>
        {
            var result = await h.Owner.Store.ReadHistoryAsync(request, subject, token);
            if (malformed == "digest") return result with { ProjectionDigest = new string('0', 64) };
            var turns = malformed == "ordering" ? result.Turns.Reverse().ToImmutableArray()
                : result.Turns.SetItem(1, result.Turns[1] with { AnswerText = new string('x', 50_000) });
            return result with { Turns = turns, ProjectionDigest = h.Codec.ComputeHistoryProjectionDigest(turns) };
        };
        await using var attempt = h.Create();

        await attempt.PrepareHistoryAsync();
        Assert.Null(await h.HistoryAsync(attempt));

        Assert.Single(h.Store.HistoryRequests);
        Assert.Empty(h.Store.TranscriptRequests);
        Assert.Equal(0, h.Store.Mutations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task History_ChangedModeScopeOrPurposeCannotReadAnotherConversationSelectionAsync()
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        await h.SeedAsync("private history");
        ReadSelectionRequest original = h.Context.Current!;
        foreach (ReadSelectionRequest changed in new[] { original with { Mode = KnowledgeMode.Business },
            original with { RepositoryScope = [Guid.NewGuid()] }, original with { Purpose = KnowledgePurpose.Reader } })
        {
            h.Context.Current = changed;
            await using var attempt = h.Create();
            await attempt.PrepareHistoryAsync();
            Assert.Null(await h.HistoryAsync(attempt));
        }
        Assert.Empty(h.Store.HistoryRequests);
        Assert.Equal(0, h.Store.Mutations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task History_UncitedDependencyRevocationBeforeHandoffSuppressesWholeSnapshotAsync()
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        await h.SeedAsync("a qualified saved answer", origin: "uncited-history-input");
        await using var attempt = h.Create();
        await attempt.PrepareHistoryAsync();
        h.Owner.Dependencies.DeniedOrigins.Add("uncited-history-input");

        Assert.Null(await h.HistoryAsync(attempt));

        Assert.Equal(1, h.Suppressions);
        Assert.NotEmpty(h.Dependencies.Checks);
        Assert.Equal(0, h.Store.Mutations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task History_ContextChangesDuringFinalAuthorityAwaitCannotApplyOrEraseReplacementAsync()
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        await h.SeedAsync("old identity content");
        await using var attempt = h.Create();
        await attempt.PrepareHistoryAsync();
        var entered = OctoberHistoryFixture.Signal();
        var release = OctoberHistoryFixture.Signal();
        h.Dependencies.Check = async (selection, caps, token) =>
        {
            entered.TrySetResult();
            await release.Task;
            return await h.Owner.Dependencies.ReauthorizeAsync(selection, caps, token);
        };
        Task<OctoberChatHistorySnapshot?> handoff = h.HistoryAsync(attempt);
        try
        {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, handoff).WaitAsync(TimeSpan.FromSeconds(10)));
            h.Context.Current = h.Context.Current! with { Subject = h.Owner.Subject with { AuthenticationBinding = "replacement-stamp" } };
        }
        finally { release.TrySetResult(); }
        Assert.Null(await handoff);
        Assert.Equal(0, h.Applies);
        Assert.Equal(0, h.Suppressions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task History_IgnoredStoreReadRemainsOwnedPastDeadlineAndCannotRestoreLateResultAsync()
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        await h.SeedAsync("saved content");
        var entered = OctoberHistoryFixture.Signal();
        var release = OctoberHistoryFixture.Signal();
        h.Store.History = async (request, subject, token) =>
        {
            var value = await h.Owner.Store.ReadHistoryAsync(request, subject, token);
            entered.TrySetResult();
            await release.Task;
            return value;
        };
        await using var attempt = h.Create();
        Task prepare = attempt.PrepareHistoryAsync();
        try
        {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, prepare).WaitAsync(TimeSpan.FromSeconds(10)));
            h.Clock.Advance(TimeSpan.FromSeconds(11));
            await prepare.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(attempt.Completion.IsCompleted);
            Assert.Null(await h.HistoryAsync(attempt));
            Assert.False(attempt.SealAndWaitForSettlementAsync().IsCompleted);
            h.CurrentAttachment = false;
        }
        finally { release.TrySetResult(); await prepare; }
        await attempt.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(h.Store.HistoryRequests);
        Assert.Equal(0, h.ReadyApplies);
        Assert.Equal(0, h.Store.Mutations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task History_ExpiryBetweenStagesDoesNotRenewOriginalDisplayLifetimeAsync()
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        await h.SeedAsync("answer");
        await using var attempt = h.Create();
        await attempt.PrepareHistoryAsync();
        int checks = h.Dependencies.Checks.Count;
        h.Clock.Advance(TimeSpan.FromSeconds(11));
        Assert.Null(await h.HistoryAsync(attempt));
        Assert.Equal(checks, h.Dependencies.Checks.Count);
        Assert.Single(h.Store.HistoryRequests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task History_SealInsideAttachmentPredicatePreventsNewContextOrStoreDispatchAsync()
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        await h.SeedAsync("answer");
        OctoberChatHistoryProjection? attempt = null;
        h.AttachmentCheck = id => { _ = attempt!.SealAndWaitForSettlementAsync(); return true; };
        await using (attempt = h.Create())
        {
            await attempt.PrepareHistoryAsync();
            await attempt.SealAndWaitForSettlementAsync();
            Assert.Equal(0, h.Context.Calls);
            Assert.Empty(h.Store.HistoryRequests);
            Assert.Empty(h.Dependencies.Checks);
        }
    }
}

/// <summary>Shared tests only: real owner seeding precedes a read-only observing consumer store.</summary>
internal sealed class OctoberHistoryFixture : IAsyncDisposable
{
    private OctoberHistoryFixture(DialoguePersistenceFixture owner)
    {
        Owner = owner;
        Store = new(owner.Store);
        Context = new(new(owner.Identity.Publication, owner.Subject, owner.Identity.Mode, KnowledgePurpose.Chat, owner.Identity.RepositoryScope));
        Dependencies = new(owner.Dependencies);
    }
    public DialoguePersistenceFixture Owner { get; }
    public OctoberDialoguePayloadCodec Codec { get; } = new();
    public ReadOnlyStore Store { get; }
    public CurrentContext Context { get; }
    public ObservedDependencies Dependencies { get; }
    public ControlledChatFixture.ManualClock Clock { get; } = new();
    public bool CurrentAttachment { get; set; } = true;
    public Func<Guid, bool>? AttachmentCheck { get; set; }
    public int Applies { get; private set; }
    public int ReadyApplies { get; private set; }
    public int Suppressions { get; private set; }
    public static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public static async Task<OctoberHistoryFixture> OpenAsync()
    {
        var owner = await DialoguePersistenceFixture.CreateAsync();
        await owner.RequireOpenAsync();
        return new(owner);
    }
    public async Task<DialogueTerminalProjection> SeedAsync(string answer, DialogueTerminalState state = DialogueTerminalState.Completed,
        string origin = "uncited-seed-input", bool reference = false)
    {
        var history = await Owner.Store.ReadHistoryAsync(Owner.HistoryRequest, Owner.Subject);
        DialogueAdmission admission = await Owner.AdmitAsync(Owner.BeginRequest(Guid.NewGuid(), history));
        var dependencies = Owner.Dependencies.Extend(admission.Request.InitialDependencies, origin);
        var saved = Owner.TerminalRequest(admission) with { AnswerText = answer, TerminalState = state, InferenceDependencies = dependencies,
            Reason = state == DialogueTerminalState.Completed ? DialogueTerminalReason.None : state == DialogueTerminalState.Interrupted
                ? DialogueTerminalReason.UserCancelled : DialogueTerminalReason.ExecutionFailure,
            ProviderFinish = state == DialogueTerminalState.Completed ? DialogueProviderFinish.ValidStop : state == DialogueTerminalState.Interrupted
                ? DialogueProviderFinish.Cancelled : DialogueProviderFinish.Unknown };
        if (reference)
        {
            var item = new OctoberChatReference(new(Owner.Document.BatchId, Owner.Identity.Publication.PublicationId,
                Owner.Document.LogicalDocumentId, Owner.Document.DocumentVersionId, "overview"), true, 2, 345);
            saved = saved with { References = [item], ConsumedReads = [new(item, Owner.Dependencies.Consume(item, dependencies))] };
        }
        saved = saved with { OutcomeDigest = Codec.ComputeOutcomeDigest(saved) };
        var result = await Owner.Store.SaveTerminalAsync(saved, Owner.Subject);
        Assert.Equal(DialogueStatus.Succeeded, result.Status);
        return Assert.IsType<DialogueTerminalProjection>(result.Projection);
    }
    public OctoberChatHistoryProjection Create() => new(Owner.Identity, Owner.HistoryRequest, Guid.NewGuid(),
        id => AttachmentCheck?.Invoke(id) ?? CurrentAttachment, Context, Store, Codec, Dependencies, Clock);
    public async Task<OctoberChatHistorySnapshot?> HistoryAsync(OctoberChatHistoryProjection attempt)
    {
        OctoberChatHistorySnapshot? result = null;
        await attempt.HandoffHistoryAsync((value, token) =>
        { token.ThrowIfCancellationRequested(); Applies++; if (value is not null) ReadyApplies++; result = value; return ValueTask.CompletedTask; },
            _ => { Suppressions++; return ValueTask.CompletedTask; });
        return result;
    }
    public OctoberChatTurnBinding Binding(DialogueTerminalProjection saved) => new(saved.Conversation.ConversationId,
        saved.Conversation.ContextId, saved.TurnId, Context.Current!, new("controlled-saved-view-only"));
    public ValueTask DisposeAsync() => Owner.DisposeAsync();

    internal sealed class CurrentContext(ReadSelectionRequest request) : IOctoberChatContextProvider
    {
        public ReadSelectionRequest? Current { get; set; } = request;
        public int Calls { get; private set; }
        public event Action? ContextChanged { add { } remove { } }
        public Task<ReadSelectionRequest?> GetCurrentAsync(CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(Current); }
    }
    internal sealed class ReadOnlyStore(IOctoberDialogueStore owner) : IOctoberDialogueStore
    {
        public int Mutations { get; private set; }
        public List<DialogueHistoryRequest> HistoryRequests { get; } = [];
        public List<DialogueHistoryRequest> TranscriptRequests { get; } = [];
        public DialogueHistoryResult? LastHistory { get; private set; }
        public DialogueTranscriptResult? LastTranscript { get; private set; }
        public Func<DialogueHistoryRequest, KnowledgeSubject, CancellationToken, Task<DialogueHistoryResult>>? History { get; set; }
        public async Task<DialogueHistoryResult> ReadHistoryAsync(DialogueHistoryRequest request, KnowledgeSubject subject, CancellationToken token = default)
        { HistoryRequests.Add(request); return LastHistory = await (History?.Invoke(request, subject, token) ?? owner.ReadHistoryAsync(request, subject, token)); }
        public async Task<DialogueTranscriptResult> ReadTranscriptAsync(DialogueHistoryRequest request, KnowledgeSubject subject, CancellationToken token = default)
        { TranscriptRequests.Add(request); return LastTranscript = await owner.ReadTranscriptAsync(request, subject, token); }
        private Exception Refuse() { Mutations++; return new InvalidOperationException("Read-only consumer must not invoke lifecycle or write operations."); }
        public Task<DialogueConversationResult> OpenConversationAsync(DialogueConversationOpenRequest request, KnowledgeSubject subject, CancellationToken token = default) => throw Refuse();
        public Task<DialogueBeginResult> BeginTurnAsync(DialogueBeginRequest request, KnowledgeSubject subject, CancellationToken token = default) => throw Refuse();
        public Task<DialogueBeginResult> LookupAdmissionAsync(DialogueAdmissionLookup request, KnowledgeSubject subject, CancellationToken token = default) => throw Refuse();
        public Task<DialogueTransitionResult> TransitionTurnAsync(DialogueTransitionRequest request, KnowledgeSubject subject, CancellationToken token = default) => throw Refuse();
        public Task<DialogueTerminalResult> SaveTerminalAsync(DialogueTerminalSaveRequest request, KnowledgeSubject subject, CancellationToken token = default) => throw Refuse();
        public Task<DialogueTerminalResult> LookupTerminalAsync(DialogueTerminalLookup request, KnowledgeSubject subject, CancellationToken token = default) => throw Refuse();
        public Task<DialogueOrphanResult> InterruptOrphanAsync(DialogueOrphanRequest request, CancellationToken token = default) => throw Refuse();
    }
    internal sealed class ObservedDependencies(IOctoberKnowledgeDependencyAuthority owner) : IOctoberKnowledgeDependencyAuthority
    {
        public List<ImmutableArray<KnowledgeDependencyHandle>> Checks { get; } = [];
        public int MergeCalls { get; private set; }
        public int RootCalls { get; private set; }
        public Func<ReadSelectionRequest, ImmutableArray<KnowledgeDependencyHandle>, CancellationToken, Task<KnowledgeDependencyStatus>>? Check { get; set; }
        public Task<KnowledgeDependencyResult> CreateNoKnowledgeRootAsync(ReadSelectionRequest selection, string digest, CancellationToken token = default)
        { RootCalls++; return owner.CreateNoKnowledgeRootAsync(selection, digest, token); }
        public Task<KnowledgeDependencyResult> MergeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> capsules, CancellationToken token = default)
        { MergeCalls++; return owner.MergeAsync(selection, capsules, token); }
        public Task<KnowledgeDependencyStatus> ReauthorizeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> capsules, CancellationToken token = default)
        { Checks.Add(capsules); return Check?.Invoke(selection, capsules, token) ?? owner.ReauthorizeAsync(selection, capsules, token); }
        public Task<KnowledgeConsumedContentResult> BuildConsumedContentAsync(ReadSelectionRequest selection, DocumentHandle document, ImmutableArray<KnowledgeDependencyHandle> pages, CancellationToken token = default) => throw new InvalidOperationException("No new read credit.");
        public Task<KnowledgeConsumedContentResult> VerifyConsumedContentAsync(ReadSelectionRequest selection, KnowledgeConsumedContentHandle proof, KnowledgeDependencyHandle dependencies, CancellationToken token = default) => throw new InvalidOperationException("No consumer proof reconstruction.");
    }
}
