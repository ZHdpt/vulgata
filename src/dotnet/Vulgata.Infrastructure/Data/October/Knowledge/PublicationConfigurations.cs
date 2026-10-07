using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

public sealed class CorpusSelectionConfiguration : IEntityTypeConfiguration<CorpusSelectionRecord>
{
    public void Configure(EntityTypeBuilder<CorpusSelectionRecord> b)
    {
        b.ToTable("OctoberCorpusSelections");
        b.HasKey(x => new { x.BatchId, x.CorpusId });
        b.HasIndex(x => x.BatchId).IsUnique();
        b.Property(x => x.CorpusDigest).IsRequired().HasMaxLength(64);
        b.Property(x => x.ContributorsJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<KnowledgeOperation>().WithMany().HasForeignKey(x => new { x.BatchId, x.OperationKind, x.OperationId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CorpusManifestConfiguration : IEntityTypeConfiguration<CorpusManifestRecord>
{
    public void Configure(EntityTypeBuilder<CorpusManifestRecord> b)
    {
        b.ToTable("OctoberCorpusManifests");
        b.HasKey(x => new { x.BatchId, x.CorpusId, x.ManifestId });
        b.HasOne<CorpusSelectionRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcceptedFileManifest>().WithMany().HasForeignKey(x => new { x.BatchId, x.ManifestId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CorpusDocumentConfiguration : IEntityTypeConfiguration<CorpusDocumentRecord>
{
    public void Configure(EntityTypeBuilder<CorpusDocumentRecord> b)
    {
        b.ToTable("OctoberCorpusDocuments");
        b.HasKey(x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId });
        b.HasAlternateKey(x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId, x.VersionId });
        b.HasIndex(x => new { x.BatchId, x.CorpusId, x.VersionId }).IsUnique();
        b.HasIndex(x => new { x.BatchId, x.CorpusId, x.Ordinal }).IsUnique();
        b.HasOne<CorpusManifestRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.ManifestId }).OnDelete(DeleteBehavior.Restrict);
        // PostgreSQL additionally retains the original native manifest-qualified FK/unique
        // constraint. It must not become an EF alternate key that forces nullable BL owners required.
        b.HasOne<DocumentVersion>().WithMany().HasForeignKey(x => new { x.BatchId, x.LogicalDocumentId, x.VersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.LogicalDocumentId, x.VersionId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CorpusCoverageConfiguration : IEntityTypeConfiguration<CorpusCoverageRecord>
{
    public void Configure(EntityTypeBuilder<CorpusCoverageRecord> b)
    {
        b.ToTable("OctoberCorpusCoverage");
        b.HasKey(x => new { x.BatchId, x.CorpusId, x.AdmissionId });
        b.Property(x => x.CoverageJson).IsRequired().HasColumnType("jsonb");
        b.Property(x => x.ContributorsJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<CorpusSelectionRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OctoberRepositoryRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.RepositoryId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class KnowledgePublicationConfiguration : IEntityTypeConfiguration<KnowledgePublicationRecord>
{
    public void Configure(EntityTypeBuilder<KnowledgePublicationRecord> b)
    {
        b.ToTable("OctoberKnowledgePublications");
        b.HasKey(x => new { x.BatchId, x.PublicationId });
        b.HasAlternateKey(x => new { x.BatchId, x.PublicationId, x.CorpusId });
        b.Property(x => x.CorpusDigest).IsRequired().HasMaxLength(64);
        b.Property(x => x.ContributorsJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<CorpusSelectionRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<KnowledgeOperation>().WithMany().HasForeignKey(x => new { x.BatchId, x.OperationKind, x.OperationId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PublicationMemberConfiguration : IEntityTypeConfiguration<PublicationMemberRecord>
{
    public void Configure(EntityTypeBuilder<PublicationMemberRecord> b)
    {
        b.ToTable("OctoberPublicationMembers");
        b.HasKey(x => new { x.BatchId, x.PublicationId, x.LogicalDocumentId });
        b.HasAlternateKey(x => new { x.BatchId, x.PublicationId, x.LogicalDocumentId, x.VersionId }).HasName("AK_PublicationMember_ExactVersion");
        b.HasIndex(x => new { x.BatchId, x.PublicationId, x.VersionId }).IsUnique();
        b.HasIndex(x => new { x.BatchId, x.PublicationId, x.Ordinal }).IsUnique();
        b.HasOne<KnowledgePublicationRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.PublicationId, x.CorpusId })
            .HasPrincipalKey(x => new { x.BatchId, x.PublicationId, x.CorpusId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DocumentVersion>().WithMany().HasForeignKey(x => new { x.BatchId, x.LogicalDocumentId, x.VersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.LogicalDocumentId, x.VersionId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PublicationCoverageConfiguration : IEntityTypeConfiguration<PublicationCoverageRecord>
{
    public void Configure(EntityTypeBuilder<PublicationCoverageRecord> b)
    {
        b.ToTable("OctoberPublicationCoverage");
        b.HasKey(x => new { x.BatchId, x.PublicationId, x.AdmissionId });
        b.Property(x => x.CoverageJson).IsRequired().HasColumnType("jsonb");
        b.Property(x => x.ContributorsJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<KnowledgePublicationRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.PublicationId, x.CorpusId })
            .HasPrincipalKey(x => new { x.BatchId, x.PublicationId, x.CorpusId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CorpusCoverageRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.AdmissionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OctoberRepositoryRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.RepositoryId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PublicationUncertaintyConfiguration : IEntityTypeConfiguration<PublicationUncertaintyRecord>
{
    public void Configure(EntityTypeBuilder<PublicationUncertaintyRecord> b)
    {
        b.ToTable("OctoberPublicationUncertainties");
        b.HasKey(x => new { x.BatchId, x.PublicationId, x.ManifestId, x.Key });
        b.HasOne<KnowledgePublicationRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.PublicationId, x.CorpusId })
            .HasPrincipalKey(x => new { x.BatchId, x.PublicationId, x.CorpusId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CorpusManifestRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.ManifestId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<KnowledgeUncertaintyRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.ManifestId, x.Key }).OnDelete(DeleteBehavior.Restrict);
    }
}
