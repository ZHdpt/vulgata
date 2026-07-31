using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class FlowTraceStepConfiguration : IEntityTypeConfiguration<FlowTraceStep>
{
    public void Configure(EntityTypeBuilder<FlowTraceStep> builder)
    {
        builder.ToTable("FlowTraceSteps", table =>
        {
            table.HasCheckConstraint("CK_FlowTraceSteps_StepOrder_Positive", "\"StepOrder\" > 0");
            table.HasCheckConstraint(
                "CK_FlowTraceSteps_StepKind_Closed",
                "\"StepKind\" IN ('Document', 'Relationship', 'Termination')");
        });
        builder.HasKey(step => step.Id);
        builder.Property(step => step.Id).ValueGeneratedNever();
        builder.Property(step => step.StepKind).HasConversion<string>().HasMaxLength(32);
        builder.Property(step => step.Description).HasMaxLength(4_096);
        builder.HasIndex(step => new { step.FlowTraceId, step.StepOrder }).IsUnique();
        builder.HasIndex(step => new { step.DocumentRepositoryId, step.DocumentVersionId });
        builder.HasOne(step => step.FlowTrace)
            .WithMany(trace => trace.Steps)
            .HasForeignKey(step => step.FlowTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(step => step.DocumentVersion)
            .WithMany()
            .HasForeignKey(step => new
            {
                step.DocumentVersionId,
                step.DocumentRepositoryId,
                step.DocumentScanRunId,
            })
            .HasPrincipalKey(version => new
            {
                version.Id,
                version.RepositoryId,
                version.ScanRunId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(step => step.Edge)
            .WithMany()
            .HasForeignKey(step => step.EdgeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
