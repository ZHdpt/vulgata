using System.Collections.Immutable;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Analysis.Java;

namespace Vulgata.OctoberGenerator;

public enum OctoberCoverageStatus { Complete, Partial, None }
public enum OctoberExecutionStatus { Completed, Failed, Interrupted, Indeterminate }
public enum OctoberPublicationStatus { NotRequested, Confirmed, NoContent, Failed, Indeterminate }
public enum OctoberPhaseStatus { NotRequested, NotStarted, Completed, Partial, NoContent, Failed, Interrupted, Indeterminate, Unavailable }
public enum OctoberBusinessDiagnosticBoundary { OriginalSlot, Finalizer, ExactLookup, ReaderBinding, ExactReader, Completed }
public enum OctoberToolFailureBoundary { ArgumentBinding, RuntimeOpen, RuntimeInvoke, RuntimeSettlement, SourceCurrentness, ResultAdmission }

public sealed record OctoberToolDiagnosticSnapshot(SourceMetadataOperation Operation, OctoberToolFailureBoundary Boundary,
    PreparedJavaNavigationOpenStatus? JavaOpenStatus = null, PreparedJavaNavigationOutcome? JavaOutcome = null,
    PreparedSourceOutcome? SourceOutcome = null, CodeGraphToolInvocationStatus? CodeGraphStatus = null,
    bool? CleanupConfirmed = null);

/// <summary>Bounded host observations; no identifiers, source text or provider payloads.</summary>
public sealed record OctoberBusinessDiagnosticSnapshot(
    BusinessFinalizationStatus? FinalizerStatus,
    KnowledgeWriteStatus? FinalizerPersistenceStatus,
    BusinessFinalizationStatus? LookupStatus,
    KnowledgeWriteStatus? LookupPersistenceStatus,
    OctoberBusinessDiagnosticBoundary Boundary,
    bool OperationMismatch)
{
    public BusinessSynthesisFailureReason? OriginalSynthesisFailureReason { get; init; }
}

public sealed record OctoberScanTimings(TimeSpan Elapsed, TimeSpan QueueWait,
    TimeSpan ToolWait, TimeSpan ModelWait, TimeSpan StoreWait);

public sealed record OctoberScanUsage(int? Requests, long? InputTokens,
    long? OutputTokens, bool HasUnknownUsage);

public sealed record OctoberWorkerDiagnosticSnapshot(OctoberFileWorkerStatus? Status,
    OctoberWorkerRetryDisposition? RetryDisposition, OctoberWorkerFailureStage? FailureStage)
{
    public OctoberWorkerTerminalFailure? TerminalFailure { get; init; }
    public OctoberWorkerBudgetFailure? BudgetFailure { get; init; }
    public OctoberWorkerParseDiagnosticSnapshot? ParseFailure { get; init; }
    public OctoberToolDiagnosticSnapshot? ToolFailure { get; init; }
}

public sealed record OctoberExecutionSnapshot(OctoberScanUsage Usage, TimeSpan QueueWait,
    TimeSpan ModelWait, int OutstandingAttempts, bool HasUnconfirmedSettlement);

/// <summary>Host reason code and scoped location; never exception or provider payload text.</summary>
public sealed record OctoberScanGap(string Code, Guid? RepositoryId = null, string? RelativePath = null);

public sealed record OctoberScanReport(
    string RequestedStage,
    string? CompletedStage,
    OctoberCoverageStatus Coverage,
    OctoberExecutionStatus Execution,
    OctoberPublicationStatus PublicationStatus,
    PublicationBinding? Publication,
    ImmutableArray<DocumentHandle> Documents,
    ImmutableArray<AcceptedFile> AcceptedFiles,
    ImmutableArray<FileCoverage> TerminalCoverage,
    OctoberScanTimings Timings,
    OctoberScanUsage Usage,
    ImmutableArray<OctoberScanGap> Gaps)
{
    public ImmutableArray<OctoberScanInventoryEntry> Inventory { get; init; } = [];
    public OctoberPhaseStatus Matching { get; init; } = RequestedStage == "cl-and-bl" ? OctoberPhaseStatus.NotStarted : OctoberPhaseStatus.NotRequested;
    public OctoberPhaseStatus BusinessLogic { get; init; } = RequestedStage == "cl-and-bl" ? OctoberPhaseStatus.NotStarted : OctoberPhaseStatus.NotRequested;
    public OctoberBusinessDiagnosticSnapshot? BusinessDiagnostics { get; init; }
    public OctoberWorkerDiagnosticSnapshot? WorkerDiagnostics { get; init; }

    public int ExitCode => (Execution, PublicationStatus) switch
    {
        (OctoberExecutionStatus.Indeterminate, _) or (_, OctoberPublicationStatus.Indeterminate) => 4,
        (OctoberExecutionStatus.Interrupted, _) => 3,
        (OctoberExecutionStatus.Completed, OctoberPublicationStatus.NoContent) => 5,
        (OctoberExecutionStatus.Completed, OctoberPublicationStatus.Confirmed)
            when RequestedStage == CompletedStage && Publication is not null && !Documents.IsDefaultOrEmpty =>
            Coverage == OctoberCoverageStatus.Complete ? 0 : 2,
        _ => 1
    };
}
