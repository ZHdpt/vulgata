using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.Agents;

/// <summary>
/// Locks the accepted public seam before behavior tests exercise it. The existing
/// prepared-source function tests cover only Story 18.1's three read tools; this
/// assertion catches accidentally omitting or reshaping the separate native-tool
/// contract required for Story 18.3.
/// </summary>
public sealed class CodeGraphFunctionsTests
{
    private static readonly Guid RepositoryId =
        Guid.Parse("18300000-0000-4000-8000-000000000001");

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CodeGraphWorkerTool_PublicSeam_ExposesTheAcceptedCallableContract()
    {
        Assembly coreAssembly = typeof(IPreparedSourceReadContext).Assembly;
        Assembly agentsAssembly = typeof(PreparedSourceFunctions).Assembly;
        Type sessionType = RequireType(coreAssembly, "Vulgata.Core.DomainServices.ICodeGraphWorkerToolSession");
        Type readinessType = RequireType(coreAssembly, "Vulgata.Core.DomainServices.CodeGraphToolReadiness");
        Type invocationResultType = RequireType(coreAssembly, "Vulgata.Core.DomainServices.CodeGraphToolInvocationResult");
        Type registrationType = RequireType(agentsAssembly, "Vulgata.Agents.WorkerExecution.CodeGraphToolRegistration");
        Type functionsType = RequireType(agentsAssembly, "Vulgata.Agents.WorkerExecution.CodeGraphFunctions");

        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(sessionType));
        AssertMethod(
            sessionType,
            "DiscoverAsync",
            typeof(Task<>).MakeGenericType(readinessType),
            typeof(CancellationToken));
        AssertMethod(
            sessionType,
            "ExploreAsync",
            typeof(Task<>).MakeGenericType(invocationResultType),
            typeof(string),
            typeof(int),
            typeof(CancellationToken));

        ConstructorInfo? constructor = functionsType.GetConstructor([sessionType]);
        Assert.True(constructor is not null, "CodeGraphFunctions must borrow the accepted tool session through its public constructor.");
        AssertMethod(
            functionsType,
            "CreateAsync",
            typeof(Task<>).MakeGenericType(registrationType),
            typeof(CancellationToken));

