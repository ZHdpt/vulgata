using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class UncertaintyConfiguration : IEntityTypeConfiguration<Uncertainty>
{
    public void Configure(EntityTypeBuilder<Uncertainty> builder)
    {
        builder.ToTable("Uncertainties", table =>
        {
            table.HasCheckConstraint(
                "CK_Uncertainties_Type_Closed",
                "\"Type\" IN ('cross_repository', 'unsupported_communication')");
            table.HasCheckConstraint(
                "CK_Uncertainties_Status_Closed",
                "\"Status\" IN ('unresolved', 'resolved', 'wontfix')");
            table.HasCheckConstraint(
                "CK_Uncertainties_SubStatus_Closed",
                "\"SubStatus\" IN ('cross_repo', 'unknown_target', 'ambiguous')");
            table.HasCheckConstraint(
                "CK_Uncertainties_ReasonCode_Closed",
                "\"ReasonCode\" IN ('variable_target', 'disallowed_invocation', " +
                "'malformed_provider', 'provider_out_of_root', 'unmatched', " +
                "'multiple_providers', 'normalization_mismatch', 'unsupported_kind')");
            table.HasCheckConstraint(
                "CK_Uncertainties_ObservationKind_Closed",
                "\"ObservationKind\" IS NULL OR \"ObservationKind\" = 'file_storage'");
            table.HasCheckConstraint(
                "CK_Uncertainties_UnsupportedCombination_Closed",
                "(\"ReasonCode\" = 'unsupported_kind' AND \"Type\" = " +
                "'unsupported_communication' AND \"ObservationKind\" = 'file_storage') OR " +
                "(\"ReasonCode\" <> 'unsupported_kind' AND \"Type\" = 'cross_repository' " +
                "AND \"ObservationKind\" IS NULL)");
            table.HasCheckConstraint(
                "CK_Uncertainties_EndpointPair_Closed",
                "(\"EndpointIdentifier\" IS NULL) = " +
                "(\"EndpointIdentifierNormalized\" IS NULL)");
            table.HasCheckConstraint(
                "CK_Uncertainties_Resolution_Closed",
                "(\"Status\" = 'unresolved' AND \"ResolvedAt\" IS NULL AND " +
                "\"ResolvedByDocumentVersionId\" IS NULL) OR \"Status\" <> 'unresolved'");
            table.HasCheckConstraint(
                "CK_Uncertainties_SchemaVersion_Closed",
                "\"ProfileSchemaVersion\" = '1.0'");
            table.HasCheckConstraint(
                "CK_Uncertainties_Hashes_Closed",
                LowerHex64("\"ProfileFingerprint\"") + " AND " +
                LowerHex64("\"EvidenceFingerprint\""));
        });
        builder.HasKey(uncertainty => uncertainty.Id);
        builder.Property(uncertainty => uncertainty.Id).ValueGeneratedNever();
        builder.Property(uncertainty => uncertainty.Type).HasMaxLength(32);
        builder.Property(uncertainty => uncertainty.Status).HasMaxLength(16);
        builder.Property(uncertainty => uncertainty.SubStatus).HasMaxLength(32);
        builder.Property(uncertainty => uncertainty.ReasonCode).HasMaxLength(64);
        builder.Property(uncertainty => uncertainty.ObservationKind).HasMaxLength(32);
        builder.Property(uncertainty => uncertainty.EndpointIdentifier).HasMaxLength(512);
        builder.Property(uncertainty => uncertainty.EndpointIdentifierNormalized).HasMaxLength(512);
        builder.Property(uncertainty => uncertainty.ProfileSchemaVersion).HasMaxLength(16);
        builder.Property(uncertainty => uncertainty.ProfileId).HasMaxLength(128);
        builder.Property(uncertainty => uncertainty.ProfileVersion).HasMaxLength(32);
        builder.Property(uncertainty => uncertainty.ProfileFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(uncertainty => uncertainty.OccurrenceId).HasMaxLength(128);
        builder.Property(uncertainty => uncertainty.EvidenceLocator).HasMaxLength(4_096);
        builder.Property(uncertainty => uncertainty.EvidenceFingerprint).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(uncertainty => uncertainty.EvidenceFingerprint).IsUnique();
        builder.HasIndex(uncertainty => new
        {
            uncertainty.RepositoryId,
            uncertainty.Status,
            uncertainty.SubStatus,
        });
        builder.HasOne(uncertainty => uncertainty.DocumentVersion)
            .WithMany()
            .HasForeignKey(uncertainty => new
            {
                uncertainty.DocumentVersionId,
                uncertainty.RepositoryId,
                uncertainty.ScanRunId,
            })
            .HasPrincipalKey(version => new
            {
                version.Id,
                version.RepositoryId,
                version.ScanRunId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(uncertainty => uncertainty.SourceAnchor)
            .WithMany()
            .HasForeignKey(uncertainty => new
            {
                uncertainty.SourceAnchorId,
                uncertainty.DocumentVersionId,
                uncertainty.RepositoryId,
                uncertainty.ScanRunId,
            })
            .HasPrincipalKey(anchor => new
            {
                anchor.Id,
                anchor.DocumentVersionId,
                anchor.RepositoryId,
                anchor.ScanRunId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(uncertainty => uncertainty.TargetRepository)
            .WithMany()
            .HasForeignKey(uncertainty => uncertainty.TargetRepositoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(uncertainty => uncertainty.ResolvedByDocumentVersion)
            .WithMany()
            .HasForeignKey(uncertainty => uncertainty.ResolvedByDocumentVersionId)
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
