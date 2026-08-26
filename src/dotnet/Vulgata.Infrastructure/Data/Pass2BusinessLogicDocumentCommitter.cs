using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Data;

using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

/// <summary>
/// Validates and atomically publishes a Pass 2 Business Logic candidate.
/// Rejected candidates leave the knowledge graph and execution authority unchanged.
/// </summary>
public sealed class Pass2BusinessLogicDocumentCommitter : IPass2BusinessLogicDocumentCommitter
{
    private const int _maximumCandidateUtf8Bytes = 65_536;
    private readonly VulgataDbContext _context;
    private readonly RepositoryAccessScope _accessScope;
    private readonly PreparedPass2WorkerContractAuthority? _workerContract;

    private PreparedPass2WorkerContractAuthority WorkerContract =>
        _workerContract ?? throw new InvalidOperationException(
            "The accepted Pass 2 worker contract authority is unavailable.");

    public Pass2BusinessLogicDocumentCommitter(
        VulgataDbContext context,
        RepositoryAccessScope accessScope)
        : this(context, accessScope, null, true)
    {
    }

    public Pass2BusinessLogicDocumentCommitter(
        VulgataDbContext context,
        RepositoryAccessScope accessScope,
        IPreparedPass2WorkerContractAuthority workerContractAuthority)
        : this(context, accessScope, workerContractAuthority, false)
    {
    }

    private Pass2BusinessLogicDocumentCommitter(
        VulgataDbContext context,
        RepositoryAccessScope accessScope,
        IPreparedPass2WorkerContractAuthority? workerContractAuthority,
        bool compatibilityAdapter)
    {
        _ = compatibilityAdapter;
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _accessScope = accessScope ?? throw new ArgumentNullException(nameof(accessScope));
        _workerContract = workerContractAuthority?.GetCurrent();
    }

    public async Task<Pass2BusinessLogicCommitResult> CommitAsync(
        Pass2BusinessLogicCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (_workerContract is null || !RequestContractMatches(request) ||
            request.Pass2JobExecutionId == Guid.Empty ||
            request.LeaseToken == Guid.Empty ||
            request.AttemptOrdinal is < 1 or > 2 || !_accessScope.HasLiveAuthorizationVerifier ||
            request.Candidate.ValueKind != JsonValueKind.Object ||
            JsonSerializer.SerializeToUtf8Bytes(request.Candidate).Length > _maximumCandidateUtf8Bytes)
        {
            return Pass2BusinessLogicCommitResult.Rejected();
        }

        try
        {
            // The job row and stable-key advisory lock provide the write serialization.
            // ReadCommitted is intentional: a cross-pair waiter must observe the winning
            // transaction's new current Version after it acquires the advisory lock.
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken).ConfigureAwait(false);
            Pass2JobExecution? lockedJob = await LockJobAsync(
                request.Pass2JobExecutionId,
                cancellationToken).ConfigureAwait(false);
            if (lockedJob is null)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return Pass2BusinessLogicCommitResult.Rejected();
            }

            if (lockedJob.State == Pass2JobExecutionState.Completed)
            {
                Pass2BusinessLogicCommitResult replay = await ReplayAsync(
                    lockedJob,
                    request,
                    cancellationToken).ConfigureAwait(false);
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                _context.ChangeTracker.Clear();
                return replay;
            }

