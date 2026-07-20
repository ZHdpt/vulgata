using System.Text.Json;

namespace Vulgata.Infrastructure.FrameworkDetection;

public sealed class FrameworkCatalog
{
    private const string _resourceName =
        "Vulgata.Infrastructure.FrameworkDetection.Catalog.framework-catalog.v1.json";
    private static readonly HashSet<string> _rootProperties =
        ["version", "entries"];
    private static readonly HashSet<string> _entryProperties =
        ["ecosystem", "packageCoordinate", "frameworkName", "allowedLanguages", "evidenceKinds"];
    private static readonly HashSet<string> _ecosystems =
        ["nuget", "npm", "maven", "python-requirements"];
    private static readonly HashSet<string> _evidenceKinds =
        ["direct", "paired-lock"];
    private readonly IReadOnlyDictionary<string, FrameworkCatalogEntry> _entriesByCoordinate;

    private FrameworkCatalog(
        string version,
        string sha256,
        IReadOnlyList<FrameworkCatalogEntry> entries)
    {
        Version = version;
        Sha256 = sha256;
        Entries = Array.AsReadOnly(entries.ToArray());
        _entriesByCoordinate = Entries.ToDictionary(
            entry => entry.PackageCoordinate,
            StringComparer.Ordinal);
    }

    public string Version { get; }
    public string Sha256 { get; }
    public IReadOnlyList<FrameworkCatalogEntry> Entries { get; }

    public FrameworkCatalogEntry? FindExact(string packageCoordinate)
    {
        ArgumentNullException.ThrowIfNull(packageCoordinate);
        return _entriesByCoordinate.GetValueOrDefault(packageCoordinate);
    }

