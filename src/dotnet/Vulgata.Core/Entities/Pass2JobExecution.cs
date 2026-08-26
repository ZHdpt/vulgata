namespace Vulgata.Core.Entities;

public enum Pass2JobExecutionState
{
    Pending,
    Running,
    RetryScheduled,
    Completed,
    Failed,
    Cancelled,
}

public sealed class Pass2JobExecution
{
    private Pass2JobExecution()
    {
    }

    public static Pass2JobExecution CreatePending(
        Guid id,
        Guid pass2ExecutionId,
        Guid flowGroupId,
        int groupOrder,
        DateTimeOffset? stateChangedAt = null)
    {
        if (id == Guid.Empty || pass2ExecutionId == Guid.Empty || flowGroupId == Guid.Empty)
        {
            throw new ArgumentException("Complete Pass 2 Job identities are required.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(groupOrder);
        DateTimeOffset changedAt = stateChangedAt ?? DateTimeOffset.UtcNow;
        ThrowIfNotUtc(changedAt, nameof(stateChangedAt));
        return new Pass2JobExecution
        {
            Id = id,
            Pass2ExecutionId = pass2ExecutionId,
            FlowGroupId = flowGroupId,
            GroupOrder = groupOrder,
            State = Pass2JobExecutionState.Pending,
            StateChangedAt = changedAt,
        };
    }

    public Guid Id { get; private set; }
    public Guid Pass2ExecutionId { get; private set; }
    public Guid FlowGroupId { get; private set; }
    public int GroupOrder { get; private set; }
    public Pass2JobExecutionState State { get; private set; }
    public int AttemptCount { get; private set; }
    public long StateVersion { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public ScanJobFailureCategory? FailureCategory { get; private set; }
    public string? FailureCode { get; private set; }
    public Guid? CommittedDocumentVersionId { get; private set; }
    public string? CompletionFingerprint { get; private set; }
    public DateTimeOffset StateChangedAt { get; private set; }

    public void AcquireLease(
        Guid leaseToken,
        DateTimeOffset leaseExpiresAt,
        DateTimeOffset stateChangedAt)
    {
        if (State != Pass2JobExecutionState.Pending || AttemptCount >= 2)
        {
            throw new InvalidOperationException("Only an eligible Pending Pass 2 Job can acquire a lease.");
        }
        if (leaseToken == Guid.Empty)
        {
            throw new ArgumentException("A lease token is required.", nameof(leaseToken));
        }

        ValidateTransitionTime(stateChangedAt);
        ThrowIfNotUtc(leaseExpiresAt, nameof(leaseExpiresAt));
        if (leaseExpiresAt <= stateChangedAt || leaseExpiresAt > stateChangedAt.AddMinutes(30))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseExpiresAt));
        }

        State = Pass2JobExecutionState.Running;
        AttemptCount++;
        StateVersion++;
        LeaseToken = leaseToken;
        LeaseExpiresAt = leaseExpiresAt;
        NextAttemptAt = null;
        FailureCategory = null;
        FailureCode = null;
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
        RequireCurrentLease(expectedStateVersion, expectedLeaseToken);
        if (AttemptCount >= 2)
        {
            throw new InvalidOperationException("The Pass 2 Job retry limit is exhausted.");
        }

        ValidateTransitionTime(stateChangedAt);
        ThrowIfNotUtc(nextAttemptAt, nameof(nextAttemptAt));
        if (nextAttemptAt <= stateChangedAt || string.IsNullOrWhiteSpace(failureCode) ||
            !Enum.IsDefined(failureCategory))
        {
            throw new ArgumentException("Complete retry failure details are required.");
        }

        State = Pass2JobExecutionState.RetryScheduled;
        StateVersion++;
        LeaseToken = null;
        LeaseExpiresAt = null;
        NextAttemptAt = nextAttemptAt;
        FailureCategory = failureCategory;
        FailureCode = failureCode.Trim();
        StateChangedAt = stateChangedAt;
    }

    public void RecoverDueRetry(long expectedStateVersion, DateTimeOffset now)
    {
        ThrowIfNotUtc(now, nameof(now));
        if (State != Pass2JobExecutionState.RetryScheduled ||
            StateVersion != expectedStateVersion || NextAttemptAt is null || now < NextAttemptAt)
        {
            throw new InvalidOperationException("Recovery requires the current due retry.");
        }

        State = Pass2JobExecutionState.Pending;
        StateVersion++;
        NextAttemptAt = null;
        FailureCategory = null;
        FailureCode = null;
        StateChangedAt = now;
    }

