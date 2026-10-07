using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulgata.Core.Entities.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Production admission on owned SQLite; live operator and F1 host capabilities are explicitly controlled.</summary>
public sealed class OctoberGenerateAdmissionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Policy_OneBatchKeyAndCanonicalDigest_BindEveryOriginalRequestIdentity()
    {
        var binding = new KnowledgeBinding("controlled/database", "target", Id(1), new string('a', 64));
        var request = new GenerateAdmissionRequest(binding, "操作员😀", Id(2), Id(3), "ignored");
        Assert.Equal(5, (int)OperationKind.BeginGenerate);
        Assert.Equal(new OperationKey(binding.BatchId, OperationKind.BeginGenerate, binding.BatchId), OctoberGenerateAdmissionPolicy.OperationFor(binding));
        string expected = AdmissionProtocol.Digest(request);
        Assert.Equal(expected, OctoberGenerateAdmissionPolicy.ComputePayloadDigest(request));
        Assert.Equal(expected, OctoberGenerateAdmissionPolicy.ComputePayloadDigest(request with { CanonicalPayloadDigest = "different transport digest" }));
        GenerateAdmissionRequest[] mutations =
        [
            request with { Binding = binding with { CanonicalDatabaseIdentity = "other/database" } },
            request with { Binding = binding with { TargetId = "other-target" } },
            request with { Binding = binding with { BatchId = Id(4) } },
            request with { Binding = binding with { SelectionDigest = new string('b', 64) } },
            request with { SubjectId = "other-operator" }, request with { RunId = Id(5) }, request with { AdmissionOperationId = Id(6) }
        ];
        Assert.All(mutations, changed => Assert.NotEqual(expected, OctoberGenerateAdmissionPolicy.ComputePayloadDigest(changed)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task StartFreshExecutionAsync_ExactFreshRequest_StoresOnlyOneOwnerKeyAndPassesExactRunDigestToF1Async()
    {
        await using var f = await AdmissionFixture.CreateAsync();
        var request = f.Request();
        var result = await f.Store.StartFreshExecutionAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        Assert.Equal(new(request.Binding.BatchId, OperationKind.BeginGenerate, request.Binding.BatchId), result.Operation);
        Assert.Null(result.AcceptedFile); Assert.Null(result.Corpus); Assert.Null(result.Publication);
        var expected = new CommitAttempt(result.Operation, request.RunId, request.CanonicalPayloadDigest);
        Assert.Contains(expected, f.Guard.Entries);
        Assert.Equal(expected, Assert.Single(f.Guard.SubmittedAttempts));
        await using var context = f.Knowledge.Factory.CreateDbContext();
        var row = await context.KnowledgeOperations.SingleAsync();
        Assert.Equal(request.Binding.BatchId, row.OperationId);
        Assert.Equal(OperationKind.BeginGenerate, row.Kind);
        Assert.Equal(request.CanonicalPayloadDigest, row.PayloadDigest);
        Assert.Empty(JsonSerializer.Deserialize<ImmutableArray<Contributor>>(row.ContributorsJson));
        Assert.Empty(await context.AcceptedFileManifests.ToArrayAsync());
        Assert.Empty(await context.CorpusSelections.ToArrayAsync());
        Assert.Empty(await context.KnowledgePublications.ToArrayAsync());
        Assert.Equal(0, f.Knowledge.Source.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DisclosureStatus.Denied)]
    [InlineData(DisclosureStatus.AuthorityUnavailable)]
    public async Task StartFreshExecutionAsync_SourceFreePreflightAuthorityFailure_DoesNotClaimOrStartTransactionAsync(DisclosureStatus status)
    {
        await using var f = await AdmissionFixture.CreateAsync();
        f.Authority.Status = status;
        var result = await f.Store.StartFreshExecutionAsync(f.Request());
        Assert.Equal(status == DisclosureStatus.Denied ? KnowledgeWriteStatus.Denied : KnowledgeWriteStatus.AuthorityUnavailable, result.Status);
        Assert.Empty(f.Guard.SubmittedAttempts);
        Assert.Equal(0, f.Knowledge.Transactions.Starts);
        await AssertEmptyAsync(f);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task StartFreshExecutionAsync_WrongPayloadDigest_RejectsBeforeClaimingAnyBatchAsync()
    {
        await using var f = await AdmissionFixture.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, (await f.Store.StartFreshExecutionAsync(f.Request() with { CanonicalPayloadDigest = new string('F', 64) })).Status);
        Assert.Empty(f.Guard.SubmittedAttempts);
        await AssertEmptyAsync(f);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReplayAndLookup_RequireCurrentDedicatedSubjectAuthority_AndNeverSubmitAnotherCommitAsync()
    {
        await using var f = await AdmissionFixture.CreateAsync();
        var request = f.Request();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await f.Store.StartFreshExecutionAsync(request)).Status);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, (await f.Store.StartFreshExecutionAsync(request)).Status);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, (await f.Store.LookupAsync(request)).Status);
        Assert.Single(f.Guard.SubmittedAttempts);
        int checks = f.Authority.Calls;
        f.Authority.Status = DisclosureStatus.Denied;
        Assert.Equal(KnowledgeWriteStatus.Denied, (await f.Store.StartFreshExecutionAsync(request)).Status);
        Assert.Equal(KnowledgeWriteStatus.Denied, (await f.Store.LookupAsync(request)).Status);
        var conflicting = f.Request() with { RunId = Guid.NewGuid() };
        conflicting = AdmissionProtocol.Seal(conflicting);
        Assert.Equal(KnowledgeWriteStatus.Denied, (await f.Store.LookupAsync(conflicting)).Status);
        Assert.True(f.Authority.Calls >= checks + 3);
        f.Authority.Status = DisclosureStatus.Allowed;
        f.Authority.HeldSubject = "another-current-operator";
        Assert.Equal(KnowledgeWriteStatus.Denied, (await f.Store.LookupAsync(request)).Status);
        Assert.Single(f.Guard.SubmittedAttempts);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("run")]
    [InlineData("admission")]
    [InlineData("subject")]
    public async Task StartFreshExecutionAsync_ChangedOriginalIdentity_ConflictsWithoutSecondClaimAsync(string field)
    {
        await using var f = await AdmissionFixture.CreateAsync();
        var original = f.Request();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await f.Store.StartFreshExecutionAsync(original)).Status);
        var changed = field switch
        {
            "run" => original with { RunId = Guid.NewGuid() },
            "admission" => original with { AdmissionOperationId = Guid.NewGuid() },
            _ => original with { SubjectId = "second-held-operator" }
        };
        if (field == "subject") f.Authority.HeldSubject = changed.SubjectId;
        changed = AdmissionProtocol.Seal(changed);
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await f.Store.StartFreshExecutionAsync(changed)).Status);
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await f.Store.LookupAsync(changed)).Status);
        await using var context = f.Knowledge.Factory.CreateDbContext();
        Assert.Equal(original.CanonicalPayloadDigest, (await context.KnowledgeOperations.SingleAsync()).PayloadDigest);
        Assert.Single(f.Guard.SubmittedAttempts);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentRequests_CompeteForSingleBatchClaim_ExactLookupReconcilesWinnerWithoutNewDispatchGrantAsync(bool identical)
    {
        await using var f = await AdmissionFixture.CreateAsync();
        var first = f.Request();
        var second = identical ? first : AdmissionProtocol.Seal(first with { RunId = Guid.NewGuid(), AdmissionOperationId = Guid.NewGuid() });
        var results = await Task.WhenAll(f.Store.StartFreshExecutionAsync(first), f.Store.StartFreshExecutionAsync(second));
        Assert.Single(results.Where(x => x.Status == KnowledgeWriteStatus.Committed));
        await using var context = f.Knowledge.Factory.CreateDbContext();
        var row = await context.KnowledgeOperations.SingleAsync();
        Assert.Equal(first.Binding.BatchId, row.OperationId);
        Assert.Equal(row.PayloadDigest == first.CanonicalPayloadDigest ? KnowledgeWriteStatus.AlreadyCommitted : KnowledgeWriteStatus.Conflict, (await f.Store.LookupAsync(first)).Status);
        Assert.Equal(row.PayloadDigest == second.CanonicalPayloadDigest ? KnowledgeWriteStatus.AlreadyCommitted : KnowledgeWriteStatus.Conflict, (await f.Store.LookupAsync(second)).Status);
        if (identical) Assert.Contains(results, x => x.Status == KnowledgeWriteStatus.AlreadyCommitted);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartFreshExecutionAsync_AcceptedOrFrozenBatch_RejectsAdmissionAndPreservesExistingKnowledgeAsync(bool frozen)
    {
        await using var f = await AdmissionFixture.CreateAsync();
        var accepted = (await f.Knowledge.Writer().AcceptCompletedFileAsync(f.Knowledge.Seal())).AcceptedFile!;
        if (frozen)
        {
            var request = new CorpusFreezeRequest(new(f.Knowledge.Binding.BatchId, OperationKind.FreezeCorpus, Id(81)), f.Knowledge.Binding, "", [accepted], [accepted.Coverage]);
            request = request with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(request) };
            f.Knowledge.FreezeAuthority.Approve(request);
            Assert.Equal(KnowledgeWriteStatus.Committed, (await f.Knowledge.Writer().FreezeCorpusAsync(request)).Status);
        }
        var result = await f.Store.StartFreshExecutionAsync(f.Request());
        Assert.DoesNotContain(result.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted });
        await using var context = f.Knowledge.Factory.CreateDbContext();
        Assert.False(await context.KnowledgeOperations.AnyAsync(x => x.Kind == OperationKind.BeginGenerate));
        Assert.Equal(1, await context.AcceptedFileManifests.CountAsync());
        Assert.Equal(frozen ? 1 : 0, await context.CorpusSelections.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartFreshExecutionAsync_EntryOrSubmissionFence_PreventsClaimAndRollsBackStagedOperationAsync(bool atSubmission)
    {
        await using var f = await AdmissionFixture.CreateAsync();
        f.Guard.EntryFenced = !atSubmission;
        f.Guard.SubmissionFenced = atSubmission;
        var result = await f.Store.StartFreshExecutionAsync(f.Request());
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, result.Status);
        Assert.Empty(f.Guard.SubmittedAttempts);
        if (!atSubmission) Assert.Equal(0, f.Knowledge.Transactions.Starts);
        await AssertEmptyAsync(f);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task StartFreshExecutionAsync_AuthorityLostAfterPreflight_RefusesActualCommitAsync()
    {
        await using var f = await AdmissionFixture.CreateAsync();
        f.Authority.OnCheck = calls => { if (calls >= 2) f.Authority.Status = DisclosureStatus.Denied; };
        Assert.Equal(KnowledgeWriteStatus.Denied, (await f.Store.StartFreshExecutionAsync(f.Request())).Status);
        Assert.True(f.Authority.Calls >= 2);
        Assert.Empty(f.Guard.SubmittedAttempts);
        await AssertEmptyAsync(f);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task StartFreshExecutionAsync_SelectionChangesBeforeTransaction_RevalidatesExactBindingAndWritesNothingAsync()
    {
        await using var f = await AdmissionFixture.CreateAsync();
        f.Guard.BeforeEntry = async () =>
        {
            await using var context = f.Knowledge.Factory.CreateDbContext();
            (await context.Batches.SingleAsync()).TargetId = "changed-target";
            await context.SaveChangesAsync();
        };
        var result = await f.Store.StartFreshExecutionAsync(f.Request());
        Assert.DoesNotContain(result.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted });
        Assert.Empty(f.Guard.SubmittedAttempts);
        await AssertEmptyAsync(f);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LookupAsync_RequestDelayedBeforeLock_AbsentRowStaysIndeterminateAndDoesNotConsumeOrCancelOriginalRunAsync()
    {
        await using var f = await AdmissionFixture.CreateAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Guard.BeforeEntry = () => release.Task;
        var request = f.Request();
        var pending = f.Store.StartFreshExecutionAsync(request);
        try
        {
            await f.Guard.EntryReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var lookup = await f.Store.LookupAsync(request);
            Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, lookup.Status);
            Assert.Null(lookup.AcceptedFile); Assert.Null(lookup.Corpus); Assert.Null(lookup.Publication);
            Assert.False(pending.IsCompleted);
            await AssertEmptyAsync(f);
        }
        finally { release.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.Equal(KnowledgeWriteStatus.Committed, (await pending).Status);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, (await f.Store.LookupAsync(request)).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task StartFreshExecutionAsync_LostAcknowledgementAndLogFailure_RetainsExactClaimAndRetriesStorageOnlyAsync()
    {
        await using var f = await AdmissionFixture.CreateAsync();
        f.Guard.LoseAcknowledgement = true;
        f.Log.Succeeds = false;
        var request = f.Request();
        Assert.Equal(KnowledgeWriteStatus.LogUnavailable, (await f.Store.StartFreshExecutionAsync(request)).Status);
        await using (var context = f.Knowledge.Factory.CreateDbContext()) Assert.Equal(1, await context.KnowledgeOperations.CountAsync());
        f.Log.Succeeds = true;
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, (await f.Store.LookupAsync(request)).Status);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, (await f.Store.StartFreshExecutionAsync(request)).Status);
        Assert.Single(f.Guard.SubmittedAttempts);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CommittedAdmission_FreezesSetupAndGenericProducerLookupCannotDiscloseItsReceiptAsync()
    {
        await using var f = await AdmissionFixture.CreateAsync();
        var request = f.Request();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await f.Store.StartFreshExecutionAsync(request)).Status);
        await using (var context = f.Knowledge.Factory.CreateDbContext())
        {
            (await context.Batches.SingleAsync()).SelectionDigest = new string('b', 64);
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        }
        f.Authority.Status = DisclosureStatus.Denied;
        Assert.Equal(DisclosureStatus.Allowed, f.Knowledge.Authorization.Status);
        var generic = await f.Knowledge.Writer().LookupOperationAsync(request.Binding, OctoberGenerateAdmissionPolicy.OperationFor(request.Binding), request.CanonicalPayloadDigest);
        Assert.Contains(generic.Status, new[] { KnowledgeWriteStatus.InvalidProposal, KnowledgeWriteStatus.Denied });
        Assert.Null(generic.AcceptedFile); Assert.Null(generic.Corpus); Assert.Null(generic.Publication);
        Assert.Equal(KnowledgeWriteStatus.Denied, (await f.Store.LookupAsync(request)).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task StartFreshExecutionAsync_CancelledSubmittedCommit_ReturnsIndeterminateAndRetainsPhysicalResourcesUntilSettlementAsync()
    {
        await using var f = await AdmissionFixture.CreateAsync();
        await using var reference = f.Knowledge.Factory.CreateDbContext();
        var probe = new AdmissionPhysicalCommitProbe();
        var options = new DbContextOptionsBuilder<OctoberDbContext>().UseSqlite(reference.Database.GetConnectionString())
            .AddInterceptors(probe, new AdmissionCommandAdapter()).Options;
        var factory = new AdmissionLifetimeFactory(options);
        var store = new OctoberGenerateAdmissionStore(factory, f.Authority, f.Guard, f.Log);
        using var cancellation = new CancellationTokenSource();
        var request = f.Request();
        var pending = store.StartFreshExecutionAsync(request, cancellation.Token);
        OctoberDbContext? producingContext = null;
        try
        {
            await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            producingContext = Assert.IsType<OctoberDbContext>(probe.ProducingContext);
            cancellation.Cancel();
            Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2))));
            Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, (await pending).Status);
            Assert.False(probe.Disposed.Task.IsCompleted);
            Assert.Null(Record.Exception(() => producingContext!.ChangeTracker.Entries().ToArray()));
            Assert.False(probe.Settled.Task.IsCompleted);
            // A separate ordinary connection sees neither a receipt nor permission to start a replacement run.
            Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, (await f.Store.LookupAsync(request)).Status);
        }
        finally
        {
            probe.Release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            await probe.Settled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        await probe.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await RequireDisposedAsync(producingContext!);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, (await f.Store.LookupAsync(request)).Status);
        await using var final = f.Knowledge.Factory.CreateDbContext();
        Assert.Equal(1, await final.KnowledgeOperations.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task StartFreshExecutionAsync_GuardInvokesCallbackAfterOriginalCancellation_FreshCommitTokenCannotAuthorizeSubmissionAsync()
    {
        await using var f = await AdmissionFixture.CreateAsync();
        var guard = new AdmissionLateCallbackGuard();
        var store = new OctoberGenerateAdmissionStore(f.Knowledge.Factory, f.Authority, guard, f.Log);
        using var cancellation = new CancellationTokenSource();
        var pending = store.StartFreshExecutionAsync(f.Request(), cancellation.Token);
        try
        {
            await guard.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
        }
        finally { guard.Release.TrySetResult(); }
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await guard.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(guard.CallbackInvoked);
        Assert.False(guard.SubmissionReturned);
        Assert.DoesNotContain(result.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted });
        await AssertEmptyAsync(f);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdmissionPublicCall_ContextFactoryIgnoresCancellation_ReturnsBoundedAndDisposesLateContextAsync(bool lookup)
    {
        await using var f = await AdmissionFixture.CreateAsync();
        await using var reference = f.Knowledge.Factory.CreateDbContext();
        var options = new DbContextOptionsBuilder<OctoberDbContext>().UseSqlite(reference.Database.GetConnectionString()).Options;
        var factory = new AdmissionLifetimeFactory(options) { Delay = true };
        var store = new OctoberGenerateAdmissionStore(factory, f.Authority, f.Guard, f.Log);
        using var cancellation = new CancellationTokenSource();
        var pending = lookup ? store.LookupAsync(f.Request(), cancellation.Token) : store.StartFreshExecutionAsync(f.Request(), cancellation.Token);
        try
        {
            await factory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2))));
            Assert.DoesNotContain((await pending).Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted });
            factory.Release.TrySetResult();
            await factory.Returned.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await RequireDisposedAsync(factory.DelayedContext!);
            Assert.Empty(f.Guard.SubmittedAttempts);
            await AssertEmptyAsync(f);
        }
        finally
        {
            factory.Release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            if (factory.DelayedContext is not null) await factory.DelayedContext.DisposeAsync();
        }
    }

    private static async Task RequireDisposedAsync(OctoberDbContext context)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            try { _ = context.Set<KnowledgeOperation>().ToArray(); }
            catch (ObjectDisposedException) { return; }
            await Task.Delay(20);
        }
        Assert.Fail("The late factory result remained undisposed after the bounded caller returned.");
    }

    private static Guid Id(int id) => Guid.Parse($"00000000-0000-0000-0000-{id:000000000000}");
    private static async Task AssertEmptyAsync(AdmissionFixture fixture)
    {
        await using var context = fixture.Knowledge.Factory.CreateDbContext();
        Assert.Equal(0, await context.KnowledgeOperations.CountAsync());
        Assert.Equal(0, await context.AcceptedFileManifests.CountAsync());
    }
}

