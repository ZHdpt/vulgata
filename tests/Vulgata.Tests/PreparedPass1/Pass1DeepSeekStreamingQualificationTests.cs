using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T076 RED tracer for the provider-facing streaming and strict-wire seam.
/// </summary>
public sealed class Pass1DeepSeekStreamingQualificationTests
{
    private static readonly Guid _systemId =
        Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid _repositoryId =
        Guid.Parse("22222222-3333-4444-8555-666666666666");
    private static readonly Guid _jobId =
        Guid.Parse("33333333-4444-4555-8666-777777777777");
    private static readonly Guid _pairId =
        Guid.Parse("44444444-5555-4666-8777-888888888888");
    private static readonly Guid _fileCodeUnitId =
        Guid.Parse("55555555-6666-4777-8888-999999999999");
    private static readonly Guid _manifestId =
        Guid.Parse("66666666-7777-4888-8999-aaaaaaaaaaaa");
    private static readonly Guid _scanExecutionId =
        Guid.Parse("77777777-8888-4999-8aaa-bbbbbbbbbbbb");
    private static readonly Guid _scanRunId =
        Guid.Parse("88888888-9999-4aaa-8bbb-cccccccccccc");

    private const string DeepSeekNormalizerName =
        "NormalizeAndValidateDeepSeekRequestBody";

    [Fact]
    public async Task ExecuteAsync_FakeWire_UsesExactlyTwoStrictStreamingFunctionsWithoutRetryAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        WireCaptureHandler transport = new();
        PreparedPass1JobExecutor executor = CreateExecutor(transport, time);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Use navigate_code for evidence and submit_code_logic for finalization.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Single(transport.RequestBodies);
        Assert.Equal("https://deepseek.test/v1/chat/completions", transport.RequestUris.Single().ToString());

        using JsonDocument request = JsonDocument.Parse(transport.RequestBodies[0]);
        Assert.Equal("deepseek-v4-flash", request.RootElement.GetProperty("model").GetString());
        Assert.True(request.RootElement.GetProperty("stream").GetBoolean());
        Assert.True(request.RootElement.TryGetProperty("max_tokens", out JsonElement maxTokens));
        Assert.Equal(32_768, maxTokens.GetInt32());
        Assert.False(request.RootElement.TryGetProperty("max_completion_tokens", out _));
        Assert.True(request.RootElement.TryGetProperty("thinking", out JsonElement thinking));
        Assert.Equal("disabled", thinking.GetProperty("type").GetString());
        Assert.Equal("required", request.RootElement.GetProperty("tool_choice").GetString());
        Assert.InRange(
            Encoding.UTF8.GetByteCount(transport.RequestBodies[0]),
            0,
            17_416_192);
        JsonElement tools = request.RootElement.GetProperty("tools");
        Assert.Equal(2, tools.GetArrayLength());
        Assert.Equal(
            ["navigate_code", "submit_code_logic"],
            tools.EnumerateArray()
                .Select(tool => tool.GetProperty("function").GetProperty("name").GetString())
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
        Assert.All(tools.EnumerateArray(), tool =>
        {
            JsonElement function = tool.GetProperty("function");
            Assert.True(function.GetProperty("strict").GetBoolean());
            Assert.False(function.GetProperty("parameters").GetProperty("additionalProperties").GetBoolean());
        });
    }

    [Fact]
    public async Task ExecuteAsync_FakeWire_SuccessfulSseDataIsParsedAndRawBytesAreNotReturnedAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        PreparedPass1WorkerPacket packet = CreatePacket();
        string document = CreateReceiptFreeCandidateJson(packet);
        RawWireCaptureHandler transport = new(CreateRawSubmitSse(document));
        PreparedPass1JobExecutor executor = CreateExecutor(transport, time);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Consume successful SSE data and retain only the canonical bounded result.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(
            Encoding.UTF8.GetString(
                PreparedPass1ResultParser.Parse(
                    packet,
                    Encoding.UTF8.GetBytes(document)).CanonicalCandidateJson.Span),
            Encoding.UTF8.GetString(result.Utf8Json.Span));
        Assert.Equal(Encoding.UTF8.GetByteCount(transport.Sse), transport.RawBytesRead);
        Assert.DoesNotContain("data:", Encoding.UTF8.GetString(result.Utf8Json.Span), StringComparison.Ordinal);
        Assert.DoesNotContain("[DONE]", Encoding.UTF8.GetString(result.Utf8Json.Span), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_FakeWire_ProjectsImmutablePublicAdapterDeclarationsAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        WireCaptureHandler transport = new();
        PreparedPass1WorkerPacket packet = CreatePacket();
        PreparedPass1JobExecutor executor = CreateExecutor(transport, time);

        _ = await executor.ExecuteAsync(packet, "Use the two declared functions only.");

        using JsonDocument request = JsonDocument.Parse(Assert.Single(transport.RequestBodies));
        JsonElement root = request.RootElement;

        AIFunction expectedNavigate = new PreparedNavigateCodeFunction(new NoOpNavigationService())
            .Create(new PreparedNavigateCodeBinding(
                Guid.Parse("11111111-1111-4111-8111-111111111111"),
                1,
                Guid.Parse("22222222-2222-4222-8222-222222222222")));
        AIFunction expectedSubmit = new PreparedSubmitCodeLogicFunction()
            .CreateSession(packet)
            .Function;

        ChatTool rawNavigate = expectedNavigate.AsOpenAIChatTool();
        ChatTool rawSubmit = expectedSubmit.AsOpenAIChatTool();

        AssertRawDeclaration(rawNavigate, "navigate_code", 2_115,
            "C8387178DA32FC682E7DE462AFA8B62AF388944CB0FC741B895375E585E87770");
        AssertRawDeclaration(rawSubmit, "submit_code_logic", 27_502,
            "77A044526E5D61480B3E86FA3C3AA354FF78A9F04BCA3B279F210DFBFB421BC8");

        byte[] rawCombined = CombineRawDeclarations(rawNavigate, rawSubmit);
        Assert.Equal(29_964, rawCombined.Length);
        Assert.Equal(
            "54321765E82EBE39092DF96A6F0E39F922E24459C34830525C22A38CDE533B61",
            Convert.ToHexString(SHA256.HashData(rawCombined)));

        JsonElement projectedNavigate = FindTool(root, "navigate_code")
            .GetProperty("function").GetProperty("parameters");
        JsonElement projectedSubmit = FindTool(root, "submit_code_logic")
            .GetProperty("function").GetProperty("parameters");
        AssertProjectedSchemas(
            projectedNavigate,
            rawNavigate.FunctionParameters!.ToMemory().ToArray(),
            projectedSubmit,
            rawSubmit.FunctionParameters!.ToMemory().ToArray());
    }

    [Fact]
    public void DeclarationAuthority_ExposesRawAndProjectedHashesAndCompleteSortedLedger()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        AIFunction navigate = new PreparedNavigateCodeFunction(new NoOpNavigationService())
            .Create(new PreparedNavigateCodeBinding(
                Guid.Parse("11111111-1111-4111-8111-111111111111"),
                1,
                Guid.Parse("22222222-2222-4222-8222-222222222222")));
        AIFunction submit = new PreparedSubmitCodeLogicFunction()
            .CreateSession(packet)
            .Function;
        object authority = DeepSeekStrictToolDeclarations.Create(navigate, submit);

        byte[] rawNavigate = ReadBytes(authority, "RawNavigateCodeParametersUtf8");
        byte[] rawSubmit = ReadBytes(authority, "RawSubmitCodeLogicParametersUtf8");
        byte[] rawCombined = ReadBytes(authority, "RawUtf8");
        byte[] projectedNavigate = ReadBytes(authority, "ProjectedNavigateCodeParametersUtf8");
        byte[] projectedSubmit = ReadBytes(authority, "ProjectedSubmitCodeLogicParametersUtf8");
        byte[] projectedCombined = ReadBytes(authority, "ProjectedUtf8");
        byte[] projectedSubmitOnly = ReadBytes(authority, "ProjectedSubmitOnlyUtf8");
        string projectedSubmitOnlySha256 = ReadString(authority, "ProjectedSubmitOnlySha256");

        Assert.Equal(2_115, rawNavigate.Length);
        Assert.Equal(27_502, rawSubmit.Length);
        Assert.Equal(29_964, rawCombined.Length);
        Assert.Equal("C8387178DA32FC682E7DE462AFA8B62AF388944CB0FC741B895375E585E87770", ReadString(authority, "RawNavigateCodeParametersSha256"));
        Assert.Equal("77A044526E5D61480B3E86FA3C3AA354FF78A9F04BCA3B279F210DFBFB421BC8", ReadString(authority, "RawSubmitCodeLogicParametersSha256"));
        Assert.Equal("54321765E82EBE39092DF96A6F0E39F922E24459C34830525C22A38CDE533B61", ReadString(authority, "RawSha256"));
        Assert.Equal(ReadString(authority, "ProjectedSha256"), Convert.ToHexString(SHA256.HashData(projectedCombined)));
        Assert.NotEqual("03B4953CBDB1B957E74B71F86EB64D41D2533A977665C5A40886808204BAA3FF", ReadString(authority, "ProjectedSha256"));
        Assert.NotEqual(rawNavigate, projectedNavigate);
        Assert.NotEqual(rawSubmit, projectedSubmit);
        Assert.Equal(projectedSubmitOnlySha256, Convert.ToHexString(SHA256.HashData(projectedSubmitOnly)));
        Assert.InRange(projectedSubmitOnly.Length, 1, 81_920);
        using (JsonDocument submitOnly = JsonDocument.Parse(projectedSubmitOnly))
        {
            JsonElement[] tools = submitOnly.RootElement.EnumerateArray().ToArray();
            Assert.Single(tools);
            JsonElement function = tools[0].GetProperty("function");
            Assert.Equal("submit_code_logic", function.GetProperty("name").GetString());
            Assert.True(function.GetProperty("strict").GetBoolean());
            Assert.Equal(
                projectedSubmit,
                Encoding.UTF8.GetBytes(function.GetProperty("parameters").GetRawText()));
        }

