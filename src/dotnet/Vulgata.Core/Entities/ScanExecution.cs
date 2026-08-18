using System.Text;
using System.Text.Json;

namespace Vulgata.Core.Entities;

public enum ScanExecutionPauseCategory
{
    ProviderUnavailable,
    OperatorCorrection,
}

public enum ScanExecutionFailureCategory
{
    Integrity,
    Security,
    Cancellation,
}

/// <summary>
/// One durable record per deadline Scan run. Created by
/// <see cref="DomainServices.SequentialScanRunner.StartAsync"/>; mutated only
/// through atomic phase transitions inside
/// <c>ScanExecutionRepository</c>. Terminal phases are immutable.
/// </summary>
public sealed class ScanExecution
{
    /// <summary>
    /// Bounded upper limit for <see cref="RestartIndicator"/>. The indicator
    /// never overflows; once this limit is reached, subsequent restart
    /// re-reads keep the value clamped.
    /// </summary>
    public const int MaxRestartIndicator = 32;

    private const int MaximumProfiledRevisionLength = 64;
    private const int MaximumIdentityLength = 128;
    private const int MaximumRuntimeIdentityUtf8Bytes = 4_096;
    private const int MaximumMachineCodeLength = 64;
    private const int MaximumTerminalMessageUtf8Bytes = 512;

    // EF Core requires a parameterless constructor. It can be private.
    private ScanExecution()
    {
        ProfiledRevision = string.Empty;
    }

    private ScanExecution(
        Guid id,
        Guid repositoryId,
        Guid systemId,
        Guid scanRunId,
        Guid preScanResultId,
        string profiledRevision,
        DateTimeOffset startedAt)
    {
        Id = id;
        RepositoryId = repositoryId;
        SystemId = systemId;
        ScanRunId = scanRunId;
        PreScanResultId = preScanResultId;
        ProfiledRevision = profiledRevision;
        StartedAt = startedAt;
        Phase = ScanExecutionPhase.Pass1Planning;
        InFlightJobId = null;
        CompletedJobCount = 0;
        TotalJobCount = 0;
        RestartIndicator = 0;
        TerminalMessage = null;
        TerminalReasonKind = null;
        TerminalAt = null;
    }

