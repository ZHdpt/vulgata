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
        string requiredPrefix) =>
        ExactEndpointNormalizer.Normalize(rawIdentifier, requiredPrefix);

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
            ConsumerDispatchCandidate[] anchoredConsumers =
            [
                .. consumerCandidates.Where(candidate =>
                    StringComparer.Ordinal.Equals(
                        candidate.OccurrenceId,
                        occurrence.OccurrenceId) &&
                    StringComparer.Ordinal.Equals(
                        candidate.Owner.ProfiledRevision,
                        consumerRepository.ExpectedCommit)),
            ];
            if (anchoredConsumers.Length == 1)
            {
                throw new ExactResolutionNonExactException(
                    occurrence.OccurrenceId,
                    "disallowed_invocation");
            }

            throw new InvalidDataException(
                "Exact resolution requires one direction-qualified consumer candidate.");
        }

        ConsumerDispatchCandidate consumer = eligibleConsumers[0];
        string normalizedConsumer;
        try
        {
            normalizedConsumer = NormalizeEndpointIdentifier(
                consumer.RawIdentifier,
                occurrence.Normalization.ConsumerRequiredPrefix);
        }
        catch (InvalidDataException)
        {
            throw new ExactResolutionNonExactException(
                occurrence.OccurrenceId,
                "normalization_mismatch");
        }

        ProviderEndpointCandidate[] directionQualifiedProviders =
        [
            .. providerCandidates.Where(candidate =>
                StringComparer.Ordinal.Equals(candidate.OccurrenceId, occurrence.OccurrenceId) &&
                candidate.Owner.RepositoryId != consumer.Owner.RepositoryId &&
                StringComparer.Ordinal.Equals(
                    candidate.Owner.ProfiledRevision,
                    providerRepository.ExpectedCommit)),
        ];
        ProviderEndpointCandidate[] eligibleProviders;
        try
        {
            eligibleProviders =
            [
                .. directionQualifiedProviders.Where(candidate =>
                    StringComparer.Ordinal.Equals(
                        NormalizeEndpointIdentifier(
                            candidate.RawIdentifier,
                            occurrence.Normalization.ProviderRequiredPrefix),
                        normalizedConsumer)),
            ];
        }
        catch (InvalidDataException)
        {
            throw new ExactResolutionNonExactException(
                occurrence.OccurrenceId,
                "normalization_mismatch");
        }

        ProviderEndpointCandidate[][] providersByDocument =
        [
            .. eligibleProviders
                .GroupBy(candidate => candidate.Owner.DocumentVersionId)
                .OrderBy(group => group.Key)
                .Select(group => group
                    .OrderBy(candidate => candidate.DeclarationEvidenceLocator, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.RawIdentifier, StringComparer.Ordinal)
                    .ToArray()),
        ];
        if (providersByDocument.Length != 1)
        {
            if (providersByDocument.Length > 1)
            {
                return new(
                    occurrence.OccurrenceId,
                    ExactResolutionDecisionKind.Uncertainty,
                    consumer.Owner,
                    null,
                    "multiple_providers",
                    consumer.InvocationEvidenceLocator);
            }

            throw new ExactResolutionNonExactException(
                occurrence.OccurrenceId,
                "unmatched");
        }

        ProviderEndpointCandidate provider = providersByDocument[0][0];
        string normalizedProvider = NormalizeEndpointIdentifier(
            provider.RawIdentifier,
            occurrence.Normalization.ProviderRequiredPrefix);
        if (occurrence.Comparison != ExactResolutionComparison.Ordinal ||
            !StringComparer.Ordinal.Equals(normalizedConsumer, normalizedProvider))
        {
            throw new ExactResolutionNonExactException(
                occurrence.OccurrenceId,
                "normalization_mismatch");
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
