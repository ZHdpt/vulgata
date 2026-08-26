using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public sealed record ExactResolutionDecisionContract(
    ExactResolutionProtocol Protocol,
    string NormalizedEndpointIdentifier,
    string ConsumerEntryStableKey,
    ExactResolutionOutcomeKind Outcome,
    string EvidenceFingerprint);

public sealed record ExactResolutionProviderEvidenceContract(
    Guid ProviderDocumentVersionId,
    Guid SourceAnchorId,
    string EvidenceFingerprint);

public static class ExactResolutionLimits
{
    public const int MaxProfileBytes = 256 * 1024;
    public const int MaxJsonDepth = 16;
    public const int MaxRepositories = 16;
    public const int MaxOccurrences = 16;
    public const int MaxUnsupportedObservations = 16;
    public const int MaxDecisions = 32;
    public const int MaxFilesPerSelector = 16;
    public const int MaxEndpointUtf8Bytes = 512;
    public const int MaxEvidenceLocatorLength = 2_048;
    public const int MaxRepositoryRelativePathLength = 2_048;
    public const int MaxIdentifierLength = 256;
}

public enum ExactResolutionProtocol
{
    CustomRpc,
}

public enum ExactResolutionComparison
{
    Ordinal,
}

public enum ExactInvocationForm
{
    SimpleMemberInvocation,
}

public enum ExactSymbolKind
{
    Type,
}

public enum ExactUnsupportedObservationKind
{
    FileStorage,
}

public sealed record ProfileRepository
{
    public ProfileRepository(
        string alias,
        string stableGitIdentity,
        string expectedRef,
        string expectedCommit)
    {
        Alias = ExactResolutionGuard.Identifier(alias, nameof(alias), 64);
        StableGitIdentity = ExactResolutionGuard.Identifier(
            stableGitIdentity,
            nameof(stableGitIdentity),
            128);
        ExpectedRef = ExactResolutionGuard.FullTagRef(expectedRef, nameof(expectedRef));
        ExpectedCommit = ExactResolutionGuard.FullCommit(expectedCommit, nameof(expectedCommit));
    }

    public string Alias { get; }
    public string StableGitIdentity { get; }
    public string ExpectedRef { get; }
    public string ExpectedCommit { get; }
}

public sealed record ExactConsumerInvocationSelector
{
    public ExactConsumerInvocationSelector(
        ExactInvocationForm form,
        string receiverSymbol,
        string memberName,
        int endpointArgumentIndex,
        int line,
        int occurrenceOrdinalOnLine)
    {
        if (form != ExactInvocationForm.SimpleMemberInvocation ||
            endpointArgumentIndex is < 0 or > 15 ||
            line < 1 ||
            occurrenceOrdinalOnLine is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(form));
        }

        Form = form;
        ReceiverSymbol = ExactResolutionGuard.JavaIdentifier(
            receiverSymbol,
            nameof(receiverSymbol));
        MemberName = ExactResolutionGuard.JavaIdentifier(memberName, nameof(memberName));
        EndpointArgumentIndex = endpointArgumentIndex;
        Line = line;
        OccurrenceOrdinalOnLine = occurrenceOrdinalOnLine;
    }

    public ExactInvocationForm Form { get; }
    public string ReceiverSymbol { get; }
    public string MemberName { get; }
    public int EndpointArgumentIndex { get; }
    public int Line { get; }
    public int OccurrenceOrdinalOnLine { get; }
}

public sealed record ExactMemberInvocationSelector
{
    public ExactMemberInvocationSelector(
        ExactInvocationForm form,
        string receiverSymbol,
        string memberName,
        int line,
        int occurrenceOrdinalOnLine)
    {
        if (form != ExactInvocationForm.SimpleMemberInvocation ||
            line < 1 ||
            occurrenceOrdinalOnLine is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(form));
        }