    public static FrameworkCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        byte[] content = buffer.ToArray();

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                content,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 16,
                });
        }
        catch (JsonException)
        {
            throw Invalid("Framework catalog JSON is invalid.");
        }

        using (document)
        {
            IReadOnlyDictionary<string, JsonElement> root = ReadClosedObject(
                document.RootElement,
                _rootProperties,
                "Framework catalog root is invalid.");
            string version = ReadRequiredString(root, "version");
            if (!StringComparer.Ordinal.Equals(version, "1"))
            {
                throw Invalid("Framework catalog version is unsupported.");
            }

            JsonElement entriesElement = ReadRequired(root, "entries");
            if (entriesElement.ValueKind != JsonValueKind.Array ||
                entriesElement.GetArrayLength() == 0)
            {
                throw Invalid("Framework catalog entries are invalid.");
            }

            var entries = new List<FrameworkCatalogEntry>(entriesElement.GetArrayLength());
            var coordinates = new Dictionary<string, FrameworkCatalogEntry>(StringComparer.Ordinal);
            foreach (JsonElement element in entriesElement.EnumerateArray())
            {
                FrameworkCatalogEntry entry = ReadEntry(element);
                if (coordinates.TryGetValue(entry.PackageCoordinate, out FrameworkCatalogEntry? existing))
                {
                    string message = StringComparer.Ordinal.Equals(
                        existing.FrameworkName,
                        entry.FrameworkName)
                        ? "Framework catalog contains a duplicate coordinate."
                        : "Framework catalog contains an ambiguous coordinate.";
                    throw Invalid(message);
                }

                coordinates.Add(entry.PackageCoordinate, entry);
                entries.Add(entry);
            }

            return new FrameworkCatalog(
                version,
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content)),
                entries.ToArray());
        }
    }

    public static FrameworkCatalog LoadEmbedded()
    {
        using Stream resource = typeof(FrameworkCatalog).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException("Embedded framework catalog is unavailable.");
        return Load(resource);
    }

    private static FrameworkCatalogEntry ReadEntry(JsonElement element)
    {
        IReadOnlyDictionary<string, JsonElement> values = ReadClosedObject(
            element,
            _entryProperties,
            "Framework catalog entry is invalid.");
        string ecosystem = ReadRequiredString(values, "ecosystem");
        string packageCoordinate = ReadRequiredString(values, "packageCoordinate");
        string frameworkName = ReadRequiredString(values, "frameworkName");
        string[] allowedLanguages = ReadStringArray(values, "allowedLanguages");
        string[] evidenceKinds = ReadStringArray(values, "evidenceKinds");

        if (!_ecosystems.Contains(ecosystem) ||
            !CoordinateMatchesEcosystem(ecosystem, packageCoordinate) ||
            !IsNormalizedToken(frameworkName, allowLanguageCharacters: false) ||
            allowedLanguages.Any(language => !IsNormalizedToken(language, allowLanguageCharacters: true)) ||
            evidenceKinds.Any(kind => !_evidenceKinds.Contains(kind)) ||
            evidenceKinds.Contains("paired-lock", StringComparer.Ordinal) &&
            ecosystem is not ("nuget" or "npm"))
        {
            throw Invalid("Framework catalog entry contains an unsupported value.");
        }

        return new FrameworkCatalogEntry(
            ecosystem,
            packageCoordinate,
            frameworkName,
            Array.AsReadOnly(allowedLanguages),
            Array.AsReadOnly(evidenceKinds));
    }

    private static IReadOnlyDictionary<string, JsonElement> ReadClosedObject(
        JsonElement element,
        IReadOnlySet<string> allowedProperties,
        string error)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(error);
        }

        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name) ||
                !values.TryAdd(property.Name, property.Value))
            {
                throw Invalid(error);
            }
        }

        if (values.Count != allowedProperties.Count)
        {
            throw Invalid(error);
        }

        return values;
    }

    private static JsonElement ReadRequired(
        IReadOnlyDictionary<string, JsonElement> values,
        string propertyName)
    {
        if (!values.TryGetValue(propertyName, out JsonElement value))
        {
            throw Invalid("Framework catalog is missing a required value.");
        }

        return value;
    }

    private static string ReadRequiredString(
        IReadOnlyDictionary<string, JsonElement> values,
        string propertyName)
    {
        JsonElement value = ReadRequired(values, propertyName);
        string? text = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
        if (value.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(text))
        {
            throw Invalid("Framework catalog string value is invalid.");
        }

        return text;
    }

    private static string[] ReadStringArray(
        IReadOnlyDictionary<string, JsonElement> values,
        string propertyName)
    {
        JsonElement value = ReadRequired(values, propertyName);
        if (value.ValueKind != JsonValueKind.Array ||
            value.GetArrayLength() == 0)
        {
            throw Invalid("Framework catalog list value is invalid.");
        }

        var items = new List<string>(value.GetArrayLength());
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                string.IsNullOrEmpty(item.GetString()) ||
                !unique.Add(item.GetString()!))
            {
                throw Invalid("Framework catalog list value is invalid.");
            }

            items.Add(item.GetString()!);
        }

        return items.ToArray();
    }

    private static bool CoordinateMatchesEcosystem(string ecosystem, string packageCoordinate) =>
        ecosystem switch
        {
            "nuget" => packageCoordinate.StartsWith("nuget:", StringComparison.Ordinal) ||
                packageCoordinate.StartsWith("nuget-sdk:", StringComparison.Ordinal) ||
                packageCoordinate.StartsWith("nuget-framework:", StringComparison.Ordinal),
            "npm" => packageCoordinate.StartsWith("npm:", StringComparison.Ordinal),
            "maven" => packageCoordinate.StartsWith("maven:", StringComparison.Ordinal),
            "python-requirements" => packageCoordinate.StartsWith("python:", StringComparison.Ordinal),
            _ => false,
        };

    private static bool IsNormalizedToken(string value, bool allowLanguageCharacters)
    {
        if (value.Length is < 1 or > 64 ||
            value[0] is < 'a' or > 'z')
        {
            return false;
        }

        return value.Skip(1).All(character =>
            character is >= 'a' and <= 'z' ||
            character is >= '0' and <= '9' ||
            character == '-' ||
            allowLanguageCharacters && (character is '_' or '+' or '#' or '.'));
    }

    private static InvalidDataException Invalid(string message) => new(message);
}

public sealed record FrameworkCatalogEntry(
    string Ecosystem,
    string PackageCoordinate,
    string FrameworkName,
    IReadOnlyList<string> AllowedLanguages,
    IReadOnlyList<string> EvidenceKinds);
