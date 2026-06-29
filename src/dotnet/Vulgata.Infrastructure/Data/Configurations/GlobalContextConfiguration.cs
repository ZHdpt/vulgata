using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

public sealed class GlobalContextConfiguration : IEntityTypeConfiguration<GlobalContext>
{
    public void Configure(EntityTypeBuilder<GlobalContext> builder)
    {
        builder.ToTable("GlobalContexts");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .ValueGeneratedNever();

        builder.Property(g => g.Context)
            .HasMaxLength(10000);
    }
}
