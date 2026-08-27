namespace Vulgata.Core.DomainServices;

public interface IProductionScanStatusReader
{
    Task<IReadOnlyList<ProductionScanStatusView>> ListVisibleAsync(
        DurableScanAuthorizationScope scope,
        CancellationToken cancellationToken = default);

    Task<ProductionScanStatusResult> GetVisibleAsync(
        DurableScanAuthorizationScope scope,
        Guid scanExecutionId,
        CancellationToken cancellationToken = default);
}

public enum ProductionScanDisplayState
{
    Preparing,
    Running,
    Retrying,
    Paused,
    Resumed,
    Completed,
    CompletedWithWarnings,
    FailedIntegrity,
    FailedSecurity,
    Failed,
    Cancelled,
}

public enum ProductionScanEvidenceEligibility
{
    ProductionPersisted,
}

public enum ProductionScanStatusOutcome
{
    Found,
    Missing,
}

public sealed record ProductionScanStatusView(
    Guid ScanExecutionId,
    Guid SystemId,
    Guid RepositoryId,
    ProductionScanDisplayState DisplayState,
    int CompletedJobCount,
    int TotalJobCount,
    int PendingJobCount,
    int RunningJobCount,
    int RetryScheduledJobCount,
    int FailedJobCount,
    DateTimeOffset? NextRetryAt,
    string? ReasonCode,
    string? ReasonTextZh,
    bool CanResume,
    IReadOnlyList<string> FailedFiles,
    int FailedFileCount,
    DateTimeOffset ObservedAt,
    long StateVersion,
    ProductionScanEvidenceEligibility EvidenceEligibility);

public sealed record ProductionScanStatusResult(
    ProductionScanStatusOutcome Outcome,
    ProductionScanStatusView? View)
{
    public static ProductionScanStatusResult Found(ProductionScanStatusView view) =>
        new(ProductionScanStatusOutcome.Found, view);

    public static ProductionScanStatusResult Missing() =>
        new(ProductionScanStatusOutcome.Missing, null);
}
