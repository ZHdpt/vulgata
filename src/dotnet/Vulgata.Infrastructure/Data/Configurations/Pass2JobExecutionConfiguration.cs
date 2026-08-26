using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class Pass2JobExecutionConfiguration : IEntityTypeConfiguration<Pass2JobExecution>
{
    public void Configure(EntityTypeBuilder<Pass2JobExecution> builder)
    {
        builder.ToTable("Pass2JobExecutions");
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).ValueGeneratedNever();
        builder.Property(job => job.State).HasConversion<string>().HasMaxLength(32);
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
