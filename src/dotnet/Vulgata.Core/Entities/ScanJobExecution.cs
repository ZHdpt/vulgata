using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Core.Entities;

public enum ScanJobExecutionState
{
    Pending,
    Running,
    RetryScheduled,
    Completed,
    Failed,
    Cancelled,
}

public enum ScanJobFailureCategory
{
    ProviderTransient,
    ProviderConfiguration,
    JobTransient,
    JobPermanent,
    Integrity,
    Security,
    Cancellation,
    Unclassified,
}

public sealed class ScanJobExecution
{
    private const string _schemaIdentity = "scan-job-execution/1.0";

    private ScanJobExecution()
    {
    }

    private ScanJobExecution(
        Guid id,
        Guid scanExecutionId,
        Guid repositoryId,
        Guid scanRunId,
        Guid fileCodeUnitId,
        int manifestOrdinal,
        DateTimeOffset stateChangedAt)
    {
        Id = id;
        ScanExecutionId = scanExecutionId;
        RepositoryId = repositoryId;
        ScanRunId = scanRunId;
        FileCodeUnitId = fileCodeUnitId;
        ManifestOrdinal = manifestOrdinal;
        State = ScanJobExecutionState.Pending;
        StateChangedAt = stateChangedAt;
    }

    public Guid Id { get; private set; }
    public Guid ScanExecutionId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public Guid FileCodeUnitId { get; private set; }
    public int ManifestOrdinal { get; private set; }
    public ScanJobExecutionState State { get; private set; }
    public int AttemptCount { get; private set; }
    public long StateVersion { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public ScanJobFailureCategory? FailureCategory { get; private set; }
    public string? FailureCode { get; private set; }
    public Guid? CommittedDocumentVersionId { get; private set; }
    public DateTimeOffset StateChangedAt { get; private set; }

    public static ScanJobExecution Create(
        Guid scanExecutionId,
        Guid repositoryId,
        Guid scanRunId,
        Guid fileCodeUnitId,
        int manifestOrdinal,
        DateTimeOffset stateChangedAt)
    {
        ThrowIfEmpty(scanExecutionId, nameof(scanExecutionId));
        ThrowIfEmpty(repositoryId, nameof(repositoryId));
        ThrowIfEmpty(scanRunId, nameof(scanRunId));
        ThrowIfEmpty(fileCodeUnitId, nameof(fileCodeUnitId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(manifestOrdinal);
        ThrowIfNotUtc(stateChangedAt, nameof(stateChangedAt));

        byte[] identityEnvelope = Encoding.UTF8.GetBytes(string.Join(
            '\n',
            _schemaIdentity,
            scanExecutionId.ToString("D"),
            fileCodeUnitId.ToString("D"),
            manifestOrdinal.ToString(CultureInfo.InvariantCulture)));
        byte[] identityBytes = SHA256.HashData(identityEnvelope)[..16];
        identityBytes[6] = (byte)((identityBytes[6] & 0x0f) | 0x80);
        identityBytes[8] = (byte)((identityBytes[8] & 0x3f) | 0x80);

        return new ScanJobExecution(
            new Guid(identityBytes, bigEndian: true),
            scanExecutionId,
            repositoryId,
            scanRunId,
            fileCodeUnitId,
            manifestOrdinal,
            stateChangedAt);
    }

    public void AcquireLease(
        Guid leaseToken,
        DateTimeOffset leaseExpiresAt,
        DateTimeOffset stateChangedAt)
    {
        if (State != ScanJobExecutionState.Pending)
        {
            throw new InvalidOperationException("Only a Pending Scan Job Execution can acquire a lease.");
        }

        ThrowIfEmpty(leaseToken, nameof(leaseToken));
        ThrowIfNotUtc(leaseExpiresAt, nameof(leaseExpiresAt));
        ThrowIfNotUtc(stateChangedAt, nameof(stateChangedAt));
        if (stateChangedAt < StateChangedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(stateChangedAt));
        }

        if (leaseExpiresAt <= stateChangedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseExpiresAt));
        }

        State = ScanJobExecutionState.Running;
        AttemptCount++;
        StateVersion++;
        LeaseToken = leaseToken;
        LeaseExpiresAt = leaseExpiresAt;
        StateChangedAt = stateChangedAt;
    }

    public void ScheduleRetry(
        long expectedStateVersion,
        Guid expectedLeaseToken,
        DateTimeOffset nextAttemptAt,
        ScanJobFailureCategory failureCategory,
        string failureCode,
        DateTimeOffset stateChangedAt)
    {
        if (State != ScanJobExecutionState.Running ||
            StateVersion != expectedStateVersion ||
            LeaseToken != expectedLeaseToken)
        {
            throw new InvalidOperationException(
                "Retry scheduling requires the current Running lease and state version.");
        }

        ThrowIfNotUtc(nextAttemptAt, nameof(nextAttemptAt));
        ThrowIfNotUtc(stateChangedAt, nameof(stateChangedAt));
        if (stateChangedAt < StateChangedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(stateChangedAt));
        }

