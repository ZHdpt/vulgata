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
        builder.Property(execution => execution.StartedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(execution => execution.TerminalAt)
            .HasColumnType("timestamp with time zone");
        builder.Property(execution => execution.PromptIdentity).HasMaxLength(128);
        builder.Property(execution => execution.DocumentSchemaIdentity).HasMaxLength(128);
        builder.Property(execution => execution.AnalysisRuntimeIdentityJson).HasColumnType("jsonb");
        builder.Property(execution => execution.AnalysisRuntimeIdentityHash)
            .HasMaxLength(64)
            .IsFixedLength();
        builder.Property(execution => execution.PauseCategory)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(execution => execution.PauseCode).HasMaxLength(64);
        builder.Property(execution => execution.FailureCategory)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(execution => execution.FailureCode).HasMaxLength(64);
        builder.Property(execution => execution.StateVersion)
            .HasDefaultValue(0L)
            .IsConcurrencyToken();
        builder.Property(execution => execution.UpdatedAt)
            .HasColumnType("timestamp with time zone");

        // Closed-enum CHECK constraints (mirrors the Story 11.1 pattern).
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ScanExecutions_Phase_Closed",
            "\"Phase\" IN ('Pass1Planning', 'Pass1Running', 'Pass1Completed', 'Completed', " +
            "'Failed', 'Cancelled', 'Prepared', 'Running', 'PausedProviderUnavailable', " +
            "'PausedOperatorCorrection', 'CompletedWithWarnings', 'FailedIntegrity', 'FailedSecurity', 'FailedPass1')"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ScanExecutions_TerminalReasonKind_Closed",
            "\"TerminalReasonKind\" IS NULL OR \"TerminalReasonKind\" IN (" +
            "'SchemaInvalid', 'Error', 'Unbounded', 'MissingPrerequisite', " +
            "'DuplicateActiveScan', 'MissingCodeUnit', 'ForbiddenContent', 'OverLimitPacket')"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ScanExecutions_RestartIndicator_Bound",
            "0 <= \"RestartIndicator\" AND \"RestartIndicator\" <= 32"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ScanExecutions_StateVersion_Bound",
            "\"StateVersion\" >= 0"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ScanExecutions_NewPathBinding_Shape",
            "(\"Phase\" IN ('Pass1Planning', 'Pass1Running', 'Pass1Completed', 'Failed') AND " +
            "\"PromptIdentity\" IS NULL AND \"DocumentSchemaIdentity\" IS NULL AND " +
            "\"AnalysisRuntimeIdentityJson\" IS NULL AND \"AnalysisRuntimeIdentityHash\" IS NULL AND " +
            "\"UpdatedAt\" IS NULL) OR " +
            "(\"Phase\" IN ('Prepared', 'Running', 'PausedProviderUnavailable', " +
            "'PausedOperatorCorrection', 'CompletedWithWarnings', 'FailedIntegrity', 'FailedSecurity', 'FailedPass1') AND " +
            "\"PromptIdentity\" IS NOT NULL AND \"DocumentSchemaIdentity\" IS NOT NULL AND " +
            "\"AnalysisRuntimeIdentityJson\" IS NOT NULL AND \"AnalysisRuntimeIdentityHash\" IS NOT NULL AND " +
            "\"UpdatedAt\" IS NOT NULL) OR " +
            "(\"Phase\" IN ('Completed', 'Cancelled') AND ((\"PromptIdentity\" IS NULL AND " +
            "\"DocumentSchemaIdentity\" IS NULL AND \"AnalysisRuntimeIdentityJson\" IS NULL AND " +
            "\"AnalysisRuntimeIdentityHash\" IS NULL AND \"UpdatedAt\" IS NULL) OR " +
            "(\"PromptIdentity\" IS NOT NULL AND \"DocumentSchemaIdentity\" IS NOT NULL AND " +
            "\"AnalysisRuntimeIdentityJson\" IS NOT NULL AND \"AnalysisRuntimeIdentityHash\" IS NOT NULL AND " +
            "\"UpdatedAt\" IS NOT NULL)))"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ScanExecutions_PauseFailure_Shape",
            "(\"Phase\" = 'PausedProviderUnavailable' AND \"PauseCategory\" = 'ProviderUnavailable' AND " +
            "\"PauseCode\" IS NOT NULL AND \"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR " +
            "(\"Phase\" = 'PausedOperatorCorrection' AND \"PauseCategory\" = 'OperatorCorrection' AND " +
            "\"PauseCode\" IS NOT NULL AND \"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR " +
            "(\"Phase\" = 'FailedIntegrity' AND \"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND " +
            "\"FailureCategory\" = 'Integrity' AND \"FailureCode\" IS NOT NULL) OR " +
            "(\"Phase\" = 'FailedSecurity' AND \"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND " +
            "\"FailureCategory\" = 'Security' AND \"FailureCode\" IS NOT NULL) OR " +
            "(\"Phase\" = 'FailedPass1' AND \"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND " +
            "\"FailureCategory\" = 'Pass1Policy' AND \"FailureCode\" IS NOT NULL) OR " +
            "(\"Phase\" NOT IN ('PausedProviderUnavailable', 'PausedOperatorCorrection', 'FailedIntegrity', 'FailedSecurity', 'FailedPass1') AND " +
            "\"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND " +
            "((\"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR " +
            "(\"Phase\" = 'Cancelled' AND \"FailureCategory\" = 'Cancellation' AND \"FailureCode\" IS NOT NULL)))"));

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
