using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>ControlledFixture only: synthetic data and explicit model adapters, never generated Knowledge or a production service.</summary>
internal sealed class OctoberKnowledgeFixture
{
    public const string Provenance = "ControlledFixture";
    public static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    public KnowledgeBinding Binding { get; } = new("ControlledFixture/database", "controlled-target", Id(1), "controlled-selection");
    public Guid RepositoryA => Id(2);
    public Guid RepositoryB => Id(3);
    public Guid RepositoryC => Id(4);
    public Guid PublicationId => Id(5);
    public Guid EarlyPublicationId => Id(6);
    public PreparedOrigin Origin => new(RepositoryA, "controlled-root-a", "src/Order.cs", "controlled-source-digest", "controlled-prepared-v1");
    public Contributor SourceA => new(ContributorKind.Source, RepositoryA, null, "controlled-root-a", null, "controlled-source-digest");
    public Contributor SourceB => new(ContributorKind.Source, RepositoryB, null, "controlled-root-b", null, "controlled-provider-digest");
    public Contributor SourceC => new(ContributorKind.Source, RepositoryC, null, "controlled-root-c", null, "controlled-denied-digest");
    public Contributor Advisory => new(ContributorKind.RepositoryAdvisory, RepositoryA, null, null, Id(7), "controlled-advisory-digest");
    public DocumentVersionReference ClA => new(Binding.BatchId, Id(10), Id(11));
    public DocumentVersionReference ClAHelper => new(Binding.BatchId, Id(16), Id(17));
    public DocumentVersionReference ClB => new(Binding.BatchId, Id(12), Id(13));
    public DocumentVersionReference ClC => new(Binding.BatchId, Id(14), Id(15));
    public DocumentVersionReference BlA => new(Binding.BatchId, Id(20), Id(21));
    public DocumentVersionReference BlHidden => new(Binding.BatchId, Id(22), Id(23));
    public DocumentVersionReference BlA2 => new(Binding.BatchId, Id(24), Id(25));
    public DocumentHandle Handle(DocumentVersionReference document, Guid? publication = null, string? section = null) => new(document.BatchId, publication ?? PublicationId, document.LogicalDocumentId, document.DocumentVersionId, section);
    public EvidenceReference SourceEvidence => new(ClA, "source-lock", new(Origin, 10, 14), null, "controlled-source-digest");
    public QualifiedClaimGroup SourceClaim => new("source-lock", "The operation acquires a lock.", ClaimQualification.SourceSupported, [SourceEvidence], []);
    public PreparedOrigin ProviderOrigin => new(RepositoryB, "controlled-root-b", "src/Provider.cs", "controlled-provider-digest", "controlled-prepared-v1");
    public QualifiedClaimGroup ProviderClaim => new("source-provider", "The provider handles the requested operation.", ClaimQualification.SourceSupported, [new(ClB, "source-provider", new(ProviderOrigin, 5, 9), null, "controlled-provider-digest")], []);
    public QualifiedClaimGroup DeniedClaim => new("source-denied", "The restricted provider handles its own operation.", ClaimQualification.SourceSupported, [new(ClC, "source-denied", new(new(RepositoryC, "controlled-root-c", "src/Restricted.cs", "controlled-denied-digest", "controlled-prepared-v1"), 3, 7), null, "controlled-denied-digest")], []);
    public QualifiedClaimGroup AdvisoryClaim => new("advisory-motive", "Advisory context suggests duplicate prevention; duration is not source-proven.", ClaimQualification.Advisory, [new(null, null, null, Id(7), "controlled-advisory-digest")], ["AdvisoryNotSourceProof"]);
    public QualifiedClaimGroup HypothesisClaim => new("hypothesis", "The provider may be unavailable.", ClaimQualification.Hypothesis, [], ["Not established by source evidence"]);
    public ImmutableArray<QualifiedClaimGroup> Claims => [SourceClaim, AdvisoryClaim, HypothesisClaim];
    public Uncertainty Unknown => new("controlled-u1", "The external destination is unresolved.", [SourceEvidence], ["Binding proof missing"]);
    public AcceptedRelationship Relationship => new(Id(30), RelationshipKind.References, ClA, ClB, "calls the bound provider operation", [SourceEvidence, ProviderClaim.Evidence[0]], []);
    public CompletedFileProposal MultiUnit => new(Binding, Id(40), Id(41), Origin, "controlled-proposal-two-units",
        [new("operation:order", null, "Order", "{\"fixture\":true}", [new(Origin, 10, 20)], [new("overview", 0, "Overview", Claims)]), new("operation:order/helper", "operation:order", "Helper", "{\"fixture\":true}", [new(Origin, 12, 14)], [new("overview", 0, "Overview", [SourceClaim])])],
        [new("call-provider", "operation:order", "caller", "controlled-static-binding", "provide", "controlled-orders", ["binding-id"], ClaimQualification.SourceSupported, [SourceEvidence])], [Unknown], [SourceA, Advisory], new(Id(40), Origin, CoverageDisposition.Accepted, null));
    public CompletedFileProposal ZeroUnit => MultiUnit with { AdmissionId = Id(42), AttemptId = Id(43), CanonicalProposalDigest = "controlled-zero", Units = [], Observations = [], Uncertainties = [], Coverage = new(Id(42), Origin with { RelativePath = "src/empty.txt" }, CoverageDisposition.CompletedZeroUnit, "Complete file has no semantic units"), Origin = Origin with { RelativePath = "src/empty.txt" } };
    public CompletedFileProposal ProviderFile => MultiUnit with { AdmissionId = Id(45), AttemptId = Id(46), Origin = ProviderOrigin, CanonicalProposalDigest = "controlled-provider-proposal", Units = [new("operation:provide", null, "Provider", "{}", [new(ProviderOrigin, 5, 9)], [new("overview", 0, "Overview", [ProviderClaim])])], Observations = [], Uncertainties = [], Contributors = [SourceB], Coverage = new(Id(45), ProviderOrigin, CoverageDisposition.Accepted, null) };
    public PublicationRequest EarlyPublication => new(new(Binding.BatchId, OperationKind.PublishSelection, Id(50)), new(Binding, Id(51), "controlled-corpus"), "controlled-early", [ClA, ClAHelper, ClB], [], [], [], [MultiUnit.Coverage, ProviderFile.Coverage, ZeroUnit.Coverage, new(Id(44), Origin with { RelativePath = "src/unprocessed.cs" }, CoverageDisposition.Unprocessed, "Graceful stop before dispatch")]);
    public ImmutableArray<FlowPublication> Flows => [new(Id(60), Id(61), BlA, [ClA, ClB], [Relationship.RelationshipVersionId], [Unknown.Key], [SourceA, SourceB, Advisory], new("订单处理", "{}", [new("overview", 0, "概要", Claims)])), new(Id(62), Id(63), BlA2, [ClB], [], [], [SourceB], new("独立业务流程", "{}", [new("overview", 0, "概要", [HypothesisClaim])]))];
    public ReadSelectionRequest Selection(KnowledgeMode mode = KnowledgeMode.IT, Guid? publication = null) => new(new(Binding, publication ?? PublicationId), new("ordinary-user", "controlled-stamp"), mode, KnowledgePurpose.Reader, [RepositoryA, RepositoryB]);
    public ImmutableArray<ControlledDocument> Documents => [new(ClA, DocumentKind.CodeLogic, "技术代码 CL-A src/Order.cs", [SourceA, Advisory], Claims, "中文😀第一段", "中文😀第二段"), new(ClAHelper, DocumentKind.CodeLogic, "CL-A helper", [SourceA], [SourceClaim], "helper", ""), new(ClB, DocumentKind.CodeLogic, "CL-B provider", [SourceB], [ProviderClaim], "provider", ""), new(ClC, DocumentKind.CodeLogic, "CL-C secret", [SourceC], [DeniedClaim], "secret", ""), new(BlA, DocumentKind.BusinessLogic, "订单处理", [SourceA, SourceB, Advisory], Claims, "订单处理😀第一段", "订单处理😀第二段"), new(BlHidden, DocumentKind.BusinessLogic, "隐藏业务标题", [SourceA, SourceC], [SourceClaim], "public citation only A, but input C", ""), new(BlA2, DocumentKind.BusinessLogic, "独立业务流程", [SourceB], [HypothesisClaim], "second root", "")];
}

