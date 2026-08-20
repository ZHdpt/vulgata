using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.Migrations;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.PreparedScanning;

internal sealed class PreparedScanPostgreSqlFixture : IAsyncDisposable
{
    private const string ProfiledRevision = "0123456789abcdef0123456789abcdef01234567";
    private readonly DisposablePostgreSqlDatabase _database;

    private PreparedScanPostgreSqlFixture(
        DisposablePostgreSqlDatabase database,
        VulgataDbContext context,
        Guid systemId,
        PreparedScanPairPackage package)
    {
        _database = database;
        Context = context;
        SystemId = systemId;
        Package = package;
    }

    internal VulgataDbContext Context { get; }
    internal Guid SystemId { get; }
    internal PreparedScanPairPackage Package { get; }
    internal string DatabaseName => _database.DatabaseName;
    internal string ConnectionString => _database.ConnectionString;

    internal PreparedScanAuthorizationScope Authorization => new(
        Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
        SystemId,
        Package.Repositories.Select(repository => repository.RepositoryId).ToArray());

    internal static async Task<PreparedScanPostgreSqlFixture> CreateAsync(
        string administratorConnectionString)
    {
        DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        try
        {
            VulgataDbContext context = CreateContext(database.ConnectionString);
            await context.Database.MigrateAsync();

            DateTimeOffset now = PreparedScanTestHost.FixedUtcNow;
            SystemEntity system = new("Prepared Story 12.2 PostgreSQL", "qualification", "global test context", now);
            Repository firstRepository = system.AddRepository(
                "prepared-pg-one", "https://example.invalid/prepared-pg-one.git", null, "repository one context", now);
            Repository secondRepository = system.AddRepository(
                "prepared-pg-two", "https://example.invalid/prepared-pg-two.git", null, "repository two context", now);
            ScanRun firstRun = ScanRun.Start(firstRepository.Id, 1_800, now);
            firstRun.BindProfiledRevision(ProfiledRevision);
            ScanRun secondRun = ScanRun.Start(secondRepository.Id, 1_800, now);
            secondRun.BindProfiledRevision(ProfiledRevision);
            ScanContextSnapshot firstSnapshot = ScanContextSnapshot.CapturePrepared(
                firstRun, system.Context, "prepared system context", firstRepository.Context, now);
            ScanContextSnapshot secondSnapshot = ScanContextSnapshot.CapturePrepared(
                secondRun, system.Context, "prepared system context", secondRepository.Context, now);
            PreScanResult firstResult = PreScanResult.CreateApproved(
                firstRepository.Id, firstRun.Id, ProfiledRevision, now, new string('a', 64),
                "预扫描已批准——继续进入顺序演示运行器", ConfirmedCommunicationKinds.None,
                0, 0, 0, false, []);
            PreScanResult secondResult = PreScanResult.CreateApproved(
                secondRepository.Id, secondRun.Id, ProfiledRevision, now, new string('b', 64),
                "预扫描已批准——继续进入顺序演示运行器", ConfirmedCommunicationKinds.None,
                0, 0, 0, false, []);
            ScanExecution firstExecution = ScanExecution.Start(
                Guid.NewGuid(), firstRepository.Id, system.Id, firstRun.Id, firstResult.Id, ProfiledRevision, now);
            ScanExecution secondExecution = ScanExecution.Start(
                Guid.NewGuid(), secondRepository.Id, system.Id, secondRun.Id, secondResult.Id, ProfiledRevision, now);
            TwoRepositoryScanCoordination coordination = TwoRepositoryScanCoordination.Create(
                firstExecution, secondExecution, now);

            context.Systems.Add(system);
            context.ScanRuns.AddRange(firstRun, secondRun);
            context.PreScanResults.AddRange(firstResult, secondResult);
            context.ScanExecutions.AddRange(firstExecution, secondExecution);
            context.TwoRepositoryScanCoordinations.Add(coordination);
            await context.SaveChangesAsync();

            PreparedScanPairPackage package = CreateCanonicalPackage(
                system.Id,
                firstRepository.Id,
                secondRepository.Id,
                firstRun.Id,
                secondRun.Id,
                coordination.Id,
                firstSnapshot,
                secondSnapshot);
            return new(database, context, system.Id, package);
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    internal static VulgataDbContext CreateContext(string connectionString)
    {
        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .Options;
        return new VulgataDbContext(options);
    }

    internal static PreparedScanStore CreateStore(VulgataDbContext context) => new(context);

    private static PreparedScanPairPackage CreateCanonicalPackage(
        Guid systemId,
        Guid firstRepositoryId,
        Guid secondRepositoryId,
        Guid firstScanRunId,
        Guid secondScanRunId,
        Guid coordinationId,
        ScanContextSnapshot firstSnapshot,
        ScanContextSnapshot secondSnapshot)
    {
        Type owner = typeof(PreparedScanAtomicReplayTests);
        Type principalType = owner.GetNestedType(
            "PreparedPrincipalSet", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The accepted T041 canonical package fixture is unavailable.");
        ConstructorInfo constructor = principalType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 8);
        object principals = constructor.Invoke(
            [systemId, firstRepositoryId, secondRepositoryId, firstScanRunId,
             secondScanRunId, coordinationId, firstSnapshot, secondSnapshot]);
        MethodInfo builder = owner.GetMethod(
            "CompletePairPackage", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The accepted T041 canonical package builder is unavailable.");
        return (PreparedScanPairPackage)(builder.Invoke(null, [principals])
            ?? throw new InvalidOperationException("The canonical package builder returned no package."));
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _database.DisposeAsync();
    }
}
