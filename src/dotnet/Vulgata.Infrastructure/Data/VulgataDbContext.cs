using Microsoft.EntityFrameworkCore;
using SystemEntity = Vulgata.Core.Entities.System;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public class VulgataDbContext(DbContextOptions<VulgataDbContext> options) : DbContext(options)
{
    public DbSet<SystemEntity> Systems => Set<SystemEntity>();
    public DbSet<RepositoryEntity> Repositories => Set<RepositoryEntity>();
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
    public DbSet<LogicalDocument> LogicalDocuments => Set<LogicalDocument>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<SourceAnchor> SourceAnchors => Set<SourceAnchor>();
    public DbSet<DocumentEdge> DocumentEdges => Set<DocumentEdge>();
    public DbSet<ExactCrossRepositoryEdgeEvidence> ExactCrossRepositoryEdgeEvidence =>
        Set<ExactCrossRepositoryEdgeEvidence>();
    public DbSet<Uncertainty> Uncertainties => Set<Uncertainty>();
    public DbSet<FlowGroup> FlowGroups => Set<FlowGroup>();
    public DbSet<FlowTrace> FlowTraces => Set<FlowTrace>();
    public DbSet<FlowTraceStep> FlowTraceSteps => Set<FlowTraceStep>();
    public DbSet<IndexEntry> IndexEntries => Set<IndexEntry>();
    public DbSet<DocumentSection> DocumentSections => Set<DocumentSection>();
    public DbSet<ScanExecution> ScanExecutions => Set<ScanExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("vulgata");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VulgataDbContext).Assembly);
    }
}
