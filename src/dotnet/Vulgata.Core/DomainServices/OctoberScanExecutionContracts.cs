using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Core.DomainServices.OctoberScan;

public enum OctoberProviderPhase
{
    Worker,
    Business
}

/// <summary>Host identities remain fixed across a file's continuation windows and retries.</summary>
public sealed record OctoberWorkerProviderAttemptRequest(
    Guid RunId,
    Guid FileWorkstreamId,
    Guid AttemptId,
    string ProviderProfileId,
    TimeSpan MaximumDuration,
    long MaximumTokens);

/// <summary>Worker leaf calls share the host admission mechanism used by Business calls.</summary>
public interface IOctoberWorkerExecutionScope
{
    Task<ProviderAdmission> AcquireWorkerAttemptAsync(
        OctoberWorkerProviderAttemptRequest request,
        CancellationToken cancellationToken = default);
}
