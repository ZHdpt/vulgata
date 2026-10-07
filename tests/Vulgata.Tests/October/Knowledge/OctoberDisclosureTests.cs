using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberDisclosureTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("a")]
    [InlineData("中文")]
    [InlineData("😀")]
    [InlineData("\u0022\\\n<>&")]
    public async Task ReadExactAsync_ActualChatSerializer_FitsWholeWrapperAndPreservesEveryCharacterAsync(string atom)
    {
        var fixture = new DisclosureFixture();
        int repetitions = atom == "a" ? 40000 : atom == "😀" ? 9000 : 6000;
        string text = "BEGIN" + string.Concat(Enumerable.Repeat(atom, repetitions)) + "END";
        fixture.SetText(text);
        var selection = await fixture.OpenAsync();
        long remaining = 262144;
        var accumulated = new StringBuilder();
        var pages = new List<KnowledgeDependencyHandle>();
        ContinuationToken? next = null;
        int count = 0;
        do
        {
            var result = await fixture.Disclosed.ReadExactAsync(new(selection, fixture.Handle, next), fixture.Budget(65536, remaining));
            var disclosure = Successful(result);
            byte[] actual = ActualChatDisclosureEncoder.Bytes(new(OctoberChatToolStatus.Succeeded, Content: disclosure.Page, References: []));
            Assert.Equal(actual, disclosure.OutputUtf8.ToArray());
            Assert.InRange(actual.Length, 1, (int)Math.Min(65536, remaining));
            Assert.Equal(DisclosureFixture.Hash(actual), disclosure.OutputDigest);
            Assert.Equal(fixture.Encoder.ProfileId, disclosure.OutputProfileId);
            using var parsed = JsonDocument.Parse(actual);
            Assert.Equal(JsonValueKind.Object, parsed.RootElement.ValueKind);
            Assert.Equal("Succeeded", parsed.RootElement.GetProperty("status").GetString());
            Assert.Equal(disclosure.Page.Content, parsed.RootElement.GetProperty("content").GetProperty("content").GetString());
            Assert.All(disclosure.Page.Claims, claim => Assert.Equal(ClaimQualification.SourceSupported, claim.Qualification));
            accumulated.Append(disclosure.Page.Content);
            pages.Add(disclosure.Dependencies);
            remaining -= actual.Length;
            next = disclosure.Page.Next;
            Assert.InRange(++count, 1, 100);
        } while (next is not null);
        Assert.Contains(text, accumulated.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, accumulated.ToString().Split("BEGIN", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, accumulated.ToString().Split("END", StringSplitOptions.None).Length - 1);
        var receipt = await fixture.Authority.BuildConsumedContentAsync(fixture.Request, fixture.Handle, pages.ToImmutableArray());
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, receipt.Status);
        Assert.False(receipt.Receipt!.IsPartial);
        Assert.Equal(count, receipt.Receipt.SuccessfulChunks);
        Assert.Equal(Encoding.UTF8.GetByteCount(accumulated.ToString()), receipt.Receipt.ContentBytes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchAsync_HostEnvelope_NeverSerializesProtectedMetadataIntoModelOutputAsync()
    {
        var fixture = new DisclosureFixture();
        var selection = await fixture.OpenAsync();
        var result = await fixture.Disclosed.SearchAsync(new(selection, ""), fixture.Budget());
        var disclosure = Successful(result);
        Assert.Equal("{}", JsonSerializer.Serialize(disclosure));
        string outward = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("Disclosure", outward, StringComparison.OrdinalIgnoreCase);
        string model = Encoding.UTF8.GetString(disclosure.OutputUtf8.AsSpan());
        foreach (string secret in new[] { disclosure.Dependencies.ProtectedPayload, disclosure.Dependencies.SetId.ToString(), disclosure.Dependencies.BindingDigest, fixture.Request.Subject.AuthenticationBinding })
            Assert.DoesNotContain(secret, model, StringComparison.Ordinal);
        Assert.Equal(ActualChatDisclosureEncoder.Bytes(new(OctoberChatToolStatus.Succeeded, Search: disclosure.Page, References: [])), disclosure.OutputUtf8.ToArray());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadExactAsync_InsufficientActualWrapperAllowance_RefusesWithoutDisclosureOrCreditAsync()
    {
        var fixture = new DisclosureFixture();
        var selection = await fixture.OpenAsync();
        foreach (var budget in new[] { fixture.Budget(65536, 1), fixture.Budget(1, 262144), fixture.Budget(65536, 0) })
        {
            var result = await fixture.Disclosed.ReadExactAsync(new(selection, fixture.Handle), budget);
            Assert.Equal(KnowledgeReadStatus.ReadBudgetExceeded, result.Status);
            Assert.Null(result.Disclosure);
        }
        var proof = await fixture.Authority.BuildConsumedContentAsync(fixture.Request, fixture.Handle, []);
        Assert.NotEqual(KnowledgeDependencyStatus.Succeeded, proof.Status);
        Assert.Null(proof.Receipt);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("revoked", KnowledgeReadStatus.Unavailable)]
    [InlineData("outage", KnowledgeReadStatus.UnavailableService)]
    [InlineData("log", KnowledgeReadStatus.UnavailableService)]
    [InlineData("cancel", KnowledgeReadStatus.Cancelled)]
    public async Task ReadExactAsync_FinalCheckFailure_NeverReturnsPageOrDependencyAsync(string failure, KnowledgeReadStatus expected)
    {
        var fixture = new DisclosureFixture();
        var selection = await fixture.OpenAsync();
        using var cancellation = new CancellationTokenSource();
        if (failure == "revoked") fixture.Data.Log.AfterAppend = () => fixture.Policy.Status = DisclosureStatus.Denied;
        if (failure == "outage") fixture.Data.Log.AfterAppend = () => fixture.Policy.Status = DisclosureStatus.AuthorityUnavailable;
        if (failure == "log") fixture.Data.Log.Succeeds = false;
        if (failure == "cancel") fixture.Data.Log.AfterAppend = cancellation.Cancel;
        var result = await fixture.Disclosed.ReadExactAsync(new(selection, fixture.Handle), fixture.Budget(), cancellation.Token);
        Assert.Equal(expected, result.Status);
        Assert.Null(result.Disclosure);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReauthorizeAsync_GraphPagesRetainUncitedAndRelationOnlyContributors_RequiresNoRereadAsync(bool relationOnly)
    {
        var fixture = new DisclosureFixture(KnowledgePurpose.Graph);
        var selection = await fixture.OpenAsync();
        var search = Successful(await fixture.Disclosed.SearchAsync(new(selection, ""), fixture.Budget()));
        var related = Successful(await fixture.Disclosed.GetRelatedAsync(new(selection, fixture.Handle), fixture.Budget()));
        Assert.NotEmpty(related.Page.Relationships);
        Assert.All(related.Page.Relationships, edge => Assert.NotNull(edge.RelationshipVersionId));
        fixture.Data.Source.Failure = new InvalidOperationException("Aggregate authorization must not reread stored Knowledge.");
        fixture.Policy.DeniedContributor = relationOnly ? fixture.RelationOnly : fixture.Uncited;
        Assert.Equal(KnowledgeDependencyStatus.Denied, await fixture.Authority.ReauthorizeAsync(fixture.Request, [search.Dependencies, related.Dependencies]));
        Assert.Contains(fixture.Policy.Requests, request => request.AllContributors.Contains(fixture.Policy.DeniedContributor));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task MergeAsync_AccumulatedHistoricalInputs_RetainsRootAndUncitedOriginsAcrossRoundsAsync()
    {
        var fixture = new DisclosureFixture();
        var selection = await fixture.OpenAsync();
        var root = await fixture.Authority.CreateNoKnowledgeRootAsync(fixture.Request, DisclosureFixture.Hash("exact-question-and-static-instructions"));
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, root.Status);
        var search = Successful(await fixture.Disclosed.SearchAsync(new(selection, ""), fixture.Budget()));
        var first = await fixture.Authority.MergeAsync(fixture.Request, [root.Dependencies!, search.Dependencies]);
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, first.Status);
        var content = Successful(await fixture.Disclosed.ReadExactAsync(new(selection, fixture.Handle), fixture.Budget()));
        var inherited = await fixture.Authority.MergeAsync(fixture.Request, [first.Dependencies!, content.Dependencies]);
        var flattened = await fixture.Authority.MergeAsync(fixture.Request, [root.Dependencies!, search.Dependencies, content.Dependencies]);
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, inherited.Status);
        Assert.Equal(flattened.Dependencies!.BindingDigest, inherited.Dependencies!.BindingDigest);
        fixture.Data.Source.Failure = new InvalidOperationException("Retained history cannot reconstruct lineage by rereading citations.");
        fixture.Policy.DeniedContributor = fixture.Uncited;
        Assert.Equal(KnowledgeDependencyStatus.Denied, await fixture.Authority.ReauthorizeAsync(fixture.Request, [inherited.Dependencies]));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CreateNoKnowledgeRootAsync_ExplicitRoot_IsDistinctFromMissingLineageAsync()
    {
        var fixture = new DisclosureFixture();
        foreach (var missing in new[] { ImmutableArray<KnowledgeDependencyHandle>.Empty, default })
        {
            Assert.Equal(KnowledgeDependencyStatus.UnknownLineage, await fixture.Authority.ReauthorizeAsync(fixture.Request, missing));
            var merged = await fixture.Authority.MergeAsync(fixture.Request, missing);
            Assert.Equal(KnowledgeDependencyStatus.UnknownLineage, merged.Status);
            Assert.Null(merged.Dependencies);
        }
        var first = await fixture.Authority.CreateNoKnowledgeRootAsync(fixture.Request, DisclosureFixture.Hash("question"));
        var replay = await fixture.Authority.CreateNoKnowledgeRootAsync(fixture.Request, DisclosureFixture.Hash("question"));
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, first.Status);
        Assert.Equal(first.Dependencies!.BindingDigest, replay.Dependencies!.BindingDigest);
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, await fixture.Authority.ReauthorizeAsync(fixture.Request, [first.Dependencies]));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReauthorizeAsync_ForgedForeignOrWrongPurposeCapsule_FailsClosedAsync()
    {
        var fixture = new DisclosureFixture();
        var selection = await fixture.OpenAsync();
        var page = Successful(await fixture.Disclosed.ReadExactAsync(new(selection, fixture.Handle), fixture.Budget()));
        var handle = page.Dependencies;
        foreach (var forged in new[] { handle with { SetId = Guid.NewGuid() }, handle with { BindingDigest = new string('0', 64) }, handle with { ProtectedPayload = handle.ProtectedPayload + "tamper" }, handle with { ProtectedPayload = fixture.Protector.ProtectConsumedContent("foreign-kind") } })
            Assert.Equal(KnowledgeDependencyStatus.InvalidProof, await fixture.Authority.ReauthorizeAsync(fixture.Request, [forged]));
        foreach (var foreign in new[] { fixture.Request with { Subject = fixture.Request.Subject with { SubjectId = "foreign-owner" } }, fixture.Request with { Mode = KnowledgeMode.Business }, fixture.Request with { Purpose = KnowledgePurpose.Graph }, fixture.Request with { RepositoryScope = [] }, fixture.Request with { Publication = fixture.Request.Publication with { PublicationId = Guid.NewGuid() } } })
            Assert.NotEqual(KnowledgeDependencyStatus.Succeeded, await fixture.Authority.ReauthorizeAsync(foreign, [handle]));
        var lostKeys = new OctoberKnowledgeDependencyAuthority(fixture.Policy, new OctoberKnowledgeDisclosureDataProtectionProtector(new EphemeralDataProtectionProvider()));
        Assert.Equal(KnowledgeDependencyStatus.InvalidProof, await lostKeys.ReauthorizeAsync(fixture.Request, [handle]));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReauthorizeAsync_ReopenedSelectionAfterCursorExpiry_UsesFreshStampAndRetainedLineageAsync()
    {
        var fixture = new DisclosureFixture();
        var selection = await fixture.OpenAsync();
        var page = Successful(await fixture.Disclosed.SearchAsync(new(selection, ""), fixture.Budget()));
        fixture.Data.Clock.Advance(TimeSpan.FromMinutes(31));
        fixture.Policy.CurrentStamp = "fresh-host-authentication-stamp";
        var refreshed = fixture.Request with { Subject = fixture.Request.Subject with { AuthenticationBinding = fixture.Policy.CurrentStamp } };
        Assert.Equal(KnowledgeReadStatus.Success, (await fixture.Reader.OpenReadSelectionAsync(refreshed)).Status);
        fixture.Data.Source.Failure = new InvalidOperationException("Retained capsule must not fetch a replacement page.");
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, await fixture.Authority.ReauthorizeAsync(refreshed, [page.Dependencies]));
        Assert.Equal(KnowledgeDependencyStatus.Denied, await fixture.Authority.ReauthorizeAsync(fixture.Request, [page.Dependencies]));
        fixture.Policy.Status = DisclosureStatus.AuthorityUnavailable;
        Assert.Equal(KnowledgeDependencyStatus.AuthorityUnavailable, await fixture.Authority.ReauthorizeAsync(refreshed, [page.Dependencies]));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BuildConsumedContentAsync_MissingIntervalsAndSemanticReplay_PreservesDistinctStreamAndSourceCoverageAsync()
    {
        var fixture = new DisclosureFixture();
        fixture.SetText(string.Concat(Enumerable.Repeat("中文😀", 10000)));
        var covered = fixture.Data.Source.Value.Coverage[0];
        fixture.Data.Source.Value = fixture.Data.Source.Value with { Coverage = fixture.Data.Source.Value.Coverage.Add(covered with { Coverage = covered.Coverage with { AdmissionId = Guid.NewGuid(), Disposition = CoverageDisposition.Unprocessed, Reason = "Permitted source analysis gap" } }) };
        var selection = await fixture.OpenAsync();
        var pages = await fixture.ReadAllAsync(selection, 18000);
        Assert.True(pages.Count >= 3);
        var all = pages.Select(page => page.Dependencies).ToImmutableArray();
        var complete = await fixture.Authority.BuildConsumedContentAsync(fixture.Request, fixture.Handle, all);
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, complete.Status);
        Assert.False(complete.Receipt!.IsPartial);
        Assert.True(complete.Receipt.OwnerCoverage!.IsPartial);
        Assert.Equal(pages.Count, complete.Receipt.SuccessfulChunks);
        Assert.Equal(JsonSerializer.Serialize(pages[0].Page.Coverage), JsonSerializer.Serialize(complete.Receipt.OwnerCoverage));
        foreach (var partial in new[] { all.RemoveAt(0), all.RemoveAt(1), all.RemoveAt(all.Length - 1) })
        {
            var receipt = await fixture.Authority.BuildConsumedContentAsync(fixture.Request, fixture.Handle, partial);
            Assert.Equal(KnowledgeDependencyStatus.Succeeded, receipt.Status);
            Assert.True(receipt.Receipt!.IsPartial);
        }
        var replay = Successful(await fixture.Disclosed.ReadExactAsync(new(selection, fixture.Handle), fixture.Budget(18000)));
        Assert.NotEqual(pages[0].Page.Next!.Value, replay.Page.Next!.Value);
        var duplicate = await fixture.Authority.BuildConsumedContentAsync(fixture.Request, fixture.Handle, all.Add(replay.Dependencies));
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, duplicate.Status);
        Assert.Equal(complete.Receipt.SuccessfulChunks, duplicate.Receipt!.SuccessfulChunks);
        Assert.Equal(complete.Receipt.ContentBytes, duplicate.Receipt.ContentBytes);
        Assert.Equal(complete.Receipt.Proof.BindingDigest, duplicate.Receipt.Proof.BindingDigest);
        var section = await fixture.Authority.BuildConsumedContentAsync(fixture.Request, fixture.Handle with { SectionId = "overview" }, all);
        Assert.Equal(KnowledgeDependencyStatus.InvalidProof, section.Status);
        Assert.Null(section.Receipt);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task VerifyConsumedContentAsync_RequiresContainedOwnerOriginsAndRejectsConflictingReplayAsync()
    {
        var fixture = new DisclosureFixture();
        var selection = await fixture.OpenAsync();
        var content = Successful(await fixture.Disclosed.ReadExactAsync(new(selection, fixture.Handle), fixture.Budget()));
        var proof = await fixture.Authority.BuildConsumedContentAsync(fixture.Request, fixture.Handle, [content.Dependencies]);
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, proof.Status);
        var root = await fixture.Authority.CreateNoKnowledgeRootAsync(fixture.Request, DisclosureFixture.Hash("question"));
        var missing = await fixture.Authority.VerifyConsumedContentAsync(fixture.Request, proof.Receipt!.Proof, root.Dependencies!);
        Assert.Equal(KnowledgeDependencyStatus.InvalidProof, missing.Status);
        Assert.Null(missing.Receipt);
        var full = await fixture.Authority.MergeAsync(fixture.Request, [root.Dependencies!, content.Dependencies]);
        var verified = await fixture.Authority.VerifyConsumedContentAsync(fixture.Request, proof.Receipt.Proof, full.Dependencies!);
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, verified.Status);
        Assert.Equal(fixture.Handle, verified.Receipt!.Document);
        var search = Successful(await fixture.Disclosed.SearchAsync(new(selection, ""), fixture.Budget()));
        Assert.NotEqual(KnowledgeDependencyStatus.Succeeded, (await fixture.Authority.BuildConsumedContentAsync(fixture.Request, fixture.Handle, [search.Dependencies])).Status);
        fixture.SetText("Changed payload at the same exact version is a controlled corrupt-source counterexample.");
        var changed = Successful(await fixture.Disclosed.ReadExactAsync(new(selection, fixture.Handle), fixture.Budget()));
        var conflict = await fixture.Authority.BuildConsumedContentAsync(fixture.Request, fixture.Handle, [content.Dependencies, changed.Dependencies]);
        Assert.Equal(KnowledgeDependencyStatus.InvalidProof, conflict.Status);
        Assert.Null(conflict.Receipt);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task MergeAsync_OriginLimit_RejectsExtensionWithoutEvictingEarlierInputsAsync()
    {
        var fixture = new DisclosureFixture();
        var roots = ImmutableArray.CreateBuilder<KnowledgeDependencyHandle>();
        for (int index = 0; index <= OctoberKnowledgeDisclosureLimits.MaximumOriginGroups; index++)
        {
            var root = await fixture.Authority.CreateNoKnowledgeRootAsync(fixture.Request, DisclosureFixture.Hash($"question-{index}"));
            Assert.Equal(KnowledgeDependencyStatus.Succeeded, root.Status);
            roots.Add(root.Dependencies!);
        }
        var allowed = await fixture.Authority.MergeAsync(fixture.Request, roots.Take(OctoberKnowledgeDisclosureLimits.MaximumOriginGroups).ToImmutableArray());
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, allowed.Status);
        var overflow = await fixture.Authority.MergeAsync(fixture.Request, roots.ToImmutable());
        Assert.Equal(KnowledgeDependencyStatus.BudgetExceeded, overflow.Status);
        Assert.Null(overflow.Dependencies);
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, await fixture.Authority.ReauthorizeAsync(fixture.Request, [allowed.Dependencies!]));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task MergeAsync_CallerCancelledAfterFirstDecrypt_StopsBeforeOpeningNextCapsuleAsync()
    {
        var fixture = new DisclosureFixture();
        var first = await fixture.Authority.CreateNoKnowledgeRootAsync(fixture.Request, DisclosureFixture.Hash("first input"));
        var second = await fixture.Authority.CreateNoKnowledgeRootAsync(fixture.Request, DisclosureFixture.Hash("second input"));
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, first.Status);
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, second.Status);
        using var cancellation = new CancellationTokenSource();
        var protector = new CancelAfterDecryptProtector(fixture.Protector, cancellation);
        var authority = new OctoberKnowledgeDependencyAuthority(fixture.Policy, protector);
        var result = await authority.MergeAsync(fixture.Request, [first.Dependencies!, second.Dependencies!], cancellation.Token);
        Assert.Equal(KnowledgeDependencyStatus.Cancelled, result.Status);
        Assert.Null(result.Dependencies);
        Assert.Equal(1, protector.DependencyDecryptions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadExactAsync_EncoderChangesProfileDuringEncoding_RejectsInconsistentOutputBindingAsync()
    {
        var fixture = new DisclosureFixture();
        var selection = await fixture.OpenAsync();
        var encoder = new ChangingProfileEncoder(fixture.Encoder);
        var result = await fixture.Disclosed.ReadExactAsync(new(selection, fixture.Handle), new(65536, 262144, encoder));
        Assert.True(encoder.Encoded);
        Assert.Equal(KnowledgeReadStatus.UnavailableService, result.Status);
        Assert.Null(result.Disclosure);
    }
    private static KnowledgeDisclosedPage<T> Successful<T>(KnowledgeDisclosureReadResult<T> result) where T : class
    {
        Assert.Equal(KnowledgeReadStatus.Success, result.Status);
        return Assert.IsType<KnowledgeDisclosedPage<T>>(result.Disclosure);
    }
}

