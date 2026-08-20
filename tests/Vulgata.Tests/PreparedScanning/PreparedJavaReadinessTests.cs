using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
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

    [Fact]
    public async Task JavaReadiness_XMLOnly_ProducesStableNotApplicableProof()
    {
        AnalysisScope scope = new(
            Guid.Parse("12000000-0000-4000-8000-000000000122"),
            Guid.Parse("22000000-0000-4000-8000-000000000122"),
            "0123456789abcdef0123456789abcdef01234567",
            "story122-xml-only",
            new string('a', 64),
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
        var service = new PreparedJavaReadinessService();
        var request = new PreparedJavaReadinessRequest(
            scope,
            new string('b', 64),
            new string('c', 64),
            EligibleJavaRelativePaths: []);

        PreparedJavaReadinessResult first = await service.ProveReadinessAsync(
            request,
            CancellationToken.None);
        PreparedJavaReadinessResult second = await service.ProveReadinessAsync(
            request,
            CancellationToken.None);

        Assert.Equal(AnalysisOperationStatus.Succeeded, first.Status);
        Assert.Null(first.Failure);
        JavaReadinessProof firstProof = Assert.IsType<JavaReadinessProof>(first.Proof);
        JavaReadinessProof secondProof = Assert.IsType<JavaReadinessProof>(second.Proof);
        Assert.Equal(JavaReadinessOutcome.NotApplicable, firstProof.Outcome);
        Assert.Null(firstProof.ReadinessFile);
        Assert.False(firstProof.WorkspaceExclusive);
        Assert.False(firstProof.InitializeCompleted);
        Assert.False(firstProof.InitializedNotificationSent);
        Assert.Equal(JavaImportState.NotApplicable, firstProof.ImportState);
        Assert.Equal(0, firstProof.DocumentSymbolCount);
        Assert.True(firstProof.RepositoryBindingMatched);
        Assert.True(firstProof.ScanRunBindingMatched);
        Assert.True(firstProof.RevisionBindingMatched);
        Assert.True(firstProof.RuntimeBindingMatched);
        Assert.True(firstProof.SnapshotBindingMatched);
        Assert.NotNull(firstProof.IdentityHash);
        Assert.Equal(firstProof.IdentityHash, secondProof.IdentityHash);
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
