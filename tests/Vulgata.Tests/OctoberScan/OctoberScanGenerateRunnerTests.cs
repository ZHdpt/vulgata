using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Infrastructure.Source;
using Vulgata.OctoberGenerator;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.PreparedSource;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Actual Begin/Worker/tools/F1/B0/reader on owned SQLite and contained temporary source; controlled model and authority only.</summary>
public sealed class OctoberScanGenerateRunnerTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing")]
    [InlineData("completed")]
    [InlineData("undefined")]
    public void TerminalSealRefusal_DiagnosticMappingPreservesUnknownAndCompletedDistinction(string condition)
    {
        // Pure host mapping conformance at the terminal no-submission branch; this does not
        // fabricate an actual accepted Worker proposal or claim a real sealing failure occurred.
        OctoberFileWorkerResult? result = condition switch
        {
            "missing" => null,
            "completed" => new(OctoberFileWorkerStatus.Completed),
            _ => new((OctoberFileWorkerStatus)99999) { RetryDisposition = (OctoberWorkerRetryDisposition)99999, FailureStage = (OctoberWorkerFailureStage)99999 },
        };
        var method = typeof(OctoberGenerateRunner).GetMethod("CreateWorkerDiagnostics", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        var diagnostic = Assert.IsType<OctoberWorkerDiagnosticSnapshot>(method.Invoke(null, [result]));
        if (condition == "completed")
        {
            Assert.Equal(OctoberFileWorkerStatus.Completed, diagnostic.Status);
            Assert.Equal(OctoberWorkerRetryDisposition.None, diagnostic.RetryDisposition);
        }
        else { Assert.Null(diagnostic.Status); Assert.Null(diagnostic.RetryDisposition); }
        Assert.Equal(condition == "undefined" ? (OctoberWorkerFailureStage?)null : OctoberWorkerFailureStage.HostSealing, diagnostic.FailureStage);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task InvalidWorkerProposal_DiagnosticOnlyReplayReauthorizesWithoutDispatchAsync(bool terminal, bool budget)
    {
        await using var harness = await Harness.CreateAsync();
        if (terminal) harness.Client.OmitTerminalFinish = true;
        else harness.Client.TerminalTextOverride = budget ? new string('x', 262145) : "{PRIVATE_INVALID_OUTPUT";
        var result = await harness.Runner.ExecuteAsync(harness.Execution);
        var report = Assert.IsType<OctoberScanReport>(result.Report);
        var diagnostic = Assert.IsType<OctoberWorkerDiagnosticSnapshot>(report.WorkerDiagnostics);
        Assert.Equal(budget ? OctoberFileWorkerStatus.BudgetExceeded : OctoberFileWorkerStatus.InvalidOutput, diagnostic.Status);
        Assert.Equal(OctoberWorkerRetryDisposition.None, diagnostic.RetryDisposition);
        Assert.Equal((terminal || budget) ? OctoberWorkerFailureStage.StreamTerminal : OctoberWorkerFailureStage.ProposalParse, diagnostic.FailureStage);
        Assert.Equal(terminal ? OctoberWorkerTerminalFailure.MissingFinish : (OctoberWorkerTerminalFailure?)null, diagnostic.TerminalFailure);
        Assert.Equal(budget ? OctoberWorkerBudgetFailure.OutputBytes : (OctoberWorkerBudgetFailure?)null, diagnostic.BudgetFailure);
        Assert.NotEqual(0, report.ExitCode); Assert.Empty(report.AcceptedFiles); Assert.Null(report.Publication);
        Assert.Equal(0, harness.FreezeCalls); Assert.Equal(2, harness.Client.Calls);
        // Isolate the metadata predicate with the actual observed immutable snapshot.
        var onlyDiagnostic = report with { Publication = null, Documents = [], AcceptedFiles = [], TerminalCoverage = [], Inventory = [] };
        typeof(OctoberGenerateRunner).GetField("_executionTask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(harness.Runner, Task.FromResult(result with { Report = onlyDiagnostic }));
        harness.AuthorityAllowed = false;
        var hidden = await harness.Runner.ExecuteAsync(harness.Execution);
        Assert.Null(hidden.Report!.WorkerDiagnostics); Assert.Null(hidden.Report.Publication); Assert.Empty(hidden.Report.Documents);
        Assert.Same(diagnostic, report.WorkerDiagnostics); Assert.Equal(2, harness.Client.Calls); Assert.Equal(1, harness.BeginCalls);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OneFile_ActualPipelinePublishesReadableCl_AndSameRunDoesNotDispatchTwiceAsync()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Runner.ExecuteAsync(harness.Execution);
        var report = Assert.IsType<OctoberScanReport>(first.Report);
        Assert.Equal(0, report.ExitCode);
        Assert.Equal("cl-only", report.CompletedStage);
        Assert.Null(report.WorkerDiagnostics);
        Assert.Single(report.AcceptedFiles);
        Assert.Single(report.Documents);
        Assert.Equal(2, harness.Client.Calls);
        Assert.True(harness.Client.SawToolResult);
        Assert.Equal(1, harness.SourceOpens);
        Assert.Equal(1, harness.BeginCalls);
        Assert.Equal(2, report.Usage.Requests);
        Assert.False(report.Usage.HasUnknownUsage);
        var replay = await harness.Runner.ExecuteAsync(harness.Execution);
        Assert.Equal(report.Publication, replay.Report!.Publication);
        Assert.Equal(2, harness.Client.Calls);
        Assert.Equal(1, harness.BeginCalls);
        var publication = Assert.IsType<PublicationBinding>(report.Publication);
        var reader = new OctoberKnowledgeReader(new OctoberKnowledgePublishedSource(harness.Database.Factory), publication,
            new B0bReaderDisclosure { CurrentSubject = "ordinary-reader" },
            new OctoberKnowledgeCursorProtector(new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider()), new B0bReadClock()), new B0bReadLog());
        var opened = await reader.OpenReadSelectionAsync(new(publication, new("ordinary-reader", "separate-authentication"), KnowledgeMode.IT, KnowledgePurpose.Reader, [harness.Database.Origin.RepositoryId]));
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        var read = await reader.ReadExactAsync(new(opened.Selection!, report.Documents[0]));
        Assert.Equal(KnowledgeReadStatus.Success, read.Status);
        Assert.NotEmpty(read.Content!);
        await using var context = harness.Database.Factory.CreateDbContext();
        Assert.Equal(1, await context.AcceptedFileManifests.CountAsync());
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InvalidSourceConfiguration_StopsBeforeBeginSourceAndProviderAsync()
    {
        await using var harness = await Harness.CreateAsync();
        harness.ConfigurationValid = false;
        var result = await harness.Runner.ExecuteAsync(harness.Execution);
        Assert.NotEqual(0, result.Report!.ExitCode);
        Assert.Equal(0, harness.BeginCalls);
        Assert.Equal(0, harness.SourceOpens);
        Assert.Equal(0, harness.ClientCreations);
        await harness.Database.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BeginAcknowledgementHeld_NoSourceOrPaidDispatchBeforeConfirmedReceiptAsync()
    {
        await using var harness = await Harness.CreateAsync();
        harness.HoldBegin = true;
        var pending = harness.Runner.ExecuteAsync(harness.Execution);
        try
        {
            await harness.BeginCommitted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(0, harness.SourceOpens);
            Assert.Equal(0, harness.Client.Calls);
            harness.ReleaseBegin.TrySetResult();
            Assert.Equal(0, (await pending.WaitAsync(TimeSpan.FromSeconds(10))).Report!.ExitCode);
        }
        finally { harness.ReleaseBegin.TrySetResult(); await pending; }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnsupportedStageOrParallelism_StopsBeforeBeginAsync(bool parallel)
    {
        await using var harness = await Harness.CreateAsync();
        var policy = parallel ? harness.Execution.Policy with { WorkerCap = 2 } : harness.Execution.Policy with { OutputStage = "cl-and-bl" };
        var result = await harness.Runner.ExecuteAsync(harness.Execution with { Policy = policy });
        Assert.True(result.Report is null || result.Report.ExitCode != 0);
        Assert.Equal(0, harness.BeginCalls);
        Assert.Equal(0, harness.SourceOpens);
        Assert.Equal(0, harness.Client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task MultipleSelectedFiles_AcceptEveryFileWithDistinctOwnershipAsync()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.AddSelectedFileAsync();
        var result = await harness.Runner.ExecuteAsync(harness.Execution);
        Assert.Equal(0, result.Report!.ExitCode);
        Assert.Equal(2, result.Report.AcceptedFiles.Length);
        Assert.Equal(2, result.Report.TerminalCoverage.Length);
        Assert.Equal(2, result.Report.AcceptedFiles.Select(file => file.AdmissionId).Distinct().Count());
        Assert.Equal(new[] { "src/Order.cs", "src/Second.cs" }, result.Report.TerminalCoverage.Select(file => file.Origin.RelativePath).Order(StringComparer.Ordinal).ToArray());
        Assert.All(result.Report.TerminalCoverage, file => Assert.Equal(CoverageDisposition.Accepted, file.Disposition));
        Assert.Equal(2, result.Report.Documents.Length);
        Assert.NotNull(result.Report.Publication);
        await using var context = harness.Database.Factory.CreateDbContext();
        Assert.Equal(2, await context.AcceptedFileManifests.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublicationCommitsThenProducerRevoked_ReportDoesNotReattachInventoryOrAcceptedMetadataAsync()
    {
        await using var harness = await Harness.CreateAsync();
        harness.AfterPublish = () => harness.AuthorityAllowed = false;
        var result = await harness.Runner.ExecuteAsync(harness.Execution);
        await using var context = harness.Database.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
        var report = Assert.IsType<OctoberScanReport>(result.Report);
        Assert.Null(report.Publication);
        Assert.Empty(report.Documents);
        Assert.Empty(report.AcceptedFiles);
        Assert.Empty(report.TerminalCoverage);
        Assert.Empty(report.Inventory);
        Assert.All(report.Gaps, gap => Assert.Null(gap.RelativePath));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostBeginOrAcceptanceAcknowledgement_UnknownLookupPreventsDispatchOrFreezeAsync(bool acceptance)
    {
        await using var harness = await Harness.CreateAsync();
        harness.LoseBeginAcknowledgement = !acceptance;
        harness.LoseAcceptanceAcknowledgement = acceptance;
        var result = await harness.Runner.ExecuteAsync(harness.Execution);
        Assert.Equal(4, result.Report!.ExitCode);
        Assert.Equal(1, harness.BeginCalls);
        Assert.Equal(acceptance ? 2 : 0, harness.Client.Calls);
        if (!acceptance) Assert.Equal(0, harness.SourceOpens);
        Assert.Null(result.Report.Publication);
        Assert.Equal(0, harness.FreezeCalls);
        await using var context = harness.Database.Factory.CreateDbContext();
        Assert.Equal(0, await context.KnowledgePublications.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Module_CancellationAfterKnownPublication_PreservesTerminalReportAsync()
    {
        await using var harness = await Harness.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var module = new OctoberGeneratorModule(harness.Authority, new SelectedTarget(harness.Execution.Setup), harness.Session.ManagementAuthorityId,
            new CancelAfterTerminal(harness.Runner, cancellation), harness.Execution.Provider);
        var result = await module.ExecuteAsync(new(OctoberGeneratorMode.Generate, harness.Execution.Setup.TargetId, harness.Execution.Setup.BatchId, null, harness.Execution.Policy), cancellation.Token);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(OctoberGeneratorExecutionStatus.GenerateFinished, result.Status);
        Assert.Equal(0, result.Report!.ExitCode);
        Assert.NotNull(result.Report.Publication);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CancelledModule_KeepsSessionUntilOwnedCleanupActuallySettlesAsync()
    {
        await using var harness = await Harness.CreateAsync();
        harness.HoldCleanup = true;
        using var cancellation = new CancellationTokenSource();
        var module = new OctoberGeneratorModule(harness.Authority, new SelectedTarget(harness.Execution.Setup), harness.Session.ManagementAuthorityId, harness.Runner, harness.Execution.Provider);
        var pending = module.ExecuteAsync(new(OctoberGeneratorMode.Generate, harness.Execution.Setup.TargetId, harness.Execution.Setup.BatchId, null, harness.Execution.Policy), cancellation.Token);
        try
        {
            await harness.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Task.WhenAny(pending, Task.Delay(100));
            Assert.False(pending.IsCompleted);
            Assert.False(harness.Session.Disposed);
            harness.ReleaseCleanup.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(harness.Session.Disposed);
        }
        finally { harness.ReleaseCleanup.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(3)); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateFactoryResource_AfterCancellation_IsDisposedExactlyOnceWithoutFreezeAsync(bool client)
    {
        await using var harness = await Harness.CreateAsync();
        harness.HoldSourceOpen = !client;
        harness.HoldClientCreation = client;
        using var cancellation = new CancellationTokenSource();
        var pending = harness.Runner.ExecuteAsync(harness.Execution, cancellation.Token);
        try
        {
            await harness.FactoryEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            harness.ReleaseFactory.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(3));
            await (client ? harness.Client.DisposedTask.Task : harness.SourceDisposed.Task).WaitAsync(TimeSpan.FromSeconds(3));
            await harness.Runner.DisposeAsync();
            Assert.Equal(1, client ? harness.Client.DisposeCalls : harness.SourceDisposals);
            Assert.Equal(0, harness.FreezeCalls);
        }
        finally { harness.ReleaseFactory.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(3)); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OwnedToolCleanupFailure_PreventsCorpusFreezeAndPublicationAsync()
    {
        await using var harness = await Harness.CreateAsync();
        harness.FailCleanup = true;
        var result = await harness.Runner.ExecuteAsync(harness.Execution);
        Assert.NotEqual(0, result.Report!.ExitCode);
        Assert.Equal(0, harness.FreezeCalls);
        await using var context = harness.Database.Factory.CreateDbContext();
        Assert.Equal(0, await context.KnowledgePublications.CountAsync());
    }
    private sealed class SelectedTarget(IOctoberHistoricalBoundSetupData setup) : IOctoberConfiguredTarget
    {
        public Task<OctoberGeneratorExecutionResult> ExecuteAsync(OctoberGeneratorCommand command, OctoberAuthorizedSelection authority, CancellationToken token = default)
            => Task.FromResult(new OctoberGeneratorExecutionResult(OctoberGeneratorExecutionStatus.Selected, BoundSetupData: setup));
    }
    private sealed class CancelAfterTerminal(IOctoberGenerateOperation inner, CancellationTokenSource cancellation) : IOctoberGenerateOperation
    {
        public async Task<OctoberGeneratorExecutionResult> ExecuteAsync(OctoberGenerateExecution execution, CancellationToken token = default)
        {
            var result = await inner.ExecuteAsync(execution, token);
            Assert.Equal(0, result.Report!.ExitCode);
            cancellation.Cancel();
            return result;
        }
    }
    internal sealed class Harness : IAsyncDisposable
    {
        internal const string Text = "if (enabled) Run();\nreturn;";
        private readonly PreparedSourceFixture _files;
        private readonly PreparedSourceFixture? _secondFiles;
        private readonly LocalAuthority _authority;
        internal readonly OperatorSession Session = new();
        internal OctoberKnowledgeAcceptanceFixture Database { get; }
        internal OctoberGenerateExecution Execution { get; }
        internal OctoberGenerateRunner Runner { get; }
        internal OctoberGenerateRunnerDependencies Dependencies { get; }
        internal Model Client { get; } = new();
        internal bool ConfigurationValid { get; set; } = true;
        internal bool HoldBegin { get; set; }
        internal bool LoseBeginAcknowledgement, LoseAcceptanceAcknowledgement;
        internal int FreezeCalls;
        internal bool HoldCleanup, FailCleanup, HoldSourceOpen, HoldClientCreation;
        internal int SourceDisposals;
        private readonly List<IOctoberScanRunSource> _ownedSources = [];
        internal TaskCompletionSource CleanupEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseCleanup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource FactoryEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseFactory { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource SourceDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Action? AfterPublish;
        internal Action<Guid>? BeforeFrame;
        internal IOctoberLocalAuthority Authority => _authority;
        internal bool AuthorityAllowed { get => _authority.Allowed; set => _authority.Allowed = value; }
        internal Task AddSelectedFileAsync() => File.WriteAllTextAsync(Path.Combine(_files.PreparedRootPath, "src", "Second.cs"), Text);
        internal int BeginCalls, SourceOpens, ClientCreations;
        internal TaskCompletionSource BeginCommitted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseBegin { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Harness(OctoberKnowledgeAcceptanceFixture database, PreparedSourceFixture files, OctoberSetupRequest setup, OctoberAuthorizedSelection selection, PreparedSourceFixture? secondFiles = null)
        {
            Database = database; _files = files; _secondFiles = secondFiles;
            _authority = new(setup, selection, new(database.Binding.BatchId, database.Origin.RepositoryId, Session.SubjectId, PreparedSourcePurpose.WorkerSourceRead, "controlled-original-approval", database.Origin.RootBindingIdentity, "owned source"), Session);
            var policy = new OctoberScanPolicy { OutputStage = "cl-only", WorkerCap = 1, MaximumTotalTokens = 1000000, MaximumRequestsPerMinute = 1000000,
                SourceRules = [.. setup.Repositories.Select(repository => new OctoberScanSourceRule(repository.RepositoryId, "src", "selected", "Explicit owned source scope"))] };
            Execution = new(Session, new Bound(setup.TargetId, setup.BatchId, new(setup, selection, DateTimeOffset.UtcNow)), selection, policy,
                new("controlled-explicit-provider", "only-source", new("selected", ApiType.ChatCompletions, "https://never-contacted.invalid/v1", "controlled-model", "credential://controlled"), "unused-protected-test-secret"));
            Dependencies = new(_authority, _ => ConfigurationValid,
                (authority, guard) => new Admission(this, new OctoberGenerateAdmissionStore(database.Factory, authority, guard, database.Log)),
                async (_, remaining, token) =>
                {
                    SourceOpens++;
                    Assert.True(BeginCommitted.Task.IsCompletedSuccessfully);
                    IOctoberScanRunSource source = secondFiles is null ? new Source(_files, _authority.Approval, remaining)
                        : new MultipleSources(new Dictionary<Guid, Source>
                        {
                            [_authority.Approval.RepositoryId] = new Source(_files, _authority.Approval, remaining),
                            [setup.Repositories[1].RepositoryId] = new Source(secondFiles, _authority.Approval with { RepositoryId = setup.Repositories[1].RepositoryId, RootBindingIdentity = setup.Repositories[1].RootBindingIdentity, ApprovalIdentity = "controlled-second-approval" }, remaining),
                        });
                    _ownedSources.Add(source);
                    if (HoldSourceOpen) { FactoryEntered.TrySetResult(); await ReleaseFactory.Task; }
                    return new ObservedSource(this, source);
                },
                async (_, _) => { ClientCreations++; if (HoldClientCreation) { FactoryEntered.TrySetResult(); await ReleaseFactory.Task; } return (IChatClient)(secondFiles is null ? Client : new Model()); },
                ports => new ObservedWriter(this, new OctoberKnowledgeWriter(database.Factory, ports.CompletionAuthority, ports.SourceVerifier, ports.ProducerAuthorization, ports.CommitGuard, database.Log, ports.FreezeAuthority)), database.Log);
            Runner = new(Dependencies);
        }
        internal static async Task<Harness> CreateAsync(OctoberKnowledgeAcceptanceFixture? suppliedDatabase = null, bool twoRepositories = false)
        {
            var database = suppliedDatabase ?? await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
            var files = await PreparedSourceFixture.CreateAsync();
            Directory.CreateDirectory(Path.Combine(files.PreparedRootPath, "src"));
            Directory.CreateDirectory(Path.Combine(files.PreparedRootPath, ".codegraph"));
            await File.WriteAllTextAsync(Path.Combine(files.PreparedRootPath, "src", "Order.cs"), Text, new UTF8Encoding(false));
            var repository = new OctoberRepositorySelection(database.Origin.RepositoryId, null, database.Origin.RootBindingIdentity, "owned source");
            PreparedSourceFixture? secondFiles = null;
            var repositories = new List<OctoberRepositorySelection> { repository };
            if (twoRepositories)
            {
                secondFiles = await PreparedSourceFixture.CreateAsync();
                Directory.CreateDirectory(Path.Combine(secondFiles.PreparedRootPath, "src"));
                Directory.CreateDirectory(Path.Combine(secondFiles.PreparedRootPath, ".codegraph"));
                await File.WriteAllTextAsync(Path.Combine(secondFiles.PreparedRootPath, "src", "Order.cs"), Text, new UTF8Encoding(false));
                repositories.Add(new(Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff1"), null, "controlled-second-root", "second owned source"));
            }
            var selectedContext = new OctoberSelectedAdvisoryContext("controlled-authority", [.. repositories],
                [new(repository.RepositoryId, OctoberContextScope.Repository, OctoberContextApplicability.Selected, database.AdvisoryContributor.AdvisoryRecordId, OctoberKnowledgeAcceptanceFixture.AdvisorySnapshot, OctoberContextEvidenceKind.AdvisoryNotSourceProof),
                 new(repository.RepositoryId, OctoberContextScope.Global, OctoberContextApplicability.Selected, database.UncitedContributor.AdvisoryRecordId, OctoberKnowledgeAcceptanceFixture.UncitedSnapshot, OctoberContextEvidenceKind.AdvisoryNotSourceProof)], []);
            var setup = new OctoberSetupRequest(database.Binding.TargetId, database.Binding.BatchId, [.. repositories],
                [new(OctoberContextScope.Repository, database.AdvisoryContributor.AdvisoryRecordId!.Value), new(OctoberContextScope.Global, database.UncitedContributor.AdvisoryRecordId!.Value)], "");
            setup = setup with { SelectionDigest = OctoberSelectionDigest.Compute(setup, selectedContext) };
            var selection = new OctoberAuthorizedSelection(setup.TargetId, setup.BatchId, "owned-operator", database.Binding.CanonicalDatabaseIdentity, [.. repositories.Select(item => item.RepositoryId)], setup.SelectionDigest, selectedContext);
            await using (var context = database.Factory.CreateDbContext())
            {
                var batch = await context.Batches.SingleAsync(); batch.SelectionDigest = setup.SelectionDigest;
                if (twoRepositories) context.RepositorySelections.Add(new() { BatchId = setup.BatchId, RepositoryId = repositories[1].RepositoryId, RootBindingIdentity = repositories[1].RootBindingIdentity, OperatorLabel = repositories[1].OperatorLabel });
                await context.SaveChangesAsync();
            }
            return new(database, files, setup, selection, secondFiles);
        }
        public async ValueTask DisposeAsync()
        {
            ReleaseBegin.TrySetResult(); ReleaseFactory.TrySetResult(); ReleaseCleanup.TrySetResult();
            try { await Runner.DisposeAsync(); }
            catch (InvalidOperationException) when (FailCleanup) { /* Expected unconfirmed cleanup; test owns actual inert resources below. */ }
            finally { foreach (var source in _ownedSources) await source.DisposeAsync(); await _files.DisposeAsync(); if (_secondFiles is not null) await _secondFiles.DisposeAsync(); await Database.DisposeAsync(); }
        }
        private sealed class Admission(Harness owner, IOctoberGenerateAdmissionStore inner) : IOctoberGenerateAdmissionStore
        {
            public async Task<KnowledgeWriteResult> StartFreshExecutionAsync(GenerateAdmissionRequest request, CancellationToken cancellationToken = default)
            {
                owner.BeginCalls++;
                var result = await inner.StartFreshExecutionAsync(request, cancellationToken);
                if (result.Status == KnowledgeWriteStatus.Committed) owner.BeginCommitted.TrySetResult();
                if (owner.HoldBegin) await owner.ReleaseBegin.Task.WaitAsync(cancellationToken);
                return owner.LoseBeginAcknowledgement ? result with { Status = KnowledgeWriteStatus.CommitIndeterminate } : result;
            }
            public Task<KnowledgeWriteResult> LookupAsync(GenerateAdmissionRequest request, CancellationToken cancellationToken = default) => owner.LoseBeginAcknowledgement ? Task.FromResult(new KnowledgeWriteResult(KnowledgeWriteStatus.CommitIndeterminate, OctoberGenerateAdmissionPolicy.OperationFor(request.Binding))) : inner.LookupAsync(request, cancellationToken);
        }
        private sealed class ObservedWriter(Harness owner, IOctoberKnowledgeWriter inner) : IOctoberKnowledgeWriter
        {
            public async Task<KnowledgeWriteResult> AcceptCompletedFileAsync(CompletedFileSubmission submission, CancellationToken cancellationToken = default)
            {
                var result = await inner.AcceptCompletedFileAsync(submission, cancellationToken);
                return owner.LoseAcceptanceAcknowledgement ? result with { Status = KnowledgeWriteStatus.CommitIndeterminate, AcceptedFile = null } : result;
            }
            public Task<KnowledgeWriteResult> LookupOperationAsync(KnowledgeBinding binding, OperationKey operation, string digest, CancellationToken cancellationToken = default)
                => owner.LoseAcceptanceAcknowledgement ? Task.FromResult(new KnowledgeWriteResult(KnowledgeWriteStatus.CommitIndeterminate, operation)) : inner.LookupOperationAsync(binding, operation, digest, cancellationToken);
            public Task<KnowledgeWriteResult> FreezeCorpusAsync(CorpusFreezeRequest request, CancellationToken cancellationToken = default)
            {
                owner.FreezeCalls++;
                return inner.FreezeCorpusAsync(request, cancellationToken);
            }
            public async Task<KnowledgeWriteResult> PublishSelectionAsync(PublicationRequest request, CancellationToken cancellationToken = default)
            {
                var result = await inner.PublishSelectionAsync(request, cancellationToken);
                if (result.Status is KnowledgeWriteStatus.Committed or KnowledgeWriteStatus.AlreadyCommitted) owner.AfterPublish?.Invoke();
                return result;
            }
        }
        internal sealed class OperatorSession : IOctoberOperatorSession
        {
            public string SubjectId => "owned-operator";
            public string ManagementAuthorityId => "controlled-authority";
            public DateTimeOffset ExpiresAtUtc => DateTimeOffset.UtcNow.AddHours(1);
            internal bool Disposed { get; private set; }
            public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
        }
        private sealed record Bound(string TargetId, Guid BatchId, OctoberBoundSetupSnapshot Snapshot) : IOctoberHistoricalBoundSetupData;
        private sealed class LocalAuthority(OctoberSetupRequest setup, OctoberAuthorizedSelection selection, PreparedSourceApproval approval, IOctoberOperatorSession session) : IOctoberLocalAuthority
        {
            internal PreparedSourceApproval Approval => approval;
            internal bool Allowed = true;
            public Task<OctoberAuthenticationResult> AuthenticateAsync(CancellationToken token) => Task.FromResult(new OctoberAuthenticationResult(OctoberAuthenticationStatus.Authenticated, session));
            public Task<OctoberAuthoritySelectionResult> AuthorizeSelectionAsync(IOctoberOperatorSession session, OctoberAuthoritySelectionRequest request, CancellationToken token) => Task.FromResult(new OctoberAuthoritySelectionResult(Allowed ? OctoberAuthorityDecisionStatus.Authorized : OctoberAuthorityDecisionStatus.Denied, Allowed ? setup : null, Allowed ? selection : null));
            public Task<OctoberSourceApprovalResult> IssueSourceApprovalAsync(IOctoberOperatorSession session, IOctoberHistoricalBoundSetupData bound, Guid repository, CancellationToken token) => Task.FromResult(new OctoberSourceApprovalResult(OctoberSourceApprovalStatus.Issued, approval, DateTimeOffset.UtcNow.AddMinutes(5)));
            public Task<PreparedSourceAuthorityResult> VerifySourceApprovalAsync(IOctoberOperatorSession session, string target, PreparedSourceApproval requested, CancellationToken token) => Task.FromResult(new PreparedSourceAuthorityResult(Allowed ? PreparedSourceAuthorityStatus.Current : PreparedSourceAuthorityStatus.Revoked, Allowed ? approval : null));
            public Task<OctoberRevocationStatus> RevokeSourceApprovalAsync(IOctoberOperatorSession session, string target, string identity, CancellationToken token) => Task.FromResult(OctoberRevocationStatus.Revoked);
        }
        private sealed class ObservedSource(Harness owner, IOctoberScanRunSource inner) : IOctoberScanRunSource
        {
            public Task<PreparedSourceResult> ListSourceAsync(Guid repository, PreparedSourceListRequest request, CancellationToken token = default) => inner.ListSourceAsync(repository, request, token);
            public Task<OctoberSourceFrameResult> AcquireFrameAsync(Guid repository, string path, CancellationToken token = default) { owner.BeforeFrame?.Invoke(repository); return inner.AcquireFrameAsync(repository, path, token); }
            public OctoberToolRuntime GetRuntime(Guid repository) => inner.GetRuntime(repository);
            public async ValueTask DisposeAsync()
            {
                owner.SourceDisposals++;
                owner.CleanupEntered.TrySetResult();
                if (owner.HoldCleanup) await owner.ReleaseCleanup.Task;
                if (owner.FailCleanup) throw new InvalidOperationException("Controlled cleanup remains unconfirmed");
                await inner.DisposeAsync();
                owner.SourceDisposed.TrySetResult();
            }
        }
        private sealed class MultipleSources(Dictionary<Guid, Source> sources) : IOctoberScanRunSource
        {
            public Task<PreparedSourceResult> ListSourceAsync(Guid repository, PreparedSourceListRequest request, CancellationToken token = default) => sources[repository].ListSourceAsync(repository, request, token);
            public Task<OctoberSourceFrameResult> AcquireFrameAsync(Guid repository, string path, CancellationToken token = default) => sources[repository].AcquireFrameAsync(repository, path, token);
            public OctoberToolRuntime GetRuntime(Guid repository) => sources[repository].GetRuntime(repository);
            public async ValueTask DisposeAsync() { foreach (var source in sources.Values) await source.DisposeAsync(); }
        }
        private sealed class Source : IOctoberScanRunSource
        {
            private readonly OctoberToolRuntime _runtime;
            private readonly PreparedCodeGraphConnection _descriptor;
            internal Source(PreparedSourceFixture files, PreparedSourceApproval approval, TimeSpan remaining)
            {
                files.Authority.Approve(approval);
                var issuer = new PreparedSourceContextIssuer(files.Authority);
                var descriptor = new PreparedCodeGraphConnection(approval.RepositoryId, Path.Combine(files.SandboxPath, "never-executed.ps1"), "never-executed", [], files.PreparedRootPath, new Dictionary<string, string>(), static () => { }, _ => new OctoberScanToolRuntimeTests.ControlledCodeGraph(approval.RepositoryId));
                _descriptor = descriptor;
                _runtime = new(approval, remaining, new OctoberToolRuntimeDependencies((_, _) => Task.FromResult(descriptor),
                    (_, _, token) => issuer.IssueAsync(new(approval, files.PreparedRootPath, PreparedSourceGrantLimits.Default), descriptor, token),
                    issuer.ObserveFileIdentityAsync, (_, _) => throw new InvalidOperationException("Unexpected native Java"), (owner, _) => owner.OpenSession()));
            }
            public Task<PreparedSourceResult> ListSourceAsync(Guid repository, PreparedSourceListRequest request, CancellationToken token = default) => _runtime.ListAsync(request, token);
            public Task<OctoberSourceFrameResult> AcquireFrameAsync(Guid repository, string path, CancellationToken token = default) => _runtime.AcquireFileFrameAsync(path, cancellationToken: token);
            public OctoberToolRuntime GetRuntime(Guid repository) => _runtime;
            public async ValueTask DisposeAsync() { await _runtime.DisposeAsync(); await _descriptor.DisposeAsync(); }
        }
        internal sealed class Model : IChatClient
        {
            internal int Calls;
            internal int DisposeCalls;
            internal TaskCompletionSource DisposedTask { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal bool SawToolResult;
            internal Action? BeforeResponse;
            internal bool SynchronousBeforeResponse;
            internal Task? ResponseGate;
            internal bool IncludeWitnessFreeUnknown;
            internal string? TerminalTextOverride;
            internal bool OmitTerminalFinish;
            public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                int call = Calls++;
                var history = messages.ToArray();
                if (SynchronousBeforeResponse)
                {
                    // The executor has already recorded dispatch before MoveNext. Complete this
                    // controlled cancellation synchronously so no unfinished iterator is implied.
                    BeforeResponse?.Invoke();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await Task.Yield();
                if (!SynchronousBeforeResponse) BeforeResponse?.Invoke();
                if (ResponseGate is not null) await ResponseGate;
                cancellationToken.ThrowIfCancellationRequested();
                if (call == 0)
                    yield return new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new FunctionCallContent("read-primary", "read_prepared_source", new Dictionary<string, object?> { ["relativePath"] = "src/Order.cs", ["byteOffset"] = 0L, ["byteCount"] = Encoding.UTF8.GetByteCount(Text) }), new UsageContent(new UsageDetails { InputTokenCount = 100, OutputTokenCount = 10 })], FinishReason = ChatFinishReason.ToolCalls };
                else
                {
                    SawToolResult = history.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any();
                    const string json = """{"units":[{"semanticSelector":"file","parentSelector":null,"title":"Conditional execution","anchors":[{"startLine":1,"endLine":2}],"sections":[{"sectionId":"logic","ordinal":0,"title":"Behavior","claims":[{"claimGroupId":"rule","text":"Run executes when enabled.","qualification":"SourceSupported","evidence":[{"startLine":1,"endLine":1}],"limitations":[]}]}]}],"observations":[],"uncertainties":[]}""";
                    yield return new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent(TerminalTextOverride ?? (IncludeWitnessFreeUnknown ? json.Replace("\"uncertainties\":[]", "\"uncertainties\":[{\"key\":\"same-unknown\",\"explanation\":\"Runtime deployment is not established.\",\"evidence\":[],\"limitations\":[]}]") : json)), new UsageContent(new UsageDetails { InputTokenCount = 120, OutputTokenCount = 40 })], FinishReason = OmitTerminalFinish ? null : ChatFinishReason.Stop };
                }
            }
            public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
            public void Dispose() { DisposeCalls++; DisposedTask.TrySetResult(); }
        }
    }
}
