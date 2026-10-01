using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Vulgata.Infrastructure.Data.October.Migrations;

[DbContext(typeof(OctoberDbContext))]
[Migration("20260930010000_AddOctoberPreparedSourceApprovals")]
public sealed class AddOctoberPreparedSourceApprovals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OctoberPreparedSourceApprovals",
            schema: "october",
            columns: table => new
            {
                ApprovalIdentity = table.Column<string>(type: "text", nullable: false),
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                SubjectId = table.Column<string>(type: "text", nullable: false),
                Purpose = table.Column<int>(type: "integer", nullable: false),
                RootBindingIdentity = table.Column<string>(type: "text", nullable: false),
                OperatorLabel = table.Column<string>(type: "text", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IsRevoked = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OctoberPreparedSourceApprovals", value => value.ApprovalIdentity);
                table.CheckConstraint("CK_OctoberPreparedSourceApprovals_Purpose", "\"Purpose\" = 0");
                table.ForeignKey(
                    name: "FK_OctoberPreparedSourceApprovals_OctoberRepositorySelections_BatchId_RepositoryId",
                    columns: value => new { value.BatchId, value.RepositoryId },
                    principalSchema: "october",
                    principalTable: "OctoberRepositorySelections",
                    principalColumns: new[] { "BatchId", "RepositoryId" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OctoberPreparedSourceApprovals_BatchId_RepositoryId",
            schema: "october",
            table: "OctoberPreparedSourceApprovals",
            columns: new[] { "BatchId", "RepositoryId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OctoberPreparedSourceApprovals", schema: "october");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("october")
            .HasAnnotation("ProductVersion", "10.0.9")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
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
            batch.Property(value => value.SelectedAtUtc);
            batch.Property(value => value.State);
        });
        modelBuilder.Entity<OctoberRepositoryRecord>(repository =>
        {
            repository.ToTable("OctoberRepositorySelections");
            repository.HasKey(value => new { value.BatchId, value.RepositoryId });
            repository.Property(value => value.SystemId);
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
            entry.Property(value => value.Applicability);
            entry.Property(value => value.EvidenceKind);
            entry.Property(value => value.OriginRecordId);
            entry.Property(value => value.Value);
            entry.HasOne<OctoberRepositoryRecord>().WithMany()
                .HasForeignKey(value => new { value.BatchId, value.RepositoryId })
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
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
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
        modelBuilder.Entity<OctoberPreparedSourceApprovalRecord>(approval =>
        {
            approval.ToTable("OctoberPreparedSourceApprovals", table =>
                table.HasCheckConstraint("CK_OctoberPreparedSourceApprovals_Purpose", "\"Purpose\" = 0"));
            approval.HasKey(value => value.ApprovalIdentity);
            approval.HasIndex(value => new { value.BatchId, value.RepositoryId });
            approval.Property(value => value.ApprovalIdentity).IsRequired();
            approval.Property(value => value.SubjectId).IsRequired();
            approval.Property(value => value.RootBindingIdentity).IsRequired();
            approval.Property(value => value.OperatorLabel).IsRequired();
            approval.Property(value => value.ExpiresAtUtc);
            approval.Property(value => value.IsRevoked);
            approval.Property(value => value.Purpose);
            approval.HasOne<OctoberRepositoryRecord>().WithMany()
                .HasForeignKey(value => new { value.BatchId, value.RepositoryId })
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
    }
}
