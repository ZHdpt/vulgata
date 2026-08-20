using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class CodeUnitConfiguration : IEntityTypeConfiguration<CodeUnit>
{
    public void Configure(EntityTypeBuilder<CodeUnit> builder)
    {
        builder.ToTable("CodeUnits");
        builder.HasKey(unit => unit.Id);
        builder.HasAlternateKey(unit => new { unit.Id, unit.ScanRunId, unit.RepositoryId });
        builder.Property(unit => unit.Id).ValueGeneratedNever();
        builder.Property(unit => unit.FilePath).HasMaxLength(2_048);
        builder.Property(unit => unit.FileIdentityHash).HasMaxLength(64).IsFixedLength();
        builder.Property(unit => unit.FileCodeUnitIdentityFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(unit => unit.FileHash).HasMaxLength(64).IsFixedLength();
        builder.Property(unit => unit.SourceHash).HasMaxLength(64).IsFixedLength();
        builder.Property(unit => unit.SymbolName).HasMaxLength(512);
        builder.Property(unit => unit.UnitType).HasConversion<string>().HasMaxLength(32);
        builder.Property(unit => unit.RangeConfidence).HasConversion<string>().HasMaxLength(32);
        builder.Property(unit => unit.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(unit => unit.Language).HasMaxLength(64);
        builder.ToTable("CodeUnits", table =>
        {
            table.HasCheckConstraint(
                "CK_CodeUnits_PreparedFileShape",
                "(\"FileCodeUnitIdentityFingerprint\" IS NULL AND \"FileHash\" IS NULL AND " +
                "\"ByteStart\" IS NULL AND \"ByteEndExclusive\" IS NULL AND " +
                "\"SegmentOrdinal\" IS NULL AND \"SegmentCount\" IS NULL) OR " +
                "(length(\"FileCodeUnitIdentityFingerprint\") = 64 AND length(\"FileHash\") = 64 AND " +
                "\"ByteStart\" >= 0 AND \"ByteEndExclusive\" > \"ByteStart\" AND " +
                "\"ByteEndExclusive\" - \"ByteStart\" <= 65536 AND " +
                "\"SegmentOrdinal\" >= 1 AND \"SegmentCount\" >= \"SegmentOrdinal\" AND " +
                "\"UnitType\" = 'File' AND \"RangeConfidence\" = 'Exact' AND \"Status\" = 'Pending')");
        });
        builder.HasIndex(unit => new
        {
            unit.ScanRunId,
            unit.FilePath,
            unit.LineStart,
            unit.LineEnd,
            unit.SymbolName,
            unit.UnitType,
        }).IsUnique();
        builder.HasOne(unit => unit.ScanRun)
            .WithMany(scanRun => scanRun.CodeUnits)
            .HasForeignKey(unit => new { unit.ScanRunId, unit.RepositoryId })
            .HasPrincipalKey(scanRun => new { scanRun.Id, scanRun.RepositoryId })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(unit => unit.Repository)
            .WithMany()
            .HasForeignKey(unit => unit.RepositoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
