using Microsoft.Extensions.AI;
using System.Text.Json;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;

using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.AnalysisRuntime;

public sealed class PreparedJavaNavigationFunctionsTests
{
    private static readonly Guid RepositoryId =
        Guid.Parse("a6000000-0000-4000-8000-000000000182");

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_ExposesExactlyTwoOrderedNavigationFunctionsWithClosedArguments()
    {
        var context = new RecordingPreparedJavaNavigationContext(RepositoryId);

        IReadOnlyList<AIFunction> functions =
            new PreparedJavaNavigationFunctions(context).Create();

        Assert.Equal(
            ["navigate_java_definition", "navigate_java_references"],
            functions.Select(function => function.Name));
        Assert.Equal(2, functions.Count);
        AssertSchema(
            functions[0],
            ["relativePath", "line", "character", "maximumLocations"]);
        AssertSchema(
            functions[1],
            ["relativePath", "line", "character", "maximumLocations"]);
        Assert.Equal(0, context.CallCount);
        Assert.Equal(0, context.DisposeCallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Functions_ForwardExactDefinitionAndReferenceRequestsAndCancellation_ReturnResultsUnchanged()
    {
        var context = new RecordingPreparedJavaNavigationContext(RepositoryId)
        {
            DefinitionResult = new PreparedJavaNavigationResult(
                RepositoryId,
                PreparedJavaNavigationOperation.Definition,
                PreparedJavaNavigationOutcome.Observed,
                [new PreparedJavaLocation(
                    new PreparedSourceLocation(RepositoryId, "src/main/java/example/Target.java"),
                    4,
                    9,
                    4,
                    21)],
                [],
                null),
            ReferencesResult = new PreparedJavaNavigationResult(
                RepositoryId,
                PreparedJavaNavigationOperation.References,
                PreparedJavaNavigationOutcome.Limited,
                [],
                [new PreparedJavaLimitation("location_limit", null)],
                PreparedJavaStoppingLimit.LocationLimit),
        };
        IReadOnlyList<AIFunction> functions = new PreparedJavaNavigationFunctions(context).Create();
        using var cancellationSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationSource.Token;

        object? definition = await functions[0].InvokeAsync(
            Arguments(
                ("relativePath", "src/main/java/example/Caller.java"),
                ("line", 12),
                ("character", 25),
                ("maximumLocations", 17)),
            cancellationToken);
        object? references = await functions[1].InvokeAsync(
            Arguments(
                ("relativePath", "src/main/java/example/Target.java"),
                ("line", 3),
                ("character", 9),
                ("maximumLocations", 24)),
            cancellationToken);

        Assert.Same(context.DefinitionResult, definition);
        Assert.Same(context.ReferencesResult, references);
        Assert.Equal(
            new PreparedJavaNavigationRequest("src/main/java/example/Caller.java", 12, 25, 17),
            context.LastDefinitionRequest);
        Assert.Equal(
            new PreparedJavaNavigationRequest("src/main/java/example/Target.java", 3, 9, 24),
            context.LastReferencesRequest);
        Assert.Equal(cancellationToken, context.LastDefinitionCancellationToken);
        Assert.Equal(cancellationToken, context.LastReferencesCancellationToken);
        Assert.Equal(2, context.CallCount);
        Assert.Equal(0, context.DisposeCallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Function_WhenCancellationArrivesDuringCall_ReturnsEmptyCancelledResult()
    {
        var context = new RecordingPreparedJavaNavigationContext(RepositoryId);
        var callStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateResultReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.DefinitionResult = new PreparedJavaNavigationResult(
            RepositoryId,
            PreparedJavaNavigationOperation.Definition,
            PreparedJavaNavigationOutcome.Observed,
            [new PreparedJavaLocation(
                new PreparedSourceLocation(RepositoryId, "src/main/java/example/Secret.java"),
                7,
                2,
                7,
                15)],
            [],
            null);
        context.DefinitionHandler = async _ =>
        {
            callStarted.TrySetResult();
            await lateResultReady.Task;
            return context.DefinitionResult;
        };
        IReadOnlyList<AIFunction> functions = new PreparedJavaNavigationFunctions(context).Create();
        using var cancellationSource = new CancellationTokenSource();

        Task<object?> invocation = functions[0].InvokeAsync(
            Arguments(
                ("relativePath", "src/main/java/example/Caller.java"),
                ("line", 12),
                ("character", 25),
                ("maximumLocations", 17)),
            cancellationSource.Token).AsTask();
        await callStarted.Task;
        cancellationSource.Cancel();
        lateResultReady.TrySetResult();
        PreparedJavaNavigationResult result = Assert.IsType<PreparedJavaNavigationResult>(await invocation);

        Assert.Equal(RepositoryId, result.RepositoryId);
        Assert.Equal(PreparedJavaNavigationOperation.Definition, result.Operation);
        Assert.Equal(PreparedJavaNavigationOutcome.Cancelled, result.Outcome);
        Assert.Empty(result.Locations);
        Assert.Empty(result.Limitations);
        Assert.Null(result.StoppingLimit);
        Assert.Equal(cancellationSource.Token, context.LastDefinitionCancellationToken);
        Assert.Equal(1, context.CallCount);
        Assert.Equal(0, context.DisposeCallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Functions_RejectPhysicalRootAndAuthorityArgumentsWithoutContextCalls()
    {
        const string protectedRoot = @"C:\owned\prepared-java";
        var context = new RecordingPreparedJavaNavigationContext(RepositoryId);
        IReadOnlyList<AIFunction> functions = new PreparedJavaNavigationFunctions(context).Create();
        AIFunctionArguments arguments = Arguments(
            ("relativePath", "src/main/java/example/Caller.java"),
            ("line", 12),
            ("character", 25),
            ("maximumLocations", 17),
            ("physicalRootPath", protectedRoot),
            ("approvalIdentity", "private-approval"));

        object? rawResult = await functions[0].InvokeAsync(arguments, CancellationToken.None);

        PreparedJavaNavigationResult result = Assert.IsType<PreparedJavaNavigationResult>(rawResult);
        Assert.Equal(RepositoryId, result.RepositoryId);
        Assert.Equal(PreparedJavaNavigationOperation.Definition, result.Operation);
        Assert.Equal(PreparedJavaNavigationOutcome.Denied, result.Outcome);
        Assert.Empty(result.Locations);
        Assert.Empty(result.Limitations);
        Assert.Null(result.StoppingLimit);
        string serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(protectedRoot, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-approval", serialized, StringComparison.Ordinal);
        Assert.Equal(0, context.CallCount);
        Assert.Equal(0, context.DisposeCallCount);
    }

    private static AIFunctionArguments Arguments(params (string Name, object? Value)[] values) =>
        new(values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal));

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

    private sealed class RecordingPreparedJavaNavigationContext(Guid repositoryId)
        : IPreparedJavaNavigationContext
    {
        public Guid RepositoryId { get; } = repositoryId;

        public int CallCount { get; private set; }

        public int DisposeCallCount { get; private set; }

        public PreparedJavaNavigationResult DefinitionResult { get; set; } = new(
            repositoryId,
            PreparedJavaNavigationOperation.Definition,
            PreparedJavaNavigationOutcome.NoResultObserved,
            [],
            [],
            null);

        public PreparedJavaNavigationResult ReferencesResult { get; set; } = new(
            repositoryId,
            PreparedJavaNavigationOperation.References,
            PreparedJavaNavigationOutcome.NoResultObserved,
            [],
            [],
            null);

        public PreparedJavaRequestHandler? DefinitionHandler { get; set; }

        public PreparedJavaRequestHandler? ReferencesHandler { get; set; }

        public PreparedJavaNavigationRequest? LastDefinitionRequest { get; private set; }

        public PreparedJavaNavigationRequest? LastReferencesRequest { get; private set; }

        public CancellationToken LastDefinitionCancellationToken { get; private set; }

        public CancellationToken LastReferencesCancellationToken { get; private set; }

        public Task<PreparedJavaNavigationResult> GetDefinitionAsync(
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastDefinitionRequest = request;
            LastDefinitionCancellationToken = cancellationToken;
            return DefinitionHandler?.Invoke(cancellationToken) ??
                Task.FromResult(DefinitionResult);
        }

        public Task<PreparedJavaNavigationResult> GetReferencesAsync(
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastReferencesRequest = request;
            LastReferencesCancellationToken = cancellationToken;
            return ReferencesHandler?.Invoke(cancellationToken) ??
                Task.FromResult(ReferencesResult);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private delegate Task<PreparedJavaNavigationResult> PreparedJavaRequestHandler(
        CancellationToken cancellationToken);
}