            ValidationAuthority? authority = await LoadAuthorityAsync(
                request.Pass2JobExecutionId,
                cancellationToken).ConfigureAwait(false);
            if (authority is null || authority.Job.LeaseToken != request.LeaseToken ||
                authority.Job.AttemptCount != request.AttemptOrdinal ||
                !StringComparer.Ordinal.Equals(
                    request.PromptIdentity, authority.Execution.WorkerPromptIdentity) ||
                !await _accessScope.HasCurrentAccessAsync(
                    authority.RepositoryIds,
                    cancellationToken).ConfigureAwait(false) ||
                !TryValidateCandidate(
                    request.Candidate,
                    authority,
                    request.PromptIdentity,
                    out Guid[] handles))
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                _context.ChangeTracker.Clear();
                return Pass2BusinessLogicCommitResult.Rejected();
            }

            Dictionary<Guid, EvidenceDescriptor>? evidence = await LoadEvidenceAsync(
                authority,
                cancellationToken).ConfigureAwait(false);
            if (evidence is null || handles.Any(handle => !evidence.ContainsKey(handle)))
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                _context.ChangeTracker.Clear();
                return Pass2BusinessLogicCommitResult.Rejected();
            }

            EvidenceDescriptor[] used = handles.Select(handle => evidence[handle]).ToArray();
            if (!ValidateClaimEvidenceKinds(request.Candidate, evidence, authority) ||
                !await RevalidateUsedEvidenceAsync(used, authority, cancellationToken)
                    .ConfigureAwait(false))
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                _context.ChangeTracker.Clear();
                return Pass2BusinessLogicCommitResult.Rejected();
            }

            string completionFingerprint = CompletionFingerprint(request, authority, evidence);

            Pass2BusinessLogicCommitResult committed = await PublishAsync(
                request,
                lockedJob,
                authority,
                evidence,
                completionFingerprint,
                cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _context.ChangeTracker.Clear();
            return committed;
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return Pass2BusinessLogicCommitResult.Conflict();
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
            return Pass2BusinessLogicCommitResult.Conflict();
        }
    }

    private async Task<Pass2JobExecution?> LockJobAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        if (_context.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
        {
            return await _context.Pass2JobExecutions.FromSqlInterpolated(
                    $"SELECT * FROM vulgata.\"Pass2JobExecutions\" WHERE \"Id\" = {jobId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        return await _context.Pass2JobExecutions.SingleOrDefaultAsync(
            value => value.Id == jobId,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Pass2BusinessLogicCommitResult> ReplayAsync(
        Pass2JobExecution job,
        Pass2BusinessLogicCommitRequest request,
        CancellationToken cancellationToken)
    {
        ValidationAuthority? authority = await LoadAuthorityAsync(
            job.Id,
            allowCompleted: true,
            cancellationToken).ConfigureAwait(false);
        if (authority is null || request.AttemptOrdinal != job.AttemptCount ||
            !StringComparer.Ordinal.Equals(
                request.PromptIdentity, authority.Execution.WorkerPromptIdentity) ||
            !await _accessScope.HasCurrentAccessAsync(authority.RepositoryIds, cancellationToken)
                .ConfigureAwait(false) ||
            !TryValidateCandidate(request.Candidate, authority, request.PromptIdentity, out Guid[] handles))
        {
            return Pass2BusinessLogicCommitResult.Conflict();
        }

        Dictionary<Guid, EvidenceDescriptor>? evidence = await LoadEvidenceAsync(
            authority,
            cancellationToken).ConfigureAwait(false);
        if (evidence is null || handles.Any(handle => !evidence.ContainsKey(handle)) ||
            !ValidateClaimEvidenceKinds(request.Candidate, evidence, authority) ||
            !await RevalidateUsedEvidenceAsync(
                handles.Select(handle => evidence[handle]).ToArray(), authority, cancellationToken)
                .ConfigureAwait(false))
        {
            return Pass2BusinessLogicCommitResult.Conflict();
        }

        string completionFingerprint = CompletionFingerprint(request, authority, evidence);
        if (!StringComparer.Ordinal.Equals(job.CompletionFingerprint, completionFingerprint))
        {
            return Pass2BusinessLogicCommitResult.Conflict();
        }

        DocumentVersion? version = await _context.DocumentVersions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == job.CommittedDocumentVersionId, cancellationToken)
            .ConfigureAwait(false);
        if (version is null || !StringComparer.Ordinal.Equals(version.CommitFingerprint, completionFingerprint) ||
            !await ValidatePublishedGraphAsync(
                request, authority, version, evidence, handles,
                cancellationToken).ConfigureAwait(false))
        {
            return Pass2BusinessLogicCommitResult.Conflict();
        }

        Guid[] usedIds = await _context.DocumentEdges.AsNoTracking()
            .Where(value => value.SourceDocumentVersionId == version.Id &&
                value.EdgeType == DocumentEdgeType.GeneratedFrom)
            .OrderBy(value => value.TargetDocumentVersionId)
            .Select(value => value.TargetDocumentVersionId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return new(
            Pass2BusinessLogicCommitStatus.Replayed,
            version.Id,
            Array.AsReadOnly(usedIds),
            "pass2-publication-replayed");
    }

    private async Task<Pass2BusinessLogicCommitResult> PublishAsync(
        Pass2BusinessLogicCommitRequest request,
        Pass2JobExecution lockedJob,
        ValidationAuthority authority,
        IReadOnlyDictionary<Guid, EvidenceDescriptor> evidence,
        string completionFingerprint,
        CancellationToken cancellationToken)
    {
        string stableKey = authority.Group.LogicalDocumentStableKey;
        if (_context.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({authority.Group.RepositoryId.ToString("D") + ":BusinessLogic:" + stableKey}, 0))",
                cancellationToken).ConfigureAwait(false);
        }

        LogicalDocument? logical = await _context.LogicalDocuments
            .Include(value => value.CurrentVersion)
            .SingleOrDefaultAsync(value => value.RepositoryId == authority.Group.RepositoryId &&
                value.DocumentType == DocumentType.BusinessLogic && value.StableKey == stableKey,
                cancellationToken).ConfigureAwait(false);
        Guid logicalId = logical?.Id ?? DeriveId(
            "pass2-business-logical-document/1.0",
            authority.Group.RepositoryId.ToString("D"),
            stableKey);
        Guid versionId = DeriveId(
            "pass2-business-document-version/1.0",
            authority.Job.Id.ToString("D"),
            completionFingerprint);
        DateTimeOffset committedAt = DateTimeOffset.UtcNow;
        if (committedAt < lockedJob.StateChangedAt)
        {
            committedAt = lockedJob.StateChangedAt;
        }

        CanonicalDocumentContent content = CreateContent(
            request.Candidate,
            authority.Group,
            versionId,
            evidence);
        var materializer = new KnowledgeDocumentMaterializer();
        KnowledgeDocumentMaterialization materialized = materializer.Materialize(
            logicalId,
            versionId,
            authority.Group.RepositoryId,
            authority.Execution.SystemId,
            content,
            committedAt);
        DocumentVersion consumer = await _context.DocumentVersions.AsNoTracking()
            .SingleAsync(value => value.Id == authority.Group.ConsumerDocumentVersionId, cancellationToken)
            .ConfigureAwait(false);
        int versionNumber = logical?.CurrentVersion?.VersionNumber + 1 ?? 1;
        DocumentVersion version = DocumentVersion.Create(
            versionId,
            logicalId,
            authority.Group.RepositoryId,
            authority.Execution.SystemId,
            authority.Group.ScanRunId,
            consumer.ProfiledRevision,
            versionNumber,
            materialized.Content.Title,
            materialized.Content.RepositoryRelativePath,
            KnowledgeDocumentLimits.SchemaIdentity,
            materialized.ContentJson,
            materialized.ContentMarkdown,
            materialized.ContentFingerprint,
            completionFingerprint,
            committedAt);

        bool isNewLogicalDocument = logical is null;
        if (isNewLogicalDocument)
        {
            logical = LogicalDocument.Create(
                logicalId,
                authority.Group.RepositoryId,
                DocumentType.BusinessLogic,
                stableKey,
                committedAt);
            _context.LogicalDocuments.Add(logical);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Guid predecessorId = logical!.CurrentVersionId ?? throw new InvalidOperationException(
                "An existing Business Logic Logical Document must have a current version.");
            logical.MoveCurrentVersion(predecessorId, version);
        }

        _context.DocumentVersions.Add(version);
        _context.IndexEntries.Add(materialized.IndexEntry);
        _context.DocumentSections.AddRange(materialized.Sections);

        Guid[] usedDocumentIds = evidence
            .Where(pair => CandidateCites(request.Candidate, pair.Key))
            .Select(pair => pair.Value.DocumentVersionId)
            .Distinct().OrderBy(value => value).ToArray();
        DocumentVersion[] usedDocuments = await _context.DocumentVersions.AsNoTracking()
            .Where(value => usedDocumentIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        foreach (DocumentVersion used in usedDocuments.OrderBy(value => value.Id))
        {
            _context.DocumentEdges.Add(new DocumentEdge(
                DeriveId(
                    "pass2-generated-from-edge/1.0",
                    versionId.ToString("D"),
                    used.Id.ToString("D")),
                versionId,
                authority.Group.RepositoryId,
                authority.Group.ScanRunId,
                used.Id,
                used.RepositoryId,
                used.ScanRunId,
                DocumentEdgeType.GeneratedFrom,
                committedAt));
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (isNewLogicalDocument)
        {
            logical.SetInitialCurrentVersion(version);
        }

        FlowGroup trackedGroup = await _context.FlowGroups.SingleAsync(
            value => value.Id == authority.Group.Id,
            cancellationToken).ConfigureAwait(false);
        Pass2Execution trackedExecution = await _context.Pass2Executions.SingleAsync(
            value => value.Id == authority.Execution.Id,
            cancellationToken).ConfigureAwait(false);
        trackedGroup.Publish(versionId);
        lockedJob.Complete(
            lockedJob.StateVersion,
            request.LeaseToken,
            versionId,
            completionFingerprint,
            committedAt);
        trackedExecution.RecordPublished(trackedExecution.StateVersion, committedAt);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _ = await new PreparedPass2Store(_context).CompleteIfTerminalAsync(
            trackedExecution,
            committedAt,
            cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(
            Pass2BusinessLogicCommitStatus.Committed,
            versionId,
            Array.AsReadOnly(usedDocumentIds),
            "pass2-publication-committed");
    }

    private Task<ValidationAuthority?> LoadAuthorityAsync(
        Guid jobId,
        CancellationToken cancellationToken) =>
        LoadAuthorityAsync(jobId, allowCompleted: false, cancellationToken);

    private async Task<ValidationAuthority?> LoadAuthorityAsync(
        Guid jobId,
        bool allowCompleted,
        CancellationToken cancellationToken)
    {
        Pass2JobExecution? job = await _context.Pass2JobExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == jobId, cancellationToken)
            .ConfigureAwait(false);
        if (job is null || job.AttemptCount is < 1 or > 2 ||
            (!allowCompleted && (job.State != Pass2JobExecutionState.Running ||
                job.LeaseToken is null || job.LeaseExpiresAt is null ||
                job.LeaseExpiresAt <= DateTimeOffset.UtcNow)) ||
            (allowCompleted && (job.State != Pass2JobExecutionState.Completed ||
                job.CommittedDocumentVersionId is null || job.CompletionFingerprint is null)))
        {
            return null;
        }

        FlowGroup? group = await _context.FlowGroups.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == job.FlowGroupId, cancellationToken)
            .ConfigureAwait(false);
        if (group is null ||
            (!allowCompleted && (group.State != "Complete" || group.BusinessLogicDocumentVersionId is not null)) ||
            (allowCompleted && (group.State != "Published" ||
                group.BusinessLogicDocumentVersionId != job.CommittedDocumentVersionId)) ||
            group.Pass2ExecutionId != job.Pass2ExecutionId || group.SystemId is null ||
            group.Protocol is null || group.ProviderRepositoryId is null ||
            group.ExactResolutionDecisionId is null || group.ExactResolutionEdgeId is null ||
            group.ConsumerDocumentVersionId is null || group.ProviderXmlDocumentVersionId is null ||
            group.ProviderImplementationDocumentVersionId is null || group.MembershipFingerprint is null)
        {
            return null;
        }

        Pass2Execution? execution = await _context.Pass2Executions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == job.Pass2ExecutionId, cancellationToken)
            .ConfigureAwait(false);
        CrossRepositoryResolutionDecision? decision = await _context
            .CrossRepositoryResolutionDecisions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == group.ExactResolutionDecisionId, cancellationToken)
            .ConfigureAwait(false);
        DocumentEdge? edge = await _context.DocumentEdges.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == group.ExactResolutionEdgeId, cancellationToken)
            .ConfigureAwait(false);
        ExactResolutionProviderEvidenceContribution[] contributions = await _context
            .ExactResolutionProviderEvidenceContributions.AsNoTracking()
            .Where(value => value.ResolutionDecisionId == group.ExactResolutionDecisionId)
            .OrderBy(value => value.ProviderOrdinal).ThenBy(value => value.EvidenceOrdinal)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        bool validExecutionState = execution is not null && (allowCompleted
            ? execution.State is Pass2ExecutionState.Running or
                Pass2ExecutionState.Completing or Pass2ExecutionState.Completed or
                Pass2ExecutionState.CompletedWithWarnings
            : execution.State == Pass2ExecutionState.Running);
        if (!validExecutionState ||
            !StringComparer.Ordinal.Equals(
                execution.WorkerPromptIdentity, WorkerContract.PromptIdentity) ||
            !StringComparer.Ordinal.Equals(
                execution.WorkerPromptFingerprint, WorkerContract.PromptFingerprint) ||
            !StringComparer.Ordinal.Equals(
                execution.SubmissionSchemaIdentity, WorkerContract.SubmissionSchemaIdentity) ||
            !StringComparer.Ordinal.Equals(
                execution.SubmissionSchemaFingerprint,
                WorkerContract.SubmissionSchemaFingerprint) ||
            !StringComparer.Ordinal.Equals(
                execution.ToolDeclarationFingerprint, WorkerContract.ToolDeclarationFingerprint) ||
            execution.SystemId != group.SystemId || decision is null || edge is null ||
            decision.Outcome != "ExactMatch" ||
            decision.Pass1PairExecutionId != execution.Pass1PairExecutionId ||
            decision.CoordinationId != execution.CoordinationId ||
            decision.SystemId != group.SystemId || decision.DocumentEdgeId != edge.Id ||
            decision.ConsumerDocumentVersionId != group.ConsumerDocumentVersionId ||
            decision.ProviderDocumentVersionId != group.ProviderXmlDocumentVersionId ||
            !StringComparer.Ordinal.Equals(decision.ConsumerEntryStableKey, group.ConsumerEntryStableKey) ||
            !StringComparer.Ordinal.Equals(decision.ConsumerEntryIdentity, group.ConsumerEntryIdentity) ||
            decision.Protocol != group.Protocol ||
            !StringComparer.Ordinal.Equals(
                decision.NormalizedEndpointIdentifier,
                group.NormalizedEndpointIdentifier) ||
            edge.SourceDocumentVersionId != group.ConsumerDocumentVersionId ||
            edge.TargetDocumentVersionId != group.ProviderXmlDocumentVersionId ||
            edge.SourceRepositoryId != group.RepositoryId ||
            edge.TargetRepositoryId != group.ProviderRepositoryId ||
            edge.SourceScanRunId != group.ScanRunId ||
            edge.EdgeType != DocumentEdgeType.References || contributions.Length == 0 ||
            !ExactResolutionCanonicalAuthority.MatchesPersistedExactAuthority(
                decision, edge, contributions) ||
            contributions.Select(value => value.ProviderOrdinal).Distinct().Count() != 1 ||
            contributions.Select(value => value.EvidenceOrdinal)
                .SequenceEqual(Enumerable.Range(1, contributions.Length)) is false)
        {
            return null;
        }

        Guid[] repositoryIds = [group.RepositoryId, group.ProviderRepositoryId.Value];
        Guid[] initialDocumentIds =
        [
            group.ConsumerDocumentVersionId.Value,
            group.ProviderXmlDocumentVersionId.Value,
            group.ProviderImplementationDocumentVersionId.Value,
        ];
        string canonicalStableKey = CanonicalLogicalStableKey(execution, group, decision);
        if (!StringComparer.Ordinal.Equals(group.SchemaIdentity, "endpoint-flow-group/1.0") ||
            !StringComparer.Ordinal.Equals(group.LogicalKeySchemaIdentity, "business-flow-logical-key/1.0") ||
            !StringComparer.Ordinal.Equals(group.LogicalDocumentStableKey, canonicalStableKey))
        {
            return null;
        }

        FlowTrace[] authorityTraces = await _context.FlowTraces.AsNoTracking()
            .Where(value => value.FlowGroupId == group.Id).OrderBy(value => value.TraceOrder)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] authorityTraceIds = authorityTraces.Select(value => value.Id).ToArray();
        FlowTraceStep[] authoritySteps = await _context.FlowTraceSteps.AsNoTracking()
            .Where(value => authorityTraceIds.Contains(value.FlowTraceId))
            .OrderBy(value => value.FlowTraceId).ThenBy(value => value.StepOrder)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] supportingEdgeIds = authoritySteps
            .Where(value => value.StepKind == FlowTraceStepKind.Relationship &&
                value.EdgeId.HasValue && value.EdgeId != edge.Id)
            .Select(value => value.EdgeId!.Value)
            .Distinct()
            .ToArray();
        DocumentEdge[] supportingEdges = await _context.DocumentEdges.AsNoTracking()
            .Where(value => supportingEdgeIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        bool canonicalPlanning = TryComputeCanonicalPlanningFingerprints(
            group,
            decision,
            contributions,
            authoritySteps,
            out string canonicalMembershipFingerprint,
            out string canonicalPlanFingerprint);
        if (authorityTraces.Length != 1 || authoritySteps.Length < 5 ||
            authoritySteps.Select(value => value.StepOrder)
                .SequenceEqual(Enumerable.Range(1, authoritySteps.Length)) is false ||
            supportingEdges.Length != supportingEdgeIds.Length ||
            !Pass2RelationshipAuthority.ValidateSupportingEdges(
                group, group.ProviderRepositoryId.Value, authoritySteps, supportingEdges) ||
            contributions.Any(value => value.ProviderDocumentVersionId != edge.TargetDocumentVersionId) ||
            !canonicalPlanning ||
            !StringComparer.Ordinal.Equals(group.MembershipFingerprint, canonicalMembershipFingerprint) ||
            !StringComparer.Ordinal.Equals(group.PlanFingerprint, canonicalPlanFingerprint))
        {
            return null;
        }
        string authorityFingerprint = Hash(JsonSerializer.Serialize(new
        {
            execution = new
            {
                execution.Id,
                execution.Pass1PairExecutionId,
                execution.CoordinationId,
                execution.SystemId,
                execution.StartedAt,
            },
            group = new
            {
                group.Id, group.Pass2ExecutionId, group.SystemId, group.RepositoryId,
                group.ProviderRepositoryId, group.Protocol, group.NormalizedEndpointIdentifier,
                group.ConsumerEntryStableKey, group.LogicalDocumentStableKey, group.PlanFingerprint,
                group.MembershipFingerprint, group.ExactResolutionDecisionId, group.ExactResolutionEdgeId,
                group.ConsumerDocumentVersionId, group.ProviderXmlDocumentVersionId,
                group.ProviderImplementationDocumentVersionId,
            },
            decision = new
            {
                decision.Id, decision.Outcome, decision.EvidenceFingerprint,
                decision.DocumentEdgeId, decision.ProviderDocumentVersionId,
                decision.ConsumerEntryStableKey, decision.Protocol,
                decision.NormalizedEndpointIdentifier,
            },
            edge = new
            {
                edge.Id, edge.SourceDocumentVersionId, edge.SourceRepositoryId, edge.SourceScanRunId,
                edge.TargetDocumentVersionId, edge.TargetRepositoryId, edge.TargetScanRunId, edge.EdgeType,
            },
            contributions = contributions.Select(value => new
            {
                value.Id, value.ResolutionDecisionId, value.ProviderOrdinal, value.EvidenceOrdinal,
                value.EvidenceFingerprint,
            }),
            traces = authorityTraces.Select(value => new
                { value.Id, value.FlowGroupId, value.TraceOrder, value.CreatedAt }),
            steps = authoritySteps.Select(value => new
            {
                value.Id, value.FlowTraceId, value.StepOrder, value.DocumentVersionId,
                value.DocumentRepositoryId, value.DocumentScanRunId, value.EdgeId,
                value.ExactResolutionDecisionId, value.StepKind, value.Description, value.CreatedAt,
            }),
        }));

        return new(job, group, execution, decision, edge, contributions, repositoryIds,
            initialDocumentIds, authorityFingerprint);
    }

    private static bool TryComputeCanonicalPlanningFingerprints(
        FlowGroup group,
        CrossRepositoryResolutionDecision decision,
        IReadOnlyCollection<ExactResolutionProviderEvidenceContribution> contributions,
        IReadOnlyList<FlowTraceStep> steps,
        out string membershipFingerprint,
        out string planFingerprint)
    {
        membershipFingerprint = string.Empty;
        planFingerprint = string.Empty;
        if (steps.Count < 5 || (steps.Count - 5) % 2 != 0 ||
            steps[0].StepKind != FlowTraceStepKind.Document ||
            steps[0].DocumentVersionId != group.ConsumerDocumentVersionId ||
            steps[1].StepKind != FlowTraceStepKind.Relationship ||
            steps[1].DocumentVersionId != group.ConsumerDocumentVersionId ||
            steps[1].EdgeId != group.ExactResolutionEdgeId ||
            steps[1].ExactResolutionDecisionId != group.ExactResolutionDecisionId ||
            steps[2].StepKind != FlowTraceStepKind.Document ||
            steps[2].DocumentVersionId != group.ProviderXmlDocumentVersionId ||
            steps[3].StepKind != FlowTraceStepKind.Relationship ||
            steps[3].DocumentVersionId != group.ProviderXmlDocumentVersionId ||
            steps[3].EdgeId is not Guid implementationEdgeId ||
            steps[4].StepKind != FlowTraceStepKind.Document ||
            steps[4].DocumentVersionId != group.ProviderImplementationDocumentVersionId)
        {
            return false;
        }

        var optionalMembers = new List<(Guid EdgeId, Guid DocumentVersionId)>();
        for (int index = 5; index < steps.Count; index += 2)
        {
            FlowTraceStep relationship = steps[index];
            FlowTraceStep document = steps[index + 1];
            if (relationship.StepKind != FlowTraceStepKind.Relationship ||
                relationship.EdgeId is not Guid optionalEdgeId ||
                document.StepKind != FlowTraceStepKind.Document)
            {
                return false;
            }
            optionalMembers.Add((optionalEdgeId, document.DocumentVersionId));
        }

        membershipFingerprint = PreparedPass2FlowPlanner.ComputeMembershipFingerprint(
            group.ConsumerDocumentVersionId!.Value,
            group.ExactResolutionEdgeId!.Value,
            group.ProviderXmlDocumentVersionId!.Value,
            implementationEdgeId,
            group.ProviderImplementationDocumentVersionId,
            optionalMembers);
        planFingerprint = PreparedPass2FlowPlanner.ComputePlanFingerprint(
            decision.Id,
            decision.EvidenceFingerprint,
            "Complete",
            membershipFingerprint,
            contributions);
        return true;
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

    private static void AddInitialDescriptor(
        IDictionary<Guid, EvidenceDescriptor> result,
        ValidationAuthority authority,
        Pass2CodeLogicRetrievalItemKind kind,
        DocumentVersion document,
        SourceAnchor? anchor,
        DocumentEdge? edge)
    {
        string handleText = Pass2InitialEvidenceHandle.Create(
            authority.Job.Id,
            authority.Job.AttemptCount,
            kind,
            document.Id,
            document.RepositoryId,
            document.ScanRunId,
            document.ProfiledRevision,
            sourceAnchorId: anchor?.Id,
            edgeId: edge?.Id,
            byteStart: anchor?.ByteStart,
            byteEnd: anchor?.ByteEnd,
            sourceHash: anchor?.SourceHash);
        Guid handle = Guid.Parse(handleText);
        var descriptor = new EvidenceDescriptor(
            kind,
            document.Id,
            document.RepositoryId,
            document.ScanRunId,
            document.ProfiledRevision,
            null,
            anchor?.Id,
            edge?.Id,
            anchor?.ByteStart,
            anchor?.ByteEnd,
            string.Empty,
            string.Empty,
            0,
            anchor?.SourceHash);
        if (!result.TryAdd(handle, descriptor))
        {
            throw new InvalidOperationException("The initial Pass 2 evidence identity collided.");
        }
    }

    private static bool ValidPersistedRetrievalFingerprints(Pass2RetrievalResult item)
    {
        string descriptor = Hash(string.Join(
            '\n',
            "pass2-retrieval-descriptor/1.0",
            item.Kind.ToString(),
            item.DocumentVersionId.ToString("D"),
            item.ReadSectionId()?.ToString("D") ?? string.Empty,
            item.ReadSourceAnchorId()?.ToString("D") ?? string.Empty,
            item.EdgeId?.ToString("D") ?? string.Empty,
            item.ByteStart?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            item.ByteEnd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));
        string evidence = Hash(string.Join(
            '\n',
            "pass2-retrieval-result/1.0",
            item.Id.ToString("D"),
            item.Kind.ToString(),
            item.DocumentVersionId.ToString("D"),
            item.ReadSectionId()?.ToString("D") ?? string.Empty,
            item.ReadSourceAnchorId()?.ToString("D") ?? string.Empty,
            item.EdgeId?.ToString("D") ?? string.Empty,
            item.ByteStart?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            item.ByteEnd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            item.ReturnedUtf8Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return StringComparer.Ordinal.Equals(item.DescriptorFingerprint, descriptor) &&
            StringComparer.Ordinal.Equals(item.EvidenceFingerprint, evidence);
    }

    private async Task<Dictionary<Guid, EvidenceDescriptor>?> LoadEvidenceAsync(
        ValidationAuthority authority,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, EvidenceDescriptor>();
        FlowTrace[] traces = await _context.FlowTraces.AsNoTracking()
            .Where(value => value.FlowGroupId == authority.Group.Id)
            .OrderBy(value => value.TraceOrder).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] traceIds = traces.Select(value => value.Id).ToArray();
        FlowTraceStep[] steps = await _context.FlowTraceSteps.AsNoTracking()
            .Where(value => traceIds.Contains(value.FlowTraceId))
            .OrderBy(value => value.FlowTraceId).ThenBy(value => value.StepOrder)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (traces.Length != 1 || steps.Length == 0 ||
            steps.Select(value => value.StepOrder).SequenceEqual(Enumerable.Range(1, steps.Length)) is false)
        {
            return null;
        }

        DocumentVersion[] initialDocuments = await _context.DocumentVersions.AsNoTracking()
            .Where(value => authority.InitialDocumentVersionIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        SourceAnchor[] anchors = await _context.SourceAnchors.AsNoTracking()
            .Where(value => authority.InitialDocumentVersionIds.Contains(value.DocumentVersionId))
            .OrderBy(value => value.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] codeUnitIds = anchors.Select(value => value.CodeUnitId).Distinct().ToArray();
        CodeUnit[] codeUnits = await _context.CodeUnits.AsNoTracking()
            .Where(value => codeUnitIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] logicalDocumentIds = initialDocuments
            .Select(value => value.LogicalDocumentId)
            .Distinct()
            .ToArray();
        LogicalDocument[] logicalDocuments = await _context.LogicalDocuments.AsNoTracking()
            .Where(value => logicalDocumentIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (initialDocuments.Length != authority.InitialDocumentVersionIds.Count ||
            logicalDocuments.Length != logicalDocumentIds.Length ||
            codeUnits.Length != codeUnitIds.Length ||
            !initialDocuments.Any(document =>
                document.Id == authority.Decision.ConsumerDocumentVersionId &&
                document.RepositoryId == authority.Decision.ConsumerRepositoryId &&
                document.ScanRunId == authority.Decision.ConsumerScanRunId &&
                StringComparer.Ordinal.Equals(
                    document.ProfiledRevision,
                    authority.Decision.ConsumerProfiledRevision)) ||
            !initialDocuments.Any(document =>
                document.Id == authority.Decision.ProviderDocumentVersionId &&
                document.RepositoryId == authority.Decision.ProviderRepositoryId &&
                document.ScanRunId == authority.Decision.ProviderScanRunId &&
                StringComparer.Ordinal.Equals(
                    document.ProfiledRevision,
                    authority.Decision.ProviderProfiledRevision)) ||
            initialDocuments.Any(document => !steps.Any(step =>
                step.DocumentVersionId == document.Id && step.DocumentRepositoryId == document.RepositoryId &&
                step.DocumentScanRunId == document.ScanRunId)) ||
            initialDocuments.Any(document =>
                document.SystemId != authority.Execution.SystemId ||
                !authority.RepositoryIds.Contains(document.RepositoryId) ||
                document.SupersededById is not null ||
                !logicalDocuments.Any(logical =>
                    logical.Id == document.LogicalDocumentId &&
                    logical.RepositoryId == document.RepositoryId &&
                    logical.DocumentType == DocumentType.CodeLogic &&
                    logical.CurrentVersionId == document.Id)) ||
            initialDocuments.Any(document => !anchors.Any(anchor =>
                anchor.DocumentVersionId == document.Id)) ||
            anchors.Any(anchor =>
                !initialDocuments.Any(document =>
                    document.Id == anchor.DocumentVersionId &&
                    document.RepositoryId == anchor.RepositoryId &&
                    document.ScanRunId == anchor.ScanRunId &&
                    StringComparer.Ordinal.Equals(
                        document.ProfiledRevision, anchor.ProfiledRevision)) ||
                !AnchorMatchesCodeUnit(
                    anchor,
                    codeUnits.Single(value => value.Id == anchor.CodeUnitId))) ||
            authority.Contributions.Any(contribution => !anchors.Any(anchor =>
                anchor.Id == contribution.SourceAnchorId &&
                anchor.DocumentVersionId == contribution.ProviderDocumentVersionId &&
                anchor.DocumentVersionId == authority.Decision.ProviderDocumentVersionId)))
        {
            return null;
        }

        foreach (DocumentVersion document in initialDocuments)
        {
            AddInitialDescriptor(result, authority, Pass2CodeLogicRetrievalItemKind.Document,
                document, null, null);
        }
        foreach (SourceAnchor anchor in anchors)
        {
            DocumentVersion document = initialDocuments.Single(value => value.Id == anchor.DocumentVersionId);
            if (anchor.RepositoryId != document.RepositoryId || anchor.ScanRunId != document.ScanRunId ||
                !StringComparer.Ordinal.Equals(anchor.ProfiledRevision, document.ProfiledRevision))
            {
                return null;
            }
            AddInitialDescriptor(result, authority, Pass2CodeLogicRetrievalItemKind.SourceAnchor,
                document, anchor, null);
        }
        foreach (FlowTraceStep step in steps.Where(value => value.StepKind == FlowTraceStepKind.Relationship))
        {
            if (step.EdgeId is not Guid edgeId)
            {
                return null;
            }
            DocumentEdge? relationship = await _context.DocumentEdges.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == edgeId, cancellationToken).ConfigureAwait(false);
            DocumentVersion? document = initialDocuments.SingleOrDefault(value => value.Id == step.DocumentVersionId);
            if (relationship is null || document is null || relationship.EdgeType != DocumentEdgeType.References ||
                relationship.SourceDocumentVersionId != step.DocumentVersionId &&
                relationship.TargetDocumentVersionId != step.DocumentVersionId)
            {
                return null;
            }
            AddInitialDescriptor(result, authority, Pass2CodeLogicRetrievalItemKind.Relationship,
                document, null, relationship);
        }

        Pass2RetrievalResult[] retrieved = await _context.Pass2RetrievalResults.AsNoTracking()
            .Where(value => value.Pass2JobExecutionId == authority.Job.Id &&
                value.AttemptOrdinal == authority.Job.AttemptCount)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        foreach (Pass2RetrievalResult item in retrieved)
        {
            string? profiledRevision = null;
            string? sourceHash = null;
            long? byteStart = item.ByteStart;
            long? byteEnd = item.ByteEnd;
            if (item.Kind is Pass2CodeLogicRetrievalItemKind.SourceAnchor or
                Pass2CodeLogicRetrievalItemKind.SourceExcerpt)
            {
                Guid? sourceAnchorId = item.ReadSourceAnchorId();
                SourceAnchor? anchor = sourceAnchorId is null
                    ? null
                    : await _context.SourceAnchors.AsNoTracking().SingleOrDefaultAsync(
                        value => value.Id == sourceAnchorId &&
                            value.DocumentVersionId == item.DocumentVersionId &&
                            value.RepositoryId == item.RepositoryId &&
                            value.ScanRunId == item.ScanRunId,
                        cancellationToken).ConfigureAwait(false);
                DocumentVersion? document = await _context.DocumentVersions.AsNoTracking()
                    .SingleOrDefaultAsync(value => value.Id == item.DocumentVersionId,
                        cancellationToken).ConfigureAwait(false);
                CodeUnit? codeUnit = anchor is null
                    ? null
                    : await _context.CodeUnits.AsNoTracking().SingleOrDefaultAsync(
                        value => value.Id == anchor.CodeUnitId,
                        cancellationToken).ConfigureAwait(false);
                if (anchor is null || document is null || codeUnit is null ||
                    !StringComparer.Ordinal.Equals(anchor.ProfiledRevision, document.ProfiledRevision) ||
                    codeUnit.RepositoryId != anchor.RepositoryId ||
                    codeUnit.ScanRunId != anchor.ScanRunId ||
                    !StringComparer.Ordinal.Equals(codeUnit.FilePath, anchor.RepositoryRelativePath) ||
                    !StringComparer.Ordinal.Equals(codeUnit.SourceHash, anchor.SourceHash) ||
                    codeUnit.LineStart != anchor.LineStart || codeUnit.LineEnd != anchor.LineEnd ||
                    item.Kind == Pass2CodeLogicRetrievalItemKind.SourceExcerpt &&
                    (item.ByteStart is null || item.ByteEnd is null ||
                        item.ByteStart < anchor.ByteStart || item.ByteEnd > anchor.ByteEnd ||
                        item.ByteStart >= item.ByteEnd))
                {
                    return null;
                }

                profiledRevision = document.ProfiledRevision;
                sourceHash = anchor.SourceHash;
                if (item.Kind == Pass2CodeLogicRetrievalItemKind.SourceAnchor)
                {
                    byteStart = anchor.ByteStart;
                    byteEnd = anchor.ByteEnd;
                }
            }

            if (item.State != Pass2RetrievalResultState.Admitted ||
                !ValidPersistedRetrievalFingerprints(item) ||
                !result.TryAdd(item.Id, new(
                    item.Kind,
                    item.DocumentVersionId,
                    item.RepositoryId,
                    item.ScanRunId,
                    profiledRevision,
                    item.ReadSectionId(),
                    item.ReadSourceAnchorId(),
                    item.EdgeId,
                    byteStart,
                    byteEnd,
                    item.DescriptorFingerprint,
                    item.EvidenceFingerprint,
                    item.ReturnedUtf8Bytes,
                    sourceHash)))
            {
                return null;
            }
        }

        return result;
    }

    private async Task<bool> RevalidateUsedEvidenceAsync(
        IReadOnlyCollection<EvidenceDescriptor> used,
        ValidationAuthority authority,
        CancellationToken cancellationToken)
    {
        Guid[] documentIds = used.Select(value => value.DocumentVersionId).Distinct().ToArray();
        DocumentVersion[] documents = await _context.DocumentVersions.AsNoTracking()
            .Where(value => documentIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (documents.Length != documentIds.Length || documents.Any(document =>
            document.SystemId != authority.Execution.SystemId ||
            !authority.RepositoryIds.Contains(document.RepositoryId) ||
            used.Where(value => value.DocumentVersionId == document.Id).Any(value =>
                value.RepositoryId != document.RepositoryId || value.ScanRunId != document.ScanRunId ||
                value.ProfiledRevision is not null &&
                !StringComparer.Ordinal.Equals(value.ProfiledRevision, document.ProfiledRevision))))
        {
            return false;
        }

        Guid[] logicalIds = documents.Select(value => value.LogicalDocumentId).Distinct().ToArray();
        LogicalDocument[] logical = await _context.LogicalDocuments.AsNoTracking()
            .Where(value => logicalIds.Contains(value.Id)).ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (logical.Length != logicalIds.Length || documents.Any(document =>
            logical.Single(value => value.Id == document.LogicalDocumentId).CurrentVersionId != document.Id))
        {
            return false;
        }

        foreach (EvidenceDescriptor descriptor in used)
        {
            if (descriptor.Kind == Pass2CodeLogicRetrievalItemKind.Section &&
                descriptor.SectionId is not Guid ||
                descriptor.Kind == Pass2CodeLogicRetrievalItemKind.SourceAnchor &&
                descriptor.SourceAnchorId is not Guid ||
                descriptor.Kind == Pass2CodeLogicRetrievalItemKind.SourceExcerpt &&
                (descriptor.SourceAnchorId is not Guid || descriptor.ByteStart is null || descriptor.ByteEnd is null) ||
                descriptor.Kind == Pass2CodeLogicRetrievalItemKind.Relationship &&
                descriptor.EdgeId is not Guid ||
                descriptor.SectionId is Guid sectionId &&
                !await _context.DocumentSections.AsNoTracking().AnyAsync(
                    value => value.Id == sectionId && value.DocumentVersionId == descriptor.DocumentVersionId,
                    cancellationToken).ConfigureAwait(false) ||
                descriptor.SourceAnchorId is Guid anchorId &&
                !await _context.SourceAnchors.AsNoTracking().AnyAsync(
                    value => value.Id == anchorId && value.DocumentVersionId == descriptor.DocumentVersionId &&
                        value.RepositoryId == descriptor.RepositoryId &&
                        value.ScanRunId == descriptor.ScanRunId &&
                        value.ProfiledRevision == descriptor.ProfiledRevision &&
                        value.SourceHash == descriptor.SourceHash &&
                        (descriptor.Kind == Pass2CodeLogicRetrievalItemKind.SourceExcerpt
                            ? descriptor.ByteStart >= value.ByteStart &&
                                descriptor.ByteEnd <= value.ByteEnd &&
                                descriptor.ByteStart < descriptor.ByteEnd
                            : !descriptor.ByteStart.HasValue ||
                                value.ByteStart == descriptor.ByteStart &&
                                value.ByteEnd == descriptor.ByteEnd),
                    cancellationToken).ConfigureAwait(false) ||
                descriptor.EdgeId is Guid edgeId &&
                !await _context.DocumentEdges.AsNoTracking().AnyAsync(
                    value => value.Id == edgeId && value.EdgeType == DocumentEdgeType.References &&
                        (value.SourceDocumentVersionId == descriptor.DocumentVersionId ||
                         value.TargetDocumentVersionId == descriptor.DocumentVersionId),
                    cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateClaimEvidenceKinds(
        JsonElement candidate,
        IReadOnlyDictionary<Guid, EvidenceDescriptor> evidence,
        ValidationAuthority authority)
    {
        foreach (JsonElement step in candidate.GetProperty("flowSteps").EnumerateArray())
        {
            Guid[] handles = HandlesFrom(step).ToArray();
            if (step.GetProperty("relationshipKind").GetString() == "References" &&
                !handles.Any(handle => evidence.TryGetValue(handle, out EvidenceDescriptor? descriptor) &&
                    descriptor.Kind == Pass2CodeLogicRetrievalItemKind.Relationship &&
                    descriptor.EdgeId == authority.Edge.Id))
            {
                return false;
            }
        }

        Guid[] sourceClaims = HandlesFrom(candidate.GetProperty("summary"))
            .Concat(candidate.GetProperty("participants").EnumerateArray().SelectMany(HandlesFrom))
            .ToArray();
        return sourceClaims.All(handle => evidence.TryGetValue(handle, out EvidenceDescriptor? descriptor) &&
            descriptor.Kind is Pass2CodeLogicRetrievalItemKind.Document or
                Pass2CodeLogicRetrievalItemKind.Section or
                Pass2CodeLogicRetrievalItemKind.SourceAnchor or
                Pass2CodeLogicRetrievalItemKind.SourceExcerpt) &&
            sourceClaims.Any(handle => evidence[handle].Kind is
                Pass2CodeLogicRetrievalItemKind.SourceAnchor or Pass2CodeLogicRetrievalItemKind.SourceExcerpt);
    }

    private static bool TryValidateCandidate(
        JsonElement candidate,
        ValidationAuthority authority,
        string expectedPromptIdentity,
        out Guid[] handles)
    {
        handles = [];
        if (!Pass2BusinessLogicSchemaContract.IsValidDocument(candidate) ||
            !candidate.TryGetProperty("binding", out JsonElement binding) ||
            !ValidateBinding(binding, authority, expectedPromptIdentity) ||
            !candidate.TryGetProperty("summary", out JsonElement summary))
        {
            return false;
        }

        var cited = new HashSet<Guid>();
        return ValidateClaim(summary, cited) &&
            ValidateArray(candidate, "participants", cited, ValidateParticipant) &&
            ValidateArray(candidate, "flowSteps", cited, ValidateFlowStep) &&
            ValidateArray(candidate, "businessRules", cited, ValidateBusinessRule) &&
            ValidateArray(candidate, "dataMovements", cited, ValidateDataMovement) &&
            ValidateArray(candidate, "failurePaths", cited, ValidateFailurePath) &&
            ValidateArray(candidate, "unresolvedStatements", cited, ValidateUnresolved) &&
            (handles = cited.OrderBy(value => value).ToArray()).Length > 0;
    }

    private static bool ValidateBinding(
        JsonElement binding,
        ValidationAuthority authority,
        string expectedPromptIdentity)
    {
        FlowGroup group = authority.Group;
        return HasGuid(binding, "jobId", authority.Job.Id) &&
            HasGuid(binding, "pass2ExecutionId", authority.Execution.Id) &&
            HasGuid(binding, "flowGroupId", group.Id) &&
            HasGuid(binding, "systemId", authority.Execution.SystemId) &&
            HasGuid(binding, "consumerRepositoryId", group.RepositoryId) &&
            HasGuid(binding, "providerRepositoryId", group.ProviderRepositoryId!.Value) &&
            HasString(binding, "protocol", group.Protocol!.Value.ToString()) &&
            HasString(binding, "normalizedEndpointIdentifierFingerprint", Hash(group.NormalizedEndpointIdentifier!)) &&
            HasString(binding, "consumerEntryStableKey", group.ConsumerEntryStableKey!) &&
            HasString(binding, "logicalKeySchemaIdentity", "business-flow-logical-key/1.0") &&
            HasString(binding, "logicalDocumentStableKey", group.LogicalDocumentStableKey) &&
            HasGuid(binding, "exactResolutionDecisionId", authority.Decision.Id) &&
            HasString(binding, "exactResolutionDecisionFingerprint", authority.Decision.EvidenceFingerprint) &&
            HasGuid(binding, "exactResolutionEdgeId", authority.Edge.Id) &&
            HasString(binding, "providerEvidenceFingerprint", ProviderFingerprint(authority.Contributions)) &&
            HasString(binding, "planFingerprint", group.PlanFingerprint) &&
            HasString(binding, "membershipFingerprint", group.MembershipFingerprint!) &&
            HasString(binding, "promptIdentity", expectedPromptIdentity) &&
            HasString(
                binding,
                "submissionSchemaIdentity",
                authority.Execution.SubmissionSchemaIdentity);
    }

    private static bool ValidateClaim(JsonElement value, HashSet<Guid> cited) =>
        ValidateHandles(value, cited);

    private static bool ValidateParticipant(JsonElement value, int order, HashSet<Guid> cited) =>
        HasOrder(value, order) && ValidateHandles(value, cited);

    private static bool ValidateFlowStep(JsonElement value, int order, HashSet<Guid> cited) =>
        HasOrder(value, order) && ValidateHandles(value, cited);

    private static bool ValidateBusinessRule(JsonElement value, int order, HashSet<Guid> cited) =>
        HasOrder(value, order) && ValidateHandles(value, cited);

    private static bool ValidateDataMovement(JsonElement value, int order, HashSet<Guid> cited) =>
        HasOrder(value, order) && ValidateHandles(value, cited);

    private static bool ValidateFailurePath(JsonElement value, int order, HashSet<Guid> cited) =>
        HasOrder(value, order) && ValidateHandles(value, cited);

    private static bool ValidateUnresolved(JsonElement value, int order, HashSet<Guid> cited) =>
        HasOrder(value, order) && ValidateHandles(value, cited);

    private static bool ValidateArray(
        JsonElement root,
        string name,
        HashSet<Guid> cited,
        Func<JsonElement, int, HashSet<Guid>, bool> validator)
    {
        if (!root.TryGetProperty(name, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        int order = 0;
        return values.EnumerateArray().All(value => validator(value, ++order, cited));
    }

    private static bool ValidateHandles(JsonElement value, HashSet<Guid> cited)
    {
        if (!value.TryGetProperty("evidenceHandleRefs", out JsonElement handles) ||
            handles.ValueKind != JsonValueKind.Array || handles.GetArrayLength() is < 1 or > 32)
        {
            return false;
        }

        var local = new HashSet<Guid>();
        foreach (JsonElement handleValue in handles.EnumerateArray())
        {
            if (handleValue.ValueKind != JsonValueKind.String ||
                !TryCanonicalHandle(handleValue.GetString(), out Guid handle) || !local.Add(handle))
            {
                return false;
            }
            cited.Add(handle);
        }
        return true;
    }

    private static bool TryCanonicalHandle(string? value, out Guid handle) =>
        Guid.TryParseExact(value, "D", out handle) &&
        StringComparer.Ordinal.Equals(value, handle.ToString("D"));

    private static bool HasOrder(JsonElement value, int expected) =>
        value.TryGetProperty("order", out JsonElement property) && property.TryGetInt32(out int actual) &&
        actual == expected;

    private static bool HasString(JsonElement value, string name, string expected) =>
        value.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.String &&
        StringComparer.Ordinal.Equals(property.GetString(), expected);

    private static bool HasGuid(JsonElement value, string name, Guid expected) =>
        HasString(value, name, expected.ToString("D"));

    private static CanonicalDocumentContent CreateContent(
        JsonElement candidate,
        FlowGroup group,
        Guid documentVersionId,
        IReadOnlyDictionary<Guid, EvidenceDescriptor> evidence)
    {
        string title = candidate.GetProperty("title").GetString()!;
        string summary = candidate.GetProperty("summary").GetProperty("description").GetString()!;
        var sections = new List<CanonicalSection>();
        int order = 1;

        JsonElement participants = candidate.GetProperty("participants");
        string overview = summary + "\n\n" + string.Join(
            "\n",
            participants.EnumerateArray().Select(value =>
                $"- {value.GetProperty("name").GetString()}: {value.GetProperty("responsibility").GetString()}"));
        Guid[] overviewDocuments = HandlesFrom(candidate.GetProperty("summary"))
            .Concat(participants.EnumerateArray().SelectMany(HandlesFrom))
            .Select(handle => evidence[handle].DocumentVersionId).Distinct().OrderBy(value => value).ToArray();
        sections.Add(new(
            DeriveId("pass2-section/1.0", documentVersionId.ToString("D"), "overview"),
            order++,
            DocumentSectionKind.Overview,
            "Overview",
            overview,
            overview,
            Array.Empty<Guid>(),
            overviewDocuments));

        AddArraySections(candidate, "flowSteps", DocumentSectionKind.ProcessStep, "Flow step", sections,
            ref order, documentVersionId, evidence,
            value => $"{value.GetProperty("actor").GetString()}: {value.GetProperty("action").GetString()}\n\n{value.GetProperty("outcome").GetString()}");
        AddArraySections(candidate, "businessRules", DocumentSectionKind.BusinessRule, "Business rule", sections,
            ref order, documentVersionId, evidence,
            value => $"{value.GetProperty("condition").GetString()}\n\n{value.GetProperty("effect").GetString()}");
        AddArraySections(candidate, "dataMovements", DocumentSectionKind.TechnicalDetail, "Data movement", sections,
            ref order, documentVersionId, evidence,
            value => $"{value.GetProperty("from").GetString()} -> {value.GetProperty("to").GetString()}: {value.GetProperty("value").GetString()}\n\n{value.GetProperty("description").GetString()}");
        AddArraySections(candidate, "failurePaths", DocumentSectionKind.Other, "Failure path", sections,
            ref order, documentVersionId, evidence,
            value => $"{value.GetProperty("trigger").GetString()}\n\n{value.GetProperty("behavior").GetString()}\n\n{value.GetProperty("businessImpact").GetString()}");
        AddArraySections(candidate, "unresolvedStatements", DocumentSectionKind.Evidence, "Unresolved", sections,
            ref order, documentVersionId, evidence,
            value => value.GetProperty("description").GetString()!);

        return new(
            KnowledgeDocumentLimits.SchemaIdentity,
            DocumentType.BusinessLogic,
            group.LogicalDocumentStableKey,
            title,
            $"business-flows/{group.LogicalDocumentStableKey["business-flow/v1/".Length..]}.md",
            summary,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.AsReadOnly(sections.ToArray()));
    }

    private static void AddArraySections(
        JsonElement candidate,
        string propertyName,
        DocumentSectionKind kind,
        string headingPrefix,
        List<CanonicalSection> sections,
        ref int order,
        Guid documentVersionId,
        IReadOnlyDictionary<Guid, EvidenceDescriptor> evidence,
        Func<JsonElement, string> render)
    {
        foreach (JsonElement item in candidate.GetProperty(propertyName).EnumerateArray())
        {
            int sourceOrder = item.GetProperty("order").GetInt32();
            string markdown = render(item);
            Guid[] documentIds = HandlesFrom(item).Select(handle => evidence[handle].DocumentVersionId)
                .Distinct().OrderBy(value => value).ToArray();
            sections.Add(new(
                DeriveId(
                    "pass2-section/1.0",
                    documentVersionId.ToString("D"),
                    propertyName,
                    sourceOrder.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                order++,
                kind,
                $"{headingPrefix} {sourceOrder}",
                markdown,
                markdown,
                Array.Empty<Guid>(),
                documentIds));
        }
    }

    private static IEnumerable<Guid> HandlesFrom(JsonElement claim) =>
        claim.GetProperty("evidenceHandleRefs").EnumerateArray().Select(value => value.GetGuid());

    private static bool CandidateCites(JsonElement candidate, Guid handle)
    {
        string expected = handle.ToString("D");
        return candidate.GetProperty("summary").GetProperty("evidenceHandleRefs")
                .EnumerateArray().Any(value => value.GetString() == expected) ||
            new[] { "participants", "flowSteps", "businessRules", "dataMovements", "failurePaths", "unresolvedStatements" }
                .SelectMany(name => candidate.GetProperty(name).EnumerateArray())
                .Any(value => value.GetProperty("evidenceHandleRefs").EnumerateArray()
                    .Any(reference => reference.GetString() == expected));
    }

    private async Task<bool> ValidatePublishedGraphAsync(
        Pass2BusinessLogicCommitRequest request,
        ValidationAuthority authority,
        DocumentVersion version,
        IReadOnlyDictionary<Guid, EvidenceDescriptor> evidence,
        IReadOnlyCollection<Guid> citedHandles,
        CancellationToken cancellationToken)
    {
        LogicalDocument? logical = await _context.LogicalDocuments.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == version.LogicalDocumentId, cancellationToken)
            .ConfigureAwait(false);
        if (logical is null || logical.RepositoryId != authority.Group.RepositoryId ||
            logical.DocumentType != DocumentType.BusinessLogic ||
            !StringComparer.Ordinal.Equals(logical.StableKey, authority.Group.LogicalDocumentStableKey) ||
            logical.CurrentVersionId is null)
        {
            return false;
        }

        if (!await ValidateVersionChainAsync(logical, version, cancellationToken)
            .ConfigureAwait(false))
        {
            return false;
        }

        DocumentVersion consumer = await _context.DocumentVersions.AsNoTracking()
            .SingleAsync(value => value.Id == authority.Group.ConsumerDocumentVersionId, cancellationToken)
            .ConfigureAwait(false);
        CanonicalDocumentContent content = CreateContent(request.Candidate, authority.Group, version.Id, evidence);
        KnowledgeDocumentMaterialization expected = new KnowledgeDocumentMaterializer().Materialize(
            logical.Id, version.Id, logical.RepositoryId, authority.Execution.SystemId, content, version.CreatedAt);
        if (version.RepositoryId != logical.RepositoryId || version.SystemId != authority.Execution.SystemId ||
            version.ScanRunId != consumer.ScanRunId ||
            !StringComparer.Ordinal.Equals(version.ProfiledRevision, consumer.ProfiledRevision) ||
            !StringComparer.Ordinal.Equals(version.Title, expected.Content.Title) ||
            !StringComparer.Ordinal.Equals(version.RepositoryRelativePath, expected.Content.RepositoryRelativePath) ||
            !StringComparer.Ordinal.Equals(version.SchemaIdentity, KnowledgeDocumentLimits.SchemaIdentity) ||
            !JsonEquivalent(version.ContentJson, expected.ContentJson) ||
            !StringComparer.Ordinal.Equals(version.ContentMarkdown, expected.ContentMarkdown) ||
            !StringComparer.Ordinal.Equals(version.ContentFingerprint, expected.ContentFingerprint))
        {
            return false;
        }


        int publishedCount = await _context.FlowGroups.AsNoTracking().CountAsync(
            value => value.Pass2ExecutionId == authority.Execution.Id &&
                value.State == "Published" && value.BusinessLogicDocumentVersionId != null,
            cancellationToken).ConfigureAwait(false);
        int failedCount = await _context.FlowGroups.AsNoTracking().CountAsync(
            value => value.Pass2ExecutionId == authority.Execution.Id && value.State == "Failed",
            cancellationToken).ConfigureAwait(false);
        if (authority.Execution.PublishedCount != publishedCount ||
            authority.Execution.FailedCount != failedCount)
        {
            return false;
        }

        DocumentSection[] actualSections = await _context.DocumentSections.AsNoTracking()
            .Where(value => value.DocumentVersionId == version.Id).OrderBy(value => value.SectionOrder)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (!JsonSerializer.Serialize(actualSections.Select(SectionShape)).Equals(
                JsonSerializer.Serialize(expected.Sections.Select(SectionShape)), StringComparison.Ordinal))
        {
            return false;
        }

        IndexEntry? actualIndex = await _context.IndexEntries.AsNoTracking()
            .SingleOrDefaultAsync(value => value.DocumentVersionId == version.Id, cancellationToken)
            .ConfigureAwait(false);
        if (actualIndex is null || !JsonSerializer.Serialize(IndexShape(actualIndex)).Equals(
                JsonSerializer.Serialize(IndexShape(expected.IndexEntry)), StringComparison.Ordinal))
        {
            return false;
        }

        Guid[] usedIds = citedHandles.Select(handle => evidence[handle].DocumentVersionId)
            .Distinct().OrderBy(value => value).ToArray();
        DocumentEdge[] generated = await _context.DocumentEdges.AsNoTracking()
            .Where(value => value.SourceDocumentVersionId == version.Id &&
                value.EdgeType == DocumentEdgeType.GeneratedFrom)
            .OrderBy(value => value.TargetDocumentVersionId).ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        DocumentVersion[] usedDocuments = await _context.DocumentVersions.AsNoTracking()
            .Where(value => usedIds.Contains(value.Id)).ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (usedDocuments.Length != usedIds.Length || generated.Length != usedIds.Length ||
            !generated.Select(value => value.TargetDocumentVersionId)
                .SequenceEqual(usedIds) || generated.Any(value =>
                    value.SourceRepositoryId != version.RepositoryId ||
                    value.SourceScanRunId != version.ScanRunId || value.CreatedAt != version.CreatedAt ||
                    value.Id != DeriveId("pass2-generated-from-edge/1.0", version.Id.ToString("D"),
                        value.TargetDocumentVersionId.ToString("D")) ||
                    usedDocuments.Single(document => document.Id == value.TargetDocumentVersionId).RepositoryId !=
                        value.TargetRepositoryId ||
                    usedDocuments.Single(document => document.Id == value.TargetDocumentVersionId).ScanRunId !=
                        value.TargetScanRunId))
        {
            return false;
        }

        return true;
    }

    private async Task<bool> ValidateVersionChainAsync(
        LogicalDocument logical,
        DocumentVersion replayedVersion,
        CancellationToken cancellationToken)
    {
        DocumentVersion[] versions = await _context.DocumentVersions.AsNoTracking()
            .Where(value => value.LogicalDocumentId == logical.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (logical.CurrentVersionId is not Guid currentVersionId ||
            versions.Count(value => value.Id == replayedVersion.Id) != 1 ||
            versions.Count(value => value.Id == currentVersionId) != 1 ||
            replayedVersion.VersionNumber < 1)
        {
            return false;
        }

        if (replayedVersion.VersionNumber == 1)
        {
            if (versions.Any(value => value.SupersededById == replayedVersion.Id))
            {
                return false;
            }
        }
        else if (versions.Count(value => value.SupersededById == replayedVersion.Id) != 1)
        {
            return false;
        }

        var visited = new HashSet<Guid>();
        DocumentVersion cursor = versions.Single(value => value.Id == replayedVersion.Id);
        while (visited.Add(cursor.Id))
        {
            if (cursor.Id == currentVersionId)
            {
                return cursor.SupersededById is null &&
                    versions.Count(value => value.VersionNumber >= replayedVersion.VersionNumber &&
                        value.VersionNumber <= cursor.VersionNumber) ==
                    cursor.VersionNumber - replayedVersion.VersionNumber + 1;
            }

            if (cursor.SupersededById is not Guid successorId)
            {
                return false;
            }

            DocumentVersion? successor = versions.SingleOrDefault(value => value.Id == successorId);
            if (successor is null || successor.LogicalDocumentId != logical.Id ||
                successor.VersionNumber != cursor.VersionNumber + 1 ||
                versions.Count(value => value.SupersededById == successor.Id) != 1)
            {
                return false;
            }
            cursor = successor;
        }

        return false;
    }

    private static object SectionShape(DocumentSection value) => new
    {
        value.Id, value.LogicalDocumentId, value.DocumentVersionId, value.RepositoryId,
        value.StableSectionId, value.SectionOrder, value.SectionKind, value.Heading,
        value.ContentMarkdown, value.SearchText, value.SourceAnchorIdsJson,
        value.DocumentVersionIdsJson, value.CreatedAt,
    };

    private static object IndexShape(IndexEntry value) => new
    {
        value.Id, value.LogicalDocumentId, value.DocumentVersionId, value.RepositoryId,
        value.SystemId, value.DocumentType, value.Title, value.Summary,
        value.RepositoryRelativePath, value.KeySymbolsJson, value.BusinessTermsJson,
        value.TechnicalTermsJson, value.SearchText, value.CreatedAt,
    };

    private static bool JsonEquivalent(string left, string right)
    {
        using JsonDocument leftDocument = JsonDocument.Parse(left);
        using JsonDocument rightDocument = JsonDocument.Parse(right);
        return JsonElement.DeepEquals(leftDocument.RootElement, rightDocument.RootElement);
    }

    private static string CompletionFingerprint(
        Pass2BusinessLogicCommitRequest request,
        ValidationAuthority authority,
        IReadOnlyDictionary<Guid, EvidenceDescriptor> evidence) =>
        Hash(string.Join(
            '\n',
            "pass2-business-logic-completion/1.0",
            request.Pass2JobExecutionId.ToString("D"),
            request.AttemptOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.PromptIdentity,
            request.PromptFingerprint,
            request.SubmissionSchemaIdentity,
            request.SubmissionSchemaFingerprint,
            request.ToolDeclarationFingerprint,
            JsonSerializer.Serialize(request.Candidate),
            authority.AuthorityFingerprint,
            string.Join('\n', evidence.OrderBy(value => value.Key).Select(value => string.Join('|',
                value.Key.ToString("D"),
                value.Value.Kind,
                value.Value.DocumentVersionId.ToString("D"),
                value.Value.RepositoryId.ToString("D"),
                value.Value.ScanRunId.ToString("D"),
                value.Value.ProfiledRevision ?? string.Empty,
                value.Value.SectionId?.ToString("D") ?? string.Empty,
                value.Value.SourceAnchorId?.ToString("D") ?? string.Empty,
                value.Value.EdgeId?.ToString("D") ?? string.Empty,
                value.Value.ByteStart?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                value.Value.ByteEnd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                value.Value.DescriptorFingerprint,
                value.Value.EvidenceFingerprint,
                value.Value.ReturnedUtf8Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                value.Value.SourceHash ?? string.Empty)))));

    private bool RequestContractMatches(Pass2BusinessLogicCommitRequest request) =>
        StringComparer.Ordinal.Equals(request.PromptIdentity, WorkerContract.PromptIdentity) &&
        StringComparer.Ordinal.Equals(request.PromptFingerprint, WorkerContract.PromptFingerprint) &&
        StringComparer.Ordinal.Equals(
            request.SubmissionSchemaIdentity,
            WorkerContract.SubmissionSchemaIdentity) &&
        StringComparer.Ordinal.Equals(
            request.SubmissionSchemaFingerprint,
            WorkerContract.SubmissionSchemaFingerprint) &&
        StringComparer.Ordinal.Equals(
            request.ToolDeclarationFingerprint,
            WorkerContract.ToolDeclarationFingerprint);

    private static Guid DeriveId(string schema, params string[] identities)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('\n', new[] { schema }.Concat(identities))))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    private static string ProviderFingerprint(
        IReadOnlyCollection<ExactResolutionProviderEvidenceContribution> contributions) =>
        Hash(string.Join('\n', new[] { "provider-evidence-set/1.0" }.Concat(
            contributions.OrderBy(value => value.ProviderOrdinal).ThenBy(value => value.EvidenceOrdinal)
                .Select(value => value.EvidenceFingerprint))));

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record ValidationAuthority(
        Pass2JobExecution Job,
        FlowGroup Group,
        Pass2Execution Execution,
        CrossRepositoryResolutionDecision Decision,
        DocumentEdge Edge,
        IReadOnlyList<ExactResolutionProviderEvidenceContribution> Contributions,
        IReadOnlyList<Guid> RepositoryIds,
        IReadOnlyList<Guid> InitialDocumentVersionIds,
        string AuthorityFingerprint);

    private sealed record EvidenceDescriptor(
        Pass2CodeLogicRetrievalItemKind Kind,
        Guid DocumentVersionId,
        Guid RepositoryId,
        Guid ScanRunId,
        string? ProfiledRevision,
        Guid? SectionId,
        Guid? SourceAnchorId,
        Guid? EdgeId,
        long? ByteStart,
        long? ByteEnd,
        string DescriptorFingerprint,
        string EvidenceFingerprint,
        int ReturnedUtf8Bytes,
        string? SourceHash);
}
