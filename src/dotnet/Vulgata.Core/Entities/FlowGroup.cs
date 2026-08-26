using Vulgata.Core.DomainServices;

namespace Vulgata.Core.Entities;

public sealed class FlowGroup
{
    private FlowGroup()
    {
    }

    public FlowGroup(
        Guid id,
        Guid businessLogicDocumentVersionId,
        Guid repositoryId,
        Guid scanRunId,
        int groupOrder,
        string title,
        DateTimeOffset createdAt)
    {
        Id = id;
        BusinessLogicDocumentVersionId = businessLogicDocumentVersionId;
        RepositoryId = repositoryId;
        ScanRunId = scanRunId;
        GroupOrder = groupOrder;
        Title = title;
        CreatedAt = createdAt;
    }

    public static FlowGroup CreatePlanned(
        Guid id,
        Guid pass2ExecutionId,
        Guid systemId,
        ExactResolutionProtocol protocol,
        string normalizedEndpointIdentifier,
        string consumerEntryIdentity,
        string consumerEntryStableKey,
        string logicalDocumentStableKey,
        Guid consumerDocumentVersionId,
        Guid consumerRepositoryId,
        Guid consumerScanRunId,
        Guid providerRepositoryId,
        Guid providerXmlDocumentVersionId,
        Guid providerImplementationDocumentVersionId,
        Guid exactResolutionDecisionId,
        Guid exactResolutionEdgeId,
        int groupOrder,
        string planFingerprint,
        string membershipFingerprint,
        DateTimeOffset createdAt)
    {
        Guid[] requiredIds =
        [
            id,
            pass2ExecutionId,
            systemId,
            consumerDocumentVersionId,
            consumerRepositoryId,
            consumerScanRunId,
            providerRepositoryId,
            providerXmlDocumentVersionId,
            providerImplementationDocumentVersionId,
            exactResolutionDecisionId,
            exactResolutionEdgeId,
        ];
        if (requiredIds.Any(value => value == Guid.Empty) ||
            consumerRepositoryId == providerRepositoryId ||
            providerXmlDocumentVersionId == providerImplementationDocumentVersionId ||
            groupOrder < 1 ||
            createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The planned Flow Group identity is invalid.");
        }

        return new FlowGroup
        {
            Id = id,
            SchemaIdentity = "endpoint-flow-group/1.0",
            Pass2ExecutionId = pass2ExecutionId,
            SystemId = systemId,
            Protocol = protocol,
            NormalizedEndpointIdentifier = RequireText(normalizedEndpointIdentifier),
            ConsumerEntryIdentity = RequireText(consumerEntryIdentity),
            ConsumerEntryStableKey = RequireText(consumerEntryStableKey),
            LogicalKeySchemaIdentity = "business-flow-logical-key/1.0",
            LogicalDocumentStableKey = RequireText(logicalDocumentStableKey),
            ConsumerDocumentVersionId = consumerDocumentVersionId,
            ProviderRepositoryId = providerRepositoryId,
            ProviderXmlDocumentVersionId = providerXmlDocumentVersionId,
            ProviderImplementationDocumentVersionId = providerImplementationDocumentVersionId,
            ExactResolutionDecisionId = exactResolutionDecisionId,
            ExactResolutionEdgeId = exactResolutionEdgeId,
            State = "Complete",
            PlanFingerprint = RequireFingerprint(planFingerprint),
            MembershipFingerprint = RequireFingerprint(membershipFingerprint),
            RepositoryId = consumerRepositoryId,
            ScanRunId = consumerScanRunId,
            GroupOrder = groupOrder,
            Title = RequireText(normalizedEndpointIdentifier),
            CreatedAt = createdAt,
        };
    }

