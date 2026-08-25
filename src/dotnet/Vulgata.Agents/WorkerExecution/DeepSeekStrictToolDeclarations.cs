using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace Vulgata.Agents.WorkerExecution;

internal sealed record DeepSeekStrictTransformLedgerRow(
    string JsonPointer,
    string TransformKind,
    string RawSubtreeSha256,
    string ProjectedSubtreeSha256);

internal sealed class DeepSeekStrictToolDeclarations
{
    private const string NavigateCodeDescription =
        "Navigate authorized evidence in the current prepared Repository revision.";
    private const string SubmitCodeLogicDescription =
        "Submit the final Code Logic Document for complete local validation.";
    private const string RawNavigateCodeSha256 =
        "C8387178DA32FC682E7DE462AFA8B62AF388944CB0FC741B895375E585E87770";
    private const string RawSubmitCodeLogicSha256 =
        "77A044526E5D61480B3E86FA3C3AA354FF78A9F04BCA3B279F210DFBFB421BC8";
    private const string RawDeclarationsSha256 =
        "54321765E82EBE39092DF96A6F0E39F922E24459C34830525C22A38CDE533B61";
    private const string ExpectedLedgerIdentity =
        "1472E4F1240A7D4568F127FDA36E72BA8B074EDAA430B54289DC5E02C0E5D6FA";
    private const int MaximumSchemaBytes = 65_536;
    private const int MaximumDeclarationsBytes = 81_920;
    private static readonly DeepSeekStrictTransformLedgerRow[] ExpectedTransformLedger =
    [
        new("/navigate_code/properties/target/properties/exactLineRange", "NullableObjectAnyOf", "DFF9FBEA7B2EFC14E44BC38C1370DC5EEC47ECC32AABC36F32E46720937BD2F9", "ED5E82CFF7735C98DA87389BF09CF3EF2C2E688A20FAD5220DDDCCF788CF8CB0"),
        new("/navigate_code/properties/target/properties/position", "NullableObjectAnyOf", "892C29BC64F8EDD47DF401698DC534651C495F9D78085D3E1424D8F218581ABF", "5E6F4F596A5B2558164E758B86448B398B0780C08E16CA5A494325145C769004"),
        new("/submit_code_logic/properties/document/properties/assumptions/items/properties/status", "HomogeneousEnumType", "60F0EF7E27A6085501622687A633CFDEEE8A092FF637CA0A8E5793CC606E8B1D", "21BEF66091C1DC5ABB6FA37802394CA5F2B659F028F15807E011B8416436D467"),
        new("/submit_code_logic/properties/document/properties/binding/properties/documentSchemaIdentity", "HomogeneousEnumType", "67A9761A4B85299D7195B2C0306ABCF1A284EFE782A98F95AA1D5DC4085705F6", "A18912E2E8A75AB22B93755E749ED6BBA1D0490980B35F4631DDDB2BBE474D0F"),
        new("/submit_code_logic/properties/document/properties/communicationEvidence/items/properties/classificationEvidence/items/properties/communicationKind", "HomogeneousEnumType", "7D7F259517B2817289A377DC2327D60C23C770C1DCDD5971BB98A716CF342F2A", "551438D45E144CB733B6EA7E21C22455E8E090E80A9B7B6224DA16154397B80A"),
        new("/submit_code_logic/properties/document/properties/communicationEvidence/items/properties/classificationEvidence/items/properties/confidence", "HomogeneousEnumType", "3301401337DD6E03370565F2C289765B3049E224B1929C8849E24DF2D330E705", "AFAF77A9B70142BD4C755C4AFDE7381488E6307BC034B4D0FB640DEFACAEAB99"),
        new("/submit_code_logic/properties/document/properties/communicationEvidence/items/properties/classificationEvidence/items/properties/evidenceRule", "HomogeneousEnumType", "9953A8F879D5CD054FB98088782A39B569D78FC7B048440C77D6CEFF62D27978", "C2B59375D3723BCC925F0A596B6E71ACEF5B6631E5F06679A7019FF8934485E3"),
        new("/submit_code_logic/properties/document/properties/communicationEvidence/items/properties/classificationEvidence/items/properties/level", "HomogeneousEnumType", "52D03B2E1700B8B8D98328AA30145663FAA6FED2A724E019B9E6EE30CB4427E4", "2A0DAA60DB6A1767051D7FFB7EDB7785FC1CB9BF821530D1F6CB29F13F410751"),
        new("/submit_code_logic/properties/document/properties/communicationEvidence/items/properties/classificationEvidence/items/properties/provenance", "HomogeneousEnumType", "0FD1D5A1978349902082F3773F34CE862664C6A44CA2846178510366844D38A6", "BD9AF40846153D75AE6D3F955D67030FAB6EA43D5A7E492AF10A8B36E73D7384"),
        new("/submit_code_logic/properties/document/properties/communicationEvidence/items/properties/communicationKind", "HomogeneousEnumType", "67263D11FBF2E00934C0A8723C1904D4C63446E73A945F61909A7346CC10CBA1", "551438D45E144CB733B6EA7E21C22455E8E090E80A9B7B6224DA16154397B80A"),
        new("/submit_code_logic/properties/document/properties/dataAccess/items/properties/mode", "HomogeneousEnumType", "07BA42D4B55D6B340C9236C641F06D179D2D5B1BED7FACAB845A40138152D577", "4ADBE03078050EE17EF651972CFB59F5C3837181B30F8DB8FC8B978A75C2DAC8"),
        new("/submit_code_logic/properties/document/properties/deferredObservations/items/properties/navigationOperation", "HomogeneousEnumType", "E5600DC196588F452A21BF75BF8658A87A06622810886BD48A66A67F9E5CE6D9", "3D211042867008B071FBDA007DAE3A94D2B63B2D3649E2508F9EED718F274784"),
        new("/submit_code_logic/properties/document/properties/deferredObservations/items/properties/outcome", "HomogeneousEnumType", "5D08CC0E71714642C89CCA18070924A7CD52308B53398C58C582224274C86105", "48A7A635A95ECB061C78AF642CBFEB6EB3496591B24B56392B8D13D92E193915"),
        new("/submit_code_logic/properties/document/properties/deferredObservations/items/properties/relationshipKind", "HomogeneousEnumType", "9A00C479DD1D984EB8A81EA32E834012D8C9B828151DECCC91DFFA7C0E88236D", "7476212B8EF60289707F8069B74CF738DDE64E670FFDFFC70CFEFEA4370756AA"),
        new("/submit_code_logic/properties/document/properties/internalUnits/items/properties/kind", "HomogeneousEnumType", "D1B850D2992F61EFBDB2F53AB1BA030A6B90B7C8477C9361344C8A715ACC65AB", "18D6C28BEC1D179CF0294B154C1941B42111251104AB11F62006CF3B487BE665"),
        new("/submit_code_logic/properties/document/properties/knownReferences/items/properties/relationshipKind", "HomogeneousEnumType", "9A00C479DD1D984EB8A81EA32E834012D8C9B828151DECCC91DFFA7C0E88236D", "7476212B8EF60289707F8069B74CF738DDE64E670FFDFFC70CFEFEA4370756AA"),
        new("/submit_code_logic/properties/document/properties/rules/items/properties/kind", "HomogeneousEnumType", "1AE6DCD2F802381613A18383FC9D7CFC8E91BC5ADA598B2932CCE6E0882642AE", "E77D83F9868B4C7C32F2F93E074C8705C1CAAB6C247543FF8FAAEE0FDC94BC7A"),
        new("/submit_code_logic/properties/document/properties/schemaIdentity", "HomogeneousEnumType", "BF7416C6773725A377CD3F8C718F151025F890C92CCDB1985FD026E4F6ED5B1E", "A18912E2E8A75AB22B93755E749ED6BBA1D0490980B35F4631DDDB2BBE474D0F"),
    ];

