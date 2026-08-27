using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;

namespace Vulgata.Infrastructure.Data;

public sealed class ExactCrossRepositoryResolutionStore(VulgataDbContext context) :
    IExactCrossRepositoryResolutionStore
{
    private const string _profileSchemaVersion = "1.0";
    private const string _resolutionKind = "ExactConfigured";
    private readonly VulgataDbContext _context = context;

    public async Task<ExactResolutionBatchOutcome> ApplyAsync(
        ExactResolutionDecisionBatch decisions,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(scope);
        ExactResolutionDecision[] materializedDecisions = [.. decisions.Decisions];
        Guid[] requiredRepositoryIds =
        [
            .. materializedDecisions.SelectMany(Owners)
                .Select(owner => owner.RepositoryId)
                .Distinct()
                .Order(),
        ];
        if (!ValidateCompleteBatch(decisions) ||
            materializedDecisions.Any(decision =>
                !scope.Contains(decision.Owner.RepositoryId) ||
                decision.Target is not null && !scope.Contains(decision.Target.RepositoryId)) ||
            !await scope.HasCurrentAccessAsync(requiredRepositoryIds, cancellationToken))
        {
            return Refused();
        }

        IExecutionStrategy strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                bool isSqlite = StringComparer.Ordinal.Equals(
                    _context.Database.ProviderName,
                    "Microsoft.EntityFrameworkCore.Sqlite");
                await using IDbContextTransaction transaction =
                    await _context.Database.BeginTransactionAsync(
                        isSqlite ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted,
                        cancellationToken);
                try
                {
                    if (!await scope.HasCurrentAccessAsync(
                            requiredRepositoryIds,
                            cancellationToken))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return ClearAndRefused();
                    }

                    V2AuthorityResolution authorityResolution =
                        await ResolveV2AuthorityAsync(
                            materializedDecisions,
                            cancellationToken).ConfigureAwait(false);
                    if (authorityResolution.IsConflict)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return ClearAndConflict();
                    }

                    V2PairAuthority? v2Authority = authorityResolution.Authority;
                    await AcquireOwnerLocksAsync(materializedDecisions, isSqlite, cancellationToken);
                    if (!await OwnersAreCurrentAsync(
                            materializedDecisions,
                            scope,
                            cancellationToken))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return ClearAndConflict();
                    }

                    ExactResolutionDecision[] authoritativeDecisions = materializedDecisions;
                    if (v2Authority is not null)
                    {
                        ExactResolutionDecision[]? resolved =
                            await ResolveProviderAuthorityAsync(
                                decisions,
                                materializedDecisions,
                                cancellationToken).ConfigureAwait(false);
                        if (resolved is null)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return ClearAndConflict();
                        }

                        authoritativeDecisions = resolved;
                    }

                    DateTimeOffset createdAt = DateTimeOffset.UtcNow;
                    var items = new List<ExactResolutionBatchItemOutcome>(
                        authoritativeDecisions.Length);
                    foreach (ExactResolutionDecision decision in authoritativeDecisions)
                    {
                        ExactResolutionOutcomeKind itemKind;
                        if (decision.Kind == ExactResolutionDecisionKind.ExactMatch)
                        {
                            MaterializedExactWrite write = MaterializeExact(
                                decisions,
                                decision,
                                createdAt,
                                v2Authority is not null);
                            if (v2Authority is null)
                            {
                                itemKind = await PrepareExactWriteAsync(write, cancellationToken);
                            }
                            else
                            {
                                MaterializedExactWrite? preparedWrite = await PrepareV2EdgeAsync(
                                    write, cancellationToken).ConfigureAwait(false);
                                itemKind = preparedWrite is null
                                    ? ExactResolutionOutcomeKind.Conflict
                                    : await PrepareV2ExactWriteAsync(
                                        decisions,
                                        decision,
                                        preparedWrite,
                                        v2Authority,
                                        cancellationToken).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            Uncertainty uncertainty = MaterializeUncertainty(
                                decisions,
                                decision,
                                createdAt);
                            itemKind = v2Authority is null
                                ? await PrepareUncertaintyWriteAsync(
                                    uncertainty, cancellationToken)
                                : await PrepareV2UncertaintyWriteAsync(
                                    decision, uncertainty, v2Authority, cancellationToken);
                        }

                        if (itemKind == ExactResolutionOutcomeKind.Conflict)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return ClearAndConflict();
                        }

                        items.Add(new(decision.Identity, itemKind));
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    if (!await scope.HasCurrentAccessAsync(
                            requiredRepositoryIds,
                            cancellationToken))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return ClearAndRefused();
                    }

                    await transaction.CommitAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                    ExactResolutionOutcomeKind kind = items.All(item =>
                        item.Kind is ExactResolutionOutcomeKind.AlreadyApplied or
                            ExactResolutionOutcomeKind.AlreadyRecorded)
                            ? ExactResolutionOutcomeKind.AlreadyApplied
                            : ExactResolutionOutcomeKind.Applied;
                    return new ExactResolutionBatchOutcome(kind, items.AsReadOnly());
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    _context.ChangeTracker.Clear();
                    throw;
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            return ClearAndConflict();
        }
        catch (Exception)
        {
            _context.ChangeTracker.Clear();
            return new ExactResolutionBatchOutcome(
                ExactResolutionOutcomeKind.Failed,
                [],
                "failed");
        }
    }

    private static bool ValidateCompleteBatch(ExactResolutionDecisionBatch batch)
    {
        if (batch.Decisions.Count is < 2 or > ExactResolutionLimits.MaxDecisions ||
            batch.Decisions.Select(decision => decision.Identity)
                .Distinct(StringComparer.Ordinal).Count() != batch.Decisions.Count ||
            !batch.Decisions.Select(decision => decision.Identity)
                .SequenceEqual(
                    batch.Decisions.Select(decision => decision.Identity)
                        .Order(StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            return false;
        }

        foreach (ExactResolutionDecision decision in batch.Decisions)
        {
            ExactResolutionDecisionEvidence? evidence = decision.ExactEvidence;
            if (decision.Kind == ExactResolutionDecisionKind.ExactMatch)
            {
                if (decision.Target is null ||
                    evidence is null ||
                    decision.Owner.RepositoryId == decision.Target.RepositoryId ||
                    !StringComparer.Ordinal.Equals(decision.ReasonCode, "exact_configured") ||
                    !StringComparer.Ordinal.Equals(
                        decision.EvidenceLocator,
                        evidence.ConsumerEvidenceLocator) ||
                    !StringComparer.Ordinal.Equals(
                        decision.TargetEvidenceLocator,
                        evidence.ProviderEvidenceLocator) ||
                    ContainsInternalIdentity(batch, decision, evidence))
                {
                    return false;
                }
            }
            else if (evidence is not null ||
                decision.TargetEvidenceLocator is not null ||
                decision.Kind == ExactResolutionDecisionKind.UnsupportedObservation &&
                (!StringComparer.Ordinal.Equals(decision.ReasonCode, "unsupported_kind") ||
                    decision.Target is not null) ||
                decision.Kind == ExactResolutionDecisionKind.Uncertainty &&
                !IsNonExactReason(decision.ReasonCode) ||
                ContainsInternalIdentity(batch, decision))
            {
                return false;
            }

            try
            {
                KnowledgeTextSafety.EnsureSafe(decision.EvidenceLocator);
                if (evidence is not null)
                {
                    KnowledgeTextSafety.EnsureSafe(evidence.ConsumerRawIdentifier);
                    KnowledgeTextSafety.EnsureSafe(evidence.ProviderRawIdentifier);
                    KnowledgeTextSafety.EnsureSafe(evidence.NormalizedIdentifier);
                    KnowledgeTextSafety.EnsureSafe(evidence.ConsumerEvidenceLocator);
                    KnowledgeTextSafety.EnsureSafe(evidence.ProviderEvidenceLocator);
                }
            }
            catch (KnowledgeStoreContractException)
            {
                return false;
            }
        }

        return true;
    }

    private async Task AcquireOwnerLocksAsync(
        IReadOnlyCollection<ExactResolutionDecision> decisions,
        bool isSqlite,
        CancellationToken cancellationToken)
    {
        Guid[] versionIds =
        [
            .. decisions.SelectMany(Owners)
                .Select(owner => owner.DocumentVersionId)
                .Distinct(),
        ];
        Guid[] ownerIds =
        [
            .. (await _context.LogicalDocuments
                    .AsNoTracking()
                    .Where(document =>
                        document.CurrentVersionId != null &&
                        versionIds.Contains(document.CurrentVersionId.Value))
                    .Select(document => document.Id)
                    .ToArrayAsync(cancellationToken))
                .Distinct()
                .Order(),
        ];
        if (isSqlite)
        {
            foreach (Guid ownerId in ownerIds)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"LogicalDocuments\" SET \"Id\" = \"Id\" WHERE \"Id\" = {ownerId}",
                    cancellationToken);
            }
        }
        else if (StringComparer.Ordinal.Equals(
                _context.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL") &&
            ownerIds.Length > 0)
        {
            _ = await _context.LogicalDocuments
                .FromSqlInterpolated(
                    $"SELECT * FROM \"vulgata\".\"LogicalDocuments\" WHERE \"Id\" = ANY ({ownerIds}) ORDER BY \"Id\" FOR UPDATE")
                .AsNoTracking()
                .Select(document => document.Id)
                .ToArrayAsync(cancellationToken);
        }
    }

    private async Task<bool> OwnersAreCurrentAsync(
        IReadOnlyCollection<ExactResolutionDecision> decisions,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken)
    {
        foreach (ExactResolutionOwner owner in decisions.SelectMany(Owners))
        {
            if (!scope.Contains(owner.RepositoryId) ||
                !await _context.DocumentVersions.AsNoTracking().AnyAsync(version =>
                    version.Id == owner.DocumentVersionId &&
                    version.RepositoryId == owner.RepositoryId &&
                    version.ScanRunId == owner.ScanRunId &&
                    version.ProfiledRevision == owner.ProfiledRevision &&
                    version.SupersededById == null,
                    cancellationToken) ||
                !await _context.LogicalDocuments.AsNoTracking().AnyAsync(document =>
                    document.RepositoryId == owner.RepositoryId &&
                    document.DocumentType == DocumentType.CodeLogic &&
                    document.CurrentVersionId == owner.DocumentVersionId,
                    cancellationToken) ||
                !await _context.ScanRuns.AsNoTracking().AnyAsync(scanRun =>
                    scanRun.Id == owner.ScanRunId &&
                    scanRun.RepositoryId == owner.RepositoryId &&
                    scanRun.ProfiledRevision == owner.ProfiledRevision,
                    cancellationToken) ||
                !await _context.SourceAnchors.AsNoTracking().AnyAsync(anchor =>
                    anchor.Id == owner.SourceAnchorId &&
                    anchor.DocumentVersionId == owner.DocumentVersionId &&
                    anchor.RepositoryId == owner.RepositoryId &&
                    anchor.ScanRunId == owner.ScanRunId &&
                    anchor.ProfiledRevision == owner.ProfiledRevision,
                    cancellationToken))
            {
                return false;
            }
        }

        return decisions.SelectMany(Owners)
            .Select(owner => owner.RepositoryId)
            .Distinct()
            .All(scope.Contains);
    }

    private static IEnumerable<ExactResolutionOwner> Owners(
        ExactResolutionDecision decision)
    {
        yield return decision.Owner;
        if (decision.Target is not null)
        {
            yield return decision.Target;
        }
    }

    private async Task<V2AuthorityResolution> ResolveV2AuthorityAsync(
        IReadOnlyCollection<ExactResolutionDecision> decisions,
        CancellationToken cancellationToken)
    {
        ExactResolutionOwner[] owners = decisions.SelectMany(Owners).Distinct().ToArray();
        Guid[] repositoryIds = owners.Select(value => value.RepositoryId).Distinct().ToArray();
        Guid[] scanRunIds = owners.Select(value => value.ScanRunId).Distinct().ToArray();
        PreparedScanManifest[] candidates = await _context.PreparedScanManifests.AsNoTracking()
            .Where(value => repositoryIds.Contains(value.RepositoryId) ||
                scanRunIds.Contains(value.ScanRunId))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (candidates.Length == 0)
        {
            return new(false, null);
        }

        Guid[] manifestIds = candidates.Select(value => value.Id).ToArray();
        Pass1PairExecution[] pairs = await _context.Pass1PairExecutions.AsNoTracking()
            .Where(value => manifestIds.Contains(value.ManifestOneId) ||
                manifestIds.Contains(value.ManifestTwoId))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var exact = new List<V2PairAuthority>();
        foreach (Pass1PairExecution pair in pairs)
        {
            PreparedScanManifest? one = candidates.SingleOrDefault(value =>
                value.Id == pair.ManifestOneId);
            PreparedScanManifest? two = candidates.SingleOrDefault(value =>
                value.Id == pair.ManifestTwoId);
            TwoRepositoryScanCoordination? coordination = await _context
                .TwoRepositoryScanCoordinations.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == pair.CoordinationId, cancellationToken)
                .ConfigureAwait(false);
            if (one is null || two is null || coordination is null ||
                pair.Phase != Pass1PairExecutionPhase.ReadyForResolution ||
                pair.TerminalAt is null || pair.SystemId != coordination.SystemId ||
                one.CoordinationId != pair.CoordinationId || two.CoordinationId != pair.CoordinationId ||
                !owners.All(owner => OwnerMatches(owner, one) || OwnerMatches(owner, two)))
            {
                continue;
            }

            exact.Add(new(pair, coordination, one, two));
        }

        return exact.Count == 1
            ? new(false, exact[0])
            : new(true, null);
    }

    private static bool OwnerMatches(ExactResolutionOwner owner, PreparedScanManifest manifest) =>
        owner.RepositoryId == manifest.RepositoryId &&
        owner.ScanRunId == manifest.ScanRunId &&
        StringComparer.Ordinal.Equals(owner.ProfiledRevision, manifest.ProfiledRevision);

    private async Task<MaterializedExactWrite?> PrepareV2EdgeAsync(
        MaterializedExactWrite write,
        CancellationToken cancellationToken)
    {
        DocumentEdge? existing = await _context.DocumentEdges.AsNoTracking()
            .SingleOrDefaultAsync(edge =>
                edge.SourceDocumentVersionId == write.Edge.SourceDocumentVersionId &&
                edge.TargetDocumentVersionId == write.Edge.TargetDocumentVersionId &&
                edge.EdgeType == DocumentEdgeType.References,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return EdgeMatchesIgnoringIdentity(existing, write.Edge)
                ? new(existing, write.Evidence)
                : null;
        }

        _context.DocumentEdges.Add(write.Edge);
        return write;
    }

    private static bool EdgeMatchesIgnoringIdentity(DocumentEdge actual, DocumentEdge expected) =>
        actual.SourceDocumentVersionId == expected.SourceDocumentVersionId &&
        actual.SourceRepositoryId == expected.SourceRepositoryId &&
        actual.SourceScanRunId == expected.SourceScanRunId &&
        actual.TargetDocumentVersionId == expected.TargetDocumentVersionId &&
        actual.TargetRepositoryId == expected.TargetRepositoryId &&
        actual.TargetScanRunId == expected.TargetScanRunId &&
        actual.EdgeType == DocumentEdgeType.References;

    private async Task<ExactResolutionOutcomeKind> PrepareExactWriteAsync(
        MaterializedExactWrite write,
        CancellationToken cancellationToken)
    {
        DocumentEdge? edgeById = await _context.DocumentEdges.AsNoTracking()
            .SingleOrDefaultAsync(edge => edge.Id == write.Edge.Id, cancellationToken);
        DocumentEdge? edgeByEndpoints = await _context.DocumentEdges.AsNoTracking()
            .SingleOrDefaultAsync(edge =>
                edge.SourceDocumentVersionId == write.Edge.SourceDocumentVersionId &&
                edge.TargetDocumentVersionId == write.Edge.TargetDocumentVersionId &&
                edge.EdgeType == write.Edge.EdgeType,
                cancellationToken);
        ExactCrossRepositoryEdgeEvidence? evidenceByEdge =
            await _context.ExactCrossRepositoryEdgeEvidence.AsNoTracking()
                .SingleOrDefaultAsync(
                    evidence => evidence.EdgeId == write.Evidence.EdgeId,
                    cancellationToken);
        ExactCrossRepositoryEdgeEvidence? evidenceByFingerprint =
            await _context.ExactCrossRepositoryEdgeEvidence.AsNoTracking()
                .SingleOrDefaultAsync(
                    evidence => evidence.EvidenceFingerprint ==
                        write.Evidence.EvidenceFingerprint,
                    cancellationToken);
        if (edgeById is not null ||
            edgeByEndpoints is not null ||
            evidenceByEdge is not null ||
            evidenceByFingerprint is not null)
        {
            return edgeById is not null &&
                edgeByEndpoints?.Id == edgeById.Id &&
                evidenceByEdge is not null &&
                evidenceByFingerprint?.EdgeId == evidenceByEdge.EdgeId &&
                edgeById.CreatedAt == evidenceByEdge.CreatedAt &&
                EdgeMatches(edgeById, write.Edge) &&
                EvidenceMatches(evidenceByEdge, write.Evidence)
                    ? ExactResolutionOutcomeKind.AlreadyApplied
                    : ExactResolutionOutcomeKind.Conflict;
        }

        _context.DocumentEdges.Add(write.Edge);
        _context.ExactCrossRepositoryEdgeEvidence.Add(write.Evidence);
        return ExactResolutionOutcomeKind.Applied;
    }

    private async Task<ExactResolutionOutcomeKind> PrepareUncertaintyWriteAsync(
        Uncertainty expected,
        CancellationToken cancellationToken)
    {
        Uncertainty? byId = await _context.Uncertainties.AsNoTracking()
            .SingleOrDefaultAsync(
                uncertainty => uncertainty.Id == expected.Id,
                cancellationToken);
        Uncertainty? byFingerprint = await _context.Uncertainties.AsNoTracking()
            .SingleOrDefaultAsync(
                uncertainty => uncertainty.EvidenceFingerprint ==
                    expected.EvidenceFingerprint,
                cancellationToken);
        if (byId is not null || byFingerprint is not null)
        {
            return byId is not null &&
                byFingerprint?.Id == byId.Id &&
                UncertaintyMatches(byId, expected)
                    ? ExactResolutionOutcomeKind.AlreadyRecorded
                    : ExactResolutionOutcomeKind.Conflict;
        }

        _context.Uncertainties.Add(expected);
        return ExactResolutionOutcomeKind.UncertaintyRecorded;
    }

    private async Task<ExactResolutionOutcomeKind> PrepareV2ExactWriteAsync(
        ExactResolutionDecisionBatch batch,
        ExactResolutionDecision source,
        MaterializedExactWrite write,
        V2PairAuthority authority,
        CancellationToken cancellationToken)
    {
        if (!IsLowerSha256(source.ConsumerEntryStableKey))
        {
            return ExactResolutionOutcomeKind.Conflict;
        }

        CrossRepositoryResolutionDecision? existing =
            await _context.CrossRepositoryResolutionDecisions.AsNoTracking()
                .SingleOrDefaultAsync(
                    decision => decision.EvidenceFingerprint ==
                        write.Evidence.EvidenceFingerprint,
                    cancellationToken);
        if (existing is not null)
        {
            ProviderObservationOrdinal[] observations = ProviderObservationOrdinals(
                source,
                existing.SystemId);
            ExactResolutionProviderEvidenceContribution[] existingContributions =
            await _context.ExactResolutionProviderEvidenceContributions.AsNoTracking()
                .Where(contribution => contribution.ResolutionDecisionId == existing.Id)
                .ToArrayAsync(cancellationToken);
            return V2DecisionMatches(existing, source, write) &&
                existingContributions.Length == observations.Length &&
                existingContributions.OrderBy(value => value.ProviderOrdinal)
                    .ThenBy(value => value.EvidenceOrdinal)
                    .Select((actual, index) => V2ContributionMatches(
                        actual,
                        existing.Id,
                        observations[index].Observation,
                        write.Evidence.EvidenceFingerprint,
                        existing.SystemId,
                        write.Edge.CreatedAt,
                        observations[index].ProviderOrdinal,
                        observations[index].EvidenceOrdinal))
                    .All(value => value)
                    ? ExactResolutionOutcomeKind.AlreadyApplied
                    : ExactResolutionOutcomeKind.Conflict;
        }

        Guid decisionId = ExactResolutionCanonicalAuthority.DecisionId(
            write.Evidence.EvidenceFingerprint);
        if (await _context.CrossRepositoryResolutionDecisions.AsNoTracking()
            .AnyAsync(decision => decision.Id == decisionId, cancellationToken))
        {
            return ExactResolutionOutcomeKind.Conflict;
        }

        CrossRepositoryResolutionDecision decision = CreateV2Decision(
            batch,
            source,
            write,
            decisionId,
            authority);
        ExactResolutionProviderEvidenceContribution[] contributions =
            ProviderObservationOrdinals(source, authority.Pair.SystemId)
                .Select(observation => CreateV2Contribution(
                    observation.Observation,
                    write,
                    decisionId,
                    authority,
                    observation.ProviderOrdinal,
                    observation.EvidenceOrdinal))
                .ToArray();
        _context.CrossRepositoryResolutionDecisions.Add(decision);
        _context.ExactResolutionProviderEvidenceContributions.AddRange(contributions);
        return ExactResolutionOutcomeKind.Applied;
    }

    private async Task<ExactResolutionOutcomeKind> PrepareV2UncertaintyWriteAsync(
        ExactResolutionDecision source,
        Uncertainty uncertainty,
        V2PairAuthority authority,
        CancellationToken cancellationToken)
    {
        if (!IsLowerSha256(source.ConsumerEntryStableKey))
        {
            return ExactResolutionOutcomeKind.Conflict;
        }

        CrossRepositoryResolutionDecision? existing = await _context
            .CrossRepositoryResolutionDecisions.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.DecisionFingerprint == uncertainty.EvidenceFingerprint,
                cancellationToken);
        if (existing is not null)
        {
            ProviderEndpointCandidate[] replayObservations = ProviderObservations(source);
            ExactResolutionProviderEvidenceContribution[] contributions = await _context
                .ExactResolutionProviderEvidenceContributions.AsNoTracking()
                .Where(value => value.ResolutionDecisionId == existing.Id)
                .OrderBy(value => value.ProviderOrdinal)
                .ThenBy(value => value.EvidenceOrdinal)
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            Uncertainty? persistedUncertainty = await _context.Uncertainties.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == uncertainty.Id, cancellationToken)
                .ConfigureAwait(false);
            return persistedUncertainty is not null &&
                NonExactDecisionMatches(
                    existing,
                    source,
                    uncertainty,
                    persistedUncertainty.CreatedAt,
                    authority,
                    replayObservations,
                    contributions) &&
                UncertaintyMatches(persistedUncertainty, uncertainty)
                    ? ExactResolutionOutcomeKind.AlreadyRecorded
                    : ExactResolutionOutcomeKind.Conflict;
        }

        ExactResolutionOutcomeKind uncertaintyOutcome =
            await PrepareUncertaintyWriteAsync(uncertainty, cancellationToken);
        if (uncertaintyOutcome == ExactResolutionOutcomeKind.Conflict)
        {
            return uncertaintyOutcome;
        }

        Guid decisionId = ExactResolutionCanonicalAuthority.DecisionId(
            uncertainty.EvidenceFingerprint);
        if (await _context.CrossRepositoryResolutionDecisions.AsNoTracking()
            .AnyAsync(value => value.Id == decisionId, cancellationToken))
        {
            return ExactResolutionOutcomeKind.Conflict;
        }

        ProviderEndpointCandidate[] observations = ProviderObservations(source);
        string[] contributionFingerprints = observations.Select(value =>
            ExactResolutionCanonicalAuthority.ProviderObservationFingerprint(
                uncertainty.EvidenceFingerprint, value)).ToArray();
        string providerSetFingerprint = contributionFingerprints.Length == 0
            ? HashIdentity("provider-evidence-set", "none")
            : ExactResolutionCanonicalAuthority.ProviderEvidenceSetFingerprint(
                contributionFingerprints);
        var decision = (CrossRepositoryResolutionDecision)Activator.CreateInstance(
            typeof(CrossRepositoryResolutionDecision), nonPublic: true)!;
        _context.Entry(decision).Property(value => value.Id).CurrentValue = decisionId;
        _context.Entry(decision).Property(value => value.ResolutionSchemaIdentity).CurrentValue =
            "exact-cross-repository-resolution/2.0";
        _context.Entry(decision).Property(value => value.Pass1PairExecutionId).CurrentValue =
            authority.Pair.Id;
        _context.Entry(decision).Property(value => value.CoordinationId).CurrentValue =
            authority.Pair.CoordinationId;
        _context.Entry(decision).Property(value => value.SystemId).CurrentValue =
            authority.Pair.SystemId;
        _context.Entry(decision).Property(value => value.ConsumerCandidateId).CurrentValue =
            DeterministicGuid(HashIdentity("candidate", source.Identity,
                source.Owner.SourceAnchorId.ToString("D")));
        _context.Entry(decision).Property(value => value.ConsumerEntryIdentity).CurrentValue =
            source.Identity;
        _context.Entry(decision).Property(value => value.ConsumerEntryStableKey).CurrentValue =
            source.ConsumerEntryStableKey!;
        _context.Entry(decision).Property(value => value.ConsumerDocumentVersionId).CurrentValue =
            source.Owner.DocumentVersionId;
        _context.Entry(decision).Property(value => value.ConsumerRepositoryId).CurrentValue =
            source.Owner.RepositoryId;
        _context.Entry(decision).Property(value => value.ConsumerScanRunId).CurrentValue =
            source.Owner.ScanRunId;
        _context.Entry(decision).Property(value => value.ConsumerProfiledRevision).CurrentValue =
            source.Owner.ProfiledRevision;
        _context.Entry(decision).Property(value => value.Protocol).CurrentValue =
            ExactResolutionProtocol.CustomRpc;
        _context.Entry(decision).Property(value => value.NormalizedEndpointIdentifier).CurrentValue =
            observations.FirstOrDefault()?.NormalizedIdentifier ?? source.Identity;
        _context.Entry(decision).Property(value => value.NormalizationRuleIdentity).CurrentValue =
            observations.FirstOrDefault()?.NormalizationRuleIdentity ?? source.Identity;
        _context.Entry(decision).Property(value => value.Outcome).CurrentValue = "Uncertainty";
        _context.Entry(decision).Property(value => value.OutcomeKind).CurrentValue =
            source.Kind.ToString();
        _context.Entry(decision).Property(value => value.ReasonCode).CurrentValue = source.ReasonCode;
        _context.Entry(decision).Property(value => value.UncertaintyId).CurrentValue = uncertainty.Id;
        _context.Entry(decision).Property(value => value.ConsumerEvidenceFingerprint).CurrentValue =
            HashIdentity("consumer-evidence", source.Owner.RepositoryId.ToString("D"),
                source.Owner.ScanRunId.ToString("D"), source.Owner.DocumentVersionId.ToString("D"),
                source.Owner.SourceAnchorId.ToString("D"), source.Owner.ProfiledRevision,
                source.EvidenceLocator);
        _context.Entry(decision).Property(value => value.ProviderEvidenceSetFingerprint).CurrentValue =
            providerSetFingerprint;
        _context.Entry(decision).Property(value => value.DecisionFingerprint).CurrentValue =
            uncertainty.EvidenceFingerprint;
        _context.Entry(decision).Property(value => value.EvidenceFingerprint).CurrentValue =
            uncertainty.EvidenceFingerprint;
        _context.Entry(decision).Property(value => value.CreatedAt).CurrentValue =
            uncertainty.CreatedAt;
        _context.CrossRepositoryResolutionDecisions.Add(decision);
        foreach (ProviderObservationOrdinal observation in
            ProviderObservationOrdinals(source, authority.Pair.SystemId))
        {
            _context.ExactResolutionProviderEvidenceContributions.Add(
                CreateV2Contribution(
                    observation.Observation,
                    uncertainty.EvidenceFingerprint,
                    uncertainty.CreatedAt,
                    decisionId,
                    authority,
                    observation.ProviderOrdinal,
                    observation.EvidenceOrdinal));
        }
        return uncertaintyOutcome == ExactResolutionOutcomeKind.AlreadyRecorded
            ? ExactResolutionOutcomeKind.Applied
            : ExactResolutionOutcomeKind.UncertaintyRecorded;
    }

    private CrossRepositoryResolutionDecision CreateV2Decision(
        ExactResolutionDecisionBatch batch,
        ExactResolutionDecision source,
        MaterializedExactWrite write,
        Guid decisionId,
        V2PairAuthority authority)
    {
        var decision = (CrossRepositoryResolutionDecision)Activator.CreateInstance(
            typeof(CrossRepositoryResolutionDecision),
            nonPublic: true)!;
        _context.Entry(decision).Property(value => value.Id).CurrentValue = decisionId;
        _context.Entry(decision).Property(value => value.ResolutionSchemaIdentity).CurrentValue =
            "exact-cross-repository-resolution/2.0";
        _context.Entry(decision).Property(value => value.Pass1PairExecutionId).CurrentValue =
            authority.Pair.Id;
        _context.Entry(decision).Property(value => value.CoordinationId).CurrentValue =
            authority.Pair.CoordinationId;
        _context.Entry(decision).Property(value => value.SystemId).CurrentValue =
            authority.Pair.SystemId;
        _context.Entry(decision).Property(value => value.ConsumerCandidateId).CurrentValue =
            DeterministicGuid(HashIdentity("candidate", source.Identity,
                source.Owner.SourceAnchorId.ToString("D")));
        _context.Entry(decision).Property(value => value.ConsumerEntryIdentity).CurrentValue =
            source.Identity;
        _context.Entry(decision).Property(value => value.ConsumerEntryStableKey).CurrentValue =
            source.ConsumerEntryStableKey!;
        _context.Entry(decision).Property(value => value.ConsumerDocumentVersionId).CurrentValue =
            source.Owner.DocumentVersionId;
        _context.Entry(decision).Property(value => value.ConsumerRepositoryId).CurrentValue =
            source.Owner.RepositoryId;
        _context.Entry(decision).Property(value => value.ConsumerScanRunId).CurrentValue =
            source.Owner.ScanRunId;
        _context.Entry(decision).Property(value => value.ConsumerProfiledRevision).CurrentValue =
            source.Owner.ProfiledRevision;
        _context.Entry(decision).Property(value => value.Protocol).CurrentValue =
            source.ExactEvidence!.Protocol;
        _context.Entry(decision).Property(value => value.NormalizedEndpointIdentifier).CurrentValue =
            source.ExactEvidence.NormalizedIdentifier;
        _context.Entry(decision).Property(value => value.NormalizationRuleIdentity).CurrentValue =
            ProviderObservations(source)[0].NormalizationRuleIdentity!;
        _context.Entry(decision).Property(value => value.Outcome).CurrentValue = "ExactMatch";
        _context.Entry(decision).Property(value => value.OutcomeKind).CurrentValue = "ExactMatch";
        _context.Entry(decision).Property(value => value.ReasonCode).CurrentValue = "unique_provider";
        _context.Entry(decision).Property(value => value.ProviderIdentity).CurrentValue =
            ProviderIdentity(authority.Pair.SystemId, source.Target!);
        _context.Entry(decision).Property(value => value.ProviderDocumentVersionId).CurrentValue =
            source.Target!.DocumentVersionId;
        _context.Entry(decision).Property(value => value.ProviderRepositoryId).CurrentValue =
            source.Target.RepositoryId;
        _context.Entry(decision).Property(value => value.ProviderScanRunId).CurrentValue =
            source.Target.ScanRunId;
        _context.Entry(decision).Property(value => value.ProviderProfiledRevision).CurrentValue =
            source.Target.ProfiledRevision;
        _context.Entry(decision).Property(value => value.DocumentEdgeId).CurrentValue = write.Edge.Id;
        _context.Entry(decision).Property(value => value.UncertaintyId).CurrentValue = null;
        _context.Entry(decision).Property(value => value.ResolvedUncertaintyId).CurrentValue = null;
        string consumerEvidenceFingerprint = HashIdentity(
            "consumer-evidence",
            source.Owner.RepositoryId.ToString("D"),
            source.Owner.ScanRunId.ToString("D"),
            source.Owner.DocumentVersionId.ToString("D"),
            source.Owner.SourceAnchorId.ToString("D"),
            source.Owner.ProfiledRevision,
            source.EvidenceLocator);
        string[] contributionFingerprints = ProviderObservations(source)
            .Select(observation => ExactResolutionCanonicalAuthority
                .ProviderObservationFingerprint(
                    write.Evidence.EvidenceFingerprint, observation))
            .ToArray();
        string providerEvidenceSetFingerprint =
            ExactResolutionCanonicalAuthority.ProviderEvidenceSetFingerprint(
                contributionFingerprints);
        _context.Entry(decision).Property(value => value.ConsumerEvidenceFingerprint).CurrentValue =
            consumerEvidenceFingerprint;
        _context.Entry(decision).Property(value => value.ProviderEvidenceSetFingerprint).CurrentValue =
            providerEvidenceSetFingerprint;
        _context.Entry(decision).Property(value => value.DecisionFingerprint).CurrentValue =
            write.Evidence.EvidenceFingerprint;
        _context.Entry(decision).Property(value => value.EvidenceFingerprint).CurrentValue =
            write.Evidence.EvidenceFingerprint;
        _context.Entry(decision).Property(value => value.CreatedAt).CurrentValue =
            write.Edge.CreatedAt;
        return decision;
    }

    private ExactResolutionProviderEvidenceContribution CreateV2Contribution(
        ProviderEndpointCandidate observation,
        MaterializedExactWrite write,
        Guid decisionId,
        V2PairAuthority authority,
        int providerOrdinal,
        int evidenceOrdinal) =>
        CreateV2Contribution(
            observation,
            write.Evidence.EvidenceFingerprint,
            write.Edge.CreatedAt,
            decisionId,
            authority,
            providerOrdinal,
            evidenceOrdinal);

    private ExactResolutionProviderEvidenceContribution CreateV2Contribution(
        ProviderEndpointCandidate observation,
        string decisionFingerprint,
        DateTimeOffset createdAt,
        Guid decisionId,
        V2PairAuthority authority,
        int providerOrdinal,
        int evidenceOrdinal)
    {
        string fingerprint = ExactResolutionCanonicalAuthority.ProviderObservationFingerprint(
            decisionFingerprint, observation);
        var contribution = (ExactResolutionProviderEvidenceContribution)Activator.CreateInstance(
            typeof(ExactResolutionProviderEvidenceContribution),
            nonPublic: true)!;
        _context.Entry(contribution).Property(value => value.Id).CurrentValue =
            ExactResolutionCanonicalAuthority.ProviderContributionId(fingerprint);
        _context.Entry(contribution).Property(value => value.ResolutionDecisionId).CurrentValue =
            decisionId;
        _context.Entry(contribution).Property(value => value.ProviderIdentity).CurrentValue =
            ProviderIdentity(authority.Pair.SystemId, observation.Owner);
        _context.Entry(contribution).Property(value => value.ProviderDocumentVersionId).CurrentValue =
            observation.Owner.DocumentVersionId;
        _context.Entry(contribution).Property(value => value.RepositoryId).CurrentValue =
            observation.Owner.RepositoryId;
        _context.Entry(contribution).Property(value => value.ScanRunId).CurrentValue =
            observation.Owner.ScanRunId;
        _context.Entry(contribution).Property(value => value.ProfiledRevision).CurrentValue =
            observation.Owner.ProfiledRevision;
        _context.Entry(contribution).Property(value => value.CommunicationClassificationId).CurrentValue =
            observation.CommunicationClassificationId;
        _context.Entry(contribution).Property(value => value.SourceAnchorId).CurrentValue =
            observation.Owner.SourceAnchorId;
        _context.Entry(contribution).Property(value => value.ProviderRawIdentifier).CurrentValue =
            observation.RawIdentifier;
        _context.Entry(contribution).Property(value => value.NormalizedEndpointIdentifier).CurrentValue =
            observation.NormalizedIdentifier;
        _context.Entry(contribution).Property(value => value.EvidenceLocator).CurrentValue =
            observation.DeclarationEvidenceLocator;
        _context.Entry(contribution).Property(value => value.ClassificationFingerprint).CurrentValue =
            observation.ClassificationFingerprint!;
        _context.Entry(contribution).Property(value => value.CatalogRuleIdentity).CurrentValue =
            observation.CatalogRuleIdentity!;
        _context.Entry(contribution).Property(value => value.NormalizationRuleIdentity).CurrentValue =
            observation.NormalizationRuleIdentity!;
        _context.Entry(contribution).Property(value => value.EvidenceFingerprint).CurrentValue =
            fingerprint;
        _context.Entry(contribution).Property(value => value.ProviderOrdinal).CurrentValue =
            providerOrdinal;
        _context.Entry(contribution).Property(value => value.EvidenceOrdinal).CurrentValue =
            evidenceOrdinal;
        _context.Entry(contribution).Property(value => value.CreatedAt).CurrentValue =
            createdAt;
        return contribution;
    }

    private static string HashIdentity(string discriminator, params string[] values)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), leaveOpen: true))
        {
            writer.Write($"vulgata.exact-resolution-{discriminator}/2");
            foreach (string value in values)
            {
                writer.Write(value);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static bool IsLowerSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static MaterializedExactWrite MaterializeExact(
        ExactResolutionDecisionBatch batch,
        ExactResolutionDecision decision,
        DateTimeOffset createdAt,
        bool v2)
    {
        ExactResolutionOwner target = decision.Target!;
        ExactResolutionDecisionEvidence exact = decision.ExactEvidence!;
        string evidenceFingerprint = ExactResolutionCanonicalAuthority.EvidenceFingerprint(
            batch, decision, exact);
        Guid edgeId = v2
            ? DeterministicGuid(HashIdentity(
                "references-edge",
                decision.Owner.DocumentVersionId.ToString("D"),
                target.DocumentVersionId.ToString("D"),
                nameof(DocumentEdgeType.References)))
            : DeterministicGuid(evidenceFingerprint);
        var edge = new DocumentEdge(
            edgeId,
            decision.Owner.DocumentVersionId,
            decision.Owner.RepositoryId,
            decision.Owner.ScanRunId,
            target.DocumentVersionId,
            target.RepositoryId,
            target.ScanRunId,
            DocumentEdgeType.References,
            createdAt);
        var evidence = new ExactCrossRepositoryEdgeEvidence(
            edgeId,
            decision.Owner.SourceAnchorId,
            decision.Owner.DocumentVersionId,
            decision.Owner.RepositoryId,
            decision.Owner.ScanRunId,
            target.SourceAnchorId,
            target.DocumentVersionId,
            target.RepositoryId,
            target.ScanRunId,
            exact.ConsumerRawIdentifier,
            exact.ProviderRawIdentifier,
            exact.NormalizedIdentifier,
            exact.Protocol,
            _resolutionKind,
            exact.Confidence,
            _profileSchemaVersion,
            batch.ProfileId,
            batch.ProfileVersion,
            batch.ProfileFingerprint,
            decision.Identity,
            exact.ConsumerEvidenceLocator,
            exact.ProviderEvidenceLocator,
            evidenceFingerprint,
            createdAt);
        return new(edge, evidence);
    }

    private static Uncertainty MaterializeUncertainty(
        ExactResolutionDecisionBatch batch,
        ExactResolutionDecision decision,
        DateTimeOffset createdAt)
    {
        bool isUnsupported =
            decision.Kind == ExactResolutionDecisionKind.UnsupportedObservation;
        string subStatus = decision.ReasonCode == "multiple_providers"
            ? "ambiguous"
            : decision.Target is null ? "unknown_target" : "cross_repo";
        string fingerprint = UncertaintyFingerprint(batch, decision, subStatus);
        return new Uncertainty(
            DeterministicGuid(fingerprint),
            decision.Owner.DocumentVersionId,
            decision.Owner.RepositoryId,
            decision.Owner.ScanRunId,
            decision.Owner.SourceAnchorId,
            isUnsupported ? "unsupported_communication" : "cross_repository",
            subStatus,
            decision.ReasonCode,
            isUnsupported ? "file_storage" : null,
            null,
            null,
            decision.Target?.RepositoryId,
            _profileSchemaVersion,
            batch.ProfileId,
            batch.ProfileVersion,
            batch.ProfileFingerprint,
            decision.Identity,
            decision.EvidenceLocator,
            fingerprint,
            createdAt);
    }

    private static string UncertaintyFingerprint(
        ExactResolutionDecisionBatch batch,
        ExactResolutionDecision decision,
        string subStatus)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), leaveOpen: true))
        {
            writer.Write("vulgata.exact-cross-repository-uncertainty/1");
            writer.Write(_profileSchemaVersion);
            writer.Write(batch.ProfileId);
            writer.Write(batch.ProfileVersion);
            writer.Write(batch.ProfileFingerprint);
            writer.Write(decision.Identity);
            writer.Write(decision.Kind.ToString());
            WriteOwner(writer, decision.Owner);
            writer.Write(decision.Target is not null);
            if (decision.Target is not null)
            {
                WriteOwner(writer, decision.Target);
            }

            writer.Write("unresolved");
            writer.Write(subStatus);
            writer.Write(decision.ReasonCode);
            writer.Write(decision.Kind == ExactResolutionDecisionKind.UnsupportedObservation
                ? "file_storage"
                : string.Empty);
            writer.Write(decision.EvidenceLocator);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()))
            .ToLowerInvariant();
    }

    private static void WriteOwner(BinaryWriter writer, ExactResolutionOwner owner)
    {
        writer.Write(owner.RepositoryId.ToByteArray());
        writer.Write(owner.ScanRunId.ToByteArray());
        writer.Write(owner.DocumentVersionId.ToByteArray());
        writer.Write(owner.SourceAnchorId.ToByteArray());
        writer.Write(owner.ProfiledRevision);
    }

    private static Guid DeterministicGuid(string fingerprint)
    {
        byte[] bytes = Convert.FromHexString(fingerprint)[..16];
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    private static bool EdgeMatches(DocumentEdge actual, DocumentEdge expected) =>
        actual.Id == expected.Id &&
        actual.SourceDocumentVersionId == expected.SourceDocumentVersionId &&
        actual.SourceRepositoryId == expected.SourceRepositoryId &&
        actual.SourceScanRunId == expected.SourceScanRunId &&
        actual.TargetDocumentVersionId == expected.TargetDocumentVersionId &&
        actual.TargetRepositoryId == expected.TargetRepositoryId &&
        actual.TargetScanRunId == expected.TargetScanRunId &&
        actual.EdgeType == expected.EdgeType;

    private static bool EvidenceMatches(
        ExactCrossRepositoryEdgeEvidence actual,
        ExactCrossRepositoryEdgeEvidence expected) =>
        actual.EdgeId == expected.EdgeId &&
        actual.ConsumerSourceAnchorId == expected.ConsumerSourceAnchorId &&
        actual.ConsumerDocumentVersionId == expected.ConsumerDocumentVersionId &&
        actual.ConsumerRepositoryId == expected.ConsumerRepositoryId &&
        actual.ConsumerScanRunId == expected.ConsumerScanRunId &&
        actual.ProviderSourceAnchorId == expected.ProviderSourceAnchorId &&
        actual.ProviderDocumentVersionId == expected.ProviderDocumentVersionId &&
        actual.ProviderRepositoryId == expected.ProviderRepositoryId &&
        actual.ProviderScanRunId == expected.ProviderScanRunId &&
        StringComparer.Ordinal.Equals(actual.ConsumerRawIdentifier, expected.ConsumerRawIdentifier) &&
        StringComparer.Ordinal.Equals(actual.ProviderRawIdentifier, expected.ProviderRawIdentifier) &&
        StringComparer.Ordinal.Equals(
            actual.NormalizedEndpointIdentifier,
            expected.NormalizedEndpointIdentifier) &&
        actual.Protocol == expected.Protocol &&
        StringComparer.Ordinal.Equals(actual.ResolutionKind, expected.ResolutionKind) &&
        actual.Confidence == expected.Confidence &&
        StringComparer.Ordinal.Equals(actual.ProfileSchemaVersion, expected.ProfileSchemaVersion) &&
        StringComparer.Ordinal.Equals(actual.ProfileId, expected.ProfileId) &&
        StringComparer.Ordinal.Equals(actual.ProfileVersion, expected.ProfileVersion) &&
        StringComparer.Ordinal.Equals(actual.ProfileFingerprint, expected.ProfileFingerprint) &&
        StringComparer.Ordinal.Equals(actual.OccurrenceId, expected.OccurrenceId) &&
        StringComparer.Ordinal.Equals(
            actual.ConsumerEvidenceLocator,
            expected.ConsumerEvidenceLocator) &&
        StringComparer.Ordinal.Equals(
            actual.ProviderEvidenceLocator,
            expected.ProviderEvidenceLocator) &&
        StringComparer.Ordinal.Equals(actual.EvidenceFingerprint, expected.EvidenceFingerprint);

    private static ProviderEndpointCandidate[] ProviderObservations(
        ExactResolutionDecision source) =>
        source.ProviderObservations is { Count: > 0 }
            ? ExactResolutionProviderObservationCanonicalizer.Canonicalize(
                source.ProviderObservations)
            : [];

    private static ProviderObservationOrdinal[] ProviderObservationOrdinals(
        ExactResolutionDecision source,
        Guid systemId)
    {
        var providerOrdinals = new Dictionary<Guid, int>();
        var evidenceCounts = new Dictionary<Guid, int>();
        var result = new List<ProviderObservationOrdinal>();
        foreach (ProviderEndpointCandidate observation in ProviderObservations(source))
        {
            Guid providerIdentity = ProviderIdentity(systemId, observation.Owner);
            if (!providerOrdinals.TryGetValue(providerIdentity, out int providerOrdinal))
            {
                providerOrdinal = providerOrdinals.Count + 1;
                providerOrdinals.Add(providerIdentity, providerOrdinal);
            }

            int evidenceOrdinal = evidenceCounts.GetValueOrDefault(providerIdentity) + 1;
            evidenceCounts[providerIdentity] = evidenceOrdinal;
            result.Add(new(
                observation,
                providerOrdinal,
                evidenceOrdinal));
        }

        return result.ToArray();
    }

    private static Guid ProviderIdentity(Guid systemId, ExactResolutionOwner owner) =>
        DeterministicGuid(HashIdentity(
            "provider-identity",
            systemId.ToString("D"),
            owner.RepositoryId.ToString("D"),
            owner.ScanRunId.ToString("D"),
            owner.ProfiledRevision,
            owner.DocumentVersionId.ToString("D")));

    private readonly record struct ProviderObservationOrdinal(
        ProviderEndpointCandidate Observation,
        int ProviderOrdinal,
        int EvidenceOrdinal);

    private async Task<ExactResolutionDecision[]?> ResolveProviderAuthorityAsync(
        ExactResolutionDecisionBatch batch,
        IReadOnlyList<ExactResolutionDecision> decisions,
        CancellationToken cancellationToken)
    {
        ExactResolutionProfile profile =
            new Vulgata.Infrastructure.CrossRepository.ExactResolutionProfileCatalog().Load();
        if (!StringComparer.Ordinal.Equals(profile.ProfileId, batch.ProfileId) ||
            !StringComparer.Ordinal.Equals(profile.ProfileVersion, batch.ProfileVersion) ||
            !StringComparer.Ordinal.Equals(profile.ProfileFingerprint, batch.ProfileFingerprint))
        {
            return null;
        }

        var result = new List<ExactResolutionDecision>(decisions.Count);
        foreach (ExactResolutionDecision decision in decisions)
        {
            ProviderEndpointCandidate[] observations = ProviderObservations(decision);
            ResolutionOccurrence? occurrence = profile.Occurrences.SingleOrDefault(value =>
                StringComparer.Ordinal.Equals(value.OccurrenceId, decision.Identity));
            if (observations.Length == 0)
            {
                if (decision.Kind == ExactResolutionDecisionKind.ExactMatch)
                {
                    return null;
                }

                result.Add(decision);
                continue;
            }
            if (occurrence is null)
            {
                return null;
            }

            var resolved = new List<ProviderEndpointCandidate>(observations.Length);
            foreach (ProviderEndpointCandidate observation in observations)
            {
                SourceAnchor? anchor = await _context.SourceAnchors.AsNoTracking()
                    .SingleOrDefaultAsync(value =>
                        value.Id == observation.Owner.SourceAnchorId &&
                        value.DocumentVersionId == observation.Owner.DocumentVersionId &&
                        value.RepositoryId == observation.Owner.RepositoryId &&
                        value.ScanRunId == observation.Owner.ScanRunId &&
                        value.ProfiledRevision == observation.Owner.ProfiledRevision,
                        cancellationToken).ConfigureAwait(false);
                if (anchor is null)
                {
                    return null;
                }

                CommunicationKindClassification[] classifications = await _context
                    .CommunicationKindClassifications.AsNoTracking()
                    .Where(value =>
                        value.PreScanResultId == observation.Owner.ScanRunId &&
                        value.Level == ClassificationLevel.Confirmed &&
                        value.Kind == CommunicationKind.Rpc)
                    .ToArrayAsync(cancellationToken).ConfigureAwait(false);
                CommunicationKindClassification[] matching = classifications.Where(value =>
                    observation.CommunicationClassificationId == Guid.Empty ||
                        value.Id == observation.CommunicationClassificationId)
                    .ToArray();
                if (matching.Length != 1 ||
                    matching[0].CatalogRuleIdentity is not { Length: > 0 } catalogRuleIdentity ||
                    !IsLowerSha256(matching[0].Fingerprint) ||
                    observation.ClassificationFingerprint is not null &&
                        !StringComparer.Ordinal.Equals(
                            observation.ClassificationFingerprint, matching[0].Fingerprint) ||
                    observation.CatalogRuleIdentity is not null &&
                        !StringComparer.Ordinal.Equals(
                            observation.CatalogRuleIdentity, catalogRuleIdentity) ||
                    observation.NormalizationRuleIdentity is not null &&
                        !StringComparer.Ordinal.Equals(
                            observation.NormalizationRuleIdentity, occurrence.Normalization.Id))
                {
                    return null;
                }

                resolved.Add(new ProviderEndpointCandidate(
                    observation.OccurrenceId,
                    observation.Owner,
                    observation.RawIdentifier,
                    observation.NormalizedIdentifier,
                    observation.DeclarationEvidenceLocator,
                    matching[0].Id,
                    matching[0].Fingerprint,
                    catalogRuleIdentity,
                    occurrence.Normalization.Id));
            }

            result.Add(decision with { ProviderObservations = resolved });
        }

        return [.. result];
    }

    private static bool V2DecisionMatches(
        CrossRepositoryResolutionDecision actual,
        ExactResolutionDecision source,
        MaterializedExactWrite write)
    {
        ProviderEndpointCandidate[] observations = ProviderObservations(source);
        string[] fingerprints = observations.Select(value =>
            ExactResolutionCanonicalAuthority.ProviderObservationFingerprint(
                write.Evidence.EvidenceFingerprint, value)).ToArray();
        string providerSet = ExactResolutionCanonicalAuthority.ProviderEvidenceSetFingerprint(
            fingerprints);
        ProviderEndpointCandidate provider = observations[0];
        string consumerFingerprint = HashIdentity(
            "consumer-evidence",
            source.Owner.RepositoryId.ToString("D"),
            source.Owner.ScanRunId.ToString("D"),
            source.Owner.DocumentVersionId.ToString("D"),
            source.Owner.SourceAnchorId.ToString("D"),
            source.Owner.ProfiledRevision,
            source.EvidenceLocator);
        return actual.ResolutionSchemaIdentity == "exact-cross-repository-resolution/2.0" &&
        actual.ConsumerCandidateId == DeterministicGuid(HashIdentity(
            "candidate", source.Identity, source.Owner.SourceAnchorId.ToString("D"))) &&
        actual.ConsumerDocumentVersionId == source.Owner.DocumentVersionId &&
        actual.ConsumerRepositoryId == source.Owner.RepositoryId &&
        actual.ConsumerScanRunId == source.Owner.ScanRunId &&
        StringComparer.Ordinal.Equals(
            actual.ConsumerProfiledRevision, source.Owner.ProfiledRevision) &&
        actual.Protocol == source.ExactEvidence!.Protocol &&
        StringComparer.Ordinal.Equals(actual.ConsumerEntryIdentity, source.Identity) &&
        StringComparer.Ordinal.Equals(
            actual.ConsumerEntryStableKey,
            source.ConsumerEntryStableKey) &&
        StringComparer.Ordinal.Equals(
            actual.NormalizedEndpointIdentifier,
            source.ExactEvidence.NormalizedIdentifier) &&
        StringComparer.Ordinal.Equals(
            actual.NormalizationRuleIdentity, provider.NormalizationRuleIdentity) &&
        StringComparer.Ordinal.Equals(actual.Outcome, "ExactMatch") &&
        StringComparer.Ordinal.Equals(actual.OutcomeKind, "ExactMatch") &&
        StringComparer.Ordinal.Equals(actual.ReasonCode, "unique_provider") &&
        actual.ProviderIdentity == DeterministicGuid(HashIdentity(
            "provider-identity",
            actual.SystemId.ToString("D"),
            source.Target!.RepositoryId.ToString("D"),
            source.Target.ScanRunId.ToString("D"),
            source.Target.ProfiledRevision,
            source.Target.DocumentVersionId.ToString("D"))) &&
        actual.ProviderDocumentVersionId == source.Target.DocumentVersionId &&
        actual.ProviderRepositoryId == source.Target.RepositoryId &&
        actual.ProviderScanRunId == source.Target.ScanRunId &&
        StringComparer.Ordinal.Equals(
            actual.ProviderProfiledRevision, source.Target.ProfiledRevision) &&
        actual.DocumentEdgeId == write.Edge.Id &&
        actual.UncertaintyId is null &&
        actual.ResolvedUncertaintyId is null &&
        StringComparer.Ordinal.Equals(
            actual.ConsumerEvidenceFingerprint, consumerFingerprint) &&
        StringComparer.Ordinal.Equals(actual.ProviderEvidenceSetFingerprint, providerSet) &&
        StringComparer.Ordinal.Equals(
            actual.DecisionFingerprint, write.Evidence.EvidenceFingerprint) &&
        StringComparer.Ordinal.Equals(
            actual.EvidenceFingerprint,
            write.Evidence.EvidenceFingerprint) &&
        actual.CreatedAt == write.Edge.CreatedAt;
    }

    private static bool V2ContributionMatches(
        ExactResolutionProviderEvidenceContribution actual,
        Guid decisionId,
        ProviderEndpointCandidate observation,
        string decisionFingerprint,
        Guid systemId,
        DateTimeOffset createdAt,
        int providerOrdinal,
        int evidenceOrdinal)
    {
        string fingerprint = ExactResolutionCanonicalAuthority.ProviderObservationFingerprint(
            decisionFingerprint, observation);
        return actual.ResolutionDecisionId == decisionId &&
            actual.Id == ExactResolutionCanonicalAuthority.ProviderContributionId(fingerprint) &&
            actual.ProviderIdentity == ProviderIdentity(systemId, observation.Owner) &&
            actual.ProviderDocumentVersionId == observation.Owner.DocumentVersionId &&
            actual.SourceAnchorId == observation.Owner.SourceAnchorId &&
            StringComparer.Ordinal.Equals(
                actual.ProviderRawIdentifier, observation.RawIdentifier) &&
            StringComparer.Ordinal.Equals(
                actual.NormalizedEndpointIdentifier, observation.NormalizedIdentifier) &&
            actual.RepositoryId == observation.Owner.RepositoryId &&
            actual.ScanRunId == observation.Owner.ScanRunId &&
            StringComparer.Ordinal.Equals(
                actual.ProfiledRevision, observation.Owner.ProfiledRevision) &&
            actual.CommunicationClassificationId == observation.CommunicationClassificationId &&
            StringComparer.Ordinal.Equals(
                actual.EvidenceLocator, observation.DeclarationEvidenceLocator) &&
            StringComparer.Ordinal.Equals(
                actual.ClassificationFingerprint, observation.ClassificationFingerprint) &&
            StringComparer.Ordinal.Equals(
                actual.CatalogRuleIdentity, observation.CatalogRuleIdentity) &&
            StringComparer.Ordinal.Equals(
                actual.NormalizationRuleIdentity, observation.NormalizationRuleIdentity) &&
            StringComparer.Ordinal.Equals(actual.EvidenceFingerprint, fingerprint) &&
            actual.ProviderOrdinal == providerOrdinal &&
            actual.EvidenceOrdinal == evidenceOrdinal &&
            actual.CreatedAt == createdAt;
    }

    private static bool NonExactDecisionMatches(
        CrossRepositoryResolutionDecision actual,
        ExactResolutionDecision source,
        Uncertainty uncertainty,
        DateTimeOffset persistedCreatedAt,
        V2PairAuthority authority,
        IReadOnlyList<ProviderEndpointCandidate> observations,
        IReadOnlyList<ExactResolutionProviderEvidenceContribution> contributions)
    {
        string[] fingerprints = observations.Select(value =>
            ExactResolutionCanonicalAuthority.ProviderObservationFingerprint(
                uncertainty.EvidenceFingerprint, value)).ToArray();
        string providerSetFingerprint = fingerprints.Length == 0
            ? HashIdentity("provider-evidence-set", "none")
            : ExactResolutionCanonicalAuthority.ProviderEvidenceSetFingerprint(fingerprints);
        string consumerFingerprint = HashIdentity(
            "consumer-evidence",
            source.Owner.RepositoryId.ToString("D"),
            source.Owner.ScanRunId.ToString("D"),
            source.Owner.DocumentVersionId.ToString("D"),
            source.Owner.SourceAnchorId.ToString("D"),
            source.Owner.ProfiledRevision,
            source.EvidenceLocator);
        string normalizedEndpoint = observations.FirstOrDefault()?.NormalizedIdentifier ??
            source.Identity;
        string normalizationRule = observations.FirstOrDefault()?.NormalizationRuleIdentity ??
            source.Identity;
        ProviderObservationOrdinal[] observationOrdinals =
            ProviderObservationOrdinals(source, authority.Pair.SystemId);
        bool contributionsMatch = contributions.Count == observationOrdinals.Length &&
            contributions.Select((actualContribution, index) => V2ContributionMatches(
                actualContribution,
                actual.Id,
                observationOrdinals[index].Observation,
                uncertainty.EvidenceFingerprint,
                authority.Pair.SystemId,
                persistedCreatedAt,
                observationOrdinals[index].ProviderOrdinal,
                observationOrdinals[index].EvidenceOrdinal)).All(value => value);

        return actual.Id == ExactResolutionCanonicalAuthority.DecisionId(
                uncertainty.EvidenceFingerprint) &&
            actual.ResolutionSchemaIdentity == "exact-cross-repository-resolution/2.0" &&
            actual.Pass1PairExecutionId == authority.Pair.Id &&
            actual.CoordinationId == authority.Pair.CoordinationId &&
            actual.SystemId == authority.Pair.SystemId &&
            actual.ConsumerCandidateId == DeterministicGuid(HashIdentity(
                "candidate", source.Identity, source.Owner.SourceAnchorId.ToString("D"))) &&
            StringComparer.Ordinal.Equals(actual.ConsumerEntryIdentity, source.Identity) &&
            StringComparer.Ordinal.Equals(
                actual.ConsumerEntryStableKey, source.ConsumerEntryStableKey) &&
            actual.ConsumerDocumentVersionId == source.Owner.DocumentVersionId &&
            actual.ConsumerRepositoryId == source.Owner.RepositoryId &&
            actual.ConsumerScanRunId == source.Owner.ScanRunId &&
            StringComparer.Ordinal.Equals(
                actual.ConsumerProfiledRevision, source.Owner.ProfiledRevision) &&
            actual.Protocol == ExactResolutionProtocol.CustomRpc &&
            StringComparer.Ordinal.Equals(
                actual.NormalizedEndpointIdentifier, normalizedEndpoint) &&
            StringComparer.Ordinal.Equals(actual.NormalizationRuleIdentity, normalizationRule) &&
            StringComparer.Ordinal.Equals(actual.Outcome, "Uncertainty") &&
            StringComparer.Ordinal.Equals(actual.OutcomeKind, source.Kind.ToString()) &&
            StringComparer.Ordinal.Equals(actual.ReasonCode, source.ReasonCode) &&
            actual.ProviderIdentity is null &&
            actual.ProviderDocumentVersionId is null &&
            actual.ProviderRepositoryId is null &&
            actual.ProviderScanRunId is null &&
            actual.ProviderProfiledRevision is null &&
            actual.DocumentEdgeId is null &&
            actual.UncertaintyId == uncertainty.Id &&
            actual.ResolvedUncertaintyId is null &&
            StringComparer.Ordinal.Equals(
                actual.ConsumerEvidenceFingerprint, consumerFingerprint) &&
            StringComparer.Ordinal.Equals(
                actual.ProviderEvidenceSetFingerprint, providerSetFingerprint) &&
            StringComparer.Ordinal.Equals(
                actual.DecisionFingerprint, uncertainty.EvidenceFingerprint) &&
            StringComparer.Ordinal.Equals(
                actual.EvidenceFingerprint, uncertainty.EvidenceFingerprint) &&
            actual.CreatedAt == persistedCreatedAt &&
            contributionsMatch;
    }

    private static bool UncertaintyMatches(Uncertainty actual, Uncertainty expected) =>
        actual.Id == expected.Id &&
        actual.DocumentVersionId == expected.DocumentVersionId &&
        actual.RepositoryId == expected.RepositoryId &&
        actual.ScanRunId == expected.ScanRunId &&
        actual.SourceAnchorId == expected.SourceAnchorId &&
        StringComparer.Ordinal.Equals(actual.Type, expected.Type) &&
        StringComparer.Ordinal.Equals(actual.Status, expected.Status) &&
        StringComparer.Ordinal.Equals(actual.SubStatus, expected.SubStatus) &&
        StringComparer.Ordinal.Equals(actual.ReasonCode, expected.ReasonCode) &&
        StringComparer.Ordinal.Equals(actual.ObservationKind, expected.ObservationKind) &&
        StringComparer.Ordinal.Equals(actual.EndpointIdentifier, expected.EndpointIdentifier) &&
        StringComparer.Ordinal.Equals(
            actual.EndpointIdentifierNormalized,
            expected.EndpointIdentifierNormalized) &&
        actual.TargetRepositoryId == expected.TargetRepositoryId &&
        StringComparer.Ordinal.Equals(
            actual.ProfileSchemaVersion,
            expected.ProfileSchemaVersion) &&
        StringComparer.Ordinal.Equals(actual.ProfileId, expected.ProfileId) &&
        StringComparer.Ordinal.Equals(actual.ProfileVersion, expected.ProfileVersion) &&
        StringComparer.Ordinal.Equals(actual.ProfileFingerprint, expected.ProfileFingerprint) &&
        StringComparer.Ordinal.Equals(actual.OccurrenceId, expected.OccurrenceId) &&
        StringComparer.Ordinal.Equals(actual.EvidenceLocator, expected.EvidenceLocator) &&
        StringComparer.Ordinal.Equals(actual.EvidenceFingerprint, expected.EvidenceFingerprint) &&
        actual.ResolvedAt == expected.ResolvedAt &&
        actual.ResolvedByDocumentVersionId == expected.ResolvedByDocumentVersionId;

    private static bool IsNonExactReason(string reasonCode) => reasonCode is
        "variable_target" or
        "disallowed_invocation" or
        "malformed_provider" or
        "provider_out_of_root" or
        "unmatched" or
        "multiple_providers" or
        "normalization_mismatch";

    private static bool ContainsInternalIdentity(
        ExactResolutionDecisionBatch batch,
        ExactResolutionDecision decision,
        ExactResolutionDecisionEvidence evidence)
    {
        Guid[] ids =
        [
            decision.Owner.RepositoryId,
            decision.Owner.ScanRunId,
            decision.Owner.DocumentVersionId,
            decision.Owner.SourceAnchorId,
            decision.Target!.RepositoryId,
            decision.Target.ScanRunId,
            decision.Target.DocumentVersionId,
            decision.Target.SourceAnchorId,
        ];
        string[] texts =
        [
            evidence.ConsumerRawIdentifier,
            evidence.ProviderRawIdentifier,
            evidence.NormalizedIdentifier,
            evidence.ConsumerEvidenceLocator,
            evidence.ProviderEvidenceLocator,
            batch.ProfileId,
            batch.ProfileVersion,
            decision.Identity,
        ];
        return ids.Any(id => texts.Any(text =>
            text.Contains(id.ToString("D"), StringComparison.OrdinalIgnoreCase) ||
            text.Contains(id.ToString("N"), StringComparison.OrdinalIgnoreCase)));
    }

    private static bool ContainsInternalIdentity(
        ExactResolutionDecisionBatch batch,
        ExactResolutionDecision decision)
    {
        Guid[] ids =
        [
            .. Owners(decision).SelectMany(owner => new[]
            {
                owner.RepositoryId,
                owner.ScanRunId,
                owner.DocumentVersionId,
                owner.SourceAnchorId,
            }),
        ];
        string[] texts =
        [
            decision.EvidenceLocator,
            batch.ProfileId,
            batch.ProfileVersion,
            decision.Identity,
            decision.ReasonCode,
        ];
        return ids.Any(id => texts.Any(text =>
            text.Contains(id.ToString("D"), StringComparison.OrdinalIgnoreCase) ||
            text.Contains(id.ToString("N"), StringComparison.OrdinalIgnoreCase)));
    }

    private static ExactResolutionBatchOutcome Refused() =>
        new(ExactResolutionOutcomeKind.Refused, [], "refused");

    private ExactResolutionBatchOutcome ClearAndRefused()
    {
        _context.ChangeTracker.Clear();
        return Refused();
    }

    private ExactResolutionBatchOutcome ClearAndConflict()
    {
        _context.ChangeTracker.Clear();
        return new(ExactResolutionOutcomeKind.Conflict, [], "conflict");
    }

    private sealed record MaterializedExactWrite(
        DocumentEdge Edge,
        ExactCrossRepositoryEdgeEvidence Evidence);

    private sealed record V2PairAuthority(
        Pass1PairExecution Pair,
        TwoRepositoryScanCoordination Coordination,
        PreparedScanManifest ManifestOne,
        PreparedScanManifest ManifestTwo);

    private sealed record V2AuthorityResolution(bool IsConflict, V2PairAuthority? Authority);
}
