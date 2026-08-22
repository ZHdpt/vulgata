using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Sole Story 12.3 prepared dispatch owner. It projects only supplied prepared
/// units and applies one pair-wide capacity limit; it has no enumeration or
/// navigation-scheduling surface.
/// </summary>
public sealed class PreparedPass1WorkerDispatcher(IPreparedPass1JobExecutor executor)
    : IPreparedPass1WorkerDispatcher
{
    private const int _maximumProviderTurns = 10;
    private const int _maximumToolAttempts = 8;
    private const string _promptResourceName =
        "Vulgata.Agents.Prompts.pass1-code-logic-worker.v2.md";
    private readonly IPreparedPass1JobExecutor _executor =
        executor ?? throw new ArgumentNullException(nameof(executor));
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, ConversationState>
        _conversationStates = new();

    public async Task<PreparedPass1DispatchResult> DispatchAsync(
        PreparedPass1DispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.ConcurrencyLimit is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Prepared Pass 1 concurrency must be between one and four.");
        }

        PreparedPass1WorkerPacket[] packets = BuildPackets(request);
        string prompt = LoadPrompt(typeof(PreparedPass1WorkerDispatcher).Assembly);
        var results = new PreparedPass1JobDispatchResult[packets.Length];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, packets.Length),
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = request.ConcurrencyLimit,
            },
            async (index, token) => await ExecuteOneAsync(
                packets[index],
                index,
                prompt,
                results,
                token));

        return new PreparedPass1DispatchResult(
            Array.AsReadOnly(packets),
            Array.AsReadOnly(results));
    }

    private async Task ExecuteOneAsync(
        PreparedPass1WorkerPacket packet,
        int index,
        string prompt,
        PreparedPass1JobDispatchResult[] results,
        CancellationToken cancellationToken)
    {
        ConversationState state = _conversationStates.GetOrAdd(
            packet.JobId,
            _ => new ConversationState(packet.RemainingNavigationBudget));
        PreparedPass1RawWorkerResult raw;
        PreparedPass1WorkerPacket currentPacket = packet;
        while (true)
        {
            int turn;
            int remaining;
            lock (state)
            {
                if (state.ProviderTurns >= _maximumProviderTurns)
                {
                    raw = PreparedPass1ResultParser.CreateDeferredCallsFallback(packet);
                    break;
                }

                state.ProviderTurns++;
                turn = state.ProviderTurns;
                remaining = state.RemainingNavigationBudget;
            }

            currentPacket = packet with { RemainingNavigationBudget = remaining };
            raw = await _executor.ExecuteAsync(currentPacket, prompt, cancellationToken);
            if (raw.Outcome != PreparedPass1RawWorkerOutcome.Rejected ||
                !string.Equals(
                    raw.ReasonCode,
                    "invalid-or-repeated-tool-call",
                    StringComparison.Ordinal))
            {
                break;
            }

            lock (state)
            {
                if (state.RemainingNavigationBudget > 0)
                {
                    state.RemainingNavigationBudget--;
                }
            }

            if (turn >= _maximumProviderTurns)
            {
                raw = PreparedPass1ResultParser.CreateDeferredCallsFallback(packet);
                break;
            }
        }
        if (!Enum.IsDefined(raw.Outcome))
        {
            throw new InvalidDataException("Prepared Pass 1 provider returned an unknown outcome.");
        }

        PreparedPass1ParsedWorkerResult? parsed = null;
        if (raw.Outcome == PreparedPass1RawWorkerOutcome.Completed)
        {
            try
            {
                parsed = PreparedPass1ResultParser.Parse(packet, raw.Utf8Json);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or ArgumentException or
                    System.Text.Json.JsonException or OverflowException)
            {
                raw = new PreparedPass1RawWorkerResult(
                    PreparedPass1RawWorkerOutcome.Rejected,
                    ReadOnlyMemory<byte>.Empty,
                    "worker-result-rejected");
            }
        }
        if (raw.Outcome != PreparedPass1RawWorkerOutcome.Completed && !raw.Utf8Json.IsEmpty)
        {
            throw new InvalidDataException("A failed prepared Worker result cannot carry provider content.");
        }

        results[index] = new PreparedPass1JobDispatchResult(
            packet.JobId,
            packet.PairSelectionOrdinal,
            raw.Outcome,
            parsed,
            GetSanitizedReasonCode(raw));
    }

    private static string? GetSanitizedReasonCode(PreparedPass1RawWorkerResult raw) => raw.Outcome switch
    {
        PreparedPass1RawWorkerOutcome.Completed => null,
        PreparedPass1RawWorkerOutcome.ProviderUnavailable when string.Equals(
            raw.ReasonCode,
            "provider-configuration",
            StringComparison.Ordinal) => "provider-configuration",
        PreparedPass1RawWorkerOutcome.ProviderUnavailable => "provider-unavailable",
        PreparedPass1RawWorkerOutcome.Rejected when string.Equals(
            raw.ReasonCode,
            "invalid-or-repeated-tool-call",
            StringComparison.Ordinal) => "invalid-or-repeated-tool-call",
        PreparedPass1RawWorkerOutcome.Rejected when raw.ReasonCode is
            "integrity-failure" or "security-failure" or "provider-configuration" or
            "cancelled" or "unclassified-systemic" => raw.ReasonCode,
        PreparedPass1RawWorkerOutcome.Rejected => "worker-result-rejected",
        _ => throw new InvalidDataException("Prepared Pass 1 provider returned an unknown outcome."),
    };

    private static PreparedPass1WorkerPacket[] BuildPackets(PreparedPass1DispatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.PreparedFileCodeUnits);
        ArgumentNullException.ThrowIfNull(request.ContextSnapshots);
        ArgumentNullException.ThrowIfNull(request.CommunicationClassifications);
        if (request.PreparedFileCodeUnits.Count == 0)
        {
            return [];
        }

        PreparedPass1FileCodeUnitAssignment[] assignments = [.. request.PreparedFileCodeUnits
            .OrderBy(value => value.PairSelectionOrdinal)
        ];
        if (assignments.Select(value => value.JobId).Distinct().Count() != assignments.Length ||
            assignments.Select(value => value.FileCodeUnitId).Distinct().Count() != assignments.Length ||
            assignments.Select(value => value.PairSelectionOrdinal).Distinct().Count() != assignments.Length ||
            assignments.Select(value => value.PreparedScanManifestId).Distinct().Count() is < 1 or > 2 ||
            assignments.Select(value => value.RepositoryId).Distinct().Count() is < 1 or > 2 ||
            assignments.Select(value => value.ScanRunId).Distinct().Count() is < 1 or > 2)
        {
            throw new InvalidDataException("Prepared Pass 1 units must belong to one exact prepared pair.");
        }

        Guid pairExecutionId = assignments[0].PairExecutionId;
        Guid systemId = assignments[0].SystemId;
        string pairFingerprint = assignments[0].PairPackageFingerprint;
        if (assignments.Any(value =>
            value.PairExecutionId != pairExecutionId ||
            value.SystemId != systemId ||
            !string.Equals(value.PairPackageFingerprint, pairFingerprint, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Prepared Pass 1 units do not belong to one exact prepared pair.");
        }


        HashSet<Guid> fileCodeUnitIds = [.. assignments.Select(value => value.FileCodeUnitId)];
        HashSet<Guid> contextSnapshotIds = [.. assignments.Select(value => value.ContextSnapshotId)];
        if (request.ContextSnapshots.Count != contextSnapshotIds.Count ||
            request.ContextSnapshots.Select(value => value.ContextSnapshotId).Distinct().Count() != request.ContextSnapshots.Count ||
            request.ContextSnapshots.Any(value => !contextSnapshotIds.Contains(value.ContextSnapshotId)) ||
            request.CommunicationClassifications.Any(value => !fileCodeUnitIds.Contains(value.FileCodeUnitId)))
        {
            throw new InvalidDataException("Prepared Pass 1 projections contain unrelated or duplicate bindings.");
        }

        return [.. assignments.Select(assignment => BuildPacket(request, assignment))];
    }

    private static PreparedPass1WorkerPacket BuildPacket(
        PreparedPass1DispatchRequest request,
        PreparedPass1FileCodeUnitAssignment assignment)
    {
        ValidateAssignment(assignment);
        PreparedPass1ContextSnapshotProjection snapshot = request.ContextSnapshots.SingleOrDefault(value =>
            value.ContextSnapshotId == assignment.ContextSnapshotId &&
            value.RepositoryId == assignment.RepositoryId &&
            value.ScanRunId == assignment.ScanRunId &&
            string.Equals(value.ProfiledRevision, assignment.ProfiledRevision, StringComparison.Ordinal))
            ?? throw new InvalidDataException("Prepared Pass 1 unit has no exact bound Scan Context Snapshot.");
        RequireSha256(snapshot.PreparedScanContextIdentityHash, nameof(snapshot.PreparedScanContextIdentityHash));
        RequireSha256(snapshot.ContextContentHash, nameof(snapshot.ContextContentHash));
        if (new[] { snapshot.GlobalContext, snapshot.SystemContext, snapshot.RepositoryContext }
            .Any(value => value is { Length: > 10_000 }))
        {
            throw new InvalidDataException("Prepared Pass 1 advisory context exceeds its bound.");
        }

        PreparedPass1CommunicationClassificationProjection[] classifications = [.. request
            .CommunicationClassifications
            .Where(value => value.FileCodeUnitId == assignment.FileCodeUnitId)
            .OrderBy(value => value.ClassificationFingerprint, StringComparer.Ordinal)
        ];
        if (classifications.Select(value => value.ClassificationId).Distinct().Count() != classifications.Length ||
            classifications.Any(value =>
                value.PreparedScanManifestId != assignment.PreparedScanManifestId ||
                value.RepositoryId != assignment.RepositoryId ||
                value.ScanRunId != assignment.ScanRunId ||
                !string.Equals(value.ProfiledRevision, assignment.ProfiledRevision, StringComparison.Ordinal) ||
                !string.Equals(value.RepositoryRelativePath, assignment.RepositoryRelativePath, StringComparison.Ordinal) ||
                !value.EvidenceLocator.StartsWith($"{assignment.RepositoryRelativePath}#", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Prepared Pass 1 classification is outside its assigned File Code Unit.");
        }

        foreach (PreparedPass1CommunicationClassificationProjection classification in classifications)
        {
            RequireSha256(classification.ClassificationFingerprint, nameof(classification.ClassificationFingerprint));
            if (!Enum.IsDefined(classification.Level) ||
                !Enum.IsDefined(classification.Confidence) ||
                !Enum.IsDefined(classification.Rule) ||
                !Enum.IsDefined(classification.Provenance) ||
                (classification.Kind.HasValue && !Enum.IsDefined(classification.Kind.Value)) ||
                string.IsNullOrWhiteSpace(classification.CatalogRuleIdentity))
            {
                throw new InvalidDataException("Prepared Pass 1 classification contains an invalid closed value.");
            }
        }

        return new PreparedPass1WorkerPacket(
            assignment.PairExecutionId,
            assignment.JobId,
            assignment.ScanExecutionId,
            assignment.SystemId,
            assignment.RepositoryId,
            assignment.ScanRunId,
            assignment.ProfiledRevision,
            assignment.FileCodeUnitId,
            assignment.FileCodeUnitIdentityFingerprint,
            assignment.PreparedScanManifestId,
            assignment.PreparedScanManifestFingerprint,
            assignment.PairPackageFingerprint,
            assignment.RepositoryRelativePath,
            assignment.ByteStart,
            assignment.ByteEnd,
            assignment.LineStart,
            assignment.LineEnd,
            assignment.SegmentOrdinal,
            assignment.SegmentCount,
            assignment.FileHash,
            assignment.SourceHash,
            assignment.PromptIdentity,
            assignment.DocumentSchemaIdentity,
            assignment.AnalysisRuntimeIdentityHash,
            assignment.ManifestOrdinal,
            assignment.PairSelectionOrdinal,
            assignment.SourceUtf8.ToArray(),
            snapshot,
            Array.AsReadOnly(classifications))
        {
            RemainingNavigationBudget = assignment.RemainingNavigationBudget,
        };
    }

    private static void ValidateAssignment(PreparedPass1FileCodeUnitAssignment value)
    {
        Guid expectedJobId = ScanJobExecution.Create(
            value.ScanExecutionId,
            value.RepositoryId,
            value.ScanRunId,
            value.FileCodeUnitId,
            value.ManifestOrdinal,
            DateTimeOffset.UnixEpoch).Id;
        if (new[]
            {
                value.PairExecutionId, value.JobId, value.ScanExecutionId, value.SystemId,
                value.RepositoryId, value.ScanRunId, value.FileCodeUnitId,
                value.PreparedScanManifestId, value.ContextSnapshotId,
            }.Any(identifier => identifier == Guid.Empty) ||
            value.ManifestOrdinal < 1 || value.PairSelectionOrdinal < 1 ||
            value.ByteStart < 0 || value.ByteEnd <= value.ByteStart ||
            value.LineStart < 1 || value.LineEnd < value.LineStart ||
            value.SegmentOrdinal < 1 || value.SegmentCount < value.SegmentOrdinal ||
            value.SourceUtf8.Length != value.ByteEnd - value.ByteStart ||
            value.JobId != expectedJobId ||
            !IsSafePath(value.RepositoryRelativePath) ||
            !IsRevision(value.ProfiledRevision) ||
            string.IsNullOrWhiteSpace(value.PromptIdentity) || value.PromptIdentity.Length > 128 ||
            !string.Equals(value.DocumentSchemaIdentity, "code-logic/file-v2", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Prepared Pass 1 File Code Unit binding is invalid.");
        }

        RequireSha256(value.FileCodeUnitIdentityFingerprint, nameof(value.FileCodeUnitIdentityFingerprint));
        RequireSha256(value.PreparedScanManifestFingerprint, nameof(value.PreparedScanManifestFingerprint));
        RequireSha256(value.PairPackageFingerprint, nameof(value.PairPackageFingerprint));
        RequireSha256(value.FileHash, nameof(value.FileHash));
        RequireSha256(value.SourceHash, nameof(value.SourceHash));
        RequireSha256(value.AnalysisRuntimeIdentityHash, nameof(value.AnalysisRuntimeIdentityHash));
        string actualSourceHash = Convert.ToHexStringLower(SHA256.HashData(value.SourceUtf8.Span));
        if (!string.Equals(actualSourceHash, value.SourceHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Prepared Pass 1 source bytes do not match Source Hash.");
        }
    }

    private static string LoadPrompt(Assembly assembly)
    {
        string[] matches = [.. assembly.GetManifestResourceNames()
            .Where(name => string.Equals(name, _promptResourceName, StringComparison.Ordinal))
        ];
        if (matches.Length != 1)
        {
            throw new InvalidOperationException("The fixed prepared Pass 1 v2 prompt is unavailable or ambiguous.");
        }

        using Stream stream = assembly.GetManifestResourceStream(matches[0])
            ?? throw new InvalidOperationException("The fixed prepared Pass 1 v2 prompt is unavailable.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        string prompt = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(prompt) ||
            !prompt.Contains("code-logic/file-v2", StringComparison.Ordinal) ||
            !prompt.Contains("navigate_code", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The fixed prepared Pass 1 v2 prompt is invalid.");
        }

        return prompt;
    }

    private static void RequireSha256(string value, string parameterName)
    {
        if (value.Length != 64 || value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new InvalidDataException($"{parameterName} must be lowercase SHA-256.");
        }
    }

    private static bool IsRevision(string value) =>
        value.Length is 40 or 64 &&
        value.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static bool IsSafePath(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 2_048 &&
        value[0] is not ('/' or '\\') &&
        !Path.IsPathRooted(value) &&
        !value.Contains('\\', StringComparison.Ordinal) &&
        value.Split('/').All(segment => segment is not ("" or "." or ".."));

    private sealed class ConversationState
    {
        public ConversationState(int remainingNavigationBudget)
        {
            if (remainingNavigationBudget is < 0 or > _maximumToolAttempts)
            {
                throw new InvalidDataException("The remaining navigation budget is outside its closed bound.");
            }

            RemainingNavigationBudget = remainingNavigationBudget;
        }

        public int ProviderTurns { get; set; }
        public int RemainingNavigationBudget { get; set; }
    }
}
