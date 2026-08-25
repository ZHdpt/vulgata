using System.Security.Cryptography;
using System.Reflection;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.PreparedScanning;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T062 RED: one real approved File Code Unit must survive the production
/// DeepSeek executor and the public dispatcher contract. The assertion exposes
/// only the bounded outcome and sanitized reason; provider content is never
/// persisted or printed.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class Pass1DeepSeekContractQualificationTests
{
    private const string AuthoritativeSchemaSha256 =
        "87C8FC21F261779A0D130F09ECCF574363B56E6D7138EA9D8933172E6CDAAC9A";
    private const string CommunicationConditionalSha256 =
        "DEA6F2555EAD89D35C1E9C0909F676B335F8648B07EA3069C4849785854FDDD9";
    private const string DeferredConditionalSha256 =
        "EBF64EBB6DDEC0B52D01CA493CE906165E69FF8CBCCA44E7AD3598517CE647A5";
    private const string ConfiguredDeepSeekEndpoint = "https://api.deepseek.com/beta";
    private const string ConfiguredDeepSeekModel = "deepseek-v4-flash";
    private const string TransportOnlySecret = "transport-only-secret";
    private const string ProjectionAlgorithmIdentity = "deepseek-strict-schema-projection-v1";
    private const string FixedSourceFreePacket =
        "{\"sourceFreeSyntheticPacket\":\"fixed\",\"binding\":\"source-free\"}";

    [Fact]
    public void SourceFreeHandshakeEvidence_ExposesSanitizedRawAndProjectedDeclarationIdentities()
    {
        PropertyInfo[] properties = typeof(DeepSeekSourceFreeHandshakeEvidence)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        string[] required =
        [
            "RawNavigateCodeSchemaSha256", "RawNavigateCodeSchemaBytes",
            "RawSubmitCodeSchemaSha256", "RawSubmitCodeSchemaBytes",
            "RawDeclarationsSha256", "RawDeclarationsBytes",
            "ProjectionAlgorithmIdentity", "LedgerIdentity",
            "ProjectedNavigateCodeSchemaSha256", "ProjectedNavigateCodeSchemaBytes",
            "ProjectedSubmitCodeSchemaSha256", "ProjectedSubmitCodeSchemaBytes",
            "ProjectedDeclarationsSha256", "ProjectedDeclarationsBytes",
        ];

        foreach (string name in required)
        {
            Assert.Contains(properties, property =>
                string.Equals(property.Name, name, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void AuthoritativeSchema_IsExactlyOneEmbeddedResource_AndMatchesIndependentRepositoryBytes()
    {
        byte[] repositoryBytes = LoadAuthoritativeSchema();
        Assert.Equal(AuthoritativeSchemaSha256, Sha256(repositoryBytes));

        Assembly assembly = typeof(PreparedPass1ResultParser).Assembly;
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith("code-logic-file-v2.schema.json", StringComparison.Ordinal))
            .ToArray();
        Assert.True(
            resources.Length == 1,
            $"The exact repository schema must be linked as one embedded resource; found {resources.Length}.");
        using Stream stream = assembly.GetManifestResourceStream(resources[0]) ??
            throw new Xunit.Sdk.XunitException("The authoritative embedded schema was not readable.");
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        Assert.Equal(repositoryBytes, copy.ToArray());
    }

    [Fact]
    public void Project_EmbeddedAuthoritativeSchema_ProducesClosedDocumentWithoutReferencesOrPartialConditionals()
    {
        object projection = InvokeProjector();
        string json = ProjectedSchemaJson(projection);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["document"], root.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Equal(["document"], root.GetProperty("properties").EnumerateObject().Select(value => value.Name).ToArray());
        Assert.DoesNotContain(EnumeratePropertyNames(root), name => name is "$ref" or "$def" or "$defs");
        Assert.DoesNotContain(EnumerateNodes(root), node =>
            node.ValueKind == JsonValueKind.Object &&
            node.TryGetProperty("anyOf", out JsonElement branches) && IsLedgerConditional(branches));
    }

    [Fact]
    public void ConditionalLedger_UsesExactPointersKindsOrderSemanticsAndIndependentHashes()
    {
        using JsonDocument schema = JsonDocument.Parse(LoadAuthoritativeSchema());
        JsonElement definitions = schema.RootElement.GetProperty("$defs");
        JsonElement communication = definitions.GetProperty("communicationEvidence").GetProperty("anyOf");
        JsonElement deferred = definitions.GetProperty("deferredObservation").GetProperty("anyOf");

        Assert.Equal(2, communication.GetArrayLength());
        Assert.Equal(2, deferred.GetArrayLength());
        Assert.Equal(
            ["classificationEvidence", "navigationReceiptIds"],
            communication.EnumerateArray().Select(branch => branch.GetProperty("properties").EnumerateObject().Single().Name).ToArray());
        Assert.Equal(
            ["targetSymbol", "targetRepositoryRelativePath"],
            deferred.EnumerateArray().Select(branch => branch.GetProperty("properties").EnumerateObject().Single().Name).ToArray());
        Assert.Equal(CommunicationConditionalSha256, Sha256(Minified(communication)));
        Assert.Equal(DeferredConditionalSha256, Sha256(Minified(deferred)));

        _ = InvokeProjector();
    }

    [Theory]
    [MemberData(nameof(SchemaDriftMutations))]
    public void Project_MutatedAuthoritativeSchema_FailsClosed(string mutation, Func<JsonObject, JsonObject> mutate)
    {
        byte[] mutated = JsonSerializer.SerializeToUtf8Bytes(mutate(ParseSchemaObject()));

        Exception exception = Assert.ThrowsAny<Exception>(() => InvokeProjector(mutated));
        Assert.IsNotType<Xunit.Sdk.XunitException>(exception);
        Assert.DoesNotContain("provider-secret", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object[]> SchemaDriftMutations()
    {
        yield return ["property", (Func<JsonObject, JsonObject>)(root => AddUnknownProperty(root))];
        yield return ["required", (Func<JsonObject, JsonObject>)(root => RemoveRequiredProperty(root))];
        yield return ["type", (Func<JsonObject, JsonObject>)(root => ChangeType(root))];
        yield return ["enum", (Func<JsonObject, JsonObject>)(root => ChangeEnum(root))];
        yield return ["pattern", (Func<JsonObject, JsonObject>)(root => ChangePattern(root))];
        yield return ["items", (Func<JsonObject, JsonObject>)(root => ChangeItems(root))];
        yield return ["numeric-bound", (Func<JsonObject, JsonObject>)(root => ChangeNumericBound(root))];
        yield return ["closed-object", (Func<JsonObject, JsonObject>)(root => OpenObject(root))];
        yield return ["reference", (Func<JsonObject, JsonObject>)(root => UnresolveReference(root))];
        yield return ["cyclic-reference", (Func<JsonObject, JsonObject>)(root => MakeCyclicReference(root))];
        yield return ["conflicting-reference", (Func<JsonObject, JsonObject>)(root => MakeConflictingReference(root))];
        yield return ["unsupported-keyword", (Func<JsonObject, JsonObject>)(root => AddUnsupportedKeyword(root))];
        yield return ["allowlist", (Func<JsonObject, JsonObject>)(root => ChangeAllowlistedKeyword(root))];
        yield return ["schema-identity", (Func<JsonObject, JsonObject>)(root => ChangeSchemaIdentity(root))];
        yield return ["vacuous-conditional", (Func<JsonObject, JsonObject>)(root => RemoveConditionalConstraint(root))];
        yield return ["partial-conditional", (Func<JsonObject, JsonObject>)(root => ChangeConditionalSemantic(root))];
        yield return ["conditional-move", (Func<JsonObject, JsonObject>)(root => MoveConditional(root))];
        yield return ["conditional-order", (Func<JsonObject, JsonObject>)(root => ReverseConditional(root))];
        yield return ["conditional-kind", (Func<JsonObject, JsonObject>)(root => ChangeConditionalKind(root))];
        yield return ["conditional-count", (Func<JsonObject, JsonObject>)(root => AddConditionalBranch(root))];
        yield return ["unclassified-conditional", (Func<JsonObject, JsonObject>)(root => AddUnclassifiedConditional(root))];
    }

    [Fact]
    public void Project_MutationCoverage_RequiresPublicSchemaBytesSeam()
    {
        Type projector = typeof(PreparedPass1ResultParser).Assembly.GetType(
            "Vulgata.Agents.WorkerExecution.DeepSeekStrictSchemaProjector",
            throwOnError: true)!;
        MethodInfo? method = projector.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(candidate =>
                candidate.Name == "Project" && candidate.GetParameters().Length == 1 &&
                IsSchemaInput(candidate.GetParameters()[0].ParameterType));

        Assert.True(
            method is not null,
            "T070 RED: mutation coverage requires a public Project schema-bytes seam; private reflection is not an accepted injection path.");
    }

    private static object InvokeProjector(byte[]? mutatedSchema = null)
    {
        Type? projector = typeof(PreparedPass1ResultParser).Assembly.GetType(
            "Vulgata.Agents.WorkerExecution.DeepSeekStrictSchemaProjector",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(projector is not null,
            "T071 RED: DeepSeekStrictSchemaProjector public seam is missing.");
        MethodInfo? method = mutatedSchema is null
            ? projector!.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .SingleOrDefault(candidate =>
                    (candidate.Name is "Build" or "Project") && candidate.GetParameters().Length == 0)
            : projector!.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .SingleOrDefault(candidate =>
                    candidate.Name == "Project" && candidate.GetParameters().Length == 1 &&
                    IsSchemaInput(candidate.GetParameters()[0].ParameterType));
        Assert.True(method is not null,
            mutatedSchema is null
                ? "T071 RED: the projector must expose one public parameterless embedded-resource seam."
                : "T070 RED: schema drift coverage requires one public Project schema-bytes seam; private reflection is not an accepted injection path.");
        object?[] arguments = mutatedSchema is null
            ? []
            : [ConvertSchemaInput(method!.GetParameters()[0].ParameterType, mutatedSchema)];
        return method!.Invoke(null, arguments) ?? throw new Xunit.Sdk.XunitException("Projector returned null.");
    }

    private static bool IsSchemaInput(Type type) =>
        type == typeof(byte[]) || type == typeof(ReadOnlyMemory<byte>) ||
        type == typeof(string) || type == typeof(JsonDocument) || type == typeof(JsonElement);

    private static object ConvertSchemaInput(Type type, byte[] bytes)
    {
        if (type == typeof(byte[])) return bytes;
        if (type == typeof(ReadOnlyMemory<byte>)) return new ReadOnlyMemory<byte>(bytes);
        if (type == typeof(string)) return Encoding.UTF8.GetString(bytes);
        if (type == typeof(JsonDocument)) return JsonDocument.Parse(bytes);
        if (type == typeof(JsonElement))
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            return document.RootElement.Clone();
        }

        throw new Xunit.Sdk.XunitException($"Unsupported schema mutation input type '{type.FullName}'.");
    }

    private static JsonObject ParseSchemaObject() => JsonNode.Parse(LoadAuthoritativeSchema())!.AsObject();

    private static JsonObject AddUnknownProperty(JsonObject root)
    {
        root["properties"]!.AsObject()["providerOnly"] = new JsonObject { ["type"] = "string" };
        return root;
    }

    private static JsonObject RemoveRequiredProperty(JsonObject root)
    {
        root["required"]!.AsArray().RemoveAt(0);
        return root;
    }

    private static JsonObject ChangeType(JsonObject root)
    {
        root["type"] = "array";
        return root;
    }

    private static JsonObject ChangeEnum(JsonObject root)
    {
        root["$defs"]!["internalUnit"]!["properties"]!["kind"]!["enum"]![0] = "Unexpected";
        return root;
    }

    private static JsonObject ChangePattern(JsonObject root)
    {
        root["$defs"]!["uuid"]!["pattern"] = "^provider-secret$";
        return root;
    }

    private static JsonObject ChangeItems(JsonObject root)
    {
        root["properties"]!["sourceAnchors"]!["items"] = new JsonObject { ["type"] = "string" };
        return root;
    }

    private static JsonObject ChangeNumericBound(JsonObject root)
    {
        root["$defs"]!["binding"]!["properties"]!["byteStart"]!["minimum"] = 1;
        return root;
    }

    private static JsonObject OpenObject(JsonObject root)
    {
        root["$defs"]!["binding"]!["additionalProperties"] = true;
        return root;
    }

    private static JsonObject UnresolveReference(JsonObject root)
    {
        root["properties"]!["binding"]!["$ref"] = "#/$defs/missing";
        return root;
    }

    private static JsonObject MakeCyclicReference(JsonObject root)
    {
        root["$defs"]!["uuid"] = new JsonObject { ["$ref"] = "#/$defs/uuid" };
        return root;
    }

    private static JsonObject MakeConflictingReference(JsonObject root)
    {
        root["properties"]!["binding"]!["$ref"] = "#/$defs/sourceAnchor";
        root["properties"]!["binding"]!["title"] = "conflicting-sibling";
        return root;
    }

    private static JsonObject AddUnsupportedKeyword(JsonObject root)
    {
        root["unevaluatedProperties"] = false;
        return root;
    }

    private static JsonObject ChangeAllowlistedKeyword(JsonObject root)
    {
        root["$defs"]!["boundedText"]!["minLength"] = 2;
        return root;
    }

    private static JsonObject ChangeSchemaIdentity(JsonObject root)
    {
        root["$id"] = "urn:vulgata:schema:code-logic:file-v3";
        return root;
    }

    private static JsonObject RemoveConditionalConstraint(JsonObject root)
    {
        JsonArray branches = root["$defs"]!["communicationEvidence"]!["anyOf"]!.AsArray();
        foreach (JsonNode? branch in branches)
        {
            JsonObject properties = branch!["properties"]!.AsObject();
            properties.First().Value!["minItems"] = null;
        }

        return root;
    }

    private static JsonObject ChangeConditionalSemantic(JsonObject root)
    {
        root["$defs"]!["deferredObservation"]!["anyOf"]![0]!["properties"]!["targetSymbol"]!["type"] = "integer";
        return root;
    }

    private static JsonObject MoveConditional(JsonObject root)
    {
        JsonNode conditional = root["$defs"]!["communicationEvidence"]!["anyOf"]!.DeepClone();
        root["$defs"]!["communicationEvidence"]!.AsObject().Remove("anyOf");
        root["anyOf"] = conditional;
        return root;
    }

    private static JsonObject ReverseConditional(JsonObject root)
    {
        JsonArray branches = root["$defs"]!["communicationEvidence"]!["anyOf"]!.AsArray();
        JsonNode first = branches[0]!.DeepClone();
        JsonNode second = branches[1]!.DeepClone();
        branches[0] = second;
        branches[1] = first;
        return root;
    }

    private static JsonObject ChangeConditionalKind(JsonObject root)
    {
        JsonNode conditional = root["$defs"]!["communicationEvidence"]!["anyOf"]!.DeepClone();
        root["$defs"]!["communicationEvidence"]!.AsObject().Remove("anyOf");
        root["$defs"]!["communicationEvidence"]!["allOf"] = conditional;
        return root;
    }

    private static JsonObject AddConditionalBranch(JsonObject root)
    {
        JsonArray branches = root["$defs"]!["deferredObservation"]!["anyOf"]!.AsArray();
        branches.Add(branches[0]!.DeepClone());
        return root;
    }

    private static JsonObject AddUnclassifiedConditional(JsonObject root)
    {
        root["$defs"]!["binding"]!["anyOf"] = new JsonArray(
            new JsonObject { ["type"] = "object" },
            new JsonObject { ["type"] = "null" });
        return root;
    }

    [Theory]
    [InlineData(false, false, true, false, false)]
    [InlineData(true, false, true, false, true)]
    [InlineData(false, true, true, false, true)]
    [InlineData(true, false, false, true, true)]
    [InlineData(true, false, false, false, false)]
    public void FullLocalValidator_RejectsBothEmptyConditionals_AndAcceptsValidOneSidedControls(
        bool classificationEvidence,
        bool navigationReceiptIds,
        bool deferredSymbol,
        bool deferredPath,
        bool expectedValid)
    {
        byte[] candidate = CreateConditionalCandidate(
            classificationEvidence,
            navigationReceiptIds,
            deferredSymbol,
            deferredPath);

        if (!expectedValid)
        {
            Assert.Throws<InvalidDataException>(() =>
                PreparedPass1ResultParser.Parse(CreateConditionalPacket(), candidate));
            return;
        }

        PreparedPass1ParsedWorkerResult parsed = PreparedPass1ResultParser.Parse(
            CreateConditionalPacket(),
            candidate);
        Assert.Equal("code-logic/file-v2", parsed.SchemaIdentity);
    }

    [Fact]
    public void Project_ProviderSchema_RemainsWithinThe65536ByteProjectionBound()
    {
        string json = ProjectedSchemaJson(InvokeProjector());
        Assert.InRange(Encoding.UTF8.GetByteCount(json), 1, 65_536);
    }

    private static readonly Guid ConditionalJobId =
        Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid ConditionalScanExecutionId =
        Guid.Parse("22222222-3333-4444-8555-666666666666");
    private static readonly Guid ConditionalSystemId =
        Guid.Parse("33333333-4444-4555-8666-777777777777");
    private static readonly Guid ConditionalRepositoryId =
        Guid.Parse("44444444-5555-4666-8777-888888888888");
    private static readonly Guid ConditionalScanRunId =
        Guid.Parse("55555555-6666-4777-8888-999999999999");
    private static readonly Guid ConditionalFileCodeUnitId =
        Guid.Parse("66666666-7777-4888-8999-aaaaaaaaaaaa");
    private static readonly Guid ConditionalManifestId =
        Guid.Parse("77777777-8888-4999-8aaa-bbbbbbbbbbbb");
    private static readonly Guid ConditionalContextId =
        Guid.Parse("88888888-9999-4aaa-8bbb-cccccccccccc");
    private static readonly Guid ConditionalClassificationId =
        Guid.Parse("99999999-aaaa-4bbb-8ccc-dddddddddddd");
    private static readonly Guid ConditionalReceiptId =
        Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff");
    private const string ConditionalRevision =
        "0123456789abcdef0123456789abcdef01234567";
    private const string ConditionalPath = "src/neutral/OrderPolicy.java";
    private const string ConditionalHash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static byte[] CreateConditionalCandidate(
        bool classificationEvidence,
        bool navigationReceiptIds,
        bool deferredSymbol,
        bool deferredPath)
    {
        byte[] source = Encoding.UTF8.GetBytes("package neutral;\nclass OrderPolicy { }\n");
        object[] classifications = classificationEvidence
            ? [new
            {
                classificationId = ConditionalClassificationId.ToString("D"),
                classificationFingerprint = ConditionalHash,
                communicationKind = "Http",
                level = "Confirmed",
                confidence = "High",
                evidenceRule = "HttpClientAnnotation",
                provenance = "StructuralAnnotation",
                catalogRuleIdentity = "catalog/http-client",
                evidenceLocator = $"{ConditionalPath}#L2",
            }]
            : [];
        string[] receiptIds = navigationReceiptIds
            ? [ConditionalReceiptId.ToString("D")]
            : [];

        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaIdentity = "code-logic/file-v2",
            binding = new
            {
                jobId = ConditionalJobId.ToString("D"),
                scanExecutionId = ConditionalScanExecutionId.ToString("D"),
                systemId = ConditionalSystemId.ToString("D"),
                repositoryId = ConditionalRepositoryId.ToString("D"),
                scanRunId = ConditionalScanRunId.ToString("D"),
                profiledRevision = ConditionalRevision,
                fileCodeUnitId = ConditionalFileCodeUnitId.ToString("D"),
                fileCodeUnitIdentityFingerprint = ConditionalHash,
                preparedScanManifestId = ConditionalManifestId.ToString("D"),
                preparedScanManifestFingerprint = ConditionalHash,
                pairPackageFingerprint = ConditionalHash,
                repositoryRelativePath = ConditionalPath,
                byteStart = 0,
                byteEnd = source.Length,
                lineStart = 1,
                lineEnd = 2,
                segmentOrdinal = 1,
                segmentCount = 1,
                fileHash = ConditionalHash,
                sourceHash = ConditionalHash,
                promptIdentity = "story-12-3-worker-v2",
                documentSchemaIdentity = "code-logic/file-v2",
                analysisRuntimeIdentityHash = ConditionalHash,
            },
            sourceAnchors = new[]
            {
                new
                {
                    key = "a001",
                    repositoryRelativePath = ConditionalPath,
                    byteStart = 0,
                    byteEnd = source.Length,
                    lineStart = 1,
                    lineEnd = 2,
                    sourceHash = ConditionalHash,
                    evidenceLocator = $"{ConditionalPath}#L1-L2",
                },
            },
            filePurpose = new
            {
                summary = "Orders the policy",
                sourceAnchorRefs = new[] { "a001" },
                responsibilities = new[]
                {
                    new { description = "Applies the policy", sourceAnchorRefs = new[] { "a001" } },
                },
            },
            internalUnits = Array.Empty<object>(),
            executionFlow = Array.Empty<object>(),
            dataFlow = Array.Empty<object>(),
            rules = Array.Empty<object>(),
            dataAccess = Array.Empty<object>(),
            communicationEvidence = new[]
            {
                new
                {
                    order = 1,
                    communicationKind = "Http",
                    observation = "Calls the policy endpoint",
                    classificationEvidence = classifications,
                    navigationReceiptIds = receiptIds,
                    sourceAnchorRefs = new[] { "a001" },
                },
            },
            knownReferences = Array.Empty<object>(),
            deferredObservations = new[]
            {
                new
                {
                    order = 1,
                    relationshipKind = "References",
                    outcome = "Ambiguous",
                    sourceClaimLocator = "OrderPolicy.resolve",
                    targetSymbol = deferredSymbol ? "neutral.Target" : null,
                    targetRepositoryRelativePath = deferredPath ? "src/neutral/Target.java" : null,
                    targetSourceHash = (string?)null,
                    navigationOperation = "Definition",
                    navigationDepth = 2,
                    navigationReceiptIds = new[] { ConditionalReceiptId.ToString("D") },
                    sourceAnchorRefs = new[] { "a001" },
                },
            },
            assumptions = new[]
            {
                new
                {
                    order = 1,
                    status = "Unresolved",
                    description = "The endpoint host is configured externally",
                    sourceAnchorRefs = Array.Empty<string>(),
                },
            },
        });
    }

    private static PreparedPass1WorkerPacket CreateConditionalPacket()
    {
        byte[] source = Encoding.UTF8.GetBytes("package neutral;\nclass OrderPolicy { }\n");
        PreparedPass1ContextSnapshotProjection context = new(
            ConditionalContextId,
            ConditionalRepositoryId,
            ConditionalScanRunId,
            ConditionalRevision,
            ConditionalHash,
            ConditionalHash,
            null,
            null,
            null);
        PreparedPass1CommunicationClassificationProjection classification = new(
            ConditionalFileCodeUnitId,
            ConditionalClassificationId,
            ConditionalManifestId,
            ConditionalRepositoryId,
            ConditionalScanRunId,
            ConditionalRevision,
            ConditionalHash,
            CommunicationKind.Http,
            ClassificationLevel.Confirmed,
            ConfidenceBand.High,
            EvidenceRule.HttpClientAnnotation,
            EvidenceProvenance.StructuralAnnotation,
            "catalog/http-client",
            ConditionalPath,
            $"{ConditionalPath}#L2");

        return new(
            Guid.Parse("dddddddd-eeee-4fff-8000-111111111111"),
            ConditionalJobId,
            ConditionalScanExecutionId,
            ConditionalSystemId,
            ConditionalRepositoryId,
            ConditionalScanRunId,
            ConditionalRevision,
            ConditionalFileCodeUnitId,
            ConditionalHash,
            ConditionalManifestId,
            ConditionalHash,
            ConditionalHash,
            ConditionalPath,
            0,
            source.Length,
            1,
            2,
            1,
            1,
            ConditionalHash,
            ConditionalHash,
            "story-12-3-worker-v2",
            "code-logic/file-v2",
            ConditionalHash,
            1,
            1,
            source,
            context,
            [classification]);
    }

    private static string ProjectedSchemaJson(object projection)
    {
        if (projection is string text)
        {
            return text;
        }

        if (projection is JsonDocument document)
        {
            return document.RootElement.GetRawText();
        }

        if (projection is JsonElement element)
        {
            return element.GetRawText();
        }

        foreach (string name in new[] { "ProjectedSchemaJson", "ProjectedSchema", "ParametersJson", "SchemaJson" })
        {
            PropertyInfo? property = projection.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property?.GetValue(projection) is string json)
            {
                return json;
            }
        }

        throw new Xunit.Sdk.XunitException("The projection result did not expose provider schema JSON.");
    }

    private static IEnumerable<JsonElement> EnumerateNodes(JsonElement element)
    {
        yield return element;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                foreach (JsonElement child in EnumerateNodes(property.Value)) yield return child;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                foreach (JsonElement nested in EnumerateNodes(child)) yield return nested;
            }
        }
    }

    private static IEnumerable<string> EnumeratePropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                yield return property.Name;
                foreach (string nested in EnumeratePropertyNames(property.Value)) yield return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                foreach (string nested in EnumeratePropertyNames(child)) yield return nested;
            }
        }
    }

    private static bool IsLedgerConditional(JsonElement branches)
    {
        if (branches.ValueKind != JsonValueKind.Array || branches.GetArrayLength() != 2) return false;
        string[] names = branches.EnumerateArray()
            .Where(branch => branch.ValueKind == JsonValueKind.Object && branch.TryGetProperty("properties", out _))
            .Select(branch => branch.GetProperty("properties").EnumerateObject().Single().Name)
            .ToArray();
        return names is ["classificationEvidence", "navigationReceiptIds"] or
            ["targetSymbol", "targetRepositoryRelativePath"];
    }

    private static byte[] LoadAuthoritativeSchema()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, "specs", "012-resumable-file-pass1-code-logic", "contracts", "code-logic-file-v2.schema.json");
            if (File.Exists(path)) return File.ReadAllBytes(path);
            directory = directory.Parent;
        }

        throw new Xunit.Sdk.XunitException("The independent repository schema could not be located.");
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Vulgata.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static byte[] Minified(JsonElement value)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream)) value.WriteTo(writer);
        return stream.ToArray();
    }

    [Fact]
    public void QualificationDiagnosticClassifier_UsesClosedCategoryWithoutEchoingInput()
    {
        const string marker = "provider-secret-arbitrary-marker";

        string category = ClassifyQualificationDiagnostic(
            new InvalidDataException($"JSON syntax rejected: {marker}"));

        Assert.Equal("json-syntax", category);
        Assert.DoesNotContain(marker, category, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("root contains an unknown or duplicate property.", "shape-root-unknown-or-duplicate")]
    [InlineData("binding omits a required property.", "shape-binding-missing-required")]
    [InlineData("filePurpose must be an object.", "shape-filePurpose-not-object")]
    [InlineData("internalUnit contains an unknown or duplicate property.", "shape-internalUnit-unknown-or-duplicate")]
    [InlineData("The authoritative result is not bounded valid JSON.", "json-syntax")]
    [InlineData("Prepared Pass 1 result contains an unknown or duplicate root property.", "shape-root-unknown-or-duplicate")]
    [InlineData("Prepared Pass 1 result omits a required root property.", "shape-root-missing-required")]
    public void QualificationDiagnosticClassifier_ReportsOnlyClosedShapeCategory(
        string diagnosticMessage,
        string expectedCategory)
    {
        string category = ClassifyQualificationDiagnostic(
            new InvalidDataException(diagnosticMessage));

        Assert.Equal(expectedCategory, category);
    }

    [Fact]
    public async Task SourceFreeHandshake_UsesConfiguredBetaRouteAndFlashModel_WithExactlyTwoStrictDeclarationsAsync()
    {
        using SourceFreeHandshakeTransport transport = new();
        DeepSeekSourceFreeHandshakeHarness harness = new(
            transport,
            new Uri(ConfiguredDeepSeekEndpoint),
            ConfiguredDeepSeekModel,
            TransportOnlySecret);

        var evidence = await harness.ExecuteAsync();

        Assert.Equal("Available", evidence.Outcome.ToString());
        Assert.Equal(ConfiguredDeepSeekEndpoint, evidence.Endpoint.ToString());
        Assert.Equal(ConfiguredDeepSeekModel, evidence.Model);
        Assert.True(evidence.Streaming);
        Assert.True(evidence.Strict);
        Assert.Equal(2, transport.Requests.Count);
        Assert.Equal(2, transport.RequestBodies.Count);
        Assert.All(
            transport.Requests,
            request => Assert.Equal(
                new Uri("https://api.deepseek.com/beta/chat/completions"),
                request.RequestUri));
        for (int index = 0; index < transport.RequestBodies.Count; index++)
        {
            using JsonDocument request = JsonDocument.Parse(transport.RequestBodies[index]);
            AssertSourceFreeHandshakeRequest(request, terminal: index == 1);
        }
    }

    [Fact]
    public async Task SourceFreeHandshake_HistoryBearingTerminalUsesProductionProjectionAndSanitizedEvidenceAsync()
    {
        using SourceFreeHandshakeTransport transport = new();
        DeepSeekSourceFreeHandshakeHarness harness = new(
            transport,
            new Uri(ConfiguredDeepSeekEndpoint),
            ConfiguredDeepSeekModel,
            TransportOnlySecret);

        DeepSeekSourceFreeHandshakeEvidence evidence = await harness.ExecuteAsync();

        Assert.Equal(DeepSeekSourceFreeHandshakeOutcome.Available, evidence.Outcome);
        Assert.Equal(2, transport.RequestBodies.Count);

        using JsonDocument ordinaryRequest = JsonDocument.Parse(transport.RequestBodies[0]);
        using JsonDocument terminalRequest = JsonDocument.Parse(transport.RequestBodies[1]);
        AssertSourceFreeHandshakeRequest(ordinaryRequest, terminal: false);
        AssertSourceFreeHandshakeRequest(terminalRequest, terminal: true);

        PreparedPass1TerminalContextProjectionResult expectedProjection =
            CreateExpectedSourceFreeTerminalProjection(
                new(
                    1,
                    PreparedPass1SubmissionRejectionKind.InvalidSemantics,
                    PreparedPass1SubmissionRepairCode.UseMinimalValidDocument));
        JsonElement[] terminalMessages = terminalRequest.RootElement
            .GetProperty("messages")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(3, terminalMessages.Length);
        Assert.Equal(
            ["system", "user", "user"],
            terminalMessages.Select(message => message.GetProperty("role").GetString()).ToArray());
        Assert.Equal(
            expectedProjection.TerminalInstruction,
            terminalMessages[0].GetProperty("content").GetString());
        Assert.Equal(
            FixedSourceFreePacket,
            terminalMessages[1].GetProperty("content").GetString());
        Assert.Equal(
            expectedProjection.ContextJson,
            terminalMessages[2].GetProperty("content").GetString());

        using JsonDocument context = JsonDocument.Parse(
            terminalMessages[2].GetProperty("content").GetString()!);
        Assert.Equal("pass1-terminal-context/1", context.RootElement
            .GetProperty("schemaIdentity").GetString());
        Assert.Single(context.RootElement.GetProperty("evidenceResults").EnumerateArray());
        Assert.Equal(JsonValueKind.Object, context.RootElement
            .GetProperty("evidenceResults")[0].GetProperty("result").ValueKind);
        JsonElement correction = context.RootElement.GetProperty("firstSubmissionCorrection");
        Assert.Equal(
            ["attempt", "category", "repairCode"],
            correction.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(1, correction.GetProperty("attempt").GetInt32());
        Assert.Equal("InvalidSemantics", correction.GetProperty("category").GetString());
        Assert.Equal(
            "UseMinimalValidDocument",
            correction.GetProperty("repairCode").GetString());

        string terminalWire = terminalRequest.RootElement.GetProperty("messages").GetRawText();
        Assert.DoesNotContain(
            ["assistant", "tool_calls", "tool_call_id", "function_call", "navigate_code", "call-history"],
            token => terminalWire.Contains(token, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(3, ReadIntEvidenceIfPresent(evidence, "TerminalMessageCount"));
        Assert.True(ReadBooleanEvidenceIfPresent(evidence, "TerminalResultPresent"));
        Assert.True(ReadBooleanEvidenceIfPresent(evidence, "TerminalHistoricalProtocolAbsent"));
        byte[] expectedPromptBytes = Encoding.UTF8.GetBytes(expectedProjection.TerminalInstruction);
        byte[] expectedContextBytes = Encoding.UTF8.GetBytes(expectedProjection.ContextJson);
        Assert.Equal(expectedPromptBytes.Length, ReadIntEvidenceIfPresent(evidence, "TerminalPromptBytes"));
        Assert.Equal(Sha256(expectedPromptBytes), ReadStringEvidenceIfPresent(evidence, "TerminalPromptSha256"));
        Assert.Equal(expectedContextBytes.Length, ReadIntEvidenceIfPresent(evidence, "TerminalContextBytes"));
        Assert.Equal(Sha256(expectedContextBytes), ReadStringEvidenceIfPresent(evidence, "TerminalContextSha256"));

        string sanitizedEvidence = JsonSerializer.Serialize(evidence);
        Assert.DoesNotContain(FixedSourceFreePacket, sanitizedEvidence, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderService.cs", sanitizedEvidence, StringComparison.Ordinal);
        Assert.DoesNotContain("call-history", sanitizedEvidence, StringComparison.Ordinal);
        Assert.DoesNotContain(
            ["candidate", "validator", "exception", "schema-fragment", "additional-source"],
            token => sanitizedEvidence.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SourceFreeHandshake_UsesSupportedDefaultsWithoutSamplingOrControlFieldsAsync()
    {
        using SourceFreeHandshakeTransport transport = new();
        DeepSeekSourceFreeHandshakeHarness harness = new(
            transport,
            new Uri(ConfiguredDeepSeekEndpoint),
            ConfiguredDeepSeekModel,
            TransportOnlySecret);

        DeepSeekSourceFreeHandshakeEvidence evidence = await harness.ExecuteAsync();

        Assert.Equal(DeepSeekSourceFreeHandshakeOutcome.Available, evidence.Outcome);
        Assert.Equal(ConfiguredDeepSeekModel, evidence.Model);
        Assert.True(evidence.Streaming);

        Assert.Equal(2, transport.RequestBodies.Count);
        for (int index = 0; index < transport.RequestBodies.Count; index++)
        {
            using JsonDocument request = JsonDocument.Parse(transport.RequestBodies[index]);
            AssertSourceFreeHandshakeRequest(request, terminal: index == 1);
        }
    }

    [Fact]
    public async Task SourceFreeHandshake_RecordsDisabledRequiredCallAndDoneEvidenceAsync()
    {
        int responseOrdinal = 0;
        using SourceFreeHandshakeTransport transport = new(
            () =>
            {
                responseOrdinal++;
                return CreateSourceFreeSseResponse(
                    includeCall: true,
                    includeDone: true,
                    functionName: responseOrdinal == 1 ? "navigate_code" : "submit_code_logic");
            });
        DeepSeekSourceFreeHandshakeHarness harness = new(
            transport,
            new Uri(ConfiguredDeepSeekEndpoint),
            ConfiguredDeepSeekModel,
            TransportOnlySecret);

        DeepSeekSourceFreeHandshakeEvidence evidence = await harness.ExecuteAsync();

        Assert.Equal(DeepSeekSourceFreeHandshakeOutcome.Available, evidence.Outcome);
        Assert.True(ReadBooleanEvidence(evidence, "ThinkingDisabled"));
        Assert.True(ReadBooleanEvidence(evidence, "ToolChoiceRequired"));
        Assert.True(ReadBooleanEvidence(evidence, "ToolCallObserved"));
        Assert.True(ReadBooleanEvidence(evidence, "DoneObserved"));
        Assert.Equal(2, transport.Requests.Count);
        Assert.Equal(2, transport.RequestBodies.Count);
    }

    [Fact]
    public async Task SourceFreeHandshake_RequiresTwoSanitizedShapesWithDistinctRequestEvidenceAsync()
    {
        int responseOrdinal = 0;
        using SourceFreeHandshakeTransport transport = new(() =>
        {
            responseOrdinal++;
            return CreateSourceFreeSseResponse(
                includeCall: true,
                includeDone: true,
                functionName: responseOrdinal == 1 ? "navigate_code" : "submit_code_logic");
        });
        DeepSeekSourceFreeHandshakeHarness harness = new(
            transport,
            new Uri(ConfiguredDeepSeekEndpoint),
            ConfiguredDeepSeekModel,
            TransportOnlySecret);

        DeepSeekSourceFreeHandshakeEvidence evidence = await harness.ExecuteAsync();

        Assert.Equal(2, transport.Requests.Count);
        Assert.Equal(2, transport.RequestBodies.Count);
        foreach (string body in transport.RequestBodies)
        {
            using JsonDocument request = JsonDocument.Parse(body);
            AssertSourceFreeHandshakeWire(request, TransportOnlySecret);
        }
        Assert.NotEqual(
            Sha256(Encoding.UTF8.GetBytes(transport.RequestBodies[0])),
            Sha256(Encoding.UTF8.GetBytes(transport.RequestBodies[1])));
        AssertSourceFreeChoice(transport.RequestBodies[0], terminal: false);
        AssertSourceFreeChoice(transport.RequestBodies[1], terminal: true);
        byte[] terminalDeclarations = Encoding.UTF8.GetBytes(
            JsonDocument.Parse(transport.RequestBodies[1])
                .RootElement.GetProperty("tools")
                .GetRawText());
        Assert.Equal(
            terminalDeclarations.Length,
            ReadIntEvidenceIfPresent(evidence, "TerminalDeclarationsBytes"));
        Assert.Equal(
            Sha256(terminalDeclarations),
            ReadStringEvidenceIfPresent(evidence, "TerminalDeclarationsSha256"));

        string[] requiredBooleanMembers =
        [
            "OrdinaryStreaming", "OrdinaryStrict", "OrdinaryThinkingDisabled",
            "OrdinaryToolChoiceRequired", "OrdinaryToolCallObserved", "OrdinaryDoneObserved",
            "TerminalStreaming", "TerminalStrict", "TerminalThinkingDisabled",
            "TerminalToolChoiceRequired", "TerminalToolCallObserved", "TerminalDoneObserved",
            "TerminalSelectedFunctionNameMatched",
        ];
        foreach (string memberName in requiredBooleanMembers)
        {
            Assert.True(ReadBooleanEvidenceIfPresent(evidence, memberName),
                $"T132 RED: source-free evidence is missing or false for '{memberName}'.");
        }

        string[] requiredIdentityMembers =
        [
            "OrdinaryRequestSha256", "OrdinaryRequestBytes",
            "TerminalRequestSha256", "TerminalRequestBytes",
            "TerminalDeclarationsSha256", "TerminalDeclarationsBytes",
        ];
        foreach (string memberName in requiredIdentityMembers)
        {
            Assert.Contains(
                evidence.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                member => string.Equals(member.Name, memberName, StringComparison.Ordinal));
        }

        string evidenceText = JsonSerializer.Serialize(evidence);
        Assert.DoesNotContain("source-free-call", evidenceText, StringComparison.Ordinal);
        Assert.DoesNotContain("tool_calls", evidenceText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("role\\\":\\\"tool", evidenceText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task SourceFreeHandshake_MissingCallOrDoneStopsWithConfigurationBeforeSourceBearingTrafficAsync(
        bool includeCall,
        bool includeDone)
    {
        using SourceFreeHandshakeTransport transport = new(
            () => CreateSourceFreeSseResponse(includeCall, includeDone));
        DeepSeekSourceFreeHandshakeHarness harness = new(
            transport,
            new Uri(ConfiguredDeepSeekEndpoint),
            ConfiguredDeepSeekModel,
            TransportOnlySecret);

        DeepSeekSourceFreeHandshakeEvidence evidence = await harness.ExecuteAsync();

        Assert.Equal(DeepSeekSourceFreeHandshakeOutcome.ProviderConfiguration, evidence.Outcome);
        Assert.Single(transport.Requests);
        Assert.Single(transport.RequestBodies);
    }

    [Fact]
    public async Task SourceFreeHandshake_ProjectsImmutableOpenAiAdapterSchemaBytesAsync()
    {
        using SourceFreeHandshakeTransport transport = new();
        DeepSeekSourceFreeHandshakeHarness harness = new(
            transport,
            new Uri(ConfiguredDeepSeekEndpoint),
            ConfiguredDeepSeekModel,
            TransportOnlySecret);

        DeepSeekSourceFreeHandshakeEvidence evidence = await harness.ExecuteAsync();
        Assert.Equal("Available", evidence.Outcome.ToString());
        Assert.Equal(2, transport.RequestBodies.Count);
        using (JsonDocument ordinaryRequest = JsonDocument.Parse(transport.RequestBodies[0]))
        using (JsonDocument terminalRequest = JsonDocument.Parse(transport.RequestBodies[1]))
        {
            AssertSourceFreeHandshakeRequest(ordinaryRequest, terminal: false);
            AssertSourceFreeHandshakeRequest(terminalRequest, terminal: true);
        }

        AIFunction navigateFunction = new PreparedNavigateCodeFunction(
                new AdapterSchemaNavigationService())
            .Create(new PreparedNavigateCodeBinding(
                Guid.Parse("11111111-1111-4111-8111-111111111111"),
                1,
                Guid.Parse("22222222-2222-4222-8222-222222222222")));
        ChatTool productionAdapterTool = navigateFunction.AsOpenAIChatTool();
        byte[] productionAdapterSchema = productionAdapterTool.FunctionParameters is null
            ? throw new Xunit.Sdk.XunitException(
                "The public OpenAI adapter omitted navigate_code parameters.")
            : productionAdapterTool.FunctionParameters.ToMemory().ToArray();
        using JsonDocument helperRequest = JsonDocument.Parse(transport.RequestBodies[0]);
        byte[] helperSchema = Encoding.UTF8.GetBytes(
            helperRequest.RootElement.GetProperty("tools")
                .EnumerateArray()
                .Single(tool => tool.GetProperty("function").GetProperty("name").GetString() == "navigate_code")
                .GetProperty("function")
                .GetProperty("parameters")
                .GetRawText());

        // The public adapter bytes are immutable raw inputs. The DeepSeek wire
        // must be a deterministic projection, not a second hand-authored
        // schema and not the rejected raw encoding.
        Assert.Equal(2_115, productionAdapterSchema.Length);
        Assert.Equal(
            "C8387178DA32FC682E7DE462AFA8B62AF388944CB0FC741B895375E585E87770",
            Sha256(productionAdapterSchema));
        Assert.Equal(evidence.NavigateCodeSchemaBytes, helperSchema.Length);
        Assert.Equal(
            evidence.NavigateCodeSchemaSha256,
            Sha256(helperSchema));
        Assert.NotEqual(productionAdapterSchema, helperSchema);
        Assert.DoesNotContain("\"type\":[\"object\",\"null\"]", Encoding.UTF8.GetString(helperSchema), StringComparison.Ordinal);
        Assert.Contains("\"anyOf\"", Encoding.UTF8.GetString(helperSchema), StringComparison.Ordinal);

        AIFunction submitFunction = new PreparedSubmitCodeLogicFunction()
            .CreateSession(CreateConditionalPacket())
            .Function;
        ChatTool productionSubmitTool = submitFunction.AsOpenAIChatTool();
        byte[] productionSubmitSchema = productionSubmitTool.FunctionParameters is null
            ? throw new Xunit.Sdk.XunitException(
                "The public OpenAI adapter omitted submit_code_logic parameters.")
            : productionSubmitTool.FunctionParameters.ToMemory().ToArray();
        byte[] helperSubmitSchema = Encoding.UTF8.GetBytes(
            helperRequest.RootElement.GetProperty("tools")
                .EnumerateArray()
                .Single(tool => tool.GetProperty("function").GetProperty("name").GetString() == "submit_code_logic")
                .GetProperty("function")
                .GetProperty("parameters")
                .GetRawText());

        Assert.Equal(27_502, productionSubmitSchema.Length);
        Assert.Equal(
            "77A044526E5D61480B3E86FA3C3AA354FF78A9F04BCA3B279F210DFBFB421BC8",
            Sha256(productionSubmitSchema));
        Assert.NotEqual(productionSubmitSchema, helperSubmitSchema);
        Assert.DoesNotContain("\"type\":[\"object\",\"null\"]", Encoding.UTF8.GetString(helperSubmitSchema), StringComparison.Ordinal);
        Assert.Contains("\"type\":\"string\"", Encoding.UTF8.GetString(helperSubmitSchema), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SourceFreeHandshake_RecordsOnlySanitizedHashesAndBytes_WithoutSourceBearingRequestFieldsAsync()
    {
        using SourceFreeHandshakeTransport transport = new();
        DeepSeekSourceFreeHandshakeHarness harness = new(
            transport,
            new Uri(ConfiguredDeepSeekEndpoint),
            ConfiguredDeepSeekModel,
            TransportOnlySecret);

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        var evidence = await harness.ExecuteAsync();
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        Assert.Equal(2, transport.RequestBodies.Count);
        byte[] requestBytes = Encoding.UTF8.GetBytes(transport.RequestBodies[0]);
        byte[] terminalRequestBytes = Encoding.UTF8.GetBytes(transport.RequestBodies[1]);
        using JsonDocument request = JsonDocument.Parse(requestBytes);
        using JsonDocument terminalRequest = JsonDocument.Parse(terminalRequestBytes);
        AssertSourceFreeHandshakeRequest(request, terminal: false);
        AssertSourceFreeHandshakeRequest(terminalRequest, terminal: true);
        byte[] declarations = Encoding.UTF8.GetBytes(request.RootElement.GetProperty("tools").GetRawText());

        Assert.Equal(ProjectionAlgorithmIdentity, evidence.ProjectionAlgorithmIdentity);
        Assert.False(string.IsNullOrWhiteSpace(evidence.ProjectionAlgorithmIdentity));
        Assert.Equal(TimeSpan.Zero, evidence.ObservedAt.Offset);
        Assert.InRange(evidence.ObservedAt, startedAt, completedAt);
        Assert.Equal(requestBytes.Length, evidence.RequestBytes);
        Assert.Equal(Sha256(requestBytes), evidence.RequestSha256);
        Assert.Equal(
            terminalRequestBytes.Length,
            ReadIntEvidenceIfPresent(evidence, "TerminalRequestBytes"));
        Assert.Equal(
            Sha256(terminalRequestBytes),
            ReadStringEvidenceIfPresent(evidence, "TerminalRequestSha256"));
        byte[] authoritativeSchema = LoadAuthoritativeSchema();
        Assert.Equal(authoritativeSchema.Length, evidence.AuthoritativeSchemaBytes);
        Assert.Equal(declarations.Length, evidence.DeclarationsBytes);
        Assert.Equal(Sha256(declarations), evidence.DeclarationsSha256);
        Assert.Equal(Sha256(authoritativeSchema), evidence.AuthoritativeSchemaSha256);
        Assert.Equal(AuthoritativeSchemaSha256, evidence.AuthoritativeSchemaSha256);
        byte[] projectedSchema = Encoding.UTF8.GetBytes(ProjectedSchemaJson(InvokeProjector()));
        Assert.Equal(projectedSchema.Length, evidence.ProjectedSchemaBytes);
        Assert.Equal(
            Sha256(projectedSchema),
            evidence.ProjectedSchemaSha256);
        JsonElement navigateTool = request.RootElement.GetProperty("tools")
            .EnumerateArray()
            .Single(tool => tool.GetProperty("function").GetProperty("name").GetString() == "navigate_code");
        byte[] navigateSchema = Encoding.UTF8.GetBytes(
            navigateTool.GetProperty("function").GetProperty("parameters").GetRawText());
        Assert.Equal(navigateSchema.Length, evidence.NavigateCodeSchemaBytes);
        Assert.Equal(Sha256(navigateSchema), evidence.NavigateCodeSchemaSha256);
        Assert.Matches("^[0-9A-F]{64}$", evidence.AuthoritativeSchemaSha256);
        Assert.Matches("^[0-9A-F]{64}$", evidence.NavigateCodeSchemaSha256);
        Assert.Matches("^[0-9A-F]{64}$", evidence.ProjectedSchemaSha256);
        Assert.Matches("^[0-9A-F]{64}$", evidence.DeclarationsSha256);
        Assert.Matches("^[0-9A-F]{64}$", evidence.RequestSha256);
        Assert.Matches("^[0-9A-F]{64}$", ReadStringEvidenceIfPresent(evidence, "TerminalRequestSha256"));

        AssertSourceFreeHandshakeWire(request, TransportOnlySecret);
        Assert.DoesNotContain(
            transport.Requests[0].Headers,
            header => header.Key.Contains("source", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Contains("repository", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Contains("scan", StringComparison.OrdinalIgnoreCase));

        string evidenceText = JsonSerializer.Serialize(evidence);
        Assert.DoesNotContain(TransportOnlySecret, evidenceText, StringComparison.Ordinal);
        Assert.DoesNotContain("reasoning", evidenceText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourceHash", evidenceText, StringComparison.OrdinalIgnoreCase);

        PropertyInfo[] evidenceProperties = typeof(DeepSeekSourceFreeHandshakeEvidence)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        string[] promptMetadataNames = ["TerminalPromptSha256", "TerminalPromptBytes"];
        string[] forbiddenEvidencePropertyNames =
        [
            "PromptResource", "PromptText", "PromptBody", "TerminalPromptContent",
            "TerminalContextJson", "TerminalRequestBody", "RequestBody", "Messages",
            "Packet", "Reasoning", "Arguments", "Results", "History",
            "RepositoryId", "SystemId", "ScanId", "JobId", "SourceHash",
        ];
        Assert.DoesNotContain(
            evidenceProperties,
            property => property.Name.Contains("Prompt", StringComparison.OrdinalIgnoreCase) &&
                !promptMetadataNames.Contains(property.Name, StringComparer.Ordinal));
        Assert.DoesNotContain(
            evidenceProperties,
            property => forbiddenEvidencePropertyNames.Contains(property.Name, StringComparer.Ordinal));

        foreach (string promptMetadataName in promptMetadataNames)
        {
            PropertyInfo metadata = Assert.Single(
                evidenceProperties.Where(property => property.Name == promptMetadataName));
            Assert.True(
                metadata.PropertyType == typeof(string) || metadata.PropertyType == typeof(int),
                $"Expected {promptMetadataName} to be a scalar hash/byte metadata property.");
        }
    }

    [T083SourceFreeHandshakeFact]
    public async Task SourceFreeHandshake_LiveBetaCapability_WritesSanitizedEvidenceAsync()
    {
        string apiKey = Environment.GetEnvironmentVariable(
            "VULGATA_T062_WORKER_PROVIDER_API_KEY")!;
        using var transport = new HttpClientHandler();
        DeepSeekSourceFreeHandshakeHarness harness = new(
            transport,
            new Uri(ConfiguredDeepSeekEndpoint),
            ConfiguredDeepSeekModel,
            apiKey);
        using var absoluteDeadline = new CancellationTokenSource(TimeSpan.FromMinutes(25));

        DeepSeekSourceFreeHandshakeEvidence evidence = await harness.ExecuteAsync(
            absoluteDeadline.Token);

        Assert.True(
            evidence.Outcome == DeepSeekSourceFreeHandshakeOutcome.Available,
            $"T083 source-free handshake stopped with sanitized outcome {evidence.Outcome}.");
        Assert.True(evidence.ThinkingDisabled);
        Assert.True(evidence.ToolChoiceRequired);
        Assert.True(evidence.ToolCallObserved);
        Assert.True(evidence.DoneObserved);
        foreach (string memberName in new[]
        {
            "OrdinaryStreaming", "OrdinaryStrict", "OrdinaryThinkingDisabled",
            "OrdinaryToolChoiceRequired", "OrdinaryToolCallObserved", "OrdinaryDoneObserved",
            "TerminalStreaming", "TerminalStrict", "TerminalThinkingDisabled",
            "TerminalToolChoiceRequired", "TerminalToolCallObserved", "TerminalDoneObserved",
            "TerminalSelectedFunctionNameMatched",
        })
        {
            Assert.True(ReadBooleanEvidenceIfPresent(evidence, memberName));
        }
        Assert.Matches("^[0-9A-F]{64}$", ReadStringEvidenceIfPresent(evidence, "OrdinaryRequestSha256"));
        Assert.Matches("^[0-9A-F]{64}$", ReadStringEvidenceIfPresent(evidence, "TerminalRequestSha256"));
        Assert.InRange(ReadIntEvidenceIfPresent(evidence, "OrdinaryRequestBytes"), 1, 17_416_192);
        Assert.InRange(ReadIntEvidenceIfPresent(evidence, "TerminalRequestBytes"), 1, 17_416_192);
        Assert.Equal(18, evidence.LedgerRowCount);
        Assert.Equal(evidence.ProjectedNavigateCodeSchemaSha256, evidence.NavigateCodeSchemaSha256);
        Assert.Equal(evidence.ProjectedNavigateCodeSchemaBytes, evidence.NavigateCodeSchemaBytes);
        Assert.Equal(evidence.ProjectedDeclarationsSha256, evidence.DeclarationsSha256);
        Assert.Equal(evidence.ProjectedDeclarationsBytes, evidence.DeclarationsBytes);
        Assert.All(
        [
            evidence.RawNavigateCodeSchemaSha256,
            evidence.RawSubmitCodeSchemaSha256,
            evidence.RawDeclarationsSha256,
            evidence.LedgerIdentity,
            evidence.ProjectedNavigateCodeSchemaSha256,
            evidence.ProjectedSubmitCodeSchemaSha256,
            evidence.ProjectedDeclarationsSha256,
            evidence.TerminalDeclarationsSha256,
            evidence.RequestSha256,
        ], value => Assert.Matches("^[0-9A-F]{64}$", value));
        Assert.All(
        [
            evidence.RawNavigateCodeSchemaBytes,
            evidence.RawSubmitCodeSchemaBytes,
            evidence.RawDeclarationsBytes,
            evidence.ProjectedNavigateCodeSchemaBytes,
            evidence.ProjectedSubmitCodeSchemaBytes,
            evidence.ProjectedDeclarationsBytes,
            evidence.TerminalDeclarationsBytes,
            evidence.RequestBytes,
        ], value => Assert.InRange(value, 1, 17_416_192));

        string repositoryRoot = FindRepositoryRoot();
        string artifactPath = Path.Combine(
            repositoryRoot,
            "artifacts",
            "qualification",
            "story-12.3",
            "deepseek-source-free-handshake.json");
        var artifact = new
        {
            schemaVersion = "story-12.3-deepseek-source-free-handshake/1.0",
            task = "T169",
            status = "pass",
            sourceCommit = "29459174348511bb813c4aa9672ccffee61d82a6",
            outcome = evidence.Outcome.ToString(),
            endpoint = evidence.Endpoint.AbsoluteUri.TrimEnd('/'),
            evidence.Model,
            evidence.ProjectionAlgorithmIdentity,
            evidence.RawNavigateCodeSchemaSha256,
            evidence.RawNavigateCodeSchemaBytes,
            evidence.RawSubmitCodeSchemaSha256,
            evidence.RawSubmitCodeSchemaBytes,
            evidence.RawDeclarationsSha256,
            evidence.RawDeclarationsBytes,
            evidence.LedgerIdentity,
            evidence.LedgerRowCount,
            evidence.ProjectedNavigateCodeSchemaSha256,
            evidence.ProjectedNavigateCodeSchemaBytes,
            evidence.ProjectedSubmitCodeSchemaSha256,
            evidence.ProjectedSubmitCodeSchemaBytes,
            evidence.ProjectedDeclarationsSha256,
            evidence.ProjectedDeclarationsBytes,
            evidence.TerminalDeclarationsSha256,
            evidence.TerminalDeclarationsBytes,
            evidence.AuthoritativeSchemaSha256,
            evidence.AuthoritativeSchemaBytes,
            evidence.ProjectedSchemaSha256,
            evidence.ProjectedSchemaBytes,
            evidence.NavigateCodeSchemaSha256,
            evidence.NavigateCodeSchemaBytes,
            evidence.DeclarationsSha256,
            evidence.DeclarationsBytes,
            evidence.RequestSha256,
            evidence.RequestBytes,
            ordinaryRequestSha256 = ReadStringEvidenceIfPresent(evidence, "OrdinaryRequestSha256"),
            ordinaryRequestBytes = ReadIntEvidenceIfPresent(evidence, "OrdinaryRequestBytes"),
            terminalRequestSha256 = ReadStringEvidenceIfPresent(evidence, "TerminalRequestSha256"),
            terminalRequestBytes = ReadIntEvidenceIfPresent(evidence, "TerminalRequestBytes"),
            evidence.Streaming,
            evidence.Strict,
            evidence.ThinkingDisabled,
            evidence.ToolChoiceRequired,
            evidence.ToolCallObserved,
            evidence.DoneObserved,
            ordinaryStreaming = ReadBooleanEvidenceIfPresent(evidence, "OrdinaryStreaming"),
            ordinaryStrict = ReadBooleanEvidenceIfPresent(evidence, "OrdinaryStrict"),
            ordinaryThinkingDisabled = ReadBooleanEvidenceIfPresent(evidence, "OrdinaryThinkingDisabled"),
            ordinaryToolChoiceRequired = ReadBooleanEvidenceIfPresent(evidence, "OrdinaryToolChoiceRequired"),
            ordinaryToolCallObserved = ReadBooleanEvidenceIfPresent(evidence, "OrdinaryToolCallObserved"),
            ordinaryDoneObserved = ReadBooleanEvidenceIfPresent(evidence, "OrdinaryDoneObserved"),
            terminalStreaming = ReadBooleanEvidenceIfPresent(evidence, "TerminalStreaming"),
            terminalStrict = ReadBooleanEvidenceIfPresent(evidence, "TerminalStrict"),
            terminalThinkingDisabled = ReadBooleanEvidenceIfPresent(evidence, "TerminalThinkingDisabled"),
            terminalToolChoiceRequired = ReadBooleanEvidenceIfPresent(evidence, "TerminalToolChoiceRequired"),
            terminalToolCallObserved = ReadBooleanEvidenceIfPresent(evidence, "TerminalToolCallObserved"),
            terminalDoneObserved = ReadBooleanEvidenceIfPresent(evidence, "TerminalDoneObserved"),
            terminalSelectedFunctionNameMatched = ReadBooleanEvidenceIfPresent(evidence, "TerminalSelectedFunctionNameMatched"),
            terminalMessageCount = ReadIntEvidenceIfPresent(evidence, "TerminalMessageCount"),
            terminalResultPresent = ReadBooleanEvidenceIfPresent(evidence, "TerminalResultPresent"),
            terminalHistoricalProtocolAbsent = ReadBooleanEvidenceIfPresent(evidence, "TerminalHistoricalProtocolAbsent"),
            terminalPromptSha256 = ReadStringEvidenceIfPresent(evidence, "TerminalPromptSha256"),
            terminalPromptBytes = ReadIntEvidenceIfPresent(evidence, "TerminalPromptBytes"),
            terminalContextSha256 = ReadStringEvidenceIfPresent(evidence, "TerminalContextSha256"),
            terminalContextBytes = ReadIntEvidenceIfPresent(evidence, "TerminalContextBytes"),
            observedAtUtc = evidence.ObservedAt,
            privacy = new
            {
                sourceDerivedPayloadSent = false,
                responseContentPersisted = false,
                credentialPersisted = false,
                requestContentPersisted = false,
            },
        };
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        File.WriteAllText(
            artifactPath,
            JsonSerializer.Serialize(artifact, new JsonSerializerOptions
            {
                WriteIndented = true,
            }) + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "ProviderConfiguration")]
    [InlineData(HttpStatusCode.BadRequest, "ProviderConfiguration")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "ProviderUnavailable")]
    public async Task SourceFreeHandshake_ProviderFailureStopsWithClosedOutcomeBeforeSourceTrafficAsync(
        HttpStatusCode statusCode,
        string expectedOutcome)
    {
        using SourceFreeHandshakeTransport transport = new(statusCode);
        DeepSeekSourceFreeHandshakeHarness harness = new(
            transport,
            new Uri(ConfiguredDeepSeekEndpoint),
            ConfiguredDeepSeekModel,
            TransportOnlySecret);

        var evidence = await harness.ExecuteAsync();

        Assert.Equal(expectedOutcome, evidence.Outcome.ToString());
        Assert.Single(transport.Requests);
        Assert.Single(transport.RequestBodies);
        using JsonDocument request = JsonDocument.Parse(transport.RequestBodies.Single());
        AssertSourceFreeHandshakeWire(request, TransportOnlySecret);
    }

    [Theory]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task SourceFreeHandshake_402And422RemainSourceFreeAndMapToConfigurationOutcomeAsync(
        HttpStatusCode statusCode)
    {
        using SourceFreeHandshakeTransport transport = new(statusCode);
        DeepSeekSourceFreeHandshakeHarness harness = new(
            transport,
            new Uri(ConfiguredDeepSeekEndpoint),
            ConfiguredDeepSeekModel,
            TransportOnlySecret);

        DeepSeekSourceFreeHandshakeEvidence evidence = await harness.ExecuteAsync();

        Assert.Equal(DeepSeekSourceFreeHandshakeOutcome.ProviderConfiguration, evidence.Outcome);
        Assert.Single(transport.Requests);
        Assert.Single(transport.RequestBodies);
        using JsonDocument request = JsonDocument.Parse(transport.RequestBodies.Single());
        AssertSourceFreeHandshakeWire(request, TransportOnlySecret);
        string evidenceText = JsonSerializer.Serialize(evidence);
        Assert.DoesNotContain(TransportOnlySecret, evidenceText, StringComparison.Ordinal);
        Assert.DoesNotContain("reasoning", evidenceText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestBody", evidenceText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("responseBody", evidenceText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SourceFreeHandshake_NetworkFailureStopsWithClosedUnavailableOutcomeBeforeSourceTrafficAsync()
    {
        using SourceFreeHandshakeTransport transport = new(() =>
            throw new HttpRequestException("synthetic transport failure"));
        DeepSeekSourceFreeHandshakeHarness harness = new(
            transport,
            new Uri(ConfiguredDeepSeekEndpoint),
            ConfiguredDeepSeekModel,
            TransportOnlySecret);

        var evidence = await harness.ExecuteAsync();

        Assert.Equal("ProviderUnavailable", evidence.Outcome.ToString());
        Assert.Single(transport.Requests);
        Assert.Single(transport.RequestBodies);
    }

    [Theory]
    [InlineData("http://api.deepseek.com/beta", "deepseek-v4-flash")]
    [InlineData("https://api.deepseek.com/beta", "deepseek-v3")]
    public async Task SourceFreeHandshake_InvalidEndpointOrModelConfigurationStopsBeforeAnyRequestAsync(
        string endpoint,
        string model)
    {
        using SourceFreeHandshakeTransport transport = new();
        DeepSeekSourceFreeHandshakeHarness harness = new(
            transport,
            new Uri(endpoint),
            model,
            TransportOnlySecret);

        var evidence = await harness.ExecuteAsync();

        Assert.Equal("ProviderConfiguration", evidence.Outcome.ToString());
        Assert.Empty(transport.Requests);
        Assert.Empty(transport.RequestBodies);
    }

    private static void AssertSourceFreeHandshakeWire(
        JsonDocument request,
        string transportOnlySecret)
    {
        JsonElement root = request.RootElement;
        JsonElement[] messageValues = root.GetProperty("messages")
            .EnumerateArray()
            .ToArray();
        string nonSchemaFields = string.Join(
            "\n",
            root.EnumerateObject()
                .Where(property => property.Name is not ("tools" or "messages"))
                .Select(property => property.Value.GetRawText()));

        string[] forbiddenIdentityValues =
        [
            "repository", "scan", "job", "fileCodeUnit", "navigationEvidence",
            "promptPacket", "systemPrompt", transportOnlySecret,
        ];
        Assert.DoesNotContain(
            forbiddenIdentityValues,
            token => nonSchemaFields.Contains(token, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            ["sourceHash", "source", "path"],
            token => nonSchemaFields.Contains(token, StringComparison.OrdinalIgnoreCase));

        string expectedContext = CreateExpectedSourceFreeTerminalProjection(
            new(
                1,
                PreparedPass1SubmissionRejectionKind.InvalidSemantics,
                PreparedPass1SubmissionRepairCode.UseMinimalValidDocument)).ContextJson;
        foreach (JsonElement message in messageValues)
        {
            string role = message.GetProperty("role").GetString()!;
            string content = message.GetProperty("content").GetString()!;
            Assert.DoesNotContain(transportOnlySecret, content, StringComparison.Ordinal);
            Assert.DoesNotContain(
                ["tool_calls", "tool_call_id", "function_call", "call-history"],
                token => content.Contains(token, StringComparison.OrdinalIgnoreCase));
            if (role == "user" &&
                content is not FixedSourceFreePacket &&
                content != expectedContext)
            {
                Assert.DoesNotContain(
                    forbiddenIdentityValues,
                    token => content.Contains(token, StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain("sourceHash", content, StringComparison.OrdinalIgnoreCase);
            }
        }

        string wire = root.GetRawText();
        Assert.DoesNotContain(
            ["tool_calls", "tool_call_id", "function_call", "\"role\":\"tool\""],
            token => wire.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static PreparedPass1TerminalContextProjectionResult
        CreateExpectedSourceFreeTerminalProjection(
            PreparedPass1TerminalContextCorrection? correction = null)
    {
        const string promptResourceName =
            "Vulgata.Agents.Prompts.pass1-code-logic-worker.v2.md";
        Assembly assembly = typeof(PreparedPass1TerminalContextProjection).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(promptResourceName)
            ?? throw new Xunit.Sdk.XunitException(
                "The source-free projection prompt resource was not readable.");
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: false);
        string promptResource = reader.ReadToEnd();
        PreparedPass1NavigationResponse fixedResult = new(
            PreparedPass1NavigationOutcome.DocumentEvidence,
            Guid.Parse("10000000-0000-4000-8000-000000000001"),
            PreparedPass1NavigationOperation.Definition,
            1,
            PreparedPass1NavigationBoundKind.Calls,
            1,
            32,
            [
                new PreparedPass1NavigationResult(
                    "src/OrderService.cs",
                    Guid.Parse("20000000-0000-4000-8000-000000000002"),
                    new byte[32],
                    [Guid.Parse("30000000-0000-4000-8000-000000000003")],
                    [Guid.Parse("40000000-0000-4000-8000-000000000004")],
                    "Process",
                    new PreparedPass1LineRange(10, 12),
                    new PreparedPass1ByteRange(100, 140),
                    "src/OrderService.cs:10-12"),
            ]);

        return PreparedPass1TerminalContextProjection.Create(
            new(
                promptResource,
                "source-free-history-bearing-prompt/v1",
                Encoding.UTF8.GetBytes(FixedSourceFreePacket),
                10,
                [PreparedPass1TerminalContextResult.FromNavigationResponse(1, fixedResult)],
                correction));
    }

    private static void AssertSourceFreeChoice(string body, bool terminal)
    {
        using JsonDocument request = JsonDocument.Parse(body);
        JsonElement choice = request.RootElement.GetProperty("tool_choice");
        if (!terminal)
        {
            Assert.Equal(JsonValueKind.String, choice.ValueKind);
            Assert.Equal("required", choice.GetString());
        }
        else
        {
            Assert.Equal(JsonValueKind.Object, choice.ValueKind);
            Assert.Equal("function", choice.GetProperty("type").GetString());
            Assert.Equal("submit_code_logic", choice.GetProperty("function").GetProperty("name").GetString());
            Assert.Equal(2, choice.EnumerateObject().Count());
        }

        JsonElement[] tools = request.RootElement.GetProperty("tools").EnumerateArray().ToArray();
        if (!terminal)
        {
            Assert.Equal(2, tools.Length);
            Assert.Equal(
                ["navigate_code", "submit_code_logic"],
                tools.Select(tool => tool.GetProperty("function").GetProperty("name").GetString())
                    .ToArray());
        }
        else
        {
            Assert.Single(tools);
            Assert.Equal(
                "submit_code_logic",
                tools[0].GetProperty("function").GetProperty("name").GetString());
        }
        Assert.All(tools, tool =>
        {
            JsonElement function = tool.GetProperty("function");
            Assert.True(function.GetProperty("strict").GetBoolean());
        });
    }

    private static void AssertSourceFreeHandshakeRequest(
        JsonDocument request,
        bool terminal)
    {
        JsonElement root = request.RootElement;
        Assert.Equal(ConfiguredDeepSeekModel, root.GetProperty("model").GetString());
        Assert.Equal(16, root.GetProperty("max_tokens").GetInt32());
        Assert.True(root.GetProperty("stream").GetBoolean());
        Assert.Equal("disabled", root.GetProperty("thinking").GetProperty("type").GetString());
        Assert.False(root.TryGetProperty("parallel_tool_calls", out _));
        Assert.False(root.TryGetProperty("response_format", out _));
        foreach (string propertyName in new[]
        {
            "reasoning_effort",
            "temperature",
            "top_p",
            "presence_penalty",
            "frequency_penalty",
        })
        {
            Assert.False(
                root.TryGetProperty(propertyName, out _),
                $"The supported-default handshake must omit '{propertyName}'.");
        }

        AssertSourceFreeChoice(root.GetRawText(), terminal);
        using JsonDocument body = JsonDocument.Parse(root.GetRawText());
        AssertSourceFreeHandshakeWire(body, TransportOnlySecret);
        string wire = root.GetRawText();
        Assert.DoesNotContain("zero-retention", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("training guarantee", wire, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ReadBooleanEvidence(
        DeepSeekSourceFreeHandshakeEvidence evidence,
        string memberName)
    {
        PropertyInfo? property = evidence.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo? field = evidence.GetType().GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object? value = property?.GetValue(evidence) ?? field?.GetValue(evidence);
        Assert.NotNull(value);
        Assert.IsType<bool>(value);
        return (bool)value!;
    }

    private static bool ReadBooleanEvidenceIfPresent(
        DeepSeekSourceFreeHandshakeEvidence evidence,
        string memberName)
    {
        PropertyInfo? property = evidence.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo? field = evidence.GetType().GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object? value = property?.GetValue(evidence) ?? field?.GetValue(evidence);
        Assert.NotNull(value);
        Assert.IsType<bool>(value);
        return (bool)value!;
    }

    private static int ReadIntEvidenceIfPresent(
        DeepSeekSourceFreeHandshakeEvidence evidence,
        string memberName)
    {
        PropertyInfo? property = evidence.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo? field = evidence.GetType().GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object? value = property?.GetValue(evidence) ?? field?.GetValue(evidence);
        Assert.NotNull(value);
        Assert.IsType<int>(value);
        return (int)value!;
    }

    private static string ReadStringEvidenceIfPresent(
        DeepSeekSourceFreeHandshakeEvidence evidence,
        string memberName)
    {
        PropertyInfo? property = evidence.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo? field = evidence.GetType().GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object? value = property?.GetValue(evidence) ?? field?.GetValue(evidence);
        Assert.NotNull(value);
        return Assert.IsType<string>(value);
    }

    private static HttpResponseMessage CreateSourceFreeSseResponse(
        bool includeCall,
        bool includeDone,
        string functionName = "navigate_code")
    {
        string call = includeCall
            ? $"data: {{\"id\":\"source-free-call\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"tool_calls\":[{{\"index\":0,\"id\":\"call-source-free\",\"type\":\"function\",\"function\":{{\"name\":\"{functionName}\",\"arguments\":\"{{}}\"}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\n"
            : "data: {\"id\":\"source-free-empty\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n";
        string done = includeDone ? "data: [DONE]\n\n" : string.Empty;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(call + done, Encoding.UTF8, "text/event-stream"),
        };
    }

    [T062HostQualificationFact]
    public async Task ApprovedFileCodeUnit_DeepSeekResponse_IsSchemaValidAtDispatcherSeamAsync()
    {
        Pass1ProductionExecutionTests.RequireRealHostQualificationPrerequisites();
        T062QualificationInputs qualification =
            await Pass1ProductionExecutionTests.LoadQualificationInputsAsync();
        await using var host = Pass1ProductionExecutionTests.CreateProductionHost(
            qualification.ConnectionString).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddScoped<QualificationResultCapture>();
                services.AddScoped<IPreparedPass1JobExecutor>(serviceProvider =>
                    new CapturingPreparedPass1JobExecutor(
                        ActivatorUtilities.CreateInstance<PreparedPass1JobExecutor>(serviceProvider),
                        serviceProvider.GetRequiredService<QualificationResultCapture>()));
            }));
        await Pass1ProductionExecutionTests.ConfigureWorkerProviderAsync(
            host,
            qualification.SystemId,
            qualification.ProviderEndpoint,
            qualification.ProviderModel);
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        Pass1PairExecution pair = await context.Pass1PairExecutions
            .AsNoTracking()
            .SingleAsync(value => value.Phase == Pass1PairExecutionPhase.FailedPass1);
        Guid[] manifestIds = [pair.ManifestOneId, pair.ManifestTwoId];
        Guid[] failedFileCodeUnitIds = await context.ScanJobExecutions
            .AsNoTracking()
            .Where(value => value.State == ScanJobExecutionState.Failed)
            .Select(value => value.FileCodeUnitId)
            .ToArrayAsync();
        PreparedScanManifestEntry entry = await context.PreparedScanManifestEntries
            .AsNoTracking()
            .Where(value => manifestIds.Contains(value.PreparedScanManifestId))
            .OrderBy(value => failedFileCodeUnitIds.Contains(value.FileCodeUnitId) ? 0 : 1)
            .ThenBy(value => value.PairSelectionOrdinal)
            .FirstAsync();
        PreparedScanManifest manifest = await context.PreparedScanManifests
            .AsNoTracking()
            .SingleAsync(value => value.Id == entry.PreparedScanManifestId);
        CodeUnit unit = await context.CodeUnits
            .AsNoTracking()
            .SingleAsync(value => value.Id == entry.FileCodeUnitId);
        ScanJobExecution job = await context.ScanJobExecutions
            .AsNoTracking()
            .SingleAsync(value => value.FileCodeUnitId == unit.Id);
        ScanExecution execution = await context.ScanExecutions
            .AsNoTracking()
            .SingleAsync(value => value.Id == job.ScanExecutionId);
        ScanContextSnapshot snapshot = await context.ScanContextSnapshots
            .AsNoTracking()
            .SingleAsync(value => value.Id == manifest.ContextSnapshotId);

        T062RepositoryQualification repository = qualification.ForRepository(unit.RepositoryId);
        string fullPath = Path.Combine(
            repository.SourceRoot,
            unit.FilePath.Replace('/', Path.DirectorySeparatorChar));
        byte[] fileBytes = await File.ReadAllBytesAsync(fullPath);
        byte[] source = fileBytes[(int)unit.ByteStart!.Value..(int)unit.ByteEndExclusive!.Value];
        string sourceHash = Convert.ToHexStringLower(SHA256.HashData(source));
        Assert.Equal(unit.SourceHash, sourceHash);

        PreparedPass1ContextSnapshotProjection contextProjection = new(
            snapshot.Id,
            snapshot.RepositoryId,
            snapshot.ScanRunId,
            snapshot.ProfiledRevision,
            snapshot.PreparedScanContextIdentityHash!,
            snapshot.ContentHash,
            snapshot.GlobalContext,
            snapshot.SystemContext,
            snapshot.RepositoryContext);
        PreparedPass1ServerAuthorization authorization = new(
            pair.Id,
            Math.Max(1, job.StateVersion),
            Math.Max(0, pair.StateVersion),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(5),
            nameof(Pass1PairExecutionPhase.Running),
            Pass1CircuitState.Closed,
            ScanJobExecutionState.Running,
            PreparedPass1AuthorizationBinding.Create(new(
                Guid.NewGuid(),
                pair.SystemId,
                manifestIds.Select(value => context.PreparedScanManifests
                    .Where(item => item.Id == value)
                    .Select(item => item.RepositoryId)
                    .Single()).ToArray())));
        PreparedPass1FileCodeUnitAssignment assignment = new(
            pair.Id,
            job.Id,
            execution.Id,
            execution.SystemId,
            unit.RepositoryId,
            unit.ScanRunId,
            execution.ProfiledRevision,
            unit.Id,
            unit.FileCodeUnitIdentityFingerprint!,
            manifest.Id,
            manifest.Fingerprint,
            pair.PairPackageFingerprint,
            unit.FilePath,
            unit.ByteStart.Value,
            unit.ByteEndExclusive.Value,
            unit.LineStart,
            unit.LineEnd,
            unit.SegmentOrdinal!.Value,
            unit.SegmentCount!.Value,
            unit.FileHash!,
            sourceHash,
            execution.PromptIdentity!,
            execution.DocumentSchemaIdentity!,
            execution.AnalysisRuntimeIdentityHash!,
            snapshot.Id,
            entry.ManifestOrdinal,
            entry.PairSelectionOrdinal,
            source)
        {
            ServerAuthorization = authorization,
        };

        IPreparedPass1WorkerDispatcher dispatcher = scope.ServiceProvider
            .GetRequiredService<IPreparedPass1WorkerDispatcher>();
        PreparedPass1DispatchResult dispatch = await dispatcher.DispatchAsync(
            new PreparedPass1DispatchRequest(
                [assignment],
                [contextProjection],
                [],
                1));
        PreparedPass1JobDispatchResult result = Assert.Single(dispatch.Results);
        QualificationResultCapture capture = scope.ServiceProvider
            .GetRequiredService<QualificationResultCapture>();
        byte[] finalResult = capture.TakeFinalResult();
        string diagnosticCategory;
        try
        {
            if (finalResult.Length == 0)
            {
                diagnosticCategory = "executor-empty-result";
            }
            else if (finalResult.Length > CodeLogicFileV2Contract.MaximumUtf8Bytes)
            {
                diagnosticCategory = "executor-size-rejection";
            }
            else
            {
                try
                {
                    _ = PreparedPass1ResultParser.Parse(
                        Assert.Single(dispatch.Packets),
                        finalResult);
                    diagnosticCategory = "accepted";
                }
                catch (Exception exception) when (
                    exception is InvalidDataException or ArgumentException or
                    System.Text.Json.JsonException or OverflowException)
                {
                    diagnosticCategory = ClassifyQualificationDiagnostic(exception);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(finalResult);
            capture.Clear();
        }

        Assert.True(
            result.Outcome == PreparedPass1RawWorkerOutcome.Completed,
            $"T062 RED: DeepSeek returned a non-schema result at the dispatcher seam; " +
            $"outcome={result.Outcome}; reason={result.ReasonCode ?? "none"}; " +
            $"provider={qualification.ProviderModel}; sourceKind={Path.GetExtension(unit.FilePath)}; " +
            $"diagnostic={diagnosticCategory}.");
        Assert.NotNull(result.ParsedResult);
        Assert.Equal("code-logic/file-v2", result.ParsedResult!.SchemaIdentity);
    }

    private static string ClassifyQualificationDiagnostic(Exception exception)
    {
        string message = exception.Message;
        if (exception is System.Text.Json.JsonException)
        {
            return message.Contains("depth", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("maximum", StringComparison.OrdinalIgnoreCase)
                ? "json-depth"
                : "json-syntax";
        }

        if (exception is OverflowException)
        {
            return "semantic-range";
        }

        if (exception is ArgumentException)
        {
            return "argument-contract";
        }

        if (exception is InvalidDataException)
        {
            if (message.Equals(
                "The authoritative result is not bounded valid JSON.",
                StringComparison.Ordinal))
            {
                return exception.InnerException?.Message.Contains(
                    "depth",
                    StringComparison.OrdinalIgnoreCase) == true
                    ? "json-depth"
                    : "json-syntax";
            }

            if (message.Equals(
                "Prepared Pass 1 result exceeds its bounded JSON shape.",
                StringComparison.Ordinal))
            {
                return "result-bound";
            }

            if (message.Equals(
                "Prepared Pass 1 result contains an unknown or duplicate root property.",
                StringComparison.Ordinal))
            {
                return "shape-root-unknown-or-duplicate";
            }

            if (message.Equals(
                "Prepared Pass 1 result omits a required root property.",
                StringComparison.Ordinal))
            {
                return "shape-root-missing-required";
            }

            foreach (string shape in new[]
            {
                "root", "binding", "sourceAnchor", "filePurpose", "claim",
                "internalUnit", "orderedClaim", "dataFlow", "rule", "dataAccess",
                "communicationEvidence", "classificationEvidence", "knownReference",
                "deferredObservation", "assumption",
            })
            {
                if (message.Equals(
                    $"{shape} contains an unknown or duplicate property.",
                    StringComparison.Ordinal))
                {
                    return $"shape-{shape}-unknown-or-duplicate";
                }

                if (message.Equals(
                    $"{shape} omits a required property.",
                    StringComparison.Ordinal))
                {
                    return $"shape-{shape}-missing-required";
                }

                if (message.Equals($"{shape} must be an object.", StringComparison.Ordinal))
                {
                    return $"shape-{shape}-not-object";
                }
            }

            if (message.Contains("binding", StringComparison.OrdinalIgnoreCase))
            {
                return "binding-mismatch";
            }

            if (message.Contains("Source Anchor", StringComparison.Ordinal) ||
                message.Contains("range", StringComparison.OrdinalIgnoreCase))
            {
                return "source-anchor-range";
            }

            if (message.Contains("filePurpose", StringComparison.Ordinal))
            {
                return "filePurpose";
            }

            if (message.Contains("communication", StringComparison.OrdinalIgnoreCase))
            {
                return "semantic-communication-evidence";
            }

            if (message.Contains("navigation", StringComparison.OrdinalIgnoreCase))
            {
                return "semantic-navigation";
            }

            if (message.Contains("array", StringComparison.OrdinalIgnoreCase))
            {
                return "semantic-array-bound";
            }

            if (message.Contains("vocabulary", StringComparison.OrdinalIgnoreCase))
            {
                return "semantic-vocabulary";
            }

            if (message.Contains("unsafe", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("text", StringComparison.OrdinalIgnoreCase))
            {
                return "semantic-text";
            }

            if (message.Contains("UUID", StringComparison.OrdinalIgnoreCase))
            {
                return "semantic-uuid";
            }

            if (message.Contains("path", StringComparison.OrdinalIgnoreCase))
            {
                return "semantic-path";
            }

            if (message.Contains("syntax", StringComparison.OrdinalIgnoreCase))
            {
                return "json-syntax";
            }

            if (message.Contains("property", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("shape", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("JSON", StringComparison.OrdinalIgnoreCase))
            {
                return "semantic-property-shape";
            }
        }

        return "closed-unknown";
    }

    private sealed class CapturingPreparedPass1JobExecutor(
        IPreparedPass1JobExecutor inner,
        QualificationResultCapture capture) : IPreparedPass1JobExecutor
    {
        public async Task<PreparedPass1RawWorkerResult> ExecuteAsync(
            PreparedPass1WorkerPacket packet,
            string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            PreparedPass1RawWorkerResult result = await inner.ExecuteAsync(
                packet,
                systemPrompt,
                cancellationToken);
            if (result.Outcome == PreparedPass1RawWorkerOutcome.Completed &&
                !result.Utf8Json.IsEmpty)
            {
                capture.Store(result.Utf8Json.Span);
            }

            return result;
        }
    }

    private sealed class QualificationResultCapture
    {
        private byte[]? _finalResult;

        public void Store(ReadOnlySpan<byte> finalResult)
        {
            Clear();
            _finalResult = finalResult.ToArray();
        }

        public byte[] TakeFinalResult()
        {
            byte[] result = _finalResult ?? [];
            _finalResult = null;
            return result;
        }

        public void Clear()
        {
            if (_finalResult is not null)
            {
                CryptographicOperations.ZeroMemory(_finalResult);
                _finalResult = null;
            }
        }
    }

    private sealed class AdapterSchemaNavigationService : IPass1NavigationService
    {
        public Task<PreparedPass1NavigationResponse> NavigateAsync(
            PreparedPass1NavigationRequestInput request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PreparedPass1NavigationResponse(
                PreparedPass1NavigationOutcome.Unavailable,
                Guid.Empty,
                request.Operation,
                request.RequestedDepth,
                null,
                0,
                0,
                []));
    }

    private sealed class SourceFreeHandshakeTransport : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _responseFactory;

        public SourceFreeHandshakeTransport()
            : this(HttpStatusCode.OK)
        {
        }

        public SourceFreeHandshakeTransport(HttpStatusCode statusCode)
            : this(statusCode == HttpStatusCode.OK
                ? CreateSuccessfulResponseSequence()
                : () => new HttpResponseMessage(statusCode))
        {
        }

        public SourceFreeHandshakeTransport(Func<HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken));

            HttpResponseMessage response = _responseFactory();
            return response;
        }

        private static Func<HttpResponseMessage> CreateSuccessfulResponseSequence()
        {
            int responseOrdinal = 0;
            return () => CreateSuccessfulResponse(
                ++responseOrdinal == 2 ? "submit_code_logic" : "navigate_code");
        }

        private static HttpResponseMessage CreateSuccessfulResponse(
            string functionName = "navigate_code") =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"data: {{\"id\":\"source-free-handshake\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"tool_calls\":[{{\"index\":0,\"id\":\"call-source-free\",\"type\":\"function\",\"function\":{{\"name\":\"{functionName}\",\"arguments\":\"{{}}\"}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\ndata: [DONE]\n\n",
                    Encoding.UTF8,
                    "text/event-stream"),
            };
    }

}

internal sealed class T083SourceFreeHandshakeFactAttribute : FactAttribute
{
    public T083SourceFreeHandshakeFactAttribute()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("VULGATA_T083_SOURCE_FREE_HANDSHAKE"),
            "1",
            StringComparison.Ordinal))
        {
            Skip = "T083 live source-free handshake is opt-in; set VULGATA_T083_SOURCE_FREE_HANDSHAKE=1.";
        }
    }
}
