using System.Security.Cryptography;
using System.Text.Json;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.CommunicationDetection;

public static class CommunicationCatalogLoader
{
    private const int _maximumCatalogBytes = 64 * 1024;
    private static readonly HashSet<string> _rootProperties =
        ["schemaIdentity", "catalogId", "version", "rules"];
    private static readonly HashSet<string> _ruleProperties =
        [
            "identity", "pathGlob", "selector", "ancestor", "attributes", "extract",
            "removePrefix", "communicationKind", "rule", "provenance",
        ];

    public static CommunicationPatternCatalog LoadEmbedded()
    {
        const string resourceName =
            "Vulgata.Infrastructure.CommunicationDetection.Catalog.communication-pattern-catalog.v1.json";
        using Stream stream = typeof(CommunicationCatalogLoader).Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException("The embedded Communication Pattern Catalog is unavailable.");
        return Load(stream);
    }

    public static CommunicationPatternCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] content = new byte[_maximumCatalogBytes + 1];
        int length = 0;
        while (length < content.Length)
        {
            int read = stream.Read(content.AsSpan(length));
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        if (length is 0 or > _maximumCatalogBytes)
        {
            throw Invalid("Catalog content is empty or exceeds its byte bound.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                content.AsMemory(0, length),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32,
                });
        }
        catch (JsonException exception)
        {
            throw Invalid("Catalog JSON is invalid.", exception);
        }

        using (document)
        {
            Dictionary<string, JsonElement> root = ClosedObject(document.RootElement, _rootProperties);
            string schemaIdentity = RequiredString(root, "schemaIdentity", 128);
            if (!StringComparer.Ordinal.Equals(schemaIdentity, "communication-pattern-catalog/1.0"))
            {
                throw Invalid("Catalog schema identity is unsupported.");
            }

            string catalogId = RequiredToken(root, "catalogId", 128);
            string version = RequiredToken(root, "version", 64);
            JsonElement rulesElement = root["rules"];
            if (rulesElement.ValueKind != JsonValueKind.Array || rulesElement.GetArrayLength() > 256)
            {
                throw Invalid("Catalog rules must be a bounded array.");
            }

            var rules = new List<CommunicationPatternRule>(rulesElement.GetArrayLength());
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement element in rulesElement.EnumerateArray())
            {
                CommunicationPatternRule rule = ReadRule(element);
                if (!identities.Add(rule.Identity))
                {
                    throw Invalid("Catalog rule identities must be unique.");
                }

                rules.Add(rule);
            }

            string hash = Convert.ToHexStringLower(SHA256.HashData(content.AsSpan(0, length)));
            return new CommunicationPatternCatalog(schemaIdentity, catalogId, version, hash, rules);
        }
    }

    private static CommunicationPatternRule ReadRule(JsonElement element)
    {
        Dictionary<string, JsonElement> values = ClosedObject(element, _ruleProperties, requireAll: false);
        string[] required =
        [
            "identity", "pathGlob", "selector", "attributes", "extract", "removePrefix",
            "communicationKind", "rule", "provenance",
        ];
        if (required.Any(name => !values.ContainsKey(name)))
        {
            throw Invalid("Catalog rule is missing a required property.");
        }
        string identity = RequiredToken(values, "identity", 128);
        string pathGlob = RequiredString(values, "pathGlob", 2_048);
        ValidatePathGlob(pathGlob);
        IReadOnlyList<CommunicationPatternElementStep> selector = ReadSteps(values["selector"], required: true);
        IReadOnlyList<CommunicationPatternElementStep>? ancestor =
            !values.TryGetValue("ancestor", out JsonElement ancestorElement) ||
                ancestorElement.ValueKind == JsonValueKind.Null
                ? null
                : ReadSteps(ancestorElement, required: true);
        IReadOnlyList<CommunicationPatternAttributeTest> attributes = ReadAttributes(values["attributes"]);
        CommunicationPatternExtraction extraction = ReadExtraction(values["extract"]);
        string? removePrefix = OptionalString(values["removePrefix"], 256);

        if (!Enum.TryParse(RequiredString(values, "communicationKind", 32), false, out CommunicationKind kind) ||
            !Enum.IsDefined(kind) ||
            !Enum.TryParse(RequiredString(values, "rule", 64), false, out EvidenceRule rule) ||
            !Enum.IsDefined(rule) ||
            !Enum.TryParse(RequiredString(values, "provenance", 64), false, out EvidenceProvenance provenance) ||
            !Enum.IsDefined(provenance))
        {
            throw Invalid("Catalog classification values must be closed enum members.");
        }

        return new(
            identity,
            pathGlob,
            selector,
            ancestor,
            attributes,
            extraction,
            removePrefix,
            kind,
            rule,
            provenance);
    }

    private static IReadOnlyList<CommunicationPatternElementStep> ReadSteps(
        JsonElement element,
        bool required)
    {
        if (element.ValueKind != JsonValueKind.Array ||
            element.GetArrayLength() > 8 ||
            (required && element.GetArrayLength() == 0))
        {
            throw Invalid("Catalog element steps must contain one to eight exact steps.");
        }

        var result = new List<CommunicationPatternElementStep>(element.GetArrayLength());
        foreach (JsonElement item in element.EnumerateArray())
        {
            Dictionary<string, JsonElement> values = ClosedObject(
                item,
                ["namespaceUri", "localName"]);
            string namespaceUri = RequiredString(values, "namespaceUri", 512, allowEmpty: true);
            string localName = RequiredToken(values, "localName", 256);
            if (namespaceUri.Contains('*', StringComparison.Ordinal) ||
                localName.Contains('*', StringComparison.Ordinal))
            {
                throw Invalid("Catalog element names cannot contain wildcards.");
            }

            result.Add(new(namespaceUri, localName));
        }

        return result;
    }

    private static IReadOnlyList<CommunicationPatternAttributeTest> ReadAttributes(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > 8)
        {
            throw Invalid("Catalog attribute tests must be a bounded array.");
        }

        var result = new List<CommunicationPatternAttributeTest>(element.GetArrayLength());
        foreach (JsonElement item in element.EnumerateArray())
        {
            HashSet<string> allowed = ["namespaceUri", "localName", "present", "equals"];
            Dictionary<string, JsonElement> values = ClosedObject(item, allowed, requireAll: false);
            if (!values.Keys.Contains("namespaceUri") || !values.Keys.Contains("localName") ||
                values.Keys.Count is < 3 or > 3)
            {
                throw Invalid("Each attribute test must contain one presence or equality operation.");
            }

            string namespaceUri = RequiredString(values, "namespaceUri", 512, allowEmpty: true);
            string localName = RequiredToken(values, "localName", 256);
            bool hasPresence = values.TryGetValue("present", out JsonElement presence);
            bool hasEquality = values.TryGetValue("equals", out JsonElement equality);
            if (hasPresence == hasEquality ||
                (hasPresence && (presence.ValueKind != JsonValueKind.True)) ||
                (hasEquality && (equality.ValueKind != JsonValueKind.String ||
                    (equality.GetString()?.Length ?? -1) > SafeXmlReader.MaximumEvidenceCharacters)))
            {
                throw Invalid("Attribute tests support only presence or literal equality.");
            }

            result.Add(new(namespaceUri, localName, hasPresence, hasEquality ? equality.GetString() : null));
        }

        return result;
    }

    private static CommunicationPatternExtraction ReadExtraction(JsonElement element)
    {
        Dictionary<string, JsonElement> root = ClosedObject(
            element,
            ["attribute", "elementText"],
            requireAll: false);
        if (root.Count != 1)
        {
            throw Invalid("Catalog extraction must choose exactly one closed operation.");
        }

        if (root.TryGetValue("elementText", out JsonElement text))
        {
            if (text.ValueKind != JsonValueKind.True)
            {
                throw Invalid("Element-text extraction must be true.");
            }

            return new(CommunicationPatternExtractionKind.ElementText, null, null);
        }

        Dictionary<string, JsonElement> attribute = ClosedObject(
            root["attribute"],
            ["namespaceUri", "localName"]);
        return new(
            CommunicationPatternExtractionKind.Attribute,
            RequiredString(attribute, "namespaceUri", 512, allowEmpty: true),
            RequiredToken(attribute, "localName", 256));
    }

    private static void ValidatePathGlob(string value)
    {
        if (value.Length == 0 || value[0] is '/' or '\\' || Path.IsPathRooted(value) ||
            value.Contains('\\', StringComparison.Ordinal) || value.Any(char.IsControl))
        {
            throw Invalid("Catalog path glob must be a contained Repository-relative pattern.");
        }

        string[] segments = value.Split('/');
        if (segments.Any(segment => segment is "" or "." or "..") ||
            segments.Count(segment => segment == "**") > 1 ||
            segments.Any(segment => segment.Contains("**", StringComparison.Ordinal) && segment != "**") ||
            value.IndexOfAny(['(', ')', '[', ']', '{', '}', '|', '+', '^', '$', ':']) >= 0)
        {
            throw Invalid("Catalog path glob contains a prohibited grammar feature.");
        }
    }

    private static Dictionary<string, JsonElement> ClosedObject(
        JsonElement element,
        HashSet<string> allowed,
        bool requireAll = true)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("Catalog value must be an object.");
        }

        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name) || !values.TryAdd(property.Name, property.Value))
            {
                throw Invalid("Catalog object contains an unknown or duplicate property.");
            }
        }

        if (requireAll && values.Count != allowed.Count)
        {
            throw Invalid("Catalog object is missing a required property.");
        }

        return values;
    }

    private static string RequiredToken(
        IReadOnlyDictionary<string, JsonElement> values,
        string name,
        int maximumLength)
    {
        string value = RequiredString(values, name, maximumLength);
        if (value.Any(character =>
            char.IsControl(character) || char.IsWhiteSpace(character) || character > 0x7f))
        {
            throw Invalid("Catalog token is not a bounded ASCII token.");
        }

        return value;
    }

    private static string RequiredString(
        IReadOnlyDictionary<string, JsonElement> values,
        string name,
        int maximumLength,
        bool allowEmpty = false)
    {
        if (!values.TryGetValue(name, out JsonElement element) || element.ValueKind != JsonValueKind.String)
        {
            throw Invalid("Catalog string field is missing or invalid.");
        }

        string value = element.GetString()!;
        if ((!allowEmpty && value.Length == 0) || value.Length > maximumLength ||
            value.Length != value.Trim().Length || value.Any(char.IsControl))
        {
            throw Invalid("Catalog string field is outside its closed bound.");
        }

        return value;
    }

    private static string? OptionalString(JsonElement element, int maximumLength) =>
        element.ValueKind == JsonValueKind.Null
            ? null
            : element.ValueKind == JsonValueKind.String &&
                element.GetString() is { } value &&
                value.Length <= maximumLength &&
                value.Length == value.Trim().Length &&
                !value.Any(char.IsControl)
                ? value
                : throw Invalid("Catalog optional string is invalid.");

    private static InvalidDataException Invalid(string message, Exception? inner = null) =>
        new(message, inner);
}
