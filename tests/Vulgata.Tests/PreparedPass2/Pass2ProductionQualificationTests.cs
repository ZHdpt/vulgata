using Vulgata.Tests.TestInfrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Vulgata.Agents;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure;
using Vulgata.Shared;
using Vulgata.Shared.Scans;
using Vulgata.Tests.PreparedPass1;
using Vulgata.Tests.PreparedScanning;

namespace Vulgata.Tests.PreparedPass2;

[Trait("Category", "Story12_4_ProductionQualification")]
public sealed class Pass2ProductionQualificationTests
{
    internal const string OptInVariable = "VULGATA_T124_RUN_PRODUCTION_QUALIFICATION";
    internal const string ProviderContentVariable = "VULGATA_T124_ALLOW_PROVIDER_CONTENT";
    internal const string ProvisionExactHandoffVariable = "VULGATA_T124_PROVISION_EXACT_HANDOFF";
    private const string _administratorVariable = "VULGATA_T124_POSTGRES_ADMIN";
    private const string _sourceConnectionVariable = "VULGATA_T124_SOURCE_CONNECTION";

    [Trait("Suite", "External")]
    [SuiteFact("External")]
    public void Qualification_IsExplicitlyOptIn_AndDoesNotChangeDefaultProviderBehavior()
    {
        string? value = Environment.GetEnvironmentVariable(OptInVariable);
        Assert.True(value is null || value is "0" or "1" or "false" or "true",
            $"{OptInVariable} must be an explicit boolean switch when supplied.");
        string? providerContent = Environment.GetEnvironmentVariable(ProviderContentVariable);
        Assert.True(providerContent is null || providerContent is "0" or "1" or "false" or "true",
            $"{ProviderContentVariable} must be an explicit boolean switch when supplied.");
        string? provision = Environment.GetEnvironmentVariable(ProvisionExactHandoffVariable);
        Assert.True(provision is null || provision is "0" or "1" or "false" or "true",
            $"{ProvisionExactHandoffVariable} must be an explicit boolean switch when supplied.");
    }

