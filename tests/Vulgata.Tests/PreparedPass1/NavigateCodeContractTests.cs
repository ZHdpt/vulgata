using System.Reflection;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T017 RED slice for the single bounded <c>navigate_code</c>
/// public seam.  The service is intentionally located through the Core
/// assembly so this test remains a truthful RED before T018 adds the port;
/// no production placeholder is introduced by the test author.
/// </summary>
public sealed class NavigateCodeContractTests
{
    private const string NavigationPortName =
        "Vulgata.Core.DomainServices.IPass1NavigationService";

    [Fact]
    public void NavigationPort_ExposesExactlyOneClosedAsyncMethodAndServerBoundRequest()
    {
        Type port = RequireNavigationPort();

        Assert.True(port.IsInterface);
        MethodInfo[] methods = port.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        MethodInfo navigate = Assert.Single(methods, method => method.Name == "NavigateAsync");
        Assert.Equal(typeof(Task<>), navigate.ReturnType.GetGenericTypeDefinition());
        Assert.Contains(navigate.GetParameters(), parameter =>
            parameter.ParameterType == typeof(CancellationToken));

        ParameterInfo requestParameter = Assert.Single(
            navigate.GetParameters(),
            parameter => parameter.ParameterType != typeof(CancellationToken));
        PropertyInfo[] requestProperties = requestParameter.ParameterType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(requestProperties, property => property.Name == "JobId");
        Assert.Contains(requestProperties, property => property.Name == "ToolAttemptOrdinal");
        Assert.Contains(requestProperties, property => property.Name == "Operation");
        Assert.Contains(requestProperties, property => property.Name == "SourceFileCodeUnitId");
        Assert.Contains(requestProperties, property => property.Name == "Target");
        Assert.Contains(requestProperties, property => property.Name is "RequestedDepth" or "Depth");
        Assert.Contains(requestProperties, property => property.Name == "ParentObservationIdentity");
        Assert.DoesNotContain(requestProperties, property =>
            property.Name.Contains("Raw", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Shell", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Network", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("FileSystem", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, method =>
            method.Name.Contains("Schedule", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Claim", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Await", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExecutorComposition_ExposesTheDurableReservationStoreForNavigationCallbacks()
    {
        ConstructorInfo[] constructors = typeof(PreparedPass1JobExecutor)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Contains(
            constructors,
            constructor => constructor.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(IPass1NavigationAttemptReservationStore)));
    }

    [Fact]
    public void NavigationOperation_ContainsExactlyTheEightAuthorizedOperations()
    {
        Type operation = typeof(PreparedPass1NavigationOperation);

        Assert.Equal(
            new[]
            {
                "Definition",
                "References",
                "Implementations",
                "Callers",
                "Callees",
                "SymbolSearch",
                "RelatedFiles",
                "ExactSourceEvidence",
            },
            Enum.GetNames(operation));

        Assert.DoesNotContain(
            Enum.GetNames(operation),
            name => name.Contains("Raw", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Query", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Shell", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NavigationRequest_BindsExactTargetIdentityAndExplicitDepthChain()
    {
        Type request = RequireNavigateRequestType();
        PropertyInfo[] requestProperties = request.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);
        Type target = Assert.IsAssignableFrom<Type>(
            request.GetProperty("Target")?.PropertyType);
        PropertyInfo[] targetProperties = target.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(targetProperties, property => property.Name == "CanonicalPath");
        Assert.Contains(targetProperties, property => property.Name == "Symbol");
        Assert.Contains(targetProperties, property => property.Name == "Position");
        Assert.Contains(targetProperties, property => property.Name is "ExactLineRange" or "LineRange");
        Assert.Contains(targetProperties, property => property.Name == "ExpectedSourceHash");
        Assert.Contains(requestProperties, property => property.Name is "RequestedDepth" or "Depth");
        Assert.Contains(requestProperties, property => property.Name == "ParentObservationIdentity");

        Type operation = request.GetProperty("Operation")!.PropertyType;
        Assert.Equal(typeof(PreparedPass1NavigationOperation), operation);
        Assert.Equal(typeof(Guid), request.GetProperty("JobId")!.PropertyType);
        Assert.Equal(typeof(Guid), request.GetProperty("SourceFileCodeUnitId")!.PropertyType);
    }

    [Fact]
    public void NavigationResponse_PreservesDocumentFirstAndImmediateSourceFallbackEvidence()
    {
        Type response = RequireNavigationResponseType();
        PropertyInfo[] properties = response.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, property => property.Name == "Outcome");
        Assert.Contains(properties, property => property.Name is "ReceiptId" or "NavigationReceiptId");
        Assert.Contains(properties, property => property.Name == "ResultCount");
        Assert.Contains(properties, property => property.Name is "EvidenceBytes" or "ReturnedEvidenceByteCount");
        Assert.Contains(properties, property => property.Name is "Results" or "Descriptors");

        Type outcome = properties.Single(property => property.Name == "Outcome").PropertyType;
        Assert.Equal(
            new[] { "DocumentEvidence", "SourceEvidence", "Unavailable", "BoundExceeded" },
            Enum.GetNames(Nullable.GetUnderlyingType(outcome) ?? outcome));

        Type resultCollection = properties.Single(property =>
            property.Name is "Results" or "Descriptors").PropertyType;
        Type result = ElementType(resultCollection);
        PropertyInfo[] resultProperties = result.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(resultProperties, property => property.Name is "CanonicalPath" or "Path");
        Assert.Contains(resultProperties, property =>
            property.Name is "DocumentVersionId" or "SourceHash" or "TargetSourceHash");
        Assert.DoesNotContain(resultProperties, property =>
            property.Name is "SourceText" or "RawSource" or "HostPath");
    }

    [Fact]
    public void NavigationResponse_RepresentsAllBoundedOutcomesAndMetadataOnlyRefusals()
    {
        Type response = RequireNavigationResponseType();
        PropertyInfo[] properties = response.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        PropertyInfo bound = Assert.Single(properties, property =>
            property.Name is "BoundKind" or "ReasonCode");
        if (bound.Name == "BoundKind")
        {
            Type boundKind = Nullable.GetUnderlyingType(bound.PropertyType) ?? bound.PropertyType;
            Assert.Equal(
                new[] { "Calls", "Results", "EvidenceBytes", "Depth" },
                Enum.GetNames(boundKind));
        }

        Type outcome = properties.Single(property => property.Name == "Outcome").PropertyType;
        Assert.Equal(typeof(PreparedPass1NavigationOutcome), Nullable.GetUnderlyingType(outcome) ?? outcome);

        Type result = ElementType(properties.Single(property =>
            property.Name is "Results" or "Descriptors").PropertyType);
        Assert.DoesNotContain(result.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
            property.Name.Contains("EvidenceText", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("SourceText", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Diagnostic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NavigationResponse_ExposesCountersNeededForEightCallTwentyResultAnd64KiBBounds()
    {
        Type response = RequireNavigationResponseType();
        PropertyInfo[] properties = response.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, property => property.Name == "ResultCount");
        Assert.Contains(properties, property => property.Name is "EvidenceBytes" or "ReturnedEvidenceByteCount");

        Type receipt = typeof(Pass1NavigationReceipt);
        Assert.Equal(typeof(int), receipt.GetProperty(nameof(Pass1NavigationReceipt.ResultCount))!.PropertyType);
        Assert.Equal(typeof(int), receipt.GetProperty(nameof(Pass1NavigationReceipt.EvidenceBytes))!.PropertyType);
        Assert.Equal(typeof(int), receipt.GetProperty(nameof(Pass1NavigationReceipt.ReceiptOrdinal))!.PropertyType);
        Assert.Contains(
            receipt.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "ToolAttemptOrdinal" or "Operation");
    }

    [Fact]
    public void NavigationContract_UsesTypedDescriptorsForStableTargetsAndDeduplicatesCycles()
    {
        Type response = RequireNavigationResponseType();
        PropertyInfo collection = response.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Single(property => property.Name is "Results" or "Descriptors");
        Type descriptor = ElementType(collection.PropertyType);
        PropertyInfo[] descriptorProperties = descriptor.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(descriptorProperties, property => property.Name is "CanonicalPath" or "Path");
        Assert.Contains(descriptorProperties, property =>
            property.Name is "DocumentVersionId" or "SourceAnchorIds" or "SourceHash");
        Assert.DoesNotContain(descriptorProperties, property =>
            property.Name.Contains("Completion", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Schedule", StringComparison.OrdinalIgnoreCase));
    }

    private static Type RequireNavigationPort()
    {
        Type? port = typeof(PreparedPass1NavigationOperation).Assembly.GetType(
            NavigationPortName,
            throwOnError: false,
            ignoreCase: false);
        Assert.True(
            port is not null,
            $"T017 RED: the bounded Pass 1 navigation port is not implemented yet; T018 must add {NavigationPortName}.");
        return port!;
    }

    private static Type RequireNavigateRequestType()
    {
        MethodInfo navigate = Assert.Single(
            RequireNavigationPort().GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "NavigateAsync");
        return Assert.Single(
            navigate.GetParameters(),
            parameter => parameter.ParameterType != typeof(CancellationToken)).ParameterType;
    }

    private static Type RequireNavigationResponseType()
    {
        MethodInfo navigate = Assert.Single(
            RequireNavigationPort().GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "NavigateAsync");
        Assert.True(
            navigate.ReturnType.IsGenericType &&
            navigate.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
            "T018 must return a typed asynchronous navigation response.");
        return navigate.ReturnType.GetGenericArguments()[0];
    }

    private static Type ElementType(Type collection)
    {
        if (collection.IsArray)
        {
            return collection.GetElementType()!;
        }

        Type enumerable = collection.GetInterfaces()
            .Append(collection)
            .Single(type => type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable.GetGenericArguments()[0];
    }
}
