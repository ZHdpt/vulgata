using Microsoft.Extensions.AI;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.PreparedSource;

public sealed class PreparedSourceToolFunctionTests
{
    private static readonly Guid RepositoryId =
        Guid.Parse("a6000000-0000-4000-8000-000000000001");

    [Fact]
    public void Create_ExposesExactlyThreeOrderedFunctionsWithClosedArguments()
    {
        var context = new RecordingPreparedSourceReadContext(RepositoryId);

        IReadOnlyList<AIFunction> functions = new PreparedSourceFunctions(context).Create();

        Assert.Equal(
            ["list_prepared_source", "read_prepared_source", "search_prepared_source"],
            functions.Select(function => function.Name));
        Assert.Equal(3, functions.Count);
        AssertSchema(functions[0], ["relativeDirectory", "pageSize", "continuation"]);
        AssertSchema(functions[1], ["relativePath", "byteOffset", "byteCount"]);
        AssertSchema(functions[2], ["relativeDirectory", "literal", "pageSize", "continuation"]);
        Assert.Equal(0, context.CallCount);
    }

    [Fact]
    public async Task Functions_ForwardExactRequestsAndCancellation_ReturnCoreResultsUnchanged()
    {
        var context = new RecordingPreparedSourceReadContext(RepositoryId);
        IReadOnlyList<AIFunction> functions = new PreparedSourceFunctions(context).Create();
        using var cancellationSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationSource.Token;

        object? listResult = await functions[0].InvokeAsync(
            Arguments(
                ("relativeDirectory", "src"),
                ("pageSize", 100),
                ("continuation", "list-cursor")),
            cancellationToken);
        object? readResult = await functions[1].InvokeAsync(
            Arguments(
                ("relativePath", "src/module.oddx"),
                ("byteOffset", 7L),
                ("byteCount", 32)),
            cancellationToken);
        object? searchResult = await functions[2].InvokeAsync(
            Arguments(
                ("relativeDirectory", "src"),
                ("literal", "target"),
                ("pageSize", 64),
                ("continuation", "search-cursor")),
            cancellationToken);

        Assert.Same(context.ListResult, listResult);
        Assert.Same(context.ReadResult, readResult);
        Assert.Same(context.SearchResult, searchResult);
        Assert.Equal(new PreparedSourceListRequest("src", 100, "list-cursor"), context.LastListRequest);
        Assert.Equal(
            new PreparedSourceReadTextRangeRequest("src/module.oddx", 7, 32),
            context.LastReadRequest);
        Assert.Equal(
            new PreparedSourceSearchTextRequest("src", "target", 64, "search-cursor"),
            context.LastSearchRequest);
        Assert.Equal(cancellationToken, context.LastListCancellationToken);
        Assert.Equal(cancellationToken, context.LastReadCancellationToken);
        Assert.Equal(cancellationToken, context.LastSearchCancellationToken);
        Assert.Equal(3, context.CallCount);
        Assert.Equal(0, context.DisposeCallCount);
    }

    [Theory]
    [InlineData("unexpectedField")]
    [InlineData("batchId")]
    [InlineData("repositoryId")]
    [InlineData("rootPath")]
    [InlineData("physicalRootPath")]
    [InlineData("path")]
    [InlineData("absolutePath")]
    [InlineData("subjectId")]
    [InlineData("approvalIdentity")]
    [InlineData("rootBindingIdentity")]
    [InlineData("writeText")]
    [InlineData("command")]
    [InlineData("shellCommand")]
    public async Task Functions_RejectUnknownAuthorityAndMutationFieldsWithoutCoreCalls(string extraField)
    {
        var context = new RecordingPreparedSourceReadContext(RepositoryId);
        IReadOnlyList<AIFunction> functions = new PreparedSourceFunctions(context).Create();
        string[] functionNames = ["list_prepared_source", "read_prepared_source", "search_prepared_source"];

        for (int index = 0; index < functions.Count; index++)
        {
            Dictionary<string, object?> values = ValidArguments(functionNames[index]);
            values[extraField] = "model-selected-value";

            object? rawResult = await functions[index].InvokeAsync(
                new AIFunctionArguments(values),
                CancellationToken.None);

            PreparedSourceResult result = Assert.IsType<PreparedSourceResult>(rawResult);
            Assert.Equal(RepositoryId, result.RepositoryId);
            Assert.Equal(PreparedSourceOutcome.Denied, result.Outcome);
            Assert.Empty(result.Entries);
            Assert.Empty(result.Matches);
            Assert.Empty(result.Limitations);
            Assert.Null(result.Text);
            Assert.Null(result.ReadLocation);
            Assert.Null(result.Continuation);
            Assert.Null(result.NextByteOffset);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(0, context.CallCount);
        }
    }

