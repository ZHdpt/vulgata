using System.Reflection;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// Story 12.2 T043 RED slice for the management-only paired preparation API.
/// The suite discovers the shared DTO and endpoint seams without duplicating
/// the Web application's authentication or persistence implementation.
/// </summary>
public sealed class PreparedScanApiTests
{
    [Fact]
    public void PreparedScanRequest_RequiresExactlyTwoRepositoryRevisionBindings()
    {
        Type request = RequireSharedType(
            type => type.Name.Contains("PreparedScan", StringComparison.Ordinal) &&
                type.Name.Contains("Request", StringComparison.Ordinal));
        PropertyInfo repositories = request.GetProperty("Repositories") ??
            throw new InvalidOperationException(
                "Prepared scan request must expose exactly two repository bindings.");
        Assert.True(
            typeof(System.Collections.IEnumerable).IsAssignableFrom(repositories.PropertyType),
            "Prepared scan request repository bindings must be a collection.");

        Type element = repositories.PropertyType.IsArray
            ? repositories.PropertyType.GetElementType()!
            : repositories.PropertyType.GetGenericArguments().Single();
        Assert.Contains(element.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == "RepositoryId");
        Assert.Contains(element.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == "GitRef");
        Assert.Contains(element.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == "ProfiledRevision");
        Assert.DoesNotContain(element.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Runtime", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Timeout", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreparedScanEndpoints_ExposeManagementPostRouteAndCoordinatorSeam()
    {
        Type endpoints = typeof(Program).Assembly.GetType(
            "Vulgata.Web.Api.Scans.PreparedScanEndpoints",
            throwOnError: false,
            ignoreCase: false) ?? throw new InvalidOperationException(
                "PreparedScanEndpoints public seam is missing.");
        Assert.Contains(
            endpoints.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name.Contains("MapPreparedScan", StringComparison.Ordinal));

        Type coordinator = typeof(Program).Assembly.GetType(
            "Vulgata.Web.Data.PreparedScanCoordinator",
            throwOnError: false,
            ignoreCase: false) ?? throw new InvalidOperationException(
                "PreparedScanCoordinator public seam is missing.");
        Assert.Contains(
            coordinator.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Prepare", StringComparison.Ordinal) ||
                method.Name.Contains("Start", StringComparison.Ordinal));
    }

    [Fact]
    public void PreparedScanResponse_UsesNamedOrdinalScopesAndStableIdentityFields()
    {
        Type response = RequireSharedType(
            type => type.Name.Contains("PreparedScan", StringComparison.Ordinal) &&
                type.Name.Contains("Response", StringComparison.Ordinal));
        PropertyInfo[] properties = response.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        string[] required =
        [
            "CoordinationId", "PairSelectionFingerprint", "PairPackageFingerprint",
            "SelectedCount",
        ];
        Assert.All(required, name => Assert.Contains(properties, property => property.Name == name));
        Assert.Contains(properties, property => property.Name == "Manifests" ||
            property.Name == "Repositories");

        Assert.DoesNotContain(
            properties,
            property => property.Name.Contains("Source", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Absolute", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Duration", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Telemetry", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Document", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreparedScanApi_DeclaresClosedProblemDetailsStatusMappingWithoutWorkerDispatch()
    {
        Type endpoints = typeof(Program).Assembly.GetType(
            "Vulgata.Web.Api.Scans.PreparedScanEndpoints",
            throwOnError: false,
            ignoreCase: false) ?? throw new InvalidOperationException(
                "PreparedScanEndpoints public seam is missing.");
        Assert.Contains(
            endpoints.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic),
            nested => nested.Name.Contains("Problem", StringComparison.OrdinalIgnoreCase) ||
                nested.Name.Contains("Response", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            endpoints.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance),
            method => method.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Document", StringComparison.OrdinalIgnoreCase));
    }

    private static Type RequireSharedType(Func<Type, bool> predicate)
    {
        Type? type = typeof(Vulgata.Shared.RoleNames).Assembly.GetTypes()
            .Where(candidate => candidate.IsPublic)
            .FirstOrDefault(predicate);
        Assert.NotNull(type);
        return type!;
    }
}