internal sealed record ControlledDocument(DocumentVersionReference Reference, DocumentKind Kind, string Title, ImmutableArray<Contributor> Contributors, ImmutableArray<QualifiedClaimGroup> Claims, string FirstChunk, string SecondChunk);

/// <summary>Modelled fresh authority: requested scope plus mutable live contributor/account outcomes. No Identity integration proof.</summary>
internal sealed class ControlledDisclosurePolicy : IOctoberKnowledgeDisclosurePolicy
{
    public DisclosureStatus LiveStatus { get; set; } = DisclosureStatus.Allowed;
    public HashSet<Guid> RevokedRepositories { get; } = [];
    public int CheckCount { get; private set; }
    public Task<DisclosureStatus> CheckAsync(DisclosureRequest request, CancellationToken cancellationToken = default)
    {
        CheckCount++;
        var allowed = LiveStatus == DisclosureStatus.Allowed && request.AllContributors.All(c => c.RepositoryId is null || (request.Selection.RepositoryScope.Contains(c.RepositoryId.Value) && !RevokedRepositories.Contains(c.RepositoryId.Value))) && (request.Selection.Mode != KnowledgeMode.Business || request.Kind != DocumentKind.CodeLogic);
        return Task.FromResult(allowed ? DisclosureStatus.Allowed : LiveStatus == DisclosureStatus.AuthorityUnavailable ? LiveStatus : DisclosureStatus.Denied);
    }
}

