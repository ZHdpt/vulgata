using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

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

                    await AcquireOwnerLocksAsync(materializedDecisions, isSqlite, cancellationToken);
                    if (!await OwnersAreCurrentAsync(
                            materializedDecisions,
                            scope,
                            cancellationToken))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return ClearAndConflict();
                    }

                    DateTimeOffset createdAt = DateTimeOffset.UtcNow;
                    var items = new List<ExactResolutionBatchItemOutcome>(
                        materializedDecisions.Length);
                    foreach (ExactResolutionDecision decision in materializedDecisions)
                    {
                        ExactResolutionOutcomeKind itemKind;
                        if (decision.Kind == ExactResolutionDecisionKind.ExactMatch)
                        {
                            MaterializedExactWrite write = MaterializeExact(
                                decisions,
                                decision,
                                createdAt);
                            itemKind = await PrepareExactWriteAsync(write, cancellationToken);
                            if (itemKind != ExactResolutionOutcomeKind.Conflict)
                            {
                                ExactResolutionOutcomeKind v2Kind =
                                    await PrepareV2ExactWriteAsync(
                                        decisions,
                                        decision,
                                        write,
                                        cancellationToken);
                                if (v2Kind == ExactResolutionOutcomeKind.Conflict)
                                {
                                    itemKind = v2Kind;
                                }
                                else if (v2Kind == ExactResolutionOutcomeKind.Applied)
                                {
                                    itemKind = v2Kind;
                                }
                            }
                        }
                        else
                        {
                            Uncertainty uncertainty = MaterializeUncertainty(
                                decisions,
                                decision,
                                createdAt);
                            itemKind = await PrepareUncertaintyWriteAsync(
                                uncertainty,
                                cancellationToken);
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
        CancellationToken cancellationToken)
    {
        CrossRepositoryResolutionDecision? existing =
            await _context.CrossRepositoryResolutionDecisions.AsNoTracking()
                .SingleOrDefaultAsync(
                    decision => decision.EvidenceFingerprint ==
                        write.Evidence.EvidenceFingerprint,
                    cancellationToken);
        if (existing is not null)
        {
            ExactResolutionProviderEvidenceContribution[] contributions =
            await _context.ExactResolutionProviderEvidenceContributions.AsNoTracking()
                .Where(contribution => contribution.ResolutionDecisionId == existing.Id)
                .ToArrayAsync(cancellationToken);
            return V2DecisionMatches(existing, source, write) &&
                contributions.Length == 1 &&
                V2ContributionMatches(contributions[0], existing.Id, source, write)
                    ? ExactResolutionOutcomeKind.AlreadyApplied
                    : ExactResolutionOutcomeKind.Conflict;
        }

        Guid decisionId = DeterministicGuid(write.Evidence.EvidenceFingerprint);
        if (await _context.CrossRepositoryResolutionDecisions.AsNoTracking()
            .AnyAsync(decision => decision.Id == decisionId, cancellationToken))
        {
            return ExactResolutionOutcomeKind.Conflict;
        }

        CrossRepositoryResolutionDecision decision = CreateV2Decision(
            batch,
            source,
            write,
            decisionId);
        ExactResolutionProviderEvidenceContribution contribution =
            CreateV2Contribution(source, write, decisionId);
        _context.CrossRepositoryResolutionDecisions.Add(decision);
        _context.ExactResolutionProviderEvidenceContributions.Add(contribution);
        return ExactResolutionOutcomeKind.Applied;
    }

    private CrossRepositoryResolutionDecision CreateV2Decision(
        ExactResolutionDecisionBatch batch,
        ExactResolutionDecision source,
        MaterializedExactWrite write,
        Guid decisionId)
    {
        var decision = (CrossRepositoryResolutionDecision)Activator.CreateInstance(
            typeof(CrossRepositoryResolutionDecision),
            nonPublic: true)!;
        _context.Entry(decision).Property(value => value.Id).CurrentValue = decisionId;
        _context.Entry(decision).Property(value => value.Pass1PairExecutionId).CurrentValue =
            DeterministicGuid(HashIdentity("pair", batch.ProfileFingerprint, source.Identity));
        _context.Entry(decision).Property(value => value.CoordinationId).CurrentValue =
            DeterministicGuid(HashIdentity("coordination", batch.ProfileFingerprint));
        _context.Entry(decision).Property(value => value.SystemId).CurrentValue =
            DeterministicGuid(HashIdentity("system", source.Owner.RepositoryId.ToString("D")));
        _context.Entry(decision).Property(value => value.ConsumerCandidateId).CurrentValue =
            DeterministicGuid(HashIdentity("candidate", source.Identity,
                source.Owner.SourceAnchorId.ToString("D")));
        _context.Entry(decision).Property(value => value.ConsumerEntryIdentity).CurrentValue =
            source.Identity;
        _context.Entry(decision).Property(value => value.ConsumerEntryStableKey).CurrentValue =
            source.Identity;
        _context.Entry(decision).Property(value => value.ConsumerDocumentVersionId).CurrentValue =
            source.Owner.DocumentVersionId;
        _context.Entry(decision).Property(value => value.Protocol).CurrentValue =
            source.ExactEvidence!.Protocol;
        _context.Entry(decision).Property(value => value.NormalizedEndpointIdentifier).CurrentValue =
            source.ExactEvidence.NormalizedIdentifier;
        _context.Entry(decision).Property(value => value.Outcome).CurrentValue = "ExactMatch";
        _context.Entry(decision).Property(value => value.ProviderDocumentVersionId).CurrentValue =
            source.Target!.DocumentVersionId;
        _context.Entry(decision).Property(value => value.DocumentEdgeId).CurrentValue = write.Edge.Id;
        _context.Entry(decision).Property(value => value.UncertaintyId).CurrentValue = null;
        _context.Entry(decision).Property(value => value.EvidenceFingerprint).CurrentValue =
            write.Evidence.EvidenceFingerprint;
        _context.Entry(decision).Property(value => value.CreatedAt).CurrentValue =
            write.Edge.CreatedAt;
        return decision;
    }

    private ExactResolutionProviderEvidenceContribution CreateV2Contribution(
        ExactResolutionDecision source,
        MaterializedExactWrite write,
        Guid decisionId)
    {
        string fingerprint = HashIdentity(
            "provider-contribution",
            write.Evidence.EvidenceFingerprint,
            source.Target!.DocumentVersionId.ToString("D"),
            source.Target.SourceAnchorId.ToString("D"));
        var contribution = (ExactResolutionProviderEvidenceContribution)Activator.CreateInstance(
            typeof(ExactResolutionProviderEvidenceContribution),
            nonPublic: true)!;
        _context.Entry(contribution).Property(value => value.Id).CurrentValue =
            DeterministicGuid(fingerprint);
        _context.Entry(contribution).Property(value => value.ResolutionDecisionId).CurrentValue =
            decisionId;
        _context.Entry(contribution).Property(value => value.ProviderDocumentVersionId).CurrentValue =
            source.Target.DocumentVersionId;
        _context.Entry(contribution).Property(value => value.SourceAnchorId).CurrentValue =
            source.Target.SourceAnchorId;
        _context.Entry(contribution).Property(value => value.EvidenceFingerprint).CurrentValue =
            fingerprint;
        _context.Entry(contribution).Property(value => value.ProviderOrdinal).CurrentValue = 1;
        _context.Entry(contribution).Property(value => value.EvidenceOrdinal).CurrentValue = 1;
        _context.Entry(contribution).Property(value => value.CreatedAt).CurrentValue =
            write.Edge.CreatedAt;
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

    private static MaterializedExactWrite MaterializeExact(
        ExactResolutionDecisionBatch batch,
        ExactResolutionDecision decision,
        DateTimeOffset createdAt)
    {
        ExactResolutionOwner target = decision.Target!;
        ExactResolutionDecisionEvidence exact = decision.ExactEvidence!;
        string evidenceFingerprint = Fingerprint(batch, decision, exact);
        Guid edgeId = DeterministicGuid(evidenceFingerprint);
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

    private static string Fingerprint(
        ExactResolutionDecisionBatch batch,
        ExactResolutionDecision decision,
        ExactResolutionDecisionEvidence evidence)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), leaveOpen: true))
        {
            writer.Write("vulgata.exact-cross-repository-edge-evidence/1");
            writer.Write(_profileSchemaVersion);
            writer.Write(batch.ProfileId);
            writer.Write(batch.ProfileVersion);
            writer.Write(batch.ProfileFingerprint);
            writer.Write(decision.Identity);
            WriteOwner(writer, decision.Owner);
            WriteOwner(writer, decision.Target!);
            writer.Write(evidence.ConsumerRawIdentifier);
            writer.Write(evidence.ProviderRawIdentifier);
            writer.Write(evidence.NormalizedIdentifier);
            writer.Write(evidence.Protocol.ToString());
            writer.Write(_resolutionKind);
            writer.Write(evidence.Confidence);
            writer.Write(evidence.ConsumerEvidenceLocator);
            writer.Write(evidence.ProviderEvidenceLocator);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()))
            .ToLowerInvariant();
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

    private static bool V2DecisionMatches(
        CrossRepositoryResolutionDecision actual,
        ExactResolutionDecision source,
        MaterializedExactWrite write) =>
        actual.ConsumerDocumentVersionId == source.Owner.DocumentVersionId &&
        actual.Protocol == source.ExactEvidence!.Protocol &&
        StringComparer.Ordinal.Equals(actual.ConsumerEntryIdentity, source.Identity) &&
        StringComparer.Ordinal.Equals(actual.ConsumerEntryStableKey, source.Identity) &&
        StringComparer.Ordinal.Equals(
            actual.NormalizedEndpointIdentifier,
            source.ExactEvidence.NormalizedIdentifier) &&
        StringComparer.Ordinal.Equals(actual.Outcome, "ExactMatch") &&
        actual.ProviderDocumentVersionId == source.Target!.DocumentVersionId &&
        actual.DocumentEdgeId == write.Edge.Id &&
        actual.UncertaintyId is null &&
        StringComparer.Ordinal.Equals(
            actual.EvidenceFingerprint,
            write.Evidence.EvidenceFingerprint);

    private static bool V2ContributionMatches(
        ExactResolutionProviderEvidenceContribution actual,
        Guid decisionId,
        ExactResolutionDecision source,
        MaterializedExactWrite write)
    {
        string fingerprint = HashIdentity(
            "provider-contribution",
            write.Evidence.EvidenceFingerprint,
            source.Target!.DocumentVersionId.ToString("D"),
            source.Target.SourceAnchorId.ToString("D"));
        return actual.ResolutionDecisionId == decisionId &&
            actual.ProviderDocumentVersionId == source.Target.DocumentVersionId &&
            actual.SourceAnchorId == source.Target.SourceAnchorId &&
            StringComparer.Ordinal.Equals(actual.EvidenceFingerprint, fingerprint) &&
            actual.ProviderOrdinal == 1 &&
            actual.EvidenceOrdinal == 1;
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
}
