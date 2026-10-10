using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.CompilerServices;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Analysis.Java;
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
    [InlineData(1000000, true)]
    [InlineData(32768, false)]
    public async Task FixedOutputReservation_GeneratorAllows40960EnvelopeWithoutOverridingSmallerCumulativeBoundAsync(long totalTokens, bool fits)
    {
        await using var host = await Harness.CreateAsync();
        host.Client.FirstReasoning = new string('r', 20000);
        host.Client.TerminalFinishOverride = ChatFinishReason.Length;
        var execution = host.Execution with { Policy = host.Execution.Policy with { MaximumTotalTokens = totalTokens } };
        var result = await host.Runner.ExecuteAsync(execution);
        var report = Assert.IsType<OctoberScanReport>(result.Report);
        var diagnostic = Assert.IsType<OctoberWorkerDiagnosticSnapshot>(report.WorkerDiagnostics);
        Assert.Equal(fits ? OctoberFileWorkerStatus.InvalidOutput : OctoberFileWorkerStatus.BudgetExceeded, diagnostic.Status);
        Assert.Equal(fits ? 2 : 1, host.Client.Calls);
        Assert.All(host.Client.OutputCaps, cap => Assert.Equal(8192, cap));
        if (fits)
        {
            Assert.Equal(OctoberWorkerTerminalFailure.UnsupportedFinish, diagnostic.TerminalFailure);
            Assert.Equal(8192, Assert.IsType<OctoberWorkerTerminalObservation>(diagnostic.TerminalObservation).MaximumOutputTokens);
        }
        else Assert.Null(diagnostic.TerminalObservation);
        Assert.Empty(report.AcceptedFiles); Assert.Null(report.Publication); Assert.Equal(0, host.FreezeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(1000000, true)]
    [InlineData(32768, false)]
    public async Task MaxOutputCap_GeneratorEmitsConfiguredCapAndRejectsInsufficientCumulativeBoundAsync(long totalTokens, bool fits)
    {
        await using var host = await Harness.CreateAsync();
        host.Client.TerminalFinishOverride = ChatFinishReason.Length;
        var execution = host.Execution with { Policy = host.Execution.Policy with { MaximumTotalTokens = totalTokens } };
        await using var runner = new OctoberGenerateRunner(host.Dependencies with { MaximumWorkerOutputTokens = 393216 });

        var result = await runner.ExecuteAsync(execution);
        var report = Assert.IsType<OctoberScanReport>(result.Report);
        Assert.Equal(fits ? OctoberFileWorkerStatus.InvalidOutput : OctoberFileWorkerStatus.BudgetExceeded,
            Assert.IsType<OctoberWorkerDiagnosticSnapshot>(report.WorkerDiagnostics).Status);
        Assert.Equal(fits ? 2 : 0, host.Client.Calls);
        if (fits)
        {
            Assert.All(host.Client.OutputCaps, cap => Assert.Equal(393216, cap));
            var observation = Assert.IsType<OctoberWorkerTerminalObservation>(report.WorkerDiagnostics!.TerminalObservation);
            Assert.Equal(393216, observation.MaximumOutputTokens);
        }
        else
        {
            Assert.Empty(host.Client.OutputCaps);
            Assert.Null(report.WorkerDiagnostics!.TerminalObservation);
        }
        Assert.Empty(report.AcceptedFiles); Assert.Null(report.Publication); Assert.Equal(0, host.FreezeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SerializedToolHistoryAbove32768Bytes_IsAdmittedWithLargeCumulativePolicyAndKeepsOutputCapAsync()
    {
        await using var host = await Harness.CreateAsync();
        host.Client.FirstReasoning = new string('r', 48000);
        host.Client.TerminalFinishOverride = ChatFinishReason.Length;
        await using var runner = new OctoberGenerateRunner(host.Dependencies with { MaximumWorkerOutputTokens = 393216 });

        var result = await runner.ExecuteAsync(host.Execution);
        var report = Assert.IsType<OctoberScanReport>(result.Report);
        var diagnostic = Assert.IsType<OctoberWorkerDiagnosticSnapshot>(report.WorkerDiagnostics);
        Assert.Equal(OctoberFileWorkerStatus.InvalidOutput, diagnostic.Status);
        Assert.Equal(OctoberWorkerFailureStage.StreamTerminal, diagnostic.FailureStage);
        Assert.Equal(OctoberWorkerTerminalFailure.UnsupportedFinish, diagnostic.TerminalFailure);
        Assert.Null(diagnostic.BudgetFailure);
        Assert.True(Encoding.UTF8.GetByteCount(host.Client.FirstReasoning) > 32768);
        Assert.True(host.Client.SawToolResult);
        Assert.Equal(2, host.Client.Calls);
        Assert.All(host.Client.OutputCaps, cap => Assert.Equal(393216, cap));
        Assert.Equal(393216, Assert.IsType<OctoberWorkerTerminalObservation>(diagnostic.TerminalObservation).MaximumOutputTokens);
        Assert.Empty(report.AcceptedFiles);
        Assert.Null(report.Publication);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UnsupportedFinishObservation_ActualRunnerMapsFinalLeafAndReauthorizesDiagnosticReplayAsync()
    {
        await using var host = await Harness.CreateAsync();
        host.Client.TerminalFinishOverride = ChatFinishReason.Length;
        var result = await host.Runner.ExecuteAsync(host.Execution);
        var report = Assert.IsType<OctoberScanReport>(result.Report);
        var diagnostic = Assert.IsType<OctoberWorkerDiagnosticSnapshot>(report.WorkerDiagnostics);
        Assert.Equal(OctoberFileWorkerStatus.InvalidOutput, diagnostic.Status);
        Assert.Equal(OctoberWorkerFailureStage.StreamTerminal, diagnostic.FailureStage);
        Assert.Equal(OctoberWorkerTerminalFailure.UnsupportedFinish, diagnostic.TerminalFailure);
        Assert.Equal(OctoberWorkerRetryDisposition.None, diagnostic.RetryDisposition);
        var observation = Assert.IsType<OctoberWorkerTerminalObservation>(diagnostic.TerminalObservation);
        Assert.Equal(OctoberWorkerUnsupportedFinish.Length, observation.FinishReason);
        Assert.Equal(8192, observation.MaximumOutputTokens);
        Assert.Equal(120L, observation.InputTokens); Assert.Equal(40L, observation.OutputTokens); Assert.Null(observation.ReasoningTokens);
        Assert.NotEqual(0, report.ExitCode); Assert.Empty(report.AcceptedFiles); Assert.Null(report.Publication);
        Assert.Equal(0, host.FreezeCalls); Assert.Equal(2, host.Client.Calls);
        var onlyDiagnostic = report with { Publication = null, Documents = [], AcceptedFiles = [], TerminalCoverage = [], Inventory = [] };
        typeof(OctoberGenerateRunner).GetField("_executionTask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(host.Runner, Task.FromResult(result with { Report = onlyDiagnostic }));
        host.AuthorityAllowed = false;
        var hidden = await host.Runner.ExecuteAsync(host.Execution);
        Assert.Null(hidden.Report!.WorkerDiagnostics);
        Assert.Equal(2, host.Client.Calls); Assert.Equal(1, host.BeginCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FailedJavaOpen_FirstCauseSurvivesSecondaryFaultThenRevocationClearsReplayAsync()
    {
        await using var host = await Harness.CreateAsync();
        host.Client.FirstCallJava = true;
        // Controlled runtime open failure; no native Java process is started.
        host.JavaOpenFailure = new InvalidOperationException("PRIVATE_CONTROLLED_OPEN_FAILURE");
        var result = await host.Runner.ExecuteAsync(host.Execution);
        var report = Assert.IsType<OctoberScanReport>(result.Report);
        var worker = Assert.IsType<OctoberWorkerDiagnosticSnapshot>(report.WorkerDiagnostics);
        var cause = Assert.IsType<OctoberToolDiagnosticSnapshot>(worker.ToolFailure);
        Assert.Equal(SourceMetadataOperation.JavaDefinition, cause.Operation); Assert.Equal(OctoberToolFailureBoundary.RuntimeOpen, cause.Boundary);
        Assert.Null(cause.JavaOpenStatus); Assert.Null(cause.CleanupConfirmed); // Unknown is not confirmed cleanup.
        Assert.NotEqual(0, report.ExitCode); Assert.Null(report.Publication); Assert.Empty(report.AcceptedFiles);
        Assert.Equal(1, host.Client.Calls); Assert.Equal(0, host.FreezeCalls);
        var onlyDiagnostic = report with { Publication = null, Documents = [], AcceptedFiles = [], TerminalCoverage = [], Inventory = [] };
        typeof(OctoberGenerateRunner).GetField("_executionTask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(host.Runner, Task.FromResult(result with { Report = onlyDiagnostic }));
        host.AuthorityAllowed = false;
        var replay = await host.Runner.ExecuteAsync(host.Execution);
        Assert.Null(replay.Report!.WorkerDiagnostics); Assert.Null(replay.Report.Publication); Assert.Empty(replay.Report.Documents);
        Assert.Same(cause, report.WorkerDiagnostics!.ToolFailure); Assert.Equal(1, host.Client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanJavaLimitation_CannotBecomeStaleFirstCauseForLaterFailureAsync(bool nextFile)
    {
        await using var host = await Harness.CreateAsync();
        host.JavaOpenFailure = new PreparedJdtLsOpenFailureException(PreparedJavaNavigationOpenStatus.ReadinessFailed, cleanupConfirmed: true);
        if (nextFile) await host.AddSelectedFileAsync();
        var first = new Harness.Model { FirstCallJava = true, TerminalTextOverride = nextFile ? null : "{PRIVATE_PARSE_FAILURE" };
        var second = new Harness.Model { TerminalTextOverride = "{PRIVATE_PARSE_FAILURE" };
        int creations = 0;
        await using var runner = new OctoberGenerateRunner(host.Dependencies with
        {
            CreateWorkerClient = (_, _) => Task.FromResult<IChatClient>(++creations == 1 ? first : second),
        });
        var result = await runner.ExecuteAsync(host.Execution);
        var report = Assert.IsType<OctoberScanReport>(result.Report);
        Assert.Equal(nextFile ? 2 : 3, first.Calls); Assert.Equal(nextFile ? 2 : 1, creations);
        Assert.Equal(nextFile ? 1 : 0, report.AcceptedFiles.Length);
        var diagnostic = Assert.IsType<OctoberWorkerDiagnosticSnapshot>(report.WorkerDiagnostics);
        Assert.Equal(OctoberFileWorkerStatus.InvalidOutput, diagnostic.Status); Assert.Equal(OctoberWorkerFailureStage.ProposalParse, diagnostic.FailureStage);
        Assert.Null(diagnostic.ToolFailure); Assert.Null(report.Publication); Assert.Equal(0, host.FreezeCalls);
    }
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
        if (terminal || budget) Assert.Null(diagnostic.ParseFailure);
        else
        {
            var parse = Assert.IsType<OctoberWorkerParseDiagnosticSnapshot>(diagnostic.ParseFailure);
            Assert.Equal(OctoberWorkerParseFailure.Syntax, parse.Category);
            Assert.Equal(OctoberWorkerParseSlot.Root, parse.Slot);
        }
        Assert.NotEqual(0, report.ExitCode); Assert.Empty(report.AcceptedFiles); Assert.Null(report.Publication);
        Assert.Equal(0, harness.FreezeCalls); Assert.Equal(terminal || budget ? 2 : 3, harness.Client.Calls);
        // Isolate the metadata predicate with the actual observed immutable snapshot.
        var onlyDiagnostic = report with { Publication = null, Documents = [], AcceptedFiles = [], TerminalCoverage = [], Inventory = [] };
        typeof(OctoberGenerateRunner).GetField("_executionTask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(harness.Runner, Task.FromResult(result with { Report = onlyDiagnostic }));
        harness.AuthorityAllowed = false;
        var hidden = await harness.Runner.ExecuteAsync(harness.Execution);
        Assert.Null(hidden.Report!.WorkerDiagnostics); Assert.Null(hidden.Report.Publication); Assert.Empty(hidden.Report.Documents);
        Assert.Same(diagnostic, report.WorkerDiagnostics); Assert.Equal(terminal || budget ? 2 : 3, harness.Client.Calls); Assert.Equal(1, harness.BeginCalls);
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
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ParseCaptureSink_BindsOriginalSessionAndRun_WritesExactBytesOnceAsync()
    {
        await using var host = await Harness.CreateAsync();
        string parent = Path.Combine(Path.GetTempPath(), "october-capture-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, "capture");
        try
        {
            var sink = new OctoberParseFailureCapture(new(host.Execution.Setup.TargetId, host.Execution.Setup.BatchId,
                host.Session.SubjectId, host.Session.ManagementAuthorityId, destination), []);
            var run = Guid.NewGuid();
            Assert.True(sink.TryBind(host.Execution, run));
            Assert.False(sink.TryBind(host.Execution, Guid.NewGuid()));
            string text = "{SYNTHETIC_CAPTURE_中😀";
            int checks = 0;
            var status = await sink.TryCaptureAsync(host.Execution, run, text, _ => { checks++; return Task.FromResult(true); });
            Assert.Equal(OctoberWorkerCaptureStatus.Captured, status); Assert.True(checks > 0);
            string file = Assert.Single(Directory.GetFiles(destination)); Assert.Equal("final-proposal.txt", Path.GetFileName(file));
            AssertCaptureAcl(new DirectoryInfo(destination).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner));
            AssertCaptureAcl(new FileInfo(file).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner));
            Assert.True((await File.ReadAllBytesAsync(file)).SequenceEqual(Encoding.UTF8.GetBytes(text)));
            Assert.Equal(OctoberWorkerCaptureStatus.AlreadyAttempted,
                await sink.TryCaptureAsync(host.Execution, run, "replacement", _ => Task.FromResult(true)));
            Assert.True((await File.ReadAllBytesAsync(file)).SequenceEqual(Encoding.UTF8.GetBytes(text)));
        }
        finally { Directory.Delete(parent, true); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("wrong-run")]
    [InlineData("foreign-session")]
    [InlineData("revoked")]
    [InlineData("cancelled")]
    [InlineData("oversize")]
    [InlineData("unencodable")]
    public async Task ParseCaptureSink_RejectsWrongAuthorityOrUnboundedTextWithoutAFileAsync(string scenario)
    {
        await using var host = await Harness.CreateAsync();
        string parent = Path.Combine(Path.GetTempPath(), "october-capture-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, "capture");
        try
        {
            var sink = new OctoberParseFailureCapture(new(host.Execution.Setup.TargetId, host.Execution.Setup.BatchId,
                host.Session.SubjectId, host.Session.ManagementAuthorityId, destination), []);
            var run = Guid.NewGuid(); Assert.True(sink.TryBind(host.Execution, run));
            var execution = scenario == "foreign-session" ? host.Execution with { Session = new Harness.OperatorSession() } : host.Execution;
            using var cancellation = new CancellationTokenSource(); if (scenario == "cancelled") cancellation.Cancel();
            string text = scenario switch { "oversize" => new string('中', 87382), "unencodable" => "\uD800", _ => "synthetic" };
            var status = await sink.TryCaptureAsync(execution, scenario == "wrong-run" ? Guid.NewGuid() : run, text,
                _ => Task.FromResult(scenario != "revoked"), cancellation.Token);
            Assert.Equal(scenario switch
            {
                "oversize" => OctoberWorkerCaptureStatus.TooLarge,
                "unencodable" => OctoberWorkerCaptureStatus.Unencodable,
                _ => OctoberWorkerCaptureStatus.Suppressed
            }, status);
            Assert.False(File.Exists(Path.Combine(destination, "final-proposal.txt")));
        }
        finally { Directory.Delete(parent, true); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("target")]
    [InlineData("batch")]
    [InlineData("subject")]
    [InlineData("management")]
    public async Task ParseCaptureSink_ConfigurationIdentityMismatchCannotBindAsync(string field)
    {
        await using var host = await Harness.CreateAsync();
        string destination = Path.Combine(Path.GetTempPath(), "october-capture-unused-" + Guid.NewGuid().ToString("N"));
        var sink = new OctoberParseFailureCapture(new(
            field == "target" ? "foreign-target" : host.Execution.Setup.TargetId,
            field == "batch" ? Guid.NewGuid() : host.Execution.Setup.BatchId,
            field == "subject" ? "foreign-subject" : host.Session.SubjectId,
            field == "management" ? "foreign-authority" : host.Session.ManagementAuthorityId, destination), []);
        Assert.False(sink.TryBind(host.Execution, Guid.NewGuid())); Assert.False(Directory.Exists(destination));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ParseCaptureSink_PreexistingDestinationIsNeverAdoptedOrOverwrittenAsync()
    {
        await using var host = await Harness.CreateAsync();
        string parent = Path.Combine(Path.GetTempPath(), "october-capture-test-" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(parent, "existing"); Directory.CreateDirectory(destination);
        string marker = Path.Combine(destination, "final-proposal.txt"); await File.WriteAllTextAsync(marker, "owned-foreign-marker");
        try
        {
            var sink = new OctoberParseFailureCapture(new(host.Execution.Setup.TargetId, host.Execution.Setup.BatchId,
                host.Session.SubjectId, host.Session.ManagementAuthorityId, destination), []);
            var run = Guid.NewGuid(); Assert.True(sink.TryBind(host.Execution, run));
            var status = await sink.TryCaptureAsync(host.Execution, run, "replacement", _ => Task.FromResult(true));
            Assert.Equal(OctoberWorkerCaptureStatus.Failed, status);
            Assert.True(await File.ReadAllTextAsync(marker) == "owned-foreign-marker"); Assert.Single(Directory.GetFiles(destination));
        }
        finally { Directory.Delete(parent, true); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Runner_ParseCaptureUsesBoundSinkOnceAndRevokedReplayClearsStatusAsync()
    {
        await using var host = await Harness.CreateAsync();
        string parent = Path.Combine(Path.GetTempPath(), "october-capture-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, "capture");
        try
        {
            host.Client.TerminalTextOverride = "{SYNTHETIC_FINAL_CAPTURE";
            var sink = new OctoberParseFailureCapture(new(host.Execution.Setup.TargetId, host.Execution.Setup.BatchId,
                host.Session.SubjectId, host.Session.ManagementAuthorityId, destination), []);
            await using var runner = new OctoberGenerateRunner(host.Dependencies with { ParseFailureCapture = sink });
            var result = await runner.ExecuteAsync(host.Execution);
            Assert.Equal(OctoberWorkerCaptureStatus.Captured, result.Report!.WorkerDiagnostics!.CaptureStatus);
            Assert.Equal(OctoberFileWorkerStatus.InvalidOutput, result.Report.WorkerDiagnostics.Status);
            Assert.True(await File.ReadAllTextAsync(Path.Combine(destination, "final-proposal.txt")) == host.Client.TerminalTextOverride);
            var replay = await runner.ExecuteAsync(host.Execution);
            Assert.Equal(result.Report.WorkerDiagnostics.CaptureStatus, replay.Report!.WorkerDiagnostics!.CaptureStatus); Assert.Equal(result.Report.WorkerDiagnostics.Status, replay.Report.WorkerDiagnostics.Status); Assert.Equal(3, host.Client.Calls);
            host.AuthorityAllowed = false;
            var hidden = await runner.ExecuteAsync(host.Execution);
            Assert.Null(hidden.Report!.WorkerDiagnostics); Assert.Empty(hidden.Report.Documents); Assert.Null(hidden.Report.Publication);
            Assert.Single(Directory.GetFiles(destination)); Assert.Equal(0, host.FreezeCalls);
        }
        finally { Directory.Delete(parent, true); }
    }
    private static void AssertCaptureAcl(FileSystemSecurity security)
    {
        var current = WindowsIdentity.GetCurrent().User!;
        Assert.Equal(current, security.GetOwner(typeof(SecurityIdentifier)));
        Assert.True(security.AreAccessRulesProtected);
        var grants = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>()
            .Where(rule => rule.AccessControlType == AccessControlType.Allow).ToArray();
        Assert.NotEmpty(grants);
        Assert.All(grants, rule => Assert.Equal(current, rule.IdentityReference));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("git-directory")]
    [InlineData("git-file")]
    [InlineData("excluded")]
    [InlineData("ambiguous")]
    [InlineData("reparse")]
    public async Task ParseCaptureSink_UnsafeDestinationNeverReceivesPayloadAsync(string scenario)
    {
        await using var host = await Harness.CreateAsync();
        string parent = Path.Combine(Path.GetTempPath(), "october-capture-security-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(parent);
        string? link = null;
        try
        {
            string boundary = Path.Combine(parent, "boundary"); Directory.CreateDirectory(boundary);
            string marker = Path.Combine(boundary, "foreign-marker.txt"); await File.WriteAllTextAsync(marker, "preserve");
            if (scenario == "git-directory") Directory.CreateDirectory(Path.Combine(boundary, ".git"));
            if (scenario == "git-file") await File.WriteAllTextAsync(Path.Combine(boundary, ".git"), "gitdir: controlled-foreign");
            string destination = Path.Combine(boundary, scenario == "ambiguous" ? "capture." : "capture");
            if (scenario == "reparse")
            {
                link = Path.Combine(parent, "link"); Directory.CreateSymbolicLink(link, boundary);
                destination = Path.Combine(link, "capture");
            }
            var sink = new OctoberParseFailureCapture(new(host.Execution.Setup.TargetId, host.Execution.Setup.BatchId,
                host.Session.SubjectId, host.Session.ManagementAuthorityId, destination), scenario == "excluded" ? [boundary] : []);
            var run = Guid.NewGuid(); Assert.True(sink.TryBind(host.Execution, run));
            var status = await sink.TryCaptureAsync(host.Execution, run, "SYNTHETIC_SECRET", _ => Task.FromResult(true));
            Assert.True(Enum.IsDefined(status)); Assert.NotEqual(OctoberWorkerCaptureStatus.Captured, status);
            Assert.True(await File.ReadAllTextAsync(marker) == "preserve");
            Assert.False(File.Exists(Path.Combine(boundary, "capture", "final-proposal.txt")));
            Assert.False(File.Exists(Path.Combine(destination, "final-proposal.txt")));
        }
        finally
        {
            if (link is not null && Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(parent, true);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseCaptureSink_ConcurrentDuplicateAndLateCancellationRetainSingleOwnedAttemptAsync(bool cancel)
    {
        await using var host = await Harness.CreateAsync();
        string parent = Path.Combine(Path.GetTempPath(), "october-capture-concurrency-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, "capture");
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<OctoberWorkerCaptureStatus>? pending = null;
        try
        {
            var sink = new OctoberParseFailureCapture(new(host.Execution.Setup.TargetId, host.Execution.Setup.BatchId,
                host.Session.SubjectId, host.Session.ManagementAuthorityId, destination), []);
            var run = Guid.NewGuid(); Assert.True(sink.TryBind(host.Execution, run));
            pending = sink.TryCaptureAsync(host.Execution, run, "first", async _ => { entered.TrySetResult(); await release.Task; return true; }, cancellation.Token);
            await Task.WhenAny(entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(3)); Assert.True(entered.Task.IsCompleted);
            Assert.Equal(OctoberWorkerCaptureStatus.AlreadyAttempted,
                await sink.TryCaptureAsync(host.Execution, run, "second", _ => Task.FromResult(true)).WaitAsync(TimeSpan.FromSeconds(3)));
            if (cancel) cancellation.Cancel();
            await Task.WhenAny(pending, Task.Delay(100)); Assert.False(pending.IsCompleted);
            release.TrySetResult();
            Assert.Equal(cancel ? OctoberWorkerCaptureStatus.Suppressed : OctoberWorkerCaptureStatus.Captured,
                await pending.WaitAsync(TimeSpan.FromSeconds(3)));
            if (cancel) Assert.False(Directory.Exists(destination));
            else Assert.True(await File.ReadAllTextAsync(Path.Combine(destination, "final-proposal.txt")) == "first");
        }
        finally
        {
            release.TrySetResult();
            if (pending is not null) await pending.WaitAsync(TimeSpan.FromSeconds(3));
            Directory.Delete(parent, true);
        }
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ParseCaptureSink_CreateNewRacePreservesForeignFileAndReportsCleanupUnconfirmedAsync()
    {
        await using var host = await Harness.CreateAsync();
        string parent = Path.Combine(Path.GetTempPath(), "october-capture-race-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, "capture");
        string file = Path.Combine(destination, "final-proposal.txt"); bool inserted = false;
        try
        {
            var sink = new OctoberParseFailureCapture(new(host.Execution.Setup.TargetId, host.Execution.Setup.BatchId,
                host.Session.SubjectId, host.Session.ManagementAuthorityId, destination), []);
            var run = Guid.NewGuid(); Assert.True(sink.TryBind(host.Execution, run));
            var status = await sink.TryCaptureAsync(host.Execution, run, "SYNTHETIC_CAPTURE_PAYLOAD", async _ =>
            {
                if (Directory.Exists(destination) && !inserted)
                {
                    await File.WriteAllTextAsync(file, "foreign-marker"); inserted = true;
                }
                return true;
            });
            Assert.True(inserted);
            Assert.Equal(OctoberWorkerCaptureStatus.CleanupUnconfirmed, status);
            Assert.True(await File.ReadAllTextAsync(file) == "foreign-marker"); Assert.Single(Directory.GetFiles(destination));
            Assert.Equal(OctoberWorkerCaptureStatus.AlreadyAttempted,
                await sink.TryCaptureAsync(host.Execution, run, "replacement", _ => Task.FromResult(true)));
        }
        finally { Directory.Delete(parent, true); }
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("exact", true)]
    [InlineData("target", false)]
    [InlineData("batch", false)]
    [InlineData("subject", false)]
    [InlineData("management", false)]
    public async Task HttpObservation_BindsOnlyConfiguredOriginalExecutionAndRunAsync(string mismatch, bool allowed)
    {
        await using var host = await Harness.CreateAsync();
        var options = new OctoberWorkerHttpObservationOptions(mismatch == "target" ? "foreign" : host.Execution.Setup.TargetId,
            mismatch == "batch" ? Guid.NewGuid() : host.Execution.Setup.BatchId,
            mismatch == "subject" ? "foreign" : host.Session.SubjectId,
            mismatch == "management" ? "foreign" : host.Session.ManagementAuthorityId);
        var observation = new OctoberWorkerHttpObservation(options); var run = Guid.NewGuid();
        Assert.Equal(allowed, observation.TryBind(host.Execution, run));
        if (allowed)
        {
            Assert.False(observation.TryBind(host.Execution, Guid.NewGuid()));
            Assert.False(observation.TryBind(host.Execution with { Session = new Harness.OperatorSession() }, run));
        }
        observation.Discard(run);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task HttpObservation_ActualFactoryRunnerForwardsFinalFactsAndRevokedReplayClearsThemAsync()
    {
        await using var host = await Harness.CreateAsync();
        var provider = new OctoberSelectedWorkerProvider("explicit-source", "only-source",
            new("selected", ApiType.ChatCompletions, "https://api.deepseek.com", "deepseek-flash", "credential://selected"), "synthetic-selected-cipher");
        var execution = host.Execution with { Provider = provider };
        var observation = new OctoberWorkerHttpObservation(new(execution.Setup.TargetId, execution.Setup.BatchId,
            execution.Session.SubjectId, execution.Session.ManagementAuthorityId));
        using var wire = new CorrectionObservationHttp();
        var factory = new OctoberWorkerClientFactory(new OctoberScanFileWorkerTests.Secrets(), wire, observation);
        await using var runner = new OctoberGenerateRunner(host.Dependencies with
        { HttpObservation = observation, CreateWorkerClient = (selected, token) => factory.CreateAsync(selected, token) });
        var result = await runner.ExecuteAsync(execution);
        var report = Assert.IsType<OctoberScanReport>(result.Report);
        var diagnostic = Assert.IsType<OctoberWorkerDiagnosticSnapshot>(report.WorkerDiagnostics);
        Assert.Equal(OctoberFileWorkerStatus.InvalidOutput, diagnostic.Status);
        var facts = Assert.IsType<OctoberWorkerHttpObservationSnapshot>(diagnostic.HttpObservation);
        Assert.Equal(true, facts.HttpEqualsSdk); Assert.Equal(true, facts.SdkEqualsParse);
        Assert.Equal(Encoding.UTF8.GetByteCount(CorrectionObservationHttp.FinalMalformedText), facts.HttpTextUtf8Bytes);
        Assert.Equal(facts.HttpTextUtf8Bytes, facts.ParseInputUtf8Bytes);
        Assert.Equal(2, wire.Requests.Count); Assert.Equal(0, host.FreezeCalls); Assert.Null(report.Publication);
        var correctionRequest = wire.Requests[1];
        Assert.Equal(8192, correctionRequest.GetProperty("max_tokens").GetInt32());
        Assert.Equal("json_object", correctionRequest.GetProperty("response_format").GetProperty("type").GetString());
        var correctionMessages = correctionRequest.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Contains(correctionMessages, message => message.TryGetProperty("reasoning_content", out var reasoning) &&
            reasoning.GetString() == CorrectionObservationHttp.FirstReasoning &&
            message.TryGetProperty("content", out var content) && content.GetString() == CorrectionObservationHttp.FirstMalformedText);
        Assert.Contains(correctionMessages, message => message.TryGetProperty("role", out var role) && role.GetString() == "user" &&
            message.TryGetProperty("content", out var content) && content.GetString()!.Contains("complete", StringComparison.OrdinalIgnoreCase) &&
            content.GetString()!.Contains("replace", StringComparison.OrdinalIgnoreCase));
        // Retain only the observed diagnostics to exercise the cached-result disclosure fence.
        typeof(OctoberGenerateRunner).GetField("_executionTask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(runner, Task.FromResult(result with { Report = report with { Inventory = [], AcceptedFiles = [], TerminalCoverage = [], Documents = [], Publication = null } }));
        host.AuthorityAllowed = false;
        var hidden = await runner.ExecuteAsync(execution);
        Assert.Null(hidden.Report!.WorkerDiagnostics); Assert.Null(hidden.Report.Publication); Assert.Empty(hidden.Report.Inventory);
        Assert.Equal(2, wire.Requests.Count); Assert.Same(facts, diagnostic.HttpObservation);
    }
    private sealed class SelectedTarget(IOctoberHistoricalBoundSetupData setup) : IOctoberConfiguredTarget
    {
        public Task<OctoberGeneratorExecutionResult> ExecuteAsync(OctoberGeneratorCommand command, OctoberAuthorizedSelection authority, CancellationToken token = default)
            => Task.FromResult(new OctoberGeneratorExecutionResult(OctoberGeneratorExecutionStatus.Selected, BoundSetupData: setup));
    }
    private sealed class CorrectionObservationHttp : HttpMessageHandler
    {
        internal const string FirstMalformedText = "{SYNTHETIC_OBSERVATION_FIRST";
        internal const string FinalMalformedText = "{SYNTHETIC_OBSERVATION_FINAL";
        internal const string FirstReasoning = "SYNTHETIC_OBSERVATION_REASONING";
        internal List<JsonElement> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Requests.Add(body.RootElement.Clone());
            int call = Requests.Count - 1;
            Assert.InRange(call, 0, 1);
            string text = call == 0 ? FirstMalformedText : FinalMalformedText;
            string? reasoning = call == 0 ? FirstReasoning : null;
            string chunk = JsonSerializer.Serialize(new
            {
                id = "controlled-json-correction",
                @object = "chat.completion.chunk",
                created = 1,
                model = "selected-model",
                choices = new[] { new { index = 0, delta = new { role = "assistant", reasoning_content = reasoning, content = text }, finish_reason = "stop" } },
            });
            string usage = JsonSerializer.Serialize(new
            {
                id = "controlled-json-correction",
                @object = "chat.completion.chunk",
                created = 1,
                model = "selected-model",
                choices = Array.Empty<object>(),
                usage = new { prompt_tokens = 20, completion_tokens = 10, total_tokens = 30 }
            });
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("data: " + chunk + "\n\ndata: " + usage + "\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream")
            };
            return response;
        }
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
        internal Exception? JavaOpenFailure;
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
                    IOctoberScanRunSource source = secondFiles is null ? new Source(_files, _authority.Approval, remaining, JavaOpenFailure)
                        : new MultipleSources(new Dictionary<Guid, Source>
                        {
                            [_authority.Approval.RepositoryId] = new Source(_files, _authority.Approval, remaining, JavaOpenFailure),
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
            internal Source(PreparedSourceFixture files, PreparedSourceApproval approval, TimeSpan remaining, Exception? javaOpenFailure = null)
            {
                files.Authority.Approve(approval);
                var issuer = new PreparedSourceContextIssuer(files.Authority);
                var descriptor = new PreparedCodeGraphConnection(approval.RepositoryId, Path.Combine(files.SandboxPath, "never-executed.ps1"), "never-executed", [], files.PreparedRootPath, new Dictionary<string, string>(), static () => { }, _ => new OctoberScanToolRuntimeTests.ControlledCodeGraph(approval.RepositoryId));
                _descriptor = descriptor;
                _runtime = new(approval, remaining, new OctoberToolRuntimeDependencies((_, _) => Task.FromResult(descriptor),
                    (_, _, token) => issuer.IssueAsync(new(approval, files.PreparedRootPath, PreparedSourceGrantLimits.Default), descriptor, token),
                    issuer.ObserveFileIdentityAsync, (_, _) => throw javaOpenFailure ?? new InvalidOperationException("Unexpected native Java"), (owner, _) => owner.OpenSession()));
            }
            public Task<PreparedSourceResult> ListSourceAsync(Guid repository, PreparedSourceListRequest request, CancellationToken token = default) => _runtime.ListAsync(request, token);
            public Task<OctoberSourceFrameResult> AcquireFrameAsync(Guid repository, string path, CancellationToken token = default) => _runtime.AcquireFileFrameAsync(path, cancellationToken: token);
            public OctoberToolRuntime GetRuntime(Guid repository) => _runtime;
            public async ValueTask DisposeAsync() { await _runtime.DisposeAsync(); await _descriptor.DisposeAsync(); }
        }
        internal sealed class Model : IChatClient
        {
            internal int Calls;
            internal string? FirstReasoning;
            internal List<int?> OutputCaps { get; } = [];
            internal int DisposeCalls;
            internal TaskCompletionSource DisposedTask { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal bool SawToolResult;
            internal Action? BeforeResponse;
            internal bool SynchronousBeforeResponse;
            internal Task? ResponseGate;
            internal bool IncludeWitnessFreeUnknown;
            internal string? TerminalTextOverride;
            internal bool OmitTerminalFinish;
            internal ChatFinishReason? TerminalFinishOverride;
            internal bool FirstCallJava;
            public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                int call = Calls++;
                OutputCaps.Add(options?.MaxOutputTokens);
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
                if (call == 0 && FirstReasoning is not null) yield return new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextReasoningContent(FirstReasoning)] };
                if (call == 0)
                    yield return new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new FunctionCallContent("read-primary", FirstCallJava ? "navigate_java_definition" : "read_prepared_source", FirstCallJava ? new Dictionary<string, object?> { ["relativePath"] = "src/Probe.java", ["line"] = 1, ["character"] = 1, ["maximumLocations"] = 5 } : new Dictionary<string, object?> { ["relativePath"] = "src/Order.cs", ["byteOffset"] = 0L, ["byteCount"] = Encoding.UTF8.GetByteCount(Text) }), new UsageContent(new UsageDetails { InputTokenCount = 100, OutputTokenCount = 10 })], FinishReason = ChatFinishReason.ToolCalls };
                else
                {
                    SawToolResult = history.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any();
                    const string json = """{"units":[{"semanticSelector":"file","parentSelector":null,"title":"Conditional execution","anchors":[{"startLine":1,"endLine":2}],"sections":[{"sectionId":"logic","ordinal":0,"title":"Behavior","claims":[{"claimGroupId":"rule","text":"Run executes when enabled.","qualification":"SourceSupported","evidence":[{"startLine":1,"endLine":1}],"limitations":[]}]}]}],"observations":[],"uncertainties":[]}""";
                    yield return new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent(TerminalTextOverride ?? (IncludeWitnessFreeUnknown ? json.Replace("\"uncertainties\":[]", "\"uncertainties\":[{\"key\":\"same-unknown\",\"explanation\":\"Runtime deployment is not established.\",\"evidence\":[],\"limitations\":[]}]") : json)), new UsageContent(new UsageDetails { InputTokenCount = 120, OutputTokenCount = 40 })], FinishReason = OmitTerminalFinish ? null : TerminalFinishOverride ?? ChatFinishReason.Stop };
                }
            }
            public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
            public void Dispose() { DisposeCalls++; DisposedTask.TrySetResult(); }
        }
    }
}
