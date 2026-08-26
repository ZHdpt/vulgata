using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class ExactResolutionProviderEvidenceContributionConfiguration :
    IEntityTypeConfiguration<ExactResolutionProviderEvidenceContribution>
{
    public void Configure(EntityTypeBuilder<ExactResolutionProviderEvidenceContribution> builder)
    {
        builder.ToTable("ExactResolutionProviderEvidenceContributions");
        builder.HasKey(contribution => contribution.Id);
        builder.Property(contribution => contribution.Id).ValueGeneratedNever();
        builder.Property(contribution => contribution.ProfiledRevision).HasMaxLength(40).IsFixedLength();
        builder.Property(contribution => contribution.ProviderRawIdentifier).HasMaxLength(512);
        builder.Property(contribution => contribution.NormalizedEndpointIdentifier).HasMaxLength(512);
        builder.Property(contribution => contribution.EvidenceLocator).HasMaxLength(2_048);
        builder.Property(contribution => contribution.ClassificationFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(contribution => contribution.CatalogRuleIdentity).HasMaxLength(256);
        builder.Property(contribution => contribution.NormalizationRuleIdentity).HasMaxLength(128);
        builder.Property(contribution => contribution.EvidenceFingerprint).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(contribution => new
        {
            contribution.ResolutionDecisionId,
            contribution.ProviderOrdinal,
            contribution.EvidenceOrdinal,
        }).IsUnique();
        builder.HasIndex(contribution => new
        {
            contribution.ResolutionDecisionId,
            contribution.EvidenceFingerprint,
        }).IsUnique();
        builder.HasOne<CrossRepositoryResolutionDecision>()
            .WithMany()
            .HasForeignKey(contribution => contribution.ResolutionDecisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CommunicationKindClassification>()
            .WithMany()
            .HasForeignKey(contribution => contribution.CommunicationClassificationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SourceAnchor>()
            .WithMany()
            .HasForeignKey(contribution => contribution.SourceAnchorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
