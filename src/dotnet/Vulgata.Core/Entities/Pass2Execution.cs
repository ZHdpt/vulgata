using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Core.Entities;

public enum Pass2ExecutionState
{
    Planning,
    Running,
    ProbingProvider,
    PausedProviderUnavailable,
    PausedOperatorCorrection,
    Completing,
    Completed,
    CompletedWithWarnings,
    FailedIntegrity,
    FailedSecurity,
    Cancelled,
}

public sealed class Pass2Execution
{
    private Pass2Execution()
    {
    }

    public static Pass2Execution CreatePlanning(
        Guid id,
        Guid pass1PairExecutionId,
        Guid coordinationId,
        Guid systemId,
        DateTimeOffset startedAt,
        string authorizationSubject = "server-authorized",
        string workerPromptIdentity = "pass2-business-logic-worker.v1",
        string submissionSchemaIdentity = "business-logic/flow-v1",
        string toolDeclarationFingerprint = "6c52bc54c73c8f52b4e0a62b0771fb03f47e0340a6f039145eb02cf217cbd93f",
        string? workerPromptFingerprint = null,
        string? submissionSchemaFingerprint = null)
    {
        workerPromptFingerprint ??= Fingerprint(
            "pass2-unbound-prompt/1.0", workerPromptIdentity);
        submissionSchemaFingerprint ??= Fingerprint(
            "pass2-unbound-schema/1.0", submissionSchemaIdentity);
        if (id == Guid.Empty || pass1PairExecutionId == Guid.Empty ||
            coordinationId == Guid.Empty || systemId == Guid.Empty ||
            startedAt.Offset != TimeSpan.Zero ||
            string.IsNullOrWhiteSpace(authorizationSubject) ||
            authorizationSubject.Trim().Length > 450 ||
            string.IsNullOrWhiteSpace(workerPromptIdentity) || workerPromptIdentity.Length > 128 ||
            string.IsNullOrWhiteSpace(submissionSchemaIdentity) || submissionSchemaIdentity.Length > 128 ||
            !IsFingerprint(workerPromptFingerprint) ||
            !IsFingerprint(submissionSchemaFingerprint) ||
            !IsFingerprint(toolDeclarationFingerprint))
        {
            throw new ArgumentException("A complete UTC-bound Pass 2 execution identity is required.");
        }

        string initialResolutionFingerprint = Fingerprint(
            "pass2-resolution-authority/1.0", pass1PairExecutionId.ToString("D"));
        string initialPlanningFingerprint = Fingerprint(
            "pass2-planning-authority/1.0", id.ToString("D"));
        string initialExecutionFingerprint = Fingerprint(
            "pass2-execution-authority/1.0",
            initialResolutionFingerprint,
            initialPlanningFingerprint,
            workerPromptIdentity,
            workerPromptFingerprint,
            submissionSchemaIdentity,
            submissionSchemaFingerprint,
            toolDeclarationFingerprint);
        return new Pass2Execution
        {
            Id = id,
            Pass1PairExecutionId = pass1PairExecutionId,
            CoordinationId = coordinationId,
            SystemId = systemId,
            AuthorizationSubject = authorizationSubject.Trim(),
            WorkerPromptIdentity = workerPromptIdentity,
            WorkerPromptFingerprint = workerPromptFingerprint,
            SubmissionSchemaIdentity = submissionSchemaIdentity,
            SubmissionSchemaFingerprint = submissionSchemaFingerprint,
            ToolDeclarationFingerprint = toolDeclarationFingerprint,
            ResolutionFingerprint = initialResolutionFingerprint,
            PlanningFingerprint = initialPlanningFingerprint,
            ExecutionFingerprint = initialExecutionFingerprint,
            State = Pass2ExecutionState.Planning,
            StartedAt = startedAt,
            UpdatedAt = startedAt,
        };
    }

