using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberBusinessSynthesisCorrectionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ThreeStructuralCorrections_PreservesPacketAndAccountsEveryAttemptAsync()
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        var spy = new SynthesisScopeSpy(scope);
        using var client = new CorrectionClient(call => { clock.Advance(TimeSpan.FromSeconds(2)); return call == 4 ? f.Response() : Invalid(call); });
        var result = await f.ExecuteAsync(client, spy, new SynthesisDisclosure(), clock);
        Assert.NotNull(result.Content);
        Assert.Equal(4, result.Attempts.Length);
        Assert.Equal(new[] { BusinessSynthesisAttemptStrategy.Configured, BusinessSynthesisAttemptStrategy.Configured,
            BusinessSynthesisAttemptStrategy.ThinkingDisabled, BusinessSynthesisAttemptStrategy.CompactedThinkingDisabled }, client.Strategies);
        Assert.Equal(new[] { 2, 4, 6, 3 }, client.Histories.Select(history => history.Length));
        foreach (var history in client.Histories)
        {
            Assert.Equal(client.Histories[0][0].Text, history[0].Text);
            Assert.Equal(client.Histories[0][1].Text, history[1].Text);
        }
        Assert.Equal("reasoning-1", Assert.Single(client.Histories[1].SelectMany(message => message.Contents).OfType<TextReasoningContent>()).Text);
        Assert.Equal(new[] { "reasoning-1", "reasoning-2" }, client.Histories[2].SelectMany(message => message.Contents).OfType<TextReasoningContent>().Select(content => content.Text));
        Assert.DoesNotContain(client.Histories[3], message => message.Role == ChatRole.Assistant);
        Assert.Contains("remainingCorrections", client.Histories[3][2].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("rejected-", string.Join("\n", client.Histories[3].Select(message => message.Text)), StringComparison.Ordinal);
        Assert.All(client.Options, options => { Assert.Equal(40, options.MaxOutputTokens); Assert.Empty(options.Tools!); Assert.Equal(ChatResponseFormat.Json, options.ResponseFormat); });
        Assert.Equal(4, spy.Requests.Count);
        for (int i = 0; i < spy.Requests.Count; i++)
        {
            string envelope = JsonSerializer.Serialize(client.Histories[i].Select(message => new
            {
                role = message.Role.Value,
                content = string.Concat(message.Contents.OfType<TextContent>().Select(content => content.Text)),
                reasoning_content = string.Concat(message.Contents.OfType<TextReasoningContent>().Select(content => content.Text))
            }));
            long inputReservation = Encoding.UTF8.GetByteCount(envelope) + 4096L + 256L * client.Histories[i].Length;
            Assert.Equal(inputReservation + 40, spy.Requests[i].MaximumTokens);
        }
        Assert.True(spy.Requests[2].MaximumTokens > spy.Requests[1].MaximumTokens);
        Assert.True(spy.Requests[3].MaximumTokens < spy.Requests[2].MaximumTokens);
        Assert.Equal(4, scope.GetSnapshot().Usage.Requests);
        Assert.Equal(40, scope.GetSnapshot().Usage.InputTokens);
        Assert.Equal(20, scope.GetSnapshot().Usage.OutputTokens);
        Assert.Equal(0, scope.GetSnapshot().OutstandingAttempts);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_AlwaysMalformed_StopsAfterFourSettledPhysicalAttemptsAsync()
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        using var client = new CorrectionClient(call => { clock.Advance(TimeSpan.FromSeconds(2)); return Invalid(call); });
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.Null(result.Content);
        Assert.Equal(BusinessSynthesisFailureReason.JsonInvalid, result.FailureReason);
        Assert.Equal(4, result.Attempts.Length);
        Assert.Equal(4, client.Histories.Count);
        Assert.Equal(0, scope.GetSnapshot().OutstandingAttempts);
        Assert.False(scope.GetSnapshot().Usage.HasUnknownUsage);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("length")]
    [InlineData("reference")]
    [InlineData("qualification")]
    [InlineData("mixed")]
    [InlineData("undefined")]
    [InlineData("unknown-usage")]
    [InlineData("transport")]
    public async Task ExecuteAsync_NonstructuralOrIndeterminateFailure_DoesNotRegenerateAsync(string failure)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        using var client = new CorrectionClient(_ =>
        {
            clock.Advance(TimeSpan.FromSeconds(2));
            if (failure == "transport") throw new HttpRequestException("Controlled transport failure");
            var response = Invalid(1);
            if (failure == "length") response.FinishReason = ChatFinishReason.Length;
            if (failure == "unknown-usage") response.Usage = null;
            if (failure is "reference" or "qualification" or "mixed" or "undefined")
            {
                var proposal = f.Proposal();
                var section = proposal.Sections[0];
                var claim = section.Claims[0];
                claim = failure == "reference" ? claim with { Inputs = [new(f.Root, "absent-section", "absent-claim")] }
                    : claim with { Qualification = failure == "mixed" ? ClaimQualification.Mixed : failure == "undefined" ? (ClaimQualification)99 : ClaimQualification.Advisory };
                proposal = proposal with { Sections = proposal.Sections.SetItem(0, section with { Claims = section.Claims.SetItem(0, claim) }) };
                response = SynthesisCase.JsonResponse(JsonSerializer.Serialize(proposal));
            }
            return response;
        });
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.Null(result.Content);
        Assert.Equal(1, client.Histories.Count);
        Assert.Single(result.Attempts);
        Assert.Equal(0, scope.GetSnapshot().OutstandingAttempts);
        if (failure is "unknown-usage" or "transport") Assert.True(scope.GetSnapshot().Usage.HasUnknownUsage);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("Ordinal")]
    [InlineData("Qualification")]
    public async Task ExecuteAsync_OmittedRequiredValue_IsCorrectedBeforeAcceptanceAsync(string omitted)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        var malformed = JsonNode.Parse(JsonSerializer.Serialize(f.Proposal()))!.AsObject();
        var section = malformed["Sections"]![0]!.AsObject();
        Assert.True(omitted == "Ordinal" ? section.Remove(omitted) : section["Claims"]![0]!.AsObject().Remove(omitted));
        using var client = new CorrectionClient(call =>
        {
            clock.Advance(TimeSpan.FromSeconds(2));
            return call == 1 ? SynthesisCase.JsonResponse(malformed.ToJsonString()) : f.Response();
        });
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.NotNull(result.Content);
        Assert.Equal(2, client.Histories.Count);
        Assert.Equal(2, result.Attempts.Length);
        Assert.Equal(new[] { BusinessSynthesisAttemptStrategy.Configured, BusinessSynthesisAttemptStrategy.Configured }, client.Strategies);
        Assert.Equal(0, scope.GetSnapshot().OutstandingAttempts);
    }
    private static ChatResponse Invalid(int call)
    {
        var response = SynthesisCase.JsonResponse("{rejected-" + call);
        response.Messages[0].Contents.Add(new TextReasoningContent("reasoning-" + call));
        return response;
    }

    private sealed class CorrectionClient(Func<int, ChatResponse> response) : IChatClient, IOctoberBusinessSynthesisClient
    {
        private readonly ResolutionClient _inner = new("selected-profile", (call, _) => Task.FromResult(response(call)));
        public string ProviderProfileId => "selected-profile";
        public bool SupportsSynthesisCorrections => true;
        internal List<ChatMessage[]> Histories { get; } = [];
        internal List<ChatOptions> Options { get; } = [];
        internal List<BusinessSynthesisAttemptStrategy> Strategies { get; } = [];
        public Task<ChatResponse> GetSynthesisResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options,
            IOctoberProviderAttemptLease lease, BusinessSynthesisAttemptStrategy strategy, Action<long> onDispatched, CancellationToken cancellationToken = default)
        {
            var history = messages.Select(message => message.Clone()).ToArray();
            Histories.Add(history); Options.Add(options.Clone()); Strategies.Add(strategy);
            return _inner.GetAttemptResponseAsync(history, options, lease, onDispatched, cancellationToken);
        }
        public Task<ChatResponse> GetAttemptResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options, IOctoberProviderAttemptLease lease,
            Action<long> onDispatched, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Synthesis-specific port required.");
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() => _inner.Dispose();
    }
}



