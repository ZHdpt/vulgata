using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Vulgata.Tests.CodeGraphQualification;

internal sealed record QualificationAttemptLedgerPolicy(
    QualificationExecutionBoundary Boundary,
    string BoundaryRelativePath,
    Func<DateTimeOffset> UtcNow);

internal sealed partial class QualificationAttemptLedger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly string _attemptsRoot;
    private readonly QualificationAttemptLedgerPolicy _policy;
    private readonly QualificationTerminalPublisher _terminalPublisher = new();

    internal QualificationAttemptLedger(string attemptsRoot, QualificationAttemptLedgerPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptsRoot);
        _attemptsRoot = Path.GetFullPath(attemptsRoot);
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    internal async Task<ClaimedProviderAuthorization> ClaimAsync(
        string attemptId,
        string token,
        string exactCommand,
        CancellationToken cancellationToken = default)
    {
        QualificationExecutionBoundary boundary = _policy.Boundary;
        if (!AttemptIdPattern().IsMatch(attemptId) ||
            attemptId != boundary.AttemptId ||
            exactCommand != boundary.ExactCommand)
        {
            throw new InvalidOperationException("The qualification attempt identity is invalid.");
        }

        string directory = Path.Combine(_attemptsRoot, attemptId);
        string authorizationPath = Path.Combine(directory, "authorized.json");
        string startedPath = Path.Combine(directory, "started.json");
        string terminalPath = Path.Combine(directory, "terminal.json");
        if (File.Exists(startedPath) || File.Exists(terminalPath))
        {
            throw new InvalidOperationException("The qualification attempt has already been claimed or completed.");
        }

        FileStream authorizationLock;
        try
        {
            authorizationLock = new FileStream(
                authorizationPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                "The qualification attempt authorization is already active.",
                exception);
        }

        try
        {
            if (File.Exists(startedPath) || File.Exists(terminalPath))
            {
                throw new InvalidOperationException(
                    "The qualification attempt has already been claimed or completed.");
            }

            QualificationAttemptAuthorization authorization =
                await JsonSerializer.DeserializeAsync<QualificationAttemptAuthorization>(
                authorizationLock,
                JsonOptions,
                cancellationToken)
                ?? throw new InvalidOperationException("The qualification authorization is empty.");

            ValidateAuthorization(authorization, attemptId, token);
            QualificationAttemptStarted started = new(
                "1.0",
                attemptId,
                _policy.UtcNow(),
                boundary.ExactCommand,
                _policy.BoundaryRelativePath,
                boundary.RawSha256,
                boundary.CorrectivePlanCommit,
                boundary.PlanReviewPath,
                boundary.PlanReviewSha256,
                boundary.ExecutionApprovalGateId);

            await using (FileStream stream = new(
                startedPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, started, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            return ClaimedProviderAuthorization.Create(attemptId, startedPath, authorizationLock);
        }
        catch
        {
            await authorizationLock.DisposeAsync();
            throw;
        }
    }

    internal async Task CompleteAsync(
        ClaimedProviderAuthorization claim,
        string outcome,
        string category,
        string evidenceCitation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ValidateTerminalValues(outcome, category, evidenceCitation);
        string expectedStartedPath = Path.Combine(_attemptsRoot, claim.AttemptId, "started.json");
        if (!File.Exists(expectedStartedPath) ||
            !string.Equals(Path.GetFullPath(claim.StartedPath), expectedStartedPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The qualification claim is not bound to this attempt ledger.");
        }

        string attemptDirectory = Path.Combine(_attemptsRoot, claim.AttemptId);
        QualificationAttemptTerminalRecord terminal = new(
            "1.0",
            claim.AttemptId,
            _policy.UtcNow(),
            outcome,
            category,
            evidenceCitation);
        try
        {
            await _terminalPublisher.PublishAsync(attemptDirectory, terminal, cancellationToken);
        }
        finally
        {
            claim.Dispose();
        }
    }

    internal async Task<bool> ReconcileAsync(
        string attemptId,
        CancellationToken cancellationToken = default)
    {
        if (attemptId != _policy.Boundary.AttemptId)
        {
            throw new InvalidOperationException("The qualification attempt identity is invalid.");
        }

        string attemptDirectory = Path.Combine(_attemptsRoot, attemptId);
        string authorizationPath = Path.Combine(attemptDirectory, "authorized.json");
        string startedPath = Path.Combine(attemptDirectory, "started.json");
        string terminalPath = Path.Combine(attemptDirectory, "terminal.json");
        if (!File.Exists(authorizationPath))
        {
            if (File.Exists(terminalPath))
            {
                throw new InvalidOperationException(
                    "The qualification attempt terminal is ledger corruption.");
            }

            return false;
        }

        FileStream authorizationLock;
        try
        {
            authorizationLock = new FileStream(
                authorizationPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);
        }
        catch (IOException)
        {
            return false;
        }

        await using (authorizationLock)
        {
            await ValidateAuthorizationForReconciliationAsync(
                authorizationLock,
                attemptId,
                cancellationToken);
            if (File.Exists(terminalPath))
            {
                await _terminalPublisher.ValidateExistingAsync(attemptDirectory, cancellationToken);
                return false;
            }

            string category = File.Exists(startedPath)
                ? "interrupted-after-claim"
                : "interrupted-before-claim";
            QualificationAttemptTerminalRecord terminal = new(
                "1.0",
                attemptId,
                _policy.UtcNow(),
                "failed",
                category,
                "specs/001-codegraph-extraction/qualification/evidence/verification.md");
            await _terminalPublisher.PublishAsync(attemptDirectory, terminal, cancellationToken);
            return true;
        }
    }

    private void ValidateAuthorization(
        QualificationAttemptAuthorization authorization,
        string attemptId,
        string token)
    {
        bool tokenMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(authorization.TokenSha256),
            Encoding.ASCII.GetBytes(Sha256(token)));
        if (authorization.SchemaVersion != "1.0" ||
            authorization.AttemptId != attemptId ||
            !tokenMatches ||
            authorization.ExpiresAt <= _policy.UtcNow() ||
            authorization.ExecutionBoundaryPath != _policy.BoundaryRelativePath ||
            authorization.ExecutionBoundarySha256 != _policy.Boundary.RawSha256)
        {
            throw new InvalidOperationException(
                "The qualification authorization does not match the accepted execution boundary.");
        }
    }

    private async Task ValidateAuthorizationForReconciliationAsync(
        Stream authorizationStream,
        string attemptId,
        CancellationToken cancellationToken)
    {
        QualificationAttemptAuthorization authorization;
        try
        {
            authorization = await JsonSerializer.DeserializeAsync<QualificationAttemptAuthorization>(
                authorizationStream,
                JsonOptions,
                cancellationToken)
                ?? throw new JsonException("The authorization is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The qualification attempt authorization is ledger corruption.",
                exception);
        }

        if (authorization.SchemaVersion != "1.0" ||
            authorization.AttemptId != attemptId ||
            !Sha256Pattern().IsMatch(authorization.TokenSha256) ||
            authorization.ExecutionBoundaryPath != _policy.BoundaryRelativePath ||
            authorization.ExecutionBoundarySha256 != _policy.Boundary.RawSha256)
        {
            throw new InvalidOperationException(
                "The qualification attempt authorization is ledger corruption.");
        }
    }

    private static void ValidateTerminalValues(string outcome, string category, string evidenceCitation)
    {
        if (outcome is not ("passed" or "failed") ||
            string.IsNullOrWhiteSpace(category) ||
            !evidenceCitation.StartsWith(
                "specs/001-codegraph-extraction/qualification/evidence/",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The qualification terminal classification is invalid.");
        }
    }

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [GeneratedRegex("^attempt-[0-9]{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex AttemptIdPattern();

    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    private sealed record QualificationAttemptAuthorization(
        string SchemaVersion,
        string AttemptId,
        string TokenSha256,
        DateTimeOffset ExpiresAt,
        string ExecutionBoundaryPath,
        string ExecutionBoundarySha256);

    private sealed record QualificationAttemptStarted(
        string SchemaVersion,
        string AttemptId,
        DateTimeOffset StartedAt,
        string ExactCommand,
        string ExecutionBoundaryPath,
        string ExecutionBoundarySha256,
        string CorrectivePlanCommit,
        string PlanReviewPath,
        string PlanReviewSha256,
        string ExecutionApprovalGateId);
}
