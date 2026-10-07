using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Chat;

/// <summary>
/// Chat consumer qualification with Business's real reader, capsule protection and dependency authority
/// over its controlled published-source fixture. No database, live Identity, provider or durable dialogue qualification.
/// </summary>
public sealed class OctoberChatOwnerConformanceTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadWiki_RealOwnerContinuationFitsActualChatWrapperAndProvidesEagerVerifiableProofAsync()
    {
        var owner = new DisclosureFixture();
        string exact = "BEGIN-CHAT" + new string('a', 40000) + "中文😀\"\\\n<>&END-CHAT";
        owner.SetText(exact);
        var (budget, session, tools) = await OpenChatAsync(owner);
        var content = new StringBuilder();
        string? continuation = null;
        long actualToolBytes = 0;
        int pages = 0;
        int largestResult = 0;
        do
        {
            OctoberChatToolInvocation invocation = await tools.InvokeForModelAsync("ReadWiki",
                JsonSerializer.Serialize(new { document = owner.Handle, continuation }));
            Assert.Equal(OctoberChatToolStatus.Succeeded, invocation.Result.Status);
            ContentPage page = Assert.IsType<ContentPage>(invocation.Result.Content);
            Assert.InRange(invocation.OutputUtf8.Length, 1, 65536);
            Assert.Equal(new OctoberChatKnowledgeOutputEncoder().EncodeContent(page).ToArray(), invocation.OutputUtf8.ToArray());
            using JsonDocument model = JsonDocument.Parse(invocation.OutputUtf8.AsMemory());
            Assert.Equal(JsonValueKind.Object, model.RootElement.ValueKind);
            Assert.Equal(page.Content, model.RootElement.GetProperty("content").GetProperty("content").GetString());
            Assert.DoesNotContain(session.InferenceDependencies.ProtectedPayload, Encoding.UTF8.GetString(invocation.OutputUtf8.AsSpan()), StringComparison.Ordinal);
            Assert.All(page.Claims, claim => Assert.Equal(ClaimQualification.SourceSupported, claim.Qualification));
            content.Append(page.Content);
            actualToolBytes += invocation.OutputUtf8.Length;
            largestResult = Math.Max(largestResult, invocation.OutputUtf8.Length);
            continuation = page.Next?.Value;
            Assert.InRange(++pages, 1, 15);
            DialogueConsumedTarget eager = Assert.Single(session.ConsumedReads);
            Assert.Equal(pages, eager.Reference.SuccessfulChunks);
            Assert.Equal(Encoding.UTF8.GetByteCount(content.ToString()), eager.Reference.ContentBytes);
            Assert.Equal(continuation is not null, eager.Reference.IsPartial);
            Assert.Empty(session.ReportedReferences);
            Assert.Equal(actualToolBytes, budget.ToolResultBytes);
        } while (continuation is not null);

        Assert.True(pages > 1);
        Assert.InRange(largestResult, 32768, 65536);
        Assert.Contains(exact, content.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, content.ToString().Split("BEGIN-CHAT", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, content.ToString().Split("END-CHAT", StringSplitOptions.None).Length - 1);
        OctoberChatToolInvocation report = await tools.InvokeForModelAsync("ReportAnswerDocuments",
            JsonSerializer.Serialize(new { documents = new[] { owner.Handle } }));
        Assert.Equal(OctoberChatToolStatus.Succeeded, report.Result.Status);
        OctoberChatReference reference = Assert.Single(report.Result.References);
        Assert.False(reference.IsPartial);
        Assert.Equal(pages, reference.SuccessfulChunks);
        Assert.Equal(Encoding.UTF8.GetByteCount(content.ToString()), reference.ContentBytes);
        Assert.Equal(actualToolBytes + report.OutputUtf8.Length, budget.ToolResultBytes);
        Assert.Equal(new[] { reference }, session.ReportedReferences);

        DialogueConsumedTarget consumed = Assert.Single(session.ConsumedReads);
        KnowledgeConsumedContentResult verified = await owner.Authority.VerifyConsumedContentAsync(owner.Request, consumed.Proof, session.InferenceDependencies);
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, verified.Status);
        Assert.Equal(owner.Handle, verified.Receipt!.Document);
        Assert.Equal(reference.IsPartial, verified.Receipt.IsPartial);
        Assert.Equal(reference.SuccessfulChunks, verified.Receipt.SuccessfulChunks);
        Assert.Equal(reference.ContentBytes, verified.Receipt.ContentBytes);
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, await owner.Authority.ReauthorizeAsync(owner.Request, [session.InferenceDependencies]));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadWiki_RealOwnerSemanticReplayKeepsUniqueCreditWhileChargingBothWireResultsAsync()
    {
        var owner = new DisclosureFixture();
        owner.SetText(string.Concat(Enumerable.Repeat("中文😀", 10000)));
        var (budget, session, tools) = await OpenChatAsync(owner, new OctoberChatLimits { ContentBytes = 18000 });
        string arguments = JsonSerializer.Serialize(new { document = owner.Handle });

        OctoberChatToolInvocation first = await tools.InvokeForModelAsync("ReadWiki", arguments);
        Assert.Equal(OctoberChatToolStatus.Succeeded, first.Result.Status);
        DialogueConsumedTarget original = Assert.Single(session.ConsumedReads);
        OctoberChatToolInvocation replay = await tools.InvokeForModelAsync("ReadWiki", arguments);
        Assert.Equal(OctoberChatToolStatus.Succeeded, replay.Result.Status);
        DialogueConsumedTarget retained = Assert.Single(session.ConsumedReads);

        Assert.NotNull(first.Result.Content!.Next);
        Assert.NotNull(replay.Result.Content!.Next);
        Assert.NotEqual(first.Result.Content.Next.Value, replay.Result.Content.Next.Value);
        Assert.Equal(first.Result.Content.Content, replay.Result.Content.Content);
        Assert.Equal(original.Reference, retained.Reference);
        Assert.True(retained.Reference.IsPartial);
        Assert.Equal(1, retained.Reference.SuccessfulChunks);
        Assert.Equal(original.Proof.BindingDigest, retained.Proof.BindingDigest);
        Assert.Equal((long)first.OutputUtf8.Length + replay.OutputUtf8.Length, budget.ToolResultBytes);
        Assert.Empty(session.ReportedReferences);
        KnowledgeConsumedContentResult verified = await owner.Authority.VerifyConsumedContentAsync(owner.Request, retained.Proof, session.InferenceDependencies);
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, verified.Status);
        Assert.Equal(1, verified.Receipt!.SuccessfulChunks);
        Assert.Equal(Encoding.UTF8.GetByteCount(first.Result.Content.Content!), verified.Receipt.ContentBytes);
        Assert.True(verified.Receipt.IsPartial);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Reauthorize_RealOwnerUncitedSearchDependencyDenialStopsChatWithoutCitationRereadAsync()
    {
        var owner = new DisclosureFixture();
        var (budget, session, tools) = await OpenChatAsync(owner);
        OctoberChatToolInvocation search = await tools.InvokeForModelAsync("BrowseWiki", "{\"query\":\"\"}");
        Assert.Equal(OctoberChatToolStatus.Succeeded, search.Result.Status);
        Assert.NotEmpty(search.Result.Search!.Documents);
        Assert.Empty(session.ConsumedReads);
        Assert.Empty(session.ReportedReferences);
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, await owner.Authority.ReauthorizeAsync(owner.Request, [session.InferenceDependencies]));
        owner.Data.Source.Failure = new InvalidOperationException("A retained inference capsule must not reread a guessed citation subset.");
        owner.Policy.DeniedContributor = owner.Uncited;

        await Assert.ThrowsAnyAsync<Exception>(() => session.ReauthorizeAsync());

        Assert.Equal(KnowledgeDependencyStatus.Denied, session.FailureStatus);
        Assert.True(budget.IsStopped);
        Assert.Contains(owner.Policy.Requests, request => request.AllContributors.Contains(owner.Uncited));
        Assert.Empty(session.ConsumedReads);
        Assert.Empty(session.ReportedReferences);
        await Assert.ThrowsAnyAsync<Exception>(() => session.ReportAsync([]));
        Assert.Equal(KnowledgeDependencyStatus.Denied, session.FailureStatus);
    }

    private static async Task<(OctoberChatBudget Budget, OctoberChatDisclosureSession Session, OctoberWikiTools Tools)> OpenChatAsync(
        DisclosureFixture owner, OctoberChatLimits? limits = null)
    {
        ReadSelection selection = await owner.OpenAsync();
        KnowledgeDependencyResult root = await owner.Authority.CreateNoKnowledgeRootAsync(owner.Request,
            DisclosureFixture.Hash("controlled question and static instructions only"));
        Assert.Equal(KnowledgeDependencyStatus.Succeeded, root.Status);
        var budget = new OctoberChatBudget(limits);
        var binding = new OctoberChatTurnBinding(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), owner.Request, selection);
        var session = new OctoberChatDisclosureSession(binding, budget, owner.Authority,
            _ => Task.FromResult<ReadSelectionRequest?>(owner.Request), root.Dependencies!);
        return (budget, session, new OctoberWikiTools(owner.Disclosed, binding, budget, session));
    }
}
