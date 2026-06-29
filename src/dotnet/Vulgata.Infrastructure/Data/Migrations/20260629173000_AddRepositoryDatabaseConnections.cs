using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    public partial class AddRepositoryDatabaseConnections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DatabaseConnections",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EncryptedConnectionString = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DatabaseType = table.Column<int>(type: "integer", nullable: false),
                    EncryptedUsername = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EncryptedPassword = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatabaseConnections_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "vulgata",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseConnections_RepositoryId",
                schema: "vulgata",
                table: "DatabaseConnections",
                column: "RepositoryId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatabaseConnections",
                schema: "vulgata");
        }
    }
}
