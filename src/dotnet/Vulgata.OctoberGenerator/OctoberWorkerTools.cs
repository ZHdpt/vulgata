using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Json;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.OctoberGenerator;

/// <summary>Borrowed qualified runtime tools with admission into the host's actual-input ledger.</summary>
public sealed class OctoberWorkerTools
{
    private readonly OctoberToolRuntime _runtime;
    private readonly OctoberWorkerInputLedger _ledger;

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
            (AIFunction)new LedgerFunction(function, _ledger)).ToArray();
    }

    private sealed class LedgerFunction(AIFunction function, OctoberWorkerInputLedger ledger) : AIFunction
    {
        private static readonly UTF8Encoding Utf8 = new(false, true);
        public override string Name => function.Name;
        public override string Description => function.Description;
        public override JsonElement JsonSchema => function.JsonSchema;
        public override JsonElement? ReturnJsonSchema => function.ReturnJsonSchema;

        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await ledger.VerifyCurrentAsync(cancellationToken))
                throw new InvalidOperationException("Worker input authority is unavailable.");
            // Freeze exactly the closed invocation seen by the facade; a mutable caller
            // dictionary must not change between native use and provenance recording.
            string safeRequest = JsonSerializer.Serialize(arguments);
            if (Utf8.GetByteCount(safeRequest) > OctoberKnowledgeInputLimits.MaximumSafeRequestUtf8Bytes)
                throw new InvalidOperationException("The Worker tool request exceeds its bound.");
            Dictionary<string, object?> snapshot = JsonSerializer.Deserialize<Dictionary<string, object?>>(safeRequest)!;
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
            using JsonDocument request = JsonDocument.Parse(safeRequest);
            string scope = operation switch
            {
                SourceMetadataOperation.CodeGraphExplore => ".",
                SourceMetadataOperation.List or SourceMetadataOperation.SearchText =>
                    request.RootElement.GetProperty("relativeDirectory").GetString()!,
                _ => request.RootElement.GetProperty("relativePath").GetString()!
            };
            if (scope.Length == 0) scope = ".";
            object result = await function.InvokeAsync(new AIFunctionArguments(snapshot), cancellationToken)
                ?? throw new InvalidOperationException("The Worker tool returned no typed result.");
            OctoberWorkerInputAdmission admitted = await ledger.AdmitToolResultAsync(operation, scope,
                safeRequest, result, cancellationToken);
            if (admitted.Status != DisclosureStatus.Allowed || admitted.Result is null)
                throw new InvalidOperationException("The Worker tool input was withheld.");
            return admitted.Result;
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
