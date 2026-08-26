using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Creates the execution-local, server-bound Pass 2 retrieval function.
/// Repository scope and persistence authority never appear in its public
/// provider argument schema.
/// </summary>
public sealed class PreparedRetrieveCodeLogicFunction(
    IPass2CodeLogicRetrievalService retrievalService)
{
    private readonly IPass2CodeLogicRetrievalService _retrievalService =
        retrievalService ?? throw new ArgumentNullException(nameof(retrievalService));

    public AIFunction Create(Guid pass2JobExecutionId) =>
        CreateSession(pass2JobExecutionId).Function;

    public PreparedRetrieveCodeLogicSession CreateSession(Guid pass2JobExecutionId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(pass2JobExecutionId, Guid.Empty);
        return new(pass2JobExecutionId, _retrievalService);
    }
}

public sealed class PreparedRetrieveCodeLogicSession
{
    private readonly Guid _pass2JobExecutionId;
    private readonly IPass2CodeLogicRetrievalService _retrievalService;
    private readonly SemaphoreSlim _serialGate = new(1, 1);

    internal PreparedRetrieveCodeLogicSession(
        Guid pass2JobExecutionId,
        IPass2CodeLogicRetrievalService retrievalService)
    {
        _pass2JobExecutionId = pass2JobExecutionId;
        _retrievalService = retrievalService;
        Function = CreateFunction();
    }

    public AIFunction Function { get; }

    public bool IsBoundExceeded { get; private set; }

    public PreparedRetrieveCodeLogicResponse? LastResponse { get; private set; }

