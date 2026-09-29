using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T018 RED boundary for endpoint-centred Pass 2 planning. The fixture
/// persists the minimum authoritative Pass 1 handoff and v2 evidence before
/// invoking the public planner; the current planner fails only because its
/// planning implementation is not yet present.
/// </summary>
public sealed class Pass2FlowPlanningTests
{
    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T018_PreparedPass2FlowPlanner_PlansPersistedAuthoritativeHandoffAsync()
    {
        await using PlanningHarness harness = await PlanningHarness.CreateAsync();

        PreparedPass2FlowPlanningResult result = await harness.Planner.PlanAsync(
            harness.Pass2ExecutionId,
            harness.Scope);

        FlowGroup group = Assert.Single(result.FlowGroups);
        Assert.Equal("endpoint-flow-group/1.0", group.SchemaIdentity);
        Assert.Equal("business-flow-logical-key/1.0", group.LogicalKeySchemaIdentity);
        Assert.Equal("Complete", group.State);
        Assert.Equal(harness.Pass2ExecutionId, group.Pass2ExecutionId);
        Assert.Equal(harness.ConsumerDocumentVersionId, group.ConsumerDocumentVersionId);
        Assert.Equal(harness.ProviderXmlDocumentVersionId, group.ProviderXmlDocumentVersionId);
        Assert.Equal(harness.ProviderImplementationDocumentVersionId, group.ProviderImplementationDocumentVersionId);
        Assert.NotEqual(group.ProviderXmlDocumentVersionId, group.ProviderImplementationDocumentVersionId);
        Assert.Equal(harness.SharedEdgeId, group.ExactResolutionEdgeId);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void T018_FlowGroup_SeparatesExecutionIdentityFromCrossScanLogicalIdentity()
    {
        AssertProperty<FlowGroup>(nameof(FlowGroup.LogicalDocumentStableKey));
        AssertProperty<FlowGroup>(nameof(FlowGroup.SchemaIdentity));
        AssertProperty<FlowGroup>(nameof(FlowGroup.Pass2ExecutionId));
        AssertProperty<FlowGroup>(nameof(FlowGroup.SystemId));
        AssertProperty<FlowGroup>(nameof(FlowGroup.Protocol));
        AssertProperty<FlowGroup>(nameof(FlowGroup.NormalizedEndpointIdentifier));
        AssertProperty<FlowGroup>(nameof(FlowGroup.ConsumerEntryIdentity));
        AssertProperty<FlowGroup>(nameof(FlowGroup.ConsumerEntryStableKey));
        AssertProperty<FlowGroup>(nameof(FlowGroup.LogicalKeySchemaIdentity));
        AssertProperty<FlowGroup>(nameof(FlowGroup.ConsumerDocumentVersionId));
        AssertProperty<FlowGroup>(nameof(FlowGroup.ProviderRepositoryId));
        AssertProperty<FlowGroup>(nameof(FlowGroup.ProviderXmlDocumentVersionId));
        AssertProperty<FlowGroup>(nameof(FlowGroup.ProviderImplementationDocumentVersionId));
        AssertProperty<FlowGroup>(nameof(FlowGroup.ExactResolutionDecisionId));
        AssertProperty<FlowGroup>(nameof(FlowGroup.ExactResolutionEdgeId));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void T018_FlowGroup_RequiresClosedCompleteMembershipAndCanonicalPlanFingerprint()
    {
        AssertProperty<FlowGroup>(nameof(FlowGroup.State));
        AssertProperty<FlowGroup>(nameof(FlowGroup.PlanFingerprint));
        AssertProperty<FlowGroup>("MembershipFingerprint");
        AssertProperty<FlowGroup>(nameof(FlowGroup.ReasonCode));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void T018_FlowTraceStep_BindsDecisionIndependentlyFromReusableEdge()
    {
        AssertProperty<FlowTraceStep>(nameof(FlowTraceStep.EdgeId));
        AssertProperty<FlowTraceStep>(nameof(FlowTraceStep.ExactResolutionDecisionId));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T018_Planning_TwoPersistedDecisionsSharingOneEdgeRemainDistinctAsync()
    {
        await using PlanningHarness harness = await PlanningHarness.CreateAsync(decisionCount: 2);

        PreparedPass2FlowPlanningResult result = await harness.Planner.PlanAsync(
            harness.Pass2ExecutionId,
            harness.Scope);

        Assert.Equal(2, result.FlowGroups.Count);
        Assert.Equal(
            2,
            result.FlowGroups.Select(group => group.ExactResolutionDecisionId).Distinct().Count());
        Assert.Single(result.FlowGroups.Select(group => group.ExactResolutionEdgeId).Distinct());
        Assert.Equal(
            2,
            result.FlowGroups.Select(group => group.LogicalDocumentStableKey).Distinct().Count());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T018_Planning_PersistedConsumerEntryChangeProducesNewBusinessFlowKeyAsync()
    {
        await using PlanningHarness harness = await PlanningHarness.CreateAsync(decisionCount: 2);

        PreparedPass2FlowPlanningResult result = await harness.Planner.PlanAsync(
            harness.Pass2ExecutionId,
            harness.Scope);

        Assert.Equal(
            2,
            result.FlowGroups.Select(group => group.ConsumerEntryStableKey).Distinct().Count());
        Assert.Equal(
            2,
            result.FlowGroups.Select(group => group.LogicalDocumentStableKey).Distinct().Count());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T018_Planning_UsesPersistedCurrentDocumentsAndCanonicalTraceOrderAsync()
    {
        await using PlanningHarness harness = await PlanningHarness.CreateAsync();

        PreparedPass2FlowPlanningResult result = await harness.Planner.PlanAsync(
            harness.Pass2ExecutionId,
            harness.Scope);

        FlowGroup group = Assert.Single(result.FlowGroups);
        Assert.Equal(1, group.GroupOrder);
        Assert.NotEmpty(group.Traces);
        Assert.All(group.Traces, trace =>
            Assert.Equal(
                Enumerable.Range(1, trace.Steps.Count),
                trace.Steps.OrderBy(step => step.StepOrder).Select(step => step.StepOrder)));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T018_Planning_PreservesConsumerEntryIdentityAcrossCurrentVersionChangeAsync()
    {
        await using PlanningHarness baseline = await PlanningHarness.CreateAsync();
        PreparedPass2FlowPlanningResult baselineResult = await baseline.Planner.PlanAsync(
            baseline.Pass2ExecutionId,
            baseline.Scope);
        FlowGroup baselineGroup = Assert.Single(baselineResult.FlowGroups);

        await using PlanningHarness harness = await PlanningHarness.CreateAsync(
            replaceCurrentConsumerVersion: true);

        PreparedPass2FlowPlanningResult result = await harness.Planner.PlanAsync(
            harness.Pass2ExecutionId,
            harness.Scope);

        FlowGroup group = Assert.Single(result.FlowGroups);
        Assert.Matches("^business-flow/v1/[0-9a-f]{64}$", baselineGroup.LogicalDocumentStableKey);
        Assert.NotEqual(baseline.ConsumerLogicalDocumentStableKey, baselineGroup.LogicalDocumentStableKey);
        Assert.Equal(baselineGroup.LogicalDocumentStableKey, group.LogicalDocumentStableKey);
        Assert.Matches("^[0-9a-f]{64}$", group.ConsumerEntryStableKey);
        Assert.Equal(baselineGroup.ConsumerEntryStableKey, group.ConsumerEntryStableKey);
        Assert.NotEqual(harness.ConsumerLogicalDocumentStableKey, group.LogicalDocumentStableKey);
        Assert.Equal(harness.ConsumerDocumentVersionId, group.ConsumerDocumentVersionId);
        Assert.NotEqual(baseline.ConsumerDocumentVersionId, group.ConsumerDocumentVersionId);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T019_Planning_ExpandsOneSameRepositoryHopAndCollapsesCycleAsync()
    {
        await using PlanningHarness harness = await PlanningHarness.CreateAsync();
        Guid optionalDocumentVersionId = await harness.AddOptionalOneHopCycleAsync();

        PreparedPass2FlowPlanningResult result = await harness.Planner.PlanAsync(
            harness.Pass2ExecutionId,
            harness.Scope);

        FlowGroup group = Assert.Single(result.FlowGroups);
        FlowTrace trace = Assert.Single(group.Traces);
        FlowTraceStep[] documentSteps = trace.Steps
            .Where(step => step.StepKind == FlowTraceStepKind.Document)
            .ToArray();
        Assert.Equal(
            documentSteps.Length,
            documentSteps.Select(step => step.DocumentVersionId).Distinct().Count());
        Assert.Single(documentSteps, step => step.DocumentVersionId == optionalDocumentVersionId);
        FlowTraceStep[] relationshipSteps = trace.Steps
            .Where(step => step.StepKind == FlowTraceStepKind.Relationship)
            .ToArray();
        Assert.All(relationshipSteps, step => Assert.True(step.EdgeId.HasValue));
        Guid[] relationshipEdgeIds = relationshipSteps
            .Select(step => step.EdgeId!.Value)
            .ToArray();
        Assert.Equal(
            [
                harness.SharedEdgeId,
                harness.ProviderImplementationEdgeId,
                Guid.Parse("00000000-0000-0000-0000-000000013505"),
            ],
            relationshipEdgeIds);
        Assert.Equal(
            relationshipEdgeIds.Length,
            relationshipEdgeIds.Distinct().Count());
        Assert.Equal(
            Enumerable.Range(1, trace.Steps.Count),
            trace.Steps.OrderBy(step => step.StepOrder).Select(step => step.StepOrder));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T019_Planning_MissingImplementationProducesIncompleteWithoutJobAsync()
    {
        await AssertIncompleteWithoutJobAsync(
            static harness => harness.RemoveProviderImplementationEdgeAsync());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T019_Planning_MultipleImplementationsProduceIncompleteWithoutJobAsync()
    {
        await AssertIncompleteWithoutJobAsync(
            static harness => harness.AddSecondProviderImplementationAsync());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T019_Planning_UnanchoredImplementationProducesIncompleteWithoutJobAsync()
    {
        await AssertIncompleteWithoutJobAsync(
            static harness => harness.RemoveProviderImplementationAnchorAsync());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T019_Planning_StaleCurrentImplementationProducesIncompleteWithoutJobAsync()
    {
        await AssertIncompleteWithoutJobAsync(
            static harness => harness.ClearProviderImplementationCurrentPointerAsync());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T019_Planning_WrongRevisionImplementationProducesIncompleteWithoutJobAsync()
    {
        await AssertIncompleteWithoutJobAsync(
            static harness => harness.ChangeProviderImplementationRevisionAsync());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T019_Planning_ForeignSystemImplementationProducesIncompleteWithoutJobAsync()
    {
        await AssertIncompleteWithoutJobAsync(
            static harness => harness.ChangeProviderImplementationSystemAsync());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T019_Planning_UnauthorizedProviderProducesIncompleteWithoutJobAsync()
    {
        await using PlanningHarness harness = await PlanningHarness.CreateAsync();
        RepositoryAccessScope consumerOnlyScope = new(
            [ExactCrossRepositoryTestValues.ConsumerRepositoryId]);

        PreparedPass2FlowPlanningResult result = await harness.Planner.PlanAsync(
            harness.Pass2ExecutionId,
            consumerOnlyScope);

        FlowGroup group = Assert.Single(result.FlowGroups);
        Assert.Equal("Incomplete", group.State);
        Assert.False(string.IsNullOrWhiteSpace(group.ReasonCode));
        Assert.Empty(await harness.Context.Pass2JobExecutions
            .AsNoTracking()
            .Where(value => value.Pass2ExecutionId == harness.Pass2ExecutionId)
            .ToArrayAsync());
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T019_Planning_IgnoresUnrelatedGraphEdgesWithoutExpandingMembershipAsync()
    {
        await using PlanningHarness harness = await PlanningHarness.CreateAsync();
        Guid unrelatedEdgeId = await harness.AddUnrelatedGraphEdgeAsync();

        PreparedPass2FlowPlanningResult result = await harness.Planner.PlanAsync(
            harness.Pass2ExecutionId,
            harness.Scope);

        FlowGroup group = Assert.Single(result.FlowGroups);
        FlowTrace trace = Assert.Single(group.Traces);
        Assert.Equal(5, trace.Steps.Count);
        Assert.DoesNotContain(trace.Steps, step => step.EdgeId == unrelatedEdgeId);
    }

    private static async Task AssertIncompleteWithoutJobAsync(
        Func<PlanningHarness, Task> mutate)
    {
        await using PlanningHarness harness = await PlanningHarness.CreateAsync();
        await mutate(harness);

        PreparedPass2FlowPlanningResult result = await harness.Planner.PlanAsync(
            harness.Pass2ExecutionId,
            harness.Scope);

        FlowGroup group = Assert.Single(result.FlowGroups);
        Assert.Equal("Incomplete", group.State);
        Assert.False(string.IsNullOrWhiteSpace(group.ReasonCode));
        Assert.Empty(await harness.Context.Pass2JobExecutions
            .AsNoTracking()
            .Where(value => value.Pass2ExecutionId == harness.Pass2ExecutionId)
            .ToArrayAsync());
    }

    private static void AssertProperty<T>(string propertyName) =>
        Assert.NotNull(typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public));

    private sealed class PlanningHarness : IAsyncDisposable
    {
        private PlanningHarness(
            ExactCrossRepositoryTestFixture fixture,
            VulgataDbContext context,
            Guid pass2ExecutionId,
            Guid consumerDocumentVersionId,
            Guid providerXmlDocumentVersionId,
            Guid providerDocumentVersionId,
            Guid sharedEdgeId,
            string consumerLogicalDocumentStableKey)
        {
            Fixture = fixture;
            Context = context;
            Planner = new PreparedPass2FlowPlanner(context);
            Pass2ExecutionId = pass2ExecutionId;
            ConsumerDocumentVersionId = consumerDocumentVersionId;
            ProviderXmlDocumentVersionId = providerXmlDocumentVersionId;
            ProviderImplementationDocumentVersionId = providerDocumentVersionId;
            SharedEdgeId = sharedEdgeId;
            ConsumerLogicalDocumentStableKey = consumerLogicalDocumentStableKey;
            Scope = fixture.Access.AuthorizedScope;
        }

        internal ExactCrossRepositoryTestFixture Fixture { get; }
        internal VulgataDbContext Context { get; }
        internal PreparedPass2FlowPlanner Planner { get; }
        internal Guid Pass2ExecutionId { get; }
        internal Guid ConsumerDocumentVersionId { get; }
        internal Guid ProviderXmlDocumentVersionId { get; }
        internal Guid ProviderImplementationDocumentVersionId { get; }
        internal Guid ProviderImplementationEdgeId { get; } = Guid.Parse(
            "00000000-0000-0000-0000-000000013100");
        internal Guid SharedEdgeId { get; }
        internal string ConsumerLogicalDocumentStableKey { get; }
        internal RepositoryAccessScope Scope { get; }

        internal async Task RemoveProviderImplementationEdgeAsync()
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"DocumentEdges\" WHERE \"Id\" = {ProviderImplementationEdgeId}");
            Context.ChangeTracker.Clear();
        }

        internal async Task RemoveProviderImplementationAnchorAsync()
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"SourceAnchors\" WHERE \"DocumentVersionId\" = {ProviderImplementationDocumentVersionId}");
            Context.ChangeTracker.Clear();
        }

        internal async Task ClearProviderImplementationCurrentPointerAsync()
        {
            Guid logicalDocumentId = await Context.DocumentVersions
                .Where(value => value.Id == ProviderImplementationDocumentVersionId)
                .Select(value => value.LogicalDocumentId)
                .SingleAsync();
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = NULL WHERE \"Id\" = {logicalDocumentId}");
            Context.ChangeTracker.Clear();
        }

        internal async Task ChangeProviderImplementationRevisionAsync()
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"DocumentVersions\" SET \"ProfiledRevision\" = {'f' + new string('0', 39)} WHERE \"Id\" = {ProviderImplementationDocumentVersionId}");
            Context.ChangeTracker.Clear();
        }

        internal async Task ChangeProviderImplementationSystemAsync()
        {
            Guid foreignSystemId = Guid.Parse("f0000000-0000-4000-8000-000000000009");
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"DocumentVersions\" SET \"SystemId\" = {foreignSystemId} WHERE \"Id\" = {ProviderImplementationDocumentVersionId}");
            Context.ChangeTracker.Clear();
        }

        internal async Task<Guid> AddOptionalOneHopCycleAsync()
        {
            Guid optionalDocumentVersionId = await AddCurrentCodeLogicDocumentAsync(
                Guid.Parse("00000000-0000-0000-0000-000000013501"),
                Guid.Parse("00000000-0000-0000-0000-000000013502"),
                Guid.Parse("00000000-0000-0000-0000-000000013503"),
                Guid.Parse("00000000-0000-0000-0000-000000013505"),
                ProviderImplementationDocumentVersionId,
                "exact-pass1/provider-optional");
            DocumentVersion source = await Context.DocumentVersions.AsNoTracking()
                .SingleAsync(value => value.Id == optionalDocumentVersionId);
            Context.DocumentEdges.Add(new DocumentEdge(
                Guid.Parse("00000000-0000-0000-0000-000000013504"),
                optionalDocumentVersionId,
                source.RepositoryId,
                source.ScanRunId,
                ProviderImplementationDocumentVersionId,
                source.RepositoryId,
                source.ScanRunId,
                DocumentEdgeType.References,
                ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(7)));
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            return optionalDocumentVersionId;
        }

        internal async Task AddSecondProviderImplementationAsync()
        {
            _ = await AddCurrentCodeLogicDocumentAsync(
                Guid.Parse("00000000-0000-0000-0000-000000013401"),
                Guid.Parse("00000000-0000-0000-0000-000000013402"),
                Guid.Parse("00000000-0000-0000-0000-000000013403"),
                Guid.Parse("00000000-0000-0000-0000-000000013404"),
                ProviderXmlDocumentVersionId,
                "exact-pass1/provider-implementation-2");
        }

        internal async Task<Guid> AddUnrelatedGraphEdgeAsync()
        {
            Guid id = Guid.Parse("00000000-0000-0000-0000-000000013601");
            DocumentVersion consumer = await Context.DocumentVersions.AsNoTracking()
                .SingleAsync(value => value.Id == ConsumerDocumentVersionId);
            DocumentVersion provider = await Context.DocumentVersions.AsNoTracking()
                .SingleAsync(value => value.Id == ProviderImplementationDocumentVersionId);
            Context.DocumentEdges.Add(new DocumentEdge(
                id,
                consumer.Id,
                consumer.RepositoryId,
                consumer.ScanRunId,
                provider.Id,
                provider.RepositoryId,
                provider.ScanRunId,
                DocumentEdgeType.References,
                ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(8)));
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            return id;
        }

        private async Task<Guid> AddCurrentCodeLogicDocumentAsync(
            Guid logicalDocumentId,
            Guid documentVersionId,
            Guid sourceAnchorId,
            Guid edgeId,
            Guid sourceDocumentVersionId,
            string stableKey)
        {
            DocumentVersion source = await Context.DocumentVersions.AsNoTracking()
                .SingleAsync(value => value.Id == sourceDocumentVersionId);
            SourceAnchor sourceAnchor = await Context.SourceAnchors.AsNoTracking()
                .SingleAsync(value => value.DocumentVersionId == sourceDocumentVersionId);
            LogicalDocument logicalDocument = LogicalDocument.Create(
                logicalDocumentId,
                source.RepositoryId,
                DocumentType.CodeLogic,
                stableKey,
                ExactCrossRepositoryTestValues.CreatedAt);
            string path = $"src/main/java/related/{documentVersionId:N}.java";
            DocumentVersion documentVersion = DocumentVersion.Create(
                documentVersionId,
                logicalDocumentId,
                source.RepositoryId,
                source.SystemId,
                source.ScanRunId,
                source.ProfiledRevision,
                1,
                "Related implementation",
                path,
                "code-logic/file-v2",
                "{}",
                "# Related implementation",
                new string('e', 64),
                source.CommitFingerprint,
                ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(7));
            documentVersion.SourceAnchors.Add(new SourceAnchor(
                sourceAnchorId,
                documentVersionId,
                source.RepositoryId,
                source.ScanRunId,
                sourceAnchor.CodeUnitId,
                source.ProfiledRevision,
                path,
                $"{path}#fixture",
                1,
                3,
                sourceAnchor.SourceHash,
                ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(7)));

            Context.LogicalDocuments.Add(logicalDocument);
            await Context.SaveChangesAsync();
            Context.DocumentVersions.Add(documentVersion);
            await Context.SaveChangesAsync();
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {documentVersionId} WHERE \"Id\" = {logicalDocumentId}");
            Context.DocumentEdges.Add(new DocumentEdge(
                edgeId,
                sourceDocumentVersionId,
                source.RepositoryId,
                source.ScanRunId,
                documentVersionId,
                source.RepositoryId,
                source.ScanRunId,
                DocumentEdgeType.References,
                ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(7)));
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            return documentVersionId;
        }

        internal static async Task<PlanningHarness> CreateAsync(
            int decisionCount = 1,
            bool replaceCurrentConsumerVersion = false)
        {
            ExactCrossRepositoryTestFixture fixture =
                await ExactCrossRepositoryTestFixture.CreateAsync();
            VulgataDbContext context = fixture.Database.CreateContext();
            try
            {
                ExactPass1DocumentFixture consumer =
                    fixture.CreatePass1Document(ExactPass1DocumentKind.Consumer);
                ExactPass1DocumentFixture provider =
                    fixture.CreatePass1Document(ExactPass1DocumentKind.Provider);

                // Reuse the existing managed Pass 1 fixture. It persists the
                // terminal Pass 1 pair, manifests, snapshots, current Documents,
                // Source Anchors, classifications, and catalog bindings.
                MethodInfo seed = typeof(ExactCrossRepositoryResolutionTests).GetMethod(
                    "SeedCurrentPairAsync",
                    BindingFlags.Static | BindingFlags.NonPublic)!;
                await (Task)seed.Invoke(null, [context, fixture, consumer, provider])!;

                Guid consumerDocumentVersionIdForPlanning = consumer.DocumentVersion.Id;
                Guid consumerEdgeIdForPlanning = Guid.Parse(
                    "00000000-0000-0000-0000-000000012100");
                SourceAnchor consumerAnchorForPlanning = consumer.SourceAnchor;
                if (replaceCurrentConsumerVersion)
                {
                    (consumerDocumentVersionIdForPlanning, consumerEdgeIdForPlanning, consumerAnchorForPlanning) =
                        await ReplaceCurrentConsumerVersionAsync(context, consumer);
                }

                // The exact cross-Repository Edge targets the provider XML
                // Code Logic Document. A separate same-Repository REFERENCES
                // Edge closes that XML declaration to the Java implementation.
                string xmlPath = Path.Combine(
                    fixture.ProviderRepository.RootPath,
                    "src/main/resources/disgram/in/cust/updateSession.xml".Replace('/', Path.DirectorySeparatorChar));
                string xmlHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(xmlPath)));
                Guid xmlCodeUnitId = Guid.Parse("00000000-0000-0000-0000-000000013001");
                CodeUnit xmlCodeUnit = new(
                    provider.ScanRun.Id,
                    provider.DocumentVersion.RepositoryId,
                    "src/main/resources/disgram/in/cust/updateSession.xml",
                    xmlHash,
                    xmlHash,
                    1,
                    3,
                    "updateSession",
                    CodeUnitType.File,
                    RangeConfidence.Exact,
                    "xml");
                Set(xmlCodeUnit, nameof(CodeUnit.Id), xmlCodeUnitId);
                context.CodeUnits.Add(xmlCodeUnit);
                Guid xmlLogicalDocumentId = Guid.Parse("00000000-0000-0000-0000-000000013002");
                Guid xmlDocumentVersionId = Guid.Parse("00000000-0000-0000-0000-000000013003");
                LogicalDocument xmlLogicalDocument = LogicalDocument.Create(
                    xmlLogicalDocumentId,
                    provider.DocumentVersion.RepositoryId,
                    DocumentType.CodeLogic,
                    "exact-pass1/provider-xml",
                    ExactCrossRepositoryTestValues.CreatedAt);
                DocumentVersion xmlDocumentVersion = DocumentVersion.Create(
                    xmlDocumentVersionId,
                    xmlLogicalDocumentId,
                    provider.DocumentVersion.RepositoryId,
                    ExactCrossRepositoryTestValues.SystemId,
                    provider.ScanRun.Id,
                    provider.DocumentVersion.ProfiledRevision,
                    1,
                    "updateSession.xml",
                    "src/main/resources/disgram/in/cust/updateSession.xml",
                    "code-logic/file-v2",
                    "{}",
                    "# updateSession.xml",
                    new string('f', 64),
                    ExactCrossRepositoryTestValues.CommitFingerprint,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt);
                SourceAnchor xmlAnchor = new(
                    Guid.Parse("00000000-0000-0000-0000-000000013004"),
                    xmlDocumentVersionId,
                    provider.DocumentVersion.RepositoryId,
                    provider.ScanRun.Id,
                    xmlCodeUnitId,
                    provider.DocumentVersion.ProfiledRevision,
                    "src/main/resources/disgram/in/cust/updateSession.xml",
                    "src/main/resources/disgram/in/cust/updateSession.xml#fixture",
                    1,
                    3,
                    xmlHash,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt);
                xmlDocumentVersion.SourceAnchors.Add(xmlAnchor);
                // Persist the logical document before its version, then bind
                // the current pointer after both rows exist. This mirrors the
                // production migration-safe insertion order and avoids the
                // LogicalDocument <-> DocumentVersion FK cycle in EF.
                context.LogicalDocuments.Add(xmlLogicalDocument);
                context.CodeUnits.Add(xmlCodeUnit);
                await context.SaveChangesAsync();
                context.DocumentVersions.Add(xmlDocumentVersion);
                await context.SaveChangesAsync();
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {xmlDocumentVersionId} WHERE \"Id\" = {xmlLogicalDocumentId}");
                context.ChangeTracker.Clear();

                TwoRepositoryScanCoordination coordination =
                    await context.TwoRepositoryScanCoordinations.SingleAsync();
                coordination.BeginPass2(
                    coordination.StateVersion,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(3));
                await context.SaveChangesAsync();

                Pass1PairExecution pair = await context.Pass1PairExecutions.SingleAsync();
                Guid pass2ExecutionId = Guid.Parse("00000000-0000-0000-0000-000000012002");
                Pass2Execution pass2 = Pass2Execution.CreatePlanning(
                    pass2ExecutionId,
                    pair.Id,
                    coordination.Id,
                    ExactCrossRepositoryTestValues.SystemId,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(4));
                context.Pass2Executions.Add(pass2);

                Guid edgeId = consumerEdgeIdForPlanning;
                DocumentEdge edge = new(
                    edgeId,
                    consumerDocumentVersionIdForPlanning,
                    consumer.DocumentVersion.RepositoryId,
                    consumer.ScanRun.Id,
                    xmlDocumentVersionId,
                    provider.DocumentVersion.RepositoryId,
                    provider.ScanRun.Id,
                    DocumentEdgeType.References,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(5));
                context.DocumentEdges.Add(edge);
                context.ExactCrossRepositoryEdgeEvidence.Add(new ExactCrossRepositoryEdgeEvidence(
                    edgeId,
                    consumerAnchorForPlanning.Id,
                    consumerDocumentVersionIdForPlanning,
                    consumer.DocumentVersion.RepositoryId,
                    consumer.ScanRun.Id,
                    xmlAnchor.Id,
                    xmlDocumentVersionId,
                    provider.DocumentVersion.RepositoryId,
                    provider.ScanRun.Id,
                    "cust/updateSession",
                    "in/cust/updateSession",
                    "cust/updateSession",
                    ExactResolutionProtocol.CustomRpc,
                    "ExactConfigured",
                    1.0000m,
                    "1.0",
                    "fixture-profile",
                    "1.0.0",
                    new string('c', 64),
                    "occurrence-001",
                    consumerAnchorForPlanning.EvidenceLocator,
                    xmlAnchor.EvidenceLocator,
                    new string('d', 64),
                    ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(5)));

                Guid implementationEdgeId = Guid.Parse("00000000-0000-0000-0000-000000013100");
                context.DocumentEdges.Add(new DocumentEdge(
                    implementationEdgeId,
                    xmlDocumentVersionId,
                    provider.DocumentVersion.RepositoryId,
                    provider.ScanRun.Id,
                    provider.DocumentVersion.Id,
                    provider.DocumentVersion.RepositoryId,
                    provider.ScanRun.Id,
                    DocumentEdgeType.References,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(5)));

                CommunicationKindClassification providerClassification =
                    await context.CommunicationKindClassifications.SingleAsync(value =>
                        value.PreScanResultId == provider.ScanRun.Id);
                Guid providerIdentity = ExactResolutionCanonicalAuthority.ProviderIdentity(
                    ExactCrossRepositoryTestValues.SystemId,
                    provider.DocumentVersion.RepositoryId,
                    provider.ScanRun.Id,
                    provider.DocumentVersion.ProfiledRevision,
                    xmlDocumentVersionId);

                for (int ordinal = 1; ordinal <= decisionCount; ordinal++)
                {
                    string decisionFingerprint = Convert.ToHexStringLower(
                        SHA256.HashData([1, (byte)ordinal]));
                    Guid decisionId = ExactResolutionCanonicalAuthority.DecisionId(
                        decisionFingerprint);
                    CrossRepositoryResolutionDecision decision = New<CrossRepositoryResolutionDecision>();
                    Set(decision, nameof(CrossRepositoryResolutionDecision.Id), decisionId);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ResolutionSchemaIdentity), "exact-cross-repository-resolution/2.0");
                    Set(decision, nameof(CrossRepositoryResolutionDecision.Pass1PairExecutionId), pair.Id);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.CoordinationId), coordination.Id);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.SystemId), ExactCrossRepositoryTestValues.SystemId);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ConsumerCandidateId), Guid.Parse($"00000000-0000-0000-0000-0000000123{ordinal:00}"));
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryIdentity), $"consumer-entry-{ordinal:00}");
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryStableKey),
                        Convert.ToHexStringLower(SHA256.HashData([4, (byte)ordinal])));
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ConsumerDocumentVersionId), consumerDocumentVersionIdForPlanning);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ConsumerRepositoryId), consumer.DocumentVersion.RepositoryId);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ConsumerScanRunId), consumer.ScanRun.Id);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ConsumerProfiledRevision), consumer.DocumentVersion.ProfiledRevision);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.Protocol), ExactResolutionProtocol.CustomRpc);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.NormalizedEndpointIdentifier), "cust/updateSession");
                    Set(decision, nameof(CrossRepositoryResolutionDecision.NormalizationRuleIdentity), "exact-endpoint-normalization/2.0");
                    Set(decision, nameof(CrossRepositoryResolutionDecision.Outcome), "ExactMatch");
                    Set(decision, nameof(CrossRepositoryResolutionDecision.OutcomeKind), "ExactMatch");
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ReasonCode), "unique_provider");
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ProviderIdentity), providerIdentity);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ProviderDocumentVersionId), xmlDocumentVersionId);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ProviderRepositoryId), provider.DocumentVersion.RepositoryId);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ProviderScanRunId), provider.ScanRun.Id);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ProviderProfiledRevision), provider.DocumentVersion.ProfiledRevision);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.DocumentEdgeId), edgeId);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ConsumerEvidenceFingerprint),
                        Convert.ToHexStringLower(SHA256.HashData([2, (byte)ordinal])));
                    Set(decision, nameof(CrossRepositoryResolutionDecision.DecisionFingerprint), decisionFingerprint);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.EvidenceFingerprint), decisionFingerprint);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.CreatedAt), ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(6 + ordinal));
                    context.CrossRepositoryResolutionDecisions.Add(decision);

                    string contributionFingerprint;
                    ExactResolutionProviderEvidenceContribution contribution = New<ExactResolutionProviderEvidenceContribution>();
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.ResolutionDecisionId), decisionId);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderDocumentVersionId), xmlDocumentVersionId);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderIdentity),
                        providerIdentity);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.RepositoryId),
                        provider.DocumentVersion.RepositoryId);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.ScanRunId),
                        provider.ScanRun.Id);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProfiledRevision),
                        provider.DocumentVersion.ProfiledRevision);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.SourceAnchorId), xmlAnchor.Id);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.CommunicationClassificationId),
                        providerClassification.Id);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderRawIdentifier),
                        "in/cust/updateSession");
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.NormalizedEndpointIdentifier),
                        "cust/updateSession");
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceLocator),
                        xmlAnchor.EvidenceLocator);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.ClassificationFingerprint),
                        providerClassification.Fingerprint);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.CatalogRuleIdentity),
                        providerClassification.CatalogRuleIdentity!);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.NormalizationRuleIdentity),
                        "exact-endpoint-normalization/2.0");
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderOrdinal), 1);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceOrdinal), 1);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.CreatedAt), ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(6 + ordinal));
                    contributionFingerprint = ExactResolutionCanonicalAuthority
                        .PersistedProviderContributionFingerprint(decisionFingerprint, contribution);
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.Id),
                        ExactResolutionCanonicalAuthority.ProviderContributionId(contributionFingerprint));
                    Set(contribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceFingerprint),
                        contributionFingerprint);
                    Set(decision, nameof(CrossRepositoryResolutionDecision.ProviderEvidenceSetFingerprint),
                        ExactResolutionCanonicalAuthority.ProviderEvidenceSetFingerprint([contributionFingerprint]));
                    context.ExactResolutionProviderEvidenceContributions.Add(contribution);
                }

                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();
                _ = await context.Pass2Executions.SingleAsync(value => value.Id == pass2ExecutionId);
                _ = await context.CrossRepositoryResolutionDecisions.FirstAsync();
                _ = await context.DocumentEdges.SingleAsync(value => value.Id == edgeId);
                return new(
                    fixture,
                    context,
                    pass2ExecutionId,
                    consumerDocumentVersionIdForPlanning,
                    xmlDocumentVersionId,
                    provider.DocumentVersion.Id,
                    edgeId,
                    "exact-pass1/consumer");
            }
            catch
            {
                await context.DisposeAsync();
                await fixture.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Fixture.DisposeAsync();
        }

        private static T New<T>() => (T)Activator.CreateInstance(
            typeof(T), BindingFlags.Instance | BindingFlags.NonPublic, null, null, null)!;

        private static void Set<T>(object target, string propertyName, T value) =>
            target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);

        private static async Task<(Guid DocumentVersionId, Guid EdgeId, SourceAnchor Anchor)>
            ReplaceCurrentConsumerVersionAsync(
                VulgataDbContext context,
                ExactPass1DocumentFixture consumer)
        {
            LogicalDocument logicalDocument = await context.LogicalDocuments
                .Include(value => value.Versions)
                .SingleAsync(value => value.Id == consumer.LogicalDocument.Id);
            await context.Entry(logicalDocument).Reference(value => value.CurrentVersion).LoadAsync();
            Guid replacementId = Guid.Parse("00000000-0000-0000-0000-000000013201");
            DocumentVersion replacement = DocumentVersion.Create(
                replacementId,
                logicalDocument.Id,
                consumer.DocumentVersion.RepositoryId,
                ExactCrossRepositoryTestValues.SystemId,
                consumer.ScanRun.Id,
                consumer.DocumentVersion.ProfiledRevision,
                2,
                consumer.DocumentVersion.Title,
                consumer.DocumentVersion.RepositoryRelativePath,
                "code-logic/file-v2",
                consumer.DocumentVersion.ContentJson,
                consumer.DocumentVersion.ContentMarkdown,
                new string('g', 64),
                ExactCrossRepositoryTestValues.CommitFingerprint,
                ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(2));
            SourceAnchor anchor = new(
                Guid.Parse("00000000-0000-0000-0000-000000013202"),
                replacementId,
                consumer.DocumentVersion.RepositoryId,
                consumer.ScanRun.Id,
                consumer.CodeUnit.Id,
                consumer.DocumentVersion.ProfiledRevision,
                consumer.DocumentVersion.RepositoryRelativePath,
                consumer.SourceAnchor.EvidenceLocator,
                consumer.SourceAnchor.LineStart,
                consumer.SourceAnchor.LineEnd,
                consumer.SourceAnchor.SourceHash,
                ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(2));
            replacement.SourceAnchors.Add(anchor);
            logicalDocument.MoveCurrentVersion(consumer.DocumentVersion.Id, replacement);
            context.DocumentVersions.Add(replacement);
            await context.SaveChangesAsync();

            return (
                replacementId,
                Guid.Parse("00000000-0000-0000-0000-000000013203"),
                anchor);
        }
    }
}
