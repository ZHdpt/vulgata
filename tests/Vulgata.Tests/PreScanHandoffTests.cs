using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared.Repositories;
using Vulgata.Web.Data;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests;

public sealed class PreScanHandoffTests
{
    private const string ProfiledRevision = "0123456789abcdef0123456789abcdef01234567";
    private const string AdvancedRevision = "fedcba9876543210fedcba9876543210fedcba98";

    [Fact]
    public async Task GetApprovedPreScanHandoffAsync_TruncatedBothKindResult_MaterializesCompleteBoundedOrderedContractAsync()
    {
        await using VulgataDbContext db = await CreateDatabaseAsync();
        RepositoryEntity repository = await AddStandaloneRepositoryAsync(db, "handoff-materialization");
        RepositoryProfileCommitDraft draft = await CreateApprovedDraftAsync(db, repository.Id);
        var sut = new ScanRunRepository(db);
        await sut.PersistCompletedProfileAsync(draft);
        db.ChangeTracker.Clear();

        ApprovedPreScanHandoff? handoff = await sut.GetApprovedPreScanHandoffAsync(
            repository.Id,
            draft.Profile.ScanRun.Id);

        Assert.NotNull(handoff);
        Assert.Equal(draft.Profile.ScanRun.Id, handoff.ResultId);
        Assert.Equal(repository.Id, handoff.RepositoryId);
        Assert.Equal(draft.Profile.ScanRun.Id, handoff.ScanRunId);
        Assert.Equal(ProfiledRevision, handoff.ProfiledRevision);
        Assert.Equal(PreScanApprovalState.PreScanApproved, handoff.State);
        Assert.Equal("1.0", handoff.SchemaVersion);
        Assert.Equal(
            [CommunicationKind.Rpc, CommunicationKind.Http],
            handoff.ConfirmedKinds);
        Assert.Equal(150, handoff.TotalConfirmedEvidenceCount);
        Assert.Equal(3, handoff.TotalHintCount);
        Assert.Equal(2, handoff.TotalWarningCount);
        Assert.True(handoff.EvidenceTruncated);
        Assert.Equal(
            [
                "src/Service.cs#annotation:rpc",
                "src/Client.cs#annotation:http-client",
            ],
            handoff.ConfirmedEvidence.Select(item => item.EvidenceLocator));
        Assert.Equal(
            ["src/Imports.cs#import:http"],
            handoff.Hints.Select(item => item.EvidenceLocator));
        Assert.Equal(
            ["(unknown)#detector:bound-exceeded"],
            handoff.Warnings.Select(item => item.EvidenceLocator));
        Assert.Contains(
            handoff.ConfirmedEvidence,
            item => item.Kind == CommunicationKind.Rpc);
        Assert.Contains(
            handoff.ConfirmedEvidence,
            item => item.Kind == CommunicationKind.Http);
    }

