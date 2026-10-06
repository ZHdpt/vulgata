using System.Collections.Immutable;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Configuration;

public sealed record OctoberProviderProfile(
    string Name,
    ApiType ApiType,
    string Endpoint,
    string Model,
    string CredentialReference)
{
    public override string ToString() => nameof(OctoberProviderProfile);
}

public enum OctoberProviderBindingStatus
{
    Bound,
    InvalidConfiguration,
    CredentialUnavailable,
    Cancelled
}

public sealed record OctoberProviderBindingResult(
    OctoberProviderBindingStatus Status,
    OctoberBoundProviderConfiguration? Binding);

public sealed class OctoberProviderConfiguration
{
    public OctoberProviderConfiguration(
        string sourceIdentity,
        string precedence,
        string workerProfile,
        string chatProfile,
        IReadOnlyCollection<OctoberProviderProfile> profiles,
        IReadOnlyDictionary<string, string> protectedCredentials)
    {
        SourceIdentity = sourceIdentity ?? throw new ArgumentNullException(nameof(sourceIdentity));
        Precedence = precedence ?? throw new ArgumentNullException(nameof(precedence));
        WorkerProfile = workerProfile ?? throw new ArgumentNullException(nameof(workerProfile));
        ChatProfile = chatProfile ?? throw new ArgumentNullException(nameof(chatProfile));
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(protectedCredentials);
        Profiles = profiles.ToImmutableArray();
        ProtectedCredentials = protectedCredentials.ToImmutableDictionary(StringComparer.Ordinal);
    }

    public string SourceIdentity { get; }
    public string Precedence { get; }
    public string WorkerProfile { get; }
    public string ChatProfile { get; }
    public ImmutableArray<OctoberProviderProfile> Profiles { get; }
    public ImmutableDictionary<string, string> ProtectedCredentials { get; }

    public override string ToString() => nameof(OctoberProviderConfiguration);
}

public sealed class OctoberBoundProviderConfiguration
{
    internal OctoberBoundProviderConfiguration(
        string sourceIdentity,
        string precedence,
        OctoberProviderProfile worker,
        OctoberProviderProfile chat)
    {
        SourceIdentity = sourceIdentity;
        Precedence = precedence;
        Worker = worker;
        Chat = chat;
    }

    public string SourceIdentity { get; }
    public string Precedence { get; }
    public OctoberProviderProfile Worker { get; }
    public OctoberProviderProfile Chat { get; }

    public override string ToString() => nameof(OctoberBoundProviderConfiguration);
}

public sealed class OctoberProviderBinder
{
    private readonly ISecretEncryptionService _encryption;

    public OctoberProviderBinder(ISecretEncryptionService encryption)
    {
        _encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
    }

    public async Task<OctoberProviderBindingResult> BindAsync(
        OctoberProviderConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (cancellationToken.IsCancellationRequested)
            return new OctoberProviderBindingResult(OctoberProviderBindingStatus.Cancelled, null);
        if (!IsExact(configuration.SourceIdentity) ||
            !string.Equals(configuration.Precedence, "only-source", StringComparison.Ordinal) ||
            !IsExact(configuration.WorkerProfile) ||
            !IsExact(configuration.ChatProfile))
            return new OctoberProviderBindingResult(OctoberProviderBindingStatus.InvalidConfiguration, null);

        Dictionary<string, OctoberProviderProfile> profiles = new(StringComparer.Ordinal);
        foreach (OctoberProviderProfile? profile in configuration.Profiles)
        {
            if (profile is null || !IsExact(profile.Name) ||
                !profiles.TryAdd(profile.Name, profile))
                return new OctoberProviderBindingResult(OctoberProviderBindingStatus.InvalidConfiguration, null);
        }

        if (!profiles.TryGetValue(configuration.WorkerProfile, out OctoberProviderProfile? worker) ||
            !profiles.TryGetValue(configuration.ChatProfile, out OctoberProviderProfile? chat) ||
            !ValidSelectedProfile(worker) || !ValidSelectedProfile(chat))
            return new OctoberProviderBindingResult(OctoberProviderBindingStatus.InvalidConfiguration, null);

        HashSet<string> selectedReferences = new(StringComparer.Ordinal)
        {
            worker.CredentialReference,
            chat.CredentialReference
        };
        foreach (string reference in selectedReferences)
        {
            if (!configuration.ProtectedCredentials.TryGetValue(reference, out string? ciphertext) ||
                string.IsNullOrWhiteSpace(ciphertext))
                return new OctoberProviderBindingResult(OctoberProviderBindingStatus.InvalidConfiguration, null);
        }

        foreach (string reference in selectedReferences)
        {
            if (cancellationToken.IsCancellationRequested)
                return new OctoberProviderBindingResult(OctoberProviderBindingStatus.Cancelled, null);

            string ciphertext = configuration.ProtectedCredentials[reference];
            bool credentialAvailable;
            try
            {
                credentialAvailable = !string.IsNullOrWhiteSpace(await _encryption.DecryptAsync(
                    "Vulgata.LlmProviders.ApiKey", ciphertext, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new OctoberProviderBindingResult(OctoberProviderBindingStatus.Cancelled, null);
            }
            catch (Exception)
            {
                return new OctoberProviderBindingResult(OctoberProviderBindingStatus.CredentialUnavailable, null);
            }

            if (cancellationToken.IsCancellationRequested)
                return new OctoberProviderBindingResult(OctoberProviderBindingStatus.Cancelled, null);
            if (!credentialAvailable)
                return new OctoberProviderBindingResult(OctoberProviderBindingStatus.CredentialUnavailable, null);
        }

        return new OctoberProviderBindingResult(OctoberProviderBindingStatus.Bound,
            new OctoberBoundProviderConfiguration(configuration.SourceIdentity,
                configuration.Precedence, worker, chat));
    }

    private static bool ValidSelectedProfile(OctoberProviderProfile profile) =>
        profile.ApiType is ApiType.ChatCompletions or ApiType.Responses or ApiType.Messages &&
        IsExact(profile.Model) && IsExact(profile.CredentialReference) &&
        IsValidEndpoint(profile.Endpoint);

    private static bool IsValidEndpoint(string? endpoint) =>
        IsExact(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme is "http" or "https" &&
        uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;

    private static bool IsExact(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