/// <summary>Real production reader/issuer/crypto over an explicitly controlled published source and fresh authority port.</summary>
internal sealed class DisclosureFixture
{
    public B0bReadFixture Data { get; } = new();
    public DisclosureTestPolicy Policy { get; } = new();
    public ActualChatDisclosureEncoder Encoder { get; } = new();
    public IOctoberKnowledgeDisclosureProtector Protector { get; }
    public OctoberKnowledgeDependencyAuthority Authority { get; }
    public OctoberKnowledgeReader Reader { get; }
    public IOctoberKnowledgeDisclosedReader Disclosed => Reader;
    public ReadSelectionRequest Request { get; }
    public DocumentHandle Handle => Data.Handle(Data.Cl);
    public Contributor Uncited { get; } = new(ContributorKind.GlobalAdvisory, null, null, null, Guid.Parse("00000000-0000-0000-0000-000000009001"), new string('d', 64));
    public Contributor RelationOnly { get; } = new(ContributorKind.GlobalAdvisory, null, null, null, Guid.Parse("00000000-0000-0000-0000-000000009002"), new string('e', 64));
    public DisclosureFixture(KnowledgePurpose purpose = KnowledgePurpose.Chat)
    {
        var keys = new EphemeralDataProtectionProvider();
        Protector = new OctoberKnowledgeDisclosureDataProtectionProtector(keys);
        Authority = new(Policy, Protector);
        Request = Data.Request() with { Purpose = purpose };
        var documents = Data.Source.Value.Documents;
        Data.Source.Value = Data.Source.Value with
        {
            Documents = documents.SetItem(0, documents[0] with { Contributors = documents[0].Contributors.Add(Uncited) }),
            Relationships = Data.Source.Value.Relationships.Select(item => item with { Contributors = item.Contributors.Add(RelationOnly) }).ToImmutableArray()
        };
        Reader = new(Data.Source, Data.Publication, Policy,
            new OctoberKnowledgeCursorProtector(new OctoberKnowledgeDataProtectionTokenProtector(keys), Data.Clock), Data.Log, Authority);
    }
    public KnowledgeReadOutputBudget Budget(int perResult = 65536, long aggregate = 262144) => new(perResult, aggregate, Encoder);
    public async Task<ReadSelection> OpenAsync()
    {
        var opened = await Reader.OpenReadSelectionAsync(Request);
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        return Assert.IsType<ReadSelection>(opened.Selection);
    }
    public void SetText(string text)
    {
        var documents = Data.Source.Value.Documents;
        var original = documents[0];
        var section = original.Sections[0] with { Claims = [original.Sections[0].Claims[0] with { Text = text }] };
        ImmutableArray<DocumentSection> sections = [section];
        var content = OctoberKnowledgeAcceptanceFixture.Unit("controlled", null, original.Title, [], sections).ContentJson;
        Data.Source.Value = Data.Source.Value with { Documents = documents.SetItem(0, original with { Sections = sections, ContentJson = content }) };
    }
    public async Task<List<KnowledgeDisclosedPage<ContentPage>>> ReadAllAsync(ReadSelection selection, int limit)
    {
        var pages = new List<KnowledgeDisclosedPage<ContentPage>>();
        ContinuationToken? next = null;
        do
        {
            var result = await Disclosed.ReadExactAsync(new(selection, Handle, next), Budget(limit, 1048576));
            Assert.Equal(KnowledgeReadStatus.Success, result.Status);
            Assert.NotNull(result.Disclosure);
            pages.Add(result.Disclosure);
            next = result.Disclosure.Page.Next;
            Assert.InRange(pages.Count, 1, 100);
        } while (next is not null);
        return pages;
    }
    public static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    public static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
}

