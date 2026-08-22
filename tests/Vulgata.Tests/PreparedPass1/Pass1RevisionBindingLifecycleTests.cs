using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T062 RED contract slice for pair-owned exact-revision bindings.
///
/// The existing Story 12.2 acquirer is repository-scoped and disposes its
/// leases with the preparation operation. Pass 1 needs a public pair-owned
/// lifecycle so every in-flight runner, revision-bound source read,
/// CodeGraph/JDT-LS operation, and reconciliation action uses the same exact
/// revision binding. Reflection keeps this test executable RED before the
/// Core contract and production registry exist; it is not a source-text test.
/// </summary>
public sealed class Pass1RevisionBindingLifecycleTests
{
    [Fact]
    public void CoreContract_RequiresAtomicPairAcquisitionAndExplicitPairRelease()
    {
        Type contract = RequireCoreContract();
        MethodInfo acquire = RequirePairAcquire(contract);
        MethodInfo release = RequirePairRelease(contract);

        Assert.Contains(
            acquire.GetParameters(),
            parameter => parameter.ParameterType == typeof(Guid));
        Assert.Contains(
            acquire.GetParameters(),
            parameter => IsRevisionRequestCollection(parameter.ParameterType));
        Assert.Contains(
            release.GetParameters(),
            parameter => parameter.ParameterType == typeof(Guid));
        Assert.DoesNotContain(
            release.GetParameters(),
            parameter => IsRevisionRequestCollection(parameter.ParameterType));

        Type acquisition = UnwrapAsync(acquire.ReturnType);
        Assert.True(
            acquisition != typeof(void),
            "T062 RED: pair acquisition must return a binding/lease projection.");
    }

    [Fact]
    public void ProductionRegistry_ImplementsTheCorePairBindingContract()
    {
        Type contract = RequireCoreContract();
        Type registry = RequireRegistryType(contract);
        Assert.True(registry!.IsPublic || registry.IsNestedPublic);
    }

    [Fact]
    public async Task AcquirePair_RequiresExactlyTwoAndRollsBackBothOnSecondFailureAsync()
    {
        Type contract = RequireCoreContract();
        FakeRevisionAcquirer acquirer = new(failingRepositoryId: RepositoryTwo);
        using ServiceProvider provider = CreateRegistry(contract, acquirer, out object registry);
        MethodInfo acquire = RequirePairAcquire(contract);
        MethodInfo resolve = RequireResolve(contract);

        IReadOnlyList<PreparedRevisionRequest> oneRequest =
            [CreateRequest(RepositoryOne)];
        await InvokeAsync(registry, acquire, PairId, oneRequest);
        Assert.Null(
            await InvokeAsync(registry, resolve, PairId, RepositoryOne));
        Assert.Empty(acquirer.SuccessfulRepositoryIds);

        IReadOnlyList<PreparedRevisionRequest> pair =
            [CreateRequest(RepositoryOne), CreateRequest(RepositoryTwo)];
        await InvokeAsync(registry, acquire, PairId, pair);

        Assert.Equal(2, acquirer.CallCount);
        Assert.True(
            acquirer.DisposeCount >= 1,
            "T062 RED: a first acquired revision lease/scope was not disposed " +
            "when the second Repository acquisition failed.");
        Assert.Null(
            await InvokeAsync(registry, resolve, PairId, RepositoryOne));
        Assert.Null(
            await InvokeAsync(registry, resolve, PairId, RepositoryTwo));
    }

