using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Infrastructure.Data.Configurations;

public sealed class SystemConfiguration : IEntityTypeConfiguration<SystemEntity>
{
    public void Configure(EntityTypeBuilder<SystemEntity> builder)
    {
        builder.ToTable("Systems");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.NormalizedName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .HasMaxLength(2000);

        builder.Property(s => s.Context)
            .HasMaxLength(10000);

        builder.HasIndex(s => s.NormalizedName)
            .IsUnique();

        builder.HasMany(s => s.Repositories)
            .WithOne(r => r.System)
            .HasForeignKey(r => r.SystemId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.OwnerAssignments)
            .WithOne(a => a.System)
            .HasForeignKey(a => a.SystemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
