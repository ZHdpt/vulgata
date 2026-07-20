using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class CodeUnitFrameworkConfiguration : IEntityTypeConfiguration<CodeUnitFramework>
{
    public void Configure(EntityTypeBuilder<CodeUnitFramework> builder)
    {
        builder.ToTable(
            "CodeUnitFrameworks",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_CodeUnitFrameworks_FrameworkName",
                    "length(\"FrameworkName\") BETWEEN 1 AND 64");
                table.HasCheckConstraint(
                    "CK_CodeUnitFrameworks_Confidence",
                    "\"Confidence\" IN ('High', 'Medium')");
            });
        builder.HasKey(framework => framework.Id);
        builder.Property(framework => framework.Id).ValueGeneratedNever();
        builder.Property(framework => framework.FrameworkName).HasMaxLength(64);
        builder.Property(framework => framework.Confidence).HasConversion<string>().HasMaxLength(16);
        builder.Property(framework => framework.DetectionSource).HasMaxLength(4_096);
        builder.HasIndex(framework => new { framework.CodeUnitId, framework.FrameworkName }).IsUnique();
        builder.HasIndex(framework => new { framework.ScanRunId, framework.FrameworkName });
        builder.HasOne(framework => framework.CodeUnit)
            .WithMany(codeUnit => codeUnit.Frameworks)
            .HasForeignKey(framework => new
            {
                framework.CodeUnitId,
                framework.ScanRunId,
                framework.RepositoryId,
            })
            .HasPrincipalKey(codeUnit => new
            {
                codeUnit.Id,
                codeUnit.ScanRunId,
                codeUnit.RepositoryId,
            })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(framework => framework.ScanRun)
            .WithMany(scanRun => scanRun.CodeUnitFrameworks)
            .HasForeignKey(framework => new { framework.ScanRunId, framework.RepositoryId })
            .HasPrincipalKey(scanRun => new { scanRun.Id, scanRun.RepositoryId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
