using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.Core.Entities.OctoberKnowledge;
using ApiType = Vulgata.Core.Entities.ApiType;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberBusinessSynthesisTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_ActualSerializedInputLocators_AreIndependentOfNewOutputLabelsAsync(bool substituteOutputSection)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        using var transport = new SynthesisWireProposalHandler(substituteOutputSection);
        using var client = await new OctoberWorkerClientFactory(new SynthesisSdkSecret(), transport).CreateAsync(
            new("controlled-wire-locators", "only-source", new("selected-profile", ApiType.ChatCompletions,
                "https://never-contacted.invalid/v1", "controlled-model", "credential://controlled-sdk"), "controlled-protected"));
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.Equal(1, transport.Calls);
        Assert.NotNull(transport.SelectedInput);
        Assert.NotEqual(SynthesisWireProposalHandler.OutputSectionId, transport.SelectedInput.SectionId);
        Assert.NotEqual(SynthesisWireProposalHandler.OutputClaimId, transport.SelectedInput.ClaimGroupId);
        Assert.Null(result.AdmissionFailure);
        if (substituteOutputSection)
        {
            Assert.Equal("SectionReference", SynthesisDiagnosticProbe.Read(result));
            Assert.Null(result.Content); Assert.Null(result.Proposal); Assert.Null(result.Dependencies);
        }
        else
        {
            Assert.NotNull(result.Content); Assert.NotNull(result.Proposal);
            var section = Assert.Single(result.Proposal.Sections);
            Assert.Equal(SynthesisWireProposalHandler.OutputSectionId, section.SectionId);
            var claim = Assert.Single(section.Claims);
            Assert.Equal(SynthesisWireProposalHandler.OutputClaimId, claim.ClaimGroupId);
            Assert.Equal(transport.SelectedInput, Assert.Single(claim.Inputs));
            Assert.Equal(transport.SelectedQualification, claim.Qualification);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void EmbeddedSynthesisPrompt_DistinguishesOutputLabelsFromExactSameChainInputLocators()
    {
        using var stream = typeof(OctoberBusinessSynthesisExecutor).Assembly.GetManifestResourceStream("Vulgata.Agents.Prompts.OctoberBusiness.Synthesis.md");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        string prompt = reader.ReadToEnd();
        foreach (string path in new[]
        {
            "input.Corpus.Documents[i].Version", "input.Corpus.Documents[i].Sections[j].SectionId",
            "input.Corpus.Documents[i].Sections[j].Claims[k].ClaimGroupId",
            "input.Corpus.Documents[i].Sections[j].Claims[k].Qualification", "group.Dependencies.Documents", "Inputs.Document"
        }) Assert.Contains(path, prompt, StringComparison.Ordinal);
        Assert.Matches(@"(?m)^.*输出.*SectionId.*ClaimGroupId.*$", prompt);
        Assert.Contains("同一", prompt, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("plain")]
    [InlineData("answer-and-reasoning")]
    [InlineData("reasoning-only")]
    [InlineData("invalid-answer")]
    public async Task ExecuteAsync_ActualSelectedSdkMapping_KeepsReasoningSeparateFromValidatedAnswerAsync(string shape)
    {
        var f = new SynthesisCase();
        string answer = JsonSerializer.Serialize(f.Proposal());
        using var transport = new SynthesisSdkResponseHandler(answer, shape);
        using var client = await new OctoberWorkerClientFactory(new SynthesisSdkSecret(), transport).CreateAsync(
            new("controlled-sdk-response", "only-source", new("selected-profile", ApiType.ChatCompletions,
                "https://never-contacted.invalid/v1", "controlled-model", "credential://controlled-sdk"), "controlled-protected"));
        var attemptClient = Assert.IsAssignableFrom<IOctoberBusinessAttemptClient>(client);
        var clock = new SynthesisClock();
        await using (var mappingScope = f.Scope(clock))
        {
            var admission = await mappingScope.AcquireProviderAttemptAsync(new(f.Run, f.Operation, Guid.NewGuid(),
                BusinessWorkKind.Synthesis, "selected-profile", TimeSpan.FromSeconds(30), 40));
            Assert.Equal(ProviderAdmissionStatus.Granted, admission.Status);
            var lease = Assert.IsAssignableFrom<IOctoberProviderAttemptLease>(admission.Lease);
            int dispatches = 0;
            try
            {
                var mapped = await attemptClient.GetAttemptResponseAsync([new(ChatRole.User, "Controlled mapping probe.")],
                    new ChatOptions { ResponseFormat = ChatResponseFormat.Json, Tools = [] }, lease, _ => dispatches++);
                // Assertion diagnostics contain only role/type categories, never answer or reasoning values.
                Assert.All(mapped.Messages, message => Assert.Equal(ChatRole.Assistant, message.Role));
                var categories = mapped.Messages.SelectMany(message => message.Contents).Select(content => content.GetType().Name)
                    .Order(StringComparer.Ordinal).ToArray();
                string[] expected = shape switch
                {
                    "plain" => [nameof(TextContent)],
                    "reasoning-only" => [nameof(TextReasoningContent)],
                    _ => [nameof(TextContent), nameof(TextReasoningContent)]
                };
                Assert.Equal(expected, categories);
                Assert.True(shape == "reasoning-only" ? string.IsNullOrEmpty(mapped.Text) :
                    string.Equals(shape == "invalid-answer" ? "{" : answer, mapped.Text, StringComparison.Ordinal));
                Assert.Equal(1, dispatches);
            }
            finally
            {
                ((IOctoberWorkerDispatchObserver)lease).CompleteDispatchObservation();
                await lease.SettleAsync(new(10, 5, null, TimeSpan.Zero, TimeSpan.Zero, ProviderAttemptOutcome.Completed));
                await lease.DisposeAsync();
            }
        }
        await using var scope = f.Scope(clock);
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.Equal(2, transport.Calls);
        Assert.Equal(10, Assert.Single(result.Attempts).InputTokens);
        Assert.Equal(5, result.Attempts[0].OutputTokens);
        if (shape is "reasoning-only" or "invalid-answer")
        {
            Assert.Null(result.Content); Assert.Null(result.Proposal); Assert.Null(result.Dependencies);
        }
        else
        {
            Assert.NotNull(result.Content); Assert.NotNull(result.Proposal); Assert.NotNull(result.Dependencies);
            Assert.Null(result.AdmissionFailure);
            Assert.True(string.Equals(OctoberKnowledgePolicy.SerializeCanonical(f.Proposal()),
                OctoberKnowledgePolicy.SerializeCanonical(result.Proposal), StringComparison.Ordinal));
            Assert.False(result.Content.ContentJson.Contains(SynthesisSdkResponseHandler.ReasoningMarker, StringComparison.Ordinal));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("tool")]
    [InlineData("unknown")]
    [InlineData("non-assistant")]
    public async Task ExecuteAsync_ReasoningCompatibility_DoesNotPermitToolsUnknownContentOrOtherRolesAsync(string shape)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        List<AIContent> contents = [new TextContent(JsonSerializer.Serialize(f.Proposal())), new TextReasoningContent("Controlled non-answer metadata.")];
        if (shape == "tool") contents.Add(new FunctionCallContent("controlled-call", "unexpected-tool", new Dictionary<string, object?>()));
        if (shape == "unknown") contents.Add(new SynthesisUnknownContent());
        var response = new ChatResponse(new ChatMessage(shape == "non-assistant" ? ChatRole.User : ChatRole.Assistant, contents))
        { FinishReason = ChatFinishReason.Stop, Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 } };
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(response));
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.Null(result.Content); Assert.Null(result.Proposal); Assert.Null(result.Dependencies);
        Assert.Equal("ResponseShape", SynthesisDiagnosticProbe.Read(result));
        Assert.Equal(1, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("ordinal", "SectionOrder")]
    [InlineData("section-duplicate", "ProposalShape")]
    [InlineData("claim-duplicate", "ClaimShape")]
    public void Validate_StructuralDiagnostic_PreservesInvalidProposalAndNoAcceptedContent(string mutation, string expected)
    {
        var f = new SynthesisCase();
        var proposal = f.Proposal();
        var section = proposal.Sections[0];
        proposal = mutation switch
        {
            "ordinal" => proposal with { Sections = [section with { Ordinal = 1 }] },
            "section-duplicate" => proposal with { Sections = [section, section with { Ordinal = 1 }] },
            _ => proposal with { Sections = [section with { Claims = section.Claims.Add(section.Claims[0]) }] }
        };
        var result = f.Validate(proposal);
        Assert.Equal(BusinessSynthesisValidationStatus.InvalidProposal, result.Status);
        Assert.Null(result.Content); Assert.Null(result.Dependencies);
        Assert.Equal(expected, SynthesisDiagnosticProbe.Read(result));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing", "ResponseMissing")]
    [InlineData("length", "ResponseLengthLimit")]
    [InlineData("non-stop", "FinishReasonNotStop")]
    [InlineData("null-finish", "FinishReasonNotStop")]
    [InlineData("shape", "ResponseShape")]
    [InlineData("payload", "ResponsePayloadLimit")]
    [InlineData("null-proposal", "ProposalMissing")]
    [InlineData("json", "JsonInvalid")]
    [InlineData("policy", "SectionOrder")]
    public async Task ExecuteAsync_ResponseDiagnostic_IdentifiesFailureWithoutContentOrRetryAsync(string mutation, string expected)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        var response = mutation switch
        {
            "missing" => null,
            "shape" => new ChatResponse(new ChatMessage(ChatRole.User, JsonSerializer.Serialize(f.Proposal()))) { FinishReason = ChatFinishReason.Stop },
            "payload" => SynthesisCase.JsonResponse(new string('x', OctoberBusinessSynthesisPolicy.MaximumPayloadUtf8Bytes + 1)),
            "null-proposal" => SynthesisCase.JsonResponse("null"),
            "json" => SynthesisCase.JsonResponse("{"),
            "policy" => SynthesisCase.JsonResponse(JsonSerializer.Serialize(f.Proposal() with { Sections = [f.Proposal().Sections[0] with { Ordinal = 1 }] })),
            _ => f.Response()
        };
        if (mutation == "length") response!.FinishReason = ChatFinishReason.Length;
        if (mutation == "non-stop") response!.FinishReason = ChatFinishReason.ToolCalls;
        if (mutation == "null-finish") response!.FinishReason = null;
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(response!));
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.Null(result.Content); Assert.Null(result.Proposal); Assert.Null(result.Dependencies);
        Assert.Equal(mutation == "json" ? ProviderAdmissionStatus.AuthorityUnavailable : null, result.AdmissionFailure);
        Assert.Equal(expected, SynthesisDiagnosticProbe.Read(result));
        Assert.Equal(1, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false, "InvalidInput", ProviderAdmissionStatus.Denied)]
    [InlineData(true, "OutputLimit", ProviderAdmissionStatus.BudgetExhausted)]
    public async Task ExecuteAsync_InputDiagnostic_PreservesExistingAdmissionFailureWithoutProviderAsync(bool oversized, string expected, ProviderAdmissionStatus status)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        var input = oversized ? f.Input with { Coverage = f.Input.Coverage.SetItem(0,
            f.Input.Coverage[0] with { Reason = new string('x', OctoberBusinessSynthesisPolicy.MaximumPayloadUtf8Bytes + 1) }) } : f.Input;
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(f.Response()));
        var result = await new OctoberBusinessSynthesisExecutor(client, "Controlled diagnostic instructions.", clock)
            .ExecuteAsync(input, oversized ? f.Group.GroupVersionId : Guid.Empty, f.Operation, f.Run, "selected-profile", 40,
                scope, new SynthesisDisclosure(), default, f.Limits);
        Assert.Equal(status, result.AdmissionFailure);
        Assert.Equal(expected, SynthesisDiagnosticProbe.Read(result));
        Assert.Null(result.Content); Assert.Null(result.Proposal); Assert.Null(result.Dependencies);
        Assert.Equal(0, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Diagnostic_ExactBoundedEnumAndNonzeroPolicyReason_AreExcludedFromOrdinaryAndCanonicalJson()
    {
        var f = new SynthesisCase();
        var result = f.Validate(f.Proposal() with { Sections = [f.Proposal().Sections[0] with { Ordinal = 1 }] });
        var property = SynthesisDiagnosticProbe.Property(result);
        Type type = property.PropertyType;
        string[] expected = ["None", "InvalidInput", "ProposalShape", "SectionOrder", "ClaimShape", "DocumentReference", "SectionReference", "ClaimReference",
            "QualificationMismatch", "InputClaimShape", "OutputLimit", "InvalidStructure", "ResponseMissing", "FinishReasonNotStop", "ResponseLengthLimit",
            "ResponseShape", "ResponsePayloadLimit", "ProposalMissing", "JsonInvalid", "TransportFailure", "ExecutionUnavailable", "UnsupportedRoot", "NotEvaluated"];
        Assert.Equal(expected, Enum.GetNames(type));
        Assert.Equal(Enumerable.Range(0, expected.Length), Enum.GetValues(type).Cast<object>().Select(Convert.ToInt32));
        SynthesisDiagnosticProbe.Set(result, "None");
        string ordinary = JsonSerializer.Serialize(result);
        string canonical = OctoberKnowledgePolicy.SerializeCanonical(result);
        SynthesisDiagnosticProbe.Set(result, "SectionOrder");
        Assert.Equal("SectionOrder", SynthesisDiagnosticProbe.Read(result));
        Assert.Equal(ordinary, JsonSerializer.Serialize(result));
        Assert.Equal(canonical, OctoberKnowledgePolicy.SerializeCanonical(result));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ExecutorDiagnostic_NonzeroReason_DoesNotChangeOrdinaryOrCanonicalSerialization()
    {
        var result = new BusinessSynthesisResult(null, null, null, []);
        string ordinary = JsonSerializer.Serialize(result);
        string canonical = OctoberKnowledgePolicy.SerializeCanonical(result);
        SynthesisDiagnosticProbe.Set(result, "SectionOrder");
        Assert.Equal("SectionOrder", SynthesisDiagnosticProbe.Read(result));
        Assert.Equal(ordinary, JsonSerializer.Serialize(result));
        Assert.Equal(canonical, OctoberKnowledgePolicy.SerializeCanonical(result));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void EmbeddedSynthesisPrompt_ExplainsEnforcedStructuralRules()
    {
        using var stream = typeof(OctoberBusinessSynthesisExecutor).Assembly.GetManifestResourceStream("Vulgata.Agents.Prompts.OctoberBusiness.Synthesis.md");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        string prompt = reader.ReadToEnd();
        Assert.Matches(@"(?m)^.*Ordinal.*0.*$", prompt);
        Assert.Matches(@"(?m)^.*SectionId.*唯一.*$", prompt);
        Assert.Matches(@"(?m)^.*ClaimGroupId.*唯一.*$", prompt);
        Assert.Contains(OctoberBusinessSynthesisPolicy.GapSectionId, prompt, StringComparison.Ordinal);
        Assert.Contains(OctoberBusinessSynthesisPolicy.GapClaimId, prompt, StringComparison.Ordinal);
        Assert.Matches(@"(?m)^.*Limitations.*\[\].*$", prompt);
        Assert.Matches(@"(?m)^.*Inputs.*重复.*$", prompt);
        Assert.Contains("DocumentVersionId", prompt, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_MissingHostHandoffLifetime_FailsClosedBeforeProviderOrAdmissionAsync()
    {
        var f = new SynthesisCase();
        var scope = new SynthesisMissingLifetimeScope();
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(f.Response()));
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), new SynthesisClock());
        Assert.Equal(ProviderAdmissionStatus.AuthorityUnavailable, result.AdmissionFailure);
        Assert.Null(result.Content); Assert.Null(result.Proposal); Assert.Null(result.Dependencies);
        Assert.Empty(result.Gaps); Assert.Empty(result.Coverage); Assert.Empty(result.PlanDispositions); Assert.Empty(result.Attempts);
        Assert.Equal(0, scope.Acquisitions); Assert.Equal(0, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false, ProviderAdmissionStatus.HardStopped)]
    [InlineData(true, ProviderAdmissionStatus.DeadlineExceeded)]
    public async Task ExecuteAsync_ActualHostLifetimeEndsDuringPostSettlementAuthority_SuppressesLateAllowedContentAsync(
        bool runDeadline, ProviderAdmissionStatus expectedFailure)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock, runDeadline ? 180 : 1800);
        // The host run predates this synthesis call: 60 seconds remain from its original 180-second allowance.
        if (runDeadline) clock.Advance(TimeSpan.FromSeconds(120));
        var authority = new SynthesisHandoffDisclosure(f.Input.Contributors);
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(f.Response()));
        var active = f.ExecuteAsync(client, scope, authority, clock);
        try
        {
            await authority.HandoffEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, client.Calls);
            Assert.False(active.IsCompleted);
            // A second actual host lease can acquire the sole slot only after the physical attempt is settled.
            clock.Advance(TimeSpan.FromSeconds(1));
            var capacity = await scope.AcquireWorkerAttemptAsync(f.Worker(1)).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(ProviderAdmissionStatus.Granted, capacity.Status);
            Assert.NotNull(capacity.Lease);
            await capacity.Lease.DisposeAsync(); // No provider dispatch; returns the probe reservation and slot.
            if (runDeadline) clock.Advance(TimeSpan.FromSeconds(60));
            else scope.HardStop();
            // Verify actual host state, independently of the now-released synthesis attempt token.
            Assert.Equal(expectedFailure, (await scope.AcquireWorkerAttemptAsync(f.Worker(1))).Status);
        }
        finally
        {
            authority.Release.TrySetResult();
            await active.WaitAsync(TimeSpan.FromSeconds(5));
        }
        var result = await active;
        Assert.Null(result.Content);
        Assert.Null(result.Proposal);
        Assert.Null(result.Dependencies);
        Assert.Empty(result.Gaps);
        Assert.Empty(result.Coverage);
        Assert.Empty(result.PlanDispositions);
        Assert.Equal(expectedFailure, result.AdmissionFailure);
        Assert.Equal(1, client.Calls);
        Assert.Single(result.Attempts);
        Assert.Equal(3, authority.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DisclosureStatus.Denied, ProviderAdmissionStatus.Denied)]
    [InlineData(DisclosureStatus.AuthorityUnavailable, ProviderAdmissionStatus.AuthorityUnavailable)]
    public async Task ExecuteAsync_AuthorityChangesDuringAwaitedSettlement_RechecksCompleteLedgerBeforeReturningContentAsync(
        DisclosureStatus changedStatus, ProviderAdmissionStatus expectedFailure)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var actualScope = f.Scope(clock);
        bool settled = false;
        var scope = new SynthesisSettlementScope(actualScope, () => settled = true);
        var authority = new SynthesisDisclosure
        {
            Decide = contributors =>
            {
                Assert.True(f.Input.Contributors.ToHashSet().SetEquals(contributors));
                Assert.Contains(f.UncitedContributor, contributors);
                Assert.Contains(f.CoverageContributor, contributors);
                return settled ? changedStatus : DisclosureStatus.Allowed;
            }
        };
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(f.Response()));

        var result = await f.ExecuteAsync(client, scope, authority, clock);

        Assert.True(settled);
        Assert.False(scope.LeaseCancellationObserved);
        Assert.Null(result.Content);
        Assert.Null(result.Proposal);
        Assert.Null(result.Dependencies);
        Assert.Empty(result.Gaps);
        Assert.Empty(result.Coverage);
        Assert.Empty(result.PlanDispositions);
        Assert.Equal(expectedFailure, result.AdmissionFailure);
        Assert.Equal(1, client.Calls);
        Assert.Equal(1, scope.SettleCalls);
        Assert.Equal(1, scope.DisposeCalls);
        Assert.True(authority.Seen.Count >= 3);
        Assert.True(f.Input.Contributors.ToHashSet().SetEquals(authority.Seen.Last()));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_SuppliedClaimOutsideFlowMembers_IsVisiblySupplementalWithoutGraphExpansion()
    {
        var f = new SynthesisCase();
        var outside = f.Input.Corpus.Documents[3].Version;
        Assert.DoesNotContain(outside, f.Group.Members);
        string original = OctoberKnowledgePolicy.SerializeCanonical(f.Input);
        var proposal = f.Proposal();
        var section = proposal.Sections[0];
        var claim = section.Claims[0] with { Inputs = [new(outside, "operation", "provider")] };
        var result = f.Validate(proposal with { Sections = proposal.Sections.SetItem(0, section with { Claims = section.Claims.SetItem(0, claim) }) });
        Assert.Equal(BusinessSynthesisValidationStatus.Succeeded, result.Status);
        var supplemental = result.Content!.Sections.SelectMany(x => x.Claims).Single(x => x.ClaimGroupId == "business-action");
        Assert.StartsWith("补充背景（未确认为本流程成员）：", supplemental.Text, StringComparison.Ordinal);
        Assert.Equal(ClaimQualification.SourceSupported, supplemental.Qualification);
        Assert.DoesNotContain(outside, f.Group.Members);
        Assert.Equal(original, OctoberKnowledgePolicy.SerializeCanonical(f.Input));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Group.Dependencies), OctoberKnowledgePolicy.SerializeCanonical(result.Dependencies));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Input.Coverage), OctoberKnowledgePolicy.SerializeCanonical(result.Coverage));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Input.PlanDispositions), OctoberKnowledgePolicy.SerializeCanonical(result.PlanDispositions));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_QualifiedProse_DerivesExactEvidencePreservesLimitationsAndEntireSuppliedLedger()
    {
        var f = new SynthesisCase();
        string original = OctoberKnowledgePolicy.SerializeCanonical(f.Input);
        var result = f.Validate(f.Proposal());
        Assert.Equal(BusinessSynthesisValidationStatus.Succeeded, result.Status);
        var content = Assert.IsType<BusinessLogicContent>(result.Content);
        var claims = content.Sections.SelectMany(x => x.Claims).ToArray();
        var source = Assert.Single(claims.Where(x => x.ClaimGroupId == "business-action"));
        var accepted = f.Claim("call-1-binding");
        Assert.Equal(ClaimQualification.SourceSupported, source.Qualification);
        Assert.All(accepted.Limitations, limit => Assert.Contains(limit, source.Limitations));
        Assert.Contains(source.Evidence, evidence => evidence.Document == f.Root && evidence.ClaimGroupId == "call-1-binding" &&
            evidence.Source == accepted.Evidence[0].Source && evidence.ContentDigest == accepted.Evidence[0].ContentDigest);
        Assert.Equal(ClaimQualification.Advisory, claims.Single(x => x.ClaimGroupId == "business-advice").Qualification);
        Assert.Equal(ClaimQualification.Hypothesis, claims.Single(x => x.ClaimGroupId == "business-unknown").Qualification);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Group.Dependencies), OctoberKnowledgePolicy.SerializeCanonical(result.Dependencies));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Input.Coverage), OctoberKnowledgePolicy.SerializeCanonical(result.Coverage));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Input.PlanDispositions), OctoberKnowledgePolicy.SerializeCanonical(result.PlanDispositions));
        Assert.Contains(f.UncitedContributor, result.Dependencies!.Contributors);
        Assert.Contains(f.CoverageContributor, result.Dependencies.Contributors);
        Assert.Contains(f.Input.Corpus.Documents[3].Version, result.Dependencies.Documents);
        Assert.Equal(2, result.Dependencies.FileUncertainties.Length);
        Assert.Equal(original, OctoberKnowledgePolicy.SerializeCanonical(f.Input));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_ModelOmitsKnownGaps_AppRetainsEveryGapAndOneUncertainGenericSummary()
    {
        var f = new SynthesisCase();
        var result = f.Validate(f.Proposal());
        Assert.Equal(BusinessSynthesisValidationStatus.Succeeded, result.Status);
        Assert.All(Enum.GetValues<SynthesisGapKind>(), kind => Assert.Contains(result.Gaps, gap => gap.Locator.Kind == kind));
        var fileGaps = result.Gaps.Where(x => x.Locator.Kind == SynthesisGapKind.FileUncertainty).ToArray();
        Assert.Equal(2, fileGaps.Length);
        Assert.Equal(2, fileGaps.Select(x => x.Locator.Key).Distinct().Count());
        Assert.All(fileGaps, gap => Assert.Contains(SynthesisCase.PrivatePath, gap.Explanation + string.Join(" ", gap.Limitations), StringComparison.Ordinal));
        var section = Assert.Single(result.Content!.Sections.Where(x => x.SectionId == OctoberBusinessSynthesisPolicy.GapSectionId));
        var summary = Assert.Single(section.Claims);
        Assert.Equal(OctoberBusinessSynthesisPolicy.GapClaimId, summary.ClaimGroupId);
        Assert.Equal(ClaimQualification.Uncertain, summary.Qualification);
        Assert.DoesNotContain(SynthesisCase.PrivatePath, summary.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("same-file-key", summary.Text, StringComparison.Ordinal);
        Assert.Contains(SynthesisCase.PrivatePath, string.Join(" ", summary.Limitations), StringComparison.Ordinal);
        var explicitGaps = OctoberBusinessSynthesisPolicy.DescribeGaps(f.Input, f.Group.GroupVersionId, f.Limits);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(result.Gaps), OctoberKnowledgePolicy.SerializeCanonical(explicitGaps));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ValidatedBl_ActualBusinessReader_HidesInternalTechnicalGapsWhileKeepingGenericUncertaintyAsync()
    {
        var f = new SynthesisCase();
        var result = f.Validate(f.Proposal());
        Assert.Equal(BusinessSynthesisValidationStatus.Succeeded, result.Status);
        var content = result.Content!;
        var publication = new PublicationBinding(f.Input.Corpus.Handle.Binding, Guid.NewGuid());
        var version = new DocumentVersionReference(publication.Knowledge.BatchId, Guid.NewGuid(), Guid.NewGuid());
        var document = new OctoberPublishedDocument(version, DocumentKind.BusinessLogic, content.Title, content.ContentJson, content.Sections, result.Dependencies!.Contributors);
        var source = new B0bPublishedSource(new(publication, [document], [], [], []));
        var reader = new OctoberKnowledgeReader(source, publication, new B0bReaderDisclosure(),
            new OctoberKnowledgeCursorProtector(new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider()), new B0bReadClock()), new B0bReadLog());
        var scope = f.Input.Contributors.Where(x => x.RepositoryId is not null).Select(x => x.RepositoryId!.Value).Distinct().ToImmutableArray();
        var selection = await reader.OpenReadSelectionAsync(new(publication, new("ordinary-user", "controlled-stamp"), KnowledgeMode.Business, KnowledgePurpose.Reader, scope));
        Assert.Equal(KnowledgeReadStatus.Success, selection.Status);
        var read = await reader.ReadExactAsync(new(selection.Selection!, new(version.BatchId, publication.PublicationId, version.LogicalDocumentId, version.DocumentVersionId)));
        Assert.Equal(KnowledgeReadStatus.Success, read.Status);
        Assert.DoesNotContain(SynthesisCase.PrivatePath, read.Content!, StringComparison.Ordinal);
        Assert.DoesNotContain("same-file-key", read.Content!, StringComparison.Ordinal);
        Assert.All(read.Claims, claim => Assert.Empty(claim.Evidence));
        Assert.Contains(read.Claims, claim => claim.Text == content.Sections.Single(x => x.SectionId == OctoberBusinessSynthesisPolicy.GapSectionId).Claims.Single().Text && claim.Qualification == ClaimQualification.Uncertain);
        // Internal provenance remains exact; Business projection must not erase it from canonical storage.
        Assert.Contains(SynthesisCase.PrivatePath, content.ContentJson, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("document")]
    [InlineData("section")]
    [InlineData("claim")]
    [InlineData("upgrade")]
    [InlineData("mixed")]
    [InlineData("reserved-gap")]
    [InlineData("invented-input")]
    public void Validate_InventedEvidenceQualificationOrReservedGap_ReturnsNoContentOrLedger(string mutation)
    {
        var f = new SynthesisCase();
        var proposal = f.Proposal();
        var section = proposal.Sections[0];
        var claim = section.Claims[0];
        claim = mutation switch
        {
            "document" => claim with { Inputs = [claim.Inputs[0] with { Document = f.Root with { DocumentVersionId = Guid.NewGuid() } }] },
            "section" => claim with { Inputs = [claim.Inputs[0] with { SectionId = "invented" }] },
            "claim" => claim with { Inputs = [claim.Inputs[0] with { ClaimGroupId = "invented" }] },
            "upgrade" => claim with { Inputs = [new(f.Root, "operation", "advice")] },
            "mixed" => claim with { Qualification = ClaimQualification.Mixed, Inputs = claim.Inputs.Add(new(f.Root, "operation", "advice")) },
            "invented-input" => claim with { Inputs = [] },
            _ => claim
        };
        section = section with { Claims = section.Claims.SetItem(0, claim) };
        if (mutation == "reserved-gap") section = section with { SectionId = OctoberBusinessSynthesisPolicy.GapSectionId };
        var result = f.Validate(proposal with { Sections = proposal.Sections.SetItem(0, section) });
        Assert.Equal(BusinessSynthesisValidationStatus.InvalidProposal, result.Status);
        Assert.Null(result.Content); Assert.Null(result.Dependencies);
        Assert.Equal(mutation switch
        {
            "document" => "DocumentReference", "section" => "SectionReference", "claim" => "ClaimReference",
            "upgrade" => "QualificationMismatch", "reserved-gap" => "ProposalShape", _ => "ClaimShape"
        }, SynthesisDiagnosticProbe.Read(result));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ActualScope_UsesSynthesisLeaseCompleteAcceptedInputAndQualifiedOutputWithoutToolsAsync()
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        var spy = new SynthesisScopeSpy(scope);
        var authority = new SynthesisDisclosure();
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(f.Response()));
        var result = await f.ExecuteAsync(client, spy, authority, clock);
        Assert.NotNull(result.Content); Assert.NotNull(result.Dependencies); Assert.NotNull(result.Proposal);
        Assert.Null(result.AdmissionFailure);
        Assert.Equal(1, client.Calls);
        Assert.Equal(BusinessWorkKind.Synthesis, Assert.Single(spy.Requests).WorkKind);
        Assert.InRange(spy.Requests[0].MaximumDuration, TimeSpan.FromTicks(1), TimeSpan.FromSeconds(180));
        var sent = Assert.Single(client.Requests);
        Assert.Empty(sent.Tools);
        string input = string.Join("\n", sent.Messages.Where(x => x.Role == ChatRole.User).Select(x => x.Text));
        Assert.Contains(f.Input.Corpus.Documents[3].Version.DocumentVersionId.ToString(), input, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(f.CoverageContributor.RepositoryId!.Value.ToString(), input, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("same-file-key", input, StringComparison.Ordinal);
        Assert.All(authority.Seen, contributors => Assert.True(f.Input.Contributors.ToHashSet().SetEquals(contributors)));
        Assert.True(authority.Seen.Count >= 2);
        Assert.Equal(10, Assert.Single(result.Attempts).InputTokens);
        Assert.Equal(5, result.Attempts[0].OutputTokens);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Group.Dependencies), OctoberKnowledgePolicy.SerializeCanonical(result.Dependencies));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Input.Coverage), OctoberKnowledgePolicy.SerializeCanonical(result.Coverage));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(f.Input.PlanDispositions), OctoberKnowledgePolicy.SerializeCanonical(result.PlanDispositions));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("outage-after")]
    public async Task ExecuteAsync_UncitedOrCoverageContributorDeniedOrAuthorityOutage_SuppressesOutputWithoutRetryAsync(string boundary)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        bool providerReturned = false;
        var authority = new SynthesisDisclosure
        {
            Decide = contributors =>
            {
                Assert.Contains(f.UncitedContributor, contributors);
                Assert.Contains(f.CoverageContributor, contributors);
                if (boundary == "outage-after" && providerReturned) throw new IOException("Controlled authority outage after response.");
                return boundary == "before" || providerReturned ? DisclosureStatus.Denied : DisclosureStatus.Allowed;
            }
        };
        using var client = new ResolutionClient("selected-profile", (_, _) => { providerReturned = true; return Task.FromResult(f.Response()); });
        var result = await f.ExecuteAsync(client, scope, authority, clock);
        Assert.Null(result.Content); Assert.Null(result.Dependencies); Assert.Null(result.Proposal);
        Assert.Equal(boundary == "outage-after" ? ProviderAdmissionStatus.AuthorityUnavailable : ProviderAdmissionStatus.Denied, result.AdmissionFailure);
        Assert.Equal(boundary == "before" ? 0 : 1, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("relationships")]
    [InlineData("dependencies")]
    [InlineData("logicalDocumentId")]
    public async Task ExecuteAsync_ModelAddsAuthoritativeFields_RejectsProposalWithoutPublishingOrRetryAsync(string field)
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        string before = OctoberKnowledgePolicy.SerializeCanonical(f.Input);
        var json = JsonSerializer.SerializeToNode(f.Proposal())!.AsObject();
        json[field] = field == "logicalDocumentId" ? JsonValue.Create(Guid.NewGuid().ToString()) : new JsonArray();
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(SynthesisCase.JsonResponse(json.ToJsonString())));
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.Null(result.Content); Assert.Null(result.Dependencies); Assert.Null(result.Proposal);
        Assert.Equal(1, client.Calls);
        Assert.Equal(before, OctoberKnowledgePolicy.SerializeCanonical(f.Input));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_TransportRetry_UsesTwoFreshAttemptsWithinOriginal180SecondsAndSharedReservationAsync()
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        var scope = new ResolutionScopeProbe(); // Controlled host retains a conservative unknown reservation and explicitly permits one retry.
        using var client = new ResolutionClient("selected-profile", (attempt, _) =>
        {
            if (attempt == 1) { clock.Advance(TimeSpan.FromSeconds(130)); throw new IOException("Controlled transient transport failure."); }
            return Task.FromResult(f.Response());
        });
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.NotNull(result.Content);
        Assert.Equal(2, client.Calls);
        Assert.Equal(2, scope.Requests.Count);
        Assert.All(scope.Requests, request => Assert.Equal(BusinessWorkKind.Synthesis, request.WorkKind));
        Assert.NotEqual(scope.Requests[0].AttemptId, scope.Requests[1].AttemptId);
        Assert.InRange(scope.Requests[1].MaximumDuration, TimeSpan.FromTicks(1), TimeSpan.FromSeconds(50));
        Assert.Null(result.Attempts[0].InputTokens); Assert.Null(result.Attempts[0].OutputTokens);
        Assert.Equal(45, scope.Inner.RemainingTokens);
        Assert.Equal(2, scope.SettleCalls); Assert.Equal(2, scope.DisposeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ExpiredOriginalDeadline_DoesNotBeginFreshRetryAllowanceAsync()
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        var scope = new ResolutionScopeProbe();
        using var client = new ResolutionClient("selected-profile", (_, _) => { clock.Advance(TimeSpan.FromSeconds(181)); throw new IOException("Controlled delayed failure."); });
        var result = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.Equal(ProviderAdmissionStatus.DeadlineExceeded, result.AdmissionFailure);
        Assert.Null(result.Content);
        Assert.Equal(1, client.Calls); Assert.Single(scope.Requests);
        Assert.Equal(1, scope.SettleCalls); Assert.Equal(1, scope.DisposeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ActualScopeUnknownUsage_BlocksFurtherWorkerAndSynthesisBudgetAsync()
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(f.Response(false)));
        var first = await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Assert.NotNull(first.Content);
        Assert.Null(Assert.Single(first.Attempts).InputTokens);
        Assert.Null(first.Attempts[0].OutputTokens); Assert.Null(first.Attempts[0].Cost);
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, (await scope.AcquireWorkerAttemptAsync(f.Worker(1))).Status);
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, (await f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock)).AdmissionFailure);
        Assert.Equal(1, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ActualSharedCapAndHardStop_KeepsUnsettledTransportOwnedDeniesQueuedAndRejectsLateContentAsync()
    {
        var f = new SynthesisCase();
        var clock = new SynthesisClock();
        await using var scope = f.Scope(clock);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ResolutionClient("selected-profile", async (_, _) => { await release.Task; return f.Response(); });
        var active = f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
        Task<BusinessSynthesisResult>? queued = null;
        try
        {
            await client.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            queued = f.ExecuteAsync(client, scope, new SynthesisDisclosure(), clock);
            Assert.False(queued.IsCompleted); Assert.Equal(1, client.Calls);
            scope.HardStop();
            Assert.True(client.LastCancellation.IsCancellationRequested);
            Assert.False(active.IsCompleted);
            Assert.Equal(ProviderAdmissionStatus.HardStopped, (await queued.WaitAsync(TimeSpan.FromSeconds(5))).AdmissionFailure);
            var cleanup = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask());
            Assert.Equal("october_provider_settlement_unconfirmed", cleanup.Message);
        }
        finally { release.TrySetResult(); await active.WaitAsync(TimeSpan.FromSeconds(5)); if (queued is not null) await queued.WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.Null((await active).Content);
        Assert.Equal(ProviderAdmissionStatus.HardStopped, (await active).AdmissionFailure);
        Assert.Equal(1, client.Calls); Assert.Equal(1, client.PeakActive);
    }
}

