using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

public sealed class BusinessFinalizationConfiguration : IEntityTypeConfiguration<BusinessFinalizationRecord>
{
    public void Configure(EntityTypeBuilder<BusinessFinalizationRecord> b)
    {
        b.ToTable("OctoberBusinessFinalizations");
        b.HasKey(x => new { x.BatchId, x.OperationKind, x.OperationId });
        b.Property(x => x.RequestDigest).HasMaxLength(64);
        b.Property(x => x.OutcomeDigest).HasMaxLength(64);
        b.Property(x => x.RequestJson).HasColumnType("jsonb");
        b.Property(x => x.ResultJson).HasColumnType("jsonb");
        b.Property(x => x.ContributorsJson).HasColumnType("jsonb");
        b.HasOne<KnowledgeOperation>().WithMany().HasForeignKey(x => new { x.BatchId, x.OperationKind, x.OperationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CorpusSelectionRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<KnowledgePublicationRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.PublicationId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LogicalFlowConfiguration : IEntityTypeConfiguration<LogicalFlowRecord>
{
    public void Configure(EntityTypeBuilder<LogicalFlowRecord> b)
    {
        b.ToTable("OctoberLogicalFlows");
        b.HasKey(x => new { x.BatchId, x.LogicalFlowId });
        b.HasIndex(x => new { x.BatchId, x.RootUnitId, x.RootKind, x.RootSelector }).IsUnique();
        b.HasOne<CodeUnit>().WithMany().HasForeignKey(x => new { x.BatchId, x.RootUnitId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OctoberRepositoryRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.HomeRepositoryId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<KnowledgeOperation>().WithMany().HasForeignKey(x => new { x.BatchId, x.OperationKind, x.OperationId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FlowGroupVersionConfiguration : IEntityTypeConfiguration<FlowGroupVersionRecord>
{
    public void Configure(EntityTypeBuilder<FlowGroupVersionRecord> b)
    {
        b.ToTable("OctoberFlowGroupVersions");
        b.HasKey(x => new { x.BatchId, x.GroupVersionId });
        b.HasAlternateKey(x => new { x.BatchId, x.CorpusId, x.GroupVersionId });
        b.Property(x => x.GroupJson).HasColumnType("jsonb");
        b.Property(x => x.ContributorsJson).HasColumnType("jsonb");
        b.HasOne<LogicalFlowRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.LogicalFlowId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CorpusDocumentRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.RootLogicalDocumentId, x.RootVersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId, x.VersionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<KnowledgeOperation>().WithMany().HasForeignKey(x => new { x.BatchId, x.OperationKind, x.OperationId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FlowDocumentDependencyConfiguration : IEntityTypeConfiguration<FlowDocumentDependencyRecord>
{
    public void Configure(EntityTypeBuilder<FlowDocumentDependencyRecord> b)
    {
        b.ToTable("OctoberFlowDocumentDependencies");
        b.HasKey(x => new { x.BatchId, x.GroupVersionId, x.LogicalDocumentId, x.VersionId });
        b.HasOne<FlowGroupVersionRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.GroupVersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.GroupVersionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CorpusDocumentRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId, x.VersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId, x.VersionId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FlowRelationshipDependencyConfiguration : IEntityTypeConfiguration<FlowRelationshipDependencyRecord>
{
    public void Configure(EntityTypeBuilder<FlowRelationshipDependencyRecord> b)
    {
        b.ToTable("OctoberFlowRelationshipDependencies");
        b.HasKey(x => new { x.BatchId, x.GroupVersionId, x.RelationshipVersionId });
        b.HasOne<FlowGroupVersionRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.GroupVersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.GroupVersionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RelationshipVersion>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.RelationshipVersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.RelationshipVersionId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FlowResolutionUncertaintyDependencyConfiguration : IEntityTypeConfiguration<FlowResolutionUncertaintyDependencyRecord>
{
    public void Configure(EntityTypeBuilder<FlowResolutionUncertaintyDependencyRecord> b)
    {
        b.ToTable("OctoberFlowResolutionUncertaintyDependencies");
        b.HasKey(x => new { x.BatchId, x.GroupVersionId, x.UncertaintyId });
        b.HasOne<FlowGroupVersionRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.GroupVersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.GroupVersionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UncertaintyVersion>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.UncertaintyId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.UncertaintyId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FlowFileUncertaintyDependencyConfiguration : IEntityTypeConfiguration<FlowFileUncertaintyDependencyRecord>
{
    public void Configure(EntityTypeBuilder<FlowFileUncertaintyDependencyRecord> b)
    {
        b.ToTable("OctoberFlowFileUncertaintyDependencies");
        b.HasKey(x => new { x.BatchId, x.GroupVersionId, x.ManifestId, x.Key });
        b.HasOne<FlowGroupVersionRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.GroupVersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.GroupVersionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CorpusManifestRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.ManifestId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<KnowledgeUncertaintyRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.ManifestId, x.Key }).OnDelete(DeleteBehavior.Restrict);
    }
}
