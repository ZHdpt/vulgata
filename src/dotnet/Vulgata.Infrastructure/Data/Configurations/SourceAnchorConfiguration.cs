using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class SourceAnchorConfiguration : IEntityTypeConfiguration<SourceAnchor>
{
    public void Configure(EntityTypeBuilder<SourceAnchor> builder)
    {
        builder.ToTable("SourceAnchors", table =>
        {
            table.HasCheckConstraint("CK_SourceAnchors_LineStart_Positive", "\"LineStart\" > 0");
            table.HasCheckConstraint("CK_SourceAnchors_LineEnd_Range", "\"LineEnd\" >= \"LineStart\"");
        });
        builder.HasKey(anchor => anchor.Id);
        builder.Property(anchor => anchor.Id).ValueGeneratedNever();
        builder.Property(anchor => anchor.ProfiledRevision).HasMaxLength(64);
        builder.Property(anchor => anchor.RepositoryRelativePath).HasMaxLength(2_048);
        builder.Property(anchor => anchor.EvidenceLocator).HasMaxLength(4_096);
        builder.Property(anchor => anchor.SourceHash).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(anchor => new
        {
            anchor.RepositoryId,
            anchor.DocumentVersionId,
            anchor.CodeUnitId,
        });
        builder.HasOne(anchor => anchor.DocumentVersion)
            .WithMany(version => version.SourceAnchors)
            .HasForeignKey(anchor => new
            {
                anchor.DocumentVersionId,
                anchor.RepositoryId,
                anchor.ScanRunId,
            })
            .HasPrincipalKey(version => new
            {
                version.Id,
                version.RepositoryId,
                version.ScanRunId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(anchor => anchor.CodeUnit)
            .WithMany()
            .HasForeignKey(anchor => new
            {
                anchor.CodeUnitId,
                anchor.ScanRunId,
                anchor.RepositoryId,
            })
            .HasPrincipalKey(unit => new { unit.Id, unit.ScanRunId, unit.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(anchor => anchor.ScanRun)
            .WithMany()
            .HasForeignKey(anchor => new { anchor.ScanRunId, anchor.RepositoryId })
            .HasPrincipalKey(scanRun => new { scanRun.Id, scanRun.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
