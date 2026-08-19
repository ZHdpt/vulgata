using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Analysis.CodeGraph;

public sealed partial class CodeGraphAnalysisAdapter
{
    private bool IsBound => _options is not null && _snapshots is not null && _process is not null;

    private async Task<AnalysisIndexResult> ExecuteIndexAsync(
        AnalysisScope scope,
        IReadOnlyList<string> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsBound)
        {
            return FailedIndex(_unavailable);
        }

        try
        {
            CodeGraphRepositorySnapshot snapshot = await ResolveSnapshotAsync(scope, cancellationToken);
            CodeGraphProcessResult process = await RunAsync(
                scope,
                snapshot,
                [.. operation, snapshot.RepositoryRoot],
                cancellationToken);
            AnalysisPortFailure? failure = GetProcessFailure(process);
            if (failure is not null)
            {
                return FailedIndex(failure);
            }

            using JsonDocument document = ParseJson(process.StandardOutput, _options!.StandardOutputByteLimit);
            JsonElement root = RequireObject(
                document.RootElement,
                "status", "indexIdentity", "durationMs", "indexedFileCount");
            if (RequireString(root, "status") != "ok")
            {
                throw new InvalidDataException("codegraph returned unsuccessful status");
            }

            string identity = RequireString(root, "indexIdentity");
            if (!StringComparer.Ordinal.Equals(identity, snapshot.ExpectedIndexIdentity))
            {
                throw new InvalidDataException("codegraph index identity mismatch");
            }

            return new AnalysisIndexResult(
                AnalysisOperationStatus.Succeeded,
                identity,
                RequireNonNegativeLong(root, "durationMs"),
                RequireNonNegativeInt(root, "indexedFileCount"),
                failure: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return FailedIndex(MapFailure(exception));
        }
    }

