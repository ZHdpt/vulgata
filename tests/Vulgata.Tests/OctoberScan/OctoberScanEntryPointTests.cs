using Microsoft.Extensions.Configuration;
using System.Text;
using System.Collections.Immutable;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Web.Data;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Configuration;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.OctoberGenerator;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

public sealed class OctoberScanEntryPointTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("observed")]
    [InlineData("arbitrary-field")]
    [InlineData("undefined-reason")]
    [InlineData("missing-reason")]
    [InlineData("wrong-boundary")]
    public async Task ResolutionDiagnostics_CliProjection_PreservesOnlyDefinedReasonAndWhitelistedFieldAsync(string condition)
    {
        var diagnostic = new OctoberBusinessDiagnosticSnapshot(null, null, null, null,
            condition == "wrong-boundary" ? OctoberBusinessDiagnosticBoundary.ExactLookup : OctoberBusinessDiagnosticBoundary.Resolution, false)
        {
            ResolutionFailureReason = condition == "undefined-reason" ? (ResolutionFailureReason)99999
                : condition == "missing-reason" ? null : ResolutionFailureReason.WitnessShape,
            ResolutionFailureField = condition == "arbitrary-field" ? "RAW_PRIVATE_MODEL_FIELD.Path[99999]" : "Caller.Document.BatchId"
        };
        Guid batch = Guid.NewGuid();
        var report = new OctoberScanReport("cl-and-bl", null, OctoberCoverageStatus.Partial, OctoberExecutionStatus.Failed,
            OctoberPublicationStatus.Failed, null, [], [], [], new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
            new(1, 30, 10, false), [new("business_resolution_failed")]) { BusinessDiagnostics = diagnostic };
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(
            new(OctoberGeneratorMode.Generate, "exact-target", batch, null, new() { OutputStage = "cl-and-bl" }),
            new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report)), output, error);
        Assert.Equal(1, exit);
        using var json = JsonDocument.Parse(output.ToString());
        var projected = json.RootElement.GetProperty("businessDiagnostics");
        var reason = projected.GetProperty("resolutionFailureReason");
        var field = projected.GetProperty("resolutionFailureField");
        if (condition is "undefined-reason" or "missing-reason" or "wrong-boundary")
        {
            Assert.Equal(JsonValueKind.Null, reason.ValueKind);
            Assert.Equal(JsonValueKind.Null, field.ValueKind);
        }
        else
        {
            Assert.Equal("WitnessShape", reason.GetString());
            if (condition == "arbitrary-field") Assert.Equal(JsonValueKind.Null, field.ValueKind);
            else Assert.Equal("Caller.Document.BatchId", field.GetString());
        }
        Assert.DoesNotContain("RAW_PRIVATE", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("99999", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("RAW_PRIVATE", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("99999", error.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("absent", 8192)]
    [InlineData("minimum", 1)]
    [InlineData("default", 8192)]
    [InlineData("maximum", 393216)]
    [InlineData("zero", 0)]
    [InlineData("above-maximum", 393217)]
    [InlineData("malformed", -1)]
    public void MaxOutputCap_WorkerConfigurationDefaultsAndEnforcesInclusiveBounds(string condition, int expected)
    {
        var values = new Dictionary<string, string?>();
        if (condition != "absent")
            values["October:WorkerMaxOutputTokens"] = condition == "malformed" ? "not-an-integer" : expected.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        if (condition is "zero" or "above-maximum" or "malformed")
        {
            var error = Assert.Throws<InvalidOperationException>(() => OctoberGeneratorEntryPoint.ReadWorkerMaxOutputTokens(configuration));
            Assert.DoesNotContain("not-an-integer", error.Message, StringComparison.Ordinal);
        }
        else Assert.Equal(expected, OctoberGeneratorEntryPoint.ReadWorkerMaxOutputTokens(configuration));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(null, true)]
    [InlineData("low", true)]
    [InlineData("", false)]
    [InlineData("LOW", false)]
    [InlineData(" low", false)]
    [InlineData("high", false)]
    [InlineData("PRIVATE_INVALID_VALUE", false)]
    [InlineData("nested", false)]
    public void DeepSeekLowReasoning_ConfigurationAcceptsOnlyAbsentOrExactLow(string? value, bool accepted)
    {
        var values = new Dictionary<string, string?>();
        if (value == "nested") values["October:DeepSeekWorkerReasoningEffort:Effort"] = "low";
        else if (value is not null) values["October:DeepSeekWorkerReasoningEffort"] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        if (accepted) Assert.Equal(value, OctoberGeneratorEntryPoint.ReadDeepSeekWorkerReasoningEffort(configuration));
        else
        {
            var error = Assert.Throws<InvalidOperationException>(() => OctoberGeneratorEntryPoint.ReadDeepSeekWorkerReasoningEffort(configuration));
            Assert.DoesNotContain("PRIVATE", error.Message);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task ConfiguredGenerate_ForwardsExactReportExitAndSafePublicationProjectionAsync(int expected)
    {
        var binding = new KnowledgeBinding("SECRET-CANONICAL-DATABASE", "explicit-target", Guid.NewGuid(), new string('a', 64));
        var publication = new PublicationBinding(binding, Guid.NewGuid());
        bool published = expected is 0 or 2;
        var report = new OctoberScanReport("cl-only", published || expected == 5 ? "cl-only" : null,
            expected == 2 ? OctoberCoverageStatus.Partial : OctoberCoverageStatus.Complete,
            expected switch { 1 => OctoberExecutionStatus.Failed, 3 => OctoberExecutionStatus.Interrupted, 4 => OctoberExecutionStatus.Indeterminate, _ => OctoberExecutionStatus.Completed },
            published ? OctoberPublicationStatus.Confirmed : expected == 5 ? OctoberPublicationStatus.NoContent : OctoberPublicationStatus.Failed,
            published ? publication : null, published ? [new(binding.BatchId, publication.PublicationId, Guid.NewGuid(), Guid.NewGuid())] : [], [], [],
            new(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(400)),
            new(null, null, null, true), [new("coverage_incomplete")]);
        using var output = new StringWriter(); using var error = new StringWriter();
        var command = new OctoberGeneratorCommand(OctoberGeneratorMode.Generate, binding.TargetId, binding.BatchId, null, new() { OutputStage = "cl-only" });
        var module = new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report));
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(command, module, output, error);
        Assert.Equal(expected, exit);
        Assert.Equal(1, module.Calls);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("cl-only", json.RootElement.GetProperty("requestedStage").GetString());
        Assert.DoesNotContain("SECRET-CANONICAL-DATABASE", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-CANONICAL-DATABASE", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("canonicalDatabaseIdentity", output.ToString(), StringComparison.OrdinalIgnoreCase);
        if (published)
        {
            var projected = json.RootElement.GetProperty("publication");
            Assert.Equal(publication.PublicationId, projected.GetProperty("publicationId").GetGuid());
            Assert.Equal(binding.BatchId, projected.GetProperty("batchId").GetGuid());
            Assert.Equal(binding.TargetId, projected.GetProperty("targetId").GetString());
            Assert.Equal(binding.SelectionDigest, projected.GetProperty("selectionDigest").GetString());
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task GenerateSelectedSentinel_IsFailureNotSuccessAsync()
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        int result = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(OctoberGeneratorMode.Generate, "target", Guid.NewGuid(), null),
            new Module(new(OctoberGeneratorExecutionStatus.Selected)), output, error);
        Assert.Equal(1, result);
        Assert.DoesNotContain("Confirmed", output.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(OctoberGeneratorMode.Prepare, OctoberGeneratorExecutionStatus.Prepared)]
    [InlineData(OctoberGeneratorMode.Select, OctoberGeneratorExecutionStatus.Selected)]
    public async Task ExistingPrepareSelect_PreserveTheirSuccessAsync(OctoberGeneratorMode mode, OctoberGeneratorExecutionStatus status)
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(0, await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(mode, "target", Guid.NewGuid(), null), new Module(new(status)), output, error));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualComposition_InvalidSelectedRootStopsBeforeOwnerBeginOrSecretUseAsync(bool business)
    {
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        string directory = Path.Combine(Path.GetTempPath(), "october-entrypoint-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "log"));
        try
        {
            var configuration = new OctoberRuntimeConfiguration(
                [new(harness.Execution.Setup.TargetId, Guid.NewGuid(), harness.Database.Origin.RepositoryId, harness.Database.Origin.RootBindingIdentity, Path.Combine(directory, "source"))],
                Path.Combine(directory, "tools", "codegraph.ps1"),
                new(Path.Combine(directory, "state"), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(15), 2 * 1024 * 1024),
                new([Path.Combine(directory, "tools")], [Path.Combine(directory, "runtime")], Path.Combine(directory, "system", "cmd.exe"), [Path.Combine(directory, "tools")], Path.Combine(directory, "jdk", "bin")), Path.Combine(directory, "log"));
            var secrets = new NoSecrets();
            int readerBindings = 0;
            var execution = harness.Execution with { Policy = harness.Execution.Policy with { OutputStage = business ? "cl-and-bl" : "cl-only" } };
            await using var actual = business
                ? OctoberGeneratorEntryPoint.ComposeGenerate(configuration, harness.Authority, harness.Database.Factory, secrets, execution.Policy, execution.Provider,
                    new OctoberBusinessSettings([], new(), 32768), (_, _, _) => { readerBindings++; throw new InvalidOperationException("Reader must not bind before exact source configuration"); })
                : OctoberGeneratorEntryPoint.ComposeGenerate(configuration, harness.Authority, harness.Database.Factory, secrets);
            var result = await actual.ExecuteAsync(execution);
            Assert.Equal(0, readerBindings);
            Assert.NotEqual(0, result.Report!.ExitCode);
            Assert.Equal(0, secrets.Accesses);
            await harness.Database.AssertNoKnowledgeAsync();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SettlementFailure_ProducesOneIndeterminateReportAndMatchingExitAsync()
    {
        var binding = new KnowledgeBinding("PRIVATE-DATABASE", "target", Guid.NewGuid(), new string('a', 64));
        var publication = new PublicationBinding(binding, Guid.NewGuid());
        var report = new OctoberScanReport("cl-only", "cl-only", OctoberCoverageStatus.Complete, OctoberExecutionStatus.Completed,
            OctoberPublicationStatus.Confirmed, publication, [new(binding.BatchId, publication.PublicationId, Guid.NewGuid(), Guid.NewGuid())], [], [],
            new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), new(1, 3, 2, false), []);
        using var output = new StringWriter(); using var error = new StringWriter();
        int settlements = 0;
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(OctoberGeneratorMode.Generate, "target", binding.BatchId, null),
            new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report)), output, error,
            settle: () => { settlements++; return ValueTask.FromException(new InvalidOperationException("PRIVATE cleanup exception path")); });
        Assert.Equal(1, settlements);
        Assert.Equal(4, exit);
        using var json = JsonDocument.Parse(output.ToString()); // Multiple success+failure documents cannot parse as one value.
        Assert.Contains("Indeterminate", json.RootElement.GetProperty("execution").ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(publication.PublicationId, json.RootElement.GetProperty("publication").GetProperty("publicationId").GetGuid());
        Assert.DoesNotContain("PRIVATE", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE", error.ToString(), StringComparison.Ordinal);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("publication-target")]
    [InlineData("publication-batch")]
    [InlineData("document-publication")]
    [InlineData("document-batch")]
    public async Task ForeignPublicationOrDocumentBinding_CannotEscapeAsSuccessfulHandlesAsync(string mismatch)
    {
        Guid batch = Guid.NewGuid();
        var binding = new KnowledgeBinding("PRIVATE-DATABASE", mismatch == "publication-target" ? "foreign-target" : "exact-target",
            mismatch == "publication-batch" ? Guid.NewGuid() : batch, new string('a', 64));
        var publication = new PublicationBinding(binding, Guid.NewGuid());
        var document = new DocumentHandle(mismatch == "document-batch" ? Guid.NewGuid() : batch,
            mismatch == "document-publication" ? Guid.NewGuid() : publication.PublicationId, Guid.NewGuid(), Guid.NewGuid());
        var report = new OctoberScanReport("cl-only", "cl-only", OctoberCoverageStatus.Complete, OctoberExecutionStatus.Completed,
            OctoberPublicationStatus.Confirmed, publication, [document], [], [],
            new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), new(1, 3, 2, false), []);
        using var output = new StringWriter(); using var error = new StringWriter();
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(OctoberGeneratorMode.Generate, "exact-target", batch, null),
            new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report)), output, error);
        Assert.NotEqual(0, exit);
        Assert.NotEqual(2, exit);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("publication").ValueKind);
        Assert.Equal(0, json.RootElement.GetProperty("documents").GetArrayLength());
        Assert.DoesNotContain(document.DocumentVersionId.ToString(), output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE-DATABASE", output.ToString(), StringComparison.Ordinal);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CoverageProjection_OmitsSourceProseAndPhysicalPaths_ButPreservesSafeIdentityAsync(bool physicalPath)
    {
        Guid batch = Guid.NewGuid(), admission = Guid.NewGuid(), manifest = Guid.NewGuid(), repository = Guid.NewGuid();
        const string sourceSecret = "PRIVATE-SOURCE-SECRET-PROSE";
        const string terminalSecret = "PRIVATE-TERMINAL-SECRET-PROSE";
        const string unsafePath = "C:/PRIVATE-PHYSICAL-SOURCE/Order.cs";
        string digest = new('b', 64);
        var origin = new PreparedOrigin(repository, "opaque-root-binding", physicalPath ? unsafePath : "src/Order.cs", digest, "opaque-approval");
        var acceptedCoverage = new FileCoverage(admission, origin, CoverageDisposition.CompletedZeroUnit, sourceSecret);
        var terminalCoverage = acceptedCoverage with { Reason = terminalSecret };
        var report = new OctoberScanReport("cl-only", "cl-only", OctoberCoverageStatus.Complete, OctoberExecutionStatus.Completed,
            OctoberPublicationStatus.NoContent, null, [], [new(manifest, admission, digest, [], acceptedCoverage)], [terminalCoverage],
            new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), new(1, 3, 2, false), []);
        using var output = new StringWriter(); using var error = new StringWriter();
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(OctoberGeneratorMode.Generate, "target", batch, null),
            new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report)), output, error);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.DoesNotContain(sourceSecret, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(terminalSecret, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-PHYSICAL-SOURCE", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-", error.ToString(), StringComparison.Ordinal);
        if (!physicalPath)
        {
            Assert.Equal(5, exit);
            var accepted = json.RootElement.GetProperty("acceptedFiles")[0];
            Assert.Equal(manifest, accepted.GetProperty("manifestId").GetGuid());
            Assert.Equal(admission, accepted.GetProperty("admissionId").GetGuid());
            Assert.Equal(digest, accepted.GetProperty("proposalDigest").GetString());
            var projectedOrigin = accepted.GetProperty("coverage").GetProperty("origin");
            Assert.Equal(repository, projectedOrigin.GetProperty("repositoryId").GetGuid());
            Assert.Equal("src/Order.cs", projectedOrigin.GetProperty("relativePath").GetString());
            Assert.Equal(digest, projectedOrigin.GetProperty("sourceDigest").GetString());
            Assert.Equal("src/Order.cs", json.RootElement.GetProperty("terminalCoverage")[0].GetProperty("origin").GetProperty("relativePath").GetString());
        }
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ClOnlyReport_ExplicitlyMarksMatchingAndBusinessLogicNotRequestedAsync()
    {
        var report = new OctoberScanReport("cl-only", "cl-only", OctoberCoverageStatus.Complete, OctoberExecutionStatus.Completed,
            OctoberPublicationStatus.NoContent, null, [], [], [],
            new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), new(0, 0, 0, false), []);
        using var output = new StringWriter(); using var error = new StringWriter();
        var module = new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report));
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(OctoberGeneratorMode.Generate, "target", Guid.NewGuid(), null), module, output, error);
        Assert.Equal(5, exit);
        Assert.Equal(1, module.Calls);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("not-requested", json.RootElement.GetProperty("matching").GetString());
        Assert.Equal("not-requested", json.RootElement.GetProperty("businessLogic").GetString());
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("throw")]
    [InlineData("cancel")]
    [InlineData("missing-report")]
    public async Task LostModuleReport_AfterInvocation_DoesNotFabricateZeroUsageAsync(string failure)
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        var module = new LostReportModule(failure);
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(OctoberGeneratorMode.Generate, "target", Guid.NewGuid(), null), module, output, error);
        Assert.Equal(1, module.Calls);
        Assert.NotEqual(0, exit);
        Assert.NotEqual(2, exit);
        using var json = JsonDocument.Parse(output.ToString());
        var usage = json.RootElement.GetProperty("usage");
        Assert.Equal(JsonValueKind.Null, usage.GetProperty("requests").ValueKind);
        Assert.Equal(JsonValueKind.Null, usage.GetProperty("inputTokens").ValueKind);
        Assert.Equal(JsonValueKind.Null, usage.GetProperty("outputTokens").ValueKind);
        Assert.True(usage.GetProperty("hasUnknownUsage").GetBoolean());
        Assert.DoesNotContain("PRIVATE-MODULE-FAILURE", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-MODULE-FAILURE", error.ToString(), StringComparison.Ordinal);
    }

    private sealed class LostReportModule(string failure) : IOctoberGeneratorModule
    {
        internal int Calls;
        public Task<OctoberGeneratorExecutionResult> ExecuteAsync(OctoberGeneratorCommand command, CancellationToken cancellationToken = default)
        {
            Calls++;
            return failure switch
            {
                "throw" => Task.FromException<OctoberGeneratorExecutionResult>(new InvalidOperationException("PRIVATE-MODULE-FAILURE")),
                "cancel" => Task.FromException<OctoberGeneratorExecutionResult>(new OperationCanceledException("PRIVATE-MODULE-FAILURE")),
                _ => Task.FromResult(new OctoberGeneratorExecutionResult(OctoberGeneratorExecutionStatus.GenerateFinished)),
            };
        }
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(OctoberPhaseStatus.Completed, OctoberPhaseStatus.Completed, "completed", "completed")]
    [InlineData(OctoberPhaseStatus.Partial, OctoberPhaseStatus.NoContent, "partial", "no-content")]
    [InlineData(OctoberPhaseStatus.Unavailable, OctoberPhaseStatus.Unavailable, "unavailable", "unavailable")]
    public async Task BusinessReport_ProjectsActualPhaseStatusesInsteadOfNotRequestedAsync(OctoberPhaseStatus matching, OctoberPhaseStatus business, string expectedMatching, string expectedBusiness)
    {
        var batch = Guid.NewGuid();
        var report = new OctoberScanReport("cl-and-bl", null, OctoberCoverageStatus.Partial, OctoberExecutionStatus.Failed,
            OctoberPublicationStatus.Failed, null, [], [], [], new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
            new(null, null, null, true), []) { Matching = matching, BusinessLogic = business };
        using var output = new StringWriter(); using var error = new StringWriter();
        await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(OctoberGeneratorMode.Generate, "target", batch, null, new() { OutputStage = "cl-and-bl" }),
            new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report)), output, error);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal(expectedMatching, json.RootElement.GetProperty("matching").GetString());
        Assert.Equal(expectedBusiness, json.RootElement.GetProperty("businessLogic").GetString());
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ActualBusinessReaderRegistrations_ResolveSameAuthorityAndOwnedAdaptersWithoutOpeningDatabaseAsync()
    {
        // DI resolution only: no PostgreSQL, authenticated Identity session, credential read or reader acquisition.
        const string identity = "postgres://localhost:1/controlled-di";
        const string connection = "Host=localhost;Port=1;Database=controlled_di;Username=unused;Password=unused";
        string directory = Path.Combine(Path.GetTempPath(), "october-reader-di-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var configuration = new OctoberAuthorityConfiguration("controlled-authority", "controlled-target", Guid.NewGuid(), identity,
                connection, connection, identity, ImmutableDictionary<string, string>.Empty.Add("controlled-target", identity),
                TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
            var services = new ServiceCollection();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            OctoberHeadlessAuthorityRegistration.AddOctoberLocalAuthority(services, configuration, new ForbiddenReaderCredentials(), TimeProvider.System);
            services.AddDbContext<OctoberDbContext>(options => options.UseNpgsql(connection));
            OctoberGeneratorEntryPoint.AddBusinessReaderDependencies(services, directory);
            await using var provider = services.BuildServiceProvider(validateScopes: true);
            await using var scope = provider.CreateAsyncScope();
            Assert.Same(scope.ServiceProvider.GetRequiredService<OctoberLocalAuthority>(), scope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>());
            var contexts = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OctoberDbContext>>();
            await using var context = await contexts.CreateDbContextAsync();
            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
            Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
            Assert.IsType<OctoberKnowledgeDataProtectionTokenProtector>(scope.ServiceProvider.GetRequiredService<IOctoberKnowledgeTokenProtector>());
            Assert.IsType<OctoberKnowledgeOperationLog>(scope.ServiceProvider.GetRequiredService<IOctoberKnowledgeReadLog>());
        }
        finally { Directory.Delete(directory, true); }
    }
    private sealed class ForbiddenReaderCredentials : IOctoberOperatorCredentialSource
    {
        public ValueTask<OctoberOperatorCredentials?> ReadAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("DI verification must not read credentials");
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("observed")]
    [InlineData("reason-none")]
    [InlineData("reason-null")]
    [InlineData("reason-undefined")]
    [InlineData("unknown")]
    [InlineData("undefined-status")]
    [InlineData("undefined-boundary")]
    [InlineData("cl-only")]
    [InlineData("foreign-publication")]
    [InlineData("cleanup-unconfirmed")]
    public async Task BusinessDiagnostics_ProjectOnlySafeObservedScalarsAndRespectOutputFencesAsync(string condition)
    {
        var snapshot = new OctoberBusinessDiagnosticSnapshot(BusinessFinalizationStatus.CommitIndeterminate, null,
            BusinessFinalizationStatus.Published, KnowledgeWriteStatus.AlreadyCommitted, OctoberBusinessDiagnosticBoundary.ExactLookup, false) { OriginalSynthesisFailureReason = BusinessSynthesisFailureReason.ProposalShape };
        if (condition == "reason-none") snapshot = snapshot with { OriginalSynthesisFailureReason = BusinessSynthesisFailureReason.None };
        if (condition == "reason-null") snapshot = snapshot with { OriginalSynthesisFailureReason = null };
        if (condition == "reason-undefined") snapshot = snapshot with { OriginalSynthesisFailureReason = (BusinessSynthesisFailureReason)99999 };
        if (condition == "unknown") snapshot = snapshot with { FinalizerStatus = null, LookupStatus = null, LookupPersistenceStatus = null };
        if (condition == "undefined-status") snapshot = snapshot with { FinalizerStatus = (BusinessFinalizationStatus)99999, FinalizerPersistenceStatus = (KnowledgeWriteStatus)99999, LookupStatus = (BusinessFinalizationStatus)99999, LookupPersistenceStatus = (KnowledgeWriteStatus)99999 };
        if (condition == "undefined-boundary") snapshot = snapshot with { Boundary = (OctoberBusinessDiagnosticBoundary)99999 };
        var batch = Guid.NewGuid();
        string stage = condition == "cl-only" ? "cl-only" : "cl-and-bl";
        var report = new OctoberScanReport(stage, null, OctoberCoverageStatus.Partial, OctoberExecutionStatus.Failed,
            OctoberPublicationStatus.Failed, null, [], [], [], new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
            new(null, null, null, true), [new("business_publication_unavailable"), new("business_PRIVATE_SECRET_UNRECOGNIZED")]) { BusinessDiagnostics = snapshot };
        if (condition == "foreign-publication")
        {
            var foreign = new PublicationBinding(new("PRIVATE-DATABASE", "foreign-target", batch, new string('a', 64)), Guid.NewGuid());
            report = report with { CompletedStage = stage, Execution = OctoberExecutionStatus.Completed, PublicationStatus = OctoberPublicationStatus.Confirmed,
                Publication = foreign, Documents = [new(batch, foreign.PublicationId, Guid.NewGuid(), Guid.NewGuid())] };
        }
        using var output = new StringWriter(); using var error = new StringWriter();
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(OctoberGeneratorMode.Generate, "exact-target", batch, null, new() { OutputStage = stage }),
            new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report)), output, error,
            settle: condition == "cleanup-unconfirmed" ? () => ValueTask.FromException(new InvalidOperationException("PRIVATE cleanup detail")) : null);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.DoesNotContain("PRIVATE", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("99999", output.ToString(), StringComparison.Ordinal);
        bool present = json.RootElement.TryGetProperty("businessDiagnostics", out var diagnostic) && diagnostic.ValueKind != JsonValueKind.Null;
        if (condition is "cl-only" or "foreign-publication" or "undefined-boundary") { Assert.False(present); return; }
        Assert.True(present);
        Assert.Equal(new[] { "boundary", "finalizerPersistenceStatus", "finalizerStatus", "lookupPersistenceStatus", "lookupStatus", "operationMismatch", "originalSynthesisFailureReason", "resolutionFailureField", "resolutionFailureReason" }, diagnostic.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(JsonValueKind.Null, diagnostic.GetProperty("resolutionFailureReason").ValueKind);
        Assert.Equal(JsonValueKind.Null, diagnostic.GetProperty("resolutionFailureField").ValueKind);
        Assert.Equal("ExactLookup", diagnostic.GetProperty("boundary").GetString());
        Assert.False(diagnostic.GetProperty("operationMismatch").GetBoolean());
        if (condition is "reason-none" or "reason-null" or "reason-undefined") Assert.Equal(JsonValueKind.Null, diagnostic.GetProperty("originalSynthesisFailureReason").ValueKind);
        else Assert.Equal("ProposalShape", diagnostic.GetProperty("originalSynthesisFailureReason").GetString());
        if (condition is "unknown" or "undefined-status")
        {
            foreach (string field in new[] { "finalizerStatus", "finalizerPersistenceStatus", "lookupStatus", "lookupPersistenceStatus" }) Assert.Equal(JsonValueKind.Null, diagnostic.GetProperty(field).ValueKind);
        }
        else
        {
            Assert.Equal("CommitIndeterminate", diagnostic.GetProperty("finalizerStatus").GetString());
            Assert.Equal(JsonValueKind.Null, diagnostic.GetProperty("finalizerPersistenceStatus").ValueKind);
            Assert.Equal("Published", diagnostic.GetProperty("lookupStatus").GetString());
            Assert.Equal("AlreadyCommitted", diagnostic.GetProperty("lookupPersistenceStatus").GetString());
        }
        Assert.Contains(json.RootElement.GetProperty("gaps").EnumerateArray(), gap => gap.GetProperty("code").GetString() == "business_publication_unavailable");
        if (condition == "cleanup-unconfirmed") { Assert.Equal(4, exit); Assert.Contains(json.RootElement.GetProperty("gaps").EnumerateArray(), gap => gap.GetProperty("code").GetString() == "owned_cleanup_unconfirmed"); }
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("defined")]
    [InlineData("undefined-category")]
    [InlineData("undefined-slot")]
    [InlineData("undefined-field-failure")]
    [InlineData("undefined-field")]
    [InlineData("negative-unit-index")]
    [InlineData("excess-unit-index")]
    [InlineData("structural")]
    [InlineData("completed")]
    [InlineData("wrong-stage")]
    [InlineData("cleared")]
    [InlineData("foreign-binding")]
    public async Task ParseDiagnostics_ProjectOnlyFixedWhitelistedScalarsForFailedParsingAsync(string condition)
    {
        var parse = new OctoberWorkerParseDiagnosticSnapshot(
            condition == "undefined-category" ? (OctoberWorkerParseFailure)99999 : OctoberWorkerParseFailure.ClosedFields,
            condition == "undefined-slot" ? (OctoberWorkerParseSlot)99999 : OctoberWorkerParseSlot.Claims,
            condition == "undefined-field-failure" ? (OctoberWorkerFieldFailure)99999 : condition == "structural" ? OctoberWorkerFieldFailure.Missing : null,
            condition == "undefined-field" ? (OctoberWorkerParseSlot)99999 : condition == "structural" ? OctoberWorkerParseSlot.ParentSelector : null,
            condition == "negative-unit-index" ? -1 : condition == "excess-unit-index" ? 65536 : condition == "structural" ? 0 : null);
        var snapshot = new OctoberWorkerDiagnosticSnapshot(
            condition == "completed" ? OctoberFileWorkerStatus.Completed : OctoberFileWorkerStatus.InvalidOutput,
            OctoberWorkerRetryDisposition.None,
            condition == "wrong-stage" ? OctoberWorkerFailureStage.ProposalValidation : OctoberWorkerFailureStage.ProposalParse)
        { ParseFailure = condition == "cleared" ? null : parse };
        var batch = Guid.NewGuid();
        var report = new OctoberScanReport("cl-only", null, OctoberCoverageStatus.Partial, OctoberExecutionStatus.Failed,
            OctoberPublicationStatus.Failed, null, [], [], [], new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
            new(null, null, null, true), []) { WorkerDiagnostics = snapshot };
        if (condition == "foreign-binding") report = report with { Publication = new(new("PRIVATE_DATABASE", "foreign-target", batch, new string('a', 64)), Guid.NewGuid()) };
        using var output = new StringWriter(); using var error = new StringWriter();
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(
            new(OctoberGeneratorMode.Generate, "exact-target", batch, null, new() { OutputStage = "cl-only" }),
            new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report)), output, error);
        Assert.NotEqual(0, exit);
        Assert.DoesNotContain("PRIVATE", output.ToString()); Assert.DoesNotContain("99999", output.ToString());
        Assert.DoesNotContain("65536", output.ToString());
        using var json = JsonDocument.Parse(output.ToString());
        bool present = json.RootElement.TryGetProperty("workerDiagnostics", out var worker) && worker.ValueKind != JsonValueKind.Null;
        if (condition == "foreign-binding") { Assert.False(present); return; }
        Assert.True(present);
        var projected = worker.GetProperty("parseFailure");
        if (condition is "completed" or "wrong-stage" or "cleared")
        {
            Assert.Equal(JsonValueKind.Null, projected.ValueKind);
            return;
        }
        Assert.Equal(new[] { "category", "field", "fieldFailure", "slot", "unitIndex" }, projected.EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal).ToArray());
        if (condition == "undefined-category") Assert.Equal(JsonValueKind.Null, projected.GetProperty("category").ValueKind);
        else Assert.Equal("ClosedFields", projected.GetProperty("category").GetString());
        if (condition == "undefined-slot") Assert.Equal(JsonValueKind.Null, projected.GetProperty("slot").ValueKind);
        else Assert.Equal("Claims", projected.GetProperty("slot").GetString());
        if (condition == "structural")
        {
            Assert.Equal("Missing", projected.GetProperty("fieldFailure").GetString());
            Assert.Equal("ParentSelector", projected.GetProperty("field").GetString());
            Assert.Equal(0, projected.GetProperty("unitIndex").GetInt32());
        }
        else
        {
            Assert.Equal(JsonValueKind.Null, projected.GetProperty("fieldFailure").ValueKind);
            Assert.Equal(JsonValueKind.Null, projected.GetProperty("field").ValueKind);
            Assert.Equal(JsonValueKind.Null, projected.GetProperty("unitIndex").ValueKind);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("absent")]
    [InlineData("valid")]
    [InlineData("negative-reserved")]
    [InlineData("over-run-limit")]
    [InlineData("started-exceeds-reserved")]
    [InlineData("foreign-binding")]
    public async Task HardRetryBudgetProjection_EmitsOnlyBoundedCountsForMatchingOutputAsync(string condition)
    {
        var batch = Guid.NewGuid();
        int reserved = condition == "negative-reserved" ? -1 : condition == "over-run-limit" ? 17 : 2;
        int started = condition == "started-exceeds-reserved" ? 3 : 1;
        var report = new OctoberScanReport("cl-only", null, OctoberCoverageStatus.Partial, OctoberExecutionStatus.Failed,
            OctoberPublicationStatus.Failed, null, [], [], [], new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
            new(null, null, null, true), [])
        {
            HardRetry = condition == "absent" ? null : new(reserved, started)
        };
        if (condition == "foreign-binding")
            report = report with { Publication = new(new("PRIVATE_DATABASE", "foreign-target", batch, new string('a', 64)), Guid.NewGuid()) };
        using var output = new StringWriter();
        using var error = new StringWriter();
        await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(
            new(OctoberGeneratorMode.Generate, "exact-target", batch, null, new() { OutputStage = "cl-only" }),
            new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report)), output, error);

        using var json = JsonDocument.Parse(output.ToString());
        bool present = json.RootElement.TryGetProperty("hardRetry", out var projected) && projected.ValueKind != JsonValueKind.Null;
        if (condition is "absent" or "negative-reserved" or "over-run-limit" or "started-exceeds-reserved" or "foreign-binding")
        {
            Assert.False(present);
            Assert.DoesNotContain("PRIVATE_DATABASE", output.ToString(), StringComparison.Ordinal);
            return;
        }
        Assert.True(present);
        Assert.Equal(new[] { "providerCallsStarted", "reservedAttempts" },
            projected.EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(2, projected.GetProperty("reservedAttempts").GetInt32());
        Assert.Equal(1, projected.GetProperty("providerCallsStarted").GetInt32());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("defined")]
    [InlineData("zero")]
    [InlineData("missing")]
    [InlineData("wrong-status")]
    [InlineData("wrong-stage")]
    [InlineData("wrong-failure")]
    [InlineData("undefined-enum")]
    [InlineData("negative-counts")]
    [InlineData("excess-cap")]
    [InlineData("max-cap")]
    public async Task UnsupportedFinishObservation_ConsoleProjectsOnlyWhitelistedScalarsAtExactFailureBoundaryAsync(string condition)
    {
        var observation = condition == "missing" ? new OctoberWorkerTerminalObservation() : new OctoberWorkerTerminalObservation(
            condition == "undefined-enum" ? (OctoberWorkerUnsupportedFinish)99999 : OctoberWorkerUnsupportedFinish.Length,
            condition == "negative-counts" ? -1 : condition == "excess-cap" ? 393217 : condition == "max-cap" ? 393216 : 137,
            condition == "negative-counts" ? -1 : condition == "zero" ? 0 : 101,
            condition == "negative-counts" ? -2 : condition == "zero" ? 0 : 37,
            condition == "negative-counts" ? -3 : condition == "zero" ? 0 : 29);
        var snapshot = new OctoberWorkerDiagnosticSnapshot(
            condition == "wrong-status" ? OctoberFileWorkerStatus.Completed : OctoberFileWorkerStatus.InvalidOutput,
            OctoberWorkerRetryDisposition.None,
            condition == "wrong-stage" ? OctoberWorkerFailureStage.ProposalParse : OctoberWorkerFailureStage.StreamTerminal)
        {
            TerminalFailure = condition == "wrong-failure" ? OctoberWorkerTerminalFailure.MissingFinish : OctoberWorkerTerminalFailure.UnsupportedFinish,
            TerminalObservation = observation
        };
        var batch = Guid.NewGuid();
        var report = new OctoberScanReport("cl-only", null, OctoberCoverageStatus.Partial, OctoberExecutionStatus.Failed,
            OctoberPublicationStatus.Failed, null, [], [], [], new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
            new(null, null, null, true), []) { WorkerDiagnostics = snapshot };
        using var output = new StringWriter(); using var error = new StringWriter();
        await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(OctoberGeneratorMode.Generate, "exact-target", batch, null, new() { OutputStage = "cl-only" }),
            new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report)), output, error);
        Assert.DoesNotContain("99999", output.ToString());
        using var json = JsonDocument.Parse(output.ToString());
        var projected = json.RootElement.GetProperty("workerDiagnostics").GetProperty("terminalObservation");
        if (condition is "wrong-status" or "wrong-stage" or "wrong-failure") { Assert.Equal(JsonValueKind.Null, projected.ValueKind); return; }
        Assert.Equal(new[] { "finishReason", "inputTokens", "maximumOutputTokens", "outputTokens", "reasoningTokens" }, projected.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        if (condition == "missing") { Assert.All(projected.EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind)); return; }
        if (condition == "undefined-enum") Assert.Equal(JsonValueKind.Null, projected.GetProperty("finishReason").ValueKind);
        else Assert.Equal("Length", projected.GetProperty("finishReason").GetString());
        if (condition is "negative-counts" or "excess-cap") Assert.Equal(JsonValueKind.Null, projected.GetProperty("maximumOutputTokens").ValueKind);
        else if (condition == "max-cap") Assert.Equal(393216, projected.GetProperty("maximumOutputTokens").GetInt32());
        else Assert.Equal(137, projected.GetProperty("maximumOutputTokens").GetInt32());
        foreach (var name in new[] { "inputTokens", "outputTokens", "reasoningTokens" })
            if (condition == "negative-counts") Assert.Equal(JsonValueKind.Null, projected.GetProperty(name).ValueKind);
            else Assert.Equal(condition == "zero" ? 0 : name == "inputTokens" ? 101 : name == "outputTokens" ? 37 : 29, projected.GetProperty(name).GetInt64());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("defined")]
    [InlineData("terminal")]
    [InlineData("completed-terminal")]
    [InlineData("undefined-terminal")]
    [InlineData("budget")]
    [InlineData("undefined-budget")]
    [InlineData("undefined")]
    [InlineData("absent")]
    [InlineData("foreign-binding")]
    public async Task WorkerDiagnostics_ProjectOnlyDefinedScalarsWithoutPayloadAsync(string condition)
    {
        var snapshot = condition == "absent" ? null : new OctoberWorkerDiagnosticSnapshot(
            OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerRetryDisposition.None, OctoberWorkerFailureStage.ProposalParse);
        if (snapshot is not null) snapshot = snapshot with { BudgetFailure = OctoberWorkerBudgetFailure.OutputBytes };
        if (condition is "budget" or "undefined-budget") snapshot = snapshot! with { Status = OctoberFileWorkerStatus.BudgetExceeded, FailureStage = OctoberWorkerFailureStage.StreamTerminal, BudgetFailure = condition == "budget" ? OctoberWorkerBudgetFailure.OutputBytes : (OctoberWorkerBudgetFailure)99999 };
        if (condition == "terminal") snapshot = snapshot! with { FailureStage = OctoberWorkerFailureStage.StreamTerminal, TerminalFailure = OctoberWorkerTerminalFailure.MissingFinish };
        if (condition == "completed-terminal") snapshot = snapshot! with { Status = OctoberFileWorkerStatus.Completed, TerminalFailure = OctoberWorkerTerminalFailure.MissingFinish };
        if (condition == "undefined-terminal") snapshot = snapshot! with { TerminalFailure = (OctoberWorkerTerminalFailure)99999 };
        if (condition == "undefined") snapshot = new((OctoberFileWorkerStatus)99999, (OctoberWorkerRetryDisposition)99999, (OctoberWorkerFailureStage)99999) { TerminalFailure = (OctoberWorkerTerminalFailure)99999 };
        var batch = Guid.NewGuid();
        var report = new OctoberScanReport("cl-only", null, OctoberCoverageStatus.Partial, OctoberExecutionStatus.Failed,
            OctoberPublicationStatus.Failed, null, [], [], [], new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
            new(null, null, null, true), []) { WorkerDiagnostics = snapshot };
        if (condition == "foreign-binding") report = report with { Publication = new(new("PRIVATE_DB", "foreign-target", batch, new string('a', 64)), Guid.NewGuid()) };
        using var output = new StringWriter(); using var error = new StringWriter();
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(OctoberGeneratorMode.Generate, "exact-target", batch, null, new() { OutputStage = "cl-only" }),
            new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report)), output, error);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.NotEqual(0, exit); Assert.DoesNotContain("PRIVATE", output.ToString()); Assert.DoesNotContain("99999", output.ToString());
        bool present = json.RootElement.TryGetProperty("workerDiagnostics", out var diagnostic) && diagnostic.ValueKind != JsonValueKind.Null;
        if (condition is "absent" or "foreign-binding") { Assert.False(present); return; }
        Assert.True(present);
        Assert.Equal(new[] { "budgetFailure", "captureStatus", "failureStage", "httpObservation", "parseFailure", "retryDisposition", "status", "terminalFailure", "terminalObservation", "toolFailure" }, diagnostic.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        if (condition == "undefined") { Assert.All(diagnostic.EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind)); return; }
        Assert.Equal(condition is "budget" or "undefined-budget" ? "BudgetExceeded" : condition == "completed-terminal" ? "Completed" : "InvalidOutput", diagnostic.GetProperty("status").GetString());
        Assert.Equal("None", diagnostic.GetProperty("retryDisposition").GetString());
        Assert.Equal(condition is "terminal" or "budget" or "undefined-budget" ? "StreamTerminal" : "ProposalParse", diagnostic.GetProperty("failureStage").GetString());
        if (condition == "terminal") Assert.Equal("MissingFinish", diagnostic.GetProperty("terminalFailure").GetString());
        else Assert.Equal(JsonValueKind.Null, diagnostic.GetProperty("terminalFailure").ValueKind);
        if (condition == "budget") Assert.Equal("OutputBytes", diagnostic.GetProperty("budgetFailure").GetString());
        else Assert.Equal(JsonValueKind.Null, diagnostic.GetProperty("budgetFailure").ValueKind);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("java")]
    [InlineData("java-source-currentness")]
    [InlineData("source")]
    [InlineData("graph")]
    [InlineData("unknown-status")]
    [InlineData("unknown-operation")]
    [InlineData("unknown-boundary")]
    [InlineData("completed-worker")]
    [InlineData("foreign-binding")]
    public async Task ToolFirstCauseProjection_IsBoundedOperationSpecificAndContainsNoPayloadAsync(string condition)
    {
        var operation = condition == "source" ? SourceMetadataOperation.ReadTextRange : condition == "graph" ? SourceMetadataOperation.CodeGraphExplore : SourceMetadataOperation.JavaDefinition;
        var cause = new OctoberToolDiagnosticSnapshot(operation, OctoberToolFailureBoundary.RuntimeOpen,
            PreparedJavaNavigationOpenStatus.ReadinessFailed, PreparedJavaNavigationOutcome.ReadinessFailed,
            PreparedSourceOutcome.Incomplete, CodeGraphToolInvocationStatus.Unavailable, true);
        if (condition == "java-source-currentness") cause = cause with { Boundary = OctoberToolFailureBoundary.SourceCurrentness };
        if (condition == "unknown-status") cause = cause with { JavaOpenStatus = (PreparedJavaNavigationOpenStatus)99999, JavaOutcome = (PreparedJavaNavigationOutcome)99999, SourceOutcome = (PreparedSourceOutcome)99999, CodeGraphStatus = (CodeGraphToolInvocationStatus)99999, CleanupConfirmed = null };
        if (condition == "unknown-operation") cause = cause with { Operation = (SourceMetadataOperation)99999 };
        if (condition == "unknown-boundary") cause = cause with { Boundary = (OctoberToolFailureBoundary)99999 };
        var snapshot = new OctoberWorkerDiagnosticSnapshot(condition == "completed-worker" ? OctoberFileWorkerStatus.Completed : OctoberFileWorkerStatus.ProviderFailed, OctoberWorkerRetryDisposition.None, OctoberWorkerFailureStage.ToolInvocation) { ToolFailure = cause };
        var batch = Guid.NewGuid();
        var report = new OctoberScanReport("cl-only", null, OctoberCoverageStatus.Partial, OctoberExecutionStatus.Failed, OctoberPublicationStatus.Failed,
            null, [], [], [], new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), new(null, null, null, true), []) { WorkerDiagnostics = snapshot };
        if (condition == "foreign-binding") report = report with { Publication = new(new("PRIVATE_DB", "foreign-target", batch, new string('a', 64)), Guid.NewGuid()) };
        using var output = new StringWriter(); using var error = new StringWriter();
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(OctoberGeneratorMode.Generate, "exact-target", batch, null, new() { OutputStage = "cl-only" }), new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report)), output, error);
        Assert.NotEqual(0, exit); Assert.DoesNotContain("PRIVATE", output.ToString()); Assert.DoesNotContain("99999", output.ToString());
        using var document = JsonDocument.Parse(output.ToString());
        bool hasWorker = document.RootElement.TryGetProperty("workerDiagnostics", out var worker) && worker.ValueKind != JsonValueKind.Null;
        if (condition == "foreign-binding") { Assert.False(hasWorker); return; }
        Assert.True(hasWorker);
        bool present = worker.TryGetProperty("toolFailure", out var projected) && projected.ValueKind != JsonValueKind.Null;
        if (condition is "unknown-operation" or "unknown-boundary" or "completed-worker") { Assert.False(present); return; }
        Assert.True(present); Assert.InRange(Encoding.UTF8.GetByteCount(projected.GetRawText()), 1, 512);
        Assert.Equal(new[] { "boundary", "cleanupConfirmed", "codeGraphStatus", "javaOpenStatus", "javaOutcome", "operation", "sourceOutcome" }, projected.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(operation.ToString(), projected.GetProperty("operation").GetString()); Assert.Equal(condition == "java-source-currentness" ? "SourceCurrentness" : "RuntimeOpen", projected.GetProperty("boundary").GetString());
        foreach (string field in new[] { "javaOpenStatus", "javaOutcome", "sourceOutcome", "codeGraphStatus" })
        {
            bool retained = (condition is "java" or "java-source-currentness") && (field is "javaOpenStatus" or "javaOutcome") || (condition is "source" or "java-source-currentness") && field == "sourceOutcome" || condition == "graph" && field == "codeGraphStatus";
            if (!retained) Assert.Equal(JsonValueKind.Null, projected.GetProperty(field).ValueKind);
        }
        if (condition is "java" or "java-source-currentness") { Assert.Equal("ReadinessFailed", projected.GetProperty("javaOpenStatus").GetString()); Assert.Equal("ReadinessFailed", projected.GetProperty("javaOutcome").GetString()); }
        if (condition is "source" or "java-source-currentness") Assert.Equal("Incomplete", projected.GetProperty("sourceOutcome").GetString());
        if (condition == "graph") Assert.Equal("Unavailable", projected.GetProperty("codeGraphStatus").GetString());
        if (condition == "unknown-status") Assert.Equal(JsonValueKind.Null, projected.GetProperty("cleanupConfirmed").ValueKind);
        else Assert.True(projected.GetProperty("cleanupConfirmed").GetBoolean());
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("absent")]
    [InlineData("disabled")]
    [InlineData("valid")]
    [InlineData("partial")]
    [InlineData("unknown")]
    [InlineData("disabled-with-fields")]
    [InlineData("wrong-target")]
    [InlineData("wrong-batch")]
    [InlineData("relative-directory")]
    [InlineData("log-overlap")]
    public void ParseCaptureConfiguration_IsExplicitRunBoundAndDefaultOffAsync(string condition)
    {
        string parent = Path.Combine(Path.GetTempPath(), "october-capture-config-" + Guid.NewGuid().ToString("N"));
        Guid batch = Guid.NewGuid();
        const string prefix = "October:ParseFailureCapture:";
        var values = new Dictionary<string, string?>();
        if (condition != "absent") values[prefix + "Enabled"] = condition.StartsWith("disabled", StringComparison.Ordinal) ? "false" : "true";
        if (condition is not ("absent" or "disabled"))
        {
            values[prefix + "TargetId"] = condition == "wrong-target" ? "foreign" : "target";
            values[prefix + "BatchId"] = (condition == "wrong-batch" ? Guid.NewGuid() : batch).ToString();
            values[prefix + "SubjectId"] = "controlled-subject";
            values[prefix + "ManagementAuthorityId"] = "controlled-authority";
            values[prefix + "Directory"] = condition == "relative-directory" ? "relative-capture" : Path.Combine(parent, condition == "log-overlap" ? "log" : "capture");
        }
        if (condition == "partial") values.Remove(prefix + "SubjectId");
        if (condition == "unknown") values[prefix + "Unexpected"] = "SYNTHETIC_SECRET";
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var command = new OctoberGeneratorCommand(OctoberGeneratorMode.Generate, "target", batch, null, new() { OutputStage = "cl-only" });
        var runtime = new OctoberRuntimeConfiguration([], Path.Combine(parent, "tools", "codegraph.ps1"),
            new(Path.Combine(parent, "state"), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(15), 2 * 1024 * 1024),
            new([Path.Combine(parent, "tools")], [Path.Combine(parent, "runtime")], Path.Combine(parent, "system", "cmd.exe"), [Path.Combine(parent, "tools")], Path.Combine(parent, "jdk", "bin")), Path.Combine(parent, "log"));
        if (condition is "absent" or "disabled")
            Assert.Null(OctoberGeneratorEntryPoint.ReadParseFailureCaptureOptions(configuration, command, null, string.Empty));
        else if (condition == "valid")
        {
            var options = Assert.IsType<OctoberParseFailureCaptureOptions>(OctoberGeneratorEntryPoint.ReadParseFailureCaptureOptions(configuration, command, runtime, Path.Combine(parent, "keys")));
            Assert.Equal("target", options.TargetId); Assert.Equal(batch, options.BatchId);
            Assert.Equal("controlled-subject", options.SubjectId); Assert.Equal("controlled-authority", options.ManagementAuthorityId);
            Assert.True(options.Directory == Path.Combine(parent, "capture"));
        }
        else
        {
            var error = Assert.Throws<InvalidOperationException>(() => OctoberGeneratorEntryPoint.ReadParseFailureCaptureOptions(configuration, command, runtime, Path.Combine(parent, "keys")));
            Assert.DoesNotContain("SYNTHETIC", error.Message); Assert.DoesNotContain(parent, error.Message);
        }
        Assert.False(Directory.Exists(parent));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("captured")]
    [InlineData("unknown")]
    [InlineData("completed")]
    [InlineData("wrong-stage")]
    [InlineData("foreign-binding")]
    public async Task ParseCaptureStatus_ProjectionNeverEchoesCaptureDataOrUndefinedStatusAsync(string condition)
    {
        var batch = Guid.NewGuid();
        var worker = new OctoberWorkerDiagnosticSnapshot(condition == "completed" ? OctoberFileWorkerStatus.Completed : OctoberFileWorkerStatus.InvalidOutput,
            OctoberWorkerRetryDisposition.None, condition == "wrong-stage" ? OctoberWorkerFailureStage.ProposalValidation : OctoberWorkerFailureStage.ProposalParse)
        { CaptureStatus = condition == "unknown" ? (OctoberWorkerCaptureStatus)99999 : OctoberWorkerCaptureStatus.Captured };
        var report = new OctoberScanReport("cl-only", null, OctoberCoverageStatus.Partial, OctoberExecutionStatus.Failed,
            OctoberPublicationStatus.Failed, null, [], [], [], new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
            new(null, null, null, true), []) { WorkerDiagnostics = worker };
        if (condition == "foreign-binding") report = report with { Publication = new(new("SYNTHETIC_DATABASE", "foreign", batch, new string('a', 64)), Guid.NewGuid()) };
        using var output = new StringWriter(); using var error = new StringWriter();
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(OctoberGeneratorMode.Generate, "target", batch, null, new() { OutputStage = "cl-only" }),
            new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report)), output, error);
        Assert.NotEqual(0, exit); Assert.DoesNotContain("SYNTHETIC", output.ToString()); Assert.DoesNotContain("99999", output.ToString());
        using var json = JsonDocument.Parse(output.ToString());
        bool present = json.RootElement.TryGetProperty("workerDiagnostics", out var diagnostic) && diagnostic.ValueKind != JsonValueKind.Null;
        if (condition == "foreign-binding") { Assert.False(present); return; }
        Assert.True(present);
        var capture = diagnostic.GetProperty("captureStatus");
        if (condition == "captured") Assert.Equal("Captured", capture.GetString());
        else Assert.Equal(JsonValueKind.Null, capture.ValueKind);
        Assert.DoesNotContain("directory", diagnostic.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payload", diagnostic.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("equivalent-layer")]
    [InlineData("conflicting-layer")]
    public void ParseCaptureConfiguration_JsonBooleanAndEquivalentProviderLayerUseSameMeaning(string condition)
    {
        Guid batch = Guid.NewGuid();
        var section = new Dictionary<string, object?> { ["Enabled"] = condition != "false" };
        if (condition != "false")
        {
            section["TargetId"] = "target"; section["BatchId"] = batch.ToString();
            section["SubjectId"] = "controlled-subject"; section["ManagementAuthorityId"] = "controlled-authority";
            section["Directory"] = @"C:\capture-diagnostic\output";
        }
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(new { October = new { ParseFailureCapture = section } });
        using var stream = new MemoryStream(json);
        var builder = new ConfigurationBuilder().AddJsonStream(stream);
        if (condition is "equivalent-layer" or "conflicting-layer")
            builder.AddInMemoryCollection(new Dictionary<string, string?> { ["October:ParseFailureCapture:Enabled"] = condition == "equivalent-layer" ? "true" : "false" });
        var configuration = builder.Build();
        var command = new OctoberGeneratorCommand(OctoberGeneratorMode.Generate, "target", batch, null, new() { OutputStage = "cl-only" });
        if (condition == "false")
            Assert.Null(OctoberGeneratorEntryPoint.ReadParseFailureCaptureOptions(configuration, command, null, string.Empty));
        else if (condition == "conflicting-layer")
            Assert.Throws<InvalidOperationException>(() => OctoberGeneratorEntryPoint.ReadParseFailureCaptureOptions(configuration, command, CaptureConfigurationRuntime(), @"C:\capture-diagnostic\keys"));
        else
        {
            var options = Assert.IsType<OctoberParseFailureCaptureOptions>(OctoberGeneratorEntryPoint.ReadParseFailureCaptureOptions(configuration, command, CaptureConfigurationRuntime(), @"C:\capture-diagnostic\keys"));
            Assert.Equal("target", options.TargetId); Assert.Equal(batch, options.BatchId);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(@"\\controlled-unreachable\share\runtime.json", false)]
    [InlineData(@"\\?\C:\controlled\runtime.json", false)]
    [InlineData(@"\\.\C:\controlled\runtime.json", false)]
    [InlineData(@"relative\runtime.json", false)]
    [InlineData(@"G:\", true)]
    public void ParseCaptureConfiguration_ExclusionPathsAreValidatedLexicallyWithoutOpeningThem(string excluded, bool accepted)
    {
        var batch = Guid.NewGuid();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["October:ParseFailureCapture:Enabled"] = "true",
            ["October:ParseFailureCapture:TargetId"] = "target",
            ["October:ParseFailureCapture:BatchId"] = batch.ToString(),
            ["October:ParseFailureCapture:SubjectId"] = "controlled-subject",
            ["October:ParseFailureCapture:ManagementAuthorityId"] = "controlled-authority",
            ["October:ParseFailureCapture:Directory"] = @"C:\capture-diagnostic\output",
            ["October:RuntimeFile"] = excluded
        }).Build();
        var command = new OctoberGeneratorCommand(OctoberGeneratorMode.Generate, "target", batch, null, new() { OutputStage = "cl-only" });
        // ReadOptions only: deliberately never instantiate a sink or probe these paths.
        if (accepted) Assert.NotNull(OctoberGeneratorEntryPoint.ReadParseFailureCaptureOptions(configuration, command, CaptureConfigurationRuntime(), @"C:\capture-diagnostic\keys"));
        else Assert.Throws<InvalidOperationException>(() => OctoberGeneratorEntryPoint.ReadParseFailureCaptureOptions(configuration, command, CaptureConfigurationRuntime(), @"C:\capture-diagnostic\keys"));
    }

    private static OctoberRuntimeConfiguration CaptureConfigurationRuntime() => new([], @"C:\capture-diagnostic\tools\codegraph.ps1",
        new(@"C:\capture-diagnostic\state", TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(15), 2 * 1024 * 1024),
        new([@"C:\capture-diagnostic\tools"], [@"C:\capture-diagnostic\runtime"], @"C:\capture-diagnostic\system\cmd.exe", [@"C:\capture-diagnostic\tools"], @"C:\capture-diagnostic\jdk\bin"), @"C:\capture-diagnostic\log");
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("absent")]
    [InlineData("disabled")]
    [InlineData("valid")]
    [InlineData("partial")]
    [InlineData("unknown")]
    [InlineData("mutual-capture")]
    [InlineData("foreign-target")]
    public void HttpObservationConfiguration_IsDefaultOffExactAndMutuallyExclusiveWithCapture(string condition)
    {
        var batch = Guid.NewGuid(); const string prefix = "October:WorkerHttpObservation:";
        var values = new Dictionary<string, string?>();
        if (condition != "absent") values[prefix + "Enabled"] = condition == "disabled" ? "false" : "true";
        if (condition is not ("absent" or "disabled"))
        {
            values[prefix + "TargetId"] = condition == "foreign-target" ? "foreign" : "target";
            values[prefix + "BatchId"] = batch.ToString(); values[prefix + "SubjectId"] = "subject";
            values[prefix + "ManagementAuthorityId"] = "authority";
        }
        if (condition == "partial") values.Remove(prefix + "SubjectId");
        if (condition == "unknown") values[prefix + "Payload"] = "SYNTHETIC_SECRET";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var command = new OctoberGeneratorCommand(OctoberGeneratorMode.Generate, "target", batch, null, new() { OutputStage = "cl-only" });
        OctoberParseFailureCaptureOptions? capture = condition == "mutual-capture" ? new("target", batch, "subject", "authority", @"C:\synthetic-unused") : null;
        if (condition is "absent" or "disabled") Assert.Null(OctoberGeneratorEntryPoint.ReadWorkerHttpObservationOptions(configuration, command, capture));
        else if (condition == "valid")
        {
            var options = Assert.IsType<OctoberWorkerHttpObservationOptions>(OctoberGeneratorEntryPoint.ReadWorkerHttpObservationOptions(configuration, command, capture));
            Assert.Equal("target", options.TargetId); Assert.Equal(batch, options.BatchId);
            Assert.Equal("subject", options.SubjectId); Assert.Equal("authority", options.ManagementAuthorityId);
        }
        else
        {
            var error = Assert.Throws<InvalidOperationException>(() => OctoberGeneratorEntryPoint.ReadWorkerHttpObservationOptions(configuration, command, capture));
            Assert.DoesNotContain("SYNTHETIC", error.Message);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("defined")]
    [InlineData("invalid-counts")]
    [InlineData("undefined")]
    [InlineData("completed")]
    [InlineData("wrong-stage")]
    [InlineData("absent")]
    [InlineData("foreign-binding")]
    public async Task HttpObservationProjection_EmitsOnlyFiniteFactsForAuthorizedParseFailureAsync(string condition)
    {
        bool undefined = condition == "undefined";
        var jsonState = new OctoberWorkerJsonObservation(undefined ? (OctoberWorkerJsonObservationKind)99999 : OctoberWorkerJsonObservationKind.MissingClosingDelimiters, 1, 1, true);
        var facts = new OctoberWorkerHttpObservationSnapshot(
            undefined ? (OctoberWorkerHttpObservationStatus)99999 : OctoberWorkerHttpObservationStatus.Complete,
            undefined ? (OctoberWorkerTokenCapKey)99999 : OctoberWorkerTokenCapKey.MaxCompletionTokens, 137,
            undefined ? (OctoberWorkerObservedFinish)99999 : OctoberWorkerObservedFinish.Stop, OctoberWorkerObservedFinish.Stop,
            true, false, false, null, 1024, 100, 100, 100, true, true, jsonState, jsonState, jsonState, 17, 17);
        if (condition == "invalid-counts") facts = facts with
        {
            CapValue = long.MaxValue, WireBytes = long.MaxValue, HttpTextUtf8Bytes = -1,
            SdkTextUtf8Bytes = int.MaxValue, ParseInputUtf8Bytes = -1,
            HttpReasoningTokens = -1, SdkReasoningTokens = long.MaxValue,
            HttpJson = new(OctoberWorkerJsonObservationKind.MissingClosingDelimiters, -1, int.MaxValue, true)
        };
        var worker = new OctoberWorkerDiagnosticSnapshot(condition == "completed" ? OctoberFileWorkerStatus.Completed : OctoberFileWorkerStatus.InvalidOutput,
            OctoberWorkerRetryDisposition.None, condition == "wrong-stage" ? OctoberWorkerFailureStage.ProposalValidation : OctoberWorkerFailureStage.ProposalParse)
        { HttpObservation = condition == "absent" ? null : facts };
        var batch = Guid.NewGuid();
        var report = new OctoberScanReport("cl-only", null, OctoberCoverageStatus.Partial, OctoberExecutionStatus.Failed,
            OctoberPublicationStatus.Failed, null, [], [], [], new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), new(null, null, null, true), []) { WorkerDiagnostics = worker };
        if (condition == "foreign-binding") report = report with { Publication = new(new("SYNTHETIC_DATABASE", "foreign", batch, new string('a', 64)), Guid.NewGuid()) };
        using var output = new StringWriter(); using var error = new StringWriter();
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(new(OctoberGeneratorMode.Generate, "target", batch, null, new() { OutputStage = "cl-only" }),
            new Module(new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report)), output, error);
        Assert.NotEqual(0, exit); Assert.DoesNotContain("SYNTHETIC", output.ToString()); Assert.DoesNotContain("99999", output.ToString());
        using var serialized = JsonDocument.Parse(output.ToString());
        bool present = serialized.RootElement.TryGetProperty("workerDiagnostics", out var diagnostic) && diagnostic.ValueKind != JsonValueKind.Null;
        if (condition == "foreign-binding") { Assert.False(present); return; }
        Assert.True(present); var projected = diagnostic.GetProperty("httpObservation");
        if (condition is "completed" or "wrong-stage" or "absent") { Assert.Equal(JsonValueKind.Null, projected.ValueKind); return; }
        string[] names = ["status", "capKey", "capValue", "httpFinish", "sdkFinish", "doneSeen", "eofSeen", "contentAfterFinish", "contentAfterDone", "wireBytes", "httpTextUtf8Bytes", "sdkTextUtf8Bytes", "parseInputUtf8Bytes", "httpEqualsSdk", "sdkEqualsParse", "httpJson", "sdkJson", "parseJson", "httpReasoningTokens", "sdkReasoningTokens"];
        Assert.Equal(names.Order(StringComparer.Ordinal), projected.EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal));
        if (condition == "invalid-counts")
        {
            foreach (string name in new[] { "capValue", "wireBytes", "httpTextUtf8Bytes", "sdkTextUtf8Bytes", "parseInputUtf8Bytes", "httpReasoningTokens", "sdkReasoningTokens" })
                Assert.Equal(JsonValueKind.Null, projected.GetProperty(name).ValueKind);
            Assert.Equal(JsonValueKind.Null, projected.GetProperty("httpJson").GetProperty("openObjects").ValueKind);
            Assert.Equal(JsonValueKind.Null, projected.GetProperty("httpJson").GetProperty("openArrays").ValueKind);
            return;
        }
        if (undefined)
        {
            Assert.Equal(JsonValueKind.Null, projected.GetProperty("status").ValueKind);
            Assert.Equal(JsonValueKind.Null, projected.GetProperty("capKey").ValueKind);
            Assert.Equal(JsonValueKind.Null, projected.GetProperty("httpFinish").ValueKind);
            Assert.Equal(JsonValueKind.Null, projected.GetProperty("httpJson").GetProperty("kind").ValueKind);
        }
        else
        {
            Assert.Equal("Complete", projected.GetProperty("status").GetString()); Assert.Equal(137, projected.GetProperty("capValue").GetInt64());
            Assert.Equal(true, projected.GetProperty("httpEqualsSdk").GetBoolean()); Assert.Equal(17, projected.GetProperty("httpReasoningTokens").GetInt64());
            Assert.Equal(new[] { "atEof", "kind", "openArrays", "openObjects" }, projected.GetProperty("parseJson").EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal));
        }
    }
    private sealed class Module(OctoberGeneratorExecutionResult result) : IOctoberGeneratorModule
    {
        internal int Calls;
        public Task<OctoberGeneratorExecutionResult> ExecuteAsync(OctoberGeneratorCommand command, CancellationToken cancellationToken = default) { Calls++; return Task.FromResult(result); }
    }
    private sealed class NoSecrets : ISecretEncryptionService
    {
        internal int Accesses;
        public Task<string> EncryptAsync(string purpose, string plain, CancellationToken cancellationToken = default) { Accesses++; throw new InvalidOperationException("Unexpected secret access"); }
        public Task<string> DecryptAsync(string purpose, string cipher, CancellationToken cancellationToken = default) { Accesses++; throw new InvalidOperationException("Unexpected secret access"); }
    }
}