        Assert.Equal(readinessType, registrationType.GetProperty("Readiness")?.PropertyType);
        Assert.Equal(typeof(IReadOnlyList<AIFunction>), registrationType.GetProperty("Functions")?.PropertyType);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CreateAsync_WhenNativeDiscoveryIsReady_ExposesOnlyTheFixedClosedExploreTool()
    {
        var readiness = ReadyReadiness();
        var session = new ControlledCodeGraphSession(readiness);

        CodeGraphToolRegistration registration = await new CodeGraphFunctions(session).CreateAsync(CancellationToken.None);

        Assert.Same(readiness, registration.Readiness);
        Assert.Equal(RepositoryId, registration.Readiness.RepositoryId);
        Assert.Equal(CodeGraphToolStage.Discovery, registration.Readiness.Stage);
        Assert.Equal(CodeGraphToolIssueCode.None, registration.Readiness.IssueCode);
        Assert.True(registration.Readiness.IsReady);
        AIFunction function = Assert.Single(registration.Functions);
        Assert.Equal("codegraph_explore", function.Name);
        AssertClosedExploreSchema(function);
        Assert.Equal(1, session.DiscoverCallCount);
        Assert.Equal(0, session.ExploreCallCount);
        Assert.Equal(0, session.DisposeCallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(CodeGraphToolReadinessStatus.Unavailable, CodeGraphToolStage.Preparation, CodeGraphToolIssueCode.InstallationUnavailable)]
    [InlineData(CodeGraphToolReadinessStatus.Unprepared, CodeGraphToolStage.Preparation, CodeGraphToolIssueCode.SourceUnprepared)]
    [InlineData(CodeGraphToolReadinessStatus.PreparationFailed, CodeGraphToolStage.Preparation, CodeGraphToolIssueCode.PreparationFailed)]
    [InlineData(CodeGraphToolReadinessStatus.ConnectionFailed, CodeGraphToolStage.Connection, CodeGraphToolIssueCode.ConnectionFailed)]
    [InlineData(CodeGraphToolReadinessStatus.DiscoveryFailed, CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.ToolMissing)]
    [InlineData(CodeGraphToolReadinessStatus.AuthorityDenied, CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.AuthorityDenied)]
    [InlineData(CodeGraphToolReadinessStatus.AuthorityUnavailable, CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.AuthorityUnavailable)]
    [InlineData(CodeGraphToolReadinessStatus.Expired, CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.SessionExpired)]
    [InlineData(CodeGraphToolReadinessStatus.Cancelled, CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.Cancelled)]
    public async Task CreateAsync_WhenDiscoveryIsNotReady_ReturnsNoWorkerToolAndBorrowsSession(
        CodeGraphToolReadinessStatus status,
        CodeGraphToolStage expectedStage,
        CodeGraphToolIssueCode expectedIssueCode)
    {
        var readiness = new CodeGraphToolReadiness(
            status,
            RepositoryId,
            expectedStage,
            expectedIssueCode);
        var session = new ControlledCodeGraphSession(readiness);

        CodeGraphToolRegistration registration = await new CodeGraphFunctions(session).CreateAsync(CancellationToken.None);

        Assert.Same(readiness, registration.Readiness);
        Assert.Equal(RepositoryId, registration.Readiness.RepositoryId);
        Assert.Equal(expectedStage, registration.Readiness.Stage);
        Assert.Equal(expectedIssueCode, registration.Readiness.IssueCode);
        Assert.False(registration.Readiness.IsReady);
        Assert.Empty(registration.Functions);
        Assert.Equal(1, session.DiscoverCallCount);
        Assert.Equal(0, session.ExploreCallCount);
        Assert.Equal(0, session.DisposeCallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Explore_ValidArguments_UsesDefaultFileLimitForwardsCancellationAndReturnsTypedSanitizedOutcome()
    {
        var result = new CodeGraphToolInvocationResult
        {
            Status = CodeGraphToolInvocationStatus.Complete,
            RepositoryId = RepositoryId,
            Text = "Calculator.Add(int, int)",
            Locations = [new PreparedSourceLocation(RepositoryId, "src/Calculator.cs")],
            Excerpts =
            [
                new CodeGraphSourceExcerpt(
                    new PreparedSourceLocation(RepositoryId, "src/Calculator.cs"),
                    "public int Add(int a, int b)"),
            ],
        };
        var session = new ControlledCodeGraphSession(
            ReadyReadiness(),
            result);
        using var cancellationSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationSource.Token;
        CodeGraphToolRegistration registration = await new CodeGraphFunctions(session).CreateAsync(CancellationToken.None);

        object? returned = await Assert.Single(registration.Functions).InvokeAsync(
            Arguments(("query", "Calculator.Add")),
            cancellationToken);

        Assert.Same(result, returned);
        Assert.Equal(RepositoryId, Assert.IsType<CodeGraphToolInvocationResult>(returned).RepositoryId);
        Assert.Equal(CodeGraphToolIssueCode.None, result.IssueCode);
        Assert.Empty(result.Limitations);
        CodeGraphSourceExcerpt excerpt = Assert.Single(result.Excerpts);
        Assert.Equal(RepositoryId, excerpt.Location.RepositoryId);
        Assert.Equal("src/Calculator.cs", excerpt.Location.RelativePath);
        Assert.Equal("public int Add(int a, int b)", excerpt.Text);
        Assert.Equal("Calculator.Add", session.LastQuery);
        Assert.Equal(4, session.LastMaxFiles);
        Assert.Equal(cancellationToken, session.LastExploreCancellationToken);
        Assert.Equal(1, session.ExploreCallCount);
        Assert.Equal(0, session.DisposeCallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(1)]
    [InlineData(12)]
    public async Task Explore_AcceptsConfiguredFileLimitBoundaries(int maxFiles)
    {
        var session = ReadySession();
        AIFunction function = Assert.Single((await new CodeGraphFunctions(session).CreateAsync(CancellationToken.None)).Functions);

        _ = await function.InvokeAsync(Arguments(("query", "lookup"), ("maxFiles", maxFiles)));

        Assert.Equal(maxFiles, session.LastMaxFiles);
        Assert.Equal(1, session.ExploreCallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Explore_EnforcesUtf8QueryLimitAt512Bytes()
    {
        string query = new string('\u754c', 170) + "ab"; // 510 multibyte bytes plus two ASCII bytes.
        Assert.Equal(512, System.Text.Encoding.UTF8.GetByteCount(query));
        var session = ReadySession();
        AIFunction function = Assert.Single((await new CodeGraphFunctions(session).CreateAsync(CancellationToken.None)).Functions);

        _ = await function.InvokeAsync(Arguments(("query", query)));

        Assert.Equal(query, session.LastQuery);
        Assert.Equal(1, session.ExploreCallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("projectPath")]
    [InlineData("root")]
    [InlineData("command")]
    [InlineData("environment")]
    [InlineData("unknownField")]
    [InlineData("nestedAuthoritySelector")]
    [InlineData("queryMissing")]
    [InlineData("queryNull")]
    [InlineData("queryNumber")]
    [InlineData("queryEmpty")]
    [InlineData("queryWhitespace")]
    [InlineData("queryTooManyUtf8Bytes")]
    [InlineData("queryInvalidUtf8")]
    [InlineData("maxFilesZero")]
    [InlineData("maxFilesTooLarge")]
    [InlineData("maxFilesText")]
    [InlineData("maxFilesFraction")]
    public async Task Explore_RejectsMalformedOrUntrustedArgumentsWithoutSessionDispatch(string scenario)
    {
        var session = ReadySession();
        AIFunction function = Assert.Single((await new CodeGraphFunctions(session).CreateAsync(CancellationToken.None)).Functions);
        AIFunctionArguments arguments = InvalidArguments(scenario);

        object? rawResult = await function.InvokeAsync(arguments);

        CodeGraphToolInvocationResult denied = Assert.IsType<CodeGraphToolInvocationResult>(rawResult);
        Assert.Equal(CodeGraphToolInvocationStatus.Denied, denied.Status);
        Assert.Null(denied.RepositoryId);
        Assert.Null(denied.Text);
        Assert.Empty(denied.Locations);
        Assert.Equal(0, session.ExploreCallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CreateAndInvoke_ForwardTheirOwnCancellationTokensUnchanged()
    {
        var session = ReadySession();
        using var discoveryCancellation = new CancellationTokenSource();
        using var invocationCancellation = new CancellationTokenSource();
        CancellationToken discoveryToken = discoveryCancellation.Token;
        CancellationToken invocationToken = invocationCancellation.Token;

        CodeGraphToolRegistration registration = await new CodeGraphFunctions(session).CreateAsync(discoveryToken);
        AIFunction function = Assert.Single(registration.Functions);
        _ = await function.InvokeAsync(Arguments(("query", "target")), invocationToken);

        Assert.Equal(discoveryToken, session.LastDiscoverCancellationToken);
        Assert.Equal(invocationToken, session.LastExploreCancellationToken);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CreateAsync_ReadyWithoutBoundRepository_DoesNotGrantNativeTool()
    {
        var readiness = new CodeGraphToolReadiness(
            CodeGraphToolReadinessStatus.Ready,
            RepositoryId: null,
            Stage: CodeGraphToolStage.Discovery,
            IssueCode: CodeGraphToolIssueCode.None);
        var session = new ControlledCodeGraphSession(readiness);

        CodeGraphToolRegistration registration = await new CodeGraphFunctions(session).CreateAsync(CancellationToken.None);

        Assert.False(registration.Readiness.IsReady);
        Assert.Empty(registration.Functions);
        Assert.Equal(0, session.ExploreCallCount);
        Assert.Equal(0, session.DisposeCallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Explore_LimitedOutcomeCarriesTypedLimitationWithoutDiagnostics()
    {
        var limited = new CodeGraphToolInvocationResult
        {
            Status = CodeGraphToolInvocationStatus.Limited,
            RepositoryId = RepositoryId,
            IssueCode = CodeGraphToolIssueCode.ResultLimit,
            Limitations = [CodeGraphToolIssueCode.ResultLimit],
            Excerpts =
            [
                new CodeGraphSourceExcerpt(
                    new PreparedSourceLocation(RepositoryId, "src/Calculator.cs"),
                    "public int Add(int a, int b)",
                    LineStart: 12,
                    LineEnd: 12),
            ],
        };
        var session = ReadySession(limited);
        AIFunction function = Assert.Single((await new CodeGraphFunctions(session).CreateAsync(CancellationToken.None)).Functions);

        object? raw = await function.InvokeAsync(Arguments(("query", "Calculator.Add")), CancellationToken.None);

        CodeGraphToolInvocationResult result = Assert.IsType<CodeGraphToolInvocationResult>(raw);
        Assert.Same(limited, result);
        Assert.Equal(CodeGraphToolIssueCode.ResultLimit, result.IssueCode);
        Assert.Equal([CodeGraphToolIssueCode.ResultLimit], result.Limitations);
        Assert.Null(result.Text);
        Assert.Single(result.Excerpts);
        Assert.Null(result.GetType().GetProperty("PhysicalRootPath"));
        Assert.Null(result.GetType().GetProperty("ApprovalIdentity"));
        Assert.Null(result.GetType().GetProperty("RawArguments"));
        Assert.Null(result.GetType().GetProperty("ServerInstructions"));
        Assert.Null(result.GetType().GetProperty("ProcessDiagnostics"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CodeGraphFunctions_BorrowsSessionAndCallerOwnsItsDisposal()
    {
        var session = ReadySession();
        CodeGraphFunctions functions = new(session);
        CodeGraphToolRegistration registration = await functions.CreateAsync(CancellationToken.None);
        AIFunction function = Assert.Single(registration.Functions);
        _ = await function.InvokeAsync(Arguments(("query", "target")));

        Assert.Equal(0, session.DisposeCallCount);
        await session.DisposeAsync();
        Assert.Equal(1, session.DisposeCallCount);

        session.ReturnExpiredWhenDisposed = true;
        object? expiredRaw = await function.InvokeAsync(Arguments(("query", "target")));
        CodeGraphToolInvocationResult expired = Assert.IsType<CodeGraphToolInvocationResult>(expiredRaw);
        Assert.Equal(CodeGraphToolInvocationStatus.Expired, expired.Status);
        Assert.Null(expired.Text);
        Assert.Empty(expired.Locations);
        Assert.Equal(2, session.ExploreCallCount);
        Assert.Equal(1, session.DisposeCallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Explore_AuthorityFailureReturnsTypedIssueWithoutBufferedSource()
    {
        var denied = new CodeGraphToolInvocationResult
        {
            Status = CodeGraphToolInvocationStatus.AuthorityUnavailable,
            RepositoryId = RepositoryId,
            IssueCode = CodeGraphToolIssueCode.AuthorityUnavailable,
        };
        var session = ReadySession(denied);
        AIFunction function = Assert.Single((await new CodeGraphFunctions(session).CreateAsync(CancellationToken.None)).Functions);

        object? raw = await function.InvokeAsync(Arguments(("query", "target")), CancellationToken.None);

        CodeGraphToolInvocationResult result = Assert.IsType<CodeGraphToolInvocationResult>(raw);
        Assert.Same(denied, result);
        Assert.Equal(CodeGraphToolInvocationStatus.AuthorityUnavailable, result.Status);
        Assert.Equal(CodeGraphToolIssueCode.AuthorityUnavailable, result.IssueCode);
        Assert.Empty(result.Limitations);
        Assert.Null(result.Text);
        Assert.Empty(result.Locations);
        Assert.Empty(result.Excerpts);
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        Type? type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
        Assert.True(type is not null, $"Accepted public CodeGraph seam type '{fullName}' is missing.");
        return type!;
    }

    private static void AssertMethod(
        Type type,
        string name,
        Type returnType,
        params Type[] parameterTypes)
    {
        MethodInfo? method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.True(method is not null, $"Accepted public method '{type.Name}.{name}' is missing.");
        Assert.Equal(returnType, method!.ReturnType);
        Assert.Equal(parameterTypes, method.GetParameters().Select(parameter => parameter.ParameterType));
    }

    private static void AssertClosedExploreSchema(AIFunction function)
    {
        using JsonDocument document = JsonDocument.Parse(function.JsonSchema.GetRawText());
        JsonElement schema = document.RootElement;
        Assert.Equal(JsonValueKind.Object, schema.ValueKind);
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["query", "maxFiles"],
            schema.GetProperty("properties").EnumerateObject().Select(property => property.Name));
        Assert.Equal(["query"], schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("string", schema.GetProperty("properties").GetProperty("query").GetProperty("type").GetString());
        JsonElement maxFiles = schema.GetProperty("properties").GetProperty("maxFiles");
        Assert.Equal("integer", maxFiles.GetProperty("type").GetString());
        Assert.Equal(1, maxFiles.GetProperty("minimum").GetInt32());
        Assert.Equal(12, maxFiles.GetProperty("maximum").GetInt32());
    }

    private static ControlledCodeGraphSession ReadySession(CodeGraphToolInvocationResult? result = null) => new(
        ReadyReadiness(),
        result);

    private static CodeGraphToolReadiness ReadyReadiness() => new(
        CodeGraphToolReadinessStatus.Ready,
        RepositoryId,
        CodeGraphToolStage.Discovery,
        CodeGraphToolIssueCode.None);

    private static AIFunctionArguments Arguments(params (string Name, object? Value)[] values) =>
        new(values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal));

    private static AIFunctionArguments InvalidArguments(string scenario)
    {
        Dictionary<string, object?> values = new(StringComparer.Ordinal)
        {
            ["query"] = "target",
        };
        switch (scenario)
        {
            case "projectPath": values["projectPath"] = @"C:\foreign"; break;
            case "root": values["root"] = @"C:\foreign"; break;
            case "command": values["command"] = "foreign-tool"; break;
            case "environment": values["environment"] = new Dictionary<string, object?> { ["CODEGRAPH_DIR"] = "foreign" }; break;
            case "unknownField": values["unsafeSelector"] = "foreign"; break;
            case "nestedAuthoritySelector": values["selector"] = new Dictionary<string, object?> { ["projectPath"] = @"C:\foreign" }; break;
            case "queryMissing": values.Remove("query"); break;
            case "queryNull": values["query"] = null; break;
            case "queryNumber": values["query"] = 42; break;
            case "queryEmpty": values["query"] = string.Empty; break;
            case "queryWhitespace": values["query"] = " \t "; break;
            case "queryTooManyUtf8Bytes": values["query"] = new string('x', 513); break;
            case "queryInvalidUtf8": values["query"] = "\uD800"; break;
            case "maxFilesZero": values["maxFiles"] = 0; break;
            case "maxFilesTooLarge": values["maxFiles"] = 13; break;
            case "maxFilesText": values["maxFiles"] = "4"; break;
            case "maxFilesFraction":
                using (JsonDocument document = JsonDocument.Parse("1.5"))
                {
                    values["maxFiles"] = document.RootElement.Clone();
                }
                break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        return new AIFunctionArguments(values);
    }

    private sealed class ControlledCodeGraphSession(
        CodeGraphToolReadiness readiness,
        CodeGraphToolInvocationResult? invocationResult = null) : ICodeGraphWorkerToolSession
    {
        private readonly CodeGraphToolInvocationResult _invocationResult = invocationResult ?? new()
        {
            Status = CodeGraphToolInvocationStatus.Complete,
            RepositoryId = RepositoryId,
            Text = "controlled bounded source result",
            Locations = [new PreparedSourceLocation(RepositoryId, "src/Module.cs")],
            Excerpts =
            [
                new CodeGraphSourceExcerpt(
                    new PreparedSourceLocation(RepositoryId, "src/Module.cs"),
                    "public sealed class Module"),
            ],
        };

        public int DiscoverCallCount { get; private set; }

        public int ExploreCallCount { get; private set; }

        public int DisposeCallCount { get; private set; }

        public string? LastQuery { get; private set; }

        public int LastMaxFiles { get; private set; }

        public CancellationToken LastDiscoverCancellationToken { get; private set; }

        public CancellationToken LastExploreCancellationToken { get; private set; }

        public bool ReturnExpiredWhenDisposed { get; set; }

        public Task<CodeGraphToolReadiness> DiscoverAsync(CancellationToken cancellationToken)
        {
            DiscoverCallCount++;
            LastDiscoverCancellationToken = cancellationToken;
            return Task.FromResult(readiness);
        }

        public Task<CodeGraphToolInvocationResult> ExploreAsync(
            string query,
            int maxFiles,
            CancellationToken cancellationToken)
        {
            ExploreCallCount++;
            LastQuery = query;
            LastMaxFiles = maxFiles;
            LastExploreCancellationToken = cancellationToken;
            if (ReturnExpiredWhenDisposed && DisposeCallCount > 0)
            {
                return Task.FromResult(new CodeGraphToolInvocationResult
                {
                    Status = CodeGraphToolInvocationStatus.Expired,
                    IssueCode = CodeGraphToolIssueCode.SessionExpired,
                });
            }

            return Task.FromResult(_invocationResult);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }
    }
}
