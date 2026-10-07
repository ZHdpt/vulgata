using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Vulgata.Infrastructure.Data.October.Migrations;

[DbContext(typeof(OctoberDbContext))]
[Migration("20261006010000_AddOctoberKnowledgePublications")]
public sealed class AddOctoberKnowledgePublications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(name: "AK_OctoberDocumentVersions_BatchId_ManifestId_LogicalDocumentI~", schema: "october",
            table: "OctoberDocumentVersions", columns: new[] { "BatchId", "ManifestId", "LogicalDocumentId", "VersionId" });

        migrationBuilder.CreateTable(name: "OctoberCorpusSelections", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                OperationKind = table.Column<int>(type: "integer", nullable: false),
                OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                CorpusDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ContributorsJson = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberCorpusSelections", x => new { x.BatchId, x.CorpusId });
                table.ForeignKey(name: "FK_OctoberCorpusSelections_KnowledgeOperations_BatchId_Operati~",
                    columns: x => new { x.BatchId, x.OperationKind, x.OperationId }, principalSchema: "october",
                    principalTable: "KnowledgeOperations", principalColumns: new[] { "BatchId", "Kind", "OperationId" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OctoberCorpusSelections_BatchId", schema: "october", table: "OctoberCorpusSelections",
            columns: new[] { "BatchId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_OctoberCorpusSelections_BatchId_OperationKind_OperationId", schema: "october", table: "OctoberCorpusSelections",
            columns: new[] { "BatchId", "OperationKind", "OperationId" });
        migrationBuilder.CreateTable(name: "OctoberCorpusManifests", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                ManifestId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberCorpusManifests", x => new { x.BatchId, x.CorpusId, x.ManifestId });
                table.ForeignKey(name: "FK_OctoberCorpusManifests_OctoberCorpusSelections_BatchId_Corp~",
                    columns: x => new { x.BatchId, x.CorpusId }, principalSchema: "october",
                    principalTable: "OctoberCorpusSelections", principalColumns: new[] { "BatchId", "CorpusId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_OctoberCorpusManifests_AcceptedFileManifests_BatchId_Manife~",
                    columns: x => new { x.BatchId, x.ManifestId }, principalSchema: "october",
                    principalTable: "AcceptedFileManifests", principalColumns: new[] { "BatchId", "ManifestId" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OctoberCorpusManifests_BatchId_ManifestId", schema: "october", table: "OctoberCorpusManifests",
            columns: new[] { "BatchId", "ManifestId" });
        migrationBuilder.CreateTable(name: "OctoberCorpusDocuments", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                ManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                LogicalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                Ordinal = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberCorpusDocuments", x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId });
                table.UniqueConstraint("AK_OctoberCorpusDocuments_BatchId_CorpusId_LogicalDocumentId_V~", x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId, x.VersionId });
                table.ForeignKey(name: "FK_OctoberCorpusDocuments_OctoberCorpusManifests_BatchId_Corpu~",
                    columns: x => new { x.BatchId, x.CorpusId, x.ManifestId }, principalSchema: "october",
                    principalTable: "OctoberCorpusManifests", principalColumns: new[] { "BatchId", "CorpusId", "ManifestId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_OctoberCorpusDocuments_OctoberDocumentVersions_BatchId_Mani~",
                    columns: x => new { x.BatchId, x.ManifestId, x.LogicalDocumentId, x.VersionId }, principalSchema: "october",
                    principalTable: "OctoberDocumentVersions", principalColumns: new[] { "BatchId", "ManifestId", "LogicalDocumentId", "VersionId" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OctoberCorpusDocuments_BatchId_CorpusId_VersionId", schema: "october", table: "OctoberCorpusDocuments",
            columns: new[] { "BatchId", "CorpusId", "VersionId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_OctoberCorpusDocuments_BatchId_CorpusId_Ordinal", schema: "october", table: "OctoberCorpusDocuments",
            columns: new[] { "BatchId", "CorpusId", "Ordinal" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_OctoberCorpusDocuments_BatchId_CorpusId_ManifestId", schema: "october", table: "OctoberCorpusDocuments",
            columns: new[] { "BatchId", "CorpusId", "ManifestId" });
        migrationBuilder.CreateIndex(name: "IX_OctoberCorpusDocuments_BatchId_ManifestId_LogicalDocumentId~", schema: "october", table: "OctoberCorpusDocuments",
            columns: new[] { "BatchId", "ManifestId", "LogicalDocumentId", "VersionId" });
        migrationBuilder.CreateTable(name: "OctoberCorpusCoverage", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                AdmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                CoverageJson = table.Column<string>(type: "jsonb", nullable: false),
                ContributorsJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberCorpusCoverage", x => new { x.BatchId, x.CorpusId, x.AdmissionId });
                table.ForeignKey(name: "FK_OctoberCorpusCoverage_OctoberCorpusSelections_BatchId_Corpu~",
                    columns: x => new { x.BatchId, x.CorpusId }, principalSchema: "october",
                    principalTable: "OctoberCorpusSelections", principalColumns: new[] { "BatchId", "CorpusId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_OctoberCorpusCoverage_OctoberRepositorySelections_BatchId_R~",
                    columns: x => new { x.BatchId, x.RepositoryId }, principalSchema: "october",
                    principalTable: "OctoberRepositorySelections", principalColumns: new[] { "BatchId", "RepositoryId" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OctoberCorpusCoverage_BatchId_RepositoryId", schema: "october", table: "OctoberCorpusCoverage",
            columns: new[] { "BatchId", "RepositoryId" });
        migrationBuilder.CreateTable(name: "OctoberKnowledgePublications", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                CorpusDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                OperationKind = table.Column<int>(type: "integer", nullable: false),
                OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                ContributorsJson = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberKnowledgePublications", x => new { x.BatchId, x.PublicationId });
                table.UniqueConstraint("AK_OctoberKnowledgePublications_BatchId_PublicationId_CorpusId", x => new { x.BatchId, x.PublicationId, x.CorpusId });
                table.ForeignKey(name: "FK_OctoberKnowledgePublications_OctoberCorpusSelections_BatchI~",
                    columns: x => new { x.BatchId, x.CorpusId }, principalSchema: "october",
                    principalTable: "OctoberCorpusSelections", principalColumns: new[] { "BatchId", "CorpusId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_OctoberKnowledgePublications_KnowledgeOperations_BatchId_Op~",
                    columns: x => new { x.BatchId, x.OperationKind, x.OperationId }, principalSchema: "october",
                    principalTable: "KnowledgeOperations", principalColumns: new[] { "BatchId", "Kind", "OperationId" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OctoberKnowledgePublications_BatchId_CorpusId", schema: "october", table: "OctoberKnowledgePublications",
            columns: new[] { "BatchId", "CorpusId" });
        migrationBuilder.CreateIndex(name: "IX_OctoberKnowledgePublications_BatchId_OperationKind_Operatio~", schema: "october", table: "OctoberKnowledgePublications",
            columns: new[] { "BatchId", "OperationKind", "OperationId" });
        migrationBuilder.CreateTable(name: "OctoberPublicationMembers", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                LogicalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                Ordinal = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberPublicationMembers", x => new { x.BatchId, x.PublicationId, x.LogicalDocumentId });
                table.ForeignKey(name: "FK_OctoberPublicationMembers_OctoberKnowledgePublications_Batc~",
                    columns: x => new { x.BatchId, x.PublicationId, x.CorpusId }, principalSchema: "october",
                    principalTable: "OctoberKnowledgePublications", principalColumns: new[] { "BatchId", "PublicationId", "CorpusId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_OctoberPublicationMembers_OctoberCorpusDocuments_BatchId_Co~",
                    columns: x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId, x.VersionId }, principalSchema: "october",
                    principalTable: "OctoberCorpusDocuments", principalColumns: new[] { "BatchId", "CorpusId", "LogicalDocumentId", "VersionId" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OctoberPublicationMembers_BatchId_PublicationId_VersionId", schema: "october", table: "OctoberPublicationMembers",
            columns: new[] { "BatchId", "PublicationId", "VersionId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_OctoberPublicationMembers_BatchId_PublicationId_Ordinal", schema: "october", table: "OctoberPublicationMembers",
            columns: new[] { "BatchId", "PublicationId", "Ordinal" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_OctoberPublicationMembers_BatchId_PublicationId_CorpusId", schema: "october", table: "OctoberPublicationMembers",
            columns: new[] { "BatchId", "PublicationId", "CorpusId" });
        migrationBuilder.CreateIndex(name: "IX_OctoberPublicationMembers_BatchId_CorpusId_LogicalDocumentI~", schema: "october", table: "OctoberPublicationMembers",
            columns: new[] { "BatchId", "CorpusId", "LogicalDocumentId", "VersionId" });
        migrationBuilder.CreateTable(name: "OctoberPublicationCoverage", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                AdmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                CoverageJson = table.Column<string>(type: "jsonb", nullable: false),
                ContributorsJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberPublicationCoverage", x => new { x.BatchId, x.PublicationId, x.AdmissionId });
                table.ForeignKey(name: "FK_OctoberPublicationCoverage_OctoberKnowledgePublications_Bat~",
                    columns: x => new { x.BatchId, x.PublicationId, x.CorpusId }, principalSchema: "october",
                    principalTable: "OctoberKnowledgePublications", principalColumns: new[] { "BatchId", "PublicationId", "CorpusId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_OctoberPublicationCoverage_OctoberCorpusCoverage_BatchId_Co~",
                    columns: x => new { x.BatchId, x.CorpusId, x.AdmissionId }, principalSchema: "october",
                    principalTable: "OctoberCorpusCoverage", principalColumns: new[] { "BatchId", "CorpusId", "AdmissionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_OctoberPublicationCoverage_OctoberRepositorySelections_Batc~",
                    columns: x => new { x.BatchId, x.RepositoryId }, principalSchema: "october",
                    principalTable: "OctoberRepositorySelections", principalColumns: new[] { "BatchId", "RepositoryId" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OctoberPublicationCoverage_BatchId_PublicationId_CorpusId", schema: "october", table: "OctoberPublicationCoverage",
            columns: new[] { "BatchId", "PublicationId", "CorpusId" });
        migrationBuilder.CreateIndex(name: "IX_OctoberPublicationCoverage_BatchId_CorpusId_AdmissionId", schema: "october", table: "OctoberPublicationCoverage",
            columns: new[] { "BatchId", "CorpusId", "AdmissionId" });
        migrationBuilder.CreateIndex(name: "IX_OctoberPublicationCoverage_BatchId_RepositoryId", schema: "october", table: "OctoberPublicationCoverage",
            columns: new[] { "BatchId", "RepositoryId" });
        migrationBuilder.CreateTable(name: "OctoberPublicationUncertainties", schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                ManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberPublicationUncertainties", x => new { x.BatchId, x.PublicationId, x.ManifestId, x.Key });
                table.ForeignKey(name: "FK_OctoberPublicationUncertainties_OctoberKnowledgePublication~",
                    columns: x => new { x.BatchId, x.PublicationId, x.CorpusId }, principalSchema: "october",
                    principalTable: "OctoberKnowledgePublications", principalColumns: new[] { "BatchId", "PublicationId", "CorpusId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_OctoberPublicationUncertainties_OctoberCorpusManifests_Batc~",
                    columns: x => new { x.BatchId, x.CorpusId, x.ManifestId }, principalSchema: "october",
                    principalTable: "OctoberCorpusManifests", principalColumns: new[] { "BatchId", "CorpusId", "ManifestId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_OctoberPublicationUncertainties_OctoberKnowledgeUncertainti~",
                    columns: x => new { x.BatchId, x.ManifestId, x.Key }, principalSchema: "october",
                    principalTable: "OctoberKnowledgeUncertainties", principalColumns: new[] { "BatchId", "ManifestId", "Key" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OctoberPublicationUncertainties_BatchId_PublicationId_Corpu~", schema: "october", table: "OctoberPublicationUncertainties",
            columns: new[] { "BatchId", "PublicationId", "CorpusId" });
        migrationBuilder.CreateIndex(name: "IX_OctoberPublicationUncertainties_BatchId_CorpusId_ManifestId", schema: "october", table: "OctoberPublicationUncertainties",
            columns: new[] { "BatchId", "CorpusId", "ManifestId" });
        migrationBuilder.CreateIndex(name: "IX_OctoberPublicationUncertainties_BatchId_ManifestId_Key", schema: "october", table: "OctoberPublicationUncertainties",
            columns: new[] { "BatchId", "ManifestId", "Key" });

        // Creation-transaction membership seals, plus the same batch lock used by trusted freeze.
        migrationBuilder.Sql("""
            CREATE FUNCTION october."RequireNewOctoberPublicationParent"() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE parent_xid xid;
            BEGIN
                CASE TG_TABLE_NAME
                WHEN 'OctoberCorpusSelections' THEN
                    SELECT xmin INTO parent_xid FROM october."KnowledgeOperations" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."Kind" = NEW."OperationKind" AND p."OperationId" = NEW."OperationId";
                WHEN 'OctoberCorpusManifests' THEN
                    SELECT xmin INTO parent_xid FROM october."OctoberCorpusSelections" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."CorpusId" = NEW."CorpusId";
                WHEN 'OctoberCorpusDocuments' THEN
                    SELECT xmin INTO parent_xid FROM october."OctoberCorpusManifests" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."CorpusId" = NEW."CorpusId" AND p."ManifestId" = NEW."ManifestId";
                WHEN 'OctoberCorpusCoverage' THEN
                    SELECT xmin INTO parent_xid FROM october."OctoberCorpusSelections" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."CorpusId" = NEW."CorpusId";
                WHEN 'OctoberKnowledgePublications' THEN
                    SELECT xmin INTO parent_xid FROM october."KnowledgeOperations" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."Kind" = NEW."OperationKind" AND p."OperationId" = NEW."OperationId";
                WHEN 'OctoberPublicationMembers' THEN
                    SELECT xmin INTO parent_xid FROM october."OctoberKnowledgePublications" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."PublicationId" = NEW."PublicationId" AND p."CorpusId" = NEW."CorpusId";
                WHEN 'OctoberPublicationCoverage' THEN
                    SELECT xmin INTO parent_xid FROM october."OctoberKnowledgePublications" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."PublicationId" = NEW."PublicationId" AND p."CorpusId" = NEW."CorpusId";
                WHEN 'OctoberPublicationUncertainties' THEN
                    SELECT xmin INTO parent_xid FROM october."OctoberKnowledgePublications" p
                    WHERE p."BatchId" = NEW."BatchId" AND p."PublicationId" = NEW."PublicationId" AND p."CorpusId" = NEW."CorpusId";
                ELSE RAISE EXCEPTION 'Unknown October publication parent' USING ERRCODE = '23514';
                END CASE;
                IF parent_xid IS NULL OR parent_xid <> pg_current_xact_id()::xid THEN
                    RAISE EXCEPTION 'Cannot extend committed October publication' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE FUNCTION october."RejectFrozenOctoberAdmission"() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                PERFORM 1 FROM october."OctoberBatches" WHERE "BatchId" = NEW."BatchId" FOR SHARE;
                IF EXISTS (SELECT 1 FROM october."OctoberCorpusSelections" WHERE "BatchId" = NEW."BatchId") THEN
                    RAISE EXCEPTION 'Cannot accept files after corpus freeze' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER "FrozenCorpusAdmission" BEFORE INSERT ON october."AcceptedFileManifests"
                FOR EACH ROW EXECUTE FUNCTION october."RejectFrozenOctoberAdmission"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberCorpusSelections" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberCorpusSelections" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberCorpusSelections" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberPublicationParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberCorpusManifests" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberCorpusManifests" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberCorpusManifests" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberPublicationParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberCorpusDocuments" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberCorpusDocuments" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberCorpusDocuments" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberPublicationParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberCorpusCoverage" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberCorpusCoverage" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberCorpusCoverage" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberPublicationParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberKnowledgePublications" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberKnowledgePublications" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberKnowledgePublications" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberPublicationParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberPublicationMembers" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberPublicationMembers" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberPublicationMembers" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberPublicationParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberPublicationCoverage" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberPublicationCoverage" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberPublicationCoverage" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberPublicationParent"();
            CREATE TRIGGER "ImmutableHistory" BEFORE UPDATE OR DELETE ON october."OctoberPublicationUncertainties" FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "ImmutableHistoryTruncate" BEFORE TRUNCATE ON october."OctoberPublicationUncertainties" FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
            CREATE TRIGGER "NewParentOnly" BEFORE INSERT ON october."OctoberPublicationUncertainties" FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberPublicationParent"();

            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER \"FrozenCorpusAdmission\" ON october.\"AcceptedFileManifests\"; DROP FUNCTION october.\"RejectFrozenOctoberAdmission\"();");
        migrationBuilder.DropTable(name: "OctoberPublicationUncertainties", schema: "october");
        migrationBuilder.DropTable(name: "OctoberPublicationCoverage", schema: "october");
        migrationBuilder.DropTable(name: "OctoberPublicationMembers", schema: "october");
        migrationBuilder.DropTable(name: "OctoberKnowledgePublications", schema: "october");
        migrationBuilder.DropTable(name: "OctoberCorpusCoverage", schema: "october");
        migrationBuilder.DropTable(name: "OctoberCorpusDocuments", schema: "october");
        migrationBuilder.DropTable(name: "OctoberCorpusManifests", schema: "october");
        migrationBuilder.DropTable(name: "OctoberCorpusSelections", schema: "october");
        migrationBuilder.Sql("DROP FUNCTION october.\"RequireNewOctoberPublicationParent\"();");
        migrationBuilder.DropUniqueConstraint(name: "AK_OctoberDocumentVersions_BatchId_ManifestId_LogicalDocumentI~", schema: "october", table: "OctoberDocumentVersions");
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

    }
}
