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

        builder.Property(r => r.GitUrl)
            .IsRequired()
            .HasMaxLength(2000);

        builder.HasOne(r => r.System)
            .WithMany(s => s.Repositories)
            .HasForeignKey(r => r.SystemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
