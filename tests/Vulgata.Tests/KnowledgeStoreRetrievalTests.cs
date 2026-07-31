using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class KnowledgeStoreRetrievalTests
{
    private static readonly Guid _anchorId =
        Guid.Parse("80000000-0000-0000-0000-000000000010");
    private static readonly Guid _targetLogicalDocumentId =
        Guid.Parse("50000000-0000-0000-0000-000000000020");
    private static readonly Guid _edgeId =
        Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid _targetCodeUnitId =
        Guid.Parse("40000000-0000-0000-0000-000000000020");
    private static readonly Guid _targetAnchorId =
        Guid.Parse("80000000-0000-0000-0000-000000000020");

    [Fact]
    public async Task GetCurrentAsync_AuthorizedRequestedSections_ReturnsCurrentKnowledgeInCanonicalOrder()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedSourceRepositoryAsync(session.Context);
        KnowledgeStoreRepository repository = CreateRepository(session.Context);
        KnowledgeCommit commit = CreateSourceCommit();

        KnowledgeCommitResult committed = await repository.CommitAsync(
            commit,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]));
        CurrentKnowledge? current = await repository.GetCurrentAsync(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]),
            [
                KnowledgeStoreTestFixtures.SecondSectionId,
                KnowledgeStoreTestFixtures.FirstSectionId,
            ]);

        Assert.Equal(KnowledgeCommitStatus.Committed, committed.Status);
        Assert.NotNull(current);
        Assert.Equal(KnowledgeStoreTestFixtures.LogicalDocumentId, current.LogicalDocumentId);
        Assert.Equal(KnowledgeStoreTestFixtures.DocumentVersionId, current.DocumentVersionId);
        Assert.Equal(
            [
                KnowledgeStoreTestFixtures.FirstSectionId,
                KnowledgeStoreTestFixtures.SecondSectionId,
            ],
            current.Sections.Select(section => section.StableSectionId));
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Fact]
    public async Task GetCurrentAsync_UnknownRequestedSection_OmitsUnknownAndPreservesCanonicalOrder()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedSourceRepositoryAsync(session.Context);
        KnowledgeStoreRepository repository = CreateRepository(session.Context);
        KnowledgeCommitResult committed = await repository.CommitAsync(
            CreateSourceCommit(),
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]));

        CurrentKnowledge? current = await repository.GetCurrentAsync(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]),
            [
                KnowledgeStoreTestFixtures.SecondSectionId,
                Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffffe"),
                KnowledgeStoreTestFixtures.FirstSectionId,
            ]);

        Assert.Equal(KnowledgeCommitStatus.Committed, committed.Status);
        Assert.Equal(
            [
                KnowledgeStoreTestFixtures.FirstSectionId,
                KnowledgeStoreTestFixtures.SecondSectionId,
            ],
            Assert.IsType<CurrentKnowledge>(current)
                .Sections
                .Select(section => section.StableSectionId));
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Fact]
    public async Task GetCurrentAsync_NonCurrentHigherVersionExists_ReturnsOnlyCurrentVersion()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedSourceRepositoryAsync(session.Context);
        KnowledgeStoreRepository repository = CreateRepository(session.Context);
        var scope = new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]);
        Guid staleVersionId =
            Guid.Parse("70000000-0000-0000-0000-000000000002");
        KnowledgeCommitResult first = await repository.CommitAsync(
            CreateSourceCommit(),
            scope);
        KnowledgeDocumentMaterialization staleMaterialization =
            new KnowledgeDocumentMaterializer().Materialize(
                KnowledgeStoreTestFixtures.LogicalDocumentId,
                staleVersionId,
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                null,
                KnowledgeStoreTestFixtures.CreateCanonicalJson(title: "非当前更高版本"),
                KnowledgeStoreTestFixtures.CreatedAt.AddMinutes(1));
        session.Context.Set<DocumentVersion>().Add(
            DocumentVersion.Create(
                staleVersionId,
                KnowledgeStoreTestFixtures.LogicalDocumentId,
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                null,
                KnowledgeStoreTestFixtures.SourceScanRunId,
                KnowledgeStoreTestFixtures.ProfiledRevision,
                2,
                staleMaterialization.Content.Title,
                staleMaterialization.Content.RepositoryRelativePath,
                staleMaterialization.Content.SchemaIdentity,
                staleMaterialization.ContentJson,
                staleMaterialization.ContentMarkdown,
                staleMaterialization.ContentFingerprint,
                KnowledgeStoreTestFixtures.CreatedAt.AddMinutes(1)));
        session.Context.Set<IndexEntry>().Add(staleMaterialization.IndexEntry);
        session.Context.Set<DocumentSection>().AddRange(staleMaterialization.Sections);
        await session.Context.SaveChangesAsync();
        session.Context.ChangeTracker.Clear();

        CurrentKnowledge? current = await repository.GetCurrentAsync(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            scope,
            []);

        Assert.Equal(KnowledgeCommitStatus.Committed, first.Status);
        Assert.Equal(
            KnowledgeStoreTestFixtures.DocumentVersionId,
            Assert.IsType<CurrentKnowledge>(current).DocumentVersionId);
        Assert.NotEqual(staleVersionId, current.DocumentVersionId);
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Fact]
    public async Task GetCurrentAsync_DuplicateRequestedSectionIds_RejectsInvalidBound()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        KnowledgeStoreRepository repository = CreateRepository(session.Context);
        var scope = new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]);

        KnowledgeStoreContractException exception =
            await Assert.ThrowsAsync<KnowledgeStoreContractException>(() =>
                repository.GetCurrentAsync(
                    KnowledgeStoreTestFixtures.LogicalDocumentId,
                    scope,
                    [
                        KnowledgeStoreTestFixtures.FirstSectionId,
                        KnowledgeStoreTestFixtures.FirstSectionId,
                    ]));

        Assert.Equal(KnowledgeRejectionCode.InvalidBound, exception.RejectionCode);
    }

    [Fact]
    public async Task GetCurrentAsync_MoreThanOneHundredRequestedSections_RejectsInvalidBound()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        KnowledgeStoreRepository repository = CreateRepository(session.Context);
        var scope = new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]);
        Guid[] requestedIds = Enumerable.Range(1, 101)
            .Select(index => Guid.Parse(
                $"00000000-0000-0000-0000-{index:D12}"))
            .ToArray();

        KnowledgeStoreContractException exception =
            await Assert.ThrowsAsync<KnowledgeStoreContractException>(() =>
                repository.GetCurrentAsync(
                    KnowledgeStoreTestFixtures.LogicalDocumentId,
                    scope,
                    requestedIds));

        Assert.Equal(KnowledgeRejectionCode.InvalidBound, exception.RejectionCode);
    }

    [Fact]
    public void RepositoryAccessScope_OneAndTwoHundredFiftySixRepositoryIds_AcceptsExactBounds()
    {
        Guid[] maximumIds = Enumerable.Range(1, 256)
            .Select(index => Guid.Parse(
                $"10000000-0000-0000-0000-{index:D12}"))
            .ToArray();

        var minimum = new RepositoryAccessScope([maximumIds[0]]);
        var maximum = new RepositoryAccessScope(maximumIds);

        Assert.Single(minimum.AuthorizedRepositoryIds);
        Assert.Equal(256, maximum.AuthorizedRepositoryIds.Count);
    }

    [Fact]
    public void RepositoryAccessScope_ZeroOrTwoHundredFiftySevenRepositoryIds_RejectsInvalidBound()
    {
        Guid[] overMaximumIds = Enumerable.Range(1, 257)
            .Select(index => Guid.Parse(
                $"20000000-0000-0000-0000-{index:D12}"))
            .ToArray();

        KnowledgeStoreContractException empty = Assert.Throws<KnowledgeStoreContractException>(
            () => new RepositoryAccessScope(Array.Empty<Guid>()));
        KnowledgeStoreContractException overMaximum =
            Assert.Throws<KnowledgeStoreContractException>(
                () => new RepositoryAccessScope(overMaximumIds));

        Assert.Equal(KnowledgeRejectionCode.InvalidBound, empty.RejectionCode);
        Assert.Equal(KnowledgeRejectionCode.InvalidBound, overMaximum.RejectionCode);
    }

    [Fact]
    public async Task GetCurrentAsync_SourceAuthorizationIsRevoked_ReturnsSameNullAsMissingDocument()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedSourceRepositoryAsync(session.Context);
        await SeedTargetRepositoryAsync(session.Context);
        KnowledgeStoreRepository repository = CreateRepository(session.Context);
        KnowledgeCommitResult committed = await repository.CommitAsync(
            CreateSourceCommit(),
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]));
        var revokedScope =
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.TargetRepositoryId]);

        CurrentKnowledge? revoked = await repository.GetCurrentAsync(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            revokedScope,
            []);
        CurrentKnowledge? missing = await repository.GetCurrentAsync(
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            revokedScope,
            []);

        Assert.Equal(KnowledgeCommitStatus.Committed, committed.Status);
        Assert.Null(revoked);
        Assert.Null(missing);
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Fact]
    public async Task GetCurrentAsync_AuthorizedSource_ReturnsBoundedSourceAnchors()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedSourceRepositoryAsync(session.Context);
        KnowledgeStoreRepository repository = CreateRepository(session.Context);
        KnowledgeCommit commit = CreateSourceCommit(includeAnchor: true);

        KnowledgeCommitResult committed = await repository.CommitAsync(
            commit,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]));
        CurrentKnowledge? current = await repository.GetCurrentAsync(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]),
            [KnowledgeStoreTestFixtures.FirstSectionId]);

        Assert.Equal(KnowledgeCommitStatus.Committed, committed.Status);
        Assert.NotNull(current);
        CurrentSourceAnchor anchor = Assert.Single(current.SourceAnchors);
        Assert.Equal(_anchorId, anchor.Id);
        Assert.Equal(KnowledgeStoreTestFixtures.SourceCodeUnitId, anchor.CodeUnitId);
        Assert.Equal("src/orders.cs", anchor.RepositoryRelativePath);
        Assert.Equal("OrderService:1-10", anchor.EvidenceLocator);
        Assert.Equal((1, 10), (anchor.LineStart, anchor.LineEnd));
        Assert.Equal(KnowledgeStoreTestFixtures.SourceHash, anchor.SourceHash);
        Assert.Equal([_anchorId], Assert.Single(current.Sections).SourceAnchorIds);
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Fact]
    public async Task GetCurrentAsync_MaximumFiniteProjection_ReturnsOnlyAcceptedBounds()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedSourceRepositoryAsync(session.Context);
        await SeedTargetRepositoryAsync(session.Context);
        KnowledgeStoreRepository repository = CreateRepository(session.Context);
        KnowledgeCommitResult committed = await repository.CommitAsync(
            CreateMaximumFiniteCommit(),
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]));
        Assert.True(
            committed.Status == KnowledgeCommitStatus.Committed,
            $"Maximum finite commit rejected: {committed.RejectionCode}");

        KnowledgeCommitResult targetCommitted = await repository.CommitAsync(
            CreateTargetCommit(),
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.TargetRepositoryId]));
        Assert.True(
            targetCommitted.Status == KnowledgeCommitStatus.Committed,
            $"Maximum edge target rejected: {targetCommitted.RejectionCode}");
        Guid[] targetVersionIds = Enumerable
            .Range(1, KnowledgeDocumentLimits.MaximumEdges)
            .Select(MaximumTargetVersionId)
            .ToArray();
        DocumentVersion[] extraTargetVersions = Enumerable
            .Range(2, KnowledgeDocumentLimits.MaximumEdges - 1)
            .Select(index => DocumentVersion.Create(
                MaximumTargetVersionId(index),
                _targetLogicalDocumentId,
                KnowledgeStoreTestFixtures.TargetRepositoryId,
                null,
                KnowledgeStoreTestFixtures.TargetScanRunId,
                KnowledgeStoreTestFixtures.ProfiledRevision,
                index,
                $"目标 {index}",
                $"src/target-{index}.cs",
                KnowledgeDocumentLimits.SchemaIdentity,
                "{}",
                string.Empty,
                new string('0', 64),
                KnowledgeStoreTestFixtures.CreatedAt))
            .ToArray();
        session.Context.Set<DocumentVersion>().AddRange(extraTargetVersions);
        session.Context.Set<DocumentEdge>().AddRange(
            targetVersionIds.Select((targetVersionId, index) => new DocumentEdge(
                MaximumId("a1000000", index + 1),
                KnowledgeStoreTestFixtures.DocumentVersionId,
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.SourceScanRunId,
                targetVersionId,
                KnowledgeStoreTestFixtures.TargetRepositoryId,
                KnowledgeStoreTestFixtures.TargetScanRunId,
                DocumentEdgeType.References,
                KnowledgeStoreTestFixtures.CreatedAt)));
        await session.Context.SaveChangesAsync();
        session.Context.ChangeTracker.Clear();

        CurrentKnowledge? current = await repository.GetCurrentAsync(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            new RepositoryAccessScope(
            [
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.TargetRepositoryId,
            ]),
            []);

        Assert.NotNull(current);
        Assert.Equal(KnowledgeDocumentLimits.MaximumAnchors, current.SourceAnchors.Count);
        Assert.Equal(KnowledgeDocumentLimits.MaximumEdges, current.Relationships.Count);
        Assert.Equal(KnowledgeDocumentLimits.MaximumFlowGroups, current.FlowGroups.Count);
        CurrentFlowGroup maximumGroup = current.FlowGroups[0];
        Assert.Equal(KnowledgeDocumentLimits.MaximumTracesPerGroup, maximumGroup.Traces.Count);
        Assert.Equal(
            KnowledgeDocumentLimits.MaximumStepsPerTrace,
            maximumGroup.Traces[0].Steps.Count);
        Assert.All(
            current.FlowGroups,
            group => Assert.InRange(
                group.Traces.Count,
                1,
                KnowledgeDocumentLimits.MaximumTracesPerGroup));
        Assert.All(
            current.FlowGroups.SelectMany(group => group.Traces),
            trace => Assert.InRange(
                trace.Steps.Count,
                1,
                KnowledgeDocumentLimits.MaximumStepsPerTrace));
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Fact]
    public async Task GetCurrentAsync_TargetAuthorizationIsRevoked_RedactsRelationshipToMarkerOnly()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedSourceRepositoryAsync(session.Context);
        await SeedTargetRepositoryAsync(session.Context);
        KnowledgeStoreRepository repository = CreateRepository(session.Context);
        await CommitCrossRepositoryBusinessKnowledgeAsync(repository);

        CurrentKnowledge? authorized = await repository.GetCurrentAsync(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            new RepositoryAccessScope(
            [
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.TargetRepositoryId,
            ]),
            []);
        CurrentKnowledge? revoked = await repository.GetCurrentAsync(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]),
            []);

        Assert.NotNull(authorized);
        AuthorizedRelationship relationship =
            Assert.IsType<AuthorizedRelationship>(Assert.Single(authorized.Relationships));
        Assert.Equal(_edgeId, relationship.EdgeId);
        Assert.Equal(DocumentEdgeType.GeneratedFrom, relationship.EdgeType);
        Assert.Equal(KnowledgeStoreTestFixtures.DocumentVersionId, relationship.SourceDocumentVersionId);
        Assert.Equal(
            KnowledgeStoreTestFixtures.TargetDocumentVersionId,
            relationship.TargetDocumentVersionId);
        Assert.Equal(
            KnowledgeStoreTestFixtures.TargetRepositoryId,
            relationship.TargetRepositoryId);
        Assert.NotNull(revoked);
        RestrictedRelationship restricted =
            Assert.IsType<RestrictedRelationship>(Assert.Single(revoked.Relationships));
        Assert.True(restricted.IsRestrictedRelationship);
        Assert.Single(typeof(RestrictedRelationship).GetProperties());
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Fact]
    public async Task GetCurrentAsync_TargetAuthorizationIsRevoked_RedactsFlowStepIdentityAndBehavior()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedSourceRepositoryAsync(session.Context);
        await SeedTargetRepositoryAsync(session.Context);
        KnowledgeStoreRepository repository = CreateRepository(session.Context);
        await CommitCrossRepositoryBusinessKnowledgeAsync(repository);

        CurrentKnowledge? authorized = await repository.GetCurrentAsync(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            new RepositoryAccessScope(
            [
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.TargetRepositoryId,
            ]),
            []);
        CurrentKnowledge? revoked = await repository.GetCurrentAsync(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]),
            []);

        Assert.NotNull(authorized);
        CurrentFlowTrace authorizedTrace =
            Assert.Single(Assert.Single(authorized.FlowGroups).Traces);
        Assert.Equal([1, 2], authorizedTrace.Steps.Select(step => step.StepOrder));
        Assert.Equal(
            KnowledgeStoreTestFixtures.TargetDocumentVersionId,
            authorizedTrace.Steps[1].DocumentVersionId);
        Assert.Equal("RESTRICTED_BEHAVIOR_SECRET", authorizedTrace.Steps[1].Description);
        Assert.False(authorizedTrace.Steps[1].IsRestrictedRelationship);

        Assert.NotNull(revoked);
        CurrentFlowTrace revokedTrace =
            Assert.Single(Assert.Single(revoked.FlowGroups).Traces);
        Assert.Equal(
            KnowledgeStoreTestFixtures.DocumentVersionId,
            revokedTrace.Steps[0].DocumentVersionId);
        Assert.Equal("业务逻辑", revokedTrace.Steps[0].Description);
        Assert.False(revokedTrace.Steps[0].IsRestrictedRelationship);
        Assert.Null(revokedTrace.Steps[1].DocumentVersionId);
        Assert.Null(revokedTrace.Steps[1].Description);
        Assert.True(revokedTrace.Steps[1].IsRestrictedRelationship);
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Fact]
    public async Task GetCurrentAsync_TargetAuthorizationIsRevoked_RedactsSectionDocumentReference()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedSourceRepositoryAsync(session.Context);
        await SeedTargetRepositoryAsync(session.Context);
        KnowledgeStoreRepository repository = CreateRepository(session.Context);
        await CommitCrossRepositoryBusinessKnowledgeAsync(repository);

        CurrentKnowledge? authorized = await repository.GetCurrentAsync(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            new RepositoryAccessScope(
            [
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.TargetRepositoryId,
            ]),
            [KnowledgeStoreTestFixtures.FirstSectionId]);
        CurrentKnowledge? revoked = await repository.GetCurrentAsync(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]),
            [KnowledgeStoreTestFixtures.FirstSectionId]);

        CurrentDocumentSection authorizedSection =
            Assert.Single(Assert.IsType<CurrentKnowledge>(authorized).Sections);
        Assert.Equal(
            [KnowledgeStoreTestFixtures.TargetDocumentVersionId],
            authorizedSection.AuthorizedDocumentVersionIds);
        Assert.False(authorizedSection.HasRestrictedDocumentReference);
        CurrentDocumentSection revokedSection =
            Assert.Single(Assert.IsType<CurrentKnowledge>(revoked).Sections);
        Assert.Empty(revokedSection.AuthorizedDocumentVersionIds);
        Assert.True(revokedSection.HasRestrictedDocumentReference);
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    [Fact]
    public async Task GetCurrentAsync_TargetAuthorizationIsRevoked_DoesNotLeakTargetIdentityInCanonicalContent()
    {
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        await SeedSourceRepositoryAsync(session.Context);
        await SeedTargetRepositoryAsync(session.Context);
        KnowledgeStoreRepository repository = CreateRepository(session.Context);
        await CommitCrossRepositoryBusinessKnowledgeAsync(repository);

        CurrentKnowledge? revoked = await repository.GetCurrentAsync(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.SourceRepositoryId]),
            []);

        Assert.NotNull(revoked);
        Assert.DoesNotContain(
            KnowledgeStoreTestFixtures.TargetDocumentVersionId.ToString(),
            revoked.ContentJson,
            StringComparison.OrdinalIgnoreCase);
        string serialized = JsonSerializer.Serialize(revoked);
        string[] forbiddenValues =
        [
            KnowledgeStoreTestFixtures.TargetDocumentVersionId.ToString(),
            KnowledgeStoreTestFixtures.TargetRepositoryId.ToString(),
            _targetLogicalDocumentId.ToString(),
            _targetAnchorId.ToString(),
            _edgeId.ToString(),
            "受限目标",
            "src/restricted-target.cs",
            "RESTRICTED_SECRET_CONTENT",
            "RESTRICTED_SECRET_SUMMARY",
            "RestrictedSecretSymbol",
            "RestrictedSecretBusinessTerm",
            "RestrictedSecretTechnicalTerm",
            "src/restricted-source.cs",
            "RESTRICTED_PROVIDER_DIAGNOSTIC_SECRET",
            "RESTRICTED_BEHAVIOR_SECRET",
        ];
        foreach (string forbiddenValue in forbiddenValues)
        {
            Assert.DoesNotContain(
                forbiddenValue,
                serialized,
                StringComparison.OrdinalIgnoreCase);
        }

        RestrictedRelationship marker =
            Assert.IsType<RestrictedRelationship>(Assert.Single(revoked.Relationships));
        Assert.True(marker.IsRestrictedRelationship);
        Assert.Single(typeof(RestrictedRelationship).GetProperties());
        KnowledgeStoreTestHarness.AssertTrackerClean(session.Context);
    }

    private static KnowledgeStoreRepository CreateRepository(VulgataDbContext context) =>
        new(context, new KnowledgeDocumentMaterializer());

    private static KnowledgeCommit CreateSourceCommit(bool includeAnchor = false)
    {
        IReadOnlyList<Guid> anchorIds = includeAnchor ? [_anchorId] : [];
        var materializer = new KnowledgeDocumentMaterializer();
        KnowledgeDocumentMaterialization materialization = materializer.Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            null,
            KnowledgeStoreTestFixtures.CreateCanonicalJson(
                sections:
                [
                    new(
                        KnowledgeStoreTestFixtures.FirstSectionId,
                        1,
                        "Overview",
                        "概览",
                        "处理订单请求。",
                        "处理订单请求",
                        anchorIds,
                        []),
                    new(
                        KnowledgeStoreTestFixtures.SecondSectionId,
                        2,
                        "TechnicalDetail",
                        "实现",
                        "调用 API。",
                        "API orders",
                        [],
                        []),
                ]),
            KnowledgeStoreTestFixtures.CreatedAt);
        IReadOnlyList<SourceAnchorDraft> anchors = includeAnchor
            ?
            [
                new(
                    _anchorId,
                    KnowledgeStoreTestFixtures.DocumentVersionId,
                    KnowledgeStoreTestFixtures.SourceRepositoryId,
                    KnowledgeStoreTestFixtures.SourceScanRunId,
                    KnowledgeStoreTestFixtures.SourceCodeUnitId,
                    KnowledgeStoreTestFixtures.ProfiledRevision,
                    "src/orders.cs",
                    "OrderService:1-10",
                    1,
                    10,
                    KnowledgeStoreTestFixtures.SourceHash),
            ]
            : [];
        return new(
            Guid.Parse("e0000000-0000-0000-0000-000000000010"),
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            null,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.ProfiledRevision,
            materialization.Content,
            anchors,
            [],
            [],
            KnowledgeStoreTestFixtures.CreatedAt);
    }

    private static KnowledgeCommit CreateMaximumFiniteCommit()
    {
        KnowledgeDocumentMaterialization materialization =
            new KnowledgeDocumentMaterializer().Materialize(
                KnowledgeStoreTestFixtures.LogicalDocumentId,
                KnowledgeStoreTestFixtures.DocumentVersionId,
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                null,
                KnowledgeStoreTestFixtures.CreateCanonicalJson(documentType: "BusinessLogic"),
                KnowledgeStoreTestFixtures.CreatedAt);
        SourceAnchorDraft[] anchors = Enumerable
            .Range(1, KnowledgeDocumentLimits.MaximumAnchors)
            .Select(index => new SourceAnchorDraft(
                MaximumId("81000000", index),
                KnowledgeStoreTestFixtures.DocumentVersionId,
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.SourceScanRunId,
                KnowledgeStoreTestFixtures.SourceCodeUnitId,
                KnowledgeStoreTestFixtures.ProfiledRevision,
                "src/orders.cs",
                $"OrderService:{index}",
                1,
                1,
                KnowledgeStoreTestFixtures.SourceHash))
            .ToArray();
        int traceId = 0;
        int stepId = 0;
        FlowGroupDraft[] groups = Enumerable
            .Range(1, KnowledgeDocumentLimits.MaximumFlowGroups)
            .Select(groupIndex =>
            {
                int traceCount = groupIndex == 1
                    ? KnowledgeDocumentLimits.MaximumTracesPerGroup
                    : 1;
                FlowTraceDraft[] traces = Enumerable.Range(1, traceCount)
                    .Select(traceIndex =>
                    {
                        int stepCount = groupIndex == 1 && traceIndex == 1
                            ? KnowledgeDocumentLimits.MaximumStepsPerTrace
                            : 1;
                        FlowTraceStepDraft[] steps = Enumerable.Range(1, stepCount)
                            .Select(stepIndex => new FlowTraceStepDraft(
                                MaximumId("d1000000", ++stepId),
                                stepIndex,
                                KnowledgeStoreTestFixtures.DocumentVersionId,
                                KnowledgeStoreTestFixtures.SourceRepositoryId,
                                KnowledgeStoreTestFixtures.SourceScanRunId,
                                null,
                                FlowTraceStepKind.Document,
                                $"步骤 {stepIndex}"))
                            .ToArray();
                        return new FlowTraceDraft(
                            MaximumId("c1000000", ++traceId),
                            traceIndex,
                            steps);
                    })
                    .ToArray();
                return new FlowGroupDraft(
                    MaximumId("b1000000", groupIndex),
                    groupIndex,
                    $"流程 {groupIndex}",
                    traces);
            })
            .ToArray();
        return new(
            Guid.Parse("e1000000-0000-0000-0000-000000000001"),
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            null,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.ProfiledRevision,
            materialization.Content,
            anchors,
            [],
            groups,
            KnowledgeStoreTestFixtures.CreatedAt);
    }

    private static Guid MaximumTargetVersionId(int index) =>
        index == 1
            ? KnowledgeStoreTestFixtures.TargetDocumentVersionId
            : MaximumId("71000000", index);

    private static Guid MaximumId(string prefix, int index) =>
        Guid.Parse($"{prefix}-0000-0000-0000-{index:D12}");

    private static async Task CommitCrossRepositoryBusinessKnowledgeAsync(
        KnowledgeStoreRepository repository)
    {
        KnowledgeCommitResult target = await repository.CommitAsync(
            CreateTargetCommit(),
            new RepositoryAccessScope([KnowledgeStoreTestFixtures.TargetRepositoryId]));
        KnowledgeCommitResult source = await repository.CommitAsync(
            CreateBusinessCommit(),
            new RepositoryAccessScope(
            [
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.TargetRepositoryId,
            ]));
        Assert.True(
            target.Status == KnowledgeCommitStatus.Committed,
            $"Target commit rejected: {target.RejectionCode}");
        Assert.True(
            source.Status == KnowledgeCommitStatus.Committed,
            $"Source commit rejected: {source.RejectionCode}");
    }

    private static KnowledgeCommit CreateTargetCommit()
    {
        KnowledgeDocumentMaterialization materialization =
            new KnowledgeDocumentMaterializer().Materialize(
                _targetLogicalDocumentId,
                KnowledgeStoreTestFixtures.TargetDocumentVersionId,
                KnowledgeStoreTestFixtures.TargetRepositoryId,
                null,
                KnowledgeStoreTestFixtures.CreateCanonicalJson(
                    title: "受限目标",
                    repositoryRelativePath: "src/restricted-target.cs",
                    summary: "RESTRICTED_SECRET_SUMMARY",
                    keySymbols: ["RestrictedSecretSymbol"],
                    businessTerms: ["RestrictedSecretBusinessTerm"],
                    technicalTerms: ["RestrictedSecretTechnicalTerm"],
                    sections:
                    [
                        new(
                            Guid.Parse("60000000-0000-0000-0000-000000000020"),
                            1,
                            "TechnicalDetail",
                            "受限内容",
                            "RESTRICTED_SECRET_CONTENT",
                            "RESTRICTED_SECRET_CONTENT",
                            [_targetAnchorId],
                            []),
                    ]),
                KnowledgeStoreTestFixtures.CreatedAt);
        return new(
            Guid.Parse("e0000000-0000-0000-0000-000000000020"),
            _targetLogicalDocumentId,
            null,
            KnowledgeStoreTestFixtures.TargetDocumentVersionId,
            KnowledgeStoreTestFixtures.TargetRepositoryId,
            KnowledgeStoreTestFixtures.TargetScanRunId,
            KnowledgeStoreTestFixtures.ProfiledRevision,
            materialization.Content,
            [
                new(
                    _targetAnchorId,
                    KnowledgeStoreTestFixtures.TargetDocumentVersionId,
                    KnowledgeStoreTestFixtures.TargetRepositoryId,
                    KnowledgeStoreTestFixtures.TargetScanRunId,
                    _targetCodeUnitId,
                    KnowledgeStoreTestFixtures.ProfiledRevision,
                    "src/restricted-source.cs",
                    "RESTRICTED_PROVIDER_DIAGNOSTIC_SECRET",
                    1,
                    2,
                    KnowledgeStoreTestFixtures.SourceHash),
            ],
            [],
            [],
            KnowledgeStoreTestFixtures.CreatedAt);
    }

    private static KnowledgeCommit CreateBusinessCommit()
    {
        KnowledgeDocumentMaterialization materialization =
            new KnowledgeDocumentMaterializer().Materialize(
                KnowledgeStoreTestFixtures.LogicalDocumentId,
                KnowledgeStoreTestFixtures.DocumentVersionId,
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                null,
                KnowledgeStoreTestFixtures.CreateCanonicalJson(
                    documentType: "BusinessLogic",
                    sections:
                    [
                        new(
                            KnowledgeStoreTestFixtures.FirstSectionId,
                            1,
                            "Overview",
                            "业务概览",
                            "处理订单业务。",
                            "订单业务",
                            [],
                            [KnowledgeStoreTestFixtures.TargetDocumentVersionId]),
                    ]),
                KnowledgeStoreTestFixtures.CreatedAt);
        return KnowledgeStoreTestFixtures.CreateBusinessKnowledgeCommit(
            materialization,
            flowGroups:
            [
                new(
                    Guid.Parse("b0000000-0000-0000-0000-000000000001"),
                    1,
                    "订单流",
                    [
                        new(
                            Guid.Parse("c0000000-0000-0000-0000-000000000001"),
                            1,
                            [
                                new(
                                    Guid.Parse("d0000000-0000-0000-0000-000000000001"),
                                    1,
                                    KnowledgeStoreTestFixtures.DocumentVersionId,
                                    KnowledgeStoreTestFixtures.SourceRepositoryId,
                                    KnowledgeStoreTestFixtures.SourceScanRunId,
                                    null,
                                    FlowTraceStepKind.Document,
                                    "业务逻辑"),
                                new(
                                    Guid.Parse("d0000000-0000-0000-0000-000000000002"),
                                    2,
                                    KnowledgeStoreTestFixtures.TargetDocumentVersionId,
                                    KnowledgeStoreTestFixtures.TargetRepositoryId,
                                    KnowledgeStoreTestFixtures.TargetScanRunId,
                                    _edgeId,
                                    FlowTraceStepKind.Relationship,
                                    "RESTRICTED_BEHAVIOR_SECRET"),
                            ]),
                    ]),
            ]);
    }

    private static async Task SeedSourceRepositoryAsync(VulgataDbContext context)
    {
        Repository repository = Repository.CreateStandalone(
            "source",
            "https://example.invalid/source.git",
            null,
            null,
            KnowledgeStoreTestFixtures.CreatedAt);
        typeof(Repository)
            .GetProperty(nameof(Repository.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(repository, KnowledgeStoreTestFixtures.SourceRepositoryId);
        context.Repositories.Add(repository);
        context.ScanRuns.Add(
            KnowledgeStoreTestFixtures.CreateCompletedScanRun(
                KnowledgeStoreTestFixtures.SourceScanRunId,
                KnowledgeStoreTestFixtures.SourceRepositoryId));
        context.CodeUnits.Add(
            KnowledgeStoreTestFixtures.CreateCodeUnit(
                KnowledgeStoreTestFixtures.SourceCodeUnitId,
                KnowledgeStoreTestFixtures.SourceScanRunId,
                KnowledgeStoreTestFixtures.SourceRepositoryId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task SeedTargetRepositoryAsync(VulgataDbContext context)
    {
        Repository repository = Repository.CreateStandalone(
            "target",
            "https://example.invalid/target.git",
            null,
            null,
            KnowledgeStoreTestFixtures.CreatedAt);
        typeof(Repository)
            .GetProperty(nameof(Repository.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(repository, KnowledgeStoreTestFixtures.TargetRepositoryId);
        context.Repositories.Add(repository);
        context.ScanRuns.Add(
            KnowledgeStoreTestFixtures.CreateCompletedScanRun(
                KnowledgeStoreTestFixtures.TargetScanRunId,
                KnowledgeStoreTestFixtures.TargetRepositoryId));
        CodeUnit targetCodeUnit = KnowledgeStoreTestFixtures.CreateCodeUnit(
            _targetCodeUnitId,
            KnowledgeStoreTestFixtures.TargetScanRunId,
            KnowledgeStoreTestFixtures.TargetRepositoryId);
        typeof(CodeUnit)
            .GetProperty(nameof(CodeUnit.FilePath), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(targetCodeUnit, "src/restricted-source.cs");
        context.CodeUnits.Add(targetCodeUnit);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }
}
