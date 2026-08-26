using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Loads, verifies, projects, and validates the sole authoritative Pass 2
/// Business Logic schema resource.
/// </summary>
public static class Pass2BusinessLogicSchemaContract
{
    public const string ResourceName =
        "Vulgata.Core.Contracts.business-logic-flow-v1.schema.json";
    private const string AcceptedSha256 =
        "758ECCC19EEF53FE9A510BD1627C6700C4FBA77BB95A72660DE6B6924131553B";
    private const int MaximumSchemaBytes = 65_536;
    private static readonly Lazy<JsonElement> _documentSchema = new(LoadAndVerify);

    public static bool IsValidDocument(JsonElement candidate) =>
        Validate(candidate, _documentSchema.Value, _documentSchema.Value);

    public static JsonElement LoadDocumentParametersSchema()
    {
        JsonObject source = JsonNode.Parse(_documentSchema.Value.GetRawText())?.AsObject() ??
            throw new InvalidDataException("The authoritative Business Logic schema is not an object.");
        JsonObject definitions = source["$defs"]?.AsObject() ??
            throw new InvalidDataException("The authoritative Business Logic schema omits its definitions.");
        JsonObject projectedDocument = ProjectObject(
            source,
            definitions,
            new HashSet<string>(StringComparer.Ordinal));

        using var destination = new MemoryStream();
        using (var writer = new Utf8JsonWriter(destination))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "object");
            writer.WriteBoolean("additionalProperties", false);
            writer.WriteStartArray("required");
            writer.WriteStringValue("document");
            writer.WriteEndArray();
            writer.WriteStartObject("properties");
            writer.WritePropertyName("document");
            projectedDocument.WriteTo(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        using JsonDocument projected = JsonDocument.Parse(destination.ToArray());
        return projected.RootElement.Clone();
    }