    internal const string ProjectionAlgorithm =
        "deepseek-strict-schema-projection-v1";

    private DeepSeekStrictToolDeclarations(
        byte[] rawUtf8,
        byte[] rawNavigateCodeParametersUtf8,
        byte[] rawSubmitCodeLogicParametersUtf8,
        byte[] projectedUtf8,
        byte[] projectedSubmitOnlyUtf8,
        byte[] projectedNavigateCodeParametersUtf8,
        byte[] projectedSubmitCodeLogicParametersUtf8,
        IReadOnlyList<DeepSeekStrictTransformLedgerRow> transformLedger)
    {
        RawUtf8 = [.. rawUtf8];
        RawSha256 = Hash(RawUtf8);
        RawNavigateCodeParametersUtf8 = [.. rawNavigateCodeParametersUtf8];
        RawNavigateCodeParametersSha256 = Hash(RawNavigateCodeParametersUtf8);
        RawSubmitCodeLogicParametersUtf8 = [.. rawSubmitCodeLogicParametersUtf8];
        RawSubmitCodeLogicParametersSha256 = Hash(RawSubmitCodeLogicParametersUtf8);
        ProjectedUtf8 = [.. projectedUtf8];
        ProjectedSha256 = Hash(ProjectedUtf8);
        ProjectedSubmitOnlyUtf8 = [.. projectedSubmitOnlyUtf8];
        ProjectedSubmitOnlySha256 = Hash(ProjectedSubmitOnlyUtf8);
        ProjectedNavigateCodeParametersUtf8 = [.. projectedNavigateCodeParametersUtf8];
        ProjectedNavigateCodeParametersSha256 = Hash(ProjectedNavigateCodeParametersUtf8);
        ProjectedSubmitCodeLogicParametersUtf8 = [.. projectedSubmitCodeLogicParametersUtf8];
        ProjectedSubmitCodeLogicParametersSha256 = Hash(ProjectedSubmitCodeLogicParametersUtf8);
        TransformLedger = transformLedger.ToArray();
        LedgerIdentity = Hash(JsonSerializer.SerializeToUtf8Bytes(TransformLedger));
        if (!string.Equals(LedgerIdentity, ExpectedLedgerIdentity, StringComparison.Ordinal))
            throw new InvalidDataException("The declaration transform ledger identity changed.");
    }

