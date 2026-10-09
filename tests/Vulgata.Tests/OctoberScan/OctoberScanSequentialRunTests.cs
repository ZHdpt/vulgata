using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Actual owner Begin/accept/freeze/publication on owned SQLite, two contained roots and controlled Worker/native adapters.</summary>
public sealed class OctoberScanSequentialRunTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("complete")]
    [InlineData("same-unknown")]
    [InlineData("cancel-active-second")]
    [InlineData("cancel-before-second-worker")]
    [InlineData("shared-budget")]
    [InlineData("stop-before-second")]
    [InlineData("second-acceptance-unknown")]
    public async Task TwoExplicitRepositories_KeepPerFileOwnershipAndOneOriginalRunAsync(string boundary)
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync(twoRepositories: true);
        using var stop = new CancellationTokenSource();
        var repositories = host.Execution.Setup.Snapshot.Request.Repositories.OrderBy(item => item.RepositoryId).ToArray();
        Assert.Equal(2, repositories.Length);
        if (boundary == "stop-before-second") host.BeforeFrame = repository => { if (repository == repositories[1].RepositoryId) stop.Cancel(); };
        var observation = new Observation(boundary);
        int clients = 0;
        var dependencies = host.Dependencies with
        {
            CreateWorkerClient = (_, _) =>
            {
                int file = ++clients;
                if (boundary == "cancel-before-second-worker" && file == 2) stop.Cancel();
                return Task.FromResult<IChatClient>(new OctoberScanGenerateRunnerTests.Harness.Model { IncludeWitnessFreeUnknown = boundary == "same-unknown", SynchronousBeforeResponse = boundary == "cancel-active-second" && file == 2, BeforeResponse = boundary == "cancel-active-second" && file == 2 ? () => stop.Cancel() : null });
            },
            CreateKnowledgeWriter = ports => new Writer(observation, new OctoberKnowledgeWriter(host.Database.Factory, ports.CompletionAuthority,
                ports.SourceVerifier, new Authority(observation, ports.ProducerAuthorization), ports.CommitGuard, host.Database.Log, ports.FreezeAuthority)),
        };
        var execution = host.Execution with { Policy = host.Execution.Policy with
        {
            WorkerCap = 1, MaximumContinuationWindows = 2, MaximumModelRequestsPerFile = 2, MaximumProviderRequests = boundary == "shared-budget" ? 3 : 4,
            MaximumTransientRetries = 0,
        } };
        Assert.True(new OctoberScanPolicyValidator().Validate(execution.Policy).IsValid);
        await using var runner = new OctoberGenerateRunner(dependencies);
        var report = Assert.IsType<OctoberScanReport>((await runner.ExecuteAsync(execution, stop.Token)).Report);
        Assert.Equal(1, host.BeginCalls);
        Assert.Equal(2, report.Inventory.Count(entry => entry.Disposition == "selected"));
        Assert.All(report.Inventory.Where(entry => entry.Disposition == "selected"), entry => Assert.Equal("src/Order.cs", entry.RelativePath));
        Assert.Equal(observation.Submissions.Count, observation.Submissions.Select(item => item.Proposal.AdmissionId).Distinct().Count());
        Assert.Equal(observation.Submissions.Count, observation.Submissions.Select(item => item.Proposal.AttemptId).Distinct().Count());
        Assert.Equal(observation.Submissions.Count, observation.Submissions.Select(item => item.Proposal.Origin.RepositoryId).Distinct().Count());
        foreach (var submission in observation.Submissions)
        {
            var repository = Assert.Single(repositories.Where(item => item.RepositoryId == submission.Proposal.Origin.RepositoryId));
            Assert.Equal(repository.RootBindingIdentity, submission.Proposal.Origin.RootBindingIdentity);
            Assert.Contains(submission.Proposal.Contributors, contributor => contributor.Kind == ContributorKind.Source && contributor.RepositoryId == repository.RepositoryId);
            Assert.DoesNotContain(submission.Proposal.Contributors, contributor => contributor.Kind == ContributorKind.Source && contributor.RepositoryId != repository.RepositoryId);
        }
        if (boundary is "complete" or "same-unknown")
        {
            Assert.Equal(0, report.ExitCode);
            Assert.Equal(4, report.Usage.Requests); // Two real Worker rounds per file; per-file cap2 requires distinct stable workstreams.
            Assert.Equal(2, observation.Submissions.Count);
            Assert.Equal(2, report.AcceptedFiles.Length); Assert.Equal(2, report.TerminalCoverage.Length);
            var frozen = Assert.Single(observation.Freezes);
            Assert.Equal(2, frozen.AcceptedFiles.Length); Assert.Equal(2, frozen.TerminalCoverage.Length);
            Assert.True(repositories.Select(item => item.RepositoryId).ToHashSet().SetEquals(frozen.TerminalCoverage.Select(item => item.Origin.RepositoryId)));
            Assert.Contains(observation.AuthorityInputs, contributors => repositories.All(repository => contributors.Any(item => item.Kind == ContributorKind.Source && item.RepositoryId == repository.RepositoryId)));
            Assert.Equal(2, report.Documents.Length);
            Assert.NotNull(report.Publication);
            if (boundary == "same-unknown")
            {
                var left = Assert.Single(observation.Submissions[0].Proposal.Uncertainties);
                var right = Assert.Single(observation.Submissions[1].Proposal.Uncertainties);
                Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(left), OctoberKnowledgePolicy.SerializeCanonical(right));
                Assert.Equal(2, Assert.Single(observation.Publications).Uncertainties.Length);
            }
        }
        else
        {
            Assert.NotEqual(0, report.ExitCode);
            Assert.Single(report.AcceptedFiles);
            Assert.Equal(repositories[0].RepositoryId, report.AcceptedFiles[0].Coverage.Origin.RepositoryId);
            Assert.Contains(report.TerminalCoverage, coverage => coverage.Origin.RepositoryId == repositories[0].RepositoryId && coverage.Disposition == CoverageDisposition.Accepted);
            Assert.Null(report.Publication); Assert.Empty(observation.Freezes);
            if (boundary == "shared-budget") { Assert.Equal(3, report.Usage.Requests); Assert.Single(observation.Submissions); }
                        if (boundary == "stop-before-second")
            {
                Assert.Equal(3, report.ExitCode); Assert.Equal(2, report.Usage.Requests); Assert.Single(observation.Submissions);
                var gap = Assert.Single(report.Gaps.Where(item => item.Code == "selected-file-unprocessed"));
                Assert.Equal(repositories[1].RepositoryId, gap.RepositoryId); Assert.Equal("src/Order.cs", gap.RelativePath);
            }
            if (boundary is "cancel-active-second" or "cancel-before-second-worker")
            {
                Assert.Equal(3, report.ExitCode);
                var active = Assert.Single(report.TerminalCoverage.Where(item => item.Origin.RepositoryId == repositories[1].RepositoryId));
                Assert.NotEqual(Guid.Empty, active.AdmissionId); Assert.Equal(CoverageDisposition.Cancelled, active.Disposition);
                Assert.DoesNotContain(report.Gaps, item => item.Code == "selected-file-unprocessed" && item.RepositoryId == repositories[1].RepositoryId);
            }
            if (boundary == "second-acceptance-unknown") { Assert.Equal(4, report.ExitCode); Assert.Equal(2, observation.Submissions.Count); Assert.True(observation.Lookups > 0); }
        }
        await using var context = host.Database.Factory.CreateDbContext();
        Assert.Equal(boundary is "complete" or "same-unknown" or "second-acceptance-unknown" ? 2 : 1, await context.AcceptedFileManifests.CountAsync());
        var replay = await runner.ExecuteAsync(execution);
        Assert.Equal(report.ExitCode, replay.Report!.ExitCode);
        Assert.Equal(1, host.BeginCalls);
        Assert.Equal(boundary is "complete" or "same-unknown" or "second-acceptance-unknown" ? 2 : 1, observation.Submissions.Count);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ForeignConcurrentExecution_CannotMutateActiveOriginalFileCoverageAsync()
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync(twoRepositories: true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int clients = 0;
        await using var runner = new OctoberGenerateRunner(host.Dependencies with
        {
            CreateWorkerClient = (_, _) => Task.FromResult<IChatClient>(++clients == 1
                ? new OctoberScanGenerateRunnerTests.Harness.Model { BeforeResponse = () => entered.TrySetResult(), ResponseGate = release.Task }
                : new OctoberScanGenerateRunnerTests.Harness.Model()),
        });
        var execution = host.Execution;
        var original = runner.ExecuteAsync(execution);
        try
        {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, original).WaitAsync(TimeSpan.FromSeconds(5)));
            var foreign = await runner.ExecuteAsync(execution with { });
            Assert.NotEqual(0, foreign.Report!.ExitCode);
            Assert.Contains(foreign.Report.Gaps, gap => gap.Code == "run_binding_unavailable");
            Assert.False(original.IsCompleted);
            release.TrySetResult();
            var report = Assert.IsType<OctoberScanReport>((await original.WaitAsync(TimeSpan.FromSeconds(10))).Report);
            Assert.Equal(0, report.ExitCode); Assert.Equal(2, report.AcceptedFiles.Length); Assert.Equal(2, report.TerminalCoverage.Length);
            Assert.All(report.TerminalCoverage, coverage => Assert.Equal(CoverageDisposition.Accepted, coverage.Disposition));
            Assert.Equal(2, report.TerminalCoverage.Select(coverage => coverage.AdmissionId).Distinct().Count());
            Assert.Equal(1, host.BeginCalls);
        }
        finally { release.TrySetResult(); await original.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
    private sealed class Observation(string boundary)
    {
        internal string Boundary => boundary;
        internal readonly List<CompletedFileSubmission> Submissions = [];
        internal readonly List<CorpusFreezeRequest> Freezes = [];
        internal readonly List<PublicationRequest> Publications = [];
        internal readonly List<ImmutableArray<Contributor>> AuthorityInputs = [];
        internal OperationKey? UnknownOperation;
        internal int Lookups;
    }
    private sealed class Authority(Observation observation, IOctoberKnowledgeProducerAuthorization inner) : IOctoberKnowledgeProducerAuthorization
    {
        public Task<DisclosureStatus> CheckAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken token = default)
        { observation.AuthorityInputs.Add(contributors); return inner.CheckAsync(binding, contributors, token); }
    }
    private sealed class Writer(Observation observation, IOctoberKnowledgeWriter inner) : IOctoberKnowledgeWriter
    {
        public async Task<KnowledgeWriteResult> AcceptCompletedFileAsync(CompletedFileSubmission submission, CancellationToken token = default)
        {
            observation.Submissions.Add(submission);
            var result = await inner.AcceptCompletedFileAsync(submission, token);
            if (observation.Boundary == "second-acceptance-unknown" && observation.Submissions.Count == 2)
            {
                observation.UnknownOperation = submission.Operation;
                return result with { Status = KnowledgeWriteStatus.CommitIndeterminate, AcceptedFile = null };
            }
            return result;
        }
        public Task<KnowledgeWriteResult> LookupOperationAsync(KnowledgeBinding binding, OperationKey operation, string digest, CancellationToken token = default)
        {
            observation.Lookups++;
            return operation == observation.UnknownOperation ? Task.FromResult(new KnowledgeWriteResult(KnowledgeWriteStatus.CommitIndeterminate, operation)) : inner.LookupOperationAsync(binding, operation, digest, token);
        }
        public Task<KnowledgeWriteResult> FreezeCorpusAsync(CorpusFreezeRequest request, CancellationToken token = default)
        { observation.Freezes.Add(request); return inner.FreezeCorpusAsync(request, token); }
        public Task<KnowledgeWriteResult> PublishSelectionAsync(PublicationRequest request, CancellationToken token = default) { observation.Publications.Add(request); return inner.PublishSelectionAsync(request, token); }
    }
}