    private static JsonElement LoadAndVerify()
    {
        Type owner = typeof(Pass2BusinessLogicSchemaContract);
        string[] matches = owner.Assembly.GetManifestResourceNames()
            .Where(name => string.Equals(name, ResourceName, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException(
                "Exactly one authoritative Business Logic schema resource is required.");
        }

        using Stream stream = owner.Assembly.GetManifestResourceStream(matches[0]) ??
            throw new InvalidDataException(
                "The authoritative Business Logic schema resource is unavailable.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        byte[] bytes = copy.ToArray();
        try
        {
            if (bytes.Length is 0 or > MaximumSchemaBytes ||
                !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), AcceptedSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The authoritative Business Logic schema identity drifted.");
            }

            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("$id", out JsonElement id) ||
                !string.Equals(id.GetString(), "urn:vulgata:schema:business-logic:flow-v1",
                    StringComparison.Ordinal) ||
                !root.TryGetProperty("additionalProperties", out JsonElement closed) ||
                closed.ValueKind != JsonValueKind.False)
            {
                throw new InvalidDataException(
                    "The authoritative Business Logic schema is not the accepted closed contract.");
            }

            return root.Clone();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static bool Validate(JsonElement value, JsonElement schema, JsonElement root)
    {
        if (schema.TryGetProperty("$ref", out JsonElement reference))
        {
            const string prefix = "#/$defs/";
            string? path = reference.GetString();
            if (path is null || !path.StartsWith(prefix, StringComparison.Ordinal) ||
                path[prefix.Length..].Contains('/', StringComparison.Ordinal) ||
                !root.GetProperty("$defs").TryGetProperty(path[prefix.Length..], out JsonElement target))
            {
                return false;
            }

            return Validate(value, target, root);
        }

        if (schema.TryGetProperty("allOf", out JsonElement allOf) &&
            !allOf.EnumerateArray().All(branch => Validate(value, branch, root)))
        {
            return false;
        }

        if (schema.TryGetProperty("type", out JsonElement type) && !MatchesType(value, type.GetString()))
        {
            return false;
        }

        if (schema.TryGetProperty("const", out JsonElement constant) &&
            !JsonElement.DeepEquals(value, constant))
        {
            return false;
        }

        if (schema.TryGetProperty("enum", out JsonElement choices) &&
            !choices.EnumerateArray().Any(choice => JsonElement.DeepEquals(value, choice)))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Object => ValidateObject(value, schema, root),
            JsonValueKind.Array => ValidateArray(value, schema, root),
            JsonValueKind.String => ValidateString(value.GetString()!, schema),
            JsonValueKind.Number => ValidateInteger(value, schema),
            _ => true,
        };
    }

    private static bool ValidateObject(JsonElement value, JsonElement schema, JsonElement root)
    {
        if (!schema.TryGetProperty("properties", out JsonElement properties))
        {
            return true;
        }

        string[] required = schema.TryGetProperty("required", out JsonElement requiredValues)
            ? requiredValues.EnumerateArray().Select(item => item.GetString()!).ToArray()
            : [];
        if (required.Any(name => !value.TryGetProperty(name, out _)))
        {
            return false;
        }

        bool closed = schema.TryGetProperty("additionalProperties", out JsonElement additional) &&
            additional.ValueKind == JsonValueKind.False;
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!properties.TryGetProperty(property.Name, out JsonElement propertySchema))
            {
                if (closed)
                {
                    return false;
                }

                continue;
            }

            if (!Validate(property.Value, propertySchema, root))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateArray(JsonElement value, JsonElement schema, JsonElement root)
    {
        int count = value.GetArrayLength();
        if (schema.TryGetProperty("minItems", out JsonElement minimum) && count < minimum.GetInt32() ||
            schema.TryGetProperty("maxItems", out JsonElement maximum) && count > maximum.GetInt32())
        {
            return false;
        }

        JsonElement[] items = value.EnumerateArray().ToArray();
        if (schema.TryGetProperty("uniqueItems", out JsonElement unique) && unique.GetBoolean() &&
            items.Where((item, index) => items.Take(index)
                .Any(previous => JsonElement.DeepEquals(previous, item))).Any())
        {
            return false;
        }

        return !schema.TryGetProperty("items", out JsonElement itemSchema) ||
            items.All(item => Validate(item, itemSchema, root));
    }

    private static bool ValidateString(string value, JsonElement schema)
    {
        int length = value.EnumerateRunes().Count();
        if (schema.TryGetProperty("minLength", out JsonElement minimum) && length < minimum.GetInt32() ||
            schema.TryGetProperty("maxLength", out JsonElement maximum) && length > maximum.GetInt32())
        {
            return false;
        }

        return !schema.TryGetProperty("pattern", out JsonElement pattern) ||
            Regex.IsMatch(value, pattern.GetString()!, RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
    }

    private static bool ValidateInteger(JsonElement value, JsonElement schema)
    {
        if (!value.TryGetInt64(out long integer))
        {
            return false;
        }

        return (!schema.TryGetProperty("minimum", out JsonElement minimum) ||
                integer >= minimum.GetInt64()) &&
            (!schema.TryGetProperty("maximum", out JsonElement maximum) ||
                integer <= maximum.GetInt64());
    }

    private static bool MatchesType(JsonElement value, string? type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        _ => false,
    };

    private static JsonObject ProjectObject(
        JsonObject source,
        JsonObject definitions,
        HashSet<string> activeReferences)
    {
        if (source.TryGetPropertyValue("$ref", out JsonNode? referenceNode))
        {
            if (source.Count != 1 || referenceNode is not JsonValue referenceValue ||
                !referenceValue.TryGetValue(out string? reference) || reference is null ||
                !reference.StartsWith("#/$defs/", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The Business Logic schema contains an unsupported reference.");
            }

            string definitionName = reference["#/$defs/".Length..];
            if (definitionName.Length == 0 || definitionName.Contains('/', StringComparison.Ordinal) ||
                !activeReferences.Add(definitionName))
            {
                throw new InvalidDataException("The Business Logic schema contains a cyclic reference.");
            }

            try
            {
                return ProjectObject(definitions[definitionName]?.AsObject() ??
                    throw new InvalidDataException("The Business Logic schema contains an unresolved reference."),
                    definitions, activeReferences);
            }
            finally
            {
                activeReferences.Remove(definitionName);
            }
        }

        if (source.TryGetPropertyValue("allOf", out JsonNode? allOfNode))
        {
            if (source.Count != 1 || allOfNode is not JsonArray branches || branches.Count == 0)
            {
                throw new InvalidDataException("The Business Logic schema contains an unsupported allOf.");
            }

            JsonObject merged = [];
            foreach (JsonNode? branch in branches)
            {
                Merge(merged, ProjectObject(branch?.AsObject() ??
                    throw new InvalidDataException("The Business Logic schema contains a malformed allOf."),
                    definitions, activeReferences));
            }

            return merged;
        }

        JsonObject result = [];
        foreach ((string keyword, JsonNode? value) in source)
        {
            switch (keyword)
            {
                case "$schema": case "$id": case "$defs": case "title": break;
                case "type": case "enum": case "pattern": case "minimum": case "maximum":
                case "minLength": case "maxLength": case "minItems": case "maxItems":
                case "uniqueItems": case "required": case "additionalProperties":
                    result[keyword] = value?.DeepClone();
                    break;
                case "const":
                    result["enum"] = new JsonArray(value?.DeepClone());
                    break;
                case "items":
                    result["items"] = ProjectObject(value?.AsObject() ??
                        throw new InvalidDataException("The Business Logic schema contains malformed items."),
                        definitions, activeReferences);
                    break;
                case "properties":
                    JsonObject projected = [];
                    foreach ((string name, JsonNode? property) in value?.AsObject() ??
                        throw new InvalidDataException("The Business Logic schema contains malformed properties."))
                    {
                        projected[name] = ProjectObject(property?.AsObject() ??
                            throw new InvalidDataException("The Business Logic schema contains a malformed property."),
                            definitions, activeReferences);
                    }
                    result["properties"] = projected;
                    break;
                default:
                    throw new InvalidDataException($"The Business Logic schema uses unsupported keyword '{keyword}'.");
            }
        }

        return result;
    }

    private static void Merge(JsonObject target, JsonObject source)
    {
        foreach ((string keyword, JsonNode? value) in source)
        {
            if (!target.TryGetPropertyValue(keyword, out JsonNode? existing))
            {
                target[keyword] = value?.DeepClone();
            }
            else if (keyword == "pattern" && existing is JsonValue existingPattern &&
                value is JsonValue newPattern && existingPattern.TryGetValue(out string? left) &&
                newPattern.TryGetValue(out string? right))
            {
                target[keyword] = string.Equals(left, right, StringComparison.Ordinal)
                    ? left : $"(?={left})(?={right})[\\s\\S]*";
            }
            else if (keyword is "minLength" or "minItems" or "minimum" &&
                existing is JsonValue existingMinimum && value is JsonValue newMinimum &&
                existingMinimum.TryGetValue(out int leftMinimum) && newMinimum.TryGetValue(out int rightMinimum))
            {
                target[keyword] = Math.Max(leftMinimum, rightMinimum);
            }
            else if (keyword is "maxLength" or "maxItems" or "maximum" &&
                existing is JsonValue existingMaximum && value is JsonValue newMaximum &&
                existingMaximum.TryGetValue(out int leftMaximum) && newMaximum.TryGetValue(out int rightMaximum))
            {
                target[keyword] = Math.Min(leftMaximum, rightMaximum);
            }
            else if (!JsonNode.DeepEquals(existing, value))
            {
                throw new InvalidDataException("The Business Logic schema contains conflicting constraints.");
            }
        }
    }
}