    private AIFunction CreateFunction()
    {
        async Task<PreparedRetrieveCodeLogicResponse> RetrieveAsync(
            Pass2CodeLogicRetrievalOperation operation,
            string? queryText,
            IReadOnlyList<string>? opaqueHandles,
            Pass2RelatedDocumentDirection? direction,
            int? containedByteOffset,
            int? containedByteLength,
            CancellationToken cancellationToken)
        {
            var request = new Pass2CodeLogicRetrievalRequest(
                operation,
                queryText,
                opaqueHandles,
                direction,
                containedByteOffset,
                containedByteLength);
            await _serialGate.WaitAsync(cancellationToken);
            try
            {
                Pass2CodeLogicRetrievalResult result =
                    await _retrievalService.RetrieveAsync(
                        _pass2JobExecutionId,
                        request,
                        cancellationToken);
                PreparedRetrieveCodeLogicResponse response =
                    PreparedRetrieveCodeLogicResponse.From(result);
                IsBoundExceeded |= string.Equals(
                    response.Code,
                    "retrieval-bound-exceeded",
                    StringComparison.Ordinal);
                LastResponse = response;
                return response;
            }
            finally
            {
                _serialGate.Release();
            }
        }

        Func<
            Pass2CodeLogicRetrievalOperation,
            string?,
            IReadOnlyList<string>?,
            Pass2RelatedDocumentDirection?,
            int?,
            int?,
            CancellationToken,
            Task<PreparedRetrieveCodeLogicResponse>> invoker = RetrieveAsync;
        AIFunction inner = AIFunctionFactory.Create(
            invoker,
            new AIFunctionFactoryOptions
            {
                Name = "retrieve_code_logic",
                Description = "Retrieve bounded authorized Code Logic evidence for this Pass 2 job.",
                AdditionalProperties = new Dictionary<string, object?>
                {
                    ["strict"] = true,
                },
                SerializerOptions = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions)
                {
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                },
            });
        return new ClosedRetrieveFunction(inner, CreateParametersSchema());
    }

    private static JsonElement CreateParametersSchema()
    {
        using var destination = new MemoryStream();
        using (var writer = new Utf8JsonWriter(destination))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "object");
            writer.WriteBoolean("additionalProperties", false);
            writer.WriteStartObject("properties");

            writer.WriteStartObject("operation");
            writer.WriteString("type", "string");
            writer.WriteStartArray("enum");
            foreach (string operation in Enum.GetNames<Pass2CodeLogicRetrievalOperation>())
            {
                writer.WriteStringValue(operation);
            }

            writer.WriteEndArray();
            writer.WriteString(
                "description",
                "Search uses queryText; Sections and SourceAnchors use opaqueHandles; " +
                "SourceExcerpt uses one handle and an optional contained byte range; " +
                "RelatedDocuments uses one handle and direction.");
            writer.WriteEndObject();

            WriteNullableString(
                writer,
                "queryText",
                maximumLength: 512,
                "Normalized Search text; null for every other operation.");

            writer.WriteStartObject("opaqueHandles");
            writer.WriteStartArray("type");
            writer.WriteStringValue("array");
            writer.WriteStringValue("null");
            writer.WriteEndArray();
            writer.WriteNumber("maxItems", Pass2RetrievalContracts.MaxResults);
            writer.WriteStartObject("items");
            writer.WriteString("type", "string");
            writer.WriteString("format", "uuid");
            writer.WriteString(
                "pattern",
                "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");
            writer.WriteEndObject();
            writer.WriteString(
                "description",
                "Empty for Search; one to ten current opaque handles for Sections or " +
                "SourceAnchors; exactly one for SourceExcerpt or RelatedDocuments.");
            writer.WriteEndObject();

            writer.WriteStartObject("direction");
            writer.WriteStartArray("anyOf");
            writer.WriteStartObject();
            writer.WriteString("type", "string");
            writer.WriteStartArray("enum");
            writer.WriteStringValue(nameof(Pass2RelatedDocumentDirection.Outgoing));
            writer.WriteStringValue(nameof(Pass2RelatedDocumentDirection.Incoming));
            writer.WriteEndArray();
            writer.WriteEndObject();
            WriteNullSchema(writer);
            writer.WriteEndArray();
            writer.WriteString(
                "description",
                "Required only for RelatedDocuments; null for every other operation.");
            writer.WriteEndObject();

            WriteNullableInteger(
                writer,
                "containedByteOffset",
                minimum: 0,
                maximum: int.MaxValue,
                "Non-negative SourceExcerpt offset within its Source Anchor; otherwise null.");
            WriteNullableInteger(
                writer,
                "containedByteLength",
                minimum: 1,
                maximum: Pass2RetrievalContracts.MaxReturnedUtf8Bytes,
                "Positive SourceExcerpt length within the remaining 65,536-byte job bound; " +
                "otherwise null.");

            writer.WriteEndObject();
            writer.WriteStartArray("required");
            writer.WriteStringValue("operation");
            writer.WriteStringValue("queryText");
            writer.WriteStringValue("opaqueHandles");
            writer.WriteStringValue("direction");
            writer.WriteStringValue("containedByteOffset");
            writer.WriteStringValue("containedByteLength");
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        using JsonDocument schema = JsonDocument.Parse(destination.ToArray());
        return schema.RootElement.Clone();
    }

    private static void WriteNullableString(
        Utf8JsonWriter writer,
        string name,
        int maximumLength,
        string description)
    {
        writer.WriteStartObject(name);
        writer.WriteStartArray("anyOf");
        writer.WriteStartObject();
        writer.WriteString("type", "string");
        writer.WriteNumber("minLength", 1);
        writer.WriteNumber("maxLength", maximumLength);
        writer.WriteEndObject();
        WriteNullSchema(writer);
        writer.WriteEndArray();
        writer.WriteString("description", description);
        writer.WriteEndObject();
    }

    private static void WriteNullableInteger(
        Utf8JsonWriter writer,
        string name,
        int minimum,
        int maximum,
        string description)
    {
        writer.WriteStartObject(name);
        writer.WriteStartArray("anyOf");
        writer.WriteStartObject();
        writer.WriteString("type", "integer");
        writer.WriteNumber("minimum", minimum);
        writer.WriteNumber("maximum", maximum);
        writer.WriteEndObject();
        WriteNullSchema(writer);
        writer.WriteEndArray();
        writer.WriteString("description", description);
        writer.WriteEndObject();
    }

    private static void WriteNullSchema(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "null");
        writer.WriteEndObject();
    }

    private sealed class ClosedRetrieveFunction(
        AIFunction inner,
        JsonElement jsonSchema) : AIFunction
    {
        public override string Name => inner.Name;
        public override string Description => inner.Description;
        public override IReadOnlyDictionary<string, object?> AdditionalProperties =>
            inner.AdditionalProperties;
        public override System.Reflection.MethodInfo? UnderlyingMethod =>
            inner.UnderlyingMethod;
        public override JsonSerializerOptions JsonSerializerOptions =>
            inner.JsonSerializerOptions;
        public override JsonElement JsonSchema { get; } = jsonSchema.Clone();
        public override JsonElement? ReturnJsonSchema => null;

        protected override ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken) =>
            inner.InvokeAsync(arguments, cancellationToken);
    }
}

public sealed record PreparedRetrieveCodeLogicResponse(
    string Outcome,
    string Code,
    Pass2CodeLogicRetrievalRemainingBudget RemainingBudget,
    IReadOnlyList<Pass2CodeLogicRetrievalProviderProjection> Items)
{
    internal static PreparedRetrieveCodeLogicResponse From(
        Pass2CodeLogicRetrievalResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new(
            result.Outcome.ToString(),
            result.Code,
            result.RemainingBudget,
            Array.AsReadOnly(result.Items
                .Select(Pass2CodeLogicRetrievalProviderCodec.Project)
                .ToArray()));
    }
}
