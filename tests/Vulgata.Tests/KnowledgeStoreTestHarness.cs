using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

internal static class KnowledgeStoreTestHarness
{
    internal static async Task<SqliteKnowledgeStoreSession> CreateSqliteSessionAsync(
        params IInterceptor[] interceptors)
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        DbContextOptionsBuilder<VulgataDbContext> options = new();
        options.UseSqlite(connection);
        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }

        var context = new VulgataDbContext(options.Options);
        await context.Database.EnsureCreatedAsync();
        return new SqliteKnowledgeStoreSession(connection, context);
    }

    internal static void AssertTrackerClean(VulgataDbContext context) =>
        Assert.Empty(context.ChangeTracker.Entries());
}

internal sealed class SqliteKnowledgeStoreSession(
    SqliteConnection connection,
    VulgataDbContext context) : IAsyncDisposable
{
    internal VulgataDbContext Context { get; } = context;

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }
}

internal sealed class ParameterCaptureInterceptor : DbCommandInterceptor
{
    private readonly List<(string CommandText, IReadOnlyList<object?> Parameters)> _commands = [];

    internal IReadOnlyList<(string CommandText, IReadOnlyList<object?> Parameters)> Commands =>
        _commands;

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Capture(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Capture(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return ValueTask.FromResult(result);
    }

    private void Capture(DbCommand command)
    {
        _commands.Add((
            command.CommandText,
            command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value).ToArray()));
    }
}

internal sealed class SaveChangesFailureInterceptor(int failureOrdinal) : SaveChangesInterceptor
{
    private int _saveOrdinal;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ThrowWhenSelected();
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ThrowWhenSelected();
        return ValueTask.FromResult(result);
    }

    private void ThrowWhenSelected()
    {
        _saveOrdinal++;
        if (_saveOrdinal == failureOrdinal)
        {
            throw new InjectedKnowledgeStorePersistenceException();
        }
    }
}

internal sealed class InjectedKnowledgeStorePersistenceException : Exception;

internal sealed class PostgreSqlBarrier(int participants)
{
    private readonly Barrier _barrier = new(participants);

    internal Task SignalAndWaitAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => _barrier.SignalAndWait(cancellationToken), cancellationToken);
}
