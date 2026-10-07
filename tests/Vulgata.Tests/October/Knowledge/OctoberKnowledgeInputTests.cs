using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberKnowledgeInputTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ComputeProposalDigest_NullAdditiveFields_PreservesLegacyGoldenBytes()
    {
        var origin = new PreparedOrigin(Id(2), "root", "src/A.cs", new string('b', 64), "v1");
        var contributor = new Contributor(ContributorKind.Source, Id(2), null, "root", null, origin.SourceDigest);
        const string expected = """{"advisoryRecordId":null,"contentDigest":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","kind":0,"repositoryId":"00000000-0000-0000-0000-000000000002","rootBindingIdentity":"root","systemId":null}""";
        Assert.Equal(expected, OctoberKnowledgePolicy.SerializeCanonical(contributor));
        var proposal = new CompletedFileProposal(new("db", "target", Id(1), new string('a', 64)), Id(3), Id(4), origin, "", [], [], [], [contributor], new(Id(3), origin, CoverageDisposition.CompletedZeroUnit, "No units"));
        // Independently hashed pre-addition literal canonical object, not output of the production codec.
        Assert.Equal("904B481E45BF823535F79A557A496254CAB76D9B064254387C30A3276C79A6D9", OctoberKnowledgePolicy.ComputeProposalDigest(proposal));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ComputeMetadataResultDigest_ExactCanonicalGolden_BindsRequestProjectionAndPreservesOrder()
    {
        var origin = new SourceMetadataOrigin(Id(2), "root", "v1", SourceMetadataOperation.SearchText, "src", "excluded");
        const string request = """{"query":"abc","maxResults":2}""";
        const string projection = """{"status":"Partial","locations":["src/B.cs","src/A.cs"],"limitations":["bounded"]}""";
        string digest = OctoberKnowledgeInputPolicy.ComputeMetadataResultDigest(origin, request, projection);
        Assert.Equal("C5238379262E18923E4D8BA36EB1EFEF870686BA4419A39CF4B3C34DE30646DB", digest);
        Assert.Equal(digest, OctoberKnowledgeInputPolicy.ComputeMetadataResultDigest(origin with { ResultDigest = "different excluded self field" }, """{"maxResults":2,"query":"abc"}""", projection));
        Assert.NotEqual(digest, OctoberKnowledgeInputPolicy.ComputeMetadataResultDigest(origin, """{"query":"abcd","maxResults":2}""", projection));
        Assert.NotEqual(digest, OctoberKnowledgeInputPolicy.ComputeMetadataResultDigest(origin, request, """{"status":"Partial","locations":["src/A.cs","src/B.cs"],"limitations":["bounded"]}"""));
        Assert.NotEqual(digest, OctoberKnowledgeInputPolicy.ComputeMetadataResultDigest(origin with { PreparedVersion = "v2" }, request, projection));
        Assert.NotEqual(digest, OctoberKnowledgeInputPolicy.ComputeMetadataResultDigest(origin with { NormalizedScopePath = "." }, request, projection));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(true)]
    [InlineData(false)]
    public void ComputeMetadataResultDigest_CanonicalRequestAndProjectionBounds_RejectsOverflowWithoutTruncation(bool requestBound)
    {
        var origin = Metadata(new(Id(2), "root", "src/A.cs", new string('b', 64), "v1"));
        int ceiling = requestBound ? OctoberKnowledgeInputLimits.MaximumSafeRequestUtf8Bytes : OctoberKnowledgeInputLimits.MaximumMetadataProjectionUtf8Bytes;
        string exact = JsonSerializer.Serialize(new { p = new string('x', ceiling - 8) });
        Assert.Equal(ceiling, Encoding.UTF8.GetByteCount(exact));
        Assert.Equal(64, OctoberKnowledgeInputPolicy.ComputeMetadataResultDigest(origin, requestBound ? exact : "{}", requestBound ? "{}" : exact).Length);
        string overflow = JsonSerializer.Serialize(new { p = new string('x', ceiling - 7) });
        AssertInvalidHelper(() => OctoberKnowledgeInputPolicy.ComputeMetadataResultDigest(origin, requestBound ? overflow : "{}", requestBound ? "{}" : overflow));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("array")]
    [InlineData("duplicate")]
    [InlineData("unicode")]
    [InlineData("absolute")]
    [InlineData("dot-segment")]
    [InlineData("null-scope")]
    [InlineData("operation")]
    public void ComputeMetadataResultDigest_MalformedOrUncontainedInput_FailsClosed(string mutation)
    {
        var origin = Metadata(new(Id(2), "root", "src/A.cs", new string('b', 64), "v1"));
        string request = "{}";
        if (mutation == "array") request = "[]";
        if (mutation == "duplicate") request = """{"query":"a","query":"b"}""";
        if (mutation == "unicode") request = "{\"query\":\"" + (char)0xD800 + "\"}";
        if (mutation == "absolute") origin = origin with { NormalizedScopePath = "C:/outside" };
        if (mutation == "dot-segment") origin = origin with { NormalizedScopePath = "src/../outside" };
        if (mutation == "null-scope") origin = origin with { NormalizedScopePath = null! };
        if (mutation == "operation") origin = origin with { Operation = (SourceMetadataOperation)999 };
        AssertInvalidHelper(() => OctoberKnowledgeInputPolicy.ComputeMetadataResultDigest(origin, request, "{}"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_PrimaryAliasAndSameBytesAtDistinctPaths_DeduplicatesOnlyExactPrimaryAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var support = fixture.Origin with { RelativePath = "src/Support.cs" };
        var verifier = new InputSourceVerifier(fixture.Origin, support);
        var legacy = fixture.Proposal;
        var aliased = legacy with { Contributors = legacy.Contributors.Add(fixture.SourceContributor with { SourceOrigin = fixture.Origin with { RelativePath = @"src\Order.cs" } }) };
        Assert.Equal(OctoberKnowledgePolicy.ComputeProposalDigest(legacy), OctoberKnowledgePolicy.ComputeProposalDigest(aliased));
        var proposal = aliased with { Contributors = aliased.Contributors.Add(Source(support)) };
        Assert.NotEqual(OctoberKnowledgePolicy.ComputeProposalDigest(legacy), OctoberKnowledgePolicy.ComputeProposalDigest(proposal));
        var result = await Writer(fixture, verifier).AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        Assert.Equal(2, verifier.Calls.Count);
        Assert.Contains(support, verifier.Calls);
        await using var context = fixture.Factory.CreateDbContext();
        var stored = await context.KnowledgeContributors.Select(row => row.ContributorJson).ToListAsync();
        Assert.Equal(3, stored.Count);
        Assert.Single(stored.Where(json => json.Contains("Support.cs", StringComparison.Ordinal)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_VerifiedSupportingAnchors_AreAllowedOnlyInEvidenceNotUnitOwnershipAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var support = fixture.Origin with { RelativePath = "src/Support.cs" };
        var verifier = new InputSourceVerifier(fixture.Origin, support);
        var proposal = SupportingEvidence(fixture.Proposal, support);
        var result = await Writer(fixture, verifier).AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.All(await context.DocumentVersions.ToListAsync(), row => Assert.Contains("Support.cs", row.ContentJson, StringComparison.Ordinal));
        Assert.Equal(fixture.Origin.RelativePath, (await context.AcceptedFileManifests.SingleAsync()).RelativePath);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("uncited-unverified")]
    [InlineData("wrong-root")]
    [InlineData("wrong-version")]
    [InlineData("line-overflow")]
    [InlineData("unit-owner")]
    [InlineData("missing-full-origin")]
    public async Task AcceptCompletedFileAsync_InexactSupportingContent_RejectsWholeFileAsync(string mutation)
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var support = fixture.Origin with { RelativePath = "src/Support.cs" };
        var verifier = new InputSourceVerifier(fixture.Origin, support);
        var proposal = SupportingEvidence(fixture.Proposal, support);
        if (mutation == "uncited-unverified")
        {
            verifier.Allowed.Remove(support);
            proposal = fixture.Proposal with { Contributors = fixture.Proposal.Contributors.Add(Source(support)) };
        }
        if (mutation == "wrong-root") verifier.Allowed[support] = new(support with { RootBindingIdentity = "foreign-root" }, 100);
        if (mutation == "wrong-version") verifier.Allowed[support] = new(support with { PreparedVersion = "other-session" }, 100);
        if (mutation == "line-overflow") verifier.Allowed[support] = new(support, 1);
        if (mutation == "unit-owner") proposal = proposal with { Units = proposal.Units.SetItem(0, proposal.Units[0] with { Anchors = [new(support, 1, 2)] }) };
        if (mutation == "missing-full-origin") proposal = proposal with { Contributors = fixture.Proposal.Contributors };
        var result = await Writer(fixture, verifier).AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, result.Status);
        Assert.Null(result.AcceptedFile);
        await fixture.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public async Task AcceptCompletedFileAsync_TotalContentOriginCap_IncludesPrimaryAndNeverTruncatesAsync(int total, bool accepted)
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var supports = Enumerable.Range(1, total - 1).Select(index => fixture.Origin with { RelativePath = $"support/File{index}.cs" }).ToArray();
        var verifier = new InputSourceVerifier([fixture.Origin, .. supports]);
        var proposal = fixture.Proposal with { Contributors = fixture.Proposal.Contributors.AddRange(supports.Select(Source)) };
        var result = await Writer(fixture, verifier).AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(accepted ? KnowledgeWriteStatus.Committed : KnowledgeWriteStatus.InvalidProposal, result.Status);
        if (accepted)
        {
            Assert.Equal(total, verifier.Calls.Distinct().Count());
            await using var context = fixture.Factory.CreateDbContext();
            Assert.Equal(total + 1, await context.KnowledgeContributors.CountAsync());
        }
        else await fixture.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("List")]
    [InlineData("SearchText")]
    public async Task AcceptCompletedFileAsync_TrustedMissingOrDirectoryMetadata_RetainsDependencyWithoutHashingMetadataAsFileAsync(string operation)
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var metadata = Metadata(fixture.Origin) with { Operation = Enum.Parse<SourceMetadataOperation>(operation), NormalizedScopePath = operation == "List" ? "." : "missing.txt" };
        var contributor = MetadataContributor(metadata);
        var ledger = new InputCompletionLedger(fixture.Completion, [metadata]);
        var verifier = new InputSourceVerifier(fixture.Origin);
        var proposal = fixture.Proposal with
        {
            Contributors = fixture.Proposal.Contributors.Add(contributor),
            Uncertainties = fixture.Proposal.Uncertainties.Add(new("metadata-limit", "The bounded metadata result does not establish absence of source behavior.", [], ["No source-content or completeness proof"]))
        };
        var result = await Writer(fixture, verifier, ledger).AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        Assert.Equal(fixture.Origin, Assert.Single(verifier.Calls));
        Assert.Contains(contributor, fixture.Authorization.LastContributors);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Contains(await context.KnowledgeContributors.Select(row => row.ContributorJson).ToListAsync(), json => json.Contains("metadataOrigin", StringComparison.Ordinal));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptCompletedFileAsync_MissingOrForgedTrustedMetadataLedger_RejectsOtherwiseWellShapedRecordAsync(bool forged)
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var metadata = Metadata(fixture.Origin);
        var ledger = new InputCompletionLedger(fixture.Completion, forged ? [metadata with { ResultDigest = new string('e', 64) }] : []);
        var proposal = fixture.Proposal with { Contributors = fixture.Proposal.Contributors.Add(MetadataContributor(metadata)) };
        var result = await Writer(fixture, new(fixture.Origin), ledger).AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(KnowledgeWriteStatus.Denied, result.Status);
        Assert.Equal(1, ledger.Calls);
        await fixture.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("advisory-source")]
    [InlineData("metadata-source")]
    [InlineData("metadata-advisory")]
    [InlineData("mirror")]
    [InlineData("source-proof")]
    public async Task AcceptCompletedFileAsync_MixedOrUpgradedMetadataFields_FailsClosedAsync(string mutation)
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var metadata = Metadata(fixture.Origin);
        var contributor = MetadataContributor(metadata);
        var proposal = fixture.Proposal;
        if (mutation == "advisory-source") contributor = fixture.AdvisoryContributor with { SourceOrigin = fixture.Origin };
        if (mutation == "metadata-source") contributor = contributor with { SourceOrigin = fixture.Origin };
        if (mutation == "metadata-advisory") contributor = contributor with { AdvisoryRecordId = OctoberKnowledgeAcceptanceFixture.Id(303) };
        if (mutation == "mirror") contributor = contributor with { ContentDigest = new string('e', 64) };
        if (mutation == "source-proof")
        {
            var fake = fixture.Origin with { RelativePath = "missing.txt", SourceDigest = metadata.ResultDigest };
            proposal = SupportingEvidence(proposal, fake) with { Contributors = fixture.Proposal.Contributors };
        }
        proposal = proposal with { Contributors = proposal.Contributors.Add(contributor) };
        var result = await Writer(fixture, new(fixture.Origin), new InputCompletionLedger(fixture.Completion, [metadata])).AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, result.Status);
        await fixture.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LookupOperationAsync_StoredSupportAndMetadata_ReauthorizesUnionWithoutExpiredSourceRereadAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var support = fixture.Origin with { RelativePath = "src/Uncited.cs" };
        var metadata = Metadata(fixture.Origin);
        var verifier = new InputSourceVerifier(fixture.Origin, support);
        var writer = Writer(fixture, verifier, new InputCompletionLedger(fixture.Completion, [metadata]));
        var proposal = fixture.Proposal with { Contributors = fixture.Proposal.Contributors.Add(Source(support)).Add(MetadataContributor(metadata)) };
        var submission = fixture.Seal(proposal);
        var committed = await writer.AcceptCompletedFileAsync(submission);
        Assert.Equal(KnowledgeWriteStatus.Committed, committed.Status);
        int verificationCount = verifier.Calls.Count;
        verifier.Expired = true;
        fixture.Completion.Status = DisclosureStatus.Denied;
        var replay = await writer.AcceptCompletedFileAsync(submission);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(committed.AcceptedFile!.ManifestId, replay.AcceptedFile!.ManifestId);
        Assert.Equal(verificationCount, verifier.Calls.Count);
        Assert.Contains(Source(support), fixture.Authorization.LastContributors);
        Assert.Contains(MetadataContributor(metadata), fixture.Authorization.LastContributors);
        fixture.Authorization.Status = DisclosureStatus.Denied;
        var denied = await writer.LookupOperationAsync(fixture.Binding, submission.Operation, new string('f', 64));
        Assert.Equal(KnowledgeWriteStatus.Denied, denied.Status);
        Assert.Null(denied.AcceptedFile);
        Assert.Equal(verificationCount, verifier.Calls.Count);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckAsync_MetadataUsesLiveSourceScopeWithoutAdvisoryRequirement_ThenDeniesRemovedRepositoryAsync()
    {
        await using var fixture = await B0bIdentityFixture.CreateAsync();
        var origin = new SourceMetadataOrigin(fixture.RepositoryId, "controlled-root", "approved-session", SourceMetadataOperation.List, ".", new string('d', 64));
        var contributor = MetadataContributor(origin) with { SystemId = fixture.SystemId };
        var request = fixture.Request() with { AllContributors = [contributor] };
        Assert.Equal(DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(request));
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync();
            var repository = await context.Repositories.SingleAsync();
            repository.UpdateDetails(repository.Name, repository.GitUrl, repository.Description, null, DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();
        }
        Assert.Equal(DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(request));
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(request with { AllContributors = [contributor with { RootBindingIdentity = "foreign-root" }] }));
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(request with { Selection = request.Selection with { Mode = KnowledgeMode.Business } }));
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync();
            context.Repositories.Remove(await context.Repositories.SingleAsync());
            await context.SaveChangesAsync();
        }
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(request));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishedReadAsync_UncitedSupportingInputs_SurviveStorePublicationAndProtectedDisclosureAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var support = fixture.Origin with { RelativePath = "src/Uncited.cs" };
        var metadata = Metadata(fixture.Origin);
        var metadataContributor = MetadataContributor(metadata);
        var writer = Writer(fixture, new(fixture.Origin, support), new InputCompletionLedger(fixture.Completion, [metadata]));
        var proposal = fixture.Proposal with { Contributors = fixture.Proposal.Contributors.Add(Source(support)).Add(metadataContributor) };
        var accepted = await writer.AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var freeze = new CorpusFreezeRequest(new(fixture.Binding.BatchId, OperationKind.FreezeCorpus, Id(951)), fixture.Binding, "", [accepted.AcceptedFile!], [proposal.Coverage]);
        freeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
        fixture.FreezeAuthority.Approve(freeze);
        var corpus = await writer.FreezeCorpusAsync(freeze);
        Assert.Equal(KnowledgeWriteStatus.Committed, corpus.Status);
        var publish = new PublicationRequest(new(fixture.Binding.BatchId, OperationKind.PublishSelection, Id(952)), corpus.Corpus!, "", accepted.AcceptedFile!.Documents, [], [], [], [proposal.Coverage]);
        publish = publish with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(publish) };
        var published = await writer.PublishSelectionAsync(publish);
        Assert.Equal(KnowledgeWriteStatus.Committed, published.Status);
        var source = new OctoberKnowledgePublishedSource(fixture.Factory);
        var stored = await source.LoadAsync(published.Publication!);
        Assert.NotNull(stored);
        Assert.All(stored.Documents, document => { Assert.Contains(Source(support), document.Contributors); Assert.Contains(metadataContributor, document.Contributors); });
        var keys = new EphemeralDataProtectionProvider();
        var policy = new DisclosureTestPolicy();
        var authority = new OctoberKnowledgeDependencyAuthority(policy, new OctoberKnowledgeDisclosureDataProtectionProtector(keys));
        var reader = new OctoberKnowledgeReader(source, published.Publication, policy, new(new OctoberKnowledgeDataProtectionTokenProtector(keys)), new B0bReadLog(), authority);
        var request = new ReadSelectionRequest(published.Publication!, new("ordinary-user", "controlled-stamp"), KnowledgeMode.IT, KnowledgePurpose.Chat, [fixture.Origin.RepositoryId]);
        var selection = await reader.OpenReadSelectionAsync(request);
        Assert.Equal(KnowledgeReadStatus.Success, selection.Status);
        var read = await ((IOctoberKnowledgeDisclosedReader)reader).SearchAsync(new(selection.Selection!, ""), new(65536, 262144, new ActualChatDisclosureEncoder()));
        Assert.Equal(KnowledgeReadStatus.Success, read.Status);
        policy.DeniedContributor = metadataContributor;
        Assert.Equal(KnowledgeDependencyStatus.Denied, await authority.ReauthorizeAsync(request, [read.Disclosure!.Dependencies]));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public async Task AcceptCompletedFileAsync_MetadataObservationCap_DoesNotReadLocationsOrTruncateLedgerAsync(int count, bool accepted)
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var metadata = Enumerable.Range(0, count).Select(index => Metadata(fixture.Origin) with { NormalizedScopePath = $"directory/item{index}" }).ToImmutableArray();
        var proposal = fixture.Proposal with { Contributors = fixture.Proposal.Contributors.AddRange(metadata.Select(MetadataContributor)) };
        var verifier = new InputSourceVerifier(fixture.Origin);
        var result = await Writer(fixture, verifier, new InputCompletionLedger(fixture.Completion, metadata)).AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(accepted ? KnowledgeWriteStatus.Committed : KnowledgeWriteStatus.InvalidProposal, result.Status);
        if (accepted)
        {
            Assert.Equal(fixture.Origin, Assert.Single(verifier.Calls));
            await using var context = fixture.Factory.CreateDbContext();
            Assert.Equal(count + fixture.Proposal.Contributors.Length, await context.KnowledgeContributors.CountAsync());
        }
        else await fixture.AssertNoKnowledgeAsync();
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AcceptCompletedFileAsync_SupportOutsidePrimaryRepositoryOrRoot_RejectsBeforeSupportVerificationAsync(bool sameRepository, bool metadataOnly)
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var outside = fixture.Origin with
        {
            RepositoryId = sameRepository ? fixture.Origin.RepositoryId : Id(960),
            RootBindingIdentity = "other-authorized-root",
            RelativePath = "src/Supporting.cs"
        };
        if (!sameRepository)
        {
            await using var setup = fixture.Factory.CreateDbContext();
            setup.RepositorySelections.Add(new()
            {
                BatchId = fixture.Binding.BatchId,
                RepositoryId = outside.RepositoryId,
                RootBindingIdentity = outside.RootBindingIdentity,
                OperatorLabel = "independently selected and permitted repository"
            });
            await setup.SaveChangesAsync();
        }
        var verifier = new InputSourceVerifier(fixture.Origin, outside);
        var metadata = Metadata(outside);
        var contributor = metadataOnly ? MetadataContributor(metadata) : Source(outside);
        var ledger = new InputCompletionLedger(fixture.Completion, [metadata]);
        var proposal = fixture.Proposal with { Contributors = fixture.Proposal.Contributors.Add(contributor) };
        Assert.Equal(DisclosureStatus.Allowed, fixture.Authorization.Status);
        var result = await Writer(fixture, verifier, ledger).AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, result.Status);
        Assert.Null(result.AcceptedFile);
        Assert.DoesNotContain(outside, verifier.Calls);
        Assert.Equal(0, fixture.Guard.Submissions);
        await fixture.AssertNoKnowledgeAsync();
    }
    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    private static SourceMetadataOrigin Metadata(PreparedOrigin origin) => new(origin.RepositoryId, origin.RootBindingIdentity, origin.PreparedVersion, SourceMetadataOperation.List, ".", new string('d', 64));
    private static Contributor Source(PreparedOrigin origin) => new(ContributorKind.Source, origin.RepositoryId, null, origin.RootBindingIdentity, null, origin.SourceDigest) { SourceOrigin = origin };
    private static Contributor MetadataContributor(SourceMetadataOrigin origin) => new(ContributorKind.SourceMetadata, origin.RepositoryId, null, origin.RootBindingIdentity, null, origin.ResultDigest) { MetadataOrigin = origin };
    private static OctoberKnowledgeWriter Writer(OctoberKnowledgeAcceptanceFixture fixture, InputSourceVerifier verifier, IOctoberFileAcceptanceAuthority? completion = null) =>
        new(fixture.Factory, completion ?? fixture.Completion, verifier, fixture.Authorization, fixture.Guard, fixture.Log, fixture.FreezeAuthority);
    private static void AssertInvalidHelper(Action action)
    {
        var error = Record.Exception(action);
        Assert.True(error is ArgumentException or JsonException, $"Expected input-validation failure, got {error?.GetType().Name ?? "success"}.");
    }
    private static CompletedFileProposal SupportingEvidence(CompletedFileProposal proposal, PreparedOrigin support)
    {
        var evidence = new EvidenceReference(null, null, new(support, 1, 2), null, support.SourceDigest);
        var units = proposal.Units.Select(unit =>
        {
            var sections = unit.Sections.Select(section => section with { Claims = section.Claims.Select(claim => claim.Qualification == ClaimQualification.SourceSupported ? claim with { Evidence = [evidence] } : claim).ToImmutableArray() }).ToImmutableArray();
            return OctoberKnowledgeAcceptanceFixture.Unit(unit.SemanticSelector, unit.ParentSelector, unit.Title, unit.Anchors, sections);
        }).ToImmutableArray();
        return proposal with { Units = units, Observations = proposal.Observations.Select(item => item with { Evidence = [evidence] }).ToImmutableArray(), Uncertainties = proposal.Uncertainties.Select(item => item with { Evidence = [evidence] }).ToImmutableArray(), Contributors = proposal.Contributors.Add(Source(support)) };
    }
}

