using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class UnresolvedStructuralRelationshipCandidateConfiguration
    : IEntityTypeConfiguration<UnresolvedStructuralRelationshipCandidate>
{
    public void Configure(EntityTypeBuilder<UnresolvedStructuralRelationshipCandidate> builder)
    {
        builder.ToTable("UnresolvedStructuralRelationshipCandidates");
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.Id).ValueGeneratedNever();
        builder.Property(candidate => candidate.RelationshipKind).HasConversion<string>().HasMaxLength(32);
        builder.Property(candidate => candidate.ProviderTargetEndpointIdentity).HasMaxLength(2_048);
        builder.HasIndex(candidate => new
        {
            candidate.SourceCodeUnitId,
            candidate.RelationshipKind,
            candidate.ProviderTargetEndpointIdentity,
        }).IsUnique();
        builder.HasOne(candidate => candidate.ScanRun)
            .WithMany(scanRun => scanRun.UnresolvedRelationships)
            .HasForeignKey(candidate => new { candidate.ScanRunId, candidate.RepositoryId })
            .HasPrincipalKey(scanRun => new { scanRun.Id, scanRun.RepositoryId })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(candidate => candidate.SourceCodeUnit)
            .WithMany()
            .HasForeignKey(candidate => new
            {
                candidate.SourceCodeUnitId,
                candidate.ScanRunId,
                candidate.RepositoryId,
            })
            .HasPrincipalKey(unit => new { unit.Id, unit.ScanRunId, unit.RepositoryId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