        Form = form;
        ReceiverSymbol = ExactResolutionGuard.JavaIdentifier(
            receiverSymbol,
            nameof(receiverSymbol));
        MemberName = ExactResolutionGuard.JavaIdentifier(memberName, nameof(memberName));
        Line = line;
        OccurrenceOrdinalOnLine = occurrenceOrdinalOnLine;
    }

    public ExactInvocationForm Form { get; }
    public string ReceiverSymbol { get; }
    public string MemberName { get; }
    public int Line { get; }
    public int OccurrenceOrdinalOnLine { get; }
}

public sealed record ExactConsumerSelector
{
    public ExactConsumerSelector(
        IEnumerable<string> files,
        ExactConsumerInvocationSelector invocation)
    {
        Files = ExactResolutionGuard.Paths(files, nameof(files));
        Invocation = invocation ?? throw new ArgumentNullException(nameof(invocation));
    }

    public IReadOnlyList<string> Files { get; }
    public ExactConsumerInvocationSelector Invocation { get; }
}

public sealed record ExactProviderDeclarationSelector
{
    public ExactProviderDeclarationSelector(
        string elementLocalName,
        string endpointAttributeLocalName,
        int line,
        int occurrenceOrdinalOnLine)
    {
        if (line < 1 || occurrenceOrdinalOnLine is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        ElementLocalName = ExactResolutionGuard.XmlLocalName(
            elementLocalName,
            nameof(elementLocalName));
        EndpointAttributeLocalName = ExactResolutionGuard.XmlLocalName(
            endpointAttributeLocalName,
            nameof(endpointAttributeLocalName));
        Line = line;
        OccurrenceOrdinalOnLine = occurrenceOrdinalOnLine;
    }

    public string ElementLocalName { get; }
    public string EndpointAttributeLocalName { get; }
    public int Line { get; }
    public int OccurrenceOrdinalOnLine { get; }
}

public sealed record ExactDocumentSelector
{
    public ExactDocumentSelector(
        string repositoryRelativePath,
        ExactSymbolKind symbolKind,
        string symbol)
    {
        if (symbolKind != ExactSymbolKind.Type)
        {
            throw new ArgumentOutOfRangeException(nameof(symbolKind));
        }

        RepositoryRelativePath = ExactResolutionGuard.RepositoryRelativePath(
            repositoryRelativePath,
            nameof(repositoryRelativePath));
        SymbolKind = symbolKind;
        Symbol = ExactResolutionGuard.QualifiedJavaSymbol(symbol, nameof(symbol));
    }

