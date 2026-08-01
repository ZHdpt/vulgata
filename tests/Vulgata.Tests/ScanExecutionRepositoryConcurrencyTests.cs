using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests;

public sealed class ScanExecutionRepositoryConcurrencyTests
{
    private const string ProfiledRevision = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task PersistAsync_TwoStaleRestartObservations_ReservesBeforeRevalidation()
    {
        string databasePath = Path.Combine(
            Path.GetTempPath(),
            $"vulgata-scan-execution-{Guid.NewGuid():N}.db");
        var commandCapture = new CommandCaptureInterceptor();
        DbContextOptions<VulgataDbContext> options = CreateOptions(
            databasePath,
            commandCapture);

        try
        {
            Guid executionId;
            await using (var setupContext = new VulgataDbContext(options))
            {
                await setupContext.Database.EnsureCreatedAsync();

                DateTimeOffset startedAt = DateTimeOffset.UtcNow;
                var system = new SystemEntity(
                    "F003 System",
                    null,
                    null,
                    startedAt);
                Repository repository = system.AddRepository(
                    "F003 Repository",
                    "https://example.com/f003.git",
                    null,
                    null,
                    startedAt);
                ScanRun scanRun = ScanRun.Start(repository.Id, 1_800, startedAt);
                ScanExecution execution = ScanExecution.Start(
                    Guid.NewGuid(),
                    repository.Id,
                    system.Id,
                    scanRun.Id,
                    scanRun.Id,
                    ProfiledRevision,
                    startedAt);
                execution.Plan(1, startedAt);
                execution.Advance(Guid.NewGuid());
                executionId = execution.Id;

                setupContext.Systems.Add(system);
                setupContext.ScanRuns.Add(scanRun);
                setupContext.ScanExecutions.Add(execution);
                await setupContext.SaveChangesAsync();
            }

            await using var firstContext = new VulgataDbContext(options);
            await using var secondContext = new VulgataDbContext(options);
            var firstRepository = new ScanExecutionRepository(firstContext);
            var secondRepository = new ScanExecutionRepository(secondContext);

            ScanExecution first = Assert.IsType<ScanExecution>(
                await firstRepository.GetAsync(executionId));
            ScanExecution second = Assert.IsType<ScanExecution>(
                await secondRepository.GetAsync(executionId));
            Assert.Equal(0, first.RestartIndicator);
            Assert.Equal(0, second.RestartIndicator);

            first.ObserveRestart();
            second.ObserveRestart();
            var releaseWriters = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Task firstWriter = Task.Run(async () =>
            {
                await releaseWriters.Task;
                await firstRepository.PersistAsync(first);
            });
            Task secondWriter = Task.Run(async () =>
            {
                await releaseWriters.Task;
                await secondRepository.PersistAsync(second);
            });

            releaseWriters.SetResult();
            await Task.WhenAll(firstWriter, secondWriter);

            await using var verificationContext = new VulgataDbContext(options);
            ScanExecution durable = await verificationContext.ScanExecutions
                .AsNoTracking()
                .SingleAsync(execution => execution.Id == executionId);

            Assert.Equal(1, durable.RestartIndicator);
            Assert.DoesNotContain(
                commandCapture.CommandTexts,
                command => command.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    private static DbContextOptions<VulgataDbContext> CreateOptions(
        string databasePath,
        DbCommandInterceptor interceptor) =>
        new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite(
                $"Data Source={databasePath};Cache=Shared;Default Timeout=30;Foreign Keys=True")
            .AddInterceptors(interceptor)
            .Options;

    private sealed class CommandCaptureInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<string> _commandTexts = new();

        internal IReadOnlyCollection<string> CommandTexts => _commandTexts;

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _commandTexts.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            _commandTexts.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