    public void MarkRunning(DateTimeOffset changedAt)
    {
        if (State != Pass2ExecutionState.Planning ||
            changedAt.Offset != TimeSpan.Zero || changedAt < UpdatedAt)
        {
            throw new InvalidOperationException("Only a complete planned execution can start running.");
        }

        State = Pass2ExecutionState.Running;
        StateVersion++;
        UpdatedAt = changedAt;
    }

    public void RecordPlan(int plannedCount)
    {
        if (State != Pass2ExecutionState.Planning || PlanRecorded ||
            plannedCount < 0 || plannedCount > 256)
        {
            throw new InvalidOperationException("Only a bounded planning execution can record its denominator.");
        }

        PlannedCount = plannedCount;
        PlanRecorded = true;
    }

    public void RecordAuthority(
        int candidateCount,
        string resolutionFingerprint,
        string planningFingerprint,
        string executionFingerprint)
    {
        if (State != Pass2ExecutionState.Planning || candidateCount < 0 || candidateCount > 256 ||
            !IsFingerprint(resolutionFingerprint) || !IsFingerprint(planningFingerprint) ||
            !IsFingerprint(executionFingerprint))
        {
            throw new InvalidOperationException("The canonical Pass 2 authority is invalid.");
        }

        CandidateCount = candidateCount;
        ResolutionFingerprint = resolutionFingerprint;
        PlanningFingerprint = planningFingerprint;
        ExecutionFingerprint = executionFingerprint;
    }

    public void RecordPublished(long expectedStateVersion, DateTimeOffset changedAt)
    {
        RequireRunningAccountingTransition(expectedStateVersion, changedAt);
        PublishedCount++;
        StateVersion++;
        UpdatedAt = changedAt;
    }

    public void RecordFailed(long expectedStateVersion, DateTimeOffset changedAt)
    {
        RequireRunningAccountingTransition(expectedStateVersion, changedAt);
        FailedCount++;
        StateVersion++;
        UpdatedAt = changedAt;
    }

    public void PauseProviderUnavailable(long expectedStateVersion, DateTimeOffset changedAt)
    {
        RequireState(Pass2ExecutionState.Running, expectedStateVersion, changedAt);
        State = Pass2ExecutionState.PausedProviderUnavailable;
        StateVersion++;
        UpdatedAt = changedAt;
    }

    public void ResumeProvider(long expectedStateVersion, DateTimeOffset changedAt)
    {
        RequireState(Pass2ExecutionState.PausedProviderUnavailable, expectedStateVersion, changedAt);
        State = Pass2ExecutionState.Running;
        StateVersion++;
        UpdatedAt = changedAt;
    }

    public void Complete(
        long expectedStateVersion,
        bool withWarnings,
        int incompleteCount,
        int boundExcludedCount,
        int omissionCount,
        string terminalWarningInventory,
        DateTimeOffset changedAt)
    {
        RequireState(Pass2ExecutionState.Running, expectedStateVersion, changedAt);
        ArgumentNullException.ThrowIfNull(terminalWarningInventory);
        if (incompleteCount < 0 || boundExcludedCount < 0 || omissionCount < 0)
        {
            throw new InvalidOperationException("The bounded Pass 2 terminal summary is invalid.");
        }
        int terminalCount = checked(
            PublishedCount + FailedCount + incompleteCount + boundExcludedCount + omissionCount);
        if (!PlanRecorded || terminalCount != PlannedCount || terminalCount > 256 ||
            terminalWarningInventory.Length > 65_536)
        {
            throw new InvalidOperationException("The bounded Pass 2 terminal summary is invalid.");
        }

        TerminalIncompleteCount = incompleteCount;
        TerminalBoundExcludedCount = boundExcludedCount;
        TerminalOmissionCount = omissionCount;
        TerminalWarningInventory = terminalWarningInventory;
        State = withWarnings
            ? Pass2ExecutionState.CompletedWithWarnings
            : Pass2ExecutionState.Completed;
        StateVersion++;
        UpdatedAt = changedAt;
    }

