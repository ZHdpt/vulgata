using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T033 pure publication-boundary tests.  The direct calls exercise the
/// approved T036 committer validation seam with the authoritative T020
/// persisted fixture.  T037 materialization remains a deliberately separate
/// causal RED until the same operation returns Committed and a version id.
/// </summary>
public sealed class Pass2DocumentCommitTests
{
    [Fact]
    public async Task CommitAsync_ValidCandidate_DerivesAllAndOnlyUsedGeneratedFromTargetsAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();

        Pass2BusinessLogicCommitResult result = await fixture.Committer.CommitAsync(
            fixture.CreateRequest(fixture.CandidateHandleSet),
            CancellationToken.None);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, result.Status);
        Guid committedVersionId = Assert.IsType<Guid>(result.DocumentVersionId);
        Assert.Equal(
            new[] { fixture.ConsumerDocumentVersionId, fixture.RetrievedDocumentVersionId }
                .Order(),
            result.UsedDocumentVersionIds);

        DocumentVersion committed = await fixture.Context.DocumentVersions
            .SingleAsync(value => value.Id == committedVersionId);
        Assert.Equal(DocumentType.BusinessLogic, await fixture.Context.LogicalDocuments
            .Where(value => value.Id == committed.LogicalDocumentId)
            .Select(value => value.DocumentType)
            .SingleAsync());
        Assert.Equal(KnowledgeDocumentLimits.SchemaIdentity, committed.SchemaIdentity);
        Assert.NotEmpty(committed.ContentJson);
        Assert.NotEmpty(committed.ContentMarkdown);
        Assert.Equal(
            new[] { fixture.ConsumerDocumentVersionId, fixture.RetrievedDocumentVersionId }.Order(),
            await fixture.Context.DocumentEdges
                .Where(value => value.SourceDocumentVersionId == committedVersionId &&
                    value.EdgeType == DocumentEdgeType.GeneratedFrom)
                .OrderBy(value => value.TargetDocumentVersionId)
                .Select(value => value.TargetDocumentVersionId)
                .ToArrayAsync());
        FlowGroup publishedGroup = await fixture.Context.FlowGroups
            .SingleAsync(value => value.Id == fixture.FlowGroupId);
        Assert.Equal("Published", publishedGroup.State);
        Assert.Equal(committedVersionId, publishedGroup.BusinessLogicDocumentVersionId);
        Pass2JobExecution completedJob = await fixture.Context.Pass2JobExecutions
            .SingleAsync(value => value.Id == fixture.Job.Id);
        Assert.Equal(Pass2JobExecutionState.Completed, completedJob.State);
        Assert.Equal(committedVersionId, completedJob.CommittedDocumentVersionId);
    }

    [Fact]
    public async Task CommitAsync_InitialEvidenceCurrentnessMutation_IsRejectedWithoutWritesAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        await fixture.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = NULL WHERE \"Id\" = {fixture.ConsumerLogicalDocumentId}");
        fixture.Context.ChangeTracker.Clear();
        string before = await fixture.GraphSnapshotAsync();

        Pass2BusinessLogicCommitResult result = await fixture.Committer.CommitAsync(
            fixture.CreateRequest([fixture.ConsumerHandle]),
            CancellationToken.None);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Null(result.DocumentVersionId);
        Assert.Equal(before, await fixture.GraphSnapshotAsync());
    }

    [Fact]
    public async Task CommitAsync_RetrievedEvidenceCurrentnessMutation_IsRejectedWithoutWritesAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        await fixture.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = NULL WHERE \"Id\" = {fixture.RetrievedLogicalDocumentId}");
        fixture.Context.ChangeTracker.Clear();
        string before = await fixture.GraphSnapshotAsync();

        Pass2BusinessLogicCommitResult result = await fixture.Committer.CommitAsync(
            fixture.CreateRequest([fixture.RetrievedHandle]),
            CancellationToken.None);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Null(result.DocumentVersionId);
        Assert.Equal(before, await fixture.GraphSnapshotAsync());
    }

    [Fact]
    public async Task CommitAsync_UnauthorizedEvidence_IsRejectedWithoutWritesAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        string before = await fixture.GraphSnapshotAsync();
        fixture.Authorization.IsAllowed = false;

        Pass2BusinessLogicCommitResult result = await fixture.Committer.CommitAsync(
            fixture.CreateRequest([fixture.ConsumerHandle]),
            CancellationToken.None);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal(before, await fixture.GraphSnapshotAsync());
    }

    [Fact]
    public async Task CommitAsync_MismatchedProfiledRevision_IsRejectedWithoutWritesAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        await fixture.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"DocumentVersions\" SET \"ProfiledRevision\" = {new string('f', 40)} WHERE \"Id\" = {fixture.ConsumerDocumentVersionId}");
        fixture.Context.ChangeTracker.Clear();
        string before = await fixture.GraphSnapshotAsync();

        Pass2BusinessLogicCommitResult result = await fixture.Committer.CommitAsync(
            fixture.CreateRequest([fixture.ConsumerHandle]),
            CancellationToken.None);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal(before, await fixture.GraphSnapshotAsync());
    }

    [Fact]
    public async Task CommitAsync_ChangedProviderBinding_IsRejectedWithoutWritesAsync()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        await fixture.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"FlowGroups\" SET \"ProviderXmlDocumentVersionId\" = {fixture.ProviderImplementationDocumentVersionId} WHERE \"Id\" = {fixture.FlowGroupId}");
        fixture.Context.ChangeTracker.Clear();
        string before = await fixture.GraphSnapshotAsync();

        Pass2BusinessLogicCommitResult result = await fixture.Committer.CommitAsync(
            fixture.CreateRequest([fixture.ConsumerHandle]),
            CancellationToken.None);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal(before, await fixture.GraphSnapshotAsync());
    }

    private sealed class CommitFixture : IAsyncDisposable
    {
        private const string PromptIdentity = "pass2-business-logic-worker.v1";
        private const string SubmissionSchemaIdentity = "business-logic/flow-v1";

        private CommitFixture(
            ExactCrossRepositoryTestFixture fixture,
            VulgataDbContext context,
            Pass2BusinessLogicDocumentCommitter committer,
            ToggleAuthorizationVerifier authorization,
            Pass2JobExecution job,
            FlowGroup group,
            Pass2Execution execution,
            CrossRepositoryResolutionDecision decision,
            DocumentEdge edge,
            ExactResolutionProviderEvidenceContribution[] contributions,
            ExactPass1DocumentFixture consumer,
            ExactPass1DocumentFixture providerXml,
            ExactPass1DocumentFixture providerImplementation,
            ExactPass1DocumentFixture retrieved,
            string consumerAnchorHandle,
            string relationshipHandle)
        {
            Fixture = fixture;
            Context = context;
            Committer = committer;
            Authorization = authorization;
            Job = job;
            Group = group;
            Execution = execution;
            Decision = decision;
            Edge = edge;
            Contributions = contributions;
            Consumer = consumer;
            ProviderXml = providerXml;
            ProviderImplementation = providerImplementation;
            Retrieved = retrieved;
            ConsumerHandle = consumerAnchorHandle;
            RetrievedHandle = ExactCrossRepositoryTestValues.Id(90_002).ToString("D");
            ConsumerAnchorHandle = consumerAnchorHandle;
            RelationshipHandle = relationshipHandle;
            CandidateHandleSet = new[] { ConsumerHandle, RetrievedHandle };
        }

        internal ExactCrossRepositoryTestFixture Fixture { get; }
        internal VulgataDbContext Context { get; }
        internal Pass2BusinessLogicDocumentCommitter Committer { get; }
        internal ToggleAuthorizationVerifier Authorization { get; }
        internal Pass2JobExecution Job { get; }
        internal FlowGroup Group { get; }
        internal Pass2Execution Execution { get; }
        internal CrossRepositoryResolutionDecision Decision { get; }
        internal DocumentEdge Edge { get; }
        internal ExactResolutionProviderEvidenceContribution[] Contributions { get; }
        internal ExactPass1DocumentFixture Consumer { get; }
        internal ExactPass1DocumentFixture ProviderXml { get; }
        internal ExactPass1DocumentFixture ProviderImplementation { get; }
        internal ExactPass1DocumentFixture Retrieved { get; }
        internal string ConsumerHandle { get; }
        internal string ConsumerAnchorHandle { get; }
        internal string RelationshipHandle { get; }
        internal string RetrievedHandle { get; }
        internal string[] CandidateHandleSet { get; }

        internal Guid FlowGroupId => Group.Id;
        internal Guid ConsumerDocumentVersionId => Consumer.DocumentVersion.Id;
        internal Guid ProviderImplementationDocumentVersionId => ProviderImplementation.DocumentVersion.Id;
        internal Guid ConsumerLogicalDocumentId => Consumer.LogicalDocument.Id;
        internal Guid RetrievedDocumentVersionId => Retrieved.DocumentVersion.Id;
        internal Guid RetrievedLogicalDocumentId => Retrieved.LogicalDocument.Id;

        internal static async Task<CommitFixture> CreateAsync()
        {
            ExactCrossRepositoryTestFixture fixture =
                await ExactCrossRepositoryTestFixture.CreateAsync();
            VulgataDbContext context = fixture.Database.CreateContext();
            try
            {
                MethodInfo seedMethod = typeof(Pass2FlowPlanningPostgreSqlTests)
                    .GetMethod("SeedPass2Async", BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("The accepted T020 seed is unavailable.");
                object invocation = seedMethod.Invoke(null, [context, fixture])
                    ?? throw new InvalidOperationException("The accepted T020 seed returned null.");
                if (invocation is not Task seedTask)
                {
                    throw new InvalidOperationException("The accepted T020 seed is not asynchronous.");
                }

                await seedTask;
                object seed = seedTask.GetType().GetProperty("Result")?.GetValue(seedTask)
                    ?? throw new InvalidOperationException("The accepted T020 seed result is unavailable.");
                await CanonicalizeExactAuthorityAsync(context);
                Pass1PairExecution pair = GetProperty<Pass1PairExecution>(seed, "Pair");
                ExactPass1DocumentFixture consumer =
                    GetProperty<ExactPass1DocumentFixture>(seed, "Consumer");
                ExactPass1DocumentFixture providerXml =
                    GetProperty<ExactPass1DocumentFixture>(seed, "ProviderXml");
                ExactPass1DocumentFixture providerImplementation =
                    GetProperty<ExactPass1DocumentFixture>(seed, "ProviderImplementation");
                ExactPass1DocumentFixture retrieved =
                    GetProperty<ExactPass1DocumentFixture>(seed, "Unsupported");
                ToggleAuthorizationVerifier authorization = new();
                RepositoryAccessScope scope = new(
                    [consumer.DocumentVersion.RepositoryId, providerXml.DocumentVersion.RepositoryId],
                    authorization);
                PreparedPass2StartResult start = await Pass2PartialCompletionTests.CreateAuthorizedStore(context)
                    .StartAsync(pair.Id, scope);
                Assert.Equal(PreparedPass2StartOutcome.Created, start.Outcome);
                Pass2JobExecution job = await context.Pass2JobExecutions.SingleAsync();
                DateTimeOffset leaseAcquiredAt = DateTimeOffset.UtcNow;
                job.AcquireLease(
                    ExactCrossRepositoryTestValues.Id(90_010),
                    leaseAcquiredAt.AddMinutes(5),
                    leaseAcquiredAt);
                await context.SaveChangesAsync();

                Pass2RetrievalReceipt receipt = Pass2RetrievalReceipt.Create(
                    ExactCrossRepositoryTestValues.Id(90_011),
                    job.Id,
                    1,
                    1,
                    Hash("tool-call"),
                    Pass2RetrievalReceiptState.Accepted,
                    Pass2CodeLogicRetrievalOperation.Search,
                    Hash("request"),
                    1,
                    1,
                    1,
                    Hash("result"),
                    ExactCrossRepositoryTestValues.CreatedAt);
                Guid retrievedResultId = ExactCrossRepositoryTestValues.Id(90_002);
                string retrievedDescriptorFingerprint = Hash(string.Join(
                    '\n',
                    "pass2-retrieval-descriptor/1.0",
                    Pass2CodeLogicRetrievalItemKind.Document.ToString(),
                    retrieved.DocumentVersion.Id.ToString("D"),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty));
                string retrievedEvidenceFingerprint = Hash(string.Join(
                    '\n',
                    "pass2-retrieval-result/1.0",
                    retrievedResultId.ToString("D"),
                    Pass2CodeLogicRetrievalItemKind.Document.ToString(),
                    retrieved.DocumentVersion.Id.ToString("D"),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "1"));
                Pass2RetrievalResult retrievedResult = Pass2RetrievalResult.CreateAdmitted(
                    retrievedResultId,
                    receipt.Id,
                    job.Id,
                    1,
                    1,
                    Pass2CodeLogicRetrievalItemKind.Document,
                    retrieved.DocumentVersion.Id,
                    retrieved.DocumentVersion.RepositoryId,
                    retrieved.DocumentVersion.ScanRunId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    retrievedDescriptorFingerprint,
                    retrievedEvidenceFingerprint,
                    1,
                    ExactCrossRepositoryTestValues.CreatedAt);
                context.Pass2RetrievalReceipts.Add(receipt);
                context.Pass2RetrievalResults.Add(retrievedResult);
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();

                job = await context.Pass2JobExecutions.SingleAsync();
                FlowGroup group = await context.FlowGroups.SingleAsync(value => value.Id == job.FlowGroupId);
                Pass2Execution execution = await context.Pass2Executions.SingleAsync(value => value.Id == job.Pass2ExecutionId);
                CrossRepositoryResolutionDecision decision = await context.CrossRepositoryResolutionDecisions
                    .SingleAsync(value => value.Id == group.ExactResolutionDecisionId);
                DocumentEdge edge = await context.DocumentEdges.SingleAsync(value => value.Id == group.ExactResolutionEdgeId);
                SourceAnchor consumerAnchor = await context.SourceAnchors.SingleAsync(
                    value => value.DocumentVersionId == consumer.DocumentVersion.Id);
                FlowTraceStep relationshipStep = await context.FlowTraceSteps.SingleAsync(
                    value => value.EdgeId == edge.Id);
                DocumentVersion relationshipDocument = await context.DocumentVersions.SingleAsync(
                    value => value.Id == relationshipStep.DocumentVersionId);
                ExactResolutionProviderEvidenceContribution[] contributions = await context
                    .ExactResolutionProviderEvidenceContributions
                    .Where(value => value.ResolutionDecisionId == decision.Id)
                    .OrderBy(value => value.ProviderOrdinal).ThenBy(value => value.EvidenceOrdinal)
                    .ToArrayAsync();
                return new(
                    fixture,
                    context,
                    new Pass2BusinessLogicDocumentCommitter(
                        context,
                        scope,
                        new TestWorkerContractAuthority(
                            Pass2PartialCompletionTests.CreateWorkerContractAuthority())),
                    authorization,
                    job,
                    group,
                    execution,
                    decision,
                    edge,
                    contributions,
                    consumer,
                    providerXml,
                    providerImplementation,
                    retrieved,
                    Pass2InitialEvidenceHandle.Create(
                        job.Id,
                        job.AttemptCount,
                        Pass2CodeLogicRetrievalItemKind.SourceAnchor,
                        consumer.DocumentVersion.Id,
                        consumer.DocumentVersion.RepositoryId,
                        consumer.DocumentVersion.ScanRunId,
                        consumer.DocumentVersion.ProfiledRevision,
                        sourceAnchorId: consumerAnchor.Id,
                        byteStart: consumerAnchor.ByteStart,
                        byteEnd: consumerAnchor.ByteEnd,
                        sourceHash: consumerAnchor.SourceHash),
                    Pass2InitialEvidenceHandle.Create(
                        job.Id,
                        job.AttemptCount,
                        Pass2CodeLogicRetrievalItemKind.Relationship,
                        relationshipDocument.Id,
                        relationshipDocument.RepositoryId,
                        relationshipDocument.ScanRunId,
                        relationshipDocument.ProfiledRevision,
                        edgeId: edge.Id));
            }
            catch
            {
                await context.DisposeAsync();
                await fixture.DisposeAsync();
                throw;
            }
        }

        internal Pass2BusinessLogicCommitRequest CreateRequest(
            IReadOnlyCollection<string> handles)
        {
            PreparedPass2WorkerContractAuthority workerContract =
                Pass2PartialCompletionTests.CreateWorkerContractAuthority();
            return new(
                Job.Id,
                Job.LeaseToken!.Value,
                Job.AttemptCount,
                CreateCandidate(handles),
                [
                    new(
                        ConsumerHandle,
                        Consumer.DocumentVersion.Id,
                        Consumer.DocumentVersion.RepositoryId,
                        Consumer.DocumentVersion.ScanRunId,
                        Consumer.DocumentVersion.ProfiledRevision,
                        SourceAnchorId: null),
                ],
                PromptIdentity)
            {
                PromptFingerprint = workerContract.PromptFingerprint,
                SubmissionSchemaIdentity = workerContract.SubmissionSchemaIdentity,
                SubmissionSchemaFingerprint = workerContract.SubmissionSchemaFingerprint,
                ToolDeclarationFingerprint = workerContract.ToolDeclarationFingerprint,
            };
        }

        internal async Task<string> GraphSnapshotAsync()
        {
            int documents = await Context.DocumentVersions.CountAsync();
            int edges = await Context.DocumentEdges.CountAsync();
            int groups = await Context.FlowGroups.CountAsync();
            int jobs = await Context.Pass2JobExecutions.CountAsync();
            return $"{documents}:{edges}:{groups}:{jobs}";
        }

        private JsonElement CreateCandidate(IReadOnlyCollection<string> handles)
        {
            bool includeRetrieved = handles.Contains(RetrievedHandle, StringComparer.Ordinal);
            string source = ConsumerAnchorHandle;
            string supporting = includeRetrieved ? RetrievedHandle : source;
            var candidate = new
            {
                schemaIdentity = SubmissionSchemaIdentity,
                binding = new
                {
                    jobId = Job.Id,
                    pass2ExecutionId = Execution.Id,
                    flowGroupId = Group.Id,
                    systemId = Execution.SystemId,
                    consumerRepositoryId = Group.RepositoryId,
                    providerRepositoryId = Group.ProviderRepositoryId!.Value,
                    protocol = Group.Protocol!.Value.ToString(),
                    normalizedEndpointIdentifierFingerprint = Hash(Group.NormalizedEndpointIdentifier!),
                    consumerEntryStableKey = Group.ConsumerEntryStableKey!,
                    logicalKeySchemaIdentity = "business-flow-logical-key/1.0",
                    logicalDocumentStableKey = Group.LogicalDocumentStableKey,
                    exactResolutionDecisionId = Decision.Id,
                    exactResolutionDecisionFingerprint = Decision.EvidenceFingerprint,
                    exactResolutionEdgeId = Edge.Id,
                    providerEvidenceFingerprint = Hash(
                        string.Join("\n", new[] { "provider-evidence-set/1.0" }
                            .Concat(Contributions.OrderBy(value => value.ProviderOrdinal)
                                .ThenBy(value => value.EvidenceOrdinal)
                                .Select(value => value.EvidenceFingerprint)))),
                    planFingerprint = Group.PlanFingerprint,
                    membershipFingerprint = Group.MembershipFingerprint!,
                    promptIdentity = PromptIdentity,
                    submissionSchemaIdentity = SubmissionSchemaIdentity,
                },
                title = "Validated business flow",
                summary = new { description = "The validated flow is supported.", evidenceHandleRefs = new[] { source } },
                participants = new[]
                {
                    new { order = 1, role = "Consumer", name = "Consumer", responsibility = "Starts the flow.", evidenceHandleRefs = new[] { source } },
                    new { order = 2, role = "Supporting", name = "Supporting", responsibility = "Supports the flow.", evidenceHandleRefs = new[] { supporting } },
                },
                flowSteps = new[]
                {
                    new { order = 1, actor = "Consumer", action = "Sends the request.", outcome = "The request is handled.", relationshipKind = "References", evidenceHandleRefs = new[] { RelationshipHandle } },
                },
                businessRules = Array.Empty<object>(),
                dataMovements = Array.Empty<object>(),
                failurePaths = Array.Empty<object>(),
                unresolvedStatements = Array.Empty<object>(),
            };
            return JsonSerializer.SerializeToElement(
                candidate,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Fixture.DisposeAsync();
        }

        private static T GetProperty<T>(object source, string name) =>
            (T)(source.GetType().GetProperty(name)?.GetValue(source)
                ?? throw new InvalidOperationException($"Seed property '{name}' is unavailable."));

        private static string Hash(string value) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

        private static async Task CanonicalizeExactAuthorityAsync(VulgataDbContext context)
        {
            CrossRepositoryResolutionDecision seededDecision =
                await context.CrossRepositoryResolutionDecisions
                    .SingleAsync(value => value.Outcome == "ExactMatch");
            ExactResolutionProviderEvidenceContribution[] seededContributions =
                await context.ExactResolutionProviderEvidenceContributions.AsNoTracking()
                    .Where(value => value.ResolutionDecisionId == seededDecision.Id)
                    .OrderBy(value => value.ProviderOrdinal).ThenBy(value => value.EvidenceOrdinal)
                    .ToArrayAsync();
            Assert.Equal("exact-cross-repository-resolution/2.0", seededDecision.ResolutionSchemaIdentity);
            Assert.Equal(
                ExactResolutionCanonicalAuthority.DecisionId(seededDecision.EvidenceFingerprint),
                seededDecision.Id);
            Assert.Matches("^[0-9a-f]{64}$", seededDecision.ConsumerEntryStableKey);
            Assert.NotEmpty(seededContributions);
            Assert.Empty(await context.ExactCrossRepositoryEdgeEvidence.AsNoTracking().ToArrayAsync());
            Assert.True(await context.DocumentEdges.AsNoTracking().AnyAsync(value =>
                value.Id == seededDecision.DocumentEdgeId &&
                value.EdgeType == DocumentEdgeType.References));
            foreach (ExactResolutionProviderEvidenceContribution seededContribution in seededContributions)
            {
                string fingerprint = ExactResolutionCanonicalAuthority.PersistedProviderContributionFingerprint(
                    seededDecision.EvidenceFingerprint,
                    seededContribution);
                Assert.Equal(fingerprint, seededContribution.EvidenceFingerprint);
                Assert.Equal(
                    ExactResolutionCanonicalAuthority.ProviderContributionId(fingerprint),
                    seededContribution.Id);
            }
            context.ChangeTracker.Clear();
        }
    }

    private sealed class ToggleAuthorizationVerifier : IRepositoryAuthorizationVerifier
    {
        internal bool IsAllowed { get; set; } = true;

        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(IsAllowed);
    }

    private sealed class TestWorkerContractAuthority(
        PreparedPass2WorkerContractAuthority contract) :
        IPreparedPass2WorkerContractAuthority
    {
        public PreparedPass2WorkerContractAuthority GetCurrent() => contract;
    }
}
