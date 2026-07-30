using System.Buffers.Binary;
using System.Diagnostics;
using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CommunicationDetection;
using Vulgata.Infrastructure.Git;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Tests;

public sealed class PreScanCommunicationDetectionTests
{
    [Fact]
    public async Task DetectAsync_DirectPreparedRpcAnnotation_ReturnsConfirmedHighWithoutRetainingSource()
    {
        const string relativePath = "src/LedgerService.java";
        const string source =
            "import org.apache.dubbo.config.annotation.DubboService;\n" +
            "@DubboService\n" +
            "public class LedgerService {}\n";
        using var snapshot = new TemporaryDirectory();
        WriteSnapshotFile(snapshot.Path, relativePath, source);
        RepositoryProfileDraft profile = CreateCompletedProfileDraft(
            relativePath,
            source,
            "java",
            "LedgerService");
        await using var lease = new TestSnapshotLease(
            profile.ScanRun.RepositoryId,
            profile.ScanRun.Id,
            ProfiledRevision,
            snapshot.Path);
        var detector = new CommunicationKindDetector(
            CommunicationEvidenceRuleCatalog.LoadEmbedded());

        PreScanResultDraft result = await detector.DetectAsync(
            profile,
            ProfiledRevision,
            lease,
            CancellationToken.None);

        CommunicationKindClassification classification = Assert.Single(
            result.Classifications,
            candidate => candidate.Rule == EvidenceRule.RpcDirectAnnotation);
        Assert.Equal(CommunicationKind.Rpc, classification.Kind);
        Assert.Equal(ClassificationLevel.Confirmed, classification.Level);
        Assert.Equal(ConfidenceBand.High, classification.Confidence);
        Assert.Equal(EvidenceProvenance.StructuralAnnotation, classification.Provenance);
        Assert.Equal(ConfirmedCommunicationKinds.Rpc, result.ConfirmedCommunicationKinds);
        Assert.DoesNotContain(source.Trim(), classification.Locator, StringComparison.Ordinal);
        Assert.DoesNotContain(snapshot.Path, classification.Locator, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DetectAsync_CommentStringImportAndCodeTokens_DoNotCreateConfirmedEvidence()
    {
        const string relativePath = "src/FakeCommunicationEvidence.java";
        const string source =
            "// @DubboService\n" +
            "String rpc = \"@DubboService\";\n" +
            "import org.apache.dubbo.config.annotation.DubboService; // @DubboService\n" +
            "String controller = \"[ApiController]\";\n" +
            "invoke(\"@FeignClient\");\n";
        using var snapshot = new TemporaryDirectory();
        WriteSnapshotFile(snapshot.Path, relativePath, source);
        RepositoryProfileDraft profile = CreateCompletedProfileDraft(
            relativePath,
            source,
            "java",
            "FakeCommunicationEvidence");
        await using var lease = new TestSnapshotLease(
            profile.ScanRun.RepositoryId,
            profile.ScanRun.Id,
            ProfiledRevision,
            snapshot.Path);
        var detector = new CommunicationKindDetector(
            CommunicationEvidenceRuleCatalog.LoadEmbedded());

        PreScanResultDraft result = await detector.DetectAsync(
            profile,
            ProfiledRevision,
            lease,
            CancellationToken.None);

        Assert.Equal(ConfirmedCommunicationKinds.None, result.ConfirmedCommunicationKinds);
        Assert.DoesNotContain(
            result.Classifications,
            item => item.Level == ClassificationLevel.Confirmed);
    }

    [Fact]
    public async Task DetectAsync_AnnotationLeadingStringArgumentDecoys_DoNotCreateConfirmedEvidence()
    {
        const string relativePath = "src/AnnotationArgumentDecoys.java";
        const string source =
            "@SuppressWarnings(\"@DubboService\")\n" +
            "[Description(\"[ApiController]\")]\n" +
            "public class AnnotationArgumentDecoys {}\n";
        using var snapshot = new TemporaryDirectory();
        WriteSnapshotFile(snapshot.Path, relativePath, source);
        RepositoryProfileDraft profile = CreateCompletedProfileDraft(
            relativePath,
            source,
            "java",
            "AnnotationArgumentDecoys");
        await using var lease = new TestSnapshotLease(
            profile.ScanRun.RepositoryId,
            profile.ScanRun.Id,
            ProfiledRevision,
            snapshot.Path);
        var detector = new CommunicationKindDetector(
            CommunicationEvidenceRuleCatalog.LoadEmbedded());

        PreScanResultDraft result = await detector.DetectAsync(
            profile,
            ProfiledRevision,
            lease,
            CancellationToken.None);

        Assert.Equal(ConfirmedCommunicationKinds.None, result.ConfirmedCommunicationKinds);
        Assert.DoesNotContain(
            result.Classifications,
            item => item.Level == ClassificationLevel.Confirmed);
    }

    [Fact]
    public async Task DetectAsync_ConfigOnlyCommittedArtifacts_ReturnsConfirmedRpcAndHttp()
    {
        using var snapshot = new TemporaryDirectory();
        WriteSnapshotFile(
            snapshot.Path,
            "config/application.properties",
            "dubbo.application.name=ledger\n");
        WriteSnapshotFile(
            snapshot.Path,
            "config/application.yml",
            "server.servlet.context-path: /ledger\n");
        RepositoryProfileDraft profile = CreateCompletedProfileDraft();
        await using var lease = new TestSnapshotLease(
            profile.ScanRun.RepositoryId,
            profile.ScanRun.Id,
            ProfiledRevision,
            snapshot.Path);
        var detector = new CommunicationKindDetector(
            CommunicationEvidenceRuleCatalog.LoadEmbedded());

        PreScanResultDraft result = await detector.DetectAsync(
            profile,
            ProfiledRevision,
            lease,
            CancellationToken.None);

        Assert.Equal(
            ConfirmedCommunicationKinds.Rpc | ConfirmedCommunicationKinds.Http,
            result.ConfirmedCommunicationKinds);
        Assert.Contains(
            result.Classifications,
            item => item is
            {
                Kind: CommunicationKind.Rpc,
                Rule: EvidenceRule.RpcMatchedConfiguration,
                Provenance: EvidenceProvenance.MatchedConfiguration,
                RelativePath: "config/application.properties",
                Level: ClassificationLevel.Confirmed,
                Confidence: ConfidenceBand.High,
            });
        Assert.Contains(
            result.Classifications,
            item => item is
            {
                Kind: CommunicationKind.Http,
                Rule: EvidenceRule.HttpMatchedConfiguration,
                Provenance: EvidenceProvenance.MatchedConfiguration,
                RelativePath: "config/application.yml",
                Level: ClassificationLevel.Confirmed,
                Confidence: ConfidenceBand.High,
            });
    }

    [Fact]
    public async Task DetectAsync_DirectPreparedHttpControllerClientAndConfiguration_ReturnsConfirmedHigh()
    {
        const string relativePath = "src/PreparedHttp.java";
        const string source =
            "@RestController\n" +
            "[ApiController]\n" +
            "[HttpGet(\"ledger/{id}\")]\n" +
            "@FeignClient(name=\"ledger\")\n" +
            "public class PreparedHttp {}\n";
        using var snapshot = new TemporaryDirectory();
        WriteSnapshotFile(snapshot.Path, relativePath, source);
        WriteSnapshotFile(
            snapshot.Path,
            "config/application.properties",
            "server.servlet.context-path=/ledger\n");
        RepositoryProfileDraft profile = CreateCompletedProfileDraft(
            relativePath,
            source,
            "java",
            "PreparedHttp");
        await using var lease = new TestSnapshotLease(
            profile.ScanRun.RepositoryId,
            profile.ScanRun.Id,
            ProfiledRevision,
            snapshot.Path);
        var detector = new CommunicationKindDetector(
            CommunicationEvidenceRuleCatalog.LoadEmbedded());

        PreScanResultDraft result = await detector.DetectAsync(
            profile,
            ProfiledRevision,
            lease,
            CancellationToken.None);

        Assert.Equal(ConfirmedCommunicationKinds.Http, result.ConfirmedCommunicationKinds);
        Assert.Contains(
            result.Classifications,
            item => item.Rule == EvidenceRule.HttpControllerAnnotation &&
                item.Level == ClassificationLevel.Confirmed &&
                item.Confidence == ConfidenceBand.High);
        Assert.Contains(
            result.Classifications,
            item => item.Rule == EvidenceRule.HttpClientAnnotation &&
                item.Level == ClassificationLevel.Confirmed &&
                item.Confidence == ConfidenceBand.High);
        Assert.Contains(
            result.Classifications,
            item => item.Rule == EvidenceRule.HttpMatchedConfiguration &&
                item.Provenance == EvidenceProvenance.MatchedConfiguration &&
                item.Level == ClassificationLevel.Confirmed &&
                item.Confidence == ConfidenceBand.High);
    }

    [Fact]
    public async Task DetectAsync_ImportNamespacePackageAndFrameworkOnlyEvidence_RemainsConservativeHint()
    {
        const string relativePath = "src/CommunicationImports.java";
        const string source =
            "import org.apache.dubbo.config.ReferenceConfig;\n" +
            "namespace System.Net.Http;\n";
        using var snapshot = new TemporaryDirectory();
        WriteSnapshotFile(snapshot.Path, relativePath, source);
        RepositoryProfileDraft profile = CreateCompletedProfileDraft(
            relativePath,
            source,
            "java",
            "CommunicationImports",
            ("spring-boot",
                "pom.xml#xml:dependency[org.springframework.boot:spring-boot-starter-web]"));
        await using var lease = new TestSnapshotLease(
            profile.ScanRun.RepositoryId,
            profile.ScanRun.Id,
            ProfiledRevision,
            snapshot.Path);
        var detector = new CommunicationKindDetector(
            CommunicationEvidenceRuleCatalog.LoadEmbedded());

        PreScanResultDraft result = await detector.DetectAsync(
            profile,
            ProfiledRevision,
            lease,
            CancellationToken.None);

        Assert.Equal(ConfirmedCommunicationKinds.None, result.ConfirmedCommunicationKinds);
        Assert.NotEmpty(result.Classifications);
        Assert.All(
            result.Classifications,
            item =>
            {
                Assert.Equal(ClassificationLevel.Hint, item.Level);
                Assert.Equal(ConfidenceBand.Conservative, item.Confidence);
            });
        Assert.Contains(
            result.Classifications,
            item => item.Kind == CommunicationKind.Rpc &&
                item.Provenance == EvidenceProvenance.Import);
        Assert.Contains(
            result.Classifications,
            item => item.Kind == CommunicationKind.Http &&
                item.Provenance == EvidenceProvenance.Import);
        Assert.Contains(
            result.Classifications,
            item => item.Kind == CommunicationKind.Http &&
                item.Provenance == EvidenceProvenance.PackageManifest);
        Assert.Contains(
            result.Classifications,
            item => item.Kind == CommunicationKind.Http &&
                item.Provenance == EvidenceProvenance.FrameworkFact);
    }

    [Fact]
    public async Task DetectAsync_MalformedUnsafeOversizedContradictoryAndUnsupportedEvidence_ApprovesWarningsOnly()
    {
        RepositoryProfileDraft profile = CreateWarningsOnlyProfileDraft();
        using var snapshot = new TemporaryDirectory();
        await using var lease = new TestSnapshotLease(
            profile.ScanRun.RepositoryId,
            profile.ScanRun.Id,
            ProfiledRevision,
            snapshot.Path);
        var detector = new CommunicationKindDetector(
            CommunicationEvidenceRuleCatalog.LoadEmbedded());

        PreScanResultDraft result = await detector.DetectAsync(
            profile,
            ProfiledRevision,
            lease,
            CancellationToken.None);

        Assert.Equal(PreScanApprovalState.PreScanApproved, result.State);
        Assert.Equal(ConfirmedCommunicationKinds.None, result.ConfirmedCommunicationKinds);
        Assert.Equal(5, result.TotalWarningCount);
        Assert.Equal(
            "预扫描已批准并保留审慎警告——继续进入顺序演示运行器",
            result.ApprovalMessage);
        Assert.All(
            result.Classifications,
            item =>
            {
                Assert.Null(item.Kind);
                Assert.Equal(ClassificationLevel.Warning, item.Level);
                Assert.Equal(ConfidenceBand.None, item.Confidence);
                Assert.Equal(EvidenceProvenance.Detector, item.Provenance);
            });
        Assert.Equal(
            [
                EvidenceRule.MalformedEvidence,
                EvidenceRule.UnsafePath,
                EvidenceRule.BoundExceeded,
                EvidenceRule.ContradictoryEvidence,
                EvidenceRule.UnsupportedFamily,
            ],
            result.Classifications.Select(item => item.Rule).Order().ToArray());
    }

    [Fact]
    public async Task DetectAsync_DuplicateDirectEvidence_CollapsesAndRetainsIndependentHint()
    {
        const string relativePath = "src/DuplicateService.java";
        const string source =
            "import org.apache.dubbo.config.annotation.DubboService;\n" +
            "@DubboService @DubboService\n";
        using var snapshot = new TemporaryDirectory();
        WriteSnapshotFile(snapshot.Path, relativePath, source);
        RepositoryProfileDraft profile = CreateCompletedProfileDraft(
            relativePath,
            source,
            "java",
            "DuplicateService");
        await using var lease = new TestSnapshotLease(
            profile.ScanRun.RepositoryId,
            profile.ScanRun.Id,
            ProfiledRevision,
            snapshot.Path);
        var detector = new CommunicationKindDetector(
            CommunicationEvidenceRuleCatalog.LoadEmbedded());

        PreScanResultDraft result = await detector.DetectAsync(
            profile,
            ProfiledRevision,
            lease,
            CancellationToken.None);

        CommunicationKindClassification direct = Assert.Single(
            result.Classifications,
            item => item.Rule == EvidenceRule.RpcDirectAnnotation);
        Assert.Equal(2, direct.OccurrenceCount);
        CommunicationKindClassification hint = Assert.Single(
            result.Classifications,
            item => item.Rule == EvidenceRule.RpcImportOnly);
        Assert.Equal(ClassificationLevel.Hint, hint.Level);
        Assert.Equal(ConfidenceBand.Conservative, hint.Confidence);
        Assert.Equal(2, result.TotalConfirmedEvidenceCount);
        Assert.Equal(1, result.TotalHintCount);
    }

    [Fact]
    public async Task DetectAsync_OverOneHundredBothKindsAndPermutation_RetainsAuthoritativeDeterministicSample()
    {
        using var snapshot = new TemporaryDirectory();
        (RepositoryProfileDraft forward, RepositoryProfileDraft reverse,
            ExpectedClassification[] expected) =
            CreateAdversarialSamplingProfiles(snapshot.Path);
        var detector = new CommunicationKindDetector(
            CommunicationEvidenceRuleCatalog.LoadEmbedded());
        await using var forwardLease = new TestSnapshotLease(
            forward.ScanRun.RepositoryId,
            forward.ScanRun.Id,
            ProfiledRevision,
            snapshot.Path);

        PreScanResultDraft forwardResult = await detector.DetectAsync(
            forward,
            ProfiledRevision,
            forwardLease,
            CancellationToken.None);

        await using var reverseLease = new TestSnapshotLease(
            reverse.ScanRun.RepositoryId,
            reverse.ScanRun.Id,
            ProfiledRevision,
            snapshot.Path);
        PreScanResultDraft reverseResult = await detector.DetectAsync(
            reverse,
            ProfiledRevision,
            reverseLease,
            CancellationToken.None);

        Assert.Equal(
            ConfirmedCommunicationKinds.Rpc | ConfirmedCommunicationKinds.Http,
            forwardResult.ConfirmedCommunicationKinds);
        Assert.Equal(105, forwardResult.TotalConfirmedEvidenceCount);
        Assert.Equal(2, forwardResult.TotalHintCount);
        Assert.Equal(2, forwardResult.TotalWarningCount);
        Assert.True(forwardResult.EvidenceTruncated);
        Assert.Equal(100, forwardResult.Classifications.Count);
        Assert.Contains(
            forwardResult.Classifications,
            item => item is
            {
                Kind: CommunicationKind.Rpc,
                Level: ClassificationLevel.Confirmed,
            });
        Assert.Contains(
            forwardResult.Classifications,
            item => item is
            {
                Kind: CommunicationKind.Http,
                Level: ClassificationLevel.Confirmed,
            });
        Assert.Contains(
            forwardResult.Classifications,
            item => item is
            {
                Level: ClassificationLevel.Warning,
                Rule: EvidenceRule.BoundExceeded,
            });
        Assert.Contains(
            forwardResult.Classifications,
            item => item.OccurrenceCount == 2);
        Assert.Equal(
            ComputeExpectedAggregateFingerprint(expected),
            forwardResult.Fingerprint);
        Assert.Equal(
            forwardResult.ConfirmedCommunicationKinds,
            reverseResult.ConfirmedCommunicationKinds);
        Assert.Equal(
            forwardResult.TotalConfirmedEvidenceCount,
            reverseResult.TotalConfirmedEvidenceCount);
        Assert.Equal(forwardResult.TotalHintCount, reverseResult.TotalHintCount);
        Assert.Equal(forwardResult.TotalWarningCount, reverseResult.TotalWarningCount);
        Assert.Equal(forwardResult.EvidenceTruncated, reverseResult.EvidenceTruncated);
        Assert.Equal(forwardResult.Fingerprint, reverseResult.Fingerprint);
        Assert.Equal(
            ProjectClassifications(forwardResult),
            ProjectClassifications(reverseResult));
    }

    [Fact]
    public async Task DetectAsync_ZeroEvidence_StillApproves()
    {
        RepositoryProfileDraft profile = CreateCompletedProfileDraft();
        using var snapshot = new TemporaryDirectory();
        await using var lease = new TestSnapshotLease(
            profile.ScanRun.RepositoryId,
            profile.ScanRun.Id,
            ProfiledRevision,
            snapshot.Path);
        var detector = new CommunicationKindDetector(
            CommunicationEvidenceRuleCatalog.LoadEmbedded());

        PreScanResultDraft result = await detector.DetectAsync(
            profile,
            ProfiledRevision,
            lease,
            CancellationToken.None);

        Assert.Equal(PreScanApprovalState.PreScanApproved, result.State);
        Assert.Equal(ConfirmedCommunicationKinds.None, result.ConfirmedCommunicationKinds);
        Assert.Empty(result.Classifications);
        Assert.Equal(0, result.TotalConfirmedEvidenceCount);
        Assert.Equal(0, result.TotalHintCount);
        Assert.Equal(0, result.TotalWarningCount);
    }

    [Fact]
    public async Task DetectAsync_TruncatedAndDeferredFamilySource_OmitsUnsupportedBehavior()
    {
        const string relativePath = "src/DeferredFamilies.java";
        const string source =
            "@DubboServ\n" +
            "@KafkaListener(topics=\"payments\")\n" +
            "openFile(\"shared.dat\");\n" +
            "navigate(\"app://ledger\");\n";
        using var snapshot = new TemporaryDirectory();
        WriteSnapshotFile(snapshot.Path, relativePath, source);
        RepositoryProfileDraft profile = CreateCompletedProfileDraft(
            relativePath,
            source,
            "java",
            "DeferredFamilies");
        await using var lease = new TestSnapshotLease(
            profile.ScanRun.RepositoryId,
            profile.ScanRun.Id,
            ProfiledRevision,
            snapshot.Path);
        var detector = new CommunicationKindDetector(
            CommunicationEvidenceRuleCatalog.LoadEmbedded());

        PreScanResultDraft result = await detector.DetectAsync(
            profile,
            ProfiledRevision,
            lease,
            CancellationToken.None);

        Assert.Equal(PreScanApprovalState.PreScanApproved, result.State);
        Assert.Equal(ConfirmedCommunicationKinds.None, result.ConfirmedCommunicationKinds);
        Assert.Empty(result.Classifications);
        Assert.DoesNotContain(
            result.Classifications,
            item => item.Rule is EvidenceRule.RpcDirectAnnotation or
                EvidenceRule.HttpControllerAnnotation or
                EvidenceRule.HttpClientAnnotation);
    }

    [Fact]
    public void CommunicationEvidenceRuleCatalog_EmbeddedVocabulary_IsVersionedAndClosed()
    {
        CommunicationEvidenceRuleCatalog catalog =
            CommunicationEvidenceRuleCatalog.LoadEmbedded();

        Assert.Equal("1.0", catalog.Version);
        Assert.Equal(13, catalog.Rules.Count);
        Assert.Equal(13, catalog.Rules.Select(rule => rule.Id).Distinct().Count());
        Assert.All(
            catalog.Rules,
            rule =>
            {
                Assert.True(Enum.IsDefined(rule.Kind));
                Assert.True(Enum.IsDefined(rule.Category));
                Assert.Contains(
                    rule.Disposition,
                    new[] { ClassificationLevel.Confirmed, ClassificationLevel.Hint });
                Assert.NotEmpty(rule.Tokens.Concat(rule.Keys));
            });
    }

    [Theory]
    [InlineData("\"version\": \"1.0\"", "\"version\": \"2.0\"")]
    [InlineData("\"kind\": \"Rpc\"", "\"kind\": \"Mq\"")]
    [InlineData("\"category\": \"DirectAnnotation\"", "\"category\": \"UserRegex\"")]
    [InlineData(
        "\"id\": \"rpc.dubbo.service-annotation\"",
        "\"id\": \"user.supplied.rule\"")]
    [InlineData("\"@DubboService\"", "\".*\"")]
    public void CommunicationEvidenceRuleCatalog_UnsafeOrUnknownVocabulary_IsRejected(
        string accepted,
        string rejected)
    {
        string json = ReadEmbeddedCommunicationCatalogJson()
            .Replace(accepted, rejected, StringComparison.Ordinal);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);

        Assert.Throws<InvalidDataException>(
            () => CommunicationEvidenceRuleCatalog.Load(stream));
    }

    [Fact]
    public void CommunicationEvidenceRuleCatalog_OversizedNonSeekableInput_StopsAfterOverflowProbe()
    {
        using var stream = new CountingNonSeekableStream(new byte[256 * 1024]);

        Assert.Throws<InvalidDataException>(
            () => CommunicationEvidenceRuleCatalog.Load(stream));
        Assert.InRange(stream.BytesRead, 64 * 1024 + 1, 64 * 1024 + 1);
    }

    [Fact]
    public void CommunicationKindDetector_PublicBoundary_HasNoExternalOrDispatchDependency()
    {
        Type detector = typeof(CommunicationKindDetector);

        Assert.Equal(
            [typeof(IPreScanCommunicationDetector)],
            detector.GetInterfaces());
        Assert.All(
            detector.GetConstructors().SelectMany(constructor => constructor.GetParameters()),
            parameter => Assert.Equal(
                typeof(CommunicationEvidenceRuleCatalog),
                parameter.ParameterType));
        Assert.DoesNotContain(
            detector.GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic),
            field => field.FieldType.Name.Contains("Http", StringComparison.OrdinalIgnoreCase) ||
                field.FieldType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase) ||
                field.FieldType.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                field.FieldType.Name.Contains("Llm", StringComparison.OrdinalIgnoreCase) ||
                field.FieldType.Name.Contains("Agent", StringComparison.OrdinalIgnoreCase) ||
                field.FieldType.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DomainLifecycle_CompletedProfileAndApprovedResult_ComposesOneIdentityBoundCommitDraft()
    {
        RepositoryProfileDraft profile = CreateCompletedProfileDraft();
        CommunicationKindClassification classification = CreateClassification(
            profile.ScanRun.Id,
            CommunicationKind.Rpc,
            ClassificationLevel.Confirmed,
            ConfidenceBand.High,
            EvidenceRule.RpcDirectAnnotation,
            EvidenceProvenance.StructuralAnnotation);
        PreScanResult result = CreateApprovedResult(profile, classification);
        PreScanResultDraft resultDraft = PreScanResultDraft.Create(result);

        RepositoryProfileCommitDraft commit =
            RepositoryProfileCommitDraft.Create(profile, resultDraft);

        Assert.Same(profile, commit.Profile);
        Assert.Same(resultDraft, commit.PreScanResult);
        Assert.Equal(profile.ScanRun.RepositoryId, commit.PreScanResult.RepositoryId);
        Assert.Equal(profile.ScanRun.Id, commit.PreScanResult.ScanRunId);
        Assert.Equal(ProfiledRevision, commit.PreScanResult.ProfiledRevision);
        Assert.Equal(PreScanApprovalState.PreScanApproved, commit.PreScanResult.State);
    }

    [Fact]
    public void DomainLifecycle_RepositoryProfileDraft_RemainsProfileOnly()
    {
        Type profileType = typeof(RepositoryProfileDraft);
        Type[] forbiddenTypes =
        [
            typeof(IPreScanCommunicationDetector),
            typeof(PreScanResult),
            typeof(PreScanResultDraft),
            typeof(RepositoryProfileCommitDraft),
        ];

        Assert.DoesNotContain(
            profileType.GetProperties(),
            property => forbiddenTypes.Contains(property.PropertyType));
        Assert.DoesNotContain(
            profileType.GetMethod(nameof(RepositoryProfileDraft.Create))!.GetParameters(),
            parameter => forbiddenTypes.Contains(parameter.ParameterType));
    }

    [Fact]
    public void DomainLifecycle_DetectorContract_ConsumesProfileRevisionAndPrivateSnapshot()
    {
        var method = typeof(IPreScanCommunicationDetector).GetMethod(
            nameof(IPreScanCommunicationDetector.DetectAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PreScanResultDraft>), method.ReturnType);
        Assert.Equal(
            [
                typeof(RepositoryProfileDraft),
                typeof(string),
                typeof(ICommittedRepositorySnapshotLease),
                typeof(CancellationToken),
            ],
            method.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void ClosedClassificationVocabulary_ContainsOnlyAcceptedValues()
    {
        Assert.Equal([PreScanApprovalState.PreScanApproved], Enum.GetValues<PreScanApprovalState>());
        Assert.Equal([CommunicationKind.Rpc, CommunicationKind.Http], Enum.GetValues<CommunicationKind>());
        Assert.Equal(
            [ClassificationLevel.Confirmed, ClassificationLevel.Hint, ClassificationLevel.Warning],
            Enum.GetValues<ClassificationLevel>());
        Assert.Equal(
            [ConfidenceBand.High, ConfidenceBand.Conservative, ConfidenceBand.None],
            Enum.GetValues<ConfidenceBand>());
        Assert.Equal(
            [
                EvidenceProvenance.StructuralAnnotation,
                EvidenceProvenance.FrameworkFact,
                EvidenceProvenance.PackageManifest,
                EvidenceProvenance.MatchedConfiguration,
                EvidenceProvenance.Import,
                EvidenceProvenance.Detector,
            ],
            Enum.GetValues<EvidenceProvenance>());
        Assert.Equal(
            [
                EvidenceRule.RpcDirectAnnotation,
                EvidenceRule.RpcMatchedConfiguration,
                EvidenceRule.HttpControllerAnnotation,
                EvidenceRule.HttpClientAnnotation,
                EvidenceRule.HttpMatchedConfiguration,
                EvidenceRule.RpcImportOnly,
                EvidenceRule.HttpImportOnly,
                EvidenceRule.MalformedEvidence,
                EvidenceRule.UnsafePath,
                EvidenceRule.BoundExceeded,
                EvidenceRule.ContradictoryEvidence,
                EvidenceRule.UnsupportedFamily,
            ],
            Enum.GetValues<EvidenceRule>());
    }

    [Theory]
    [InlineData(ClassificationLevel.Confirmed, ConfidenceBand.Conservative)]
    [InlineData(ClassificationLevel.Confirmed, ConfidenceBand.None)]
    [InlineData(ClassificationLevel.Hint, ConfidenceBand.High)]
    [InlineData(ClassificationLevel.Hint, ConfidenceBand.None)]
    [InlineData(ClassificationLevel.Warning, ConfidenceBand.High)]
    [InlineData(ClassificationLevel.Warning, ConfidenceBand.Conservative)]
    public void Classification_Create_WithLevelConfidenceMismatch_Rejects(
        ClassificationLevel level,
        ConfidenceBand confidence)
    {
        Assert.Throws<ArgumentException>(() =>
            CreateClassification(
                Guid.NewGuid(),
                level == ClassificationLevel.Warning ? null : CommunicationKind.Rpc,
                level,
                confidence,
                level switch
                {
                    ClassificationLevel.Confirmed => EvidenceRule.RpcDirectAnnotation,
                    ClassificationLevel.Hint => EvidenceRule.RpcImportOnly,
                    _ => EvidenceRule.MalformedEvidence,
                },
                level switch
                {
                    ClassificationLevel.Confirmed => EvidenceProvenance.StructuralAnnotation,
                    ClassificationLevel.Hint => EvidenceProvenance.Import,
                    _ => EvidenceProvenance.Detector,
                }));
    }

    [Fact]
    public void Classification_Create_WithRuleKindOrProvenanceMismatch_Rejects()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateClassification(
                Guid.NewGuid(),
                CommunicationKind.Http,
                ClassificationLevel.Confirmed,
                ConfidenceBand.High,
                EvidenceRule.RpcDirectAnnotation,
                EvidenceProvenance.StructuralAnnotation));
        Assert.Throws<ArgumentException>(() =>
            CreateClassification(
                Guid.NewGuid(),
                CommunicationKind.Rpc,
                ClassificationLevel.Hint,
                ConfidenceBand.Conservative,
                EvidenceRule.RpcImportOnly,
                EvidenceProvenance.StructuralAnnotation));
        Assert.Throws<ArgumentException>(() =>
            CreateClassification(
                Guid.NewGuid(),
                CommunicationKind.Rpc,
                ClassificationLevel.Warning,
                ConfidenceBand.None,
                EvidenceRule.UnsupportedFamily,
                EvidenceProvenance.Detector));
    }

    [Fact]
    public void Classification_Create_WithCompatibleClosedTuples_Accepts()
    {
        Guid resultId = Guid.NewGuid();

        CommunicationKindClassification confirmed = CreateClassification(
            resultId,
            CommunicationKind.Http,
            ClassificationLevel.Confirmed,
            ConfidenceBand.High,
            EvidenceRule.HttpMatchedConfiguration,
            EvidenceProvenance.MatchedConfiguration);
        CommunicationKindClassification hint = CreateClassification(
            resultId,
            CommunicationKind.Http,
            ClassificationLevel.Hint,
            ConfidenceBand.Conservative,
            EvidenceRule.HttpImportOnly,
            EvidenceProvenance.PackageManifest);
        CommunicationKindClassification warning = CreateClassification(
            resultId,
            null,
            ClassificationLevel.Warning,
            ConfidenceBand.None,
            EvidenceRule.UnsupportedFamily,
            EvidenceProvenance.Detector);

        Assert.Equal(ClassificationLevel.Confirmed, confirmed.Level);
        Assert.Equal(ClassificationLevel.Hint, hint.Level);
        Assert.Equal(ClassificationLevel.Warning, warning.Level);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/rooted.cs")]
    [InlineData("C:/rooted.cs")]
    [InlineData("../escaping.cs")]
    [InlineData("src/../escaping.cs")]
    [InlineData("src/\u0001control.cs")]
    public void Classification_Create_WithUnsafeRelativePath_Rejects(string relativePath)
    {
        Assert.Throws<ArgumentException>(() =>
            CommunicationKindClassification.Create(
                Guid.NewGuid(),
                CommunicationKind.Rpc,
                ClassificationLevel.Confirmed,
                ConfidenceBand.High,
                EvidenceRule.RpcDirectAnnotation,
                EvidenceProvenance.StructuralAnnotation,
                relativePath,
                $"{relativePath}#L1",
                1,
                Fingerprint));
    }

    [Fact]
    public void Classification_Create_WithOutOfBoundsOrUnboundLocator_Rejects()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateClassification(Guid.NewGuid(), locator: "src/Other.cs#L1"));
        Assert.Throws<ArgumentException>(() =>
            CreateClassification(Guid.NewGuid(), relativePath: new string('a', 2_049)));
        Assert.Throws<ArgumentException>(() =>
            CreateClassification(
                Guid.NewGuid(),
                locator: $"src/Service.cs#{new string('x', 4_096)}"));
    }

