namespace Vulgata.Tests.CodeGraphQualification;

internal static class ProviderExecutionGuard
{
    internal static ProviderExecutionLease? Issue(ClaimedProviderAuthorization? claimedAuthorization)
    {
        return claimedAuthorization is null ? null : ProviderExecutionLease.Create(claimedAuthorization);
    }
}

internal static class ProviderQualificationEntry
{
    private const string EvidenceCitation =
        "specs/001-codegraph-extraction/qualification/evidence/verification.md";
    internal const string AttemptIdEnvironmentVariable = "VULGATA_QUALIFICATION_ATTEMPT_ID";
    internal const string TokenEnvironmentVariable = "VULGATA_QUALIFICATION_TOKEN";
    internal const string ExactCommandEnvironmentVariable = "VULGATA_QUALIFICATION_EXACT_COMMAND";

    internal static async Task<ProviderExecutionLease?> TryAcquireAsync(
        string attemptsRoot,
        IReadOnlyDictionary<string, string?> environment,
        QualificationAttemptLedgerPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(policy);

        if (!environment.TryGetValue(AttemptIdEnvironmentVariable, out string? attemptId) ||
            !environment.TryGetValue(TokenEnvironmentVariable, out string? token) ||
            !environment.TryGetValue(ExactCommandEnvironmentVariable, out string? exactCommand) ||
            string.IsNullOrWhiteSpace(attemptId) ||
            string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(exactCommand))
        {
            return null;
        }

        if (!string.Equals(attemptId, "attempt-0002", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only the approved attempt-0002 identity may acquire provider capability.");
        }

        QualificationAttemptLedger ledger = new(attemptsRoot, policy);
        ClaimedProviderAuthorization claim = await ledger.ClaimAsync(
            attemptId,
            token,
            exactCommand,
            cancellationToken);
        return ProviderExecutionGuard.Issue(claim)
            ?? throw new InvalidOperationException("The claimed authorization did not produce a provider lease.");
    }

    internal static async Task<bool> ExecuteAsync(
        string attemptsRoot,
        IReadOnlyDictionary<string, string?> environment,
        QualificationAttemptLedgerPolicy policy,
        Func<ProviderExecutionLease, CancellationToken, Task<ProviderQualificationTerminal>> providerOperation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerOperation);
        ProviderExecutionLease? lease = await TryAcquireAsync(
            attemptsRoot,
            environment,
            policy,
            cancellationToken);
        if (lease is null)
        {
            return false;
        }

        QualificationAttemptLedger ledger = new(attemptsRoot, policy);
        try
        {
            ProviderQualificationTerminal terminal = await providerOperation(lease, cancellationToken);
            await ledger.CompleteAsync(
                lease.Authorization,
                terminal.Outcome,
                terminal.Category,
                EvidenceCitation,
                cancellationToken);
        }
        catch
        {
            await ledger.CompleteAsync(
                lease.Authorization,
                "failed",
                "provider-entry-failure",
                EvidenceCitation,
                CancellationToken.None);
            throw;
        }

        return true;
    }
}

internal sealed record ProviderQualificationTerminal(string Outcome, string Category);

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