/// <summary>Independent explicit key ordering/encoding oracle, not the application digest helper.</summary>
internal static class AdmissionProtocol
{
    public static GenerateAdmissionRequest Seal(GenerateAdmissionRequest request) => request with { CanonicalPayloadDigest = Digest(request) };
    public static string Digest(GenerateAdmissionRequest request)
    {
        static string Q<T>(T value) => JsonSerializer.Serialize(value);
        string json = "{\"admissionOperationId\":" + Q(request.AdmissionOperationId) + ",\"binding\":{\"batchId\":" + Q(request.Binding.BatchId) +
            ",\"canonicalDatabaseIdentity\":" + Q(request.Binding.CanonicalDatabaseIdentity) + ",\"selectionDigest\":" + Q(request.Binding.SelectionDigest) +
            ",\"targetId\":" + Q(request.Binding.TargetId) + "},\"domain\":\"october/generate-admission/1\",\"runId\":" + Q(request.RunId) + ",\"subjectId\":" + Q(request.SubjectId) + "}";
        return Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false, true).GetBytes(json)));
    }
}
internal sealed class AdmissionFixture : IAsyncDisposable
{
    private AdmissionFixture(OctoberKnowledgeAcceptanceFixture knowledge)
    {
        Knowledge = knowledge;
        Store = new OctoberGenerateAdmissionStore(knowledge.Factory, Authority, Guard, Log);
    }
    public OctoberKnowledgeAcceptanceFixture Knowledge { get; }
    public AdmissionAuthority Authority { get; } = new();
    public AdmissionGuard Guard { get; } = new();
    public AdmissionLog Log { get; } = new();
    public IOctoberGenerateAdmissionStore Store { get; }
    public GenerateAdmissionRequest Request() => AdmissionProtocol.Seal(new(Knowledge.Binding, "held-operator", Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"), ""));
    public static async Task<AdmissionFixture> CreateAsync() => new(await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync());
    public ValueTask DisposeAsync() => Knowledge.DisposeAsync();
}
internal sealed class AdmissionAuthority : IOctoberGenerateAdmissionAuthority
{
    private int _calls;
    public int Calls => Volatile.Read(ref _calls);
    public string HeldSubject { get; set; } = "held-operator";
    public DisclosureStatus Status { get; set; } = DisclosureStatus.Allowed;
    public Action<int>? OnCheck { get; set; }
    public Task<DisclosureStatus> CheckAsync(GenerateAdmissionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int calls = Interlocked.Increment(ref _calls);
        OnCheck?.Invoke(calls);
        return Task.FromResult(request.SubjectId == HeldSubject ? Status : DisclosureStatus.Denied);
    }
}
internal sealed class AdmissionGuard : IOctoberCommitSubmissionGuard
{
    public List<CommitAttempt> Entries { get; } = [];
    public List<CommitAttempt> SubmittedAttempts { get; } = [];
    public bool EntryFenced { get; set; }
    public bool SubmissionFenced { get; set; }
    public bool LoseAcknowledgement { get; set; }
    public Func<Task>? BeforeEntry { get; set; }
    public TaskCompletionSource EntryReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt, CancellationToken cancellationToken = default)
    {
        lock (Entries) Entries.Add(attempt);
        EntryReached.TrySetResult();
        if (BeforeEntry is { } before) await before().WaitAsync(cancellationToken);
        return EntryFenced ? CommitEligibilityStatus.StaleOrFenced : CommitEligibilityStatus.Eligible;
    }
    public async Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default)
    {
        if (SubmissionFenced) return new(CommitSubmissionStatus.StaleOrFenced, null);
        var submission = submitAsync(cancellationToken);
        lock (SubmittedAttempts) SubmittedAttempts.Add(attempt);
        var settlement = await submission.Settlement;
        return new(CommitSubmissionStatus.Submitted, LoseAcknowledgement ? CommitSettlement.Indeterminate : settlement);
    }
}
internal sealed class AdmissionLog : IOctoberKnowledgeOperationLog
{
    public bool Succeeds { get; set; } = true;
    public Task<bool> AppendAsync(KnowledgeOperationLogEntry entry, CancellationToken cancellationToken = default) => Task.FromResult(Succeeds);
}


