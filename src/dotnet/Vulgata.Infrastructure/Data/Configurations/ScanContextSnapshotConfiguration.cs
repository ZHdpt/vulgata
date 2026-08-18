using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class ScanContextSnapshotConfiguration :
    IEntityTypeConfiguration<ScanContextSnapshot>
{
    public void Configure(EntityTypeBuilder<ScanContextSnapshot> builder)
    {
        builder.ToTable("ScanContextSnapshots", table =>
        {
            table.HasCheckConstraint(
                "CK_ScanContextSnapshots_ContentHash_Length",
                "length(\"ContentHash\") = 64");
            table.HasCheckConstraint(
                "CK_ScanContextSnapshots_SchemaIdentity_Closed",
                "\"SchemaIdentity\" = 'scan-context-snapshot/1.0'");
        });
        builder.HasKey(snapshot => snapshot.Id);
        builder.HasAlternateKey(snapshot => new { snapshot.ScanRunId, snapshot.RepositoryId });
        builder.Property(snapshot => snapshot.Id).ValueGeneratedNever();
        builder.Property(snapshot => snapshot.ProfiledRevision).HasMaxLength(64).IsRequired();
        builder.Property(snapshot => snapshot.GlobalContext).HasMaxLength(10_000);
        builder.Property(snapshot => snapshot.SystemContext).HasMaxLength(10_000);
        builder.Property(snapshot => snapshot.RepositoryContext).HasMaxLength(10_000);
        builder.Property(snapshot => snapshot.SchemaIdentity).HasMaxLength(128).IsRequired();
        builder.Property(snapshot => snapshot.ContentHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(snapshot => snapshot.CapturedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.HasIndex(snapshot => new { snapshot.ScanRunId, snapshot.RepositoryId })
            .IsUnique();
        builder.HasOne<Repository>()
            .WithMany()
            .HasForeignKey(snapshot => snapshot.RepositoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanRun>()
            .WithMany()
            .HasForeignKey(snapshot => new { snapshot.ScanRunId, snapshot.RepositoryId })
            .HasPrincipalKey(scanRun => new { scanRun.Id, scanRun.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