    public Guid Id { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid SystemId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public Guid PreScanResultId { get; private set; }
    public string ProfiledRevision { get; private set; }
    public ScanExecutionPhase Phase { get; private set; }
    public Guid? InFlightJobId { get; private set; }
    public int CompletedJobCount { get; private set; }
    public int TotalJobCount { get; private set; }
    public int RestartIndicator { get; private set; }
    public string? TerminalMessage { get; private set; }
    public ScanExecutionFailureKind? TerminalReasonKind { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? TerminalAt { get; private set; }
    public string? PromptIdentity { get; private set; }
    public string? DocumentSchemaIdentity { get; private set; }
    public string? AnalysisRuntimeIdentityJson { get; private set; }
    public string? AnalysisRuntimeIdentityHash { get; private set; }
    public ScanExecutionPauseCategory? PauseCategory { get; private set; }
    public string? PauseCode { get; private set; }
    public ScanExecutionFailureCategory? FailureCategory { get; private set; }
    public string? FailureCode { get; private set; }
    public long StateVersion { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public static ScanExecution CreateNewPath(
        Guid id,
        Guid repositoryId,
        Guid systemId,
        Guid scanRunId,
        string profiledRevision,
        string promptIdentity,
        string documentSchemaIdentity,
        string analysisRuntimeIdentityJson,
        string analysisRuntimeIdentityHash,
        DateTimeOffset startedAt)
    {
        ThrowIfEmpty(id, nameof(id));
        ThrowIfEmpty(repositoryId, nameof(repositoryId));
        ThrowIfEmpty(systemId, nameof(systemId));
        ThrowIfEmpty(scanRunId, nameof(scanRunId));
        string revision = RequireProfiledRevision(profiledRevision);
        string prompt = RequireIdentity(promptIdentity, nameof(promptIdentity));
        string schema = RequireIdentity(
            documentSchemaIdentity,
            nameof(documentSchemaIdentity));
        string runtimeJson = RequireRuntimeIdentityJson(analysisRuntimeIdentityJson);
        string runtimeHash = RequireSha256(
            analysisRuntimeIdentityHash,
            nameof(analysisRuntimeIdentityHash));
        RequireUtc(startedAt, nameof(startedAt));

        return new ScanExecution
        {
            Id = id,
            RepositoryId = repositoryId,
            SystemId = systemId,
            ScanRunId = scanRunId,
            ProfiledRevision = revision,
            PromptIdentity = prompt,
            DocumentSchemaIdentity = schema,
            AnalysisRuntimeIdentityJson = runtimeJson,
            AnalysisRuntimeIdentityHash = runtimeHash,
            StartedAt = startedAt,
            UpdatedAt = startedAt,
            Phase = ScanExecutionPhase.Prepared,
        };
    }

    public void BeginRunning(long expectedStateVersion, DateTimeOffset updatedAt)
    {
        EnsureNewPathTransition(ScanExecutionPhase.Prepared, expectedStateVersion, updatedAt);
        Phase = ScanExecutionPhase.Running;
        AdvanceNewPathVersion(updatedAt);
    }

    public void PauseProviderUnavailable(
        long expectedStateVersion,
        string pauseCode,
        DateTimeOffset updatedAt)
    {
        EnsureNewPathTransition(ScanExecutionPhase.Running, expectedStateVersion, updatedAt);
        string code = RequireMachineCode(pauseCode, nameof(pauseCode));

        Phase = ScanExecutionPhase.PausedProviderUnavailable;
        PauseCategory = ScanExecutionPauseCategory.ProviderUnavailable;
        PauseCode = code;
        AdvanceNewPathVersion(updatedAt);
    }

    public void PauseOperatorCorrection(
        long expectedStateVersion,
        string pauseCode,
        DateTimeOffset updatedAt)
    {
        EnsureNewPathTransition(ScanExecutionPhase.Running, expectedStateVersion, updatedAt);
        string code = RequireMachineCode(pauseCode, nameof(pauseCode));

        Phase = ScanExecutionPhase.PausedOperatorCorrection;
        PauseCategory = ScanExecutionPauseCategory.OperatorCorrection;
        PauseCode = code;
        AdvanceNewPathVersion(updatedAt);
    }

    public void ResumeToPrepared(long expectedStateVersion, DateTimeOffset updatedAt)
    {
        if (Phase is not (ScanExecutionPhase.PausedProviderUnavailable or
            ScanExecutionPhase.PausedOperatorCorrection))
        {
            throw new InvalidOperationException(
                "Only a paused new-path Scan Execution can be manually resumed.");
        }

        EnsureNewPathVersionAndTime(expectedStateVersion, updatedAt);
        Phase = ScanExecutionPhase.Prepared;
        PauseCategory = null;
        PauseCode = null;
        AdvanceNewPathVersion(updatedAt);
    }

    public void CompleteWithWarnings(
        long expectedStateVersion,
        string terminalMessage,
        DateTimeOffset updatedAt)
    {
        EnsureNewPathTransition(ScanExecutionPhase.Running, expectedStateVersion, updatedAt);
        string message = RequireTerminalMessage(terminalMessage);

        Phase = ScanExecutionPhase.CompletedWithWarnings;
        TerminalMessage = message;
        TerminalAt = updatedAt;
        InFlightJobId = null;
        PauseCategory = null;
        PauseCode = null;
        FailureCategory = null;
        FailureCode = null;
        AdvanceNewPathVersion(updatedAt);
    }

    public void FailIntegrity(
        long expectedStateVersion,
        string failureCode,
        DateTimeOffset updatedAt) =>
        FailNewPath(
            ScanExecutionPhase.FailedIntegrity,
            ScanExecutionFailureCategory.Integrity,
            expectedStateVersion,
            failureCode,
            updatedAt);

    public void FailSecurity(
        long expectedStateVersion,
        string failureCode,
        DateTimeOffset updatedAt) =>
        FailNewPath(
            ScanExecutionPhase.FailedSecurity,
            ScanExecutionFailureCategory.Security,
            expectedStateVersion,
            failureCode,
            updatedAt);

    public void Cancel(
        long expectedStateVersion,
        string cancelCode,
        DateTimeOffset updatedAt)
    {
        EnsureNewPathNonterminal();
        EnsureNewPathVersionAndTime(expectedStateVersion, updatedAt);
        string code = RequireMachineCode(cancelCode, nameof(cancelCode));

        Phase = ScanExecutionPhase.Cancelled;
        TerminalAt = updatedAt;
        InFlightJobId = null;
        PauseCategory = null;
        PauseCode = null;
        FailureCategory = ScanExecutionFailureCategory.Cancellation;
        FailureCode = code;
        AdvanceNewPathVersion(updatedAt);
    }

    /// <summary>
    /// Start a new Scan execution from an approved Pre-Scan Handoff. Validates
    /// every identity field is non-empty, the Profiled Revision is a bounded
    /// lowercase Git object ID, and creates the record in the
    /// <see cref="ScanExecutionPhase.Pass1Planning"/> phase. Throws
    /// <see cref="ArgumentException"/> on rejection.
    /// </summary>
    public static ScanExecution Start(
        Guid id,
        Guid repositoryId,
        Guid systemId,
        Guid scanRunId,
        Guid preScanResultId,
        string profiledRevision,
        DateTimeOffset startedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "The Scan execution identifier must be non-empty.",
                nameof(id));
        }

        if (repositoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Repository identifier must be non-empty.",
                nameof(repositoryId));
        }

        if (systemId == Guid.Empty)
        {
            throw new ArgumentException(
                "The System identifier must be non-empty.",
                nameof(systemId));
        }

        if (scanRunId == Guid.Empty)
        {
            throw new ArgumentException(
                "The ScanRun identifier must be non-empty.",
                nameof(scanRunId));
        }

        if (preScanResultId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Pre-Scan Result identifier must be non-empty.",
                nameof(preScanResultId));
        }

