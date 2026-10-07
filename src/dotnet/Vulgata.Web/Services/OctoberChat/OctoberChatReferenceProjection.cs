using System.Collections.Immutable;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Web.Explore;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>Server-acquired exact destination. Reference coverage is the original saved owner fact.</summary>
public sealed record OctoberChatReferenceRow(OctoberChatReference Reference, string Title, string Link);

/// <summary>Host-only display handoff, not a reusable permission or browser-supplied result.</summary>
public sealed class OctoberChatReferenceFooterState
{
    internal OctoberChatReferenceFooterState(bool isUnavailable, ImmutableArray<OctoberChatReferenceRow> rows,
        KnowledgeDependencyHandle? dependencies)
    {
        IsUnavailable = isUnavailable;
        Rows = rows;
        Dependencies = dependencies;
    }

    public bool IsUnavailable { get; }
    public ImmutableArray<OctoberChatReferenceRow> Rows { get; }
    [JsonIgnore] public KnowledgeDependencyHandle? Dependencies { get; }
}

/// <summary>
/// One saved-turn display attempt and attachment. Prepare and Handoff share one
/// original ten-second lifetime; no retry, continuation or generation budget is used.
/// Candidate data stays private until fresh authorization at the actual handoff.
/// The trusted sink must recheck its token and current attachment at UI application.
/// </summary>
public sealed class OctoberChatReferenceProjection : IAsyncDisposable
{
    private static readonly TimeSpan _duration = TimeSpan.FromSeconds(10);
    private static readonly UTF8Encoding _utf8 = new(false, true);
    private const int MaximumReads = 16;
    private const int MaximumResultBytes = 65_536;
    private const int MaximumAggregateBytes = 262_144;
    private readonly object _gate = new();
    private readonly OctoberChatTurnBinding _binding;
    private readonly DialogueTerminalProjection _saved;
    private readonly Guid _attachmentId;
    private readonly Func<Guid, bool> _isCurrentAttachment;
    private readonly IOctoberChatContextProvider _context;
    private readonly IOctoberKnowledgeReader _reader;
    private readonly IOctoberKnowledgeDisclosedReader _disclosed;
    private readonly IOctoberKnowledgeDependencyAuthority _dependencies;
    private readonly TimeProvider _clock;
    private readonly ExploreGraphOutputEncoder _encoder = new();
    private readonly CancellationTokenSource _closing = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HashSet<Task> _retained = [];
    private readonly List<KnowledgeDependencyHandle> _capsules = [];
    private CancellationTokenSource? _deadline;
    private CancellationTokenSource? _lifetime;
    private KnowledgeDependencyHandle? _merged;
    private ImmutableArray<OctoberChatReferenceRow> _rows = [];
    private long _startedAt;
    private int _activeStages;
    private int _prepareUsed;
    private int _handoffUsed;
    private int _disposed;
    private bool _prepareDone;
    private bool _ready;
    private bool _suppression;
    private bool _contextLost;
    private bool _sealed;
    public OctoberChatReferenceProjection(OctoberChatTurnBinding binding, DialogueTerminalProjection saved,
        Guid attachmentId, Func<Guid, bool> isCurrentAttachment, IOctoberChatContextProvider context,
        IOctoberKnowledgeReader reader, IOctoberKnowledgeDisclosedReader disclosedReader,
        IOctoberKnowledgeDependencyAuthority dependencies)
        : this(binding, saved, attachmentId, isCurrentAttachment, context, reader, disclosedReader, dependencies, TimeProvider.System)
    {
    }

