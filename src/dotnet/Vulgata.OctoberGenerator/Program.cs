using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    internal static OctoberGenerateRunner ComposeGenerate(OctoberRuntimeConfiguration runtime,
        IOctoberLocalAuthority authority, IDbContextFactory<OctoberDbContext> contexts,
        ISecretEncryptionService encryption, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(encryption);
        if (!runtime.IsValid()) throw new InvalidOperationException("October runtime configuration is invalid.");
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        var log = new OctoberKnowledgeOperationLog(runtime.LogDirectory, clock);
        var sources = new OctoberScanRunSourceFactory(runtime, authority, clock);
        var clients = new OctoberWorkerClientFactory(encryption);
        return new OctoberGenerateRunner(new(authority, sources.ValidateSelection,
            (admissionAuthority, guard) => new OctoberGenerateAdmissionStore(contexts, admissionAuthority, guard, log),
            sources.OpenAsync, clients.CreateAsync,
            ports => new OctoberKnowledgeWriter(contexts, ports.CompletionAuthority, ports.SourceVerifier,
                ports.ProducerAuthorization, ports.CommitGuard, log, ports.FreezeAuthority), log), clock);
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
                    TerminalCoverage = [], Inventory = [], Gaps = [new("output_binding_invalid")]
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
            string keyRingPath;
            string applicationName;
            try
            {
                command = ReadCommand(configuration);
                if (command.Mode == OctoberGeneratorMode.Generate)
                    runtime = await ReadRuntimeConfigurationAsync(configuration);
                authorityConfiguration = ReadAuthorityConfiguration(configuration, command);
                keyRingPath = Required(configuration, "DataProtection:KeyRingPath");
                applicationName = Required(configuration, "DataProtection:ApplicationName");
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
            OctoberGenerateRunner? generate = command.Mode == OctoberGeneratorMode.Generate
                ? ComposeGenerate(runtime!, localAuthority,
                    new ConfiguredContextFactory(scope.ServiceProvider.GetRequiredService<DbContextOptions<OctoberDbContext>>()),
                    protection.GetRequiredService<ISecretEncryptionService>()) : null;
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
        new(0, 0, 0, false), [new(cancelled ? "run_interrupted" : "generate_terminal_report_unavailable")]);

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
            Matching = "not-requested",
            BusinessLogic = "not-requested",
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

    private static string? SafeRelativePath(string? path) => path is not null && path.Length <= 2048 &&
        !path.StartsWith('/') && !path.Contains('\\') && !path.Contains(':') && !path.Any(char.IsControl) &&
        (path.Length == 0 || path.Split('/').All(part => part is not ("" or "." or ".."))) ? path : null;

    private static string SafeGapCode(string code) => code switch
    {
        "owned_cleanup_unconfirmed" or "coverage_incomplete" or "invalid_run_configuration" or "authority_unavailable" or
        "begin_indeterminate" or "begin_settlement_pending" or "fresh_run_required" or "run_ineligible" or "run_interrupted" or
        "run_unavailable" or "single_file_checkpoint_limit" or "source_frame_unavailable" or "source_frame_binding_changed" or
        "file_deadline_exceeded" or "provider_settlement_unconfirmed" or "worker_not_completed" or "file_acceptance_unavailable" or
        "generate_terminal_report_unavailable" or "output_binding_invalid" or "commit_indeterminate" or "operation_log_unavailable" or "phase_ineligible" or
        "publication_failed" or "publication_interrupted" or "publication_unavailable" or "no_cl_content" or
        "freeze_settlement_pending" or "publication_settlement_pending" or "phase_closed" or "unsupported_stage" or
        "publication_not_started" or "source-authority-or-identity-lost" or "incomplete-directory" or "source-limitation" or
        "directory-limit" or "metadata-limit" or "listing-unavailable" or "cancelled" => code,
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
            targets.ToImmutable(), TimeSpan.FromSeconds(30), TimeSpan.FromHours(1));
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
