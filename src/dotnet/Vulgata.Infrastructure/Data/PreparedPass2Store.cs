using System.Data;
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public enum PreparedPass2StartOutcome
{
    Created,
    Replay,
    Conflict,
    Rejected,
    NotFound,
    Cancelled,
    Failed,
}

public sealed record PreparedPass2StartResult(
    PreparedPass2StartOutcome Outcome,
    Guid? ExecutionId,
    int FlowGroupCount,
    int OmissionCount,
    int JobCount,
    int RetrievalBudgetCount,
    bool IsReplay);

internal sealed record Pass2RetrievalReservation(
    bool IsReserved,
    int AttemptOrdinal,
    int CallOrdinal,
    string ToolCallIdFingerprint,
    string RequestFingerprint,
    long JobStateVersion,
    Guid LeaseToken,
    DateTimeOffset LeaseExpiresAt,
    Pass2CodeLogicRetrievalRemainingBudget RemainingBudget)
{
    internal Func<CancellationToken, Task<bool>>? AdmissionAuthorityCheck { get; init; }
}

internal sealed record Pass2ExplicitRetrievalResolution(
    Pass2RetrievalReservation? Reservation,
    Pass2CodeLogicRetrievalResult? CompletedResult,
    Pass2RetrievalReceipt? CompletedReceipt = null);

public sealed class PreparedPass2Store(VulgataDbContext dbContext)
{
    private const int _maxFlowGroups = 10;
    private readonly VulgataDbContext _dbContext = dbContext ??
        throw new ArgumentNullException(nameof(dbContext));

    internal async Task<Pass2RetrievalReservation> ReserveRetrievalAsync(
        Guid pass2JobExecutionId,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await ReserveRetrievalOnceAsync(
                    pass2JobExecutionId,
                    request,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (attempt < 3 && IsSerializationFailure(exception))
            {
                _dbContext.ChangeTracker.Clear();
            }
        }
    }

    private async Task<Pass2RetrievalReservation> ReserveRetrievalOnceAsync(
        Guid pass2JobExecutionId,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(pass2JobExecutionId, Guid.Empty);
        _dbContext.ChangeTracker.Clear();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        Pass2RetrievalBudget? budget = await LockedBudgetQuery(pass2JobExecutionId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        Pass2JobExecution? job = await _dbContext.Pass2JobExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == pass2JobExecutionId, cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (budget is null || job is null || job.State != Pass2JobExecutionState.Running ||
            job.AttemptCount is < 1 or > 2 || job.LeaseToken is null ||
            job.LeaseExpiresAt is null || job.LeaseExpiresAt <= now)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return new(false, 0, 0, string.Empty, string.Empty, 0, Guid.Empty,
                DateTimeOffset.MinValue, EmptyRemainingBudget());
        }

        int attemptOrdinal = Math.Max(1, job.AttemptCount);
        if (budget.CallsReserved >= Pass2RetrievalContracts.MaxCalls)
        {
            Pass2CodeLogicRetrievalRemainingBudget remaining = RemainingBudget(budget);
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return new(false, attemptOrdinal, 0, string.Empty, string.Empty,
                job.StateVersion, job.LeaseToken.Value, job.LeaseExpiresAt.Value, remaining);
        }

        int callOrdinal = budget.ReserveCall(DateTimeOffset.UtcNow);
        string requestFingerprint = FingerprintRequest(request);
        string toolCallIdFingerprint = LowerSha256(string.Join(
            '\n',
            "pass2-automatic-tool-call/1.0",
            pass2JobExecutionId.ToString("D"),
            attemptOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
            callOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Pass2CodeLogicRetrievalRemainingBudget reservedRemaining = RemainingBudget(budget);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _dbContext.ChangeTracker.Clear();
        return new(
            true,
            attemptOrdinal,
            callOrdinal,
            toolCallIdFingerprint,
            requestFingerprint,
            job.StateVersion,
            job.LeaseToken.Value,
            job.LeaseExpiresAt.Value,
            reservedRemaining);
    }

    internal async Task<Pass2ExplicitRetrievalResolution> ResolveExplicitRetrievalAsync(
        Guid pass2JobExecutionId,
        Pass2RetrievalCallIdentity identity,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(request);
        _dbContext.ChangeTracker.Clear();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        Pass2RetrievalBudget? budget = await LockedBudgetQuery(pass2JobExecutionId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        Pass2JobExecution? job = await _dbContext.Pass2JobExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == pass2JobExecutionId, cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (budget is null || job is null || job.State != Pass2JobExecutionState.Running ||
            job.LeaseToken is null || job.LeaseExpiresAt is null || job.LeaseExpiresAt <= now ||
            identity.AttemptOrdinal != job.AttemptCount)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return new(null, Pass2CodeLogicRetrievalResult.Refused());
        }

        string requestFingerprint = FingerprintRequest(request);
        Pass2RetrievalReceipt? receipt = await _dbContext.Pass2RetrievalReceipts.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Pass2JobExecutionId == pass2JobExecutionId &&
                    value.CallOrdinal == identity.CallOrdinal,
                cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            if (receipt.AttemptOrdinal != identity.AttemptOrdinal ||
                !StringComparer.Ordinal.Equals(
                    receipt.ToolCallIdFingerprint,
                    identity.ToolCallIdFingerprint) ||
                !StringComparer.Ordinal.Equals(receipt.RequestFingerprint, requestFingerprint))
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                _dbContext.ChangeTracker.Clear();
                return new(null, Pass2CodeLogicRetrievalResult.Refused());
            }

            Pass2CodeLogicRetrievalResult replay = Rehydrate(receipt, budget);
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return new(null, replay, receipt);
        }

        if (identity.CallOrdinal <= budget.CallsReserved ||
            identity.CallOrdinal != budget.CallsReserved + 1 ||
            budget.CallsReserved >= Pass2RetrievalContracts.MaxCalls)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return new(null, Pass2CodeLogicRetrievalResult.Refused());
        }