    internal OctoberChatReferenceProjection(OctoberChatTurnBinding binding, DialogueTerminalProjection saved,
        Guid attachmentId, Func<Guid, bool> isCurrentAttachment, IOctoberChatContextProvider context,
        IOctoberKnowledgeReader reader, IOctoberKnowledgeDisclosedReader disclosedReader,
        IOctoberKnowledgeDependencyAuthority dependencies, TimeProvider clock)
    {
        _binding = binding ?? throw new ArgumentNullException(nameof(binding));
        _saved = saved ?? throw new ArgumentNullException(nameof(saved));
        _attachmentId = attachmentId;
        _isCurrentAttachment = isCurrentAttachment ?? throw new ArgumentNullException(nameof(isCurrentAttachment));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _disclosed = disclosedReader ?? throw new ArgumentNullException(nameof(disclosedReader));
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_sealed || _prepareUsed++ != 0) return;
            _activeStages++;
            _startedAt = _clock.GetTimestamp();
            _deadline = new(_duration, _clock);
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _deadline.Token, _closing.Token);
        }
        try
        {
            CancellationToken token = _lifetime.Token;
            if (!MatchesSavedIdentity()) { _contextLost = true; return; }
            if (!ValidSaved()) return;
            _capsules.Add(_saved.InferenceDependencies!);
            _merged = _saved.InferenceDependencies;
            ReadSelectionRequest? current = await CurrentAsync(token);
            if (current is null) return;
            if (!AcceptAuthority(await AwaitAsync(() => _dependencies.ReauthorizeAsync(current, [_merged!], token), token))) return;
            if (_saved.References.Length > MaximumReads) return;
            if (_saved.References.IsEmpty) { _ready = true; return; }
            ReadSelectionResult opened = await AwaitAsync(() => _reader.OpenReadSelectionAsync(current, token), token);
            if (opened is not { Status: KnowledgeReadStatus.Success, Selection: { } selection } ||
                string.IsNullOrWhiteSpace(selection.Token)) return;
            List<OctoberChatReferenceRow> rows = [];
            long remaining = MaximumAggregateBytes;
            foreach (OctoberChatReference reference in _saved.References)
            {
                if (!await SameContextAsync(current, token)) return;
                var budget = new KnowledgeReadOutputBudget((int)Math.Min(MaximumResultBytes, remaining), remaining, _encoder);
                KnowledgeDisclosureReadResult<ContentPage> read = await AwaitAsync(
                    () => _disclosed.ReadExactAsync(new(selection, reference.Document), budget, token), token);
                if (read is not { Status: KnowledgeReadStatus.Success, Disclosure: { } envelope } ||
                    !ValidPage(envelope, reference.Document, remaining)) return;
                remaining -= envelope.OutputUtf8.Length;
                // Retain every valid acquired capsule, even if its union operation
                // later fails. No guessed contributor reconstruction or empty root.
                _capsules.Add(envelope.Dependencies);
                if (!await SameContextAsync(current, token)) return;
                KnowledgeDependencyResult union = await AwaitAsync(() => _dependencies.MergeAsync(current, _capsules.ToImmutableArray(), token), token);
                if (!AcceptAuthority(union.Status) || !ValidCapsule(union.Dependencies)) return;
                _merged = union.Dependencies;
                rows.Add(new(reference, envelope.Page.Document!.Title, ExactDocumentLinkBuilder.Build(reference.Document)));
                if (remaining == 0 && rows.Count < _saved.References.Length) return;
            }
            token.ThrowIfCancellationRequested();
            _rows = rows.ToImmutableArray();
            _ready = true;
        }
        catch (Exception) { _ready = false; }
        finally
        {
            lock (_gate) { _prepareDone = true; _activeStages--; CompleteIfSettled(); }
        }
    }

    public async Task HandoffAsync(Func<OctoberChatReferenceFooterState, CancellationToken, ValueTask> apply,
        Func<CancellationToken, ValueTask> suppressDependentDisplay, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(suppressDependentDisplay);
        lock (_gate)
        {
            if (_sealed || !_prepareDone || _handoffUsed++ != 0) return;
            _activeStages++;
        }
        try
        {
            // No renewal across the inter-stage queue delay, including with a
            // controllable monotonic clock whose timer callback has not run yet.
            if (_lifetime is null || _lifetime.IsCancellationRequested || _clock.GetElapsedTime(_startedAt) >= _duration) return;
            using var caller = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            CancellationToken token = caller.Token;
            ReadSelectionRequest? current = await CurrentAsync(token);
            if (current is null) return;
            if (_capsules.Count > 0)
            {
                ImmutableArray<KnowledgeDependencyHandle> all = _merged is null
                    ? _capsules.ToImmutableArray() : _capsules.Append(_merged).Distinct().ToImmutableArray();
                if (!AcceptAuthority(await AwaitAsync(() => _dependencies.ReauthorizeAsync(current, all, token), token))) _ready = false;
            }
            else _ready = false;
            if (!await SameContextAsync(current, token)) return;
            if (_suppression)
            {
                await AwaitAsync(() => suppressDependentDisplay(token).AsTask(), token);
                if (!await SameContextAsync(current, token)) return;
            }
            var state = _ready && !_suppression && _merged is not null
                ? new OctoberChatReferenceFooterState(false, _rows, _merged)
                : new OctoberChatReferenceFooterState(true, [], null);
            token.ThrowIfCancellationRequested();
            if (!IsCurrent()) return;
            // The supplied callback is the UI handoff, not permission to queue an
            // unchecked later apply. Its actual task stays owned if it ignores CT.
            await AwaitAsync(() => apply(state, token).AsTask(), token);
        }
        catch (Exception) { }
        finally
        {
            Seal();
            lock (_gate) { _activeStages--; CompleteIfSettled(); }
        }
    }

    /// <summary>Actual retained task settlement, not successful display or permission.</summary>
    public Task Completion => _completion.Task;
    public Task SealAndWaitForSettlementAsync() { Seal(); return Completion; }
    public async ValueTask DisposeAsync()
    {
        await SealAndWaitForSettlementAsync();
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime?.Dispose();
        _deadline?.Dispose();
        _closing.Dispose();
    }

    private void Seal()
    {
        lock (_gate)
        {
            if (_sealed) return;
            _sealed = true;
            Retain(Task.Run(() => _closing.CancelAsync()));
            CompleteIfSettled();
        }
    }

    private void Retain(Task task)
    {
        lock (_gate) _retained.Add(task);
        _ = ObserveAsync(task);
    }
    private async Task ObserveAsync(Task task)
    {
        try { await task; } catch (Exception) { }
        finally { lock (_gate) { _retained.Remove(task); CompleteIfSettled(); } }
    }
    private void CompleteIfSettled()
    {
        if (_sealed && _activeStages == 0 && _retained.Count == 0) _completion.TrySetResult();
    }
    private Task<T> Start<T>(Func<Task<T>> operation, CancellationToken token)
    {
        lock (_gate)
        {
            CheckStart(token);
            Task<T> task = operation();
            Retain(task);
            return task;
        }
    }
    private Task Start(Func<Task> operation, CancellationToken token)
    {
        lock (_gate)
        {
            CheckStart(token);
            Task task = operation();
            Retain(task);
            return task;
        }
    }
    private void CheckStart(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_sealed || _clock.GetElapsedTime(_startedAt) >= _duration) throw new OperationCanceledException(token);
    }
    private async Task<T> AwaitAsync<T>(Func<Task<T>> operation, CancellationToken token)
    {
        // Admission and actual task retention share Seal's gate. Merely passing a
        // cancelled token to a task already invoked would not prevent a late call.
        T result = await Start(operation, token).WaitAsync(token);
        lock (_gate) CheckStart(token);
        return result;
    }
    private async Task AwaitAsync(Func<Task> operation, CancellationToken token)
    {
        await Start(operation, token).WaitAsync(token);
        lock (_gate) CheckStart(token);
    }

    private bool IsCurrent()
    {
        try
        {
            lock (_gate) if (_sealed || _contextLost) return false;
            if (_attachmentId != Guid.Empty && _isCurrentAttachment(_attachmentId))
            {
                // A trusted attachment predicate may itself close this instance.
                lock (_gate) return !_sealed && !_contextLost;
            }
        }
        catch (Exception) { }
        _contextLost = true;
        return false;
    }
    private async Task<ReadSelectionRequest?> CurrentAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!IsCurrent()) return null;
        ReadSelectionRequest? current = await AwaitAsync(() => _context.GetCurrentAsync(token), token);
        if (!MatchesContext(current) || !IsCurrent()) { _contextLost = true; return null; }
        return current;
    }
    private async Task<bool> SameContextAsync(ReadSelectionRequest before, CancellationToken token)
    {
        ReadSelectionRequest? after = await CurrentAsync(token);
        if (after is null || after.Subject.AuthenticationBinding != before.Subject.AuthenticationBinding)
        { _contextLost = true; return false; }
        return true;
    }
    private bool MatchesContext(ReadSelectionRequest? current) => current is not null && current.Subject is not null &&
        current.Purpose == KnowledgePurpose.Chat && current.Subject.SubjectId == _binding.Request.Subject.SubjectId &&
        !string.IsNullOrWhiteSpace(current.Subject.AuthenticationBinding) && current.Publication == _binding.Request.Publication &&
        current.Mode == _binding.Request.Mode && !current.RepositoryScope.IsDefaultOrEmpty &&
        current.RepositoryScope.Order().SequenceEqual(_binding.Request.RepositoryScope.Order());

    private bool AcceptAuthority(KnowledgeDependencyStatus status)
    {
        if (status is KnowledgeDependencyStatus.Denied or KnowledgeDependencyStatus.AuthorityUnavailable) _suppression = true;
        return status == KnowledgeDependencyStatus.Succeeded;
    }
    private bool MatchesSavedIdentity() => _binding.ConversationId != Guid.Empty && _binding.ContextId != Guid.Empty &&
        _saved.Conversation.ConversationId == _binding.ConversationId && _saved.Conversation.ContextId == _binding.ContextId &&
        _saved.TurnId == _binding.TurnId && _saved.TurnId != Guid.Empty && _saved.Sequence > 0 &&
        _saved.Conversation.OwnerSubjectId == _binding.Request.Subject.SubjectId && _saved.Conversation.Publication == _binding.Request.Publication &&
        _saved.Conversation.Mode == _binding.Request.Mode && !_saved.Conversation.RepositoryScope.IsDefault &&
        _saved.Conversation.RepositoryScope.Order().SequenceEqual(_binding.Request.RepositoryScope.Order()) && MatchesContext(_binding.Request);

    private bool ValidSaved() => Enum.IsDefined(_saved.TerminalState) && Enum.IsDefined(_saved.Reason) && ValidCapsule(_saved.InferenceDependencies) &&
        !_saved.References.IsDefault && _saved.References.Length <= OctoberDialogueLimits.MaximumReferences &&
        _saved.References.All(reference => reference is not null && reference.Document is not null &&
            reference.Document.BatchId == _binding.Request.Publication.Knowledge.BatchId &&
            reference.Document.PublicationId == _binding.Request.Publication.PublicationId &&
            reference.SuccessfulChunks > 0 && reference.ContentBytes >= 0 && ExactDocumentLinkBuilder.Build(reference.Document).Length > 0) &&
        _saved.References.Select(reference => reference.Document).Distinct().Count() == _saved.References.Length;

    private bool ValidPage(KnowledgeDisclosedPage<ContentPage> envelope, DocumentHandle exact, long remaining)
    {
        if (envelope.Page is not { Status: KnowledgeReadStatus.Success, Document: { } document } || document.Handle != exact ||
            !Enum.IsDefined(document.Kind) || _binding.Request.Mode == KnowledgeMode.Business && document.Kind != DocumentKind.BusinessLogic ||
            document.Title is null || !ValidCapsule(envelope.Dependencies) || envelope.OutputUtf8.IsDefaultOrEmpty ||
            envelope.OutputUtf8.Length > MaximumResultBytes || envelope.OutputUtf8.Length > remaining ||
            envelope.OutputProfileId != _encoder.ProfileId) return false;
        _ = _utf8.GetByteCount(document.Title);
        ImmutableArray<byte> expected = _encoder.EncodeContent(envelope.Page);
        return envelope.OutputUtf8.AsSpan().SequenceEqual(expected.AsSpan()) &&
            envelope.OutputDigest == Convert.ToHexString(SHA256.HashData(envelope.OutputUtf8.AsSpan())) &&
            envelope.PageDigest == ExploreGraphOutputEncoder.ComputePageDigest(envelope.Page);
    }

    private static bool ValidCapsule(KnowledgeDependencyHandle? capsule) => capsule is not null && capsule.SetId != Guid.Empty &&
        capsule.BindingDigest is { Length: 64 } && capsule.BindingDigest.All(Uri.IsHexDigit) &&
        !string.IsNullOrWhiteSpace(capsule.ProtectedPayload) &&
        _utf8.GetByteCount(capsule.ProtectedPayload) <= OctoberKnowledgeDisclosureLimits.MaximumProtectedPayloadUtf8Bytes;
}
