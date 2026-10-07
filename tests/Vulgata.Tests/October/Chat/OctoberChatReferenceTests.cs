using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Bunit;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Shared.OctoberChat;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Explore;
using Vulgata.Web.Services.OctoberChat;
using Vulgata.Web.ViewModels.OctoberChat;
using FooterComponent = Vulgata.Web.Components.Chat.OctoberChatReferenceFooter;

namespace Vulgata.Tests.October.Chat;

/// <summary>
/// Saved-display consumer tests. Real Business reader/protection uses its controlled published source;
/// supplied saved projections are explicitly controlled unless a case obtains an actual SQLite receipt.
/// No browser navigation, live identity or parent-page activation is qualified.
/// </summary>
public sealed class OctoberChatReferenceTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAndHandoff_ExactSavedVersionsSectionsOrderAndCoverageUseOwnerMetadataAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        var first = new OctoberChatReference(h.Owner.Data.Handle(h.Owner.Data.OtherCl), false, 3, 987);
        var second = new OctoberChatReference(h.Owner.Handle with { SectionId = "overview" }, true, 2, 345);
        h.Saved = h.Saved with { References = [first, second] };
        h.CreateProjection();

        await h.PrepareAndHandoffAsync();

        OctoberChatReferenceFooterState state = Assert.Single(h.States);
        Assert.False(state.IsUnavailable);
        Assert.Equal(new[] { first, second }, state.Rows.Select(row => row.Reference));
        Assert.Equal(new[] { "CL helper", "CL order" }, state.Rows.Select(row => row.Title));
        Assert.Equal(new[] { ExactDocumentLinkBuilder.Build(first.Document), ExactDocumentLinkBuilder.Build(second.Document) }, state.Rows.Select(row => row.Link));
        Assert.Contains($"version={second.Document.DocumentVersionId:D}", state.Rows[1].Link, StringComparison.Ordinal);
        Assert.Contains("section=overview", state.Rows[1].Link, StringComparison.Ordinal);
        Assert.All(h.Reads.Requests, request => Assert.Null(request.Continuation));
        Assert.Equal(2, h.Reads.Requests.Count);
        Assert.Equal(0, h.Dependencies.BuildCalls);
        Assert.Equal(0, h.Dependencies.VerifyCalls);
        Assert.Equal(0, h.Dependencies.RootCalls);
        Assert.NotNull(state.Dependencies);
        Assert.Contains(h.Dependencies.MergeCalls, caps => caps.Contains(h.Saved.InferenceDependencies!));
        Assert.Contains(h.Dependencies.CheckCalls, caps => caps.Contains(state.Dependencies!));
        Assert.Equal(0, h.Suppressions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAndHandoff_EmptyFooterStillRechecksRetainedUncitedHistoryUnionAsync()
    {
        await using Harness h = await Harness.OpenAsync(history: true);
        h.Saved = h.Saved with { References = [] };
        h.CreateProjection();
        await h.Projection!.PrepareAsync();
        h.Owner.Policy.DeniedContributor = h.Owner.Uncited;

        await h.HandoffAsync();

        Assert.Empty(h.Reads.Requests);
        Assert.Equal(1, h.Suppressions);
        Assert.True(Assert.Single(h.States).IsUnavailable);
        Assert.Contains(h.Owner.Policy.Requests, request => request.AllContributors.Contains(h.Owner.Uncited));
        Assert.Equal(0, h.Dependencies.RootCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("outer")]
    [InlineData("page")]
    [InlineData("profile")]
    [InlineData("page-digest")]
    [InlineData("output-digest")]
    [InlineData("handle")]
    [InlineData("null-document")]
    public async Task PrepareAndHandoff_InconsistentOwnerEnvelopeNeverDisclosesMetadataAsync(string invalid)
    {
        await using Harness h = await Harness.OpenAsync();
        h.Reads.Mutate = (result, budget) =>
        {
            KnowledgeDisclosedPage<ContentPage> envelope = result.Disclosure!;
            return invalid switch
            {
                "outer" => result with { Status = KnowledgeReadStatus.Unavailable },
                "page" => result with { Disclosure = Encode(envelope.Page with { Status = KnowledgeReadStatus.Unavailable }, envelope.Dependencies, budget) },
                "profile" => result with { Disclosure = envelope with { OutputProfileId = "unagreed-profile" } },
                "page-digest" => result with { Disclosure = envelope with { PageDigest = new string('0', 64) } },
                "output-digest" => result with { Disclosure = envelope with { OutputDigest = new string('0', 64) } },
                "handle" => result with { Disclosure = Encode(envelope.Page with { Document = envelope.Page.Document! with
                    { Handle = envelope.Page.Document.Handle with { DocumentVersionId = Guid.NewGuid() } } }, envelope.Dependencies, budget) },
                _ => result with { Disclosure = Encode(envelope.Page with { Document = null }, envelope.Dependencies, budget) },
            };
        };
        h.CreateProjection();

        await h.PrepareAndHandoffAsync();

        OctoberChatReferenceFooterState state = Assert.Single(h.States);
        Assert.True(state.IsUnavailable);
        Assert.Empty(state.Rows);
        Assert.Null(state.Dependencies);
        Assert.Equal(0, h.Suppressions);
        Assert.Single(h.Reads.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("read-count")]
    [InlineData("single-result")]
    [InlineData("aggregate")]
    public async Task PrepareAndHandoff_DisplayReadAndActualEncodedByteLimitsFailWithoutContinuationAsync(string limit)
    {
        await using Harness h = await Harness.OpenAsync();
        int count = limit == "read-count" ? 17 : limit == "aggregate" ? 6 : 1;
        h.Saved = h.Saved with { References = Enumerable.Range(0, count).Select(index =>
            new OctoberChatReference(h.Owner.Handle with { SectionId = "bounded-" + index }, true, 1, 1)).ToImmutableArray() };
        h.Reads.Read = (request, budget, _) =>
        {
            // Deliberately oversized canonical-port response; actual encoder owns its bytes.
            var page = new ContentPage(KnowledgeReadStatus.Success,
                new(request.Document, DocumentKind.CodeLogic, "controlled title", []),
                new string('x', limit == "single-result" ? 65_536 : 60_000), [], new("must-not-follow"), new(true, []));
            return Task.FromResult(new KnowledgeDisclosureReadResult<ContentPage>(KnowledgeReadStatus.Success,
                Encode(page, h.Saved.InferenceDependencies!, budget)));
        };
        h.CreateProjection();

        await h.PrepareAndHandoffAsync();

        Assert.True(Assert.Single(h.States).IsUnavailable);
        Assert.Empty(h.States[0].Rows);
        Assert.All(h.Reads.Requests, request => Assert.Null(request.Continuation));
        Assert.All(h.Reads.Budgets, budget =>
        {
            Assert.InRange(budget.MaximumResultUtf8Bytes, 1, 65_536);
            Assert.InRange(budget.RemainingAggregateUtf8Bytes, 1, 262_144);
        });
        if (limit == "read-count") Assert.Empty(h.Reads.Requests);
        else if (limit == "single-result") Assert.Single(h.Reads.Requests);
        else
        {
            Assert.InRange(h.Reads.Requests.Count, 4, 5);
            Assert.True(h.Reads.Budgets.Last().RemainingAggregateUtf8Bytes < 60_000);
        }
        Assert.Equal(0, h.Dependencies.BuildCalls);
        Assert.Equal(0, h.Dependencies.VerifyCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Handoff_ExpiryAfterPrepareDoesNotRenewTenSecondAttemptAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.CreateProjection();
        await h.Projection!.PrepareAsync();
        int checks = h.Dependencies.CheckCalls.Count;
        int reads = h.Reads.Requests.Count;
        h.Clock.Advance(TimeSpan.FromSeconds(11));

        await h.HandoffAsync();

        Assert.All(h.States, state => Assert.True(state.IsUnavailable));
        Assert.Equal(checks, h.Dependencies.CheckCalls.Count);
        Assert.Equal(reads, h.Reads.Requests.Count);
        Assert.Equal(0, h.Suppressions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Handoff_SameTurnReplacedAttachmentCannotApplyOrEraseCurrentViewAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.CreateProjection();
        await h.Projection!.PrepareAsync();
        h.IsCurrent = false;
        h.Owner.Policy.DeniedContributor = h.Owner.Uncited;

        await h.HandoffAsync();

        Assert.Empty(h.States);
        Assert.Equal(0, h.Suppressions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Prepare_DeadlineDoesNotPretendIgnoredOwnerReadSettledOrAllowLateRestoreAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Reads.Read = async (request, budget, token) =>
        {
            var result = await h.Owner.Disclosed.ReadExactAsync(request, budget, token);
            entered.TrySetResult();
            await release.Task;
            return result;
        };
        h.CreateProjection();
        Task prepare = h.Projection!.PrepareAsync();
        try
        {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, prepare).WaitAsync(TimeSpan.FromSeconds(10)));
            h.Clock.Advance(TimeSpan.FromSeconds(11));
            await prepare.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(h.Projection.Completion.IsCompleted);
            await h.HandoffAsync();
            Assert.All(h.States, state => Assert.True(state.IsUnavailable));
            Task settlement = h.Projection.SealAndWaitForSettlementAsync();
            Assert.False(settlement.IsCompleted);
        }
        finally { release.TrySetResult(); await prepare; }
        await h.Projection.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.All(h.States, state => Assert.True(state.IsUnavailable));
        Assert.Equal(0, h.Suppressions);
        Assert.Single(h.Reads.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handoff_WrongPurposeOrChangedContextNeverDisclosesOrErasesAsync(bool changedAfterPrepare)
    {
        await using Harness h = await Harness.OpenAsync();
        if (!changedAfterPrepare) h.Context.Current = h.Context.Current! with { Purpose = KnowledgePurpose.Reader };
        h.CreateProjection();
        await h.Projection!.PrepareAsync();
        if (changedAfterPrepare) h.Context.Current = h.Context.Current! with { Subject = h.Owner.Request.Subject with { SubjectId = "another-owner" } };

        await h.HandoffAsync();

        Assert.Empty(h.States);
        Assert.Equal(0, h.Suppressions);
        if (!changedAfterPrepare) Assert.Empty(h.Reads.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handoff_AggregateDenialClearsOnlyBoundDependentViewPreservingDurableFlagsAsync(bool uncertain)
    {
        await using Harness h = await Harness.OpenAsync();
        var model = new OctoberChatTurnViewModel(h.Binding);
        var other = new OctoberChatTurnViewModel(h.Binding with { TurnId = Guid.NewGuid() });
        foreach (var view in new[] { model, other })
        {
            Assert.True(view.Apply(Event(view.Binding, 1, OctoberChatEventKind.TurnStarted)));
            Assert.True(view.Apply(Event(view.Binding, 2, OctoberChatEventKind.AnswerDelta, "已显示的保存正文")));
            Assert.True(view.Apply(uncertain ? Event(view.Binding, 3, OctoberChatEventKind.TurnSaveUncertain)
                : Event(view.Binding, 3, OctoberChatEventKind.TurnSaved) with { SavedOutcome = OctoberChatDisplayOutcome.Completed }));
        }
        Assert.False(model.TrySuppressDisclosure(other.Binding));
        h.CreateProjection();
        await h.Projection!.PrepareAsync();
        h.Owner.Policy.DeniedContributor = h.Owner.Uncited;

        await h.Projection.HandoffAsync((_, _) => ValueTask.CompletedTask, token =>
        {
            token.ThrowIfCancellationRequested();
            Assert.True(model.TrySuppressDisclosure(h.Binding));
            Assert.False(other.TrySuppressDisclosure(h.Binding));
            return ValueTask.CompletedTask;
        });

        Assert.Equal(string.Empty, model.AnswerText);
        Assert.Empty(model.References);
        Assert.Equal(!uncertain, model.IsTerminal);
        Assert.Equal(uncertain, model.IsSaveUncertain);
        Assert.Equal("已显示的保存正文", other.AnswerText);
        Assert.False(model.Apply(Event(h.Binding, 4, OctoberChatEventKind.AnswerDelta, "旧正文复活")));
        Assert.Equal(string.Empty, model.AnswerText);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Handoff_AuthorityOutageAfterPrepareSuppressesEntireDependentDisplayAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.CreateProjection();
        await h.Projection!.PrepareAsync();
        h.Owner.Policy.Status = DisclosureStatus.AuthorityUnavailable;

        await h.HandoffAsync();

        Assert.Equal(1, h.Suppressions);
        Assert.True(Assert.Single(h.States).IsUnavailable);
        Assert.Empty(h.States[0].Rows);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAndHandoff_ActualSavedOwnerProjectionGetsFreshDisplayLifetimeAfterGenerationEndedAsync()
    {
        await using var owner = await DialoguePersistenceFixture.CreateAsync();
        DialogueAdmission admission = await owner.AdmitAsync();
        var reference = new OctoberChatReference(new(owner.Document.BatchId, owner.Identity.Publication.PublicationId,
            owner.Document.LogicalDocumentId, owner.Document.DocumentVersionId, "overview"), true, 2, 345);
        KnowledgeDependencyHandle aggregate = owner.Dependencies.Extend(owner.Root, "original-history-and-uncited-inputs");
        KnowledgeConsumedContentHandle proof = owner.Dependencies.Consume(reference, aggregate);
        var codec = new OctoberDialoguePayloadCodec();
        var terminal = owner.TerminalRequest(admission) with { References = [reference],
            ConsumedReads = [new(reference, proof)], InferenceDependencies = aggregate };
        terminal = terminal with { OutcomeDigest = codec.ComputeOutcomeDigest(terminal) };
        Assert.Equal(DialogueStatus.Succeeded, (await owner.Store.TransitionTurnAsync(new(terminal.Owner,
            DialogueTurnState.Running, DialogueTurnState.Saving), owner.Subject)).Status);
        DialogueTerminalResult saved = await owner.Store.SaveTerminalAsync(terminal, owner.Subject);
        Assert.Equal(DialogueStatus.Succeeded, saved.Status);
        Assert.NotNull(saved.Receipt);
        DialogueTerminalProjection projection = Assert.IsType<DialogueTerminalProjection>(saved.Projection);
        owner.Clock.Now = admission.DeadlineUtc.AddHours(1);
        var current = new ReadSelectionRequest(owner.Identity.Publication, owner.Subject, owner.Identity.Mode,
            KnowledgePurpose.Chat, owner.Identity.RepositoryScope);
        var binding = new OctoberChatTurnBinding(owner.Identity.ConversationId, owner.Identity.ContextId, admission.TurnId, current, new("ended-generation"));
        var read = new ReadObserver(new FixedSelectionReader(), new DisclosureFixture().Disclosed);
        read.Read = (request, budget, _) => Task.FromResult(new KnowledgeDisclosureReadResult<ContentPage>(KnowledgeReadStatus.Success,
            Encode(new(KnowledgeReadStatus.Success, new(request.Document, DocumentKind.CodeLogic, "保存版本的标题", []),
                "metadata acquired for display only", [], null, new(false, [])), aggregate, budget)));
        var dependencies = new DependencyObserver(owner.Dependencies);
        Guid attachment = Guid.NewGuid();
        await using var display = new OctoberChatReferenceProjection(binding, projection, attachment, id => id == attachment,
            new MutableContext(current), read, read, dependencies, owner.Clock);
        OctoberChatReferenceFooterState? state = null;

        await display.PrepareAsync();
        await display.HandoffAsync((value, _) => { state = value; return ValueTask.CompletedTask; }, _ => throw new InvalidOperationException("No suppression expected."));

        Assert.NotNull(state);
        Assert.False(state.IsUnavailable);
        Assert.Equal(reference, Assert.Single(state.Rows).Reference);
        Assert.Equal("保存版本的标题", state.Rows[0].Title);
        Assert.Equal(1, read.Requests.Count);
        Assert.Equal(0, dependencies.BuildCalls);
        Assert.Equal(0, dependencies.VerifyCalls);
        Assert.Null((await owner.OpenAsync()).Conversation!.ActiveTurnId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Footer_EncodesOwnerTitlesAcceptsOnlyExactLocalLinksAndClearsReplacedState()
    {
        using var context = new BunitContext();
        var owner = new DisclosureFixture();
        var reference = new OctoberChatReference(owner.Handle with { SectionId = "overview" }, true, 2, 345);
        string exact = ExactDocumentLinkBuilder.Build(reference.Document);
        const string title = "<img src=x onerror=alert(1)> [模型链接](https://untrusted.invalid/)";
        // Isolated leaf encoding only; this labelled opaque value is not an authorization claim.
        var leafOnlyCapsule = new KnowledgeDependencyHandle(Guid.NewGuid(), new string('a', 64), "controlled leaf-only opaque dependency");
        var ready = new OctoberChatReferenceFooterState(false, [new(reference, title, exact)], leafOnlyCapsule);
        var component = context.Render<FooterComponent>(parameters => parameters.Add(item => item.State, ready));

        Assert.Equal(title, Assert.Single(component.FindAll("a")).TextContent);
        Assert.Equal(exact, component.Find("a").GetAttribute("href"));
        Assert.Empty(component.FindAll("img,script"));
        Assert.Contains("部分", component.Markup, StringComparison.Ordinal);
        component.Render(parameters => parameters.Add(item => item.State,
            new OctoberChatReferenceFooterState(false, [new(reference, "unsafe", "javascript:alert(1)")], leafOnlyCapsule)));
        Assert.Empty(component.FindAll("a"));
        component.Render(parameters => parameters.Add(item => item.State, new OctoberChatReferenceFooterState(true, [], null)));
        Assert.Empty(component.FindAll("a"));
        Assert.DoesNotContain(title, component.Markup, StringComparison.Ordinal);
        Assert.Contains("暂不可用", component.Markup, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Seal_ReentrantAttachmentCheckPreventsNewContextDispatchInEitherStageAsync(bool handoff)
    {
        await using Harness h = await Harness.OpenAsync();
        h.CreateProjection();
        if (handoff) await h.Projection!.PrepareAsync();
        int predicateCalls = 0;
        int contextsAtSeal = -1;
        int readsAtSeal = -1;
        int checksAtSeal = -1;
        int opensAtSeal = -1;
        int applied = 0;
        int suppressed = 0;
        Task? settlement = null;
        h.CurrentAttachment = _ =>
        {
            // Seal at this stage's first attachment check, before context dispatch.
            // No predicate-order count beyond entry or cancellation timing is assumed.
            if (++predicateCalls == 1)
            {
                contextsAtSeal = h.Context.Calls;
                readsAtSeal = h.Reads.Requests.Count;
                checksAtSeal = h.Dependencies.CheckCalls.Count;
                opensAtSeal = h.Reads.OpenCalls;
                settlement = h.Projection!.SealAndWaitForSettlementAsync();
            }
            return true;
        };

        if (handoff)
            await h.Projection!.HandoffAsync((_, _) => { applied++; return ValueTask.CompletedTask; },
                _ => { suppressed++; return ValueTask.CompletedTask; });
        else
            await h.Projection!.PrepareAsync();

        Assert.NotNull(settlement);
        await settlement.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(contextsAtSeal, h.Context.Calls);
        Assert.Equal(readsAtSeal, h.Reads.Requests.Count);
        Assert.Equal(checksAtSeal, h.Dependencies.CheckCalls.Count);
        Assert.Equal(opensAtSeal, h.Reads.OpenCalls);
        Assert.Equal(0, applied);
        Assert.Equal(0, suppressed);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Handoff_IgnoredApplyCancellationRetainsActualSettlementAndNeverReplaysOrRestoresLateUiAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.CreateProjection();
        await h.Projection!.PrepareAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int applyCalls = 0;
        int suppressCalls = 0;
        int actualUiUpdates = 0;
        int reads = h.Reads.Requests.Count;
        int checks = h.Dependencies.CheckCalls.Count;
        Task handoff = h.Projection.HandoffAsync(async (_, token) =>
        {
            applyCalls++;
            entered.TrySetResult();
            await release.Task; // Controlled dispatcher delay ignores cancellation.
            // The trusted host checks again at actual UI mutation, not only queue entry.
            token.ThrowIfCancellationRequested();
            if (h.IsCurrent) actualUiUpdates++;
        }, _ => { suppressCalls++; return ValueTask.CompletedTask; });
        try
        {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, handoff).WaitAsync(TimeSpan.FromSeconds(10)));
            h.Clock.Advance(TimeSpan.FromSeconds(11));
            await handoff.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(h.Projection.Completion.IsCompleted);
            Task settlement = h.Projection.SealAndWaitForSettlementAsync();
            Assert.False(settlement.IsCompleted);
            int checksAfterTimeout = h.Dependencies.CheckCalls.Count;
            Assert.True(checksAfterTimeout > checks);
            h.IsCurrent = false;
            await h.Projection.PrepareAsync();
            await h.Projection.HandoffAsync((_, _) => { applyCalls++; return ValueTask.CompletedTask; },
                _ => { suppressCalls++; return ValueTask.CompletedTask; });
            Assert.Equal(1, applyCalls);
            Assert.Equal(0, suppressCalls);
            Assert.Equal(0, actualUiUpdates);
            Assert.Equal(reads, h.Reads.Requests.Count);
            Assert.Equal(checksAfterTimeout, h.Dependencies.CheckCalls.Count);
        }
        finally { release.TrySetResult(); await handoff; }
        await h.Projection.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(h.Projection.Completion.IsCompleted);
        Assert.Equal(1, applyCalls);
        Assert.Equal(0, actualUiUpdates);
    }

    private static OctoberChatEvent Event(OctoberChatTurnBinding binding, long sequence, OctoberChatEventKind kind, string? text = null) =>
        new(binding.ConversationId, binding.ContextId, binding.TurnId, sequence, DateTimeOffset.UnixEpoch.AddSeconds(sequence), kind, text);

    private static KnowledgeDisclosedPage<ContentPage> Encode(ContentPage page, KnowledgeDependencyHandle dependencies, KnowledgeReadOutputBudget budget)
    {
        ImmutableArray<byte> bytes = budget.Encoder.EncodeContent(page);
        return new(page, dependencies, OctoberKnowledgePolicy.Hash("october.knowledge.page.v1\nContentPage\n" + OctoberKnowledgePolicy.SerializeCanonical(page)),
            budget.Encoder.ProfileId, Convert.ToHexString(SHA256.HashData(bytes.AsSpan())), bytes);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private Harness(DisclosureFixture owner, KnowledgeDependencyHandle original)
        {
            Owner = owner;
            var identity = new DialogueConversationIdentity(Guid.NewGuid(), Guid.NewGuid(), owner.Request.Subject.SubjectId,
                owner.Request.Publication, owner.Request.Mode, owner.Request.RepositoryScope);
            Binding = new(identity.ConversationId, identity.ContextId, Guid.NewGuid(), owner.Request, new("controlled-ended-generation-selection"));
            Saved = new(identity, Binding.TurnId, 1, "保存的问题", "保存的回答", DialogueTerminalState.Completed,
                DialogueTerminalReason.None, [new(owner.Handle, true, 2, 345)], original);
            Context = new(owner.Request);
            Reads = new(owner.Reader, owner.Disclosed);
            Dependencies = new(owner.Authority);
        }
        public DisclosureFixture Owner { get; }
        public OctoberChatTurnBinding Binding { get; }
        public DialogueTerminalProjection Saved { get; set; }
        public MutableContext Context { get; }
        public ReadObserver Reads { get; }
        public DependencyObserver Dependencies { get; }
        public ControlledChatFixture.ManualClock Clock { get; } = new();
        public Guid AttachmentId { get; } = Guid.NewGuid();
        public bool IsCurrent { get; set; } = true;
        public Func<Guid, bool>? CurrentAttachment { get; set; }
        public OctoberChatReferenceProjection? Projection { get; private set; }
        public List<OctoberChatReferenceFooterState> States { get; } = [];
        public int Suppressions { get; private set; }
        public static async Task<Harness> OpenAsync(bool history = false)
        {
            var owner = new DisclosureFixture();
            KnowledgeDependencyResult root = await owner.Authority.CreateNoKnowledgeRootAsync(owner.Request, DisclosureFixture.Hash("controlled original prompt"));
            Assert.Equal(KnowledgeDependencyStatus.Succeeded, root.Status);
            KnowledgeDependencyHandle original = root.Dependencies!;
            if (history)
            {
                ReadSelection selection = await owner.OpenAsync();
                KnowledgeDisclosureReadResult<SearchPage> prior = await owner.Disclosed.SearchAsync(new(selection, "", 20), owner.Budget());
                Assert.Equal(KnowledgeReadStatus.Success, prior.Status);
                original = (await owner.Authority.MergeAsync(owner.Request, [original, prior.Disclosure!.Dependencies])).Dependencies!;
            }
            return new(owner, original);
        }
        public void CreateProjection() => Projection = new(Binding, Saved, AttachmentId, id => CurrentAttachment?.Invoke(id) ?? (IsCurrent && id == AttachmentId),
            Context, Reads, Reads, Dependencies, Clock);
        public async Task PrepareAndHandoffAsync()
        { await Projection!.PrepareAsync(); await HandoffAsync(); }
        public Task HandoffAsync() => Projection!.HandoffAsync((state, token) =>
        {
            token.ThrowIfCancellationRequested();
            if (IsCurrent) States.Add(state);
            return ValueTask.CompletedTask;
        }, token =>
        {
            token.ThrowIfCancellationRequested();
            if (IsCurrent) Suppressions++;
            return ValueTask.CompletedTask;
        });
        public async ValueTask DisposeAsync()
        { if (Projection is not null) await Projection.DisposeAsync(); }
    }

    private sealed class MutableContext(ReadSelectionRequest request) : IOctoberChatContextProvider
    {
        public ReadSelectionRequest? Current { get; set; } = request;
        public int Calls { get; private set; }
        public event Action? ContextChanged { add { } remove { } }
        public Task<ReadSelectionRequest?> GetCurrentAsync(CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(Current); }
    }

    /// <summary>Only the actual saved-store projection case scripts fresh selection, not owner cryptography or persistence.</summary>
    private sealed class FixedSelectionReader : IOctoberKnowledgeReader
    {
        public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken token = default) => Task.FromResult(new ReadSelectionResult(KnowledgeReadStatus.Success, new("controlled-fresh-display-selection")));
        public Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken token = default) => throw new InvalidOperationException();
        public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken token = default) => throw new InvalidOperationException();
        public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken token = default) => throw new InvalidOperationException();
    }

    private sealed class ReadObserver(IOctoberKnowledgeReader reader, IOctoberKnowledgeDisclosedReader disclosed) : IOctoberKnowledgeReader, IOctoberKnowledgeDisclosedReader
    {
        public List<ExactReadRequest> Requests { get; } = [];
        public List<KnowledgeReadOutputBudget> Budgets { get; } = [];
        public int OpenCalls { get; private set; }
        public Func<ExactReadRequest, KnowledgeReadOutputBudget, CancellationToken, Task<KnowledgeDisclosureReadResult<ContentPage>>>? Read { get; set; }
        public Func<KnowledgeDisclosureReadResult<ContentPage>, KnowledgeReadOutputBudget, KnowledgeDisclosureReadResult<ContentPage>>? Mutate { get; set; }
        public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken token = default)
        { OpenCalls++; return reader.OpenReadSelectionAsync(request, token); }
        public async Task<KnowledgeDisclosureReadResult<ContentPage>> ReadExactAsync(ExactReadRequest request, KnowledgeReadOutputBudget budget, CancellationToken token = default)
        {
            Requests.Add(request); Budgets.Add(budget);
            KnowledgeDisclosureReadResult<ContentPage> result = Read is null ? await disclosed.ReadExactAsync(request, budget, token) : await Read(request, budget, token);
            return Mutate?.Invoke(result, budget) ?? result;
        }
        public Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken token = default) => throw new InvalidOperationException("No legacy search.");
        public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken token = default) => throw new InvalidOperationException("No legacy exact-read fallback.");
        public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken token = default) => throw new InvalidOperationException("No legacy related read.");
        public Task<KnowledgeDisclosureReadResult<SearchPage>> SearchAsync(KnowledgeSearchRequest request, KnowledgeReadOutputBudget budget, CancellationToken token = default) => throw new InvalidOperationException("No discovery.");
        public Task<KnowledgeDisclosureReadResult<RelatedPage>> GetRelatedAsync(RelatedReadRequest request, KnowledgeReadOutputBudget budget, CancellationToken token = default) => throw new InvalidOperationException("No related read.");
    }

    private sealed class DependencyObserver(IOctoberKnowledgeDependencyAuthority inner) : IOctoberKnowledgeDependencyAuthority
    {
        public List<ImmutableArray<KnowledgeDependencyHandle>> MergeCalls { get; } = [];
        public List<ImmutableArray<KnowledgeDependencyHandle>> CheckCalls { get; } = [];
        public int RootCalls { get; private set; }
        public int BuildCalls { get; private set; }
        public int VerifyCalls { get; private set; }
        public Task<KnowledgeDependencyResult> CreateNoKnowledgeRootAsync(ReadSelectionRequest selection, string digest, CancellationToken token = default)
        { RootCalls++; return inner.CreateNoKnowledgeRootAsync(selection, digest, token); }
        public Task<KnowledgeDependencyResult> MergeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken token = default)
        { MergeCalls.Add(dependencies); return inner.MergeAsync(selection, dependencies, token); }
        public Task<KnowledgeDependencyStatus> ReauthorizeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken token = default)
        { CheckCalls.Add(dependencies); return inner.ReauthorizeAsync(selection, dependencies, token); }
        public Task<KnowledgeConsumedContentResult> BuildConsumedContentAsync(ReadSelectionRequest selection, DocumentHandle document, ImmutableArray<KnowledgeDependencyHandle> pages, CancellationToken token = default)
        { BuildCalls++; return inner.BuildConsumedContentAsync(selection, document, pages, token); }
        public Task<KnowledgeConsumedContentResult> VerifyConsumedContentAsync(ReadSelectionRequest selection, KnowledgeConsumedContentHandle proof, KnowledgeDependencyHandle dependencies, CancellationToken token = default)
        { VerifyCalls++; return inner.VerifyConsumedContentAsync(selection, proof, dependencies, token); }
    }
}
