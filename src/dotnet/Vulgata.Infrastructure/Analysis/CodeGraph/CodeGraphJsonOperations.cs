using System.Text;
using System.Text.Json;
using System.Diagnostics;
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
            Stopwatch duration = Stopwatch.StartNew();
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

            RequireMutationOutput(
                process.StandardOutput,
                _options!.StandardOutputByteLimit,
                allowEmpty: operation is ["index", "--quiet"]);

            CodeGraphRepositorySnapshot statusSnapshot =
                await ResolveSnapshotAsync(scope, cancellationToken);
            CodeGraphProcessResult statusProcess = await RunAsync(
                scope,
                statusSnapshot,
                ["status", statusSnapshot.RepositoryRoot, "--json"],
                cancellationToken);
            failure = GetProcessFailure(statusProcess);
            if (failure is not null)
            {
                return FailedIndex(failure);
            }

            ParsedStatus status = ParseStatus(
                statusProcess.StandardOutput,
                _options.StandardOutputByteLimit,
                statusSnapshot);
            if (!status.Ready || status.Stale)
            {
                throw new InvalidDataException("codegraph index is not ready");
            }

            duration.Stop();
            return new AnalysisIndexResult(
                AnalysisOperationStatus.Succeeded,
                statusSnapshot.ExpectedIndexIdentity,
                duration.ElapsedMilliseconds,
                status.IndexedFileCount,
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

            ParsedStatus status = ParseStatus(
                process.StandardOutput,
                _options!.StandardOutputByteLimit,
                snapshot);

            return new CodeGraphStatusResult(
                AnalysisOperationStatus.Succeeded,
                status.Ready,
                status.Stale,
                status.IndexedFileCount,
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
                ["query", query.SearchAtom, "--kind", ToCodeGraphSymbolKind(query.Kind),
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
            JsonElement values = RequireRootArray(document.RootElement, query.Bounds.ItemLimit);
            List<AnalysisSymbolEvidence> evidence = [];
            foreach (JsonElement value in values.EnumerateArray())
            {
                JsonElement result = RequireObject(value, "node", "score");
                RequireFiniteNumber(result, "score");
                JsonElement item = RequireObject(
                    result.GetProperty("node"),
                    "id", "kind", "name", "qualifiedName", "filePath", "language",
                    "startLine", "endLine", "startColumn", "endColumn", "visibility",
                    "isExported", "isAsync", "isStatic", "isAbstract", "updatedAt");
                _ = RequireString(item, "id");
                _ = RequireString(item, "name");
                _ = RequireString(item, "language");
                AnalysisLineRange range = RequireRange(item);
                int startColumn = RequireNonNegativeInt(item, "startColumn");
                int endColumn = RequireNonNegativeInt(item, "endColumn");
                if (range.StartLine == range.EndLine && endColumn < startColumn)
                {
                    throw new InvalidDataException("codegraph returned invalid column range");
                }

                RequireVisibility(item, "visibility");
                _ = RequireBoolean(item, "isExported");
                _ = RequireBoolean(item, "isAsync");
                _ = RequireBoolean(item, "isStatic");
                _ = RequireBoolean(item, "isAbstract");
                _ = RequireNonNegativeLong(item, "updatedAt");
                evidence.Add(new AnalysisSymbolEvidence(
                    RequireString(item, "qualifiedName"),
                    ParseSymbolKind(RequireString(item, "kind")),
                    RequireContainedRelativePath(item, "filePath"),
                    range));
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
            string collectionName = operation == "callers" ? "callers" : "callees";
            JsonElement root = RequireObject(document.RootElement, "symbol", collectionName);
            string requestedSymbol = RequireString(root, "symbol");
            if (!StringComparer.Ordinal.Equals(requestedSymbol, request.ExactSymbol))
            {
                throw new InvalidDataException("codegraph relationship symbol mismatch");
            }

            JsonElement values = RequireArray(root, collectionName, request.Bounds.ItemLimit);
            List<AnalysisRelationshipEvidence> evidence = [];
            foreach (JsonElement value in values.EnumerateArray())
            {
                JsonElement item = RequireObject(value, "name", "kind", "filePath", "startLine");
                string relatedSymbol = RequireString(item, "name");
                _ = ParseSymbolKind(RequireString(item, "kind"));
                int line = RequirePositiveInt(item, "startLine");
                evidence.Add(new AnalysisRelationshipEvidence(
                    operation == "callers" ? relatedSymbol : requestedSymbol,
                    operation == "callers" ? requestedSymbol : relatedSymbol,
                    RequireContainedRelativePath(item, "filePath"),
                    new AnalysisLineRange(line, line)));
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
            JsonElement root = RequireObject(
                document.RootElement,
                "symbol", "depth", "nodeCount", "edgeCount", "affected");
            if (!StringComparer.Ordinal.Equals(RequireString(root, "symbol"), request.ExactSymbol) ||
                RequireNonNegativeInt(root, "depth") != 1)
            {
                throw new InvalidDataException("codegraph impact scope mismatch");
            }

            _ = RequireNonNegativeInt(root, "nodeCount");
            _ = RequireNonNegativeInt(root, "edgeCount");
            JsonElement values = RequireArray(root, "affected", request.Bounds.ItemLimit);
            List<string> pathList = [];
            foreach (JsonElement value in values.EnumerateArray())
            {
                JsonElement item = RequireObject(value, "name", "kind", "filePath", "startLine");
                _ = RequireString(item, "name");
                _ = ParseSymbolKind(RequireString(item, "kind"));
                _ = RequirePositiveInt(item, "startLine");
                pathList.Add(RequireContainedRelativePath(item, "filePath"));
            }

            string[] paths = [.. pathList];
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

    private static void RequireMutationOutput(byte[] bytes, int byteLimit, bool allowEmpty)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
        {
            if (allowEmpty)
            {
                return;
            }

            throw new InvalidDataException("codegraph mutation output outside byte limit");
        }

        if (bytes.Length > byteLimit)
        {
            throw new InvalidDataException("codegraph mutation output outside byte limit");
        }

        string output = new UTF8Encoding(false, true).GetString(bytes).Trim();
        if (string.IsNullOrWhiteSpace(output) || output.Length > byteLimit ||
            output.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t'))
        {
            throw new InvalidDataException("codegraph mutation output malformed");
        }
    }

    private static ParsedStatus ParseStatus(
        byte[] bytes,
        int byteLimit,
        CodeGraphRepositorySnapshot snapshot)
    {
        using JsonDocument document = ParseJson(bytes, byteLimit);
        JsonElement root = RequireObject(
            document.RootElement,
            "initialized", "version", "projectPath", "indexPath", "lastIndexed",
            "fileCount", "nodeCount", "edgeCount", "dbSizeBytes", "backend", "journalMode",
            "nodesByKind", "languages", "pendingChanges", "worktreeMismatch", "index");
        bool initialized = RequireBoolean(root, "initialized");
        _ = RequireString(root, "version");
        RequireTrustedStatusPath(root, "projectPath", snapshot.RepositoryRoot);
        RequireTrustedStatusPath(root, "indexPath", Path.Combine(snapshot.RepositoryRoot, ".codegraph"));
        _ = RequireString(root, "lastIndexed");
        int fileCount = RequireNonNegativeInt(root, "fileCount");
        _ = RequireNonNegativeInt(root, "nodeCount");
        _ = RequireNonNegativeInt(root, "edgeCount");
        _ = RequireNonNegativeLong(root, "dbSizeBytes");
        _ = RequireString(root, "backend");
        _ = RequireString(root, "journalMode");
        RequireCountMap(root, "nodesByKind");
        RequireUniqueStringArray(root, "languages", 128);
        JsonElement pendingChanges = RequireObject(
            root.GetProperty("pendingChanges"), "added", "modified", "removed");
        bool hasPendingChanges = RequireNonNegativeInt(pendingChanges, "added") != 0 ||
            RequireNonNegativeInt(pendingChanges, "modified") != 0 ||
            RequireNonNegativeInt(pendingChanges, "removed") != 0;

        bool worktreeMismatch = RequireWorktreeMismatch(root.GetProperty("worktreeMismatch"));
        JsonElement index = RequireObject(
            root.GetProperty("index"),
            "builtWithVersion", "builtWithExtractionVersion", "currentExtractionVersion",
            "reindexRecommended", "state", "pendingRefs");
        _ = RequireString(index, "builtWithVersion");
        _ = RequireNonNegativeInt(index, "builtWithExtractionVersion");
        _ = RequireNonNegativeInt(index, "currentExtractionVersion");
        bool reindexRecommended = RequireBoolean(index, "reindexRecommended");
        string state = RequireString(index, "state");
        int pendingRefs = RequireNonNegativeInt(index, "pendingRefs");
        bool stale = hasPendingChanges || worktreeMismatch || reindexRecommended || pendingRefs != 0;
        return new ParsedStatus(initialized && state == "complete", stale, fileCount);
    }

    private static JsonElement RequireRootArray(JsonElement value, int itemLimit)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > itemLimit)
        {
            throw new InvalidDataException("codegraph result exceeds item limit");
        }

        return value;
    }

    private static bool RequireBoolean(JsonElement parent, string propertyName)
    {
        JsonElement value = parent.GetProperty(propertyName);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException("codegraph returned invalid boolean"),
        };
    }

    private static void RequireVisibility(JsonElement parent, string propertyName)
    {
        _ = RequireString(parent, propertyName) switch
        {
            "public" or "private" or "protected" or "internal" => true,
            _ => throw new InvalidDataException("codegraph returned unknown visibility"),
        };
    }

    private static void RequireFiniteNumber(JsonElement parent, string propertyName)
    {
        JsonElement value = parent.GetProperty(propertyName);
        if (!value.TryGetDouble(out double number) || !double.IsFinite(number))
        {
            throw new InvalidDataException("codegraph returned invalid number");
        }
    }

    private static void RequireCountMap(JsonElement parent, string propertyName)
    {
        JsonElement value = parent.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("codegraph returned invalid count map");
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!names.Add(property.Name) || string.IsNullOrWhiteSpace(property.Name) ||
                property.Name.Length > 128 || property.Name.Any(char.IsControl) ||
                !property.Value.TryGetInt32(out int count) || count < 0)
            {
                throw new InvalidDataException("codegraph returned invalid count map");
            }
        }
    }

    private static void RequireUniqueStringArray(
        JsonElement parent,
        string propertyName,
        int itemLimit)
    {
        JsonElement values = RequireArray(parent, propertyName, itemLimit);
        HashSet<string> observed = new(StringComparer.Ordinal);
        foreach (JsonElement value in values.EnumerateArray())
        {
            string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (string.IsNullOrWhiteSpace(text) || text.Length > 128 || text.Any(char.IsControl) ||
                !observed.Add(text))
            {
                throw new InvalidDataException("codegraph returned invalid string array");
            }
        }
    }

    private static bool RequireWorktreeMismatch(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        JsonElement mismatch = RequireObject(value, "worktreeRoot", "indexRoot");
        RequireAbsoluteBoundedPath(mismatch, "worktreeRoot");
        RequireAbsoluteBoundedPath(mismatch, "indexRoot");
        return true;
    }

    private static void RequireAbsoluteBoundedPath(JsonElement parent, string propertyName)
    {
        string path = RequireString(parent, propertyName);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidDataException("codegraph returned invalid absolute path");
        }

        _ = Path.GetFullPath(path);
    }

    private static void RequireTrustedStatusPath(
        JsonElement parent,
        string propertyName,
        string trustedPath)
    {
        string observed = RequireString(parent, propertyName);
        if (!Path.IsPathFullyQualified(observed) ||
            !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(observed), Path.GetFullPath(trustedPath)))
        {
            throw new InvalidDataException("codegraph status path mismatch");
        }
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
        "type" or "class" or "interface" or "enum" => AnalysisSymbolKind.Type,
        "method" or "function" => AnalysisSymbolKind.Method,
        "property" => AnalysisSymbolKind.Property,
        "field" => AnalysisSymbolKind.Field,
        "constructor" => AnalysisSymbolKind.Constructor,
        _ => throw new InvalidDataException("codegraph returned unknown symbol kind"),
    };

    private static string ToCodeGraphSymbolKind(AnalysisSymbolKind value) => value switch
    {
        AnalysisSymbolKind.Type => "class",
        AnalysisSymbolKind.Method => "method",
        AnalysisSymbolKind.Property => "property",
        AnalysisSymbolKind.Field => "field",
        AnalysisSymbolKind.Constructor => "constructor",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
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

    private sealed record ParsedStatus(bool Ready, bool Stale, int IndexedFileCount);
}
