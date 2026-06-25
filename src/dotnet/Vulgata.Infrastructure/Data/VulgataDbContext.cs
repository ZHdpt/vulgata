using Microsoft.EntityFrameworkCore;

namespace Vulgata.Infrastructure.Data;

public class VulgataDbContext(DbContextOptions<VulgataDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("vulgata");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VulgataDbContext).Assembly);
    }
}
