using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;

namespace Vulgata.Tests.October.Integration;

/// <summary>
/// Partial assertion authoring only: no discoverable test, Generate orchestration, resource acquisition or approval bootstrap.
/// An authorized caller must provide the actual final publication and already authenticated ordinary reader capture.
/// These checks establish exact Business reading, not source-semantic correctness or browser/circuit qualification.
/// </summary>
public sealed class OctoberGeneratedBlReaderE2ETests
{
    // Total canonical page traffic per assertion helper, including denial pages; not production read policy.
    // Exact publication snapshots use the owner's separate stored-read bounds, not the public page cap.
    private const int MaximumPages = 128;
    private const long MaximumAggregateBytes = 8 * 1024 * 1024;
    // Test-fixture safeguards separate from graph/page limits and production policy. Reference observations
    // are charged across BOTH dependency snapshots for the entire positive assertion helper.
    private const int MaximumBusinessDocuments = 128;
    private const int MaximumDependencyReferences = 4096;

    internal sealed record ReconstructedBusinessStream(DocumentHandle Document, string Content,
        ImmutableArray<string> OrderedClaimTexts, int Pages, ContinuationToken? FirstContinuation);

    /// <summary>
    /// Borrows the caller's successfully opened actual ordinary Business session. The caller holds it through
    /// verification and actual Identity revocation, then disposes it after the revocation assertions.
    /// </summary>
    internal static async Task<ImmutableArray<ReconstructedBusinessStream>> AssertBusinessPublicationAsync(
        OctoberHostSelectionTests.GeneratorPrepareFixture fixture, PublicationBinding publication,
        OctoberHostSelectionTests.GeneratorPrepareFixture.OrdinaryReaderSession session,
        CancellationToken cancellationToken)
    {
        var original = await fixture.LoadExactPublicationAsync(publication, cancellationToken);
        Assert.Equal(publication, original.Publication);
        var expected = original.Documents.Where(document => document.Kind == DocumentKind.BusinessLogic)
            .ToDictionary(document => Handle(publication, document.Reference));
        Assert.NotEmpty(expected);
        Assert.InRange(expected.Count, 1, MaximumBusinessDocuments);
        Assert.Equal(original.Documents.Length, original.Documents.Select(document => document.Reference).Distinct().Count());
        var dependencyBudget = new DependencyObservationBudget();
        var originalDependencies = await CaptureDependenciesAsync(fixture, publication, expected.Keys, dependencyBudget, cancellationToken);
        var budget = new VerificationBudget();
        Assert.NotNull(session);
        Assert.NotNull(session.Selection);

        var seen = new Dictionary<DocumentHandle, string>();
        var searchCursors = new HashSet<string>(StringComparer.Ordinal);
        ContinuationToken? cursor = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await session.SearchAsync("", 20, cursor, cancellationToken);
            budget.Admit(page);
            Assert.Equal(KnowledgeReadStatus.Success, page.Status);
            Assert.InRange(page.Documents.Length, 0, 20);
            foreach (var summary in page.Documents)
            {
                Assert.Equal(DocumentKind.BusinessLogic, summary.Kind);
                Assert.True(expected.TryGetValue(summary.Handle, out var document), "Business browse returned an unexpected exact handle.");
                Assert.Equal(document!.Title, summary.Title);
                Assert.True(seen.TryAdd(summary.Handle, summary.Title), "Business browse repeated an exact Document.");
                Assert.All(summary.QualifiedExcerpt, claim => Assert.Empty(claim.Evidence));
            }
            if (page.Next is null) { break; }
            Assert.True(searchCursors.Add(page.Next.Value), "Business browse repeated a continuation.");
            cursor = page.Next;
        }
        Assert.True(expected.Keys.ToHashSet().SetEquals(seen.Keys), "Complete Business browse must match the actual published BL membership.");

