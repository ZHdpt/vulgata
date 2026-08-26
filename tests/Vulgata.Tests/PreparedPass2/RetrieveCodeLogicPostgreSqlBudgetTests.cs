using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.Migrations;
using Xunit.Abstractions;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T026 PostgreSQL REDs for the public retrieve_code_logic seam. The fixture
/// is the same authoritative terminal Pass 1 pair used by T020; only the
/// uniquely named database is created by this test.
/// </summary>
public sealed class RetrieveCodeLogicPostgreSqlBudgetTests(ITestOutputHelper output)
{
    [Fact]
    public async Task RetrieveAsync_EightCallsAreInclusiveAndNinthIsPersistedBoundRefusalAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            IPass2CodeLogicRetrievalService service = CreateService(context, scope);
            Pass2CodeLogicRetrievalRequest request = new(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "ledger");

            for (int ordinal = 1; ordinal <= Pass2RetrievalContracts.MaxCalls; ordinal++)
            {
                Pass2CodeLogicRetrievalResult result = await service.RetrieveAsync(jobId, request);
                Assert.True(
                    result.Outcome is Pass2CodeLogicRetrievalOutcome.Accepted or
                    Pass2CodeLogicRetrievalOutcome.NoResults,
                    $"call {ordinal} must complete without consuming an unavailable/refused path");
            }

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget budget = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(Pass2RetrievalContracts.MaxCalls, budget.CallsReserved);
            Assert.Equal(Pass2RetrievalContracts.MaxCalls,
                await context.Pass2RetrievalReceipts.CountAsync());
            int resultCountBeforeNinth = await context.Pass2RetrievalResults.CountAsync();

