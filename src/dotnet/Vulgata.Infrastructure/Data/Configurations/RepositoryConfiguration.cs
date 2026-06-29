using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Infrastructure.Data.Configurations;

public sealed class RepositoryConfiguration : IEntityTypeConfiguration<RepositoryEntity>
{
    public void Configure(EntityTypeBuilder<RepositoryEntity> builder)
    {
        builder.ToTable("Repositories");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.NormalizedName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.GitUrl)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(r => r.Description)
            .HasMaxLength(2000);

        builder.Property(r => r.Context)
            .HasMaxLength(10000);

        builder.Property(r => r.SystemId)
            .IsRequired(false);

        builder.HasIndex(r => new { r.SystemId, r.NormalizedName })
            .IsUnique();

        builder.HasIndex(r => r.NormalizedName)
            .HasDatabaseName("IX_Repositories_Standalone_NormalizedName")
            .IsUnique()
            .HasFilter("\"SystemId\" IS NULL");

        builder.HasOne(r => r.System)
            .WithMany(s => s.Repositories)
            .HasForeignKey(r => r.SystemId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
