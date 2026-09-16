using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T019 RED boundary for the security half of the prepared navigate_code
/// facade.  The tests use the accepted Core/Infrastructure composition seam
/// through reflection so the security contract can be observed before T018
/// and T020 add their production types.  They intentionally do not reach into
/// raw analysis providers, storage, or the Worker implementation.
/// </summary>
public sealed class NavigateCodeSecurityTests
{
    private static readonly string[] _securityCases =
    [
        "cross-Repository target",
        "stale Profiled Revision",
        "stale source hash",
        "unauthorized ownership scope",
        "raw LSP message",
        "raw CodeGraph query/storage",
        "shell/process",
        "network",
        "unrestricted filesystem",
        "absolute host path",
        "write operation",
        "navigation scheduling",
    ];

    [Fact]
    public void NavigationFacade_BindsSecurityCasesToOneClosedUnavailableOutcome()
    {
        Type port = RequireType(
            "Vulgata.Core.DomainServices.IPass1NavigationService",
            typeof(PreparedPass1NavigationOutcome).Assembly);
        Type implementation = RequireType(
            "Vulgata.Infrastructure.Analysis.PreparedNavigation.PreparedPass1NavigationService",
            typeof(VulgataDbContext).Assembly);

        Assert.True(port.IsInterface, "Navigation must cross one dedicated Core port.");
        Assert.True(
            port.IsAssignableFrom(implementation),
            "The Infrastructure implementation must implement the dedicated Core port.");

        MethodInfo navigate = Assert.Single(
            port.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "NavigateAsync");
        Assert.Contains(
            navigate.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));

        Type requestType = navigate.GetParameters()
            .Single(parameter => parameter.ParameterType != typeof(CancellationToken))
            .ParameterType;
        PropertyInfo[] requestProperties = requestType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        // The Core request is server-owned and intentionally carries stable
        // Job/File Code Unit identity.  The model-visible boundary is checked
        // below on PreparedNavigateCodeTarget instead.
        Assert.Contains(requestProperties, property => property.Name == "JobId");
        Assert.Contains(requestProperties, property => property.Name == "SourceFileCodeUnitId");

        Assembly agents = Assembly.Load("Vulgata.Agents");
        Type function = RequireType(
            "Vulgata.Agents.WorkerExecution.PreparedNavigateCodeFunction",
            agents);
        MethodInfo create = Assert.Single(
            function.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "Create");
        Assert.Equal(
            "Microsoft.Extensions.AI.AIFunction",
            create.ReturnType.FullName);

