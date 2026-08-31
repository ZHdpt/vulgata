using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared.Scans;
using Vulgata.Tests.PreparedScanning.TestDoubles;
using Vulgata.Web.Data;
using Xunit.Abstractions;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.PreparedScanning;

[Collection(PreparedScanningCollection.Name)]
public sealed class PreparedScanReleaseQualificationTests(ITestOutputHelper output)
{
    private const string PromptIdentity = "prepared-scan-prompt/story122-v1";
    private const string DocumentSchemaIdentity = "code-logic/file-v2";
    private const string RuntimeIdentityJson = "{\"runtime\":\"prepared-release-test\"}";
    private static readonly string RuntimeIdentityHash = new('9', 64);

    public enum ReviewedSeedFixtureMode
    {
        None,
        Missing,
        Filtered,
        Unsafe,
        NonContained,
        EmptyHash,
        Duplicate,
        Ambiguous,
        SplitInvalid,
        MoreThanTwentySegments,
        FinalSelectionOmission,
        CatalogUnavailable,
    }

    [Fact]
    public async Task ProductionPairBuilder_MatchedReviewedProfile_PromotesEveryRequiredSegmentAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(output, reviewedPair: true);

        PreparedScanPairBuildResult result = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(result.Failure);
        Assert.NotNull(result.PairPackage);
        PreparedFileCodeUnitDraft[] required = result.PairPackage!.Repositories
            .SelectMany(repository => repository.FileCodeUnits)
            .Where(unit => unit.DirectBusinessScore == 1_100)
            .ToArray();
        Assert.Equal(4, required.Length);
        Assert.All(required, unit =>
        {
            Assert.Equal(1_100, unit.FinalBusinessScore);
            Assert.Equal(PreparedSelectionReason.DirectBusinessEvidence, unit.SelectionReason);
        });
        Assert.Equal(2, harness.AnalysisEvents.Count(value => value.StartsWith("proof:", StringComparison.Ordinal)));
        Assert.All(harness.JavaRequests, paths => Assert.NotEmpty(paths));
    }

    [Theory]
    [InlineData(ReviewedSeedFixtureMode.Missing)]
    [InlineData(ReviewedSeedFixtureMode.Filtered)]
    [InlineData(ReviewedSeedFixtureMode.Unsafe)]
    [InlineData(ReviewedSeedFixtureMode.NonContained)]
    [InlineData(ReviewedSeedFixtureMode.EmptyHash)]
    [InlineData(ReviewedSeedFixtureMode.Duplicate)]
    [InlineData(ReviewedSeedFixtureMode.Ambiguous)]
    [InlineData(ReviewedSeedFixtureMode.SplitInvalid)]
    [InlineData(ReviewedSeedFixtureMode.MoreThanTwentySegments)]
    public async Task ProductionPairBuilder_MatchedReviewedProfile_InvalidRequiredEvidenceFailsBeforeManifestAsync(
        ReviewedSeedFixtureMode mode)
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(
            output,
            reviewedPair: true,
            reviewedSeedFixtureMode: mode);

        PreparedScanPairBuildResult result = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(result.PairPackage);
        Assert.NotNull(result.Failure);
        Assert.Equal("prepared-scan-reviewed-profile-seed-incomplete", result.Failure!.FailureCode);
        Assert.Equal(0, harness.AnalysisEvents.Count(value => value.StartsWith("proof:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ProductionPairBuilder_MatchedReviewedProfile_SelectionOverflowPreservesRequiredSegmentsAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(
            output,
            reviewedPair: true,
            reviewedSeedFixtureMode: ReviewedSeedFixtureMode.FinalSelectionOmission);

        PreparedScanPairBuildResult result = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(result.Failure);
        Assert.NotNull(result.PairPackage);
        Assert.Equal(
            4,
            result.PairPackage!.Repositories
                .SelectMany(repository => repository.FileCodeUnits)
                .Count(unit => unit.DirectBusinessScore == 1_100));
    }

    [Fact]
    public async Task ProductionPairBuilder_NonMatchingPair_IsByteIdenticalToGenericSelectionAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(output);

        PreparedScanPairBuildResult first = await harness.BuildAsync(reverseInputs: false);
        PreparedScanPairBuildResult reversed = await harness.BuildAsync(reverseInputs: true);

        Assert.Null(first.Failure);
        Assert.Null(reversed.Failure);
        Assert.Equal(ReleaseProjection(first.PairPackage!), ReleaseProjection(reversed.PairPackage!));
        Assert.DoesNotContain(
            first.PairPackage!.Repositories.SelectMany(repository => repository.FileCodeUnits),
            unit => unit.DirectBusinessScore == 1_100);
    }

    [Fact]
    public async Task ProductionPairBuilder_MatchedReviewedProfile_CatalogUnavailableMapsClosedOutcomeAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(
            output,
            reviewedPair: true,
            reviewedSeedFixtureMode: ReviewedSeedFixtureMode.CatalogUnavailable);

        PreparedScanPairBuildResult result = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(result.PairPackage);
        Assert.NotNull(result.Failure);
        Assert.Equal("prepared-scan-reviewed-profile-unavailable", result.Failure!.FailureCode);
        Assert.Equal(0, harness.AnalysisEvents.Count(value => value.StartsWith("proof:", StringComparison.Ordinal)));
    }

    [Fact]
    public void ReviewedProfileEvidence_IsScore1100AndAllRequiredSegmentsRemainMandatory()
    {
        bool parsed = Enum.TryParse(
            "ReviewedExactResolutionProfile",
            ignoreCase: false,
            out PreparedBusinessEvidence reviewed);
        Assert.True(parsed, "The reviewed exact-profile evidence value is not present yet.");
        Assert.Equal(1_100, PreparedScanPlanner.Score(reviewed));

        Guid firstRepository = Guid.Parse("00000000-0000-0000-0000-000000000101");
        Guid secondRepository = Guid.Parse("00000000-0000-0000-0000-000000000102");
        PreparedFileCodeUnitDraft[] candidates =
        [
            .. RequiredCandidates(firstRepository, reviewed),
            .. RequiredCandidates(secondRepository, reviewed),
            .. Enumerable.Range(0, 10).Select(index =>
                PreparedScanPlanner.CreateFileCodeUnit(new PreparedFileCodeUnitInput(
                    index % 2 == 0 ? firstRepository : secondRepository,
                    Guid.Parse("00000000-0000-0000-0000-000000000201"),
                    new string('a', 40),
                    $"src/optional/{index:D2}.java",
                    Hash($"optional-file:{index}"),
                    0,
                    16,
                    1,
                    1,
                    1,
                    1,
                    Hash($"optional-range:{index}"),
                    [PreparedBusinessEvidence.ProductionFallback])))
        ];

        PreparedSelectionPlan plan = PreparedScanPlanner.Plan(candidates, 50, 150);
        PreparedFileCodeUnitDraft[] required = candidates
            .Where(candidate => candidate.DirectBusinessScore == 1_100)
            .ToArray();
        Assert.Equal(40, required.Length);
        Assert.All(required, candidate =>
        {
            Assert.Equal(1_100, candidate.FinalBusinessScore);
            Assert.Equal(PreparedSelectionReason.DirectBusinessEvidence, candidate.SelectionReason);
            Assert.Contains(plan.Selected, selected =>
                selected.IdentityFingerprint == candidate.IdentityFingerprint);
        });
    }

    [Fact]
    public void ReviewedProfileSelection_IsIndependentOfRepositoryAndCandidateOrder()
    {
        bool parsed = Enum.TryParse(
            "ReviewedExactResolutionProfile",
            ignoreCase: false,
            out PreparedBusinessEvidence reviewed);
        Assert.True(parsed, "The reviewed exact-profile evidence value is not present yet.");
        Guid firstRepository = Guid.Parse("00000000-0000-0000-0000-000000000111");
        Guid secondRepository = Guid.Parse("00000000-0000-0000-0000-000000000112");
        PreparedFileCodeUnitDraft[] candidates =
        [
            .. RequiredCandidates(firstRepository, reviewed),
            .. RequiredCandidates(secondRepository, reviewed),
            .. Enumerable.Range(0, 10).Select(index =>
                PreparedScanPlanner.CreateFileCodeUnit(new PreparedFileCodeUnitInput(
                    index % 2 == 0 ? firstRepository : secondRepository,
                    Guid.Parse("00000000-0000-0000-0000-000000000211"),
                    new string('b', 40),
                    $"src/optional/{index:D2}.xml",
                    Hash($"optional-reversal:{index}"),
                    0,
                    16,
                    1,
                    1,
                    1,
                    1,
                    Hash($"optional-range-reversal:{index}"),
                    [PreparedBusinessEvidence.ProductionFallback])))
        ];

        static string Projection(PreparedSelectionPlan value) => string.Join(
            "|",
            value.Selected
                .OrderBy(candidate => candidate.RepositoryId)
                .ThenBy(candidate => candidate.RelativePath, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.SegmentOrdinal)
                .Select(candidate =>
                    $"{candidate.RepositoryId:D}:{candidate.RelativePath}:{candidate.SegmentOrdinal}:" +
                    $"{candidate.DirectBusinessScore}:{candidate.IdentityFingerprint}"));

        string forward = Projection(PreparedScanPlanner.Plan(candidates, 50, 150));
        string reversed = Projection(PreparedScanPlanner.Plan([.. candidates.Reverse()], 50, 150));
        Assert.Equal(forward, reversed);
    }

    private static IEnumerable<PreparedFileCodeUnitDraft> RequiredCandidates(
        Guid repositoryId,
        PreparedBusinessEvidence reviewed)
    {
        for (int index = 0; index < 20; index++)
        {
            yield return PreparedScanPlanner.CreateFileCodeUnit(new PreparedFileCodeUnitInput(
                repositoryId,
                Guid.Parse("00000000-0000-0000-0000-000000000301"),
                new string('c', 40),
                $"src/reviewed/required-{index:D2}.java",
                Hash($"required-file:{repositoryId:D}:{index}"),
                0,
                16,
                1,
                1,
                1,
                1,
                Hash($"required-range:{repositoryId:D}:{index}"),
                [reviewed]));
        }
    }

    [Fact]
    public void PreparedJavaRangeSource_ExposesRevisionBoundExactTopLevelRangeSeam()
    {
        Type? sourceType = typeof(PreparedJdtSymbolSpan).Assembly.GetType(
            "Vulgata.Core.DomainServices.IPreparedTopLevelSymbolSource");
        Assert.NotNull(sourceType);
        Assert.True(sourceType!.IsInterface);

        MethodInfo? method = sourceType.GetMethod(
            "GetRangesAsync",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        ParameterInfo[] parameters = method!.GetParameters();
        Assert.Collection(
            parameters,
            parameter => Assert.Equal(typeof(AnalysisScope), parameter.ParameterType),
            parameter => Assert.Equal(typeof(string), parameter.ParameterType),
            parameter => Assert.Equal(typeof(string), parameter.ParameterType),
            parameter => Assert.Equal(typeof(CancellationToken), parameter.ParameterType));
        Assert.True(method.ReturnType.IsGenericType);
        Assert.Equal(typeof(Task<>), method.ReturnType.GetGenericTypeDefinition());
        Type resultType = method.ReturnType.GetGenericArguments()[0];
        Assert.NotNull(resultType.GetProperty("Ranges"));
        Assert.NotNull(resultType.GetProperty("Failure"));
    }

    [Fact]
    public async Task ProductionPairBuilder_OversizedJava_ConsumesExactRangesOrClosesWholeFileAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(output);
        harness.EnableJavaRangeQualification();
        string source = CreateOversizedJavaSource();

        harness.JavaRangeSource.Mode = JavaRangeFixtureMode.Exact;
        PreparedScanPairBuildResult exact = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(exact.Failure);
        Assert.NotNull(exact.PairPackage);
        PreparedFileCodeUnitDraft[] exactUnits = exact.PairPackage!.Repositories
            .SelectMany(repository => repository.FileCodeUnits)
            .Where(unit => unit.RelativePath == ReleaseHarness.OversizedJavaPath)
            .OrderBy(unit => unit.SegmentOrdinal)
            .ToArray();
        Assert.Equal(2, exactUnits.Length);
        string fileHash = Hash(source);
        Assert.All(exactUnits, unit =>
        {
            Assert.Equal(fileHash, unit.FileHash);
            Assert.Equal(2, unit.SegmentCount);
            Assert.InRange(unit.ByteEndExclusive - unit.ByteStart, 1, PreparedSourceRangePlanner.MaximumRawBytes);
            Assert.InRange(unit.LineEnd - unit.LineStart + 1, 1, PreparedSourceRangePlanner.MaximumLogicalLines);
        });
        byte[] sourceBytes = Encoding.UTF8.GetBytes(source);
        long firstStart = Encoding.UTF8.GetByteCount("package release;\n");
        long firstEnd = firstStart + Encoding.UTF8.GetByteCount(source.Split('\n')[1]);
        long secondStart = firstEnd + Encoding.UTF8.GetByteCount("\n");
        long secondEnd = sourceBytes.Length - Encoding.UTF8.GetByteCount("\n");
        Assert.Equal(firstStart, exactUnits[0].ByteStart);
        Assert.Equal(firstEnd, exactUnits[0].ByteEndExclusive);
        Assert.Equal(secondStart, exactUnits[1].ByteStart);
        Assert.Equal(secondEnd, exactUnits[1].ByteEndExclusive);
        Assert.Equal(1, exactUnits[0].SegmentOrdinal);
        Assert.Equal(2, exactUnits[1].SegmentOrdinal);
        Assert.Equal(2, exactUnits[0].LineStart);
        Assert.Equal(2, exactUnits[0].LineEnd);
        Assert.Equal(3, exactUnits[1].LineStart);
        Assert.Equal(3, exactUnits[1].LineEnd);
        Assert.Equal(
            HashRawInterval(source, exactUnits[0].ByteStart, exactUnits[0].ByteEndExclusive),
            exactUnits[0].SourceHash);
        Assert.Equal(
            HashRawInterval(source, exactUnits[1].ByteStart, exactUnits[1].ByteEndExclusive),
            exactUnits[1].SourceHash);
        Assert.True(harness.JavaRangeSource.Calls >= 1);

        foreach (JavaRangeFixtureMode mode in new[]
        {
            JavaRangeFixtureMode.Missing,
            JavaRangeFixtureMode.Malformed,
            JavaRangeFixtureMode.Oversized,
        })
        {
            harness.JavaRangeSource.Mode = mode;
            PreparedScanPairBuildResult closed = await harness.BuildAsync(reverseInputs: false);

            Assert.Null(closed.Failure);
            Assert.NotNull(closed.PairPackage);
            Assert.Empty(closed.PairPackage!.Repositories
                .SelectMany(repository => repository.FileCodeUnits)
                .Where(unit => unit.RelativePath == ReleaseHarness.OversizedJavaPath));
            Assert.Single(closed.PairPackage.Repositories.SelectMany(repository => repository.Audits), audit =>
                audit.RelativePath == ReleaseHarness.OversizedJavaPath &&
                audit.Outcome == PreparedScanAuditOutcome.Skipped &&
                audit.Reason == PreparedScanAuditReason.InvalidSplitRange &&
                audit.FileCodeUnitIdentityFingerprint is null &&
                audit.PairSelectionOrdinal is null);
        }
    }

    [Fact]
    public async Task ProductionPairBuilder_ThreeRunsIncludingReversedInputs_ProducesIdenticalSc001OutputsAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(output);

        PreparedScanPairBuildResult first = await harness.BuildAsync(reverseInputs: false);
        PreparedScanPairBuildResult second = await harness.BuildAsync(reverseInputs: false);
        PreparedScanPairBuildResult reversed = await harness.BuildAsync(reverseInputs: true);

        Assert.Null(first.Failure);
        Assert.NotNull(first.PairPackage);
        Assert.Null(second.Failure);
        Assert.NotNull(second.PairPackage);
        Assert.Null(reversed.Failure);
        Assert.NotNull(reversed.PairPackage);
        Assert.Equal(ReleaseProjection(first.PairPackage!), ReleaseProjection(second.PairPackage!));
        Assert.Equal(ReleaseProjection(first.PairPackage!), ReleaseProjection(reversed.PairPackage!));
        Assert.InRange(first.PairPackage!.SelectedCount, 50, 150);
        harness.AssertTimingBounds();
    }

    [Fact]
    public async Task ProductionPairBuilder_OversizedEligibleXml_IsSplitIntoBoundedFileCodeUnitsAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(output);

        PreparedScanPairBuildResult result = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(result.Failure);
        Assert.NotNull(result.PairPackage);
        PreparedFileCodeUnitDraft[] splitUnits =
        [
            .. result.PairPackage!.Repositories
                .SelectMany(repository => repository.FileCodeUnits)
                .Where(unit => unit.RelativePath == ReleaseHarness.OversizedXmlPath)
                .OrderBy(unit => unit.SegmentOrdinal),
        ];
        Assert.True(splitUnits.Length >= 2,
            "T031 requires an eligible oversized XML file to be split through SafeXmlDirectChildRangeSet; it must not be silently skipped.");
        Assert.All(splitUnits, unit =>
        {
            Assert.InRange(unit.ByteEndExclusive - unit.ByteStart, 1, PreparedSourceRangePlanner.MaximumRawBytes);
            Assert.InRange(unit.LineEnd - unit.LineStart + 1, 1, PreparedSourceRangePlanner.MaximumLogicalLines);
        });
    }

    [Fact]
    public async Task ProductionPairBuilder_MatchingXmlCatalogEvidence_ProducesClassificationsAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(output);

        PreparedScanPairBuildResult result = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(result.Failure);
        Assert.NotNull(result.PairPackage);
        CommunicationKindClassification[] classifications =
        [
            .. result.PairPackage!.Repositories.SelectMany(repository => repository.Classifications),
        ];
        Assert.NotEmpty(classifications);
        Assert.Contains(classifications, classification =>
            classification.Kind == CommunicationKind.Http &&
            classification.Level == ClassificationLevel.Confirmed &&
            classification.RelativePath.EndsWith("application.xml", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProductionPairBuilder_RelatedExpansion_IsBoundedAfterReadinessAndFallsBackDeterministicallyAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(output);
        harness.EnableRelatedExpansionQualification();
        var requests = new List<PreparedRelatedFilesRequest>();
        harness.CodeGraph.RelatedFilesHandler = (request, _) =>
        {
            requests.Add(request);
            harness.AnalysisEvents.Add($"related:{request.Scope.RepositoryId:D}");
            return Task.FromResult(new PreparedRelatedFilesResult(
                AnalysisOperationStatus.Succeeded,
                [new(
                    "src/release/Entry00.java",
                    request.SeedRelativePaths[0],
                    PreparedRelationshipKind.Calls,
                    1)],
                null));
        };

        PreparedScanPairBuildResult expanded = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(expanded.Failure);
        Assert.NotNull(expanded.PairPackage);
        Assert.NotEmpty(requests);
        Assert.All(requests, request =>
        {
            Assert.InRange(request.SeedRelativePaths.Count, 1, PreparedScanPlanner.MaximumHighValueSeedsPerRepository);
            Assert.Equal(2, request.MaximumHops);
            Assert.Equal(200, request.MaximumFiles);
            Assert.Equal(
                [PreparedRelationshipKind.Calls, PreparedRelationshipKind.Imports, PreparedRelationshipKind.Inherits],
                request.RelationshipKinds);
            int readinessIndex = harness.AnalysisEvents.LastIndexOf($"java:{request.Scope.RepositoryId:D}");
            int expansionIndex = harness.AnalysisEvents.LastIndexOf($"related:{request.Scope.RepositoryId:D}");
            Assert.True(readinessIndex >= 0 && expansionIndex > readinessIndex);
        });
        foreach (PreparedRepositoryPackage repository in expanded.PairPackage.Repositories.OrderBy(value => value.RepositoryId))
        {
            PreparedFileCodeUnitDraft expandedUnit = repository.FileCodeUnits
                .Single(unit => unit.RelativePath == "src/release/Entry00.java");
            Assert.Equal(100, expandedUnit.DirectBusinessScore);
            Assert.Equal(900, expandedUnit.FinalBusinessScore);
            Assert.Equal(PreparedSelectionReason.RelatedCall, expandedUnit.SelectionReason);
        }

        harness.CodeGraph.RelatedFilesHandler = (_, _) => Task.FromResult(new PreparedRelatedFilesResult(
            AnalysisOperationStatus.Failed,
            [],
            new AnalysisPortFailure(
                AnalysisPortFailureReason.Unavailable,
                "related-expansion-unavailable",
                "bounded test failure")));
        PreparedScanPairBuildResult fallback = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(fallback.Failure);
        Assert.NotNull(fallback.PairPackage);
        foreach (PreparedRepositoryPackage repository in fallback.PairPackage.Repositories.OrderBy(value => value.RepositoryId))
        {
            PreparedFileCodeUnitDraft fallbackUnit = repository.FileCodeUnits
                .Single(unit => unit.RelativePath == "src/release/Entry00.java");
            Assert.Equal(100, fallbackUnit.DirectBusinessScore);
            Assert.Equal(100, fallbackUnit.FinalBusinessScore);
            Assert.Equal(PreparedSelectionReason.ProductionFallback, fallbackUnit.SelectionReason);
        }
        Assert.Contains(
            fallback.PairPackage.Repositories.SelectMany(repository => repository.Audits),
            audit => audit.Outcome == PreparedScanAuditOutcome.Warning &&
                audit.Reason == PreparedScanAuditReason.RelatedFileExpansionFailed);

        harness.CodeGraph.RelatedFilesHandler = (_, _) => Task.FromResult(new PreparedRelatedFilesResult(
            AnalysisOperationStatus.Failed,
            [],
            new AnalysisPortFailure(
                AnalysisPortFailureReason.TimedOut,
                "related-expansion-timeout",
                "bounded test timeout")));
        PreparedScanPairBuildResult timedOut = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(timedOut.Failure);
        Assert.NotNull(timedOut.PairPackage);
        foreach (PreparedRepositoryPackage repository in timedOut.PairPackage.Repositories.OrderBy(value => value.RepositoryId))
        {
            PreparedFileCodeUnitDraft timedOutUnit = repository.FileCodeUnits
                .Single(unit => unit.RelativePath == "src/release/Entry00.java");
            Assert.Equal(100, timedOutUnit.DirectBusinessScore);
            Assert.Equal(100, timedOutUnit.FinalBusinessScore);
            Assert.Equal(PreparedSelectionReason.ProductionFallback, timedOutUnit.SelectionReason);
        }
        Assert.Contains(
            timedOut.PairPackage.Repositories.SelectMany(repository => repository.Audits),
            audit => audit.Outcome == PreparedScanAuditOutcome.Warning &&
                audit.Reason == PreparedScanAuditReason.RelatedFileExpansionFailed);
    }

    [Fact]
    public async Task ProductionPairBuilder_EmptyOrdinalFirstJava_IsExcludedFromReadinessCandidatesAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(output);
        harness.EnableEmptyJavaReadinessCandidate();

        PreparedScanPairBuildResult result = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(result.Failure);
        Assert.NotNull(result.PairPackage);
        Assert.NotEmpty(harness.JavaRequests);
        Assert.All(
            harness.JavaRequests,
            paths =>
            {
                Assert.DoesNotContain("src/release/A-Empty.java", paths);
                Assert.Contains("src/release/Entry00.java", paths);
            });
    }

    [Fact]
    public async Task ProductionPairBuilder_InventoryOutcomesBecomeCanonicalAuditsAndWarningsAsync()
    {
        await using ReleaseHarness harness = await ReleaseHarness.CreateAsync(output);
        harness.EnableInventoryEdgeCases();

        PreparedScanPairBuildResult result = await harness.BuildAsync(reverseInputs: false);

        Assert.Null(result.Failure);
        Assert.NotNull(result.PairPackage);
        foreach (PreparedRepositoryPackage repository in result.PairPackage!.Repositories)
        {
            Assert.Contains(repository.Audits, audit =>
                audit.RelativePath == ReleaseHarness.FilteredPath &&
                audit.Outcome == PreparedScanAuditOutcome.Filtered &&
                audit.Reason == PreparedScanAuditReason.ExcludedTestPath &&
                audit.FileCodeUnitIdentityFingerprint is null &&
                audit.PairSelectionOrdinal is null);
            Assert.Contains(repository.Audits, audit =>
                audit.RelativePath == ReleaseHarness.InvalidSplitPath &&
                audit.Outcome == PreparedScanAuditOutcome.Skipped &&
                audit.Reason == PreparedScanAuditReason.InvalidSplitRange &&
                audit.FileCodeUnitIdentityFingerprint is null &&
                audit.PairSelectionOrdinal is null);
            Assert.Contains(repository.Audits, audit =>
                audit.RelativePath == ReleaseHarness.UnsafeXmlPath &&
                audit.Outcome == PreparedScanAuditOutcome.Warning &&
                audit.Reason == PreparedScanAuditReason.UnsafeXml &&
                audit.FileCodeUnitIdentityFingerprint is null &&
                audit.PairSelectionOrdinal is null);
            Assert.Contains(repository.Audits, audit =>
                audit.RelativePath == ReleaseHarness.HardBoundXmlPath &&
                audit.Outcome == PreparedScanAuditOutcome.Warning &&
                audit.Reason == PreparedScanAuditReason.HardBoundExceeded &&
                audit.FileCodeUnitIdentityFingerprint is null &&
                audit.PairSelectionOrdinal is null);
            Assert.Contains(repository.Audits, audit =>
                audit.RelativePath == ReleaseHarness.BinaryJavaPath &&
                audit.Outcome == PreparedScanAuditOutcome.Filtered &&
                audit.Reason == PreparedScanAuditReason.BinaryJava &&
                audit.FileCodeUnitIdentityFingerprint is null &&
                audit.PairSelectionOrdinal is null);
            Assert.True(repository.TotalWarningCount >= 2);
            Assert.Contains(repository.Classifications, classification =>
                classification.Level == ClassificationLevel.Warning &&
                classification.Rule == EvidenceRule.BoundExceeded &&
                classification.RelativePath == ReleaseHarness.HardBoundXmlPath);
        }
    }

    private static string ReleaseProjection(PreparedScanPairPackage package)
    {
        object projection = new
        {
            package.CoordinationId,
            package.PairSelectionFingerprint,
            package.PairPackageFingerprint,
            package.SelectedCount,
            Repositories = package.Repositories
                .OrderBy(repository => repository.RepositoryId.ToString("D"), StringComparer.Ordinal)
                .Select(repository => new
                {
                    repository.RepositoryId,
                    repository.ScanRunId,
                    repository.ProfiledRevision,
                    repository.SnapshotIdentity,
                    repository.RuntimeIdentityHash,
                    CodeGraphProofIdentity = repository.CodeGraphProof.IdentityHash,
                    JavaReadinessIdentity = repository.JavaReadiness.IdentityHash,
                    CatalogIdentity = repository.Catalog.ResourceHash,
                    repository.PreparedScanContextIdentityHash,
                    repository.ContextContentHash,
                    repository.ManifestFingerprint,
                    Units = repository.FileCodeUnits
                        .OrderBy(unit => unit.ManifestOrdinal)
                        .Select(unit => new
                        {
                            unit.RelativePath,
                            unit.FileHash,
                            unit.ByteStart,
                            unit.ByteEndExclusive,
                            unit.LineStart,
                            unit.LineEnd,
                            unit.SegmentOrdinal,
                            unit.SegmentCount,
                            unit.SourceHash,
                            unit.IdentityFingerprint,
                            unit.DirectBusinessScore,
                            unit.FinalBusinessScore,
                            unit.SelectionReason,
                            unit.ManifestOrdinal,
                            unit.PairSelectionOrdinal,
                        }),
                    Audits = repository.Audits
                        .OrderBy(audit => audit.Ordinal)
                        .Select(audit => new
                        {
                            audit.Ordinal,
                            audit.RelativePath,
                            audit.Outcome,
                            audit.Reason,
                            audit.FileHash,
                            audit.RawByteCount,
                            audit.LogicalLineCount,
                            audit.FileCodeUnitIdentityFingerprint,
                            audit.PairSelectionOrdinal,
                            audit.PreparedScanManifestEntryId,
                            audit.DetailFingerprint,
                        }),
                    Classifications = repository.Classifications
                        .OrderBy(classification => classification.Fingerprint, StringComparer.Ordinal)
                        .Select(classification => new
                        {
                            classification.Kind,
                            classification.Level,
                            classification.Confidence,
                            classification.Rule,
                            classification.Provenance,
                            classification.RelativePath,
                            classification.Locator,
                            classification.OccurrenceCount,
                            classification.Fingerprint,
                        }),
                }),
        };
        return JsonSerializer.Serialize(projection);
    }

    private enum JavaRangeFixtureMode
    {
        Exact,
        Missing,
        Malformed,
        Oversized,
    }

    private sealed class QualificationJavaRangeSource : IPreparedTopLevelSymbolSource
    {
        internal JavaRangeFixtureMode Mode { get; set; } = JavaRangeFixtureMode.Exact;

        internal int Calls { get; private set; }

        public Task<PreparedJavaSourceResult> GetRangesAsync(
            AnalysisScope scope,
            string relativePath,
            string fileHash,
            CancellationToken cancellationToken)
        {
            Calls++;
            Assert.NotEqual(Guid.Empty, scope.RepositoryId);
            Assert.False(string.IsNullOrWhiteSpace(scope.ProfiledRevision));
            Assert.Equal(ReleaseHarness.OversizedJavaPath, relativePath);
            Assert.Equal(Hash(CreateOversizedJavaSource()), fileHash);
            cancellationToken.ThrowIfCancellationRequested();

            string[] lines = CreateOversizedJavaSource().Split('\n');
            return Task.FromResult(Mode switch
            {
                JavaRangeFixtureMode.Exact => new PreparedJavaSourceResult(
                    [
                        new(1, 0, 1, lines[1].Length),
                        new(2, 0, 2, lines[2].Length),
                    ],
                    null),
                JavaRangeFixtureMode.Missing => new PreparedJavaSourceResult(
                    [],
                    new AnalysisPortFailure(
                        AnalysisPortFailureReason.MalformedResponse,
                        "missing_java_ranges",
                        "missing exact Java ranges")),
                JavaRangeFixtureMode.Malformed => new PreparedJavaSourceResult(
                    [new(1, 0, 1, 0)],
                    null),
                JavaRangeFixtureMode.Oversized => new PreparedJavaSourceResult(
                    [new(1, 0, 2, lines[2].Length)],
                    null),
                _ => throw new ArgumentOutOfRangeException(),
            });
        }
    }

    private sealed class ReleaseHarness : IAsyncDisposable
    {
        internal const string OversizedXmlPath = "src/release/oversized-application.xml";
        internal const string OversizedJavaPath = "src/release/A-LargeEntry.java";
        internal const string FilteredPath = "src/test/Filtered.java";
        internal const string InvalidSplitPath = "src/release/invalid-split.xml";
        internal const string UnsafeXmlPath = "src/release/unsafe.xml";
        internal const string HardBoundXmlPath = "src/release/hard-bound.xml";
        internal const string BinaryJavaPath = "src/release/binary.java";
        private readonly LoginLogoutTests.CustomWebApplicationFactory _baseFactory;
        private readonly WebApplicationFactory<Program> _factory;
        private readonly AsyncServiceScope _scope;
        private readonly IPreparedScanPairBuilder _builder;
        private readonly SystemEntity _system;
        private readonly RepositoryEntity[] _repositories;
        private readonly PreparedScanRepositoryRevisionBinding[] _bindings;
        private readonly PreparedScanInitiationTuple _tuple;
        private readonly StubPreparedCodeGraphAnalysisPort _codeGraph;
        private readonly QualificationJavaRangeSource _javaRangeSource;
        private readonly List<string> _analysisEvents;
        private readonly List<IReadOnlyList<string>> _javaRequests;
        private readonly ITestOutputHelper _output;
        private readonly bool _reviewedPair;
        private readonly ReviewedSeedFixtureMode _reviewedSeedFixtureMode;
        private readonly Dictionary<Guid, List<long>> _codeGraphMilliseconds = [];
        private readonly Dictionary<Guid, List<long>> _javaMilliseconds = [];
        private readonly Dictionary<Guid, List<long>> _preScanMilliseconds = [];
        private bool _reverseCandidates;
        private bool _includeInventoryEdgeCases;
        private bool _includeJavaRangeQualification;
        private bool _omitOversizedXmlForRelatedExpansion;
        private bool _includeEmptyJavaReadinessCandidate;

        private ReleaseHarness(
            LoginLogoutTests.CustomWebApplicationFactory baseFactory,
            WebApplicationFactory<Program> factory,
            AsyncServiceScope scope,
            IPreparedScanPairBuilder builder,
            SystemEntity system,
            RepositoryEntity[] repositories,
            PreparedScanRepositoryRevisionBinding[] bindings,
            PreparedScanInitiationTuple tuple,
            StubPreparedCodeGraphAnalysisPort codeGraph,
            QualificationJavaRangeSource javaRangeSource,
            List<string> analysisEvents,
            List<IReadOnlyList<string>> javaRequests,
            ITestOutputHelper output,
            bool reviewedPair,
            ReviewedSeedFixtureMode reviewedSeedFixtureMode)
        {
            _baseFactory = baseFactory;
            _factory = factory;
            _scope = scope;
            _builder = builder;
            _system = system;
            _repositories = repositories;
            _bindings = bindings;
            _tuple = tuple;
            _codeGraph = codeGraph;
            _javaRangeSource = javaRangeSource;
            _analysisEvents = analysisEvents;
            _javaRequests = javaRequests;
            _output = output;
            _reviewedPair = reviewedPair;
            _reviewedSeedFixtureMode = reviewedSeedFixtureMode;
        }

        internal StubPreparedCodeGraphAnalysisPort CodeGraph => _codeGraph;

        internal QualificationJavaRangeSource JavaRangeSource => _javaRangeSource;

        internal List<string> AnalysisEvents => _analysisEvents;

        internal IReadOnlyList<IReadOnlyList<string>> JavaRequests => _javaRequests;

        internal void EnableInventoryEdgeCases() => _includeInventoryEdgeCases = true;

        internal void EnableJavaRangeQualification() => _includeJavaRangeQualification = true;

        internal void EnableRelatedExpansionQualification() => _omitOversizedXmlForRelatedExpansion = true;

        internal void EnableEmptyJavaReadinessCandidate() => _includeEmptyJavaReadinessCandidate = true;

        internal static async Task<ReleaseHarness> CreateAsync(
            ITestOutputHelper output,
            bool reviewedPair = false,
            ReviewedSeedFixtureMode reviewedSeedFixtureMode = ReviewedSeedFixtureMode.None)
        {
            Dictionary<Guid, List<long>>? codeGraphTimings = null;
            Dictionary<Guid, List<long>>? javaTimings = null;
            var starts = new Dictionary<Guid, Stopwatch>();
            var analysisEvents = new List<string>();
            var javaRequests = new List<IReadOnlyList<string>>();
            var javaRangeSource = new QualificationJavaRangeSource();
            var baseFactory = new LoginLogoutTests.CustomWebApplicationFactory();
            var revisionAcquirer = new StubPreparedRevisionAcquirer();
            revisionAcquirer.Handler = (request, _) =>
            {
                starts[request.RepositoryId] = Stopwatch.StartNew();
                return Task.FromResult(new PreparedRevisionAcquisitionResult(
                    new PreparedRevisionSnapshot(
                        request.RepositoryId,
                        request.ScanRunId,
                        request.RequestedRef,
                        request.ProfiledRevision,
                        Hash($"snapshot:{request.RepositoryId:D}"),
                        $"snapshot-{request.RepositoryId:N}",
                        $"root-{request.RepositoryId:N}"),
                    null));
            };
            var snapshotSource = new StubPreparedSnapshotSource();
            ReleaseHarness? harness = null;
            snapshotSource.Handler = (snapshot, _) => Task.FromResult(new PreparedSnapshotReadResult(
                CreateReleaseFiles(
                    snapshot,
                    harness?._reverseCandidates == true,
                    harness?._includeInventoryEdgeCases == true,
                    harness is not null &&
                        harness._includeJavaRangeQualification == true &&
                        harness._repositories[0].Id == snapshot.RepositoryId,
                    harness?._omitOversizedXmlForRelatedExpansion != true &&
                        harness?._includeJavaRangeQualification != true &&
                        harness?._includeInventoryEdgeCases != true,
                    harness?._includeEmptyJavaReadinessCandidate == true,
                    harness?._reviewedPair == true,
                    harness?._reviewedPair == true &&
                        harness._repositories[0].Id == snapshot.RepositoryId,
                    harness?._reviewedSeedFixtureMode ?? ReviewedSeedFixtureMode.None),
                null));
            var codeGraph = new StubPreparedCodeGraphAnalysisPort();
            codeGraph.ProofHandler = (request, _) =>
            {
                analysisEvents.Add($"proof:{request.Scope.RepositoryId:D}");
                Stopwatch timer = Stopwatch.StartNew();
                PreparedCodeGraphProofResult result = CompleteCodeGraph(request);
                timer.Stop();
                AddTiming(codeGraphTimings!, request.Scope.RepositoryId, timer.ElapsedMilliseconds);
                return Task.FromResult(result);
            };
            var java = new StubPreparedJavaReadinessPort();
            java.Handler = (request, _) =>
            {
                analysisEvents.Add($"java:{request.Scope.RepositoryId:D}");
                javaRequests.Add(request.EligibleJavaRelativePaths.ToArray());
                Stopwatch timer = Stopwatch.StartNew();
                PreparedJavaReadinessResult result = ReadyJava(request);
                timer.Stop();
                AddTiming(javaTimings!, request.Scope.RepositoryId, timer.ElapsedMilliseconds);
                return Task.FromResult(result);
            };
            var catalog = new StubPreparedCommunicationCatalogSource
            {
                Handler = reviewedSeedFixtureMode == ReviewedSeedFixtureMode.CatalogUnavailable
                    ? _ => Task.FromResult(new PreparedCatalogLoadResult(
                        null,
                        new PreparedScanFailure(
                            PreparedScanFailureReason.AnalysisNotReady,
                            "catalog-unavailable",
                            "catalog-unavailable")))
                    : _ => Task.FromResult(new PreparedCatalogLoadResult(CreateCatalog(), null)),
            };
            var runtimeProbe = new StubReadyAnalysisRuntimeHealthProbe();
            WebApplicationFactory<Program> factory = baseFactory.WithWebHostBuilder(web =>
                web.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IPreparedRevisionAcquirer>();
                    services.AddSingleton<IPreparedRevisionAcquirer>(revisionAcquirer);
                    services.RemoveAll<IPreparedSnapshotSource>();
                    services.AddSingleton<IPreparedSnapshotSource>(snapshotSource);
                    services.RemoveAll<IAnalysisRuntimeHealthProbe>();
                    services.AddSingleton<IAnalysisRuntimeHealthProbe>(runtimeProbe);
                    services.RemoveAll<IPreparedCommunicationCatalogSource>();
                    services.AddSingleton<IPreparedCommunicationCatalogSource>(catalog);
                    services.RemoveAll<IPreparedCodeGraphAnalysisPort>();
                    services.AddSingleton<IPreparedCodeGraphAnalysisPort>(codeGraph);
                    services.RemoveAll<IPreparedJavaReadinessPort>();
                    services.AddSingleton<IPreparedJavaReadinessPort>(java);
                    services.RemoveAll<IPreparedTopLevelSymbolSource>();
                    services.AddSingleton<IPreparedTopLevelSymbolSource>(javaRangeSource);
                }));
            AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            VulgataDbContext db = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            (SystemEntity system, RepositoryEntity[] repositories, PreparedScanRepositoryRevisionBinding[] bindings) =
                await SeedAsync(db, reviewedPair);
            PreparedScanInitiationTuple tuple = CreateInitiationTuple(db, system, repositories, bindings);
            IPreparedScanPairBuilder builder = scope.ServiceProvider.GetRequiredService<IPreparedScanPairBuilder>();
            harness = new(
                baseFactory,
                factory,
                scope,
                builder,
                system,
                repositories,
                bindings,
                tuple,
                codeGraph,
                javaRangeSource,
                analysisEvents,
                javaRequests,
                output,
                reviewedPair,
                reviewedSeedFixtureMode);
            codeGraphTimings = harness._codeGraphMilliseconds;
            javaTimings = harness._javaMilliseconds;
            harness._starts = starts;
            return harness;
        }

        private Dictionary<Guid, Stopwatch> _starts = [];

        internal async Task<PreparedScanPairBuildResult> BuildAsync(bool reverseInputs)
        {
            _reverseCandidates = reverseInputs;
            RepositoryEntity[] repositories = reverseInputs ? [.. _repositories.Reverse()] : _repositories;
            PreparedScanRepositoryRevisionBinding[] bindings = reverseInputs ? [.. _bindings.Reverse()] : _bindings;
            Stopwatch total = Stopwatch.StartNew();
            PreparedScanPairBuildResult result = await _builder.BuildAsync(
                _tuple,
                repositories,
                bindings,
                CancellationToken.None);
            total.Stop();
            foreach (RepositoryEntity repository in _repositories)
            {
                long elapsed = _starts.TryGetValue(repository.Id, out Stopwatch? timer)
                    ? timer.ElapsedMilliseconds
                    : total.ElapsedMilliseconds;
                AddTiming(_preScanMilliseconds, repository.Id, elapsed);
            }
            WriteTimings();
            return result;
        }

        internal void AssertTimingBounds()
        {
            Assert.All(_preScanMilliseconds.Values.SelectMany(values => values), value =>
                Assert.InRange(value, 0, 119_999));
            Assert.All(_codeGraphMilliseconds.Values.SelectMany(values => values), value =>
                Assert.InRange(value, 0, 29_999));
            Assert.All(_javaMilliseconds.Values.SelectMany(values => values), value =>
                Assert.InRange(value, 0, 59_999));
        }

        private void WriteTimings()
        {
            foreach (RepositoryEntity repository in _repositories.OrderBy(value => value.Id))
            {
                _output.WriteLine(
                    $"Repository={repository.Id:D}; PreScanMs={Last(_preScanMilliseconds, repository.Id)}; " +
                    $"CodeGraphMs={Last(_codeGraphMilliseconds, repository.Id)}; JdtLsMs={Last(_javaMilliseconds, repository.Id)}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            _factory.Dispose();
            _baseFactory.Dispose();
        }

        private static long Last(Dictionary<Guid, List<long>> values, Guid id) =>
            values.TryGetValue(id, out List<long>? timings) && timings.Count > 0 ? timings[^1] : -1;

        private static void AddTiming(Dictionary<Guid, List<long>> values, Guid id, long elapsed)
        {
            if (!values.TryGetValue(id, out List<long>? timings))
            {
                timings = [];
                values.Add(id, timings);
            }
            timings.Add(elapsed);
        }
    }

    private static IReadOnlyList<PreparedSnapshotFile> CreateReleaseFiles(
        PreparedRevisionSnapshot snapshot,
        bool reverse,
        bool includeInventoryEdgeCases = false,
        bool includeJavaRangeQualification = false,
        bool includeOversizedXml = true,
        bool includeEmptyJavaReadinessCandidate = false,
        bool reviewedPair = false,
        bool reviewedAccountRepository = false,
        ReviewedSeedFixtureMode reviewedSeedFixtureMode = ReviewedSeedFixtureMode.None)
    {
        var files = new List<PreparedSnapshotFile>(61);
        for (int index = 0; index < 60; index++)
        {
            bool xml = index % 2 != 0;
            string path = xml ? $"src/release/config-{index:00}.xml" : $"src/release/Entry{index:00}.java";
            string source = xml
                ? $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><configuration><endpoint url=\"https://service-{index}.invalid/api\" /></configuration>"
                : $"package release; public final class Entry{index:00} {{ public static final int VALUE = {index}; }}";
            files.Add(File(path, xml ? "XML" : "Java", source));
        }
        var oversized = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><configuration>");
        for (int index = 0; index < 4_000; index++)
        {
            oversized.Append("<endpoint url=\"https://oversized.invalid/api/")
                .Append(index)
                .Append("\" />\n");
        }
        oversized.Append("</configuration>");
        if (includeOversizedXml)
        {
            files.Add(File(ReleaseHarness.OversizedXmlPath, "XML", oversized.ToString()));
        }
        if (includeJavaRangeQualification)
        {
            files.Add(File(
                ReleaseHarness.OversizedJavaPath,
                "Java",
                CreateOversizedJavaSource()));
        }
        if (includeEmptyJavaReadinessCandidate)
        {
            files.Add(File("src/release/A-Empty.java", "Java", string.Empty));
        }
        if (includeInventoryEdgeCases)
        {
            files.Add(new PreparedSnapshotFile(
                ReleaseHarness.FilteredPath,
                "Java",
                false,
                "ExcludedTestPath",
                null,
                null,
                0,
                0,
                null,
                ReadOnlyMemory<byte>.Empty));
            files.Add(File(
                ReleaseHarness.InvalidSplitPath,
                "XML",
                CreateInvalidSplitXml()));
            files.Add(File(
                ReleaseHarness.UnsafeXmlPath,
                "XML",
                "<!DOCTYPE configuration [<!ENTITY xxe SYSTEM \"file:///secret\">]><configuration><endpoint url=\"&xxe;\" /></configuration>"));
            files.Add(File(
                ReleaseHarness.HardBoundXmlPath,
                "XML",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><configuration>" +
                new string('x', PreparedSourceRangePlanner.MaximumRawBytes + 1) +
                "</configuration>"));
            files.Add(new PreparedSnapshotFile(
                ReleaseHarness.BinaryJavaPath,
                "Java",
                false,
                "BinaryJava",
                null,
                null,
                4,
                null,
                null,
                ReadOnlyMemory<byte>.Empty));
        }

        if (reviewedPair)
        {
            string[] requiredPaths = reviewedAccountRepository
                ? [
                    "src/main/java/cn/com/yitong/actions/mobile/UpdateChannelSessionOp.java",
                    "src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java",
                ]
                : [
                    "src/main/resources/disgram/in/cust/updateSession.xml",
                    "src/main/java/cn/com/yitong/actions/domain/login/UpdateSessionOp.java",
                ];
            for (int index = 0; index < requiredPaths.Length; index++)
            {
                string path = requiredPaths[index];
                bool omit = reviewedSeedFixtureMode == ReviewedSeedFixtureMode.Missing && index == 0;
                if (omit)
                {
                    continue;
                }

                string language = path.EndsWith(".xml", StringComparison.Ordinal) ? "XML" : "Java";
                string source = language == "XML"
                    ? "<?xml version=\"1.0\" encoding=\"UTF-8\"?><configuration><trans name=\"in/cust/updateSession\" /></configuration>"
                    : "package reviewed; public final class Required {}";
                PreparedSnapshotFile required = File(path, language, source);
                if (reviewedSeedFixtureMode == ReviewedSeedFixtureMode.Filtered)
                {
                    required = required with { IsEligible = false, Outcome = "ExcludedTestPath" };
                }
                else if (reviewedSeedFixtureMode == ReviewedSeedFixtureMode.Unsafe && language == "XML")
                {
                    required = new PreparedSnapshotFile(
                        path,
                        language,
                        true,
                        "EligibleProductionSource",
                        required.FileHash,
                        required.SourceHash,
                        required.RawByteCount,
                        required.LogicalLineCount,
                        required.EncodingIdentity,
                        Encoding.UTF8.GetBytes(
                            "<!DOCTYPE configuration [<!ENTITY xxe SYSTEM \"file:///secret\">]>" +
                            "<configuration><endpoint url=\"&xxe;\" /></configuration>"));
                }
                else if (reviewedSeedFixtureMode == ReviewedSeedFixtureMode.NonContained)
                {
                    required = required with { RelativePath = "../outside/required.java" };
                }
                else if (reviewedSeedFixtureMode == ReviewedSeedFixtureMode.EmptyHash)
                {
                    required = required with { FileHash = null, SourceHash = null };
                }
                else if (reviewedSeedFixtureMode == ReviewedSeedFixtureMode.SplitInvalid && language == "XML")
                {
                    required = File(path, language, CreateInvalidSplitXml());
                }

                files.Add(required);
                if (reviewedSeedFixtureMode is ReviewedSeedFixtureMode.Duplicate or ReviewedSeedFixtureMode.Ambiguous)
                {
                    files.Add(reviewedSeedFixtureMode == ReviewedSeedFixtureMode.Ambiguous
                        ? File(path, language, source + " // ambiguous")
                        : required);
                }
            }

            if (reviewedSeedFixtureMode == ReviewedSeedFixtureMode.MoreThanTwentySegments && !reviewedAccountRepository)
            {
                string path = "src/main/resources/disgram/in/cust/updateSession.xml";
                StringBuilder source = new("<?xml version=\"1.0\" encoding=\"UTF-8\"?><configuration>");
                for (int index = 0; index < 21; index++)
                {
                    source.Append("<entry name=\"").Append(index).Append("\">")
                        .Append(new string('x', 4_000))
                        .Append("</entry>");
                }
                source.Append("</configuration>");
                files.RemoveAll(file => file.RelativePath == path);
                files.Add(File(path, "XML", source.ToString()));
            }

            if (reviewedSeedFixtureMode == ReviewedSeedFixtureMode.FinalSelectionOmission && reviewedAccountRepository)
            {
                for (int index = 0; index < 140; index++)
                {
                    files.Add(File($"src/optional/overflow-{index:D3}.java", "Java", "package overflow; final class Optional {}"));
                }
            }
        }
        return reverse ? [.. files.AsEnumerable().Reverse()] : files;
    }

    private static PreparedSnapshotFile File(string path, string language, string source)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(source);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new(path, language, true, "EligibleProductionSource", hash, hash, bytes.LongLength,
            source.Count(character => character == '\n') + 1, "utf-8", bytes);
    }

    private static CommunicationPatternCatalog CreateCatalog() => new(
        "communication-pattern-catalog/1.0",
        "release-qualification-catalog",
        "1",
        Hash("release-qualification-catalog"),
        [
            new CommunicationCatalogRule(
                "http-endpoint",
                "src/release/*.xml",
                [new("", "endpoint")],
                null,
                [],
                new(CommunicationCatalogExtractionKind.Attribute, "", "url"),
                null,
                CommunicationKind.Http,
                EvidenceRule.HttpMatchedConfiguration,
                EvidenceProvenance.MatchedConfiguration),
        ]);

    private static PreparedCodeGraphProofResult CompleteCodeGraph(PreparedCodeGraphProofRequest request) => new(
        AnalysisOperationStatus.Succeeded,
        new CodeGraphCompletenessProof(
            request.Scope.RepositoryId, request.Scope.ScanRunId, request.Scope.ProfiledRevision,
            request.SnapshotIdentity, request.SnapshotRootBindingHash, request.Scope.RuntimeIdentityHash,
            "release-index", "codegraph-completeness-proof/1.0", true, CodeGraphIndexState.Complete,
            0, 0, 0, false, false, 0, 61, request.LogicalProjectBinding, request.LogicalIndexBinding,
            true, true, true, true, true, true, true, Hash("codegraph-proof"), 0),
        null);

    private static PreparedJavaReadinessResult ReadyJava(PreparedJavaReadinessRequest request) => new(
        AnalysisOperationStatus.Succeeded,
        new JavaReadinessProof(
            request.Scope.RepositoryId, request.Scope.ScanRunId, request.Scope.ProfiledRevision,
            request.SnapshotIdentity, request.Scope.RuntimeIdentityHash, request.WorkspaceBindingHash,
            "java-readiness/1.0", JavaReadinessOutcome.Ready,
            "jdt-language-server-1.61.0-202608141332", Hash("jdt-archive"), 21, "2.25.29",
            AnalysisTransport.Stdio, true, request.EligibleJavaRelativePaths.Order(StringComparer.Ordinal).First(),
            true, true, JavaImportState.Usable, 30, true, true, true, true, true, Hash("java-ready"), 0),
        null);

    private static async Task<(SystemEntity, RepositoryEntity[], PreparedScanRepositoryRevisionBinding[])> SeedAsync(
        VulgataDbContext db,
        bool reviewedPair = false)
    {
        DateTimeOffset now = PreparedScanTestHost.FixedUtcNow;
        string firstRevision = reviewedPair
            ? "19c60cd3f3976b7697c487c7bafd96142e3a7625"
            : "0123456789abcdef0123456789abcdef01234567";
        string secondRevision = reviewedPair
            ? "47f6b627a7598f862873610413eec1e1e7bf46bf"
            : "fedcba9876543210fedcba9876543210fedcba98";
        string firstRef = reviewedPair ? "refs/tags/vulgata/August15th-sample" : "refs/tags/release-a";
        string secondRef = reviewedPair ? "refs/tags/vulgata/August15th-sample" : "refs/tags/release-b";
        SystemEntity system = new("Release qualification system", "neutral", "global", now);
        RepositoryEntity first = system.AddRepository(
            reviewedPair ? "mbs_account" : "release-one",
            "https://example.invalid/release-one.git",
            null,
            "one",
            now);
        RepositoryEntity second = system.AddRepository(
            reviewedPair ? "mbs_channel" : "release-two",
            "https://example.invalid/release-two.git",
            null,
            "two",
            now);
        ScanRun firstRun = ScanRun.Start(first.Id, 1_800, now);
        ScanRun secondRun = ScanRun.Start(second.Id, 1_800, now);
        firstRun.BindProfiledRevision(firstRevision);
        secondRun.BindProfiledRevision(secondRevision);
        ScanContextSnapshot firstContext = ScanContextSnapshot.CapturePrepared(firstRun, "global", "system", first.Context, now);
        ScanContextSnapshot secondContext = ScanContextSnapshot.CapturePrepared(secondRun, "global", "system", second.Context, now);
        PreScanResult firstPreScan = PreScanResult.CreateApproved(first.Id, firstRun.Id, firstRevision, now,
            new('f', 64), "预扫描已批准——继续进入顺序演示运行器", ConfirmedCommunicationKinds.None, 0, 0, 0, false, []);
        PreScanResult secondPreScan = PreScanResult.CreateApproved(second.Id, secondRun.Id, secondRevision, now,
            new('1', 64), "预扫描已批准——继续进入顺序演示运行器", ConfirmedCommunicationKinds.None, 0, 0, 0, false, []);
        ScanExecution firstExecution = ScanExecution.CreateNewPath(
            Guid.NewGuid(), first.Id, system.Id, firstRun.Id, firstRevision,
            PromptIdentity, DocumentSchemaIdentity, RuntimeIdentityJson, RuntimeIdentityHash, now);
        ScanExecution secondExecution = ScanExecution.CreateNewPath(
            Guid.NewGuid(), second.Id, system.Id, secondRun.Id, secondRevision,
            PromptIdentity, DocumentSchemaIdentity, RuntimeIdentityJson, RuntimeIdentityHash, now);
        TwoRepositoryScanCoordination coordination = TwoRepositoryScanCoordination.Create(firstExecution, secondExecution, now);
        db.Systems.Add(system);
        db.ScanRuns.AddRange(firstRun, secondRun);
        db.ScanContextSnapshots.AddRange(firstContext, secondContext);
        db.PreScanResults.AddRange(firstPreScan, secondPreScan);
        db.ScanExecutions.AddRange(firstExecution, secondExecution);
        db.TwoRepositoryScanCoordinations.Add(coordination);
        await db.SaveChangesAsync();
        return (system, [first, second],
        [
            new() { RepositoryId = first.Id, GitRef = firstRef, ProfiledRevision = firstRevision },
            new() { RepositoryId = second.Id, GitRef = secondRef, ProfiledRevision = secondRevision },
        ]);
    }

    private static PreparedScanInitiationTuple CreateInitiationTuple(
        VulgataDbContext db,
        SystemEntity system,
        IReadOnlyList<RepositoryEntity> repositories,
        IReadOnlyList<PreparedScanRepositoryRevisionBinding> bindings)
    {
        TwoRepositoryScanCoordination coordination = db.TwoRepositoryScanCoordinations
            .Single(value => value.SystemId == system.Id);
        PreparedScanInitiationRepositoryTuple[] tupleRepositories = repositories
            .Select(repository =>
            {
                PreparedScanRepositoryRevisionBinding binding = bindings.Single(
                    value => value.RepositoryId == repository.Id);
                ScanExecution execution = db.ScanExecutions.Single(
                    value => value.RepositoryId == repository.Id);
                ScanRun run = db.ScanRuns.Single(value => value.Id == execution.ScanRunId);
                return new PreparedScanInitiationRepositoryTuple(
                    repository.Id,
                    binding.GitRef,
                    binding.ProfiledRevision,
                    run.Id,
                    execution.Id,
                    execution.Phase,
                    execution.PromptIdentity!,
                    execution.DocumentSchemaIdentity!,
                    execution.AnalysisRuntimeIdentityJson!,
                    execution.AnalysisRuntimeIdentityHash!,
                    execution.PreScanResultId);
            })
            .ToArray();
        return new(
            system.Id,
            new string('a', 64),
            coordination.CreatedAt,
            coordination.Id,
            coordination.StateVersion,
            coordination.Phase,
            PreparedScanArtifactPresence.None,
            tupleRepositories);
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string HashRawInterval(string source, long byteStart, long byteEndExclusive)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(source);
        return Convert.ToHexStringLower(SHA256.HashData(
            bytes.AsSpan(checked((int)byteStart), checked((int)(byteEndExclusive - byteStart)))));
    }

    private static string CreateOversizedJavaSource() =>
        "package release;\n" +
        "public final class LargeEntryA {/*" + new string('a', 40_000) + "*/}\n" +
        "public final class LargeEntryB {/*" + new string('b', 40_000) + "*/}\n";

    private static string CreateInvalidSplitXml()
    {
        var source = new StringBuilder("<configuration><payload>");
        for (int index = 0; index < 10_000; index++)
        {
            source.Append("<item />");
        }

        return source.Append("</payload></configuration>").ToString();
    }
}
