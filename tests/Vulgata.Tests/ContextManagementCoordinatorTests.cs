using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.ContextManagement;
using Vulgata.Infrastructure.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests;

/// <summary>
/// Unit tests for ContextManagementCoordinator, the new write-side seam that consolidates
/// the 6 inline endpoint handlers. Pins the pending/immediate branching, visibility filter,
/// and clear-on-apply contract previously duplicated across Global/System/Repository scopes.
/// </summary>
public sealed class ContextManagementCoordinatorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<VulgataDbContext> _dbFactory;
    private readonly FakeScanStateService _scanState = new();
    private readonly ISystemRepository _systemRepository;

    public ContextManagementCoordinatorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _dbFactory = new InMemoryDbContextFactory(options);
        _systemRepository = new SystemRepository(_dbFactory.CreateDbContext());

        using VulgataDbContext seedDb = _dbFactory.CreateDbContext();
        seedDb.Database.EnsureCreated();
    }

    [Fact]
    public async Task SaveAsync_GlobalScope_NoScanRunning_AppliesImmediatelyAndClearsPending()
    {
        ContextManagementCoordinator coordinator = BuildCoordinator();

        Result result = await coordinator.SaveAsync(ContextScope.Global, "全局术语", AdminUser(), default);

        Assert.True(result.IsSuccess);
        await using VulgataDbContext db = _dbFactory.CreateDbContext();
        GlobalContext? saved = await db.GlobalContexts.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal("全局术语", saved!.Context);
        Assert.Empty(await db.PendingContextChanges.ToListAsync());
    }

    [Fact]
    public async Task SaveAsync_GlobalScope_ScanRunning_QueuesPendingAndKeepsCurrent()
    {
        ContextManagementCoordinator coordinator = BuildCoordinator();
        _scanState.AnyScanRunning = true;

        Result result = await coordinator.SaveAsync(ContextScope.Global, "待生效上下文", AdminUser(), default);

        Assert.True(result.IsSuccess);
        await using VulgataDbContext db = _dbFactory.CreateDbContext();
        PendingContextChange? pending = await db.PendingContextChanges
            .Where(p => p.ScopeType == ContextScopeType.Global && p.ScopeKey == "global")
            .FirstOrDefaultAsync();
        Assert.NotNull(pending);
        Assert.Equal("待生效上下文", pending!.Context);
        Assert.Null(await db.GlobalContexts.FirstOrDefaultAsync());
    }

    [Fact]
    public async Task SaveAsync_SystemScope_NonOwnerUser_ReturnsNotFound()
    {
        ContextManagementCoordinator coordinator = BuildCoordinator();
        Guid systemId = await SeedSystemAsync("测试系统", ownerId: "owner-1");

        Result result = await coordinator.SaveAsync(
            ContextScope.ForSystem(systemId),
            "越权上下文",
            NonAdminUser("outsider-1"),
            default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ContextSaveOutcome.SystemNotFound, result.Outcome);
    }

    [Fact]
    public async Task SaveAsync_SystemScope_OwnerUser_Succeeds()
    {
        ContextManagementCoordinator coordinator = BuildCoordinator();
        Guid systemId = await SeedSystemAsync("测试系统", ownerId: "owner-1");

        Result result = await coordinator.SaveAsync(
            ContextScope.ForSystem(systemId),
            "系统上下文",
            NonAdminUser("owner-1"),
            default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SaveAsync_SystemScope_AdminUser_SucceedsEvenWithoutOwnership()
    {
        ContextManagementCoordinator coordinator = BuildCoordinator();
        Guid systemId = await SeedSystemAsync("测试系统", ownerId: "owner-1");

        Result result = await coordinator.SaveAsync(
            ContextScope.ForSystem(systemId),
            "管理员覆盖上下文",
            AdminUser(),
            default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SaveAsync_RepositoryScope_StandaloneRepo_NonAdminSucceeds()
    {
        ContextManagementCoordinator coordinator = BuildCoordinator();
        Guid repoId = await SeedStandaloneRepositoryAsync("独立仓库");

        Result result = await coordinator.SaveAsync(
            ContextScope.ForRepository(repoId),
            "独立仓库上下文",
            NonAdminUser("any-owner"),
            default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SaveAsync_GlobalScope_NonAdminUser_ReturnsForbidden()
    {
        ContextManagementCoordinator coordinator = BuildCoordinator();

        Result result = await coordinator.SaveAsync(
            ContextScope.Global,
            "越权全局上下文",
            NonAdminUser("owner-1"),
            default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ContextSaveOutcome.Forbidden, result.Outcome);
    }

    [Fact]
    public async Task SaveAsync_RepositoryScope_ScanRunning_QueuesPending()
    {
        ContextManagementCoordinator coordinator = BuildCoordinator();
        Guid repoId = await SeedStandaloneRepositoryAsync("扫描中仓库");
        _scanState.RepositoryScansRunning.Add(repoId);

        Result result = await coordinator.SaveAsync(
            ContextScope.ForRepository(repoId),
            "待生效仓库上下文",
            AdminUser(),
            default);

        Assert.True(result.IsSuccess);
        await using VulgataDbContext db = _dbFactory.CreateDbContext();
        PendingContextChange? pending = await db.PendingContextChanges
            .Where(p => p.ScopeType == ContextScopeType.Repository && p.ScopeKey == repoId.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(pending);
    }

    [Fact]
    public async Task GetStateAsync_GlobalScope_ReturnsCurrentAndPendingValues()
    {
        ContextManagementCoordinator coordinator = BuildCoordinator();
        await coordinator.SaveAsync(ContextScope.Global, "当前值", AdminUser(), default);
        _scanState.AnyScanRunning = true;
        await coordinator.SaveAsync(ContextScope.Global, "待生效值", AdminUser(), default);

        ContextState state = await coordinator.GetStateAsync(ContextScope.Global, AdminUser(), default);

        Assert.Equal("当前值", state.CurrentValue);
        Assert.Equal("待生效值", state.PendingValue);
        Assert.True(state.IsScanRunning);
    }

    private ContextManagementCoordinator BuildCoordinator() =>
        new(_dbFactory, _scanState, _systemRepository);

    private async Task<Guid> SeedSystemAsync(string name, string ownerId)
    {
        await using VulgataDbContext db = _dbFactory.CreateDbContext();
        SystemEntity system = new(name, null, null, DateTimeOffset.UtcNow);
        db.Systems.Add(system);
        db.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(system.Id, ownerId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return system.Id;
    }

    private async Task<Guid> SeedStandaloneRepositoryAsync(string name)
    {
        await using VulgataDbContext db = _dbFactory.CreateDbContext();
        Repository repo = Repository.CreateStandalone(name, "https://example.com/repo.git", null, null, DateTimeOffset.UtcNow);
        db.Repositories.Add(repo);
        await db.SaveChangesAsync();
        return repo.Id;
    }

    private static IUserContext AdminUser() => new FakeUserContext("admin-1", isAdministrator: true);

    private static IUserContext NonAdminUser(string userId) => new FakeUserContext(userId, isAdministrator: false);

    public void Dispose()
    {
        _connection.Dispose();
    }

    private sealed class InMemoryDbContextFactory : IDbContextFactory<VulgataDbContext>
    {
        private readonly DbContextOptions<VulgataDbContext> _options;

        public InMemoryDbContextFactory(DbContextOptions<VulgataDbContext> options)
        {
            _options = options;
        }

        public VulgataDbContext CreateDbContext()
        {
            return new VulgataDbContext(_options);
        }
    }

    private sealed class FakeScanStateService : IScanStateService
    {
        public bool AnyScanRunning { get; set; }
        public HashSet<Guid> SystemScansRunning { get; } = [];
        public HashSet<Guid> RepositoryScansRunning { get; } = [];

        public Task<bool> IsAnyScanRunningAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(AnyScanRunning);

        public Task<bool> IsSystemScanRunningAsync(Guid systemId, CancellationToken cancellationToken = default)
            => Task.FromResult(SystemScansRunning.Contains(systemId));

        public Task<bool> IsRepositoryScanRunningAsync(Guid repositoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(RepositoryScansRunning.Contains(repositoryId));
    }

    private sealed class FakeUserContext(string userId, bool isAdministrator) : IUserContext
    {
        public string UserId { get; } = userId;
        public bool IsAdministrator { get; } = isAdministrator;

        public SystemOwnerFilter ToSystemOwnerFilter() => isAdministrator
            ? SystemOwnerFilter.AllSystems()
            : SystemOwnerFilter.OwnedBy(UserId);
    }
}
