using System.Reflection;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared.Scans;
using Vulgata.Tests.PreparedScanning.TestDoubles;
using Vulgata.Web.Data;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// Story 12.2 T043 RED slice for the management-only paired preparation API.
/// The suite discovers the shared DTO and endpoint seams without duplicating
/// the Web application's authentication or persistence implementation.
/// </summary>
public sealed class PreparedScanApiTests
{
    private const string PromptIdentity = "prepared-scan-prompt/story122-v1";
    private const string DocumentSchemaIdentity = "code-logic/file-v2";
    private const string RuntimeIdentityJson = "{\"runtime\":\"prepared-api-test\"}";
    private static readonly string RuntimeIdentityHash = new('9', 64);

    [Fact]
    public void PreparedScanRequest_RequiresExactlyTwoRepositoryRevisionBindings()
    {
        Type request = RequireSharedType(
            type => type.Name.Contains("PreparedScan", StringComparison.Ordinal) &&
                type.Name.Contains("Request", StringComparison.Ordinal));
        PropertyInfo repositories = request.GetProperty("Repositories") ??
            throw new InvalidOperationException(
                "Prepared scan request must expose exactly two repository bindings.");
        Assert.True(
            typeof(System.Collections.IEnumerable).IsAssignableFrom(repositories.PropertyType),
            "Prepared scan request repository bindings must be a collection.");

        Type element = repositories.PropertyType.IsArray
            ? repositories.PropertyType.GetElementType()!
            : repositories.PropertyType.GetGenericArguments().Single();
        Assert.Contains(element.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == "RepositoryId");
        Assert.Contains(element.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == "GitRef");
        Assert.Contains(element.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == "ProfiledRevision");
        Assert.DoesNotContain(element.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Runtime", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Timeout", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreparedScanEndpoints_ExposeManagementPostRouteAndCoordinatorSeam()
    {
        Type endpoints = typeof(Program).Assembly.GetType(
            "Vulgata.Web.Api.Scans.PreparedScanEndpoints",
            throwOnError: false,
            ignoreCase: false) ?? throw new InvalidOperationException(
                "PreparedScanEndpoints public seam is missing.");
        Assert.Contains(
            endpoints.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name.Contains("MapPreparedScan", StringComparison.Ordinal));

        Type coordinator = typeof(Program).Assembly.GetType(
            "Vulgata.Web.Data.PreparedScanCoordinator",
            throwOnError: false,
            ignoreCase: false) ?? throw new InvalidOperationException(
                "PreparedScanCoordinator public seam is missing.");
        Assert.Contains(
            coordinator.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Prepare", StringComparison.Ordinal) ||
                method.Name.Contains("Start", StringComparison.Ordinal));
    }

    [Fact]
    public void PreparedScanResponse_UsesNamedOrdinalScopesAndStableIdentityFields()
    {
        Type response = RequireSharedType(
            type => type.Name.Contains("PreparedScan", StringComparison.Ordinal) &&
                type.Name.Contains("Response", StringComparison.Ordinal));
        PropertyInfo[] properties = response.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        string[] required =
        [
            "CoordinationId", "PairSelectionFingerprint", "PairPackageFingerprint",
            "SelectedCount",
        ];
        Assert.All(required, name => Assert.Contains(properties, property => property.Name == name));
        Assert.Contains(properties, property => property.Name == "Manifests" ||
            property.Name == "Repositories");

        Assert.DoesNotContain(
            properties,
            property => property.Name.Contains("Source", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Absolute", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Duration", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Telemetry", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Document", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreparedScanApi_DeclaresClosedProblemDetailsStatusMappingWithoutWorkerDispatch()
    {
        Type endpoints = typeof(Program).Assembly.GetType(
            "Vulgata.Web.Api.Scans.PreparedScanEndpoints",
            throwOnError: false,
            ignoreCase: false) ?? throw new InvalidOperationException(
                "PreparedScanEndpoints public seam is missing.");
        Assert.Contains(
            endpoints.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic),
            nested => nested.Name.Contains("Problem", StringComparison.OrdinalIgnoreCase) ||
                nested.Name.Contains("Response", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            endpoints.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance),
                method => method.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Document", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreparedScanSeams_RequireImmutableInitiationTuple()
    {
        MethodInfo[] buildMethods = typeof(IPreparedScanPairBuilder).GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Where(method => method.Name == nameof(IPreparedScanPairBuilder.BuildAsync))
            .ToArray();
        Assert.NotEmpty(buildMethods);
        Assert.All(buildMethods, method =>
        {
            ParameterInfo? firstParameter = method.GetParameters().FirstOrDefault();
            Assert.NotNull(firstParameter);
            Assert.Equal(typeof(PreparedScanInitiationTuple), firstParameter!.ParameterType);
        });
        Assert.DoesNotContain(
            buildMethods,
            method => method.GetParameters().FirstOrDefault()?.ParameterType == typeof(Guid));

        MethodInfo[] coordinatorPreparationMethods = typeof(PreparedScanCoordinator).GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Where(method =>
                method.GetParameters().Any(parameter => parameter.ParameterType == typeof(PreparedScanRequest)) &&
                method.GetParameters().Any(parameter => parameter.ParameterType == typeof(IUserContext)))
            .ToArray();
        Assert.NotEmpty(coordinatorPreparationMethods);
        Assert.DoesNotContain(
            coordinatorPreparationMethods,
            method => !method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(PreparedScanInitiationTuple)));
    }

    [Fact]
    public void PreparedScanApi_SemanticOwnerIsServerOwnedAndInputCarriesRequiredFqn()
    {
        Type request = RequireSharedType(
            type => type.Name.Contains("PreparedScan", StringComparison.Ordinal) &&
                type.Name.Contains("Request", StringComparison.Ordinal));
        Assert.DoesNotContain(
            request.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("Symbol", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Owner", StringComparison.OrdinalIgnoreCase));

        ConstructorInfo[] inputConstructors = typeof(PreparedFileCodeUnitInput)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(
            inputConstructors.SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.Name == "symbolName" &&
                parameter.ParameterType == typeof(string) &&
                !parameter.IsOptional);
    }

    [Fact]
    public async Task PreparedScanPairBuilder_ProductionCompositionBuildsNeutralExactTwoPackageAsync()
    {
        int revisionAcquisitionCalls = 0;
        int snapshotReads = 0;
        int catalogCalls = 0;
        int codeGraphCalls = 0;
        int javaReadinessCalls = 0;
        StubPreparedRevisionAcquirer revisionAcquirer = new();
        revisionAcquirer.Handler = (request, _) =>
        {
            revisionAcquisitionCalls++;
            return Task.FromResult(new PreparedRevisionAcquisitionResult(
                new PreparedRevisionSnapshot(
                    request.RepositoryId,
                    request.ScanRunId,
                    request.RequestedRef,
                    request.ProfiledRevision,
                    new('a', 64),
                    $"snapshot-{request.RepositoryId:N}",
                    $"root-{request.RepositoryId:N}"),
                null));
        };

        StubPreparedSnapshotSource snapshotSource = new();
        snapshotSource.Handler = (snapshot, _) =>
        {
            snapshotReads++;
            return Task.FromResult(new PreparedSnapshotReadResult(
                CreateNeutralSnapshotFiles(snapshot),
                null));
        };
        StubReadyAnalysisRuntimeHealthProbe runtimeProbe = new();

        StubPreparedCodeGraphAnalysisPort codeGraph = new();
        codeGraph.ProofHandler = (request, _) =>
        {
            codeGraphCalls++;
            return Task.FromResult(new PreparedCodeGraphProofResult(
                AnalysisOperationStatus.Succeeded,
                new CodeGraphCompletenessProof(
                    request.Scope.RepositoryId,
                    request.Scope.ScanRunId,
                    request.Scope.ProfiledRevision,
                    request.SnapshotIdentity,
                    request.SnapshotRootBindingHash,
                    request.Scope.RuntimeIdentityHash,
                    "prepared-api-index",
                    "codegraph-completeness-proof/1.0",
                    true,
                    CodeGraphIndexState.Complete,
                    0,
                    0,
                    0,
                    false,
                    false,
                    0,
                    64,
                    request.LogicalProjectBinding,
                    request.LogicalIndexBinding,
                    true,
                    true,
                    true,
                    true,
                    true,
                    true,
                    true,
                    new('b', 64),
                    0),
                null));
        };

        StubPreparedJavaReadinessPort javaReadiness = new();
        javaReadiness.Handler = (request, _) =>
        {
            javaReadinessCalls++;
            return Task.FromResult(new PreparedJavaReadinessResult(
                AnalysisOperationStatus.Succeeded,
                new JavaReadinessProof(
                    request.Scope.RepositoryId,
                    request.Scope.ScanRunId,
                    request.Scope.ProfiledRevision,
                    request.SnapshotIdentity,
                    request.Scope.RuntimeIdentityHash,
                    request.WorkspaceBindingHash,
                    "java-readiness/1.0",
                    JavaReadinessOutcome.NotApplicable,
                    "jdt-language-server-1.61.0-202608141332",
                    new('c', 64),
                    21,
                    "2.25.29",
                    AnalysisTransport.Stdio,
                    false,
                    null,
                    false,
                    false,
                    JavaImportState.NotApplicable,
                    0,
                    true,
                    true,
                    true,
                    true,
                    true,
                    new('d', 64),
                    0),
                null));
        };

        StubPreparedCommunicationCatalogSource catalog = new();
        catalog.Handler = _ =>
        {
            catalogCalls++;
            return Task.FromResult(new PreparedCatalogLoadResult(
                new CommunicationPatternCatalog(
                    "communication-pattern-catalog/1.0",
                    "prepared-api-catalog",
                    "1",
                    new('e', 64),
                    []),
                null));
        };

        using LoginLogoutTests.CustomWebApplicationFactory baseFactory = new();
        using WebApplicationFactory<Program> factory = baseFactory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPreparedRevisionAcquirer>();
                services.AddSingleton<IPreparedRevisionAcquirer>(revisionAcquirer);
                services.RemoveAll<IPreparedSnapshotSource>();
                services.AddSingleton<IPreparedSnapshotSource>(snapshotSource);
                services.RemoveAll<IAnalysisRuntimeHealthProbe>();
                services.AddSingleton<IAnalysisRuntimeHealthProbe>(runtimeProbe);
                services.RemoveAll<IPreparedCommunicationCatalogSource>();
                services.AddSingleton<IPreparedCommunicationCatalogSource>(catalog);
                services.RemoveAll<IPreparedCodeGraphAnalysisPort>();
                services.AddSingleton<IPreparedCodeGraphAnalysisPort>(codeGraph);
                services.RemoveAll<IPreparedJavaReadinessPort>();
                services.AddSingleton<IPreparedJavaReadinessPort>(javaReadiness);
            }));

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        VulgataDbContext db = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        (SystemEntity system, RepositoryEntity first, RepositoryEntity second,
            PreparedScanRepositoryRevisionBinding[] bindings) = await SeedExactTwoBindingsAsync(db);
        Assert.Equal(2, await db.Repositories.CountAsync(repository => repository.SystemId == system.Id));
        Assert.Equal(2, await db.ScanRuns.CountAsync(run =>
            run.RepositoryId == first.Id || run.RepositoryId == second.Id));
        Assert.Equal(1, await db.TwoRepositoryScanCoordinations.CountAsync(coordination =>
            coordination.SystemId == system.Id));
        IPreparedScanPairBuilder builder = scope.ServiceProvider
            .GetRequiredService<IPreparedScanPairBuilder>();
        PreparedScanInitiationTuple tuple = CreateInitiationTuple(db, system, [first, second], bindings);

        PreparedScanPairBuildResult result = await builder.BuildAsync(
            tuple,
            [first, second],
            bindings,
            CancellationToken.None);

        Assert.True(
            revisionAcquisitionCalls > 0,
            "Production pair composition must invoke IPreparedRevisionAcquirer for each persisted revision.");
        Assert.Equal(2, revisionAcquisitionCalls);
        Assert.Equal(2, snapshotReads);
        Assert.Equal(1, catalogCalls);
        Assert.Equal(2, codeGraphCalls);
        Assert.Equal(2, javaReadinessCalls);
        Assert.True(
            catalogCalls > 0,
            "Production pair composition must invoke IPreparedCommunicationCatalogSource.");
        Assert.True(
            codeGraphCalls > 0,
            "Production pair composition must invoke IPreparedCodeGraphAnalysisPort.");
        Assert.True(
            javaReadinessCalls > 0,
            "Production pair composition must invoke IPreparedJavaReadinessPort.");
        Assert.NotEqual("prepared_scan_pipeline_unavailable", result.Failure?.FailureCode);
        Assert.NotNull(result.PairPackage);
        Assert.Null(result.Failure);
        Assert.Equal(2, result.PairPackage.Repositories.Count);
        Assert.InRange(result.PairPackage.SelectedCount, 50, 150);
    }

    private static IReadOnlyList<PreparedSnapshotFile> CreateNeutralSnapshotFiles(
        PreparedRevisionSnapshot snapshot)
    {
        List<PreparedSnapshotFile> files = new(capacity: 60);
        for (int index = 0; index < 60; index++)
        {
            bool java = index % 2 == 0;
            string relativePath = java
                ? $"src/neutral/Entry-{index:00}.java"
                : $"src/neutral/config-{index:00}.xml";
            string source = java
                ? $"package neutral;\npublic final class Entry{index:00} {{\n    public static final int VALUE = {index};\n}}\n"
                : $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<configuration><entry name=\"neutral-{index:00}\" value=\"{snapshot.RepositoryId:N}\" /></configuration>\n";
            byte[] rawBytes = System.Text.Encoding.UTF8.GetBytes(source);
            string hash = Convert.ToHexStringLower(SHA256.HashData(rawBytes));
            files.Add(new PreparedSnapshotFile(
                relativePath,
                java ? "Java" : "XML",
                IsEligible: true,
                "EligibleProductionSource",
                hash,
                hash,
                rawBytes.LongLength,
                source.Count(character => character == '\n'),
                "utf-8",
                rawBytes));
        }

        return files;
    }

    private static async Task<(
        SystemEntity System,
        RepositoryEntity First,
        RepositoryEntity Second,
        PreparedScanRepositoryRevisionBinding[] Bindings)> SeedExactTwoBindingsAsync(
        VulgataDbContext db)
    {
        DateTimeOffset now = PreparedScanTestHost.FixedUtcNow;
        const string firstRevision = "0123456789abcdef0123456789abcdef01234567";
        const string secondRevision = "fedcba9876543210fedcba9876543210fedcba98";
        SystemEntity system = new(
            "Prepared API composition system",
            "persisted exact-two fixture",
            "prepared API global context",
            now);
        RepositoryEntity first = system.AddRepository(
            "prepared-api-one",
            "https://example.invalid/prepared-api-one.git",
            null,
            "repository one context",
            now);
        RepositoryEntity second = system.AddRepository(
            "prepared-api-two",
            "https://example.invalid/prepared-api-two.git",
            null,
            "repository two context",
            now);
        ScanRun firstRun = ScanRun.Start(first.Id, 1_800, now);
        firstRun.BindProfiledRevision(firstRevision);
        ScanRun secondRun = ScanRun.Start(second.Id, 1_800, now);
        secondRun.BindProfiledRevision(secondRevision);
        ScanContextSnapshot firstSnapshot = ScanContextSnapshot.CapturePrepared(
            firstRun,
            "prepared API global context",
            "prepared API system context",
            first.Context,
            now);
        ScanContextSnapshot secondSnapshot = ScanContextSnapshot.CapturePrepared(
            secondRun,
            "prepared API global context",
            "prepared API system context",
            second.Context,
            now);
        PreScanResult firstPreScan = PreScanResult.CreateApproved(
            first.Id,
            firstRun.Id,
            firstRevision,
            now,
            new('f', 64),
            "预扫描已批准——继续进入顺序演示运行器",
            ConfirmedCommunicationKinds.None,
            0,
            0,
            0,
            false,
            []);
        PreScanResult secondPreScan = PreScanResult.CreateApproved(
            second.Id,
            secondRun.Id,
            secondRevision,
            now,
            new('1', 64),
            "预扫描已批准——继续进入顺序演示运行器",
            ConfirmedCommunicationKinds.None,
            0,
            0,
            0,
            false,
            []);
        ScanExecution firstExecution = ScanExecution.CreateNewPath(
            Guid.NewGuid(), first.Id, system.Id, firstRun.Id, firstRevision,
            PromptIdentity, DocumentSchemaIdentity, RuntimeIdentityJson, RuntimeIdentityHash, now);
        ScanExecution secondExecution = ScanExecution.CreateNewPath(
            Guid.NewGuid(), second.Id, system.Id, secondRun.Id, secondRevision,
            PromptIdentity, DocumentSchemaIdentity, RuntimeIdentityJson, RuntimeIdentityHash, now);
        TwoRepositoryScanCoordination coordination = TwoRepositoryScanCoordination.Create(
            firstExecution,
            secondExecution,
            now);

        db.Systems.Add(system);
        db.ScanRuns.AddRange(firstRun, secondRun);
        db.ScanContextSnapshots.AddRange(firstSnapshot, secondSnapshot);
        db.PreScanResults.AddRange(firstPreScan, secondPreScan);
        db.ScanExecutions.AddRange(firstExecution, secondExecution);
        db.TwoRepositoryScanCoordinations.Add(coordination);
        await db.SaveChangesAsync();

        return (
            system,
            first,
            second,
            [
                new() { RepositoryId = first.Id, GitRef = "refs/tags/release-a", ProfiledRevision = firstRevision },
                new() { RepositoryId = second.Id, GitRef = "refs/tags/release-b", ProfiledRevision = secondRevision },
            ]);
    }

    private static PreparedScanInitiationTuple CreateInitiationTuple(
        VulgataDbContext db,
        SystemEntity system,
        IReadOnlyList<RepositoryEntity> repositories,
        IReadOnlyList<PreparedScanRepositoryRevisionBinding> bindings)
    {
        TwoRepositoryScanCoordination coordination = db.TwoRepositoryScanCoordinations
            .Single(value => value.SystemId == system.Id);
        PreparedScanInitiationRepositoryTuple[] tupleRepositories = repositories
            .Select(repository =>
            {
                PreparedScanRepositoryRevisionBinding binding = bindings.Single(
                    value => value.RepositoryId == repository.Id);
                ScanExecution execution = db.ScanExecutions.Single(
                    value => value.RepositoryId == repository.Id);
                ScanRun run = db.ScanRuns.Single(value => value.Id == execution.ScanRunId);
                return new PreparedScanInitiationRepositoryTuple(
                    repository.Id,
                    binding.GitRef,
                    binding.ProfiledRevision,
                    run.Id,
                    execution.Id,
                    execution.Phase,
                    execution.PromptIdentity!,
                    execution.DocumentSchemaIdentity!,
                    execution.AnalysisRuntimeIdentityJson!,
                    execution.AnalysisRuntimeIdentityHash!,
                    execution.PreScanResultId);
            })
            .ToArray();
        return new(
            system.Id,
            new string('a', 64),
            coordination.CreatedAt,
            coordination.Id,
            coordination.StateVersion,
            coordination.Phase,
            PreparedScanArtifactPresence.None,
            tupleRepositories);
    }

    private static Type RequireSharedType(Func<Type, bool> predicate)
    {
        Type? type = typeof(Vulgata.Shared.RoleNames).Assembly.GetTypes()
            .Where(candidate => candidate.IsPublic)
            .FirstOrDefault(predicate);
        Assert.NotNull(type);
        return type!;
    }
}