    private async Task<CodeGraphStatusResult> ExecuteStatusAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsBound)
        {
            return FailedStatus(_unavailable);
        }

        try
        {
            CodeGraphRepositorySnapshot snapshot = await ResolveSnapshotAsync(scope, cancellationToken);
            CodeGraphProcessResult process = await RunAsync(
                scope,
                snapshot,
                ["status", snapshot.RepositoryRoot, "--json"],
                cancellationToken);
            AnalysisPortFailure? failure = GetProcessFailure(process);
            if (failure is not null)
            {
                return FailedStatus(failure);
            }

            using JsonDocument document = ParseJson(process.StandardOutput, _options!.StandardOutputByteLimit);
            JsonElement root = RequireObject(document.RootElement, "status", "indexIdentity", "indexedFileCount");
            string status = RequireString(root, "status");
            string identity = RequireString(root, "indexIdentity");
            if (!StringComparer.Ordinal.Equals(identity, snapshot.ExpectedIndexIdentity) || status != "ready")
            {
                throw new InvalidDataException("codegraph status identity mismatch");
            }

            return new CodeGraphStatusResult(
                AnalysisOperationStatus.Succeeded,
                ready: true,
                stale: false,
                RequireNonNegativeInt(root, "indexedFileCount"),
                failure: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return FailedStatus(MapFailure(exception));
        }
    }

    private async Task<SymbolSearchResult> ExecuteSymbolSearchAsync(
        AnalysisScope scope,
        AnalysisSymbolQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsBound)
        {
            return new SymbolSearchResult(AnalysisOperationStatus.Failed, [], _unavailable);
        }

        try
        {
            CodeGraphRepositorySnapshot snapshot = await ResolveSnapshotAsync(scope, cancellationToken);
            CodeGraphProcessResult process = await RunAsync(
                scope,
                snapshot,
                ["query", query.SearchAtom, "--kind", query.Kind.ToString().ToLowerInvariant(),
                    "--path", snapshot.RepositoryRoot, "--limit", Invariant(query.Bounds.ItemLimit), "--json"],
                cancellationToken);
            AnalysisPortFailure? failure = GetProcessFailure(process);
            if (failure is not null)
            {
                return new SymbolSearchResult(AnalysisOperationStatus.Failed, [], failure);
            }

            using JsonDocument document = ParseJson(
                process.StandardOutput,
                Math.Min(_options!.StandardOutputByteLimit, query.Bounds.ByteLimit));
            JsonElement root = RequireObject(document.RootElement, "symbols");
            JsonElement values = RequireArray(root, "symbols", query.Bounds.ItemLimit);
            List<AnalysisSymbolEvidence> evidence = [];
            foreach (JsonElement value in values.EnumerateArray())
            {
                JsonElement item = RequireObject(value, "symbol", "kind", "relativePath", "startLine", "endLine");
                evidence.Add(new AnalysisSymbolEvidence(
                    RequireString(item, "symbol"),
                    ParseSymbolKind(RequireString(item, "kind")),
                    RequireContainedRelativePath(item, "relativePath"),
                    RequireRange(item)));
            }

            return new SymbolSearchResult(AnalysisOperationStatus.Succeeded, evidence, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new SymbolSearchResult(AnalysisOperationStatus.Failed, [], MapFailure(exception));
        }
    }

    private async Task<RelationshipResult> ExecuteRelationshipsAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        string operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsBound)
        {
            return new RelationshipResult(AnalysisOperationStatus.Failed, [], _unavailable);
        }

        try
        {
            CodeGraphRepositorySnapshot snapshot = await ResolveSnapshotAsync(scope, cancellationToken);
            CodeGraphProcessResult process = await RunAsync(
                scope,
                snapshot,
                [operation, request.ExactSymbol, "--path", snapshot.RepositoryRoot,
                    "--limit", Invariant(request.Bounds.ItemLimit), "--json"],
                cancellationToken);
            AnalysisPortFailure? failure = GetProcessFailure(process);
            if (failure is not null)
            {
                return new RelationshipResult(AnalysisOperationStatus.Failed, [], failure);
            }

            using JsonDocument document = ParseJson(
                process.StandardOutput,
                Math.Min(_options!.StandardOutputByteLimit, request.Bounds.ByteLimit));
            JsonElement root = RequireObject(document.RootElement, "relationships");
            JsonElement values = RequireArray(root, "relationships", request.Bounds.ItemLimit);
            List<AnalysisRelationshipEvidence> evidence = [];
            foreach (JsonElement value in values.EnumerateArray())
            {
                JsonElement item = RequireObject(
                    value,
                    "sourceSymbol", "targetSymbol", "relativePath", "startLine", "endLine");
                evidence.Add(new AnalysisRelationshipEvidence(
                    RequireString(item, "sourceSymbol"),
                    RequireString(item, "targetSymbol"),
                    RequireContainedRelativePath(item, "relativePath"),
                    RequireRange(item)));
            }

            return new RelationshipResult(AnalysisOperationStatus.Succeeded, evidence, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new RelationshipResult(AnalysisOperationStatus.Failed, [], MapFailure(exception));
        }
    }

    private async Task<RelatedFilesResult> ExecuteRelatedFilesAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsBound)
        {
            return new RelatedFilesResult(AnalysisOperationStatus.Failed, [], _unavailable);
        }

        try
        {
            CodeGraphRepositorySnapshot snapshot = await ResolveSnapshotAsync(scope, cancellationToken);
            CodeGraphProcessResult process = await RunAsync(
                scope,
                snapshot,
                ["impact", request.ExactSymbol, "--path", snapshot.RepositoryRoot, "--depth", "1", "--json"],
                cancellationToken);
            AnalysisPortFailure? failure = GetProcessFailure(process);
            if (failure is not null)
            {
                return new RelatedFilesResult(AnalysisOperationStatus.Failed, [], failure);
            }

            using JsonDocument document = ParseJson(
                process.StandardOutput,
                Math.Min(_options!.StandardOutputByteLimit, request.Bounds.ByteLimit));
            JsonElement root = RequireObject(document.RootElement, "relativePaths");
            JsonElement values = RequireArray(root, "relativePaths", request.Bounds.ItemLimit);
            string[] paths = values.EnumerateArray().Select(RequireContainedRelativePath).ToArray();
            if (paths.Distinct(StringComparer.Ordinal).Count() != paths.Length)
            {
                throw new InvalidDataException("codegraph returned duplicate paths");
            }

            return new RelatedFilesResult(AnalysisOperationStatus.Succeeded, paths, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new RelatedFilesResult(AnalysisOperationStatus.Failed, [], MapFailure(exception));
        }
    }

    private async Task<CodeGraphRepositorySnapshot> ResolveSnapshotAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= scope.Deadline.UnixTimeMilliseconds)
        {
            throw new TimeoutException("codegraph deadline expired");
        }

        CodeGraphRepositorySnapshot? snapshot = await _snapshots!.ResolveAsync(scope, cancellationToken);
        if (snapshot is null || snapshot.RepositoryId != scope.RepositoryId ||
            snapshot.ScanRunId != scope.ScanRunId ||
            !StringComparer.Ordinal.Equals(snapshot.ProfiledRevision, scope.ProfiledRevision) ||
            !StringComparer.Ordinal.Equals(snapshot.WorkingCopyBindingToken, scope.WorkingCopyBindingToken))
        {
            throw new UnauthorizedAccessException("codegraph snapshot binding rejected");
        }

        return snapshot;
    }

    private Task<CodeGraphProcessResult> RunAsync(
        AnalysisScope scope,
        CodeGraphRepositorySnapshot snapshot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        long remainingMilliseconds = scope.Deadline.UnixTimeMilliseconds -
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (remainingMilliseconds <= 0)
        {
            throw new TimeoutException("codegraph deadline expired");
        }

        TimeSpan timeout = TimeSpan.FromMilliseconds(Math.Min(
            _options!.OperationTimeout.TotalMilliseconds,
            remainingMilliseconds));
        return _process!.RunAsync(
            new CodeGraphProcessRequest(
                _options.CommandPath,
                arguments,
                snapshot.RepositoryRoot,
                timeout,
                _options.StandardOutputByteLimit,
                _options.StandardErrorByteLimit),
            cancellationToken);
    }

    private static AnalysisPortFailure? GetProcessFailure(CodeGraphProcessResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.TimedOut)
        {
            return new AnalysisPortFailure(
                AnalysisPortFailureReason.TimedOut,
                "codegraph_timed_out",
                "codegraph timed out");
        }

        if (result.ExitCode != 0)
        {
            return new AnalysisPortFailure(
                AnalysisPortFailureReason.ProcessFailed,
                "codegraph_process_failed",
                DecodeDiagnostic(result.StandardError));
        }

        return null;
    }

    private static AnalysisPortFailure MapFailure(Exception exception) => exception switch
    {
        TimeoutException => new(
            AnalysisPortFailureReason.TimedOut,
            "codegraph_timed_out",
            "codegraph timed out"),
        UnauthorizedAccessException => new(
            AnalysisPortFailureReason.Rejected,
            "codegraph_scope_rejected",
            "codegraph scope rejected"),
        JsonException or InvalidDataException or ArgumentException or OverflowException => new(
            AnalysisPortFailureReason.MalformedResponse,
            "codegraph_response_malformed",
            "codegraph response malformed"),
        _ => new(
            AnalysisPortFailureReason.ProcessFailed,
            "codegraph_process_failed",
            "codegraph process failed"),
    };

    private static AnalysisIndexResult FailedIndex(AnalysisPortFailure failure) =>
        new(AnalysisOperationStatus.Failed, "codegraph-failed", 0, 0, failure);

    private static CodeGraphStatusResult FailedStatus(AnalysisPortFailure failure) =>
        new(AnalysisOperationStatus.Failed, false, true, 0, failure);

    private static JsonDocument ParseJson(byte[] bytes, int byteLimit)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0 || bytes.Length > byteLimit)
        {
            throw new InvalidDataException("codegraph response outside byte limit");
        }

        return JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 16,
        });
    }

    private static JsonElement RequireObject(JsonElement value, params string[] expectedProperties)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("codegraph expected JSON object");
        }

        HashSet<string> expected = new(expectedProperties, StringComparer.Ordinal);
        HashSet<string> observed = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!expected.Contains(property.Name) || !observed.Add(property.Name))
            {
                throw new InvalidDataException("codegraph returned unknown or duplicate field");
            }
        }

        if (!observed.SetEquals(expected))
        {
            throw new InvalidDataException("codegraph response field missing");
        }

        return value;
    }

    private static JsonElement RequireArray(JsonElement parent, string propertyName, int itemLimit)
    {
        JsonElement value = parent.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > itemLimit)
        {
            throw new InvalidDataException("codegraph result exceeds item limit");
        }

        return value;
    }

    private static string RequireString(JsonElement parent, string propertyName)
    {
        JsonElement value = parent.GetProperty(propertyName);
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1_024 || text.Any(char.IsControl))
        {
            throw new InvalidDataException("codegraph returned invalid text");
        }

        return text;
    }

    private static AnalysisLineRange RequireRange(JsonElement item) =>
        new(RequirePositiveInt(item, "startLine"), RequirePositiveInt(item, "endLine"));

    private static int RequirePositiveInt(JsonElement parent, string propertyName)
    {
        int value = RequireNonNegativeInt(parent, propertyName);
        return value > 0 ? value : throw new InvalidDataException("codegraph returned invalid range");
    }

    private static int RequireNonNegativeInt(JsonElement parent, string propertyName)
    {
        JsonElement value = parent.GetProperty(propertyName);
        if (!value.TryGetInt32(out int number) || number < 0)
        {
            throw new InvalidDataException("codegraph returned invalid number");
        }

        return number;
    }

    private static long RequireNonNegativeLong(JsonElement parent, string propertyName)
    {
        JsonElement value = parent.GetProperty(propertyName);
        if (!value.TryGetInt64(out long number) || number < 0)
        {
            throw new InvalidDataException("codegraph returned invalid number");
        }

        return number;
    }

    private static AnalysisSymbolKind ParseSymbolKind(string value) => value switch
    {
        "type" => AnalysisSymbolKind.Type,
        "method" => AnalysisSymbolKind.Method,
        "property" => AnalysisSymbolKind.Property,
        "field" => AnalysisSymbolKind.Field,
        "constructor" => AnalysisSymbolKind.Constructor,
        _ => throw new InvalidDataException("codegraph returned unknown symbol kind"),
    };

    private static string RequireContainedRelativePath(JsonElement parent, string propertyName) =>
        RequireContainedRelativePath(parent.GetProperty(propertyName));

    private static string RequireContainedRelativePath(JsonElement value)
    {
        string? path = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1_024)
        {
            throw new InvalidDataException("codegraph returned invalid path");
        }

        string normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Contains(':') ||
            normalized.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException("codegraph returned uncontained path");
        }

        return normalized;
    }

    private static string DecodeDiagnostic(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return "codegraph process failed";
        }

        string value = Encoding.UTF8.GetString(bytes).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "codegraph process failed";
        }

        return value.Length <= 256 ? value : value[..256];
    }

    private static string Invariant(int value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
