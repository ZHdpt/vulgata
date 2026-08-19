namespace Vulgata.Core.DomainServices;

public sealed record ScanRecoveryDispatchIdentity(
    string PromptIdentity,
    string DocumentSchemaIdentity);

public interface IScanRecoveryDispatchIdentitySource
{
    Task<ScanRecoveryDispatchIdentity?> GetAsync(
        Guid systemId,
        CancellationToken cancellationToken = default);
}
