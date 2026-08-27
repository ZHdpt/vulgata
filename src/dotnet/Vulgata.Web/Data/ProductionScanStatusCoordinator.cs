using Vulgata.Core.DomainServices;

namespace Vulgata.Web.Data;

public sealed class ProductionScanStatusCoordinator(
    IUserContext userContext,
    ISystemRepository systemRepository,
    IProductionScanStatusReader statusReader,
    IScanRecoveryCoordinator recoveryCoordinator)
{
    public async Task<IReadOnlyList<ProductionScanStatusView>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        DurableScanAuthorizationScope scope = await CreateScopeAsync(cancellationToken);
        return await statusReader.ListVisibleAsync(scope, cancellationToken);
    }

    public async Task<ProductionScanStatusResult> GetAsync(
        Guid scanExecutionId,
        CancellationToken cancellationToken = default)
    {
        DurableScanAuthorizationScope scope = await CreateScopeAsync(cancellationToken);
        return await statusReader.GetVisibleAsync(scope, scanExecutionId, cancellationToken);
    }

    public async Task<ProductionScanResumeResult> ResumeAsync(
        Guid scanExecutionId,
        CancellationToken cancellationToken = default)
    {
        DurableScanAuthorizationScope scope = await CreateScopeAsync(cancellationToken);
        ProductionScanStatusResult current = await statusReader.GetVisibleAsync(
            scope,
            scanExecutionId,
            cancellationToken);
        if (current is not { Outcome: ProductionScanStatusOutcome.Found, View.CanResume: true } ||
            current.View is not { } visible)
        {
            return new ProductionScanResumeResult(
                ProductionScanResumeOutcome.Missing,
                ProductionScanStatusResult.Missing());
        }

        scope = scope with { OwnedRepositoryIds = [visible.RepositoryId] };
        ScanRecoveryResult recovery = await recoveryCoordinator.ResumeAsync(
            scanExecutionId,
            scope,
            cancellationToken);
        ProductionScanStatusResult refreshed = await statusReader.GetVisibleAsync(
            scope,
            scanExecutionId,
            cancellationToken);

        ProductionScanResumeOutcome outcome = recovery switch
        {
            { Outcome: DurableScanStoreOutcome.Applied or DurableScanStoreOutcome.Replay } =>
                ProductionScanResumeOutcome.Applied,
            { Outcome: DurableScanStoreOutcome.Conflict } => ProductionScanResumeOutcome.Conflict,
            { Outcome: DurableScanStoreOutcome.NotFound } or
            { Reason: DurableScanReason.Unauthorized } => ProductionScanResumeOutcome.Missing,
            _ => ProductionScanResumeOutcome.Failed,
        };
        return new ProductionScanResumeResult(outcome, refreshed);
    }

    private async Task<DurableScanAuthorizationScope> CreateScopeAsync(
        CancellationToken cancellationToken)
    {
        var systems = await systemRepository.ListVisibleAsync(
            userContext.ToSystemOwnerFilter(),
            cancellationToken);
        return new DurableScanAuthorizationScope(
            userContext.UserId,
            systems.Select(system => system.Id).ToArray());
    }
}

public enum ProductionScanResumeOutcome
{
    Applied,
    Conflict,
    Missing,
    Failed,
}

public sealed record ProductionScanResumeResult(
    ProductionScanResumeOutcome Outcome,
    ProductionScanStatusResult Status);
