using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

public sealed class SystemLlmProviderOverrideConfiguration : IEntityTypeConfiguration<SystemLlmProviderOverride>
{
    public void Configure(EntityTypeBuilder<SystemLlmProviderOverride> builder)
    {
        builder.ToTable("SystemLlmProviderOverrides");

        builder.HasKey(overrideEntry => overrideEntry.Id);

        builder.Property(overrideEntry => overrideEntry.Id)
            .ValueGeneratedNever();

        builder.Property(overrideEntry => overrideEntry.SystemId)
            .IsRequired();

        builder.Property(overrideEntry => overrideEntry.LlmProviderId)
            .IsRequired();

        builder.Property(overrideEntry => overrideEntry.AgentType)
            .HasConversion<int>();

        builder.Property(overrideEntry => overrideEntry.CreatedAt)
            .IsRequired();

        builder.Property(overrideEntry => overrideEntry.UpdatedAt)
            .IsRequired();

        builder.HasOne(overrideEntry => overrideEntry.System)
            .WithMany(system => system.LlmProviderOverrides)
            .HasForeignKey(overrideEntry => overrideEntry.SystemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(overrideEntry => overrideEntry.LlmProvider)
            .WithMany(provider => provider.SystemLlmProviderOverrides)
            .HasForeignKey(overrideEntry => overrideEntry.LlmProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(overrideEntry => new { overrideEntry.SystemId, overrideEntry.AgentType })
            .IsUnique();
    }
}
