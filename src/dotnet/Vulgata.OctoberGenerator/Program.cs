using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Configuration;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Web.Data;

namespace Vulgata.OctoberGenerator;

public static class OctoberGeneratorEntryPoint
{
    public static async Task<int> Main()
    {
        try
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            OctoberGeneratorCommand command;
            OctoberAuthorityConfiguration authorityConfiguration;
            string keyRingPath;
            string applicationName;
            try
            {
                command = ReadCommand(configuration);
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
            Console.Out.WriteLine("October provider configuration Bound.");

            IOctoberOperatorCredentialSource credentialSource;
            try
            {
                credentialSource = Required(configuration, "October:Operator:CredentialSource") switch
                {
                    "console" when configuration["October:Operator:CredentialFile"] is null =>
                        new OctoberConsoleCredentialSource(),
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
            OctoberGeneratorModule module = new(
                scope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>(),
                target, authorityConfiguration.AuthorityId, provider: workerProvider);
            OctoberGeneratorExecutionResult result = await module.ExecuteAsync(command);
            if (result.Status == OctoberGeneratorExecutionStatus.Denied)
                Console.Error.WriteLine("October setup failed: Denied.");
            if (result.Status == OctoberGeneratorExecutionStatus.AuthorityUnavailable)
                Console.Error.WriteLine("October setup failed: AuthorityUnavailable.");
            if (result.Status == OctoberGeneratorExecutionStatus.TargetUnavailable)
                Console.Error.WriteLine("October setup failed: TargetUnavailable.");
            if (result.Status == OctoberGeneratorExecutionStatus.Prepared &&
                result.CommittedBatch is { } committed)
                Console.Out.WriteLine($"October setup Prepared: batch {committed.BatchId:D}.");
            if (result.Status == OctoberGeneratorExecutionStatus.Selected &&
                result.BoundSetupData is { } bound)
                Console.Out.WriteLine($"October setup Selected: batch {bound.BatchId:D}.");
            return result.Status is OctoberGeneratorExecutionStatus.Prepared or
                OctoberGeneratorExecutionStatus.Selected ? 0 : 1;
        }
        catch (Exception)
        {
            return 1;
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