/// <summary>Mechanical test host adapter delegates to Chat's actual internal serializer; it does not reproduce its options.</summary>
internal sealed class ActualChatDisclosureEncoder : IOctoberKnowledgeOutputEncoder
{
    public string ProfileId => "october-chat-tool-result-real-serializer-test-v1";
    public ImmutableArray<byte> EncodeSearch(SearchPage page) => Bytes(new(OctoberChatToolStatus.Succeeded, Search: page, References: [])).ToImmutableArray();
    public ImmutableArray<byte> EncodeContent(ContentPage page) => Bytes(new(OctoberChatToolStatus.Succeeded, Content: page, References: [])).ToImmutableArray();
    public ImmutableArray<byte> EncodeRelated(RelatedPage page) => Bytes(new(OctoberChatToolStatus.Succeeded, Related: page, References: [])).ToImmutableArray();
    public static byte[] Bytes(OctoberChatToolResult result) => Encoding.UTF8.GetBytes(OctoberWikiTools.SerializeResult(result).GetRawText());
}

/// <summary>Controlled current host authority, including independent denial of uncited contributor identities.</summary>
internal sealed class DisclosureTestPolicy : IOctoberKnowledgeDisclosurePolicy
{
    public DisclosureStatus Status { get; set; } = DisclosureStatus.Allowed;
    public string CurrentStamp { get; set; } = "controlled-stamp";
    public Contributor? DeniedContributor { get; set; }
    public List<DisclosureRequest> Requests { get; } = [];
    public Task<DisclosureStatus> CheckAsync(DisclosureRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        if (Status != DisclosureStatus.Allowed) return Task.FromResult(Status);
        bool allowed = request.Selection.Subject.SubjectId == "ordinary-user" && request.Selection.Subject.AuthenticationBinding == CurrentStamp &&
            (request.Selection.Mode != KnowledgeMode.Business || request.Kind != DocumentKind.CodeLogic) &&
            request.AllContributors.All(item => item != DeniedContributor && (item.RepositoryId is null || request.Selection.RepositoryScope.Contains(item.RepositoryId.Value)));
        return Task.FromResult(allowed ? DisclosureStatus.Allowed : DisclosureStatus.Denied);
    }
}

