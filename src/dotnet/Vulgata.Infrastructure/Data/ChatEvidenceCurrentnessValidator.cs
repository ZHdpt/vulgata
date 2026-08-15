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
                        where logicalDocumentIds.Contains(logical.Id) &&
                              repositoryIds.Contains(logical.RepositoryId) &&
                              version.SystemId == request.SystemId &&
                              version.SupersededById == null
                        select new CurrentDocumentRow(
                            logical.Id,
                            version.Id))
                    .Take(6)
                    .ToArrayAsync(cancellationToken);
                bool documentsAreCurrent = documents.Length == request.Documents.Count &&
                                           request.Documents.All(expected => documents.Any(actual =>
                                               actual.LogicalDocumentId == expected.LogicalDocumentId &&
                                               actual.DocumentVersionId == expected.ExpectedCurrentDocumentVersionId));
                bool sectionsAreCurrent = documentsAreCurrent &&
                                          await ValidateSectionsAsync(request, cancellationToken);
                bool anchorsAreCurrent = sectionsAreCurrent &&
                                         await ValidateAnchorsAsync(request, repositoryIds, cancellationToken);
                bool relationshipsAreCurrent = anchorsAreCurrent &&
                                               await ValidateRelationshipsAsync(request, repositoryIds, cancellationToken);
                if (!relationshipsAreCurrent ||
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
        CancellationToken cancellationToken)
    {
        Guid[] documentVersionIds = request.Documents
            .Select(document => document.ExpectedCurrentDocumentVersionId)
            .ToArray();
        CurrentRelationshipRow[] relationships = await _context.DocumentEdges
            .AsNoTracking()
            .Where(edge =>
                documentVersionIds.Contains(edge.SourceDocumentVersionId) &&
                documentVersionIds.Contains(edge.TargetDocumentVersionId))
            .Select(edge => new CurrentRelationshipRow(
                edge.Id,
                edge.EdgeType,
                edge.SourceDocumentVersionId,
                edge.TargetDocumentVersionId,
                edge.SourceRepositoryId,
                edge.TargetRepositoryId))
            .Take(33)
            .ToArrayAsync(cancellationToken);
        Guid[] edgeIds = relationships.Select(relationship => relationship.EdgeId).ToArray();
        ExactRelationshipRow[] exactRelationships = await _context.ExactCrossRepositoryEdgeEvidence
            .AsNoTracking()
            .Where(exact => edgeIds.Contains(exact.EdgeId))
            .Select(exact => new ExactRelationshipRow(
                exact.EdgeId,
                exact.ConsumerDocumentVersionId,
                exact.ConsumerRepositoryId,
                exact.ProviderDocumentVersionId,
                exact.ProviderRepositoryId,
                exact.ProfileFingerprint,
                exact.EvidenceFingerprint))
            .Take(33)
            .ToArrayAsync(cancellationToken);

        return request.Relationships.All(expected => expected.RelationshipType switch
        {
            ChatRelationshipType.References => relationships.Any(edge =>
                edge.EdgeType == DocumentEdgeType.References &&
                MatchesEndpoints(edge, expected, repositoryIds) &&
                exactRelationships.Any(exact =>
                    exact.EdgeId == edge.EdgeId &&
                    exact.ConsumerDocumentVersionId == expected.SourceDocumentVersionId &&
                    exact.ConsumerRepositoryId == expected.SourceRepositoryId &&
                    exact.ProviderDocumentVersionId == expected.TargetDocumentVersionId &&
                    exact.ProviderRepositoryId == expected.TargetRepositoryId &&
                    string.Equals(
                        exact.ProfileFingerprint,
                        expected.ProfileFingerprint,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        exact.EvidenceFingerprint,
                        expected.RelationshipIdentity,
                        StringComparison.Ordinal))),
            ChatRelationshipType.GeneratedFrom => relationships.Any(edge =>
                edge.EdgeType == DocumentEdgeType.GeneratedFrom &&
                MatchesEndpoints(edge, expected, repositoryIds) &&
                string.Equals(
                    StableIdentity(edge.EdgeId),
                    expected.RelationshipIdentity,
                    StringComparison.Ordinal)),
            _ => false,
        });
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
        CurrentRelationshipRow edge,
        ChatCurrentRelationship expected,
        IReadOnlyCollection<Guid> repositoryIds) =>
        edge.SourceDocumentVersionId == expected.SourceDocumentVersionId &&
        edge.TargetDocumentVersionId == expected.TargetDocumentVersionId &&
        edge.SourceRepositoryId == expected.SourceRepositoryId &&
        edge.TargetRepositoryId == expected.TargetRepositoryId &&
        repositoryIds.Contains(edge.SourceRepositoryId) &&
        repositoryIds.Contains(edge.TargetRepositoryId);

    private static string StableIdentity(Guid edgeId) =>
        Sha256(edgeId.ToString("D"));

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
        Guid DocumentVersionId);

    private sealed record CurrentSectionRow(
        Guid StableSectionId,
        Guid DocumentVersionId,
        string ContentMarkdown);

    private sealed record CurrentAnchorRow(
        Guid AnchorId,
        Guid DocumentVersionId,
        Guid RepositoryId,
        string RepositoryRelativePath,
        int LineStart,
        int LineEnd,
        string SourceHash);

    private sealed record CurrentRelationshipRow(
        Guid EdgeId,
        DocumentEdgeType EdgeType,
        Guid SourceDocumentVersionId,
        Guid TargetDocumentVersionId,
        Guid SourceRepositoryId,
        Guid TargetRepositoryId);

    private sealed record ExactRelationshipRow(
        Guid EdgeId,
        Guid ConsumerDocumentVersionId,
        Guid ConsumerRepositoryId,
        Guid ProviderDocumentVersionId,
        Guid ProviderRepositoryId,
        string ProfileFingerprint,
        string EvidenceFingerprint);
}
