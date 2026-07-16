using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Vulgata.Tests.CodeGraphQualification;

internal sealed record QualificationAttemptLedgerPolicy(
    string CorrectivePlanCommit,
    string PlanReviewPath,
    string PlanReviewSha256,
    string WorkflowApprovalObservedAt,
    string WorkflowApprovalSummarySha256,
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
        if (!AttemptIdPattern().IsMatch(attemptId))
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

        QualificationAttemptAuthorization authorization;
        await using (FileStream stream = new(authorizationPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            authorization = await JsonSerializer.DeserializeAsync<QualificationAttemptAuthorization>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("The qualification authorization is empty.");
        }

        ValidateAuthorization(authorization, attemptId, token, exactCommand);
        QualificationAttemptStarted started = new(
            "1.0",
            attemptId,
            _policy.UtcNow(),
            exactCommand,
            _policy.CorrectivePlanCommit,
            _policy.PlanReviewPath,
            _policy.PlanReviewSha256,
            _policy.WorkflowApprovalObservedAt);

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

        return ClaimedProviderAuthorization.Create(attemptId, startedPath);
    }

    internal async Task CompleteAsync(
        ClaimedProviderAuthorization claim,
        string outcome,
        string category,
        string evidenceCitation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (outcome is not ("passed" or "failed") ||
            string.IsNullOrWhiteSpace(category) ||
            !evidenceCitation.StartsWith(
                "specs/001-codegraph-extraction/qualification/evidence/",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The qualification terminal classification is invalid.");
        }

        string expectedStartedPath = Path.Combine(_attemptsRoot, claim.AttemptId, "started.json");
        if (!File.Exists(expectedStartedPath) ||
            !string.Equals(Path.GetFullPath(claim.StartedPath), expectedStartedPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The qualification claim is not bound to this attempt ledger.");
        }

        string terminalPath = Path.Combine(_attemptsRoot, claim.AttemptId, "terminal.json");
        QualificationAttemptTerminal terminal = new(
            "1.0",
            claim.AttemptId,
            _policy.UtcNow(),
            outcome,
            category,
            evidenceCitation);
        try
        {
            await using FileStream stream = new(
                terminalPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough);
            await JsonSerializer.SerializeAsync(stream, terminal, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);
        }
        catch (IOException exception) when (File.Exists(terminalPath))
        {
            throw new InvalidOperationException("The qualification attempt already has a terminal record.", exception);
        }
    }

    private void ValidateAuthorization(
        QualificationAttemptAuthorization authorization,
        string attemptId,
        string token,
        string exactCommand)
    {
        DateTimeOffset now = _policy.UtcNow();
        bool tokenMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(authorization.TokenSha256),
            Encoding.ASCII.GetBytes(Sha256(token)));
        if (authorization.SchemaVersion != "1.0" ||
            authorization.AttemptId != attemptId ||
            !tokenMatches ||
            authorization.ExpiresAt <= now ||
            authorization.ExactCommand != exactCommand ||
            authorization.CorrectivePlanCommit != _policy.CorrectivePlanCommit ||
            authorization.PlanReviewPath != _policy.PlanReviewPath ||
            authorization.PlanReviewSha256 != _policy.PlanReviewSha256 ||
            authorization.WorkflowApprovalObservedAt != _policy.WorkflowApprovalObservedAt ||
            authorization.WorkflowApprovalSummarySha256 != _policy.WorkflowApprovalSummarySha256)
        {
            throw new InvalidOperationException("The qualification authorization does not match the accepted execution boundary.");
        }
    }

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [GeneratedRegex("^attempt-[0-9]{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex AttemptIdPattern();

    private sealed record QualificationAttemptAuthorization(
        string SchemaVersion,
        string AttemptId,
        string TokenSha256,
        DateTimeOffset ExpiresAt,
        string ExactCommand,
        string CorrectivePlanCommit,
        string PlanReviewPath,
        string PlanReviewSha256,
        string WorkflowApprovalObservedAt,
        string WorkflowApprovalSummarySha256);

    private sealed record QualificationAttemptStarted(
        string SchemaVersion,
        string AttemptId,
        DateTimeOffset StartedAt,
        string ExactCommand,
        string CorrectivePlanCommit,
        string PlanReviewPath,
        string PlanReviewSha256,
        string WorkflowApprovalObservedAt);

    private sealed record QualificationAttemptTerminal(
        string SchemaVersion,
        string AttemptId,
        DateTimeOffset CompletedAt,
        string Outcome,
        string Category,
        string EvidenceCitation);
}
