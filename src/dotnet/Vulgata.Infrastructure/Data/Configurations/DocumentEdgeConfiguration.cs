using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class DocumentEdgeConfiguration : IEntityTypeConfiguration<DocumentEdge>
{
    public void Configure(EntityTypeBuilder<DocumentEdge> builder)
    {
        builder.ToTable("DocumentEdges", table =>
            table.HasCheckConstraint(
                "CK_DocumentEdges_EdgeType_Closed",
                "\"EdgeType\" IN ('References', 'GeneratedFrom')"));
        builder.HasKey(edge => edge.Id);
        builder.Property(edge => edge.Id).ValueGeneratedNever();
        builder.Property(edge => edge.EdgeType).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(edge => new
        {
            edge.SourceDocumentVersionId,
            edge.TargetDocumentVersionId,
            edge.EdgeType,
        }).IsUnique();
        builder.HasIndex(edge => new { edge.SourceRepositoryId, edge.SourceDocumentVersionId });
        builder.HasIndex(edge => new { edge.TargetRepositoryId, edge.TargetDocumentVersionId });
        builder.HasOne(edge => edge.SourceDocumentVersion)
            .WithMany()
            .HasForeignKey(edge => new
            {
                edge.SourceDocumentVersionId,
                edge.SourceRepositoryId,
                edge.SourceScanRunId,
            })
            .HasPrincipalKey(version => new
            {
                version.Id,
                version.RepositoryId,
                version.ScanRunId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(edge => edge.TargetDocumentVersion)
            .WithMany()
            .HasForeignKey(edge => new
            {
                edge.TargetDocumentVersionId,
                edge.TargetRepositoryId,
                edge.TargetScanRunId,
            })
            .HasPrincipalKey(version => new
            {
                version.Id,
                version.RepositoryId,
                version.ScanRunId,
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
