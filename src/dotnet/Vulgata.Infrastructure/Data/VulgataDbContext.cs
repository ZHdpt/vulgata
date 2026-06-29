using Microsoft.EntityFrameworkCore;
using SystemEntity = Vulgata.Core.Entities.System;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public class VulgataDbContext(DbContextOptions<VulgataDbContext> options) : DbContext(options)
{
    public DbSet<SystemEntity> Systems => Set<SystemEntity>();
    public DbSet<RepositoryEntity> Repositories => Set<RepositoryEntity>();
    public DbSet<LlmProvider> LlmProviders => Set<LlmProvider>();
    public DbSet<SystemLlmProviderOverride> SystemLlmProviderOverrides => Set<SystemLlmProviderOverride>();
    public DbSet<SystemOwnerAssignment> SystemOwnerAssignments => Set<SystemOwnerAssignment>();
    public DbSet<GlobalContext> GlobalContexts => Set<GlobalContext>();
    public DbSet<PendingContextChange> PendingContextChanges => Set<PendingContextChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("vulgata");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VulgataDbContext).Assembly);
    }
}