/// <summary>Scripted in-memory consumer model. Tokens are dictionary keys, not cryptographic/runtime cursor protection.</summary>
internal sealed class ControlledKnowledgeReader(OctoberKnowledgeFixture fixture, IOctoberKnowledgeDisclosurePolicy policy) : IOctoberKnowledgeReader
{
    private readonly Dictionary<string, ReadSelectionRequest> _selections = [];
    private readonly Dictionary<string, (string Selection, DocumentHandle Handle)> _chunks = [];
    private readonly Dictionary<string, (string Selection, string Query, int PageSize, int Offset)> _pages = [];
    public Action? BeforeDisclosure { get; set; }
    private int _tokenSequence;
    private string Token() => $"controlled-opaque-{++_tokenSequence:0000}-x";
    public async Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Publication.Knowledge != fixture.Binding || (request.Publication.PublicationId != fixture.PublicationId && request.Publication.PublicationId != fixture.EarlyPublicationId) || await policy.CheckAsync(new(request, [], null), cancellationToken) != DisclosureStatus.Allowed)
            return new(KnowledgeReadStatus.Unavailable, null);
        var token = Token();
        _selections.Add(token, request);
        return new(KnowledgeReadStatus.Success, new(token));
    }
    private IEnumerable<ControlledDocument> Published(ReadSelectionRequest selection) => selection.Publication.PublicationId == fixture.EarlyPublicationId ? fixture.Documents.Where(d => d.Reference == fixture.ClA || d.Reference == fixture.ClAHelper || d.Reference == fixture.ClB) : fixture.Documents;
    private DocumentSummary Summary(ControlledDocument document, ReadSelectionRequest selection, string? section = null)
    {
        var claims = selection.Mode == KnowledgeMode.Business ? document.Claims.Select(c => c with { Evidence = [] }).ToImmutableArray() : document.Claims;
        return new(fixture.Handle(document.Reference, selection.Publication.PublicationId, section), document.Kind, document.Title, claims);
    }
    private static CoverageProjection Coverage => new(true, ["ControlledFixture: one selected file was unprocessed"]);
    public async Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (!_selections.TryGetValue(request.Selection.Token, out var selection)) return new(KnowledgeReadStatus.Unavailable, [], null, null);
        var offset = 0;
        if (request.Continuation is not null)
        {
            if (!_pages.TryGetValue(request.Continuation.Value, out var cursor) || cursor.Selection != request.Selection.Token || cursor.Query != request.Query || cursor.PageSize != request.PageSize) return new(KnowledgeReadStatus.InvalidContinuation, [], null, null);
            offset = cursor.Offset;
        }
        var allowed = new List<ControlledDocument>();
        foreach (var document in Published(selection))
            if (document.Title.Contains(request.Query, StringComparison.OrdinalIgnoreCase) && await policy.CheckAsync(new(selection, document.Contributors, document.Kind), cancellationToken) == DisclosureStatus.Allowed) allowed.Add(document);
        var selected = allowed.OrderBy(d => d.Reference.LogicalDocumentId).Skip(offset).Take(request.PageSize).ToArray();
        BeforeDisclosure?.Invoke();
        foreach (var document in selected)
            if (await policy.CheckAsync(new(selection, document.Contributors, document.Kind), cancellationToken) != DisclosureStatus.Allowed) return new(KnowledgeReadStatus.Unavailable, [], null, null);
        ContinuationToken? next = null;
        if (allowed.Count > offset + selected.Length)
        {
            var token = Token();
            _pages.Add(token, (request.Selection.Token, request.Query, request.PageSize, offset + selected.Length));
            next = new(token);
        }
        return new(KnowledgeReadStatus.Success, selected.Select(d => Summary(d, selection)).ToImmutableArray(), next, Coverage);
    }
    public async Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken = default)
    {
        if (!_selections.TryGetValue(request.Selection.Token, out var selection)) return Unavailable();
        var document = Published(selection).SingleOrDefault(d => fixture.Handle(d.Reference, selection.Publication.PublicationId, request.Document.SectionId) == request.Document);
        if (document is null || (request.Document.SectionId is not null && request.Document.SectionId != "overview") || await policy.CheckAsync(new(selection, document.Contributors, document.Kind), cancellationToken) != DisclosureStatus.Allowed) return Unavailable();
        var content = document.FirstChunk;
        ContinuationToken? next = null;
        if (request.Continuation is not null)
        {
            if (!_chunks.TryGetValue(request.Continuation.Value, out var cursor) || cursor.Selection != request.Selection.Token || cursor.Handle != request.Document) return new(KnowledgeReadStatus.InvalidContinuation, null, null, [], null, null);
            content = document.SecondChunk;
        }
        else if (document.SecondChunk.Length > 0)
        {
            var token = Token();
            _chunks.Add(token, (request.Selection.Token, request.Document));
            next = new(token);
        }
        BeforeDisclosure?.Invoke();
        if (await policy.CheckAsync(new(selection, document.Contributors, document.Kind), cancellationToken) != DisclosureStatus.Allowed) return Unavailable();
        var summary = Summary(document, selection, request.Document.SectionId);
        return new(KnowledgeReadStatus.Success, summary, content, summary.QualifiedExcerpt, next, Coverage);
    }
    private static ContentPage Unavailable() => new(KnowledgeReadStatus.Unavailable, null, null, [], null, null);
    public async Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken = default)
    {
        var content = await ReadExactAsync(new(request.Selection, request.Document), cancellationToken);
        if (content.Status != KnowledgeReadStatus.Success) return new(KnowledgeReadStatus.Unavailable, [], [], null, null);
        var selection = _selections[request.Selection.Token];
        if (selection.Mode == KnowledgeMode.Business || selection.Publication.PublicationId == fixture.EarlyPublicationId) return new(KnowledgeReadStatus.Success, [], [], null, Coverage);
        var relationship = fixture.Relationship;
        if (request.Document.DocumentVersionId != relationship.From.DocumentVersionId) return new(KnowledgeReadStatus.Success, [], [], null, Coverage);
        var target = await ReadExactAsync(new(request.Selection, fixture.Handle(relationship.To)), cancellationToken);
        return target.Status == KnowledgeReadStatus.Success ? new(KnowledgeReadStatus.Success, [new(relationship.Kind, fixture.Handle(relationship.From), fixture.Handle(relationship.To), relationship.Meaning)], [fixture.Unknown], null, Coverage) : new(KnowledgeReadStatus.Unavailable, [], [], null, null);
    }
}

