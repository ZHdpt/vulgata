using System.Collections;
using System.Reflection;
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