        var reconstructed = ImmutableArray.CreateBuilder<ReconstructedBusinessStream>();
        foreach (var pair in expected.OrderBy(pair => pair.Key.LogicalDocumentId))
        {
            var document = pair.Value;
            Assert.Equal(document.Sections.Length, document.Sections.Select(section => section.SectionId).Distinct().Count());
            reconstructed.Add(await AssertStreamAsync(pair.Key, document, null,
                (handle, continuation, token) => session.ReadExactAsync(handle, continuation, token), budget, cancellationToken));
            foreach (var section in document.Sections.OrderBy(section => section.Ordinal))
            {
                reconstructed.Add(await AssertStreamAsync(pair.Key with { SectionId = section.SectionId }, document, section.SectionId,
                    (handle, continuation, token) => session.ReadExactAsync(handle, continuation, token), budget, cancellationToken));
            }
        }

        // Positive complete Business reads above prevent an unavailable service from satisfying the denial checks.
        var actualCl = original.Documents.Where(document => document.Kind == DocumentKind.CodeLogic).ToArray();
        Assert.NotEmpty(actualCl);
        foreach (var document in actualCl)
        {
            var denied = await session.ReadExactAsync(Handle(publication, document.Reference), null, cancellationToken);
            budget.Admit(denied);
            AssertUnavailable(denied);
        }
        var valid = expected.Keys.First();
        foreach (var wrong in new[]
        {
            valid with { BatchId = Guid.NewGuid() }, valid with { PublicationId = Guid.NewGuid() },
            valid with { LogicalDocumentId = Guid.NewGuid() }, valid with { DocumentVersionId = Guid.NewGuid() },
            valid with { SectionId = "absent-" + Guid.NewGuid().ToString("N") },
        })
        {
            var denied = await session.ReadExactAsync(wrong, null, cancellationToken);
            budget.Admit(denied);
            AssertUnavailable(denied);
        }

