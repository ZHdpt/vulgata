using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// The deterministic provider-facing parameters schema derived from the
/// authoritative local <c>code-logic/file-v2</c> schema.
/// </summary>
/// <param name="ProjectedSchemaJson">The minified, fully inlined parameters schema.</param>
public sealed record DeepSeekStrictSchemaProjection(string ProjectedSchemaJson);

/// <summary>
/// Projects the hash-bound local result schema into DeepSeek's strict function subset.
/// </summary>
public static class DeepSeekStrictSchemaProjector
{
    private const string _resourceName =
        "Vulgata.Agents.Contracts.code-logic-file-v2.schema.json";
    private const string _authoritativeSchemaSha256 =
        "87C8FC21F261779A0D130F09ECCF574363B56E6D7138EA9D8933172E6CDAAC9A";
    private const string _communicationConditionalPointer =
        "/$defs/communicationEvidence/anyOf";
    private const string _communicationConditionalSha256 =
        "DEA6F2555EAD89D35C1E9C0909F676B335F8648B07EA3069C4849785854FDDD9";
    private const string _deferredConditionalPointer =
        "/$defs/deferredObservation/anyOf";
    private const string _deferredConditionalSha256 =
        "EBF64EBB6DDEC0B52D01CA493CE906165E69FF8CBCCA44E7AD3598517CE647A5";
    private const int _maximumProjectedSchemaBytes = 65_536;

    private static readonly HashSet<string> _recognizedKeywords = new(StringComparer.Ordinal)
    {
        "$schema", "$id", "$defs", "$ref", "title", "type",
        "additionalProperties", "required", "properties", "const", "enum",
        "pattern", "items", "minimum", "maximum", "minLength", "maxLength",
        "minItems", "maxItems", "uniqueItems", "allOf", "oneOf", "anyOf",
    };

    /// <summary>
    /// Builds the strict parameters schema from the sole embedded authoritative resource.
    /// </summary>
    /// <returns>The deterministic closed <c>document</c> wrapper.</returns>
    /// <exception cref="InvalidDataException">
    /// The authoritative resource, identity, references, conditionals, or supported
    /// schema structure drifted from the accepted projection contract.
    /// </exception>
    public static DeepSeekStrictSchemaProjection Build() =>
        ProjectOwned(LoadAuthoritativeSchema());

    /// <summary>
    /// Projects caller-supplied qualification bytes through the same hash-bound path.
    /// </summary>
    /// <param name="schemaBytes">Candidate authoritative schema bytes to qualify.</param>
    /// <returns>The deterministic closed <c>document</c> wrapper.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schemaBytes"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    /// The candidate does not exactly match the accepted authoritative schema or
    /// violates the strict projection contract.
    /// </exception>
    public static DeepSeekStrictSchemaProjection Project(byte[] schemaBytes)
    {
        ArgumentNullException.ThrowIfNull(schemaBytes);
        return ProjectOwned([.. schemaBytes]);
    }