    [Fact]
    public void Create_BorrowsContextAndDoesNotDisposeItsGrant()
    {
        var context = new RecordingPreparedSourceReadContext(RepositoryId);

        _ = new PreparedSourceFunctions(context).Create();
        _ = new PreparedSourceFunctions(context).Create();

        Assert.Equal(0, context.DisposeCallCount);
        Assert.Equal(0, context.CallCount);
    }

    private static void AssertSchema(AIFunction function, string[] expectedProperties)
    {
        using System.Text.Json.JsonDocument schema =
            System.Text.Json.JsonDocument.Parse(function.JsonSchema.GetRawText());
        System.Text.Json.JsonElement root = schema.RootElement;

        Assert.Equal(System.Text.Json.JsonValueKind.Object, root.ValueKind);
        Assert.True(
            root.TryGetProperty("additionalProperties", out System.Text.Json.JsonElement additionalProperties),
            $"{function.Name} must declare a closed schema: {root.GetRawText()}");
        Assert.False(additionalProperties.GetBoolean());
        Assert.Equal(
            expectedProperties,
            root.GetProperty("properties").EnumerateObject().Select(property => property.Name));
    }

    private static AIFunctionArguments Arguments(params (string Name, object? Value)[] values) =>
        new(values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal));

    private static Dictionary<string, object?> ValidArguments(string functionName) => functionName switch
    {
        "list_prepared_source" => new(StringComparer.Ordinal)
        {
            ["relativeDirectory"] = "",
            ["pageSize"] = 10,
            ["continuation"] = null,
        },
        "read_prepared_source" => new(StringComparer.Ordinal)
        {
            ["relativePath"] = "module.oddx",
            ["byteOffset"] = 0L,
            ["byteCount"] = 1,
        },
        "search_prepared_source" => new(StringComparer.Ordinal)
        {
            ["relativeDirectory"] = "",
            ["literal"] = "match",
            ["pageSize"] = 10,
            ["continuation"] = null,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(functionName)),
    };

    private sealed class RecordingPreparedSourceReadContext(Guid repositoryId)
        : IPreparedSourceReadContext
    {
        public Guid RepositoryId { get; } = repositoryId;

        public PreparedSourceContextLifetime Lifetime => PreparedSourceContextLifetime.Open;

        public PreparedSourceResult ListResult { get; } = new()
        {
            RepositoryId = repositoryId,
            Operation = PreparedSourceOperation.List,
            Outcome = PreparedSourceOutcome.Complete,
            Entries =
            [
                new PreparedSourceEntry(
                    new PreparedSourceLocation(repositoryId, "src/item.oddx"),
                    PreparedSourceEntryKind.RegularFile),
            ],
        };

        public PreparedSourceResult ReadResult { get; } = new()
        {
            RepositoryId = repositoryId,
            Operation = PreparedSourceOperation.ReadTextRange,
            Outcome = PreparedSourceOutcome.Complete,
            Text = "controlled source text",
            ReadLocation = new PreparedSourceReadLocation(repositoryId, "src/module.oddx", 7, 39),
        };

        public PreparedSourceResult SearchResult { get; } = new()
        {
            RepositoryId = repositoryId,
            Operation = PreparedSourceOperation.SearchText,
            Outcome = PreparedSourceOutcome.Complete,
            Matches =
            [
                new PreparedSourceMatch(
                    new PreparedSourceMatchLocation(repositoryId, "src/module.oddx", 7, 6, 1, 8),
                    "target"),
            ],
        };

        public int CallCount { get; private set; }

        public int DisposeCallCount { get; private set; }

        public PreparedSourceListRequest? LastListRequest { get; private set; }

        public PreparedSourceReadTextRangeRequest? LastReadRequest { get; private set; }

        public PreparedSourceSearchTextRequest? LastSearchRequest { get; private set; }

        public CancellationToken LastListCancellationToken { get; private set; }

        public CancellationToken LastReadCancellationToken { get; private set; }

        public CancellationToken LastSearchCancellationToken { get; private set; }

        public Task<PreparedSourceResult> ListAsync(
            PreparedSourceListRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastListRequest = request;
            LastListCancellationToken = cancellationToken;
            return Task.FromResult(ListResult);
        }

        public Task<PreparedSourceResult> ReadTextRangeAsync(
            PreparedSourceReadTextRangeRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastReadRequest = request;
            LastReadCancellationToken = cancellationToken;
            return Task.FromResult(ReadResult);
        }

        public Task<PreparedSourceResult> SearchTextAsync(
            PreparedSourceSearchTextRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastSearchRequest = request;
            LastSearchCancellationToken = cancellationToken;
            return Task.FromResult(SearchResult);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }
    }
}
