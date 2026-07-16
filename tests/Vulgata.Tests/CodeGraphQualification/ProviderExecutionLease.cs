namespace Vulgata.Tests.CodeGraphQualification;

internal sealed class ProviderExecutionLease
{
    private ProviderExecutionLease(ClaimedProviderAuthorization authorization)
    {
        Authorization = authorization;
    }

    internal ClaimedProviderAuthorization Authorization { get; }

    internal static ProviderExecutionLease Create(ClaimedProviderAuthorization authorization) => new(authorization);
}
