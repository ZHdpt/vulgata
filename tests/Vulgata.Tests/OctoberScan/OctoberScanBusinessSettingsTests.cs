using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

public sealed class OctoberScanBusinessSettingsTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitSettings_PreserveEmptyOrExactRootsAndFiniteLimitsAsync(bool empty)
    {
        using var file = new Document();
        var expected = Valid();
        if (empty) expected = expected with { Roots = [] };
        file.Write(JsonSerializer.Serialize(expected, Json));
        var actual = await OctoberGeneratorEntryPoint.ReadBusinessSettingsAsync(file.Configuration());
        Assert.True(actual.IsValid());
        Assert.Equal(expected.Roots.ToArray(), actual.Roots.ToArray());
        Assert.Equal(expected.Limits, actual.Limits);
        Assert.Equal(expected.MaximumAttemptTokens, actual.MaximumAttemptTokens);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing-file")]
    [InlineData("missing-roots")]
    [InlineData("null-roots")]
    [InlineData("unknown-member")]
    [InlineData("duplicate-member")]
    [InlineData("empty-repository")]
    [InlineData("absolute-path")]
    [InlineData("parent-path")]
    [InlineData("empty-selector")]
    [InlineData("unsupported-kind")]
    [InlineData("duplicate-root")]
    [InlineData("root-limit")]
    [InlineData("node-limit")]
    [InlineData("edge-limit")]
    [InlineData("depth-limit")]
    [InlineData("zero-tokens")]
    [InlineData("missing-maxEdges")]
    [InlineData("missing-maxDepth")]
    [InlineData("missing-kind")]
    public async Task InvalidSettings_CannotDefaultOrWidenTheExplicitSelectionAsync(string fault)
    {
        using var file = new Document();
        var value = Valid();
        var root = value.Roots[0];
        value = fault switch
        {
            "empty-repository" => value with { Roots = [root with { RepositoryId = Guid.Empty }] },
            "absolute-path" => value with { Roots = [root with { RelativePath = "C:/private/source.cs" }] },
            "parent-path" => value with { Roots = [root with { RelativePath = "../source.cs" }] },
            "empty-selector" => value with { Roots = [root with { SemanticSelector = "" }] },
            "unsupported-kind" => value with { Roots = [root with { Kind = FlowRootKind.Handler }] },
            "duplicate-root" => value with { Roots = [root, root] },
            "root-limit" => value with { Roots = Enumerable.Range(0, 257).Select(i => root with { SemanticSelector = "unit-" + i }).ToImmutableArray() },
            "node-limit" => value with { Limits = new(257, 512, 16) },
            "edge-limit" => value with { Limits = new(256, 513, 16) },
            "depth-limit" => value with { Limits = new(256, 512, 17) },
            "zero-tokens" => value with { MaximumAttemptTokens = 0 },
            _ => value,
        };
        var json = JsonSerializer.SerializeToNode(value, Json)!.AsObject();
        if (fault == "missing-roots") json.Remove("roots");
        if (fault == "missing-kind") json["roots"]![0]!.AsObject().Remove("kind");
        if (fault == "null-roots") json["roots"] = null;
        if (fault == "missing-maxEdges") json["limits"]!.AsObject().Remove("maxEdges");
        if (fault == "missing-maxDepth") json["limits"]!.AsObject().Remove("maxDepth");
        if (fault == "unknown-member") json["inferRoots"] = true;
        string text = json.ToJsonString();
        if (fault == "duplicate-member") text = "{\"maximumAttemptTokens\":1," + text[1..];
        file.Write(text);
        await Assert.ThrowsAnyAsync<Exception>(() => OctoberGeneratorEntryPoint.ReadBusinessSettingsAsync(
            fault == "missing-file" ? new ConfigurationBuilder().Build() : file.Configuration()));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("exact")]
    [InlineData("empty")]
    [InlineData("missing")]
    [InlineData("ambiguous")]
    public void ExplicitRoots_BindOnlyOneExactOwnerProjectedUnit(string mode)
    {
        // Controlled owner-projection-shaped values; this is not persistence or Identity qualification.
        var input = new FlowPlannerCase(2, [], []).Input;
        var unit = input.Units[1];
        var settings = new OctoberBusinessSettings([new(unit.PrimaryOrigin.RepositoryId, unit.PrimaryOrigin.RelativePath, unit.SemanticSelector, FlowRootKind.SelectedCodeUnit)], new(), 32768);
        if (mode == "empty") settings = settings with { Roots = [] };
        if (mode == "missing") settings = settings with { Roots = [settings.Roots[0] with { RepositoryId = Guid.NewGuid() }] };
        if (mode == "ambiguous") input = input with { Units = input.Units.Add(unit with { UnitId = Guid.NewGuid() }) };
        if (mode is "missing" or "ambiguous") { Assert.ThrowsAny<Exception>(() => settings.SelectRoots(input)); return; }
        var selected = settings.SelectRoots(input);
        if (mode == "empty") Assert.Empty(selected);
        else Assert.Equal(new FlowRootSelection(unit.UnitId, unit.Document, FlowRootKind.SelectedCodeUnit, unit.SemanticSelector), Assert.Single(selected));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClOnly_DoesNotReadMissingOrMalformedBusinessSettingsAsync(bool malformed)
    {
        using var file = new Document();
        if (malformed) file.Write("not-json");
        var settings = await OctoberGeneratorEntryPoint.ReadBusinessSettingsForPolicyAsync(file.Configuration(), new() { OutputStage = "cl-only" });
        Assert.Null(settings);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BusinessAttemptTokenCeiling_CannotExceedTheOriginalScanPolicyAsync(bool excessive)
    {
        using var file = new Document();
        file.Write(JsonSerializer.Serialize(Valid(), Json));
        var policy = new OctoberScanPolicy { OutputStage = "cl-and-bl", MaximumTotalTokens = excessive ? 32767 : 32768,
            SourceRules = [new(Valid().Roots[0].RepositoryId, "src", "selected", "controlled selection")] };
        if (excessive) await Assert.ThrowsAnyAsync<Exception>(() => OctoberGeneratorEntryPoint.ReadBusinessSettingsForPolicyAsync(file.Configuration(), policy));
        else Assert.Equal(32768, (await OctoberGeneratorEntryPoint.ReadBusinessSettingsForPolicyAsync(file.Configuration(), policy))!.MaximumAttemptTokens);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ClOnlyComposition_DoesNotRequireAnyBusinessDependency()
    {
        Assert.Null(OctoberGeneratorEntryPoint.ComposeBusiness(new() { OutputStage = "cl-only" }, null!, null, null!, null!, null));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualBusinessComposition_UsesOwnerFactoriesExactProviderAndMandatoryReaderBindingAsync(bool missingBinding)
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var input = new FlowPlannerCase(1, [], []).Input;
        var unit = input.Units[0];
        var settings = new OctoberBusinessSettings([new(unit.PrimaryOrigin.RepositoryId, unit.PrimaryOrigin.RelativePath, unit.SemanticSelector, FlowRootKind.SelectedCodeUnit)], new(16, 24, 4), 32768);
        var policy = host.Execution.Policy with { OutputStage = "cl-and-bl" };
        int binds = 0;
        Func<IOctoberOperatorSession, PublicationBinding, CancellationToken, Task<OctoberVerifiedReaderBinding?>> binder = (_, _, _) => { binds++; throw new InvalidOperationException("No reader acquisition during composition"); };
        if (missingBinding)
        {
            Assert.ThrowsAny<Exception>(() => OctoberGeneratorEntryPoint.ComposeBusiness(policy, host.Execution.Provider, settings, host.Database.Factory, host.Database.Log, null));
            return;
        }
        var composition = Assert.IsType<OctoberBusinessRunComposition>(OctoberGeneratorEntryPoint.ComposeBusiness(policy, host.Execution.Provider, settings, host.Database.Factory, host.Database.Log, binder));
        Assert.Equal(0, binds);
        Assert.Same(binder, composition.BindReader);
        Assert.Equal(settings.Limits, composition.Limits); Assert.Equal(settings.MaximumAttemptTokens, composition.MaximumAttemptTokens);
        Assert.Equal(host.Execution.Provider.Profile.Model, composition.ProviderConfiguration.Model);
        Assert.Equal(host.Execution.Provider.Profile.Endpoint, composition.ProviderConfiguration.Endpoint);
        Assert.Equal(host.Execution.Provider.Profile.Name, composition.ProviderConfiguration.ProfileName);
        Assert.NotEmpty(composition.ResolutionInstructions); Assert.NotEmpty(composition.SynthesisInstructions);
        Assert.Equal(new FlowRootSelection(unit.UnitId, unit.Document, FlowRootKind.SelectedCodeUnit, unit.SemanticSelector), Assert.Single(composition.SelectRoots(input)));
        Assert.IsType<OctoberResolutionStore>(composition.CreateResolutionStore(host.Database.Authorization, host.Database.Guard));
        Assert.IsType<OctoberFlowPlanningInputLoader>(composition.CreatePlanningInputLoader(host.Database.Authorization));
        Assert.IsType<OctoberFlowPublicationStore>(composition.CreatePublicationStore(host.Database.Authorization, host.Database.Guard));
    }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static OctoberBusinessSettings Valid() => new([new(Guid.Parse("19b8c12e-c5a5-4f99-a376-6c1e04b36b41"), "src/Order.cs", "file", FlowRootKind.SelectedCodeUnit)], new(16, 24, 4), 32768);
    private sealed class Document : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "october-business-settings-" + Guid.NewGuid().ToString("N"));
        internal Document() => Directory.CreateDirectory(_directory);
        internal void Write(string text) => File.WriteAllText(Path.Combine(_directory, "settings.json"), text);
        internal IConfigurationRoot Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["October:BusinessSettingsFile"] = Path.Combine(_directory, "settings.json") }).Build();
        public void Dispose() => Directory.Delete(_directory, true);
    }
}
