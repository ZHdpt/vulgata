using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Loads planner inputs through the owning immutable October persistence boundary.</summary>
public sealed class OctoberFlowPlanningInputLoader : IOctoberFlowPlanningInputLoader
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private readonly IDbContextFactory<OctoberDbContext> _contexts;
    private readonly IOctoberKnowledgeProducerAuthorization _authorization;

    public OctoberFlowPlanningInputLoader(IDbContextFactory<OctoberDbContext> contexts,
        IOctoberKnowledgeProducerAuthorization authorization)
    {
        _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public async Task<FlowPlanningInputResult> LoadAsync(CorpusHandle corpus, ImmutableArray<FlowRootSelection> roots,
        CancellationToken cancellationToken = default)
    {
        if (corpus is null || corpus.Binding is null || roots.IsDefault || roots.Length > OctoberKnowledgeStoredReadBudget.MaximumItems)
            return Unavailable();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var ct = deadline.Token;
        OctoberDbContext? context = null;
        Task<FlowPlanningInputResult>? reading = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            var creating = _contexts.CreateDbContextAsync(ct);
            try { context = await creating.WaitAsync(ct); }
            catch
            {
                _ = DisposeLateContextAsync(creating);
                throw;
            }
            ct.ThrowIfCancellationRequested();
            reading = LoadCoreAsync(context, corpus, roots, ct);
            var result = await reading.WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception) { return Unavailable(); }
        finally
        {
            if (context is not null)
            {
                if (reading is { IsCompleted: false }) _ = DisposeAfterReadAsync(context, reading);
                else await context.DisposeAsync();
            }
        }
    }

    private async Task<FlowPlanningInputResult> LoadCoreAsync(OctoberDbContext context, CorpusHandle handle,
        ImmutableArray<FlowRootSelection> roots, CancellationToken ct)
    {
        Guid batch = handle.Binding.BatchId;
        Guid corpusId = handle.CorpusId;
        var budget = new OctoberKnowledgeStoredReadBudget();
        // The corpus ledger is itself bounded before materialization or authorization.
        if (context.Database.IsNpgsql())
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("ContributorsJson"::text)::bigint AS "Value"
                FROM october."OctoberCorpusSelections" WHERE "BatchId"={batch} AND "CorpusId"={corpusId} LIMIT 2
                """).ToListAsync(ct), 1, bytes: true);
        else
            budget.Add(await context.CorpusSelections.Where(x => x.BatchId == batch && x.CorpusId == corpusId)
                .Select(x => (long)x.ContributorsJson.Length).Take(2).ToListAsync(ct), 1);
        ct.ThrowIfCancellationRequested();
        var retained = await OctoberResolutionStore.FindCorpusAsync(context, handle, ct);
        if (retained is null) return Unavailable();
        var ledger = Required<ImmutableArray<Contributor>>(retained.ContributorsJson);
        if (ledger.IsDefault || ledger.Length > OctoberKnowledgeStoredReadBudget.MaximumContributors || ledger.Any(x => x is null) ||
            !await AuthorizedAsync(handle.Binding, ledger, ct)) return Unavailable();
        if (retained.CorpusDigest != handle.CorpusDigest) return Unavailable();
        await PreflightAsync(context, handle, budget, ct);
        ct.ThrowIfCancellationRequested();
        var corpus = await OctoberResolutionStore.LoadExactCorpusAsync(context, handle, ct, budget);
        var plan = OctoberResolutionPolicy.Plan(corpus);
        var memberRows = await context.CorpusDocuments.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(ct);
        var manifestIds = await context.CorpusManifests.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).Select(x => x.ManifestId).ToArrayAsync(ct);
        var manifests = await context.AcceptedFileManifests.AsNoTracking().Where(x => x.BatchId == batch && manifestIds.Contains(x.ManifestId)).ToListAsync(ct);
        var coverageRows = await context.CorpusCoverage.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(ct);
        var coverage = coverageRows.Select(x => Required<FileCoverage>(x.CoverageJson)).ToImmutableArray();
        var contributorRows = await context.KnowledgeContributors.AsNoTracking().Where(x => x.BatchId == batch && manifestIds.Contains(x.ManifestId)).ToListAsync(ct);
        if (manifests.Count != manifestIds.Length || memberRows.Count != corpus.Documents.Length ||
            !SameSet(ledger, coverageRows.SelectMany(x => Required<ImmutableArray<Contributor>>(x.ContributorsJson))) ||
            !coverage.Select(x => x.AdmissionId).ToHashSet().SetEquals(coverageRows.Select(x => x.AdmissionId)))
            return Unavailable();
        foreach (var row in coverageRows)
        {
            var value = Required<FileCoverage>(row.CoverageJson);
            var supplied = Required<ImmutableArray<Contributor>>(row.ContributorsJson);
            if (!OctoberKnowledgePublicationPolicy.ValidCoverage(value) || value.AdmissionId != row.AdmissionId || value.Origin.RepositoryId != row.RepositoryId)
                return Unavailable();
            var manifest = manifests.SingleOrDefault(x => x.AdmissionId == row.AdmissionId);
            if (manifest is not null)
            {
                if (!OctoberKnowledgePublicationPolicy.SameCoverage(value, Coverage(manifest)) ||
                    !SameSet(supplied, contributorRows.Where(x => x.ManifestId == manifest.ManifestId).Select(x => Required<Contributor>(x.ContributorJson))))
                    return Unavailable();
            }
            else
            {
                if (value.Disposition is CoverageDisposition.Accepted or CoverageDisposition.CompletedZeroUnit) return Unavailable();
                var repository = await context.RepositorySelections.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == batch && x.RepositoryId == value.Origin.RepositoryId, ct);
                if (repository is null || repository.RootBindingIdentity != value.Origin.RootBindingIdentity ||
                    !SameSet(supplied, [new Contributor(ContributorKind.Source, repository.RepositoryId, repository.SystemId,
                        value.Origin.RootBindingIdentity, null, value.Origin.SourceDigest)])) return Unavailable();
            }
        }
        if (manifests.Any(x => coverage.All(c => c.AdmissionId != x.AdmissionId) ||
                memberRows.Count(m => m.ManifestId == x.ManifestId) != x.UnitCount)) return Unavailable();

        var fileUnknownRows = await context.KnowledgeUncertainties.AsNoTracking()
            .Where(x => x.BatchId == batch && manifestIds.Contains(x.ManifestId)).ToListAsync(ct);
        var fileUnknowns = ImmutableArray.CreateBuilder<FlowFileUncertainty>();
        foreach (var row in fileUnknownRows)
        {
            ct.ThrowIfCancellationRequested();
            var content = Required<Uncertainty>(row.ContentJson);
            var manifest = manifests.Single(x => x.ManifestId == row.ManifestId);
            var manifestContributors = contributorRows.Where(x => x.ManifestId == row.ManifestId)
                .Select(x => Required<Contributor>(x.ContributorJson)).ToArray();
            if (string.IsNullOrWhiteSpace(row.Key) || row.Key != content.Key || string.IsNullOrWhiteSpace(content.Explanation) ||
                content.Evidence.IsDefault || content.Limitations.IsDefault || content.Evidence.Any(e =>
                    e is null || e.Document is not null || e.ClaimGroupId is not null ||
                    e.Source is { } source && (e.AdvisoryRecordId is not null || source.StartLine < 1 || source.EndLine < source.StartLine ||
                        source.Origin.RepositoryId != manifest.RepositoryId || source.Origin.RootBindingIdentity != manifest.RootBindingIdentity ||
                        e.ContentDigest != source.Origin.SourceDigest || !manifestContributors.Any(c => c.Kind == ContributorKind.Source &&
                            c.RepositoryId == source.Origin.RepositoryId && c.RootBindingIdentity == source.Origin.RootBindingIdentity && c.ContentDigest == e.ContentDigest &&
                            (c.SourceOrigin is { } suppliedOrigin && SameOrigin(suppliedOrigin, source.Origin) ||
                                c.SourceOrigin is null && SameOrigin(Coverage(manifest).Origin, source.Origin)))) ||
                    e.Source is null && (e.AdvisoryRecordId is null || !manifestContributors.Any(c =>
                        c.AdvisoryRecordId == e.AdvisoryRecordId && c.ContentDigest == e.ContentDigest))))
                return Unavailable();
            fileUnknowns.Add(new(batch, row.ManifestId, row.Key, content));
        }

        var projectedUnits = ImmutableArray.CreateBuilder<FlowPlanningUnit>();
        foreach (var member in memberRows)
        {
            ct.ThrowIfCancellationRequested();
            var version = await context.DocumentVersions.AsNoTracking().SingleAsync(x => x.BatchId == batch && x.VersionId == member.VersionId && x.LogicalDocumentId == member.LogicalDocumentId, ct);
            var logical = await context.LogicalDocuments.AsNoTracking().SingleAsync(x => x.BatchId == batch && x.LogicalDocumentId == member.LogicalDocumentId, ct);
            var unit = await context.CodeUnits.AsNoTracking().SingleAsync(x => x.BatchId == batch && x.UnitId == logical.UnitId, ct);
            var manifest = manifests.Single(x => x.ManifestId == member.ManifestId);
            var origin = Coverage(manifest).Origin;
            var anchors = Required<ImmutableArray<SourceAnchor>>(version.AnchorsJson);
            if (logical.Kind != DocumentKind.CodeLogic || version.ManifestId != manifest.ManifestId || unit.ManifestId != manifest.ManifestId ||
                unit.RepositoryId != manifest.RepositoryId || anchors.IsDefaultOrEmpty || anchors.Any(x => !SameOrigin(x.Origin, origin) || x.StartLine < 1 || x.EndLine < x.StartLine))
                return Unavailable();
            projectedUnits.Add(new(unit.UnitId, new(batch, logical.LogicalDocumentId, version.VersionId), unit.SemanticSelector, origin, anchors));
        }

        var items = await context.ResolutionItems.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(ct);
        var dependencies = await context.ResolutionDocumentDependencies.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(ct);
        var edges = await context.RelationshipVersions.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(ct);
        var unknowns = await context.UncertaintyVersions.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(ct);
        if (items.Count != plan.Inquiries.Length || !items.Select(x => x.InquiryFingerprint).ToHashSet().SetEquals(plan.Inquiries.Select(x => x.Fingerprint)) ||
            dependencies.Any(x => items.All(i => i.ItemId != x.ItemId)) || edges.Any(x => items.All(i => i.ItemId != x.ItemId)) || unknowns.Any(x => items.All(i => i.ItemId != x.ItemId)))
            return Unavailable();
        var resolutions = ImmutableArray.CreateBuilder<FlowAcceptedResolution>();
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            var inquiry = plan.Inquiries.Single(x => x.Fingerprint == item.InquiryFingerprint);
            var outcome = Required<ResolutionOutcome>(item.OutcomeJson);
            if (item.ItemId != new Guid(Convert.FromHexString(inquiry.Fingerprint).AsSpan(0, 16)) ||
                item.OperationKind != OperationKind.AcceptResolution || item.PolicyVersion != OctoberResolutionPolicy.PolicyVersion ||
                !SameSet(Required<ImmutableArray<Contributor>>(item.ContributorsJson), ledger) ||
                !SameSet(Required<ImmutableArray<ResolutionItemDisposition>>(item.PlanDispositionsJson), plan.Dispositions)) return Unavailable();
            ResolutionOutcome expected;
            if (item.WitnessJson is not null)
                expected = OctoberResolutionPolicy.Validate(corpus, inquiry, Required<ResolutionWitness>(item.WitnessJson)) with { Contributors = ledger };
            else
            {
                if (outcome.Kind is not (ResolutionOutcomeKind.Failed or ResolutionOutcomeKind.NotEvaluated)) return Unavailable();
                var caller = corpus.Documents.Single(x => x.Version == inquiry.Caller.Document).Observations.Single(x => x.ObservationKey == inquiry.Caller.ObservationKey);
                string reason = outcome.Kind == ResolutionOutcomeKind.Failed ? "The host inquiry failed; no connecting conclusion was accepted." : "The host did not evaluate this selected inquiry.";
                expected = new(inquiry.Fingerprint, outcome.Kind, null, new("resolution/" + inquiry.Fingerprint,
                    "Static linkage remains unresolved.", caller.Evidence, inquiry.Limitations.Add(reason)), ledger);
            }
            if (!Same(expected, outcome)) return Unavailable();
            var itemDependencies = dependencies.Where(x => x.ItemId == item.ItemId).ToArray();
            if (itemDependencies.Length != corpus.Documents.Length || !itemDependencies.Select(x => new DocumentVersionReference(batch, x.LogicalDocumentId, x.VersionId))
                    .ToHashSet().SetEquals(corpus.Documents.Select(x => x.Version)) ||
                itemDependencies.Count(x => x.IsInquiryCaller) != 1 ||
                !itemDependencies.Any(x => x.IsInquiryCaller && x.LogicalDocumentId == inquiry.Caller.Document.LogicalDocumentId && x.VersionId == inquiry.Caller.Document.DocumentVersionId)) return Unavailable();
            var itemEdges = edges.Where(x => x.ItemId == item.ItemId).ToArray();
            if (itemEdges.Length != (outcome.Relationship is null ? 0 : 1)) return Unavailable();
            if (outcome.Relationship is { } edge)
            {
                var stored = itemEdges[0];
                if (stored.RelationshipVersionId != edge.RelationshipVersionId || stored.FromLogicalDocumentId != edge.From.LogicalDocumentId ||
                    stored.FromVersionId != edge.From.DocumentVersionId || stored.ToLogicalDocumentId != edge.To.LogicalDocumentId ||
                    stored.ToVersionId != edge.To.DocumentVersionId || !Same(Required<AcceptedRelationship>(stored.ContentJson), edge)) return Unavailable();
            }
            var itemUnknowns = unknowns.Where(x => x.ItemId == item.ItemId).ToArray();
            if (itemUnknowns.Length != (outcome.Uncertainty is null ? 0 : 1)) return Unavailable();
            if (outcome.Uncertainty is { } unknown)
            {
                Guid itemId = item.ItemId;
                var expectedId = new Guid(Convert.FromHexString(OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(
                    new { domain = "october/resolution-uncertainty/1", itemId, unknown.Key }))).AsSpan(0, 16));
                if (itemUnknowns[0].UncertaintyId != expectedId || itemUnknowns[0].Key != unknown.Key || !Same(Required<Uncertainty>(itemUnknowns[0].ContentJson), unknown)) return Unavailable();
            }
            var operation = await context.KnowledgeOperations.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == batch && x.Kind == item.OperationKind && x.OperationId == item.OperationId, ct);
            if (operation is null || !SameSet(Required<ImmutableArray<Contributor>>(operation.ContributorsJson), ledger)) return Unavailable();
            var receipt = Required<KnowledgeWriteResult>(operation.CommittedResultJson);
            if (receipt.Status != KnowledgeWriteStatus.Committed || receipt.Corpus != handle || receipt.Operation != new OperationKey(batch, item.OperationKind, item.OperationId)) return Unavailable();
            resolutions.Add(new(item.ItemId, outcome, itemUnknowns.SingleOrDefault()?.UncertaintyId));
        }
        var input = new FlowPlanningInput(corpus, projectedUnits.ToImmutable(), resolutions.ToImmutable(), roots, plan.Dispositions, coverage)
        {
            Contributors = ledger,
            FileUncertainties = fileUnknowns.OrderBy(x => x.ManifestId).ThenBy(x => x.Key, StringComparer.Ordinal).ToImmutableArray()
        };
        // Check exact source-backed unit identity even for a deferred root kind. This probe
        // discards its groups and never changes the requested kind or authorizes synthesis.
        // Deduplicate only the probe: the original selection can contain the same unit under
        // distinct kinds and must retain that complete terminal-outcome partition.
        var identityProbe = OctoberFlowPlanner.Plan(input with
        {
            Roots = roots.Select(root => root with { Kind = FlowRootKind.SelectedCodeUnit }).Distinct().ToImmutableArray()
        });
        if (identityProbe.Status != FlowPlanningStatus.Succeeded ||
            identityProbe.RootDispositions.Any(x => x.Kind != FlowRootDispositionKind.Planned)) return Unavailable();
        var planned = OctoberFlowPlanner.Plan(input);
        if (planned.Status != FlowPlanningStatus.Succeeded ||
            planned.RootDispositions.Any(x => x.Kind is not (FlowRootDispositionKind.Planned or FlowRootDispositionKind.UnsupportedKind))) return Unavailable();
        if (!await AuthorizedAsync(handle.Binding, ledger, ct)) return Unavailable();
        ct.ThrowIfCancellationRequested();
        return new(KnowledgeReadStatus.Success, input);
    }

    private async Task<bool> AuthorizedAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var checking = _authorization.CheckAsync(binding, contributors, ct);
        try
        {
            var result = await checking.WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            return result == DisclosureStatus.Allowed;
        }
        catch { _ = ObserveAsync(checking); throw; }
    }

    private static async Task PreflightAsync(OctoberDbContext context, CorpusHandle handle, OctoberKnowledgeStoredReadBudget budget, CancellationToken ct)
    {
        Guid batch = handle.Binding.BatchId;
        Guid corpusId = handle.CorpusId;
        var items = context.ResolutionItems.Where(x => x.BatchId == batch && x.CorpusId == corpusId);
        if (context.Database.IsNpgsql())
        {
            // jsonb is measured as its actual UTF-8 representation, not SQL length(jsonb) or
            // an in-memory string. Every query feeds the same aggregate budget as the CL read.
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT coalesce(octet_length("WitnessJson"::text),0)::bigint + octet_length("OutcomeJson"::text)
                    + octet_length("ContributorsJson"::text) + octet_length("PlanDispositionsJson"::text) AS "Value"
                FROM october."OctoberResolutionItems" WHERE "BatchId"={batch} AND "CorpusId"={corpusId} LIMIT 65
                """).ToListAsync(ct), 64, bytes: true);
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("CoverageJson"::text)::bigint + octet_length("ContributorsJson"::text) AS "Value"
                FROM october."OctoberCorpusCoverage" WHERE "BatchId"={batch} AND "CorpusId"={corpusId} LIMIT 10001
                """).ToListAsync(ct), bytes: true);
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("ContentJson"::text)::bigint AS "Value"
                FROM october."OctoberRelationshipVersions" WHERE "BatchId"={batch} AND "CorpusId"={corpusId} LIMIT 65
                """).ToListAsync(ct), 64, bytes: true);
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("ContentJson"::text)::bigint AS "Value"
                FROM october."OctoberUncertaintyVersions" WHERE "BatchId"={batch} AND "CorpusId"={corpusId} LIMIT 65
                """).ToListAsync(ct), 64, bytes: true);
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length(v."AnchorsJson"::text)::bigint AS "Value"
                FROM october."OctoberDocumentVersions" v JOIN october."OctoberCorpusDocuments" m
                    ON m."BatchId"=v."BatchId" AND m."VersionId"=v."VersionId" AND m."LogicalDocumentId"=v."LogicalDocumentId"
                WHERE m."BatchId"={batch} AND m."CorpusId"={corpusId} LIMIT 10001
                """).ToListAsync(ct), bytes: true);
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length(o."ContributorsJson"::text)::bigint + octet_length(o."CommittedResultJson"::text) AS "Value"
                FROM october."KnowledgeOperations" o JOIN october."OctoberResolutionItems" i
                    ON i."BatchId"=o."BatchId" AND i."OperationKind"=o."Kind" AND i."OperationId"=o."OperationId"
                WHERE i."BatchId"={batch} AND i."CorpusId"={corpusId} LIMIT 65
                """).ToListAsync(ct), 64, bytes: true);
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length(u."ContentJson"::text)::bigint AS "Value"
                FROM october."OctoberKnowledgeUncertainties" u JOIN october."OctoberCorpusManifests" m
                    ON m."BatchId"=u."BatchId" AND m."ManifestId"=u."ManifestId"
                WHERE m."BatchId"={batch} AND m."CorpusId"={corpusId} LIMIT 10001
                """).ToListAsync(ct), bytes: true);
        }
        else
        {
            budget.Add(await items.Select(x => (long)(x.WitnessJson == null ? 0 : x.WitnessJson.Length) + x.OutcomeJson.Length + x.ContributorsJson.Length + x.PlanDispositionsJson.Length).Take(65).ToListAsync(ct), 64);
            budget.Add(await context.CorpusCoverage.Where(x => x.BatchId == batch && x.CorpusId == corpusId)
                .Select(x => (long)x.CoverageJson.Length + x.ContributorsJson.Length).Take(10001).ToListAsync(ct));
            budget.Add(await context.RelationshipVersions.Where(x => x.BatchId == batch && x.CorpusId == corpusId).Select(x => (long)x.ContentJson.Length).Take(65).ToListAsync(ct), 64);
            budget.Add(await context.UncertaintyVersions.Where(x => x.BatchId == batch && x.CorpusId == corpusId).Select(x => (long)x.ContentJson.Length).Take(65).ToListAsync(ct), 64);
            var versionIds = context.CorpusDocuments.Where(x => x.BatchId == batch && x.CorpusId == corpusId).Select(x => x.VersionId);
            budget.Add(await context.DocumentVersions.Where(x => x.BatchId == batch && versionIds.Contains(x.VersionId)).Select(x => (long)x.AnchorsJson.Length).Take(10001).ToListAsync(ct));
            var operationIds = items.Select(x => x.OperationId);
            budget.Add(await context.KnowledgeOperations.Where(x => x.BatchId == batch && x.Kind == OperationKind.AcceptResolution && operationIds.Contains(x.OperationId))
                .Select(x => (long)x.ContributorsJson.Length + x.CommittedResultJson.Length).Take(65).ToListAsync(ct), 64);
            var manifestIds = context.CorpusManifests.Where(x => x.BatchId == batch && x.CorpusId == corpusId).Select(x => x.ManifestId);
            budget.Add(await context.KnowledgeUncertainties.Where(x => x.BatchId == batch && manifestIds.Contains(x.ManifestId))
                .Select(x => (long)x.ContentJson.Length).Take(10001).ToListAsync(ct));
        }
        budget.Add(await context.CorpusManifests.Where(x => x.BatchId == batch && x.CorpusId == corpusId).Select(x => 0L).Take(10001).ToListAsync(ct));
        budget.Add(await context.ResolutionDocumentDependencies.Where(x => x.BatchId == batch && x.CorpusId == corpusId).Select(x => 0L).Take(640001).ToListAsync(ct), 640000);
    }

    private static FileCoverage Coverage(AcceptedFileManifest manifest) => new(manifest.AdmissionId,
        new(manifest.RepositoryId, manifest.RootBindingIdentity, manifest.RelativePath, manifest.SourceDigest, manifest.PreparedVersion),
        manifest.CoverageDisposition, manifest.CoverageReason);
    private static bool Same<T>(T left, T right) => OctoberKnowledgePolicy.SerializeCanonical(left) == OctoberKnowledgePolicy.SerializeCanonical(right);
    private static bool SameOrigin(PreparedOrigin left, PreparedOrigin right) =>
        (left with { RelativePath = OctoberKnowledgePolicy.NormalizePath(left.RelativePath) }) ==
        (right with { RelativePath = OctoberKnowledgePolicy.NormalizePath(right.RelativePath) });
    private static bool SameSet<T>(IEnumerable<T> left, IEnumerable<T> right) =>
        left.Select(OctoberKnowledgePolicy.SerializeCanonical).ToHashSet(StringComparer.Ordinal).SetEquals(right.Select(OctoberKnowledgePolicy.SerializeCanonical));
    private static T Required<T>(string json) => JsonSerializer.Deserialize<T>(json, _json) ?? throw new JsonException("Missing immutable flow input.");
    private static FlowPlanningInputResult Unavailable() => new(KnowledgeReadStatus.Unavailable, null);
    private static async Task ObserveAsync(Task task) { try { await task; } catch (Exception) { } }
    private static async Task DisposeLateContextAsync(Task<OctoberDbContext> creating)
    {
        try { await using var context = await creating; } catch (Exception) { }
    }
    private static async Task DisposeAfterReadAsync(OctoberDbContext context, Task reading)
    {
        try { await reading; } catch (Exception) { }
        finally { try { await context.DisposeAsync(); } catch (Exception) { } }
    }
}
