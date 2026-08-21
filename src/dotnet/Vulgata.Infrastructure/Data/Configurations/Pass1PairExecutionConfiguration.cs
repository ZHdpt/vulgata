using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class Pass1PairExecutionConfiguration : IEntityTypeConfiguration<Pass1PairExecution>
{
    public void Configure(EntityTypeBuilder<Pass1PairExecution> builder)
    {
        builder.ToTable("Pass1PairExecutions", table =>
        {
            table.HasCheckConstraint("CK_Pass1PairExecutions_Concurrency", "\"InitialConcurrencyLimit\" BETWEEN 1 AND 4 AND \"ConcurrencyLimit\" BETWEEN 1 AND 4");
            table.HasCheckConstraint("CK_Pass1PairExecutions_Counts", "\"DispatchedFileCount\" BETWEEN 50 AND 150 AND \"HighValueSeedCount\" BETWEEN 0 AND \"DispatchedFileCount\"");
            table.HasCheckConstraint("CK_Pass1PairExecutions_ProbeOrdinal", "\"ProbeOrdinal\" BETWEEN 0 AND 5");
            table.HasCheckConstraint("CK_Pass1PairExecutions_Versions", "\"StateVersion\" >= 0 AND \"ConcurrencyRevision\" >= 0");
        });
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.PairPackageFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.PairSelectionFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.Pass1PairFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.PromptIdentityOne).HasMaxLength(128);
        builder.Property(value => value.PromptIdentityTwo).HasMaxLength(128);
        builder.Property(value => value.DocumentSchemaIdentity).HasMaxLength(128);
        builder.Property(value => value.AnalysisRuntimeIdentityHashOne).HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.AnalysisRuntimeIdentityHashTwo).HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.Phase).HasConversion<string>().HasMaxLength(32);
        builder.Property(value => value.CircuitState).HasConversion<string>().HasMaxLength(16);
        builder.Property(value => value.ReasonCategory).HasConversion<string>().HasMaxLength(32);
        builder.Property(value => value.ReasonCode).HasMaxLength(64);
        builder.Property(value => value.StateVersion).IsConcurrencyToken();
        builder.Property(value => value.StartedAt).HasColumnType("timestamp with time zone");
        builder.Property(value => value.UpdatedAt).HasColumnType("timestamp with time zone");
        builder.Property(value => value.ConcurrencyUpdatedAt).HasColumnType("timestamp with time zone");
        builder.Property(value => value.NextProbeAt).HasColumnType("timestamp with time zone");
        builder.Property(value => value.RetryAfterAt).HasColumnType("timestamp with time zone");
        builder.Property(value => value.TerminalAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(value => value.CoordinationId).IsUnique();
        builder.HasIndex(value => value.PairPackageFingerprint).IsUnique();
        builder.HasOne<TwoRepositoryScanCoordination>().WithMany().HasForeignKey(value => value.CoordinationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PreparedScanManifest>().WithMany().HasForeignKey(value => value.ManifestOneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PreparedScanManifest>().WithMany().HasForeignKey(value => value.ManifestTwoId).OnDelete(DeleteBehavior.Restrict);
    }
}
