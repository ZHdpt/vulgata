using System.Reflection;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

public sealed class PreparedPass2FlowCatalog
{
    public const string FixedResourceName =
        "Vulgata.Agents.PreparedFlows.demo-business-flow.v1.json";
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    private PreparedPass2FlowCatalog(string resourceName, PreparedPass2Flow flow)
    {
        ResourceName = resourceName;
        Flow = flow;
    }

    public string ResourceName { get; }
    public PreparedPass2Flow Flow { get; }

    public static PreparedPass2FlowCatalog Load(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        PreparedPass2FlowResource[] resources = assembly.GetManifestResourceNames()
            .Select(name => new PreparedPass2FlowResource(
                name,
                () => assembly.GetManifestResourceStream(name) ??
                    throw new PreparedPass2FlowContractException()))
            .ToArray();
        return Load(resources);
    }

    internal static PreparedPass2FlowCatalog Load(
        IReadOnlyList<PreparedPass2FlowResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        PreparedPass2FlowResource[] matches = resources
            .Where(resource => StringComparer.Ordinal.Equals(
                resource.Name,
                FixedResourceName))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new PreparedPass2FlowContractException();
        }

        byte[] utf8Json;
        using (Stream stream = matches[0].Open())
        {
            if (!stream.CanRead ||
                stream.CanSeek && stream.Length is < 1 or >
                    PreparedPass2FlowLimits.MaximumManifestUtf8Bytes)
            {
                throw new PreparedPass2FlowContractException();
            }

            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            utf8Json = memory.ToArray();
        }

        if (utf8Json.Length is < 1 or > PreparedPass2FlowLimits.MaximumManifestUtf8Bytes)
        {
            throw new PreparedPass2FlowContractException();
        }

        ValidateJson(utf8Json);
        try
        {
            _ = _strictUtf8.GetCharCount(utf8Json);
            using JsonDocument document = JsonDocument.Parse(
                utf8Json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = PreparedPass2FlowLimits.MaximumJsonDepth,
                });
            JsonElement root = document.RootElement;
            RequireProperties(
                root,
                "schemaIdentity",
                "manifestId",
                "flowId",
                "systemId",
                "repositoryId",
                "members",
                "traceEvidence");
            PreparedPass2FlowMember[] members = RequireArray(root, "members")
                .EnumerateArray()
                .Select(ParseMember)
                .ToArray();
            PreparedPass2TraceEvidence[] evidence = RequireArray(root, "traceEvidence")
                .EnumerateArray()
                .Select(ParseEvidence)
                .ToArray();
            PreparedPass2Flow flow = PreparedPass2Flow.Create(
                RequireString(root, "schemaIdentity"),
                RequireGuid(root, "manifestId"),
                RequireGuid(root, "flowId"),
                RequireGuid(root, "systemId"),
                RequireGuid(root, "repositoryId"),
                members,
                evidence);
            return new(FixedResourceName, flow);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or
                FormatException or DecoderFallbackException)
        {
            throw new PreparedPass2FlowContractException();
        }
    }

    private static PreparedPass2FlowMember ParseMember(JsonElement element)
    {
        RequireProperties(element, "order", "jobId", "codeUnitId", "logicalDocumentId");
        return new(
            RequireInt32(element, "order"),
            RequireGuid(element, "jobId"),
            RequireGuid(element, "codeUnitId"),
            RequireGuid(element, "logicalDocumentId"));
    }

    private static PreparedPass2TraceEvidence ParseEvidence(JsonElement element)
    {
        RequireProperties(
            element,
            "order",
            "memberJobId",
            "codeUnitId",
            "label",
            "locator");
        return new(
            RequireInt32(element, "order"),
            RequireGuid(element, "memberJobId"),
            RequireGuid(element, "codeUnitId"),
            RequireString(element, "label"),
            RequireString(element, "locator"));
    }

    private static void RequireProperties(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new PreparedPass2FlowContractException();
        }

        string[] actual = element.EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        if (actual.Length != expected.Length ||
            !actual.ToHashSet(StringComparer.Ordinal).SetEquals(expected))
        {
            throw new PreparedPass2FlowContractException();
        }
    }

    private static JsonElement RequireArray(JsonElement element, string propertyName)
    {
        JsonElement value = element.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new PreparedPass2FlowContractException();
        }

        return value;
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        JsonElement value = element.GetProperty(propertyName);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new PreparedPass2FlowContractException();
    }

    private static Guid RequireGuid(JsonElement element, string propertyName) =>
        Guid.Parse(RequireString(element, propertyName));

    private static int RequireInt32(JsonElement element, string propertyName)
    {
        JsonElement value = element.GetProperty(propertyName);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result)
            ? result
            : throw new PreparedPass2FlowContractException();
    }

    private static void ValidateJson(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            var reader = new Utf8JsonReader(
                utf8Json,
                new JsonReaderOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = PreparedPass2FlowLimits.MaximumJsonDepth,
                });
            var properties = new Stack<HashSet<string>>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    properties.Push(new(StringComparer.Ordinal));
                }
                else if (reader.TokenType == JsonTokenType.PropertyName &&
                    (properties.Count == 0 || !properties.Peek().Add(reader.GetString()!)))
                {
                    throw new PreparedPass2FlowContractException();
                }
                else if (reader.TokenType == JsonTokenType.EndObject)
                {
                    _ = properties.Pop();
                }
            }

            if (properties.Count != 0 || reader.BytesConsumed != utf8Json.Length)
            {
                throw new PreparedPass2FlowContractException();
            }
        }
        catch (JsonException)
        {
            throw new PreparedPass2FlowContractException();
        }
    }
}

internal sealed record PreparedPass2FlowResource(string Name, Func<Stream> Open);
