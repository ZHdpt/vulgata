using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared.Scans;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Web.Data;

public enum PreparedScanPreparationOutcome
{
    Created,
    Replay,
    NotFound,
    Conflict,
    Rejected,
    Unavailable,
}

public sealed record PreparedScanPreparationResult(
    PreparedScanPreparationOutcome Outcome,
    PreparedScanResponse? Response = null,
    string? FailureCode = null);

public sealed record PreparedScanPairBuildResult(
    PreparedScanPairPackage? PairPackage,
    long ExpectedCoordinationVersion,
    PreparedScanFailure? Failure);

/// <summary>
/// Closed composition seam for the already-approved Story 12.2 preparation pipeline.
/// It builds prepared state only and grants no execution authority.
/// </summary>
public interface IPreparedScanPairBuilder
{
    Task<PreparedScanPairBuildResult> BuildAsync(
        PreparedScanInitiationTuple tuple,
        IReadOnlyList<RepositoryEntity> repositories,
        IReadOnlyList<PreparedScanRepositoryRevisionBinding> revisionBindings,
        CancellationToken cancellationToken);
}

public sealed class PreparedScanCoordinator(
    ISystemRepository systemRepository,
    IRepositoryRepository repositoryRepository,
    IPreparedScanPairBuilder pairBuilder,
    IPreparedScanStore store,
    IPreparedScanHandoffSource handoffSource)
{
    public async Task<PreparedScanPreparationResult> PrepareInitiatedAsync(
        Guid systemId,
        PreparedScanRequest request,
        IUserContext user,
        PreparedScanInitiationTuple tuple,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(tuple);
        if (systemId == Guid.Empty ||
            request.Repositories is not { Count: 2 } bindings ||
            bindings.Any(binding => binding.RepositoryId == Guid.Empty) ||
            bindings.Select(binding => binding.RepositoryId).Distinct().Count() != 2 ||
            !Guid.TryParse(user.UserId, out Guid userId) ||
            userId == Guid.Empty ||
            !HasExactTupleBindings(tuple, systemId, bindings))
        {
            return NotFound();
        }

        PreparedScanAuthorizationScope authorizationScope = new(
            userId,
            systemId,
            bindings
                .Select(binding => binding.RepositoryId)
                .OrderBy(id => id.ToString("D"), StringComparer.Ordinal)
                .ToArray());
        if (tuple.CoordinationPhase != TwoRepositoryScanCoordinationPhase.Preparing ||
            tuple.PreparedArtifactPresence != PreparedScanArtifactPresence.None)
        {
            return await RecoverPreparedAsync(authorizationScope, tuple, cancellationToken);
        }

        RepositoryEntity[]? repositories = await ReadAuthorizedRepositoriesAsync(
            systemId,
            bindings,
            user,
            cancellationToken);
        if (repositories is null)
        {
            return NotFound();
        }

        PreparedScanPairBuildResult build;
        try
        {
            build = await pairBuilder.BuildAsync(
                tuple,
                repositories,
                bindings,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Unavailable("prepared_scan_pipeline_failed");
        }

        if (build.PairPackage is not PreparedScanPairPackage pairPackage)
        {
            return MapFailure(build.Failure);
        }

        if (!HasExactPackageBindings(pairPackage, bindings))
        {
            return Rejected("prepared_scan_package_invalid");
        }

        // Visibility is deliberately rechecked after all analysis and immediately
        // before the only durable prepared-state write.
        repositories = await ReadAuthorizedRepositoriesAsync(
            systemId,
            bindings,
            user,
            cancellationToken);
        if (repositories is null)
        {
            return NotFound();
        }

        PreparedScanCommitResult commit = await store.CommitPairAsync(
            authorizationScope,
            pairPackage,
            build.ExpectedCoordinationVersion,
            cancellationToken);

        if (commit.Outcome is not (PreparedScanCommitOutcome.Committed or PreparedScanCommitOutcome.Replay))
        {
            return commit.Outcome switch
            {
                PreparedScanCommitOutcome.Conflict => Conflict(commit.Failure?.FailureCode),
                PreparedScanCommitOutcome.Rejected => Rejected(commit.Failure?.FailureCode),
                PreparedScanCommitOutcome.Cancelled => Unavailable("prepared_scan_cancelled"),
                _ => Unavailable(commit.Failure?.FailureCode),
            };
        }

        PreparedScanHandoff? handoff = await handoffSource.GetAsync(
            authorizationScope,
            pairPackage.CoordinationId,
            cancellationToken);
        PreparedScanResponse? response = handoff is null ? null : MapResponse(handoff);
        if (response is null ||
            !StringComparer.Ordinal.Equals(response.PairPackageFingerprint, commit.PairPackageFingerprint))
        {
            return Unavailable("prepared_scan_handoff_unavailable");
        }

        return new PreparedScanPreparationResult(
            commit.Outcome == PreparedScanCommitOutcome.Committed
                ? PreparedScanPreparationOutcome.Created
                : PreparedScanPreparationOutcome.Replay,
            response);
    }

    public async Task<PreparedScanPreparationResult> RecoverPreparedAsync(
        PreparedScanAuthorizationScope authorizationScope,
        PreparedScanInitiationTuple tuple,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorizationScope);
        ArgumentNullException.ThrowIfNull(tuple);
        Guid[] repositoryIds = authorizationScope.RepositoryIds
            .Distinct()
            .OrderBy(id => id.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (authorizationScope.UserId == Guid.Empty ||
            authorizationScope.SystemId != tuple.SystemId ||
            tuple.CoordinationId == Guid.Empty ||
            tuple.Repositories is not { Count: 2 } repositories ||
            repositoryIds.Length != 2 ||
            !repositoryIds.SequenceEqual(
                repositories.Select(repository => repository.RepositoryId)
                    .OrderBy(id => id.ToString("D"), StringComparer.Ordinal)))
        {
            return Conflict("prepared_scan_initiation_tuple_conflict");
        }

        PreparedScanHandoff? handoff = await handoffSource.GetAsync(
            authorizationScope,
            tuple.CoordinationId,
            cancellationToken);
        PreparedScanResponse? response = handoff is null ? null : MapResponse(handoff);
        if (response is null || !HandoffMatchesTuple(response, tuple))
        {
            return Conflict("prepared_scan_handoff_conflict");
        }

        return new PreparedScanPreparationResult(PreparedScanPreparationOutcome.Replay, response);
    }

    private async Task<RepositoryEntity[]?> ReadAuthorizedRepositoriesAsync(
        Guid systemId,
        IReadOnlyList<PreparedScanRepositoryRevisionBinding> bindings,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        SystemEntity? system = await systemRepository.GetVisibleByIdAsync(
            systemId,
            user.ToSystemOwnerFilter(),
            cancellationToken);
        if (system?.Id != systemId)
        {
            return null;
        }

        var repositories = new List<RepositoryEntity>(2);
        foreach (PreparedScanRepositoryRevisionBinding binding in bindings
                     .OrderBy(value => value.RepositoryId.ToString("D"), StringComparer.Ordinal))
        {
            RepositoryEntity? repository = await repositoryRepository.GetBySystemAndIdAsync(
                systemId,
                binding.RepositoryId,
                cancellationToken);
            if (repository?.Id != binding.RepositoryId || repository.SystemId != systemId)
            {
                return null;
            }

            repositories.Add(repository);
        }

        return [.. repositories];
    }

    private static bool HasExactPackageBindings(
        PreparedScanPairPackage pairPackage,
        IReadOnlyList<PreparedScanRepositoryRevisionBinding> bindings)
    {
        if (pairPackage.CoordinationId == Guid.Empty ||
            pairPackage.Repositories is not { Count: 2 } packages ||
            packages.Select(package => package.RepositoryId).Distinct().Count() != 2 ||
            pairPackage.SelectedCount is < 50 or > 150)
        {
            return false;
        }

        foreach (PreparedScanRepositoryRevisionBinding binding in bindings)
        {
            PreparedRepositoryPackage? package = packages.SingleOrDefault(
                candidate => candidate.RepositoryId == binding.RepositoryId);
            if (package is null ||
                package.ScanRunId == Guid.Empty ||
                !StringComparer.Ordinal.Equals(package.RequestedRef, binding.GitRef) ||
                !StringComparer.Ordinal.Equals(package.ProfiledRevision, binding.ProfiledRevision))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasExactTupleBindings(
        PreparedScanInitiationTuple tuple,
        Guid systemId,
        IReadOnlyList<PreparedScanRepositoryRevisionBinding> bindings)
    {
        if (tuple.SystemId != systemId ||
            tuple.CoordinationId == Guid.Empty ||
            tuple.InitiatedAt.Offset != TimeSpan.Zero ||
            string.IsNullOrWhiteSpace(tuple.EffectiveAuthorizationScopeFingerprint) ||
            !Enum.IsDefined(tuple.CoordinationPhase) ||
            !Enum.IsDefined(tuple.PreparedArtifactPresence) ||
            tuple.Repositories is not { Count: 2 } repositories ||
            repositories.Select(repository => repository.RepositoryId).Distinct().Count() != 2 ||
            repositories.Any(repository => !Enum.IsDefined(repository.ScanExecutionPhase)))
        {
            return false;
        }

        foreach (PreparedScanRepositoryRevisionBinding binding in bindings)
        {
            PreparedScanInitiationRepositoryTuple? repository = repositories.SingleOrDefault(
                candidate => candidate.RepositoryId == binding.RepositoryId);
            if (repository is null ||
                repository.ScanRunId == Guid.Empty ||
                repository.ScanExecutionId == Guid.Empty ||
                !StringComparer.Ordinal.Equals(repository.RequestedRef, binding.GitRef) ||
                !StringComparer.Ordinal.Equals(repository.ProfiledRevision, binding.ProfiledRevision))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HandoffMatchesTuple(
        PreparedScanResponse response,
        PreparedScanInitiationTuple tuple)
    {
        if (response.CoordinationId != tuple.CoordinationId ||
            response.Manifests is not { Count: 2 } manifests)
        {
            return false;
        }

        foreach (PreparedScanInitiationRepositoryTuple repository in tuple.Repositories)
        {
            PreparedScanManifestResponse? manifest = manifests.SingleOrDefault(
                candidate => candidate.RepositoryId == repository.RepositoryId);
            if (manifest is null ||
                manifest.ScanRunId != repository.ScanRunId ||
                !StringComparer.Ordinal.Equals(manifest.ProfiledRevision, repository.ProfiledRevision))
            {
                return false;
            }
        }

        return true;
    }

    private static PreparedScanResponse? MapResponse(PreparedScanHandoff handoff)
    {
        PreparedScanManifestBinding[] bindings =
        [
            .. handoff.Manifests.OrderBy(
                binding => binding.RepositoryId.ToString("D"),
                StringComparer.Ordinal),
        ];
        if (bindings.Length != 2 ||
            bindings.Select(binding => binding.RepositoryId).Distinct().Count() != 2 ||
            handoff.FileCodeUnits.Count != handoff.SelectedCount)
        {
            return null;
        }

        var manifests = new List<PreparedScanManifestResponse>(2);
        foreach (PreparedScanManifestBinding binding in bindings)
        {
            PreparedFileCodeUnitDraft[] entries =
            [
                .. handoff.FileCodeUnits
                    .Where(unit => unit.RepositoryId == binding.RepositoryId)
                    .OrderBy(unit => unit.ManifestOrdinal),
            ];
            if (!entries.Select(entry => entry.ManifestOrdinal)
                    .SequenceEqual(Enumerable.Range(1, entries.Length)))
            {
                return null;
            }

            manifests.Add(new PreparedScanManifestResponse(
                binding.RepositoryId,
                binding.ScanRunId,
                binding.ProfiledRevision,
                binding.ManifestId,
                binding.ManifestFingerprint,
                binding.CodeGraphProofIdentityHash,
                binding.JavaReadinessIdentityHash,
                binding.PreparedScanContextIdentityHash,
                [
                    .. entries.Select(entry => new PreparedScanEntryResponse(
                        entry.Id,
                        entry.IdentityFingerprint,
                        entry.ManifestOrdinal,
                        entry.PairSelectionOrdinal)),
                ]));
        }

        return new PreparedScanResponse(
            handoff.CoordinationId,
            handoff.PairSelectionFingerprint,
            handoff.PairPackageFingerprint,
            manifests,
            handoff.SelectedCount);
    }

    private static PreparedScanPreparationResult MapFailure(PreparedScanFailure? failure) =>
        failure?.Reason switch
        {
            PreparedScanFailureReason.NotFound => NotFound(failure.FailureCode),
            PreparedScanFailureReason.SourceIdentityMismatch or
                PreparedScanFailureReason.StaleCoordination or
                PreparedScanFailureReason.CanonicalConflict => Conflict(failure.FailureCode),
            PreparedScanFailureReason.AnalysisNotReady when
                failure.FailureCode.Contains("unavailable", StringComparison.OrdinalIgnoreCase) =>
                Unavailable(failure.FailureCode),
            PreparedScanFailureReason.PersistenceFailure or PreparedScanFailureReason.Cancelled =>
                Unavailable(failure.FailureCode),
            _ => Rejected(failure?.FailureCode),
        };

    private static PreparedScanPreparationResult NotFound(string? code = null) =>
        new(PreparedScanPreparationOutcome.NotFound, FailureCode: code ?? "prepared_scan_not_found");

    private static PreparedScanPreparationResult Conflict(string? code) =>
        new(PreparedScanPreparationOutcome.Conflict, FailureCode: code ?? "prepared_scan_conflict");

    private static PreparedScanPreparationResult Rejected(string? code) =>
        new(PreparedScanPreparationOutcome.Rejected, FailureCode: code ?? "prepared_scan_rejected");

    private static PreparedScanPreparationResult Unavailable(string? code) =>
        new(PreparedScanPreparationOutcome.Unavailable, FailureCode: code ?? "prepared_scan_unavailable");
}