    [Trait("Suite", "External")]
    [T048ProductionQualificationFact]
    public async Task ApprovedTwoRepositoryPair_ProducesSanitizedSourceGroundedBusinessDocumentAsync()
    {
        RequireOptIn();
        await using QualificationDatabase database = await QualificationDatabase.CloneAsync(
            Required(_administratorVariable), Required(_sourceConnectionVariable));
        database.RetainSuccessfulResult();
        string? previousConnection = Environment.GetEnvironmentVariable(
            "VULGATA_T062_QUALIFICATION_CONNECTION");
        Environment.SetEnvironmentVariable("VULGATA_T062_QUALIFICATION_CONNECTION", database.ConnectionString);
        try
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(QualificationTimeout());
            T062QualificationInputs qualification = IsTrue(Environment.GetEnvironmentVariable(
                    ProvisionExactHandoffVariable))
                ? await Story124ExactHandoffProvisioner.ProvisionAsync(database.ConnectionString)
                : await LoadQualificationInputsAsync(database.ConnectionString);
            PreparedPass1ExecutionDiagnosticObserver executionDiagnostics = new();
            PreparedPass1WireBudgetDiagnosticObserver wireDiagnostics = new();
            PreparedPass1HostedErrorObserver hostedErrors = new();
            var providerHttpDiagnostics = new Story124ProviderHttpDiagnosticHandler();
            using WebApplicationFactory<Program> host = Pass1ProductionExecutionTests
                .CreateProductionHost(database.ConnectionString)
                .WithWebHostBuilder(builder =>
                {
                    builder.UseSetting(
                        "Logging:LogLevel:Vulgata.Web.Data.PreparedPass1ExecutionHostedService",
                        "Error");
                    builder.ConfigureTestServices(services =>
                    {
                        services.RemoveAll<IPreparedPass1ExecutionDiagnosticObserver>();
                        services.RemoveAll<PreparedPass1ExecutionDiagnosticObserver>();
                        services.AddSingleton(executionDiagnostics);
                        services.AddSingleton<IPreparedPass1ExecutionDiagnosticObserver>(executionDiagnostics);
                        services.RemoveAll<IPreparedPass1WireBudgetDiagnosticObserver>();
                        services.RemoveAll<PreparedPass1WireBudgetDiagnosticObserver>();
                        services.AddSingleton(wireDiagnostics);
                        services.AddSingleton<IPreparedPass1WireBudgetDiagnosticObserver>(wireDiagnostics);
                        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(hostedErrors);
                        services.RemoveAll<HttpMessageHandler>();
                        services.AddSingleton<HttpMessageHandler>(providerHttpDiagnostics);
                        services.RemoveAll<IPreparedPass2StreamingTransport>();
                        services.AddScoped<IPreparedPass2StreamingTransport>(provider =>
                            new Story124DiagnosticPass2Transport(
                                new OpenAiCompatiblePass2StreamingTransport(
                                    provider.GetRequiredService<WorkerAgentProviderResolver>(),
                                    provider.GetRequiredService<ISecretEncryptionService>(),
                                    provider.GetRequiredService<HttpMessageHandler>()),
                                providerHttpDiagnostics));
                        services.RemoveAll<IPreparedPass2WorkerDispatcher>();
                        services.AddScoped<IPreparedPass2WorkerDispatcher>(provider =>
                            new Story124DiagnosticPass2Dispatcher(
                                provider.GetRequiredService<PreparedPass2WorkerDispatcher>(),
                                providerHttpDiagnostics));
                        services.RemoveAll<IPass2BusinessLogicDocumentCommitter>();
                        services.AddScoped<AuthorizedPass2BusinessLogicDocumentCommitter>();
                        services.AddScoped<IPass2BusinessLogicDocumentCommitter>(provider =>
                            new Story124DiagnosticCommitter(
                                provider.GetRequiredService<AuthorizedPass2BusinessLogicDocumentCommitter>(),
                                providerHttpDiagnostics));
                        if (IsTrue(Environment.GetEnvironmentVariable(ProvisionExactHandoffVariable)))
                        {
                            services.RemoveAll<IRevisionBoundSourceReader>();
                            services.AddSingleton<IRevisionBoundSourceReader>(
                                new Story124QualificationSourceReader(qualification.Repositories));
                        }
                    });
                });
            await Pass1ProductionExecutionTests.ConfigureWorkerProviderAsync(
                host, qualification.SystemId, qualification.ProviderEndpoint, qualification.ProviderModel);
            await AssertProviderAvailableAsync(host, qualification.SystemId);
            using HttpClient client = await Pass1ProductionExecutionTests.CreateAuthenticatedClientAsync(
                host, qualification.SystemId);

            Guid pairId;
            Pass1PairExecutionPhase pairPhase;
            await using (Vulgata.Infrastructure.Data.VulgataDbContext discovery =
                PreparedScanPostgreSqlFixture.CreateContext(database.ConnectionString))
            {
                Guid[] manifestIds = qualification.ManifestIds.ToArray();
                var pair = await discovery.Pass1PairExecutions.AsNoTracking()
                    .Where(value => value.SystemId == qualification.SystemId &&
                        ((value.ManifestOneId == manifestIds[0] && value.ManifestTwoId == manifestIds[1]) ||
                         (value.ManifestOneId == manifestIds[1] && value.ManifestTwoId == manifestIds[0])))
                    .OrderByDescending(value => value.Id)
                    .Select(value => new { value.Id, value.Phase })
                    .FirstOrDefaultAsync();
                Assert.NotNull(pair);
                pairId = pair!.Id;
                pairPhase = pair.Phase;
                Assert.NotEqual(Guid.Empty, pairId);
                Assert.False(await discovery.Pass2Executions.AsNoTracking().AnyAsync(
                    value => value.Pass1PairExecutionId == pairId),
                    "T048 requires a fresh disposable Pass 2 boundary, not replayed qualification state.");
            }

            if (pairPhase != Pass1PairExecutionPhase.ReadyForResolution)
            {
                Assert.Contains(pairPhase, new[]
                {
                    Pass1PairExecutionPhase.Running,
                    Pass1PairExecutionPhase.Reconciling,
                    Pass1PairExecutionPhase.PausedProviderUnavailable,
                    Pass1PairExecutionPhase.PausedOperatorCorrection,
                    Pass1PairExecutionPhase.PausedInspection,
                });
                Pass1ExecutionStatusResponse current = await ReadPass1StatusAsync(client, pairId);
                if (current.ConcurrencyLimit != 4)
                {
                    using HttpResponseMessage concurrencyResponse = await client.PatchAsJsonAsync(
                        $"/api/pass1-executions/{pairId:D}/concurrency",
                        new UpdatePass1ConcurrencyRequest(current.StateVersion, 4));
                    string concurrencyBody = await concurrencyResponse.Content.ReadAsStringAsync();
                    Assert.True(concurrencyResponse.StatusCode == HttpStatusCode.OK,
                        $"T048 prerequisite Pass 1 concurrency update failed closed with HTTP " +
                        $"{(int)concurrencyResponse.StatusCode}; response_hash={Hash(concurrencyBody)}.");
                }
                using var resumeRequest = new HttpRequestMessage(
                    HttpMethod.Post, $"/api/pass1-executions/{pairId:D}/resume");
                using HttpResponseMessage resumeResponse = await client.SendAsync(resumeRequest);
                string resumeBody = await resumeResponse.Content.ReadAsStringAsync();
                Assert.True(resumeResponse.StatusCode == HttpStatusCode.OK,
                    $"T048 prerequisite Pass 1 resume failed closed with HTTP " +
                    $"{(int)resumeResponse.StatusCode}; response_hash={Hash(resumeBody)}.");
                Pass1ExecutionStatusResponse pass1Terminal = await WaitForPass1TerminalAsync(
                    client, pairId, Remaining(deadline), hostedErrors);
                Assert.True(
                    string.Equals(pass1Terminal.Phase, "ReadyForResolution", StringComparison.Ordinal),
                    $"T048 prerequisite Pass 1 stopped at {pass1Terminal.Phase}; " +
                    $"execution={Closed(executionDiagnostics.Categories)}; " +
                    $"functionCallSubtype={executionDiagnostics.FunctionCallSubtype?.ToString() ?? "none"}; " +
                    $"wire={Closed(wireDiagnostics.Categories)}; " +
                    $"outgoingRequestRejectionKind={wireDiagnostics.OutgoingRequestRejectionKind?.ToString() ?? "none"}; " +
                    $"warnings={Closed(pass1Terminal.Warnings.Select(value => $"{value.FailureCategory}:{value.FailureCode}"))}.");
                Assert.Equal(pass1Terminal.DispatchedCount,
                    pass1Terminal.CompletedCount + pass1Terminal.FailedCount);
                Assert.InRange(pass1Terminal.FailedCount, 0,
                    Math.Min(5, pass1Terminal.DispatchedCount / 10));
            }
            await using (Vulgata.Infrastructure.Data.VulgataDbContext ready =
                PreparedScanPostgreSqlFixture.CreateContext(database.ConnectionString))
            {
                Pass1Outcome outcome = await ready.Pass1Outcomes.AsNoTracking()
                    .SingleAsync(value => value.PairExecutionId == pairId);
                Assert.Contains(outcome.Status, new[]
                {
                    Pass1OutcomeStatus.Succeeded,
                    Pass1OutcomeStatus.CompletedWithWarnings,
                });
                Assert.Equal(outcome.DispatchedCount,
                    outcome.CompletedCount + outcome.OrdinaryFailureCount);
            }

            using HttpResponseMessage startResponse = await client.PostAsJsonAsync(
                "/api/pass2-executions", new StartPass2ExecutionRequest(pairId));
            string startBody = await startResponse.Content.ReadAsStringAsync();
            Assert.True(startResponse.StatusCode == HttpStatusCode.Created,
                $"T048 Pass 2 Start failed closed with HTTP {(int)startResponse.StatusCode}; " +
                $"response_hash={Hash(startBody)}.");
            Pass2ExecutionStartResponse started = JsonSerializer.Deserialize<Pass2ExecutionStartResponse>(
                startBody, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new Xunit.Sdk.XunitException("T048 Pass 2 Start returned no typed response.");
            Assert.False(started.IsReplay);
            Assert.True(started.JobCount > 0);

            Pass2ExecutionStatusResponse terminal = await WaitForTerminalAsync(
                client, started.Pass2ExecutionId, Remaining(deadline), providerHttpDiagnostics);
            Assert.Contains(terminal.State, new[] { "Completed", "CompletedWithWarnings" });
            QualificationEvidence evidence = await VerifyAndCreateEvidenceAsync(
                database, qualification, started, terminal, providerHttpDiagnostics);
            await WriteEvidenceAsync(evidence);
            database.RetainSuccessfulResult();
        }
        catch (Exception exception)
        {
            await WriteEvidenceAsync(QualificationEvidence.Failed(database, ClassifyFailure(exception)));
            throw;
        }
        finally
        {
            Environment.SetEnvironmentVariable("VULGATA_T062_QUALIFICATION_CONNECTION", previousConnection);
        }
    }