    public void FailIntegrity(long expectedStateVersion, DateTimeOffset changedAt) =>
        Fail(
            Pass2ExecutionState.FailedIntegrity,
            expectedStateVersion,
            changedAt);

    public void FailSecurity(long expectedStateVersion, DateTimeOffset changedAt) =>
        Fail(
            Pass2ExecutionState.FailedSecurity,
            expectedStateVersion,
            changedAt);

    public void RejectCorruptTerminal(long expectedStateVersion, DateTimeOffset changedAt)
    {
        if (State is not (Pass2ExecutionState.Completed or Pass2ExecutionState.CompletedWithWarnings) ||
            StateVersion != expectedStateVersion || changedAt.Offset != TimeSpan.Zero ||
            changedAt < UpdatedAt)
        {
            throw new InvalidOperationException("Only the current corrupt terminal execution can be rejected.");
        }

        State = Pass2ExecutionState.FailedIntegrity;
        StateVersion++;
        UpdatedAt = changedAt;
    }

    public Guid Id { get; private set; }
    public Guid Pass1PairExecutionId { get; private set; }
    public Guid CoordinationId { get; private set; }
    public Guid SystemId { get; private set; }
    public string AuthorizationSubject { get; private set; } = string.Empty;
    public string WorkerPromptIdentity { get; private set; } = string.Empty;
    public string WorkerPromptFingerprint { get; private set; } = string.Empty;
    public string SubmissionSchemaIdentity { get; private set; } = string.Empty;
    public string SubmissionSchemaFingerprint { get; private set; } = string.Empty;
    public string ToolDeclarationFingerprint { get; private set; } = string.Empty;
    public int CandidateCount { get; private set; }
    public string ResolutionFingerprint { get; private set; } = string.Empty;
    public string PlanningFingerprint { get; private set; } = string.Empty;
    public string ExecutionFingerprint { get; private set; } = string.Empty;
    public Pass2ExecutionState State { get; private set; }
    public int PublishedCount { get; private set; }
    public int FailedCount { get; private set; }
    public int PlannedCount { get; private set; }
    public bool PlanRecorded { get; private set; }
    public int TerminalIncompleteCount { get; private set; }
    public int TerminalBoundExcludedCount { get; private set; }
    public int TerminalOmissionCount { get; private set; }
    public string? TerminalWarningInventory { get; private set; }
    public long StateVersion { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private void RequireRunningAccountingTransition(
        long expectedStateVersion,
        DateTimeOffset changedAt)
    {
        if (State != Pass2ExecutionState.Running || StateVersion != expectedStateVersion ||
            changedAt.Offset != TimeSpan.Zero || changedAt < UpdatedAt)
        {
            throw new InvalidOperationException(
                "Only the current running Pass 2 execution can advance terminal accounting.");
        }
    }

    private void RequireState(
        Pass2ExecutionState expectedState,
        long expectedStateVersion,
        DateTimeOffset changedAt)
    {
        if (State != expectedState || StateVersion != expectedStateVersion ||
            changedAt.Offset != TimeSpan.Zero || changedAt < UpdatedAt)
        {
            throw new InvalidOperationException("The current Pass 2 execution state is required.");
        }
    }

    private void Fail(
        Pass2ExecutionState terminalState,
        long expectedStateVersion,
        DateTimeOffset changedAt)
    {
        RequireState(Pass2ExecutionState.Running, expectedStateVersion, changedAt);
        State = terminalState;
        StateVersion++;
        UpdatedAt = changedAt;
    }

    private static bool IsFingerprint(string value) =>
        value.Length == 64 && value.All(character =>
            char.IsAsciiHexDigit(character) && !char.IsAsciiLetterUpper(character));

    private static string Fingerprint(params string[] values) =>
        Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\n', values))));
}
