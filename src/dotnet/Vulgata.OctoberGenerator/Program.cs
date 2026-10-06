using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
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
            try
            {
                providerConfiguration = await ReadProviderConfigurationAsync(configuration);
            }
            catch (InvalidOperationException)
            {
                Console.Error.WriteLine("October provider configuration failed: InvalidConfiguration.");
                return 1;
            }
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
                target, authorityConfiguration.AuthorityId);
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

    private static OctoberGeneratorCommand ReadCommand(IConfiguration configuration)
    {
        OctoberGeneratorMode mode = configuration["October:Mode"] switch
        {
            "prepare" => OctoberGeneratorMode.Prepare,
            "select" => OctoberGeneratorMode.Select,
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

        return new OctoberGeneratorCommand(mode, targetId, batchId, request);
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

    private static async Task<OctoberProviderConfiguration> ReadProviderConfigurationAsync(
        IConfigurationRoot configuration)
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
                RequiredString(root, "workerProfile"), RequiredString(root, "chatProfile"),
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
