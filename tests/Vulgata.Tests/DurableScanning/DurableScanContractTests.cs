using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using System.Runtime.CompilerServices;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.DurableScanning;

public sealed class DurableScanContractTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void DurablePorts_ExposeClosedAsyncOperationsAndImmutableBoundedModels()
    {
        AssertEnumNames<DurableScanStoreOutcome>(
            "Applied",
            "Replay",
            "Conflict",
            "Rejected",
            "NotFound");
        AssertEnumNames<DurableScanReason>(
            "None",
            "Unauthorized",
            "InvalidIdentity",
            "StaleStateVersion",
            "LeaseMismatch",
            "IntegrityViolation",
            "ProviderUnavailable",
            "Cancelled",
            "Failed");
        AssertEnumNames<ScanJobResultCommitOutcome>(
            "Committed",
            "Conflict",
            "Rejected",
            "Cancelled",
            "Failed");

        AssertImmutableModel(
            typeof(DurableScanAuthorizationScope),
            "ActorId",
            "OwnedSystemIds");
        AssertImmutableModel(
            typeof(DurableScanStoreResult),
            "Outcome",
            "Reason",
            "StateVersion",
            "LeaseToken",
            "LeaseExpiresAt");
        AssertImmutableModel(
            typeof(ScanRecoverySnapshot),
            "ScanExecutionId",
            "SystemId",
            "RepositoryId",
            "StateVersion",
            "Jobs");
        AssertImmutableModel(
            typeof(ScanRecoverySnapshotResult),
            "Outcome",
            "Reason",
            "Snapshot");
        AssertImmutableModel(
            typeof(ScanRecoveryBatchTransitionCommand),
            "ScanExecutionId",
            "ExpectedStateVersion",
            "Transitions");
        AssertImmutableModel(
            typeof(ScanRecoveryResult),
            "Outcome",
            "Reason",
            "StateVersion");
        AssertImmutableModel(
            typeof(TwoRepositoryScanCoordinationResult),
            "Outcome",
            "Reason",
            "Coordination");
        AssertImmutableModel(
            typeof(DurableScanProgressResult),
            "ScanExecutionId",
            "StateVersion",
            "TotalJobCount",
            "CompletedJobCount",
            "PendingJobCount",
            "RunningJobCount",
            "RetryScheduledJobCount",
            "FailedJobCount",
            "CancelledJobCount");
        AssertImmutableModel(
            typeof(ScanJobResultCommitRequest),
            "AuthorizationScope",
            "RepositoryId",
            "ScanExecutionId",
            "ScanJobExecutionId",
            "FileCodeUnitId",
            "ScanRunId",
            "ProfiledRevision",
            "PromptIdentity",
            "DocumentSchemaIdentity",
            "AnalysisRuntimeIdentityHash",
            "DocumentVersionId",
            "CommitFingerprint",
            "ExpectedStateVersion",
            "LeaseToken",
            "WorkerResult");
        AssertImmutableModel(
            typeof(ScanJobResultCommitResult),
            "Outcome",
            "Reason",
            "CommittedDocumentVersionId",
            "StateVersion",
            "LeaseToken",
            "IsReplay");

        AssertAsyncPort(
            typeof(IDurableScanStore),
            "CreateContextSnapshotAsync",
            "CreateScanExecutionAsync",
            "InsertJobExecutionsAsync",
            "ReadRecoverySnapshotAsync",
            "ApplyRecoveryTransitionAsync",
            "ApplyRecoveryTransitionsAsync",
            "ClaimPendingJobAsync",
            "TransitionRunningJobAsync",
            "ReadProgressAsync");
        AssertTaskResultTypes(
            typeof(IDurableScanStore),
            ("CreateContextSnapshotAsync", typeof(DurableScanStoreResult)),
            ("CreateScanExecutionAsync", typeof(DurableScanStoreResult)),
            ("InsertJobExecutionsAsync", typeof(DurableScanStoreResult)),
            ("ReadRecoverySnapshotAsync", typeof(ScanRecoverySnapshotResult)),
            ("ApplyRecoveryTransitionAsync", typeof(DurableScanStoreResult)),
            ("ApplyRecoveryTransitionsAsync", typeof(DurableScanStoreResult)),
            ("ClaimPendingJobAsync", typeof(DurableScanStoreResult)),
            ("TransitionRunningJobAsync", typeof(DurableScanStoreResult)),
            ("ReadProgressAsync", typeof(DurableScanProgressResult)));
        AssertAsyncPort(
            typeof(ITwoRepositoryScanCoordinationStore),
            "CreateAsync",
            "ReadAsync",
            "TransitionAsync");
        AssertTaskResultTypes(
            typeof(ITwoRepositoryScanCoordinationStore),
            ("CreateAsync", typeof(TwoRepositoryScanCoordinationResult)),
            ("ReadAsync", typeof(TwoRepositoryScanCoordinationResult)),
            ("TransitionAsync", typeof(TwoRepositoryScanCoordinationResult)));
        AssertAsyncPort(typeof(IScanRecoveryCoordinator), "ResumeAsync");
        AssertTaskResultTypes(
            typeof(IScanRecoveryCoordinator),
            ("ResumeAsync", typeof(ScanRecoveryResult)));
        AssertAsyncPort(typeof(IScanJobResultCommitter), "CommitAsync");
        AssertTaskResultTypes(
            typeof(IScanJobResultCommitter),
            ("CommitAsync", typeof(ScanJobResultCommitResult)));

        AssertNoNRepositoryOrPersistenceLeak(typeof(ITwoRepositoryScanCoordinationStore));
        AssertNoForbiddenPortTypes(typeof(IDurableScanStore));
        AssertNoForbiddenPortTypes(typeof(ITwoRepositoryScanCoordinationStore));
        AssertNoForbiddenPortTypes(typeof(IScanRecoveryCoordinator));
        AssertNoForbiddenPortTypes(typeof(IScanJobResultCommitter));
    }

    private static void AssertEnumNames<TEnum>(params string[] expected)
        where TEnum : struct, Enum
    {
        Assert.Equal(expected, Enum.GetNames<TEnum>());
    }

    private static void AssertImmutableModel(Type modelType, params string[] requiredProperties)
    {
        Assert.True(modelType.IsClass && modelType.IsSealed, $"{modelType.Name} must be a sealed model.");

        foreach (PropertyInfo property in modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            MethodInfo? setter = property.SetMethod;
            Assert.True(
                setter is null || setter.IsPrivate ||
                setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)),
                $"{modelType.Name}.{property.Name} must be immutable or init-only.");
        }

        foreach (string propertyName in requiredProperties)
        {
            Assert.NotNull(
                modelType.GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.Instance));
        }
    }

    private static void AssertAsyncPort(Type portType, params string[] expectedMethodNames)
    {
        Assert.True(portType.IsInterface, $"{portType.Name} must be a Core-owned interface.");
        MethodInfo[] methods = portType.GetMethods();

        Assert.Equal(
            expectedMethodNames.Order(StringComparer.Ordinal),
            methods.Select(method => method.Name).Order(StringComparer.Ordinal));

        foreach (MethodInfo method in methods)
        {
            Assert.EndsWith("Async", method.Name, StringComparison.Ordinal);
            Assert.True(
                method.ReturnType.IsGenericType &&
                method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
                $"{portType.Name}.{method.Name} must return Task<T>." );
            Assert.Contains(
                method.GetParameters(),
                parameter => parameter.ParameterType == typeof(CancellationToken));
        }
    }

    private static void AssertTaskResultTypes(
        Type portType,
        params (string MethodName, Type ResultType)[] expectedResults)
    {
        foreach ((string methodName, Type resultType) in expectedResults)
        {
            MethodInfo method = portType.GetMethod(methodName)!;
            Assert.Equal(typeof(Task<>).MakeGenericType(resultType), method.ReturnType);
        }
    }

    private static void AssertNoNRepositoryOrPersistenceLeak(Type portType)
    {
        foreach (MethodInfo method in portType.GetMethods())
        {
            Assert.DoesNotContain(
                method.GetParameters(),
                parameter => IsCollection(parameter.ParameterType));
            AssertNoForbiddenParameterTypes(method);
        }
    }

    private static void AssertNoForbiddenPortTypes(Type portType)
    {
        foreach (MethodInfo method in portType.GetMethods())
        {
            AssertNoForbiddenParameterTypes(method);
            Assert.DoesNotContain("IQueryable", method.ReturnType.FullName ?? method.ReturnType.Name, StringComparison.Ordinal);
            Assert.DoesNotContain("DbSet", method.ReturnType.FullName ?? method.ReturnType.Name, StringComparison.Ordinal);
        }
    }

    private static void AssertNoForbiddenParameterTypes(MethodInfo method)
    {
        foreach (ParameterInfo parameter in method.GetParameters())
        {
            Type parameterType = parameter.ParameterType;
            string fullName = parameterType.FullName ?? parameterType.Name;

            Assert.DoesNotContain("EntityFrameworkCore", fullName, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Diagnostics.Process", fullName, StringComparison.Ordinal);
            Assert.DoesNotContain("System.IO.Stream", fullName, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Net", fullName, StringComparison.Ordinal);
        }
    }

    private static bool IsCollection(Type type) =>
        type != typeof(string) &&
        (type.IsArray ||
            type.GetInterfaces().Any(candidate =>
                candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() is
                    var definition &&
                    (definition == typeof(IEnumerable<>) ||
                        definition == typeof(IReadOnlyCollection<>) ||
                        definition == typeof(IReadOnlyList<>))));
}