/// <summary>Controlled trusted source adapter verifies exact allowlisted content origins; metadata is never a file-read request.</summary>
internal sealed class InputSourceVerifier(params PreparedOrigin[] origins) : IOctoberKnowledgeSourceVerifier
{
    public Dictionary<PreparedOrigin, VerifiedKnowledgeSource> Allowed { get; } = origins.ToDictionary(origin => origin, origin => new VerifiedKnowledgeSource(origin, 100));
    public List<PreparedOrigin> Calls { get; } = [];
    public bool Expired { get; set; }
    public Task<VerifiedKnowledgeSource?> VerifyAsync(KnowledgeBinding binding, PreparedOrigin origin, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(origin);
        if (Expired) throw new InvalidOperationException("Expired source capability must not be reopened for accepted replay.");
        var normalized = origin with { RelativePath = origin.RelativePath.Replace('\\', '/') };
        return Task.FromResult(Allowed.GetValueOrDefault(normalized));
    }
}

/// <summary>Controlled host completion ledger authenticates exact metadata records independently of their structural shape.</summary>
internal sealed class InputCompletionLedger(TestCompletionAuthority inner, ImmutableArray<SourceMetadataOrigin> retained) : IOctoberFileAcceptanceAuthority
{
    public int Calls { get; private set; }
    public async Task<DisclosureStatus> VerifyAsync(CompletedFileSubmission submission, CancellationToken cancellationToken = default)
    {
        Calls++;
        var result = await inner.VerifyAsync(submission, cancellationToken);
        if (result != DisclosureStatus.Allowed) return result;
        return submission.Proposal.Contributors.Where(item => item.MetadataOrigin is not null).All(item => retained.Contains(item.MetadataOrigin!)) ? DisclosureStatus.Allowed : DisclosureStatus.Denied;
    }
}