        string revision = RequireProfiledRevision(profiledRevision);

        return new(
            id,
            repositoryId,
            systemId,
            scanRunId,
            preScanResultId,
            revision,
            startedAt);
    }

    /// <summary>
    /// Transition from <see cref="ScanExecutionPhase.Pass1Planning"/> to
    /// <see cref="ScanExecutionPhase.Pass1Running"/> and set the total job
    /// count from the prepared Code Units.
    /// </summary>
    public void Plan(int totalJobCount, DateTimeOffset plannedAt)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Planning);
        ArgumentOutOfRangeException.ThrowIfNegative(totalJobCount, nameof(totalJobCount));
        if (totalJobCount == 0)
        {
            throw new ArgumentException(
                "The total job count must be positive for a Pass 1 dispatch.",
                nameof(totalJobCount));
        }

        Phase = ScanExecutionPhase.Pass1Running;
        TotalJobCount = totalJobCount;
    }

    /// <summary>
    /// Advance: dispatch one job. Sets <see cref="InFlightJobId"/> and
    /// requires <see cref="ScanExecutionPhase.Pass1Running"/> with no job
    /// already in flight.
    /// </summary>
    public void Advance(Guid jobId)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException(
                "The in-flight job identifier must be non-empty.",
                nameof(jobId));
        }

        EnsurePhase(ScanExecutionPhase.Pass1Running);
        if (InFlightJobId is not null)
        {
            throw new InvalidOperationException(
                "A job is already in flight; the runner dispatches one at a time.");
        }

        InFlightJobId = jobId;
    }

    /// <summary>
    /// Record a Valid result: clear <see cref="InFlightJobId"/> and increment
    /// <see cref="CompletedJobCount"/>. Requires the same job to be in flight.
    /// </summary>
    public void RecordValid(Guid jobId)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Running);
        EnsureInFlightMatch(jobId);
        InFlightJobId = null;
        CompletedJobCount++;
    }

    /// <summary>
    /// Mark Pass 1 durably complete while preserving the nonterminal boundary
    /// required before the prepared Pass 2 flow can begin.
    /// </summary>
    public void CompletePass1()
    {
        EnsurePhase(ScanExecutionPhase.Pass1Running);
        if (InFlightJobId is not null)
        {
            throw new InvalidOperationException(
                "Cannot complete Pass 1 while a job is in flight.");
        }

        if (CompletedJobCount != TotalJobCount)
        {
            throw new InvalidOperationException(
                "Cannot complete Pass 1 before every prepared job is valid.");
        }

        Phase = ScanExecutionPhase.Pass1Completed;
    }

    /// <summary>
    /// Begin the single prepared Pass 2 dispatch from the durable
    /// <see cref="ScanExecutionPhase.Pass1Completed"/> boundary.
    /// </summary>
    public void BeginPass2(Guid jobId)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Pass 2 job identifier must be non-empty.",
                nameof(jobId));
        }

        EnsurePhase(ScanExecutionPhase.Pass1Completed);
        if (InFlightJobId is not null)
        {
            throw new InvalidOperationException(
                "A Pass 2 job is already in flight.");
        }

        if (CompletedJobCount != TotalJobCount)
        {
            throw new InvalidOperationException(
                "Pass 2 cannot begin before every Pass 1 job is valid.");
        }

        InFlightJobId = jobId;
    }

    /// <summary>
    /// Complete the Scan only after the prepared Pass 2 result has committed.
    /// </summary>
    public void CompletePass2(
        Guid jobId,
        string successMessage,
        DateTimeOffset completedAt)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Completed);
        EnsureInFlightMatch(jobId);
        string message = RequireTerminalMessage(successMessage);
        if (completedAt < StartedAt)
        {
            throw new ArgumentException(
                "The terminal timestamp must be at or after the start timestamp.",
                nameof(completedAt));
        }

        Phase = ScanExecutionPhase.Completed;
        TerminalMessage = message;
        TerminalAt = completedAt;
        InFlightJobId = null;
    }

    /// <summary>
    /// Stop a non-committed Pass 2 outcome without exposing a successful Scan.
    /// </summary>
    public void FailPass2(
        Guid jobId,
        ScanExecutionFailureKind reasonKind,
        string terminalMessage,
        DateTimeOffset failedAt)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Completed);
        EnsureInFlightMatch(jobId);
        if (!Enum.IsDefined(reasonKind))
        {
            throw new ArgumentException(
                "The failure reason kind is not on the closed allowlist.",
                nameof(reasonKind));
        }

        string message = RequireTerminalMessage(terminalMessage);
        if (failedAt < StartedAt)
        {
            throw new ArgumentException(
                "The terminal timestamp must be at or after the start timestamp.",
                nameof(failedAt));
        }

        Phase = ScanExecutionPhase.Failed;
        TerminalReasonKind = reasonKind;
        TerminalMessage = message;
        TerminalAt = failedAt;
        InFlightJobId = null;
    }

    /// <summary>
    /// Record a failure: transition to <see cref="ScanExecutionPhase.Failed"/>
    /// with a bounded Chinese terminal message and closed failure kind. Clears
    /// <see cref="InFlightJobId"/>. The atomic transaction ensures no partial
    /// state is visible. No Document Version is committed by the runner.
    /// </summary>
    public void RecordFailure(
        Guid jobId,
        ScanExecutionFailureKind reasonKind,
        string terminalMessage,
        DateTimeOffset failedAt)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Running);
        EnsureInFlightMatch(jobId);
        if (!Enum.IsDefined(reasonKind))
        {
            throw new ArgumentException(
                "The failure reason kind is not on the closed allowlist.",
                nameof(reasonKind));
        }

        string message = RequireTerminalMessage(terminalMessage);
        if (failedAt < StartedAt)
        {
            throw new ArgumentException(
                "The terminal timestamp must be at or after the start timestamp.",
                nameof(failedAt));
        }

        Phase = ScanExecutionPhase.Failed;
        TerminalReasonKind = reasonKind;
        TerminalMessage = message;
        TerminalAt = failedAt;
        InFlightJobId = null;
    }

    /// <summary>
    /// Record a boundary rejection failure without an in-flight job. Used
    /// when the packet factory rejects forbidden/oversized content before
    /// dispatch. Transitions from <see cref="ScanExecutionPhase.Pass1Running"/>
    /// to <see cref="ScanExecutionPhase.Failed"/>.
    /// </summary>
    public void RecordBoundaryRejection(
        ScanExecutionFailureKind reasonKind,
        string terminalMessage,
        DateTimeOffset failedAt)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Running);
        if (InFlightJobId is not null)
        {
            throw new InvalidOperationException(
                "Boundary rejection must not be recorded while a job is in flight.");
        }

        if (!Enum.IsDefined(reasonKind))
        {
            throw new ArgumentException(
                "The failure reason kind is not on the closed allowlist.",
                nameof(reasonKind));
        }

        string message = RequireTerminalMessage(terminalMessage);
        if (failedAt < StartedAt)
        {
            throw new ArgumentException(
                "The terminal timestamp must be at or after the start timestamp.",
                nameof(failedAt));
        }

        Phase = ScanExecutionPhase.Failed;
        TerminalReasonKind = reasonKind;
        TerminalMessage = message;
        TerminalAt = failedAt;
    }

    /// <summary>
    /// Finalize: when <see cref="CompletedJobCount"/> equals
    /// <see cref="TotalJobCount"/>, transition through
    /// <see cref="ScanExecutionPhase.Pass1Completed"/> to
    /// <see cref="ScanExecutionPhase.Completed"/> with a bounded Chinese
    /// success message and the exact job count.
    /// </summary>
    public void Finalize(string successMessage, DateTimeOffset completedAt)
    {
        EnsurePhase(ScanExecutionPhase.Pass1Running);
        if (InFlightJobId is not null)
        {
            throw new InvalidOperationException(
                "Cannot finalize while a job is in flight.");
        }

        if (CompletedJobCount != TotalJobCount)
        {
            throw new InvalidOperationException(
                "Cannot finalize: not all jobs have returned Valid results.");
        }

        string message = RequireTerminalMessage(successMessage);
        if (completedAt < StartedAt)
        {
            throw new ArgumentException(
                "The terminal timestamp must be at or after the start timestamp.",
                nameof(completedAt));
        }

        Phase = ScanExecutionPhase.Pass1Completed;
        Phase = ScanExecutionPhase.Completed;
        TerminalMessage = message;
        TerminalAt = completedAt;
    }

    /// <summary>
    /// Cancel: transition from <see cref="ScanExecutionPhase.Pass1Planning"/>
    /// or <see cref="ScanExecutionPhase.Pass1Running"/> to
    /// <see cref="ScanExecutionPhase.Cancelled"/> with a bounded Chinese
    /// message. No LLM resume.
    /// </summary>
    public void Cancel(string cancelMessage, DateTimeOffset cancelledAt)
    {
        if (Phase is not (ScanExecutionPhase.Pass1Planning or ScanExecutionPhase.Pass1Running))
        {
            throw new InvalidOperationException(
                "A Scan execution can only be cancelled from Pass1Planning or Pass1Running.");
        }

        string message = RequireTerminalMessage(cancelMessage);
        if (cancelledAt < StartedAt)
        {
            throw new ArgumentException(
                "The terminal timestamp must be at or after the start timestamp.",
                nameof(cancelledAt));
        }

        Phase = ScanExecutionPhase.Cancelled;
        TerminalMessage = message;
        TerminalAt = cancelledAt;
        InFlightJobId = null;
    }

    /// <summary>
    /// Record a restart re-read observation. Increments
    /// <see cref="RestartIndicator"/> (bounded 0–
    /// <see cref="MaxRestartIndicator"/>) only if a job is in flight. Does
    /// NOT resume the LLM job and does NOT fabricate a result. Does NOT
    /// mutate <see cref="Phase"/>.
    /// </summary>
    public void ObserveRestart()
    {
        if (Phase != ScanExecutionPhase.Pass1Running)
        {
            return;
        }

        if (InFlightJobId is null)
        {
            return;
        }

        if (RestartIndicator < MaxRestartIndicator)
        {
            RestartIndicator++;
        }
    }

    private void FailNewPath(
        ScanExecutionPhase terminalPhase,
        ScanExecutionFailureCategory failureCategory,
        long expectedStateVersion,
        string failureCode,
        DateTimeOffset updatedAt)
    {
        EnsureNewPathNonterminal();
        EnsureNewPathVersionAndTime(expectedStateVersion, updatedAt);
        string code = RequireMachineCode(failureCode, nameof(failureCode));

        Phase = terminalPhase;
        TerminalAt = updatedAt;
        InFlightJobId = null;
        PauseCategory = null;
        PauseCode = null;
        FailureCategory = failureCategory;
        FailureCode = code;
        AdvanceNewPathVersion(updatedAt);
    }

    private void EnsureNewPathNonterminal()
    {
        if (Phase is not (ScanExecutionPhase.Prepared or
            ScanExecutionPhase.Running or
            ScanExecutionPhase.PausedProviderUnavailable or
            ScanExecutionPhase.PausedOperatorCorrection))
        {
            throw new InvalidOperationException(
                "The new-path Scan Execution is not in a mutable phase.");
        }
    }

    private void EnsureNewPathTransition(
        ScanExecutionPhase expectedPhase,
        long expectedStateVersion,
        DateTimeOffset updatedAt)
    {
        if (Phase != expectedPhase)
        {
            throw new InvalidOperationException(
                $"The Scan execution is in phase {Phase}; expected {expectedPhase}.");
        }

        EnsureNewPathVersionAndTime(expectedStateVersion, updatedAt);
    }

    private void EnsureNewPathVersionAndTime(
        long expectedStateVersion,
        DateTimeOffset updatedAt)
    {
        if (StateVersion != expectedStateVersion || UpdatedAt is null)
        {
            throw new InvalidOperationException(
                "The new-path Scan Execution state version is stale.");
        }

        RequireUtc(updatedAt, nameof(updatedAt));
        if (updatedAt < UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAt));
        }
    }

    private void AdvanceNewPathVersion(DateTimeOffset updatedAt)
    {
        StateVersion++;
        UpdatedAt = updatedAt;
    }

    private void EnsurePhase(ScanExecutionPhase expected)
    {
        if (Phase != expected)
        {
            throw new InvalidOperationException(
                $"The Scan execution is in phase {Phase}; expected {expected}.");
        }
    }

    private void EnsureInFlightMatch(Guid jobId)
    {
        if (InFlightJobId is null || InFlightJobId != jobId)
        {
            throw new InvalidOperationException(
                "The in-flight job identifier does not match the recorded job.");
        }
    }

    private static string RequireProfiledRevision(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > MaximumProfiledRevisionLength ||
            normalized.Any(character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "The Profiled Revision must be a bounded lowercase Git object ID.",
                nameof(value));
        }

        return normalized;
    }

    private static void ThrowIfEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identity must not be empty.", parameterName);
        }
    }

    private static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use the UTC offset.", parameterName);
        }
    }

    private static string RequireIdentity(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length > MaximumIdentityLength ||
            normalized.Any(character =>
                character <= '\u001f' || character is >= '\u007f' and <= '\u009f') ||
            Path.IsPathRooted(normalized))
        {
            throw new ArgumentException(
                "Identity must be bounded and must not contain a host path.",
                parameterName);
        }

        return normalized;
    }

    private static string RequireRuntimeIdentityJson(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (Encoding.UTF8.GetByteCount(normalized) > MaximumRuntimeIdentityUtf8Bytes)
        {
            throw new ArgumentException(
                "Analysis Runtime Identity exceeds the finite UTF-8 byte limit.",
                nameof(value));
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(normalized);
            if (ContainsRootedPath(document.RootElement))
            {
                throw new ArgumentException(
                    "Analysis Runtime Identity must not contain an absolute host path.",
                    nameof(value));
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "Analysis Runtime Identity must be valid JSON.",
                nameof(value),
                exception);
        }

        return normalized;
    }

    private static bool ContainsRootedPath(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return Path.IsPathRooted(element.GetString());
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().Any(ContainsRootedPath);
        }

        return element.ValueKind == JsonValueKind.Object &&
            element.EnumerateObject().Any(property => ContainsRootedPath(property.Value));
    }

    private static string RequireSha256(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != 64 ||
            normalized.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "Identity hash must be a lowercase SHA-256 value.",
                parameterName);
        }

        return normalized;
    }

    private static string RequireMachineCode(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string normalized = value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        if (normalized.Length > MaximumMachineCodeLength ||
            normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException(
                "Code must be a bounded machine-readable value.",
                parameterName);
        }

        return normalized;
    }

    private static string RequireTerminalMessage(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Any(character =>
                character <= '\u001f' ||
                character is >= '\u007f' and <= '\u009f'))
        {
            throw new ArgumentException(
                "The terminal message contains forbidden control characters.",
                nameof(value));
        }

        int byteCount = Encoding.UTF8.GetByteCount(normalized);
        if (byteCount > MaximumTerminalMessageUtf8Bytes)
        {
            throw new ArgumentException(
                "The terminal message exceeds the finite UTF-8 byte limit.",
                nameof(value));
        }

        return normalized;
    }
}
