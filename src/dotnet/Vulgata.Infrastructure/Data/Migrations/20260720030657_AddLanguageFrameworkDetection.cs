using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageFrameworkDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodeUnitFrameworks",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameworkName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Confidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DetectionSource = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeUnitFrameworks", x => x.Id);
                    table.CheckConstraint("CK_CodeUnitFrameworks_Confidence", "\"Confidence\" IN ('High', 'Medium')");
                    table.CheckConstraint(
                        "CK_CodeUnitFrameworks_FrameworkName",
                        "\"FrameworkName\" ~ '^[a-z][a-z0-9-]{0,63}$'");
                    table.ForeignKey(
                        name: "FK_CodeUnitFrameworks_CodeUnits_CodeUnitId_ScanRunId_Repositor~",
                        columns: x => new { x.CodeUnitId, x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "CodeUnits",
                        principalColumns: new[] { "Id", "ScanRunId", "RepositoryId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CodeUnitFrameworks_ScanRuns_ScanRunId_RepositoryId",
                        columns: x => new { x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScanRunLanguageSummaries",
                schema: "vulgata",
                columns: table => new
                {
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DistinctFileCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanRunLanguageSummaries", x => new { x.ScanRunId, x.Language });
                    table.CheckConstraint("CK_ScanRunLanguageSummaries_DistinctFileCount", "\"DistinctFileCount\" > 0");
                    table.CheckConstraint(
                        "CK_ScanRunLanguageSummaries_Language",
                        "\"Language\" ~ '^[a-z][a-z0-9_+#.-]{0,63}$'");
                    table.ForeignKey(
                        name: "FK_ScanRunLanguageSummaries_ScanRuns_ScanRunId_RepositoryId",
                        columns: x => new { x.ScanRunId, x.RepositoryId },
                        principalSchema: "vulgata",
                        principalTable: "ScanRuns",
                        principalColumns: new[] { "Id", "RepositoryId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodeUnitFrameworks_CodeUnitId_FrameworkName",
                schema: "vulgata",
                table: "CodeUnitFrameworks",
                columns: new[] { "CodeUnitId", "FrameworkName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeUnitFrameworks_CodeUnitId_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "CodeUnitFrameworks",
                columns: new[] { "CodeUnitId", "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeUnitFrameworks_ScanRunId_FrameworkName",
                schema: "vulgata",
                table: "CodeUnitFrameworks",
                columns: new[] { "ScanRunId", "FrameworkName" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeUnitFrameworks_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "CodeUnitFrameworks",
                columns: new[] { "ScanRunId", "RepositoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanRunLanguageSummaries_ScanRunId_RepositoryId",
                schema: "vulgata",
                table: "ScanRunLanguageSummaries",
                columns: new[] { "ScanRunId", "RepositoryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodeUnitFrameworks",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "ScanRunLanguageSummaries",
                schema: "vulgata");
        }
    }
}
