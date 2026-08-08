namespace Vulgata.Core.DomainServices;

public interface IPreparedDemoPass2FlowPlanner
{
    Task<ResolvedPreparedPass2Flow?> PlanAsync(
        ExactResolutionProfile profile,
        ExactResolutionRuntimeRequest runtimeRequest,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default);
}
