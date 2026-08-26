using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class ExactCrossRepositoryResolutionDecisionConfiguration :
    IEntityTypeConfiguration<CrossRepositoryResolutionDecision>
{
    public void Configure(EntityTypeBuilder<CrossRepositoryResolutionDecision> builder)
    {
        builder.ToTable("CrossRepositoryResolutionDecisions");
        builder.HasKey(decision => decision.Id);
        builder.Property(decision => decision.Id).ValueGeneratedNever();
        builder.Property(decision => decision.Protocol).HasConversion<string>().HasMaxLength(32);
        builder.Property(decision => decision.ConsumerEntryIdentity).HasMaxLength(512);
        builder.Property(decision => decision.ConsumerEntryStableKey).HasMaxLength(512);
        builder.Property(decision => decision.NormalizedEndpointIdentifier).HasMaxLength(512);
        builder.Property(decision => decision.Outcome).HasMaxLength(64);
        builder.Property(decision => decision.EvidenceFingerprint).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(decision => decision.EvidenceFingerprint).IsUnique();
        builder.HasIndex(decision => decision.UncertaintyId).IsUnique();
        builder.HasOne<DocumentEdge>()
            .WithMany()
            .HasForeignKey(decision => decision.DocumentEdgeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uncertainty>()
            .WithOne()
            .HasForeignKey<CrossRepositoryResolutionDecision>(decision => decision.UncertaintyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
