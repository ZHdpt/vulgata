using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.Core.Entities.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberBusinessSynthesisTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_MissingHostHandoffLifetime_FailsClosedBeforeProviderOrAdmissionAsync()
    {
        var f = new SynthesisCase();
        var scope = new SynthesisMissingLifetimeScope();
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(f.Response()));
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), new SynthesisClock());
        Assert.Equal(ProviderAdmissionStatus.AuthorityUnavailable, result.AdmissionFailure);
        Assert.Null(result.Content); Assert.Null(result.Proposal); Assert.Null(result.Dependencies);
        Assert.Empty(result.Gaps); Assert.Empty(result.Coverage); Assert.Empty(result.PlanDispositions); Assert.Empty(result.Attempts);
        Assert.Equal(0, scope.Acquisitions); Assert.Equal(0, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false, ProviderAdmissionStatus.HardStopped)]
    [InlineData(true, ProviderAdmissionStatus.DeadlineExceeded)]
    public async Task ExecuteAsync_ActualHostLifetimeEndsDuringPostSettlementAuthority_SuppressesLateAllowedContentAsync(
        bool runDeadline, ProviderAdmissionStatus expectedFailure)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock, runDeadline ? 180 : 1800);
        // The host run predates this synthesis call: 60 seconds remain from its original 180-second allowance.
        if (runDeadline) clock.Advance(TimeSpan.FromSeconds(120));
        var authority = new SynthesisHandoffDisclosure(f.Input.Contributors);
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(f.Response()));
        var active = f.ExecuteAsync(client, scope, authority, clock);
        try
        {
            await authority.HandoffEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, client.Calls);
            Assert.False(active.IsCompleted);
            // A second actual host lease can acquire the sole slot only after the physical attempt is settled.
            clock.Advance(TimeSpan.FromSeconds(1));
            var capacity = await scope.AcquireWorkerAttemptAsync(f.Worker(1)).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(ProviderAdmissionStatus.Granted, capacity.Status);
            Assert.NotNull(capacity.Lease);
            await capacity.Lease.DisposeAsync(); // No provider dispatch; returns the probe reservation and slot.
            if (runDeadline) clock.Advance(TimeSpan.FromSeconds(60));
            else scope.HardStop();
            // Verify actual host state, independently of the now-released synthesis attempt token.
            Assert.Equal(expectedFailure, (await scope.AcquireWorkerAttemptAsync(f.Worker(1))).Status);
        }
        finally
        {
            authority.Release.TrySetResult();
            await active.WaitAsync(TimeSpan.FromSeconds(5));
        }
        var result = await active;
        Assert.Null(result.Content);
        Assert.Null(result.Proposal);
        Assert.Null(result.Dependencies);
        Assert.Empty(result.Gaps);
        Assert.Empty(result.Coverage);
        Assert.Empty(result.PlanDispositions);
        Assert.Equal(expectedFailure, result.AdmissionFailure);
        Assert.Equal(1, client.Calls);
        Assert.Single(result.Attempts);
        Assert.Equal(3, authority.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DisclosureStatus.Denied, ProviderAdmissionStatus.Denied)]
    [InlineData(DisclosureStatus.AuthorityUnavailable, ProviderAdmissionStatus.AuthorityUnavailable)]
    public async Task ExecuteAsync_AuthorityChangesDuringAwaitedSettlement_RechecksCompleteLedgerBeforeReturningContentAsync(
        DisclosureStatus changedStatus, ProviderAdmissionStatus expectedFailure)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var actualScope = f.Scope(clock);
        bool settled = false;
        var scope = new SynthesisSettlementScope(actualScope, () => settled = true);
        var authority = new SynthesisDisclosure
        {
            Decide = contributors =>
            {
                Assert.True(f.Input.Contributors.ToHashSet().SetEquals(contributors));
                Assert.Contains(f.UncitedContributor, contributors);
                Assert.Contains(f.CoverageContributor, contributors);
                return settled ? changedStatus : DisclosureStatus.Allowed;
            }
        };
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(f.Response()));

        var result = await f.ExecuteAsync(client, scope, authority, clock);

        Assert.True(settled);
        Assert.False(scope.LeaseCancellationObserved);
        Assert.Null(result.Content);
        Assert.Null(result.Proposal);
        Assert.Null(result.Dependencies);
        Assert.Empty(result.Gaps);
        Assert.Empty(result.Coverage);
        Assert.Empty(result.PlanDispositions);
        Assert.Equal(expectedFailure, result.AdmissionFailure);
        Assert.Equal(1, client.Calls);
        Assert.Equal(1, scope.SettleCalls);
        Assert.Equal(1, scope.DisposeCalls);
        Assert.True(authority.Seen.Count >= 3);
        Assert.True(f.Input.Contributors.ToHashSet().SetEquals(authority.Seen.Last()));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_SuppliedClaimOutsideFlowMembers_IsVisiblySupplementalWithoutGraphExpansion()
    {
        var f = new SynthesisCase();
        var outside = f.Input.Corpus.Documents[3].Version;
        Assert.DoesNotContain(outside, f.Group.Members);
        string original = OctoberKnowledgePolicy.SerializeCanonical(f.Input);
        var proposal = f.Proposal();
        var section = proposal.Sections[0];
        var claim = section.Claims[0] with { Inputs = [new(outside, "operation", "provider")] };
        var result = f.Validate(proposal with { Sections = proposal.Sections.SetItem(0, section with { Claims = section.Claims.SetItem(0, claim) }) });
        Assert.Equal(BusinessSynthesisValidationStatus.Succeeded, result.Status);
        var supplemental = result.Content!.Sections.SelectMany(x => x.Claims).Single(x => x.ClaimGroupId == "business-action");
        Assert.StartsWith("补充背景（未确认为本流程成员）：", supplemental.Text, StringComparison.Ordinal);
        Assert.Equal(ClaimQualification.SourceSupported, supplemental.Qualification);
        Assert.DoesNotContain(outside, f.Group.Members);
        Assert.Equal(original, OctoberKnowledgePolicy.SerializeCanonical(f.Input));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Group.Dependencies), OctoberKnowledgePolicy.SerializeCanonical(result.Dependencies));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Input.Coverage), OctoberKnowledgePolicy.SerializeCanonical(result.Coverage));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Input.PlanDispositions), OctoberKnowledgePolicy.SerializeCanonical(result.PlanDispositions));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_QualifiedProse_DerivesExactEvidencePreservesLimitationsAndEntireSuppliedLedger()
    {
        var f = new SynthesisCase();
        string original = OctoberKnowledgePolicy.SerializeCanonical(f.Input);
        var result = f.Validate(f.Proposal());
        Assert.Equal(BusinessSynthesisValidationStatus.Succeeded, result.Status);
        var content = Assert.IsType<BusinessLogicContent>(result.Content);
        var claims = content.Sections.SelectMany(x => x.Claims).ToArray();
        var source = Assert.Single(claims.Where(x => x.ClaimGroupId == "business-action"));
        var accepted = f.Claim("call-1-binding");
        Assert.Equal(ClaimQualification.SourceSupported, source.Qualification);
        Assert.All(accepted.Limitations, limit => Assert.Contains(limit, source.Limitations));
        Assert.Contains(source.Evidence, evidence => evidence.Document == f.Root && evidence.ClaimGroupId == "call-1-binding" &&
            evidence.Source == accepted.Evidence[0].Source && evidence.ContentDigest == accepted.Evidence[0].ContentDigest);
        Assert.Equal(ClaimQualification.Advisory, claims.Single(x => x.ClaimGroupId == "business-advice").Qualification);
        Assert.Equal(ClaimQualification.Hypothesis, claims.Single(x => x.ClaimGroupId == "business-unknown").Qualification);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Group.Dependencies), OctoberKnowledgePolicy.SerializeCanonical(result.Dependencies));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Input.Coverage), OctoberKnowledgePolicy.SerializeCanonical(result.Coverage));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Input.PlanDispositions), OctoberKnowledgePolicy.SerializeCanonical(result.PlanDispositions));
        Assert.Contains(f.UncitedContributor, result.Dependencies!.Contributors);
        Assert.Contains(f.CoverageContributor, result.Dependencies.Contributors);
        Assert.Contains(f.Input.Corpus.Documents[3].Version, result.Dependencies.Documents);
        Assert.Equal(2, result.Dependencies.FileUncertainties.Length);
        Assert.Equal(original, OctoberKnowledgePolicy.SerializeCanonical(f.Input));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_ModelOmitsKnownGaps_AppRetainsEveryGapAndOneUncertainGenericSummary()
    {
        var f = new SynthesisCase();
        var result = f.Validate(f.Proposal());
        Assert.Equal(BusinessSynthesisValidationStatus.Succeeded, result.Status);
        Assert.All(Enum.GetValues<SynthesisGapKind>(), kind => Assert.Contains(result.Gaps, gap => gap.Locator.Kind == kind));
        var fileGaps = result.Gaps.Where(x => x.Locator.Kind == SynthesisGapKind.FileUncertainty).ToArray();
        Assert.Equal(2, fileGaps.Length);
        Assert.Equal(2, fileGaps.Select(x => x.Locator.Key).Distinct().Count());
        Assert.All(fileGaps, gap => Assert.Contains(SynthesisCase.PrivatePath, gap.Explanation + string.Join(" ", gap.Limitations), StringComparison.Ordinal));
        var section = Assert.Single(result.Content!.Sections.Where(x => x.SectionId == OctoberBusinessSynthesisPolicy.GapSectionId));
        var summary = Assert.Single(section.Claims);
        Assert.Equal(OctoberBusinessSynthesisPolicy.GapClaimId, summary.ClaimGroupId);
        Assert.Equal(ClaimQualification.Uncertain, summary.Qualification);
        Assert.DoesNotContain(SynthesisCase.PrivatePath, summary.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("same-file-key", summary.Text, StringComparison.Ordinal);
        Assert.Contains(SynthesisCase.PrivatePath, string.Join(" ", summary.Limitations), StringComparison.Ordinal);
        var explicitGaps = OctoberBusinessSynthesisPolicy.DescribeGaps(f.Input, f.Group.GroupVersionId, f.Limits);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(result.Gaps), OctoberKnowledgePolicy.SerializeCanonical(explicitGaps));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ValidatedBl_ActualBusinessReader_HidesInternalTechnicalGapsWhileKeepingGenericUncertaintyAsync()
    {
        var f = new SynthesisCase();
        var result = f.Validate(f.Proposal());
        Assert.Equal(BusinessSynthesisValidationStatus.Succeeded, result.Status);
        var content = result.Content!;
        var publication = new PublicationBinding(f.Input.Corpus.Handle.Binding, Guid.NewGuid());
        var version = new DocumentVersionReference(publication.Knowledge.BatchId, Guid.NewGuid(), Guid.NewGuid());
        var document = new OctoberPublishedDocument(version, DocumentKind.BusinessLogic, content.Title, content.ContentJson, content.Sections, result.Dependencies!.Contributors);
        var source = new B0bPublishedSource(new(publication, [document], [], [], []));
        var reader = new OctoberKnowledgeReader(source, publication, new B0bReaderDisclosure(),
            new OctoberKnowledgeCursorProtector(new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider()), new B0bReadClock()), new B0bReadLog());
        var scope = f.Input.Contributors.Where(x => x.RepositoryId is not null).Select(x => x.RepositoryId!.Value).Distinct().ToImmutableArray();
        var selection = await reader.OpenReadSelectionAsync(new(publication, new("ordinary-user", "controlled-stamp"), KnowledgeMode.Business, KnowledgePurpose.Reader, scope));
        Assert.Equal(KnowledgeReadStatus.Success, selection.Status);
        var read = await reader.ReadExactAsync(new(selection.Selection!, new(version.BatchId, publication.PublicationId, version.LogicalDocumentId, version.DocumentVersionId)));
        Assert.Equal(KnowledgeReadStatus.Success, read.Status);
        Assert.DoesNotContain(SynthesisCase.PrivatePath, read.Content!, StringComparison.Ordinal);
        Assert.DoesNotContain("same-file-key", read.Content!, StringComparison.Ordinal);
        Assert.All(read.Claims, claim => Assert.Empty(claim.Evidence));
        Assert.Contains(read.Claims, claim => claim.Text == content.Sections.Single(x => x.SectionId == OctoberBusinessSynthesisPolicy.GapSectionId).Claims.Single().Text && claim.Qualification == ClaimQualification.Uncertain);
        // Internal provenance remains exact; Business projection must not erase it from canonical storage.
        Assert.Contains(SynthesisCase.PrivatePath, content.ContentJson, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("document")]
    [InlineData("section")]
    [InlineData("claim")]
    [InlineData("upgrade")]
    [InlineData("mixed")]
    [InlineData("reserved-gap")]
    [InlineData("invented-input")]
    public void Validate_InventedEvidenceQualificationOrReservedGap_ReturnsNoContentOrLedger(string mutation)
    {
        var f = new SynthesisCase();
        var proposal = f.Proposal();
        var section = proposal.Sections[0];
        var claim = section.Claims[0];
        claim = mutation switch
        {
            "document" => claim with { Inputs = [claim.Inputs[0] with { Document = f.Root with { DocumentVersionId = Guid.NewGuid() } }] },
            "section" => claim with { Inputs = [claim.Inputs[0] with { SectionId = "invented" }] },
            "claim" => claim with { Inputs = [claim.Inputs[0] with { ClaimGroupId = "invented" }] },
            "upgrade" => claim with { Inputs = [new(f.Root, "operation", "advice")] },
            "mixed" => claim with { Qualification = ClaimQualification.Mixed, Inputs = claim.Inputs.Add(new(f.Root, "operation", "advice")) },
            "invented-input" => claim with { Inputs = [] },
            _ => claim
        };
        section = section with { Claims = section.Claims.SetItem(0, claim) };
        if (mutation == "reserved-gap") section = section with { SectionId = OctoberBusinessSynthesisPolicy.GapSectionId };
        var result = f.Validate(proposal with { Sections = proposal.Sections.SetItem(0, section) });
        Assert.Equal(BusinessSynthesisValidationStatus.InvalidProposal, result.Status);
        Assert.Null(result.Content); Assert.Null(result.Dependencies);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ActualScope_UsesSynthesisLeaseCompleteAcceptedInputAndQualifiedOutputWithoutToolsAsync()
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        var spy = new SynthesisScopeSpy(scope);
        var authority = new SynthesisDisclosure();
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(f.Response()));
        var result = await f.ExecuteAsync(client, spy, authority, clock);
        Assert.NotNull(result.Content); Assert.NotNull(result.Dependencies); Assert.NotNull(result.Proposal);
        Assert.Null(result.AdmissionFailure);
        Assert.Equal(1, client.Calls);
        Assert.Equal(BusinessWorkKind.Synthesis, Assert.Single(spy.Requests).WorkKind);
        Assert.InRange(spy.Requests[0].MaximumDuration, TimeSpan.FromTicks(1), TimeSpan.FromSeconds(180));
        var sent = Assert.Single(client.Requests);
        Assert.Empty(sent.Tools);
        string input = string.Join("\n", sent.Messages.Where(x => x.Role == ChatRole.User).Select(x => x.Text));
        Assert.Contains(f.Input.Corpus.Documents[3].Version.DocumentVersionId.ToString(), input, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(f.CoverageContributor.RepositoryId!.Value.ToString(), input, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("same-file-key", input, StringComparison.Ordinal);
        Assert.All(authority.Seen, contributors => Assert.True(f.Input.Contributors.ToHashSet().SetEquals(contributors)));
        Assert.True(authority.Seen.Count >= 2);
        Assert.Equal(10, Assert.Single(result.Attempts).InputTokens);
        Assert.Equal(5, result.Attempts[0].OutputTokens);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Group.Dependencies), OctoberKnowledgePolicy.SerializeCanonical(result.Dependencies));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Input.Coverage), OctoberKnowledgePolicy.SerializeCanonical(result.Coverage));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Input.PlanDispositions), OctoberKnowledgePolicy.SerializeCanonical(result.PlanDispositions));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("outage-after")]
    public async Task ExecuteAsync_UncitedOrCoverageContributorDeniedOrAuthorityOutage_SuppressesOutputWithoutRetryAsync(string boundary)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        bool providerReturned = false;
        var authority = new SynthesisDisclosure
        {
            Decide = contributors =>
            {
                Assert.Contains(f.UncitedContributor, contributors);
                Assert.Contains(f.CoverageContributor, contributors);
                if (boundary == "outage-after" && providerReturned) throw new IOException("Controlled authority outage after response.");
                return boundary == "before" || providerReturned ? DisclosureStatus.Denied : DisclosureStatus.Allowed;
            }
        };
        using var client = new ResolutionClient("selected-profile", (_, _) => { providerReturned = true; return Task.FromResult(f.Response()); });
        var result = await f.ExecuteAsync(client, scope, authority, clock);
        Assert.Null(result.Content); Assert.Null(result.Dependencies); Assert.Null(result.Proposal);
        Assert.Equal(boundary == "outage-after" ? ProviderAdmissionStatus.AuthorityUnavailable : ProviderAdmissionStatus.Denied, result.AdmissionFailure);
        Assert.Equal(boundary == "before" ? 0 : 1, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("relationships")]
    [InlineData("dependencies")]
    [InlineData("logicalDocumentId")]
    public async Task ExecuteAsync_ModelAddsAuthoritativeFields_RejectsProposalWithoutPublishingOrRetryAsync(string field)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        string before = OctoberKnowledgePolicy.SerializeCanonical(f.Input);
        var json = JsonSerializer.SerializeToNode(f.Proposal())!.AsObject();
        json[field] = field == "logicalDocumentId" ? JsonValue.Create(Guid.NewGuid().ToString()) : new JsonArray();
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(SynthesisCase.JsonResponse(json.ToJsonString())));
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.Null(result.Content); Assert.Null(result.Dependencies); Assert.Null(result.Proposal);
        Assert.Equal(1, client.Calls);
        Assert.Equal(before, OctoberKnowledgePolicy.SerializeCanonical(f.Input));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_TransportRetry_UsesTwoFreshAttemptsWithinOriginal180SecondsAndSharedReservationAsync()
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        var scope = new ResolutionScopeProbe(); // Controlled host retains a conservative unknown reservation and explicitly permits one retry.
        using var client = new ResolutionClient("selected-profile", (attempt, _) =>
        {
            if (attempt == 1) { clock.Advance(TimeSpan.FromSeconds(130)); throw new IOException("Controlled transient transport failure."); }
            return Task.FromResult(f.Response());
        });
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.NotNull(result.Content);
        Assert.Equal(2, client.Calls);
        Assert.Equal(2, scope.Requests.Count);
        Assert.All(scope.Requests, request => Assert.Equal(BusinessWorkKind.Synthesis, request.WorkKind));
        Assert.NotEqual(scope.Requests[0].AttemptId, scope.Requests[1].AttemptId);
        Assert.InRange(scope.Requests[1].MaximumDuration, TimeSpan.FromTicks(1), TimeSpan.FromSeconds(50));
        Assert.Null(result.Attempts[0].InputTokens); Assert.Null(result.Attempts[0].OutputTokens);
        Assert.Equal(45, scope.Inner.RemainingTokens);
        Assert.Equal(2, scope.SettleCalls); Assert.Equal(2, scope.DisposeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ExpiredOriginalDeadline_DoesNotBeginFreshRetryAllowanceAsync()
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        var scope = new ResolutionScopeProbe();
        using var client = new ResolutionClient("selected-profile", (_, _) => { clock.Advance(TimeSpan.FromSeconds(181)); throw new IOException("Controlled delayed failure."); });
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.Equal(ProviderAdmissionStatus.DeadlineExceeded, result.AdmissionFailure);
        Assert.Null(result.Content);
        Assert.Equal(1, client.Calls); Assert.Single(scope.Requests);
        Assert.Equal(1, scope.SettleCalls); Assert.Equal(1, scope.DisposeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ActualScopeUnknownUsage_BlocksFurtherWorkerAndSynthesisBudgetAsync()
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(f.Response(false)));
        var first = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.NotNull(first.Content);
        Assert.Null(Assert.Single(first.Attempts).InputTokens);
        Assert.Null(first.Attempts[0].OutputTokens); Assert.Null(first.Attempts[0].Cost);
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, (await scope.AcquireWorkerAttemptAsync(f.Worker(1))).Status);
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, (await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock)).AdmissionFailure);
        Assert.Equal(1, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ActualSharedCapAndHardStop_KeepsUnsettledTransportOwnedDeniesQueuedAndRejectsLateContentAsync()
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ResolutionClient("selected-profile", async (_, _) => { await release.Task; return f.Response(); });
        var active = f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Task<BusinessSynthesisResult>? queued = null;
        try
        {
            await client.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            queued = f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
            Assert.False(queued.IsCompleted); Assert.Equal(1, client.Calls);
            scope.HardStop();
            Assert.True(client.LastCancellation.IsCancellationRequested);
            Assert.False(active.IsCompleted);
            Assert.Equal(ProviderAdmissionStatus.HardStopped, (await queued.WaitAsync(TimeSpan.FromSeconds(5))).AdmissionFailure);
            var cleanup = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask());
            Assert.Equal("october_provider_settlement_unconfirmed", cleanup.Message);
        }
        finally { release.TrySetResult(); await active.WaitAsync(TimeSpan.FromSeconds(5)); if (queued is not null) await queued.WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.Null((await active).Content);
        Assert.Equal(ProviderAdmissionStatus.HardStopped, (await active).AdmissionFailure);
        Assert.Equal(1, client.Calls); Assert.Equal(1, client.PeakActive);
    }
}

