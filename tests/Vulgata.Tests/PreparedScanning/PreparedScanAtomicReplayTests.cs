using System.Reflection;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// Story 12.2 T041 RED slice.  The store contract is intentionally exercised
/// through the single pair-commit seam; implementation details such as EF
/// tracking and transaction objects are not test authorities.
/// </summary>
public sealed class PreparedScanAtomicReplayTests
{
    [Fact]
    public void PreparedStore_ExposesOnePairWideAtomicCommitImplementation()
    {
        Type store = RequireStoreImplementation();
        Assert.True(
            typeof(IPreparedScanStore).IsAssignableFrom(store),
            "The durable implementation must use the Core pair-wide store port.");

        MethodInfo[] methods = store.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(methods, method => method.Name == "CommitPairAsync");
        Assert.DoesNotContain(
            methods,
            method => method.Name is "CommitAsync" or "InsertManifestAsync" or "SaveManifestAsync");
    }

    [Fact]
    public void PreparedStore_CommitPairAsync_ReturnsClosedCommitOutcomeAndFailureSurface()
    {
        Type store = RequireStoreImplementation();
        MethodInfo method = store.GetMethod("CommitPairAsync") ??
            throw new InvalidOperationException("PreparedScanStore.CommitPairAsync is missing.");
        Assert.Equal(typeof(Task<PreparedScanCommitResult>), method.ReturnType);
        ParameterInfo[] parameters = method.GetParameters();
        Assert.Equal(4, parameters.Length);
        Assert.Contains(parameters, parameter =>
            parameter.ParameterType == typeof(PreparedScanAuthorizationScope));
        Assert.Contains(parameters, parameter =>
            parameter.ParameterType == typeof(PreparedScanPairPackage));
        Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(long));
        Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(CancellationToken));

        Assert.Equal(
            ["Committed", "Replay", "Conflict", "Rejected", "Cancelled", "Failed"],
            Enum.GetNames<PreparedScanCommitOutcome>());
    }

    [Fact]
    public void PreparedStore_ReplayIdentityHasAllTenTypedAuditPreimageFields()
    {
        PropertyInfo[] audit = typeof(PreparedScanAuditDraft)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        string[] canonicalTags =
        [
            "RepositoryId", "ScanRunId", "RelativePath", "Outcome", "Reason",
            "FileHash", "RawByteCount", "LogicalLineCount",
            "FileCodeUnitIdentityFingerprint", "PairSelectionOrdinal",
        ];
        Assert.All(canonicalTags, name => Assert.Contains(audit, property => property.Name == name));
        Assert.Contains(audit, property => property.Name == "DetailFingerprint");

        Type store = RequireStoreImplementation();
        Assert.Contains(
            store.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Replay", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Validate", StringComparison.OrdinalIgnoreCase) ||
                method.Name == "CommitPairAsync");
    }

    [Fact]
    public void PreparedStore_PackageSurfaceBindsTwoRepositoriesAndNoDispatchAuthority()
    {
        PropertyInfo[] package = typeof(PreparedScanPairPackage)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(package, property => property.Name == "CoordinationId");
        Assert.Contains(package, property => property.Name == "Repositories");
        Assert.Contains(package, property => property.Name == "PairSelectionFingerprint");
        Assert.Contains(package, property => property.Name == "PairPackageFingerprint");
        Assert.DoesNotContain(
            package,
            property => property.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Document", StringComparison.OrdinalIgnoreCase));

        Type store = RequireStoreImplementation();
        Assert.Contains(
            store.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "CommitPairAsync");
    }

    private static Type RequireStoreImplementation()
    {
        Type? store = typeof(Vulgata.Infrastructure.Git.PreparedScanCandidateEnumerator)
            .Assembly.GetType(
                "Vulgata.Infrastructure.Data.PreparedScanStore",
                throwOnError: false,
                ignoreCase: false);
        Assert.NotNull(
            store);
        return store!;
    }
}
