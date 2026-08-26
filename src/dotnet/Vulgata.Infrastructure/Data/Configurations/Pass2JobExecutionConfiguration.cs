using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class Pass2JobExecutionConfiguration : IEntityTypeConfiguration<Pass2JobExecution>
{
    public void Configure(EntityTypeBuilder<Pass2JobExecution> builder)
    {
        const string completionFingerprintIsLowerHex =
            "length(\"CompletionFingerprint\") = 64 AND lower(\"CompletionFingerprint\") = \"CompletionFingerprint\" AND length(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(\"CompletionFingerprint\", '0', ''), '1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), '8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '')) = 0";
        builder.ToTable("Pass2JobExecutions", table =>
        {
            table.HasCheckConstraint(
                "CK_Pass2JobExecutions_Bounds",
                "\"GroupOrder\" BETWEEN 1 AND 10 AND \"AttemptCount\" BETWEEN 0 AND 2 AND \"StateVersion\" >= 0");
            table.HasCheckConstraint(
                "CK_Pass2JobExecutions_StateShape",
                "(\"State\" = 'Running' AND \"LeaseToken\" IS NOT NULL AND \"LeaseExpiresAt\" IS NOT NULL AND \"NextAttemptAt\" IS NULL) OR " +
                "(\"State\" = 'RetryScheduled' AND \"LeaseToken\" IS NULL AND \"LeaseExpiresAt\" IS NULL AND \"NextAttemptAt\" IS NOT NULL) OR " +
                "(\"State\" IN ('Pending','Failed','Cancelled') AND \"LeaseToken\" IS NULL AND \"LeaseExpiresAt\" IS NULL AND \"NextAttemptAt\" IS NULL) OR " +
                $"(\"State\" = 'Completed' AND \"LeaseToken\" IS NULL AND \"LeaseExpiresAt\" IS NULL AND \"NextAttemptAt\" IS NULL AND \"CommittedDocumentVersionId\" IS NOT NULL AND {completionFingerprintIsLowerHex})");
            table.HasCheckConstraint(
                "CK_Pass2JobExecutions_FailureShape",
                "(\"FailureCategory\" IS NULL) = (\"FailureCode\" IS NULL)");
        });
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).ValueGeneratedNever();
        builder.Property(job => job.State).HasConversion<string>().HasMaxLength(32);
        builder.Property(job => job.FailureCategory).HasConversion<string>().HasMaxLength(32);
        builder.Property(job => job.FailureCode).HasMaxLength(128);
        builder.Property(job => job.StateVersion).IsConcurrencyToken();
        builder.Property(job => job.CompletionFingerprint).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(job => job.FlowGroupId).IsUnique();
        builder.HasIndex(job => new { job.Pass2ExecutionId, job.GroupOrder }).IsUnique();
        builder.HasOne<Pass2Execution>()
            .WithMany()
            .HasForeignKey(job => job.Pass2ExecutionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FlowGroup>()
            .WithOne()
            .HasForeignKey<Pass2JobExecution>(job => job.FlowGroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentVersion>()
            .WithMany()
            .HasForeignKey(job => job.CommittedDocumentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