    [Fact]
    public void PreScanResult_Create_WithIdentityRevisionOrConfirmedFlagMismatch_Rejects()
    {
        RepositoryProfileDraft profile = CreateCompletedProfileDraft();
        CommunicationKindClassification wrongOwner = CreateClassification(
            Guid.NewGuid(),
            CommunicationKind.Rpc,
            ClassificationLevel.Confirmed,
            ConfidenceBand.High,
            EvidenceRule.RpcDirectAnnotation,
            EvidenceProvenance.StructuralAnnotation);
        Assert.Throws<ArgumentException>(() => CreateApprovedResult(profile, wrongOwner));

        Assert.Throws<ArgumentException>(() =>
            CreateApprovedResult(
                profile,
                CreateClassification(
                    profile.ScanRun.Id,
                    CommunicationKind.Rpc,
                    ClassificationLevel.Confirmed,
                    ConfidenceBand.High,
                    EvidenceRule.RpcDirectAnnotation,
                    EvidenceProvenance.StructuralAnnotation),
                profiledRevision: ProfiledRevision.ToUpperInvariant()));

        CommunicationKindClassification rpc = CreateClassification(
            profile.ScanRun.Id,
            CommunicationKind.Rpc,
            ClassificationLevel.Confirmed,
            ConfidenceBand.High,
            EvidenceRule.RpcDirectAnnotation,
            EvidenceProvenance.StructuralAnnotation);
        Assert.Throws<ArgumentException>(() =>
            CreateApprovedResult(
                profile,
                rpc,
                confirmedKinds: ConfirmedCommunicationKinds.Http));
    }

