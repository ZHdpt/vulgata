namespace Vulgata.Tests.CodeGraphQualification;

internal sealed class ProviderExecutionLease : IDisposable
{
    private ProviderExecutionLease(ClaimedProviderAuthorization authorization)
    {
        Authorization = authorization;
    }

    internal ClaimedProviderAuthorization Authorization { get; }

    internal static ProviderExecutionLease Create(ClaimedProviderAuthorization authorization) => new(authorization);

    public void Dispose() => Authorization.Dispose();
}
