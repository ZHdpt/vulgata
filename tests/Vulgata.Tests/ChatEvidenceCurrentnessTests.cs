using System.Data.Common;
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
            original.Relationships);

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
        await AssertMissingAfterProviderWindowMutationAsync(fixture => fixture.CommitReferencesEvidenceLossAsync());
        await AssertMissingAfterProviderWindowMutationAsync(fixture => fixture.CommitReferencesProfileChangeAsync());
        await AssertMissingAfterProviderWindowMutationAsync(fixture => fixture.CommitReferencesDirectionChangeAsync());
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
        Assert.Equal(ChatCurrentnessValidationOutcome.MissingOrStale, outcome);
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
        VulgataDbContext context) : IAsyncDisposable
    {
        internal static string ProfileFingerprint =>
            new ExactResolutionProfileCatalog().Load().ProfileFingerprint;

        private const string _evidenceFingerprint =
            "4123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private static readonly Guid _businessLogicalDocumentId = Id(10_001);
        private static readonly Guid _businessDocumentVersionId = Id(10_002);
        private static readonly Guid _businessSourceAnchorId = Id(10_003);
        private static readonly Guid _referencesEdgeId = Id(10_004);
        private static readonly Guid _generatedFromEdgeId = Id(10_005);

        internal VulgataDbContext Context { get; } = context;

        internal VulgataDbContext CreateContext(params IInterceptor[] interceptors) =>
            exactFixture.Database.CreateContext(null, interceptors);

        internal IReadOnlyCollection<Guid> RepositoryIds { get; } =
        [
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
        ];

        internal async Task<EvidencePacket> RetrieveAsync()
        {
            var scope = new RepositoryAccessScope(RepositoryIds);
            ChatQuestionRequest request = ChatQuestionRequest.Create(
                "currentness-request",
                ChatMode.Business,
                ExactCrossRepositoryTestValues.SystemId,
                RepositoryIds,
                "客户会话如何跨仓库更新？");
            ChatEvidenceResult result = await new ChatEvidenceRetriever(Context)
                .RetrieveAsync(request, scope);
            return Assert.IsType<EvidencePacket>(result.Packet);
        }

        internal static ChatCurrentnessValidationRequest CreateValidationRequest(
            EvidencePacket packet)
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
                    document.DocumentVersionId)).ToArray(),
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
                packet.Relationships.Select(relationship =>
                {
                    EvidenceDocument source = documents[relationship.SourceDocumentHandle];
                    EvidenceDocument target = documents[relationship.TargetDocumentHandle];
                    return new ChatCurrentRelationship(
                        relationship.RelationshipIdentity,
                        relationship.RelationshipType,
                        source.DocumentVersionId,
                        target.DocumentVersionId,
                        source.RepositoryId,
                        target.RepositoryId,
                        relationship.ProfileFingerprint);
                }).ToArray());
        }

        internal Task CommitSectionTextChangeAsync() => CommitSqlAsync(
            $"UPDATE \"DocumentSections\" SET \"ContentMarkdown\" = '篡改后的持久化内容。' WHERE \"StableSectionId\" = '{Id(10_201)}'");

        internal Task CommitAnchorLossAsync() => CommitSqlAsync(
            $"DELETE FROM \"SourceAnchors\" WHERE \"Id\" = '{_businessSourceAnchorId}'");

        internal Task CommitAnchorRangeChangeAsync() => CommitSqlAsync(
            $"UPDATE \"SourceAnchors\" SET \"LineStart\" = 13, \"LineEnd\" = 19 WHERE \"Id\" = '{_businessSourceAnchorId}'");

        internal Task CommitGeneratedFromLossAsync() => CommitSqlAsync(
            $"DELETE FROM \"DocumentEdges\" WHERE \"Id\" = '{_generatedFromEdgeId}'");

        internal Task CommitReferencesEvidenceLossAsync() => CommitSqlAsync(
            $"DELETE FROM \"ExactCrossRepositoryEdgeEvidence\" WHERE \"EdgeId\" = '{_referencesEdgeId}'");

        internal Task CommitReferencesProfileChangeAsync() => CommitSqlAsync(
            $"UPDATE \"ExactCrossRepositoryEdgeEvidence\" SET \"ProfileFingerprint\" = '5123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef' WHERE \"EdgeId\" = '{_referencesEdgeId}'");

        internal Task CommitReferencesDirectionChangeAsync() => CommitSqlAsync(
            $"UPDATE \"ExactCrossRepositoryEdgeEvidence\" SET \"ConsumerSourceAnchorId\" = '{ExactCrossRepositoryTestValues.ProviderSourceAnchorId}', \"ConsumerDocumentVersionId\" = '{ExactCrossRepositoryTestValues.ProviderDocumentVersionId}', \"ConsumerRepositoryId\" = '{ExactCrossRepositoryTestValues.ProviderRepositoryId}', \"ConsumerScanRunId\" = '{ExactCrossRepositoryTestValues.ProviderScanRunId}', \"ProviderSourceAnchorId\" = '{ExactCrossRepositoryTestValues.ConsumerSourceAnchorId}', \"ProviderDocumentVersionId\" = '{ExactCrossRepositoryTestValues.ConsumerDocumentVersionId}', \"ProviderRepositoryId\" = '{ExactCrossRepositoryTestValues.ConsumerRepositoryId}', \"ProviderScanRunId\" = '{ExactCrossRepositoryTestValues.ConsumerScanRunId}' WHERE \"EdgeId\" = '{_referencesEdgeId}'");

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
            await mutationContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"DocumentVersions\" SET \"SupersededById\" = {successorId} WHERE \"Id\" = {ExactCrossRepositoryTestValues.ProviderDocumentVersionId}");
            await mutationContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {successorId} WHERE \"Id\" = {ExactCrossRepositoryTestValues.ProviderLogicalDocumentId}");
        }

        private async Task CommitSqlAsync(string sql)
        {
            await using VulgataDbContext mutationContext = exactFixture.Database.CreateContext();
            _ = await mutationContext.Database.ExecuteSqlRawAsync(sql);
        }

        internal static async Task<CurrentnessFixture> CreateAsync()
        {
            ExactCrossRepositoryTestFixture exactFixture =
                await ExactCrossRepositoryTestFixture.CreateAsync();
            VulgataDbContext context = exactFixture.Database.CreateContext();
            try
            {
                ExactCrossRepositoryManagedFixture managed = exactFixture.CreateManagedRepositories();
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
                context.Systems.Add(managed.System);
                context.ScanRuns.AddRange(consumer.ScanRun, provider.ScanRun);
                await context.SaveChangesAsync();

                LogicalDocument consumerDocument = LogicalDocument.Create(
                    consumer.LogicalDocument.Id,
                    consumer.DocumentVersion.RepositoryId,
                    DocumentType.CodeLogic,
                    "prepared/consumer",
                    ExactCrossRepositoryTestValues.CreatedAt);
                LogicalDocument providerDocument = LogicalDocument.Create(
                    provider.LogicalDocument.Id,
                    provider.DocumentVersion.RepositoryId,
                    DocumentType.CodeLogic,
                    "prepared/provider",
                    ExactCrossRepositoryTestValues.CreatedAt);
                LogicalDocument businessDocument = LogicalDocument.Create(
                    _businessLogicalDocumentId,
                    consumer.DocumentVersion.RepositoryId,
                    DocumentType.BusinessLogic,
                    "prepared/business-flow",
                    ExactCrossRepositoryTestValues.CreatedAt);
                context.LogicalDocuments.AddRange(
                    consumerDocument,
                    providerDocument,
                    businessDocument);
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
                context.DocumentVersions.AddRange(
                    consumer.DocumentVersion,
                    provider.DocumentVersion,
                    businessVersion);
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

                var referencesEdge = new DocumentEdge(
                    _referencesEdgeId,
                    consumer.DocumentVersion.Id,
                    consumer.DocumentVersion.RepositoryId,
                    consumer.DocumentVersion.ScanRunId,
                    provider.DocumentVersion.Id,
                    provider.DocumentVersion.RepositoryId,
                    provider.DocumentVersion.ScanRunId,
                    DocumentEdgeType.References,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt);
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
                context.DocumentEdges.AddRange(referencesEdge, generatedFromEdge);
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
                        _evidenceFingerprint,
                        ExactCrossRepositoryTestValues.Pass1CompletedAt));

                var flowGroup = new FlowGroup(
                    Id(10_301),
                    businessVersion.Id,
                    businessVersion.RepositoryId,
                    businessVersion.ScanRunId,
                    1,
                    "客户会话更新",
                    ExactCrossRepositoryTestValues.Pass1CompletedAt);
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

                await SetCurrentVersionAsync(context, consumerDocument.Id, consumer.DocumentVersion.Id);
                await SetCurrentVersionAsync(context, providerDocument.Id, provider.DocumentVersion.Id);
                await SetCurrentVersionAsync(context, businessDocument.Id, businessVersion.Id);
                context.ChangeTracker.Clear();
                return new CurrentnessFixture(exactFixture, context);
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

        internal static string Hash(string value) => Sha256(value);

        private static string Sha256(string value) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

        private static Guid Id(int value) =>
            Guid.Parse($"11510000-0000-0000-0000-{value:D12}");
    }
}