    internal byte[] RawUtf8 { get; }
    internal string RawSha256 { get; }
    internal byte[] RawNavigateCodeParametersUtf8 { get; }
    internal string RawNavigateCodeParametersSha256 { get; }
    internal byte[] RawSubmitCodeLogicParametersUtf8 { get; }
    internal string RawSubmitCodeLogicParametersSha256 { get; }
    internal byte[] ProjectedUtf8 { get; }
    internal string ProjectedSha256 { get; }
    internal byte[] ProjectedSubmitOnlyUtf8 { get; }
    internal string ProjectedSubmitOnlySha256 { get; }
    internal byte[] ProjectedNavigateCodeParametersUtf8 { get; }
    internal string ProjectedNavigateCodeParametersSha256 { get; }
    internal byte[] ProjectedSubmitCodeLogicParametersUtf8 { get; }
    internal string ProjectedSubmitCodeLogicParametersSha256 { get; }
    internal IReadOnlyList<DeepSeekStrictTransformLedgerRow> TransformLedger { get; }
    internal string LedgerIdentity { get; }
    internal string ProjectionAlgorithmIdentity => ProjectionAlgorithm;
    internal int TransformLedgerRowCount => TransformLedger.Count;

    // AD-19 compatibility aliases. Production consumes the accepted projected wire.
    internal byte[] Utf8 => ProjectedUtf8;
    internal string Sha256 => ProjectedSha256;
    internal byte[] NavigateCodeParametersUtf8 => ProjectedNavigateCodeParametersUtf8;
    internal string NavigateCodeParametersSha256 => ProjectedNavigateCodeParametersSha256;
    internal byte[] SubmitCodeLogicParametersUtf8 => ProjectedSubmitCodeLogicParametersUtf8;
    internal string SubmitCodeLogicParametersSha256 => ProjectedSubmitCodeLogicParametersSha256;

