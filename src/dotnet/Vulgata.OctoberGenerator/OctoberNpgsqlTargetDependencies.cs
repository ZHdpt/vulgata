using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Infrastructure.Data.October;

namespace Vulgata.OctoberGenerator;

internal sealed class OctoberNpgsqlTargetDependencies(
    OctoberDbContext database,
    string connectionString) : IOctoberConnectedTargetIdentityVerifier, IOctoberTargetMigrator
{
    public async Task<OctoberConnectedTargetStatus> VerifyAsync(
        string targetId,
        string canonicalDatabaseIdentity,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return OctoberConnectedTargetStatus.Cancelled;

        try
        {
            NpgsqlConnectionStringBuilder descriptor = new(connectionString);
            string declaredIdentity = string.Create(CultureInfo.InvariantCulture,
                $"postgres://{descriptor.Host}:{descriptor.Port}/{descriptor.Database}");
            if (!string.Equals(declaredIdentity, canonicalDatabaseIdentity, StringComparison.Ordinal))
                return OctoberConnectedTargetStatus.Mismatched;

            await using NpgsqlConnection connection = new(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using NpgsqlCommand command = new("SELECT current_database();", connection);
            string? connectedDatabase = (string?)await command.ExecuteScalarAsync(cancellationToken);
            return string.Equals(connectedDatabase, descriptor.Database, StringComparison.Ordinal)
                ? OctoberConnectedTargetStatus.Matched
                : OctoberConnectedTargetStatus.Mismatched;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OctoberConnectedTargetStatus.Cancelled;
        }
        catch (Exception)
        {
            return OctoberConnectedTargetStatus.Unavailable;
        }
    }

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        database.Database.MigrateAsync(cancellationToken);
}