    [Fact]
    public void RepositoryProfileCommitDraft_Create_WithDifferentRepositoryOrScanRun_Rejects()
    {
        RepositoryProfileDraft profile = CreateCompletedProfileDraft();
        RepositoryProfileDraft otherProfile = CreateCompletedProfileDraft();
        PreScanResult result = CreateApprovedResult(
            otherProfile,
            CreateClassification(
                otherProfile.ScanRun.Id,
                CommunicationKind.Rpc,
                ClassificationLevel.Confirmed,
                ConfidenceBand.High,
                EvidenceRule.RpcDirectAnnotation,
                EvidenceProvenance.StructuralAnnotation));

        Assert.Throws<InvalidOperationException>(() =>
            RepositoryProfileCommitDraft.Create(profile, PreScanResultDraft.Create(result)));
    }

    [Fact]
    public async Task AcquireAsync_DirtyReusableCheckout_RejectsWithoutProfiledRevision()
    {
        using var source = new TemporaryGitRepository();
        source.Write("src/version.txt", "committed");
        source.CommitAll();
        using var storage = new TemporaryDirectory();
        RepositoryEntity repository = CreateRepository(source.Path);
        var service = new GitWorkingCopyService(new GitWorkingCopyOptions(storage.Path));

        GitWorkingCopyResult initial =
            await service.AcquireAsync(repository, Guid.NewGuid(), CancellationToken.None);
        Assert.True(initial.Succeeded);
        AssertProfiledRevision(initial.ProfiledRevision);
        File.WriteAllText(
            System.IO.Path.Combine(initial.WorkingCopyPath!, "src", "version.txt"),
            "dirty",
            Encoding.UTF8);

        GitWorkingCopyResult rejected =
            await service.AcquireAsync(repository, Guid.NewGuid(), CancellationToken.None);

        Assert.False(rejected.Succeeded);
        Assert.Null(rejected.WorkingCopyPath);
        Assert.Null(rejected.ProfiledRevision);
    }

