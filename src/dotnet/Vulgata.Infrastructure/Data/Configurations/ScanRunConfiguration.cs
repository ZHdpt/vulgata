using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class ScanRunConfiguration : IEntityTypeConfiguration<ScanRun>
{
    public void Configure(EntityTypeBuilder<ScanRun> builder)
    {
        builder.ToTable("ScanRuns");
        builder.HasKey(scanRun => scanRun.Id);
        builder.HasAlternateKey(scanRun => new { scanRun.Id, scanRun.RepositoryId });
        builder.Property(scanRun => scanRun.Id).ValueGeneratedNever();
        builder.Property(scanRun => scanRun.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(scanRun => scanRun.TerminalMessage).HasMaxLength(512);
        builder.HasIndex(scanRun => new { scanRun.RepositoryId, scanRun.StartedAt });
        builder.HasOne(scanRun => scanRun.Repository)
            .WithMany()
            .HasForeignKey(scanRun => scanRun.RepositoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
