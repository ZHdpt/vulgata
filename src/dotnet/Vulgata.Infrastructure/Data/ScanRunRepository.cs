using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class ScanRunRepository(VulgataDbContext dbContext) : IScanRunRepository
{
    public Task<ScanRun?> GetAsync(
        Guid repositoryId,
        Guid scanRunId,
        CancellationToken cancellationToken = default) =>
        dbContext.ScanRuns
            .AsNoTracking()
            .Include(scanRun => scanRun.CodeUnits)
            .Include(scanRun => scanRun.LanguageSummaries)
            .Include(scanRun => scanRun.CodeUnitFrameworks)
            .Include(scanRun => scanRun.CandidateOutcomes)
            .SingleOrDefaultAsync(
                scanRun => scanRun.RepositoryId == repositoryId && scanRun.Id == scanRunId,
                cancellationToken);

    public async Task<ApprovedPreScanHandoff?> GetApprovedPreScanHandoffAsync(
        Guid repositoryId,
        Guid scanRunId,
        CancellationToken cancellationToken = default)
    {
        PreScanResult? result = await dbContext.PreScanResults
            .AsNoTracking()
            .Include(item => item.Classifications)
            .SingleOrDefaultAsync(
                item =>
                    item.RepositoryId == repositoryId &&
                    item.ScanRunId == scanRunId,
                cancellationToken);
        return result is null
            ? null
            : ApprovedPreScanHandoff.Create(
                result.Id,
                result.RepositoryId,
                result.ScanRunId,
                result.ProfiledRevision,
                result.State,
                result.SchemaVersion,
                result.ApprovedAt,
                result.ApprovalMessage,
                result.ConfirmedCommunicationKinds,
                result.TotalConfirmedEvidenceCount,
                result.TotalHintCount,
                result.TotalWarningCount,
                result.EvidenceTruncated,
                result.Classifications);
    }

    public async Task AddAsync(ScanRun scanRun, CancellationToken cancellationToken = default) =>
        await dbContext.ScanRuns.AddAsync(scanRun, cancellationToken);

    public void AddCandidateOutcomes(IEnumerable<ScanCandidateOutcome> outcomes) =>
        dbContext.ScanCandidateOutcomes.AddRange(outcomes);

    public void AddStructuralRecords(
        IEnumerable<CodeUnit> codeUnits,
        IEnumerable<CodeUnitEdge> edges,
        IEnumerable<UnresolvedStructuralRelationshipCandidate> unresolvedRelationships)
    {
        dbContext.CodeUnits.AddRange(codeUnits);
        dbContext.CodeUnitEdges.AddRange(edges);
        dbContext.UnresolvedStructuralRelationshipCandidates.AddRange(unresolvedRelationships);
    }

    public async Task<PreScanResult> PersistCompletedProfileAsync(
        RepositoryProfileCommitDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        RepositoryProfileDraft profile = draft.Profile;
        PreScanResult result = draft.PreScanResult.Result;
        EnsureCompleteLanguageProfile(profile);
        EnsureCompositeIdentity(profile, result);

        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                PreScanResult? existing = await dbContext.PreScanResults
                    .AsNoTracking()
                    .Include(persisted => persisted.Classifications)
                    .SingleOrDefaultAsync(
                        persisted => persisted.ScanRunId == profile.ScanRun.Id,
                        cancellationToken);
                if (existing is not null)
                {
                    EnsureReplayMatches(profile, result, existing);
                    await transaction.CommitAsync(cancellationToken);
                    return existing;
                }

                ScanRun durableRun = await dbContext.ScanRuns
                    .AsNoTracking()
                    .SingleAsync(
                        persisted =>
                            persisted.RepositoryId == profile.ScanRun.RepositoryId &&
                            persisted.Id == profile.ScanRun.Id,
                        cancellationToken);
                if (durableRun.Status != ScanRunStatus.Profiling)
                {
                    throw new InvalidOperationException(
                        "A new Pre-Scan Result requires one durable Profiling ScanRun.");
                }

                if (dbContext.Entry(profile.ScanRun).State == EntityState.Detached)
                {
                    dbContext.Attach(profile.ScanRun);
                    dbContext.Entry(profile.ScanRun).State = EntityState.Modified;
                }

                dbContext.CodeUnits.AddRange(profile.CodeUnits);
                dbContext.CodeUnitEdges.AddRange(profile.CodeUnitEdges);
                dbContext.UnresolvedStructuralRelationshipCandidates.AddRange(
                    profile.UnresolvedRelationships);
                dbContext.CodeUnitFrameworks.AddRange(profile.CodeUnitFrameworks);
                dbContext.ScanRunLanguageSummaries.AddRange(profile.LanguageSummaries);
                dbContext.ScanCandidateOutcomes.AddRange(profile.CandidateOutcomes);
                await dbContext.PreScanResults.AddAsync(result, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                throw;
            }
        });
    }

    public async Task RecoverFailedFinalCommitAsync(
        Guid repositoryId,
        Guid scanRunId,
        string sanitizedMessage,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken = default)
    {
        await RecoverFinalCommitAsync(
            repositoryId,
            scanRunId,
            failedAt,
            abort: false,
            cancellationToken);
    }

    public async Task RecoverAbortedFinalCommitAsync(
        Guid repositoryId,
        Guid scanRunId,
        string sanitizedMessage,
        DateTimeOffset abortedAt,
        CancellationToken cancellationToken = default)
    {
        await RecoverFinalCommitAsync(
            repositoryId,
            scanRunId,
            abortedAt,
            abort: true,
            cancellationToken);
    }

    private async Task RecoverFinalCommitAsync(
        Guid repositoryId,
        Guid scanRunId,
        DateTimeOffset terminalAt,
        bool abort,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        ScanRun durableRun = await dbContext.ScanRuns.SingleAsync(
            scanRun => scanRun.RepositoryId == repositoryId && scanRun.Id == scanRunId,
            cancellationToken);
        if (durableRun.Status != ScanRunStatus.Profiling)
        {
            throw new InvalidOperationException("Only a durable Profiling ScanRun can be recovered.");
        }

        if (abort)
        {
            durableRun.Abort("代码结构分析已取消。", terminalAt);
        }
        else
        {
            durableRun.Fail("代码结构分析失败，请稍后重试。", terminalAt);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private static void EnsureCompleteLanguageProfile(RepositoryProfileDraft draft)
    {
        if (draft.CodeUnits.Any(unit => !CodeUnit.IsNormalizedLanguage(unit.Language)) ||
            draft.CodeUnits
                .GroupBy(unit => unit.FilePath, StringComparer.Ordinal)
                .Any(group => group
                    .Select(unit => unit.Language)
                    .Distinct(StringComparer.Ordinal)
                    .Skip(1)
                    .Any()))
        {
            throw new InvalidOperationException(
                "A completed Repository Profile Draft requires one normalized language per retained Code Unit path.");
        }
    }

    private static void EnsureCompositeIdentity(
        RepositoryProfileDraft profile,
        PreScanResult result)
    {
        if (profile.ScanRun.Status != ScanRunStatus.Completed ||
            profile.ScanRun.ProfiledRevision is null ||
            profile.ScanRun.RepositoryId != result.RepositoryId ||
            profile.ScanRun.Id != result.ScanRunId ||
            result.Id != result.ScanRunId ||
            !StringComparer.Ordinal.Equals(
                profile.ScanRun.ProfiledRevision,
                result.ProfiledRevision))
        {
            throw new InvalidOperationException(
                "Atomic profile approval requires one completed ScanRun and exact Profiled Revision.");
        }
    }

    private static void EnsureReplayMatches(
        RepositoryProfileDraft profile,
        PreScanResult requested,
        PreScanResult existing)
    {
        if (existing.RepositoryId != profile.ScanRun.RepositoryId ||
            existing.ScanRunId != profile.ScanRun.Id ||
            !StringComparer.Ordinal.Equals(
                existing.ProfiledRevision,
                profile.ScanRun.ProfiledRevision))
        {
            throw new InvalidOperationException(
                "The persisted Pre-Scan Result does not match the requested ScanRun revision.");
        }

        if (!StringComparer.Ordinal.Equals(existing.Fingerprint, requested.Fingerprint))
        {
            throw new InvalidOperationException(
                "A different Pre-Scan Result fingerprint requires a new ScanRun.");
        }
    }
}