/// <summary>Owned SQLite factory that can model an asynchronous factory ignoring cancellation.</summary>
internal sealed class AdmissionLifetimeFactory(DbContextOptions<OctoberDbContext> options) : IDbContextFactory<OctoberDbContext>
{
    public List<OctoberDbContext> CreatedContexts { get; } = [];
    public bool Delay { get; set; }
    public OctoberDbContext? DelayedContext { get; private set; }
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public OctoberDbContext CreateDbContext() => new(options);
    public async Task<OctoberDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        var context = new OctoberDbContext(options);
        lock (CreatedContexts) CreatedContexts.Add(context);
        if (Delay)
        {
            DelayedContext = context;
            Entered.TrySetResult();
            await Release.Task; // Explicitly ignores the caller token to exercise ownership of late arrivals.
            Returned.TrySetResult();
        }
        return context;
    }
}
internal sealed class AdmissionLateCallbackGuard : IOctoberCommitSubmissionGuard
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool CallbackInvoked { get; private set; }
    public bool SubmissionReturned { get; private set; }
    public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt, CancellationToken cancellationToken = default) => Task.FromResult(CommitEligibilityStatus.Eligible);
    public async Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default)
    {
        Entered.TrySetResult();
        await Release.Task;
        try
        {
            CallbackInvoked = true;
            var submitted = submitAsync(CancellationToken.None);
            SubmissionReturned = true;
            return new(CommitSubmissionStatus.Submitted, await submitted.Settlement);
        }
        catch (OperationCanceledException) { return new(CommitSubmissionStatus.CancelledBeforeSubmission, null); }
        finally { Completed.TrySetResult(); }
    }
}