/// <summary>ControlledFixture issuer; production must trust only Scan's actual non-model issuer.</summary>
internal sealed class ControlledCompletedProof(CompletedFileProposal proposal) : CompletedFileProof(proposal.AdmissionId, proposal.AttemptId, proposal.CanonicalProposalDigest);

/// <summary>Manually advanced monotonic clock/fence model, with no database or timer qualification.</summary>
internal sealed class ControlledCommitGuard : IOctoberCommitSubmissionGuard
{
    public int Clock { get; set; }
    public int Deadline { get; set; } = 10;
    public bool Fenced { get; set; }
    public bool Eligible => !Fenced && Clock < Deadline;
    public int Submissions { get; private set; }
    public Func<DisclosureStatus> CurrentAuthority { get; set; } = () => DisclosureStatus.Allowed;
    public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt, CancellationToken cancellationToken = default) => Task.FromResult(!Eligible ? CommitEligibilityStatus.StaleOrFenced : CurrentAuthority() switch { DisclosureStatus.Allowed => CommitEligibilityStatus.Eligible, DisclosureStatus.AuthorityUnavailable => CommitEligibilityStatus.AuthorityUnavailable, _ => CommitEligibilityStatus.Denied });
    public Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default)
    {
        if (!Eligible) return Task.FromResult(new CommitGuardResult(CommitSubmissionStatus.StaleOrFenced, null));
        var authority = CurrentAuthority();
        if (authority != DisclosureStatus.Allowed) return Task.FromResult(new CommitGuardResult(authority == DisclosureStatus.AuthorityUnavailable ? CommitSubmissionStatus.AuthorityUnavailable : CommitSubmissionStatus.Denied, null));
        // Synchronous model transition is the single serialized fence/submission boundary.
        Submissions++;
        return SettleAsync(submitAsync(cancellationToken).Settlement);
    }
    private static async Task<CommitGuardResult> SettleAsync(Task<CommitSettlement> outcome) => new(CommitSubmissionStatus.Submitted, await outcome);
}