/// <summary>Controlled accepted-input fixture; real pure planner and real host scope, without source/provider runtime qualification.</summary>
internal sealed class SynthesisCase
{
    public const string PrivatePath = "private/sentinel-payments-secret.cs";
    public FlowPlanningInput Input { get; }
    public FlowGroupVersion Group { get; }
    public FlowPlanningLimits Limits { get; } = new(1, 512, 16);
    public DocumentVersionReference Root => Input.Corpus.Documents[0].Version;
    public Contributor UncitedContributor { get; }
    public Contributor CoverageContributor { get; }
    public Guid Run { get; } = ResolutionCase.Id(97001);
    public OperationKey Operation => new(Input.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, ResolutionCase.Id(97002));

    public SynthesisCase()
    {
        var basis = new FlowPlannerCase(4, [(0, 1), (2, 3)], [0], unsupportedObservation: true);
        UncitedContributor = basis.UncitedAdvisory;
        var origin = new PreparedOrigin(ResolutionCase.Id(96000), "controlled-coverage-root", PrivatePath, new string('d', 64), "controlled-v1");
        CoverageContributor = new(ContributorKind.Source, origin.RepositoryId, null, origin.RootBindingIdentity, null, origin.SourceDigest);
        var evidence = basis.Input.Corpus.Documents[0].Sections[0].Claims[0].Evidence;
        Input = basis.Input with
        {
            Coverage = basis.Input.Coverage.Add(new(ResolutionCase.Id(96001), origin, CoverageDisposition.Unprocessed, "Unprocessed controlled source " + PrivatePath)),
            Contributors = basis.Input.Contributors.Add(CoverageContributor),
            Resolutions = basis.Input.Resolutions.Select(item => item with
            {
                Outcome = item.Outcome with { Contributors = basis.Input.Contributors.Add(CoverageContributor) }
            }).ToImmutableArray(),
            FileUncertainties =
            [
                new(basis.Input.Corpus.Handle.Binding.BatchId, ResolutionCase.Id(96002), "same-file-key", new("same-file-key", "First file unknown: " + PrivatePath, evidence, ["First limitation " + PrivatePath])),
                new(basis.Input.Corpus.Handle.Binding.BatchId, ResolutionCase.Id(96003), "same-file-key", new("same-file-key", "Second file unknown: " + PrivatePath, evidence, ["Second limitation " + PrivatePath]))
            ]
        };
        var plan = OctoberFlowPlanner.Plan(Input, Limits);
        Assert.Equal(FlowPlanningStatus.Succeeded, plan.Status);
        Group = Assert.Single(plan.Groups);
    }

