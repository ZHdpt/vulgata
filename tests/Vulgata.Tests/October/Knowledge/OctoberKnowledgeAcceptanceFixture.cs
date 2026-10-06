using System.Collections.Immutable;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Owned SQLite plus controlled host capabilities. Tests call the production policy/writer, never a fixture writer.</summary>
internal sealed class OctoberKnowledgeAcceptanceFixture : IAsyncDisposable
{
    private readonly string? _ownedDirectory;
    private OctoberKnowledgeAcceptanceFixture(DbContextOptions<OctoberDbContext> options, string? ownedDirectory, TestTransactionCounter? transactions = null)
    {
        Factory = new KnowledgeContextFactory(options);
        Transactions = transactions ?? new TestTransactionCounter();
        _ownedDirectory = ownedDirectory;
        Source = new TestKnowledgeSourceVerifier(new(Origin, 100));
        Guard.Authority = () => Authorization.Status;
    }
    public KnowledgeContextFactory Factory { get; }
    public TestTransactionCounter Transactions { get; }
    public TestCompletionAuthority Completion { get; } = new();
    public TestKnowledgeSourceVerifier Source { get; }
    public TestProducerAuthorization Authorization { get; } = new();
    public TestAcceptanceGuard Guard { get; } = new();
    public TestKnowledgeLog Log { get; } = new();
    public TestCorpusFreezeAuthority FreezeAuthority { get; } = new();
    public static Guid Id(int id) => Guid.Parse($"00000000-0000-0000-0000-{id:000000000000}");
    public KnowledgeBinding Binding { get; } = new("ControlledFixture/b0a", "b0a-owned-target", Id(301), new string('a', 64));
    public PreparedOrigin Origin { get; } = new(Id(302), "prepared-b0a-root", "src/Order.cs", new string('b', 64), "prepared-source-v1");
    public Contributor SourceContributor => new(ContributorKind.Source, Origin.RepositoryId, null, Origin.RootBindingIdentity, null, Origin.SourceDigest);
    public const string AdvisorySnapshot = "Selected repository advisory snapshot. Preserve its exact bytes.\n";
    public const string UncitedSnapshot = "Selected global advisory supplied but not cited.";
    public Contributor AdvisoryContributor => new(ContributorKind.RepositoryAdvisory, Origin.RepositoryId, null, null, Id(303), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AdvisorySnapshot))));
    public Contributor UncitedContributor => new(ContributorKind.GlobalAdvisory, null, null, null, Id(390), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(UncitedSnapshot))));
    public CompletedFileProposal Proposal
    {
        get
        {
            var evidence = new EvidenceReference(null, null, new(Origin, 10, 15), null, Origin.SourceDigest);
            var source = new QualifiedClaimGroup("action", "Acquires the order lock.", ClaimQualification.SourceSupported, [evidence], []);
            var advisory = new QualifiedClaimGroup("motive", "Advisory context suggests avoiding duplicate orders.", ClaimQualification.Advisory, [new(null, null, null, Id(303), AdvisoryContributor.ContentDigest)], ["AdvisoryNotSourceProof"]);
            return new(Binding, Id(304), Id(305), Origin, "",
                [Unit("operation:order", null, "Order operation", [new(Origin, 10, 20)], [new("overview", 0, "Overview", [source, advisory])]),
                 Unit("operation:order/helper", "operation:order", "Order helper", [new(Origin, 12, 15)], [new("overview", 0, "Overview", [source])])],
                [new("provider-call", "operation:order", "caller", "binding", "submit", "orders", ["binding-key"], ClaimQualification.SourceSupported, [evidence])],
                [new("unknown-destination", "The runtime destination is unknown.", [evidence], ["No deployment evidence"])],
                [SourceContributor, AdvisoryContributor], new(Id(304), Origin, CoverageDisposition.Accepted, null));
        }
    }
    public static SemanticUnitProposal Unit(string selector, string? parent, string title, ImmutableArray<SourceAnchor> anchors, ImmutableArray<DocumentSection> sections)
    {
        // Independent transport serialization deliberately does not use production CreateContentJson.
        string json = JsonSerializer.Serialize(new { schemaVersion = 1, title, sections }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new(selector, parent, title, json, anchors, sections);
    }
    public CompletedFileSubmission Seal(CompletedFileProposal? proposal = null, Guid? operationId = null)
    {
        proposal ??= Proposal;
        proposal = proposal with { CanonicalProposalDigest = OctoberKnowledgePolicy.ComputeProposalDigest(proposal) };
        return new(new(proposal.Binding.BatchId, OperationKind.AcceptFile, operationId ?? Id(306)), proposal, new TestCompletedFileProof(proposal));
    }
    public OctoberKnowledgeWriter Writer() => new(Factory, Completion, Source, Authorization, Guard, Log, FreezeAuthority);
    public static async Task<OctoberKnowledgeAcceptanceFixture> CreateSqliteAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "vulgata-knowledge-b0a-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var transactions = new TestTransactionCounter();
        var options = new DbContextOptionsBuilder<OctoberDbContext>().AddInterceptors(transactions).UseSqlite($"Data Source={Path.Combine(directory, "knowledge.db")};Foreign Keys=True;Pooling=False").Options;
        var fixture = new OctoberKnowledgeAcceptanceFixture(options, directory, transactions);
        await using var context = fixture.Factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        await fixture.SeedAsync();
        fixture.Transactions.Reset();
        return fixture;
    }
    public static async Task<OctoberKnowledgeAcceptanceFixture> CreateWithOptionsAsync(DbContextOptions<OctoberDbContext> options)
    {
        var fixture = new OctoberKnowledgeAcceptanceFixture(options, null);
        await fixture.SeedAsync();
        fixture.Transactions.Reset();
        return fixture;
    }
    private async Task SeedAsync()
    {
        await using var context = Factory.CreateDbContext();
        context.Batches.Add(new() { BatchId = Binding.BatchId, TargetId = Binding.TargetId, CanonicalDatabaseIdentity = Binding.CanonicalDatabaseIdentity, SelectionDigest = Binding.SelectionDigest, SubjectId = "owned-operator", ManagementAuthorityId = "controlled-authority", State = OctoberBatchState.Selectable, SelectedAtUtc = DateTimeOffset.UtcNow });
        context.RepositorySelections.Add(new() { BatchId = Binding.BatchId, RepositoryId = Origin.RepositoryId, RootBindingIdentity = Origin.RootBindingIdentity, OperatorLabel = "owned-root" });
        context.ContextReferences.Add(new() { BatchId = Binding.BatchId, Scope = 2, RecordId = Id(303) });
        context.ContextReferences.Add(new() { BatchId = Binding.BatchId, Scope = 0, RecordId = Id(390) });
        context.AdvisoryEntries.Add(new() { BatchId = Binding.BatchId, RepositoryId = Origin.RepositoryId, Scope = 2, OriginRecordId = Id(303), Value = AdvisorySnapshot, Applicability = 0, EvidenceKind = 0 });
        context.AdvisoryEntries.Add(new() { BatchId = Binding.BatchId, RepositoryId = Origin.RepositoryId, Scope = 0, OriginRecordId = Id(390), Value = UncitedSnapshot, Applicability = 0, EvidenceKind = 0 });
        await context.SaveChangesAsync();
    }
    public async Task AssertNoKnowledgeAsync()
    {
        await using var context = Factory.CreateDbContext();
        Assert.Equal(0, await context.KnowledgeOperations.CountAsync());
        Assert.Equal(0, await context.AcceptedFileManifests.CountAsync());
        Assert.Equal(0, await context.CodeUnits.CountAsync());
        Assert.Equal(0, await context.DocumentVersions.CountAsync());
        Assert.Equal(0, await context.DocumentSections.CountAsync());
        Assert.Equal(0, await context.ClaimGroups.CountAsync());
        Assert.Equal(0, await context.KnowledgeContributors.CountAsync());
        Assert.Equal(0, await context.CommunicationObservations.CountAsync());
        Assert.Equal(0, await context.KnowledgeUncertainties.CountAsync());
    }
    public ValueTask DisposeAsync()
    {
        if (_ownedDirectory is not null) Directory.Delete(_ownedDirectory, true);
        return ValueTask.CompletedTask;
    }
}

