using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

public sealed class PendingContextChangeConfiguration : IEntityTypeConfiguration<PendingContextChange>
{
    public void Configure(EntityTypeBuilder<PendingContextChange> builder)
    {
        builder.ToTable("PendingContextChanges");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.ScopeType)
            .IsRequired();

        builder.Property(p => p.ScopeKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Context)
            .HasMaxLength(10000);

        builder.HasIndex(p => new { p.ScopeType, p.ScopeKey })
            .IsUnique();
    }
}
