using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    public partial class InitialVulgataDomain : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "vulgata");

            migrationBuilder.CreateTable(
                name: "GlobalContexts",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Context = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalContexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LlmProviders",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BaseEndpointUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EncryptedApiKey = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SupportedApiTypes = table.Column<int>(type: "integer", nullable: false),
                    DefaultAgentType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingContextChanges",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<int>(type: "integer", nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Context = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingContextChanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Systems",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Context = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Systems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Repositories",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GitUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Context = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repositories_Systems_SystemId",
                        column: x => x.SystemId,
                        principalSchema: "vulgata",
                        principalTable: "Systems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SystemOwnerAssignments",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemOwnerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemOwnerAssignments_Systems_SystemId",
                        column: x => x.SystemId,
                        principalSchema: "vulgata",
                        principalTable: "Systems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlmProviders_NormalizedName",
                schema: "vulgata",
                table: "LlmProviders",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingContextChanges_ScopeType_ScopeKey",
                schema: "vulgata",
                table: "PendingContextChanges",
                columns: new[] { "ScopeType", "ScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_NormalizedName",
                schema: "vulgata",
                table: "Repositories",
                columns: new[] { "SystemId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Standalone_NormalizedName",
                schema: "vulgata",
                table: "Repositories",
                column: "NormalizedName",
                unique: true,
                filter: "\"SystemId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_SystemId",
                schema: "vulgata",
                table: "Repositories",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Systems_NormalizedName",
                schema: "vulgata",
                table: "Systems",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemOwnerAssignments_SystemId_UserId",
                schema: "vulgata",
                table: "SystemOwnerAssignments",
                columns: new[] { "SystemId", "UserId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlobalContexts",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "LlmProviders",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "PendingContextChanges",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Repositories",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "SystemOwnerAssignments",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Systems",
                schema: "vulgata");
        }
    }
}
