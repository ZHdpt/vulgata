using Microsoft.EntityFrameworkCore;
using SystemEntity = Vulgata.Core.Entities.System;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using Vulgata.Core.Entities;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Data;

public class VulgataDbContext(DbContextOptions<VulgataDbContext> options) :
    DbContext(options), IPreparedPass2ExecutionQueryService
{
    public Task<Pass1PairExecution?> FindPairAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        new PreparedPass2ExecutionQueryService(this).FindPairAsync(id, cancellationToken);

    public Task<TwoRepositoryScanCoordination?> FindCoordinationAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        new PreparedPass2ExecutionQueryService(this).FindCoordinationAsync(id, cancellationToken);

    public Task<Pass2Execution?> FindExecutionAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        new PreparedPass2ExecutionQueryService(this).FindExecutionAsync(id, cancellationToken);

    public Task<PreparedPass2StatusAuthority> ReadStatusAsync(
        Guid executionId, CancellationToken cancellationToken = default) =>
        new PreparedPass2ExecutionQueryService(this).ReadStatusAsync(
            executionId, cancellationToken);

    public DbSet<SystemEntity> Systems => Set<SystemEntity>();
    public DbSet<RepositoryEntity> Repositories => Set<RepositoryEntity>();
    public DbSet<SourceSnapshot> SourceSnapshots => Set<SourceSnapshot>();
    public DbSet<DatabaseConnection> DatabaseConnections => Set<DatabaseConnection>();
    public DbSet<LlmProvider> LlmProviders => Set<LlmProvider>();
    public DbSet<SystemLlmProviderOverride> SystemLlmProviderOverrides => Set<SystemLlmProviderOverride>();
    public DbSet<SystemOwnerAssignment> SystemOwnerAssignments => Set<SystemOwnerAssignment>();
    public DbSet<GlobalContext> GlobalContexts => Set<GlobalContext>();
    public DbSet<PendingContextChange> PendingContextChanges => Set<PendingContextChange>();
    public DbSet<ScanRun> ScanRuns => Set<ScanRun>();
    public DbSet<ScanCandidateOutcome> ScanCandidateOutcomes => Set<ScanCandidateOutcome>();
    public DbSet<CodeUnit> CodeUnits => Set<CodeUnit>();
    public DbSet<CodeUnitEdge> CodeUnitEdges => Set<CodeUnitEdge>();
    public DbSet<UnresolvedStructuralRelationshipCandidate> UnresolvedStructuralRelationshipCandidates =>
        Set<UnresolvedStructuralRelationshipCandidate>();
    public DbSet<CodeUnitFramework> CodeUnitFrameworks => Set<CodeUnitFramework>();
    public DbSet<ScanRunLanguageSummary> ScanRunLanguageSummaries => Set<ScanRunLanguageSummary>();
    public DbSet<PreScanResult> PreScanResults => Set<PreScanResult>();
    public DbSet<CommunicationKindClassification> CommunicationKindClassifications =>
        Set<CommunicationKindClassification>();
    public DbSet<PreparedScanManifest> PreparedScanManifests => Set<PreparedScanManifest>();
    public DbSet<PreparedScanManifestEntry> PreparedScanManifestEntries =>
        Set<PreparedScanManifestEntry>();
    public DbSet<PreparedScanAudit> PreparedScanAudits => Set<PreparedScanAudit>();
    public DbSet<LogicalDocument> LogicalDocuments => Set<LogicalDocument>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<SourceAnchor> SourceAnchors => Set<SourceAnchor>();
    public DbSet<DocumentEdge> DocumentEdges => Set<DocumentEdge>();
    public DbSet<ExactCrossRepositoryEdgeEvidence> ExactCrossRepositoryEdgeEvidence =>
        Set<ExactCrossRepositoryEdgeEvidence>();
    public DbSet<CrossRepositoryResolutionDecision> CrossRepositoryResolutionDecisions =>
        Set<CrossRepositoryResolutionDecision>();
    public DbSet<ExactResolutionProviderEvidenceContribution> ExactResolutionProviderEvidenceContributions =>
        Set<ExactResolutionProviderEvidenceContribution>();
    public DbSet<Uncertainty> Uncertainties => Set<Uncertainty>();
    public DbSet<FlowGroup> FlowGroups => Set<FlowGroup>();
    public DbSet<Pass2Execution> Pass2Executions => Set<Pass2Execution>();
    public DbSet<Pass2Omission> Pass2Omissions => Set<Pass2Omission>();
    public DbSet<Pass2JobExecution> Pass2JobExecutions => Set<Pass2JobExecution>();
    public DbSet<Pass2RetrievalBudget> Pass2RetrievalBudgets => Set<Pass2RetrievalBudget>();
    public DbSet<Pass2RetrievalReceipt> Pass2RetrievalReceipts => Set<Pass2RetrievalReceipt>();
    public DbSet<Pass2RetrievalResult> Pass2RetrievalResults => Set<Pass2RetrievalResult>();
    public DbSet<FlowTrace> FlowTraces => Set<FlowTrace>();
    public DbSet<FlowTraceStep> FlowTraceSteps => Set<FlowTraceStep>();
    public DbSet<IndexEntry> IndexEntries => Set<IndexEntry>();
    public DbSet<DocumentSection> DocumentSections => Set<DocumentSection>();
    public DbSet<ScanExecution> ScanExecutions => Set<ScanExecution>();
    public DbSet<ScanContextSnapshot> ScanContextSnapshots => Set<ScanContextSnapshot>();
    public DbSet<ScanJobExecution> ScanJobExecutions => Set<ScanJobExecution>();
    public DbSet<TwoRepositoryScanCoordination> TwoRepositoryScanCoordinations =>
        Set<TwoRepositoryScanCoordination>();
    public DbSet<Pass1PairExecution> Pass1PairExecutions => Set<Pass1PairExecution>();
    public DbSet<Pass1ConcurrencyChange> Pass1ConcurrencyChanges => Set<Pass1ConcurrencyChange>();
    public DbSet<Pass1NavigationBudget> Pass1NavigationBudgets => Set<Pass1NavigationBudget>();
    public DbSet<Pass1NavigationReceipt> Pass1NavigationReceipts => Set<Pass1NavigationReceipt>();
    public DbSet<Pass1Outcome> Pass1Outcomes => Set<Pass1Outcome>();
    public DbSet<Pass1FailureInventoryEntry> Pass1FailureInventoryEntries => Set<Pass1FailureInventoryEntry>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateDurableScanTemporalBindings();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        await ValidateDurableScanTemporalBindingsAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("vulgata");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VulgataDbContext).Assembly);
    }

    private void ValidateDurableScanTemporalBindings()
    {
        RejectSnapshotMutationOrDeletion();
        foreach (ScanContextSnapshot snapshot in AddedSnapshots())
        {
            ScanRun scanRun = FindTrackedScanRun(snapshot.ScanRunId, snapshot.RepositoryId) ??
                ScanRuns.AsNoTracking().SingleOrDefault(value =>
                    value.Id == snapshot.ScanRunId && value.RepositoryId == snapshot.RepositoryId) ??
                throw new InvalidOperationException("The bound Scan Run does not exist.");
            ValidateSnapshotAgainstScanRun(snapshot, scanRun);

            ScanExecution? execution = FindTrackedNewPathExecution(
                snapshot.ScanRunId,
                snapshot.RepositoryId) ??
                ScanExecutions.AsNoTracking().FirstOrDefault(value =>
                    value.ScanRunId == snapshot.ScanRunId &&
                    value.RepositoryId == snapshot.RepositoryId &&
                    value.PromptIdentity != null);
            ValidateSnapshotAgainstExecution(snapshot, execution);
        }

        foreach (ScanExecution execution in AddedNewPathExecutions())
        {
            ScanContextSnapshot? snapshot = FindTrackedSnapshot(
                execution.ScanRunId,
                execution.RepositoryId) ??
                ScanContextSnapshots.AsNoTracking().SingleOrDefault(value =>
                    value.ScanRunId == execution.ScanRunId &&
                    value.RepositoryId == execution.RepositoryId);
            ValidateSnapshotAgainstExecution(snapshot, execution);
        }
    }

    private async Task ValidateDurableScanTemporalBindingsAsync(
        CancellationToken cancellationToken)
    {
        RejectSnapshotMutationOrDeletion();
        foreach (ScanContextSnapshot snapshot in AddedSnapshots())
        {
            ScanRun scanRun = FindTrackedScanRun(snapshot.ScanRunId, snapshot.RepositoryId) ??
                await ScanRuns.AsNoTracking().SingleOrDefaultAsync(
                    value => value.Id == snapshot.ScanRunId &&
                        value.RepositoryId == snapshot.RepositoryId,
                    cancellationToken) ??
                throw new InvalidOperationException("The bound Scan Run does not exist.");
            ValidateSnapshotAgainstScanRun(snapshot, scanRun);

            ScanExecution? execution = FindTrackedNewPathExecution(
                snapshot.ScanRunId,
                snapshot.RepositoryId) ??
                await ScanExecutions.AsNoTracking().FirstOrDefaultAsync(
                    value => value.ScanRunId == snapshot.ScanRunId &&
                        value.RepositoryId == snapshot.RepositoryId &&
                        value.PromptIdentity != null,
                    cancellationToken);
            ValidateSnapshotAgainstExecution(snapshot, execution);
        }

        foreach (ScanExecution execution in AddedNewPathExecutions())
        {
            ScanContextSnapshot? snapshot = FindTrackedSnapshot(
                execution.ScanRunId,
                execution.RepositoryId) ??
                await ScanContextSnapshots.AsNoTracking().SingleOrDefaultAsync(
                    value => value.ScanRunId == execution.ScanRunId &&
                        value.RepositoryId == execution.RepositoryId,
                    cancellationToken);
            ValidateSnapshotAgainstExecution(snapshot, execution);
        }
    }

    private IEnumerable<ScanContextSnapshot> AddedSnapshots() =>
        ChangeTracker.Entries<ScanContextSnapshot>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity);

    private void RejectSnapshotMutationOrDeletion()
    {
        if (ChangeTracker.Entries<ScanContextSnapshot>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "A Scan Context Snapshot is immutable and insert-only.");
        }
    }

    private IEnumerable<ScanExecution> AddedNewPathExecutions() =>
        ChangeTracker.Entries<ScanExecution>()
            .Where(entry => entry.State == EntityState.Added &&
                entry.Entity.PromptIdentity is not null)
            .Select(entry => entry.Entity);

    private ScanRun? FindTrackedScanRun(Guid scanRunId, Guid repositoryId) =>
        ChangeTracker.Entries<ScanRun>()
            .Where(entry => entry.State != EntityState.Deleted)
            .Select(entry => entry.Entity)
            .SingleOrDefault(value =>
                value.Id == scanRunId && value.RepositoryId == repositoryId);

    private ScanContextSnapshot? FindTrackedSnapshot(Guid scanRunId, Guid repositoryId) =>
        ChangeTracker.Entries<ScanContextSnapshot>()
            .Where(entry => entry.State != EntityState.Deleted)
            .Select(entry => entry.Entity)
            .SingleOrDefault(value =>
                value.ScanRunId == scanRunId && value.RepositoryId == repositoryId);

    private ScanExecution? FindTrackedNewPathExecution(Guid scanRunId, Guid repositoryId) =>
        ChangeTracker.Entries<ScanExecution>()
            .Where(entry => entry.State != EntityState.Deleted)
            .Select(entry => entry.Entity)
            .FirstOrDefault(value =>
                value.ScanRunId == scanRunId &&
                value.RepositoryId == repositoryId &&
                value.PromptIdentity is not null);

    private static void ValidateSnapshotAgainstScanRun(
        ScanContextSnapshot snapshot,
        ScanRun scanRun)
    {
        if (snapshot.CapturedAt < scanRun.StartedAt ||
            !string.Equals(
                snapshot.ProfiledRevision,
                scanRun.ProfiledRevision,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Scan Context Snapshot does not match the bound Scan Run temporal and revision identity.");
        }
    }

    private static void ValidateSnapshotAgainstExecution(
        ScanContextSnapshot? snapshot,
        ScanExecution? execution)
    {
        if (snapshot is null || execution is null)
        {
            return;
        }

        if (snapshot.CapturedAt > execution.StartedAt ||
            !string.Equals(
                snapshot.ProfiledRevision,
                execution.ProfiledRevision,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Scan Context Snapshot must be captured before its new-path Scan Execution starts.");
        }
    }
}
