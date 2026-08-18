using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class ScanJobExecutionConfiguration : IEntityTypeConfiguration<ScanJobExecution>
{
    public void Configure(EntityTypeBuilder<ScanJobExecution> builder)
    {
        builder.ToTable("ScanJobExecutions", table =>
        {
            table.HasCheckConstraint(
                "CK_ScanJobExecutions_State_Closed",
                "\"State\" IN ('Pending', 'Running', 'RetryScheduled', 'Completed', 'Failed', 'Cancelled')");
            table.HasCheckConstraint(
                "CK_ScanJobExecutions_FailureCategory_Closed",
                "\"FailureCategory\" IS NULL OR \"FailureCategory\" IN (" +
                "'ProviderTransient', 'ProviderConfiguration', 'JobTransient', 'JobPermanent', " +
                "'Integrity', 'Security', 'Cancellation', 'Unclassified')");
            table.HasCheckConstraint(
                "CK_ScanJobExecutions_OrdinalAttemptVersion_Bounds",
                "\"ManifestOrdinal\" > 0 AND \"AttemptCount\" >= 0 AND \"StateVersion\" >= 0");
            table.HasCheckConstraint(
                "CK_ScanJobExecutions_LeaseRetryCompletion_Shape",
                "(\"State\" = 'Running' AND \"LeaseToken\" IS NOT NULL AND " +
                "\"LeaseExpiresAt\" IS NOT NULL AND \"LeaseExpiresAt\" > \"StateChangedAt\" AND " +
                "\"NextAttemptAt\" IS NULL AND \"CommittedDocumentVersionId\" IS NULL) OR " +
                "(\"State\" = 'RetryScheduled' AND \"LeaseToken\" IS NULL AND " +
                "\"LeaseExpiresAt\" IS NULL AND \"NextAttemptAt\" IS NOT NULL AND " +
                "\"CommittedDocumentVersionId\" IS NULL) OR " +
                "(\"State\" = 'Completed' AND \"LeaseToken\" IS NULL AND " +
                "\"LeaseExpiresAt\" IS NULL AND \"NextAttemptAt\" IS NULL AND " +
                "\"CommittedDocumentVersionId\" IS NOT NULL) OR " +
                "(\"State\" IN ('Pending', 'Failed', 'Cancelled') AND \"LeaseToken\" IS NULL AND " +
                "\"LeaseExpiresAt\" IS NULL AND \"NextAttemptAt\" IS NULL AND " +
                "\"CommittedDocumentVersionId\" IS NULL)");
            table.HasCheckConstraint(
                "CK_ScanJobExecutions_Failure_Shape",
                "(\"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR " +
                "(\"FailureCategory\" IS NOT NULL AND \"FailureCode\" IS NOT NULL)");
        });
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).ValueGeneratedNever();
        builder.Property(job => job.State).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(job => job.FailureCategory).HasConversion<string>().HasMaxLength(32);
        builder.Property(job => job.FailureCode).HasMaxLength(64);
        builder.Property(job => job.NextAttemptAt).HasColumnType("timestamp with time zone");
        builder.Property(job => job.LeaseExpiresAt).HasColumnType("timestamp with time zone");
        builder.Property(job => job.StateChangedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(job => job.StateVersion).IsConcurrencyToken();
        builder.HasIndex(job => new { job.ScanExecutionId, job.FileCodeUnitId }).IsUnique();
        builder.HasIndex(job => new { job.ScanExecutionId, job.ManifestOrdinal }).IsUnique();
        builder.HasIndex(job => job.CommittedDocumentVersionId)
            .IsUnique()
            .HasFilter("\"CommittedDocumentVersionId\" IS NOT NULL");
        builder.HasOne<Repository>()
            .WithMany()
            .HasForeignKey(job => job.RepositoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanRun>()
            .WithMany()
            .HasForeignKey(job => new { job.ScanRunId, job.RepositoryId })
            .HasPrincipalKey(scanRun => new { scanRun.Id, scanRun.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanExecution>()
            .WithMany()
            .HasForeignKey(job => new { job.ScanExecutionId, job.RepositoryId })
            .HasPrincipalKey(execution => new { execution.Id, execution.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CodeUnit>()
            .WithMany()
            .HasForeignKey(job => new { job.FileCodeUnitId, job.ScanRunId, job.RepositoryId })
            .HasPrincipalKey(unit => new { unit.Id, unit.ScanRunId, unit.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentVersion>()
            .WithMany()
            .HasForeignKey(job => new
            {
                job.CommittedDocumentVersionId,
                job.RepositoryId,
                job.ScanRunId,
            })
            .HasPrincipalKey(version => new
            {
                version.Id,
                version.RepositoryId,
                version.ScanRunId,
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