        int callOrdinal = budget.ReserveCall(DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Pass2RetrievalReservation reservation = new(
            true,
            identity.AttemptOrdinal,
            callOrdinal,
            identity.ToolCallIdFingerprint,
            requestFingerprint,
            job.StateVersion,
            job.LeaseToken.Value,
            job.LeaseExpiresAt.Value,
            RemainingBudget(budget));
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _dbContext.ChangeTracker.Clear();
        return new(reservation, null);
    }

    internal async Task<Pass2CodeLogicRetrievalResult> AdmitRetrievalAsync(
        Guid pass2JobExecutionId,
        Pass2RetrievalReservation reservation,
        Pass2CodeLogicRetrievalRequest request,
        Pass2CodeLogicRetrievalResult candidate,
        IReadOnlyCollection<Guid> initialDocumentVersionIds,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await AdmitRetrievalOnceAsync(
                    pass2JobExecutionId,
                    reservation,
                    request,
                    candidate,
                    initialDocumentVersionIds,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (attempt < 3 && IsSerializationFailure(exception))
            {
                _dbContext.ChangeTracker.Clear();
            }
        }
    }

    private async Task<Pass2CodeLogicRetrievalResult> AdmitRetrievalOnceAsync(
        Guid pass2JobExecutionId,
        Pass2RetrievalReservation reservation,
        Pass2CodeLogicRetrievalRequest request,
        Pass2CodeLogicRetrievalResult candidate,
        IReadOnlyCollection<Guid> initialDocumentVersionIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(initialDocumentVersionIds);
        if (!reservation.IsReserved || reservation.AttemptOrdinal < 1 || reservation.CallOrdinal < 1)
        {
            throw new ArgumentException("A durable retrieval reservation is required.", nameof(reservation));
        }

        _dbContext.ChangeTracker.Clear();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        Pass2RetrievalBudget budget = await LockedBudgetQuery(pass2JobExecutionId)
            .SingleAsync(cancellationToken).ConfigureAwait(false);
        Pass2JobExecution? currentJob = await _dbContext.Pass2JobExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == pass2JobExecutionId, cancellationToken)
            .ConfigureAwait(false);
        if (currentJob is null || currentJob.State != Pass2JobExecutionState.Running ||
            currentJob.AttemptCount != reservation.AttemptOrdinal ||
            currentJob.StateVersion != reservation.JobStateVersion ||
            currentJob.LeaseToken != reservation.LeaseToken ||
            currentJob.LeaseExpiresAt != reservation.LeaseExpiresAt ||
            currentJob.LeaseExpiresAt <= DateTimeOffset.UtcNow ||
            reservation.CallOrdinal > budget.CallsReserved ||
            await _dbContext.Pass2RetrievalReceipts.AsNoTracking().AnyAsync(
                value => value.Pass2JobExecutionId == pass2JobExecutionId &&
                    value.CallOrdinal == reservation.CallOrdinal,
                cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        if (reservation.AdmissionAuthorityCheck is not null &&
            !await reservation.AdmissionAuthorityCheck(cancellationToken).ConfigureAwait(false))
        {
            candidate = Pass2CodeLogicRetrievalResult.Refused();
        }

        Guid[] candidateDocumentIds = candidate.Items
            .Select(value => value.DocumentVersionId)
            .Distinct()
            .ToArray();
        int currentCandidateCount = await _dbContext.DocumentVersions.AsNoTracking()
            .Where(value => candidateDocumentIds.Contains(value.Id) &&
                value.SupersededById == null &&
                _dbContext.LogicalDocuments.Any(logical =>
                    logical.Id == value.LogicalDocumentId &&
                    logical.CurrentVersionId == value.Id &&
                    logical.RepositoryId == value.RepositoryId &&
                    logical.DocumentType == DocumentType.CodeLogic))
            .CountAsync(cancellationToken).ConfigureAwait(false);
        if (currentCandidateCount != candidateDocumentIds.Length)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        Pass2CodeLogicRetrievalItem[] canonicalItems = candidate.Outcome ==
                Pass2CodeLogicRetrievalOutcome.Accepted
            ? candidate.Items
                .GroupBy(value => value.OpaqueHandle, StringComparer.Ordinal)
                .Select(grouping => grouping.First())
                .ToArray()
            : Array.Empty<Pass2CodeLogicRetrievalItem>();
        if (canonicalItems.Length != candidate.Items.Count)
        {
            candidate = Pass2CodeLogicRetrievalResult.Accepted(canonicalItems);
        }

        int resultCount = candidate.Outcome == Pass2CodeLogicRetrievalOutcome.Accepted
            ? canonicalItems.Length
            : 0;
        int returnedUtf8Bytes = candidate.Outcome == Pass2CodeLogicRetrievalOutcome.Accepted
            ? checked(canonicalItems.Sum(value => value.ReturnedUtf8Bytes))
            : 0;
        Pass2RetrievalResult[] admittedResults = await _dbContext.Pass2RetrievalResults
            .AsNoTracking()
            .Where(value => value.Pass2JobExecutionId == pass2JobExecutionId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        HashSet<Guid> admittedAdditionalDocuments = admittedResults
            .Select(value => value.DocumentVersionId)
            .Where(value => !initialDocumentVersionIds.Contains(value))
            .ToHashSet();
        int additionalDocuments = candidate.Outcome == Pass2CodeLogicRetrievalOutcome.Accepted
            ? canonicalItems.Select(value => value.DocumentVersionId)
                .Where(value => !initialDocumentVersionIds.Contains(value) &&
                    !admittedAdditionalDocuments.Contains(value))
                .Distinct()
                .Count()
            : 0;
        bool exceedsSectionBound = canonicalItems
            .Where(value => value.Kind == Pass2CodeLogicRetrievalItemKind.Section &&
                value.SectionId.HasValue)
            .GroupBy(value => value.DocumentVersionId)
            .Any(grouping => admittedResults
                .Where(value =>
                    value.Kind == Pass2CodeLogicRetrievalItemKind.Section &&
                    value.DocumentVersionId == grouping.Key)
                .Select(value => value.ReadSectionId())
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .Concat(grouping.Select(value => value.SectionId!.Value))
                .Distinct()
                .Count() > Pass2RetrievalContracts.MaxSectionsPerDocument);
        bool exceedsBound = budget.ResultsReturned + resultCount > Pass2RetrievalContracts.MaxResults ||
            budget.ReturnedUtf8Bytes + returnedUtf8Bytes > Pass2RetrievalContracts.MaxReturnedUtf8Bytes ||
            budget.AdditionalDocumentCount + additionalDocuments >
                Pass2RetrievalContracts.MaxAdditionalDocuments ||
            exceedsSectionBound;
        Pass2RetrievalReceiptState receiptState = exceedsBound
            ? Pass2RetrievalReceiptState.BoundExceeded
            : ReceiptState(candidate.Outcome);
        Guid receiptId = DeriveId(
            "pass2-retrieval-receipt/1.0",
            pass2JobExecutionId,
            GuidFromOrdinal(reservation.AttemptOrdinal),
            GuidFromOrdinal(reservation.CallOrdinal));
        Pass2RetrievalReceipt receipt = Pass2RetrievalReceipt.Create(
            receiptId,
            pass2JobExecutionId,
            reservation.AttemptOrdinal,
            reservation.CallOrdinal,
            reservation.ToolCallIdFingerprint,
            receiptState,
            request.Operation,
            reservation.RequestFingerprint,
            exceedsBound ? 0 : resultCount,
            exceedsBound ? 0 : returnedUtf8Bytes,
            exceedsBound ? 0 : additionalDocuments,
            ResultSetFingerprint(exceedsBound ? Array.Empty<Pass2CodeLogicRetrievalItem>() : candidate.Items),
            DateTimeOffset.UtcNow);
        _dbContext.Pass2RetrievalReceipts.Add(receipt);

        if (!exceedsBound && candidate.Outcome == Pass2CodeLogicRetrievalOutcome.Accepted)
        {
            budget.Admit(
                resultCount,
                additionalDocuments,
                returnedUtf8Bytes,
                DateTimeOffset.UtcNow);
            Guid[] documentVersionIds = candidate.Items
                .Select(value => value.DocumentVersionId)
                .Distinct()
                .ToArray();
            Dictionary<Guid, DocumentVersion> documents = await _dbContext.DocumentVersions
                .AsNoTracking()
                .Where(value => documentVersionIds.Contains(value.Id))
                .ToDictionaryAsync(value => value.Id, cancellationToken).ConfigureAwait(false);
            DateTimeOffset createdAt = receipt.CreatedAt;
            for (int index = 0; index < candidate.Items.Count; index++)
            {
                Pass2CodeLogicRetrievalItem item = candidate.Items[index];
                DocumentVersion document = documents[item.DocumentVersionId];
                int resultOrdinal = index + 1;
                Guid resultId = Guid.ParseExact(item.OpaqueHandle, "D");
                Pass2RetrievalResult? existingResult = admittedResults.SingleOrDefault(
                    value => value.Id == resultId);
                if (existingResult is not null)
                {
                    if (!PersistedResultMatches(
                            existingResult,
                            item,
                            document,
                            pass2JobExecutionId,
                            reservation.AttemptOrdinal))
                    {
                        await transaction.RollbackAsync(CancellationToken.None)
                            .ConfigureAwait(false);
                        _dbContext.ChangeTracker.Clear();
                        return Pass2CodeLogicRetrievalResult.Refused();
                    }

                    continue;
                }

                _dbContext.Pass2RetrievalResults.Add(Pass2RetrievalResult.CreateAdmitted(
                    resultId,
                    receiptId,
                    pass2JobExecutionId,
                    reservation.AttemptOrdinal,
                    resultOrdinal,
                    item.Kind,
                    item.DocumentVersionId,
                    document.RepositoryId,
                    document.ScanRunId,
                    item.SectionId,
                    item.SourceAnchorId,
                    item.EdgeId,
                    item.ByteStart,
                    item.ByteEnd,
                    FingerprintDescriptor(item),
                    FingerprintResult(item),
                    item.ReturnedUtf8Bytes,
                    createdAt));
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Pass2CodeLogicRetrievalRemainingBudget remaining = RemainingBudget(budget);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _dbContext.ChangeTracker.Clear();
        return exceedsBound
            ? Pass2CodeLogicRetrievalResult.BoundExceeded(remaining)
            : candidate.WithRemainingBudget(remaining);
    }

    public async Task<PreparedPass2StartResult> StartAsync(
        Guid pass1PairExecutionId,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(pass1PairExecutionId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(scope);
        if (cancellationToken.IsCancellationRequested)
        {
            return Result(PreparedPass2StartOutcome.Cancelled);
        }

        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken).ConfigureAwait(false);

            Pass1PairExecution? pair = await _dbContext.Pass1PairExecutions.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == pass1PairExecutionId, cancellationToken)
                .ConfigureAwait(false);
            if (pair is null)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return Result(PreparedPass2StartOutcome.NotFound);
            }

            PreparedScanManifest[] manifests = await _dbContext.PreparedScanManifests.AsNoTracking()
                .Where(value => value.Id == pair.ManifestOneId || value.Id == pair.ManifestTwoId)
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            Guid[] repositoryIds = manifests.Select(value => value.RepositoryId).Distinct().ToArray();
            if (!IsAuthorizedTerminalPair(pair, manifests, repositoryIds, scope) ||
                !await scope.HasCurrentAccessAsync(repositoryIds, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return Result(PreparedPass2StartOutcome.Rejected);
            }

            Pass2Execution? existing = await _dbContext.Pass2Executions.AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.Pass1PairExecutionId == pass1PairExecutionId,
                    cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                PreparedPass2StartResult replay = await ReplayAsync(
                    existing,
                    pair,
                    cancellationToken).ConfigureAwait(false);
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return replay;
            }

            DateTimeOffset startedAt = pair.TerminalAt ?? pair.UpdatedAt;
            Guid executionId = DeriveId("pass2-execution/1.0", pair.Id);
            Pass2Execution execution = Pass2Execution.CreatePlanning(
                executionId,
                pair.Id,
                pair.CoordinationId,
                pair.SystemId,
                startedAt);
            _dbContext.Pass2Executions.Add(execution);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            PreparedPass2FlowPlanningResult plan = await new PreparedPass2FlowPlanner(_dbContext)
                .PlanAsync(execution.Id, scope, cancellationToken).ConfigureAwait(false);
            FlowGroup[] groups = plan.FlowGroups.Take(_maxFlowGroups).ToArray();
            Pass2Omission[] boundOmissions = plan.FlowGroups.Skip(_maxFlowGroups)
                .Select(group => Pass2Omission.CreateBoundExcluded(
                    DeriveId(
                        "pass2-omission/1.0",
                        execution.Id,
                        group.ExactResolutionDecisionId ?? throw new InvalidOperationException(
                            "A bound-excluded flow candidate must retain its exact decision identity.")),
                    execution.Id,
                    group.ExactResolutionDecisionId!.Value,
                    startedAt))
                .ToArray();
            Pass2Omission[] omissions = plan.Omissions.Concat(boundOmissions).ToArray();
            _dbContext.FlowGroups.AddRange(groups);
            _dbContext.Pass2Omissions.AddRange(omissions);

            var jobs = new List<Pass2JobExecution>();
            var budgets = new List<Pass2RetrievalBudget>();
            foreach (FlowGroup group in groups.Where(value => value.State == "Complete"))
            {
                Guid jobId = DeriveId("pass2-job/1.0", execution.Id, group.Id);
                Pass2JobExecution job = Pass2JobExecution.CreatePending(
                    jobId,
                    execution.Id,
                    group.Id,
                    group.GroupOrder);
                jobs.Add(job);
                budgets.Add(Pass2RetrievalBudget.Create(job.Id, startedAt));
            }

            _dbContext.Pass2JobExecutions.AddRange(jobs);
            _dbContext.Pass2RetrievalBudgets.AddRange(budgets);
            execution.MarkRunning(startedAt);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return new(
                PreparedPass2StartOutcome.Created,
                execution.Id,
                groups.Length,
                omissions.Length,
                jobs.Count,
                budgets.Count,
                false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _dbContext.ChangeTracker.Clear();
            return Result(PreparedPass2StartOutcome.Cancelled);
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            return Result(PreparedPass2StartOutcome.Conflict);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            _dbContext.ChangeTracker.Clear();
            return Result(PreparedPass2StartOutcome.Failed);
        }
    }

    private static bool IsAuthorizedTerminalPair(
        Pass1PairExecution pair,
        IReadOnlyCollection<PreparedScanManifest> manifests,
        IReadOnlyCollection<Guid> repositoryIds,
        RepositoryAccessScope scope) =>
        pair.Phase == Pass1PairExecutionPhase.ReadyForResolution &&
        pair.TerminalAt.HasValue &&
        manifests.Count == 2 &&
        repositoryIds.Count == 2 &&
        manifests.All(value =>
            value.ApprovalState == PreparedScanManifestApprovalState.Approved &&
            value.CoordinationId == pair.CoordinationId &&
            (value.Id == pair.ManifestOneId || value.Id == pair.ManifestTwoId) &&
            scope.Contains(value.RepositoryId));

    private async Task<PreparedPass2StartResult> ReplayAsync(
        Pass2Execution execution,
        Pass1PairExecution pair,
        CancellationToken cancellationToken)
    {
        if (execution.Id != DeriveId("pass2-execution/1.0", pair.Id) ||
            execution.CoordinationId != pair.CoordinationId ||
            execution.SystemId != pair.SystemId ||
            execution.State != Pass2ExecutionState.Running)
        {
            return Result(PreparedPass2StartOutcome.Conflict, execution.Id);
        }

        FlowGroup[] groups = await _dbContext.FlowGroups.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == execution.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Pass2Omission[] omissions = await _dbContext.Pass2Omissions.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == execution.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Pass2JobExecution[] jobs = await _dbContext.Pass2JobExecutions.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == execution.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] jobIds = jobs.Select(value => value.Id).ToArray();
        int budgets = await _dbContext.Pass2RetrievalBudgets.AsNoTracking()
            .CountAsync(value => jobIds.Contains(value.Pass2JobExecutionId), cancellationToken)
            .ConfigureAwait(false);
        CrossRepositoryResolutionDecision[] decisions = await _dbContext
            .CrossRepositoryResolutionDecisions.AsNoTracking()
            .Where(value =>
                value.Pass1PairExecutionId == pair.Id &&
                value.CoordinationId == pair.CoordinationId &&
                value.SystemId == pair.SystemId &&
                value.Outcome == "ExactMatch")
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] orderedDecisionIds = decisions
            .OrderBy(value => value.Protocol)
            .ThenBy(value => value.NormalizedEndpointIdentifier, StringComparer.Ordinal)
            .ThenBy(value => value.ConsumerEntryStableKey, StringComparer.Ordinal)
            .ThenBy(value => value.Id.ToString("N"), StringComparer.Ordinal)
            .Select(value => value.Id)
            .ToArray();
        Guid[] expectedGroupDecisionIds = orderedDecisionIds.Take(_maxFlowGroups).ToArray();
        Guid[] actualGroupDecisionIds = groups.OrderBy(value => value.GroupOrder)
            .Select(value => value.ExactResolutionDecisionId ?? Guid.Empty)
            .ToArray();
        Guid[] expectedOmissionDecisionIds = orderedDecisionIds.Skip(_maxFlowGroups).ToArray();
        Guid[] actualOmissionDecisionIds = omissions
            .OrderBy(value => Array.IndexOf(expectedOmissionDecisionIds, value.ExactResolutionDecisionId))
            .Select(value => value.ExactResolutionDecisionId)
            .ToArray();
        int completeGroups = groups.Count(value => value.State == "Complete");
        if (groups.Length == 0 ||
            !actualGroupDecisionIds.SequenceEqual(expectedGroupDecisionIds) ||
            !actualOmissionDecisionIds.SequenceEqual(expectedOmissionDecisionIds) ||
            omissions.Any(value =>
                value.State != Pass2OmissionState.Omitted ||
                value.ReasonCode != "flow-group-bound" ||
                value.Id != DeriveId(
                    "pass2-omission/1.0",
                    execution.Id,
                    value.ExactResolutionDecisionId)) ||
            jobs.Length != completeGroups || budgets != jobs.Length ||
            jobs.Any(job => groups.All(group => group.Id != job.FlowGroupId)))
        {
            return Result(PreparedPass2StartOutcome.Conflict, execution.Id);
        }

        return new(
            PreparedPass2StartOutcome.Replay,
            execution.Id,
            groups.Length,
            omissions.Length,
            jobs.Length,
            budgets,
            true);
    }

