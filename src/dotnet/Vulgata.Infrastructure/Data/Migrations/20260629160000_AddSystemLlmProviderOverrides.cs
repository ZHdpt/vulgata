using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    public partial class AddSystemLlmProviderOverrides : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemLlmProviderOverrides",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LlmProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemLlmProviderOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemLlmProviderOverrides_LlmProviders_LlmProviderId",
                        column: x => x.LlmProviderId,
                        principalSchema: "vulgata",
                        principalTable: "LlmProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SystemLlmProviderOverrides_Systems_SystemId",
                        column: x => x.SystemId,
                        principalSchema: "vulgata",
                        principalTable: "Systems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemLlmProviderOverrides_LlmProviderId",
                schema: "vulgata",
                table: "SystemLlmProviderOverrides",
                column: "LlmProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLlmProviderOverrides_SystemId_AgentType",
                schema: "vulgata",
                table: "SystemLlmProviderOverrides",
                columns: new[] { "SystemId", "AgentType" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemLlmProviderOverrides",
                schema: "vulgata");
        }
    }
}
