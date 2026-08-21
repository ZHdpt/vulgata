using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class Pass1ConcurrencyChangeConfiguration : IEntityTypeConfiguration<Pass1ConcurrencyChange>
{
    public void Configure(EntityTypeBuilder<Pass1ConcurrencyChange> builder)
    {
        builder.ToTable("Pass1ConcurrencyChanges", table => table.HasCheckConstraint("CK_Pass1ConcurrencyChanges_Shape", "\"ExpectedStateVersion\" >= 0 AND \"ResultingStateVersion\" = \"ExpectedStateVersion\" + 1 AND \"PreviousLimit\" BETWEEN 1 AND 4 AND \"EffectiveLimit\" BETWEEN 1 AND 4 AND \"PreviousLimit\" <> \"EffectiveLimit\" AND \"ConcurrencyRevision\" > 0"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.ChangedByPrincipalId).HasMaxLength(256);
        builder.Property(value => value.ChangeFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.ChangedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(value => new { value.PairExecutionId, value.ExpectedStateVersion }).IsUnique();
        builder.HasIndex(value => new { value.PairExecutionId, value.ConcurrencyRevision }).IsUnique();
        builder.HasOne<Pass1PairExecution>().WithMany().HasForeignKey(value => value.PairExecutionId).OnDelete(DeleteBehavior.Restrict);
    }
}