    private static Guid DeriveId(string schema, params Guid[] identities)
    {
        string canonical = schema + "\n" + string.Join('\n', identities.Select(value => value.ToString("D")));
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    private IQueryable<Pass2RetrievalBudget> LockedBudgetQuery(Guid pass2JobExecutionId)
    {
        if (_dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
        {
            return _dbContext.Pass2RetrievalBudgets.FromSqlInterpolated(
                $"SELECT * FROM vulgata.\"Pass2RetrievalBudgets\" WHERE \"Pass2JobExecutionId\" = {pass2JobExecutionId} FOR UPDATE");
        }

        return _dbContext.Pass2RetrievalBudgets
            .Where(value => value.Pass2JobExecutionId == pass2JobExecutionId);
    }

    private static Pass2RetrievalReceiptState ReceiptState(
        Pass2CodeLogicRetrievalOutcome outcome) => outcome switch
        {
            Pass2CodeLogicRetrievalOutcome.Accepted => Pass2RetrievalReceiptState.Accepted,
            Pass2CodeLogicRetrievalOutcome.NoResults => Pass2RetrievalReceiptState.NoResults,
            Pass2CodeLogicRetrievalOutcome.BoundExceeded => Pass2RetrievalReceiptState.BoundExceeded,
            Pass2CodeLogicRetrievalOutcome.Refused => Pass2RetrievalReceiptState.Refused,
            _ => Pass2RetrievalReceiptState.Unavailable,
        };

    private static Pass2CodeLogicRetrievalRemainingBudget RemainingBudget(
        Pass2RetrievalBudget budget) => new(
            Math.Max(0, Pass2RetrievalContracts.MaxCalls - budget.CallsReserved),
            Math.Max(0, Pass2RetrievalContracts.MaxResults - budget.ResultsReturned),
            Math.Max(
                0,
                Pass2RetrievalContracts.MaxAdditionalDocuments - budget.AdditionalDocumentCount),
            Pass2RetrievalContracts.MaxSectionsPerDocument,
            Math.Max(
                0,
                Pass2RetrievalContracts.MaxReturnedUtf8Bytes - budget.ReturnedUtf8Bytes));

    private static Pass2CodeLogicRetrievalRemainingBudget EmptyRemainingBudget() =>
        new(0, 0, 0, 0, 0);

    private static string FingerprintRequest(Pass2CodeLogicRetrievalRequest request) =>
        LowerSha256(string.Join(
            '\n',
            "pass2-retrieval-request/1.0",
            request.Operation.ToString(),
            request.QueryText ?? string.Empty,
            string.Join('\n', request.OpaqueHandles),
            request.Direction?.ToString() ?? string.Empty,
            request.ContainedByteOffset?.ToString(
                System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            request.ContainedByteLength?.ToString(
                System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));

    private static string FingerprintResult(Pass2CodeLogicRetrievalItem item) =>
        LowerSha256(string.Join(
            '\n',
            "pass2-retrieval-result/1.0",
            item.OpaqueHandle,
            item.Kind.ToString(),
            item.DocumentVersionId.ToString("D"),
            item.SectionId?.ToString("D") ?? string.Empty,
            item.SourceAnchorId?.ToString("D") ?? string.Empty,
            item.EdgeId?.ToString("D") ?? string.Empty,
            item.ByteStart?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            item.ByteEnd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            item.ReturnedUtf8Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static string FingerprintDescriptor(Pass2CodeLogicRetrievalItem item) =>
        LowerSha256(string.Join(
            '\n',
            "pass2-retrieval-descriptor/1.0",
            item.Kind.ToString(),
            item.DocumentVersionId.ToString("D"),
            item.SectionId?.ToString("D") ?? string.Empty,
            item.SourceAnchorId?.ToString("D") ?? string.Empty,
            item.EdgeId?.ToString("D") ?? string.Empty,
            item.ByteStart?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            item.ByteEnd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));

    private static string ResultSetFingerprint(
        IReadOnlyCollection<Pass2CodeLogicRetrievalItem> items) =>
        LowerSha256(string.Join(
            '\n',
            new[] { "pass2-retrieval-result-set/1.0" }
                .Concat(items.Select(FingerprintResult))));

    internal static bool MatchesCompletedReplay(
        Pass2RetrievalReceipt receipt,
        Pass2CodeLogicRetrievalResult candidate) =>
        receipt.State == Pass2RetrievalReceiptState.Accepted &&
        candidate.Outcome == Pass2CodeLogicRetrievalOutcome.Accepted &&
        candidate.Items.Count == receipt.ResultCount &&
        candidate.Items.Sum(value => value.ReturnedUtf8Bytes) == receipt.ReturnedUtf8Bytes &&
        StringComparer.Ordinal.Equals(
            ResultSetFingerprint(candidate.Items),
            receipt.ResultSetFingerprint);

    private static bool PersistedResultMatches(
        Pass2RetrievalResult persisted,
        Pass2CodeLogicRetrievalItem item,
        DocumentVersion document,
        Guid pass2JobExecutionId,
        int attemptOrdinal) =>
        persisted.Pass2JobExecutionId == pass2JobExecutionId &&
        persisted.AttemptOrdinal == attemptOrdinal &&
        persisted.State == Pass2RetrievalResultState.Admitted &&
        persisted.Kind == item.Kind &&
        persisted.DocumentVersionId == item.DocumentVersionId &&
        persisted.RepositoryId == document.RepositoryId &&
        persisted.ScanRunId == document.ScanRunId &&
        persisted.ReadSectionId() == item.SectionId &&
        persisted.ReadSourceAnchorId() == item.SourceAnchorId &&
        persisted.EdgeId == item.EdgeId &&
        persisted.ByteStart == item.ByteStart &&
        persisted.ByteEnd == item.ByteEnd &&
        StringComparer.Ordinal.Equals(
            persisted.DescriptorFingerprint,
            FingerprintDescriptor(item)) &&
        StringComparer.Ordinal.Equals(
            persisted.EvidenceFingerprint,
            FingerprintResult(item)) &&
        persisted.ReturnedUtf8Bytes == item.ReturnedUtf8Bytes;

    private static Pass2CodeLogicRetrievalResult Rehydrate(
        Pass2RetrievalReceipt receipt,
        Pass2RetrievalBudget budget)
    {
        Pass2CodeLogicRetrievalRemainingBudget remaining = RemainingBudget(budget);
        if (receipt.State == Pass2RetrievalReceiptState.NoResults)
        {
            return Pass2CodeLogicRetrievalResult.NoResults(remaining);
        }

        if (receipt.State == Pass2RetrievalReceiptState.BoundExceeded)
        {
            return Pass2CodeLogicRetrievalResult.BoundExceeded(remaining);
        }

        if (receipt.State == Pass2RetrievalReceiptState.Refused)
        {
            return Pass2CodeLogicRetrievalResult.Refused().WithRemainingBudget(remaining);
        }

        if (receipt.State == Pass2RetrievalReceiptState.Unavailable)
        {
            return Pass2CodeLogicRetrievalResult.Unavailable().WithRemainingBudget(remaining);
        }

        return Pass2CodeLogicRetrievalResult.Accepted(
            Array.Empty<Pass2CodeLogicRetrievalItem>(),
            remaining);
    }

    private static string LowerSha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool IsSerializationFailure(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } ||
        exception.InnerException is not null && IsSerializationFailure(exception.InnerException);

    private static Guid GuidFromOrdinal(int ordinal)
    {
        Span<byte> bytes = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes[12..], ordinal);
        return new Guid(bytes, bigEndian: true);
    }

    private static PreparedPass2StartResult Result(
        PreparedPass2StartOutcome outcome,
        Guid? executionId = null) =>
        new(outcome, executionId, 0, 0, 0, 0, false);
}
