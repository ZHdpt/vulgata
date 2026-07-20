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
            .Include(scanRun => scanRun.LanguageSummaries)
            .Include(scanRun => scanRun.CodeUnitFrameworks)
            .Include(scanRun => scanRun.CandidateOutcomes)
            .SingleOrDefaultAsync(
                scanRun => scanRun.RepositoryId == repositoryId && scanRun.Id == scanRunId,
                cancellationToken);

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

    public async Task PersistCompletedProfileAsync(
        RepositoryProfileDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        EnsureCompleteLanguageProfile(draft);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            dbContext.CodeUnits.AddRange(draft.CodeUnits);
            dbContext.CodeUnitEdges.AddRange(draft.CodeUnitEdges);
            dbContext.UnresolvedStructuralRelationshipCandidates.AddRange(draft.UnresolvedRelationships);
            dbContext.CodeUnitFrameworks.AddRange(draft.CodeUnitFrameworks);
            dbContext.ScanRunLanguageSummaries.AddRange(draft.LanguageSummaries);
            dbContext.ScanCandidateOutcomes.AddRange(draft.CandidateOutcomes);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
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
}
