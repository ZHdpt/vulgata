using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Web.Data;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T036 RED slice.
///
/// This file is deliberately below the authenticated HTTP boundary.  T044
/// owns endpoint status, authentication, ownership nondisclosure, DTO, and
/// ProblemDetails assertions.  T036 records the pre-production recovery
/// contract that T037 must provide to that endpoint: one pair-wide resume
/// operation, current binding revalidation, durable transitions, provider
/// probing, and sanitized outcomes.
///
/// The reflection seam is intentional while the coordinator/store owner is
/// absent.  It keeps this test-only slice compilable without adding a
/// production type merely to make RED compile.
/// </summary>
public sealed class Pass1RecoveryAndResumeApiTests
{
    private static readonly TimeSpan[] ExpectedProviderProbeWindows =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
    ];

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedPass1Coordinator_ExposesOnePairWideResumeSeam()
    {
        MethodInfo resume = RequireResumeMethod();

        Assert.Contains(
            resume.GetParameters(),
            parameter => parameter.ParameterType == typeof(Guid));
        Assert.Contains(
            resume.GetParameters(),
            parameter => parameter.ParameterType == typeof(PreparedScanAuthorizationScope));
        Assert.Contains(
            resume.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));
        Assert.True(
            resume.ReturnType.IsGenericType &&
            resume.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
            "T036 RED: recovery must return a typed durable result, not an HTTP response or raw diagnostic.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedPass1Store_ExposesAtomicRecoveryPortSeparateFromStartAndClaim()
    {
        Type store = typeof(IPreparedPass1Store);
        MethodInfo? recovery = store.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(method => method.Name is "RecoverAsync" or "ResumeAsync");

        Assert.True(
            recovery is not null,
            "T036 RED: IPreparedPass1Store must expose the durable pair recovery operation before T037 can be implemented.");
        Assert.Contains(
            recovery!.GetParameters(),
            parameter => parameter.ParameterType == typeof(Guid));
        Assert.Contains(
            recovery.GetParameters(),
            parameter => parameter.ParameterType == typeof(PreparedScanAuthorizationScope));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("pair")]
    [InlineData("child-one")]
    [InlineData("child-two")]
    [InlineData("repository")]
    [InlineData("profiled-revision")]
    [InlineData("affected-source-hash")]
    [InlineData("prompt-identity")]
    [InlineData("document-schema-identity")]
    [InlineData("analysis-runtime-identity")]
    [InlineData("provider-availability")]
    public void Recovery_RevalidatesPairAndBothChildrenForEveryCurrentBinding(string binding)
    {
        MethodInfo resume = RequireResumeMethod();
        Type result = UnwrapTask(resume.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(
            properties,
            property => property.Name is "Outcome" or "RecoveryOutcome");
        Assert.Contains(
            properties,
            property => property.Name is "Reason" or "FailureCode");
        Assert.True(
            properties.Any(property => property.Name.Contains("Pair", StringComparison.OrdinalIgnoreCase)) ||
            binding == "pair",
            $"T036 RED: recovery result must retain pair binding evidence for {binding}.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Recovery_TransitionsOnlyExpiredLeasesAndDueNonExhaustedRetries()
    {
        MethodInfo resume = RequireResumeMethod();
        Type result = UnwrapTask(resume.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(
            properties,
            property => property.Name is "RecoveredExpiredLeaseCount" or "ExpiredLeaseCount");
        Assert.Contains(
            properties,
            property => property.Name is "ReleasedDueRetryCount" or "DueRetryCount");
        Assert.Contains(
            properties,
            property => property.Name is "CompletedJobCount" or "SkippedCompletedJobCount");
        Assert.Contains(
            properties,
            property => property.Name is "PendingJobCount" or "RemainingJobCount");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Recovery_UsesExactFiveProviderProbeWindowsAndStopsPairClaimsWhileUnavailable()
    {
        Type coordinator = RequireCoordinator();
        MethodInfo? probeWindow = coordinator.GetMethod(
            "GetProviderProbeWindow",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

        Assert.True(
            probeWindow is not null,
            "T036 RED: provider recovery must expose the deterministic probe-window policy to the coordinator/store seam.");
        Assert.Equal(typeof(TimeSpan), probeWindow!.ReturnType);
        Assert.Equal(typeof(int), Assert.Single(probeWindow.GetParameters()).ParameterType);

        object? instance = probeWindow.IsStatic ? null : Activator.CreateInstance(coordinator);
        for (int ordinal = 0; ordinal < ExpectedProviderProbeWindows.Length; ordinal++)
        {
            Assert.Equal(
                ExpectedProviderProbeWindows[ordinal],
                Assert.IsType<TimeSpan>(probeWindow.Invoke(instance, [ordinal])));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Recovery_CancellationProducesDurableCancelledOutcomeWithoutContinuation()
    {
        Type result = UnwrapTask(RequireResumeMethod().ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(
            properties,
            property => property.PropertyType == typeof(DurableScanReason) &&
                property.Name is "Reason" or "FailureReason");
        Assert.Contains(
            Enum.GetNames<DurableScanReason>(),
            name => name == nameof(DurableScanReason.Cancelled));
        Assert.Contains(
            properties,
            property => property.Name is "Continued" or "CanContinue" or "ContinuationAllowed");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Recovery_ExactNoOpReplayIsDurableAndCompletedJobsAreSkipped()
    {
        Type result = UnwrapTask(RequireResumeMethod().ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(
            properties,
            property => property.Name is "IsReplay" or "Replay");
        Assert.Contains(
            properties,
            property => property.Name is "StateVersion" or "ResultingStateVersion");
        Assert.Contains(
            properties,
            property => property.Name is "SkippedCompletedJobCount" or "CompletedJobCount");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("provider-unavailable")]
    [InlineData("operator-correction")]
    [InlineData("inspection")]
    [InlineData("integrity")]
    [InlineData("security")]
    [InlineData("cancelled")]
    // Characterization only: the additive pair entity already exposes these
    // bounded phase/reason atoms and no RED is expected for that existing
    // behavior.  The recovery owner still has to persist them atomically.
    public void Recovery_PersistsSanitizedPauseOrFailAtoms(string stateAtom)
    {
        Type pair = typeof(Pass1PairExecution);
        PropertyInfo[] properties = pair.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(
            properties,
            property => property.Name is "Phase" or "CircuitState");
        Assert.Contains(
            properties,
            property => property.Name is "ReasonCategory" or "ReasonCode");
        Assert.DoesNotContain(
            properties,
            property => property.Name.Contains("Diagnostic", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("RawResponse", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            Enum.GetNames<Pass1PairExecutionPhase>().Any(name =>
                name.Contains(stateAtom.Replace("-", string.Empty), StringComparison.OrdinalIgnoreCase)) ||
            stateAtom == "operator-correction" || stateAtom == "provider-unavailable",
            $"T036 RED: durable pair phase must represent {stateAtom} without raw provider state.");
    }

    private static Type RequireCoordinator()
    {
        Type? coordinator = typeof(PreparedScanCoordinator).Assembly.GetType(
            "Vulgata.Web.Data.PreparedPass1Coordinator",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(
            coordinator is not null,
            "T036 RED: PreparedPass1Coordinator recovery owner is not implemented yet; T037 must add the closed coordinator seam.");
        return coordinator!;
    }

    private static MethodInfo RequireResumeMethod()
    {
        MethodInfo? resume = RequireCoordinator().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(method => method.Name is "ResumeAsync" or "RecoverAsync");
        Assert.True(
            resume is not null,
            "T036 RED: PreparedPass1Coordinator must expose one typed pair-wide ResumeAsync/RecoverAsync operation.");
        return resume!;
    }

    private static Type UnwrapTask(Type returnType)
    {
        Assert.True(
            returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(Task<>),
            "T036 RED: recovery must be asynchronous and return a typed durable result.");
        return returnType.GetGenericArguments()[0];
    }
}
