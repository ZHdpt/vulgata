using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Self-contained owner capsules: no read database, lasting permission or consumer-supplied contributor issuance.</summary>
public sealed class OctoberKnowledgeDependencyAuthority : IOctoberKnowledgeDependencyAuthority
{
    private const string DependencyDomain = "october.knowledge.dependencies.v1";
    private const string ConsumedDomain = "october.knowledge.consumed-content.v1";
    private readonly IOctoberKnowledgeDisclosurePolicy _policy;
    private readonly IOctoberKnowledgeDisclosureProtector _protector;
    public OctoberKnowledgeDependencyAuthority(IOctoberKnowledgeDisclosurePolicy policy, IOctoberKnowledgeDisclosureProtector protector)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public Task<KnowledgeDependencyResult> CreateNoKnowledgeRootAsync(ReadSelectionRequest selection,
        string initialInputDigest, CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        if (!OctoberKnowledgeDisclosureCodec.IsDigest(initialInputDigest)) throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
        var set = new DisclosureSet(OctoberKnowledgeDisclosureCodec.Binding(selection), [new("root", initialInputDigest, "", "", "", [], null)]);
        await CheckAsync(selection, set, token);
        return new KnowledgeDependencyResult(KnowledgeDependencyStatus.Succeeded, Seal(set));
    }, status => new KnowledgeDependencyResult(status, null), cancellationToken);

    public Task<KnowledgeDependencyResult> MergeAsync(ReadSelectionRequest selection,
        ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        var set = OpenAll(selection, dependencies, token);
        await CheckAsync(selection, set, token);
        return new KnowledgeDependencyResult(KnowledgeDependencyStatus.Succeeded, Seal(set));
    }, status => new KnowledgeDependencyResult(status, null), cancellationToken);

    public Task<KnowledgeDependencyStatus> ReauthorizeAsync(ReadSelectionRequest selection,
        ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        var set = OpenAll(selection, dependencies, token);
        await CheckAsync(selection, set, token);
        return KnowledgeDependencyStatus.Succeeded;
    }, status => status, cancellationToken);

    public Task<KnowledgeConsumedContentResult> BuildConsumedContentAsync(ReadSelectionRequest selection,
        DocumentHandle document, ImmutableArray<KnowledgeDependencyHandle> admittedContentPages,
        CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        var dependencies = OpenAll(selection, admittedContentPages, token);
        if (dependencies.Origins.Any(origin => origin.Kind != "content" || origin.Content?.Document != document)) throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
        var consumed = NormalizeConsumed(new(dependencies.Binding, document, dependencies.Origins
            .Select(origin => new ConsumedSlice(origin.Content!, origin.Groups)).ToImmutableArray()));
        await CheckAsync(selection, dependencies, token);
        return new KnowledgeConsumedContentResult(KnowledgeDependencyStatus.Succeeded, Receipt(consumed, dependencies));
    }, status => new KnowledgeConsumedContentResult(status, null), cancellationToken);

    public Task<KnowledgeConsumedContentResult> VerifyConsumedContentAsync(ReadSelectionRequest selection,
        KnowledgeConsumedContentHandle proof, KnowledgeDependencyHandle inferenceDependencies,
        CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        if (proof is null) throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
        var consumed = NormalizeConsumed(Open<ConsumedSet>(proof.ReceiptId, proof.BindingDigest, proof.ProtectedPayload,
            "consumed-content", ConsumedDomain, _protector.UnprotectConsumedContent));
        RequireBinding(selection, consumed.Binding);
        var inference = OpenAll(selection, [inferenceDependencies], token);
        var candidates = inference.Origins.Where(origin => origin.Kind == "content")
            .Select(origin => new ConsumedSlice(origin.Content!, origin.Groups)).ToArray();
        if (consumed.Slices.Any(slice => !candidates.Any(candidate => OctoberKnowledgeDisclosureCodec.Canonical(candidate) == OctoberKnowledgeDisclosureCodec.Canonical(slice))))
            throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
        await CheckAsync(selection, inference, token);
        var contentOrigins = inference.Origins.Where(origin => origin.Content is not null && consumed.Slices.Any(slice =>
            OctoberKnowledgeDisclosureCodec.Canonical(new ConsumedSlice(origin.Content, origin.Groups)) == OctoberKnowledgeDisclosureCodec.Canonical(slice))).ToImmutableArray();
        return new KnowledgeConsumedContentResult(KnowledgeDependencyStatus.Succeeded, Receipt(consumed, new(inference.Binding, contentOrigins), proof));
    }, status => new KnowledgeConsumedContentResult(status, null), cancellationToken);

    internal KnowledgeDependencyHandle Issue(ReadSelectionRequest selection, string kind, string pageDigest, string outputProfile,
        string outputDigest, IEnumerable<DisclosureRequest> authorizedDisclosures, DisclosureContent? content) =>
        Seal(new(OctoberKnowledgeDisclosureCodec.Binding(selection), [new(kind, "", pageDigest, outputProfile, outputDigest,
            OctoberKnowledgeDisclosureCodec.Groups(authorizedDisclosures), content)]));

    private DisclosureSet OpenAll(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> handles, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (handles.IsDefaultOrEmpty) throw new DisclosureFailure(KnowledgeDependencyStatus.UnknownLineage);
        // A bounded input batch cannot evade union limits by sending many repeated large envelopes.
        if (handles.Length > OctoberKnowledgeDisclosureLimits.MaximumOriginGroups) throw new DisclosureFailure(KnowledgeDependencyStatus.BudgetExceeded);
        var binding = OctoberKnowledgeDisclosureCodec.Binding(selection);
        var origins = ImmutableArray.CreateBuilder<DisclosureOrigin>();
        foreach (var handle in handles)
        {
            token.ThrowIfCancellationRequested();
            if (handle is null) throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
            var set = OctoberKnowledgeDisclosureCodec.Normalize(Open<DisclosureSet>(handle.SetId, handle.BindingDigest, handle.ProtectedPayload,
                "dependencies", DependencyDomain, _protector.UnprotectDependency));
            token.ThrowIfCancellationRequested();
            RequireBinding(selection, set.Binding);
            origins.AddRange(set.Origins);
        }
        var result = OctoberKnowledgeDisclosureCodec.Normalize(new(binding, origins.ToImmutable()));
        OctoberKnowledgeDisclosureCodec.LimitText(OctoberKnowledgeDisclosureCodec.Canonical(result), OctoberKnowledgeDisclosureLimits.MaximumCanonicalPayloadUtf8Bytes);
        return result;
    }

    private KnowledgeDependencyHandle Seal(DisclosureSet value)
    {
        var set = OctoberKnowledgeDisclosureCodec.Normalize(value);
        string digest = OctoberKnowledgeDisclosureCodec.Digest(DependencyDomain, set);
        Guid id = Guid.NewGuid();
        return new(id, digest, Protect(new DisclosureSeal<DisclosureSet>(1, "dependencies", id, digest, set), _protector.ProtectDependency));
    }
    private string Protect<T>(DisclosureSeal<T> value, Func<string, string> protect)
    {
        string plaintext = OctoberKnowledgeDisclosureCodec.Canonical(value);
        OctoberKnowledgeDisclosureCodec.LimitText(plaintext, OctoberKnowledgeDisclosureLimits.MaximumCanonicalPayloadUtf8Bytes);
        string protectedPayload = protect(plaintext);
        OctoberKnowledgeDisclosureCodec.LimitText(protectedPayload, OctoberKnowledgeDisclosureLimits.MaximumProtectedPayloadUtf8Bytes);
        return protectedPayload;
    }
    private static T Open<T>(Guid id, string digest, string protectedPayload, string kind, string domain, Func<string, string> unprotect)
    {
        if (id == Guid.Empty || !OctoberKnowledgeDisclosureCodec.IsDigest(digest) || string.IsNullOrWhiteSpace(protectedPayload)) throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
        OctoberKnowledgeDisclosureCodec.LimitText(protectedPayload, OctoberKnowledgeDisclosureLimits.MaximumProtectedPayloadUtf8Bytes);
        try
        {
            string plaintext = unprotect(protectedPayload);
            OctoberKnowledgeDisclosureCodec.LimitText(plaintext, OctoberKnowledgeDisclosureLimits.MaximumCanonicalPayloadUtf8Bytes);
            var seal = OctoberKnowledgeDisclosureCodec.Read<DisclosureSeal<T>>(plaintext);
            if (seal is null || seal.Version != 1 || seal.Kind != kind || seal.Id != id || seal.BindingDigest != digest || seal.Value is null ||
                OctoberKnowledgeDisclosureCodec.Canonical(seal) != plaintext ||
                OctoberKnowledgeDisclosureCodec.Digest(domain, seal.Value) != digest) throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
            return seal.Value;
        }
        catch (DisclosureFailure) { throw; }
        catch (Exception) { throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof); }
    }
    private static void RequireBinding(ReadSelectionRequest selection, DisclosureBinding binding)
    {
        if (OctoberKnowledgeDisclosureCodec.Canonical(OctoberKnowledgeDisclosureCodec.Binding(selection)) != OctoberKnowledgeDisclosureCodec.Canonical(binding))
            throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
    }
    private async Task CheckAsync(ReadSelectionRequest selection, DisclosureSet set, CancellationToken token)
    {
        RequireBinding(selection, set.Binding);
        await CheckPolicyAsync(new(selection, [], null), token);
        foreach (var group in set.Origins.SelectMany(origin => origin.Groups).DistinctBy(OctoberKnowledgeDisclosureCodec.Canonical))
            await CheckPolicyAsync(new(selection, group.Contributors, group.Kind), token);
        // Recheck subject eligibility at the actual return boundary, after all contributor lookups.
        await CheckPolicyAsync(new(selection, [], null), token);
    }
    private async Task CheckPolicyAsync(DisclosureRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var status = await _policy.CheckAsync(request, token).WaitAsync(token);
        token.ThrowIfCancellationRequested();
        if (status != DisclosureStatus.Allowed) throw new DisclosureFailure(status == DisclosureStatus.Denied ? KnowledgeDependencyStatus.Denied : KnowledgeDependencyStatus.AuthorityUnavailable);
    }

    private static ConsumedSet NormalizeConsumed(ConsumedSet value)
    {
        if (value.Slices.IsDefaultOrEmpty || value.Slices.Length > OctoberKnowledgeDisclosureLimits.MaximumOriginGroups) throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
        var ordered = value.Slices.OrderBy(slice => slice.Content.Start).ThenBy(slice => slice.Content.End).ToArray();
        var first = ordered[0].Content;
        var slices = ImmutableArray.CreateBuilder<ConsumedSlice>();
        foreach (var slice in ordered)
        {
            var content = slice.Content;
            if (content.Document != value.Document || content.ProjectionDigest != first.ProjectionDigest || content.TotalLength != first.TotalLength ||
                OctoberKnowledgeDisclosureCodec.Canonical(content.Coverage) != OctoberKnowledgeDisclosureCodec.Canonical(first.Coverage) ||
                content.Start < 0 || content.End <= content.Start || content.End > content.TotalLength || content.ContentBytes < 1 ||
                !OctoberKnowledgeDisclosureCodec.IsDigest(content.ChunkDigest)) throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
            if (slices.Count != 0 && content.Start < slices[^1].Content.End)
            {
                if (OctoberKnowledgeDisclosureCodec.Canonical(slice) != OctoberKnowledgeDisclosureCodec.Canonical(slices[^1])) throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
                continue;
            }
            slices.Add(slice);
        }
        return value with { Slices = slices.ToImmutable() };
    }
    private KnowledgeConsumedContentReceipt Receipt(ConsumedSet consumed, DisclosureSet dependencies, KnowledgeConsumedContentHandle? existingProof = null)
    {
        string digest = OctoberKnowledgeDisclosureCodec.Digest(ConsumedDomain, consumed);
        Guid id = existingProof?.ReceiptId ?? Guid.NewGuid();
        var proof = existingProof ?? new KnowledgeConsumedContentHandle(id, digest,
            Protect(new DisclosureSeal<ConsumedSet>(1, "consumed-content", id, digest, consumed), _protector.ProtectConsumedContent));
        int position = 0;
        bool partial = false;
        long bytes = 0;
        foreach (var slice in consumed.Slices)
        {
            partial |= slice.Content.Start != position;
            position = slice.Content.End;
            bytes = checked(bytes + slice.Content.ContentBytes);
        }
        partial |= position != consumed.Slices[0].Content.TotalLength;
        return new(proof, consumed.Document, partial, consumed.Slices.Length, bytes, consumed.Slices[0].Content.Coverage, Seal(dependencies));
    }
    private static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> execute, Func<KnowledgeDependencyStatus, T> failure, CancellationToken caller)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try { return await execute(deadline.Token).WaitAsync(deadline.Token); }
        catch (OperationCanceledException) { return failure(caller.IsCancellationRequested ? KnowledgeDependencyStatus.Cancelled : KnowledgeDependencyStatus.AuthorityUnavailable); }
        catch (DisclosureFailure exception) { return failure(exception.Status); }
        catch (Exception) { return failure(KnowledgeDependencyStatus.AuthorityUnavailable); }
    }
}
