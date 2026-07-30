using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class CommunicationKindClassificationConfiguration
    : IEntityTypeConfiguration<CommunicationKindClassification>
{
    public void Configure(EntityTypeBuilder<CommunicationKindClassification> builder)
    {
        builder.ToTable("CommunicationKindClassifications", table =>
        {
            table.HasCheckConstraint(
                "CK_CommunicationKindClassifications_Kind",
                "\"Kind\" IS NULL OR \"Kind\" IN ('Rpc', 'Http')");
            table.HasCheckConstraint(
                "CK_CommunicationKindClassifications_Level",
                "\"Level\" IN ('Confirmed', 'Hint', 'Warning')");
            table.HasCheckConstraint(
                "CK_CommunicationKindClassifications_Confidence",
                "\"Confidence\" IN ('High', 'Conservative', 'None')");
            table.HasCheckConstraint(
                "CK_CommunicationKindClassifications_Rule",
                "\"Rule\" IN (" +
                "'RpcDirectAnnotation', 'RpcMatchedConfiguration', " +
                "'HttpControllerAnnotation', 'HttpClientAnnotation', " +
                "'HttpMatchedConfiguration', 'RpcImportOnly', 'HttpImportOnly', " +
                "'MalformedEvidence', 'UnsafePath', 'BoundExceeded', " +
                "'ContradictoryEvidence', 'UnsupportedFamily')");
            table.HasCheckConstraint(
                "CK_CommunicationKindClassifications_Provenance",
                "\"Provenance\" IN (" +
                "'StructuralAnnotation', 'FrameworkFact', 'PackageManifest', " +
                "'MatchedConfiguration', 'Import', 'Detector')");
            table.HasCheckConstraint(
                "CK_CommunicationKindClassifications_LevelConfidence",
                "(\"Level\" = 'Confirmed' AND \"Confidence\" = 'High' AND \"Kind\" IS NOT NULL) OR " +
                "(\"Level\" = 'Hint' AND \"Confidence\" = 'Conservative' AND \"Kind\" IS NOT NULL) OR " +
                "(\"Level\" = 'Warning' AND \"Confidence\" = 'None')");
            table.HasCheckConstraint(
                "CK_CommunicationKindClassifications_Compatibility",
                "(\"Rule\" = 'RpcDirectAnnotation' AND \"Kind\" = 'Rpc' AND " +
                "\"Level\" = 'Confirmed' AND \"Confidence\" = 'High' AND " +
                "\"Provenance\" = 'StructuralAnnotation') OR " +
                "(\"Rule\" = 'RpcMatchedConfiguration' AND \"Kind\" = 'Rpc' AND " +
                "\"Level\" = 'Confirmed' AND \"Confidence\" = 'High' AND " +
                "\"Provenance\" = 'MatchedConfiguration') OR " +
                "(\"Rule\" IN ('HttpControllerAnnotation', 'HttpClientAnnotation') AND " +
                "\"Kind\" = 'Http' AND \"Level\" = 'Confirmed' AND " +
                "\"Confidence\" = 'High' AND \"Provenance\" = 'StructuralAnnotation') OR " +
                "(\"Rule\" = 'HttpMatchedConfiguration' AND \"Kind\" = 'Http' AND " +
                "\"Level\" = 'Confirmed' AND \"Confidence\" = 'High' AND " +
                "\"Provenance\" = 'MatchedConfiguration') OR " +
                "(\"Rule\" = 'RpcImportOnly' AND \"Kind\" = 'Rpc' AND " +
                "\"Level\" = 'Hint' AND \"Confidence\" = 'Conservative' AND " +
                "\"Provenance\" IN ('Import', 'PackageManifest', 'FrameworkFact')) OR " +
                "(\"Rule\" = 'HttpImportOnly' AND \"Kind\" = 'Http' AND " +
                "\"Level\" = 'Hint' AND \"Confidence\" = 'Conservative' AND " +
                "\"Provenance\" IN ('Import', 'PackageManifest', 'FrameworkFact')) OR " +
                "(\"Rule\" IN ('MalformedEvidence', 'UnsafePath', 'BoundExceeded', " +
                "'ContradictoryEvidence') AND \"Level\" = 'Warning' AND " +
                "\"Confidence\" = 'None' AND \"Provenance\" = 'Detector') OR " +
                "(\"Rule\" = 'UnsupportedFamily' AND \"Kind\" IS NULL AND " +
                "\"Level\" = 'Warning' AND \"Confidence\" = 'None' AND " +
                "\"Provenance\" = 'Detector')");
            table.HasCheckConstraint(
                "CK_CommunicationKindClassifications_PathLocator",
                "length(\"RelativePath\") BETWEEN 1 AND 2048 AND " +
                "length(\"Locator\") BETWEEN 1 AND 4096");
            table.HasCheckConstraint(
                "CK_CommunicationKindClassifications_OccurrenceCount",
                "\"OccurrenceCount\" BETWEEN 1 AND 250000");
            table.HasCheckConstraint(
                "CK_CommunicationKindClassifications_Fingerprint_Length",
                "length(\"Fingerprint\") = 64");
        });

        builder.HasKey(classification => classification.Id);
        builder.Property(classification => classification.Id).ValueGeneratedNever();
        builder.Property(classification => classification.Kind)
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(classification => classification.Level)
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(classification => classification.Confidence)
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(classification => classification.Rule)
            .HasConversion<string>()
            .HasMaxLength(64);
        builder.Property(classification => classification.Provenance)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(classification => classification.RelativePath)
            .HasMaxLength(2_048)
            .IsRequired();
        builder.Property(classification => classification.Locator)
            .HasMaxLength(4_096)
            .IsRequired();
        builder.Property(classification => classification.Fingerprint)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(classification => new
            {
                classification.PreScanResultId,
                classification.Fingerprint,
            })
            .IsUnique();
        builder.HasIndex(classification => new
            {
                classification.PreScanResultId,
                classification.Level,
                classification.Kind,
            });
    }
}
