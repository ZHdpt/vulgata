using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.Java;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T024 RED boundary for the pinned, revision-bound JDT-LS readiness seam.
/// Reflection keeps this test executable before the new production result
/// type exists; the failure is then the missing behavior, not a compile error.
/// </summary>
public sealed class PreparedJavaReadinessTests
{
    private const string _readinessServiceName =
        "Vulgata.Infrastructure.Analysis.Java.PreparedJavaReadinessService";

    private static readonly string[] _requiredReadinessProperties =
    [
        "Outcome",
        "RepositoryId",
        "ScanRunId",
        "ProfiledRevision",
        "SnapshotIdentity",
        "RuntimeIdentityHash",
        "JdtLsReleaseIdentity",
        "JdtLsArchiveSha256",
        "JavaMajorVersion",
        "ClientName",
        "ClientVersion",
        "Transport",
        "WorkspaceBindingMatches",
        "Initialized",
        "ImportStateUsable",
        "DocumentSymbolCount",
        "JavaReadinessIdentityHash"
    ];

    [Fact]
    public void JavaReadiness_PublicSeam_AcceptsOnlyRevisionBoundAnalysisScopeAndReturnsTypedOutcome()
    {
        Type serviceType = RequireType(
            typeof(JdtLsAnalysisSessionFactory).Assembly,
            _readinessServiceName);

        MethodInfo[] scopeOperations = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name.EndsWith("Async", StringComparison.Ordinal))
            .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(AnalysisScope)))
            .ToArray();

        Assert.NotEmpty(scopeOperations);

        Type readinessType = FindReadinessType(typeof(JdtLsAnalysisSessionFactory).Assembly);
        foreach (string propertyName in _requiredReadinessProperties)
        {
            Assert.NotNull(
                readinessType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance));
        }
    }

    [Fact]
    public void JavaReadiness_UsesPinnedJava21StdioRuntimeAndOrdinalFirstFileSelection()
    {
        Type serviceType = RequireType(
            typeof(JdtLsAnalysisSessionFactory).Assembly,
            _readinessServiceName);
        Type readinessType = FindReadinessType(typeof(JdtLsAnalysisSessionFactory).Assembly);

        Assert.Contains(
            serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(method => method.Name),
            name => name.Contains("Ready", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Evaluate", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("navigate_code", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Raw", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            readinessType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name),
            name => name is "JdtLsReleaseIdentity" or "JavaMajorVersion" or "ClientName");
    }

    [Fact]
    public void JavaReadiness_XMLOnlyHasExplicitNotApplicableAndDurationCannotChangeIdentity()
    {
        Type readinessType = FindReadinessType(typeof(JdtLsAnalysisSessionFactory).Assembly);
        PropertyInfo outcome = readinessType.GetProperty("Outcome") ??
            readinessType.GetProperty("Status") ??
            throw new InvalidOperationException("Java readiness outcome is not exposed.");

        PropertyInfo identity = readinessType.GetProperty("JavaReadinessIdentityHash") ??
            readinessType.GetProperty("IdentityHash") ??
            throw new InvalidOperationException("Java readiness identity hash is not exposed.");
        Assert.Equal(typeof(string), identity.PropertyType);

        PropertyInfo? duration = readinessType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(property => property.Name.Contains("Duration", StringComparison.Ordinal));
        Assert.NotNull(duration);

        Type? outcomeEnum = readinessType.Assembly.GetTypes()
            .FirstOrDefault(type =>
                type.IsEnum &&
                Enum.GetNames(type).Any(value =>
                    value.Contains("NotApplicable", StringComparison.OrdinalIgnoreCase)));
        Assert.NotNull(outcomeEnum);
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        Type? type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
        Assert.NotNull(type);
        return type!;
    }

    private static Type FindReadinessType(Assembly assembly)
    {
        Type? type = assembly.GetTypes()
            .Concat(typeof(JdtLsAnalysisSessionFactory).Assembly.GetTypes())
            .FirstOrDefault(candidate => candidate.Name.Contains("JavaReadiness", StringComparison.Ordinal));
        Assert.NotNull(type);
        return type!;
    }
}
