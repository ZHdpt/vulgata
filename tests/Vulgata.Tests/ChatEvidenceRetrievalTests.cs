using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class ChatEvidenceRetrievalTests
{
    [Fact]
    public async Task RetrieveAsync_AcceptedV2DecisionAndCurrentEdgeWithoutLegacyEvidence_ReturnsCompletePacketAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.RemoveLegacyEvidenceAsync();

        ChatEvidenceResult result = await new ChatEvidenceRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("v2-authority"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.NotNull(result.Packet);
    }

    [Fact]
    public async Task RetrieveAsync_LegacyEvidenceWithoutAcceptedV2Decision_DoesNotAuthorizePacketAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.RemoveV2AuthorityAsync();

        ChatEvidenceResult result = await new ChatEvidenceRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("legacy-only"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.IncompleteOrStale, result.Kind);
        Assert.Null(result.Packet);
    }

    [Fact]
    public async Task RetrieveAsync_AuthorizedCurrentPreparedFlow_ReturnsCompleteCanonicalPacketAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        var verifier = new RecordingAuthorizationVerifier(true, true, true, true);
        var scope = new RepositoryAccessScope(fixture.RepositoryIds, verifier);
        ChatRetrievalRequest request = fixture.CreateRequest("request-1");
        var retriever = new ChatEvidenceRetriever(fixture.Context);

        ChatEvidenceResult first = await retriever.RetrieveAsync(request, scope);
        ChatEvidenceResult second = await retriever.RetrieveAsync(request, scope);

        EvidencePacket packet = Assert.IsType<EvidencePacket>(first.Packet);
        EvidencePacket repeatedPacket = Assert.IsType<EvidencePacket>(second.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, first.Kind);
        Assert.Equal(ChatEvidenceResultKind.Found, second.Kind);
        Assert.Equal(["d1", "d2", "d3"], packet.Documents.Select(document => document.DocumentHandle));
        Assert.Equal(["s1", "s2", "s3"], packet.Sections.Select(section => section.SectionHandle));
        Assert.Equal(["a1", "a2", "a3"], packet.SourceAnchors.Select(anchor => anchor.AnchorHandle));
        Assert.All(packet.SourceAnchors, anchor =>
        {
            Assert.DoesNotContain(":", anchor.RepositoryRelativePath, StringComparison.Ordinal);
            Assert.True(anchor.LineStart > 0);
            Assert.True(anchor.LineEnd >= anchor.LineStart);
        });
        Assert.Collection(
            packet.Relationships,
            relationship =>
            {
                Assert.Equal(ChatRelationshipType.References, relationship.RelationshipType);
                Assert.Equal("d2", relationship.SourceDocumentHandle);
                Assert.Equal("d3", relationship.TargetDocumentHandle);
                Assert.Equal(fixture.AcceptedProviderEvidenceSetFingerprint, relationship.ProfileFingerprint);
            },
            relationship =>
            {
                Assert.Equal(ChatRelationshipType.GeneratedFrom, relationship.RelationshipType);
                Assert.Equal("d1", relationship.SourceDocumentHandle);
                Assert.Equal("d2", relationship.TargetDocumentHandle);
                Assert.Null(relationship.ProfileFingerprint);
            });
        Assert.NotEmpty(packet.FlowEvidence);
        Assert.Equal(packet.Fingerprint, repeatedPacket.Fingerprint);
        Assert.Equal(4, verifier.CallCount);
        Assert.All(verifier.RequiredRepositoryIds, ids => Assert.Equal(fixture.RepositoryIds, ids));
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task RetrieveAsync_CurrentDirectedOneHopReference_ExpandsRelatedUnitAndExcludesSecondHopAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddCurrentOneHopReferenceAsync();

        ChatEvidenceResult result = await new ChatEvidenceRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("one-hop-reference"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        EvidenceDocument related = Assert.Single(
            packet.Documents,
            document => document.Title == "会话调用扩展");
        Assert.Contains(packet.Sections, section => section.DocumentHandle == related.DocumentHandle);
        Assert.Contains(packet.SourceAnchors, anchor => anchor.DocumentHandle == related.DocumentHandle);
        Assert.Contains(
            packet.Relationships,
            relationship =>
                relationship.RelationshipType == ChatRelationshipType.References &&
                relationship.TargetDocumentHandle == related.DocumentHandle);
        Assert.DoesNotContain(packet.Documents, document => document.Title == "二跳无关代码");
        Assert.True(packet.Documents.Count <= 5);
    }

    [Fact]
    public async Task RetrieveAsync_BoundedValidatedConversation_IsBoundToReturnedPacketAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        ChatRetrievalRequest emptyRequest = fixture.CreateRequest("conversation-empty");
        ChatEvidenceResult emptyResult = await new ChatEvidenceRetriever(fixture.Context)
            .RetrieveAsync(
                emptyRequest,
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));
        EvidencePacket emptyPacket = Assert.IsType<EvidencePacket>(emptyResult.Packet);
        EvidenceSection citedSection = emptyPacket.Sections[0];
        EvidenceDocument citedDocument = Assert.Single(
            emptyPacket.Documents,
            document => document.DocumentHandle == citedSection.DocumentHandle);
        ValidatedConversationTurn priorTurn = ValidatedConversationTurn.Create(
            "上一轮客户会话问题",
            "上一轮已验证回答。",
            emptyPacket.Fingerprint,
            [citedSection.SectionHandle],
            [citedDocument.LogicalDocumentId]);
        ChatConversationState conversation = emptyRequest.Conversation.AppendValidatedTurn(priorTurn);
        ChatRetrievalRequest contextualRequest = fixture.CreateRequest(
            "conversation-context",
            conversation);

        ChatEvidenceResult contextualResult = await new ChatEvidenceRetriever(fixture.Context)
            .RetrieveAsync(
                contextualRequest,
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        EvidencePacket contextualPacket = Assert.IsType<EvidencePacket>(contextualResult.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, emptyResult.Kind);
        Assert.Equal(ChatEvidenceResultKind.Found, contextualResult.Kind);
        ValidatedConversationTurn returnedTurn = Assert.Single(contextualPacket.Conversation.Turns);
        Assert.Equal(priorTurn.Question, returnedTurn.Question);
        Assert.Equal(priorTurn.AnswerText, returnedTurn.AnswerText);
        Assert.Equal(priorTurn.PacketFingerprint, returnedTurn.PacketFingerprint);
        Assert.Equal(priorTurn.CitationHandles, returnedTurn.CitationHandles);
        Assert.Equal(priorTurn.LogicalDocumentIds, returnedTurn.LogicalDocumentIds);
        Assert.NotEqual(emptyPacket.Fingerprint, contextualPacket.Fingerprint);
    }

    [Fact]
    public async Task RetrieveAsync_InvalidOrRestrictedRelationalEvidence_ReturnsClosedWholePacketFailureAsync()
    {
        await AssertClosedFailureAfterMutationAsync(
            fixture => fixture.AddOutOfScopeSectionSupportAsync());
        await AssertClosedFailureAfterMutationAsync(
            fixture => fixture.CorruptSectionSupportJsonAsync());
        await AssertClosedFailureAfterMutationAsync(
            fixture => fixture.RestrictSectionAsync());
        await AssertClosedFailureAfterMutationAsync(
            fixture => fixture.RemoveRequiredGeneratedFromAsync());
        await AssertClosedFailureAfterMutationAsync(
            fixture => fixture.StaleCurrentDocumentAsync());
        await AssertClosedFailureAfterMutationAsync(
            fixture => fixture.ExceedSectionMaximumAsync());
        await AssertClosedFailureAfterMutationAsync(
            fixture => fixture.ExceedAnchorMaximumAsync());
        await AssertClosedFailureAfterMutationAsync(
            fixture => fixture.ExceedFlowMaximumAsync());
        await AssertClosedFailureAfterMutationAsync(
            fixture => fixture.ExceedDocumentMaximumAsync());

        await using PreparedEvidenceFixture revokedFixture =
            await PreparedEvidenceFixture.CreateAsync();
        var revokedScope = new RepositoryAccessScope(
            revokedFixture.RepositoryIds,
            new RecordingAuthorizationVerifier(true, false));
        ChatEvidenceResult revoked = await new ChatEvidenceRetriever(revokedFixture.Context)
            .RetrieveAsync(revokedFixture.CreateRequest("revoked"), revokedScope);

        Assert.Equal(ChatEvidenceResultKind.NotFoundOrUnauthorized, revoked.Kind);
        Assert.Null(revoked.Packet);
        Assert.Null(revokedFixture.Context.Database.CurrentTransaction);

        var missingEndpointScope = new RepositoryAccessScope(
            [ExactCrossRepositoryTestValues.ConsumerRepositoryId]);
        ChatEvidenceResult missingEndpoint = await new ChatEvidenceRetriever(revokedFixture.Context)
            .RetrieveAsync(revokedFixture.CreateRequest("missing-endpoint"), missingEndpointScope);
        Assert.Equal(ChatEvidenceResultKind.NotFoundOrUnauthorized, missingEndpoint.Kind);
        Assert.Null(missingEndpoint.Packet);
    }

    [Theory]
    [InlineData("ProfileId")]
    [InlineData("ProfileVersion")]
    [InlineData("ProfileFingerprint")]
    public async Task RetrieveAsync_LegacyProfileIdentityDrifts_DoNotAffectAcceptedV2PacketAsync(
        string field)
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.DriftExactProfileAsync(field);
        var scope = new RepositoryAccessScope(
            fixture.RepositoryIds,
            new RecordingAuthorizationVerifier(true, true));

        ChatEvidenceResult result = await new ChatEvidenceRetriever(fixture.Context)
            .RetrieveAsync(fixture.CreateRequest($"wrong-{field}"), scope);

        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.NotNull(result.Packet);
    }

    [Fact]
    public async Task RetrieveAsync_LegacyReversedRelationshipEvidence_DoesNotAffectAcceptedV2PacketAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.ReverseExactRelationshipEvidenceAsync();

        ChatEvidenceResult result = await new ChatEvidenceRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("legacy-reversed-relationship"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.NotNull(result.Packet);
    }

    [Fact]
    public async Task RetrieveAsync_WrongProfileDecoyEdge_IsIgnoredAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddDecoyExactEdgeAsync(acceptedProfile: false);
        var scope = new RepositoryAccessScope(
            fixture.RepositoryIds,
            new RecordingAuthorizationVerifier(true, true));
        var retriever = new ChatEvidenceRetriever(fixture.Context);

        ChatEvidenceResult withDecoy = await retriever.RetrieveAsync(
            fixture.CreateRequest("wrong-profile-decoy"),
            scope);

        Assert.Equal(ChatEvidenceResultKind.Found, withDecoy.Kind);
    }

    [Fact]
    public async Task RetrieveAsync_SecondLegacyPreparedEdge_DoesNotAffectAcceptedV2PacketAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddDecoyExactEdgeAsync(acceptedProfile: true);
        var scope = new RepositoryAccessScope(
            fixture.RepositoryIds,
            new RecordingAuthorizationVerifier(true, true));

        ChatEvidenceResult result = await new ChatEvidenceRetriever(fixture.Context).RetrieveAsync(
            fixture.CreateRequest("non-unique-accepted"),
            scope);

        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.NotNull(result.Packet);
    }

    [Fact]
    public async Task RetrieveAsync_CurrentProfiledRevisionDrifts_ReturnsClosedWholePacketFailureAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.DriftConsumerProfiledRevisionAsync();
        var scope = new RepositoryAccessScope(
            fixture.RepositoryIds,
            new RecordingAuthorizationVerifier(true, true));

        ChatEvidenceResult result = await new ChatEvidenceRetriever(fixture.Context)
            .RetrieveAsync(fixture.CreateRequest("stale-profiled-revision"), scope);

        Assert.Equal(ChatEvidenceResultKind.IncompleteOrStale, result.Kind);
        Assert.Null(result.Packet);
    }

    private static async Task AssertClosedFailureAfterMutationAsync(
        Func<PreparedEvidenceFixture, Task> mutateAsync,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(mutateAsync))]
        string? mutationName = null)
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await mutateAsync(fixture);
        var scope = new RepositoryAccessScope(
            fixture.RepositoryIds,
            new RecordingAuthorizationVerifier(true, true));

        ChatEvidenceResult result = await new ChatEvidenceRetriever(fixture.Context)
            .RetrieveAsync(fixture.CreateRequest(Guid.NewGuid().ToString("N")), scope);

        Assert.True(
            result.Kind is ChatEvidenceResultKind.NotFoundOrUnauthorized or
                ChatEvidenceResultKind.IncompleteOrStale,
            $"Mutation did not close packet: {mutationName}");
        Assert.Null(result.Packet);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    private sealed class RecordingAuthorizationVerifier(params bool[] decisions) :
        IRepositoryAuthorizationVerifier
    {
        private readonly Queue<bool> _decisions = new(decisions);

        internal int CallCount => RequiredRepositoryIds.Count;

        internal List<IReadOnlyCollection<Guid>> RequiredRepositoryIds { get; } = [];

        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequiredRepositoryIds.Add(requiredRepositoryIds.ToArray());
            return ValueTask.FromResult(_decisions.Count > 0 && _decisions.Dequeue());
        }
    }

    private sealed class PreparedEvidenceFixture(
        ExactCrossRepositoryTestFixture exactFixture,
        VulgataDbContext context,
        Guid referencesEdgeId,
        string acceptedEvidenceFingerprint,
        string acceptedProviderEvidenceSetFingerprint) : IAsyncDisposable
    {
        internal static string ProfileFingerprint =>
            new ExactResolutionProfileCatalog().Load().ProfileFingerprint;
        private const string EvidenceFingerprint =
            "4123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private static readonly Guid _businessLogicalDocumentId = Id(10_001);
        private static readonly Guid _businessDocumentVersionId = Id(10_002);
        private static readonly Guid _businessSourceAnchorId = Id(10_003);
        private static readonly Guid _generatedFromEdgeId = Id(10_005);
        private static readonly Guid _oneHopLogicalDocumentId = Id(60_001);
        private static readonly Guid _oneHopDocumentVersionId = Id(60_002);
        private static readonly Guid _oneHopSourceAnchorId = Id(60_003);
        private static readonly Guid _oneHopSectionId = Id(60_004);
        private static readonly Guid _oneHopStableSectionId = Id(60_005);
        private static readonly Guid _oneHopEdgeId = Id(60_006);
        private static readonly Guid _secondHopLogicalDocumentId = Id(60_011);
        private static readonly Guid _secondHopDocumentVersionId = Id(60_012);
        private static readonly Guid _secondHopSourceAnchorId = Id(60_013);
        private static readonly Guid _secondHopSectionId = Id(60_014);
        private static readonly Guid _secondHopStableSectionId = Id(60_015);
        private static readonly Guid _secondHopEdgeId = Id(60_016);

        internal VulgataDbContext Context { get; } = context;

        internal Guid ReferencesEdgeId { get; } = referencesEdgeId;

        internal string AcceptedEvidenceFingerprint { get; } = acceptedEvidenceFingerprint;

        internal string AcceptedProviderEvidenceSetFingerprint { get; } =
            acceptedProviderEvidenceSetFingerprint;

        internal IReadOnlyCollection<Guid> RepositoryIds { get; } =
        [
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
        ];

        internal ChatRetrievalRequest CreateRequest(
            string requestId,
            ChatConversationState? conversation = null)
        {
            ChatQuestionRequest questionRequest = ChatQuestionRequest.Create(
                requestId,
                ChatMode.Business,
                ExactCrossRepositoryTestValues.SystemId,
                RepositoryIds,
                "客户会话如何跨仓库更新？");
            ChatConversationScope conversationScope = ChatConversationScope.Create(
                questionRequest.Mode,
                questionRequest.SystemId,
                questionRequest.RepositoryIds);
            return ChatRetrievalRequest.Create(
                questionRequest,
                conversation ?? ChatConversationState.Empty(conversationScope),
                DateTimeOffset.UtcNow.AddMinutes(1));
        }

        internal async Task DriftExactProfileAsync(string field)
        {
            string wrongValue = field == "ProfileFingerprint"
                ? "3123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
                : $"wrong-{field}";
            await Context.Database.ExecuteSqlRawAsync(
                $"UPDATE \"ExactCrossRepositoryEdgeEvidence\" SET \"{field}\" = {{0}} WHERE \"EdgeId\" = {{1}}",
                wrongValue,
                ReferencesEdgeId);
            Context.ChangeTracker.Clear();
        }

        internal async Task RemoveLegacyEvidenceAsync()
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"ExactCrossRepositoryEdgeEvidence\" WHERE \"EdgeId\" = {ReferencesEdgeId}");
            Context.ChangeTracker.Clear();
        }

        internal async Task RemoveV2AuthorityAsync()
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"FlowGroups\" SET \"ExactResolutionDecisionId\" = NULL, \"ExactResolutionEdgeId\" = NULL WHERE \"ExactResolutionDecisionId\" IN (SELECT \"Id\" FROM \"CrossRepositoryResolutionDecisions\" WHERE \"DocumentEdgeId\" = {ReferencesEdgeId})");
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"ExactResolutionProviderEvidenceContributions\" WHERE \"ResolutionDecisionId\" IN (SELECT \"Id\" FROM \"CrossRepositoryResolutionDecisions\" WHERE \"DocumentEdgeId\" = {ReferencesEdgeId})");
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"CrossRepositoryResolutionDecisions\" WHERE \"DocumentEdgeId\" = {ReferencesEdgeId}");
            Context.ChangeTracker.Clear();
        }

        internal async Task DriftConsumerProfiledRevisionAsync()
        {
            const string staleRevision = "0000000000000000000000000000000000000000";
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"ScanRuns\" SET \"ProfiledRevision\" = {staleRevision} WHERE \"Id\" = {ExactCrossRepositoryTestValues.ConsumerScanRunId}");
            Context.ChangeTracker.Clear();
        }

        internal async Task AddDecoyExactEdgeAsync(bool acceptedProfile)
        {
            ExactCrossRepositoryEdgeEvidence source = await Context.ExactCrossRepositoryEdgeEvidence
                .AsNoTracking()
                .SingleAsync(value => value.EdgeId == ReferencesEdgeId);
            int discriminator = acceptedProfile ? 70_002 : 70_001;
            Guid edgeId = Id(discriminator);
            Context.DocumentEdges.Add(new DocumentEdge(
                edgeId,
                _businessDocumentVersionId,
                source.ConsumerRepositoryId,
                source.ConsumerScanRunId,
                source.ProviderDocumentVersionId,
                source.ProviderRepositoryId,
                source.ProviderScanRunId,
                DocumentEdgeType.References,
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            Context.ExactCrossRepositoryEdgeEvidence.Add(new ExactCrossRepositoryEdgeEvidence(
                edgeId,
                _businessSourceAnchorId,
                _businessDocumentVersionId,
                source.ConsumerRepositoryId,
                source.ConsumerScanRunId,
                source.ProviderSourceAnchorId,
                source.ProviderDocumentVersionId,
                source.ProviderRepositoryId,
                source.ProviderScanRunId,
                source.ConsumerRawIdentifier,
                source.ProviderRawIdentifier,
                source.NormalizedEndpointIdentifier,
                source.Protocol,
                source.ResolutionKind,
                source.Confidence,
                source.ProfileSchemaVersion,
                acceptedProfile ? source.ProfileId : "decoy-profile",
                source.ProfileVersion,
                source.ProfileFingerprint,
                source.OccurrenceId,
                source.ConsumerEvidenceLocator,
                source.ProviderEvidenceLocator,
                acceptedProfile
                    ? "5123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
                    : "6123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        internal async Task AddCurrentOneHopReferenceAsync()
        {
            LogicalDocument oneHopLogicalDocument = LogicalDocument.Create(
                _oneHopLogicalDocumentId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                DocumentType.CodeLogic,
                "prepared/one-hop-session-extension",
                ExactCrossRepositoryTestValues.CreatedAt);
            LogicalDocument secondHopLogicalDocument = LogicalDocument.Create(
                _secondHopLogicalDocumentId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                DocumentType.CodeLogic,
                "prepared/second-hop-unrelated",
                ExactCrossRepositoryTestValues.CreatedAt);
            Context.LogicalDocuments.AddRange(oneHopLogicalDocument, secondHopLogicalDocument);
            await Context.SaveChangesAsync();

            DocumentVersion oneHopVersion = DocumentVersion.Create(
                _oneHopDocumentVersionId,
                _oneHopLogicalDocumentId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.SystemId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                ExactCrossRepositoryTestValues.SourceHash[..40],
                1,
                "会话调用扩展",
                "src/main/java/provider/SessionExtension.java",
                KnowledgeDocumentLimits.SchemaIdentity,
                "{}",
                "会话调用扩展处理。",
                ExactCrossRepositoryTestValues.ContentFingerprint,
                ExactCrossRepositoryTestValues.CommitFingerprint,
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            oneHopVersion.SourceAnchors.Add(new SourceAnchor(
                _oneHopSourceAnchorId,
                oneHopVersion.Id,
                oneHopVersion.RepositoryId,
                oneHopVersion.ScanRunId,
                ExactCrossRepositoryTestValues.ProviderCodeUnitId,
                oneHopVersion.ProfiledRevision,
                "src/main/java/provider/SessionExtension.java",
                "SessionExtension.java#line=8;rule=one-hop",
                8,
                12,
                ExactCrossRepositoryTestValues.SourceHash,
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            DocumentVersion secondHopVersion = DocumentVersion.Create(
                _secondHopDocumentVersionId,
                _secondHopLogicalDocumentId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.SystemId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                ExactCrossRepositoryTestValues.SourceHash[..40],
                1,
                "二跳无关代码",
                "src/main/java/provider/Unrelated.java",
                KnowledgeDocumentLimits.SchemaIdentity,
                "{}",
                "二跳无关代码。",
                ExactCrossRepositoryTestValues.ContentFingerprint,
                ExactCrossRepositoryTestValues.CommitFingerprint,
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            secondHopVersion.SourceAnchors.Add(new SourceAnchor(
                _secondHopSourceAnchorId,
                secondHopVersion.Id,
                secondHopVersion.RepositoryId,
                secondHopVersion.ScanRunId,
                ExactCrossRepositoryTestValues.ProviderCodeUnitId,
                secondHopVersion.ProfiledRevision,
                "src/main/java/provider/Unrelated.java",
                "Unrelated.java#line=3;rule=second-hop",
                3,
                5,
                ExactCrossRepositoryTestValues.SourceHash,
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            Context.DocumentVersions.AddRange(oneHopVersion, secondHopVersion);
            await Context.SaveChangesAsync();

            Context.DocumentSections.AddRange(
                Section(
                    _oneHopSectionId,
                    _oneHopLogicalDocumentId,
                    oneHopVersion,
                    _oneHopStableSectionId,
                    "会话调用扩展",
                    "会话调用扩展处理。",
                    _oneHopSourceAnchorId,
                    [ExactCrossRepositoryTestValues.ProviderDocumentVersionId]),
                Section(
                    _secondHopSectionId,
                    _secondHopLogicalDocumentId,
                    secondHopVersion,
                    _secondHopStableSectionId,
                    "二跳无关代码",
                    "二跳无关代码。",
                    _secondHopSourceAnchorId,
                    [_oneHopDocumentVersionId]));
            Context.DocumentEdges.AddRange(
                new DocumentEdge(
                    _oneHopEdgeId,
                    ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
                    ExactCrossRepositoryTestValues.ProviderRepositoryId,
                    ExactCrossRepositoryTestValues.ProviderScanRunId,
                    _oneHopDocumentVersionId,
                    ExactCrossRepositoryTestValues.ProviderRepositoryId,
                    ExactCrossRepositoryTestValues.ProviderScanRunId,
                    DocumentEdgeType.References,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt),
                new DocumentEdge(
                    _secondHopEdgeId,
                    _oneHopDocumentVersionId,
                    ExactCrossRepositoryTestValues.ProviderRepositoryId,
                    ExactCrossRepositoryTestValues.ProviderScanRunId,
                    _secondHopDocumentVersionId,
                    ExactCrossRepositoryTestValues.ProviderRepositoryId,
                    ExactCrossRepositoryTestValues.ProviderScanRunId,
                    DocumentEdgeType.References,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));
            await Context.SaveChangesAsync();
            await SetCurrentVersionAsync(Context, _oneHopLogicalDocumentId, _oneHopDocumentVersionId);
            await SetCurrentVersionAsync(Context, _secondHopLogicalDocumentId, _secondHopDocumentVersionId);
            Context.ChangeTracker.Clear();
        }

        internal async Task AddOutOfScopeSectionSupportAsync()
        {
            string unsupportedIds = JsonSerializer.Serialize(new[] { Id(99_999) });
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"DocumentSections\" SET \"DocumentVersionIdsJson\" = {unsupportedIds} WHERE \"Id\" = {Id(10_101)}");
            Context.ChangeTracker.Clear();
        }

        internal async Task CorruptSectionSupportJsonAsync()
        {
            const string malformedJson = "[";
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"DocumentSections\" SET \"DocumentVersionIdsJson\" = {malformedJson} WHERE \"Id\" = {Id(10_101)}");
            Context.ChangeTracker.Clear();
        }

        internal async Task RestrictSectionAsync()
        {
            const string restricted = "password=restricted";
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"DocumentSections\" SET \"ContentMarkdown\" = {restricted} WHERE \"Id\" = {Id(10_101)}");
            Context.ChangeTracker.Clear();
        }

        internal async Task RemoveRequiredGeneratedFromAsync()
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"DocumentEdges\" WHERE \"Id\" = {_generatedFromEdgeId}");
            Context.ChangeTracker.Clear();
        }

        internal async Task ReverseExactRelationshipEvidenceAsync()
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"ExactCrossRepositoryEdgeEvidence\" SET \"ConsumerSourceAnchorId\" = {ExactCrossRepositoryTestValues.ProviderSourceAnchorId}, \"ConsumerDocumentVersionId\" = {ExactCrossRepositoryTestValues.ProviderDocumentVersionId}, \"ConsumerRepositoryId\" = {ExactCrossRepositoryTestValues.ProviderRepositoryId}, \"ConsumerScanRunId\" = {ExactCrossRepositoryTestValues.ProviderScanRunId}, \"ProviderSourceAnchorId\" = {ExactCrossRepositoryTestValues.ConsumerSourceAnchorId}, \"ProviderDocumentVersionId\" = {ExactCrossRepositoryTestValues.ConsumerDocumentVersionId}, \"ProviderRepositoryId\" = {ExactCrossRepositoryTestValues.ConsumerRepositoryId}, \"ProviderScanRunId\" = {ExactCrossRepositoryTestValues.ConsumerScanRunId} WHERE \"EdgeId\" = {ReferencesEdgeId}");
            Context.ChangeTracker.Clear();
        }

        internal async Task StaleCurrentDocumentAsync()
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = NULL WHERE \"Id\" = {ExactCrossRepositoryTestValues.ProviderLogicalDocumentId}");
            Context.ChangeTracker.Clear();
        }

        internal async Task ExceedSectionMaximumAsync()
        {
            for (int index = 0; index < 18; index++)
            {
                Context.DocumentSections.Add(Section(
                    Id(20_000 + index),
                    _businessLogicalDocumentId,
                    await Context.DocumentVersions.SingleAsync(
                        version => version.Id == _businessDocumentVersionId),
                    Id(21_000 + index),
                    $"额外证据 {index + 1}",
                    $"额外的有界业务证据 {index + 1}。",
                    _businessSourceAnchorId,
                    [ExactCrossRepositoryTestValues.ConsumerDocumentVersionId],
                    index + 2));
            }

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        internal async Task ExceedAnchorMaximumAsync()
        {
            Guid[] additionalAnchorIds = Enumerable.Range(0, 30)
                .Select(index => Id(30_000 + index))
                .ToArray();
            Context.SourceAnchors.AddRange(additionalAnchorIds.Select((anchorId, index) =>
                new SourceAnchor(
                    anchorId,
                    _businessDocumentVersionId,
                    ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                    ExactCrossRepositoryTestValues.ConsumerScanRunId,
                    ExactCrossRepositoryTestValues.ConsumerCodeUnitId,
                    ExactCrossRepositoryTestValues.SourceHash[..40],
                    $"src/main/java/customer/Extra{index + 1}.java",
                    $"Extra{index + 1}.java#line=1;rule=prepared-flow",
                    1,
                    1,
                    ExactCrossRepositoryTestValues.SourceHash,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt)));
            await Context.SaveChangesAsync();
            string anchorIds = JsonSerializer.Serialize(
                new[] { _businessSourceAnchorId }.Concat(additionalAnchorIds));
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"DocumentSections\" SET \"SourceAnchorIdsJson\" = {anchorIds} WHERE \"Id\" = {Id(10_101)}");
            Context.ChangeTracker.Clear();
        }

        internal async Task ExceedFlowMaximumAsync()
        {
            for (int index = 0; index < 11; index++)
            {
                Context.FlowTraceSteps.Add(new FlowTraceStep(
                    Id(40_000 + index),
                    Id(10_302),
                    index + 3,
                    ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
                    ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                    ExactCrossRepositoryTestValues.ConsumerScanRunId,
                    null,
                    FlowTraceStepKind.Document,
                    $"额外流程步骤 {index + 1}。",
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));
            }

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        internal async Task ExceedDocumentMaximumAsync()
        {
            for (int index = 0; index < 3; index++)
            {
                Guid logicalDocumentId = Id(50_000 + index);
                Guid documentVersionId = Id(51_000 + index);
                LogicalDocument logicalDocument = LogicalDocument.Create(
                    logicalDocumentId,
                    ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                    DocumentType.BusinessLogic,
                    $"prepared/extra-business-{index + 1}",
                    ExactCrossRepositoryTestValues.CreatedAt);
                Context.LogicalDocuments.Add(logicalDocument);
                await Context.SaveChangesAsync();
                Context.DocumentVersions.Add(DocumentVersion.Create(
                    documentVersionId,
                    logicalDocumentId,
                    ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                    ExactCrossRepositoryTestValues.SystemId,
                    ExactCrossRepositoryTestValues.ConsumerScanRunId,
                    ExactCrossRepositoryTestValues.SourceHash[..40],
                    1,
                    $"额外业务文档 {index + 1}",
                    $"docs/extra-business-{index + 1}.md",
                    KnowledgeDocumentLimits.SchemaIdentity,
                    "{}",
                    $"额外业务内容 {index + 1}。",
                    ExactCrossRepositoryTestValues.ContentFingerprint,
                    ExactCrossRepositoryTestValues.CommitFingerprint,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));
                Context.DocumentEdges.Add(new DocumentEdge(
                    Id(52_000 + index),
                    documentVersionId,
                    ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                    ExactCrossRepositoryTestValues.ConsumerScanRunId,
                    ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
                    ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                    ExactCrossRepositoryTestValues.ConsumerScanRunId,
                    DocumentEdgeType.GeneratedFrom,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));
                await Context.SaveChangesAsync();
                await SetCurrentVersionAsync(Context, logicalDocumentId, documentVersionId);
            }

            Context.ChangeTracker.Clear();
        }

        internal static async Task<PreparedEvidenceFixture> CreateAsync()
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
                await Vulgata.Tests.PreparedPass2.ExactCrossRepositoryResolutionTests
                    .SeedAcceptedV2PairAsync(context, exactFixture, consumer, provider);
                await ApplyAcceptedV2AuthorityAsync(context, profile, occurrence, consumer, provider);
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
                        "业务概览",
                        "客户请求经由消费端调用提供端完成会话更新。",
                        _businessSourceAnchorId,
                        [consumer.DocumentVersion.Id]),
                    Section(
                        Id(10_102),
                        consumer.LogicalDocument.Id,
                        consumer.DocumentVersion,
                        Id(10_202),
                        "消费端",
                        "消费端调用 cust/updateSession。",
                        consumer.SourceAnchor.Id,
                        [provider.DocumentVersion.Id]),
                    Section(
                        Id(10_103),
                        provider.LogicalDocument.Id,
                        provider.DocumentVersion,
                        Id(10_203),
                        "提供端",
                        "提供端处理会话更新。",
                        provider.SourceAnchor.Id,
                        []));

                DocumentEdge referencesEdge = await context.DocumentEdges.SingleAsync(edge =>
                    edge.SourceDocumentVersionId == consumer.DocumentVersion.Id &&
                    edge.TargetDocumentVersionId == provider.DocumentVersion.Id &&
                    edge.EdgeType == DocumentEdgeType.References);
                CrossRepositoryResolutionDecision acceptedDecision = await context
                    .CrossRepositoryResolutionDecisions
                    .SingleAsync(decision => decision.DocumentEdgeId == referencesEdge.Id);
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
                // The accepted v2 helper already persisted and tracked the canonical REFERENCES edge.
                // Add only the fixture-owned GENERATED_FROM edge; re-adding REFERENCES violates the
                // unique (source,target,type) constraint under SQLite.
                context.DocumentEdges.Add(generatedFromEdge);
                context.ExactCrossRepositoryEdgeEvidence.Add(
                    new ExactCrossRepositoryEdgeEvidence(
                        referencesEdge.Id,
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
                context.Entry(flowGroup).Property(nameof(FlowGroup.SchemaIdentity)).CurrentValue =
                    "endpoint-flow-group/1.0";
                context.Entry(flowGroup).Property(nameof(FlowGroup.State)).CurrentValue = "Published";
                context.Entry(flowGroup).Property(nameof(FlowGroup.PlanFingerprint)).CurrentValue =
                    new string('a', 64);
                context.Entry(flowGroup).Property(nameof(FlowGroup.MembershipFingerprint)).CurrentValue =
                    new string('b', 64);
                context.Entry(flowGroup).Property(nameof(FlowGroup.ExactResolutionDecisionId)).CurrentValue =
                    acceptedDecision.Id;
                context.Entry(flowGroup).Property(nameof(FlowGroup.ExactResolutionEdgeId)).CurrentValue =
                    referencesEdge.Id;
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
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));
                context.FlowGroups.Add(flowGroup);
                await context.SaveChangesAsync();

                await SetCurrentVersionAsync(
                    context,
                    consumer.LogicalDocument.Id,
                    consumer.DocumentVersion.Id);
                await SetCurrentVersionAsync(
                    context,
                    provider.LogicalDocument.Id,
                    provider.DocumentVersion.Id);
                await SetCurrentVersionAsync(context, businessDocument.Id, businessVersion.Id);
                context.ChangeTracker.Clear();
                return new PreparedEvidenceFixture(
                    exactFixture,
                    context,
                    referencesEdge.Id,
                    acceptedDecision.EvidenceFingerprint,
                    acceptedDecision.ProviderEvidenceSetFingerprint);
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
            string heading,
            string contentMarkdown,
            Guid anchorId,
            IReadOnlyCollection<Guid> supportedDocumentVersionIds,
            int sectionOrder = 1) =>
            DocumentSection.Create(
                id,
                logicalDocumentId,
                version.Id,
                version.RepositoryId,
                stableSectionId,
                sectionOrder,
                DocumentSectionKind.Overview,
                heading,
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

        private static Guid Id(int value) =>
            Guid.Parse($"11500000-0000-0000-0000-{value:D12}");
    }
}
