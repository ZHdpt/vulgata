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
        builder.Property(unit => unit.SourceHash).HasMaxLength(64).IsFixedLength();
        builder.Property(unit => unit.SymbolName).HasMaxLength(512);
        builder.Property(unit => unit.UnitType).HasConversion<string>().HasMaxLength(32);
        builder.Property(unit => unit.RangeConfidence).HasConversion<string>().HasMaxLength(32);
        builder.Property(unit => unit.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(unit => unit.Language).HasMaxLength(64);
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
