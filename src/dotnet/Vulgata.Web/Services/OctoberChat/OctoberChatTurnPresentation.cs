using System.Collections.Immutable;
using System.Text;
using System.Threading.Channels;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberChat;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>
/// One trusted host attachment, created only after confirmed Running inside the
/// actual generation-task observer. The original admission binds identity; it does
/// not by itself authorize construction, dispatch or a claim of durable completion.
/// Delivery owns a separate lifetime from generation and has no replay or reattach.
/// A trusted asynchronous sink must recheck the supplied token and IsDetached at
/// its actual UI apply/render handoff. Authorization cannot recall content already
/// handed over or force an uncooperative subscriber to finish.
/// </summary>
public sealed class OctoberChatTurnPresentation : IAsyncDisposable
{
    public const int DefaultQueueCapacity = 64;
    public const int MaximumQueueCapacity = 256;
    public const long DefaultMaximumRetainedBytes = 4 * 1024 * 1024;
    public const long MaximumRetainedBytesLimit = 8 * 1024 * 1024;
    private static readonly UTF8Encoding _utf8 = new(false, true);
    private readonly object _gate = new();
    private readonly OctoberChatTurnBinding _binding;
    private readonly DialogueAdmission _admission;
    private readonly Guid _saveOperationId;
    private readonly IOctoberChatContextProvider _context;
    private readonly IOctoberKnowledgeDependencyAuthority _dependencies;
    private readonly Func<OctoberChatEvent, CancellationToken, ValueTask> _deliver;
    private readonly TimeProvider _clock;
    private readonly long _maximumRetainedBytes;
    private readonly Channel<PendingEvent> _events;
    private readonly CancellationTokenSource _deliveryCancellation = new();
    private Task _cancellationTask = Task.CompletedTask;
    private long _retainedBytes;
    private long _sequence;
    private bool _detached;
    private bool _overflowed;
    private bool _terminalQueued;
    private bool _uncertainQueued;
    private bool _suppressionPending;
    private int _disposed;

    private PendingEvent? _textTail;

    private sealed class PendingEvent(OctoberChatEvent item, KnowledgeDependencyHandle? dependencies, long reservedBytes)
    {
        public OctoberChatEvent Item { get; set; } = item;
        public KnowledgeDependencyHandle? Dependencies { get; } = dependencies;
        public long ReservedBytes { get; set; } = reservedBytes;
    }

    public OctoberChatTurnPresentation(OctoberChatTurnBinding binding, DialogueAdmission admission,
        Guid saveOperationId, IOctoberChatContextProvider context, IOctoberKnowledgeDependencyAuthority dependencies,
        Func<OctoberChatEvent, CancellationToken, ValueTask> deliver)
        : this(binding, admission, saveOperationId, context, dependencies, deliver, TimeProvider.System)
    {
    }

