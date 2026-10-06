using Microsoft.EntityFrameworkCore;
using Vulgata.Core.Entities.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;

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

    public DbSet<OctoberPreparedSourceApprovalRecord> PreparedSourceApprovals =>
        Set<OctoberPreparedSourceApprovalRecord>();

    public DbSet<KnowledgeOperation> KnowledgeOperations => Set<KnowledgeOperation>();
    public DbSet<AcceptedFileManifest> AcceptedFileManifests => Set<AcceptedFileManifest>();
    public DbSet<CodeUnit> CodeUnits => Set<CodeUnit>();
    public DbSet<LogicalDocument> LogicalDocuments => Set<LogicalDocument>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentSectionRecord> DocumentSections => Set<DocumentSectionRecord>();
    public DbSet<QualifiedClaimGroupRecord> ClaimGroups => Set<QualifiedClaimGroupRecord>();
    public DbSet<KnowledgeContributorRecord> KnowledgeContributors => Set<KnowledgeContributorRecord>();
    public DbSet<CommunicationObservationRecord> CommunicationObservations => Set<CommunicationObservationRecord>();
    public DbSet<KnowledgeUncertaintyRecord> KnowledgeUncertainties => Set<KnowledgeUncertaintyRecord>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateKnowledgeChanges();
        foreach (var batch in ChangedSetupBatches())
            if (KnowledgeOperations.Any(x => x.BatchId == batch))
                throw new InvalidOperationException("Accepted October setup is immutable.");
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateKnowledgeChanges();
        foreach (var batch in ChangedSetupBatches())
            if (await KnowledgeOperations.AnyAsync(x => x.BatchId == batch, cancellationToken))
                throw new InvalidOperationException("Accepted October setup is immutable.");
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateKnowledgeChanges()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries().Any(x => x.Entity is IOctoberKnowledgeRecord &&
            x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Accepted October Knowledge is append-only.");
        var added = ChangeTracker.Entries().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        foreach (var child in added.OfType<IOctoberKnowledgeRecord>())
        {
            bool hasNewParent = child switch
            {
                KnowledgeOperation => true,
                AcceptedFileManifest row => added.OfType<KnowledgeOperation>().Any(x => x.BatchId == row.BatchId && x.Kind == row.OperationKind && x.OperationId == row.OperationId),
                CodeUnit row => added.OfType<AcceptedFileManifest>().Any(x => x.BatchId == row.BatchId && x.ManifestId == row.ManifestId),
                LogicalDocument row => added.OfType<CodeUnit>().Any(x => x.BatchId == row.BatchId && x.UnitId == row.UnitId),
                DocumentVersion row => added.OfType<AcceptedFileManifest>().Any(x => x.BatchId == row.BatchId && x.ManifestId == row.ManifestId),
                DocumentSectionRecord row => added.OfType<DocumentVersion>().Any(x => x.BatchId == row.BatchId && x.VersionId == row.VersionId),
                QualifiedClaimGroupRecord row => added.OfType<DocumentSectionRecord>().Any(x => x.BatchId == row.BatchId && x.VersionId == row.VersionId && x.SectionId == row.SectionId),
                KnowledgeContributorRecord row => added.OfType<AcceptedFileManifest>().Any(x => x.BatchId == row.BatchId && x.ManifestId == row.ManifestId),
                CommunicationObservationRecord row => added.OfType<CodeUnit>().Any(x => x.BatchId == row.BatchId && x.ManifestId == row.ManifestId && x.UnitId == row.UnitId),
                KnowledgeUncertaintyRecord row => added.OfType<AcceptedFileManifest>().Any(x => x.BatchId == row.BatchId && x.ManifestId == row.ManifestId),
                _ => false
            };
            if (!hasNewParent) throw new InvalidOperationException("Cannot extend committed October Knowledge.");
        }
    }

    private IEnumerable<Guid> ChangedSetupBatches() => ChangeTracker.Entries()
        .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
        .Select(x => x.Entity switch
        {
            OctoberBatchRecord row => row.BatchId,
            OctoberRepositoryRecord row => row.BatchId,
            OctoberContextReferenceRecord row => row.BatchId,
            OctoberAdvisoryEntryRecord row => row.BatchId,
            OctoberContextDifferenceRecord row => row.BatchId,
            _ => Guid.Empty
        }).Where(x => x != Guid.Empty).Distinct();

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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OctoberDbContext).Assembly,
            type => type.Namespace == typeof(KnowledgeOperationConfiguration).Namespace);

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

        modelBuilder.Entity<OctoberPreparedSourceApprovalRecord>(approval =>
        {
            approval.ToTable("OctoberPreparedSourceApprovals", table =>
                table.HasCheckConstraint("CK_OctoberPreparedSourceApprovals_Purpose", "\"Purpose\" = 0"));
            approval.HasKey(value => value.ApprovalIdentity);
            approval.Property(value => value.ApprovalIdentity).IsRequired();
            approval.Property(value => value.SubjectId).IsRequired();
            approval.Property(value => value.RootBindingIdentity).IsRequired();
            approval.Property(value => value.OperatorLabel).IsRequired();
            approval.HasOne<OctoberRepositoryRecord>().WithMany()
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

public sealed class OctoberPreparedSourceApprovalRecord
{
    public string ApprovalIdentity { get; set; } = string.Empty;
    public Guid BatchId { get; set; }
    public Guid RepositoryId { get; set; }
    public string SubjectId { get; set; } = string.Empty;
    public int Purpose { get; set; }
    public string RootBindingIdentity { get; set; } = string.Empty;
    public string OperatorLabel { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool IsRevoked { get; set; }
}
