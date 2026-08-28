using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class ChatEvidenceCurrentnessValidator(VulgataDbContext context) :
    IChatEvidenceCurrentnessValidator
{
    private readonly VulgataDbContext _context = context;

    public async Task<ChatCurrentnessValidationOutcome> ValidateCurrentAsync(
        ChatCurrentnessValidationRequest request,
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        if (request is null || accessScope is null)
        {
            return ChatCurrentnessValidationOutcome.MissingOrStale;
        }

        Guid[] repositoryIds = request.RepositoryIds.Order().ToArray();
        if (repositoryIds.Length != 2 ||
            repositoryIds.Any(repositoryId => !accessScope.Contains(repositoryId)) ||
            !await accessScope.HasCurrentAccessAsync(repositoryIds, cancellationToken))
        {
            return ChatCurrentnessValidationOutcome.MissingOrStale;
        }

        IExecutionStrategy strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
            try
            {
                Guid[] logicalDocumentIds = request.Documents
                    .Select(document => document.LogicalDocumentId)
                    .ToArray();
                CurrentDocumentRow[] documents = await (
                        from logical in _context.LogicalDocuments.AsNoTracking()
                        join version in _context.DocumentVersions.AsNoTracking()
                            on logical.CurrentVersionId equals version.Id
                        join scanRun in _context.ScanRuns.AsNoTracking()
                            on version.ScanRunId equals scanRun.Id
                        where logicalDocumentIds.Contains(logical.Id) &&
                              repositoryIds.Contains(logical.RepositoryId) &&
                              version.SystemId == request.SystemId &&
                              version.SupersededById == null
                        select new CurrentDocumentRow(
                            logical.Id,
                            version.Id,
                            version.RepositoryId,
                            version.ScanRunId,
                            version.ProfiledRevision,
                            version.Title,
                            scanRun.RepositoryId,
                            scanRun.Status,
                            scanRun.ProfiledRevision))
                    .Take(6)
                    .ToArrayAsync(cancellationToken);
                bool documentsAreCurrent = documents.Length == request.Documents.Count &&
                                           request.Documents.All(expected => documents.Any(actual =>
                                               actual.LogicalDocumentId == expected.LogicalDocumentId &&
                                               actual.DocumentVersionId == expected.ExpectedCurrentDocumentVersionId &&
                                               string.Equals(
                                                   actual.Title,
                                                   expected.ExpectedTitle,
                                                   StringComparison.Ordinal) &&
                                               actual.ScanRunRepositoryId == actual.RepositoryId &&
                                               actual.ScanRunStatus == ScanRunStatus.Completed &&
                                               string.Equals(
                                                   actual.ScanRunProfiledRevision,
                                                   actual.ProfiledRevision,
                                                   StringComparison.Ordinal)));
                bool sectionsAreCurrent = documentsAreCurrent &&
                                          await ValidateSectionsAsync(request, cancellationToken);
                bool anchorsAreCurrent = sectionsAreCurrent &&
                                         await ValidateAnchorsAsync(
                                             request,
                                             repositoryIds,
                                             documents,
                                             cancellationToken);
                bool relationshipsAreCurrent = anchorsAreCurrent &&
                                               await ValidateRelationshipsAsync(
                                                   request,
                                                   repositoryIds,
                                                   documents,
                                                   cancellationToken);
                bool flowGroupsAreCurrent = relationshipsAreCurrent &&
                                            await ValidateFlowGroupsAsync(
                                                request,
                                                documents,
                                                cancellationToken);
                if (!flowGroupsAreCurrent ||
                    !await accessScope.HasCurrentAccessAsync(repositoryIds, cancellationToken))
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    _context.ChangeTracker.Clear();
                    return ChatCurrentnessValidationOutcome.MissingOrStale;
                }

                await transaction.CommitAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                return ChatCurrentnessValidationOutcome.Current;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private async Task<bool> ValidateSectionsAsync(
        ChatCurrentnessValidationRequest request,
        CancellationToken cancellationToken)
    {
        Guid[] stableSectionIds = request.Sections
            .Select(section => section.StableSectionId)
            .Distinct()
            .ToArray();
        CurrentSectionRow[] sections = await _context.DocumentSections
            .AsNoTracking()
            .Where(section => stableSectionIds.Contains(section.StableSectionId))
            .Select(section => new CurrentSectionRow(
                section.StableSectionId,
                section.DocumentVersionId,
                section.ContentMarkdown))
            .Take(21)
            .ToArrayAsync(cancellationToken);
        int requestedSectionCount = request.Sections
            .Select(section => (section.StableSectionId, section.DocumentVersionId))
            .Distinct()
            .Count();
        return sections.Length == requestedSectionCount &&
               request.Sections.All(expected => sections.Any(actual =>
                   actual.StableSectionId == expected.StableSectionId &&
                   actual.DocumentVersionId == expected.DocumentVersionId &&
                   SpanMatches(actual.ContentMarkdown, expected)));
    }

    private async Task<bool> ValidateAnchorsAsync(
        ChatCurrentnessValidationRequest request,
        IReadOnlyCollection<Guid> repositoryIds,
        IReadOnlyCollection<CurrentDocumentRow> documents,
        CancellationToken cancellationToken)
    {
        Guid[] anchorIds = request.SourceAnchors
            .Select(anchor => anchor.AnchorId)
            .ToArray();
        CurrentAnchorRow[] anchors = await _context.SourceAnchors
            .AsNoTracking()
            .Where(anchor => anchorIds.Contains(anchor.Id))
            .Select(anchor => new CurrentAnchorRow(
                anchor.Id,
                anchor.DocumentVersionId,
                anchor.RepositoryId,
                anchor.ScanRunId,
                anchor.ProfiledRevision,
                anchor.RepositoryRelativePath,
                anchor.LineStart,
                anchor.LineEnd,
                anchor.SourceHash))
            .Take(33)
            .ToArrayAsync(cancellationToken);
        return anchors.Length == request.SourceAnchors.Count &&
               request.SourceAnchors.All(expected => anchors.Any(actual =>
                   actual.AnchorId == expected.AnchorId &&
                   actual.DocumentVersionId == expected.DocumentVersionId &&
                   actual.RepositoryId == expected.RepositoryId &&
                   documents.Any(document =>
                       document.DocumentVersionId == actual.DocumentVersionId &&
                       document.RepositoryId == actual.RepositoryId &&
                       document.ScanRunId == actual.ScanRunId &&
                       string.Equals(
                           document.ProfiledRevision,
                           actual.ProfiledRevision,
                           StringComparison.Ordinal)) &&
                   repositoryIds.Contains(actual.RepositoryId) &&
                   string.Equals(
                       NormalizePath(actual.RepositoryRelativePath),
                       expected.RepositoryRelativePath,
                       StringComparison.Ordinal) &&
                   IsSafeRepositoryRelativePath(actual.RepositoryRelativePath) &&
                   actual.LineStart > 0 &&
                   actual.LineEnd >= actual.LineStart &&
                   actual.LineStart == expected.LineStart &&
                   actual.LineEnd == expected.LineEnd &&
                   string.Equals(actual.SourceHash, expected.SourceHash, StringComparison.Ordinal) &&
                   IsLowerHexSha256(actual.SourceHash)));
    }

    private async Task<bool> ValidateRelationshipsAsync(
        ChatCurrentnessValidationRequest request,
        IReadOnlyCollection<Guid> repositoryIds,
        IReadOnlyCollection<CurrentDocumentRow> documents,
        CancellationToken cancellationToken)
    {
        Guid[] expectedEdgeIds = request.Relationships
            .Select(relationship => relationship.EdgeId)
            .ToArray();
        DocumentEdge[] relationships = await _context.DocumentEdges
            .AsNoTracking()
            .Where(edge => expectedEdgeIds.Contains(edge.Id))
            .Take(33)
            .ToArrayAsync(cancellationToken);
        Guid[] scanRunIds = documents
            .Select(document => document.ScanRunId)
            .Distinct()
            .ToArray();
        CurrentScanRunRow[] scanRuns = await _context.ScanRuns
            .AsNoTracking()
            .Where(scanRun => scanRunIds.Contains(scanRun.Id))
            .Select(scanRun => new CurrentScanRunRow(
                scanRun.Id,
                scanRun.ProfiledRevision))
            .Take(6)
            .ToArrayAsync(cancellationToken);

        Guid[] edgeIds = relationships.Select(relationship => relationship.Id).ToArray();
        CrossRepositoryResolutionDecision[] exactDecisions = await _context
            .CrossRepositoryResolutionDecisions
            .AsNoTracking()
            .Where(decision =>
                decision.DocumentEdgeId.HasValue &&
                edgeIds.Contains(decision.DocumentEdgeId.Value))
            .Take(257)
            .ToArrayAsync(cancellationToken);
        if (exactDecisions.Length > 256)
        {
            return false;
        }

        Guid[] decisionIds = exactDecisions.Select(decision => decision.Id).ToArray();
        ExactResolutionProviderEvidenceContribution[] contributions = await _context
            .ExactResolutionProviderEvidenceContributions
            .AsNoTracking()
            .Where(contribution => decisionIds.Contains(contribution.ResolutionDecisionId))
            .Take(65_537)
            .ToArrayAsync(cancellationToken);
        if (contributions.Length > 65_536 ||
            exactDecisions.Any(decision =>
            {
                int count = contributions.Count(contribution =>
                    contribution.ResolutionDecisionId == decision.Id);
                return count is < 1 or > 256;
            }))
        {
            return false;
        }

        FlowGroup[] exactFlowGroups = await _context.FlowGroups
            .AsNoTracking()
            .Where(group =>
                group.ExactResolutionEdgeId.HasValue &&
                edgeIds.Contains(group.ExactResolutionEdgeId.Value))
            .Take(11)
            .ToArrayAsync(cancellationToken);
        if (exactFlowGroups.Length > 10)
        {
            return false;
        }

        return request.Relationships.All(expected => expected.RelationshipType switch
        {
            ChatRelationshipType.References => relationships.Any(edge =>
                edge.Id == expected.EdgeId &&
                edge.EdgeType == DocumentEdgeType.References &&
                MatchesEndpoints(edge, expected, repositoryIds) &&
                MatchesRelationshipFlowGroup(request, expected) &&
                (expected.SourceRepositoryId == expected.TargetRepositoryId
                    ? expected.ExactDecisionId is null &&
                      expected.ProfileFingerprint is null &&
                      MatchesGeneratedFromEndpointAuthority(
                          edge,
                          expected,
                          documents,
                          scanRuns)
                    : HasAcceptedExactDecision(
                        request,
                        request.SystemId,
                        expected,
                        edge,
                        documents,
                        exactDecisions,
                        contributions,
                        exactFlowGroups))),
            ChatRelationshipType.GeneratedFrom => relationships.Any(edge =>
                edge.Id == expected.EdgeId &&
                edge.EdgeType == DocumentEdgeType.GeneratedFrom &&
                MatchesEndpoints(edge, expected, repositoryIds) &&
                MatchesGeneratedFromEndpointAuthority(
                    edge,
                    expected,
                    documents,
                    scanRuns) &&
                MatchesRelationshipFlowGroup(request, expected)),
            _ => false,
        });
    }

    private static bool MatchesRelationshipFlowGroup(
        ChatCurrentnessValidationRequest request,
        ChatCurrentRelationship expected) =>
        expected.FlowGroupId is not Guid flowGroupId ||
        request.FlowGroups.Any(group =>
            group.FlowGroupId == flowGroupId &&
            group.ExactResolutionEdgeId == expected.EdgeId &&
            group.ExactResolutionDecisionId == expected.ExactDecisionId);

    private static bool MatchesGeneratedFromEndpointAuthority(
        DocumentEdge edge,
        ChatCurrentRelationship expected,
        IReadOnlyCollection<CurrentDocumentRow> documents,
        IReadOnlyCollection<CurrentScanRunRow> scanRuns)
    {
        CurrentDocumentRow? source = documents.SingleOrDefault(document =>
            document.DocumentVersionId == expected.SourceDocumentVersionId);
        CurrentDocumentRow? target = documents.SingleOrDefault(document =>
            document.DocumentVersionId == expected.TargetDocumentVersionId);
        return source is not null &&
               target is not null &&
               edge.SourceScanRunId == source.ScanRunId &&
               edge.TargetScanRunId == target.ScanRunId &&
               scanRuns.Any(scanRun =>
                   scanRun.ScanRunId == source.ScanRunId &&
                   string.Equals(
                       scanRun.ProfiledRevision,
                       source.ProfiledRevision,
                       StringComparison.Ordinal)) &&
               scanRuns.Any(scanRun =>
                   scanRun.ScanRunId == target.ScanRunId &&
                   string.Equals(
                       scanRun.ProfiledRevision,
                       target.ProfiledRevision,
                       StringComparison.Ordinal));
    }

    private async Task<bool> ValidateFlowGroupsAsync(
        ChatCurrentnessValidationRequest request,
        IReadOnlyCollection<CurrentDocumentRow> documents,
        CancellationToken cancellationToken)
    {
        if (request.FlowGroups.Count == 0)
        {
            return true;
        }

        Guid[] flowGroupIds = request.FlowGroups.Select(group => group.FlowGroupId).ToArray();
        FlowCurrentnessRow[] rows = await (
            from flowGroup in _context.FlowGroups.AsNoTracking()
            join trace in _context.FlowTraces.AsNoTracking()
                on flowGroup.Id equals trace.FlowGroupId
            join step in _context.FlowTraceSteps.AsNoTracking()
                on trace.Id equals step.FlowTraceId
            where flowGroupIds.Contains(flowGroup.Id)
            orderby flowGroup.Id, trace.TraceOrder, step.StepOrder, step.Id
            select new FlowCurrentnessRow(
                flowGroup.Id,
                flowGroup.SchemaIdentity!,
                flowGroup.State,
                flowGroup.SystemId,
                flowGroup.RepositoryId,
                flowGroup.ScanRunId,
                flowGroup.BusinessLogicDocumentVersionId,
                flowGroup.ExactResolutionDecisionId,
                flowGroup.ExactResolutionEdgeId,
                flowGroup.PlanFingerprint,
                flowGroup.MembershipFingerprint!,
                trace.Id,
                trace.TraceOrder,
                step.Id,
                step.StepOrder,
                step.StepKind,
                step.Description,
                step.DocumentVersionId,
                step.DocumentRepositoryId,
                step.DocumentScanRunId,
                step.EdgeId,
                step.ExactResolutionDecisionId))
            .Take(13)
            .ToArrayAsync(cancellationToken);
        if (rows.Length is < 1 or > 12)
        {
            return false;
        }

        Guid[] decisionIds = request.FlowGroups
            .Select(group => group.ExactResolutionDecisionId)
            .Where(decisionId => decisionId.HasValue)
            .Select(decisionId => decisionId!.Value)
            .Distinct()
            .ToArray();
        CrossRepositoryResolutionDecision[] decisions = await _context
            .CrossRepositoryResolutionDecisions
            .AsNoTracking()
            .Where(decision => decisionIds.Contains(decision.Id))
            .Take(11)
            .ToArrayAsync(cancellationToken);

        return request.FlowGroups.All(expected =>
        {
            FlowCurrentnessRow[] actual = rows
                .Where(row => row.FlowGroupId == expected.FlowGroupId)
                .ToArray();
            return actual.Length == expected.Steps.Count &&
                   actual.All(row =>
                       string.Equals(row.SchemaIdentity, expected.SchemaIdentity, StringComparison.Ordinal) &&
                       string.Equals(row.State, expected.State, StringComparison.Ordinal) &&
                       row.SystemId == request.SystemId &&
                       row.BusinessLogicDocumentVersionId == expected.BusinessLogicDocumentVersionId &&
                       row.ExactResolutionDecisionId == expected.ExactResolutionDecisionId &&
                       row.ExactResolutionEdgeId == expected.ExactResolutionEdgeId &&
                       string.Equals(row.PlanFingerprint, expected.PlanFingerprint, StringComparison.Ordinal) &&
                       string.Equals(row.MembershipFingerprint, expected.MembershipFingerprint, StringComparison.Ordinal) &&
                       row.FlowTraceId == expected.FlowTraceId &&
                       row.FlowTraceOrder == expected.FlowTraceOrder) &&
                   expected.Steps.All(step => actual.Any(row => MatchesFlowStep(row, step))) &&
                   documents.Any(document =>
                       document.DocumentVersionId == expected.BusinessLogicDocumentVersionId &&
                       document.RepositoryId == actual[0].RepositoryId &&
                       document.ScanRunId == actual[0].ScanRunId) &&
                   (expected.ExactResolutionDecisionId is null
                       ? request.Relationships.Any(relationship =>
                           relationship.EdgeId == expected.ExactResolutionEdgeId &&
                           relationship.RelationshipType == ChatRelationshipType.References &&
                           relationship.SourceRepositoryId == relationship.TargetRepositoryId &&
                           relationship.ExactDecisionId is null)
                       : decisions.Any(decision =>
                        decision.Id == expected.ExactResolutionDecisionId &&
                        decision.DocumentEdgeId == expected.ExactResolutionEdgeId));
        });
    }

    private static bool MatchesFlowStep(
        FlowCurrentnessRow actual,
        EvidenceFlowStepAuthority expected) =>
        actual.FlowTraceStepId == expected.FlowTraceStepId &&
        actual.StepOrder == expected.StepOrder &&
        actual.StepKind == expected.StepKind &&
        string.Equals(actual.Description, expected.Description, StringComparison.Ordinal) &&
        actual.DocumentVersionId == expected.DocumentVersionId &&
        actual.DocumentRepositoryId == expected.DocumentRepositoryId &&
        actual.DocumentScanRunId == expected.DocumentScanRunId &&
        actual.EdgeId == expected.EdgeId &&
        actual.StepExactResolutionDecisionId == expected.ExactResolutionDecisionId;

    private static bool HasAcceptedExactDecision(
        ChatCurrentnessValidationRequest request,
        Guid systemId,
        ChatCurrentRelationship expected,
        DocumentEdge edge,
        IReadOnlyCollection<CurrentDocumentRow> documents,
        IReadOnlyCollection<CrossRepositoryResolutionDecision> decisions,
        IReadOnlyCollection<ExactResolutionProviderEvidenceContribution> contributions,
        IReadOnlyCollection<FlowGroup> flowGroups)
    {
        CrossRepositoryResolutionDecision[] edgeDecisions = decisions
            .Where(decision => decision.DocumentEdgeId == edge.Id)
            .ToArray();
        ExactResolutionProviderEvidenceContribution[] edgeContributions = contributions
            .Where(contribution => edgeDecisions.Any(decision =>
                decision.Id == contribution.ResolutionDecisionId))
            .ToArray();
        if (!ChatExactDecisionSetAuthority.TryCreate(
                edge,
                edgeDecisions,
                edgeContributions,
                out ChatExactDecisionSet decisionSet) ||
            decisionSet.SelectedDecision.Id != expected.ExactDecisionId ||
            expected.ExactDecisionCount > 0 &&
                (decisionSet.Count != expected.ExactDecisionCount ||
                 !string.Equals(
                     decisionSet.Fingerprint,
                     expected.ExactDecisionSetFingerprint,
                     StringComparison.Ordinal)) ||
            decisionSet.Decisions.Any(decision =>
                decision.SystemId != systemId ||
                decision.ConsumerDocumentVersionId != expected.SourceDocumentVersionId ||
                decision.ConsumerRepositoryId != expected.SourceRepositoryId ||
                decision.ProviderDocumentVersionId != expected.TargetDocumentVersionId ||
                decision.ProviderRepositoryId != expected.TargetRepositoryId))
        {
            return false;
        }

        CrossRepositoryResolutionDecision decision = decisionSet.SelectedDecision;
        CurrentDocumentRow? consumer = documents.SingleOrDefault(document =>
            document.DocumentVersionId == expected.SourceDocumentVersionId);
        CurrentDocumentRow? provider = documents.SingleOrDefault(document =>
            document.DocumentVersionId == expected.TargetDocumentVersionId);
        Guid[] setDecisionIds = decisionSet.Decisions
            .Select(item => item.Id)
            .ToArray();
        FlowGroup[] decisionFlowGroups = flowGroups
            .Where(group =>
                group.ExactResolutionEdgeId == edge.Id &&
                group.ExactResolutionDecisionId.HasValue &&
                setDecisionIds.Contains(group.ExactResolutionDecisionId.Value))
            .ToArray();
        EvidenceFlowGroupAuthority[] requestedFlowGroups = request.FlowGroups
            .Where(group =>
                group.ExactResolutionEdgeId == expected.EdgeId &&
                group.ExactResolutionDecisionId.HasValue &&
                decisionSet.Decisions.Any(item =>
                    item.Id == group.ExactResolutionDecisionId.Value))
            .ToArray();

        return consumer is not null &&
               provider is not null &&
               consumer.RepositoryId == decision.ConsumerRepositoryId &&
               consumer.ScanRunId == decision.ConsumerScanRunId &&
               string.Equals(
                   consumer.ProfiledRevision,
                   decision.ConsumerProfiledRevision,
                   StringComparison.Ordinal) &&
               provider.RepositoryId == decision.ProviderRepositoryId &&
               provider.ScanRunId == decision.ProviderScanRunId &&
               string.Equals(
                   provider.ProfiledRevision,
                   decision.ProviderProfiledRevision,
                   StringComparison.Ordinal) &&
               (expected.ProfileFingerprint is null ||
                string.Equals(
                    decision.ProviderEvidenceSetFingerprint,
                    expected.ProfileFingerprint,
                    StringComparison.Ordinal)) &&
               requestedFlowGroups.All(requested =>
                   decisionFlowGroups.Any(group =>
                       group.Id == requested.FlowGroupId &&
                       group.ExactResolutionDecisionId == requested.ExactResolutionDecisionId &&
                       string.Equals(group.State, "Published", StringComparison.Ordinal) &&
                       group.BusinessLogicDocumentVersionId.HasValue &&
                       group.SystemId == systemId)) &&
               (expected.FlowGroupId is null && requestedFlowGroups.Length == 0 ||
                expected.FlowGroupId is Guid expectedFlowGroupId &&
                requestedFlowGroups.Any(group => group.FlowGroupId == expectedFlowGroupId));
    }

    private static bool SpanMatches(
        string contentMarkdown,
        ChatCurrentSection expected)
    {
        long spanEnd = (long)expected.SpanStart + expected.SpanLength;
        return spanEnd <= contentMarkdown.Length &&
               string.Equals(
                   Sha256(contentMarkdown.Substring(expected.SpanStart, expected.SpanLength)),
                   expected.TextSha256,
                   StringComparison.Ordinal);
    }

    private static bool MatchesEndpoints(
        DocumentEdge edge,
        ChatCurrentRelationship expected,
        IReadOnlyCollection<Guid> repositoryIds) =>
        edge.SourceDocumentVersionId == expected.SourceDocumentVersionId &&
        edge.TargetDocumentVersionId == expected.TargetDocumentVersionId &&
        edge.SourceRepositoryId == expected.SourceRepositoryId &&
        edge.TargetRepositoryId == expected.TargetRepositoryId &&
        repositoryIds.Contains(edge.SourceRepositoryId) &&
        repositoryIds.Contains(edge.TargetRepositoryId);

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool IsLowerHexSha256(string value) =>
        value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string NormalizePath(string value) => value.Replace('\\', '/');

    private static bool IsSafeRepositoryRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = NormalizePath(value);
        string[] segments = normalized.Split('/');
        return !normalized.StartsWith('/') &&
               !(normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':') &&
               !Uri.TryCreate(normalized, UriKind.Absolute, out _) &&
               segments.All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    private sealed record CurrentDocumentRow(
        Guid LogicalDocumentId,
        Guid DocumentVersionId,
        Guid RepositoryId,
        Guid ScanRunId,
        string ProfiledRevision,
        string Title,
        Guid ScanRunRepositoryId,
        ScanRunStatus ScanRunStatus,
        string? ScanRunProfiledRevision);

    private sealed record CurrentSectionRow(
        Guid StableSectionId,
        Guid DocumentVersionId,
        string ContentMarkdown);

    private sealed record CurrentScanRunRow(
        Guid ScanRunId,
        string? ProfiledRevision);

    private sealed record CurrentAnchorRow(
        Guid AnchorId,
        Guid DocumentVersionId,
        Guid RepositoryId,
        Guid ScanRunId,
        string ProfiledRevision,
        string RepositoryRelativePath,
        int LineStart,
        int LineEnd,
        string SourceHash);

    private sealed record FlowCurrentnessRow(
        Guid FlowGroupId,
        string SchemaIdentity,
        string State,
        Guid? SystemId,
        Guid RepositoryId,
        Guid ScanRunId,
        Guid? BusinessLogicDocumentVersionId,
        Guid? ExactResolutionDecisionId,
        Guid? ExactResolutionEdgeId,
        string PlanFingerprint,
        string MembershipFingerprint,
        Guid FlowTraceId,
        int FlowTraceOrder,
        Guid FlowTraceStepId,
        int StepOrder,
        FlowTraceStepKind StepKind,
        string Description,
        Guid DocumentVersionId,
        Guid DocumentRepositoryId,
        Guid DocumentScanRunId,
        Guid? EdgeId,
        Guid? StepExactResolutionDecisionId);

}
