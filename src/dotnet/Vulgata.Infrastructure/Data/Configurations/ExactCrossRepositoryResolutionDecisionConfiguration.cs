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
        builder.Property(decision => decision.ResolutionSchemaIdentity).HasMaxLength(128).IsRequired();
        builder.Property(decision => decision.Protocol).HasConversion<string>().HasMaxLength(32);
        builder.Property(decision => decision.ConsumerEntryIdentity).HasMaxLength(512);
        builder.Property(decision => decision.ConsumerEntryStableKey).HasMaxLength(512);
        builder.Property(decision => decision.ConsumerProfiledRevision).HasMaxLength(40).IsFixedLength();
        builder.Property(decision => decision.NormalizedEndpointIdentifier).HasMaxLength(512);
        builder.Property(decision => decision.NormalizationRuleIdentity).HasMaxLength(128).IsRequired();
        builder.Property(decision => decision.Outcome).HasMaxLength(64);
        builder.Property(decision => decision.OutcomeKind).HasMaxLength(32).IsRequired();
        builder.Property(decision => decision.ReasonCode).HasMaxLength(64).IsRequired();
        builder.Property(decision => decision.ProviderProfiledRevision).HasMaxLength(40).IsFixedLength();
        builder.Property(decision => decision.EvidenceFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(decision => decision.ConsumerEvidenceFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(decision => decision.ProviderEvidenceSetFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(decision => decision.DecisionFingerprint).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(decision => decision.DecisionFingerprint).IsUnique();
        builder.HasIndex(decision => new
        {
            decision.Pass1PairExecutionId,
            decision.ConsumerCandidateId,
            decision.Protocol,
            decision.NormalizedEndpointIdentifier,
        }).IsUnique();
        builder.HasIndex(decision => decision.UncertaintyId).IsUnique();
        builder.HasIndex(decision => decision.ResolvedUncertaintyId).IsUnique();
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
