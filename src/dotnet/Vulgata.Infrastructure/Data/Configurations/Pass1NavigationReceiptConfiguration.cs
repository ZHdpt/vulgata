using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class Pass1NavigationReceiptConfiguration : IEntityTypeConfiguration<Pass1NavigationReceipt>
{
    public void Configure(EntityTypeBuilder<Pass1NavigationReceipt> builder)
    {
        builder.ToTable("Pass1NavigationReceipts", table => table.HasCheckConstraint("CK_Pass1NavigationReceipts_Bounds", "\"ReceiptOrdinal\" BETWEEN 1 AND 9 AND (\"ToolAttemptOrdinal\" IS NULL OR \"ToolAttemptOrdinal\" BETWEEN 1 AND 8) AND (\"Depth\" IS NULL OR \"Depth\" BETWEEN 1 AND 4) AND \"ResultCount\" BETWEEN 0 AND 20 AND \"EvidenceBytes\" BETWEEN 0 AND 65536"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.Operation).HasConversion<string>().HasMaxLength(32);
        builder.Property(value => value.Outcome).HasConversion<string>().HasMaxLength(32);
        builder.Property(value => value.BoundKind).HasConversion<string>().HasMaxLength(32);
        builder.Property(value => value.RequestFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.ReceiptFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.DescriptorJson).HasColumnType("jsonb");
        builder.Property(value => value.CreatedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(value => new { value.ScanJobExecutionId, value.ReceiptOrdinal }).IsUnique();
        builder.HasIndex(value => new { value.ScanJobExecutionId, value.ToolAttemptOrdinal }).IsUnique().HasFilter("\"ToolAttemptOrdinal\" IS NOT NULL");
        builder.HasIndex(value => value.ReceiptFingerprint).IsUnique();
        builder.HasOne<ScanJobExecution>().WithMany().HasForeignKey(value => value.ScanJobExecutionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Pass1PairExecution>().WithMany().HasForeignKey(value => value.PairExecutionId).OnDelete(DeleteBehavior.Restrict);
    }
}
