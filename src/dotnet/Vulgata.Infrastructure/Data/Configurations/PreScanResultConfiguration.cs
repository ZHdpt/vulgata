using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class PreScanResultConfiguration : IEntityTypeConfiguration<PreScanResult>
{
    public void Configure(EntityTypeBuilder<PreScanResult> builder)
    {
        builder.ToTable("PreScanResults", table =>
        {
            table.HasCheckConstraint(
                "CK_PreScanResults_Identity",
                "\"Id\" = \"ScanRunId\"");
            table.HasCheckConstraint(
                "CK_PreScanResults_State",
                "\"State\" = 'PreScanApproved'");
            table.HasCheckConstraint(
                "CK_PreScanResults_SchemaVersion",
                "\"SchemaVersion\" IN ('1.0', '2.0')");
            table.HasCheckConstraint(
                "CK_PreScanResults_ProfiledRevision_Length",
                "length(\"ProfiledRevision\") BETWEEN 1 AND 64");
            table.HasCheckConstraint(
                "CK_PreScanResults_Fingerprint_Length",
                "length(\"Fingerprint\") = 64");
            table.HasCheckConstraint(
                "CK_PreScanResults_ApprovalMessage_Length",
                "length(\"ApprovalMessage\") BETWEEN 1 AND 256");
            table.HasCheckConstraint(
                "CK_PreScanResults_ConfirmedKinds",
                "\"ConfirmedCommunicationKinds\" BETWEEN 0 AND 3");
            table.HasCheckConstraint(
                "CK_PreScanResults_Counts",
                "\"TotalConfirmedEvidenceCount\" BETWEEN 0 AND 250000 AND " +
                "\"TotalHintCount\" BETWEEN 0 AND 250000 AND " +
                "\"TotalWarningCount\" BETWEEN 0 AND 250000");
            table.HasCheckConstraint(
                "CK_PreScanResults_PreparedV2Shape",
                "(\"SchemaVersion\" = '1.0' AND \"PreparedScanManifestId\" IS NULL AND " +
                "\"PreparedScanManifestFingerprint\" IS NULL AND \"ContextSnapshotId\" IS NULL AND " +
                "\"PreparedScanContextIdentityHash\" IS NULL AND \"ContextContentHash\" IS NULL AND " +
                "\"HandoffSchemaIdentity\" IS NULL AND \"PairPackageFingerprint\" IS NULL) OR " +
                "(\"SchemaVersion\" = '2.0' AND \"PreparedScanManifestId\" IS NOT NULL AND " +
                "length(\"PreparedScanManifestFingerprint\") = 64 AND \"ContextSnapshotId\" IS NOT NULL AND " +
                "length(\"PreparedScanContextIdentityHash\") = 64 AND length(\"ContextContentHash\") = 64 AND " +
                "\"HandoffSchemaIdentity\" = 'prepared-scan-handoff/2.0' AND " +
                "length(\"PairPackageFingerprint\") = 64)");
        });

        builder.HasKey(result => result.Id);
        builder.Property(result => result.Id).ValueGeneratedNever();
        builder.Property(result => result.ProfiledRevision)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(result => result.State)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(result => result.SchemaVersion)
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(result => result.Fingerprint)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(result => result.ApprovalMessage)
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(result => result.ConfirmedCommunicationKinds)
            .HasConversion<int>();
        builder.Property(result => result.PreparedScanManifestFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(result => result.PreparedScanContextIdentityHash).HasMaxLength(64).IsFixedLength();
        builder.Property(result => result.ContextContentHash).HasMaxLength(64).IsFixedLength();
        builder.Property(result => result.HandoffSchemaIdentity).HasMaxLength(128);
        builder.Property(result => result.PairPackageFingerprint).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(result => new { result.RepositoryId, result.ScanRunId })
            .IsUnique();
        builder.HasOne(result => result.ScanRun)
            .WithOne(scanRun => scanRun.PreScanResult)
            .HasPrincipalKey<ScanRun>(scanRun => new
            {
                scanRun.Id,
                scanRun.RepositoryId,
            })
            .HasForeignKey<PreScanResult>(result => new
            {
                result.Id,
                result.RepositoryId,
            })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(result => result.Classifications)
            .WithOne(classification => classification.PreScanResult)
            .HasForeignKey(classification => classification.PreScanResultId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PreparedScanManifest>()
            .WithOne()
            .HasForeignKey<PreScanResult>(result => new
            {
                result.PreparedScanManifestId,
                result.ScanRunId,
                result.RepositoryId,
            })
            .HasPrincipalKey<PreparedScanManifest>(manifest => new
            {
                manifest.Id,
                manifest.ScanRunId,
                manifest.RepositoryId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanContextSnapshot>()
            .WithMany()
            .HasForeignKey(result => new
            {
                result.ContextSnapshotId,
                result.ScanRunId,
                result.RepositoryId,
            })
            .HasPrincipalKey(snapshot => new
            {
                snapshot.Id,
                snapshot.ScanRunId,
                snapshot.RepositoryId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(result => result.Classifications)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
