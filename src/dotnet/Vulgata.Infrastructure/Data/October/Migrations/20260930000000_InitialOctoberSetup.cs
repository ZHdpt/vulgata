using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Vulgata.Infrastructure.Data.October.Migrations;

[DbContext(typeof(OctoberDbContext))]
[Migration("20260930000000_InitialOctoberSetup")]
public sealed class InitialOctoberSetup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "october");

        migrationBuilder.CreateTable(
            name: "OctoberBatches",
            schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                SelectionSlot = table.Column<int>(type: "integer", nullable: false),
                TargetId = table.Column<string>(type: "text", nullable: false),
                SubjectId = table.Column<string>(type: "text", nullable: false),
                CanonicalDatabaseIdentity = table.Column<string>(type: "text", nullable: false),
                SelectionDigest = table.Column<string>(type: "character varying(64)", maxLength: 64,
                    nullable: false),
                ManagementAuthorityId = table.Column<string>(type: "text", nullable: false),
                SelectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                State = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberBatches", value => value.BatchId);
                table.CheckConstraint("CK_OctoberBatches_State", "\"State\" IN (0, 1)");
            });

        migrationBuilder.CreateTable(
            name: "OctoberRepositorySelections",
            schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                SystemId = table.Column<Guid>(type: "uuid", nullable: true),
                RootBindingIdentity = table.Column<string>(type: "text", nullable: false),
                OperatorLabel = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberRepositorySelections", value => new
                {
                    value.BatchId, value.RepositoryId
                });
                table.ForeignKey("FK_OctoberRepositorySelections_OctoberBatches_BatchId",
                    column: value => value.BatchId,
                    principalSchema: "october", principalTable: "OctoberBatches", principalColumn: "BatchId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OctoberContextReferences",
            schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                Scope = table.Column<int>(type: "integer", nullable: false),
                RecordId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberContextReferences", value => new
                {
                    value.BatchId, value.Scope, value.RecordId
                });
                table.ForeignKey("FK_OctoberContextReferences_OctoberBatches_BatchId",
                    column: value => value.BatchId,
                    principalSchema: "october", principalTable: "OctoberBatches", principalColumn: "BatchId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OctoberAdvisoryEntries",
            schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                Scope = table.Column<int>(type: "integer", nullable: false),
                Applicability = table.Column<int>(type: "integer", nullable: false),
                OriginRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                Value = table.Column<string>(type: "text", nullable: true),
                EvidenceKind = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberAdvisoryEntries", value => new
                {
                    value.BatchId, value.RepositoryId, value.Scope
                });
                table.ForeignKey("FK_OctoberAdvisoryEntries_OctoberRepositorySelections_BatchId_RepositoryId",
                    columns: value => new { value.BatchId, value.RepositoryId },
                    principalSchema: "october", principalTable: "OctoberRepositorySelections",
                    principalColumns: new[] { "BatchId", "RepositoryId" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OctoberContextDifferences",
            schema: "october",
            columns: table => new
            {
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                LeftScope = table.Column<int>(type: "integer", nullable: false),
                RightScope = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberContextDifferences", value => new
                {
                    value.BatchId, value.RepositoryId, value.LeftScope, value.RightScope
                });
                table.ForeignKey("FK_OctoberContextDifferences_OctoberRepositorySelections_BatchId_RepositoryId",
                    columns: value => new { value.BatchId, value.RepositoryId },
                    principalSchema: "october", principalTable: "OctoberRepositorySelections",
                    principalColumns: new[] { "BatchId", "RepositoryId" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OctoberBatches_SelectionSlot",
            schema: "october",
            table: "OctoberBatches",
            column: "SelectionSlot",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OctoberAdvisoryEntries", schema: "october");
        migrationBuilder.DropTable(name: "OctoberContextDifferences", schema: "october");
        migrationBuilder.DropTable(name: "OctoberContextReferences", schema: "october");
        migrationBuilder.DropTable(name: "OctoberRepositorySelections", schema: "october");
        migrationBuilder.DropTable(name: "OctoberBatches", schema: "october");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("october");
        modelBuilder.Entity<OctoberBatchRecord>(batch =>
        {
            batch.ToTable("OctoberBatches", table =>
                table.HasCheckConstraint("CK_OctoberBatches_State", "\"State\" IN (0, 1)"));
            batch.HasKey(value => value.BatchId);
            batch.HasIndex(value => value.SelectionSlot).IsUnique();
            batch.Property(value => value.TargetId).IsRequired();
            batch.Property(value => value.SubjectId).IsRequired();
            batch.Property(value => value.CanonicalDatabaseIdentity).IsRequired();
            batch.Property(value => value.SelectionDigest).IsRequired().HasMaxLength(64);
            batch.Property(value => value.ManagementAuthorityId).IsRequired();
        });
        modelBuilder.Entity<OctoberRepositoryRecord>(repository =>
        {
            repository.ToTable("OctoberRepositorySelections");
            repository.HasKey(value => new { value.BatchId, value.RepositoryId });
            repository.Property(value => value.RootBindingIdentity).IsRequired();
            repository.Property(value => value.OperatorLabel).IsRequired();
            repository.HasOne<OctoberBatchRecord>().WithMany().HasForeignKey(value => value.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OctoberContextReferenceRecord>(reference =>
        {
            reference.ToTable("OctoberContextReferences");
            reference.HasKey(value => new { value.BatchId, value.Scope, value.RecordId });
            reference.HasOne<OctoberBatchRecord>().WithMany().HasForeignKey(value => value.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OctoberAdvisoryEntryRecord>(entry =>
        {
            entry.ToTable("OctoberAdvisoryEntries");
            entry.HasKey(value => new { value.BatchId, value.RepositoryId, value.Scope });
            entry.HasOne<OctoberRepositoryRecord>().WithMany()
                .HasForeignKey(value => new { value.BatchId, value.RepositoryId })
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OctoberContextDifferenceRecord>(difference =>
        {
            difference.ToTable("OctoberContextDifferences");
            difference.HasKey(value => new
            {
                value.BatchId, value.RepositoryId, value.LeftScope, value.RightScope
            });
            difference.HasOne<OctoberRepositoryRecord>().WithMany()
                .HasForeignKey(value => new { value.BatchId, value.RepositoryId })
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