    [Fact]
    public async Task AcquireAsync_CleanCheckout_CapturesExactLowercaseProfiledRevision()
    {
        using var source = new TemporaryGitRepository();
        source.Write("src/version.txt", "committed");
        string expectedRevision = source.CommitAll();
        using var storage = new TemporaryDirectory();
        RepositoryEntity repository = CreateRepository(source.Path);
        var service = new GitWorkingCopyService(new GitWorkingCopyOptions(storage.Path));

        GitWorkingCopyResult result =
            await service.AcquireAsync(repository, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(expectedRevision, result.ProfiledRevision);
        AssertProfiledRevision(result.ProfiledRevision);
    }

    [Fact]
    public async Task AcquireAsync_ExactRevision_MaterializesPrivateGitObjectSnapshot()
    {
        using var source = new TemporaryGitRepository();
        source.Write("src/version.txt", "committed-v1");
        source.CommitAll();
        using var storage = new TemporaryDirectory();
        RepositoryEntity repository = CreateRepository(source.Path);
        var workingCopyService = new GitWorkingCopyService(new GitWorkingCopyOptions(storage.Path));
        GitWorkingCopyResult workingCopy =
            await workingCopyService.AcquireAsync(
                repository,
                Guid.NewGuid(),
                CancellationToken.None);
        var snapshotService =
            new CommittedRepositorySnapshotService(new GitWorkingCopyOptions(storage.Path));
        Guid scanRunId = Guid.NewGuid();

        ICommittedRepositorySnapshotLease lease = await snapshotService.AcquireAsync(
            repository,
            scanRunId,
            workingCopy,
            CancellationToken.None);
        string snapshotRoot = lease.RootPath;

        Assert.NotEqual(workingCopy.WorkingCopyPath, snapshotRoot);
        Assert.Equal(repository.Id, lease.RepositoryId);
        Assert.Equal(scanRunId, lease.ScanRunId);
        Assert.Equal(workingCopy.ProfiledRevision, lease.ProfiledRevision);
        Assert.Equal(
            "committed-v1",
            await File.ReadAllTextAsync(
                System.IO.Path.Combine(snapshotRoot, "src", "version.txt"),
                CancellationToken.None));

        await lease.DisposeAsync();
        Assert.False(Directory.Exists(snapshotRoot));
    }

    [Fact]
    public async Task AcquireAsync_UnsafeArchiveEntries_RejectsAndPublishesNoLease()
    {
        await AssertRejectedArchiveAsync(CreateTar(("C:/absolute.txt", TarEntryType.RegularFile, "x")));
        await AssertRejectedArchiveAsync(CreateTar(("../../escape.txt", TarEntryType.RegularFile, "x")));
        await AssertRejectedArchiveAsync(CreateTar(("bad\nname.txt", TarEntryType.RegularFile, "x")));
        await AssertRejectedArchiveAsync(CreateTar(("link.txt", TarEntryType.SymbolicLink, "target")));
        await AssertRejectedArchiveAsync(CreateTar(("reparse.txt", TarEntryType.HardLink, "target")));
        await AssertRejectedArchiveAsync(
            CreateTar(
                ("duplicate.txt", TarEntryType.RegularFile, "first"),
                ("duplicate.txt", TarEntryType.RegularFile, "second")));
    }

    [Fact]
    public async Task AcquireAsync_PerEntryBoundExceeded_RejectsAndCleansPartialSnapshot()
    {
        await AssertRejectedArchiveAsync(
            CreateTar(("large.bin", TarEntryType.RegularFile, "12345")),
            new CommittedRepositorySnapshotLimits(
                MaximumEntryBytes: 4,
                MaximumAggregateBytes: 64));
    }

    [Fact]
    public async Task AcquireAsync_AggregateBoundExceeded_RejectsAndCleansPartialSnapshot()
    {
        await AssertRejectedArchiveAsync(
            CreateTar(
                ("one.bin", TarEntryType.RegularFile, "1234"),
                ("two.bin", TarEntryType.RegularFile, "5678")),
            new CommittedRepositorySnapshotLimits(
                MaximumEntryBytes: 7,
                MaximumAggregateBytes: 7));
    }

    [Fact]
    public async Task AcquireAsync_MalformedArchive_RejectsAndCleansPartialSnapshot()
    {
        await AssertRejectedArchiveAsync(
            new MemoryStream(Encoding.ASCII.GetBytes("not a tar archive"), writable: false));
    }

    [Fact]
    public async Task AcquireAsync_CancelledBeforeAcquisition_CancelsWithoutSnapshot()
    {
        using var source = new TemporaryGitRepository();
        source.Write("tracked.txt", "tracked");
        source.CommitAll();
        using var storage = new TemporaryDirectory();
        RepositoryEntity repository = CreateRepository(source.Path);
        GitWorkingCopyResult workingCopy = await new GitWorkingCopyService(
                new GitWorkingCopyOptions(storage.Path))
            .AcquireAsync(repository, Guid.NewGuid(), CancellationToken.None);
        var service =
            new CommittedRepositorySnapshotService(new GitWorkingCopyOptions(storage.Path));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AcquireAsync(repository, Guid.NewGuid(), workingCopy, cancellation.Token));

        AssertNoSnapshots(storage.Path);
    }

