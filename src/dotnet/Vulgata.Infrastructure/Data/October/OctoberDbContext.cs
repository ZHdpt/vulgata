using Microsoft.EntityFrameworkCore;

namespace Vulgata.Infrastructure.Data.October;

public sealed class OctoberDbContext : DbContext
{
    public OctoberDbContext(DbContextOptions<OctoberDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(npgsqlOptions =>
            npgsqlOptions.MigrationsHistoryTable("__OctoberMigrationsHistory", "october"));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("october");
    }
}