    [Fact]
    public async Task AcquirePair_ResolvesStableTokensReleasesPairAndReacquiresExactRevisionAsync()
    {
        Type contract = RequireCoreContract();
        FakeRevisionAcquirer acquirer = new();
        using ServiceProvider provider = CreateRegistry(contract, acquirer, out object registry);
        MethodInfo acquire = RequirePairAcquire(contract);
        MethodInfo resolve = RequireResolve(contract);
        MethodInfo release = RequirePairRelease(contract);
        IReadOnlyList<PreparedRevisionRequest> pair =
            [CreateRequest(RepositoryOne), CreateRequest(RepositoryTwo)];

        await InvokeAsync(registry, acquire, PairId, pair);
        string firstToken = RequireOpaqueToken(
            await InvokeAsync(registry, resolve, PairId, RepositoryOne));
        string secondToken = RequireOpaqueToken(
            await InvokeAsync(registry, resolve, PairId, RepositoryTwo));
        Assert.NotEqual(firstToken, secondToken);
        Assert.Equal(
            firstToken,
            RequireOpaqueToken(await InvokeAsync(registry, resolve, PairId, RepositoryOne)));

        // The release is intentionally after the pair-wide claim stop and
        // local runner/navigation/reconciliation drain. It models the safe
        // ProbingProvider boundary; a successful probe must reacquire before
        // any new claim is allowed.
        await InvokeAsync(registry, release, PairId);
        Assert.True(
            acquirer.DisposeCount >= 1,
            "T062 RED: pair-wide release did not dispose the acquired revision scope.");
        Assert.Null(await InvokeAsync(registry, resolve, PairId, RepositoryOne));
        Assert.Null(await InvokeAsync(registry, resolve, PairId, RepositoryTwo));

        await InvokeAsync(registry, acquire, PairId, pair);
        string reacquiredToken = RequireOpaqueToken(
            await InvokeAsync(registry, resolve, PairId, RepositoryOne));
        Assert.Equal(firstToken, reacquiredToken);
        Assert.Equal(4, acquirer.CallCount);
        Assert.Equal(
            pair.Select(request => request.ProfiledRevision).Order(StringComparer.Ordinal),
            acquirer.SuccessfulRevisions.Take(2).Order(StringComparer.Ordinal));
        Assert.Equal(
            pair.Select(request => request.ProfiledRevision).Order(StringComparer.Ordinal),
            acquirer.SuccessfulRevisions.Skip(2).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void BindingProjection_RequiresStablePerRepositoryOpaqueTokensWithoutHostPaths()
    {
        Type contract = RequireCoreContract();
        MethodInfo acquire = RequirePairAcquire(contract);
        Type acquisition = UnwrapAsync(acquire.ReturnType);
        Type binding = FindBindingProjection(acquisition);

        Assert.True(
            binding.GetProperty("PairExecutionId") is not null,
            "T062 RED: binding projection must remain owned by one pair execution.");
        Assert.True(
            binding.GetProperty("RepositoryId") is not null,
            "T062 RED: each repository binding must be addressable without a path.");
        Assert.True(
            binding.GetProperty("ProfiledRevision") is not null,
            "T062 RED: each binding must carry the exact Profiled Revision.");

        PropertyInfo? token = binding.GetProperties().FirstOrDefault(property =>
            property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            token is not null,
            "T062 RED: source, CodeGraph, and JDT-LS resolvers need one stable " +
            "opaque per-pair/per-repository binding token.");
        Assert.False(
            binding.GetProperties().Any(property => IsHostPath(property.Name)),
            "T062 RED: host paths must not cross the Core binding projection.");
    }

    [Fact]
    public void LifecycleSurface_ProvidesReacquisitionAfterReleaseAndPreservesActiveRunnerBoundary()
    {
        Type contract = RequireCoreContract();
        MethodInfo acquire = RequirePairAcquire(contract);
        MethodInfo release = RequirePairRelease(contract);

        // A second exact AcquirePair call with the same persisted pair and two
        // requests is the restart/manual-resume path. Release is pair-wide and
        // is legal only after local drain; it must not be a repository-only
        // operation that can strand one of the two bindings.
        Assert.Contains("Pair", acquire.Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pair", release.Name, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            acquire.GetParameters().Count(parameter =>
                parameter.ParameterType == typeof(Guid)) >= 1,
            "T062 RED: reacquisition must be anchored to the persisted pair id.");
        Assert.Single(
            release.GetParameters().Where(parameter =>
                parameter.ParameterType == typeof(Guid)));

        // The provider-facing packet is intentionally separate from the
        // binding projection. It may carry server-only authorization outside
        // the provider payload, but must not expose a host path or binding
        // token as a model-visible field.
        PropertyInfo[] packetProperties = typeof(PreparedPass1WorkerPacket)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Assert.DoesNotContain(
            packetProperties,
            property => IsHostPath(property.Name));
        Assert.DoesNotContain(
            packetProperties,
            property => property.Name.Contains("BindingToken", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Store_ExposesAtomicRevisionBindingFailureTransitionWithClosedEffects()
    {
        MethodInfo method = RequireRevisionBindingFailureMethod();
        ParameterInfo[] parameters = method.GetParameters();

        Assert.Contains(
            parameters,
            parameter => parameter.ParameterType == typeof(Guid) &&
                parameter.Name is "pairExecutionId" or "pairId");
        Assert.Contains(
            parameters,
            parameter => parameter.ParameterType == typeof(PreparedScanAuthorizationScope));
        Assert.Contains(
            parameters,
            parameter => parameter.ParameterType == typeof(long) &&
                parameter.Name is "expectedStateVersion" or "pairStateVersion");
        Assert.Contains(
            parameters,
            parameter => parameter.ParameterType == typeof(DateTimeOffset) &&
                parameter.Name is "observedAt" or "failedAt");
        Assert.Contains(
            parameters,
            parameter => parameter.ParameterType == typeof(CancellationToken));
        Assert.DoesNotContain(
            parameters,
            parameter => parameter.ParameterType == typeof(string) &&
                parameter.Name is "reasonCode" or "diagnostic");

        Type result = UnwrapAsync(method.ReturnType);
        PropertyInfo[] resultProperties = result.GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        Assert.Contains(resultProperties, property => property.Name == "PairState");
        Assert.Contains(resultProperties, property => property.Name == "FailureCode");
        PropertyInfo pairStateProperty = resultProperties.Single(
            property => property.Name == "PairState");
        Assert.Equal(
            typeof(Pass1PairExecutionPhase),
            Nullable.GetUnderlyingType(pairStateProperty.PropertyType) ??
                pairStateProperty.PropertyType);

        PropertyInfo? outcomeProperty = resultProperties
            .SingleOrDefault(property => property.Name == "Outcome");
        Assert.NotNull(outcomeProperty);
        Type outcome = Nullable.GetUnderlyingType(outcomeProperty!.PropertyType) ??
            outcomeProperty.PropertyType;
        Assert.True(
            Enum.GetNames(outcome).Any(name =>
                name.Contains("Replay", StringComparison.OrdinalIgnoreCase)) &&
            Enum.GetNames(outcome).Any(name =>
                name.Contains("Conflict", StringComparison.OrdinalIgnoreCase)),
            "T062 RED: revision-binding failure must support exact replay and " +
            "reject stale/unauthorized/mismatched authority without mutation.");
    }

    private static Type RequireCoreContract()
    {
        Type[] contracts = typeof(PreparedRevisionRequest).Assembly
            .GetTypes()
            .Where(type => type is { IsInterface: true } &&
                type.Name.Contains("Revision", StringComparison.OrdinalIgnoreCase) &&
                type.Name.Contains("Binding", StringComparison.OrdinalIgnoreCase) &&
                type.GetMethods().Any(method => IsAcquire(method)) &&
                type.GetMethods().Any(method => IsRelease(method)))
            .ToArray();

        Assert.True(
            contracts.Length > 0,
            "T062 RED: Core has no public pair-owned exact-revision binding " +
            "contract with atomic acquisition and pair release.");
        Assert.True(
            contracts.Length == 1,
            $"T062 RED: Core exposes {contracts.Length} ambiguous revision-binding contracts.");
        return contracts[0];
    }

    private static Type RequireRegistryType(Type contract)
    {
        Type? registry = VulgataAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } &&
                contract.IsAssignableFrom(type))
            .SingleOrDefault();

        Assert.True(
            registry is not null,
            "T062 RED: no production pair-owned exact-revision binding registry " +
            "implements the Core contract; DI must not rely on per-repository " +
            "pre-scan disposal or an unbound source reader.");
        return registry!;
    }

    private static MethodInfo RequirePairAcquire(Type contract)
    {
        MethodInfo? method = contract.GetMethods()
            .SingleOrDefault(candidate =>
                IsAcquire(candidate) &&
                candidate.Name.Contains("Pair", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            method is not null,
            "T062 RED: Core binding contract must expose one pair-scoped " +
            "AcquirePair operation over exactly two persisted revision requests.");
        return method!;
    }

    private static MethodInfo RequirePairRelease(Type contract)
    {
        MethodInfo? method = contract.GetMethods()
            .SingleOrDefault(candidate =>
                IsRelease(candidate) &&
                candidate.Name.Contains("Pair", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            method is not null,
            "T062 RED: Core binding contract must expose one pair-scoped release " +
            "operation for terminal, paused, exception, and disposal cleanup.");
        return method!;
    }

    private static MethodInfo RequireResolve(Type contract)
    {
        MethodInfo? method = contract.GetMethods()
            .SingleOrDefault(candidate =>
                (candidate.Name.Contains("Resolve", StringComparison.OrdinalIgnoreCase) ||
                    candidate.Name.Contains("Get", StringComparison.OrdinalIgnoreCase)) &&
                candidate.Name.Contains("Binding", StringComparison.OrdinalIgnoreCase) &&
                candidate.GetParameters().Count(parameter =>
                    parameter.ParameterType == typeof(Guid)) >= 2);
        Assert.True(
            method is not null,
            "T062 RED: Core binding contract must resolve a stable opaque token " +
            "by persisted pair and Repository identity for source/CodeGraph/JDT-LS.");
        return method!;
    }

    private static MethodInfo RequireRevisionBindingFailureMethod()
    {
        MethodInfo? method = typeof(IPreparedPass1Store).GetMethods()
            .SingleOrDefault(candidate =>
                candidate.Name.Contains("Fail", StringComparison.OrdinalIgnoreCase) &&
                candidate.Name.Contains("Revision", StringComparison.OrdinalIgnoreCase) &&
                candidate.Name.Contains("Binding", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            method is not null,
            "T062 RED: IPreparedPass1Store must expose an atomic " +
            "FailRevisionBindingAsync pair transition before rejected exact " +
            "two-Repository acquisition can be closed safely.");
        Assert.True(
            method!.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
            "T062 RED: revision-binding failure must return a typed durable result.");
        return method;
    }

    private static Type FindBindingProjection(Type acquisition)
    {
        Type? binding = acquisition.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.Name is "Bindings" or "Repositories")
            .Select(property => property.PropertyType)
            .Select(GetElementType)
            .FirstOrDefault(type => type is not null &&
                type.Name.Contains("Binding", StringComparison.OrdinalIgnoreCase));

        Assert.True(
            binding is not null,
            "T062 RED: pair acquisition must return two repository binding " +
            "projections rather than independent disposable snapshots.");
        return binding!;
    }

    private static bool IsAcquire(MethodInfo method) =>
        method.Name.Contains("Acquire", StringComparison.OrdinalIgnoreCase);

    private static bool IsRelease(MethodInfo method) =>
        method.Name.Contains("Release", StringComparison.OrdinalIgnoreCase);

    private static bool IsRevisionRequestCollection(Type type) =>
        GetElementType(type) == typeof(PreparedRevisionRequest);

    private static Type? GetElementType(Type type) =>
        type.IsArray
            ? type.GetElementType()
            : type.IsGenericType && type.GetGenericArguments().Length == 1
                ? type.GetGenericArguments()[0]
                : null;

    private static Type UnwrapAsync(Type type) =>
        type.IsGenericType &&
        (type.GetGenericTypeDefinition() == typeof(Task<>) ||
            type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            ? type.GetGenericArguments()[0]
            : type;

    private static bool HasProperty(Type type, string name) =>
        type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public) is not null;

    private static IEnumerable<Assembly> VulgataAssemblies()
    {
        string[] names =
        [
            "Vulgata.Core",
            "Vulgata.Infrastructure",
            "Vulgata.Agents",
            "Vulgata.Web",
        ];
        return names
            .Select(name =>
            {
                try
                {
                    return Assembly.Load(name);
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
            })
            .Where(assembly => assembly is not null)!;
    }

    private static bool IsHostPath(string name) =>
        name.Contains("RootPath", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("HostPath", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("AbsolutePath", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("WorkingCopyPath", StringComparison.OrdinalIgnoreCase);

    private static async Task<object?> InvokeAsync(
        object target,
        MethodInfo method,
        Guid pairId,
        object? requestOrRepository = null)
    {
        object?[] arguments = method.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(Guid)
                ? (object?)((requestOrRepository is Guid repositoryId &&
                        method.GetParameters().Count(candidate =>
                            candidate.ParameterType == typeof(Guid)) > 1 &&
                        parameter.Position > 0)
                    ? repositoryId
                    : pairId)
                : IsRevisionRequestCollection(parameter.ParameterType)
                    ? requestOrRepository
                    : parameter.ParameterType == typeof(CancellationToken)
                        ? CancellationToken.None
                        : null)
            .ToArray();
        object? result = method.Invoke(target, arguments);
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        return result;
    }

    private static ServiceProvider CreateRegistry(
        Type contract,
        FakeRevisionAcquirer acquirer,
        out object registry)
    {
        Type registryType = RequireRegistryType(contract);
        ConstructorInfo? constructor = registryType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(candidate =>
                candidate.GetParameters().Any(parameter =>
                    parameter.ParameterType == typeof(IServiceScopeFactory)));
        Assert.True(
            constructor is not null,
            "T062 RED: production registry must resolve the scoped exact-revision " +
            "acquirer through IServiceScopeFactory; tests must not replace scanning services.");

        ServiceCollection services = new();
        services.AddScoped<IPreparedRevisionAcquirer>(_ => acquirer);
        services.AddScoped<IRevisionBoundSourceReader, FakeRevisionBoundSourceReader>();
        ServiceProvider provider = services.BuildServiceProvider();
        try
        {
            registry = ActivatorUtilities.CreateInstance(provider, registryType);
            return provider;
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    private static string RequireOpaqueToken(object? value)
    {
        Assert.True(value is not null, "T062 RED: released or missing pair binding resolved evidence.");
        if (value is string token)
        {
            Assert.False(string.IsNullOrWhiteSpace(token));
            Assert.DoesNotContain("\\", token, StringComparison.Ordinal);
            Assert.DoesNotContain("/", token, StringComparison.Ordinal);
            return token;
        }

        PropertyInfo? tokenProperty = value!.GetType().GetProperties()
            .FirstOrDefault(property =>
                property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.True(tokenProperty is not null, "T062 RED: binding did not return an opaque token.");
        string? propertyValue = tokenProperty!.GetValue(value)?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(propertyValue));
        return propertyValue!;
    }

    private static PreparedRevisionRequest CreateRequest(Guid repositoryId) =>
        new(
            repositoryId,
            Guid.NewGuid(),
            "https://example.invalid/repository.git",
            "refs/heads/main",
            repositoryId == RepositoryOne
                ? "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                : "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

    private static readonly Guid PairId =
        Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid RepositoryOne =
        Guid.Parse("20000000-0000-4000-8000-000000000001");
    private static readonly Guid RepositoryTwo =
        Guid.Parse("20000000-0000-4000-8000-000000000002");

    private sealed class FakeRevisionAcquirer(Guid? failingRepositoryId = null)
        : IPreparedRevisionAcquirer, IDisposable
    {
        public int CallCount { get; private set; }
        public List<Guid> SuccessfulRepositoryIds { get; } = [];
        public List<string> SuccessfulRevisions { get; } = [];
        public int DisposeCount { get; private set; }

        public Task<PreparedRevisionAcquisitionResult> AcquireAsync(
            PreparedRevisionRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (request.RepositoryId == failingRepositoryId)
            {
                return Task.FromResult(new PreparedRevisionAcquisitionResult(
                    null,
                    new PreparedScanFailure(
                        PreparedScanFailureReason.SourceIdentityMismatch,
                        "fake-revision-failure",
                        "controlled test failure")));
            }

            SuccessfulRepositoryIds.Add(request.RepositoryId);
            SuccessfulRevisions.Add(request.ProfiledRevision);
            string token = $"opaque-{request.RepositoryId:N}-{CallCount}";
            return Task.FromResult(new PreparedRevisionAcquisitionResult(
                new PreparedRevisionSnapshot(
                    request.RepositoryId,
                    request.ScanRunId,
                    request.RequestedRef,
                    request.ProfiledRevision,
                    $"snapshot-{request.RepositoryId:N}",
                    token,
                    token),
                null));
        }

        public void Dispose() => DisposeCount++;
    }

    private sealed class FakeRevisionBoundSourceReader : IRevisionBoundSourceReader
    {
        public Task<SourceExcerptResult> ReadAsync(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken)
        {
            const string excerpt = "class BindingFixture {}";
            return Task.FromResult(new SourceExcerptResult(
                AnalysisOperationStatus.Succeeded,
                scope.RepositoryId,
                scope.ProfiledRevision,
                request.RelativePath,
                request.Range,
                request.ExpectedSourceSha256,
                excerpt,
                excerpt.Length,
                null));
        }
    }
}
