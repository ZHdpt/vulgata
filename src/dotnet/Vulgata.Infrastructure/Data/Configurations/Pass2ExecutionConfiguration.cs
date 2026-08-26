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
        builder.Property(execution => execution.PublishedCount);
        builder.Property(execution => execution.FailedCount);
        builder.Property(execution => execution.StateVersion).IsConcurrencyToken();
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Pass2Executions_Accounting",
            "\"PublishedCount\" >= 0 AND \"FailedCount\" >= 0"));
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
