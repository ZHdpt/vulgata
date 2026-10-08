using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>Observes canonical operations for one host registration; owns no persistence implementation.</summary>
internal sealed class OctoberChatObservedDialogueStore : IOctoberDialogueStore
{
    private readonly IOctoberDialogueStore _inner;
    private readonly OctoberChatExecutionRegistry _registry;
    private readonly OctoberChatExecutionRegistration _registration;
    internal OctoberChatObservedDialogueStore(IOctoberDialogueStore inner, OctoberChatExecutionRegistry registry,
        OctoberChatExecutionRegistration registration)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        _registry.RegisterStoreOwner(registration, inner);
    }

    public Task<DialogueConversationResult> OpenConversationAsync(DialogueConversationOpenRequest request,
        KnowledgeSubject subject, CancellationToken cancellationToken = default) => Observe(
            request.CreateIfMissing is null ? null : DialogueMutationKind.CreateConversation, request, subject,
            () => _inner.OpenConversationAsync(request, subject, cancellationToken));
    public Task<DialogueBeginResult> BeginTurnAsync(DialogueBeginRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => Observe(DialogueMutationKind.AdmitTurn, request, subject,
            () => _inner.BeginTurnAsync(request, subject, cancellationToken));
    public Task<DialogueBeginResult> LookupAdmissionAsync(DialogueAdmissionLookup request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => Observe(null, request, subject,
            () => _inner.LookupAdmissionAsync(request, subject, cancellationToken));
    public Task<DialogueTransitionResult> TransitionTurnAsync(DialogueTransitionRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => Observe(DialogueMutationKind.TransitionTurn, request, subject,
            () => _inner.TransitionTurnAsync(request, subject, cancellationToken));
    public Task<DialogueTerminalResult> SaveTerminalAsync(DialogueTerminalSaveRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => Observe(DialogueMutationKind.SaveTerminal, request, subject,
            () => _inner.SaveTerminalAsync(request, subject, cancellationToken));
    public Task<DialogueTerminalResult> LookupTerminalAsync(DialogueTerminalLookup request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => Observe(null, request, subject,
            () => _inner.LookupTerminalAsync(request, subject, cancellationToken));
    public Task<DialogueHistoryResult> ReadHistoryAsync(DialogueHistoryRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => Observe(null, request, subject,
            () => _inner.ReadHistoryAsync(request, subject, cancellationToken));
    public Task<DialogueTranscriptResult> ReadTranscriptAsync(DialogueHistoryRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default) => Observe(null, request, subject,
            () => _inner.ReadTranscriptAsync(request, subject, cancellationToken));
    public Task<DialogueOrphanResult> InterruptOrphanAsync(DialogueOrphanRequest request,
        CancellationToken cancellationToken = default) => Task.FromException<DialogueOrphanResult>(
            new InvalidOperationException("Owner death has not been established."));

    private Task<T> Observe<T>(DialogueMutationKind? kind, object request, KnowledgeSubject subject, Func<Task<T>> start) =>
        _registry.ObserveStoreAsync(_registration, kind, request, subject, start);
}
