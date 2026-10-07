using System.Collections.Immutable;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Atomic, producer-only October file acceptance. Publication/read behavior belongs to B0b.</summary>
public sealed class OctoberKnowledgeWriter : IOctoberKnowledgeWriter
{
    private readonly IDbContextFactory<OctoberDbContext> _contexts;
    private readonly IOctoberFileAcceptanceAuthority _authority;
    private readonly IOctoberKnowledgeSourceVerifier _sourceVerifier;
    private readonly IOctoberKnowledgeProducerAuthorization _authorization;
    private readonly IOctoberCommitSubmissionGuard _guard;
    private readonly IOctoberKnowledgeOperationLog _log;
    private readonly OctoberKnowledgeOperationStore _operations;
    private readonly OctoberCorpusSelectionStore _corpora;
    private readonly OctoberKnowledgePublicationStore _publications;

    public OctoberKnowledgeWriter(IDbContextFactory<OctoberDbContext> contexts, IOctoberFileAcceptanceAuthority authority,
        IOctoberKnowledgeSourceVerifier sourceVerifier, IOctoberKnowledgeProducerAuthorization authorization,
        IOctoberCommitSubmissionGuard guard, IOctoberKnowledgeOperationLog log, IOctoberCorpusFreezeAuthority freezeAuthority)
    {
        _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _sourceVerifier = sourceVerifier ?? throw new ArgumentNullException(nameof(sourceVerifier));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        ArgumentNullException.ThrowIfNull(freezeAuthority);
        _operations = new(contexts);
        _corpora = new(contexts, authorization, guard, log, freezeAuthority);
        _publications = new(contexts, authorization, guard, log);
    }

