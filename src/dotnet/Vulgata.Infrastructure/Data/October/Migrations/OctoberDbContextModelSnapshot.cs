using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Vulgata.Infrastructure.Data.October.Migrations;

[DbContext(typeof(OctoberDbContext))]
public sealed class OctoberDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("october")
            .HasAnnotation("ProductVersion", "10.0.9")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("Vulgata.Infrastructure.Data.October.OctoberBatchRecord", b =>
        {
            b.Property<Guid>("BatchId").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<int>("SelectionSlot").HasColumnType("integer");
            b.Property<string>("TargetId").IsRequired().HasColumnType("text");
            b.Property<string>("SubjectId").IsRequired().HasColumnType("text");
            b.Property<string>("CanonicalDatabaseIdentity").IsRequired().HasColumnType("text");
            b.Property<string>("SelectionDigest").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property<string>("ManagementAuthorityId").IsRequired().HasColumnType("text");
            b.Property<DateTimeOffset>("SelectedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<int>("State").HasColumnType("integer");
            b.HasKey("BatchId");
            b.HasIndex("SelectionSlot").IsUnique();
            b.ToTable("OctoberBatches", "october", table =>
                table.HasCheckConstraint("CK_OctoberBatches_State", "\"State\" IN (0, 1)"));
        });

        modelBuilder.Entity("Vulgata.Infrastructure.Data.October.OctoberRepositoryRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("RepositoryId").HasColumnType("uuid");
            b.Property<Guid?>("SystemId").HasColumnType("uuid");
            b.Property<string>("RootBindingIdentity").IsRequired().HasColumnType("text");
            b.Property<string>("OperatorLabel").IsRequired().HasColumnType("text");
            b.HasKey("BatchId", "RepositoryId");
            b.ToTable("OctoberRepositorySelections", "october");
        });

        modelBuilder.Entity("Vulgata.Infrastructure.Data.October.OctoberContextReferenceRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<int>("Scope").HasColumnType("integer");
            b.Property<Guid>("RecordId").HasColumnType("uuid");
            b.HasKey("BatchId", "Scope", "RecordId");
            b.ToTable("OctoberContextReferences", "october");
        });

        modelBuilder.Entity("Vulgata.Infrastructure.Data.October.OctoberAdvisoryEntryRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("RepositoryId").HasColumnType("uuid");
            b.Property<int>("Scope").HasColumnType("integer");
            b.Property<int>("Applicability").HasColumnType("integer");
            b.Property<Guid?>("OriginRecordId").HasColumnType("uuid");
            b.Property<string>("Value").HasColumnType("text");
            b.Property<int>("EvidenceKind").HasColumnType("integer");
            b.HasKey("BatchId", "RepositoryId", "Scope");
            b.ToTable("OctoberAdvisoryEntries", "october");
        });

        modelBuilder.Entity("Vulgata.Infrastructure.Data.October.OctoberContextDifferenceRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("RepositoryId").HasColumnType("uuid");
            b.Property<int>("LeftScope").HasColumnType("integer");
            b.Property<int>("RightScope").HasColumnType("integer");
            b.HasKey("BatchId", "RepositoryId", "LeftScope", "RightScope");
            b.ToTable("OctoberContextDifferences", "october");
        });

        modelBuilder.Entity("Vulgata.Infrastructure.Data.October.OctoberPreparedSourceApprovalRecord", b =>
        {
            b.Property<string>("ApprovalIdentity").IsRequired().HasColumnType("text");
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("RepositoryId").HasColumnType("uuid");
            b.Property<string>("SubjectId").IsRequired().HasColumnType("text");
            b.Property<int>("Purpose").HasColumnType("integer");
            b.Property<string>("RootBindingIdentity").IsRequired().HasColumnType("text");
            b.Property<string>("OperatorLabel").IsRequired().HasColumnType("text");
            b.Property<DateTimeOffset>("ExpiresAtUtc").HasColumnType("timestamp with time zone");
            b.Property<bool>("IsRevoked").HasColumnType("boolean");
            b.HasKey("ApprovalIdentity");
            b.HasIndex("BatchId", "RepositoryId");
            b.ToTable("OctoberPreparedSourceApprovals", "october", table =>
                table.HasCheckConstraint("CK_OctoberPreparedSourceApprovals_Purpose", "\"Purpose\" = 0"));
        });

        modelBuilder.Entity("Vulgata.Infrastructure.Data.October.OctoberRepositoryRecord", b =>
        {
            b.HasOne("Vulgata.Infrastructure.Data.October.OctoberBatchRecord", null).WithMany()
                .HasForeignKey("BatchId").OnDelete(DeleteBehavior.Cascade).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Infrastructure.Data.October.OctoberContextReferenceRecord", b =>
        {
            b.HasOne("Vulgata.Infrastructure.Data.October.OctoberBatchRecord", null).WithMany()
                .HasForeignKey("BatchId").OnDelete(DeleteBehavior.Cascade).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Infrastructure.Data.October.OctoberAdvisoryEntryRecord", b =>
        {
            b.HasOne("Vulgata.Infrastructure.Data.October.OctoberRepositoryRecord", null).WithMany()
                .HasForeignKey("BatchId", "RepositoryId").OnDelete(DeleteBehavior.Cascade).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Infrastructure.Data.October.OctoberContextDifferenceRecord", b =>
        {
            b.HasOne("Vulgata.Infrastructure.Data.October.OctoberRepositoryRecord", null).WithMany()
                .HasForeignKey("BatchId", "RepositoryId").OnDelete(DeleteBehavior.Cascade).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Infrastructure.Data.October.OctoberPreparedSourceApprovalRecord", b =>
        {
            b.HasOne("Vulgata.Infrastructure.Data.October.OctoberRepositoryRecord", null).WithMany()
                .HasForeignKey("BatchId", "RepositoryId").OnDelete(DeleteBehavior.Cascade).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.KnowledgeOperation", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<int>("Kind").HasColumnType("integer");
            b.Property<Guid>("OperationId").HasColumnType("uuid");
            b.Property<string>("PayloadDigest").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property<string>("CommittedResultJson").IsRequired().HasColumnType("jsonb");
            b.Property<string>("ContributorsJson").IsRequired().HasColumnType("jsonb");
            b.Property<DateTimeOffset>("CommittedAtUtc").HasColumnType("timestamp with time zone");
            b.HasKey("BatchId", "Kind", "OperationId");
            b.ToTable("KnowledgeOperations", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.AcceptedFileManifest", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("ManifestId").HasColumnType("uuid");
            b.Property<Guid>("AdmissionId").HasColumnType("uuid");
            b.Property<Guid>("RepositoryId").HasColumnType("uuid");
            b.Property<string>("RootBindingIdentity").IsRequired().HasColumnType("text");
            b.Property<string>("RelativePath").IsRequired().HasColumnType("text");
            b.Property<string>("SourceDigest").IsRequired().HasColumnType("text");
            b.Property<string>("PreparedVersion").IsRequired().HasColumnType("text");
            b.Property<string>("ProposalDigest").IsRequired().HasColumnType("text");
            b.Property<int>("CoverageDisposition").HasColumnType("integer");
            b.Property<string>("CoverageReason").HasColumnType("text");
            b.Property<int>("UnitCount").HasColumnType("integer");
            b.Property<int>("OperationKind").HasColumnType("integer");
            b.Property<Guid>("OperationId").HasColumnType("uuid");
            b.HasKey("BatchId", "ManifestId");
            b.HasIndex("BatchId", "AdmissionId").IsUnique();
            b.HasIndex("BatchId", "RepositoryId");
            b.HasIndex("BatchId", "OperationKind", "OperationId");
            b.ToTable("AcceptedFileManifests", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.CodeUnit", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("UnitId").HasColumnType("uuid");
            b.Property<Guid>("ManifestId").HasColumnType("uuid");
            b.Property<Guid>("RepositoryId").HasColumnType("uuid");
            b.Property<string>("SemanticKey").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property<string>("SemanticSelector").IsRequired().HasColumnType("text");
            b.Property<Guid?>("ParentUnitId").HasColumnType("uuid");
            b.HasKey("BatchId", "UnitId");
            b.HasAlternateKey("BatchId", "ManifestId", "UnitId");
            b.HasIndex("BatchId", "SemanticKey").IsUnique();
            b.HasIndex("BatchId", "RepositoryId");
            b.HasIndex("BatchId", "ManifestId", "ParentUnitId");
            b.ToTable("OctoberCodeUnits", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.LogicalDocument", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("LogicalDocumentId").HasColumnType("uuid");
            b.Property<Guid>("UnitId").HasColumnType("uuid");
            b.Property<int>("Kind").HasColumnType("integer");
            b.HasKey("BatchId", "LogicalDocumentId");
            b.HasIndex("BatchId", "UnitId").IsUnique();
            b.ToTable("OctoberLogicalDocuments", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.DocumentVersion", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("VersionId").HasColumnType("uuid");
            b.Property<Guid>("LogicalDocumentId").HasColumnType("uuid");
            b.Property<Guid>("ManifestId").HasColumnType("uuid");
            b.Property<int>("SchemaVersion").HasColumnType("integer");
            b.Property<string>("Title").IsRequired().HasColumnType("text");
            b.Property<string>("ContentJson").IsRequired().HasColumnType("jsonb");
            b.Property<string>("ContentDigest").IsRequired().HasColumnType("text");
            b.Property<string>("AnchorsJson").IsRequired().HasColumnType("jsonb");
            b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            b.HasKey("BatchId", "VersionId");
            b.HasIndex("BatchId", "LogicalDocumentId");
            b.HasIndex("BatchId", "ManifestId");
            b.ToTable("OctoberDocumentVersions", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.DocumentSectionRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("VersionId").HasColumnType("uuid");
            b.Property<string>("SectionId").IsRequired().HasColumnType("text");
            b.Property<int>("Ordinal").HasColumnType("integer");
            b.Property<string>("Title").IsRequired().HasColumnType("text");
            b.HasKey("BatchId", "VersionId", "SectionId");
            b.HasIndex("BatchId", "VersionId", "Ordinal").IsUnique();
            b.ToTable("OctoberDocumentSections", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.QualifiedClaimGroupRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("VersionId").HasColumnType("uuid");
            b.Property<string>("SectionId").IsRequired().HasColumnType("text");
            b.Property<string>("ClaimGroupId").IsRequired().HasColumnType("text");
            b.Property<string>("Text").IsRequired().HasColumnType("text");
            b.Property<int>("Qualification").HasColumnType("integer");
            b.Property<string>("EvidenceJson").IsRequired().HasColumnType("jsonb");
            b.Property<string>("LimitationsJson").IsRequired().HasColumnType("jsonb");
            b.HasKey("BatchId", "VersionId", "SectionId", "ClaimGroupId");
            b.ToTable("OctoberQualifiedClaimGroups", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.KnowledgeContributorRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("ManifestId").HasColumnType("uuid");
            b.Property<int>("Ordinal").HasColumnType("integer");
            b.Property<string>("ContributorJson").IsRequired().HasColumnType("jsonb");
            b.HasKey("BatchId", "ManifestId", "Ordinal");
            b.ToTable("KnowledgeContributors", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.CommunicationObservationRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("ManifestId").HasColumnType("uuid");
            b.Property<string>("ObservationKey").IsRequired().HasColumnType("text");
            b.Property<Guid>("UnitId").HasColumnType("uuid");
            b.Property<string>("ContentJson").IsRequired().HasColumnType("jsonb");
            b.HasKey("BatchId", "ManifestId", "ObservationKey");
            b.HasIndex("BatchId", "ManifestId", "UnitId");
            b.ToTable("OctoberCommunicationObservations", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.KnowledgeUncertaintyRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("ManifestId").HasColumnType("uuid");
            b.Property<string>("Key").IsRequired().HasColumnType("text");
            b.Property<string>("ContentJson").IsRequired().HasColumnType("jsonb");
            b.HasKey("BatchId", "ManifestId", "Key");
            b.ToTable("OctoberKnowledgeUncertainties", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.KnowledgeOperation", b =>
        {
            b.HasOne("Vulgata.Infrastructure.Data.October.OctoberBatchRecord", null).WithMany()
                .HasForeignKey("BatchId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.AcceptedFileManifest", b =>
        {
            b.HasOne("Vulgata.Infrastructure.Data.October.OctoberRepositoryRecord", null).WithMany()
                .HasForeignKey("BatchId", "RepositoryId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.KnowledgeOperation", null).WithMany()
                .HasForeignKey("BatchId", "OperationKind", "OperationId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.CodeUnit", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.AcceptedFileManifest", null).WithMany()
                .HasForeignKey("BatchId", "ManifestId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Vulgata.Infrastructure.Data.October.OctoberRepositoryRecord", null).WithMany()
                .HasForeignKey("BatchId", "RepositoryId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CodeUnit", null).WithMany()
                .HasForeignKey("BatchId", "ManifestId", "ParentUnitId")
                .HasPrincipalKey("BatchId", "ManifestId", "UnitId").OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.LogicalDocument", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CodeUnit", null).WithMany()
                .HasForeignKey("BatchId", "UnitId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.DocumentVersion", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.LogicalDocument", null).WithMany()
                .HasForeignKey("BatchId", "LogicalDocumentId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.AcceptedFileManifest", null).WithMany()
                .HasForeignKey("BatchId", "ManifestId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.DocumentSectionRecord", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.DocumentVersion", null).WithMany()
                .HasForeignKey("BatchId", "VersionId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.QualifiedClaimGroupRecord", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.DocumentSectionRecord", null).WithMany()
                .HasForeignKey("BatchId", "VersionId", "SectionId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.KnowledgeContributorRecord", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.AcceptedFileManifest", null).WithMany()
                .HasForeignKey("BatchId", "ManifestId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.CommunicationObservationRecord", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CodeUnit", null).WithMany()
                .HasForeignKey("BatchId", "ManifestId", "UnitId")
                .HasPrincipalKey("BatchId", "ManifestId", "UnitId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.KnowledgeUncertaintyRecord", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.AcceptedFileManifest", null).WithMany()
                .HasForeignKey("BatchId", "ManifestId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

    }
}
