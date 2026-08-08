using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class PreparedDemoPass2FlowPlanner(
    VulgataDbContext dbContext,
    IKnowledgeStoreRepository knowledgeStore) : IPreparedDemoPass2FlowPlanner
{
    private readonly VulgataDbContext _dbContext = dbContext ??
        throw new ArgumentNullException(nameof(dbContext));
    private readonly IKnowledgeStoreRepository _knowledgeStore = knowledgeStore ??
        throw new ArgumentNullException(nameof(knowledgeStore));

    public async Task<ResolvedPreparedPass2Flow?> PlanAsync(
        ExactResolutionProfile profile,
        ExactResolutionRuntimeRequest runtimeRequest,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(runtimeRequest);
        ArgumentNullException.ThrowIfNull(scope);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!StringComparer.Ordinal.Equals(profile.SchemaVersion, "1.0") ||
                !StringComparer.Ordinal.Equals(profile.ProfileId, runtimeRequest.ProfileId) ||
                !StringComparer.Ordinal.Equals(profile.ProfileVersion, runtimeRequest.ProfileVersion) ||
                !StringComparer.Ordinal.Equals(profile.ProfileFingerprint, runtimeRequest.ProfileFingerprint) ||
                profile.Repositories.Count != 2 || profile.Occurrences.Count != 1 ||
                runtimeRequest.Bindings.Count != 2)
            {
                return null;
            }

            ResolutionOccurrence occurrence = profile.Occurrences[0];
            ExactResolutionRuntimeBinding consumerRuntime = runtimeRequest.Bindings.Single(
                value => StringComparer.Ordinal.Equals(value.Alias, occurrence.ConsumerAlias));
            ExactResolutionRuntimeBinding providerRuntime = runtimeRequest.Bindings.Single(
                value => StringComparer.Ordinal.Equals(value.Alias, occurrence.ProviderAlias));
            ProfileRepository consumerProfile = profile.Repositories.Single(
                value => StringComparer.Ordinal.Equals(value.Alias, occurrence.ConsumerAlias));
            ProfileRepository providerProfile = profile.Repositories.Single(
                value => StringComparer.Ordinal.Equals(value.Alias, occurrence.ProviderAlias));
            Guid[] repositoryIds = [consumerRuntime.RepositoryId, providerRuntime.RepositoryId];
            if (repositoryIds.Distinct().Count() != 2 ||
                repositoryIds.Any(id => !scope.Contains(id)) ||
                !await scope.HasCurrentAccessAsync(repositoryIds, cancellationToken)
                    .ConfigureAwait(false))
            {
                return null;
            }

            int repositoryCount = await _dbContext.Repositories.AsNoTracking()
                .CountAsync(repository =>
                    repositoryIds.Contains(repository.Id) &&
                    repository.SystemId == runtimeRequest.SystemId,
                    cancellationToken).ConfigureAwait(false);
            if (repositoryCount != 2)
            {
                return null;
            }

            ExactResolutionRuntimeBinding[] runtimeBindings = [consumerRuntime, providerRuntime];
            ScanRun[] scanRuns = await _dbContext.ScanRuns.AsNoTracking()
                .Where(scanRun => runtimeBindings.Select(value => value.ScanRunId).Contains(scanRun.Id))
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            ScanExecution[] executions = await _dbContext.ScanExecutions.AsNoTracking()
                .Where(execution => runtimeBindings.Select(value => value.ScanExecutionId).Contains(execution.Id))
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (scanRuns.Length != 2 || executions.Length != 2)
            {
                return null;
            }

            if (!RuntimeStateMatches(
                    consumerRuntime,
                    consumerProfile,
                    runtimeRequest.SystemId,
                    scanRuns,
                    executions) ||
                !RuntimeStateMatches(
                    providerRuntime,
                    providerProfile,
                    runtimeRequest.SystemId,
                    scanRuns,
                    executions))
            {
                return null;
            }

            ExactCrossRepositoryEdgeEvidence[] exactEvidence = await _dbContext
                .ExactCrossRepositoryEdgeEvidence.AsNoTracking()
                .Where(value =>
                    value.ProfileSchemaVersion == profile.SchemaVersion &&
                    value.ProfileId == profile.ProfileId &&
                    value.ProfileVersion == profile.ProfileVersion &&
                    value.ProfileFingerprint == profile.ProfileFingerprint &&
                    value.OccurrenceId == occurrence.OccurrenceId &&
                    value.ConsumerRepositoryId == consumerRuntime.RepositoryId &&
                    value.ConsumerScanRunId == consumerRuntime.ScanRunId &&
                    value.ProviderRepositoryId == providerRuntime.RepositoryId &&
                    value.ProviderScanRunId == providerRuntime.ScanRunId)
                .Take(2)
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (exactEvidence.Length != 1)
            {
                return null;
            }

            ExactCrossRepositoryEdgeEvidence evidence = exactEvidence[0];
            DocumentEdge[] edges = await _dbContext.DocumentEdges.AsNoTracking()
                .Where(edge => edge.Id == evidence.EdgeId)
                .Take(2)
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (edges.Length != 1 ||
                edges[0].EdgeType != DocumentEdgeType.References ||
                edges[0].SourceDocumentVersionId != evidence.ConsumerDocumentVersionId ||
                edges[0].SourceRepositoryId != consumerRuntime.RepositoryId ||
                edges[0].TargetDocumentVersionId != evidence.ProviderDocumentVersionId ||
                edges[0].TargetRepositoryId != providerRuntime.RepositoryId)
            {
                return null;
            }

            Guid[] versionIds =
                [evidence.ConsumerDocumentVersionId, evidence.ProviderDocumentVersionId];
            DocumentVersion[] versions = await _dbContext.DocumentVersions.AsNoTracking()
                .Where(version => versionIds.Contains(version.Id))
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            LogicalDocument[] logicalDocuments = await _dbContext.LogicalDocuments.AsNoTracking()
                .Where(document =>
                    document.CurrentVersionId.HasValue &&
                    versionIds.Contains(document.CurrentVersionId.Value))
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            Guid[] exactAnchorIds =
                [evidence.ConsumerSourceAnchorId, evidence.ProviderSourceAnchorId];
            SourceAnchor[] exactAnchors = await _dbContext.SourceAnchors.AsNoTracking()
                .Where(anchor => exactAnchorIds.Contains(anchor.Id))
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (versions.Length != 2 || logicalDocuments.Length != 2 || exactAnchors.Length != 2)
            {
                return null;
            }

            var members = new List<PreparedPass2RuntimeMember>(2);
            var currentDocuments = new List<CurrentKnowledge>(2);
            foreach ((ExactResolutionRuntimeBinding runtime, ProfileRepository repositoryProfile,
                Guid versionId, Guid exactAnchorId) in new[]
                {
                    (consumerRuntime, consumerProfile, evidence.ConsumerDocumentVersionId,
                        evidence.ConsumerSourceAnchorId),
                    (providerRuntime, providerProfile, evidence.ProviderDocumentVersionId,
                        evidence.ProviderSourceAnchorId),
                })
            {
                DocumentVersion version = versions.Single(value => value.Id == versionId);
                LogicalDocument logicalDocument = logicalDocuments.Single(
                    value => value.CurrentVersionId == versionId);
                SourceAnchor exactAnchor = exactAnchors.Single(value => value.Id == exactAnchorId);
                ScanExecution execution = executions.Single(value => value.Id == runtime.ScanExecutionId);
                if (logicalDocument.DocumentType != DocumentType.CodeLogic ||
                    logicalDocument.RepositoryId != runtime.RepositoryId ||
                    version.LogicalDocumentId != logicalDocument.Id ||
                    version.RepositoryId != runtime.RepositoryId ||
                    version.ScanRunId != runtime.ScanRunId ||
                    version.SupersededById is not null ||
                    !StringComparer.Ordinal.Equals(version.ProfiledRevision, repositoryProfile.ExpectedCommit) ||
                    exactAnchor.DocumentVersionId != versionId ||
                    exactAnchor.RepositoryId != runtime.RepositoryId ||
                    exactAnchor.ScanRunId != runtime.ScanRunId ||
                    !StringComparer.Ordinal.Equals(exactAnchor.ProfiledRevision, repositoryProfile.ExpectedCommit))
                {
                    return null;
                }

                OrdinaryPass1JobIdentity jobIdentity = OrdinaryPass1JobIdentity.Create(
                    execution.Id,
                    execution.ScanRunId,
                    execution.RepositoryId,
                    exactAnchor.CodeUnitId,
                    execution.ProfiledRevision);
                if (OrdinaryPass1JobIdentity.CreateDocumentVersionId(jobIdentity.JobId) != version.Id)
                {
                    return null;
                }

                CurrentKnowledge? current = await _knowledgeStore.GetCurrentAsync(
                    logicalDocument.Id,
                    scope,
                    [],
                    cancellationToken).ConfigureAwait(false);
                if (current is null || current.DocumentVersionId != version.Id ||
                    current.RepositoryId != runtime.RepositoryId ||
                    current.SourceAnchors.All(anchor => anchor.Id != exactAnchor.Id))
                {
                    return null;
                }

                members.Add(new(
                    members.Count + 1,
                    jobIdentity.JobId,
                    exactAnchor.CodeUnitId,
                    logicalDocument.Id,
                    runtime.RepositoryId,
                    version.Id,
                    current.SourceAnchors.Select(anchor => anchor.Id)
                        .OrderBy(id => Convert.ToHexString(id.ToByteArray(bigEndian: true)),
                            StringComparer.Ordinal)
                        .ToArray()));
                currentDocuments.Add(current);
            }

            var consumerBinding = ToRuntimeBinding(consumerRuntime, consumerProfile, executions);
            var providerBinding = ToRuntimeBinding(providerRuntime, providerProfile, executions);
            PreparedPass2TraceEvidence[] traceEvidence =
            [
                new(1, members[0].JobId, members[0].CodeUnitId,
                    "消费方调用", evidence.ConsumerEvidenceLocator),
                new(2, members[1].JobId, members[1].CodeUnitId,
                    "提供方实现", evidence.ProviderEvidenceLocator),
            ];
            PreparedPass2RuntimeBinding binding = PreparedPass2RuntimeBinding.Create(
                profile.SchemaVersion,
                profile.ProfileId,
                profile.ProfileVersion,
                profile.ProfileFingerprint,
                occurrence.OccurrenceId,
                runtimeRequest.SystemId,
                consumerBinding,
                providerBinding,
                evidence.EdgeId,
                [evidence.EdgeId],
                members,
                traceEvidence);
            return new(binding.Flow, currentDocuments.AsReadOnly(), binding);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool RuntimeStateMatches(
        ExactResolutionRuntimeBinding runtime,
        ProfileRepository profile,
        Guid systemId,
        IReadOnlyList<ScanRun> scanRuns,
        IReadOnlyList<ScanExecution> executions)
    {
        ScanRun scanRun = scanRuns.Single(value => value.Id == runtime.ScanRunId);
        ScanExecution execution = executions.Single(value => value.Id == runtime.ScanExecutionId);
        return scanRun.RepositoryId == runtime.RepositoryId &&
            scanRun.Status == ScanRunStatus.Completed &&
            StringComparer.Ordinal.Equals(scanRun.ProfiledRevision, profile.ExpectedCommit) &&
            execution.RepositoryId == runtime.RepositoryId &&
            execution.SystemId == systemId && execution.ScanRunId == runtime.ScanRunId &&
            StringComparer.Ordinal.Equals(execution.ProfiledRevision, profile.ExpectedCommit) &&
            execution.Phase is ScanExecutionPhase.Pass1Completed or ScanExecutionPhase.Completed;
    }

    private static PreparedPass2RuntimeRepositoryBinding ToRuntimeBinding(
        ExactResolutionRuntimeBinding runtime,
        ProfileRepository profile,
        IReadOnlyList<ScanExecution> executions) =>
        new(
            runtime.Alias,
            runtime.StableGitIdentity,
            runtime.RepositoryId,
            runtime.ScanRunId,
            runtime.ScanExecutionId,
            profile.ExpectedRef,
            profile.ExpectedCommit,
            executions.Single(value => value.Id == runtime.ScanExecutionId).ProfiledRevision);
}
