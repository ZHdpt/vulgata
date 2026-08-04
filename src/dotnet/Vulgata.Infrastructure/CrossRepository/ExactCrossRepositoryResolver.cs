using System.Text;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.CrossRepository;

public sealed class ExactCrossRepositoryResolver : IExactCrossRepositoryResolver
{
    private const decimal ExactMatchConfidence = 1.0000m;

    public Task<ExactResolutionDecisionBatch> ResolveAsync(
        ExactResolutionProfile profile,
        IReadOnlyList<ConsumerDispatchCandidate> consumerCandidates,
        IReadOnlyList<ProviderEndpointCandidate> providerCandidates,
        IReadOnlyList<ExactResolutionDecision> unsupportedDecisions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(consumerCandidates);
        ArgumentNullException.ThrowIfNull(providerCandidates);
        ArgumentNullException.ThrowIfNull(unsupportedDecisions);
        cancellationToken.ThrowIfCancellationRequested();

        var decisions = new List<ExactResolutionDecision>(
            profile.Occurrences.Count + unsupportedDecisions.Count);
        foreach (ResolutionOccurrence occurrence in profile.Occurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            decisions.Add(ResolveOccurrence(
                profile,
                occurrence,
                consumerCandidates,
                providerCandidates));
        }

        decisions.AddRange(unsupportedDecisions);
        ExactResolutionDecision[] ordered =
        [
            .. decisions.OrderBy(decision => decision.Identity, StringComparer.Ordinal),
        ];
        return Task.FromResult(ExactResolutionDecisionBatch.Create(profile, ordered));
    }

    internal static string NormalizeEndpointIdentifier(
        string rawIdentifier,
        string requiredPrefix)
    {
        ArgumentNullException.ThrowIfNull(rawIdentifier);
        ArgumentNullException.ThrowIfNull(requiredPrefix);
        string normalized = rawIdentifier.Normalize(NormalizationForm.FormC)
            .Trim(' ', '\t', '\r', '\n');
        if (requiredPrefix.Length > 0)
        {
            if (!normalized.StartsWith(requiredPrefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The endpoint identifier does not satisfy the configured normalization rule.");
            }

            normalized = normalized[requiredPrefix.Length..];
            if (normalized.StartsWith(requiredPrefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The endpoint identifier does not satisfy the configured normalization rule.");
            }
        }

        if (normalized.Length == 0 ||
            Encoding.UTF8.GetByteCount(normalized) > ExactResolutionLimits.MaxEndpointUtf8Bytes ||
            normalized.Any(char.IsControl))
        {
            throw new InvalidDataException(
                "The endpoint identifier does not satisfy the configured normalization rule.");
        }

        return normalized;
    }

    private static ExactResolutionDecision ResolveOccurrence(
        ExactResolutionProfile profile,
        ResolutionOccurrence occurrence,
        IReadOnlyList<ConsumerDispatchCandidate> consumerCandidates,
        IReadOnlyList<ProviderEndpointCandidate> providerCandidates)
    {
        ProfileRepository consumerRepository = FindRepository(profile, occurrence.ConsumerAlias);
        ProfileRepository providerRepository = FindRepository(profile, occurrence.ProviderAlias);
        ConsumerDispatchCandidate[] eligibleConsumers =
        [
            .. consumerCandidates.Where(candidate =>
                StringComparer.Ordinal.Equals(candidate.OccurrenceId, occurrence.OccurrenceId) &&
                StringComparer.Ordinal.Equals(
                    candidate.Owner.ProfiledRevision,
                    consumerRepository.ExpectedCommit) &&
                MatchesConfiguredInvocation(candidate, occurrence.Consumer.Invocation)),
        ];
        if (eligibleConsumers.Length != 1)
        {
            throw new InvalidDataException(
                "Exact resolution requires one direction-qualified consumer candidate.");
        }

        ConsumerDispatchCandidate consumer = eligibleConsumers[0];
        string normalizedConsumer = NormalizeEndpointIdentifier(
            consumer.RawIdentifier,
            occurrence.Normalization.ConsumerRequiredPrefix);
        ProviderEndpointCandidate[] eligibleProviders =
        [
            .. providerCandidates.Where(candidate =>
                StringComparer.Ordinal.Equals(candidate.OccurrenceId, occurrence.OccurrenceId) &&
                candidate.Owner.RepositoryId != consumer.Owner.RepositoryId &&
                StringComparer.Ordinal.Equals(
                    candidate.Owner.ProfiledRevision,
                    providerRepository.ExpectedCommit) &&
                StringComparer.Ordinal.Equals(
                    NormalizeEndpointIdentifier(
                        candidate.RawIdentifier,
                        occurrence.Normalization.ProviderRequiredPrefix),
                    normalizedConsumer)),
        ];
        if (eligibleProviders.Length != 1)
        {
            throw new InvalidDataException(
                "Exact resolution requires exactly one direction-qualified provider candidate.");
        }

        ProviderEndpointCandidate provider = eligibleProviders[0];
        string normalizedProvider = NormalizeEndpointIdentifier(
            provider.RawIdentifier,
            occurrence.Normalization.ProviderRequiredPrefix);
        if (occurrence.Comparison != ExactResolutionComparison.Ordinal ||
            !StringComparer.Ordinal.Equals(normalizedConsumer, normalizedProvider))
        {
            throw new InvalidDataException(
                "The configured endpoint identifiers do not match ordinally.");
        }

        var evidence = new ExactResolutionDecisionEvidence(
            consumer.RawIdentifier,
            provider.RawIdentifier,
            normalizedConsumer,
            occurrence.Protocol,
            ExactMatchConfidence,
            consumer.InvocationEvidenceLocator,
            provider.DeclarationEvidenceLocator);
        return new(
            occurrence.OccurrenceId,
            ExactResolutionDecisionKind.ExactMatch,
            consumer.Owner,
            provider.Owner,
            "exact_configured",
            consumer.InvocationEvidenceLocator,
            provider.DeclarationEvidenceLocator,
            evidence);
    }

    private static ProfileRepository FindRepository(
        ExactResolutionProfile profile,
        string alias) =>
        profile.Repositories.Single(repository =>
            StringComparer.Ordinal.Equals(repository.Alias, alias));

    private static bool MatchesConfiguredInvocation(
        ConsumerDispatchCandidate candidate,
        ExactConsumerInvocationSelector selector) =>
        candidate.CallForm == selector.Form &&
        StringComparer.Ordinal.Equals(candidate.ReceiverSymbol, selector.ReceiverSymbol) &&
        StringComparer.Ordinal.Equals(candidate.MemberName, selector.MemberName) &&
        candidate.ArgumentIndex == selector.EndpointArgumentIndex &&
        candidate.Line == selector.Line &&
        candidate.Ordinal == selector.OccurrenceOrdinalOnLine;
}
