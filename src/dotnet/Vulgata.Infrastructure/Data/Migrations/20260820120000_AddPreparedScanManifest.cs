using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPreparedScanManifest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PreScanResults_SchemaVersion",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.AddColumn<string>(
                name: "PreparedScanContextIdentityHash",
                schema: "vulgata",
                table: "ScanContextSnapshots",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextContentHash",
                schema: "vulgata",
                table: "PreScanResults",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContextSnapshotId",
                schema: "vulgata",
                table: "PreScanResults",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoffSchemaIdentity",
                schema: "vulgata",
                table: "PreScanResults",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PairPackageFingerprint",
                schema: "vulgata",
                table: "PreScanResults",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreparedScanContextIdentityHash",
                schema: "vulgata",
                table: "PreScanResults",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreparedScanManifestFingerprint",
                schema: "vulgata",
                table: "PreScanResults",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreparedScanManifestId",
                schema: "vulgata",
                table: "PreScanResults",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatalogRuleIdentity",
                schema: "vulgata",
                table: "CommunicationKindClassifications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ByteEndExclusive",
                schema: "vulgata",
                table: "CodeUnits",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ByteStart",
                schema: "vulgata",
                table: "CodeUnits",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileCodeUnitIdentityFingerprint",
                schema: "vulgata",
                table: "CodeUnits",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                schema: "vulgata",
                table: "CodeUnits",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SegmentCount",
                schema: "vulgata",
                table: "CodeUnits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SegmentOrdinal",
                schema: "vulgata",
                table: "CodeUnits",
                type: "integer",
                nullable: true);

            // SQLite rebuilds dependent tables when adding foreign keys. Make the
            // new principal tuple visible before it rebuilds PreScanResults; the
            // provider-neutral alternate key below remains the authoritative model.
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(
                    "CREATE UNIQUE INDEX \"IX_ScanContextSnapshots_PreparedBinding\" " +
                    "ON \"ScanContextSnapshots\" (\"Id\", \"ScanRunId\", \"RepositoryId\");");
            }

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ScanContextSnapshots_Id_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "ScanContextSnapshots",
                columns: new[] { "Id", "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateTable(
                name: "PreparedScanManifests",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoordinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfiledRevision = table.Column<string>(type: "character(40)", fixedLength: true, maxLength: 40, nullable: false),
                    RequestedRef = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SchemaIdentity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SnapshotIdentity = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    RuntimeIdentityHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CodeGraphProofIdentityHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    JavaReadinessIdentityHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CodeGraphRepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeGraphScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeGraphProfiledRevision = table.Column<string>(type: "character(40)", fixedLength: true, maxLength: 40, nullable: false),
                    CodeGraphSnapshotIdentity = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CodeGraphSnapshotRootBindingHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CodeGraphRuntimeIdentityHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CodeGraphIndexIdentity = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CodeGraphProofSchemaIdentity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CodeGraphInitialized = table.Column<bool>(type: "boolean", nullable: false),
                    CodeGraphIndexState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CodeGraphPendingAdded = table.Column<long>(type: "bigint", nullable: false),
                    CodeGraphPendingModified = table.Column<long>(type: "bigint", nullable: false),
                    CodeGraphPendingRemoved = table.Column<long>(type: "bigint", nullable: false),
                    CodeGraphWorktreeMismatch = table.Column<bool>(type: "boolean", nullable: false),
                    CodeGraphReindexRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    CodeGraphPendingReferences = table.Column<long>(type: "bigint", nullable: false),
                    CodeGraphIndexedFileCount = table.Column<long>(type: "bigint", nullable: false),
                    CodeGraphLogicalProjectBinding = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CodeGraphLogicalIndexBinding = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CodeGraphRepositoryBindingMatched = table.Column<bool>(type: "boolean", nullable: false),
                    CodeGraphScanRunBindingMatched = table.Column<bool>(type: "boolean", nullable: false),
                    CodeGraphRevisionBindingMatched = table.Column<bool>(type: "boolean", nullable: false),
                    CodeGraphRuntimeBindingMatched = table.Column<bool>(type: "boolean", nullable: false),
                    CodeGraphSnapshotBindingMatched = table.Column<bool>(type: "boolean", nullable: false),
                    CodeGraphProjectPathTrusted = table.Column<bool>(type: "boolean", nullable: false),
                    CodeGraphIndexPathTrusted = table.Column<bool>(type: "boolean", nullable: false),
                    JavaReadinessRepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    JavaReadinessScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    JavaReadinessProfiledRevision = table.Column<string>(type: "character(40)", fixedLength: true, maxLength: 40, nullable: false),
                    JavaReadinessSnapshotIdentity = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    JavaReadinessRuntimeIdentityHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    JavaReadinessWorkspaceBindingHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    JavaReadinessSchemaIdentity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    JavaReadinessOutcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JavaReadinessJdtLsRelease = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    JavaReadinessJdtLsDistributionArchiveHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    JavaReadinessJavaMajor = table.Column<int>(type: "integer", nullable: false),
                    JavaReadinessStreamJsonRpcVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    JavaReadinessTransport = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JavaReadinessWorkspaceExclusive = table.Column<bool>(type: "boolean", nullable: false),
                    JavaReadinessFile = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    JavaReadinessInitializeCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    JavaReadinessInitializedNotificationSent = table.Column<bool>(type: "boolean", nullable: false),
                    JavaReadinessImportState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JavaReadinessDocumentSymbolCount = table.Column<long>(type: "bigint", nullable: false),
                    JavaReadinessRepositoryBindingMatched = table.Column<bool>(type: "boolean", nullable: false),
                    JavaReadinessScanRunBindingMatched = table.Column<bool>(type: "boolean", nullable: false),
                    JavaReadinessRevisionBindingMatched = table.Column<bool>(type: "boolean", nullable: false),
                    JavaReadinessRuntimeBindingMatched = table.Column<bool>(type: "boolean", nullable: false),
                    JavaReadinessSnapshotBindingMatched = table.Column<bool>(type: "boolean", nullable: false),
                    CodeGraphObservedDurationMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    JavaReadinessObservedDurationMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    CatalogSchemaIdentity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CatalogHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CandidateCount = table.Column<int>(type: "integer", nullable: false),
                    EligibleCount = table.Column<int>(type: "integer", nullable: false),
                    FilteredCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    SelectedCount = table.Column<int>(type: "integer", nullable: false),
                    TotalConfirmedEvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    TotalHintCount = table.Column<int>(type: "integer", nullable: false),
                    TotalWarningCount = table.Column<int>(type: "integer", nullable: false),
                    RetainedClassificationCount = table.Column<int>(type: "integer", nullable: false),
                    EvidenceTruncated = table.Column<bool>(type: "boolean", nullable: false),
                    ContextSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreparedScanContextIdentityHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ContextContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PairSelectionFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ApprovalState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreparedScanManifests", x => x.Id);
                    table.UniqueConstraint("AK_PreparedScanManifests_Id_CoordinationId", x => new { x.Id, x.CoordinationId });
                    table.UniqueConstraint("AK_PreparedScanManifests_Id_ScanRunId_RepositoryId", x => new { x.Id, x.ScanRunId, x.RepositoryId });
                    table.CheckConstraint("CK_PreparedScanManifests_ApprovalState", "\"ApprovalState\" = 'Approved'");
                    table.CheckConstraint("CK_PreparedScanManifests_Counts", "\"CandidateCount\" >= 0 AND \"EligibleCount\" >= 0 AND \"FilteredCount\" >= 0 AND \"SkippedCount\" >= 0 AND \"SelectedCount\" >= 0 AND \"TotalConfirmedEvidenceCount\" >= 0 AND \"TotalHintCount\" >= 0 AND \"TotalWarningCount\" >= 0 AND \"RetainedClassificationCount\" BETWEEN 0 AND 256");
                    table.CheckConstraint("CK_PreparedScanManifests_Durations", "\"CodeGraphObservedDurationMilliseconds\" >= 0 AND \"JavaReadinessObservedDurationMilliseconds\" >= 0");
                    table.CheckConstraint("CK_PreparedScanManifests_Hashes", "length(\"SnapshotIdentity\") = 64 AND length(\"RuntimeIdentityHash\") = 64 AND length(\"CodeGraphProofIdentityHash\") = 64 AND length(\"JavaReadinessIdentityHash\") = 64 AND length(\"CatalogHash\") = 64 AND length(\"PreparedScanContextIdentityHash\") = 64 AND length(\"ContextContentHash\") = 64 AND length(\"PairSelectionFingerprint\") = 64 AND length(\"Fingerprint\") = 64");
                    table.CheckConstraint("CK_PreparedScanManifests_ProfiledRevision", "length(\"ProfiledRevision\") = 40");
                    table.CheckConstraint("CK_PreparedScanManifests_ProofCounts", "\"CodeGraphPendingAdded\" >= 0 AND \"CodeGraphPendingModified\" >= 0 AND \"CodeGraphPendingRemoved\" >= 0 AND \"CodeGraphPendingReferences\" >= 0 AND \"CodeGraphIndexedFileCount\" >= 0 AND \"JavaReadinessDocumentSymbolCount\" >= 0");
                    table.CheckConstraint("CK_PreparedScanManifests_SchemaIdentity", "\"SchemaIdentity\" = 'prepared-scan-manifest/1.0'");
                    table.ForeignKey(
                        name: "FK_PreparedScanManifests_ScanContextSnapshots_ContextSnapshotI~",
                        columns: x => new { x.ContextSnapshotId, x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanContextSnapshots",
                        principalColumns: new[] { "Id", "ScanRunId", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreparedScanManifests_ScanRuns_ScanRunId_RepositoryId",
                        columns: x => new { x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreparedScanManifests_TwoRepositoryScanCoordinations_Coordi~",
                        column: x => x.CoordinationId,
                        principalSchema: "vulgata",
                        principalTable: "TwoRepositoryScanCoordinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreparedScanManifestEntries",
                schema: "vulgata",
                columns: table => new
                {
                    FileCodeUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreparedScanManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoordinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileCodeUnitIdentityFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    DirectBusinessScore = table.Column<int>(type: "integer", nullable: false),
                    FinalBusinessScore = table.Column<int>(type: "integer", nullable: false),
                    SelectionReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ManifestOrdinal = table.Column<int>(type: "integer", nullable: false),
                    PairSelectionOrdinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreparedScanManifestEntries", x => x.FileCodeUnitId);
                    table.UniqueConstraint("AK_PreparedScanManifestEntries_PreparedScanManifestId_FileCode~", x => new { x.PreparedScanManifestId, x.FileCodeUnitId, x.FileCodeUnitIdentityFingerprint, x.PairSelectionOrdinal });
                    table.CheckConstraint("CK_PreparedScanManifestEntries_Fingerprint", "length(\"FileCodeUnitIdentityFingerprint\") = 64");
                    table.CheckConstraint("CK_PreparedScanManifestEntries_Ordinals", "\"ManifestOrdinal\" >= 1 AND \"PairSelectionOrdinal\" >= 1");
                    table.CheckConstraint("CK_PreparedScanManifestEntries_Scores", "\"DirectBusinessScore\" >= 0 AND \"FinalBusinessScore\" >= \"DirectBusinessScore\"");
                    table.CheckConstraint("CK_PreparedScanManifestEntries_SelectionReason", "\"SelectionReason\" IN ('DirectBusinessEvidence', 'RelatedCall', 'RelatedImport', 'RelatedInheritance', 'ProductionFallback')");
                    table.ForeignKey(
                        name: "FK_PreparedScanManifestEntries_CodeUnits_FileCodeUnitId",
                        column: x => x.FileCodeUnitId,
                        principalSchema: "vulgata",
                        principalTable: "CodeUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreparedScanManifestEntries_PreparedScanManifests_PreparedS~",
                        columns: x => new { x.PreparedScanManifestId, x.CoordinationId },
                        principalSchema: "vulgata",
                        principalTable: "PreparedScanManifests",
                        principalColumns: new[] { "Id", "CoordinationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreparedScanAudits",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PreparedScanManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    RelativePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    RawByteCount = table.Column<long>(type: "bigint", nullable: true),
                    LogicalLineCount = table.Column<int>(type: "integer", nullable: true),
                    FileCodeUnitIdentityFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    PairSelectionOrdinal = table.Column<int>(type: "integer", nullable: true),
                    PreparedScanManifestEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    DetailFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreparedScanAudits", x => x.Id);
                    table.CheckConstraint("CK_PreparedScanAudits_Hashes", "(\"FileHash\" IS NULL OR length(\"FileHash\") = 64) AND (\"FileCodeUnitIdentityFingerprint\" IS NULL OR length(\"FileCodeUnitIdentityFingerprint\") = 64) AND length(\"DetailFingerprint\") = 64");
                    table.CheckConstraint("CK_PreparedScanAudits_Measurements", "(\"RawByteCount\" IS NULL OR \"RawByteCount\" >= 0) AND (\"LogicalLineCount\" IS NULL OR \"LogicalLineCount\" >= 0)");
                    table.CheckConstraint("CK_PreparedScanAudits_Ordinal", "\"Ordinal\" >= 1");
                    table.CheckConstraint("CK_PreparedScanAudits_SelectedShape", "(\"Outcome\" = 'Selected' AND \"FileCodeUnitIdentityFingerprint\" IS NOT NULL AND \"PairSelectionOrdinal\" IS NOT NULL AND \"PairSelectionOrdinal\" >= 1 AND \"PreparedScanManifestEntryId\" IS NOT NULL) OR (\"Outcome\" = 'Eligible' AND \"FileCodeUnitIdentityFingerprint\" IS NOT NULL AND \"PairSelectionOrdinal\" IS NULL AND \"PreparedScanManifestEntryId\" IS NULL) OR (\"Outcome\" IN ('Filtered', 'Skipped', 'Warning') AND \"FileCodeUnitIdentityFingerprint\" IS NULL AND \"PairSelectionOrdinal\" IS NULL AND \"PreparedScanManifestEntryId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_PreparedScanAudits_PreparedScanManifestEntries_PreparedScan~",
                        columns: x => new { x.PreparedScanManifestId, x.PreparedScanManifestEntryId, x.FileCodeUnitIdentityFingerprint, x.PairSelectionOrdinal },
                        principalSchema: "vulgata",
                        principalTable: "PreparedScanManifestEntries",
                        principalColumns: new[] { "PreparedScanManifestId", "FileCodeUnitId", "FileCodeUnitIdentityFingerprint", "PairSelectionOrdinal" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreparedScanAudits_PreparedScanManifests_PreparedScanManife~",
                        columns: x => new { x.PreparedScanManifestId, x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "PreparedScanManifests",
                        principalColumns: new[] { "Id", "ScanRunId", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScanContextSnapshots_PreparedIdentityHash_Length",
                schema: "vulgata",
                table: "ScanContextSnapshots",
                sql: "\"PreparedScanContextIdentityHash\" IS NULL OR length(\"PreparedScanContextIdentityHash\") = 64");

            migrationBuilder.CreateIndex(
                name: "IX_PreScanResults_ContextSnapshotId_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "PreScanResults",
                columns: new[] { "ContextSnapshotId", "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_PreScanResults_PreparedScanManifestId_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "PreScanResults",
                columns: new[] { "PreparedScanManifestId", "ScanRunId", "RepositoryId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PreScanResults_PreparedV2Shape",
                schema: "vulgata",
                table: "PreScanResults",
                sql: "(\"SchemaVersion\" = '1.0' AND \"PreparedScanManifestId\" IS NULL AND \"PreparedScanManifestFingerprint\" IS NULL AND \"ContextSnapshotId\" IS NULL AND \"PreparedScanContextIdentityHash\" IS NULL AND \"ContextContentHash\" IS NULL AND \"HandoffSchemaIdentity\" IS NULL AND \"PairPackageFingerprint\" IS NULL) OR (\"SchemaVersion\" = '2.0' AND \"PreparedScanManifestId\" IS NOT NULL AND length(\"PreparedScanManifestFingerprint\") = 64 AND \"ContextSnapshotId\" IS NOT NULL AND length(\"PreparedScanContextIdentityHash\") = 64 AND length(\"ContextContentHash\") = 64 AND \"HandoffSchemaIdentity\" = 'prepared-scan-handoff/2.0' AND length(\"PairPackageFingerprint\") = 64)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PreScanResults_SchemaVersion",
                schema: "vulgata",
                table: "PreScanResults",
                sql: "\"SchemaVersion\" IN ('1.0', '2.0')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CommunicationKindClassifications_CatalogRuleIdentity_Length",
                schema: "vulgata",
                table: "CommunicationKindClassifications",
                sql: "\"CatalogRuleIdentity\" IS NULL OR length(\"CatalogRuleIdentity\") BETWEEN 1 AND 256");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CodeUnits_PreparedFileShape",
                schema: "vulgata",
                table: "CodeUnits",
                sql: "(\"FileCodeUnitIdentityFingerprint\" IS NULL AND \"FileHash\" IS NULL AND \"ByteStart\" IS NULL AND \"ByteEndExclusive\" IS NULL AND \"SegmentOrdinal\" IS NULL AND \"SegmentCount\" IS NULL) OR (length(\"FileCodeUnitIdentityFingerprint\") = 64 AND length(\"FileHash\") = 64 AND \"ByteStart\" >= 0 AND \"ByteEndExclusive\" > \"ByteStart\" AND \"ByteEndExclusive\" - \"ByteStart\" <= 65536 AND \"SegmentOrdinal\" >= 1 AND \"SegmentCount\" >= \"SegmentOrdinal\" AND \"UnitType\" = 'File' AND \"RangeConfidence\" = 'Exact' AND \"Status\" = 'Pending')");

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanAudits_PreparedScanManifestId_FileCodeUnitIdent~",
                schema: "vulgata",
                table: "PreparedScanAudits",
                columns: new[] { "PreparedScanManifestId", "FileCodeUnitIdentityFingerprint" },
                unique: true,
                filter: "\"FileCodeUnitIdentityFingerprint\" IS NOT NULL AND \"Outcome\" = 'Selected'");

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanAudits_PreparedScanManifestId_Ordinal",
                schema: "vulgata",
                table: "PreparedScanAudits",
                columns: new[] { "PreparedScanManifestId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanAudits_PreparedScanManifestId_PairSelectionOrdi~",
                schema: "vulgata",
                table: "PreparedScanAudits",
                columns: new[] { "PreparedScanManifestId", "PairSelectionOrdinal" },
                unique: true,
                filter: "\"PairSelectionOrdinal\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanAudits_PreparedScanManifestId_PreparedScanMani~1",
                schema: "vulgata",
                table: "PreparedScanAudits",
                columns: new[] { "PreparedScanManifestId", "PreparedScanManifestEntryId", "FileCodeUnitIdentityFingerprint", "PairSelectionOrdinal" });

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanAudits_PreparedScanManifestId_PreparedScanManif~",
                schema: "vulgata",
                table: "PreparedScanAudits",
                columns: new[] { "PreparedScanManifestId", "PreparedScanManifestEntryId" },
                unique: true,
                filter: "\"PreparedScanManifestEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanAudits_PreparedScanManifestId_RelativePath_Outc~",
                schema: "vulgata",
                table: "PreparedScanAudits",
                columns: new[] { "PreparedScanManifestId", "RelativePath", "Outcome", "Reason", "DetailFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanAudits_PreparedScanManifestId_ScanRunId_Reposit~",
                schema: "vulgata",
                table: "PreparedScanAudits",
                columns: new[] { "PreparedScanManifestId", "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanManifestEntries_CoordinationId_PairSelectionOrd~",
                schema: "vulgata",
                table: "PreparedScanManifestEntries",
                columns: new[] { "CoordinationId", "PairSelectionOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanManifestEntries_PreparedScanManifestId_Coordina~",
                schema: "vulgata",
                table: "PreparedScanManifestEntries",
                columns: new[] { "PreparedScanManifestId", "CoordinationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanManifestEntries_PreparedScanManifestId_FileCode~",
                schema: "vulgata",
                table: "PreparedScanManifestEntries",
                columns: new[] { "PreparedScanManifestId", "FileCodeUnitIdentityFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanManifestEntries_PreparedScanManifestId_Manifest~",
                schema: "vulgata",
                table: "PreparedScanManifestEntries",
                columns: new[] { "PreparedScanManifestId", "ManifestOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanManifests_ContextSnapshotId_ScanRunId_Repositor~",
                schema: "vulgata",
                table: "PreparedScanManifests",
                columns: new[] { "ContextSnapshotId", "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanManifests_CoordinationId_RepositoryId",
                schema: "vulgata",
                table: "PreparedScanManifests",
                columns: new[] { "CoordinationId", "RepositoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanManifests_Fingerprint",
                schema: "vulgata",
                table: "PreparedScanManifests",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanManifests_RepositoryId_ScanRunId",
                schema: "vulgata",
                table: "PreparedScanManifests",
                columns: new[] { "RepositoryId", "ScanRunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreparedScanManifests_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "PreparedScanManifests",
                columns: new[] { "ScanRunId", "RepositoryId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PreScanResults_PreparedScanManifests_PreparedScanManifestId~",
                schema: "vulgata",
                table: "PreScanResults",
                columns: new[] { "PreparedScanManifestId", "ScanRunId", "RepositoryId" },
                principalSchema: "vulgata",
                principalTable: "PreparedScanManifests",
                principalColumns: new[] { "Id", "ScanRunId", "RepositoryId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PreScanResults_ScanContextSnapshots_ContextSnapshotId_ScanR~",
                schema: "vulgata",
                table: "PreScanResults",
                columns: new[] { "ContextSnapshotId", "ScanRunId", "RepositoryId" },
                principalSchema: "vulgata",
                principalTable: "ScanContextSnapshots",
                principalColumns: new[] { "Id", "ScanRunId", "RepositoryId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PreScanResults_PreparedScanManifests_PreparedScanManifestId~",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.DropForeignKey(
                name: "FK_PreScanResults_ScanContextSnapshots_ContextSnapshotId_ScanR~",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.DropTable(
                name: "PreparedScanAudits",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "PreparedScanManifestEntries",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "PreparedScanManifests",
                schema: "vulgata");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ScanContextSnapshots_Id_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "ScanContextSnapshots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScanContextSnapshots_PreparedIdentityHash_Length",
                schema: "vulgata",
                table: "ScanContextSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_PreScanResults_ContextSnapshotId_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.DropIndex(
                name: "IX_PreScanResults_PreparedScanManifestId_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PreScanResults_PreparedV2Shape",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PreScanResults_SchemaVersion",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CommunicationKindClassifications_CatalogRuleIdentity_Length",
                schema: "vulgata",
                table: "CommunicationKindClassifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CodeUnits_PreparedFileShape",
                schema: "vulgata",
                table: "CodeUnits");

            migrationBuilder.DropColumn(
                name: "PreparedScanContextIdentityHash",
                schema: "vulgata",
                table: "ScanContextSnapshots");

            migrationBuilder.DropColumn(
                name: "ContextContentHash",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.DropColumn(
                name: "ContextSnapshotId",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.DropColumn(
                name: "HandoffSchemaIdentity",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.DropColumn(
                name: "PairPackageFingerprint",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.DropColumn(
                name: "PreparedScanContextIdentityHash",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.DropColumn(
                name: "PreparedScanManifestFingerprint",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.DropColumn(
                name: "PreparedScanManifestId",
                schema: "vulgata",
                table: "PreScanResults");

            migrationBuilder.DropColumn(
                name: "CatalogRuleIdentity",
                schema: "vulgata",
                table: "CommunicationKindClassifications");

            migrationBuilder.DropColumn(
                name: "ByteEndExclusive",
                schema: "vulgata",
                table: "CodeUnits");

            migrationBuilder.DropColumn(
                name: "ByteStart",
                schema: "vulgata",
                table: "CodeUnits");

            migrationBuilder.DropColumn(
                name: "FileCodeUnitIdentityFingerprint",
                schema: "vulgata",
                table: "CodeUnits");

            migrationBuilder.DropColumn(
                name: "FileHash",
                schema: "vulgata",
                table: "CodeUnits");

            migrationBuilder.DropColumn(
                name: "SegmentCount",
                schema: "vulgata",
                table: "CodeUnits");

            migrationBuilder.DropColumn(
                name: "SegmentOrdinal",
                schema: "vulgata",
                table: "CodeUnits");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PreScanResults_SchemaVersion",
                schema: "vulgata",
                table: "PreScanResults",
                sql: "\"SchemaVersion\" = '1.0'");
        }
    }
}
