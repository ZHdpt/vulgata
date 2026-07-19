using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class ScanCandidateOutcomeConfiguration : IEntityTypeConfiguration<ScanCandidateOutcome>
{
    public void Configure(EntityTypeBuilder<ScanCandidateOutcome> builder)
    {
        builder.ToTable("ScanCandidateOutcomes");
        builder.HasKey(outcome => outcome.Id);
        builder.Property(outcome => outcome.Id).ValueGeneratedNever();
        builder.Property(outcome => outcome.FilePath).HasMaxLength(2_048);
        builder.Property(outcome => outcome.Outcome).HasConversion<string>().HasMaxLength(32);
        builder.Property(outcome => outcome.ReasonOrCategory).HasMaxLength(512);
        builder.HasIndex(outcome => new
        {
            outcome.ScanRunId,
            outcome.FilePath,
            outcome.Outcome,
            outcome.ReasonOrCategory,
        }).IsUnique();
        builder.HasOne(outcome => outcome.ScanRun)
            .WithMany(scanRun => scanRun.CandidateOutcomes)
            .HasForeignKey(outcome => outcome.ScanRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
