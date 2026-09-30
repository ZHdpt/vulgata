using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Vulgata.Infrastructure.Data.October.Migrations;

[DbContext(typeof(OctoberDbContext))]
public sealed class OctoberDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
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