/// <summary>Builds a controlled provider proposal exclusively from the actual SDK request's accepted-input chain.</summary>
internal sealed class SynthesisWireProposalHandler(bool substituteOutputSection) : HttpMessageHandler
{
    public const string OutputSectionId = "generated-business-section";
    public const string OutputClaimId = "generated-business-claim";
    public int Calls { get; private set; }
    public SynthesisClaimInput? SelectedInput { get; private set; }
    public ClaimQualification SelectedQualification { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        using var wire = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        var user = Assert.Single(wire.RootElement.GetProperty("messages").EnumerateArray().Where(message => message.GetProperty("role").GetString() == "user"));
        var content = user.GetProperty("content");
        string payload = content.ValueKind == JsonValueKind.String ? content.GetString()! :
            Assert.Single(content.EnumerateArray().Where(part => part.GetProperty("type").GetString() == "text")).GetProperty("text").GetString()!;
        using var accepted = JsonDocument.Parse(payload);
        var dependencies = accepted.RootElement.GetProperty("group").GetProperty("Dependencies").GetProperty("Documents").EnumerateArray()
            .Select(item => JsonSerializer.Deserialize<DocumentVersionReference>(item.GetRawText())!).ToHashSet();
        var document = accepted.RootElement.GetProperty("input").GetProperty("Corpus").GetProperty("Documents").EnumerateArray()
            .First(item => dependencies.Contains(JsonSerializer.Deserialize<DocumentVersionReference>(item.GetProperty("Version").GetRawText())!));
        var version = JsonSerializer.Deserialize<DocumentVersionReference>(document.GetProperty("Version").GetRawText())!;
        var section = document.GetProperty("Sections").EnumerateArray().First(item => item.GetProperty("Claims").GetArrayLength() > 0);
        var claim = section.GetProperty("Claims").EnumerateArray().First(item => item.GetProperty("Qualification").GetInt32() == (int)ClaimQualification.SourceSupported);
        SelectedInput = new(version, section.GetProperty("SectionId").GetString()!, claim.GetProperty("ClaimGroupId").GetString()!);
        SelectedQualification = (ClaimQualification)claim.GetProperty("Qualification").GetInt32();
        Assert.False(document.GetProperty("Sections").EnumerateArray().Any(item => item.GetProperty("SectionId").GetString() == OutputSectionId));
        var reference = substituteOutputSection ? SelectedInput with { SectionId = OutputSectionId } : SelectedInput;
        var proposal = new BusinessSynthesisProposal("业务处理说明", [new(OutputSectionId, 0, "处理结果",
            [new(OutputClaimId, "根据已接受的操作说明处理业务请求。", SelectedQualification, [reference], [])])]);
        string json = JsonSerializer.Serialize(new
        {
            id = "controlled-wire-locators", @object = "chat.completion", created = 1, model = "controlled-model",
            choices = new[] { new { index = 0, message = new { role = "assistant", content = JsonSerializer.Serialize(proposal) }, finish_reason = "stop" } },
            usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
        });
        return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}

/// <summary>Installed SDK over controlled HTTP only; response values are never written to diagnostics.</summary>
internal sealed class SynthesisSdkResponseHandler(string answer, string shape) : HttpMessageHandler
{
    public const string ReasoningMarker = "controlled-private-non-answer-metadata";
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        var message = new JsonObject { ["role"] = "assistant", ["content"] = shape == "reasoning-only" ? null : shape == "invalid-answer" ? "{" : answer };
        if (shape != "plain") message["reasoning_content"] = shape is "reasoning-only" or "invalid-answer" ? answer : ReasoningMarker;
        string json = JsonSerializer.Serialize(new
        {
            id = "controlled-sdk", @object = "chat.completion", created = 1, model = "controlled-model",
            choices = new[] { new { index = 0, message, finish_reason = "stop" } },
            usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
        });
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
internal sealed class SynthesisSdkSecret : ISecretEncryptionService
{
    public Task<string> EncryptAsync(string purpose, string plain, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<string> DecryptAsync(string purpose, string cipher, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.True(cipher == "controlled-protected");
        return Task.FromResult("controlled-synthetic-token");
    }
}
internal sealed class SynthesisUnknownContent : AIContent { }

/// <summary>Controlled accepted-input fixture; real pure planner and real host scope, without source/provider runtime qualification.</summary>
internal sealed class SynthesisCase
{
    public const string PrivatePath = "private/sentinel-payments-secret.cs";
    public FlowPlanningInput Input { get; }
    public FlowGroupVersion Group { get; }
    public FlowPlanningLimits Limits { get; } = new(1, 512, 16);
    public DocumentVersionReference Root => Input.Corpus.Documents[0].Version;
    public Contributor UncitedContributor { get; }
    public Contributor CoverageContributor { get; }
    public Guid Run { get; } = ResolutionCase.Id(97001);
    public OperationKey Operation => new(Input.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, ResolutionCase.Id(97002));