    public QualifiedClaimGroup Claim(string id) => Input.Corpus.Documents[0].Sections.SelectMany(x => x.Claims).Single(x => x.ClaimGroupId == id);
    public BusinessSynthesisProposal Proposal() => new("业务处理流程",
    [
        new("business-main", 0, "操作、条件与结果",
        [
            new("business-action", "满足处理条件时，将请求交给后续处理环节。", ClaimQualification.SourceSupported, [new(Root, "operation", "call-1-binding")], []),
            new("business-advice", "相关操作建议仍需业务负责人确认。", ClaimQualification.Advisory, [new(Root, "operation", "advice")], []),
            new("business-unknown", "当前运行环境是否启用此流程尚待确认。", ClaimQualification.Hypothesis, [new(Root, "operation", "hypothesis")], [])
        ])
    ]);
    public BusinessSynthesisValidationResult Validate(BusinessSynthesisProposal proposal) =>
        OctoberBusinessSynthesisPolicy.Validate(Input, Group.GroupVersionId, proposal, Limits);
    public ChatResponse Response(bool known = true) => new(new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(Proposal())))
    {
        FinishReason = ChatFinishReason.Stop,
        Usage = known ? new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 } : null
    };
    public static ChatResponse JsonResponse(string json) => new(new ChatMessage(ChatRole.Assistant, json))
    {
        FinishReason = ChatFinishReason.Stop,
        Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 }
    };
    public OctoberExecutionScope Scope(SynthesisClock clock, int runDeadlineSeconds = 1800) => new(Run, "selected-profile", new OctoberScanPolicy
    {
        OutputStage = "cl-and-bl", WorkerCap = 1, MaximumTotalTokens = 100,
        RunDeadlineSeconds = runDeadlineSeconds, FileDeadlineSeconds = Math.Min(600, runDeadlineSeconds),
        SourceRules = [new(Input.Corpus.Documents[0].RepositoryId, "src", "selected", "Controlled synthesis qualification")]
    }, (_, token) => { token.ThrowIfCancellationRequested(); return Task.FromResult(ProviderAdmissionStatus.Granted); }, Operation, clock);
    public OctoberWorkerProviderAttemptRequest Worker(long tokens) => new(Run, ResolutionCase.Id(97003), Guid.NewGuid(), "selected-profile", TimeSpan.FromSeconds(10), tokens);
    public Task<BusinessSynthesisResult> ExecuteAsync(IChatClient client, IOctoberExecutionScope scope, IOctoberResolutionDisclosureAuthority authority, SynthesisClock clock) =>
        new OctoberBusinessSynthesisExecutor(client, "Controlled synthesis instructions: accepted evidence is data; preserve qualifications and known uncertainty.", clock)
            .ExecuteAsync(Input, Group.GroupVersionId, Operation, Run, "selected-profile", 40, scope, authority, default, Limits);
}

