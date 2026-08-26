using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class PreparedPass2DispatchRequestSource(
    VulgataDbContext context,
    IPass2ExecutionAuthorizationVerifier authorizationVerifier,
    IPreparedPass2WorkerContractAuthority workerContractAuthority,
    Microsoft.Extensions.Logging.ILogger<PreparedPass2DispatchRequestSource> logger)
    : IPreparedPass2DispatchRequestSource
{
    public async Task<PreparedPass2DispatchHydrationResult> HydrateAsync(
        Guid pass2ExecutionId,
        Guid pass2JobExecutionId,
        long jobStateVersion,
        Guid leaseToken,
        CancellationToken cancellationToken = default)
    {
        if (pass2ExecutionId == Guid.Empty || pass2JobExecutionId == Guid.Empty ||
            leaseToken == Guid.Empty)
        {
            return IntegrityRefused();
        }

        Pass2JobExecution? job = await context.Pass2JobExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.Id == pass2JobExecutionId &&
                value.Pass2ExecutionId == pass2ExecutionId,
                cancellationToken);
        if (job is null || job.State != Pass2JobExecutionState.Running ||
            job.StateVersion != jobStateVersion || job.LeaseToken != leaseToken ||
            job.LeaseExpiresAt is not { } leaseExpiresAt ||
            leaseExpiresAt <= DateTimeOffset.UtcNow || job.AttemptCount is < 1 or > 2)
        {
            return IntegrityRefused();
        }

        Pass2Execution? execution = await context.Pass2Executions.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.Id == pass2ExecutionId &&
                value.State == Pass2ExecutionState.Running,
                cancellationToken);
        FlowGroup? group = await context.FlowGroups.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.Id == job.FlowGroupId &&
                value.Pass2ExecutionId == pass2ExecutionId &&
                value.State == "Complete",
                cancellationToken);
        PreparedPass2WorkerContractAuthority workerContract =
            workerContractAuthority.GetCurrent();
        if (execution is null || group is null ||
            !StringComparer.Ordinal.Equals(
                execution.WorkerPromptIdentity, workerContract.PromptIdentity) ||
            !StringComparer.Ordinal.Equals(
                execution.WorkerPromptFingerprint, workerContract.PromptFingerprint) ||
            !StringComparer.Ordinal.Equals(
                execution.SubmissionSchemaIdentity, workerContract.SubmissionSchemaIdentity) ||
            !StringComparer.Ordinal.Equals(
                execution.SubmissionSchemaFingerprint,
                workerContract.SubmissionSchemaFingerprint) ||
            !StringComparer.Ordinal.Equals(
                execution.ToolDeclarationFingerprint, workerContract.ToolDeclarationFingerprint) ||
            group.SystemId != execution.SystemId ||
            group.ExactResolutionDecisionId is not Guid decisionId ||
            group.ExactResolutionEdgeId is not Guid edgeId ||
            group.Protocol is not { } protocol ||
            group.ProviderRepositoryId is not Guid providerRepositoryId ||
            group.ConsumerDocumentVersionId is not Guid consumerDocumentVersionId ||
            group.ProviderXmlDocumentVersionId is not Guid providerXmlDocumentVersionId ||
            group.ProviderImplementationDocumentVersionId is not Guid providerImplementationId ||
            string.IsNullOrWhiteSpace(group.NormalizedEndpointIdentifier) ||
            string.IsNullOrWhiteSpace(group.ConsumerEntryStableKey) ||
            string.IsNullOrWhiteSpace(group.MembershipFingerprint))
        {
            return IntegrityRefused();
        }

        TwoRepositoryScanCoordination? coordination = await context
            .TwoRepositoryScanCoordinations.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.Id == execution.CoordinationId &&
                value.SystemId == execution.SystemId &&
                value.Phase == TwoRepositoryScanCoordinationPhase.Pass2,
                cancellationToken);
        if (coordination is null)
        {
            return IntegrityRefused();
        }

        Guid[] repositoryIds =
            [coordination.RepositoryOneId, coordination.RepositoryTwoId];
        if (!repositoryIds.Contains(group.RepositoryId) ||
            !repositoryIds.Contains(providerRepositoryId))
        {
            return IntegrityRefused();
        }

        RepositoryAccessScope? accessScope = await Pass2ScopeResolver.ResolveAsync(
            context,
            authorizationVerifier,
            job.Id,
            cancellationToken);
        if (accessScope is null ||
            !await accessScope.HasCurrentAccessAsync(repositoryIds, cancellationToken))
        {
            return SecurityRefused();
        }

        CrossRepositoryResolutionDecision? decision = await context
            .CrossRepositoryResolutionDecisions.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.Id == decisionId &&
                value.Pass1PairExecutionId == execution.Pass1PairExecutionId &&
                value.CoordinationId == execution.CoordinationId &&
                value.SystemId == execution.SystemId &&
                value.DocumentEdgeId == edgeId &&
                value.Outcome == "ExactMatch",
                cancellationToken);
        DocumentEdge? edge = await context.DocumentEdges.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == edgeId, cancellationToken);
        ExactResolutionProviderEvidenceContribution[] contributions = await context
            .ExactResolutionProviderEvidenceContributions.AsNoTracking()
            .Where(value => value.ResolutionDecisionId == decisionId)
            .OrderBy(value => value.ProviderOrdinal)
            .ThenBy(value => value.EvidenceOrdinal)
            .ToArrayAsync(cancellationToken);
        if (decision is null || edge is null || contributions.Length == 0 ||
            decision.SystemId != execution.SystemId ||
            decision.ConsumerDocumentVersionId != consumerDocumentVersionId ||
            decision.ProviderDocumentVersionId != providerXmlDocumentVersionId ||
            decision.Protocol != protocol ||
            !StringComparer.Ordinal.Equals(
                decision.NormalizedEndpointIdentifier, group.NormalizedEndpointIdentifier) ||
            !StringComparer.Ordinal.Equals(
                decision.ConsumerEntryStableKey, group.ConsumerEntryStableKey) ||
            !StringComparer.Ordinal.Equals(
                decision.ConsumerEntryIdentity, group.ConsumerEntryIdentity) ||
            edge.EdgeType != DocumentEdgeType.References ||
            edge.SourceDocumentVersionId != consumerDocumentVersionId ||
            edge.TargetDocumentVersionId != providerXmlDocumentVersionId ||
            edge.SourceRepositoryId != group.RepositoryId ||
            edge.TargetRepositoryId != providerRepositoryId ||
            edge.SourceScanRunId != group.ScanRunId ||
            contributions.Select(value => value.ProviderOrdinal).Distinct().Count() != 1 ||
            !contributions.Select(value => value.EvidenceOrdinal)
                .SequenceEqual(Enumerable.Range(1, contributions.Length)) ||
            !ExactResolutionCanonicalAuthority.MatchesPersistedExactAuthority(
                decision, edge, contributions) ||
            !StringComparer.Ordinal.Equals(group.SchemaIdentity, "endpoint-flow-group/1.0") ||
            !StringComparer.Ordinal.Equals(
                group.LogicalKeySchemaIdentity, "business-flow-logical-key/1.0") ||
            !StringComparer.Ordinal.Equals(
                group.LogicalDocumentStableKey,
                CanonicalLogicalStableKey(execution, group, decision)))
        {
            logger.LogWarning(
                "Pass 2 dispatch hydration refused {ExecutionId} with integrity code {Code}.",
                pass2ExecutionId,
                "exact-authority-invalid");
            return IntegrityRefused();
        }

        Guid[] coreDocumentIds =
            [consumerDocumentVersionId, providerXmlDocumentVersionId, providerImplementationId];
        FlowTrace[] traces = await context.FlowTraces.AsNoTracking()
            .Where(value => value.FlowGroupId == group.Id)
            .OrderBy(value => value.TraceOrder)
            .ToArrayAsync(cancellationToken);
        Guid[] traceIds = traces.Select(value => value.Id).ToArray();
        FlowTraceStep[] traceSteps = await context.FlowTraceSteps.AsNoTracking()
            .Where(step => traceIds.Contains(step.FlowTraceId))
            .OrderBy(step => step.FlowTraceId)
            .ThenBy(step => step.StepOrder)
            .ToArrayAsync(cancellationToken);
        Guid[] supportingEdgeIds = traceSteps
            .Where(value => value.StepKind == FlowTraceStepKind.Relationship &&
                value.EdgeId.HasValue && value.EdgeId != edgeId)
            .Select(value => value.EdgeId!.Value)
            .Distinct()
            .ToArray();
        DocumentEdge[] supportingEdges = await context.DocumentEdges.AsNoTracking()
            .Where(value => supportingEdgeIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        if (traces.Length != 1 || traces[0].TraceOrder != 1 ||
            !ValidateCanonicalTrace(group, decision, contributions, traces[0], traceSteps) ||
            supportingEdges.Length != supportingEdgeIds.Length ||
            !Pass2RelationshipAuthority.ValidateSupportingEdges(
                group, providerRepositoryId, traceSteps, supportingEdges))
        {
            logger.LogWarning(
                "Pass 2 dispatch hydration refused {ExecutionId} with integrity code {Code}.",
                pass2ExecutionId,
                "trace-authority-invalid");
            return IntegrityRefused();
        }
        Guid[] memberDocumentIds = coreDocumentIds
            .Concat(traceSteps.Select(value => value.DocumentVersionId))
            .Distinct()
            .ToArray();
        DocumentVersion[] documents = await context.DocumentVersions.AsNoTracking()
            .Where(value => memberDocumentIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        SourceAnchor[] anchors = await context.SourceAnchors.AsNoTracking()
            .Where(value => memberDocumentIds.Contains(value.DocumentVersionId))
            .ToArrayAsync(cancellationToken);
        Guid[] codeUnitIds = anchors.Select(value => value.CodeUnitId).Distinct().ToArray();
        CodeUnit[] codeUnits = await context.CodeUnits.AsNoTracking()
            .Where(value => codeUnitIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        Guid[] logicalDocumentIds = documents.Select(value => value.LogicalDocumentId).ToArray();
        LogicalDocument[] logicalDocuments = await context.LogicalDocuments.AsNoTracking()
            .Where(value => logicalDocumentIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        if (documents.Length != memberDocumentIds.Length ||
            logicalDocuments.Length != logicalDocumentIds.Distinct().Count() ||
            codeUnits.Length != codeUnitIds.Length ||
            !documents.Any(value =>
                value.Id == decision.ConsumerDocumentVersionId &&
                value.RepositoryId == decision.ConsumerRepositoryId &&
                value.ScanRunId == decision.ConsumerScanRunId &&
                StringComparer.Ordinal.Equals(
                    value.ProfiledRevision, decision.ConsumerProfiledRevision)) ||
            !documents.Any(value =>
                value.Id == decision.ProviderDocumentVersionId &&
                value.RepositoryId == decision.ProviderRepositoryId &&
                value.ScanRunId == decision.ProviderScanRunId &&
                StringComparer.Ordinal.Equals(
                    value.ProfiledRevision, decision.ProviderProfiledRevision)) ||
            documents.Any(value =>
                value.SystemId != execution.SystemId ||
                !repositoryIds.Contains(value.RepositoryId) ||
                value.SupersededById is not null ||
                value.ScanRunId != ExpectedScanRunId(value.RepositoryId, coordination) ||
                !StringComparer.Ordinal.Equals(
                    value.ProfiledRevision,
                    ExpectedProfiledRevision(value.RepositoryId, coordination)) ||
                !logicalDocuments.Any(logical =>
                    logical.Id == value.LogicalDocumentId &&
                    logical.RepositoryId == value.RepositoryId &&
                    logical.DocumentType == DocumentType.CodeLogic &&
                    logical.CurrentVersionId == value.Id) ||
                !anchors.Any(anchor => anchor.DocumentVersionId == value.Id)) ||
            anchors.Any(anchor =>
                !documents.Any(document =>
                    document.Id == anchor.DocumentVersionId &&
                    document.RepositoryId == anchor.RepositoryId &&
                    document.ScanRunId == anchor.ScanRunId &&
                    StringComparer.Ordinal.Equals(
                        document.ProfiledRevision, anchor.ProfiledRevision)) ||
                !AnchorMatchesCodeUnit(
                    anchor,
                    codeUnits.Single(value => value.Id == anchor.CodeUnitId))) ||
            contributions.Any(contribution => !anchors.Any(anchor =>
                anchor.Id == contribution.SourceAnchorId &&
                anchor.DocumentVersionId == contribution.ProviderDocumentVersionId &&
                anchor.DocumentVersionId == providerXmlDocumentVersionId)) ||
            traceSteps.Any(step => !documents.Any(document =>
                document.Id == step.DocumentVersionId &&
                document.RepositoryId == step.DocumentRepositoryId &&
                document.ScanRunId == step.DocumentScanRunId)))
        {
            return IntegrityRefused();
        }

        DocumentVersion[] orderedDocuments = memberDocumentIds
            .Select(id => documents.Single(value => value.Id == id))
            .ToArray();
        ScanJobExecution[] completedScanJobs = await context.ScanJobExecutions.AsNoTracking()
            .Where(value =>
                value.State == ScanJobExecutionState.Completed &&
                value.CommittedDocumentVersionId != null &&
                memberDocumentIds.Contains(value.CommittedDocumentVersionId.Value))
            .ToArrayAsync(cancellationToken);
        PreparedScanManifestEntry[] manifestEntries = await context.PreparedScanManifestEntries
            .AsNoTracking()
            .Where(value =>
                value.CoordinationId == coordination.Id &&
                codeUnitIds.Contains(value.FileCodeUnitId))
            .ToArrayAsync(cancellationToken);
        var runtimeMembers = new List<PreparedPass2RuntimeMember>(orderedDocuments.Length);
        var traceEvidence = new List<PreparedPass2TraceEvidence>(orderedDocuments.Length);
        var initialEvidence = new List<Pass2BusinessLogicEvidenceBinding>();
        for (int index = 0; index < orderedDocuments.Length; index++)
        {
            DocumentVersion document = orderedDocuments[index];
            SourceAnchor[] documentAnchors = anchors
                .Where(value => value.DocumentVersionId == document.Id)
                .OrderBy(value => GuidKey(value.Id), StringComparer.Ordinal)
                .ToArray();
            int order = index + 1;
            Guid codeUnitId = documentAnchors[0].CodeUnitId;
            Guid scanExecutionId = ExpectedScanExecutionId(document.RepositoryId, coordination);
            PreparedScanManifestEntry[] matchingEntries = manifestEntries
                .Where(value => value.FileCodeUnitId == codeUnitId)
                .ToArray();
            ScanJobExecution[] matchingScanJobs = completedScanJobs.Where(value =>
                    value.ScanExecutionId == scanExecutionId &&
                    value.ScanRunId == document.ScanRunId &&
                    value.RepositoryId == document.RepositoryId &&
                    value.FileCodeUnitId == codeUnitId &&
                    matchingEntries.Length == 1 &&
                    value.ManifestOrdinal == matchingEntries[0].ManifestOrdinal &&
                    value.CommittedDocumentVersionId == document.Id)
                .ToArray();
            OrdinaryPass1JobIdentity memberIdentity;
            if (documentAnchors.Any(value => value.CodeUnitId != codeUnitId) ||
                matchingEntries.Length != 1 || matchingScanJobs.Length != 1 ||
                ScanJobExecution.Create(
                    scanExecutionId,
                    document.RepositoryId,
                    document.ScanRunId,
                    codeUnitId,
                    matchingEntries[0].ManifestOrdinal,
                    matchingScanJobs[0].StateChangedAt).Id != matchingScanJobs[0].Id)
            {
                return IntegrityRefused();
            }
            try
            {
                memberIdentity = OrdinaryPass1JobIdentity.Create(
                    scanExecutionId,
                    document.ScanRunId,
                    document.RepositoryId,
                    codeUnitId,
                    document.ProfiledRevision);
            }
            catch (ArgumentException)
            {
                return IntegrityRefused();
            }
            if (OrdinaryPass1JobIdentity.CreateDocumentVersionId(memberIdentity.JobId) != document.Id)
            {
                return IntegrityRefused();
            }
            Guid memberJobId = memberIdentity.JobId;
            runtimeMembers.Add(new(
                order,
                memberJobId,
                codeUnitId,
                document.LogicalDocumentId,
                document.RepositoryId,
                document.Id,
                Array.AsReadOnly(documentAnchors.Select(value => value.Id).ToArray())));
            traceEvidence.Add(new(
                order,
                memberJobId,
                codeUnitId,
                $"Flow member {order}",
                $"flow-member/{order}"));
            initialEvidence.Add(CreateDocumentBinding(job, document));
            initialEvidence.AddRange(documentAnchors.Select(anchor =>
                CreateAnchorBinding(job, document, anchor)));
        }

        DocumentVersion consumer = documents.Single(value => value.Id == consumerDocumentVersionId);
        initialEvidence.Add(new(
            Pass2InitialEvidenceHandle.Create(
                job.Id,
                job.AttemptCount,
                Pass2CodeLogicRetrievalItemKind.Relationship,
                consumer.Id,
                consumer.RepositoryId,
                consumer.ScanRunId,
                consumer.ProfiledRevision,
                edgeId: edgeId),
            consumer.Id,
            consumer.RepositoryId,
            consumer.ScanRunId,
            consumer.ProfiledRevision,
            EdgeId: edgeId));

        Repository[] repositories = await context.Repositories.AsNoTracking()
            .Where(value => repositoryIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        Pass1PairExecution? pair = await context.Pass1PairExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.Id == execution.Pass1PairExecutionId &&
                value.CoordinationId == coordination.Id &&
                value.SystemId == execution.SystemId,
                cancellationToken);
        Guid[] manifestIds = pair is null
            ? []
            : [pair.ManifestOneId, pair.ManifestTwoId];
        PreparedScanManifest[] manifests = await context.PreparedScanManifests.AsNoTracking()
            .Where(value => manifestIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        if (repositories.Length != 2 || manifests.Length != 2 || manifests.Any(value =>
                value.CoordinationId != coordination.Id ||
                !repositoryIds.Contains(value.RepositoryId)))
        {
            return IntegrityRefused();
        }

        PreparedPass2RuntimeRepositoryBinding consumerRepository = RepositoryBinding(
            "consumer", group.RepositoryId, coordination, repositories, manifests,
            pair!);
        PreparedPass2RuntimeRepositoryBinding providerRepository = RepositoryBinding(
            "provider", providerRepositoryId, coordination, repositories, manifests,
            pair!);
        Guid[] contributionIds = contributions.Select(value => value.Id)
            .OrderBy(GuidKey, StringComparer.Ordinal)
            .ToArray();

        PreparedPass2RuntimeBinding binding;
        try
        {
            binding = PreparedPass2RuntimeBinding.Create(
                "1.0",
                "exact-resolution-decision",
                "1.0",
                decision.EvidenceFingerprint,
                decision.ConsumerEntryIdentity,
                execution.SystemId,
                consumerRepository,
                providerRepository,
                edgeId,
                contributionIds,
                runtimeMembers.AsReadOnly(),
                traceEvidence.AsReadOnly());
        }
        catch (Exception exception) when (
            exception is ArgumentException or PreparedPass2FlowContractException)
        {
            logger.LogWarning(
                "Pass 2 dispatch hydration refused {ExecutionId} at runtime-binding.",
                pass2ExecutionId);
            return IntegrityRefused();
        }

        string providerEvidenceFingerprint =
            ExactResolutionCanonicalAuthority.ProviderEvidenceSetFingerprint(
                contributions.Select(value => value.EvidenceFingerprint));
        if (!StringComparer.Ordinal.Equals(
                decision.ProviderEvidenceSetFingerprint,
                providerEvidenceFingerprint))
        {
            return IntegrityRefused();
        }
        logger.LogDebug(
            "Pass 2 dispatch hydration admitted {ExecutionId} for worker dispatch.",
            pass2ExecutionId);
        return Hydrated(new(
            job.Id,
            leaseToken,
            leaseExpiresAt,
            job.AttemptCount,
            execution.Id,
            group.Id,
            decision.Id,
            edgeId,
            decision.EvidenceFingerprint,
            providerEvidenceFingerprint,
            protocol.ToString(),
            Hash(group.NormalizedEndpointIdentifier),
            group.ConsumerEntryStableKey,
            group.LogicalDocumentStableKey,
            group.PlanFingerprint,
            group.MembershipFingerprint,
            execution.WorkerPromptIdentity,
            execution.SubmissionSchemaIdentity,
            workerContract.ToolDeclarationIdentity,
            initialEvidence.AsReadOnly(),
            binding.Flow,
            binding)
        {
            PromptFingerprint = workerContract.PromptFingerprint,
            SubmissionSchemaFingerprint = workerContract.SubmissionSchemaFingerprint,
            ToolDeclarationFingerprint = workerContract.ToolDeclarationFingerprint,
        });
    }

    private static Pass2BusinessLogicEvidenceBinding CreateDocumentBinding(
        Pass2JobExecution job,
        DocumentVersion document) => new(
        Pass2InitialEvidenceHandle.Create(
            job.Id,
            job.AttemptCount,
            Pass2CodeLogicRetrievalItemKind.Document,
            document.Id,
            document.RepositoryId,
            document.ScanRunId,
            document.ProfiledRevision),
        document.Id,
        document.RepositoryId,
        document.ScanRunId,
        document.ProfiledRevision);

    private static Pass2BusinessLogicEvidenceBinding CreateAnchorBinding(
        Pass2JobExecution job,
        DocumentVersion document,
        SourceAnchor anchor) => new(
        Pass2InitialEvidenceHandle.Create(
            job.Id,
            job.AttemptCount,
            Pass2CodeLogicRetrievalItemKind.SourceAnchor,
            document.Id,
            document.RepositoryId,
            document.ScanRunId,
            document.ProfiledRevision,
            sourceAnchorId: anchor.Id,
            byteStart: anchor.ByteStart,
            byteEnd: anchor.ByteEnd,
            sourceHash: anchor.SourceHash),
        document.Id,
        document.RepositoryId,
        document.ScanRunId,
        document.ProfiledRevision,
        SourceAnchorId: anchor.Id,
        ByteStart: anchor.ByteStart,
        ByteEnd: anchor.ByteEnd);

    private static PreparedPass2RuntimeRepositoryBinding RepositoryBinding(
        string alias,
        Guid repositoryId,
        TwoRepositoryScanCoordination coordination,
        IReadOnlyList<Repository> repositories,
        IReadOnlyList<PreparedScanManifest> manifests,
        Pass1PairExecution pair)
    {
        bool first = repositoryId == coordination.RepositoryOneId;
        Repository repository = repositories.Single(value => value.Id == repositoryId);
        PreparedScanManifest manifest = manifests.Single(value =>
            value.RepositoryId == repositoryId &&
            value.Id == (first ? pair.ManifestOneId : pair.ManifestTwoId));
        return new(
            alias,
            repository.GitUrl,
            repositoryId,
            first ? coordination.ScanRunOneId : coordination.ScanRunTwoId,
            first ? coordination.ScanExecutionOneId : coordination.ScanExecutionTwoId,
            manifest.RequestedRef,
            first ? coordination.ProfiledRevisionOne : coordination.ProfiledRevisionTwo,
            first ? coordination.ProfiledRevisionOne : coordination.ProfiledRevisionTwo);
    }

    private static string CanonicalLogicalStableKey(
        Pass2Execution execution,
        FlowGroup group,
        CrossRepositoryResolutionDecision decision) =>
        "business-flow/v1/" + Hash(string.Join(
            '\n',
            "business-flow-logical-key/1.0",
            execution.SystemId.ToString("D"),
            group.RepositoryId.ToString("D"),
            group.ProviderRepositoryId!.Value.ToString("D"),
            decision.Protocol.ToString(),
            decision.NormalizedEndpointIdentifier,
            decision.ConsumerEntryStableKey));

    private static bool ValidateCanonicalTrace(
        FlowGroup group,
        CrossRepositoryResolutionDecision decision,
        IReadOnlyCollection<ExactResolutionProviderEvidenceContribution> contributions,
        FlowTrace trace,
        IReadOnlyList<FlowTraceStep> steps)
    {
        if (steps.Count < 5 || (steps.Count - 5) % 2 != 0 ||
            steps.Any(value => value.FlowTraceId != trace.Id) ||
            !steps.Select(value => value.StepOrder)
                .SequenceEqual(Enumerable.Range(1, steps.Count)) ||
            !StepMatches(steps[0], group.ConsumerDocumentVersionId, null, null,
                FlowTraceStepKind.Document, "Consumer Document") ||
            !StepMatches(steps[1], group.ConsumerDocumentVersionId, group.ExactResolutionEdgeId,
                group.ExactResolutionDecisionId, FlowTraceStepKind.Relationship,
                "Exact Cross-Repository relationship") ||
            !StepMatches(steps[2], group.ProviderXmlDocumentVersionId, null, null,
                FlowTraceStepKind.Document, "Provider XML Document") ||
            steps[3].EdgeId is not Guid implementationEdgeId ||
            !StepMatches(steps[3], group.ProviderXmlDocumentVersionId, implementationEdgeId, null,
                FlowTraceStepKind.Relationship, "Provider implementation relationship") ||
            !StepMatches(steps[4], group.ProviderImplementationDocumentVersionId, null, null,
                FlowTraceStepKind.Document, "Provider implementation Document"))
        {
            return false;
        }

        var optionalMembers = new List<(Guid EdgeId, Guid DocumentVersionId)>();
        for (int index = 5; index < steps.Count; index += 2)
        {
            FlowTraceStep relationship = steps[index];
            FlowTraceStep document = steps[index + 1];
            if (relationship.EdgeId is not Guid optionalEdgeId ||
                !StepMatches(relationship, relationship.DocumentVersionId, optionalEdgeId, null,
                    FlowTraceStepKind.Relationship, "Same-Repository supporting relationship") ||
                !StepMatches(document, document.DocumentVersionId, null, null,
                    FlowTraceStepKind.Document, "Supporting Document"))
            {
                return false;
            }
            optionalMembers.Add((optionalEdgeId, document.DocumentVersionId));
        }

        string membershipFingerprint = PreparedPass2FlowPlanner.ComputeMembershipFingerprint(
            group.ConsumerDocumentVersionId!.Value,
            group.ExactResolutionEdgeId!.Value,
            group.ProviderXmlDocumentVersionId!.Value,
            implementationEdgeId,
            group.ProviderImplementationDocumentVersionId,
            optionalMembers);
        string planFingerprint = PreparedPass2FlowPlanner.ComputePlanFingerprint(
            decision.Id,
            decision.EvidenceFingerprint,
            "Complete",
            membershipFingerprint,
            contributions);
        return StringComparer.Ordinal.Equals(group.MembershipFingerprint, membershipFingerprint) &&
            StringComparer.Ordinal.Equals(group.PlanFingerprint, planFingerprint);
    }

    private static bool StepMatches(
        FlowTraceStep step,
        Guid? documentVersionId,
        Guid? edgeId,
        Guid? decisionId,
        FlowTraceStepKind kind,
        string description) =>
        step.DocumentVersionId == documentVersionId &&
        step.EdgeId == edgeId &&
        step.ExactResolutionDecisionId == decisionId &&
        step.StepKind == kind &&
        StringComparer.Ordinal.Equals(step.Description, description);

    private static Guid ExpectedScanRunId(
        Guid repositoryId,
        TwoRepositoryScanCoordination coordination) =>
        repositoryId == coordination.RepositoryOneId
            ? coordination.ScanRunOneId
            : coordination.ScanRunTwoId;

    private static string ExpectedProfiledRevision(
        Guid repositoryId,
        TwoRepositoryScanCoordination coordination) =>
        repositoryId == coordination.RepositoryOneId
            ? coordination.ProfiledRevisionOne
            : coordination.ProfiledRevisionTwo;

    private static Guid ExpectedScanExecutionId(
        Guid repositoryId,
        TwoRepositoryScanCoordination coordination) =>
        repositoryId == coordination.RepositoryOneId
            ? coordination.ScanExecutionOneId
            : coordination.ScanExecutionTwoId;

    private static bool AnchorMatchesCodeUnit(SourceAnchor anchor, CodeUnit codeUnit) =>
        anchor.CodeUnitId == codeUnit.Id &&
        anchor.RepositoryId == codeUnit.RepositoryId &&
        anchor.ScanRunId == codeUnit.ScanRunId &&
        anchor.LineStart == codeUnit.LineStart &&
        anchor.LineEnd == codeUnit.LineEnd &&
        StringComparer.Ordinal.Equals(anchor.RepositoryRelativePath, codeUnit.FilePath) &&
        StringComparer.Ordinal.Equals(anchor.SourceHash, codeUnit.SourceHash) &&
        (codeUnit.ByteStart is null ||
            anchor.ByteStart == codeUnit.ByteStart && anchor.ByteEnd == codeUnit.ByteEndExclusive);

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static PreparedPass2DispatchHydrationResult Hydrated(
        PreparedPass2DispatchRequest request) => new(
        PreparedPass2DispatchHydrationOutcome.Hydrated,
        request);

    private static PreparedPass2DispatchHydrationResult IntegrityRefused() => new(
        PreparedPass2DispatchHydrationOutcome.IntegrityRefused,
        null);

    private static PreparedPass2DispatchHydrationResult SecurityRefused() => new(
        PreparedPass2DispatchHydrationOutcome.SecurityRefused,
        null);

    private static string GuidKey(Guid value) =>
        Convert.ToHexString(value.ToByteArray(bigEndian: true));

}
