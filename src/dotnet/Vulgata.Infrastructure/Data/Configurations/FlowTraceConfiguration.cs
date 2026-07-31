using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class FlowTraceConfiguration : IEntityTypeConfiguration<FlowTrace>
{
    public void Configure(EntityTypeBuilder<FlowTrace> builder)
    {
        builder.ToTable("FlowTraces", table =>
            table.HasCheckConstraint("CK_FlowTraces_TraceOrder_Positive", "\"TraceOrder\" > 0"));
        builder.HasKey(trace => trace.Id);
        builder.Property(trace => trace.Id).ValueGeneratedNever();
        builder.HasIndex(trace => new { trace.FlowGroupId, trace.TraceOrder }).IsUnique();
        builder.HasOne(trace => trace.FlowGroup)
            .WithMany(group => group.Traces)
            .HasForeignKey(trace => trace.FlowGroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
