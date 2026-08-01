using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class ScanExecutionConfiguration : IEntityTypeConfiguration<ScanExecution>
{
    public void Configure(EntityTypeBuilder<ScanExecution> builder)
    {
        builder.ToTable("ScanExecutions");
        builder.HasKey(execution => execution.Id);
        // Alternate keys mirror the Story 11.1 composite-identity pattern.
        builder.HasAlternateKey(execution => new { execution.Id, execution.RepositoryId });
        builder.HasAlternateKey(execution => new { execution.ScanRunId, execution.RepositoryId });

        builder.Property(execution => execution.Id).ValueGeneratedNever();
        builder.Property(execution => execution.RepositoryId).IsRequired();
        builder.Property(execution => execution.SystemId).IsRequired();
        builder.Property(execution => execution.ScanRunId).IsRequired();
        builder.Property(execution => execution.PreScanResultId).IsRequired();
        builder.Property(execution => execution.ProfiledRevision).HasMaxLength(64).IsRequired();
        builder.Property(execution => execution.Phase)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(execution => execution.InFlightJobId);
        builder.Property(execution => execution.CompletedJobCount).IsRequired();
        builder.Property(execution => execution.TotalJobCount).IsRequired();
        builder.Property(execution => execution.RestartIndicator).IsRequired();
        builder.Property(execution => execution.TerminalMessage).HasMaxLength(512);
        builder.Property(execution => execution.TerminalReasonKind)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(execution => execution.StartedAt).IsRequired();
        builder.Property(execution => execution.TerminalAt);

        // Closed-enum CHECK constraints (mirrors the Story 11.1 pattern).
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ScanExecutions_Phase_Closed",
            "\"Phase\" IN ('Pass1Planning', 'Pass1Running', 'Pass1Completed', 'Completed', 'Failed', 'Cancelled')"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ScanExecutions_TerminalReasonKind_Closed",
            "\"TerminalReasonKind\" IS NULL OR \"TerminalReasonKind\" IN (" +
            "'SchemaInvalid', 'Error', 'Unbounded', 'MissingPrerequisite', " +
            "'DuplicateActiveScan', 'MissingCodeUnit', 'ForbiddenContent', 'OverLimitPacket')"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ScanExecutions_RestartIndicator_Bound",
            "0 <= \"RestartIndicator\" AND \"RestartIndicator\" <= 32"));

        // Indexes by Repository, ScanRun, Phase for the active-scan lookup.
        builder.HasIndex(execution => new { execution.RepositoryId, execution.ScanRunId, execution.Phase });
        builder.HasIndex(execution => new { execution.ScanRunId, execution.RepositoryId });
        builder.HasIndex(execution => execution.RepositoryId);

        // FKs: Repository (simple) and ScanRun (composite via alternate key).
        // Restrict/NoAction — never cascade delete a ScanExecution.
        builder.HasOne<Repository>()
            .WithMany()
            .HasForeignKey(execution => execution.RepositoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanRun>()
            .WithMany()
            .HasForeignKey(execution => new { execution.ScanRunId, execution.RepositoryId })
            .HasPrincipalKey(scanRun => new { scanRun.Id, scanRun.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
