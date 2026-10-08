using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberDialogue;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Durable, fenced dialogue on the owned October database. No generation or provider I/O occurs here.</summary>
public sealed class OctoberDialogueStore : IOctoberDialogueStore, IAsyncDisposable
{
    private static readonly UTF8Encoding _utf8 = new(false, true);
    private readonly IDbContextFactory<OctoberDbContext> _contexts;
    private readonly KnowledgeBinding _binding;
    private readonly IOctoberKnowledgeDisclosurePolicy _disclosure;
    private readonly IOctoberKnowledgeDependencyAuthority _dependencies;
    private readonly IOctoberDialogueExecutionAuthority _execution;
    private readonly IOctoberDialogueCommitGuard _guard;
    private readonly IOctoberDialoguePayloadCodec _codec;
    private readonly TimeProvider _clock;
    private readonly object _lifetimeGate = new();
    private int _ownedOperations;
    private bool _closing;
    private bool _cleanupFailed;
    private TaskCompletionSource? _disposal;

    public OctoberDialogueStore(IDbContextFactory<OctoberDbContext> contexts, KnowledgeBinding configuredBinding,
        IOctoberKnowledgeDisclosurePolicy disclosure, IOctoberKnowledgeDependencyAuthority dependencies,
        IOctoberDialogueExecutionAuthority execution, IOctoberDialogueCommitGuard guard,
        IOctoberDialoguePayloadCodec codec, TimeProvider timeProvider)
    {
        _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        _binding = configuredBinding ?? throw new ArgumentNullException(nameof(configuredBinding));
        _disclosure = disclosure ?? throw new ArgumentNullException(nameof(disclosure));
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _clock = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task<DialogueConversationResult> OpenConversationAsync(DialogueConversationOpenRequest request,
        KnowledgeSubject subject, CancellationToken cancellationToken = default) => BoundedAsync(async ct =>
    {
        if (request.ConversationId == Guid.Empty || request.ContextId == Guid.Empty || !Text(subject.SubjectId, 1024, true))
            return new DialogueConversationResult(DialogueStatus.Invalid, null);
        await using var readOwner = await OpenReadContextAsync(ct);
        var read = readOwner.Context;
        var prior = await read.DialogueConversations.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == request.ConversationId, ct);
        if (prior is not null) return await ProjectConversationAsync(read, prior, request, subject, DialogueStatus.Existing, ct);
        if (request.CreateIfMissing is null) return new(DialogueStatus.Indeterminate, null);
        var identity = new DialogueConversationIdentity(request.ConversationId, request.ContextId, subject.SubjectId,
            request.CreateIfMissing.Publication, request.CreateIfMissing.Mode, request.CreateIfMissing.RepositoryScope);
        if (!ValidIdentity(identity)) return new(DialogueStatus.Invalid, null);
        var auth = await AuthorizeAsync(read, identity, subject, null, ct);
        if (auth != DialogueStatus.Succeeded) return new(auth, null);
        var attempt = new DialogueCommitAttempt(DialogueMutationKind.CreateConversation, identity.ConversationId, identity,
            null, null, null, OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(identity)), subject, null);
        var status = await MutateAsync(attempt, async (db, token) =>
        {
            if (await db.DialogueConversations.AnyAsync(x => x.ConversationId == identity.ConversationId, token)) return DialogueStatus.Existing;
            var permitted = await AuthorizeAsync(db, identity, subject, null, token);
            if (permitted != DialogueStatus.Succeeded) return permitted;
            db.DialogueConversations.Add(new()
            {
                ConversationId = identity.ConversationId, ContextId = identity.ContextId, OwnerSubjectId = identity.OwnerSubjectId,
                BatchId = identity.Publication.Knowledge.BatchId, PublicationId = identity.Publication.PublicationId,
                IdentityJson = OctoberKnowledgePolicy.SerializeCanonical(identity), CreatedAtUtc = _clock.GetUtcNow(), NextTurnSequence = 1, NextFence = 1
            });
            return DialogueStatus.Succeeded;
        }, token => AuthorizeFreshAsync(identity, subject, null, token), ct);
        if (status is not (DialogueStatus.Succeeded or DialogueStatus.Existing or DialogueStatus.Indeterminate)) return new(status, null);
        await using var finalOwner = await OpenReadContextAsync(ct);
        var final = finalOwner.Context;
        var row = await final.DialogueConversations.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == identity.ConversationId, ct);
        return row is null ? new(DialogueStatus.Indeterminate, null) : await ProjectConversationAsync(final, row, request, subject,
            status == DialogueStatus.Succeeded ? DialogueStatus.Succeeded : DialogueStatus.Existing, ct);
    }, status => new(status, null), cancellationToken);

    public Task<DialogueBeginResult> BeginTurnAsync(DialogueBeginRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => BoundedAsync(async ct =>
    {
        await using var readOwner = await OpenReadContextAsync(ct);
        var read = readOwner.Context;
        var row = await read.DialogueConversations.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == request.ConversationId, ct);
        if (row is null) return new DialogueBeginResult(DialogueStatus.Indeterminate, null, false);
        var identity = Identity(row);
        var auth = await AuthorizeAsync(read, identity, subject, null, ct);
        if (auth != DialogueStatus.Succeeded) return new(auth, null, false);
        if (request.ContextId != identity.ContextId || !ValidBegin(identity, request)) return new(DialogueStatus.Invalid, null, false);
        var existing = await read.DialogueTurns.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == request.ConversationId && x.ClientRequestKey == request.ClientRequestKey, ct);
        if (existing is not null) return await ProjectAdmissionAsync(read, identity, existing, request.RequestDigest, subject, false, ct);
        auth = DependencyStatus(await ReauthorizeDependenciesAsync(Selection(identity, subject), [request.InitialDependencies], ct));
        if (auth != DialogueStatus.Succeeded) return new(auth, null, false);
        if (await VerifyAdmissionAsync(request, ct) != DialogueExecutionStatus.Verified) return new(DialogueStatus.AuthorityUnavailable, null, false);
        var attempt = new DialogueCommitAttempt(DialogueMutationKind.AdmitTurn, request.ClientRequestKey, identity, null,
            request.Execution, null, request.RequestDigest, subject, request.InitialDependencies);
        var status = await MutateAsync(attempt, async (db, token) =>
        {
            var conversation = await db.DialogueConversations.SingleAsync(x => x.ConversationId == identity.ConversationId, token);
            if (await db.DialogueTurns.AnyAsync(x => x.ConversationId == request.ConversationId && x.ClientRequestKey == request.ClientRequestKey, token))
                return DialogueStatus.Existing;
            if (await db.DialogueTurns.AnyAsync(x => x.ConversationId == request.ConversationId && x.State < DialogueTurnState.Completed, token))
                return DialogueStatus.Busy;
            var history = await HistoryAsync(db, identity, new(identity.ConversationId, identity.ContextId, null,
                request.Limits.HistoryTurns, request.Limits.HistoryBytes), subject, token);
            if (history.Status != DialogueStatus.Succeeded) return history.Status;
            if (!request.HistoryTurnIds.SequenceEqual(history.Turns.Select(x => x.TurnId)) || request.HistoryProjectionDigest != history.ProjectionDigest)
                return DialogueStatus.Conflict;
            if (history.Dependencies is not null)
            {
                var union = await MergeDependenciesAsync(Selection(identity, subject), [request.InitialDependencies, history.Dependencies], token);
                if (union.Status != KnowledgeDependencyStatus.Succeeded) return DependencyStatus(union.Status);
                if (union.Dependencies?.BindingDigest != request.InitialDependencies.BindingDigest) return DialogueStatus.Invalid;
            }
            long sequence = conversation.NextTurnSequence;
            long fence = conversation.NextFence;
            conversation.NextTurnSequence = checked(sequence + 1);
            conversation.NextFence = checked(fence + 1);
            var now = _clock.GetUtcNow();
            db.DialogueTurns.Add(new()
            {
                ConversationId = identity.ConversationId, TurnId = Guid.NewGuid(), ClientRequestKey = request.ClientRequestKey,
                Sequence = sequence, Fence = fence, OwnerGeneration = request.Execution.OwnerGeneration, AttemptId = request.Execution.AttemptId,
                RequestDigest = request.RequestDigest, BeginEnvelopeJson = OctoberDialoguePayloadCodec.EncodeBegin(request),
                AdmittedAtUtc = now, DeadlineUtc = now.Add(request.Limits.Duration), State = DialogueTurnState.Admitted
            });
            return DialogueStatus.Succeeded;
        }, token => AuthorizeFreshAsync(identity, subject, request.InitialDependencies, token), ct);
        if (status is not (DialogueStatus.Succeeded or DialogueStatus.Existing or DialogueStatus.Indeterminate)) return new(status, null, false);
        await using var finalOwner = await OpenReadContextAsync(ct);
        var final = finalOwner.Context;
        var saved = await final.DialogueTurns.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == identity.ConversationId && x.ClientRequestKey == request.ClientRequestKey, ct);
        return saved is null ? new(DialogueStatus.Indeterminate, null, false) : await ProjectAdmissionAsync(final, identity, saved,
            request.RequestDigest, subject, status == DialogueStatus.Succeeded, ct);
    }, status => new(status, null, false), cancellationToken);

    public Task<DialogueBeginResult> LookupAdmissionAsync(DialogueAdmissionLookup request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => BoundedAsync(async ct =>
    {
        await using var dbOwner = await OpenReadContextAsync(ct);
        var db = dbOwner.Context;
        var row = await db.DialogueConversations.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == request.ConversationId, ct);
        if (row is null) return new DialogueBeginResult(DialogueStatus.Indeterminate, null, false);
        var identity = Identity(row);
        var auth = await AuthorizeAsync(db, identity, subject, null, ct);
        if (auth != DialogueStatus.Succeeded) return new(auth, null, false);
        if (request.ContextId != identity.ContextId || !Digest(request.RequestDigest)) return new(DialogueStatus.Invalid, null, false);
        var turn = await db.DialogueTurns.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == request.ConversationId && x.ClientRequestKey == request.ClientRequestKey, ct);
        return turn is null ? new(DialogueStatus.Indeterminate, null, false) : await ProjectAdmissionAsync(db, identity, turn, request.RequestDigest, subject, false, ct);
    }, status => new(status, null, false), cancellationToken);

    public Task<DialogueTransitionResult> TransitionTurnAsync(DialogueTransitionRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => BoundedAsync(async ct =>
    {
        await using var readOwner = await OpenReadContextAsync(ct);
        var read = readOwner.Context;
        var row = await read.DialogueConversations.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == request.Owner.ConversationId, ct);
        if (row is null) return new DialogueTransitionResult(DialogueStatus.Indeterminate, null);
        var identity = Identity(row);
        var auth = await AuthorizeAsync(read, identity, subject, null, ct);
        if (auth != DialogueStatus.Succeeded) return new(auth, null);
        if (!ValidTransition(request.ExpectedState, request.NextState)) return new(DialogueStatus.Invalid, null);
        var turn = await read.DialogueTurns.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == row.ConversationId && x.TurnId == request.Owner.TurnId, ct);
        if (turn is null) return new(DialogueStatus.Indeterminate, null);
        if (!Owns(identity, turn, request.Owner)) return new(DialogueStatus.StaleFence, null);
        var begin = OctoberDialoguePayloadCodec.DecodeBegin(turn.BeginEnvelopeJson);
        string digest = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(request));
        var attempt = new DialogueCommitAttempt(DialogueMutationKind.TransitionTurn, new Guid(Convert.FromHexString(digest).AsSpan(0, 16)),
            identity, turn.TurnId, request.Owner.Execution, request.Owner.Fence, digest, subject, begin.InitialDependencies);
        var status = await MutateAsync(attempt, async (db, token) =>
        {
            var current = await db.DialogueTurns.SingleAsync(x => x.ConversationId == row.ConversationId && x.TurnId == turn.TurnId, token);
            if (!Owns(identity, current, request.Owner)) return DialogueStatus.StaleFence;
            if (current.State >= DialogueTurnState.Completed || current.State == request.NextState) return DialogueStatus.Existing;
            if (current.State != request.ExpectedState) return DialogueStatus.Conflict;
            current.State = request.NextState;
            return DialogueStatus.Succeeded;
        }, token => AuthorizeFreshAsync(identity, subject, begin.InitialDependencies, token), ct);
        if (status is not (DialogueStatus.Succeeded or DialogueStatus.Existing or DialogueStatus.Indeterminate)) return new(status, null);
        await using var finalOwner = await OpenReadContextAsync(ct);
        var final = finalOwner.Context;
        var actual = await final.DialogueTurns.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == row.ConversationId && x.TurnId == turn.TurnId, ct);
        auth = await AuthorizeAsync(final, identity, subject, begin.InitialDependencies, ct);
        return auth != DialogueStatus.Succeeded ? new(auth, null) : new(status, actual?.State);
    }, status => new(status, null), cancellationToken);

    public Task<DialogueTerminalResult> SaveTerminalAsync(DialogueTerminalSaveRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => BoundedAsync(async ct =>
    {
        await using var readOwner = await OpenReadContextAsync(ct);
        var read = readOwner.Context;
        var row = await read.DialogueConversations.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == request.Owner.ConversationId, ct);
        if (row is null) return new DialogueTerminalResult(DialogueStatus.Indeterminate, null, null);
        var identity = Identity(row);
        bool sanitized = Sanitized(request);
        if (subject.SubjectId != identity.OwnerSubjectId) return new(DialogueStatus.Denied, null, null);
        var turn = await read.DialogueTurns.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == row.ConversationId && x.TurnId == request.Owner.TurnId, ct);
        if (turn is null) return new(DialogueStatus.Indeterminate, null, null);
        if (!Owns(identity, turn, request.Owner)) return new(DialogueStatus.StaleFence, null, null);
        var prior = await read.DialogueTerminals.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == row.ConversationId && x.TurnId == turn.TurnId, ct);
        if (prior is not null)
            return await ProjectTerminalAsync(read, identity, turn, prior, request.SaveOperationId, request.OutcomeDigest,
                request.Owner.Fence, subject, DialogueStatus.Existing, ct, () => _codec.ComputeOutcomeDigest(request));
        if (!ValidTerminal(request, Admission(identity, turn))) return new(DialogueStatus.Invalid, null, null);
        var verified = await VerifyTerminalAsync(read, identity, turn, request, subject, ct);
        if (verified.Status != DialogueStatus.Succeeded) return new(verified.Status, null, null);
        var attempt = new DialogueCommitAttempt(DialogueMutationKind.SaveTerminal, request.SaveOperationId, identity, turn.TurnId,
            request.Owner.Execution, request.Owner.Fence, request.OutcomeDigest, subject, request.InferenceDependencies);
        var status = await MutateAsync(attempt, async (db, token) =>
        {
            var current = await db.DialogueTurns.SingleAsync(x => x.ConversationId == row.ConversationId && x.TurnId == turn.TurnId, token);
            if (!Owns(identity, current, request.Owner)) return DialogueStatus.StaleFence;
            if (await db.DialogueTerminals.AnyAsync(x => x.ConversationId == row.ConversationId && x.TurnId == turn.TurnId, token))
                return DialogueStatus.Existing;
            if (current.State >= DialogueTurnState.Completed) return DialogueStatus.Conflict;
            var check = await VerifyTerminalAsync(db, identity, current, request, subject, token);
            if (check.Status != DialogueStatus.Succeeded) return check.Status;
            AddTerminal(db, identity, current, request, check.ConsumedJson);
            return DialogueStatus.Succeeded;
        }, async token =>
        {
            if (await VerifyExecutionTerminalAsync(Admission(identity, turn), request, token) != DialogueExecutionStatus.Verified)
                return DialogueStatus.AuthorityUnavailable;
            return sanitized ? DialogueStatus.Succeeded : await AuthorizeFreshAsync(identity, subject, request.InferenceDependencies, token);
        }, ct);
        if (status is not (DialogueStatus.Succeeded or DialogueStatus.Existing or DialogueStatus.Indeterminate)) return new(status, null, null);
        await using var finalOwner = await OpenReadContextAsync(ct);
        var final = finalOwner.Context;
        var saved = await final.DialogueTerminals.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == row.ConversationId && x.TurnId == turn.TurnId, ct);
        if (saved is null) return new(DialogueStatus.Indeterminate, null, null);
        var savedTurn = await final.DialogueTurns.AsNoTracking().SingleAsync(x => x.ConversationId == row.ConversationId && x.TurnId == turn.TurnId, ct);
        if (sanitized && status == DialogueStatus.Succeeded) return new(DialogueStatus.Succeeded, Receipt(identity, savedTurn, saved), null);
        return await ProjectTerminalAsync(final, identity, savedTurn, saved, request.SaveOperationId, request.OutcomeDigest,
            request.Owner.Fence, subject, status == DialogueStatus.Succeeded ? status : DialogueStatus.Existing, ct);
    }, status => new(status, null, null), cancellationToken);

    public Task<DialogueTerminalResult> LookupTerminalAsync(DialogueTerminalLookup request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => BoundedAsync(async ct =>
    {
        await using var dbOwner = await OpenReadContextAsync(ct);
        var db = dbOwner.Context;
        var row = await db.DialogueConversations.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == request.ConversationId, ct);
        if (row is null) return new DialogueTerminalResult(DialogueStatus.Indeterminate, null, null);
        var identity = Identity(row);
        var auth = await AuthorizeAsync(db, identity, subject, null, ct);
        if (auth != DialogueStatus.Succeeded) return new(auth, null, null);
        if (request.ContextId != identity.ContextId || request.SaveOperationId == Guid.Empty || !Digest(request.OutcomeDigest))
            return new(DialogueStatus.Invalid, null, null);
        var turn = await db.DialogueTurns.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == row.ConversationId && x.TurnId == request.TurnId, ct);
        var terminal = await db.DialogueTerminals.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == row.ConversationId && x.TurnId == request.TurnId, ct);
        return turn is null || terminal is null ? new(DialogueStatus.Indeterminate, null, null) : await ProjectTerminalAsync(db, identity,
            turn, terminal, request.SaveOperationId, request.OutcomeDigest, request.ExpectedFence, subject, DialogueStatus.Existing, ct);
    }, status => new(status, null, null), cancellationToken);

    public Task<DialogueHistoryResult> ReadHistoryAsync(DialogueHistoryRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => BoundedAsync(async ct =>
    {
        await using var dbOwner = await OpenReadContextAsync(ct);
        var db = dbOwner.Context;
        var row = await db.DialogueConversations.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == request.ConversationId, ct);
        return row is null ? new DialogueHistoryResult(DialogueStatus.Indeterminate, [], false, null, null) :
            await HistoryAsync(db, Identity(row), request, subject, ct);
    }, status => new(status, [], false, null, null), cancellationToken);

    public Task<DialogueTranscriptResult> ReadTranscriptAsync(DialogueHistoryRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => BoundedAsync(async ct =>
    {
        await using var dbOwner = await OpenReadContextAsync(ct);
        var db = dbOwner.Context;
        var row = await db.DialogueConversations.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == request.ConversationId, ct);
        if (row is null) return new DialogueTranscriptResult(DialogueStatus.Indeterminate, [], false);
        var identity = Identity(row);
        var auth = await AuthorizeAsync(db, identity, subject, null, ct);
        if (auth != DialogueStatus.Succeeded) return new(auth, [], false);
        if (!ValidHistory(identity, request)) return new(DialogueStatus.Invalid, [], false);
        var turns = await db.DialogueTurns.AsNoTracking().Where(x => x.ConversationId == row.ConversationId && x.State >= DialogueTurnState.Completed &&
            (request.BeforeSequenceExclusive == null || x.Sequence < request.BeforeSequenceExclusive))
            .OrderByDescending(x => x.Sequence).Take(request.MaximumTurns + 1).ToListAsync(ct);
        var result = new List<DialogueTerminalProjection>();
        bool omitted = false;
        foreach (var turn in turns)
        {
            if (result.Count == request.MaximumTurns) { omitted = true; break; }
            var terminal = await db.DialogueTerminals.AsNoTracking().SingleAsync(x => x.ConversationId == row.ConversationId && x.TurnId == turn.TurnId, ct);
            var saved = OctoberDialoguePayloadCodec.DecodeTerminal(terminal.TerminalEnvelopeJson);
            if (saved.InferenceDependencies is not null && await AuthorizeAsync(db, identity, subject, saved.InferenceDependencies, ct) != DialogueStatus.Succeeded)
            { omitted = true; continue; }
            var projection = Projection(identity, turn, saved);
            if (JsonSerializer.SerializeToUtf8Bytes(result.Append(projection), OctoberDialoguePayloadCodec.Json).Length > request.MaximumUtf8Bytes)
            { omitted = true; break; }
            result.Add(projection);
        }
        var capsules = result.Where(x => x.InferenceDependencies is not null).Select(x => x.InferenceDependencies!).ToImmutableArray();
        auth = await AuthorizeAsync(db, identity, subject, null, ct);
        if (auth != DialogueStatus.Succeeded) return new(auth, [], false);
        if (!capsules.IsEmpty && await ReauthorizeDependenciesAsync(Selection(identity, subject), capsules, ct) != KnowledgeDependencyStatus.Succeeded)
            return new(DialogueStatus.Succeeded, [], true);
        result.Reverse();
        return new(DialogueStatus.Succeeded, result.ToImmutableArray(), omitted);
    }, status => new(status, [], false), cancellationToken);

    public Task<DialogueOrphanResult> InterruptOrphanAsync(DialogueOrphanRequest request,
        CancellationToken cancellationToken = default) => BoundedAsync(async ct =>
    {
        if (request.OrphanOperationId == Guid.Empty) return new DialogueOrphanResult(DialogueStatus.Invalid, null);
        await using var readOwner = await OpenReadContextAsync(ct);
        var read = readOwner.Context;
        var row = await read.DialogueConversations.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == request.ConversationId, ct);
        if (row is null) return new(DialogueStatus.Indeterminate, null);
        var identity = Identity(row);
        if (request.ContextId != identity.ContextId || !await BindingExistsAsync(read, identity, ct)) return new(DialogueStatus.Denied, null);
        if (await VerifyOwnerDeathAsync(request.ExpectedExecution, ct) != DialogueExecutionStatus.Verified)
            return new(DialogueStatus.AuthorityUnavailable, null);
        string digest = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(request));
        var attempt = new DialogueCommitAttempt(DialogueMutationKind.InterruptOrphan, request.OrphanOperationId, identity,
            request.TurnId, request.ExpectedExecution, request.ExpectedFence, digest, null, null);
        var status = await MutateAsync(attempt, async (db, token) =>
        {
            var current = await db.DialogueTurns.SingleOrDefaultAsync(x => x.ConversationId == row.ConversationId && x.TurnId == request.TurnId, token);
            if (current is null) return DialogueStatus.Indeterminate;
            if (current.OwnerGeneration != request.ExpectedExecution.OwnerGeneration || current.AttemptId != request.ExpectedExecution.AttemptId)
                return DialogueStatus.StaleFence;
            if (await db.DialogueTerminals.AnyAsync(x => x.ConversationId == row.ConversationId && x.TurnId == request.TurnId, token))
                return DialogueStatus.Existing;
            if (current.Fence != request.ExpectedFence) return DialogueStatus.StaleFence;
            if (await VerifyOwnerDeathAsync(request.ExpectedExecution, token) != DialogueExecutionStatus.Verified)
                return DialogueStatus.AuthorityUnavailable;
            var conversation = await db.DialogueConversations.SingleAsync(x => x.ConversationId == row.ConversationId, token);
            current.Fence = conversation.NextFence;
            conversation.NextFence = checked(conversation.NextFence + 1);
            var terminal = new DialogueTerminalSaveRequest(new(identity.ConversationId, identity.ContextId, current.TurnId,
                request.ExpectedExecution, current.Fence), request.OrphanOperationId, "", DialogueTerminalState.Interrupted,
                DialogueTerminalReason.OwnerLost, DialogueProviderFinish.Unknown, "", [], [], null,
                OctoberDialoguePayloadCodec.DecodeBegin(current.BeginEnvelopeJson).HistoryTurnIds, _clock.GetUtcNow());
            terminal = terminal with { OutcomeDigest = _codec.ComputeOutcomeDigest(terminal) };
            AddTerminal(db, identity, current, terminal, "[]");
            return DialogueStatus.Succeeded;
        }, async token => await VerifyOwnerDeathAsync(request.ExpectedExecution, token) == DialogueExecutionStatus.Verified
            ? DialogueStatus.Succeeded : DialogueStatus.AuthorityUnavailable, ct);
        if (status is not (DialogueStatus.Succeeded or DialogueStatus.Existing or DialogueStatus.Indeterminate)) return new(status, null);
        await using var finalOwner = await OpenReadContextAsync(ct);
        var final = finalOwner.Context;
        var saved = await final.DialogueTerminals.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == row.ConversationId && x.TurnId == request.TurnId, ct);
        var turn = await final.DialogueTurns.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == row.ConversationId && x.TurnId == request.TurnId, ct);
        return saved is null || turn is null ? new(DialogueStatus.Indeterminate, null) : new(status == DialogueStatus.Succeeded ? status : DialogueStatus.Existing, Receipt(identity, turn, saved));
    }, status => new(status, null), cancellationToken);

    private async Task<DialogueStatus> MutateAsync(DialogueCommitAttempt attempt,
        Func<OctoberDbContext, CancellationToken, Task<DialogueStatus>> stage,
        Func<CancellationToken, Task<DialogueStatus>> reauthorize, CancellationToken ct)
    {
        OctoberDbContext? db = null;
        IDbContextTransaction? transaction = null;
        object submissionGate = new();
        bool submissionClosed = false;
        TaskCompletionSource<CommitSettlement>? ownedInvocation = null;
        Task<CommitSettlement>? CloseSubmission()
        {
            lock (submissionGate)
            {
                submissionClosed = true;
                return ownedInvocation?.Task;
            }
        }
        try
        {
            db = await OpenContextAsync(ct);
            var eligibility = await TrackAsync(() => _guard.CheckBeforeTransactionAsync(attempt, ct)).WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (eligibility != CommitEligibilityStatus.Eligible) return Eligibility(eligibility);
            transaction = await db.Database.BeginTransactionAsync(ct);
            db.Database.AutoSavepointsEnabled = false;
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM october.\"OctoberDialogueConversations\" WHERE \"ConversationId\" = {attempt.Conversation.ConversationId} FOR UPDATE", ct);
            var result = await stage(db, ct);
            if (result != DialogueStatus.Succeeded) return result;
            ct.ThrowIfCancellationRequested();
            await db.SaveChangesAsync(ct);
            result = await reauthorize(ct);
            if (result != DialogueStatus.Succeeded) return result;
            ct.ThrowIfCancellationRequested();
            CommitGuardResult guarded;
            try
            {
                var guardCall = TrackAsync(() => _guard.SubmitAsync(attempt, token =>
                {
                    TaskCompletionSource<CommitSettlement> owned;
                    lock (submissionGate)
                    {
                        if (submissionClosed || ownedInvocation is not null) throw new OperationCanceledException(ct);
                        ct.ThrowIfCancellationRequested();
                        token.ThrowIfCancellationRequested();
                        // Publish resource ownership before entering an API whose synchronous prelude may still be running.
                        owned = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        ownedInvocation = owned;
                    }
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        token.ThrowIfCancellationRequested();
                        // Still synchronous under the host guard; never hold our local gate across provider code.
                        Task actualCommit = transaction.GetDbTransaction().CommitAsync(token);
                        _ = TrackAsync(() => CompleteSettlementAsync(actualCommit, owned));
                        // The ownership promise is not exposed as SubmittedCommit until the actual API was invoked.
                        return new SubmittedCommit(owned.Task);
                    }
                    catch
                    {
                        owned.TrySetResult(CommitSettlement.Indeterminate);
                        throw;
                    }
                }, ct));
                _ = ObserveGuardAsync(guardCall);
                guarded = await guardCall.WaitAsync(ct);
            }
            finally
            {
                CloseSubmission();
            }
            var settlement = CloseSubmission();
            if (settlement is null) return guarded.Status switch
            {
                CommitSubmissionStatus.StaleOrFenced => DialogueStatus.StaleFence,
                CommitSubmissionStatus.Denied => DialogueStatus.Denied,
                CommitSubmissionStatus.AuthorityUnavailable => DialogueStatus.AuthorityUnavailable,
                _ => DialogueStatus.Indeterminate
            };
            return await settlement.WaitAsync(ct) == CommitSettlement.Committed ? DialogueStatus.Succeeded : DialogueStatus.Indeterminate;
        }
        catch (Exception error)
        {
            // Arbitrate in the catch body, never an exception filter that runs before inner finally blocks.
            var settlement = CloseSubmission();
            if (settlement is null && error is (ArgumentException or JsonException or OverflowException)) throw;
            return DialogueStatus.Indeterminate;
        }
        finally
        {
            var settlement = CloseSubmission();
            if (settlement is { IsCompleted: false })
            {
                // This includes a claimed provider invocation that has not returned its Task yet.
                _ = TrackAsync(() => DisposeAfterSettlementAsync(settlement, transaction, db));
            }
            else
            {
                await DisposeOwnedAsync(transaction, db);
            }
        }
    }

    private async Task DisposeAfterSettlementAsync(Task<CommitSettlement> settlement, IDbContextTransaction? transaction, OctoberDbContext? db)
    {
        try { await settlement; }
        finally
        {
            await DisposeOwnedAsync(transaction, db);
        }
    }

    private static async Task CompleteSettlementAsync(Task commit, TaskCompletionSource<CommitSettlement> owned)
    {
        try { await commit; owned.TrySetResult(CommitSettlement.Committed); }
        catch (Exception) { owned.TrySetResult(CommitSettlement.Indeterminate); }
    }

    private static async Task ObserveGuardAsync(Task guard)
    {
        try { await guard; }
        catch (Exception) { /* The bounded caller has already handled this acknowledgement failure. */ }
    }

    private async Task<DialogueConversationResult> ProjectConversationAsync(OctoberDbContext db, DialogueConversationRecord row,
        DialogueConversationOpenRequest request, KnowledgeSubject subject, DialogueStatus status, CancellationToken ct)
    {
        var identity = Identity(row);
        var auth = await AuthorizeAsync(db, identity, subject, null, ct);
        if (auth != DialogueStatus.Succeeded) return new(auth, null);
        if (request.ContextId != row.ContextId || request.CreateIfMissing is { } creation &&
            (creation.Publication != identity.Publication || creation.Mode != identity.Mode || !creation.RepositoryScope.SequenceEqual(identity.RepositoryScope)))
            return new(DialogueStatus.Conflict, null);
        var active = await db.DialogueTurns.AsNoTracking().Where(x => x.ConversationId == row.ConversationId && x.State < DialogueTurnState.Completed)
            .Select(x => (Guid?)x.TurnId).SingleOrDefaultAsync(ct);
        return new(status, new(identity, row.CreatedAtUtc, row.NextTurnSequence, active));
    }

    private async Task<DialogueBeginResult> ProjectAdmissionAsync(OctoberDbContext db, DialogueConversationIdentity identity,
        DialogueTurnRecord turn, string digest, KnowledgeSubject subject, bool fresh, CancellationToken ct)
    {
        var admission = Admission(identity, turn);
        var auth = await AuthorizeAsync(db, identity, subject, admission.Request.InitialDependencies, ct);
        if (auth != DialogueStatus.Succeeded) return new(auth, null, false);
        if (digest != turn.RequestDigest) return new(DialogueStatus.Conflict, null, false);
        if (fresh && await VerifyAdmissionAsync(admission.Request, ct) != DialogueExecutionStatus.Verified)
            return new(DialogueStatus.AuthorityUnavailable, null, false);
        return new(fresh ? DialogueStatus.Succeeded : DialogueStatus.Existing, admission, fresh);
    }

    private async Task<DialogueTerminalResult> ProjectTerminalAsync(OctoberDbContext db, DialogueConversationIdentity identity,
        DialogueTurnRecord turn, DialogueTerminalRecord terminal, Guid operation, string digest, long fence, KnowledgeSubject subject,
        DialogueStatus status, CancellationToken ct, Func<string>? recompute = null)
    {
        var saved = OctoberDialoguePayloadCodec.DecodeTerminal(terminal.TerminalEnvelopeJson);
        var auth = await AuthorizeAsync(db, identity, subject, saved.InferenceDependencies, ct);
        if (auth != DialogueStatus.Succeeded) return new(auth, null, null);
        if (fence != terminal.Fence) return new(DialogueStatus.StaleFence, null, null);
        if (operation != terminal.SaveOperationId || digest != terminal.OutcomeDigest || recompute is not null && recompute() != digest)
            return new(DialogueStatus.Conflict, null, null);
        return new(status, Receipt(identity, turn, terminal), saved.InferenceDependencies is null ? null : Projection(identity, turn, saved));
    }

    private async Task<DialogueHistoryResult> HistoryAsync(OctoberDbContext db, DialogueConversationIdentity identity,
        DialogueHistoryRequest request, KnowledgeSubject subject, CancellationToken ct)
    {
        var auth = await AuthorizeAsync(db, identity, subject, null, ct);
        if (auth != DialogueStatus.Succeeded) return new(auth, [], false, null, null);
        if (!ValidHistory(identity, request)) return new(DialogueStatus.Invalid, [], false, null, null);
        var rows = await db.DialogueTurns.AsNoTracking().Where(x => x.ConversationId == identity.ConversationId &&
            (request.BeforeSequenceExclusive == null || x.Sequence < request.BeforeSequenceExclusive))
            .OrderByDescending(x => x.Sequence).Take(request.MaximumTurns + 1).ToListAsync(ct);
        var turns = new List<DialogueHistoryTurn>();
        bool omitted = false;
        foreach (var row in rows)
        {
            if (turns.Count == request.MaximumTurns || row.State != DialogueTurnState.Completed) { omitted = true; break; }
            var terminal = await db.DialogueTerminals.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == row.ConversationId && x.TurnId == row.TurnId, ct);
            if (terminal is null) { omitted = true; break; }
            var saved = OctoberDialoguePayloadCodec.DecodeTerminal(terminal.TerminalEnvelopeJson);
            if (saved.InferenceDependencies is null || await AuthorizeAsync(db, identity, subject, saved.InferenceDependencies, ct) != DialogueStatus.Succeeded)
            { omitted = true; break; }
            var begin = OctoberDialoguePayloadCodec.DecodeBegin(row.BeginEnvelopeJson);
            var turn = new DialogueHistoryTurn(row.TurnId, row.Sequence, begin.Question, saved.AnswerText, saved.References, saved.InferenceDependencies);
            var candidate = turns.Append(turn).Reverse().ToImmutableArray();
            if (_codec.MeasureHistoryProjectionUtf8Bytes(candidate) > request.MaximumUtf8Bytes) { omitted = true; break; }
            turns.Add(turn);
        }
        turns.Reverse();
        var result = turns.ToImmutableArray();
        KnowledgeDependencyHandle? dependencies = null;
        if (!result.IsEmpty)
        {
            var merged = await MergeDependenciesAsync(Selection(identity, subject), result.Select(x => x.InferenceDependencies).ToImmutableArray(), ct);
            if (merged.Status != KnowledgeDependencyStatus.Succeeded) return new(DialogueStatus.Succeeded, [], true, _codec.ComputeHistoryProjectionDigest([]), null);
            dependencies = merged.Dependencies;
        }
        auth = await AuthorizeAsync(db, identity, subject, dependencies, ct);
        return auth != DialogueStatus.Succeeded ? new(auth, [], false, null, null) :
            new(DialogueStatus.Succeeded, result, omitted, _codec.ComputeHistoryProjectionDigest(result), dependencies);
    }

    private async Task<(DialogueStatus Status, string ConsumedJson)> VerifyTerminalAsync(OctoberDbContext db,
        DialogueConversationIdentity identity, DialogueTurnRecord turn, DialogueTerminalSaveRequest request, KnowledgeSubject subject, CancellationToken ct)
    {
        var admission = Admission(identity, turn);
        if (await VerifyExecutionTerminalAsync(admission, request, ct) != DialogueExecutionStatus.Verified)
            return (DialogueStatus.AuthorityUnavailable, "[]");
        if (Sanitized(request)) return (await BindingExistsAsync(db, identity, ct) ? DialogueStatus.Succeeded : DialogueStatus.Denied, "[]");
        var auth = await AuthorizeAsync(db, identity, subject, request.InferenceDependencies, ct);
        if (auth != DialogueStatus.Succeeded) return (auth, "[]");
        var selection = Selection(identity, subject);
        var union = await MergeDependenciesAsync(selection, [request.InferenceDependencies!, admission.Request.InitialDependencies], ct);
        if (union.Status != KnowledgeDependencyStatus.Succeeded) return (DependencyStatus(union.Status), "[]");
        if (union.Dependencies?.BindingDigest != request.InferenceDependencies!.BindingDigest) return (DialogueStatus.Invalid, "[]");
        var facts = new List<object>();
        foreach (var consumed in request.ConsumedReads)
        {
            var verified = await VerifyConsumedAsync(selection, consumed.Proof, request.InferenceDependencies, ct);
            if (verified.Status != KnowledgeDependencyStatus.Succeeded || verified.Receipt is not { } receipt)
                return (DependencyStatus(verified.Status), "[]");
            var reference = consumed.Reference;
            if (reference.Document != receipt.Document || reference.IsPartial != receipt.IsPartial || reference.SuccessfulChunks != receipt.SuccessfulChunks || reference.ContentBytes != receipt.ContentBytes)
                return (DialogueStatus.Invalid, "[]");
            if (!await ExactReferenceAsync(db, identity, reference, ct)) return (DialogueStatus.Invalid, "[]");
            facts.Add(new { receipt.Proof, receipt.Document, receipt.IsPartial, receipt.SuccessfulChunks, receipt.ContentBytes, receipt.OwnerCoverage });
        }
        if (request.References.Any(x => !request.ConsumedReads.Any(c => c.Reference == x))) return (DialogueStatus.Invalid, "[]");
        return (DialogueStatus.Succeeded, JsonSerializer.Serialize(facts, OctoberDialoguePayloadCodec.Json));
    }

    private void AddTerminal(OctoberDbContext db, DialogueConversationIdentity identity, DialogueTurnRecord turn,
        DialogueTerminalSaveRequest request, string verifiedConsumedJson)
    {
        turn.State = (DialogueTurnState)((int)request.TerminalState + 4);
        db.DialogueTerminals.Add(new()
        {
            ConversationId = turn.ConversationId, TurnId = turn.TurnId, BatchId = identity.Publication.Knowledge.BatchId,
            PublicationId = identity.Publication.PublicationId, SaveOperationId = request.SaveOperationId, OutcomeDigest = request.OutcomeDigest,
            TerminalState = request.TerminalState, Reason = request.Reason, Fence = turn.Fence,
            CommittedAtUtc = _clock.GetUtcNow(), TerminalEnvelopeJson = OctoberDialoguePayloadCodec.EncodeTerminal(request), VerifiedConsumedJson = verifiedConsumedJson
        });
        for (int ordinal = 0; ordinal < request.References.Length; ordinal++)
        {
            var document = request.References[ordinal].Document;
            db.DialogueReferences.Add(new()
            {
                ConversationId = turn.ConversationId, TurnId = turn.TurnId, Ordinal = ordinal, BatchId = document.BatchId,
                PublicationId = document.PublicationId, LogicalDocumentId = document.LogicalDocumentId,
                VersionId = document.DocumentVersionId, SectionId = document.SectionId
            });
        }
    }

    private async Task<DialogueStatus> AuthorizeFreshAsync(DialogueConversationIdentity identity, KnowledgeSubject subject,
        KnowledgeDependencyHandle? dependencies, CancellationToken ct)
    {
        await using var dbOwner = await OpenReadContextAsync(ct);
        var db = dbOwner.Context;
        return await AuthorizeAsync(db, identity, subject, dependencies, ct);
    }

    private async Task<DialogueStatus> AuthorizeAsync(OctoberDbContext db, DialogueConversationIdentity identity, KnowledgeSubject subject,
        KnowledgeDependencyHandle? dependencies, CancellationToken ct)
    {
        if (identity.OwnerSubjectId != subject.SubjectId || !await BindingExistsAsync(db, identity, ct)) return DialogueStatus.Denied;
        var status = await CheckDisclosureAsync(new(Selection(identity, subject), [], null), ct);
        if (status != DisclosureStatus.Allowed) return status == DisclosureStatus.Denied ? DialogueStatus.Denied : DialogueStatus.AuthorityUnavailable;
        return dependencies is null ? DialogueStatus.Succeeded :
            DependencyStatus(await ReauthorizeDependenciesAsync(Selection(identity, subject), [dependencies], ct));
    }

    private async Task<bool> BindingExistsAsync(OctoberDbContext db, DialogueConversationIdentity identity, CancellationToken ct) =>
        identity.Publication.Knowledge == _binding &&
        await db.Batches.AsNoTracking().AnyAsync(x => x.BatchId == _binding.BatchId && x.TargetId == _binding.TargetId &&
            x.CanonicalDatabaseIdentity == _binding.CanonicalDatabaseIdentity && x.SelectionDigest == _binding.SelectionDigest, ct) &&
        await db.KnowledgePublications.AsNoTracking().AnyAsync(x => x.BatchId == _binding.BatchId && x.PublicationId == identity.Publication.PublicationId, ct);

    private static async Task<bool> ExactReferenceAsync(OctoberDbContext db, DialogueConversationIdentity identity, OctoberChatReference reference, CancellationToken ct)
    {
        var d = reference.Document;
        if (d.BatchId != identity.Publication.Knowledge.BatchId || d.PublicationId != identity.Publication.PublicationId) return false;
        if (!await db.PublicationMembers.AnyAsync(x => x.BatchId == d.BatchId && x.PublicationId == d.PublicationId &&
            x.LogicalDocumentId == d.LogicalDocumentId && x.VersionId == d.DocumentVersionId, ct)) return false;
        return d.SectionId is null || await db.DocumentSections.AnyAsync(x => x.BatchId == d.BatchId && x.VersionId == d.DocumentVersionId && x.SectionId == d.SectionId, ct);
    }

    private static ReadSelectionRequest Selection(DialogueConversationIdentity identity, KnowledgeSubject subject) =>
        new(identity.Publication, subject, identity.Mode, KnowledgePurpose.Chat, identity.RepositoryScope);
    private static DialogueConversationIdentity Identity(DialogueConversationRecord row) =>
        JsonSerializer.Deserialize<DialogueConversationIdentity>(row.IdentityJson, OctoberDialoguePayloadCodec.Json) ?? throw new JsonException("Missing dialogue identity.");
    private static DialogueAdmission Admission(DialogueConversationIdentity identity, DialogueTurnRecord turn) =>
        new(identity, OctoberDialoguePayloadCodec.DecodeBegin(turn.BeginEnvelopeJson), turn.TurnId, turn.Sequence, turn.Fence, turn.AdmittedAtUtc, turn.DeadlineUtc, turn.State);
    private static DialogueTerminalReceipt Receipt(DialogueConversationIdentity identity, DialogueTurnRecord turn, DialogueTerminalRecord terminal) =>
        new(identity.ConversationId, identity.ContextId, turn.TurnId, turn.Sequence, terminal.Fence, terminal.SaveOperationId,
            terminal.OutcomeDigest, terminal.TerminalState, terminal.Reason, terminal.CommittedAtUtc);
    private static DialogueTerminalProjection Projection(DialogueConversationIdentity identity, DialogueTurnRecord turn, DialogueTerminalSaveRequest request) =>
        new(identity, turn.TurnId, turn.Sequence, request.InferenceDependencies is null ? "" : OctoberDialoguePayloadCodec.DecodeBegin(turn.BeginEnvelopeJson).Question,
            request.AnswerText, request.TerminalState, request.Reason, request.References, request.InferenceDependencies);
    private static bool Owns(DialogueConversationIdentity identity, DialogueTurnRecord turn, DialogueTurnOwnership owner) =>
        identity.ContextId == owner.ContextId && turn.Fence == owner.Fence && turn.OwnerGeneration == owner.Execution.OwnerGeneration && turn.AttemptId == owner.Execution.AttemptId;

    private static bool Text(string? value, int maximum, bool nonblank = false)
    {
        if (value is null || nonblank && string.IsNullOrWhiteSpace(value)) return false;
        try { return _utf8.GetByteCount(value) <= maximum; }
        catch (EncoderFallbackException) { return false; }
    }
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(x => x is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static bool GuidArray(ImmutableArray<Guid> values, int maximum, bool nonempty = false) =>
        !values.IsDefault && (!nonempty || !values.IsEmpty) && values.Length <= maximum && values.All(x => x != Guid.Empty) && values.Distinct().Count() == values.Length;
    private static bool Capsule(KnowledgeDependencyHandle? value) => value is not null && value.SetId != Guid.Empty && Digest(value.BindingDigest) &&
        Text(value.ProtectedPayload, OctoberKnowledgeDisclosureLimits.MaximumProtectedPayloadUtf8Bytes, true);

    private bool ValidIdentity(DialogueConversationIdentity identity) =>
        identity.ConversationId != Guid.Empty && identity.ContextId != Guid.Empty && identity.Publication.PublicationId != Guid.Empty &&
        identity.Publication.Knowledge == _binding && _binding.BatchId != Guid.Empty && Enum.IsDefined(identity.Mode) &&
        Text(identity.OwnerSubjectId, 1024, true) && Text(_binding.CanonicalDatabaseIdentity, 4096, true) && Text(_binding.TargetId, 256, true) &&
        _binding.SelectionDigest.Length == 64 && _binding.SelectionDigest.All(Uri.IsHexDigit) && GuidArray(identity.RepositoryScope, 256, true) &&
        identity.RepositoryScope.SequenceEqual(identity.RepositoryScope.OrderBy(x => x.ToString("D"), StringComparer.Ordinal));

    private bool ValidBegin(DialogueConversationIdentity identity, DialogueBeginRequest request)
    {
        var l = request.Limits;
        if (request.ConversationId != identity.ConversationId || request.ContextId != identity.ContextId || request.ClientRequestKey == Guid.Empty ||
            request.Execution is null || request.Execution.OwnerGeneration == Guid.Empty || request.Execution.AttemptId == Guid.Empty || l is null ||
            !Text(request.Question, 32000, true) || !Text(request.ProviderProfileId, 256, true) || !Text(request.ProviderProfileRevision, 256, true) ||
            !Digest(request.RequestDigest) || !Digest(request.PromptDigest) || !Digest(request.HistoryProjectionDigest) ||
            !GuidArray(request.HistoryTurnIds, 12) || !Capsule(request.InitialDependencies)) return false;
        if (l.QuestionCharacters is < 1 or > 8000 || l.OutputBytes is < 1 or > 65536 || l.HistoryTurns is < 1 or > 12 ||
            l.HistoryBytes is < 1 or > 49152 || l.PageSize is < 1 or > 100 || l.ContentBytes is < 1 or > 65536 ||
            l.ProviderRounds < 1 || l.ToolCalls < 1 || l.ConcurrentTools < 1 || l.ToolArgumentBytes < 1 || l.ToolResultBytes < 1 ||
            l.Duration <= TimeSpan.Zero || l.SseRoundBytes < 1 || l.SseTurnBytes < 1) return false;
        if (request.Question.EnumerateRunes().Count() > l.QuestionCharacters || request.HistoryTurnIds.Length > l.HistoryTurns) return false;
        _ = _clock.GetUtcNow().Add(l.Duration);
        return _codec.MeasureBeginEnvelopeUtf8Bytes(request) <= OctoberDialogueLimits.MaximumBeginEnvelopeUtf8Bytes &&
            _codec.ComputeBeginDigest(identity, request) == request.RequestDigest;
    }

    private bool ValidTerminal(DialogueTerminalSaveRequest request, DialogueAdmission admission)
    {
        if (request.SaveOperationId == Guid.Empty || !Digest(request.OutcomeDigest) || !Enum.IsDefined(request.TerminalState) ||
            !Enum.IsDefined(request.Reason) || !Enum.IsDefined(request.ProviderFinish) || !Text(request.AnswerText, admission.Request.Limits.OutputBytes) ||
            request.References.IsDefault || request.ConsumedReads.IsDefault || request.References.Length > 256 || request.ConsumedReads.Length > 256 ||
            !GuidArray(request.HistoryTurnIds, 12) || !request.HistoryTurnIds.SequenceEqual(admission.Request.HistoryTurnIds) ||
            request.GenerationEndedAtUtc.Offset != TimeSpan.Zero || request.GenerationEndedAtUtc < admission.AdmittedAtUtc ||
            request.GenerationEndedAtUtc > _clock.GetUtcNow()) return false;
        if (request.TerminalState == DialogueTerminalState.Completed &&
            (request.ProviderFinish != DialogueProviderFinish.ValidStop || request.Reason != DialogueTerminalReason.None ||
             string.IsNullOrWhiteSpace(request.AnswerText) || request.GenerationEndedAtUtc > admission.DeadlineUtc)) return false;
        if (request.TerminalState != DialogueTerminalState.Completed && request.Reason == DialogueTerminalReason.None) return false;
        if (!Sanitized(request) && !Capsule(request.InferenceDependencies)) return false;
        if (request.References.Any(x => !ValidReference(x)) || request.ConsumedReads.Any(x => !ValidReference(x.Reference) || x.Proof is null ||
                x.Proof.ReceiptId == Guid.Empty || !Digest(x.Proof.BindingDigest) || !Text(x.Proof.ProtectedPayload, OctoberKnowledgeDisclosureLimits.MaximumProtectedPayloadUtf8Bytes, true)) ||
            request.References.Select(x => x.Document).Distinct().Count() != request.References.Length ||
            request.ConsumedReads.Select(x => x.Reference.Document).Distinct().Count() != request.ConsumedReads.Length) return false;
        return OctoberDialoguePayloadCodec.MeasureReferences(request.References) <= OctoberDialogueLimits.MaximumReferenceArrayUtf8Bytes &&
            OctoberDialoguePayloadCodec.MeasureConsumed(request.ConsumedReads) <= OctoberDialogueLimits.MaximumConsumedArrayUtf8Bytes &&
            _codec.MeasureTerminalEnvelopeUtf8Bytes(request) <= OctoberDialogueLimits.MaximumTerminalEnvelopeUtf8Bytes &&
            _codec.ComputeOutcomeDigest(request) == request.OutcomeDigest;
    }

    private static bool ValidReference(OctoberChatReference reference) => reference is not null && reference.Document is { } d &&
        d.BatchId != Guid.Empty && d.PublicationId != Guid.Empty && d.LogicalDocumentId != Guid.Empty && d.DocumentVersionId != Guid.Empty &&
        (d.SectionId is null || Text(d.SectionId, 1024, true)) && reference.SuccessfulChunks >= 1 && reference.ContentBytes >= 0;
    private static bool Sanitized(DialogueTerminalSaveRequest request) => request.TerminalState is DialogueTerminalState.Interrupted or DialogueTerminalState.Failed &&
        request.Reason is DialogueTerminalReason.AuthorizationDenied or DialogueTerminalReason.AuthorityUnavailable &&
        request.AnswerText == "" && !request.References.IsDefault && request.References.IsEmpty && !request.ConsumedReads.IsDefault &&
        request.ConsumedReads.IsEmpty && request.InferenceDependencies is null;
    private static bool ValidHistory(DialogueConversationIdentity identity, DialogueHistoryRequest request) =>
        request.ContextId == identity.ContextId && request.MaximumTurns is >= 1 and <= 12 && request.MaximumUtf8Bytes is >= 1 and <= 49152 &&
        (request.BeforeSequenceExclusive is null or > 0);
    private static bool ValidTransition(DialogueTurnState from, DialogueTurnState to) => (from, to) is
        (DialogueTurnState.Admitted, DialogueTurnState.Running) or (DialogueTurnState.Running, DialogueTurnState.Saving) or
        (DialogueTurnState.Saving, DialogueTurnState.SaveIndeterminate) or (DialogueTurnState.SaveIndeterminate, DialogueTurnState.Saving);
    private static DialogueStatus DependencyStatus(KnowledgeDependencyStatus status) => status switch
    {
        KnowledgeDependencyStatus.Succeeded => DialogueStatus.Succeeded,
        KnowledgeDependencyStatus.Denied => DialogueStatus.Denied,
        KnowledgeDependencyStatus.AuthorityUnavailable => DialogueStatus.AuthorityUnavailable,
        KnowledgeDependencyStatus.Cancelled => DialogueStatus.Indeterminate,
        _ => DialogueStatus.Invalid
    };
    private static DialogueStatus Eligibility(CommitEligibilityStatus status) => status switch
    {
        CommitEligibilityStatus.StaleOrFenced => DialogueStatus.StaleFence,
        CommitEligibilityStatus.Denied => DialogueStatus.Denied,
        CommitEligibilityStatus.AuthorityUnavailable => DialogueStatus.AuthorityUnavailable,
        _ => DialogueStatus.Indeterminate
    };

    private Task<DialogueExecutionStatus> VerifyAdmissionAsync(DialogueBeginRequest request, CancellationToken ct) =>
        ExternalAsync(() => _execution.VerifyAdmissionAsync(request, ct), ct);
    private Task<DialogueExecutionStatus> VerifyExecutionTerminalAsync(DialogueAdmission admission, DialogueTerminalSaveRequest request, CancellationToken ct) =>
        ExternalAsync(() => _execution.VerifyTerminalAsync(admission, request, ct), ct);
    private Task<DialogueExecutionStatus> VerifyOwnerDeathAsync(DialogueExecutionIdentity execution, CancellationToken ct) =>
        ExternalAsync(() => _execution.VerifyOwnerDeathAsync(execution, ct), ct);
    private Task<KnowledgeDependencyStatus> ReauthorizeDependenciesAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken ct) =>
        ExternalAsync(() => _dependencies.ReauthorizeAsync(selection, dependencies, ct), ct);
    private Task<KnowledgeDependencyResult> MergeDependenciesAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken ct) =>
        ExternalAsync(() => _dependencies.MergeAsync(selection, dependencies, ct), ct);
    private Task<KnowledgeConsumedContentResult> VerifyConsumedAsync(ReadSelectionRequest selection, KnowledgeConsumedContentHandle proof, KnowledgeDependencyHandle dependencies, CancellationToken ct) =>
        ExternalAsync(() => _dependencies.VerifyConsumedContentAsync(selection, proof, dependencies, ct), ct);
    private Task<DisclosureStatus> CheckDisclosureAsync(DisclosureRequest request, CancellationToken ct) =>
        ExternalAsync(() => _disclosure.CheckAsync(request, ct), ct);

    private async Task<T> ExternalAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        T result = await TrackAsync(operation).WaitAsync(ct);
        ct.ThrowIfCancellationRequested();
        return result;
    }

    private async Task<OctoberDbContext> OpenContextAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Task<OctoberDbContext> pending = TrackAsync(() => _contexts.CreateDbContextAsync(ct));
        OctoberDbContext? result = null;
        try
        {
            result = await pending.WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            return result;
        }
        catch
        {
            if (result is not null) await DisposeOwnedAsync(null, result);
            else _ = TrackAsync(() => DisposeLateContextAsync(pending));
            throw;
        }
    }

    private async Task DisposeLateContextAsync(Task<OctoberDbContext> pending)
    {
        OctoberDbContext context;
        try { context = await pending; }
        catch (Exception) { return; } // A failed factory produced no owned context.
        await DisposeOwnedAsync(null, context);
    }

    private Task<T> BoundedAsync<T>(Func<CancellationToken, Task<T>> action, Func<DialogueStatus, T> failure, CancellationToken cancellationToken)
    {
        lock (_lifetimeGate)
        {
            if (_closing) return Task.FromResult(failure(DialogueStatus.Indeterminate));
            _ownedOperations++;
        }
        return RunBoundedAsync(action, failure, cancellationToken);
    }

    private async Task<T> RunBoundedAsync<T>(Func<CancellationToken, Task<T>> action, Func<DialogueStatus, T> failure, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(OctoberDialogueLimits.MaximumPersistenceAttempt);
        // The bounded response and underlying operation have distinct lifetimes. The admitted
        // operation retains its contexts and may register children before retiring its ownership.
        Task<T> pending = RunOwnedAsync(() => action(deadline.Token));
        _ = ObserveGuardAsync(pending);
        try { return await pending.WaitAsync(deadline.Token); }
        catch (Exception ex) when (ex is ArgumentException or JsonException or OverflowException)
        { return failure(DialogueStatus.Invalid); }
        catch (Exception) { return failure(DialogueStatus.Indeterminate); }
    }

    /// <summary>Closes new calls and awaits all admitted work and physical cleanup. Cancellation is not settlement.</summary>
    public ValueTask DisposeAsync()
    {
        lock (_lifetimeGate)
        {
            _closing = true;
            _disposal ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            CompleteDisposalIfDrained();
            return new(_disposal.Task);
        }
    }

    private Task<T> TrackAsync<T>(Func<Task<T>> action)
    {
        // Internal children remain admissible while their registered parent drains after close.
        lock (_lifetimeGate) _ownedOperations++;
        Task<T> pending = RunOwnedAsync(action);
        _ = ObserveGuardAsync(pending);
        return pending;
    }

    private Task TrackAsync(Func<Task> action)
    {
        lock (_lifetimeGate) _ownedOperations++;
        Task pending = RunOwnedAsync(action);
        _ = ObserveGuardAsync(pending);
        return pending;
    }

    private async Task<T> RunOwnedAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        finally { RetireOwnedOperation(); }
    }

    private async Task RunOwnedAsync(Func<Task> action)
    {
        try { await action(); }
        finally { RetireOwnedOperation(); }
    }

    private void RetireOwnedOperation()
    {
        lock (_lifetimeGate)
        {
            _ownedOperations--;
            CompleteDisposalIfDrained();
        }
    }

    private void CompleteDisposalIfDrained()
    {
        if (!_closing || _ownedOperations != 0 || _disposal is null) return;
        if (_cleanupFailed) _disposal.TrySetException(new InvalidOperationException("october_dialogue_cleanup_unconfirmed"));
        else _disposal.TrySetResult();
    }

    private async Task<ReadContextLease> OpenReadContextAsync(CancellationToken ct) => new(this, await OpenContextAsync(ct));

    private sealed class ReadContextLease(OctoberDialogueStore owner, OctoberDbContext context) : IAsyncDisposable
    {
        public OctoberDbContext Context { get; } = context;
        public ValueTask DisposeAsync() => new(owner.DisposeOwnedAsync(null, Context));
    }

    private async Task DisposeOwnedAsync(IDbContextTransaction? transaction, OctoberDbContext? context)
    {
        bool failed = false;
        try { if (transaction is not null) await transaction.DisposeAsync(); }
        catch (Exception) { failed = true; }
        try { if (context is not null) await context.DisposeAsync(); }
        catch (Exception) { failed = true; }
        if (!failed) return;
        lock (_lifetimeGate) _cleanupFailed = true;
        throw new InvalidOperationException("october_dialogue_cleanup_unconfirmed");
    }
}
