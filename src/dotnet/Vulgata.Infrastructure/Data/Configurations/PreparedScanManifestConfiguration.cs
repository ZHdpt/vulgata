using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class PreparedScanManifestConfiguration : IEntityTypeConfiguration<PreparedScanManifest>
{
    public void Configure(EntityTypeBuilder<PreparedScanManifest> builder)
    {
        builder.ToTable("PreparedScanManifests", table =>
        {
            table.HasCheckConstraint("CK_PreparedScanManifests_SchemaIdentity", "\"SchemaIdentity\" = 'prepared-scan-manifest/1.0'");
            table.HasCheckConstraint("CK_PreparedScanManifests_ApprovalState", "\"ApprovalState\" = 'Approved'");
            table.HasCheckConstraint("CK_PreparedScanManifests_ProfiledRevision", "length(\"ProfiledRevision\") = 40");
            table.HasCheckConstraint("CK_PreparedScanManifests_Hashes", "length(\"SnapshotIdentity\") = 64 AND length(\"RuntimeIdentityHash\") = 64 AND length(\"CodeGraphProofIdentityHash\") = 64 AND length(\"JavaReadinessIdentityHash\") = 64 AND length(\"CatalogHash\") = 64 AND length(\"PreparedScanContextIdentityHash\") = 64 AND length(\"ContextContentHash\") = 64 AND length(\"PairSelectionFingerprint\") = 64 AND length(\"Fingerprint\") = 64");
            table.HasCheckConstraint("CK_PreparedScanManifests_Counts", "\"CandidateCount\" >= 0 AND \"EligibleCount\" >= 0 AND \"FilteredCount\" >= 0 AND \"SkippedCount\" >= 0 AND \"SelectedCount\" >= 0 AND \"TotalConfirmedEvidenceCount\" >= 0 AND \"TotalHintCount\" >= 0 AND \"TotalWarningCount\" >= 0 AND \"RetainedClassificationCount\" BETWEEN 0 AND 256");
            table.HasCheckConstraint("CK_PreparedScanManifests_Durations", "\"CodeGraphObservedDurationMilliseconds\" >= 0 AND \"JavaReadinessObservedDurationMilliseconds\" >= 0");
            table.HasCheckConstraint("CK_PreparedScanManifests_ProofCounts", "\"CodeGraphPendingAdded\" >= 0 AND \"CodeGraphPendingModified\" >= 0 AND \"CodeGraphPendingRemoved\" >= 0 AND \"CodeGraphPendingReferences\" >= 0 AND \"CodeGraphIndexedFileCount\" >= 0 AND \"JavaReadinessDocumentSymbolCount\" >= 0");
        });
        builder.HasKey(manifest => manifest.Id);
        builder.HasAlternateKey(manifest => new { manifest.Id, manifest.CoordinationId });
        builder.HasAlternateKey(manifest => new
        {
            manifest.Id,
            manifest.ScanRunId,
            manifest.RepositoryId,
        });
        builder.Property(manifest => manifest.Id).ValueGeneratedNever();
        builder.Property(manifest => manifest.ProfiledRevision).HasMaxLength(40).IsFixedLength().IsRequired();
        builder.Property(manifest => manifest.RequestedRef).HasMaxLength(1_024).IsRequired();
        builder.Property(manifest => manifest.SchemaIdentity).HasMaxLength(128).IsRequired();
        foreach (string propertyName in new[] { nameof(PreparedScanManifest.SnapshotIdentity), nameof(PreparedScanManifest.RuntimeIdentityHash), nameof(PreparedScanManifest.CodeGraphProofIdentityHash), nameof(PreparedScanManifest.JavaReadinessIdentityHash), nameof(PreparedScanManifest.CatalogHash), nameof(PreparedScanManifest.PreparedScanContextIdentityHash), nameof(PreparedScanManifest.ContextContentHash), nameof(PreparedScanManifest.PairSelectionFingerprint), nameof(PreparedScanManifest.Fingerprint) })
            builder.Property<string>(propertyName).HasMaxLength(64).IsFixedLength().IsRequired();
        foreach (string propertyName in new[]
        {
            nameof(PreparedScanManifest.CodeGraphSnapshotIdentity),
            nameof(PreparedScanManifest.CodeGraphSnapshotRootBindingHash),
            nameof(PreparedScanManifest.CodeGraphRuntimeIdentityHash),
            nameof(PreparedScanManifest.JavaReadinessSnapshotIdentity),
            nameof(PreparedScanManifest.JavaReadinessRuntimeIdentityHash),
            nameof(PreparedScanManifest.JavaReadinessWorkspaceBindingHash),
            nameof(PreparedScanManifest.JavaReadinessJdtLsDistributionArchiveHash),
        })
            builder.Property<string>(propertyName).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(manifest => manifest.CodeGraphProfiledRevision).HasMaxLength(40).IsFixedLength().IsRequired();
        builder.Property(manifest => manifest.JavaReadinessProfiledRevision).HasMaxLength(40).IsFixedLength().IsRequired();
        builder.Property(manifest => manifest.CodeGraphIndexIdentity).HasMaxLength(1_024).IsRequired();
        builder.Property(manifest => manifest.CodeGraphProofSchemaIdentity).HasMaxLength(128).IsRequired();
        builder.Property(manifest => manifest.CodeGraphIndexState).HasConversion<string>().HasMaxLength(32);
        builder.Property(manifest => manifest.CodeGraphLogicalProjectBinding).HasMaxLength(2_048).IsRequired();
        builder.Property(manifest => manifest.CodeGraphLogicalIndexBinding).HasMaxLength(2_048).IsRequired();
        builder.Property(manifest => manifest.JavaReadinessSchemaIdentity).HasMaxLength(128).IsRequired();
        builder.Property(manifest => manifest.JavaReadinessOutcome).HasConversion<string>().HasMaxLength(32);
        builder.Property(manifest => manifest.JavaReadinessJdtLsRelease).HasMaxLength(128).IsRequired();
        builder.Property(manifest => manifest.JavaReadinessStreamJsonRpcVersion).HasMaxLength(64).IsRequired();
        builder.Property(manifest => manifest.JavaReadinessTransport).HasConversion<string>().HasMaxLength(32);
        builder.Property(manifest => manifest.JavaReadinessFile).HasMaxLength(4_096);
        builder.Property(manifest => manifest.JavaReadinessImportState).HasConversion<string>().HasMaxLength(32);
        builder.Property(manifest => manifest.CatalogSchemaIdentity).HasMaxLength(128).IsRequired();
        builder.Property(manifest => manifest.ApprovalState).HasConversion<string>().HasMaxLength(32);
        builder.Property(manifest => manifest.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(manifest => manifest.ApprovedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(manifest => new { manifest.RepositoryId, manifest.ScanRunId }).IsUnique();
        builder.HasIndex(manifest => manifest.Fingerprint).IsUnique();
        builder.HasIndex(manifest => new { manifest.CoordinationId, manifest.RepositoryId }).IsUnique();
        builder.HasOne(manifest => manifest.ScanRun).WithMany().HasForeignKey(manifest => new { manifest.ScanRunId, manifest.RepositoryId }).HasPrincipalKey(run => new { run.Id, run.RepositoryId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TwoRepositoryScanCoordination>().WithMany().HasForeignKey(manifest => manifest.CoordinationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanContextSnapshot>()
            .WithMany()
            .HasForeignKey(manifest => new
            {
                manifest.ContextSnapshotId,
                manifest.ScanRunId,
                manifest.RepositoryId,
            })
            .HasPrincipalKey(snapshot => new
            {
                snapshot.Id,
                snapshot.ScanRunId,
                snapshot.RepositoryId,
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