            Pass2CodeLogicRetrievalResult ninth = await service.RetrieveAsync(jobId, request);
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.BoundExceeded, ninth.Outcome);

            context.ChangeTracker.Clear();
            budget = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(Pass2RetrievalContracts.MaxCalls, budget.CallsReserved);
            Pass2RetrievalReceipt[] receipts = await context.Pass2RetrievalReceipts
                .OrderBy(receipt => receipt.CallOrdinal)
                .ToArrayAsync();
            Assert.Equal(Pass2RetrievalContracts.MaxCalls, receipts.Length);
            Assert.Equal(
                Enumerable.Range(1, Pass2RetrievalContracts.MaxCalls),
                receipts.Select(receipt => receipt.CallOrdinal));
            Assert.DoesNotContain(receipts, receipt => receipt.CallOrdinal == 9);
            Assert.Equal(resultCountBeforeNinth, await context.Pass2RetrievalResults.CountAsync());
        });
    }

    [Fact]
    public async Task RetrieveAsync_RepeatedRequestChargesCallsButKeepsDocumentAccountingCanonicalAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            IPass2CodeLogicRetrievalService service = CreateService(context, scope);
            Pass2CodeLogicRetrievalRequest request = new(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "ledger");

            Pass2CodeLogicRetrievalResult first = await service.RetrieveAsync(jobId, request);
            Pass2CodeLogicRetrievalResult second = await service.RetrieveAsync(jobId, request);
            Assert.True(first.Outcome is Pass2CodeLogicRetrievalOutcome.Accepted or
                Pass2CodeLogicRetrievalOutcome.NoResults);
            Assert.True(second.Outcome is Pass2CodeLogicRetrievalOutcome.Accepted or
                Pass2CodeLogicRetrievalOutcome.NoResults);

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget budget = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(2, budget.CallsReserved);
            Assert.InRange(budget.ResultsReturned, 0, Pass2RetrievalContracts.MaxResults);
            Assert.InRange(budget.AdditionalDocumentCount, 0,
                Pass2RetrievalContracts.MaxAdditionalDocuments);
            Assert.InRange(budget.ReturnedUtf8Bytes, 0, Pass2RetrievalContracts.MaxReturnedUtf8Bytes);
            Assert.Equal(2, await context.Pass2RetrievalReceipts.CountAsync());
        });
    }

    [Fact]
    public async Task RetrieveAsync_NewServiceInstanceDoesNotResetPersistedBudgetAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            Pass2CodeLogicRetrievalRequest request = new(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "ledger");
            Pass2CodeLogicRetrievalResult first = await CreateService(context, scope)
                .RetrieveAsync(jobId, request);
            Pass2CodeLogicRetrievalResult second = await CreateService(context, scope)
                .RetrieveAsync(jobId, request);

            Assert.True(first.Outcome is Pass2CodeLogicRetrievalOutcome.Accepted or
                Pass2CodeLogicRetrievalOutcome.NoResults);
            Assert.True(second.Outcome is Pass2CodeLogicRetrievalOutcome.Accepted or
                Pass2CodeLogicRetrievalOutcome.NoResults);

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget budget = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(2, budget.CallsReserved);
            Assert.Equal(2, await context.Pass2RetrievalReceipts.CountAsync());
        });
    }

    private async Task RunOnSeededPostgreSqlAsync(
        Func<VulgataDbContext, Guid, RepositoryAccessScope, Task> action)
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            output.WriteLine(
                "Qualification not executed: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
            return;
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        string databaseName = database.DatabaseName;
        output.WriteLine("Owned disposable PostgreSQL database: {0}", databaseName);
        try
        {
            await using ExactCrossRepositoryTestFixture fixture =
                await ExactCrossRepositoryTestFixture.CreateAsync();
            await using VulgataDbContext context = CreateContext(database.ConnectionString);
            await context.Database.MigrateAsync();

            (Pass1PairExecution pair, RepositoryAccessScope scope) =
                await SeedUsingT020Async(context, fixture);
            PreparedPass2StartResult start = await new PreparedPass2Store(context)
                .StartAsync(pair.Id, scope);
            Assert.Equal(PreparedPass2StartOutcome.Created, start.Outcome);
            Pass2JobExecution job = await context.Pass2JobExecutions.SingleAsync();
            await action(context, job.Id, scope);
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertDatabaseDroppedAsync(administratorConnectionString, databaseName);
    }

    private static async Task<(Pass1PairExecution Pair, RepositoryAccessScope Scope)> SeedUsingT020Async(
        VulgataDbContext context,
        ExactCrossRepositoryTestFixture fixture)
    {
        // T020 owns the accepted authoritative pair fixture. Invoke only its
        // private setup helper; the behavior under test remains this class's
        // public IPass2CodeLogicRetrievalService call.
        MethodInfo seedMethod = typeof(Pass2FlowPlanningPostgreSqlTests).GetMethod(
            "SeedPass2Async",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("T020 authoritative seed helper is missing.");
        object invocation = seedMethod.Invoke(null, [context, fixture])
            ?? throw new InvalidOperationException("T020 seed invocation returned null.");
        if (invocation is not Task seedTask)
        {
            throw new InvalidOperationException("T020 seed helper is not asynchronous.");
        }

        await seedTask;
        object seed = seedTask.GetType().GetProperty("Result")?.GetValue(seedTask)
            ?? throw new InvalidOperationException("T020 seed result is unavailable.");
        Pass1PairExecution pair = (Pass1PairExecution)(seed.GetType()
            .GetProperty("Pair")?.GetValue(seed)
            ?? throw new InvalidOperationException("T020 seed pair is unavailable."));
        RepositoryAccessScope scope = (RepositoryAccessScope)(seed.GetType()
            .GetProperty("Scope")?.GetValue(seed)
            ?? throw new InvalidOperationException("T020 seed scope is unavailable."));
        return (pair, scope);
    }

    private static IPass2CodeLogicRetrievalService CreateService(
        VulgataDbContext context,
        RepositoryAccessScope scope)
    {
        return new Pass2CodeLogicRetrievalService(context, scope, new UnavailableSourceReader());
    }

    private static VulgataDbContext CreateContext(string connectionString)
    {
        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                "public"))
            .Options;
        return new VulgataDbContext(options);
    }

    private static async Task AssertDatabaseDroppedAsync(
        string administratorConnectionString,
        string databaseName)
    {
        NpgsqlConnectionStringBuilder administrator = new(administratorConnectionString)
        {
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 15,
        };
        await using NpgsqlConnection connection = new(administrator.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            connection);
        command.Parameters.AddWithValue("name", databaseName);
        bool exists = (bool)(await command.ExecuteScalarAsync() ?? false);
        Assert.False(exists, $"Owned disposable database '{databaseName}' was not dropped.");
    }

    private sealed class UnavailableSourceReader : IRevisionBoundSourceReader
    {
        public Task<SourceExcerptResult> ReadAsync(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new SourceExcerptResult(
                AnalysisOperationStatus.Failed,
                scope.RepositoryId,
                scope.ProfiledRevision,
                request.RelativePath,
                request.Range,
                request.ExpectedSourceSha256,
                "analysis_unavailable",
                19,
                new AnalysisPortFailure(
                    AnalysisPortFailureReason.Unavailable,
                    "source_revision_lease_unavailable",
                    "source_revision_lease_unavailable")));
        }
    }
}