        Assert.Equal("deepseek-strict-schema-projection-v1", ReadString(authority, "ProjectionAlgorithmIdentity"));
        object ledger = ReadMember(authority, "TransformLedger");
        object[] rows = ((System.Collections.IEnumerable)ledger).Cast<object>().ToArray();
        Assert.Equal(18, rows.Length);
        string[] pointers = rows.Select(row => ReadString(row, "JsonPointer", "Pointer")).ToArray();
        Assert.Equal(pointers.OrderBy(value => value, StringComparer.Ordinal), pointers);
        Assert.Equal(18, pointers.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(1, pointers.Count(value => value.EndsWith("/position", StringComparison.Ordinal)));
        Assert.Equal(1, pointers.Count(value => value.EndsWith("/exactLineRange", StringComparison.Ordinal)));
        Assert.Equal(16, rows.Count(row => ReadString(row, "TransformKind", "Kind").Contains("Enum", StringComparison.OrdinalIgnoreCase)));
        Assert.All(rows, row =>
        {
            Assert.Matches("^/[A-Za-z0-9_./-]+$", ReadString(row, "JsonPointer", "Pointer"));
            Assert.Matches("^[0-9A-F]{64}$", ReadString(row, "RawSubtreeSha256", "InputSubtreeSha256"));
            Assert.Matches("^[0-9A-F]{64}$", ReadString(row, "ProjectedSubtreeSha256", "OutputSubtreeSha256"));
        });

        byte[] rawNavigateSnapshot = [.. rawNavigate];
        byte[] rawSubmitSnapshot = [.. rawSubmit];
        _ = DeepSeekStrictToolDeclarations.Create(navigate, submit);
        Assert.Equal(rawNavigateSnapshot, ReadBytes(authority, "RawNavigateCodeParametersUtf8"));
        Assert.Equal(rawSubmitSnapshot, ReadBytes(authority, "RawSubmitCodeLogicParametersUtf8"));
    }

    [Theory]
    [MemberData(nameof(StrictProjectionMutationCases))]
    public void DeclarationAuthority_RejectsUnsupportedOrDriftedRawSchemaBeforeSend(
        string mutation,
        Func<JsonObject, JsonObject> mutate)
    {
        byte[] raw = new PreparedNavigateCodeFunction(new NoOpNavigationService())
            .Create(new PreparedNavigateCodeBinding(
                Guid.Parse("11111111-1111-4111-8111-111111111111"),
                1,
                Guid.Parse("22222222-2222-4222-8222-222222222222")))
            .AsOpenAIChatTool().FunctionParameters!.ToMemory().ToArray();
        JsonObject mutated = mutate(JsonNode.Parse(raw)!.AsObject());

        MethodInfo? method = typeof(DeepSeekStrictToolDeclarations).GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(candidate =>
                (candidate.Name is "ProjectRaw" or "Project" or "CreateFromRaw") &&
                candidate.GetParameters().Length >= 1 &&
                candidate.GetParameters().All(parameter =>
                    parameter.ParameterType == typeof(byte[]) ||
                    parameter.ParameterType == typeof(ReadOnlyMemory<byte>) ||
                    parameter.ParameterType == typeof(string)));
        Assert.True(method is not null,
            $"T097 RED: shared raw-schema projection seam is missing for mutation '{mutation}'.");

        byte[] mutatedBytes = JsonSerializer.SerializeToUtf8Bytes(mutated);
        object[] arguments = method!.GetParameters().Select(parameter =>
        {
            if (parameter.ParameterType == typeof(byte[])) return (object)mutatedBytes;
            if (parameter.ParameterType == typeof(ReadOnlyMemory<byte>)) return new ReadOnlyMemory<byte>(mutatedBytes);
            return Encoding.UTF8.GetString(mutatedBytes);
        }).ToArray();
        TargetInvocationException? invocation = Assert.Throws<TargetInvocationException>(
            () => method.Invoke(null, arguments));
        Assert.NotNull(invocation.InnerException);
    }

    public static IEnumerable<object[]> StrictProjectionMutationCases()
    {
        yield return ["missing-ledger-node", (Func<JsonObject, JsonObject>)RemovePositionProperty];
        yield return ["additional-ledger-node", (Func<JsonObject, JsonObject>)AddNestedProperty];
        yield return ["reordered-ledger-node", (Func<JsonObject, JsonObject>)ReverseNullableTypeArray];
        yield return ["changed-ledger-node", (Func<JsonObject, JsonObject>)ChangeNullableTypeArray];
        yield return ["changed-type-array", (Func<JsonObject, JsonObject>)SetTypeArray];
        yield return ["unsupported-keyword", (Func<JsonObject, JsonObject>)AddUnsupportedKeyword];
        yield return ["open-object", (Func<JsonObject, JsonObject>)OpenRoot];
        yield return ["incomplete-object", (Func<JsonObject, JsonObject>)RemoveRequired];
        yield return ["reference-cycle", (Func<JsonObject, JsonObject>)AddReferenceCycle];
        yield return ["empty-enum", (Func<JsonObject, JsonObject>)(root => MutateEnum(root, values => []))];
        yield return ["heterogeneous-enum", (Func<JsonObject, JsonObject>)(root => MutateEnum(root, values => ["x", 1]))];
        yield return ["raw-hash-drift", (Func<JsonObject, JsonObject>)(root => AddDescription(root, "drift"))];
        yield return ["size-overflow", (Func<JsonObject, JsonObject>)(root => AddDescription(root, new string('x', 82_000)))];
    }

    private static JsonObject RemoveRequired(JsonObject root)
    {
        root["required"]?.AsArray().RemoveAt(0);
        return root;
    }

    private static JsonObject AddProperty(JsonObject root)
    {
        root["providerOnly"] = new JsonObject { ["type"] = "string" };
        return root;
    }

    private static JsonObject RemovePositionProperty(JsonObject root)
    {
        JsonObject? owner = FindObjectWithProperty(root, "position");
        Assert.True(owner is not null, "T097 fixture requires the nullable position transform node.");
        owner!.Remove("position");
        return root;
    }

    private static JsonObject AddNestedProperty(JsonObject root)
    {
        JsonObject? owner = FindPropertiesObject(root);
        Assert.True(owner is not null, "T097 fixture requires a closed object transform node.");
        owner!["providerOnly"] = new JsonObject { ["type"] = "string" };
        return root;
    }

    private static JsonObject ReverseNullableTypeArray(JsonObject root)
    {
        JsonObject? node = FindTypeArray(root, "object", "null");
        Assert.True(node is not null, "T097 fixture requires an object-null transform node.");
        node!["type"] = new JsonArray("null", "object");
        return root;
    }

    private static JsonObject ChangeNullableTypeArray(JsonObject root)
    {
        JsonObject? node = FindTypeArray(root, "object", "null");
        Assert.True(node is not null, "T097 fixture requires an object-null transform node.");
        node!["type"] = new JsonArray("object", "string");
        return root;
    }

    private static JsonObject SetTypeArray(JsonObject root)
    {
        root["type"] = new JsonArray("object", "null", "string");
        return root;
    }

    private static JsonObject AddUnsupportedKeyword(JsonObject root)
    {
        root["unevaluatedProperties"] = false;
        return root;
    }

    private static JsonObject OpenRoot(JsonObject root)
    {
        root["additionalProperties"] = true;
        return root;
    }

    private static JsonObject AddReferenceCycle(JsonObject root)
    {
        root["$ref"] = "#";
        return root;
    }

    private static JsonObject AddDescription(JsonObject root, string value)
    {
        root["description"] = value;
        return root;
    }

    private static JsonObject MutateEnum(JsonObject root, Func<JsonArray, JsonArray> mutation)
    {
        JsonObject? node = FindEnumOnly(root);
        Assert.True(node is not null, "T097 fixture requires an enum-only raw schema node.");
        JsonArray values = node!["enum"]!.AsArray();
        node.Remove("type");
        node["enum"] = mutation(values);
        return root;
    }

    private static JsonObject? FindEnumOnly(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey("enum") && obj["enum"] is JsonArray values && values.Count > 0)
            {
                return obj;
            }

