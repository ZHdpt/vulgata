using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableScanFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ScanExecutions_Phase_Closed",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.AddColumn<string>(
                name: "AnalysisRuntimeIdentityHash",
                schema: "vulgata",
                table: "ScanExecutions",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisRuntimeIdentityJson",
                schema: "vulgata",
                table: "ScanExecutions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentSchemaIdentity",
                schema: "vulgata",
                table: "ScanExecutions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCategory",
                schema: "vulgata",
                table: "ScanExecutions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                schema: "vulgata",
                table: "ScanExecutions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PauseCategory",
                schema: "vulgata",
                table: "ScanExecutions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PauseCode",
                schema: "vulgata",
                table: "ScanExecutions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptIdentity",
                schema: "vulgata",
                table: "ScanExecutions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StateVersion",
                schema: "vulgata",
                table: "ScanExecutions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "vulgata",
                table: "ScanExecutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScanContextSnapshots",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfiledRevision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GlobalContext = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    SystemContext = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    RepositoryContext = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    SchemaIdentity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanContextSnapshots", x => x.Id);
                    table.UniqueConstraint("AK_ScanContextSnapshots_ScanRunId_RepositoryId", x => new { x.ScanRunId, x.RepositoryId });
                    table.CheckConstraint("CK_ScanContextSnapshots_ContentHash_Length", "length(\"ContentHash\") = 64");
                    table.CheckConstraint("CK_ScanContextSnapshots_SchemaIdentity_Closed", "\"SchemaIdentity\" = 'scan-context-snapshot/1.0'");
                    table.ForeignKey(
                        name: "FK_ScanContextSnapshots_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "vulgata",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanContextSnapshots_ScanRuns_ScanRunId_RepositoryId",
                        columns: x => new { x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScanJobExecutions",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileCodeUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManifestOrdinal = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    StateVersion = table.Column<long>(type: "bigint", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CommittedDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    StateChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanJobExecutions", x => x.Id);
                    table.CheckConstraint("CK_ScanJobExecutions_Failure_Shape", "(\"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR (\"FailureCategory\" IS NOT NULL AND \"FailureCode\" IS NOT NULL)");
                    table.CheckConstraint("CK_ScanJobExecutions_FailureCategory_Closed", "\"FailureCategory\" IS NULL OR \"FailureCategory\" IN ('ProviderTransient', 'ProviderConfiguration', 'JobTransient', 'JobPermanent', 'Integrity', 'Security', 'Cancellation', 'Unclassified')");
                    table.CheckConstraint("CK_ScanJobExecutions_LeaseRetryCompletion_Shape", "(\"State\" = 'Running' AND \"LeaseToken\" IS NOT NULL AND \"LeaseExpiresAt\" IS NOT NULL AND \"LeaseExpiresAt\" > \"StateChangedAt\" AND \"NextAttemptAt\" IS NULL AND \"CommittedDocumentVersionId\" IS NULL) OR (\"State\" = 'RetryScheduled' AND \"LeaseToken\" IS NULL AND \"LeaseExpiresAt\" IS NULL AND \"NextAttemptAt\" IS NOT NULL AND \"CommittedDocumentVersionId\" IS NULL) OR (\"State\" = 'Completed' AND \"LeaseToken\" IS NULL AND \"LeaseExpiresAt\" IS NULL AND \"NextAttemptAt\" IS NULL AND \"CommittedDocumentVersionId\" IS NOT NULL) OR (\"State\" IN ('Pending', 'Failed', 'Cancelled') AND \"LeaseToken\" IS NULL AND \"LeaseExpiresAt\" IS NULL AND \"NextAttemptAt\" IS NULL AND \"CommittedDocumentVersionId\" IS NULL)");
                    table.CheckConstraint("CK_ScanJobExecutions_OrdinalAttemptVersion_Bounds", "\"ManifestOrdinal\" > 0 AND \"AttemptCount\" >= 0 AND \"StateVersion\" >= 0");
                    table.CheckConstraint("CK_ScanJobExecutions_State_Closed", "\"State\" IN ('Pending', 'Running', 'RetryScheduled', 'Completed', 'Failed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_ScanJobExecutions_CodeUnits_FileCodeUnitId_ScanRunId_Reposi~",
                        columns: x => new { x.FileCodeUnitId, x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "CodeUnits",
                        principalColumns: new[] { "Id", "ScanRunId", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanJobExecutions_DocumentVersions_CommittedDocumentVersion~",
                        columns: x => new { x.CommittedDocumentVersionId, x.RepositoryId, x.ScanRunId },
                        principalSchema: "vulgata",
                        principalTable: "DocumentVersions",
                        principalColumns: new[] { "Id", "RepositoryId", "ScanRunId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanJobExecutions_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "vulgata",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanJobExecutions_ScanExecutions_ScanExecutionId_Repository~",
                        columns: x => new { x.ScanExecutionId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanExecutions",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanJobExecutions_ScanRuns_ScanRunId_RepositoryId",
                        columns: x => new { x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TwoRepositoryScanCoordinations",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryOneId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryTwoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunOneId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunTwoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanExecutionOneId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanExecutionTwoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfiledRevisionOne = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProfiledRevisionTwo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Phase = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TerminalAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwoRepositoryScanCoordinations", x => x.Id);
                    table.CheckConstraint("CK_TwoRepositoryScanCoordinations_Phase_Closed", "\"Phase\" IN ('Preparing', 'Pass1', 'Resolving', 'Pass2', 'Paused', 'Completed', 'CompletedWithWarnings', 'FailedIntegrity', 'FailedSecurity', 'Cancelled')");
                    table.CheckConstraint("CK_TwoRepositoryScanCoordinations_ReasonCategory_Closed", "\"ReasonCategory\" IS NULL OR \"ReasonCategory\" IN ('ProviderUnavailable', 'OperatorCorrection', 'Integrity', 'Security', 'Cancellation', 'Unclassified')");
                    table.CheckConstraint("CK_TwoRepositoryScanCoordinations_Repositories_Distinct", "\"RepositoryOneId\" <> \"RepositoryTwoId\"");
                    table.CheckConstraint("CK_TwoRepositoryScanCoordinations_StateVersion_Bound", "\"StateVersion\" >= 0");
                    table.CheckConstraint("CK_TwoRepositoryScanCoordinations_TerminalReason_Shape", "(\"Phase\" IN ('Completed', 'CompletedWithWarnings') AND \"TerminalAt\" IS NOT NULL AND \"ReasonCategory\" IS NULL AND \"ReasonCode\" IS NULL) OR (\"Phase\" IN ('FailedIntegrity', 'FailedSecurity', 'Cancelled') AND \"TerminalAt\" IS NOT NULL AND \"ReasonCategory\" IS NOT NULL AND \"ReasonCode\" IS NOT NULL) OR (\"Phase\" = 'Paused' AND \"TerminalAt\" IS NULL AND \"ReasonCategory\" IS NOT NULL AND \"ReasonCode\" IS NOT NULL) OR (\"Phase\" IN ('Preparing', 'Pass1', 'Resolving', 'Pass2') AND \"TerminalAt\" IS NULL AND \"ReasonCategory\" IS NULL AND \"ReasonCode\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_TwoRepositoryScanCoordinations_Repositories_RepositoryOneId",
                        column: x => x.RepositoryOneId,
                        principalSchema: "vulgata",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TwoRepositoryScanCoordinations_Repositories_RepositoryTwoId",
                        column: x => x.RepositoryTwoId,
                        principalSchema: "vulgata",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TwoRepositoryScanCoordinations_ScanExecutions_ScanExecution~",
                        columns: x => new { x.ScanExecutionOneId, x.RepositoryOneId },
                        principalSchema: "vulgata",
                        principalTable: "ScanExecutions",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TwoRepositoryScanCoordinations_ScanExecutions_ScanExecutio~1",
                        columns: x => new { x.ScanExecutionTwoId, x.RepositoryTwoId },
                        principalSchema: "vulgata",
                        principalTable: "ScanExecutions",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TwoRepositoryScanCoordinations_ScanRuns_ScanRunOneId_Reposi~",
                        columns: x => new { x.ScanRunOneId, x.RepositoryOneId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TwoRepositoryScanCoordinations_ScanRuns_ScanRunTwoId_Reposi~",
                        columns: x => new { x.ScanRunTwoId, x.RepositoryTwoId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TwoRepositoryScanCoordinations_Systems_SystemId",
                        column: x => x.SystemId,
                        principalSchema: "vulgata",
                        principalTable: "Systems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "CK_ScanExecutions_StateVersion_Bound",
                schema: "vulgata",
                table: "ScanExecutions",
                sql: "\"StateVersion\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ScanContextSnapshots_RepositoryId",
                schema: "vulgata",
                table: "ScanContextSnapshots",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanContextSnapshots_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "ScanContextSnapshots",
                columns: new[] { "ScanRunId", "RepositoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanJobExecutions_CommittedDocumentVersionId",
                schema: "vulgata",
                table: "ScanJobExecutions",
                column: "CommittedDocumentVersionId",
                unique: true,
                filter: "\"CommittedDocumentVersionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScanJobExecutions_CommittedDocumentVersionId_RepositoryId_S~",
                schema: "vulgata",
                table: "ScanJobExecutions",
                columns: new[] { "CommittedDocumentVersionId", "RepositoryId", "ScanRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanJobExecutions_FileCodeUnitId_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "ScanJobExecutions",
                columns: new[] { "FileCodeUnitId", "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanJobExecutions_RepositoryId",
                schema: "vulgata",
                table: "ScanJobExecutions",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanJobExecutions_ScanExecutionId_FileCodeUnitId",
                schema: "vulgata",
                table: "ScanJobExecutions",
                columns: new[] { "ScanExecutionId", "FileCodeUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanJobExecutions_ScanExecutionId_ManifestOrdinal",
                schema: "vulgata",
                table: "ScanJobExecutions",
                columns: new[] { "ScanExecutionId", "ManifestOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanJobExecutions_ScanExecutionId_RepositoryId",
                schema: "vulgata",
                table: "ScanJobExecutions",
                columns: new[] { "ScanExecutionId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanJobExecutions_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "ScanJobExecutions",
                columns: new[] { "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_TwoRepositoryScanCoordinations_RepositoryOneId",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations",
                column: "RepositoryOneId");

            migrationBuilder.CreateIndex(
                name: "IX_TwoRepositoryScanCoordinations_RepositoryTwoId",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations",
                column: "RepositoryTwoId");

            migrationBuilder.CreateIndex(
                name: "IX_TwoRepositoryScanCoordinations_ScanExecutionOneId_Repositor~",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations",
                columns: new[] { "ScanExecutionOneId", "RepositoryOneId" });

            migrationBuilder.CreateIndex(
                name: "IX_TwoRepositoryScanCoordinations_ScanExecutionTwoId_Repositor~",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations",
                columns: new[] { "ScanExecutionTwoId", "RepositoryTwoId" });

            migrationBuilder.CreateIndex(
                name: "IX_TwoRepositoryScanCoordinations_ScanRunOneId_RepositoryOneId",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations",
                columns: new[] { "ScanRunOneId", "RepositoryOneId" });

            migrationBuilder.CreateIndex(
                name: "IX_TwoRepositoryScanCoordinations_ScanRunTwoId_RepositoryTwoId",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations",
                columns: new[] { "ScanRunTwoId", "RepositoryTwoId" });

            migrationBuilder.CreateIndex(
                name: "IX_TwoRepositoryScanCoordinations_SystemId",
                schema: "vulgata",
                table: "TwoRepositoryScanCoordinations",
                column: "SystemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScanContextSnapshots",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "ScanJobExecutions",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "TwoRepositoryScanCoordinations",
                schema: "vulgata");

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
                name: "CK_ScanExecutions_StateVersion_Bound",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropColumn(
                name: "AnalysisRuntimeIdentityHash",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropColumn(
                name: "AnalysisRuntimeIdentityJson",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropColumn(
                name: "DocumentSchemaIdentity",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropColumn(
                name: "FailureCategory",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropColumn(
                name: "PauseCategory",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropColumn(
                name: "PauseCode",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropColumn(
                name: "PromptIdentity",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropColumn(
                name: "StateVersion",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "vulgata",
                table: "ScanExecutions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScanExecutions_Phase_Closed",
                schema: "vulgata",
                table: "ScanExecutions",
                sql: "\"Phase\" IN ('Pass1Planning', 'Pass1Running', 'Pass1Completed', 'Completed', 'Failed', 'Cancelled')");
        }
    }
}
