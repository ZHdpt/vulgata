using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberChat;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Services.OctoberChat;
using Vulgata.Web.ViewModels.OctoberChat;

namespace Vulgata.Tests.October.Chat;

/// <summary>Actual owner saved projections, then read-only consumer loading; no stream replay, retry or live execution authority.</summary>
public sealed class OctoberChatSavedOutcomeTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DialogueTerminalState.Completed, "回答已保存。")]
    [InlineData(DialogueTerminalState.Interrupted, "回答已中断，已保留部分内容。")]
    [InlineData(DialogueTerminalState.Failed, "回答失败，已保留部分内容。")]
    public async Task Transcript_LoadsExactSavedTextReferencesAndTruthfulOutcomeWithoutStreamReplayAsync(DialogueTerminalState state, string label)
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        DialogueTerminalProjection persisted = await h.SeedAsync("SourceSupported 操作；Advisory 动机。😀\n部分读取的限定说明。", state, reference: true);
        await using var attempt = h.Create();
        await attempt.PrepareTranscriptAsync();
        OctoberChatTranscriptSnapshot? snapshot = null;

        await attempt.HandoffTranscriptAsync((value, token) =>
        { token.ThrowIfCancellationRequested(); snapshot = value; return ValueTask.CompletedTask; },
            _ => throw new InvalidOperationException("No suppression expected."));

        Assert.NotNull(snapshot);
        Assert.Same(h.Store.LastTranscript, snapshot.Result);
        DialogueTerminalProjection loaded = Assert.Single(snapshot.Result.Turns);
        Assert.Equal(persisted.TurnId, loaded.TurnId);
        Assert.Equal(persisted.Sequence, loaded.Sequence);
        Assert.Equal(persisted.Question, loaded.Question);
        Assert.Equal(persisted.AnswerText, loaded.AnswerText);
        Assert.Equal(persisted.TerminalState, loaded.TerminalState);
        Assert.Equal(persisted.Reason, loaded.Reason);
        Assert.Equal(persisted.References.ToArray(), loaded.References.ToArray());
        Assert.Equal(h.Owner.Identity, snapshot.Conversation);
        Assert.Equal(h.Context.Current, snapshot.ReadSelection);
        Assert.Equal(h.Store.LastTranscript!.ContextOmitted, snapshot.ContextOmitted);
        var model = OctoberChatTurnViewModel.CreateSaved(h.Binding(persisted), persisted);
        Assert.NotNull(model);
        Assert.Equal(persisted.AnswerText, model.AnswerText);
        Assert.Equal(persisted.References, model.References);
        Assert.Equal(label, model.StatusMessage);
        Assert.True(model.IsTerminal);
        Assert.False(model.IsSaveUncertain);
        Assert.Null(model.ActivityMessage);
        Assert.False(model.Apply(Event(model.Binding, 1, OctoberChatEventKind.TurnStarted)));
        Assert.False(model.Apply(Event(model.Binding, 2, OctoberChatEventKind.AnswerDelta, "不得重放")));
        Assert.Equal(persisted.AnswerText, model.AnswerText);
        Assert.Equal(12, Assert.Single(h.Store.TranscriptRequests).MaximumTurns);
        Assert.Equal(49_152, h.Store.TranscriptRequests[0].MaximumUtf8Bytes);
        Assert.Empty(h.Store.HistoryRequests);
        Assert.Equal(0, h.Store.Mutations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CreateSaved_RejectsDifferentContextTurnModeOrScopeWithoutApplyingProjectionAsync()
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        DialogueTerminalProjection saved = await h.SeedAsync("bound content");
        OctoberChatTurnBinding binding = h.Binding(saved);
        foreach (OctoberChatTurnBinding wrong in new[] { binding with { ContextId = Guid.NewGuid() }, binding with { TurnId = Guid.NewGuid() },
            binding with { Request = binding.Request with { Mode = KnowledgeMode.Business } },
            binding with { Request = binding.Request with { RepositoryScope = [Guid.NewGuid()] } } })
            Assert.Null(OctoberChatTurnViewModel.CreateSaved(wrong, saved));
        Assert.Equal(0, h.Store.Mutations);
        Assert.Empty(h.Store.TranscriptRequests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CreateSaved_BoundSuppressionPreservesTerminalFactsAndCannotBeRestoredByOldEventsAsync()
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        DialogueTerminalProjection saved = await h.SeedAsync("saved content", reference: true);
        var binding = h.Binding(saved);
        var model = OctoberChatTurnViewModel.CreateSaved(binding, saved);
        Assert.NotNull(model);
        Assert.False(model.TrySuppressDisclosure(binding with { ContextId = Guid.NewGuid() }));
        Assert.Equal(saved.AnswerText, model.AnswerText);

        Assert.True(model.TrySuppressDisclosure(binding));

        Assert.Equal(string.Empty, model.AnswerText);
        Assert.Empty(model.References);
        Assert.True(model.IsTerminal);
        Assert.False(model.IsSaveUncertain);
        Assert.False(model.Apply(Event(binding, 100, OctoberChatEventKind.TurnSaved, saved.AnswerText) with
            { SavedOutcome = OctoberChatDisplayOutcome.Completed, References = saved.References }));
        Assert.Equal(string.Empty, model.AnswerText);
        Assert.Equal(0, h.Store.Mutations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Transcript_PreservesActualOwnerEmptyOrphanClosureWithoutInventingInferenceOrOwnerDeathAsync()
    {
        await using var h = await OctoberHistoryFixture.OpenAsync();
        DialogueAdmission running = await h.Owner.AdmitAsync();
        h.Owner.Execution.OwnerDeathStatus = DialogueExecutionStatus.Verified;
        DialogueOrphanResult orphan = await h.Owner.Store.InterruptOrphanAsync(new(Guid.NewGuid(), h.Owner.Identity.ConversationId,
            h.Owner.Identity.ContextId, running.TurnId, running.Request.Execution, running.Fence));
        Assert.Equal(DialogueStatus.Succeeded, orphan.Status);
        Assert.NotNull(orphan.Receipt);
        await using var attempt = h.Create();
        await attempt.PrepareTranscriptAsync();
        OctoberChatTranscriptSnapshot? snapshot = null;

        await attempt.HandoffTranscriptAsync((value, _) => { snapshot = value; return ValueTask.CompletedTask; },
            _ => throw new InvalidOperationException("Generic empty owner closure is not a new disclosure denial."));

        Assert.NotNull(snapshot);
        DialogueTerminalProjection row = Assert.Single(snapshot.Result.Turns);
        Assert.Equal(DialogueTerminalState.Interrupted, row.TerminalState);
        Assert.Equal(DialogueTerminalReason.OwnerLost, row.Reason);
        Assert.Equal(string.Empty, row.Question);
        Assert.Equal(string.Empty, row.AnswerText);
        Assert.Empty(row.References);
        Assert.Null(row.InferenceDependencies);
        var model = OctoberChatTurnViewModel.CreateSaved(h.Binding(row), row);
        Assert.NotNull(model);
        Assert.True(model.IsTerminal);
        Assert.Empty(model.AnswerText);
        Assert.Contains("尚未收到可用内容", model.StatusMessage, StringComparison.Ordinal);
        Assert.Empty(h.Store.HistoryRequests);
        Assert.Equal(0, h.Store.Mutations);
        Assert.Equal(0, h.Dependencies.RootCalls);
    }

    private static OctoberChatEvent Event(OctoberChatTurnBinding binding, long sequence, OctoberChatEventKind kind, string? text = null) =>
        new(binding.ConversationId, binding.ContextId, binding.TurnId, sequence, DateTimeOffset.UnixEpoch.AddSeconds(sequence), kind, text);
}
