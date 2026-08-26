using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T030 behavioral contract for the closed Pass 2 Worker functions.
/// </summary>
public sealed class Pass2StrictSubmissionTests
{
    private static readonly Guid JobId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Fact]
    public void Pass2StrictDeclarations_ExposeExactlyRetrieveAndSubmitFunctions()
    {
        AIFunction retrieve = CreateRetrieveFunction();
        AIFunction submit = CreateSubmitFunction();
        Pass2StrictDeclarationSet declarations =
            Pass2StrictToolDeclarations.Create(retrieve, submit);

        using JsonDocument ordinary = JsonDocument.Parse(declarations.ProjectedUtf8);
        JsonElement[] tools = ordinary.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, tools.Length);
        Assert.Equal(
            ["retrieve_code_logic", "submit_business_logic"],
            tools.Select(ToolName));
        Assert.All(tools, tool =>
        {
            Assert.Equal("function", tool.GetProperty("type").GetString());
            Assert.True(tool.GetProperty("function").GetProperty("strict").GetBoolean());
            Assert.False(
                tool.GetProperty("function").GetProperty("parameters")
                    .GetProperty("additionalProperties").GetBoolean());
        });

        using JsonDocument submitOnly = JsonDocument.Parse(declarations.ProjectedSubmitOnlyUtf8);
        JsonElement[] terminalTools = submitOnly.RootElement.EnumerateArray().ToArray();
        Assert.Equal(1, terminalTools.Length);
        Assert.Equal("submit_business_logic", ToolName(terminalTools[0]));
    }

    [Fact]
    public async Task RetrieveCodeLogic_UsesBoundedSelectorsAndServerOwnedCallbackAsync()
    {
        RecordingRetrievalService service = new();
        AIFunction function = new PreparedRetrieveCodeLogicFunction(service).Create(JobId);
        Assert.Equal("retrieve_code_logic", function.Name);
        Assert.True(function.AdditionalProperties.TryGetValue("strict", out object? strict));
        Assert.True(strict is true);

        using JsonDocument schema = JsonDocument.Parse(function.JsonSchema.GetRawText());
        JsonElement root = schema.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.True(
            root.TryGetProperty("additionalProperties", out JsonElement additionalProperties),
            $"retrieve_code_logic must declare a closed schema; actual schema: {root.GetRawText()}");
        Assert.False(additionalProperties.GetBoolean());
        Assert.Equal(
            [
                "operation", "queryText", "opaqueHandles", "direction",
                "containedByteOffset", "containedByteLength",
            ],
            root.GetProperty("properties").EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [
                "operation", "queryText", "opaqueHandles", "direction",
                "containedByteOffset", "containedByteLength",
            ],
            root.GetProperty("required").EnumerateArray().Select(value => value.GetString()));

        JsonElement queryText = root.GetProperty("properties").GetProperty("queryText");
        Assert.Equal(2, queryText.GetProperty("anyOf").GetArrayLength());
        JsonElement opaqueHandles = root.GetProperty("properties").GetProperty("opaqueHandles");
        Assert.InRange(opaqueHandles.GetProperty("maxItems").GetInt32(), 1, 32);
        JsonElement offset = root.GetProperty("properties").GetProperty("containedByteOffset");
        Assert.True(offset.GetProperty("anyOf").EnumerateArray().Any(value =>
            value.TryGetProperty("minimum", out JsonElement minimum) && minimum.GetInt32() == 0));

        string[] forbiddenAuthorityFields =
        [
            "systemId", "repositoryId", "scanRunId", "profiledRevision",
            "authorizationScope", "provider", "endpoint", "flowGroup",
            "sourceRoot", "absolutePath", "sql", "graphQuery", "shellCommand",
        ];
        Assert.DoesNotContain(
            root.GetProperty("properties").EnumerateObject().Select(property => property.Name),
            name => forbiddenAuthorityFields.Contains(name, StringComparer.OrdinalIgnoreCase));

        _ = await function.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["operation"] = Pass2CodeLogicRetrievalOperation.Search,
                ["queryText"] = "bounded",
                ["opaqueHandles"] = Array.Empty<string>(),
                ["direction"] = null,
                ["containedByteOffset"] = null,
                ["containedByteLength"] = null,
            }),
            CancellationToken.None);
        Assert.Equal(1, service.Calls);
        Assert.Equal(JobId, service.LastJobId);
        Assert.NotNull(service.LastRequest);
        Assert.Equal(Pass2CodeLogicRetrievalOperation.Search, service.LastRequest!.Operation);
    }

    [Fact]
    public void SubmitBusinessLogic_UsesOneClosedDocumentArgumentWithoutEvidenceAuthority()
    {
        AIFunction function = CreateSubmitFunction();
        Assert.Equal("submit_business_logic", function.Name);
        Assert.True(function.AdditionalProperties.TryGetValue("strict", out object? strict));
        Assert.True(strict is true);

        using JsonDocument schema = JsonDocument.Parse(function.JsonSchema.GetRawText());
        JsonElement root = schema.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["document"], root.GetProperty("required").EnumerateArray()
            .Select(value => value.GetString()));
        Assert.Equal(["document"], root.GetProperty("properties").EnumerateObject()
            .Select(property => property.Name));

        string[] forbiddenAuthorityFields =
        [
            "systemId", "repositoryId", "scanRunId", "profiledRevision",
            "authorizationScope", "provider", "endpoint", "flowGroup",
            "sourceRoot", "absolutePath", "query", "operation", "opaqueHandles",
            "generatedFromDocumentVersionIds",
        ];
        Assert.DoesNotContain(
            root.GetProperty("properties").EnumerateObject()
                .Select(property => property.Name),
            name => forbiddenAuthorityFields.Contains(name, StringComparer.OrdinalIgnoreCase));

        JsonElement document = root.GetProperty("properties").GetProperty("document");
        Assert.False(document.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("business-logic/flow-v1", document.GetProperty("properties")
            .GetProperty("schemaIdentity").GetProperty("enum")[0].GetString());

        Assembly core = typeof(Pass2BusinessLogicSchemaContract).Assembly;
        string[] schemaResources = core.GetManifestResourceNames()
            .Where(name => name.EndsWith("business-logic-flow-v1.schema.json", StringComparison.Ordinal))
            .ToArray();
        Assert.Single(schemaResources);
        Assert.Equal(
            Pass2BusinessLogicSchemaContract.ResourceName,
            schemaResources[0]);
    }

    [Fact]
    public void SubmitBusinessLogic_LocalValidationParityTracksEmbeddedSchemaBoundsAndEnums()
    {
        Assembly core = typeof(Pass2BusinessLogicSchemaContract).Assembly;
        using Stream stream = core.GetManifestResourceStream(
            Pass2BusinessLogicSchemaContract.ResourceName)!;
        using JsonDocument schema = JsonDocument.Parse(stream);
        JsonElement document = schema.RootElement;
        Assert.False(document.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            [
                "schemaIdentity", "binding", "title", "summary", "participants", "flowSteps",
                "businessRules", "dataMovements", "failurePaths", "unresolvedStatements",
            ],
            document.GetProperty("required").EnumerateArray().Select(value => value.GetString()));

        JsonElement properties = document.GetProperty("properties");
        Assert.Equal(2, properties.GetProperty("participants").GetProperty("minItems").GetInt32());
        Assert.Equal(16, properties.GetProperty("participants").GetProperty("maxItems").GetInt32());
        Assert.Equal(1, properties.GetProperty("flowSteps").GetProperty("minItems").GetInt32());
        Assert.Equal(32, properties.GetProperty("flowSteps").GetProperty("maxItems").GetInt32());
        Assert.All(
            new[] { "businessRules", "dataMovements", "failurePaths", "unresolvedStatements" },
            name => Assert.Equal(32, properties.GetProperty(name).GetProperty("maxItems").GetInt32()));

        JsonElement definitions = document.GetProperty("$defs");
        JsonElement participant = definitions.GetProperty("participant");
        Assert.Equal(["Consumer", "Provider", "Supporting"],
            participant.GetProperty("properties").GetProperty("role")
                .GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        JsonElement handles = definitions.GetProperty("evidenceHandleRefs");
        Assert.True(handles.GetProperty("uniqueItems").GetBoolean());
        Assert.Equal(1, handles.GetProperty("minItems").GetInt32());
        Assert.Equal(32, handles.GetProperty("maxItems").GetInt32());

        JsonElement flowStep = definitions.GetProperty("flowStep");
        Assert.Equal(["Internal", "References"],
            flowStep.GetProperty("properties").GetProperty("relationshipKind")
                .GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        JsonElement boundedText = definitions.GetProperty("boundedText");
        Assert.Equal(1, boundedText.GetProperty("minLength").GetInt32());
        Assert.Equal(4096, boundedText.GetProperty("maxLength").GetInt32());
        JsonElement shortText = definitions.GetProperty("shortText");
        Assert.Equal(256, shortText.GetProperty("allOf")[1].GetProperty("maxLength").GetInt32());
    }

    [Fact]
    public void PreparedPass2Dispatch_RequiresServerClaimedJobAndAuthorityBinding()
    {
        Type requestType = typeof(PreparedPass2DispatchRequest);
        string[] requiredServerAuthority =
        [
            "Pass2JobExecutionId",
            "LeaseToken",
            "AttemptOrdinal",
            "FlowGroupId",
            "ExactResolutionDecisionId",
            "LogicalDocumentStableKey",
            "PlanFingerprint",
            "MembershipFingerprint",
            "PromptIdentity",
            "SubmissionSchemaIdentity",
            "ToolDeclarationIdentity",
        ];

        PropertyInfo[] properties = requestType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (string propertyName in requiredServerAuthority)
        {
            Assert.Contains(
                properties,
                property => string.Equals(property.Name, propertyName, StringComparison.Ordinal));
        }

        Assert.Contains(
            typeof(PreparedPass2WorkerDispatcher).GetConstructors(),
            constructor => constructor.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(IPass2BusinessLogicDocumentCommitter)));
    }

    [Fact]
    public void SubmitBusinessLogic_PreservesAllAcceptedResourceConstraints()
    {
        AIFunction function = CreateSubmitFunction();
        using JsonDocument schema = JsonDocument.Parse(function.JsonSchema.GetRawText());
        JsonElement document = schema.RootElement.GetProperty("properties")
            .GetProperty("document");
        JsonElement documentProperties = document.GetProperty("properties");

        Assert.Equal(
            [
                "schemaIdentity", "binding", "title", "summary", "participants",
                "flowSteps", "businessRules", "dataMovements", "failurePaths",
                "unresolvedStatements",
            ],
            document.GetProperty("required").EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Equal(2, documentProperties.GetProperty("participants")
            .GetProperty("minItems").GetInt32());
        Assert.Equal(16, documentProperties.GetProperty("participants")
            .GetProperty("maxItems").GetInt32());
        Assert.Equal(1, documentProperties.GetProperty("flowSteps")
            .GetProperty("minItems").GetInt32());
        Assert.Equal(32, documentProperties.GetProperty("flowSteps")
            .GetProperty("maxItems").GetInt32());

        JsonElement title = documentProperties.GetProperty("title");
        Assert.Equal(1, title.GetProperty("minLength").GetInt32());
        Assert.Equal(256, title.GetProperty("maxLength").GetInt32());

        JsonElement summaryEvidence = documentProperties.GetProperty("summary")
            .GetProperty("properties").GetProperty("evidenceHandleRefs");
        Assert.Equal(1, summaryEvidence.GetProperty("minItems").GetInt32());
        Assert.Equal(32, summaryEvidence.GetProperty("maxItems").GetInt32());
        Assert.True(summaryEvidence.GetProperty("uniqueItems").GetBoolean());
        Assert.Equal(
            ["business-logic/flow-v1"],
            documentProperties.GetProperty("schemaIdentity").GetProperty("enum")
                .EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public void PreparedPass2ProviderRequest_CarriesBoundedTranscriptAndRepairContext()
    {
        Type requestType = typeof(PreparedPass2ProviderRequest);
        PropertyInfo[] properties = requestType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Assert.Contains(
            properties,
            property => string.Equals(property.Name, "TranscriptUtf8", StringComparison.Ordinal));
        Assert.Contains(
            properties,
            property => string.Equals(property.Name, "ToolCallIds", StringComparison.Ordinal));
        Assert.Contains(
            properties,
            property => string.Equals(property.Name, "SanitizedToolResultsUtf8", StringComparison.Ordinal));
        Assert.Contains(
            properties,
            property => string.Equals(property.Name, "RepairContextUtf8", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PreparedPass2WorkerDispatcher_PropagatesClaimedAuthorityToRetrievalAndCommitterAsync()
    {
        Guid claimedJobId = Guid.Parse("61000000-0000-4000-8000-000000000006");
        PreparedPass2DispatchRequest request = CreateDispatchRequest(claimedJobId);
        DispatchRetrievalService retrieval = new();
        DispatchCommitter committer = new();
        DispatchTransport transport = new();
        var executor = new PreparedPass2JobExecutor(
            transport,
            new PreparedRetrieveCodeLogicFunction(retrieval),
            new PreparedSubmitBusinessLogicFunction());
        var dispatcher = new PreparedPass2WorkerDispatcher(executor, committer);

        PreparedPass2DispatchResult result = await dispatcher.DispatchAsync(request);

        Assert.True(result.Accepted, result.ReasonCode);
        Assert.Equal(claimedJobId, retrieval.LastJobId);
        Assert.NotEqual(request.Flow.FlowId, retrieval.LastJobId);
        Assert.NotNull(committer.Request);
        Assert.Equal(claimedJobId, committer.Request!.Pass2JobExecutionId);
        Assert.Equal(request.LeaseToken, committer.Request.LeaseToken);
        Assert.Equal(request.AttemptOrdinal, committer.Request.AttemptOrdinal);
        Assert.Equal(request.PromptIdentity, committer.Request.PromptIdentity);
        Assert.Equal(request.InitialEvidence, committer.Request.InitialEvidence);

        Assert.Equal(2, transport.Requests.Count);
        string packetJson = Encoding.UTF8.GetString(transport.Requests[0].PacketUtf8.Span)
            .Split("\nserverAuthority:", 2, StringSplitOptions.None)[0];
        using JsonDocument packet = JsonDocument.Parse(packetJson);
        Assert.Equal(claimedJobId, packet.RootElement.GetProperty("jobId").GetGuid());
        Assert.NotEqual(request.Flow.FlowId, packet.RootElement.GetProperty("jobId").GetGuid());
        Assert.Equal(request.FlowGroupId, packet.RootElement.GetProperty("flowGroupId").GetGuid());
        Assert.Equal(request.PlanFingerprint, packet.RootElement.GetProperty("planFingerprint").GetString());
        Assert.Equal(request.MembershipFingerprint, packet.RootElement.GetProperty("membershipFingerprint").GetString());
        Assert.Equal(request.ToolDeclarationIdentity, packet.RootElement.GetProperty("toolDeclarationIdentity").GetString());
    }

    private static AIFunction CreateRetrieveFunction() =>
        new PreparedRetrieveCodeLogicFunction(new RecordingRetrievalService()).Create(JobId);

    private static AIFunction CreateSubmitFunction() =>
        new PreparedSubmitBusinessLogicFunction().Create(
            static (candidate, _) => Task.FromResult(
                PreparedPass2CandidateDecision.Reject(
                    PreparedPass2SubmissionDisposition.InvalidShape)));

    private static string? ToolName(JsonElement tool) =>
        tool.GetProperty("function").GetProperty("name").GetString();

    private sealed class RecordingRetrievalService : IPass2CodeLogicRetrievalService
    {
        public int Calls { get; private set; }
        public Guid LastJobId { get; private set; }
        public Pass2CodeLogicRetrievalRequest? LastRequest { get; private set; }

        public Task<Pass2CodeLogicRetrievalResult> RetrieveAsync(
            Guid pass2JobExecutionId,
            Pass2CodeLogicRetrievalRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastJobId = pass2JobExecutionId;
            LastRequest = request;
            return Task.FromResult(Pass2CodeLogicRetrievalResult.NoResults());
        }
    }

    private static PreparedPass2DispatchRequest CreateDispatchRequest(Guid claimedJobId)
    {
        Guid systemId = Guid.Parse("71000000-0000-4000-8000-000000000007");
        Guid consumerRepositoryId = Guid.Parse("72000000-0000-4000-8000-000000000007");
        Guid providerRepositoryId = Guid.Parse("73000000-0000-4000-8000-000000000007");
        Guid relationshipId = Guid.Parse("74000000-0000-4000-8000-000000000007");
        Guid consumerJobId = Guid.Parse("75000000-0000-4000-8000-000000000007");
        Guid providerJobId = Guid.Parse("76000000-0000-4000-8000-000000000007");
        Guid consumerCodeUnitId = Guid.Parse("77000000-0000-4000-8000-000000000007");
        Guid providerCodeUnitId = Guid.Parse("78000000-0000-4000-8000-000000000007");
        Guid consumerLogicalDocumentId = Guid.Parse("79000000-0000-4000-8000-000000000007");
        Guid providerLogicalDocumentId = Guid.Parse("7a000000-0000-4000-8000-000000000007");
        Guid consumerDocumentVersionId = Guid.Parse("7b000000-0000-4000-8000-000000000007");
        Guid providerDocumentVersionId = Guid.Parse("7c000000-0000-4000-8000-000000000007");
        Guid consumerAnchorId = Guid.Parse("7d000000-0000-4000-8000-000000000007");
        Guid providerAnchorId = Guid.Parse("7e000000-0000-4000-8000-000000000007");
        Guid relationshipEvidenceId = Guid.Parse("7f000000-0000-4000-8000-000000000007");
        Guid pass2ExecutionId = Guid.Parse("80000000-0000-4000-8000-000000000008");

        PreparedPass2RuntimeRepositoryBinding consumer = new(
            "consumer", "consumer-git", consumerRepositoryId,
            Guid.Parse("81000000-0000-4000-8000-000000000008"),
            Guid.Parse("82000000-0000-4000-8000-000000000008"), "refs/tags/consumer",
            new('a', 40), new('a', 40));
        PreparedPass2RuntimeRepositoryBinding provider = new(
            "provider", "provider-git", providerRepositoryId,
            Guid.Parse("83000000-0000-4000-8000-000000000008"),
            Guid.Parse("84000000-0000-4000-8000-000000000008"), "refs/tags/provider",
            new('b', 40), new('b', 40));
        PreparedPass2RuntimeMember[] members =
        [
            new(1, consumerJobId, consumerCodeUnitId, consumerLogicalDocumentId,
                consumerRepositoryId, consumerDocumentVersionId, [consumerAnchorId]),
            new(2, providerJobId, providerCodeUnitId, providerLogicalDocumentId,
                providerRepositoryId, providerDocumentVersionId, [providerAnchorId]),
        ];
        PreparedPass2RuntimeBinding binding = PreparedPass2RuntimeBinding.Create(
            "1.0", "profile-v2", "1.0.0", new('c', 64), "occurrence-v2", systemId,
            consumer, provider, relationshipId, [relationshipEvidenceId], members,
            [
                new(1, consumerJobId, consumerCodeUnitId, "consumer", "src/consumer.cs#L1"),
                new(2, providerJobId, providerCodeUnitId, "provider", "src/provider.cs#L1"),
            ]);

        string revision = new('a', 40);
        List<Pass2BusinessLogicEvidenceBinding> evidence = [];
        foreach ((Guid documentVersionId, Guid repositoryId, Guid scanRunId, Guid anchorId, Pass2CodeLogicRetrievalItemKind kind) in
                 new[]
                 {
                     (consumerDocumentVersionId, consumerRepositoryId, consumer.ScanRunId, consumerAnchorId, Pass2CodeLogicRetrievalItemKind.Document),
                     (providerDocumentVersionId, providerRepositoryId, provider.ScanRunId, providerAnchorId, Pass2CodeLogicRetrievalItemKind.Document),
                 })
        {
            evidence.Add(new(
                Pass2InitialEvidenceHandle.Create(
                    claimedJobId, 1, kind, documentVersionId, repositoryId, scanRunId,
                    revision),
                documentVersionId, repositoryId, scanRunId, revision));
            evidence.Add(new(
                Pass2InitialEvidenceHandle.Create(
                    claimedJobId, 1, Pass2CodeLogicRetrievalItemKind.SourceAnchor,
                    documentVersionId, repositoryId, scanRunId, revision,
                    sourceAnchorId: anchorId),
                documentVersionId, repositoryId, scanRunId, revision,
                SourceAnchorId: anchorId));
        }

        evidence.Add(new(
            Pass2InitialEvidenceHandle.Create(
                claimedJobId, 1, Pass2CodeLogicRetrievalItemKind.Relationship,
                consumerDocumentVersionId, consumerRepositoryId, consumer.ScanRunId, revision,
                edgeId: relationshipId),
            consumerDocumentVersionId, consumerRepositoryId, consumer.ScanRunId, revision,
            EdgeId: relationshipId));

        return new(
            claimedJobId,
            Guid.Parse("85000000-0000-4000-8000-000000000008"),
            DateTimeOffset.UtcNow.AddMinutes(5),
            1,
            pass2ExecutionId,
            Guid.Parse("86000000-0000-4000-8000-000000000008"),
            Guid.Parse("87000000-0000-4000-8000-000000000008"),
            Guid.Parse("88000000-0000-4000-8000-000000000008"),
            new('d', 64), new('e', 64), "Http", new('f', 64), new('1', 64),
            "business-flow/v1/" + new string('2', 64), new('3', 64), new('4', 64),
            "pass2-business-logic-worker.v1", "business-logic/flow-v1",
            Pass2StrictToolDeclarations.Identity, evidence, binding.Flow, binding);
    }

    private sealed class DispatchRetrievalService : IPass2CodeLogicRetrievalService
    {
        public Guid LastJobId { get; private set; }

        public Task<Pass2CodeLogicRetrievalResult> RetrieveAsync(
            Guid pass2JobExecutionId,
            Pass2CodeLogicRetrievalRequest request,
            CancellationToken cancellationToken = default)
        {
            LastJobId = pass2JobExecutionId;
            return Task.FromResult(Pass2CodeLogicRetrievalResult.NoResults());
        }
    }

    private sealed class DispatchCommitter : IPass2BusinessLogicDocumentCommitter
    {
        public Pass2BusinessLogicCommitRequest? Request { get; private set; }

        public Task<Pass2BusinessLogicCommitResult> CommitAsync(
            Pass2BusinessLogicCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new Pass2BusinessLogicCommitResult(
                Pass2BusinessLogicCommitStatus.Committed,
                Guid.Parse("89000000-0000-4000-8000-000000000008"),
                request.InitialEvidence.Select(value => value.DocumentVersionId).Distinct().ToArray(),
                "committed"));
        }
    }

    private sealed class DispatchTransport : IPreparedPass2StreamingTransport
    {
        public List<PreparedPass2ProviderRequest> Requests { get; } = [];

        public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(
            PreparedPass2ProviderRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            string response = Requests.Count == 1
                ? "{\"toolCalls\":[{\"id\":\"retrieve-1\",\"name\":\"retrieve_code_logic\",\"arguments\":{\"operation\":\"Search\",\"queryText\":\"bounded\",\"opaqueHandles\":[],\"direction\":null,\"containedByteOffset\":null,\"containedByteLength\":null}}]}"
                : "{\"toolCalls\":[{\"id\":\"submit-1\",\"name\":\"submit_business_logic\",\"arguments\":{\"document\":{}}}]}";
            cancellationToken.ThrowIfCancellationRequested();
            yield return Encoding.UTF8.GetBytes(response);
            await Task.CompletedTask;
        }
    }
}