    public void RecoverExpiredLease(long expectedStateVersion, DateTimeOffset now)
    {
        ThrowIfNotUtc(now, nameof(now));
        if (State != Pass2JobExecutionState.Running ||
            StateVersion != expectedStateVersion || LeaseExpiresAt is null ||
            now < LeaseExpiresAt)
        {
            throw new InvalidOperationException("Recovery requires the current expired Pass 2 lease.");
        }

        StateVersion++;
        LeaseToken = null;
        LeaseExpiresAt = null;
        NextAttemptAt = null;
        StateChangedAt = now;
        if (AttemptCount < 2)
        {
            State = Pass2JobExecutionState.Pending;
            FailureCategory = null;
            FailureCode = null;
            return;
        }

        State = Pass2JobExecutionState.Failed;
        FailureCategory = ScanJobFailureCategory.Unclassified;
        FailureCode = "lease-expired";
    }

    public void ReleaseForProviderPause(
        long expectedStateVersion,
        Guid expectedLeaseToken,
        DateTimeOffset stateChangedAt)
    {
        RequireCurrentLease(expectedStateVersion, expectedLeaseToken);
        ValidateTransitionTime(stateChangedAt);
        State = Pass2JobExecutionState.Pending;
        AttemptCount--;
        StateVersion++;
        LeaseToken = null;
        LeaseExpiresAt = null;
        FailureCategory = null;
        FailureCode = null;
        StateChangedAt = stateChangedAt;
    }

    public void Fail(
        long expectedStateVersion,
        Guid expectedLeaseToken,
        ScanJobFailureCategory failureCategory,
        string failureCode,
        DateTimeOffset stateChangedAt)
    {
        RequireCurrentLease(expectedStateVersion, expectedLeaseToken);
        ValidateTransitionTime(stateChangedAt);
        if (!Enum.IsDefined(failureCategory) || string.IsNullOrWhiteSpace(failureCode))
        {
            throw new ArgumentException("Complete Pass 2 failure details are required.");
        }

        State = Pass2JobExecutionState.Failed;
        StateVersion++;
        LeaseToken = null;
        LeaseExpiresAt = null;
        NextAttemptAt = null;
        FailureCategory = failureCategory;
        FailureCode = failureCode.Trim();
        StateChangedAt = stateChangedAt;
    }

    public void Complete(
        long expectedStateVersion,
        Guid expectedLeaseToken,
        Guid committedDocumentVersionId,
        string completionFingerprint,
        DateTimeOffset completedAt)
    {
        RequireCurrentLease(expectedStateVersion, expectedLeaseToken);
        ValidateTransitionTime(completedAt);
        if (committedDocumentVersionId == Guid.Empty || !IsLowerSha256(completionFingerprint))
        {
            throw new ArgumentException("Complete Pass 2 publication identity is required.");
        }

        State = Pass2JobExecutionState.Completed;
        StateVersion++;
        LeaseToken = null;
        LeaseExpiresAt = null;
        NextAttemptAt = null;
        FailureCategory = null;
        FailureCode = null;
        CommittedDocumentVersionId = committedDocumentVersionId;
        CompletionFingerprint = completionFingerprint;
        StateChangedAt = completedAt;
    }

    private void RequireCurrentLease(long expectedStateVersion, Guid expectedLeaseToken)
    {
        if (State != Pass2JobExecutionState.Running || StateVersion != expectedStateVersion ||
            LeaseToken != expectedLeaseToken)
        {
            throw new InvalidOperationException("The current Running Pass 2 lease is required.");
        }
    }

    private void ValidateTransitionTime(DateTimeOffset value)
    {
        ThrowIfNotUtc(value, nameof(value));
        if (value < StateChangedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    private static void ThrowIfNotUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Pass 2 Job timestamps must be UTC.", parameterName);
        }
    }

    private static bool IsLowerSha256(string value) =>
        value is { Length: 64 } && value.All(character =>
            char.IsAsciiHexDigit(character) && !char.IsAsciiLetterUpper(character));
}