        if (nextAttemptAt <= stateChangedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(nextAttemptAt));
        }

        ThrowIfInvalidFailureCategory(failureCategory);
        string normalizedFailureCode = NormalizeFailureCode(failureCode);

        State = ScanJobExecutionState.RetryScheduled;
        StateVersion++;
        LeaseToken = null;
        LeaseExpiresAt = null;
        NextAttemptAt = nextAttemptAt;
        FailureCategory = failureCategory;
        FailureCode = normalizedFailureCode;
        StateChangedAt = stateChangedAt;
    }

    public void RecoverDueRetry(long expectedStateVersion, DateTimeOffset now)
    {
        ThrowIfNotUtc(now, nameof(now));
        if (State != ScanJobExecutionState.RetryScheduled ||
            StateVersion != expectedStateVersion ||
            NextAttemptAt is null ||
            now < NextAttemptAt)
        {
            throw new InvalidOperationException(
                "Retry recovery requires the current version of a due Retry Scheduled job.");
        }

        State = ScanJobExecutionState.Pending;
        StateVersion++;
        NextAttemptAt = null;
        FailureCategory = null;
        FailureCode = null;
        StateChangedAt = now;
    }

    public void Complete(
        long expectedStateVersion,
        Guid expectedLeaseToken,
        Guid committedDocumentVersionId,
        DateTimeOffset stateChangedAt)
    {
        if (State != ScanJobExecutionState.Running ||
            StateVersion != expectedStateVersion ||
            LeaseToken != expectedLeaseToken)
        {
            throw new InvalidOperationException(
                "Completion requires the current Running lease and state version.");
        }

        ThrowIfEmpty(committedDocumentVersionId, nameof(committedDocumentVersionId));
        ThrowIfNotUtc(stateChangedAt, nameof(stateChangedAt));
        if (stateChangedAt < StateChangedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(stateChangedAt));
        }

        State = ScanJobExecutionState.Completed;
        StateVersion++;
        LeaseToken = null;
        LeaseExpiresAt = null;
        NextAttemptAt = null;
        FailureCategory = null;
        FailureCode = null;
        CommittedDocumentVersionId = committedDocumentVersionId;
        StateChangedAt = stateChangedAt;
    }

    public void Fail(
        long expectedStateVersion,
        Guid? expectedLeaseToken,
        ScanJobFailureCategory failureCategory,
        string failureCode,
        DateTimeOffset stateChangedAt)
    {
        if ((State != ScanJobExecutionState.Running &&
             State != ScanJobExecutionState.RetryScheduled) ||
            StateVersion != expectedStateVersion ||
            LeaseToken != expectedLeaseToken)
        {
            throw new InvalidOperationException(
                "Failure requires the current Running lease or Retry Scheduled state version.");
        }

        ThrowIfInvalidFailureCategory(failureCategory);
        string normalizedFailureCode = NormalizeFailureCode(failureCode);
        ThrowIfNotUtc(stateChangedAt, nameof(stateChangedAt));
        if (stateChangedAt < StateChangedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(stateChangedAt));
        }

        State = ScanJobExecutionState.Failed;
        StateVersion++;
        LeaseToken = null;
        LeaseExpiresAt = null;
        NextAttemptAt = null;
        CommittedDocumentVersionId = null;
        FailureCategory = failureCategory;
        FailureCode = normalizedFailureCode;
        StateChangedAt = stateChangedAt;
    }

    public void Cancel(
        long expectedStateVersion,
        Guid? expectedLeaseToken,
        string failureCode,
        DateTimeOffset stateChangedAt)
    {
        if ((State != ScanJobExecutionState.Pending &&
             State != ScanJobExecutionState.Running &&
             State != ScanJobExecutionState.RetryScheduled) ||
            StateVersion != expectedStateVersion ||
            LeaseToken != expectedLeaseToken)
        {
            throw new InvalidOperationException(
                "Cancellation requires the current state version and lease shape.");
        }

        string normalizedFailureCode = NormalizeFailureCode(failureCode);
        ThrowIfNotUtc(stateChangedAt, nameof(stateChangedAt));
        if (stateChangedAt < StateChangedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(stateChangedAt));
        }

        State = ScanJobExecutionState.Cancelled;
        StateVersion++;
        LeaseToken = null;
        LeaseExpiresAt = null;
        NextAttemptAt = null;
        CommittedDocumentVersionId = null;
        FailureCategory = ScanJobFailureCategory.Cancellation;
        FailureCode = normalizedFailureCode;
        StateChangedAt = stateChangedAt;
    }

    public void RecoverExpiredLease(long expectedStateVersion, DateTimeOffset now)
    {
        ThrowIfNotUtc(now, nameof(now));
        if (State != ScanJobExecutionState.Running ||
            StateVersion != expectedStateVersion ||
            LeaseExpiresAt is null ||
            now <= LeaseExpiresAt)
        {
            throw new InvalidOperationException(
                "Lease recovery requires the current version of a strictly expired Running job.");
        }

        State = ScanJobExecutionState.Pending;
        StateVersion++;
        LeaseToken = null;
        LeaseExpiresAt = null;
        StateChangedAt = now;
    }

    public void RelinquishLeaseForProviderProbe(
        long expectedStateVersion,
        Guid expectedLeaseToken,
        DateTimeOffset stateChangedAt)
    {
        if (State != ScanJobExecutionState.Running ||
            StateVersion != expectedStateVersion ||
            LeaseToken != expectedLeaseToken)
        {
            throw new InvalidOperationException(
                "Provider probe relinquishment requires the current Running lease and state version.");
        }

        ThrowIfNotUtc(stateChangedAt, nameof(stateChangedAt));
        if (stateChangedAt < StateChangedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(stateChangedAt));
        }

        State = ScanJobExecutionState.Pending;
        StateVersion++;
        LeaseToken = null;
        LeaseExpiresAt = null;
        NextAttemptAt = null;
        FailureCategory = null;
        FailureCode = null;
        StateChangedAt = stateChangedAt;
    }

    private static void ThrowIfEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identity must not be empty.", parameterName);
        }
    }

    private static void ThrowIfNotUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use the UTC offset.", parameterName);
        }
    }

    private static void ThrowIfInvalidFailureCategory(ScanJobFailureCategory value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    private static string NormalizeFailureCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string normalized = value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        if (normalized.Length is < 1 or > 64 ||
            normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException(
                "Failure code must be a bounded machine-readable value.",
                nameof(value));
        }

        return normalized;
    }
}
