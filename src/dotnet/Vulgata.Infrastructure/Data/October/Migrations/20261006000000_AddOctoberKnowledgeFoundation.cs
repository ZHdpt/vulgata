using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Vulgata.Infrastructure.Data.October.Migrations;

[DbContext(typeof(OctoberDbContext))]
[Migration("20261006000000_AddOctoberKnowledgeFoundation")]
public sealed class AddOctoberKnowledgeFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "KnowledgeOperations", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<int>(type: "integer", nullable: false),
                OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                PayloadDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CommittedResultJson = table.Column<string>(type: "jsonb", nullable: false),
                ContributorsJson = table.Column<string>(type: "jsonb", nullable: false),
                CommittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KnowledgeOperations", x => new { x.BatchId, x.Kind, x.OperationId });
                table.ForeignKey(name: "FK_KnowledgeOperations_OctoberBatches_BatchId",
                    columns: x => new { x.BatchId }, principalSchema: "october",
                    principalTable: "OctoberBatches", principalColumns: new[] { "BatchId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AcceptedFileManifests", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                ManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                AdmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                RootBindingIdentity = table.Column<string>(type: "text", nullable: false),
                RelativePath = table.Column<string>(type: "text", nullable: false),
                SourceDigest = table.Column<string>(type: "text", nullable: false),
                PreparedVersion = table.Column<string>(type: "text", nullable: false),
                ProposalDigest = table.Column<string>(type: "text", nullable: false),
                CoverageDisposition = table.Column<int>(type: "integer", nullable: false),
                CoverageReason = table.Column<string>(type: "text", nullable: true),
                UnitCount = table.Column<int>(type: "integer", nullable: false),
                OperationKind = table.Column<int>(type: "integer", nullable: false),
                OperationId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AcceptedFileManifests", x => new { x.BatchId, x.ManifestId });
                table.ForeignKey(name: "FK_AcceptedFileManifests_OctoberRepositorySelections_BatchId_R~",
                    columns: x => new { x.BatchId, x.RepositoryId }, principalSchema: "october",
                    principalTable: "OctoberRepositorySelections", principalColumns: new[] { "BatchId", "RepositoryId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_AcceptedFileManifests_KnowledgeOperations_BatchId_Operation~",
                    columns: x => new { x.BatchId, x.OperationKind, x.OperationId }, principalSchema: "october",
                    principalTable: "KnowledgeOperations", principalColumns: new[] { "BatchId", "Kind", "OperationId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_AcceptedFileManifests_BatchId_AdmissionId", schema: "october", table: "AcceptedFileManifests",
            columns: new[] { "BatchId", "AdmissionId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_AcceptedFileManifests_BatchId_RepositoryId", schema: "october", table: "AcceptedFileManifests",
            columns: new[] { "BatchId", "RepositoryId" });
        migrationBuilder.CreateIndex(name: "IX_AcceptedFileManifests_BatchId_OperationKind_OperationId", schema: "october", table: "AcceptedFileManifests",
            columns: new[] { "BatchId", "OperationKind", "OperationId" });
        migrationBuilder.CreateTable(
            name: "OctoberCodeUnits", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                ManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                SemanticKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SemanticSelector = table.Column<string>(type: "text", nullable: false),
                ParentUnitId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberCodeUnits", x => new { x.BatchId, x.UnitId });
                table.UniqueConstraint("AK_OctoberCodeUnits_BatchId_ManifestId_UnitId", x => new { x.BatchId, x.ManifestId, x.UnitId });
                table.ForeignKey(name: "FK_OctoberCodeUnits_AcceptedFileManifests_BatchId_ManifestId",
                    columns: x => new { x.BatchId, x.ManifestId }, principalSchema: "october",
                    principalTable: "AcceptedFileManifests", principalColumns: new[] { "BatchId", "ManifestId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_OctoberCodeUnits_OctoberRepositorySelections_BatchId_Reposi~",
                    columns: x => new { x.BatchId, x.RepositoryId }, principalSchema: "october",
                    principalTable: "OctoberRepositorySelections", principalColumns: new[] { "BatchId", "RepositoryId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_OctoberCodeUnits_OctoberCodeUnits_BatchId_ManifestId_Parent~",
                    columns: x => new { x.BatchId, x.ManifestId, x.ParentUnitId }, principalSchema: "october",
                    principalTable: "OctoberCodeUnits", principalColumns: new[] { "BatchId", "ManifestId", "UnitId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OctoberCodeUnits_BatchId_SemanticKey", schema: "october", table: "OctoberCodeUnits",
            columns: new[] { "BatchId", "SemanticKey" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_OctoberCodeUnits_BatchId_RepositoryId", schema: "october", table: "OctoberCodeUnits",
            columns: new[] { "BatchId", "RepositoryId" });
        migrationBuilder.CreateIndex(name: "IX_OctoberCodeUnits_BatchId_ManifestId_ParentUnitId", schema: "october", table: "OctoberCodeUnits",
            columns: new[] { "BatchId", "ManifestId", "ParentUnitId" });
        migrationBuilder.CreateTable(
            name: "OctoberLogicalDocuments", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                LogicalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberLogicalDocuments", x => new { x.BatchId, x.LogicalDocumentId });
                table.ForeignKey(name: "FK_OctoberLogicalDocuments_OctoberCodeUnits_BatchId_UnitId",
                    columns: x => new { x.BatchId, x.UnitId }, principalSchema: "october",
                    principalTable: "OctoberCodeUnits", principalColumns: new[] { "BatchId", "UnitId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OctoberLogicalDocuments_BatchId_UnitId", schema: "october", table: "OctoberLogicalDocuments",
            columns: new[] { "BatchId", "UnitId" }, unique: true);
        migrationBuilder.CreateTable(
            name: "OctoberDocumentVersions", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                LogicalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                ManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "text", nullable: false),
                ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                ContentDigest = table.Column<string>(type: "text", nullable: false),
                AnchorsJson = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberDocumentVersions", x => new { x.BatchId, x.VersionId });
                table.ForeignKey(name: "FK_OctoberDocumentVersions_OctoberLogicalDocuments_BatchId_Log~",
                    columns: x => new { x.BatchId, x.LogicalDocumentId }, principalSchema: "october",
                    principalTable: "OctoberLogicalDocuments", principalColumns: new[] { "BatchId", "LogicalDocumentId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_OctoberDocumentVersions_AcceptedFileManifests_BatchId_Manif~",
                    columns: x => new { x.BatchId, x.ManifestId }, principalSchema: "october",
                    principalTable: "AcceptedFileManifests", principalColumns: new[] { "BatchId", "ManifestId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OctoberDocumentVersions_BatchId_LogicalDocumentId", schema: "october", table: "OctoberDocumentVersions",
            columns: new[] { "BatchId", "LogicalDocumentId" });
        migrationBuilder.CreateIndex(name: "IX_OctoberDocumentVersions_BatchId_ManifestId", schema: "october", table: "OctoberDocumentVersions",
            columns: new[] { "BatchId", "ManifestId" });
        migrationBuilder.CreateTable(
            name: "OctoberDocumentSections", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<string>(type: "text", nullable: false),
                Ordinal = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberDocumentSections", x => new { x.BatchId, x.VersionId, x.SectionId });
                table.ForeignKey(name: "FK_OctoberDocumentSections_OctoberDocumentVersions_BatchId_Ver~",
                    columns: x => new { x.BatchId, x.VersionId }, principalSchema: "october",
                    principalTable: "OctoberDocumentVersions", principalColumns: new[] { "BatchId", "VersionId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OctoberDocumentSections_BatchId_VersionId_Ordinal", schema: "october", table: "OctoberDocumentSections",
            columns: new[] { "BatchId", "VersionId", "Ordinal" }, unique: true);
        migrationBuilder.CreateTable(
            name: "OctoberQualifiedClaimGroups", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<string>(type: "text", nullable: false),
                ClaimGroupId = table.Column<string>(type: "text", nullable: false),
                Text = table.Column<string>(type: "text", nullable: false),
                Qualification = table.Column<int>(type: "integer", nullable: false),
                EvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                LimitationsJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberQualifiedClaimGroups", x => new { x.BatchId, x.VersionId, x.SectionId, x.ClaimGroupId });
                table.ForeignKey(name: "FK_OctoberQualifiedClaimGroups_OctoberDocumentSections_BatchId~",
                    columns: x => new { x.BatchId, x.VersionId, x.SectionId }, principalSchema: "october",
                    principalTable: "OctoberDocumentSections", principalColumns: new[] { "BatchId", "VersionId", "SectionId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "KnowledgeContributors", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                ManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                Ordinal = table.Column<int>(type: "integer", nullable: false),
                ContributorJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KnowledgeContributors", x => new { x.BatchId, x.ManifestId, x.Ordinal });
                table.ForeignKey(name: "FK_KnowledgeContributors_AcceptedFileManifests_BatchId_Manifes~",
                    columns: x => new { x.BatchId, x.ManifestId }, principalSchema: "october",
                    principalTable: "AcceptedFileManifests", principalColumns: new[] { "BatchId", "ManifestId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "OctoberCommunicationObservations", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                ManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                ObservationKey = table.Column<string>(type: "text", nullable: false),
                UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                ContentJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberCommunicationObservations", x => new { x.BatchId, x.ManifestId, x.ObservationKey });
                table.ForeignKey(name: "FK_OctoberCommunicationObservations_OctoberCodeUnits_BatchId_M~",
                    columns: x => new { x.BatchId, x.ManifestId, x.UnitId }, principalSchema: "october",
                    principalTable: "OctoberCodeUnits", principalColumns: new[] { "BatchId", "ManifestId", "UnitId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OctoberCommunicationObservations_BatchId_ManifestId_UnitId", schema: "october", table: "OctoberCommunicationObservations",
            columns: new[] { "BatchId", "ManifestId", "UnitId" });
        migrationBuilder.CreateTable(
            name: "OctoberKnowledgeUncertainties", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                ManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "text", nullable: false),
                ContentJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberKnowledgeUncertainties", x => new { x.BatchId, x.ManifestId, x.Key });
                table.ForeignKey(name: "FK_OctoberKnowledgeUncertainties_AcceptedFileManifests_BatchId~",
                    columns: x => new { x.BatchId, x.ManifestId }, principalSchema: "october",
                    principalTable: "AcceptedFileManifests", principalColumns: new[] { "BatchId", "ManifestId" },
                    onDelete: ReferentialAction.Restrict);
            });


        // Same-transaction parent checks seal immutable memberships without a receipt table.
        migrationBuilder.Sql("""
            CREATE FUNCTION october."RejectOctoberKnowledgeMutation"() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Accepted October Knowledge is immutable' USING ERRCODE = '23514';
            END;
            $$;
            CREATE FUNCTION october."RequireNewOctoberKnowledgeParent"() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE parent_xid xid;
            BEGIN
                CASE TG_TABLE_NAME
                WHEN 'AcceptedFileManifests' THEN
                    SELECT xmin INTO parent_xid FROM october."KnowledgeOperations" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."Kind" = NEW."OperationKind" AND p."OperationId" = NEW."OperationId";
                WHEN 'OctoberCodeUnits' THEN
                    SELECT xmin INTO parent_xid FROM october."AcceptedFileManifests" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."ManifestId" = NEW."ManifestId";
                WHEN 'OctoberLogicalDocuments' THEN
                    SELECT xmin INTO parent_xid FROM october."OctoberCodeUnits" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."UnitId" = NEW."UnitId";
                WHEN 'OctoberDocumentVersions' THEN
                    SELECT xmin INTO parent_xid FROM october."AcceptedFileManifests" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."ManifestId" = NEW."ManifestId";
                WHEN 'OctoberDocumentSections' THEN
                    SELECT xmin INTO parent_xid FROM october."OctoberDocumentVersions" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."VersionId" = NEW."VersionId";
                WHEN 'OctoberQualifiedClaimGroups' THEN
                    SELECT xmin INTO parent_xid FROM october."OctoberDocumentSections" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."VersionId" = NEW."VersionId" AND p."SectionId" = NEW."SectionId";
                WHEN 'KnowledgeContributors' THEN
                    SELECT xmin INTO parent_xid FROM october."AcceptedFileManifests" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."ManifestId" = NEW."ManifestId";
                WHEN 'OctoberCommunicationObservations' THEN
                    SELECT xmin INTO parent_xid FROM october."OctoberCodeUnits" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."ManifestId" = NEW."ManifestId" AND p."UnitId" = NEW."UnitId";
                WHEN 'OctoberKnowledgeUncertainties' THEN
                    SELECT xmin INTO parent_xid FROM october."AcceptedFileManifests" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."ManifestId" = NEW."ManifestId";
                ELSE RAISE EXCEPTION 'Unknown October Knowledge parent' USING ERRCODE = '23514';
                END CASE;
                IF parent_xid IS NULL OR parent_xid <> pg_current_xact_id()::xid THEN
                    RAISE EXCEPTION 'Cannot extend committed October Knowledge' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE FUNCTION october."ProtectAcceptedOctoberSetup"() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE affected_batch uuid;
            BEGIN
                affected_batch := CASE WHEN TG_OP = 'INSERT' THEN NEW."BatchId" ELSE OLD."BatchId" END;
                PERFORM 1 FROM october."OctoberBatches" WHERE "BatchId" = affected_batch FOR UPDATE;
                IF EXISTS (SELECT 1 FROM october."KnowledgeOperations" WHERE "BatchId" = affected_batch) THEN
                    RAISE EXCEPTION 'Accepted October setup is immutable' USING ERRCODE = '23514';
                END IF;
                IF TG_OP = 'UPDATE' AND NEW."BatchId" <> OLD."BatchId" THEN
                    PERFORM 1 FROM october."OctoberBatches" WHERE "BatchId" = NEW."BatchId" FOR UPDATE;
                    IF EXISTS (SELECT 1 FROM october."KnowledgeOperations" WHERE "BatchId" = NEW."BatchId") THEN
                        RAISE EXCEPTION 'Cannot extend accepted October setup' USING ERRCODE = '23514';
                    END IF;
                END IF;
                IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
            END;
            $$;
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."KnowledgeOperations" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."KnowledgeOperations" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."AcceptedFileManifests" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."AcceptedFileManifests" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."AcceptedFileManifests" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberKnowledgeParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberCodeUnits" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberCodeUnits" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberCodeUnits" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberKnowledgeParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberLogicalDocuments" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberLogicalDocuments" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberLogicalDocuments" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberKnowledgeParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberDocumentVersions" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberDocumentVersions" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberDocumentVersions" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberKnowledgeParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberDocumentSections" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberDocumentSections" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberDocumentSections" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberKnowledgeParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberQualifiedClaimGroups" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberQualifiedClaimGroups" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberQualifiedClaimGroups" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberKnowledgeParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."KnowledgeContributors" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."KnowledgeContributors" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."KnowledgeContributors" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberKnowledgeParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberCommunicationObservations" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberCommunicationObservations" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberCommunicationObservations" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberKnowledgeParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberKnowledgeUncertainties" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberKnowledgeUncertainties" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberKnowledgeUncertainties" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberKnowledgeParent"();
            CREATE TRIGGER "AcceptedSetupHistory" BEFORE INSERT OR UPDATE OR DELETE ON october."OctoberBatches" FOR EACH ROW EXECUTE FUNCTION october."ProtectAcceptedOctoberSetup"();
            CREATE TRIGGER "AcceptedSetupHistory" BEFORE INSERT OR UPDATE OR DELETE ON october."OctoberRepositorySelections" FOR EACH ROW EXECUTE FUNCTION october."ProtectAcceptedOctoberSetup"();
            CREATE TRIGGER "AcceptedSetupHistory" BEFORE INSERT OR UPDATE OR DELETE ON october."OctoberContextReferences" FOR EACH ROW EXECUTE FUNCTION october."ProtectAcceptedOctoberSetup"();
            CREATE TRIGGER "AcceptedSetupHistory" BEFORE INSERT OR UPDATE OR DELETE ON october."OctoberAdvisoryEntries" FOR EACH ROW EXECUTE FUNCTION october."ProtectAcceptedOctoberSetup"();
            CREATE TRIGGER "AcceptedSetupHistory" BEFORE INSERT OR UPDATE OR DELETE ON october."OctoberContextDifferences" FOR EACH ROW EXECUTE FUNCTION october."ProtectAcceptedOctoberSetup"();

            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (string table in new[] { "OctoberBatches", "OctoberRepositorySelections", "OctoberContextReferences", "OctoberAdvisoryEntries", "OctoberContextDifferences" })
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS \"AcceptedSetupHistory\" ON october.\"{table}\";");
        migrationBuilder.DropTable(name: "OctoberKnowledgeUncertainties", schema: "october");
        migrationBuilder.DropTable(name: "OctoberCommunicationObservations", schema: "october");
        migrationBuilder.DropTable(name: "KnowledgeContributors", schema: "october");
        migrationBuilder.DropTable(name: "OctoberQualifiedClaimGroups", schema: "october");
        migrationBuilder.DropTable(name: "OctoberDocumentSections", schema: "october");
        migrationBuilder.DropTable(name: "OctoberDocumentVersions", schema: "october");
        migrationBuilder.DropTable(name: "OctoberLogicalDocuments", schema: "october");
        migrationBuilder.DropTable(name: "OctoberCodeUnits", schema: "october");
        migrationBuilder.DropTable(name: "AcceptedFileManifests", schema: "october");
        migrationBuilder.DropTable(name: "KnowledgeOperations", schema: "october");
        migrationBuilder.Sql("DROP FUNCTION october.\"ProtectAcceptedOctoberSetup\"(); DROP FUNCTION october.\"RequireNewOctoberKnowledgeParent\"(); DROP FUNCTION october.\"RejectOctoberKnowledgeMutation\"();");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
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