    public string RepositoryRelativePath { get; }
    public ExactSymbolKind SymbolKind { get; }
    public string Symbol { get; }
}

public sealed record ExactProviderSelector
{
    public ExactProviderSelector(
        string root,
        IEnumerable<string> files,
        ExactProviderDeclarationSelector declaration,
        ExactDocumentSelector implementationDocument)
    {
        Root = ExactResolutionGuard.RepositoryRelativePath(root, nameof(root));
        Files = ExactResolutionGuard.Paths(files, nameof(files));
        string prefix = Root + '/';
        if (Files.Any(path => !path.StartsWith(prefix, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Provider files must remain beneath the configured root.", nameof(files));
        }

        Declaration = declaration ?? throw new ArgumentNullException(nameof(declaration));
        ImplementationDocument = implementationDocument ??
            throw new ArgumentNullException(nameof(implementationDocument));
    }

    public string Root { get; }
    public IReadOnlyList<string> Files { get; }
    public ExactProviderDeclarationSelector Declaration { get; }
    public ExactDocumentSelector ImplementationDocument { get; }
}

public sealed record ExactEndpointNormalization
{
    public ExactEndpointNormalization(
        string id,
        string consumerRequiredPrefix,
        string providerRequiredPrefix)
    {
        Id = ExactResolutionGuard.Identifier(id, nameof(id), 64);
        ConsumerRequiredPrefix = ExactResolutionGuard.Prefix(
            consumerRequiredPrefix,
            nameof(consumerRequiredPrefix));
        ProviderRequiredPrefix = ExactResolutionGuard.Prefix(
            providerRequiredPrefix,
            nameof(providerRequiredPrefix));
    }

    public string Id { get; }
    public string ConsumerRequiredPrefix { get; }
    public string ProviderRequiredPrefix { get; }
}

public sealed record ResolutionOccurrence
{
    public ResolutionOccurrence(
        string occurrenceId,
        ExactResolutionProtocol protocol,
        string consumerAlias,
        string providerAlias,
        ExactConsumerSelector consumer,
        ExactProviderSelector provider,
        ExactEndpointNormalization normalization,
        ExactResolutionComparison comparison)
    {
        if (protocol != ExactResolutionProtocol.CustomRpc ||
            comparison != ExactResolutionComparison.Ordinal)
        {
            throw new ArgumentOutOfRangeException(nameof(protocol));
        }

        OccurrenceId = ExactResolutionGuard.Identifier(occurrenceId, nameof(occurrenceId), 128);
        ConsumerAlias = ExactResolutionGuard.Identifier(consumerAlias, nameof(consumerAlias), 64);
        ProviderAlias = ExactResolutionGuard.Identifier(providerAlias, nameof(providerAlias), 64);
        if (StringComparer.Ordinal.Equals(ConsumerAlias, ProviderAlias))
        {
            throw new ArgumentException("Consumer and provider aliases must differ.");
        }

        Protocol = protocol;
        Consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Normalization = normalization ?? throw new ArgumentNullException(nameof(normalization));
        Comparison = comparison;
    }

    public string OccurrenceId { get; }
    public ExactResolutionProtocol Protocol { get; }
    public string ConsumerAlias { get; }
    public string ProviderAlias { get; }
    public ExactConsumerSelector Consumer { get; }
    public ExactProviderSelector Provider { get; }
    public ExactEndpointNormalization Normalization { get; }
    public ExactResolutionComparison Comparison { get; }
}

public sealed record UnsupportedObservation
{
    public UnsupportedObservation(
        string observationId,
        ExactUnsupportedObservationKind kind,
        string ownerAlias,
        ExactDocumentSelector ownerDocument,
        ExactMemberInvocationSelector selector,
        string evidenceLocator)
    {
        if (kind != ExactUnsupportedObservationKind.FileStorage)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ObservationId = ExactResolutionGuard.Identifier(observationId, nameof(observationId), 128);
        Kind = kind;
        OwnerAlias = ExactResolutionGuard.Identifier(ownerAlias, nameof(ownerAlias), 64);
        OwnerDocument = ownerDocument ?? throw new ArgumentNullException(nameof(ownerDocument));
        Selector = selector ?? throw new ArgumentNullException(nameof(selector));
        EvidenceLocator = ExactResolutionGuard.EvidenceLocator(evidenceLocator, nameof(evidenceLocator));
    }

    public string ObservationId { get; }
    public ExactUnsupportedObservationKind Kind { get; }
    public string OwnerAlias { get; }
    public ExactDocumentSelector OwnerDocument { get; }
    public ExactMemberInvocationSelector Selector { get; }
    public string EvidenceLocator { get; }
}

public sealed class ExactResolutionProfile
{
    public ExactResolutionProfile(
        string schemaVersion,
        string profileId,
        string profileVersion,
        string profileFingerprint,
        IEnumerable<ProfileRepository> repositories,
        IEnumerable<ResolutionOccurrence> occurrences,
        IEnumerable<UnsupportedObservation> unsupportedObservations,
        ReadOnlyMemory<byte> canonicalUtf8Json)
    {
        SchemaVersion = ExactResolutionGuard.Exact(schemaVersion, "1.0", nameof(schemaVersion));
        ProfileId = ExactResolutionGuard.Identifier(profileId, nameof(profileId), 128);
        ProfileVersion = ExactResolutionGuard.SemanticVersion(profileVersion, nameof(profileVersion));
        CanonicalUtf8Json = canonicalUtf8Json.ToArray();
        if (CanonicalUtf8Json.Length is 0 or > ExactResolutionLimits.MaxProfileBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(canonicalUtf8Json));
        }

        string computed = Convert.ToHexString(SHA256.HashData(CanonicalUtf8Json.Span))
            .ToLowerInvariant();
        ProfileFingerprint = ExactResolutionGuard.Exact(
            profileFingerprint,
            computed,
            nameof(profileFingerprint));
        Repositories = ExactResolutionGuard.CanonicalItems(
            repositories,
            2,
            ExactResolutionLimits.MaxRepositories,
            static value => value.Alias,
            nameof(repositories));
        Occurrences = ExactResolutionGuard.CanonicalItems(
            occurrences,
            1,
            ExactResolutionLimits.MaxOccurrences,
            static value => value.OccurrenceId,
            nameof(occurrences));
        UnsupportedObservations = ExactResolutionGuard.CanonicalItems(
            unsupportedObservations,
            1,
            ExactResolutionLimits.MaxUnsupportedObservations,
            static value => value.ObservationId,
            nameof(unsupportedObservations));

        HashSet<string> aliases = Repositories.Select(value => value.Alias).ToHashSet(StringComparer.Ordinal);
        if (Occurrences.Any(value =>
                !aliases.Contains(value.ConsumerAlias) ||
                !aliases.Contains(value.ProviderAlias)) ||
            UnsupportedObservations.Any(value => !aliases.Contains(value.OwnerAlias)) ||
            Repositories.Select(value => value.StableGitIdentity).Distinct(StringComparer.Ordinal).Count() !=
                Repositories.Count)
        {
            throw new ArgumentException("Profile selectors must bind unique configured Repository aliases.");
        }
    }

    public string SchemaVersion { get; }
    public string ProfileId { get; }
    public string ProfileVersion { get; }
    public string ProfileFingerprint { get; }
    public IReadOnlyList<ProfileRepository> Repositories { get; }
    public IReadOnlyList<ResolutionOccurrence> Occurrences { get; }
    public IReadOnlyList<UnsupportedObservation> UnsupportedObservations { get; }
    public ReadOnlyMemory<byte> CanonicalUtf8Json { get; }
}

public sealed record ExactResolutionRuntimeBinding(
    string Alias,
    string StableGitIdentity,
    Guid RepositoryId,
    Guid ScanRunId,
    Guid ScanExecutionId);

public sealed class ExactResolutionRuntimeRequest
{
    private ExactResolutionRuntimeRequest(
        Guid systemId,
        ExactResolutionProfile profile,
        IReadOnlyList<ExactResolutionRuntimeBinding> bindings)
    {
        SystemId = systemId;
        ProfileId = profile.ProfileId;
        ProfileVersion = profile.ProfileVersion;
        ProfileFingerprint = profile.ProfileFingerprint;
        Bindings = bindings;
    }

