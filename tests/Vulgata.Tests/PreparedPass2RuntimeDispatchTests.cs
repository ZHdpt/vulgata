using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class PreparedPass2RuntimeDispatchTests
{
    [Fact]
    public void Create_RuntimeBinding_ProjectsCrossRepositorySupportToExactSevenSlots()
    {
        ResolvedPreparedPass2Flow resolved = CreateResolved();

        Pass2BusinessLogicJobPayload payload = Pass2BusinessLogicJobPayload.Create(resolved);

        Assert.Equal(resolved.Flow.ManifestId, payload.ManifestId);
        Assert.Equal(resolved.Flow.FlowId, payload.FlowId);
        Assert.Equal(resolved.Flow.RepositoryId, payload.RepositoryId);
        Assert.Equal(
            resolved.CodeLogicDocuments.Select(document => document.DocumentVersionId),
            payload.CodeLogicDocumentVersions.Select(document => document.DocumentVersionId));
        Assert.Equal(
            resolved.CodeLogicDocuments.Select(document => document.RepositoryId),
            payload.CodeLogicDocumentVersions.Select(document => document.RepositoryId));
        Assert.Equal(
            [
                nameof(Pass2BusinessLogicJobPayload.PreparedFlowSchemaIdentity),
                nameof(Pass2BusinessLogicJobPayload.ManifestId),
                nameof(Pass2BusinessLogicJobPayload.FlowId),
                nameof(Pass2BusinessLogicJobPayload.SystemId),
                nameof(Pass2BusinessLogicJobPayload.RepositoryId),
                nameof(Pass2BusinessLogicJobPayload.TraceEvidence),
                nameof(Pass2BusinessLogicJobPayload.CodeLogicDocumentVersions),
            ],
            typeof(Pass2BusinessLogicJobPayload)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(property => property.Name != nameof(Pass2BusinessLogicJobPayload.Kind))
                .Select(property => property.Name));
    }

    [Fact]
    public void Validate_RuntimeBindingChangedAfterPlanning_RejectsBeforeDispatch()
    {
        ResolvedPreparedPass2Flow resolved = CreateResolved();
        ResolvedPreparedPass2Flow substituted = resolved with
        {
            Flow = PreparedPass2Flow.Create(
                resolved.Flow.SchemaIdentity,
                resolved.Flow.ManifestId,
                Id(999),
                resolved.Flow.SystemId,
                resolved.Flow.RepositoryId,
                resolved.Flow.Members,
                resolved.Flow.TraceEvidence),
        };

        Assert.Throws<PreparedPass2FlowContractException>(() =>
            PreparedPass2FlowResolver.ValidateRuntime(substituted));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidatePass2_SectionAnchorOwnership_EnforcesConsumerOnly(bool providerOwned)
    {
        ResolvedPreparedPass2Flow resolved = CreateResolved();
        Pass2BusinessLogicJobPayload payload = Pass2BusinessLogicJobPayload.Create(resolved);
        WorkerAgentJobPacket packet = WorkerAgentJobPacket.Create(
            Id(30),
            WorkerAgentJobKind.Pass2BusinessLogicDocument,
            resolved.RuntimeBinding.Consumer.ScanExecutionId,
            resolved.Flow.RepositoryId,
            resolved.Flow.SystemId,
            resolved.RuntimeBinding.Consumer.ScanRunId,
            resolved.RuntimeBinding.Consumer.ProfiledRevision,
            WorkerAgentContractLimits.SupportedSchemaVersion,
            payload);
        WorkerAgentCodeLogicDocumentVersionProjection selectedDocument =
            payload.CodeLogicDocumentVersions.Single(document =>
                document.RepositoryId == (providerOwned
                    ? resolved.RuntimeBinding.Provider.RepositoryId
                    : resolved.RuntimeBinding.Consumer.RepositoryId));
        WorkerAgentSourceAnchor selectedAnchor = selectedDocument.SourceAnchors.Single();
        WorkerAgentSourceAnchorDto[] anchors = payload.CodeLogicDocumentVersions
            .SelectMany(document => document.SourceAnchors)
            .OrderBy(anchor => anchor.Id)
            .Select(anchor => new WorkerAgentSourceAnchorDto
            {
                Id = anchor.Id,
                RepositoryRelativePath = anchor.RepositoryRelativePath,
                EvidenceLocator = anchor.EvidenceLocator,
                LineStart = anchor.LineStart,
                LineEnd = anchor.LineEnd,
                SourceHash = anchor.SourceHash,
            })
            .ToArray();
        var response = new WorkerAgentPass2ResponseDto
        {
            JobId = packet.JobId,
            SystemId = packet.SystemId,
            RepositoryId = packet.RepositoryId,
            ScanRunId = packet.ScanRunId,
            ProfiledRevision = packet.ProfiledRevision,
            FlowId = payload.FlowId,
            Purpose = "Explain the prepared flow.",
            MainSteps =
            [
                new WorkerAgentBusinessMainStepDto
                {
                    Order = 1,
                    Description = "Call the provider.",
                    TraceEvidenceOrders = [1, 2],
                },
            ],
            Outcome = "The provider completes the request.",
            Content = new WorkerAgentCanonicalContentDto
            {
                SchemaIdentity = KnowledgeDocumentLimits.SchemaIdentity,
                DocumentType = DocumentType.BusinessLogic,
                StableKey = "consumer-provider-flow",
                Title = "Consumer provider flow",
                RepositoryRelativePath = "flows/consumer-provider",
                Summary = "A prepared cross-repository flow.",
                KeySymbols = [],
                BusinessTerms = [],
                TechnicalTerms = [],
                Sections =
                [
                    new WorkerAgentSectionDto
                    {
                        StableSectionId = Id(31),
                        Order = 1,
                        Kind = DocumentSectionKind.ProcessStep,
                        Heading = "Provider step",
                        Markdown = "The provider handles the request.",
                        SearchText = "provider request",
                        SourceAnchorIds = [selectedAnchor.Id],
                        DocumentVersionIds = [selectedDocument.DocumentVersionId],
                    },
                ],
            },
            SourceAnchors = anchors,
            TraceEvidenceOrders = [1, 2],
            GeneratedFromDocumentVersionIds = payload.CodeLogicDocumentVersions
                .Select(document => document.DocumentVersionId)
                .ToArray(),
        };

        if (providerOwned)
        {
            Assert.Throws<ArgumentException>(() => WorkerAgentResultValidator.ValidatePass2(
                packet,
                response,
                DateTimeOffset.Parse("2026-08-13T00:00:00Z"),
                DateTimeOffset.Parse("2026-08-13T00:00:01Z")));
        }
        else
        {
            WorkerAgentResultEnvelope result = WorkerAgentResultValidator.ValidatePass2(
                packet,
                response,
                DateTimeOffset.Parse("2026-08-13T00:00:00Z"),
                DateTimeOffset.Parse("2026-08-13T00:00:01Z"));
            Assert.Equal(WorkerAgentResultOutcome.Valid, result.Outcome);
        }
    }

    [Fact]
    public void ProductionComposition_RuntimePlannerReplacesFixedCatalogAuthority()
    {
        using var databaseFactory = new LoginLogoutTests.CustomWebApplicationFactory();
        using WebApplicationFactory<Program> factory = databaseFactory.WithWebHostBuilder(_ => { });
        using IServiceScope scope = factory.Services.CreateScope();

        Assert.IsType<PreparedDemoPass2FlowPlanner>(
            scope.ServiceProvider.GetRequiredService<IPreparedDemoPass2FlowPlanner>());
        Assert.Null(scope.ServiceProvider.GetService<PreparedPass2FlowCatalog>());
        Assert.Null(scope.ServiceProvider.GetService<PreparedPass2Flow>());
    }

    private static ResolvedPreparedPass2Flow CreateResolved()
    {
        PreparedPass2RuntimeRepositoryBinding consumer = RepositoryBinding("consumer", 2, 3, 4, 'a');
        PreparedPass2RuntimeRepositoryBinding provider = RepositoryBinding("provider", 5, 6, 7, 'b');
        PreparedPass2RuntimeMember[] members =
        [
            new(1, Id(10), Id(11), Id(12), consumer.RepositoryId, Id(13), [Id(14)]),
            new(2, Id(15), Id(16), Id(17), provider.RepositoryId, Id(18), [Id(19)]),
        ];
        PreparedPass2RuntimeBinding binding = PreparedPass2RuntimeBinding.Create(
            "1.0",
            "demo-profile",
            "1.0.0",
            new string('1', 64),
            "rpc-1",
            Id(1),
            consumer,
            provider,
            Id(8),
            [Id(9)],
            members,
            [
                new(1, members[0].JobId, members[0].CodeUnitId,
                    "消费方调用", "src/Consumer.cs#L1-L2"),
                new(2, members[1].JobId, members[1].CodeUnitId,
                    "提供方实现", "src/Provider.cs#L3-L4"),
            ]);
        CurrentKnowledge[] documents =
        [
            Current(members[0], "Consumer", "src/Consumer.cs"),
            Current(members[1], "Provider", "src/Provider.cs"),
        ];
        return new(binding.Flow, documents, binding);
    }

    private static CurrentKnowledge Current(
        PreparedPass2RuntimeMember member,
        string name,
        string path) =>
        new(
            member.LogicalDocumentId,
            member.DocumentVersionId,
            member.RepositoryId,
            DocumentType.CodeLogic,
            name,
            path,
            "{}",
            name,
            [new CurrentSourceAnchor(
                member.SourceAnchorIds[0],
                member.CodeUnitId,
                path,
                $"{name}.Execute",
                1,
                2,
                new string('c', 64))],
            [],
            [],
            new CurrentIndexEntry(name, [], [], [], name),
            []);

    private static PreparedPass2RuntimeRepositoryBinding RepositoryBinding(
        string alias,
        int repository,
        int scanRun,
        int scanExecution,
        char commit) =>
        new(
            alias,
            $"git-{alias}",
            Id(repository),
            Id(scanRun),
            Id(scanExecution),
            $"refs/tags/{alias}",
            new string(commit, 40),
            new string(commit, 40));

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