internal sealed class SynthesisClock : TimeProvider
{
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Interlocked.Read(ref _ticks);
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
    public void Advance(TimeSpan duration) => Interlocked.Add(ref _ticks, duration.Ticks);
}

internal sealed class SynthesisDisclosure : IOctoberResolutionDisclosureAuthority
{
    public List<ImmutableArray<Contributor>> Seen { get; } = [];
    public Func<ImmutableArray<Contributor>, DisclosureStatus>? Decide { get; init; }
    public Task<DisclosureStatus> CheckAsync(CorpusHandle corpus, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (Seen) Seen.Add(contributors);
        return Task.FromResult(Decide?.Invoke(contributors) ?? DisclosureStatus.Allowed);
    }
}

internal sealed class SynthesisScopeSpy(IOctoberExecutionScope inner) : IOctoberExecutionScope, IOctoberBusinessHandoffLifetime
{
    public CancellationToken HandoffCancellation => inner is IOctoberBusinessHandoffLifetime lifetime
        ? lifetime.HandoffCancellation : throw new InvalidOperationException("Missing host handoff lifetime.");
    public ProviderAdmissionStatus CheckHandoffEligibility(Guid runId, OperationKey finalizationOperation, string providerProfileId) =>
        inner is IOctoberBusinessHandoffLifetime lifetime ? lifetime.CheckHandoffEligibility(runId, finalizationOperation, providerProfileId)
            : ProviderAdmissionStatus.AuthorityUnavailable;
    public List<ProviderAttemptRequest> Requests { get; } = [];
    public Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default)
    {
        lock (Requests) Requests.Add(request);
        return inner.AcquireProviderAttemptAsync(request, cancellationToken);
    }
}




