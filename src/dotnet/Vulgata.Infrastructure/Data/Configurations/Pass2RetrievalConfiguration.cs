using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class Pass2RetrievalBudgetConfiguration : IEntityTypeConfiguration<Pass2RetrievalBudget>
{
    public void Configure(EntityTypeBuilder<Pass2RetrievalBudget> builder)
    {
        builder.ToTable("Pass2RetrievalBudgets");
        builder.HasKey(budget => budget.Id);
        builder.Property(budget => budget.Id).ValueGeneratedNever();
        builder.Property(budget => budget.State).HasConversion<string>().HasMaxLength(32);
        builder.Property(budget => budget.StateVersion).IsConcurrencyToken();
        builder.HasIndex(budget => budget.Pass2JobExecutionId).IsUnique();
        builder.HasOne<Pass2JobExecution>()
            .WithOne()
            .HasForeignKey<Pass2RetrievalBudget>(budget => budget.Pass2JobExecutionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class Pass2RetrievalReceiptConfiguration : IEntityTypeConfiguration<Pass2RetrievalReceipt>
{
    public void Configure(EntityTypeBuilder<Pass2RetrievalReceipt> builder)
    {
        builder.ToTable("Pass2RetrievalReceipts");
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id).ValueGeneratedNever();
        builder.Property(receipt => receipt.State).HasConversion<string>().HasMaxLength(32);
        builder.Property(receipt => receipt.RequestFingerprint).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(receipt => new { receipt.Pass2JobExecutionId, receipt.CallOrdinal }).IsUnique();
        builder.HasOne<Pass2JobExecution>()
            .WithMany()
            .HasForeignKey(receipt => receipt.Pass2JobExecutionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class Pass2RetrievalResultConfiguration : IEntityTypeConfiguration<Pass2RetrievalResult>
{
    public void Configure(EntityTypeBuilder<Pass2RetrievalResult> builder)
    {
        builder.ToTable("Pass2RetrievalResults");
        builder.HasKey(result => result.Id);
        builder.Property(result => result.Id).ValueGeneratedNever();
        builder.Property(result => result.State).HasConversion<string>().HasMaxLength(32);
        builder.Property(result => result.EvidenceFingerprint).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(result => new { result.ReceiptId, result.ResultOrdinal }).IsUnique();
        builder.HasOne<Pass2RetrievalReceipt>()
            .WithMany()
            .HasForeignKey(result => result.ReceiptId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Pass2JobExecution>()
            .WithMany()
            .HasForeignKey(result => result.Pass2JobExecutionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentVersion>()
            .WithMany()
            .HasForeignKey(result => result.DocumentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
