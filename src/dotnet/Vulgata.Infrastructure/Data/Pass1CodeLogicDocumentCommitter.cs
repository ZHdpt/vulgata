using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

/// <summary>
/// Defensive Story 12.3 materialization boundary. The authoritative JSON is
/// validated and projected again after durable bindings are loaded; no
/// provider-authored derivative representation reaches persistence.
/// </summary>
public sealed class Pass1CodeLogicDocumentCommitter(VulgataDbContext context)
    : IPass1CodeLogicDocumentCommitter
{
    internal static IReadOnlyList<Pass1ReferenceObservation> ReadReferenceObservations(
        DocumentVersion source,
        Guid sourceFileCodeUnitId,
        int pairSelectionOrdinal)
    {
        ArgumentNullException.ThrowIfNull(source);
        using JsonDocument document = JsonDocument.Parse(source.ContentJson);
        JsonElement root = document.RootElement;
        if (source.SchemaIdentity != "code-logic/file-v2" ||
            root.GetProperty("schemaIdentity").GetString() != "code-logic/file-v2")
        {
            throw new InvalidDataException("Reconciliation requires authoritative code-logic/file-v2 content.");
        }

        var observations = new List<Pass1ReferenceObservation>();
        foreach (JsonElement known in root.GetProperty("knownReferences").EnumerateArray())
        {
            observations.Add(CreateObservation(
                source,
                sourceFileCodeUnitId,
                pairSelectionOrdinal,
                known.GetProperty("sourceClaimLocator").GetString()!,
                normalizedTargetSymbol: null,
                known.GetProperty("targetRepositoryRelativePath").GetString(),
                known.GetProperty("targetSourceHash").GetString(),
                known.GetProperty("targetDocumentVersionId").GetGuid(),
                canResolve: true,
                rejectionCode: null));
        }

        foreach (JsonElement deferred in root.GetProperty("deferredObservations").EnumerateArray())
        {
            string outcome = deferred.GetProperty("outcome").GetString()!;
            string? path = OptionalString(deferred, "targetRepositoryRelativePath");
            string? hash = OptionalString(deferred, "targetSourceHash");
            string? symbol = OptionalString(deferred, "targetSymbol");
            bool canResolve = outcome == "Insufficient" && path is not null && hash is not null;
            observations.Add(CreateObservation(
                source,
                sourceFileCodeUnitId,
                pairSelectionOrdinal,
                deferred.GetProperty("sourceClaimLocator").GetString()!,
                symbol,
                path,
                hash,
                directTargetDocumentVersionId: null,
                canResolve,
                canResolve ? null : DeferredReason(outcome)));
        }

        return observations
            .OrderBy(value => value.SourceRepositoryId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(value => value.PairSelectionOrdinal)
            .ThenBy(value => value.SourceDocumentVersionId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(value => value.ObservationFingerprint, StringComparer.Ordinal)
            .ThenBy(value => value.TargetCanonicalPath, StringComparer.Ordinal)
            .ThenBy(value => value.NormalizedTargetSymbol, StringComparer.Ordinal)
            .ThenBy(value => value.DirectTargetDocumentVersionId?.ToString("D"), StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<Pass1DocumentCommitResult> CommitAsync(
        Pass1DocumentCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Packet);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ExpectedLeaseToken == Guid.Empty ||
            request.ExpectedJobStateVersion < 0 ||
            request.CommittedAt.Offset != TimeSpan.Zero ||
            !PacketSourceMatches(request.Packet))
        {
            return Rejected("invalid-commit-authorization");
        }

        byte[] canonical;
        CodeLogicFileV2Projection projection;
        try
        {
            canonical = CodeLogicFileV2Contract.ValidateAndCanonicalize(
                request.Packet,
                request.CanonicalCodeLogicJson);
            if (!canonical.AsSpan().SequenceEqual(request.CanonicalCodeLogicJson.Span))
            {
                return Rejected("noncanonical-authoritative-json");
            }

            PreparedPass1CanonicalArtifact logicalIdentity =
                PreparedPass1CanonicalCodec.CreateLogicalDocument(new(
                    request.Packet.RepositoryId,
                    request.Packet.RepositoryRelativePath,
                    checked((ulong)request.Packet.SegmentOrdinal)));
            PreparedPass1CanonicalArtifact versionIdentity =
                PreparedPass1CanonicalCodec.CreateDocumentVersion(new(
                    request.Packet.JobId,
                    Convert.FromHexString(request.Packet.FileCodeUnitIdentityFingerprint),
                    request.Packet.DocumentSchemaIdentity));
            if (logicalIdentity.Id != request.LogicalDocumentId ||
                versionIdentity.Id != request.DocumentVersionId)
            {
                return Rejected("noncanonical-document-identity");
            }

            projection = CodeLogicFileV2Projector.Project(
                Encoding.UTF8.GetString(canonical),
                request.Packet.PairExecutionId,
                request.Packet.JobId,
                request.LogicalDocumentId,
                request.DocumentVersionId);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidDataException or JsonException or OverflowException)
        {
            return Rejected("invalid-authoritative-result");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.ChangeTracker.Clear();

        Pass1PairExecution? lockedPair = await context.Pass1PairExecutions
            .FromSqlInterpolated($$"""
                SELECT * FROM vulgata."Pass1PairExecutions"
                WHERE "Id" = {{request.Packet.PairExecutionId}}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (lockedPair is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Rejected("unknown-pair-execution");
        }

        ScanJobExecution? job = await context.ScanJobExecutions
            .FromSqlInterpolated($$"""
                SELECT * FROM vulgata."ScanJobExecutions"
                WHERE "Id" = {{request.Packet.JobId}}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            return Rejected("unknown-job");
        }

        string commitFingerprint = Convert.ToHexStringLower(projection.CommitFingerprint.Span);
        if (job.State == ScanJobExecutionState.Completed)
        {
            DocumentVersion? committed = await context.DocumentVersions.AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.Id == job.CommittedDocumentVersionId,
                    cancellationToken);
            Guid? currentVersionId = await context.LogicalDocuments.AsNoTracking()
                .Where(value => value.Id == request.LogicalDocumentId)
                .Select(value => value.CurrentVersionId)
                .SingleOrDefaultAsync(cancellationToken);
            bool canonicalGraphMatches = await CanonicalGraphMatchesAsync(
                request,
                projection,
                cancellationToken);
            bool exact = JobMatchesPacket(job, request.Packet) &&
                await DurableBindingsMatchAsync(request.Packet, projection, cancellationToken) &&
                canonicalGraphMatches &&
                job.CommittedDocumentVersionId == request.DocumentVersionId &&
                currentVersionId == request.DocumentVersionId &&
                committed is not null &&
                committed.LogicalDocumentId == request.LogicalDocumentId &&
                committed.RepositoryId == request.Packet.RepositoryId &&
                committed.ScanRunId == request.Packet.ScanRunId &&
                committed.ProfiledRevision == request.Packet.ProfiledRevision &&
                committed.RepositoryRelativePath == request.Packet.RepositoryRelativePath &&
                committed.SchemaIdentity == request.Packet.DocumentSchemaIdentity &&
                committed.CommitFingerprint == commitFingerprint &&
                committed.ContentFingerprint ==
                    Convert.ToHexStringLower(projection.ContentFingerprint.Span) &&
                committed.ContentMarkdown == projection.ContentMarkdown;
            await transaction.RollbackAsync(cancellationToken);
            return exact
                ? new(Pass1DocumentCommitStatus.Replayed, request.LogicalDocumentId,
                    request.DocumentVersionId, commitFingerprint, null)
                : Conflict("changed-completed-replay");
        }

        if (job.State != ScanJobExecutionState.Running ||
            job.StateVersion != request.ExpectedJobStateVersion ||
            job.LeaseToken != request.ExpectedLeaseToken ||
            !JobMatchesPacket(job, request.Packet) ||
            !await DurableBindingsMatchAsync(request.Packet, projection, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Rejected("durable-binding-mismatch");
        }

        LogicalDocument? logical = await context.LogicalDocuments
            .FromSqlInterpolated($$"""
                SELECT * FROM vulgata."LogicalDocuments"
                WHERE "Id" = {{request.LogicalDocumentId}}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (logical is not null)
        {
            await context.Entry(logical)
                .Reference(value => value.CurrentVersion)
                .LoadAsync(cancellationToken);
        }
        int versionNumber;
        if (logical is null)
        {
            logical = LogicalDocument.Create(
                request.LogicalDocumentId,
                request.Packet.RepositoryId,
                DocumentType.CodeLogic,
                StableKey(request.Packet),
                request.CommittedAt);
            versionNumber = 1;
            context.LogicalDocuments.Add(logical);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (logical.RepositoryId != request.Packet.RepositoryId ||
                logical.DocumentType != DocumentType.CodeLogic ||
                logical.StableKey != StableKey(request.Packet) ||
                logical.CurrentVersion is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict("logical-document-conflict");
            }

            versionNumber = checked(logical.CurrentVersion.VersionNumber + 1);
        }

        DocumentVersion version = DocumentVersion.Create(
            request.DocumentVersionId,
            request.LogicalDocumentId,
            request.Packet.RepositoryId,
            request.Packet.SystemId,
            request.Packet.ScanRunId,
            request.Packet.ProfiledRevision,
            versionNumber,
            projection.IndexEntry.Title,
            request.Packet.RepositoryRelativePath,
            "code-logic/file-v2",
            projection.ContentJson,
            projection.ContentMarkdown,
            Convert.ToHexStringLower(projection.ContentFingerprint.Span),
            commitFingerprint,
            request.CommittedAt);

        context.DocumentVersions.Add(version);
        await context.SaveChangesAsync(cancellationToken);

        if (logical.CurrentVersion is null)
        {
            logical.SetInitialCurrentVersion(version);
        }
        else
        {
            logical.MoveCurrentVersion(logical.CurrentVersion.Id, version);
        }

        context.SourceAnchors.AddRange(projection.SourceAnchors.Select(anchor => new SourceAnchor(
            anchor.Id,
            request.DocumentVersionId,
            request.Packet.RepositoryId,
            request.Packet.ScanRunId,
            anchor.FileCodeUnitId,
            request.Packet.ProfiledRevision,
            anchor.RepositoryRelativePath,
            anchor.EvidenceLocator,
            checked((int)anchor.LineStart),
            checked((int)anchor.LineEnd),
            Convert.ToHexStringLower(anchor.SourceHash.Span),
            checked((long)anchor.ByteStart),
            checked((long)anchor.ByteEnd),
            request.CommittedAt)));
        context.DocumentSections.AddRange(projection.Sections.Select(section => DocumentSection.Create(
            section.StableSectionId,
            request.LogicalDocumentId,
            request.DocumentVersionId,
            request.Packet.RepositoryId,
            section.StableSectionId,
            section.SectionOrder,
            section.SectionKind,
            section.Heading,
            section.Markdown,
            section.SearchText,
            JsonSerializer.Serialize(section.SourceAnchorIds),
            JsonSerializer.Serialize(section.AuthorizedDocumentVersionIds),
            request.CommittedAt)));
        context.IndexEntries.Add(IndexEntry.Create(
            projection.IndexEntry.Id,
            request.LogicalDocumentId,
            request.DocumentVersionId,
            request.Packet.RepositoryId,
            request.Packet.SystemId,
            DocumentType.CodeLogic,
            projection.IndexEntry.Title,
            projection.IndexEntry.Summary,
            request.Packet.RepositoryRelativePath,
            JsonSerializer.Serialize(projection.IndexEntry.KeySymbols),
            JsonSerializer.Serialize(projection.IndexEntry.BusinessTerms),
            JsonSerializer.Serialize(projection.IndexEntry.TechnicalTerms),
            projection.IndexEntry.SearchText,
            request.CommittedAt));

        Guid[] targetIds = projection.DirectReferenceEdges
            .Select(value => value.TargetDocumentVersionId)
            .Distinct()
            .ToArray();
        DocumentVersion[] targets = await context.DocumentVersions.AsNoTracking()
            .Where(value => targetIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        foreach (CodeLogicFileV2ReferenceEdgeProjection edge in projection.DirectReferenceEdges)
        {
            DocumentVersion target = targets.Single(value => value.Id == edge.TargetDocumentVersionId);
            context.DocumentEdges.Add(new DocumentEdge(
                edge.Id,
                request.DocumentVersionId,
                request.Packet.RepositoryId,
                request.Packet.ScanRunId,
                target.Id,
                target.RepositoryId,
                target.ScanRunId,
                DocumentEdgeType.References,
                request.CommittedAt));
        }

        job.Complete(
            request.ExpectedJobStateVersion,
            request.ExpectedLeaseToken,
            request.DocumentVersionId,
            request.CommittedAt);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(Pass1DocumentCommitStatus.Committed, request.LogicalDocumentId,
            request.DocumentVersionId, commitFingerprint, null);
    }

    private async Task<bool> CanonicalGraphMatchesAsync(
        Pass1DocumentCommitRequest request,
        CodeLogicFileV2Projection projection,
        CancellationToken cancellationToken)
    {
        SourceAnchor[] anchors = await context.SourceAnchors.AsNoTracking()
            .Where(value => value.DocumentVersionId == request.DocumentVersionId)
            .ToArrayAsync(cancellationToken);
        if (anchors.Length != projection.SourceAnchors.Count ||
            projection.SourceAnchors.Any(expected => !anchors.Any(actual =>
                actual.Id == expected.Id &&
                actual.DocumentVersionId == request.DocumentVersionId &&
                actual.RepositoryId == request.Packet.RepositoryId &&
                actual.ScanRunId == request.Packet.ScanRunId &&
                actual.CodeUnitId == expected.FileCodeUnitId &&
                actual.ProfiledRevision == request.Packet.ProfiledRevision &&
                actual.RepositoryRelativePath == expected.RepositoryRelativePath &&
                actual.EvidenceLocator == expected.EvidenceLocator &&
                actual.LineStart == checked((int)expected.LineStart) &&
                actual.LineEnd == checked((int)expected.LineEnd) &&
                actual.SourceHash == Convert.ToHexStringLower(expected.SourceHash.Span) &&
                actual.ByteStart == checked((long)expected.ByteStart) &&
                actual.ByteEnd == checked((long)expected.ByteEnd) &&
                actual.CreatedAt == request.CommittedAt)))
        {
            return false;
        }

        DocumentSection[] sections = await context.DocumentSections.AsNoTracking()
            .Where(value => value.DocumentVersionId == request.DocumentVersionId)
            .OrderBy(value => value.SectionOrder)
            .ToArrayAsync(cancellationToken);
        if (sections.Length != projection.Sections.Count)
        {
            return false;
        }

        for (int index = 0; index < sections.Length; index++)
        {
            DocumentSection actual = sections[index];
            CodeLogicFileV2SectionProjection expected = projection.Sections[index];
            if (actual.Id != expected.StableSectionId ||
                actual.LogicalDocumentId != request.LogicalDocumentId ||
                actual.DocumentVersionId != request.DocumentVersionId ||
                actual.RepositoryId != request.Packet.RepositoryId ||
                actual.StableSectionId != expected.StableSectionId ||
                actual.SectionOrder != expected.SectionOrder ||
                actual.SectionKind != expected.SectionKind ||
                actual.Heading != expected.Heading ||
                actual.ContentMarkdown != expected.Markdown ||
                actual.SearchText != expected.SearchText ||
                !CodeLogicFileV2Projector.MatchesOrderedJsonArray(
                    actual.SourceAnchorIdsJson,
                    expected.SourceAnchorIds) ||
                !CodeLogicFileV2Projector.MatchesOrderedJsonArray(
                    actual.DocumentVersionIdsJson,
                    expected.AuthorizedDocumentVersionIds) ||
                actual.CreatedAt != request.CommittedAt)
            {
                return false;
            }
        }

        IndexEntry[] entries = await context.IndexEntries.AsNoTracking()
            .Where(value => value.DocumentVersionId == request.DocumentVersionId)
            .ToArrayAsync(cancellationToken);
        CodeLogicFileV2IndexEntryProjection indexEntry = projection.IndexEntry;
        if (entries.Length != 1)
        {
            return false;
        }

        IndexEntry actualIndex = entries[0];
        if (actualIndex.Id != indexEntry.Id ||
            actualIndex.LogicalDocumentId != request.LogicalDocumentId ||
            actualIndex.DocumentVersionId != request.DocumentVersionId ||
            actualIndex.RepositoryId != request.Packet.RepositoryId ||
            actualIndex.SystemId != request.Packet.SystemId ||
            actualIndex.DocumentType != DocumentType.CodeLogic ||
            actualIndex.Title != indexEntry.Title ||
            actualIndex.Summary != indexEntry.Summary ||
            actualIndex.RepositoryRelativePath != request.Packet.RepositoryRelativePath ||
            !CodeLogicFileV2Projector.MatchesOrderedJsonArray(
                actualIndex.KeySymbolsJson,
                indexEntry.KeySymbols) ||
            !CodeLogicFileV2Projector.MatchesOrderedJsonArray(
                actualIndex.BusinessTermsJson,
                indexEntry.BusinessTerms) ||
            !CodeLogicFileV2Projector.MatchesOrderedJsonArray(
                actualIndex.TechnicalTermsJson,
                indexEntry.TechnicalTerms) ||
            actualIndex.SearchText != indexEntry.SearchText ||
            actualIndex.CreatedAt != request.CommittedAt)
        {
            return false;
        }

        DocumentEdge[] edges = await context.DocumentEdges.AsNoTracking()
            .Where(value => value.SourceDocumentVersionId == request.DocumentVersionId)
            .ToArrayAsync(cancellationToken);
        Pass1Outcome? outcome = await context.Pass1Outcomes.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.PairExecutionId == request.Packet.PairExecutionId,
                cancellationToken);
        Guid[] targetIds = projection.DirectReferenceEdges
            .Select(value => value.TargetDocumentVersionId)
            .Distinct()
            .ToArray();
        DocumentVersion[] targets = await context.DocumentVersions.AsNoTracking()
            .Where(value => targetIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        bool directEdgesMatch = targets.Length == targetIds.Length &&
            projection.DirectReferenceEdges.All(expected =>
            {
                DocumentVersion? target = targets.SingleOrDefault(value =>
                    value.Id == expected.TargetDocumentVersionId);
                return target is not null && edges.Any(actual =>
                    actual.Id == expected.Id &&
                    actual.SourceDocumentVersionId == request.DocumentVersionId &&
                    actual.SourceRepositoryId == request.Packet.RepositoryId &&
                    actual.SourceScanRunId == request.Packet.ScanRunId &&
                    actual.TargetDocumentVersionId == target.Id &&
                    actual.TargetRepositoryId == target.RepositoryId &&
                    actual.TargetScanRunId == target.ScanRunId &&
                    actual.EdgeType == expected.EdgeType &&
                    actual.CreatedAt == request.CommittedAt);
            });
        if (!directEdgesMatch)
        {
            return false;
        }

        return outcome is null
            ? edges.Length == projection.DirectReferenceEdges.Count
            : await ReconciledUnionMatchesAsync(
                request.Packet.PairExecutionId,
                outcome,
                cancellationToken);
    }

    private async Task<bool> ReconciledUnionMatchesAsync(
        Guid pairExecutionId,
        Pass1Outcome outcome,
        CancellationToken cancellationToken)
    {
        if (outcome.Status == Pass1OutcomeStatus.FailedPass1)
        {
            return false;
        }

        Pass1PairExecution? pair = await context.Pass1PairExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == pairExecutionId, cancellationToken);
        if (pair is null || pair.Phase != Pass1PairExecutionPhase.ReadyForResolution)
        {
            return false;
        }

        PreparedScanManifestEntry[] entries = await context.PreparedScanManifestEntries.AsNoTracking()
            .Where(value => value.PreparedScanManifestId == pair.ManifestOneId ||
                value.PreparedScanManifestId == pair.ManifestTwoId)
            .ToArrayAsync(cancellationToken);
        TwoRepositoryScanCoordination? coordination =
            await context.TwoRepositoryScanCoordinations.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == pair.CoordinationId, cancellationToken);
        if (coordination is null)
        {
            return false;
        }

        Guid[] unitIds = entries.Select(value => value.FileCodeUnitId).ToArray();
        ScanJobExecution[] jobs = await context.ScanJobExecutions.AsNoTracking()
            .Where(value => unitIds.Contains(value.FileCodeUnitId) &&
                (value.ScanExecutionId == coordination.ScanExecutionOneId ||
                    value.ScanExecutionId == coordination.ScanExecutionTwoId))
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, PreparedScanManifestEntry> entriesByUnit = entries
            .ToDictionary(value => value.FileCodeUnitId);
        ScanJobExecution[] completedJobs = jobs
            .Where(value => entriesByUnit.ContainsKey(value.FileCodeUnitId) &&
                value.State == ScanJobExecutionState.Completed &&
                value.CommittedDocumentVersionId is not null)
            .OrderBy(value => entriesByUnit[value.FileCodeUnitId].PairSelectionOrdinal)
            .ToArray();
        Guid[] sourceIds = completedJobs
            .Select(value => value.CommittedDocumentVersionId!.Value)
            .ToArray();
        DocumentVersion[] sources = await context.DocumentVersions.AsNoTracking()
            .Where(value => sourceIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        if (entries.Length != pair.DispatchedFileCount ||
            jobs.Length != pair.DispatchedFileCount ||
            completedJobs.Length != outcome.CompletedCount ||
            sources.Length != completedJobs.Length)
        {
            return false;
        }

        Guid[] repositoryIds = jobs.Select(value => value.RepositoryId).Distinct().ToArray();
        Guid[] currentIds = await context.LogicalDocuments.AsNoTracking()
            .Where(value => value.CurrentVersionId != null &&
                repositoryIds.Contains(value.RepositoryId))
            .Select(value => value.CurrentVersionId!.Value)
            .ToArrayAsync(cancellationToken);
        DocumentVersion[] currentVersions = await context.DocumentVersions.AsNoTracking()
            .Where(value => currentIds.Contains(value.Id) &&
                value.SchemaIdentity == pair.DocumentSchemaIdentity)
            .ToArrayAsync(cancellationToken);
        Guid[] currentVersionIds = currentVersions.Select(value => value.Id).ToArray();
        SourceAnchor[] currentAnchors = await context.SourceAnchors.AsNoTracking()
            .Where(value => currentVersionIds.Contains(value.DocumentVersionId))
            .ToArrayAsync(cancellationToken);

        var observations = new List<Pass1ReferenceObservation>();
        foreach (ScanJobExecution completedJob in completedJobs)
        {
            DocumentVersion source = sources.Single(value =>
                value.Id == completedJob.CommittedDocumentVersionId);
            observations.AddRange(ReadReferenceObservations(
                source,
                completedJob.FileCodeUnitId,
                entriesByUnit[completedJob.FileCodeUnitId].PairSelectionOrdinal));
        }

        Pass1ReferenceObservation[] orderedObservations = observations
            .OrderBy(value => value.SourceRepositoryId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(value => value.PairSelectionOrdinal)
            .ThenBy(value => value.SourceDocumentVersionId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(value => value.ObservationFingerprint, StringComparer.Ordinal)
            .ThenBy(value => value.TargetCanonicalPath, StringComparer.Ordinal)
            .ThenBy(value => value.NormalizedTargetSymbol, StringComparer.Ordinal)
            .ThenBy(value => value.DirectTargetDocumentVersionId?.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<PreparedPass1ReconciledEdge> expectedEdges =
            PreparedPass1ReconciliationProjector.ProjectExpectedUnion(
                orderedObservations,
                currentVersions,
                currentAnchors);
        DocumentEdge[] actualEdges = await context.DocumentEdges.AsNoTracking()
            .Where(value => sourceIds.Contains(value.SourceDocumentVersionId))
            .ToArrayAsync(cancellationToken);
        return outcome.ReconciledEdgeCount == expectedEdges.Count &&
            PreparedPass1ReconciliationProjector.MatchesDurableUnion(
                expectedEdges,
                actualEdges,
                orderedObservations,
                sources,
                currentVersions,
                outcome.CompletedAt);
    }

    private async Task<bool> DurableBindingsMatchAsync(
        PreparedPass1WorkerPacket packet,
        CodeLogicFileV2Projection projection,
        CancellationToken cancellationToken)
    {
        Pass1PairExecution? pair = await context.Pass1PairExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == packet.PairExecutionId, cancellationToken);
        ScanExecution? execution = await context.ScanExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == packet.ScanExecutionId, cancellationToken);
        PreparedScanManifest? manifest = await context.PreparedScanManifests.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == packet.PreparedScanManifestId, cancellationToken);
        PreparedScanManifestEntry? entry = await context.PreparedScanManifestEntries.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.PreparedScanManifestId == packet.PreparedScanManifestId &&
                value.FileCodeUnitId == packet.FileCodeUnitId,
                cancellationToken);
        CodeUnit? unit = await context.CodeUnits.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == packet.FileCodeUnitId, cancellationToken);

        if (pair is null || execution is null || manifest is null || entry is null || unit is null ||
            pair.SystemId != packet.SystemId || pair.PairPackageFingerprint != packet.PairPackageFingerprint ||
            pair.DocumentSchemaIdentity != packet.DocumentSchemaIdentity ||
            (pair.ManifestOneId != manifest.Id && pair.ManifestTwoId != manifest.Id) ||
            execution.RepositoryId != packet.RepositoryId || execution.SystemId != packet.SystemId ||
            execution.ScanRunId != packet.ScanRunId || execution.ProfiledRevision != packet.ProfiledRevision ||
            execution.PromptIdentity != packet.PromptIdentity ||
            execution.DocumentSchemaIdentity != packet.DocumentSchemaIdentity ||
            execution.AnalysisRuntimeIdentityHash != packet.AnalysisRuntimeIdentityHash ||
            manifest.RepositoryId != packet.RepositoryId || manifest.ScanRunId != packet.ScanRunId ||
            manifest.ProfiledRevision != packet.ProfiledRevision || manifest.Fingerprint != packet.PreparedScanManifestFingerprint ||
            manifest.RuntimeIdentityHash != packet.AnalysisRuntimeIdentityHash ||
            manifest.ApprovalState != PreparedScanManifestApprovalState.Approved ||
            entry.FileCodeUnitIdentityFingerprint != packet.FileCodeUnitIdentityFingerprint ||
            entry.ManifestOrdinal != packet.ManifestOrdinal || entry.PairSelectionOrdinal != packet.PairSelectionOrdinal ||
            unit.RepositoryId != packet.RepositoryId || unit.ScanRunId != packet.ScanRunId ||
            unit.FilePath != packet.RepositoryRelativePath || unit.FileHash != packet.FileHash ||
            unit.SourceHash != packet.SourceHash || unit.ByteStart != packet.ByteStart ||
            unit.ByteEndExclusive != packet.ByteEnd || unit.LineStart != packet.LineStart ||
            unit.LineEnd != packet.LineEnd || unit.SegmentOrdinal != packet.SegmentOrdinal ||
            unit.SegmentCount != packet.SegmentCount ||
            unit.FileCodeUnitIdentityFingerprint != packet.FileCodeUnitIdentityFingerprint)
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(projection.ContentJson);
        Guid[] receiptIds = ReceiptIds(document.RootElement).Distinct().ToArray();
        Pass1NavigationReceipt[] receipts = await context.Pass1NavigationReceipts.AsNoTracking()
            .Where(value => receiptIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        if (receipts.Length != receiptIds.Length || receipts.Any(value =>
                value.ScanJobExecutionId != packet.JobId ||
                value.PairExecutionId != packet.PairExecutionId ||
                value.ToolAttemptOrdinal is < 1 or > 8 || value.Depth is < 1 or > 3 ||
                value.ResultCount > 20 || value.EvidenceBytes > 65_536))
        {
            return false;
        }

        Guid[] classificationIds = document.RootElement.GetProperty("communicationEvidence")
            .EnumerateArray()
            .SelectMany(value => value.GetProperty("classificationEvidence").EnumerateArray())
            .Select(value => value.GetProperty("classificationId").GetGuid())
            .Distinct()
            .ToArray();
        CommunicationKindClassification[] classifications = await context.CommunicationKindClassifications
            .AsNoTracking()
            .Where(value => classificationIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        if (classifications.Length != classificationIds.Length ||
            classifications.Any(value => value.PreScanResultId != execution.PreScanResultId) ||
            packet.CommunicationClassifications.Any(supplied =>
                !ClassificationMatches(supplied, classifications)))
        {
            return false;
        }

        Guid[] targetIds = projection.DirectReferenceEdges.Select(value => value.TargetDocumentVersionId).ToArray();
        if (targetIds.Length != targetIds.Distinct().Count())
        {
            return false;
        }

        DocumentVersion[] targets = await context.DocumentVersions.AsNoTracking()
            .Where(value => targetIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        if (targets.Length != targetIds.Distinct().Count() ||
            targets.Any(value => value.RepositoryId != packet.RepositoryId || value.SupersededById != null))
        {
            return false;
        }

        Dictionary<Guid, (string Path, string SourceHash)> claimedTargets = document.RootElement
            .GetProperty("knownReferences")
            .EnumerateArray()
            .ToDictionary(
                value => value.GetProperty("targetDocumentVersionId").GetGuid(),
                value => (
                    value.GetProperty("targetRepositoryRelativePath").GetString()!,
                    value.GetProperty("targetSourceHash").GetString()!));
        Guid[] targetAnchorIds = targets.Select(value => value.Id).ToArray();
        SourceAnchor[] targetAnchors = await context.SourceAnchors.AsNoTracking()
            .Where(value => targetAnchorIds.Contains(value.DocumentVersionId))
            .ToArrayAsync(cancellationToken);
        if (targets.Any(target =>
                !claimedTargets.TryGetValue(target.Id, out var claim) ||
                target.RepositoryRelativePath != claim.Path ||
                !targetAnchors.Any(anchor =>
                    anchor.DocumentVersionId == target.Id && anchor.SourceHash == claim.SourceHash)))
        {
            return false;
        }

        Guid[] currentTargetIds = await context.LogicalDocuments.AsNoTracking()
            .Where(value => value.CurrentVersionId != null && targetIds.Contains(value.CurrentVersionId.Value))
            .Select(value => value.CurrentVersionId!.Value)
            .ToArrayAsync(cancellationToken);
        return currentTargetIds.Order().SequenceEqual(targetIds.Distinct().Order());
    }

    private static bool JobMatchesPacket(ScanJobExecution job, PreparedPass1WorkerPacket packet) =>
        job.ScanExecutionId == packet.ScanExecutionId &&
        job.RepositoryId == packet.RepositoryId &&
        job.ScanRunId == packet.ScanRunId &&
        job.FileCodeUnitId == packet.FileCodeUnitId &&
        job.ManifestOrdinal == packet.ManifestOrdinal;

    private static bool PacketSourceMatches(PreparedPass1WorkerPacket packet) =>
        packet.SourceUtf8.Length == packet.ByteEnd - packet.ByteStart &&
        Convert.ToHexStringLower(SHA256.HashData(packet.SourceUtf8.Span)) == packet.SourceHash;

    private static bool ClassificationMatches(
        PreparedPass1CommunicationClassificationProjection supplied,
        IReadOnlyList<CommunicationKindClassification> durable)
    {
        CommunicationKindClassification? value = durable.SingleOrDefault(item => item.Id == supplied.ClassificationId);
        return value is not null && supplied.FileCodeUnitId != Guid.Empty &&
            value.Fingerprint == supplied.ClassificationFingerprint &&
            value.Kind == supplied.Kind && value.Level == supplied.Level &&
            value.Confidence == supplied.Confidence && value.Rule == supplied.Rule &&
            value.Provenance == supplied.Provenance &&
            value.CatalogRuleIdentity == supplied.CatalogRuleIdentity &&
            value.RelativePath == supplied.RepositoryRelativePath &&
            value.Locator == supplied.EvidenceLocator;
    }

    private static IEnumerable<Guid> ReceiptIds(JsonElement root) =>
        root.GetProperty("communicationEvidence").EnumerateArray()
            .SelectMany(value => value.GetProperty("navigationReceiptIds").EnumerateArray())
            .Concat(root.GetProperty("knownReferences").EnumerateArray()
                .Select(value => value.GetProperty("navigationReceiptId")))
            .Concat(root.GetProperty("deferredObservations").EnumerateArray()
                .SelectMany(value => value.GetProperty("navigationReceiptIds").EnumerateArray()))
            .Select(value => value.GetGuid());

    private static string StableKey(PreparedPass1WorkerPacket packet) =>
        $"{packet.RepositoryRelativePath}#segment={packet.SegmentOrdinal}";

    private static Pass1ReferenceObservation CreateObservation(
        DocumentVersion source,
        Guid sourceFileCodeUnitId,
        int pairSelectionOrdinal,
        string sourceClaimLocator,
        string? normalizedTargetSymbol,
        string? targetCanonicalPath,
        string? targetSourceHash,
        Guid? directTargetDocumentVersionId,
        bool canResolve,
        string? rejectionCode)
    {
        ReadOnlyMemory<byte>? hash = targetSourceHash is null
            ? null
            : Convert.FromHexString(targetSourceHash);
        PreparedPass1CanonicalArtifact artifact = PreparedPass1CanonicalCodec.CreateReferenceObservation(new(
            source.Id,
            sourceFileCodeUnitId,
            sourceClaimLocator,
            normalizedTargetSymbol,
            targetCanonicalPath,
            hash,
            TargetLineRange: null));
        return new(
            source.RepositoryId,
            source.ScanRunId,
            source.ProfiledRevision,
            pairSelectionOrdinal,
            source.Id,
            artifact.FingerprintHex,
            normalizedTargetSymbol,
            targetCanonicalPath,
            targetSourceHash,
            directTargetDocumentVersionId,
            canResolve,
            rejectionCode);
    }

    private static string? OptionalString(JsonElement owner, string propertyName)
    {
        JsonElement value = owner.GetProperty(propertyName);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }

    private static string DeferredReason(string outcome) => outcome switch
    {
        "Ambiguous" => "ambiguous-target",
        "Stale" => "stale-target",
        "Unsupported" => "unsupported-operation",
        "CrossRepository" => "cross-repository",
        "BoundExceeded" => "navigation-bound-exceeded",
        _ => "no-exact-current-target",
    };

    private static Pass1DocumentCommitResult Rejected(string reason) =>
        new(Pass1DocumentCommitStatus.Rejected, null, null, null, reason);

    private static Pass1DocumentCommitResult Conflict(string reason) =>
        new(Pass1DocumentCommitStatus.Conflict, null, null, null, reason);
}

internal sealed record Pass1ReferenceObservation(
    Guid SourceRepositoryId,
    Guid SourceScanRunId,
    string SourceProfiledRevision,
    int PairSelectionOrdinal,
    Guid SourceDocumentVersionId,
    string ObservationFingerprint,
    string? NormalizedTargetSymbol,
    string? TargetCanonicalPath,
    string? TargetSourceHash,
    Guid? DirectTargetDocumentVersionId,
    bool CanResolve,
    string? RejectionCode);