        var retained = await fixture.LoadExactPublicationAsync(publication, cancellationToken);
        Assert.Equal(publication, retained.Publication);
        AssertUnchangedDocuments(original.Documents, retained.Documents);
        var retainedDependencies = await CaptureDependenciesAsync(fixture, publication, expected.Keys, dependencyBudget, cancellationToken);
        foreach (var pair in originalDependencies)
        {
            Assert.True(pair.Value.SetEquals(retainedDependencies[pair.Key]),
                "Exact Business Document dependencies changed during reads.");
        }
        return reconstructed.ToImmutable();
    }

    private static async Task<Dictionary<DocumentHandle, HashSet<DocumentVersionReference>>> CaptureDependenciesAsync(
        OctoberHostSelectionTests.GeneratorPrepareFixture fixture, PublicationBinding publication,
        IEnumerable<DocumentHandle> businessDocuments, DependencyObservationBudget budget, CancellationToken cancellationToken)
    {
        var result = new Dictionary<DocumentHandle, HashSet<DocumentVersionReference>>();
        foreach (var document in businessDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Null(document.SectionId);
            var dependencies = await fixture.LoadExactBusinessDependenciesAsync(publication, document, cancellationToken);
            Assert.False(dependencies.IsDefaultOrEmpty, "A published Business Document must retain its exact dependencies.");
            budget.Admit(dependencies.Length);
            var references = dependencies.ToHashSet();
            Assert.Equal(dependencies.Length, references.Count);
            Assert.All(dependencies, dependency =>
            {
                Assert.Equal(publication.Knowledge.BatchId, dependency.BatchId);
                Assert.NotEqual(Guid.Empty, dependency.LogicalDocumentId);
                Assert.NotEqual(Guid.Empty, dependency.DocumentVersionId);
            });
            // Keep every owner-returned reference, including supplemental nonmembers. Neither Business
            // visibility nor final-publication membership is a requirement, and no graph Edge is inferred.
            result.Add(document, references);
        }
        return result;
    }

    private sealed class DependencyObservationBudget
    {
        private int _observations;
        internal void Admit(int count)
        {
            Assert.InRange(count, 1, MaximumDependencyReferences - _observations);
            _observations += count;
        }
    }

    /// <summary>
    /// The caller rotates/revokes actual Identity first and supplies the SAME borrowed session used above.
    /// No login, new stamp or new session is allowed; the caller disposes the session after these assertions.
    /// </summary>
    internal static async Task AssertRevokedBusinessReaderAsync(
        OctoberHostSelectionTests.GeneratorPrepareFixture.OrdinaryReaderSession session,
        PublicationBinding publication, ImmutableArray<ReconstructedBusinessStream> previouslyReadStreams,
        CancellationToken cancellationToken)
    {
        Assert.NotNull(session);
        Assert.False(previouslyReadStreams.IsDefaultOrEmpty, "Revocation requires the successful stream observations.");
        Assert.Equal(previouslyReadStreams.Length, previouslyReadStreams.Select(stream => stream.Document).Distinct().Count());
        var budget = new VerificationBudget();
        foreach (var stream in previouslyReadStreams)
        {
            Assert.True(stream.Pages > 0, "Revocation requires a previously successful exact stream.");
            Assert.Equal(publication.PublicationId, stream.Document.PublicationId);
            Assert.Equal(publication.Knowledge.BatchId, stream.Document.BatchId);
            var content = await session.ReadExactAsync(stream.Document, null, cancellationToken);
            budget.Admit(content);
            AssertUnavailable(content);
            if (stream.FirstContinuation is { } continuation)
            {
                var continued = await session.ReadExactAsync(stream.Document, continuation, cancellationToken);
                budget.Admit(continued);
                AssertUnavailable(continued);
            }
        }
        var browse = await session.SearchAsync("", 20, null, cancellationToken);
        budget.Admit(browse);
        Assert.Equal(KnowledgeReadStatus.Unavailable, browse.Status);
        Assert.Empty(browse.Documents);
        Assert.Null(browse.Next);
        Assert.Null(browse.Coverage);
    }

    private static async Task<ReconstructedBusinessStream> AssertStreamAsync(DocumentHandle handle,
        OctoberPublishedDocument document, string? sectionId,
        Func<DocumentHandle, ContinuationToken?, CancellationToken, Task<ContentPage>> read,
        VerificationBudget budget, CancellationToken cancellationToken)
    {
        var expected = document.Sections.Where(section => sectionId is null || section.SectionId == sectionId)
            .OrderBy(section => section.Ordinal).SelectMany(section => section.Claims).ToArray();
        Assert.NotEmpty(expected);
        Assert.All(expected, claim => Assert.False(string.IsNullOrEmpty(claim.Text), "Published claim text must be nonempty for this qualification."));
        var reconstructed = expected.Select(_ => new StringBuilder()).ToArray();
        var content = new StringBuilder();
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        var claimIndex = 0;
        var pages = 0;
        ContinuationToken? firstContinuation = null;
        ContinuationToken? cursor = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await read(handle, cursor, cancellationToken);
            budget.Admit(page);
            pages++;
            Assert.Equal(KnowledgeReadStatus.Success, page.Status);
            Assert.Equal(handle, page.Document!.Handle);
            Assert.Equal(document.Title, page.Document.Title);
            Assert.Equal(DocumentKind.BusinessLogic, page.Document.Kind);
            Assert.All(page.Document.QualifiedExcerpt, claim => Assert.Empty(claim.Evidence));
            Assert.NotNull(page.Content);
            // Strict encoding rejects partial surrogate pairs instead of silently replacing them.
            _ = new UTF8Encoding(false, true).GetBytes(page.Content);
            content.Append(page.Content);
            foreach (var fragment in page.Claims)
            {
                Assert.True(claimIndex < expected.Length, "The content stream repeated or invented a claim.");
                var original = expected[claimIndex];
                Assert.Equal(original.Qualification, fragment.Qualification);
                Assert.Empty(fragment.Evidence);
                if (!original.Limitations.IsEmpty) { Assert.NotEmpty(fragment.Limitations); }
                Assert.False(string.IsNullOrEmpty(fragment.Text), "A credited claim fragment must contain text.");
                var offset = reconstructed[claimIndex].Length;
                Assert.True(offset + fragment.Text.Length <= original.Text.Length, "Claim continuation duplicated text.");
                Assert.Equal(original.Text.Substring(offset, fragment.Text.Length), fragment.Text);
                reconstructed[claimIndex].Append(fragment.Text);
                if (reconstructed[claimIndex].Length == original.Text.Length) { claimIndex++; }
            }
            if (page.Next is null) { break; }
            firstContinuation ??= page.Next;
            Assert.True(cursors.Add(page.Next.Value), "Exact content repeated an opaque continuation.");
            cursor = page.Next;
        }
        Assert.Equal(expected.Length, claimIndex);
        var claimTexts = reconstructed.Select(value => value.ToString()).ToImmutableArray();
        Assert.Equal(expected.Select(claim => claim.Text).ToArray(), claimTexts.ToArray());
        Assert.Equal(ExpectedBusinessNarrative(document, sectionId), content.ToString());
        return new(handle, content.ToString(), claimTexts, pages, firstContinuation);
    }

    // Independent mechanical projection oracle from the accepted Business read contract.
    // This neither shares the production chunk algorithm nor establishes source-semantic correctness.
    private static string ExpectedBusinessNarrative(OctoberPublishedDocument document, string? sectionId)
    {
        var expected = new StringBuilder();
        if (sectionId is null) { expected.Append(document.Title).Append('\n'); }
        foreach (var section in document.Sections.Where(section => sectionId is null || section.SectionId == sectionId)
            .OrderBy(section => section.Ordinal))
        {
            expected.Append(section.Title).Append('\n');
            foreach (var claim in section.Claims)
            {
                var label = claim.Qualification switch
                {
                    ClaimQualification.SourceSupported => "来源支持",
                    ClaimQualification.Advisory => "建议性上下文",
                    ClaimQualification.Hypothesis => "假设",
                    ClaimQualification.Uncertain => "不确定",
                    ClaimQualification.Mixed => "混合依据",
                    _ => throw new InvalidOperationException("The published claim qualification is unsupported."),
                };
                expected.Append('[').Append(label).Append("] ").Append(claim.Text).Append('\n');
                if (!claim.Limitations.IsDefaultOrEmpty)
                {
                    expected.Append("限制：存在已记录的证据限制。\n");
                }
            }
        }
        return expected.ToString();
    }

    private static DocumentHandle Handle(PublicationBinding publication, DocumentVersionReference document) =>
        new(document.BatchId, publication.PublicationId, document.LogicalDocumentId, document.DocumentVersionId);

    private static void AssertUnavailable(ContentPage page)
    {
        Assert.Equal(KnowledgeReadStatus.Unavailable, page.Status);
        Assert.Null(page.Document); Assert.Null(page.Content); Assert.Empty(page.Claims);
        Assert.Null(page.Next); Assert.Null(page.Coverage);
    }

    private static void AssertUnchangedDocuments(ImmutableArray<OctoberPublishedDocument> before, ImmutableArray<OctoberPublishedDocument> after)
    {
        var original = before.ToDictionary(document => document.Reference);
        var retained = after.ToDictionary(document => document.Reference);
        Assert.True(original.Keys.ToHashSet().SetEquals(retained.Keys), "Publication membership changed during reads.");
        foreach (var pair in original)
        {
            var current = retained[pair.Key];
            Assert.Equal(pair.Value.Kind, current.Kind);
            Assert.Equal(pair.Value.Title, current.Title);
            Assert.Equal(OctoberKnowledgePolicy.CanonicalizeJson(pair.Value.ContentJson), OctoberKnowledgePolicy.CanonicalizeJson(current.ContentJson));
            Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(pair.Value.Sections.OrderBy(section => section.Ordinal).ThenBy(section => section.SectionId)),
                OctoberKnowledgePolicy.SerializeCanonical(current.Sections.OrderBy(section => section.Ordinal).ThenBy(section => section.SectionId)));
        }
    }

    private sealed class VerificationBudget
    {
        private int _pages;
        private long _bytes;
        internal void Admit<T>(T page)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(page).Length;
            Assert.InRange(bytes, 1, KnowledgeContract.MaximumContentBytes);
            _bytes += bytes;
            Assert.True(++_pages <= MaximumPages && _bytes <= MaximumAggregateBytes,
                "The explicit qualification traversal cap was exceeded; this is not a production-policy waiver.");
        }
    }
}