/// <summary>Minimal scripted F1/F5 port consumer. This is not a transactional persistence implementation.</summary>
internal sealed class ControlledKnowledgeWriter(OctoberKnowledgeFixture fixture, ControlledCommitGuard guard) : IOctoberKnowledgeWriter, IOctoberFileAcceptanceAuthority
{
    private readonly Dictionary<OperationKey, KnowledgeWriteResult> _committed = [];
    private readonly HashSet<OperationKey> _pending = [];
    private readonly Dictionary<OperationKey, string> _digests = [];
    public Action? AfterStaging { get; set; }
    public Func<Task<CommitSettlement>> SettleSubmission { get; set; } = () => Task.FromResult(CommitSettlement.Committed);
    public int TransactionsStarted { get; private set; }
    public int AcceptedRows { get; private set; }
    public int StagedRollbacks { get; private set; }
    public int ProviderCalls => 0;
    public DisclosureStatus Authority { get; set; } = DisclosureStatus.Allowed;
    public Task<DisclosureStatus> VerifyAsync(CompletedFileSubmission submission, CancellationToken cancellationToken = default) => Task.FromResult(submission.Proof is ControlledCompletedProof && submission.Proof.AdmissionId == submission.Proposal.AdmissionId && submission.Proof.AttemptId == submission.Proposal.AttemptId && submission.Proof.ProposalDigest == submission.Proposal.CanonicalProposalDigest ? Authority : DisclosureStatus.Denied);
    public async Task<KnowledgeWriteResult> AcceptCompletedFileAsync(CompletedFileSubmission submission, CancellationToken cancellationToken = default)
    {
        if (_digests.TryGetValue(submission.Operation, out var digest))
        {
            if (digest != submission.Proposal.CanonicalProposalDigest) return new(KnowledgeWriteStatus.Conflict, submission.Operation);
            var existing = await LookupOperationAsync(submission.Proposal.Binding, submission.Operation, digest, cancellationToken);
            return existing.Status == KnowledgeWriteStatus.Committed ? existing with { Status = KnowledgeWriteStatus.AlreadyCommitted } : existing;
        }
        var authority = await VerifyAsync(submission, cancellationToken);
        if (authority != DisclosureStatus.Allowed) return new(authority == DisclosureStatus.AuthorityUnavailable ? KnowledgeWriteStatus.AuthorityUnavailable : KnowledgeWriteStatus.Denied, submission.Operation);
        guard.CurrentAuthority = () => Authority;
        if (await guard.CheckBeforeTransactionAsync(new(submission.Operation, submission.Proposal.AttemptId, submission.Proposal.CanonicalProposalDigest), cancellationToken) != CommitEligibilityStatus.Eligible) return new(KnowledgeWriteStatus.StaleOrFenced, submission.Operation);
        TransactionsStarted++;
        AfterStaging?.Invoke();
        var result = await guard.SubmitAsync(new(submission.Operation, submission.Proposal.AttemptId, submission.Proposal.CanonicalProposalDigest), _ => { _pending.Add(submission.Operation); _digests[submission.Operation] = submission.Proposal.CanonicalProposalDigest; return new SubmittedCommit(SettleSubmission()); }, cancellationToken);
        if (result.Status != CommitSubmissionStatus.Submitted)
        {
            StagedRollbacks++;
            var status = result.Status switch { CommitSubmissionStatus.Denied => KnowledgeWriteStatus.Denied, CommitSubmissionStatus.AuthorityUnavailable => KnowledgeWriteStatus.AuthorityUnavailable, _ => KnowledgeWriteStatus.StaleOrFenced };
            return new(status, submission.Operation);
        }
        if (result.Settlement == CommitSettlement.Indeterminate) return new(KnowledgeWriteStatus.CommitIndeterminate, submission.Operation);
        _pending.Remove(submission.Operation);
        if (result.Settlement != CommitSettlement.Committed) return new(KnowledgeWriteStatus.CancelledBeforeCommit, submission.Operation);
        AcceptedRows++;
        var accepted = new AcceptedFile(OctoberKnowledgeFixture.Id(70), submission.Proposal.AdmissionId, submission.Proposal.CanonicalProposalDigest, submission.Proposal.Units.Length == 0 ? [] : submission.Proposal.Origin.RepositoryId == fixture.RepositoryB ? [fixture.ClB] : [fixture.ClA, fixture.ClAHelper], submission.Proposal.Coverage);
        var committed = new KnowledgeWriteResult(KnowledgeWriteStatus.Committed, submission.Operation, accepted);
        _committed.Add(submission.Operation, committed);
        return Authority == DisclosureStatus.Allowed ? committed : new(KnowledgeWriteStatus.Denied, submission.Operation);
    }
    public Task<KnowledgeWriteResult> LookupOperationAsync(KnowledgeBinding binding, OperationKey operation, string payloadDigest, CancellationToken cancellationToken = default)
    {
        if (Authority != DisclosureStatus.Allowed) return Task.FromResult(new KnowledgeWriteResult(KnowledgeWriteStatus.Denied, operation));
        if (binding != fixture.Binding || (_digests.TryGetValue(operation, out var digest) && digest != payloadDigest)) return Task.FromResult(new KnowledgeWriteResult(KnowledgeWriteStatus.Conflict, operation));
        return Task.FromResult(_committed.TryGetValue(operation, out var result) ? result : new KnowledgeWriteResult(_pending.Contains(operation) ? KnowledgeWriteStatus.CommitIndeterminate : KnowledgeWriteStatus.CancelledBeforeCommit, operation));
    }
    public Task<KnowledgeWriteResult> FreezeCorpusAsync(CorpusFreezeRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new KnowledgeWriteResult(_pending.Count > 0 ? KnowledgeWriteStatus.CommitIndeterminate : KnowledgeWriteStatus.Committed, request.Operation, Corpus: _pending.Count > 0 ? null : new(fixture.Binding, OctoberKnowledgeFixture.Id(51), request.CanonicalPayloadDigest)));
    public Task<KnowledgeWriteResult> PublishSelectionAsync(PublicationRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new KnowledgeWriteResult(request.Documents.IsEmpty ? KnowledgeWriteStatus.InvalidProposal : KnowledgeWriteStatus.Committed, request.Operation, Publication: request.Documents.IsEmpty ? null : new(fixture.Binding, fixture.EarlyPublicationId)));
}