    [Fact]
    public async Task AcquireAsync_DirtyAfterWorkingCopyAcquisition_RejectsAndCleansSnapshot()
    {
        using var source = new TemporaryGitRepository();
        source.Write("tracked.txt", "tracked");
        source.CommitAll();
        using var storage = new TemporaryDirectory();
        RepositoryEntity repository = CreateRepository(source.Path);
        GitWorkingCopyResult workingCopy = await new GitWorkingCopyService(
                new GitWorkingCopyOptions(storage.Path))
            .AcquireAsync(repository, Guid.NewGuid(), CancellationToken.None);
        File.WriteAllText(
            System.IO.Path.Combine(workingCopy.WorkingCopyPath!, "tracked.txt"),
            "dirty",
            Encoding.UTF8);
        var service =
            new CommittedRepositorySnapshotService(new GitWorkingCopyOptions(storage.Path));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AcquireAsync(
                repository,
                Guid.NewGuid(),
                workingCopy,
                CancellationToken.None));

        AssertNoSnapshots(storage.Path);
    }

    [Fact]
    public async Task AcquireAsync_CancelledDuringMaterialization_CleansPartialSnapshot()
    {
        using var source = new TemporaryGitRepository();
        source.Write("tracked.txt", "tracked");
        source.CommitAll();
        using var storage = new TemporaryDirectory();
        RepositoryEntity repository = CreateRepository(source.Path);
        GitWorkingCopyResult workingCopy = await new GitWorkingCopyService(
                new GitWorkingCopyOptions(storage.Path))
            .AcquireAsync(repository, Guid.NewGuid(), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var service = new CommittedRepositorySnapshotService(
            new GitWorkingCopyOptions(storage.Path),
            new CancellingArchiveSource(cancellation),
            CommittedRepositorySnapshotLimits.Production);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AcquireAsync(
                repository,
                Guid.NewGuid(),
                workingCopy,
                cancellation.Token));

        AssertNoSnapshots(storage.Path);
    }

    [Fact]
    public async Task AcquireAsync_SameRepository_HoldsAcquisitionLockUntilLeaseDisposal()
    {
        using var source = new TemporaryGitRepository();
        source.Write("tracked.txt", "tracked");
        source.CommitAll();
        using var storage = new TemporaryDirectory();
        RepositoryEntity repository = CreateRepository(source.Path);
        GitWorkingCopyResult workingCopy = await new GitWorkingCopyService(
                new GitWorkingCopyOptions(storage.Path))
            .AcquireAsync(repository, Guid.NewGuid(), CancellationToken.None);
        var archiveSource = new CountingArchiveSource(
            () => CreateTar(("tracked.txt", TarEntryType.RegularFile, "tracked")));
        var service = new CommittedRepositorySnapshotService(
            new GitWorkingCopyOptions(storage.Path),
            archiveSource,
            CommittedRepositorySnapshotLimits.Production);
        ICommittedRepositorySnapshotLease first = await service.AcquireAsync(
            repository,
            Guid.NewGuid(),
            workingCopy,
            CancellationToken.None);

        Task<ICommittedRepositorySnapshotLease> waiting = service.AcquireAsync(
            repository,
            Guid.NewGuid(),
            workingCopy,
            CancellationToken.None);
        await Task.Delay(100, CancellationToken.None);
        Assert.Equal(1, archiveSource.OpenCount);
        Assert.False(waiting.IsCompleted);

        await first.DisposeAsync();
        await using ICommittedRepositorySnapshotLease second = await waiting;
        Assert.Equal(2, archiveSource.OpenCount);
    }

    [Fact]
    public async Task AcquireAsync_CleanupFailure_PreservesPrimaryFailureAndReleasesAcquisitionLock()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var source = new TemporaryGitRepository();
        source.Write("tracked.txt", "tracked");
        source.CommitAll();
        using var storage = new TemporaryDirectory();
        RepositoryEntity repository = CreateRepository(source.Path);
        GitWorkingCopyResult workingCopy = await new GitWorkingCopyService(
                new GitWorkingCopyOptions(storage.Path))
            .AcquireAsync(repository, Guid.NewGuid(), CancellationToken.None);
        using var archiveSource = new CleanupBlockingArchiveSource(storage.Path);
        var service = new CommittedRepositorySnapshotService(
            new GitWorkingCopyOptions(storage.Path),
            archiveSource,
            CommittedRepositorySnapshotLimits.Production);

        Exception? acquisitionFailure = await Record.ExceptionAsync(
            () => service.AcquireAsync(
                repository,
                Guid.NewGuid(),
                workingCopy,
                CancellationToken.None));
        Assert.True(archiveSource.CleanupWasBlocked);
        archiveSource.ReleaseCleanupBlock();

        using var retryCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        ICommittedRepositorySnapshotLease? retryLease = null;
        Exception? retryFailure = await Record.ExceptionAsync(async () =>
        {
            retryLease = await service.AcquireAsync(
                repository,
                Guid.NewGuid(),
                workingCopy,
                retryCancellation.Token);
        });
        if (retryLease is not null)
        {
            await retryLease.DisposeAsync();
        }

        Assert.IsType<InvalidDataException>(acquisitionFailure);
        Assert.Null(retryFailure);
    }

    private static async Task AssertRejectedArchiveAsync(
        Stream archive,
        CommittedRepositorySnapshotLimits? limits = null)
    {
        using (archive)
        using (var source = new TemporaryGitRepository())
        using (var storage = new TemporaryDirectory())
        {
            source.Write("tracked.txt", "tracked");
            source.CommitAll();
            RepositoryEntity repository = CreateRepository(source.Path);
            GitWorkingCopyResult workingCopy = await new GitWorkingCopyService(
                    new GitWorkingCopyOptions(storage.Path))
                .AcquireAsync(repository, Guid.NewGuid(), CancellationToken.None);
            var service = new CommittedRepositorySnapshotService(
                new GitWorkingCopyOptions(storage.Path),
                new FixedArchiveSource(archive),
                limits ?? CommittedRepositorySnapshotLimits.Production);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => service.AcquireAsync(
                    repository,
                    Guid.NewGuid(),
                    workingCopy,
                    CancellationToken.None));

            AssertNoSnapshots(storage.Path);
        }
    }

    private const string ProfiledRevision = "0123456789abcdef0123456789abcdef01234567";
    private static readonly string Fingerprint = new('a', 64);

    private static RepositoryProfileDraft CreateCompletedProfileDraft()
    {
        Guid repositoryId = Guid.NewGuid();
        ScanRun scanRun = ScanRun.Start(repositoryId, 1_800, DateTimeOffset.UtcNow);
        scanRun.SetCandidateCount(0);
        scanRun.Complete(0, 0, DateTimeOffset.UtcNow);
        return RepositoryProfileDraft.Create(
            scanRun,
            [],
            [],
            [],
            [],
            [],
            []);
    }

    private static RepositoryProfileDraft CreateWarningsOnlyProfileDraft()
    {
        Guid repositoryId = Guid.NewGuid();
        ScanRun scanRun = ScanRun.Start(repositoryId, 1_800, DateTimeOffset.UtcNow);
        scanRun.SetCandidateCount(5);
        ScanCandidateOutcome[] outcomes =
        [
            new(
                scanRun.Id,
                "config/malformed.xml",
                ScanCandidateOutcomeKind.Errored,
                ScanCandidateOutcomeReasons.FrameworkEvidenceMalformed,
                0,
                DateTimeOffset.UtcNow),
            new(
                scanRun.Id,
                "config/unsafe.xml",
                ScanCandidateOutcomeKind.Errored,
                ScanCandidateOutcomeReasons.FrameworkEvidenceUnsafePath,
                1,
                DateTimeOffset.UtcNow),
            new(
                scanRun.Id,
                "config/oversized.xml",
                ScanCandidateOutcomeKind.Errored,
                ScanCandidateOutcomeReasons.FrameworkEvidenceBoundExceeded,
                2,
                DateTimeOffset.UtcNow),
            new(
                scanRun.Id,
                "config/ambiguous.xml",
                ScanCandidateOutcomeKind.Skipped,
                ScanCandidateOutcomeReasons.FrameworkEvidenceAmbiguousScope,
                3,
                DateTimeOffset.UtcNow),
            new(
                scanRun.Id,
                "config/unsupported.xml",
                ScanCandidateOutcomeKind.Skipped,
                ScanCandidateOutcomeReasons.FrameworkEvidenceUnsupported,
                4,
                DateTimeOffset.UtcNow),
        ];
        scanRun.Complete(0, 0, DateTimeOffset.UtcNow);
        return RepositoryProfileDraft.Create(
            scanRun,
            [],
            [],
            [],
            [],
            [],
            outcomes);
    }

    private static (
        RepositoryProfileDraft Forward,
        RepositoryProfileDraft Reverse,
        ExpectedClassification[] Expected)
        CreateAdversarialSamplingProfiles(string snapshotRoot)
    {
        Guid repositoryId = Guid.NewGuid();
        ScanRun scanRun = ScanRun.Start(repositoryId, 1_800, DateTimeOffset.UtcNow);
        scanRun.SetCandidateCount(105);
        var codeUnits = new List<CodeUnit>();
        var expected = new List<ExpectedClassification>();
        for (var index = 0; index < 52; index++)
        {
            string path = $"src/rpc/Rpc{index:D3}.java";
            string source = index == 0
                ? "import org.apache.dubbo.Service;\n@DubboService @DubboService\n"
                : "@DubboService\n";
            WriteSnapshotFile(snapshotRoot, path, source);
            codeUnits.Add(CreateCodeUnit(scanRun, path, source, $"Rpc{index:D3}"));
            expected.Add(new(
                CommunicationKind.Rpc,
                ClassificationLevel.Confirmed,
                ConfidenceBand.High,
                EvidenceRule.RpcDirectAnnotation,
                EvidenceProvenance.StructuralAnnotation,
                path,
                index == 0 ? "L2" : "L1",
                index == 0 ? 2 : 1));
            if (index == 0)
            {
                expected.Add(new(
                    CommunicationKind.Rpc,
                    ClassificationLevel.Hint,
                    ConfidenceBand.Conservative,
                    EvidenceRule.RpcImportOnly,
                    EvidenceProvenance.Import,
                    path,
                    "L1",
                    1));
            }
        }

        for (var index = 0; index < 52; index++)
        {
            string path = $"src/http/Http{index:D3}.cs";
            string source = index == 0
                ? "using System.Net.Http;\n[ApiController]\n"
                : "[ApiController]\n";
            WriteSnapshotFile(snapshotRoot, path, source);
            codeUnits.Add(CreateCodeUnit(scanRun, path, source, $"Http{index:D3}"));
            expected.Add(new(
                CommunicationKind.Http,
                ClassificationLevel.Confirmed,
                ConfidenceBand.High,
                EvidenceRule.HttpControllerAnnotation,
                EvidenceProvenance.StructuralAnnotation,
                path,
                index == 0 ? "L2" : "L1",
                1));
            if (index == 0)
            {
                expected.Add(new(
                    CommunicationKind.Http,
                    ClassificationLevel.Hint,
                    ConfidenceBand.Conservative,
                    EvidenceRule.HttpImportOnly,
                    EvidenceProvenance.Import,
                    path,
                    "L1",
                    1));
            }
        }

        var warning = new ScanCandidateOutcome(
            scanRun.Id,
            "config/ambiguous.xml",
            ScanCandidateOutcomeKind.Skipped,
            ScanCandidateOutcomeReasons.FrameworkEvidenceAmbiguousScope,
            104,
            DateTimeOffset.UtcNow);
        expected.Add(new(
            null,
            ClassificationLevel.Warning,
            ConfidenceBand.None,
            EvidenceRule.ContradictoryEvidence,
            EvidenceProvenance.Detector,
            warning.FilePath,
            "audit:ContradictoryEvidence",
            1));
        expected.Add(new(
            null,
            ClassificationLevel.Warning,
            ConfidenceBand.None,
            EvidenceRule.BoundExceeded,
            EvidenceProvenance.Detector,
            CommunicationKindClassification.UnknownRelativePath,
            "sampling-bound",
            1));
        scanRun.Complete(104, 104, DateTimeOffset.UtcNow);
        var languageSummary = new ScanRunLanguageSummary(
            scanRun.Id,
            repositoryId,
            "test",
            distinctFileCount: 104);
        RepositoryProfileDraft Create(IEnumerable<CodeUnit> orderedUnits) =>
            RepositoryProfileDraft.Create(
                scanRun,
                orderedUnits,
                [],
                [],
                [],
                [languageSummary],
                [warning]);
        return (
            Create(codeUnits),
            Create(codeUnits.AsEnumerable().Reverse()),
            [.. expected]);
    }

    private static CodeUnit CreateCodeUnit(
        ScanRun scanRun,
        string relativePath,
        string source,
        string symbolName) =>
        new(
            scanRun.Id,
            scanRun.RepositoryId,
            relativePath,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(relativePath))),
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source))),
            lineStart: 1,
            lineEnd: source.Count(character => character == '\n'),
            symbolName,
            CodeUnitType.Class,
            RangeConfidence.Exact,
            "test");

    private static string ComputeExpectedAggregateFingerprint(
        IEnumerable<ExpectedClassification> candidates)
    {
        ExpectedClassification[] canonical =
        [
            .. candidates
                .OrderBy(candidate => candidate.Level)
                .ThenBy(candidate => candidate.Kind)
                .ThenBy(candidate => candidate.Rule)
                .ThenBy(candidate => candidate.RelativePath, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Selector, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Confidence)
                .ThenBy(candidate => candidate.Provenance),
        ];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendCanonicalField(hash, ProfiledRevision);
        AppendCanonicalField(
            hash,
            ((int)(ConfirmedCommunicationKinds.Rpc | ConfirmedCommunicationKinds.Http))
                .ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendCanonicalField(hash, "105");
        AppendCanonicalField(hash, "2");
        AppendCanonicalField(hash, "2");
        foreach (ExpectedClassification candidate in canonical)
        {
            AppendCanonicalField(hash, candidate.Kind?.ToString() ?? string.Empty);
            AppendCanonicalField(hash, candidate.Level.ToString());
            AppendCanonicalField(hash, candidate.Confidence.ToString());
            AppendCanonicalField(hash, candidate.Rule.ToString());
            AppendCanonicalField(hash, candidate.Provenance.ToString());
            AppendCanonicalField(hash, candidate.RelativePath);
            AppendCanonicalField(hash, candidate.Selector);
            AppendCanonicalField(
                hash,
                candidate.OccurrenceCount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendCanonicalField(IncrementalHash hash, string value)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, encoded.Length);
        hash.AppendData(length);
        hash.AppendData(encoded);
    }

    private static string[] ProjectClassifications(PreScanResultDraft result) =>
    [
        .. result.Classifications.Select(item => string.Join(
            "\u001f",
            item.Kind?.ToString() ?? string.Empty,
            item.Level,
            item.Confidence,
            item.Rule,
            item.Provenance,
            item.RelativePath,
            item.Locator,
            item.OccurrenceCount,
            item.Fingerprint)),
    ];

    private static RepositoryProfileDraft CreateCompletedProfileDraft(
        string relativePath,
        string source,
        string language,
        string symbolName,
        params (string FrameworkName, string DetectionSource)[] frameworks)
    {
        Guid repositoryId = Guid.NewGuid();
        ScanRun scanRun = ScanRun.Start(repositoryId, 1_800, DateTimeOffset.UtcNow);
        scanRun.SetCandidateCount(1);
        var codeUnit = new CodeUnit(
            scanRun.Id,
            repositoryId,
            relativePath,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(relativePath))),
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source))),
            lineStart: 1,
            lineEnd: source.Count(character => character == '\n'),
            symbolName,
            CodeUnitType.Class,
            RangeConfidence.Exact,
            language);
        scanRun.Complete(1, 0, DateTimeOffset.UtcNow);
        var languageSummary = new ScanRunLanguageSummary(
            scanRun.Id,
            repositoryId,
            language,
            distinctFileCount: 1);
        CodeUnitFramework[] frameworkFacts =
        [
            .. frameworks.Select(framework => new CodeUnitFramework(
                codeUnit.Id,
                scanRun.Id,
                repositoryId,
                framework.FrameworkName,
                FrameworkConfidence.High,
                framework.DetectionSource,
                DateTimeOffset.UtcNow)),
        ];
        return RepositoryProfileDraft.Create(
            scanRun,
            [codeUnit],
            [],
            [],
            frameworkFacts,
            [languageSummary],
            []);
    }

    private static void WriteSnapshotFile(
        string snapshotRoot,
        string relativePath,
        string source)
    {
        string fullPath = System.IO.Path.Combine(
            snapshotRoot,
            relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(
            fullPath,
            source,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static CommunicationKindClassification CreateClassification(
        Guid preScanResultId,
        CommunicationKind? kind = CommunicationKind.Rpc,
        ClassificationLevel level = ClassificationLevel.Confirmed,
        ConfidenceBand confidence = ConfidenceBand.High,
        EvidenceRule rule = EvidenceRule.RpcDirectAnnotation,
        EvidenceProvenance provenance = EvidenceProvenance.StructuralAnnotation,
        string relativePath = "src/Service.cs",
        string? locator = null) =>
        CommunicationKindClassification.Create(
            preScanResultId,
            kind,
            level,
            confidence,
            rule,
            provenance,
            relativePath,
            locator ?? $"{relativePath}#L1",
            1,
            Fingerprint);

    private static PreScanResult CreateApprovedResult(
        RepositoryProfileDraft profile,
        CommunicationKindClassification classification,
        string profiledRevision = ProfiledRevision,
        ConfirmedCommunicationKinds confirmedKinds = ConfirmedCommunicationKinds.Rpc) =>
        PreScanResult.CreateApproved(
            profile.ScanRun.RepositoryId,
            profile.ScanRun.Id,
            profiledRevision,
            profile.ScanRun.CompletedAt!.Value,
            Fingerprint,
            "预扫描已批准",
            confirmedKinds,
            classification.Level == ClassificationLevel.Confirmed ? classification.OccurrenceCount : 0,
            classification.Level == ClassificationLevel.Hint ? classification.OccurrenceCount : 0,
            classification.Level == ClassificationLevel.Warning ? classification.OccurrenceCount : 0,
            evidenceTruncated: false,
            [classification]);

    private static MemoryStream CreateTar(
        params (string Name, TarEntryType EntryType, string ContentOrTarget)[] entries)
    {
        var stream = new MemoryStream();
        using (var writer = new TarWriter(stream, leaveOpen: true))
        {
            foreach ((string name, TarEntryType entryType, string contentOrTarget) in entries)
            {
                var entry = new PaxTarEntry(entryType, name);
                if (entryType == TarEntryType.RegularFile)
                {
                    entry.DataStream = new MemoryStream(
                        Encoding.UTF8.GetBytes(contentOrTarget),
                        writable: false);
                }
                else
                {
                    entry.LinkName = contentOrTarget;
                }

                writer.WriteEntry(entry);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static void AssertNoSnapshots(string storageRoot)
    {
        string snapshotRoot = System.IO.Path.Combine(storageRoot, ".snapshots");
        Assert.False(
            Directory.Exists(snapshotRoot) &&
            Directory.EnumerateFileSystemEntries(snapshotRoot, "*", SearchOption.AllDirectories).Any());
    }

    private static void AssertProfiledRevision(string? revision)
    {
        Assert.NotNull(revision);
        Assert.InRange(revision.Length, 1, 64);
        Assert.Equal(revision.ToLowerInvariant(), revision);
        Assert.All(revision, character => Assert.True(char.IsAsciiHexDigit(character)));
    }

    private static RepositoryEntity CreateRepository(string gitUrl) =>
        RepositoryEntity.CreateStandalone(
            "snapshot-fixture",
            gitUrl,
            null,
            null,
            DateTimeOffset.UtcNow);

    private sealed class FixedArchiveSource(Stream stream) : ICommittedRepositoryArchiveSource
    {
        public Task<Stream> OpenArchiveAsync(
            string workingCopyPath,
            string profiledRevision,
            CancellationToken cancellationToken) =>
            Task.FromResult(stream);
    }

    private static string ReadEmbeddedCommunicationCatalogJson()
    {
        const string resourceName =
            "Vulgata.Infrastructure.CommunicationDetection.Catalog.communication-evidence-rules.v1.json";

        using Stream resource = typeof(CommunicationEvidenceRuleCatalog).Assembly
            .GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(
            resource,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false);
        return reader.ReadToEnd();
    }

    private sealed class CountingNonSeekableStream(byte[] content) : Stream
    {
        private int _position;

        public int BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            int bytesToRead = Math.Min(count, content.Length - _position);
            content.AsSpan(_position, bytesToRead).CopyTo(buffer.AsSpan(offset, bytesToRead));
            _position += bytesToRead;
            BytesRead += bytesToRead;
            return bytesToRead;
        }

        public override int Read(Span<byte> buffer)
        {
            int bytesToRead = Math.Min(buffer.Length, content.Length - _position);
            content.AsSpan(_position, bytesToRead).CopyTo(buffer);
            _position += bytesToRead;
            BytesRead += bytesToRead;
            return bytesToRead;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    private sealed class TestSnapshotLease(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string rootPath) : ICommittedRepositorySnapshotLease
    {
        public Guid RepositoryId { get; } = repositoryId;
        public Guid ScanRunId { get; } = scanRunId;
        public string ProfiledRevision { get; } = profiledRevision;
        public string RootPath { get; } = rootPath;

        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CountingArchiveSource(Func<Stream> streamFactory)
        : ICommittedRepositoryArchiveSource
    {
        public int OpenCount { get; private set; }

        public Task<Stream> OpenArchiveAsync(
            string workingCopyPath,
            string profiledRevision,
            CancellationToken cancellationToken)
        {
            OpenCount++;
            return Task.FromResult(streamFactory());
        }
    }

    private sealed class CancellingArchiveSource(CancellationTokenSource cancellation)
        : ICommittedRepositoryArchiveSource
    {
        public Task<Stream> OpenArchiveAsync(
            string workingCopyPath,
            string profiledRevision,
            CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            return Task.FromResult<Stream>(
                CreateTar(("tracked.txt", TarEntryType.RegularFile, "tracked")));
        }
    }

    private sealed class CleanupBlockingArchiveSource(string storageRoot)
        : ICommittedRepositoryArchiveSource, IDisposable
    {
        private FileStream? _cleanupBlock;
        private int _openCount;

        public bool CleanupWasBlocked => _cleanupBlock is not null;

        public Task<Stream> OpenArchiveAsync(
            string workingCopyPath,
            string profiledRevision,
            CancellationToken cancellationToken)
        {
            _openCount++;
            if (_openCount == 1)
            {
                return Task.FromResult<Stream>(
                    new CallbackReadStream(
                        CreateTar(
                            ("locked.txt", TarEntryType.RegularFile, "locked"),
                            ("link.txt", TarEntryType.SymbolicLink, "target")),
                        TryBlockCleanup));
            }

            return Task.FromResult<Stream>(
                CreateTar(("tracked.txt", TarEntryType.RegularFile, "tracked")));
        }

        public void ReleaseCleanupBlock()
        {
            _cleanupBlock?.Dispose();
            _cleanupBlock = null;
        }

        public void Dispose() => ReleaseCleanupBlock();

        private void TryBlockCleanup()
        {
            if (_cleanupBlock is not null)
            {
                return;
            }

            string snapshotStorage = System.IO.Path.Combine(storageRoot, ".snapshots");
            if (!Directory.Exists(snapshotStorage))
            {
                return;
            }

            string? extractedFile = Directory
                .EnumerateFiles(snapshotStorage, "locked.txt", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (extractedFile is null)
            {
                return;
            }

            try
            {
                _cleanupBlock = new FileStream(
                    extractedFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None);
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class CallbackReadStream(Stream inner, Action onRead) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            onRead();
            int read = inner.Read(buffer, offset, count);
            onRead();
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            onRead();
            int read = inner.Read(buffer);
            onRead();
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            onRead();
            int read = await inner.ReadAsync(buffer, cancellationToken);
            onRead();
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }

    private sealed class TemporaryGitRepository : IDisposable
    {
        public TemporaryGitRepository()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"vulgata-snapshot-source-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
            RunGit("init", "--quiet");
            RunGit("config", "user.email", "vulgata-tests@example.invalid");
            RunGit("config", "user.name", "Vulgata Tests");
        }

        public string Path { get; }

        public void Write(string relativePath, string content)
        {
            string path = System.IO.Path.Combine(
                Path,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public string CommitAll()
        {
            RunGit("add", "--all");
            RunGit("commit", "--quiet", "-m", "fixture");
            return RunGit("rev-parse", "--verify", "HEAD").Trim();
        }

        public void Dispose() => TemporaryDirectory.Delete(Path);

        private string RunGit(params string[] arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("git")
                {
                    WorkingDirectory = Path,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            foreach (string argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            Assert.True(process.Start());
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(
                process.ExitCode == 0,
                $"git {string.Join(' ', arguments)} failed: {standardError}");
            return standardOutput;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"vulgata-snapshot-storage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Delete(Path);

        public static void Delete(string path)
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed record ExpectedClassification(
        CommunicationKind? Kind,
        ClassificationLevel Level,
        ConfidenceBand Confidence,
        EvidenceRule Rule,
        EvidenceProvenance Provenance,
        string RelativePath,
        string Selector,
        int OccurrenceCount);
}
