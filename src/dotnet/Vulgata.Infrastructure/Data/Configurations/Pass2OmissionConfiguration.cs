using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class Pass2OmissionConfiguration : IEntityTypeConfiguration<Pass2Omission>
{
    public void Configure(EntityTypeBuilder<Pass2Omission> builder)
    {
        builder.ToTable("Pass2Omissions");
        builder.HasKey(omission => omission.Id);
        builder.Property(omission => omission.Id).ValueGeneratedNever();
        builder.Property(omission => omission.State).HasConversion<string>().HasMaxLength(32);
        builder.Property(omission => omission.ReasonCode).HasMaxLength(64);
        builder.HasIndex(omission => new
        {
            omission.Pass2ExecutionId,
            omission.ExactResolutionDecisionId,
        }).IsUnique();
        builder.HasOne<Pass2Execution>()
            .WithMany()
            .HasForeignKey(omission => omission.Pass2ExecutionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CrossRepositoryResolutionDecision>()
            .WithMany()
            .HasForeignKey(omission => omission.ExactResolutionDecisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