    public SynthesisCase()
    {
        var basis = new FlowPlannerCase(4, [(0, 1), (2, 3)], [0], unsupportedObservation: true);
        UncitedContributor = basis.UncitedAdvisory;
        var origin = new PreparedOrigin(ResolutionCase.Id(96000), "controlled-coverage-root", PrivatePath, new string('d', 64), "controlled-v1");
        CoverageContributor = new(ContributorKind.Source, origin.RepositoryId, null, origin.RootBindingIdentity, null, origin.SourceDigest);
        var evidence = basis.Input.Corpus.Documents[0].Sections[0].Claims[0].Evidence;
        Input = basis.Input with
        {
            Coverage = basis.Input.Coverage.Add(new(ResolutionCase.Id(96001), origin, CoverageDisposition.Unprocessed, "Unprocessed controlled source " + PrivatePath)),
            Contributors = basis.Input.Contributors.Add(CoverageContributor),
            Resolutions = basis.Input.Resolutions.Select(item => item with
            {
                Outcome = item.Outcome with { Contributors = basis.Input.Contributors.Add(CoverageContributor) }
            }).ToImmutableArray(),
            FileUncertainties =
            [
                new(basis.Input.Corpus.Handle.Binding.BatchId, ResolutionCase.Id(96002), "same-file-key", new("same-file-key", "First file unknown: " + PrivatePath, evidence, ["First limitation " + PrivatePath])),
                new(basis.Input.Corpus.Handle.Binding.BatchId, ResolutionCase.Id(96003), "same-file-key", new("same-file-key", "Second file unknown: " + PrivatePath, evidence, ["Second limitation " + PrivatePath]))
            ]
        };
        var plan = OctoberFlowPlanner.Plan(Input, Limits);
        Assert.Equal(FlowPlanningStatus.Succeeded, plan.Status);
        Group = Assert.Single(plan.Groups);
    }

