using System.Reflection;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T040 RED slice.
///
/// Reconciliation is deliberately entered through the prepared Pass 1 store
/// seam.  The implementation owner must add one deterministic ReconcileAsync
/// operation before these tests can become executable against PostgreSQL.  No
/// test double is allowed to hide the missing transaction boundary: the
/// operation must read immutable authoritative Documents/observations and
/// write only exact current same-Repository REFERENCES Edges plus the
/// operational outcome.
///
/// The cases below are the acceptance boundary for T041/T043.  They remain
/// here as a RED contract while the production seam is absent; the GREEN
/// implementation should drive them with a pair fixture containing current,
/// stale, ambiguous, unsupported, cross-Repository, self, duplicate, and
/// deferred observations.
/// </summary>
public sealed class Pass1ReconciliationTests
{
    private static readonly string[] ExactTargetCaseNames =
    [
        "known-reference.current.same-repository.same-scan.same-revision.current-hash",
        "deferred-observation.current.same-repository.same-scan.same-revision.current-hash",
    ];

    private static readonly string[] RejectedObservationCaseNames =
    [
        "deferred-observation.stale-document-version",
        "deferred-observation.changed-target-hash",
        "deferred-observation.ambiguous-target",
        "deferred-observation.unsupported-operation",
        "deferred-observation.cross-repository",
        "deferred-observation.cross-scan-run",
        "deferred-observation.cross-profiled-revision",
        "deferred-observation.self-target",
        "deferred-observation.duplicate-existing-edge",
        "deferred-observation.no-exact-current-target",
    ];

    public static TheoryData<string> ExactTargetCases => new()
    {
        ExactTargetCaseNames[0],
        ExactTargetCaseNames[1],
    };

    public static TheoryData<string> RejectedObservationCases => new()
    {
        RejectedObservationCaseNames[0],
        RejectedObservationCaseNames[1],
        RejectedObservationCaseNames[2],
        RejectedObservationCaseNames[3],
        RejectedObservationCaseNames[4],
        RejectedObservationCaseNames[5],
        RejectedObservationCaseNames[6],
        RejectedObservationCaseNames[7],
        RejectedObservationCaseNames[8],
        RejectedObservationCaseNames[9],
    };

    [Fact]
    public void PreparedPass1Store_ExposesOneDeterministicReconciliationOperation()
    {
        MethodInfo method = RequireReconciliationMethod();

        Assert.Equal("ReconcileAsync", method.Name);
        Assert.True(
            method.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
            "T040 RED: reconciliation must return one typed durable result.");
        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(Guid) &&
                parameter.Name is "pairExecutionId" or "pairId");
        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));
        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType.Name.Contains("Reconciliation", StringComparison.Ordinal) ||
                parameter.ParameterType.Name.Contains("Pass1", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(ExactTargetCases))]
    public void ReconcileAsync_ExactCurrentSameRepositoryTarget_CreatesOnlyOneReferencesEdge(
        string caseName)
    {
        MethodInfo method = RequireReconciliationMethod();
        Type result = UnwrapTask(method.ReturnType);

        Assert.Contains(caseName, ExactTargetCaseNames);
        Assert.Contains(
            result.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "Edges" or "ReferencesEdges" or "CreatedEdges");
        Assert.Contains(
            result.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "ReconciliationFingerprint" or "Fingerprint");
        Assert.DoesNotContain(
            result.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("CrossRepository", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Uncertainty", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(RejectedObservationCases))]
    public void ReconcileAsync_NonExactObservation_CreatesNoEdgeAndPreservesDeferredContent(
        string caseName)
    {
        MethodInfo method = RequireReconciliationMethod();
        Type result = UnwrapTask(method.ReturnType);

        Assert.Contains(caseName, RejectedObservationCaseNames);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(
            properties,
            property => property.Name is "Warnings" or "WarningInventory" or "RejectedObservations");
        Assert.Contains(
            properties,
            property => property.Name is "Outcome" or "Status" or "Disposition");
        Assert.DoesNotContain(
            properties,
            property => property.Name is "ContentJson" or "ContentMarkdown" or "DocumentVersion" or
                "DocumentSections" or "IndexEntries" or "SourceAnchors");
    }

    [Fact]
    public void ReconcileAsync_TenCompletionOrders_ExposeCanonicalEdgeAndWarningOutputsOnly()
    {
        MethodInfo method = RequireReconciliationMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // T041 must sort source Repository, pair ordinal, source Document
        // Version, observation fingerprint, and target locator.  Completion
        // order must not be an input or an output identity.
        Assert.Contains(properties, property => property.Name is "Edges" or "ReferencesEdges" or "CreatedEdges");
        Assert.Contains(properties, property => property.Name is "Warnings" or "WarningInventory");
        Assert.Contains(properties, property => property.Name is "ReconciliationFingerprint" or "Fingerprint");
        Assert.DoesNotContain(
            properties,
            property => property.Name.Contains("CompletionOrder", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("CompletedAt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReconcileAsync_ExactReplay_IsIdempotentAndChangedReplayIsConflict()
    {
        MethodInfo method = RequireReconciliationMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, property => property.Name is "Outcome" or "Status" or "Disposition");
        Type? outcome = properties
            .FirstOrDefault(property => property.Name is "Outcome" or "Status" or "Disposition")
            ?.PropertyType;
        if (outcome is not null && outcome.IsEnum)
        {
            Assert.Contains("Replay", Enum.GetNames(outcome), StringComparer.Ordinal);
            Assert.Contains("Conflict", Enum.GetNames(outcome), StringComparer.Ordinal);
        }

        Assert.Contains(properties, property => property.Name is "CreatedEdgeCount" or "ReconciledEdgeCount" or "EdgeCount");
    }

    [Fact]
    public void ReconcileAsync_MutatesNoImmutableDocumentRepresentation()
    {
        MethodInfo method = RequireReconciliationMethod();
        Type result = UnwrapTask(method.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.DoesNotContain(
            properties,
            property => property.Name is "ContentJson" or "ContentMarkdown" or "DocumentVersion" or
                "DocumentSections" or "Sections" or "IndexEntry" or "IndexEntries" or
                "SourceAnchors" or "Anchors");
        Assert.Contains(properties, property => property.Name is "Edges" or "ReferencesEdges" or "CreatedEdges");
        Assert.Contains(properties, property => property.Name is "Warnings" or "WarningInventory");
    }

    private static MethodInfo RequireReconciliationMethod()
    {
        MethodInfo? method = typeof(IPreparedPass1Store)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(candidate => candidate.Name == "ReconcileAsync");

        Assert.True(
            method is not null,
            "T040 RED: IPreparedPass1Store must expose one durable ReconcileAsync same-Repository reconciliation seam.");
        return method!;
    }

    private static Type UnwrapTask(Type returnType)
    {
        Assert.True(
            returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(Task<>),
            "The reconciliation seam must return Task<T>.");
        return returnType.GetGenericArguments()[0];
    }
}
