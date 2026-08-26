using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class FlowGroupConfiguration : IEntityTypeConfiguration<FlowGroup>
{
    public void Configure(EntityTypeBuilder<FlowGroup> builder)
    {
        builder.ToTable("FlowGroups", table =>
        {
            table.HasCheckConstraint("CK_FlowGroups_GroupOrder_Positive", "\"GroupOrder\" > 0");
            table.HasCheckConstraint(
                "CK_FlowGroups_SchemaIdentity_Closed",
                "\"SchemaIdentity\" IS NULL OR \"SchemaIdentity\" = 'endpoint-flow-group/1.0'");
            table.HasCheckConstraint(
                "CK_FlowGroups_Protocol_Closed",
                "\"Protocol\" IS NULL OR \"Protocol\" IN ('CustomRpc', 'Http')");
            table.HasCheckConstraint(
                "CK_FlowGroups_State_Closed",
                "\"SchemaIdentity\" IS NULL OR \"State\" IN ('Complete', 'Incomplete', 'BoundExcluded', 'Running', 'Published', 'Failed')");
            table.HasCheckConstraint(
                "CK_FlowGroups_LogicalKeySchemaIdentity_Closed",
                "\"LogicalKeySchemaIdentity\" IS NULL OR \"LogicalKeySchemaIdentity\" = 'business-flow-logical-key/1.0'");
            table.HasCheckConstraint(
                "CK_FlowGroups_Fingerprint_Lengths",
                "(\"PlanFingerprint\" = '' OR length(\"PlanFingerprint\") = 64) AND (\"MembershipFingerprint\" IS NULL OR length(\"MembershipFingerprint\") = 64) AND (\"ConsumerEntryStableKey\" IS NULL OR length(\"ConsumerEntryStableKey\") = 64)");
            table.HasCheckConstraint(
                "CK_FlowGroups_ProviderRepository_CrossRepository",
                "\"ProviderRepositoryId\" IS NULL OR \"ProviderRepositoryId\" <> \"RepositoryId\"");
        });
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Id).ValueGeneratedNever();
        builder.Property(group => group.SchemaIdentity).HasMaxLength(128);
        builder.Property(group => group.Protocol).HasConversion<string>().HasMaxLength(32);
        builder.Property(group => group.NormalizedEndpointIdentifier).HasMaxLength(512);
        builder.Property(group => group.ConsumerEntryIdentity).HasMaxLength(512);
        builder.Property(group => group.ConsumerEntryStableKey).HasMaxLength(64).IsFixedLength();
        builder.Property(group => group.LogicalKeySchemaIdentity).HasMaxLength(128);
        builder.Property(group => group.Title).HasMaxLength(4_096);
        builder.Property(group => group.LogicalDocumentStableKey).HasMaxLength(512);
        builder.Property(group => group.State).HasMaxLength(32);
        builder.Property(group => group.PlanFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(group => group.MembershipFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(group => group.ReasonCode).HasMaxLength(64);
        builder.HasIndex(group => new
        {
            group.BusinessLogicDocumentVersionId,
            group.GroupOrder,
        }).IsUnique();
        builder.HasIndex(group => new { group.RepositoryId, group.BusinessLogicDocumentVersionId });
        builder.HasIndex(group => new { group.Pass2ExecutionId, group.GroupOrder }).IsUnique();
        builder.HasIndex(group => new { group.Pass2ExecutionId, group.PlanFingerprint }).IsUnique();
        builder.HasIndex(group => group.ExactResolutionDecisionId).IsUnique();
        builder.HasIndex(group => group.ExactResolutionEdgeId);
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
        builder.HasOne<Pass2Execution>()
            .WithMany()
            .HasForeignKey(group => group.Pass2ExecutionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SystemEntity>()
            .WithMany()
            .HasForeignKey(group => group.SystemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentVersion>()
            .WithMany()
            .HasForeignKey(group => new
            {
                group.ConsumerDocumentVersionId,
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
        builder.HasOne<Repository>()
            .WithMany()
            .HasForeignKey(group => group.ProviderRepositoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentVersion>()
            .WithMany()
            .HasForeignKey(group => group.ProviderXmlDocumentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentVersion>()
            .WithMany()
            .HasForeignKey(group => group.ProviderImplementationDocumentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CrossRepositoryResolutionDecision>()
            .WithOne()
            .HasForeignKey<FlowGroup>(group => group.ExactResolutionDecisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentEdge>()
            .WithMany()
            .HasForeignKey(group => group.ExactResolutionEdgeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
