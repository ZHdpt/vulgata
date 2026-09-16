using System.Reflection;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Tests;

internal static class KnowledgeStoreTestFixtures
{
    internal static readonly Guid SystemId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    internal static readonly Guid SourceRepositoryId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    internal static readonly Guid TargetRepositoryId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    internal static readonly Guid SourceScanRunId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    internal static readonly Guid TargetScanRunId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    internal static readonly Guid SourceCodeUnitId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    internal static readonly Guid LogicalDocumentId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    internal static readonly Guid DocumentVersionId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    internal static readonly Guid TargetDocumentVersionId = Guid.Parse("60000000-0000-0000-0000-000000000002");
    internal static readonly Guid FirstSectionId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    internal static readonly Guid SecondSectionId = Guid.Parse("70000000-0000-0000-0000-000000000002");
    internal static readonly DateTimeOffset CreatedAt =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    internal const string ProfiledRevision = "0123456789abcdef0123456789abcdef01234567";
    internal const string SourceHash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    internal static string CreateCanonicalJson(
        string documentType = "CodeLogic",
        string title = "订单服务",
        string repositoryRelativePath = "src/orders.cs",
        string summary = "处理订单。",
        IReadOnlyList<string>? keySymbols = null,
        IReadOnlyList<string>? businessTerms = null,
        IReadOnlyList<string>? technicalTerms = null,
        IReadOnlyList<CanonicalSectionFixture>? sections = null)
    {
        keySymbols ??= ["OrderService"];
        businessTerms ??= ["订单"];
        technicalTerms ??= ["HTTP"];
        sections ??=
        [
            new(
                FirstSectionId,
                1,
                "Overview",
                "概览",
                "处理订单请求。",
                "处理订单请求",
                [],
                []),
            new(
                SecondSectionId,
                2,
                "TechnicalDetail",
                "实现",
                "调用 `/api/orders`。",
                "API orders",
                [],
                []),
        ];

        var value = new
        {
            schemaIdentity = "vulgata.knowledge-document/1.0",
            documentType,
            stableKey = "orders/service",
            title,
            repositoryRelativePath,
            summary,
            keySymbols,
            businessTerms,
            technicalTerms,
            sections = sections.Select(section => new
            {
                stableSectionId = section.StableSectionId,
                order = section.Order,
                kind = section.Kind,
                heading = section.Heading,
                markdown = section.Markdown,
                searchText = section.SearchText,
                sourceAnchorIds = section.SourceAnchorIds,
                documentVersionIds = section.DocumentVersionIds,
            }),
        };

        return JsonSerializer.Serialize(value);
    }

    internal static RepositoryEntity CreateRepository(Guid id, string name)
    {
        RepositoryEntity repository = RepositoryEntity.Create(
            SystemId,
            name,
            $"https://example.invalid/{name}.git",
            null,
            null,
            CreatedAt);
        SetId(repository, id);
        return repository;
    }

    internal static ScanRun CreateCompletedScanRun(Guid id, Guid repositoryId)
    {
        ScanRun scanRun = ScanRun.Start(repositoryId, 1_800, CreatedAt);
        SetId(scanRun, id);
        scanRun.BindProfiledRevision(ProfiledRevision);
        scanRun.SetCandidateCount(0);
        scanRun.Complete(0, 0, CreatedAt.AddSeconds(1));
        return scanRun;
    }

    internal static CodeUnit CreateCodeUnit(Guid id, Guid scanRunId, Guid repositoryId)
    {
        var codeUnit = new CodeUnit(
            scanRunId,
            repositoryId,
            "src/orders.cs",
            SourceHash,
            SourceHash,
            1,
            10,
            "OrderService",
            CodeUnitType.Class,
            RangeConfidence.Exact,
            "csharp");
        SetId(codeUnit, id);
        return codeUnit;
    }

    internal static KnowledgeCommit CreateBusinessKnowledgeCommit(
        KnowledgeDocumentMaterialization materialization,
        IReadOnlyList<EdgeDraft>? edges = null,
        IReadOnlyList<FlowGroupDraft>? flowGroups = null)
    {
        Guid edgeId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
        edges ??=
        [
            new(
                edgeId,
                DocumentVersionId,
                SourceRepositoryId,
                SourceScanRunId,
                TargetDocumentVersionId,
                TargetRepositoryId,
                TargetScanRunId,
                DocumentEdgeType.GeneratedFrom),
        ];
        flowGroups ??=
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
                                DocumentVersionId,
                                SourceRepositoryId,
                                SourceScanRunId,
                                null,
                                FlowTraceStepKind.Document,
                                "业务逻辑"),
                            new(
                                Guid.Parse("d0000000-0000-0000-0000-000000000002"),
                                2,
                                TargetDocumentVersionId,
                                TargetRepositoryId,
                                TargetScanRunId,
                                edgeId,
                                FlowTraceStepKind.Relationship,
                                "代码证据"),
                        ]),
                ]),
        ];

        return new(
            Guid.Parse("e0000000-0000-0000-0000-000000000001"),
            LogicalDocumentId,
            null,
            DocumentVersionId,
            SourceRepositoryId,
            SourceScanRunId,
            ProfiledRevision,
            materialization.Content,
            [],
            edges,
            flowGroups,
            CreatedAt);
    }

    private static void SetId<T>(T value, Guid id)
    {
        PropertyInfo property = typeof(T).GetProperty(nameof(RepositoryEntity.Id)) ??
            throw new InvalidOperationException("Fixture entity has no Id property.");
        property.SetValue(value, id);
    }
}

internal sealed record CanonicalSectionFixture(
    Guid StableSectionId,
    int Order,
    string Kind,
    string Heading,
    string Markdown,
    string SearchText,
    IReadOnlyList<Guid> SourceAnchorIds,
    IReadOnlyList<Guid> DocumentVersionIds);
