using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.CrossRepository;

namespace Vulgata.Tests;

public sealed class ExactCrossRepositoryResolverTests
{
    [Fact]
    public async Task ResolveAsync_ReviewedDirection_BuildsProfileOrderedExactDecisionAsync()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactResolutionOwner consumerOwner = Owner(
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ConsumerScanRunId,
            ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
            ExactCrossRepositoryTestValues.ConsumerSourceAnchorId,
            Commit(profile, occurrence.ConsumerAlias));
        ExactResolutionOwner providerOwner = Owner(
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
            ExactCrossRepositoryTestValues.ProviderSourceAnchorId,
            Commit(profile, occurrence.ProviderAlias));
        var consumer = new ConsumerDispatchCandidate(
            occurrence.OccurrenceId,
            consumerOwner,
            ExactInvocationForm.SimpleMemberInvocation,
            "netTools",
            "execute",
            1,
            45,
            0,
            "  cust/updateSession  ",
            "must-not-be-trusted",
            "consumer.java#line=45;rule=exact-java-direct-literal-v1");
        var provider = new ProviderEndpointCandidate(
            occurrence.OccurrenceId,
            providerOwner,
            "  in/cust/updateSession  ",
            "must-not-be-trusted",
            "provider.xml#line=2;rule=exact-xml-root-attribute-v1");
        ExactResolutionDecision unsupported = UnsupportedDecision(
            Assert.Single(profile.UnsupportedObservations).ObservationId,
            consumerOwner);

        ExactResolutionDecisionBatch batch = await new ExactCrossRepositoryResolver().ResolveAsync(
            profile,
            [consumer],
            [provider],
            [unsupported]);

