using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class FlowGroupConfiguration : IEntityTypeConfiguration<FlowGroup>
{
    public void Configure(EntityTypeBuilder<FlowGroup> builder)
    {
        builder.ToTable("FlowGroups", table =>
            table.HasCheckConstraint("CK_FlowGroups_GroupOrder_Positive", "\"GroupOrder\" > 0"));
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Id).ValueGeneratedNever();
        builder.Property(group => group.Title).HasMaxLength(4_096);
        builder.HasIndex(group => new
        {
            group.BusinessLogicDocumentVersionId,
            group.GroupOrder,
        }).IsUnique();
        builder.HasIndex(group => new { group.RepositoryId, group.BusinessLogicDocumentVersionId });
        builder.HasOne(group => group.BusinessLogicDocumentVersion)
            .WithMany(version => version.FlowGroups)
            .HasForeignKey(group => new
            {
                group.BusinessLogicDocumentVersionId,
                group.RepositoryId,
                group.ScanRunId,
            })
            .HasPrincipalKey(version => new
            {
                version.Id,
                version.RepositoryId,
                version.ScanRunId,
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
