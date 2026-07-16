namespace Vulgata.Tests.CodeGraphQualification;

internal static class ProviderExecutionGuard
{
    internal static ProviderExecutionLease? Issue(ClaimedProviderAuthorization? claimedAuthorization)
    {
        _ = claimedAuthorization;
        return null;
    }
}

internal sealed class ClaimedProviderAuthorization
{
    private ClaimedProviderAuthorization()
    {
    }
}
