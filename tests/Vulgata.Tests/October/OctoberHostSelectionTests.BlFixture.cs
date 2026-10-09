using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Configuration;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.October.Integration;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October;

public sealed partial class OctoberHostSelectionTests
{
    internal sealed partial class GeneratorPrepareFixture
    {
        private static int _blInstallation;
        private int _blLaunched;
        private ApprovedBlPreparedInputs? _blPrepared;
        private ApprovedBlGenerateResult? _blResult;
        private OctoberOperatorRootSelectionRequest? _blOperatorRequest;
        private static readonly JsonSerializerOptions BlJson = new(JsonSerializerDefaults.Web)
        { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };

        internal sealed record ApprovedBlInputBundle(OctoberProviderProfile Profile, OctoberScanPolicy Policy,
            string RuntimeFilePath, string ScanPolicyFilePath, string BusinessSettingsFilePath,
            Func<CancellationToken, Task<string>> ReadApprovedCredential);
        internal sealed record ApprovedBlPreparedInputs(KnowledgeBinding Binding, ApprovedClSource Source,
            OctoberProviderProfile Profile, OctoberScanPolicy Policy, OctoberGenerateInputFiles Files,
            string BusinessSettingsFilePath);
        internal sealed record ApprovedBlGenerateResult(int ExitCode, string StandardOutput, string StandardError,
            OctoberOperatorRootSelectionRequest? Request, OctoberOperatorRootSelectionResponse? Response, string? InteractionFailure);

        private void RequireBlGrant()
        {
            if (!SuiteAuthorization.IsEnabled("E2E") || Environment.GetEnvironmentVariable("VULGATA_TEST_RESOURCE_RUN") != _runId
                || Environment.GetEnvironmentVariable("VULGATA_OCTOBER_OPERATION_BL") != "account-policy-cl-bl-v1")
                throw new InvalidOperationException("Explicit owned BL qualification approval is required.");
            TestResourceScope.ValidateOwnedTarget(ConnectionString);
        }

