using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.ChatExecution.October;

/// <summary>Internal owner-port consumer; never issues capsules, reconstructs contributors or persists dialogue.</summary>
internal sealed class OctoberChatDisclosureSession
{
    private readonly OctoberChatTurnBinding _binding;
    private readonly IOctoberKnowledgeDependencyAuthority _authority;
    private readonly Func<CancellationToken, Task<ReadSelectionRequest?>> _currentSelection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly Dictionary<DocumentHandle, ImmutableArray<KnowledgeDependencyHandle>> _contentPages = [];
    private readonly List<DocumentHandle> _reported = [];
    private KnowledgeDependencyHandle _dependencies;
    private ImmutableArray<DialogueConsumedTarget> _consumed = [];
    private KnowledgeDependencyStatus? _failure;
    private readonly Func<KnowledgeDependencyHandle, ImmutableArray<DialogueConsumedTarget>, bool>? _canRetain;

    internal OctoberChatDisclosureSession(OctoberChatTurnBinding binding, OctoberChatBudget budget,
        IOctoberKnowledgeDependencyAuthority authority,
        Func<CancellationToken, Task<ReadSelectionRequest?>> currentSelection,
        KnowledgeDependencyHandle initialDependencies,
        Func<KnowledgeDependencyHandle, ImmutableArray<DialogueConsumedTarget>, bool>? canRetain = null)
    {
        _binding = binding ?? throw new ArgumentNullException(nameof(binding));
        Budget = budget ?? throw new ArgumentNullException(nameof(budget));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _currentSelection = currentSelection ?? throw new ArgumentNullException(nameof(currentSelection));
        if (!ValidHandle(initialDependencies)) throw new ArgumentException("An explicit owner dependency capsule is required.", nameof(initialDependencies));
        _dependencies = initialDependencies;
        _canRetain = canRetain;
    }

    internal OctoberChatBudget Budget { get; }
    internal OctoberChatTurnBinding Binding => _binding;
    internal KnowledgeDependencyHandle InferenceDependencies { get { lock (_stateGate) return _dependencies; } }
    internal ImmutableArray<DialogueConsumedTarget> ConsumedReads { get { lock (_stateGate) return _consumed; } }
    internal KnowledgeDependencyStatus? FailureStatus { get { lock (_stateGate) return _failure; } }
    internal ImmutableArray<OctoberChatReference> ReportedReferences
    {
        get { lock (_stateGate) return _reported.Select(target => _consumed.Single(value => value.Reference.Document == target).Reference).ToImmutableArray(); }
    }

    internal Task ReauthorizeAsync(CancellationToken cancellationToken = default) => InScopeAsync(async token =>
    {
        await CheckAsync(_dependencies, token);
        return true;
    }, cancellationToken);

    // Trusted internal assertion that this page was admitted; the encoded facade uses
    // the overload below so byte accounting and state publication form one admission.
    internal Task AdmitPageAsync(KnowledgeDependencyHandle pageDependencies, DocumentHandle? contentTarget,
        CancellationToken cancellationToken = default) => AdmitAsync(pageDependencies, contentTarget, null, false, cancellationToken);

    internal Task AdmitEncodedPageAsync(KnowledgeDependencyHandle pageDependencies, DocumentHandle? contentTarget,
        int outputBytes, bool content, CancellationToken cancellationToken = default) =>
        AdmitAsync(pageDependencies, contentTarget, outputBytes, content, cancellationToken);

