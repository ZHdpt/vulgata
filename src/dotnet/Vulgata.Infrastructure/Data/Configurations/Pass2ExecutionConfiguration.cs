using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class Pass2ExecutionConfiguration : IEntityTypeConfiguration<Pass2Execution>
{
    public void Configure(EntityTypeBuilder<Pass2Execution> builder)
    {
        builder.ToTable("Pass2Executions");
        builder.HasKey(execution => execution.Id);
        builder.Property(execution => execution.Id).ValueGeneratedNever();
        builder.Property(execution => execution.State).HasConversion<string>().HasMaxLength(64);
        builder.Property(execution => execution.AuthorizationSubject).HasMaxLength(450).IsRequired();
        builder.Property(execution => execution.WorkerPromptIdentity).HasMaxLength(128).IsRequired();
        builder.Property(execution => execution.WorkerPromptFingerprint)
            .HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(execution => execution.SubmissionSchemaIdentity).HasMaxLength(128).IsRequired();
        builder.Property(execution => execution.SubmissionSchemaFingerprint)
            .HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(execution => execution.ToolDeclarationFingerprint)
            .HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(execution => execution.CandidateCount);
        builder.Property(execution => execution.ResolutionFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(execution => execution.PlanningFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(execution => execution.ExecutionFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(execution => execution.PublishedCount);
        builder.Property(execution => execution.FailedCount);
        builder.Property(execution => execution.PlannedCount);
        builder.Property(execution => execution.PlanRecorded);
        builder.Property(execution => execution.TerminalIncompleteCount);
        builder.Property(execution => execution.TerminalBoundExcludedCount);
        builder.Property(execution => execution.TerminalOmissionCount);
        builder.Property(execution => execution.TerminalWarningInventory).HasMaxLength(65_536);
        builder.Property(execution => execution.StateVersion).IsConcurrencyToken();
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Pass2Executions_Accounting",
            "\"CandidateCount\" BETWEEN 0 AND 256 AND " +
            "length(\"ResolutionFingerprint\") = 64 AND " +
            "length(\"PlanningFingerprint\") = 64 AND " +
            "length(\"ExecutionFingerprint\") = 64 AND " +
            "\"PublishedCount\" >= 0 AND \"FailedCount\" >= 0 AND " +
            "\"PlannedCount\" BETWEEN 0 AND 256 AND " +
            "\"TerminalIncompleteCount\" >= 0 AND \"TerminalBoundExcludedCount\" >= 0 AND " +
            "\"TerminalOmissionCount\" >= 0"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Pass2Executions_AcceptedWorkerContract",
            "length(\"WorkerPromptIdentity\") BETWEEN 1 AND 128 AND " +
            "length(\"WorkerPromptFingerprint\") = 64 AND " +
            "length(\"SubmissionSchemaIdentity\") BETWEEN 1 AND 128 AND " +
            "length(\"SubmissionSchemaFingerprint\") = 64 AND " +
            "length(\"ToolDeclarationFingerprint\") = 64"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Pass2Executions_TerminalSummary",
            "(\"State\" IN ('Completed', 'CompletedWithWarnings') AND " +
            "\"TerminalWarningInventory\" IS NOT NULL) OR " +
            "(\"State\" IN ('FailedIntegrity', 'FailedSecurity')) OR " +
            "(\"State\" NOT IN ('Completed', 'CompletedWithWarnings', 'FailedIntegrity', 'FailedSecurity') AND " +
            "\"TerminalWarningInventory\" IS NULL AND \"TerminalIncompleteCount\" = 0 AND " +
            "\"TerminalBoundExcludedCount\" = 0 AND \"TerminalOmissionCount\" = 0)"));
        builder.HasIndex(execution => execution.Pass1PairExecutionId).IsUnique();
        builder.HasOne<Pass1PairExecution>()
            .WithOne()
            .HasForeignKey<Pass2Execution>(execution => execution.Pass1PairExecutionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TwoRepositoryScanCoordination>()
            .WithOne()
            .HasForeignKey<Pass2Execution>(execution => execution.CoordinationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SystemEntity>()
            .WithMany()
            .HasForeignKey(execution => execution.SystemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
