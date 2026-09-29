using Vulgata.Tests.TestInfrastructure;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T024 RED slice for the authoritative code-logic/file-v2
/// result boundary.  The fixture is an independent literal semantic object;
/// the Worker is not allowed to author server identities, projections, or
/// evidence that is not bound to the prepared packet.
/// </summary>
public sealed class CodeLogicFileV2SchemaTests
{
    private static readonly Guid _jobId =
        Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid _scanExecutionId =
        Guid.Parse("22222222-3333-4444-8555-666666666666");
    private static readonly Guid _systemId =
        Guid.Parse("33333333-4444-4555-8666-777777777777");
    private static readonly Guid _repositoryId =
        Guid.Parse("44444444-5555-4666-8777-888888888888");
    private static readonly Guid _scanRunId =
        Guid.Parse("55555555-6666-4777-8888-999999999999");
    private static readonly Guid _fileCodeUnitId =
        Guid.Parse("66666666-7777-4888-8999-aaaaaaaaaaaa");
    private static readonly Guid _manifestId =
        Guid.Parse("77777777-8888-4999-8aaa-bbbbbbbbbbbb");
    private static readonly Guid _contextId =
        Guid.Parse("88888888-9999-4aaa-8bbb-cccccccccccc");
    private static readonly Guid _classificationId =
        Guid.Parse("99999999-aaaa-4bbb-8ccc-dddddddddddd");
    private static readonly Guid _targetDocumentVersionId =
        Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
    private static readonly Guid _navigationReceiptId =
        Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff");

    private const string _profiledRevision =
        "0123456789abcdef0123456789abcdef01234567";
    private const string _path = "src/neutral/OrderPolicy.java";
    private const string _source = "package neutral;\nclass OrderPolicy { }\n";
    private const string _hash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string _manifestFingerprint =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const string _pairFingerprint =
        "2222222222222222222222222222222222222222222222222222222222222222";
    private const string _runtimeHash =
        "3333333333333333333333333333333333333333333333333333333333333333";
    private const string _classificationFingerprint =
        "4444444444444444444444444444444444444444444444444444444444444444";
    private const string _targetSourceHash =
        "5555555555555555555555555555555555555555555555555555555555555555";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Parse_CompleteBoundedFileV2Result_AcceptsAuthoritativeSemanticShape()
    {
        PreparedPass1ParsedWorkerResult result = Parse(BaseJson());

        Assert.Equal("code-logic/file-v2", result.SchemaIdentity);
        Assert.Equal(BaseJson(), Encoding.UTF8.GetString(result.CanonicalCandidateJson.Span));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("unknown-root-field")]
    [InlineData("duplicate-root-field")]
    [InlineData("unknown-nested-field")]
    [InlineData("unsafe-text")]
    [InlineData("over-byte-bound")]
    [InlineData("over-depth-bound")]
    [InlineData("worker-authored-identity")]
    [InlineData("repository-mismatch")]
    [InlineData("revision-mismatch")]
    [InlineData("path-mismatch")]
    [InlineData("hash-mismatch")]
    [InlineData("invalid-range")]
    [InlineData("unknown-anchor")]
    [InlineData("unanchored-claim")]
    [InlineData("classification-mismatch")]
    [InlineData("forged-navigation-receipt")]
    public void Parse_SemanticOrSecurityMutation_RejectsWithoutAcceptingDocument(
        string mutation)
    {
        byte[] json = Mutate(mutation);

        Assert.Throws<InvalidDataException>(() => Parse(json));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Parse_RejectsWorkerAuthoredProjectionAndIdentityFields()
    {
        JsonObject root = JsonNode.Parse(BaseJson())!.AsObject();
        root["documentVersionId"] = _targetDocumentVersionId.ToString("D");
        root["contentFingerprint"] = _hash;
        root["documentSections"] = new JsonArray();
        root["indexEntry"] = new JsonObject();
        root["edgeId"] = Guid.NewGuid().ToString("D");

        Assert.Throws<InvalidDataException>(() => Parse(root));
    }

    private static PreparedPass1ParsedWorkerResult Parse(string json) =>
        Parse(Encoding.UTF8.GetBytes(json));

    private static PreparedPass1ParsedWorkerResult Parse(byte[] json) =>
        PreparedPass1ResultParser.Parse(CreatePacket(), json);

    private static PreparedPass1ParsedWorkerResult Parse(JsonObject root) =>
        Parse(root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));

