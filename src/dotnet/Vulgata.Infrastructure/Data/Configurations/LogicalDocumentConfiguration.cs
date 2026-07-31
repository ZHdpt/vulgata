using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class LogicalDocumentConfiguration : IEntityTypeConfiguration<LogicalDocument>
{
    public void Configure(EntityTypeBuilder<LogicalDocument> builder)
    {
        builder.ToTable("LogicalDocuments", table =>
            table.HasCheckConstraint(
                "CK_LogicalDocuments_DocumentType_Closed",
                "\"DocumentType\" IN ('CodeLogic', 'BusinessLogic')"));
        builder.HasKey(document => document.Id);
        builder.HasAlternateKey(document => new { document.Id, document.RepositoryId });
        builder.Property(document => document.Id).ValueGeneratedNever();
        builder.Property(document => document.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(document => document.StableKey).HasMaxLength(4_096);
        builder.Property(document => document.CurrentVersionId).IsConcurrencyToken();
        builder.HasIndex(document => new
        {
            document.RepositoryId,
            document.DocumentType,
            document.StableKey,
        }).IsUnique();
        builder.HasIndex(document => new { document.RepositoryId, document.CurrentVersionId });
        builder.HasOne(document => document.Repository)
            .WithMany()
            .HasForeignKey(document => document.RepositoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(document => document.CurrentVersion)
            .WithOne()
            .HasForeignKey<LogicalDocument>(document => new
            {
                document.CurrentVersionId,
                document.Id,
                document.RepositoryId,
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
