using System.Collections;
using System.Reflection;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T034 RED boundary for the pair-wide provider recovery circuit.
///
/// The tests use only the public health-probe/store seams.  They intentionally
/// fail while T035's provider circuit has not been added; no test double is
/// allowed to make the missing durable behavior appear green.
/// </summary>
public sealed class Pass1ProviderCircuitTests
{
    private const string ProbeTypeName =
        "Vulgata.Agents.WorkerExecution.WorkerAgentProviderHealthProbe";

    [Fact]
    public void ProviderHealthProbe_UsesExactlyFiveIncreasingRecoveryWindows()
    {
        Type probe = RequireProbeType();
        MethodInfo delay = RequirePublicStaticMethod(
            probe,
            "GetProbeDelay",
            parameterCount: 1);

        Assert.Equal(typeof(TimeSpan), delay.ReturnType);
        Assert.Equal(typeof(int), delay.GetParameters()[0].ParameterType);

        TimeSpan[] expected =
        [
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(5),
        ];

        for (int ordinal = 1; ordinal <= expected.Length; ordinal++)
        {
            object? value = delay.Invoke(null, [ordinal]);
            Assert.Equal(expected[ordinal - 1], Assert.IsType<TimeSpan>(value));
        }

        Assert.Throws<TargetInvocationException>(() => delay.Invoke(null, [0]));
        Assert.Throws<TargetInvocationException>(() => delay.Invoke(null, [6]));
    }

    [Fact]
    public void ProviderHealthProbe_JitterIsDeterministicNonnegativeAndBelowTenPercent()
    {
        Type probe = RequireProbeType();
        MethodInfo jitter = RequirePublicStaticMethod(
            probe,
            "ComputeProbeJitter",
            parameterCount: 2);
        ParameterInfo[] parameters = jitter.GetParameters();

        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal(typeof(int), parameters[1].ParameterType);
        Assert.Equal(typeof(TimeSpan), jitter.ReturnType);

        Guid pairId = Guid.Parse("11111111-2222-4333-8444-555555555555");
        for (int ordinal = 1; ordinal <= 5; ordinal++)
        {
            TimeSpan first = Assert.IsType<TimeSpan>(
                jitter.Invoke(null, [pairId, ordinal]));
            TimeSpan second = Assert.IsType<TimeSpan>(
                jitter.Invoke(null, [pairId, ordinal]));

            Assert.Equal(first, second);
            Assert.True(first >= TimeSpan.Zero);
            Assert.True(
                first < GetNominalDelay(probe, ordinal) / 10,
                $"Probe {ordinal} jitter must be strictly below 10% of its nominal delay.");
        }
    }

    [Fact]
    public void ProviderHealthProbe_BoundsRetryAfterWithoutAddingAProbeOrAnUnboundedWait()
    {
        Type probe = RequireProbeType();
        MethodInfo eligibility = RequirePublicStaticMethod(
            probe,
            "GetProbeEligibility",
            parameterCount: 4);
        ParameterInfo[] parameters = eligibility.GetParameters();

        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal(typeof(int), parameters[1].ParameterType);
        Assert.Equal(typeof(DateTimeOffset), parameters[2].ParameterType);
        Assert.Equal(typeof(DateTimeOffset?), parameters[3].ParameterType);
        Assert.Equal(typeof(DateTimeOffset), eligibility.ReturnType);

        Guid pairId = Guid.Parse("11111111-2222-4333-8444-555555555555");
        DateTimeOffset observedAt = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset nominal = Assert.IsType<DateTimeOffset>(
            eligibility.Invoke(null, [pairId, 1, observedAt, null]));
        DateTimeOffset bounded = Assert.IsType<DateTimeOffset>(
            eligibility.Invoke(
                null,
                [pairId, 1, observedAt, observedAt.AddHours(2)]));

        Assert.True(nominal >= observedAt.AddSeconds(10));
        Assert.True(bounded <= observedAt.AddMinutes(10));
        Assert.True(bounded >= nominal);
    }

