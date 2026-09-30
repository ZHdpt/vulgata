using Microsoft.EntityFrameworkCore;

namespace Vulgata.Infrastructure.Data.October;

public sealed class OctoberDbContext : DbContext
{
    public OctoberDbContext(DbContextOptions<OctoberDbContext> options)
        : base(options)
    {
    }

    public DbSet<OctoberBatchRecord> Batches => Set<OctoberBatchRecord>();

    public DbSet<OctoberRepositoryRecord> RepositorySelections => Set<OctoberRepositoryRecord>();

    public DbSet<OctoberContextReferenceRecord> ContextReferences => Set<OctoberContextReferenceRecord>();

    public DbSet<OctoberAdvisoryEntryRecord> AdvisoryEntries => Set<OctoberAdvisoryEntryRecord>();

    public DbSet<OctoberContextDifferenceRecord> ContextDifferences => Set<OctoberContextDifferenceRecord>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        bool alternateProviderSelected = optionsBuilder.Options.Extensions.Any(extension =>
            extension.Info.IsDatabaseProvider &&
            extension.GetType().Assembly !=
            typeof(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder).Assembly);
        if (alternateProviderSelected)
        {
            return;
        }

        optionsBuilder.UseNpgsql(npgsqlOptions =>
            npgsqlOptions.MigrationsHistoryTable("__OctoberMigrationsHistory", "october"));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
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

public enum OctoberBatchState
{
    NotSelectable,
    Selectable
}

public sealed class OctoberBatchRecord
{
    public Guid BatchId { get; set; }
    public int SelectionSlot { get; set; } = 1;
    public string TargetId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string CanonicalDatabaseIdentity { get; set; } = string.Empty;
    public string SelectionDigest { get; set; } = string.Empty;
    public string ManagementAuthorityId { get; set; } = string.Empty;
    public DateTimeOffset SelectedAtUtc { get; set; }
    public OctoberBatchState State { get; set; }
}

public sealed class OctoberRepositoryRecord
{
    public Guid BatchId { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid? SystemId { get; set; }
    public string RootBindingIdentity { get; set; } = string.Empty;
    public string OperatorLabel { get; set; } = string.Empty;
}

public sealed class OctoberContextReferenceRecord
{
    public Guid BatchId { get; set; }
    public int Scope { get; set; }
    public Guid RecordId { get; set; }
}

public sealed class OctoberAdvisoryEntryRecord
{
    public Guid BatchId { get; set; }
    public Guid RepositoryId { get; set; }
    public int Scope { get; set; }
    public int Applicability { get; set; }
    public Guid? OriginRecordId { get; set; }
    public string? Value { get; set; }
    public int EvidenceKind { get; set; }
}

public sealed class OctoberContextDifferenceRecord
{
    public Guid BatchId { get; set; }
    public Guid RepositoryId { get; set; }
    public int LeftScope { get; set; }
    public int RightScope { get; set; }
}
