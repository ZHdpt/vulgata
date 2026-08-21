using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("DocumentVersions", table =>
        {
            table.HasCheckConstraint("CK_DocumentVersions_VersionNumber_Positive", "\"VersionNumber\" > 0");
            table.HasCheckConstraint(
                "CK_DocumentVersions_SupersededBy_NotSelf",
                "\"SupersededById\" IS NULL OR \"SupersededById\" <> \"Id\"");
            table.HasCheckConstraint(
                "CK_DocumentVersions_ContentFingerprint_Length",
                "length(\"ContentFingerprint\") = 64");
            table.HasCheckConstraint(
                "CK_DocumentVersions_CommitFingerprint_Length",
                "length(\"CommitFingerprint\") = 64");
            table.HasCheckConstraint(
                "CK_DocumentVersions_SchemaIdentity_Closed",
                "\"SchemaIdentity\" IN ('vulgata.knowledge-document/1.0', 'code-logic/file-v2')");
        });
        builder.HasKey(version => version.Id);
        builder.HasAlternateKey(version => new
        {
            version.Id,
            version.LogicalDocumentId,
            version.RepositoryId,
        });
        builder.HasAlternateKey(version => new
        {
            version.Id,
            version.RepositoryId,
            version.ScanRunId,
        });
        builder.Property(version => version.Id).ValueGeneratedNever();
        builder.Property(version => version.ProfiledRevision).HasMaxLength(64);
        builder.Property(version => version.Title).HasMaxLength(4_096);
        builder.Property(version => version.RepositoryRelativePath).HasMaxLength(2_048);
        builder.Property(version => version.SchemaIdentity).HasMaxLength(128);
        builder.Property(version => version.ContentJson).HasColumnType("jsonb");
        builder.Property(version => version.ContentMarkdown).HasMaxLength(1_048_576);
        builder.Property(version => version.ContentFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(version => version.CommitFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(version => version.SupersededById).IsConcurrencyToken();
        builder.HasIndex(version => new { version.LogicalDocumentId, version.VersionNumber })
            .IsUnique();
        builder.HasIndex(version => version.SupersededById)
            .IsUnique();
        builder.HasIndex(version => new { version.RepositoryId, version.ScanRunId });
        builder.HasOne(version => version.LogicalDocument)
            .WithMany(document => document.Versions)
            .HasForeignKey(version => new { version.LogicalDocumentId, version.RepositoryId })
            .HasPrincipalKey(document => new { document.Id, document.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(version => version.ScanRun)
            .WithMany()
            .HasForeignKey(version => new { version.ScanRunId, version.RepositoryId })
            .HasPrincipalKey(scanRun => new { scanRun.Id, scanRun.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(version => version.SupersededBy)
            .WithOne()
            .HasForeignKey<DocumentVersion>(version => new
            {
                version.SupersededById,
                version.LogicalDocumentId,
                version.RepositoryId,
            })
            .HasPrincipalKey<DocumentVersion>(version => new
            {
                version.Id,
                version.LogicalDocumentId,
                version.RepositoryId,
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
