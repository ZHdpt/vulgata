using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class PreparedScanManifestEntryConfiguration : IEntityTypeConfiguration<PreparedScanManifestEntry>
{
    public void Configure(EntityTypeBuilder<PreparedScanManifestEntry> builder)
    {
        builder.ToTable("PreparedScanManifestEntries", table =>
        {
            table.HasCheckConstraint("CK_PreparedScanManifestEntries_Fingerprint", "length(\"FileCodeUnitIdentityFingerprint\") = 64");
            table.HasCheckConstraint("CK_PreparedScanManifestEntries_Scores", "\"DirectBusinessScore\" >= 0 AND \"FinalBusinessScore\" >= \"DirectBusinessScore\"");
            table.HasCheckConstraint("CK_PreparedScanManifestEntries_Ordinals", "\"ManifestOrdinal\" >= 1 AND \"PairSelectionOrdinal\" >= 1");
            table.HasCheckConstraint("CK_PreparedScanManifestEntries_SelectionReason", "\"SelectionReason\" IN ('DirectBusinessEvidence', 'RelatedCall', 'RelatedImport', 'RelatedInheritance', 'ProductionFallback')");
        });
        builder.HasKey(entry => entry.FileCodeUnitId);
        builder.HasAlternateKey(entry => new { entry.PreparedScanManifestId, entry.FileCodeUnitId, entry.FileCodeUnitIdentityFingerprint, entry.PairSelectionOrdinal });
        builder.Property(entry => entry.FileCodeUnitId).ValueGeneratedNever();
        builder.Property(entry => entry.FileCodeUnitIdentityFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(entry => entry.SelectionReason).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(entry => new { entry.PreparedScanManifestId, entry.ManifestOrdinal }).IsUnique();
        builder.HasIndex(entry => new { entry.PreparedScanManifestId, entry.FileCodeUnitIdentityFingerprint }).IsUnique();
        builder.HasIndex(entry => new { entry.CoordinationId, entry.PairSelectionOrdinal }).IsUnique();
        builder.HasOne(entry => entry.PreparedScanManifest).WithMany(manifest => manifest.Entries).HasForeignKey(entry => new { entry.PreparedScanManifestId, entry.CoordinationId }).HasPrincipalKey(manifest => new { manifest.Id, manifest.CoordinationId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entry => entry.FileCodeUnit).WithOne().HasForeignKey<PreparedScanManifestEntry>(entry => entry.FileCodeUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}
