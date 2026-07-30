using System.Security.Cryptography;
using System.Text.Json;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.CommunicationDetection;

public enum CommunicationEvidenceCategory
{
    DirectAnnotation,
    ControllerAnnotation,
    ClientAnnotation,
    MatchedConfiguration,
    Import,
    PackageManifest,
    FrameworkFact,
}

public sealed record CommunicationEvidenceRule(
    string Id,
    CommunicationKind Kind,
    CommunicationEvidenceCategory Category,
    ClassificationLevel Disposition,
    IReadOnlyList<string> Tokens,
    IReadOnlyList<string> Keys);

public sealed class CommunicationEvidenceRuleCatalog
{
    private const string _resourceName =
        "Vulgata.Infrastructure.CommunicationDetection.Catalog.communication-evidence-rules.v1.json";
    private static readonly HashSet<string> _rootProperties =
        ["version", "rules"];
    private static readonly HashSet<string> _ruleProperties =
        ["id", "kind", "category", "disposition", "tokens", "keys"];
    private static readonly IReadOnlyDictionary<string, ExpectedRule> _expectedRules =
        new Dictionary<string, ExpectedRule>(StringComparer.Ordinal)
        {
            ["rpc.dubbo.service-annotation"] = new(
                CommunicationKind.Rpc,
                CommunicationEvidenceCategory.DirectAnnotation,
                ClassificationLevel.Confirmed,
                ["@DubboService", "@DubboReference"],
                []),
            ["rpc.sofa.service-annotation"] = new(
                CommunicationKind.Rpc,
                CommunicationEvidenceCategory.DirectAnnotation,
                ClassificationLevel.Confirmed,
                ["@SofaService", "@SofaReference"],
                []),
            ["rpc.dubbo.application-configuration"] = new(
                CommunicationKind.Rpc,
                CommunicationEvidenceCategory.MatchedConfiguration,
                ClassificationLevel.Confirmed,
                [],
                ["dubbo.application.name", "dubbo.protocol.name"]),
            ["http.spring.controller-annotation"] = new(
                CommunicationKind.Http,
                CommunicationEvidenceCategory.ControllerAnnotation,
                ClassificationLevel.Confirmed,
                [
                    "@RestController",
                    "@RequestMapping",
                    "@GetMapping",
                    "@PostMapping",
                    "@PutMapping",
                    "@DeleteMapping",
                    "@PatchMapping",
                ],
                []),
            ["http.dotnet.controller-annotation"] = new(
                CommunicationKind.Http,
                CommunicationEvidenceCategory.ControllerAnnotation,
                ClassificationLevel.Confirmed,
                [
                    "[ApiController]",
                    "[HttpGet",
                    "[HttpPost",
                    "[HttpPut",
                    "[HttpDelete",
                    "[HttpPatch",
                ],
                []),
            ["http.feign.client-annotation"] = new(
                CommunicationKind.Http,
                CommunicationEvidenceCategory.ClientAnnotation,
                ClassificationLevel.Confirmed,
                ["@FeignClient"],
                []),
            ["http.versioned-route-configuration"] = new(
                CommunicationKind.Http,
                CommunicationEvidenceCategory.MatchedConfiguration,
                ClassificationLevel.Confirmed,
                [],
                ["server.servlet.context-path", "spring.mvc.servlet.path"]),
            ["rpc.import-presence"] = new(
                CommunicationKind.Rpc,
                CommunicationEvidenceCategory.Import,
                ClassificationLevel.Hint,
                ["org.apache.dubbo", "com.alipay.sofa.rpc"],
                []),
            ["http.import-presence"] = new(
                CommunicationKind.Http,
                CommunicationEvidenceCategory.Import,
                ClassificationLevel.Hint,
                ["org.springframework.web", "System.Net.Http"],
                []),
            ["rpc.package-presence"] = new(
                CommunicationKind.Rpc,
                CommunicationEvidenceCategory.PackageManifest,
                ClassificationLevel.Hint,
                [
                    "maven:org.apache.dubbo:dubbo",
                    "maven:com.alipay.sofa:sofa-rpc-all",
                ],
                []),
            ["http.package-presence"] = new(
                CommunicationKind.Http,
                CommunicationEvidenceCategory.PackageManifest,
                ClassificationLevel.Hint,
                [
                    "maven:org.springframework.boot:spring-boot-starter-web",
                    "nuget:microsoft.extensions.http",
                ],
                []),
            ["rpc.framework-presence"] = new(
                CommunicationKind.Rpc,
                CommunicationEvidenceCategory.FrameworkFact,
                ClassificationLevel.Hint,
                ["dubbo", "sofa-rpc"],
                []),
            ["http.framework-presence"] = new(
                CommunicationKind.Http,
                CommunicationEvidenceCategory.FrameworkFact,
                ClassificationLevel.Hint,
                ["aspnet-core", "spring-boot"],
                []),
        };