    public Guid SystemId { get; }
    public string ProfileId { get; }
    public string ProfileVersion { get; }
    public string ProfileFingerprint { get; }
    public IReadOnlyList<ExactResolutionRuntimeBinding> Bindings { get; }

    public static ExactResolutionRuntimeRequest Create(
        Guid systemId,
        ExactResolutionProfile profile,
        IEnumerable<ExactResolutionRuntimeBinding> bindings)
    {
        if (systemId == Guid.Empty)
        {
            throw new ArgumentException("One System identity is required.", nameof(systemId));
        }

        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(bindings);
        ExactResolutionRuntimeBinding[] values = [.. bindings];
        if (values.Length != profile.Repositories.Count ||
            values.Any(value =>
                value.RepositoryId == Guid.Empty ||
                value.ScanRunId == Guid.Empty ||
                value.ScanExecutionId == Guid.Empty) ||
            values.Select(value => value.Alias).Distinct(StringComparer.Ordinal).Count() != values.Length ||
            values.Select(value => value.RepositoryId).Distinct().Count() != values.Length)
        {
            throw new ArgumentException("Runtime bindings must be complete, unique, and distinct.", nameof(bindings));
        }

        Dictionary<string, ExactResolutionRuntimeBinding> byAlias;
        try
        {
            byAlias = values.ToDictionary(value => value.Alias, StringComparer.Ordinal);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Runtime aliases must be unique.", nameof(bindings), exception);
        }

        var ordered = new List<ExactResolutionRuntimeBinding>(profile.Repositories.Count);
        foreach (ProfileRepository repository in profile.Repositories)
        {
            if (!byAlias.TryGetValue(repository.Alias, out ExactResolutionRuntimeBinding? binding) ||
                !StringComparer.Ordinal.Equals(repository.StableGitIdentity, binding.StableGitIdentity))
            {
                throw new ArgumentException("Runtime bindings must match the reviewed profile.", nameof(bindings));
            }

            ordered.Add(binding);
        }

        return new(systemId, profile, ordered.AsReadOnly());
    }
}

public sealed record ExactResolutionOwner(
    Guid RepositoryId,
    Guid ScanRunId,
    Guid DocumentVersionId,
    Guid SourceAnchorId,
    string ProfiledRevision)
{
    public void Validate()
    {
        if (RepositoryId == Guid.Empty ||
            ScanRunId == Guid.Empty ||
            DocumentVersionId == Guid.Empty ||
            SourceAnchorId == Guid.Empty)
        {
            throw new ArgumentException("Complete owner identities are required.");
        }

        _ = ExactResolutionGuard.FullCommit(ProfiledRevision, nameof(ProfiledRevision));
    }
}

public sealed record ProviderEndpointCandidate
{
    public ProviderEndpointCandidate(
        string occurrenceId,
        ExactResolutionOwner owner,
        string rawIdentifier,
        string normalizedIdentifier,
        string declarationEvidenceLocator)
    {
        OccurrenceId = ExactResolutionGuard.Identifier(occurrenceId, nameof(occurrenceId), 128);
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Owner.Validate();
        RawIdentifier = ExactResolutionGuard.Endpoint(rawIdentifier, nameof(rawIdentifier));
        NormalizedIdentifier = ExactResolutionGuard.Endpoint(
            normalizedIdentifier,
            nameof(normalizedIdentifier));
        DeclarationEvidenceLocator = ExactResolutionGuard.EvidenceLocator(
            declarationEvidenceLocator,
            nameof(declarationEvidenceLocator));
    }