    private Task AdmitAsync(KnowledgeDependencyHandle pageDependencies, DocumentHandle? target, int? bytes, bool content,
        CancellationToken cancellationToken) => InScopeAsync(async token =>
    {
        if (!ValidHandle(pageDependencies)) throw Reject(KnowledgeDependencyStatus.InvalidProof);
        if (target is not null && (!Selected(target) ||
            !_contentPages.ContainsKey(target) && _contentPages.Count >= OctoberDialogueLimits.MaximumConsumedTargets))
            throw Reject(KnowledgeDependencyStatus.InvalidProof);
        ReadSelectionRequest selection = await CurrentAsync(token);
        KnowledgeDependencyResult merged = await _authority.MergeAsync(selection, [_dependencies, pageDependencies], token).WaitAsync(token);
        token.ThrowIfCancellationRequested();
        if (merged.Status != KnowledgeDependencyStatus.Succeeded) throw Reject(merged.Status);
        if (!ValidHandle(merged.Dependencies)) throw Reject(KnowledgeDependencyStatus.InvalidProof);

        ImmutableArray<KnowledgeDependencyHandle> pages = [];
        DialogueConsumedTarget? consumed = null;
        if (target is not null)
        {
            pages = (_contentPages.TryGetValue(target, out var previous) ? previous : []).Add(pageDependencies);
            selection = await CurrentAsync(token);
            KnowledgeConsumedContentResult built = await _authority.BuildConsumedContentAsync(selection, target, pages, token).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            KnowledgeConsumedContentReceipt receipt = AcceptReceipt(built, target);
            consumed = new(new(target, receipt.IsPartial, receipt.SuccessfulChunks, receipt.ContentBytes), receipt.Proof);
        }

        await CheckAsync(merged.Dependencies!, token);
        token.ThrowIfCancellationRequested();
        ImmutableArray<DialogueConsumedTarget> candidate = _consumed;
        if (target is not null)
        {
            int existing = -1;
            for (int i = 0; i < candidate.Length; i++) if (candidate[i].Reference.Document == target) { existing = i; break; }
            candidate = existing < 0 ? candidate.Add(consumed!) : candidate.SetItem(existing, consumed!);
        }
        if (_canRetain is not null && !_canRetain(merged.Dependencies!, candidate))
            throw Reject(KnowledgeDependencyStatus.BudgetExceeded);
        token.ThrowIfCancellationRequested();
        // There are no awaited operations after this commit. Failed accounting never
        // publishes an inference union or consumed proof for an undelivered result.
        if (bytes is { } length && !Budget.TryAddToolResultBytes(length, content))
            throw Reject(KnowledgeDependencyStatus.BudgetExceeded);
        lock (_stateGate)
        {
            _dependencies = merged.Dependencies!;
            if (target is not null)
            {
                _contentPages[target] = pages;
                _consumed = candidate;
            }
        }
        return true;
    }, cancellationToken);

    internal async Task<ImmutableArray<OctoberChatReference>?> ReportAsync(IEnumerable<DocumentHandle> documents,
        CancellationToken cancellationToken = default) => (await ReportCoreAsync(documents, false, cancellationToken))?.Result.References;

    internal Task<OctoberChatToolInvocation?> ReportEncodedAsync(IEnumerable<DocumentHandle> documents,
        CancellationToken cancellationToken = default) => ReportCoreAsync(documents, true, cancellationToken);

    private Task<OctoberChatToolInvocation?> ReportCoreAsync(IEnumerable<DocumentHandle> documents, bool encoded,
        CancellationToken cancellationToken) => InScopeAsync<OctoberChatToolInvocation?>(async token =>
    {
        DocumentHandle[] requested = documents.ToArray();
        await CheckAsync(_dependencies, token);
        if (requested.Any(target => target is null || !_contentPages.ContainsKey(target))) return null;
        DocumentHandle[] ordered = _reported.Concat(requested).Distinct().ToArray();
        var references = ImmutableArray.CreateBuilder<OctoberChatReference>();
        foreach (DocumentHandle target in ordered)
        {
            DialogueConsumedTarget consumed = _consumed.Single(value => value.Reference.Document == target);
            ReadSelectionRequest selection = await CurrentAsync(token);
            KnowledgeConsumedContentResult verified = await _authority.VerifyConsumedContentAsync(
                selection, consumed.Proof, _dependencies, token).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            KnowledgeConsumedContentReceipt receipt = AcceptReceipt(verified, target);
            var reference = new OctoberChatReference(target, receipt.IsPartial, receipt.SuccessfulChunks, receipt.ContentBytes);
            if (reference != consumed.Reference || receipt.Proof.BindingDigest != consumed.Proof.BindingDigest)
                throw Reject(KnowledgeDependencyStatus.InvalidProof);
            references.Add(reference);
        }
        await CheckAsync(_dependencies, token);
        token.ThrowIfCancellationRequested();
        var result = new OctoberChatToolResult(OctoberChatToolStatus.Succeeded, References: references.ToImmutable());
        ImmutableArray<byte> output = encoded ? OctoberWikiTools.SerializeResultUtf8(result) : [];
        if (encoded && !Budget.TryAddToolResultBytes(output.Length)) throw Reject(KnowledgeDependencyStatus.BudgetExceeded);
        lock (_stateGate)
        {
            _reported.Clear();
            _reported.AddRange(ordered);
        }
        return new(result, output);
    }, cancellationToken);