    internal static DeepSeekStrictToolDeclarations Create(
        AIFunction navigateCode,
        AIFunction submitCodeLogic)
    {
        ArgumentNullException.ThrowIfNull(navigateCode);
        ArgumentNullException.ThrowIfNull(submitCodeLogic);
        ValidateStrictFunction(navigateCode, "navigate_code");
        ValidateStrictFunction(submitCodeLogic, "submit_code_logic");
        ChatTool navigateTool = navigateCode.AsOpenAIChatTool();
        ChatTool submitTool = submitCodeLogic.AsOpenAIChatTool();
        ValidateTool(navigateTool, "navigate_code", NavigateCodeDescription);
        ValidateTool(submitTool, "submit_code_logic", SubmitCodeLogicDescription);

        byte[] rawNavigate = navigateTool.FunctionParameters!.ToMemory().ToArray();
        byte[] rawSubmit = submitTool.FunctionParameters!.ToMemory().ToArray();
        ValidateIdentity(rawNavigate, RawNavigateCodeSha256, "navigate_code");
        ValidateIdentity(rawSubmit, RawSubmitCodeLogicSha256, "submit_code_logic");
        byte[] rawDeclarations = WriteDeclarations(navigateTool, rawNavigate, submitTool, rawSubmit);
        ValidateIdentity(rawDeclarations, RawDeclarationsSha256, "combined declarations");

        List<DeepSeekStrictTransformLedgerRow> ledger = [];
        byte[] projectedNavigate = ProjectSchema(rawNavigate, "/navigate_code", ledger);
        byte[] projectedSubmit = ProjectSchema(rawSubmit, "/submit_code_logic", ledger);
        DeepSeekStrictTransformLedgerRow[] sortedLedger = ledger
            .OrderBy(row => row.JsonPointer, StringComparer.Ordinal)
            .ToArray();
        ValidateCompleteLedger(sortedLedger);
        byte[] projectedDeclarations = WriteDeclarations(
            navigateTool,
            projectedNavigate,
            submitTool,
            projectedSubmit);
        byte[] projectedSubmitOnlyDeclarations =
            DeriveSubmitOnlyDeclarations(projectedDeclarations);
        if (projectedNavigate.Length > MaximumSchemaBytes ||
            projectedSubmit.Length > MaximumSchemaBytes ||
            projectedDeclarations.Length > MaximumDeclarationsBytes ||
            projectedSubmitOnlyDeclarations.Length > MaximumDeclarationsBytes)
        {
            throw new InvalidDataException("The projected DeepSeek declarations exceed their bound.");
        }

        return new(
            rawDeclarations,
            rawNavigate,
            rawSubmit,
            projectedDeclarations,
            projectedSubmitOnlyDeclarations,
            projectedNavigate,
            projectedSubmit,
            sortedLedger);
    }

    internal static byte[] ProjectRaw(byte[] rawSchema)
    {
        ArgumentNullException.ThrowIfNull(rawSchema);
        string functionName = Hash(rawSchema) switch
        {
            RawNavigateCodeSha256 => "navigate_code",
            RawSubmitCodeLogicSha256 => "submit_code_logic",
            _ => throw new InvalidDataException("The raw declaration identity is not accepted."),
        };
        List<DeepSeekStrictTransformLedgerRow> ledger = [];
        byte[] projected = ProjectSchema(rawSchema, "/" + functionName, ledger);
        ValidateFunctionLedger(functionName, ledger);
        return projected;
    }

    private static byte[] ProjectSchema(
        byte[] rawSchema,
        string rootPointer,
        List<DeepSeekStrictTransformLedgerRow> ledger)
    {
        if (rawSchema.Length is 0 or > MaximumSchemaBytes)
        {
            throw new InvalidDataException("The raw declaration schema exceeds its bound.");
        }
        using JsonDocument document = JsonDocument.Parse(rawSchema, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 64,
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !HasUniquePropertyNames(document.RootElement))
        {
            throw new InvalidDataException("The raw declaration schema is malformed.");
        }
        byte[] projected = ProjectNode(document.RootElement, rootPointer, ledger);
        if (projected.Length > MaximumSchemaBytes)
        {
            throw new InvalidDataException("The projected declaration schema exceeds its bound.");
        }
        return projected;
    }

