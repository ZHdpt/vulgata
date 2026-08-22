using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Server-owned identity bound to the sole prepared Pass 1 Worker function.
/// These values are deliberately absent from the model-visible arguments.
/// </summary>
public sealed record PreparedNavigateCodeBinding(
    Guid JobId,
    ulong ToolAttemptOrdinal,
    Guid SourceFileCodeUnitId,
    Guid PairExecutionId = default,
    long JobStateVersion = 0,
    long PairStateVersion = 0,
    Guid LeaseToken = default,
    DateTimeOffset LeaseExpiresAt = default,
    string PairState = "",
    Pass1CircuitState CircuitState = Pass1CircuitState.Closed,
    ScanJobExecutionState JobState = ScanJobExecutionState.Pending,
    string AuthorizationBinding = "");

/// <summary>
/// Closed model-visible target for <c>navigate_code</c>. Paths are always
/// Repository-relative and hashes are lowercase SHA-256 values.
/// </summary>
public sealed record PreparedNavigateCodeTarget(
    [property: JsonPropertyName("repositoryRelativePath")] string? CanonicalPath,
    string? Symbol,
    PreparedPass1Position? Position,
    PreparedPass1LineRange? ExactLineRange,
    string? ExpectedSourceHash);

/// <summary>
/// Creates the one function available to a prepared Pass 1 Worker. The
/// function delegates only to the authorized, revision-bound navigation port.
/// </summary>
public sealed class PreparedNavigateCodeFunction(IPass1NavigationService navigationService)
{
    private readonly IPass1NavigationService _navigationService =
        navigationService ?? throw new ArgumentNullException(nameof(navigationService));

    public AIFunction Create(PreparedNavigateCodeBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.JobId == Guid.Empty ||
            binding.SourceFileCodeUnitId == Guid.Empty ||
            binding.ToolAttemptOrdinal < 1)
        {
            throw new ArgumentException(
                "A prepared navigation function requires an exact server binding.",
                nameof(binding));
        }

        async Task<PreparedPass1NavigationResponse> NavigateAsync(
            PreparedPass1NavigationOperation operation,
            PreparedNavigateCodeTarget target,
            ulong depth,
            string? parentObservationIdentity,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (!TryDecodeSha256(target.ExpectedSourceHash, out ReadOnlyMemory<byte>? sourceHash) ||
                !TryDecodeSha256(parentObservationIdentity, out ReadOnlyMemory<byte>? parentIdentity))
            {
                return Unavailable(operation, depth);
            }

            var request = new PreparedPass1NavigationRequestInput(
                binding.JobId,
                binding.ToolAttemptOrdinal,
                operation,
                binding.SourceFileCodeUnitId,
                new PreparedPass1NavigationTarget(
                    target.CanonicalPath,
                    target.Symbol,
                    target.Position,
                    target.ExactLineRange,
                    sourceHash),
                depth,
                parentIdentity,
                binding.PairExecutionId,
                binding.JobStateVersion,
                binding.PairStateVersion,
                binding.LeaseToken,
                binding.LeaseExpiresAt,
                binding.PairState,
                binding.CircuitState,
                binding.JobState,
                binding.AuthorizationBinding);
            return await _navigationService.NavigateAsync(request, cancellationToken);
        }

        Func<
            PreparedPass1NavigationOperation,
            PreparedNavigateCodeTarget,
            ulong,
            string?,
            CancellationToken,
            Task<PreparedPass1NavigationResponse>> function = NavigateAsync;
        return AIFunctionFactory.Create(
            function,
            new AIFunctionFactoryOptions
            {
                Name = "navigate_code",
                Description =
                    "Navigate authorized evidence in the current prepared Repository revision.",
                SerializerOptions = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions)
                {
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                },
            });
    }

    private static bool TryDecodeSha256(
        string? value,
        out ReadOnlyMemory<byte>? decoded)
    {
        if (value is null)
        {
            decoded = null;
            return true;
        }

        if (value.Length != 64 ||
            value.Any(character => character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f')))
        {
            decoded = null;
            return false;
        }

        decoded = Convert.FromHexString(value);
        return true;
    }

    private static PreparedPass1NavigationResponse Unavailable(
        PreparedPass1NavigationOperation operation,
        ulong requestedDepth) =>
        new(
            PreparedPass1NavigationOutcome.Unavailable,
            Guid.Empty,
            operation,
            requestedDepth,
            null,
            0,
            0,
            []);
}