    public string OccurrenceId { get; }
    public ExactResolutionOwner Owner { get; }
    public string RawIdentifier { get; }
    public string NormalizedIdentifier { get; }
    public string DeclarationEvidenceLocator { get; }
}

public sealed record ConsumerDispatchCandidate
{
    public ConsumerDispatchCandidate(
        string occurrenceId,
        ExactResolutionOwner owner,
        ExactInvocationForm callForm,
        string receiverSymbol,
        string memberName,
        int argumentIndex,
        int line,
        int ordinal,
        string rawIdentifier,
        string normalizedIdentifier,
        string invocationEvidenceLocator)
    {
        _ = new ExactConsumerInvocationSelector(
            callForm,
            receiverSymbol,
            memberName,
            argumentIndex,
            line,
            ordinal);
        OccurrenceId = ExactResolutionGuard.Identifier(occurrenceId, nameof(occurrenceId), 128);
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Owner.Validate();
        CallForm = callForm;
        ReceiverSymbol = receiverSymbol;
        MemberName = memberName;
        ArgumentIndex = argumentIndex;
        Line = line;
        Ordinal = ordinal;
        RawIdentifier = ExactResolutionGuard.Endpoint(rawIdentifier, nameof(rawIdentifier));
        NormalizedIdentifier = ExactResolutionGuard.Endpoint(
            normalizedIdentifier,
            nameof(normalizedIdentifier));
        InvocationEvidenceLocator = ExactResolutionGuard.EvidenceLocator(
            invocationEvidenceLocator,
            nameof(invocationEvidenceLocator));
    }

