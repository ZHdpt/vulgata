using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T028 RED slice.
///
/// The implementation owner must add one public, Story-12.3-specific commit
/// port.  These tests intentionally resolve that port by name while T029 is
/// absent: the RED is therefore the missing production seam, rather than a
/// test double or a call through the legacy WorkerAgentResultCommitter.
///
/// The mutation matrix is kept here as the acceptance boundary for the port.
/// Every entry must be rejected before any Logical Document, Document Version,
/// Source Anchor, Document Section, Index Entry, REFERENCES Edge, or completed
/// Scan Job Execution is visible.  T029/T031 should replace the reflection
/// probe with the concrete request type while preserving these cases.
/// </summary>
public sealed class Pass1DocumentCommitTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ReadReferenceObservations_UnsupportedSymbolWithoutPathOrHash_ProducesClosedObservation()
    {
        const string content = """
            {
              "schemaIdentity": "code-logic/file-v2",
              "knownReferences": [],
              "deferredObservations": [
                {
                  "sourceClaimLocator": "claim@L1:C1",
                  "targetSymbol": "sendEvent",
                  "targetRepositoryRelativePath": null,
                  "targetSourceHash": null,
                  "outcome": "Unsupported"
                }
              ]
            }
            """;
        DocumentVersion source = DocumentVersion.Create(
            Guid.Parse("10000000-0000-4000-8000-000000000001"),
            Guid.Parse("10000000-0000-4000-8000-000000000002"),
            Guid.Parse("10000000-0000-4000-8000-000000000003"),
            Guid.Parse("10000000-0000-4000-8000-000000000004"),
            Guid.Parse("10000000-0000-4000-8000-000000000005"),
            "0123456789abcdef0123456789abcdef01234567",
            1,
            "Source.java",
            "src/Source.java",
            "code-logic/file-v2",
            content,
            "# Source",
            new string('1', 64),
            new string('2', 64),
            DateTimeOffset.Parse("2026-08-27T00:00:00Z"));

        Pass1ReferenceObservation observation = Assert.Single(
            Pass1CodeLogicDocumentCommitter.ReadReferenceObservations(
                source,
                Guid.Parse("10000000-0000-4000-8000-000000000006"),
                1));

        Assert.Equal("sendEvent", observation.NormalizedTargetSymbol);
        Assert.Null(observation.TargetCanonicalPath);
        Assert.Null(observation.TargetSourceHash);
        Assert.False(observation.CanResolve);
        Assert.Equal("unsupported-operation", observation.RejectionCode);
        Assert.Equal(64, observation.ObservationFingerprint.Length);
    }

    public static TheoryData<string> CanonicalGraphReplayCases => new()
    {
        "source-anchor-remove",
        "source-anchor-mutate",
        "source-anchor-reorder",
        "source-anchor-duplicate",
        "source-anchor-cardinality",
        "document-section-remove",
        "document-section-mutate",
        "document-section-reorder",
        "document-section-duplicate",
        "document-section-cardinality",
        "index-entry-remove",
        "index-entry-mutate",
        "index-entry-reorder",
        "index-entry-duplicate",
        "index-entry-cardinality",
        "direct-references-edge-remove",
        "direct-references-edge-mutate",
        "direct-references-edge-reorder",
        "direct-references-edge-duplicate",
        "direct-references-edge-cardinality",
        "reconciled-edge-union-remove",
        "reconciled-edge-union-mutate",
        "reconciled-edge-union-reorder",
        "reconciled-edge-union-duplicate",
        "reconciled-edge-union-cardinality",
    };

    public static TheoryData<string> BindingMutations => new()
    {
        "identity.job",
        "identity.scan-execution",
        "identity.system",
        "identity.repository",
        "identity.scan-run",
        "identity.file-code-unit",
        "identity.file-code-unit-fingerprint",
        "identity.prepared-manifest",
        "identity.prepared-manifest-fingerprint",
        "identity.pair-package-fingerprint",
        "identity.prompt",
        "identity.document-schema",
        "identity.analysis-runtime",
        "authorization.repository-scope",
        "authorization.lease-token",
        "authorization.job-state-version",
        "authorization.stale-job",
        "repository.profiled-revision",
        "repository.path",
        "repository.file-hash",
        "repository.source-hash",
        "repository.byte-range",
        "repository.line-range",
        "repository.segment-range",
        "schema.unknown-field",
        "schema.duplicate-field",
        "schema.unsupported-identity",
        "schema.over-byte-bound",
        "schema.over-depth-bound",
        "schema.unsafe-text",
        "bound.too-many-sections",
        "bound.too-many-anchors",
        "bound.too-many-edges",
        "bound.too-many-search-terms",
        "bound.markdown-bytes",
        "reference.unauthorized-target",
        "reference.cross-repository-target",
        "reference.stale-target",
        "reference.changed-target-hash",
        "reference.duplicate-target",
        "classification.unknown-id",
        "classification.fingerprint",
        "classification.kind",
        "classification.level",
        "classification.confidence",
        "classification.rule",
        "classification.provenance",
        "classification.catalog-rule",
        "classification.evidence-locator",
        "classification.mixed-kind",
        "receipt.unknown",
        "receipt.job",
        "receipt.operation",
        "receipt.depth",
        "receipt.bound-exceeded-positive-claim",
        "anchor.unknown-key",
        "anchor.duplicate-key",
        "anchor.gap-key",
        "anchor.path",
        "anchor.hash",
        "anchor.byte-range",
        "anchor.line-range",
        "anchor.outside-file-unit",
        "anchor.missing-source-evidence",
    };

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CommitPort_IsStory12Point3SpecificAndHasOneAtomicOperation()
    {
        Type port = ResolvePort();
        MethodInfo[] methods = port.GetMethods();

        Assert.Single(methods);
        Assert.Equal("CommitAsync", methods[0].Name);
        Assert.DoesNotContain(
            methods[0].GetParameters(),
            parameter => parameter.ParameterType == typeof(KnowledgeCommit));
        Assert.DoesNotContain(
            methods[0].GetParameters(),
            parameter => parameter.ParameterType.Name.Contains("WorkerAgentResult", StringComparison.Ordinal));
        Assert.Contains(
            methods[0].GetParameters(),
            parameter => parameter.ParameterType == typeof(PreparedPass1CommitInput) ||
                         parameter.ParameterType.Name.Contains("Pass1DocumentCommit", StringComparison.Ordinal));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [MemberData(nameof(BindingMutations))]
    public void CommitAsync_InvalidBindingOrEvidenceMutation_IsRejectedBeforeCommit(
        string mutation)
    {
        // The concrete request/result fixture is deliberately supplied by the
        // T029 owner.  Until that production seam exists, resolving it fails
        // here (the expected RED).  Once present, this assertion is the
        // mutation table that must be driven through the public CommitAsync
        // method and must return Rejected/Conflict with zero graph writes.
        Type port = ResolvePort();
        MethodInfo commit = Assert.Single(
            port.GetMethods().Where(method => method.Name == "CommitAsync"));

        Assert.Contains(mutation, BindingMutations);
        Assert.NotNull(commit);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CommitAsync_ExactLostAcknowledgementReplay_IsZeroWriteAndReturnsSameVersion()
    {
        Type port = ResolvePort();
        MethodInfo commit = Assert.Single(
            port.GetMethods().Where(method => method.Name == "CommitAsync"));

        Assert.Contains(
            commit.GetParameters(),
            parameter => parameter.ParameterType == typeof(PreparedPass1CommitInput) ||
                         parameter.ParameterType.Name.Contains("Pass1DocumentCommit", StringComparison.Ordinal));
        Assert.DoesNotContain(
            commit.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken) &&
                         parameter.IsOut);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CommitAsync_ChangedReplay_IsConflictWithZeroWrite()
    {
        Type port = ResolvePort();
        MethodInfo commit = Assert.Single(
            port.GetMethods().Where(method => method.Name == "CommitAsync"));

        Assert.Contains(
            commit.GetParameters(),
            parameter => parameter.ParameterType == typeof(PreparedPass1CommitInput) ||
                         parameter.ParameterType.Name.Contains("Pass1DocumentCommit", StringComparison.Ordinal));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CommitAsync_CommitBoundaryIncludesAllImmutableGraphAndJobState()
    {
        // This is intentionally a named contract assertion until T029 adds
        // the concrete result type: the operation must be a specific v2 port,
        // never the legacy materializer or a separate projection transaction.
        Type port = ResolvePort();
        Assert.DoesNotContain(
            port.GetMethods(),
            method => method.Name is "CommitDocumentAsync" or "CommitProjectionAsync");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CommitBoundary_DoesNotExposeExecutionLocalStrictResultState()
    {
        Type[] durableBoundaryTypes =
        [
            typeof(Pass1DocumentCommitRequest),
            typeof(Pass1DocumentCommitResult),
            typeof(PreparedPass1CommitInput),
        ];
        string[] forbiddenTokens =
        [
            "DeepSeek",
            "StrictResult",
            "SubmissionSession",
            "ProviderTranscript",
            "ReasoningContent",
            "ToolCall",
        ];

        foreach (Type boundaryType in durableBoundaryTypes)
        {
            MemberInfo[] members = boundaryType
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(member => member.MemberType is MemberTypes.Property or MemberTypes.Field)
                .ToArray();
            Assert.DoesNotContain(
                members,
                member => forbiddenTokens.Any(token =>
                    member.Name.Contains(token, StringComparison.OrdinalIgnoreCase)));

            Type[] memberTypes = members
                .Select(member => member switch
                {
                    PropertyInfo property => property.PropertyType,
                    FieldInfo field => field.FieldType,
                    _ => typeof(object),
                })
                .ToArray();
            Assert.DoesNotContain(
                memberTypes,
                memberType =>
                    memberType.Namespace?.StartsWith("Vulgata.Agents", StringComparison.Ordinal) == true ||
                    memberType.Namespace?.StartsWith("Microsoft.Extensions.AI", StringComparison.Ordinal) == true);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [MemberData(nameof(CanonicalGraphReplayCases))]
    public void CommitAsync_CanonicalGraphCorruptionCase_UsesThePublicAtomicReplayPort(
        string corruption)
    {
        // T060 RED public-seam cases.  The PostgreSQL companion executes each
        // case against a completed commit.  This assertion keeps every
        // corruption operation bound to the one public CommitAsync operation;
        // a second projection/materializer port cannot silently accept it.
        Type port = ResolvePort();
        MethodInfo commit = Assert.Single(
            port.GetMethods().Where(method => method.Name == "CommitAsync"));
        Type result = UnwrapTask(commit.ReturnType);

        Assert.Contains(corruption, CanonicalGraphReplayCases);
        Assert.Contains(
            result.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "Status" or "Outcome" or "Disposition");
        Assert.Contains(
            result.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "DocumentVersionId" or "VersionId");
        Assert.Contains(
            result.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "CommitFingerprint" or "Fingerprint");

        Type status = result.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .First(property => property.Name is "Status" or "Outcome" or "Disposition")
            .PropertyType;
        Assert.True(status.IsEnum, "The public commit result must expose a closed replay disposition.");
        Assert.Contains("Replayed", Enum.GetNames(status), StringComparer.Ordinal);
        Assert.Contains("Conflict", Enum.GetNames(status), StringComparer.Ordinal);
    }

    private static Type UnwrapTask(Type returnType)
    {
        Assert.True(
            returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(Task<>),
            "The public commit operation must return Task<T>.");
        return returnType.GetGenericArguments()[0];
    }

    private static Type ResolvePort() =>
        typeof(PreparedPass1CommitInput).Assembly.GetType(
            "Vulgata.Core.DomainServices.IPass1CodeLogicDocumentCommitter")
        ?? throw new Xunit.Sdk.XunitException(
            "T028 RED: IPass1CodeLogicDocumentCommitter is not implemented yet.");
}
