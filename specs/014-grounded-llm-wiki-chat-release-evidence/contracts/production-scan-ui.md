# Contract: Production Scan Status UI

## Authority

The production UI reads only persisted `ScanExecution`, `ScanJobExecution`, safe `FileCodeUnit` metadata, and accepted Pass 2/two-Repository coordination terminal state. `DemoScanUiState`, `DemoScanPage`, its iframe, random counters, and Backup B values are never inputs.

## Read interface

```csharp
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
```

Infrastructure materializes bounded results; Web resolves the current user and visible Systems/Repositories. Missing and unauthorized executions return the same result and no protected counts/reasons.

## Closed display mapping

| Persisted condition | Chinese presentation intent |
| --- | --- |
| prepared/planning/indexing | 准备中 / 建立索引中 |
| running with pending/running jobs | 扫描中 |
| any retry-scheduled job | 正在重试; show earliest next attempt |
| paused provider/operator | 已暂停; bounded reason; `继续扫描` when eligible |
| resumed to prepared | 已恢复，等待继续 |
| completed without warnings | 已完成 |
| completed with warning/failed ordinary jobs/omissions | 已完成，但有警告 |
| integrity failure | 完整性校验失败 |
| security failure | 安全校验失败 |
| other terminal failure/cancellation | 扫描失败 / 已取消 |

Counts are derived from persisted job rows. Reasons are mapped from closed machine codes to bounded Chinese text; raw exceptions/provider responses/source are forbidden. Failed-file summary returns at most twenty safe Repository-relative paths plus the total failed count.

## Resume behavior

`继续扫描` is visible only for a currently recoverable paused execution and a current authorized SystemOwner/Administrator under ManagementAccess. Selection invokes the existing durable recovery coordinator with server-owned scope. Caller state, runtime identity, job counts, or retry time are not accepted. After applied/replay/conflict/failure, the page re-reads persisted state.

Hidden/missing remains missing-equivalent. Stale conflict asks for refresh. Integrity/provider failures use existing bounded Chinese categories. The UI does not directly dispatch a Worker Agent or alter retry/scheduling policy.

## Evidence eligibility

Every view has an internal `ProductionPersisted` origin. Release screenshots must bind to the current candidate/evidence manifest and a real execution ID/state version. Screenshots from `/management/demo-scan`, the demo iframe, or any fake-state origin are rejected.
