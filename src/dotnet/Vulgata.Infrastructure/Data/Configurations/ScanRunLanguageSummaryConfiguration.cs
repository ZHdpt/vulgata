using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class ScanRunLanguageSummaryConfiguration : IEntityTypeConfiguration<ScanRunLanguageSummary>
{
    public void Configure(EntityTypeBuilder<ScanRunLanguageSummary> builder)
    {
        builder.ToTable(
            "ScanRunLanguageSummaries",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ScanRunLanguageSummaries_Language",
                    "length(\"Language\") BETWEEN 1 AND 64");
                table.HasCheckConstraint(
                    "CK_ScanRunLanguageSummaries_DistinctFileCount",
                    "\"DistinctFileCount\" > 0");
            });
        builder.HasKey(summary => new { summary.ScanRunId, summary.Language });
        builder.Property(summary => summary.Language).HasMaxLength(64);
        builder.HasOne(summary => summary.ScanRun)
            .WithMany(scanRun => scanRun.LanguageSummaries)
            .HasForeignKey(summary => new { summary.ScanRunId, summary.RepositoryId })
            .HasPrincipalKey(scanRun => new { scanRun.Id, scanRun.RepositoryId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
