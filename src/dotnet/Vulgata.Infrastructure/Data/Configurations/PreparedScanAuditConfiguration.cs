using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class PreparedScanAuditConfiguration : IEntityTypeConfiguration<PreparedScanAudit>
{
    public void Configure(EntityTypeBuilder<PreparedScanAudit> builder)
    {
        builder.ToTable("PreparedScanAudits", table =>
        {
            table.HasCheckConstraint("CK_PreparedScanAudits_Ordinal", "\"Ordinal\" >= 1");
            table.HasCheckConstraint("CK_PreparedScanAudits_Hashes", "(\"FileHash\" IS NULL OR length(\"FileHash\") = 64) AND (\"FileCodeUnitIdentityFingerprint\" IS NULL OR length(\"FileCodeUnitIdentityFingerprint\") = 64) AND length(\"DetailFingerprint\") = 64");
            table.HasCheckConstraint("CK_PreparedScanAudits_Measurements", "(\"RawByteCount\" IS NULL OR \"RawByteCount\" >= 0) AND (\"LogicalLineCount\" IS NULL OR \"LogicalLineCount\" >= 0)");
            table.HasCheckConstraint("CK_PreparedScanAudits_SelectedShape", "(\"Outcome\" = 'Selected' AND \"FileCodeUnitIdentityFingerprint\" IS NOT NULL AND \"PairSelectionOrdinal\" IS NOT NULL AND \"PairSelectionOrdinal\" >= 1 AND \"PreparedScanManifestEntryId\" IS NOT NULL) OR (\"Outcome\" = 'Eligible' AND \"FileCodeUnitIdentityFingerprint\" IS NOT NULL AND \"PairSelectionOrdinal\" IS NULL AND \"PreparedScanManifestEntryId\" IS NULL) OR (\"Outcome\" IN ('Filtered', 'Skipped', 'Warning') AND \"FileCodeUnitIdentityFingerprint\" IS NULL AND \"PairSelectionOrdinal\" IS NULL AND \"PreparedScanManifestEntryId\" IS NULL)");
        });
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.Id).ValueGeneratedNever();
        builder.Property(audit => audit.RelativePath).HasMaxLength(2_048).IsRequired();
        builder.Property(audit => audit.Outcome).HasConversion<string>().HasMaxLength(16);
        builder.Property(audit => audit.Reason).HasConversion<string>().HasMaxLength(64);
        builder.Property(audit => audit.FileHash).HasMaxLength(64).IsFixedLength();
        builder.Property(audit => audit.FileCodeUnitIdentityFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(audit => audit.DetailFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(audit => new { audit.PreparedScanManifestId, audit.Ordinal }).IsUnique();
        builder.HasIndex(audit => new { audit.PreparedScanManifestId, audit.RelativePath, audit.Outcome, audit.Reason, audit.DetailFingerprint }).IsUnique();
        builder.HasIndex(audit => new { audit.PreparedScanManifestId, audit.PreparedScanManifestEntryId }).IsUnique().HasFilter("\"PreparedScanManifestEntryId\" IS NOT NULL");
        builder.HasIndex(audit => new { audit.PreparedScanManifestId, audit.FileCodeUnitIdentityFingerprint }).IsUnique().HasFilter("\"FileCodeUnitIdentityFingerprint\" IS NOT NULL AND \"Outcome\" = 'Selected'");
        builder.HasIndex(audit => new { audit.PreparedScanManifestId, audit.PairSelectionOrdinal }).IsUnique().HasFilter("\"PairSelectionOrdinal\" IS NOT NULL");
        builder.HasOne(audit => audit.PreparedScanManifest)
            .WithMany(manifest => manifest.Audits)
            .HasForeignKey(audit => new
            {
                audit.PreparedScanManifestId,
                audit.ScanRunId,
                audit.RepositoryId,
            })
            .HasPrincipalKey(manifest => new
            {
                manifest.Id,
                manifest.ScanRunId,
                manifest.RepositoryId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(audit => audit.PreparedScanManifestEntry).WithMany().HasForeignKey(audit => new { audit.PreparedScanManifestId, audit.PreparedScanManifestEntryId, audit.FileCodeUnitIdentityFingerprint, audit.PairSelectionOrdinal }).HasPrincipalKey(entry => new { entry.PreparedScanManifestId, FileCodeUnitId = entry.FileCodeUnitId, entry.FileCodeUnitIdentityFingerprint, entry.PairSelectionOrdinal }).OnDelete(DeleteBehavior.Restrict);
    }
}
