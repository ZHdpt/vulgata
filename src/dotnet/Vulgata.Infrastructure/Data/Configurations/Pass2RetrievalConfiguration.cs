using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class Pass2RetrievalBudgetConfiguration : IEntityTypeConfiguration<Pass2RetrievalBudget>
{
    public void Configure(EntityTypeBuilder<Pass2RetrievalBudget> builder)
    {
        builder.ToTable("Pass2RetrievalBudgets", table =>
        {
            table.HasCheckConstraint(
                "CK_Pass2RetrievalBudgets_Bounds",
                "\"CallsReserved\" BETWEEN 0 AND 8 AND \"ResultsReturned\" BETWEEN 0 AND 10 AND \"AdditionalDocumentCount\" BETWEEN 0 AND 4 AND \"ReturnedUtf8Bytes\" BETWEEN 0 AND 65536 AND \"StateVersion\" >= 0");
            table.HasCheckConstraint(
                "CK_Pass2RetrievalBudgets_State",
                "\"State\" IN ('Available','Exhausted') AND ((\"CallsReserved\" = 8 AND \"State\" = 'Exhausted') OR (\"CallsReserved\" < 8 AND \"State\" = 'Available'))");
        });
        builder.HasKey(budget => budget.Pass2JobExecutionId);
        builder.Property(budget => budget.Pass2JobExecutionId).ValueGeneratedNever();
        builder.Property(budget => budget.State).HasConversion<string>().HasMaxLength(32);
        builder.Property(budget => budget.StateVersion).IsConcurrencyToken();
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
        const string hexToolCall = "length(\"ToolCallIdFingerprint\") = 64 AND lower(\"ToolCallIdFingerprint\") = \"ToolCallIdFingerprint\" AND length(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(\"ToolCallIdFingerprint\", '0', ''), '1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), '8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '')) = 0";
        const string hexRequest = "length(\"RequestFingerprint\") = 64 AND lower(\"RequestFingerprint\") = \"RequestFingerprint\" AND length(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(\"RequestFingerprint\", '0', ''), '1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), '8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '')) = 0";
        const string hexResultSet = "length(\"ResultSetFingerprint\") = 64 AND lower(\"ResultSetFingerprint\") = \"ResultSetFingerprint\" AND length(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(\"ResultSetFingerprint\", '0', ''), '1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), '8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '')) = 0";
        builder.ToTable("Pass2RetrievalReceipts", table =>
        {
            table.HasCheckConstraint(
                "CK_Pass2RetrievalReceipts_Bounds",
                "\"AttemptOrdinal\" BETWEEN 1 AND 2 AND \"CallOrdinal\" BETWEEN 1 AND 8 AND \"ResultCount\" BETWEEN 0 AND 10 AND \"ReturnedUtf8Bytes\" BETWEEN 0 AND 65536 AND \"NewAdditionalDocumentCount\" BETWEEN 0 AND 4");
            table.HasCheckConstraint(
                "CK_Pass2RetrievalReceipts_Fingerprints",
                $"{hexToolCall} AND {hexRequest} AND {hexResultSet}");
            table.HasCheckConstraint(
                "CK_Pass2RetrievalReceipts_ClosedValues",
                "\"State\" IN ('Accepted','NoResults','BoundExceeded','Refused','Unavailable') AND \"Operation\" IN ('Search','Sections','SourceAnchors','SourceExcerpt','RelatedDocuments')");
            table.HasCheckConstraint(
                "CK_Pass2RetrievalReceipts_OutcomeDeltas",
                "\"State\" = 'Accepted' OR (\"ResultCount\" = 0 AND \"ReturnedUtf8Bytes\" = 0 AND \"NewAdditionalDocumentCount\" = 0)");
        });
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id).ValueGeneratedNever();
        builder.Property(receipt => receipt.State).HasConversion<string>().HasMaxLength(32);
        builder.Property(receipt => receipt.Operation).HasConversion<string>().HasMaxLength(32);
        builder.Property(receipt => receipt.ToolCallIdFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(receipt => receipt.RequestFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(receipt => receipt.ResultSetFingerprint).HasMaxLength(64).IsFixedLength();
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
        const string hexDescriptor = "length(\"DescriptorFingerprint\") = 64 AND lower(\"DescriptorFingerprint\") = \"DescriptorFingerprint\" AND length(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(\"DescriptorFingerprint\", '0', ''), '1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), '8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '')) = 0";
        const string hexEvidence = "length(\"EvidenceFingerprint\") = 64 AND lower(\"EvidenceFingerprint\") = \"EvidenceFingerprint\" AND length(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(\"EvidenceFingerprint\", '0', ''), '1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), '8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '')) = 0";
        builder.ToTable("Pass2RetrievalResults", table =>
        {
            table.HasCheckConstraint(
                "CK_Pass2RetrievalResults_Bounds",
                "\"AttemptOrdinal\" BETWEEN 1 AND 2 AND \"ResultOrdinal\" BETWEEN 1 AND 10 AND \"ReturnedUtf8Bytes\" BETWEEN 0 AND 65536");
            table.HasCheckConstraint(
                "CK_Pass2RetrievalResults_Fingerprints",
                $"{hexDescriptor} AND {hexEvidence}");
            table.HasCheckConstraint(
                "CK_Pass2RetrievalResults_ClosedValues",
                "\"State\" = 'Admitted' AND \"Kind\" IN ('Document','Section','SourceAnchor','SourceExcerpt','Relationship')");
            table.HasCheckConstraint(
                "CK_Pass2RetrievalResults_Coordinates",
                "(\"Kind\" = 'Document' AND \"SectionId\" IS NULL AND \"SourceAnchorId\" IS NULL AND \"EdgeId\" IS NULL AND \"ByteStart\" IS NULL AND \"ByteEnd\" IS NULL) OR " +
                "(\"Kind\" = 'Section' AND \"SectionId\" IS NOT NULL AND \"SourceAnchorId\" IS NULL AND \"EdgeId\" IS NULL AND \"ByteStart\" IS NULL AND \"ByteEnd\" IS NULL) OR " +
                "(\"Kind\" = 'SourceAnchor' AND \"SectionId\" IS NULL AND \"SourceAnchorId\" IS NOT NULL AND \"EdgeId\" IS NULL AND \"ByteStart\" IS NULL AND \"ByteEnd\" IS NULL) OR " +
                "(\"Kind\" = 'SourceExcerpt' AND \"SectionId\" IS NULL AND \"SourceAnchorId\" IS NOT NULL AND \"EdgeId\" IS NULL AND \"ByteStart\" >= 0 AND \"ByteEnd\" > \"ByteStart\") OR " +
                "(\"Kind\" = 'Relationship' AND \"SectionId\" IS NULL AND \"SourceAnchorId\" IS NULL AND \"EdgeId\" IS NOT NULL AND \"ByteStart\" IS NULL AND \"ByteEnd\" IS NULL)");
        });
        builder.HasKey(result => result.Id);
        builder.Property(result => result.Id).ValueGeneratedNever();
        builder.Property(result => result.State).HasConversion<string>().HasMaxLength(32);
        builder.Property(result => result.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property<Guid?>("SectionId");
        builder.Property<Guid?>("SourceAnchorId");
        builder.Property(result => result.DescriptorFingerprint).HasMaxLength(64).IsFixedLength();
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
