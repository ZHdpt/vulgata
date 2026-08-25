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
    string AuthorizationBinding = "",
    PreparedPass1NavigationScope? ReservationScope = null,
    PreparedPass1NavigationReservationTicket? ReservationCursor = null);

internal sealed record PreparedNavigateCodeSession(
    AIFunction Function,
    Func<bool> ShouldTerminate);

internal sealed class PreparedPass1ReceiptProvenanceLedger
{
    private const int _maximumReceiptCount = 8;
    private readonly object _sync = new();
    private readonly HashSet<Guid> _receiptIds = [];

    internal bool TryRecord(Guid receiptId)
    {
        if (receiptId == Guid.Empty)
        {
            return true;
        }

        lock (_sync)
        {
            if (_receiptIds.Contains(receiptId))
            {
                return true;
            }

            if (_receiptIds.Count >= _maximumReceiptCount)
            {
                return false;
            }

            return _receiptIds.Add(receiptId);
        }
    }

    internal bool Contains(Guid receiptId)
    {
        lock (_sync)
        {
            return receiptId != Guid.Empty && _receiptIds.Contains(receiptId);
        }
    }
}

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
public sealed class PreparedNavigateCodeFunction(
    IPass1NavigationService navigationService,
    IPass1NavigationAttemptReservationStore? reservationStore = null)
{
    private readonly IPass1NavigationService _navigationService =
        navigationService ?? throw new ArgumentNullException(nameof(navigationService));

    private readonly IPass1NavigationAttemptReservationStore? _reservationStore = reservationStore;

    public AIFunction Create(PreparedNavigateCodeBinding binding) =>
        CreateSession(binding).Function;

    internal PreparedNavigateCodeSession CreateSession(
        PreparedNavigateCodeBinding binding,
        IPass1NavigationAttemptReservationStore? reservationStoreOverride = null,
        PreparedPass1ReceiptProvenanceLedger? receiptProvenance = null)
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

        PreparedPass1NavigationReservationTicket? cursor = binding.ReservationCursor;
        PreparedPass1NavigationScope? currentScope = binding.ReservationScope;
        IPass1NavigationAttemptReservationStore? effectiveReservationStore =
            reservationStoreOverride ?? _reservationStore;
        PreparedPass1ReceiptProvenanceLedger effectiveReceiptProvenance =
            receiptProvenance ?? new();
        bool shouldTerminate = false;

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

            ulong requestedOrdinal = binding.ToolAttemptOrdinal;
            if (effectiveReservationStore is not null && currentScope is not null)
            {
                requestedOrdinal = checked((ulong)((cursor?.ToolAttemptCount ?? currentScope.ToolAttemptCount) + 1));
            }

            var request = new PreparedPass1NavigationRequestInput(
                binding.JobId,
                requestedOrdinal,
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

            if (effectiveReservationStore is not null && currentScope is not null)
            {
                PreparedPass1NavigationReservationTicket? reserved;
                try
                {
                    reserved = await effectiveReservationStore.ReserveNextAsync(
                        new(currentScope, request, cursor),
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    shouldTerminate = true;
                    return Unavailable(operation, depth);
                }

                if (reserved is null ||
                    reserved.JobId != binding.JobId ||
                    reserved.PairExecutionId != binding.PairExecutionId ||
                    reserved.LeaseToken != binding.LeaseToken ||
                    reserved.ToolAttemptOrdinal != checked((int)requestedOrdinal) ||
                    reserved.ToolAttemptCount != reserved.ToolAttemptOrdinal)
                {
                    shouldTerminate = true;
                    return Unavailable(operation, depth);
                }

                request = request with
                {
                    ToolAttemptOrdinal = checked((ulong)reserved.ToolAttemptOrdinal),
                };
                PreparedPass1NavigationResponse response =
                    _navigationService is IPass1ReservedNavigationService reservedNavigation
                        ? await reservedNavigation.NavigateReservedAsync(
                            request,
                            reserved,
                            cancellationToken)
                        : await _navigationService.NavigateAsync(request, cancellationToken);
                bool accepted = response.Outcome is PreparedPass1NavigationOutcome.DocumentEvidence or
                    PreparedPass1NavigationOutcome.SourceEvidence;
                currentScope = currentScope with
                {
                    ToolAttemptCount = reserved.ToolAttemptCount,
                    AcceptedCallCount = currentScope.AcceptedCallCount + (accepted ? 1 : 0),
                    EvidenceBytes = currentScope.EvidenceBytes + (accepted ? response.EvidenceBytes : 0),
                };
                cursor = response.ReservationTicket ?? reserved;
                if (!effectiveReceiptProvenance.TryRecord(response.ReceiptId))
                {
                    shouldTerminate = true;
                }

                return response;
            }

            PreparedPass1NavigationResponse unreservedResponse =
                await _navigationService.NavigateAsync(request, cancellationToken);
            if (!effectiveReceiptProvenance.TryRecord(unreservedResponse.ReceiptId))
            {
                shouldTerminate = true;
            }

            return unreservedResponse;
        }

        Func<
            PreparedPass1NavigationOperation,
            PreparedNavigateCodeTarget,
            ulong,
            string?,
            CancellationToken,
            Task<PreparedPass1NavigationResponse>> function = NavigateAsync;
        AIFunction tool = AIFunctionFactory.Create(
            function,
            new AIFunctionFactoryOptions
            {
                Name = "navigate_code",
                Description =
                    "Navigate authorized evidence in the current prepared Repository revision.",
                AdditionalProperties = new Dictionary<string, object?>
                {
                    ["strict"] = true,
                },
                SerializerOptions = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions)
                {
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                },
            });
        return new(tool, () => shouldTerminate);
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
