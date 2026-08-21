using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class Pass1NavigationBudgetConfiguration : IEntityTypeConfiguration<Pass1NavigationBudget>
{
    public void Configure(EntityTypeBuilder<Pass1NavigationBudget> builder)
    {
        builder.ToTable("Pass1NavigationBudgets", table => table.HasCheckConstraint("CK_Pass1NavigationBudgets_Bounds", "\"ToolAttemptCount\" BETWEEN 0 AND 8 AND \"AcceptedCallCount\" BETWEEN 0 AND \"ToolAttemptCount\" AND \"EvidenceBytes\" BETWEEN 0 AND 65536 AND \"MaximumDepthObserved\" BETWEEN 0 AND 3 AND \"StateVersion\" >= 0"));
        builder.HasKey(value => value.ScanJobExecutionId);
        builder.Property(value => value.ScanJobExecutionId).ValueGeneratedNever();
        builder.Property(value => value.StateVersion).IsConcurrencyToken();
        builder.Property(value => value.UpdatedAt).HasColumnType("timestamp with time zone");
        builder.HasOne<ScanJobExecution>().WithOne().HasForeignKey<Pass1NavigationBudget>(value => value.ScanJobExecutionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Pass1PairExecution>().WithMany().HasForeignKey(value => value.PairExecutionId).OnDelete(DeleteBehavior.Restrict);
    }
}