    [Fact]
    public async Task GetSystemRepositoryAsync_VisibleExactRevision_MapsSchemaFieldsWithoutPersistenceInternalsAsync()
    {
        (SystemEntity system, RepositoryEntity repository) = CreateSystemRepository();
        ApprovedPreScanHandoff handoff = CreateCoreHandoff(repository.Id);
        var systems = new SequencedSystemRepository(system, system);
        var repositories = new SequencedRepositoryRepository(repository, repository);
        var handoffs = new FixedHandoffRepository(handoff);
        var sut = new PreScanHandoffService(systems, repositories, handoffs);

        ApprovedPreScanHandoffDto? dto = await sut.GetSystemRepositoryAsync(
            system.Id,
            repository.Id,
            handoff.ScanRunId,
            ProfiledRevision,
            new TestUserContext());

        Assert.NotNull(dto);
        Assert.Equal("PreScanApproved", dto.State);
        Assert.Equal("1.0", dto.SchemaVersion);
        Assert.Equal(["Rpc", "Http"], dto.ConfirmedKinds);
        Assert.Equal(
            ["Rpc", "Http"],
            dto.ConfirmedEvidence.Select(item => item.Kind));
        Assert.Equal(["Http"], dto.Hints.Select(item => item.Kind));
        Assert.Null(Assert.Single(dto.Warnings).Kind);
        Assert.Equal(2, systems.VisibilityChecks);
        Assert.Equal(2, repositories.SystemVisibilityChecks);
        string json = JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("Fingerprint", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClassificationId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RelativePath", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Source", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Payload", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WorkingCopy", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Dispatch", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSystemRepositoryAsync_CheckoutAdvancedAfterApproval_ReturnsStableOriginalOnlyForRequestedProfiledRevisionAsync()
    {
        (SystemEntity system, RepositoryEntity repository) = CreateSystemRepository();
        ApprovedPreScanHandoff handoff = CreateCoreHandoff(repository.Id);
        var sut = new PreScanHandoffService(
            new SequencedSystemRepository(system, system, system, system),
            new SequencedRepositoryRepository(repository, repository, repository, repository),
            new FixedHandoffRepository(handoff));
        var user = new TestUserContext();

        ApprovedPreScanHandoffDto? original = await sut.GetSystemRepositoryAsync(
            system.Id,
            repository.Id,
            handoff.ScanRunId,
            ProfiledRevision,
            user);
        ApprovedPreScanHandoffDto? advanced = await sut.GetSystemRepositoryAsync(
            system.Id,
            repository.Id,
            handoff.ScanRunId,
            AdvancedRevision,
            user);

        Assert.NotNull(original);
        Assert.Equal(ProfiledRevision, original.ProfiledRevision);
        Assert.Null(advanced);
    }

    [Fact]
    public async Task GetSystemRepositoryAsync_WrongSystemHiddenMissingOrRevoked_DisclosesNoHandoffAsync()
    {
        (SystemEntity system, RepositoryEntity repository) = CreateSystemRepository();
        ApprovedPreScanHandoff handoff = CreateCoreHandoff(repository.Id);
        var user = new TestUserContext();

        var hiddenRead = new FixedHandoffRepository(handoff);
        var hidden = new PreScanHandoffService(
            new SequencedSystemRepository((SystemEntity?)null),
            new SequencedRepositoryRepository(repository),
            hiddenRead);
        Assert.Null(await hidden.GetSystemRepositoryAsync(
            system.Id,
            repository.Id,
            handoff.ScanRunId,
            ProfiledRevision,
            user));
        Assert.Equal(0, hiddenRead.MaterializationCount);

        (SystemEntity _, RepositoryEntity repositoryFromAnotherSystem) =
            CreateSystemRepository();
        var wrongSystemRead = new FixedHandoffRepository(handoff);
        var wrongSystem = new PreScanHandoffService(
            new SequencedSystemRepository(system),
            new SequencedRepositoryRepository(repositoryFromAnotherSystem),
            wrongSystemRead);
        Assert.Null(await wrongSystem.GetSystemRepositoryAsync(
            system.Id,
            repository.Id,
            handoff.ScanRunId,
            ProfiledRevision,
            user));
        Assert.Equal(0, wrongSystemRead.MaterializationCount);

        var missingRead = new FixedHandoffRepository(null);
        var missing = new PreScanHandoffService(
            new SequencedSystemRepository(system),
            new SequencedRepositoryRepository(repository),
            missingRead);
        Assert.Null(await missing.GetSystemRepositoryAsync(
            system.Id,
            repository.Id,
            handoff.ScanRunId,
            ProfiledRevision,
            user));

        var revokedRead = new FixedHandoffRepository(handoff);
        var revoked = new PreScanHandoffService(
            new SequencedSystemRepository(system, (SystemEntity?)null),
            new SequencedRepositoryRepository(repository, repository),
            revokedRead);
        Assert.Null(await revoked.GetSystemRepositoryAsync(
            system.Id,
            repository.Id,
            handoff.ScanRunId,
            ProfiledRevision,
            user));
        Assert.Equal(1, revokedRead.MaterializationCount);
    }

    [Theory]
    [InlineData(
        true,
        "预扫描已批准并保留审慎警告——继续进入顺序演示运行器")]
    [InlineData(
        false,
        "预扫描已批准——继续进入顺序演示运行器")]
    public async Task GetSystemRepositoryAsync_ExactApprovedOutcomeAfterVisibilityRevocation_DisclosesNothingAsync(
        bool hasWarnings,
        string approvedMessage)
    {
        (SystemEntity system, RepositoryEntity repository) = CreateSystemRepository();
        ApprovedPreScanHandoff handoff = CreateCoreHandoff(repository.Id, hasWarnings);
        var handoffs = new FixedHandoffRepository(handoff);
        var sut = new PreScanHandoffService(
            new SequencedSystemRepository(system, system, system, (SystemEntity?)null),
            new SequencedRepositoryRepository(repository, repository, repository),
            handoffs);
        var user = new TestUserContext();

        ApprovedPreScanHandoffDto? visible = await sut.GetSystemRepositoryAsync(
            system.Id,
            repository.Id,
            handoff.ScanRunId,
            ProfiledRevision,
            user);
        ApprovedPreScanHandoffDto? revoked = await sut.GetSystemRepositoryAsync(
            system.Id,
            repository.Id,
            handoff.ScanRunId,
            ProfiledRevision,
            user);

        Assert.NotNull(visible);
        Assert.Equal(approvedMessage, visible.ApprovalMessage);
        Assert.Equal(hasWarnings ? 2 : 0, visible.TotalWarningCount);
        Assert.Null(revoked);
        Assert.Equal("null", JsonSerializer.Serialize(revoked));
        Assert.Equal(2, handoffs.MaterializationCount);
    }

    [Fact]
    public async Task GetStandaloneRepositoryAsync_VisibilityRevokedAfterLoad_DisclosesNoHandoffAsync()
    {
        RepositoryEntity repository = RepositoryEntity.CreateStandalone(
            "standalone-handoff",
            "https://example.invalid/standalone-handoff.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        ApprovedPreScanHandoff handoff = CreateCoreHandoff(repository.Id);
        var repositories = new SequencedRepositoryRepository(
            repository,
            (RepositoryEntity?)null);
        var handoffs = new FixedHandoffRepository(handoff);
        var sut = new PreScanHandoffService(
            new SequencedSystemRepository(),
            repositories,
            handoffs);

        ApprovedPreScanHandoffDto? dto = await sut.GetStandaloneRepositoryAsync(
            repository.Id,
            handoff.ScanRunId,
            ProfiledRevision,
            new TestUserContext());

        Assert.Null(dto);
        Assert.Equal(2, repositories.StandaloneVisibilityChecks);
        Assert.Equal(1, handoffs.MaterializationCount);
    }

    private static ApprovedPreScanHandoff CreateCoreHandoff(
        Guid repositoryId,
        bool hasWarnings = true)
    {
        Guid scanRunId = Guid.NewGuid();
        DateTimeOffset approvedAt = DateTimeOffset.UtcNow;
        var classifications = new List<CommunicationKindClassification>
        {
            CreateClassification(
                scanRunId,
                CommunicationKind.Rpc,
                ClassificationLevel.Confirmed,
                ConfidenceBand.High,
                EvidenceRule.RpcDirectAnnotation,
                EvidenceProvenance.StructuralAnnotation,
                "src/Service.cs",
                "src/Service.cs#annotation:rpc",
                '1'),
            CreateClassification(
                scanRunId,
                CommunicationKind.Http,
                ClassificationLevel.Confirmed,
                ConfidenceBand.High,
                EvidenceRule.HttpClientAnnotation,
                EvidenceProvenance.StructuralAnnotation,
                "src/Client.cs",
                "src/Client.cs#annotation:http-client",
                '2'),
            CreateClassification(
                scanRunId,
                CommunicationKind.Http,
                ClassificationLevel.Hint,
                ConfidenceBand.Conservative,
                EvidenceRule.HttpImportOnly,
                EvidenceProvenance.Import,
                "src/Imports.cs",
                "src/Imports.cs#import:http",
                '3'),
        };
        if (hasWarnings)
        {
            classifications.Add(CreateClassification(
                scanRunId,
                null,
                ClassificationLevel.Warning,
                ConfidenceBand.None,
                EvidenceRule.BoundExceeded,
                EvidenceProvenance.Detector,
                CommunicationKindClassification.UnknownRelativePath,
                "(unknown)#detector:bound-exceeded",
                '4'));
        }

        return ApprovedPreScanHandoff.Create(
            scanRunId,
            repositoryId,
            scanRunId,
            ProfiledRevision,
            PreScanApprovalState.PreScanApproved,
            "1.0",
            approvedAt,
            hasWarnings
                ? "预扫描已批准并保留审慎警告——继续进入顺序演示运行器"
                : "预扫描已批准——继续进入顺序演示运行器",
            ConfirmedCommunicationKinds.Rpc | ConfirmedCommunicationKinds.Http,
            150,
            3,
            hasWarnings ? 2 : 0,
            true,
            classifications);
    }

    private static async Task<RepositoryProfileCommitDraft> CreateApprovedDraftAsync(
        VulgataDbContext db,
        Guid repositoryId)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        ScanRun run = ScanRun.Start(repositoryId, 1_800, startedAt);
        db.ScanRuns.Add(run);
        await db.SaveChangesAsync();
        string sourceHash = new('f', 64);
        var codeUnit = new CodeUnit(
            run.Id,
            repositoryId,
            "src/Prepared.cs",
            sourceHash,
            sourceHash,
            1,
            1,
            "Prepared",
            CodeUnitType.Class,
            RangeConfidence.Exact,
            "c_sharp");
        run.CodeUnits.Add(codeUnit);
        run.SetCandidateCount(1);
        run.BindProfiledRevision(ProfiledRevision);
        run.Complete(1, 1, startedAt.AddSeconds(1));
        var summary = new ScanRunLanguageSummary(run.Id, repositoryId, "c_sharp", 1);
        run.LanguageSummaries.Add(summary);
        var profile = RepositoryProfileDraft.Create(
            run,
            [codeUnit],
            [],
            [],
            [],
            [summary],
            []);
        ApprovedPreScanHandoff handoff = CreateCoreHandoff(repositoryId);
        CommunicationKindClassification[] classifications =
        [
            .. handoff.ConfirmedEvidence
                .Concat(handoff.Hints)
                .Concat(handoff.Warnings)
                .Select((item, index) => CreateClassification(
                    run.Id,
                    item.Kind,
                    item.Level,
                    item.Confidence,
                    item.Rule,
                    item.Provenance,
                    item.EvidenceLocator[..item.EvidenceLocator.IndexOf('#')],
                    item.EvidenceLocator,
                    (char)('5' + index))),
        ];
        PreScanResult result = PreScanResult.CreateApproved(
            repositoryId,
            run.Id,
            ProfiledRevision,
            startedAt.AddSeconds(2),
            new('a', 64),
            handoff.ApprovalMessage,
            ConfirmedCommunicationKinds.Rpc | ConfirmedCommunicationKinds.Http,
            150,
            3,
            2,
            true,
            classifications);
        return RepositoryProfileCommitDraft.Create(
            profile,
            PreScanResultDraft.Create(result));
    }

    private static CommunicationKindClassification CreateClassification(
        Guid scanRunId,
        CommunicationKind? kind,
        ClassificationLevel level,
        ConfidenceBand confidence,
        EvidenceRule rule,
        EvidenceProvenance provenance,
        string relativePath,
        string locator,
        char fingerprintCharacter) =>
        CommunicationKindClassification.Create(
            scanRunId,
            kind,
            level,
            confidence,
            rule,
            provenance,
            relativePath,
            locator,
            1,
            new string(fingerprintCharacter, 64));

    private static async Task<VulgataDbContext> CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new VulgataDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<RepositoryEntity> AddStandaloneRepositoryAsync(
        VulgataDbContext db,
        string name)
    {
        RepositoryEntity repository = RepositoryEntity.CreateStandalone(
            name,
            $"https://example.invalid/{name}.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        db.Repositories.Add(repository);
        await db.SaveChangesAsync();
        return repository;
    }

    private static (SystemEntity System, RepositoryEntity Repository) CreateSystemRepository()
    {
        var system = new SystemEntity(
            "handoff-system",
            null,
            null,
            DateTimeOffset.UtcNow);
        RepositoryEntity repository = system.AddRepository(
            "handoff-repository",
            "https://example.invalid/handoff.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        return (system, repository);
    }

    private sealed class TestUserContext : IUserContext
    {
        public string UserId => "handoff-user";
        public bool IsAdministrator => true;
        public SystemOwnerFilter ToSystemOwnerFilter() => SystemOwnerFilter.AllSystems();
    }

    private sealed class FixedHandoffRepository(ApprovedPreScanHandoff? handoff)
        : IScanRunRepository
    {
        public int MaterializationCount { get; private set; }

        public Task<ApprovedPreScanHandoff?> GetApprovedPreScanHandoffAsync(
            Guid repositoryId,
            Guid scanRunId,
            CancellationToken cancellationToken = default)
        {
            MaterializationCount++;
            return Task.FromResult(
                handoff?.RepositoryId == repositoryId && handoff.ScanRunId == scanRunId
                    ? handoff
                    : null);
        }

        public Task<ScanRun?> GetAsync(
            Guid repositoryId,
            Guid scanRunId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(ScanRun scanRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void AddCandidateOutcomes(IEnumerable<ScanCandidateOutcome> outcomes) =>
            throw new NotSupportedException();

        public void AddStructuralRecords(
            IEnumerable<CodeUnit> codeUnits,
            IEnumerable<CodeUnitEdge> edges,
            IEnumerable<UnresolvedStructuralRelationshipCandidate> unresolvedRelationships) =>
            throw new NotSupportedException();

        public Task PersistCompletedProfileAsync(
            RepositoryProfileDraft draft,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PreScanResult> PersistCompletedProfileAsync(
            RepositoryProfileCommitDraft draft,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RecoverFailedFinalCommitAsync(
            Guid repositoryId,
            Guid scanRunId,
            string sanitizedMessage,
            DateTimeOffset failedAt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SequencedSystemRepository(params SystemEntity?[] values)
        : ISystemRepository
    {
        private readonly Queue<SystemEntity?> _values = new(values);
        public int VisibilityChecks { get; private set; }

        public Task<SystemEntity?> GetVisibleByIdAsync(
            Guid id,
            SystemOwnerFilter filter,
            CancellationToken cancellationToken = default)
        {
            VisibilityChecks++;
            return Task.FromResult(_values.Count == 0 ? null : _values.Dequeue());
        }

        public Task<IReadOnlyList<SystemEntity>> ListVisibleAsync(
            SystemOwnerFilter filter,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SystemEntity?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> NameExistsAsync(
            string name,
            Guid? excludeSystemId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SystemEntity> AddAsync(
            SystemEntity system,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SystemDeleteResult> DeleteIfNoDependenciesAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SystemDependencyCounts> GetDependencyCountsAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SystemOwnerAssignmentWriteResult> AssignOwnerAsync(
            Guid systemId,
            string userId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveOwnerAsync(
            Guid systemId,
            string userId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SystemOwnerAssignmentSummary>> ListOwnersAsync(
            Guid systemId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountOwnerAssignmentsByUserAsync(
            string userId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SequencedRepositoryRepository(params RepositoryEntity?[] values)
        : IRepositoryRepository
    {
        private readonly Queue<RepositoryEntity?> _values = new(values);
        public int SystemVisibilityChecks { get; private set; }
        public int StandaloneVisibilityChecks { get; private set; }

        public Task<RepositoryEntity?> GetBySystemAndIdAsync(
            Guid systemId,
            Guid repositoryId,
            CancellationToken cancellationToken = default)
        {
            SystemVisibilityChecks++;
            return Task.FromResult(_values.Count == 0 ? null : _values.Dequeue());
        }

        public Task<RepositoryEntity?> GetStandaloneByIdAsync(
            Guid repositoryId,
            CancellationToken cancellationToken = default)
        {
            StandaloneVisibilityChecks++;
            return Task.FromResult(_values.Count == 0 ? null : _values.Dequeue());
        }

        public Task<IReadOnlyList<RepositoryEntity>> ListBySystemAsync(
            Guid systemId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RepositoryEntity>> ListStandaloneAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RepositoryEntity?> GetByIdAsync(
            Guid repositoryId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> NameExistsAsync(
            Guid systemId,
            string name,
            Guid? excludeRepositoryId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> StandaloneNameExistsAsync(
            string name,
            Guid? excludeRepositoryId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RepositoryEntity> AddAsync(
            RepositoryEntity repository,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Remove(RepositoryEntity repository) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
