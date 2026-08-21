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
            bool exact = JobMatchesPacket(job, request.Packet) &&
                await DurableBindingsMatchAsync(request.Packet, projection, cancellationToken) &&
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

    private static Pass1DocumentCommitResult Rejected(string reason) =>
        new(Pass1DocumentCommitStatus.Rejected, null, null, null, reason);

    private static Pass1DocumentCommitResult Conflict(string reason) =>
        new(Pass1DocumentCommitStatus.Conflict, null, null, null, reason);
}
