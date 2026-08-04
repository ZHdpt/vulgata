using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExactCrossRepositoryResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_SourceAnchors_Id_DocumentVersionId_RepositoryId_ScanRunId",
                schema: "vulgata",
                table: "SourceAnchors",
                columns: new[] { "Id", "DocumentVersionId", "RepositoryId", "ScanRunId" });

            migrationBuilder.CreateTable(
                name: "ExactCrossRepositoryEdgeEvidence",
                schema: "vulgata",
                columns: table => new
                {
                    EdgeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerSourceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerRepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderSourceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderRepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerRawIdentifier = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ProviderRawIdentifier = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    NormalizedEndpointIdentifier = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Protocol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResolutionKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    ProfileSchemaVersion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProfileId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProfileVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProfileFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OccurrenceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ConsumerEvidenceLocator = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    ProviderEvidenceLocator = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExactCrossRepositoryEdgeEvidence", x => x.EdgeId);
                    table.CheckConstraint("CK_ExactCrossRepositoryEdgeEvidence_Confidence_Exact", "\"Confidence\" = 1.0000");
                    table.CheckConstraint("CK_ExactCrossRepositoryEdgeEvidence_Hashes_Closed", "length(\"ProfileFingerprint\") = 64 AND lower(\"ProfileFingerprint\") = \"ProfileFingerprint\" AND length(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(\"ProfileFingerprint\", '0', ''), '1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), '8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '')) = 0 AND length(\"EvidenceFingerprint\") = 64 AND lower(\"EvidenceFingerprint\") = \"EvidenceFingerprint\" AND length(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(\"EvidenceFingerprint\", '0', ''), '1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), '8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '')) = 0");
                    table.CheckConstraint("CK_ExactCrossRepositoryEdgeEvidence_Protocol_Closed", "\"Protocol\" = 'CustomRpc'");
                    table.CheckConstraint("CK_ExactCrossRepositoryEdgeEvidence_Repositories_Distinct", "\"ConsumerRepositoryId\" <> \"ProviderRepositoryId\"");
                    table.CheckConstraint("CK_ExactCrossRepositoryEdgeEvidence_ResolutionKind_Closed", "\"ResolutionKind\" = 'ExactConfigured'");
                    table.CheckConstraint("CK_ExactCrossRepositoryEdgeEvidence_SchemaVersion_Closed", "\"ProfileSchemaVersion\" = '1.0'");
                    table.ForeignKey(
                        name: "FK_ExactCrossRepositoryEdgeEvidence_DocumentEdges_EdgeId",
                        column: x => x.EdgeId,
                        principalSchema: "vulgata",
                        principalTable: "DocumentEdges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExactCrossRepositoryEdgeEvidence_SourceAnchors_ConsumerSour~",
                        columns: x => new { x.ConsumerSourceAnchorId, x.ConsumerDocumentVersionId, x.ConsumerRepositoryId, x.ConsumerScanRunId },
                        principalSchema: "vulgata",
                        principalTable: "SourceAnchors",
                        principalColumns: new[] { "Id", "DocumentVersionId", "RepositoryId", "ScanRunId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExactCrossRepositoryEdgeEvidence_SourceAnchors_ProviderSour~",
                        columns: x => new { x.ProviderSourceAnchorId, x.ProviderDocumentVersionId, x.ProviderRepositoryId, x.ProviderScanRunId },
                        principalSchema: "vulgata",
                        principalTable: "SourceAnchors",
                        principalColumns: new[] { "Id", "DocumentVersionId", "RepositoryId", "ScanRunId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Uncertainties",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SubStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ObservationKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    EndpointIdentifier = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    EndpointIdentifierNormalized = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TargetRepositoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProfileSchemaVersion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProfileId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProfileVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProfileFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OccurrenceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EvidenceLocator = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uncertainties", x => x.Id);
                    table.CheckConstraint("CK_Uncertainties_EndpointPair_Closed", "(\"EndpointIdentifier\" IS NULL) = (\"EndpointIdentifierNormalized\" IS NULL)");
                    table.CheckConstraint("CK_Uncertainties_Hashes_Closed", "length(\"ProfileFingerprint\") = 64 AND lower(\"ProfileFingerprint\") = \"ProfileFingerprint\" AND length(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(\"ProfileFingerprint\", '0', ''), '1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), '8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '')) = 0 AND length(\"EvidenceFingerprint\") = 64 AND lower(\"EvidenceFingerprint\") = \"EvidenceFingerprint\" AND length(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(\"EvidenceFingerprint\", '0', ''), '1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), '8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '')) = 0");
                    table.CheckConstraint("CK_Uncertainties_ObservationKind_Closed", "\"ObservationKind\" IS NULL OR \"ObservationKind\" = 'file_storage'");
                    table.CheckConstraint("CK_Uncertainties_ReasonCode_Closed", "\"ReasonCode\" IN ('variable_target', 'disallowed_invocation', 'malformed_provider', 'provider_out_of_root', 'unmatched', 'multiple_providers', 'normalization_mismatch', 'unsupported_kind')");
                    table.CheckConstraint("CK_Uncertainties_Resolution_Closed", "(\"Status\" = 'unresolved' AND \"ResolvedAt\" IS NULL AND \"ResolvedByDocumentVersionId\" IS NULL) OR \"Status\" <> 'unresolved'");
                    table.CheckConstraint("CK_Uncertainties_SchemaVersion_Closed", "\"ProfileSchemaVersion\" = '1.0'");
                    table.CheckConstraint("CK_Uncertainties_Status_Closed", "\"Status\" IN ('unresolved', 'resolved', 'wontfix')");
                    table.CheckConstraint("CK_Uncertainties_SubStatus_Closed", "\"SubStatus\" IN ('cross_repo', 'unknown_target', 'ambiguous')");
                    table.CheckConstraint("CK_Uncertainties_Type_Closed", "\"Type\" IN ('cross_repository', 'unsupported_communication')");
                    table.CheckConstraint("CK_Uncertainties_UnsupportedCombination_Closed", "(\"ReasonCode\" = 'unsupported_kind' AND \"Type\" = 'unsupported_communication' AND \"ObservationKind\" = 'file_storage') OR (\"ReasonCode\" <> 'unsupported_kind' AND \"Type\" = 'cross_repository' AND \"ObservationKind\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Uncertainties_DocumentVersions_DocumentVersionId_Repository~",
                        columns: x => new { x.DocumentVersionId, x.RepositoryId, x.ScanRunId },
                        principalSchema: "vulgata",
                        principalTable: "DocumentVersions",
                        principalColumns: new[] { "Id", "RepositoryId", "ScanRunId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Uncertainties_DocumentVersions_ResolvedByDocumentVersionId",
                        column: x => x.ResolvedByDocumentVersionId,
                        principalSchema: "vulgata",
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Uncertainties_Repositories_TargetRepositoryId",
                        column: x => x.TargetRepositoryId,
                        principalSchema: "vulgata",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Uncertainties_SourceAnchors_SourceAnchorId_DocumentVersionI~",
                        columns: x => new { x.SourceAnchorId, x.DocumentVersionId, x.RepositoryId, x.ScanRunId },
                        principalSchema: "vulgata",
                        principalTable: "SourceAnchors",
                        principalColumns: new[] { "Id", "DocumentVersionId", "RepositoryId", "ScanRunId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExactCrossRepositoryEdgeEvidence_ConsumerSourceAnchorId_Con~",
                schema: "vulgata",
                table: "ExactCrossRepositoryEdgeEvidence",
                columns: new[] { "ConsumerSourceAnchorId", "ConsumerDocumentVersionId", "ConsumerRepositoryId", "ConsumerScanRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExactCrossRepositoryEdgeEvidence_EvidenceFingerprint",
                schema: "vulgata",
                table: "ExactCrossRepositoryEdgeEvidence",
                column: "EvidenceFingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExactCrossRepositoryEdgeEvidence_ProviderSourceAnchorId_Pro~",
                schema: "vulgata",
                table: "ExactCrossRepositoryEdgeEvidence",
                columns: new[] { "ProviderSourceAnchorId", "ProviderDocumentVersionId", "ProviderRepositoryId", "ProviderScanRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_Uncertainties_DocumentVersionId_RepositoryId_ScanRunId",
                schema: "vulgata",
                table: "Uncertainties",
                columns: new[] { "DocumentVersionId", "RepositoryId", "ScanRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_Uncertainties_EvidenceFingerprint",
                schema: "vulgata",
                table: "Uncertainties",
                column: "EvidenceFingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Uncertainties_RepositoryId_Status_SubStatus",
                schema: "vulgata",
                table: "Uncertainties",
                columns: new[] { "RepositoryId", "Status", "SubStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Uncertainties_ResolvedByDocumentVersionId",
                schema: "vulgata",
                table: "Uncertainties",
                column: "ResolvedByDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Uncertainties_SourceAnchorId_DocumentVersionId_RepositoryId~",
                schema: "vulgata",
                table: "Uncertainties",
                columns: new[] { "SourceAnchorId", "DocumentVersionId", "RepositoryId", "ScanRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_Uncertainties_TargetRepositoryId",
                schema: "vulgata",
                table: "Uncertainties",
                column: "TargetRepositoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExactCrossRepositoryEdgeEvidence",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Uncertainties",
                schema: "vulgata");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SourceAnchors_Id_DocumentVersionId_RepositoryId_ScanRunId",
                schema: "vulgata",
                table: "SourceAnchors");
        }
    }
}
