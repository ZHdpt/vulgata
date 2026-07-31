using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class KnowledgeStoreDomainTests
{
    [Fact]
    public void Materialize_ValidCanonicalContent_ProducesDeterministicFiniteProjection()
    {
        string input = KnowledgeStoreTestFixtures.CreateCanonicalJson();
        var materializer = new KnowledgeDocumentMaterializer();

        KnowledgeDocumentMaterialization first = materializer.Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SystemId,
            input,
            KnowledgeStoreTestFixtures.CreatedAt);
        KnowledgeDocumentMaterialization second = materializer.Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SystemId,
            input,
            KnowledgeStoreTestFixtures.CreatedAt);

        Assert.Equal(first.ContentJson, second.ContentJson);
        Assert.Equal(
            "# 订单服务\n\n## 概览\n\n处理订单请求。\n\n## 实现\n\n调用 `/api/orders`。",
            first.ContentMarkdown);
        Assert.Equal(
            Convert.ToHexStringLower(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes($"{first.ContentJson}\n{first.ContentMarkdown}"))),
            first.ContentFingerprint);
        Assert.Equal("订单服务", first.IndexEntry.Title);
        Assert.Equal("src/orders.cs", first.IndexEntry.RepositoryRelativePath);
        Assert.Equal(2, first.Sections.Count);
        Assert.Collection(
            first.Sections,
            section =>
            {
                Assert.Equal(1, section.SectionOrder);
                Assert.Equal(KnowledgeStoreTestFixtures.FirstSectionId, section.StableSectionId);
            },
            section =>
            {
                Assert.Equal(2, section.SectionOrder);
                Assert.Equal(KnowledgeStoreTestFixtures.SecondSectionId, section.StableSectionId);
            });
    }

    [Theory]
    [InlineData("password=hunter2")]
    [InlineData("pwd=hunter2")]
    [InlineData("api_key=abc")]
    [InlineData("apikey=abc")]
    [InlineData("client_secret=abc")]
    [InlineData("secret=abc")]
    [InlineData("access_token=abc")]
    [InlineData("Authorization: Bearer abc")]
    [InlineData("-----BEGIN PRIVATE KEY-----")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----")]
    [InlineData("Host=db;Password=p")]
    [InlineData(@"C:\agent\_work\repo")]
    [InlineData(@"\\server\share\repo")]
    [InlineData("file:///tmp/repo")]
    [InlineData("/home/user/repo")]
    [InlineData("/var/lib/repo")]
    [InlineData("/tmp/repo")]
    [InlineData("/Users/name/repo")]
    [InlineData("```csharp\nvar secret = 1;\n```")]
    [InlineData("~~~\nsource\n~~~")]
    [InlineData("<pre><code>source</code></pre>")]
    [InlineData("    source line")]
    [InlineData("Stack trace: failure")]
    [InlineData("Traceback (most recent call last):")]
    [InlineData("InnerException")]
    [InlineData("SqlState")]
    [InlineData("Exception: failed")]
    [InlineData(" at Example.Service.Run()")]
    public void Materialize_ProhibitedSensitiveMarker_RejectsWithoutRawDiagnostic(
        string prohibitedValue)
    {
        var materializer = new KnowledgeDocumentMaterializer();
        string input = KnowledgeStoreTestFixtures.CreateCanonicalJson(summary: prohibitedValue);

        KnowledgeStoreContractException exception = Assert.Throws<KnowledgeStoreContractException>(
            () => materializer.Materialize(
                KnowledgeStoreTestFixtures.LogicalDocumentId,
                KnowledgeStoreTestFixtures.DocumentVersionId,
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.SystemId,
                input,
                KnowledgeStoreTestFixtures.CreatedAt));

        Assert.Equal(KnowledgeRejectionCode.InvalidContent, exception.RejectionCode);
        Assert.DoesNotContain(
            prohibitedValue,
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(InvalidCanonicalJsonCases))]
    public void Materialize_InvalidCanonicalShape_RejectsClosedCode(string input)
    {
        var materializer = new KnowledgeDocumentMaterializer();

        KnowledgeStoreContractException exception = Assert.Throws<KnowledgeStoreContractException>(
            () => materializer.Materialize(
                KnowledgeStoreTestFixtures.LogicalDocumentId,
                KnowledgeStoreTestFixtures.DocumentVersionId,
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.SystemId,
                input,
                KnowledgeStoreTestFixtures.CreatedAt));

        Assert.Equal(KnowledgeRejectionCode.InvalidContent, exception.RejectionCode);
    }

    [Fact]
    public void Materialize_FormCAndSafeCounterexamples_NormalizesWithoutFalsePositive()
    {
        var materializer = new KnowledgeDocumentMaterializer();
        string input = KnowledgeStoreTestFixtures.CreateCanonicalJson(
            title: "Cafe\u0301",
            summary: "PasswordValidator checks authorization for /api/orders.");

        KnowledgeDocumentMaterialization result = materializer.Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SystemId,
            input,
            KnowledgeStoreTestFixtures.CreatedAt);

        Assert.Equal("Café", result.Content.Title);
        Assert.Contains("/api/orders", result.IndexEntry.SearchText, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ExactBoundaryCases))]
    public void Materialize_ExactBoundary_AcceptsOrRejectsOneUnitOverflow(
        string input,
        bool accepted,
        KnowledgeRejectionCode expectedCode)
    {
        var materializer = new KnowledgeDocumentMaterializer();

        if (accepted)
        {
            KnowledgeDocumentMaterialization result = materializer.Materialize(
                KnowledgeStoreTestFixtures.LogicalDocumentId,
                KnowledgeStoreTestFixtures.DocumentVersionId,
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.SystemId,
                input,
                KnowledgeStoreTestFixtures.CreatedAt);
            Assert.NotNull(result.IndexEntry);
            return;
        }

        KnowledgeStoreContractException exception = Assert.Throws<KnowledgeStoreContractException>(
            () => materializer.Materialize(
                KnowledgeStoreTestFixtures.LogicalDocumentId,
                KnowledgeStoreTestFixtures.DocumentVersionId,
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                KnowledgeStoreTestFixtures.SystemId,
                input,
                KnowledgeStoreTestFixtures.CreatedAt));
        Assert.Equal(expectedCode, exception.RejectionCode);
    }

    public static TheoryData<string> InvalidCanonicalJsonCases()
    {
        string valid = KnowledgeStoreTestFixtures.CreateCanonicalJson(title: "Title");
        var cases = new TheoryData<string>
        {
            "[]",
            valid.Replace(
                "\"schemaIdentity\":\"vulgata.knowledge-document/1.0\"",
                "\"schemaIdentity\":\"other\"",
                StringComparison.Ordinal),
            valid.Replace(
                "\"documentType\":\"CodeLogic\"",
                "\"documentType\":\"Unknown\"",
                StringComparison.Ordinal),
            valid.Replace(
                "\"kind\":\"Overview\"",
                "\"kind\":\"Unknown\"",
                StringComparison.Ordinal),
            valid.Replace(
                "\"title\":\"Title\"",
                "\"title\":\"first\",\"title\":\"Title\"",
                StringComparison.Ordinal),
            valid.Replace("\"title\":\"Title\",", string.Empty, StringComparison.Ordinal),
            $"{valid[..^1]},\"payload\":{{}}}}",
            $"{valid[..^1]},\"sourceText\":\"source\"}}",
            $"{valid[..^1]},\"snippet\":\"source\"}}",
            $"{valid[..^1]},\"code\":\"source\"}}",
            $"{valid[..^1]},\"diagnostic\":\"detail\"}}",
            $"{valid[..^1]},\"stackTrace\":\"detail\"}}",
            $"{valid[..^1]},\"password\":\"detail\"}}",
            $"{valid[..^1]},\"apiKey\":\"detail\"}}",
            $"{valid[..^1]},}}",
            $"/*comment*/{valid}",
            """{"a":{"b":{"c":{"d":{"e":1}}}}}""",
        };
        return cases;
    }

    public static TheoryData<string, bool, KnowledgeRejectionCode> ExactBoundaryCases()
    {
        var cases = new TheoryData<string, bool, KnowledgeRejectionCode>();
        AddPair(
            cases,
            KnowledgeStoreTestFixtures.CreateCanonicalJson(title: new string('a', 4_096)),
            KnowledgeStoreTestFixtures.CreateCanonicalJson(title: new string('a', 4_097)));
        AddPair(
            cases,
            KnowledgeStoreTestFixtures.CreateCanonicalJson(
                repositoryRelativePath: new string('p', 2_048)),
            KnowledgeStoreTestFixtures.CreateCanonicalJson(
                repositoryRelativePath: new string('p', 2_049)));
        AddPair(
            cases,
            KnowledgeStoreTestFixtures.CreateCanonicalJson(
                keySymbols: Enumerable.Range(0, 256).Select(index => $"s{index:D3}").ToArray()),
            KnowledgeStoreTestFixtures.CreateCanonicalJson(
                keySymbols: Enumerable.Range(0, 257).Select(index => $"s{index:D3}").ToArray()));
        AddPair(
            cases,
            CreateWithSections(CreateSections(256)),
            CreateWithSections(CreateSections(257)));
        AddPair(
            cases,
            CreateWithSections(
                [CreateSection(1, markdown: new string('m', 65_536))]),
            CreateWithSections(
                [CreateSection(1, markdown: new string('m', 65_537))]));
        AddPair(
            cases,
            CreateWithSections(
                [CreateSection(1, searchText: new string('s', 131_072))]),
            CreateWithSections(
                [CreateSection(1, searchText: new string('s', 131_073))]));
        AddPair(
            cases,
            CreateWithSections(
                [CreateSection(1, sourceAnchorIds: CreateGuids(512, "8"))]),
            CreateWithSections(
                [CreateSection(1, sourceAnchorIds: CreateGuids(513, "8"))]));
        AddPair(
            cases,
            CreateWithSections(
                [CreateSection(1, documentVersionIds: CreateGuids(1_024, "9"))]),
            CreateWithSections(
                [CreateSection(1, documentVersionIds: CreateGuids(1_025, "9"))]));
        AddPair(
            cases,
            CreateJsonAtExactUtf8Size(KnowledgeDocumentLimits.MaximumContentBytes),
            CreateJsonAtExactUtf8Size(KnowledgeDocumentLimits.MaximumContentBytes + 1));
        return cases;
    }

    private static void AddPair(
        TheoryData<string, bool, KnowledgeRejectionCode> cases,
        string accepted,
        string rejected)
    {
        cases.Add(accepted, true, KnowledgeRejectionCode.InvalidBound);
        cases.Add(rejected, false, KnowledgeRejectionCode.InvalidBound);
    }

    private static string CreateJsonAtExactUtf8Size(int desiredBytes)
    {
        CanonicalSectionFixture[] sections = CreateSections(8);
        string baseline = CreateWithSections(sections);
        int remaining = desiredBytes - Encoding.UTF8.GetByteCount(baseline);
        Assert.True(remaining >= 0);

        CanonicalSectionFixture[] filled = sections
            .Select((section, index) =>
            {
                int addition = Math.Min(remaining, 131_071);
                remaining -= addition;
                return section with { SearchText = new string('s', addition + 1) };
            })
            .ToArray();
        Assert.Equal(0, remaining);
        string result = CreateWithSections(filled);
        Assert.Equal(desiredBytes, Encoding.UTF8.GetByteCount(result));
        return result;
    }

    private static CanonicalSectionFixture[] CreateSections(int count) =>
        Enumerable.Range(1, count).Select(index => CreateSection(index)).ToArray();

    private static CanonicalSectionFixture CreateSection(
        int order,
        string markdown = "m",
        string searchText = "s",
        IReadOnlyList<Guid>? sourceAnchorIds = null,
        IReadOnlyList<Guid>? documentVersionIds = null) =>
        new(
            Guid.Parse($"70000000-0000-0000-0000-{order:X12}"),
            order,
            "Overview",
            $"h{order:D3}",
            markdown,
            searchText,
            sourceAnchorIds ?? [],
            documentVersionIds ?? []);

    private static Guid[] CreateGuids(int count, string prefix) =>
        Enumerable.Range(1, count)
            .Select(index => Guid.Parse($"{prefix}0000000-0000-0000-0000-{index:X12}"))
            .ToArray();

    private static string CreateWithSections(IReadOnlyList<CanonicalSectionFixture> sections) =>
        KnowledgeStoreTestFixtures.CreateCanonicalJson(sections: sections);

    [Fact]
    public void ValidateCommit_ExactGeneratedFromFlow_AcceptsClosedBusinessEvidence()
    {
        var materializer = new KnowledgeDocumentMaterializer();
        KnowledgeDocumentMaterialization content = materializer.Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SystemId,
            KnowledgeStoreTestFixtures.CreateCanonicalJson(documentType: "BusinessLogic"),
            KnowledgeStoreTestFixtures.CreatedAt);
        KnowledgeCommit commit =
            KnowledgeStoreTestFixtures.CreateBusinessKnowledgeCommit(content);

        KnowledgeStoreContractValidator.ValidateCommit(commit);
    }

    [Fact]
    public void ValidateCommit_UndefinedDocumentEdgeType_RejectsClosedCode()
    {
        var materializer = new KnowledgeDocumentMaterializer();
        KnowledgeDocumentMaterialization content = materializer.Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SystemId,
            KnowledgeStoreTestFixtures.CreateCanonicalJson(documentType: "BusinessLogic"),
            KnowledgeStoreTestFixtures.CreatedAt);
        KnowledgeCommit valid =
            KnowledgeStoreTestFixtures.CreateBusinessKnowledgeCommit(content);
        EdgeDraft invalidEdge = valid.Edges[0] with { EdgeType = (DocumentEdgeType)999 };
        KnowledgeCommit invalid = valid with { Edges = [invalidEdge] };

        KnowledgeStoreContractException exception =
            Assert.Throws<KnowledgeStoreContractException>(
                () => KnowledgeStoreContractValidator.ValidateCommit(invalid));

        Assert.Equal(KnowledgeRejectionCode.InvalidEdgeSemantic, exception.RejectionCode);
    }

    [Fact]
    public void ValidateCommit_UndefinedFlowTraceStepKind_RejectsClosedCode()
    {
        var materializer = new KnowledgeDocumentMaterializer();
        KnowledgeDocumentMaterialization content = materializer.Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SystemId,
            KnowledgeStoreTestFixtures.CreateCanonicalJson(documentType: "BusinessLogic"),
            KnowledgeStoreTestFixtures.CreatedAt);
        KnowledgeCommit valid =
            KnowledgeStoreTestFixtures.CreateBusinessKnowledgeCommit(content);
        FlowGroupDraft validGroup = valid.FlowGroups[0];
        FlowTraceDraft validTrace = validGroup.Traces[0];
        FlowTraceStepDraft invalidStep =
            validTrace.Steps[0] with { StepKind = (FlowTraceStepKind)999 };
        FlowTraceDraft invalidTrace = validTrace with
        {
            Steps = [invalidStep, .. validTrace.Steps.Skip(1)],
        };
        FlowGroupDraft invalidGroup = validGroup with { Traces = [invalidTrace] };
        KnowledgeCommit invalid = valid with { FlowGroups = [invalidGroup] };

        KnowledgeStoreContractException exception =
            Assert.Throws<KnowledgeStoreContractException>(
                () => KnowledgeStoreContractValidator.ValidateCommit(invalid));

        Assert.Equal(KnowledgeRejectionCode.InvalidFlow, exception.RejectionCode);
    }

    [Fact]
    public void ValidateCommit_NonContiguousFlowAndWrongEdgeSemantics_RejectsClosedCodes()
    {
        var materializer = new KnowledgeDocumentMaterializer();
        KnowledgeDocumentMaterialization codeLogic = materializer.Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SystemId,
            KnowledgeStoreTestFixtures.CreateCanonicalJson(),
            KnowledgeStoreTestFixtures.CreatedAt);
        EdgeDraft generatedFrom = new(
            Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.TargetDocumentVersionId,
            KnowledgeStoreTestFixtures.TargetRepositoryId,
            KnowledgeStoreTestFixtures.TargetScanRunId,
            DocumentEdgeType.GeneratedFrom);

        KnowledgeCommit wrongSemantic =
            KnowledgeStoreTestFixtures.CreateBusinessKnowledgeCommit(codeLogic, [generatedFrom]);
        KnowledgeStoreContractException semanticException =
            Assert.Throws<KnowledgeStoreContractException>(
                () => KnowledgeStoreContractValidator.ValidateCommit(wrongSemantic));
        Assert.Equal(
            KnowledgeRejectionCode.InvalidEdgeSemantic,
            semanticException.RejectionCode);

        KnowledgeDocumentMaterialization businessLogic = materializer.Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SystemId,
            KnowledgeStoreTestFixtures.CreateCanonicalJson(documentType: "BusinessLogic"),
            KnowledgeStoreTestFixtures.CreatedAt);
        KnowledgeCommit valid =
            KnowledgeStoreTestFixtures.CreateBusinessKnowledgeCommit(businessLogic);
        FlowGroupDraft invalidGroup = valid.FlowGroups[0] with { GroupOrder = 2 };
        KnowledgeCommit wrongOrder = valid with { FlowGroups = [invalidGroup] };
        KnowledgeStoreContractException flowException =
            Assert.Throws<KnowledgeStoreContractException>(
                () => KnowledgeStoreContractValidator.ValidateCommit(wrongOrder));
        Assert.Equal(KnowledgeRejectionCode.InvalidFlow, flowException.RejectionCode);
    }

    [Fact]
    public void MoveCurrentVersion_ValidSuccessor_PreservesImmutableVersionChain()
    {
        var materializer = new KnowledgeDocumentMaterializer();
        KnowledgeDocumentMaterialization firstContent = materializer.Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SystemId,
            KnowledgeStoreTestFixtures.CreateCanonicalJson(),
            KnowledgeStoreTestFixtures.CreatedAt);
        DocumentVersion first = CreateVersion(
            KnowledgeStoreTestFixtures.DocumentVersionId,
            1,
            firstContent);
        LogicalDocument document = LogicalDocument.Create(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            DocumentType.CodeLogic,
            firstContent.Content.StableKey,
            KnowledgeStoreTestFixtures.CreatedAt);
        document.SetInitialCurrentVersion(first);

        Guid successorId = Guid.Parse("60000000-0000-0000-0000-000000000003");
        KnowledgeDocumentMaterialization secondContent = materializer.Materialize(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            successorId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SystemId,
            KnowledgeStoreTestFixtures.CreateCanonicalJson(title: "订单服务 V2"),
            KnowledgeStoreTestFixtures.CreatedAt.AddMinutes(1));
        DocumentVersion successor = CreateVersion(successorId, 2, secondContent);

        document.MoveCurrentVersion(first.Id, successor);

        Assert.Equal(successorId, document.CurrentVersionId);
        Assert.Equal(successorId, first.SupersededById);
        Assert.Equal(firstContent.ContentJson, first.ContentJson);
        Assert.Equal(2, document.Versions.Count);
        Assert.Throws<InvalidOperationException>(() => first.SupersedeWith(Guid.NewGuid()));
    }

    private static DocumentVersion CreateVersion(
        Guid id,
        int versionNumber,
        KnowledgeDocumentMaterialization content) =>
        DocumentVersion.Create(
            id,
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SystemId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.ProfiledRevision,
            versionNumber,
            content.Content.Title,
            content.Content.RepositoryRelativePath,
            content.Content.SchemaIdentity,
            content.ContentJson,
            content.ContentMarkdown,
            content.ContentFingerprint,
            KnowledgeStoreTestFixtures.CreatedAt.AddMinutes(versionNumber - 1));
}
