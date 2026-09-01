using Vulgata.Core.DomainServices;
using Vulgata.Shared.Repositories;

namespace Vulgata.Web.Data;

public enum RepositoryProfilingOutcome
{
    Success,
    RepositoryNotFound,
    GitFailure,
    ProviderUnavailable,
    ProviderFailure,
    CandidateAuditBoundExceeded,
}

public sealed record RepositoryProfilingResult(
    RepositoryProfilingOutcome Outcome,
    ScanRunSummaryDto? Value = null,
    string? Message = null);

public interface IRepositoryProfilingCoordinator
{
    Task<RepositoryProfilingResult> StartSystemRepositoryAsync(
        Guid systemId,
        Guid repositoryId,
        IUserContext user,
        CancellationToken cancellationToken = default);

    Task<RepositoryProfilingResult> StartStandaloneRepositoryAsync(
        Guid repositoryId,
        IUserContext user,
        CancellationToken cancellationToken = default);

    Task<ScanRunSummaryDto?> GetSystemRepositoryRunAsync(
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        IUserContext user,
        CancellationToken cancellationToken = default);

    Task<ScanRunSummaryDto?> GetStandaloneRepositoryRunAsync(
        Guid repositoryId,
        Guid scanRunId,
        IUserContext user,
        CancellationToken cancellationToken = default);
}

public sealed class RepositoryProfilingCoordinator : IRepositoryProfilingCoordinator
{
    private const string _message =
        "Legacy Repository profiling is not implemented after CGC decommission.";

    public Task<RepositoryProfilingResult> StartSystemRepositoryAsync(
        Guid systemId,
        Guid repositoryId,
        IUserContext user,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(_message);

    public Task<RepositoryProfilingResult> StartStandaloneRepositoryAsync(
        Guid repositoryId,
        IUserContext user,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(_message);

    public Task<ScanRunSummaryDto?> GetSystemRepositoryRunAsync(
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        IUserContext user,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(_message);

    public Task<ScanRunSummaryDto?> GetStandaloneRepositoryRunAsync(
        Guid repositoryId,
        Guid scanRunId,
        IUserContext user,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(_message);
}