    private static byte[] Mutate(string mutation)
    {
        JsonObject root = JsonNode.Parse(BaseJson())!.AsObject();
        switch (mutation)
        {
            case "unknown-root-field":
                root["unknown"] = "must reject";
                return Encoding.UTF8.GetBytes(root.ToJsonString());
            case "duplicate-root-field":
                return Encoding.UTF8.GetBytes(
                    BaseJson().Replace(
                        "\"schemaIdentity\":\"code-logic/file-v2\"",
                        "\"schemaIdentity\":\"code-logic/file-v2\",\"schemaIdentity\":\"code-logic/file-v2\"",
                        StringComparison.Ordinal));
            case "unknown-nested-field":
                root["sourceAnchors"]![0]! ["serverAnchorId"] = "server-owned";
                break;
            case "unsafe-text":
                root["filePurpose"]!["summary"] = "unsafe\u0001text";
                break;
            case "over-byte-bound":
                root["filePurpose"]!["summary"] = new string('界', 22_000);
                break;
            case "over-depth-bound":
                JsonNode value = new JsonObject { ["value"] = "leaf" };
                for (int index = 0; index < 40; index++)
                {
                    value = new JsonObject { ["nested"] = value };
                }

                root["assumptions"] = new JsonArray(value);
                break;
            case "worker-authored-identity":
                root["binding"]!["documentVersionId"] = _targetDocumentVersionId.ToString("D");
                break;
            case "repository-mismatch":
                root["binding"]!["repositoryId"] = Guid.NewGuid().ToString("D");
                break;
            case "revision-mismatch":
                root["binding"]!["profiledRevision"] = new string('a', 40);
                break;
            case "path-mismatch":
                root["binding"]!["repositoryRelativePath"] = "src/other/File.java";
                break;
            case "hash-mismatch":
                root["binding"]!["sourceHash"] = _targetSourceHash;
                break;
            case "invalid-range":
                root["binding"]!["byteEnd"] = 0;
                root["sourceAnchors"]![0]!["byteEnd"] = 0;
                break;
            case "unknown-anchor":
                root["filePurpose"]!["sourceAnchorRefs"] = new JsonArray("a999");
                break;
            case "unanchored-claim":
                root["filePurpose"]!["responsibilities"]![0]!["sourceAnchorRefs"] = new JsonArray();
                break;
            case "classification-mismatch":
                root["communicationEvidence"]![0]!["classificationEvidence"]![0]!["classificationFingerprint"] = _targetSourceHash;
                break;
            case "forged-navigation-receipt":
                root["knownReferences"]![0]!["navigationReceiptId"] = Guid.NewGuid().ToString("D");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static PreparedPass1WorkerPacket CreatePacket()
    {
        byte[] source = Encoding.UTF8.GetBytes(_source);
        PreparedPass1ContextSnapshotProjection context = new(
            _contextId,
            _repositoryId,
            _scanRunId,
            _profiledRevision,
            _hash,
            _hash,
            null,
            null,
            null);
        PreparedPass1CommunicationClassificationProjection classification = new(
            _fileCodeUnitId,
            _classificationId,
            _manifestId,
            _repositoryId,
            _scanRunId,
            _profiledRevision,
            _classificationFingerprint,
            CommunicationKind.Http,
            ClassificationLevel.Confirmed,
            ConfidenceBand.High,
            EvidenceRule.HttpClientAnnotation,
            EvidenceProvenance.StructuralAnnotation,
            "catalog/http-client",
            _path,
            $"{_path}#L2");

        return new(
            Guid.Parse("dddddddd-eeee-4fff-8000-111111111111"),
            _jobId,
            _scanExecutionId,
            _systemId,
            _repositoryId,
            _scanRunId,
            _profiledRevision,
            _fileCodeUnitId,
            _hash,
            _manifestId,
            _manifestFingerprint,
            _pairFingerprint,
            _path,
            0,
            source.Length,
            1,
            3,
            1,
            1,
            _hash,
            _hash,
            "story-12-3-worker-v2",
            "code-logic/file-v2",
            _runtimeHash,
            1,
            1,
            source,
            context,
            [classification]);
    }

    private static string BaseJson()
    {
        int sourceBytes = Encoding.UTF8.GetByteCount(_source);
        return JsonSerializer.Serialize(new
        {
            schemaIdentity = "code-logic/file-v2",
            binding = new
            {
                jobId = _jobId.ToString("D"),
                scanExecutionId = _scanExecutionId.ToString("D"),
                systemId = _systemId.ToString("D"),
                repositoryId = _repositoryId.ToString("D"),
                scanRunId = _scanRunId.ToString("D"),
                profiledRevision = _profiledRevision,
                fileCodeUnitId = _fileCodeUnitId.ToString("D"),
                fileCodeUnitIdentityFingerprint = _hash,
                preparedScanManifestId = _manifestId.ToString("D"),
                preparedScanManifestFingerprint = _manifestFingerprint,
                pairPackageFingerprint = _pairFingerprint,
                repositoryRelativePath = _path,
                byteStart = 0,
                byteEnd = sourceBytes,
                lineStart = 1,
                lineEnd = 3,
                segmentOrdinal = 1,
                segmentCount = 1,
                fileHash = _hash,
                sourceHash = _hash,
                promptIdentity = "story-12-3-worker-v2",
                documentSchemaIdentity = "code-logic/file-v2",
                analysisRuntimeIdentityHash = _runtimeHash,
            },
            sourceAnchors = new[]
            {
                new
                {
                    key = "a001",
                    repositoryRelativePath = _path,
                    byteStart = 0,
                    byteEnd = sourceBytes,
                    lineStart = 1,
                    lineEnd = 3,
                    sourceHash = _hash,
                    evidenceLocator = $"{_path}#L1-L3",
                },
            },
            filePurpose = new
            {
                summary = "Validates the order submission policy.",
                sourceAnchorRefs = new[] { "a001" },
                responsibilities = new[]
                {
                    new
                    {
                        description = "Validates the order policy input.",
                        sourceAnchorRefs = new[] { "a001" },
                    },
                },
            },
            internalUnits = new[]
            {
                new
                {
                    order = 1,
                    kind = "Type",
                    qualifiedSymbol = "neutral.OrderPolicy",
                    signature = "class OrderPolicy",
                    responsibility = "Owns order policy validation.",
                    byteStart = 18,
                    byteEnd = sourceBytes - 2,
                    lineStart = 2,
                    lineEnd = 2,
                    sourceAnchorRefs = new[] { "a001" },
                },
            },
            executionFlow = new[]
            {
                new
                {
                    order = 1,
                    description = "The policy validates the order.",
                    sourceAnchorRefs = new[] { "a001" },
                },
            },
            dataFlow = new[]
            {
                new
                {
                    order = 1,
                    from = "order",
                    to = "policy",
                    value = "order input",
                    description = "Order input flows into policy validation.",
                    sourceAnchorRefs = new[] { "a001" },
                },
            },
            rules = new[]
            {
                new
                {
                    order = 1,
                    kind = "Code",
                    condition = "order is received",
                    effect = "policy validation runs",
                    sourceAnchorRefs = new[] { "a001" },
                },
            },
            dataAccess = new[]
            {
                new
                {
                    order = 1,
                    mode = "Read",
                    subject = "order",
                    description = "Reads the order input.",
                    sourceAnchorRefs = new[] { "a001" },
                },
            },
            communicationEvidence = new[]
            {
                new
                {
                    order = 1,
                    communicationKind = "Http",
                    observation = "The policy is reached through the HTTP client.",
                    classificationEvidence = new[]
                    {
                        new
                        {
                            classificationId = _classificationId.ToString("D"),
                            classificationFingerprint = _classificationFingerprint,
                            communicationKind = "Http",
                            level = "Confirmed",
                            confidence = "High",
                            evidenceRule = "HttpClientAnnotation",
                            provenance = "StructuralAnnotation",
                            catalogRuleIdentity = "catalog/http-client",
                            evidenceLocator = $"{_path}#L2",
                        },
                    },
                    navigationReceiptIds = Array.Empty<Guid>(),
                    sourceAnchorRefs = new[] { "a001" },
                },
            },
            knownReferences = new[]
            {
                new
                {
                    order = 1,
                    relationshipKind = "References",
                    targetDocumentVersionId = _targetDocumentVersionId.ToString("D"),
                    targetRepositoryRelativePath = "src/neutral/Target.java",
                    targetSourceHash = _targetSourceHash,
                    navigationReceiptId = _navigationReceiptId.ToString("D"),
                    sourceClaimLocator = $"{_path}#L2",
                    sourceAnchorRefs = new[] { "a001" },
                },
            },
            deferredObservations = new[]
            {
                new
                {
                    order = 1,
                    relationshipKind = "References",
                    outcome = "Insufficient",
                    sourceClaimLocator = $"{_path}#L2",
                    targetSymbol = "neutral.Target",
                    targetRepositoryRelativePath = "src/neutral/Target.java",
                    targetSourceHash = _targetSourceHash,
                    navigationOperation = "Definition",
                    navigationDepth = 1,
                    navigationReceiptIds = new[] { _navigationReceiptId },
                    sourceAnchorRefs = new[] { "a001" },
                },
            },
            assumptions = new[]
            {
                new
                {
                    order = 1,
                    status = "Unresolved",
                    description = "The target may be supplied by another scan.",
                    sourceAnchorRefs = new[] { "a001" },
                },
            },
        });
    }
}
