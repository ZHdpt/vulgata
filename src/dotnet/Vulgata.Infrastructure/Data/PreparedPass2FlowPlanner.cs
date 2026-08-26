using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed record PreparedPass2FlowPlanningResult(
    IReadOnlyList<FlowGroup> FlowGroups,
    IReadOnlyList<Pass2Omission> Omissions);

public sealed class PreparedPass2FlowPlanner(VulgataDbContext dbContext)
{
    private const string _flowSchema = "endpoint-flow-group/1.0";
    private const string _logicalKeySchema = "business-flow-logical-key/1.0";
    private readonly VulgataDbContext _dbContext = dbContext ??
        throw new ArgumentNullException(nameof(dbContext));

    public async Task<PreparedPass2FlowPlanningResult> PlanAsync(
        Guid pass2ExecutionId,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfEqual(pass2ExecutionId, Guid.Empty);
        cancellationToken.ThrowIfCancellationRequested();

        Pass2Execution execution = await _dbContext.Pass2Executions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == pass2ExecutionId, cancellationToken)
            .ConfigureAwait(false) ?? throw IntegrityFailure();
        Pass1PairExecution pair = await _dbContext.Pass1PairExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == execution.Pass1PairExecutionId, cancellationToken)
            .ConfigureAwait(false) ?? throw IntegrityFailure();
        if (execution.State != Pass2ExecutionState.Planning ||
            pair.Phase != Pass1PairExecutionPhase.ReadyForResolution ||
            pair.CoordinationId != execution.CoordinationId ||
            pair.SystemId != execution.SystemId)
        {
            throw IntegrityFailure();
        }

        CrossRepositoryResolutionDecision[] decisions = await _dbContext
            .CrossRepositoryResolutionDecisions.AsNoTracking()
            .Where(value =>
                value.Pass1PairExecutionId == pair.Id &&
                value.CoordinationId == pair.CoordinationId &&
                value.SystemId == pair.SystemId &&
                value.Outcome == "ExactMatch")
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        decisions = decisions
            .OrderBy(value => value.Protocol)
            .ThenBy(value => value.NormalizedEndpointIdentifier, StringComparer.Ordinal)
            .ThenBy(value => value.ConsumerEntryStableKey, StringComparer.Ordinal)
            .ThenBy(value => GuidKey(value.Id), StringComparer.Ordinal)
            .ToArray();
        if (decisions.Length == 0)
        {
            return new(Array.Empty<FlowGroup>(), Array.Empty<Pass2Omission>());
        }

        Guid[] decisionIds = decisions.Select(value => value.Id).ToArray();
        ExactResolutionProviderEvidenceContribution[] contributions = await _dbContext
            .ExactResolutionProviderEvidenceContributions.AsNoTracking()
            .Where(value => decisionIds.Contains(value.ResolutionDecisionId))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] edgeIds = decisions.Select(value => value.DocumentEdgeId ?? Guid.Empty)
            .Distinct().ToArray();
        DocumentEdge[] crossEdges = await _dbContext.DocumentEdges.AsNoTracking()
            .Where(value => edgeIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

        var prepared = new List<PreparedGroup>(decisions.Length);
        foreach (CrossRepositoryResolutionDecision decision in decisions)
        {
            prepared.Add(await PrepareAsync(
                execution,
                pair,
                decision,
                contributions.Where(value => value.ResolutionDecisionId == decision.Id).ToArray(),
                crossEdges,
                scope,
                cancellationToken).ConfigureAwait(false));
        }

        var groups = new List<FlowGroup>(prepared.Count);
        for (int index = 0; index < prepared.Count; index++)
        {
            PreparedGroup value = prepared[index];
            int groupOrder = index + 1;
            int documentCount = value.ProviderImplementation is null
                ? 2
                : 3 + value.OptionalMembers.Count;
            int evidenceStepCount = value.ProviderImplementation is null
                ? 4
                : 5 + (value.OptionalMembers.Count * 2);
            bool boundExcluded = value.IncompleteReason is null &&
                (documentCount > Pass2ExecutionContracts.MaxDocumentVersions ||
                    evidenceStepCount > Pass2ExecutionContracts.MaxEvidenceSteps);
            string membershipFingerprint = ComputeMembershipFingerprint(
                value.Consumer.Id,
                value.CrossEdge.Id,
                value.ProviderXml.Id,
                value.ImplementationEdge?.Id,
                value.ProviderImplementation?.Id,
                value.OptionalMembers.Select(item => (item.Edge.Id, item.Document.Id)));
            string planFingerprint = ComputePlanFingerprint(
                value.Decision.Id,
                value.Decision.EvidenceFingerprint,
                boundExcluded ? "flow-group-bound" : value.IncompleteReason ?? "Complete",
                membershipFingerprint,
                value.Contributions);
            Guid groupId = Uuid8(HashBytes(
                _flowSchema,
                pair.Id,
                execution.SystemId,
                value.Decision.Protocol,
                value.Decision.NormalizedEndpointIdentifier,
                value.Decision.ConsumerEntryIdentity,
                value.Decision.Id,
                value.CrossEdge.Id,
                value.ProviderXml.Id,
                value.ProviderXml.RepositoryId));
            string logicalStableKey = "business-flow/v1/" + Hash(
                _logicalKeySchema,
                execution.SystemId,
                value.Consumer.RepositoryId,
                value.ProviderXml.RepositoryId,
                value.Decision.Protocol,
                value.Decision.NormalizedEndpointIdentifier,
                value.Decision.ConsumerEntryStableKey);
            FlowGroup group;
            if (boundExcluded)
            {
                group = FlowGroup.CreateBoundExcluded(
                    groupId,
                    execution.Id,
                    execution.SystemId,
                    value.Decision.Protocol,
                    value.Decision.NormalizedEndpointIdentifier,
                    value.Decision.ConsumerEntryIdentity,
                    value.Decision.ConsumerEntryStableKey,
                    logicalStableKey,
                    value.Consumer.Id,
                    value.Consumer.RepositoryId,
                    value.Consumer.ScanRunId,
                    value.ProviderXml.RepositoryId,
                    value.ProviderXml.Id,
                    value.ProviderImplementation!.Id,
                    value.Decision.Id,
                    value.CrossEdge.Id,
                    groupOrder,
                    planFingerprint,
                    membershipFingerprint,
                    execution.StartedAt);
                AddTrace(group, value, execution.StartedAt);
            }
            else if (value.IncompleteReason is null)
            {
                group = FlowGroup.CreatePlanned(
                    groupId,
                    execution.Id,
                    execution.SystemId,
                    value.Decision.Protocol,
                    value.Decision.NormalizedEndpointIdentifier,
                    value.Decision.ConsumerEntryIdentity,
                    value.Decision.ConsumerEntryStableKey,
                    logicalStableKey,
                    value.Consumer.Id,
                    value.Consumer.RepositoryId,
                    value.Consumer.ScanRunId,
                    value.ProviderXml.RepositoryId,
                    value.ProviderXml.Id,
                    value.ProviderImplementation!.Id,
                    value.Decision.Id,
                    value.CrossEdge.Id,
                    groupOrder,
                    planFingerprint,
                    membershipFingerprint,
                    execution.StartedAt);
                AddTrace(group, value, execution.StartedAt);
            }
            else
            {
                group = FlowGroup.CreateIncomplete(
                    groupId,
                    execution.Id,
                    execution.SystemId,
                    value.Decision.Protocol,
                    value.Decision.NormalizedEndpointIdentifier,
                    value.Decision.ConsumerEntryIdentity,
                    value.Decision.ConsumerEntryStableKey,
                    logicalStableKey,
                    value.Consumer.Id,
                    value.Consumer.RepositoryId,
                    value.Consumer.ScanRunId,
                    value.ProviderXml.RepositoryId,
                    value.ProviderXml.Id,
                    value.Decision.Id,
                    value.CrossEdge.Id,
                    groupOrder,
                    planFingerprint,
                    membershipFingerprint,
                    value.IncompleteReason,
                    execution.StartedAt);
                AddIncompleteTrace(group, value, execution.StartedAt);
            }
            groups.Add(group);
        }

        return new(groups.AsReadOnly(), Array.Empty<Pass2Omission>());
    }

    private async Task<PreparedGroup> PrepareAsync(
        Pass2Execution execution,
        Pass1PairExecution pair,
        CrossRepositoryResolutionDecision decision,
        IReadOnlyList<ExactResolutionProviderEvidenceContribution> contributions,
        IReadOnlyList<DocumentEdge> crossEdges,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken)
    {
        if (decision.DocumentEdgeId is not Guid crossEdgeId ||
            decision.ProviderDocumentVersionId is not Guid providerXmlId ||
            contributions.Count == 0 ||
            contributions.Any(value => value.ProviderDocumentVersionId != providerXmlId))
        {
            throw IntegrityFailure();
        }

        DocumentEdge crossEdge = crossEdges.SingleOrDefault(value => value.Id == crossEdgeId) ??
            throw IntegrityFailure();
        if (crossEdge.EdgeType != DocumentEdgeType.References ||
            crossEdge.SourceDocumentVersionId != decision.ConsumerDocumentVersionId ||
            crossEdge.TargetDocumentVersionId != providerXmlId ||
            crossEdge.SourceRepositoryId == crossEdge.TargetRepositoryId)
        {
            throw IntegrityFailure();
        }

        DocumentVersion[] members = await _dbContext.DocumentVersions.AsNoTracking()
            .Where(value => value.Id == decision.ConsumerDocumentVersionId || value.Id == providerXmlId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        DocumentVersion consumer = members.SingleOrDefault(
            value => value.Id == decision.ConsumerDocumentVersionId) ?? throw IntegrityFailure();
        DocumentVersion providerXml = members.SingleOrDefault(
            value => value.Id == providerXmlId) ?? throw IntegrityFailure();
        LogicalDocument[] currentCoreOwners = await _dbContext.LogicalDocuments.AsNoTracking()
            .Where(value =>
                value.DocumentType == DocumentType.CodeLogic &&
                value.CurrentVersionId.HasValue &&
                (value.CurrentVersionId.Value == consumer.Id ||
                    value.CurrentVersionId.Value == providerXml.Id))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (currentCoreOwners.Length != 2 ||
            consumer.SystemId != execution.SystemId ||
            providerXml.SystemId != execution.SystemId ||
            consumer.RepositoryId != crossEdge.SourceRepositoryId ||
            consumer.ScanRunId != crossEdge.SourceScanRunId ||
            providerXml.RepositoryId != crossEdge.TargetRepositoryId ||
            providerXml.ScanRunId != crossEdge.TargetScanRunId ||
            decision.Pass1PairExecutionId != pair.Id)
        {
            throw IntegrityFailure();
        }

        Guid[] repositories = [consumer.RepositoryId, providerXml.RepositoryId];
        if (repositories.Distinct().Count() != 2 ||
            repositories.Any(value => !scope.Contains(value)) ||
            !await scope.HasCurrentAccessAsync(repositories, cancellationToken).ConfigureAwait(false))
        {
            return Incomplete(
                decision, contributions, crossEdge, consumer, providerXml,
                "unauthorized-core-member");
        }

        DocumentEdge[] implementationEdges = await _dbContext.DocumentEdges.AsNoTracking()
            .Where(value =>
                value.SourceDocumentVersionId == providerXml.Id &&
                value.SourceRepositoryId == providerXml.RepositoryId &&
                value.TargetRepositoryId == providerXml.RepositoryId &&
                value.EdgeType == DocumentEdgeType.References)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (implementationEdges.Length == 0)
        {
            return Incomplete(
                decision, contributions, crossEdge, consumer, providerXml,
                "missing-provider-implementation");
        }

        if (implementationEdges.Length > 1)
        {
            return Incomplete(
                decision, contributions, crossEdge, consumer, providerXml,
                "multiple-provider-implementations");
        }

        DocumentEdge implementationEdge = implementationEdges[0];
        DocumentVersion providerImplementation = await _dbContext.DocumentVersions.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == implementationEdge.TargetDocumentVersionId,
                cancellationToken).ConfigureAwait(false) ?? throw IntegrityFailure();
        if (!StringComparer.Ordinal.Equals(
                providerImplementation.ProfiledRevision,
                providerXml.ProfiledRevision))
        {
            return Incomplete(
                decision, contributions, crossEdge, consumer, providerXml,
                "wrong-revision-provider-implementation");
        }

        bool hasImplementationAnchor = await _dbContext.SourceAnchors.AsNoTracking()
            .AnyAsync(value =>
                value.DocumentVersionId == providerImplementation.Id &&
                value.RepositoryId == providerImplementation.RepositoryId &&
                value.ScanRunId == providerImplementation.ScanRunId &&
                value.ProfiledRevision == providerImplementation.ProfiledRevision,
                cancellationToken).ConfigureAwait(false);
        if (!hasImplementationAnchor)
        {
            return Incomplete(
                decision, contributions, crossEdge, consumer, providerXml,
                "unanchored-provider-implementation");
        }

        bool implementationIsCurrent = await _dbContext.LogicalDocuments.AsNoTracking()
            .AnyAsync(value =>
                value.DocumentType == DocumentType.CodeLogic &&
                value.RepositoryId == providerImplementation.RepositoryId &&
                value.CurrentVersionId == providerImplementation.Id,
                cancellationToken).ConfigureAwait(false);
        if (!implementationIsCurrent)
        {
            return Incomplete(
                decision, contributions, crossEdge, consumer, providerXml,
                "stale-provider-implementation");
        }

        if (providerImplementation.SystemId != execution.SystemId)
        {
            return Incomplete(
                decision, contributions, crossEdge, consumer, providerXml,
                "foreign-system-provider-implementation");
        }

        if (providerImplementation.RepositoryId != implementationEdge.TargetRepositoryId ||
            providerImplementation.ScanRunId != implementationEdge.TargetScanRunId)
        {
            throw IntegrityFailure();
        }

        Guid[] coreIds = [consumer.Id, providerXml.Id, providerImplementation.Id];
        DocumentEdge[] optionalEdges = await _dbContext.DocumentEdges.AsNoTracking()
            .Where(value =>
                coreIds.Contains(value.SourceDocumentVersionId) &&
                value.SourceRepositoryId == value.TargetRepositoryId &&
                value.EdgeType == DocumentEdgeType.References &&
                value.Id != implementationEdge.Id &&
                !coreIds.Contains(value.TargetDocumentVersionId))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] optionalTargetIds = optionalEdges.Select(value => value.TargetDocumentVersionId)
            .Distinct().ToArray();
        DocumentVersion[] optionalDocuments = await _dbContext.DocumentVersions.AsNoTracking()
            .Where(value => optionalTargetIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var optionalMembers = new List<OptionalMember>();
        foreach (IGrouping<Guid, DocumentEdge> targetEdges in optionalEdges
            .GroupBy(value => value.TargetDocumentVersionId))
        {
            DocumentEdge edge = targetEdges
                .OrderBy(value => GuidKey(value.Id), StringComparer.Ordinal).First();
            DocumentVersion? document = optionalDocuments.SingleOrDefault(
                value => value.Id == targetEdges.Key);
            DocumentVersion source = coreIds.Contains(edge.SourceDocumentVersionId)
                ? new[] { consumer, providerXml, providerImplementation }.Single(
                    value => value.Id == edge.SourceDocumentVersionId)
                : throw IntegrityFailure();
            if (document is null ||
                document.RepositoryId != edge.TargetRepositoryId ||
                document.ScanRunId != edge.TargetScanRunId ||
                document.SystemId != execution.SystemId ||
                !StringComparer.Ordinal.Equals(document.ProfiledRevision, source.ProfiledRevision))
            {
                continue;
            }

            bool current = await _dbContext.LogicalDocuments.AsNoTracking()
                .AnyAsync(value =>
                    value.DocumentType == DocumentType.CodeLogic &&
                    value.RepositoryId == document.RepositoryId &&
                    value.CurrentVersionId == document.Id,
                    cancellationToken).ConfigureAwait(false);
            bool anchored = await _dbContext.SourceAnchors.AsNoTracking()
                .AnyAsync(value =>
                    value.DocumentVersionId == document.Id &&
                    value.RepositoryId == document.RepositoryId &&
                    value.ScanRunId == document.ScanRunId &&
                    value.ProfiledRevision == document.ProfiledRevision,
                    cancellationToken).ConfigureAwait(false);
            if (current && anchored)
            {
                optionalMembers.Add(new(edge, source, document));
            }
        }

        return new(decision, contributions, crossEdge, consumer, providerXml,
            implementationEdge, providerImplementation,
            optionalMembers
                .OrderBy(value => GuidKey(value.Document.RepositoryId), StringComparer.Ordinal)
                .ThenBy(value => GuidKey(value.Document.Id), StringComparer.Ordinal)
                .ThenBy(value => GuidKey(value.Edge.Id), StringComparer.Ordinal)
                .ToArray(),
            null);
    }

    private static void AddTrace(FlowGroup group, PreparedGroup value, DateTimeOffset createdAt)
    {
        Guid traceId = Uuid8(HashBytes("endpoint-flow-trace/1.0", group.Id));
        var trace = new FlowTrace(traceId, group.Id, 1, createdAt);
        (DocumentVersion Document, DocumentEdge? Edge, Guid? DecisionId, FlowTraceStepKind Kind,
            string Description)[] steps =
        [
            (value.Consumer, null, null, FlowTraceStepKind.Document, "Consumer Document"),
            (value.Consumer, value.CrossEdge, value.Decision.Id, FlowTraceStepKind.Relationship,
                "Exact Cross-Repository relationship"),
            (value.ProviderXml, null, null, FlowTraceStepKind.Document, "Provider XML Document"),
            (value.ProviderXml, value.ImplementationEdge!, null, FlowTraceStepKind.Relationship,
                "Provider implementation relationship"),
            (value.ProviderImplementation!, null, null, FlowTraceStepKind.Document,
                "Provider implementation Document"),
        ];
        var orderedSteps = new List<(DocumentVersion Document, DocumentEdge? Edge,
            Guid? DecisionId, FlowTraceStepKind Kind, string Description)>(steps);
        foreach (OptionalMember optional in value.OptionalMembers)
        {
            orderedSteps.Add((
                optional.Source,
                optional.Edge,
                null,
                FlowTraceStepKind.Relationship,
                "Same-Repository supporting relationship"));
            orderedSteps.Add((
                optional.Document,
                null,
                null,
                FlowTraceStepKind.Document,
                "Supporting Document"));
        }

        for (int index = 0; index < orderedSteps.Count; index++)
        {
            var step = orderedSteps[index];
            int order = index + 1;
            Guid stepId = Uuid8(HashBytes(
                "endpoint-flow-trace-step/1.0",
                traceId,
                order,
                step.Document.Id,
                step.Edge?.Id,
                step.DecisionId));
            trace.Steps.Add(new FlowTraceStep(
                stepId,
                traceId,
                order,
                step.Document.Id,
                step.Document.RepositoryId,
                step.Document.ScanRunId,
                step.Edge?.Id,
                step.Kind,
                step.Description,
                createdAt,
                step.DecisionId));
        }

        group.Traces.Add(trace);
    }

    private static void AddIncompleteTrace(
        FlowGroup group,
        PreparedGroup value,
        DateTimeOffset createdAt)
    {
        Guid traceId = Uuid8(HashBytes("endpoint-flow-trace/1.0", group.Id));
        var trace = new FlowTrace(traceId, group.Id, 1, createdAt);
        (DocumentVersion Document, DocumentEdge? Edge, Guid? DecisionId,
            FlowTraceStepKind Kind, string Description)[] steps =
        [
            (value.Consumer, null, null, FlowTraceStepKind.Document, "Consumer Document"),
            (value.Consumer, value.CrossEdge, value.Decision.Id, FlowTraceStepKind.Relationship,
                "Exact Cross-Repository relationship"),
            (value.ProviderXml, null, null, FlowTraceStepKind.Document, "Provider XML Document"),
            (value.ProviderXml, null, null, FlowTraceStepKind.Termination, value.IncompleteReason!),
        ];
        for (int index = 0; index < steps.Length; index++)
        {
            var step = steps[index];
            int order = index + 1;
            trace.Steps.Add(new FlowTraceStep(
                Uuid8(HashBytes(
                    "endpoint-flow-trace-step/1.0",
                    traceId,
                    order,
                    step.Document.Id,
                    step.Edge?.Id,
                    step.DecisionId)),
                traceId,
                order,
                step.Document.Id,
                step.Document.RepositoryId,
                step.Document.ScanRunId,
                step.Edge?.Id,
                step.Kind,
                step.Description,
                createdAt,
                step.DecisionId));
        }

        group.Traces.Add(trace);
    }

    private static PreparedGroup Incomplete(
        CrossRepositoryResolutionDecision decision,
        IReadOnlyList<ExactResolutionProviderEvidenceContribution> contributions,
        DocumentEdge crossEdge,
        DocumentVersion consumer,
        DocumentVersion providerXml,
        string reason) =>
        new(
            decision,
            contributions,
            crossEdge,
            consumer,
            providerXml,
            null,
            null,
            Array.Empty<OptionalMember>(),
            reason);

    private static InvalidOperationException IntegrityFailure() =>
        new("The persisted Pass 2 planning evidence is incomplete or inconsistent.");

    private static string GuidKey(Guid value) =>
        Convert.ToHexString(value.ToByteArray(bigEndian: true));

    internal static string ComputeMembershipFingerprint(
        Guid consumerDocumentVersionId,
        Guid crossRepositoryEdgeId,
        Guid providerXmlDocumentVersionId,
        Guid? implementationEdgeId,
        Guid? providerImplementationDocumentVersionId,
        IEnumerable<(Guid EdgeId, Guid DocumentVersionId)> optionalMembers)
    {
        object?[] values =
        [
            "flow-membership/1.0",
            consumerDocumentVersionId,
            crossRepositoryEdgeId,
            providerXmlDocumentVersionId,
            implementationEdgeId,
            providerImplementationDocumentVersionId,
            .. optionalMembers.SelectMany(item =>
                new object?[] { item.EdgeId, item.DocumentVersionId }),
        ];
        return Hash(values);
    }

    internal static string ComputePlanFingerprint(
        Guid decisionId,
        string decisionEvidenceFingerprint,
        string reasonCode,
        string membershipFingerprint,
        IEnumerable<ExactResolutionProviderEvidenceContribution> contributions) =>
        Hash(
            "endpoint-flow-plan/1.0",
            decisionId,
            decisionEvidenceFingerprint,
            reasonCode,
            membershipFingerprint,
            string.Join('\n', contributions
                .OrderBy(item => item.ProviderOrdinal)
                .ThenBy(item => item.EvidenceOrdinal)
                .ThenBy(item => GuidKey(item.Id), StringComparer.Ordinal)
                .Select(item => item.EvidenceFingerprint)));

    private static string Hash(params object?[] values) =>
        Convert.ToHexStringLower(HashBytes(values));

    private static byte[] HashBytes(params object?[] values) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            '\n',
            values.Select(value => value switch
            {
                null => "-",
                Guid identifier => identifier.ToString("D"),
                Enum member => member.ToString(),
                _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            }))));

    private static Guid Uuid8(ReadOnlySpan<byte> hash)
    {
        byte[] bytes = hash[..16].ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    private sealed record PreparedGroup(
        CrossRepositoryResolutionDecision Decision,
        IReadOnlyList<ExactResolutionProviderEvidenceContribution> Contributions,
        DocumentEdge CrossEdge,
        DocumentVersion Consumer,
        DocumentVersion ProviderXml,
        DocumentEdge? ImplementationEdge,
        DocumentVersion? ProviderImplementation,
        IReadOnlyList<OptionalMember> OptionalMembers,
        string? IncompleteReason);

    private sealed record OptionalMember(
        DocumentEdge Edge,
        DocumentVersion Source,
        DocumentVersion Document);
}