    private CommunicationEvidenceRuleCatalog(
        string version,
        string sha256,
        IReadOnlyList<CommunicationEvidenceRule> rules)
    {
        Version = version;
        Sha256 = sha256;
        Rules = Array.AsReadOnly([.. rules]);
    }

    public string Version { get; }
    public string Sha256 { get; }
    public IReadOnlyList<CommunicationEvidenceRule> Rules { get; }

    public static CommunicationEvidenceRuleCatalog LoadEmbedded()
    {
        using Stream resource = typeof(CommunicationEvidenceRuleCatalog)
            .Assembly
            .GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException(
                "Embedded communication evidence rule catalog is unavailable.");
        return Load(resource);
    }

    public static CommunicationEvidenceRuleCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        const int maximumContentLength = 64 * 1024;
        byte[] content = new byte[maximumContentLength + 1];
        var contentLength = 0;
        while (contentLength < content.Length)
        {
            int bytesRead = stream.Read(content.AsSpan(contentLength));
            if (bytesRead == 0)
            {
                break;
            }

            contentLength += bytesRead;
        }

        if (contentLength is 0 or > maximumContentLength)
        {
            throw Invalid("Communication evidence rule catalog is not bounded.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                content.AsMemory(0, contentLength),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 16,
                });
        }
        catch (JsonException)
        {
            throw Invalid("Communication evidence rule catalog JSON is invalid.");
        }

        using (document)
        {
            IReadOnlyDictionary<string, JsonElement> root = ReadClosedObject(
                document.RootElement,
                _rootProperties,
                "Communication evidence rule catalog root is invalid.");
            string version = ReadRequiredString(root, "version");
            if (!StringComparer.Ordinal.Equals(version, "1.0"))
            {
                throw Invalid("Communication evidence rule catalog version is unsupported.");
            }

            JsonElement rulesElement = ReadRequired(root, "rules");
            if (rulesElement.ValueKind != JsonValueKind.Array ||
                rulesElement.GetArrayLength() != _expectedRules.Count)
            {
                throw Invalid("Communication evidence rule catalog rules are incomplete.");
            }

            var rules = new List<CommunicationEvidenceRule>(_expectedRules.Count);
            var foundIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement element in rulesElement.EnumerateArray())
            {
                CommunicationEvidenceRule rule = ReadRule(element);
                if (!foundIds.Add(rule.Id))
                {
                    throw Invalid("Communication evidence rule catalog contains a duplicate rule.");
                }

                rules.Add(rule);
            }

            if (!_expectedRules.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(foundIds))
            {
                throw Invalid("Communication evidence rule catalog contains an unknown rule.");
            }

            return new(
                version,
                Convert.ToHexStringLower(SHA256.HashData(content.AsSpan(0, contentLength))),
                rules);
        }
    }

    private static CommunicationEvidenceRule ReadRule(JsonElement element)
    {
        IReadOnlyDictionary<string, JsonElement> values = ReadClosedObject(
            element,
            _ruleProperties,
            "Communication evidence rule is invalid.");
        string id = ReadRequiredString(values, "id");
        string kindText = ReadRequiredString(values, "kind");
        string categoryText = ReadRequiredString(values, "category");
        string dispositionText = ReadRequiredString(values, "disposition");
        string[] tokens = ReadStringArray(values, "tokens");
        string[] keys = ReadStringArray(values, "keys");

        if (!_expectedRules.TryGetValue(id, out ExpectedRule? expected) ||
            !Enum.TryParse(kindText, ignoreCase: false, out CommunicationKind kind) ||
            !Enum.IsDefined(kind) ||
            !Enum.TryParse(
                categoryText,
                ignoreCase: false,
                out CommunicationEvidenceCategory category) ||
            !Enum.IsDefined(category) ||
            !Enum.TryParse(
                dispositionText,
                ignoreCase: false,
                out ClassificationLevel disposition) ||
            disposition is not (ClassificationLevel.Confirmed or ClassificationLevel.Hint) ||
            tokens.Length + keys.Length == 0 ||
            tokens.Length > 16 ||
            keys.Length > 16 ||
            tokens.Any(token => !IsExactBoundedValue(token, 128)) ||
            keys.Any(key => !IsExactBoundedKey(key)) ||
            tokens.Distinct(StringComparer.Ordinal).Count() != tokens.Length ||
            keys.Distinct(StringComparer.Ordinal).Count() != keys.Length ||
            !IsCompatible(category, disposition, tokens, keys) ||
            !MatchesExpected(expected, kind, category, disposition, tokens, keys))
        {
            throw Invalid("Communication evidence rule values are invalid.");
        }

        return new(
            id,
            kind,
            category,
            disposition,
            Array.AsReadOnly(tokens),
            Array.AsReadOnly(keys));
    }

    private static bool IsCompatible(
        CommunicationEvidenceCategory category,
        ClassificationLevel disposition,
        string[] tokens,
        string[] keys) =>
        category switch
        {
            CommunicationEvidenceCategory.DirectAnnotation or
                CommunicationEvidenceCategory.ControllerAnnotation or
                CommunicationEvidenceCategory.ClientAnnotation =>
                disposition == ClassificationLevel.Confirmed &&
                tokens.Length > 0 &&
                keys.Length == 0,
            CommunicationEvidenceCategory.MatchedConfiguration =>
                disposition == ClassificationLevel.Confirmed &&
                tokens.Length == 0 &&
                keys.Length > 0,
            CommunicationEvidenceCategory.Import or
                CommunicationEvidenceCategory.PackageManifest or
                CommunicationEvidenceCategory.FrameworkFact =>
                disposition == ClassificationLevel.Hint &&
                tokens.Length > 0 &&
                keys.Length == 0,
            _ => false,
        };

    private static bool MatchesExpected(
        ExpectedRule expected,
        CommunicationKind kind,
        CommunicationEvidenceCategory category,
        ClassificationLevel disposition,
        string[] tokens,
        string[] keys) =>
        expected.Kind == kind &&
        expected.Category == category &&
        expected.Disposition == disposition &&
        expected.Tokens.SequenceEqual(tokens, StringComparer.Ordinal) &&
        expected.Keys.SequenceEqual(keys, StringComparer.Ordinal);

    private static bool IsExactBoundedValue(string value, int maximumLength) =>
        value.Length is > 0 &&
        value.Length <= maximumLength &&
        value.Length == value.Trim().Length &&
        value.All(character =>
            !char.IsControl(character) &&
            !char.IsWhiteSpace(character) &&
            character <= 0x7f);

    private static bool IsExactBoundedKey(string value) =>
        IsExactBoundedValue(value, 128) &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(character =>
            character is >= 'a' and <= 'z' or
                >= '0' and <= '9' or
                '.' or '-' or '_');

    private static IReadOnlyDictionary<string, JsonElement> ReadClosedObject(
        JsonElement element,
        HashSet<string> allowedProperties,
        string message)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(message);
        }

        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name) ||
                !values.TryAdd(property.Name, property.Value))
            {
                throw Invalid(message);
            }
        }

        if (values.Count != allowedProperties.Count)
        {
            throw Invalid(message);
        }

        return values;
    }

    private static JsonElement ReadRequired(
        IReadOnlyDictionary<string, JsonElement> values,
        string name) =>
        values.TryGetValue(name, out JsonElement value)
            ? value
            : throw Invalid("Communication evidence rule catalog field is missing.");

    private static string ReadRequiredString(
        IReadOnlyDictionary<string, JsonElement> values,
        string name)
    {
        JsonElement element = ReadRequired(values, name);
        string? value = element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
        return !string.IsNullOrEmpty(value)
            ? value
            : throw Invalid("Communication evidence rule catalog string is invalid.");
    }

    private static string[] ReadStringArray(
        IReadOnlyDictionary<string, JsonElement> values,
        string name)
    {
        JsonElement element = ReadRequired(values, name);
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("Communication evidence rule catalog array is invalid.");
        }

        var result = new List<string>(element.GetArrayLength());
        foreach (JsonElement item in element.EnumerateArray())
        {
            string? value = item.ValueKind == JsonValueKind.String
                ? item.GetString()
                : null;
            if (value is null)
            {
                throw Invalid("Communication evidence rule catalog array item is invalid.");
            }

            result.Add(value);
        }

        return [.. result];
    }

    private static InvalidDataException Invalid(string message) => new(message);

    private sealed record ExpectedRule(
        CommunicationKind Kind,
        CommunicationEvidenceCategory Category,
        ClassificationLevel Disposition,
        string[] Tokens,
        string[] Keys);
}