    public QualifiedClaimGroup Claim(string id) => Input.Corpus.Documents[0].Sections.SelectMany(x => x.Claims).Single(x => x.ClaimGroupId == id);
    public BusinessSynthesisProposal Proposal() => new("业务处理流程",
    [
        new("business-main", 0, "操作、条件与结果",
        [
            new("business-action", "满足处理条件时，将请求交给后续处理环节。", ClaimQualification.SourceSupported, [new(Root, "operation", "call-1-binding")], []),
            new("business-advice", "相关操作建议仍需业务负责人确认。", ClaimQualification.Advisory, [new(Root, "operation", "advice")], []),
            new("business-unknown", "当前运行环境是否启用此流程尚待确认。", ClaimQualification.Hypothesis, [new(Root, "operation", "hypothesis")], [])
        ])
    ]);
    public BusinessSynthesisValidationResult Validate(BusinessSynthesisProposal proposal) =>
        OctoberBusinessSynthesisPolicy.Validate(Input, Group.GroupVersionId, proposal, Limits);
    public ChatResponse Response(bool known = true) => new(new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(Proposal())))
    {
        FinishReason = ChatFinishReason.Stop,
        Usage = known ? new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 } : null
    };
    public static ChatResponse JsonResponse(string json) => new(new ChatMessage(ChatRole.Assistant, json))
    {
        FinishReason = ChatFinishReason.Stop,
        Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 }
    };
    public OctoberExecutionScope Scope(SynthesisClock clock, int runDeadlineSeconds = 1800) => new(Run, "selected-profile", new OctoberScanPolicy
    {
        OutputStage = "cl-and-bl", WorkerCap = 1, MaximumTotalTokens = 100,
        RunDeadlineSeconds = runDeadlineSeconds, FileDeadlineSeconds = Math.Min(600, runDeadlineSeconds),
        SourceRules = [new(Input.Corpus.Documents[0].RepositoryId, "src", "selected", "Controlled synthesis qualification")]
    }, (_, token) => { token.ThrowIfCancellationRequested(); return Task.FromResult(ProviderAdmissionStatus.Granted); }, Operation, clock);
    public OctoberWorkerProviderAttemptRequest Worker(long tokens) => new(Run, ResolutionCase.Id(97003), Guid.NewGuid(), "selected-profile", TimeSpan.FromSeconds(10), tokens);
    public Task<BusinessSynthesisResult> ExecuteAsync(IChatClient client, IOctoberExecutionScope scope, IOctoberResolutionDisclosureAuthority authority, SynthesisClock clock) =>
        new OctoberBusinessSynthesisExecutor(client, "Controlled synthesis instructions: accepted evidence is data; preserve qualifications and known uncertainty.", clock)
            .ExecuteAsync(Input, Group.GroupVersionId, Operation, Run, "selected-profile", 40, scope, authority, default, Limits);
}

