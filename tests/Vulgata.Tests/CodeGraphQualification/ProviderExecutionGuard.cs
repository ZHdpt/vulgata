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
    internal const string BoundaryPathEnvironmentVariable = "VULGATA_QUALIFICATION_BOUNDARY_PATH";
    internal const string BoundarySha256EnvironmentVariable = "VULGATA_QUALIFICATION_BOUNDARY_SHA256";
    internal const string RepositoryRootEnvironmentVariable = "VULGATA_QUALIFICATION_REPOSITORY_ROOT";

    internal static async Task<ProviderExecutionLease?> TryAcquireAsync(
        string attemptsRoot,
        IReadOnlyDictionary<string, string?> environment,
        Func<DateTimeOffset> utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(utcNow);

        if (!environment.TryGetValue(AttemptIdEnvironmentVariable, out string? attemptId) ||
            !environment.TryGetValue(TokenEnvironmentVariable, out string? token) ||
            !environment.TryGetValue(ExactCommandEnvironmentVariable, out string? exactCommand) ||
            !environment.TryGetValue(BoundaryPathEnvironmentVariable, out string? boundaryPath) ||
            !environment.TryGetValue(BoundarySha256EnvironmentVariable, out string? boundarySha256) ||
            !environment.TryGetValue(RepositoryRootEnvironmentVariable, out string? repositoryRoot) ||
            string.IsNullOrWhiteSpace(attemptId) ||
            string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(exactCommand) ||
            string.IsNullOrWhiteSpace(boundaryPath) ||
            string.IsNullOrWhiteSpace(boundarySha256) ||
            string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return null;
        }

        string root = Path.GetFullPath(repositoryRoot);
        string absoluteBoundaryPath = Path.GetFullPath(
            Path.Combine(root, boundaryPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!absoluteBoundaryPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The qualification execution boundary path escapes the repository root.");
        }

        QualificationExecutionBoundary boundary = await QualificationExecutionBoundary.LoadAsync(
            absoluteBoundaryPath,
            boundarySha256,
            cancellationToken);
        if (attemptId != boundary.AttemptId || exactCommand != boundary.ExactCommand)
        {
            throw new InvalidOperationException(
                "The wrapper environment does not match the qualification execution boundary.");
        }

        QualificationAttemptLedgerPolicy policy = new(boundary, boundaryPath, utcNow);
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
        Func<DateTimeOffset> utcNow,
        Func<ProviderExecutionLease, CancellationToken, Task<ProviderQualificationTerminal>> providerOperation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerOperation);
        ProviderExecutionLease? lease = await TryAcquireAsync(
            attemptsRoot,
            environment,
            utcNow,
            cancellationToken);
        if (lease is null)
        {
            return false;
        }

        using (lease)
        {
            string repositoryRoot = environment[RepositoryRootEnvironmentVariable]!;
            string boundaryPath = environment[BoundaryPathEnvironmentVariable]!;
            string boundarySha256 = environment[BoundarySha256EnvironmentVariable]!;
            QualificationExecutionBoundary boundary = await QualificationExecutionBoundary.LoadAsync(
                Path.Combine(repositoryRoot, boundaryPath.Replace('/', Path.DirectorySeparatorChar)),
                boundarySha256,
                cancellationToken);
            QualificationAttemptLedger ledger = new(
                attemptsRoot,
                new QualificationAttemptLedgerPolicy(boundary, boundaryPath, utcNow));
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
        }

        return true;
    }
}

internal sealed record ProviderQualificationTerminal(string Outcome, string Category);

internal sealed class ClaimedProviderAuthorization : IDisposable
{
    private readonly Stream _authorizationLock;
    private bool _disposed;

    private ClaimedProviderAuthorization(
        string attemptId,
        string startedPath,
        Stream authorizationLock)
    {
        AttemptId = attemptId;
        StartedPath = startedPath;
        _authorizationLock = authorizationLock;
    }

    internal string AttemptId { get; }

    internal string StartedPath { get; }

    internal static ClaimedProviderAuthorization Create(
        string attemptId,
        string startedPath,
        Stream authorizationLock) =>
        new(attemptId, startedPath, authorizationLock);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _authorizationLock.Dispose();
        _disposed = true;
    }
}
