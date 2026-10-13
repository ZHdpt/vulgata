using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Configuration;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Web.Data;

namespace Vulgata.OctoberGenerator;

public static class OctoberGeneratorEntryPoint
{
    internal static int ReadWorkerMaxOutputTokens(IConfiguration configuration)
    {
        const string key = "October:WorkerMaxOutputTokens";
        string? value = configuration[key];
        if (configuration.GetSection(key).GetChildren().Any())
            throw new InvalidOperationException("October Worker token configuration is invalid.");
        if (value is null) return 8192;
        if (!int.TryParse(value, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out int maximum) || maximum is < 1 or > 393216)
            throw new InvalidOperationException("October Worker token configuration is invalid.");
        return maximum;
    }

    internal static string? ReadDeepSeekWorkerReasoningEffort(IConfiguration configuration)
    {
        const string key = "October:DeepSeekWorkerReasoningEffort";
        string? value = configuration[key];
        if (configuration.GetSection(key).GetChildren().Any() || value is not (null or "low"))
            throw new InvalidOperationException("October Worker reasoning configuration is invalid.");
        return value;
    }

    internal static OctoberWorkerHttpObservationOptions? ReadWorkerHttpObservationOptions(IConfiguration configuration,
        OctoberGeneratorCommand command, OctoberParseFailureCaptureOptions? captureOptions)
    {
        const string prefix = "October:WorkerHttpObservation";
        var children = configuration.GetSection(prefix).GetChildren().ToArray();
        if (children.Length == 0 && configuration[prefix] is null) return null;
        try
        {
            static string BooleanText(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "true" :
                string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ? "false" : throw new InvalidOperationException();
            string[] names = ["Enabled", "TargetId", "BatchId", "SubjectId", "ManagementAuthorityId"];
            if (configuration[prefix] is not null || children.Any(item => !names.Contains(item.Key, StringComparer.Ordinal) || item.GetChildren().Any()))
                throw new InvalidOperationException();
            if (configuration is IConfigurationRoot root)
                foreach (string name in names)
                    if (root.Providers.Select(provider => provider.TryGet(prefix + ":" + name, out string? value) ? value : null)
                        .Select(value => name == "Enabled" && value is not null ? BooleanText(value) : value)
                        .Where(value => value is not null).Distinct(StringComparer.Ordinal).Skip(1).Any()) throw new InvalidOperationException();
            string enabled = BooleanText(configuration[prefix + ":Enabled"]);
            if (enabled == "false" && children.Length == 1) return null;
            if (enabled != "true" || children.Length != names.Length || captureOptions is not null || command.Mode != OctoberGeneratorMode.Generate)
                throw new InvalidOperationException();
            string Exact(string name)
            {
                string value = configuration[prefix + ":" + name] ?? throw new InvalidOperationException();
                if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Trim() != value || value.Any(char.IsControl)) throw new InvalidOperationException();
                return value;
            }
            string target = Exact("TargetId");
            if (target != command.TargetId || !Guid.TryParseExact(Exact("BatchId"), "D", out Guid batch) || batch == Guid.Empty || batch != command.BatchId)
                throw new InvalidOperationException();
            return new(target, batch, Exact("SubjectId"), Exact("ManagementAuthorityId"));
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        { throw new InvalidOperationException("October Worker HTTP observation configuration is invalid."); }
    }

    internal static OctoberParseFailureCaptureOptions? ReadParseFailureCaptureOptions(IConfiguration configuration,
        OctoberGeneratorCommand command, OctoberRuntimeConfiguration? runtime, string keyRingPath)
    {
        const string prefix = "October:ParseFailureCapture";
        var children = configuration.GetSection(prefix).GetChildren().ToArray();
        if (children.Length == 0 && configuration[prefix] is null) return null;
        try
        {
            static string BooleanText(string? value) =>
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "true" :
                string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ? "false" : throw new InvalidOperationException();
            string[] names = ["Enabled", "TargetId", "BatchId", "SubjectId", "ManagementAuthorityId", "Directory"];
            if (configuration[prefix] is not null || children.Any(item => !names.Contains(item.Key, StringComparer.Ordinal) || item.GetChildren().Any()))
                throw new InvalidOperationException();
            if (configuration is IConfigurationRoot root)
                foreach (string name in names)
                    if (root.Providers.Select(provider => provider.TryGet(prefix + ":" + name, out string? value) ? value : null)
                        .Select(value => name == "Enabled" && value is not null ? BooleanText(value) : value)
                        .Where(value => value is not null).Distinct(StringComparer.Ordinal).Skip(1).Any()) throw new InvalidOperationException();
            string enabled = BooleanText(configuration[prefix + ":Enabled"]);
            if (enabled == "false" && children.Length == 1) return null;
            if (enabled != "true" || children.Length != names.Length || command.Mode != OctoberGeneratorMode.Generate || runtime is null)
                throw new InvalidOperationException();
            string Exact(string name)
            {
                string value = configuration[prefix + ":" + name] ?? throw new InvalidOperationException();
                if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Trim() != value || value.Any(char.IsControl))
                    throw new InvalidOperationException();
                return value;
            }
            string target = Exact("TargetId");
            if (target != command.TargetId || !Guid.TryParseExact(Exact("BatchId"), "D", out Guid batch) || batch != command.BatchId)
                throw new InvalidOperationException();
            string directory = Exact("Directory");
            string[] exclusions = CaptureExcludedPaths(configuration, runtime, keyRingPath);
            if (!OctoberParseFailureCapture.SafePath(directory) || exclusions.Any(path => !OctoberParseFailureCapture.SafePath(path, allowRoot: true)) ||
                exclusions.Any(path => OctoberParseFailureCapture.Overlaps(directory, path))) throw new InvalidOperationException();
            return new(target, batch, Exact("SubjectId"), Exact("ManagementAuthorityId"), directory);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or NotSupportedException or IOException)
        { throw new InvalidOperationException("October parse capture configuration is invalid."); }
    }

