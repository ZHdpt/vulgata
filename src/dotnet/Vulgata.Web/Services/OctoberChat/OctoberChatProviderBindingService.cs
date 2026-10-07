using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>Nonsecret identity of one host-frozen admission snapshot, not a historical credential version.</summary>
public sealed class OctoberChatProviderSnapshot
{
    internal OctoberChatProviderSnapshot(Guid providerId, string model, Uri endpoint)
    {
        ProviderId = providerId;
        Revision = Guid.NewGuid().ToString("N");
        Model = model;
        Endpoint = endpoint;
    }

    public Guid ProviderId { get; }
    public string Revision { get; }
    public string Model { get; }
    public Uri Endpoint { get; }
    public ApiType ApiType => ApiType.ChatCompletions;
}

/// <summary>
/// One host-owned binding per admitted turn. No default selection, provider dispatch,
/// durable admission or credential replay; the eventual request owner must still
/// check live eligibility immediately before sending.
/// </summary>
public sealed class OctoberChatProviderBindingService
{
    private readonly Guid _selectedProviderId;
    private readonly Func<CancellationToken, Task<LlmProvider?>> _readCurrent;
    private readonly ISecretEncryptionService _secrets;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private OctoberChatProviderSnapshot? _snapshot;
    private CapturedProvider? _captured;
    private bool _invalidated;

    public OctoberChatProviderBindingService(Guid selectedProviderId,
        IServiceScopeFactory scopes, ISecretEncryptionService secrets)
        : this(selectedProviderId, CreateFreshReader(selectedProviderId, scopes), secrets) { }

    // Controlled tests may supply an inert repository. Production always uses a
    // fresh scope: management repositories return tracked EF entities.
    internal OctoberChatProviderBindingService(Guid selectedProviderId,
        ILlmProviderRepository repository, ISecretEncryptionService secrets)
        : this(selectedProviderId, token => repository.GetByIdAsync(selectedProviderId, token), secrets) { }

    private OctoberChatProviderBindingService(Guid selectedProviderId,
        Func<CancellationToken, Task<LlmProvider?>> readCurrent, ISecretEncryptionService secrets)
    {
        if (selectedProviderId == Guid.Empty) throw new ArgumentException("An explicit provider is required.", nameof(selectedProviderId));
        _selectedProviderId = selectedProviderId;
        _readCurrent = readCurrent;
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    public async Task<OctoberChatProviderSnapshot?> FreezeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_invalidated) return null;
            if (_snapshot is not null)
                return await IsCurrentCoreAsync(_snapshot, cancellationToken) ? _snapshot : null;

            LlmProvider? row = await _readCurrent(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (row is null || row.Id != _selectedProviderId || string.IsNullOrWhiteSpace(row.Name) ||
                string.IsNullOrWhiteSpace(row.EncryptedApiKey) ||
                (row.SupportedApiTypes & ApiType.ChatCompletions) == 0 ||
                !Uri.TryCreate(row.BaseEndpointUrl, UriKind.Absolute, out var endpoint) ||
                endpoint.Scheme is not ("http" or "https") || endpoint.UserInfo.Length != 0 ||
                endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
            {
                _invalidated = true;
                return null;
            }

            // Copy scalar values, not a mutable tracked entity. Existing Web provider
            // administration uses Name as the model, as does ChatAgentProviderResolver.
            _captured = Capture(row);
            _snapshot = new(row.Id, row.Name, endpoint);
            return _snapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { _invalidated = true; return null; }
        finally { _gate.Release(); }
    }

    public async Task<bool> IsCurrentAsync(OctoberChatProviderSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await IsCurrentCoreAsync(snapshot, cancellationToken); }
        finally { _gate.Release(); }
    }

    /// <summary>Trusted factory only; never put this result in a DTO, log, transcript or model input.</summary>
    internal async Task<string?> DecryptCurrentKeyAsync(OctoberChatProviderSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!await IsCurrentCoreAsync(snapshot, cancellationToken)) return null;
            string key = await _secrets.DecryptAsync(SecretPurposes.LlmApiKey, _captured!.ProtectedKey, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(key)) { _invalidated = true; return null; }
            // Decryption can yield. A configuration change while it was pending must
            // not return a usable credential for the stale admission snapshot.
            return await IsCurrentCoreAsync(snapshot, cancellationToken) ? key : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { _invalidated = true; return null; }
        finally { _gate.Release(); }
    }

    private async Task<bool> IsCurrentCoreAsync(OctoberChatProviderSnapshot snapshot, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_invalidated || _captured is null || snapshot is null || !ReferenceEquals(snapshot, _snapshot)) return false;
        try
        {
            LlmProvider? current = await _readCurrent(token);
            token.ThrowIfCancellationRequested();
            if (current is not null && Capture(current) == _captured) return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { /* Unavailable authority cannot preserve dispatch eligibility. */ }
        _invalidated = true;
        return false;
    }

    private static CapturedProvider Capture(LlmProvider row) => new(row.Id, row.Name, row.NormalizedName,
        row.BaseEndpointUrl, row.EncryptedApiKey, row.SupportedApiTypes, row.DefaultAgentType,
        row.CreatedAt, row.UpdatedAt);

    private static Func<CancellationToken, Task<LlmProvider?>> CreateFreshReader(Guid providerId, IServiceScopeFactory scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        return async token =>
        {
            await using var scope = scopes.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ILlmProviderRepository>().GetByIdAsync(providerId, token);
        };
    }

    // Private comparison values only: neither a public credential identity nor a
    // version store. A new host never replays this snapshot to restart generation.
    private sealed record CapturedProvider(Guid Id, string Name, string NormalizedName, string Endpoint,
        string ProtectedKey, ApiType SupportedApis, AgentType DefaultAgent, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
}
