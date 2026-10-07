using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.OctoberGenerator;

public enum OctoberCoverageStatus { Complete, Partial, None }
public enum OctoberExecutionStatus { Completed, Failed, Interrupted, Indeterminate }
public enum OctoberPublicationStatus { NotRequested, Confirmed, NoContent, Failed, Indeterminate }

public sealed record OctoberScanTimings(TimeSpan Elapsed, TimeSpan QueueWait,
    TimeSpan ToolWait, TimeSpan ModelWait, TimeSpan StoreWait);

public sealed record OctoberScanUsage(int Requests, long? InputTokens,
    long? OutputTokens, bool HasUnknownUsage);

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
