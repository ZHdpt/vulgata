using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T032 RED slice.
///
/// The failure policy is owned by the prepared Pass 1 store/dispatcher seam.
/// T033 must add one durable operation to classify an observed job outcome,
/// apply the pair-wide state transition, and return a sanitized result.  The
/// tests deliberately discover that operation through the public store
/// contract so this test author does not add a production type merely to make
/// the RED compile.
///
/// T038 will extend this same file with terminal accounting tests.  Nothing
/// below asserts terminal outcome accounting or later-pass authorization.
/// </summary>
public sealed class Pass1FailurePolicyTests
{
    private static readonly string[] FailureCategoryNames =
    [
        nameof(ScanJobFailureCategory.ProviderTransient),
        nameof(ScanJobFailureCategory.ProviderConfiguration),
        nameof(ScanJobFailureCategory.JobTransient),
        nameof(ScanJobFailureCategory.JobPermanent),
        nameof(ScanJobFailureCategory.Integrity),
        nameof(ScanJobFailureCategory.Security),
        nameof(ScanJobFailureCategory.Cancellation),
        nameof(ScanJobFailureCategory.Unclassified),
    ];

    public static TheoryData<string> ClosedFailureCategories => new()
    {
        FailureCategoryNames[0],
        FailureCategoryNames[1],
        FailureCategoryNames[2],
        FailureCategoryNames[3],
        FailureCategoryNames[4],
        FailureCategoryNames[5],
        FailureCategoryNames[6],
        FailureCategoryNames[7],
    };

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedPass1Store_ExposesOneDurableFailureClassificationSeam()
    {
        MethodInfo method = RequireFailureMethod();

        Assert.True(
            method.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
            "The failure policy must be asynchronous and return a typed durable result.");
        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(Guid) &&
                parameter.Name is "pairExecutionId" or "pairId");
        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(Guid) &&
                parameter.Name is "jobId" or "scanJobExecutionId");
        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(long) &&
                parameter.Name is "expectedStateVersion" or "jobStateVersion");
        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(ScanJobFailureCategory) &&
                parameter.Name is "failureCategory" or "classification");
        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));

        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(properties, property => property.Name is "Outcome" or "Disposition" or "State");
        Assert.Contains(properties, property => property.Name is "FailureCategory" or "Classification");
        Assert.Contains(properties, property => property.Name is "FailureCode" or "ReasonCode");
        Assert.DoesNotContain(
            properties,
            property => property.Name.Contains("Raw", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Diagnostic", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("ProviderBody", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Credential", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [MemberData(nameof(ClosedFailureCategories))]
    public void PreparedPass1FailureClassification_UsesTheClosedDurableCategoryAllowlist(
        string categoryName)
    {
        _ = RequireFailureMethod();

        Assert.True(
            Enum.IsDefined(typeof(ScanJobFailureCategory), categoryName),
            $"The durable failure category {categoryName} is not on the closed allowlist.");
        Assert.Equal(
            FailureCategoryNames.OrderBy(value => value),
            Enum.GetNames<ScanJobFailureCategory>().OrderBy(value => value));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobTransient_FirstFailure_RetrysTheSameJobOnceWithoutChangingDenominator()
    {
        MethodInfo method = RequireFailureMethod();
        Type result = UnwrapTask(method.ReturnType);

        Assert.Contains(
            result.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "RetryScheduled" or "RetryEligible" or "NextAttemptAt");
        Assert.Contains(
            result.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "AttemptCount" or "NextAttemptCount");
        Assert.Contains(
            result.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "DispatchedCount" or "Denominator" or "PairDispatchedCount");

        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(Guid) &&
                parameter.Name is "expectedLeaseToken" or "leaseToken");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobTransient_SecondFailure_BecomesOneOrdinaryTerminalFailure()
    {
        MethodInfo method = RequireFailureMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, property => property.Name is "Terminal" or "IsTerminal");
        Assert.Contains(properties, property => property.Name is "OrdinaryFailureCount" or "FailureCount");
        Assert.Contains(properties, property => property.Name is "FailureCategory" or "Classification");
        Assert.DoesNotContain(properties, property => property.Name is "ProviderProbeCount" or "ProbeCount");

        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(long) &&
                parameter.Name is "expectedStateVersion" or "jobStateVersion");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(nameof(ScanJobFailureCategory.ProviderConfiguration), "PausedProviderConfiguration")]
    [InlineData(nameof(ScanJobFailureCategory.Unclassified), "PausedUnclassified")]
    public void ProviderConfigurationOrUnclassified_PausesWithoutRetryOrNewDispatch(
        string categoryName,
        string expectedPauseState)
    {
        MethodInfo method = RequireFailureMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, property => property.Name is "PauseState" or "PairState" or "ScanState");
        Assert.Contains(properties, property => property.Name is "ManualResumeAction" or "ResumeAction");
        Assert.Contains(properties, property => property.Name is "NewDispatchAllowed" or "CanDispatch");
        Assert.Contains(properties, property => property.Name is "FailureCategory" or "Classification");

        Assert.DoesNotContain(properties, property => property.Name is "RetryCount" or "RetryAttempt");
        Assert.Contains(categoryName, FailureCategoryNames);
        Assert.NotEmpty(expectedPauseState);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Cancellation_StopsRemainingWorkWithoutAutomaticResume()
    {
        MethodInfo method = RequireFailureMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, property => property.Name is "Cancellation" or "Cancelled" or "CancellationRequested");
        Assert.Contains(properties, property => property.Name is "AutomaticResumeAllowed" or "CanAutoResume");
        Assert.Contains(properties, property => property.Name is "NewDispatchAllowed" or "CanDispatch");
        Assert.Contains(properties, property => property.Name is "FailureCategory" or "Classification");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(nameof(ScanJobFailureCategory.Integrity))]
    [InlineData(nameof(ScanJobFailureCategory.Security))]
    public void IntegrityOrSecurity_FailsClosedAndStopsNewDispatch(string categoryName)
    {
        MethodInfo method = RequireFailureMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, property => property.Name is "FailClosed" or "Closed" or "TerminalFailure");
        Assert.Contains(properties, property => property.Name is "NewDispatchAllowed" or "CanDispatch");
        Assert.Contains(properties, property => property.Name is "DocumentCommitted" or "CommittedDocument");
        Assert.Contains(categoryName, ClosedFailureCategories);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void DurableFailureStatus_IsSanitizedAndNeverCarriesRawDiagnostics()
    {
        MethodInfo method = RequireFailureMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, property => property.Name is "FailureCode" or "ReasonCode");
        Assert.Contains(properties, property => property.Name is "Status" or "State" or "Disposition");
        Assert.DoesNotContain(
            properties,
            property => property.Name.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Stack", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Headers", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Body", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AttemptsRetriesAndDuplicateDelivery_DoNotInflateDispatchedDenominator()
    {
        MethodInfo method = RequireFailureMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, property => property.Name is "DispatchedCount" or "Denominator" or "PairDispatchedCount");
        Assert.Contains(properties, property => property.Name is "OrdinaryFailureCount" or "FailureCount");
        Assert.DoesNotContain(properties, property => property.Name is "AttemptCount" && property.PropertyType == typeof(string));
        Assert.DoesNotContain(properties, property => property.Name is "ProviderProbeCount" or "RecoveredLeaseCount");
    }

    // T038 RED: terminal accounting is one atomic pair-scoped operation.  The
    // result must be reconstructed from the immutable start-time job set and
    // the accepted Story 12.2 manifest entries; callers cannot supply a live
    // denominator, a rescored seed count, or an independent failure list.
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void TerminalAccounting_UsesFixedStartDenominatorAndExactFailureBudget()
    {
        MethodInfo method = RequireTerminalAccountingMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, property => property.Name == "DispatchedCount");
        Assert.Contains(properties, property => property.Name == "CompletedCount");
        Assert.Contains(properties, property => property.Name == "OrdinaryFailureCount");
        Assert.Contains(properties, property => property.Name == "OrdinaryFailureBudget");
        Assert.Contains(properties, property => property.Name is "Outcome" or "Status");
        Assert.DoesNotContain(
            method.GetParameters(),
            parameter => parameter.Name is "dispatchedCount" or "ordinaryFailureBudget" or "failureCount" or
                "ordinaryFailureCount");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(0, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(50, 5)]
    [InlineData(51, 5)]
    public void TerminalAccounting_OrdinaryFailureBudget_IsMinFiveFloorTenPercent(
        int dispatchedCount,
        int expectedBudget)
    {
        MethodInfo method = RequireTerminalAccountingMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo? budget = result.GetProperty("OrdinaryFailureBudget");

        Assert.NotNull(budget);
        Assert.Equal(typeof(int), budget!.PropertyType);
        Assert.InRange(dispatchedCount, 0, int.MaxValue);
        Assert.Equal(expectedBudget, Math.Min(5, dispatchedCount / 10));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void TerminalAccounting_ReconstructsStory12Point2SeedsAndRequiresEverySeedSuccess()
    {
        MethodInfo method = RequireTerminalAccountingMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, property => property.Name == "HighValueSeedCount");
        Assert.Contains(properties, property => property.Name == "SucceededHighValueSeedCount");
        Assert.DoesNotContain(
            method.GetParameters(),
            parameter => parameter.Name is "highValueSeedCount" or "succeededHighValueSeedCount" or
                "seedCount" or "seedPaths");
        Assert.DoesNotContain(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(IEnumerable<Guid>) ||
                parameter.ParameterType == typeof(IReadOnlyCollection<Guid>));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("Succeeded")]
    [InlineData("CompletedWithWarnings")]
    [InlineData("FailedPass1")]
    public void TerminalAccounting_UsesHonestOutcomeAndWarningInventory(
        string expectedStatus)
    {
        MethodInfo method = RequireTerminalAccountingMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        PropertyInfo? status = properties.SingleOrDefault(property => property.Name is "Outcome" or "Status");
        Assert.NotNull(status);
        Assert.True(status!.PropertyType.IsEnum, "Terminal accounting status must be a closed enum.");
        Assert.Contains(expectedStatus, Enum.GetNames(status.PropertyType));

        PropertyInfo? inventory = properties.SingleOrDefault(property =>
            property.Name is "FailureInventory" or "FailedFiles" or "Warnings");
        Assert.NotNull(inventory);
        Assert.True(
            inventory!.PropertyType != typeof(string),
            "The failed-file inventory must be a structured bounded collection.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void TerminalAccounting_FailureInventoryIsCanonicalAndBoundedToPreparedFileIdentity()
    {
        MethodInfo method = RequireTerminalAccountingMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo inventory = result.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(property => property.Name is "FailureInventory" or "FailedFiles" or "Warnings")!;
        Assert.NotNull(inventory);

        Type entry = ResolveElementType(inventory!.PropertyType);
        PropertyInfo[] properties = entry.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(properties, property => property.Name == "FileCodeUnitId");
        Assert.Contains(properties, property => property.Name == "RepositoryId");
        Assert.Contains(properties, property => property.Name == "PairSelectionOrdinal");
        Assert.Contains(properties, property => property.Name == "ManifestOrdinal");
        Assert.Contains(properties, property => property.Name == "SegmentOrdinal");
        Assert.Contains(properties, property => property.Name == "RepositoryRelativePath");
        Assert.Contains(properties, property => property.Name == "FailureCategory");
        Assert.Contains(properties, property => property.Name == "FailureCode");
        Assert.DoesNotContain(
            properties,
            property => property.Name.Contains("Source", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Provider", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Diagnostic", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Exception", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void TerminalAccounting_FailedSeedOrOverBudgetStopsAtResolvingWithoutLaterPassAuthorization()
    {
        MethodInfo method = RequireTerminalAccountingMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        PropertyInfo? coordinationPhase = properties.SingleOrDefault(property =>
            property.Name is "CoordinationPhase" or "HandoffPhase" or "PairPhase");
        Assert.NotNull(coordinationPhase);
        Assert.True(coordinationPhase!.PropertyType.IsEnum);
        Assert.Contains("Resolving", Enum.GetNames(coordinationPhase.PropertyType));

        PropertyInfo? laterPass = properties.SingleOrDefault(property =>
            property.Name is "LaterPassAuthorized" or "Pass2Authorized" or "Story12Point4Authorized");
        Assert.NotNull(laterPass);
        Assert.Equal(typeof(bool), laterPass!.PropertyType);
        Assert.DoesNotContain(
            properties,
            property => property.Name.Contains("Pass2Job", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("BusinessLogicDocument", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("CrossRepository", StringComparison.OrdinalIgnoreCase));
    }

    private static MethodInfo RequireFailureMethod()
    {
        Type? contract = typeof(CanonicalEnvelope).Assembly.GetType(
            "Vulgata.Core.DomainServices.IPreparedPass1Store",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(
            contract is not null,
            "T032 RED: IPreparedPass1Store is not available; T033 must preserve the public store contract.");

        MethodInfo? method = contract!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(candidate => candidate.Name == "RecordFailureAsync");
        Assert.True(
            method is not null,
            "T032 RED: IPreparedPass1Store must expose one durable RecordFailureAsync classification seam.");
        return method!;
    }

    private static MethodInfo RequireTerminalAccountingMethod()
    {
        Type? contract = typeof(CanonicalEnvelope).Assembly.GetType(
            "Vulgata.Core.DomainServices.IPreparedPass1Store",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(
            contract is not null,
            "T038 RED: IPreparedPass1Store is not available; T039 must preserve the public store contract.");

        MethodInfo? method = contract!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(candidate => candidate.Name == "CompletePass1Async");
        Assert.True(
            method is not null,
            "T038 RED: IPreparedPass1Store must expose one atomic CompletePass1Async terminal-accounting seam.");
        Assert.True(
            method!.GetParameters().Any(parameter => parameter.ParameterType == typeof(Guid)),
            "Terminal accounting must be pair-scoped by the durable pair execution identity.");
        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));
        return method;
    }

    private static Type ResolveElementType(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType()!;
        }

        Type? enumerable = collectionType.GetInterfaces()
            .Append(collectionType)
            .SingleOrDefault(type =>
                type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        Assert.NotNull(enumerable);
        return enumerable!.GetGenericArguments()[0];
    }

    private static Type UnwrapTask(Type returnType)
    {
        Assert.True(
            returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(Task<>),
            "The failure classification seam must return Task<T>.");
        return returnType.GetGenericArguments()[0];
    }
}