internal sealed class KnowledgeContextFactory(DbContextOptions<OctoberDbContext> options) : IDbContextFactory<OctoberDbContext>
{
    public bool Available { get; set; } = true;
    public OctoberDbContext CreateDbContext() => Available ? new(options) : throw new IOException("Controlled context creation outage.");
    public Task<OctoberDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
}
internal sealed class TestCompletedFileProof(CompletedFileProposal proposal) : CompletedFileProof(proposal.AdmissionId, proposal.AttemptId, proposal.CanonicalProposalDigest);
internal sealed class ForgedCompletedFileProof(CompletedFileProposal proposal) : CompletedFileProof(proposal.AdmissionId, proposal.AttemptId, proposal.CanonicalProposalDigest);
internal sealed class TestCompletionAuthority : IOctoberFileAcceptanceAuthority
{
    public int Calls { get; private set; }
    public DisclosureStatus Status { get; set; } = DisclosureStatus.Allowed;
    public Task<DisclosureStatus> VerifyAsync(CompletedFileSubmission submission, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(submission.Proof is TestCompletedFileProof && submission.Proof.AdmissionId == submission.Proposal.AdmissionId && submission.Proof.AttemptId == submission.Proposal.AttemptId && submission.Proof.ProposalDigest == submission.Proposal.CanonicalProposalDigest ? Status : DisclosureStatus.Denied);
    }
}
internal sealed class TestKnowledgeSourceVerifier(VerifiedKnowledgeSource source) : IOctoberKnowledgeSourceVerifier
{
    public VerifiedKnowledgeSource? Result { get; set; } = source;
    public int Calls { get; private set; }
    public Task<VerifiedKnowledgeSource?> VerifyAsync(KnowledgeBinding binding, PreparedOrigin origin, CancellationToken cancellationToken = default) { Calls++; return Task.FromResult(Result); }
}
internal sealed class TestProducerAuthorization : IOctoberKnowledgeProducerAuthorization
{
    public DisclosureStatus Status { get; set; } = DisclosureStatus.Allowed;
    public ImmutableArray<Contributor> LastContributors { get; private set; }
    public Task<DisclosureStatus> CheckAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default) { LastContributors = contributors; return Task.FromResult(Status); }
}
internal sealed class TestKnowledgeLog : IOctoberKnowledgeOperationLog
{
    public bool Succeeds { get; set; } = true;
    public List<KnowledgeOperationLogEntry> Entries { get; } = [];
    public Task<bool> AppendAsync(KnowledgeOperationLogEntry entry, CancellationToken cancellationToken = default) { Entries.Add(entry); return Task.FromResult(Succeeds); }
}
/// <summary>Controlled Scan guard implements actual current eligibility and synchronous submission handoff; it never implements persistence.</summary>
internal sealed class TestAcceptanceGuard : IOctoberCommitSubmissionGuard
{
    public int Clock { get; set; }
    public int Deadline { get; set; } = 10;
    public bool Fenced { get; set; }
    public int Entries { get; private set; }
    public int Submissions { get; private set; }
    public Action? BeforeSubmission { get; set; }
    public Action? AfterSettlement { get; set; }
    public Func<DisclosureStatus> Authority { get; set; } = () => DisclosureStatus.Allowed;
    public bool LoseAcknowledgement { get; set; }
    public TaskCompletionSource<CommitSettlement>? SettlementOverride { get; set; }
    public TaskCompletionSource<bool> Submitted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt, CancellationToken cancellationToken = default)
    {
        Entries++;
        return Task.FromResult(Fenced || Clock >= Deadline ? CommitEligibilityStatus.StaleOrFenced : CommitEligibilityStatus.Eligible);
    }
    public async Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default)
    {
        BeforeSubmission?.Invoke();
        if (Fenced || Clock >= Deadline) return new(CommitSubmissionStatus.StaleOrFenced, null);
        var authority = Authority();
        if (authority != DisclosureStatus.Allowed) return new(authority == DisclosureStatus.AuthorityUnavailable ? CommitSubmissionStatus.AuthorityUnavailable : CommitSubmissionStatus.Denied, null);
        cancellationToken.ThrowIfCancellationRequested();
        var commit = submitAsync(cancellationToken);
        Submissions++;
        Submitted.TrySetResult(true);
        var settlement = await commit.Settlement;
        AfterSettlement?.Invoke();
        if (SettlementOverride is not null) settlement = await SettlementOverride.Task;
        return new(CommitSubmissionStatus.Submitted, LoseAcknowledgement ? CommitSettlement.Indeterminate : settlement);
    }
}





