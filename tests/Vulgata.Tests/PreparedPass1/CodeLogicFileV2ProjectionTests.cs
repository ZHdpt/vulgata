using System.Collections;
using System.Reflection;
using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T026 RED slice.  These tests deliberately enter through the
/// future pure projector seam and keep the authoritative fixture as JSON.  A
/// Worker result must never be allowed to provide Markdown, Sections, search
/// text, identities, or a commit fingerprint.
/// </summary>
public sealed class CodeLogicFileV2ProjectionTests
{
    private static readonly Guid JobId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ScanExecutionId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid SystemId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid RepositoryId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ScanRunId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid FileCodeUnitId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid ManifestId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly Guid PairPackageId = Guid.Parse("88888888-8888-4888-8888-888888888888");
    private static readonly Guid DocumentVersionId = Guid.Parse("99999999-9999-4999-8999-999999999999");
    private static readonly Guid LogicalDocumentId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid TargetDocumentVersionA = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid TargetDocumentVersionB = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

    private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string HashC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void Project_AuthoritativeJson_UsesFixedOrderAndAtMostNineSectionGroups()
    {
        object projection = InvokeProjector(AuthoritativeJson());
        string markdown = RequiredString(projection, "ContentMarkdown");

        string[] expectedHeadings =
        [
            "# src/example/Greeter.java",
            "## 文件用途",
            "## 内部单元",
            "## 执行与数据流",
            "## 业务与代码规则",
            "## 数据读写",
            "## 通信证据",
            "## 引用",
            "## 延后观察",
            "## 假设与未决证据",
        ];

        int previous = -1;
        foreach (string heading in expectedHeadings)
        {
            int position = markdown.IndexOf(heading, StringComparison.Ordinal);
            Assert.True(position > previous, $"Missing or out-of-order fixed heading: {heading}");
            previous = position;
        }

        IReadOnlyList<object> sections = RequiredSequence(projection, "Sections");
        Assert.InRange(sections.Count, 1, 9);
        Assert.Equal(
            Enumerable.Range(1, sections.Count),
            sections.Select(section => Convert.ToInt32(RequiredProperty(section, "SectionOrder"))));
        Assert.DoesNotContain(
            sections,
            section => Convert.ToString(RequiredProperty(section, "Heading"))!.Contains("LLM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Project_RepeatedRuns_ProduceByteIdenticalMarkdownSectionsIndexAndCommitFingerprint()
    {
        object first = InvokeProjector(AuthoritativeJson());
        object second = InvokeProjector(AuthoritativeJson());

        Assert.Equal(RequiredString(first, "ContentMarkdown"), RequiredString(second, "ContentMarkdown"));
        Assert.Equal(CanonicalSequence(first, "Sections"), CanonicalSequence(second, "Sections"));
        Assert.Equal(CanonicalObject(first, "IndexEntry"), CanonicalObject(second, "IndexEntry"));
        Assert.Equal(
            CanonicalFingerprint(first, "CommitFingerprint"),
            CanonicalFingerprint(second, "CommitFingerprint"));
    }

    [Fact]
    public void Project_AuthoritativeJson_OrdersAnchorsAndSameRepositoryReferencesCanonically()
    {
        object projection = InvokeProjector(AuthoritativeJson());

        IReadOnlyList<object> anchors = RequiredSequence(projection, "SourceAnchors");
        Assert.Equal(
            ["a001", "a002"],
            anchors.Select(anchor => RequiredString(anchor, "Key")).ToArray());

        IReadOnlyList<object> edges = RequiredSequence(projection, "DirectReferenceEdges", "ReferenceEdges");
        Assert.Equal(2, edges.Count);
        Assert.Equal(
            [TargetDocumentVersionA, TargetDocumentVersionB],
            edges.Select(edge => RequiredGuid(edge, "TargetDocumentVersionId", "TargetId")).ToArray());
        Assert.All(
            edges,
            edge => Assert.Equal("References", Convert.ToString(RequiredProperty(edge, "EdgeType", "RelationshipKind"))));
    }

    [Fact]
    public void Project_DerivesSearchAndMarkdownOnlyFromAuthoritativeJsonClaims()
    {
        object projection = InvokeProjector(AuthoritativeJson());
        string markdown = RequiredString(projection, "ContentMarkdown");
        object index = RequiredProperty(projection, "IndexEntry");
        string searchText = RequiredString(index, "SearchText");

        Assert.Contains("Greeter coordinates the greeting", markdown, StringComparison.Ordinal);
        Assert.Contains("greeting", searchText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not-in-authoritative-json", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("not-in-authoritative-json", searchText, StringComparison.Ordinal);
        Assert.DoesNotContain("independent worker markdown", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("independent worker markdown", searchText, StringComparison.OrdinalIgnoreCase);
    }

    private static object InvokeProjector(string json)
    {
        Type? projector = typeof(CanonicalEnvelope).Assembly.GetType(
            "Vulgata.Core.DomainServices.CodeLogicFileV2Projector",
            throwOnError: false,
            ignoreCase: false);

        Assert.True(
            projector is not null,
            "T027 production seam is missing: CodeLogicFileV2Projector must be added before this RED turns GREEN.");

        MethodInfo? method = projector!.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(candidate => candidate.Name == "Project");
        Assert.True(
            method is not null,
            "T027 must expose one unambiguous public static Project seam for authoritative file-v2 JSON.");

        using JsonDocument document = JsonDocument.Parse(json);
        ParameterInfo[] parameters = method!.GetParameters();
        object?[] arguments = parameters.Select(parameter =>
            CreateProjectArgument(parameter, document, json)).ToArray();

        try
        {
            return method.Invoke(null, arguments) ??
                throw new Xunit.Sdk.XunitException("The projector returned null.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new Xunit.Sdk.XunitException(
                $"The authoritative projection seam rejected the valid fixture: {exception.InnerException.Message}",
                exception.InnerException);
        }
    }

    private static object? CreateProjectArgument(ParameterInfo parameter, JsonDocument document, string json)
    {
        Type type = parameter.ParameterType;
        if (type == typeof(string))
        {
            return json;
        }

        if (type == typeof(JsonDocument))
        {
            return document;
        }

        if (type == typeof(JsonElement))
        {
            return document.RootElement.Clone();
        }

        if (type == typeof(Guid))
        {
            string name = parameter.Name ?? string.Empty;
            return name.Contains("pair", StringComparison.OrdinalIgnoreCase) ? PairPackageId :
                name.Contains("logical", StringComparison.OrdinalIgnoreCase) ? LogicalDocumentId :
                name.Contains("document", StringComparison.OrdinalIgnoreCase) ? DocumentVersionId :
                name.Contains("job", StringComparison.OrdinalIgnoreCase) ? JobId :
                name.Contains("repository", StringComparison.OrdinalIgnoreCase) ? RepositoryId :
                name.Contains("scan", StringComparison.OrdinalIgnoreCase) ? ScanRunId :
                throw new Xunit.Sdk.XunitException($"T027 Project parameter '{name}' needs an explicit identity name.");
        }

        throw new Xunit.Sdk.XunitException(
            $"T027 public Project seam has unsupported parameter '{parameter.Name}' of type '{type.FullName}'.");
    }

    private static object RequiredProperty(object instance, params string[] names)
    {
        foreach (string name in names)
        {
            PropertyInfo? property = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property is not null)
            {
                return property.GetValue(instance) ??
                    throw new Xunit.Sdk.XunitException($"Projection property '{name}' was null.");
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"Projection type '{instance.GetType().FullName}' is missing '{string.Join(" or ", names)}'.");
    }

    private static string RequiredString(object instance, params string[] names) =>
        RequiredProperty(instance, names) as string ??
        throw new Xunit.Sdk.XunitException($"Projection property '{string.Join(" or ", names)}' was not a string.");

    private static Guid RequiredGuid(object instance, params string[] names)
    {
        object value = RequiredProperty(instance, names);
        return value switch
        {
            Guid id => id,
            string text when Guid.TryParse(text, out Guid id) => id,
            _ => throw new Xunit.Sdk.XunitException($"Projection property '{string.Join(" or ", names)}' was not a Guid."),
        };
    }

    private static IReadOnlyList<object> RequiredSequence(object instance, params string[] names)
    {
        object value = RequiredProperty(instance, names);
        if (value is not IEnumerable enumerable)
        {
            throw new Xunit.Sdk.XunitException($"Projection property '{string.Join(" or ", names)}' was not a sequence.");
        }

        return enumerable.Cast<object>().ToArray();
    }

    private static string CanonicalSequence(object instance, string name) =>
        string.Join("\n", RequiredSequence(instance, name).Select(item =>
            string.Join("|", item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => $"{property.Name}={property.GetValue(item)}"))));

    private static string CanonicalObject(object instance, string name) =>
        string.Join("|", RequiredProperty(instance, name).GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}={property.GetValue(RequiredProperty(instance, name))}"));

    private static string CanonicalFingerprint(object instance, string name)
    {
        object value = RequiredProperty(instance, name);
        return value switch
        {
            byte[] bytes => Convert.ToHexString(bytes),
            ReadOnlyMemory<byte> memory => Convert.ToHexString(memory.Span),
            string text => text,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string AuthoritativeJson() => $$"""
        {
          "schemaIdentity":"code-logic/file-v2",
          "binding":{
            "jobId":"{{JobId:D}}","scanExecutionId":"{{ScanExecutionId:D}}","systemId":"{{SystemId:D}}","repositoryId":"{{RepositoryId:D}}","scanRunId":"{{ScanRunId:D}}","profiledRevision":"{{Revision}}","fileCodeUnitId":"{{FileCodeUnitId:D}}","fileCodeUnitIdentityFingerprint":"{{HashA}}","preparedScanManifestId":"{{ManifestId:D}}","preparedScanManifestFingerprint":"{{HashB}}","pairPackageFingerprint":"{{HashC}}","repositoryRelativePath":"src/example/Greeter.java","byteStart":0,"byteEnd":80,"lineStart":1,"lineEnd":7,"segmentOrdinal":1,"segmentCount":1,"fileHash":"{{HashA}}","sourceHash":"{{HashB}}","promptIdentity":"pass1-code-logic-worker.v2","documentSchemaIdentity":"code-logic/file-v2","analysisRuntimeIdentityHash":"{{HashC}}"
          },
          "sourceAnchors":[
            {"key":"a001","repositoryRelativePath":"src/example/Greeter.java","byteStart":0,"byteEnd":40,"lineStart":1,"lineEnd":3,"sourceHash":"{{HashB}}","evidenceLocator":"L1:C1-L3:C1"},
            {"key":"a002","repositoryRelativePath":"src/example/Greeter.java","byteStart":40,"byteEnd":80,"lineStart":4,"lineEnd":7,"sourceHash":"{{HashB}}","evidenceLocator":"L4:C1-L7:C1"}
          ],
          "filePurpose":{"summary":"Greeter coordinates the greeting","sourceAnchorRefs":["a001"],"responsibilities":[{"description":"Coordinates the greeting request","sourceAnchorRefs":["a001"]}]},
          "internalUnits":[{"order":1,"kind":"Type","qualifiedSymbol":"example.Greeter","signature":"class Greeter","responsibility":"Coordinates the greeting","byteStart":0,"byteEnd":80,"lineStart":1,"lineEnd":7,"sourceAnchorRefs":["a001","a002"]}],
          "executionFlow":[{"order":1,"description":"Receives a greeting request","sourceAnchorRefs":["a001"]}],
          "dataFlow":[{"order":1,"from":"request","to":"response","value":"greeting","description":"Copies the greeting value","sourceAnchorRefs":["a002"]}],
          "rules":[{"order":1,"kind":"Business","condition":"When greeting is present","effect":"Return the greeting","sourceAnchorRefs":["a002"]}],
          "dataAccess":[{"order":1,"mode":"Read","subject":"greeting","description":"Reads the greeting value","sourceAnchorRefs":["a002"]}],
          "communicationEvidence":[{"order":1,"communicationKind":"Http","observation":"Calls the greeting endpoint","classificationEvidence":[{"classificationId":"dddddddd-dddd-4ddd-8ddd-dddddddddddd","classificationFingerprint":"{{HashA}}","communicationKind":"Http","level":"Confirmed","confidence":"High","evidenceRule":"HttpControllerAnnotation","provenance":"StructuralAnnotation","catalogRuleIdentity":"http-controller","evidenceLocator":"L4:C1-L4:C8"}],"navigationReceiptIds":[],"sourceAnchorRefs":["a002"]}],
          "knownReferences":[
            {"order":1,"relationshipKind":"References","targetDocumentVersionId":"{{TargetDocumentVersionA:D}}","targetRepositoryRelativePath":"src/example/Formatter.java","targetSourceHash":"{{HashA}}","navigationReceiptId":"eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee","sourceClaimLocator":"Greeter.format","sourceAnchorRefs":["a002"]},
            {"order":2,"relationshipKind":"References","targetDocumentVersionId":"{{TargetDocumentVersionB:D}}","targetRepositoryRelativePath":"src/example/Logger.java","targetSourceHash":"{{HashB}}","navigationReceiptId":"ffffffff-ffff-4fff-8fff-ffffffffffff","sourceClaimLocator":"Greeter.log","sourceAnchorRefs":["a002"]}
          ],
          "deferredObservations":[{"order":1,"relationshipKind":"References","outcome":"Ambiguous","sourceClaimLocator":"Greeter.resolve","targetSymbol":"example.Unknown","targetRepositoryRelativePath":null,"targetSourceHash":null,"navigationOperation":"Definition","navigationDepth":2,"navigationReceiptIds":["eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"],"sourceAnchorRefs":["a001"]}],
          "assumptions":[{"order":1,"status":"Unresolved","description":"The endpoint host is configured externally","sourceAnchorRefs":[]}]
        }
        """;
}