/// <summary>
/// Exposes a controlled provider transaction whose actual CommitAsync ignores cancellation until released.
/// This blocks the physical SQLite commit, not merely a guard acknowledgement after commit.
/// </summary>
internal sealed class AdmissionPhysicalCommitProbe : DbTransactionInterceptor
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Settled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public OctoberDbContext? ProducingContext { get; private set; }
    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result) => Capture(eventData, result);
    public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Capture(eventData, result));
    private DbTransaction Capture(TransactionEndEventData eventData, DbTransaction transaction)
    {
        // Observe the owner of this physical transaction, not whichever context a factory created most recently.
        ProducingContext = Assert.IsType<OctoberDbContext>(eventData.Context);
        return new AdmissionPhysicalTransaction(transaction, this);
    }
}
internal sealed class AdmissionPhysicalTransaction(DbTransaction inner, AdmissionPhysicalCommitProbe probe) : DbTransaction
{
    public DbTransaction Inner => inner;
    public override IsolationLevel IsolationLevel => inner.IsolationLevel;
    protected override DbConnection? DbConnection => inner.Connection;
    public override void Commit() => inner.Commit();
    public override async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        probe.Entered.TrySetResult();
        try
        {
            await probe.Release.Task;
            await inner.CommitAsync(CancellationToken.None);
        }
        finally { probe.Settled.TrySetResult(); }
    }
    public override void Rollback() => inner.Rollback();
    public override Task RollbackAsync(CancellationToken cancellationToken = default) => inner.RollbackAsync(cancellationToken);
    protected override void Dispose(bool disposing)
    {
        if (disposing) { inner.Dispose(); probe.Disposed.TrySetResult(); }
        base.Dispose(disposing);
    }
    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        probe.Disposed.TrySetResult();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Delegates every command to actual SQLite while unwrapping the controlled transaction for its typed setter.</summary>