        internal async Task<ApprovedBlPreparedInputs> PrepareApprovedBlInputsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireBlGrant();
            if (Interlocked.CompareExchange(ref _blInstallation, 1, 0) != 0)
                throw new InvalidOperationException("The approved BL qualification is single-use in this process.");
            lock (_readerLock)
            {
                if (_prepareClStarted != 0 || _approvedClInputs is not null) throw new InvalidOperationException("The fixture is already prepared.");
                _prepareClStarted = 1;
            }
            var inputs = await CreateApprovedBlInputsAsync(cancellationToken);
            string sourcePath = Path.Combine(OwnedSourceRoot, "AccountPolicy.java");
            _ = OctoberGenerateFixtureInputs.ValidateFiles(OwnedRoot, "generate", inputs.RuntimeFilePath,
                inputs.ScanPolicyFilePath, sourcePath, false);
            string digest = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(sourcePath, cancellationToken)));
            var source = new ApprovedClSource(RepositoryId, CreatePrepareRequest().Repositories.Single().RootBindingIdentity,
                "AccountPolicy.java", digest);
            string credentialFile = await WriteGenerateWorkerProviderAsync(inputs.Profile, inputs.ReadApprovedCredential, cancellationToken);
            using (var process = StartGenerator(JsonSerializer.Serialize(CreatePrepareRequest())))
            {
                var prepared = await WaitForGeneratorAsync(process);
                if (prepared.ExitCode != 0) throw new InvalidOperationException("Owned BL batch preparation failed.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await ReadBoundSnapshotAsync();
            var binding = new KnowledgeBinding(CanonicalDatabaseIdentity, TargetId, BatchId, snapshot.Request.SelectionDigest);
            await using var context = CreateOctoberContext();
            if (await context.KnowledgeOperations.AnyAsync(x => x.BatchId == BatchId, cancellationToken)
                || await context.PreparedSourceApprovals.AnyAsync(x => x.BatchId == BatchId, cancellationToken)
                || await context.AcceptedFileManifests.AnyAsync(x => x.BatchId == BatchId, cancellationToken))
                throw new InvalidOperationException("BL qualification requires a fresh ungenerated batch.");
            _preparedClExpectation = (binding, source);
            return _blPrepared = new(binding, source, inputs.Profile, inputs.Policy,
                new(inputs.RuntimeFilePath, inputs.ScanPolicyFilePath, credentialFile), inputs.BusinessSettingsFilePath);
        }

        private async Task<ApprovedBlInputBundle> CreateApprovedBlInputsAsync(CancellationToken token)
        {
            string endpoint = Environment.GetEnvironmentVariable("VULGATA_OCTOBER_BL_LLM_ENDPOINT") ?? "";
            string model = Environment.GetEnvironmentVariable("VULGATA_OCTOBER_BL_LLM_MODEL") ?? "";
            if (endpoint != "https://api.deepseek.com" && endpoint != "https://api.deepseek.com/" || model != "deepseek-flash")
                throw new InvalidOperationException("BL provider differs from its explicit approval.");
            if (Directory.EnumerateFileSystemEntries(OwnedSourceRoot).Any())
                throw new InvalidOperationException("BL source directory must be fresh.");
            const string graph = @"G:\packages\npm\prefix\codegraph.ps1";
            const string jdt = @"G:\source\repos\eclipse.jdt.ls\org.eclipse.jdt.ls.product\target\repository";
            const string java = @"C:\Program Files\Java\jdk-21.0.12\bin";
            string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string cmd = Path.Combine(system, "cmd.exe");
            if (!File.Exists(graph) || !File.Exists(Path.Combine(java, "java.exe")) || !File.Exists(cmd)
                || !File.Exists(Path.Combine(local, "jdtls.bat")) || !File.Exists(Path.Combine(local, "python.exe"))
                || !File.Exists(Path.Combine(jdt, "bin", "jdtls.py")) || !Directory.Exists(Path.Combine(jdt, "plugins")))
                throw new InvalidOperationException("Approved BL native prerequisites are unavailable.");
            const string source = """
                public final class AccountPolicy {
                    private AccountPolicy() { }

                    public static boolean canRegister(boolean phoneVerified, boolean accountExists) {
                        return phoneVerified && !accountExists;
                    }

                    public static boolean canLogin(boolean accountActive, boolean passwordMatches, int failedAttempts) {
                        if (!accountActive || failedAttempts >= 5) {
                            return false;
                        }
                        return passwordMatches;
                    }
                }
                """;
            var policy = new OctoberScanPolicy
            {
                OutputStage = "cl-and-bl", WorkerCap = 1, RunDeadlineSeconds = 900, FileDeadlineSeconds = 480,
                GracefulDrainSeconds = 30, MaximumContinuationWindows = 3, MaximumModelRequestsPerFile = 3,
                MaximumTransientRetries = 0, MaximumProviderRequests = 8, MaximumRequestsPerMinute = 8,
                MaximumTotalTokens = 48000,
                SourceRules = [new(RepositoryId, "AccountPolicy.java", "selected", "Explicit approved synthetic BL qualification")]
            };
            string state = Path.Combine(OwnedRoot, "bl-state"), logs = Path.Combine(OwnedRoot, "bl-logs");
            string[] executables = [local, java, system];
            var runtime = new OctoberRuntimeConfiguration(
                [new(TargetId, BatchId, RepositoryId, CreatePrepareRequest().Repositories.Single().RootBindingIdentity, OwnedSourceRoot)], graph,
                new PreparedJavaNavigationProfile(state, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(15), 2 * 1024 * 1024),
                new PreparedJdtHostPolicy(executables, [jdt, Directory.GetParent(java)!.FullName], cmd, executables, java), logs);
            var settings = new OctoberBusinessSettings([], new FlowPlanningLimits(16, 32, 4), 8000);
            if (!new OctoberScanPolicyValidator().Validate(policy).IsValid || !runtime.IsValid() || !settings.IsValid())
                throw new InvalidOperationException("Approved BL configuration is invalid.");
            Directory.CreateDirectory(state); Directory.CreateDirectory(logs);
            string runtimeFile = Path.Combine(OwnedRoot, "bl-runtime.json"), policyFile = Path.Combine(OwnedRoot, "bl-policy.json"),
                businessFile = Path.Combine(OwnedRoot, "bl-business.json");
            await File.WriteAllTextAsync(Path.Combine(OwnedSourceRoot, "AccountPolicy.java"), source, new UTF8Encoding(false, true), token);
            await File.WriteAllTextAsync(runtimeFile, JsonSerializer.Serialize(runtime, BlJson), token);
            await File.WriteAllTextAsync(policyFile, JsonSerializer.Serialize(policy, BlJson), token);
            await File.WriteAllTextAsync(businessFile, JsonSerializer.Serialize(settings, BlJson), token);
            return new(new("approved-bl-worker", ApiType.ChatCompletions, endpoint, model, "approved-bl-key"), policy,
                runtimeFile, policyFile, businessFile, cancellation =>
                {
                    cancellation.ThrowIfCancellationRequested(); RequireBlGrant();
                    return Task.FromResult(Environment.GetEnvironmentVariable("VULGATA_OCTOBER_BL_LLM_KEY") ?? "");
                });
        }

        internal async Task<ApprovedBlGenerateResult> RunApprovedBlGenerateAsync(ApprovedBlPreparedInputs inputs,
            Func<OctoberOperatorRootSelectionRequest, CancellationToken, Task<ImmutableArray<Guid>>> selectRoots,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); RequireBlGrant(); ArgumentNullException.ThrowIfNull(selectRoots);
            if (!ReferenceEquals(inputs, _blPrepared) || Interlocked.CompareExchange(ref _blLaunched, 1, 0) != 0)
                throw new InvalidOperationException("Only the original single BL run may launch.");
            string exchange = Path.Combine(OwnedRoot, "bl-operator-exchange");
            using var process = StartGenerator("generate", null, runtimeFilePath: inputs.Files.RuntimeFilePath,
                scanPolicyFilePath: inputs.Files.ScanPolicyFilePath, workerProviderConfigurationFilePath: inputs.Files.WorkerProviderConfigurationFilePath,
                businessSettingsFilePath: inputs.BusinessSettingsFilePath, businessRootSelectionDirectory: exchange,
                businessRootSelectionTimeoutSeconds: 120);
            // Sole stdout/stderr consumer, started before any operator wait.
            var terminal = WaitForGenerateAsync(process, inputs.Policy.RunDeadlineSeconds);
            using var interactionStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            interactionStop.CancelAfter(TimeSpan.FromSeconds(inputs.Policy.RunDeadlineSeconds));
            var interaction = ExchangeBlRootsAsync(exchange, inputs, selectRoots, interactionStop.Token);
            try
            {
                await Task.WhenAny(terminal, interaction);
                if (terminal.IsCompleted) interactionStop.Cancel();
                var raw = await terminal;
                interactionStop.Cancel();
                var receipt = await interaction;
                return _blResult = new(raw.ExitCode, raw.StandardOutput, raw.StandardError, receipt.Request, receipt.Response, receipt.Failure);
            }
            finally
            {
                interactionStop.Cancel();
                await interaction;
            }
        }

        private async Task<(OctoberOperatorRootSelectionRequest? Request, OctoberOperatorRootSelectionResponse? Response, string? Failure)>
            ExchangeBlRootsAsync(string directory, ApprovedBlPreparedInputs inputs,
                Func<OctoberOperatorRootSelectionRequest, CancellationToken, Task<ImmutableArray<Guid>>> select, CancellationToken token)
        {
            OctoberOperatorRootSelectionRequest? request = null;
            OctoberOperatorRootSelectionResponse? response = null;
            string temporary = Path.Combine(directory, "operator-response.tmp");
            bool ownsTemporary = false;
            bool published = false;
            string stage = "waiting-request";
            var elapsed = Stopwatch.StartNew();
            void Report(string reason) => _output.WriteLine(
                $"bl-operator: stage={stage}; reason={reason}; request-present={request is not null}; response-present={response is not null}; response-published={published}; candidates={(request is null || request.Candidates.IsDefault ? 0 : request.Candidates.Length)}; elapsed-ms={elapsed.ElapsedMilliseconds}");
            try
            {
                string path = Path.Combine(directory, "root-selection.request.json");
                while (!File.Exists(path)) { token.ThrowIfCancellationRequested(); await Task.Delay(25, token); }
                stage = "contained-file-check";
                _ = OctoberGenerateFixtureInputs.ValidateFiles(OwnedRoot, "generate", inputs.Files.RuntimeFilePath,
                    inputs.Files.ScanPolicyFilePath, path, false);
                stage = "request-read-parse";
                await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (stream.Length is <= 0 or > 1024 * 1024) throw new InvalidOperationException();
                    byte[] bytes = new byte[(int)stream.Length]; await stream.ReadExactlyAsync(bytes, token);
                    using var json = JsonDocument.Parse(bytes);
                    RejectDuplicateBlProperties(json.RootElement);
                    request = json.RootElement.Deserialize<OctoberOperatorRootSelectionRequest>(BlJson) ?? throw new InvalidOperationException();
                }
                stage = "request-shape";
                if (request.Version != 1 || request.RequestId == Guid.Empty || request.RunId == Guid.Empty || request.CorpusId == Guid.Empty
                    || request.ExpiresAtUtc <= DateTimeOffset.UtcNow || request.Candidates.IsDefaultOrEmpty || request.Candidates.Length > 1024
                    || request.Candidates.Any(x => x is null || x.UnitId == Guid.Empty || x.Document is null
                        || x.Document.BatchId != BatchId || x.RepositoryId != RepositoryId || x.RelativePath != inputs.Source.RelativePath)
                    || request.Candidates.Select(x => x.UnitId).Distinct().Count() != request.Candidates.Length)
                    throw new InvalidOperationException();
                stage = "source-provenance";
                _ = await ResolveGeneratedOriginAsync(token);
                await using (var context = CreateOctoberContext())
                {
                    stage = "corpus-binding";
                    var corpus = await context.CorpusSelections.AsNoTracking().SingleAsync(x => x.BatchId == BatchId && x.CorpusId == request.CorpusId, token);
                    foreach (var candidate in request.Candidates)
                    {
                        stage = "candidate-corpus-binding";
                        if (!await context.CorpusDocuments.AsNoTracking().AnyAsync(x => x.BatchId == BatchId && x.CorpusId == corpus.CorpusId
                            && x.LogicalDocumentId == candidate.Document.LogicalDocumentId && x.VersionId == candidate.Document.DocumentVersionId, token))
                            throw new InvalidOperationException();
                        stage = "candidate-unit-binding";
                        var logical = await context.LogicalDocuments.AsNoTracking().SingleAsync(x => x.BatchId == BatchId
                            && x.LogicalDocumentId == candidate.Document.LogicalDocumentId, token);
                        var unit = await context.CodeUnits.AsNoTracking().SingleAsync(x => x.BatchId == BatchId && x.UnitId == candidate.UnitId, token);
                        var manifest = await context.AcceptedFileManifests.AsNoTracking().SingleAsync(x => x.BatchId == BatchId && x.ManifestId == unit.ManifestId, token);
                        if (logical.Kind != DocumentKind.CodeLogic || logical.UnitId != candidate.UnitId || unit.RepositoryId != RepositoryId
                            || unit.SemanticSelector != candidate.SemanticSelector || manifest.RelativePath != inputs.Source.RelativePath
                            || manifest.RootBindingIdentity != inputs.Source.RootBindingIdentity || manifest.SourceDigest != inputs.Source.SourceDigest)
                            throw new InvalidOperationException();
                    }
                }
                stage = "operator-callback";
                using var expiry = CancellationTokenSource.CreateLinkedTokenSource(token);
                expiry.CancelAfter(request.ExpiresAtUtc - DateTimeOffset.UtcNow);
                _blOperatorRequest = request;
                var ids = await select(request, expiry.Token);
                expiry.Token.ThrowIfCancellationRequested();
                stage = "response-validation";
                if (ids.IsDefault || ids.Length != 1 || !request.Candidates.Any(x => x.UnitId == ids[0])) throw new InvalidOperationException();
                response = new(1, request.RequestId, request.RunId, ids);
                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(response, BlJson);
                if (payload.Length > 64 * 1024) throw new InvalidOperationException();
                stage = "response-temp-write";
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { ownsTemporary = true; await output.WriteAsync(payload, expiry.Token); await output.FlushAsync(expiry.Token); }
                expiry.Token.ThrowIfCancellationRequested();
                stage = "response-atomic-publish";
                File.Move(temporary, Path.Combine(directory, "root-selection.response.json"), false);
                ownsTemporary = false;
                published = true;
                Report("completed");
                return (request, response, null);
            }
            catch (OperationCanceledException) { Report("cancelled-or-expired"); return (request, response, "operator-cancelled-or-expired"); }
            catch (Exception) { Report("rejected"); return (request, response, "operator-exchange-rejected"); }
            finally { _blOperatorRequest = null; if (ownsTemporary && File.Exists(temporary)) File.Delete(temporary); }
        }

        /// <summary>Explicit operator choice for this fixed synthetic method, not generic root discovery.</summary>
        internal async Task<Guid> ResolveApprovedRegistrationRootAsync(OctoberOperatorRootSelectionRequest request,
            CancellationToken cancellationToken = default)
        {
            string stage = "held-request";
            string reason = "rejected";
            int inspected = 0, invalidOrigin = 0, withinMethod = 0, coversDeclaration = 0, coversReturn = 0, eligible = 0;
            var elapsed = Stopwatch.StartNew();
            void Report() => _output.WriteLine($"bl-registration-selector: stage={stage}; reason={reason}; offered={(request is null || request.Candidates.IsDefault ? 0 : request.Candidates.Length)}; inspected={inspected}; invalid-origin={invalidOrigin}; within-method={withinMethod}; covers-declaration={coversDeclaration}; covers-return={coversReturn}; eligible={eligible}; elapsed-ms={elapsed.ElapsedMilliseconds}");
            try
            {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(request, _blOperatorRequest) || _blPrepared is not { } prepared
                || request.ExpiresAtUtc <= DateTimeOffset.UtcNow || prepared.Source.RelativePath != "AccountPolicy.java")
                throw new InvalidOperationException("No held approved synthetic operator request exists.");
            stage = "source-validation";
            string path = Path.Combine(OwnedSourceRoot, prepared.Source.RelativePath);
            _ = OctoberGenerateFixtureInputs.ValidateFiles(OwnedRoot, "generate", prepared.Files.RuntimeFilePath,
                prepared.Files.ScanPolicyFilePath, path, false);
            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            string[] lines = new UTF8Encoding(false, true).GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            if (Convert.ToHexString(SHA256.HashData(bytes)) != prepared.Source.SourceDigest)
                throw new InvalidOperationException("The approved synthetic registration source has changed.");
            stage = "method-snippet";
            int[] declarations = Enumerable.Range(0, Math.Max(0, lines.Length - 2)).Where(index =>
                lines[index].Trim() == "public static boolean canRegister(boolean phoneVerified, boolean accountExists) {"
                && lines[index + 1].Trim() == "return phoneVerified && !accountExists;"
                && lines[index + 2].Trim() == "}").ToArray();
            if (declarations.Length != 1)
                throw new InvalidOperationException("The approved synthetic registration method is absent or ambiguous.");
            int declarationLine = declarations[0] + 1, returnLine = declarationLine + 1, endLine = declarationLine + 2;
            stage = "source-provenance";
            PreparedOrigin origin = await ResolveGeneratedOriginAsync(cancellationToken);
            await using var context = CreateOctoberContext();
            List<Guid> matches = [];
            foreach (var candidate in request.Candidates)
            {
                stage = "candidate-document";
                inspected++;
                var corpusDocument = await context.CorpusDocuments.AsNoTracking().SingleAsync(x => x.BatchId == BatchId
                    && x.CorpusId == request.CorpusId && x.LogicalDocumentId == candidate.Document.LogicalDocumentId
                    && x.VersionId == candidate.Document.DocumentVersionId, cancellationToken);
                var version = await context.DocumentVersions.AsNoTracking().SingleAsync(x => x.BatchId == BatchId
                    && x.LogicalDocumentId == candidate.Document.LogicalDocumentId && x.VersionId == candidate.Document.DocumentVersionId,
                    cancellationToken);
                if (version.Kind != DocumentKind.CodeLogic || version.ManifestId != corpusDocument.ManifestId)
                    throw new InvalidOperationException("The offered candidate is not exact accepted Code Logic.");
                stage = "candidate-anchors";
                var anchors = JsonSerializer.Deserialize<ImmutableArray<Vulgata.Core.DomainServices.OctoberKnowledge.SourceAnchor>>(version.AnchorsJson, BlJson);
                if (anchors.IsDefaultOrEmpty || anchors.Any(anchor => anchor is null || anchor.Origin != origin))
                {
                    invalidOrigin++;
                    reason = "candidate-origin-invalid";
                    throw new InvalidOperationException("The offered candidate has unexpected source origins.");
                }
                if (anchors.All(anchor => anchor.StartLine >= declarationLine && anchor.EndLine <= endLine && anchor.StartLine <= anchor.EndLine)) withinMethod++;
                if (anchors.Any(anchor => anchor.StartLine <= declarationLine && anchor.EndLine >= declarationLine)) coversDeclaration++;
                if (anchors.Any(anchor => anchor.StartLine <= returnLine && anchor.EndLine >= returnLine)) coversReturn++;
                if (anchors.All(anchor => anchor.StartLine >= declarationLine && anchor.EndLine <= endLine && anchor.StartLine <= anchor.EndLine)
                    && anchors.Any(anchor => anchor.StartLine <= declarationLine && anchor.EndLine >= declarationLine)
                    && anchors.Any(anchor => anchor.StartLine <= returnLine && anchor.EndLine >= returnLine))
                { matches.Add(candidate.UnitId); eligible++; }
            }
            stage = "unique-registration-root";
            if (matches.Count != 1)
            {
                reason = matches.Count == 0 ? "registration-absent" : "registration-ambiguous";
                throw new InvalidOperationException("The offered registration root is absent or ambiguous.");
            }
            reason = "selected";
            Report();
            return matches[0];
            }
            catch (OperationCanceledException) { reason = "cancelled-or-expired"; Report(); throw; }
            catch (Exception) { Report(); throw; }
        }

        private static void RejectDuplicateBlProperties(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                { if (!names.Add(property.Name)) throw new InvalidOperationException(); RejectDuplicateBlProperties(property.Value); }
            }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var child in element.EnumerateArray()) RejectDuplicateBlProperties(child);
        }

        internal async Task<PublicationBinding> ResolveGeneratedBlPublicationAsync(ApprovedBlGenerateResult result,
            CancellationToken cancellationToken = default)
        {
            if (!ReferenceEquals(result, _blResult) || result.ExitCode != 0 || result.InteractionFailure is not null
                || result.Request is not { } request || result.Response is not { } response || _blPrepared is null)
                throw new InvalidOperationException("No successful original BL result is available.");
            _ = await ResolveGeneratedOriginAsync(cancellationToken);
            await using var context = CreateOctoberContext();
            var finalization = await context.BusinessFinalizations.AsNoTracking().SingleAsync(x => x.BatchId == BatchId, cancellationToken);
            var persisted = JsonSerializer.Deserialize<FlowFinalizationPublicationRequest>(finalization.RequestJson, BlJson)
                ?? throw new InvalidOperationException("Missing original finalization request.");
            if (finalization.CorpusId != request.CorpusId || finalization.OperationKind != OperationKind.FinalizeCorpus
                || finalization.PublicationId is null || persisted.Corpus.Binding != _blPrepared.Binding
                || persisted.Operation.BatchId != BatchId || persisted.Operation.Kind != OperationKind.FinalizeCorpus
                || persisted.Operation.OperationId != finalization.OperationId
                || persisted.Corpus.CorpusId != request.CorpusId || persisted.Selection.RunId != request.RunId
                || !persisted.Selection.Roots.Select(x => x.UnitId).Order().SequenceEqual(response.SelectedUnitIds.Order()))
                throw new InvalidOperationException("Original BL finalization binding is inconsistent.");
            var publication = await context.KnowledgePublications.AsNoTracking().SingleAsync(x => x.BatchId == BatchId
                && x.PublicationId == finalization.PublicationId && x.CorpusId == request.CorpusId
                && x.OperationId == finalization.OperationId && x.OperationKind == OperationKind.FinalizeCorpus, cancellationToken);
            var binding = new PublicationBinding(_blPrepared.Binding, publication.PublicationId);
            _ = await LoadExactPublicationAsync(binding, cancellationToken);
            return binding;
        }
    }
}
