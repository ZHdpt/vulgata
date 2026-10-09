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

    internal sealed record ReconstructedBusinessStream(DocumentHandle Document, string Content,
        ImmutableArray<string> OrderedClaimTexts, int Pages);

    internal static async Task<ImmutableArray<ReconstructedBusinessStream>> AssertBusinessPublicationAsync(
        OctoberHostSelectionTests.GeneratorPrepareFixture fixture, PublicationBinding publication,
        string subjectId, string capturedSecurityStamp, CancellationToken cancellationToken)
    {
        var original = await fixture.LoadExactPublicationAsync(publication, cancellationToken);
        Assert.Equal(publication, original.Publication);
        var expected = original.Documents.Where(document => document.Kind == DocumentKind.BusinessLogic)
            .ToDictionary(document => Handle(publication, document.Reference));
        Assert.NotEmpty(expected);
        Assert.Equal(original.Documents.Length, original.Documents.Select(document => document.Reference).Distinct().Count());
        var budget = new VerificationBudget();
        await using var session = await fixture.OpenReaderSessionAsync(publication, subjectId,
            capturedSecurityStamp, KnowledgeMode.Business, cancellationToken);
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
        return reconstructed.ToImmutable();
    }

    /// <summary>The caller rotates/revokes actual Identity first. This helper never logs in or mints a fresh stamp.</summary>
    internal static async Task AssertRevokedBusinessReaderAsync(
        OctoberHostSelectionTests.GeneratorPrepareFixture fixture, PublicationBinding publication,
        DocumentHandle previouslyReadBusinessDocument, string subjectId, string originalCapturedStamp,
        CancellationToken cancellationToken)
    {
        Assert.Equal(publication.PublicationId, previouslyReadBusinessDocument.PublicationId);
        Assert.Equal(publication.Knowledge.BatchId, previouslyReadBusinessDocument.BatchId);
        var budget = new VerificationBudget();
        await using var denied = await fixture.OpenReaderSessionAsync(publication, subjectId,
            originalCapturedStamp, KnowledgeMode.Business, cancellationToken);
        if (denied is null) { return; }
        var content = await denied.ReadExactAsync(previouslyReadBusinessDocument, null, cancellationToken);
        budget.Admit(content);
        AssertUnavailable(content);
        var browse = await denied.SearchAsync("", 20, null, cancellationToken);
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
            Assert.True(cursors.Add(page.Next.Value), "Exact content repeated an opaque continuation.");
            cursor = page.Next;
        }
        Assert.Equal(expected.Length, claimIndex);
        var claimTexts = reconstructed.Select(value => value.ToString()).ToImmutableArray();
        Assert.Equal(expected.Select(claim => claim.Text).ToArray(), claimTexts.ToArray());
        Assert.Equal(ExpectedBusinessNarrative(document, sectionId), content.ToString());
        return new(handle, content.ToString(), claimTexts, pages);
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