/// <summary>Cancellation observation around real authenticated decryption; does not issue or replace proof data.</summary>
internal sealed class CancelAfterDecryptProtector(IOctoberKnowledgeDisclosureProtector inner, CancellationTokenSource cancellation) : IOctoberKnowledgeDisclosureProtector
{
    public int DependencyDecryptions { get; private set; }
    public string ProtectDependency(string plaintext) => inner.ProtectDependency(plaintext);
    public string UnprotectDependency(string protectedPayload)
    {
        string plaintext = inner.UnprotectDependency(protectedPayload);
        DependencyDecryptions++;
        cancellation.Cancel();
        return plaintext;
    }
    public string ProtectConsumedContent(string plaintext) => inner.ProtectConsumedContent(plaintext);
    public string UnprotectConsumedContent(string protectedPayload) => inner.UnprotectConsumedContent(protectedPayload);
}

/// <summary>Controlled faulty host encoder; the result bytes still come from the actual Chat serializer.</summary>
internal sealed class ChangingProfileEncoder(ActualChatDisclosureEncoder inner) : IOctoberKnowledgeOutputEncoder
{
    public bool Encoded { get; private set; }
    public string ProfileId => Encoded ? "changed-profile" : inner.ProfileId;
    public ImmutableArray<byte> EncodeSearch(SearchPage page) { var bytes = inner.EncodeSearch(page); Encoded = true; return bytes; }
    public ImmutableArray<byte> EncodeContent(ContentPage page) { var bytes = inner.EncodeContent(page); Encoded = true; return bytes; }
    public ImmutableArray<byte> EncodeRelated(RelatedPage page) { var bytes = inner.EncodeRelated(page); Encoded = true; return bytes; }
}
