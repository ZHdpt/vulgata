using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class ExactCrossRepositoryEdgeEvidenceConfiguration :
    IEntityTypeConfiguration<ExactCrossRepositoryEdgeEvidence>
{
    public void Configure(EntityTypeBuilder<ExactCrossRepositoryEdgeEvidence> builder)
    {
        builder.ToTable("ExactCrossRepositoryEdgeEvidence", table =>
        {
            table.HasCheckConstraint(
                "CK_ExactCrossRepositoryEdgeEvidence_Repositories_Distinct",
                "\"ConsumerRepositoryId\" <> \"ProviderRepositoryId\"");
            table.HasCheckConstraint(
                "CK_ExactCrossRepositoryEdgeEvidence_Protocol_Closed",
                "\"Protocol\" = 'CustomRpc'");
            table.HasCheckConstraint(
                "CK_ExactCrossRepositoryEdgeEvidence_ResolutionKind_Closed",
                "\"ResolutionKind\" = 'ExactConfigured'");
            table.HasCheckConstraint(
                "CK_ExactCrossRepositoryEdgeEvidence_Confidence_Exact",
                "\"Confidence\" = 1.0000");
            table.HasCheckConstraint(
                "CK_ExactCrossRepositoryEdgeEvidence_SchemaVersion_Closed",
                "\"ProfileSchemaVersion\" = '1.0'");
            table.HasCheckConstraint(
                "CK_ExactCrossRepositoryEdgeEvidence_Hashes_Closed",
                LowerHex64("\"ProfileFingerprint\"") + " AND " +
                LowerHex64("\"EvidenceFingerprint\""));
        });
        builder.HasKey(evidence => evidence.EdgeId);
        builder.Property(evidence => evidence.EdgeId).ValueGeneratedNever();
        builder.Property(evidence => evidence.ConsumerRawIdentifier).HasMaxLength(512);
        builder.Property(evidence => evidence.ProviderRawIdentifier).HasMaxLength(512);
        builder.Property(evidence => evidence.NormalizedEndpointIdentifier).HasMaxLength(512);
        builder.Property(evidence => evidence.Protocol).HasConversion<string>().HasMaxLength(32);
        builder.Property(evidence => evidence.ResolutionKind).HasMaxLength(32);
        builder.Property(evidence => evidence.Confidence).HasPrecision(5, 4);
        builder.Property(evidence => evidence.ProfileSchemaVersion).HasMaxLength(16);
        builder.Property(evidence => evidence.ProfileId).HasMaxLength(128);
        builder.Property(evidence => evidence.ProfileVersion).HasMaxLength(32);
        builder.Property(evidence => evidence.ProfileFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(evidence => evidence.OccurrenceId).HasMaxLength(128);
        builder.Property(evidence => evidence.ConsumerEvidenceLocator).HasMaxLength(4_096);
        builder.Property(evidence => evidence.ProviderEvidenceLocator).HasMaxLength(4_096);
        builder.Property(evidence => evidence.EvidenceFingerprint).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(evidence => evidence.EvidenceFingerprint).IsUnique();
        builder.HasOne(evidence => evidence.Edge)
            .WithOne()
            .HasForeignKey<ExactCrossRepositoryEdgeEvidence>(evidence => evidence.EdgeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(evidence => evidence.ConsumerSourceAnchor)
            .WithMany()
            .HasForeignKey(evidence => new
            {
                evidence.ConsumerSourceAnchorId,
                evidence.ConsumerDocumentVersionId,
                evidence.ConsumerRepositoryId,
                evidence.ConsumerScanRunId,
            })
            .HasPrincipalKey(anchor => new
            {
                anchor.Id,
                anchor.DocumentVersionId,
                anchor.RepositoryId,
                anchor.ScanRunId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(evidence => evidence.ProviderSourceAnchor)
            .WithMany()
            .HasForeignKey(evidence => new
            {
                evidence.ProviderSourceAnchorId,
                evidence.ProviderDocumentVersionId,
                evidence.ProviderRepositoryId,
                evidence.ProviderScanRunId,
            })
            .HasPrincipalKey(anchor => new
            {
                anchor.Id,
                anchor.DocumentVersionId,
                anchor.RepositoryId,
                anchor.ScanRunId,
            })
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static string LowerHex64(string column)
    {
        string stripped = column;
        foreach (char character in "0123456789abcdef")
        {
            stripped = $"replace({stripped}, '{character}', '')";
        }

        return $"length({column}) = 64 AND lower({column}) = {column} AND length({stripped}) = 0";
    }
}
