using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSequentialScanRunner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScanExecutions",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreScanResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfiledRevision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Phase = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InFlightJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedJobCount = table.Column<int>(type: "integer", nullable: false),
                    TotalJobCount = table.Column<int>(type: "integer", nullable: false),
                    RestartIndicator = table.Column<int>(type: "integer", nullable: false),
                    TerminalMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TerminalReasonKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TerminalAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanExecutions", x => x.Id);
                    table.UniqueConstraint("AK_ScanExecutions_Id_RepositoryId", x => new { x.Id, x.RepositoryId });
                    table.UniqueConstraint("AK_ScanExecutions_ScanRunId_RepositoryId", x => new { x.ScanRunId, x.RepositoryId });
                    table.CheckConstraint("CK_ScanExecutions_Phase_Closed", "\"Phase\" IN ('Pass1Planning', 'Pass1Running', 'Pass1Completed', 'Completed', 'Failed', 'Cancelled')");
                    table.CheckConstraint("CK_ScanExecutions_RestartIndicator_Bound", "0 <= \"RestartIndicator\" AND \"RestartIndicator\" <= 32");
                    table.CheckConstraint("CK_ScanExecutions_TerminalReasonKind_Closed", "\"TerminalReasonKind\" IS NULL OR \"TerminalReasonKind\" IN ('SchemaInvalid', 'Error', 'Unbounded', 'MissingPrerequisite', 'DuplicateActiveScan', 'MissingCodeUnit', 'ForbiddenContent', 'OverLimitPacket')");
                    table.ForeignKey(
                        name: "FK_ScanExecutions_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "vulgata",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanExecutions_ScanRuns_ScanRunId_RepositoryId",
                        columns: x => new { x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScanExecutions_RepositoryId",
                schema: "vulgata",
                table: "ScanExecutions",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanExecutions_RepositoryId_ScanRunId_Phase",
                schema: "vulgata",
                table: "ScanExecutions",
                columns: new[] { "RepositoryId", "ScanRunId", "Phase" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanExecutions_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "ScanExecutions",
                columns: new[] { "ScanRunId", "RepositoryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScanExecutions",
                schema: "vulgata");
        }
    }
}
