using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

public sealed class LlmProviderConfiguration : IEntityTypeConfiguration<LlmProvider>
{
    public void Configure(EntityTypeBuilder<LlmProvider> builder)
    {
        builder.ToTable("LlmProviders", "vulgata");

        builder.HasKey(provider => provider.Id);

        builder.Property(provider => provider.Id)
            .ValueGeneratedNever();

        builder.Property(provider => provider.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(provider => provider.NormalizedName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(provider => provider.BaseEndpointUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(provider => provider.EncryptedApiKey)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(provider => provider.SupportedApiTypes)
            .HasConversion<int>();

        builder.Property(provider => provider.DefaultAgentType)
            .HasConversion<int>();

        builder.Property(provider => provider.CreatedAt)
            .IsRequired();

        builder.Property(provider => provider.UpdatedAt)
            .IsRequired();

        builder.HasIndex(provider => provider.NormalizedName)
            .IsUnique();
    }
}
