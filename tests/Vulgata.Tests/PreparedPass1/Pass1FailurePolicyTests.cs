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

    [Fact]
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

    [Theory]
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

    [Fact]
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

    [Fact]
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

    [Theory]
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

    [Fact]
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

    [Theory]
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

    [Fact]
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

    [Fact]
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

    private static Type UnwrapTask(Type returnType)
    {
        Assert.True(
            returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(Task<>),
            "The failure classification seam must return Task<T>.");
        return returnType.GetGenericArguments()[0];
    }
}
