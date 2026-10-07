using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.October.Migrations
{
    /// <inheritdoc />
    public partial class AddOctoberFlowPublications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep the native manifest-qualified CL FK and UNIQUE constraint. PostgreSQL
            // permits NULL on the BL side of UNIQUE, whereas an EF alternate key would force
            // ManifestId required. The common exact-version FK below is additional protection.

            migrationBuilder.DropForeignKey(
                name: "FK_OctoberDocumentVersions_OctoberLogicalDocuments_BatchId_Log~",
                schema: "october",
                table: "OctoberDocumentVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_OctoberPublicationMembers_OctoberCorpusDocuments_BatchId_Co~",
                schema: "october",
                table: "OctoberPublicationMembers");

            migrationBuilder.DropIndex(
                name: "IX_OctoberDocumentVersions_BatchId_LogicalDocumentId",
                schema: "october",
                table: "OctoberDocumentVersions");

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                schema: "october",
                table: "OctoberLogicalDocuments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "LogicalFlowId",
                schema: "october",
                table: "OctoberLogicalDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ManifestId",
                schema: "october",
                table: "OctoberDocumentVersions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "FinalizationOperationId",
                schema: "october",
                table: "OctoberDocumentVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinalizationOperationKind",
                schema: "october",
                table: "OctoberDocumentVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FlowGroupVersionId",
                schema: "october",
                table: "OctoberDocumentVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                schema: "october",
                table: "OctoberDocumentVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_OctoberLogicalDocuments_BatchId_LogicalDocumentId_Kind",
                schema: "october",
                table: "OctoberLogicalDocuments",
                columns: new[] { "BatchId", "LogicalDocumentId", "Kind" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_OctoberDocumentVersions_BatchId_LogicalDocumentId_VersionId",
                schema: "october",
                table: "OctoberDocumentVersions",
                columns: new[] { "BatchId", "LogicalDocumentId", "VersionId" });

            migrationBuilder.CreateTable(
                name: "OctoberBusinessFinalizations",
                schema: "october",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationKind = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OutcomeDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    ContributorsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OctoberBusinessFinalizations", x => new { x.BatchId, x.OperationKind, x.OperationId });
                    table.ForeignKey(
                        name: "FK_OctoberBusinessFinalizations_KnowledgeOperations_BatchId_Op~",
                        columns: x => new { x.BatchId, x.OperationKind, x.OperationId },
                        principalSchema: "october",
                        principalTable: "KnowledgeOperations",
                        principalColumns: new[] { "BatchId", "Kind", "OperationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OctoberBusinessFinalizations_OctoberCorpusSelections_BatchI~",
                        columns: x => new { x.BatchId, x.CorpusId },
                        principalSchema: "october",
                        principalTable: "OctoberCorpusSelections",
                        principalColumns: new[] { "BatchId", "CorpusId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OctoberBusinessFinalizations_OctoberKnowledgePublications_B~",
                        columns: x => new { x.BatchId, x.PublicationId },
                        principalSchema: "october",
                        principalTable: "OctoberKnowledgePublications",
                        principalColumns: new[] { "BatchId", "PublicationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OctoberLogicalFlows",
                schema: "october",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalFlowId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootKind = table.Column<int>(type: "integer", nullable: false),
                    RootSelector = table.Column<string>(type: "text", nullable: false),
                    HomeRepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationKind = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OctoberLogicalFlows", x => new { x.BatchId, x.LogicalFlowId });
                    table.ForeignKey(
                        name: "FK_OctoberLogicalFlows_KnowledgeOperations_BatchId_OperationKi~",
                        columns: x => new { x.BatchId, x.OperationKind, x.OperationId },
                        principalSchema: "october",
                        principalTable: "KnowledgeOperations",
                        principalColumns: new[] { "BatchId", "Kind", "OperationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OctoberLogicalFlows_OctoberCodeUnits_BatchId_RootUnitId",
                        columns: x => new { x.BatchId, x.RootUnitId },
                        principalSchema: "october",
                        principalTable: "OctoberCodeUnits",
                        principalColumns: new[] { "BatchId", "UnitId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OctoberLogicalFlows_OctoberRepositorySelections_BatchId_Hom~",
                        columns: x => new { x.BatchId, x.HomeRepositoryId },
                        principalSchema: "october",
                        principalTable: "OctoberRepositorySelections",
                        principalColumns: new[] { "BatchId", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OctoberFlowGroupVersions",
                schema: "october",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalFlowId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootLogicalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationKind = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupJson = table.Column<string>(type: "jsonb", nullable: false),
                    ContributorsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OctoberFlowGroupVersions", x => new { x.BatchId, x.GroupVersionId });
                    table.UniqueConstraint("AK_OctoberFlowGroupVersions_BatchId_CorpusId_GroupVersionId", x => new { x.BatchId, x.CorpusId, x.GroupVersionId });
                    table.ForeignKey(
                        name: "FK_OctoberFlowGroupVersions_KnowledgeOperations_BatchId_Operat~",
                        columns: x => new { x.BatchId, x.OperationKind, x.OperationId },
                        principalSchema: "october",
                        principalTable: "KnowledgeOperations",
                        principalColumns: new[] { "BatchId", "Kind", "OperationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OctoberFlowGroupVersions_OctoberCorpusDocuments_BatchId_Cor~",
                        columns: x => new { x.BatchId, x.CorpusId, x.RootLogicalDocumentId, x.RootVersionId },
                        principalSchema: "october",
                        principalTable: "OctoberCorpusDocuments",
                        principalColumns: new[] { "BatchId", "CorpusId", "LogicalDocumentId", "VersionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OctoberFlowGroupVersions_OctoberLogicalFlows_BatchId_Logica~",
                        columns: x => new { x.BatchId, x.LogicalFlowId },
                        principalSchema: "october",
                        principalTable: "OctoberLogicalFlows",
                        principalColumns: new[] { "BatchId", "LogicalFlowId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OctoberFlowDocumentDependencies",
                schema: "october",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsMember = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OctoberFlowDocumentDependencies", x => new { x.BatchId, x.GroupVersionId, x.LogicalDocumentId, x.VersionId });
                    table.ForeignKey(
                        name: "FK_OctoberFlowDocumentDependencies_OctoberCorpusDocuments_Batc~",
                        columns: x => new { x.BatchId, x.CorpusId, x.LogicalDocumentId, x.VersionId },
                        principalSchema: "october",
                        principalTable: "OctoberCorpusDocuments",
                        principalColumns: new[] { "BatchId", "CorpusId", "LogicalDocumentId", "VersionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OctoberFlowDocumentDependencies_OctoberFlowGroupVersions_Ba~",
                        columns: x => new { x.BatchId, x.CorpusId, x.GroupVersionId },
                        principalSchema: "october",
                        principalTable: "OctoberFlowGroupVersions",
                        principalColumns: new[] { "BatchId", "CorpusId", "GroupVersionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OctoberFlowFileUncertaintyDependencies",
                schema: "october",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    CorpusId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OctoberFlowFileUncertaintyDependencies", x => new { x.BatchId, x.GroupVersionId, x.ManifestId, x.Key });
                    table.ForeignKey(
                        name: "FK_OctoberFlowFileUncertaintyDependencies_OctoberCorpusManifes~",
                        columns: x => new { x.BatchId, x.CorpusId, x.ManifestId },
                        principalSchema: "october",
                        principalTable: "OctoberCorpusManifests",
                        principalColumns: new[] { "BatchId", "CorpusId", "ManifestId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OctoberFlowFileUncertaintyDependencies_OctoberFlowGroupVers~",
                        columns: x => new { x.BatchId, x.CorpusId, x.GroupVersionId },
                        principalSchema: "october",
                        principalTable: "OctoberFlowGroupVersions",
                        principalColumns: new[] { "BatchId", "CorpusId", "GroupVersionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OctoberFlowFileUncertaintyDependencies_OctoberKnowledgeUnce~",
                        columns: x => new { x.BatchId, x.ManifestId, x.Key },
                        principalSchema: "october",
                        principalTable: "OctoberKnowledgeUncertainties",
                        principalColumns: new[] { "BatchId", "ManifestId", "Key" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OctoberFlowRelationshipDependencies",
                schema: "october",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsMember = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OctoberFlowRelationshipDependencies", x => new { x.BatchId, x.GroupVersionId, x.RelationshipVersionId });
                    table.ForeignKey(
                        name: "FK_OctoberFlowRelationshipDependencies_OctoberFlowGroupVersion~",
                        columns: x => new { x.BatchId, x.CorpusId, x.GroupVersionId },
                        principalSchema: "october",
                        principalTable: "OctoberFlowGroupVersions",
                        principalColumns: new[] { "BatchId", "CorpusId", "GroupVersionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OctoberFlowRelationshipDependencies_OctoberRelationshipVers~",
                        columns: x => new { x.BatchId, x.CorpusId, x.RelationshipVersionId },
                        principalSchema: "october",
                        principalTable: "OctoberRelationshipVersions",
                        principalColumns: new[] { "BatchId", "CorpusId", "RelationshipVersionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OctoberFlowResolutionUncertaintyDependencies",
                schema: "october",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UncertaintyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsSliceQualifier = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OctoberFlowResolutionUncertaintyDependencies", x => new { x.BatchId, x.GroupVersionId, x.UncertaintyId });
                    table.ForeignKey(
                        name: "FK_OctoberFlowResolutionUncertaintyDependencies_OctoberFlowGro~",
                        columns: x => new { x.BatchId, x.CorpusId, x.GroupVersionId },
                        principalSchema: "october",
                        principalTable: "OctoberFlowGroupVersions",
                        principalColumns: new[] { "BatchId", "CorpusId", "GroupVersionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OctoberFlowResolutionUncertaintyDependencies_OctoberUncerta~",
                        columns: x => new { x.BatchId, x.CorpusId, x.UncertaintyId },
                        principalSchema: "october",
                        principalTable: "OctoberUncertaintyVersions",
                        principalColumns: new[] { "BatchId", "CorpusId", "UncertaintyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberPublicationMembers_BatchId_LogicalDocumentId_Version~",
                schema: "october",
                table: "OctoberPublicationMembers",
                columns: new[] { "BatchId", "LogicalDocumentId", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberLogicalDocuments_BatchId_LogicalFlowId",
                schema: "october",
                table: "OctoberLogicalDocuments",
                columns: new[] { "BatchId", "LogicalFlowId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LogicalDocument_Owner",
                schema: "october",
                table: "OctoberLogicalDocuments",
                sql: "(\"Kind\" = 0 AND \"UnitId\" IS NOT NULL AND \"LogicalFlowId\" IS NULL) OR (\"Kind\" = 1 AND \"UnitId\" IS NULL AND \"LogicalFlowId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_OctoberDocumentVersions_BatchId_FinalizationOperationKind_F~",
                schema: "october",
                table: "OctoberDocumentVersions",
                columns: new[] { "BatchId", "FinalizationOperationKind", "FinalizationOperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberDocumentVersions_BatchId_FlowGroupVersionId",
                schema: "october",
                table: "OctoberDocumentVersions",
                columns: new[] { "BatchId", "FlowGroupVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberDocumentVersions_BatchId_LogicalDocumentId_Kind",
                schema: "october",
                table: "OctoberDocumentVersions",
                columns: new[] { "BatchId", "LogicalDocumentId", "Kind" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentVersion_Owner",
                schema: "october",
                table: "OctoberDocumentVersions",
                sql: "(\"Kind\" = 0 AND \"ManifestId\" IS NOT NULL AND \"FlowGroupVersionId\" IS NULL AND \"FinalizationOperationKind\" IS NULL AND \"FinalizationOperationId\" IS NULL) OR (\"Kind\" = 1 AND \"ManifestId\" IS NULL AND \"FlowGroupVersionId\" IS NOT NULL AND \"FinalizationOperationKind\" IS NOT NULL AND \"FinalizationOperationKind\" = 3 AND \"FinalizationOperationId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_OctoberCorpusDocuments_BatchId_LogicalDocumentId_VersionId",
                schema: "october",
                table: "OctoberCorpusDocuments",
                columns: new[] { "BatchId", "LogicalDocumentId", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberBusinessFinalizations_BatchId_CorpusId",
                schema: "october",
                table: "OctoberBusinessFinalizations",
                columns: new[] { "BatchId", "CorpusId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberBusinessFinalizations_BatchId_PublicationId",
                schema: "october",
                table: "OctoberBusinessFinalizations",
                columns: new[] { "BatchId", "PublicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberFlowDocumentDependencies_BatchId_CorpusId_GroupVersi~",
                schema: "october",
                table: "OctoberFlowDocumentDependencies",
                columns: new[] { "BatchId", "CorpusId", "GroupVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberFlowDocumentDependencies_BatchId_CorpusId_LogicalDoc~",
                schema: "october",
                table: "OctoberFlowDocumentDependencies",
                columns: new[] { "BatchId", "CorpusId", "LogicalDocumentId", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberFlowFileUncertaintyDependencies_BatchId_CorpusId_Gro~",
                schema: "october",
                table: "OctoberFlowFileUncertaintyDependencies",
                columns: new[] { "BatchId", "CorpusId", "GroupVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberFlowFileUncertaintyDependencies_BatchId_CorpusId_Man~",
                schema: "october",
                table: "OctoberFlowFileUncertaintyDependencies",
                columns: new[] { "BatchId", "CorpusId", "ManifestId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberFlowFileUncertaintyDependencies_BatchId_ManifestId_K~",
                schema: "october",
                table: "OctoberFlowFileUncertaintyDependencies",
                columns: new[] { "BatchId", "ManifestId", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberFlowGroupVersions_BatchId_CorpusId_RootLogicalDocume~",
                schema: "october",
                table: "OctoberFlowGroupVersions",
                columns: new[] { "BatchId", "CorpusId", "RootLogicalDocumentId", "RootVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberFlowGroupVersions_BatchId_LogicalFlowId",
                schema: "october",
                table: "OctoberFlowGroupVersions",
                columns: new[] { "BatchId", "LogicalFlowId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberFlowGroupVersions_BatchId_OperationKind_OperationId",
                schema: "october",
                table: "OctoberFlowGroupVersions",
                columns: new[] { "BatchId", "OperationKind", "OperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberFlowRelationshipDependencies_BatchId_CorpusId_GroupV~",
                schema: "october",
                table: "OctoberFlowRelationshipDependencies",
                columns: new[] { "BatchId", "CorpusId", "GroupVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberFlowRelationshipDependencies_BatchId_CorpusId_Relati~",
                schema: "october",
                table: "OctoberFlowRelationshipDependencies",
                columns: new[] { "BatchId", "CorpusId", "RelationshipVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberFlowResolutionUncertaintyDependencies_BatchId_Corpu~1",
                schema: "october",
                table: "OctoberFlowResolutionUncertaintyDependencies",
                columns: new[] { "BatchId", "CorpusId", "UncertaintyId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberFlowResolutionUncertaintyDependencies_BatchId_Corpus~",
                schema: "october",
                table: "OctoberFlowResolutionUncertaintyDependencies",
                columns: new[] { "BatchId", "CorpusId", "GroupVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberLogicalFlows_BatchId_HomeRepositoryId",
                schema: "october",
                table: "OctoberLogicalFlows",
                columns: new[] { "BatchId", "HomeRepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberLogicalFlows_BatchId_OperationKind_OperationId",
                schema: "october",
                table: "OctoberLogicalFlows",
                columns: new[] { "BatchId", "OperationKind", "OperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OctoberLogicalFlows_BatchId_RootUnitId_RootKind_RootSelector",
                schema: "october",
                table: "OctoberLogicalFlows",
                columns: new[] { "BatchId", "RootUnitId", "RootKind", "RootSelector" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OctoberCorpusDocuments_OctoberDocumentVersions_BatchId_Logi~",
                schema: "october",
                table: "OctoberCorpusDocuments",
                columns: new[] { "BatchId", "LogicalDocumentId", "VersionId" },
                principalSchema: "october",
                principalTable: "OctoberDocumentVersions",
                principalColumns: new[] { "BatchId", "LogicalDocumentId", "VersionId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OctoberDocumentVersions_OctoberBusinessFinalizations_BatchI~",
                schema: "october",
                table: "OctoberDocumentVersions",
                columns: new[] { "BatchId", "FinalizationOperationKind", "FinalizationOperationId" },
                principalSchema: "october",
                principalTable: "OctoberBusinessFinalizations",
                principalColumns: new[] { "BatchId", "OperationKind", "OperationId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OctoberDocumentVersions_OctoberFlowGroupVersions_BatchId_Fl~",
                schema: "october",
                table: "OctoberDocumentVersions",
                columns: new[] { "BatchId", "FlowGroupVersionId" },
                principalSchema: "october",
                principalTable: "OctoberFlowGroupVersions",
                principalColumns: new[] { "BatchId", "GroupVersionId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OctoberDocumentVersions_OctoberLogicalDocuments_BatchId_Log~",
                schema: "october",
                table: "OctoberDocumentVersions",
                columns: new[] { "BatchId", "LogicalDocumentId", "Kind" },
                principalSchema: "october",
                principalTable: "OctoberLogicalDocuments",
                principalColumns: new[] { "BatchId", "LogicalDocumentId", "Kind" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OctoberLogicalDocuments_OctoberLogicalFlows_BatchId_Logical~",
                schema: "october",
                table: "OctoberLogicalDocuments",
                columns: new[] { "BatchId", "LogicalFlowId" },
                principalSchema: "october",
                principalTable: "OctoberLogicalFlows",
                principalColumns: new[] { "BatchId", "LogicalFlowId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OctoberPublicationMembers_OctoberDocumentVersions_BatchId_L~",
                schema: "october",
                table: "OctoberPublicationMembers",
                columns: new[] { "BatchId", "LogicalDocumentId", "VersionId" },
                principalSchema: "october",
                principalTable: "OctoberDocumentVersions",
                principalColumns: new[] { "BatchId", "LogicalDocumentId", "VersionId" },
                onDelete: ReferentialAction.Restrict);

            InstallFlowGuards(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("October accepted flow history cannot be downgraded destructively; retain this additive schema.");
        }
    }
}
