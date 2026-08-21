using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddResumablePass1CodeLogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TwoRepositoryScanCoordinations_Phase_Closed",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TwoRepositoryScanCoordinations_ReasonCategory_Closed",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TwoRepositoryScanCoordinations_TerminalReason_Shape",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScanExecutions_NewPathBinding_Shape",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScanExecutions_PauseFailure_Shape",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScanExecutions_Phase_Closed",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentVersions_SchemaIdentity_Closed",
                schema: "vulgata",
                table: "DocumentVersions");

            migrationBuilder.AddColumn<long>(
                name: "ByteEnd",
                schema: "vulgata",
                table: "SourceAnchors",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ByteStart",
                schema: "vulgata",
                table: "SourceAnchors",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Pass1PairExecutions",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoordinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PairPackageFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PairSelectionFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ManifestOneId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManifestTwoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pass1PairFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PromptIdentityOne = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PromptIdentityTwo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DocumentSchemaIdentity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AnalysisRuntimeIdentityHashOne = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    AnalysisRuntimeIdentityHashTwo = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    InitialConcurrencyLimit = table.Column<int>(type: "integer", nullable: false),
                    ConcurrencyLimit = table.Column<int>(type: "integer", nullable: false),
                    ConcurrencyRevision = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedFileCount = table.Column<int>(type: "integer", nullable: false),
                    HighValueSeedCount = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CircuitState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProbeOrdinal = table.Column<int>(type: "integer", nullable: false),
                    NextProbeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryAfterAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReasonCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StateVersion = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TerminalAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass1PairExecutions", x => x.Id);
                    table.CheckConstraint("CK_Pass1PairExecutions_Concurrency", "\"InitialConcurrencyLimit\" BETWEEN 1 AND 4 AND \"ConcurrencyLimit\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_Pass1PairExecutions_Counts", "\"DispatchedFileCount\" BETWEEN 50 AND 150 AND \"HighValueSeedCount\" BETWEEN 0 AND \"DispatchedFileCount\"");
                    table.CheckConstraint("CK_Pass1PairExecutions_ProbeOrdinal", "\"ProbeOrdinal\" BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_Pass1PairExecutions_Versions", "\"StateVersion\" >= 0 AND \"ConcurrencyRevision\" >= 0");
                    table.ForeignKey(
                        name: "FK_Pass1PairExecutions_PreparedScanManifests_ManifestOneId",
                        column: x => x.ManifestOneId,
                        principalSchema: "vulgata",
                        principalTable: "PreparedScanManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pass1PairExecutions_PreparedScanManifests_ManifestTwoId",
                        column: x => x.ManifestTwoId,
                        principalSchema: "vulgata",
                        principalTable: "PreparedScanManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pass1PairExecutions_TwoRepositoryScanCoordinations_Coordina~",
                        column: x => x.CoordinationId,
                        principalSchema: "vulgata",
                        principalTable: "TwoRepositoryScanCoordinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pass1ConcurrencyChanges",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PairExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedStateVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingStateVersion = table.Column<long>(type: "bigint", nullable: false),
                    PreviousLimit = table.Column<int>(type: "integer", nullable: false),
                    EffectiveLimit = table.Column<int>(type: "integer", nullable: false),
                    ConcurrencyRevision = table.Column<long>(type: "bigint", nullable: false),
                    ChangedByPrincipalId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ChangeFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass1ConcurrencyChanges", x => x.Id);
                    table.CheckConstraint("CK_Pass1ConcurrencyChanges_Shape", "\"ExpectedStateVersion\" >= 0 AND \"ResultingStateVersion\" = \"ExpectedStateVersion\" + 1 AND \"PreviousLimit\" BETWEEN 1 AND 4 AND \"EffectiveLimit\" BETWEEN 1 AND 4 AND \"PreviousLimit\" <> \"EffectiveLimit\" AND \"ConcurrencyRevision\" > 0");
                    table.ForeignKey(
                        name: "FK_Pass1ConcurrencyChanges_Pass1PairExecutions_PairExecutionId",
                        column: x => x.PairExecutionId,
                        principalSchema: "vulgata",
                        principalTable: "Pass1PairExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pass1NavigationBudgets",
                schema: "vulgata",
                columns: table => new
                {
                    ScanJobExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PairExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    AcceptedCallCount = table.Column<int>(type: "integer", nullable: false),
                    EvidenceBytes = table.Column<int>(type: "integer", nullable: false),
                    MaximumDepthObserved = table.Column<int>(type: "integer", nullable: false),
                    StateVersion = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass1NavigationBudgets", x => x.ScanJobExecutionId);
                    table.CheckConstraint("CK_Pass1NavigationBudgets_Bounds", "\"ToolAttemptCount\" BETWEEN 0 AND 8 AND \"AcceptedCallCount\" BETWEEN 0 AND \"ToolAttemptCount\" AND \"EvidenceBytes\" BETWEEN 0 AND 65536 AND \"MaximumDepthObserved\" BETWEEN 0 AND 3 AND \"StateVersion\" >= 0");
                    table.ForeignKey(
                        name: "FK_Pass1NavigationBudgets_Pass1PairExecutions_PairExecutionId",
                        column: x => x.PairExecutionId,
                        principalSchema: "vulgata",
                        principalTable: "Pass1PairExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pass1NavigationBudgets_ScanJobExecutions_ScanJobExecutionId",
                        column: x => x.ScanJobExecutionId,
                        principalSchema: "vulgata",
                        principalTable: "ScanJobExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pass1NavigationReceipts",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanJobExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PairExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptOrdinal = table.Column<int>(type: "integer", nullable: false),
                    ToolAttemptOrdinal = table.Column<int>(type: "integer", nullable: true),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Depth = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BoundKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RequestFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    ReceiptFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultCount = table.Column<int>(type: "integer", nullable: false),
                    EvidenceBytes = table.Column<int>(type: "integer", nullable: false),
                    DescriptorJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass1NavigationReceipts", x => x.Id);
                    table.CheckConstraint("CK_Pass1NavigationReceipts_Bounds", "\"ReceiptOrdinal\" BETWEEN 1 AND 9 AND (\"ToolAttemptOrdinal\" IS NULL OR \"ToolAttemptOrdinal\" BETWEEN 1 AND 8) AND (\"Depth\" IS NULL OR \"Depth\" BETWEEN 1 AND 4) AND \"ResultCount\" BETWEEN 0 AND 20 AND \"EvidenceBytes\" BETWEEN 0 AND 65536");
                    table.ForeignKey(
                        name: "FK_Pass1NavigationReceipts_Pass1PairExecutions_PairExecutionId",
                        column: x => x.PairExecutionId,
                        principalSchema: "vulgata",
                        principalTable: "Pass1PairExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pass1NavigationReceipts_ScanJobExecutions_ScanJobExecutionId",
                        column: x => x.ScanJobExecutionId,
                        principalSchema: "vulgata",
                        principalTable: "ScanJobExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pass1Outcomes",
                schema: "vulgata",
                columns: table => new
                {
                    PairExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DispatchedCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedCount = table.Column<int>(type: "integer", nullable: false),
                    OrdinaryFailureCount = table.Column<int>(type: "integer", nullable: false),
                    OrdinaryFailureBudget = table.Column<int>(type: "integer", nullable: false),
                    HighValueSeedCount = table.Column<int>(type: "integer", nullable: false),
                    SucceededHighValueSeedCount = table.Column<int>(type: "integer", nullable: false),
                    ReconciliationFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    ReconciledEdgeCount = table.Column<int>(type: "integer", nullable: false),
                    OutcomeFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass1Outcomes", x => x.PairExecutionId);
                    table.CheckConstraint("CK_Pass1Outcomes_Counts", "\"DispatchedCount\" >= 0 AND \"CompletedCount\" BETWEEN 0 AND \"DispatchedCount\" AND \"OrdinaryFailureCount\" BETWEEN 0 AND \"DispatchedCount\" AND \"OrdinaryFailureBudget\" BETWEEN 0 AND 5 AND \"HighValueSeedCount\" BETWEEN 0 AND \"DispatchedCount\" AND \"SucceededHighValueSeedCount\" BETWEEN 0 AND \"HighValueSeedCount\" AND \"ReconciledEdgeCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_Pass1Outcomes_Pass1PairExecutions_PairExecutionId",
                        column: x => x.PairExecutionId,
                        principalSchema: "vulgata",
                        principalTable: "Pass1PairExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pass1FailureInventoryEntries",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PairExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileCodeUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PairSelectionOrdinal = table.Column<int>(type: "integer", nullable: false),
                    ManifestOrdinal = table.Column<int>(type: "integer", nullable: false),
                    SegmentOrdinal = table.Column<int>(type: "integer", nullable: false),
                    RepositoryRelativePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    FailureCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass1FailureInventoryEntries", x => x.Id);
                    table.CheckConstraint("CK_Pass1FailureInventoryEntries_Ordinals", "\"PairSelectionOrdinal\" > 0 AND \"ManifestOrdinal\" > 0 AND \"SegmentOrdinal\" > 0");
                    table.ForeignKey(
                        name: "FK_Pass1FailureInventoryEntries_CodeUnits_FileCodeUnitId",
                        column: x => x.FileCodeUnitId,
                        principalSchema: "vulgata",
                        principalTable: "CodeUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pass1FailureInventoryEntries_Pass1Outcomes_PairExecutionId",
                        column: x => x.PairExecutionId,
                        principalSchema: "vulgata",
                        principalTable: "Pass1Outcomes",
                        principalColumn: "PairExecutionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pass1FailureInventoryEntries_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "vulgata",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TwoRepositoryScanCoordinations_Phase_Closed",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations",
                sql: "\"Phase\" IN ('Preparing', 'Pass1', 'Resolving', 'Pass2', 'Paused', 'Completed', 'CompletedWithWarnings', 'FailedIntegrity', 'FailedSecurity', 'Cancelled', 'FailedPass1')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TwoRepositoryScanCoordinations_ReasonCategory_Closed",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations",
                sql: "\"ReasonCategory\" IS NULL OR \"ReasonCategory\" IN ('ProviderUnavailable', 'OperatorCorrection', 'Integrity', 'Security', 'Cancellation', 'Unclassified', 'Pass1Policy')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TwoRepositoryScanCoordinations_TerminalReason_Shape",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations",
                sql: "(\"Phase\" IN ('Completed', 'CompletedWithWarnings') AND \"TerminalAt\" IS NOT NULL AND \"ReasonCategory\" IS NULL AND \"ReasonCode\" IS NULL) OR (\"Phase\" IN ('FailedIntegrity', 'FailedSecurity', 'Cancelled', 'FailedPass1') AND \"TerminalAt\" IS NOT NULL AND \"ReasonCategory\" IS NOT NULL AND \"ReasonCode\" IS NOT NULL) OR (\"Phase\" = 'Paused' AND \"TerminalAt\" IS NULL AND \"ReasonCategory\" IS NOT NULL AND \"ReasonCode\" IS NOT NULL) OR (\"Phase\" IN ('Preparing', 'Pass1', 'Resolving', 'Pass2') AND \"TerminalAt\" IS NULL AND \"ReasonCategory\" IS NULL AND \"ReasonCode\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SourceAnchors_ByteRange_Shape",
                schema: "vulgata",
                table: "SourceAnchors",
                sql: "(\"ByteStart\" IS NULL AND \"ByteEnd\" IS NULL) OR (\"ByteStart\" IS NOT NULL AND \"ByteEnd\" IS NOT NULL AND \"ByteStart\" >= 0 AND \"ByteEnd\" > \"ByteStart\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScanExecutions_NewPathBinding_Shape",
                schema: "vulgata",
                table: "ScanExecutions",
                sql: "(\"Phase\" IN ('Pass1Planning', 'Pass1Running', 'Pass1Completed', 'Failed') AND \"PromptIdentity\" IS NULL AND \"DocumentSchemaIdentity\" IS NULL AND \"AnalysisRuntimeIdentityJson\" IS NULL AND \"AnalysisRuntimeIdentityHash\" IS NULL AND \"UpdatedAt\" IS NULL) OR (\"Phase\" IN ('Prepared', 'Running', 'PausedProviderUnavailable', 'PausedOperatorCorrection', 'CompletedWithWarnings', 'FailedIntegrity', 'FailedSecurity', 'FailedPass1') AND \"PromptIdentity\" IS NOT NULL AND \"DocumentSchemaIdentity\" IS NOT NULL AND \"AnalysisRuntimeIdentityJson\" IS NOT NULL AND \"AnalysisRuntimeIdentityHash\" IS NOT NULL AND \"UpdatedAt\" IS NOT NULL) OR (\"Phase\" IN ('Completed', 'Cancelled') AND ((\"PromptIdentity\" IS NULL AND \"DocumentSchemaIdentity\" IS NULL AND \"AnalysisRuntimeIdentityJson\" IS NULL AND \"AnalysisRuntimeIdentityHash\" IS NULL AND \"UpdatedAt\" IS NULL) OR (\"PromptIdentity\" IS NOT NULL AND \"DocumentSchemaIdentity\" IS NOT NULL AND \"AnalysisRuntimeIdentityJson\" IS NOT NULL AND \"AnalysisRuntimeIdentityHash\" IS NOT NULL AND \"UpdatedAt\" IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScanExecutions_PauseFailure_Shape",
                schema: "vulgata",
                table: "ScanExecutions",
                sql: "(\"Phase\" = 'PausedProviderUnavailable' AND \"PauseCategory\" = 'ProviderUnavailable' AND \"PauseCode\" IS NOT NULL AND \"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR (\"Phase\" = 'PausedOperatorCorrection' AND \"PauseCategory\" = 'OperatorCorrection' AND \"PauseCode\" IS NOT NULL AND \"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR (\"Phase\" = 'FailedIntegrity' AND \"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND \"FailureCategory\" = 'Integrity' AND \"FailureCode\" IS NOT NULL) OR (\"Phase\" = 'FailedSecurity' AND \"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND \"FailureCategory\" = 'Security' AND \"FailureCode\" IS NOT NULL) OR (\"Phase\" = 'FailedPass1' AND \"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND \"FailureCategory\" = 'Pass1Policy' AND \"FailureCode\" IS NOT NULL) OR (\"Phase\" NOT IN ('PausedProviderUnavailable', 'PausedOperatorCorrection', 'FailedIntegrity', 'FailedSecurity', 'FailedPass1') AND \"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND ((\"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR (\"Phase\" = 'Cancelled' AND \"FailureCategory\" = 'Cancellation' AND \"FailureCode\" IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScanExecutions_Phase_Closed",
                schema: "vulgata",
                table: "ScanExecutions",
                sql: "\"Phase\" IN ('Pass1Planning', 'Pass1Running', 'Pass1Completed', 'Completed', 'Failed', 'Cancelled', 'Prepared', 'Running', 'PausedProviderUnavailable', 'PausedOperatorCorrection', 'CompletedWithWarnings', 'FailedIntegrity', 'FailedSecurity', 'FailedPass1')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentVersions_SchemaIdentity_Closed",
                schema: "vulgata",
                table: "DocumentVersions",
                sql: "\"SchemaIdentity\" IN ('vulgata.knowledge-document/1.0', 'code-logic/file-v2')");

            migrationBuilder.CreateIndex(
                name: "IX_Pass1ConcurrencyChanges_PairExecutionId_ConcurrencyRevision",
                schema: "vulgata",
                table: "Pass1ConcurrencyChanges",
                columns: new[] { "PairExecutionId", "ConcurrencyRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass1ConcurrencyChanges_PairExecutionId_ExpectedStateVersion",
                schema: "vulgata",
                table: "Pass1ConcurrencyChanges",
                columns: new[] { "PairExecutionId", "ExpectedStateVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass1FailureInventoryEntries_FileCodeUnitId",
                schema: "vulgata",
                table: "Pass1FailureInventoryEntries",
                column: "FileCodeUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Pass1FailureInventoryEntries_PairExecutionId_FileCodeUnitId",
                schema: "vulgata",
                table: "Pass1FailureInventoryEntries",
                columns: new[] { "PairExecutionId", "FileCodeUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass1FailureInventoryEntries_PairExecutionId_PairSelectionO~",
                schema: "vulgata",
                table: "Pass1FailureInventoryEntries",
                columns: new[] { "PairExecutionId", "PairSelectionOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass1FailureInventoryEntries_RepositoryId",
                schema: "vulgata",
                table: "Pass1FailureInventoryEntries",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Pass1NavigationBudgets_PairExecutionId",
                schema: "vulgata",
                table: "Pass1NavigationBudgets",
                column: "PairExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Pass1NavigationReceipts_PairExecutionId",
                schema: "vulgata",
                table: "Pass1NavigationReceipts",
                column: "PairExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Pass1NavigationReceipts_ReceiptFingerprint",
                schema: "vulgata",
                table: "Pass1NavigationReceipts",
                column: "ReceiptFingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass1NavigationReceipts_ScanJobExecutionId_ReceiptOrdinal",
                schema: "vulgata",
                table: "Pass1NavigationReceipts",
                columns: new[] { "ScanJobExecutionId", "ReceiptOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass1NavigationReceipts_ScanJobExecutionId_ToolAttemptOrdin~",
                schema: "vulgata",
                table: "Pass1NavigationReceipts",
                columns: new[] { "ScanJobExecutionId", "ToolAttemptOrdinal" },
                unique: true,
                filter: "\"ToolAttemptOrdinal\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Pass1PairExecutions_CoordinationId",
                schema: "vulgata",
                table: "Pass1PairExecutions",
                column: "CoordinationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass1PairExecutions_ManifestOneId",
                schema: "vulgata",
                table: "Pass1PairExecutions",
                column: "ManifestOneId");

            migrationBuilder.CreateIndex(
                name: "IX_Pass1PairExecutions_ManifestTwoId",
                schema: "vulgata",
                table: "Pass1PairExecutions",
                column: "ManifestTwoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pass1PairExecutions_PairPackageFingerprint",
                schema: "vulgata",
                table: "Pass1PairExecutions",
                column: "PairPackageFingerprint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pass1ConcurrencyChanges",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Pass1FailureInventoryEntries",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Pass1NavigationBudgets",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Pass1NavigationReceipts",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Pass1Outcomes",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Pass1PairExecutions",
                schema: "vulgata");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TwoRepositoryScanCoordinations_Phase_Closed",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TwoRepositoryScanCoordinations_ReasonCategory_Closed",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TwoRepositoryScanCoordinations_TerminalReason_Shape",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SourceAnchors_ByteRange_Shape",
                schema: "vulgata",
                table: "SourceAnchors");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScanExecutions_NewPathBinding_Shape",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScanExecutions_PauseFailure_Shape",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScanExecutions_Phase_Closed",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentVersions_SchemaIdentity_Closed",
                schema: "vulgata",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "ByteEnd",
                schema: "vulgata",
                table: "SourceAnchors");

            migrationBuilder.DropColumn(
                name: "ByteStart",
                schema: "vulgata",
                table: "SourceAnchors");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TwoRepositoryScanCoordinations_Phase_Closed",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations",
                sql: "\"Phase\" IN ('Preparing', 'Pass1', 'Resolving', 'Pass2', 'Paused', 'Completed', 'CompletedWithWarnings', 'FailedIntegrity', 'FailedSecurity', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TwoRepositoryScanCoordinations_ReasonCategory_Closed",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations",
                sql: "\"ReasonCategory\" IS NULL OR \"ReasonCategory\" IN ('ProviderUnavailable', 'OperatorCorrection', 'Integrity', 'Security', 'Cancellation', 'Unclassified')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TwoRepositoryScanCoordinations_TerminalReason_Shape",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations",
                sql: "(\"Phase\" IN ('Completed', 'CompletedWithWarnings') AND \"TerminalAt\" IS NOT NULL AND \"ReasonCategory\" IS NULL AND \"ReasonCode\" IS NULL) OR (\"Phase\" IN ('FailedIntegrity', 'FailedSecurity', 'Cancelled') AND \"TerminalAt\" IS NOT NULL AND \"ReasonCategory\" IS NOT NULL AND \"ReasonCode\" IS NOT NULL) OR (\"Phase\" = 'Paused' AND \"TerminalAt\" IS NULL AND \"ReasonCategory\" IS NOT NULL AND \"ReasonCode\" IS NOT NULL) OR (\"Phase\" IN ('Preparing', 'Pass1', 'Resolving', 'Pass2') AND \"TerminalAt\" IS NULL AND \"ReasonCategory\" IS NULL AND \"ReasonCode\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScanExecutions_NewPathBinding_Shape",
                schema: "vulgata",
                table: "ScanExecutions",
                sql: "(\"Phase\" IN ('Pass1Planning', 'Pass1Running', 'Pass1Completed', 'Failed') AND \"PromptIdentity\" IS NULL AND \"DocumentSchemaIdentity\" IS NULL AND \"AnalysisRuntimeIdentityJson\" IS NULL AND \"AnalysisRuntimeIdentityHash\" IS NULL AND \"UpdatedAt\" IS NULL) OR (\"Phase\" IN ('Prepared', 'Running', 'PausedProviderUnavailable', 'PausedOperatorCorrection', 'CompletedWithWarnings', 'FailedIntegrity', 'FailedSecurity') AND \"PromptIdentity\" IS NOT NULL AND \"DocumentSchemaIdentity\" IS NOT NULL AND \"AnalysisRuntimeIdentityJson\" IS NOT NULL AND \"AnalysisRuntimeIdentityHash\" IS NOT NULL AND \"UpdatedAt\" IS NOT NULL) OR (\"Phase\" IN ('Completed', 'Cancelled') AND ((\"PromptIdentity\" IS NULL AND \"DocumentSchemaIdentity\" IS NULL AND \"AnalysisRuntimeIdentityJson\" IS NULL AND \"AnalysisRuntimeIdentityHash\" IS NULL AND \"UpdatedAt\" IS NULL) OR (\"PromptIdentity\" IS NOT NULL AND \"DocumentSchemaIdentity\" IS NOT NULL AND \"AnalysisRuntimeIdentityJson\" IS NOT NULL AND \"AnalysisRuntimeIdentityHash\" IS NOT NULL AND \"UpdatedAt\" IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScanExecutions_PauseFailure_Shape",
                schema: "vulgata",
                table: "ScanExecutions",
                sql: "(\"Phase\" = 'PausedProviderUnavailable' AND \"PauseCategory\" = 'ProviderUnavailable' AND \"PauseCode\" IS NOT NULL AND \"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR (\"Phase\" = 'PausedOperatorCorrection' AND \"PauseCategory\" = 'OperatorCorrection' AND \"PauseCode\" IS NOT NULL AND \"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR (\"Phase\" = 'FailedIntegrity' AND \"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND \"FailureCategory\" = 'Integrity' AND \"FailureCode\" IS NOT NULL) OR (\"Phase\" = 'FailedSecurity' AND \"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND \"FailureCategory\" = 'Security' AND \"FailureCode\" IS NOT NULL) OR (\"Phase\" NOT IN ('PausedProviderUnavailable', 'PausedOperatorCorrection', 'FailedIntegrity', 'FailedSecurity') AND \"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND ((\"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR (\"Phase\" = 'Cancelled' AND \"FailureCategory\" = 'Cancellation' AND \"FailureCode\" IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScanExecutions_Phase_Closed",
                schema: "vulgata",
                table: "ScanExecutions",
                sql: "\"Phase\" IN ('Pass1Planning', 'Pass1Running', 'Pass1Completed', 'Completed', 'Failed', 'Cancelled', 'Prepared', 'Running', 'PausedProviderUnavailable', 'PausedOperatorCorrection', 'CompletedWithWarnings', 'FailedIntegrity', 'FailedSecurity')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentVersions_SchemaIdentity_Closed",
                schema: "vulgata",
                table: "DocumentVersions",
                sql: "\"SchemaIdentity\" = 'vulgata.knowledge-document/1.0'");
        }
    }
}