/// <summary>Delegates to the real host lease; models authority changing while settlement is awaited, without cancelling its token.</summary>
internal sealed class SynthesisSettlementScope(IOctoberExecutionScope inner, Action afterSettlement) : IOctoberExecutionScope, IOctoberBusinessHandoffLifetime
{
    public CancellationToken HandoffCancellation => inner is IOctoberBusinessHandoffLifetime lifetime
        ? lifetime.HandoffCancellation : throw new InvalidOperationException("Missing host handoff lifetime.");
    public ProviderAdmissionStatus CheckHandoffEligibility(Guid runId, OperationKey finalizationOperation, string providerProfileId) =>
        inner is IOctoberBusinessHandoffLifetime lifetime ? lifetime.CheckHandoffEligibility(runId, finalizationOperation, providerProfileId)
            : ProviderAdmissionStatus.AuthorityUnavailable;
    private readonly Action _afterSettlement = afterSettlement;
    public int SettleCalls { get; private set; }
    public int DisposeCalls { get; private set; }
    public bool LeaseCancellationObserved { get; private set; }
    public async Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default)
    {
        var admission = await inner.AcquireProviderAttemptAsync(request, cancellationToken);
        return admission.Lease is { } lease ? admission with { Lease = new SettlementLease(this, lease) } : admission;
    }
    private sealed class SettlementLease(SynthesisSettlementScope owner, IOctoberProviderAttemptLease innerLease) : IOctoberProviderAttemptLease, IOctoberWorkerDispatchObserver
    {
        private IOctoberWorkerDispatchObserver Observer => innerLease as IOctoberWorkerDispatchObserver
            ?? throw new InvalidOperationException("Missing inner lease dispatch observer.");
        public void RecordDispatch() => Observer.RecordDispatch();
        public void CompleteDispatchObservation() => Observer.CompleteDispatchObservation();
        public Guid AttemptId => innerLease.AttemptId;
        public CancellationToken CancellationToken => innerLease.CancellationToken;
        public Task<ProviderAdmissionStatus> CheckBeforeSendAsync(CancellationToken cancellationToken = default) => innerLease.CheckBeforeSendAsync(cancellationToken);
        public async Task SettleAsync(ProviderAttemptUsage usage, CancellationToken cancellationToken = default)
        {
            owner.SettleCalls++;
            await innerLease.SettleAsync(usage, cancellationToken);
            owner.LeaseCancellationObserved |= innerLease.CancellationToken.IsCancellationRequested;
            owner._afterSettlement();
        }
        public async ValueTask DisposeAsync()
        {
            owner.DisposeCalls++;
            owner.LeaseCancellationObserved |= innerLease.CancellationToken.IsCancellationRequested;
            await innerLease.DisposeAsync();
        }
    }
}

/// <summary>Pauses only the post-settlement full-contributor check and then returns Allowed even if the old attempt token changed.</summary>
internal sealed class SynthesisHandoffDisclosure(ImmutableArray<Contributor> expected) : IOctoberResolutionDisclosureAuthority
{
    private int _calls;
    public int Calls => Volatile.Read(ref _calls);
    public TaskCompletionSource HandoffEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<DisclosureStatus> CheckAsync(CorpusHandle corpus, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default)
    {
        Assert.True(expected.ToHashSet().SetEquals(contributors));
        if (Interlocked.Increment(ref _calls) == 3)
        {
            HandoffEntered.TrySetResult();
            await Release.Task;
        }
        return DisclosureStatus.Allowed;
    }
}


internal sealed class SynthesisMissingLifetimeScope : IOctoberExecutionScope
{
    public int Acquisitions { get; private set; }
    public Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default)
    {
        Acquisitions++;
        return Task.FromResult(new ProviderAdmission(ProviderAdmissionStatus.AuthorityUnavailable, null));
    }
}