    private static async Task<Pass1ExecutionStatusResponse> ReadPass1StatusAsync(
        HttpClient client, Guid executionId)
    {
        using HttpResponseMessage response = await client.GetAsync(
            $"/api/pass1-executions/{executionId:D}");
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"T048 prerequisite Pass 1 status failed closed with HTTP " +
            $"{(int)response.StatusCode}; response_hash={Hash(body)}.");
        return JsonSerializer.Deserialize<Pass1ExecutionStatusResponse>(
            body, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new Xunit.Sdk.XunitException(
                "T048 prerequisite Pass 1 status returned no typed response.");
    }

    private static async Task<Pass1ExecutionStatusResponse> WaitForPass1TerminalAsync(
        HttpClient client, Guid executionId, TimeSpan timeout,
        PreparedPass1HostedErrorObserver hostedErrors)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        Pass1ExecutionStatusResponse? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await client.GetAsync(
                $"/api/pass1-executions/{executionId:D}");
            string body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"T048 prerequisite Pass 1 status failed closed with HTTP " +
                $"{(int)response.StatusCode}; response_hash={Hash(body)}.");
            last = JsonSerializer.Deserialize<Pass1ExecutionStatusResponse>(
                body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(last);
            if (!hostedErrors.Errors.IsEmpty)
            {
                throw new Xunit.Sdk.XunitException(
                    $"T048 prerequisite Pass 1 hosted runner stopped unexpectedly; " +
                    $"errors={Closed(hostedErrors.Errors)}.");
            }
            if (last!.Phase is "ReadyForResolution" or "FailedPass1" or "FailedIntegrity" or
                "FailedSecurity" or "Cancelled" or "PausedProviderUnavailable" or
                "PausedOperatorCorrection" or "PausedInspection")
                return last;
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        throw new TimeoutException(
            $"T048 prerequisite Pass 1 did not reach terminal; last_phase={last?.Phase ?? "unavailable"}.");
    }

    private static TimeSpan Remaining(DateTimeOffset deadline)
    {
        TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
        return remaining > TimeSpan.Zero
            ? remaining
            : throw new TimeoutException("T048 exhausted the approved qualification cap.");
    }

    private static async Task<T062QualificationInputs> LoadQualificationInputsAsync(
        string connectionString)
    {
        Guid systemId = Guid.Parse(Required("VULGATA_T062_SYSTEM_ID"));
        Guid[] manifestIds =
        [
            Guid.Parse(Required("VULGATA_T062_MANIFEST_ONE_ID")),
            Guid.Parse(Required("VULGATA_T062_MANIFEST_TWO_ID")),
        ];
        string[] roots =
        [
            Required("VULGATA_T062_REPOSITORY_ONE_ROOT"),
            Required("VULGATA_T062_REPOSITORY_TWO_ROOT"),
        ];
        var providerEndpoint = new Uri(Required("VULGATA_T062_WORKER_PROVIDER_ENDPOINT"));
        string providerModel = Required("VULGATA_T062_WORKER_PROVIDER_MODEL");

        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            PreparedScanPostgreSqlFixture.CreateContext(connectionString);
        var manifests = await context.PreparedScanManifests.AsNoTracking()
            .Where(value => manifestIds.Contains(value.Id))
            .Select(value => new
            {
                value.Id,
                value.RepositoryId,
                value.ProfiledRevision,
                value.SchemaIdentity,
                value.ApprovalState,
                value.SelectedCount,
            })
            .ToArrayAsync();
        if (manifests.Length != 2)
        {
            string[] available = await context.PreparedScanManifests.AsNoTracking()
                .OrderByDescending(value => value.CreatedAt)
                .Select(value => value.Id.ToString() + "|" + value.RepositoryId.ToString() + "|" +
                    value.SelectedCount.ToString())
                .Take(20)
                .ToArrayAsync();
            bool systemExists = await context.Systems.AsNoTracking()
                .AnyAsync(value => value.Id == systemId);
            int repositoryCount = await context.Repositories.AsNoTracking().CountAsync();
            int scanRunCount = await context.ScanRuns.AsNoTracking().CountAsync();
            int pass1PairCount = await context.Pass1PairExecutions.AsNoTracking().CountAsync();
            int documentVersionCount = await context.DocumentVersions.AsNoTracking().CountAsync();
            throw new Xunit.Sdk.XunitException(
                $"T048 expected both approved manifests but found {manifests.Length}; " +
                $"system_exists={systemExists}; repositories={repositoryCount}; scan_runs={scanRunCount}; " +
                $"pass1_pairs={pass1PairCount}; document_versions={documentVersionCount}; " +
                $"available_manifest_inventory={string.Join(',', available)}");
        }
        Assert.Equal(2, manifests.Select(value => value.RepositoryId).Distinct().Count());
        Assert.All(manifests, value =>
        {
            Assert.Equal(PreparedScanManifestApprovalState.Approved, value.ApprovalState);
            Assert.Equal(PreparedScanManifest.CurrentSchemaIdentity, value.SchemaIdentity);
            Assert.InRange(value.SelectedCount, 0, 150);
            Assert.False(string.IsNullOrWhiteSpace(value.ProfiledRevision));
        });
        Assert.InRange(manifests.Sum(value => value.SelectedCount), 50, 150);

        Dictionary<Guid, string> rootsByManifest = manifestIds.Zip(roots)
            .ToDictionary(value => value.First, value => value.Second);
        T062RepositoryQualification[] repositories = manifests.Select(value =>
            new T062RepositoryQualification(
                value.Id, value.RepositoryId, rootsByManifest[value.Id], value.ProfiledRevision))
            .OrderBy(value => value.RepositoryId)
            .ToArray();
        var selectedFiles = await context.PreparedScanManifestEntries.AsNoTracking()
            .Where(value => manifestIds.Contains(value.PreparedScanManifestId))
            .Select(value => new
            {
                value.PreparedScanManifestId,
                value.FileCodeUnit!.FilePath,
                value.FileCodeUnit.SourceHash,
                value.FileCodeUnit.FileHash,
            })
            .ToArrayAsync();
        int exactJavaXmlCount = 0;
        foreach (var selected in selectedFiles)
        {
            T062RepositoryQualification repository = repositories.Single(
                value => value.ManifestId == selected.PreparedScanManifestId);
            string fullPath = Path.Combine(repository.SourceRoot,
                selected.FilePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath), $"T048 source byte prerequisite missing: {fullPath}");
            byte[] bytes = await File.ReadAllBytesAsync(fullPath);
            string sourceHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            Assert.Equal(sourceHash, selected.SourceHash);
            Assert.Equal(sourceHash, selected.FileHash);
            if (selected.FilePath.EndsWith(".java", StringComparison.OrdinalIgnoreCase) ||
                selected.FilePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                exactJavaXmlCount++;
        }
        Assert.True(exactJavaXmlCount >= 2,
            "T048 requires exact Java and XML source evidence in the approved pair.");
        return new(connectionString, systemId, manifestIds, repositories, providerEndpoint, providerModel);
    }

    private static async Task<QualificationEvidence> VerifyAndCreateEvidenceAsync(
        QualificationDatabase database,
        T062QualificationInputs qualification,
        Pass2ExecutionStartResponse started,
        Pass2ExecutionStatusResponse terminal,
        Story124ProviderHttpDiagnosticHandler providerHttpDiagnostics)
    {
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            PreparedScanPostgreSqlFixture.CreateContext(database.ConnectionString);
        FlowGroup[] groups = await context.FlowGroups.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == started.Pass2ExecutionId)
            .OrderBy(value => value.GroupOrder).ToArrayAsync();
        Pass2JobExecution[] jobs = await context.Pass2JobExecutions.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == started.Pass2ExecutionId)
            .OrderBy(value => value.GroupOrder).ToArrayAsync();
        Guid[] documentIds = groups.Where(value => value.BusinessLogicDocumentVersionId.HasValue)
            .Select(value => value.BusinessLogicDocumentVersionId!.Value).ToArray();
        Assert.True(documentIds.Length > 0,
            $"T048 published no Business Logic Document; terminal={terminal.State}; " +
            $"jobs={string.Join(',', jobs.Select(value => $"{value.State}:{value.AttemptCount}:" +
                $"{value.FailureCategory?.ToString() ?? "none"}:{value.FailureCode ?? "none"}"))}; " +
            $"provider={string.Join(',', providerHttpDiagnostics.Observations)}.");
        DocumentVersion[] documents = await context.DocumentVersions.AsNoTracking()
            .Where(value => documentIds.Contains(value.Id)).ToArrayAsync();
        Assert.Equal(documentIds.Length, documents.Length);
        Assert.All(documents, document =>
        {
            Assert.False(string.IsNullOrWhiteSpace(document.ContentFingerprint));
            Assert.True(Encoding.UTF8.GetByteCount(document.ContentMarkdown) > 0);
        });
        DocumentEdge[] provenance = await context.DocumentEdges.AsNoTracking()
            .Where(value => documentIds.Contains(value.SourceDocumentVersionId) &&
                value.EdgeType == DocumentEdgeType.GeneratedFrom).ToArrayAsync();
        Assert.NotEmpty(provenance);
        Guid[] usedDocumentIds = provenance.Select(value => value.TargetDocumentVersionId)
            .Distinct().ToArray();
        SourceAnchor[] anchors = await context.SourceAnchors.AsNoTracking()
            .Where(value => usedDocumentIds.Contains(value.DocumentVersionId)).ToArrayAsync();
        Assert.All(groups.Where(value => value.BusinessLogicDocumentVersionId.HasValue), group =>
        {
            Assert.NotNull(group.ExactResolutionDecisionId);
            Assert.NotNull(group.ExactResolutionEdgeId);
            Assert.Contains(provenance, value => value.SourceDocumentVersionId ==
                group.BusinessLogicDocumentVersionId && value.TargetDocumentVersionId ==
                group.ConsumerDocumentVersionId);
            Assert.Contains(provenance, value => value.SourceDocumentVersionId ==
                group.BusinessLogicDocumentVersionId && value.TargetDocumentVersionId ==
                group.ProviderXmlDocumentVersionId);
            Assert.Contains(anchors, value => value.DocumentVersionId == group.ConsumerDocumentVersionId);
            Assert.Contains(anchors, value => value.DocumentVersionId == group.ProviderXmlDocumentVersionId);
        });
        return QualificationEvidence.Succeeded(
            database, qualification, started, terminal, groups, jobs, documents, provenance, anchors);
    }

    private static async Task<Pass2ExecutionStatusResponse> WaitForTerminalAsync(
        HttpClient client,
        Guid executionId,
        TimeSpan timeout,
        Story124ProviderHttpDiagnosticHandler providerHttpDiagnostics)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await client.GetAsync($"/api/pass2-executions/{executionId:D}");
            string body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"T048 status failed closed with HTTP {(int)response.StatusCode}; response_hash={Hash(body)}.");
            Pass2ExecutionStatusResponse status = JsonSerializer.Deserialize<Pass2ExecutionStatusResponse>(
                body, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new Xunit.Sdk.XunitException("T048 status returned no typed response.");
            if (status.State is "Completed" or "CompletedWithWarnings")
            {
                return status;
            }
            if (status.State is "PausedProviderUnavailable" or "FailedIntegrity" or
                "FailedSecurity" or "Cancelled")
            {
                throw new Xunit.Sdk.XunitException(
                    $"T048 Pass 2 stopped before terminal success; state={status.State}; " +
                    $"pending={status.PendingCount}; running={status.RunningCount}; " +
                    $"retry_scheduled={status.RetryScheduledCount}; " +
                    $"provider_http={string.Join(',', providerHttpDiagnostics.Observations)}.");
            }
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        throw new TimeoutException("T048 Pass 2 execution did not reach terminal within the approved cap.");
    }

    private static async Task AssertProviderAvailableAsync(
        WebApplicationFactory<Program> host,
        Guid systemId)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        WorkerAgentProviderResolver resolver =
            scope.ServiceProvider.GetRequiredService<WorkerAgentProviderResolver>();
        ISecretEncryptionService encryption =
            scope.ServiceProvider.GetRequiredService<ISecretEncryptionService>();
        IPreparedPass1ProviderHealthProbe healthProbe =
            scope.ServiceProvider.GetRequiredService<IPreparedPass1ProviderHealthProbe>();
        ResolvedWorkerAgentProvider provider = await resolver.ResolveAsync(systemId);
        string apiKey = await encryption.DecryptAsync(
            SecretPurposes.LlmApiKey, provider.EncryptedApiKey);
        PreparedPass1ProviderHealthResult result = await healthProbe.ProbeAsync(
            provider.Endpoint, provider.Model, apiKey);
        Assert.True(result.IsAvailable,
            $"T048 provider preflight failed closed; outcome={result.Outcome}; " +
            $"endpoint_path_hash={Hash(provider.Endpoint.AbsolutePath)}; " +
            $"model_hash={Hash(provider.Model)}.");
    }

    private static TimeSpan QualificationTimeout()
    {
        const int maximumSeconds = 9_000;
        string? configured = Environment.GetEnvironmentVariable("VULGATA_T124_TERMINAL_TIMEOUT_SECONDS");
        return configured is null ? TimeSpan.FromSeconds(maximumSeconds) :
            int.TryParse(configured, out int seconds) && seconds is >= 30 and <= maximumSeconds
                ? TimeSpan.FromSeconds(seconds)
                : throw new Xunit.Sdk.XunitException(
                    "VULGATA_T124_TERMINAL_TIMEOUT_SECONDS must be from 30 through 9000.");
    }

    private static void RequireOptIn()
    {
        Assert.True(IsTrue(Environment.GetEnvironmentVariable(OptInVariable)),
            $"T048 requires {OptInVariable}=1.");
        Assert.True(IsTrue(Environment.GetEnvironmentVariable(ProviderContentVariable)),
            $"T048 requires {ProviderContentVariable}=1.");
        Environment.SetEnvironmentVariable("VULGATA_T062_APPROVED_PAIR_REAL", "1");
        Environment.SetEnvironmentVariable("VULGATA_T062_SOURCE_BINDING_READY", "1");
    }

    internal sealed class T048ProductionQualificationFactAttribute : SuiteFactAttribute
    {
        public T048ProductionQualificationFactAttribute() : base("External") => Skip =
            !IsTrue(Environment.GetEnvironmentVariable("VULGATA_T124_RUN_PRODUCTION_QUALIFICATION")) ||
            !IsTrue(Environment.GetEnvironmentVariable("VULGATA_T124_ALLOW_PROVIDER_CONTENT"))
                ? "T048 external qualification is opt-in; set both authorization flags to 1."
                : null;
    }

    private static bool IsTrue(string? value) =>
        value is "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static string Required(string variable) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable))
            ? Environment.GetEnvironmentVariable(variable)!
            : throw new Xunit.Sdk.XunitException($"T048 requires {variable}.");

    private static string ClassifyFailure(Exception exception)
    {
        string text = exception.ToString();
        if (text.Contains("429", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("balance", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("token limit", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("tokens exhausted", StringComparison.OrdinalIgnoreCase)) return "provider_capacity";
        if (exception is PostgresException or NpgsqlException || exception is IOException ||
            text.Contains("expected both approved manifests", StringComparison.OrdinalIgnoreCase))
            return "environment";
        if (exception is HttpRequestException ||
            text.Contains("connection refused", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("name resolution", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("socket", StringComparison.OrdinalIgnoreCase)) return "network";
        return "qualification_assertion";
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Closed<T>(IEnumerable<T> values) =>
        string.Join(',', values.Select(value => value?.ToString() ?? "none").Distinct(StringComparer.Ordinal));

    private sealed class PreparedPass1HostedErrorObserver : Microsoft.Extensions.Logging.ILoggerProvider
    {
        internal System.Collections.Concurrent.ConcurrentQueue<string> Errors { get; } = new();

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) =>
            new HostedErrorLogger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class HostedErrorLogger(
            PreparedPass1HostedErrorObserver owner,
            string categoryName) : Microsoft.Extensions.Logging.ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) =>
                logLevel >= Microsoft.Extensions.Logging.LogLevel.Error &&
                string.Equals(
                    categoryName,
                    "Vulgata.Web.Data.PreparedPass1ExecutionHostedService",
                    StringComparison.Ordinal);

            public void Log<TState>(
                Microsoft.Extensions.Logging.LogLevel logLevel,
                Microsoft.Extensions.Logging.EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                string message = formatter(state, exception)
                    .Replace('\r', ' ')
                    .Replace('\n', ' ');
                if (message.Length > 600) message = message[..600];
                owner.Errors.Enqueue(
                    $"{exception?.GetType().Name ?? "none"}:{message}");
            }
        }
    }

    private static async Task WriteEvidenceAsync(QualificationEvidence evidence)
    {
        string root = Directory.GetCurrentDirectory();
        while (!Directory.Exists(Path.Combine(root, "specs")))
        {
            root = Directory.GetParent(root)?.FullName
                ?? throw new InvalidOperationException("T048 repository root is unavailable.");
        }
        string path = Path.Combine(root, "specs", "013-exact-cross-repository-flow-pass2",
            "evidence", "qualification", "story-12.4-live-two-repository.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(evidence,
            QualificationEvidenceJsonOptions()) + Environment.NewLine);
    }

    [Trait("Suite", "External")]
    [SuiteFact("External")]
    public void QualificationEvidence_SerializesRequiredSnakeCaseContract()
    {
        var evidence = new QualificationEvidence(
            "story-12.4-live-two-repository/1.0", "qualification", "observed", "Completed",
            "system", ["repository"], ["revision"], "https://opencode.ai",
            "deepseek-v4-flash", "localhost:5432/disposable", 1, 1, 1, 0, 3, 3,
            ["document"], [1], true, "none");

        using JsonDocument serialized = JsonDocument.Parse(
            JsonSerializer.Serialize(evidence, QualificationEvidenceJsonOptions()));

        Assert.True(serialized.RootElement.TryGetProperty("schema_version", out _));
        Assert.True(serialized.RootElement.TryGetProperty("qualification_id", out _));
        Assert.True(serialized.RootElement.TryGetProperty("content_dark_diagnostics", out _));
        Assert.False(serialized.RootElement.TryGetProperty("schemaVersion", out _));
    }

    private static JsonSerializerOptions QualificationEvidenceJsonOptions() =>
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        };

    private sealed record QualificationEvidence(
        string SchemaVersion, string QualificationId, string ObservedAt, string Status,
        string SystemIdentityHash, string[] RepositoryIdentityHashes, string[] RevisionHashes,
        string ProviderEndpoint, string ProviderModel, string DatabaseTarget, int FlowGroupCount,
        int JobCount, int DocumentCount, int OmissionCount, int GeneratedFromCount,
        int SourceAnchorCount, string[] DocumentContentHashes, int[] DocumentUtf8Bytes,
        bool ContentDarkDiagnostics, string FailureCategory)
    {
        internal static QualificationEvidence Succeeded(
            QualificationDatabase database, T062QualificationInputs qualification,
            Pass2ExecutionStartResponse started, Pass2ExecutionStatusResponse terminal,
            IReadOnlyCollection<FlowGroup> groups, IReadOnlyCollection<Pass2JobExecution> jobs,
            IReadOnlyCollection<DocumentVersion> documents, IReadOnlyCollection<DocumentEdge> provenance,
            IReadOnlyCollection<SourceAnchor> anchors) => new(
                "story-12.4-live-two-repository/1.0", Hash(started.Pass2ExecutionId.ToString("D")),
                DateTimeOffset.UtcNow.ToString("O"), terminal.State, Hash(qualification.SystemId.ToString("D")),
                qualification.Repositories.Select(value => Hash(value.RepositoryId.ToString("D"))).ToArray(),
                qualification.Repositories.Select(value => Hash(value.ProfiledRevision)).ToArray(),
                qualification.ProviderEndpoint.GetLeftPart(UriPartial.Authority), qualification.ProviderModel,
                database.SanitizedTarget, groups.Count, jobs.Count, documents.Count, terminal.OmissionCount,
                provenance.Count, anchors.Count,
                documents.OrderBy(value => value.Id).Select(value => value.ContentFingerprint).ToArray(),
                documents.OrderBy(value => value.Id).Select(value => Encoding.UTF8.GetByteCount(value.ContentMarkdown)).ToArray(),
                true, "none");

        internal static QualificationEvidence Failed(QualificationDatabase database, string category) => new(
            "story-12.4-live-two-repository/1.0", Hash(database.DatabaseName), DateTimeOffset.UtcNow.ToString("O"),
            "failed", string.Empty, [], [], string.Empty, string.Empty, database.SanitizedTarget,
            0, 0, 0, 0, 0, 0, [], [], true, category);
    }

    private sealed class Story124ProviderHttpDiagnosticHandler : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _inner = new(new SocketsHttpHandler(), true);
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _observations = new();

        internal IReadOnlyList<string> Observations => _observations.ToArray();

        internal void ObserveTransportException(Exception exception)
        {
            string frames = string.Join('>', new System.Diagnostics.StackTrace(exception, true)
                .GetFrames()
                .Take(5)
                .Select(frame =>
                    $"{frame.GetMethod()?.DeclaringType?.Name}.{frame.GetMethod()?.Name}:{frame.GetFileLineNumber()}"));
            _observations.Enqueue($"transport:{exception.GetType().Name}:{frames}");
        }

        internal void ObserveDispatcherException(Exception exception)
        {
            string frames = string.Join('>', new System.Diagnostics.StackTrace(exception, true)
                .GetFrames()
                .Take(7)
                .Select(frame =>
                    $"{frame.GetMethod()?.DeclaringType?.Name}.{frame.GetMethod()?.Name}:{frame.GetFileLineNumber()}"));
            _observations.Enqueue($"dispatcher:{exception.GetType().Name}:{frames}");
        }

        internal void ObserveToolControlFields(ReadOnlySpan<byte> chunk)
        {
            if (chunk.IsEmpty)
            {
                return;
            }
            try
            {
                using JsonDocument document = JsonDocument.Parse(chunk.ToArray());
                if (!document.RootElement.TryGetProperty("toolCalls", out JsonElement calls) ||
                    calls.ValueKind != JsonValueKind.Array)
                {
                    return;
                }
                foreach (JsonElement call in calls.EnumerateArray())
                {
                    string name = call.TryGetProperty("name", out JsonElement nameValue)
                        ? nameValue.GetString() ?? "missing"
                        : "missing";
                    string operation = "none";
                    string direction = "none";
                    if (call.TryGetProperty("arguments", out JsonElement arguments) &&
                        arguments.ValueKind == JsonValueKind.Object)
                    {
                        operation = arguments.TryGetProperty("operation", out JsonElement operationValue)
                            ? operationValue.ToString()
                            : "none";
                        direction = arguments.TryGetProperty("direction", out JsonElement directionValue)
                            ? directionValue.ToString()
                            : "none";
                    }
                    _observations.Enqueue($"tool:{name}:{operation}:{direction}");
                }
            }
            catch (JsonException)
            {
                _observations.Enqueue("tool:unparseable");
            }
        }

        internal void ObserveCandidate(
            Pass2BusinessLogicCommitRequest request,
            Pass2BusinessLogicCommitResult result)
        {
            byte[] candidate = Encoding.UTF8.GetBytes(request.Candidate.GetRawText());
            string hash = Convert.ToHexStringLower(SHA256.HashData(candidate));
            string directory = Path.Combine(
                Directory.GetCurrentDirectory(), ".tmp-story124-candidates");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,
                $"{request.Pass2JobExecutionId:N}-{request.AttemptOrdinal}-{hash}.json");
            File.WriteAllBytes(path, candidate);
            _observations.Enqueue(
                $"candidate:{hash}:{result.Status}:schema=" +
                Pass2BusinessLogicSchemaContract.IsValidDocument(request.Candidate));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                HttpResponseMessage response = await _inner.SendAsync(request, cancellationToken);
                _observations.Enqueue(
                    $"{request.Method}:{Hash(request.RequestUri?.AbsolutePath ?? string.Empty)}:{(int)response.StatusCode}");
                return response;
            }
            catch (Exception exception)
            {
                _observations.Enqueue(
                    $"{request.Method}:{Hash(request.RequestUri?.AbsolutePath ?? string.Empty)}:{exception.GetType().Name}");
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class Story124DiagnosticPass2Transport(
        IPreparedPass2StreamingTransport inner,
        Story124ProviderHttpDiagnosticHandler diagnostics) : IPreparedPass2StreamingTransport
    {
        public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(
            PreparedPass2ProviderRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await using IAsyncEnumerator<ReadOnlyMemory<byte>> enumerator =
                inner.StreamAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (Exception exception)
                {
                    diagnostics.ObserveTransportException(exception);
                    throw;
                }

                if (!hasNext)
                {
                    yield break;
                }
                diagnostics.ObserveToolControlFields(enumerator.Current.Span);
                yield return enumerator.Current;
            }
        }
    }

    private sealed class Story124DiagnosticPass2Dispatcher(
        IPreparedPass2WorkerDispatcher inner,
        Story124ProviderHttpDiagnosticHandler diagnostics) : IPreparedPass2WorkerDispatcher
    {
        public async Task<PreparedPass2DispatchResult> DispatchAsync(
            PreparedPass2DispatchRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await inner.DispatchAsync(request, cancellationToken);
            }
            catch (Exception exception)
            {
                diagnostics.ObserveDispatcherException(exception);
                throw;
            }
        }
    }

    private sealed class Story124DiagnosticCommitter(
        IPass2BusinessLogicDocumentCommitter inner,
        Story124ProviderHttpDiagnosticHandler diagnostics)
        : IPass2BusinessLogicDocumentCommitter
    {
        public async Task<Pass2BusinessLogicCommitResult> CommitAsync(
            Pass2BusinessLogicCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            Pass2BusinessLogicCommitResult result = await inner.CommitAsync(
                request, cancellationToken);
            diagnostics.ObserveCandidate(request, result);
            return result;
        }
    }

    internal sealed class QualificationDatabase : IAsyncDisposable
    {
        private readonly string _administrator;
        private bool _disposed;
        private bool _retainSuccessfulResult;
        private QualificationDatabase(string administrator, string databaseName, string connectionString)
        {
            _administrator = administrator;
            DatabaseName = databaseName;
            ConnectionString = connectionString;
            var target = new NpgsqlConnectionStringBuilder(connectionString);
            SanitizedTarget = $"{target.Host}:{target.Port}/{databaseName}";
        }
        internal string DatabaseName { get; }
        internal string ConnectionString { get; }
        internal string SanitizedTarget { get; }

        internal void RetainSuccessfulResult() => _retainSuccessfulResult = true;

        internal static async Task<QualificationDatabase> CloneAsync(
            string administratorConnection, string sourceConnection)
        {
            TestResourceScope.ValidateAdministrator(administratorConnection);
            TestResourceScope.ValidateOwnedTarget(sourceConnection);
            var administrator = new NpgsqlConnectionStringBuilder(administratorConnection)
            { Pooling = false, Timeout = 10, CommandTimeout = 120 };
            var source = new NpgsqlConnectionStringBuilder(sourceConnection);
            bool approvedSource = source.Database.StartsWith("vulgata_story124_", StringComparison.Ordinal);
            if (!approvedSource ||
                !StringComparer.OrdinalIgnoreCase.Equals(source.Host, administrator.Host) ||
                source.Port != administrator.Port)
                throw new InvalidOperationException(
                    "T048 source must be the approved vulgata database on the administrator instance.");
            string name = "vulgata_story124_" + Guid.NewGuid().ToString("N");
            await using var connection = new NpgsqlConnection(administrator.ConnectionString);
            await connection.OpenAsync();
            await using (var version = new NpgsqlCommand(
                "SELECT current_setting('server_version_num')::integer;", connection))
                Assert.Equal(17, Convert.ToInt32(await version.ExecuteScalarAsync(),
                    System.Globalization.CultureInfo.InvariantCulture) / 10_000);
            await using (var create = new NpgsqlCommand(
                $"CREATE DATABASE {Quote(name)} WITH TEMPLATE {Quote(source.Database)};", connection))
                await create.ExecuteNonQueryAsync();
            TestResourceScope.RecordCreated(administrator.ConnectionString, name);
            var target = new NpgsqlConnectionStringBuilder(sourceConnection)
            { Database = name, Pooling = false };
            var database = new QualificationDatabase(
                administrator.ConnectionString, name, target.ConnectionString);
            try
            {
                await using Vulgata.Infrastructure.Data.VulgataDbContext context =
                    PreparedScanPostgreSqlFixture.CreateContext(database.ConnectionString);
                await context.Database.MigrateAsync();
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            if (_retainSuccessfulResult) return;
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(_administrator);
            await connection.OpenAsync();
            await using (var terminate = new NpgsqlCommand(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
                "WHERE datname = @name AND pid <> pg_backend_pid();", connection))
            {
                terminate.Parameters.AddWithValue("name", DatabaseName);
                await terminate.ExecuteNonQueryAsync();
            }
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {Quote(DatabaseName)};", connection);
            await drop.ExecuteNonQueryAsync();
        }

        private static string Quote(string value)
        {
            if (!value.StartsWith("vulgata_story124_", StringComparison.Ordinal) && value != "vulgata")
                throw new InvalidOperationException("T048 refused a database outside its exact boundary.");
            return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
        }
    }
}
