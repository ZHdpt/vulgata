using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October;

public sealed class OctoberPersistenceModelTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OctoberModel_UsesFixedOctoberSchema()
    {
        using OctoberDbContext october = CreateOctoberModelContext();

        Assert.Equal("october", october.Model.GetDefaultSchema());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OctoberModel_UsesDedicatedMigrationsHistoryTable()
    {
        using OctoberDbContext october = CreateOctoberModelContext();

        string historyScript = october.GetService<IHistoryRepository>().GetCreateScript();
        Assert.Contains("october.\"__OctoberMigrationsHistory\"", historyScript);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OctoberModel_HasSeparateModelSnapshot()
    {
        using OctoberDbContext october = CreateOctoberModelContext();

        ModelSnapshot? snapshot = october.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);
        Assert.Equal("october", snapshot!.Model.GetDefaultSchema());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OrdinaryVulgataModel_ContainsNoOctoberMappings()
    {
        using VulgataDbContext ordinary = CreateOrdinaryModelContext();

        Assert.DoesNotContain(ordinary.Model.GetEntityTypes(), entity =>
            string.Equals(entity.GetSchema(), "october", StringComparison.Ordinal) ||
            entity.ClrType.Namespace?.Contains(".October", StringComparison.Ordinal) == true);
    }

    private static OctoberDbContext CreateOctoberModelContext()
    {
        DbContextOptions<OctoberDbContext> options = new DbContextOptionsBuilder<OctoberDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=october_model_only;Username=model;Password=model")
            .Options;

        return new OctoberDbContext(options);
    }

    private static VulgataDbContext CreateOrdinaryModelContext()
    {
        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=vulgata_model_only;Username=model;Password=model")
            .Options;

        return new VulgataDbContext(options);
    }
}
