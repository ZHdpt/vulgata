using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T022 RED boundary for the Story 12.2 revision-bound CodeGraph proof.
///
/// These tests deliberately discover the next public seam by reflection.  That
/// keeps the RED honest (an absent production seam is an assertion failure,
/// rather than a test-project compilation failure) while preventing the test
/// from inventing a second analysis contract.
/// </summary>
public sealed class PreparedCodeGraphReadinessTests
{
    private const string _preparedAdapterName =
        "Vulgata.Infrastructure.Analysis.CodeGraph.PreparedCodeGraphAnalysisAdapter";

    private static readonly string[] _requiredCompletenessProperties =
    [
        "Initialized",
        "IndexState",
        "PendingAdded",
        "PendingModified",
        "PendingRemoved",
        "WorktreeMismatch",
        "ReindexRecommended",
        "PendingReferences",
        "RepositoryId",
        "ScanRunId",
        "ProfiledRevision",
        "SnapshotIdentity",
        "RuntimeIdentityHash",
        "IndexIdentity",
        "ProjectPath",
        "IndexPath",
        "RepositoryBindingMatches",
        "ScanRunBindingMatches",
        "ProfiledRevisionBindingMatches",
        "SnapshotBindingMatches",
        "RuntimeBindingMatches",
        "IndexBindingMatches",
        "TrustedProjectPath",
        "TrustedIndexPath",
        "CodeGraphProofIdentityHash"
    ];

    [Fact]
    public void CodeGraphReadiness_PublicSeam_ExposesTypedRevisionBoundCompletenessProof()
    {
        Type adapterType = RequireType(
            typeof(CodeGraphAnalysisAdapter).Assembly,
            _preparedAdapterName);

        Assert.True(
            typeof(ICodeGraphAnalysisPort).IsAssignableFrom(adapterType),
            "Prepared CodeGraph readiness must remain behind the existing closed Core port.");

        Type statusType = typeof(CodeGraphStatusResult);
        foreach (string propertyName in _requiredCompletenessProperties)
        {
            Assert.NotNull(
                statusType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance));
        }

        Assert.Contains(
            adapterType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(method => method.Name),
            name => name is "IndexAsync" or "RebuildAsync");
    }

    [Fact]
    public void CodeGraphReadiness_ApprovedAdapter_UsesPinnedIdentityAndNoLegacyContextOrRawToolSurface()
    {
        Type adapterType = RequireType(
            typeof(CodeGraphAnalysisAdapter).Assembly,
            _preparedAdapterName);

        Assert.DoesNotContain("CodeGraphContext", adapterType.FullName!, StringComparison.Ordinal);
        Assert.DoesNotContain(
            adapterType.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => new[] { "Explore", "Node", "Files", "Daemon", "Unlock", "Install", "Upgrade" }
                .Contains(method.Name, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void CodeGraphReadiness_DurationIsAuditOnly_AndHardFailuresHaveClosedOutcomeSurface()
    {
        Type adapterType = RequireType(
            typeof(CodeGraphAnalysisAdapter).Assembly,
            _preparedAdapterName);

        Type proofType = FindTypeContaining(
            typeof(CodeGraphAnalysisAdapter).Assembly,
            "CodeGraphProof");

        PropertyInfo hash = proofType.GetProperty("CodeGraphProofIdentityHash") ??
            proofType.GetProperty("IdentityHash") ??
            throw new InvalidOperationException("CodeGraph proof identity hash is not exposed.");
        Assert.Equal(typeof(string), hash.PropertyType);

        PropertyInfo? duration = proofType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(property => property.Name.Contains("Duration", StringComparison.Ordinal));
        Assert.NotNull(duration);
        Assert.Contains(
            proofType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name),
            name => name.Contains("Failure", StringComparison.Ordinal) ||
                name.Contains("Complete", StringComparison.Ordinal));

        Assert.NotNull(adapterType);
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        Type? type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
        Assert.NotNull(type);
        return type!;
    }

    private static Type FindTypeContaining(Assembly assembly, string name)
    {
        Type? type = assembly.GetTypes()
            .FirstOrDefault(candidate => candidate.Name.Contains(name, StringComparison.Ordinal));
        Assert.NotNull(type);
        return type!;
    }
}
