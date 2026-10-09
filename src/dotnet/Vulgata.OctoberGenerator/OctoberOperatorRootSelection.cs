using System.Collections.Immutable;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.OctoberGenerator;

internal sealed record OctoberOperatorRootCandidate(Guid UnitId, DocumentVersionReference Document,
    Guid RepositoryId, string RelativePath, string SemanticSelector);

internal sealed record OctoberOperatorRootSelectionRequest(int Version, Guid RequestId, Guid RunId,
    Guid CorpusId, DateTimeOffset ExpiresAtUtc, ImmutableArray<OctoberOperatorRootCandidate> Candidates);

internal sealed record OctoberOperatorRootSelectionResponse(int Version, Guid RequestId, Guid RunId,
    ImmutableArray<Guid> SelectedUnitIds);

internal sealed record OctoberOperatorRootSelectionOptions(string Mode = "exact-settings",
    string? Directory = null, int? TimeoutSeconds = null);

/// <summary>Finite, original-run operator exchange; no absent response is an empty selection.</summary>
internal sealed class OctoberOperatorRootSelection
{
    private readonly OctoberOperatorRootSelectionOptions _options;
    private readonly TimeProvider _time;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal OctoberOperatorRootSelection(OctoberOperatorRootSelectionOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _time = timeProvider ?? TimeProvider.System;
    }

    internal async Task<OctoberOperatorRootSelectionResponse?> InteractAsync(OctoberOperatorRootSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_options.Mode != "operator" || _options.TimeoutSeconds is not (>= 1 and <= 300) ||
            request.Version != 1 || request.RequestId == Guid.Empty || request.RunId == Guid.Empty || request.CorpusId == Guid.Empty ||
            request.Candidates.IsDefault || request.Candidates.Length > 1024 ||
            request.Candidates.Any(candidate => candidate is null || candidate.UnitId == Guid.Empty || candidate.Document is null ||
                candidate.Document.BatchId == Guid.Empty || candidate.Document.LogicalDocumentId == Guid.Empty || candidate.Document.DocumentVersionId == Guid.Empty ||
                !new OctoberBusinessSettings([new(candidate.RepositoryId, candidate.RelativePath, candidate.SemanticSelector,
                    FlowRootKind.SelectedCodeUnit)], new(), 1).IsValid()) ||
            request.Candidates.Select(candidate => candidate.UnitId).Distinct().Count() != request.Candidates.Length)
            throw new InvalidOperationException("October operator request is invalid.");
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(request, Json);
        if (payload.Length > 1024 * 1024) throw new InvalidOperationException("October operator request exceeds its bound.");
        TimeSpan remaining = request.ExpiresAtUtc - _time.GetUtcNow();
        remaining = remaining < TimeSpan.FromSeconds(_options.TimeoutSeconds.Value) ? remaining : TimeSpan.FromSeconds(_options.TimeoutSeconds.Value);
        if (remaining <= TimeSpan.Zero) throw new OperationCanceledException("October operator request expired.");
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(remaining, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        long started = _time.GetTimestamp();
        using var files = OctoberOperatorExchangeFiles.Create(_options.Directory!);
        files.PublishRequest(payload);
        while (true)
        {
            linked.Token.ThrowIfCancellationRequested();
            if (_time.GetElapsedTime(started) >= remaining) throw new OperationCanceledException("October operator request expired.");
            byte[]? bytes = files.TryReadResponse(64 * 1024);
            if (bytes is not null)
            {
                using var document = JsonDocument.Parse(bytes);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("October operator response is invalid.");
                var properties = root.EnumerateObject().ToArray();
                string[] required = ["version", "requestId", "runId", "selectedUnitIds"];
                if (properties.Length != required.Length || !properties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(required))
                    throw new InvalidOperationException("October operator response fields are invalid.");
                var response = root.Deserialize<OctoberOperatorRootSelectionResponse>(Json);
                if (response is null || response.Version != 1 || response.RequestId != request.RequestId || response.RunId != request.RunId ||
                    response.SelectedUnitIds.IsDefault || response.SelectedUnitIds.Length > 256 ||
                    response.SelectedUnitIds.Distinct().Count() != response.SelectedUnitIds.Length ||
                    response.SelectedUnitIds.Any(id => !request.Candidates.Any(candidate => candidate.UnitId == id)))
                    throw new InvalidOperationException("October operator response selection is invalid.");
                linked.Token.ThrowIfCancellationRequested();
                if (_time.GetElapsedTime(started) >= remaining) throw new OperationCanceledException("October operator request expired.");
                return response;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(25), _time, linked.Token);
        }
    }
}