    public string OccurrenceId { get; }
    public ExactResolutionOwner Owner { get; }
    public ExactInvocationForm CallForm { get; }
    public string ReceiverSymbol { get; }
    public string MemberName { get; }
    public int ArgumentIndex { get; }
    public int Line { get; }
    public int Ordinal { get; }
    public string RawIdentifier { get; }
    public string NormalizedIdentifier { get; }
    public string InvocationEvidenceLocator { get; }
}

public enum ExactResolutionDecisionKind
{
    ExactMatch,
    Uncertainty,
    UnsupportedObservation,
}

public sealed record ExactResolutionDecisionEvidence
{
    public ExactResolutionDecisionEvidence(
        string consumerRawIdentifier,
        string providerRawIdentifier,
        string normalizedIdentifier,
        ExactResolutionProtocol protocol,
        decimal confidence,
        string consumerEvidenceLocator,
        string providerEvidenceLocator)
    {
        if (protocol != ExactResolutionProtocol.CustomRpc || confidence != 1.0000m)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        ConsumerRawIdentifier = ExactResolutionGuard.Endpoint(
            consumerRawIdentifier,
            nameof(consumerRawIdentifier));
        ProviderRawIdentifier = ExactResolutionGuard.Endpoint(
            providerRawIdentifier,
            nameof(providerRawIdentifier));
        NormalizedIdentifier = ExactResolutionGuard.Endpoint(
            normalizedIdentifier,
            nameof(normalizedIdentifier));
        Protocol = protocol;
        Confidence = confidence;
        ConsumerEvidenceLocator = ExactResolutionGuard.EvidenceLocator(
            consumerEvidenceLocator,
            nameof(consumerEvidenceLocator));
        ProviderEvidenceLocator = ExactResolutionGuard.EvidenceLocator(
            providerEvidenceLocator,
            nameof(providerEvidenceLocator));
    }

    public string ConsumerRawIdentifier { get; }
    public string ProviderRawIdentifier { get; }
    public string NormalizedIdentifier { get; }
    public ExactResolutionProtocol Protocol { get; }
    public decimal Confidence { get; }
    public string ConsumerEvidenceLocator { get; }
    public string ProviderEvidenceLocator { get; }
}

public sealed record ExactResolutionDecision(
    string Identity,
    ExactResolutionDecisionKind Kind,
    ExactResolutionOwner Owner,
    ExactResolutionOwner? Target,
    string ReasonCode,
    string EvidenceLocator,
    string? TargetEvidenceLocator = null,
    ExactResolutionDecisionEvidence? ExactEvidence = null);

public sealed class ExactResolutionDecisionBatch
{
    private ExactResolutionDecisionBatch(
        ExactResolutionProfile profile,
        IReadOnlyList<ExactResolutionDecision> decisions)
    {
        ProfileId = profile.ProfileId;
        ProfileVersion = profile.ProfileVersion;
        ProfileFingerprint = profile.ProfileFingerprint;
        Decisions = decisions;
    }

    public string ProfileId { get; }
    public string ProfileVersion { get; }
    public string ProfileFingerprint { get; }
    public IReadOnlyList<ExactResolutionDecision> Decisions { get; }