    private static DeepSeekStrictSchemaProjection ProjectOwned(byte[] schemaBytes)
    {
        try
        {
            string actualHash = Convert.ToHexString(SHA256.HashData(schemaBytes));
            if (!string.Equals(actualHash, _authoritativeSchemaSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The authoritative Code Logic schema identity drifted.");
            }

            JsonObject schema = JsonNode.Parse(schemaBytes)?.AsObject() ??
                throw new InvalidDataException("The authoritative Code Logic schema must be one object.");
            JsonObject definitions = schema["$defs"]?.AsObject() ??
                throw new InvalidDataException("The authoritative Code Logic schema omits $defs.");
            ProjectionContext context = new(definitions);
            JsonObject projectedDocument = ProjectObject(schema, string.Empty, context);
            context.RequireCompleteConditionalLedger();

            JsonObject wrapper = new()
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("document"),
                ["properties"] = new JsonObject
                {
                    ["document"] = projectedDocument,
                },
            };
            string json = wrapper.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            if (Encoding.UTF8.GetByteCount(json) > _maximumProjectedSchemaBytes)
            {
                throw new InvalidDataException("The projected Code Logic schema exceeds its byte bound.");
            }

            return new DeepSeekStrictSchemaProjection(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The authoritative Code Logic schema is invalid JSON.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(schemaBytes);
        }
    }

    private static byte[] LoadAuthoritativeSchema()
    {
        Assembly assembly = typeof(DeepSeekStrictSchemaProjector).Assembly;
        string[] matchingResources = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith("code-logic-file-v2.schema.json", StringComparison.Ordinal))
            .ToArray();
        if (matchingResources.Length != 1 ||
            !string.Equals(matchingResources[0], _resourceName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Exactly one stable authoritative Code Logic schema resource is required.");
        }

        using Stream stream = assembly.GetManifestResourceStream(_resourceName) ??
            throw new InvalidDataException("The authoritative Code Logic schema resource is unavailable.");
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static JsonObject ProjectObject(
        JsonObject source,
        string pointer,
        ProjectionContext context)
    {
        RejectUnrecognizedKeywords(source, pointer);

        if (source.TryGetPropertyValue("$ref", out JsonNode? referenceNode))
        {
            if (source.Count != 1 || referenceNode is not JsonValue referenceValue ||
                !referenceValue.TryGetValue(out string? reference))
            {
                throw new InvalidDataException($"A schema reference has conflicting siblings at {Display(pointer)}.");
            }

            return context.Resolve(reference ?? string.Empty);
        }

        if (source.TryGetPropertyValue("allOf", out JsonNode? allOfNode))
        {
            if (source.Count != 1 || allOfNode is not JsonArray branches || branches.Count == 0)
            {
                throw new InvalidDataException($"An allOf node is unsupported at {Display(pointer)}.");
            }

            JsonObject merged = new();
            for (int index = 0; index < branches.Count; index++)
            {
                JsonObject branch = branches[index]?.AsObject() ??
                    throw new InvalidDataException($"An allOf branch is not an object at {Display(pointer)}.");
                Merge(merged, ProjectObject(branch, $"{pointer}/allOf/{index}", context), pointer);
            }

            return merged;
        }

        if (source.TryGetPropertyValue("oneOf", out JsonNode? oneOfNode))
        {
            if (source.Count != 1 || oneOfNode is not JsonArray alternatives ||
                alternatives.Count != 2 || !ContainsExactlyOneNullAlternative(alternatives))
            {
                throw new InvalidDataException($"Only a two-branch nullable oneOf is supported at {Display(pointer)}.");
            }

            JsonArray projectedAlternatives = [];
            for (int index = 0; index < alternatives.Count; index++)
            {
                projectedAlternatives.Add(ProjectObject(
                    alternatives[index]?.AsObject() ??
                        throw new InvalidDataException($"A oneOf branch is not an object at {Display(pointer)}."),
                    $"{pointer}/oneOf/{index}",
                    context));
            }

            return new JsonObject { ["anyOf"] = projectedAlternatives };
        }

        JsonObject result = new();
        foreach ((string keyword, JsonNode? value) in source)
        {
            switch (keyword)
            {
                case "$schema":
                case "$id":
                case "$defs":
                case "title":
                case "minLength":
                case "maxLength":
                case "minItems":
                case "maxItems":
                case "uniqueItems":
                    break;
                case "type":
                case "enum":
                case "pattern":
                case "minimum":
                case "maximum":
                    result[keyword] = value?.DeepClone();
                    break;
                case "const":
                    result["enum"] = new JsonArray(value?.DeepClone());
                    break;
                case "items":
                    result["items"] = ProjectObject(
                        value?.AsObject() ??
                            throw new InvalidDataException($"Schema items are not an object at {Display(pointer)}."),
                        $"{pointer}/items",
                        context);
                    break;
                case "properties":
                    result["properties"] = ProjectProperties(
                        value?.AsObject() ??
                            throw new InvalidDataException($"Schema properties are not an object at {Display(pointer)}."),
                        pointer,
                        context);
                    break;
                case "required":
                    result["required"] = ProjectRequired(value, pointer);
                    break;
                case "additionalProperties":
                    if (value is not JsonValue additionalProperties ||
                        !additionalProperties.TryGetValue(out bool isAllowed) || isAllowed)
                    {
                        throw new InvalidDataException($"A provider object is not closed at {Display(pointer)}.");
                    }

                    result["additionalProperties"] = false;
                    break;
                case "anyOf":
                    context.AcceptLocalOnlyConditional($"{pointer}/anyOf", value);
                    break;
                default:
                    throw new InvalidDataException($"Unsupported schema keyword '{keyword}' at {Display(pointer)}.");
            }
        }

        RequireClosedCompleteObject(source, result, pointer);
        return result;
    }

    private static JsonObject ProjectProperties(
        JsonObject properties,
        string pointer,
        ProjectionContext context)
    {
        JsonObject result = new();
        foreach ((string name, JsonNode? value) in properties)
        {
            result[name] = ProjectObject(
                value?.AsObject() ??
                    throw new InvalidDataException($"Property '{name}' is not a schema object at {Display(pointer)}."),
                $"{pointer}/properties/{EscapePointer(name)}",
                context);
        }

        return result;
    }

    private static JsonArray ProjectRequired(JsonNode? value, string pointer)
    {
        JsonArray source = value?.AsArray() ??
            throw new InvalidDataException($"Required is not an array at {Display(pointer)}.");
        JsonArray result = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonNode? item in source)
        {
            if (item is not JsonValue requiredValue ||
                !requiredValue.TryGetValue(out string? name) ||
                string.IsNullOrEmpty(name) || !names.Add(name))
            {
                throw new InvalidDataException($"Required contains an invalid entry at {Display(pointer)}.");
            }

            result.Add(name);
        }

        return result;
    }

    private static void RequireClosedCompleteObject(
        JsonObject source,
        JsonObject result,
        string pointer)
    {
        if (source["type"]?.GetValue<string>() != "object")
        {
            return;
        }

        JsonObject properties = result["properties"]?.AsObject() ??
            throw new InvalidDataException($"An object omits properties at {Display(pointer)}.");
        JsonArray required = result["required"]?.AsArray() ??
            throw new InvalidDataException($"An object omits required at {Display(pointer)}.");
        string[] propertyNames = properties.Select(property => property.Key).ToArray();
        string[] requiredNames = required.Select(node => node?.GetValue<string>()).OfType<string>().ToArray();
        if (!propertyNames.SequenceEqual(requiredNames, StringComparer.Ordinal) ||
            result["additionalProperties"]?.GetValue<bool>() != false)
        {
            throw new InvalidDataException($"An object is not closed with every property required at {Display(pointer)}.");
        }
    }

    private static bool ContainsExactlyOneNullAlternative(JsonArray alternatives)
    {
        int nullAlternatives = alternatives.Count(node =>
            node is JsonObject branch && branch.Count == 1 &&
            branch["type"]?.GetValue<string>() == "null");
        return nullAlternatives == 1;
    }

    private static void RejectUnrecognizedKeywords(JsonObject source, string pointer)
    {
        foreach (string keyword in source.Select(property => property.Key))
        {
            if (!_recognizedKeywords.Contains(keyword))
            {
                throw new InvalidDataException($"Unsupported schema keyword '{keyword}' at {Display(pointer)}.");
            }
        }
    }

    private static void Merge(JsonObject target, JsonObject source, string pointer)
    {
        foreach ((string keyword, JsonNode? value) in source)
        {
            if (!target.TryGetPropertyValue(keyword, out JsonNode? existing))
            {
                target[keyword] = value?.DeepClone();
                continue;
            }

            if (keyword == "pattern" && existing is JsonValue existingPatternValue &&
                value is JsonValue newPatternValue &&
                existingPatternValue.TryGetValue(out string? existingPattern) &&
                newPatternValue.TryGetValue(out string? newPattern))
            {
                target[keyword] = string.Equals(existingPattern, newPattern, StringComparison.Ordinal)
                    ? existingPattern
                    : $"(?={existingPattern})(?={newPattern})[\\s\\S]*";
                continue;
            }

            if (!JsonNode.DeepEquals(existing, value))
            {
                throw new InvalidDataException($"Conflicting allOf constraints occur at {Display(pointer)}.");
            }
        }
    }

    private static string EscapePointer(string value) =>
        value.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

    private static string Display(string pointer) => string.IsNullOrEmpty(pointer) ? "/" : pointer;

    private sealed class ProjectionContext(JsonObject definitions)
    {
        private readonly HashSet<string> _activeReferences = new(StringComparer.Ordinal);
        private readonly HashSet<string> _acceptedConditionals = new(StringComparer.Ordinal);

        public JsonObject Resolve(string reference)
        {
            const string prefix = "#/$defs/";
            if (!reference.StartsWith(prefix, StringComparison.Ordinal) ||
                reference.Length == prefix.Length || reference[prefix.Length..].Contains('/'))
            {
                throw new InvalidDataException("Only direct local $defs references are supported.");
            }

            string definitionName = reference[prefix.Length..];
            if (!_activeReferences.Add(definitionName))
            {
                throw new InvalidDataException($"Cyclic schema reference '{reference}' is unsupported.");
            }

            try
            {
                JsonObject definition = definitions[definitionName]?.AsObject() ??
                    throw new InvalidDataException($"Unresolved schema reference '{reference}'.");
                return ProjectObject(definition, $"/$defs/{EscapePointer(definitionName)}", this);
            }
            finally
            {
                _activeReferences.Remove(definitionName);
            }
        }

        public void AcceptLocalOnlyConditional(string pointer, JsonNode? conditional)
        {
            string expectedHash;
            string[] expectedBranchProperties;
            if (string.Equals(pointer, _communicationConditionalPointer, StringComparison.Ordinal))
            {
                expectedHash = _communicationConditionalSha256;
                expectedBranchProperties = ["classificationEvidence", "navigationReceiptIds"];
            }
            else if (string.Equals(pointer, _deferredConditionalPointer, StringComparison.Ordinal))
            {
                expectedHash = _deferredConditionalSha256;
                expectedBranchProperties = ["targetSymbol", "targetRepositoryRelativePath"];
            }
            else
            {
                throw new InvalidDataException($"An unclassified conditional occurs at {Display(pointer)}.");
            }

            if (!_acceptedConditionals.Add(pointer) || conditional is not JsonArray branches ||
                branches.Count != 2)
            {
                throw new InvalidDataException($"Conditional kind or cardinality drifted at {pointer}.");
            }

            string[] branchProperties = branches.Select(branch =>
                branch?.AsObject()["properties"]?.AsObject().Single().Key ?? string.Empty).ToArray();
            string actualHash = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(branches.ToJsonString(new JsonSerializerOptions { WriteIndented = false }))));
            if (!branchProperties.SequenceEqual(expectedBranchProperties, StringComparer.Ordinal) ||
                !string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Conditional semantics drifted at {pointer}.");
            }
        }

        public void RequireCompleteConditionalLedger()
        {
            if (!_acceptedConditionals.SetEquals(
                [_communicationConditionalPointer, _deferredConditionalPointer]))
            {
                throw new InvalidDataException("The local-only conditional ledger is incomplete.");
            }
        }
    }
}
