using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeGraphProfiling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScanRuns",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TotalCandidateFileCount = table.Column<int>(type: "integer", nullable: false),
                    ParsedFileCount = table.Column<int>(type: "integer", nullable: false),
                    ProviderProcessTimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    NeedsHumanReview = table.Column<bool>(type: "boolean", nullable: false),
                    TerminalMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanRuns", x => x.Id);
                    table.UniqueConstraint("AK_ScanRuns_Id_RepositoryId", x => new { x.Id, x.RepositoryId });
                    table.ForeignKey(
                        name: "FK_ScanRuns_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "vulgata",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeUnits",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    FileIdentityHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SourceHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    LineStart = table.Column<int>(type: "integer", nullable: false),
                    LineEnd = table.Column<int>(type: "integer", nullable: false),
                    SymbolName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    UnitType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RangeConfidence = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Language = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeUnits", x => x.Id);
                    table.UniqueConstraint("AK_CodeUnits_Id_ScanRunId_RepositoryId", x => new { x.Id, x.ScanRunId, x.RepositoryId });
                    table.ForeignKey(
                        name: "FK_CodeUnits_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "vulgata",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CodeUnits_ScanRuns_ScanRunId_RepositoryId",
                        columns: x => new { x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScanCandidateOutcomes",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonOrCategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AuditGroupOrdinal = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanCandidateOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanCandidateOutcomes_ScanRuns_ScanRunId",
                        column: x => x.ScanRunId,
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CodeUnitEdges",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCodeUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetCodeUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    EdgeType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeUnitEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeUnitEdges_CodeUnits_SourceCodeUnitId_ScanRunId_Reposito~",
                        columns: x => new { x.SourceCodeUnitId, x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "CodeUnits",
                        principalColumns: new[] { "Id", "ScanRunId", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CodeUnitEdges_CodeUnits_TargetCodeUnitId_ScanRunId_Reposito~",
                        columns: x => new { x.TargetCodeUnitId, x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "CodeUnits",
                        principalColumns: new[] { "Id", "ScanRunId", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CodeUnitEdges_ScanRuns_ScanRunId_RepositoryId",
                        columns: x => new { x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnresolvedStructuralRelationshipCandidates",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCodeUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderTargetEndpointIdentity = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnresolvedStructuralRelationshipCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnresolvedStructuralRelationshipCandidates_CodeUnits_Source~",
                        columns: x => new { x.SourceCodeUnitId, x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "CodeUnits",
                        principalColumns: new[] { "Id", "ScanRunId", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnresolvedStructuralRelationshipCandidates_ScanRuns_ScanRun~",
                        columns: x => new { x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodeUnitEdges_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "CodeUnitEdges",
                columns: new[] { "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeUnitEdges_SourceCodeUnitId_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "CodeUnitEdges",
                columns: new[] { "SourceCodeUnitId", "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeUnitEdges_SourceCodeUnitId_TargetCodeUnitId_EdgeType",
                schema: "vulgata",
                table: "CodeUnitEdges",
                columns: new[] { "SourceCodeUnitId", "TargetCodeUnitId", "EdgeType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeUnitEdges_TargetCodeUnitId_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "CodeUnitEdges",
                columns: new[] { "TargetCodeUnitId", "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeUnits_RepositoryId",
                schema: "vulgata",
                table: "CodeUnits",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeUnits_ScanRunId_FilePath_LineStart_LineEnd_SymbolName_U~",
                schema: "vulgata",
                table: "CodeUnits",
                columns: new[] { "ScanRunId", "FilePath", "LineStart", "LineEnd", "SymbolName", "UnitType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeUnits_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "CodeUnits",
                columns: new[] { "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanCandidateOutcomes_ScanRunId_FilePath_Outcome_ReasonOrCa~",
                schema: "vulgata",
                table: "ScanCandidateOutcomes",
                columns: new[] { "ScanRunId", "FilePath", "Outcome", "ReasonOrCategory" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanRuns_RepositoryId_StartedAt",
                schema: "vulgata",
                table: "ScanRuns",
                columns: new[] { "RepositoryId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UnresolvedStructuralRelationshipCandidates_ScanRunId_Reposi~",
                schema: "vulgata",
                table: "UnresolvedStructuralRelationshipCandidates",
                columns: new[] { "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_UnresolvedStructuralRelationshipCandidates_SourceCodeUnitI~1",
                schema: "vulgata",
                table: "UnresolvedStructuralRelationshipCandidates",
                columns: new[] { "SourceCodeUnitId", "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_UnresolvedStructuralRelationshipCandidates_SourceCodeUnitId~",
                schema: "vulgata",
                table: "UnresolvedStructuralRelationshipCandidates",
                columns: new[] { "SourceCodeUnitId", "RelationshipKind", "ProviderTargetEndpointIdentity" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodeUnitEdges",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "ScanCandidateOutcomes",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "UnresolvedStructuralRelationshipCandidates",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "CodeUnits",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "ScanRuns",
                schema: "vulgata");
        }
    }
}
