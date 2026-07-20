using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CodeGraph;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.FrameworkDetection;
using Vulgata.Shared.Repositories;
using Vulgata.Web.Data;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Tests;

public sealed class RepositoryProfilingTests
{
    [Fact]
    public void ScanRun_CompleteWithFiveHundredCandidatesAndNoUnits_RequiresHumanReview()
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        ScanRun run = ScanRun.Start(Guid.NewGuid(), 1_800, started);

        run.SetCandidateCount(500);
        run.Complete(0, 0, started.AddSeconds(3));

        Assert.Equal(ScanRunStatus.Completed, run.Status);
        Assert.True(run.NeedsHumanReview);
        Assert.Equal(3_000, run.DurationMilliseconds);
    }

    [Fact]
    public void CodeUnit_WithContentHashes_StartsPendingAndUsesApprovedVocabulary()
    {
        string hash = new('a', 64);
        var unit = new CodeUnit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "src/OrderService.cs",
            hash,
            hash,
            2,
            8,
            "PlaceOrder",
            CodeUnitType.Method,
            RangeConfidence.Exact,
            null);

        Assert.Equal(CodeUnitStatus.Pending, unit.Status);
        Assert.Equal(RangeConfidence.Exact, unit.RangeConfidence);
        Assert.DoesNotContain("High", Enum.GetNames<RangeConfidence>());
    }

    [Fact]
    public void Model_CodeGraphRecords_EnforcesRepositoryAndScanRunIdentity()
    {
        var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var db = new VulgataDbContext(dbOptions);

        var codeUnit = db.Model.FindEntityType(typeof(CodeUnit));
        Assert.NotNull(codeUnit);
        Assert.Contains(
            codeUnit!.GetForeignKeys(),
            foreignKey =>
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual(["ScanRunId", "RepositoryId"]) &&
                foreignKey.PrincipalKey.Properties.Select(property => property.Name)
                    .SequenceEqual(["Id", "RepositoryId"]));

        var edge = db.Model.FindEntityType(typeof(CodeUnitEdge));
        Assert.NotNull(edge);
        Assert.Contains(
            edge!.GetForeignKeys(),
            foreignKey =>
                foreignKey.DependentToPrincipal?.Name == nameof(CodeUnitEdge.SourceCodeUnit) &&
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual(["SourceCodeUnitId", "ScanRunId", "RepositoryId"]));
        Assert.Contains(
            edge.GetForeignKeys(),
            foreignKey =>
                foreignKey.DependentToPrincipal?.Name == nameof(CodeUnitEdge.TargetCodeUnit) &&
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual(["TargetCodeUnitId", "ScanRunId", "RepositoryId"]));

        var unresolved = db.Model.FindEntityType(typeof(UnresolvedStructuralRelationshipCandidate));
        Assert.NotNull(unresolved);
        Assert.Contains(
            unresolved!.GetForeignKeys(),
            foreignKey =>
                foreignKey.DependentToPrincipal?.Name ==
                    nameof(UnresolvedStructuralRelationshipCandidate.SourceCodeUnit) &&
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual(["SourceCodeUnitId", "ScanRunId", "RepositoryId"]));
    }

    [Fact]
    public void Model_LanguageFrameworkDetection_EnforcesAggregateConstraints()
    {
        Type? frameworkType = typeof(CodeUnit).Assembly.GetType(
            "Vulgata.Core.Entities.CodeUnitFramework");
        Type? summaryType = typeof(CodeUnit).Assembly.GetType(
            "Vulgata.Core.Entities.ScanRunLanguageSummary");

        Assert.NotNull(frameworkType);
        Assert.NotNull(summaryType);

        var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var db = new VulgataDbContext(dbOptions);

        var framework = db.Model.FindEntityType(frameworkType!);
        Assert.NotNull(framework);
        Assert.Contains(
            framework!.GetForeignKeys(),
            foreignKey =>
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual(["CodeUnitId", "ScanRunId", "RepositoryId"]) &&
                foreignKey.PrincipalKey.Properties.Select(property => property.Name)
                    .SequenceEqual(["Id", "ScanRunId", "RepositoryId"]));
        Assert.Contains(
            framework.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(["CodeUnitId", "FrameworkName"]));

        var summary = db.Model.FindEntityType(summaryType!);
        Assert.NotNull(summary);
        Assert.Equal(
            ["ScanRunId", "Language"],
            summary!.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Contains(
            summary.GetForeignKeys(),
            foreignKey =>
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual(["ScanRunId", "RepositoryId"]) &&
                foreignKey.PrincipalKey.Properties.Select(property => property.Name)
                    .SequenceEqual(["Id", "RepositoryId"]));

        Assert.True(db.Model.FindEntityType(typeof(CodeUnit))!
            .FindProperty(nameof(CodeUnit.Language))!.IsNullable);
    }

    [Fact]
    public async Task ScanRun_FrameworkEvidenceAuditTotalOutcomeCount_PersistsCompleteCountBeyondRetainedDetailsAsync()
    {
        PropertyInfo? property = typeof(ScanRun).GetProperty(
            "FrameworkEvidenceAuditTotalOutcomeCount");
        MethodInfo? setter = typeof(ScanRun).GetMethod(
            "SetFrameworkEvidenceAuditTotalOutcomeCount",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        Assert.NotNull(setter);

        var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new VulgataDbContext(dbOptions);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        RepositoryEntity repository = RepositoryEntity.CreateStandalone(
            "audit-count",
            "https://example.invalid/audit-count.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        ScanRun run = ScanRun.Start(repository.Id, 1_800, DateTimeOffset.UtcNow);
        setter!.Invoke(run, [101]);
        db.Repositories.Add(repository);
        db.ScanRuns.Add(run);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        ScanRun reloaded = await db.ScanRuns.SingleAsync();

        Assert.Equal(101, property!.GetValue(reloaded));
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => setter.Invoke(reloaded, [-1]));
        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public async Task RecoverFailedFinalCommitAsync_WithAdversarialDiagnostic_PersistsClosedChineseFailureAsync()
    {
        const string rawDiagnostic =
            "MATCH (n) RETURN n; source=class Secret {}; C:\\host\\repo\\Secret.cs; " +
            "package=attacker-value; password=hunter2; token=sk-live-secret; elapsed=9876ms";
        const string safeMessage = "代码结构分析失败，请稍后重试。";
        var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new VulgataDbContext(dbOptions);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        RepositoryEntity repository = RepositoryEntity.CreateStandalone(
            "recovery-disclosure",
            "https://example.invalid/recovery-disclosure.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        ScanRun run = ScanRun.Start(repository.Id, 1_800, DateTimeOffset.UtcNow);
        db.Repositories.Add(repository);
        db.ScanRuns.Add(run);
        await db.SaveChangesAsync();
        var scanRunRepository = new ScanRunRepository(db);

        await scanRunRepository.RecoverFailedFinalCommitAsync(
            repository.Id,
            run.Id,
            rawDiagnostic,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        db.ChangeTracker.Clear();
        ScanRun persisted = await db.ScanRuns.SingleAsync();
        Assert.Equal(ScanRunStatus.Failed, persisted.Status);
        Assert.Equal(safeMessage, persisted.TerminalMessage);
        Assert.InRange(persisted.TerminalMessage!.Length, 1, 64);
        Assert.DoesNotContain("MATCH", persisted.TerminalMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", persisted.TerminalMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", persisted.TerminalMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attacker-value", persisted.TerminalMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", persisted.TerminalMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-live-secret", persisted.TerminalMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("9876ms", persisted.TerminalMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartStandaloneRepository_WithAdversarialGitFailure_ReturnsAndPersistsClosedChineseFailureAsync()
    {
        const string rawDiagnostic =
            "fatal: query=MATCH (n) RETURN n; source=class Secret {}; " +
            "C:\\host\\repo\\Secret.cs; package=attacker-value; password=hunter2; " +
            "token=sk-live-secret; elapsed=9876ms";
        const string safeMessage = "仓库克隆失败，请检查仓库地址和访问权限。";
        var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new VulgataDbContext(dbOptions);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        RepositoryEntity repository = RepositoryEntity.CreateStandalone(
            "git-disclosure",
            "https://example.invalid/git-disclosure.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        db.Repositories.Add(repository);
        await db.SaveChangesAsync();
        var coordinator = new RepositoryProfilingCoordinator(
            new SystemRepository(db),
            new RepositoryRepository(db),
            new ScanRunRepository(db),
            new FailingWorkingCopy(rawDiagnostic),
            new FixedCandidates([]),
            new CountingFailureProvider(),
            new CodeGraphOptions(Path.Combine(Path.GetTempPath(), "unused-state"), 1_800),
            NullLogger<RepositoryProfilingCoordinator>.Instance,
            new EmptyFrameworkEvidenceDetector(),
            FrameworkCatalog.LoadEmbedded());

        RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
            repository.Id,
            null!,
            CancellationToken.None);

        Assert.Equal(RepositoryProfilingOutcome.GitFailure, result.Outcome);
        Assert.Equal(safeMessage, result.Message);
        ScanRunSummaryDto summary = Assert.IsType<ScanRunSummaryDto>(result.Value);
        Assert.Equal(safeMessage, summary.Message);
        db.ChangeTracker.Clear();
        ScanRun persisted = await db.ScanRuns.SingleAsync();
        Assert.Equal(ScanRunStatus.Failed, persisted.Status);
        Assert.Equal(safeMessage, persisted.TerminalMessage);
        string inspected = string.Join(
            '\n',
            result.Message,
            summary.Message,
            persisted.TerminalMessage);
        Assert.InRange(inspected.Length, 1, 192);
        Assert.DoesNotContain("MATCH", inspected, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", inspected, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", inspected, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attacker-value", inspected, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", inspected, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-live-secret", inspected, StringComparison.Ordinal);
        Assert.DoesNotContain("9876ms", inspected, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartStandaloneRepository_WithFrameworkEvidence_PersistsScopedRankedLanguageFactsInOnePassAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-framework-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "app.ts"), "export const app = 1;");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "server.py"), "def server(): pass");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Other.cs"), "class Other { }");
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "frameworks",
                "https://example.invalid/frameworks.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var provider = new FixedLanguageProvider(
            [
                new("src/app.ts", "App", CodeUnitType.Function, 1, 1, "app", RangeConfidence.Exact, "typescript"),
                new("src/server.py", "server", CodeUnitType.Function, 1, 1, "server", RangeConfidence.Exact, "python"),
                new("src/Other.cs", "Other", CodeUnitType.Class, 1, 1, "other", RangeConfidence.Exact, "c_sharp"),
            ]);
            var candidates = new FixedCandidates(
            [
                new("src/app.ts", false, "Eligible", 0),
                new("src/server.py", false, "Eligible", 0),
                new("src/Other.cs", false, "Eligible", 0),
            ]);
            var detector = new CountingFrameworkDetector();
            RepositoryProfilingCoordinator coordinator = CreateCoordinatorWithDetector(
                db,
                root,
                provider,
                candidates,
                detector);

            RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
                repository.Id,
                null!,
                CancellationToken.None);

            Assert.Equal(RepositoryProfilingOutcome.Success, result.Outcome);
            Assert.Equal(1, detector.DetectionCount);
            Assert.Equal(3, detector.ApplicabilityCount);
            CodeUnitFramework[] facts = await db.CodeUnitFrameworks
                .OrderBy(fact => fact.FrameworkName)
                .ToArrayAsync();
            Assert.Equal(3, facts.Length);
            Assert.Equal(["django", "express", "react"], facts.Select(fact => fact.FrameworkName));
            Assert.All(facts, fact => Assert.Equal(FrameworkConfidence.High, fact.Confidence));
            Assert.DoesNotContain(facts, fact => fact.DetectionSource.Contains("lock", StringComparison.Ordinal));
            CodeUnit other = await db.CodeUnits.SingleAsync(unit => unit.FilePath == "src/Other.cs");
            Assert.DoesNotContain(facts, fact => fact.CodeUnitId == other.Id);
            ScanRun run = await db.ScanRuns.SingleAsync();
            Assert.Equal(
                101,
                typeof(ScanRun).GetProperty("FrameworkEvidenceAuditTotalOutcomeCount")!
                    .GetValue(run));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartStandaloneRepository_WhenFrameworkDetectorFails_PersistsNoStructuralDraftAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-framework-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "A.cs"), "class A { }");
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "framework-failure",
                "https://example.invalid/framework-failure.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var detector = new FailingFrameworkDetector();
            RepositoryProfilingCoordinator coordinator = CreateCoordinatorWithDetector(
                db,
                root,
                new SingleUnitProvider(),
                new FixedCandidates(
                [
                    new("src/A.cs", false, "Eligible", 0),
                    new("src/Generated.g.cs", true, "Generated", 1),
                ]),
                detector);

            RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
                repository.Id,
                null!,
                CancellationToken.None);

            Assert.NotEqual(RepositoryProfilingOutcome.Success, result.Outcome);
            Assert.Equal(1, detector.DetectionCount);
            db.ChangeTracker.Clear();
            Assert.Empty(await db.CodeUnits.ToListAsync());
            Assert.Empty(await db.CodeUnitEdges.ToListAsync());
            Assert.Empty(await db.UnresolvedStructuralRelationshipCandidates.ToListAsync());
            Assert.Empty(await db.CodeUnitFrameworks.ToListAsync());
            Assert.Empty(await db.ScanRunLanguageSummaries.ToListAsync());
            ScanCandidateOutcome local = await db.ScanCandidateOutcomes.SingleAsync();
            Assert.Equal("src/Generated.g.cs", local.FilePath);
            ScanRun run = await db.ScanRuns.SingleAsync();
            Assert.Equal(ScanRunStatus.Failed, run.Status);
            Assert.Equal("框架证据检测失败。", run.TerminalMessage);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(AdversarialGlobalFailure.MalformedProviderLanguage)]
    [InlineData(AdversarialGlobalFailure.WrongTypedProviderLanguage)]
    [InlineData(AdversarialGlobalFailure.TruncatedProviderResponse)]
    [InlineData(AdversarialGlobalFailure.ProviderProcessFailure)]
    [InlineData(AdversarialGlobalFailure.ProviderQueryFailure)]
    [InlineData(AdversarialGlobalFailure.ProviderCleanupFailure)]
    [InlineData(AdversarialGlobalFailure.DetectorRepositoryBoundExceeded)]
    [InlineData(AdversarialGlobalFailure.DetectorFault)]
    public async Task StartStandaloneRepository_WithAdversarialGlobalFailure_RetainsOnlyLocalAuditAndTerminalizesSafelyAsync(
        AdversarialGlobalFailure failure)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"vulgata-global-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(
            Path.Combine(root, "src", "A.cs"),
            "class A { void Run() { } }");
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "adversarial-global-failure",
                "https://example.invalid/adversarial-global-failure.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var detector = new AdversarialGlobalFailureDetector(failure);
            RepositoryProfilingCoordinator coordinator = CreateCoordinatorWithDetector(
                db,
                root,
                new AdversarialGlobalFailureProvider(failure),
                new FixedCandidates(
                [
                    new("src/A.cs", false, "Eligible", 0),
                    new("src/Generated.g.cs", true, "Generated", 1),
                ]),
                detector);

            RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
                repository.Id,
                null!,
                CancellationToken.None);

            string expectedMessage = failure is
                AdversarialGlobalFailure.DetectorRepositoryBoundExceeded or
                AdversarialGlobalFailure.DetectorFault
                    ? "框架证据检测失败。"
                    : "代码结构分析失败，请稍后重试。";
            Assert.Equal(RepositoryProfilingOutcome.ProviderFailure, result.Outcome);
            Assert.Equal(expectedMessage, result.Message);
            ScanRunSummaryDto value = Assert.IsType<ScanRunSummaryDto>(result.Value);
            Assert.Equal("失败", value.Status);
            Assert.Equal(expectedMessage, value.Message);
            Assert.Equal(
                failure is
                    AdversarialGlobalFailure.DetectorRepositoryBoundExceeded or
                    AdversarialGlobalFailure.DetectorFault
                        ? 1
                        : 0,
                detector.DetectionCount);
            Assert.Equal(0, detector.ApplicabilityCount);

            db.ChangeTracker.Clear();
            Assert.Empty(await db.CodeUnits.ToListAsync());
            Assert.Empty(await db.CodeUnitEdges.ToListAsync());
            Assert.Empty(await db.UnresolvedStructuralRelationshipCandidates.ToListAsync());
            Assert.Empty(await db.CodeUnitFrameworks.ToListAsync());
            Assert.Empty(await db.ScanRunLanguageSummaries.ToListAsync());
            ScanCandidateOutcome localAudit = await db.ScanCandidateOutcomes.SingleAsync();
            Assert.Equal("src/Generated.g.cs", localAudit.FilePath);
            Assert.Equal(ScanCandidateOutcomeKind.Filtered, localAudit.Outcome);
            Assert.Equal("Generated", localAudit.ReasonOrCategory);
            Assert.NotEqual(
                ScanCandidateOutcomeReasons.UnknownLanguage,
                localAudit.ReasonOrCategory);
            Assert.DoesNotContain(
                "FrameworkEvidence",
                localAudit.ReasonOrCategory,
                StringComparison.Ordinal);
            ScanRun run = await db.ScanRuns.SingleAsync();
            Assert.Equal(ScanRunStatus.Failed, run.Status);
            Assert.Equal(expectedMessage, run.TerminalMessage);
            Assert.Equal(1, run.TotalCandidateFileCount);
            Assert.Equal(
                0,
                typeof(ScanRun).GetProperty("FrameworkEvidenceAuditTotalOutcomeCount")!
                    .GetValue(run));
            string durableAndResultText = string.Join(
                '\n',
                result.Message,
                value.Message,
                run.TerminalMessage,
                $"{localAudit.FilePath}|{localAudit.Outcome}|{localAudit.ReasonOrCategory}");
            Assert.InRange(durableAndResultText.Length, 1, 256);
            foreach (string forbidden in new[]
                     {
                         "MATCH",
                         "class Secret",
                         "C:\\secrets\\source.cs",
                         "attacker-value",
                         "hunter2",
                         "do-not-persist",
                         "9876ms",
                         "RAW-DIAGNOSTIC",
                     })
            {
                Assert.DoesNotContain(forbidden, durableAndResultText, StringComparison.Ordinal);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(InvalidFrameworkEvidence.Malformed)]
    [InlineData(InvalidFrameworkEvidence.Unsupported)]
    [InlineData(InvalidFrameworkEvidence.Ambiguous)]
    [InlineData(InvalidFrameworkEvidence.Escape)]
    [InlineData(InvalidFrameworkEvidence.Reparse)]
    [InlineData(InvalidFrameworkEvidence.Oversize)]
    public async Task StartStandaloneRepository_WithInvalidIndividualFrameworkEvidence_IsolatesClosedAuditAndRetainsValidFactAsync(
        InvalidFrameworkEvidence invalidEvidence)
    {
        using var fixture = new ProfilingEvidenceGitFixture();
        const string forbiddenSource =
            "RAW-SOURCE-CONTENT ABSOLUTE-HOST-SECRET ARBITRARY-PACKAGE-VALUE " +
            "RAW-PARSER-DIAGNOSTIC credential=USER:PASSWORD TOKEN-DO-NOT-PERSIST";
        fixture.Write(
            "package.json",
            $$"""{ "dependencies": { "react": "{{forbiddenSource}}" } }""");
        fixture.Write("src/App.ts", "export class App { }");
        fixture.Write("ambiguous/App.cs", "class App { }");

        string[] expectedAuditPaths;
        ScanCandidateOutcomeKind expectedAuditKind;
        string expectedAuditReason;
        if (invalidEvidence == InvalidFrameworkEvidence.Malformed)
        {
            fixture.Write(
                "invalid-malformed/package.json",
                $$"""{ "dependencies": { "express": "{{forbiddenSource}}" """);
            expectedAuditPaths = ["invalid-malformed/package.json"];
            expectedAuditKind = ScanCandidateOutcomeKind.Errored;
            expectedAuditReason = ScanCandidateOutcomeReasons.FrameworkEvidenceMalformed;
        }
        else if (invalidEvidence == InvalidFrameworkEvidence.Unsupported)
        {
            fixture.Write(
                "invalid-unsupported/requirements.txt",
                $"django @ https://user:TOKEN-DO-NOT-PERSIST@example.invalid/{forbiddenSource}.whl");
            expectedAuditPaths = ["invalid-unsupported/requirements.txt"];
            expectedAuditKind = ScanCandidateOutcomeKind.Skipped;
            expectedAuditReason = ScanCandidateOutcomeReasons.FrameworkEvidenceUnsupported;
        }
        else if (invalidEvidence == InvalidFrameworkEvidence.Ambiguous)
        {
            fixture.Write(
                "Api.csproj",
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Mvc" Version="SECRET-VERSION" />
                  </ItemGroup>
                </Project>
                """);
            fixture.Write(
                "Client.csproj",
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly"
                                      Version="TOKEN-DO-NOT-PERSIST" />
                  </ItemGroup>
                </Project>
                """);
            expectedAuditPaths = ["Api.csproj", "Client.csproj"];
            expectedAuditKind = ScanCandidateOutcomeKind.Skipped;
            expectedAuditReason = ScanCandidateOutcomeReasons.FrameworkEvidenceAmbiguousScope;
        }
        else if (invalidEvidence == InvalidFrameworkEvidence.Escape)
        {
            fixture.CreateFileSymbolicLink(
                "invalid-escape/package.json",
                "outside/package.json",
                $$"""{ "dependencies": { "express": "{{forbiddenSource}}" } }""",
                targetOutsideRepository: true);
            expectedAuditPaths = ["invalid-escape/package.json"];
            expectedAuditKind = ScanCandidateOutcomeKind.Errored;
            expectedAuditReason = ScanCandidateOutcomeReasons.FrameworkEvidenceUnsafePath;
        }
        else if (invalidEvidence == InvalidFrameworkEvidence.Reparse)
        {
            fixture.CreateFileSymbolicLink(
                "invalid-reparse/package.json",
                "inside/package.json",
                $$"""{ "dependencies": { "express": "{{forbiddenSource}}" } }""",
                targetOutsideRepository: false);
            expectedAuditPaths = ["invalid-reparse/package.json"];
            expectedAuditKind = ScanCandidateOutcomeKind.Errored;
            expectedAuditReason = ScanCandidateOutcomeReasons.FrameworkEvidenceReparsePoint;
        }
        else
        {
            fixture.Write(
                "invalid-oversize/package.json",
                $$"""{ "dependencies": { "express": "{{forbiddenSource}}" } }""" +
                new string(' ', (8 * 1024 * 1024) + 1));
            expectedAuditPaths = ["invalid-oversize/package.json"];
            expectedAuditKind = ScanCandidateOutcomeKind.Errored;
            expectedAuditReason = ScanCandidateOutcomeReasons.FrameworkEvidenceBoundExceeded;
        }

        if (invalidEvidence == InvalidFrameworkEvidence.Reparse)
        {
            fixture.Track(
                "package.json",
                "src/App.ts",
                "ambiguous/App.cs",
                "invalid-reparse/package.json");
        }
        else
        {
            fixture.TrackAll();
        }

        var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new VulgataDbContext(dbOptions);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        RepositoryEntity repository = RepositoryEntity.CreateStandalone(
            "individual-framework-evidence",
            "https://example.invalid/individual-framework-evidence.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        db.Repositories.Add(repository);
        await db.SaveChangesAsync();
        var provider = new FixedLanguageProvider(
        [
            new(
                "src/App.ts",
                "App",
                CodeUnitType.Class,
                1,
                1,
                "app",
                RangeConfidence.Exact,
                "typescript"),
            new(
                "ambiguous/App.cs",
                "App",
                CodeUnitType.Class,
                1,
                1,
                "ambiguous-app",
                RangeConfidence.Exact,
                "c_sharp"),
        ]);
        RepositoryProfilingCoordinator coordinator = CreateCoordinatorWithDetector(
            db,
            fixture.Path,
            provider,
            new FixedCandidates(
            [
                new("src/App.ts", false, "Eligible", 0),
                new("ambiguous/App.cs", false, "Eligible", 1),
                new("src/Generated.g.cs", true, "Generated", 2),
            ]),
            new FrameworkEvidenceDetector(FrameworkCatalog.LoadEmbedded()));

        RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
            repository.Id,
            null!,
            CancellationToken.None);

        Assert.Equal(RepositoryProfilingOutcome.Success, result.Outcome);
        db.ChangeTracker.Clear();
        Assert.Equal(2, await db.CodeUnits.CountAsync());
        Assert.Empty(await db.CodeUnitEdges.ToListAsync());
        Assert.Empty(await db.UnresolvedStructuralRelationshipCandidates.ToListAsync());
        CodeUnitFramework validFact = await db.CodeUnitFrameworks.SingleAsync();
        Assert.Equal("react", validFact.FrameworkName);
        Assert.Equal(FrameworkConfidence.High, validFact.Confidence);
        Assert.Equal(
            "package.json#json:/dependencies/react",
            validFact.DetectionSource);
        CodeUnit validUnit = await db.CodeUnits.SingleAsync(
            unit => unit.Id == validFact.CodeUnitId);
        Assert.Equal("src/App.ts", validUnit.FilePath);
        Assert.DoesNotContain(
            await db.CodeUnitFrameworks.ToListAsync(),
            fact => fact.FrameworkName == "aspnet-core");
        Assert.Equal(2, await db.ScanRunLanguageSummaries.CountAsync());

        ScanCandidateOutcome[] frameworkAudits =
        [
            .. await db.ScanCandidateOutcomes
                .Where(outcome => outcome.ReasonOrCategory.StartsWith(
                    "FrameworkEvidence"))
                .OrderBy(outcome => outcome.FilePath)
                .ToListAsync(),
        ];
        Assert.Equal(expectedAuditPaths.Length, frameworkAudits.Length);
        Assert.Equal(
            expectedAuditPaths,
            frameworkAudits.Select(outcome => outcome.FilePath));
        Assert.All(
            frameworkAudits,
            outcome =>
            {
                Assert.Equal(expectedAuditKind, outcome.Outcome);
                Assert.Equal(expectedAuditReason, outcome.ReasonOrCategory);
                Assert.True(outcome.FilePath is not "(unknown)");
                Assert.False(Path.IsPathFullyQualified(outcome.FilePath));
            });
        ScanCandidateOutcome localAudit = await db.ScanCandidateOutcomes.SingleAsync(
            outcome => outcome.ReasonOrCategory == "Generated");
        Assert.Equal("src/Generated.g.cs", localAudit.FilePath);
        ScanRun run = await db.ScanRuns.SingleAsync();
        Assert.Equal(ScanRunStatus.Completed, run.Status);
        Assert.Equal(
            expectedAuditPaths.Length,
            typeof(ScanRun).GetProperty("FrameworkEvidenceAuditTotalOutcomeCount")!
                .GetValue(run));

        string durableAndResultText = string.Join(
            '\n',
            frameworkAudits.Select(outcome =>
                    $"{outcome.FilePath}|{outcome.Outcome}|{outcome.ReasonOrCategory}")
                .Concat(
                    await db.CodeUnitFrameworks
                        .Select(fact =>
                            $"{fact.FrameworkName}|{fact.Confidence}|{fact.DetectionSource}")
                        .ToListAsync())
                .Append(run.TerminalMessage ?? string.Empty)
                .Append(JsonSerializer.Serialize(result)));
        Assert.DoesNotContain(
            fixture.Path,
            durableAndResultText,
            StringComparison.OrdinalIgnoreCase);
        foreach (string forbidden in forbiddenSource.Split(' '))
        {
            Assert.DoesNotContain(forbidden, durableAndResultText, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("SECRET-VERSION", durableAndResultText, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonException", durableAndResultText, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidDataException", durableAndResultText, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", durableAndResultText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartStandaloneRepository_WithSaturatedAuditAndRepeatedAmbiguity_CountsUniqueCompleteOutcomesOnceAsync()
    {
        using var fixture = new ProfilingEvidenceGitFixture();
        fixture.Write(
            "package.json",
            """
            { "dependencies": { "react": "VALID-VALUE" } }
            """);
        fixture.Write(
            "Api.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Mvc" Version="SECRET-VERSION" />
              </ItemGroup>
            </Project>
            """);
        fixture.Write(
            "Client.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly"
                                  Version="TOKEN-DO-NOT-PERSIST" />
              </ItemGroup>
            </Project>
            """);
        fixture.Write("src/A.cs", "class A { }");
        fixture.Write("src/B.cs", "class B { }");
        fixture.Write("src/App.ts", "export class App { }");
        for (var index = 0; index < 100; index++)
        {
            fixture.Write(
                $"audit/{index:D3}/requirements.txt",
                $"django @ https://example.invalid/UNSUPPORTED-{index:D3}");
        }

        fixture.TrackAll();

        var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new VulgataDbContext(dbOptions);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        RepositoryEntity repository = RepositoryEntity.CreateStandalone(
            "saturated-framework-audit",
            "https://example.invalid/saturated-framework-audit.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        db.Repositories.Add(repository);
        await db.SaveChangesAsync();
        var provider = new FixedLanguageProvider(
        [
            new(
                "src/A.cs",
                "A",
                CodeUnitType.Class,
                1,
                1,
                "a",
                RangeConfidence.Exact,
                "c_sharp"),
            new(
                "src/B.cs",
                "B",
                CodeUnitType.Class,
                1,
                1,
                "b",
                RangeConfidence.Exact,
                "c_sharp"),
            new(
                "src/App.ts",
                "App",
                CodeUnitType.Class,
                1,
                1,
                "app",
                RangeConfidence.Exact,
                "typescript"),
        ]);
        var detector = new CountingDelegatingFrameworkDetector(
            new FrameworkEvidenceDetector(FrameworkCatalog.LoadEmbedded()));
        RepositoryProfilingCoordinator coordinator = CreateCoordinatorWithDetector(
            db,
            fixture.Path,
            provider,
            new FixedCandidates(
            [
                new("src/A.cs", false, "Eligible", 0),
                new("src/B.cs", false, "Eligible", 1),
                new("src/App.ts", false, "Eligible", 2),
            ]),
            detector);

        RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
            repository.Id,
            null!,
            CancellationToken.None);

        Assert.Equal(RepositoryProfilingOutcome.Success, result.Outcome);
        Assert.Equal(1, detector.DetectionCount);
        db.ChangeTracker.Clear();
        ScanRun run = await db.ScanRuns.SingleAsync();
        Assert.Equal(
            102,
            typeof(ScanRun).GetProperty("FrameworkEvidenceAuditTotalOutcomeCount")!
                .GetValue(run));
        ScanCandidateOutcome[] retainedFrameworkAudits =
        [
            .. await db.ScanCandidateOutcomes
                .Where(outcome => outcome.ReasonOrCategory.StartsWith(
                    "FrameworkEvidence"))
                .ToListAsync(),
        ];
        Assert.Equal(100, retainedFrameworkAudits.Length);
        Assert.All(
            retainedFrameworkAudits,
            outcome => Assert.Equal(
                ScanCandidateOutcomeReasons.FrameworkEvidenceUnsupported,
                outcome.ReasonOrCategory));
        Assert.DoesNotContain(
            retainedFrameworkAudits,
            outcome =>
                outcome.ReasonOrCategory ==
                ScanCandidateOutcomeReasons.FrameworkEvidenceAmbiguousScope);
        CodeUnitFramework fact = await db.CodeUnitFrameworks.SingleAsync();
        Assert.Equal("react", fact.FrameworkName);
        Assert.DoesNotContain(
            await db.CodeUnitFrameworks.ToListAsync(),
            candidate => candidate.FrameworkName == "aspnet-core");
    }

    [Fact]
    public void RepositoryProfileDraft_WithMissingLanguageSummary_RejectsBeforePersistence()
    {
        Type? draftType = typeof(ICodeGraphProvider).Assembly.GetType(
            "Vulgata.Core.DomainServices.RepositoryProfileDraft");
        Assert.NotNull(draftType);

        Guid repositoryId = Guid.NewGuid();
        ScanRun run = ScanRun.Start(repositoryId, 1_800, DateTimeOffset.UtcNow);
        run.SetCandidateCount(1);
        string hash = new('a', 64);
        var unit = new CodeUnit(
            run.Id,
            repositoryId,
            "src/A.cs",
            hash,
            hash,
            1,
            1,
            "A",
            CodeUnitType.Class,
            RangeConfidence.Exact,
            "c_sharp");
        run.Complete(1, 1, DateTimeOffset.UtcNow);

        MethodInfo? create = draftType!.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(create);
        ParameterInfo[] parameters = create!.GetParameters();
        Assert.Equal(7, parameters.Length);
        object frameworks = Array.CreateInstance(
            typeof(CodeUnit).Assembly.GetType("Vulgata.Core.Entities.CodeUnitFramework")!,
            0);
        object summaries = Array.CreateInstance(
            typeof(CodeUnit).Assembly.GetType("Vulgata.Core.Entities.ScanRunLanguageSummary")!,
            0);

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
            create.Invoke(
                null,
                [
                    run,
                    new[] { unit },
                    Array.Empty<CodeUnitEdge>(),
                    Array.Empty<UnresolvedStructuralRelationshipCandidate>(),
                    frameworks,
                    summaries,
                    Array.Empty<ScanCandidateOutcome>(),
                ]));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("language summary", exception.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Model_HistoricalCodeUnitWithNullLanguage_RemainsReadableAsync()
    {
        var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new VulgataDbContext(dbOptions);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        RepositoryEntity repository = RepositoryEntity.CreateStandalone(
            "historical",
            "https://example.invalid/historical.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        ScanRun run = ScanRun.Start(repository.Id, 1_800, DateTimeOffset.UtcNow);
        string hash = new('a', 64);
        var historical = new CodeUnit(
            run.Id,
            repository.Id,
            "src/Legacy.cs",
            hash,
            hash,
            1,
            1,
            "Legacy",
            CodeUnitType.Class,
            RangeConfidence.Exact,
            null);
        db.Repositories.Add(repository);
        db.ScanRuns.Add(run);
        db.CodeUnits.Add(historical);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        CodeUnit reloaded = await db.CodeUnits.SingleAsync();

        Assert.Null(reloaded.Language);
    }

    [Fact]
    public void ProjectionQueries_ExactCanonicalText_HaveAcceptedHashes()
    {
        Assert.Equal(
            "dbba4ff5ac4834ae2857307dad36c177d9655ac951744c646f9c6e54d3923f33",
            Convert.ToHexStringLower(SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(CodeGraphContextProviderQueries.CodeUnits))));
        Assert.Equal(
            "40d2da3b9b520b3c4eca3fd7c3bad4f4fb10643736e3a9e9b4ddf62c92adc096",
            Convert.ToHexStringLower(SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(CodeGraphContextProviderQueries.Relationships))));
    }

    [Fact]
    public async Task Parse_WithIndependentProjections_PreservesIsolatedUnitsAndMapsRelationshipsAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-projection-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "A.cs"), "class A { }\n// isolated");
        try
        {
            object[] codeUnitRows =
            [
                new
                {
                    record_kind = "code_unit",
                    provider_kind = "File",
                    path = "src/A.cs",
                    name = (string?)null,
                    start_line = (int?)null,
                    end_line = (int?)null,
                    parser_language = (string?)null,
                },
                new
                {
                    record_kind = "code_unit",
                    provider_kind = "Module",
                    path = (string?)null,
                    name = "external.module",
                    start_line = (int?)1,
                    end_line = (int?)1,
                    parser_language = (string?)null,
                },
                new
                {
                    record_kind = "code_unit",
                    provider_kind = "Class",
                    path = "src/A.cs",
                    name = "IsolatedClass",
                    start_line = (int?)1,
                    end_line = (int?)1,
                    parser_language = "c_sharp",
                },
                new
                {
                    record_kind = "code_unit",
                    provider_kind = "Function",
                    path = "src/A.cs",
                    name = "IsolatedFunction",
                    start_line = (int?)2,
                    end_line = (int?)2,
                    parser_language = "C_SHARP",
                },
            ];
            object[] relationshipRows =
            [
                new
                {
                    record_kind = "relationship",
                    relationship_kind = "CONTAINS",
                    source_provider_kind = "File",
                    source_path = "src/A.cs",
                    source_name = (string?)null,
                    source_start_line = (int?)null,
                    source_end_line = (int?)null,
                    target_provider_kind = "Class",
                    target_path = "src/A.cs",
                    target_name = "IsolatedClass",
                    target_start_line = (int?)1,
                    target_end_line = (int?)1,
                },
                new
                {
                    record_kind = "relationship",
                    relationship_kind = "IMPORTS",
                    source_provider_kind = "File",
                    source_path = "src/A.cs",
                    source_name = (string?)null,
                    source_start_line = (int?)null,
                    source_end_line = (int?)null,
                    target_provider_kind = "Module",
                    target_path = (string?)null,
                    target_name = "external.module",
                    target_start_line = (int?)1,
                    target_end_line = (int?)1,
                },
            ];
            byte[] codeUnitsJson = CreateEnvelope(
                CodeGraphContextProviderQueries.CodeUnits,
                codeUnitRows);
            byte[] relationshipsJson = CreateEnvelope(
                CodeGraphContextProviderQueries.Relationships,
                relationshipRows);

            CodeGraphProviderResult result = CodeGraphHttpProjectionParser.Parse(
                codeUnitsJson,
                relationshipsJson,
                root,
                TimeSpan.FromSeconds(1));

            Assert.True(result.Succeeded);
            Assert.Equal(3, result.CodeUnits.Count);
            Assert.Equal(2, result.Relationships.Count);
            Assert.Contains(result.CodeUnits, unit => unit.SymbolName == "IsolatedClass");
            Assert.Contains(result.CodeUnits, unit => unit.SymbolName == "IsolatedFunction");
            Assert.All(result.CodeUnits, unit => Assert.Equal("c_sharp", unit.Language));
            ProviderCodeUnit file = Assert.Single(
                result.CodeUnits,
                unit => unit.UnitType == CodeUnitType.File);
            Assert.Equal((1, 2), (file.LineStart, file.LineEnd));
            Assert.All(result.CodeUnits, unit =>
                Assert.Matches("^cgc-v1:[0-9a-f]{64}$", unit.EndpointIdentity));
            ProviderStructuralRelationship contains = Assert.Single(
                result.Relationships,
                relationship => relationship.RelationshipKind == StructuralRelationshipKind.Contains);
            ProviderCodeUnit containedClass = Assert.Single(
                result.CodeUnits,
                unit => unit.SymbolName == "IsolatedClass");
            Assert.Equal(file.EndpointIdentity, contains.SourceEndpointIdentity);
            Assert.Equal(containedClass.EndpointIdentity, contains.TargetEndpointIdentity);
            ProviderStructuralRelationship imports = Assert.Single(
                result.Relationships,
                relationship => relationship.RelationshipKind == StructuralRelationshipKind.Imports);
            Assert.DoesNotContain(
                result.CodeUnits,
                unit => unit.EndpointIdentity == imports.TargetEndpointIdentity);
            Assert.Equal(1, result.ParsedFileCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartStandaloneRepository_WithParserLanguageProfile_PersistsPerPathLanguagesAndDistinctPathSummariesAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-language-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Mismatch.cs"), "class Mismatch { void Run() { } }");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Novel.cs"), "class Novel { }");
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "language-profile",
                "https://example.invalid/language-profile.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var provider = new FixedLanguageProvider(
            [
                new(
                    "src/Mismatch.cs",
                    "Mismatch",
                    CodeUnitType.Class,
                    1,
                    1,
                    "mismatch-class",
                    RangeConfidence.Exact,
                    " KOTLIN "),
                new(
                    "src/Mismatch.cs",
                    "Run",
                    CodeUnitType.Function,
                    1,
                    1,
                    "mismatch-function",
                    RangeConfidence.Exact,
                    "kotlin"),
                new(
                    "src/Novel.cs",
                    "Novel",
                    CodeUnitType.Class,
                    1,
                    1,
                    "novel-class",
                    RangeConfidence.Exact,
                    "zig_0+#.-"),
            ]);
            var candidates = new FixedCandidates(
            [
                new("src/Mismatch.cs", false, "Eligible", 0),
                new("src/Novel.cs", false, "Eligible", 0),
            ]);
            RepositoryProfilingCoordinator coordinator =
                CreateCoordinator(db, root, provider, candidates);

            RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
                repository.Id,
                null!,
                CancellationToken.None);

            Assert.Equal(RepositoryProfilingOutcome.Success, result.Outcome);
            CodeUnit[] units = await db.CodeUnits.ToArrayAsync();
            Assert.Equal(3, units.Length);
            Assert.All(
                units.Where(unit => unit.FilePath == "src/Mismatch.cs"),
                unit => Assert.Equal("kotlin", unit.Language));
            Assert.Equal(
                "zig_0+#.-",
                Assert.Single(units, unit => unit.FilePath == "src/Novel.cs").Language);
            ScanRunLanguageSummary[] summaries = await db.ScanRunLanguageSummaries.ToArrayAsync();
            Assert.Equal(2, summaries.Length);
            Assert.Contains(
                summaries,
                summary => summary.Language == "kotlin" && summary.DistinctFileCount == 1);
            Assert.Contains(
                summaries,
                summary => summary.Language == "zig_0+#.-" && summary.DistinctFileCount == 1);
            ScanRun run = await db.ScanRuns.SingleAsync();
            Assert.Equal(1_800, run.ProviderProcessTimeoutSeconds);
            Assert.Equal(TimeSpan.FromSeconds(1_800), provider.ObservedTimeout);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartStandaloneRepository_WhenEligibleCandidateIsAbsentFromCompleteProvider_FiltersUnknownLanguageOnceAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-unknown-candidate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Known.cs"), "class Known { }");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Unknown.txt"), "no parser identity");
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "unknown-candidate",
                "https://example.invalid/unknown-candidate.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var provider = new FixedLanguageProvider(
            [
                new(
                    "src/Known.cs",
                    "Known",
                    CodeUnitType.Class,
                    1,
                    1,
                    "known",
                    RangeConfidence.Exact,
                    "c_sharp"),
            ]);
            var candidates = new FixedCandidates(
            [
                new("src/Known.cs", false, "Eligible", 0),
                new("src/Unknown.txt", false, "Eligible", 1),
                new("src/Unknown.txt", false, "EligibleDuplicate", 2),
            ]);
            RepositoryProfilingCoordinator coordinator =
                CreateCoordinator(db, root, provider, candidates);

            RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
                repository.Id,
                null!,
                CancellationToken.None);

            Assert.Equal(RepositoryProfilingOutcome.Success, result.Outcome);
            CodeUnit retained = Assert.Single(await db.CodeUnits.ToArrayAsync());
            Assert.Equal("src/Known.cs", retained.FilePath);
            Assert.Empty(await db.CodeUnitEdges.ToArrayAsync());
            Assert.Empty(await db.UnresolvedStructuralRelationshipCandidates.ToArrayAsync());
            Assert.Empty(await db.CodeUnitFrameworks
                .Where(fact => fact.CodeUnitId != retained.Id)
                .ToArrayAsync());
            ScanCandidateOutcome unknown = Assert.Single(
                await db.ScanCandidateOutcomes
                    .Where(outcome => outcome.FilePath == "src/Unknown.txt")
                    .ToArrayAsync());
            Assert.Equal(ScanCandidateOutcomeKind.Filtered, unknown.Outcome);
            Assert.Equal(ScanCandidateOutcomeReasons.UnknownLanguage, unknown.ReasonOrCategory);
            Assert.DoesNotContain(
                await db.ScanCandidateOutcomes.ToArrayAsync(),
                outcome => outcome.ReasonOrCategory == "NoCodeUnitExtracted");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartStandaloneRepository_AtUnknownAuditBoundProfilesButOverflowRejectsBeforeProviderAsync(
        bool combinedUtf8ByteBound)
    {
        IReadOnlyList<string> acceptedPaths = combinedUtf8ByteBound
            ? CreateExactUnknownAuditByteBoundPaths()
            :
            [
                .. Enumerable.Range(0, 10_000)
                    .Select(index => $"src/unknown/{index:D5}.txt"),
            ];
        IReadOnlyList<string> overflowPaths = combinedUtf8ByteBound
            ? [.. acceptedPaths, "z"]
            :
            [
                .. Enumerable.Range(0, 10_001)
                    .Select(index => $"src/unknown/{index:D5}.txt"),
            ];

        CandidateBoundObservation accepted =
            await ProfileCandidateBoundCaseAsync(acceptedPaths);
        CandidateBoundObservation overflow =
            await ProfileCandidateBoundCaseAsync(overflowPaths);

        Assert.Equal(1, accepted.ProviderInvocationCount);
        Assert.Equal(0, accepted.DetectorInvocationCount);
        Assert.Equal(RepositoryProfilingOutcome.ProviderFailure, accepted.Outcome);
        Assert.Equal(0, overflow.ProviderInvocationCount);
        Assert.Equal(0, overflow.DetectorInvocationCount);
        Assert.NotEqual(RepositoryProfilingOutcome.ProviderFailure, overflow.Outcome);
        Assert.Equal("候选源文件审计范围超出限制。", overflow.Message);
        Assert.Equal(ScanRunStatus.Failed, overflow.ScanRunStatus);
        Assert.Equal(0, overflow.TotalCandidateFileCount);
        Assert.Equal(0, overflow.CandidateOutcomeCount);
        Assert.Equal(0, overflow.CodeUnitCount);
        Assert.Equal(0, overflow.EdgeCount);
        Assert.Equal(0, overflow.UnresolvedRelationshipCount);
        Assert.Equal(0, overflow.FrameworkFactCount);
        Assert.Equal(0, overflow.LanguageSummaryCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartStandaloneRepository_WithIncompleteOrConflictingProviderLanguage_PersistsNoProviderAggregateAsync(
        bool conflicting)
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-invalid-language-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "A.cs"), "class A { void Run() { } }");
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "invalid-language",
                "https://example.invalid/invalid-language.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var provider = new FixedLanguageProvider(
            [
                new(
                    "src/A.cs",
                    "A",
                    CodeUnitType.Class,
                    1,
                    1,
                    "a",
                    RangeConfidence.Exact,
                    conflicting ? "java" : null),
                new(
                    "src/A.cs",
                    "Run",
                    CodeUnitType.Function,
                    1,
                    1,
                    "run",
                    RangeConfidence.Exact,
                    conflicting ? "kotlin" : "java"),
            ],
            [new("a", "run", StructuralRelationshipKind.Contains)]);
            var candidates = new FixedCandidates(
            [
                new("src/A.cs", false, "Eligible", 0),
                new("src/Generated.g.cs", true, "Generated", 1),
            ]);
            RepositoryProfilingCoordinator coordinator =
                CreateCoordinator(db, root, provider, candidates);

            RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
                repository.Id,
                null!,
                CancellationToken.None);

            Assert.Equal(RepositoryProfilingOutcome.ProviderFailure, result.Outcome);
            Assert.Equal("代码结构分析失败，请稍后重试。", result.Message);
            ScanRunSummaryDto value = Assert.IsType<ScanRunSummaryDto>(result.Value);
            Assert.Equal("失败", value.Status);
            Assert.Equal("代码结构分析失败，请稍后重试。", value.Message);
            db.ChangeTracker.Clear();
            Assert.Empty(await db.CodeUnits.ToListAsync());
            Assert.Empty(await db.CodeUnitEdges.ToListAsync());
            Assert.Empty(await db.UnresolvedStructuralRelationshipCandidates.ToListAsync());
            Assert.Empty(await db.CodeUnitFrameworks.ToListAsync());
            Assert.Empty(await db.ScanRunLanguageSummaries.ToListAsync());
            ScanCandidateOutcome localAudit = await db.ScanCandidateOutcomes.SingleAsync();
            Assert.Equal("src/Generated.g.cs", localAudit.FilePath);
            Assert.Equal("Generated", localAudit.ReasonOrCategory);
            ScanRun run = await db.ScanRuns.SingleAsync();
            Assert.Equal(ScanRunStatus.Failed, run.Status);
            Assert.Equal("代码结构分析失败，请稍后重试。", run.TerminalMessage);
            Assert.Equal(1_800, run.ProviderProcessTimeoutSeconds);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("java!")]
    public void ScanRun_Complete_WithNullEmptyOrInvalidCodeUnitLanguage_RejectsDraft(
        string? invalidLanguage)
    {
        Guid repositoryId = Guid.NewGuid();
        ScanRun run = ScanRun.Start(repositoryId, 1_800, DateTimeOffset.UtcNow);
        run.SetCandidateCount(1);
        CodeUnit unit = CreateLanguageValidationUnit(run, repositoryId, "src/A.cs", "A", "java");
        typeof(CodeUnit).GetProperty(nameof(CodeUnit.Language))!.SetValue(unit, invalidLanguage);
        run.CodeUnits.Add(unit);

        Assert.Throws<InvalidOperationException>(() =>
            run.Complete(1, 1, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ScanRun_Complete_WithConflictingLanguagesForOnePath_RejectsDraft()
    {
        Guid repositoryId = Guid.NewGuid();
        ScanRun run = ScanRun.Start(repositoryId, 1_800, DateTimeOffset.UtcNow);
        run.SetCandidateCount(1);
        run.CodeUnits.Add(CreateLanguageValidationUnit(run, repositoryId, "src/A.cs", "A", "java"));
        run.CodeUnits.Add(CreateLanguageValidationUnit(run, repositoryId, "src/A.cs", "Run", "kotlin"));

        Assert.Throws<InvalidOperationException>(() =>
            run.Complete(1, 2, DateTimeOffset.UtcNow));
    }

    private static CodeUnit CreateLanguageValidationUnit(
        ScanRun run,
        Guid repositoryId,
        string path,
        string symbol,
        string language)
    {
        string hash = new('a', 64);
        return new CodeUnit(
            run.Id,
            repositoryId,
            path,
            hash,
            hash,
            1,
            1,
            symbol,
            CodeUnitType.Class,
            RangeConfidence.Exact,
            language);
    }

    private static byte[] CreateEnvelope(string query, object[] results) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            status = "ok",
            message = (string?)null,
            error = (string?)null,
            data = new
            {
                success = true,
                query,
                record_count = results.Length,
                results,
            },
        });

    [Fact]
    public async Task StartStandaloneRepository_WithControlledProvider_PersistsTraceableResultAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "A.cs"), new string('a', 5_000));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "B.cs"), "class B { void Call() {} }");
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "fixture",
                "https://example.invalid/fixture.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();

            var coordinator = new RepositoryProfilingCoordinator(
                new SystemRepository(db),
                new RepositoryRepository(db),
                new ScanRunRepository(db),
                new SuccessfulWorkingCopy(root),
                new ControlledCandidates(),
                new ControlledProvider(),
                new CodeGraphOptions(Path.Combine(root, "state"), 1_800),
                NullLogger<RepositoryProfilingCoordinator>.Instance,
                new EmptyFrameworkEvidenceDetector(),
                FrameworkCatalog.LoadEmbedded());

            RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
                repository.Id,
                null!,
                CancellationToken.None);

            Assert.Equal(RepositoryProfilingOutcome.Success, result.Outcome);
            ScanRun run = await db.ScanRuns.SingleAsync();
            Assert.Equal(ScanRunStatus.Completed, run.Status);
            Assert.Equal(2, await db.CodeUnits.CountAsync());
            Assert.Equal(1, await db.CodeUnitEdges.CountAsync());
            Assert.Equal(0, await db.UnresolvedStructuralRelationshipCandidates.CountAsync());
            ScanCandidateOutcome[] candidateOutcomes =
                await db.ScanCandidateOutcomes.OrderBy(outcome => outcome.FilePath).ToArrayAsync();
            Assert.Equal(3, candidateOutcomes.Length);
            Assert.Contains(
                candidateOutcomes,
                outcome => outcome.FilePath == "src/A.cs" &&
                    outcome.Outcome == ScanCandidateOutcomeKind.Parsed &&
                    outcome.ReasonOrCategory == "CodeUnitExtracted");
            Assert.Contains(
                candidateOutcomes,
                outcome => outcome.FilePath == "src/B.cs" &&
                    outcome.Outcome == ScanCandidateOutcomeKind.Parsed &&
                    outcome.ReasonOrCategory == "CodeUnitExtracted");
            Assert.All(candidateOutcomes, outcome => Assert.InRange(outcome.ReasonOrCategory.Length, 1, 512));
            CodeUnit first = await db.CodeUnits.SingleAsync(unit => unit.FilePath == "src/A.cs");
            byte[] source = await File.ReadAllBytesAsync(Path.Combine(root, "src", "A.cs"));
            Assert.Equal(
                Convert.ToHexStringLower(SHA256.HashData(source.AsSpan(0, 4_096))),
                first.FileIdentityHash);
            Assert.NotEqual(first.FileIdentityHash, first.SourceHash);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartStandaloneRepository_WhenFinalCommitFails_RecoversDurableRunAndKeepsOnlyLocalAuditAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "A.cs"), "class A { }");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "B.cs"), "class B { }");
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "final-commit-failure",
                "https://example.invalid/fixture.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var failingRepository = new FailingFinalCommitScanRunRepository(new ScanRunRepository(db));
            var coordinator = new RepositoryProfilingCoordinator(
                new SystemRepository(db),
                new RepositoryRepository(db),
                failingRepository,
                new SuccessfulWorkingCopy(root),
                new ControlledCandidates(),
                new ControlledProvider(),
                new CodeGraphOptions(Path.Combine(root, "state"), 1_800),
                NullLogger<RepositoryProfilingCoordinator>.Instance,
                new EmptyFrameworkEvidenceDetector(),
                FrameworkCatalog.LoadEmbedded());

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                coordinator.StartStandaloneRepositoryAsync(repository.Id, null!, CancellationToken.None));

            db.ChangeTracker.Clear();
            ScanRun run = await db.ScanRuns.SingleAsync();
            Assert.Equal(ScanRunStatus.Failed, run.Status);
            Assert.Equal("代码结构分析失败，请稍后重试。", run.TerminalMessage);
            ScanCandidateOutcome localAudit = await db.ScanCandidateOutcomes.SingleAsync();
            Assert.Equal("src/Generated.g.cs", localAudit.FilePath);
            Assert.Equal(ScanCandidateOutcomeKind.Filtered, localAudit.Outcome);
            Assert.Equal("Generated", localAudit.ReasonOrCategory);
            Assert.Empty(await db.CodeUnits.ToListAsync());
            Assert.Empty(await db.CodeUnitEdges.ToListAsync());
            Assert.Empty(await db.UnresolvedStructuralRelationshipCandidates.ToListAsync());
            Assert.Empty(await db.CodeUnitFrameworks.ToListAsync());
            Assert.Empty(await db.ScanRunLanguageSummaries.ToListAsync());
            Assert.Equal(1, failingRepository.RecoveryCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public Task StartStandaloneRepository_WhenCancelledAfterDraftAttachmentBeforeFinalPersistence_DurablyAbortsWithoutProviderRowsAsync() =>
        AssertLateFinalBoundaryFailureAsync(
            LateFinalBoundaryFailureMode.CancelBeforePersistence,
            ScanRunStatus.Aborted,
            "代码结构分析已取消。");

    [Fact]
    public Task StartStandaloneRepository_WhenCancelledDuringFinalPersistenceAfterComplete_DurablyAbortsWithoutProviderRowsAsync() =>
        AssertLateFinalBoundaryFailureAsync(
            LateFinalBoundaryFailureMode.CancelDuringPersistence,
            ScanRunStatus.Aborted,
            "代码结构分析已取消。");

    [Fact]
    public Task StartStandaloneRepository_WhenFinalPersistenceThrowsIOException_DurablyFailsWithoutProviderRowsAsync() =>
        AssertLateFinalBoundaryFailureAsync(
            LateFinalBoundaryFailureMode.NonDbUpdateFailure,
            ScanRunStatus.Failed,
            "代码结构分析失败，请稍后重试。");

    private static async Task AssertLateFinalBoundaryFailureAsync(
        LateFinalBoundaryFailureMode failureMode,
        ScanRunStatus expectedStatus,
        string expectedMessage)
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "A.cs"), "class A { }");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "B.cs"), "class B { }");
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                $"late-final-boundary-{failureMode}",
                "https://example.invalid/fixture.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var boundaryRepository = new LateFinalBoundaryScanRunRepository(
                new ScanRunRepository(db),
                db,
                failureMode);
            var coordinator = new RepositoryProfilingCoordinator(
                new SystemRepository(db),
                new RepositoryRepository(db),
                boundaryRepository,
                new SuccessfulWorkingCopy(root),
                new ControlledCandidates(),
                new ControlledProvider(),
                new CodeGraphOptions(Path.Combine(root, "state"), 1_800),
                NullLogger<RepositoryProfilingCoordinator>.Instance,
                new AspNetFrameworkDetector(),
                FrameworkCatalog.LoadEmbedded());

            if (failureMode == LateFinalBoundaryFailureMode.NonDbUpdateFailure)
            {
                await Assert.ThrowsAsync<IOException>(() =>
                    coordinator.StartStandaloneRepositoryAsync(
                        repository.Id,
                        null!,
                        CancellationToken.None));
            }
            else
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    coordinator.StartStandaloneRepositoryAsync(
                        repository.Id,
                        null!,
                        CancellationToken.None));
            }

            Assert.True(boundaryRepository.ObservedCodeUnitCount > 0);
            Assert.True(boundaryRepository.ObservedFrameworkCount > 0);
            db.ChangeTracker.Clear();
            ScanRun run = await db.ScanRuns.SingleAsync();
            Assert.Equal(expectedStatus, run.Status);
            Assert.Equal(expectedMessage, run.TerminalMessage);
            ScanCandidateOutcome localAudit = await db.ScanCandidateOutcomes.SingleAsync();
            Assert.Equal("src/Generated.g.cs", localAudit.FilePath);
            Assert.Equal(ScanCandidateOutcomeKind.Filtered, localAudit.Outcome);
            Assert.Equal("Generated", localAudit.ReasonOrCategory);
            Assert.Empty(await db.CodeUnits.ToListAsync());
            Assert.Empty(await db.CodeUnitEdges.ToListAsync());
            Assert.Empty(await db.UnresolvedStructuralRelationshipCandidates.ToListAsync());
            Assert.Empty(await db.CodeUnitFrameworks.ToListAsync());
            Assert.Empty(await db.ScanRunLanguageSummaries.ToListAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartStandaloneRepository_WithDuplicateResolvedEdge_DoesNotPersistUnresolvedCandidateAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "A.cs"), "class A { }");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "B.cs"), "class B { }");
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "duplicate-edge",
                "https://example.invalid/fixture.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var coordinator = CreateCoordinator(db, root, new DuplicateEdgeProvider());

            RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
                repository.Id,
                null!,
                CancellationToken.None);

            Assert.Equal(RepositoryProfilingOutcome.Success, result.Outcome);
            Assert.Single(await db.CodeUnitEdges.ToListAsync());
            Assert.Empty(await db.UnresolvedStructuralRelationshipCandidates.ToListAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartStandaloneRepository_WithMalformedAuditPath_IsolatesAuditRecordAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "malformed-audit",
                "https://example.invalid/fixture.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var coordinator = CreateCoordinator(db, root, new MalformedAuditProvider());

            RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
                repository.Id,
                null!,
                CancellationToken.None);

            Assert.Equal(RepositoryProfilingOutcome.Success, result.Outcome);
            ScanCandidateOutcome audit = await db.ScanCandidateOutcomes.SingleAsync(
                outcome => outcome.ReasonOrCategory == "MalformedProviderRow");
            Assert.Equal("(unknown)", audit.FilePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartStandaloneRepository_WhenEligiblePathBecomesSymbolicLink_DoesNotReadOutsideRootAsync()
    {
        string parent = Path.Combine(Path.GetTempPath(), $"vulgata-profile-{Guid.NewGuid():N}");
        string root = Path.Combine(parent, "repository");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(parent, "outside.cs"), "outside secret");
        File.CreateSymbolicLink(
            Path.Combine(root, "src", "A.cs"),
            Path.Combine(parent, "outside.cs"));
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "link-race",
                "https://example.invalid/fixture.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var coordinator = CreateCoordinator(db, root, new SingleUnitProvider());

            RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
                repository.Id,
                null!,
                CancellationToken.None);

            Assert.Equal(RepositoryProfilingOutcome.Success, result.Outcome);
            Assert.Empty(await db.CodeUnits.ToListAsync());
            Assert.Contains(
                await db.ScanCandidateOutcomes.ToListAsync(),
                outcome => outcome.ReasonOrCategory == "InvalidProviderUnit");
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public async Task StartStandaloneRepository_WhenProviderIsCancelled_TerminalizesScanRunAsAbortedAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "cancelled-provider",
                "https://example.invalid/fixture.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var coordinator = CreateCoordinator(db, root, new CancelledProvider());

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                coordinator.StartStandaloneRepositoryAsync(repository.Id, null!, CancellationToken.None));

            db.ChangeTracker.Clear();
            ScanRun run = await db.ScanRuns.SingleAsync();
            Assert.Equal(ScanRunStatus.Aborted, run.Status);
            Assert.Equal("代码结构分析已取消。", run.TerminalMessage);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartStandaloneRepository_WhenProviderIsMissing_RetainsFilteredAuditAndNoStructuralRecordsAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "missing-provider",
                "https://example.invalid/fixture.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var coordinator = new RepositoryProfilingCoordinator(
                new SystemRepository(db),
                new RepositoryRepository(db),
                new ScanRunRepository(db),
                new SuccessfulWorkingCopy(root),
                new ControlledCandidates(),
                new MissingProvider(),
                new CodeGraphOptions(Path.Combine(root, "state"), 1_800),
                NullLogger<RepositoryProfilingCoordinator>.Instance,
                new EmptyFrameworkEvidenceDetector(),
                FrameworkCatalog.LoadEmbedded());

            RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
                repository.Id,
                null!,
                CancellationToken.None);

            Assert.Equal(RepositoryProfilingOutcome.ProviderUnavailable, result.Outcome);
            Assert.Equal("本地 CodeGraph 服务不可用，请联系管理员。", result.Message);
            ScanRunSummaryDto value = Assert.IsType<ScanRunSummaryDto>(result.Value);
            Assert.Equal("失败", value.Status);
            Assert.Equal("本地 CodeGraph 服务不可用，请联系管理员。", value.Message);
            ScanRun run = await db.ScanRuns.SingleAsync();
            Assert.Equal(ScanRunStatus.Failed, run.Status);
            Assert.Equal("本地 CodeGraph 服务不可用，请联系管理员。", run.TerminalMessage);
            Assert.Equal(1, await db.ScanCandidateOutcomes.CountAsync());
            Assert.Equal(0, await db.CodeUnits.CountAsync());
            Assert.Equal(0, await db.CodeUnitEdges.CountAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartSystemRepository_WhenVisibilityIsRevokedDuringProfiling_ReturnsNotFoundAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            var system = new Vulgata.Core.Entities.System(
                "visibility",
                null,
                null,
                DateTimeOffset.UtcNow);
            RepositoryEntity repository = system.AddRepository(
                "fixture",
                "https://example.invalid/fixture.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Systems.Add(system);
            await db.SaveChangesAsync();
            var visibility = new VisibleThenRevokedSystemRepository(system);
            var coordinator = new RepositoryProfilingCoordinator(
                visibility,
                new RepositoryRepository(db),
                new ScanRunRepository(db),
                new SuccessfulWorkingCopy(root),
                new ControlledCandidates(),
                new MalformedAuditProvider(),
                new CodeGraphOptions(Path.Combine(root, "state"), 1_800),
                NullLogger<RepositoryProfilingCoordinator>.Instance,
                new EmptyFrameworkEvidenceDetector(),
                FrameworkCatalog.LoadEmbedded());

            RepositoryProfilingResult result = await coordinator.StartSystemRepositoryAsync(
                system.Id,
                repository.Id,
                new TestUserContext(),
                CancellationToken.None);

            Assert.Equal(RepositoryProfilingOutcome.RepositoryNotFound, result.Outcome);
            Assert.Equal(2, visibility.VisibilityChecks);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static object CreateRow(
        string kind,
        string sourcePath,
        string sourceSymbol,
        int sourceStart,
        int sourceEnd,
        string sourceIdentity,
        string targetPath,
        string targetSymbol,
        int targetStart,
        int targetEnd,
        string targetIdentity) => new
        {
            relationship_kind = kind,
            source_path = sourcePath,
            source_symbol = sourceSymbol,
            source_start_line = sourceStart,
            source_end_line = sourceEnd,
            source_identity = sourceIdentity,
            target_path = targetPath,
            target_symbol = targetSymbol,
            target_start_line = targetStart,
            target_end_line = targetEnd,
            target_identity = targetIdentity,
            filtered_target = false,
        };

    private sealed class SuccessfulWorkingCopy(string path) : IGitWorkingCopyService
    {
        public Task<GitWorkingCopyResult> AcquireAsync(
            RepositoryEntity repository,
            Guid scanRunId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GitWorkingCopyResult.Success(path));
    }

    private sealed class FailingWorkingCopy(string diagnostic) : IGitWorkingCopyService
    {
        public Task<GitWorkingCopyResult> AcquireAsync(
            RepositoryEntity repository,
            Guid scanRunId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GitWorkingCopyResult.Failure(diagnostic));
    }

    private sealed class ControlledCandidates : IScanCandidateEnumerator
    {
        public Task<IReadOnlyList<ScanSourceCandidate>> EnumerateAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScanSourceCandidate>>(
            [
                new("src/A.cs", false, "Eligible", 0),
                new("src/B.cs", false, "Eligible", 0),
                new("src/Generated.g.cs", true, "Generated", 0),
            ]);
    }

    private sealed class FixedCandidates(IReadOnlyList<ScanSourceCandidate> candidates)
        : IScanCandidateEnumerator
    {
        public Task<IReadOnlyList<ScanSourceCandidate>> EnumerateAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(candidates);
    }

    private sealed class FixedLanguageProvider(
        IReadOnlyList<ProviderCodeUnit> units,
        IReadOnlyList<ProviderStructuralRelationship>? relationships = null)
        : ICodeGraphProvider
    {
        public TimeSpan? ObservedTimeout { get; private set; }

        public Task<CodeGraphProviderResult> ProfileAsync(
            string workingCopyPath,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            ObservedTimeout = timeout;
            return Task.FromResult(new CodeGraphProviderResult(
                true,
                units,
                relationships ?? [],
                [],
                units.Select(unit => unit.RelativePath).Distinct(StringComparer.Ordinal).Count(),
                TimeSpan.FromMilliseconds(10),
                null));
        }
    }

    private sealed class CountingFailureProvider : ICodeGraphProvider
    {
        public int InvocationCount { get; private set; }

        public Task<CodeGraphProviderResult> ProfileAsync(
            string workingCopyPath,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(CodeGraphProviderResult.Failure(
                "intentional boundary fixture failure",
                TimeSpan.FromMilliseconds(1)));
        }
    }

    private sealed class ControlledProvider : ICodeGraphProvider
    {
        public Task<CodeGraphProviderResult> ProfileAsync(
            string workingCopyPath,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CodeGraphProviderResult(
                true,
                [
                    new("src/A.cs", "A", CodeUnitType.Class, 1, 1, "a", RangeConfidence.Exact, "c_sharp"),
                    new("src/B.cs", "B", CodeUnitType.Class, 1, 1, "b", RangeConfidence.Exact, "c_sharp"),
                ],
                [new("a", "b", StructuralRelationshipKind.Calls)],
                [],
                2,
                TimeSpan.FromMilliseconds(10),
                null));
    }

    private sealed class DuplicateEdgeProvider : ICodeGraphProvider
    {
        public Task<CodeGraphProviderResult> ProfileAsync(
            string workingCopyPath,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CodeGraphProviderResult(
                true,
                [
                    new("src/A.cs", "A", CodeUnitType.Class, 1, 1, "a", RangeConfidence.Exact, "c_sharp"),
                    new("src/B.cs", "B", CodeUnitType.Class, 1, 1, "b", RangeConfidence.Exact, "c_sharp"),
                ],
                [
                    new("a", "b", StructuralRelationshipKind.Calls),
                    new("a", "b", StructuralRelationshipKind.Calls),
                ],
                [],
                2,
                TimeSpan.FromMilliseconds(10),
                null));
    }

    private sealed class MalformedAuditProvider : ICodeGraphProvider
    {
        public Task<CodeGraphProviderResult> ProfileAsync(
            string workingCopyPath,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CodeGraphProviderResult(
                true,
                [],
                [],
                [new("../secret.cs", ScanCandidateOutcomeKind.Errored, "MalformedProviderRow")],
                0,
                TimeSpan.FromMilliseconds(10),
                null));
    }

    private sealed class SingleUnitProvider : ICodeGraphProvider
    {
        public Task<CodeGraphProviderResult> ProfileAsync(
            string workingCopyPath,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CodeGraphProviderResult(
                true,
                [new("src/A.cs", "A", CodeUnitType.Class, 1, 1, "a", RangeConfidence.Exact, "c_sharp")],
                [],
                [],
                1,
                TimeSpan.FromMilliseconds(10),
                null));
    }

    private sealed class CancelledProvider : ICodeGraphProvider
    {
        public Task<CodeGraphProviderResult> ProfileAsync(
            string workingCopyPath,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException(cancellationToken);
    }

    private sealed class MissingProvider : ICodeGraphProvider
    {
        public Task<CodeGraphProviderResult> ProfileAsync(
            string workingCopyPath,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CodeGraphProviderResult.Failure(
                "CodeGraph CLI not found",
                TimeSpan.FromMilliseconds(1)));
    }

    public enum AdversarialGlobalFailure
    {
        MalformedProviderLanguage,
        WrongTypedProviderLanguage,
        TruncatedProviderResponse,
        ProviderProcessFailure,
        ProviderQueryFailure,
        ProviderCleanupFailure,
        DetectorRepositoryBoundExceeded,
        DetectorFault,
    }

    public enum InvalidFrameworkEvidence
    {
        Malformed,
        Unsupported,
        Ambiguous,
        Escape,
        Reparse,
        Oversize,
    }

    private sealed class AdversarialGlobalFailureProvider(
        AdversarialGlobalFailure failure) : ICodeGraphProvider
    {
        private const string RawDiagnostic =
            "query=MATCH (n) RETURN n; source=class Secret {}; " +
            "path=C:\\secrets\\source.cs; package=attacker-value; " +
            "password=hunter2; token=do-not-persist; elapsed=9876ms; RAW-DIAGNOSTIC";

        public Task<CodeGraphProviderResult> ProfileAsync(
            string workingCopyPath,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            failure switch
            {
                AdversarialGlobalFailure.MalformedProviderLanguage =>
                    throw new InvalidDataException(
                        $"Malformed provider language. {RawDiagnostic}"),
                AdversarialGlobalFailure.WrongTypedProviderLanguage =>
                    throw new InvalidDataException(
                        $"Provider language was not a string. {RawDiagnostic}"),
                AdversarialGlobalFailure.TruncatedProviderResponse =>
                    throw new InvalidDataException(
                        $"Provider response was truncated. {RawDiagnostic}"),
                AdversarialGlobalFailure.ProviderProcessFailure =>
                    Task.FromResult(CodeGraphProviderResult.Failure(
                        $"Provider process failed. {RawDiagnostic}",
                        TimeSpan.FromMilliseconds(1))),
                AdversarialGlobalFailure.ProviderQueryFailure =>
                    Task.FromResult(CodeGraphProviderResult.Failure(
                        $"Provider query failed. {RawDiagnostic}",
                        TimeSpan.FromMilliseconds(1))),
                AdversarialGlobalFailure.ProviderCleanupFailure =>
                    Task.FromResult(CodeGraphProviderResult.Failure(
                        $"Provider cleanup failed. {RawDiagnostic}",
                        TimeSpan.FromMilliseconds(1))),
                _ => Task.FromResult(new CodeGraphProviderResult(
                    true,
                    [
                        new(
                            "src/A.cs",
                            "A",
                            CodeUnitType.Class,
                            1,
                            1,
                            "a",
                            RangeConfidence.Exact,
                            "c_sharp"),
                    ],
                    [],
                    [],
                    1,
                    TimeSpan.FromMilliseconds(10),
                    null)),
            };
    }

    private sealed class AdversarialGlobalFailureDetector(
        AdversarialGlobalFailure failure) : IFrameworkEvidenceDetector
    {
        public int DetectionCount { get; private set; }
        public int ApplicabilityCount { get; private set; }

        public Task<IReadOnlyList<PackageEvidence>> DetectAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Legacy detector wrapper must not be used.");

        public Task<FrameworkEvidenceDetectionResult> DetectDetailedAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default)
        {
            DetectionCount++;
            return failure switch
            {
                AdversarialGlobalFailure.DetectorRepositoryBoundExceeded =>
                    Task.FromResult(FrameworkEvidenceDetectionResult.Failed(
                        FrameworkEvidenceDetectionFailure.RepositoryBoundExceeded,
                        "query=MATCH (n) RETURN n; source=class Secret {}; " +
                        "path=C:\\secrets\\source.cs; package=attacker-value; " +
                        "password=hunter2; token=do-not-persist; elapsed=9876ms; RAW-DIAGNOSTIC")),
                AdversarialGlobalFailure.DetectorFault =>
                    throw new InvalidDataException(
                        "Detector fault; query=MATCH (n) RETURN n; source=class Secret {}; " +
                        "path=C:\\secrets\\source.cs; package=attacker-value; " +
                        "password=hunter2; token=do-not-persist; elapsed=9876ms; RAW-DIAGNOSTIC"),
                _ => Task.FromResult(FrameworkEvidenceDetectionResult.Success([], [], 0)),
            };
        }

        public FrameworkEvidenceDetectionResult SelectApplicable(
            FrameworkEvidenceDetectionResult detection,
            string codeUnitPath,
            string parserLanguage)
        {
            ApplicabilityCount++;
            return FrameworkEvidenceDetectionResult.Success(
                [],
                detection.AuditOutcomes,
                detection.TotalAuditOutcomeCount);
        }
    }

    private sealed class ProfilingEvidenceGitFixture : IDisposable
    {
        private string? _externalDirectory;

        public ProfilingEvidenceGitFixture()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"vulgata-profile-evidence-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
            RunGit("init", "--quiet");
            RunGit("config", "core.longpaths", "true");
            RunGit("config", "core.symlinks", "true");
        }

        public string Path { get; }

        public void Write(string relativePath, string content)
        {
            string fullPath = System.IO.Path.Combine(
                Path,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(
                fullPath,
                content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public void TrackAll() => RunGit("add", "--all");

        public void Track(params string[] relativePaths)
        {
            var arguments = new List<string> { "add", "--" };
            arguments.AddRange(relativePaths);
            RunGit([.. arguments]);
        }

        public void CreateFileSymbolicLink(
            string linkRelativePath,
            string targetRelativePath,
            string targetContent,
            bool targetOutsideRepository)
        {
            string targetRoot;
            if (targetOutsideRepository)
            {
                _externalDirectory ??= System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"vulgata-profile-evidence-external-{Guid.NewGuid():N}");
                targetRoot = _externalDirectory;
            }
            else
            {
                targetRoot = System.IO.Path.Combine(Path, ".fixture-targets");
            }

            string targetPath = System.IO.Path.Combine(
                targetRoot,
                targetRelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetPath)!);
            File.WriteAllText(
                targetPath,
                targetContent,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            string linkPath = System.IO.Path.Combine(
                Path,
                linkRelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(linkPath)!);
            File.CreateSymbolicLink(linkPath, targetPath);
        }

        public void Dispose()
        {
            DeleteDirectory(Path);
            if (_externalDirectory is not null)
            {
                DeleteDirectory(_externalDirectory);
            }
        }

        private static void DeleteDirectory(string path)
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void RunGit(params string[] arguments)
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    WorkingDirectory = Path,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                },
            };
            foreach (string argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            Assert.True(process.Start());
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(
                process.ExitCode == 0,
                $"git {string.Join(' ', arguments)} failed: {standardError}");
        }
    }

    private sealed class CountingDelegatingFrameworkDetector(
        IFrameworkEvidenceDetector inner) : IFrameworkEvidenceDetector
    {
        public int DetectionCount { get; private set; }

        public Task<IReadOnlyList<PackageEvidence>> DetectAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default) =>
            inner.DetectAsync(workingCopyPath, cancellationToken);

        public Task<FrameworkEvidenceDetectionResult> DetectDetailedAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default)
        {
            DetectionCount++;
            return inner.DetectDetailedAsync(workingCopyPath, cancellationToken);
        }

        public FrameworkEvidenceDetectionResult SelectApplicable(
            FrameworkEvidenceDetectionResult detection,
            string codeUnitPath,
            string parserLanguage) =>
            inner.SelectApplicable(detection, codeUnitPath, parserLanguage);
    }

    private sealed class EmptyFrameworkEvidenceDetector : IFrameworkEvidenceDetector
    {
        public Task<IReadOnlyList<PackageEvidence>> DetectAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PackageEvidence>>([]);

        public Task<FrameworkEvidenceDetectionResult> DetectDetailedAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FrameworkEvidenceDetectionResult.Success([], [], 0));

        public FrameworkEvidenceDetectionResult SelectApplicable(
            FrameworkEvidenceDetectionResult detection,
            string codeUnitPath,
            string parserLanguage) =>
            FrameworkEvidenceDetectionResult.Success(
                [],
                detection.AuditOutcomes,
                detection.TotalAuditOutcomeCount);
    }

    private sealed class CountingFrameworkDetector : IFrameworkEvidenceDetector
    {
        public int DetectionCount { get; private set; }
        public int ApplicabilityCount { get; private set; }

        public Task<IReadOnlyList<PackageEvidence>> DetectAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Legacy detector wrapper must not be used.");

        public Task<FrameworkEvidenceDetectionResult> DetectDetailedAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default)
        {
            DetectionCount++;
            return Task.FromResult(FrameworkEvidenceDetectionResult.Success(
                [],
                [
                    new(
                        "unsupported/package.json",
                        ScanCandidateOutcomeKind.Skipped,
                        ScanCandidateOutcomeReasons.FrameworkEvidenceUnsupported,
                        "框架证据格式不受支持。"),
                    new(
                        "malformed/package.json",
                        ScanCandidateOutcomeKind.Errored,
                        ScanCandidateOutcomeReasons.FrameworkEvidenceMalformed,
                        "框架证据格式无效。"),
                ],
                totalAuditOutcomeCount: 101));
        }

        public FrameworkEvidenceDetectionResult SelectApplicable(
            FrameworkEvidenceDetectionResult detection,
            string codeUnitPath,
            string parserLanguage)
        {
            ApplicabilityCount++;
            IReadOnlyList<PackageEvidence> evidence = parserLanguage switch
            {
                "typescript" =>
                [
                    Evidence(
                        "npm:react",
                        "package.json#json:/dependencies/react"),
                    Evidence(
                        "npm:express",
                        "package.json#json:/dependencies/express"),
                ],
                "python" =>
                [
                    Evidence(
                        "python:django",
                        "requirements.txt#line:1:python:django",
                        ecosystem: "python-requirements"),
                ],
                _ => [],
            };
            return FrameworkEvidenceDetectionResult.Success(
                evidence,
                detection.AuditOutcomes,
                detection.TotalAuditOutcomeCount);
        }

        private static PackageEvidence Evidence(
            string coordinate,
            string source,
            string ecosystem = "npm") =>
            new(
                source[..source.IndexOf('#', StringComparison.Ordinal)],
                string.Empty,
                ecosystem,
                coordinate,
                PackageEvidenceRelationship.Direct,
                source,
                0);
    }

    private sealed class FailingFrameworkDetector : IFrameworkEvidenceDetector
    {
        public int DetectionCount { get; private set; }

        public Task<IReadOnlyList<PackageEvidence>> DetectAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Legacy detector wrapper must not be used.");

        public Task<FrameworkEvidenceDetectionResult> DetectDetailedAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default)
        {
            DetectionCount++;
            return Task.FromResult(FrameworkEvidenceDetectionResult.Failed(
                FrameworkEvidenceDetectionFailure.DetectorFailed,
                "框架证据检测失败。"));
        }

        public FrameworkEvidenceDetectionResult SelectApplicable(
            FrameworkEvidenceDetectionResult detection,
            string codeUnitPath,
            string parserLanguage) =>
            throw new InvalidOperationException("Applicability must not run after detector failure.");
    }

    private sealed class AspNetFrameworkDetector : IFrameworkEvidenceDetector
    {
        private static readonly PackageEvidence _evidence = new(
            "App.csproj",
            string.Empty,
            "nuget",
            "nuget:Microsoft.AspNetCore.Mvc",
            PackageEvidenceRelationship.Direct,
            "App.csproj#xml:PackageReference[Microsoft.AspNetCore.Mvc]",
            0);

        public Task<IReadOnlyList<PackageEvidence>> DetectAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PackageEvidence>>([_evidence]);

        public Task<FrameworkEvidenceDetectionResult> DetectDetailedAsync(
            string workingCopyPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FrameworkEvidenceDetectionResult.Success([_evidence], [], 0));

        public FrameworkEvidenceDetectionResult SelectApplicable(
            FrameworkEvidenceDetectionResult detection,
            string codeUnitPath,
            string parserLanguage) =>
            FrameworkEvidenceDetectionResult.Success(
                [_evidence],
                detection.AuditOutcomes,
                detection.TotalAuditOutcomeCount);
    }

    private enum LateFinalBoundaryFailureMode
    {
        CancelBeforePersistence,
        CancelDuringPersistence,
        NonDbUpdateFailure,
    }

    private sealed class LateFinalBoundaryScanRunRepository(
        IScanRunRepository inner,
        VulgataDbContext dbContext,
        LateFinalBoundaryFailureMode failureMode) : IScanRunRepository
    {
        public int ObservedCodeUnitCount { get; private set; }
        public int ObservedFrameworkCount { get; private set; }

        public Task<ScanRun?> GetAsync(
            Guid repositoryId,
            Guid scanRunId,
            CancellationToken cancellationToken = default) =>
            inner.GetAsync(repositoryId, scanRunId, cancellationToken);

        public Task AddAsync(ScanRun scanRun, CancellationToken cancellationToken = default) =>
            inner.AddAsync(scanRun, cancellationToken);

        public void AddCandidateOutcomes(IEnumerable<ScanCandidateOutcome> outcomes) =>
            inner.AddCandidateOutcomes(outcomes);

        public void AddStructuralRecords(
            IEnumerable<CodeUnit> codeUnits,
            IEnumerable<CodeUnitEdge> edges,
            IEnumerable<UnresolvedStructuralRelationshipCandidate> unresolvedRelationships) =>
            inner.AddStructuralRecords(codeUnits, edges, unresolvedRelationships);

        public async Task PersistCompletedProfileAsync(
            RepositoryProfileDraft draft,
            CancellationToken cancellationToken = default)
        {
            ObservedCodeUnitCount = draft.CodeUnits.Count;
            ObservedFrameworkCount = draft.CodeUnitFrameworks.Count;
            if (failureMode == LateFinalBoundaryFailureMode.CancelBeforePersistence)
            {
                throw new OperationCanceledException(
                    new CancellationToken(canceled: true));
            }

            if (failureMode == LateFinalBoundaryFailureMode.NonDbUpdateFailure)
            {
                throw new IOException("Injected non-DbUpdate final persistence failure.");
            }

            using var cancellation = new CancellationTokenSource();
            EventHandler<SavingChangesEventArgs> cancelDuringSave =
                (_, _) => cancellation.Cancel();
            dbContext.SavingChanges += cancelDuringSave;
            try
            {
                await inner.PersistCompletedProfileAsync(draft, cancellation.Token);
            }
            finally
            {
                dbContext.SavingChanges -= cancelDuringSave;
            }
        }

        public Task RecoverFailedFinalCommitAsync(
            Guid repositoryId,
            Guid scanRunId,
            string sanitizedMessage,
            DateTimeOffset failedAt,
            CancellationToken cancellationToken = default) =>
            inner.RecoverFailedFinalCommitAsync(
                repositoryId,
                scanRunId,
                sanitizedMessage,
                failedAt,
                cancellationToken);

        public Task RecoverAbortedFinalCommitAsync(
            Guid repositoryId,
            Guid scanRunId,
            string sanitizedMessage,
            DateTimeOffset abortedAt,
            CancellationToken cancellationToken = default) =>
            inner.RecoverAbortedFinalCommitAsync(
                repositoryId,
                scanRunId,
                sanitizedMessage,
                abortedAt,
                cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            inner.SaveChangesAsync(cancellationToken);
    }

    private sealed class FailingFinalCommitScanRunRepository(IScanRunRepository inner) : IScanRunRepository
    {
        public int RecoveryCount { get; private set; }

        public Task<ScanRun?> GetAsync(
            Guid repositoryId,
            Guid scanRunId,
            CancellationToken cancellationToken = default) =>
            inner.GetAsync(repositoryId, scanRunId, cancellationToken);

        public Task AddAsync(ScanRun scanRun, CancellationToken cancellationToken = default) =>
            inner.AddAsync(scanRun, cancellationToken);

        public void AddCandidateOutcomes(IEnumerable<ScanCandidateOutcome> outcomes) =>
            inner.AddCandidateOutcomes(outcomes);

        public void AddStructuralRecords(
            IEnumerable<CodeUnit> codeUnits,
            IEnumerable<CodeUnitEdge> edges,
            IEnumerable<UnresolvedStructuralRelationshipCandidate> unresolvedRelationships) =>
            inner.AddStructuralRecords(codeUnits, edges, unresolvedRelationships);

        public Task PersistCompletedProfileAsync(
            RepositoryProfileDraft draft,
            CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Injected final commit failure.");

        public async Task RecoverFailedFinalCommitAsync(
            Guid repositoryId,
            Guid scanRunId,
            string sanitizedMessage,
            DateTimeOffset failedAt,
            CancellationToken cancellationToken = default)
        {
            RecoveryCount++;
            await inner.RecoverFailedFinalCommitAsync(
                repositoryId,
                scanRunId,
                sanitizedMessage,
                failedAt,
                cancellationToken);
        }

        public Task RecoverAbortedFinalCommitAsync(
            Guid repositoryId,
            Guid scanRunId,
            string sanitizedMessage,
            DateTimeOffset abortedAt,
            CancellationToken cancellationToken = default) =>
            inner.RecoverAbortedFinalCommitAsync(
                repositoryId,
                scanRunId,
                sanitizedMessage,
                abortedAt,
                cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            inner.SaveChangesAsync(cancellationToken);
    }

    private sealed class TestUserContext : IUserContext
    {
        public string UserId => "user";

        public bool IsAdministrator => false;

        public SystemOwnerFilter ToSystemOwnerFilter() => SystemOwnerFilter.OwnedBy(UserId);
    }

    private sealed class VisibleThenRevokedSystemRepository(
        Vulgata.Core.Entities.System visibleSystem) : ISystemRepository
    {
        public int VisibilityChecks { get; private set; }

        public Task<Vulgata.Core.Entities.System?> GetVisibleByIdAsync(
            Guid id,
            SystemOwnerFilter filter,
            CancellationToken cancellationToken = default)
        {
            VisibilityChecks++;
            return Task.FromResult<Vulgata.Core.Entities.System?>(
                VisibilityChecks == 1 ? visibleSystem : null);
        }

        public Task<IReadOnlyList<Vulgata.Core.Entities.System>> ListVisibleAsync(
            SystemOwnerFilter filter,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Vulgata.Core.Entities.System?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> NameExistsAsync(
            string name,
            Guid? excludeSystemId = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Vulgata.Core.Entities.System> AddAsync(
            Vulgata.Core.Entities.System system,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SystemDeleteResult> DeleteIfNoDependenciesAsync(
            Guid id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SystemDependencyCounts> GetDependencyCountsAsync(
            Guid id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SystemOwnerAssignmentWriteResult> AssignOwnerAsync(
            Guid systemId,
            string userId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> RemoveOwnerAsync(
            Guid systemId,
            string userId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<SystemOwnerAssignmentSummary>> ListOwnersAsync(
            Guid systemId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> CountOwnerAssignmentsByUserAsync(
            string userId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static RepositoryProfilingCoordinator CreateCoordinator(
        VulgataDbContext db,
        string root,
        ICodeGraphProvider provider,
        IScanCandidateEnumerator? candidates = null) =>
        new(
            new SystemRepository(db),
            new RepositoryRepository(db),
            new ScanRunRepository(db),
            new SuccessfulWorkingCopy(root),
            candidates ?? new ControlledCandidates(),
            provider,
            new CodeGraphOptions(Path.Combine(root, "state"), 1_800),
            NullLogger<RepositoryProfilingCoordinator>.Instance,
            new EmptyFrameworkEvidenceDetector(),
            FrameworkCatalog.LoadEmbedded());

    private static string[] CreateExactUnknownAuditByteBoundPaths()
    {
        const int pathCount = 8_192;
        const int pathByteCount = 2_048;
        string[] paths =
        [
            .. Enumerable.Range(0, pathCount).Select(index =>
            {
                string prefix = $"src/{index:D4}/";
                return prefix + new string('x', pathByteCount - prefix.Length);
            }),
        ];
        Assert.Equal(
            16 * 1024 * 1024,
            paths.Sum(Encoding.UTF8.GetByteCount));
        return paths;
    }

    private static async Task<CandidateBoundObservation> ProfileCandidateBoundCaseAsync(
        IReadOnlyList<string> paths)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"vulgata-unknown-audit-bound-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var dbOptions = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var db = new VulgataDbContext(dbOptions);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "unknown-audit-bound",
                "https://example.invalid/unknown-audit-bound.git",
                null,
                null,
                DateTimeOffset.UtcNow);
            db.Repositories.Add(repository);
            await db.SaveChangesAsync();
            var provider = new CountingFailureProvider();
            var detector = new CountingFrameworkDetector();
            var coordinator = CreateCoordinatorWithDetector(
                db,
                root,
                provider,
                new FixedCandidates(
                [
                    .. paths.Select((path, index) =>
                        new ScanSourceCandidate(path, false, "Eligible", index)),
                ]),
                detector);

            RepositoryProfilingResult result =
                await coordinator.StartStandaloneRepositoryAsync(
                    repository.Id,
                    null!,
                    CancellationToken.None);
            ScanRun run = await db.ScanRuns.SingleAsync();
            return new CandidateBoundObservation(
                result.Outcome,
                result.Message,
                provider.InvocationCount,
                detector.DetectionCount,
                run.Status,
                run.TotalCandidateFileCount,
                await db.ScanCandidateOutcomes.CountAsync(),
                await db.CodeUnits.CountAsync(),
                await db.CodeUnitEdges.CountAsync(),
                await db.UnresolvedStructuralRelationshipCandidates.CountAsync(),
                await db.CodeUnitFrameworks.CountAsync(),
                await db.ScanRunLanguageSummaries.CountAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static RepositoryProfilingCoordinator CreateCoordinatorWithDetector(
        VulgataDbContext db,
        string root,
        ICodeGraphProvider provider,
        IScanCandidateEnumerator candidates,
        IFrameworkEvidenceDetector detector)
    {
        object[] services =
        [
            new SystemRepository(db),
            new RepositoryRepository(db),
            new ScanRunRepository(db),
            new SuccessfulWorkingCopy(root),
            candidates,
            provider,
            new CodeGraphOptions(Path.Combine(root, "state"), 1_800),
            NullLogger<RepositoryProfilingCoordinator>.Instance,
            detector,
            FrameworkCatalog.LoadEmbedded(),
        ];
        ConstructorInfo? constructor = typeof(RepositoryProfilingCoordinator)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(candidate =>
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Length == services.Length &&
                    parameters.Zip(services).All(pair =>
                        pair.First.ParameterType.IsInstanceOfType(pair.Second));
            });
        Assert.NotNull(constructor);
        return Assert.IsType<RepositoryProfilingCoordinator>(
            constructor!.Invoke(services));
    }

    private sealed record CandidateBoundObservation(
        RepositoryProfilingOutcome Outcome,
        string? Message,
        int ProviderInvocationCount,
        int DetectorInvocationCount,
        ScanRunStatus ScanRunStatus,
        int TotalCandidateFileCount,
        int CandidateOutcomeCount,
        int CodeUnitCount,
        int EdgeCount,
        int UnresolvedRelationshipCount,
        int FrameworkFactCount,
        int LanguageSummaryCount);
}
