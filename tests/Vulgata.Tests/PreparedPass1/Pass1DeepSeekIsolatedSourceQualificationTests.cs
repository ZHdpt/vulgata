using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.PreparedScanning;
using Vulgata.Web.Data;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T084: three sequential source-bearing jobs through the production Worker,
/// durable claim, and atomic Document graph commit boundaries.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class Pass1DeepSeekIsolatedSourceQualificationTests
{
    [Fact]
    public void IsolatedQualification_DiagnosticObserverDoesNotExposeCandidateValidationDetails()
    {
        var observer = new PreparedPass1SubmissionDiagnosticObserver();
        observer.Observe(PreparedPass1SubmissionRejectionKind.InvalidSemantics);

        Assert.Equal(
            [PreparedPass1SubmissionRejectionKind.InvalidSemantics],
            observer.Categories);
        Assert.Null(
            typeof(IPreparedPass1SubmissionDiagnosticObserver).GetProperty(
                "DiagnosticMessages",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.DoesNotContain(
            typeof(IPreparedPass1SubmissionDiagnosticObserver).GetMethods(),
            method => method.Name == "Observe" && method.GetParameters().Length == 2);
    }

    [T084IsolatedSourceJobsFact]
    public async Task ThreeIsolatedSourceJobs_CompleteCommitAndReplayWithoutLeakingPayloadsAsync()
    {
        Pass1ProductionExecutionTests.RequireRealHostQualificationPrerequisites();
        T062QualificationInputs qualification =
            await Pass1ProductionExecutionTests.LoadQualificationInputsAsync();
        await using var host = CreateT084ProductionHost(
            qualification.ConnectionString);
        await Pass1ProductionExecutionTests.ConfigureWorkerProviderAsync(
            host,
            qualification.SystemId,
            qualification.ProviderEndpoint,
            qualification.ProviderModel);

        PairBinding pairBinding =
            await LoadPairBindingAsync(qualification.ConnectionString, qualification.SystemId);
        PreparedScanAuthorizationScope authorization = new(
            Guid.NewGuid(),
            qualification.SystemId,
            pairBinding.RepositoryIds);
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(150));
        IPreparedPass1RevisionBindingRegistry revisionBindings = host.Services
            .GetRequiredService<IPreparedPass1RevisionBindingRegistry>();
        PreparedPass1PairRevisionBindings? acquired = await revisionBindings.AcquirePairAsync(
            pairBinding.PairId,
            pairBinding.RevisionRequests,
            timeout.Token);
        Assert.NotNull(acquired);
        var evidence = new List<IsolatedJobEvidence>(3);

        try
        {
            for (int ordinal = 1; ordinal <= 3; ordinal++)
            {
                await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
                IServiceProvider services = scope.ServiceProvider;
                IPreparedPass1SubmissionDiagnosticObserver diagnostics = services
                    .GetRequiredService<IPreparedPass1SubmissionDiagnosticObserver>();
                IPreparedPass1WireBudgetDiagnosticObserver wireBudgetDiagnostics = services
                    .GetRequiredService<IPreparedPass1WireBudgetDiagnosticObserver>();
                IPreparedPass1ExecutionDiagnosticObserver executionDiagnostics = services
                    .GetRequiredService<IPreparedPass1ExecutionDiagnosticObserver>();
                T084ProviderRejectionCapture providerRejection = services
                    .GetRequiredService<T084ProviderRejectionCapture>();
                T084ExecutorDiagnosticCapture executorDiagnostics = services
                    .GetRequiredService<T084ExecutorDiagnosticCapture>();
                DateTimeOffset now = DateTimeOffset.UtcNow;
                PreparedPass1ClaimResult claim = await services
                    .GetRequiredService<IPreparedPass1Store>()
                    .ClaimNextAsync(pairBinding.PairId, now, TimeSpan.FromMinutes(30), timeout.Token);
                Assert.True(
                    claim.Outcome == PreparedPass1ClaimOutcome.Claimed,
                    $"T084 stopped before job {ordinal}: claim outcome={claim.Outcome}.");

                PreparedPass1WorkerPacket? hydrated = await services
                    .GetRequiredService<IPreparedPass1ClaimedPacketSource>()
                    .HydrateAsync(pairBinding.PairId, claim, DateTimeOffset.UtcNow, timeout.Token);
                Assert.NotNull(hydrated);
                PreparedPass1WorkerPacket packet = hydrated! with
                {
                    ServerAuthorization = new PreparedPass1ServerAuthorization(
                        pairBinding.PairId,
                        claim.JobStateVersion,
                        claim.PairStateVersion,
                        claim.LeaseToken,
                        claim.LeaseExpiresAt,
                        nameof(Pass1PairExecutionPhase.Running),
                        Pass1CircuitState.Closed,
                        ScanJobExecutionState.Running,
                        PreparedPass1AuthorizationBinding.Create(authorization)),
                };

            PreparedPass1DispatchResult dispatch = await services
                .GetRequiredService<PreparedPass1DispatchWorker>()
                .ExecuteAsync(CreateDispatchRequest(packet), timeout.Token);
            PreparedPass1JobDispatchResult result = Assert.Single(dispatch.Results);
            IReadOnlyList<PreparedPass1SubmissionRejectionKind> categories = diagnostics.Categories;
            Assert.All(
                categories,
                category => Assert.True(
                    category is PreparedPass1SubmissionRejectionKind.MissingSubmission
                        or PreparedPass1SubmissionRejectionKind.InvalidShape
                        or PreparedPass1SubmissionRejectionKind.InvalidBinding
                        or PreparedPass1SubmissionRejectionKind.InvalidEvidence
                        or PreparedPass1SubmissionRejectionKind.InvalidSemantics,
                    $"T084 observed an unrecognized submission category for job {ordinal}."));
            Assert.True(
                categories.Count <= 1,
                $"T084 observed more than one sanitized submission category for job {ordinal}.");
            string? diagnosticCategory = categories.Count == 0
                ? null
                : Assert.Single(categories).ToString();
            IReadOnlyList<PreparedPass1WireBudgetDiagnosticKind> wireBudgetCategories =
                wireBudgetDiagnostics.Categories;
            Assert.All(
                wireBudgetCategories,
                category => Assert.True(
                    category is PreparedPass1WireBudgetDiagnosticKind.PreparedInitialMessage
                        or PreparedPass1WireBudgetDiagnosticKind.OutgoingRequest
                        or PreparedPass1WireBudgetDiagnosticKind.RawResponse
                        or PreparedPass1WireBudgetDiagnosticKind.AssembledResponse,
                    $"T084 observed an unrecognized wire-budget category for job {ordinal}."));
            Assert.True(
                wireBudgetCategories.Count <= 1,
                $"T084 observed more than one wire-budget category for job {ordinal}.");
            string? wireBudgetCategory = wireBudgetCategories.Count == 0
                ? null
                : Assert.Single(wireBudgetCategories).ToString();
            IReadOnlyList<PreparedPass1ExecutionDiagnosticKind> executionCategories =
                executionDiagnostics.Categories;
            Assert.All(
                executionCategories,
                category => Assert.True(
                    category is PreparedPass1ExecutionDiagnosticKind.ProviderClientRequestRejected
                        or PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall
                        or PreparedPass1ExecutionDiagnosticKind.UnexpectedException,
                    $"T084 observed an unrecognized execution category for job {ordinal}."));
            Assert.True(
                executionCategories.Count <= 1,
                $"T084 observed more than one sanitized execution category for job {ordinal}.");
            string? executionCategory = executionCategories.Count == 0
                ? null
                : Assert.Single(executionCategories).ToString();
            string? functionCallSubtype = ReadFunctionCallSubtype(executionDiagnostics);
            Assert.True(
                functionCallSubtype is null
                or "UnknownFunction"
                or "MultipleFunctions"
                or "MissingFunction"
                or "InvalidCallIdentity"
                or "MixedFunctions"
                or "MultipleSubmissions"
                or "TerminalMultipleSubmissions"
                or "TerminalNavigation"
                or "TerminalMixedFunctions"
                or "TerminalMultipleNavigations"
                or "NavigationBudgetExceeded"
                or "NavigationUnavailable"
                or "TurnIneligible"
                or "TerminalTurnRequiresSubmission"
                or "SubmissionUnavailable"
                or "PostTurnLimit",
                "T084 observed an unrecognized payload-free function-call subtype.");
            if (executionCategories.Contains(PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall))
            {
                Assert.NotNull(functionCallSubtype);
            }
            string executorCategory = executorDiagnostics.Category.ToString();
            Assert.True(
                result.Outcome == PreparedPass1RawWorkerOutcome.Completed,
                $"T084 stopped at job {ordinal}: outcome={result.Outcome}; " +
                $"reason={result.ReasonCode ?? "none"}; " +
                $"executorCategory={executorCategory}; " +
                $"executionCategory={executionCategory ?? "none"}; " +
                $"functionCallSubtype={functionCallSubtype ?? "none"}; " +
                $"submissionCategory={diagnosticCategory ?? "none"}; " +
                $"wireBudgetCategory={wireBudgetCategory ?? "none"}; " +
                $"providerHttp={providerRejection.Summary}; " +
                $"extension={Path.GetExtension(packet.RepositoryRelativePath)}.");
            Assert.NotNull(result.ParsedResult);
            Assert.Equal("code-logic/file-v2", result.ParsedResult!.SchemaIdentity);

            PreparedPass1CanonicalArtifact logical = PreparedPass1CanonicalCodec.CreateLogicalDocument(
                new(packet.RepositoryId, packet.RepositoryRelativePath, checked((ulong)packet.SegmentOrdinal)));
            PreparedPass1CanonicalArtifact version = PreparedPass1CanonicalCodec.CreateDocumentVersion(
                new(packet.JobId, Convert.FromHexString(packet.FileCodeUnitIdentityFingerprint), packet.DocumentSchemaIdentity));
            Pass1DocumentCommitRequest commitRequest = new(
                packet,
                result.ParsedResult.CanonicalCandidateJson,
                logical.Id!.Value,
                version.Id!.Value,
                claim.JobStateVersion,
                claim.LeaseToken,
                DateTimeOffset.UtcNow);
            IPass1CodeLogicDocumentCommitter committer = services
                .GetRequiredService<IPass1CodeLogicDocumentCommitter>();
            Pass1DocumentCommitResult committed = await committer.CommitAsync(
                commitRequest,
                timeout.Token);
            Assert.True(
                committed.Status == Pass1DocumentCommitStatus.Committed,
                $"T084 commit stopped at job {ordinal}: status={committed.Status}; " +
                $"reason={committed.ReasonCode ?? "none"}; " +
                $"submissionCategory={diagnosticCategory ?? "none"}; " +
                $"extension={Path.GetExtension(packet.RepositoryRelativePath)}.");
            Assert.NotNull(committed.DocumentVersionId);
            DocumentGraphEvidence beforeReplay = await ReadGraphEvidenceAsync(
                services.GetRequiredService<VulgataDbContext>(),
                packet.JobId,
                committed.DocumentVersionId.Value,
                timeout.Token);

            Pass1DocumentCommitResult replayed = await committer.CommitAsync(
                commitRequest,
                timeout.Token);
            Assert.Equal(Pass1DocumentCommitStatus.Replayed, replayed.Status);
            Assert.Equal(committed.CommitFingerprint, replayed.CommitFingerprint);
            Assert.Equal(
                beforeReplay,
                await ReadGraphEvidenceAsync(
                    services.GetRequiredService<VulgataDbContext>(),
                    packet.JobId,
                    committed.DocumentVersionId.Value,
                    timeout.Token));

                evidence.Add(new(
                    ordinal,
                    claim.PairSelectionOrdinal,
                    Path.GetExtension(packet.RepositoryRelativePath).ToLowerInvariant(),
                    packet.SourceUtf8.Length,
                    Sha256(packet.SourceUtf8.Span),
                    Sha256(result.ParsedResult.CanonicalCandidateJson.Span),
                    committed.CommitFingerprint!,
                    beforeReplay.SourceAnchorCount,
                    beforeReplay.SectionCount,
                    beforeReplay.IndexEntryCount,
                    executorCategory,
                    executionCategory,
                    diagnosticCategory,
                    wireBudgetCategory));
            }
        }
        finally
        {
            await revisionBindings.ReleasePairAsync(pairBinding.PairId);
        }

        Assert.Equal(3, evidence.Count);
        await WriteEvidenceAsync(qualification, pairBinding.PairId, evidence, timeout.Token);
    }

    private static async Task<PairBinding>
        LoadPairBindingAsync(string connectionString, Guid systemId)
    {
        await using VulgataDbContext context =
            PreparedScanPostgreSqlFixture.CreateContext(connectionString);
        Pass1PairExecution pair = await context.Pass1PairExecutions
            .AsNoTracking()
            .SingleAsync(value =>
                value.SystemId == systemId &&
                value.Phase == Pass1PairExecutionPhase.Running &&
                value.CircuitState == Pass1CircuitState.Closed);
        Guid[] manifestIds = [pair.ManifestOneId, pair.ManifestTwoId];
        PreparedScanManifest[] manifests = await context.PreparedScanManifests
            .AsNoTracking()
            .Where(value => manifestIds.Contains(value.Id))
            .OrderBy(value => value.RepositoryId)
            .ToArrayAsync();
        Guid[] repositoryIds = [.. manifests.Select(value => value.RepositoryId).Order()];
        Assert.Equal(2, repositoryIds.Distinct().Count());
        Dictionary<Guid, string> origins = await context.Repositories
            .AsNoTracking()
            .Where(value => repositoryIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, value => value.GitUrl);
        PreparedRevisionRequest[] requests = [.. manifests.Select(value => new PreparedRevisionRequest(
            value.RepositoryId,
            value.ScanRunId,
            origins[value.RepositoryId],
            value.RequestedRef,
            value.ProfiledRevision))];
        return new(pair.Id, repositoryIds, requests);
    }

    private static WebApplicationFactory<Program> CreateT084ProductionHost(
        string connectionString) =>
        Pass1ProductionExecutionTests.CreateProductionHost(connectionString)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPreparedPass1SubmissionDiagnosticObserver>();
                services.RemoveAll<PreparedPass1SubmissionDiagnosticObserver>();
                services.AddScoped<PreparedPass1SubmissionDiagnosticObserver>();
                services.AddScoped<IPreparedPass1SubmissionDiagnosticObserver>(services =>
                    services.GetRequiredService<PreparedPass1SubmissionDiagnosticObserver>());
                services.RemoveAll<IPreparedPass1WireBudgetDiagnosticObserver>();
                services.RemoveAll<PreparedPass1WireBudgetDiagnosticObserver>();
                services.AddScoped<PreparedPass1WireBudgetDiagnosticObserver>();
                services.AddScoped<IPreparedPass1WireBudgetDiagnosticObserver>(services =>
                    services.GetRequiredService<PreparedPass1WireBudgetDiagnosticObserver>());
                services.RemoveAll<IPreparedPass1ExecutionDiagnosticObserver>();
                services.RemoveAll<PreparedPass1ExecutionDiagnosticObserver>();
                services.AddScoped<PreparedPass1ExecutionDiagnosticObserver>();
                services.AddScoped<IPreparedPass1ExecutionDiagnosticObserver>(services =>
                    services.GetRequiredService<PreparedPass1ExecutionDiagnosticObserver>());
                services.RemoveAll<HttpMessageHandler>();
                services.RemoveAll<T084ProviderRejectionCapture>();
                services.AddSingleton<T084ProviderRejectionCapture>();
                services.AddSingleton<HttpMessageHandler>(services =>
                    services.GetRequiredService<T084ProviderRejectionCapture>());
                services.RemoveAll<PreparedSubmitCodeLogicFunction>();
                services.AddScoped<PreparedSubmitCodeLogicFunction>(services =>
                    new PreparedSubmitCodeLogicFunction(
                        services.GetRequiredService<IPreparedPass1SubmissionDiagnosticObserver>()));
                services.RemoveAll<T084ExecutorDiagnosticCapture>();
                services.AddScoped<T084ExecutorDiagnosticCapture>(services =>
                    new T084ExecutorDiagnosticCapture(
                        new PreparedPass1JobExecutor(
                            services.GetRequiredService<WorkerAgentProviderResolver>(),
                            services.GetRequiredService<ISecretEncryptionService>(),
                            services.GetRequiredService<PreparedNavigateCodeFunction>(),
                            services.GetRequiredService<HttpMessageHandler>(),
                            services.GetRequiredService<PreparedSubmitCodeLogicFunction>(),
                            services.GetRequiredService<IPreparedPass1WireBudgetDiagnosticObserver>(),
                            services.GetRequiredService<IPreparedPass1ExecutionDiagnosticObserver>())));
                services.RemoveAll<IPreparedPass1JobExecutor>();
                services.AddScoped<IPreparedPass1JobExecutor>(services =>
                    services.GetRequiredService<T084ExecutorDiagnosticCapture>());
                services.RemoveAll<IPreparedPass1WorkerDispatcher>();
                services.AddScoped<IPreparedPass1WorkerDispatcher>(services =>
                    new PreparedPass1WorkerDispatcher(
                        services.GetRequiredService<IPreparedPass1JobExecutor>(),
                        services.GetRequiredService<IPreparedPass1SubmissionDiagnosticObserver>()));
            }));

    private static PreparedPass1DispatchRequest CreateDispatchRequest(
        PreparedPass1WorkerPacket packet)
    {
        PreparedPass1FileCodeUnitAssignment assignment = new(
            packet.PairExecutionId,
            packet.JobId,
            packet.ScanExecutionId,
            packet.SystemId,
            packet.RepositoryId,
            packet.ScanRunId,
            packet.ProfiledRevision,
            packet.FileCodeUnitId,
            packet.FileCodeUnitIdentityFingerprint,
            packet.PreparedScanManifestId,
            packet.PreparedScanManifestFingerprint,
            packet.PairPackageFingerprint,
            packet.RepositoryRelativePath,
            packet.ByteStart,
            packet.ByteEnd,
            packet.LineStart,
            packet.LineEnd,
            packet.SegmentOrdinal,
            packet.SegmentCount,
            packet.FileHash,
            packet.SourceHash,
            packet.PromptIdentity,
            packet.DocumentSchemaIdentity,
            packet.AnalysisRuntimeIdentityHash,
            packet.ContextSnapshot.ContextSnapshotId,
            packet.ManifestOrdinal,
            packet.PairSelectionOrdinal,
            packet.SourceUtf8)
        {
            RemainingNavigationBudget = packet.RemainingNavigationBudget,
            ServerAuthorization = packet.ServerAuthorization,
        };
        return new(
            [assignment],
            [packet.ContextSnapshot],
            packet.CommunicationClassifications,
            1);
    }

    private static async Task<DocumentGraphEvidence> ReadGraphEvidenceAsync(
        VulgataDbContext context,
        Guid jobId,
        Guid documentVersionId,
        CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var graph = await context.DocumentVersions
            .AsNoTracking()
            .Where(value => value.Id == documentVersionId)
            .Select(value => new
            {
                value.SchemaIdentity,
                value.ContentFingerprint,
                value.CommitFingerprint,
                value.SupersededById,
                SourceAnchorCount = value.SourceAnchors.Count,
                SectionCount = value.Sections.Count,
                IndexEntryCount = value.IndexEntry == null ? 0 : 1,
            })
            .SingleAsync(cancellationToken);
        ScanJobExecution job = await context.ScanJobExecutions
            .AsNoTracking()
            .SingleAsync(value => value.Id == jobId, cancellationToken);
        Assert.Equal(ScanJobExecutionState.Completed, job.State);
        Assert.Equal(documentVersionId, job.CommittedDocumentVersionId);
        Assert.Equal("code-logic/file-v2", graph.SchemaIdentity);
        Assert.Null(graph.SupersededById);
        Assert.True(graph.SourceAnchorCount > 0);
        Assert.True(graph.SectionCount > 0);
        Assert.Equal(1, graph.IndexEntryCount);
        return new(
            graph.ContentFingerprint,
            graph.CommitFingerprint,
            graph.SourceAnchorCount,
            graph.SectionCount,
            graph.IndexEntryCount);
    }

    private static async Task WriteEvidenceAsync(
        T062QualificationInputs qualification,
        Guid pairId,
        IReadOnlyList<IsolatedJobEvidence> jobs,
        CancellationToken cancellationToken)
    {
        string repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(
            repositoryRoot,
            "artifacts",
            "qualification",
            "story-12.3",
            "deepseek-isolated-source-jobs.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "story-12.3-deepseek-isolated-source-jobs/1.0",
            status = "passed",
            pairExecutionId = pairId,
            provider = new
            {
                endpoint = qualification.ProviderEndpoint.GetLeftPart(UriPartial.Authority),
                model = qualification.ProviderModel,
                streaming = true,
                strictFunctionCount = 2,
            },
            isolation = new
            {
                sequential = true,
                freshDependencyInjectionScopePerJob = true,
                sourceOrProviderPayloadPersisted = false,
            },
            jobs,
        }, new JsonSerializerOptions { WriteIndented = true });
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temporary, json, cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new Xunit.Sdk.XunitException("Repository root could not be located.");
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string? ReadFunctionCallSubtype(
        IPreparedPass1ExecutionDiagnosticObserver diagnostics)
    {
        PropertyInfo? property = diagnostics.GetType().GetProperty(
            "FunctionCallSubtype",
            BindingFlags.Instance | BindingFlags.Public);
        return property?.GetValue(diagnostics)?.ToString();
    }

    private enum T084ExecutorReasonCategory
    {
        Completed,
        SecurityFailure,
        IntegrityFailure,
        ProviderRequestBoundExceeded,
        ProviderConfiguration,
        ProviderUnavailable,
        InvalidOrRepeatedToolCall,
        WorkerResultRejected,
        UnclassifiedSystemic,
        Unknown,
    }

    private sealed class T084ExecutorDiagnosticCapture(
        IPreparedPass1JobExecutor inner) : IPreparedPass1JobExecutor
    {
        private int _captured;

        public T084ExecutorReasonCategory Category { get; private set; } =
            T084ExecutorReasonCategory.Unknown;

        public async Task<PreparedPass1RawWorkerResult> ExecuteAsync(
            PreparedPass1WorkerPacket packet,
            string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            PreparedPass1RawWorkerResult result = await inner.ExecuteAsync(
                packet,
                systemPrompt,
                cancellationToken);
            if (Interlocked.Exchange(ref _captured, 1) == 0)
            {
                Category = Classify(result);
            }

            return result;
        }

        private static T084ExecutorReasonCategory Classify(
            PreparedPass1RawWorkerResult result)
        {
            if (result.Outcome == PreparedPass1RawWorkerOutcome.Completed)
            {
                return T084ExecutorReasonCategory.Completed;
            }

            return result.ReasonCode switch
            {
                "security-failure" => T084ExecutorReasonCategory.SecurityFailure,
                "integrity-failure" => T084ExecutorReasonCategory.IntegrityFailure,
                "provider-request-bound-exceeded" =>
                    T084ExecutorReasonCategory.ProviderRequestBoundExceeded,
                "provider-configuration" => T084ExecutorReasonCategory.ProviderConfiguration,
                "provider-unavailable" => T084ExecutorReasonCategory.ProviderUnavailable,
                "invalid-or-repeated-tool-call" =>
                    T084ExecutorReasonCategory.InvalidOrRepeatedToolCall,
                "worker-result-rejected" => T084ExecutorReasonCategory.WorkerResultRejected,
                "unclassified-systemic" => T084ExecutorReasonCategory.UnclassifiedSystemic,
                _ => T084ExecutorReasonCategory.Unknown,
            };
        }
    }

    /// <summary>
    /// Qualification-only transport capture. It retains only the HTTP status and
    /// short identifier-shaped DeepSeek error fields; request and response payloads,
    /// messages, headers, and credentials are never retained.
    /// </summary>
    private sealed class T084ProviderRejectionCapture()
        : DelegatingHandler(new SocketsHttpHandler())
    {
        private const int MaximumInspectableErrorBytes = 65_536;
        private readonly object _sync = new();
        private string _summary = "none";
        private string _requestSummary = "none";

        public string Summary
        {
            get
            {
                lock (_sync)
                {
                    return _summary + ",request-" + _requestSummary;
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                byte[] requestBody = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                HttpContent originalRequest = request.Content;
                var replacementRequest = new ByteArrayContent(requestBody);
                foreach (KeyValuePair<string, IEnumerable<string>> header in originalRequest.Headers)
                {
                    replacementRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                request.Content = replacementRequest;
                lock (_sync)
                {
                    _requestSummary = ReadSanitizedRequestShape(requestBody);
                }
            }

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            string type = "none";
            string code = "none";
            string parameter = "none";
            string fieldHint = "none";
            long? contentLength = response.Content?.Headers.ContentLength;
            if (response.Content is not null &&
                contentLength is >= 0 and <= MaximumInspectableErrorBytes)
            {
                byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                HttpContent original = response.Content;
                var replacement = new ByteArrayContent(body);
                foreach (KeyValuePair<string, IEnumerable<string>> header in original.Headers)
                {
                    replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                response.Content = replacement;
                (type, code, parameter, fieldHint) = ReadSanitizedErrorIdentifiers(body);
            }

            lock (_sync)
            {
                _summary =
                    $"status-{(int)response.StatusCode},type-{type},code-{code}," +
                    $"param-{parameter},fieldHint-{fieldHint}";
            }

            return response;
        }

        private static string ReadSanitizedRequestShape(byte[] body)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                JsonElement root = document.RootElement;
                string responseFormat = root.TryGetProperty("response_format", out JsonElement format) &&
                    format.ValueKind == JsonValueKind.Object &&
                    format.TryGetProperty("type", out JsonElement formatType)
                        ? ReadIdentifierValue(formatType)
                        : "absent";
                string toolChoice = root.TryGetProperty("tool_choice", out JsonElement choice)
                    ? ReadIdentifierValue(choice)
                    : "absent";
                string parallel = root.TryGetProperty("parallel_tool_calls", out JsonElement parallelCalls)
                    ? parallelCalls.GetRawText()
                    : "absent";
                List<string> schemas = [];
                if (root.TryGetProperty("tools", out JsonElement tools) &&
                    tools.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement tool in tools.EnumerateArray())
                    {
                        JsonElement function = tool.GetProperty("function");
                        string name = ReadIdentifierValue(function.GetProperty("name"));
                        byte[] parameters = Encoding.UTF8.GetBytes(
                            function.GetProperty("parameters").GetRawText());
                        schemas.Add(
                            $"{name}-{parameters.Length}-{Convert.ToHexString(SHA256.HashData(parameters))}");
                    }
                }

                return $"responseFormat-{responseFormat},toolChoice-{toolChoice}," +
                    $"parallel-{parallel},schemas-{string.Join('|', schemas)}";
            }
            catch (Exception exception) when (
                exception is JsonException or InvalidOperationException)
            {
                return "unreadable";
            }
        }

        private static (string Type, string Code, string Parameter, string FieldHint)
            ReadSanitizedErrorIdentifiers(byte[] body)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("error", out JsonElement error) &&
                    error.ValueKind == JsonValueKind.Object)
                {
                    return (
                        ReadIdentifier(error, "type"),
                        ReadIdentifier(error, "code"),
                        ReadIdentifier(error, "param"),
                        ReadFieldHint(error));
                }
            }
            catch (JsonException)
            {
            }

            return ("none", "none", "none", "none");
        }

        private static string ReadFieldHint(JsonElement error)
        {
            if (!error.TryGetProperty("message", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String)
            {
                return "none";
            }

            string message = value.GetString() ?? string.Empty;
            string[] knownFields =
            [
                "tool_choice",
                "response_format",
                "reasoning_content",
                "max_tokens",
                "thinking",
                "strict",
                "schema",
            ];
            string[] matches =
            [
                .. knownFields.Where(field =>
                    message.Contains(field, StringComparison.OrdinalIgnoreCase)),
            ];
            return matches.Length == 0 ? "other" : string.Join('+', matches);
        }

        private static string ReadIdentifier(JsonElement error, string propertyName)
        {
            if (!error.TryGetProperty(propertyName, out JsonElement value) ||
                value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return "none";
            }

            string? candidate = value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : value.GetRawText();
            return candidate is { Length: > 0 and <= 64 } &&
                candidate.All(character =>
                    char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.')
                    ? candidate
                    : "redacted";
        }

        private static string ReadIdentifierValue(JsonElement value)
        {
            string? candidate = value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : value.GetRawText();
            return candidate is { Length: > 0 and <= 64 } &&
                candidate.All(character =>
                    char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.')
                    ? candidate
                    : "redacted";
        }
    }

    private sealed record DocumentGraphEvidence(
        string ContentFingerprint,
        string CommitFingerprint,
        int SourceAnchorCount,
        int SectionCount,
        int IndexEntryCount);

    private sealed record IsolatedJobEvidence(
        int QualificationOrdinal,
        int PairSelectionOrdinal,
        string SourceKind,
        int SourceUtf8Bytes,
        string SourceSha256,
        string CanonicalResultSha256,
        string CommitFingerprint,
        int SourceAnchorCount,
        int SectionCount,
        int IndexEntryCount,
        string ExecutorReasonCategory,
        string? ExecutionDiagnosticCategory,
        string? SubmissionDiagnosticCategory,
        string? WireBudgetCategory);

    private sealed record PairBinding(
        Guid PairId,
        Guid[] RepositoryIds,
        IReadOnlyList<PreparedRevisionRequest> RevisionRequests);
}

internal sealed class T084IsolatedSourceJobsFactAttribute : FactAttribute
{
    public T084IsolatedSourceJobsFactAttribute()
    {
        Skip = !string.Equals(
            Environment.GetEnvironmentVariable("VULGATA_T084_ISOLATED_SOURCE_JOBS"),
            "1",
            StringComparison.Ordinal)
            ? "T084 isolated source jobs are opt-in; set VULGATA_T084_ISOLATED_SOURCE_JOBS=1."
            : Pass1ProductionExecutionTests.HostQualificationSkipReason();
    }
}
