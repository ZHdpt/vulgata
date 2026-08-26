using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPreparedPass2Infrastructure(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PreparedPass2Store>();
        services.AddScoped<PreparedPass2FlowPlanner>();
        services.AddScoped<PreparedPass2ExecutionQueryService>();
        services.AddScoped<IPreparedPass2ExecutionQueryService>(provider =>
            provider.GetRequiredService<PreparedPass2ExecutionQueryService>());
        services.AddScoped<IPass2AuthorizationDataReader>(provider =>
            provider.GetRequiredService<PreparedPass2ExecutionQueryService>());
        services.AddScoped<IPreparedPass2DispatchRequestSource,
            PreparedPass2DispatchRequestSource>();
        services.AddScoped<IPass2CodeLogicRetrievalService,
            AuthorizedPass2CodeLogicRetrievalService>();
        services.AddScoped<IPass2BusinessLogicDocumentCommitter,
            AuthorizedPass2BusinessLogicDocumentCommitter>();
        return services;
    }
}

internal sealed class AuthorizedPass2CodeLogicRetrievalService(
    VulgataDbContext context,
    IRevisionBoundSourceReader sourceReader,
    IPass2ExecutionAuthorizationVerifier authorizationVerifier)
    : IPass2CodeLogicRetrievalService
{
    public async Task<Pass2CodeLogicRetrievalResult> RetrieveAsync(
        Guid pass2JobExecutionId,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        RepositoryAccessScope? scope = await Pass2ScopeResolver.ResolveAsync(
            context, authorizationVerifier, pass2JobExecutionId, cancellationToken);
        return scope is null
            ? Pass2CodeLogicRetrievalResult.Refused()
            : await new Pass2CodeLogicRetrievalService(context, scope, sourceReader)
                .RetrieveAsync(pass2JobExecutionId, request, cancellationToken);
    }
}

internal sealed class AuthorizedPass2BusinessLogicDocumentCommitter(
    VulgataDbContext context,
    IPass2ExecutionAuthorizationVerifier authorizationVerifier,
    IPreparedPass2WorkerContractAuthority workerContractAuthority)
    : IPass2BusinessLogicDocumentCommitter
{
    public async Task<Pass2BusinessLogicCommitResult> CommitAsync(
        Pass2BusinessLogicCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        RepositoryAccessScope? scope = await Pass2ScopeResolver.ResolveAsync(
            context,
            authorizationVerifier,
            request.Pass2JobExecutionId,
            cancellationToken);
        return scope is null
            ? Pass2BusinessLogicCommitResult.Rejected()
            : await new Pass2BusinessLogicDocumentCommitter(
                    context, scope, workerContractAuthority)
                .CommitAsync(request, cancellationToken);
    }
}

internal static class Pass2ScopeResolver
{
    internal static async Task<RepositoryAccessScope?> ResolveAsync(
        VulgataDbContext context,
        IPass2ExecutionAuthorizationVerifier authorizationVerifier,
        Guid pass2JobExecutionId,
        CancellationToken cancellationToken)
    {
        if (pass2JobExecutionId == Guid.Empty)
        {
            return null;
        }

        var authority = await context.Pass2JobExecutions.AsNoTracking()
            .Where(job => job.Id == pass2JobExecutionId)
            .Join(
                context.Pass2Executions.AsNoTracking(),
                job => job.Pass2ExecutionId,
                execution => execution.Id,
                (job, execution) => new
                {
                    execution.CoordinationId,
                    execution.SystemId,
                    execution.AuthorizationSubject,
                })
            .Join(
                context.TwoRepositoryScanCoordinations.AsNoTracking(),
                value => value.CoordinationId,
                coordination => coordination.Id,
                (execution, coordination) => new
                {
                    ExecutionSystemId = execution.SystemId,
                    CoordinationSystemId = coordination.SystemId,
                    coordination.RepositoryOneId,
                    coordination.RepositoryTwoId,
                    execution.AuthorizationSubject,
                })
            .SingleOrDefaultAsync(cancellationToken);
        if (authority is null ||
            authority.ExecutionSystemId != authority.CoordinationSystemId ||
            authority.RepositoryOneId == authority.RepositoryTwoId ||
            string.IsNullOrWhiteSpace(authority.AuthorizationSubject))
        {
            return null;
        }

        Guid[] repositoryIds =
            [authority.RepositoryOneId, authority.RepositoryTwoId];
        var verifier = new PersistedPass2AuthorizationVerifier(
            context,
            authorizationVerifier,
            authority.ExecutionSystemId,
            authority.AuthorizationSubject,
            repositoryIds);
        var scope = new RepositoryAccessScope(repositoryIds, verifier);
        return await scope.HasCurrentAccessAsync(repositoryIds, cancellationToken)
            ? scope
            : null;
    }
}

internal sealed class PersistedPass2AuthorizationVerifier(
    VulgataDbContext context,
    IPass2ExecutionAuthorizationVerifier authorizationVerifier,
    Guid systemId,
    string authorizationSubject,
    IReadOnlyCollection<Guid> repositoryIds) : IRepositoryAuthorizationVerifier
{
    public async ValueTask<bool> HasCurrentAccessAsync(
        IReadOnlyCollection<Guid> requiredRepositoryIds,
        CancellationToken cancellationToken = default)
    {
        Guid[] required = requiredRepositoryIds.Order().ToArray();
        Guid[] expected = repositoryIds.Order().ToArray();
        if (!required.SequenceEqual(expected) ||
            string.IsNullOrWhiteSpace(authorizationSubject))
        {
            return false;
        }

        bool repositoriesRemainBound = await context.Repositories.AsNoTracking()
            .CountAsync(value =>
                required.Contains(value.Id) && value.SystemId == systemId,
                cancellationToken) == required.Length;
        return repositoriesRemainBound && await authorizationVerifier.HasCurrentAccessAsync(
            authorizationSubject,
            systemId,
            required,
            cancellationToken);
    }
}
