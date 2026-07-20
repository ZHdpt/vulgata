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

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                coordinator.StartStandaloneRepositoryAsync(
                    repository.Id,
                    null!,
                    CancellationToken.None));

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
}
