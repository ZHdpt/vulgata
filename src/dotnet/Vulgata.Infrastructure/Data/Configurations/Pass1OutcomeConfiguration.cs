using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class Pass1OutcomeConfiguration : IEntityTypeConfiguration<Pass1Outcome>
{
    public void Configure(EntityTypeBuilder<Pass1Outcome> builder)
    {
        builder.ToTable("Pass1Outcomes", table => table.HasCheckConstraint("CK_Pass1Outcomes_Counts", "\"DispatchedCount\" >= 0 AND \"CompletedCount\" BETWEEN 0 AND \"DispatchedCount\" AND \"OrdinaryFailureCount\" BETWEEN 0 AND \"DispatchedCount\" AND \"OrdinaryFailureBudget\" BETWEEN 0 AND 5 AND \"HighValueSeedCount\" BETWEEN 0 AND \"DispatchedCount\" AND \"SucceededHighValueSeedCount\" BETWEEN 0 AND \"HighValueSeedCount\" AND \"ReconciledEdgeCount\" >= 0"));
        builder.HasKey(value => value.PairExecutionId);
        builder.Property(value => value.PairExecutionId).ValueGeneratedNever();
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(value => value.ReconciliationFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.OutcomeFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.CompletedAt).HasColumnType("timestamp with time zone");
        builder.HasOne<Pass1PairExecution>().WithOne().HasForeignKey<Pass1Outcome>(value => value.PairExecutionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class Pass1FailureInventoryEntryConfiguration : IEntityTypeConfiguration<Pass1FailureInventoryEntry>
{
    public void Configure(EntityTypeBuilder<Pass1FailureInventoryEntry> builder)
    {
        builder.ToTable("Pass1FailureInventoryEntries", table => table.HasCheckConstraint("CK_Pass1FailureInventoryEntries_Ordinals", "\"PairSelectionOrdinal\" > 0 AND \"ManifestOrdinal\" > 0 AND \"SegmentOrdinal\" > 0"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.RepositoryRelativePath).HasMaxLength(2048);
        builder.Property(value => value.FailureCategory).HasConversion<string>().HasMaxLength(32);
        builder.Property(value => value.FailureCode).HasMaxLength(64);
        builder.HasIndex(value => new { value.PairExecutionId, value.FileCodeUnitId }).IsUnique();
        builder.HasIndex(value => new { value.PairExecutionId, value.PairSelectionOrdinal }).IsUnique();
        builder.HasOne<Pass1Outcome>().WithMany().HasForeignKey(value => value.PairExecutionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CodeUnit>().WithMany().HasForeignKey(value => value.FileCodeUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Repository>().WithMany().HasForeignKey(value => value.RepositoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
