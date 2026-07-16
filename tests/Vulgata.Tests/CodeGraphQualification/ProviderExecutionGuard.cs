namespace Vulgata.Tests.CodeGraphQualification;

internal static class ProviderExecutionGuard
{
    internal static ProviderExecutionLease? Issue(ClaimedProviderAuthorization? claimedAuthorization)
    {
        return claimedAuthorization is null ? null : ProviderExecutionLease.Create(claimedAuthorization);
    }
}

internal sealed class ClaimedProviderAuthorization
{
    private ClaimedProviderAuthorization(string attemptId, string startedPath)
    {
        AttemptId = attemptId;
        StartedPath = startedPath;
    }

    internal string AttemptId { get; }

    internal string StartedPath { get; }

    internal static ClaimedProviderAuthorization Create(string attemptId, string startedPath) => new(attemptId, startedPath);
}
