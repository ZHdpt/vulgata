using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

public sealed class SystemOwnerAssignmentConfiguration : IEntityTypeConfiguration<SystemOwnerAssignment>
{
    public void Configure(EntityTypeBuilder<SystemOwnerAssignment> builder)
    {
        builder.ToTable("SystemOwnerAssignments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasOne(a => a.System)
            .WithMany(s => s.OwnerAssignments)
            .HasForeignKey(a => a.SystemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.SystemId, a.UserId })
            .IsUnique();
    }
}
