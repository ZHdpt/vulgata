using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.OctoberScan;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberBusinessRecoveryTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RecoveryV2_ExactPolicyReceipt_ReplaysFencesOldOwnerAndAdmitsFreshV2Async()
    {
        await using var fixture = await BusinessRecoveryFixture.CreateAsync(resolutionPolicyVersion: OctoberResolutionPolicy.RetainedPolicyVersion);
        string before = await fixture.SemanticSnapshotAsync();
        var result = await fixture.Recovery.RecoverAsync(fixture.Request);
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        Assert.Equal(OctoberResolutionPolicy.RetainedPolicyVersion, result.Abandonment!.ResolutionPolicyVersion);
        Assert.Equal(fixture.Begin.CanonicalPayloadDigest, result.Abandonment.BeginPayloadDigest);
        Assert.Equal(before, await fixture.SemanticSnapshotAsync());
        var replay = await fixture.Recovery.RecoverAsync(fixture.Request);
        var lookup = await fixture.Recovery.LookupAsync(fixture.Request);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, lookup.Status);
        Assert.Equal(result.Abandonment, replay.Abandonment);
        Assert.Equal(result.Abandonment, lookup.Abandonment);
        Assert.Equal(CommitEligibilityStatus.StaleOrFenced, await fixture.Store.CheckActiveAsync(fixture.Begin, fixture.Admission));
        var completion = new BusinessFromClCompletionRequest(fixture.Begin, "");
        completion = completion with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeCompletionDigest(completion) };
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, (await fixture.Store.CompleteAsync(completion, fixture.Admission)).Status);
        Assert.Null((await fixture.NewStore().BeginAsync(fixture.Begin)).Admission);
        var fresh = await fixture.NewStore().BeginAsync(BusinessRecoveryFixture.NewBegin(fixture.Begin.Publication,
            fixture.Begin.SubjectId, OctoberResolutionPolicy.RetainedPolicyVersion));
        Assert.Equal(KnowledgeWriteStatus.Committed, fresh.Status);
        Assert.NotNull(fresh.Admission);
        Assert.Equal(OctoberResolutionPolicy.RetainedPolicyVersion, fresh.Snapshot!.ResolutionPolicyVersion);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void RecoveryV2_AbsentPolicy_PreservesExplicitLegacyReceiptBytes()
    {
        var publication = new PublicationBinding(new("controlled/database", "controlled-target", Guid.NewGuid(), new string('a', 64)), Guid.NewGuid());
        var receipt = new BusinessFromClAbandonment(publication, new string('b', 64), "recovery-owner", new string('c', 64))
        { BeginSubjectId = "original-owner" };
        string legacy = OctoberKnowledgePolicy.SerializeCanonical(new
        {
            receipt.Publication, receipt.BeginPayloadDigest, receipt.RecoverySubjectId, receipt.EvidenceDigest, receipt.BeginSubjectId
        });
        Assert.Equal(legacy, OctoberKnowledgePolicy.SerializeCanonical(receipt));
        Assert.Equal(legacy, OctoberKnowledgePolicy.SerializeCanonical(receipt with { ResolutionPolicyVersion = null }));
        Assert.DoesNotContain("resolutionPolicyVersion", legacy, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(null)]
    [InlineData(OctoberResolutionPolicy.PolicyVersion)]
    public async Task RecoveryV2_AlteredReceiptPolicy_CannotReplayOrReleaseAnotherRunAsync(string? changedPolicy)
    {
        await using var fixture = await BusinessRecoveryFixture.CreateAsync(resolutionPolicyVersion: OctoberResolutionPolicy.RetainedPolicyVersion);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Recovery.RecoverAsync(fixture.Request)).Status);
        await using (var context = fixture.Database.Factory.CreateDbContext())
        {
            var row = await context.KnowledgeOperations.AsNoTracking().SingleAsync(value => value.Kind == OperationKind.AbandonBusinessFromCl);
            var receipt = JsonSerializer.Deserialize<KnowledgeWriteResult>(row.CommittedResultJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            string corrupt = OctoberKnowledgePolicy.SerializeCanonical(receipt with
            { Abandonment = receipt.Abandonment! with { ResolutionPolicyVersion = changedPolicy } });
            // Deliberate corruption of this test-owned SQLite receipt, preserving the original digest and journal identity.
            Assert.Equal(1, await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"KnowledgeOperations\" SET \"CommittedResultJson\"={corrupt} WHERE \"Kind\"={(int)OperationKind.AbandonBusinessFromCl}"));
        }
        var replay = await fixture.Recovery.RecoverAsync(fixture.Request);
        var lookup = await fixture.Recovery.LookupAsync(fixture.Request);
        foreach (var result in new[] { replay, lookup })
        {
            Assert.DoesNotContain(result.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted, KnowledgeWriteStatus.LogUnavailable });
            Assert.Null(result.Abandonment);
        }
        var fresh = await fixture.NewStore().BeginAsync(BusinessRecoveryFixture.NewBegin(fixture.Begin.Publication,
            fixture.Begin.SubjectId, OctoberResolutionPolicy.RetainedPolicyVersion));
        Assert.NotEqual(KnowledgeWriteStatus.Committed, fresh.Status);
        Assert.Null(fresh.Admission);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RecoverAsync_ExactAbandonment_PreservesHistoryFencesOldOwnerAndPermitsOnlyFreshRunAsync()
    {
        await using var fixture = await BusinessRecoveryFixture.CreateAsync();
        string before = await fixture.SemanticSnapshotAsync();
        var blocked = await fixture.NewStore().BeginAsync(BusinessRecoveryFixture.NewBegin(fixture.Begin.Publication, fixture.Begin.SubjectId));
        Assert.Equal(KnowledgeWriteStatus.Conflict, blocked.Status);
        Assert.Equal(CommitEligibilityStatus.Eligible, await fixture.Store.CheckActiveAsync(fixture.Begin, fixture.Admission));
        var result = await fixture.Recovery.RecoverAsync(fixture.Request);
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        Assert.Equal(OctoberBusinessFromClRecoveryPolicy.Operation(fixture.Request), result.Operation);
        Assert.Equal(new BusinessFromClAbandonment(fixture.Begin.Publication, fixture.Begin.CanonicalPayloadDigest, fixture.Request.RecoverySubjectId, fixture.Request.EvidenceDigest) { BeginSubjectId = fixture.Begin.SubjectId }, result.Abandonment);
        Assert.Null(result.Publication);
        Assert.Null(result.AcceptedFile);
        Assert.Null(result.Corpus);
        Assert.Equal(before, await fixture.SemanticSnapshotAsync());
        Assert.Equal(CommitEligibilityStatus.StaleOrFenced, await fixture.Store.CheckActiveAsync(fixture.Begin, fixture.Admission));
        var completion = new BusinessFromClCompletionRequest(fixture.Begin, "");
        completion = completion with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeCompletionDigest(completion) };
        var lateNormalCompletion = await fixture.Store.CompleteAsync(completion, fixture.Admission);
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, lateNormalCompletion.Status);
        Assert.Null(lateNormalCompletion.Abandonment);
        var old = await fixture.NewStore().BeginAsync(fixture.Begin);
        Assert.Null(old.Admission);
        var sameOwnerReplay = await fixture.Store.BeginAsync(fixture.Begin);
        Assert.Null(sameOwnerReplay.Admission);
        var fresh = await fixture.NewStore().BeginAsync(BusinessRecoveryFixture.NewBegin(fixture.Begin.Publication, fixture.Begin.SubjectId));
        Assert.Equal(KnowledgeWriteStatus.Committed, fresh.Status);
        Assert.NotNull(fresh.Admission);
        await using var context = fixture.Database.Factory.CreateDbContext();
        Assert.Single(await context.KnowledgeOperations.Where(row => row.Kind == OperationKind.BeginBusinessFromCl && row.OperationId == fixture.Begin.RunId).ToArrayAsync());
        Assert.Single(await context.KnowledgeOperations.Where(row => row.Kind == OperationKind.AbandonBusinessFromCl).ToArrayAsync());
        Assert.Empty(await context.KnowledgeOperations.Where(row => row.Kind == OperationKind.CompleteBusinessFromCl && row.OperationId == fixture.Begin.RunId).ToArrayAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RecoverAsync_IdenticalReplayAndLookup_RetainExactAdministrativeReceiptAsync()
    {
        await using var fixture = await BusinessRecoveryFixture.CreateAsync();
        var first = await fixture.Recovery.RecoverAsync(fixture.Request);
        Assert.Equal(KnowledgeWriteStatus.Committed, first.Status);
        var replay = await fixture.Recovery.RecoverAsync(fixture.Request);
        var lookup = await fixture.Recovery.LookupAsync(fixture.Request);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, lookup.Status);
        Assert.Equal(first.Abandonment, replay.Abandonment);
        Assert.Equal(first.Abandonment, lookup.Abandonment);
        await using var context = fixture.Database.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync(row => row.Kind == OperationKind.AbandonBusinessFromCl));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoverAsync_ChangedRecoveryIdentityOrEvidence_ConflictsWithoutReplacingReceiptAsync(bool subject)
    {
        await using var fixture = await BusinessRecoveryFixture.CreateAsync();
        var first = await fixture.Recovery.RecoverAsync(fixture.Request);
        Assert.Equal(KnowledgeWriteStatus.Committed, first.Status);
        var changed = BusinessRecoveryFixture.Seal(subject ? fixture.Request with { RecoverySubjectId = "different-authorized-recovery-operator" } : fixture.Request with { EvidenceDigest = new string('e', 64) });
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await fixture.Recovery.RecoverAsync(changed)).Status);
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await fixture.Recovery.LookupAsync(changed)).Status);
        Assert.Equal(first.Abandonment, (await fixture.Recovery.LookupAsync(fixture.Request)).Abandonment);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("evidence")]
    [InlineData("recovery-subject")]
    [InlineData("payload")]
    [InlineData("begin-digest")]
    [InlineData("database")]
    [InlineData("publication")]
    [InlineData("run")]
    public async Task RecoverAsync_MalformedOrForeignBinding_NeverAppendsAbandonmentAsync(string mutation)
    {
        await using var fixture = await BusinessRecoveryFixture.CreateAsync();
        var request = fixture.Request;
        if (mutation == "evidence") request = BusinessRecoveryFixture.Seal(request with { EvidenceDigest = "not-a-digest" });
        if (mutation == "recovery-subject") request = BusinessRecoveryFixture.Seal(request with { RecoverySubjectId = "" });
        if (mutation == "payload") request = request with { CanonicalPayloadDigest = new string('f', 64) };
        if (mutation == "begin-digest") request = BusinessRecoveryFixture.Seal(request with { Begin = request.Begin with { CanonicalPayloadDigest = new string('f', 64) } });
        if (mutation == "database")
        {
            var begin = request.Begin with { Publication = request.Begin.Publication with { Knowledge = request.Begin.Publication.Knowledge with { CanonicalDatabaseIdentity = "different-database" } } };
            request = BusinessRecoveryFixture.Seal(request with { Begin = begin with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeBeginDigest(begin) } });
        }
        if (mutation == "publication")
        {
            var begin = request.Begin with { Publication = request.Begin.Publication with { PublicationId = Guid.NewGuid() } };
            request = BusinessRecoveryFixture.Seal(request with { Begin = begin with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeBeginDigest(begin) } });
        }
        if (mutation == "run") request = BusinessRecoveryFixture.Seal(request with { Begin = BusinessRecoveryFixture.NewBegin(request.Begin.Publication, request.Begin.SubjectId) });
        var result = await fixture.Recovery.RecoverAsync(request);
        Assert.DoesNotContain(result.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted, KnowledgeWriteStatus.LogUnavailable });
        Assert.Null(result.Abandonment);
        await fixture.AssertNoAbandonmentAsync();
        Assert.Equal(CommitEligibilityStatus.Eligible, await fixture.Store.CheckActiveAsync(fixture.Begin, fixture.Admission));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(BusinessFromClSettlement.Unsettled)]
    [InlineData(BusinessFromClSettlement.Unavailable)]
    public async Task RecoverAsync_UnprovenOwnerOrSettlement_DoesNotReleaseOldRunAsync(BusinessFromClSettlement settlement)
    {
        await using var fixture = await BusinessRecoveryFixture.CreateAsync();
        fixture.Authority.Status = settlement;
        var result = await fixture.Recovery.RecoverAsync(fixture.Request);
        Assert.DoesNotContain(result.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted, KnowledgeWriteStatus.LogUnavailable });
        Assert.Null(result.Abandonment);
        Assert.Equal(KnowledgeWriteStatus.AuthorityUnavailable, result.Status);
        Assert.True(fixture.Authority.Calls > 0);
        await fixture.AssertNoAbandonmentAsync();
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await fixture.NewStore().BeginAsync(BusinessRecoveryFixture.NewBegin(fixture.Begin.Publication, fixture.Begin.SubjectId))).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RecoverAsync_SubmissionFence_RollsBackWithoutAdministrativeReceiptAsync()
    {
        await using var fixture = await BusinessRecoveryFixture.CreateAsync();
        fixture.Database.Guard.BeforeSubmission = () => fixture.Database.Guard.Fenced = true;
        var result = await fixture.Recovery.RecoverAsync(fixture.Request);
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, result.Status);
        Assert.Null(result.Abandonment);
        await fixture.AssertNoAbandonmentAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RecoverAsync_DeniedSubmissionAuthority_PreservesUnmatchedBeginAsync()
    {
        await using var fixture = await BusinessRecoveryFixture.CreateAsync();
        fixture.Database.Guard.Authority = () => DisclosureStatus.Denied;
        var result = await fixture.Recovery.RecoverAsync(fixture.Request);
        Assert.Equal(KnowledgeWriteStatus.Denied, result.Status);
        Assert.Null(result.Abandonment);
        await fixture.AssertNoAbandonmentAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BeginAsync_TamperedAdministrativeReceipt_NeverTreatsItAsValidatedTerminalAsync()
    {
        await using var fixture = await BusinessRecoveryFixture.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Recovery.RecoverAsync(fixture.Request)).Status);
        await using (var context = fixture.Database.Factory.CreateDbContext())
        {
            var row = await context.KnowledgeOperations.AsNoTracking().SingleAsync(item => item.Kind == OperationKind.AbandonBusinessFromCl);
            var receipt = JsonSerializer.Deserialize<KnowledgeWriteResult>(row.CommittedResultJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            string corrupt = OctoberKnowledgePolicy.SerializeCanonical(receipt with { Abandonment = receipt.Abandonment! with { BeginSubjectId = "forged-original-owner" } });
            // Deliberate corruption in this owned SQLite database; no history mutation is performed against PostgreSQL.
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"KnowledgeOperations\" SET \"CommittedResultJson\"={corrupt} WHERE \"Kind\"={(int)OperationKind.AbandonBusinessFromCl}");
        }
        var recoveryReplay = await fixture.Recovery.RecoverAsync(fixture.Request);
        Assert.DoesNotContain(recoveryReplay.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted, KnowledgeWriteStatus.LogUnavailable });
        Assert.Null(recoveryReplay.Abandonment);
        var recoveryLookup = await fixture.Recovery.LookupAsync(fixture.Request);
        Assert.DoesNotContain(recoveryLookup.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted, KnowledgeWriteStatus.LogUnavailable });
        Assert.Null(recoveryLookup.Abandonment);
        var fresh = await fixture.NewStore().BeginAsync(BusinessRecoveryFixture.NewBegin(fixture.Begin.Publication, fixture.Begin.SubjectId));
        Assert.NotEqual(KnowledgeWriteStatus.Committed, fresh.Status);
        Assert.Null(fresh.Admission);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BeginAsync_SelfConsistentTerminalPayloadForForeignBatch_DoesNotReleaseLocalBeginAsync()
    {
        await using var fixture = await BusinessRecoveryFixture.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Recovery.RecoverAsync(fixture.Request)).Status);
        await using (var context = fixture.Database.Factory.CreateDbContext())
        {
            var beginRow = await context.KnowledgeOperations.AsNoTracking().SingleAsync(item => item.Kind == OperationKind.BeginBusinessFromCl);
            var abandonedRow = await context.KnowledgeOperations.AsNoTracking().SingleAsync(item => item.Kind == OperationKind.AbandonBusinessFromCl);
            var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var beginResult = JsonSerializer.Deserialize<KnowledgeWriteResult>(beginRow.CommittedResultJson, json)!;
            var abandonedResult = JsonSerializer.Deserialize<KnowledgeWriteResult>(abandonedRow.CommittedResultJson, json)!;
            var foreignBinding = fixture.Begin.Publication.Knowledge with { BatchId = Guid.NewGuid() };
            var foreignBegin = fixture.Begin with { Publication = fixture.Begin.Publication with { Knowledge = foreignBinding } };
            foreignBegin = foreignBegin with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeBeginDigest(foreignBegin) };
            var foreignRecovery = BusinessRecoveryFixture.Seal(fixture.Request with { Begin = foreignBegin });
            string beginJson = OctoberKnowledgePolicy.SerializeCanonical(beginResult with
            {
                Publication = foreignBegin.Publication,
                Corpus = beginResult.Corpus! with { Binding = foreignBinding }
            });
            string abandonedJson = OctoberKnowledgePolicy.SerializeCanonical(abandonedResult with
            {
                Abandonment = new(foreignBegin.Publication, foreignBegin.CanonicalPayloadDigest, foreignRecovery.RecoverySubjectId, foreignRecovery.EvidenceDigest)
                { BeginSubjectId = foreignBegin.SubjectId }
            });
            // Keep physical journal keys in the local batch while making both embedded payloads/digests mutually consistent.
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"KnowledgeOperations\" SET \"PayloadDigest\"={foreignBegin.CanonicalPayloadDigest}, \"CommittedResultJson\"={beginJson} WHERE \"Kind\"={(int)OperationKind.BeginBusinessFromCl}");
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"KnowledgeOperations\" SET \"PayloadDigest\"={foreignRecovery.CanonicalPayloadDigest}, \"CommittedResultJson\"={abandonedJson} WHERE \"Kind\"={(int)OperationKind.AbandonBusinessFromCl}");
        }
        var fresh = await fixture.NewStore().BeginAsync(BusinessRecoveryFixture.NewBegin(fixture.Begin.Publication, fixture.Begin.SubjectId));
        Assert.NotEqual(KnowledgeWriteStatus.Committed, fresh.Status);
        Assert.Null(fresh.Admission);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RecoverAsync_LogFailureAfterCommit_StillAllowsExactLookupWithoutRepeatingRecoveryAsync()
    {
        await using var fixture = await BusinessRecoveryFixture.CreateAsync();
        fixture.Database.Log.Succeeds = false;
        var result = await fixture.Recovery.RecoverAsync(fixture.Request);
        Assert.Equal(KnowledgeWriteStatus.LogUnavailable, result.Status);
        Assert.NotNull(result.Abandonment);
        fixture.Database.Log.Succeeds = true;
        Assert.Equal(result.Abandonment, (await fixture.Recovery.LookupAsync(fixture.Request)).Abandonment);
        Assert.Equal(CommitEligibilityStatus.StaleOrFenced, await fixture.Store.CheckActiveAsync(fixture.Begin, fixture.Admission));
    }
}