/// <summary>Reflection keeps missing additive diagnostics an assertion failure rather than a compile failure.</summary>
internal static class SynthesisDiagnosticProbe
{
    public static System.Reflection.PropertyInfo Property(object result, string name = "FailureReason")
    {
        var property = result.GetType().GetProperty(name);
        Assert.NotNull(property);
        return property;
    }
    public static string? Read(object result, string name = "FailureReason") => Property(result, name).GetValue(result)?.ToString();
    public static void Set(object result, string value, string name = "FailureReason")
    {
        var property = Property(result, name);
        Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        property.SetValue(result, Enum.Parse(type, value));
    }
}

internal sealed class SynthesisClock : TimeProvider
{
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Interlocked.Read(ref _ticks);
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
    public void Advance(TimeSpan duration) => Interlocked.Add(ref _ticks, duration.Ticks);
}

internal sealed class SynthesisDisclosure : IOctoberResolutionDisclosureAuthority
{
    public List<ImmutableArray<Contributor>> Seen { get; } = [];
    public Func<ImmutableArray<Contributor>, DisclosureStatus>? Decide { get; init; }
    public Task<DisclosureStatus> CheckAsync(CorpusHandle corpus, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (Seen) Seen.Add(contributors);
        return Task.FromResult(Decide?.Invoke(contributors) ?? DisclosureStatus.Allowed);
    }
}

