using Vulgata.Tests.TestInfrastructure;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests;

public sealed class BoundedRetryWorkerAgentDispatcherTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DispatchAsync_SchemaInvalidThenValid_ReturnsValidSecondAttemptAsync()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        WorkerAgentResultEnvelope schemaInvalid = Result(
            packet,
            WorkerAgentResultOutcome.SchemaInvalid);
        WorkerAgentResultEnvelope valid = Result(
            packet,
            WorkerAgentResultOutcome.Valid);
        var inner = new ScriptedDispatcher(schemaInvalid, valid);
        var dispatcher = new BoundedRetryWorkerAgentDispatcher(inner, maximumAttempts: 3);

        WorkerAgentResultEnvelope result = await dispatcher.DispatchAsync(
            packet,
            new RepositoryAccessScope([packet.RepositoryId]));

        Assert.Equal(WorkerAgentResultOutcome.Valid, result.Outcome);
        Assert.Equal(2, inner.DispatchCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DispatchAsync_RetryableOutcomesExhausted_ReturnsThirdAndStopsAsync()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        var inner = new ScriptedDispatcher(
            Result(packet, WorkerAgentResultOutcome.SchemaInvalid),
            Result(packet, WorkerAgentResultOutcome.Error),
            Result(packet, WorkerAgentResultOutcome.SchemaInvalid));
        var dispatcher = new BoundedRetryWorkerAgentDispatcher(inner, maximumAttempts: 3);

        WorkerAgentResultEnvelope result = await dispatcher.DispatchAsync(
            packet,
            new RepositoryAccessScope([packet.RepositoryId]));

        Assert.Equal(WorkerAgentResultOutcome.SchemaInvalid, result.Outcome);
        Assert.Equal(3, inner.DispatchCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DispatchAsync_Unbounded_DoesNotRetryAsync()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        var inner = new ScriptedDispatcher(
            Result(packet, WorkerAgentResultOutcome.Unbounded),
            Result(packet, WorkerAgentResultOutcome.Valid));
        var dispatcher = new BoundedRetryWorkerAgentDispatcher(inner, maximumAttempts: 3);

        WorkerAgentResultEnvelope result = await dispatcher.DispatchAsync(
            packet,
            new RepositoryAccessScope([packet.RepositoryId]));

        Assert.Equal(WorkerAgentResultOutcome.Unbounded, result.Outcome);
        Assert.Equal(1, inner.DispatchCount);
    }

    private static WorkerAgentResultEnvelope Result(
        WorkerAgentJobPacket packet,
        WorkerAgentResultOutcome outcome)
    {
        DateTimeOffset dispatchedAt = DateTimeOffset.Parse("2026-08-13T00:00:00Z");
        return WorkerAgentResultEnvelope.CreateForJob(
            packet,
            outcome,
            outcome == WorkerAgentResultOutcome.Valid ? null : "模型结果未通过架构校验。",
            dispatchedAt,
            dispatchedAt.AddSeconds(1),
            outcome == WorkerAgentResultOutcome.Valid
                ? WorkerAgentTestFixtures.CreatePass1ResultPayload(packet)
                : null);
    }

    private sealed class ScriptedDispatcher(
        params WorkerAgentResultEnvelope[] results) : IWorkerAgentDispatcher
    {
        private readonly Queue<WorkerAgentResultEnvelope> _results = new(results);

        public int DispatchCount { get; private set; }

        public Task<WorkerAgentResultEnvelope> DispatchAsync(
            WorkerAgentJobPacket packet,
            CancellationToken cancellationToken = default) =>
            DispatchAsync(
                packet,
                new RepositoryAccessScope([packet.RepositoryId]),
                cancellationToken);

        public Task<WorkerAgentResultEnvelope> DispatchAsync(
            WorkerAgentJobPacket packet,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DispatchCount++;
            return Task.FromResult(_results.Dequeue());
        }
    }
}
