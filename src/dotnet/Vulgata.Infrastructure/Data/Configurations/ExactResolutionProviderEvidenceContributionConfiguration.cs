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
        builder.HasOne<SourceAnchor>()
            .WithMany()
            .HasForeignKey(contribution => contribution.SourceAnchorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
