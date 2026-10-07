using System.Text;
using System.Text.Json;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Chat;

public sealed class OctoberContentContinuationTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(null)]
    [InlineData("overview")]
    public async Task ReadWiki_LargeDocumentOrSingleSection_ContinuesOpaqueChunksWithoutLosingQualificationsAsync(string? section)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        DocumentHandle handle = fixture.Knowledge.Handle(fixture.Knowledge.ClA, section: section);
        string firstText = new('中', 12000);
        // Keep each complete JSON projection below 64 KiB, including JSON's surrogate
        // escaping, while the exact combined UTF-8 content remains larger than 64 KiB.
        string secondText = string.Concat(Enumerable.Repeat("😀", 2000)) + new string('x', 28000);
        fixture.Reader.ProjectContent = (page, request) => page.Status == KnowledgeReadStatus.Success
            ? page with { Content = request.Continuation is null ? firstText : secondText }
            : page;

        OctoberChatToolResult first = await harness.Tools.InvokeAsync("ReadWiki", Args(handle));
        Assert.Equal(OctoberChatToolStatus.Succeeded, first.Status);
        ContinuationToken token = Assert.IsType<ContinuationToken>(first.Content!.Next);
        OctoberChatReference prefix = Assert.Single(harness.Ledger.Snapshot);
        Assert.True(prefix.IsPartial);
        Assert.Equal(1, prefix.SuccessfulChunks);

        OctoberChatToolResult second = await harness.Tools.InvokeAsync("ReadWiki", Args(handle, token.Value));

        Assert.Equal(OctoberChatToolStatus.Succeeded, second.Status);
        Assert.Null(second.Content!.Next);
        Assert.Equal(token, fixture.Reader.ExactRequests[1].Continuation);
        Assert.Equal(handle, fixture.Reader.ExactRequests[1].Document);
        Assert.Equal(harness.Binding.Selection, fixture.Reader.ExactRequests[1].Selection);
        Assert.Equal(firstText + secondText, first.Content.Content + second.Content.Content);
        var utf8 = new UTF8Encoding(false, true);
        Assert.True(utf8.GetByteCount(firstText + secondText) > KnowledgeContract.MaximumContentBytes);
        foreach (ContentPage page in new[] { first.Content, second.Content })
        {
            Assert.InRange(utf8.GetByteCount(page.Content!), 1, KnowledgeContract.MaximumContentBytes);
            Assert.Equal(page.Content, utf8.GetString(utf8.GetBytes(page.Content!)));
            Assert.Equal(fixture.Knowledge.Claims.Length, page.Claims.Length);
            for (int index = 0; index < fixture.Knowledge.Claims.Length; index++)
            {
                QualifiedClaimGroup expected = fixture.Knowledge.Claims[index];
                QualifiedClaimGroup actual = page.Claims[index];
                Assert.Equal(expected.ClaimGroupId, actual.ClaimGroupId);
                Assert.Equal(expected.Text, actual.Text);
                Assert.Equal(expected.Qualification, actual.Qualification);
                Assert.Equal(expected.Evidence.ToArray(), actual.Evidence.ToArray());
                Assert.Equal(expected.Limitations.ToArray(), actual.Limitations.ToArray());
            }
            Assert.Contains(page.Claims, claim => claim.Qualification == ClaimQualification.Advisory);
        }

        OctoberChatReference completed = Assert.Single(harness.Ledger.Snapshot);
        Assert.False(completed.IsPartial);
        Assert.Equal(2, completed.SuccessfulChunks);
        Assert.Equal(utf8.GetByteCount(firstText + secondText), completed.ContentBytes);
        Assert.True(second.Content.Coverage!.IsPartial); // Corpus coverage does not mean unread content.
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("tamper")]
    [InlineData("section")]
    [InlineData("selection")]
    [InlineData("expired-owner-result")]
    public async Task ReadWiki_InvalidContinuation_DoesNotAddCreditOrSubstituteSelectionAsync(string mismatch)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        DocumentHandle handle = fixture.Knowledge.Handle(fixture.Knowledge.ClA, section: "overview");
        OctoberChatToolResult first = await harness.Tools.InvokeAsync("ReadWiki", Args(handle));
        Assert.Equal(OctoberChatToolStatus.Succeeded, first.Status);
        string token = first.Content!.Next!.Value;
        OctoberChatReference prefix = Assert.Single(harness.Ledger.Snapshot);
        if (mismatch == "tamper") token += "-tampered";
        if (mismatch == "section") handle = handle with { SectionId = null };
        if (mismatch == "selection") harness = await fixture.OpenWikiAsync();
        if (mismatch == "expired-owner-result")
        {
            // Models the owner's expiry response; not a claim to implement or qualify cursor expiry.
            fixture.Reader.ProjectContent = (_, _) => new(KnowledgeReadStatus.InvalidContinuation, null, null, [], null, null);
        }

        OctoberChatToolResult rejected = await harness.Tools.InvokeAsync("ReadWiki", Args(handle, token));

        Assert.NotEqual(OctoberChatToolStatus.Succeeded, rejected.Status);
        Assert.Null(rejected.Content);
        if (mismatch == "selection") Assert.Empty(harness.Ledger.Snapshot);
        else Assert.Equal(prefix, Assert.Single(harness.Ledger.Snapshot));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DisclosureStatus.Denied)]
    [InlineData(DisclosureStatus.AuthorityUnavailable)]
    public async Task ReadWiki_AuthorityLostBetweenChunks_KeepsOnlySuccessfullyConsumedPrefixAsync(DisclosureStatus status)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        DocumentHandle handle = fixture.Knowledge.Handle(fixture.Knowledge.ClA);
        OctoberChatToolResult first = await harness.Tools.InvokeAsync("ReadWiki", Args(handle));
        OctoberChatReference prefix = Assert.Single(harness.Ledger.Snapshot);
        fixture.Policy.LiveStatus = status;

        OctoberChatToolResult denied = await harness.Tools.InvokeAsync("ReadWiki", Args(handle, first.Content!.Next!.Value));

        Assert.Equal(OctoberChatToolStatus.Unavailable, denied.Status);
        Assert.Null(denied.Content);
        Assert.Equal(prefix, Assert.Single(harness.Ledger.Snapshot));
        Assert.True(prefix.IsPartial);
        // This ledger is evidence of consumption, not authority to disclose cached prose or references.
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadWiki_CancelledWhileAwaitingOwner_DoesNotCreditUnreturnedPageAndReleasesSlotAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        fixture.Reader.ExactGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        Task<OctoberChatToolResult> read = harness.Tools.InvokeAsync("ReadWiki", Args(fixture.Knowledge.Handle(fixture.Knowledge.ClA)), cancellation.Token);
        cancellation.Cancel();

        OctoberChatToolResult cancelled = await read;

        Assert.Equal(OctoberChatToolStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.Content);
        Assert.Empty(harness.Ledger.Snapshot);
        Assert.Equal(0, harness.Budget.ActiveTools);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadWiki_OriginalLifetimeExpiresWhileAwaitingOwner_CancelsWithoutCreditOrLeakedSlotAsync(bool deadline)
    {
        var fixture = new ControlledChatFixture();
        var clock = new ControlledChatFixture.ManualClock();
        using var lifetime = new CancellationTokenSource();
        var budget = new OctoberChatBudget(timeProvider: clock, cancellationToken: lifetime.Token);
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync(budget: budget);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Reader.ExactGate = gate;
        Task<OctoberChatToolResult> read = harness.Tools.InvokeAsync("ReadWiki", Args(fixture.Knowledge.Handle(fixture.Knowledge.ClA)));
        try
        {
            Assert.Single(fixture.Reader.ExactRequests);
            if (deadline) clock.Advance(TimeSpan.FromSeconds(180));
            else lifetime.Cancel();
            OctoberChatToolResult stopped = await read.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(deadline ? OctoberChatToolStatus.BudgetExceeded : OctoberChatToolStatus.Cancelled, stopped.Status);
            Assert.Null(stopped.Content);
            Assert.Empty(harness.Ledger.Snapshot);
            Assert.Equal(0, budget.ActiveTools);
            Assert.True(budget.IsStopped);
        }
        finally
        {
            gate.TrySetResult();
            await read;
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadWiki_AggregateBudgetExhaustedBeforeContinuation_ReturnsIncompleteWithoutCreditingUnreadSuffixAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        DocumentHandle handle = fixture.Knowledge.Handle(fixture.Knowledge.ClA);
        OctoberChatToolResult first = await harness.Tools.InvokeAsync("ReadWiki", Args(handle));
        OctoberChatReference prefix = Assert.Single(harness.Ledger.Snapshot);
        Assert.True(harness.Budget.TryAddToolResultBytes(262144 - harness.Budget.ToolResultBytes));

        OctoberChatToolResult bounded = await harness.Tools.InvokeAsync("ReadWiki", Args(handle, first.Content!.Next!.Value));

        Assert.Equal(OctoberChatToolStatus.BudgetExceeded, bounded.Status);
        Assert.Null(bounded.Content);
        Assert.Equal(prefix, Assert.Single(harness.Ledger.Snapshot));
        Assert.True(prefix.IsPartial);
        Assert.True(harness.Budget.IsStopped);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadWiki_OversizedIndividualResult_IsRejectedWholeWithoutTruncationOrCreditAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        fixture.Reader.ProjectContent = (page, _) => page with { Content = new string('x', 65537) };

        OctoberChatToolResult result = await harness.Tools.InvokeAsync("ReadWiki", Args(fixture.Knowledge.Handle(fixture.Knowledge.ClA)));

        Assert.Equal(OctoberChatToolStatus.BudgetExceeded, result.Status);
        Assert.Null(result.Content);
        Assert.Empty(harness.Ledger.Snapshot);
        Assert.Equal(0, harness.Budget.ActiveTools);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("qualification-metadata")]
    [InlineData("json-escaping")]
    public async Task ReadWiki_CompleteProjectedResultExceeds64KiB_RejectsWholePageWithoutTruncationAsync(string expansion)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        fixture.Reader.ProjectContent = (page, _) => expansion == "qualification-metadata"
            ? page with { Content = "small", Claims = [fixture.Knowledge.AdvisoryClaim with { Text = new string('中', 23000) }] }
            : page with { Content = new string('\n', 33000) };

        OctoberChatToolResult result = await harness.Tools.InvokeAsync("ReadWiki", Args(fixture.Knowledge.Handle(fixture.Knowledge.ClA)));

        Assert.Equal(OctoberChatToolStatus.BudgetExceeded, result.Status);
        Assert.Null(result.Content);
        Assert.Empty(harness.Ledger.Snapshot);
        Assert.Equal(0, harness.Budget.ActiveTools);
        Assert.True(harness.Budget.IsStopped);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("invalid-operation")]
    [InlineData("argument")]
    [InlineData("json")]
    public async Task ReadWiki_OwnerServiceException_IsUnavailableWithoutBlamingModelOrLeakingDetailsAsync(string exceptionKind)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        Exception failure = exceptionKind switch
        {
            "invalid-operation" => new InvalidOperationException("SECRET owner database failure"),
            "argument" => new ArgumentException("SECRET owner service failure"),
            _ => new JsonException("SECRET owner storage data failure"),
        };
        fixture.Reader.ProjectContent = (_, _) => throw failure;

        OctoberChatToolResult result = await harness.Tools.InvokeAsync("ReadWiki", Args(fixture.Knowledge.Handle(fixture.Knowledge.ClA)));

        Assert.Equal(OctoberChatToolStatus.Unavailable, result.Status);
        Assert.Null(result.Content);
        Assert.Null(result.Search);
        Assert.Null(result.Related);
        Assert.Empty(result.References);
        Assert.Empty(harness.Ledger.Snapshot);
        Assert.Equal(0, harness.Budget.ActiveTools);
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Record_DuplicatePrefixAndMiddleOnlyRead_DoNotInventCompleteCoverageAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        DocumentHandle handle = fixture.Knowledge.Handle(fixture.Knowledge.ClA);
        ContentPage first = await fixture.Reader.ReadExactAsync(new(harness.Binding.Selection, handle));
        ContentPage last = await fixture.Reader.ReadExactAsync(new(harness.Binding.Selection, handle, first.Next));
        Assert.True(harness.Ledger.Record(handle, null, first));
        OctoberChatReference prefix = Assert.Single(harness.Ledger.Snapshot);
        harness.Ledger.Record(handle, null, first);
        Assert.Equal(prefix, Assert.Single(harness.Ledger.Snapshot));

        var middleOnly = new OctoberTurnReadLedger(harness.Binding);
        Assert.True(middleOnly.Record(handle, first.Next, last));
        OctoberChatReference partial = Assert.Single(middleOnly.Snapshot);

        Assert.True(partial.IsPartial);
        Assert.Equal(1, partial.SuccessfulChunks);
        Assert.Equal(Encoding.UTF8.GetByteCount(last.Content!), partial.ContentBytes);
        Assert.True(harness.Ledger.Record(handle, first.Next, last));
        Assert.False(Assert.Single(harness.Ledger.Snapshot).IsPartial);
    }

    private static string Args(DocumentHandle document, string? continuation = null) =>
        JsonSerializer.Serialize(new { document, continuation });
}
