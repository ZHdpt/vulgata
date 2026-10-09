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
using Vulgata.OctoberGenerator;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

public sealed class OctoberScanEntryPointTests
{
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
        Assert.Equal(new[] { "boundary", "finalizerPersistenceStatus", "finalizerStatus", "lookupPersistenceStatus", "lookupStatus", "operationMismatch", "originalSynthesisFailureReason" }, diagnostic.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
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
    [InlineData("undefined")]
    [InlineData("absent")]
    [InlineData("foreign-binding")]
    public async Task WorkerDiagnostics_ProjectOnlyDefinedScalarsWithoutPayloadAsync(string condition)
    {
        var snapshot = condition == "absent" ? null : new OctoberWorkerDiagnosticSnapshot(
            OctoberFileWorkerStatus.InvalidOutput, OctoberWorkerRetryDisposition.None, OctoberWorkerFailureStage.ProposalParse);
        if (condition == "undefined") snapshot = new((OctoberFileWorkerStatus)99999, (OctoberWorkerRetryDisposition)99999, (OctoberWorkerFailureStage)99999);
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
        Assert.Equal(new[] { "failureStage", "retryDisposition", "status" }, diagnostic.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        if (condition == "undefined") { Assert.All(diagnostic.EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind)); return; }
        Assert.Equal("InvalidOutput", diagnostic.GetProperty("status").GetString());
        Assert.Equal("None", diagnostic.GetProperty("retryDisposition").GetString());
        Assert.Equal("ProposalParse", diagnostic.GetProperty("failureStage").GetString());
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
