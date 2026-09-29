using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T032 RED boundary for the typed Pass 2 Business Logic submission seam.
/// The fixture is the persisted exact-resolution handoff; every mutation below
/// changes one candidate/admission fact and observes the closed result without
/// granting the committer a publication path.
/// </summary>
public sealed class Pass2BusinessLogicValidationTests
{
    public static TheoryData<string> RejectionVectors => new()
    {
        "unknown-handle", "prior-attempt", "claim-support", "contiguous-ordinals",
        "group-fingerprint", "provider-fingerprint", "edge-fingerprint",
        "logical-key-fingerprint", "unsupported-relationship", "incomplete-provenance",
        "bounded-utf8", "fabricated-canonical-handle", "document-only-relationship",
        "missing-source-anchor", "wrong-source-anchor", "source-range-drift",
        "relationship-without-references-edge", "retrieved-fingerprint-drift", "prior-job",
    };

    private static string ProviderEvidenceFingerprint(IEnumerable<string> evidenceFingerprints) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            '\n', evidenceFingerprints))));

    public static TheoryData<string> EdgeAuthorityVectors => new()
    {
        "provider-implementation-edge-type",
        "provider-implementation-edge-source",
        "provider-implementation-edge-target",
        "provider-implementation-edge-repository",
        "provider-implementation-edge-scan-run",
        "provider-implementation-edge-provider-repository-locality",
        "provider-implementation-edge-provider-scan-locality",
        "provider-implementation-edge-direction",
        "optional-edge-type",
        "optional-edge-source",
        "optional-edge-target",
        "optional-edge-repository",
        "optional-edge-scan-run",
        "optional-edge-direction",
    };

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [MemberData(nameof(EdgeAuthorityVectors))]
    public async Task CommitAsync_EdgeAuthorityMutation_RejectsWithoutWritesAsync(string mutation)
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        if (mutation.StartsWith("optional-edge-", StringComparison.Ordinal))
        {
            await fixture.AddCanonicalOptionalSupportingEdgeAsync();
        }
        JsonObject candidate = fixture.CreateCandidate();

        await fixture.MutateEdgeAuthorityAsync(mutation);
        string before = await fixture.DurableMutationSnapshotAsync();
        fixture.Access.ResetDataAccessCounters();

        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate, fixture.ConsumerHandle);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Null(result.DocumentVersionId);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal("pass2-candidate-rejected", result.Code);
        Assert.Equal(0, fixture.Access.WriteCallCount);
        Assert.Equal(before, await fixture.DurableMutationSnapshotAsync());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CommitAsync_ValidCandidate_CommitsWithExactUsedDocumentsAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        JsonObject candidate = fixture.CreateCandidate();

        Assert.Equal(
            ExactResolutionCanonicalAuthority.ProviderIdentity(
                fixture.Decision.SystemId,
                fixture.Decision.ProviderRepositoryId!.Value,
                fixture.Decision.ProviderScanRunId!.Value,
                fixture.Decision.ProviderProfiledRevision!,
                fixture.Decision.ProviderDocumentVersionId!.Value),
            fixture.Decision.ProviderIdentity);

        fixture.Access.ResetDataAccessCounters();
        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate, fixture.ConsumerHandle);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, result.Status);
        Guid committedDocumentVersionId = Assert.IsType<Guid>(result.DocumentVersionId);
        Assert.Equal(
            new[] { fixture.ConsumerDocumentVersionId, fixture.ProviderXmlDocumentVersionId }
                .OrderBy(value => value),
            result.UsedDocumentVersionIds);
        Assert.Equal("pass2-publication-committed", result.Code);
        ExactCrossRepositoryAccessCounts activity = fixture.Access.Snapshot();
        Assert.True(activity.QueryCalls > 0 || activity.WriteCalls > 0);
        Assert.Equal(committedDocumentVersionId, await fixture.Context.DocumentVersions
            .AsNoTracking().Where(value => value.Id == committedDocumentVersionId)
            .Select(value => value.Id).SingleAsync());
        FlowGroup publishedGroup = await fixture.Context.FlowGroups.AsNoTracking()
            .SingleAsync(value => value.Id == fixture.Group.Id);
        Assert.Equal("Published", publishedGroup.State);
        Assert.Equal(committedDocumentVersionId, publishedGroup.BusinessLogicDocumentVersionId);
        Assert.Equal(committedDocumentVersionId, await fixture.Context.Pass2JobExecutions
            .AsNoTracking().Where(value => value.Id == fixture.Job.Id)
            .Select(value => value.CommittedDocumentVersionId).SingleAsync());
        Guid[] generatedTargets = await fixture.Context.DocumentEdges.AsNoTracking()
            .Where(value => value.SourceDocumentVersionId == committedDocumentVersionId &&
                value.EdgeType == DocumentEdgeType.GeneratedFrom)
            .OrderBy(value => value.TargetDocumentVersionId)
            .Select(value => value.TargetDocumentVersionId).ToArrayAsync();
        Assert.Equal(result.UsedDocumentVersionIds, generatedTargets);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CommitAsync_PersistedCanonicalProviderEvidenceSetFingerprint_CommitsAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        JsonObject candidate = fixture.CreateCandidate();
        candidate["binding"]!["providerEvidenceFingerprint"] =
            fixture.Decision.ProviderEvidenceSetFingerprint;

        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate, fixture.ConsumerHandle);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, result.Status);
        Assert.NotNull(result.DocumentVersionId);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CommitAsync_SupportingEdgeMutatedAfterCandidateValidation_RejectsWithoutWritesAsync()
    {
        var interceptor = new PublicationRelationshipMutationInterceptor();
        await using CommitFixture fixture = await CommitFixture.CreateAsync(interceptor);
        await fixture.AddCanonicalOptionalSupportingEdgeAsync();
        JsonObject candidate = fixture.CreateCandidate();
        string before = await fixture.DurableMutationSnapshotAsync();
        fixture.Access.ResetDataAccessCounters();
        interceptor.Arm(Assert.IsType<Guid>(fixture.OptionalSupportingEdgeId));

        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate, fixture.ConsumerHandle);

        Assert.True(interceptor.MutationApplied);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Null(result.DocumentVersionId);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal("pass2-candidate-rejected", result.Code);
        Assert.Equal(0, fixture.Access.WriteCallCount);
        Assert.Equal(before, await fixture.DurableMutationSnapshotAsync());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [InlineData("source-anchor-hash", "source-anchor")]
    [InlineData("source-anchor-profiled-revision", "source-anchor")]
    [InlineData("source-excerpt-hash", "source-excerpt")]
    [InlineData("source-excerpt-range", "source-excerpt")]
    [InlineData("source-excerpt-current-pointer", "source-excerpt")]
    public async Task CommitAsync_AdmittedRetrievedEvidenceAuthorityDrift_RejectsWithoutPublicationAsync(
        string vector, string evidenceKind)
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        string admittedHandle = evidenceKind == "source-anchor"
            ? fixture.ConsumerRetrievedSourceAnchorHandle
            : fixture.ConsumerRetrievedSourceExcerptHandle;
        JsonObject candidate = fixture.CreateCandidate(
            additionalEvidenceHandles: [admittedHandle],
            includeConsumerSourceAnchor: false);

        switch (vector)
        {
            case "source-anchor-hash":
            case "source-excerpt-hash":
                await fixture.MutateRetrievedSourceAnchorIdentityAsync();
                break;
            case "source-anchor-profiled-revision":
                await fixture.MutateRetrievedSourceAnchorProfiledRevisionAsync();
                break;
            case "source-excerpt-range":
                await fixture.MutateRetrievedSourceAnchorRangeAsync();
                break;
            case "source-excerpt-current-pointer":
                await fixture.ClearConsumerCurrentVersionAsync();
                break;
            default:
                throw new Xunit.Sdk.XunitException($"Unknown T032 authority vector: {vector}");
        }

        string beforeGroupState = await fixture.Context.FlowGroups.AsNoTracking()
            .Where(value => value.Id == fixture.Group.Id)
            .Select(value => value.State)
            .SingleAsync();
        Guid? beforePublishedVersion = await fixture.Context.FlowGroups.AsNoTracking()
            .Where(value => value.Id == fixture.Group.Id)
            .Select(value => value.BusinessLogicDocumentVersionId)
            .SingleAsync();
        fixture.Access.ResetDataAccessCounters();

        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate, fixture.ConsumerHandle, includeConsumerSourceAnchor: false);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Null(result.DocumentVersionId);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal("pass2-candidate-rejected", result.Code);
        Assert.Equal(0, fixture.Access.WriteCallCount);
        Assert.Equal(beforeGroupState, await fixture.Context.FlowGroups.AsNoTracking()
            .Where(value => value.Id == fixture.Group.Id)
            .Select(value => value.State)
            .SingleAsync());
        Assert.Equal(beforePublishedVersion, await fixture.Context.FlowGroups.AsNoTracking()
            .Where(value => value.Id == fixture.Group.Id)
            .Select(value => value.BusinessLogicDocumentVersionId)
            .SingleAsync());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CommitAsync_RetrievedSourceExcerptStrictSubrange_CommitsWithoutInitialAnchorAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        await fixture.AddStrictRetrievedSourceExcerptAsync();
        JsonObject candidate = fixture.CreateCandidate(
            additionalEvidenceHandles: [fixture.ConsumerStrictSourceExcerptHandle],
            includeConsumerSourceAnchor: false);

        Assert.DoesNotContain(
            fixture.ConsumerSourceAnchorHandle,
            candidate["summary"]!["evidenceHandleRefs"]!.AsArray()
                .Select(value => value!.GetValue<string>()));

        fixture.Access.ResetDataAccessCounters();
        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate, fixture.ConsumerHandle, includeConsumerSourceAnchor: false);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, result.Status);
        Guid committedDocumentVersionId = Assert.IsType<Guid>(result.DocumentVersionId);
        Assert.Equal(
            new[] { fixture.ConsumerDocumentVersionId, fixture.ProviderXmlDocumentVersionId }
                .OrderBy(value => value),
            result.UsedDocumentVersionIds);
        ExactCrossRepositoryAccessCounts activity = fixture.Access.Snapshot();
        Assert.True(activity.QueryCalls > 0 || activity.WriteCalls > 0);
        Assert.Equal(committedDocumentVersionId, await fixture.Context.DocumentVersions
            .AsNoTracking().Where(value => value.Id == committedDocumentVersionId)
            .Select(value => value.Id).SingleAsync());
        FlowGroup publishedGroup = await fixture.Context.FlowGroups.AsNoTracking()
            .SingleAsync(value => value.Id == fixture.Group.Id);
        Assert.Equal("Published", publishedGroup.State);
        Assert.Equal(committedDocumentVersionId, publishedGroup.BusinessLogicDocumentVersionId);
        Assert.Equal(committedDocumentVersionId, await fixture.Context.Pass2JobExecutions
            .AsNoTracking().Where(value => value.Id == fixture.Job.Id)
            .Select(value => value.CommittedDocumentVersionId).SingleAsync());
        Guid[] generatedTargets = await fixture.Context.DocumentEdges.AsNoTracking()
            .Where(value => value.SourceDocumentVersionId == committedDocumentVersionId &&
                value.EdgeType == DocumentEdgeType.GeneratedFrom)
            .OrderBy(value => value.TargetDocumentVersionId)
            .Select(value => value.TargetDocumentVersionId).ToArrayAsync();
        Assert.Equal(result.UsedDocumentVersionIds, generatedTargets);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CommitAsync_AdmittedSourceExcerptFromLineRangedAnchor_CommitsAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        await fixture.Context.SourceAnchors
            .Where(value => value.Id == fixture.ConsumerSourceAnchorId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.ByteStart, (long?)null)
                .SetProperty(value => value.ByteEnd, (long?)null));
        fixture.Context.ChangeTracker.Clear();
        JsonObject candidate = fixture.CreateCandidate(
            additionalEvidenceHandles: [fixture.ConsumerRetrievedSourceExcerptHandle],
            includeConsumerSourceAnchor: false);

        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate, fixture.ConsumerHandle, includeConsumerSourceAnchor: false);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, result.Status);
        Assert.NotNull(result.DocumentVersionId);
        Assert.Contains(fixture.ConsumerDocumentVersionId, result.UsedDocumentVersionIds);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [InlineData("planFingerprint")]
    [InlineData("membershipFingerprint")]
    [InlineData("exactResolutionDecisionFingerprint")]
    public async Task CommitAsync_PersistedAuthorityFingerprintDriftRejectsEchoedCorruptionWithoutPublicationAsync(
        string fingerprint)
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        string canonical = fingerprint switch
        {
            "planFingerprint" => fixture.CanonicalPlanFingerprint,
            "membershipFingerprint" => fixture.CanonicalMembershipFingerprint,
            "exactResolutionDecisionFingerprint" => fixture.CanonicalDecisionEvidenceFingerprint,
            _ => throw new Xunit.Sdk.XunitException($"Unknown T032 fingerprint vector: {fingerprint}"),
        };

        await fixture.MutateAuthorityFingerprintAsync(fingerprint);
        string persistedCorruption = fingerprint switch
        {
            "planFingerprint" => await fixture.Context.FlowGroups.AsNoTracking()
                .Where(value => value.Id == fixture.Group.Id)
                .Select(value => value.PlanFingerprint).SingleAsync(),
            "membershipFingerprint" => await fixture.Context.FlowGroups.AsNoTracking()
                .Where(value => value.Id == fixture.Group.Id)
                .Select(value => value.MembershipFingerprint!).SingleAsync(),
            "exactResolutionDecisionFingerprint" => await fixture.Context
                .CrossRepositoryResolutionDecisions.AsNoTracking()
                .Where(value => value.Id == fixture.Decision.Id)
                .Select(value => value.EvidenceFingerprint).SingleAsync(),
            _ => throw new Xunit.Sdk.XunitException($"Unknown T032 fingerprint vector: {fingerprint}"),
        };
        Assert.NotEqual(canonical, persistedCorruption);

        JsonObject candidate = fixture.CreateCandidate();
        candidate["binding"]!.AsObject()[fingerprint] = persistedCorruption;
        int beforeDocumentVersionCount = await fixture.Context.DocumentVersions.CountAsync();
        string beforeGroupState = await fixture.Context.FlowGroups.AsNoTracking()
            .Where(value => value.Id == fixture.Group.Id)
            .Select(value => value.State).SingleAsync();
        fixture.Access.ResetDataAccessCounters();

        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate, fixture.ConsumerHandle);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Null(result.DocumentVersionId);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal("pass2-candidate-rejected", result.Code);
        Assert.Equal(0, fixture.Access.WriteCallCount);
        Assert.Equal(beforeDocumentVersionCount, await fixture.Context.DocumentVersions.CountAsync());
        Assert.Equal(beforeGroupState, await fixture.Context.FlowGroups.AsNoTracking()
            .Where(value => value.Id == fixture.Group.Id)
            .Select(value => value.State).SingleAsync());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CommitAsync_CoordinatedAuthorityFingerprintCorruptionRejectsEchoedCorruptionWithoutPublicationAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        Guid[] canonicalContributionIds = fixture.Contributions
            .OrderBy(value => value.ProviderOrdinal).ThenBy(value => value.EvidenceOrdinal)
            .Select(value => value.Id).ToArray();
        string canonicalDecisionFingerprint = fixture.CanonicalDecisionEvidenceFingerprint;
        string canonicalPlanFingerprint = fixture.CanonicalPlanFingerprint;

        await fixture.MutateCoordinatedAuthorityFingerprintsAsync();
        string persistedDecisionFingerprint = await fixture.Context
            .CrossRepositoryResolutionDecisions.AsNoTracking()
            .Where(value => value.Id == fixture.Decision.Id)
            .Select(value => value.EvidenceFingerprint).SingleAsync();
        string persistedPlanFingerprint = await fixture.Context.FlowGroups.AsNoTracking()
            .Where(value => value.Id == fixture.Group.Id)
            .Select(value => value.PlanFingerprint).SingleAsync();
        string[] persistedContributionFingerprints = await fixture.Context
            .ExactResolutionProviderEvidenceContributions.AsNoTracking()
            .Where(value => value.ResolutionDecisionId == fixture.Decision.Id)
            .OrderBy(value => value.ProviderOrdinal).ThenBy(value => value.EvidenceOrdinal)
            .Select(value => value.EvidenceFingerprint).ToArrayAsync();
        string persistedProviderFingerprint = ProviderEvidenceFingerprint(
            persistedContributionFingerprints);

        Assert.NotEqual(canonicalDecisionFingerprint, persistedDecisionFingerprint);
        Assert.NotEqual(canonicalPlanFingerprint, persistedPlanFingerprint);
        Assert.All(persistedContributionFingerprints, value =>
            Assert.DoesNotContain(value, fixture.Contributions.Select(item => item.EvidenceFingerprint)));

        JsonObject candidate = fixture.CreateCandidate();
        JsonObject binding = candidate["binding"]!.AsObject();
        binding["exactResolutionDecisionFingerprint"] = persistedDecisionFingerprint;
        binding["providerEvidenceFingerprint"] = persistedProviderFingerprint;
        binding["planFingerprint"] = persistedPlanFingerprint;
        int beforeDocumentVersionCount = await fixture.Context.DocumentVersions.CountAsync();
        string beforeGroupState = await fixture.Context.FlowGroups.AsNoTracking()
            .Where(value => value.Id == fixture.Group.Id)
            .Select(value => value.State).SingleAsync();
        fixture.Access.ResetDataAccessCounters();

        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate, fixture.ConsumerHandle);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Null(result.DocumentVersionId);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal("pass2-candidate-rejected", result.Code);
        Assert.Equal(0, fixture.Access.WriteCallCount);
        Assert.Equal(beforeDocumentVersionCount, await fixture.Context.DocumentVersions.CountAsync());
        Assert.Equal(beforeGroupState, await fixture.Context.FlowGroups.AsNoTracking()
            .Where(value => value.Id == fixture.Group.Id)
            .Select(value => value.State).SingleAsync());
        Assert.Equal(canonicalContributionIds, await fixture.Context
            .ExactResolutionProviderEvidenceContributions.AsNoTracking()
            .Where(value => value.ResolutionDecisionId == fixture.Decision.Id)
            .OrderBy(value => value.ProviderOrdinal).ThenBy(value => value.EvidenceOrdinal)
            .Select(value => value.Id).ToArrayAsync());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CommitAsync_ProviderEvidenceSetFingerprintDriftAfterDispatch_RejectsWithoutPublicationAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        JsonObject candidate = fixture.CreateCandidate();
        int beforeDocumentVersionCount = await fixture.Context.DocumentVersions.CountAsync();
        string beforeGroupState = await fixture.Context.FlowGroups.AsNoTracking()
            .Where(value => value.Id == fixture.Group.Id)
            .Select(value => value.State).SingleAsync();
        await fixture.Context.CrossRepositoryResolutionDecisions
            .Where(value => value.Id == fixture.Decision.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                value => value.ProviderEvidenceSetFingerprint,
                new string('d', 64)));
        fixture.Context.ChangeTracker.Clear();
        fixture.Access.ResetDataAccessCounters();

        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate, fixture.ConsumerHandle);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Null(result.DocumentVersionId);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal("pass2-candidate-rejected", result.Code);
        Assert.Equal(0, fixture.Access.WriteCallCount);
        Assert.Equal(beforeDocumentVersionCount, await fixture.Context.DocumentVersions.CountAsync());
        Assert.Equal(beforeGroupState, await fixture.Context.FlowGroups.AsNoTracking()
            .Where(value => value.Id == fixture.Group.Id)
            .Select(value => value.State).SingleAsync());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CommitAsync_CorrelatedProviderOwnerAndFingerprintDrift_RejectsWithoutPublicationAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        ExactResolutionProviderEvidenceContribution contribution = await fixture.Context
            .ExactResolutionProviderEvidenceContributions.AsNoTracking()
            .SingleAsync(value => value.ResolutionDecisionId == fixture.Decision.Id);
        Guid driftedRepositoryId = fixture.Group.RepositoryId;
        Guid driftedScanRunId = fixture.Group.ScanRunId;
        string driftedRevision = fixture.ConsumerProfiledRevision;
        Guid driftedProviderIdentity = ExactResolutionCanonicalAuthority.ProviderIdentity(
            fixture.Decision.SystemId,
            driftedRepositoryId,
            driftedScanRunId,
            driftedRevision,
            contribution.ProviderDocumentVersionId);
        SetPrivate(contribution, nameof(contribution.RepositoryId), driftedRepositoryId);
        SetPrivate(contribution, nameof(contribution.ScanRunId), driftedScanRunId);
        SetPrivate(contribution, nameof(contribution.ProfiledRevision), driftedRevision);
        SetPrivate(contribution, nameof(contribution.ProviderIdentity), driftedProviderIdentity);
        string driftedContributionFingerprint =
            ExactResolutionCanonicalAuthority.PersistedProviderContributionFingerprint(
                fixture.Decision.EvidenceFingerprint,
                contribution);
        Guid driftedContributionId =
            ExactResolutionCanonicalAuthority.ProviderContributionId(
                driftedContributionFingerprint);
        string driftedSetFingerprint =
            ExactResolutionCanonicalAuthority.ProviderEvidenceSetFingerprint(
                [driftedContributionFingerprint]);
        await fixture.Context.ExactResolutionProviderEvidenceContributions
            .Where(value => value.Id == contribution.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Id, driftedContributionId)
                .SetProperty(value => value.RepositoryId, driftedRepositoryId)
                .SetProperty(value => value.ScanRunId, driftedScanRunId)
                .SetProperty(value => value.ProfiledRevision, driftedRevision)
                .SetProperty(value => value.ProviderIdentity, driftedProviderIdentity)
                .SetProperty(value => value.EvidenceFingerprint, driftedContributionFingerprint));
        await fixture.Context.CrossRepositoryResolutionDecisions
            .Where(value => value.Id == fixture.Decision.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.ProviderIdentity, driftedProviderIdentity)
                .SetProperty(value => value.ProviderEvidenceSetFingerprint, driftedSetFingerprint));
        fixture.Context.ChangeTracker.Clear();
        JsonObject candidate = fixture.CreateCandidate();
        candidate["binding"]!["providerEvidenceFingerprint"] =
            ProviderEvidenceFingerprint([driftedContributionFingerprint]);
        int beforeDocumentVersionCount = await fixture.Context.DocumentVersions.CountAsync();
        fixture.Access.ResetDataAccessCounters();

        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate,
            fixture.ConsumerHandle);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Null(result.DocumentVersionId);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal("pass2-candidate-rejected", result.Code);
        Assert.Equal(0, fixture.Access.WriteCallCount);
        Assert.Equal(beforeDocumentVersionCount, await fixture.Context.DocumentVersions.CountAsync());
    }

    private static void SetPrivate<T>(object target, string propertyName, T value) =>
        target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(target, value);

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CommitAsync_TitleBoundaryUsesEmbeddedBusinessLogicSchemaAuthorityAsync()
    {
        Assembly core = typeof(Pass2BusinessLogicSchemaContract).Assembly;
        using Stream schemaStream = core.GetManifestResourceStream(
            Pass2BusinessLogicSchemaContract.ResourceName)!;
        using JsonDocument schema = await JsonDocument.ParseAsync(schemaStream);
        int titleMaximum = schema.RootElement.GetProperty("$defs")
            .GetProperty("shortText").GetProperty("allOf")[1]
            .GetProperty("maxLength").GetInt32();

        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        JsonObject candidate = fixture.CreateCandidate();
        candidate["title"] = new string('界', titleMaximum + 1);
        fixture.Access.ResetDataAccessCounters();

        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate, fixture.ConsumerHandle);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Null(result.DocumentVersionId);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal("pass2-candidate-rejected", result.Code);
        Assert.Equal(0, fixture.Access.WriteCallCount);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteTheory("LocalTools")]
    [MemberData(nameof(RejectionVectors))]
    public async Task CommitAsync_InvalidSemanticVector_RejectsWithoutWritesAsync(string vector)
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        JsonObject candidate = fixture.CreateCandidate();
        int? attemptOrdinal = null;

        switch (vector)
        {
            case "unknown-handle":
                candidate["summary"]!["evidenceHandleRefs"] = new JsonArray(
                    JsonValue.Create("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
                break;
            case "prior-attempt":
                attemptOrdinal = 2;
                break;
            case "claim-support":
                candidate["summary"]!["evidenceHandleRefs"] = new JsonArray();
                break;
            case "contiguous-ordinals":
                candidate["participants"]![0]!["order"] = 2;
                break;
            case "group-fingerprint":
                candidate["binding"]!["membershipFingerprint"] = new string('0', 64);
                break;
            case "provider-fingerprint":
                candidate["binding"]!["providerEvidenceFingerprint"] = new string('0', 64);
                break;
            case "edge-fingerprint":
                candidate["binding"]!["exactResolutionEdgeId"] =
                    "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
                break;
            case "logical-key-fingerprint":
                candidate["binding"]!["logicalDocumentStableKey"] = "business-flow/v1/wrong";
                break;
            case "unsupported-relationship":
                candidate["flowSteps"]![0]!["relationshipKind"] = "GeneratedFrom";
                break;
            case "incomplete-provenance":
                await fixture.CorruptTraceProvenanceAsync();
                break;
            case "bounded-utf8":
                candidate["title"] = new string('界', 33_000);
                break;
            case "fabricated-canonical-handle":
                string fabricated = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
                candidate = fixture.CreateCandidate(fabricated);
                break;
            case "document-only-relationship":
                candidate = fixture.CreateCandidate(
                    relationshipHandle: fixture.ProviderDocumentHandle);
                break;
            case "missing-source-anchor":
                candidate = fixture.CreateCandidate(fixture.ConsumerSourceAnchorHandle);
                await fixture.RemoveConsumerSourceAnchorsAsync();
                break;
            case "wrong-source-anchor":
                candidate = fixture.CreateCandidate(fixture.ConsumerSourceAnchorHandle);
                await fixture.MutateConsumerSourceAnchorIdentityAsync();
                break;
            case "source-range-drift":
                candidate = fixture.CreateCandidate(fixture.ConsumerSourceAnchorHandle);
                await fixture.MutateConsumerSourceAnchorRangeAsync();
                break;
            case "relationship-without-references-edge":
                await fixture.MutateExactResolutionEdgeTypeAsync();
                break;
            case "prior-job":
                break;
            case "retrieved-fingerprint-drift":
                await fixture.DriftRetrievedFingerprintAsync();
                break;
            default:
                throw new Xunit.Sdk.XunitException($"Unknown T032 vector: {vector}");
        }

        fixture.Access.ResetDataAccessCounters();
        string submittedConsumerHandle = vector is ("missing-source-anchor" or "wrong-source-anchor" or "source-range-drift")
            ? fixture.ConsumerSourceAnchorHandle
            : vector == "fabricated-canonical-handle"
                ? "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
                : fixture.ConsumerHandle;
        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate,
            submittedConsumerHandle,
            attemptOrdinal,
            vector == "prior-job" ? Guid.NewGuid() : null);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Null(result.DocumentVersionId);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal("pass2-candidate-rejected", result.Code);
        Assert.Equal(0, fixture.Access.WriteCallCount);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CommitAsync_ReturnedButUncitedEvidenceIsExcludedFromUsedDocumentsAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        JsonObject candidate = fixture.CreateCandidate();

        fixture.Access.ResetDataAccessCounters();
        Pass2BusinessLogicCommitResult result = await fixture.CommitAsync(
            candidate, fixture.ConsumerHandle);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, result.Status);
        Guid committedDocumentVersionId = Assert.IsType<Guid>(result.DocumentVersionId);
        Assert.DoesNotContain(fixture.ProviderImplementationDocumentVersionId,
            result.UsedDocumentVersionIds);
        Assert.Equal(2, result.UsedDocumentVersionIds.Count);
        ExactCrossRepositoryAccessCounts activity = fixture.Access.Snapshot();
        Assert.True(activity.QueryCalls > 0 || activity.WriteCalls > 0);
        Assert.Equal(committedDocumentVersionId, await fixture.Context.DocumentVersions
            .AsNoTracking().Where(value => value.Id == committedDocumentVersionId)
            .Select(value => value.Id).SingleAsync());
        Guid[] generatedTargets = await fixture.Context.DocumentEdges.AsNoTracking()
            .Where(value => value.SourceDocumentVersionId == committedDocumentVersionId &&
                value.EdgeType == DocumentEdgeType.GeneratedFrom)
            .Select(value => value.TargetDocumentVersionId).ToArrayAsync();
        Assert.Equal(result.UsedDocumentVersionIds.OrderBy(value => value),
            generatedTargets.OrderBy(value => value));
    }

    private sealed class CommitFixture : IAsyncDisposable
    {
        private const string SchemaIdentity = "business-logic/flow-v1";
        private static readonly PreparedPass2WorkerContractAuthority WorkerContract =
            CreateWorkerContractAuthority();

        private CommitFixture(ExactCrossRepositoryTestFixture fixture, VulgataDbContext context,
            ExactCrossRepositoryAccessProbe access, RepositoryAccessScope scope,
            IAsyncDisposable planningHarness,
            Pass2JobExecution job, FlowGroup group, Pass2Execution execution,
            CrossRepositoryResolutionDecision decision, DocumentEdge edge,
            IReadOnlyList<ExactResolutionProviderEvidenceContribution> contributions,
            Guid consumerSourceAnchorId, Guid providerSourceAnchorId)
        {
            Fixture = fixture;
            Context = context;
            Access = access;
            Scope = scope;
            PlanningHarness = planningHarness;
            Job = job;
            Group = group;
            Execution = execution;
            Decision = decision;
            Edge = edge;
            Contributions = contributions;
            ConsumerDocumentVersionId = group.ConsumerDocumentVersionId!.Value;
            ProviderXmlDocumentVersionId = group.ProviderXmlDocumentVersionId!.Value;
            ProviderImplementationDocumentVersionId = group.ProviderImplementationDocumentVersionId!.Value;
            ProviderDocumentHandle = "10000000-0000-4000-8000-000000000002";
            ProviderRelationshipHandle = "10000000-0000-4000-8000-000000000003";
            ProviderHandle = ProviderRelationshipHandle;
            ConsumerRetrievedSourceAnchorHandle = "10000000-0000-4000-8000-000000000004";
            ConsumerRetrievedSourceExcerptHandle = "10000000-0000-4000-8000-000000000005";
            ConsumerStrictSourceExcerptHandle = "10000000-0000-4000-8000-000000000007";
            ConsumerSourceAnchorId = consumerSourceAnchorId;
            ProviderSourceAnchorId = providerSourceAnchorId;
            PromptIdentity = WorkerContract.PromptIdentity;
            SubmissionSchemaIdentity = WorkerContract.SubmissionSchemaIdentity;
            CanonicalPlanFingerprint = group.PlanFingerprint!;
            CanonicalMembershipFingerprint = group.MembershipFingerprint!;
            CanonicalDecisionEvidenceFingerprint = decision.EvidenceFingerprint;
            ConsumerProfiledRevision = Context.DocumentVersions.AsNoTracking()
                .Single(value => value.Id == ConsumerDocumentVersionId).ProfiledRevision;
            ConsumerHandle = Pass2InitialEvidenceHandle.Create(
                Job.Id,
                Job.AttemptCount,
                Pass2CodeLogicRetrievalItemKind.Document,
                ConsumerDocumentVersionId,
                Group.RepositoryId,
                Context.DocumentVersions.AsNoTracking()
                    .Single(value => value.Id == ConsumerDocumentVersionId).ScanRunId,
                ConsumerProfiledRevision);
            ConsumerSourceAnchorHandle = Pass2InitialEvidenceHandle.Create(
                Job.Id,
                Job.AttemptCount,
                Pass2CodeLogicRetrievalItemKind.SourceAnchor,
                ConsumerDocumentVersionId,
                Group.RepositoryId,
                Context.DocumentVersions.AsNoTracking()
                    .Single(value => value.Id == ConsumerDocumentVersionId).ScanRunId,
                ConsumerProfiledRevision,
                sourceAnchorId: ConsumerSourceAnchorId,
                byteStart: Context.SourceAnchors.AsNoTracking().Single(value => value.Id == ConsumerSourceAnchorId).ByteStart,
                byteEnd: Context.SourceAnchors.AsNoTracking().Single(value => value.Id == ConsumerSourceAnchorId).ByteEnd,
                sourceHash: Context.SourceAnchors.AsNoTracking().Single(value => value.Id == ConsumerSourceAnchorId).SourceHash);
        }

        public ExactCrossRepositoryTestFixture Fixture { get; }
        public VulgataDbContext Context { get; }
        public ExactCrossRepositoryAccessProbe Access { get; }
        public RepositoryAccessScope Scope { get; }
        public IAsyncDisposable PlanningHarness { get; }
        public Pass2JobExecution Job { get; }
        public FlowGroup Group { get; }
        public Pass2Execution Execution { get; }
        public CrossRepositoryResolutionDecision Decision { get; }
        public DocumentEdge Edge { get; }
        public Guid? OptionalSupportingEdgeId { get; private set; }
        public IReadOnlyList<ExactResolutionProviderEvidenceContribution> Contributions { get; }
        public Guid ConsumerDocumentVersionId { get; }
        public Guid ProviderXmlDocumentVersionId { get; }
        public Guid ProviderImplementationDocumentVersionId { get; }
        public string ConsumerHandle { get; }
        public string ConsumerSourceAnchorHandle { get; }
        public string ProviderDocumentHandle { get; }
        public string ProviderRelationshipHandle { get; }
        public string ProviderHandle { get; }
        public string ConsumerRetrievedSourceAnchorHandle { get; }
        public string ConsumerRetrievedSourceExcerptHandle { get; }
        public string ConsumerStrictSourceExcerptHandle { get; }
        public string PromptIdentity { get; }
        public string SubmissionSchemaIdentity { get; }
        public Guid ConsumerSourceAnchorId { get; }
        public Guid ProviderSourceAnchorId { get; }
        public string CanonicalPlanFingerprint { get; }
        public string CanonicalMembershipFingerprint { get; }
        public string CanonicalDecisionEvidenceFingerprint { get; }
        public string ConsumerProfiledRevision { get; set; }

        public static async Task<CommitFixture> CreateAsync(
            IInterceptor? interceptor = null)
        {
            object? planningHarness = null;
            try
            {
                Type harnessType = typeof(Pass2FlowPlanningTests).GetNestedType(
                    "PlanningHarness", BindingFlags.NonPublic)!;
                MethodInfo createHarness = harnessType.GetMethod(
                    "CreateAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
                Task harnessTask = (Task)createHarness.Invoke(null, [1, false])!;
                await harnessTask;
                planningHarness = harnessTask.GetType().GetProperty("Result")!.GetValue(harnessTask)!;
                ExactCrossRepositoryTestFixture fixture = GetProperty<ExactCrossRepositoryTestFixture>(
                    planningHarness, "Fixture");
                VulgataDbContext planningContext = GetProperty<VulgataDbContext>(
                    planningHarness, "Context");
                Guid executionId = GetProperty<Guid>(planningHarness, "Pass2ExecutionId");
                RepositoryAccessScope scope = new(
                    [ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                     ExactCrossRepositoryTestValues.ProviderRepositoryId],
                    new AllowAccessVerifier());
                CrossRepositoryResolutionDecision canonicalDecision = await planningContext
                    .CrossRepositoryResolutionDecisions.AsNoTracking().SingleAsync();
                Assert.Equal(
                    ExactResolutionCanonicalAuthority.DecisionId(
                        canonicalDecision.EvidenceFingerprint),
                    canonicalDecision.Id);
                Assert.Matches("^[0-9a-f]{64}$", canonicalDecision.ConsumerEntryStableKey);
                Assert.Empty(await planningContext.ExactCrossRepositoryEdgeEvidence
                    .Where(value => value.EdgeId == canonicalDecision.DocumentEdgeId)
                    .Where(value => value.EvidenceFingerprint == canonicalDecision.EvidenceFingerprint)
                    .ToArrayAsync());
                planningContext.ChangeTracker.Clear();
                PreparedPass2FlowPlanningResult plan = await new PreparedPass2FlowPlanner(planningContext)
                    .PlanAsync(executionId, scope);
                FlowGroup group = Assert.Single(plan.FlowGroups);
                Pass2Execution execution = await planningContext.Pass2Executions
                    .SingleAsync(value => value.Id == executionId);
                DateTimeOffset createdAt = DateTimeOffset.UtcNow;
                Pass2JobExecution job = Pass2JobExecution.CreatePending(
                    Guid.NewGuid(), executionId, group.Id, group.GroupOrder, createdAt);
                planningContext.FlowGroups.Add(group);
                planningContext.Pass2JobExecutions.Add(job);
                planningContext.Pass2RetrievalBudgets.Add(Pass2RetrievalBudget.Create(job.Id, createdAt));
                execution.RecordPlan(1);
                execution.MarkRunning(createdAt);
                planningContext.Entry(execution).Property(
                    nameof(Pass2Execution.WorkerPromptIdentity)).CurrentValue =
                    WorkerContract.PromptIdentity;
                planningContext.Entry(execution).Property(
                    nameof(Pass2Execution.WorkerPromptFingerprint)).CurrentValue =
                    WorkerContract.PromptFingerprint;
                planningContext.Entry(execution).Property(
                    nameof(Pass2Execution.SubmissionSchemaIdentity)).CurrentValue =
                    WorkerContract.SubmissionSchemaIdentity;
                planningContext.Entry(execution).Property(
                    nameof(Pass2Execution.SubmissionSchemaFingerprint)).CurrentValue =
                    WorkerContract.SubmissionSchemaFingerprint;
                planningContext.Entry(execution).Property(
                    nameof(Pass2Execution.ToolDeclarationFingerprint)).CurrentValue =
                    WorkerContract.ToolDeclarationFingerprint;
                await planningContext.SaveChangesAsync();
                DateTimeOffset now = DateTimeOffset.UtcNow;
                job.AcquireLease(Guid.NewGuid(), now.AddMinutes(10), now);
                await planningContext.SaveChangesAsync();
                planningContext.ChangeTracker.Clear();
                VulgataDbContext context = interceptor is null
                    ? fixture.Database.CreateContext(fixture.Access)
                    : fixture.Database.CreateContext(fixture.Access, interceptor);
                CrossRepositoryResolutionDecision decision = await context
                    .CrossRepositoryResolutionDecisions.AsNoTracking()
                    .SingleAsync(value => value.Id == group.ExactResolutionDecisionId);
                DocumentEdge edge = await context.DocumentEdges.AsNoTracking()
                    .SingleAsync(value => value.Id == group.ExactResolutionEdgeId);
                ExactResolutionProviderEvidenceContribution[] contributions = await context
                    .ExactResolutionProviderEvidenceContributions.AsNoTracking()
                    .Where(value => value.ResolutionDecisionId == decision.Id)
                    .OrderBy(value => value.ProviderOrdinal).ThenBy(value => value.EvidenceOrdinal)
                    .ToArrayAsync();
                DocumentVersion providerDocument = await context.DocumentVersions.AsNoTracking()
                    .SingleAsync(value => value.Id == group.ProviderXmlDocumentVersionId);
                // The shared Pass 1 fixture has no byte range. Add the smallest
                // valid range so the SourceExcerpt result can be admitted through
                // the same persisted retrieval ledger as the other evidence.
                await context.SourceAnchors
                    .Where(value => value.DocumentVersionId == group.ConsumerDocumentVersionId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(value => value.ByteStart, 0L)
                        .SetProperty(value => value.ByteEnd, 1L));
                context.ChangeTracker.Clear();
                SourceAnchor consumerSourceAnchor = await context.SourceAnchors.AsNoTracking()
                    .Where(value => value.DocumentVersionId == group.ConsumerDocumentVersionId)
                    .SingleAsync();
                Guid consumerSourceAnchorId = consumerSourceAnchor.Id;
                Guid providerSourceAnchorId = await context.SourceAnchors.AsNoTracking()
                    .Where(value => value.DocumentVersionId == group.ProviderXmlDocumentVersionId)
                    .Select(value => value.Id).FirstAsync();
                DateTimeOffset retrievalCreatedAt = DateTimeOffset.UtcNow;
                Pass2RetrievalReceipt receipt = Pass2RetrievalReceipt.Create(
                    Guid.Parse("10000000-0000-4000-8000-000000000011"), job.Id, 1, 1,
                    Hash("t032-tool-call"), Pass2RetrievalReceiptState.Accepted,
                    Pass2CodeLogicRetrievalOperation.Search, Hash("t032-request"),
                    4, 4, 1, Hash("t032-result"), retrievalCreatedAt);
                Pass2RetrievalResult providerDocumentResult = Pass2RetrievalResult.CreateAdmitted(
                    Guid.Parse("10000000-0000-4000-8000-000000000002"), receipt.Id, job.Id,
                    1, 1, Pass2CodeLogicRetrievalItemKind.Document, providerDocument.Id,
                    providerDocument.RepositoryId, providerDocument.ScanRunId,
                    null, null, null, null, null,
                    RetrievalDescriptorFingerprint(
                        Pass2CodeLogicRetrievalItemKind.Document, providerDocument.Id),
                    RetrievalEvidenceFingerprint(
                        Guid.Parse("10000000-0000-4000-8000-000000000002"),
                        Pass2CodeLogicRetrievalItemKind.Document, providerDocument.Id, 1),
                    1, retrievalCreatedAt);
                Pass2RetrievalResult providerRelationshipResult = Pass2RetrievalResult.CreateAdmitted(
                    Guid.Parse("10000000-0000-4000-8000-000000000003"), receipt.Id, job.Id,
                    1, 2, Pass2CodeLogicRetrievalItemKind.Relationship, providerDocument.Id,
                    providerDocument.RepositoryId, providerDocument.ScanRunId,
                    null, null, group.ExactResolutionEdgeId, null, null,
                    RetrievalDescriptorFingerprint(
                        Pass2CodeLogicRetrievalItemKind.Relationship,
                        providerDocument.Id, edgeId: group.ExactResolutionEdgeId),
                    RetrievalEvidenceFingerprint(
                        Guid.Parse("10000000-0000-4000-8000-000000000003"),
                        Pass2CodeLogicRetrievalItemKind.Relationship, providerDocument.Id, 1,
                        edgeId: group.ExactResolutionEdgeId),
                    1, retrievalCreatedAt);
                Pass2RetrievalResult consumerSourceAnchorResult = Pass2RetrievalResult.CreateAdmitted(
                    Guid.Parse("10000000-0000-4000-8000-000000000004"), receipt.Id, job.Id,
                    1, 3, Pass2CodeLogicRetrievalItemKind.SourceAnchor,
                    group.ConsumerDocumentVersionId!.Value,
                    consumerSourceAnchor.RepositoryId, consumerSourceAnchor.ScanRunId,
                    null, consumerSourceAnchor.Id, null, null, null,
                    RetrievalDescriptorFingerprint(
                        Pass2CodeLogicRetrievalItemKind.SourceAnchor,
                        group.ConsumerDocumentVersionId.Value,
                        sourceAnchorId: consumerSourceAnchor.Id),
                    RetrievalEvidenceFingerprint(
                        Guid.Parse("10000000-0000-4000-8000-000000000004"),
                        Pass2CodeLogicRetrievalItemKind.SourceAnchor,
                        group.ConsumerDocumentVersionId.Value, 1,
                        sourceAnchorId: consumerSourceAnchor.Id),
                    1, retrievalCreatedAt);
                Pass2RetrievalResult consumerSourceExcerptResult = Pass2RetrievalResult.CreateAdmitted(
                    Guid.Parse("10000000-0000-4000-8000-000000000005"), receipt.Id, job.Id,
                    1, 4, Pass2CodeLogicRetrievalItemKind.SourceExcerpt,
                    group.ConsumerDocumentVersionId.Value,
                    consumerSourceAnchor.RepositoryId, consumerSourceAnchor.ScanRunId,
                    null, consumerSourceAnchor.Id, null,
                    consumerSourceAnchor.ByteStart, consumerSourceAnchor.ByteEnd,
                    RetrievalDescriptorFingerprint(
                        Pass2CodeLogicRetrievalItemKind.SourceExcerpt,
                        group.ConsumerDocumentVersionId.Value,
                        sourceAnchorId: consumerSourceAnchor.Id,
                        byteStart: consumerSourceAnchor.ByteStart,
                        byteEnd: consumerSourceAnchor.ByteEnd),
                    RetrievalEvidenceFingerprint(
                        Guid.Parse("10000000-0000-4000-8000-000000000005"),
                        Pass2CodeLogicRetrievalItemKind.SourceExcerpt,
                        group.ConsumerDocumentVersionId.Value, 1,
                        sourceAnchorId: consumerSourceAnchor.Id,
                        byteStart: consumerSourceAnchor.ByteStart,
                        byteEnd: consumerSourceAnchor.ByteEnd),
                    1, retrievalCreatedAt);
                context.Pass2RetrievalReceipts.Add(receipt);
                context.Pass2RetrievalResults.AddRange(
                    providerDocumentResult, providerRelationshipResult,
                    consumerSourceAnchorResult, consumerSourceExcerptResult);
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();
                return new(fixture, context, fixture.Access, scope, (IAsyncDisposable)planningHarness,
                    job, group, execution, decision, edge, contributions,
                    consumerSourceAnchorId, providerSourceAnchorId);
            }
            catch
            {
                if (planningHarness is IAsyncDisposable disposable)
                {
                    await disposable.DisposeAsync();
                }
                throw;
            }
        }

        private static T GetProperty<T>(object value, string name) =>
            (T)value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(value)!;

        private static T NewEntity<T>() => (T)Activator.CreateInstance(
            typeof(T), BindingFlags.Instance | BindingFlags.NonPublic, null, null, null)!;

        private static void SetProperty<T>(object value, string name, T propertyValue) =>
            value.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(value, propertyValue);

        public JsonObject CreateCandidate(
            string? consumerHandle = null,
            string? providerDocumentHandle = null,
            string? relationshipHandle = null,
            IReadOnlyList<string>? additionalEvidenceHandles = null,
            bool includeConsumerSourceAnchor = true)
        {
            consumerHandle ??= ConsumerHandle;
            providerDocumentHandle ??= ProviderDocumentHandle;
            relationshipHandle ??= ProviderRelationshipHandle;
            string providerFingerprint = ProviderEvidenceFingerprint(
                Contributions.OrderBy(value => value.ProviderOrdinal)
                    .ThenBy(value => value.EvidenceOrdinal));
            var candidate = new
            {
                schemaIdentity = SchemaIdentity,
                binding = new
                {
                    jobId = Job.Id.ToString("D"), pass2ExecutionId = Execution.Id.ToString("D"),
                    flowGroupId = Group.Id.ToString("D"), systemId = Execution.SystemId.ToString("D"),
                    consumerRepositoryId = Group.RepositoryId.ToString("D"),
                    providerRepositoryId = Group.ProviderRepositoryId!.Value.ToString("D"),
                    protocol = Group.Protocol!.Value.ToString(),
                    normalizedEndpointIdentifierFingerprint = Hash(Group.NormalizedEndpointIdentifier!),
                    consumerEntryStableKey = Group.ConsumerEntryStableKey,
                    logicalKeySchemaIdentity = "business-flow-logical-key/1.0",
                    logicalDocumentStableKey = Group.LogicalDocumentStableKey,
                    exactResolutionDecisionId = Decision.Id.ToString("D"),
                    exactResolutionDecisionFingerprint = Decision.EvidenceFingerprint,
                    exactResolutionEdgeId = Edge.Id.ToString("D"),
                    providerEvidenceFingerprint = providerFingerprint,
                    planFingerprint = Group.PlanFingerprint, membershipFingerprint = Group.MembershipFingerprint,
                    promptIdentity = PromptIdentity, submissionSchemaIdentity = SubmissionSchemaIdentity,
                },
                title = "跨仓库业务流程",
                summary = new
                {
                    description = "Consumer invokes provider through the exact resolved endpoint.",
                        evidenceHandleRefs = (includeConsumerSourceAnchor
                            ? new[] { consumerHandle, ConsumerSourceAnchorHandle }
                            : new[] { consumerHandle })
                            .Concat(additionalEvidenceHandles ?? [])
                            .ToArray(),
                },
                participants = new[]
                {
                    new { order = 1, role = "Consumer", name = "Consumer", responsibility = "Initiates the business flow.", evidenceHandleRefs = new[] { consumerHandle } },
                    new { order = 2, role = "Provider", name = "Provider", responsibility = "Provides the resolved endpoint behavior.", evidenceHandleRefs = new[] { providerDocumentHandle } },
                },
                flowSteps = new[]
                {
                    new { order = 1, actor = "Consumer", action = "Invokes the provider endpoint.", outcome = "Provider handles the request.", relationshipKind = "References", evidenceHandleRefs = new[] { relationshipHandle } },
                },
                businessRules = Array.Empty<object>(), dataMovements = Array.Empty<object>(),
                failurePaths = Array.Empty<object>(), unresolvedStatements = Array.Empty<object>(),
            };
            return JsonNode.Parse(JsonSerializer.Serialize(candidate))!.AsObject();
        }

        public async Task<Pass2BusinessLogicCommitResult> CommitAsync(JsonObject candidate,
            string consumerHandle, int? attemptOrdinal = null, Guid? jobIdOverride = null,
            bool includeConsumerSourceAnchor = true, Guid? sourceAnchorOverride = null,
            long? byteStart = null, long? byteEnd = null)
        {
            DocumentVersion consumer = await Context.DocumentVersions.AsNoTracking()
                .SingleAsync(value => value.Id == ConsumerDocumentVersionId);
            Pass2BusinessLogicEvidenceBinding initialEvidence = new(
                consumerHandle, ConsumerDocumentVersionId, consumer.RepositoryId,
                consumer.ScanRunId, ConsumerProfiledRevision, null,
                includeConsumerSourceAnchor ? sourceAnchorOverride ?? ConsumerSourceAnchorId : null,
                null, byteStart, byteEnd);
            Pass2BusinessLogicCommitRequest request = new(jobIdOverride ?? Job.Id, Job.LeaseToken!.Value,
                attemptOrdinal ?? Job.AttemptCount,
                JsonDocument.Parse(candidate.ToJsonString()).RootElement.Clone(),
                [initialEvidence], PromptIdentity)
            {
                PromptFingerprint = WorkerContract.PromptFingerprint,
                SubmissionSchemaIdentity = WorkerContract.SubmissionSchemaIdentity,
                SubmissionSchemaFingerprint = WorkerContract.SubmissionSchemaFingerprint,
                ToolDeclarationFingerprint = WorkerContract.ToolDeclarationFingerprint,
            };
            return await new Pass2BusinessLogicDocumentCommitter(
                Context, Scope, new TestWorkerContractAuthority(WorkerContract)).CommitAsync(request);
        }

        private static PreparedPass2WorkerContractAuthority CreateWorkerContractAuthority()
        {
            Assembly assembly = typeof(PreparedPass2WorkerContractAuthoritySource).Assembly;
            static byte[] ReadResource(Assembly assembly, string suffix)
            {
                string name = assembly.GetManifestResourceNames().Single(value =>
                    value.EndsWith(suffix, StringComparison.Ordinal));
                using Stream source = assembly.GetManifestResourceStream(name) ??
                    throw new InvalidOperationException($"Missing accepted Pass 2 resource {suffix}.");
                using var buffer = new MemoryStream();
                source.CopyTo(buffer);
                return buffer.ToArray();
            }

            byte[] prompt = ReadResource(assembly, "pass2-business-logic-worker.v1.md");
            byte[] schema = ReadResource(assembly, "business-logic-flow-v1.schema.json");
            Pass2StrictDeclarationSet declarations = Pass2StrictToolDeclarations.Create(
                new PreparedRetrieveCodeLogicFunction(new EmptyRetrievalService()).Create(
                    Guid.Parse("00000000-0000-4000-8000-000000000001")),
                new PreparedSubmitBusinessLogicFunction().Create(static (_, _) =>
                    Task.FromResult(PreparedPass2CandidateDecision.Reject(
                        PreparedPass2SubmissionDisposition.InvalidSemantics))));
            return new(
                "pass2-business-logic-worker.v1",
                Convert.ToHexStringLower(SHA256.HashData(prompt)),
                "business-logic/flow-v1",
                Convert.ToHexStringLower(SHA256.HashData(schema)),
                Pass2StrictToolDeclarations.Identity,
                Convert.ToHexStringLower(SHA256.HashData(declarations.ProjectedUtf8)));
        }

        private sealed class TestWorkerContractAuthority(
            PreparedPass2WorkerContractAuthority contract) :
            IPreparedPass2WorkerContractAuthority
        {
            public PreparedPass2WorkerContractAuthority GetCurrent() => contract;
        }

        private sealed class EmptyRetrievalService : IPass2CodeLogicRetrievalService
        {
            public Task<Pass2CodeLogicRetrievalResult> RetrieveAsync(
                Guid pass2JobExecutionId,
                Pass2CodeLogicRetrievalRequest request,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(Pass2CodeLogicRetrievalResult.NoResults());
        }

        public async Task AddStrictRetrievedSourceExcerptAsync()
        {
            SourceAnchor sourceAnchor = await Context.SourceAnchors.AsNoTracking()
                .SingleAsync(value => value.Id == ConsumerSourceAnchorId);
            DateTimeOffset createdAt = DateTimeOffset.UtcNow;
            SourceAnchor strictAnchor = new(
                Guid.Parse("10000000-0000-4000-8000-000000000006"),
                sourceAnchor.DocumentVersionId,
                sourceAnchor.RepositoryId,
                sourceAnchor.ScanRunId,
                sourceAnchor.CodeUnitId,
                sourceAnchor.ProfiledRevision,
                sourceAnchor.RepositoryRelativePath,
                sourceAnchor.EvidenceLocator + "/strict-subrange",
                sourceAnchor.LineStart,
                sourceAnchor.LineEnd,
                sourceAnchor.SourceHash,
                0L,
                4L,
                createdAt);
            Pass2RetrievalReceipt receipt = Pass2RetrievalReceipt.Create(
                Guid.Parse("10000000-0000-4000-8000-000000000012"), Job.Id, 1, 2,
                Hash("t032-strict-tool-call"), Pass2RetrievalReceiptState.Accepted,
                Pass2CodeLogicRetrievalOperation.Search, Hash("t032-strict-request"),
                1, 2, 0, Hash("t032-strict-result"), createdAt);
            Pass2RetrievalResult result = Pass2RetrievalResult.CreateAdmitted(
                Guid.Parse(ConsumerStrictSourceExcerptHandle), receipt.Id, Job.Id,
                1, 1, Pass2CodeLogicRetrievalItemKind.SourceExcerpt,
                sourceAnchor.DocumentVersionId, sourceAnchor.RepositoryId,
                sourceAnchor.ScanRunId, null, strictAnchor.Id, null, 1L, 3L,
                RetrievalDescriptorFingerprint(
                    Pass2CodeLogicRetrievalItemKind.SourceExcerpt,
                    sourceAnchor.DocumentVersionId, sourceAnchorId: strictAnchor.Id,
                    byteStart: 1, byteEnd: 3),
                RetrievalEvidenceFingerprint(
                    Guid.Parse(ConsumerStrictSourceExcerptHandle),
                    Pass2CodeLogicRetrievalItemKind.SourceExcerpt,
                    sourceAnchor.DocumentVersionId, 2,
                    sourceAnchorId: strictAnchor.Id, byteStart: 1, byteEnd: 3),
                2, createdAt);
            Context.SourceAnchors.Add(strictAnchor);
            Context.Pass2RetrievalReceipts.Add(receipt);
            Context.Pass2RetrievalResults.Add(result);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        public async Task DriftRetrievedFingerprintAsync()
        {
            Pass2RetrievalResult result = await Context.Pass2RetrievalResults
                .SingleAsync(value => value.Id == Guid.Parse(ProviderRelationshipHandle));
            typeof(Pass2RetrievalResult).GetProperty(nameof(Pass2RetrievalResult.EvidenceFingerprint))!
                .SetValue(result, new string('0', 64));
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        public async Task RemoveConsumerSourceAnchorsAsync()
        {
            await Context.SourceAnchors
                .Where(value => value.DocumentVersionId == ConsumerDocumentVersionId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    value => value.ProfiledRevision, new string('f', 40)));
            Context.ChangeTracker.Clear();
        }

        public async Task MutateConsumerSourceAnchorIdentityAsync()
        {
            await Context.SourceAnchors.Where(value => value.Id == ConsumerSourceAnchorId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    value => value.SourceHash, new string('0', 64)));
            Context.ChangeTracker.Clear();
        }

        public async Task MutateConsumerSourceAnchorRangeAsync()
        {
            await Context.SourceAnchors.Where(value => value.Id == ConsumerSourceAnchorId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.ByteStart, 1L)
                    .SetProperty(value => value.ByteEnd, 2L));
            Context.ChangeTracker.Clear();
        }

        public async Task MutateConsumerSourceAnchorProfiledRevisionAsync()
        {
            await Context.SourceAnchors.Where(value => value.Id == ConsumerSourceAnchorId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    value => value.ProfiledRevision, new string('f', 40)));
            Context.ChangeTracker.Clear();
        }

        public async Task MutateRetrievedSourceAnchorIdentityAsync()
        {
            await Context.SourceAnchors.Where(value => value.Id == ConsumerSourceAnchorId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    value => value.SourceHash, new string('0', 64)));
            Context.ChangeTracker.Clear();
        }

        public async Task MutateRetrievedSourceAnchorRangeAsync()
        {
            await Context.SourceAnchors.Where(value => value.Id == ConsumerSourceAnchorId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.ByteStart, 2L)
                    .SetProperty(value => value.ByteEnd, 4L));
            Context.ChangeTracker.Clear();
        }

        public async Task MutateRetrievedSourceAnchorProfiledRevisionAsync()
        {
            await Context.SourceAnchors.Where(value => value.Id == ConsumerSourceAnchorId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    value => value.ProfiledRevision, new string('f', 40)));
            Context.ChangeTracker.Clear();
        }

        public async Task MutateAuthorityFingerprintAsync(string fingerprint)
        {
            switch (fingerprint)
            {
                case "planFingerprint":
                    await Context.FlowGroups.Where(value => value.Id == Group.Id)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(
                            value => value.PlanFingerprint, new string('a', 64)));
                    break;
                case "membershipFingerprint":
                    await Context.FlowGroups.Where(value => value.Id == Group.Id)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(
                            value => value.MembershipFingerprint, new string('b', 64)));
                    break;
                case "exactResolutionDecisionFingerprint":
                    await Context.CrossRepositoryResolutionDecisions
                        .Where(value => value.Id == Decision.Id)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(
                            value => value.EvidenceFingerprint, new string('c', 64)));
                    break;
                default:
                    throw new Xunit.Sdk.XunitException(
                        $"Unknown T032 fingerprint vector: {fingerprint}");
            }

            Context.ChangeTracker.Clear();
        }

        public async Task MutateCoordinatedAuthorityFingerprintsAsync()
        {
            string marker = Hash("t032-coordinated-authority-corruption/1.0");
            ExactResolutionProviderEvidenceContribution[] orderedContributions =
                Contributions.OrderBy(value => value.ProviderOrdinal)
                    .ThenBy(value => value.EvidenceOrdinal).ToArray();
            string[] corruptedContributionFingerprints = orderedContributions
                .Select((value, index) => Hash(string.Join(
                    '\n', "t032-coordinated-provider-contribution/1.0", marker,
                    index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    value.Id.ToString("D"))))
                .ToArray();
            for (int index = 0; index < orderedContributions.Length; index++)
            {
                Guid contributionId = orderedContributions[index].Id;
                string corruptedFingerprint = corruptedContributionFingerprints[index];
                await Context.ExactResolutionProviderEvidenceContributions
                    .Where(value => value.Id == contributionId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        value => value.EvidenceFingerprint, corruptedFingerprint));
            }

            string corruptedProviderFingerprint = ProviderEvidenceFingerprint(
                corruptedContributionFingerprints);
            string corruptedDecisionFingerprint = Hash(string.Join(
                '\n', "t032-coordinated-decision/1.0", marker,
                corruptedProviderFingerprint));
            await Context.CrossRepositoryResolutionDecisions
                .Where(value => value.Id == Decision.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    value => value.EvidenceFingerprint, corruptedDecisionFingerprint));
            ExactResolutionProviderEvidenceContribution[] persistedContributions =
                await Context.ExactResolutionProviderEvidenceContributions.AsNoTracking()
                    .Where(value => value.ResolutionDecisionId == Decision.Id)
                    .OrderBy(value => value.ProviderOrdinal)
                    .ThenBy(value => value.EvidenceOrdinal).ToArrayAsync();
            string membershipFingerprint = await Context.FlowGroups.AsNoTracking()
                .Where(value => value.Id == Group.Id)
                .Select(value => value.MembershipFingerprint!).SingleAsync();
            string corruptedPlanFingerprint = PreparedPass2FlowPlanner.ComputePlanFingerprint(
                Decision.Id,
                corruptedDecisionFingerprint,
                "Complete",
                membershipFingerprint,
                persistedContributions);
            await Context.FlowGroups.Where(value => value.Id == Group.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    value => value.PlanFingerprint, corruptedPlanFingerprint));
            Context.ChangeTracker.Clear();
        }

        public async Task ClearConsumerCurrentVersionAsync()
        {
            Guid logicalDocumentId = await Context.DocumentVersions.AsNoTracking()
                .Where(value => value.Id == ConsumerDocumentVersionId)
                .Select(value => value.LogicalDocumentId)
                .SingleAsync();
            await Context.LogicalDocuments.Where(value => value.Id == logicalDocumentId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    value => value.CurrentVersionId, (Guid?)null));
            Context.ChangeTracker.Clear();
        }

        public async Task MutateExactResolutionEdgeTypeAsync()
        {
            await Context.DocumentEdges.Where(value => value.Id == Edge.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    value => value.EdgeType, DocumentEdgeType.GeneratedFrom));
            Context.ChangeTracker.Clear();
        }

        public async Task CorruptTraceProvenanceAsync()
        {
            FlowTraceStep step = await Context.FlowTraceSteps
                .Where(value => value.DocumentVersionId == ConsumerDocumentVersionId)
                .OrderBy(value => value.StepOrder)
                .FirstAsync();
            await Context.FlowTraceSteps.Where(value => value.Id == step.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.StepOrder, 99));
            Context.ChangeTracker.Clear();
        }

        public async Task AddCanonicalOptionalSupportingEdgeAsync()
        {
            FlowTrace trace = await Context.FlowTraces
                .SingleAsync(value => value.FlowGroupId == Group.Id);
            FlowTraceStep[] existingSteps = await Context.FlowTraceSteps
                .Where(value => value.FlowTraceId == trace.Id)
                .OrderBy(value => value.StepOrder)
                .ToArrayAsync();
            Assert.Equal(5, existingSteps.Length);
            Guid implementationEdgeId = Assert.IsType<Guid>(existingSteps[3].EdgeId);
            DocumentVersion providerXml = await Context.DocumentVersions
                .SingleAsync(value => value.Id == ProviderXmlDocumentVersionId);
            DocumentVersion providerImplementation = await Context.DocumentVersions
                .SingleAsync(value => value.Id == ProviderImplementationDocumentVersionId);
            Guid optionalEdgeId = Guid.NewGuid();
            OptionalSupportingEdgeId = optionalEdgeId;
            DateTimeOffset createdAt = providerImplementation.CreatedAt.AddTicks(1);
            Context.DocumentEdges.Add(new DocumentEdge(
                optionalEdgeId,
                providerImplementation.Id,
                providerImplementation.RepositoryId,
                providerImplementation.ScanRunId,
                providerXml.Id,
                providerXml.RepositoryId,
                providerXml.ScanRunId,
                DocumentEdgeType.References,
                createdAt));
            Context.FlowTraceSteps.Add(new FlowTraceStep(
                Guid.NewGuid(), trace.Id, 6,
                providerImplementation.Id, providerImplementation.RepositoryId,
                providerImplementation.ScanRunId, optionalEdgeId,
                FlowTraceStepKind.Relationship,
                "Same-Repository supporting relationship", createdAt));
            Context.FlowTraceSteps.Add(new FlowTraceStep(
                Guid.NewGuid(), trace.Id, 7,
                providerXml.Id, providerXml.RepositoryId, providerXml.ScanRunId,
                null, FlowTraceStepKind.Document, "Supporting Document", createdAt));
            string membershipFingerprint = PreparedPass2FlowPlanner.ComputeMembershipFingerprint(
                Group.ConsumerDocumentVersionId!.Value,
                Group.ExactResolutionEdgeId!.Value,
                ProviderXmlDocumentVersionId,
                implementationEdgeId,
                ProviderImplementationDocumentVersionId,
                [(optionalEdgeId, providerXml.Id)]);
            string planFingerprint = PreparedPass2FlowPlanner.ComputePlanFingerprint(
                Decision.Id, Decision.EvidenceFingerprint, "Complete", membershipFingerprint,
                Contributions);
            FlowGroup persistedGroup = await Context.FlowGroups
                .SingleAsync(value => value.Id == Group.Id);
            SetProperty(persistedGroup, nameof(FlowGroup.MembershipFingerprint), membershipFingerprint);
            SetProperty(persistedGroup, nameof(FlowGroup.PlanFingerprint), planFingerprint);
            SetProperty(Group, nameof(FlowGroup.MembershipFingerprint), membershipFingerprint);
            SetProperty(Group, nameof(FlowGroup.PlanFingerprint), planFingerprint);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        public async Task MutateEdgeAuthorityAsync(string mutation)
        {
            FlowTrace trace = await Context.FlowTraces
                .SingleAsync(value => value.FlowGroupId == Group.Id);
            FlowTraceStep implementationStep = await Context.FlowTraceSteps
                .SingleAsync(value => value.FlowTraceId == trace.Id && value.StepOrder == 4);
            DocumentEdge edge = await Context.DocumentEdges
                .SingleAsync(value => value.Id == implementationStep.EdgeId);
            if (mutation.StartsWith("optional-edge-", StringComparison.Ordinal))
            {
                FlowTraceStep optionalStep = await Context.FlowTraceSteps
                    .SingleAsync(value => value.FlowTraceId == trace.Id && value.StepOrder == 6);
                edge = await Context.DocumentEdges.SingleAsync(value => value.Id == optionalStep.EdgeId);
            }

            DocumentVersion source = await Context.DocumentVersions
                .SingleAsync(value => value.Id == edge.SourceDocumentVersionId);
            DocumentVersion target = await Context.DocumentVersions
                .SingleAsync(value => value.Id == edge.TargetDocumentVersionId);
            bool foreignKeyBypass = false;
            string? bypassProperty = null;
            bool assertRepositoryLocalityMutation = false;
            bool assertScanLocalityMutation = false;
            switch (mutation)
            {
                case "provider-implementation-edge-type":
                case "optional-edge-type":
                    SetProperty(edge, nameof(DocumentEdge.EdgeType), DocumentEdgeType.GeneratedFrom);
                    break;
                case "provider-implementation-edge-source":
                    SetProperty(edge, nameof(DocumentEdge.SourceDocumentVersionId), target.Id);
                    SetProperty(edge, nameof(DocumentEdge.SourceRepositoryId), target.RepositoryId);
                    SetProperty(edge, nameof(DocumentEdge.SourceScanRunId), target.ScanRunId);
                    break;
                case "optional-edge-source":
                    SetProperty(edge, nameof(DocumentEdge.SourceDocumentVersionId), target.Id);
                    SetProperty(edge, nameof(DocumentEdge.SourceRepositoryId), target.RepositoryId);
                    SetProperty(edge, nameof(DocumentEdge.SourceScanRunId), target.ScanRunId);
                    break;
                case "provider-implementation-edge-target":
                    SetProperty(edge, nameof(DocumentEdge.TargetDocumentVersionId), ConsumerDocumentVersionId);
                    SetProperty(edge, nameof(DocumentEdge.TargetRepositoryId), Group.RepositoryId);
                    SetProperty(edge, nameof(DocumentEdge.TargetScanRunId), Group.ScanRunId);
                    break;
                case "optional-edge-target":
                    SetProperty(edge, nameof(DocumentEdge.TargetDocumentVersionId), ConsumerDocumentVersionId);
                    SetProperty(edge, nameof(DocumentEdge.TargetRepositoryId), Group.RepositoryId);
                    SetProperty(edge, nameof(DocumentEdge.TargetScanRunId), Group.ScanRunId);
                    break;
                case "provider-implementation-edge-repository":
                case "optional-edge-repository":
                    SetProperty(edge, nameof(DocumentEdge.SourceRepositoryId), Group.RepositoryId);
                    foreignKeyBypass = true;
                    bypassProperty = nameof(DocumentEdge.SourceRepositoryId);
                    break;
                case "provider-implementation-edge-scan-run":
                case "optional-edge-scan-run":
                    SetProperty(edge, nameof(DocumentEdge.SourceScanRunId), Group.ScanRunId);
                    foreignKeyBypass = true;
                    bypassProperty = nameof(DocumentEdge.SourceScanRunId);
                    break;
                case "provider-implementation-edge-provider-repository-locality":
                    Assert.NotEqual(source.RepositoryId, Group.RepositoryId);
                    SetProperty(edge, nameof(DocumentEdge.TargetRepositoryId), Group.RepositoryId);
                    foreignKeyBypass = true;
                    bypassProperty = nameof(DocumentEdge.TargetRepositoryId);
                    assertRepositoryLocalityMutation = true;
                    break;
                case "provider-implementation-edge-provider-scan-locality":
                    Assert.NotEqual(source.ScanRunId, Group.ScanRunId);
                    SetProperty(edge, nameof(DocumentEdge.TargetScanRunId), Group.ScanRunId);
                    foreignKeyBypass = true;
                    bypassProperty = nameof(DocumentEdge.TargetScanRunId);
                    assertScanLocalityMutation = true;
                    break;
                case "provider-implementation-edge-direction":
                    SwapEdgeEndpoints(edge, source, target);
                    break;
                case "optional-edge-direction":
                    SetProperty(edge, nameof(DocumentEdge.SourceDocumentVersionId), target.Id);
                    SetProperty(edge, nameof(DocumentEdge.TargetDocumentVersionId), ConsumerDocumentVersionId);
                    SetProperty(edge, nameof(DocumentEdge.SourceRepositoryId), target.RepositoryId);
                    SetProperty(edge, nameof(DocumentEdge.TargetRepositoryId), Group.RepositoryId);
                    SetProperty(edge, nameof(DocumentEdge.SourceScanRunId), target.ScanRunId);
                    SetProperty(edge, nameof(DocumentEdge.TargetScanRunId), Group.ScanRunId);
                    break;
                default:
                    throw new Xunit.Sdk.XunitException($"Unknown edge mutation '{mutation}'.");
            }

            if (!foreignKeyBypass)
            {
                await Context.SaveChangesAsync();
                if (assertRepositoryLocalityMutation)
                {
                    Assert.Equal(Group.RepositoryId, await Context.DocumentEdges.AsNoTracking()
                        .Where(value => value.Id == edge.Id)
                        .Select(value => value.TargetRepositoryId).SingleAsync());
                }

                if (assertScanLocalityMutation)
                {
                    Assert.Equal(Group.ScanRunId, await Context.DocumentEdges.AsNoTracking()
                        .Where(value => value.Id == edge.Id)
                        .Select(value => value.TargetScanRunId).SingleAsync());
                }

                Context.ChangeTracker.Clear();
                return;
            }

            await Context.Database.OpenConnectionAsync();
            try
            {
                await using (System.Data.Common.DbCommand pragma =
                    Context.Database.GetDbConnection().CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys = OFF";
                    await pragma.ExecuteNonQueryAsync();
                }
                switch (bypassProperty)
                {
                    case nameof(DocumentEdge.SourceRepositoryId):
                        await Context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE \"DocumentEdges\" SET \"SourceRepositoryId\" = {Group.RepositoryId} WHERE \"Id\" = {edge.Id}");
                        break;
                    case nameof(DocumentEdge.SourceScanRunId):
                        await Context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE \"DocumentEdges\" SET \"SourceScanRunId\" = {Group.ScanRunId} WHERE \"Id\" = {edge.Id}");
                        break;
                    case nameof(DocumentEdge.TargetRepositoryId):
                        await Context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE \"DocumentEdges\" SET \"TargetRepositoryId\" = {Group.RepositoryId} WHERE \"Id\" = {edge.Id}");
                        break;
                    case nameof(DocumentEdge.TargetScanRunId):
                        await Context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE \"DocumentEdges\" SET \"TargetScanRunId\" = {Group.ScanRunId} WHERE \"Id\" = {edge.Id}");
                        break;
                    default:
                        throw new Xunit.Sdk.XunitException("Unknown edge bypass mutation.");
                }
                await using (System.Data.Common.DbCommand pragma =
                    Context.Database.GetDbConnection().CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys = ON";
                    await pragma.ExecuteNonQueryAsync();
                }
                if (assertRepositoryLocalityMutation)
                {
                    Assert.Equal(Group.RepositoryId, await Context.DocumentEdges.AsNoTracking()
                        .Where(value => value.Id == edge.Id)
                        .Select(value => value.TargetRepositoryId).SingleAsync());
                }

                if (assertScanLocalityMutation)
                {
                    Assert.Equal(Group.ScanRunId, await Context.DocumentEdges.AsNoTracking()
                        .Where(value => value.Id == edge.Id)
                        .Select(value => value.TargetScanRunId).SingleAsync());
                }
            }
            finally
            {
                await Context.Database.CloseConnectionAsync();
            }
            Context.ChangeTracker.Clear();
        }

        public async Task<string> DurableMutationSnapshotAsync()
        {
            var documents = await Context.DocumentVersions.AsNoTracking()
                .OrderBy(value => value.Id).Select(value => new
                {
                    value.Id, value.SupersededById, value.ContentFingerprint,
                }).ToArrayAsync();
            var edges = await Context.DocumentEdges.AsNoTracking()
                .OrderBy(value => value.Id).Select(value => new
                {
                    value.Id, value.SourceDocumentVersionId, value.SourceRepositoryId,
                    value.SourceScanRunId, value.TargetDocumentVersionId,
                    value.TargetRepositoryId, value.TargetScanRunId, value.EdgeType,
                }).ToArrayAsync();
            FlowGroup group = await Context.FlowGroups.AsNoTracking()
                .SingleAsync(value => value.Id == Group.Id);
            Pass2JobExecution job = await Context.Pass2JobExecutions.AsNoTracking()
                .SingleAsync(value => value.Id == Job.Id);
            Pass2Execution execution = await Context.Pass2Executions.AsNoTracking()
                .SingleAsync(value => value.Id == Execution.Id);
            var traces = await Context.FlowTraceSteps.AsNoTracking()
                .Where(value => value.FlowTraceId == traceIdForSnapshot)
                .OrderBy(value => value.StepOrder).Select(value => new
                {
                    value.Id, value.StepOrder, value.DocumentVersionId, value.EdgeId,
                    value.Description,
                }).ToArrayAsync();
            return JsonSerializer.Serialize(new
            {
                documents, edges, traces,
                GroupState = group.State, group.BusinessLogicDocumentVersionId,
                group.PlanFingerprint, group.MembershipFingerprint,
                JobState = job.State, job.AttemptCount, job.CommittedDocumentVersionId,
                ExecutionState = execution.State, execution.PublishedCount, execution.FailedCount,
                execution.TerminalIncompleteCount, execution.TerminalBoundExcludedCount,
                execution.TerminalOmissionCount, execution.StateVersion,
            });
        }

        private Guid traceIdForSnapshot => Context.FlowTraces
            .Where(value => value.FlowGroupId == Group.Id)
            .Select(value => value.Id).Single();

        private static void SwapEdgeEndpoints(
            DocumentEdge edge,
            DocumentVersion source,
            DocumentVersion target)
        {
            SetProperty(edge, nameof(DocumentEdge.SourceDocumentVersionId), target.Id);
            SetProperty(edge, nameof(DocumentEdge.TargetDocumentVersionId), source.Id);
            SetProperty(edge, nameof(DocumentEdge.SourceRepositoryId), target.RepositoryId);
            SetProperty(edge, nameof(DocumentEdge.TargetRepositoryId), source.RepositoryId);
            SetProperty(edge, nameof(DocumentEdge.SourceScanRunId), target.ScanRunId);
            SetProperty(edge, nameof(DocumentEdge.TargetScanRunId), source.ScanRunId);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await PlanningHarness.DisposeAsync();
        }

    private static string Hash(string value) => Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string ProviderEvidenceFingerprint(
        IEnumerable<ExactResolutionProviderEvidenceContribution> contributions) =>
        ProviderEvidenceFingerprint(contributions.Select(value => value.EvidenceFingerprint));

    private static string ProviderEvidenceFingerprint(IEnumerable<string> evidenceFingerprints) =>
        Hash(string.Join('\n', evidenceFingerprints));

        private static string RetrievalDescriptorFingerprint(
            Pass2CodeLogicRetrievalItemKind kind,
            Guid documentVersionId,
            Guid? sectionId = null,
            Guid? sourceAnchorId = null,
            Guid? edgeId = null,
            long? byteStart = null,
            long? byteEnd = null) => Hash(string.Join(
                '\n',
                "pass2-retrieval-descriptor/1.0",
                kind.ToString(),
                documentVersionId.ToString("D"),
                sectionId?.ToString("D") ?? string.Empty,
                sourceAnchorId?.ToString("D") ?? string.Empty,
                edgeId?.ToString("D") ?? string.Empty,
                byteStart?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                byteEnd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));

        private static string RetrievalEvidenceFingerprint(
            Guid id,
            Pass2CodeLogicRetrievalItemKind kind,
            Guid documentVersionId,
            int returnedUtf8Bytes,
            Guid? sectionId = null,
            Guid? sourceAnchorId = null,
            Guid? edgeId = null,
            long? byteStart = null,
            long? byteEnd = null) => Hash(string.Join(
                '\n',
                "pass2-retrieval-result/1.0",
                id.ToString("D"),
                kind.ToString(),
                documentVersionId.ToString("D"),
                sectionId?.ToString("D") ?? string.Empty,
                sourceAnchorId?.ToString("D") ?? string.Empty,
                edgeId?.ToString("D") ?? string.Empty,
                byteStart?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                byteEnd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                returnedUtf8Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    private sealed class PublicationRelationshipMutationInterceptor : DbCommandInterceptor
    {
        private Guid _edgeId;
        private int _armed;
        private int _mutationStarted;

        public bool MutationApplied { get; private set; }

        public void Arm(Guid edgeId)
        {
            Assert.NotEqual(Guid.Empty, edgeId);
            _edgeId = edgeId;
            Volatile.Write(ref _armed, 1);
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _armed) != 0 &&
                Volatile.Read(ref _mutationStarted) == 0 &&
                command.CommandText.Contains("FROM \"FlowTraces\"", StringComparison.Ordinal) &&
                Interlocked.CompareExchange(ref _mutationStarted, 1, 0) == 0)
            {
                await using DbCommand mutation = command.Connection!.CreateCommand();
                mutation.Transaction = command.Transaction;
                mutation.CommandText = "UPDATE \"DocumentEdges\" SET \"EdgeType\" = 'GeneratedFrom' WHERE \"Id\" = $edgeId";
                DbParameter parameter = mutation.CreateParameter();
                parameter.ParameterName = "$edgeId";
                parameter.Value = _edgeId;
                mutation.Parameters.Add(parameter);
                int affected = await mutation.ExecuteNonQueryAsync(cancellationToken);
                Assert.Equal(1, affected);
                MutationApplied = true;
            }

            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class AllowAccessVerifier : IRepositoryAuthorizationVerifier
    {
        public ValueTask<bool> HasCurrentAccessAsync(IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(requiredRepositoryIds.Count == 2);
    }
}