            foreach (JsonNode? child in obj.Select(pair => pair.Value))
            {
                JsonObject? found = child is null ? null : FindEnumOnly(child);
                if (found is not null) return found;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                JsonObject? found = child is null ? null : FindEnumOnly(child);
                if (found is not null) return found;
            }
        }

        return null;
    }

    private static JsonObject? FindPropertiesObject(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey("properties")) return obj["properties"]!.AsObject();
            foreach (JsonNode? child in obj.Select(pair => pair.Value))
            {
                JsonObject? found = child is null ? null : FindPropertiesObject(child);
                if (found is not null) return found;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                JsonObject? found = child is null ? null : FindPropertiesObject(child);
                if (found is not null) return found;
            }
        }

        return null;
    }

    private static JsonObject? FindObjectWithProperty(JsonNode node, string propertyName)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey("properties") && obj["properties"]!.AsObject().ContainsKey(propertyName))
            {
                return obj["properties"]!.AsObject();
            }
            foreach (JsonNode? child in obj.Select(pair => pair.Value))
            {
                JsonObject? found = child is null ? null : FindObjectWithProperty(child, propertyName);
                if (found is not null) return found;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                JsonObject? found = child is null ? null : FindObjectWithProperty(child, propertyName);
                if (found is not null) return found;
            }
        }

        return null;
    }

    private static JsonObject? FindTypeArray(JsonNode node, params string[] expected)
    {
        if (node is JsonObject obj)
        {
            if (obj["type"] is JsonArray values &&
                values.Select(value => value?.GetValue<string>()).SequenceEqual(expected, StringComparer.Ordinal))
            {
                return obj;
            }
            foreach (JsonNode? child in obj.Select(pair => pair.Value))
            {
                JsonObject? found = child is null ? null : FindTypeArray(child, expected);
                if (found is not null) return found;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                JsonObject? found = child is null ? null : FindTypeArray(child, expected);
                if (found is not null) return found;
            }
        }

        return null;
    }

    private static object ReadMember(object instance, params string[] names)
    {
        Type type = instance.GetType();
        foreach (string name in names)
        {
            PropertyInfo? property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(instance) is object value) return value;
            FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(instance) is object fieldValue) return fieldValue;
        }

        throw new Xunit.Sdk.XunitException($"T097 RED: required declaration member is missing: {string.Join('/', names)}.");
    }

    private static string ReadString(object instance, params string[] names) =>
        ReadMember(instance, names) as string ??
        throw new Xunit.Sdk.XunitException($"T097 RED: declaration member is not a string: {string.Join('/', names)}.");

    private static byte[] ReadBytes(object instance, params string[] names)
    {
        object value = ReadMember(instance, names);
        return value switch
        {
            byte[] bytes => [.. bytes],
            ReadOnlyMemory<byte> memory => memory.ToArray(),
            _ => throw new Xunit.Sdk.XunitException($"T097 RED: declaration member is not byte data: {string.Join('/', names)}."),
        };
    }

    [Fact]
    public async Task ExecuteAsync_FakeWire_OmitsUnsupportedDeepSeekFieldsAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        WireCaptureHandler transport = new();
        PreparedPass1JobExecutor executor = CreateExecutor(transport, time);

        _ = await executor.ExecuteAsync(CreatePacket(), "Use the two declared functions only.");

        using JsonDocument request = JsonDocument.Parse(Assert.Single(transport.RequestBodies));
        Assert.Equal("required", request.RootElement.GetProperty("tool_choice").GetString());
        Assert.False(request.RootElement.TryGetProperty("parallel_tool_calls", out _));
        Assert.False(request.RootElement.TryGetProperty("response_format", out _));
    }

    private static void AssertRawDeclaration(
        ChatTool tool,
        string expectedName,
        int expectedBytes,
        string expectedSha256)
    {
        Assert.Equal(expectedName, tool.FunctionName);
        byte[] raw = tool.FunctionParameters!.ToMemory().ToArray();
        Assert.Equal(expectedBytes, raw.Length);
        Assert.Equal(expectedSha256, Convert.ToHexString(SHA256.HashData(raw)));
    }

    private static byte[] CombineRawDeclarations(ChatTool navigate, ChatTool submit)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WriteString("type", "function");
            writer.WriteStartObject("function");
            writer.WriteString("name", navigate.FunctionName);
            writer.WriteString("description", navigate.FunctionDescription);
            writer.WritePropertyName("parameters");
            writer.WriteRawValue(navigate.FunctionParameters!.ToMemory().Span);
            writer.WriteBoolean("strict", true);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteStartObject();
            writer.WriteString("type", "function");
            writer.WriteStartObject("function");
            writer.WriteString("name", submit.FunctionName);
            writer.WriteString("description", submit.FunctionDescription);
            writer.WritePropertyName("parameters");
            writer.WriteRawValue(submit.FunctionParameters!.ToMemory().Span);
            writer.WriteBoolean("strict", true);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndArray();
        }

        return stream.ToArray();
    }

    private static JsonElement FindTool(JsonElement request, string name) =>
        request.GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("function").GetProperty("name").GetString() == name);

    private static void AssertProjectedSchemas(
        JsonElement projectedNavigate,
        byte[] rawNavigateBytes,
        JsonElement projectedSubmit,
        byte[] rawSubmitBytes)
    {
        (Dictionary<string, string> Raw, Dictionary<string, string> Projected) navigate =
            ReadSchemaPair(projectedNavigate, rawNavigateBytes);
        (Dictionary<string, string> Raw, Dictionary<string, string> Projected) submit =
            ReadSchemaPair(projectedSubmit, rawSubmitBytes);

        Dictionary<string, string>[] rawSchemas = [navigate.Raw, submit.Raw];
        Dictionary<string, string>[] projectedSchemas = [navigate.Projected, submit.Projected];

        int[] rawNullableObjectsByFunction = rawSchemas
            .Select(schema => schema.Values.Count(value => IsTypeArray(value, "object", "null")))
            .ToArray();
        int[] projectedNullableObjectsByFunction = projectedSchemas
            .Select(schema => schema.Values.Count(value => IsAnyOfObjectNull(value)))
            .ToArray();
        Assert.Equal([2, 0], rawNullableObjectsByFunction);
        Assert.Equal([2, 0], projectedNullableObjectsByFunction);
        Assert.Equal(2, rawNullableObjectsByFunction.Sum());
        Assert.Equal(2, projectedNullableObjectsByFunction.Sum());
        Assert.DoesNotContain(projectedSchemas.SelectMany(schema => schema.Values),
            value => IsTypeArray(value, "object", "null"));

        int[] rawEnumOnlyByFunction = rawSchemas
            .Select(schema => schema.Values.Count(IsStringEnumOnlyWithoutType))
            .ToArray();
        int[] projectedEnumOnlyByFunction = projectedSchemas
            .Select(schema => schema.Values.Count(IsStringEnumOnlyWithoutType))
            .ToArray();
        int[] rawExplicitStringEnumsByFunction = rawSchemas
            .Select(schema => schema.Values.Count(IsExplicitStringEnum))
            .ToArray();
        int[] projectedExplicitStringEnumsByFunction = projectedSchemas
            .Select(schema => schema.Values.Count(IsExplicitStringEnum))
            .ToArray();
        Assert.Equal([0, 16], rawEnumOnlyByFunction);
        Assert.Equal([0, 0], projectedEnumOnlyByFunction);
        Assert.Equal([0, 16], projectedExplicitStringEnumsByFunction
            .Zip(rawExplicitStringEnumsByFunction, (projected, raw) => projected - raw)
            .ToArray());
        Assert.Equal(16, rawEnumOnlyByFunction.Sum());
        Assert.Equal(16, projectedExplicitStringEnumsByFunction.Zip(
            rawExplicitStringEnumsByFunction, (projected, raw) => projected - raw).Sum());

        for (int index = 0; index < rawSchemas.Length; index++)
        {
            string[] primitiveNullablePaths = rawSchemas[index]
                .Where(pair => IsPrimitiveNullableTypeArray(pair.Value))
                .Select(pair => pair.Key)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            foreach (string path in primitiveNullablePaths)
            {
                Assert.True(projectedSchemas[index].TryGetValue(path, out string? projectedValue));
                Assert.Equal(NormalizeJson(rawSchemas[index][path]), NormalizeJson(projectedValue!));
            }
        }
        Assert.Equal(4, rawSchemas.Sum(schema => schema.Values.Count(IsPrimitiveNullableTypeArray)));

        Assert.All(projectedSchemas.SelectMany(schema => schema.Values), value => AssertNoReference(value));
        Assert.All(projectedSchemas.SelectMany(schema => schema.Values), value => AssertClosedObjects(value));
    }

    private static (Dictionary<string, string> Raw, Dictionary<string, string> Projected)
        ReadSchemaPair(JsonElement projected, byte[] rawBytes)
    {
        using JsonDocument rawDocument = JsonDocument.Parse(rawBytes);
        return (FlattenSchema(rawDocument.RootElement), FlattenSchema(projected));
    }

    private static string NormalizeJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(document.RootElement));
    }

    private static string CreateReceiptFreeCandidateJson(PreparedPass1WorkerPacket packet)
    {
        JsonObject root = JsonNode.Parse(
            Encoding.UTF8.GetString(
                PreparedPass1ResultParser.CreateDeferredCallsFallback(packet).Utf8Json.Span))!.AsObject();
        root["knownReferences"] = new JsonArray();
        root["deferredObservations"] = new JsonArray();
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static Dictionary<string, string> FlattenSchema(JsonElement root)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        Walk(root, "", values);
        return values;
    }

    private static void Walk(JsonElement node, string path, Dictionary<string, string> values)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        values[path] = node.GetRawText();
        foreach (JsonProperty property in node.EnumerateObject())
        {
            if ((property.Name is "properties" or "$defs") &&
                property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty child in property.Value.EnumerateObject())
                {
                    Walk(child.Value, path + "/" + child.Name, values);
                }
            }
            else if (property.Name is "items" or "anyOf" or "oneOf" or "allOf")
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    int index = 0;
                    foreach (JsonElement child in property.Value.EnumerateArray())
                    {
                        Walk(child, path + "/" + property.Name + "/" + index++, values);
                    }
                }
                else
                {
                    Walk(property.Value, path + "/" + property.Name, values);
                }
            }
        }
    }

    private static bool IsTypeArray(string json, params string[] values)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("type", out JsonElement type) ||
            type.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return
            type.EnumerateArray().Select(value => value.GetString())
                .SequenceEqual(values, StringComparer.Ordinal);
    }

    private static bool IsPrimitiveNullableTypeArray(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("type", out JsonElement type) ||
            type.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        string[] values = type.EnumerateArray().Select(value => value.GetString()!).ToArray();
        return values.Contains("null") &&
            values.All(value => value is "null" or "string" or "number" or "integer" or "boolean");
    }

    private static bool IsStringEnumOnlyWithoutType(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return root.TryGetProperty("enum", out JsonElement values) &&
            !root.TryGetProperty("type", out _) &&
            values.ValueKind == JsonValueKind.Array &&
            values.GetArrayLength() > 0 &&
            values.EnumerateArray().All(value => value.ValueKind == JsonValueKind.String);
    }

    private static bool IsExplicitStringEnum(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return root.TryGetProperty("type", out JsonElement type) &&
            type.ValueKind == JsonValueKind.String && type.GetString() == "string" &&
            root.TryGetProperty("enum", out JsonElement values) &&
            values.ValueKind == JsonValueKind.Array &&
            values.GetArrayLength() > 0 &&
            values.EnumerateArray().All(value => value.ValueKind == JsonValueKind.String);
    }

    private static bool IsAnyOfObjectNull(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("anyOf", out JsonElement branches) ||
            branches.ValueKind != JsonValueKind.Array || branches.GetArrayLength() != 2)
        {
            return false;
        }

        JsonElement[] values = branches.EnumerateArray().ToArray();
        return values[0].GetProperty("type").GetString() == "object" &&
            values[0].GetProperty("additionalProperties").GetBoolean() == false &&
            values[1].GetProperty("type").GetString() == "null";
    }

    private static void AssertNoReference(string json)
    {
        Assert.DoesNotContain("\"$ref\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"$defs\"", json, StringComparison.Ordinal);
    }

    private static void AssertClosedObjects(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("type", out JsonElement type) &&
            type.ValueKind == JsonValueKind.String && type.GetString() == "object")
        {
            Assert.True(document.RootElement.TryGetProperty("additionalProperties", out JsonElement closed));
            Assert.False(closed.GetBoolean());
            Assert.True(document.RootElement.TryGetProperty("required", out _));
        }
    }

    [Theory]
    [MemberData(nameof(DeepSeekRequestShapeCases))]
    public void NormalizeAndValidateDeepSeekRequestBody_UsesIndependentTokenThinkingAndChoiceOperations(
        string caseName,
        string sourceJson,
        bool accepted,
        bool expectsTokenRename,
        bool expectsThinkingInsertion,
        bool expectsChoiceConstruction)
    {
        _ = caseName;
        byte[]? normalized = InvokeDeepSeekNormalizer(sourceJson);

        if (!accepted)
        {
            Assert.Null(normalized);
            return;
        }

        Assert.NotNull(normalized);
        using JsonDocument document = JsonDocument.Parse(normalized!);
        JsonElement root = document.RootElement;
        Assert.Equal(32_768, root.GetProperty("max_tokens").GetInt32());
        Assert.False(root.TryGetProperty("max_completion_tokens", out _));
        Assert.Equal("disabled", root.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal("required", root.GetProperty("tool_choice").GetString());
        Assert.True(root.GetProperty("stream").GetBoolean());
        Assert.Equal(
            expectsTokenRename,
            sourceJson.Contains("max_completion_tokens", StringComparison.Ordinal));
        Assert.Equal(
            expectsThinkingInsertion,
            !sourceJson.Contains("\"thinking\"", StringComparison.Ordinal));
        Assert.Equal(
            expectsChoiceConstruction,
            !sourceJson.Contains("\"tool_choice\"", StringComparison.Ordinal));
    }

    public static IEnumerable<object[]> DeepSeekRequestShapeCases()
    {
        const string exactThinking =
            "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"tool_choice\":\"required\",\"stream\":true}";
        yield return ["exact-fields", exactThinking, true, false, false, false];
        yield return ["absent-thinking-and-choice", "{\"max_tokens\":32768,\"stream\":true}", true, false, true, true];
        yield return ["rename-token-only", "{\"max_completion_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"tool_choice\":\"required\",\"stream\":true}", true, true, false, false];
        yield return ["rename-and-insert", "{\"max_completion_tokens\":32768,\"stream\":true}", true, true, true, true];

        yield return ["missing-token", "{\"thinking\":{\"type\":\"disabled\"},\"stream\":true}", false, false, false, false];
        yield return ["wrong-token-string", "{\"max_tokens\":\"32768\",\"stream\":true}", false, false, false, false];
        yield return ["wrong-token-decimal", "{\"max_tokens\":32768.0,\"stream\":true}", false, false, false, false];
        yield return ["wrong-token-boolean", "{\"max_tokens\":true,\"stream\":true}", false, false, false, false];
        yield return ["wrong-token-null", "{\"max_tokens\":null,\"stream\":true}", false, false, false, false];
        yield return ["duplicate-token", "{\"max_tokens\":32768,\"max_tokens\":32768,\"stream\":true}", false, false, false, false];
        yield return ["simultaneous-token-fields", "{\"max_tokens\":32768,\"max_completion_tokens\":32768,\"stream\":true}", false, false, false, false];
        yield return ["duplicate-completion-token", "{\"max_completion_tokens\":32768,\"max_completion_tokens\":32768,\"stream\":true}", false, false, false, false];
        yield return ["wrong-completion-token", "{\"max_completion_tokens\":\"32768\",\"stream\":true}", false, false, false, false];
        yield return ["malformed-json", "{\"max_tokens\":32768,\"stream\":true", false, false, false, false];

        yield return ["thinking-string", "{\"max_tokens\":32768,\"thinking\":\"enabled\",\"stream\":true}", false, false, false, false];
        yield return ["thinking-null", "{\"max_tokens\":32768,\"thinking\":null,\"stream\":true}", false, false, false, false];
        yield return ["thinking-array", "{\"max_tokens\":32768,\"thinking\":[],\"stream\":true}", false, false, false, false];
        yield return ["thinking-number", "{\"max_tokens\":32768,\"thinking\":1,\"stream\":true}", false, false, false, false];
        yield return ["thinking-enabled", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"enabled\"},\"stream\":true}", false, false, false, false];
        yield return ["thinking-extra-property", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\",\"extra\":true},\"stream\":true}", false, false, false, false];
        yield return ["duplicate-thinking", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"thinking\":{\"type\":\"disabled\"},\"stream\":true}", false, false, false, false];
        yield return ["stream-disabled", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"stream\":false}", false, false, false, false];
        yield return ["stream-wrong-type", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"stream\":\"true\"}", false, false, false, false];

        yield return ["choice-auto", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"tool_choice\":\"auto\",\"stream\":true}", true, false, false, false];
        yield return ["choice-required", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"tool_choice\":\"required\",\"stream\":true}", true, false, false, false];
        yield return ["duplicate-choice", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"tool_choice\":\"auto\",\"tool_choice\":\"auto\",\"stream\":true}", false, false, false, false];
        yield return ["choice-object", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"tool_choice\":{\"type\":\"function\"},\"stream\":true}", false, false, false, false];
        yield return ["choice-none", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"tool_choice\":\"none\",\"stream\":true}", false, false, false, false];
        yield return ["reasoning-effort", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"reasoning_effort\":\"low\",\"stream\":true}", false, false, false, false];
        yield return ["temperature", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"temperature\":0.2,\"stream\":true}", false, false, false, false];
        yield return ["top-p", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"top_p\":0.2,\"stream\":true}", false, false, false, false];
        yield return ["presence-penalty", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"presence_penalty\":0.2,\"stream\":true}", false, false, false, false];
        yield return ["frequency-penalty", "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"frequency_penalty\":0.2,\"stream\":true}", false, false, false, false];
    }

    [Theory]
    [MemberData(nameof(DeepSeekCompatibilityShapeCases))]
    public void NormalizeAndValidateDeepSeekRequestBody_ConstructsExactRequiredChoiceOrRejectsIt(
        string caseName,
        string sourceJson,
        bool accepted)
    {
        _ = caseName;
        byte[]? normalized = InvokeDeepSeekNormalizer(sourceJson);

        if (!accepted)
        {
            Assert.Null(normalized);
            return;
        }

        Assert.NotNull(normalized);
        using JsonDocument document = JsonDocument.Parse(normalized!);
        Assert.Equal("required", document.RootElement.GetProperty("tool_choice").GetString());
        Assert.False(document.RootElement.TryGetProperty("parallel_tool_calls", out _));
        Assert.False(document.RootElement.TryGetProperty("response_format", out _));
        Assert.Equal("disabled", document.RootElement.GetProperty("thinking").GetProperty("type").GetString());
    }

    public static IEnumerable<object[]> DeepSeekCompatibilityShapeCases()
    {
        const string prefix = "{\"max_tokens\":32768,\"thinking\":{\"type\":\"disabled\"},\"stream\":true";
        yield return ["absent", prefix + "}", true];
        yield return ["exact-tool-choice", prefix + ",\"tool_choice\":\"auto\"}", true];
        yield return ["required-tool-choice", prefix + ",\"tool_choice\":\"required\"}", true];
        yield return ["exact-parallel", prefix + ",\"parallel_tool_calls\":false}", true];
        yield return ["exact-response-format", prefix + ",\"response_format\":{\"type\":\"json_object\"}}", true];
        yield return ["duplicate-tool-choice", prefix + ",\"tool_choice\":\"auto\",\"tool_choice\":\"auto\"}", false];
        yield return ["tool-choice-wrong-type", prefix + ",\"tool_choice\":{}}", false];
        yield return ["tool-choice-wrong-value", prefix + ",\"tool_choice\":\"none\"}", false];
        yield return ["duplicate-parallel", prefix + ",\"parallel_tool_calls\":false,\"parallel_tool_calls\":false}", false];
        yield return ["parallel-wrong-type", prefix + ",\"parallel_tool_calls\":\"false\"}", false];
        yield return ["parallel-wrong-value", prefix + ",\"parallel_tool_calls\":true}", false];
        yield return ["duplicate-response-format", prefix + ",\"response_format\":{\"type\":\"json_object\"},\"response_format\":{\"type\":\"json_object\"}}", false];
        yield return ["response-format-wrong-type", prefix + ",\"response_format\":\"json_object\"}", false];
        yield return ["response-format-wrong-value", prefix + ",\"response_format\":{\"type\":\"text\"}}", false];
    }

    [Fact]
    public async Task NormalizeAndValidateDeepSeekRequestBody_IsScopedToPreparedExecutorAndDoesNotMutateSiblingHttpClientAsync()
    {
        byte[] sourceBody = Encoding.UTF8.GetBytes(
            "{\"max_completion_tokens\":32768,\"stream\":true}");
        byte[] originalBody = [.. sourceBody];
        byte[]? normalized = InvokeDeepSeekNormalizer(sourceBody);
        Assert.NotNull(normalized);
        Assert.Equal(originalBody, sourceBody);

        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        WireCaptureHandler transport = new();
        PreparedPass1JobExecutor executor = CreateExecutor(transport, time);
        _ = await executor.ExecuteAsync(CreatePacket(), "Use only the prepared executor wire.");

        const string siblingBody =
            "{\"ordinary_client\":true,\"max_completion_tokens\":123}";
        using HttpClient siblingClient = new(transport, disposeHandler: false);
        using HttpResponseMessage siblingResponse = await siblingClient.PostAsync(
            "https://sibling.test/v1/chat/completions",
            new StringContent(siblingBody, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, siblingResponse.StatusCode);
        Assert.Equal(2, transport.RequestBodies.Count);
        Assert.Equal(siblingBody, transport.RequestBodies[1]);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderFailureDoesNotUseSdkRetryAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        FailureCaptureHandler transport = new(HttpStatusCode.ServiceUnavailable);
        PreparedPass1JobExecutor executor = CreateExecutor(transport, time);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Use the declared functions only.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal("provider-unavailable", result.ReasonCode);
        Assert.Single(transport.RequestBodies);
    }

    [Fact]
    public async Task ExecuteAsync_LatestLegalSecondSubmissionCorrectsTurnNineAtTurnTenWithoutRequestElevenAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        PreparedPass1WorkerPacket packet = CreatePacket();
        string validDocument = CreateReceiptFreeCandidateJson(packet);
        LatestLegalTurnHandler transport = new(validDocument);
        PreparedPass1JobExecutor executor = CreateExecutor(transport, time);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Use eight bounded navigations, reject the first submission on forced turn nine, then correct it on turn ten without request eleven.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(validDocument, Encoding.UTF8.GetString(result.Utf8Json.Span));
        Assert.Equal(10, transport.RequestBodies.Count);
        AssertOutgoingBodiesWithinLimit(transport.RequestBodies);
        Assert.Equal(
            ["submit_code_logic", "submit_code_logic"],
            transport.FunctionNames.Skip(8).ToArray());

        Assert.Equal(10, transport.RequestBodies.Count);
        object authority = CreateDeclarationAuthority(packet);
        for (int index = 0; index < transport.RequestBodies.Count; index++)
        {
            AssertT132ToolChoice(transport.RequestBodies[index], index + 1);
            AssertT145DeclarationIdentity(transport.RequestBodies[index], index + 1, authority);
        }
    }

    [Theory]
    [MemberData(nameof(T132TurnAwareToolChoiceCases))]
    public void NormalizeAndValidateDeepSeekRequestBody_UsesClosedOrdinaryAndTerminalChoiceMatrix(
        string caseName,
        int providerTurn,
        string toolChoiceJson,
        bool accepted,
        bool terminalChoice)
    {
        _ = caseName;
        string choiceProperty = toolChoiceJson == "<absent>"
            ? string.Empty
            : toolChoiceJson == "<duplicate>"
                ? ",\"tool_choice\":{\"type\":\"function\",\"function\":{\"name\":\"submit_code_logic\"}},\"tool_choice\":{\"type\":\"function\",\"function\":{\"name\":\"submit_code_logic\"}}"
                : $",\"tool_choice\":{toolChoiceJson}";
        string source =
            $"{{\"max_tokens\":32768,\"thinking\":{{\"type\":\"disabled\"}}{choiceProperty},\"stream\":true}}";
        byte[]? normalized = InvokeTurnAwareDeepSeekNormalizer(source, providerTurn);

        if (!accepted)
        {
            Assert.Null(normalized);
            return;
        }

        Assert.NotNull(normalized);
        using JsonDocument document = JsonDocument.Parse(normalized!);
        JsonElement choice = document.RootElement.GetProperty("tool_choice");
        if (!terminalChoice)
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
    }

    public static IEnumerable<object[]> T132TurnAwareToolChoiceCases()
    {
        yield return ["ordinary-absent", 8, "<absent>", true, false];
        yield return ["ordinary-auto", 8, "\"auto\"", true, false];
        yield return ["ordinary-required", 8, "\"required\"", true, false];
        yield return ["ordinary-named-submit", 8, "{\"type\":\"function\",\"function\":{\"name\":\"submit_code_logic\"}}", false, false];
        yield return ["terminal-named-submit", 10, "{\"type\":\"function\",\"function\":{\"name\":\"submit_code_logic\"}}", true, true];
        yield return ["terminal-absent", 10, "<absent>", false, true];
        yield return ["terminal-auto", 10, "\"auto\"", false, true];
        yield return ["terminal-required", 10, "\"required\"", false, true];
        yield return ["terminal-none", 10, "\"none\"", false, true];
        yield return ["terminal-navigation", 10, "{\"type\":\"function\",\"function\":{\"name\":\"navigate_code\"}}", false, true];
        yield return ["terminal-extra-property", 10, "{\"type\":\"function\",\"function\":{\"name\":\"submit_code_logic\"},\"extra\":true}", false, true];
        yield return ["terminal-wrong-type", 10, "{\"type\":\"function\",\"function\":{\"name\":123}}", false, true];
        yield return ["terminal-duplicate", 10, "<duplicate>", false, true];
    }

    [Fact]
    public async Task ExecuteAsync_ReplaysReasoningContentToolCallAndResultUnchangedAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        PreparedPass1WorkerPacket packet = CreatePacket();
        string validDocument = CreateReceiptFreeCandidateJson(packet);
        ReasoningReplayHandler transport = new(validDocument);
        PreparedPass1JobExecutor executor = CreateExecutor(transport, time);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Preserve all reasoning, content, tool calls, and tool results across requests.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(2, transport.RequestBodies.Count);
        AssertOutgoingBodiesWithinLimit(transport.RequestBodies);
        string replay = transport.RequestBodies[1];
        Assert.Contains("reasoning_content", replay, StringComparison.Ordinal);
        Assert.Contains("reasoning-unchanged", replay, StringComparison.Ordinal);
        Assert.Contains("content-unchanged", replay, StringComparison.Ordinal);
        Assert.Contains("call-replay-1", replay, StringComparison.Ordinal);
        Assert.Contains("\"role\":\"tool\"", replay, StringComparison.Ordinal);
        Assert.Contains("navigate-result-unchanged", replay, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_ReplayedAssistantToolCallContentIsAlwaysStringAsync(bool explicitNullContent)
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        PreparedPass1WorkerPacket packet = CreatePacket();
        string validDocument = CreateReceiptFreeCandidateJson(packet);
        AssistantContentReplayHandler transport = new(validDocument, explicitNullContent);
        PreparedPass1JobExecutor executor = CreateExecutor(transport, time);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Replay assistant tool calls with a string content field.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(2, transport.RequestBodies.Count);
        using JsonDocument replay = JsonDocument.Parse(transport.RequestBodies[1]);
        JsonElement assistant = replay.RootElement.GetProperty("messages")
            .EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "assistant");
        Assert.True(assistant.TryGetProperty("content", out JsonElement content));
        Assert.Equal(JsonValueKind.String, content.ValueKind);
        Assert.Equal(string.Empty, content.GetString());
    }

    [Fact]
    public async Task ExecuteAsync_IndividuallyLegalAssistantMessagesExceedAggregateWithoutLaterSendAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        AggregateAssistantOverflowHandler transport = new(8_500_000);
        RecordingWireBudgetDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            time,
            wireBudgetObserver: diagnostics);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Replay complete assistant messages, but reject their aggregate above 16777216 bytes before request three.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("provider-request-bound-exceeded", result.ReasonCode);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Equal(2, transport.RequestBodies.Count);
        AssertOutgoingBodiesWithinLimit(transport.RequestBodies);
        Assert.Equal(
            [PreparedPass1WireBudgetDiagnosticKind.OutgoingRequest],
            diagnostics.Categories);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationStopsStreamingTransportAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        CancellationCaptureHandler transport = new();
        PreparedPass1JobExecutor executor = CreateExecutor(transport, time);
        using CancellationTokenSource cancellation = new();

        Task<PreparedPass1RawWorkerResult> execution = executor.ExecuteAsync(
            CreatePacket(),
            "Stream until cancelled.",
            cancellation.Token);
        await transport.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await execution);
        await transport.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(transport.RequestBodies);
    }

    [Fact]
    public async Task ExecuteAsync_StreamingUpdatesResetInactivityDeadlineAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        PreparedPass1WorkerPacket packet = CreatePacket();
        string validDocument = CreateReceiptFreeCandidateJson(packet);
        TimedUpdateHandler transport = new(validDocument, TimeSpan.FromMilliseconds(10));
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            time,
            TimeSpan.FromMilliseconds(40),
            TimeSpan.FromSeconds(2));

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Keep the execution alive for every streamed update before submitting.");

        Assert.True(
            result.Outcome == PreparedPass1RawWorkerOutcome.Completed,
            $"Outcome={result.Outcome}; reason={result.ReasonCode ?? "<none>"}; requests={transport.RequestBodies.Count}");
        Assert.Equal(2, transport.RequestBodies.Count);
        AssertOutgoingBodiesWithinLimit(transport.RequestBodies);
    }

    [Fact]
    public async Task ExecuteAsync_AbsoluteCapDoesNotResetForStreamingUpdatesAsync()
    {
        ManualTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        PreparedPass1WorkerPacket packet = CreatePacket();
        AbsoluteCapHandler transport = new(time, TimeSpan.FromMilliseconds(50));
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            time,
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(250));

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "The absolute attempt cap must not reset for streamed updates.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal("provider-unavailable", result.ReasonCode);
        Assert.Single(transport.RequestBodies);
        Assert.Equal(250, time.Elapsed.TotalMilliseconds);
    }

    [Fact]
    public async Task ExecuteAsync_AssembledResponseAboveOriginalLimitRemainsLegalBelowRawAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        BudgetCaptureHandler transport = new(CreateOversizedReasoningSseForAssembledBytes(200_000));
        RecordingWireBudgetDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            time,
            wireBudgetObserver: diagnostics);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Allow an assembled streamed response above the original 131072-byte limit while it remains below raw.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("worker-result-rejected", result.ReasonCode);
        Assert.Single(transport.RequestBodies);
        Assert.Empty(diagnostics.Categories);
    }

    [Fact]
    public async Task ExecuteAsync_RawResponsePrecedesAssembledResponseWhenEqualCapsAreCrossedAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        RecordingWireBudgetDiagnosticObserver diagnostics = new();
        BudgetCaptureHandler transport = new(CreateOversizedReasoningSseForAssembledBytes(16_777_217));
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            time,
            wireBudgetObserver: diagnostics);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Raw transport framing wins when equal raw and assembled caps are crossed together.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("provider-request-bound-exceeded", result.ReasonCode);
        Assert.Single(transport.RequestBodies);
        await transport.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(
            [PreparedPass1WireBudgetDiagnosticKind.RawResponse],
            diagnostics.Categories);
    }

    [Fact]
    public async Task ExecuteAsync_RawResponseBoundaryPlusOneCancelsWithoutLaterSendAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        BudgetCaptureHandler transport = new(CreatePaddedSse(16_777_217));
        RecordingWireBudgetDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            time,
            wireBudgetObserver: diagnostics);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Reject raw streamed response bytes at 16 MiB plus one.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("provider-request-bound-exceeded", result.ReasonCode);
        Assert.Single(transport.RequestBodies);
        await transport.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(
            [PreparedPass1WireBudgetDiagnosticKind.RawResponse],
            diagnostics.Categories);
    }

    [Fact]
    public async Task ExecuteAsync_RawResponseBoundaryPlusOne_ReportsCategoryWhenTransportCancellationCallbackThrowsAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        BudgetCaptureHandler transport = new(
            CreatePaddedSse(16_777_217),
            throwOnCancellationCallback: true);
        RecordingWireBudgetDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            time,
            wireBudgetObserver: diagnostics);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Report the raw-response category even when transport cancellation unwinding throws.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("provider-request-bound-exceeded", result.ReasonCode);
        Assert.Single(transport.RequestBodies);
        await transport.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(
            [PreparedPass1WireBudgetDiagnosticKind.RawResponse],
            diagnostics.Categories);
    }

    [Fact]
    public async Task ExecuteAsync_RawResponseExactBoundaryRemainsLegalAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        string sse = CreatePaddedSse(16_777_216);
        Assert.Equal(16_777_216, Encoding.UTF8.GetByteCount(sse));
        BudgetCaptureHandler transport = new(sse);
        PreparedPass1JobExecutor executor = CreateExecutor(transport, time);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Accept the exact legal raw streamed response boundary.");

        // No RED is expected: the response is legal at exactly 16 MiB raw
        // bytes; it has no final function call, so ordinary result rejection
        // is the independent expected outcome.
        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("worker-result-rejected", result.ReasonCode);
        Assert.Single(transport.RequestBodies);
    }

    [Fact]
    public async Task ExecuteAsync_PreparedInitialMessageBoundaryPlusOneSendsNoRequestAsync()
    {
        FixedTimeProvider time = new(DateTimeOffset.Parse("2026-08-24T00:00:00+08:00"));
        WireCaptureHandler transport = new();
        PreparedPass1JobExecutor executor = CreateExecutor(transport, time);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            new string('p', 131_073));

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("provider-request-bound-exceeded", result.ReasonCode);
        Assert.Empty(transport.RequestBodies);
    }

    [Fact]
    public void PreparedPass1WireBudget_ExactLimitsAreLegalAndPlusOneIsClosed()
    {
        Assert.Equal(131_072, PreparedPass1WireBudget.MaximumPreparedInitialMessageBytes);
        Assert.Equal(81_920, PreparedPass1WireBudget.MaximumFunctionDeclarationsBytes);
        Assert.Equal(16_777_216, PreparedPass1WireBudget.MaximumRawResponseBytes);
        Assert.Equal(16_777_216, PreparedPass1WireBudget.MaximumAssembledResponseBytes);
        Assert.Equal(16_777_216, PreparedPass1WireBudget.MaximumAssistantContributionBytes);
        Assert.Equal(9, PreparedPass1WireBudget.MaximumAssistantContributionCount);
        Assert.Equal(393_216, PreparedPass1WireBudget.MaximumToolResultAggregateBytes);
        Assert.Equal(32_768, PreparedPass1WireBudget.FixedRequestEnvelopeMarginBytes);
        Assert.Equal(17_416_192, PreparedPass1WireBudget.MaximumOutgoingBodyBytes);
        Assert.Equal(
            131_072
                + 81_920
                + 16_777_216
                + 393_216
                + 32_768,
            PreparedPass1WireBudget.MaximumOutgoingBodyBytes);

        int[] nineLegalAssistantContributions = [
            1_864_135,
            1_864_135,
            1_864_135,
            1_864_135,
            1_864_135,
            1_864_135,
            1_864_135,
            1_864_135,
            1_864_136,
        ];
        int[] multipleLegalAssistantContributions = [8_388_608, 8_388_608];
        int[] multipleOverLimitAssistantContributions = [8_388_608, 8_388_609];
        int[] tenAssistantContributions = Enumerable.Repeat(1_677_721, 10).ToArray();

        bool Fits(
            int preparedInitialMessageBytes = 0,
            int functionDeclarationsBytes = 0,
            int rawResponseBytes = 0,
            int assembledResponseBytes = 0,
            int[]? assistantContributionBytes = null,
            int toolResultAggregateBytes = 0,
            int outgoingBodyBytes = 32_768) =>
            PreparedPass1WireBudget.IsWithinLimit(
                preparedInitialMessageBytes,
                functionDeclarationsBytes,
                rawResponseBytes,
                assembledResponseBytes,
                assistantContributionBytes ?? [],
                toolResultAggregateBytes,
                outgoingBodyBytes);

        Assert.True(Fits(preparedInitialMessageBytes: 131_072));
        Assert.True(Fits(functionDeclarationsBytes: 81_920));
        Assert.True(Fits(rawResponseBytes: 16_777_216));
        Assert.True(Fits(assembledResponseBytes: 16_777_216));
        Assert.True(Fits(assistantContributionBytes: [16_777_216]));
        Assert.True(Fits(assistantContributionBytes: multipleLegalAssistantContributions));
        Assert.True(Fits(assistantContributionBytes: nineLegalAssistantContributions));
        Assert.True(Fits(toolResultAggregateBytes: 393_216));
        Assert.True(Fits(outgoingBodyBytes: 17_416_192));

        Assert.True(PreparedPass1WireBudget.IsWithinLimit(
            preparedInitialMessageBytes: 131_072,
            functionDeclarationsBytes: 81_920,
            rawResponseBytes: 16_777_216,
            assembledResponseBytes: 16_777_216,
            assistantContributionBytes: nineLegalAssistantContributions,
            toolResultAggregateBytes: 393_216,
            outgoingBodyBytes: 17_416_192));

        Assert.False(PreparedPass1WireBudget.IsWithinLimit(
            preparedInitialMessageBytes: 131_073,
            functionDeclarationsBytes: 0,
            rawResponseBytes: 0,
            assembledResponseBytes: 0,
            assistantContributionBytes: [],
            toolResultAggregateBytes: 0,
            outgoingBodyBytes: 32_768));
        Assert.False(PreparedPass1WireBudget.IsWithinLimit(
            preparedInitialMessageBytes: 0,
            functionDeclarationsBytes: 81_921,
            rawResponseBytes: 0,
            assembledResponseBytes: 0,
            assistantContributionBytes: [],
            toolResultAggregateBytes: 0,
            outgoingBodyBytes: 32_768));
        Assert.False(PreparedPass1WireBudget.IsWithinLimit(
            preparedInitialMessageBytes: 0,
            functionDeclarationsBytes: 0,
            rawResponseBytes: 16_777_217,
            assembledResponseBytes: 0,
            assistantContributionBytes: [],
            toolResultAggregateBytes: 0,
            outgoingBodyBytes: 32_768));
        Assert.False(PreparedPass1WireBudget.IsWithinLimit(
            preparedInitialMessageBytes: 0,
            functionDeclarationsBytes: 0,
            rawResponseBytes: 0,
            assembledResponseBytes: 16_777_217,
            assistantContributionBytes: [],
            toolResultAggregateBytes: 0,
            outgoingBodyBytes: 32_768));
        Assert.False(PreparedPass1WireBudget.IsWithinLimit(
            preparedInitialMessageBytes: 0,
            functionDeclarationsBytes: 0,
            rawResponseBytes: 0,
            assembledResponseBytes: 0,
            assistantContributionBytes: [16_777_217],
            toolResultAggregateBytes: 0,
            outgoingBodyBytes: 32_768));
        Assert.False(PreparedPass1WireBudget.IsWithinLimit(
            preparedInitialMessageBytes: 0,
            functionDeclarationsBytes: 0,
            rawResponseBytes: 0,
            assembledResponseBytes: 0,
            assistantContributionBytes: tenAssistantContributions,
            toolResultAggregateBytes: 0,
            outgoingBodyBytes: 32_768));
        Assert.False(PreparedPass1WireBudget.IsWithinLimit(
            preparedInitialMessageBytes: 0,
            functionDeclarationsBytes: 0,
            rawResponseBytes: 0,
            assembledResponseBytes: 0,
            assistantContributionBytes: multipleOverLimitAssistantContributions,
            toolResultAggregateBytes: 0,
            outgoingBodyBytes: 32_768));
        Assert.False(PreparedPass1WireBudget.IsWithinLimit(
            preparedInitialMessageBytes: 0,
            functionDeclarationsBytes: 0,
            rawResponseBytes: 0,
            assembledResponseBytes: 0,
            assistantContributionBytes: [],
            toolResultAggregateBytes: 393_217,
            outgoingBodyBytes: 32_768));
        Assert.False(PreparedPass1WireBudget.IsWithinLimit(
            preparedInitialMessageBytes: 0,
            functionDeclarationsBytes: 0,
            rawResponseBytes: 0,
            assembledResponseBytes: 0,
            assistantContributionBytes: [],
            toolResultAggregateBytes: 0,
            outgoingBodyBytes: 17_416_193));
    }

    private static PreparedPass1JobExecutor CreateExecutor(
        HttpMessageHandler transport,
        TimeProvider timeProvider,
        TimeSpan? inactivityTimeout = null,
        TimeSpan? absoluteTimeout = null,
        IPreparedPass1WireBudgetDiagnosticObserver? wireBudgetObserver = null)
    {
        LlmProvider provider = new(
            "deepseek-v4-flash",
            "https://deepseek.test/v1",
            "ciphertext",
            ApiType.ChatCompletions,
            AgentType.Worker,
            timeProvider.GetUtcNow());
        WorkerAgentProviderResolver resolver = new(
            new NoOverrideRepository(),
            new SingleProviderRepository(provider));
        return new PreparedPass1JobExecutor(
            resolver,
            new PlaintextEncryptionService(),
            new PreparedNavigateCodeFunction(new NoOpNavigationService()),
            transport,
            timeProvider,
            inactivityTimeout ?? TimeSpan.FromMinutes(5),
            absoluteTimeout ?? TimeSpan.FromMinutes(25),
            null,
            wireBudgetObserver);
    }

    private static byte[]? InvokeDeepSeekNormalizer(string sourceJson)
    {
        return InvokeDeepSeekNormalizer(Encoding.UTF8.GetBytes(sourceJson));
    }

    private static byte[]? InvokeDeepSeekNormalizer(byte[] sourceBody)
    {
        MethodInfo method = RequireDeepSeekNormalizer();
        object? result = method.Invoke(null, [sourceBody]);
        return result is null ? null : Assert.IsType<byte[]>(result);
    }

    private static byte[]? InvokeTurnAwareDeepSeekNormalizer(string sourceJson, int providerTurn)
    {
        MethodInfo? method = typeof(PreparedPass1JobExecutor)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(candidate =>
            {
                if (candidate.Name != DeepSeekNormalizerName ||
                    candidate.ReturnType != typeof(byte[]))
                {
                    return false;
                }

                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Any(parameter => parameter.ParameterType == typeof(byte[])) &&
                    parameters.Any(parameter =>
                        parameter.ParameterType == typeof(int) ||
                        parameter.ParameterType == typeof(bool));
            });

        Assert.True(
            method is not null,
            "T132 RED: the DeepSeek final-wire normalizer must expose a provider-turn-aware public seam.");

        ParameterInfo[] methodParameters = method!.GetParameters();
        object?[] arguments = new object?[methodParameters.Length];
        for (int index = 0; index < methodParameters.Length; index++)
        {
            arguments[index] = methodParameters[index].ParameterType == typeof(byte[])
                ? Encoding.UTF8.GetBytes(sourceJson)
                : methodParameters[index].ParameterType == typeof(int)
                    ? providerTurn
                    : methodParameters[index].ParameterType == typeof(bool)
                    ? providerTurn >= 9
                    : null;
        }

        object? result = method.Invoke(null, arguments);
        return result is null ? null : Assert.IsType<byte[]>(result);
    }

    private static void AssertT132ToolChoice(string body, int requestOrdinal)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        JsonElement choice = root.GetProperty("tool_choice");
        if (requestOrdinal <= 8)
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

        JsonElement[] tools = root.GetProperty("tools").EnumerateArray().ToArray();
        if (requestOrdinal <= 8)
        {
            Assert.Equal(2, tools.Length);
            Assert.Equal(
                ["navigate_code", "submit_code_logic"],
                tools.Select(tool => tool.GetProperty("function").GetProperty("name").GetString())
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray());
        }
        else
        {
            Assert.Single(tools);
            Assert.Equal(
                "submit_code_logic",
                tools[0].GetProperty("function").GetProperty("name").GetString());
        }
        Assert.All(tools, tool => Assert.True(tool.GetProperty("function").GetProperty("strict").GetBoolean()));
    }

    private static void AssertT145DeclarationIdentity(
        string body,
        int requestOrdinal,
        object authority)
    {
        byte[] expected = requestOrdinal <= 8
            ? ReadBytes(authority, "ProjectedUtf8")
            : ReadBytes(authority, "ProjectedSubmitOnlyUtf8");
        string expectedSha256 = requestOrdinal <= 8
            ? ReadString(authority, "ProjectedSha256")
            : ReadString(authority, "ProjectedSubmitOnlySha256");
        using JsonDocument document = JsonDocument.Parse(body);
        byte[] actual = Encoding.UTF8.GetBytes(
            document.RootElement.GetProperty("tools").GetRawText());
        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(expected, actual);
        Assert.Equal(expectedSha256, Convert.ToHexString(SHA256.HashData(actual)));
    }

    private static object CreateDeclarationAuthority(PreparedPass1WorkerPacket packet)
    {
        AIFunction navigate = new PreparedNavigateCodeFunction(new NoOpNavigationService())
            .Create(new PreparedNavigateCodeBinding(
                Guid.Parse("11111111-1111-4111-8111-111111111111"),
                1,
                Guid.Parse("22222222-2222-4222-8222-222222222222")));
        AIFunction submit = new PreparedSubmitCodeLogicFunction()
            .CreateSession(packet)
            .Function;
        return DeepSeekStrictToolDeclarations.Create(navigate, submit);
    }

    private static MethodInfo RequireDeepSeekNormalizer()
    {
        MethodInfo? method = typeof(PreparedPass1JobExecutor)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(candidate =>
                candidate.Name == DeepSeekNormalizerName &&
                candidate.ReturnType == typeof(byte[]) &&
                candidate.GetParameters().Length == 1 &&
                candidate.GetParameters()[0].ParameterType == typeof(byte[]));

        Assert.True(
            method is not null,
            $"T091 RED: PreparedPass1JobExecutor must expose the executor-local {DeepSeekNormalizerName}(byte[]) source-shape seam.");
        return method!;
    }

    private static void AssertOutgoingBodiesWithinLimit(IEnumerable<string> bodies)
    {
        Assert.All(
            bodies,
            body => Assert.InRange(
                Encoding.UTF8.GetByteCount(body),
                0,
                17_416_192));
    }

    private sealed class RecordingWireBudgetDiagnosticObserver
        : IPreparedPass1WireBudgetDiagnosticObserver
    {
        public List<PreparedPass1WireBudgetDiagnosticKind> Categories { get; } = [];

        public void Observe(PreparedPass1WireBudgetDiagnosticKind category) =>
            Categories.Add(category);
    }

    private static PreparedPass1WorkerPacket CreatePacket()
    {
        byte[] source = Encoding.UTF8.GetBytes("class Example { }\n");
        string hash = new('a', 64);
        PreparedPass1ContextSnapshotProjection context = new(
            Guid.NewGuid(),
            _repositoryId,
            _scanRunId,
            new('b', 40),
            hash,
            hash,
            null,
            null,
            null);
        PreparedPass1ServerAuthorization authorization = new(
            _pairId,
            1,
            1,
            Guid.Parse("99999999-aaaa-4bbb-8ccc-dddddddddddd"),
            DateTimeOffset.UtcNow.AddMinutes(5),
            nameof(Pass1PairExecutionPhase.Running),
            Pass1CircuitState.Closed,
            ScanJobExecutionState.Running,
            PreparedPass1AuthorizationBinding.Create(new(
                Guid.NewGuid(),
                _systemId,
                [_repositoryId])));

        return new(
            _pairId,
            _jobId,
            _scanExecutionId,
            _systemId,
            _repositoryId,
            _scanRunId,
            new('b', 40),
            _fileCodeUnitId,
            hash,
            _manifestId,
            new('c', 64),
            new('d', 64),
            "src/Example.java",
            0,
            source.Length,
            1,
            1,
            1,
            1,
            hash,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(source)),
            "story-12-3-worker-v2",
            "code-logic/file-v2",
            new('e', 64),
            1,
            1,
            source,
            context,
            [])
        {
            ServerAuthorization = authorization,
        };
    }

    private sealed class WireCaptureHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];
        public List<Uri> RequestUris { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            const string sse =
                "data: {\"id\":\"wire-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"{}\"},\"finish_reason\":\"stop\"}]}\n\n" +
                "data: [DONE]\n\n";
            StreamContent content = new(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class FailureCaptureHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(status)
            {
                Content = new StringContent("provider failure"),
            };
        }
    }

    private sealed class BudgetCaptureHandler(
        string sse,
        bool throwOnCancellationCallback = false) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        public TaskCompletionSource<bool> CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            cancellationToken.Register(() =>
            {
                CancellationObserved.TrySetResult(true);
                if (throwOnCancellationCallback)
                {
                    throw new InvalidOperationException();
                }
            });
            StreamContent content = new(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private static string CreateOversizedReasoningSse(int reasoningByteCount)
    {
        string reasoning = new('r', reasoningByteCount);
        string chunk = JsonSerializer.Serialize(new
        {
            id = "budget-assembled-plus-one",
            @object = "chat.completion.chunk",
            choices = new[]
            {
                new
                {
                    index = 0,
                    delta = new { reasoning_content = reasoning },
                    finish_reason = (string?)null,
                },
            },
        });
        return $"data: {chunk}\n\ndata: [DONE]\n\n";
    }

    private static string CreateOversizedReasoningSseForAssembledBytes(int assembledByteCount) =>
        CreateOversizedReasoningSse(assembledByteCount - 24);

    private static string CreatePaddedSse(int totalByteCount)
    {
        const string prefix =
            "data: {\"id\":\"budget-raw-plus-one\",\"object\":\"chat.completion.chunk\",\"padding\":\"";
        const string suffix =
            "\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":null}]}\n\n";
        const string done = "data: [DONE]\n\n";
        int paddingByteCount = totalByteCount -
            Encoding.UTF8.GetByteCount(prefix + suffix + done);
        Assert.True(paddingByteCount > 0);
        return prefix + new string('p', paddingByteCount) + suffix + done;
    }

    private static string CreateRawSubmitSse(string document)
    {
        string arguments = JsonSerializer.Serialize($"{{\"document\":{document}}}");
        return
            $"data: {{\"id\":\"raw-submit\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{{\"index\":0,\"id\":\"call-raw-submit\",\"type\":\"function\",\"function\":{{\"name\":\"submit_code_logic\",\"arguments\":{arguments}}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\n" +
            "data: [DONE]\n\n";
    }

    private sealed class RawWireCaptureHandler(string sse) : HttpMessageHandler
    {
        public string Sse { get; } = sse;
        public int RawBytesRead { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _ = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            Stream stream = new CountingReadStream(
                Encoding.UTF8.GetBytes(Sse),
                count => RawBytesRead += count);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class CountingReadStream(byte[] bytes, Action<int> onRead) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count);
            onRead(read);
            return read;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            int read = _inner.Read(buffer, offset, count);
            onRead(read);
            return Task.FromResult(read);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int count = _inner.Read(buffer.Span);
            onRead(count);
            return ValueTask.FromResult(count);
        }

        public override void Flush() => _inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class LatestLegalTurnHandler(string validDocument) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];
        public List<string> FunctionNames { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int ordinal = RequestBodies.Count;
            string functionName = ordinal <= 8 ? "navigate_code" : "submit_code_logic";
            string arguments = ordinal <= 8
                ? "{\"operation\":\"Definition\",\"target\":{\"repositoryRelativePath\":\"src/Example.java\",\"symbol\":\"Example\",\"position\":null,\"exactLineRange\":null,\"expectedSourceHash\":\"ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff\"},\"depth\":1,\"parentObservationIdentity\":null}"
                : ordinal == 9
                    ? "{\"document\":{}}"
                    : $"{{\"document\":{validDocument}}}";
            FunctionNames.Add(functionName);
            string serializedArguments = JsonSerializer.Serialize(arguments);
            string sse =
                $"data: {{\"id\":\"turn-{ordinal}\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{{\"index\":0,\"id\":\"call-{ordinal}\",\"type\":\"function\",\"function\":{{\"name\":\"{functionName}\",\"arguments\":{serializedArguments}}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\n" +
                "data: [DONE]\n\n";
            StreamContent content = new(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class ReasoningReplayHandler(string validDocument) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int ordinal = RequestBodies.Count;
            string sse = ordinal == 1
                ?
                "data: {\"id\":\"replay-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"reasoning_content\":\"reasoning-unchanged\"},\"finish_reason\":null}]}\n\n" +
                "data: {\"id\":\"replay-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"content-unchanged\"},\"finish_reason\":null}]}\n\n" +
                "data: {\"id\":\"replay-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call-replay-1\",\"type\":\"function\",\"function\":{\"name\":\"navigate_code\",\"arguments\":\"{\\\"operation\\\":\\\"Definition\\\",\\\"target\\\":{\\\"repositoryRelativePath\\\":\\\"src/Example.java\\\",\\\"symbol\\\":\\\"Example\\\",\\\"position\\\":null,\\\"exactLineRange\\\":null,\\\"expectedSourceHash\\\":\\\"ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff\\\"},\\\"depth\\\":1,\\\"parentObservationIdentity\\\":null}\"}}]},\"finish_reason\":\"tool_calls\"}]}\n\n" +
                "data: [DONE]\n\n"
                :
                CreateSubmitSse(validDocument);
            StreamContent content = new(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }

        private static string CreateSubmitSse(string document)
        {
            string arguments = JsonSerializer.Serialize($"{{\"document\":{document}}}");
            return
                $"data: {{\"id\":\"replay-2\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{{\"index\":0,\"id\":\"call-submit-2\",\"type\":\"function\",\"function\":{{\"name\":\"submit_code_logic\",\"arguments\":{arguments}}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\n" +
                "data: [DONE]\n\n";
        }
    }

    private sealed class AssistantContentReplayHandler(
        string validDocument,
        bool explicitNullContent) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            string sse = RequestBodies.Count == 1
                ? CreateNavigateSse()
                : CreateSubmitSse();
            StreamContent content = new(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }

        private string CreateNavigateSse()
        {
            string optionalContent = explicitNullContent ? "\"content\":null," : string.Empty;
            string arguments = "{\"operation\":\"Definition\",\"target\":{\"repositoryRelativePath\":\"src/Example.java\",\"symbol\":\"Example\",\"position\":null,\"exactLineRange\":null,\"expectedSourceHash\":\"ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff\"},\"depth\":1,\"parentObservationIdentity\":null}";
            string serializedArguments = JsonSerializer.Serialize(arguments);
            return
                "data: {\"id\":\"content-replay-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{" +
                "\"role\":\"assistant\",\"reasoning_content\":\"reasoning-preserved\"," +
                optionalContent +
                "\"tool_calls\":[{\"index\":0,\"id\":\"content-call-1\",\"type\":\"function\",\"function\":{\"name\":\"navigate_code\",\"arguments\":" +
                serializedArguments +
                "}}]},\"finish_reason\":\"tool_calls\"}]}\n\n" +
                "data: [DONE]\n\n";
        }

        private string CreateSubmitSse()
        {
            string arguments = JsonSerializer.Serialize($"{{\"document\":{validDocument}}}");
            return
                $"data: {{\"id\":\"content-replay-2\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{{\"index\":0,\"id\":\"content-call-2\",\"type\":\"function\",\"function\":{{\"name\":\"submit_code_logic\",\"arguments\":{arguments}}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\n" +
                "data: [DONE]\n\n";
        }
    }

    private sealed class AggregateAssistantOverflowHandler(int reasoningByteCount) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int ordinal = RequestBodies.Count;
            if (ordinal > 2)
            {
                throw new InvalidOperationException("The aggregate bound must prevent request three.");
            }

            string arguments = JsonSerializer.Serialize(new
            {
                operation = "Definition",
                target = new
                {
                    repositoryRelativePath = "src/Example.java",
                    symbol = "Example",
                    position = (object?)null,
                    exactLineRange = (object?)null,
                    expectedSourceHash = new string('f', 64),
                },
                depth = 1,
                parentObservationIdentity = (string?)null,
            });
            string reasoning = JsonSerializer.Serialize(new
            {
                id = $"aggregate-{ordinal}",
                @object = "chat.completion.chunk",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new { reasoning_content = new string('r', reasoningByteCount) },
                        finish_reason = (string?)null,
                    },
                },
            });
            string toolCall = JsonSerializer.Serialize(new
            {
                id = $"aggregate-{ordinal}",
                @object = "chat.completion.chunk",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new
                        {
                            tool_calls = new[]
                            {
                                new
                                {
                                    index = 0,
                                    id = $"aggregate-call-{ordinal}",
                                    type = "function",
                                    function = new { name = "navigate_code", arguments },
                                },
                            },
                        },
                        finish_reason = "tool_calls",
                    },
                },
            });
            string sse = $"data: {reasoning}\n\ndata: {toolCall}\n\ndata: [DONE]\n\n";
            StreamContent content = new(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class CancellationCaptureHandler : HttpMessageHandler
    {
        public TaskCompletionSource<bool> RequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            RequestStarted.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved.TrySetResult(true);
                throw;
            }

            throw new InvalidOperationException("The cancellation handler must not return a response.");
        }
    }

    private sealed class TimedUpdateHandler(string validDocument, TimeSpan delay) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            string sse = RequestBodies.Count == 1
                ? CreateTimedNavigationSse()
                : CreateTimedSubmissionSse(validDocument);
            StreamContent content = new(new ChunkedReadStream(
                sse.Split("\u001f", StringSplitOptions.None)
                    .Select(value => Encoding.UTF8.GetBytes(value))
                    .ToArray(),
                delay));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }

        private static string CreateTimedNavigationSseValid()
        {
            string arguments = JsonSerializer.Serialize(new
            {
                operation = "Definition",
                target = new
                {
                    repositoryRelativePath = "src/Example.java",
                    symbol = "Example",
                    position = (object?)null,
                    exactLineRange = (object?)null,
                    expectedSourceHash = new string('f', 64),
                },
                depth = 1,
                parentObservationIdentity = (string?)null,
            });
            string reasoning = JsonSerializer.Serialize(new
            {
                id = "timed-1",
                @object = "chat.completion.chunk",
                choices = new[] { new { index = 0, delta = new { role = "assistant", reasoning_content = "reasoning-timed" }, finish_reason = (string?)null } },
            });
            string content = JsonSerializer.Serialize(new
            {
                id = "timed-1",
                @object = "chat.completion.chunk",
                choices = new[] { new { index = 0, delta = new { content = "content-timed" }, finish_reason = (string?)null } },
            });
            string toolCall = JsonSerializer.Serialize(new
            {
                id = "timed-1",
                @object = "chat.completion.chunk",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new
                        {
                            tool_calls = new[]
                            {
                                new
                                {
                                    index = 0,
                                    id = "call-timed-1",
                                    type = "function",
                                    function = new { name = "navigate_code", arguments },
                                },
                            },
                        },
                        finish_reason = "tool_calls",
                    },
                },
            });
            return string.Join(
                "\n\n\u001f",
                ["data: " + reasoning, "data: " + content, "data: " + toolCall, "data: [DONE]"])
                + "\n\n";
        }

        private static string CreateTimedNavigationSse() => CreateTimedNavigationSseValid();

        private static string CreateTimedSubmissionSse(string document)
        {
            string arguments = JsonSerializer.Serialize("{\"document\":" + document + "}");
            return
                "data: {\"id\":\"timed-2\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"tool_calls\":[{\"index\":0,\"id\":\"call-timed-2\",\"type\":\"function\",\"function\":{\"name\":\"submit_code_logic\",\"arguments\":" + arguments + "}}]},\"finish_reason\":\"tool_calls\"}]}\n\n\u001f" +
                "data: [DONE]\n\n";
        }
    }

    private sealed class ChunkedReadStream(
        byte[][] chunks,
        TimeSpan delay,
        Action? onChunkRead = null) : Stream
    {
        private int _index;
        private int _offset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => chunks.Sum(chunk => chunk.LongLength);
        public override long Position { get => _index; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_index >= chunks.Length)
            {
                return 0;
            }

            if (_offset == 0)
            {
                await Task.Delay(delay, cancellationToken);
            }

            byte[] chunk = chunks[_index];
            int count = Math.Min(buffer.Length, chunk.Length - _offset);
            chunk.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            if (_offset == chunk.Length)
            {
                _index++;
                _offset = 0;
                onChunkRead?.Invoke();
            }

            return count;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
    {
        private readonly object _sync = new();
        private readonly List<ManualTimer> _timers = [];
        private DateTimeOffset _current = current;

        public TimeSpan Elapsed => GetUtcNow() - _initial;

        private DateTimeOffset _initial = current;

        public override DateTimeOffset GetUtcNow()
        {
            lock (_sync)
            {
                return _current;
            }
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            ManualTimer timer = new(this, callback, state, dueTime, period);
            lock (_sync)
            {
                _timers.Add(timer);
            }

            return timer;
        }

        public void Advance(TimeSpan amount)
        {
            ManualTimer[] due;
            lock (_sync)
            {
                _current = _current.Add(amount);
                due = _timers.Where(timer => timer.IsDue(_current)).ToArray();
            }

            foreach (ManualTimer timer in due)
            {
                timer.Fire();
            }
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly ManualTimeProvider _owner;
            private readonly TimerCallback _callback;
            private readonly object? _state;
            private DateTimeOffset _dueAt;
            private TimeSpan _period;
            private bool _disposed;

            public ManualTimer(
                ManualTimeProvider owner,
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                _owner = owner;
                _callback = callback;
                _state = state;
                _dueAt = owner._current.Add(dueTime);
                _period = period;
            }

            public bool IsDue(DateTimeOffset now) => !_disposed && _dueAt <= now;

            public void Fire()
            {
                lock (_owner._sync)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    if (_period == Timeout.InfiniteTimeSpan)
                    {
                        _disposed = true;
                        _owner._timers.Remove(this);
                    }
                    else
                    {
                        _dueAt = _owner._current.Add(_period);
                    }
                }

                _callback(_state);
            }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (_owner._sync)
                {
                    if (_disposed)
                    {
                        return false;
                    }

                    _dueAt = _owner._current.Add(dueTime);
                    _period = period;
                    return true;
                }
            }

            public void Dispose()
            {
                lock (_owner._sync)
                {
                    _disposed = true;
                    _owner._timers.Remove(this);
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class AbsoluteCapHandler(ManualTimeProvider time, TimeSpan advancePerChunk) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            string[] chunks = Enumerable.Range(1, 8)
                .Select(index =>
                    $"data: {{\"id\":\"absolute-{index}\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"reasoning_content\":\"update-{index}\"}},\"finish_reason\":null}}]}}\n\n")
                .Append("data: [DONE]\n\n")
                .ToArray();
            StreamContent content = new(new ChunkedReadStream(
                chunks.Select(value => Encoding.UTF8.GetBytes(value)).ToArray(),
                TimeSpan.Zero,
                () => time.Advance(advancePerChunk)));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class NoOverrideRepository : ISystemLlmProviderOverrideRepository
    {
        public Task<SystemLlmProviderOverride?> GetBySystemAndAgentTypeAsync(
            Guid systemId,
            AgentType agentType,
            CancellationToken cancellationToken = default) => Task.FromResult<SystemLlmProviderOverride?>(null);

        public Task<IReadOnlyList<SystemLlmProviderOverride>> ListBySystemAsync(
            Guid systemId,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SystemLlmProviderOverride>>([]);

        public Task<SystemLlmProviderOverride?> GetBySystemAndIdAsync(
            Guid systemId,
            Guid overrideId,
            CancellationToken cancellationToken = default) => Task.FromResult<SystemLlmProviderOverride?>(null);

        public Task<SystemLlmProviderOverride> AddAsync(
            SystemLlmProviderOverride overrideEntry,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> RemoveAsync(
            Guid systemId,
            Guid overrideId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class SingleProviderRepository(LlmProvider provider) : ILlmProviderRepository
    {
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LlmProvider>>([provider]);

        public Task<LlmProvider?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) => Task.FromResult<LlmProvider?>(id == provider.Id ? provider : null);

        public Task<bool> NameExistsAsync(
            string name,
            Guid? excludeProviderId = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LlmProvider> AddAsync(
            LlmProvider value,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Remove(LlmProvider value) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PlaintextEncryptionService : ISecretEncryptionService
    {
        public Task<string> EncryptAsync(
            string purpose,
            string plain,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> DecryptAsync(
            string purpose,
            string cipher,
            CancellationToken cancellationToken = default) => Task.FromResult("test-api-key");
    }

    private sealed class NoOpNavigationService : IPass1NavigationService
    {
        public Task<PreparedPass1NavigationResponse> NavigateAsync(
            PreparedPass1NavigationRequestInput request,
            CancellationToken cancellationToken)
        {
            PreparedPass1NavigationResult result = new(
                "src/Example.java",
                null,
                new byte[32],
                [],
                [],
                "Example",
                null,
                null,
                "navigate-result-unchanged");
            return Task.FromResult(new PreparedPass1NavigationResponse(
                PreparedPass1NavigationOutcome.Unavailable,
                Guid.Empty,
                request.Operation,
                request.RequestedDepth,
                null,
                1,
                1,
                [result]));
        }
    }
}
