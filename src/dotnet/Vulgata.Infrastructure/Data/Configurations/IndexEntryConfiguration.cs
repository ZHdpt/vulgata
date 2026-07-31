using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class IndexEntryConfiguration : IEntityTypeConfiguration<IndexEntry>
{
    public void Configure(EntityTypeBuilder<IndexEntry> builder)
    {
        builder.ToTable("IndexEntries", table =>
            table.HasCheckConstraint(
                "CK_IndexEntries_DocumentType_Closed",
                "\"DocumentType\" IN ('CodeLogic', 'BusinessLogic')"));
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.DocumentType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entry => entry.Title).HasMaxLength(4_096);
        builder.Property(entry => entry.Summary).HasMaxLength(4_096);
        builder.Property(entry => entry.RepositoryRelativePath).HasMaxLength(2_048);
        builder.Property(entry => entry.KeySymbolsJson).HasColumnType("jsonb");
        builder.Property(entry => entry.BusinessTermsJson).HasColumnType("jsonb");
        builder.Property(entry => entry.TechnicalTermsJson).HasColumnType("jsonb");
        builder.Property(entry => entry.SearchText).HasMaxLength(4_194_304);
        builder.HasIndex(entry => entry.DocumentVersionId).IsUnique();
        builder.HasIndex(entry => new { entry.RepositoryId, entry.DocumentType, entry.Title });
        builder.HasOne(entry => entry.DocumentVersion)
            .WithOne(version => version.IndexEntry)
            .HasForeignKey<IndexEntry>(entry => new
            {
                entry.DocumentVersionId,
                entry.LogicalDocumentId,
                entry.RepositoryId,
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
