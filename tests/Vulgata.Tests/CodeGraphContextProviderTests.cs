using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CodeGraph;

namespace Vulgata.Tests;

public sealed class CodeGraphContextProviderTests
{
    [Fact]
    public void GatewayStartInfo_PrivateProcess_SetsOnlyExecuteCypherQueryLimitAt250001()
    {
        string? globalBefore = Environment.GetEnvironmentVariable("TOOL_RESULT_LIMITS");
        MethodInfo? createStartInfo = typeof(CodeGraphContextProvider).GetMethod(
            "CreateGatewayStartInfo",
            BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(string), typeof(int)]);
        Assert.NotNull(createStartInfo);

        var startInfo = Assert.IsType<ProcessStartInfo>(
            createStartInfo!.Invoke(null, [Path.GetTempPath(), 12_345]));

        Assert.True(startInfo.Environment.TryGetValue("TOOL_RESULT_LIMITS", out string? raw));
        using JsonDocument limits = JsonDocument.Parse(raw!);
        JsonProperty limit = Assert.Single(limits.RootElement.EnumerateObject());
        Assert.Equal("execute_cypher_query", limit.Name);
        Assert.Equal(250_001, limit.Value.GetInt32());
        Assert.Equal(globalBefore, Environment.GetEnvironmentVariable("TOOL_RESULT_LIMITS"));
    }

    [Theory]
    [InlineData(",\"truncated\":true,\"result_limit\":100")]
    [InlineData(",\"truncated\":false")]
    [InlineData(",\"result_limit\":100")]
    [InlineData(",\"truncated\":false,\"truncated\":true,\"result_limit\":100")]
    [InlineData(",\"truncated\":\"false\",\"result_limit\":\"100\"")]
    [InlineData(",\"truncated\":false,\"result_limit\":250001")]
    public void ParseEnvelope_WhenReservedCompletenessMetadataIsPresent_RejectsAtomically(
        string completenessMetadata)
    {
        object[] shortenedRows = Enumerable.Range(0, 100)
            .Select(index => (object)new
            {
                record_kind = "code_unit",
                provider_kind = "File",
                path = $"src/File{index}.cs",
                name = (string?)null,
                start_line = (int?)null,
                end_line = (int?)null,
            })
            .ToArray();
        byte[] codeUnits = CreateEnvelopeWithRawDataSuffix(
            CodeGraphContextProviderQueries.CodeUnits,
            shortenedRows,
            completenessMetadata);
        byte[] relationships = CreateEnvelopeWithRawDataSuffix(
            CodeGraphContextProviderQueries.Relationships,
            [],
            string.Empty);

        Assert.Throws<InvalidDataException>(() =>
            CodeGraphHttpProjectionParser.Parse(
                codeUnits,
                relationships,
                Path.GetTempPath(),
                TimeSpan.Zero));
    }

    [Fact]
    public void ParseEnvelope_WhenCombinedRowsAre250001_RejectsBeforeRowMapping()
    {
        object?[] rows = new object?[250_001];
        byte[] codeUnits = CreateEnvelopeWithRawDataSuffix(
            CodeGraphContextProviderQueries.CodeUnits,
            rows,
            string.Empty);
        byte[] relationships = CreateEnvelopeWithRawDataSuffix(
            CodeGraphContextProviderQueries.Relationships,
            [],
            string.Empty);

        Assert.Throws<InvalidDataException>(() =>
            CodeGraphHttpProjectionParser.Parse(
                codeUnits,
                relationships,
                Path.GetTempPath(),
                TimeSpan.Zero));
    }

    [Fact]
    public void ProductionQueries_ExposeExactlyTwoImmutableProjections()
    {
        FieldInfo[] queryFields = typeof(CodeGraphContextProviderQueries)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(string))
            .ToArray();

        Assert.Equal(2, queryFields.Length);
        Assert.Equal(
            ["CodeUnits", "Relationships"],
            queryFields.Select(field => field.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ProjectionA_UsesExactNullableParserLanguageAlias_AndProjectionBRemainsUnchanged()
    {
        Assert.Equal(
            1,
            CountOccurrences(
                CodeGraphContextProviderQueries.CodeUnits,
                "node.lang AS parser_language"));
        Assert.DoesNotContain(
            "parser_language",
            CodeGraphContextProviderQueries.Relationships,
            StringComparison.Ordinal);
        Assert.Equal(
            "40d2da3b9b520b3c4eca3fd7c3bad4f4fb10643736e3a9e9b4ddf62c92adc096",
            Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes(CodeGraphContextProviderQueries.Relationships))));
    }

    [Theory]
    [InlineData("C#", " C_SHARP ", "c_sharp")]
    [InlineData("Java", "JAVA", "java")]
    [InlineData("Python", "python", "python")]
    [InlineData("JavaScript", "JAVASCRIPT", "javascript")]
    [InlineData("TypeScript", "typescript", "typescript")]
    [InlineData("Go", "GO", "go")]
    [InlineData("Kotlin", "kotlin", "kotlin")]
    [InlineData("Swift", "SWIFT", "swift")]
    [InlineData("Rust", "rust", "rust")]
    [InlineData("C", "C", "c")]
    [InlineData("C++", "CPP", "cpp")]
    [InlineData("Additional", "TSX", "tsx")]
    public void ParseProjectionA_EpicLanguageMatrix_NormalizesProviderIdentity(
        string fixtureLanguage,
        string providerLanguage,
        string expectedToken)
    {
        Assert.False(string.IsNullOrWhiteSpace(fixtureLanguage));
        CodeGraphProviderResult result = ParseCodeUnits(
        [
            CreateCodeUnitRow("Class", "src/A.cs", "A", 1, 1, providerLanguage),
        ]);

        ProviderCodeUnit unit = Assert.Single(result.CodeUnits);
        Assert.Equal(expectedToken, unit.Language);
    }

    [Theory]
    [InlineData(" \uFF34\uFF33\uFF38 ", "tsx")]
    [InlineData("zig_0+#.-", "zig_0+#.-")]
    public void ParseProjectionA_FormKcAndOpenGrammar_NormalizesValidIdentity(
        string providerLanguage,
        string expectedToken)
    {
        CodeGraphProviderResult result = ParseCodeUnits(
        [
            CreateCodeUnitRow("Class", "src/A.cs", "A", 1, 1, providerLanguage),
        ]);

        Assert.Equal(expectedToken, Assert.Single(result.CodeUnits).Language);
    }

    [Fact]
    public void ParseProjectionA_FileNullAndDuplicateStructuralObservation_AssignsIdentityToEveryUnit()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-language-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "A.cs"), "class A { }");
        try
        {
            CodeGraphProviderResult result = ParseCodeUnits(
            [
                CreateCodeUnitRow("File", "src/A.cs", null, null, null, null),
                CreateCodeUnitRow("Class", "src/A.cs", "A", 1, 1, "c_sharp"),
                CreateCodeUnitRow("Class", "src/A.cs", "A", 1, 1, "C_SHARP"),
            ],
            root);

            Assert.Equal(2, result.CodeUnits.Count);
            Assert.All(result.CodeUnits, unit => Assert.Equal("c_sharp", unit.Language));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ParseProjectionA_SupportedFileWithoutParserIdentity_FiltersOnceAndRemovesAllStructure()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-unknown-language-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "Unknown.cs"), "not structurally parsed");
        try
        {
            object?[] codeUnitRows =
            [
                CreateCodeUnitRow("File", "src/Unknown.cs", null, null, null, null),
                CreateCodeUnitRow("File", "src/Unknown.cs", null, null, null, null),
            ];
            object?[] relationshipRows =
            [
                CreateRelationshipRow(
                    "CONTAINS",
                    "File",
                    "src/Unknown.cs",
                    null,
                    null,
                    null,
                    "Class",
                    "src/Unknown.cs",
                    "Synthetic",
                    1,
                    1),
            ];

            CodeGraphProviderResult result = CodeGraphHttpProjectionParser.Parse(
                CreateEnvelopeWithRawDataSuffix(
                    CodeGraphContextProviderQueries.CodeUnits,
                    codeUnitRows,
                    string.Empty),
                CreateEnvelopeWithRawDataSuffix(
                    CodeGraphContextProviderQueries.Relationships,
                    relationshipRows,
                    string.Empty),
                root,
                TimeSpan.Zero);

            Assert.True(result.Succeeded);
            ProviderCandidateOutcome outcome = Assert.Single(result.CandidateOutcomes);
            Assert.Equal("src/Unknown.cs", outcome.RelativePath.Value);
            Assert.Equal(ScanCandidateOutcomeKind.Filtered, outcome.Outcome);
            Assert.Equal(ScanCandidateOutcomeReasons.UnknownLanguage, outcome.Reason);
            Assert.Empty(result.CodeUnits);
            Assert.Empty(result.Relationships);
            Assert.Equal(0, result.ParsedFileCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("1java")]
    [InlineData("java!")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void ParseProjectionA_InvalidParserLanguage_RejectsAtomically(string invalidLanguage)
    {
        Assert.Throws<InvalidDataException>(() => ParseCodeUnits(
        [
            CreateCodeUnitRow("Class", "src/A.cs", "A", 1, 1, invalidLanguage),
        ]));
    }

    [Fact]
    public void ParseProjectionA_WrongTypedParserLanguage_RejectsAtomically()
    {
        Assert.Throws<InvalidDataException>(() => ParseCodeUnits(
        [
            CreateCodeUnitRow("Class", "src/A.cs", "A", 1, 1, 42),
        ]));
    }

    [Fact]
    public void ParseProjectionA_DuplicateParserLanguageProperty_RejectsAtomically()
    {
        const string row =
            """{"record_kind":"code_unit","provider_kind":"Class","path":"src/A.cs","name":"A","start_line":1,"end_line":1,"parser_language":"java","parser_language":"java"}""";
        byte[] codeUnits = CreateEnvelopeFromRawResults(
            CodeGraphContextProviderQueries.CodeUnits,
            row,
            recordCount: 1);
        byte[] relationships = CreateEnvelopeWithRawDataSuffix(
            CodeGraphContextProviderQueries.Relationships,
            [],
            string.Empty);

        Assert.Throws<InvalidDataException>(() =>
            CodeGraphHttpProjectionParser.Parse(
                codeUnits,
                relationships,
                Path.GetTempPath(),
                TimeSpan.Zero));
    }

    [Fact]
    public void ParseProjectionA_SamePathConflictingStructuralLanguages_RejectsAtomically()
    {
        Assert.Throws<InvalidDataException>(() => ParseCodeUnits(
        [
            CreateCodeUnitRow("Class", "src/A.cs", "A", 1, 1, "java"),
            CreateCodeUnitRow("Function", "src/A.cs", "run", 2, 2, "kotlin"),
        ]));
    }

    [Fact]
    public void ParseProjectionA_ParserLanguage_DoesNotChangeCgcV1EndpointIdentity()
    {
        CodeGraphProviderResult result = ParseCodeUnits(
        [
            CreateCodeUnitRow("Class", "src/A.cs", "A", 1, 1, "c_sharp"),
        ]);
        string canonical = string.Join('\0', "Class", "src/A.cs", "A", "1", "1");
        string expected = "cgc-v1:" +
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

        Assert.Equal(expected, Assert.Single(result.CodeUnits).EndpointIdentity);
    }

    [Theory]
    [MemberData(nameof(UnsafeProviderPathCases))]
    public void ParseMalformedProviderRow_WithUnsafePath_OwnedOutcomeContainsOnlyUnknown(
        string rawPath,
        string hostFragment,
        bool relationshipRow)
    {
        object?[] codeUnitRows = relationshipRow
            ? []
            :
            [
                CreateCodeUnitRow("Unsupported", rawPath, "Secret", 1, 1, null),
            ];
        object?[] relationshipRows = relationshipRow
            ?
            [
                CreateRelationshipRow(
                    "UNSUPPORTED",
                    "Class",
                    rawPath,
                    "Secret",
                    1,
                    1,
                    "Class",
                    "src/Safe.cs",
                    "Safe",
                    1,
                    1),
            ]
            : [];

        CodeGraphProviderResult result = CodeGraphHttpProjectionParser.Parse(
            CreateEnvelopeWithRawDataSuffix(
                CodeGraphContextProviderQueries.CodeUnits,
                codeUnitRows,
                string.Empty),
            CreateEnvelopeWithRawDataSuffix(
                CodeGraphContextProviderQueries.Relationships,
                relationshipRows,
                string.Empty),
            Path.GetTempPath(),
            TimeSpan.Zero);

        ProviderCandidateOutcome outcome = Assert.Single(result.CandidateOutcomes);
        Assert.Equal("(unknown)", outcome.RelativePath.Value);
        Assert.Equal("MalformedProviderRecord", outcome.Reason);
        string ownedResult = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(rawPath, ownedResult, StringComparison.Ordinal);
        Assert.DoesNotContain(hostFragment, ownedResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SafeProviderPath_RevalidateUnknown_RemainsUnknown()
    {
        SafeProviderPath unknown = SafeProviderPath.FromProviderScalar(null);

        SafeProviderPath revalidated = SafeProviderPath.Revalidate(unknown);

        Assert.Equal("(unknown)", revalidated.Value);
        Assert.False(revalidated.IsKnown);
    }

    private static byte[] CreateEnvelopeWithRawDataSuffix(
        string query,
        object?[] results,
        string dataSuffix)
    {
        string rows = JsonSerializer.Serialize(results);
        string json = "{\"status\":\"ok\",\"message\":null,\"error\":null,\"data\":{\"success\":true,\"query\":" +
            JsonSerializer.Serialize(query) +
            ",\"record_count\":" +
            results.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"results\":" +
            rows +
            dataSuffix +
            "}}";
        return Encoding.UTF8.GetBytes(json);
    }

    private static CodeGraphProviderResult ParseCodeUnits(object?[] rows, string? workingCopyPath = null)
    {
        byte[] codeUnits = CreateEnvelopeWithRawDataSuffix(
            CodeGraphContextProviderQueries.CodeUnits,
            rows,
            string.Empty);
        byte[] relationships = CreateEnvelopeWithRawDataSuffix(
            CodeGraphContextProviderQueries.Relationships,
            [],
            string.Empty);
        return CodeGraphHttpProjectionParser.Parse(
            codeUnits,
            relationships,
            workingCopyPath ?? Path.GetTempPath(),
            TimeSpan.Zero);
    }

    private static Dictionary<string, object?> CreateCodeUnitRow(
        string providerKind,
        string? path,
        string? name,
        int? startLine,
        int? endLine,
        object? parserLanguage) =>
        new(StringComparer.Ordinal)
        {
            ["record_kind"] = "code_unit",
            ["provider_kind"] = providerKind,
            ["path"] = path,
            ["name"] = name,
            ["start_line"] = startLine,
            ["end_line"] = endLine,
            ["parser_language"] = parserLanguage,
        };

    private static Dictionary<string, object?> CreateRelationshipRow(
        string relationshipKind,
        string sourceProviderKind,
        string? sourcePath,
        string? sourceName,
        int? sourceStartLine,
        int? sourceEndLine,
        string targetProviderKind,
        string? targetPath,
        string? targetName,
        int? targetStartLine,
        int? targetEndLine) =>
        new(StringComparer.Ordinal)
        {
            ["record_kind"] = "relationship",
            ["relationship_kind"] = relationshipKind,
            ["source_provider_kind"] = sourceProviderKind,
            ["source_path"] = sourcePath,
            ["source_name"] = sourceName,
            ["source_start_line"] = sourceStartLine,
            ["source_end_line"] = sourceEndLine,
            ["target_provider_kind"] = targetProviderKind,
            ["target_path"] = targetPath,
            ["target_name"] = targetName,
            ["target_start_line"] = targetStartLine,
            ["target_end_line"] = targetEndLine,
        };

    private static byte[] CreateEnvelopeFromRawResults(string query, string rawRows, int recordCount)
    {
        string json = "{\"status\":\"ok\",\"message\":null,\"error\":null,\"data\":{\"success\":true,\"query\":" +
            JsonSerializer.Serialize(query) +
            ",\"record_count\":" +
            recordCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"results\":[" +
            rawRows +
            "]}}";
        return Encoding.UTF8.GetBytes(json);
    }

    private static int CountOccurrences(string value, string fragment)
    {
        int count = 0;
        int start = 0;
        while ((start = value.IndexOf(fragment, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += fragment.Length;
        }

        return count;
    }

    public static TheoryData<string, string, bool> UnsafeProviderPathCases
    {
        get
        {
            var data = new TheoryData<string, string, bool>();
            string[] unsafePaths =
            [
                @"C:\Users\Admin\private\secret.cs",
                @"\\build-server\private-share\secret.cs",
                "/etc/private/secret.cs",
                "../private/secret.cs",
                "src/\u0001host-secret.cs",
                "src//private/secret.cs",
                "src/" + new string('x', 2_049) + "oversize-host-fragment.cs",
            ];
            string[] fragments =
            [
                "Users",
                "build-server",
                "etc/private",
                "private/secret",
                "host-secret",
                "private/secret",
                "oversize-host-fragment",
            ];
            foreach (bool relationshipRow in new[] { false, true })
            {
                for (var index = 0; index < unsafePaths.Length; index++)
                {
                    data.Add(unsafePaths[index], fragments[index], relationshipRow);
                }
            }

            return data;
        }
    }
}
