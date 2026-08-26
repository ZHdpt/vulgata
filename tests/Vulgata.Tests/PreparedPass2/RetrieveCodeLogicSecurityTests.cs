using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T025 security/currentness REDs use the accepted T026 authoritative pair
/// fixture and mutate one persisted fact per case. Handle-dependent vectors
/// remain deferred because the current DTO has no way to issue a valid opaque
/// current-attempt handle.
/// </summary>
public sealed class RetrieveCodeLogicSecurityTests
{
    [Theory]
    [InlineData("stale")]
    [InlineData("superseded")]
    [InlineData("cross-system")]
    [InlineData("profiled-revision")]
    public async Task RetrieveAsync_PersistedSecurityMutation_ReturnsClosedRefusalWithoutAuthorityMutationAsync(
        string mutation)
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync(mutation);

        string authorityBefore = await fixture.AuthoritySnapshotAsync();
        string persistedBefore = await fixture.MutationSnapshotAsync();
        await fixture.ApplyMutationAsync();
        string persistedAfter = await fixture.MutationSnapshotAsync();
        Assert.NotEqual(persistedBefore, persistedAfter);
        string mutatedFixture = await fixture.AuthoritySnapshotAsync();
        Assert.Equal(authorityBefore, mutatedFixture);

        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "ledger"));

        string authorityAfter = await fixture.AuthoritySnapshotAsync();
        Assert.Equal(authorityBefore, authorityAfter);
        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.DoesNotContain(mutation, result.Code, StringComparison.Ordinal);
        Assert.Equal(0, fixture.SourceReader.Calls);
    }

    [Fact]
    public async Task RetrieveAsync_RevokedServerScope_ReturnsClosedRefusalWithoutAuthorityMutationAsync()
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("revoked-scope");

        string authorityBefore = await fixture.AuthoritySnapshotAsync();
        Pass2CodeLogicRetrievalResult result = await fixture.ServiceWithRevokedScope
            .RetrieveAsync(
                fixture.JobExecutionId,
                new(
                    Pass2CodeLogicRetrievalOperation.Search,
                    queryText: "ledger"));

        Assert.Equal(authorityBefore, await fixture.AuthoritySnapshotAsync());
        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.SourceReader.Calls);
    }

    [Fact]
    public async Task RetrieveAsync_HandleIssuedForDifferentJob_IsContentDarkRefusalAsync()
    {
        await using PersistedRetrievalFixture owner =
            await PersistedRetrievalFixture.CreateAsync("none");
        await using PersistedRetrievalFixture otherJob =
            await PersistedRetrievalFixture.CreateAsync("cross-job");

        Pass2CodeLogicRetrievalItem section = await owner.IssueSectionHandleAsync();
        Guid otherJobId = await otherJob.Context.Pass2JobExecutions
            .Where(value => value.Id != otherJob.JobExecutionId)
            .Select(value => value.Id)
            .SingleAsync();
        Pass2CodeLogicRetrievalResult result = await otherJob.Service.RetrieveAsync(
            otherJobId,
            new(
                Pass2CodeLogicRetrievalOperation.Sections,
                opaqueHandles: [section.OpaqueHandle]));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.DoesNotContain(section.OpaqueHandle, result.Code, StringComparison.Ordinal);
        Assert.Equal(0, otherJob.SourceReader.Calls);
    }

    [Fact]
    public async Task RetrieveAsync_IssuedAnchorWithPersistedHashMismatch_IsContentDarkRefusalAsync()
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");
        Pass2CodeLogicRetrievalItem anchor = await fixture.IssueAnchorHandleAsync();
        string originalHash = await fixture.Context.SourceAnchors
            .Where(value => value.Id == anchor.SourceAnchorId)
            .Select(value => value.SourceHash)
            .SingleAsync();
        fixture.SourceReader.FixedSourceHash = originalHash;
        string before = await fixture.AnchorSnapshotAsync(anchor.SourceAnchorId!.Value);

        await fixture.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"SourceAnchors\" SET \"SourceHash\" = {new string('c', 64)} WHERE \"Id\" = {anchor.SourceAnchorId}");
        fixture.Context.ChangeTracker.Clear();

        string after = await fixture.AnchorSnapshotAsync(anchor.SourceAnchorId.Value);
        Assert.NotEqual(before, after);
        string authorityBefore = await fixture.AuthoritySnapshotAsync();
        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.SourceExcerpt,
                opaqueHandles: [anchor.OpaqueHandle],
                containedByteOffset: 0,
                containedByteLength: 6));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.Equal(1, fixture.SourceReader.Calls);
        Assert.DoesNotContain("ledger", result.Code, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(authorityBefore, await fixture.AuthoritySnapshotAsync());
    }

    [Fact]
    public async Task RetrieveAsync_IssuedAnchorWithPersistedRangeMismatch_IsContentDarkRefusalAsync()
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");
        Pass2CodeLogicRetrievalItem anchor = await fixture.IssueAnchorHandleAsync();
        string before = await fixture.AnchorSnapshotAsync(anchor.SourceAnchorId!.Value);

        await fixture.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"SourceAnchors\" SET \"ByteStart\" = 10, \"ByteEnd\" = 16 WHERE \"Id\" = {anchor.SourceAnchorId}");
        fixture.Context.ChangeTracker.Clear();

        string after = await fixture.AnchorSnapshotAsync(anchor.SourceAnchorId.Value);
        Assert.NotEqual(before, after);
        string authorityBefore = await fixture.AuthoritySnapshotAsync();
        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.SourceExcerpt,
                opaqueHandles: [anchor.OpaqueHandle],
                containedByteOffset: 0,
                containedByteLength: 6));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.SourceReader.Calls);
        Assert.Equal(authorityBefore, await fixture.AuthoritySnapshotAsync());
    }

    [Fact]
    public async Task RetrieveAsync_RelatedDocuments_StopsAtOneReferencesHopAndExcludesNonReferencesAsync()
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");
        Pass2CodeLogicRetrievalItem document = await fixture.IssueDocumentHandleAsync();
        Guid implementationId = await fixture.Context.FlowGroups
            .Where(value => value.Pass2ExecutionId == fixture.ExecutionId)
            .Select(value => value.ProviderImplementationDocumentVersionId!.Value)
            .SingleAsync();
        DocumentVersion consumer = await fixture.Context.DocumentVersions
            .SingleAsync(value => value.Id == document.DocumentVersionId);
        DocumentVersion implementation = await fixture.Context.DocumentVersions
            .SingleAsync(value => value.Id == implementationId);
        DocumentEdge nonReference = new(
            ExactCrossRepositoryTestValues.Id(41_001),
            consumer.Id,
            consumer.RepositoryId,
            consumer.ScanRunId,
            implementation.Id,
            implementation.RepositoryId,
            implementation.ScanRunId,
            DocumentEdgeType.GeneratedFrom,
            ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(1));
        fixture.Context.DocumentEdges.Add(nonReference);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        string authorityBefore = await fixture.AuthoritySnapshotAsync();

        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.RelatedDocuments,
                opaqueHandles: [document.OpaqueHandle],
                direction: Pass2RelatedDocumentDirection.Outgoing));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, result.Outcome);
        Pass2CodeLogicRetrievalItem related = Assert.Single(result.Items);
        Assert.Equal(Pass2CodeLogicRetrievalItemKind.Relationship, related.Kind);
        Assert.NotEqual(implementationId, related.DocumentVersionId);
        Assert.DoesNotContain(result.Items, value => value.DocumentVersionId == implementationId);
        Assert.Equal(authorityBefore, await fixture.AuthoritySnapshotAsync());
    }

    [Fact]
    public async Task RetrieveAsync_UnknownUnauthorizedAndStaleRequestsHaveEquivalentContentDarkRefusalAsync()
    {
        await using PersistedRetrievalFixture owner =
            await PersistedRetrievalFixture.CreateAsync("none");
        await using PersistedRetrievalFixture stale =
            await PersistedRetrievalFixture.CreateAsync("stale");
        Pass2CodeLogicRetrievalItem issued = await owner.IssueSectionHandleAsync();
        await stale.ApplyMutationAsync();

        Pass2CodeLogicRetrievalResult unknown = await owner.Service.RetrieveAsync(
            owner.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.Sections,
                opaqueHandles: ["opaque-handle-never-issued"]));
        Pass2CodeLogicRetrievalResult unauthorized = await stale.Service.RetrieveAsync(
            stale.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.Sections,
                opaqueHandles: [issued.OpaqueHandle]));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, unknown.Outcome);
        Assert.Equal(unknown.Outcome, unauthorized.Outcome);
        Assert.Equal(unknown.Code, unauthorized.Code);
        Assert.Equal(unknown.RemainingBudget, unauthorized.RemainingBudget);
        Assert.Empty(unknown.Items);
        Assert.Empty(unauthorized.Items);
        Assert.DoesNotContain("opaque-handle-never-issued", unknown.Code, StringComparison.Ordinal);
        Assert.DoesNotContain(issued.OpaqueHandle, unauthorized.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void RetrieveCodeLogic_RequestCannotSupplyAuthorityMutationFields()
    {
        string[] forbiddenFragments = ["Provider", "Group", "System", "Repository", "Revision"];
        Assert.DoesNotContain(
            typeof(Pass2CodeLogicRetrievalRequest).GetProperties(),
            property => forbiddenFragments.Any(fragment =>
                property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class PersistedRetrievalFixture : IAsyncDisposable
    {
        private readonly string _mutation;

        private PersistedRetrievalFixture(
            ExactCrossRepositoryTestFixture fixture,
            VulgataDbContext context,
            Guid jobExecutionId,
            Guid executionId,
            RepositoryAccessScope scope,
            string mutation)
        {
            Fixture = fixture;
            Context = context;
            JobExecutionId = jobExecutionId;
            ExecutionId = executionId;
            Scope = scope;
            _mutation = mutation;
            SourceReader = new RecordingSourceReader();
            Service = new Pass2CodeLogicRetrievalService(Context, Scope, SourceReader);
            ServiceWithRevokedScope = new Pass2CodeLogicRetrievalService(
                Context,
                new RepositoryAccessScope(Scope.AuthorizedRepositoryIds, new DeniedAccessVerifier()),
                SourceReader);
        }

        internal ExactCrossRepositoryTestFixture Fixture { get; }
        internal VulgataDbContext Context { get; }
        internal Guid JobExecutionId { get; }
        internal Guid ExecutionId { get; }
        internal RepositoryAccessScope Scope { get; }
        internal RecordingSourceReader SourceReader { get; }
        internal IPass2CodeLogicRetrievalService Service { get; }
        internal IPass2CodeLogicRetrievalService ServiceWithRevokedScope { get; }

        internal static async Task<PersistedRetrievalFixture> CreateAsync(string mutation)
        {
            ExactCrossRepositoryTestFixture fixture =
                await ExactCrossRepositoryTestFixture.CreateAsync();
            VulgataDbContext context = fixture.Database.CreateContext();
            try
            {
                MethodInfo seedMethod = typeof(Pass2FlowPlanningPostgreSqlTests)
                    .GetMethod("SeedPass2Async", BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("The authoritative T026 seed is unavailable.");
                object invocation = seedMethod.Invoke(null, [context, fixture])
                    ?? throw new InvalidOperationException("The authoritative T026 seed returned null.");
                if (invocation is not Task seedTask)
                {
                    throw new InvalidOperationException("The authoritative T026 seed is not asynchronous.");
                }

                await seedTask;
                object seed = seedTask.GetType().GetProperty("Result")?.GetValue(seedTask)
                    ?? throw new InvalidOperationException("The authoritative T026 seed result is unavailable.");
                Pass1PairExecution pair = (Pass1PairExecution)GetRequiredProperty(seed, "Pair");
                RepositoryAccessScope scope = (RepositoryAccessScope)GetRequiredProperty(seed, "Scope");
                await AddSearchableSectionsAsync(context, seed);
                if (mutation == "cross-job")
                {
                    await AddAdditionalDecisionAsync(context, seed);
                }

                PreparedPass2StartResult start = await new PreparedPass2Store(context)
                    .StartAsync(pair.Id, scope);
                Assert.Equal(PreparedPass2StartOutcome.Created, start.Outcome);
                Pass2JobExecution job = await context.Pass2JobExecutions
                    .OrderBy(value => value.GroupOrder)
                    .FirstAsync();
                Guid executionId = job.Pass2ExecutionId;
                Assert.Equal(1, await context.Pass2Executions.CountAsync());
                Assert.Equal(start.JobCount, await context.Pass2JobExecutions.CountAsync());
                Assert.Equal(start.RetrievalBudgetCount, await context.Pass2RetrievalBudgets.CountAsync());
                Assert.NotEmpty(await context.FlowGroups.ToArrayAsync());
                Assert.NotEmpty(await context.FlowTraces.ToArrayAsync());
                Assert.NotEmpty(await context.FlowTraceSteps.ToArrayAsync());
                Assert.NotEmpty(await context.DocumentVersions.ToArrayAsync());
                Assert.NotEmpty(await context.DocumentSections.ToArrayAsync());
                Assert.NotEmpty(await context.SourceAnchors.ToArrayAsync());
                Assert.NotEmpty(await context.DocumentEdges.ToArrayAsync());
                context.ChangeTracker.Clear();
                return new(fixture, context, job.Id, executionId, scope, mutation);
            }
            catch
            {
                await context.DisposeAsync();
                await fixture.DisposeAsync();
                throw;
            }
        }

        internal async Task ApplyMutationAsync()
        {
            if (_mutation is "revoked-scope" or "none")
            {
                return;
            }

            Guid documentVersionId = await Context.FlowTraceSteps
                .Where(step => step.DocumentVersionId != Guid.Empty)
                .OrderBy(step => step.StepOrder)
                .Select(step => step.DocumentVersionId)
                .FirstAsync();

            switch (_mutation)
            {
                case "stale":
                {
                    Guid logicalDocumentId = await Context.DocumentVersions
                        .Where(version => version.Id == documentVersionId)
                        .Select(version => version.LogicalDocumentId)
                        .SingleAsync();
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = NULL WHERE \"Id\" = {logicalDocumentId}");
                    break;
                }
                case "superseded":
                {
                    DocumentVersion current = await Context.DocumentVersions
                        .SingleAsync(version => version.Id == documentVersionId);
                    Guid successorId = ExactCrossRepositoryTestValues.Id(40_001);
                    DocumentVersion successor = DocumentVersion.Create(
                        successorId,
                        current.LogicalDocumentId,
                        current.RepositoryId,
                        current.SystemId,
                        current.ScanRunId,
                        current.ProfiledRevision,
                        current.VersionNumber + 1,
                        current.Title + " successor",
                        current.RepositoryRelativePath,
                        current.SchemaIdentity,
                        current.ContentJson,
                        current.ContentMarkdown,
                        new string('a', 64),
                        new string('b', 64),
                        current.CreatedAt.AddSeconds(1));
                    Context.DocumentVersions.Add(successor);
                    await Context.SaveChangesAsync();
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"DocumentVersions\" SET \"SupersededById\" = {successorId} WHERE \"Id\" = {documentVersionId}");
                    break;
                }
                case "cross-system":
                {
                    Guid foreignSystemId = Guid.Parse("f0000000-0000-4000-8000-000000000009");
                    SystemEntity foreignSystem = new(
                        "T025 foreign system",
                        null,
                        null,
                        DateTimeOffset.UtcNow);
                    ExactCrossRepositoryReflection.SetId(foreignSystem, foreignSystemId);
                    Context.Systems.Add(foreignSystem);
                    await Context.SaveChangesAsync();
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"Pass2Executions\" SET \"SystemId\" = {foreignSystemId} WHERE \"Id\" = (SELECT \"Pass2ExecutionId\" FROM \"Pass2JobExecutions\" WHERE \"Id\" = {JobExecutionId})");
                    break;
                }
                case "profiled-revision":
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"DocumentVersions\" SET \"ProfiledRevision\" = {new string('f', 40)} WHERE \"Id\" = {documentVersionId}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(_mutation));
            }

            Context.ChangeTracker.Clear();
        }

        internal async Task<string> MutationSnapshotAsync()
        {
            if (_mutation is "revoked-scope" or "none")
            {
                return _mutation;
            }

            Guid documentVersionId = await Context.FlowTraceSteps
                .Where(step => step.DocumentVersionId != Guid.Empty)
                .OrderBy(step => step.StepOrder)
                .Select(step => step.DocumentVersionId)
                .FirstAsync();

            return _mutation switch
            {
                "stale" => (await Context.DocumentVersions
                    .Where(version => version.Id == documentVersionId)
                    .Select(version => version.LogicalDocumentId)
                    .Join(
                        Context.LogicalDocuments,
                        logicalDocumentId => logicalDocumentId,
                        logicalDocument => logicalDocument.Id,
                        (_, logicalDocument) => logicalDocument.CurrentVersionId)
                    .SingleAsync()).ToString() ?? string.Empty,
                "superseded" => (await Context.DocumentVersions
                    .Where(version => version.Id == documentVersionId)
                    .Select(version => version.SupersededById)
                    .SingleAsync()).ToString() ?? string.Empty,
                "profiled-revision" => await Context.DocumentVersions
                    .Where(version => version.Id == documentVersionId)
                    .Select(version => version.ProfiledRevision)
                    .SingleAsync(),
                "cross-system" => (await Context.Pass2JobExecutions
                    .Where(job => job.Id == JobExecutionId)
                    .Join(
                        Context.Pass2Executions,
                        job => job.Pass2ExecutionId,
                        execution => execution.Id,
                        (_, execution) => execution.SystemId)
                    .SingleAsync()).ToString(),
                _ => throw new ArgumentOutOfRangeException(nameof(_mutation)),
            };
        }

        internal async Task<Pass2CodeLogicRetrievalItem> IssueSectionHandleAsync()
        {
            Pass2CodeLogicRetrievalResult search = await Service.RetrieveAsync(
                JobExecutionId,
                new(Pass2CodeLogicRetrievalOperation.Search, queryText: "ledger"));
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, search.Outcome);
            Pass2CodeLogicRetrievalItem? section = search.Items.FirstOrDefault(value =>
                value.Kind == Pass2CodeLogicRetrievalItemKind.Section);
            Assert.NotNull(section);
            return section;
        }

        internal async Task<Pass2CodeLogicRetrievalItem> IssueAnchorHandleAsync()
        {
            Pass2CodeLogicRetrievalItem section = await IssueSectionHandleAsync();
            Pass2CodeLogicRetrievalResult anchors = await Service.RetrieveAsync(
                JobExecutionId,
                new(
                    Pass2CodeLogicRetrievalOperation.SourceAnchors,
                    opaqueHandles: [section.OpaqueHandle]));
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, anchors.Outcome);
            Pass2CodeLogicRetrievalItem? anchor = anchors.Items.FirstOrDefault(value =>
                value.Kind == Pass2CodeLogicRetrievalItemKind.SourceAnchor);
            Assert.NotNull(anchor);
            return anchor;
        }

        internal async Task<Pass2CodeLogicRetrievalItem> IssueDocumentHandleAsync()
        {
            Pass2CodeLogicRetrievalResult search = await Service.RetrieveAsync(
                JobExecutionId,
                new(Pass2CodeLogicRetrievalOperation.Search, queryText: "consumer"));
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, search.Outcome);
            return Assert.Single(search.Items, value =>
                value.Kind == Pass2CodeLogicRetrievalItemKind.Document);
        }

        internal async Task<string> AnchorSnapshotAsync(Guid anchorId)
        {
            return await Context.SourceAnchors
                .Where(value => value.Id == anchorId)
                .Select(value => value.SourceHash + ":" + value.ByteStart + ":" + value.ByteEnd)
                .SingleAsync();
        }

        internal async Task<string> AuthoritySnapshotAsync()
        {
            Guid executionId = await Context.Pass2JobExecutions
                .Where(job => job.Id == JobExecutionId)
                .Select(job => job.Pass2ExecutionId)
                .SingleAsync();
            var groupRows = await Context.FlowGroups.AsNoTracking()
                .Where(group => group.Pass2ExecutionId == executionId)
                .OrderBy(group => group.GroupOrder)
                .Select(group => new
                {
                    group.Id,
                    group.State,
                    group.PlanFingerprint,
                    group.MembershipFingerprint,
                    group.ExactResolutionDecisionId,
                    group.ExactResolutionEdgeId,
                })
                .ToArrayAsync();
            string groups = string.Join(
                "|",
                groupRows.Select(group => string.Join(
                    ":",
                    group.Id,
                    group.State,
                    group.PlanFingerprint,
                    group.MembershipFingerprint,
                    group.ExactResolutionDecisionId,
                    group.ExactResolutionEdgeId)));
            Guid[] decisionIds = groupRows
                .Where(group => group.ExactResolutionDecisionId is not null)
                .Select(group => group.ExactResolutionDecisionId!.Value)
                .ToArray();
            var decisionRows = await Context.CrossRepositoryResolutionDecisions.AsNoTracking()
                .Where(decision => decisionIds.Contains(decision.Id))
                .OrderBy(decision => decision.Id)
                .Select(decision => new
                {
                    decision.Id,
                    decision.ProviderDocumentVersionId,
                    decision.DocumentEdgeId,
                    decision.EvidenceFingerprint,
                })
                .ToArrayAsync();
            string decisions = string.Join(
                "|",
                decisionRows.Select(decision => string.Join(
                    ":",
                    decision.Id,
                    decision.ProviderDocumentVersionId,
                    decision.DocumentEdgeId,
                    decision.EvidenceFingerprint)));
            return groups + "\n" + decisions;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Fixture.DisposeAsync();
        }

        private static object GetRequiredProperty(object target, string name) =>
            target.GetType().GetProperty(name)?.GetValue(target)
            ?? throw new InvalidOperationException($"The authoritative seed property '{name}' is unavailable.");

        private static async Task AddSearchableSectionsAsync(VulgataDbContext context, object seed)
        {
            ExactPass1DocumentFixture[] documents =
            [
                (ExactPass1DocumentFixture)GetRequiredProperty(seed, "Consumer"),
                (ExactPass1DocumentFixture)GetRequiredProperty(seed, "ProviderXml"),
                (ExactPass1DocumentFixture)GetRequiredProperty(seed, "ProviderImplementation"),
            ];

            for (int index = 0; index < documents.Length; index++)
            {
                ExactPass1DocumentFixture document = documents[index];
                context.DocumentSections.Add(DocumentSection.Create(
                    ExactCrossRepositoryTestValues.Id(30_001 + index),
                    document.LogicalDocument.Id,
                    document.DocumentVersion.Id,
                    document.DocumentVersion.RepositoryId,
                    ExactCrossRepositoryTestValues.Id(30_101 + index),
                    1,
                    DocumentSectionKind.Evidence,
                    $"Ledger evidence {index + 1}",
                    $"ledger updateSession evidence {index + 1}",
                    "ledger updateSession",
                    $"[\"{document.SourceAnchor.Id}\"]",
                    $"[\"{document.DocumentVersion.Id}\"]",
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));
                context.IndexEntries.Add(IndexEntry.Create(
                    ExactCrossRepositoryTestValues.Id(30_201 + index),
                    document.LogicalDocument.Id,
                    document.DocumentVersion.Id,
                    document.DocumentVersion.RepositoryId,
                    ExactCrossRepositoryTestValues.SystemId,
                    DocumentType.CodeLogic,
                    $"{document.DocumentVersion.Title} ledger",
                    $"Current {document.DocumentVersion.Title} document",
                    document.DocumentVersion.RepositoryRelativePath,
                    "[]",
                    "[]",
                    "[]",
                    index == 0 ? "consumer ledger" : $"provider ledger {index}",
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));
            }

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        private static async Task AddAdditionalDecisionAsync(
            VulgataDbContext context,
            object seed)
        {
            MethodInfo method = typeof(Pass2FlowPlanningPostgreSqlTests)
                .GetMethod("AddExactDecisionsAsync", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The accepted T020 decision fixture helper is unavailable.");
            object invocation = method.Invoke(null, [context, seed, 1])
                ?? throw new InvalidOperationException("The accepted T020 decision fixture invocation returned null.");
            if (invocation is not Task task)
            {
                throw new InvalidOperationException("The accepted T020 decision fixture helper is not asynchronous.");
            }

            await task;
        }
    }

    private sealed class DeniedAccessVerifier : IRepositoryAuthorizationVerifier
    {
        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class RecordingSourceReader : IRevisionBoundSourceReader
    {
        public int Calls { get; private set; }
        public string? FixedSourceHash { get; set; }

        public Task<SourceExcerptResult> ReadAsync(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new SourceExcerptResult(
                AnalysisOperationStatus.Succeeded,
                scope.RepositoryId,
                scope.ProfiledRevision,
                request.RelativePath,
                request.Range,
                FixedSourceHash ?? request.ExpectedSourceSha256,
                "ledger",
                6,
                null));
        }
    }
}
