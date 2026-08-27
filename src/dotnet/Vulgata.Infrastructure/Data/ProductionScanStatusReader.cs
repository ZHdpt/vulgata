using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class ProductionScanStatusReader(VulgataDbContext context) : IProductionScanStatusReader
{
    private const int MaximumFailedFiles = 20;

    public async Task<IReadOnlyList<ProductionScanStatusView>> ListVisibleAsync(
        DurableScanAuthorizationScope scope,
        CancellationToken cancellationToken = default)
    {
        ScanExecution[] executions = await VisibleExecutions(scope)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        var views = new List<ProductionScanStatusView>(executions.Length);
        foreach (ScanExecution execution in executions.OrderByDescending(ObservedAt))
        {
            views.Add(await ProjectAsync(execution, cancellationToken));
        }

        return views;
    }

    public async Task<ProductionScanStatusResult> GetVisibleAsync(
        DurableScanAuthorizationScope scope,
        Guid scanExecutionId,
        CancellationToken cancellationToken = default)
    {
        ScanExecution? execution = await VisibleExecutions(scope)
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == scanExecutionId, cancellationToken);

        return execution is null
            ? ProductionScanStatusResult.Missing()
            : ProductionScanStatusResult.Found(await ProjectAsync(execution, cancellationToken));
    }

    private IQueryable<ScanExecution> VisibleExecutions(DurableScanAuthorizationScope scope)
    {
        Guid[] systemIds = scope.OwnedSystemIds.Distinct().ToArray();
        IQueryable<ScanExecution> query = context.ScanExecutions
            .Where(execution => systemIds.Contains(execution.SystemId));

        if (scope.OwnedRepositoryIds is { } ownedRepositoryIds)
        {
            Guid[] repositoryIds = ownedRepositoryIds.Distinct().ToArray();
            query = query.Where(execution => repositoryIds.Contains(execution.RepositoryId));
        }

        return query;
    }

    private async Task<ProductionScanStatusView> ProjectAsync(
        ScanExecution execution,
        CancellationToken cancellationToken)
    {
        ScanJobExecution[] jobs = await context.ScanJobExecutions
            .AsNoTracking()
            .Where(job => job.ScanExecutionId == execution.Id)
            .ToArrayAsync(cancellationToken);

        ScanJobExecution[] failedJobs = jobs
            .Where(job => job.State == ScanJobExecutionState.Failed)
            .ToArray();
        Guid[] failedFileIds = failedJobs
            .Select(job => job.FileCodeUnitId)
            .Distinct()
            .ToArray();
        string[] failedFiles = failedFileIds.Length == 0
            ? []
            : await context.CodeUnits
                .AsNoTracking()
                .Where(unit => failedFileIds.Contains(unit.Id))
                .Select(unit => unit.FilePath)
                .Distinct()
                .OrderBy(path => path)
                .Take(MaximumFailedFiles)
                .ToArrayAsync(cancellationToken);

        int retryCount = jobs.Count(job => job.State == ScanJobExecutionState.RetryScheduled);
        DateTimeOffset? nextRetryAt = jobs
            .Where(job => job.State == ScanJobExecutionState.RetryScheduled)
            .Select(job => job.NextAttemptAt)
            .Where(value => value.HasValue)
            .Min();
        (string? reasonCode, string? reasonTextZh) = MapReason(execution, jobs);

        return new ProductionScanStatusView(
            execution.Id,
            execution.SystemId,
            execution.RepositoryId,
            MapDisplayState(execution, retryCount),
            jobs.Count(job => job.State == ScanJobExecutionState.Completed),
            jobs.Length,
            jobs.Count(job => job.State == ScanJobExecutionState.Pending),
            jobs.Count(job => job.State == ScanJobExecutionState.Running),
            retryCount,
            failedJobs.Length,
            nextRetryAt,
            reasonCode,
            reasonTextZh,
            execution.Phase is ScanExecutionPhase.PausedProviderUnavailable or
                ScanExecutionPhase.PausedOperatorCorrection,
            failedFiles,
            failedFileIds.Length,
            ObservedAt(execution),
            execution.StateVersion,
            ProductionScanEvidenceEligibility.ProductionPersisted);
    }

    private static ProductionScanDisplayState MapDisplayState(
        ScanExecution execution,
        int retryScheduledJobCount)
    {
        if (retryScheduledJobCount > 0)
        {
            return ProductionScanDisplayState.Retrying;
        }

        return execution.Phase switch
        {
            ScanExecutionPhase.Pass1Planning or ScanExecutionPhase.Pass1Completed =>
                ProductionScanDisplayState.Preparing,
            ScanExecutionPhase.Prepared when execution.StateVersion > 0 =>
                ProductionScanDisplayState.Resumed,
            ScanExecutionPhase.Prepared => ProductionScanDisplayState.Preparing,
            ScanExecutionPhase.Pass1Running or ScanExecutionPhase.Running =>
                ProductionScanDisplayState.Running,
            ScanExecutionPhase.PausedProviderUnavailable or
                ScanExecutionPhase.PausedOperatorCorrection => ProductionScanDisplayState.Paused,
            ScanExecutionPhase.Completed => ProductionScanDisplayState.Completed,
            ScanExecutionPhase.CompletedWithWarnings =>
                ProductionScanDisplayState.CompletedWithWarnings,
            ScanExecutionPhase.FailedIntegrity => ProductionScanDisplayState.FailedIntegrity,
            ScanExecutionPhase.FailedSecurity => ProductionScanDisplayState.FailedSecurity,
            ScanExecutionPhase.Cancelled => ProductionScanDisplayState.Cancelled,
            _ => ProductionScanDisplayState.Failed,
        };
    }

    private static (string? Code, string? TextZh) MapReason(
        ScanExecution execution,
        IReadOnlyList<ScanJobExecution> jobs)
    {
        if (execution.Phase == ScanExecutionPhase.PausedProviderUnavailable)
        {
            return (execution.PauseCode, "LLM 服务暂时不可用，请稍后继续扫描。");
        }

        if (execution.Phase == ScanExecutionPhase.PausedOperatorCorrection)
        {
            return (execution.PauseCode, "扫描配置需要修正后才能继续。");
        }

        if (execution.Phase == ScanExecutionPhase.FailedIntegrity)
        {
            return (execution.FailureCode, "持久化状态或源码版本的完整性校验失败。");
        }

        if (execution.Phase == ScanExecutionPhase.FailedSecurity)
        {
            return (execution.FailureCode, "扫描内容未通过安全校验。");
        }

        if (execution.Phase == ScanExecutionPhase.Cancelled)
        {
            return (execution.FailureCode, "扫描已取消。");
        }

        ScanJobExecution? failedJob = jobs
            .Where(job => job.FailureCategory is not null)
            .OrderBy(job => job.ManifestOrdinal)
            .FirstOrDefault();
        return failedJob?.FailureCategory switch
        {
            ScanJobFailureCategory.Integrity =>
                (failedJob.FailureCode, "文件处理未通过完整性校验。"),
            ScanJobFailureCategory.Security =>
                (failedJob.FailureCode, "文件处理未通过安全校验。"),
            ScanJobFailureCategory.Cancellation =>
                (failedJob.FailureCode, "文件处理已取消。"),
            ScanJobFailureCategory.ProviderTransient or
                ScanJobFailureCategory.ProviderConfiguration =>
                (failedJob.FailureCode, "LLM 服务暂时不可用，请稍后重试。"),
            not null => (failedJob.FailureCode, "部分文件处理失败，请查看扫描状态。"),
            _ when execution.Phase is ScanExecutionPhase.Failed or ScanExecutionPhase.FailedPass1 =>
                (execution.FailureCode ?? execution.TerminalReasonKind?.ToString(), "扫描失败。"),
            _ when execution.Phase == ScanExecutionPhase.CompletedWithWarnings =>
                ("completed-with-warnings", "扫描已完成，但存在警告。"),
            _ => (null, null),
        };
    }

    private static DateTimeOffset ObservedAt(ScanExecution execution) =>
        execution.UpdatedAt ?? execution.TerminalAt ?? execution.StartedAt;
}
