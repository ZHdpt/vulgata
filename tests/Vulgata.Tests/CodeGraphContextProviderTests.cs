using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
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
}
