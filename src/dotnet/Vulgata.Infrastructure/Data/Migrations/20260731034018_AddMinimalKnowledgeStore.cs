using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMinimalKnowledgeStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentEdges",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetRepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    EdgeType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentEdges", x => x.Id);
                    table.CheckConstraint("CK_DocumentEdges_EdgeType_Closed", "\"EdgeType\" IN ('References', 'GeneratedFrom')");
                });

            migrationBuilder.CreateTable(
                name: "DocumentSections",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StableSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionOrder = table.Column<int>(type: "integer", nullable: false),
                    SectionKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Heading = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    ContentMarkdown = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    SearchText = table.Column<string>(type: "character varying(131072)", maxLength: 131072, nullable: false),
                    SourceAnchorIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    DocumentVersionIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSections", x => x.Id);
                    table.CheckConstraint("CK_DocumentSections_SectionKind_Closed", "\"SectionKind\" IN ('Overview', 'BusinessRule', 'ProcessStep', 'TechnicalDetail', 'Evidence', 'Other')");
                    table.CheckConstraint("CK_DocumentSections_SectionOrder_Positive", "\"SectionOrder\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersions",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfiledRevision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    RepositoryRelativePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    SchemaIdentity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    ContentMarkdown = table.Column<string>(type: "character varying(1048576)", maxLength: 1048576, nullable: false),
                    ContentFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SupersededById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersions", x => x.Id);
                    table.UniqueConstraint("AK_DocumentVersions_Id_LogicalDocumentId_RepositoryId", x => new { x.Id, x.LogicalDocumentId, x.RepositoryId });
                    table.UniqueConstraint("AK_DocumentVersions_Id_RepositoryId_ScanRunId", x => new { x.Id, x.RepositoryId, x.ScanRunId });
                    table.CheckConstraint("CK_DocumentVersions_ContentFingerprint_Length", "length(\"ContentFingerprint\") = 64");
                    table.CheckConstraint("CK_DocumentVersions_SchemaIdentity_Closed", "\"SchemaIdentity\" = 'vulgata.knowledge-document/1.0'");
                    table.CheckConstraint("CK_DocumentVersions_SupersededBy_NotSelf", "\"SupersededById\" IS NULL OR \"SupersededById\" <> \"Id\"");
                    table.CheckConstraint("CK_DocumentVersions_VersionNumber_Positive", "\"VersionNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_DocumentVersions_DocumentVersions_SupersededById_LogicalDoc~",
                        columns: x => new { x.SupersededById, x.LogicalDocumentId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "DocumentVersions",
                        principalColumns: new[] { "Id", "LogicalDocumentId", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_ScanRuns_ScanRunId_RepositoryId",
                        columns: x => new { x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FlowGroups",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessLogicDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupOrder = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowGroups", x => x.Id);
                    table.CheckConstraint("CK_FlowGroups_GroupOrder_Positive", "\"GroupOrder\" > 0");
                    table.ForeignKey(
                        name: "FK_FlowGroups_DocumentVersions_BusinessLogicDocumentVersionId_~",
                        columns: x => new { x.BusinessLogicDocumentVersionId, x.RepositoryId, x.ScanRunId },
                        principalSchema: "vulgata",
                        principalTable: "DocumentVersions",
                        principalColumns: new[] { "Id", "RepositoryId", "ScanRunId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IndexEntries",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Summary = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    RepositoryRelativePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    KeySymbolsJson = table.Column<string>(type: "jsonb", nullable: false),
                    BusinessTermsJson = table.Column<string>(type: "jsonb", nullable: false),
                    TechnicalTermsJson = table.Column<string>(type: "jsonb", nullable: false),
                    SearchText = table.Column<string>(type: "character varying(4194304)", maxLength: 4194304, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndexEntries_DocumentVersions_DocumentVersionId_LogicalDocu~",
                        columns: x => new { x.DocumentVersionId, x.LogicalDocumentId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "DocumentVersions",
                        principalColumns: new[] { "Id", "LogicalDocumentId", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LogicalDocuments",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StableKey = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogicalDocuments", x => x.Id);
                    table.UniqueConstraint("AK_LogicalDocuments_Id_RepositoryId", x => new { x.Id, x.RepositoryId });
                    table.CheckConstraint("CK_LogicalDocuments_DocumentType_Closed", "\"DocumentType\" IN ('CodeLogic', 'BusinessLogic')");
                    table.ForeignKey(
                        name: "FK_LogicalDocuments_DocumentVersions_CurrentVersionId_Id_Repos~",
                        columns: x => new { x.CurrentVersionId, x.Id, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "DocumentVersions",
                        principalColumns: new[] { "Id", "LogicalDocumentId", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LogicalDocuments_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "vulgata",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourceAnchors",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfiledRevision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RepositoryRelativePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    EvidenceLocator = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    LineStart = table.Column<int>(type: "integer", nullable: false),
                    LineEnd = table.Column<int>(type: "integer", nullable: false),
                    SourceHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceAnchors", x => x.Id);
                    table.CheckConstraint("CK_SourceAnchors_LineEnd_Range", "\"LineEnd\" >= \"LineStart\"");
                    table.CheckConstraint("CK_SourceAnchors_LineStart_Positive", "\"LineStart\" > 0");
                    table.ForeignKey(
                        name: "FK_SourceAnchors_CodeUnits_CodeUnitId_ScanRunId_RepositoryId",
                        columns: x => new { x.CodeUnitId, x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "CodeUnits",
                        principalColumns: new[] { "Id", "ScanRunId", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourceAnchors_DocumentVersions_DocumentVersionId_Repository~",
                        columns: x => new { x.DocumentVersionId, x.RepositoryId, x.ScanRunId },
                        principalSchema: "vulgata",
                        principalTable: "DocumentVersions",
                        principalColumns: new[] { "Id", "RepositoryId", "ScanRunId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourceAnchors_ScanRuns_ScanRunId_RepositoryId",
                        columns: x => new { x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FlowTraces",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraceOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowTraces", x => x.Id);
                    table.CheckConstraint("CK_FlowTraces_TraceOrder_Positive", "\"TraceOrder\" > 0");
                    table.ForeignKey(
                        name: "FK_FlowTraces_FlowGroups_FlowGroupId",
                        column: x => x.FlowGroupId,
                        principalSchema: "vulgata",
                        principalTable: "FlowGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FlowTraceSteps",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentRepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    EdgeId = table.Column<Guid>(type: "uuid", nullable: true),
                    StepKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowTraceSteps", x => x.Id);
                    table.CheckConstraint("CK_FlowTraceSteps_StepKind_Closed", "\"StepKind\" IN ('Document', 'Relationship', 'Termination')");
                    table.CheckConstraint("CK_FlowTraceSteps_StepOrder_Positive", "\"StepOrder\" > 0");
                    table.ForeignKey(
                        name: "FK_FlowTraceSteps_DocumentEdges_EdgeId",
                        column: x => x.EdgeId,
                        principalSchema: "vulgata",
                        principalTable: "DocumentEdges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FlowTraceSteps_DocumentVersions_DocumentVersionId_DocumentR~",
                        columns: x => new { x.DocumentVersionId, x.DocumentRepositoryId, x.DocumentScanRunId },
                        principalSchema: "vulgata",
                        principalTable: "DocumentVersions",
                        principalColumns: new[] { "Id", "RepositoryId", "ScanRunId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FlowTraceSteps_FlowTraces_FlowTraceId",
                        column: x => x.FlowTraceId,
                        principalSchema: "vulgata",
                        principalTable: "FlowTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEdges_SourceDocumentVersionId_SourceRepositoryId_So~",
                schema: "vulgata",
                table: "DocumentEdges",
                columns: new[] { "SourceDocumentVersionId", "SourceRepositoryId", "SourceScanRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEdges_SourceDocumentVersionId_TargetDocumentVersion~",
                schema: "vulgata",
                table: "DocumentEdges",
                columns: new[] { "SourceDocumentVersionId", "TargetDocumentVersionId", "EdgeType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEdges_SourceRepositoryId_SourceDocumentVersionId",
                schema: "vulgata",
                table: "DocumentEdges",
                columns: new[] { "SourceRepositoryId", "SourceDocumentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEdges_TargetDocumentVersionId_TargetRepositoryId_Ta~",
                schema: "vulgata",
                table: "DocumentEdges",
                columns: new[] { "TargetDocumentVersionId", "TargetRepositoryId", "TargetScanRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEdges_TargetRepositoryId_TargetDocumentVersionId",
                schema: "vulgata",
                table: "DocumentEdges",
                columns: new[] { "TargetRepositoryId", "TargetDocumentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSections_DocumentVersionId_LogicalDocumentId_Reposi~",
                schema: "vulgata",
                table: "DocumentSections",
                columns: new[] { "DocumentVersionId", "LogicalDocumentId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSections_DocumentVersionId_SectionOrder",
                schema: "vulgata",
                table: "DocumentSections",
                columns: new[] { "DocumentVersionId", "SectionOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSections_DocumentVersionId_StableSectionId",
                schema: "vulgata",
                table: "DocumentSections",
                columns: new[] { "DocumentVersionId", "StableSectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSections_RepositoryId_LogicalDocumentId",
                schema: "vulgata",
                table: "DocumentSections",
                columns: new[] { "RepositoryId", "LogicalDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_LogicalDocumentId_RepositoryId",
                schema: "vulgata",
                table: "DocumentVersions",
                columns: new[] { "LogicalDocumentId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_LogicalDocumentId_VersionNumber",
                schema: "vulgata",
                table: "DocumentVersions",
                columns: new[] { "LogicalDocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_RepositoryId_ScanRunId",
                schema: "vulgata",
                table: "DocumentVersions",
                columns: new[] { "RepositoryId", "ScanRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "DocumentVersions",
                columns: new[] { "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_SupersededById",
                schema: "vulgata",
                table: "DocumentVersions",
                column: "SupersededById",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_SupersededById_LogicalDocumentId_Repositor~",
                schema: "vulgata",
                table: "DocumentVersions",
                columns: new[] { "SupersededById", "LogicalDocumentId", "RepositoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlowGroups_BusinessLogicDocumentVersionId_GroupOrder",
                schema: "vulgata",
                table: "FlowGroups",
                columns: new[] { "BusinessLogicDocumentVersionId", "GroupOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlowGroups_BusinessLogicDocumentVersionId_RepositoryId_Scan~",
                schema: "vulgata",
                table: "FlowGroups",
                columns: new[] { "BusinessLogicDocumentVersionId", "RepositoryId", "ScanRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_FlowGroups_RepositoryId_BusinessLogicDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups",
                columns: new[] { "RepositoryId", "BusinessLogicDocumentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_FlowTraces_FlowGroupId_TraceOrder",
                schema: "vulgata",
                table: "FlowTraces",
                columns: new[] { "FlowGroupId", "TraceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlowTraceSteps_DocumentRepositoryId_DocumentVersionId",
                schema: "vulgata",
                table: "FlowTraceSteps",
                columns: new[] { "DocumentRepositoryId", "DocumentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_FlowTraceSteps_DocumentVersionId_DocumentRepositoryId_Docum~",
                schema: "vulgata",
                table: "FlowTraceSteps",
                columns: new[] { "DocumentVersionId", "DocumentRepositoryId", "DocumentScanRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_FlowTraceSteps_EdgeId",
                schema: "vulgata",
                table: "FlowTraceSteps",
                column: "EdgeId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowTraceSteps_FlowTraceId_StepOrder",
                schema: "vulgata",
                table: "FlowTraceSteps",
                columns: new[] { "FlowTraceId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndexEntries_DocumentVersionId",
                schema: "vulgata",
                table: "IndexEntries",
                column: "DocumentVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndexEntries_DocumentVersionId_LogicalDocumentId_Repository~",
                schema: "vulgata",
                table: "IndexEntries",
                columns: new[] { "DocumentVersionId", "LogicalDocumentId", "RepositoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndexEntries_RepositoryId_DocumentType_Title",
                schema: "vulgata",
                table: "IndexEntries",
                columns: new[] { "RepositoryId", "DocumentType", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_LogicalDocuments_CurrentVersionId_Id_RepositoryId",
                schema: "vulgata",
                table: "LogicalDocuments",
                columns: new[] { "CurrentVersionId", "Id", "RepositoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogicalDocuments_RepositoryId_CurrentVersionId",
                schema: "vulgata",
                table: "LogicalDocuments",
                columns: new[] { "RepositoryId", "CurrentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_LogicalDocuments_RepositoryId_DocumentType_StableKey",
                schema: "vulgata",
                table: "LogicalDocuments",
                columns: new[] { "RepositoryId", "DocumentType", "StableKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceAnchors_CodeUnitId_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "SourceAnchors",
                columns: new[] { "CodeUnitId", "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceAnchors_DocumentVersionId_RepositoryId_ScanRunId",
                schema: "vulgata",
                table: "SourceAnchors",
                columns: new[] { "DocumentVersionId", "RepositoryId", "ScanRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceAnchors_RepositoryId_DocumentVersionId_CodeUnitId",
                schema: "vulgata",
                table: "SourceAnchors",
                columns: new[] { "RepositoryId", "DocumentVersionId", "CodeUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceAnchors_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "SourceAnchors",
                columns: new[] { "ScanRunId", "RepositoryId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentEdges_DocumentVersions_SourceDocumentVersionId_Sour~",
                schema: "vulgata",
                table: "DocumentEdges",
                columns: new[] { "SourceDocumentVersionId", "SourceRepositoryId", "SourceScanRunId" },
                principalSchema: "vulgata",
                principalTable: "DocumentVersions",
                principalColumns: new[] { "Id", "RepositoryId", "ScanRunId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentEdges_DocumentVersions_TargetDocumentVersionId_Targ~",
                schema: "vulgata",
                table: "DocumentEdges",
                columns: new[] { "TargetDocumentVersionId", "TargetRepositoryId", "TargetScanRunId" },
                principalSchema: "vulgata",
                principalTable: "DocumentVersions",
                principalColumns: new[] { "Id", "RepositoryId", "ScanRunId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentSections_DocumentVersions_DocumentVersionId_Logical~",
                schema: "vulgata",
                table: "DocumentSections",
                columns: new[] { "DocumentVersionId", "LogicalDocumentId", "RepositoryId" },
                principalSchema: "vulgata",
                principalTable: "DocumentVersions",
                principalColumns: new[] { "Id", "LogicalDocumentId", "RepositoryId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentVersions_LogicalDocuments_LogicalDocumentId_Reposit~",
                schema: "vulgata",
                table: "DocumentVersions",
                columns: new[] { "LogicalDocumentId", "RepositoryId" },
                principalSchema: "vulgata",
                principalTable: "LogicalDocuments",
                principalColumns: new[] { "Id", "RepositoryId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(
                    """
                    PRAGMA foreign_keys=OFF;
                    DROP TABLE "DocumentSections";
                    DROP TABLE "FlowTraceSteps";
                    DROP TABLE "IndexEntries";
                    DROP TABLE "SourceAnchors";
                    DROP TABLE "DocumentEdges";
                    DROP TABLE "FlowTraces";
                    DROP TABLE "FlowGroups";
                    DROP TABLE "DocumentVersions";
                    DROP TABLE "LogicalDocuments";
                    PRAGMA foreign_keys=ON;
                    """,
                    suppressTransaction: true);
                return;
            }

            migrationBuilder.DropForeignKey(
                name: "FK_LogicalDocuments_DocumentVersions_CurrentVersionId_Id_Repos~",
                schema: "vulgata",
                table: "LogicalDocuments");

            migrationBuilder.DropTable(
                name: "DocumentSections",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "FlowTraceSteps",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "IndexEntries",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "SourceAnchors",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "DocumentEdges",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "FlowTraces",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "FlowGroups",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "DocumentVersions",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "LogicalDocuments",
                schema: "vulgata");
        }
    }
}
