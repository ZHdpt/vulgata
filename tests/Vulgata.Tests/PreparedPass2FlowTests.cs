using Vulgata.Tests.TestInfrastructure;
using System.Text;
using System.Text.Json.Nodes;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests;

public sealed class PreparedPass2FlowTests
{
    private const string _profiledRevision =
        "0123456789abcdef0123456789abcdef01234567";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Catalog_EmbeddedFixedManifest_LoadsExactlyOneClosedPreparedFlow()
    {
        PreparedPass2FlowCatalog catalog = PreparedPass2FlowCatalog.Load(
            typeof(PreparedPass2FlowCatalog).Assembly);

        Assert.Equal(PreparedPass2FlowCatalog.FixedResourceName, catalog.ResourceName);
        Assert.Equal(PreparedPass2FlowLimits.SupportedSchemaIdentity, catalog.Flow.SchemaIdentity);
        Assert.Equal(Id(200), catalog.Flow.ManifestId);
        Assert.Equal(Id(201), catalog.Flow.FlowId);
        Assert.Equal(Id(4), catalog.Flow.SystemId);
        Assert.Equal(Id(3), catalog.Flow.RepositoryId);
        Assert.Equal([Id(1), Id(10)], catalog.Flow.Members.Select(member => member.JobId));
        Assert.Equal([Id(6), Id(11)], catalog.Flow.Members.Select(member => member.CodeUnitId));
        Assert.Equal([1, 2], catalog.Flow.TraceEvidence.Select(evidence => evidence.Order));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Catalog_ZeroOrMultipleFixedResources_RejectsBeforeUse()
    {
        Assert.Throws<PreparedPass2FlowContractException>(() =>
            PreparedPass2FlowCatalog.Load([]));
        Assert.Throws<PreparedPass2FlowContractException>(() =>
            PreparedPass2FlowCatalog.Load(
            [
                Resource(ValidManifestJson()),
                Resource(ValidManifestJson()),
            ]));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing-schema")]
    [InlineData("wrong-schema")]
    [InlineData("extra-field")]
    [InlineData("empty-id")]
    [InlineData("empty-members")]
    [InlineData("duplicate-member")]
    [InlineData("reordered-member")]
    [InlineData("noncontiguous-trace")]
    [InlineData("duplicate-trace")]
    [InlineData("oversized-evidence")]
    [InlineData("raw-source")]
    [InlineData("credential")]
    [InlineData("absolute-path")]
    [InlineData("traversal")]
    [InlineData("query")]
    public void Catalog_InvalidClosedManifest_RejectsBeforeUse(string scenario)
    {
        JsonObject manifest = JsonNode.Parse(ValidManifestJson())!.AsObject();
        JsonArray members = manifest["members"]!.AsArray();
        JsonArray trace = manifest["traceEvidence"]!.AsArray();
        switch (scenario)
        {
            case "missing-schema":
                _ = manifest.Remove("schemaIdentity");
                break;
            case "wrong-schema":
                manifest["schemaIdentity"] = "vulgata.prepared-pass2-flow/2.0";
                break;
            case "extra-field":
                manifest["entryPointQuery"] = "MATCH (n) RETURN n";
                break;
            case "empty-id":
                members[0]!["jobId"] = Guid.Empty.ToString("D");
                break;
            case "empty-members":
                manifest["members"] = new JsonArray();
                break;
            case "duplicate-member":
                members.Add(members[0]!.DeepClone());
                break;
            case "reordered-member":
                JsonNode first = members[0]!.DeepClone();
                JsonNode second = members[1]!.DeepClone();
                members[0] = second;
                members[1] = first;
                break;
            case "noncontiguous-trace":
                trace[1]!["order"] = 3;
                break;
            case "duplicate-trace":
                trace.Add(trace[0]!.DeepClone());
                break;
            case "oversized-evidence":
                trace[0]!["label"] = new string('x', 513);
                break;
            case "raw-source":
                trace[0]!["label"] = "public class Order { return total; }";
                break;
            case "credential":
                trace[0]!["label"] = "api_key=secret-value";
                break;
            case "absolute-path":
                trace[0]!["locator"] = "C:\\source\\OrderService.cs";
                break;
            case "traversal":
                trace[0]!["label"] = "traversal from every entry point";
                break;
            case "query":
                trace[0]!["label"] = "MATCH (n) RETURN n query";
                break;
        }

        Assert.Throws<PreparedPass2FlowContractException>(() =>
            PreparedPass2FlowCatalog.Load([Resource(manifest.ToJsonString())]));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Resolve_ExactCurrentCodeLogicDocuments_ReturnsManifestOrder()
    {
        PreparedPass2Flow flow = LoadFlow();
        PreparedPass2CodeLogicCandidate[] candidates = CreateCandidates(flow);

        ResolvedPreparedPass2Flow resolved = PreparedPass2FlowResolver.Resolve(
            flow,
            Id(4),
            Id(3),
            Id(5),
            _profiledRevision,
            new RepositoryAccessScope([Id(3)]),
            candidates);

        Assert.Equal(flow, resolved.Flow);
        Assert.Equal(
            [Id(301), Id(302)],
            resolved.CodeLogicDocuments.Select(document => document.DocumentVersionId));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Resolve_InvalidMemberResolution_RejectsWithZeroProviderCalls()
    {
        PreparedPass2Flow flow = LoadFlow();
        PreparedPass2CodeLogicCandidate[] valid = CreateCandidates(flow);
        var invalidCases = new Dictionary<string, IReadOnlyList<PreparedPass2CodeLogicCandidate>>
        {
            ["zero"] = [],
            ["missing"] = [valid[0]],
            ["extra"] = [.. valid, Candidate(flow.Members[0], 9, 399)],
            ["stale"] = [valid[0] with { CommittedDocumentVersionId = Id(999) }, valid[1]],
            ["duplicate"] = [valid[0], valid[0]],
            ["reordered"] = [valid[1], valid[0]],
            ["ambiguous"] = [valid[0], valid[0] with { CommittedDocumentVersionId = Id(998) }],
            ["unauthorized"] = [valid[0] with { RepositoryId = Id(99) }, valid[1]],
            ["wrong-system"] = [valid[0] with { SystemId = Id(99) }, valid[1]],
            ["wrong-scan"] = [valid[0] with { ScanRunId = Id(99) }, valid[1]],
            ["wrong-revision"] = [valid[0] with { ProfiledRevision = new('a', 40) }, valid[1]],
        };
        int providerCalls = 0;

        foreach ((string scenario, IReadOnlyList<PreparedPass2CodeLogicCandidate> candidates)
            in invalidCases)
        {
            PreparedPass2FlowContractException exception = Assert.Throws<
                PreparedPass2FlowContractException>(() =>
                {
                    _ = PreparedPass2FlowResolver.Resolve(
                        flow,
                        Id(4),
                        Id(3),
                        Id(5),
                        _profiledRevision,
                        new RepositoryAccessScope([Id(3)]),
                        candidates);
                    providerCalls++;
                });
            Assert.NotNull(exception);
            Assert.False(string.IsNullOrWhiteSpace(scenario));
        }

        Assert.Equal(0, providerCalls);
    }

    private static PreparedPass2Flow LoadFlow() =>
        PreparedPass2FlowCatalog.Load(typeof(PreparedPass2FlowCatalog).Assembly).Flow;

    private static PreparedPass2CodeLogicCandidate[] CreateCandidates(
        PreparedPass2Flow flow) =>
    [
        Candidate(flow.Members[0], 1, 301),
        Candidate(flow.Members[1], 2, 302),
    ];

    private static PreparedPass2CodeLogicCandidate Candidate(
        PreparedPass2FlowMember member,
        int offset,
        int documentVersionId) =>
        new(
            member.JobId,
            member.CodeUnitId,
            Id(4),
            Id(3),
            Id(5),
            _profiledRevision,
            Id(documentVersionId),
            Current(member, offset, documentVersionId));

    private static CurrentKnowledge Current(
        PreparedPass2FlowMember member,
        int offset,
        int documentVersionId) =>
        new(
            member.LogicalDocumentId,
            Id(documentVersionId),
            Id(3),
            DocumentType.CodeLogic,
            $"Code Logic {offset}",
            $"src/Demo/Flow{offset}.cs",
            "{}",
            $"Code Logic {offset}",
            [new CurrentSourceAnchor(
                Id(400 + offset),
                member.CodeUnitId,
                $"src/Demo/Flow{offset}.cs",
                $"Flow{offset}.Execute",
                1,
                2,
                new('a', 64))],
            [],
            [],
            new CurrentIndexEntry($"Summary {offset}", [], [], [], $"flow {offset}"),
            []);

    private static PreparedPass2FlowResource Resource(string json) =>
        new(
            PreparedPass2FlowCatalog.FixedResourceName,
            () => new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false));

    private static string ValidManifestJson() =>
        """
        {
          "schemaIdentity": "vulgata.prepared-pass2-flow/1.0",
          "manifestId": "00000000-0000-0000-0000-000000000200",
          "flowId": "00000000-0000-0000-0000-000000000201",
          "systemId": "00000000-0000-0000-0000-000000000004",
          "repositoryId": "00000000-0000-0000-0000-000000000003",
          "members": [
            {
              "order": 1,
              "jobId": "00000000-0000-0000-0000-000000000001",
              "codeUnitId": "00000000-0000-0000-0000-000000000006",
              "logicalDocumentId": "00000000-0000-0000-0000-000000000101"
            },
            {
              "order": 2,
              "jobId": "00000000-0000-0000-0000-000000000010",
              "codeUnitId": "00000000-0000-0000-0000-000000000011",
              "logicalDocumentId": "00000000-0000-0000-0000-000000000102"
            }
          ],
          "traceEvidence": [
            {
              "order": 1,
              "memberJobId": "00000000-0000-0000-0000-000000000001",
              "codeUnitId": "00000000-0000-0000-0000-000000000006",
              "label": "订单总额计算",
              "locator": "src/Orders/OrderService.cs#L12-L24"
            },
            {
              "order": 2,
              "memberJobId": "00000000-0000-0000-0000-000000000010",
              "codeUnitId": "00000000-0000-0000-0000-000000000011",
              "label": "库存预留",
              "locator": "src/Inventory/InventoryService.cs#L30-L44"
            }
          ]
        }
        """;

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
