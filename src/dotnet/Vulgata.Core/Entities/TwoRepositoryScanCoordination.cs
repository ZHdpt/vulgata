using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Core.Entities;

public enum TwoRepositoryScanCoordinationPhase
{
    Preparing,
    Pass1,
    Resolving,
    Pass2,
    Paused,
    Completed,
    CompletedWithWarnings,
    FailedIntegrity,
    FailedSecurity,
    Cancelled,
    FailedPass1,
}

public enum TwoRepositoryScanReasonCategory
{
    ProviderUnavailable,
    OperatorCorrection,
    Integrity,
    Security,
    Cancellation,
    Unclassified,
    Pass1Policy,
}

public sealed class TwoRepositoryScanCoordination
{
    private const string _schemaIdentity = "two-repository-scan-coordination/1.0";
    private const int _maximumReasonCodeLength = 64;

    private TwoRepositoryScanCoordination()
    {
        ProfiledRevisionOne = string.Empty;
        ProfiledRevisionTwo = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid SystemId { get; private set; }
    public Guid RepositoryOneId { get; private set; }
    public Guid RepositoryTwoId { get; private set; }
    public Guid ScanRunOneId { get; private set; }
    public Guid ScanRunTwoId { get; private set; }
    public Guid ScanExecutionOneId { get; private set; }
    public Guid ScanExecutionTwoId { get; private set; }
    public string ProfiledRevisionOne { get; private set; }
    public string ProfiledRevisionTwo { get; private set; }
    public TwoRepositoryScanCoordinationPhase Phase { get; private set; }
    public TwoRepositoryScanReasonCategory? ReasonCategory { get; private set; }
    public string? ReasonCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? TerminalAt { get; private set; }
    public long StateVersion { get; private set; }

    public static TwoRepositoryScanCoordination Create(
        ScanExecution first,
        ScanExecution second,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        RequireBoundExecution(first, nameof(first));
        RequireBoundExecution(second, nameof(second));
        RequireUtc(createdAt, nameof(createdAt));

        if (first.SystemId != second.SystemId)
        {
            throw new ArgumentException(
                "Both Repository Scans must belong to the same System.",
                nameof(second));
        }

        if (first.RepositoryId == second.RepositoryId)
        {
            throw new ArgumentException(
                "Two distinct Repositories are required.",
                nameof(second));
        }

        if (first.ScanRunId == second.ScanRunId || first.Id == second.Id)
        {
            throw new ArgumentException(
                "Independent Scan Run and Scan Execution identities are required.",
                nameof(second));
        }

        if (createdAt < first.StartedAt || createdAt < second.StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(createdAt));
        }

        ScanExecution repositoryOne = first.RepositoryId.CompareTo(second.RepositoryId) < 0
            ? first
            : second;
        ScanExecution repositoryTwo = ReferenceEquals(repositoryOne, first) ? second : first;
        Guid id = CreateId(first.SystemId, repositoryOne.Id, repositoryTwo.Id);

        return new TwoRepositoryScanCoordination
        {
            Id = id,
            SystemId = first.SystemId,
            RepositoryOneId = repositoryOne.RepositoryId,
            RepositoryTwoId = repositoryTwo.RepositoryId,
            ScanRunOneId = repositoryOne.ScanRunId,
            ScanRunTwoId = repositoryTwo.ScanRunId,
            ScanExecutionOneId = repositoryOne.Id,
            ScanExecutionTwoId = repositoryTwo.Id,
            ProfiledRevisionOne = repositoryOne.ProfiledRevision,
            ProfiledRevisionTwo = repositoryTwo.ProfiledRevision,
            Phase = TwoRepositoryScanCoordinationPhase.Preparing,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
    }

    public void BeginPass1(long expectedStateVersion, DateTimeOffset updatedAt) =>
        Transition(
            TwoRepositoryScanCoordinationPhase.Preparing,
            TwoRepositoryScanCoordinationPhase.Pass1,
            expectedStateVersion,
            updatedAt);

    public void BeginResolving(long expectedStateVersion, DateTimeOffset updatedAt) =>
        Transition(
            TwoRepositoryScanCoordinationPhase.Pass1,
            TwoRepositoryScanCoordinationPhase.Resolving,
            expectedStateVersion,
            updatedAt);

    public void BeginPass2(long expectedStateVersion, DateTimeOffset updatedAt) =>
        Transition(
            TwoRepositoryScanCoordinationPhase.Resolving,
            TwoRepositoryScanCoordinationPhase.Pass2,
            expectedStateVersion,
            updatedAt);

    public void Pause(
        long expectedStateVersion,
        TwoRepositoryScanReasonCategory reasonCategory,
        string reasonCode,
        DateTimeOffset updatedAt)
    {
        EnsureMutable();
        EnsureVersionAndTime(expectedStateVersion, updatedAt);
        ThrowIfInvalidReasonCategory(reasonCategory);
        string code = NormalizeReasonCode(reasonCode);

        Phase = TwoRepositoryScanCoordinationPhase.Paused;
        ReasonCategory = reasonCategory;
        ReasonCode = code;
        Advance(updatedAt);
    }

    public void CompleteFromChildren(
        long expectedStateVersion,
        ScanExecution repositoryOne,
        ScanExecution repositoryTwo,
        DateTimeOffset updatedAt)
    {
        if (Phase != TwoRepositoryScanCoordinationPhase.Pass2)
        {
            throw new InvalidOperationException(
                "Combined completion requires the Pass2 phase.");
        }

        EnsureVersionAndTime(expectedStateVersion, updatedAt);
        ArgumentNullException.ThrowIfNull(repositoryOne);
        ArgumentNullException.ThrowIfNull(repositoryTwo);
        if (!MatchesRepositoryOne(repositoryOne) || !MatchesRepositoryTwo(repositoryTwo))
        {
            throw new InvalidOperationException(
                "The child Scan Execution identities do not match the coordination binding.");
        }

        if (!IsSuccessful(repositoryOne.Phase) || !IsSuccessful(repositoryTwo.Phase))
        {
            throw new InvalidOperationException(
                "Both bound Repository Scans must have valid terminal success outcomes.");
        }

        Phase = repositoryOne.Phase == ScanExecutionPhase.CompletedWithWarnings ||
            repositoryTwo.Phase == ScanExecutionPhase.CompletedWithWarnings
            ? TwoRepositoryScanCoordinationPhase.CompletedWithWarnings
            : TwoRepositoryScanCoordinationPhase.Completed;
        ReasonCategory = null;
        ReasonCode = null;
        TerminalAt = updatedAt;
        Advance(updatedAt);
    }

    public void ResumePass2(long expectedStateVersion, DateTimeOffset updatedAt)
    {
        if (Phase != TwoRepositoryScanCoordinationPhase.Paused)
        {
            throw new InvalidOperationException("Only a paused coordination can resume Pass 2.");
        }

        EnsureVersionAndTime(expectedStateVersion, updatedAt);
        Phase = TwoRepositoryScanCoordinationPhase.Pass2;
        ReasonCategory = null;
        ReasonCode = null;
        Advance(updatedAt);
    }

    public void CompletePass2(
        long expectedStateVersion,
        bool withWarnings,
        DateTimeOffset updatedAt)
    {
        if (Phase != TwoRepositoryScanCoordinationPhase.Pass2)
        {
            throw new InvalidOperationException("Pass 2 completion requires the Pass2 phase.");
        }

        EnsureVersionAndTime(expectedStateVersion, updatedAt);
        Phase = withWarnings
            ? TwoRepositoryScanCoordinationPhase.CompletedWithWarnings
            : TwoRepositoryScanCoordinationPhase.Completed;
        ReasonCategory = null;
        ReasonCode = null;
        TerminalAt = updatedAt;
        Advance(updatedAt);
    }

    public void FailIntegrity(
        long expectedStateVersion,
        string reasonCode,
        DateTimeOffset updatedAt) =>
        Terminate(
            TwoRepositoryScanCoordinationPhase.FailedIntegrity,
            TwoRepositoryScanReasonCategory.Integrity,
            expectedStateVersion,
            reasonCode,
            updatedAt);

    public void FailSecurity(
        long expectedStateVersion,
        string reasonCode,
        DateTimeOffset updatedAt) =>
        Terminate(
            TwoRepositoryScanCoordinationPhase.FailedSecurity,
            TwoRepositoryScanReasonCategory.Security,
            expectedStateVersion,
            reasonCode,
            updatedAt);

    public void FailPass1(
        long expectedStateVersion,
        string reasonCode,
        DateTimeOffset updatedAt) =>
        Terminate(
            TwoRepositoryScanCoordinationPhase.FailedPass1,
            TwoRepositoryScanReasonCategory.Pass1Policy,
            expectedStateVersion,
            reasonCode,
            updatedAt);

    public void Cancel(
        long expectedStateVersion,
        string reasonCode,
        DateTimeOffset updatedAt) =>
        Terminate(
            TwoRepositoryScanCoordinationPhase.Cancelled,
            TwoRepositoryScanReasonCategory.Cancellation,
            expectedStateVersion,
            reasonCode,
            updatedAt);

    private void Transition(
        TwoRepositoryScanCoordinationPhase expectedPhase,
        TwoRepositoryScanCoordinationPhase nextPhase,
        long expectedStateVersion,
        DateTimeOffset updatedAt)
    {
        if (Phase != expectedPhase)
        {
            throw new InvalidOperationException(
                $"The coordination is in phase {Phase}; expected {expectedPhase}.");
        }

        EnsureVersionAndTime(expectedStateVersion, updatedAt);
        Phase = nextPhase;
        ReasonCategory = null;
        ReasonCode = null;
        Advance(updatedAt);
    }

    private void Terminate(
        TwoRepositoryScanCoordinationPhase terminalPhase,
        TwoRepositoryScanReasonCategory reasonCategory,
        long expectedStateVersion,
        string reasonCode,
        DateTimeOffset updatedAt)
    {
        EnsureMutable();
        EnsureVersionAndTime(expectedStateVersion, updatedAt);
        string code = NormalizeReasonCode(reasonCode);

        Phase = terminalPhase;
        ReasonCategory = reasonCategory;
        ReasonCode = code;
        TerminalAt = updatedAt;
        Advance(updatedAt);
    }

    private void EnsureMutable()
    {
        if (Phase is TwoRepositoryScanCoordinationPhase.Completed or
            TwoRepositoryScanCoordinationPhase.CompletedWithWarnings or
            TwoRepositoryScanCoordinationPhase.FailedIntegrity or
            TwoRepositoryScanCoordinationPhase.FailedSecurity or
            TwoRepositoryScanCoordinationPhase.FailedPass1 or
            TwoRepositoryScanCoordinationPhase.Cancelled)
        {
            throw new InvalidOperationException("A terminal coordination is immutable.");
        }
    }

    private void EnsureVersionAndTime(long expectedStateVersion, DateTimeOffset updatedAt)
    {
        if (StateVersion != expectedStateVersion)
        {
            throw new InvalidOperationException("The coordination state version is stale.");
        }

        RequireUtc(updatedAt, nameof(updatedAt));
        if (updatedAt < UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAt));
        }
    }

    private void Advance(DateTimeOffset updatedAt)
    {
        StateVersion++;
        UpdatedAt = updatedAt;
    }

    private bool MatchesRepositoryOne(ScanExecution execution) =>
        execution.Id == ScanExecutionOneId &&
        execution.RepositoryId == RepositoryOneId &&
        execution.ScanRunId == ScanRunOneId &&
        execution.SystemId == SystemId &&
        string.Equals(
            execution.ProfiledRevision,
            ProfiledRevisionOne,
            StringComparison.Ordinal);

    private bool MatchesRepositoryTwo(ScanExecution execution) =>
        execution.Id == ScanExecutionTwoId &&
        execution.RepositoryId == RepositoryTwoId &&
        execution.ScanRunId == ScanRunTwoId &&
        execution.SystemId == SystemId &&
        string.Equals(
            execution.ProfiledRevision,
            ProfiledRevisionTwo,
            StringComparison.Ordinal);

    private static bool IsSuccessful(ScanExecutionPhase phase) =>
        phase is ScanExecutionPhase.Completed or ScanExecutionPhase.CompletedWithWarnings;

    private static void RequireBoundExecution(ScanExecution execution, string parameterName)
    {
        if (execution.Id == Guid.Empty ||
            execution.SystemId == Guid.Empty ||
            execution.RepositoryId == Guid.Empty ||
            execution.ScanRunId == Guid.Empty ||
            string.IsNullOrWhiteSpace(execution.ProfiledRevision))
        {
            throw new ArgumentException(
                "A complete bound Repository Scan identity is required.",
                parameterName);
        }
    }

    private static Guid CreateId(
        Guid systemId,
        Guid scanExecutionOneId,
        Guid scanExecutionTwoId)
    {
        byte[] envelope = Encoding.UTF8.GetBytes(string.Join(
            '\n',
            _schemaIdentity,
            systemId.ToString("D"),
            scanExecutionOneId.ToString("D"),
            scanExecutionTwoId.ToString("D"),
            1.ToString(CultureInfo.InvariantCulture)));
        byte[] identityBytes = SHA256.HashData(envelope)[..16];
        identityBytes[6] = (byte)((identityBytes[6] & 0x0f) | 0x80);
        identityBytes[8] = (byte)((identityBytes[8] & 0x3f) | 0x80);
        return new Guid(identityBytes, bigEndian: true);
    }

    private static void ThrowIfInvalidReasonCategory(TwoRepositoryScanReasonCategory value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    private static string NormalizeReasonCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        if (normalized.Length > _maximumReasonCodeLength ||
            normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException(
                "Reason code must be a bounded machine-readable value.",
                nameof(value));
        }

        return normalized;
    }

    private static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use the UTC offset.", parameterName);
        }
    }
}
