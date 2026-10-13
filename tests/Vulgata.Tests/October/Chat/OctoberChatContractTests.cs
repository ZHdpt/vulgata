using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberChat;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Chat;

public sealed class OctoberChatContractTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Limits_Defaults_MatchAcceptedBoundedTurn()
    {
        var limits = new OctoberChatLimits();
        Assert.Equal(8000, limits.QuestionCharacters);
        Assert.Equal(30, limits.ProviderRounds);
        Assert.Equal(64, limits.ToolCalls);
        Assert.Equal(2, limits.ConcurrentTools);
        Assert.Equal(16384, limits.ToolArgumentBytes);
        Assert.Equal(20, limits.PageSize);
        Assert.Equal(65536, limits.ContentBytes);
        Assert.Equal(262144, limits.ToolResultBytes);
        Assert.Equal(65536, limits.OutputBytes);
        Assert.Equal(12, limits.HistoryTurns);
        Assert.Equal(49152, limits.HistoryBytes);
        Assert.Equal(TimeSpan.FromSeconds(540), limits.Duration);
        Assert.Equal(2097152, limits.SseRoundBytes);
        Assert.Equal(8388608, limits.SseTurnBytes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(8000, true)]
    [InlineData(8001, false)]
    [InlineData(0, false)]
    public void TryValidateQuestion_CharacterBound_RejectsWithoutTruncation(int length, bool expected)
    {
        var budget = new OctoberChatBudget();
        Assert.Equal(expected, budget.TryValidateQuestion(new string('问', length)));
        Assert.False(new OctoberChatBudget().TryValidateQuestion("   "));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void TryStartProviderRound_ThirtyRounds_ThirtyFirstStopsWithoutResettingCounters()
    {
        var budget = new OctoberChatBudget();
        for (int index = 1; index <= 30; index++)
        {
            Assert.True(budget.TryStartProviderRound(out int round));
            Assert.Equal(index, round);
        }

        Assert.False(budget.TryStartProviderRound(out _));
        Assert.True(budget.IsStopped);
        Assert.Equal(30, budget.ProviderRounds);
        Assert.False(budget.TryAddOutput("不能继续"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void TryStartToolCall_TotalAndConcurrentLimits_CountRefusalsAndReleaseExactlyOnce()
    {
        var budget = new OctoberChatBudget();
        Assert.True(budget.TryStartToolCall(2, out IDisposable? first));
        Assert.True(budget.TryStartToolCall(2, out IDisposable? second));
        Assert.False(budget.TryStartToolCall(2, out IDisposable? refused));
        Assert.Null(refused);
        Assert.Equal(3, budget.ToolCalls);
        Assert.Equal(2, budget.ActiveTools);
        first!.Dispose();
        first.Dispose();
        Assert.Equal(1, budget.ActiveTools);
        second!.Dispose();
        for (int index = 3; index < 64; index++)
        {
            Assert.True(budget.TryStartToolCall(2, out IDisposable? lease));
            lease!.Dispose();
        }

        Assert.Equal(64, budget.ToolCalls);
        Assert.False(budget.TryStartToolCall(2, out _));
        Assert.True(budget.IsStopped);
        Assert.Equal(0, budget.ActiveTools);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(16385)]
    [InlineData(-1)]
    public void TryStartToolCall_InvalidArgumentByteCount_ConsumesAttemptAndCannotRecoverBudget(int bytes)
    {
        var budget = new OctoberChatBudget();
        Assert.False(budget.TryStartToolCall(bytes, out IDisposable? lease));
        Assert.Null(lease);
        Assert.Equal(1, budget.ToolCalls);
        Assert.Equal(0, budget.ActiveTools);
        Assert.True(budget.IsStopped);
        Assert.False(budget.TryStartToolCall(2, out _));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void TryAddToolResultBytes_ChunkAndAggregateLimits_ExhaustAcrossContinuations()
    {
        var budget = new OctoberChatBudget();
        for (int index = 0; index < 4; index++)
        {
            Assert.True(budget.TryAddToolResultBytes(65536, content: true));
        }

        Assert.Equal(262144, budget.ToolResultBytes);
        Assert.False(budget.TryAddToolResultBytes(1, content: true));
        Assert.Equal(262144, budget.ToolResultBytes);
        Assert.True(budget.IsStopped);
        Assert.False(new OctoberChatBudget().TryAddToolResultBytes(65537, content: true));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("results")]
    [InlineData("sse")]
    public void Budget_NegativeAccounting_CannotSubtractPreviouslyConsumedBytes(string counter)
    {
        var budget = new OctoberChatBudget();
        Assert.True(budget.TryAddToolResultBytes(100));
        Assert.True(budget.TryStartProviderRound(out int round));
        Assert.True(budget.TryAddSseBytes(round, 100));

        Assert.False(counter == "results" ? budget.TryAddToolResultBytes(-1) : budget.TryAddSseBytes(round, -1));

        Assert.Equal(100, budget.ToolResultBytes);
        Assert.Equal(100, budget.SseBytes);
        Assert.True(budget.IsStopped);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void TryAddOutput_Utf8LimitAndMalformedSurrogate_RejectsWholeDeltaWithoutReplacement()
    {
        var budget = new OctoberChatBudget();
        string exact = string.Concat(Enumerable.Repeat("😀", 16384));
        Assert.True(budget.TryAddOutput(exact));
        Assert.Equal(65536, budget.OutputBytes);
        Assert.False(budget.TryAddOutput("中"));
        Assert.Equal(65536, budget.OutputBytes);
        Assert.True(budget.IsStopped);
        var malformed = new OctoberChatBudget();
        Assert.False(malformed.TryAddOutput("\ud800"));
        Assert.Equal(0, malformed.OutputBytes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Budget_OriginalDeadlineAndCancellation_StopAllFurtherAdmission()
    {
        var clock = new ControlledChatFixture.ManualClock();
        var budget = new OctoberChatBudget(timeProvider: clock);
        clock.Advance(TimeSpan.FromSeconds(539));
        Assert.True(budget.TryStartProviderRound(out _));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(budget.IsStopped);
        Assert.False(budget.TryStartToolCall(2, out _));
        Assert.False(budget.TryStartProviderRound(out _));
        using var cancellation = new CancellationTokenSource();
        var cancelled = new OctoberChatBudget(cancellationToken: cancellation.Token);
        cancellation.Cancel();
        Assert.True(cancelled.IsStopped);
        Assert.False(cancelled.TryAddOutput("不应输出"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void TryAddSseBytes_PerRoundAndAggregateLimits_CannotResetAcrossProviderRounds()
    {
        var perRound = new OctoberChatBudget();
        Assert.True(perRound.TryStartProviderRound(out int first));
        Assert.True(perRound.TryAddSseBytes(first, 2097152));
        Assert.False(perRound.TryAddSseBytes(first, 1));
        Assert.Equal(2097152, perRound.SseBytes);
        var aggregate = new OctoberChatBudget();
        for (int index = 0; index < 4; index++)
        {
            Assert.True(aggregate.TryStartProviderRound(out int round));
            Assert.True(aggregate.TryAddSseBytes(round, 2097152));
        }

        Assert.True(aggregate.TryStartProviderRound(out int fifth));
        Assert.False(aggregate.TryAddSseBytes(fifth, 1));
        Assert.Equal(8388608, aggregate.SseBytes);
        Assert.True(aggregate.IsStopped);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void TryAddSseBytes_UnissuedRound_DoesNotInventASeparateAllowance()
    {
        var budget = new OctoberChatBudget();
        Assert.False(budget.TryAddSseBytes(1, 100));
        Assert.Equal(0, budget.SseBytes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Budget_StricterConfiguredLimits_AreAppliedWithoutChangingCanonicalDefaults()
    {
        var budget = new OctoberChatBudget(new OctoberChatLimits { OutputBytes = 3, ToolCalls = 1 });
        Assert.True(budget.TryAddOutput("中"));
        Assert.True(budget.TryStartToolCall(2, out IDisposable? lease));
        lease!.Dispose();
        Assert.False(budget.TryStartToolCall(2, out _));
        Assert.Equal(3, budget.OutputBytes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("toolCalls")]
    [InlineData("concurrency")]
    [InlineData("output")]
    [InlineData("duration")]
    [InlineData("page")]
    public void Budget_NonPositiveConfiguration_IsRejectedBeforeExecution(string invalid)
    {
        OctoberChatLimits limits = invalid switch
        {
            "toolCalls" => new() { ToolCalls = 0 },
            "concurrency" => new() { ConcurrentTools = -1 },
            "output" => new() { OutputBytes = 0 },
            "duration" => new() { Duration = TimeSpan.Zero },
            _ => new() { PageSize = 0 },
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new OctoberChatBudget(limits));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Budget_TrustedDeploymentOverride_IsFixedForTurnAndCannotExpandCanonicalContentBound()
    {
        var configured = new OctoberChatLimits { ProviderRounds = 7, ContentBytes = 100000 };
        var budget = new OctoberChatBudget(configured);
        for (int index = 0; index < 7; index++) Assert.True(budget.TryStartProviderRound(out _));
        configured = configured with { ProviderRounds = 10 };
        Assert.False(budget.TryStartProviderRound(out _));
        Assert.Equal(7, budget.ProviderRounds);
        Assert.False(new OctoberChatBudget(configured).TryAddToolResultBytes(65537, content: true));
        Assert.Equal(30, new OctoberChatLimits().ProviderRounds);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("conversation")]
    [InlineData("context")]
    [InlineData("turn")]
    public void TryAccept_ForeignEnvelope_DoesNotAdvanceCurrentTurnSequence(string foreign)
    {
        var fixture = new ControlledChatFixture();
        var binding = new OctoberChatTurnBinding(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), fixture.Selection(), new ReadSelection("controlled"));
        var sequence = new OctoberChatEventSequence(binding);
        var item = new OctoberChatEvent(binding.ConversationId, binding.ContextId, binding.TurnId, 1, DateTimeOffset.UnixEpoch, OctoberChatEventKind.AnswerDelta, "可见片段");
        OctoberChatEvent other = foreign switch
        {
            "conversation" => item with { ConversationId = Guid.NewGuid(), Sequence = 100 },
            "context" => item with { ContextId = Guid.NewGuid(), Sequence = 100 },
            _ => item with { TurnId = Guid.NewGuid(), Sequence = 100 },
        };

        Assert.False(sequence.TryAccept(other));
        Assert.True(sequence.TryAccept(item));
        Assert.False(sequence.TryAccept(item));
        Assert.False(sequence.TryAccept(item with { Sequence = 0 }));
        Assert.True(sequence.TryAccept(item with { Sequence = 2 }));
        Assert.False(sequence.TryAccept(item));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("kind")]
    [InlineData("missing-time")]
    [InlineData("non-utc")]
    public void TryAccept_InvalidKindOrObservation_DoesNotAdvanceSequence(string invalid)
    {
        var fixture = new ControlledChatFixture();
        var binding = new OctoberChatTurnBinding(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), fixture.Selection(), new ReadSelection("controlled"));
        var sequence = new OctoberChatEventSequence(binding);
        var valid = new OctoberChatEvent(binding.ConversationId, binding.ContextId, binding.TurnId, 1, DateTimeOffset.UnixEpoch, OctoberChatEventKind.AnswerDelta, "片段");
        OctoberChatEvent malformed = invalid switch
        {
            "kind" => valid with { Kind = (OctoberChatEventKind)999, Sequence = 100 },
            "missing-time" => valid with { ObservedAtUtc = default, Sequence = 100 },
            _ => valid with { ObservedAtUtc = DateTimeOffset.UnixEpoch.ToOffset(TimeSpan.FromHours(8)), Sequence = 100 },
        };

        Assert.False(sequence.TryAccept(malformed));
        Assert.True(sequence.TryAccept(valid));
    }
}
