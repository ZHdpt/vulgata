using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class ChatEvidenceCurrentnessTests
{
    [Fact]
    public async Task ValidateCurrentAsync_LegacyExactEvidenceWithoutAcceptedV2Decision_ReturnsMissingOrStaleAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync(
            includeAcceptedV2Authority: true);
        EvidencePacket packet = await fixture.RetrieveAsync();
        await fixture.RemoveV2AuthorityAsync();

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    CurrentnessFixture.CreateValidationRequest(packet),
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
    }

    [Theory]
    [InlineData("Incomplete")]
    [InlineData("Complete")]
    [InlineData("Failed")]
    public async Task RetrieveAsync_NonPublishedFlowGroup_ReturnsIncompleteOrStaleAsync(
        string state)
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        await fixture.SetFlowGroupStateAsync(state);

        ChatEvidenceResult result = await fixture.RetrieveAsyncResult();

        Assert.Equal(ChatEvidenceResultKind.IncompleteOrStale, result.Kind);
        Assert.Null(result.Packet);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task ValidateCurrentAsync_AdditionalFlowGroupMismatchedDecision_ReturnsMissingOrStaleAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();
        EvidenceFlowGroupAuthority existingGroup = Assert.Single(packet.FlowGroups);
        EvidenceFlowGroupAuthority additionalGroup = existingGroup with
        {
            FlowGroupId = Guid.Parse("11510000-0000-0000-0000-000000010311"),
            ExactResolutionDecisionId = Guid.Parse("11510000-0000-0000-0000-000000010314"),
        };
        ChatCurrentnessValidationRequest original =
            CurrentnessFixture.CreateValidationRequest(packet);
        ChatCurrentnessValidationRequest request = ChatCurrentnessValidationRequest.Create(
            original.RequestId,
            original.EvidenceFingerprint,
            original.SystemId,
            original.RepositoryIds,
            original.Documents,
            original.Sections,
            original.SourceAnchors,
            original.Relationships,
            [.. original.FlowGroups, additionalGroup]);

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    request,
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task ValidateCurrentAsync_ExactCurrentAuthorizedSupport_ReturnsCurrentAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();
        var verifier = new RecordingAuthorizationVerifier(true, true);
        var scope = new RepositoryAccessScope(fixture.RepositoryIds, verifier);
        var validator = new ChatEvidenceCurrentnessValidator(fixture.Context);

        ChatCurrentnessValidationOutcome outcome = await validator.ValidateCurrentAsync(
            CurrentnessFixture.CreateValidationRequest(packet),
            scope);

        Assert.Equal(ChatCurrentnessValidationOutcome.Current, outcome);
        Assert.Equal(2, verifier.CallCount);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task ValidateCurrentAsync_TwoDistinctClaimSpansInSameSection_ReturnsCurrentAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();
        ChatCurrentnessValidationRequest original =
            CurrentnessFixture.CreateValidationRequest(packet);
        ChatCurrentSection first = original.Sections[0];
        string content = packet.Sections.Single(section => section.StableSectionId == first.StableSectionId).ContentMarkdown;
        int secondSpanLength = Math.Min(5, content.Length);
        var secondSpan = new ChatCurrentSection(
            first.StableSectionId,
            first.DocumentVersionId,
            0,
            secondSpanLength,
            CurrentnessFixture.Hash(content[..secondSpanLength]));
        ChatCurrentnessValidationRequest request = ChatCurrentnessValidationRequest.Create(
            original.RequestId,
            original.EvidenceFingerprint,
            original.SystemId,
            original.RepositoryIds,
            original.Documents,
            [.. original.Sections, secondSpan],
            original.SourceAnchors,
            original.Relationships,
            original.FlowGroups);

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    request,
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.Current, outcome);
    }

    [Fact]
    public async Task ValidateCurrentAsync_CommittedSupportMutationAfterRetrieval_ReturnsMissingOrStaleAsync()
    {
        await AssertMissingAfterProviderWindowMutationAsync(fixture => fixture.CommitSuccessorAsync());
        await AssertMissingAfterProviderWindowMutationAsync(fixture => fixture.CommitSectionTextChangeAsync());
        await AssertMissingAfterProviderWindowMutationAsync(fixture => fixture.CommitAnchorLossAsync());
        await AssertMissingAfterProviderWindowMutationAsync(fixture => fixture.CommitAnchorRangeChangeAsync());
        await AssertMissingAfterProviderWindowMutationAsync(fixture => fixture.CommitGeneratedFromLossAsync());
    }

    [Theory]
    [InlineData("generated-from-source-scan-run")]
    [InlineData("generated-from-target-scan-run")]
    [InlineData("generated-from-source-profiled-revision")]
    [InlineData("generated-from-target-profiled-revision")]
    public async Task ValidateCurrentAsync_GeneratedFromEndpointIdentityMutation_ReturnsMissingOrStaleAsync(
        string mutation)
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();
        Assert.Contains(
            packet.Relationships,
            relationship => relationship.RelationshipType == ChatRelationshipType.GeneratedFrom);

        await fixture.CommitGeneratedFromIdentityMutationAsync(mutation);

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    // Isolate the GENERATED_FROM render-time proof from the
                    // unrelated exact REFERENCES proof. The endpoint profile
                    // mutations below must be attributable to this edge.
                    CurrentnessFixture.CreateValidationRequest(
                        packet,
                        generatedFromOnly: true),
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task RetrieveAsync_SharedEdgeWithTwoCurrentDecisions_PreservesDecisionSetAndOwnedFlowGroupsAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        await fixture.AddSecondCurrentDecisionAndFlowGroupAsync();

        ChatEvidenceResult result = await fixture.RetrieveAsyncResult();

        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        EvidenceRelationship relationship = Assert.Single(
            packet.Relationships,
            item => item.EdgeId == fixture.ReferencesEdgeId);
        Assert.NotNull(relationship.ExactDecisionId);
        Assert.Equal(2, relationship.ExactDecisionCount);
        Assert.NotNull(relationship.ExactDecisionSetFingerprint);
        Assert.Equal(2, packet.FlowGroups.Count);
        Assert.Equal(
            relationship.ExactDecisionId,
            packet.FlowGroups[0].ExactResolutionDecisionId);
        Assert.NotEqual(
            packet.FlowGroups[0].ExactResolutionDecisionId,
            packet.FlowGroups[1].ExactResolutionDecisionId);
    }

    [Fact]
    public async Task ValidateCurrentAsync_SharedEdgeDecisionSetMutationAfterSelection_ReturnsMissingOrStaleAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();
        await fixture.AddSecondCurrentDecisionAndFlowGroupAsync();

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    CurrentnessFixture.CreateValidationRequest(packet),
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
    }

    [Theory]
    [InlineData("absent", ChatCurrentnessValidationOutcome.Current)]
    [InlineData("present-valid", ChatCurrentnessValidationOutcome.Current)]
    [InlineData("present-inconsistent", ChatCurrentnessValidationOutcome.MissingOrStale)]
    [InlineData("selected-required", ChatCurrentnessValidationOutcome.MissingOrStale)]
    public async Task ValidateCurrentAsync_RelationshipAndFlowGroupStateTable_UsesClosedOutcomesAsync(
        string state,
        ChatCurrentnessValidationOutcome expectedOutcome)
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();
        if (state == "selected-required")
        {
            await fixture.CommitFlowGroupStateChangeAsync();
        }

        EvidenceFlowGroupAuthority[] flowGroups = state switch
        {
            "absent" => [],
            "present-inconsistent" =>
            [
                .. packet.FlowGroups,
                packet.FlowGroups[0] with
                {
                    FlowGroupId = CurrentnessFixture.Id(10_311),
                    ExactResolutionDecisionId = CurrentnessFixture.Id(10_314),
                },
            ],
            _ => packet.FlowGroups.ToArray(),
        };
        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    CurrentnessFixture.CreateValidationRequest(
                        packet,
                        flowGroups: flowGroups,
                        omitFlowGroupBindings: state == "absent"),
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(expectedOutcome, outcome);
    }

    [Fact]
    public async Task ValidateCurrentAsync_SameRepositoryReferencesWithoutExactAuthority_ReturnsCurrentAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();
        await fixture.AddSameRepositoryReferenceAsync();

        ChatCurrentnessValidationRequest original =
            CurrentnessFixture.CreateValidationRequest(packet);
        EvidenceDocument source = packet.Documents.Single(document =>
            document.DocumentVersionId == fixture.BusinessDocumentVersionId);
        EvidenceDocument target = packet.Documents.Single(document =>
            document.DocumentVersionId == ExactCrossRepositoryTestValues.ConsumerDocumentVersionId);
        ChatCurrentnessValidationRequest? request = null;
        Exception? constructionFailure = Record.Exception(() => request =
            ChatCurrentnessValidationRequest.Create(
                original.RequestId,
                original.EvidenceFingerprint,
                original.SystemId,
                original.RepositoryIds,
                original.Documents,
                original.Sections,
                original.SourceAnchors,
                [
                    .. original.Relationships,
                    new ChatCurrentRelationship(
                        fixture.SameRepositoryReferenceEdgeId,
                        ChatRelationshipType.References,
                        source.DocumentVersionId,
                        target.DocumentVersionId,
                        source.RepositoryId,
                        target.RepositoryId,
                        1,
                        null,
                        null,
                        null),
                ],
                original.FlowGroups));
        Assert.Null(constructionFailure);
        Assert.NotNull(request);

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    request!,
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.Current, outcome);
    }

    [Theory]
    [InlineData("scan-run")]
    [InlineData("profiled-revision")]
    public async Task ValidateCurrentAsync_SourceAnchorIdentityMutation_ReturnsMissingOrStaleAsync(
        string mutation)
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();
        await fixture.CommitSourceAnchorIdentityMutationAsync(mutation);

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    CurrentnessFixture.CreateValidationRequest(packet),
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
    }

    [Theory]
    [InlineData("flow-group-identity")]
    [InlineData("flow-group-state")]
    [InlineData("flow-group-document-binding")]
    [InlineData("flow-group-decision-binding")]
    [InlineData("flow-group-edge-binding")]
    [InlineData("flow-trace-identity")]
    [InlineData("flow-trace-order")]
    [InlineData("flow-trace-step-identity")]
    [InlineData("flow-trace-step-order")]
    [InlineData("flow-trace-step-content")]
    [InlineData("flow-trace-step-document-binding")]
    [InlineData("flow-trace-step-edge-binding")]
    [InlineData("flow-trace-step-decision-binding")]
    // The public seam must receive the packet-selected Flow Group/trace/step snapshot.
    public async Task ValidateCurrentAsync_PublishedFlowGroupMutationAfterRetrieval_ReturnsMissingOrStaleAsync(
        string mutation)
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();
        Assert.Equal([1, 2], packet.FlowEvidence.Select(step => step.StepOrder));

        await fixture.CommitFlowMutationAsync(mutation);

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    CurrentnessFixture.CreateValidationRequest(packet),
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task ValidateCurrentAsync_PacketDocumentTitleMutation_ReturnsMissingOrStaleAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();

        await fixture.CommitDocumentTitleMutationAsync();

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    CurrentnessFixture.CreateValidationRequest(packet),
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    [Theory]
    [InlineData("flow-group-system")]
    [InlineData("flow-group-repository")]
    [InlineData("flow-group-scan-run")]
    public async Task ValidateCurrentAsync_FlowGroupOwnerBindingMutation_ReturnsMissingOrStaleAsync(
        string mutation)
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();

        await fixture.CommitFlowGroupOwnerMutationAsync(mutation);

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    CurrentnessFixture.CreateValidationRequest(packet),
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task ValidateCurrentAsync_SelectedScanRunNotCompleted_ReturnsMissingOrStaleAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();

        await fixture.CommitSelectedScanRunStatusMutationAsync();

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    CurrentnessFixture.CreateValidationRequest(packet),
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task ValidateCurrentAsync_SelectedProviderScanRunProfiledRevisionDrift_ReturnsMissingOrStaleAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();

        await fixture.CommitSelectedScanRunProfiledRevisionMutationAsync();

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    CurrentnessFixture.CreateValidationRequest(packet),
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task ValidateCurrentAsync_FlowStepDocumentProfiledRevisionDrift_ReturnsMissingOrStaleAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();
        Assert.Contains(
            packet.FlowGroups.SelectMany(group => group.Steps),
            step => step.DocumentVersionId == ExactCrossRepositoryTestValues.ProviderDocumentVersionId);

        await fixture.CommitFlowStepDocumentProfiledRevisionMutationAsync();

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    CurrentnessFixture.CreateValidationRequest(packet),
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task ValidateCurrentAsync_CrossSystemExactDecisionRow_ReturnsMissingOrStaleAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();

        await fixture.CommitCrossSystemDecisionMutationAsync();

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    CurrentnessFixture.CreateValidationRequest(packet),
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task ValidateCurrentAsync_ExactDecisionCanonicalFingerprintMutation_ReturnsMissingOrStaleAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();

        await fixture.CommitExactDecisionCanonicalFingerprintMutationAsync();

        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    CurrentnessFixture.CreateValidationRequest(packet),
                    new RepositoryAccessScope(
                        fixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task ValidateCurrentAsync_LegacySupportMutationsAfterRetrieval_DoNotAffectAcceptedV2CurrentnessAsync()
    {
        await AssertCurrentAfterProviderWindowMutationAsync(fixture => fixture.CommitReferencesEvidenceLossAsync());
        await AssertCurrentAfterProviderWindowMutationAsync(fixture => fixture.CommitReferencesProfileChangeAsync());
        await AssertCurrentAfterProviderWindowMutationAsync(fixture => fixture.CommitReferencesDirectionChangeAsync());
    }

    [Fact]
    public async Task ValidateCurrentAsync_AuthorizationCancellationOrDatabaseFailure_PreservesClosedSemanticsAsync()
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();
        ChatCurrentnessValidationRequest request =
            CurrentnessFixture.CreateValidationRequest(packet);
        var validator = new ChatEvidenceCurrentnessValidator(fixture.Context);

        ChatCurrentnessValidationOutcome denied = await validator.ValidateCurrentAsync(
            request,
            new RepositoryAccessScope(
                fixture.RepositoryIds,
                new RecordingAuthorizationVerifier(false)));
        ChatCurrentnessValidationOutcome revoked = await validator.ValidateCurrentAsync(
            request,
            new RepositoryAccessScope(
                fixture.RepositoryIds,
                new RecordingAuthorizationVerifier(true, false)));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            validator.ValidateCurrentAsync(
                request,
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true)),
                cancellation.Token));

        await using VulgataDbContext failingContext = fixture.CreateContext(
            new ThrowingReadInterceptor());
        var databaseFailure = new ChatEvidenceCurrentnessValidator(failingContext);
        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() => databaseFailure.ValidateCurrentAsync(
            request,
            new RepositoryAccessScope(
                fixture.RepositoryIds,
                new RecordingAuthorizationVerifier(true, true))));

        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, denied);
        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, revoked);
        Assert.Equal(ThrowingReadInterceptor.Message, failure.Message);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
        Assert.Null(failingContext.Database.CurrentTransaction);
    }

    private static async Task AssertMissingAfterProviderWindowMutationAsync(
        Func<CurrentnessFixture, Task> commitMutationAsync)
    {
        await AssertAfterProviderWindowMutationAsync(
            commitMutationAsync,
            ChatCurrentnessValidationOutcome.MissingOrStale);
    }

    private static async Task AssertCurrentAfterProviderWindowMutationAsync(
        Func<CurrentnessFixture, Task> commitMutationAsync)
    {
        await AssertAfterProviderWindowMutationAsync(
            commitMutationAsync,
            ChatCurrentnessValidationOutcome.Current);
    }

    private static async Task AssertAfterProviderWindowMutationAsync(
        Func<CurrentnessFixture, Task> commitMutationAsync,
        ChatCurrentnessValidationOutcome expectedOutcome)
    {
        await using CurrentnessFixture fixture = await CurrentnessFixture.CreateAsync();
        EvidencePacket packet = await fixture.RetrieveAsync();
        var barrier = new ProviderMutationBarrier();
        Task providerPhase = barrier.RunProviderPhaseAsync();
        await barrier.ProviderStarted;

        await commitMutationAsync(fixture);
        barrier.RecordCommittedMutation();
        barrier.ReleaseProvider();
        await providerPhase;

        var scope = new RepositoryAccessScope(
            fixture.RepositoryIds,
            new RecordingAuthorizationVerifier(true, true));
        ChatCurrentnessValidationOutcome outcome =
            await new ChatEvidenceCurrentnessValidator(fixture.Context)
                .ValidateCurrentAsync(
                    CurrentnessFixture.CreateValidationRequest(packet),
                    scope);

        Assert.True(barrier.MutationCommittedBeforeProviderRelease);
        Assert.Equal(expectedOutcome, outcome);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    private sealed class ProviderMutationBarrier
    {
        private readonly TaskCompletionSource _providerStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _providerReleased =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private bool _mutationCommitted;

        internal Task ProviderStarted => _providerStarted.Task;

        internal bool MutationCommittedBeforeProviderRelease { get; private set; }

        internal async Task RunProviderPhaseAsync()
        {
            _providerStarted.SetResult();
            await _providerReleased.Task;
        }

        internal void RecordCommittedMutation() => _mutationCommitted = true;

        internal void ReleaseProvider()
        {
            MutationCommittedBeforeProviderRelease = _mutationCommitted;
            _providerReleased.SetResult();
        }
    }

    private sealed class ThrowingReadInterceptor : DbCommandInterceptor
    {
        internal const string Message = "currentness database read failed";

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Message);
    }

    private sealed class RecordingAuthorizationVerifier(params bool[] decisions) :
        IRepositoryAuthorizationVerifier
    {
        private readonly Queue<bool> _decisions = new(decisions);

        internal int CallCount { get; private set; }

        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(_decisions.Count > 0 && _decisions.Dequeue());
        }
    }

    private sealed class CurrentnessFixture(
        ExactCrossRepositoryTestFixture exactFixture,
        VulgataDbContext context,
        Guid referencesEdgeId) : IAsyncDisposable
    {
        internal static string ProfileFingerprint =>
            new ExactResolutionProfileCatalog().Load().ProfileFingerprint;

        private static readonly Guid _businessLogicalDocumentId = Id(10_001);
        private static readonly Guid _businessDocumentVersionId = Id(10_002);
        private static readonly Guid _businessSourceAnchorId = Id(10_003);
        private Guid _referencesEdgeId = referencesEdgeId;
        private static readonly Guid _generatedFromEdgeId = Id(10_005);
        private static readonly Guid _sameRepositoryReferenceEdgeId = Id(10_006);

        internal VulgataDbContext Context { get; } = context;

        internal Guid BusinessDocumentVersionId => _businessDocumentVersionId;

        internal Guid SameRepositoryReferenceEdgeId => _sameRepositoryReferenceEdgeId;

        internal Guid ReferencesEdgeId => _referencesEdgeId;

        internal VulgataDbContext CreateContext(params IInterceptor[] interceptors) =>
            exactFixture.Database.CreateContext(null, interceptors);

        internal IReadOnlyCollection<Guid> RepositoryIds { get; } =
        [
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
        ];

        internal async Task<EvidencePacket> RetrieveAsync()
        {
            ChatEvidenceResult result = await RetrieveAsyncResult();
            return Assert.IsType<EvidencePacket>(result.Packet);
        }

        internal async Task<ChatEvidenceResult> RetrieveAsyncResult()
        {
            var scope = new RepositoryAccessScope(RepositoryIds);
            ChatQuestionRequest questionRequest = ChatQuestionRequest.Create(
                "currentness-request",
                ChatMode.Business,
                ExactCrossRepositoryTestValues.SystemId,
                RepositoryIds,
                "客户会话如何跨仓库更新？");
            ChatConversationScope conversationScope = ChatConversationScope.Create(
                questionRequest.Mode,
                questionRequest.SystemId,
                questionRequest.RepositoryIds);
            ChatRetrievalRequest request = ChatRetrievalRequest.Create(
                questionRequest,
                ChatConversationState.Empty(conversationScope),
                DateTimeOffset.UtcNow.AddMinutes(1));
            return await new ChatEvidenceRetriever(Context)
                .RetrieveAsync(request, scope);
        }

        internal async Task SetFlowGroupStateAsync(string state)
        {
            int affectedRows = await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"FlowGroups\" SET \"State\" = {state} WHERE \"Id\" = {Id(10_301)}");
            Assert.Equal(1, affectedRows);
            Context.ChangeTracker.Clear();
        }

        internal async Task AddSecondCurrentDecisionAndFlowGroupAsync()
        {
            ExactCrossRepositoryEdgeEvidence evidence = await Context
                .ExactCrossRepositoryEdgeEvidence
                .AsNoTracking()
                .SingleAsync(value => value.EdgeId == _referencesEdgeId);
            CrossRepositoryResolutionDecision templateDecision = await Context
                .CrossRepositoryResolutionDecisions
                .AsNoTracking()
                .SingleAsync(value => value.DocumentEdgeId == _referencesEdgeId);
            ExactResolutionProviderEvidenceContribution templateContribution = await Context
                .ExactResolutionProviderEvidenceContributions
                .AsNoTracking()
                .SingleAsync(value => value.ResolutionDecisionId == templateDecision.Id);

            const string secondaryOccurrenceId = "shared-edge-secondary";
            ExactCrossRepositoryEdgeEvidence secondaryEvidence = new(
                evidence.EdgeId,
                evidence.ConsumerSourceAnchorId,
                evidence.ConsumerDocumentVersionId,
                evidence.ConsumerRepositoryId,
                evidence.ConsumerScanRunId,
                evidence.ProviderSourceAnchorId,
                evidence.ProviderDocumentVersionId,
                evidence.ProviderRepositoryId,
                evidence.ProviderScanRunId,
                evidence.ConsumerRawIdentifier,
                evidence.ProviderRawIdentifier,
                "cust/updateSession-secondary",
                evidence.Protocol,
                evidence.ResolutionKind,
                evidence.Confidence,
                evidence.ProfileSchemaVersion,
                evidence.ProfileId,
                evidence.ProfileVersion,
                evidence.ProfileFingerprint,
                secondaryOccurrenceId,
                evidence.ConsumerEvidenceLocator,
                evidence.ProviderEvidenceLocator,
                string.Empty,
                evidence.CreatedAt);
            string secondaryFingerprint = ExactResolutionCanonicalAuthority.EvidenceFingerprint(
                secondaryEvidence,
                templateDecision.ConsumerProfiledRevision,
                templateDecision.ProviderProfiledRevision!);
            Guid secondaryDecisionId = ExactResolutionCanonicalAuthority.DecisionId(
                secondaryFingerprint);

            ExactResolutionProviderEvidenceContribution secondaryContribution = NewPrivate<ExactResolutionProviderEvidenceContribution>();
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.ResolutionDecisionId), secondaryDecisionId);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderIdentity), templateContribution.ProviderIdentity);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderDocumentVersionId), templateContribution.ProviderDocumentVersionId);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.RepositoryId), templateContribution.RepositoryId);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.ScanRunId), templateContribution.ScanRunId);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.ProfiledRevision), templateContribution.ProfiledRevision);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.CommunicationClassificationId), templateContribution.CommunicationClassificationId);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.SourceAnchorId), templateContribution.SourceAnchorId);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderRawIdentifier), templateContribution.ProviderRawIdentifier);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.NormalizedEndpointIdentifier), "cust/updateSession-secondary");
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceLocator), templateContribution.EvidenceLocator);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.ClassificationFingerprint), templateContribution.ClassificationFingerprint);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.CatalogRuleIdentity), templateContribution.CatalogRuleIdentity);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.NormalizationRuleIdentity), templateContribution.NormalizationRuleIdentity);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderOrdinal), templateContribution.ProviderOrdinal);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceOrdinal), templateContribution.EvidenceOrdinal + 1);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.CreatedAt), templateContribution.CreatedAt);
            string contributionFingerprint = ExactResolutionCanonicalAuthority.PersistedProviderContributionFingerprint(
                secondaryFingerprint,
                secondaryContribution);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceFingerprint), contributionFingerprint);
            SetPrivate(secondaryContribution, nameof(ExactResolutionProviderEvidenceContribution.Id),
                ExactResolutionCanonicalAuthority.ProviderContributionId(contributionFingerprint));

            CrossRepositoryResolutionDecision secondaryDecision = NewPrivate<CrossRepositoryResolutionDecision>();
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.Id), secondaryDecisionId);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ResolutionSchemaIdentity), templateDecision.ResolutionSchemaIdentity);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.Pass1PairExecutionId), templateDecision.Pass1PairExecutionId);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.CoordinationId), templateDecision.CoordinationId);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.SystemId), templateDecision.SystemId);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ConsumerCandidateId), Id(10_315));
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryIdentity), "shared-edge-secondary-entry");
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryStableKey), new string('2', 64));
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ConsumerDocumentVersionId), templateDecision.ConsumerDocumentVersionId);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ConsumerRepositoryId), templateDecision.ConsumerRepositoryId);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ConsumerScanRunId), templateDecision.ConsumerScanRunId);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ConsumerProfiledRevision), templateDecision.ConsumerProfiledRevision);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.Protocol), templateDecision.Protocol);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.NormalizedEndpointIdentifier), "cust/updateSession-secondary");
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.NormalizationRuleIdentity), templateDecision.NormalizationRuleIdentity);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.Outcome), templateDecision.Outcome);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.OutcomeKind), templateDecision.OutcomeKind);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ReasonCode), templateDecision.ReasonCode);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ProviderIdentity), templateDecision.ProviderIdentity);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ProviderDocumentVersionId), templateDecision.ProviderDocumentVersionId);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ProviderRepositoryId), templateDecision.ProviderRepositoryId);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ProviderScanRunId), templateDecision.ProviderScanRunId);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ProviderProfiledRevision), templateDecision.ProviderProfiledRevision);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.DocumentEdgeId), _referencesEdgeId);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ConsumerEvidenceFingerprint), secondaryFingerprint);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.ProviderEvidenceSetFingerprint),
                ExactResolutionCanonicalAuthority.ProviderEvidenceSetFingerprint([contributionFingerprint]));
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.DecisionFingerprint), secondaryFingerprint);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.EvidenceFingerprint), secondaryFingerprint);
            SetPrivate(secondaryDecision, nameof(CrossRepositoryResolutionDecision.CreatedAt), templateDecision.CreatedAt);

            Context.CrossRepositoryResolutionDecisions.Add(secondaryDecision);
            Context.ExactResolutionProviderEvidenceContributions.Add(secondaryContribution);

            var flowGroup = new FlowGroup(
                Id(10_311),
                _businessDocumentVersionId,
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                ExactCrossRepositoryTestValues.ConsumerScanRunId,
                2,
                "客户会话更新补充流程",
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            Context.Entry(flowGroup).Property(nameof(FlowGroup.SchemaIdentity)).CurrentValue = "endpoint-flow-group/1.0";
            Context.Entry(flowGroup).Property(nameof(FlowGroup.State)).CurrentValue = "Published";
            Context.Entry(flowGroup).Property(nameof(FlowGroup.SystemId)).CurrentValue = ExactCrossRepositoryTestValues.SystemId;
            Context.Entry(flowGroup).Property(nameof(FlowGroup.PlanFingerprint)).CurrentValue = new string('c', 64);
            Context.Entry(flowGroup).Property(nameof(FlowGroup.MembershipFingerprint)).CurrentValue = new string('d', 64);
            Context.Entry(flowGroup).Property(nameof(FlowGroup.ExactResolutionDecisionId)).CurrentValue = secondaryDecisionId;
            Context.Entry(flowGroup).Property(nameof(FlowGroup.ExactResolutionEdgeId)).CurrentValue = _referencesEdgeId;
            var trace = new FlowTrace(
                Id(10_312),
                flowGroup.Id,
                1,
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            trace.Steps.Add(new FlowTraceStep(
                Id(10_313),
                trace.Id,
                1,
                ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                ExactCrossRepositoryTestValues.ConsumerScanRunId,
                _referencesEdgeId,
                FlowTraceStepKind.Relationship,
                "通过第二个精确决策调用提供端。",
                ExactCrossRepositoryTestValues.Pass1CompletedAt,
                secondaryDecisionId));
            flowGroup.Traces.Add(trace);
            Context.FlowGroups.Add(flowGroup);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        private static T NewPrivate<T>() => (T)Activator.CreateInstance(
            typeof(T),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: null,
            culture: null)!;

        private static void SetPrivate<T>(object target, string propertyName, T value) =>
            target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);

        internal async Task AddSameRepositoryReferenceAsync()
        {
            Context.DocumentEdges.Add(new DocumentEdge(
                _sameRepositoryReferenceEdgeId,
                _businessDocumentVersionId,
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                ExactCrossRepositoryTestValues.ConsumerScanRunId,
                ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                ExactCrossRepositoryTestValues.ConsumerScanRunId,
                DocumentEdgeType.References,
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        internal Task CommitFlowGroupIdentityChangeAsync() => CommitSqlAsync(
            $"UPDATE \"FlowGroups\" SET \"SchemaIdentity\" = NULL WHERE \"Id\" = {Id(10_301)}");

        internal Task CommitFlowGroupStateChangeAsync() => CommitSqlAsync(
            $"UPDATE \"FlowGroups\" SET \"State\" = 'Complete' WHERE \"Id\" = {Id(10_301)}");

        internal Task CommitFlowGroupDocumentBindingLossAsync() => CommitSqlAsync(
            $"UPDATE \"FlowGroups\" SET \"BusinessLogicDocumentVersionId\" = NULL WHERE \"Id\" = {Id(10_301)}");

        internal Task CommitFlowGroupDecisionBindingLossAsync() => CommitSqlAsync(
            $"UPDATE \"FlowGroups\" SET \"ExactResolutionDecisionId\" = NULL WHERE \"Id\" = {Id(10_301)}");

        internal Task CommitFlowGroupEdgeBindingLossAsync() => CommitSqlAsync(
            $"UPDATE \"FlowGroups\" SET \"ExactResolutionEdgeId\" = NULL WHERE \"Id\" = {Id(10_301)}");

        internal Task CommitFlowGroupOwnerMutationAsync(string mutation) => mutation switch
        {
            "flow-group-system" => CommitSqlAsync(
                $"UPDATE \"FlowGroups\" SET \"SystemId\" = NULL WHERE \"Id\" = {Id(10_301)}"),
            "flow-group-repository" => CommitSqlWithForeignKeysDisabledAsync(
                $"UPDATE \"FlowGroups\" SET \"RepositoryId\" = {Id(10_999)} WHERE \"Id\" = {Id(10_301)}"),
            "flow-group-scan-run" => CommitSqlWithForeignKeysDisabledAsync(
                $"UPDATE \"FlowGroups\" SET \"ScanRunId\" = {ExactCrossRepositoryTestValues.UnsupportedScanRunId} WHERE \"Id\" = {Id(10_301)}"),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown Flow Group owner mutation."),
        };

        internal async Task CommitFlowTraceLossAsync()
        {
            await CommitSqlAsync(
                $"DELETE FROM \"FlowTraceSteps\" WHERE \"Id\" = {Id(10_303)}");
            await CommitSqlAsync(
                $"DELETE FROM \"FlowTraceSteps\" WHERE \"Id\" = {Id(10_304)}");
            await CommitSqlAsync(
                $"DELETE FROM \"FlowTraces\" WHERE \"Id\" = {Id(10_302)}");
        }

        internal Task CommitFlowTraceOrderChangeAsync() => CommitSqlAsync(
            $"UPDATE \"FlowTraces\" SET \"TraceOrder\" = 2 WHERE \"Id\" = {Id(10_302)}");

        internal Task CommitFlowTraceStepLossAsync() => CommitSqlAsync(
            $"DELETE FROM \"FlowTraceSteps\" WHERE \"Id\" = {Id(10_304)}");

        internal Task CommitFlowTraceStepOrderChangeAsync() => CommitSqlAsync(
            $"UPDATE \"FlowTraceSteps\" SET \"StepOrder\" = 3 WHERE \"Id\" = {Id(10_304)}");

        internal Task CommitFlowTraceStepContentChangeAsync() => CommitSqlAsync(
            $"UPDATE \"FlowTraceSteps\" SET \"Description\" = '篡改后的流程步骤。' WHERE \"Id\" = {Id(10_304)}");

        internal Task CommitFlowTraceStepDocumentBindingChangeAsync() => CommitSqlAsync(
            $"UPDATE \"FlowTraceSteps\" SET \"DocumentVersionId\" = {ExactCrossRepositoryTestValues.ConsumerDocumentVersionId}, \"DocumentRepositoryId\" = {ExactCrossRepositoryTestValues.ConsumerRepositoryId}, \"DocumentScanRunId\" = {ExactCrossRepositoryTestValues.ConsumerScanRunId} WHERE \"Id\" = {Id(10_304)}");

        internal Task CommitFlowTraceStepEdgeBindingLossAsync() => CommitSqlAsync(
            $"UPDATE \"FlowTraceSteps\" SET \"EdgeId\" = NULL WHERE \"Id\" = {Id(10_304)}");

        internal Task CommitFlowTraceStepDecisionBindingLossAsync() => CommitSqlAsync(
            $"UPDATE \"FlowTraceSteps\" SET \"ExactResolutionDecisionId\" = NULL WHERE \"Id\" = {Id(10_304)}");

        internal Task CommitFlowMutationAsync(string mutation) => mutation switch
        {
            "flow-group-identity" => CommitFlowGroupIdentityChangeAsync(),
            "flow-group-state" => CommitFlowGroupStateChangeAsync(),
            "flow-group-document-binding" => CommitFlowGroupDocumentBindingLossAsync(),
            "flow-group-decision-binding" => CommitFlowGroupDecisionBindingLossAsync(),
            "flow-group-edge-binding" => CommitFlowGroupEdgeBindingLossAsync(),
            "flow-trace-identity" => CommitFlowTraceLossAsync(),
            "flow-trace-order" => CommitFlowTraceOrderChangeAsync(),
            "flow-trace-step-identity" => CommitFlowTraceStepLossAsync(),
            "flow-trace-step-order" => CommitFlowTraceStepOrderChangeAsync(),
            "flow-trace-step-content" => CommitFlowTraceStepContentChangeAsync(),
            "flow-trace-step-document-binding" => CommitFlowTraceStepDocumentBindingChangeAsync(),
            "flow-trace-step-edge-binding" => CommitFlowTraceStepEdgeBindingLossAsync(),
            "flow-trace-step-decision-binding" => CommitFlowTraceStepDecisionBindingLossAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown Flow Group mutation."),
        };

        internal async Task RemoveV2AuthorityAsync()
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"FlowGroups\" SET \"ExactResolutionDecisionId\" = NULL, \"ExactResolutionEdgeId\" = NULL WHERE \"ExactResolutionDecisionId\" IN (SELECT \"Id\" FROM \"CrossRepositoryResolutionDecisions\" WHERE \"DocumentEdgeId\" = {_referencesEdgeId})");
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"FlowTraceSteps\" SET \"ExactResolutionDecisionId\" = NULL WHERE \"ExactResolutionDecisionId\" IN (SELECT \"Id\" FROM \"CrossRepositoryResolutionDecisions\" WHERE \"DocumentEdgeId\" = {_referencesEdgeId})");
            // Detach the decision from the REFERENCES Edge. Retain its dependent
            // contribution rows so SQLite's restrictive foreign keys stay valid;
            // a decision without an edge is not accepted v2 authority.
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"CrossRepositoryResolutionDecisions\" SET \"DocumentEdgeId\" = NULL WHERE \"DocumentEdgeId\" = {_referencesEdgeId}");
            Context.ChangeTracker.Clear();
        }

        internal static ChatCurrentnessValidationRequest CreateValidationRequest(
            EvidencePacket packet,
            bool generatedFromOnly = false,
            IReadOnlyCollection<EvidenceFlowGroupAuthority>? flowGroups = null,
            bool omitFlowGroupBindings = false)
        {
            Dictionary<string, EvidenceDocument> documents = packet.Documents.ToDictionary(
                document => document.DocumentHandle,
                StringComparer.Ordinal);
            Dictionary<string, EvidenceAnchor> anchors = packet.SourceAnchors.ToDictionary(
                anchor => anchor.AnchorHandle,
                StringComparer.Ordinal);
            return ChatCurrentnessValidationRequest.Create(
                packet.RequestId,
                packet.Fingerprint,
                packet.SystemId,
                packet.RepositoryIds,
                packet.Documents.Select(document => new ChatCurrentDocument(
                    document.LogicalDocumentId,
                    document.DocumentVersionId,
                    document.Title)).ToArray(),
                packet.Sections.Select(section => new ChatCurrentSection(
                    section.StableSectionId,
                    documents[section.DocumentHandle].DocumentVersionId,
                    0,
                    section.ContentMarkdown.Length,
                    Sha256(section.ContentMarkdown))).ToArray(),
                packet.SourceAnchors.Select(anchor => new ChatCurrentSourceAnchor(
                    anchor.AnchorId,
                    anchor.DocumentVersionId,
                    anchor.RepositoryId,
                    anchor.RepositoryRelativePath,
                    anchor.LineStart,
                    anchor.LineEnd,
                    anchor.SourceHash)).ToArray(),
                packet.Relationships
                    .Where(relationship =>
                        !generatedFromOnly ||
                        relationship.RelationshipType == ChatRelationshipType.GeneratedFrom)
                    .Select(relationship =>
                {
                    EvidenceDocument source = documents[relationship.SourceDocumentHandle];
                    EvidenceDocument target = documents[relationship.TargetDocumentHandle];
                    bool isCrossRepositoryReference =
                        relationship.RelationshipType == ChatRelationshipType.References &&
                        source.RepositoryId != target.RepositoryId;
                    return new ChatCurrentRelationship(
                        relationship.EdgeId,
                        relationship.RelationshipType,
                        source.DocumentVersionId,
                        target.DocumentVersionId,
                        source.RepositoryId,
                        target.RepositoryId,
                        relationship.Depth,
                        relationship.ExactDecisionId,
                        isCrossRepositoryReference ? relationship.ExactDecisionCount : 0,
                        isCrossRepositoryReference ? relationship.ExactDecisionSetFingerprint : null,
                        omitFlowGroupBindings ? null : relationship.FlowGroupId,
                        relationship.ProfileFingerprint);
                }).ToArray(),
                flowGroups ?? packet.FlowGroups);
        }

        internal Task CommitSectionTextChangeAsync() => CommitSqlAsync(
            $"UPDATE \"DocumentSections\" SET \"ContentMarkdown\" = '篡改后的持久化内容。' WHERE \"StableSectionId\" = {Id(10_201)}");

        internal Task CommitAnchorLossAsync() => CommitSqlAsync(
            $"DELETE FROM \"SourceAnchors\" WHERE \"Id\" = {_businessSourceAnchorId}");

        internal Task CommitAnchorRangeChangeAsync() => CommitSqlAsync(
            $"UPDATE \"SourceAnchors\" SET \"LineStart\" = 13, \"LineEnd\" = 19 WHERE \"Id\" = {_businessSourceAnchorId}");

        internal Task CommitGeneratedFromLossAsync() => CommitSqlAsync(
            $"DELETE FROM \"DocumentEdges\" WHERE \"Id\" = {_generatedFromEdgeId}");

        internal Task CommitGeneratedFromIdentityMutationAsync(string mutation) => mutation switch
        {
            "generated-from-source-scan-run" => CommitGeneratedFromScanRunMutationAsync(
                nameof(DocumentEdge.SourceScanRunId)),
            "generated-from-target-scan-run" => CommitGeneratedFromScanRunMutationAsync(
                nameof(DocumentEdge.TargetScanRunId)),
            "generated-from-source-profiled-revision" => CommitSqlAsync(
                $"UPDATE \"DocumentVersions\" SET \"ProfiledRevision\" = {new string('0', 40)} WHERE \"Id\" = {_businessDocumentVersionId}"),
            "generated-from-target-profiled-revision" => CommitSqlAsync(
                $"UPDATE \"DocumentVersions\" SET \"ProfiledRevision\" = {new string('0', 40)} WHERE \"Id\" = {ExactCrossRepositoryTestValues.ConsumerDocumentVersionId}"),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown GENERATED_FROM identity mutation."),
        };

        private async Task CommitGeneratedFromScanRunMutationAsync(string edgeScanRunProperty)
        {
            await Context.Database.OpenConnectionAsync();
            try
            {
                await using (DbCommand pragma = Context.Database.GetDbConnection().CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys = OFF";
                    await pragma.ExecuteNonQueryAsync();
                }

                switch (edgeScanRunProperty)
                {
                    case nameof(DocumentEdge.SourceScanRunId):
                        await Context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE \"DocumentEdges\" SET \"SourceScanRunId\" = {ExactCrossRepositoryTestValues.UnsupportedScanRunId} WHERE \"Id\" = {_generatedFromEdgeId}");
                        break;
                    case nameof(DocumentEdge.TargetScanRunId):
                        await Context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE \"DocumentEdges\" SET \"TargetScanRunId\" = {ExactCrossRepositoryTestValues.UnsupportedScanRunId} WHERE \"Id\" = {_generatedFromEdgeId}");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(edgeScanRunProperty), edgeScanRunProperty, null);
                }

                await using (DbCommand pragma = Context.Database.GetDbConnection().CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys = ON";
                    await pragma.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                await Context.Database.CloseConnectionAsync();
            }

            Context.ChangeTracker.Clear();
        }

        internal Task CommitReferencesEvidenceLossAsync() => CommitSqlAsync(
            $"DELETE FROM \"ExactCrossRepositoryEdgeEvidence\" WHERE \"EdgeId\" = {_referencesEdgeId}");

        internal Task CommitReferencesProfileChangeAsync() => CommitSqlAsync(
            $"UPDATE \"ExactCrossRepositoryEdgeEvidence\" SET \"ProfileFingerprint\" = '5123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef' WHERE \"EdgeId\" = {_referencesEdgeId}");

        internal Task CommitReferencesDirectionChangeAsync() => CommitSqlAsync(
            $"UPDATE \"ExactCrossRepositoryEdgeEvidence\" SET \"ConsumerSourceAnchorId\" = {ExactCrossRepositoryTestValues.ProviderSourceAnchorId}, \"ConsumerDocumentVersionId\" = {ExactCrossRepositoryTestValues.ProviderDocumentVersionId}, \"ConsumerRepositoryId\" = {ExactCrossRepositoryTestValues.ProviderRepositoryId}, \"ConsumerScanRunId\" = {ExactCrossRepositoryTestValues.ProviderScanRunId}, \"ProviderSourceAnchorId\" = {ExactCrossRepositoryTestValues.ConsumerSourceAnchorId}, \"ProviderDocumentVersionId\" = {ExactCrossRepositoryTestValues.ConsumerDocumentVersionId}, \"ProviderRepositoryId\" = {ExactCrossRepositoryTestValues.ConsumerRepositoryId}, \"ProviderScanRunId\" = {ExactCrossRepositoryTestValues.ConsumerScanRunId} WHERE \"EdgeId\" = {_referencesEdgeId}");

        internal Task CommitDocumentTitleMutationAsync() => CommitSqlAsync(
            $"UPDATE \"DocumentVersions\" SET \"Title\" = '篡改后的文档标题。' WHERE \"Id\" = {_businessDocumentVersionId}");

        internal Task CommitSelectedScanRunStatusMutationAsync() => CommitSqlAsync(
            $"UPDATE \"ScanRuns\" SET \"Status\" = 'Profiling' WHERE \"Id\" = {ExactCrossRepositoryTestValues.ConsumerScanRunId}");

        internal Task CommitSelectedScanRunProfiledRevisionMutationAsync() => CommitSqlAsync(
            $"UPDATE \"ScanRuns\" SET \"ProfiledRevision\" = {new string('0', 40)} WHERE \"Id\" = {ExactCrossRepositoryTestValues.ProviderScanRunId}");

        internal Task CommitFlowStepDocumentProfiledRevisionMutationAsync() => CommitSqlAsync(
            $"UPDATE \"DocumentVersions\" SET \"ProfiledRevision\" = {new string('0', 40)} WHERE \"Id\" = {ExactCrossRepositoryTestValues.ProviderDocumentVersionId}");

        internal Task CommitSourceAnchorIdentityMutationAsync(string mutation) => mutation switch
        {
            "scan-run" => CommitSqlWithForeignKeysDisabledAsync(
                $"UPDATE \"SourceAnchors\" SET \"ScanRunId\" = {ExactCrossRepositoryTestValues.UnsupportedScanRunId} WHERE \"Id\" = {_businessSourceAnchorId}"),
            "profiled-revision" => CommitSqlAsync(
                $"UPDATE \"SourceAnchors\" SET \"ProfiledRevision\" = {new string('0', 40)} WHERE \"Id\" = {_businessSourceAnchorId}"),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown Source Anchor identity mutation."),
        };

        internal Task CommitCrossSystemDecisionMutationAsync() => CommitSqlAsync(
            $"UPDATE \"CrossRepositoryResolutionDecisions\" SET \"SystemId\" = {Id(10_998)} WHERE \"DocumentEdgeId\" = {_referencesEdgeId}");

        internal Task CommitExactDecisionCanonicalFingerprintMutationAsync() => CommitSqlAsync(
            $"UPDATE \"CrossRepositoryResolutionDecisions\" SET \"DecisionFingerprint\" = '5123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef' WHERE \"DocumentEdgeId\" = {_referencesEdgeId}");

        internal async Task CommitSuccessorAsync()
        {
            await using VulgataDbContext mutationContext = exactFixture.Database.CreateContext();
            Guid successorId = Id(10_006);
            mutationContext.DocumentVersions.Add(DocumentVersion.Create(
                successorId,
                ExactCrossRepositoryTestValues.ProviderLogicalDocumentId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.SystemId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                exactFixture.ProviderRepository.Commit,
                2,
                "提供端后继文档",
                "docs/provider-successor.md",
                KnowledgeDocumentLimits.SchemaIdentity,
                "{}",
                "后继版本。",
                ExactCrossRepositoryTestValues.ContentFingerprint,
                ExactCrossRepositoryTestValues.CommitFingerprint,
                ExactCrossRepositoryTestValues.Pass1CompletedAt.AddMinutes(1)));
            await mutationContext.SaveChangesAsync();
            int supersededRows = await mutationContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"DocumentVersions\" SET \"SupersededById\" = {successorId} WHERE \"Id\" = {ExactCrossRepositoryTestValues.ProviderDocumentVersionId}");
            int currentVersionRows = await mutationContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {successorId} WHERE \"Id\" = {ExactCrossRepositoryTestValues.ProviderLogicalDocumentId}");
            Assert.Equal(1, supersededRows);
            Assert.Equal(1, currentVersionRows);
        }

        private async Task CommitSqlAsync(FormattableString sql)
        {
            await using VulgataDbContext mutationContext = exactFixture.Database.CreateContext();
            int affectedRows = await mutationContext.Database.ExecuteSqlInterpolatedAsync(sql);
            Assert.Equal(1, affectedRows);
        }

        private async Task CommitSqlWithForeignKeysDisabledAsync(FormattableString sql)
        {
            await using VulgataDbContext mutationContext = exactFixture.Database.CreateContext();
            await mutationContext.Database.OpenConnectionAsync();
            try
            {
                await using (DbCommand pragma = mutationContext.Database.GetDbConnection().CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys = OFF";
                    await pragma.ExecuteNonQueryAsync();
                }

                int affectedRows = await mutationContext.Database.ExecuteSqlInterpolatedAsync(sql);
                Assert.Equal(1, affectedRows);

                await using (DbCommand pragma = mutationContext.Database.GetDbConnection().CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys = ON";
                    await pragma.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                await mutationContext.Database.CloseConnectionAsync();
            }
        }

        internal static async Task<CurrentnessFixture> CreateAsync(
            bool includeAcceptedV2Authority = true)
        {
            ExactCrossRepositoryTestFixture exactFixture =
                await ExactCrossRepositoryTestFixture.CreateAsync();
            VulgataDbContext context = exactFixture.Database.CreateContext();
            try
            {
                ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
                ResolutionOccurrence occurrence = profile.Occurrences.Single();
                ExactPass1DocumentFixture consumer = exactFixture.CreatePass1Document(
                    ExactPass1DocumentKind.Consumer,
                    profile.Repositories.Single(repository =>
                        repository.Alias == occurrence.ConsumerAlias).ExpectedCommit);
                ExactPass1DocumentFixture provider = exactFixture.CreatePass1Document(
                    ExactPass1DocumentKind.Provider,
                    profile.Repositories.Single(repository =>
                        repository.Alias == occurrence.ProviderAlias).ExpectedCommit);
                await SeedAcceptedV2PairAsync(context, exactFixture, consumer, provider);
                await ApplyAcceptedV2AuthorityAsync(context, profile, occurrence, consumer, provider);
                context.ChangeTracker.Clear();

                LogicalDocument consumerDocument = consumer.LogicalDocument;
                LogicalDocument providerDocument = provider.LogicalDocument;
                LogicalDocument businessDocument = LogicalDocument.Create(
                    _businessLogicalDocumentId,
                    consumer.DocumentVersion.RepositoryId,
                    DocumentType.BusinessLogic,
                    "prepared/business-flow",
                    ExactCrossRepositoryTestValues.CreatedAt);
                context.LogicalDocuments.Add(businessDocument);
                await context.SaveChangesAsync();

                DocumentVersion businessVersion = DocumentVersion.Create(
                    _businessDocumentVersionId,
                    _businessLogicalDocumentId,
                    consumer.DocumentVersion.RepositoryId,
                    ExactCrossRepositoryTestValues.SystemId,
                    consumer.DocumentVersion.ScanRunId,
                    consumer.DocumentVersion.ProfiledRevision,
                    1,
                    "客户会话更新业务流程",
                    "docs/customer-session-flow.md",
                    KnowledgeDocumentLimits.SchemaIdentity,
                    "{}",
                    "客户请求经由消费端调用提供端完成会话更新。",
                    ExactCrossRepositoryTestValues.ContentFingerprint,
                    ExactCrossRepositoryTestValues.CommitFingerprint,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt);
                var businessAnchor = new SourceAnchor(
                    _businessSourceAnchorId,
                    businessVersion.Id,
                    businessVersion.RepositoryId,
                    businessVersion.ScanRunId,
                    consumer.CodeUnit.Id,
                    businessVersion.ProfiledRevision,
                    "src/main/java/customer/SessionFlow.java",
                    "SessionFlow.java#line=12;rule=prepared-flow",
                    12,
                    18,
                    ExactCrossRepositoryTestValues.SourceHash,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt);
                businessVersion.SourceAnchors.Add(businessAnchor);
                context.DocumentVersions.Add(businessVersion);
                await context.SaveChangesAsync();

                context.DocumentSections.AddRange(
                    Section(
                        Id(10_101),
                        _businessLogicalDocumentId,
                        businessVersion,
                        Id(10_201),
                        "客户请求经由消费端调用提供端完成会话更新。",
                        _businessSourceAnchorId,
                        [consumer.DocumentVersion.Id]),
                    Section(
                        Id(10_102),
                        consumer.LogicalDocument.Id,
                        consumer.DocumentVersion,
                        Id(10_202),
                        "消费端调用 cust/updateSession。",
                        consumer.SourceAnchor.Id,
                        [provider.DocumentVersion.Id]),
                    Section(
                        Id(10_103),
                        provider.LogicalDocument.Id,
                        provider.DocumentVersion,
                        Id(10_203),
                        "提供端处理会话更新。",
                        provider.SourceAnchor.Id,
                        []));

                DocumentEdge referencesEdge = await context.DocumentEdges.SingleAsync(edge =>
                    edge.SourceDocumentVersionId == consumer.DocumentVersion.Id &&
                    edge.TargetDocumentVersionId == provider.DocumentVersion.Id &&
                    edge.EdgeType == DocumentEdgeType.References);
                Guid referencesEdgeId = referencesEdge.Id;
                CrossRepositoryResolutionDecision acceptedDecision = await context
                    .CrossRepositoryResolutionDecisions
                    .SingleAsync(decision => decision.DocumentEdgeId == referencesEdgeId);
                var generatedFromEdge = new DocumentEdge(
                    _generatedFromEdgeId,
                    businessVersion.Id,
                    businessVersion.RepositoryId,
                    businessVersion.ScanRunId,
                    consumer.DocumentVersion.Id,
                    consumer.DocumentVersion.RepositoryId,
                    consumer.DocumentVersion.ScanRunId,
                    DocumentEdgeType.GeneratedFrom,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt);
                context.DocumentEdges.Add(generatedFromEdge);
                context.ExactCrossRepositoryEdgeEvidence.Add(
                    new ExactCrossRepositoryEdgeEvidence(
                        referencesEdgeId,
                        consumer.SourceAnchor.Id,
                        consumer.DocumentVersion.Id,
                        consumer.DocumentVersion.RepositoryId,
                        consumer.DocumentVersion.ScanRunId,
                        provider.SourceAnchor.Id,
                        provider.DocumentVersion.Id,
                        provider.DocumentVersion.RepositoryId,
                        provider.DocumentVersion.ScanRunId,
                        "cust/updateSession",
                        "in/cust/updateSession",
                        "cust/updateSession",
                        ExactResolutionProtocol.CustomRpc,
                        "ExactConfigured",
                        1.0000m,
                        profile.SchemaVersion,
                        profile.ProfileId,
                        profile.ProfileVersion,
                        ProfileFingerprint,
                        occurrence.OccurrenceId,
                        consumer.SourceAnchor.EvidenceLocator,
                        provider.SourceAnchor.EvidenceLocator,
                        acceptedDecision.EvidenceFingerprint,
                        ExactCrossRepositoryTestValues.Pass1CompletedAt));

                var flowGroup = new FlowGroup(
                    Id(10_301),
                    businessVersion.Id,
                    businessVersion.RepositoryId,
                    businessVersion.ScanRunId,
                    1,
                    "客户会话更新",
                    ExactCrossRepositoryTestValues.Pass1CompletedAt);
                // The retriever's production baseline is an accepted v2 Published
                // Flow Group. The legacy constructor intentionally leaves these
                // persisted authority fields empty, which would make every
                // otherwise-current fixture row ineligible once publication
                // currentness is enforced. Keep the fixture's existing document
                // shape, but seed the accepted publication identity explicitly.
                context.Entry(flowGroup).Property(nameof(FlowGroup.SchemaIdentity)).CurrentValue =
                    "endpoint-flow-group/1.0";
                context.Entry(flowGroup).Property(nameof(FlowGroup.State)).CurrentValue = "Published";
                context.Entry(flowGroup).Property(nameof(FlowGroup.SystemId)).CurrentValue =
                    ExactCrossRepositoryTestValues.SystemId;
                context.Entry(flowGroup).Property(nameof(FlowGroup.PlanFingerprint)).CurrentValue =
                    new string('a', 64);
                context.Entry(flowGroup).Property(nameof(FlowGroup.MembershipFingerprint)).CurrentValue =
                    new string('b', 64);
                context.Entry(flowGroup).Property(nameof(FlowGroup.ExactResolutionDecisionId)).CurrentValue =
                    acceptedDecision.Id;
                context.Entry(flowGroup).Property(nameof(FlowGroup.ExactResolutionEdgeId)).CurrentValue =
                    referencesEdgeId;
                var flowTrace = new FlowTrace(
                    Id(10_302),
                    flowGroup.Id,
                    1,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt);
                flowGroup.Traces.Add(flowTrace);
                flowTrace.Steps.Add(new FlowTraceStep(
                    Id(10_303),
                    flowTrace.Id,
                    1,
                    consumer.DocumentVersion.Id,
                    consumer.DocumentVersion.RepositoryId,
                    consumer.DocumentVersion.ScanRunId,
                    null,
                    FlowTraceStepKind.Document,
                    "消费端接收请求。",
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));
                flowTrace.Steps.Add(new FlowTraceStep(
                    Id(10_304),
                    flowTrace.Id,
                    2,
                    provider.DocumentVersion.Id,
                    provider.DocumentVersion.RepositoryId,
                    provider.DocumentVersion.ScanRunId,
                    referencesEdge.Id,
                    FlowTraceStepKind.Relationship,
                    "通过精确 REFERENCES Edge 调用提供端。",
                    ExactCrossRepositoryTestValues.Pass1CompletedAt,
                    acceptedDecision.Id));
                context.FlowGroups.Add(flowGroup);
                await context.SaveChangesAsync();

                if (!includeAcceptedV2Authority)
                {
                    await context.Database.ExecuteSqlInterpolatedAsync(
                        $"DELETE FROM \"ExactResolutionProviderEvidenceContributions\" WHERE \"ResolutionDecisionId\" IN (SELECT \"Id\" FROM \"CrossRepositoryResolutionDecisions\" WHERE \"DocumentEdgeId\" = {referencesEdgeId})");
                    await context.Database.ExecuteSqlInterpolatedAsync(
                        $"DELETE FROM \"CrossRepositoryResolutionDecisions\" WHERE \"DocumentEdgeId\" = {referencesEdgeId}");
                }

                await SetCurrentVersionAsync(context, consumerDocument.Id, consumer.DocumentVersion.Id);
                await SetCurrentVersionAsync(context, providerDocument.Id, provider.DocumentVersion.Id);
                await SetCurrentVersionAsync(context, businessDocument.Id, businessVersion.Id);
                context.ChangeTracker.Clear();
                return new CurrentnessFixture(exactFixture, context, referencesEdgeId);
            }
            catch
            {
                await context.DisposeAsync();
                await exactFixture.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await exactFixture.DisposeAsync();
        }

        private static DocumentSection Section(
            Guid id,
            Guid logicalDocumentId,
            DocumentVersion version,
            Guid stableSectionId,
            string contentMarkdown,
            Guid anchorId,
            IReadOnlyCollection<Guid> supportedDocumentVersionIds) =>
            DocumentSection.Create(
                id,
                logicalDocumentId,
                version.Id,
                version.RepositoryId,
                stableSectionId,
                1,
                DocumentSectionKind.Overview,
                "概览",
                contentMarkdown,
                contentMarkdown,
                JsonSerializer.Serialize(new[] { anchorId }),
                JsonSerializer.Serialize(supportedDocumentVersionIds),
                ExactCrossRepositoryTestValues.Pass1CompletedAt);

        private static async Task SetCurrentVersionAsync(
            VulgataDbContext context,
            Guid logicalDocumentId,
            Guid documentVersionId) =>
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {documentVersionId} WHERE \"Id\" = {logicalDocumentId}");

        private static async Task SeedAcceptedV2PairAsync(
            VulgataDbContext context,
            ExactCrossRepositoryTestFixture fixture,
            ExactPass1DocumentFixture consumer,
            ExactPass1DocumentFixture provider)
        {
            await Vulgata.Tests.PreparedPass2.ExactCrossRepositoryResolutionTests
                .SeedAcceptedV2PairAsync(context, fixture, consumer, provider);
        }

        private static async Task ApplyAcceptedV2AuthorityAsync(
            VulgataDbContext context,
            ExactResolutionProfile profile,
            ResolutionOccurrence occurrence,
            ExactPass1DocumentFixture consumer,
            ExactPass1DocumentFixture provider)
        {
            ExactResolutionOwner consumerOwner = new(
                consumer.DocumentVersion.RepositoryId,
                consumer.DocumentVersion.ScanRunId,
                consumer.DocumentVersion.Id,
                consumer.SourceAnchor.Id,
                consumer.DocumentVersion.ProfiledRevision);
            ExactResolutionOwner providerOwner = new(
                provider.DocumentVersion.RepositoryId,
                provider.DocumentVersion.ScanRunId,
                provider.DocumentVersion.Id,
                provider.SourceAnchor.Id,
                provider.DocumentVersion.ProfiledRevision);
            var consumerCandidate = new ConsumerDispatchCandidate(
                occurrence.OccurrenceId,
                consumerOwner,
                occurrence.Consumer.Invocation.Form,
                occurrence.Consumer.Invocation.ReceiverSymbol,
                occurrence.Consumer.Invocation.MemberName,
                occurrence.Consumer.Invocation.EndpointArgumentIndex,
                occurrence.Consumer.Invocation.Line,
                occurrence.Consumer.Invocation.OccurrenceOrdinalOnLine,
                "cust/updateSession",
                "cust/updateSession",
                "consumer.java#line=45;rule=exact-java-direct-literal-v1",
                new string('e', 64));
            var providerCandidate = new ProviderEndpointCandidate(
                occurrence.OccurrenceId,
                providerOwner,
                "in/cust/updateSession",
                "cust/updateSession",
                "provider.xml#line=2;rule=exact-xml-root-attribute-v1");
            var unsupported = new ExactResolutionDecision(
                profile.UnsupportedObservations.Single().ObservationId,
                ExactResolutionDecisionKind.UnsupportedObservation,
                consumerOwner,
                null,
                "unsupported_kind",
                "unsupported.java#line=66;rule=unsupported-member-invocation-v1",
                ConsumerEntryStableKey: new string('f', 64));
            ExactResolutionDecisionBatch batch = await new ExactCrossRepositoryResolver().ResolveAsync(
                profile,
                [consumerCandidate],
                [providerCandidate],
                [unsupported]);
            ExactResolutionBatchOutcome outcome = await new ExactCrossRepositoryResolutionStore(context)
                .ApplyAsync(
                    batch,
                    new RepositoryAccessScope([
                        consumer.DocumentVersion.RepositoryId,
                        provider.DocumentVersion.RepositoryId]));
            Assert.Equal(ExactResolutionOutcomeKind.Applied, outcome.Kind);
        }

        internal static string Hash(string value) => Sha256(value);

        private static string Sha256(string value) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

        internal static Guid Id(int value) =>
            Guid.Parse($"11510000-0000-0000-0000-{value:D12}");
    }
}
