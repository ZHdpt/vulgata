using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Controlled original-slot/owner-store integration through real synthesis. Does not qualify Scan slot issuance, storage or live providers.</summary>
public sealed class OctoberBusinessFinalizerTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FinalizeAsync_PublicationWindow_AllowsCompletionAfterElevenSecondsAsync()
    {
        using var f = new FinalizerCase();
        var clock = new Vulgata.Tests.OctoberScan.OctoberScanAcceptanceHarness.ManualClock();
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Store.BeforePublish = async token => { entered.SetResult(token); await release.Task.WaitAsync(token); };
        var pending = f.Finalizer(timeProvider: clock).FinalizeAsync(f.Request);
        try
        {
            var publicationToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromSeconds(11));
            Assert.False(publicationToken.IsCancellationRequested);
            Assert.False(pending.IsCompleted);
            release.SetResult();
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(BusinessFinalizationStatus.Published, result.Status);
            Assert.Equal(f.Publication, result.Publication);
            Assert.Equal(1, f.Store.Publishes); Assert.Equal(1, f.Slot.Invocations); Assert.Equal(4, f.Client.Calls);
        }
        finally { release.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalizeAsync_PublicationWindow_DeadlineOrHostStopCancelsWithoutRetryAsync(bool hardStop)
    {
        using var f = new FinalizerCase();
        var clock = new Vulgata.Tests.OctoberScan.OctoberScanAcceptanceHarness.ManualClock();
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Store.BeforePublish = async token => { entered.SetResult(token); await release.Task.WaitAsync(token); };
        var finalizer = f.Finalizer(timeProvider: clock);
        var pending = finalizer.FinalizeAsync(f.Request);
        try
        {
            var publicationToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromSeconds(hardStop ? 11 : 119));
            Assert.False(publicationToken.IsCancellationRequested);
            Assert.False(pending.IsCompleted);
            if (hardStop) f.Scope.HardStop();
            else clock.Advance(TimeSpan.FromSeconds(1));
            Assert.True(publicationToken.IsCancellationRequested);
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(hardStop ? BusinessFinalizationStatus.HardStopped : BusinessFinalizationStatus.CommitIndeterminate, result.Status);
            Assert.Null(result.Publication); Assert.Null(f.Store.Durable);
            int lookups = f.Store.Lookups;
            var replay = await finalizer.FinalizeAsync(f.Request).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(result.Status, replay.Status); Assert.Null(replay.Publication);
            if (!hardStop) Assert.True(f.Store.Lookups > lookups);
            Assert.Equal(1, f.Store.Publishes); Assert.Equal(1, f.Slot.Invocations); Assert.Equal(4, f.Client.Calls);
        }
        finally { release.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalizeAsync_OriginalFailureDiagnostic_SurvivesAuthorizedLookupOnlyForOriginalInvocationAsync(bool bothRootsFail)
    {
        using var f = new FinalizerCase();
        var expectedStatus = bothRootsFail ? BusinessFinalizationStatus.Failed : BusinessFinalizationStatus.PublishedPartial;
        f.Store.PublishStatus = expectedStatus;
        f.Store.PublishCoverage = new(!bothRootsFail, []);
        f.Store.PublishPersistenceStatus = KnowledgeWriteStatus.Committed;
        f.Provider = (call, _) => Task.FromResult(call == 1
            ? SynthesisCase.JsonResponse(JsonSerializer.Serialize(f.Proposal(0) with { Sections = [f.Proposal(0).Sections[0] with { Ordinal = 1 }] }))
            : bothRootsFail ? SynthesisCase.JsonResponse("null") : f.Response(call - 1));
        var finalizer = f.Finalizer();
        var original = await finalizer.FinalizeAsync(f.Request);
        Assert.Equal(expectedStatus, original.Status);
        Assert.Equal(bothRootsFail ? null : f.Publication, original.Publication);
        Assert.Equal("SectionOrder", SynthesisDiagnosticProbe.Read(original, "OriginalSynthesisFailureReason"));
        Assert.True(f.Store.Lookups >= 1);
        Assert.Null(SynthesisDiagnosticProbe.Read(f.Store.Durable!, "OriginalSynthesisFailureReason"));
        Assert.Equal(bothRootsFail ? 0 : 1, f.Store.Submitted!.Completed.Length);
        Assert.Equal(bothRootsFail ? 2 : 1, f.Store.Submitted.Dispositions.Length);
        var cached = await finalizer.FinalizeAsync(f.Request);
        var importedSlot = await f.Finalizer().FinalizeAsync(f.Request);
        Assert.Equal(original.Publication, cached.Publication);
        Assert.Equal(original.Publication, importedSlot.Publication);
        Assert.Null(SynthesisDiagnosticProbe.Read(cached, "OriginalSynthesisFailureReason"));
        Assert.Null(SynthesisDiagnosticProbe.Read(importedSlot, "OriginalSynthesisFailureReason"));
        Assert.Equal(bothRootsFail ? 2 : 3, f.Client.Calls); Assert.Equal(1, f.Slot.Invocations); Assert.Equal(1, f.Store.Publishes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("injected")]
    [InlineData("revoked")]
    [InlineData("foreign")]
    [InlineData("legacy-null")]
    [InlineData("durable-replay")]
    [InlineData("indeterminate")]
    public async Task FinalizeAsync_LookupDiagnostic_IsNeverAcceptedAsOriginalAttemptAuthorityAsync(string boundary)
    {
        using var f = new FinalizerCase();
        f.Store.PublishStatus = BusinessFinalizationStatus.PublishedPartial;
        f.Store.PublishCoverage = new(true, []);
        f.Store.PublishPersistenceStatus = KnowledgeWriteStatus.Committed;
        f.Provider = (call, _) =>
        {
            var response = f.Response(call - 1);
            if (call == 1 && boundary != "injected") response.FinishReason = ChatFinishReason.Length;
            return Task.FromResult(response);
        };
        var lookup = f.Result(BusinessFinalizationStatus.PublishedPartial) with { Coverage = new(true, []), PersistenceStatus = KnowledgeWriteStatus.Committed };
        lookup = boundary switch
        {
            "revoked" => f.Result(BusinessFinalizationStatus.Denied),
            "indeterminate" => f.Result(BusinessFinalizationStatus.CommitIndeterminate),
            "foreign" => lookup with { Operation = lookup.Operation with { OperationId = Guid.NewGuid() } },
            "legacy-null" => lookup with { PersistenceStatus = null },
            "durable-replay" => lookup with { PersistenceStatus = KnowledgeWriteStatus.AlreadyCommitted, IsReplay = true },
            _ => lookup
        };
        SynthesisDiagnosticProbe.Set(lookup, "ResponseShape", "OriginalSynthesisFailureReason");
        f.Store.LookupOverride = lookup;
        if (boundary == "durable-replay") f.Store.Initial = new(BusinessFinalizationInitialReadKind.NoDispatchResult, lookup);
        var result = await f.Finalizer().FinalizeAsync(f.Request);
        Assert.Null(SynthesisDiagnosticProbe.Read(result, "OriginalSynthesisFailureReason"));
        Assert.Equal(boundary switch
        {
            "revoked" => BusinessFinalizationStatus.Denied,
            "foreign" or "indeterminate" => BusinessFinalizationStatus.CommitIndeterminate,
            _ => BusinessFinalizationStatus.PublishedPartial
        }, result.Status);
        if (boundary is "revoked" or "foreign" or "indeterminate") { Assert.Null(result.Publication); Assert.Empty(result.Coverage.PermittedGaps); }
        if (boundary == "durable-replay") { Assert.Equal(0, f.Client.Calls); Assert.Equal(0, f.Store.Publishes); }
        else Assert.Equal(boundary == "injected" ? 4 : 3, f.Client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void FinalizationDiagnostic_NonnullReason_DoesNotChangeOrdinaryOrCanonicalSerialization()
    {
        using var f = new FinalizerCase();
        var result = f.Result(BusinessFinalizationStatus.Failed);
        string ordinary = JsonSerializer.Serialize(result);
        string canonical = OctoberKnowledgePolicy.SerializeCanonical(result);
        SynthesisDiagnosticProbe.Set(result, "SectionOrder", "OriginalSynthesisFailureReason");
        Assert.Equal("SectionOrder", SynthesisDiagnosticProbe.Read(result, "OriginalSynthesisFailureReason"));
        Assert.Equal(ordinary, JsonSerializer.Serialize(result));
        Assert.Equal(canonical, OctoberKnowledgePolicy.SerializeCanonical(result));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("conflict")]
    [InlineData("invalid-proposal")]
    [InlineData("indeterminate")]
    [InlineData("committed-replay")]
    [InlineData("partial-without-partial-coverage")]
    public async Task FinalizeAsync_IncoherentFreshPublicationResult_ExposesNoArtifactOrCoverageAsync(string contradiction)
    {
        using var f = new FinalizerCase();
        var published = f.Result(BusinessFinalizationStatus.Published) with { Coverage = new(false, ["controlled-incoherent-private-coverage"]) };
        f.Store.LookupOverride = contradiction switch
        {
            "conflict" => published with { PersistenceStatus = KnowledgeWriteStatus.Conflict },
            "invalid-proposal" => published with { PersistenceStatus = KnowledgeWriteStatus.InvalidProposal },
            "indeterminate" => published with { PersistenceStatus = KnowledgeWriteStatus.CommitIndeterminate },
            "committed-replay" => published with { Status = BusinessFinalizationStatus.PublishedPartial, Coverage = new(true, ["controlled-incoherent-private-coverage"]), PersistenceStatus = KnowledgeWriteStatus.Committed, IsReplay = true },
            _ => published with { Status = BusinessFinalizationStatus.PublishedPartial, Coverage = new(false, ["controlled-incoherent-private-coverage"]) }
        };

        var result = await f.Finalizer().FinalizeAsync(f.Request);

        Assert.Equal(BusinessFinalizationStatus.CommitIndeterminate, result.Status);
        Assert.Null(result.Publication);
        Assert.Empty(result.Coverage.PermittedGaps);
        Assert.Equal(1, f.Slot.Invocations); Assert.Equal(1, f.Store.Publishes); Assert.Equal(4, f.Client.Calls);
        Assert.True(f.Store.Lookups >= 1);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("unavailable")]
    [InlineData("indeterminate")]
    [InlineData("throws")]
    public async Task FinalizeAsync_FreshLookupFailsAfterCachedPublication_DoesNotExposeCachedPublicationOrCoverageAsync(string outage)
    {
        using var f = new FinalizerCase();
        f.Store.PublishStatus = BusinessFinalizationStatus.PublishedPartial;
        f.Store.PublishCoverage = new(true, ["controlled-private-cached-coverage"]);
        var first = await f.Finalizer().FinalizeAsync(f.Request);
        Assert.Equal(BusinessFinalizationStatus.PublishedPartial, first.Status);
        Assert.Equal(f.Publication, first.Publication);
        Assert.Contains("controlled-private-cached-coverage", first.Coverage.PermittedGaps);
        int lookups = f.Store.Lookups;
        if (outage == "throws") f.Store.LookupFailure = new IOException("Controlled fresh owner lookup outage.");
        else f.Store.LookupOverride = f.Result(outage == "unavailable" ? BusinessFinalizationStatus.AuthorityUnavailable : BusinessFinalizationStatus.CommitIndeterminate);

        var replay = await f.Finalizer().FinalizeAsync(f.Request);

        Assert.Equal(outage == "unavailable" ? BusinessFinalizationStatus.AuthorityUnavailable : BusinessFinalizationStatus.CommitIndeterminate, replay.Status);
        Assert.Null(replay.Publication);
        Assert.Empty(replay.Coverage.PermittedGaps);
        Assert.True(f.Store.Lookups > lookups);
        Assert.Equal(1, f.Slot.Invocations); Assert.Equal(1, f.Store.Publishes); Assert.Equal(4, f.Client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FinalizeAsync_FirstRootCompletedSecondRootInvalid_PublishesOriginalSuccessAndExactFailedDispositionOnceAsync()
    {
        using var f = new FinalizerCase();
        f.Store.PublishStatus = BusinessFinalizationStatus.PublishedPartial;
        f.Provider = (call, _) => Task.FromResult(call == 1 ? f.Response(0) : SynthesisCase.JsonResponse("{}"));

        var result = await f.Finalizer().FinalizeAsync(f.Request);
        var replay = await f.Finalizer().FinalizeAsync(f.Request);

        Assert.Equal(BusinessFinalizationStatus.PublishedPartial, result.Status);
        Assert.Equal(result.Publication, replay.Publication);
        Assert.Equal(3, f.Client.Calls); Assert.Equal(1, f.Store.Publishes); Assert.Equal(1, f.Slot.Invocations);
        var submitted = Assert.IsType<FlowFinalizationPublicationRequest>(f.Store.Submitted);
        var completed = Assert.Single(submitted.Completed);
        var failed = Assert.Single(submitted.Dispositions);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Proposal(0)), OctoberKnowledgePolicy.SerializeCanonical(completed.Proposal));
        Assert.Equal(FlowFinalizationDispositionKind.Failed, failed.Kind);
        Assert.False(string.IsNullOrWhiteSpace(failed.Reason));
        Assert.NotEqual(completed.Root, failed.Root);
        Assert.True(f.Request.Selection!.Roots.ToHashSet().SetEquals(new[] { completed.Root, failed.Root }));
        var groups = OctoberFlowPlanner.Plan(f.Input, f.Request.Selection.Limits).Groups;
        Assert.Equal(completed.Root.Document, groups.Single(x => x.GroupVersionId == completed.GroupVersionId).RootVersion);
        Assert.Equal(failed.Root.Document, groups.Single(x => x.GroupVersionId == failed.GroupVersionId).RootVersion);
        Assert.Equal(OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(submitted), submitted.CanonicalOutcomeDigest);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FinalizeAsync_OriginalHostStopsDuringProvider_CancelsTransportAndRetainsConsumedSlotWithoutLaterRootsAsync()
    {
        using var f = new FinalizerCase();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider = async (call, _) => { await release.Task; return f.Response(call - 1); };
        var pending = f.Finalizer().FinalizeAsync(f.Request);
        try
        {
            await f.Client.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            f.Slot.StopHost();
            Assert.True(f.Slot.HostToken.IsCancellationRequested);
            Assert.True(f.Client.LastCancellation.IsCancellationRequested);
            Assert.Equal(1, f.Client.Calls);
            Assert.Equal(0, f.Store.Publishes);
        }
        finally
        {
            release.TrySetResult();
            if (f.Slot.Retained is not null) await f.Slot.Retained.WaitAsync(TimeSpan.FromSeconds(5));
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        var first = await pending;
        var replay = await f.Finalizer().FinalizeAsync(f.Request);
        Assert.Null(first.Publication); Assert.Null(replay.Publication);
        Assert.Empty(first.Coverage.PermittedGaps); Assert.Empty(replay.Coverage.PermittedGaps);
        Assert.DoesNotContain(first.Status, new[] { BusinessFinalizationStatus.Published, BusinessFinalizationStatus.PublishedPartial, BusinessFinalizationStatus.NoContent });
        Assert.DoesNotContain(replay.Status, new[] { BusinessFinalizationStatus.Published, BusinessFinalizationStatus.PublishedPartial, BusinessFinalizationStatus.NoContent });
        Assert.Equal(1, f.Slot.Invocations); Assert.Equal(1, f.Client.Calls); Assert.Equal(0, f.Store.Publishes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("profile")]
    [InlineData("configuration")]
    [InlineData("prompt")]
    [InlineData("digest")]
    [InlineData("null-selection")]
    [InlineData("default-roots")]
    [InlineData("invalid-root")]
    [InlineData("missing-lifetime")]
    public async Task FinalizeAsync_InvalidPinnedRequest_IsDeniedBeforeOriginalSlotOrProviderAsync(string mutation)
    {
        using var f = new FinalizerCase();
        var r = f.Request;
        var s = r.Selection!;
        r = mutation switch
        {
            "profile" => FinalizerCase.Seal(r with { Selection = s with { ProviderProfileId = "unselected-profile" } }),
            "configuration" => FinalizerCase.Seal(r with { Selection = s with { ProviderConfigurationFingerprint = new string('E', 64) } }),
            "prompt" => FinalizerCase.Seal(r with { Selection = s with { PromptDigest = new string('F', 64) } }),
            "digest" => r with { CanonicalPayloadDigest = new string('A', 64) },
            "null-selection" => r with { Selection = null },
            "default-roots" => r with { Selection = s with { Roots = default } },
            "invalid-root" => r with { Selection = s with { Roots = [s.Roots[0] with { UnitId = Guid.Empty }] } },
            "missing-lifetime" => r with { ExecutionScope = new FinalizationForbiddenScope() },
            _ => throw new InvalidOperationException()
        };
        var result = await f.Finalizer().FinalizeAsync(r);
        Assert.Equal(mutation == "missing-lifetime" ? BusinessFinalizationStatus.AuthorityUnavailable : BusinessFinalizationStatus.Denied, result.Status);
        Assert.Null(result.Publication);
        Assert.Equal(0, f.Slot.Calls); Assert.Equal(0, f.Slot.Invocations);
        Assert.Equal(0, f.Store.InitialReads); Assert.Equal(0, f.Store.Publishes);
        Assert.Equal(0, f.Scope.Grants); Assert.Equal(0, f.Client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalizeAsync_MissingOriginalSlotOrSameStoreInitialRead_FailsClosedAsync(bool missingCompanion)
    {
        using var f = new FinalizerCase();
        var finalizer = f.Finalizer(missingSlot: !missingCompanion, store: missingCompanion ? new FinalizerStoreWithoutInitialRead(f.Store) : null);
        var result = await finalizer.FinalizeAsync(f.Request);
        Assert.Equal(BusinessFinalizationStatus.AuthorityUnavailable, result.Status);
        Assert.Null(result.Publication);
        Assert.Equal(0, f.Slot.Calls); Assert.Equal(0, f.Client.Calls); Assert.Equal(0, f.Store.InitialReads);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("existing")]
    [InlineData("conflict")]
    [InlineData("unavailable")]
    public async Task FinalizeAsync_InitialReadWithoutDispatchAuthority_NeverLoadsOrGeneratesAsync(string state)
    {
        using var f = new FinalizerCase();
        var result = state == "conflict" ? f.Result(BusinessFinalizationStatus.Failed) with { PersistenceStatus = KnowledgeWriteStatus.Conflict }
            : f.Result(BusinessFinalizationStatus.Published) with { IsReplay = true };
        f.Store.Initial = state == "unavailable" ? new(BusinessFinalizationInitialReadKind.Unavailable, null)
            : new(BusinessFinalizationInitialReadKind.NoDispatchResult, result);
        if (state != "unavailable") f.Store.Durable = result;
        var actual = await f.Finalizer().FinalizeAsync(f.Request);
        Assert.Equal(state == "unavailable" ? BusinessFinalizationStatus.CommitIndeterminate : result.Status, actual.Status);
        Assert.Equal(1, f.Slot.Invocations); Assert.True(f.Store.InitialObservedInsideOriginalCallback);
        Assert.Equal(0, f.Loader.Calls); Assert.Equal(0, f.Store.Publishes); Assert.Equal(0, f.Client.Calls);
        if (state != "unavailable") Assert.True(f.Store.Lookups >= 1);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("absent-with-result")]
    [InlineData("unavailable-with-result")]
    [InlineData("result-without-result")]
    [InlineData("unknown-kind")]
    public async Task FinalizeAsync_InconsistentInitialReadUnion_IsIndeterminateWithoutGenerationAsync(string shape)
    {
        using var f = new FinalizerCase();
        f.Store.Initial = shape switch
        {
            "absent-with-result" => new(BusinessFinalizationInitialReadKind.ObservedAbsent, f.Result(BusinessFinalizationStatus.Published)),
            "unavailable-with-result" => new(BusinessFinalizationInitialReadKind.Unavailable, f.Result(BusinessFinalizationStatus.Published)),
            "result-without-result" => new(BusinessFinalizationInitialReadKind.NoDispatchResult, null),
            _ => new((BusinessFinalizationInitialReadKind)99, null)
        };
        var result = await f.Finalizer().FinalizeAsync(f.Request);
        Assert.Equal(BusinessFinalizationStatus.CommitIndeterminate, result.Status);
        Assert.Null(result.Publication);
        Assert.Equal(0, f.Loader.Calls); Assert.Equal(0, f.Store.Publishes); Assert.Equal(0, f.Client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FinalizeAsync_OriginalObservedAbsence_SynthesizesSequentiallyAndPublishesOriginalProposalsOnceAsync()
    {
        using var f = new FinalizerCase();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider = async (call, _) => { if (call == 1) await release.Task; return f.Response(call - 1); };
        var pending = f.Finalizer().FinalizeAsync(f.Request);
        try
        {
            await f.Client.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, f.Client.Calls); Assert.Equal(0, f.Store.Publishes);
            Assert.True(f.Store.InitialObservedInsideOriginalCallback);
        }
        finally { release.TrySetResult(); }
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(BusinessFinalizationStatus.Published, result.Status);
        Assert.Equal(f.Publication, result.Publication);
        Assert.Equal(4, f.Client.Calls); Assert.Equal(1, f.Client.PeakActive);
        Assert.Equal(1, f.Slot.Invocations); Assert.Equal(1, f.Store.InitialReads); Assert.Equal(1, f.Store.Publishes);
        Assert.Equal(1, f.Loader.Calls); Assert.True(f.Store.Lookups >= 1);
        var submitted = Assert.IsType<FlowFinalizationPublicationRequest>(f.Store.Submitted);
        Assert.Equal(f.Request.Operation, submitted.Operation); Assert.Equal(f.Request.Corpus, submitted.Corpus);
        Assert.Equal(f.Request.CanonicalPayloadDigest, submitted.CanonicalPayloadDigest);
        Assert.Equal(OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(submitted), submitted.CanonicalOutcomeDigest);
        Assert.Empty(submitted.Dispositions); Assert.Equal(2, submitted.Completed.Length);
        Assert.True(f.Input.Roots.ToHashSet().SetEquals(submitted.Completed.Select(x => x.Root)));
        var proposals = submitted.Completed.Select(x => OctoberKnowledgePolicy.SerializeCanonical(x.Proposal)).ToHashSet();
        Assert.Contains(OctoberKnowledgePolicy.SerializeCanonical(f.Proposal(0)), proposals);
        Assert.Contains(OctoberKnowledgePolicy.SerializeCanonical(f.Proposal(1)), proposals);
        Assert.All(submitted.Completed, value => Assert.DoesNotContain(value.Proposal.Sections, section => section.SectionId == OctoberBusinessSynthesisPolicy.GapSectionId));
        Assert.All(f.Client.Requests, sent => Assert.Empty(sent.Tools));
        Assert.All(f.Disclosure.Seen, value => Assert.True(f.Input.Contributors.ToHashSet().SetEquals(value)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FinalizeAsync_ExplicitEmptySelection_PersistsNoContentWithoutProviderCallsAsync()
    {
        using var f = new FinalizerCase(emptyRoots: true);
        f.Store.PublishStatus = BusinessFinalizationStatus.NoContent;
        var result = await f.Finalizer().FinalizeAsync(f.Request);
        Assert.Equal(BusinessFinalizationStatus.NoContent, result.Status); Assert.Null(result.Publication);
        Assert.Equal(0, f.Client.Calls); Assert.Equal(1, f.Store.Publishes);
        Assert.Empty(f.Store.Submitted!.Completed); Assert.Empty(f.Store.Submitted.Dispositions);
        Assert.True(f.Store.Lookups >= 1);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FinalizeAsync_AmbiguousPublishAndRepeatedCall_OnlyReconcileWithoutRegenerationAsync()
    {
        using var f = new FinalizerCase();
        f.Store.PublishStatus = BusinessFinalizationStatus.CommitIndeterminate;
        var first = await f.Finalizer().FinalizeAsync(f.Request);
        int lookups = f.Store.Lookups;
        var second = await f.Finalizer().FinalizeAsync(f.Request);
        Assert.Equal(BusinessFinalizationStatus.CommitIndeterminate, first.Status);
        Assert.Equal(BusinessFinalizationStatus.CommitIndeterminate, second.Status);
        Assert.Null(first.Publication); Assert.Null(second.Publication);
        Assert.Equal(1, f.Slot.Invocations); Assert.Equal(1, f.Store.Publishes); Assert.Equal(4, f.Client.Calls);
        Assert.True(f.Store.Lookups > lookups);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalizeAsync_FreshOwnerLookupDenial_SuppressesFirstOrCachedReturnAsync(bool denyCachedOnly)
    {
        using var f = new FinalizerCase();
        if (!denyCachedOnly) f.Store.LookupOverride = f.Result(BusinessFinalizationStatus.Denied);
        var first = await f.Finalizer().FinalizeAsync(f.Request);
        if (denyCachedOnly) Assert.Equal(BusinessFinalizationStatus.Published, first.Status);
        else { Assert.Equal(BusinessFinalizationStatus.Denied, first.Status); Assert.Null(first.Publication); }
        int lookups = f.Store.Lookups;
        f.Store.LookupOverride = f.Result(BusinessFinalizationStatus.Denied);
        var replay = await f.Finalizer().FinalizeAsync(f.Request);
        Assert.Equal(BusinessFinalizationStatus.Denied, replay.Status); Assert.Null(replay.Publication);
        Assert.True(f.Store.Lookups > lookups);
        Assert.Equal(1, f.Slot.Invocations); Assert.Equal(1, f.Store.Publishes); Assert.Equal(4, f.Client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FinalizeAsync_CancelledFirstWaiter_DoesNotCancelHostCallbackOrPermitReplacementAsync()
    {
        using var f = new FinalizerCase();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider = async (call, _) => { if (call == 1) await release.Task; return f.Response(call - 1); };
        using var waiting = new CancellationTokenSource();
        var first = f.Finalizer().FinalizeAsync(f.Request, waiting.Token);
        Task<BusinessFinalizationResult>? duplicate = null;
        try
        {
            await f.Client.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            waiting.Cancel();
            try { Assert.Null((await first.WaitAsync(TimeSpan.FromSeconds(5))).Publication); }
            catch (OperationCanceledException) { }
            Assert.False(f.Slot.HostToken.IsCancellationRequested);
            Assert.False(f.Client.LastCancellation.IsCancellationRequested);
            Assert.False(f.Slot.Retained!.IsCompleted);
            duplicate = f.Finalizer().FinalizeAsync(f.Request);
            Assert.Equal(1, f.Slot.Invocations); Assert.Equal(1, f.Client.Calls);
        }
        finally
        {
            release.TrySetResult();
            if (f.Slot.Retained is not null) await f.Slot.Retained.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.NotNull(duplicate);
        var result = await duplicate.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(BusinessFinalizationStatus.Published, result.Status);
        Assert.Equal(1, f.Slot.Invocations); Assert.Equal(1, f.Store.Publishes); Assert.Equal(4, f.Client.Calls);
    }
}

internal sealed class FinalizerCase : IDisposable
{
    public const string ProfileId = "host-profile-id";
    public const string Instructions = "Controlled synthesis instructions: use only accepted evidence; preserve qualifications.";
    public FlowPlanningInput Input { get; }
    public ControlledExecutionScope Scope { get; } = new(initialTokens: 1_000_000);
    public FinalizationRequest Request { get; }
    public FinalizerControlledSlot Slot { get; }
    public FinalizerOwnerStore Store { get; }
    public FinalizerInputLoader Loader { get; }
    public ResolutionClient Client { get; }
    public SynthesisDisclosure Disclosure { get; } = new();
    public PublicationBinding Publication { get; }
    public Func<int, CancellationToken, Task<ChatResponse>>? Provider { get; set; }
    private int _draftCalls;
    public FinalizerCase(bool emptyRoots = false)
    {
        var basis = new FlowPlannerCase(2, [], [0, 1]);
        Input = basis.Input with { Roots = emptyRoots ? [] : basis.Input.Roots };
        Request = Seal(new(new(Input.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, ResolutionCase.Id(99501)), Input.Corpus.Handle, "", Scope)
        {
            Selection = new(Input.Roots, new(), ResolutionCase.Id(99502), ProfileId,
                OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(FinalizationContractCase.Provider), OctoberKnowledgePolicy.Hash(Instructions), 40)
        });
        Publication = new(Input.Corpus.Handle.Binding, ResolutionCase.Id(99503));
        Slot = new(Request);
        Store = new(this);
        Loader = new(Input);
        Client = new(ProfileId, (_, cancellation) => ControlledResponseAsync(cancellation));
    }
    private Task<ChatResponse> ControlledResponseAsync(CancellationToken cancellation)
    {
        var messages = Client.Requests[^1].Messages;
        using var request = JsonDocument.Parse(messages.Last(message => message.Role == ChatRole.User).Text);
        if (request.RootElement.TryGetProperty("phase", out var phase) && phase.GetString() == "semantic-review")
        {
            Assert.Equal(new[] { ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.User }, messages.Select(message => message.Role));
            Assert.False(request.RootElement.TryGetProperty("candidate", out _));
            Assert.False(request.RootElement.TryGetProperty("packet", out _));
            var candidate = JsonSerializer.Deserialize<BusinessSynthesisProposal>(messages[^2].Text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(candidate), messages[^2].Text);
            Assert.Equal(OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(candidate)), request.RootElement.GetProperty("proposalDigest").GetString());
            return Task.FromResult(OctoberBusinessSynthesisTests.SemanticReviewForCandidate(Input, candidate));
        }
        int draft = ++_draftCalls;
        return Provider?.Invoke(draft, cancellation) ?? Task.FromResult(Response(draft - 1));
    }
    public static FinalizationRequest Seal(FinalizationRequest request) => request with { CanonicalPayloadDigest = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(request) };
    public BusinessSynthesisProposal Proposal(int index) => new("业务环节" + index,
        [new("business-step", 0, "处理结果", [new("business-claim", "接收业务请求并执行相应处理。", ClaimQualification.SourceSupported,
            [new(Input.Corpus.Documents[index].Version, "operation", "provider")], [])])]);
    public ChatResponse Response(int index) => new(new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(Proposal(index))))
    { FinishReason = ChatFinishReason.Stop, Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 } };
    public BusinessFinalizationResult Result(BusinessFinalizationStatus status) => new(status, Request.Operation,
        status is BusinessFinalizationStatus.Published or BusinessFinalizationStatus.PublishedPartial ? Publication : null, new(status == BusinessFinalizationStatus.PublishedPartial, []));
    public OctoberBusinessFinalizer Finalizer(bool missingSlot = false, IOctoberFlowPublicationStore? store = null, TimeProvider? timeProvider = null) =>
        OctoberBusinessModule.CreateFinalizer(missingSlot ? null : Slot, store ?? Store, Loader, Client, ProfileId, FinalizationContractCase.Provider, Instructions, Disclosure, timeProvider ?? new SynthesisClock());
    public void Dispose() { Client.Dispose(); Slot.Dispose(); }
}

/// <summary>Explicit controlled single-flight protocol model, not Scan-issued admission authority.</summary>
internal sealed class FinalizerControlledSlot(FinalizationRequest bound) : IOctoberOriginalBusinessFinalizationSlot, IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _host = new();
    private TaskCompletionSource<BusinessFinalizationResult>? _retained;
    public int Calls { get; private set; }
    public int Invocations { get; private set; }
    public bool InsideOriginalCallback { get; private set; }
    public CancellationToken HostToken => _host.Token;
    public void Dispose() => _host.Dispose();
    public void StopHost() => _host.Cancel();
    public Task<BusinessFinalizationResult>? Retained => _retained?.Task;
    public Task<BusinessFinalizationResult> RunOnceAsync(FinalizationRequest request, Func<CancellationToken, Task<BusinessFinalizationResult>> originalWork, CancellationToken waitCancellation = default)
    {
        TaskCompletionSource<BusinessFinalizationResult> retained;
        bool dispatch = false;
        lock (_gate)
        {
            Calls++;
            if (request.Operation != bound.Operation || request.Corpus != bound.Corpus || request.CanonicalPayloadDigest != bound.CanonicalPayloadDigest || !ReferenceEquals(request.ExecutionScope, bound.ExecutionScope))
                throw new InvalidOperationException("Controlled slot rejects replacement bindings.");
            if (_retained is null) { _retained = new(TaskCreationOptions.RunContinuationsAsynchronously); dispatch = true; }
            retained = _retained;
        }
        if (dispatch) _ = ExecuteAsync(retained, originalWork);
        return retained.Task.WaitAsync(waitCancellation);
    }
    private async Task ExecuteAsync(TaskCompletionSource<BusinessFinalizationResult> retained, Func<CancellationToken, Task<BusinessFinalizationResult>> work)
    {
        Invocations++;
        InsideOriginalCallback = true;
        try { retained.TrySetResult(await work(_host.Token)); }
        catch (Exception error) { retained.TrySetException(error); }
        finally { InsideOriginalCallback = false; }
    }
}

internal sealed class FinalizerInputLoader(FlowPlanningInput input) : IOctoberFlowPlanningInputLoader
{
    public int Calls { get; private set; }
    public Task<FlowPlanningInputResult> LoadAsync(CorpusHandle corpus, ImmutableArray<FlowRootSelection> roots, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); Calls++;
        Assert.Equal(input.Corpus.Handle, corpus); Assert.True(input.Roots.ToHashSet().SetEquals(roots));
        return Task.FromResult(new FlowPlanningInputResult(KnowledgeReadStatus.Success, input));
    }
}

internal sealed class FinalizerOwnerStore(FinalizerCase fixture) : IOctoberFlowPublicationStore, IOctoberFlowPublicationInitialRead
{
    public BusinessFinalizationInitialRead Initial { get; set; } = new(BusinessFinalizationInitialReadKind.ObservedAbsent, null);
    public BusinessFinalizationResult? Durable { get; set; }
    public BusinessFinalizationResult? LookupOverride { get; set; }
    public Exception? LookupFailure { get; set; }
    public CoverageProjection? PublishCoverage { get; set; }
    public BusinessFinalizationStatus PublishStatus { get; set; } = BusinessFinalizationStatus.Published;
    public KnowledgeWriteStatus? PublishPersistenceStatus { get; set; }
    public Func<CancellationToken, Task>? BeforePublish { get; set; }
    public int InitialReads { get; private set; }
    public int Publishes { get; private set; }
    public int Lookups { get; private set; }
    public bool InitialObservedInsideOriginalCallback { get; private set; }
    public FlowFinalizationPublicationRequest? Submitted { get; private set; }
    public Task<BusinessFinalizationInitialRead> ReadForOriginalDispatchAsync(CorpusHandle corpus, OperationKey operation, string canonicalPayloadDigest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); InitialReads++;
        InitialObservedInsideOriginalCallback = fixture.Slot.InsideOriginalCallback;
        Assert.True(InitialObservedInsideOriginalCallback);
        Assert.Equal(fixture.Request.Corpus, corpus); Assert.Equal(fixture.Request.Operation, operation); Assert.Equal(fixture.Request.CanonicalPayloadDigest, canonicalPayloadDigest);
        return Task.FromResult(Initial);
    }
    public async Task<BusinessFinalizationResult> PublishAsync(FlowFinalizationPublicationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); Publishes++; Submitted = request;
        if (BeforePublish is not null) await BeforePublish(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var result = fixture.Result(PublishStatus) with { PersistenceStatus = PublishPersistenceStatus };
        if (PublishCoverage is not null) result = result with { Coverage = PublishCoverage };
        if (PublishStatus != BusinessFinalizationStatus.CommitIndeterminate) Durable = result;
        return result;
    }
    public Task<BusinessFinalizationResult> LookupAsync(CorpusHandle corpus, OperationKey operation, string canonicalPayloadDigest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); Lookups++;
        Assert.Equal(fixture.Request.Corpus, corpus); Assert.Equal(fixture.Request.Operation, operation); Assert.Equal(fixture.Request.CanonicalPayloadDigest, canonicalPayloadDigest);
        if (LookupFailure is not null) throw LookupFailure;
        return Task.FromResult(LookupOverride ?? Durable ?? fixture.Result(BusinessFinalizationStatus.CommitIndeterminate));
    }
}

internal sealed class FinalizerStoreWithoutInitialRead(FinalizerOwnerStore inner) : IOctoberFlowPublicationStore
{
    public Task<BusinessFinalizationResult> PublishAsync(FlowFinalizationPublicationRequest request, CancellationToken cancellationToken = default) => inner.PublishAsync(request, cancellationToken);
    public Task<BusinessFinalizationResult> LookupAsync(CorpusHandle corpus, OperationKey operation, string canonicalPayloadDigest, CancellationToken cancellationToken = default) => inner.LookupAsync(corpus, operation, canonicalPayloadDigest, cancellationToken);
}