    public static ExactResolutionDecisionBatch Create(
        ExactResolutionProfile profile,
        IEnumerable<ExactResolutionDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(decisions);
        ExactResolutionDecision[] values = [.. decisions];
        string[] expected =
        [
            .. profile.Occurrences.Select(value => value.OccurrenceId)
                .Concat(profile.UnsupportedObservations.Select(value => value.ObservationId))
                .Order(StringComparer.Ordinal),
        ];
        if (values.Length is < 2 or > ExactResolutionLimits.MaxDecisions ||
            !values.Select(value => value.Identity).SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new ArgumentException("The decision batch must exactly follow canonical profile identity order.", nameof(decisions));
        }

        foreach (ExactResolutionDecision decision in values)
        {
            _ = ExactResolutionGuard.Identifier(decision.Identity, nameof(decisions), 128);
            decision.Owner.Validate();
            decision.Target?.Validate();
            _ = ExactResolutionGuard.Identifier(decision.ReasonCode, nameof(decisions), 64);
            _ = ExactResolutionGuard.EvidenceLocator(decision.EvidenceLocator, nameof(decisions));
            if (decision.TargetEvidenceLocator is not null)
            {
                _ = ExactResolutionGuard.EvidenceLocator(
                    decision.TargetEvidenceLocator,
                    nameof(decisions));
            }

            if (decision.Kind == ExactResolutionDecisionKind.ExactMatch)
            {
                if (decision.Target is null ||
                    decision.ExactEvidence is null ||
                    decision.Owner.RepositoryId == decision.Target.RepositoryId ||
                    !StringComparer.Ordinal.Equals(decision.ReasonCode, "exact_configured") ||
                    !StringComparer.Ordinal.Equals(
                        decision.EvidenceLocator,
                        decision.ExactEvidence.ConsumerEvidenceLocator) ||
                    !StringComparer.Ordinal.Equals(
                        decision.TargetEvidenceLocator,
                        decision.ExactEvidence.ProviderEvidenceLocator))
                {
                    throw new ArgumentException(
                        "An exact decision requires complete direction-qualified evidence.",
                        nameof(decisions));
                }
            }
            else if (decision.ExactEvidence is not null)
            {
                throw new ArgumentException(
                    "Only an exact decision may carry exact evidence.",
                    nameof(decisions));
            }
        }

        return new(profile, Array.AsReadOnly(values));
    }
}

public enum ExactResolutionOutcomeKind
{
    WaitingForPass1,
    Applied,
    AlreadyApplied,
    UncertaintyRecorded,
    AlreadyRecorded,
    Refused,
    Conflict,
    Cancelled,
    Failed,
}

public sealed record ExactResolutionOutcome(
    ExactResolutionOutcomeKind Kind,
    string? Code = null,
    ExactResolutionRelationshipReadModel? Relationship = null);

public sealed record ExactResolutionBatchItemOutcome(
    string Identity,
    ExactResolutionOutcomeKind Kind);

public sealed record ExactResolutionBatchOutcome(
    ExactResolutionOutcomeKind Kind,
    IReadOnlyList<ExactResolutionBatchItemOutcome> Items,
    string? Code = null);

public sealed record ExactCrossRepositoryEvidenceReadModel(
    string ConsumerRawIdentifier,
    string ProviderRawIdentifier,
    string NormalizedIdentifier,
    ExactResolutionProtocol Protocol,
    string ProfileId,
    string ProfileVersion,
    string ProfileFingerprint,
    string OccurrenceId,
    decimal Confidence,
    string ConsumerEvidenceLocator,
    string ProviderEvidenceLocator);

public sealed record ExactResolutionRelationshipReadModel(
    string RelationshipIdentity,
    DocumentEdgeType EdgeType,
    ExactCrossRepositoryEvidenceReadModel ExactEvidence);

public interface IExactGitRevisionSnapshotService
{
    Task<ICommittedRepositorySnapshotLease> AcquireAsync(
        Repository repository,
        Guid scanRunId,
        string expectedRef,
        string expectedCommit,
        CancellationToken cancellationToken = default);
}

internal static class ExactResolutionGuard
{
    internal static string Exact(string value, string expected, string parameterName)
    {
        if (!StringComparer.Ordinal.Equals(value, expected))
        {
            throw new ArgumentException("The value is outside the closed contract.", parameterName);
        }

        return value;
    }

    internal static string Identifier(string value, string parameterName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > maxLength ||
            !value.IsNormalized(NormalizationForm.FormC) ||
            value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character is not ('-' or '_' or '.')))
        {
            throw new ArgumentException("A bounded canonical identifier is required.", parameterName);
        }

        return value;
    }

