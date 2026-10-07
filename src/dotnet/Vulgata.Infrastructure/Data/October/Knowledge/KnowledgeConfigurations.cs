using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

public sealed class KnowledgeOperationConfiguration : IEntityTypeConfiguration<KnowledgeOperation>
{
    public void Configure(EntityTypeBuilder<KnowledgeOperation> b)
    {
        b.ToTable("KnowledgeOperations");
        b.HasKey(x => new { x.BatchId, x.Kind, x.OperationId });
        b.Property(x => x.PayloadDigest).IsRequired().HasMaxLength(64);
        b.Property(x => x.CommittedResultJson).IsRequired().HasColumnType("jsonb");
        b.Property(x => x.ContributorsJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<OctoberBatchRecord>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class AcceptedFileManifestConfiguration : IEntityTypeConfiguration<AcceptedFileManifest>
{
    public void Configure(EntityTypeBuilder<AcceptedFileManifest> b)
    {
        b.ToTable("AcceptedFileManifests");
        b.HasKey(x => new { x.BatchId, x.ManifestId });
        b.HasIndex(x => new { x.BatchId, x.AdmissionId }).IsUnique();
        b.HasOne<OctoberRepositoryRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.RepositoryId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<KnowledgeOperation>().WithMany().HasForeignKey(x => new { x.BatchId, x.OperationKind, x.OperationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CodeUnitConfiguration : IEntityTypeConfiguration<CodeUnit>
{
    public void Configure(EntityTypeBuilder<CodeUnit> b)
    {
        b.ToTable("OctoberCodeUnits");
        b.HasKey(x => new { x.BatchId, x.UnitId });
        b.HasAlternateKey(x => new { x.BatchId, x.ManifestId, x.UnitId });
        b.HasIndex(x => new { x.BatchId, x.SemanticKey }).IsUnique();
        b.Property(x => x.SemanticKey).HasMaxLength(64);
        b.HasOne<AcceptedFileManifest>().WithMany().HasForeignKey(x => new { x.BatchId, x.ManifestId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OctoberRepositoryRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.RepositoryId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CodeUnit>().WithMany().HasForeignKey(x => new { x.BatchId, x.ManifestId, x.ParentUnitId })
            .HasPrincipalKey(x => new { x.BatchId, x.ManifestId, x.UnitId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class LogicalDocumentConfiguration : IEntityTypeConfiguration<LogicalDocument>
{
    public void Configure(EntityTypeBuilder<LogicalDocument> b)
    {
        b.ToTable("OctoberLogicalDocuments", t => t.HasCheckConstraint("CK_LogicalDocument_Owner",
            "(\"Kind\" = 0 AND \"UnitId\" IS NOT NULL AND \"LogicalFlowId\" IS NULL) OR (\"Kind\" = 1 AND \"UnitId\" IS NULL AND \"LogicalFlowId\" IS NOT NULL)"));
        b.HasKey(x => new { x.BatchId, x.LogicalDocumentId });
        b.HasAlternateKey(x => new { x.BatchId, x.LogicalDocumentId, x.Kind });
        b.HasIndex(x => new { x.BatchId, x.UnitId }).IsUnique();
        b.HasIndex(x => new { x.BatchId, x.LogicalFlowId }).IsUnique();
        b.HasOne<CodeUnit>().WithMany().HasForeignKey(x => new { x.BatchId, x.UnitId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LogicalFlowRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.LogicalFlowId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> b)
    {
        b.ToTable("OctoberDocumentVersions", t => t.HasCheckConstraint("CK_DocumentVersion_Owner",
            "(\"Kind\" = 0 AND \"ManifestId\" IS NOT NULL AND \"FlowGroupVersionId\" IS NULL AND \"FinalizationOperationKind\" IS NULL AND \"FinalizationOperationId\" IS NULL) OR (\"Kind\" = 1 AND \"ManifestId\" IS NULL AND \"FlowGroupVersionId\" IS NOT NULL AND \"FinalizationOperationKind\" IS NOT NULL AND \"FinalizationOperationKind\" = 3 AND \"FinalizationOperationId\" IS NOT NULL)"));
        b.HasKey(x => new { x.BatchId, x.VersionId });
        b.HasAlternateKey(x => new { x.BatchId, x.LogicalDocumentId, x.VersionId });
        // Preserve the foundation index explicitly: the publication identity key now
        // covers this prefix and otherwise suppresses EF's convention-created index.
        b.HasIndex(x => new { x.BatchId, x.ManifestId });
        b.Property(x => x.ContentJson).IsRequired().HasColumnType("jsonb");
        b.Property(x => x.AnchorsJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<LogicalDocument>().WithMany().HasForeignKey(x => new { x.BatchId, x.LogicalDocumentId, x.Kind })
            .HasPrincipalKey(x => new { x.BatchId, x.LogicalDocumentId, x.Kind }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcceptedFileManifest>().WithMany().HasForeignKey(x => new { x.BatchId, x.ManifestId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FlowGroupVersionRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.FlowGroupVersionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<BusinessFinalizationRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.FinalizationOperationKind, x.FinalizationOperationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class DocumentSectionConfiguration : IEntityTypeConfiguration<DocumentSectionRecord>
{
    public void Configure(EntityTypeBuilder<DocumentSectionRecord> b)
    {
        b.ToTable("OctoberDocumentSections");
        b.HasKey(x => new { x.BatchId, x.VersionId, x.SectionId });
        b.HasIndex(x => new { x.BatchId, x.VersionId, x.Ordinal }).IsUnique();
        b.HasOne<DocumentVersion>().WithMany().HasForeignKey(x => new { x.BatchId, x.VersionId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class QualifiedClaimGroupConfiguration : IEntityTypeConfiguration<QualifiedClaimGroupRecord>
{
    public void Configure(EntityTypeBuilder<QualifiedClaimGroupRecord> b)
    {
        b.ToTable("OctoberQualifiedClaimGroups");
        b.HasKey(x => new { x.BatchId, x.VersionId, x.SectionId, x.ClaimGroupId });
        b.Property(x => x.EvidenceJson).IsRequired().HasColumnType("jsonb");
        b.Property(x => x.LimitationsJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<DocumentSectionRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.VersionId, x.SectionId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class KnowledgeContributorConfiguration : IEntityTypeConfiguration<KnowledgeContributorRecord>
{
    public void Configure(EntityTypeBuilder<KnowledgeContributorRecord> b)
    {
        b.ToTable("KnowledgeContributors");
        b.HasKey(x => new { x.BatchId, x.ManifestId, x.Ordinal });
        b.Property(x => x.ContributorJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<AcceptedFileManifest>().WithMany().HasForeignKey(x => new { x.BatchId, x.ManifestId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CommunicationObservationConfiguration : IEntityTypeConfiguration<CommunicationObservationRecord>
{
    public void Configure(EntityTypeBuilder<CommunicationObservationRecord> b)
    {
        b.ToTable("OctoberCommunicationObservations");
        b.HasKey(x => new { x.BatchId, x.ManifestId, x.ObservationKey });
        b.Property(x => x.ContentJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<CodeUnit>().WithMany().HasForeignKey(x => new { x.BatchId, x.ManifestId, x.UnitId })
            .HasPrincipalKey(x => new { x.BatchId, x.ManifestId, x.UnitId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class KnowledgeUncertaintyConfiguration : IEntityTypeConfiguration<KnowledgeUncertaintyRecord>
{
    public void Configure(EntityTypeBuilder<KnowledgeUncertaintyRecord> b)
    {
        b.ToTable("OctoberKnowledgeUncertainties");
        b.HasKey(x => new { x.BatchId, x.ManifestId, x.Key });
        b.Property(x => x.ContentJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<AcceptedFileManifest>().WithMany().HasForeignKey(x => new { x.BatchId, x.ManifestId }).OnDelete(DeleteBehavior.Restrict);
    }
}