    public static FlowGroup CreateIncomplete(
        Guid id,
        Guid pass2ExecutionId,
        Guid systemId,
        ExactResolutionProtocol protocol,
        string normalizedEndpointIdentifier,
        string consumerEntryIdentity,
        string consumerEntryStableKey,
        string logicalDocumentStableKey,
        Guid consumerDocumentVersionId,
        Guid consumerRepositoryId,
        Guid consumerScanRunId,
        Guid providerRepositoryId,
        Guid providerXmlDocumentVersionId,
        Guid exactResolutionDecisionId,
        Guid exactResolutionEdgeId,
        int groupOrder,
        string planFingerprint,
        string membershipFingerprint,
        string reasonCode,
        DateTimeOffset createdAt)
    {
        Guid[] requiredIds =
        [
            id,
            pass2ExecutionId,
            systemId,
            consumerDocumentVersionId,
            consumerRepositoryId,
            consumerScanRunId,
            providerRepositoryId,
            providerXmlDocumentVersionId,
            exactResolutionDecisionId,
            exactResolutionEdgeId,
        ];
        if (requiredIds.Any(value => value == Guid.Empty) ||
            consumerRepositoryId == providerRepositoryId ||
            groupOrder < 1 ||
            createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The incomplete Flow Group identity is invalid.");
        }

        return new FlowGroup
        {
            Id = id,
            SchemaIdentity = "endpoint-flow-group/1.0",
            Pass2ExecutionId = pass2ExecutionId,
            SystemId = systemId,
            Protocol = protocol,
            NormalizedEndpointIdentifier = RequireText(normalizedEndpointIdentifier),
            ConsumerEntryIdentity = RequireText(consumerEntryIdentity),
            ConsumerEntryStableKey = RequireText(consumerEntryStableKey),
            LogicalKeySchemaIdentity = "business-flow-logical-key/1.0",
            LogicalDocumentStableKey = RequireText(logicalDocumentStableKey),
            ConsumerDocumentVersionId = consumerDocumentVersionId,
            ProviderRepositoryId = providerRepositoryId,
            ProviderXmlDocumentVersionId = providerXmlDocumentVersionId,
            ExactResolutionDecisionId = exactResolutionDecisionId,
            ExactResolutionEdgeId = exactResolutionEdgeId,
            State = "Incomplete",
            ReasonCode = RequireText(reasonCode),
            PlanFingerprint = RequireFingerprint(planFingerprint),
            MembershipFingerprint = RequireFingerprint(membershipFingerprint),
            RepositoryId = consumerRepositoryId,
            ScanRunId = consumerScanRunId,
            GroupOrder = groupOrder,
            Title = RequireText(normalizedEndpointIdentifier),
            CreatedAt = createdAt,
        };
    }

    public static FlowGroup CreateBoundExcluded(
        Guid id,
        Guid pass2ExecutionId,
        Guid systemId,
        ExactResolutionProtocol protocol,
        string normalizedEndpointIdentifier,
        string consumerEntryIdentity,
        string consumerEntryStableKey,
        string logicalDocumentStableKey,
        Guid consumerDocumentVersionId,
        Guid consumerRepositoryId,
        Guid consumerScanRunId,
        Guid providerRepositoryId,
        Guid providerXmlDocumentVersionId,
        Guid providerImplementationDocumentVersionId,
        Guid exactResolutionDecisionId,
        Guid exactResolutionEdgeId,
        int groupOrder,
        string planFingerprint,
        string membershipFingerprint,
        DateTimeOffset createdAt)
    {
        FlowGroup group = CreatePlanned(
            id,
            pass2ExecutionId,
            systemId,
            protocol,
            normalizedEndpointIdentifier,
            consumerEntryIdentity,
            consumerEntryStableKey,
            logicalDocumentStableKey,
            consumerDocumentVersionId,
            consumerRepositoryId,
            consumerScanRunId,
            providerRepositoryId,
            providerXmlDocumentVersionId,
            providerImplementationDocumentVersionId,
            exactResolutionDecisionId,
            exactResolutionEdgeId,
            groupOrder,
            planFingerprint,
            membershipFingerprint,
            createdAt);
        group.State = "BoundExcluded";
        group.ReasonCode = "flow-group-bound";
        return group;
    }

    public Guid Id { get; private set; }
    public Guid? BusinessLogicDocumentVersionId { get; private set; }
    public string? SchemaIdentity { get; private set; }
    public Guid? Pass2ExecutionId { get; private set; }
    public Guid? SystemId { get; private set; }
    public ExactResolutionProtocol? Protocol { get; private set; }
    public string? NormalizedEndpointIdentifier { get; private set; }
    public string? ConsumerEntryIdentity { get; private set; }
    public string? ConsumerEntryStableKey { get; private set; }
    public string? LogicalKeySchemaIdentity { get; private set; }
    public string LogicalDocumentStableKey { get; private set; } = string.Empty;
    public Guid? ConsumerDocumentVersionId { get; private set; }
    public Guid? ProviderRepositoryId { get; private set; }
    public Guid? ProviderXmlDocumentVersionId { get; private set; }
    public Guid? ProviderImplementationDocumentVersionId { get; private set; }
    public Guid? ExactResolutionDecisionId { get; private set; }
    public Guid? ExactResolutionEdgeId { get; private set; }
    public string State { get; private set; } = string.Empty;
    public string PlanFingerprint { get; private set; } = string.Empty;
    public string? MembershipFingerprint { get; private set; }
    public string? ReasonCode { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public int GroupOrder { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DocumentVersion? BusinessLogicDocumentVersion { get; private set; }
    public ICollection<FlowTrace> Traces { get; } = [];

    public void Publish(Guid businessLogicDocumentVersionId)
    {
        if (businessLogicDocumentVersionId == Guid.Empty ||
            State != "Complete" || BusinessLogicDocumentVersionId is not null)
        {
            throw new InvalidOperationException("Only a complete unpublished Flow Group can publish.");
        }

        BusinessLogicDocumentVersionId = businessLogicDocumentVersionId;
        State = "Published";
    }

    private static string RequireText(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }

    private static string RequireFingerprint(string value)
    {
        if (value is not { Length: 64 } || value.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("A lower-case SHA-256 fingerprint is required.", nameof(value));
        }

        return value;
    }
}