    internal static string JavaIdentifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > ExactResolutionLimits.MaxIdentifierLength ||
            !value.IsNormalized(NormalizationForm.FormC) ||
            !IsJavaIdentifierStart(value[0]) ||
            value.Skip(1).Any(character => !IsJavaIdentifierPart(character)))
        {
            throw new ArgumentException("A bounded Java identifier is required.", parameterName);
        }

        return value;
    }

    internal static string QualifiedJavaSymbol(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 512 ||
            value.Split('.').Any(part =>
                string.IsNullOrEmpty(part) ||
                !StringComparer.Ordinal.Equals(JavaIdentifier(part, parameterName), part)))
        {
            throw new ArgumentException("A bounded exact type symbol is required.", parameterName);
        }

        return value;
    }

    internal static string XmlLocalName(string value, string parameterName) =>
        JavaIdentifier(value, parameterName);

    internal static string SemanticVersion(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string[] components = value.Split('.');
        if (components.Length != 3 ||
            components.Any(component =>
                component.Length == 0 ||
                component.Length > 6 ||
                component.Any(character => !char.IsAsciiDigit(character)) ||
                (component.Length > 1 && component[0] == '0')))
        {
            throw new ArgumentException("A bounded semantic version is required.", parameterName);
        }

        return value;
    }

    internal static string FullTagRef(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 256 ||
            !value.StartsWith("refs/tags/", StringComparison.Ordinal) ||
            value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains("@{", StringComparison.Ordinal) ||
            value.Contains("//", StringComparison.Ordinal) ||
            value.EndsWith("/", StringComparison.Ordinal) ||
            value.EndsWith(".", StringComparison.Ordinal) ||
            value.IndexOfAny(['~', '^', ':', '?', '*', '[', '\\']) >= 0)
        {
            throw new ArgumentException("One safe full tag ref is required.", parameterName);
        }

        return value;
    }

    internal static string FullCommit(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length != 40 ||
            value.Any(character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("One full lowercase Git commit is required.", parameterName);
        }

        return value;
    }

    internal static string RepositoryRelativePath(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > ExactResolutionLimits.MaxRepositoryRelativePathLength ||
            !value.IsNormalized(NormalizationForm.FormC) ||
            value[0] is '/' or '\\' ||
            value.Contains('\\', StringComparison.Ordinal) ||
            value.Contains("://", StringComparison.Ordinal) ||
            value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':')
        {
            throw new ArgumentException("A safe Repository-relative path is required.", parameterName);
        }

        string[] segments = value.Split('/');
        if (segments.Any(segment =>
                segment is "" or "." or ".." ||
                segment.Any(char.IsControl)))
        {
            throw new ArgumentException("A safe Repository-relative path is required.", parameterName);
        }

        return value;
    }

    internal static IReadOnlyList<string> Paths(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        string[] paths = [.. values.Select(value => RepositoryRelativePath(value, parameterName))];
        if (paths.Length is < 1 or > ExactResolutionLimits.MaxFilesPerSelector ||
            paths.Distinct(StringComparer.Ordinal).Count() != paths.Length ||
            !paths.SequenceEqual(paths.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new ArgumentException("Selector paths must be finite, unique, and canonical.", parameterName);
        }

        return Array.AsReadOnly(paths);
    }

    internal static string Prefix(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length > 64 ||
            !value.IsNormalized(NormalizationForm.FormC) ||
            value.Any(char.IsControl) ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("A bounded canonical prefix is required.", parameterName);
        }

        return value;
    }

    internal static string Endpoint(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (Encoding.UTF8.GetByteCount(value) > ExactResolutionLimits.MaxEndpointUtf8Bytes ||
            !value.IsNormalized(NormalizationForm.FormC) ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException("A bounded endpoint identifier is required.", parameterName);
        }

        return value;
    }

    internal static string EvidenceLocator(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > ExactResolutionLimits.MaxEvidenceLocatorLength ||
            value.Any(character => char.IsControl(character) && character is not ('\t' or '\r' or '\n')) ||
            value.Contains("://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("/", StringComparison.Ordinal) ||
            value.StartsWith("\\", StringComparison.Ordinal) ||
            value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':')
        {
            throw new ArgumentException("A safe bounded Evidence Locator is required.", parameterName);
        }

        return value;
    }

    internal static IReadOnlyList<T> CanonicalItems<T>(
        IEnumerable<T> values,
        int minimum,
        int maximum,
        Func<T, string> identity,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        T[] items = [.. values];
        string[] identities = [.. items.Select(identity)];
        if (items.Length < minimum ||
            items.Length > maximum ||
            identities.Distinct(StringComparer.Ordinal).Count() != items.Length ||
            !identities.SequenceEqual(identities.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new ArgumentException("Values must be finite, unique, and canonically ordered.", parameterName);
        }

        return Array.AsReadOnly(items);
    }

    private static bool IsJavaIdentifierStart(char value) =>
        char.IsAsciiLetter(value) || value is '_' or '$';

    private static bool IsJavaIdentifierPart(char value) =>
        IsJavaIdentifierStart(value) || char.IsAsciiDigit(value);
}