    private static byte[] ProjectNode(
        JsonElement node,
        string pointer,
        List<DeepSeekStrictTransformLedgerRow> ledger)
    {
        ValidateSchemaNode(node);
        if (IsNullableObject(node))
        {
            string rawHash = Hash(Encoding.UTF8.GetBytes(node.GetRawText()));
            byte[] projected = WriteNullableObject(node, pointer, ledger);
            ledger.Add(new(pointer, "NullableObjectAnyOf", rawHash, Hash(projected)));
            return projected;
        }
        if (TryGetEnumOnlyType(node, out string? enumType))
        {
            string rawHash = Hash(Encoding.UTF8.GetBytes(node.GetRawText()));
            byte[] projected = WriteEnumWithType(node, enumType!);
            ledger.Add(new(pointer, "HomogeneousEnumType", rawHash, Hash(projected)));
            return projected;
        }
        using var destination = new MemoryStream();
        using (var writer = new Utf8JsonWriter(destination))
        {
            WriteOrdinaryNode(writer, node, pointer, ledger);
        }
        return destination.ToArray();
    }

    private static byte[] WriteNullableObject(
        JsonElement node,
        string pointer,
        List<DeepSeekStrictTransformLedgerRow> ledger)
    {
        using var destination = new MemoryStream();
        using (var writer = new Utf8JsonWriter(destination))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in node.EnumerateObject())
            {
                if (property.Name is "properties" or "required" or "additionalProperties") continue;
                if (!string.Equals(property.Name, "type", StringComparison.Ordinal))
                {
                    WriteProjectedProperty(writer, property, pointer, ledger);
                    continue;
                }
                writer.WriteStartArray("anyOf");
                writer.WriteStartObject();
                writer.WriteString("type", "object");
                foreach (JsonProperty objectProperty in node.EnumerateObject())
                {
                    if (objectProperty.Name is "properties" or "required" or "additionalProperties")
                    {
                        WriteProjectedProperty(writer, objectProperty, pointer, ledger);
                    }
                }
                writer.WriteEndObject();
                writer.WriteStartObject();
                writer.WriteString("type", "null");
                writer.WriteEndObject();
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
        return destination.ToArray();
    }

    private static byte[] WriteEnumWithType(JsonElement node, string enumType)
    {
        using var destination = new MemoryStream();
        using (var writer = new Utf8JsonWriter(destination))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in node.EnumerateObject())
            {
                if (string.Equals(property.Name, "enum", StringComparison.Ordinal))
                {
                    writer.WriteString("type", enumType);
                }
                property.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return destination.ToArray();
    }

    private static void WriteOrdinaryNode(
        Utf8JsonWriter writer,
        JsonElement node,
        string pointer,
        List<DeepSeekStrictTransformLedgerRow> ledger)
    {
        writer.WriteStartObject();
        foreach (JsonProperty property in node.EnumerateObject())
        {
            WriteProjectedProperty(writer, property, pointer, ledger);
        }
        writer.WriteEndObject();
    }

