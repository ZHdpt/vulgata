using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Explicit maintenance-only abandonment. Never reconstructs an admission or deletes history.</summary>
public sealed class OctoberBusinessFromClRecoveryStore(
    IDbContextFactory<OctoberDbContext> contexts,
    IOctoberBusinessFromClRecoveryAuthority authority,
    IOctoberCommitSubmissionGuard guard,
    IOctoberKnowledgeOperationLog log)
{
    public async Task<KnowledgeWriteResult> RecoverAsync(BusinessFromClRecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var operation = OctoberBusinessFromClRecoveryPolicy.Operation(request);
        if (!Valid(request)) return new(KnowledgeWriteStatus.InvalidProposal, operation);
        var authorization = new RecoveryAuthorization(authority, request);
        var committer = new OctoberKnowledgePublicationCommitter(contexts, authorization, guard, log);
        var result = await committer.ExecuteAsync(request.Begin.Publication.Knowledge, operation, operation.Kind,
            request.CanonicalPayloadDigest, () => OctoberBusinessFromClRecoveryPolicy.ComputeDigest(request),
            async ct => await authorization.CheckAsync(request.Begin.Publication.Knowledge, [], ct) == DisclosureStatus.Allowed
                ? null : KnowledgeWriteStatus.AuthorityUnavailable,
            async (context, ct) =>
            {
                // The exclusive batch lock has been acquired. All earlier semantic transactions have settled.
                var begin = await FindAsync(context, OctoberBusinessFromClPolicy.BeginOperation(request.Begin), ct);
                if (begin is null || begin.PayloadDigest != request.Begin.CanonicalPayloadDigest)
                    return new(new(KnowledgeWriteStatus.Conflict, operation), []);
                var original = OctoberKnowledgeOperationStore.Result(begin);
                if (original.Status != KnowledgeWriteStatus.Committed || original.Operation != OctoberBusinessFromClPolicy.BeginOperation(request.Begin) ||
                    original.Publication != request.Begin.Publication || original.AcceptedFile is not null ||
                    original.Corpus?.Binding != request.Begin.Publication.Knowledge || original.Abandonment is not null)
                    return new(new(KnowledgeWriteStatus.Conflict, operation), []);
                if (await FindAsync(context, OctoberBusinessFromClPolicy.CompleteOperation(request.Begin), ct) is not null)
                    return new(new(KnowledgeWriteStatus.Conflict, operation), []);
                return new(new KnowledgeWriteResult(KnowledgeWriteStatus.Committed, operation)
                {
                    Abandonment = new(request.Begin.Publication, begin.PayloadDigest, request.RecoverySubjectId, request.EvidenceDigest)
                    { BeginSubjectId = request.Begin.SubjectId }
                }, []);
            }, cancellationToken, request.Begin.RunId);
        if (result.Status is not (KnowledgeWriteStatus.Committed or KnowledgeWriteStatus.AlreadyCommitted or KnowledgeWriteStatus.LogUnavailable))
            return result;
        var verified = await LookupAsync(request, cancellationToken);
        return verified.Status == KnowledgeWriteStatus.AlreadyCommitted ? verified with { Status = result.Status } : verified;
    }

    public async Task<KnowledgeWriteResult> LookupAsync(BusinessFromClRecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var operation = OctoberBusinessFromClRecoveryPolicy.Operation(request);
        if (!Valid(request)) return new(KnowledgeWriteStatus.InvalidProposal, operation);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var authorization = new RecoveryAuthorization(authority, request);
            if (await authorization.CheckAsync(request.Begin.Publication.Knowledge, [], deadline.Token) != DisclosureStatus.Allowed)
                return new(KnowledgeWriteStatus.AuthorityUnavailable, operation);
            await using var context = await contexts.CreateDbContextAsync(deadline.Token);
            var binding = request.Begin.Publication.Knowledge;
            if (!await context.Batches.AnyAsync(x => x.BatchId == binding.BatchId && x.TargetId == binding.TargetId &&
                x.CanonicalDatabaseIdentity == binding.CanonicalDatabaseIdentity && x.SelectionDigest == binding.SelectionDigest, deadline.Token))
                return new(KnowledgeWriteStatus.Denied, operation);
            var begin = await FindAsync(context, OctoberBusinessFromClPolicy.BeginOperation(request.Begin), deadline.Token);
            var row = await FindAsync(context, operation, deadline.Token);
            if (row is null) return new(KnowledgeWriteStatus.CommitIndeterminate, operation);
            if (row.PayloadDigest != request.CanonicalPayloadDigest || begin?.PayloadDigest != request.Begin.CanonicalPayloadDigest)
                return new(KnowledgeWriteStatus.Conflict, operation);
            if (begin is null || !ValidTerminal(begin, row)) return new(KnowledgeWriteStatus.CommitIndeterminate, operation);
            var result = OctoberKnowledgeOperationStore.Result(row);
            if (result.Abandonment != new BusinessFromClAbandonment(request.Begin.Publication, request.Begin.CanonicalPayloadDigest,
                request.RecoverySubjectId, request.EvidenceDigest) { BeginSubjectId = request.Begin.SubjectId }) return new(KnowledgeWriteStatus.Conflict, operation);
            if (await authorization.CheckAsync(binding, [], deadline.Token) != DisclosureStatus.Allowed)
                return new(KnowledgeWriteStatus.AuthorityUnavailable, operation);
            return result with { Status = KnowledgeWriteStatus.AlreadyCommitted };
        }
        catch (Exception) { return new(KnowledgeWriteStatus.CommitIndeterminate, operation); }
    }

    internal static bool ValidTerminal(KnowledgeOperation begin, KnowledgeOperation? abandoned)
    {
        if (abandoned is null) return false;
        try
        {
            var original = OctoberKnowledgeOperationStore.Result(begin);
            var result = OctoberKnowledgeOperationStore.Result(abandoned);
            return begin.BatchId != Guid.Empty && begin.OperationId != Guid.Empty &&
                begin.Kind == OperationKind.BeginBusinessFromCl && original.Status == KnowledgeWriteStatus.Committed &&
                original.Operation == new OperationKey(begin.BatchId, begin.Kind, begin.OperationId) &&
                original.Publication is { } publication && publication.Knowledge.BatchId == begin.BatchId &&
                publication.PublicationId != Guid.Empty && original.Corpus?.Binding == publication.Knowledge &&
                original.AcceptedFile is null && original.Abandonment is null &&
                abandoned.BatchId == begin.BatchId && abandoned.OperationId == begin.OperationId &&
                abandoned.Kind == OperationKind.AbandonBusinessFromCl && result.Status == KnowledgeWriteStatus.Committed &&
                result.Operation == new OperationKey(begin.BatchId, abandoned.Kind, begin.OperationId) &&
                result.Corpus is null && result.Publication is null && result.AcceptedFile is null &&
                OctoberKnowledgeOperationStore.Contributors(abandoned).IsEmpty && result.Abandonment is { } recovery &&
                recovery.Publication == publication && recovery.BeginPayloadDigest == begin.PayloadDigest &&
                !string.IsNullOrWhiteSpace(recovery.RecoverySubjectId) && recovery.RecoverySubjectId.Length <= 256 &&
                HexDigest(recovery.EvidenceDigest) && ExactDigests(begin, abandoned, recovery);
        }
        catch (Exception) { return false; }
    }

    private static bool ExactDigests(KnowledgeOperation begin, KnowledgeOperation abandoned, BusinessFromClAbandonment recovery)
    {
        var request = new BusinessFromClRequest(recovery.Publication, recovery.BeginSubjectId, begin.OperationId, begin.PayloadDigest);
        var maintenance = new BusinessFromClRecoveryRequest(request, recovery.RecoverySubjectId, recovery.EvidenceDigest, abandoned.PayloadDigest);
        return !string.IsNullOrWhiteSpace(recovery.BeginSubjectId) &&
            request.CanonicalPayloadDigest == OctoberBusinessFromClPolicy.ComputeBeginDigest(request) &&
            maintenance.CanonicalPayloadDigest == OctoberBusinessFromClRecoveryPolicy.ComputeDigest(maintenance);
    }

    private static bool Valid(BusinessFromClRecoveryRequest request)
    {
        try
        {
            return request.Begin.RunId != Guid.Empty && request.Begin.Publication.PublicationId != Guid.Empty &&
                request.Begin.Publication.Knowledge.BatchId != Guid.Empty && !string.IsNullOrWhiteSpace(request.Begin.SubjectId) &&
                request.Begin.CanonicalPayloadDigest == OctoberBusinessFromClPolicy.ComputeBeginDigest(request.Begin) &&
                !string.IsNullOrWhiteSpace(request.RecoverySubjectId) && request.RecoverySubjectId.Length <= 256 &&
                HexDigest(request.EvidenceDigest) && request.CanonicalPayloadDigest == OctoberBusinessFromClRecoveryPolicy.ComputeDigest(request);
        }
        catch (Exception) { return false; }
    }

    private static bool HexDigest(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static async Task<KnowledgeOperation?> FindAsync(OctoberDbContext context, OperationKey operation, CancellationToken ct) =>
        await context.KnowledgeOperations.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == operation.BatchId &&
            x.Kind == operation.Kind && x.OperationId == operation.OperationId, ct);

    private sealed class RecoveryAuthorization(IOctoberBusinessFromClRecoveryAuthority authority, BusinessFromClRecoveryRequest request)
        : IOctoberKnowledgeProducerAuthorization
    {
        public async Task<DisclosureStatus> CheckAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors,
            CancellationToken cancellationToken = default)
        {
            if (binding != request.Begin.Publication.Knowledge || !contributors.IsEmpty) return DisclosureStatus.Denied;
            try
            {
                return await authority.VerifyAsync(request, cancellationToken).WaitAsync(cancellationToken) == BusinessFromClSettlement.Settled
                    ? DisclosureStatus.Allowed : DisclosureStatus.AuthorityUnavailable;
            }
            catch (Exception) { return DisclosureStatus.AuthorityUnavailable; }
        }
    }
}