internal sealed class AdmissionCommandAdapter : DbCommandInterceptor
{
    public override DbCommand CommandCreated(CommandEndEventData eventData, DbCommand result) => new AdmissionSqliteCommand(result);
}
internal sealed class AdmissionSqliteCommand(DbCommand inner) : DbCommand
{
    public override string CommandText { get => inner.CommandText; set => inner.CommandText = value; }
    public override int CommandTimeout { get => inner.CommandTimeout; set => inner.CommandTimeout = value; }
    public override CommandType CommandType { get => inner.CommandType; set => inner.CommandType = value; }
    public override bool DesignTimeVisible { get => inner.DesignTimeVisible; set => inner.DesignTimeVisible = value; }
    public override UpdateRowSource UpdatedRowSource { get => inner.UpdatedRowSource; set => inner.UpdatedRowSource = value; }
    protected override DbConnection? DbConnection { get => inner.Connection; set => inner.Connection = value; }
    protected override DbParameterCollection DbParameterCollection => inner.Parameters;
    protected override DbTransaction? DbTransaction
    {
        get => inner.Transaction;
        set => inner.Transaction = value is AdmissionPhysicalTransaction wrapped ? wrapped.Inner : value;
    }
    public override void Cancel() => inner.Cancel();
    public override int ExecuteNonQuery() => inner.ExecuteNonQuery();
    public override object? ExecuteScalar() => inner.ExecuteScalar();
    public override void Prepare() => inner.Prepare();
    protected override DbParameter CreateDbParameter() => inner.CreateParameter();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => inner.ExecuteReader(behavior);
    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => inner.ExecuteNonQueryAsync(cancellationToken);
    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => inner.ExecuteScalarAsync(cancellationToken);
    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) => inner.ExecuteReaderAsync(behavior, cancellationToken);
    protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    public override async ValueTask DisposeAsync() { await inner.DisposeAsync(); GC.SuppressFinalize(this); }
}