    internal OctoberChatTurnPresentation(OctoberChatTurnBinding binding, DialogueAdmission admission,
        Guid saveOperationId, IOctoberChatContextProvider context, IOctoberKnowledgeDependencyAuthority dependencies,
        Func<OctoberChatEvent, CancellationToken, ValueTask> deliver, TimeProvider clock,
        int queueCapacity = DefaultQueueCapacity, long maximumRetainedBytes = DefaultMaximumRetainedBytes)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(deliver);
        ArgumentNullException.ThrowIfNull(clock);
        if (queueCapacity is < 1 or > MaximumQueueCapacity || maximumRetainedBytes is < 1 or > MaximumRetainedBytesLimit)
            throw new ArgumentOutOfRangeException(nameof(queueCapacity), "Presentation queue bounds are invalid.");
        _binding = binding;
        _admission = admission;
        _saveOperationId = saveOperationId;
        _context = context;
        _dependencies = dependencies;
        _deliver = deliver;
        _clock = clock;
        _maximumRetainedBytes = maximumRetainedBytes;
        if (saveOperationId == Guid.Empty || !MatchesAdmission(admission) || !MatchesContext(binding.Request) ||
            !ValidCapsule(admission.Request.InitialDependencies))
            throw new ArgumentException("The presentation attachment does not match the admitted turn.");
        _events = Channel.CreateBounded<PendingEvent>(new BoundedChannelOptions(queueCapacity)
        {
            SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        _context.ContextChanged += Detach;
        _ = Enqueue(OctoberChatEventKind.TurnStarted, admission.Request.InitialDependencies);
        // Never invoke a subscriber inline in the constructor or producer's call.
        Completion = Task.Run(DeliverAsync);
    }

    public bool IsDetached
    {
        get
        {
            lock (_gate) return _detached;
        }
    }

    public bool Overflowed
    {
        get
        {
            lock (_gate) return _overflowed;
        }
    }

    /// <summary>Actual delivery lifetime, not proof that all events were displayed.</summary>
    public Task Completion { get; }

    /// <summary>Conservative managed-memory reservation, including the active callback; not serialized byte accounting.</summary>
    internal long RetainedBytes
    {
        get
        {
            lock (_gate) return _retainedBytes;
        }
    }

    public bool TryPublishText(string text, KnowledgeDependencyHandle dependencies)
    {
        try
        {
            return text is not null && _utf8.GetByteCount(text) <= OctoberDialogueLimits.MaximumAnswerUtf8Bytes &&
                   ValidCapsule(dependencies) && Enqueue(OctoberChatEventKind.AnswerDelta, dependencies, text);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool TryPublishProgress(OctoberChatToolProgress progress, KnowledgeDependencyHandle dependencies)
    {
        try
        {
            if (progress is null || progress.OperationId == Guid.Empty || !Enum.IsDefined(progress.Kind) || !ValidCapsule(dependencies)) return false;
            OctoberChatEventKind kind = progress.Kind switch
            {
                OctoberChatToolProgressKind.Started => OctoberChatEventKind.ToolStarted,
                OctoberChatToolProgressKind.Completed => OctoberChatEventKind.ToolCompleted,
                _ => OctoberChatEventKind.ToolFailed
            };
            return Enqueue(kind, dependencies, toolId: progress.OperationId.ToString("N"));
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Accepts trusted coordinator results only, never browser-supplied receipt claims.</summary>
    public bool TryPublishResult(OctoberChatCoordinationResult result)
    {
        try
        {
            if (result?.Begin.Admission is not { } admission || !MatchesAdmission(admission)) return false;
            if (result.State == DialogueTurnState.SaveIndeterminate && result.Terminal?.Receipt is null)
                return Enqueue(OctoberChatEventKind.TurnSaveUncertain, _admission.Request.InitialDependencies);
            if (result.Terminal is not { Status: DialogueStatus.Succeeded or DialogueStatus.Existing, Receipt: { } receipt } terminal ||
                result.PendingSave is not null || receipt.ConversationId != _binding.ConversationId || receipt.ContextId != _binding.ContextId ||
                receipt.TurnId != _binding.TurnId || receipt.Sequence != _admission.Sequence || receipt.Fence != _admission.Fence ||
                receipt.SaveOperationId != _saveOperationId || !ValidDigest(receipt.OutcomeDigest) || !Enum.IsDefined(receipt.TerminalState) ||
                !Enum.IsDefined(receipt.Reason) || receipt.CommittedAtUtc == default || receipt.CommittedAtUtc.Offset != TimeSpan.Zero)
                return false;
            var (state, outcome) = receipt.TerminalState switch
            {
                DialogueTerminalState.Completed => (DialogueTurnState.Completed, OctoberChatDisplayOutcome.Completed),
                DialogueTerminalState.Interrupted => (DialogueTurnState.Interrupted, OctoberChatDisplayOutcome.Interrupted),
                _ => (DialogueTurnState.Failed, OctoberChatDisplayOutcome.Failed)
            };
            if (result.State != state) return false;
            if (terminal.Projection is { } projection)
            {
                if (!MatchesConversation(projection.Conversation) || projection.TurnId != _binding.TurnId || projection.Sequence != _admission.Sequence ||
                    projection.TerminalState != receipt.TerminalState || projection.Reason != receipt.Reason ||
                    projection.Question != _admission.Request.Question || projection.AnswerText is null ||
                    _utf8.GetByteCount(projection.AnswerText) > OctoberDialogueLimits.MaximumAnswerUtf8Bytes ||
                    !ValidReferences(projection.References) || !ValidCapsule(projection.InferenceDependencies)) return false;
                return Enqueue(OctoberChatEventKind.TurnSaved, projection.InferenceDependencies, projection.AnswerText,
                    references: projection.References, outcome: outcome);
            }

            // The owner intentionally returns no dependent content for these
            // confirmed closures. Missing ordinary projection never means success.
            if (receipt.TerminalState is not (DialogueTerminalState.Failed or DialogueTerminalState.Interrupted) ||
                receipt.Reason is not (DialogueTerminalReason.AuthorizationDenied or DialogueTerminalReason.AuthorityUnavailable)) return false;
            return Enqueue(OctoberChatEventKind.TurnSaved, null, string.Empty, references: [], outcome: outcome);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool Enqueue(OctoberChatEventKind kind, KnowledgeDependencyHandle? dependencies, string? text = null,
        string? toolId = null, ImmutableArray<OctoberChatReference> references = default, OctoberChatDisplayOutcome? outcome = null)
    {
        // Deliberately overcount shared strings/capsules per item. This is a local
        // display queue policy and does not spend or reset the execution budget.
        long reserved = 256 + 2L * ((text?.Length ?? 0) + (toolId?.Length ?? 0));
        if (dependencies is not null) reserved += 512 + 2L * (dependencies.BindingDigest.Length + dependencies.ProtectedPayload.Length);
        if (!references.IsDefault)
            foreach (OctoberChatReference reference in references)
                reserved += 256 + 2L * (reference.Document.SectionId?.Length ?? 0);
        lock (_gate)
        {
            if (_detached || _suppressionPending || _terminalQueued || _uncertainQueued && kind != OctoberChatEventKind.TurnSaved) return false;
            // Coalesce only adjacent, still-queued text with the exact same proof.
            // The consumer still reauthorizes at handoff; no permission is cached.
            if (kind == OctoberChatEventKind.AnswerDelta && _textTail is { } tail && tail.Dependencies == dependencies &&
                _utf8.GetByteCount(tail.Item.Text + text) <= OctoberDialogueLimits.MaximumAnswerUtf8Bytes)
            {
                long added = 2L * (text?.Length ?? 0);
                if (added > _maximumRetainedBytes - _retainedBytes)
                {
                    _overflowed = true;
                    DetachLocked();
                    return false;
                }

                tail.Item = tail.Item with { Text = tail.Item.Text + text };
                tail.ReservedBytes += added;
                _retainedBytes += added;
                return true;
            }

            if (reserved > _maximumRetainedBytes - _retainedBytes)
            {
                _overflowed = true;
                DetachLocked();
                return false;
            }

            var item = new OctoberChatEvent(_binding.ConversationId, _binding.ContextId, _binding.TurnId,
                ++_sequence, _clock.GetUtcNow(), kind, text, toolId, references, outcome);
            _retainedBytes += reserved;
            var pending = new PendingEvent(item, dependencies, reserved);
            if (!_events.Writer.TryWrite(pending))
            {
                _retainedBytes -= reserved;
                _overflowed = true;
                DetachLocked();
                return false;
            }

            _textTail = kind == OctoberChatEventKind.AnswerDelta ? pending : null;
            _uncertainQueued |= kind == OctoberChatEventKind.TurnSaveUncertain;
            if (kind == OctoberChatEventKind.TurnSaved)
            {
                _terminalQueued = true;
                _events.Writer.TryComplete();
            }

            return true;
        }
    }

    private async Task DeliverAsync()
    {
        CancellationToken token = _deliveryCancellation.Token;
        bool startedDelivered = false;
        try
        {
            await foreach (PendingEvent pending in _events.Reader.ReadAllAsync(token))
            {
                // Claim before inspecting the event so a publisher cannot mutate
                // an item being authorized or delivered.
                lock (_gate)
                {
                    if (ReferenceEquals(_textTail, pending)) _textTail = null;
                }

                try
                {
                    if (IsDetached) break;
                    ReadSelectionRequest? before = await _context.GetCurrentAsync(token);
                    if (!MatchesContext(before))
                    {
                        Detach();
                        break;
                    }

                    KnowledgeDependencyStatus authorization = pending.Dependencies is { } complete
                        ? await _dependencies.ReauthorizeAsync(before!, [complete], token) : KnowledgeDependencyStatus.Succeeded;
                    bool suppress = authorization is KnowledgeDependencyStatus.Denied or KnowledgeDependencyStatus.AuthorityUnavailable;
                    if (authorization != KnowledgeDependencyStatus.Succeeded && !suppress)
                    {
                        Detach();
                        break;
                    }

                    if (suppress)
                    {
                        lock (_gate)
                        {
                            // Stop accepting content, but keep this attachment valid
                            // until its sequential content-free UI handoff returns.
                            _suppressionPending = true;
                            _events.Writer.TryComplete();
                        }
                    }

                    ReadSelectionRequest? after = await _context.GetCurrentAsync(token);
                    if (!MatchesContext(after) || after!.Subject.AuthenticationBinding != before!.Subject.AuthenticationBinding || IsDetached)
                    {
                        Detach();
                        break;
                    }

                    token.ThrowIfCancellationRequested();
                    if (suppress)
                    {
                        if (startedDelivered)
                        {
                            // Replace the refused item at its existing sequence. No
                            // dependent payload, terminal outcome or save claim crosses.
                            await _deliver(pending.Item with
                            {
                                Kind = OctoberChatEventKind.DisclosureSuppressed, Text = null,
                                ToolCallId = null, References = default, SavedOutcome = null
                            }, token);
                        }

                        break; // Finally detaches only after the actual callback settles.
                    }

                    // Await the actual callback. Cancellation cannot pretend a
                    // delayed UI handoff has already returned or been recalled.
                    await _deliver(pending.Item, token);
                    startedDelivered |= pending.Item.Kind == OctoberChatEventKind.TurnStarted;
                    if (pending.Item.Kind == OctoberChatEventKind.TurnSaved) break;
                }
                finally
                {
                    Release(pending);
                }
            }
        }
        catch (Exception)
        {
            Detach();
        }
        finally
        {
            Detach();
            _context.ContextChanged -= Detach;
            while (_events.Reader.TryRead(out PendingEvent? pending)) Release(pending);
            Task cancellation;
            lock (_gate) cancellation = _cancellationTask;
            try
            {
                await cancellation;
            }
            catch (Exception)
            {
            } // Throwing token callbacks never escape into generation.
        }
    }

    private void Release(PendingEvent pending)
    {
        lock (_gate) _retainedBytes -= pending.ReservedBytes;
    }

    public void Detach()
    {
        lock (_gate) DetachLocked();
    }

    private void DetachLocked()
    {
        if (_detached) return;
        _detached = true;
        _textTail = null;
        _events.Writer.TryComplete();
        // Never execute arbitrary cancellation registrations on the publisher.
        _cancellationTask = Task.Run(() => _deliveryCancellation.CancelAsync());
    }

    public async ValueTask DisposeAsync()
    {
        Detach();
        await Completion;
        if (Interlocked.Exchange(ref _disposed, 1) == 0) _deliveryCancellation.Dispose();
    }

    private bool MatchesAdmission(DialogueAdmission admission) => MatchesConversation(admission.Conversation) &&
                                                                  admission.TurnId == _binding.TurnId && admission.Sequence == _admission.Sequence &&
                                                                  admission.Fence == _admission.Fence &&
                                                                  admission.Request.ConversationId == _binding.ConversationId &&
                                                                  admission.Request.ContextId == _binding.ContextId &&
                                                                  admission.Request.ClientRequestKey == _admission.Request.ClientRequestKey &&
                                                                  admission.Request.RequestDigest == _admission.Request.RequestDigest &&
                                                                  admission.Request.Execution == _admission.Request.Execution && admission.Sequence > 0 && admission.Fence > 0;

    private bool MatchesConversation(DialogueConversationIdentity identity) =>
        identity.ConversationId == _binding.ConversationId && identity.ContextId == _binding.ContextId &&
        identity.OwnerSubjectId == _binding.Request.Subject.SubjectId && identity.Publication == _binding.Request.Publication &&
        identity.Mode == _binding.Request.Mode && !identity.RepositoryScope.IsDefault &&
        identity.RepositoryScope.Order().SequenceEqual(_binding.Request.RepositoryScope.Order());

    private bool MatchesContext(ReadSelectionRequest? current) => current is not null && current.Subject is not null &&
                                                                  !string.IsNullOrWhiteSpace(current.Subject.AuthenticationBinding) &&
                                                                  current.Subject.SubjectId == _binding.Request.Subject.SubjectId &&
                                                                  current.Purpose == KnowledgePurpose.Chat && current.Publication == _binding.Request.Publication &&
                                                                  current.Mode == _binding.Request.Mode &&
                                                                  !current.RepositoryScope.IsDefaultOrEmpty && current.RepositoryScope.Order()
                                                                      .SequenceEqual(_binding.Request.RepositoryScope.Order());

    private static bool ValidDigest(string? digest) => digest is { Length: 64 } && digest.All(Uri.IsHexDigit);

    private static bool ValidCapsule(KnowledgeDependencyHandle? dependencies) => dependencies is not null && dependencies.SetId != Guid.Empty &&
                                                                                 ValidDigest(dependencies.BindingDigest) &&
                                                                                 !string.IsNullOrWhiteSpace(dependencies.ProtectedPayload) &&
                                                                                 _utf8.GetByteCount(dependencies.ProtectedPayload) <=
                                                                                 OctoberKnowledgeDisclosureLimits.MaximumProtectedPayloadUtf8Bytes;

    private bool ValidReferences(ImmutableArray<OctoberChatReference> references) => !references.IsDefault &&
                                                                                     references.Length <= OctoberDialogueLimits.MaximumReferences && references.All(reference =>
                                                                                         reference is not null && reference.Document is { } document &&
                                                                                         document.BatchId == _binding.Request.Publication.Knowledge.BatchId &&
                                                                                         document.PublicationId == _binding.Request.Publication.PublicationId &&
                                                                                         document.LogicalDocumentId != Guid.Empty && document.DocumentVersionId != Guid.Empty &&
                                                                                         (document.SectionId is null || _utf8.GetByteCount(document.SectionId) <=
                                                                                             OctoberDialogueLimits.MaximumSectionIdUtf8Bytes) &&
                                                                                         reference.SuccessfulChunks > 0 && reference.ContentBytes >= 0);
}
