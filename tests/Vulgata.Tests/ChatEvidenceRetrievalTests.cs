using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class ChatEvidenceRetrievalTests
{
    [Fact]
    public void RetrieveAsync_UsesSingleTypedPath_ObsoleteClassifierAndLegacyRowsAreAbsent()
    {
        string source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "dotnet",
                "Vulgata.Infrastructure",
                "Data",
                "ChatEvidenceRetriever.cs"));
        MethodInfo[] nonPublicMethods = typeof(ChatEvidenceRetriever)
            .GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
        Type[] nestedTypes = typeof(ChatEvidenceRetriever)
            .GetNestedTypes(BindingFlags.NonPublic);

        Assert.Equal(1, CountOccurrences(source, "_packetBuilder.Build("));
        Assert.DoesNotContain("new ChatEvidencePacketBuilder", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RetrieveProjectionOnlyAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ChatRelationshipEvidenceProjector", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EvidencePacket projectionPacket", source, StringComparison.Ordinal);
        Assert.DoesNotContain("projectionPacket.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentExactDecisionCandidate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RelationshipPacketRow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExactCrossRepositoryEdgeEvidence", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExactResolutionProfileCatalog", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            nonPublicMethods,
            method => method.Name == "RequiresCrossRepositoryEvidence");
        Assert.DoesNotContain(
            nestedTypes,
            type => type.Name is "ExactRelationshipRow" or "ProvenanceRow");
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("Vulgata repository root could not be located.");
    }

    [Fact]
    public async Task RetrieveAsync_CrossRepositoryLiteralAndNeutralEquivalent_PreserveLexicalPrimaryIdentitiesAsync()
    {
        await using PreparedEvidenceFixture literalFixture = await PreparedEvidenceFixture.CreateAsync();
        await using PreparedEvidenceFixture neutralFixture = await PreparedEvidenceFixture.CreateAsync();

        ChatEvidenceResult literal = await CreateRetriever(literalFixture.Context)
            .RetrieveAsync(
                literalFixture.CreateRequest(
                    "b1-literal",
                    question: "客户会话如何跨仓库更新？"),
                new RepositoryAccessScope(
                    literalFixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));
        ChatEvidenceResult neutral = await CreateRetriever(neutralFixture.Context)
            .RetrieveAsync(
                neutralFixture.CreateRequest(
                    "b1-neutral",
                    question: "客户会话如何更新？"),
                new RepositoryAccessScope(
                    neutralFixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        EvidencePacket literalPacket = Assert.IsType<EvidencePacket>(literal.Packet);
        EvidencePacket neutralPacket = Assert.IsType<EvidencePacket>(neutral.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, literal.Kind);
        Assert.Equal(ChatEvidenceResultKind.Found, neutral.Kind);
        Assert.Equal(
            neutralPacket.Sections.Select(section => section.StableSectionId),
            literalPacket.Sections.Select(section => section.StableSectionId));
        Assert.Equal(
            neutralPacket.Documents.Select(document => document.DocumentVersionId),
            literalPacket.Documents.Select(document => document.DocumentVersionId));
    }

    [Fact]
    public async Task RetrieveAsync_RelationshipExpansionMutation_DoesNotReplaceExistingLexicalPrimarySectionsAsync()
    {
        await using PreparedEvidenceFixture baselineFixture = await PreparedEvidenceFixture.CreateAsync();
        await using PreparedEvidenceFixture expandedFixture = await PreparedEvidenceFixture.CreateAsync();

        EvidencePacket baselinePacket = Assert.IsType<EvidencePacket>(
            (await CreateRetriever(baselineFixture.Context)
                .RetrieveAsync(
                    baselineFixture.CreateRequest("b1-baseline"),
                    new RepositoryAccessScope(
                        baselineFixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)))).Packet);

        await expandedFixture.AddCurrentOneHopReferenceAsync();
        EvidencePacket expandedPacket = Assert.IsType<EvidencePacket>(
            (await CreateRetriever(expandedFixture.Context)
                .RetrieveAsync(
                    expandedFixture.CreateRequest("b1-expanded"),
                    new RepositoryAccessScope(
                        expandedFixture.RepositoryIds,
                        new RecordingAuthorizationVerifier(true, true)))).Packet);

        Assert.All(
            baselinePacket.Sections,
            section => Assert.Contains(
                expandedPacket.Sections,
                expanded => expanded.StableSectionId == section.StableSectionId));
    }

    [Fact]
    public async Task RelationshipProjector_FirstThreeLexicalSeeds_AreOneHopBoundedAndCanonicalAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddCurrentOneHopReferenceAsync();

        Guid[] seeds =
        [
            fixture.OneHopDocumentVersionId,
            ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
            ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
            fixture.BusinessDocumentVersionId,
        ];
        TypedRelationshipProjection typedProjection = await ProjectRelationshipsAsync(
            fixture,
            ChatMode.Business,
            seeds);
        ChatRelationshipExpansionProjection projection = typedProjection.Expansion;

        Assert.NotEmpty(projection.Relationships);
        Guid[] expectedSeeds = seeds.Take(3).ToArray();
        Assert.All(
            projection.Relationships,
            relationship => Assert.Contains(
                typedProjection.DocumentVersionIdsByHandle[relationship.SourceDocumentHandle],
                expectedSeeds));
        Assert.DoesNotContain(
            projection.Relationships,
            relationship => typedProjection.DocumentVersionIdsByHandle[relationship.SourceDocumentHandle] ==
                fixture.SecondHopDocumentVersionId);
        Assert.All(
            projection.Relationships.GroupBy(relationship => relationship.SourceDocumentHandle),
            group => Assert.InRange(group.Count(), 1, 2));
        Assert.InRange(projection.Relationships.Count, 1, 32);
        Assert.InRange(projection.SupportSections.Count, 1, 20);
        Assert.InRange(projection.FlowGroups.Sum(group => group.Steps.Count), 0, 12);
        Assert.Equal(
            projection.Relationships.Select(relationship => relationship.EdgeId).Distinct(),
            projection.Relationships.Select(relationship => relationship.EdgeId));
        Assert.Equal(
            projection.Relationships
                .OrderBy(relationship => relationship.RelationshipType == ChatRelationshipType.References ? 0 : 1)
                .ThenBy(relationship => relationship.EdgeId)
                .Select(relationship => relationship.EdgeId),
            projection.Relationships.Select(relationship => relationship.EdgeId));
        Assert.Contains(
            projection.Relationships,
            relationship =>
                relationship.EdgeId == fixture.OneHopEdgeId &&
                relationship.RelationshipType == ChatRelationshipType.References);
        Assert.Contains(
            projection.Relationships,
            relationship =>
                relationship.EdgeId == fixture.SecondHopEdgeId &&
                relationship.RelationshipType == ChatRelationshipType.References);
    }

    [Fact]
    public async Task RelationshipProjector_LowerRankedOptionalRelatedDocument_DoesNotDisplaceHigherRankedEvidenceAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddCurrentOneHopReferenceAsync();
        await fixture.AddOptionalProviderSecondHopReferenceAsync();

        TypedRelationshipProjection typedProjection = await ProjectRelationshipsAsync(
            fixture,
            ChatMode.Business,
            [ExactCrossRepositoryTestValues.ProviderDocumentVersionId]);
        ChatRelationshipExpansionProjection projection = typedProjection.Expansion;

        EvidenceRelationship relationship = Assert.Single(
            projection.Relationships,
            item => item.EdgeId == fixture.OneHopEdgeId);
        Assert.Equal(fixture.OneHopEdgeId, relationship.EdgeId);
        Assert.Equal(
            fixture.OneHopDocumentVersionId,
            typedProjection.DocumentVersionIdsByHandle[relationship.TargetDocumentHandle]);
        Assert.Contains(
            projection.Relationships,
            item => typedProjection.DocumentVersionIdsByHandle[item.TargetDocumentHandle] ==
                fixture.SecondHopDocumentVersionId);
    }

    [Fact]
    public async Task RelationshipProjector_CandidateBeyondLegacyQueryCap_HigherSeedRankWinsBeforeBoundsAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        Guid expectedEdgeId = await fixture.AddCandidatesBeyondLegacyQueryCapAsync();

        TypedRelationshipProjection typedProjection = await ProjectRelationshipsAsync(
            fixture,
            ChatMode.Business,
            [
                ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
                ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
                fixture.BusinessDocumentVersionId,
            ]);
        ChatRelationshipExpansionProjection projection = typedProjection.Expansion;

        EvidenceRelationship relationship = Assert.Single(
            projection.Relationships,
            item => item.EdgeId == expectedEdgeId);
        Assert.Equal(expectedEdgeId, relationship.EdgeId);
        Assert.Equal(
            ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
            typedProjection.DocumentVersionIdsByHandle[relationship.SourceDocumentHandle]);
    }

    [Fact]
    public async Task RelationshipProjector_BusinessAndItMode_PreferAcceptedRelationshipTypeBeforeBoundsAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.RemoveOptionalCrossRepositorySupportAsync();
        (Guid generatedFromEdgeId, Guid referencesEdgeId) =
            await fixture.AddModePreferenceCandidatesAsync();
        ChatRelationshipExpansionProjection businessProjection = (await ProjectRelationshipsAsync(
            fixture,
            ChatMode.Business,
            [fixture.BusinessDocumentVersionId])).Expansion;
        ChatRelationshipExpansionProjection itProjection = (await ProjectRelationshipsAsync(
            fixture,
            ChatMode.It,
            [fixture.BusinessDocumentVersionId])).Expansion;

        Assert.Contains(
            businessProjection.Relationships,
            relationship => relationship.EdgeId == generatedFromEdgeId);
        Assert.DoesNotContain(
            businessProjection.Relationships,
            relationship => relationship.EdgeId == referencesEdgeId);
        Assert.Contains(
            itProjection.Relationships,
            relationship => relationship.EdgeId == referencesEdgeId);
    }

    [Fact]
    public async Task RetrieveAsync_Exactly256RankedCandidates_IsAcceptedAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.RemoveV2AuthorityAsync();
        await fixture.AddBusinessSectionsAsync(253);

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("ranked-window-256"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.InRange(packet.Sections.Count, 1, 12);
    }

    [Fact]
    public async Task RetrieveAsync_MoreThan256EligibleCandidates_RanksFullSetAndKeepsDeterministicHighestWindowAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.RemoveV2AuthorityAsync();
        await fixture.RemoveOptionalCrossRepositorySupportAsync();
        Guid expectedPrimarySectionId = await fixture
            .AddLateHighestRankedBusinessSectionAsync(300);

        ChatRetrievalRequest request = fixture.CreateRequest(
            "ranked-window-over-256",
            question: "WindowPriorityToken");
        RepositoryAccessScope scope = new(
            fixture.RepositoryIds,
            new RecordingAuthorizationVerifier(true, true, true, true));
        ChatEvidenceRetriever retriever = CreateRetriever(fixture.Context);

        ChatEvidenceResult first = await retriever.RetrieveAsync(request, scope);
        ChatEvidenceResult repeated = await retriever.RetrieveAsync(request, scope);

        Assert.Equal(ChatEvidenceResultKind.Found, first.Kind);
        Assert.Equal(ChatEvidenceResultKind.Found, repeated.Kind);
        EvidencePacket firstPacket = Assert.IsType<EvidencePacket>(first.Packet);
        EvidencePacket repeatedPacket = Assert.IsType<EvidencePacket>(repeated.Packet);
        Assert.Equal(
            [expectedPrimarySectionId],
            firstPacket.Sections.Select(section => section.StableSectionId));
        Assert.Equal(
            firstPacket.Sections.Select(section => section.StableSectionId),
            repeatedPacket.Sections.Select(section => section.StableSectionId));
        Assert.Equal(
            firstPacket.Sections.Count,
            firstPacket.Sections.Select(section => section.StableSectionId).Distinct().Count());
    }

    [Fact]
    public async Task RetrieveAsync_SelectsTwelvePrimariesWithAtMostFourSectionsPerDocumentAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.RemoveV2AuthorityAsync();
        await fixture.AddFourScoredSectionsPerDocumentAsync();

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("primary-sections-12"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.Equal(12, packet.Sections.Count);
        Assert.Equal(3, packet.Sections.GroupBy(section => section.DocumentHandle).Count());
        Assert.All(
            packet.Sections.GroupBy(section => section.DocumentHandle),
            group => Assert.Equal(4, group.Count()));
    }

    [Fact]
    public async Task RetrieveAsync_MoreThanTwentyEligibleSections_RemainsBoundedAndDeterministicAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.RemoveV2AuthorityAsync();
        await fixture.AddMoreThanTwentyEligibleSectionsAsync();

        RepositoryAccessScope scope = new(
            fixture.RepositoryIds,
            new RecordingAuthorizationVerifier(true, true, true, true));
        ChatRetrievalRequest request = fixture.CreateRequest("eligible-sections-over-20");
        ChatEvidenceRetriever retriever = CreateRetriever(fixture.Context);

        ChatEvidenceResult first = await retriever.RetrieveAsync(request, scope);
        ChatEvidenceResult repeated = await retriever.RetrieveAsync(request, scope);
        Assert.Equal(ChatEvidenceResultKind.Found, first.Kind);
        Assert.Equal(ChatEvidenceResultKind.Found, repeated.Kind);
        EvidencePacket firstPacket = Assert.IsType<EvidencePacket>(first.Packet);
        EvidencePacket repeatedPacket = Assert.IsType<EvidencePacket>(repeated.Packet);

        Assert.InRange(firstPacket.Sections.Count, 1, 12);
        Assert.InRange(firstPacket.Sections.Count, 1, 20);
        Assert.All(
            firstPacket.Sections.GroupBy(section => section.DocumentHandle),
            group => Assert.InRange(group.Count(), 1, 4));
        Assert.Equal(
            firstPacket.Sections.Select(section => section.StableSectionId),
            repeatedPacket.Sections.Select(section => section.StableSectionId));
    }

    [Fact]
    public async Task RetrieveAsync_SixthLowerRankedDocument_IsExcludedAndPacketRemainsBoundedAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.ExceedDocumentMaximumAsync();

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("document-bound-6"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.InRange(packet.Documents.Count, 1, 5);
    }

    [Fact]
    public async Task RetrieveAsync_AcceptedV2DecisionAndCurrentEdgeWithoutLegacyEvidence_ReturnsCompletePacketAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.RemoveLegacyEvidenceAsync();

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
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

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest(
                    "legacy-only",
                    question: "完全无关的主题"),
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
        var retriever = CreateRetriever(fixture.Context);

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

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
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
    public async Task RetrieveAsync_DuplicateRelatedTarget_ReturnsIncompleteOrStaleAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddCurrentOneHopReferenceAsync();
        await fixture.AddDuplicateOneHopTargetAsync();

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("duplicate-related-target"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.IncompleteOrStale, result.Kind);
        Assert.Null(result.Packet);
    }

    [Fact]
    public async Task RetrieveAsync_SelectedPublishedRelatedFlowGroup_IsIncludedWithItsAuthorityAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddCurrentOneHopReferenceAsync();
        (Guid relatedEdgeId, Guid relatedDecisionId) = await fixture.AddSecondAcceptedV2ReferenceAsync();
        await fixture.AddPublishedOneHopFlowGroupAsync(relatedEdgeId, relatedDecisionId);

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("related-flow-group"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.Contains(packet.FlowGroups, flowGroup =>
            flowGroup.ExactResolutionEdgeId == relatedEdgeId &&
            flowGroup.ExactResolutionDecisionId == relatedDecisionId);
    }

    [Fact]
    public async Task RetrieveAsync_MultiplePublishedFlowGroupsForOneRelationship_PreservesEveryGroupAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddCurrentOneHopReferenceAsync();
        (Guid relatedEdgeId, Guid relatedDecisionId) = await fixture.AddSecondAcceptedV2ReferenceAsync();
        Guid secondRelatedDecisionId = await fixture.AddAdditionalAcceptedV2DecisionAsync(
            relatedEdgeId,
            relatedDecisionId);
        await fixture.AddPublishedOneHopFlowGroupAsync(relatedEdgeId, relatedDecisionId);
        await fixture.AddSecondPublishedOneHopFlowGroupAsync(relatedEdgeId, secondRelatedDecisionId);

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("multiple-related-flow-groups"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.Contains(packet.FlowGroups, group => group.FlowGroupId == fixture.RelatedFlowGroupId);
        Assert.Contains(packet.FlowGroups, group => group.FlowGroupId == fixture.SecondRelatedFlowGroupId);
        Assert.Equal(4, packet.FlowEvidence.Count);
    }

    [Fact]
    public async Task RetrieveAsync_SharedReferencesEdgeWithCanonicalDecisionAndReversedFlowGroupIds_PreservesAuthorityAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        CrossRepositoryResolutionDecision primaryDecision = await fixture.Context
            .CrossRepositoryResolutionDecisions
            .AsNoTracking()
            .SingleAsync(decision => decision.DocumentEdgeId == fixture.ReferencesEdgeId);
        Guid secondaryDecisionId = await fixture.AddAdditionalAcceptedV2DecisionAsync(
            fixture.ReferencesEdgeId,
            primaryDecision.Id);
        await fixture.AddSecondPublishedOneHopFlowGroupAsync(
            fixture.ReferencesEdgeId,
            secondaryDecisionId,
            ExactCrossRepositoryTestValues.ProviderDocumentVersionId);

        CrossRepositoryResolutionDecision[] decisions = await fixture.Context
            .CrossRepositoryResolutionDecisions
            .AsNoTracking()
            .Where(decision => decision.DocumentEdgeId == fixture.ReferencesEdgeId)
            .ToArrayAsync();
        ExactResolutionProviderEvidenceContribution[] contributions = await fixture.Context
            .ExactResolutionProviderEvidenceContributions
            .AsNoTracking()
            .Where(contribution => decisions.Select(decision => decision.Id)
                .Contains(contribution.ResolutionDecisionId))
            .ToArrayAsync();
        DocumentEdge referencesEdge = await fixture.Context.DocumentEdges
            .AsNoTracking()
            .SingleAsync(edge => edge.Id == fixture.ReferencesEdgeId);
        Assert.True(
            ChatExactDecisionSetAuthority.TryCreate(
                referencesEdge,
                decisions,
                contributions,
                out ChatExactDecisionSet expectedDecisionSet));
        Assert.Equal(secondaryDecisionId, expectedDecisionSet.SelectedDecision.Id);
        Assert.True(fixture.SecondRelatedFlowGroupId > fixture.BaseFlowGroupId);

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("shared-edge-reversed-flow-group-order"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        EvidenceRelationship relationship = Assert.Single(
            packet.Relationships,
            item => item.EdgeId == fixture.ReferencesEdgeId);
        Assert.Equal(expectedDecisionSet.SelectedDecision.Id, relationship.ExactDecisionId);
        Assert.Equal(expectedDecisionSet.Count, relationship.ExactDecisionCount);
        Assert.Equal(expectedDecisionSet.Fingerprint, relationship.ExactDecisionSetFingerprint);
        Assert.Equal(
            expectedDecisionSet.SelectedDecision.ProviderEvidenceSetFingerprint,
            relationship.ProfileFingerprint);
        Assert.Equal(2, packet.FlowGroups.Count);
        Assert.Equal(
            expectedDecisionSet.Decisions.Select(decision => decision.Id).ToHashSet(),
            packet.FlowGroups
                .Select(group => group.ExactResolutionDecisionId)
                .OfType<Guid>()
                .ToHashSet());
        Assert.All(
            packet.FlowGroups,
            group =>
            {
                Assert.Equal(fixture.ReferencesEdgeId, group.ExactResolutionEdgeId);
                Assert.Contains(
                    expectedDecisionSet.Decisions,
                    decision => decision.Id == group.ExactResolutionDecisionId);
                Assert.Equal("Published", group.State);
                Assert.Matches("^[0-9a-f]{64}$", group.PlanFingerprint);
                Assert.Matches("^[0-9a-f]{64}$", group.MembershipFingerprint);
            });
    }

    [Fact]
    public async Task RetrieveAsync_BoundedValidatedConversation_IsBoundToReturnedPacketAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        ChatRetrievalRequest emptyRequest = fixture.CreateRequest("conversation-empty");
        ChatEvidenceResult emptyResult = await CreateRetriever(fixture.Context)
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

        ChatEvidenceResult contextualResult = await CreateRetriever(fixture.Context)
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
    public async Task RetrieveAsync_CurrentNonFlowQuestion_DoesNotRequirePreparedExactRelationshipAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.RemoveV2AuthorityAsync();

        ChatEvidenceResult result = await CreateRetriever(
                fixture.Context,
                new ProjectionOnlyScopeResolver())
            .RetrieveAsync(
                fixture.CreateRequest(
                    "projection-only",
                    question: "客户会话更新业务流程"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.Contains(
            Assert.IsType<EvidencePacket>(result.Packet).Sections,
            section => section.Heading == "业务概览");
    }

    [Fact]
    public async Task RetrieveAsync_CrossRepositoryQuestionWithoutExactRelationship_UsesProjectionPrimaryAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.RemoveV2AuthorityAsync();

        ChatEvidenceResult result = await CreateRetriever(
                fixture.Context,
                new ProjectionOnlyScopeResolver())
            .RetrieveAsync(
                fixture.CreateRequest(
                    "cross-repository-without-exact-relationship",
                    question: "客户会话如何跨仓库更新？"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Contains(packet.Sections, section => section.Heading == "业务概览");
    }

    [Fact]
    public async Task RetrieveAsync_RepositoryNameTerm_SelectsCurrentRepositoryDocumentAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.RemoveV2AuthorityAsync();

        ChatEvidenceResult result = await CreateRetriever(
                fixture.Context,
                new ProjectionOnlyScopeResolver())
            .RetrieveAsync(
                fixture.CreateRequest(
                    "repository-name-term",
                    question: "fixture-provider"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.Contains(
            packet.Documents,
            document => document.RepositoryId == ExactCrossRepositoryTestValues.ProviderRepositoryId);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("profile")]
    public async Task RetrieveAsync_ProjectionEndpointWithoutCurrentScanIdentity_ReturnsIncompleteOrStaleAsync(
        string mutation)
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.RemoveV2AuthorityAsync();
        if (mutation == "status")
        {
            await fixture.SetConsumerScanRunStatusAsync("Profiling");
        }
        else
        {
            await fixture.DriftConsumerProfiledRevisionAsync();
        }

        ChatEvidenceResult result = await CreateRetriever(
                fixture.Context,
                new ProjectionOnlyScopeResolver())
            .RetrieveAsync(
                fixture.CreateRequest("projection-current-identity"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.IncompleteOrStale, result.Kind);
        Assert.Null(result.Packet);
    }

    [Fact]
    public async Task RetrieveAsync_AcceptedV2Relationship_UsesProjectionFirstLexicalSelectionAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddStandaloneCurrentCodeLogicAsync();

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest(
                    "projection-first-with-v2",
                    question: "SpecialSessionAnchor"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.Contains(packet.Sections, section => section.Heading == "特殊会话锚点说明");
        Assert.DoesNotContain(packet.Sections, section => section.Heading is "业务概览" or "消费端" or "提供端");
    }

    [Fact]
    public async Task RetrieveAsync_MultipleAcceptedV2Edges_DoesNotRequireSingletonExactDecisionAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddCurrentOneHopReferenceAsync();
        await fixture.AddSecondAcceptedV2ReferenceAsync();

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("multiple-accepted-v2-edges"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.NotNull(result.Packet);
    }

    [Fact]
    public async Task RetrieveAsync_CrossRepositoryQuestionWithoutOptionalProvenanceOrFlowGroup_UsesProjectionPrimaryAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.RemoveOptionalCrossRepositorySupportAsync();

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("cross-projection-without-optional-support"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.Contains(packet.Sections, section => section.Heading == "业务概览");
    }

    [Fact]
    public async Task RetrieveAsync_CrossRepositoryQuestion_IgnoresUnrelatedStaleCrossRepositoryEdgeAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddCurrentOneHopReferenceAsync();
        await fixture.RemoveV2AuthorityAsync();
        await fixture.AddStaleUnrelatedCrossRepositoryEdgeAsync();

        ChatEvidenceResult result = await CreateRetriever(
                fixture.Context,
                new ProjectionOnlyScopeResolver())
            .RetrieveAsync(
                fixture.CreateRequest(
                    "stale-unrelated-cross-edge",
                    question: "客户会话如何跨仓库更新？"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.NotNull(result.Packet);
    }

    [Fact]
    public async Task RetrieveAsync_EmptyAdditionalPublishedFlowGroup_RejectsWholePacketAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddCurrentOneHopReferenceAsync();
        await fixture.AddPublishedOneHopFlowGroupAsync();
        await fixture.AddEmptyPublishedOneHopFlowGroupAsync();

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("empty-additional-flow-group"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.IncompleteOrStale, result.Kind);
        Assert.Null(result.Packet);
    }

    [Fact]
    public async Task RetrieveAsync_ItQuestion_SelectsTechnicalDetailAndEvidenceProjectionSectionsAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddTechnicalAndEvidenceSectionsAsync();
        await fixture.RemoveV2AuthorityAsync();

        ChatEvidenceResult result = await CreateRetriever(
                fixture.Context,
                new ProjectionOnlyScopeResolver())
            .RetrieveAsync(
                fixture.CreateRequest(
                    "it-technical-evidence",
                    question: "TechnicalBoundaryToken",
                    mode: ChatMode.It),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.Contains(packet.Sections, section => section.Heading == "技术实现细节");
        Assert.Contains(packet.Sections, section => section.Heading == "技术证据");
    }

    [Fact]
    public async Task RetrieveAsync_ItModeZeroPositivePrimary_UsesServerOwnedAdvisoryBusinessSummaryFallbackAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        const string composedAdvisoryText = "会话更新";
        var advisoryContext = new ChatAdvisoryContextSnapshot(
            [
                new ChatAdvisoryContextSourceUnit(
                    ChatAdvisoryContextSourceKind.System,
                    ExactCrossRepositoryTestValues.SystemId,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt,
                    composedAdvisoryText),
            ],
            composedAdvisoryText,
            ["会话", "更新"],
            Encoding.UTF8.GetByteCount(composedAdvisoryText),
            Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes("server-owned-t070"))));

        var advisoryReader = new TransactionObservingAdvisoryContextReader(
            fixture.Context,
            () => 1,
            ChatAdvisoryContextReadResult.Succeeded(advisoryContext),
            ChatAdvisoryContextReadResult.Succeeded(advisoryContext));
        ChatEvidenceResult result = await CreateRetrieverWithAdvisoryReader(
                fixture.Context,
                advisoryReader)
            .RetrieveAsync(
                fixture.CreateRequest(
                    "it-advisory-fallback",
                    question: "北京天气怎么样？",
                    mode: ChatMode.It),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.Equal(composedAdvisoryText, packet.AdvisoryContext);
        Assert.Contains(
            packet.Sections.Where(section =>
                packet.Selection.PrimarySectionHandles.Contains(section.SectionHandle)),
            section => section.Heading == "业务概览");
    }

    [Fact]
    public async Task RetrieveAsync_TransactionAdvisorySnapshot_IgnoresCallerAndBindsSingleAuthorizedReadAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        ChatAdvisoryContextSnapshot callerSnapshot = CreateAdvisorySnapshot(
            "caller-forged-context",
            "caller-forged-authority");
        ChatAdvisoryContextSnapshot transactionSnapshot = CreateAdvisorySnapshot(
            "server-transaction-context",
            "server-transaction-authority");
        ChatAdvisoryContextSnapshot afterReadMutation = CreateAdvisorySnapshot(
            "mutated-after-read-context",
            "mutated-after-read-authority");
        var verifier = new RecordingAuthorizationVerifier(true, true);
        var reader = new TransactionObservingAdvisoryContextReader(
            fixture.Context,
            () => verifier.CallCount,
            ChatAdvisoryContextReadResult.Succeeded(transactionSnapshot),
            ChatAdvisoryContextReadResult.Succeeded(afterReadMutation));

        ChatEvidenceResult result = await CreateRetrieverWithAdvisoryReader(
                fixture.Context,
                reader)
            .RetrieveAsync(
                fixture.CreateRequest(
                    "transaction-advisory-authority",
                    advisoryContext: callerSnapshot),
                new RepositoryAccessScope(fixture.RepositoryIds, verifier));

        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.Equal(1, reader.CallCount);
        Assert.Equal(1, reader.AuthorizationCallCountAtRead);
        Assert.Equal(IsolationLevel.Serializable, reader.IsolationLevelAtRead);
        Assert.Equal(transactionSnapshot.ComposedText, packet.AdvisoryContext);
        Assert.Equal(
            transactionSnapshot.AuthorityFingerprint,
            packet.AdvisoryContextAuthorityFingerprint);
        Assert.DoesNotContain(callerSnapshot.ComposedText, packet.AdvisoryContext, StringComparison.Ordinal);
        Assert.DoesNotContain(afterReadMutation.ComposedText, packet.AdvisoryContext, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        ChatAdvisoryContextReadResultKind.NotFoundOrUnauthorized,
        ChatEvidenceResultKind.NotFoundOrUnauthorized)]
    [InlineData(
        ChatAdvisoryContextReadResultKind.InconsistentCurrentEvidence,
        ChatEvidenceResultKind.IncompleteOrStale)]
    public async Task RetrieveAsync_ServerOwnedAdvisoryClosedResult_FailsClosedAsync(
        ChatAdvisoryContextReadResultKind readerKind,
        ChatEvidenceResultKind expectedKind)
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        ChatAdvisoryContextReadResult closedResult = readerKind switch
        {
            ChatAdvisoryContextReadResultKind.NotFoundOrUnauthorized =>
                ChatAdvisoryContextReadResult.NotFoundOrUnauthorized(),
            ChatAdvisoryContextReadResultKind.InconsistentCurrentEvidence =>
                ChatAdvisoryContextReadResult.InconsistentCurrentEvidence(),
            _ => throw new InvalidOperationException("The test requires a closed advisory result."),
        };
        var reader = new TransactionObservingAdvisoryContextReader(
            fixture.Context,
            () => 1,
            closedResult,
            closedResult);

        ChatEvidenceResult result = await CreateRetrieverWithAdvisoryReader(
                fixture.Context,
                reader)
            .RetrieveAsync(
                fixture.CreateRequest("closed-advisory-authority"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true)));

        Assert.Equal(expectedKind, result.Kind);
        Assert.Null(result.Packet);
        Assert.Equal(1, reader.CallCount);
        Assert.Equal(IsolationLevel.Serializable, reader.IsolationLevelAtRead);
    }

    [Theory]
    [InlineData("北京天气怎么样？")]
    [InlineData("如何 主要 系统 客户 用户 进行 可以 需要 之后")]
    public async Task RetrieveAsync_AcceptedV2Relationship_WithoutLexicalMatch_ReturnsInsufficientEvidenceAsync(
        string question)
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest("projection-refusal", question: question),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.IncompleteOrStale, result.Kind);
        Assert.Null(result.Packet);
    }

    [Fact]
    public async Task RetrieveAsync_FollowUpQuestion_UsesPriorValidatedQuestionForCurrentLexicalSelectionAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddStandaloneCurrentCodeLogicAsync();

        ChatRetrievalRequest firstRequest = fixture.CreateRequest(
            "conversation-seed",
            question: "特殊会话锚点");
        ChatEvidenceResult firstResult = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                firstRequest,
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));
        EvidencePacket firstPacket = Assert.IsType<EvidencePacket>(firstResult.Packet);
        EvidenceSection citedSection = Assert.Single(
            firstPacket.Sections,
            section => section.Heading == "特殊会话锚点说明");
        EvidenceDocument citedDocument = Assert.Single(
            firstPacket.Documents,
            document => document.DocumentHandle == citedSection.DocumentHandle);
        ChatConversationState conversation = firstRequest.Conversation.AppendValidatedTurn(
            ValidatedConversationTurn.Create(
                firstRequest.Question,
                "已验证的特殊会话锚点答案。",
                firstPacket.Fingerprint,
                [citedSection.SectionHandle],
                [citedDocument.LogicalDocumentId]));

        ChatEvidenceResult followUpResult = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest(
                    "conversation-follow-up",
                    conversation,
                    "它如何工作？"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.Found, followUpResult.Kind);
        Assert.Contains(
            Assert.IsType<EvidencePacket>(followUpResult.Packet).Sections,
            section => section.Heading == "特殊会话锚点说明");
    }

    [Fact]
    public async Task RetrieveAsync_FollowUpPriorEvidenceNoLongerCurrent_ReturnsMissingEquivalentAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddStandaloneCurrentCodeLogicAsync();

        ChatRetrievalRequest firstRequest = fixture.CreateRequest(
            "conversation-currentness-seed",
            question: "特殊会话锚点");
        ChatEvidenceResult firstResult = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                firstRequest,
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));
        EvidencePacket firstPacket = Assert.IsType<EvidencePacket>(firstResult.Packet);
        EvidenceSection citedSection = Assert.Single(
            firstPacket.Sections,
            section => section.Heading == "特殊会话锚点说明");
        EvidenceDocument citedDocument = Assert.Single(
            firstPacket.Documents,
            document => document.DocumentHandle == citedSection.DocumentHandle);
        ChatConversationState conversation = firstRequest.Conversation.AppendValidatedTurn(
            ValidatedConversationTurn.Create(
                firstRequest.Question,
                "已验证的特殊会话锚点答案。",
                firstPacket.Fingerprint,
                [citedSection.SectionHandle],
                [citedDocument.LogicalDocumentId]));
        await fixture.RemoveStandaloneCurrentCodeLogicEvidenceAsync();

        ChatEvidenceResult followUpResult = await CreateRetriever(fixture.Context)
            .RetrieveAsync(
                fixture.CreateRequest(
                    "conversation-currentness-follow-up",
                    conversation,
                    "它如何工作？"),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        Assert.Equal(ChatEvidenceResultKind.NotFoundOrUnauthorized, followUpResult.Kind);
        Assert.Null(followUpResult.Packet);
        Assert.Empty(conversation.Turns);
    }

    [Theory]
    [InlineData("SpecialSessionAnchor")]
    [InlineData("src/main/java/provider/SpecialSessionAnchor.java")]
    [InlineData("POST /special-session-anchor")]
    [InlineData("technical-session-term")]
    public async Task RetrieveAsync_CurrentIndexEntryMetadataMatch_RetainsPositiveProseContentMatchAsync(
        string question)
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.AddStandaloneCurrentCodeLogicAsync();
        await fixture.RemoveV2AuthorityAsync();

        ChatEvidenceResult result = await CreateRetriever(
                fixture.Context,
                new ProjectionOnlyScopeResolver())
            .RetrieveAsync(
                fixture.CreateRequest(
                    "index-symbol-precedence",
                    question: question),
                new RepositoryAccessScope(
                    fixture.RepositoryIds,
                    new RecordingAuthorizationVerifier(true, true)));

        EvidencePacket packet = Assert.IsType<EvidencePacket>(result.Packet);
        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.Contains(packet.Sections, section => section.Heading == "特殊会话锚点说明");
        Assert.Contains(packet.Sections, section => section.Heading == "普通说明");
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
            fixture => fixture.ExceedAnchorMaximumAsync());
        await AssertClosedFailureAfterMutationAsync(
            fixture => fixture.ExceedFlowMaximumAsync());
        await using PreparedEvidenceFixture revokedFixture =
            await PreparedEvidenceFixture.CreateAsync();
        var revokedScope = new RepositoryAccessScope(
            revokedFixture.RepositoryIds,
            new RecordingAuthorizationVerifier(true, false));
        ChatEvidenceResult revoked = await CreateRetriever(revokedFixture.Context)
            .RetrieveAsync(revokedFixture.CreateRequest("revoked"), revokedScope);

        Assert.Equal(ChatEvidenceResultKind.NotFoundOrUnauthorized, revoked.Kind);
        Assert.Null(revoked.Packet);
        Assert.Null(revokedFixture.Context.Database.CurrentTransaction);

        var missingEndpointScope = new RepositoryAccessScope(
            [ExactCrossRepositoryTestValues.ConsumerRepositoryId]);
        ChatEvidenceResult missingEndpoint = await CreateRetriever(revokedFixture.Context)
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

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(fixture.CreateRequest($"wrong-{field}"), scope);

        Assert.Equal(ChatEvidenceResultKind.Found, result.Kind);
        Assert.NotNull(result.Packet);
    }

    [Fact]
    public async Task RetrieveAsync_LegacyReversedRelationshipEvidence_DoesNotAffectAcceptedV2PacketAsync()
    {
        await using PreparedEvidenceFixture fixture = await PreparedEvidenceFixture.CreateAsync();
        await fixture.ReverseExactRelationshipEvidenceAsync();

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
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
        var retriever = CreateRetriever(fixture.Context);

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

        ChatEvidenceResult result = await CreateRetriever(fixture.Context).RetrieveAsync(
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

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
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

        ChatEvidenceResult result = await CreateRetriever(fixture.Context)
            .RetrieveAsync(fixture.CreateRequest(Guid.NewGuid().ToString("N")), scope);

        Assert.True(
            result.Kind is ChatEvidenceResultKind.NotFoundOrUnauthorized or
                ChatEvidenceResultKind.IncompleteOrStale,
            $"Mutation did not close packet: {mutationName}");
        Assert.Null(result.Packet);
        Assert.Null(fixture.Context.Database.CurrentTransaction);
    }

    private static async Task<TypedRelationshipProjection> ProjectRelationshipsAsync(
        PreparedEvidenceFixture fixture,
        ChatMode mode,
        IReadOnlyList<Guid> orderedPrimaryDocumentVersionIds)
    {
        AuthorizedChatDocument[] currentDocuments = await (
            from logical in fixture.Context.LogicalDocuments.AsNoTracking()
            join version in fixture.Context.DocumentVersions.AsNoTracking()
                on logical.CurrentVersionId equals version.Id
            join scanRun in fixture.Context.ScanRuns.AsNoTracking()
                on version.ScanRunId equals scanRun.Id
            where fixture.RepositoryIds.Contains(version.RepositoryId) &&
                version.SystemId == ExactCrossRepositoryTestValues.SystemId &&
                version.SupersededById == null &&
                scanRun.Status == ScanRunStatus.Completed
            select new AuthorizedChatDocument(
                logical.Id,
                version.Id,
                version.RepositoryId,
                version.ScanRunId,
                logical.DocumentType,
                version.Title,
                version.ProfiledRevision))
            .ToArrayAsync();
        Dictionary<Guid, AuthorizedChatDocument> currentByVersionId = currentDocuments
            .ToDictionary(document => document.DocumentVersionId);
        EvidenceDocument[] primaryDocuments = orderedPrimaryDocumentVersionIds
            .Select((documentVersionId, index) =>
            {
                AuthorizedChatDocument document = currentByVersionId[documentVersionId];
                return new EvidenceDocument(
                    $"d{index + 1}",
                    document.LogicalDocumentId,
                    document.DocumentVersionId,
                    document.RepositoryId,
                    document.DocumentType,
                    document.Title,
                    document.ProfiledRevision);
            })
            .ToArray();
        EvidenceDocument firstDocument = primaryDocuments[0];
        AuthorizedChatDocument firstAuthorizedDocument = currentByVersionId[firstDocument.DocumentVersionId];
        Guid stableSectionId = Guid.Parse("00000000-0000-0000-0000-000000090001");
        Guid sourceAnchorId = Guid.Parse("00000000-0000-0000-0000-000000090002");
        ChatLexicalPrimaryProjection primary = ChatLexicalPrimaryProjection.Create(
            primaryDocuments,
            [new EvidenceSection(
                "s-primary",
                firstDocument.DocumentHandle,
                stableSectionId,
                1,
                DocumentSectionKind.Overview,
                "主证据",
                "主证据内容。",
                [],
                [])],
            [],
            new string('a', 64));
        var accessScope = new RepositoryAccessScope(
            fixture.RepositoryIds,
            new RecordingAuthorizationVerifier(true));
        AuthorizedChatSnapshot snapshot = AuthorizedChatSnapshot.Create(
            new AuthorizedChatScope(
                ExactCrossRepositoryTestValues.SystemId,
                fixture.RepositoryIds,
                accessScope),
            new ChatAdvisoryContextSnapshot([], string.Empty, [], 0, new string('0', 64)),
            currentDocuments,
            [new AuthorizedChatSection(
                firstAuthorizedDocument.DocumentVersionId,
                stableSectionId,
                1,
                DocumentSectionKind.Overview,
                "主证据",
                "主证据内容。",
                [sourceAnchorId],
                [],
                "src/Primary.cs",
                "[]",
                "[]",
                "[]",
                "主证据",
                "主仓库")],
            [new AuthorizedChatSourceAnchor(
                sourceAnchorId,
                firstAuthorizedDocument.DocumentVersionId,
                firstAuthorizedDocument.RepositoryId,
                firstAuthorizedDocument.ScanRunId,
                firstAuthorizedDocument.ProfiledRevision,
                "src/Primary.cs",
                1,
                1,
                new string('b', 64))],
            ChatConversationState.Empty(ChatConversationScope.Create(
                mode,
                ExactCrossRepositoryTestValues.SystemId,
                fixture.RepositoryIds)));
        ChatRetrievalStageResult<ChatRelationshipExpansionProjection> result =
            await ChatRelationshipProjectorFactory.Create(fixture.Context).ProjectAsync(
                snapshot,
                primary,
                CancellationToken.None);
        Assert.Equal(ChatRetrievalStageResultKind.Succeeded, result.Kind);
        ChatRelationshipExpansionProjection expansion = Assert.IsType<ChatRelationshipExpansionProjection>(
            result.Value);
        Dictionary<string, Guid> documentVersionIdsByHandle = primary.PrimaryDocuments
            .Concat(expansion.AddedDocuments)
            .ToDictionary(
                document => document.DocumentHandle,
                document => document.DocumentVersionId,
                StringComparer.Ordinal);
        return new TypedRelationshipProjection(expansion, documentVersionIdsByHandle);
    }

    private sealed record TypedRelationshipProjection(
        ChatRelationshipExpansionProjection Expansion,
        IReadOnlyDictionary<string, Guid> DocumentVersionIdsByHandle);

    private static ChatEvidenceRetriever CreateRetriever(
        VulgataDbContext context,
        IPreparedChatScopeResolver? preparedScopeResolver = null) =>
        new(
            context,
            preparedScopeResolver,
            new CanonicalEmptyAdvisoryContextReader(),
            new ChatLexicalEvidenceProjector(),
            new ChatRelationshipEvidenceProjector(context),
            new ChatEvidencePacketBuilder(),
            logger: null);

    private static ChatEvidenceRetriever CreateRetrieverWithAdvisoryReader(
        VulgataDbContext context,
        IChatAdvisoryContextReader advisoryContextReader)
    {
        ConstructorInfo constructor = Assert.Single(typeof(ChatEvidenceRetriever).GetConstructors());
        Assert.Contains(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IChatAdvisoryContextReader));
        object?[] arguments = constructor.GetParameters()
            .Select(parameter => (object?)(parameter.ParameterType switch
            {
                Type type when type == typeof(VulgataDbContext) => context,
                Type type when type == typeof(IPreparedChatScopeResolver) =>
                    new ProjectionOnlyScopeResolver(),
                Type type when type == typeof(IChatAdvisoryContextReader) => advisoryContextReader,
                Type type when type == typeof(IChatLexicalEvidenceProjector) =>
                    new ChatLexicalEvidenceProjector(),
                Type type when type == typeof(IChatRelationshipProjector) =>
                    new ChatRelationshipEvidenceProjector(context),
                Type type when type == typeof(ChatEvidencePacketBuilder) =>
                    new ChatEvidencePacketBuilder(),
                _ when parameter.Name == "logger" => null,
                _ => throw new InvalidOperationException(
                    $"Unsupported ChatEvidenceRetriever dependency: {parameter.ParameterType}.")
            }))
            .ToArray();
        return Assert.IsType<ChatEvidenceRetriever>(constructor.Invoke(arguments));
    }

    private static ChatAdvisoryContextSnapshot CreateAdvisorySnapshot(
        string composedText,
        string fingerprintSeed) =>
        new(
            [new ChatAdvisoryContextSourceUnit(
                ChatAdvisoryContextSourceKind.System,
                ExactCrossRepositoryTestValues.SystemId,
                ExactCrossRepositoryTestValues.Pass1CompletedAt,
                composedText)],
            composedText,
            [composedText],
            Encoding.UTF8.GetByteCount(composedText),
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSeed))));

    private sealed class TransactionObservingAdvisoryContextReader(
        VulgataDbContext context,
        Func<int> authorizationCallCount,
        ChatAdvisoryContextReadResult firstResult,
        ChatAdvisoryContextReadResult afterReadMutation) : IChatAdvisoryContextReader
    {
        private ChatAdvisoryContextReadResult _nextResult = firstResult;

        internal int CallCount { get; private set; }
        internal int? AuthorizationCallCountAtRead { get; private set; }
        internal IsolationLevel? IsolationLevelAtRead { get; private set; }

        public Task<ChatAdvisoryContextReadResult> ReadAsync(
            AuthorizedChatScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            AuthorizationCallCountAtRead = authorizationCallCount();
            IsolationLevelAtRead = context.Database.CurrentTransaction?
                .GetDbTransaction().IsolationLevel;
            ChatAdvisoryContextReadResult result = _nextResult;
            _nextResult = afterReadMutation;
            return Task.FromResult(result);
        }
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

    private sealed class ProjectionOnlyScopeResolver : IPreparedChatScopeResolver
    {
        public Task<PreparedChatScopeBinding?> ResolveAsync(
            Guid systemId,
            IReadOnlyCollection<Guid> repositoryIds,
            RepositoryAccessScope accessScope,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PreparedChatScopeBinding?>(new PreparedChatScopeBinding(
                systemId,
                repositoryIds.Order().First(),
                repositoryIds.Order().Skip(1).First()));
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
        private static readonly Guid _duplicateOneHopEdgeId = Id(60_017);
        private static readonly Guid _optionalSecondHopEdgeId = Id(60_018);
        private static readonly Guid _relatedFlowGroupId = Id(60_031);
        private static readonly Guid _relatedFlowTraceId = Id(60_032);
        private static readonly Guid _relatedFlowStepId = Id(60_033);
        private static readonly Guid _secondRelatedFlowGroupId = Id(60_034);
        private static readonly Guid _secondRelatedFlowTraceId = Id(60_035);
        private static readonly Guid _secondRelatedFlowStepId = Id(60_036);
        private static readonly Guid _staleUnrelatedEdgeId = Id(60_041);
        private static readonly Guid _emptyFlowGroupId = Id(60_042);

        internal VulgataDbContext Context { get; } = context;

        internal Guid ReferencesEdgeId { get; } = referencesEdgeId;

        internal Guid GeneratedFromEdgeId => _generatedFromEdgeId;

        internal Guid OneHopEdgeId => _oneHopEdgeId;

        internal Guid OneHopDocumentVersionId => _oneHopDocumentVersionId;

        internal Guid SecondHopDocumentVersionId => _secondHopDocumentVersionId;

        internal Guid BusinessDocumentVersionId => _businessDocumentVersionId;

        internal Guid SecondHopEdgeId => _secondHopEdgeId;

        internal async Task AddOptionalProviderSecondHopReferenceAsync()
        {
            Context.DocumentEdges.Add(new DocumentEdge(
                _optionalSecondHopEdgeId,
                ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                _secondHopDocumentVersionId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                DocumentEdgeType.References,
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        internal Guid RelatedFlowGroupId => _relatedFlowGroupId;

        internal Guid BaseFlowGroupId => Id(10_301);

        internal Guid SecondRelatedFlowGroupId => _secondRelatedFlowGroupId;

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
            ChatConversationState? conversation = null,
            string question = "客户会话如何跨仓库更新？",
            ChatMode mode = ChatMode.Business,
            ChatAdvisoryContextSnapshot? advisoryContext = null)
        {
            ChatQuestionRequest questionRequest = ChatQuestionRequest.Create(
                requestId,
                mode,
                ExactCrossRepositoryTestValues.SystemId,
                RepositoryIds,
                question);
            ChatConversationScope conversationScope = ChatConversationScope.Create(
                mode,
                questionRequest.SystemId,
                questionRequest.RepositoryIds);
            return ChatRetrievalRequest.Create(
                questionRequest,
                conversation ?? ChatConversationState.Empty(conversationScope),
                DateTimeOffset.UtcNow.AddMinutes(1),
                advisoryContext);
        }

        internal async Task RemoveOptionalCrossRepositorySupportAsync()
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"DocumentEdges\" WHERE \"Id\" = {_generatedFromEdgeId}");
            await RemoveBaseFlowGroupAuthorityAsync();
        }

        internal async Task RemoveBaseFlowGroupAuthorityAsync()
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"FlowTraceSteps\" WHERE \"FlowTraceId\" IN (SELECT \"Id\" FROM \"FlowTraces\" WHERE \"FlowGroupId\" = {Id(10_301)})");
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"FlowTraces\" WHERE \"FlowGroupId\" = {Id(10_301)}");
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"FlowGroups\" WHERE \"Id\" = {Id(10_301)}");
            Context.ChangeTracker.Clear();
        }

        internal async Task AddDuplicateOneHopTargetAsync()
        {
            Context.DocumentEdges.Add(new DocumentEdge(
                _duplicateOneHopEdgeId,
                ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                _oneHopDocumentVersionId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                DocumentEdgeType.GeneratedFrom,
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        internal async Task AddPublishedOneHopFlowGroupAsync(
            Guid? exactResolutionEdgeId = null,
            Guid? exactResolutionDecisionId = null)
        {
            var flowGroup = new FlowGroup(
                _relatedFlowGroupId,
                _businessDocumentVersionId,
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                ExactCrossRepositoryTestValues.ConsumerScanRunId,
                2,
                "会话调用扩展流程",
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            Context.Entry(flowGroup).Property(nameof(FlowGroup.SchemaIdentity)).CurrentValue =
                "endpoint-flow-group/1.0";
            Context.Entry(flowGroup).Property(nameof(FlowGroup.State)).CurrentValue = "Published";
            Context.Entry(flowGroup).Property(nameof(FlowGroup.SystemId)).CurrentValue =
                ExactCrossRepositoryTestValues.SystemId;
            Context.Entry(flowGroup).Property(nameof(FlowGroup.PlanFingerprint)).CurrentValue =
                new string('c', 64);
            Context.Entry(flowGroup).Property(nameof(FlowGroup.MembershipFingerprint)).CurrentValue =
                new string('d', 64);
            Context.Entry(flowGroup).Property(nameof(FlowGroup.ExactResolutionEdgeId)).CurrentValue =
                exactResolutionEdgeId ?? _oneHopEdgeId;
            if (exactResolutionDecisionId is Guid decisionId)
            {
                Context.Entry(flowGroup).Property(nameof(FlowGroup.ExactResolutionDecisionId)).CurrentValue =
                    decisionId;
            }
            var trace = new FlowTrace(
                _relatedFlowTraceId,
                _relatedFlowGroupId,
                1,
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            trace.Steps.Add(new FlowTraceStep(
                _relatedFlowStepId,
                _relatedFlowTraceId,
                1,
                _oneHopDocumentVersionId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                exactResolutionEdgeId ?? _oneHopEdgeId,
                FlowTraceStepKind.Relationship,
                "通过同仓库 REFERENCES Edge 调用扩展。",
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            flowGroup.Traces.Add(trace);
            Context.FlowGroups.Add(flowGroup);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        internal async Task AddSecondPublishedOneHopFlowGroupAsync(
            Guid? exactResolutionEdgeId = null,
            Guid? exactResolutionDecisionId = null,
            Guid? flowStepDocumentVersionId = null)
        {
            var flowGroup = new FlowGroup(
                _secondRelatedFlowGroupId,
                _businessDocumentVersionId,
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                ExactCrossRepositoryTestValues.ConsumerScanRunId,
                3,
                "会话调用扩展补充流程",
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            Context.Entry(flowGroup).Property(nameof(FlowGroup.SchemaIdentity)).CurrentValue =
                "endpoint-flow-group/1.0";
            Context.Entry(flowGroup).Property(nameof(FlowGroup.State)).CurrentValue = "Published";
            Context.Entry(flowGroup).Property(nameof(FlowGroup.SystemId)).CurrentValue =
                ExactCrossRepositoryTestValues.SystemId;
            Context.Entry(flowGroup).Property(nameof(FlowGroup.PlanFingerprint)).CurrentValue =
                new string('e', 64);
            Context.Entry(flowGroup).Property(nameof(FlowGroup.MembershipFingerprint)).CurrentValue =
                new string('f', 64);
            Context.Entry(flowGroup).Property(nameof(FlowGroup.ExactResolutionEdgeId)).CurrentValue =
                exactResolutionEdgeId ?? _oneHopEdgeId;
            if (exactResolutionDecisionId is Guid decisionId)
            {
                Context.Entry(flowGroup).Property(nameof(FlowGroup.ExactResolutionDecisionId)).CurrentValue =
                    decisionId;
            }
            var trace = new FlowTrace(
                _secondRelatedFlowTraceId,
                _secondRelatedFlowGroupId,
                1,
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            trace.Steps.Add(new FlowTraceStep(
                _secondRelatedFlowStepId,
                _secondRelatedFlowTraceId,
                1,
                flowStepDocumentVersionId ?? _oneHopDocumentVersionId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                exactResolutionEdgeId ?? _oneHopEdgeId,
                FlowTraceStepKind.Relationship,
                "通过同仓库 REFERENCES Edge 调用补充流程。",
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            flowGroup.Traces.Add(trace);
            Context.FlowGroups.Add(flowGroup);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        internal async Task AddEmptyPublishedOneHopFlowGroupAsync()
        {
            var flowGroup = new FlowGroup(
                _emptyFlowGroupId,
                _oneHopDocumentVersionId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                4,
                "会话调用扩展空流程",
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            Context.Entry(flowGroup).Property(nameof(FlowGroup.SchemaIdentity)).CurrentValue =
                "endpoint-flow-group/1.0";
            Context.Entry(flowGroup).Property(nameof(FlowGroup.State)).CurrentValue = "Published";
            Context.Entry(flowGroup).Property(nameof(FlowGroup.SystemId)).CurrentValue =
                ExactCrossRepositoryTestValues.SystemId;
            Context.Entry(flowGroup).Property(nameof(FlowGroup.PlanFingerprint)).CurrentValue =
                new string('1', 64);
            Context.Entry(flowGroup).Property(nameof(FlowGroup.MembershipFingerprint)).CurrentValue =
                new string('2', 64);
            Context.Entry(flowGroup).Property(nameof(FlowGroup.ExactResolutionEdgeId)).CurrentValue =
                _oneHopEdgeId;
            Context.FlowGroups.Add(flowGroup);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        internal async Task AddStaleUnrelatedCrossRepositoryEdgeAsync()
        {
            Context.DocumentEdges.Add(new DocumentEdge(
                _staleUnrelatedEdgeId,
                ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                ExactCrossRepositoryTestValues.ConsumerScanRunId,
                _oneHopDocumentVersionId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                DocumentEdgeType.References,
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            await Context.SaveChangesAsync();
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = NULL WHERE \"Id\" = {_oneHopLogicalDocumentId}");
            Context.ChangeTracker.Clear();
        }

        internal async Task AddTechnicalAndEvidenceSectionsAsync()
        {
            await AddModeSpecificDocumentAsync(
                Id(62_001),
                Id(62_002),
                Id(62_003),
                Id(62_004),
                Id(62_005),
                Id(62_006),
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                ExactCrossRepositoryTestValues.ProviderCodeUnitId,
                DocumentSectionKind.TechnicalDetail,
                "技术实现细节",
                "TechnicalBoundaryToken 技术实现细节。");
            await AddModeSpecificDocumentAsync(
                Id(62_011),
                Id(62_012),
                Id(62_013),
                Id(62_014),
                Id(62_015),
                Id(62_016),
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                ExactCrossRepositoryTestValues.ConsumerScanRunId,
                ExactCrossRepositoryTestValues.ConsumerCodeUnitId,
                DocumentSectionKind.Evidence,
                "技术证据",
                "TechnicalBoundaryToken 技术证据。");
            Context.ChangeTracker.Clear();
        }

        internal async Task AddBusinessSectionsAsync(int additionalCount)
        {
            DocumentVersion version = await Context.DocumentVersions
                .SingleAsync(value => value.Id == _businessDocumentVersionId);
            for (int index = 0; index < additionalCount; index++)
            {
                Context.DocumentSections.Add(Section(
                    Id(70_000 + index),
                    _businessLogicalDocumentId,
                    version,
                    Id(71_000 + index),
                    $"客户会话补充证据 {index + 1}",
                    $"客户会话更新边界补充证据 {index + 1}。",
                    _businessSourceAnchorId,
                    [ExactCrossRepositoryTestValues.ConsumerDocumentVersionId],
                    index + 2));
            }

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        internal async Task<Guid> AddLateHighestRankedBusinessSectionAsync(int decoyCount)
        {
            await AddBusinessSectionsAsync(decoyCount);
            DocumentVersion version = await Context.DocumentVersions
                .SingleAsync(value => value.Id == _businessDocumentVersionId);
            Guid stableSectionId = Id(99_001);
            Context.DocumentSections.Add(Section(
                Id(99_000),
                _businessLogicalDocumentId,
                version,
                stableSectionId,
                "WindowPriorityToken",
                "WindowPriorityToken",
                _businessSourceAnchorId,
                [],
                decoyCount + 2));
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            return stableSectionId;
        }

        internal async Task AddFourScoredSectionsPerDocumentAsync()
        {
            (Guid VersionId, Guid LogicalDocumentId, Guid AnchorId)[] documents =
            [
                (_businessDocumentVersionId, _businessLogicalDocumentId, _businessSourceAnchorId),
                (ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
                    ExactCrossRepositoryTestValues.ConsumerLogicalDocumentId,
                    ExactCrossRepositoryTestValues.ConsumerSourceAnchorId),
                (ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
                    ExactCrossRepositoryTestValues.ProviderLogicalDocumentId,
                    ExactCrossRepositoryTestValues.ProviderSourceAnchorId),
            ];

            int sequence = 0;
            foreach ((Guid versionId, Guid logicalDocumentId, Guid anchorId) in documents)
            {
                DocumentVersion version = await Context.DocumentVersions
                    .SingleAsync(value => value.Id == versionId);
                for (int sectionOrder = 2; sectionOrder <= 4; sectionOrder++)
                {
                    Context.DocumentSections.Add(Section(
                        Id(74_000 + sequence),
                        logicalDocumentId,
                        version,
                        Id(75_000 + sequence),
                        $"客户会话更新补充段落 {sequence + 1}",
                        $"客户会话更新的确定性检索证据 {sequence + 1}。",
                        anchorId,
                        [],
                        sectionOrder));
                    sequence++;
                }
            }

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        private async Task AddModeSpecificDocumentAsync(
            Guid logicalDocumentId,
            Guid documentVersionId,
            Guid sourceAnchorId,
            Guid sectionId,
            Guid stableSectionId,
            Guid indexEntryId,
            Guid repositoryId,
            Guid scanRunId,
            Guid codeUnitId,
            DocumentSectionKind sectionKind,
            string heading,
            string content)
        {
            string profiledRevision = await CurrentProfiledRevisionAsync(scanRunId);
            LogicalDocument logicalDocument = LogicalDocument.Create(
                logicalDocumentId,
                repositoryId,
                DocumentType.CodeLogic,
                $"prepared/{heading}",
                ExactCrossRepositoryTestValues.CreatedAt);
            Context.LogicalDocuments.Add(logicalDocument);
            await Context.SaveChangesAsync();

            DocumentVersion version = DocumentVersion.Create(
                documentVersionId,
                logicalDocumentId,
                repositoryId,
                ExactCrossRepositoryTestValues.SystemId,
                scanRunId,
                profiledRevision,
                1,
                heading,
                $"src/{heading}.java",
                KnowledgeDocumentLimits.SchemaIdentity,
                "{}",
                content,
                ExactCrossRepositoryTestValues.ContentFingerprint,
                ExactCrossRepositoryTestValues.CommitFingerprint,
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            version.SourceAnchors.Add(new SourceAnchor(
                sourceAnchorId,
                documentVersionId,
                repositoryId,
                scanRunId,
                codeUnitId,
                version.ProfiledRevision,
                $"src/{heading}.java",
                $"{heading}.java#line=1;rule=mode-specific",
                1,
                3,
                ExactCrossRepositoryTestValues.SourceHash,
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            Context.DocumentVersions.Add(version);
            Context.DocumentSections.Add(Section(
                sectionId,
                logicalDocumentId,
                version,
                stableSectionId,
                heading,
                content,
                sourceAnchorId,
                [],
                sectionKind: sectionKind));
            Context.IndexEntries.Add(IndexEntry.Create(
                indexEntryId,
                logicalDocumentId,
                documentVersionId,
                repositoryId,
                ExactCrossRepositoryTestValues.SystemId,
                DocumentType.CodeLogic,
                heading,
                content,
                version.RepositoryRelativePath,
                "[]",
                "[]",
                "[\"TechnicalBoundaryToken\"]",
                "TechnicalBoundaryToken",
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            await Context.SaveChangesAsync();
            await SetCurrentVersionAsync(Context, logicalDocumentId, documentVersionId);
        }

        internal async Task AddStandaloneCurrentCodeLogicAsync()
        {
            string providerProfiledRevision = await CurrentProfiledRevisionAsync(
                ExactCrossRepositoryTestValues.ProviderScanRunId);
            string consumerProfiledRevision = await CurrentProfiledRevisionAsync(
                ExactCrossRepositoryTestValues.ConsumerScanRunId);
            Guid logicalDocumentId = Id(61_001);
            Guid documentVersionId = Id(61_002);
            Guid sourceAnchorId = Id(61_003);
            LogicalDocument logicalDocument = LogicalDocument.Create(
                logicalDocumentId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                DocumentType.CodeLogic,
                "prepared/special-session-anchor",
                ExactCrossRepositoryTestValues.CreatedAt);
            Context.LogicalDocuments.Add(logicalDocument);
            await Context.SaveChangesAsync();

            DocumentVersion version = DocumentVersion.Create(
                documentVersionId,
                logicalDocumentId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.SystemId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                providerProfiledRevision,
                1,
                "特殊会话锚点",
                "src/main/java/provider/SpecialSessionAnchor.java",
                KnowledgeDocumentLimits.SchemaIdentity,
                "{}",
                "特殊会话锚点处理规则。",
                ExactCrossRepositoryTestValues.ContentFingerprint,
                ExactCrossRepositoryTestValues.CommitFingerprint,
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            version.SourceAnchors.Add(new SourceAnchor(
                sourceAnchorId,
                documentVersionId,
                version.RepositoryId,
                version.ScanRunId,
                ExactCrossRepositoryTestValues.ProviderCodeUnitId,
                version.ProfiledRevision,
                "src/main/java/provider/SpecialSessionAnchor.java",
                "SpecialSessionAnchor.java#line=1;rule=conversation",
                1,
                3,
                ExactCrossRepositoryTestValues.SourceHash,
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            Context.DocumentVersions.Add(version);
            Context.DocumentSections.Add(Section(
                Id(61_004),
                logicalDocumentId,
                version,
                Id(61_005),
                "特殊会话锚点说明",
                "特殊会话锚点处理规则。",
                sourceAnchorId,
                []));
            Context.IndexEntries.Add(IndexEntry.Create(
                Id(61_006),
                logicalDocumentId,
                documentVersionId,
                version.RepositoryId,
                ExactCrossRepositoryTestValues.SystemId,
                DocumentType.CodeLogic,
                version.Title,
                "特殊会话锚点处理规则。",
                version.RepositoryRelativePath,
                "[\"SpecialSessionAnchor\"]",
                "[]",
                "[\"POST /special-session-anchor\",\"technical-session-term\"]",
                "SpecialSessionAnchor POST /special-session-anchor technical-session-term",
                ExactCrossRepositoryTestValues.Pass1CompletedAt));

            Guid proseLogicalDocumentId = Id(61_011);
            Guid proseDocumentVersionId = Id(61_012);
            Guid proseSourceAnchorId = Id(61_013);
            LogicalDocument proseLogicalDocument = LogicalDocument.Create(
                proseLogicalDocumentId,
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                DocumentType.CodeLogic,
                "prepared/prose-special-session-anchor",
                ExactCrossRepositoryTestValues.CreatedAt);
            Context.LogicalDocuments.Add(proseLogicalDocument);
            await Context.SaveChangesAsync();
            DocumentVersion proseVersion = DocumentVersion.Create(
                proseDocumentVersionId,
                proseLogicalDocumentId,
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                ExactCrossRepositoryTestValues.SystemId,
                ExactCrossRepositoryTestValues.ConsumerScanRunId,
                consumerProfiledRevision,
                1,
                "普通说明",
                "src/main/java/customer/ProseSpecialSessionAnchor.java",
                KnowledgeDocumentLimits.SchemaIdentity,
                "{}",
                "SpecialSessionAnchor 仅作为普通说明文本出现。",
                ExactCrossRepositoryTestValues.ContentFingerprint,
                ExactCrossRepositoryTestValues.CommitFingerprint,
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            proseVersion.SourceAnchors.Add(new SourceAnchor(
                proseSourceAnchorId,
                proseDocumentVersionId,
                proseVersion.RepositoryId,
                proseVersion.ScanRunId,
                ExactCrossRepositoryTestValues.ConsumerCodeUnitId,
                proseVersion.ProfiledRevision,
                "src/main/java/customer/ProseSpecialSessionAnchor.java",
                "ProseSpecialSessionAnchor.java#line=1;rule=prose",
                1,
                3,
                ExactCrossRepositoryTestValues.SourceHash,
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            Context.DocumentVersions.Add(proseVersion);
            Context.DocumentSections.Add(Section(
                Id(61_014),
                proseLogicalDocumentId,
                proseVersion,
                Id(61_015),
                "普通说明",
                "SpecialSessionAnchor 仅作为普通说明文本出现。",
                proseSourceAnchorId,
                []));
            await Context.SaveChangesAsync();
            await SetCurrentVersionAsync(Context, logicalDocumentId, documentVersionId);
            await SetCurrentVersionAsync(Context, proseLogicalDocumentId, proseDocumentVersionId);
            Context.ChangeTracker.Clear();
        }

        internal async Task RemoveStandaloneCurrentCodeLogicEvidenceAsync()
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"IndexEntries\" WHERE \"DocumentVersionId\" = {Id(61_002)}");
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"DocumentSections\" WHERE \"DocumentVersionId\" = {Id(61_002)}");
            Context.ChangeTracker.Clear();
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

        internal async Task SetConsumerScanRunStatusAsync(string status)
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"ScanRuns\" SET \"Status\" = {status} WHERE \"Id\" = {ExactCrossRepositoryTestValues.ConsumerScanRunId}");
            Context.ChangeTracker.Clear();
        }

        private async Task<string> CurrentProfiledRevisionAsync(Guid scanRunId) =>
            await Context.ScanRuns
                .AsNoTracking()
                .Where(scanRun => scanRun.Id == scanRunId)
                .Select(scanRun => scanRun.ProfiledRevision)
                .SingleAsync() ?? throw new InvalidOperationException("Fixture Scan Run has no Profiled Revision.");

        internal async Task AddCurrentOneHopReferenceAsync()
        {
            string providerProfiledRevision = await CurrentProfiledRevisionAsync(
                ExactCrossRepositoryTestValues.ProviderScanRunId);
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
                providerProfiledRevision,
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
                providerProfiledRevision,
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

        internal async Task<Guid> AddCandidatesBeyondLegacyQueryCapAsync()
        {
            const int legacyQueryCap = 97;
            DocumentVersion consumerSource = await Context.DocumentVersions
                .AsNoTracking()
                .SingleAsync(version =>
                    version.Id == ExactCrossRepositoryTestValues.ConsumerDocumentVersionId);
            DocumentVersion providerSource = await Context.DocumentVersions
                .AsNoTracking()
                .SingleAsync(version =>
                    version.Id == ExactCrossRepositoryTestValues.ProviderDocumentVersionId);

            for (int index = 0; index <= legacyQueryCap; index++)
            {
                bool isExpectedWinner = index == legacyQueryCap;
                DocumentVersion source = isExpectedWinner ? providerSource : consumerSource;
                Guid codeUnitId = isExpectedWinner
                    ? ExactCrossRepositoryTestValues.ProviderCodeUnitId
                    : ExactCrossRepositoryTestValues.ConsumerCodeUnitId;
                int idBase = 80_000 + (index * 10);
                Guid logicalDocumentId = Id(idBase);
                Guid documentVersionId = Id(idBase + 1);
                Guid sourceAnchorId = Id(idBase + 2);
                Guid sectionId = Id(idBase + 3);
                Guid stableSectionId = Id(idBase + 4);
                Guid edgeId = Id(idBase + 5);
                string name = isExpectedWinner ? "higher-seed-winner" : $"lower-seed-{index:D3}";

                Context.LogicalDocuments.Add(LogicalDocument.Create(
                    logicalDocumentId,
                    source.RepositoryId,
                    DocumentType.CodeLogic,
                    $"prepared/t068-{name}",
                    ExactCrossRepositoryTestValues.CreatedAt));
                DocumentVersion target = DocumentVersion.Create(
                    documentVersionId,
                    logicalDocumentId,
                    source.RepositoryId,
                    ExactCrossRepositoryTestValues.SystemId,
                    source.ScanRunId,
                    source.ProfiledRevision,
                    1,
                    $"T068 {name}",
                    $"src/t068/{name}.java",
                    KnowledgeDocumentLimits.SchemaIdentity,
                    "{}",
                    $"T068 relationship candidate {name}.",
                    ExactCrossRepositoryTestValues.ContentFingerprint,
                    ExactCrossRepositoryTestValues.CommitFingerprint,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt);
                target.SourceAnchors.Add(new SourceAnchor(
                    sourceAnchorId,
                    target.Id,
                    target.RepositoryId,
                    target.ScanRunId,
                    codeUnitId,
                    target.ProfiledRevision,
                    $"src/t068/{name}.java",
                    $"{name}.java#line=1;rule=t068",
                    1,
                    2,
                    ExactCrossRepositoryTestValues.SourceHash,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));
                Context.DocumentVersions.Add(target);
                Context.DocumentSections.Add(Section(
                    sectionId,
                    logicalDocumentId,
                    target,
                    stableSectionId,
                    $"T068 {name}",
                    $"T068 relationship candidate {name}.",
                    sourceAnchorId,
                    [source.Id]));
                Context.DocumentEdges.Add(new DocumentEdge(
                    edgeId,
                    source.Id,
                    source.RepositoryId,
                    source.ScanRunId,
                    target.Id,
                    target.RepositoryId,
                    target.ScanRunId,
                    DocumentEdgeType.References,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));
            }

            await Context.SaveChangesAsync();
            for (int index = 0; index <= legacyQueryCap; index++)
            {
                int idBase = 80_000 + (index * 10);
                await SetCurrentVersionAsync(Context, Id(idBase), Id(idBase + 1));
            }

            Context.ChangeTracker.Clear();
            return Id(80_000 + (legacyQueryCap * 10) + 5);
        }

        internal async Task<(Guid GeneratedFromEdgeId, Guid ReferencesEdgeId)>
            AddModePreferenceCandidatesAsync()
        {
            DocumentVersion businessSource = await Context.DocumentVersions
                .AsNoTracking()
                .SingleAsync(version => version.Id == _businessDocumentVersionId);
            Guid generatedFromEdgeId = Id(90_005);
            Guid referencesEdgeId = Id(90_015);

            foreach ((int idBase, string name, DocumentEdgeType edgeType) in new[]
            {
                (90_000, "business-generated", DocumentEdgeType.GeneratedFrom),
                (90_010, "it-reference", DocumentEdgeType.References),
                (90_020, "business-generated-secondary", DocumentEdgeType.GeneratedFrom),
            })
            {
                Guid logicalDocumentId = Id(idBase);
                Guid documentVersionId = Id(idBase + 1);
                Guid sourceAnchorId = Id(idBase + 2);
                Context.LogicalDocuments.Add(LogicalDocument.Create(
                    logicalDocumentId,
                    businessSource.RepositoryId,
                    DocumentType.CodeLogic,
                    $"prepared/t068-{name}",
                    ExactCrossRepositoryTestValues.CreatedAt));
                DocumentVersion target = DocumentVersion.Create(
                    documentVersionId,
                    logicalDocumentId,
                    businessSource.RepositoryId,
                    ExactCrossRepositoryTestValues.SystemId,
                    businessSource.ScanRunId,
                    businessSource.ProfiledRevision,
                    1,
                    $"T068 {name}",
                    $"src/t068/{name}.java",
                    KnowledgeDocumentLimits.SchemaIdentity,
                    "{}",
                    $"T068 mode preference candidate {name}.",
                    ExactCrossRepositoryTestValues.ContentFingerprint,
                    ExactCrossRepositoryTestValues.CommitFingerprint,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt);
                target.SourceAnchors.Add(new SourceAnchor(
                    sourceAnchorId,
                    target.Id,
                    target.RepositoryId,
                    target.ScanRunId,
                    ExactCrossRepositoryTestValues.ConsumerCodeUnitId,
                    target.ProfiledRevision,
                    $"src/t068/{name}.java",
                    $"{name}.java#line=1;rule=t068-mode",
                    1,
                    2,
                    ExactCrossRepositoryTestValues.SourceHash,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));
                Context.DocumentVersions.Add(target);
                Context.DocumentSections.Add(Section(
                    Id(idBase + 3),
                    logicalDocumentId,
                    target,
                    Id(idBase + 4),
                    $"T068 {name}",
                    $"T068 mode preference candidate {name}.",
                    sourceAnchorId,
                    [businessSource.Id]));
                Context.DocumentEdges.Add(new DocumentEdge(
                    Id(idBase + 5),
                    businessSource.Id,
                    businessSource.RepositoryId,
                    businessSource.ScanRunId,
                    target.Id,
                    target.RepositoryId,
                    target.ScanRunId,
                    edgeType,
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));
            }

            await Context.SaveChangesAsync();
            await SetCurrentVersionAsync(Context, Id(90_000), Id(90_001));
            await SetCurrentVersionAsync(Context, Id(90_010), Id(90_011));
            await SetCurrentVersionAsync(Context, Id(90_020), Id(90_021));
            Context.ChangeTracker.Clear();
            return (generatedFromEdgeId, referencesEdgeId);
        }

        internal async Task<(Guid EdgeId, Guid DecisionId)> AddSecondAcceptedV2ReferenceAsync()
        {
            const string normalizedEndpoint = "cust/updateSession-secondary";
            const string occurrenceId = "secondary-update-session";
            Guid edgeId = Id(60_021);
            Guid candidateId = Id(60_022);

            ExactCrossRepositoryEdgeEvidence templateEvidence = await Context
                .ExactCrossRepositoryEdgeEvidence
                .AsNoTracking()
                .SingleAsync(value => value.EdgeId == ReferencesEdgeId);
            CrossRepositoryResolutionDecision templateDecision = await Context
                .CrossRepositoryResolutionDecisions
                .AsNoTracking()
                .SingleAsync(value => value.DocumentEdgeId == ReferencesEdgeId);
            ExactResolutionProviderEvidenceContribution templateContribution = await Context
                .ExactResolutionProviderEvidenceContributions
                .AsNoTracking()
                .SingleAsync(value => value.ResolutionDecisionId == templateDecision.Id);
            SourceAnchor oneHopAnchor = await Context.SourceAnchors
                .AsNoTracking()
                .SingleAsync(anchor => anchor.Id == _oneHopSourceAnchorId);
            string oneHopRevision = await Context.ScanRuns
                .AsNoTracking()
                .Where(scanRun => scanRun.Id == ExactCrossRepositoryTestValues.ProviderScanRunId)
                .Select(scanRun => scanRun.ProfiledRevision)
                .SingleAsync() ?? throw new InvalidOperationException("Fixture provider revision is missing.");

            Context.DocumentEdges.Add(new DocumentEdge(
                edgeId,
                ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                ExactCrossRepositoryTestValues.ConsumerScanRunId,
                _oneHopDocumentVersionId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                DocumentEdgeType.References,
                ExactCrossRepositoryTestValues.Pass1CompletedAt));
            var evidence = new ExactCrossRepositoryEdgeEvidence(
                edgeId,
                templateEvidence.ConsumerSourceAnchorId,
                templateEvidence.ConsumerDocumentVersionId,
                templateEvidence.ConsumerRepositoryId,
                templateEvidence.ConsumerScanRunId,
                oneHopAnchor.Id,
                _oneHopDocumentVersionId,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                templateEvidence.ConsumerRawIdentifier,
                templateEvidence.ProviderRawIdentifier,
                normalizedEndpoint,
                templateEvidence.Protocol,
                templateEvidence.ResolutionKind,
                templateEvidence.Confidence,
                templateEvidence.ProfileSchemaVersion,
                templateEvidence.ProfileId,
                templateEvidence.ProfileVersion,
                templateEvidence.ProfileFingerprint,
                occurrenceId,
                templateEvidence.ConsumerEvidenceLocator,
                oneHopAnchor.EvidenceLocator,
                string.Empty,
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            string evidenceFingerprint = ExactResolutionCanonicalAuthority.EvidenceFingerprint(
                evidence,
                templateDecision.ConsumerProfiledRevision,
                oneHopRevision);
            SetPrivate(evidence, nameof(ExactCrossRepositoryEdgeEvidence.EvidenceFingerprint), evidenceFingerprint);
            Context.ExactCrossRepositoryEdgeEvidence.Add(evidence);

            ExactResolutionProviderEvidenceContribution contribution = NewPrivate<ExactResolutionProviderEvidenceContribution>();
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ResolutionDecisionId),
                ExactResolutionCanonicalAuthority.DecisionId(evidenceFingerprint));
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderIdentity),
                ExactResolutionCanonicalAuthority.ProviderIdentity(
                    ExactCrossRepositoryTestValues.SystemId,
                    ExactCrossRepositoryTestValues.ProviderRepositoryId,
                    ExactCrossRepositoryTestValues.ProviderScanRunId,
                    oneHopRevision,
                    _oneHopDocumentVersionId));
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderDocumentVersionId), _oneHopDocumentVersionId);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.RepositoryId), templateContribution.RepositoryId);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ScanRunId), templateContribution.ScanRunId);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProfiledRevision), oneHopRevision);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.CommunicationClassificationId), templateContribution.CommunicationClassificationId);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.SourceAnchorId), oneHopAnchor.Id);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderRawIdentifier), templateContribution.ProviderRawIdentifier);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.NormalizedEndpointIdentifier), normalizedEndpoint);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceLocator), oneHopAnchor.EvidenceLocator);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ClassificationFingerprint), templateContribution.ClassificationFingerprint);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.CatalogRuleIdentity), templateContribution.CatalogRuleIdentity);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.NormalizationRuleIdentity), templateContribution.NormalizationRuleIdentity);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderOrdinal), 1);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceOrdinal), 1);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.CreatedAt), ExactCrossRepositoryTestValues.Pass1CompletedAt);
            string contributionFingerprint = ExactResolutionCanonicalAuthority.PersistedProviderContributionFingerprint(
                evidenceFingerprint,
                contribution);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceFingerprint), contributionFingerprint);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.Id),
                ExactResolutionCanonicalAuthority.ProviderContributionId(contributionFingerprint));
            Context.ExactResolutionProviderEvidenceContributions.Add(contribution);

            CrossRepositoryResolutionDecision decision = NewPrivate<CrossRepositoryResolutionDecision>();
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.Id),
                ExactResolutionCanonicalAuthority.DecisionId(evidenceFingerprint));
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ResolutionSchemaIdentity), templateDecision.ResolutionSchemaIdentity);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.Pass1PairExecutionId), templateDecision.Pass1PairExecutionId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.CoordinationId), templateDecision.CoordinationId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.SystemId), templateDecision.SystemId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerCandidateId), candidateId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryIdentity), "secondary-entry");
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryStableKey), new string('1', 64));
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerDocumentVersionId), templateDecision.ConsumerDocumentVersionId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerRepositoryId), templateDecision.ConsumerRepositoryId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerScanRunId), templateDecision.ConsumerScanRunId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerProfiledRevision), templateDecision.ConsumerProfiledRevision);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.Protocol), templateDecision.Protocol);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.NormalizedEndpointIdentifier), normalizedEndpoint);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.NormalizationRuleIdentity), templateDecision.NormalizationRuleIdentity);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.Outcome), templateDecision.Outcome);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.OutcomeKind), templateDecision.OutcomeKind);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ReasonCode), templateDecision.ReasonCode);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ProviderIdentity), contribution.ProviderIdentity);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ProviderDocumentVersionId), _oneHopDocumentVersionId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ProviderRepositoryId), templateDecision.ProviderRepositoryId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ProviderScanRunId), templateDecision.ProviderScanRunId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ProviderProfiledRevision), oneHopRevision);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.DocumentEdgeId), edgeId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerEvidenceFingerprint), evidenceFingerprint);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ProviderEvidenceSetFingerprint),
                ExactResolutionCanonicalAuthority.ProviderEvidenceSetFingerprint([contributionFingerprint]));
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.DecisionFingerprint), evidenceFingerprint);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.EvidenceFingerprint), evidenceFingerprint);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.CreatedAt), ExactCrossRepositoryTestValues.Pass1CompletedAt);
            Context.CrossRepositoryResolutionDecisions.Add(decision);

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            return (edgeId, decision.Id);
        }

        internal async Task<Guid> AddAdditionalAcceptedV2DecisionAsync(
            Guid edgeId,
            Guid sourceDecisionId)
        {
            CrossRepositoryResolutionDecision sourceDecision = await Context
                .CrossRepositoryResolutionDecisions
                .AsNoTracking()
                .SingleAsync(decision => decision.Id == sourceDecisionId);
            ExactResolutionProviderEvidenceContribution sourceContribution = await Context
                .ExactResolutionProviderEvidenceContributions
                .AsNoTracking()
                .SingleAsync(contribution => contribution.ResolutionDecisionId == sourceDecisionId);

            string evidenceFingerprint = Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes($"story125-second-flow-group:{edgeId:D}")));
            Guid decisionId = ExactResolutionCanonicalAuthority.DecisionId(evidenceFingerprint);
            const string normalizedEndpoint = "cust/updateSession-secondary-2";
            CrossRepositoryResolutionDecision decision = NewPrivate<CrossRepositoryResolutionDecision>();
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.Id), decisionId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ResolutionSchemaIdentity), sourceDecision.ResolutionSchemaIdentity);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.Pass1PairExecutionId), sourceDecision.Pass1PairExecutionId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.CoordinationId), sourceDecision.CoordinationId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.SystemId), sourceDecision.SystemId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerCandidateId), Id(60_023));
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryIdentity), "secondary-entry-2");
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryStableKey), new string('2', 64));
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerDocumentVersionId), sourceDecision.ConsumerDocumentVersionId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerRepositoryId), sourceDecision.ConsumerRepositoryId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerScanRunId), sourceDecision.ConsumerScanRunId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerProfiledRevision), sourceDecision.ConsumerProfiledRevision);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.Protocol), sourceDecision.Protocol);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.NormalizedEndpointIdentifier), normalizedEndpoint);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.NormalizationRuleIdentity), sourceDecision.NormalizationRuleIdentity);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.Outcome), sourceDecision.Outcome);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.OutcomeKind), sourceDecision.OutcomeKind);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ReasonCode), sourceDecision.ReasonCode);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ProviderIdentity), sourceDecision.ProviderIdentity);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ProviderDocumentVersionId), sourceDecision.ProviderDocumentVersionId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ProviderRepositoryId), sourceDecision.ProviderRepositoryId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ProviderScanRunId), sourceDecision.ProviderScanRunId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ProviderProfiledRevision), sourceDecision.ProviderProfiledRevision);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.DocumentEdgeId), edgeId);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ConsumerEvidenceFingerprint), evidenceFingerprint);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.DecisionFingerprint), evidenceFingerprint);
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.EvidenceFingerprint), evidenceFingerprint);

            ExactResolutionProviderEvidenceContribution contribution = NewPrivate<ExactResolutionProviderEvidenceContribution>();
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ResolutionDecisionId), decisionId);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderIdentity), sourceContribution.ProviderIdentity);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderDocumentVersionId), sourceContribution.ProviderDocumentVersionId);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.RepositoryId), sourceContribution.RepositoryId);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ScanRunId), sourceContribution.ScanRunId);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProfiledRevision), sourceContribution.ProfiledRevision);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.CommunicationClassificationId), sourceContribution.CommunicationClassificationId);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.SourceAnchorId), sourceContribution.SourceAnchorId);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderRawIdentifier), sourceContribution.ProviderRawIdentifier);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.NormalizedEndpointIdentifier), normalizedEndpoint);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceLocator), sourceContribution.EvidenceLocator);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ClassificationFingerprint), sourceContribution.ClassificationFingerprint);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.CatalogRuleIdentity), sourceContribution.CatalogRuleIdentity);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.NormalizationRuleIdentity), sourceContribution.NormalizationRuleIdentity);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderOrdinal), sourceContribution.ProviderOrdinal);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceOrdinal), sourceContribution.EvidenceOrdinal);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.CreatedAt), sourceContribution.CreatedAt);
            string contributionFingerprint = ExactResolutionCanonicalAuthority.PersistedProviderContributionFingerprint(
                evidenceFingerprint,
                contribution);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceFingerprint), contributionFingerprint);
            SetPrivate(contribution, nameof(ExactResolutionProviderEvidenceContribution.Id),
                ExactResolutionCanonicalAuthority.ProviderContributionId(contributionFingerprint));
            SetPrivate(decision, nameof(CrossRepositoryResolutionDecision.ProviderEvidenceSetFingerprint),
                ExactResolutionCanonicalAuthority.ProviderEvidenceSetFingerprint([contributionFingerprint]));

            Context.CrossRepositoryResolutionDecisions.Add(decision);
            Context.ExactResolutionProviderEvidenceContributions.Add(contribution);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            return decisionId;
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

        internal async Task AddMoreThanTwentyEligibleSectionsAsync()
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
                context.Entry(flowGroup).Property(nameof(FlowGroup.SystemId)).CurrentValue =
                    ExactCrossRepositoryTestValues.SystemId;
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
            int sectionOrder = 1,
            DocumentSectionKind sectionKind = DocumentSectionKind.Overview) =>
            DocumentSection.Create(
                id,
                logicalDocumentId,
                version.Id,
                version.RepositoryId,
                stableSectionId,
                sectionOrder,
                sectionKind,
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
