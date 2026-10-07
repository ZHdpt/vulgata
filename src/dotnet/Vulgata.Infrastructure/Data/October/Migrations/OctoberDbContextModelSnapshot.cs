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
            b.HasAlternateKey("BatchId", "ManifestId", "LogicalDocumentId", "VersionId");
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


        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.CorpusSelectionRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<int>("OperationKind").HasColumnType("integer");
            b.Property<Guid>("OperationId").HasColumnType("uuid");
            b.Property<string>("CorpusDigest").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property<string>("ContributorsJson").IsRequired().HasColumnType("jsonb");
            b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            b.HasKey("BatchId", "CorpusId");
            b.HasIndex("BatchId").IsUnique();
            b.HasIndex("BatchId", "OperationKind", "OperationId");
            b.ToTable("OctoberCorpusSelections", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.CorpusManifestRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<Guid>("ManifestId").HasColumnType("uuid");
            b.HasKey("BatchId", "CorpusId", "ManifestId");
            b.HasIndex("BatchId", "ManifestId");
            b.ToTable("OctoberCorpusManifests", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.CorpusDocumentRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<Guid>("ManifestId").HasColumnType("uuid");
            b.Property<Guid>("LogicalDocumentId").HasColumnType("uuid");
            b.Property<Guid>("VersionId").HasColumnType("uuid");
            b.Property<int>("Ordinal").HasColumnType("integer");
            b.HasKey("BatchId", "CorpusId", "LogicalDocumentId");
            b.HasAlternateKey("BatchId", "CorpusId", "LogicalDocumentId", "VersionId");
            b.HasIndex("BatchId", "CorpusId", "VersionId").IsUnique();
            b.HasIndex("BatchId", "CorpusId", "Ordinal").IsUnique();
            b.HasIndex("BatchId", "CorpusId", "ManifestId");
            b.HasIndex("BatchId", "ManifestId", "LogicalDocumentId", "VersionId");
            b.ToTable("OctoberCorpusDocuments", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.CorpusCoverageRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<Guid>("AdmissionId").HasColumnType("uuid");
            b.Property<Guid>("RepositoryId").HasColumnType("uuid");
            b.Property<string>("CoverageJson").IsRequired().HasColumnType("jsonb");
            b.Property<string>("ContributorsJson").IsRequired().HasColumnType("jsonb");
            b.HasKey("BatchId", "CorpusId", "AdmissionId");
            b.HasIndex("BatchId", "RepositoryId");
            b.ToTable("OctoberCorpusCoverage", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.KnowledgePublicationRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("PublicationId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<string>("CorpusDigest").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property<int>("OperationKind").HasColumnType("integer");
            b.Property<Guid>("OperationId").HasColumnType("uuid");
            b.Property<string>("ContributorsJson").IsRequired().HasColumnType("jsonb");
            b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            b.HasKey("BatchId", "PublicationId");
            b.HasAlternateKey("BatchId", "PublicationId", "CorpusId");
            b.HasIndex("BatchId", "CorpusId");
            b.HasIndex("BatchId", "OperationKind", "OperationId");
            b.ToTable("OctoberKnowledgePublications", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.PublicationMemberRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("PublicationId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<Guid>("LogicalDocumentId").HasColumnType("uuid");
            b.Property<Guid>("VersionId").HasColumnType("uuid");
            b.Property<int>("Ordinal").HasColumnType("integer");
            b.HasKey("BatchId", "PublicationId", "LogicalDocumentId");
            b.HasAlternateKey("BatchId", "PublicationId", "LogicalDocumentId", "VersionId").HasName("AK_PublicationMember_ExactVersion");
            b.HasIndex("BatchId", "PublicationId", "VersionId").IsUnique();
            b.HasIndex("BatchId", "PublicationId", "Ordinal").IsUnique();
            b.HasIndex("BatchId", "PublicationId", "CorpusId");
            b.HasIndex("BatchId", "CorpusId", "LogicalDocumentId", "VersionId");
            b.ToTable("OctoberPublicationMembers", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.PublicationCoverageRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("PublicationId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<Guid>("AdmissionId").HasColumnType("uuid");
            b.Property<Guid>("RepositoryId").HasColumnType("uuid");
            b.Property<string>("CoverageJson").IsRequired().HasColumnType("jsonb");
            b.Property<string>("ContributorsJson").IsRequired().HasColumnType("jsonb");
            b.HasKey("BatchId", "PublicationId", "AdmissionId");
            b.HasIndex("BatchId", "PublicationId", "CorpusId");
            b.HasIndex("BatchId", "CorpusId", "AdmissionId");
            b.HasIndex("BatchId", "RepositoryId");
            b.ToTable("OctoberPublicationCoverage", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.PublicationUncertaintyRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("PublicationId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<Guid>("ManifestId").HasColumnType("uuid");
            b.Property<string>("Key").IsRequired().HasColumnType("text");
            b.HasKey("BatchId", "PublicationId", "ManifestId", "Key");
            b.HasIndex("BatchId", "PublicationId", "CorpusId");
            b.HasIndex("BatchId", "CorpusId", "ManifestId");
            b.HasIndex("BatchId", "ManifestId", "Key");
            b.ToTable("OctoberPublicationUncertainties", "october");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.CorpusSelectionRecord", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.KnowledgeOperation", null).WithMany().HasForeignKey("BatchId", "OperationKind", "OperationId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.CorpusManifestRecord", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CorpusSelectionRecord", null).WithMany().HasForeignKey("BatchId", "CorpusId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.AcceptedFileManifest", null).WithMany().HasForeignKey("BatchId", "ManifestId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.CorpusDocumentRecord", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CorpusManifestRecord", null).WithMany().HasForeignKey("BatchId", "CorpusId", "ManifestId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.DocumentVersion", null).WithMany().HasForeignKey("BatchId", "ManifestId", "LogicalDocumentId", "VersionId")
                .HasPrincipalKey("BatchId", "ManifestId", "LogicalDocumentId", "VersionId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.CorpusCoverageRecord", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CorpusSelectionRecord", null).WithMany().HasForeignKey("BatchId", "CorpusId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Vulgata.Infrastructure.Data.October.OctoberRepositoryRecord", null).WithMany().HasForeignKey("BatchId", "RepositoryId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.KnowledgePublicationRecord", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CorpusSelectionRecord", null).WithMany().HasForeignKey("BatchId", "CorpusId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.KnowledgeOperation", null).WithMany().HasForeignKey("BatchId", "OperationKind", "OperationId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.PublicationMemberRecord", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.KnowledgePublicationRecord", null).WithMany().HasForeignKey("BatchId", "PublicationId", "CorpusId")
                .HasPrincipalKey("BatchId", "PublicationId", "CorpusId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CorpusDocumentRecord", null).WithMany().HasForeignKey("BatchId", "CorpusId", "LogicalDocumentId", "VersionId")
                .HasPrincipalKey("BatchId", "CorpusId", "LogicalDocumentId", "VersionId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.PublicationCoverageRecord", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.KnowledgePublicationRecord", null).WithMany().HasForeignKey("BatchId", "PublicationId", "CorpusId")
                .HasPrincipalKey("BatchId", "PublicationId", "CorpusId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CorpusCoverageRecord", null).WithMany().HasForeignKey("BatchId", "CorpusId", "AdmissionId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Vulgata.Infrastructure.Data.October.OctoberRepositoryRecord", null).WithMany().HasForeignKey("BatchId", "RepositoryId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.PublicationUncertaintyRecord", b =>
        {
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.KnowledgePublicationRecord", null).WithMany().HasForeignKey("BatchId", "PublicationId", "CorpusId")
                .HasPrincipalKey("BatchId", "PublicationId", "CorpusId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CorpusManifestRecord", null).WithMany().HasForeignKey("BatchId", "CorpusId", "ManifestId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.KnowledgeUncertaintyRecord", null).WithMany().HasForeignKey("BatchId", "ManifestId", "Key").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberDialogue.DialogueConversationRecord", b =>
        {
            b.Property<Guid>("ConversationId").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<Guid>("ContextId").HasColumnType("uuid");
            b.Property<string>("OwnerSubjectId").IsRequired().HasColumnType("text");
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("PublicationId").HasColumnType("uuid");
            b.Property<string>("IdentityJson").IsRequired().HasColumnType("jsonb");
            b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<long>("NextTurnSequence").HasColumnType("bigint");
            b.Property<long>("NextFence").HasColumnType("bigint");
            b.HasKey("ConversationId");
            b.HasAlternateKey("ConversationId", "BatchId", "PublicationId").HasName("AK_DialogueConversation_Publication");
            b.HasIndex("BatchId", "PublicationId").HasDatabaseName("IX_DialogueConversation_Publication");
            b.ToTable("OctoberDialogueConversations", "october", t => t.HasCheckConstraint("CK_DialogueConversation_Counters", "\"NextTurnSequence\" > 0 AND \"NextFence\" > 0"));
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.KnowledgePublicationRecord", null).WithMany()
                .HasForeignKey("BatchId", "PublicationId").OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_DialogueConversation_Publication");
        });
        modelBuilder.Entity("Vulgata.Core.Entities.OctoberDialogue.DialogueTurnRecord", b =>
        {
            b.Property<Guid>("ConversationId").HasColumnType("uuid");
            b.Property<Guid>("TurnId").HasColumnType("uuid");
            b.Property<Guid>("ClientRequestKey").HasColumnType("uuid");
            b.Property<long>("Sequence").HasColumnType("bigint");
            b.Property<long>("Fence").HasColumnType("bigint");
            b.Property<Guid>("OwnerGeneration").HasColumnType("uuid");
            b.Property<Guid>("AttemptId").HasColumnType("uuid");
            b.Property<string>("RequestDigest").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property<string>("BeginEnvelopeJson").IsRequired().HasColumnType("jsonb");
            b.Property<DateTimeOffset>("AdmittedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("DeadlineUtc").HasColumnType("timestamp with time zone");
            b.Property<int>("State").HasColumnType("integer");
            b.HasKey("ConversationId", "TurnId");
            b.HasIndex("ConversationId", "ClientRequestKey").IsUnique();
            b.HasIndex("ConversationId", "Sequence").IsUnique();
            b.HasIndex("ConversationId").IsUnique().HasDatabaseName("UX_DialogueTurn_Active").HasFilter("\"State\" IN (0, 1, 2, 3)");
            b.ToTable("OctoberDialogueTurns", "october", t => t.HasCheckConstraint("CK_DialogueTurn_State", "\"State\" BETWEEN 0 AND 6 AND \"Sequence\" > 0 AND \"Fence\" > 0"));
            b.HasOne("Vulgata.Core.Entities.OctoberDialogue.DialogueConversationRecord", null).WithMany()
                .HasForeignKey("ConversationId").OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_DialogueTurn_Conversation");
        });
        modelBuilder.Entity("Vulgata.Core.Entities.OctoberDialogue.DialogueTerminalRecord", b =>
        {
            b.Property<Guid>("ConversationId").HasColumnType("uuid");
            b.Property<Guid>("TurnId").HasColumnType("uuid");
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("PublicationId").HasColumnType("uuid");
            b.Property<Guid>("SaveOperationId").HasColumnType("uuid");
            b.Property<string>("OutcomeDigest").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property<int>("TerminalState").HasColumnType("integer");
            b.Property<int>("Reason").HasColumnType("integer");
            b.Property<long>("Fence").HasColumnType("bigint");
            b.Property<DateTimeOffset>("CommittedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<string>("TerminalEnvelopeJson").IsRequired().HasColumnType("jsonb");
            b.Property<string>("VerifiedConsumedJson").IsRequired().HasColumnType("jsonb");
            b.HasKey("ConversationId", "TurnId");
            b.HasAlternateKey("ConversationId", "TurnId", "BatchId", "PublicationId").HasName("AK_DialogueTerminal_Publication");
            b.HasIndex("ConversationId", "SaveOperationId").IsUnique();
            b.HasIndex("ConversationId", "BatchId", "PublicationId").HasDatabaseName("IX_DialogueTerminal_ConversationPublication");
            b.ToTable("OctoberDialogueTerminals", "october", t => t.HasCheckConstraint("CK_DialogueTerminal_State", "\"TerminalState\" BETWEEN 0 AND 2 AND \"Fence\" > 0"));
            b.HasOne("Vulgata.Core.Entities.OctoberDialogue.DialogueTurnRecord", null).WithOne()
                .HasForeignKey("Vulgata.Core.Entities.OctoberDialogue.DialogueTerminalRecord", "ConversationId", "TurnId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_DialogueTerminal_Turn");
            b.HasOne("Vulgata.Core.Entities.OctoberDialogue.DialogueConversationRecord", null).WithMany()
                .HasForeignKey("ConversationId", "BatchId", "PublicationId").HasPrincipalKey("ConversationId", "BatchId", "PublicationId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_DialogueTerminal_ConversationPublication");
        });
        modelBuilder.Entity("Vulgata.Core.Entities.OctoberDialogue.DialogueReferenceRecord", b =>
        {
            b.Property<Guid>("ConversationId").HasColumnType("uuid");
            b.Property<Guid>("TurnId").HasColumnType("uuid");
            b.Property<int>("Ordinal").HasColumnType("integer");
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("PublicationId").HasColumnType("uuid");
            b.Property<Guid>("LogicalDocumentId").HasColumnType("uuid");
            b.Property<Guid>("VersionId").HasColumnType("uuid");
            b.Property<string>("SectionId").HasColumnType("text");
            b.HasKey("ConversationId", "TurnId", "Ordinal");
            b.HasIndex("ConversationId", "TurnId", "BatchId", "PublicationId").HasDatabaseName("IX_DialogueReference_Terminal");
            b.HasIndex("BatchId", "PublicationId", "LogicalDocumentId", "VersionId").HasDatabaseName("IX_DialogueReference_PublicationVersion");
            b.HasIndex("BatchId", "VersionId", "SectionId").HasDatabaseName("IX_DialogueReference_Section");
            b.ToTable("OctoberDialogueReferences", "october");
            b.HasOne("Vulgata.Core.Entities.OctoberDialogue.DialogueTerminalRecord", null).WithMany()
                .HasForeignKey("ConversationId", "TurnId", "BatchId", "PublicationId").HasPrincipalKey("ConversationId", "TurnId", "BatchId", "PublicationId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_DialogueReference_Terminal");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.PublicationMemberRecord", null).WithMany()
                .HasForeignKey("BatchId", "PublicationId", "LogicalDocumentId", "VersionId").HasPrincipalKey("BatchId", "PublicationId", "LogicalDocumentId", "VersionId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_DialogueReference_PublicationVersion");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.DocumentSectionRecord", null).WithMany()
                .HasForeignKey("BatchId", "VersionId", "SectionId").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_DialogueReference_Section");
        });
        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.ResolutionItem", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<Guid>("ItemId").HasColumnType("uuid");
            b.Property<int>("OperationKind").HasColumnType("integer");
            b.Property<Guid>("OperationId").HasColumnType("uuid");
            b.Property<string>("InquiryFingerprint").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property<string>("PolicyVersion").IsRequired().HasColumnType("text");
            b.Property<string>("WitnessJson").HasColumnType("jsonb");
            b.Property<string>("OutcomeJson").IsRequired().HasColumnType("jsonb");
            b.Property<string>("ContributorsJson").IsRequired().HasColumnType("jsonb");
            b.Property<string>("PlanDispositionsJson").IsRequired().HasColumnType("jsonb");
            b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            b.HasKey("BatchId", "ItemId");
            b.HasAlternateKey("BatchId", "CorpusId", "ItemId").HasName("AK_ResolutionItem_Corpus");
            b.HasIndex("BatchId", "CorpusId", "InquiryFingerprint").IsUnique().HasDatabaseName("UX_ResolutionItem_Fingerprint");
            b.HasIndex("BatchId", "OperationKind", "OperationId").IsUnique().HasDatabaseName("UX_ResolutionItem_Operation");
            b.ToTable("OctoberResolutionItems", "october");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CorpusSelectionRecord", null).WithMany()
                .HasForeignKey("BatchId", "CorpusId").HasPrincipalKey("BatchId", "CorpusId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_ResolutionItem_Corpus");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.KnowledgeOperation", null).WithMany()
                .HasForeignKey("BatchId", "OperationKind", "OperationId").HasPrincipalKey("BatchId", "Kind", "OperationId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_ResolutionItem_Operation");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.ResolutionDocumentDependencyRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<Guid>("ItemId").HasColumnType("uuid");
            b.Property<Guid>("LogicalDocumentId").HasColumnType("uuid");
            b.Property<Guid>("VersionId").HasColumnType("uuid");
            b.Property<bool>("IsInquiryCaller").ValueGeneratedOnAdd().HasColumnType("boolean").HasDefaultValue(false);
            b.HasKey("BatchId", "ItemId", "LogicalDocumentId", "VersionId");
            b.HasIndex("BatchId", "ItemId").IsUnique().HasDatabaseName("UX_ResolutionDependency_InquiryCaller").HasFilter("\"IsInquiryCaller\" = TRUE");
            b.HasIndex("BatchId", "CorpusId", "ItemId").HasDatabaseName("IX_ResolutionDependency_Item");
            b.HasIndex("BatchId", "CorpusId", "LogicalDocumentId", "VersionId").HasDatabaseName("IX_ResolutionDependency_Document");
            b.ToTable("OctoberResolutionDocumentDependencies", "october");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.ResolutionItem", null).WithMany()
                .HasForeignKey("BatchId", "CorpusId", "ItemId").HasPrincipalKey("BatchId", "CorpusId", "ItemId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_ResolutionDependency_Item");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CorpusDocumentRecord", null).WithMany()
                .HasForeignKey("BatchId", "CorpusId", "LogicalDocumentId", "VersionId").HasPrincipalKey("BatchId", "CorpusId", "LogicalDocumentId", "VersionId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_ResolutionDependency_Document");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.RelationshipVersion", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<Guid>("RelationshipVersionId").HasColumnType("uuid");
            b.Property<Guid>("ItemId").HasColumnType("uuid");
            b.Property<Guid>("FromLogicalDocumentId").HasColumnType("uuid");
            b.Property<Guid>("FromVersionId").HasColumnType("uuid");
            b.Property<Guid>("ToLogicalDocumentId").HasColumnType("uuid");
            b.Property<Guid>("ToVersionId").HasColumnType("uuid");
            b.Property<string>("ContentJson").IsRequired().HasColumnType("jsonb");
            b.HasKey("BatchId", "RelationshipVersionId");
            b.HasAlternateKey("BatchId", "CorpusId", "RelationshipVersionId").HasName("AK_RelationshipVersion_Corpus");
            b.HasAlternateKey("BatchId", "CorpusId", "RelationshipVersionId", "FromLogicalDocumentId", "FromVersionId", "ToLogicalDocumentId", "ToVersionId").HasName("AK_RelationshipVersion_Endpoints");
            b.HasIndex("BatchId", "CorpusId", "ItemId").IsUnique().HasDatabaseName("UX_RelationshipVersion_Item");
            b.HasIndex("BatchId", "CorpusId", "FromLogicalDocumentId", "FromVersionId").HasDatabaseName("IX_RelationshipVersion_From");
            b.HasIndex("BatchId", "CorpusId", "ToLogicalDocumentId", "ToVersionId").HasDatabaseName("IX_RelationshipVersion_To");
            b.ToTable("OctoberRelationshipVersions", "october");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.ResolutionItem", null).WithMany()
                .HasForeignKey("BatchId", "CorpusId", "ItemId").HasPrincipalKey("BatchId", "CorpusId", "ItemId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_RelationshipVersion_Item");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CorpusDocumentRecord", null).WithMany()
                .HasForeignKey("BatchId", "CorpusId", "FromLogicalDocumentId", "FromVersionId").HasPrincipalKey("BatchId", "CorpusId", "LogicalDocumentId", "VersionId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_RelationshipVersion_From");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.CorpusDocumentRecord", null).WithMany()
                .HasForeignKey("BatchId", "CorpusId", "ToLogicalDocumentId", "ToVersionId").HasPrincipalKey("BatchId", "CorpusId", "LogicalDocumentId", "VersionId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_RelationshipVersion_To");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.UncertaintyVersion", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<Guid>("UncertaintyId").HasColumnType("uuid");
            b.Property<Guid>("ItemId").HasColumnType("uuid");
            b.Property<string>("Key").IsRequired().HasColumnType("text");
            b.Property<string>("ContentJson").IsRequired().HasColumnType("jsonb");
            b.HasKey("BatchId", "UncertaintyId");
            b.HasAlternateKey("BatchId", "CorpusId", "UncertaintyId").HasName("AK_UncertaintyVersion_Corpus");
            b.HasIndex("BatchId", "CorpusId", "Key").IsUnique().HasDatabaseName("UX_UncertaintyVersion_Key");
            b.HasIndex("BatchId", "CorpusId", "ItemId").IsUnique().HasDatabaseName("UX_UncertaintyVersion_Item");
            b.ToTable("OctoberUncertaintyVersions", "october");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.ResolutionItem", null).WithMany()
                .HasForeignKey("BatchId", "CorpusId", "ItemId").HasPrincipalKey("BatchId", "CorpusId", "ItemId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_UncertaintyVersion_Item");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.PublicationRelationshipRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<Guid>("PublicationId").HasColumnType("uuid");
            b.Property<Guid>("RelationshipVersionId").HasColumnType("uuid");
            b.Property<Guid>("FromLogicalDocumentId").HasColumnType("uuid");
            b.Property<Guid>("FromVersionId").HasColumnType("uuid");
            b.Property<Guid>("ToLogicalDocumentId").HasColumnType("uuid");
            b.Property<Guid>("ToVersionId").HasColumnType("uuid");
            b.HasKey("BatchId", "PublicationId", "RelationshipVersionId");
            b.HasIndex("BatchId", "PublicationId", "CorpusId").HasDatabaseName("IX_PublicationRelationship_Publication");
            b.HasIndex("BatchId", "CorpusId", "RelationshipVersionId", "FromLogicalDocumentId", "FromVersionId", "ToLogicalDocumentId", "ToVersionId").HasDatabaseName("IX_PublicationRelationship_Evidence");
            b.HasIndex("BatchId", "PublicationId", "FromLogicalDocumentId", "FromVersionId").HasDatabaseName("IX_PublicationRelationship_From");
            b.HasIndex("BatchId", "PublicationId", "ToLogicalDocumentId", "ToVersionId").HasDatabaseName("IX_PublicationRelationship_To");
            b.ToTable("OctoberPublicationRelationships", "october");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.KnowledgePublicationRecord", null).WithMany()
                .HasForeignKey("BatchId", "PublicationId", "CorpusId").HasPrincipalKey("BatchId", "PublicationId", "CorpusId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_PublicationRelationship_Publication");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.RelationshipVersion", null).WithMany()
                .HasForeignKey("BatchId", "CorpusId", "RelationshipVersionId", "FromLogicalDocumentId", "FromVersionId", "ToLogicalDocumentId", "ToVersionId").HasPrincipalKey("BatchId", "CorpusId", "RelationshipVersionId", "FromLogicalDocumentId", "FromVersionId", "ToLogicalDocumentId", "ToVersionId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_PublicationRelationship_Evidence");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.PublicationMemberRecord", null).WithMany()
                .HasForeignKey("BatchId", "PublicationId", "FromLogicalDocumentId", "FromVersionId").HasPrincipalKey("BatchId", "PublicationId", "LogicalDocumentId", "VersionId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_PublicationRelationship_From");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.PublicationMemberRecord", null).WithMany()
                .HasForeignKey("BatchId", "PublicationId", "ToLogicalDocumentId", "ToVersionId").HasPrincipalKey("BatchId", "PublicationId", "LogicalDocumentId", "VersionId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_PublicationRelationship_To");
        });

        modelBuilder.Entity("Vulgata.Core.Entities.OctoberKnowledge.PublicationResolutionUncertaintyRecord", b =>
        {
            b.Property<Guid>("BatchId").HasColumnType("uuid");
            b.Property<Guid>("CorpusId").HasColumnType("uuid");
            b.Property<Guid>("PublicationId").HasColumnType("uuid");
            b.Property<Guid>("UncertaintyId").HasColumnType("uuid");
            b.HasKey("BatchId", "PublicationId", "UncertaintyId");
            b.HasIndex("BatchId", "PublicationId", "CorpusId").HasDatabaseName("IX_PublicationResolutionUnknown_Publication");
            b.HasIndex("BatchId", "CorpusId", "UncertaintyId").HasDatabaseName("IX_PublicationResolutionUnknown_Evidence");
            b.ToTable("OctoberPublicationResolutionUncertainties", "october");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.KnowledgePublicationRecord", null).WithMany()
                .HasForeignKey("BatchId", "PublicationId", "CorpusId").HasPrincipalKey("BatchId", "PublicationId", "CorpusId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_PublicationResolutionUnknown_Publication");
            b.HasOne("Vulgata.Core.Entities.OctoberKnowledge.UncertaintyVersion", null).WithMany()
                .HasForeignKey("BatchId", "CorpusId", "UncertaintyId").HasPrincipalKey("BatchId", "CorpusId", "UncertaintyId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired().HasConstraintName("FK_PublicationResolutionUnknown_Evidence");
        });
    }
}