    private static string[] CaptureExcludedPaths(IConfiguration configuration, OctoberRuntimeConfiguration runtime, string keyRingPath)
        => runtime.PreparedRoots.Select(root => root.PhysicalRootPath)
            .Concat([runtime.JavaNavigation.DisposableStateRoot, runtime.LogDirectory, keyRingPath,
                runtime.JavaHostPolicy.JavaBinDirectory, Path.GetDirectoryName(runtime.CodeGraphCommandPath)!,
                Path.GetDirectoryName(runtime.JavaHostPolicy.FixedCmdAbsolutePath)!])
            .Concat(runtime.JavaHostPolicy.DeclaredRuntimeReadRoots)
            .Concat(runtime.JavaHostPolicy.ApprovedExecutableDirectories)
            .Concat(runtime.JavaHostPolicy.FixedChildPathEntries)
            .Concat(new[] { "October:Operator:CredentialFile", "October:Providers:ConfigurationFile", "October:RuntimeFile",
                "October:ScanPolicyFile", "October:BusinessSettingsFile", "October:BusinessRootSelectionDirectory" }
                .Select(key => configuration[key]).OfType<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    internal static OctoberOperatorRootSelectionOptions? ReadOperatorRootSelectionOptions(IConfiguration configuration,
        OctoberScanPolicy policy, OctoberBusinessSettings? settings, OctoberRuntimeConfiguration runtime)
    {
        if (policy.OutputStage == "cl-only") return null;
        string mode = configuration["October:BusinessRootSelectionMode"] ?? "exact-settings";
        if (policy.OutputStage != "cl-and-bl" || settings is null || !settings.IsValid())
            throw new InvalidOperationException("October operator root configuration is invalid.");
        if (mode == "exact-settings") return new();
        string? directory = configuration["October:BusinessRootSelectionDirectory"];
        if (mode != "operator" || !settings.Roots.IsEmpty || string.IsNullOrWhiteSpace(directory) ||
            directory.Trim() != directory || !Path.IsPathFullyQualified(directory) || directory.Any(char.IsControl) ||
            !int.TryParse(configuration["October:BusinessRootSelectionTimeoutSeconds"],
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int timeout) || timeout is < 1 or > 300)
            throw new InvalidOperationException("October operator root configuration is invalid.");
        directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        string? parent = Path.GetDirectoryName(directory);
        if (parent is null || !Directory.Exists(parent) || Path.Exists(directory) ||
            runtime.PreparedRoots.Any(root => OperatorPathsOverlap(root.PhysicalRootPath, directory)) ||
            OperatorPathsOverlap(runtime.LogDirectory, directory) || OperatorPathsOverlap(runtime.JavaNavigation.DisposableStateRoot, directory))
            throw new InvalidOperationException("October operator directory is unavailable.");
        return new(mode, directory, timeout);
    }

    private static bool OperatorPathsOverlap(string left, string right)
    {
        left = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
        right = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ||
            left.StartsWith(Path.EndsInDirectorySeparator(right) ? right : right + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            right.StartsWith(Path.EndsInDirectorySeparator(left) ? left : left + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    internal static void AddBusinessReaderDependencies(IServiceCollection services, string ownedLogDirectory,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IDbContextFactory<OctoberDbContext>>(provider =>
            new ConfiguredContextFactory(provider.GetRequiredService<DbContextOptions<OctoberDbContext>>()));
        services.AddSingleton<IOctoberKnowledgeTokenProtector, OctoberKnowledgeDataProtectionTokenProtector>();
        services.AddSingleton<IOctoberKnowledgeReadLog>(new OctoberKnowledgeOperationLog(ownedLogDirectory, timeProvider));
    }

    internal static OctoberBusinessRunComposition? ComposeBusiness(OctoberScanPolicy policy,
        OctoberSelectedWorkerProvider provider, OctoberBusinessSettings? settings,
        IDbContextFactory<OctoberDbContext> contexts, IOctoberKnowledgeOperationLog log,
        Func<IOctoberOperatorSession, PublicationBinding, CancellationToken, Task<OctoberVerifiedReaderBinding?>>? bindReader,
        OctoberOperatorRootSelectionOptions? operatorSelection = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.OutputStage == "cl-only") return null;
        if (policy.OutputStage != "cl-and-bl" || !new OctoberScanPolicyValidator().Validate(policy).IsValid ||
            settings is null || !settings.IsValid() || settings.MaximumAttemptTokens > policy.MaximumTotalTokens ||
            settings.ContextOverride is not null && policy.ExistingClPublicationId is null ||
            settings.ResolutionPolicyVersion is not null && policy.ExistingClPublicationId is null ||
            provider is null || provider.Precedence != "only-source" || provider.Profile.ApiType != ApiType.ChatCompletions ||
            contexts is null || log is null || bindReader is null)
            throw new InvalidOperationException("October Business composition is invalid.");
        if (operatorSelection is not null && operatorSelection.Mode != "exact-settings" &&
            (operatorSelection.Mode != "operator" || !settings.Roots.IsEmpty || operatorSelection.TimeoutSeconds is not (>= 1 and <= 300)))
            throw new InvalidOperationException("October Business operator selection is invalid.");
        var profile = provider.Profile;
        var projection = new BusinessProviderConfigurationProjection(provider.SourceIdentity, 0, profile.Name,
            "chat-completions", profile.Endpoint, profile.Model, profile.CredentialReference);
        _ = OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(projection);
        return new(
            (authorization, guard) => new OctoberResolutionStore(contexts, authorization, guard, log, settings.ResolutionPolicyVersion),
            authorization => new OctoberFlowPlanningInputLoader(contexts, authorization, settings.ResolutionPolicyVersion),
            (authorization, guard) => new OctoberFlowPublicationStore(contexts, authorization, guard, log),
            settings.SelectRoots, projection, LoadBusinessInstructions("Resolution"), LoadBusinessInstructions("Synthesis"),
            settings.Limits, settings.MaximumAttemptTokens)
        {
            BindReader = bindReader, OperatorSelection = operatorSelection, ContextOverride = settings.ContextOverride,
            ResolutionPolicyVersion = settings.ResolutionPolicyVersion,
            InteractAsync = operatorSelection?.Mode == "operator"
                ? new OctoberOperatorRootSelection(operatorSelection, timeProvider).InteractAsync : null
        };
    }

    private static string LoadBusinessInstructions(string name)
    {
        using var stream = typeof(OctoberBusinessModule).Assembly.GetManifestResourceStream(
            $"Vulgata.Agents.Prompts.OctoberBusiness.{name}.md") ?? throw new InvalidOperationException("October Business instructions are unavailable.");
        if (stream.Length is <= 0 or > KnowledgeContract.MaximumContentBytes)
            throw new InvalidOperationException("October Business instructions are invalid.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    internal static OctoberGenerateRunner ComposeGenerate(OctoberRuntimeConfiguration runtime,
        IOctoberLocalAuthority authority, IDbContextFactory<OctoberDbContext> contexts,
        ISecretEncryptionService encryption, OctoberScanPolicy policy, OctoberSelectedWorkerProvider provider,
        OctoberBusinessSettings? settings,
        Func<IOctoberOperatorSession, PublicationBinding, CancellationToken, Task<OctoberVerifiedReaderBinding?>>? bindReader,
        TimeProvider? timeProvider = null, OctoberOperatorRootSelectionOptions? operatorSelection = null,
        OctoberParseFailureCapture? parseFailureCapture = null, OctoberWorkerHttpObservation? httpObservation = null,
        string? deepSeekWorkerReasoningEffort = null, int workerMaxOutputTokens = 8192)
    {
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        if (parseFailureCapture is not null && httpObservation is not null) throw new InvalidOperationException("October diagnostic configuration is invalid.");
        var dependencies = ComposeGenerateDependencies(runtime, authority, contexts, encryption, clock, httpObservation, deepSeekWorkerReasoningEffort);
        return new OctoberGenerateRunner(dependencies with
        {
            MaximumWorkerOutputTokens = workerMaxOutputTokens,
            Business = ComposeBusiness(policy, provider, settings, contexts, dependencies.OperationLog, bindReader, operatorSelection, clock) is { } business
                ? business with
                {
                    DiagnosticCapture = settings!.CaptureSemanticDiagnostics || settings.EnableReferencedSourceRetrieval
                        ? new OctoberBusinessDiagnosticCapture(runtime.LogDirectory, encryption).CaptureAsync : null,
                    CreateSourceReader = settings.EnableReferencedSourceRetrieval
                        ? execution => OctoberBusinessSourceReaderFactory.Create(runtime, authority, execution, clock) : null
                } : null,
            ParseFailureCapture = parseFailureCapture,
            HttpObservation = httpObservation
        }, clock);
    }

    internal static async Task<OctoberBusinessSettings?> ReadBusinessSettingsForPolicyAsync(IConfiguration configuration,
        OctoberScanPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.OutputStage == "cl-only") return null;
        if (policy.OutputStage != "cl-and-bl" || !new OctoberScanPolicyValidator().Validate(policy).IsValid)
            throw new InvalidOperationException("October Business activation is invalid.");
        var settings = await ReadBusinessSettingsAsync(configuration, cancellationToken);
        if (settings.MaximumAttemptTokens > policy.MaximumTotalTokens ||
            settings.ContextOverride is not null && policy.ExistingClPublicationId is null ||
            settings.ResolutionPolicyVersion is not null && policy.ExistingClPublicationId is null)
            throw new InvalidOperationException("October Business attempt exceeds the original Scan policy.");
        return settings;
    }

    internal static async Task<OctoberBusinessSettings> ReadBusinessSettingsAsync(IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (configuration.GetSection("October:BusinessSettings").GetChildren().Any()) throw new InvalidOperationException();
            string path = Required(configuration, "October:BusinessSettingsFile");
            if (!Path.IsPathFullyQualified(path) || path.Trim() != path) throw new InvalidOperationException();
            if (configuration is IConfigurationRoot root)
            {
                var selectors = new HashSet<string>(StringComparer.Ordinal);
                foreach (IConfigurationProvider provider in root.Providers)
                    if (provider.TryGet("October:BusinessSettingsFile", out string? value) && value is not null) selectors.Add(value);
                if (selectors.Count > 1) throw new InvalidOperationException();
            }
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length is <= 0 or > 1_048_576) throw new InvalidOperationException();
            byte[] bytes = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
            if (await stream.ReadAsync(new byte[1], cancellationToken) != 0) throw new InvalidOperationException();
            int offset = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
            string json = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            RequireUniqueProperties(document.RootElement);
            if (!document.RootElement.TryGetProperty("limits", out var limits) || limits.ValueKind != JsonValueKind.Object ||
                !limits.TryGetProperty("maxNodes", out _) || !limits.TryGetProperty("maxEdges", out _) ||
                !limits.TryGetProperty("maxDepth", out _)) throw new InvalidOperationException();
            if (!document.RootElement.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array ||
                roots.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("kind", out _)))
                throw new InvalidOperationException();
            var settings = document.RootElement.Deserialize<OctoberBusinessSettings>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16,
                Converters = { new JsonStringEnumConverter<FlowRootKind>(), new JsonStringEnumConverter<OctoberContextScope>() }
            });
            if (settings is null || !settings.IsValid()) throw new InvalidOperationException();
            cancellationToken.ThrowIfCancellationRequested();
            return settings;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DecoderFallbackException or
            JsonException or InvalidOperationException or ArgumentException or NotSupportedException or OverflowException)
        { throw new InvalidOperationException("October Business settings are invalid."); }
    }

    internal static OctoberGenerateRunner ComposeGenerate(OctoberRuntimeConfiguration runtime,
        IOctoberLocalAuthority authority, IDbContextFactory<OctoberDbContext> contexts,
        ISecretEncryptionService encryption, TimeProvider? timeProvider = null)
    {
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        return new OctoberGenerateRunner(ComposeGenerateDependencies(runtime, authority, contexts, encryption, clock), clock);
    }

    private static OctoberGenerateRunnerDependencies ComposeGenerateDependencies(OctoberRuntimeConfiguration runtime,
        IOctoberLocalAuthority authority, IDbContextFactory<OctoberDbContext> contexts,
        ISecretEncryptionService encryption, TimeProvider clock, OctoberWorkerHttpObservation? httpObservation = null,
        string? deepSeekWorkerReasoningEffort = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(encryption);
        if (!runtime.IsValid()) throw new InvalidOperationException("October runtime configuration is invalid.");
        var log = new OctoberKnowledgeOperationLog(runtime.LogDirectory, clock);
        var sources = new OctoberScanRunSourceFactory(runtime, authority, clock);
        var clients = httpObservation is null
            ? new OctoberWorkerClientFactory(encryption, deepSeekWorkerReasoningEffort: deepSeekWorkerReasoningEffort)
            : new OctoberWorkerClientFactory(encryption, null, httpObservation, deepSeekWorkerReasoningEffort);
        return new(authority, sources.ValidateSelection,
            (admissionAuthority, guard) => new OctoberGenerateAdmissionStore(contexts, admissionAuthority, guard, log),
            sources.OpenAsync, clients.CreateAsync,
            ports => new OctoberKnowledgeWriter(contexts, ports.CompletionAuthority, ports.SourceVerifier,
                ports.ProducerAuthorization, ports.CommitGuard, log, ports.FreezeAuthority), log)
        {
            JsonCorrectionLog = new OctoberJsonCorrectionLog(runtime.LogDirectory, clock).AppendAsync,
            CreateBusinessFromClStore = (owner, begin, completion, settlement) =>
                new OctoberBusinessFromClStore(contexts, owner, begin, completion, settlement, log)
        };
    }

    internal static async Task<int> ExecuteConfiguredAsync(OctoberGeneratorCommand command,
        IOctoberGeneratorModule module, TextWriter output, TextWriter error,
        CancellationToken cancellationToken = default, Func<ValueTask>? settle = null)
    {
        OctoberGeneratorExecutionResult result;
        try { result = await module.ExecuteAsync(command, cancellationToken); }
        catch (OperationCanceledException) { result = new(OctoberGeneratorExecutionStatus.Cancelled); }
        catch (Exception) { result = new(OctoberGeneratorExecutionStatus.TargetUnavailable); }
        if (command.Mode == OctoberGeneratorMode.Generate)
        {
            OctoberScanReport report = result.Status == OctoberGeneratorExecutionStatus.GenerateFinished && result.Report is not null
                ? result.Report : EntryFailure(command.ScanPolicy?.OutputStage, result.Status == OctoberGeneratorExecutionStatus.Cancelled);
            if (!OutputBindingMatches(command, report))
                report = report with
                {
                    CompletedStage = null, Coverage = OctoberCoverageStatus.None, Execution = OctoberExecutionStatus.Failed,
                    PublicationStatus = OctoberPublicationStatus.Failed, Publication = null, Documents = [], AcceptedFiles = [],
                    TerminalCoverage = [], Inventory = [], Gaps = [new("output_binding_invalid")],
                    BusinessDiagnostics = null,
                    WorkerDiagnostics = null,
                    HardRetry = null,
                    Matching = report.RequestedStage == "cl-only" ? OctoberPhaseStatus.NotRequested : OctoberPhaseStatus.Unavailable,
                    BusinessLogic = report.RequestedStage == "cl-only" ? OctoberPhaseStatus.NotRequested : OctoberPhaseStatus.Unavailable
                };
            if (settle is not null)
            {
                try { await settle(); }
                catch (Exception)
                {
                    report = report with { Execution = OctoberExecutionStatus.Indeterminate,
                        Gaps = report.Gaps.Add(new("owned_cleanup_unconfirmed")) };
                }
            }
            await output.WriteLineAsync(SerializeReport(report));
            return report.ExitCode;
        }
        if (result.Status is OctoberGeneratorExecutionStatus.Denied or OctoberGeneratorExecutionStatus.AuthorityUnavailable or OctoberGeneratorExecutionStatus.TargetUnavailable)
            await error.WriteLineAsync($"October setup failed: {result.Status}.");
        if (result.Status == OctoberGeneratorExecutionStatus.Prepared && result.CommittedBatch is { } committed)
            await output.WriteLineAsync($"October setup Prepared: batch {committed.BatchId:D}.");
        if (result.Status == OctoberGeneratorExecutionStatus.Selected && result.BoundSetupData is { } selected)
            await output.WriteLineAsync($"October setup Selected: batch {selected.BatchId:D}.");
        return result.Status is OctoberGeneratorExecutionStatus.Prepared or OctoberGeneratorExecutionStatus.Selected ? 0 : 1;
    }

    public static async Task<int> Main()
    {
        try
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            OctoberGeneratorCommand command;
            OctoberAuthorityConfiguration authorityConfiguration;
            OctoberRuntimeConfiguration? runtime = null;
            OctoberBusinessSettings? businessSettings = null;
            OctoberOperatorRootSelectionOptions? operatorSelection = null;
            OctoberParseFailureCaptureOptions? captureOptions = null;
            OctoberWorkerHttpObservationOptions? observationOptions = null;
            string? deepSeekWorkerReasoningEffort = null;
            int workerMaxOutputTokens = 8192;
            string keyRingPath;
            string applicationName;
            try
            {
                command = ReadCommand(configuration);
                if (command.Mode == OctoberGeneratorMode.Generate)
                {
                    deepSeekWorkerReasoningEffort = ReadDeepSeekWorkerReasoningEffort(configuration);
                    workerMaxOutputTokens = ReadWorkerMaxOutputTokens(configuration);
                    runtime = await ReadRuntimeConfigurationAsync(configuration);
                    businessSettings = await ReadBusinessSettingsForPolicyAsync(configuration, command.ScanPolicy!);
                    operatorSelection = ReadOperatorRootSelectionOptions(configuration, command.ScanPolicy!, businessSettings, runtime);
                }
                authorityConfiguration = ReadAuthorityConfiguration(configuration, command);
                keyRingPath = Required(configuration, "DataProtection:KeyRingPath");
                applicationName = Required(configuration, "DataProtection:ApplicationName");
                captureOptions = ReadParseFailureCaptureOptions(configuration, command, runtime, keyRingPath);
                observationOptions = ReadWorkerHttpObservationOptions(configuration, command, captureOptions);
            }
            catch (Exception exception) when (exception is InvalidOperationException or JsonException or
                ArgumentException)
            {
                Console.Error.WriteLine("October startup configuration failed: InvalidConfiguration.");
                return 1;
            }

            IServiceCollection registrations = new ServiceCollection();
            registrations.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                .SetApplicationName(applicationName);
            registrations.AddSingleton<ISecretEncryptionService, SecretEncryptionService>();
            await using ServiceProvider protection = registrations.BuildServiceProvider(validateScopes: true);
            OctoberProviderConfiguration providerConfiguration;
            OctoberSelectedWorkerProvider? workerProvider = null;
            try
            {
                providerConfiguration = command.Mode == OctoberGeneratorMode.Generate
                    ? await ReadGenerateProviderConfigurationAsync(configuration)
                    : await ReadProviderConfigurationAsync(configuration);
            }
            catch (InvalidOperationException)
            {
                Console.Error.WriteLine("October provider configuration failed: InvalidConfiguration.");
                return 1;
            }
            if (command.Mode == OctoberGeneratorMode.Generate)
            {
                workerProvider = await BindWorkerProviderAsync(providerConfiguration,
                    protection.GetRequiredService<ISecretEncryptionService>());
                if (workerProvider is null)
                {
                    Console.Error.WriteLine("October Worker provider binding failed: InvalidConfiguration.");
                    return 1;
                }
            }
            else
            {
                OctoberProviderBindingResult providerBinding = await new OctoberProviderBinder(
                protection.GetRequiredService<ISecretEncryptionService>())
                .BindAsync(providerConfiguration);
                if (providerBinding.Status != OctoberProviderBindingStatus.Bound ||
                    providerBinding.Binding is null)
                {
                    if (providerBinding.Status == OctoberProviderBindingStatus.InvalidConfiguration)
                        Console.Error.WriteLine("October provider binding failed: InvalidConfiguration.");
                    if (providerBinding.Status == OctoberProviderBindingStatus.CredentialUnavailable)
                        Console.Error.WriteLine("October provider binding failed: CredentialUnavailable.");
                    return 1;
                }
            }
            if (command.Mode != OctoberGeneratorMode.Generate)
                Console.Out.WriteLine("October provider configuration Bound.");

            IOctoberOperatorCredentialSource credentialSource;
            try
            {
                credentialSource = Required(configuration, "October:Operator:CredentialSource") switch
                {
                    "console" when configuration["October:Operator:CredentialFile"] is null =>
                        command.Mode == OctoberGeneratorMode.Generate
                            ? new OctoberConsoleCredentialSource(new GenerateCredentialTerminal())
                            : new OctoberConsoleCredentialSource(),
                    "protected-file" => new OctoberProtectedFileCredentialSource(
                        Required(configuration, "October:Operator:CredentialFile"),
                        protection.GetRequiredService<IDataProtectionProvider>()),
                    _ => throw new InvalidOperationException("October operator configuration is invalid.")
                };
            }
            catch (InvalidOperationException)
            {
                Console.Error.WriteLine("October startup configuration failed: InvalidConfiguration.");
                return 1;
            }
            OctoberHeadlessAuthorityRegistration.AddOctoberLocalAuthority(
                registrations, authorityConfiguration, credentialSource, TimeProvider.System);
            registrations.AddDbContext<OctoberDbContext>(options =>
                options.UseNpgsql(authorityConfiguration.DataConnectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__OctoberMigrationsHistory", "october")));
            if (command.Mode == OctoberGeneratorMode.Generate && command.ScanPolicy!.OutputStage == "cl-and-bl")
                AddBusinessReaderDependencies(registrations, runtime!.LogDirectory);

            await using ServiceProvider host = registrations.BuildServiceProvider(validateScopes: true);
            await using AsyncServiceScope scope = host.CreateAsyncScope();
            OctoberDbContext database = scope.ServiceProvider.GetRequiredService<OctoberDbContext>();
            OctoberNpgsqlTargetDependencies targetDependencies = new(
                database, authorityConfiguration.DataConnectionString);
            OctoberConfiguredTarget target = new(
                authorityConfiguration.TargetId, authorityConfiguration.CanonicalDatabaseIdentity,
                targetDependencies, targetDependencies,
                new OctoberSetupStore(database, TimeProvider.System),
                new OctoberBatchSelector(database, authorityConfiguration.TargetId,
                    authorityConfiguration.CanonicalDatabaseIdentity));
            IOctoberLocalAuthority localAuthority = scope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
            Func<IOctoberOperatorSession, PublicationBinding, CancellationToken, Task<OctoberVerifiedReaderBinding?>>? bindReader = null;
            if (command.Mode == OctoberGeneratorMode.Generate && command.ScanPolicy!.OutputStage == "cl-and-bl")
            {
                var originalIssuer = scope.ServiceProvider.GetRequiredService<OctoberLocalAuthority>();
                if (!ReferenceEquals(originalIssuer, localAuthority))
                    throw new InvalidOperationException("October operator reader issuer is inconsistent.");
                bindReader = async (originalSession, publication, token) =>
                {
                    var bound = await originalIssuer.TryBindReaderAsync(originalSession, publication, token);
                    return bound is null ? null : new(bound.Subject, bound.Selection, bound.Reader, bound);
                };
            }
            OctoberGenerateRunner? generate = command.Mode == OctoberGeneratorMode.Generate
                ? ComposeGenerate(runtime!, localAuthority,
                    new ConfiguredContextFactory(scope.ServiceProvider.GetRequiredService<DbContextOptions<OctoberDbContext>>()),
                    protection.GetRequiredService<ISecretEncryptionService>(), command.ScanPolicy!, workerProvider!,
                    businessSettings, bindReader, operatorSelection: operatorSelection,
                    parseFailureCapture: captureOptions is null ? null : new OctoberParseFailureCapture(captureOptions,
                        CaptureExcludedPaths(configuration, runtime!, keyRingPath)),
                    httpObservation: observationOptions is null ? null : new OctoberWorkerHttpObservation(observationOptions),
                    deepSeekWorkerReasoningEffort: deepSeekWorkerReasoningEffort,
                    workerMaxOutputTokens: workerMaxOutputTokens) : null;
            OctoberGeneratorModule module = new(localAuthority, target, authorityConfiguration.AuthorityId, generate, workerProvider);
            return await ExecuteConfiguredAsync(command, module, Console.Out, Console.Error,
                settle: generate is null ? null : () => generate.DisposeAsync());
        }
        catch (Exception)
        {
            return 1;
        }
    }

    private static OctoberScanReport EntryFailure(string? stage, bool cancelled) => new(stage ?? string.Empty, null,
        OctoberCoverageStatus.None, cancelled ? OctoberExecutionStatus.Interrupted : OctoberExecutionStatus.Failed,
        OctoberPublicationStatus.NotRequested, null, [], [], [],
        new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
        new(null, null, null, true), [new(cancelled ? "run_interrupted" : "generate_terminal_report_unavailable")]);

    private static bool OutputBindingMatches(OctoberGeneratorCommand command, OctoberScanReport report)
    {
        if (report.Documents.IsDefault || report.AcceptedFiles.IsDefault || report.TerminalCoverage.IsDefault)
            return false;
        if (report.Publication is { } publication &&
            (publication.Knowledge.TargetId != command.TargetId || publication.Knowledge.BatchId != command.BatchId ||
             publication.PublicationId == Guid.Empty || publication.Knowledge.SelectionDigest is not { Length: 64 } ||
             !publication.Knowledge.SelectionDigest.All(char.IsAsciiHexDigit)))
            return false;
        return report.Documents.All(document => document is not null && report.Publication is { } selected &&
                document.BatchId == command.BatchId && document.PublicationId == selected.PublicationId &&
                document.LogicalDocumentId != Guid.Empty && document.DocumentVersionId != Guid.Empty) &&
            report.AcceptedFiles.All(file => file is not null && file.ManifestId != Guid.Empty && file.AdmissionId != Guid.Empty &&
                SafeCoverageIdentity(file.Coverage) && file.AdmissionId == file.Coverage.AdmissionId && Digest(file.ProposalDigest) &&
                !file.Documents.IsDefault && file.Documents.All(document => document is not null &&
                    document.BatchId == command.BatchId && document.LogicalDocumentId != Guid.Empty && document.DocumentVersionId != Guid.Empty)) &&
            report.TerminalCoverage.All(SafeCoverageIdentity);
    }

    private static bool SafeCoverageIdentity(FileCoverage coverage) => coverage is { Origin: { } origin } &&
        coverage.AdmissionId != Guid.Empty && origin.RepositoryId != Guid.Empty && Enum.IsDefined(coverage.Disposition) &&
        SafeRelativePath(origin.RelativePath) is { Length: > 0 } && Digest(origin.SourceDigest) &&
        OpaqueIdentity(origin.RootBindingIdentity) && OpaqueIdentity(origin.PreparedVersion);

    private static bool Digest(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    private static bool OpaqueIdentity(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 4096 &&
        value == value.Trim() && !value.Any(char.IsControl) && !Path.IsPathFullyQualified(value) &&
        !value.Contains(":/", StringComparison.Ordinal) && !value.Contains(":\\", StringComparison.Ordinal);

    private static object ProjectCoverage(FileCoverage coverage) => new
    {
        coverage.AdmissionId,
        Origin = new
        {
            coverage.Origin.RepositoryId,
            coverage.Origin.RootBindingIdentity,
            coverage.Origin.RelativePath,
            coverage.Origin.SourceDigest,
            coverage.Origin.PreparedVersion
        },
        coverage.Disposition,
        Reason = coverage.Disposition switch
        {
            CoverageDisposition.Accepted => null,
            CoverageDisposition.CompletedZeroUnit => "completed-zero-unit",
            CoverageDisposition.Failed => "failed",
            CoverageDisposition.Cancelled => "cancelled",
            _ => "unprocessed"
        }
    };

    private static string SerializeReport(OctoberScanReport report)
    {
        // Project the binding explicitly: its canonical database identity belongs
        // to trusted composition and is never part of the console protocol.
        object? publication = report.Publication is { } selected ? new
        {
            selected.Knowledge.TargetId,
            selected.Knowledge.BatchId,
            selected.Knowledge.SelectionDigest,
            selected.PublicationId
        } : null;
        var projection = new
        {
            report.RequestedStage,
            report.CompletedStage,
            report.Coverage,
            report.Execution,
            report.PublicationStatus,
            Matching = PhaseStatus(report.Matching),
            BusinessLogic = PhaseStatus(report.BusinessLogic),
            BusinessDiagnostics = ProjectBusinessDiagnostics(report),
            HardRetry = report.HardRetry is { ReservedAttempts: >= 0 and <= 16, ProviderCallsStarted: >= 0 and <= 16 } correction &&
                correction.ProviderCallsStarted <= correction.ReservedAttempts
                ? new { correction.ReservedAttempts, correction.ProviderCallsStarted } : null,
            WorkerDiagnostics = report.WorkerDiagnostics is { } worker ? new
            {
                Status = DefinedStatus(worker.Status),
                RetryDisposition = DefinedStatus(worker.RetryDisposition),
                FailureStage = DefinedStatus(worker.FailureStage),
                TerminalFailure = worker.Status is { } workerStatus && Enum.IsDefined(workerStatus) &&
                    workerStatus != Vulgata.Agents.WorkerExecution.OctoberFileWorkerStatus.Completed
                    ? DefinedStatus(worker.TerminalFailure) : null,
                TerminalObservation = ProjectTerminalObservation(worker),
                BudgetFailure = worker.Status == Vulgata.Agents.WorkerExecution.OctoberFileWorkerStatus.BudgetExceeded
                    ? DefinedStatus(worker.BudgetFailure) : null,
                ParseFailure = worker.Status is { } parseStatus && Enum.IsDefined(parseStatus) &&
                    parseStatus != Vulgata.Agents.WorkerExecution.OctoberFileWorkerStatus.Completed &&
                    worker.FailureStage == Vulgata.Agents.WorkerExecution.OctoberWorkerFailureStage.ProposalParse &&
                    worker.ParseFailure is { } parse ? new
                    {
                        Category = DefinedStatus(parse.Category),
                        Slot = DefinedStatus(parse.Slot),
                        FieldFailure = DefinedStatus(parse.FieldFailure),
                        Field = DefinedStatus(parse.Field),
                        UnitIndex = parse.UnitIndex is >= 0 and <= 65535 ? parse.UnitIndex : null
                    } : null,
                CaptureStatus = worker.Status is { } captureWorkerStatus && Enum.IsDefined(captureWorkerStatus) &&
                    captureWorkerStatus != Vulgata.Agents.WorkerExecution.OctoberFileWorkerStatus.Completed &&
                    worker.FailureStage == Vulgata.Agents.WorkerExecution.OctoberWorkerFailureStage.ProposalParse
                    ? DefinedStatus(worker.CaptureStatus) : null,
                HttpObservation = ProjectHttpObservation(worker),
                ToolFailure = ProjectToolFailure(worker)
            } : null,
            Publication = publication,
            report.Documents,
            AcceptedFiles = report.AcceptedFiles.Select(file => new
            {
                file.ManifestId,
                file.AdmissionId,
                file.ProposalDigest,
                file.Documents,
                Coverage = ProjectCoverage(file.Coverage)
            }),
            TerminalCoverage = report.TerminalCoverage.Select(ProjectCoverage),
            Inventory = report.Inventory.Select(entry => new
            {
                entry.RepositoryId,
                RelativePath = SafeRelativePath(entry.RelativePath),
                entry.Kind,
                entry.Disposition,
                Reason = entry.Disposition switch
                {
                    "selected" => "configured-selected-source",
                    "supporting" => "configured-supporting-source",
                    "excluded" => "excluded-from-declared-source-scope",
                    "unsupported" => "unsupported-source-kind",
                    _ => "source-disposition-unavailable"
                },
                entry.Priority,
                entry.ObservedByteLength
            }),
            Gaps = report.Gaps.Select(gap => new { Code = SafeGapCode(gap.Code), gap.RepositoryId, RelativePath = SafeRelativePath(gap.RelativePath) }),
            report.Timings,
            report.Usage,
            report.ExitCode
        };
        return JsonSerializer.Serialize(projection, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        });
    }

    private static object? ProjectTerminalObservation(OctoberWorkerDiagnosticSnapshot worker)
    {
        if (worker.Status != Vulgata.Agents.WorkerExecution.OctoberFileWorkerStatus.InvalidOutput ||
            worker.FailureStage != Vulgata.Agents.WorkerExecution.OctoberWorkerFailureStage.StreamTerminal ||
            worker.TerminalFailure != Vulgata.Agents.WorkerExecution.OctoberWorkerTerminalFailure.UnsupportedFinish ||
            worker.TerminalObservation is not { } observed) return null;
        return new
        {
            FinishReason = DefinedStatus(observed.FinishReason),
            MaximumOutputTokens = observed.MaximumOutputTokens is > 0 and <= 393216 ? observed.MaximumOutputTokens : null,
            InputTokens = observed.InputTokens is >= 0 ? observed.InputTokens : null,
            OutputTokens = observed.OutputTokens is >= 0 ? observed.OutputTokens : null,
            ReasoningTokens = observed.ReasoningTokens is >= 0 ? observed.ReasoningTokens : null
        };
    }

    private static object? ProjectHttpObservation(OctoberWorkerDiagnosticSnapshot worker)
    {
        if (worker.Status is not { } status || !Enum.IsDefined(status) ||
            status == Vulgata.Agents.WorkerExecution.OctoberFileWorkerStatus.Completed ||
            worker.FailureStage != Vulgata.Agents.WorkerExecution.OctoberWorkerFailureStage.ProposalParse ||
            worker.HttpObservation is not { } observed) return null;
        static long? Bound(long? value, long maximum) => value is >= 0 && value <= maximum ? value : null;
        static object? Json(Vulgata.Agents.WorkerExecution.OctoberWorkerJsonObservation? value) => value is null ? null : new
        {
            Kind = DefinedStatus(value.Kind), OpenObjects = Bound(value.OpenObjects, 32),
            OpenArrays = Bound(value.OpenArrays, 32), value.AtEof
        };
        return new
        {
            Status = DefinedStatus(observed.Status), CapKey = DefinedStatus(observed.CapKey),
            CapValue = observed.CapValue is > 0 ? Bound(observed.CapValue, int.MaxValue) : null,
            HttpFinish = DefinedStatus(observed.HttpFinish), SdkFinish = DefinedStatus(observed.SdkFinish),
            observed.DoneSeen, observed.EofSeen, observed.ContentAfterFinish, observed.ContentAfterDone,
            WireBytes = Bound(observed.WireBytes, 4194304), HttpTextUtf8Bytes = Bound(observed.HttpTextUtf8Bytes, 262144),
            SdkTextUtf8Bytes = Bound(observed.SdkTextUtf8Bytes, 262144), ParseInputUtf8Bytes = Bound(observed.ParseInputUtf8Bytes, 262144),
            observed.HttpEqualsSdk, observed.SdkEqualsParse,
            HttpJson = Json(observed.HttpJson), SdkJson = Json(observed.SdkJson), ParseJson = Json(observed.ParseJson),
            HttpReasoningTokens = Bound(observed.HttpReasoningTokens, int.MaxValue), SdkReasoningTokens = Bound(observed.SdkReasoningTokens, int.MaxValue)
        };
    }

    private static object? ProjectToolFailure(OctoberWorkerDiagnosticSnapshot worker)
    {
        if (worker.Status is not { } status || !Enum.IsDefined(status) ||
            status == Vulgata.Agents.WorkerExecution.OctoberFileWorkerStatus.Completed ||
            worker.ToolFailure is not { } observed || !Enum.IsDefined(observed.Operation) ||
            !Enum.IsDefined(observed.Boundary)) return null;
        bool java = observed.Operation is SourceMetadataOperation.JavaDefinition or SourceMetadataOperation.JavaReferences;
        bool source = observed.Operation is SourceMetadataOperation.List or SourceMetadataOperation.ReadTextRange or SourceMetadataOperation.SearchText;
        return new
        {
            observed.Operation,
            observed.Boundary,
            JavaOpenStatus = java ? DefinedStatus(observed.JavaOpenStatus) : null,
            JavaOutcome = java ? DefinedStatus(observed.JavaOutcome) : null,
            SourceOutcome = source || (java && observed.Boundary == OctoberToolFailureBoundary.SourceCurrentness)
                ? DefinedStatus(observed.SourceOutcome) : null,
            CodeGraphStatus = observed.Operation == SourceMetadataOperation.CodeGraphExplore ? DefinedStatus(observed.CodeGraphStatus) : null,
            observed.CleanupConfirmed
        };
    }

    private static string? SafeRelativePath(string? path) => path is not null && path.Length <= 2048 &&
        !path.StartsWith('/') && !path.Contains('\\') && !path.Contains(':') && !path.Any(char.IsControl) &&
        (path.Length == 0 || path.Split('/').All(part => part is not ("" or "." or ".."))) ? path : null;

    private static object? ProjectBusinessDiagnostics(OctoberScanReport report)
    {
        if (report.RequestedStage != "cl-and-bl" || report.BusinessDiagnostics is not { } observed ||
            !Enum.IsDefined(observed.Boundary)) return null;
        var resolutionReason = observed.Boundary == OctoberBusinessDiagnosticBoundary.Resolution
            ? DefinedStatus(observed.ResolutionFailureReason) : null;
        return new
        {
            FinalizerStatus = DefinedStatus(observed.FinalizerStatus),
            FinalizerPersistenceStatus = DefinedStatus(observed.FinalizerPersistenceStatus),
            LookupStatus = DefinedStatus(observed.LookupStatus),
            LookupPersistenceStatus = DefinedStatus(observed.LookupPersistenceStatus),
            OriginalSynthesisFailureReason = observed.OriginalSynthesisFailureReason == BusinessSynthesisFailureReason.None
                ? null : DefinedStatus(observed.OriginalSynthesisFailureReason),
            ResolutionFailureReason = resolutionReason,
            ResolutionFailureField = resolutionReason == ResolutionFailureReason.WitnessShape
                ? observed.ResolutionFailureField switch
                {
                    "Caller.Document.BatchId" => "Caller.Document.BatchId",
                    "Caller" => "Caller",
                    "Provider" => "Provider",
                    "SupportingAssertions.Kind" => "SupportingAssertions.Kind",
                    "CallerAction" => "CallerAction", "ProviderRole" => "ProviderRole",
                    "ProviderOperation" => "ProviderOperation", "Mechanism" => "Mechanism",
                    "NamespaceInterpretation" => "NamespaceInterpretation", "BindingInterpretation" => "BindingInterpretation",
                    "AliasOrConfigurationMapping" => "AliasOrConfigurationMapping",
                    "SupportingAssertions" => "SupportingAssertions", "OpposingAssertions" => "OpposingAssertions",
                    "ConnectionExplanation" => "ConnectionExplanation", "Limitations" => "Limitations",
                    "HasUnresolvedDecisiveOpposition" => "HasUnresolvedDecisiveOpposition",
                    "ClaimsDeploymentCertainty" => "ClaimsDeploymentCertainty", "BindingToken" => "BindingToken",
                    _ => null
                } : null,
            observed.Boundary,
            observed.OperationMismatch
        };
    }

    private static T? DefinedStatus<T>(T? value) where T : struct, Enum =>
        value is { } defined && Enum.IsDefined(defined) ? defined : null;

    private static string PhaseStatus(OctoberPhaseStatus status) => status switch
    {
        OctoberPhaseStatus.NotRequested => "not-requested", OctoberPhaseStatus.NotStarted => "not-started",
        OctoberPhaseStatus.Completed => "completed", OctoberPhaseStatus.Partial => "partial",
        OctoberPhaseStatus.NoContent => "no-content", OctoberPhaseStatus.Failed => "failed",
        OctoberPhaseStatus.Interrupted => "interrupted", OctoberPhaseStatus.Indeterminate => "indeterminate",
        _ => "unavailable"
    };

    private static string SafeGapCode(string code) => code switch
    {
        "json-correction-log-incomplete" or "owned_cleanup_unconfirmed" or "coverage_incomplete" or "invalid_run_configuration" or "authority_unavailable" or
        "begin_indeterminate" or "begin_settlement_pending" or "fresh_run_required" or "run_ineligible" or "run_interrupted" or
        "run_unavailable" or "single_file_checkpoint_limit" or "source_frame_unavailable" or "source_frame_binding_changed" or
        "file_deadline_exceeded" or "provider_settlement_unconfirmed" or "worker_not_completed" or "file_acceptance_unavailable" or
        "generate_terminal_report_unavailable" or "output_binding_invalid" or "commit_indeterminate" or "operation_log_unavailable" or "phase_ineligible" or
        "publication_failed" or "publication_interrupted" or "publication_unavailable" or "no_cl_content" or
        "freeze_settlement_pending" or "publication_settlement_pending" or "phase_closed" or "unsupported_stage" or
        "publication_not_started" or "source-authority-or-identity-lost" or "incomplete-directory" or "source-limitation" or
        "directory-limit" or "metadata-limit" or "listing-unavailable" or "cancelled" or
        "business_authority_unavailable" or "business_client_unavailable" or "business_corpus_unavailable" or
        "business_existing_cl_admission_unavailable" or "business_existing_resolution_unavailable" or
        "business_attempt_unsettled" or "business_attempt_close_unconfirmed" or
        "business_freeze_unavailable" or "business_initial_read_unavailable" or "business_original_slot_unavailable" or
        "business_planning_inputs_unavailable" or "business_publication_binding_unavailable" or
        "business_publication_read_unavailable" or "business_publication_unavailable" or
        "business_resolution_failed" or "business_resolution_ineligible" or "business_resolution_limit" or
        "business_resolution_unavailable" or "business_roots_unavailable" or "business_run_ineligible" => code,
        _ => "source-or-execution-gap"
    };

    private sealed class ConfiguredContextFactory(DbContextOptions<OctoberDbContext> options) : IDbContextFactory<OctoberDbContext>
    {
        public OctoberDbContext CreateDbContext() => new(options);
    }

    private sealed class GenerateCredentialTerminal : IOctoberCredentialTerminal
    {
        public bool IsInteractive => !Console.IsInputRedirected && !Console.IsErrorRedirected && Environment.UserInteractive;
        public void Write(string value) => Console.Error.Write(value);
        public async ValueTask<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
        {
            while (!Console.KeyAvailable)
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return Console.ReadKey(intercept);
        }
    }

    internal static async Task<OctoberRuntimeConfiguration> ReadRuntimeConfigurationAsync(
        IConfigurationRoot configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (configuration.GetSection("October:Runtime").GetChildren().Any())
                throw new InvalidOperationException();
            string path = Required(configuration, "October:RuntimeFile");
            if (!Path.IsPathFullyQualified(path) || path != path.Trim())
                throw new InvalidOperationException();
            var selectors = new HashSet<string>(StringComparer.Ordinal);
            foreach (IConfigurationProvider source in configuration.Providers)
                if (source.TryGet("October:RuntimeFile", out string? value) && value is not null)
                    selectors.Add(value);
            if (selectors.Count != 1) throw new InvalidOperationException();

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length is <= 0 or > 1_048_576) throw new InvalidOperationException();
            byte[] bytes = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
            if (await stream.ReadAsync(new byte[1], cancellationToken) != 0)
                throw new InvalidOperationException();
            int offset = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
            string json = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            RequireUniqueProperties(document.RootElement);
            OctoberRuntimeConfiguration? runtime = document.RootElement.Deserialize<OctoberRuntimeConfiguration>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                MaxDepth = 16
            });
            if (runtime is null || !runtime.IsValid()) throw new InvalidOperationException();
            cancellationToken.ThrowIfCancellationRequested();
            return runtime;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DecoderFallbackException or
            JsonException or InvalidOperationException or ArgumentException or NotSupportedException or OverflowException)
        {
            throw new InvalidOperationException("October runtime configuration is invalid.");
        }
    }

    private static OctoberGeneratorCommand ReadCommand(IConfiguration configuration)
    {
        OctoberGeneratorMode mode = configuration["October:Mode"] switch
        {
            "prepare" => OctoberGeneratorMode.Prepare,
            "select" => OctoberGeneratorMode.Select,
            "generate" => OctoberGeneratorMode.Generate,
            _ => throw new InvalidOperationException("October generator mode is invalid.")
        };
        string targetId = Required(configuration, "October:TargetId");
        if (!Guid.TryParse(configuration["October:BatchId"], out Guid batchId) ||
            batchId == Guid.Empty)
            throw new InvalidOperationException("October batch is invalid.");

        OctoberSetupRequest? request = null;
        if (mode == OctoberGeneratorMode.Prepare)
        {
            request = JsonSerializer.Deserialize<OctoberSetupRequest>(
                Required(configuration, "October:SetupRequestJson"));
            if (request is null ||
                !string.Equals(request.TargetId, targetId, StringComparison.Ordinal) ||
                request.BatchId != batchId)
                throw new InvalidOperationException("October setup request is invalid.");
        }
        else if (configuration["October:SetupRequestJson"] is not null)
        {
            throw new InvalidOperationException("October select request is invalid.");
        }

        return new OctoberGeneratorCommand(mode, targetId, batchId, request,
            mode == OctoberGeneratorMode.Generate ? ReadScanPolicy(configuration) : null);
    }

    internal static OctoberScanPolicy ReadScanPolicy(IConfiguration configuration)
    {
        try
        {
            if (configuration["October:OutputStage"] is not null ||
                configuration.GetSection("October:ScanPolicy").GetChildren().Any())
                throw new InvalidOperationException();
            string path = Required(configuration, "October:ScanPolicyFile");
            if (configuration is IConfigurationRoot root)
            {
                var selectors = new HashSet<string>(StringComparer.Ordinal);
                foreach (IConfigurationProvider source in root.Providers)
                    if (source.TryGet("October:ScanPolicyFile", out string? supplied) && supplied is not null)
                        selectors.Add(supplied);
                if (selectors.Count > 1)
                    throw new InvalidOperationException();
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > 1_048_576)
                throw new InvalidOperationException();
            byte[] bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1)
                throw new InvalidOperationException();
            int offset = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
            string json = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            RequireUniqueProperties(document.RootElement);
            OctoberScanPolicy? policy = document.RootElement.Deserialize<OctoberScanPolicy>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                MaxDepth = 16
            });
            if (policy is null || !new OctoberScanPolicyValidator().Validate(policy).IsValid)
                throw new InvalidOperationException();
            return policy;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            DecoderFallbackException or JsonException or InvalidOperationException or ArgumentException or
            NotSupportedException or OverflowException)
        {
            throw new InvalidOperationException("October scan policy is invalid.");
        }
    }

    internal static Task<OctoberProviderConfiguration> ReadGenerateProviderConfigurationAsync(
        IConfigurationRoot configuration) => ReadProviderDocumentAsync(configuration, requireChat: false);

    internal static async Task<OctoberSelectedWorkerProvider?> BindWorkerProviderAsync(
        OctoberProviderConfiguration configuration, ISecretEncryptionService encryption,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(encryption);
        if (cancellationToken.IsCancellationRequested || !Exact(configuration.SourceIdentity) ||
            configuration.Precedence != "only-source" || !Exact(configuration.WorkerProfile))
            return null;

        var profiles = new Dictionary<string, OctoberProviderProfile>(StringComparer.Ordinal);
        foreach (OctoberProviderProfile? profile in configuration.Profiles)
            if (profile is null || !Exact(profile.Name) || !profiles.TryAdd(profile.Name, profile))
                return null;
        if (!profiles.TryGetValue(configuration.WorkerProfile, out OctoberProviderProfile? selected) ||
            selected.ApiType is not (ApiType.ChatCompletions or ApiType.Responses or ApiType.Messages) ||
            !Exact(selected.Model) || !Exact(selected.CredentialReference) || !Exact(selected.Endpoint) ||
            !Uri.TryCreate(selected.Endpoint, UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme is not ("http" or "https") || endpoint.UserInfo.Length != 0 ||
            endpoint.Fragment.Length != 0 ||
            !configuration.ProtectedCredentials.TryGetValue(selected.CredentialReference, out string? ciphertext) ||
            string.IsNullOrWhiteSpace(ciphertext))
            return null;
        try
        {
            string credential = await encryption.DecryptAsync(
                "Vulgata.LlmProviders.ApiKey", ciphertext, cancellationToken);
            if (string.IsNullOrWhiteSpace(credential) || cancellationToken.IsCancellationRequested)
                return null;
            return new OctoberSelectedWorkerProvider(configuration.SourceIdentity,
                configuration.Precedence, selected, ciphertext);
        }
        catch (Exception)
        {
            return null;
        }

        static bool Exact(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim() == value;
    }

    private static void RequireUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidOperationException();
                RequireUniqueProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray())
                RequireUniqueProperties(item);
    }

    private static OctoberAuthorityConfiguration ReadAuthorityConfiguration(
        IConfiguration configuration, OctoberGeneratorCommand command)
    {
        ImmutableDictionary<string, string>.Builder targets =
            ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (IConfigurationSection item in configuration.GetSection("October:Targets").GetChildren())
        {
            if (!targets.TryAdd(Required(item, "TargetId"),
                    Required(item, "CanonicalDatabaseIdentity")))
                throw new InvalidOperationException("October target configuration is invalid.");
        }

        if (!targets.TryGetValue(command.TargetId, out string? canonicalIdentity))
            throw new InvalidOperationException("October selected target is invalid.");
        string dataConnection = Required(configuration, "ConnectionStrings:DefaultConnection");
        string? identityName = configuration["October:Identity:ConnectionName"];
        string identityConnection = identityName is null
            ? dataConnection
            : Required(configuration, $"ConnectionStrings:{identityName}");
        string identityCanonical = identityName is null
            ? canonicalIdentity
            : Required(configuration, "October:Identity:CanonicalDatabaseIdentity");

        return new OctoberAuthorityConfiguration(
            Required(configuration, "October:Authority:Id"),
            command.TargetId, command.BatchId, canonicalIdentity,
            dataConnection, identityConnection, identityCanonical,
            targets.ToImmutable(), TimeSpan.FromSeconds(30), AuthoritySessionLifetime(command));
    }

    internal static TimeSpan AuthoritySessionLifetime(OctoberGeneratorCommand command)
    {
        if (command.Mode != OctoberGeneratorMode.Generate) return TimeSpan.FromHours(1);
        if (command.ScanPolicy is not { } policy || !new OctoberScanPolicyValidator().Validate(policy).IsValid)
            throw new InvalidOperationException("A validated Generate policy is required for the session lifetime.");
        return TimeSpan.FromSeconds(Math.Max(3600, policy.RunDeadlineSeconds));
    }

    private static string Required(IConfiguration configuration, string key)
    {
        string? value = configuration[key];
        return !string.IsNullOrWhiteSpace(value) ? value :
            throw new InvalidOperationException("October configuration is invalid.");
    }

    private static Task<OctoberProviderConfiguration> ReadProviderConfigurationAsync(
        IConfigurationRoot configuration) => ReadProviderDocumentAsync(configuration, requireChat: true);

    private static async Task<OctoberProviderConfiguration> ReadProviderDocumentAsync(
        IConfigurationRoot configuration, bool requireChat)
    {
        HashSet<string> suppliedSelectors = new(StringComparer.Ordinal);
        foreach (IConfigurationProvider source in configuration.Providers)
        {
            if (source.TryGet("October:Providers:ConfigurationFile", out string? supplied) &&
                supplied is not null)
                suppliedSelectors.Add(supplied);
        }
        if (suppliedSelectors.Count > 1 ||
            configuration.GetSection("October:Providers").GetChildren()
                .Any(section => !string.Equals(section.Key, "ConfigurationFile", StringComparison.Ordinal)))
            throw new InvalidOperationException("October provider configuration is invalid.");

        string path = Required(configuration, "October:Providers:ConfigurationFile");
        try
        {
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(
                path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)));
            JsonElement root = document.RootElement;
            ValidateProperties(root, "sourceIdentity", "precedence", "workerProfile", "chatProfile",
                "profiles", "protectedCredentials");
            OctoberProviderProfile[] profiles = root.GetProperty("profiles").EnumerateArray()
                .Select(profile =>
                {
                    ValidateProperties(profile, "name", "apiType", "endpoint", "model",
                        "credentialReference");
                    return new OctoberProviderProfile(
                        RequiredString(profile, "name"),
                        RequiredString(profile, "apiType") switch
                        {
                            "chat-completions" => ApiType.ChatCompletions,
                            "responses" => ApiType.Responses,
                            "messages" => ApiType.Messages,
                            _ => throw new FormatException()
                        },
                        RequiredString(profile, "endpoint"), RequiredString(profile, "model"),
                        RequiredString(profile, "credentialReference"));
                }).ToArray();
            Dictionary<string, string> credentials = new(StringComparer.Ordinal);
            foreach (JsonProperty entry in root.GetProperty("protectedCredentials").EnumerateObject())
            {
                if (!credentials.TryAdd(entry.Name, entry.Value.GetString() ?? throw new FormatException()))
                    throw new FormatException();
            }
            return new OctoberProviderConfiguration(
                RequiredString(root, "sourceIdentity"), RequiredString(root, "precedence"),
                RequiredString(root, "workerProfile"), requireChat
                    ? RequiredString(root, "chatProfile")
                    : root.TryGetProperty("chatProfile", out JsonElement chat) ? chat.GetString() ?? string.Empty : string.Empty,
                profiles, credentials);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            DecoderFallbackException or JsonException or InvalidOperationException or
            KeyNotFoundException or FormatException)
        {
            throw new InvalidOperationException("October provider configuration is invalid.");
        }

        static string RequiredString(JsonElement value, string name) =>
            value.GetProperty(name).GetString() ?? throw new FormatException();

        static void ValidateProperties(JsonElement value, params string[] names)
        {
            if (value.ValueKind != JsonValueKind.Object)
                throw new FormatException();
            HashSet<string> allowed = new(names, StringComparer.Ordinal);
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!allowed.Contains(property.Name) || !seen.Add(property.Name))
                    throw new FormatException();
            }
        }
    }
}