    public async Task<KnowledgeWriteResult> AcceptCompletedFileAsync(CompletedFileSubmission submission, CancellationToken cancellationToken = default)
    {
        var operation = submission.Operation;
        var proposal = submission.Proposal;
        if (operation.Kind != OperationKind.AcceptFile || operation.BatchId != proposal.Binding.BatchId || operation.OperationId == Guid.Empty)
            return new(KnowledgeWriteStatus.InvalidProposal, operation);
        bool commitMayHaveBeenSubmitted = false;
        Task<CommitSettlement>? actualSettlement = null;
        try
        {
            // Reconciliation precedes capability validation; a durable result outlives the completion issuer.
            await using (var initial = await _contexts.CreateDbContextAsync(cancellationToken))
            {
                if (!await BindingMatchesAsync(initial, proposal.Binding, cancellationToken))
                    return new(KnowledgeWriteStatus.Denied, operation);
            }
            var previous = await _operations.FindAsync(operation, cancellationToken);
            if (previous is not null)
                return await DiscloseAsync(proposal.Binding, operation, proposal.CanonicalProposalDigest, previous, cancellationToken, proposal: proposal);

            var authority = await VerifyCompletionAsync(submission, cancellationToken);
            if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
            if (submission.Proof.AdmissionId != proposal.AdmissionId || submission.Proof.AttemptId != proposal.AttemptId ||
                submission.Proof.ProposalDigest != proposal.CanonicalProposalDigest)
                return new(KnowledgeWriteStatus.NotCompleted, operation);
            var origins = OctoberKnowledgePolicy.ContentOrigins(proposal);
            var verified = ImmutableArray.CreateBuilder<VerifiedKnowledgeSource>();
            try
            {
                foreach (var origin in origins)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var source = await _sourceVerifier.VerifyAsync(proposal.Binding, origin, cancellationToken).WaitAsync(cancellationToken);
                    if (source is null || source.LineCount < 0 ||
                        (source.Origin with { RelativePath = OctoberKnowledgePolicy.NormalizePath(source.Origin.RelativePath) }) != origin)
                        return new(KnowledgeWriteStatus.InvalidProposal, operation);
                    verified.Add(source);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { return new(KnowledgeWriteStatus.AuthorityUnavailable, operation); }
            if (!OctoberKnowledgePolicy.ValidateInputs(proposal, verified.ToImmutable()) ||
                OctoberKnowledgePolicy.ComputeProposalDigest(proposal) != proposal.CanonicalProposalDigest)
                return new(KnowledgeWriteStatus.InvalidProposal, operation);
            // Normalize only after checking the original issuer-authenticated ledger and complete digest.
            proposal = proposal with { Contributors = OctoberKnowledgePolicy.NormalizeContributors(proposal) };
            submission = submission with { Proposal = proposal };
            var current = await AuthorizeAsync(proposal.Binding, proposal.Contributors, cancellationToken);
            if (current != DisclosureStatus.Allowed) return new(AuthorityStatus(current), operation);
            var attempt = new CommitAttempt(operation, proposal.AttemptId, proposal.CanonicalProposalDigest);

            await using (var context = await _contexts.CreateDbContextAsync(cancellationToken))
            {
                var eligibility = await _guard.CheckBeforeTransactionAsync(attempt, cancellationToken);
                if (eligibility != CommitEligibilityStatus.Eligible) return new(EligibilityStatus(eligibility), operation);
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                // One atomic SaveChanges; any failure rolls the whole transaction back. Avoid
                // subtransaction xids because the database seals child membership to its creating transaction.
                context.Database.AutoSavepointsEnabled = false;
                // The batch lock serializes setup retargeting against its first accepted operation.
                // Database setup mutation triggers take the conflicting lock before testing immutability.
                if (context.Database.IsNpgsql())
                    await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM october.\"OctoberBatches\" WHERE \"BatchId\" = {operation.BatchId} FOR SHARE", cancellationToken);
                if (!await BindingMatchesAsync(context, proposal.Binding, cancellationToken) ||
                    !await SourceSetupMatchesAsync(context, proposal, cancellationToken))
                    return new(KnowledgeWriteStatus.Denied, operation);
                if (await context.CorpusSelections.AnyAsync(x => x.BatchId == operation.BatchId, cancellationToken))
                    return new(KnowledgeWriteStatus.StaleOrFenced, operation);
                if (await context.AcceptedFileManifests.AnyAsync(x => x.BatchId == operation.BatchId && x.AdmissionId == proposal.AdmissionId, cancellationToken))
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return await ReconcileCollisionAsync(proposal.Binding, operation, proposal.CanonicalProposalDigest, proposal, cancellationToken);
                }
                Stage(context, submission);
                await context.SaveChangesAsync(cancellationToken);
                current = await AuthorizeAsync(proposal.Binding, proposal.Contributors, cancellationToken);
                if (current != DisclosureStatus.Allowed) return new(AuthorityStatus(current), operation);
                var guarded = await _guard.SubmitAsync(attempt, token =>
                {
                    // Invoke the PROVIDER commit before returning. EF's CommitAsync interceptor pipeline
                    // can await before physical submission and therefore cannot serve this boundary.
                    commitMayHaveBeenSubmitted = true;
                    Task providerCommit = transaction.GetDbTransaction().CommitAsync(token);
                    actualSettlement = SettleAsync(providerCommit);
                    return new SubmittedCommit(actualSettlement);
                }, cancellationToken);
                if (!commitMayHaveBeenSubmitted)
                    return new(SubmissionStatus(guarded.Status), operation);
                if (actualSettlement is not null) await actualSettlement;
                // Never treat cancellation, an absent row, or a guard's lost acknowledgement as rollback.
                // A fresh connection resolves the transaction's durable result below.
            }
            return await LookupBoundedAsync(proposal.Binding, operation, proposal.CanonicalProposalDigest, CancellationToken.None, actualSettlement?.Result == CommitSettlement.Committed);
        }
        catch (OperationCanceledException)
        {
            if (!commitMayHaveBeenSubmitted) return new(KnowledgeWriteStatus.CancelledBeforeCommit, operation);
        }
        catch (DbUpdateException exception) when (!commitMayHaveBeenSubmitted)
        {
            if (IsUniqueOrContention(exception))
                return await ReconcileCollisionAsync(proposal.Binding, operation, proposal.CanonicalProposalDigest, proposal, CancellationToken.None);
            return new(KnowledgeWriteStatus.StorageUnavailable, operation);
        }
        catch (Exception error) when (error is ArgumentException or System.Text.Json.JsonException)
        {
            return new(KnowledgeWriteStatus.InvalidProposal, operation);
        }
        catch (Exception)
        {
            if (!commitMayHaveBeenSubmitted) return new(KnowledgeWriteStatus.StorageUnavailable, operation);
        }
        if (actualSettlement is not null) await actualSettlement;
        return await LookupOperationAsync(proposal.Binding, operation, proposal.CanonicalProposalDigest, CancellationToken.None);
    }

    public Task<KnowledgeWriteResult> LookupOperationAsync(KnowledgeBinding binding, OperationKey operation, string payloadDigest, CancellationToken cancellationToken = default) =>
        LookupBoundedAsync(binding, operation, payloadDigest, cancellationToken);

    private async Task<KnowledgeWriteResult> LookupBoundedAsync(KnowledgeBinding binding, OperationKey operation, string payloadDigest,
        CancellationToken callerCancellation, bool knownNewCommit = false)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(callerCancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var cancellationToken = deadline.Token;
        try
        {
            // BeginGenerate has a distinct live operator/owner recovery boundary, never generic Knowledge lookup.
            if (operation.Kind == OperationKind.BeginGenerate || operation.BatchId != binding.BatchId)
                return new(KnowledgeWriteStatus.Denied, operation);
            await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
            if (!await BindingMatchesAsync(context, binding, cancellationToken)) return new(KnowledgeWriteStatus.Denied, operation);
            var previous = await _operations.FindAsync(operation, cancellationToken);
            if (previous is not null) return await DiscloseAsync(binding, operation, payloadDigest, previous, cancellationToken, knownNewCommit);
            var authority = await AuthorizeAsync(binding, [], cancellationToken);
            return new(authority == DisclosureStatus.Allowed ? KnowledgeWriteStatus.CommitIndeterminate : AuthorityStatus(authority), operation);
        }
        catch (Exception)
        {
            return new(KnowledgeWriteStatus.CommitIndeterminate, operation);
        }
    }

    public Task<KnowledgeWriteResult> FreezeCorpusAsync(CorpusFreezeRequest request, CancellationToken cancellationToken = default) =>
        _corpora.FreezeAsync(request, cancellationToken);
    public Task<KnowledgeWriteResult> PublishSelectionAsync(PublicationRequest request, CancellationToken cancellationToken = default) =>
        _publications.PublishAsync(request, cancellationToken);

    private async Task<KnowledgeWriteResult> DiscloseAsync(KnowledgeBinding binding, OperationKey operation, string digest,
        KnowledgeOperation stored, CancellationToken cancellationToken, bool knownNewCommit = false, CompletedFileProposal? proposal = null)
    {
        var contributors = OctoberKnowledgeOperationStore.Contributors(stored);
        var authority = await AuthorizeAsync(binding, contributors, cancellationToken);
        if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
        if (proposal is not null && OctoberKnowledgePolicy.ComputeProposalDigest(proposal) != digest) return new(KnowledgeWriteStatus.Conflict, operation);
        if (stored.PayloadDigest != digest) return new(KnowledgeWriteStatus.Conflict, operation);
        var result = OctoberKnowledgeOperationStore.Result(stored) with { Status = knownNewCommit ? KnowledgeWriteStatus.Committed : KnowledgeWriteStatus.AlreadyCommitted };
        bool logged;
        try
        {
            logged = await _log.AppendAsync(new(binding, operation, result.Status), cancellationToken).WaitAsync(cancellationToken);
        }
        catch (Exception)
        {
            logged = false;
        }
        authority = await AuthorizeAsync(binding, contributors, cancellationToken);
        if (authority != DisclosureStatus.Allowed) return new(AuthorityStatus(authority), operation);
        return logged ? result : result with { Status = KnowledgeWriteStatus.LogUnavailable };
    }

    private async Task<KnowledgeWriteResult> ReconcileCollisionAsync(KnowledgeBinding binding, OperationKey operation, string digest,
        CompletedFileProposal proposal, CancellationToken cancellationToken)
    {
        try
        {
            return await ReconcileCollisionCoreAsync(binding, operation, digest, proposal, cancellationToken);
        }
        catch (Exception)
        {
            return new(KnowledgeWriteStatus.CommitIndeterminate, operation);
        }
    }

    private async Task<KnowledgeWriteResult> ReconcileCollisionCoreAsync(KnowledgeBinding binding, OperationKey operation, string digest,
        CompletedFileProposal proposal, CancellationToken callerCancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(callerCancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var cancellationToken = deadline.Token;
        // Bounded lookup only: no repeated transaction or provider generation. A SQLite contention
        // loser can reach here before its winner settles; PostgreSQL uniqueness normally waits for it.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var existing = await _operations.FindAsync(operation, cancellationToken);
            if (existing is not null) return await DiscloseAsync(binding, operation, digest, existing, cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        var manifest = await context.AcceptedFileManifests.AsNoTracking().SingleOrDefaultAsync(x =>
            x.BatchId == binding.BatchId && x.AdmissionId == proposal.AdmissionId, cancellationToken);
        if (manifest is not null)
        {
            var winner = await _operations.FindAsync(new(binding.BatchId, manifest.OperationKind, manifest.OperationId), cancellationToken);
            if (winner is not null)
            {
                var winnerAuthority = await AuthorizeAsync(binding, OctoberKnowledgeOperationStore.Contributors(winner), cancellationToken);
                return new(winnerAuthority == DisclosureStatus.Allowed ? KnowledgeWriteStatus.Conflict : AuthorityStatus(winnerAuthority), operation);
            }
        }
        var authority = await AuthorizeAsync(binding, proposal.Contributors, cancellationToken);
        return new(authority == DisclosureStatus.Allowed ? KnowledgeWriteStatus.CommitIndeterminate : AuthorityStatus(authority), operation);
    }

    private static async Task<bool> BindingMatchesAsync(OctoberDbContext context, KnowledgeBinding binding, CancellationToken cancellationToken) =>
        await context.Batches.AsNoTracking().AnyAsync(x => x.BatchId == binding.BatchId && x.TargetId == binding.TargetId &&
            x.CanonicalDatabaseIdentity == binding.CanonicalDatabaseIdentity && x.SelectionDigest == binding.SelectionDigest, cancellationToken);

    private static async Task<bool> SourceSetupMatchesAsync(OctoberDbContext context, CompletedFileProposal proposal, CancellationToken cancellationToken)
    {
        var repositories = await context.RepositorySelections.AsNoTracking().Where(x => x.BatchId == proposal.Binding.BatchId).ToListAsync(cancellationToken);
        if (!repositories.Any(x => x.RepositoryId == proposal.Origin.RepositoryId && x.RootBindingIdentity == proposal.Origin.RootBindingIdentity)) return false;
        var advisory = await context.ContextReferences.AsNoTracking().Where(x => x.BatchId == proposal.Binding.BatchId).ToListAsync(cancellationToken);
        var snapshots = await context.AdvisoryEntries.AsNoTracking().Where(x => x.BatchId == proposal.Binding.BatchId).ToListAsync(cancellationToken);
        foreach (var contributor in proposal.Contributors)
        {
            if (contributor.RepositoryId is { } repository && !repositories.Any(x => x.RepositoryId == repository &&
                (contributor.SystemId is null || contributor.SystemId == x.SystemId))) return false;
            if (contributor.Kind is ContributorKind.Source or ContributorKind.SourceMetadata)
            {
                bool valid = contributor.Kind == ContributorKind.Source
                    ? OctoberKnowledgeInputPolicy.IsValidSourceContributor(contributor)
                    : OctoberKnowledgeInputPolicy.IsValidMetadataContributor(contributor);
                if (!valid || !repositories.Any(row => row.RepositoryId == contributor.RepositoryId &&
                    row.RootBindingIdentity == contributor.RootBindingIdentity)) return false;
                continue;
            }
            if (contributor.SourceOrigin is not null || contributor.MetadataOrigin is not null) return false;
            if (contributor.Kind == ContributorKind.SystemAdvisory && !repositories.Any(x => x.SystemId == contributor.SystemId)) return false;
            if (contributor.AdvisoryRecordId is { } record)
            {
                int scope = (int)contributor.Kind - 1;
                if (!advisory.Any(x => x.RecordId == record && x.Scope == scope)) return false;
                var selected = snapshots.Where(x => x.OriginRecordId == record && x.Scope == scope && x.Applicability == 0 &&
                    x.EvidenceKind == 0 && x.Value is not null &&
                    (contributor.Kind != ContributorKind.RepositoryAdvisory || x.RepositoryId == contributor.RepositoryId) &&
                    (contributor.Kind != ContributorKind.SystemAdvisory || repositories.Any(r => r.RepositoryId == x.RepositoryId && r.SystemId == contributor.SystemId))).ToArray();
                // Existing setup stores the exact selected text. Hash its UTF-8 bytes without
                // whitespace/newline/Unicode rewriting; permission alone does not prove provenance.
                if (selected.Length == 0 || selected.Any(x => OctoberKnowledgePolicy.Hash(x.Value!) != contributor.ContentDigest)) return false;
            }
        }
        return true;
    }

    private async Task<DisclosureStatus> VerifyCompletionAsync(CompletedFileSubmission submission, CancellationToken cancellationToken)
    {
        try
        {
            return await _authority.VerifyAsync(submission, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return DisclosureStatus.AuthorityUnavailable;
        }
    }
    private async Task<DisclosureStatus> AuthorizeAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken)
    {
        try
        {
            return await _authorization.CheckAsync(binding, contributors, cancellationToken).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return DisclosureStatus.AuthorityUnavailable;
        }
    }
    private static async Task<CommitSettlement> SettleAsync(Task commit)
    {
        try
        {
            await commit; return CommitSettlement.Committed;
        }
        catch (Exception)
        {
            return CommitSettlement.Indeterminate;
        }
    }
    private static bool IsUniqueOrContention(DbUpdateException exception) => exception.InnerException switch
    {
        Npgsql.PostgresException { SqlState: "23505" } => true,
        DbException error when error.GetType().Name == "SqliteException" &&
            error.GetType().GetProperty("SqliteErrorCode")?.GetValue(error) is int code && code is 5 or 6 or 19 =>
                code != 19 || error.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(error) is 1555 or 2067,
        _ => false
    };
    private static KnowledgeWriteStatus AuthorityStatus(DisclosureStatus status) => status == DisclosureStatus.Denied
        ? KnowledgeWriteStatus.Denied : KnowledgeWriteStatus.AuthorityUnavailable;
    private static KnowledgeWriteStatus EligibilityStatus(CommitEligibilityStatus status) => status switch
    {
        CommitEligibilityStatus.Denied => KnowledgeWriteStatus.Denied,
        CommitEligibilityStatus.AuthorityUnavailable => KnowledgeWriteStatus.AuthorityUnavailable,
        CommitEligibilityStatus.Cancelled => KnowledgeWriteStatus.CancelledBeforeCommit,
        _ => KnowledgeWriteStatus.StaleOrFenced
    };
    private static KnowledgeWriteStatus SubmissionStatus(CommitSubmissionStatus status) => status switch
    {
        CommitSubmissionStatus.Denied => KnowledgeWriteStatus.Denied,
        CommitSubmissionStatus.AuthorityUnavailable => KnowledgeWriteStatus.AuthorityUnavailable,
        CommitSubmissionStatus.CancelledBeforeSubmission => KnowledgeWriteStatus.CancelledBeforeCommit,
        _ => KnowledgeWriteStatus.StaleOrFenced
    };

    private static void Stage(OctoberDbContext context, CompletedFileSubmission submission)
    {
        var p = submission.Proposal;
        Guid batch = p.Binding.BatchId;
        Guid manifest = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var documents = ImmutableArray.CreateBuilder<DocumentVersionReference>();
        foreach (var unit in p.Units.OrderBy(x => x.SemanticSelector, StringComparer.Ordinal))
        {
            Guid unitId = OctoberKnowledgePolicy.SemanticUnitId(p, unit.SemanticSelector);
            Guid documentId = OctoberKnowledgePolicy.LogicalDocumentId(unitId);
            Guid version = Guid.NewGuid();
            context.CodeUnits.Add(new()
            {
                BatchId = batch,
                UnitId = unitId,
                ManifestId = manifest,
                RepositoryId = p.Origin.RepositoryId,
                SemanticKey = OctoberKnowledgePolicy.SemanticKey(p, unit.SemanticSelector),
                SemanticSelector = unit.SemanticSelector,
                ParentUnitId = unit.ParentSelector is null ? null : OctoberKnowledgePolicy.SemanticUnitId(p, unit.ParentSelector)
            });
            context.LogicalDocuments.Add(new()
            {
                BatchId = batch,
                LogicalDocumentId = documentId,
                UnitId = unitId,
                Kind = DocumentKind.CodeLogic
            });
            string content = OctoberKnowledgePolicy.CreateContentJson(unit.Title, unit.Sections);
            context.DocumentVersions.Add(new()
            {
                BatchId = batch,
                VersionId = version,
                LogicalDocumentId = documentId,
                ManifestId = manifest,
                SchemaVersion = p.SchemaVersion,
                Title = unit.Title,
                ContentJson = content,
                ContentDigest = OctoberKnowledgePolicy.Hash(content),
                AnchorsJson = OctoberKnowledgePolicy.SerializeCanonical(unit.Anchors),
                CreatedAtUtc = now
            });
            foreach (var section in unit.Sections)
            {
                context.DocumentSections.Add(new()
                {
                    BatchId = batch,
                    VersionId = version,
                    SectionId = section.SectionId,
                    Ordinal = section.Ordinal,
                    Title = section.Title
                });
                foreach (var claim in section.Claims)
                    context.ClaimGroups.Add(new()
                    {
                        BatchId = batch,
                        VersionId = version,
                        SectionId = section.SectionId,
                        ClaimGroupId = claim.ClaimGroupId,
                        Text = claim.Text,
                        Qualification = claim.Qualification,
                        EvidenceJson = OctoberKnowledgePolicy.SerializeCanonical(claim.Evidence),
                        LimitationsJson = OctoberKnowledgePolicy.SerializeCanonical(claim.Limitations)
                    });
            }
            documents.Add(new(batch, documentId, version));
        }
        var result = new KnowledgeWriteResult(KnowledgeWriteStatus.Committed, submission.Operation,
            new(manifest, p.AdmissionId, p.CanonicalProposalDigest, documents.ToImmutable(), p.Coverage));
        context.KnowledgeOperations.Add(new()
        {
            BatchId = batch,
            Kind = submission.Operation.Kind,
            OperationId = submission.Operation.OperationId,
            PayloadDigest = p.CanonicalProposalDigest,
            CommittedResultJson = OctoberKnowledgePolicy.SerializeCanonical(result),
            ContributorsJson = OctoberKnowledgePolicy.SerializeCanonical(p.Contributors),
            CommittedAtUtc = now
        });
        context.AcceptedFileManifests.Add(new()
        {
            BatchId = batch,
            ManifestId = manifest,
            AdmissionId = p.AdmissionId,
            RepositoryId = p.Origin.RepositoryId,
            RootBindingIdentity = p.Origin.RootBindingIdentity,
            RelativePath = OctoberKnowledgePolicy.NormalizePath(p.Origin.RelativePath),
            SourceDigest = p.Origin.SourceDigest,
            PreparedVersion = p.Origin.PreparedVersion,
            ProposalDigest = p.CanonicalProposalDigest,
            CoverageDisposition = p.Coverage.Disposition,
            CoverageReason = p.Coverage.Reason,
            UnitCount = p.Units.Length,
            OperationId = submission.Operation.OperationId
        });
        for (int index = 0; index < p.Contributors.Length; index++)
            context.KnowledgeContributors.Add(new()
            {
                BatchId = batch,
                ManifestId = manifest,
                Ordinal = index,
                ContributorJson = OctoberKnowledgePolicy.SerializeCanonical(p.Contributors[index])
            });
        foreach (var observation in p.Observations)
            context.CommunicationObservations.Add(new()
            {
                BatchId = batch,
                ManifestId = manifest,
                ObservationKey = observation.ObservationKey,
                UnitId = OctoberKnowledgePolicy.SemanticUnitId(p, observation.OwnerSelector),
                ContentJson = OctoberKnowledgePolicy.SerializeCanonical(observation)
            });
        foreach (var uncertainty in p.Uncertainties)
            context.KnowledgeUncertainties.Add(new()
            {
                BatchId = batch,
                ManifestId = manifest,
                Key = uncertainty.Key,
                ContentJson = OctoberKnowledgePolicy.SerializeCanonical(uncertainty)
            });
    }
}