/// <summary>Counts actual provider transaction starts; fixture creation/schema/seed starts are reset before test actions.</summary>
internal sealed class TestTransactionCounter : DbTransactionInterceptor
{
    private int _starts;
    public int Starts => Volatile.Read(ref _starts);
    public void Reset() => Interlocked.Exchange(ref _starts, 0);
    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        Interlocked.Increment(ref _starts);
        return result;
    }
    public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _starts);
        return ValueTask.FromResult(result);
    }
}

/// <summary>Explicit test-only Scan terminal-inventory attestation; no request is eligible until approved by fixture setup.</summary>
internal sealed class TestCorpusFreezeAuthority : IOctoberCorpusFreezeAuthority
{
    private CorpusFreezeRequest? _approved;
    public CorpusFreezeEligibility Eligibility { get; set; } = CorpusFreezeEligibility.Unsettled;
    public int Calls { get; private set; }
    public void Approve(CorpusFreezeRequest request)
    {
        _approved = request;
        Eligibility = CorpusFreezeEligibility.Eligible;
    }
    public Task<CorpusFreezeEligibility> VerifyAsync(CorpusFreezeRequest request, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Eligibility != CorpusFreezeEligibility.Eligible) return Task.FromResult(Eligibility);
        bool exact = _approved is not null && _approved.Operation == request.Operation && _approved.Binding == request.Binding &&
            _approved.CanonicalPayloadDigest == request.CanonicalPayloadDigest &&
            OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(request) == _approved.CanonicalPayloadDigest;
        return Task.FromResult(exact ? CorpusFreezeEligibility.Eligible : CorpusFreezeEligibility.Denied);
    }
}
