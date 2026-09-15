using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRetainedSourceSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "SourceSnapshots",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfiledRevision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GitObjectFormat = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    ManifestSchemaIdentity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ManifestSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EntryCount = table.Column<long>(type: "bigint", nullable: false),
                    TotalBlobBytes = table.Column<long>(type: "bigint", nullable: false),
                    InternalStorageKey = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceSnapshots", x => x.Id);
                    table.CheckConstraint("CK_SourceSnapshots_Counts", "\"EntryCount\" >= 0 AND \"TotalBlobBytes\" >= 0");
                    table.CheckConstraint("CK_SourceSnapshots_InternalStorageKey", "length(\"InternalStorageKey\") BETWEEN 1 AND 2048");
                    table.CheckConstraint("CK_SourceSnapshots_ManifestSchemaIdentity", "\"ManifestSchemaIdentity\" = 'retained-source-manifest/1.0'");
                    table.CheckConstraint("CK_SourceSnapshots_ManifestSha256", "length(\"ManifestSha256\") = 64 AND replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(\"ManifestSha256\", '0', ''), '1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), '8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '') = ''");
                    table.CheckConstraint("CK_SourceSnapshots_ProfiledRevision", "((\"GitObjectFormat\" = 'sha1' AND length(\"ProfiledRevision\") = 40) OR (\"GitObjectFormat\" = 'sha256' AND length(\"ProfiledRevision\") = 64)) AND replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(\"ProfiledRevision\", '0', ''), '1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), '8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '') = ''");
                    table.ForeignKey(
                        name: "FK_SourceSnapshots_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "vulgata",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceSnapshots_InternalStorageKey",
                schema: "vulgata",
                table: "SourceSnapshots",
                column: "InternalStorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceSnapshots_RepositoryId_ProfiledRevision",
                schema: "vulgata",
                table: "SourceSnapshots",
                columns: ["RepositoryId", "ProfiledRevision"],
                unique: true);

            if (ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    """
                    CREATE FUNCTION "vulgata"."RejectSourceSnapshotMutation"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    AS $function$
                    BEGIN
                        RAISE EXCEPTION 'SourceSnapshots are immutable.';
                    END;
                    $function$;

                    CREATE TRIGGER "TR_SourceSnapshots_Immutable"
                    BEFORE UPDATE OR DELETE ON "vulgata"."SourceSnapshots"
                    FOR EACH ROW EXECUTE FUNCTION "vulgata"."RejectSourceSnapshotMutation"();
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "SourceSnapshots",
                schema: "vulgata");

            if (ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    "DROP FUNCTION IF EXISTS \"vulgata\".\"RejectSourceSnapshotMutation\"();");
            }
        }
    }
}