    [Fact]
    public void PreparedPass1Store_ProviderTransientAndProbeSeamsCarryPairWideDurableCircuitState()
    {
        Type store = typeof(IPreparedPass1Store);
        MethodInfo transient = RequirePublicInstanceMethod(
            store,
            "RecordProviderTransientAsync",
            "OpenProviderCircuitAsync",
            "RecordProviderFailureAsync");
        MethodInfo probe = RequirePublicInstanceMethod(
            store,
            "ProbeProviderAsync",
            "RunProviderProbeAsync",
            "AttemptProviderProbeAsync");

        Assert.Contains(transient.GetParameters(), parameter => parameter.ParameterType == typeof(Guid));
        Assert.Contains(transient.GetParameters(), parameter => parameter.ParameterType == typeof(DateTimeOffset));
        Assert.Contains(transient.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken));
        Assert.Contains(probe.GetParameters(), parameter => parameter.ParameterType == typeof(Guid));
        Assert.Contains(probe.GetParameters(), parameter => parameter.ParameterType == typeof(DateTimeOffset));
        Assert.Contains(probe.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken));

        AssertCircuitResult(transient.ReturnType);
        AssertCircuitResult(probe.ReturnType);
    }

    [Fact]
    public void ProviderCircuit_StopsBothRepositoryClaimsAndPausesAfterTheFifthFailedProbe()
    {
        Type result = RequireCircuitResultType(typeof(IPreparedPass1Store));
        PropertyInfo outcome = RequireProperty(result, "Outcome", "ProviderOutcome");
        PropertyInfo circuit = RequireProperty(result, "CircuitState");
        PropertyInfo phase = RequireProperty(result, "Phase");
        PropertyInfo ordinal = RequireProperty(result, "ProbeOrdinal");
        PropertyInfo reason = RequireProperty(result, "ReasonCode");
        PropertyInfo manualAction = RequireProperty(
            result,
            "ManualResumeAction",
            "ManualAction");

        Assert.True(outcome.PropertyType.IsEnum);
        Assert.True(circuit.PropertyType.IsEnum);
        Assert.True(phase.PropertyType.IsEnum);
        Assert.Equal(typeof(int), ordinal.PropertyType);
        Assert.Equal(typeof(string), reason.PropertyType);
        Assert.Equal(typeof(string), manualAction.PropertyType);

        Assert.Contains(
            Enum.GetNames(circuit.PropertyType),
            name => string.Equals(name, nameof(Pass1CircuitState.Probing), StringComparison.Ordinal));
        Assert.Contains(
            Enum.GetNames(circuit.PropertyType),
            name => string.Equals(name, nameof(Pass1CircuitState.Paused), StringComparison.Ordinal));
        Assert.Contains(
            Enum.GetNames(phase.PropertyType),
            name => string.Equals(
                name,
                nameof(Pass1PairExecutionPhase.PausedProviderUnavailable),
                StringComparison.Ordinal));
    }

    [Fact]
    public void ProviderCircuit_RecoveryStatusPreservesCompletedDocumentsAndAllowsOnlyRemainingClaims()
    {
        Type result = RequireCircuitResultType(typeof(IPreparedPass1Store));
        PropertyInfo completed = RequireProperty(
            result,
            "CompletedDocumentCount",
            "CompletedCount",
            "PreservedCompletedDocumentCount");
        PropertyInfo documentIds = RequireProperty(
            result,
            "CompletedDocumentVersionIds",
            "PreservedDocumentVersionIds",
            "CommittedDocumentVersionIds");
        PropertyInfo claimGate = RequireProperty(
            result,
            "AllowsNewClaims",
            "CanClaim",
            "ClaimsAllowed");

        Assert.Equal(typeof(int), completed.PropertyType);
        Assert.True(
            typeof(IEnumerable).IsAssignableFrom(documentIds.PropertyType),
            "Provider recovery must expose only bounded completed-document identity, not document content.");
        Assert.Equal(typeof(bool), claimGate.PropertyType);
        Assert.DoesNotContain(
            result.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("Source", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Credential", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Raw", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProviderCircuit_UsesTheVersionedActiveHealthProbeContractRatherThanConfigurationResolution()
    {
        Assembly core = Assembly.Load("Vulgata.Core");
        Type contract = core.GetType(
            "Vulgata.Core.DomainServices.IPreparedPass1ProviderHealthProbe",
            throwOnError: false,
            ignoreCase: false)
            ?? throw new InvalidOperationException(
                "T056 RED: the versioned active provider health probe contract is missing.");

        Assert.True(contract.IsInterface);
        MethodInfo[] methods = contract.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(
            methods,
            method => method.ReturnType.IsGenericType &&
                method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
        Assert.DoesNotContain(
            methods,
            method => method.Name.Contains("Resolve", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Configured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProviderCircuit_ExposesNoProbeTicketBeyondTheFifthDurableWindow()
    {
        Type probe = RequireProbeType();
        MethodInfo delay = RequirePublicStaticMethod(probe, "GetProbeDelay", parameterCount: 1);
        for (int ordinal = 1; ordinal <= 5; ordinal++)
        {
            Assert.IsType<TimeSpan>(delay.Invoke(null, [ordinal]));
        }

        Type healthProbe = Assembly.Load("Vulgata.Agents").GetType(
            "Vulgata.Agents.WorkerExecution.PreparedPass1ProviderHealthProbe",
            throwOnError: false,
            ignoreCase: false)
            ?? throw new InvalidOperationException(
                "T056 RED: the active provider probe must own the fifth-failure/no-sixth boundary.");
        Assert.DoesNotContain(
            healthProbe.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name.Contains("Sixth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProviderProbeDriver_ConsumesFiveDurableWindowsWithoutClaimsAndPersistsTheFifthPause()
    {
        Type driver = RequireType(
            "Vulgata.Web",
            "Vulgata.Web.Data.PreparedPass1ProbeDriver");
        Type healthContract = RequireType(
            "Vulgata.Core",
            "Vulgata.Core.DomainServices.IPreparedPass1ProviderHealthProbe");

        StatefulProxy.Reset();
        object store = DispatchProxy.Create(typeof(IPreparedPass1Store), typeof(StatefulProxy));
        object health = DispatchProxy.Create(healthContract, typeof(StatefulProxy));
        ManualTimeProvider time = new(new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero));
        object instance = CreateDriver(driver, store, health, time);
        MethodInfo operation = driver.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.ReturnType == typeof(Task) ||
                (method.ReturnType.IsGenericType &&
                    method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
            .SingleOrDefault(method => method.Name.Contains("Probe", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Run", StringComparison.OrdinalIgnoreCase))
            ?? throw new Xunit.Sdk.XunitException(
                "T056 RED: the provider probe driver must expose one invokable async operation.");

        DateTimeOffset previousFailure = time.GetUtcNow();
        for (int ordinal = 1; ordinal <= 5; ordinal++)
        {
            TimeSpan nominal = WorkerAgentProviderHealthProbe.GetProbeDelay(ordinal);
            DateTimeOffset eligible = previousFailure.Add(nominal);
            time.SetUtcNow(eligible);
            InvokeDriver(operation, instance, store, health, time, ordinal);
            Assert.Equal(eligible, StatefulProxy.ProbeTimes[^1]);
            previousFailure = eligible;
        }

        Assert.Equal(5, StatefulProxy.ProbeTimes.Count);
        Assert.Equal(
            [1, 2, 3, 4, 5],
            StatefulProxy.ProbeOrdinals);
        Assert.Empty(StatefulProxy.ClaimCalls);
        Assert.Equal(5, StatefulProxy.VersionedTransitions);
        Assert.Equal(Pass1PairExecutionPhase.PausedProviderUnavailable, StatefulProxy.LastPhase);
        Assert.Equal("继续扫描", StatefulProxy.LastManualResumeAction);

        time.SetUtcNow(time.GetUtcNow().AddHours(2));
        InvokeDriver(operation, instance, store, health, time, 6);
        Assert.Equal(5, StatefulProxy.ProbeTimes.Count);
        Assert.Empty(StatefulProxy.ClaimCalls);
    }

    private static Type RequireType(string assemblyName, string fullName) =>
        Assembly.Load(assemblyName).GetType(fullName, throwOnError: false, ignoreCase: false)
        ?? throw new InvalidOperationException($"T056 RED: missing production seam {fullName}.");

    private static object CreateDriver(
        Type implementation,
        object store,
        object health,
        TimeProvider time)
    {
        foreach (ConstructorInfo constructor in implementation
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(value => value.GetParameters().Length))
        {
            try
            {
                return constructor.Invoke(constructor.GetParameters()
                    .Select(parameter => ResolveDriverValue(parameter.ParameterType, parameter.Name, store, health, time))
                    .ToArray());
            }
            catch (TargetInvocationException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        throw new Xunit.Sdk.XunitException(
            "T056 RED: provider probe driver cannot be composed with stateful public seams and fake time.");
    }

    private static void InvokeDriver(
        MethodInfo operation,
        object instance,
        object store,
        object health,
        ManualTimeProvider time,
        int ordinal)
    {
        object invocation = operation.Invoke(instance, operation.GetParameters()
            .Select(parameter => ResolveDriverValue(
                parameter.ParameterType,
                parameter.Name,
                store,
                health,
                time,
                ordinal))
            .ToArray())
            ?? throw new Xunit.Sdk.XunitException("T056 RED: driver returned no async operation.");
        if (invocation is not Task task)
        {
            throw new Xunit.Sdk.XunitException("T056 RED: driver operation must return Task or Task<T>.");
        }

        task.GetAwaiter().GetResult();
    }

    private static object? ResolveDriverValue(
        Type type,
        string? name,
        object store,
        object health,
        TimeProvider time,
        int ordinal = 1)
    {
        if (type == typeof(TimeProvider))
        {
            return time;
        }

        if (type == typeof(CancellationToken))
        {
            return CancellationToken.None;
        }

        if (type == typeof(Guid))
        {
            return Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        }

        if (type == typeof(DateTimeOffset))
        {
            return time.GetUtcNow();
        }

        if (type == typeof(Uri))
        {
            return new Uri("https://provider.invalid/v1");
        }

        if (type == typeof(int))
        {
            return name?.Contains("ordinal", StringComparison.OrdinalIgnoreCase) == true ? ordinal : 0;
        }

        if (type == typeof(long))
        {
            return 1L;
        }

        if (type == typeof(bool))
        {
            return false;
        }

        if (type == typeof(string))
        {
            return "health-test-secret";
        }

        if (type.IsInterface)
        {
            if (type.IsInstanceOfType(health))
            {
                return health;
            }

            if (type.IsInstanceOfType(store))
            {
                return store;
            }

            return DispatchProxy.Create(type, typeof(StatefulProxy));
        }

        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        ConstructorInfo? constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(value => value.GetParameters().Length)
            .FirstOrDefault();
        return constructor?.Invoke(constructor.GetParameters()
            .Select(parameter => ResolveDriverValue(
                parameter.ParameterType,
                parameter.Name,
                store,
                health,
                time,
                ordinal))
            .ToArray());
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void SetUtcNow(DateTimeOffset value) => _now = value;
    }

    private class StatefulProxy : DispatchProxy
    {
        public static List<DateTimeOffset> ProbeTimes { get; } = [];

        public static List<int> ProbeOrdinals { get; } = [];

        public static List<string> ClaimCalls { get; } = [];

        public static int VersionedTransitions { get; private set; }

        public static Pass1PairExecutionPhase LastPhase { get; private set; }

        public static string? LastManualResumeAction { get; private set; }

        public static void Reset()
        {
            ProbeTimes.Clear();
            ProbeOrdinals.Clear();
            ClaimCalls.Clear();
            VersionedTransitions = 0;
            LastPhase = default;
            LastManualResumeAction = null;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                return null;
            }

            string methodName = targetMethod.Name;
            if (methodName.Contains("Claim", StringComparison.OrdinalIgnoreCase))
            {
                ClaimCalls.Add(methodName);
            }

            if (methodName.Contains("ProbeProvider", StringComparison.OrdinalIgnoreCase))
            {
                int ordinal = args?.OfType<int>().FirstOrDefault() ?? ProbeTimes.Count + 1;
                DateTimeOffset observedAt = args?.OfType<DateTimeOffset>().FirstOrDefault() ?? DateTimeOffset.MinValue;
                ProbeOrdinals.Add(ordinal);
                ProbeTimes.Add(observedAt);
                VersionedTransitions++;
                LastPhase = ordinal == 5
                    ? Pass1PairExecutionPhase.PausedProviderUnavailable
                    : Pass1PairExecutionPhase.Running;
                LastManualResumeAction = ordinal == 5 ? "继续扫描" : null;
                return TaskResult(
                    targetMethod.ReturnType,
                    new PreparedPass1ProviderCircuitResult(
                        ordinal == 5
                            ? PreparedPass1ProviderCircuitOutcome.Paused
                            : PreparedPass1ProviderCircuitOutcome.ProbeScheduled,
                        ordinal == 5 ? Pass1CircuitState.Paused : Pass1CircuitState.Probing,
                        LastPhase,
                        ordinal,
                        ordinal == 5 ? null : observedAt.AddSeconds(10),
                        null,
                        "provider-unavailable",
                        ordinal == 5 ? "继续扫描" : string.Empty,
                        0,
                        [],
                        false));
            }

            return TaskResult(targetMethod.ReturnType, null);
        }

        private static object? TaskResult(Type returnType, object? value)
        {
            if (returnType == typeof(Task))
            {
                return Task.CompletedTask;
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                Type valueType = returnType.GetGenericArguments()[0];
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(valueType)
                    .Invoke(null, [value]);
            }

            return null;
        }
    }

    private static Type RequireProbeType()
    {
        Assembly agents = Assembly.Load("Vulgata.Agents");
        return agents.GetType(ProbeTypeName, throwOnError: false, ignoreCase: false)
            ?? throw new InvalidOperationException(
                $"T034 RED: the T035 provider health probe seam is missing: {ProbeTypeName}.");
    }

    private static MethodInfo RequirePublicStaticMethod(
        Type type,
        string name,
        int parameterCount) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(method =>
                method.Name == name && method.GetParameters().Length == parameterCount)
        ?? throw new InvalidOperationException(
            $"T034 RED: {type.FullName} must expose public static {name} with {parameterCount} parameters.");

    private static MethodInfo RequirePublicInstanceMethod(Type type, params string[] names) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(method => names.Contains(method.Name, StringComparer.Ordinal))
        ?? throw new InvalidOperationException(
            $"T034 RED: {type.FullName} must expose one of: {string.Join(", ", names)}.");

    private static Type RequireCircuitResultType(Type store)
    {
        MethodInfo method = RequirePublicInstanceMethod(
            store,
            "RecordProviderTransientAsync",
            "OpenProviderCircuitAsync",
            "RecordProviderFailureAsync");
        Type returnType = method.ReturnType;
        Assert.True(
            returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>),
            "The provider circuit transition must be an asynchronous typed public seam.");
        return returnType.GetGenericArguments()[0];
    }

    private static void AssertCircuitResult(Type returnType)
    {
        Assert.True(
            returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>),
            "Provider circuit transitions must return typed asynchronous results.");
        Type result = returnType.GetGenericArguments()[0];
        _ = RequireProperty(result, "CircuitState");
        _ = RequireProperty(result, "ProbeOrdinal");
        _ = RequireProperty(result, "NextProbeAt");
        _ = RequireProperty(result, "RetryAfterAt");
    }

    private static PropertyInfo RequireProperty(Type type, params string[] names) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(property => names.Contains(property.Name, StringComparer.Ordinal))
        ?? throw new InvalidOperationException(
            $"T034 RED: {type.FullName} must expose one of: {string.Join(", ", names)}.");

    private static TimeSpan GetNominalDelay(Type probe, int ordinal) =>
        Assert.IsType<TimeSpan>(
            RequirePublicStaticMethod(probe, "GetProbeDelay", 1).Invoke(null, [ordinal]));
}
