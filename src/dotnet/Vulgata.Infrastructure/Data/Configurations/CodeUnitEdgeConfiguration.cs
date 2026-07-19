using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class CodeUnitEdgeConfiguration : IEntityTypeConfiguration<CodeUnitEdge>
{
    public void Configure(EntityTypeBuilder<CodeUnitEdge> builder)
    {
        builder.ToTable("CodeUnitEdges");
        builder.HasKey(edge => edge.Id);
        builder.Property(edge => edge.Id).ValueGeneratedNever();
        builder.Property(edge => edge.EdgeType).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(edge => new { edge.SourceCodeUnitId, edge.TargetCodeUnitId, edge.EdgeType }).IsUnique();
        builder.HasOne(edge => edge.ScanRun)
            .WithMany(scanRun => scanRun.CodeUnitEdges)
            .HasForeignKey(edge => new { edge.ScanRunId, edge.RepositoryId })
            .HasPrincipalKey(scanRun => new { scanRun.Id, scanRun.RepositoryId })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(edge => edge.SourceCodeUnit)
            .WithMany()
            .HasForeignKey(edge => new { edge.SourceCodeUnitId, edge.ScanRunId, edge.RepositoryId })
            .HasPrincipalKey(unit => new { unit.Id, unit.ScanRunId, unit.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(edge => edge.TargetCodeUnit)
            .WithMany()
            .HasForeignKey(edge => new { edge.TargetCodeUnitId, edge.ScanRunId, edge.RepositoryId })
            .HasPrincipalKey(unit => new { unit.Id, unit.ScanRunId, unit.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
