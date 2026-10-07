using System.Collections.Immutable;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Real SQLite B0/B1 persistence with controlled source, Scan and live authorization adapters.</summary>
public sealed class OctoberFlowInputStoreTests
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadAsync_RealAcceptedB1_ProjectsExactUnitsAnchorsOutcomesAndCompleteLedgerAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var accepted = await fixture.Store().AcceptOutcomeAsync(fixture.Request());
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var roots = await RootsAsync(fixture);

        var result = await Loader(fixture).LoadAsync(fixture.Corpus.Handle, roots);

        Assert.Equal(KnowledgeReadStatus.Success, result.Status);
        var input = Assert.IsType<FlowPlanningInput>(result.Input);
        Assert.Equal(fixture.Corpus.Handle, input.Corpus.Handle);
        Assert.Equal(roots.ToArray(), input.Roots.ToArray());
        Assert.Equal(3, input.Units.Length);
        Assert.Equal(3, input.Corpus.Documents.Length);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        foreach (var unit in input.Units)
        {
            var logical = await context.LogicalDocuments.SingleAsync(value => value.LogicalDocumentId == unit.Document.LogicalDocumentId);
            var storedUnit = await context.CodeUnits.SingleAsync(value => value.UnitId == logical.UnitId);
            var version = await context.DocumentVersions.SingleAsync(value => value.VersionId == unit.Document.DocumentVersionId);
            Assert.Equal(storedUnit.UnitId, unit.UnitId);
            Assert.Equal(storedUnit.SemanticSelector, unit.SemanticSelector);
            Assert.Equal(fixture.Files.Single(file => file.ManifestId == version.ManifestId).Coverage.Origin, unit.PrimaryOrigin);
            Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(Read<ImmutableArray<SourceAnchor>>(version.AnchorsJson)),
                OctoberKnowledgePolicy.SerializeCanonical(unit.Anchors));
        }
        var item = await context.ResolutionItems.SingleAsync();
        var projected = Assert.Single(input.Resolutions);
        Assert.Equal(item.ItemId, projected.ItemId);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(accepted.Outcome), OctoberKnowledgePolicy.SerializeCanonical(projected.Outcome));
        Assert.Equal((await context.UncertaintyVersions.SingleAsync()).UncertaintyId, projected.UncertaintyVersionId);
        var fullLedger = Read<ImmutableArray<Contributor>>((await context.CorpusSelections.SingleAsync()).ContributorsJson);
        Assert.True(fullLedger.ToHashSet().SetEquals(input.Contributors));
        Assert.True(fullLedger.ToHashSet().SetEquals(fixture.Authorization.LastContributors));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(OctoberResolutionPolicy.Plan(input.Corpus).Dispositions),
            OctoberKnowledgePolicy.SerializeCanonical(input.PlanDispositions));
        Assert.Equal(FlowPlanningStatus.Succeeded, OctoberFlowPlanner.Plan(input).Status);
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadAsync_RealWriterAcceptsMixedPathSeparators_PreservesRawEvidenceAndStoredDigestsAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var backslashOrigin = fixture.Origin with { RelativePath = fixture.Origin.RelativePath.Replace('/', '\\') };
        var original = fixture.Proposal;
        var units = original.Units.Select(unit =>
        {
            var sections = unit.Sections.Select(section => section with
            {
                Claims = section.Claims.Select(claim => claim with
                {
                    Evidence = claim.Evidence.Select(evidence => evidence.Source is { } anchor
                        ? evidence with { Source = anchor with { Origin = backslashOrigin } } : evidence).ToImmutableArray()
                }).ToImmutableArray()
            }).ToImmutableArray();
            return OctoberKnowledgeAcceptanceFixture.Unit(unit.SemanticSelector, unit.ParentSelector, unit.Title,
                unit.Anchors.Select(anchor => anchor with { Origin = backslashOrigin }).ToImmutableArray(), sections);
        }).ToImmutableArray();
        var proposal = original with { Units = units, Observations = [] };
        Assert.True(OctoberKnowledgePolicy.Validate(proposal, new(fixture.Origin, 100)));
        var accepted = await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var freeze = new CorpusFreezeRequest(new(fixture.Binding.BatchId, OperationKind.FreezeCorpus, Guid.NewGuid()),
            fixture.Binding, "", [accepted.AcceptedFile!], [proposal.Coverage]);
        freeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
        fixture.FreezeAuthority.Approve(freeze);
        var frozen = await fixture.Writer().FreezeCorpusAsync(freeze);
        Assert.Equal(KnowledgeWriteStatus.Committed, frozen.Status);
        var root = await RootAsync(fixture, accepted.AcceptedFile!.Documents[0]);
        await using var context = fixture.Factory.CreateDbContext();
        var before = await context.DocumentVersions.AsNoTracking().OrderBy(value => value.VersionId)
            .Select(value => new { value.VersionId, value.ContentJson, value.ContentDigest, value.AnchorsJson }).ToListAsync();
        Assert.All(before, value => Assert.All(Read<ImmutableArray<SourceAnchor>>(value.AnchorsJson),
            anchor => Assert.Equal(backslashOrigin, anchor.Origin)));

        var loaded = await new OctoberFlowPlanningInputLoader(fixture.Factory, fixture.Authorization).LoadAsync(frozen.Corpus!, [root]);

        Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
        var input = Assert.IsType<FlowPlanningInput>(loaded.Input);
        Assert.All(input.Units, unit =>
        {
            Assert.Equal(fixture.Origin, unit.PrimaryOrigin);
            Assert.All(unit.Anchors, anchor => Assert.Equal(backslashOrigin, anchor.Origin));
        });
        Assert.All(input.Corpus.Documents.SelectMany(document => document.Sections).SelectMany(section => section.Claims)
            .SelectMany(claim => claim.Evidence).Where(evidence => evidence.Source is not null),
            evidence => Assert.Equal(backslashOrigin, evidence.Source!.Origin));
        Assert.Equal(FlowPlanningStatus.Succeeded, OctoberFlowPlanner.Plan(input).Status);
        Assert.Single(OctoberFlowPlanner.Plan(input).Groups);
        var after = await context.DocumentVersions.AsNoTracking().OrderBy(value => value.VersionId)
            .Select(value => new { value.VersionId, value.ContentJson, value.ContentDigest, value.AnchorsJson }).ToListAsync();
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(before), OctoberKnowledgePolicy.SerializeCanonical(after));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadAsync_ZeroUnitAndUnprocessedCoverage_UsesActualFrozenCorpusLedgerAsync()
    {
        await using var acceptance = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var local = await CreateLocalCorpusAsync(acceptance);
        var authority = new FlowLoaderAuthority();
        var loader = new OctoberFlowPlanningInputLoader(acceptance.Factory, authority);

        var result = await loader.LoadAsync(local.Corpus, local.Roots);

        Assert.Equal(KnowledgeReadStatus.Success, result.Status);
        var input = Assert.IsType<FlowPlanningInput>(result.Input);
        Assert.Empty(input.Resolutions);
        Assert.Equal(2, input.Units.Length);
        Assert.Equal(3, input.Coverage.Length);
        Assert.Contains(input.Coverage, value => value.Disposition == CoverageDisposition.CompletedZeroUnit);
        Assert.Contains(input.Coverage, value => value.Disposition == CoverageDisposition.Unprocessed);
        Assert.Contains(local.ZeroSource, input.Contributors);
        Assert.Contains(local.GapSource, input.Contributors);
        Assert.Contains(acceptance.UncitedContributor, input.Contributors);
        await using (var context = acceptance.Factory.CreateDbContext())
        {
            var retainedUnknowns = await context.KnowledgeUncertainties.AsNoTracking().ToListAsync();
            Assert.Equal(2, retainedUnknowns.Count);
            Assert.Equal(retainedUnknowns.Count, input.FileUncertainties.Length);
            foreach (var unknown in retainedUnknowns)
            {
                var projected = Assert.Single(input.FileUncertainties.Where(value =>
                    value.BatchId == unknown.BatchId && value.ManifestId == unknown.ManifestId && value.Key == unknown.Key));
                Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(Read<Uncertainty>(unknown.ContentJson)),
                    OctoberKnowledgePolicy.SerializeCanonical(projected.Content));
            }
        }
        Assert.All(authority.Seen, ledger => Assert.True(ledger.Contains(local.ZeroSource) && ledger.Contains(local.GapSource)));
        var planned = OctoberFlowPlanner.Plan(input);
        Assert.Equal(FlowPlanningStatus.Succeeded, planned.Status);
        Assert.Contains(local.ZeroSource, Assert.Single(planned.Groups).Dependencies.Contributors);
        Assert.Contains(local.GapSource, planned.Groups[0].Dependencies.Contributors);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(input.FileUncertainties.OrderBy(value => value.ManifestId).ThenBy(value => value.Key)),
            OctoberKnowledgePolicy.SerializeCanonical(planned.Groups[0].Dependencies.FileUncertainties.OrderBy(value => value.ManifestId).ThenBy(value => value.Key)));
        Assert.Contains(planned.Groups[0].Dependencies.FileUncertainties, value => value.Key == "zero-unit-unknown");
        authority.DeniedRepository = local.GapSource.RepositoryId;
        AssertUnavailable(await loader.LoadAsync(local.Corpus, local.Roots));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadAsync_AllSelectedInquiriesNeedPersistedDisposition_ExplicitHostFailureCompletesSetAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync(secondCaller: true);
        var first = fixture.Request();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store().AcceptOutcomeAsync(first)).Status);
        var roots = await RootsAsync(fixture);
        AssertUnavailable(await Loader(fixture).LoadAsync(fixture.Corpus.Handle, roots));
        var other = OctoberResolutionPolicy.Plan(fixture.Corpus).Inquiries.Single(value => value.Fingerprint != first.InquiryFingerprint);
        var failed = ResolutionPersistenceFixture.Digest(first with
        {
            Operation = first.Operation with { OperationId = Guid.NewGuid() }, AttemptId = Guid.NewGuid(),
            InquiryFingerprint = other.Fingerprint, Witness = null, ExecutionDisposition = ResolutionOutcomeKind.Failed
        });
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store().AcceptOutcomeAsync(failed)).Status);

        var loaded = await Loader(fixture).LoadAsync(fixture.Corpus.Handle, roots);

        Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
        Assert.Equal(2, loaded.Input!.Resolutions.Length);
        var retained = Assert.Single(loaded.Input.Resolutions.Where(value => value.Outcome.Kind == ResolutionOutcomeKind.Failed));
        Assert.Null(retained.Outcome.Relationship);
        Assert.NotEmpty(retained.Outcome.Uncertainty!.Limitations);
        Assert.Equal(FlowPlanningStatus.Succeeded, OctoberFlowPlanner.Plan(loaded.Input).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("witness")]
    [InlineData("outcome")]
    [InlineData("relationship")]
    [InlineData("uncertainty")]
    [InlineData("missing-dependency")]
    [InlineData("caller-marker")]
    [InlineData("contributors")]
    [InlineData("policy")]
    [InlineData("dispositions")]
    [InlineData("anchors")]
    public async Task LoadAsync_PersistedProjectionCorruption_ReturnsNoSubstituteInputAsync(string corruption)
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store().AcceptOutcomeAsync(fixture.Request())).Status);
        var roots = await RootsAsync(fixture);
        // Deliberately bypass EF's append-only SaveChanges guard using parameterized SQL updates
        // in this owned SQLite file, which has no PostgreSQL triggers. Corruption is not acceptance.
        await using (var context = fixture.Acceptance.Factory.CreateDbContext())
        {
            var item = await context.ResolutionItems.SingleAsync();
            switch (corruption)
            {
                case "witness":
                    string witnessJson = OctoberKnowledgePolicy.SerializeCanonical(Read<ResolutionWitness>(item.WitnessJson!) with { ConnectionExplanation = "A substituted original witness." });
                    Assert.Equal(1, await context.ResolutionItems.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.WitnessJson, witnessJson)));
                    break;
                case "outcome":
                    var outcome = Read<ResolutionOutcome>(item.OutcomeJson);
                    string outcomeJson = OctoberKnowledgePolicy.SerializeCanonical(outcome with { Relationship = outcome.Relationship! with { Meaning = "Substituted accepted conclusion." } });
                    Assert.Equal(1, await context.ResolutionItems.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.OutcomeJson, outcomeJson)));
                    break;
                case "relationship":
                    var edge = await context.RelationshipVersions.SingleAsync();
                    string edgeJson = OctoberKnowledgePolicy.SerializeCanonical(Read<AcceptedRelationship>(edge.ContentJson) with { To = fixture.Corpus.Documents[2].Version });
                    Assert.Equal(1, await context.RelationshipVersions.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.ContentJson, edgeJson)));
                    break;
                case "uncertainty":
                    var uncertainty = await context.UncertaintyVersions.SingleAsync();
                    string uncertaintyJson = OctoberKnowledgePolicy.SerializeCanonical(Read<Uncertainty>(uncertainty.ContentJson) with { Limitations = [] });
                    Assert.Equal(1, await context.UncertaintyVersions.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.ContentJson, uncertaintyJson)));
                    break;
                case "missing-dependency":
                    Assert.Equal(1, await context.ResolutionDocumentDependencies.Where(value => value.VersionId == fixture.Corpus.Documents[2].Version.DocumentVersionId).ExecuteDeleteAsync());
                    break;
                case "caller-marker":
                    Assert.Equal(1, await context.ResolutionDocumentDependencies.Where(value => value.IsInquiryCaller).ExecuteUpdateAsync(setters => setters.SetProperty(value => value.IsInquiryCaller, false)));
                    break;
                case "contributors":
                    Assert.Equal(1, await context.ResolutionItems.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.ContributorsJson, "[]")));
                    break;
                case "policy":
                    Assert.Equal(1, await context.ResolutionItems.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.PolicyVersion, "unrecognized-resolution-policy")));
                    break;
                case "dispositions":
                    string dispositionsJson = OctoberKnowledgePolicy.SerializeCanonical(new[] { new ResolutionItemDisposition(fixture.Inquiry.Caller, ResolutionOutcomeKind.NotEvaluated, "Invented omission.") });
                    Assert.Equal(1, await context.ResolutionItems.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.PlanDispositionsJson, dispositionsJson)));
                    break;
                case "anchors":
                    var version = await context.DocumentVersions.SingleAsync(value => value.VersionId == roots[0].Document.DocumentVersionId);
                    var anchors = Read<ImmutableArray<SourceAnchor>>(version.AnchorsJson);
                    string anchorsJson = OctoberKnowledgePolicy.SerializeCanonical(anchors.Select(anchor => anchor with { Origin = anchor.Origin with { RelativePath = "src/Substitute.cs" } }));
                    Assert.Equal(1, await context.DocumentVersions.Where(value => value.VersionId == version.VersionId).ExecuteUpdateAsync(setters => setters.SetProperty(value => value.AnchorsJson, anchorsJson)));
                    break;
            }
        }

        AssertUnavailable(await Loader(fixture).LoadAsync(fixture.Corpus.Handle, roots));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("database")]
    [InlineData("target")]
    [InlineData("batch")]
    [InlineData("digest")]
    [InlineData("root-version")]
    [InlineData("root-unit")]
    public async Task LoadAsync_ForeignBindingOrRoot_NeverFallsBackToAnotherVersionAsync(string mismatch)
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store().AcceptOutcomeAsync(fixture.Request())).Status);
        var roots = await RootsAsync(fixture);
        var corpus = fixture.Corpus.Handle;
        switch (mismatch)
        {
            case "database": corpus = corpus with { Binding = corpus.Binding with { CanonicalDatabaseIdentity = "foreign-db" } }; break;
            case "target": corpus = corpus with { Binding = corpus.Binding with { TargetId = "foreign-target" } }; break;
            case "batch": corpus = corpus with { Binding = corpus.Binding with { BatchId = Guid.NewGuid() } }; break;
            case "digest": corpus = corpus with { CorpusDigest = new string('f', 64) }; break;
            case "root-version": roots = [roots[0] with { Document = roots[0].Document with { DocumentVersionId = Guid.NewGuid() } }]; break;
            case "root-unit": roots = [roots[0] with { UnitId = Guid.NewGuid() }]; break;
        }
        AssertUnavailable(await Loader(fixture).LoadAsync(corpus, roots));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("denied")]
    [InlineData("outage")]
    [InlineData("final-denial")]
    public async Task LoadAsync_LiveFullContributorAuthorityFails_DisclosesNothingAsync(string failure)
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store().AcceptOutcomeAsync(fixture.Request())).Status);
        var authority = new FlowLoaderAuthority
        {
            DeniedRepository = failure == "denied" ? fixture.ThirdRepository : null,
            Status = failure == "outage" ? DisclosureStatus.AuthorityUnavailable : DisclosureStatus.Allowed,
            RevokeAfterFirst = failure == "final-denial"
        };
        var result = await new OctoberFlowPlanningInputLoader(fixture.Acceptance.Factory, authority)
            .LoadAsync(fixture.Corpus.Handle, await RootsAsync(fixture));
        AssertUnavailable(result);
        Assert.NotEmpty(authority.Seen);
        Assert.All(authority.Seen, contributors => Assert.True(fixture.AllContributors.ToHashSet().SetEquals(contributors)));
        if (failure == "final-denial") Assert.True(authority.Seen.Count >= 2);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadAsync_OversizedStoredWitness_RejectsBeforeMaterializingItsTextAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store().AcceptOutcomeAsync(fixture.Request())).Status);
        var roots = await RootsAsync(fixture);
        await using var reference = fixture.Acceptance.Factory.CreateDbContext();
        var item = await reference.ResolutionItems.SingleAsync();
        string oversizedWitness = OctoberKnowledgePolicy.SerializeCanonical(Read<ResolutionWitness>(item.WitnessJson!) with { ConnectionExplanation = new string('x', 9 * 1024 * 1024) });
        Assert.Equal(1, await reference.ResolutionItems.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.WitnessJson, oversizedWitness)));
        var probe = new FlowWitnessMaterializationProbe();
        var options = new DbContextOptionsBuilder<OctoberDbContext>().UseSqlite(reference.Database.GetConnectionString()).AddInterceptors(probe).Options;
        var loader = new OctoberFlowPlanningInputLoader(new KnowledgeContextFactory(options), fixture.Authorization);

        AssertUnavailable(await loader.LoadAsync(fixture.Corpus.Handle, roots));

        Assert.False(probe.MaterializedWitness, "The loader must reject stored-byte overflow before reading the oversized witness into memory.");
        Assert.True(probe.Commands > 0);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadAsync_CancelledWhileFactoryIgnoresToken_ReturnsBoundedAndDisposesLateContextAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var roots = await RootsAsync(fixture);
        await using var reference = fixture.Acceptance.Factory.CreateDbContext();
        var options = new DbContextOptionsBuilder<OctoberDbContext>().UseSqlite(reference.Database.GetConnectionString()).Options;
        var factory = new AdmissionLifetimeFactory(options) { Delay = true };
        using var cancellation = new CancellationTokenSource();
        var pending = new OctoberFlowPlanningInputLoader(factory, fixture.Authorization).LoadAsync(fixture.Corpus.Handle, roots, cancellation.Token);
        try
        {
            await factory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2))));
            AssertUnavailable(await pending);
        }
        finally
        {
            factory.Release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            if (factory.DelayedContext is { } late)
            {
                await factory.Returned.Task.WaitAsync(TimeSpan.FromSeconds(5));
                for (int attempt = 0; attempt < 100 && Record.Exception(() => late.ChangeTracker.Entries().ToArray()) is not ObjectDisposedException; attempt++)
                    await Task.Delay(20);
                Assert.IsType<ObjectDisposedException>(Record.Exception(() => late.ChangeTracker.Entries().ToArray()));
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadAsync_CancelledWhileAuthorityIgnoresToken_ReturnsWithoutLateDisclosureAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store().AcceptOutcomeAsync(fixture.Request())).Status);
        var authority = new FlowLoaderAuthority { Block = true };
        using var cancellation = new CancellationTokenSource();
        var pending = new OctoberFlowPlanningInputLoader(fixture.Acceptance.Factory, authority)
            .LoadAsync(fixture.Corpus.Handle, await RootsAsync(fixture), cancellation.Token);
        try
        {
            await authority.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2))));
            AssertUnavailable(await pending);
        }
        finally { authority.Release.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
        AssertUnavailable(await pending);
    }

    private static OctoberFlowPlanningInputLoader Loader(ResolutionPersistenceFixture fixture) => new(fixture.Acceptance.Factory, fixture.Authorization);
    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, _json)!;
    private static void AssertUnavailable(FlowPlanningInputResult result)
    {
        Assert.Equal(KnowledgeReadStatus.Unavailable, result.Status);
        Assert.Null(result.Input);
    }
    private static async Task<ImmutableArray<FlowRootSelection>> RootsAsync(ResolutionPersistenceFixture fixture) =>
        [await RootAsync(fixture.Acceptance, fixture.Corpus.Documents[0].Version)];
    private static async Task<FlowRootSelection> RootAsync(OctoberKnowledgeAcceptanceFixture fixture, DocumentVersionReference document)
    {
        await using var context = fixture.Factory.CreateDbContext();
        var logical = await context.LogicalDocuments.SingleAsync(value => value.LogicalDocumentId == document.LogicalDocumentId);
        var unit = await context.CodeUnits.SingleAsync(value => value.UnitId == logical.UnitId);
        return new(unit.UnitId, document, FlowRootKind.SelectedCodeUnit, unit.SemanticSelector);
    }

    private static async Task<(CorpusHandle Corpus, ImmutableArray<FlowRootSelection> Roots, Contributor ZeroSource, Contributor GapSource)> CreateLocalCorpusAsync(OctoberKnowledgeAcceptanceFixture fixture)
    {
        var gapOrigin = fixture.Origin with { RepositoryId = OctoberKnowledgeAcceptanceFixture.Id(89005), RootBindingIdentity = "controlled-gap-root", RelativePath = "src/NotStarted.cs", SourceDigest = new string('d', 64) };
        await using (var context = fixture.Factory.CreateDbContext())
        {
            context.RepositorySelections.Add(new()
            {
                BatchId = fixture.Binding.BatchId, RepositoryId = gapOrigin.RepositoryId,
                RootBindingIdentity = gapOrigin.RootBindingIdentity, OperatorLabel = "controlled coverage-only repository"
            });
            await context.SaveChangesAsync();
        }
        var root = fixture.Proposal with { Observations = [] };
        var acceptedRoot = await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal(root));
        Assert.Equal(KnowledgeWriteStatus.Committed, acceptedRoot.Status);
        var zeroOrigin = fixture.Origin with { RelativePath = "src/NoUnits.cs", SourceDigest = new string('c', 64) };
        var zeroSource = new Contributor(ContributorKind.Source, zeroOrigin.RepositoryId, null, zeroOrigin.RootBindingIdentity, null, zeroOrigin.SourceDigest);
        var zero = root with
        {
            AdmissionId = OctoberKnowledgeAcceptanceFixture.Id(89000), AttemptId = OctoberKnowledgeAcceptanceFixture.Id(89001), Origin = zeroOrigin,
            Units = [], Observations = [],
            Uncertainties = [new("zero-unit-unknown", "This accepted file has no semantic unit and retains an explicit input uncertainty.",
                [new(null, null, new(zeroOrigin, 1, 2), null, zeroOrigin.SourceDigest)], ["No source-backed operation was accepted."])],
            Contributors = [zeroSource, fixture.UncitedContributor],
            Coverage = new(OctoberKnowledgeAcceptanceFixture.Id(89000), zeroOrigin, CoverageDisposition.CompletedZeroUnit, "The complete file contains no semantic units.")
        };
        fixture.Source.Result = new(zeroOrigin, 100);
        Assert.True(OctoberKnowledgePolicy.Validate(zero, fixture.Source.Result));
        var acceptedZero = await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal(zero, OctoberKnowledgeAcceptanceFixture.Id(89002)));
        Assert.Equal(KnowledgeWriteStatus.Committed, acceptedZero.Status);
        var gapSource = new Contributor(ContributorKind.Source, gapOrigin.RepositoryId, null, gapOrigin.RootBindingIdentity, null, gapOrigin.SourceDigest);
        var freeze = new CorpusFreezeRequest(new(fixture.Binding.BatchId, OperationKind.FreezeCorpus, OctoberKnowledgeAcceptanceFixture.Id(89003)), fixture.Binding, "",
            [acceptedRoot.AcceptedFile!, acceptedZero.AcceptedFile!], [root.Coverage, zero.Coverage, new(OctoberKnowledgeAcceptanceFixture.Id(89004), gapOrigin, CoverageDisposition.Unprocessed, "Controlled graceful-stop gap.")]);
        freeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
        fixture.FreezeAuthority.Approve(freeze);
        var frozen = await fixture.Writer().FreezeCorpusAsync(freeze);
        Assert.Equal(KnowledgeWriteStatus.Committed, frozen.Status);
        var selectedRoot = await RootAsync(fixture, acceptedRoot.AcceptedFile!.Documents[0]);
        return (frozen.Corpus!, [selectedRoot], zeroSource, gapSource);
    }
}

internal sealed class FlowLoaderAuthority : IOctoberKnowledgeProducerAuthorization
{
    public Guid? DeniedRepository { get; set; }
    public DisclosureStatus Status { get; set; } = DisclosureStatus.Allowed;
    public bool RevokeAfterFirst { get; set; }
    public bool Block { get; set; }
    public List<ImmutableArray<Contributor>> Seen { get; } = [];
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<DisclosureStatus> CheckAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default)
    {
        Seen.Add(contributors);
        Entered.TrySetResult();
        if (Block) await Release.Task; // Deliberately ignores cancellation to test bounded disclosure ownership.
        return RevokeAfterFirst && Seen.Count > 1 || DeniedRepository is { } denied && contributors.Any(value => value.RepositoryId == denied)
            ? DisclosureStatus.Denied : Status;
    }
}

internal sealed class FlowWitnessMaterializationProbe : DbCommandInterceptor
{
    public int Commands { get; private set; }
    public bool MaterializedWitness { get; private set; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Commands++;
        return ValueTask.FromResult(result);
    }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        for (int index = 0; index < result.FieldCount; index++)
            if (result.GetName(index) == "WitnessJson") MaterializedWitness = true;
        return ValueTask.FromResult(result);
    }
}
