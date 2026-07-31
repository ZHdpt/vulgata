using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class DocumentSectionConfiguration : IEntityTypeConfiguration<DocumentSection>
{
    public void Configure(EntityTypeBuilder<DocumentSection> builder)
    {
        builder.ToTable("DocumentSections", table =>
        {
            table.HasCheckConstraint("CK_DocumentSections_SectionOrder_Positive", "\"SectionOrder\" > 0");
            table.HasCheckConstraint(
                "CK_DocumentSections_SectionKind_Closed",
                "\"SectionKind\" IN ('Overview', 'BusinessRule', 'ProcessStep', 'TechnicalDetail', 'Evidence', 'Other')");
        });
        builder.HasKey(section => section.Id);
        builder.Property(section => section.Id).ValueGeneratedNever();
        builder.Property(section => section.SectionKind).HasConversion<string>().HasMaxLength(32);
        builder.Property(section => section.Heading).HasMaxLength(4_096);
        builder.Property(section => section.ContentMarkdown).HasMaxLength(65_536);
        builder.Property(section => section.SearchText).HasMaxLength(131_072);
        builder.Property(section => section.SourceAnchorIdsJson).HasColumnType("jsonb");
        builder.Property(section => section.DocumentVersionIdsJson).HasColumnType("jsonb");
        builder.HasIndex(section => new { section.DocumentVersionId, section.StableSectionId })
            .IsUnique();
        builder.HasIndex(section => new { section.DocumentVersionId, section.SectionOrder })
            .IsUnique();
        builder.HasIndex(section => new { section.RepositoryId, section.LogicalDocumentId });
        builder.HasOne(section => section.DocumentVersion)
            .WithMany(version => version.Sections)
            .HasForeignKey(section => new
            {
                section.DocumentVersionId,
                section.LogicalDocumentId,
                section.RepositoryId,
            })
            .HasPrincipalKey(version => new
            {
                version.Id,
                version.LogicalDocumentId,
                version.RepositoryId,
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
