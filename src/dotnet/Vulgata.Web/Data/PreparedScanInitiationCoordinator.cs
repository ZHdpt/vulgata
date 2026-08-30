using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared.Scans;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Web.Data;

internal sealed class PreparedScanInitiationCoordinator(
    ISystemRepository systemRepository,
    IRepositoryRepository repositoryRepository,
    IPreparedPass1ContractIdentitySource contractIdentitySource,
    IPreparedScanRuntimeIdentityResolver runtimeIdentityResolver,
    IPreparedScanInitiationAuthorizationRechecker authorizationRechecker,
    IPreparedScanInitiationStore initiationStore,
    PreparedScanCoordinator preparedScanCoordinator)
{
    private const int _providerProcessTimeoutSeconds = 1_800;

    public async Task<PreparedScanPreparationResult> InitiateAsync(
        Guid systemId,
        PreparedScanRequest request,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(user);
        if (!TryNormalizeRequest(request, out PreparedScanRepositoryRevisionBinding[] bindings) ||
            systemId == Guid.Empty ||
            string.IsNullOrWhiteSpace(user.UserId))
        {
            return NotFound();
        }

        Vulgata.Core.Entities.System? system = await systemRepository.GetVisibleByIdAsync(
            systemId,
            user.ToSystemOwnerFilter(),
            cancellationToken);
        if (system?.Id != systemId ||
            await ReadRepositoriesAsync(systemId, bindings, cancellationToken) is null)
        {
            return NotFound();
        }

        PreparedPass1ContractIdentity? contractIdentity;
        try
        {
            contractIdentity = contractIdentitySource.GetCurrent();
        }
        catch (Exception)
        {
            return Unavailable("prepared_scan_identity_unavailable");
        }

        if (contractIdentity is null || !HasExactPreparedPass1Identity(contractIdentity))
        {
            return Unavailable("prepared_scan_identity_unavailable");
        }

        var initiationRepositories = new List<PreparedScanInitiationRepositoryCommand>(2);
        foreach (PreparedScanRepositoryRevisionBinding binding in bindings)
        {
            PreparedScanRuntimeIdentityResolution resolution =
                await runtimeIdentityResolver.ResolveAsync(
                    binding.RepositoryId,
                    binding.ProfiledRevision,
                    cancellationToken);
            if (resolution.Identity is not AnalysisRuntimeIdentity runtimeIdentity ||
                runtimeIdentity.RepositoryId != binding.RepositoryId ||
                !string.Equals(
                    runtimeIdentity.ProfiledRevision,
                    binding.ProfiledRevision,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    runtimeIdentity.Sha256,
                    AnalysisRuntimeIdentity.ComputeSha256(runtimeIdentity.CanonicalJson),
                    StringComparison.Ordinal))
            {
                return Unavailable(resolution.FailureCode);
            }

            initiationRepositories.Add(new(
                binding.RepositoryId,
                binding.GitRef,
                binding.ProfiledRevision,
                runtimeIdentity.CanonicalJson,
                runtimeIdentity.Sha256));
        }

        PreparedScanInitiationAuthorizationResult freshAuthorization =
            await authorizationRechecker.RecheckAsync(
                user.UserId,
                systemId,
                bindings.Select(binding => binding.RepositoryId).ToArray(),
                cancellationToken);
        if (!freshAuthorization.IsAuthorized ||
            freshAuthorization.UserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(freshAuthorization.EffectiveAuthorizationMode) ||
            !freshAuthorization.RepositoryIds.SequenceEqual(
                bindings.Select(binding => binding.RepositoryId)))
        {
            return NotFound();
        }

        PreparedScanInitiationCommand command = new(
            freshAuthorization.UserId,
            freshAuthorization.EffectiveAuthorizationMode,
            systemId,
            initiationRepositories,
            contractIdentity.PromptIdentity,
            contractIdentity.DocumentSchemaIdentity,
            _providerProcessTimeoutSeconds,
            TimeProvider.System.GetUtcNow());
        PreparedScanInitiationResult initiation =
            await initiationStore.CreateOrReplayAsync(command, cancellationToken);
        if (initiation.Outcome is not (
                PreparedScanInitiationOutcome.Created or
                PreparedScanInitiationOutcome.Replay) ||
            initiation.Tuple is not PreparedScanInitiationTuple tuple)
        {
            return MapInitiationFailure(initiation.Outcome);
        }

        PreparedScanAuthorizationScope authorizationScope = new(
            freshAuthorization.UserId,
            systemId,
            freshAuthorization.RepositoryIds);
        if (tuple.CoordinationPhase != TwoRepositoryScanCoordinationPhase.Preparing ||
            tuple.PreparedArtifactPresence != PreparedScanArtifactPresence.None)
        {
            return await preparedScanCoordinator.RecoverPreparedAsync(
                authorizationScope,
                tuple,
                cancellationToken);
        }

        return await preparedScanCoordinator.PrepareInitiatedAsync(
            systemId,
            request,
            user,
            tuple,
            cancellationToken);
    }

    private async Task<RepositoryEntity[]?> ReadRepositoriesAsync(
        Guid systemId,
        IReadOnlyList<PreparedScanRepositoryRevisionBinding> bindings,
        CancellationToken cancellationToken)
    {
        var repositories = new List<RepositoryEntity>(2);
        foreach (PreparedScanRepositoryRevisionBinding binding in bindings)
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

    private static bool TryNormalizeRequest(
        PreparedScanRequest request,
        out PreparedScanRepositoryRevisionBinding[] bindings)
    {
        bindings = request.Repositories?
            .OrderBy(binding => binding.RepositoryId)
            .ToArray() ?? [];
        return bindings.Length == 2 &&
            bindings.All(binding =>
                binding.RepositoryId != Guid.Empty &&
                IsSafeFullRef(binding.GitRef) &&
                IsExactRevision(binding.ProfiledRevision)) &&
            bindings.Select(binding => binding.RepositoryId).Distinct().Count() == 2;
    }

    private static bool IsExactRevision(string? value) =>
        value is { Length: 40 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool HasExactPreparedPass1Identity(
        PreparedPass1ContractIdentity identity)
    {
        const string promptIdentityPrefix =
            "pass1-code-logic-worker.v2/sha256/";
        return string.Equals(
                identity.DocumentSchemaIdentity,
                "code-logic/file-v2",
                StringComparison.Ordinal) &&
            identity.PromptIdentity is { } promptIdentity &&
            promptIdentity.Length == promptIdentityPrefix.Length + 64 &&
            promptIdentity.StartsWith(promptIdentityPrefix, StringComparison.Ordinal) &&
            promptIdentity[promptIdentityPrefix.Length..].All(
                character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static bool IsSafeFullRef(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 256 ||
            (!value.StartsWith("refs/heads/", StringComparison.Ordinal) &&
             !value.StartsWith("refs/tags/", StringComparison.Ordinal)) ||
            value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains("@{", StringComparison.Ordinal) ||
            value.Contains("//", StringComparison.Ordinal) ||
            value.EndsWith("/", StringComparison.Ordinal) ||
            value.EndsWith(".", StringComparison.Ordinal) ||
            value.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) ||
            value.IndexOfAny(['~', '^', ':', '?', '*', '[', '\\', '+']) >= 0)
        {
            return false;
        }

        return true;
    }

    private static PreparedScanPreparationResult MapInitiationFailure(
        PreparedScanInitiationOutcome outcome) => outcome switch
        {
            PreparedScanInitiationOutcome.NotFound => NotFound(),
            PreparedScanInitiationOutcome.Conflict =>
                Conflict("prepared_scan_initiation_conflict"),
            PreparedScanInitiationOutcome.Rejected =>
                new(PreparedScanPreparationOutcome.Rejected,
                    FailureCode: "prepared_scan_initiation_rejected"),
            PreparedScanInitiationOutcome.Cancelled =>
                Unavailable("prepared_scan_initiation_cancelled"),
            _ => Unavailable("prepared_scan_initiation_unavailable"),
        };

    private static PreparedScanPreparationResult NotFound() =>
        new(PreparedScanPreparationOutcome.NotFound,
            FailureCode: "prepared_scan_not_found");

    private static PreparedScanPreparationResult Conflict(string failureCode) =>
        new(PreparedScanPreparationOutcome.Conflict, FailureCode: failureCode);

    private static PreparedScanPreparationResult Unavailable(string? failureCode) =>
        new(PreparedScanPreparationOutcome.Unavailable,
            FailureCode: string.IsNullOrWhiteSpace(failureCode)
                ? "prepared_scan_initiation_unavailable"
                : failureCode);
}
