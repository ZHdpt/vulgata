using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Pure canonical contract qualification; no persisted-corpus membership or live provider authority is claimed.</summary>
public sealed class OctoberBusinessFinalizationContractTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ProviderFingerprint_VersionedNonsecretProjection_MatchesIndependentGoldenSha()
    {
        Assert.Equal(1, OctoberBusinessFinalizationPolicy.SchemaVersion);
        Assert.Equal("199B7036796F44AAD8F93B23AA8F9743747385F37705FE1FC51BF40C03255B0C",
            OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(FinalizationContractCase.Provider));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ProviderFingerprint_EveryProjectionFieldAndExactEndpointSpelling_AffectsIdentity()
    {
        var p = FinalizationContractCase.Provider;
        string expected = OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(p);
        BusinessProviderConfigurationProjection[] changed =
        [
            p with { SourceIdentity = "owned/other-settings.json" }, p with { Precedence = 3 },
            p with { ProfileName = "other" }, p with { ApiType = "AzureOpenAI" },
            p with { Endpoint = p.Endpoint + "/" }, p with { Endpoint = "https://PROVIDER.invalid/v1" },
            p with { Model = "model-y" }, p with { CredentialReferenceId = "credential-reference-18" }
        ];
        Assert.All(changed, value => Assert.NotEqual(expected, OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(value)));
        Assert.DoesNotContain(typeof(BusinessProviderConfigurationProjection).GetProperties(), property =>
            property.Name is "Credential" or "Password" or "ApiKey" or "CredentialHash" or "Ciphertext");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ProviderFingerprint_InvalidProjection_IsRejected()
    {
        var p = FinalizationContractCase.Provider;
        BusinessProviderConfigurationProjection[] invalid =
        [
            null!, p with { SourceIdentity = "" }, p with { ProfileName = " " }, p with { ApiType = "" },
            p with { Endpoint = "" }, p with { Model = "" }, p with { CredentialReferenceId = "" },
            p with { SourceIdentity = "broken\ud800" }
        ];
        Assert.All(invalid, value => Assert.ThrowsAny<ArgumentException>(() => OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(value)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void RequestDigest_ExactVersionedProjection_ExcludesExecutionCapabilityAndSelfDigest()
    {
        var f = new FinalizationContractCase();
        var request = f.Request();
        string expected = f.RequestOracle(request);
        Assert.Equal(expected, OctoberBusinessFinalizationPolicy.ComputeRequestDigest(request));
        Assert.Equal(expected, OctoberBusinessFinalizationPolicy.ComputeRequestDigest(request with
        {
            CanonicalPayloadDigest = "excluded transport field", ExecutionScope = new FinalizationForbiddenScope(),
            Selection = request.Selection! with { Roots = request.Selection.Roots.Reverse().ToImmutableArray() }
        }));
        Assert.Matches("^[A-F0-9]{64}$", expected);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void RequestDigest_EveryBoundOperationCorpusAndSelectionValue_ChangesIdentity()
    {
        var f = new FinalizationContractCase();
        var r = f.Request();
        var s = r.Selection!;
        var root = s.Roots[0];
        string expected = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(r);
        FinalizationRequest[] changed =
        [
            r with { Operation = r.Operation with { OperationId = FinalizationContractCase.Id(88) } },
            r with { Corpus = r.Corpus with { Binding = r.Corpus.Binding with { CanonicalDatabaseIdentity = "other/database" } } },
            r with { Corpus = r.Corpus with { Binding = r.Corpus.Binding with { TargetId = "other-target" } } },
            r with { Corpus = r.Corpus with { Binding = r.Corpus.Binding with { SelectionDigest = new string('B', 64) } } },
            r with { Corpus = r.Corpus with { CorpusId = FinalizationContractCase.Id(89) } },
            r with { Corpus = r.Corpus with { CorpusDigest = new string('C', 64) } },
            r with { Selection = s with { Roots = s.Roots.SetItem(0, root with { Selector = "other-root" }) } },
            r with { Selection = s with { Roots = s.Roots.SetItem(0, root with { Document = root.Document with { DocumentVersionId = FinalizationContractCase.Id(90) } }) } },
            r with { Selection = s with { Limits = s.Limits with { MaxNodes = 128 } } },
            r with { Selection = s with { RunId = FinalizationContractCase.Id(91) } },
            r with { Selection = s with { ProviderProfileId = "other-profile" } },
            r with { Selection = s with { ProviderConfigurationFingerprint = new string('D', 64) } },
            r with { Selection = s with { PromptDigest = new string('E', 64) } },
            r with { Selection = s with { MaximumTokens = 81 } }
        ];
        Assert.All(changed, value => Assert.NotEqual(expected, OctoberBusinessFinalizationPolicy.ComputeRequestDigest(value)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void RequestDigest_ExplicitEmptyAnd256Roots_AreValidWithoutMembershipClaims()
    {
        var f = new FinalizationContractCase();
        var r = f.Request();
        var empty = r with { Selection = r.Selection! with { Roots = [] } };
        Assert.Equal(f.RequestOracle(empty), OctoberBusinessFinalizationPolicy.ComputeRequestDigest(empty));
        var boundary = r with { Selection = r.Selection! with { Roots = Enumerable.Range(0, 256).Select(FinalizationContractCase.Root).ToImmutableArray() } };
        Assert.Equal(256, OctoberBusinessFinalizationPolicy.MaximumRoots);
        string digest = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(boundary);
        Assert.Equal(digest, OctoberBusinessFinalizationPolicy.ComputeRequestDigest(boundary with { Selection = boundary.Selection! with { Roots = boundary.Selection.Roots.Reverse().ToImmutableArray() } }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("null")]
    [InlineData("selection")]
    [InlineData("default-roots")]
    [InlineData("257-roots")]
    [InlineData("duplicate-root")]
    [InlineData("conflicting-version")]
    [InlineData("root-batch")]
    [InlineData("operation-batch")]
    [InlineData("operation-kind")]
    [InlineData("root-id")]
    [InlineData("limits")]
    [InlineData("run")]
    [InlineData("fingerprint")]
    [InlineData("prompt")]
    [InlineData("tokens")]
    public void RequestDigest_InvalidBoundShape_IsRejected(string mutation)
    {
        var f = new FinalizationContractCase();
        var r = f.Request(); var s = r.Selection!; var root = s.Roots[0];
        r = mutation switch
        {
            "null" => null!, "selection" => r with { Selection = null },
            "default-roots" => r with { Selection = s with { Roots = default } },
            "257-roots" => r with { Selection = s with { Roots = Enumerable.Range(0, 257).Select(FinalizationContractCase.Root).ToImmutableArray() } },
            "duplicate-root" => r with { Selection = s with { Roots = s.Roots.Add(root) } },
            "conflicting-version" => r with { Selection = s with { Roots = s.Roots.Add(root with { Document = root.Document with { DocumentVersionId = FinalizationContractCase.Id(92) } }) } },
            "root-batch" => r with { Selection = s with { Roots = s.Roots.SetItem(0, root with { Document = root.Document with { BatchId = FinalizationContractCase.Id(93) } }) } },
            "operation-batch" => r with { Operation = r.Operation with { BatchId = FinalizationContractCase.Id(93) } },
            "operation-kind" => r with { Operation = r.Operation with { Kind = OperationKind.AcceptFile } },
            "root-id" => r with { Selection = s with { Roots = s.Roots.SetItem(0, root with { UnitId = Guid.Empty }) } },
            "limits" => r with { Selection = s with { Limits = new(257, 512, 16) } },
            "run" => r with { Selection = s with { RunId = Guid.Empty } },
            "fingerprint" => r with { Selection = s with { ProviderConfigurationFingerprint = "not-sha" } },
            "prompt" => r with { Selection = s with { PromptDigest = "not-sha" } },
            "tokens" => r with { Selection = s with { MaximumTokens = 0 } },
            _ => throw new InvalidOperationException()
        };
        Assert.ThrowsAny<ArgumentException>(() => OctoberBusinessFinalizationPolicy.ComputeRequestDigest(r));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OutcomeDigest_BindsOriginalProposalsAndTypedDisposition_ExcludesSelfAndSetOrder()
    {
        var f = new FinalizationContractCase();
        var request = f.Outcome();
        string expected = f.OutcomeOracle(request);
        Assert.Equal(expected, OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(request));
        Assert.Equal(expected, OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(request with
        {
            CanonicalOutcomeDigest = "excluded self digest", Completed = request.Completed.Reverse().ToImmutableArray(),
            Dispositions = request.Dispositions.Reverse().ToImmutableArray(),
            Selection = request.Selection with { Roots = request.Selection.Roots.Reverse().ToImmutableArray() }
        }));
        var first = request.Completed[0];
        var section = first.Proposal.Sections[0];
        var claim = section.Claims[0];
        FlowFinalizationPublicationRequest[] changed =
        [
            request with { Completed = request.Completed.SetItem(0, first with { GroupVersionId = FinalizationContractCase.Id(94) }) },
            request with { Completed = request.Completed.SetItem(0, first with { Proposal = first.Proposal with { Title = "Changed original model narrative" } }) },
            request with { Completed = request.Completed.SetItem(0, first with { Proposal = first.Proposal with { Sections = first.Proposal.Sections.SetItem(0, section with { Claims = section.Claims.SetItem(0, claim with { Text = "Changed effect" }) }) } }) },
            request with { Dispositions = request.Dispositions.SetItem(0, request.Dispositions[0] with { Reason = "Different terminal fact" }) },
            request with { Dispositions = request.Dispositions.SetItem(0, request.Dispositions[0] with { Kind = FlowFinalizationDispositionKind.NotEvaluated }) },
            request with { Dispositions = request.Dispositions.SetItem(0, request.Dispositions[0] with { GroupVersionId = FinalizationContractCase.Id(95) }) }
        ];
        Assert.All(changed, value => Assert.NotEqual(expected, OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(value)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OutcomeDigest_EmptySelectionRequiresExplicitEmptyOutcomes_AndProducesNoContentIdentity()
    {
        var f = new FinalizationContractCase();
        var request = f.Request() with { Selection = f.Request().Selection! with { Roots = [] } };
        var outcome = new FlowFinalizationPublicationRequest(request.Operation, request.Corpus, f.RequestOracle(request), request.Selection!, [], [], "ignored");
        Assert.Equal(f.OutcomeOracle(outcome), OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(outcome));
        Assert.ThrowsAny<ArgumentException>(() => OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(outcome with { Completed = default }));
        Assert.ThrowsAny<ArgumentException>(() => OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(outcome with { Dispositions = default }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("request-digest")]
    [InlineData("missing-root")]
    [InlineData("duplicate-completion")]
    [InlineData("completed-and-failed")]
    [InlineData("unselected-root")]
    [InlineData("wrong-version")]
    [InlineData("group")]
    [InlineData("proposal")]
    [InlineData("kind")]
    [InlineData("reason")]
    public void OutcomeDigest_InvalidTerminalBinding_IsRejected(string mutation)
    {
        var f = new FinalizationContractCase(); var r = f.Outcome(); var first = r.Completed[0];
        r = mutation switch
        {
            "request-digest" => r with { CanonicalPayloadDigest = new string('F', 64) },
            "missing-root" => r with { Completed = r.Completed.RemoveAt(0) },
            "duplicate-completion" => r with { Completed = r.Completed.Add(first) },
            "completed-and-failed" => r with { Dispositions = r.Dispositions.Add(new(first.Root, first.GroupVersionId, FlowFinalizationDispositionKind.Failed, "Already completed")) },
            "unselected-root" => r with { Completed = r.Completed.SetItem(0, first with { Root = FinalizationContractCase.Root(12) }) },
            "wrong-version" => r with { Completed = r.Completed.SetItem(0, first with { Root = first.Root with { Document = first.Root.Document with { DocumentVersionId = FinalizationContractCase.Id(96) } } }) },
            "group" => r with { Completed = r.Completed.SetItem(0, first with { GroupVersionId = Guid.Empty }) },
            "proposal" => r with { Completed = r.Completed.SetItem(0, first with { Proposal = null! }) },
            "kind" => r with { Dispositions = r.Dispositions.SetItem(0, r.Dispositions[0] with { Kind = (FlowFinalizationDispositionKind)100 }) },
            "reason" => r with { Dispositions = r.Dispositions.SetItem(0, r.Dispositions[0] with { Reason = " " }) },
            _ => throw new InvalidOperationException()
        };
        Assert.ThrowsAny<ArgumentException>(() => OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(r));
    }
}

internal sealed class FinalizationContractCase
{
    public static Guid Id(int value) => ResolutionCase.Id(980000 + value);
    public static BusinessProviderConfigurationProjection Provider => new("owned/settings.json", 2, "selected", "OpenAI", "https://provider.invalid/v1", "model-x", "credential-reference-17");
    public static FlowRootSelection Root(int index) => new(Id(1000 + index), new(Id(1), Id(2000 + index), Id(3000 + index)), FlowRootKind.SelectedCodeUnit, "root-" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture));
    public FinalizationRequest Request() => new(new(Id(1), OperationKind.FinalizeCorpus, Id(2)),
        new(new("controlled/database", "selected-target", Id(1), new string('A', 64)), Id(3), new string('B', 64)), "excluded", new FinalizationForbiddenScope())
    {
        Selection = new(Enumerable.Range(0, 4).Select(Root).ToImmutableArray(), new(), Id(4), "selected-profile", new string('C', 64), new string('D', 64), 80)
    };
    public FlowFinalizationPublicationRequest Outcome()
    {
        var request = Request();
        FlowSynthesisSubmission Completed(int index) => new(Root(index), Id(4000 + index), new("业务处理",
            [new("operation", 0, "业务步骤", [new("claim", "执行处理并返回结果。", ClaimQualification.SourceSupported, [new(Root(index).Document, "operation", "provider")], [])])]));
        return new(request.Operation, request.Corpus, RequestOracle(request), request.Selection!, [Completed(0), Completed(1)],
            [new(Root(2), Id(4002), FlowFinalizationDispositionKind.Failed, "Controlled provider failure"), new(Root(3), null, FlowFinalizationDispositionKind.NotEvaluated, "Controlled remaining budget exhausted")], "excluded");
    }
    private static BusinessFinalizationSelection Sorted(BusinessFinalizationSelection selection) => selection with
    {
        Roots = selection.Roots.OrderBy(root => root.Selector, StringComparer.Ordinal).ToImmutableArray()
    };
    public string RequestOracle(FinalizationRequest request) => Sha(OctoberKnowledgePolicy.SerializeCanonical(new
    {
        domain = "october/business-finalization-request/1", schemaVersion = 1,
        request.Operation, request.Corpus, selection = Sorted(request.Selection!)
    }));
    public string OutcomeOracle(FlowFinalizationPublicationRequest request) => Sha(OctoberKnowledgePolicy.SerializeCanonical(new
    {
        domain = "october/business-finalization-outcome/1", schemaVersion = 1,
        request.Operation, request.Corpus, request.CanonicalPayloadDigest, selection = Sorted(request.Selection),
        completed = request.Completed.OrderBy(value => value.Root.Selector, StringComparer.Ordinal),
        dispositions = request.Dispositions.OrderBy(value => value.Root.Selector, StringComparer.Ordinal)
    }));
    private static string Sha(string canonical) => Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false, true).GetBytes(canonical)));
}

/// <summary>A process-local capability must neither be inspected nor invoked when deriving logical request identity.</summary>
internal sealed class FinalizationForbiddenScope : IOctoberExecutionScope
{
    public string SerializationTripwire => throw new InvalidOperationException("Execution capability must be excluded from canonical request identity.");
    public Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Canonical helpers must not dispatch providers.");
}