/// <summary>Controlled generated CL, real continuation/recovery stores, explicit host settlement adapter; no live provider or process-death claim.</summary>
internal sealed class BusinessRecoveryFixture : IAsyncDisposable
{
    private BusinessRecoveryFixture(OctoberScanGenerateRunnerTests.Harness harness) { Harness = harness; }
    public OctoberScanGenerateRunnerTests.Harness Harness { get; }
    public OctoberKnowledgeAcceptanceFixture Database => Harness.Database;
    public RecoveryTestAuthority Authority { get; } = new();
    public OctoberBusinessFromClStore Store { get; private set; } = null!;
    public BusinessFromClRequest Begin { get; private set; } = null!;
    public BusinessFromClAdmission Admission { get; private set; } = null!;
    public BusinessFromClRecoveryRequest Request { get; private set; } = null!;
    public OctoberBusinessFromClRecoveryStore Recovery => new(Database.Factory, Authority, Database.Guard, Database.Log);
    public OctoberBusinessFromClStore NewStore() => new(Database.Factory, new AllowedContinuationAuthority(), Database.Guard, Database.Guard, new SettledContinuationAuthority(), Database.Log);
    public static async Task<BusinessRecoveryFixture> CreateAsync(OctoberKnowledgeAcceptanceFixture? supplied = null, string? resolutionPolicyVersion = null)
    {
        var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync(supplied);
        var fixture = new BusinessRecoveryFixture(harness);
        try
        {
            var generated = await harness.Runner.ExecuteAsync(harness.Execution);
            Assert.Equal(0, generated.Report!.ExitCode);
            fixture.Begin = NewBegin(Assert.IsType<PublicationBinding>(generated.Report.Publication), harness.Execution.Session.SubjectId, resolutionPolicyVersion);
            fixture.Store = fixture.NewStore();
            var result = await fixture.Store.BeginAsync(fixture.Begin);
            Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
            fixture.Admission = Assert.IsAssignableFrom<BusinessFromClAdmission>(result.Admission);
            fixture.Request = Seal(new(fixture.Begin, "authorized-recovery-operator", new string('d', 64), ""));
            fixture.Authority.Status = BusinessFromClSettlement.Settled;
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }
    public static BusinessFromClRequest NewBegin(PublicationBinding publication, string subject, string? resolutionPolicyVersion = null)
    {
        var request = new BusinessFromClRequest(publication, subject, Guid.NewGuid(), "") { ResolutionPolicyVersion = resolutionPolicyVersion };
        return request with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeBeginDigest(request) };
    }
    public static BusinessFromClRecoveryRequest Seal(BusinessFromClRecoveryRequest request) => request with { CanonicalPayloadDigest = OctoberBusinessFromClRecoveryPolicy.ComputeDigest(request) };
    public async Task AssertNoAbandonmentAsync()
    {
        await using var context = Database.Factory.CreateDbContext();
        Assert.Empty(await context.KnowledgeOperations.Where(row => row.Kind == OperationKind.AbandonBusinessFromCl).ToArrayAsync());
    }
    public async Task<string> SemanticSnapshotAsync()
    {
        await using var context = Database.Factory.CreateDbContext();
        return OctoberKnowledgePolicy.SerializeCanonical(new
        {
            publications = await context.KnowledgePublications.AsNoTracking().OrderBy(row => row.PublicationId).ToArrayAsync(),
            members = await context.PublicationMembers.AsNoTracking().OrderBy(row => row.PublicationId).ThenBy(row => row.Ordinal).ToArrayAsync(),
            versions = await context.DocumentVersions.AsNoTracking().OrderBy(row => row.VersionId).ToArrayAsync(),
            originalBegin = await context.KnowledgeOperations.AsNoTracking().SingleAsync(row => row.Kind == OperationKind.BeginBusinessFromCl && row.OperationId == Begin.RunId),
            invocationMarkers = await context.KnowledgeOperations.AsNoTracking().Where(row => row.Kind == OperationKind.BeginGenerate).OrderBy(row => row.OperationId).ToArrayAsync()
        });
    }
    public ValueTask DisposeAsync() => Harness.DisposeAsync();
    private sealed class AllowedContinuationAuthority : IOctoberBusinessFromClAuthority
    {
        public Task<DisclosureStatus> CheckAsync(BusinessFromClRequest request, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default) => Task.FromResult(DisclosureStatus.Allowed);
    }
    private sealed class SettledContinuationAuthority : IOctoberBusinessFromClSettlementAuthority
    {
        public Task<BusinessFromClSettlement> VerifyAsync(BusinessFromClCompletionRequest request, BusinessFromClAdmission admission, CancellationToken cancellationToken = default) => Task.FromResult(BusinessFromClSettlement.Settled);
    }
}
internal sealed class RecoveryTestAuthority : IOctoberBusinessFromClRecoveryAuthority
{
    public BusinessFromClSettlement Status { get; set; } = BusinessFromClSettlement.Unsettled;
    public int Calls { get; private set; }
    public Task<BusinessFromClSettlement> VerifyAsync(BusinessFromClRecoveryRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(Status);
    }
}
