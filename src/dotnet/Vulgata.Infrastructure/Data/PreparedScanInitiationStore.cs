using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class PreparedScanInitiationStore(VulgataDbContext context) :
    IPreparedScanInitiationStore
{
    private const int ProviderProcessTimeoutSeconds = 1_800;
    private const string ExecutionIdentityKind = "PreparedScanInitiationExecution";
    private const string SystemLockDomain =
        "vulgata/story-12.5/prepared-scan-initiation-system-lock/1";

    public async Task<PreparedScanInitiationResult> CreateOrReplayAsync(
        PreparedScanInitiationCommand command,
        CancellationToken cancellationToken)
    {
        NormalizedCommand normalized;
        try
        {
            normalized = Normalize(command);
        }
        catch (ArgumentException)
        {
            return new(PreparedScanInitiationOutcome.Rejected);
        }

        IDbContextTransaction? transaction = null;
        bool committed = false;
        try
        {
            transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            await context.Database.ExecuteSqlRawAsync(
                "SET LOCAL lock_timeout = '5s';",
                cancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({CreateSystemLockKey(normalized.SystemId)});",
                cancellationToken);

            if (!await OwnsCanonicalPairAsync(normalized, cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return new(PreparedScanInitiationOutcome.NotFound);
            }

            ExistingTupleState existing = await ReadExistingStateAsync(
                normalized,
                cancellationToken);
            if (existing.Outcome is not null)
            {
                if (existing.Outcome == PreparedScanInitiationOutcome.Replay)
                {
                    await transaction.CommitAsync(cancellationToken);
                    committed = true;
                    return new(existing.Outcome.Value, existing.Tuple);
                }

                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return new(existing.Outcome.Value);
            }

            PreparedScanInitiationTuple created = CreateTuple(normalized);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return new(PreparedScanInitiationOutcome.Created, created);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RollbackAndClearAsync(transaction, committed);
            return new(PreparedScanInitiationOutcome.Cancelled);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            await RollbackAndClearAsync(transaction, committed);
            return new(PreparedScanInitiationOutcome.Unavailable);
        }
        catch (Exception)
        {
            await RollbackAndClearAsync(transaction, committed);
            return new(PreparedScanInitiationOutcome.Unavailable);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<bool> OwnsCanonicalPairAsync(
        NormalizedCommand command,
        CancellationToken cancellationToken)
    {
        if (!await context.Systems.AsNoTracking().AnyAsync(
                system => system.Id == command.SystemId,
                cancellationToken))
        {
            return false;
        }

        Guid firstId = command.Repositories[0].Command.RepositoryId;
        Guid secondId = command.Repositories[1].Command.RepositoryId;
        int ownedCount = await context.Repositories.AsNoTracking().CountAsync(
            repository =>
                (repository.Id == firstId || repository.Id == secondId) &&
                repository.SystemId == command.SystemId,
            cancellationToken);
        return ownedCount == 2;
    }

    private async Task<ExistingTupleState> ReadExistingStateAsync(
        NormalizedCommand command,
        CancellationToken cancellationToken)
    {
        List<TwoRepositoryScanCoordination> coordinations = await context
            .TwoRepositoryScanCoordinations
            .AsNoTracking()
            .Where(coordination => coordination.SystemId == command.SystemId)
            .ToListAsync(cancellationToken);
        if (coordinations.Count > 1)
        {
            return ExistingTupleState.Rejected;
        }

        if (coordinations.Count == 0)
        {
            return await ClassifyAbsentCoordinationAsync(command, cancellationToken);
        }

        TwoRepositoryScanCoordination coordination = coordinations[0];
        if (!CoordinationHasExpectedIdentity(coordination, command))
        {
            return ExistingTupleState.Conflict;
        }

        List<ScanExecution> systemExecutions = await context.ScanExecutions
            .AsNoTracking()
            .Where(execution => execution.SystemId == command.SystemId)
            .ToListAsync(cancellationToken);
        if (systemExecutions.Count != 2)
        {
            return ExistingTupleState.Rejected;
        }

        Guid[] runIds = [coordination.ScanRunOneId, coordination.ScanRunTwoId];
        List<ScanRun> runs = await context.ScanRuns
            .AsNoTracking()
            .Where(run => runIds.Contains(run.Id))
            .ToListAsync(cancellationToken);
        if (runs.Count != 2)
        {
            return ExistingTupleState.Rejected;
        }

        ScanExecution? firstExecution = systemExecutions.SingleOrDefault(
            execution => execution.Id == command.Repositories[0].ExecutionId);
        ScanExecution? secondExecution = systemExecutions.SingleOrDefault(
            execution => execution.Id == command.Repositories[1].ExecutionId);
        if (firstExecution is null || secondExecution is null)
        {
            return ExistingTupleState.Conflict;
        }

        ScanRun? firstRun = runs.SingleOrDefault(run => run.Id == firstExecution.ScanRunId);
        ScanRun? secondRun = runs.SingleOrDefault(run => run.Id == secondExecution.ScanRunId);
        if (!Matches(
                firstRun,
                firstExecution,
                command.Repositories[0],
                command.SystemId,
                coordination.CreatedAt) ||
            !Matches(
                secondRun,
                secondExecution,
                command.Repositories[1],
                command.SystemId,
                coordination.CreatedAt) ||
            TwoRepositoryScanCoordination.Create(
                firstExecution,
                secondExecution,
                coordination.CreatedAt).Id != coordination.Id)
        {
            return ExistingTupleState.Rejected;
        }

        PreparedScanArtifactPresence artifactPresence =
            await ReadArtifactPresenceAsync(coordination, cancellationToken);
        return new(
            PreparedScanInitiationOutcome.Replay,
            MaterializeTuple(
                command,
                coordination,
                firstRun!,
                firstExecution,
                secondRun!,
                secondExecution,
                artifactPresence));
    }

    private async Task<PreparedScanArtifactPresence> ReadArtifactPresenceAsync(
        TwoRepositoryScanCoordination coordination,
        CancellationToken cancellationToken)
    {
        bool hasPreparedSnapshot = await context.ScanContextSnapshots
            .AsNoTracking()
            .AnyAsync(snapshot =>
                snapshot.PreparedScanContextIdentityHash != null &&
                ((snapshot.ScanRunId == coordination.ScanRunOneId &&
                    snapshot.RepositoryId == coordination.RepositoryOneId) ||
                 (snapshot.ScanRunId == coordination.ScanRunTwoId &&
                    snapshot.RepositoryId == coordination.RepositoryTwoId)),
                cancellationToken);
        if (hasPreparedSnapshot)
        {
            return PreparedScanArtifactPresence.Present;
        }

        bool hasManifest = await context.PreparedScanManifests
            .AsNoTracking()
            .AnyAsync(manifest =>
                manifest.CoordinationId == coordination.Id &&
                ((manifest.ScanRunId == coordination.ScanRunOneId &&
                    manifest.RepositoryId == coordination.RepositoryOneId) ||
                 (manifest.ScanRunId == coordination.ScanRunTwoId &&
                    manifest.RepositoryId == coordination.RepositoryTwoId)),
                cancellationToken);
        if (hasManifest)
        {
            return PreparedScanArtifactPresence.Present;
        }

        bool hasPreparedResult = await (
            from result in context.PreScanResults.AsNoTracking()
            join manifest in context.PreparedScanManifests.AsNoTracking()
                on result.PreparedScanManifestId equals (Guid?)manifest.Id
            where manifest.CoordinationId == coordination.Id &&
                ((result.ScanRunId == coordination.ScanRunOneId &&
                    result.RepositoryId == coordination.RepositoryOneId) ||
                 (result.ScanRunId == coordination.ScanRunTwoId &&
                    result.RepositoryId == coordination.RepositoryTwoId))
            select result.Id).AnyAsync(cancellationToken);
        if (hasPreparedResult)
        {
            return PreparedScanArtifactPresence.Present;
        }

        bool hasPreparedCodeUnit = await context.CodeUnits
            .AsNoTracking()
            .AnyAsync(codeUnit =>
                codeUnit.FileCodeUnitIdentityFingerprint != null &&
                ((codeUnit.ScanRunId == coordination.ScanRunOneId &&
                    codeUnit.RepositoryId == coordination.RepositoryOneId) ||
                 (codeUnit.ScanRunId == coordination.ScanRunTwoId &&
                    codeUnit.RepositoryId == coordination.RepositoryTwoId)),
                cancellationToken);
        return hasPreparedCodeUnit
            ? PreparedScanArtifactPresence.Present
            : PreparedScanArtifactPresence.None;
    }

    private async Task<ExistingTupleState> ClassifyAbsentCoordinationAsync(
        NormalizedCommand command,
        CancellationToken cancellationToken)
    {
        if (await context.ScanExecutions.AsNoTracking().AnyAsync(
                execution => execution.SystemId == command.SystemId,
                cancellationToken))
        {
            return ExistingTupleState.Rejected;
        }

        Guid firstId = command.Repositories[0].Command.RepositoryId;
        Guid secondId = command.Repositories[1].Command.RepositoryId;
        List<ScanRun> runs = await context.ScanRuns
            .AsNoTracking()
            .Where(run => run.RepositoryId == firstId || run.RepositoryId == secondId)
            .ToListAsync(cancellationToken);
        bool hasPartialInitiationRun = runs.Any(run =>
            run.ProviderProcessTimeoutSeconds == ProviderProcessTimeoutSeconds &&
            run.StartedAt == command.InitiatedAt &&
            command.Repositories.Any(repository =>
                repository.Command.RepositoryId == run.RepositoryId &&
                StringComparer.Ordinal.Equals(
                    repository.Command.ProfiledRevision,
                    run.ProfiledRevision)));
        return hasPartialInitiationRun
            ? ExistingTupleState.Rejected
            : ExistingTupleState.Absent;
    }

    private PreparedScanInitiationTuple CreateTuple(NormalizedCommand command)
    {
        ScanRun firstRun = ScanRun.Start(
            command.Repositories[0].Command.RepositoryId,
            ProviderProcessTimeoutSeconds,
            command.InitiatedAt);
        firstRun.BindProfiledRevision(command.Repositories[0].Command.ProfiledRevision);
        ScanRun secondRun = ScanRun.Start(
            command.Repositories[1].Command.RepositoryId,
            ProviderProcessTimeoutSeconds,
            command.InitiatedAt);
        secondRun.BindProfiledRevision(command.Repositories[1].Command.ProfiledRevision);

        ScanExecution firstExecution = CreateExecution(
            command,
            command.Repositories[0],
            firstRun.Id);
        ScanExecution secondExecution = CreateExecution(
            command,
            command.Repositories[1],
            secondRun.Id);
        TwoRepositoryScanCoordination coordination = TwoRepositoryScanCoordination.Create(
            firstExecution,
            secondExecution,
            command.InitiatedAt);

        context.ScanRuns.AddRange(firstRun, secondRun);
        context.ScanExecutions.AddRange(firstExecution, secondExecution);
        context.TwoRepositoryScanCoordinations.Add(coordination);
        return MaterializeTuple(
            command,
            coordination,
            firstRun,
            firstExecution,
            secondRun,
            secondExecution,
            PreparedScanArtifactPresence.None);
    }

    private static ScanExecution CreateExecution(
        NormalizedCommand command,
        NormalizedRepository repository,
        Guid scanRunId) =>
        ScanExecution.CreateNewPath(
            repository.ExecutionId,
            repository.Command.RepositoryId,
            command.SystemId,
            scanRunId,
            repository.Command.ProfiledRevision,
            command.PromptIdentity,
            command.DocumentSchemaIdentity,
            repository.Command.RuntimeIdentityJson,
            repository.Command.RuntimeIdentityHash,
            command.InitiatedAt);

    private static bool CoordinationHasExpectedIdentity(
        TwoRepositoryScanCoordination coordination,
        NormalizedCommand command) =>
        coordination.RepositoryOneId == command.Repositories[0].Command.RepositoryId &&
        coordination.RepositoryTwoId == command.Repositories[1].Command.RepositoryId &&
        coordination.ScanExecutionOneId == command.Repositories[0].ExecutionId &&
        coordination.ScanExecutionTwoId == command.Repositories[1].ExecutionId;

    private static bool Matches(
        ScanRun? run,
        ScanExecution execution,
        NormalizedRepository expected,
        Guid systemId,
        DateTimeOffset initiatedAt) =>
        run is not null &&
        run.Id == execution.ScanRunId &&
        run.RepositoryId == expected.Command.RepositoryId &&
        run.ProviderProcessTimeoutSeconds == ProviderProcessTimeoutSeconds &&
        run.StartedAt == initiatedAt &&
        StringComparer.Ordinal.Equals(run.ProfiledRevision, expected.Command.ProfiledRevision) &&
        execution.Id == expected.ExecutionId &&
        execution.RepositoryId == expected.Command.RepositoryId &&
        execution.SystemId == systemId &&
        execution.PreScanResultId == Guid.Empty &&
        execution.StartedAt == initiatedAt &&
        StringComparer.Ordinal.Equals(execution.ProfiledRevision, expected.Command.ProfiledRevision) &&
        StringComparer.Ordinal.Equals(execution.PromptIdentity, expected.PromptIdentity) &&
        StringComparer.Ordinal.Equals(execution.DocumentSchemaIdentity, expected.DocumentSchemaIdentity) &&
        JsonEquals(
            execution.AnalysisRuntimeIdentityJson,
            expected.Command.RuntimeIdentityJson) &&
        StringComparer.Ordinal.Equals(
            execution.AnalysisRuntimeIdentityHash,
            expected.Command.RuntimeIdentityHash);

    private static bool JsonEquals(string? stored, string expected)
    {
        if (stored is null)
        {
            return false;
        }

        try
        {
            using JsonDocument storedDocument = JsonDocument.Parse(stored);
            using JsonDocument expectedDocument = JsonDocument.Parse(expected);
            return JsonElement.DeepEquals(
                storedDocument.RootElement,
                expectedDocument.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static PreparedScanInitiationTuple MaterializeTuple(
        NormalizedCommand command,
        TwoRepositoryScanCoordination coordination,
        ScanRun firstRun,
        ScanExecution firstExecution,
        ScanRun secondRun,
        ScanExecution secondExecution,
        PreparedScanArtifactPresence artifactPresence) =>
        new(
            command.SystemId,
            command.EffectiveAuthorizationScopeFingerprint,
            coordination.CreatedAt,
            coordination.Id,
            coordination.StateVersion,
            coordination.Phase,
            artifactPresence,
            [
                MaterializeRepository(command.Repositories[0], firstRun, firstExecution),
                MaterializeRepository(command.Repositories[1], secondRun, secondExecution),
            ]);

    private static PreparedScanInitiationRepositoryTuple MaterializeRepository(
        NormalizedRepository expected,
        ScanRun run,
        ScanExecution execution) =>
        new(
            expected.Command.RepositoryId,
            expected.Command.RequestedRef,
            expected.Command.ProfiledRevision,
            run.Id,
            execution.Id,
            execution.Phase,
            expected.PromptIdentity,
            expected.DocumentSchemaIdentity,
            expected.Command.RuntimeIdentityJson,
            expected.Command.RuntimeIdentityHash,
            execution.PreScanResultId);

    private static NormalizedCommand Normalize(PreparedScanInitiationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.SystemId == Guid.Empty ||
            command.AuthorizationUserId == Guid.Empty ||
            command.Repositories.Count != 2 ||
            command.ProviderProcessTimeoutSeconds != ProviderProcessTimeoutSeconds ||
            command.InitiatedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A complete canonical initiation command is required.");
        }

        PreparedScanInitiationRepositoryCommand[] repositories = command.Repositories
            .OrderBy(repository => repository.RepositoryId)
            .ToArray();
        if (repositories[0].RepositoryId == Guid.Empty ||
            repositories[0].RepositoryId == repositories[1].RepositoryId)
        {
            throw new ArgumentException("Two distinct Repository identities are required.");
        }

        NormalizedRepository[] normalized = repositories
            .Select(repository => NormalizeRepository(command, repository))
            .ToArray();
        string scopeFingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('\n', normalized.Select(repository => repository.IdentityFingerprint)))));
        return new(
            command.SystemId,
            command.PromptIdentity,
            command.DocumentSchemaIdentity,
            command.InitiatedAt,
            scopeFingerprint,
            normalized);
    }

    private static NormalizedRepository NormalizeRepository(
        PreparedScanInitiationCommand command,
        PreparedScanInitiationRepositoryCommand repository)
    {
        PreparedCanonicalArtifact identity = PreparedScanInitiationIdentityBuilder.CreateExecution(
            command.AuthorizationUserId,
            command.EffectiveAuthorizationMode,
            command.SystemId,
            repository.RepositoryId,
            repository.RequestedRef,
            repository.ProfiledRevision,
            command.PromptIdentity,
            command.DocumentSchemaIdentity,
            repository.RuntimeIdentityJson,
            repository.RuntimeIdentityHash);
        return new(
            repository,
            command.PromptIdentity,
            command.DocumentSchemaIdentity,
            identity.Fingerprint,
            PreparedScanIdentityBuilders.CreateDeterministicIdentity(
                ExecutionIdentityKind,
                identity.Fingerprint));
    }

    private static long CreateSystemLockKey(Guid systemId)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{SystemLockDomain}\n{systemId:D}"));
        return BinaryPrimitives.ReadInt64BigEndian(digest);
    }

    private async Task RollbackAndClearAsync(
        IDbContextTransaction? transaction,
        bool committed)
    {
        if (!committed && transaction is not null)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception)
            {
                // A commit acknowledgement can fail after PostgreSQL has committed.
            }
        }

        context.ChangeTracker.Clear();
    }

    private sealed record NormalizedCommand(
        Guid SystemId,
        string PromptIdentity,
        string DocumentSchemaIdentity,
        DateTimeOffset InitiatedAt,
        string EffectiveAuthorizationScopeFingerprint,
        IReadOnlyList<NormalizedRepository> Repositories);

    private sealed record NormalizedRepository(
        PreparedScanInitiationRepositoryCommand Command,
        string PromptIdentity,
        string DocumentSchemaIdentity,
        string IdentityFingerprint,
        Guid ExecutionId);

    private sealed record ExistingTupleState(
        PreparedScanInitiationOutcome? Outcome,
        PreparedScanInitiationTuple? Tuple = null)
    {
        internal static ExistingTupleState Absent { get; } =
            new((PreparedScanInitiationOutcome?)null);
        internal static ExistingTupleState Conflict { get; } =
            new(PreparedScanInitiationOutcome.Conflict);
        internal static ExistingTupleState Rejected { get; } =
            new(PreparedScanInitiationOutcome.Rejected);
    }
}