/// <summary>Controlled host cap=1 queue/accounting model. No real scheduler, rate limiter or provider transport.</summary>
internal sealed class ControlledExecutionScope : IOctoberExecutionScope, IOctoberBusinessHandoffLifetime
{
    private readonly Queue<(ProviderAttemptRequest Request, TaskCompletionSource<ProviderAdmission> Completion)> _queue = [];
    private ControlledLease? _active;
    private bool _hardStopped;
    private readonly CancellationTokenSource _stop = new();
    private readonly CancellationToken _stopToken;
    private bool _deadlineExpired;
    public ControlledExecutionScope() => _stopToken = _stop.Token;
    public CancellationToken HandoffCancellation => _stopToken;
    // Explicit protocol model only: real Scan adapters independently bind run/operation/profile identities.
    public ProviderAdmissionStatus CheckHandoffEligibility(Guid runId, OperationKey finalizationOperation, string providerProfileId) =>
        _hardStopped ? ProviderAdmissionStatus.HardStopped : DeadlineExpired ? ProviderAdmissionStatus.DeadlineExceeded : ProviderAdmissionStatus.Granted;
    public bool RateAllowed { get; set; } = true;
    public bool RateDenied { get; set; }
    public bool DeadlineExpired
    {
        get => _deadlineExpired;
        set { if (value) { _deadlineExpired = true; _stop.Cancel(); } }
    }
    public long RemainingTokens { get; private set; } = 100;
    public int RemainingRequests { get; private set; } = 4;
    public int Grants { get; private set; }
    public int Releases { get; private set; }
    public int Settlements { get; private set; }
    public int Sends { get; private set; }
    public List<ProviderAttemptUsage> Usage { get; } = [];
    public Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<ProviderAdmission>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue((request, completion));
        Pump();
        return completion.Task;
    }
    public void Pump()
    {
        while (_queue.TryPeek(out var queued))
        {
            ProviderAdmissionStatus? denied = _hardStopped ? ProviderAdmissionStatus.HardStopped : DeadlineExpired ? ProviderAdmissionStatus.DeadlineExceeded : RateDenied ? ProviderAdmissionStatus.Denied : RemainingRequests <= 0 || queued.Request.MaximumTokens > RemainingTokens ? ProviderAdmissionStatus.BudgetExhausted : null;
            if (denied is not null) { _queue.Dequeue(); queued.Completion.SetResult(new(denied.Value, null)); continue; }
            if (_active is not null || !RateAllowed) return;
            _queue.Dequeue();
            RemainingRequests--;
            RemainingTokens -= queued.Request.MaximumTokens;
            _active = new(this, queued.Request.AttemptId, queued.Request.MaximumTokens);
            Grants++;
            queued.Completion.SetResult(new(ProviderAdmissionStatus.Granted, _active));
        }
    }
    public void HardStop() { _hardStopped = true; _stop.Cancel(); Pump(); }
    public sealed class ControlledLease(ControlledExecutionScope owner, Guid attemptId, long reservation) : IOctoberProviderAttemptLease
    {
        private bool _settled;
        private bool _released;
        public Guid AttemptId => attemptId;
        public CancellationToken CancellationToken => owner._stopToken;
        public Task<ProviderAdmissionStatus> CheckBeforeSendAsync(CancellationToken cancellationToken = default)
        {
            var status = owner._hardStopped ? ProviderAdmissionStatus.HardStopped : owner.DeadlineExpired ? ProviderAdmissionStatus.DeadlineExceeded : owner.RateDenied || !owner.RateAllowed ? ProviderAdmissionStatus.Denied : ProviderAdmissionStatus.Granted;
            if (status == ProviderAdmissionStatus.Granted) owner.Sends++;
            return Task.FromResult(status);
        }
        public Task SettleAsync(ProviderAttemptUsage usage, CancellationToken cancellationToken = default)
        {
            if (_settled) return Task.CompletedTask;
            _settled = true;
            owner.Settlements++;
            owner.Usage.Add(usage);
            if (usage.InputTokens is not null && usage.OutputTokens is not null) owner.RemainingTokens += reservation - usage.InputTokens.Value - usage.OutputTokens.Value;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            // An unsettled transport cannot release capacity, even after cancellation.
            if (_settled && !_released) { _released = true; owner.Releases++; owner._active = null; owner.Pump(); }
            return ValueTask.CompletedTask;
        }
    }
}




internal sealed class UntrustedControlledProof(CompletedFileProposal proposal) : CompletedFileProof(proposal.AdmissionId, proposal.AttemptId, proposal.CanonicalProposalDigest);

/// <summary>Fixed finalizer response consumer probe, not an inquiry/synthesis implementation.</summary>
internal sealed class ControlledFinalizer(BusinessFinalizationResult result) : IOctoberBusinessFinalizer
{
    public Task<BusinessFinalizationResult> FinalizeAsync(FinalizationRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(request.ExecutionScope is null ? result with { Status = BusinessFinalizationStatus.Failed, Publication = null } : result);
}
