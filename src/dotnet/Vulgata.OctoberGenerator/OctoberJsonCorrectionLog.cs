using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vulgata.Agents.WorkerExecution;

namespace Vulgata.OctoberGenerator;

/// <summary>Host-owned diagnostic output, separate from Knowledge commit authority.</summary>
internal sealed class OctoberJsonCorrectionLog
{
    private readonly string _directory;
    private readonly string _path;
    private readonly TimeProvider _clock;

    internal OctoberJsonCorrectionLog(string ownedDirectory, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedDirectory);
        if (!Path.IsPathFullyQualified(ownedDirectory) || !Directory.Exists(ownedDirectory))
            throw new ArgumentException("An existing absolute host-owned directory is required.", nameof(ownedDirectory));
        _directory = Path.GetFullPath(ownedDirectory);
        _path = Path.Combine(_directory, "worker-json-corrections.jsonl");
        _clock = timeProvider ?? TimeProvider.System;
    }

    internal async Task<bool> AppendAsync(OctoberFileWorkerRequest request,
        ImmutableArray<OctoberJsonCorrectionEvent> events, CancellationToken cancellationToken = default)
    {
        if (request?.Binding is null || request.Source?.Origin is null || request.Limits is null ||
            request.Binding.BatchId == Guid.Empty || request.RunId == Guid.Empty || request.FileWorkstreamId == Guid.Empty ||
            request.AdmissionId == Guid.Empty || request.AttemptId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.ProviderProfileId) || string.IsNullOrWhiteSpace(request.Source.Origin.RelativePath) ||
            events.IsDefaultOrEmpty || events.Length > 11 || cancellationToken.IsCancellationRequested)
            return false;
        // One bounded append after Worker settlement. No lock retry, provider replay or
        // abandoned write task on timeout. Cancellation covers the actual write/flush.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            var lines = new StringBuilder();
            DateTimeOffset recordedAt = _clock.GetUtcNow();
            foreach (OctoberJsonCorrectionEvent entry in events)
            {
                if (entry is null || !Enum.IsDefined(entry.Phase) || entry.RetryOrdinal is < 0 or > 3 ||
                    entry.WindowOrdinal is < 1 or > 100 || entry.ProviderRounds is < 0 or > 100 ||
                    entry.ElapsedMilliseconds < 0 || entry.CandidateUtf8Bytes is < 0 or > 1048576 ||
                    !Enum.IsDefined(entry.Strategy) || entry.RemovedMessages is < 0 or > 1000000)
                    return false;
                // Explicit projection prevents a future result/request property from leaking
                // source, model text, reasoning, physical paths or credentials into the log.
                string line = JsonSerializer.Serialize(new
                {
                    recordedAtUtc = recordedAt, batchId = request.Binding.BatchId,
                    runId = request.RunId, fileWorkstreamId = request.FileWorkstreamId,
                    admissionId = request.AdmissionId, attemptId = request.AttemptId,
                    relativePathSha256 = Hash(request.Source.Origin.RelativePath),
                    providerSha256 = Hash(request.ProviderProfileId), hardRetryMode = request.Limits.HardRetryMode,
                    maximumOrdinaryCorrections = request.Limits.MaximumJsonCorrectionAttempts,
                    maximumProviderRounds = request.Limits.MaximumProviderRounds,
                    maximumOutputTokensPerRequest = request.Limits.MaximumOutputTokensPerRequest,
                    maximumInputUtf8Bytes = request.Limits.MaximumInputUtf8Bytes,
                    maximumOutputUtf8Bytes = request.Limits.MaximumOutputUtf8Bytes,
                    phase = entry.Phase.ToString(), retryOrdinal = entry.RetryOrdinal,
                    windowOrdinal = entry.WindowOrdinal, providerRounds = entry.ProviderRounds,
                    elapsedMilliseconds = entry.ElapsedMilliseconds, candidateUtf8Bytes = entry.CandidateUtf8Bytes,
                    diagnostic = entry.Diagnostic is { } diagnostic ? new
                    {
                        category = diagnostic.Category?.ToString(), slot = diagnostic.Slot?.ToString(),
                        fieldFailure = diagnostic.FieldFailure?.ToString(), field = diagnostic.Field?.ToString(),
                        unitIndex = diagnostic.UnitIndex, lineNumber = diagnostic.LineNumber,
                        bytePositionInLine = diagnostic.BytePositionInLine
                    } : null,
                    status = entry.Status?.ToString(), failureStage = entry.FailureStage?.ToString(),
                    budgetFailure = entry.BudgetFailure?.ToString(), terminalFailure = entry.TerminalFailure?.ToString(),
                    finishReason = entry.FinishReason?.ToString(), strategy = entry.Strategy.ToString(),
                    removedMessages = entry.RemovedMessages
                });
                if (Encoding.UTF8.GetByteCount(line) > 4096) return false;
                lines.Append(line).Append('\n');
            }
            for (DirectoryInfo? parent = new(_directory); parent is not null; parent = parent.Parent)
                if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            if (Directory.Exists(_path) || Path.Exists(_path) &&
                (File.GetAttributes(_path) & FileAttributes.ReparsePoint) != 0) return false;
            deadline.Token.ThrowIfCancellationRequested();
            await using var stream = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
            long previousLength = stream.Length;
            if (previousLength > 0)
            {
                stream.Seek(-1, SeekOrigin.End);
                if (stream.ReadByte() != '\n') return false;
            }
            stream.Seek(0, SeekOrigin.End);
            try
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(lines.ToString()), deadline.Token).ConfigureAwait(false);
                await stream.FlushAsync(deadline.Token).ConfigureAwait(false);
                return true;
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                // Undo only this append while exclusively held; never rewrite prior records.
                try { stream.SetLength(previousLength); }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
                return false;
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or OperationCanceledException or ArgumentException)
        {
            return false;
        }
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