    internal InvalidOperationException Reject(KnowledgeDependencyStatus status)
    {
        lock (_stateGate) _failure ??= status == KnowledgeDependencyStatus.Succeeded || !Enum.IsDefined(status)
            ? KnowledgeDependencyStatus.InvalidProof : status;
        Budget.StopExecution();
        return new DisclosureException();
    }

    internal static bool ValidHandle(KnowledgeDependencyHandle? handle) => handle is not null && handle.SetId != Guid.Empty &&
        ValidDigest(handle.BindingDigest) && !string.IsNullOrWhiteSpace(handle.ProtectedPayload);

    private static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    private KnowledgeConsumedContentReceipt AcceptReceipt(KnowledgeConsumedContentResult result, DocumentHandle target)
    {
        if (result.Status != KnowledgeDependencyStatus.Succeeded) throw Reject(result.Status);
        if (result.Receipt is not { } receipt || receipt.Document != target || receipt.SuccessfulChunks <= 0 ||
            receipt.ContentBytes < 0 || !ValidHandle(receipt.Dependencies) || receipt.Proof is not { } proof ||
            proof.ReceiptId == Guid.Empty || !ValidDigest(proof.BindingDigest) || string.IsNullOrWhiteSpace(proof.ProtectedPayload))
            throw Reject(KnowledgeDependencyStatus.InvalidProof);
        return receipt;
    }

    private async Task CheckAsync(KnowledgeDependencyHandle dependencies, CancellationToken token)
    {
        ReadSelectionRequest selection = await CurrentAsync(token);
        KnowledgeDependencyStatus status = await _authority.ReauthorizeAsync(selection, [dependencies], token).WaitAsync(token);
        token.ThrowIfCancellationRequested();
        if (status != KnowledgeDependencyStatus.Succeeded) throw Reject(status);
    }

    private async Task<ReadSelectionRequest> CurrentAsync(CancellationToken token)
    {
        ReadSelectionRequest? current = await _currentSelection(token).WaitAsync(token);
        token.ThrowIfCancellationRequested();
        ReadSelectionRequest original = _binding.Request;
        if (current is null || current.Subject is null || current.Subject.SubjectId != original.Subject.SubjectId ||
            string.IsNullOrWhiteSpace(current.Subject.AuthenticationBinding) || current.Publication != original.Publication ||
            current.Mode != original.Mode || current.Purpose != KnowledgePurpose.Chat || original.Purpose != KnowledgePurpose.Chat ||
            current.RepositoryScope.IsDefault || current.RepositoryScope.Any(value => value == Guid.Empty) ||
            current.RepositoryScope.Distinct().Count() != current.RepositoryScope.Length ||
            !current.RepositoryScope.ToHashSet().SetEquals(original.RepositoryScope))
            throw Reject(KnowledgeDependencyStatus.Denied);
        return current;
    }

    private bool Selected(DocumentHandle target) => target.BatchId == _binding.Request.Publication.Knowledge.BatchId &&
        target.PublicationId == _binding.Request.Publication.PublicationId && target.LogicalDocumentId != Guid.Empty && target.DocumentVersionId != Guid.Empty;

    private async Task<T> InScopeAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        using var deadline = Budget.CreateDeadlineCancellation();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Budget.OriginalCancellationToken, deadline.Token);
        bool entered = false;
        try
        {
            await _gate.WaitAsync(linked.Token);
            entered = true;
            if (FailureStatus is not null) throw new DisclosureException();
            if (Budget.IsStopped) throw Reject(KnowledgeDependencyStatus.BudgetExceeded);
            return await operation(linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            _ = Reject(cancellationToken.IsCancellationRequested || Budget.OriginalCancellationToken.IsCancellationRequested
                ? KnowledgeDependencyStatus.Cancelled : KnowledgeDependencyStatus.BudgetExceeded);
            throw;
        }
        catch (DisclosureException) { throw; }
        catch (Exception) { throw Reject(KnowledgeDependencyStatus.AuthorityUnavailable); }
        finally { if (entered) _gate.Release(); }
    }

    private sealed class DisclosureException : InvalidOperationException
    {
        internal DisclosureException() : base("Canonical Chat disclosure is unavailable.") { }
    }
}
