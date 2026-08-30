using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared.Scans;
using Vulgata.Web.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.PreparedScanning;

[Collection(PreparedScanningCollection.Name)]
public sealed class PreparedScanInitiationCoordinatorTests
{
    [Theory]
    [InlineData("AdministratorRole")]
    [InlineData("SystemOwnerRole")]
    [InlineData("SystemOwnerAssignment")]
    [InlineData("FirstRepositoryMembership")]
    [InlineData("SecondRepositoryMembership")]
    public async Task FreshAuthorizationRevocation_IsMissingEquivalentBeforeStoreAsync(string revocation)
    {
        MutableAuthorizationState state = new();
        CallbackProxy runtime = CreateCallbackProxy<IPreparedScanRuntimeIdentityResolver>(
            method =>
            {
                if (method.Name == "ResolveAsync")
                {
                    state.RuntimeIdentityChecks++;
                    if (state.RuntimeIdentityChecks == 2)
                    {
                        state.Revoked = revocation;
                    }
                }
            },
            (method, args) => CreateCompletedReturn(method, success: true, args));
        CallbackProxy authorization = CreateCallbackProxy<IPreparedScanInitiationAuthorizationRechecker>(
            method => state.FreshAuthorizationChecks++,
            (method, args) => CreateCompletedReturn(method, success: false, args));
        CountingInitiationStore store = new();
        object coordinator = CreateInitiationCoordinator(runtime, authorization, store, state);

        PreparedScanPreparationResult result = await InvokeInitiateAsync(
            coordinator,
            TestSystemId,
            CreateValidRequest(),
            new TestUserContext(TestUserId));

        Assert.Equal(PreparedScanPreparationOutcome.NotFound, result.Outcome);
        Assert.Equal(0, store.CallCount);
        Assert.Equal(2, state.RuntimeIdentityChecks);
        Assert.Equal(1, state.FreshAuthorizationChecks);
        Assert.Null(result.Response);
        Assert.DoesNotContain(TestSystemId.ToString("D"), result.FailureCode ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("InvisibleSystem")]
    [InlineData("InvisibleRepository")]
    [InlineData("MalformedScope")]
    [InlineData("MismatchedPair")]
    [InlineData("MismatchedRevision")]
    public async Task InitialScopeFailure_IsMissingOrClosedWithoutStoreAsync(string scenario)
    {
        CountingInitiationStore store = new();
        MutableAuthorizationState state = new() { Revoked = scenario };
        object coordinator = CreateInitiationCoordinator(
            CreateCallbackProxy<IPreparedScanRuntimeIdentityResolver>(
                resultFactory: (method, args) => CreateCompletedReturn(
                    method,
                    success: true,
                    args,
                    runtimeRevisionOverride: scenario == "MismatchedRevision"
                        ? new string('1', 40)
                        : null)),
            CreateCallbackProxy<IPreparedScanInitiationAuthorizationRechecker>(
                resultFactory: (method, args) => CreateCompletedReturn(method, success: true, args)),
            store,
            state);
        PreparedScanRequest request = CreateValidRequest();
        if (scenario == "MalformedScope")
        {
            request.Repositories = [];
        }
        else if (scenario == "MismatchedPair")
        {
            request.Repositories = request.Repositories
                .Select(binding => new PreparedScanRepositoryRevisionBinding
                {
                    RepositoryId = Guid.NewGuid(),
                    GitRef = binding.GitRef,
                    ProfiledRevision = binding.ProfiledRevision,
                })
                .ToArray();
        }
        else if (scenario == "MismatchedRevision")
        {
            request.Repositories = request.Repositories
                .Select(binding => new PreparedScanRepositoryRevisionBinding
                {
                    RepositoryId = binding.RepositoryId,
                    GitRef = binding.GitRef,
                    ProfiledRevision = new string('f', 40),
                })
                .ToArray();
        }

        PreparedScanPreparationResult result = await InvokeInitiateAsync(
            coordinator,
            scenario == "InvisibleSystem" ? Guid.NewGuid() : TestSystemId,
            request,
            new TestUserContext(TestUserId));

        if (scenario == "MismatchedRevision")
        {
            Assert.Equal(PreparedScanPreparationOutcome.Unavailable, result.Outcome);
        }
        else
        {
            Assert.Contains(
                result.Outcome,
                new[] { PreparedScanPreparationOutcome.NotFound, PreparedScanPreparationOutcome.Conflict });
        }
        Assert.Equal(0, store.CallCount);
        Assert.Null(result.Response);
        Assert.DoesNotContain(TestRepositoryOneId.ToString("D"), result.FailureCode ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeNotReady_IsReturnedBeforeStoreAsync()
    {
        CountingInitiationStore store = new();
        object coordinator = CreateInitiationCoordinator(
            CreateCallbackProxy<IPreparedScanRuntimeIdentityResolver>(
                resultFactory: (method, args) => CreateCompletedReturn(method, success: false, args)),
            CreateCallbackProxy<IPreparedScanInitiationAuthorizationRechecker>(
                resultFactory: (method, args) => CreateCompletedReturn(method, success: true, args)),
            store,
            new MutableAuthorizationState());

        PreparedScanPreparationResult result = await InvokeInitiateAsync(
            coordinator,
            TestSystemId,
            CreateValidRequest(),
            new TestUserContext(TestUserId));

        Assert.Equal(PreparedScanPreparationOutcome.Unavailable, result.Outcome);
        Assert.Equal(0, store.CallCount);
        Assert.Null(result.Response);
    }

    [Fact]
    public void PrePublicationRevocation_RemainsASeparatePreparedCoordinatorBoundary()
    {
        MethodInfo prepare = Assert.Single(
            typeof(PreparedScanCoordinator).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "PrepareInitiatedAsync");
        Assert.Contains(
            prepare.GetParameters(),
            parameter => parameter.ParameterType == typeof(PreparedScanInitiationTuple));
        Assert.DoesNotContain(
            typeof(PreparedScanCoordinator).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "PrepareAsync" &&
                      !method.GetCustomAttributes<ObsoleteAttribute>().Any());
    }

    [Fact]
    public void ExactTuplePreparation_RequiresTupleOnlyCoordinatorAndBuilderSeams()
    {
        MethodInfo build = Assert.Single(
            typeof(IPreparedScanPairBuilder).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == nameof(IPreparedScanPairBuilder.BuildAsync));
        Assert.Collection(
            build.GetParameters(),
            parameter => Assert.Equal(typeof(PreparedScanInitiationTuple), parameter.ParameterType),
            parameter => Assert.Equal(typeof(IReadOnlyList<Vulgata.Core.Entities.Repository>), parameter.ParameterType),
            parameter => Assert.Equal(typeof(IReadOnlyList<PreparedScanRepositoryRevisionBinding>), parameter.ParameterType),
            parameter => Assert.Equal(typeof(CancellationToken), parameter.ParameterType));

        MethodInfo prepare = Assert.Single(
            typeof(PreparedScanCoordinator).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "PrepareInitiatedAsync");
        Assert.Collection(
            prepare.GetParameters(),
            parameter => Assert.Equal(typeof(Guid), parameter.ParameterType),
            parameter => Assert.Equal(typeof(PreparedScanRequest), parameter.ParameterType),
            parameter => Assert.Equal(typeof(IUserContext), parameter.ParameterType),
            parameter => Assert.Equal(typeof(PreparedScanInitiationTuple), parameter.ParameterType),
            parameter => Assert.Equal(typeof(CancellationToken), parameter.ParameterType));

        MethodInfo recover = Assert.Single(
            typeof(PreparedScanCoordinator).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "RecoverPreparedAsync");
        Assert.Collection(
            recover.GetParameters(),
            parameter => Assert.Equal(typeof(PreparedScanAuthorizationScope), parameter.ParameterType),
            parameter => Assert.Equal(typeof(PreparedScanInitiationTuple), parameter.ParameterType),
            parameter => Assert.Equal(typeof(CancellationToken), parameter.ParameterType));
    }

    [Fact]
    public async Task RecoverPreparedAsync_UsesOnlyTheExistingHandoffSourceAsync()
    {
        MethodInfo recover = Assert.Single(
            typeof(PreparedScanCoordinator).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "RecoverPreparedAsync");

        InvocationProxy handoffProxy = new();
        IPreparedScanHandoffSource handoffSource = CreateProxy<IPreparedScanHandoffSource>(
            out handoffProxy,
            (_, _) => Task.FromResult<PreparedScanHandoff?>(null));
        InvocationProxy pairBuilderProxy = null!;
        IPreparedScanPairBuilder pairBuilder = CreateProxy<IPreparedScanPairBuilder>(out pairBuilderProxy);
        PreparedScanCoordinator coordinator = new(
            CreateProxy<ISystemRepository>(out _),
            CreateProxy<IRepositoryRepository>(out _),
            pairBuilder,
            CreateProxy<IPreparedScanStore>(out _),
            handoffSource);

        Guid systemId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid firstRepositoryId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid secondRepositoryId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        PreparedScanInitiationTuple tuple = new(
            systemId,
            new string('a', 64),
            new DateTimeOffset(2026, 8, 30, 1, 2, 3, TimeSpan.Zero),
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            7,
            TwoRepositoryScanCoordinationPhase.Pass1,
            PreparedScanArtifactPresence.Present,
            [
                new(
                    firstRepositoryId,
                    "refs/tags/one",
                    new string('1', 40),
                    Guid.Parse("40000000-0000-0000-0000-000000000001"),
                    Guid.Parse("50000000-0000-0000-0000-000000000001"),
                    ScanExecutionPhase.Prepared,
                    "prompt",
                    "code-logic/file-v2",
                    "{\"runtime\":\"one\"}",
                    new string('b', 64),
                    Guid.Empty),
                new(
                    secondRepositoryId,
                    "refs/tags/two",
                    new string('2', 40),
                    Guid.Parse("40000000-0000-0000-0000-000000000002"),
                    Guid.Parse("50000000-0000-0000-0000-000000000002"),
                    ScanExecutionPhase.Prepared,
                    "prompt",
                    "code-logic/file-v2",
                    "{\"runtime\":\"two\"}",
                    new string('c', 64),
                    Guid.Empty),
            ]);

        PreparedScanAuthorizationScope authorization = new(
            Guid.Parse("60000000-0000-0000-0000-000000000001"),
            systemId,
            [firstRepositoryId, secondRepositoryId]);
        object?[] arguments = [authorization, tuple, CancellationToken.None];

        Task<PreparedScanPreparationResult> operation =
            Assert.IsType<Task<PreparedScanPreparationResult>>(recover.Invoke(coordinator, arguments));
        PreparedScanPreparationResult result = await operation;

        Assert.Equal(PreparedScanPreparationOutcome.Conflict, result.Outcome);
        Assert.Equal(1, handoffProxy.InvocationCount);
        Assert.Equal(0, pairBuilderProxy.InvocationCount);
    }

    private static T CreateProxy<T>(
        out InvocationProxy proxy,
        Func<MethodInfo, object?[], object?>? handler = null)
        where T : class
    {
        T instance = DispatchProxy.Create<T, InvocationProxy>();
        proxy = (InvocationProxy)(object)instance;
        proxy.ResultFactory = handler;
        return instance;
    }

    private static object CreateInitiationCoordinator(
        CallbackProxy runtime,
        CallbackProxy authorization,
        CountingInitiationStore store,
        MutableAuthorizationState state)
    {
        Type coordinatorType = RequireInitiationCoordinator();
        SystemEntity system = new("test", null, null, DateTimeOffset.UtcNow);
        SetPrivateId(system, TestSystemId);
        Repository first = system.AddRepository("one", "https://example.invalid/one", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("two", "https://example.invalid/two", null, null, DateTimeOffset.UtcNow);
        SetPrivateId(first, TestRepositoryOneId);
        SetPrivateId(second, TestRepositoryTwoId);

        ISystemRepository systems = CreateProxy<ISystemRepository>(
            out _,
            (method, _) => method.Name == "GetVisibleByIdAsync"
                ? Task.FromResult<SystemEntity?>(state.Revoked == "InvisibleSystem" ? null : system)
                : null);
        IRepositoryRepository repositories = CreateProxy<IRepositoryRepository>(
            out _,
            (method, args) => method.Name is "GetBySystemAndIdAsync" or "GetByIdAsync"
                ? Task.FromResult<Repository?>(ResolveRepository(args, state, first, second))
                : null);
        IScanRecoveryDispatchIdentitySource identity = CreateProxy<IScanRecoveryDispatchIdentitySource>(
            out _,
            (method, _) => method.Name == "GetAsync"
                ? Task.FromResult<ScanRecoveryDispatchIdentity?>(
                    new("prepared-scan-prompt", "code-logic/file-v2"))
                : null);
        IPreparedScanHandoffSource handoff = CreateProxy<IPreparedScanHandoffSource>(out _);
        IPreparedScanPairBuilder pairBuilder = CreateProxy<IPreparedScanPairBuilder>(out _);
        IPreparedScanStore preparedStore = CreateProxy<IPreparedScanStore>(out _);
        PreparedScanCoordinator preparedCoordinator = new(
            systems,
            repositories,
            pairBuilder,
            preparedStore,
            handoff);

        ConstructorInfo constructor = Assert.Single(
            coordinatorType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        object?[] arguments = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(ISystemRepository) ? systems :
                parameter.ParameterType == typeof(IRepositoryRepository) ? repositories :
                parameter.ParameterType == typeof(IScanRecoveryDispatchIdentitySource) ? identity :
                parameter.ParameterType == typeof(IPreparedScanInitiationStore) ? store :
                parameter.ParameterType == typeof(IPreparedScanRuntimeIdentityResolver) ? runtime :
                parameter.ParameterType == typeof(IPreparedScanInitiationAuthorizationRechecker) ? authorization :
                parameter.ParameterType == typeof(PreparedScanCoordinator) ? preparedCoordinator :
                (object?)null)
            .ToArray();
        return Activator.CreateInstance(
            coordinatorType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            args: arguments,
            culture: null)!;
    }

    private static Repository? ResolveRepository(
        object?[] args,
        MutableAuthorizationState state,
        Repository first,
        Repository second)
    {
        Guid requestedId = args
            .OfType<Guid>()
            .FirstOrDefault(id => id != TestSystemId);
        if (state.Revoked == "InvisibleRepository" && requestedId == TestRepositoryTwoId)
        {
            return null;
        }

        return requestedId == TestRepositoryOneId ? first :
            requestedId == TestRepositoryTwoId ? second : null;
    }

    private static async Task<PreparedScanPreparationResult> InvokeInitiateAsync(
        object coordinator,
        Guid systemId,
        PreparedScanRequest request,
        IUserContext user)
        => await InvokeInitiateAsync(
            coordinator,
            [systemId, request, user, CancellationToken.None]);

    private static async Task<PreparedScanPreparationResult> InvokeInitiateAsync(
        object coordinator,
        object?[] explicitArguments)
    {
        MethodInfo initiate = Assert.Single(
            coordinator.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "InitiateAsync");
        object?[] arguments = initiate.GetParameters()
            .Select(parameter => explicitArguments.FirstOrDefault(value =>
                value is not null && parameter.ParameterType.IsInstanceOfType(value)) ??
                (parameter.ParameterType == typeof(CancellationToken) ? CancellationToken.None : null))
            .ToArray();
        object? operation = initiate.Invoke(coordinator, arguments);
        return await Assert.IsType<Task<PreparedScanPreparationResult>>(operation!);
    }

    private static PreparedScanRequest CreateValidRequest() => new()
    {
        Repositories =
        [
            new()
            {
                RepositoryId = TestRepositoryOneId,
                GitRef = "refs/tags/one",
                ProfiledRevision = new string('1', 40),
            },
            new()
            {
                RepositoryId = TestRepositoryTwoId,
                GitRef = "refs/tags/two",
                ProfiledRevision = new string('2', 40),
            },
        ],
    };

    private static CallbackProxy CreateCallbackProxy<T>(
        Action<MethodInfo>? onInvocation = null,
        Func<MethodInfo, object?[], object?>? resultFactory = null)
        where T : class
    {
        T instance = DispatchProxy.Create<T, CallbackProxy>();
        CallbackProxy proxy = (CallbackProxy)(object)instance;
        proxy.OnInvocation = onInvocation;
        proxy.ResultFactory = resultFactory;
        return proxy;
    }

    private static object? CreateCompletedReturn(
        MethodInfo method,
        bool success,
        object?[] args,
        string? runtimeRevisionOverride = null)
    {
        Type returnType = method.ReturnType;
        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            Type valueType = returnType.GetGenericArguments()[0];
            object? value = CreateResultValue(valueType, method.Name, success, args, runtimeRevisionOverride);
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(valueType)
                .Invoke(null, new object?[] { value });
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            Type valueType = returnType.GetGenericArguments()[0];
            object? value = CreateResultValue(valueType, method.Name, success, args, runtimeRevisionOverride);
            return Activator.CreateInstance(returnType, value);
        }

        return CreateResultValue(returnType, method.Name, success, args, runtimeRevisionOverride);
    }

    private static object? CreateResultValue(
        Type valueType,
        string methodName,
        bool success,
        object?[] args,
        string? runtimeRevisionOverride = null)
    {
        if (valueType.Name == "PreparedScanRuntimeIdentityResolution")
        {
            Guid repositoryId = args
                .OfType<Guid>()
                .FirstOrDefault(id => id != Guid.Empty);
            AnalysisRuntimeIdentity? identity = success
                ? CreateReadyRuntimeIdentity(
                    repositoryId == Guid.Empty ? TestRepositoryOneId : repositoryId,
                    runtimeRevisionOverride ??
                    args.OfType<string>().FirstOrDefault(value => value.Length == 40) ?? new string('1', 40))
                : null;
            if (success)
            {
                ConstructorInfo? readyConstructor = valueType.GetConstructor(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    [typeof(AnalysisRuntimeIdentity)],
                    modifiers: null);
                if (readyConstructor is not null)
                {
                    return readyConstructor.Invoke([identity]);
                }

                MethodInfo? readyFactory = valueType.GetMethod(
                    "Ready",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (readyFactory is not null)
                {
                    return InvokeResultFactory(readyFactory, identity);
                }
            }
            else
            {
                MethodInfo? notReadyFactory = valueType.GetMethod(
                    "NotReady",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (notReadyFactory is not null)
                {
                    return InvokeResultFactory(notReadyFactory, null);
                }
            }

            return null;
        }

        if (valueType.Name == "PreparedScanInitiationAuthorizationResult")
        {
            return Activator.CreateInstance(
                valueType,
                success,
                Guid.Parse(TestUserId),
                success ? "Administrator" : null,
                success
                    ? new[] { TestRepositoryOneId, TestRepositoryTwoId }
                    : Array.Empty<Guid>());
        }

        if (!success && !methodName.Contains("Resolve", StringComparison.Ordinal))
        {
            foreach (string factoryName in new[] { "NotFound", "Denied", "Unauthorized", "Missing" })
            {
                MethodInfo? factory = valueType.GetMethod(
                    factoryName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (factory is not null)
                {
                    return InvokeFactory(factory);
                }
            }
        }

        if (success || methodName.Contains("Resolve", StringComparison.Ordinal))
        {
            foreach (string factoryName in new[] { "Ready", "Authorized", "Allowed", "Succeeded", "Success" })
            {
                MethodInfo? factory = valueType.GetMethod(
                    factoryName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (factory is not null)
                {
                    return InvokeFactory(factory);
                }
            }
        }

        if (valueType == typeof(string))
        {
            return success ? "Administrator" : null;
        }

        if (valueType == typeof(bool))
        {
            return success;
        }

        if (valueType.IsArray)
        {
            return Array.CreateInstance(valueType.GetElementType()!, 0);
        }

        if (valueType.IsValueType)
        {
            return Activator.CreateInstance(valueType);
        }

        return null;
    }

    private static AnalysisRuntimeIdentity CreateReadyRuntimeIdentity(Guid repositoryId, string revision) =>
        AnalysisRuntimeIdentity.Create(
            new AnalysisRuntimeOptions("codegraph", "jdtls", "java"),
            repositoryId,
            revision,
            "@colbymchenry/codegraph",
            "1.5.0",
            "https://github.com/colbymchenry/codegraph",
            "codegraph",
            "@colbymchenry/codegraph-win32-x64@1.5.0",
            [
                new("lib/dist/bin/codegraph.js", "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03"),
                new("node.exe", "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3"),
                new("npm-shim.js", "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877"),
            ],
            "jdt-language-server-1.61.0-202608141332",
            "jdt-language-server-1.61.0-202608141332.zip",
            new string('d', 64),
            21,
            "StreamJsonRpc",
            "2.25.29",
            "stdio");

    private static object? InvokeFactory(MethodInfo factory)
    {
        object?[] arguments = factory.GetParameters()
            .Select(parameter => parameter.HasDefaultValue
                ? parameter.DefaultValue
                : parameter.ParameterType.IsValueType
                    ? Activator.CreateInstance(parameter.ParameterType)
                    : null)
            .ToArray();
        try
        {
            return factory.Invoke(null, arguments);
        }
        catch (TargetInvocationException)
        {
            return null;
        }
    }

    private static object? InvokeResultFactory(
        MethodInfo factory,
        AnalysisRuntimeIdentity? identity)
    {
        object?[] arguments = factory.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(AnalysisRuntimeIdentity)
                ? identity
                : parameter.ParameterType == typeof(TimeProvider)
                    ? TimeProvider.System
                    : parameter.ParameterType == typeof(string)
                        ? parameter.Name?.Contains("component", StringComparison.OrdinalIgnoreCase) == true
                            ? "codegraph"
                            : parameter.Name?.Contains("diagnostic", StringComparison.OrdinalIgnoreCase) == true
                                ? "Runtime unavailable"
                                : "analysis_runtime_not_ready"
                        : parameter.HasDefaultValue
                            ? parameter.DefaultValue
                            : parameter.ParameterType.IsValueType
                                ? Activator.CreateInstance(parameter.ParameterType)
                                : null)
            .ToArray();
        return factory.Invoke(null, arguments);
    }

    private static void SetPrivateId(object entity, Guid id) =>
        entity.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(entity, id);

    private static Type RequireInitiationCoordinator() =>
        RequireType("Vulgata.Web.Data.PreparedScanInitiationCoordinator");

    private static Type RequireType(string fullName)
    {
        Type? type = typeof(PreparedScanCoordinator).Assembly.GetType(fullName);
        Assert.NotNull(type);
        return type!;
    }

    private const string TestUserId = "70000000-0000-0000-0000-000000000001";
    private static readonly Guid TestSystemId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TestRepositoryOneId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid TestRepositoryTwoId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    private sealed class MutableAuthorizationState
    {
        public string? Revoked { get; set; }

        public int RuntimeIdentityChecks { get; set; }

        public int FreshAuthorizationChecks { get; set; }
    }

    private sealed class TestUserContext(string userId) : IUserContext
    {
        public string UserId { get; } = userId;

        public bool IsAdministrator => true;

        public SystemOwnerFilter ToSystemOwnerFilter() => SystemOwnerFilter.AllSystems();
    }

    private sealed class CountingInitiationStore : IPreparedScanInitiationStore
    {
        public int CallCount { get; private set; }

        public Task<PreparedScanInitiationResult> CreateOrReplayAsync(
            PreparedScanInitiationCommand command,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new PreparedScanInitiationResult(PreparedScanInitiationOutcome.Unavailable));
        }
    }

    private class InvocationProxy : DispatchProxy
    {
        public int InvocationCount { get; private set; }

        public Func<MethodInfo, object?[], object?>? ResultFactory { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            InvocationCount++;
            return ResultFactory?.Invoke(targetMethod!, args ?? []) ?? null;
        }
    }

    private class CallbackProxy : DispatchProxy
    {
        public Action<MethodInfo>? OnInvocation { get; set; }

        public Func<MethodInfo, object?[], object?>? ResultFactory { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            OnInvocation?.Invoke(targetMethod!);
            return ResultFactory?.Invoke(targetMethod!, args ?? []);
        }
    }
}
