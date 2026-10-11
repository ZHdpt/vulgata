using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.OctoberGenerator;

/// <summary>Borrowed qualified runtime tools with admission into the host's actual-input ledger.</summary>
public sealed class OctoberWorkerTools
{
    private readonly OctoberToolRuntime _runtime;
    private readonly OctoberWorkerInputLedger _ledger;
    private OctoberToolDiagnosticSnapshot? _firstFailure;
    internal OctoberToolDiagnosticSnapshot? FirstFailure => Volatile.Read(ref _firstFailure);

    public OctoberWorkerTools(OctoberToolRuntime runtime, OctoberWorkerInputLedger ledger)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        if (!runtime.HasPreparedBinding(ledger.Origin.RepositoryId, ledger.Origin.RootBindingIdentity, ledger.Origin.PreparedVersion))
            throw new ArgumentException("Worker tools require the exact prepared input binding.");
    }

    public async Task<IReadOnlyList<AIFunction>> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await _ledger.VerifyCurrentAsync(cancellationToken))
            throw new InvalidOperationException("Worker input authority is unavailable.");
        CodeGraphToolRegistration graph = await new CodeGraphFunctions(new BorrowedGraph(_runtime)).CreateAsync(cancellationToken);
        if (!graph.Readiness.IsReady)
            throw new InvalidOperationException("Required CodeGraph readiness is unavailable.");
        IReadOnlyList<AIFunction> source = new PreparedSourceFunctions(new BorrowedSource(_runtime)).Create();
        IReadOnlyList<AIFunction> java = new PreparedJavaNavigationFunctions(new BorrowedJava(_runtime)).Create();
        return source.Concat(java).Concat(graph.Functions).Select(function =>
            (AIFunction)new LedgerFunction(function, this)).ToArray();
    }

    private sealed class LedgerFunction(AIFunction function, OctoberWorkerTools owner) : AIFunction
    {
        private readonly OctoberWorkerInputLedger ledger = owner._ledger;
        private static readonly UTF8Encoding Utf8 = new(false, true);
        private readonly JsonElement _schema = WorkerSchema(function);
        public override string Name => function.Name;
        public override string Description => function.Description;
        public override JsonElement JsonSchema => _schema;
        public override JsonElement? ReturnJsonSchema => function.ReturnJsonSchema;

        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            SourceMetadataOperation operation = Name switch
            {
                "list_prepared_source" => SourceMetadataOperation.List,
                "read_prepared_source" => SourceMetadataOperation.ReadTextRange,
                "search_prepared_source" => SourceMetadataOperation.SearchText,
                "navigate_java_definition" => SourceMetadataOperation.JavaDefinition,
                "navigate_java_references" => SourceMetadataOperation.JavaReferences,
                "codegraph_explore" => SourceMetadataOperation.CodeGraphExplore,
                _ => throw new InvalidOperationException("The Worker tool is not declared.")
            };
            OctoberToolDiagnosticSnapshot? first = null;
            OctoberToolFailureBoundary boundary = OctoberToolFailureBoundary.SourceCurrentness;
            object? result = null;
            using var observation = owner._runtime.ObserveInvocation(operation, diagnostic =>
            {
                if (diagnostic.Operation == operation && Enum.IsDefined(diagnostic.Boundary))
                    Interlocked.CompareExchange(ref first, diagnostic, null);
            });
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await ledger.VerifyCurrentAsync(cancellationToken))
                    throw new InvalidOperationException("Worker input authority is unavailable.");
                boundary = OctoberToolFailureBoundary.ArgumentBinding;
                // Freeze exactly the closed invocation seen by the facade; a mutable caller
                // dictionary must not change between native use and provenance recording.
                string safeRequest;
                try
                {
                    // Serialization replaces invalid UTF-16. Reject it before freezing
                    // so the runtime never searches a silently substituted literal.
                    if (Name == "search_prepared_source" && arguments.TryGetValue("literal", out object? literal) &&
                        literal is string rawLiteral && !ValidSearchLiteral(rawLiteral))
                        return new { status = "InvalidArguments" };
                    safeRequest = JsonSerializer.Serialize(arguments);
                    if (Utf8.GetByteCount(safeRequest) > OctoberKnowledgeInputLimits.MaximumSafeRequestUtf8Bytes)
                        return new { status = "InvalidArguments" };
                }
                catch (Exception error) when (error is JsonException or NotSupportedException or EncoderFallbackException)
                { return new { status = "InvalidArguments" }; }
                using JsonDocument request = JsonDocument.Parse(safeRequest);
                if (!ValidArguments(Name, request.RootElement)) return new { status = "InvalidArguments" };
                Dictionary<string, object?> snapshot = JsonSerializer.Deserialize<Dictionary<string, object?>>(safeRequest)!;
                string scope = operation switch
                {
                    SourceMetadataOperation.CodeGraphExplore => ".",
                    SourceMetadataOperation.List or SourceMetadataOperation.SearchText =>
                        request.RootElement.GetProperty("relativeDirectory").GetString()!,
                    _ => request.RootElement.GetProperty("relativePath").GetString()!
                };
                if (scope.Length == 0) scope = ".";
                boundary = OctoberToolFailureBoundary.RuntimeInvoke;
                result = await function.InvokeAsync(new AIFunctionArguments(snapshot), cancellationToken)
                    ?? throw new InvalidOperationException("The Worker tool returned no typed result.");
                if (result is CodeGraphToolInvocationResult graph && graph.Status is not
                    (CodeGraphToolInvocationStatus.Complete or CodeGraphToolInvocationStatus.NoMatch or CodeGraphToolInvocationStatus.Limited))
                    throw new InvalidOperationException("Required CodeGraph invocation is unavailable.");
                boundary = OctoberToolFailureBoundary.ResultAdmission;
                OctoberWorkerInputAdmission admitted = await ledger.AdmitToolResultAsync(operation, scope,
                    safeRequest, result, cancellationToken);
                if (admitted.Status != DisclosureStatus.Allowed || admitted.Result is null)
                    throw new InvalidOperationException("The Worker tool input was withheld.");
                return admitted.Result;
            }
            catch
            {
                // Close the invocation before publishing its first failure. Late work
                // cannot replace this cause or populate another attempt's collector.
                observation.Dispose();
                var fallback = new OctoberToolDiagnosticSnapshot(operation, boundary,
                    JavaOutcome: result is PreparedJavaNavigationResult java && Enum.IsDefined(java.Outcome) ? java.Outcome : null,
                    SourceOutcome: result is PreparedSourceResult source && Enum.IsDefined(source.Outcome) ? source.Outcome : null,
                    CodeGraphStatus: result is CodeGraphToolInvocationResult graph && Enum.IsDefined(graph.Status) ? graph.Status : null);
                Interlocked.CompareExchange(ref first, fallback, null);
                Interlocked.CompareExchange(ref owner._firstFailure, first, null);
                throw;
            }
        }

        private static JsonElement WorkerSchema(AIFunction tool)
        {
            if (tool.Name is not ("list_prepared_source" or "search_prepared_source")) return tool.JsonSchema;
            JsonObject schema = JsonNode.Parse(tool.JsonSchema.GetRawText())!.AsObject();
            schema["properties"]!.AsObject().Remove("continuation");
            if (schema["required"] is JsonArray required)
                for (int index = required.Count - 1; index >= 0; index--)
                    if (required[index]?.GetValue<string>() == "continuation") required.RemoveAt(index);
            if (tool.Name == "search_prepared_source")
            {
                JsonObject properties = schema["properties"]!.AsObject();
                properties["relativeDirectory"]!["description"] =
                    "Canonical repository-relative directory with forward slashes; use the empty string for the root, never '.'. No empty, dot, parent, reserved-name or trailing-dot/space components.";
                properties["literal"]!["description"] =
                    "Nonempty single-line literal, valid UTF-8, at most 256 UTF-8 bytes. No CR or LF. Whitespace is significant.";
                properties["literal"]!["minLength"] = 1;
            }
            return JsonSerializer.SerializeToElement(schema);
        }

        private static bool ValidArguments(string name, JsonElement value) => name switch
        {
            "list_prepared_source" => Only(value, "relativeDirectory", "pageSize") &&
                Text(value, "relativeDirectory") && Integer(value, "pageSize", 1, PreparedSourceCallLimits.MaximumListEntries),
            "read_prepared_source" => Only(value, "relativePath", "byteOffset", "byteCount") &&
                Text(value, "relativePath") && Integer(value, "byteOffset", 0, long.MaxValue) && Integer(value, "byteCount", 1, PreparedSourceCallLimits.MaximumReadBytes),
            "search_prepared_source" => Only(value, "relativeDirectory", "literal", "pageSize") &&
                SearchArguments(value) && Integer(value, "pageSize", 1, PreparedSourceCallLimits.MaximumSearchMatches),
            "navigate_java_definition" or "navigate_java_references" => Only(value, "relativePath", "line", "character", "maximumLocations") &&
                Text(value, "relativePath") && Integer(value, "line", 1, int.MaxValue) && Integer(value, "character", 1, int.MaxValue) &&
                Integer(value, "maximumLocations", 1, 100),
            "codegraph_explore" => Only(value, "query", "maxFiles") && Query(value) &&
                (!value.TryGetProperty("maxFiles", out _) || Integer(value, "maxFiles", 1, 12)),
            _ => false
        };

        private static bool Only(JsonElement value, params string[] names) => value.ValueKind == JsonValueKind.Object &&
            value.EnumerateObject().All(property => names.Contains(property.Name, StringComparer.Ordinal));
        private static bool Text(JsonElement value, string name) => value.TryGetProperty(name, out var member) && member.ValueKind == JsonValueKind.String;
        private static bool SearchArguments(JsonElement value)
        {
            if (!Text(value, "relativeDirectory") || !Text(value, "literal")) return false;
            try
            {
                return CanonicalSearchDirectory(value.GetProperty("relativeDirectory").GetString()!) &&
                    ValidSearchLiteral(value.GetProperty("literal").GetString()!);
            }
            catch (InvalidOperationException) { return false; } // Invalid escaped UTF-16 in a JsonElement.
        }

        private static bool ValidSearchLiteral(string literal)
        {
            if (literal.Length == 0 || literal.Contains('\r') || literal.Contains('\n')) return false;
            try { return Utf8.GetByteCount(literal) <= PreparedSourceCallLimits.MaximumSearchLiteralUtf8Bytes; }
            catch (EncoderFallbackException) { return false; }
        }

        // Pure argument parity with PreparedSourceTraversal; authorization, owned
        // state, containment and lifetime remain the source reader's responsibility.
        private static bool CanonicalSearchDirectory(string path)
        {
            if (path.StartsWith('/') || path.EndsWith('/') || path.Contains('\\') || path.Contains(':')) return false;
            return path.Length == 0 || path.Split('/').All(CanonicalSearchComponent);
        }

        private static bool CanonicalSearchComponent(string component)
        {
            if (component.Length is 0 or > 255 || component is "." or ".." ||
                component.EndsWith('.') || component.EndsWith(' ') ||
                component.Any(character => character < ' ' || character is '<' or '>' or ':' or '"' or '\\' or '/' or '|' or '?' or '*'))
                return false;
            string baseName = component.Split('.')[0];
            if (baseName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                baseName.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) ||
                baseName.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase) ||
                baseName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                baseName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                baseName.Equals("NUL", StringComparison.OrdinalIgnoreCase)) return false;
            return !(baseName.Length == 4 &&
                (baseName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || baseName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                baseName[3] is (>= '1' and <= '9') or '\u00B9' or '\u00B2' or '\u00B3');
        }
        private static bool Integer(JsonElement value, string name, long minimum, long maximum) =>
            value.TryGetProperty(name, out var member) && member.ValueKind == JsonValueKind.Number &&
            member.TryGetInt64(out long integer) && integer >= minimum && integer <= maximum;
        private static bool Query(JsonElement value)
        {
            if (!Text(value, "query")) return false;
            string query = value.GetProperty("query").GetString()!;
            if (string.IsNullOrWhiteSpace(query)) return false;
            try { return Utf8.GetByteCount(query) <= 512; }
            catch (EncoderFallbackException) { return false; }
        }
    }

    // The generator owns the runtime and its processes. These adapters borrow it;
    // disposing a facade must never end another file's host-owned tool lifetime.
    private sealed class BorrowedSource(OctoberToolRuntime runtime) : IPreparedSourceReadContext
    {
        public Guid RepositoryId => runtime.RepositoryId;
        public PreparedSourceContextLifetime Lifetime => PreparedSourceContextLifetime.Open;
        public Task<PreparedSourceResult> ListAsync(PreparedSourceListRequest request, CancellationToken cancellationToken = default)
            => runtime.ListAsync(request, cancellationToken);
        public Task<PreparedSourceResult> ReadTextRangeAsync(PreparedSourceReadTextRangeRequest request, CancellationToken cancellationToken = default)
            => runtime.ReadTextRangeAsync(request, cancellationToken);
        public Task<PreparedSourceResult> SearchTextAsync(PreparedSourceSearchTextRequest request, CancellationToken cancellationToken = default)
            => runtime.SearchTextAsync(request, cancellationToken);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BorrowedJava(OctoberToolRuntime runtime) : IPreparedJavaNavigationContext
    {
        public Guid RepositoryId => runtime.RepositoryId;
        public Task<PreparedJavaNavigationResult> GetDefinitionAsync(PreparedJavaNavigationRequest request, CancellationToken cancellationToken = default)
            => runtime.NavigateAsync(PreparedJavaNavigationOperation.Definition, request, cancellationToken);
        public Task<PreparedJavaNavigationResult> GetReferencesAsync(PreparedJavaNavigationRequest request, CancellationToken cancellationToken = default)
            => runtime.NavigateAsync(PreparedJavaNavigationOperation.References, request, cancellationToken);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BorrowedGraph(OctoberToolRuntime runtime) : ICodeGraphWorkerToolSession
    {
        public Task<CodeGraphToolReadiness> DiscoverAsync(CancellationToken cancellationToken)
            => runtime.PrepareAsync(cancellationToken);
        public Task<CodeGraphToolInvocationResult> ExploreAsync(string query, int maxFiles, CancellationToken cancellationToken)
            => runtime.ExploreAsync(query, maxFiles, cancellationToken);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
