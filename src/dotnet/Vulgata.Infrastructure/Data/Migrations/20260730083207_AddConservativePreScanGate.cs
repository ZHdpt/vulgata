using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConservativePreScanGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfiledRevision",
                schema: "vulgata",
                table: "ScanRuns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PreScanResults",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfiledRevision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ApprovalMessage = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ConfirmedCommunicationKinds = table.Column<int>(type: "integer", nullable: false),
                    TotalConfirmedEvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    TotalHintCount = table.Column<int>(type: "integer", nullable: false),
                    TotalWarningCount = table.Column<int>(type: "integer", nullable: false),
                    EvidenceTruncated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreScanResults", x => x.Id);
                    table.CheckConstraint("CK_PreScanResults_ApprovalMessage_Length", "length(\"ApprovalMessage\") BETWEEN 1 AND 256");
                    table.CheckConstraint("CK_PreScanResults_ConfirmedKinds", "\"ConfirmedCommunicationKinds\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_PreScanResults_Counts", "\"TotalConfirmedEvidenceCount\" BETWEEN 0 AND 250000 AND \"TotalHintCount\" BETWEEN 0 AND 250000 AND \"TotalWarningCount\" BETWEEN 0 AND 250000");
                    table.CheckConstraint("CK_PreScanResults_Fingerprint_Length", "length(\"Fingerprint\") = 64");
                    table.CheckConstraint("CK_PreScanResults_Identity", "\"Id\" = \"ScanRunId\"");
                    table.CheckConstraint("CK_PreScanResults_ProfiledRevision_Length", "length(\"ProfiledRevision\") BETWEEN 1 AND 64");
                    table.CheckConstraint("CK_PreScanResults_SchemaVersion", "\"SchemaVersion\" = '1.0'");
                    table.CheckConstraint("CK_PreScanResults_State", "\"State\" = 'PreScanApproved'");
                    table.ForeignKey(
                        name: "FK_PreScanResults_ScanRuns_Id_RepositoryId",
                        columns: x => new { x.Id, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationKindClassifications",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PreScanResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Confidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Rule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RelativePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Locator = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    OccurrenceCount = table.Column<int>(type: "integer", nullable: false),
                    Fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationKindClassifications", x => x.Id);
                    table.CheckConstraint("CK_CommunicationKindClassifications_Compatibility", "(\"Rule\" = 'RpcDirectAnnotation' AND \"Kind\" = 'Rpc' AND \"Level\" = 'Confirmed' AND \"Confidence\" = 'High' AND \"Provenance\" = 'StructuralAnnotation') OR (\"Rule\" = 'RpcMatchedConfiguration' AND \"Kind\" = 'Rpc' AND \"Level\" = 'Confirmed' AND \"Confidence\" = 'High' AND \"Provenance\" = 'MatchedConfiguration') OR (\"Rule\" IN ('HttpControllerAnnotation', 'HttpClientAnnotation') AND \"Kind\" = 'Http' AND \"Level\" = 'Confirmed' AND \"Confidence\" = 'High' AND \"Provenance\" = 'StructuralAnnotation') OR (\"Rule\" = 'HttpMatchedConfiguration' AND \"Kind\" = 'Http' AND \"Level\" = 'Confirmed' AND \"Confidence\" = 'High' AND \"Provenance\" = 'MatchedConfiguration') OR (\"Rule\" = 'RpcImportOnly' AND \"Kind\" = 'Rpc' AND \"Level\" = 'Hint' AND \"Confidence\" = 'Conservative' AND \"Provenance\" IN ('Import', 'PackageManifest', 'FrameworkFact')) OR (\"Rule\" = 'HttpImportOnly' AND \"Kind\" = 'Http' AND \"Level\" = 'Hint' AND \"Confidence\" = 'Conservative' AND \"Provenance\" IN ('Import', 'PackageManifest', 'FrameworkFact')) OR (\"Rule\" IN ('MalformedEvidence', 'UnsafePath', 'BoundExceeded', 'ContradictoryEvidence') AND \"Level\" = 'Warning' AND \"Confidence\" = 'None' AND \"Provenance\" = 'Detector') OR (\"Rule\" = 'UnsupportedFamily' AND \"Kind\" IS NULL AND \"Level\" = 'Warning' AND \"Confidence\" = 'None' AND \"Provenance\" = 'Detector')");
                    table.CheckConstraint("CK_CommunicationKindClassifications_Confidence", "\"Confidence\" IN ('High', 'Conservative', 'None')");
                    table.CheckConstraint("CK_CommunicationKindClassifications_Fingerprint_Length", "length(\"Fingerprint\") = 64");
                    table.CheckConstraint("CK_CommunicationKindClassifications_Kind", "\"Kind\" IS NULL OR \"Kind\" IN ('Rpc', 'Http')");
                    table.CheckConstraint("CK_CommunicationKindClassifications_Level", "\"Level\" IN ('Confirmed', 'Hint', 'Warning')");
                    table.CheckConstraint("CK_CommunicationKindClassifications_LevelConfidence", "(\"Level\" = 'Confirmed' AND \"Confidence\" = 'High' AND \"Kind\" IS NOT NULL) OR (\"Level\" = 'Hint' AND \"Confidence\" = 'Conservative' AND \"Kind\" IS NOT NULL) OR (\"Level\" = 'Warning' AND \"Confidence\" = 'None')");
                    table.CheckConstraint("CK_CommunicationKindClassifications_OccurrenceCount", "\"OccurrenceCount\" BETWEEN 1 AND 250000");
                    table.CheckConstraint("CK_CommunicationKindClassifications_PathLocator", "length(\"RelativePath\") BETWEEN 1 AND 2048 AND length(\"Locator\") BETWEEN 1 AND 4096");
                    table.CheckConstraint("CK_CommunicationKindClassifications_Provenance", "\"Provenance\" IN ('StructuralAnnotation', 'FrameworkFact', 'PackageManifest', 'MatchedConfiguration', 'Import', 'Detector')");
                    table.CheckConstraint("CK_CommunicationKindClassifications_Rule", "\"Rule\" IN ('RpcDirectAnnotation', 'RpcMatchedConfiguration', 'HttpControllerAnnotation', 'HttpClientAnnotation', 'HttpMatchedConfiguration', 'RpcImportOnly', 'HttpImportOnly', 'MalformedEvidence', 'UnsafePath', 'BoundExceeded', 'ContradictoryEvidence', 'UnsupportedFamily')");
                    table.ForeignKey(
                        name: "FK_CommunicationKindClassifications_PreScanResults_PreScanResu~",
                        column: x => x.PreScanResultId,
                        principalSchema: "vulgata",
                        principalTable: "PreScanResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationKindClassifications_PreScanResultId_Fingerprint",
                schema: "vulgata",
                table: "CommunicationKindClassifications",
                columns: new[] { "PreScanResultId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationKindClassifications_PreScanResultId_Level_Kind",
                schema: "vulgata",
                table: "CommunicationKindClassifications",
                columns: new[] { "PreScanResultId", "Level", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_PreScanResults_Id_RepositoryId",
                schema: "vulgata",
                table: "PreScanResults",
                columns: new[] { "Id", "RepositoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreScanResults_RepositoryId_ScanRunId",
                schema: "vulgata",
                table: "PreScanResults",
                columns: new[] { "RepositoryId", "ScanRunId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunicationKindClassifications",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "PreScanResults",
                schema: "vulgata");

            migrationBuilder.DropColumn(
                name: "ProfiledRevision",
                schema: "vulgata",
                table: "ScanRuns");
        }
    }
}
