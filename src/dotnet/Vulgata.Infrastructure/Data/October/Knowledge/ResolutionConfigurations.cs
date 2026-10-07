using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

public sealed class ResolutionItemConfiguration : IEntityTypeConfiguration<ResolutionItem>
{
    public void Configure(EntityTypeBuilder<ResolutionItem> b)
    {
        b.ToTable("OctoberResolutionItems");
        b.HasKey(x => new { x.BatchId, x.ItemId });
        b.HasAlternateKey(x => new { x.BatchId, x.CorpusId, x.ItemId }).HasName("AK_ResolutionItem_Corpus");
        b.HasIndex(x => new { x.BatchId, x.CorpusId, x.InquiryFingerprint }).IsUnique().HasDatabaseName("UX_ResolutionItem_Fingerprint");
        b.HasIndex(x => new { x.BatchId, x.OperationKind, x.OperationId }).IsUnique().HasDatabaseName("UX_ResolutionItem_Operation");
        b.Property(x => x.InquiryFingerprint).IsRequired().HasMaxLength(64);
        b.Property(x => x.PolicyVersion).IsRequired();
        b.Property(x => x.WitnessJson).HasColumnType("jsonb");
        b.Property(x => x.OutcomeJson).IsRequired().HasColumnType("jsonb");
        b.Property(x => x.ContributorsJson).IsRequired().HasColumnType("jsonb");
        b.Property(x => x.PlanDispositionsJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<CorpusSelectionRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ResolutionItem_Corpus");
        b.HasOne<KnowledgeOperation>().WithMany().HasForeignKey(x => new { x.BatchId, x.OperationKind, x.OperationId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ResolutionItem_Operation");
    }
}

public sealed class ResolutionDocumentDependencyConfiguration : IEntityTypeConfiguration<ResolutionDocumentDependencyRecord>
{
    public void Configure(EntityTypeBuilder<ResolutionDocumentDependencyRecord> b)
    {
        b.ToTable("OctoberResolutionDocumentDependencies");
        b.HasKey(x => new { x.BatchId, x.ItemId, x.LogicalDocumentId, x.VersionId });
        b.Property(x => x.IsInquiryCaller).HasDefaultValue(false);
        b.HasIndex(x => new { x.BatchId, x.ItemId }).IsUnique().HasDatabaseName("UX_ResolutionDependency_InquiryCaller")
            .HasFilter("\"IsInquiryCaller\" = TRUE");
        b.HasIndex(x => new { x.BatchId, x.CorpusId, x.ItemId }).HasDatabaseName("IX_ResolutionDependency_Item");
        b.HasIndex(x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId, x.VersionId }).HasDatabaseName("IX_ResolutionDependency_Document");
        b.HasOne<ResolutionItem>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.ItemId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.ItemId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ResolutionDependency_Item");
        b.HasOne<CorpusDocumentRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId, x.VersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId, x.VersionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ResolutionDependency_Document");
    }
}

public sealed class RelationshipVersionConfiguration : IEntityTypeConfiguration<RelationshipVersion>
{
    public void Configure(EntityTypeBuilder<RelationshipVersion> b)
    {
        b.ToTable("OctoberRelationshipVersions");
        b.HasKey(x => new { x.BatchId, x.RelationshipVersionId });
        b.HasAlternateKey(x => new { x.BatchId, x.CorpusId, x.RelationshipVersionId }).HasName("AK_RelationshipVersion_Corpus");
        b.HasAlternateKey(x => new { x.BatchId, x.CorpusId, x.RelationshipVersionId, x.FromLogicalDocumentId, x.FromVersionId, x.ToLogicalDocumentId, x.ToVersionId }).HasName("AK_RelationshipVersion_Endpoints");
        b.HasIndex(x => new { x.BatchId, x.CorpusId, x.ItemId }).IsUnique().HasDatabaseName("UX_RelationshipVersion_Item");
        b.HasIndex(x => new { x.BatchId, x.CorpusId, x.FromLogicalDocumentId, x.FromVersionId }).HasDatabaseName("IX_RelationshipVersion_From");
        b.HasIndex(x => new { x.BatchId, x.CorpusId, x.ToLogicalDocumentId, x.ToVersionId }).HasDatabaseName("IX_RelationshipVersion_To");
        b.Property(x => x.ContentJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<ResolutionItem>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.ItemId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.ItemId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_RelationshipVersion_Item");
        b.HasOne<CorpusDocumentRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.FromLogicalDocumentId, x.FromVersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId, x.VersionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_RelationshipVersion_From");
        b.HasOne<CorpusDocumentRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.ToLogicalDocumentId, x.ToVersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId, x.VersionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_RelationshipVersion_To");
    }
}

public sealed class UncertaintyVersionConfiguration : IEntityTypeConfiguration<UncertaintyVersion>
{
    public void Configure(EntityTypeBuilder<UncertaintyVersion> b)
    {
        b.ToTable("OctoberUncertaintyVersions");
        b.HasKey(x => new { x.BatchId, x.UncertaintyId });
        b.HasAlternateKey(x => new { x.BatchId, x.CorpusId, x.UncertaintyId }).HasName("AK_UncertaintyVersion_Corpus");
        b.HasIndex(x => new { x.BatchId, x.CorpusId, x.Key }).IsUnique().HasDatabaseName("UX_UncertaintyVersion_Key");
        b.HasIndex(x => new { x.BatchId, x.CorpusId, x.ItemId }).IsUnique().HasDatabaseName("UX_UncertaintyVersion_Item");
        b.Property(x => x.Key).IsRequired();
        b.Property(x => x.ContentJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<ResolutionItem>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.ItemId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.ItemId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_UncertaintyVersion_Item");
    }
}

public sealed class PublicationRelationshipConfiguration : IEntityTypeConfiguration<PublicationRelationshipRecord>
{
    public void Configure(EntityTypeBuilder<PublicationRelationshipRecord> b)
    {
        b.ToTable("OctoberPublicationRelationships");
        b.HasKey(x => new { x.BatchId, x.PublicationId, x.RelationshipVersionId });
        b.HasIndex(x => new { x.BatchId, x.PublicationId, x.CorpusId }).HasDatabaseName("IX_PublicationRelationship_Publication");
        b.HasIndex(x => new { x.BatchId, x.CorpusId, x.RelationshipVersionId, x.FromLogicalDocumentId, x.FromVersionId, x.ToLogicalDocumentId, x.ToVersionId }).HasDatabaseName("IX_PublicationRelationship_Evidence");
        b.HasIndex(x => new { x.BatchId, x.PublicationId, x.FromLogicalDocumentId, x.FromVersionId }).HasDatabaseName("IX_PublicationRelationship_From");
        b.HasIndex(x => new { x.BatchId, x.PublicationId, x.ToLogicalDocumentId, x.ToVersionId }).HasDatabaseName("IX_PublicationRelationship_To");
        b.HasOne<KnowledgePublicationRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.PublicationId, x.CorpusId })
            .HasPrincipalKey(x => new { x.BatchId, x.PublicationId, x.CorpusId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PublicationRelationship_Publication");
        b.HasOne<RelationshipVersion>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.RelationshipVersionId, x.FromLogicalDocumentId, x.FromVersionId, x.ToLogicalDocumentId, x.ToVersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.RelationshipVersionId, x.FromLogicalDocumentId, x.FromVersionId, x.ToLogicalDocumentId, x.ToVersionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PublicationRelationship_Evidence");
        b.HasOne<PublicationMemberRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.PublicationId, x.FromLogicalDocumentId, x.FromVersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.PublicationId, x.LogicalDocumentId, x.VersionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PublicationRelationship_From");
        b.HasOne<PublicationMemberRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.PublicationId, x.ToLogicalDocumentId, x.ToVersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.PublicationId, x.LogicalDocumentId, x.VersionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PublicationRelationship_To");
    }
}

public sealed class PublicationResolutionUncertaintyConfiguration : IEntityTypeConfiguration<PublicationResolutionUncertaintyRecord>
{
    public void Configure(EntityTypeBuilder<PublicationResolutionUncertaintyRecord> b)
    {
        b.ToTable("OctoberPublicationResolutionUncertainties");
        b.HasKey(x => new { x.BatchId, x.PublicationId, x.UncertaintyId });
        b.HasIndex(x => new { x.BatchId, x.PublicationId, x.CorpusId }).HasDatabaseName("IX_PublicationResolutionUnknown_Publication");
        b.HasIndex(x => new { x.BatchId, x.CorpusId, x.UncertaintyId }).HasDatabaseName("IX_PublicationResolutionUnknown_Evidence");
        b.HasOne<KnowledgePublicationRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.PublicationId, x.CorpusId })
            .HasPrincipalKey(x => new { x.BatchId, x.PublicationId, x.CorpusId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PublicationResolutionUnknown_Publication");
        b.HasOne<UncertaintyVersion>().WithMany().HasForeignKey(x => new { x.BatchId, x.CorpusId, x.UncertaintyId })
            .HasPrincipalKey(x => new { x.BatchId, x.CorpusId, x.UncertaintyId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PublicationResolutionUnknown_Evidence");
    }
}