    private static void WriteProjectedProperty(
        Utf8JsonWriter writer,
        JsonProperty property,
        string pointer,
        List<DeepSeekStrictTransformLedgerRow> ledger)
    {
        writer.WritePropertyName(property.Name);
        if (string.Equals(property.Name, "properties", StringComparison.Ordinal))
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("A schema properties value is malformed.");
            writer.WriteStartObject();
            foreach (JsonProperty child in property.Value.EnumerateObject())
            {
                writer.WritePropertyName(child.Name);
                byte[] projected = ProjectNode(
                    child.Value,
                    pointer + "/properties/" + EscapePointer(child.Name),
                    ledger);
                writer.WriteRawValue(projected, skipInputValidation: false);
            }
            writer.WriteEndObject();
        }
        else if (string.Equals(property.Name, "items", StringComparison.Ordinal))
        {
            byte[] projected = ProjectNode(property.Value, pointer + "/items", ledger);
            writer.WriteRawValue(projected, skipInputValidation: false);
        }
        else
        {
            property.Value.WriteTo(writer);
        }
    }

    private static void ValidateSchemaNode(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("A schema node is not an object.");
        foreach (JsonProperty property in node.EnumerateObject())
        {
            if (property.Name is "$ref" or "$defs" or "$def" or
                "unevaluatedProperties" or "oneOf" or "allOf")
                throw new InvalidDataException("The raw declaration uses an unsupported keyword.");
        }

        bool hasType = node.TryGetProperty("type", out JsonElement type);
        if (hasType)
        {
            if (type.ValueKind == JsonValueKind.Array)
            {
                string[] values = type.EnumerateArray()
                    .Select(value => value.ValueKind == JsonValueKind.String ? value.GetString()! : "")
                    .ToArray();
                bool nullableObject = values.SequenceEqual(["object", "null"], StringComparer.Ordinal);
                bool nullablePrimitive = values.Length == 2 && values[1] == "null" &&
                    values[0] is "string" or "boolean" or "integer" or "number";
                if (!nullableObject && !nullablePrimitive)
                    throw new InvalidDataException("The raw declaration uses an unsupported type array.");
            }
            else if (type.ValueKind != JsonValueKind.String ||
                type.GetString() is not ("object" or "array" or "string" or "boolean" or
                    "integer" or "number" or "null"))
                throw new InvalidDataException("The raw declaration uses an unsupported type.");
        }

        bool objectNode = hasType &&
            (type.ValueKind == JsonValueKind.String && type.GetString() == "object" || IsNullableObject(node));
        if (objectNode) ValidateClosedObject(node);
        else if (node.TryGetProperty("properties", out _))
            throw new InvalidDataException("A properties node has no object discriminator.");

        if (node.TryGetProperty("enum", out JsonElement enumValues))
            _ = GetHomogeneousEnumType(enumValues);
    }

    private static void ValidateClosedObject(JsonElement node)
    {
        if (!node.TryGetProperty("properties", out JsonElement properties) ||
            properties.ValueKind != JsonValueKind.Object ||
            !node.TryGetProperty("required", out JsonElement required) ||
            required.ValueKind != JsonValueKind.Array ||
            !node.TryGetProperty("additionalProperties", out JsonElement additionalProperties) ||
            additionalProperties.ValueKind != JsonValueKind.False)
            throw new InvalidDataException("An object schema is not closed and complete.");
        string[] propertyNames = properties.EnumerateObject().Select(property => property.Name).ToArray();
        string[] requiredNames = required.EnumerateArray()
            .Select(value => value.ValueKind == JsonValueKind.String ? value.GetString()! : "")
            .ToArray();
        if (propertyNames.Length != requiredNames.Length ||
            propertyNames.Except(requiredNames, StringComparer.Ordinal).Any() ||
            requiredNames.Distinct(StringComparer.Ordinal).Count() != requiredNames.Length)
            throw new InvalidDataException("An object schema has incomplete required membership.");
    }

    private static bool IsNullableObject(JsonElement node)
    {
        if (!node.TryGetProperty("type", out JsonElement type) || type.ValueKind != JsonValueKind.Array)
            return false;
        string?[] values = type.EnumerateArray().Select(value => value.GetString()).ToArray();
        return values.SequenceEqual(["object", "null"], StringComparer.Ordinal);
    }

    private static bool TryGetEnumOnlyType(JsonElement node, out string? enumType)
    {
        enumType = null;
        if (!node.TryGetProperty("enum", out JsonElement values) ||
            node.TryGetProperty("type", out _) || node.TryGetProperty("anyOf", out _) ||
            node.TryGetProperty("$ref", out _)) return false;
        enumType = GetHomogeneousEnumType(values);
        return true;
    }

    private static string GetHomogeneousEnumType(JsonElement values)
    {
        if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() == 0)
            throw new InvalidDataException("An enum is empty or malformed.");
        JsonValueKind[] kinds = values.EnumerateArray().Select(value => value.ValueKind).ToArray();
        if (kinds.All(kind => kind == JsonValueKind.String)) return "string";
        if (kinds.All(kind => kind is JsonValueKind.True or JsonValueKind.False)) return "boolean";
        if (kinds.All(kind => kind == JsonValueKind.Number))
            return values.EnumerateArray().All(value => value.TryGetInt64(out _)) ? "integer" : "number";
        throw new InvalidDataException("An enum is heterogeneous or unsupported.");
    }

    private static void ValidateCompleteLedger(IReadOnlyList<DeepSeekStrictTransformLedgerRow> ledger)
    {
        if (!ledger.SequenceEqual(ExpectedTransformLedger))
            throw new InvalidDataException("The declaration transform ledger is incomplete or changed.");
        ValidateFunctionLedger("navigate_code", ledger.Where(row =>
            row.JsonPointer.StartsWith("/navigate_code/", StringComparison.Ordinal)));
        ValidateFunctionLedger("submit_code_logic", ledger.Where(row =>
            row.JsonPointer.StartsWith("/submit_code_logic/", StringComparison.Ordinal)));
    }

    private static void ValidateFunctionLedger(
        string functionName,
        IEnumerable<DeepSeekStrictTransformLedgerRow> rows)
    {
        DeepSeekStrictTransformLedgerRow[] values = rows
            .OrderBy(row => row.JsonPointer, StringComparer.Ordinal).ToArray();
        DeepSeekStrictTransformLedgerRow[] expected = ExpectedTransformLedger.Where(row =>
            row.JsonPointer.StartsWith("/" + functionName + "/", StringComparison.Ordinal)).ToArray();
        if (!values.SequenceEqual(expected))
            throw new InvalidDataException("A function transform ledger is incomplete or changed.");
    }

    private static byte[] WriteDeclarations(
        ChatTool navigateTool,
        byte[] navigateParameters,
        ChatTool submitTool,
        byte[] submitParameters)
    {
        using var destination = new MemoryStream();
        using (var writer = new Utf8JsonWriter(destination))
        {
            writer.WriteStartArray();
            WriteTool(writer, navigateTool, navigateParameters);
            WriteTool(writer, submitTool, submitParameters);
            writer.WriteEndArray();
        }
        return destination.ToArray();
    }

    private static byte[] DeriveSubmitOnlyDeclarations(byte[] projectedDeclarations)
    {
        using JsonDocument document = JsonDocument.Parse(projectedDeclarations);
        JsonElement[] declarations = document.RootElement.EnumerateArray().ToArray();
        if (declarations.Length != 2 ||
            !string.Equals(
                declarations[1].GetProperty("function").GetProperty("name").GetString(),
                "submit_code_logic",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The projected submit declaration is unavailable.");
        }

        using var destination = new MemoryStream();
        using (var writer = new Utf8JsonWriter(destination))
        {
            writer.WriteStartArray();
            writer.WriteRawValue(declarations[1].GetRawText(), skipInputValidation: false);
            writer.WriteEndArray();
        }

        return destination.ToArray();
    }

    private static void ValidateStrictFunction(AIFunction function, string expectedName)
    {
        if (!string.Equals(function.Name, expectedName, StringComparison.Ordinal) ||
            !function.AdditionalProperties.TryGetValue("strict", out object? strict) || strict is not true)
            throw new InvalidDataException($"The accepted {expectedName} function is not strict.");
    }

    private static void ValidateTool(ChatTool tool, string expectedName, string expectedDescription)
    {
        if (!string.Equals(tool.FunctionName, expectedName, StringComparison.Ordinal) ||
            !string.Equals(tool.FunctionDescription, expectedDescription, StringComparison.Ordinal) ||
            tool.FunctionParameters is null)
            throw new InvalidDataException(
                $"The public OpenAI adapter omitted the accepted {expectedName} declaration.");
    }

    private static void ValidateIdentity(byte[] value, string expectedSha256, string name)
    {
        if (value.Length is 0 or > MaximumDeclarationsBytes ||
            !string.Equals(Hash(value), expectedSha256, StringComparison.Ordinal))
            throw new InvalidDataException($"The immutable raw {name} declaration changed.");
    }

    private static void WriteTool(Utf8JsonWriter writer, ChatTool tool, byte[] parameters)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "function");
        writer.WriteStartObject("function");
        writer.WriteString("name", tool.FunctionName);
        writer.WriteString("description", tool.FunctionDescription);
        writer.WritePropertyName("parameters");
        writer.WriteRawValue(parameters, skipInputValidation: false);
        writer.WriteBoolean("strict", true);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static bool HasUniquePropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
                if (!names.Add(property.Name) || !HasUniquePropertyNames(property.Value)) return false;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
                if (!HasUniquePropertyNames(item)) return false;
        }
        return true;
    }

    private static string EscapePointer(string value) =>
        value.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

    private static string Hash(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value));
}