        Type modelTarget = RequireType(
            "Vulgata.Agents.WorkerExecution.PreparedNavigateCodeTarget",
            agents);
        PropertyInfo[] modelProperties = modelTarget.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(
            modelProperties,
            property => property.Name is "JobId" or "ToolAttemptOrdinal" or
                "SourceFileCodeUnitId" or "RepositoryId" or "ScanRunId" or
                "ProfiledRevision" or "AuthorizationScope" ||
                property.Name.Contains("Budget", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(modelProperties, property => property.Name == "CanonicalPath");
        Assert.Contains(modelProperties, property => property.Name == "ExpectedSourceHash");

        Type responseType = UnwrapTaskResult(navigate.ReturnType);
        PropertyInfo outcome = responseType.GetProperty(
            "Outcome",
            BindingFlags.Public | BindingFlags.Instance)
            ?? throw new Xunit.Sdk.XunitException(
                "navigate_code must return a bounded response with one Outcome.");
        Assert.Equal(typeof(PreparedPass1NavigationOutcome), outcome.PropertyType);
        Assert.Contains(
            Enum.GetNames<PreparedPass1NavigationOutcome>(),
            value => value == nameof(PreparedPass1NavigationOutcome.Unavailable));

        // Every security case has the same nondisclosing result shape.  The
        // facade must not add a distinct existence/authorization diagnostic.
        Assert.Equal(13, _securityCases.Length);
        Assert.DoesNotContain(
            responseType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "Diagnostic" or "ProviderError" or "HostPath" or
                "RawResponse" or "RawQuery" or "RepositoryId" or "ProfiledRevision");
    }

    [Fact]
    public void NavigationRequest_UsesOnlyClosedOperationsAndBoundedTargetFields()
    {
        Type requestType = typeof(PreparedPass1NavigationRequestInput);
        PropertyInfo[] properties = requestType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        string[] serverAuthorizationFields =
        [
            "PairExecutionId",
            "JobStateVersion",
            "PairStateVersion",
            "LeaseToken",
            "LeaseExpiresAt",
            "PairState",
            "CircuitState",
            "JobState",
            "AuthorizationBinding",
        ];
        string[] allowed =
        [
            "JobId",
            "ToolAttemptOrdinal",
            "Operation",
            "SourceFileCodeUnitId",
            "Target",
            "RequestedDepth",
            "ParentObservationIdentity",
            .. serverAuthorizationFields,
        ];
        Assert.All(
            properties,
            property => Assert.Contains(property.Name, allowed));
        Assert.All(
            serverAuthorizationFields,
            field => Assert.Contains(properties, property => property.Name == field));

        Assert.Equal(
            [
                nameof(PreparedPass1NavigationOperation.Definition),
                nameof(PreparedPass1NavigationOperation.References),
                nameof(PreparedPass1NavigationOperation.Implementations),
                nameof(PreparedPass1NavigationOperation.Callers),
                nameof(PreparedPass1NavigationOperation.Callees),
                nameof(PreparedPass1NavigationOperation.SymbolSearch),
                nameof(PreparedPass1NavigationOperation.RelatedFiles),
                nameof(PreparedPass1NavigationOperation.ExactSourceEvidence),
            ],
            Enum.GetNames<PreparedPass1NavigationOperation>());

        PropertyInfo target = requestType.GetProperty("Target")
            ?? throw new Xunit.Sdk.XunitException("Navigation request must carry one typed target.");
        PropertyInfo[] targetProperties = target.PropertyType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);
        string[] targetPropertyNames = [.. targetProperties.Select(property => property.Name)];
        Assert.Contains("CanonicalPath", targetPropertyNames);
        Assert.Contains("ExpectedSourceHash", targetPropertyNames);
        Assert.Contains("ExactLineRange", targetPropertyNames);
        Assert.DoesNotContain(
            targetProperties,
            property => property.Name.Contains("Absolute", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Host", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Command", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Query", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NavigationComposition_RegistersOnlyOneClosedWorkerFunction()
    {
        Assembly agents = Assembly.Load("Vulgata.Agents");
        Type function = RequireType(
            "Vulgata.Agents.WorkerExecution.PreparedNavigateCodeFunction",
            agents);

        Assert.DoesNotContain(
            function.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Schedule", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Enumerate", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Claim", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Write", StringComparison.OrdinalIgnoreCase));

        string[] forbidden =
        [
            "JsonRpc",
            "LspClient",
            "DbContext",
            "ProcessStartInfo",
            "HttpClient",
            "Socket",
            "FileStream",
            "DirectoryInfo",
            "NetworkStream",
        ];
        foreach (string forbiddenName in forbidden)
        {
            Assert.DoesNotContain(
                function.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
                member => member.Name.Contains(forbiddenName, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void NavigationImplementation_DoesNotExposeRawProviderOrMutationSurface()
    {
        Type implementation = RequireType(
            "Vulgata.Infrastructure.Analysis.PreparedNavigation.PreparedPass1NavigationService",
            typeof(VulgataDbContext).Assembly);

        string[] forbidden =
        [
            "JsonRpc",
            "LspClient",
            "RawLsp",
            "RawCodeGraph",
            "CodeGraphStorage",
            "Shell",
            "Process",
            "Network",
            "Socket",
            "DbContext",
            "Schedule",
            "Enumerate",
            "Claim",
            "Cancel",
            "Write",
        ];
        IEnumerable<MemberInfo> publicMembers = implementation.GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        foreach (string forbiddenName in forbidden)
        {
            Assert.DoesNotContain(
                publicMembers,
                member => member.Name.Contains(forbiddenName, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void SecurityCases_DoNotGainDistinctExistenceOrAuthorizationDisclosure()
    {
        Type responseType = RequireResponseType();
        PropertyInfo[] properties = responseType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, property => property.Name == "Outcome");
        Assert.DoesNotContain(
            properties,
            property => property.Name.Contains("Exists", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Authorized", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Reason", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
    }

    private static Type RequireType(string fullName, Assembly assembly) =>
        assembly.GetType(fullName, throwOnError: false, ignoreCase: false)
        ?? throw new InvalidOperationException(
            $"T018/T020 production seam is missing: {fullName}.");

    private static Type UnwrapTaskResult(Type returnType) =>
        returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
            ? returnType.GetGenericArguments()[0]
            : throw new Xunit.Sdk.XunitException(
                "navigate_code must be asynchronous at the public seam.");

    private static Type RequireResponseType()
    {
        Type port = RequireType(
            "Vulgata.Core.DomainServices.IPass1NavigationService",
            typeof(PreparedPass1NavigationOutcome).Assembly);
        MethodInfo navigate = Assert.Single(
            port.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "NavigateAsync");
        return UnwrapTaskResult(navigate.ReturnType);
    }
}