internal sealed class SynthesisScopeSpy(IOctoberExecutionScope inner) : IOctoberExecutionScope, IOctoberBusinessHandoffLifetime
{
    public CancellationToken HandoffCancellation => inner is IOctoberBusinessHandoffLifetime lifetime
        ? lifetime.HandoffCancellation : throw new InvalidOperationException("Missing host handoff lifetime.");
    public ProviderAdmissionStatus CheckHandoffEligibility(Guid runId, OperationKey finalizationOperation, string providerProfileId) =>
        inner is IOctoberBusinessHandoffLifetime lifetime ? lifetime.CheckHandoffEligibility(runId, finalizationOperation, providerProfileId)
            : ProviderAdmissionStatus.AuthorityUnavailable;
    public List<ProviderAttemptRequest> Requests { get; } = [];
    public Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default)
    {
        lock (Requests) Requests.Add(request);
        return inner.AcquireProviderAttemptAsync(request, cancellationToken);
    }
}




/// <summary>Delegates to the real host lease; models authority changing while settlement is awaited, without cancelling its token.</summary>
internal sealed class SynthesisSettlementScope(IOctoberExecutionScope inner, Action afterSettlement) : IOctoberExecutionScope, IOctoberBusinessHandoffLifetime
{
    public CancellationToken HandoffCancellation => inner is IOctoberBusinessHandoffLifetime lifetime
        ? lifetime.HandoffCancellation : throw new InvalidOperationException("Missing host handoff lifetime.");
    public ProviderAdmissionStatus CheckHandoffEligibility(Guid runId, OperationKey finalizationOperation, string providerProfileId) =>
        inner is IOctoberBusinessHandoffLifetime lifetime ? lifetime.CheckHandoffEligibility(runId, finalizationOperation, providerProfileId)
            : ProviderAdmissionStatus.AuthorityUnavailable;
    private readonly Action _afterSettlement = afterSettlement;
    public int SettleCalls { get; private set; }
    public int DisposeCalls { get; private set; }
    public bool LeaseCancellationObserved { get; private set; }
    public async Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default)
    {
        var admission = await inner.AcquireProviderAttemptAsync(request, cancellationToken);
        return admission.Lease is { } lease ? admission with { Lease = new SettlementLease(this, lease) } : admission;
    }
    private sealed class SettlementLease(SynthesisSettlementScope owner, IOctoberProviderAttemptLease innerLease) : IOctoberProviderAttemptLease, IOctoberWorkerDispatchObserver
    {
        private IOctoberWorkerDispatchObserver Observer => innerLease as IOctoberWorkerDispatchObserver
            ?? throw new InvalidOperationException("Missing inner lease dispatch observer.");
        public void RecordDispatch() => Observer.RecordDispatch();
        public void CompleteDispatchObservation() => Observer.CompleteDispatchObservation();
        public Guid AttemptId => innerLease.AttemptId;
        public CancellationToken CancellationToken => innerLease.CancellationToken;
        public Task<ProviderAdmissionStatus> CheckBeforeSendAsync(CancellationToken cancellationToken = default) => innerLease.CheckBeforeSendAsync(cancellationToken);
        public async Task SettleAsync(ProviderAttemptUsage usage, CancellationToken cancellationToken = default)
        {
            owner.SettleCalls++;
            await innerLease.SettleAsync(usage, cancellationToken);
            owner.LeaseCancellationObserved |= innerLease.CancellationToken.IsCancellationRequested;
            owner._afterSettlement();
        }
        public async ValueTask DisposeAsync()
        {
            owner.DisposeCalls++;
            owner.LeaseCancellationObserved |= innerLease.CancellationToken.IsCancellationRequested;
            await innerLease.DisposeAsync();
        }
    }
}

/// <summary>Pauses only the post-settlement full-contributor check and then returns Allowed even if the old attempt token changed.</summary>
internal sealed class SynthesisHandoffDisclosure(ImmutableArray<Contributor> expected) : IOctoberResolutionDisclosureAuthority
{
    private int _calls;
    public int Calls => Volatile.Read(ref _calls);
    public TaskCompletionSource HandoffEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<DisclosureStatus> CheckAsync(CorpusHandle corpus, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default)
    {
        Assert.True(expected.ToHashSet().SetEquals(contributors));
        if (Interlocked.Increment(ref _calls) == 3)
        {
            HandoffEntered.TrySetResult();
            await Release.Task;
        }
        return DisclosureStatus.Allowed;
    }
}


internal sealed class SynthesisMissingLifetimeScope : IOctoberExecutionScope
{
    public int Acquisitions { get; private set; }
    public Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default)
    {
        Acquisitions++;
        return Task.FromResult(new ProviderAdmission(ProviderAdmissionStatus.AuthorityUnavailable, null));
    }
}
