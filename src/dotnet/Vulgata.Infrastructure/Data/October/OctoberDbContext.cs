using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberDialogue;
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

    public DbSet<CorpusSelectionRecord> CorpusSelections => Set<CorpusSelectionRecord>();
    public DbSet<CorpusManifestRecord> CorpusManifests => Set<CorpusManifestRecord>();
    public DbSet<CorpusDocumentRecord> CorpusDocuments => Set<CorpusDocumentRecord>();
    public DbSet<CorpusCoverageRecord> CorpusCoverage => Set<CorpusCoverageRecord>();
    public DbSet<KnowledgePublicationRecord> KnowledgePublications => Set<KnowledgePublicationRecord>();
    public DbSet<PublicationMemberRecord> PublicationMembers => Set<PublicationMemberRecord>();
    public DbSet<PublicationCoverageRecord> PublicationCoverage => Set<PublicationCoverageRecord>();
    public DbSet<PublicationUncertaintyRecord> PublicationUncertainties => Set<PublicationUncertaintyRecord>();

    public DbSet<DialogueConversationRecord> DialogueConversations => Set<DialogueConversationRecord>();
    public DbSet<DialogueTurnRecord> DialogueTurns => Set<DialogueTurnRecord>();
    public DbSet<DialogueTerminalRecord> DialogueTerminals => Set<DialogueTerminalRecord>();
    public DbSet<DialogueReferenceRecord> DialogueReferences => Set<DialogueReferenceRecord>();
    public DbSet<ResolutionItem> ResolutionItems => Set<ResolutionItem>();
    public DbSet<ResolutionDocumentDependencyRecord> ResolutionDocumentDependencies => Set<ResolutionDocumentDependencyRecord>();
    public DbSet<RelationshipVersion> RelationshipVersions => Set<RelationshipVersion>();
    public DbSet<UncertaintyVersion> UncertaintyVersions => Set<UncertaintyVersion>();
    public DbSet<PublicationRelationshipRecord> PublicationRelationships => Set<PublicationRelationshipRecord>();
    public DbSet<PublicationResolutionUncertaintyRecord> PublicationResolutionUncertainties => Set<PublicationResolutionUncertaintyRecord>();
    public DbSet<BusinessFinalizationRecord> BusinessFinalizations => Set<BusinessFinalizationRecord>();
    public DbSet<LogicalFlowRecord> LogicalFlows => Set<LogicalFlowRecord>();
    public DbSet<FlowGroupVersionRecord> FlowGroupVersions => Set<FlowGroupVersionRecord>();
    public DbSet<FlowDocumentDependencyRecord> FlowDocumentDependencies => Set<FlowDocumentDependencyRecord>();
    public DbSet<FlowRelationshipDependencyRecord> FlowRelationshipDependencies => Set<FlowRelationshipDependencyRecord>();
    public DbSet<FlowResolutionUncertaintyDependencyRecord> FlowResolutionUncertaintyDependencies => Set<FlowResolutionUncertaintyDependencyRecord>();
    public DbSet<FlowFileUncertaintyDependencyRecord> FlowFileUncertaintyDependencies => Set<FlowFileUncertaintyDependencyRecord>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateKnowledgeChanges();
        foreach (var batch in ChangedSetupBatches())
            if (KnowledgeOperations.Any(x => x.BatchId == batch))
                throw new InvalidOperationException("Accepted October setup is immutable.");
        foreach (var batch in NewAcceptanceBatches())
            if (CorpusSelections.Any(x => x.BatchId == batch))
                throw new InvalidOperationException("Cannot accept files after corpus freeze.");
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateKnowledgeChanges();
        foreach (var batch in ChangedSetupBatches())
            if (await KnowledgeOperations.AnyAsync(x => x.BatchId == batch, cancellationToken))
                throw new InvalidOperationException("Accepted October setup is immutable.");
        foreach (var batch in NewAcceptanceBatches())
            if (await CorpusSelections.AnyAsync(x => x.BatchId == batch, cancellationToken))
                throw new InvalidOperationException("Cannot accept files after corpus freeze.");
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateKnowledgeChanges()
    {
        ChangeTracker.DetectChanges();
        ValidateDialogueChanges();
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
                LogicalDocument row => row.Kind == DocumentKind.CodeLogic
                    ? added.OfType<CodeUnit>().Any(x => x.BatchId == row.BatchId && x.UnitId == row.UnitId)
                    : added.OfType<LogicalFlowRecord>().Any(x => x.BatchId == row.BatchId && x.LogicalFlowId == row.LogicalFlowId),
                DocumentVersion row => row.Kind == DocumentKind.CodeLogic
                    ? added.OfType<AcceptedFileManifest>().Any(x => x.BatchId == row.BatchId && x.ManifestId == row.ManifestId)
                    : added.OfType<BusinessFinalizationRecord>().Any(x => x.BatchId == row.BatchId && x.OperationKind == row.FinalizationOperationKind && x.OperationId == row.FinalizationOperationId),
                DocumentSectionRecord row => added.OfType<DocumentVersion>().Any(x => x.BatchId == row.BatchId && x.VersionId == row.VersionId),
                QualifiedClaimGroupRecord row => added.OfType<DocumentSectionRecord>().Any(x => x.BatchId == row.BatchId && x.VersionId == row.VersionId && x.SectionId == row.SectionId),
                KnowledgeContributorRecord row => added.OfType<AcceptedFileManifest>().Any(x => x.BatchId == row.BatchId && x.ManifestId == row.ManifestId),
                CommunicationObservationRecord row => added.OfType<CodeUnit>().Any(x => x.BatchId == row.BatchId && x.ManifestId == row.ManifestId && x.UnitId == row.UnitId),
                KnowledgeUncertaintyRecord row => added.OfType<AcceptedFileManifest>().Any(x => x.BatchId == row.BatchId && x.ManifestId == row.ManifestId),
                CorpusSelectionRecord row => added.OfType<KnowledgeOperation>().Any(x => x.BatchId == row.BatchId && x.Kind == row.OperationKind && x.OperationId == row.OperationId),
                CorpusManifestRecord row => added.OfType<CorpusSelectionRecord>().Any(x => x.BatchId == row.BatchId && x.CorpusId == row.CorpusId),
                CorpusDocumentRecord row => added.OfType<CorpusManifestRecord>().Any(x => x.BatchId == row.BatchId && x.CorpusId == row.CorpusId && x.ManifestId == row.ManifestId),
                CorpusCoverageRecord row => added.OfType<CorpusSelectionRecord>().Any(x => x.BatchId == row.BatchId && x.CorpusId == row.CorpusId),
                KnowledgePublicationRecord row => added.OfType<KnowledgeOperation>().Any(x => x.BatchId == row.BatchId && x.Kind == row.OperationKind && x.OperationId == row.OperationId),
                PublicationMemberRecord row => added.OfType<KnowledgePublicationRecord>().Any(x => x.BatchId == row.BatchId && x.PublicationId == row.PublicationId && x.CorpusId == row.CorpusId),
                PublicationCoverageRecord row => added.OfType<KnowledgePublicationRecord>().Any(x => x.BatchId == row.BatchId && x.PublicationId == row.PublicationId && x.CorpusId == row.CorpusId),
                PublicationUncertaintyRecord row => added.OfType<KnowledgePublicationRecord>().Any(x => x.BatchId == row.BatchId && x.PublicationId == row.PublicationId && x.CorpusId == row.CorpusId),
                ResolutionItem row => added.OfType<KnowledgeOperation>().Any(x => x.BatchId == row.BatchId && x.Kind == row.OperationKind && x.OperationId == row.OperationId),
                ResolutionDocumentDependencyRecord row => added.OfType<ResolutionItem>().Any(x => x.BatchId == row.BatchId && x.CorpusId == row.CorpusId && x.ItemId == row.ItemId),
                RelationshipVersion row => added.OfType<ResolutionItem>().Any(x => x.BatchId == row.BatchId && x.CorpusId == row.CorpusId && x.ItemId == row.ItemId),
                UncertaintyVersion row => added.OfType<ResolutionItem>().Any(x => x.BatchId == row.BatchId && x.CorpusId == row.CorpusId && x.ItemId == row.ItemId),
                PublicationRelationshipRecord row => added.OfType<KnowledgePublicationRecord>().Any(x => x.BatchId == row.BatchId && x.PublicationId == row.PublicationId && x.CorpusId == row.CorpusId),
                PublicationResolutionUncertaintyRecord row => added.OfType<KnowledgePublicationRecord>().Any(x => x.BatchId == row.BatchId && x.PublicationId == row.PublicationId && x.CorpusId == row.CorpusId),
                BusinessFinalizationRecord row => added.OfType<KnowledgeOperation>().Any(x => x.BatchId == row.BatchId && x.Kind == row.OperationKind && x.OperationId == row.OperationId),
                LogicalFlowRecord row => added.OfType<KnowledgeOperation>().Any(x => x.BatchId == row.BatchId && x.Kind == row.OperationKind && x.OperationId == row.OperationId),
                FlowGroupVersionRecord row => added.OfType<KnowledgeOperation>().Any(x => x.BatchId == row.BatchId && x.Kind == row.OperationKind && x.OperationId == row.OperationId),
                FlowDocumentDependencyRecord row => added.OfType<FlowGroupVersionRecord>().Any(x => x.BatchId == row.BatchId && x.CorpusId == row.CorpusId && x.GroupVersionId == row.GroupVersionId),
                FlowRelationshipDependencyRecord row => added.OfType<FlowGroupVersionRecord>().Any(x => x.BatchId == row.BatchId && x.CorpusId == row.CorpusId && x.GroupVersionId == row.GroupVersionId),
                FlowResolutionUncertaintyDependencyRecord row => added.OfType<FlowGroupVersionRecord>().Any(x => x.BatchId == row.BatchId && x.CorpusId == row.CorpusId && x.GroupVersionId == row.GroupVersionId),
                FlowFileUncertaintyDependencyRecord row => added.OfType<FlowGroupVersionRecord>().Any(x => x.BatchId == row.BatchId && x.CorpusId == row.CorpusId && x.GroupVersionId == row.GroupVersionId),
                _ => false
            };
            if (!hasNewParent) throw new InvalidOperationException("Cannot extend committed October Knowledge.");
        }
        ValidateDocumentOwnership(added);
    }

    private void ValidateDocumentOwnership(object[] added)
    {
        DocumentVersion? Version(Guid batch, Guid logical, Guid version) => added.OfType<DocumentVersion>()
            .SingleOrDefault(x => x.BatchId == batch && x.LogicalDocumentId == logical && x.VersionId == version) ??
            DocumentVersions.AsNoTracking().SingleOrDefault(x => x.BatchId == batch && x.LogicalDocumentId == logical && x.VersionId == version);
        FlowGroupVersionRecord? Group(Guid batch, Guid? group) => added.OfType<FlowGroupVersionRecord>()
            .SingleOrDefault(x => x.BatchId == batch && x.GroupVersionId == group) ??
            FlowGroupVersions.AsNoTracking().SingleOrDefault(x => x.BatchId == batch && x.GroupVersionId == group);
        foreach (var version in added.OfType<DocumentVersion>().Where(x => x.Kind == DocumentKind.BusinessLogic))
        {
            var logical = added.OfType<LogicalDocument>().SingleOrDefault(x => x.BatchId == version.BatchId && x.LogicalDocumentId == version.LogicalDocumentId) ??
                LogicalDocuments.AsNoTracking().SingleOrDefault(x => x.BatchId == version.BatchId && x.LogicalDocumentId == version.LogicalDocumentId);
            var group = Group(version.BatchId, version.FlowGroupVersionId);
            if (group is null || logical is null || logical.LogicalFlowId != group.LogicalFlowId)
                throw new InvalidOperationException("A BL version must belong to its exact logical flow.");
        }
        foreach (var member in added.OfType<CorpusDocumentRecord>())
        {
            var version = Version(member.BatchId, member.LogicalDocumentId, member.VersionId);
            if (version is null || version.Kind != DocumentKind.CodeLogic || version.ManifestId != member.ManifestId)
                throw new InvalidOperationException("Frozen corpus membership requires exact CL manifest ownership.");
        }
        foreach (var member in added.OfType<PublicationMemberRecord>())
        {
            var version = Version(member.BatchId, member.LogicalDocumentId, member.VersionId);
            bool valid = version is not null && (version.Kind == DocumentKind.CodeLogic
                ? CorpusDocuments.AsNoTracking().Any(x => x.BatchId == member.BatchId && x.CorpusId == member.CorpusId && x.LogicalDocumentId == member.LogicalDocumentId && x.VersionId == member.VersionId)
                : Group(member.BatchId, version.FlowGroupVersionId)?.CorpusId == member.CorpusId);
            if (!valid) throw new InvalidOperationException("Publication membership must pin an exact CL corpus or BL group version.");
        }
    }

    private void ValidateDialogueChanges()
    {
        foreach (var entry in ChangeTracker.Entries<DialogueConversationRecord>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified &&
                entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not (nameof(DialogueConversationRecord.NextTurnSequence) or nameof(DialogueConversationRecord.NextFence))))
                throw new InvalidOperationException("Dialogue identity is immutable.");
            if (entry.State == EntityState.Modified &&
                (entry.Entity.NextTurnSequence < entry.OriginalValues.GetValue<long>(nameof(DialogueConversationRecord.NextTurnSequence)) ||
                 entry.Entity.NextFence < entry.OriginalValues.GetValue<long>(nameof(DialogueConversationRecord.NextFence))))
                throw new InvalidOperationException("Dialogue counters cannot decrease.");
        }
        foreach (var entry in ChangeTracker.Entries<DialogueTurnRecord>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified &&
                entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not (nameof(DialogueTurnRecord.State) or nameof(DialogueTurnRecord.Fence))))
                throw new InvalidOperationException("The original dialogue admission is immutable.");
            if (entry.State != EntityState.Modified) continue;
            var old = entry.OriginalValues.GetValue<DialogueTurnState>(nameof(DialogueTurnRecord.State));
            if (old >= DialogueTurnState.Completed)
                throw new InvalidOperationException("Terminal dialogue state is immutable.");
            if (entry.Entity.State >= DialogueTurnState.Completed && !ChangeTracker.Entries<DialogueTerminalRecord>().Any(x =>
                x.State == EntityState.Added && x.Entity.ConversationId == entry.Entity.ConversationId && x.Entity.TurnId == entry.Entity.TurnId &&
                (int)x.Entity.TerminalState + 4 == (int)entry.Entity.State && x.Entity.Fence == entry.Entity.Fence))
                throw new InvalidOperationException("Terminal state and outcome must be saved together.");
        }
        foreach (var entry in ChangeTracker.Entries<DialogueTerminalRecord>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Dialogue terminal history is immutable.");
            if (entry.State == EntityState.Added && !ChangeTracker.Entries<DialogueTurnRecord>().Any(x =>
                x.State == EntityState.Modified && x.Entity.ConversationId == entry.Entity.ConversationId && x.Entity.TurnId == entry.Entity.TurnId &&
                (int)entry.Entity.TerminalState + 4 == (int)x.Entity.State && entry.Entity.Fence == x.Entity.Fence))
                throw new InvalidOperationException("A terminal requires the matching atomic turn transition.");
        }
        foreach (var entry in ChangeTracker.Entries<DialogueReferenceRecord>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted || entry.State == EntityState.Added &&
                !ChangeTracker.Entries<DialogueTerminalRecord>().Any(x => x.State == EntityState.Added &&
                    x.Entity.ConversationId == entry.Entity.ConversationId && x.Entity.TurnId == entry.Entity.TurnId))
                throw new InvalidOperationException("Terminal references cannot change after acceptance.");
        }
    }

    private IEnumerable<Guid> NewAcceptanceBatches() => ChangeTracker.Entries<AcceptedFileManifest>()
        .Where(x => x.State == EntityState.Added).Select(x => x.Entity.BatchId).Distinct();

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
