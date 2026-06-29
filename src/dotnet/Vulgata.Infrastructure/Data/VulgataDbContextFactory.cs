using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vulgata.Infrastructure.Data;

public sealed class VulgataDbContextFactory : IDesignTimeDbContextFactory<VulgataDbContext>
{
    public VulgataDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<VulgataDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=vulgata;Username=vulgata;Password=vulgata",
            npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(3));

        return new VulgataDbContext(optionsBuilder.Options);
    }
}
