using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class TwoRepositoryScanCoordinationConfiguration :
    IEntityTypeConfiguration<TwoRepositoryScanCoordination>
{
    public void Configure(EntityTypeBuilder<TwoRepositoryScanCoordination> builder)
    {
        builder.ToTable("TwoRepositoryScanCoordinations", table =>
        {
            table.HasCheckConstraint(
                "CK_TwoRepositoryScanCoordinations_Phase_Closed",
                "\"Phase\" IN ('Preparing', 'Pass1', 'Resolving', 'Pass2', 'Paused', " +
                "'Completed', 'CompletedWithWarnings', 'FailedIntegrity', 'FailedSecurity', 'Cancelled')");
            table.HasCheckConstraint(
                "CK_TwoRepositoryScanCoordinations_ReasonCategory_Closed",
                "\"ReasonCategory\" IS NULL OR \"ReasonCategory\" IN (" +
                "'ProviderUnavailable', 'OperatorCorrection', 'Integrity', 'Security', " +
                "'Cancellation', 'Unclassified')");
            table.HasCheckConstraint(
                "CK_TwoRepositoryScanCoordinations_Repositories_Distinct",
                "\"RepositoryOneId\" <> \"RepositoryTwoId\"");
            table.HasCheckConstraint(
                "CK_TwoRepositoryScanCoordinations_StateVersion_Bound",
                "\"StateVersion\" >= 0");
            table.HasCheckConstraint(
                "CK_TwoRepositoryScanCoordinations_TerminalReason_Shape",
                "(\"Phase\" IN ('Completed', 'CompletedWithWarnings') AND \"TerminalAt\" IS NOT NULL AND " +
                "\"ReasonCategory\" IS NULL AND \"ReasonCode\" IS NULL) OR " +
                "(\"Phase\" IN ('FailedIntegrity', 'FailedSecurity', 'Cancelled') AND " +
                "\"TerminalAt\" IS NOT NULL AND \"ReasonCategory\" IS NOT NULL AND \"ReasonCode\" IS NOT NULL) OR " +
                "(\"Phase\" = 'Paused' AND \"TerminalAt\" IS NULL AND " +
                "\"ReasonCategory\" IS NOT NULL AND \"ReasonCode\" IS NOT NULL) OR " +
                "(\"Phase\" IN ('Preparing', 'Pass1', 'Resolving', 'Pass2') AND " +
                "\"TerminalAt\" IS NULL AND \"ReasonCategory\" IS NULL AND \"ReasonCode\" IS NULL)");
        });
        builder.HasKey(coordination => coordination.Id);
        builder.Property(coordination => coordination.Id).ValueGeneratedNever();
        builder.Property(coordination => coordination.ProfiledRevisionOne).HasMaxLength(64).IsRequired();
        builder.Property(coordination => coordination.ProfiledRevisionTwo).HasMaxLength(64).IsRequired();
        builder.Property(coordination => coordination.Phase)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(coordination => coordination.ReasonCategory)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(coordination => coordination.ReasonCode).HasMaxLength(64);
        builder.Property(coordination => coordination.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(coordination => coordination.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(coordination => coordination.TerminalAt)
            .HasColumnType("timestamp with time zone");
        builder.Property(coordination => coordination.StateVersion).IsConcurrencyToken();
        builder.HasOne<SystemEntity>()
            .WithMany()
            .HasForeignKey(coordination => coordination.SystemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Repository>()
            .WithMany()
            .HasForeignKey(coordination => coordination.RepositoryOneId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Repository>()
            .WithMany()
            .HasForeignKey(coordination => coordination.RepositoryTwoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanRun>()
            .WithMany()
            .HasForeignKey(coordination => new
            {
                coordination.ScanRunOneId,
                coordination.RepositoryOneId,
            })
            .HasPrincipalKey(scanRun => new { scanRun.Id, scanRun.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanRun>()
            .WithMany()
            .HasForeignKey(coordination => new
            {
                coordination.ScanRunTwoId,
                coordination.RepositoryTwoId,
            })
            .HasPrincipalKey(scanRun => new { scanRun.Id, scanRun.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanExecution>()
            .WithMany()
            .HasForeignKey(coordination => new
            {
                coordination.ScanExecutionOneId,
                coordination.RepositoryOneId,
            })
            .HasPrincipalKey(execution => new { execution.Id, execution.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanExecution>()
            .WithMany()
            .HasForeignKey(coordination => new
            {
                coordination.ScanExecutionTwoId,
                coordination.RepositoryTwoId,
            })
            .HasPrincipalKey(execution => new { execution.Id, execution.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