        Assert.Equal(
            profile.Occurrences.Select(value => value.OccurrenceId)
                .Concat(profile.UnsupportedObservations.Select(value => value.ObservationId))
                .Order(StringComparer.Ordinal),
            batch.Decisions.Select(value => value.Identity));
        ExactResolutionDecision exact = Assert.Single(
            batch.Decisions,
            decision => decision.Kind == ExactResolutionDecisionKind.ExactMatch);
        Assert.Equal(consumerOwner, exact.Owner);
        Assert.Equal(providerOwner, exact.Target);
        Assert.Equal("exact_configured", exact.ReasonCode);
        ExactResolutionDecisionEvidence evidence = Assert.IsType<ExactResolutionDecisionEvidence>(
            exact.ExactEvidence);
        Assert.Equal("  cust/updateSession  ", evidence.ConsumerRawIdentifier);
        Assert.Equal("  in/cust/updateSession  ", evidence.ProviderRawIdentifier);
        Assert.Equal("cust/updateSession", evidence.NormalizedIdentifier);
        Assert.Equal(ExactResolutionProtocol.CustomRpc, evidence.Protocol);
        Assert.Equal(1.0000m, evidence.Confidence);
        Assert.Equal(consumer.InvocationEvidenceLocator, evidence.ConsumerEvidenceLocator);
        Assert.Equal(provider.DeclarationEvidenceLocator, evidence.ProviderEvidenceLocator);
    }

    [Fact]
    public void NormalizeEndpointIdentifier_FormCAsciiEdgesAndOneProviderPrefix_IsDeterministic()
    {
        const string decomposed = "\t cafe\u0301 \r\n";

        string consumer = ExactCrossRepositoryResolver.NormalizeEndpointIdentifier(
            decomposed,
            requiredPrefix: string.Empty);
        string provider = ExactCrossRepositoryResolver.NormalizeEndpointIdentifier(
            "\t in/cafe\u0301 \r\n",
            requiredPrefix: "in/");
        InvalidDataException repeatedPrefix = Assert.Throws<InvalidDataException>(() =>
            ExactCrossRepositoryResolver.NormalizeEndpointIdentifier(
                "in/in/cust/updateSession",
                requiredPrefix: "in/"));

        Assert.Equal("caf\u00e9", consumer);
        Assert.True(consumer.IsNormalized(NormalizationForm.FormC));
        Assert.Equal(consumer, provider);
        Assert.Equal(
            "The endpoint identifier does not satisfy the configured normalization rule.",
            repeatedPrefix.Message);
    }

    [Theory]
    [InlineData("cust/updateSession", "in/")]
    [InlineData("in/", "in/")]
    [InlineData("in/cust\nupdateSession", "in/")]
    public void NormalizeEndpointIdentifier_InvalidPrefixResult_IsRejected(
        string rawIdentifier,
        string requiredPrefix)
    {
        Assert.Throws<InvalidDataException>(() =>
            ExactCrossRepositoryResolver.NormalizeEndpointIdentifier(
                rawIdentifier,
                requiredPrefix));
    }

    [Fact]
    public void NormalizeEndpointIdentifier_OverBoundUtf8Result_IsRejected()
    {
        string rawIdentifier = "in/" +
            new string('a', ExactResolutionLimits.MaxEndpointUtf8Bytes + 1);

        Assert.Throws<InvalidDataException>(() =>
            ExactCrossRepositoryResolver.NormalizeEndpointIdentifier(
                rawIdentifier,
                "in/"));
    }

    [Fact]
    public async Task ResolveAsync_ReversedDirectionOrOrdinalMismatch_ProducesNoExactDecisionAsync()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactResolutionOwner reversedConsumer = Owner(
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ConsumerScanRunId,
            ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
            ExactCrossRepositoryTestValues.ConsumerSourceAnchorId,
            Commit(profile, occurrence.ProviderAlias));
        ExactResolutionOwner reversedProvider = Owner(
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
            ExactCrossRepositoryTestValues.ProviderSourceAnchorId,
            Commit(profile, occurrence.ConsumerAlias));

        await Assert.ThrowsAsync<InvalidDataException>(() => ResolveAsync(
            profile,
            occurrence,
            reversedConsumer,
            reversedProvider,
            "cust/updateSession",
            "in/cust/updateSession"));

        ExactResolutionOwner consumer = reversedConsumer with
        {
            ProfiledRevision = Commit(profile, occurrence.ConsumerAlias),
        };
        ExactResolutionOwner provider = reversedProvider with
        {
            ProfiledRevision = Commit(profile, occurrence.ProviderAlias),
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => ResolveAsync(
            profile,
            occurrence,
            consumer,
            provider,
            "cust/updateSession",
            "in/Cust/updateSession"));
    }

    [Fact]
    public async Task ResolveAsync_TwoEligibleProviders_ProducesNoExactDecisionAsync()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactResolutionOwner consumerOwner = Owner(
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ConsumerScanRunId,
            ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
            ExactCrossRepositoryTestValues.ConsumerSourceAnchorId,
            Commit(profile, occurrence.ConsumerAlias));
        ExactResolutionOwner providerOwner = Owner(
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
            ExactCrossRepositoryTestValues.ProviderSourceAnchorId,
            Commit(profile, occurrence.ProviderAlias));
        ConsumerDispatchCandidate consumer = Consumer(occurrence, consumerOwner, "cust/updateSession");
        ProviderEndpointCandidate provider = Provider(occurrence, providerOwner, "in/cust/updateSession");
        ProviderEndpointCandidate duplicate = Provider(
            occurrence,
            providerOwner with
            {
                DocumentVersionId = ExactCrossRepositoryTestValues.Id(620),
                SourceAnchorId = ExactCrossRepositoryTestValues.Id(720),
            },
            "in/cust/updateSession");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ExactCrossRepositoryResolver().ResolveAsync(
                profile,
                [consumer],
                [provider, duplicate],
                [UnsupportedDecision(
                    Assert.Single(profile.UnsupportedObservations).ObservationId,
                    consumerOwner)]));
    }

    [Theory]
    [InlineData("receiver")]
    [InlineData("member")]
    [InlineData("argument")]
    [InlineData("line")]
    [InlineData("ordinal")]
    [InlineData("occurrence")]
    public async Task ResolveAsync_UnconfiguredConsumerCandidate_ProducesNoExactDecisionAsync(
        string mismatch)
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactResolutionOwner consumerOwner = ConsumerOwner(profile, occurrence);
        ExactResolutionOwner providerOwner = ProviderOwner(profile, occurrence);
        ExactConsumerInvocationSelector selector = occurrence.Consumer.Invocation;
        var consumer = new ConsumerDispatchCandidate(
            mismatch == "occurrence" ? "another-occurrence" : occurrence.OccurrenceId,
            consumerOwner,
            selector.Form,
            mismatch == "receiver" ? "otherTools" : selector.ReceiverSymbol,
            mismatch == "member" ? "call" : selector.MemberName,
            mismatch == "argument" ? selector.EndpointArgumentIndex + 1 : selector.EndpointArgumentIndex,
            mismatch == "line" ? selector.Line + 1 : selector.Line,
            mismatch == "ordinal" ? selector.OccurrenceOrdinalOnLine + 1 : selector.OccurrenceOrdinalOnLine,
            "cust/updateSession",
            "cust/updateSession",
            "consumer.java#line=45;rule=exact-java-direct-literal-v1");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ExactCrossRepositoryResolver().ResolveAsync(
                profile,
                [consumer],
                [Provider(occurrence, providerOwner, "in/cust/updateSession")],
                [UnsupportedDecision(
                    Assert.Single(profile.UnsupportedObservations).ObservationId,
                    consumerOwner)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task ResolveAsync_ConsumerCardinalityNotOne_ProducesNoExactDecisionAsync(
        int count)
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactResolutionOwner consumerOwner = ConsumerOwner(profile, occurrence);
        ExactResolutionOwner providerOwner = ProviderOwner(profile, occurrence);
        ConsumerDispatchCandidate[] consumers = count == 0
            ? []
            :
            [
                Consumer(occurrence, consumerOwner, "cust/updateSession"),
                Consumer(
                    occurrence,
                    consumerOwner with
                    {
                        DocumentVersionId = ExactCrossRepositoryTestValues.Id(621),
                        SourceAnchorId = ExactCrossRepositoryTestValues.Id(721),
                    },
                    "cust/updateSession"),
            ];

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ExactCrossRepositoryResolver().ResolveAsync(
                profile,
                consumers,
                [Provider(occurrence, providerOwner, "in/cust/updateSession")],
                [UnsupportedDecision(
                    Assert.Single(profile.UnsupportedObservations).ObservationId,
                    consumerOwner)]));
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("unmatched")]
    [InlineData("self")]
    public async Task ResolveAsync_NoUniqueDirectionQualifiedProvider_ProducesNoExactDecisionAsync(
        string mode)
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactResolutionOwner consumerOwner = ConsumerOwner(profile, occurrence);
        ExactResolutionOwner providerOwner = ProviderOwner(profile, occurrence);
        ProviderEndpointCandidate[] providers = mode switch
        {
            "zero" => [],
            "unmatched" => [Provider(occurrence, providerOwner, "in/otherEndpoint")],
            "self" => [Provider(
                occurrence,
                providerOwner with { RepositoryId = consumerOwner.RepositoryId },
                "in/cust/updateSession")],
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ExactCrossRepositoryResolver().ResolveAsync(
                profile,
                [Consumer(occurrence, consumerOwner, "cust/updateSession")],
                providers,
                [UnsupportedDecision(
                    Assert.Single(profile.UnsupportedObservations).ObservationId,
                    consumerOwner)]));
    }

    private static Task<ExactResolutionDecisionBatch> ResolveAsync(
        ExactResolutionProfile profile,
        ResolutionOccurrence occurrence,
        ExactResolutionOwner consumerOwner,
        ExactResolutionOwner providerOwner,
        string consumerIdentifier,
        string providerIdentifier) =>
        new ExactCrossRepositoryResolver().ResolveAsync(
            profile,
            [Consumer(occurrence, consumerOwner, consumerIdentifier)],
            [Provider(occurrence, providerOwner, providerIdentifier)],
            [UnsupportedDecision(
                Assert.Single(profile.UnsupportedObservations).ObservationId,
                consumerOwner)]);

    private static ConsumerDispatchCandidate Consumer(
        ResolutionOccurrence occurrence,
        ExactResolutionOwner owner,
        string rawIdentifier) =>
        new(
            occurrence.OccurrenceId,
            owner,
            occurrence.Consumer.Invocation.Form,
            occurrence.Consumer.Invocation.ReceiverSymbol,
            occurrence.Consumer.Invocation.MemberName,
            occurrence.Consumer.Invocation.EndpointArgumentIndex,
            occurrence.Consumer.Invocation.Line,
            occurrence.Consumer.Invocation.OccurrenceOrdinalOnLine,
            rawIdentifier,
            rawIdentifier,
            "consumer.java#line=45;rule=exact-java-direct-literal-v1");

    private static ProviderEndpointCandidate Provider(
        ResolutionOccurrence occurrence,
        ExactResolutionOwner owner,
        string rawIdentifier) =>
        new(
            occurrence.OccurrenceId,
            owner,
            rawIdentifier,
            rawIdentifier,
            "provider.xml#line=2;rule=exact-xml-root-attribute-v1");

    private static ExactResolutionDecision UnsupportedDecision(
        string identity,
        ExactResolutionOwner owner) =>
        new(
            identity,
            ExactResolutionDecisionKind.UnsupportedObservation,
            owner,
            null,
            "unsupported_kind",
            "unsupported.java#line=66;rule=unsupported-member-invocation-v1");

    private static ExactResolutionOwner Owner(
        Guid repositoryId,
        Guid scanRunId,
        Guid documentVersionId,
        Guid sourceAnchorId,
        string profiledRevision) =>
        new(repositoryId, scanRunId, documentVersionId, sourceAnchorId, profiledRevision);

    private static ExactResolutionOwner ConsumerOwner(
        ExactResolutionProfile profile,
        ResolutionOccurrence occurrence) =>
        Owner(
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ConsumerScanRunId,
            ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
            ExactCrossRepositoryTestValues.ConsumerSourceAnchorId,
            Commit(profile, occurrence.ConsumerAlias));

    private static ExactResolutionOwner ProviderOwner(
        ExactResolutionProfile profile,
        ResolutionOccurrence occurrence) =>
        Owner(
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
            ExactCrossRepositoryTestValues.ProviderSourceAnchorId,
            Commit(profile, occurrence.ProviderAlias));

    private static string Commit(ExactResolutionProfile profile, string alias) =>
        Assert.Single(profile.Repositories, repository => repository.Alias == alias).ExpectedCommit;
}
