using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Web.Data;
using Vulgata.Web.Services.OctoberChat;

namespace Vulgata.Web.Explore;

/// <summary>Explicit October reader composition; never starts the generator or migrates its schema.</summary>
public static class OctoberWikiRegistration
{
    public static IServiceCollection AddOctoberWikiReader(this IServiceCollection services,
        IConfiguration configuration, Func<IServiceProvider, string> frozenConnection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(frozenConnection);

        // Copy trusted host configuration once. Runtime reloads cannot retarget a circuit.
        var selection = ReadSelection(configuration.GetSection("October:Wiki"));
        if (selection is null)
        {
            services.AddOctoberKnowledgeReads(null);
            services.AddScoped<IOctoberChatContextProvider, UnavailableOctoberChatContextProvider>();
            services.AddScoped<IOctoberChatSelectionService, UnavailableOctoberChatSelectionService>();
            services.AddOctoberChat();
            return services.AddExploreReader();
        }

        services.AddDbContextFactory<OctoberDbContext>((provider, options) =>
            options.UseNpgsql(frozenConnection(provider), npgsql =>
                npgsql.MigrationsHistoryTable("__OctoberMigrationsHistory", "october")));
        services.AddScoped<IOctoberKnowledgeLiveReadAuthority>(provider =>
            new OctoberKnowledgeIdentityAdapter(provider.GetRequiredService<IServiceScopeFactory>(),
                async token => (await provider.GetRequiredService<AuthenticationStateProvider>()
                    .GetAuthenticationStateAsync().WaitAsync(token)).User));
        services.AddScoped<IOctoberKnowledgeDisclosurePolicy, OctoberKnowledgeDisclosurePolicy>();
        services.AddSingleton<IOctoberKnowledgeTokenProtector, OctoberKnowledgeDataProtectionTokenProtector>();
        services.AddSingleton<IOctoberKnowledgeDisclosureProtector, OctoberKnowledgeDisclosureDataProtectionProtector>();
        services.AddScoped<OctoberKnowledgeDependencyAuthority>();
        services.AddScoped<IOctoberKnowledgeDependencyAuthority>(provider => provider.GetRequiredService<OctoberKnowledgeDependencyAuthority>());
        services.AddOctoberKnowledgeReads(selection.Publication, selection.LogDirectory);
        services.AddScoped<IExploreContextProvider>(provider => new OctoberExploreContextProvider(
            selection, provider.GetRequiredService<AuthenticationStateProvider>(),
            provider.GetRequiredService<IOptions<IdentityOptions>>(),
            provider.GetRequiredService<IOctoberKnowledgeDisclosurePolicy>()));
        services.AddScoped<IOctoberChatContextProvider>(provider => new OctoberChatContextProvider(
            selection, provider.GetRequiredService<AuthenticationStateProvider>(),
            provider.GetRequiredService<IOptions<IdentityOptions>>(),
            provider.GetRequiredService<IOctoberKnowledgeDisclosurePolicy>()));
        services.AddOctoberChat();
        return services.AddExploreReader();
    }

    private static OctoberWikiSelection? ReadSelection(IConfigurationSection section)
    {
        if (!bool.TryParse(section["Enabled"], out bool enabled) || !enabled ||
            !Guid.TryParse(section["BatchId"], out Guid batch) || batch == Guid.Empty ||
            !Guid.TryParse(section["PublicationId"], out Guid publication) || publication == Guid.Empty ||
            section["Mode"] is not ("Business" or "IT") ||
            string.IsNullOrWhiteSpace(section["CanonicalDatabaseIdentity"]) ||
            string.IsNullOrWhiteSpace(section["TargetId"]) ||
            string.IsNullOrWhiteSpace(section["SelectionDigest"]) ||
            section["LogDirectory"] is not { } log || !Path.IsPathFullyQualified(log) || !Directory.Exists(log))
            return null;

        var scope = section.GetSection("RepositoryIds");
        if (!scope.Exists()) return null; // Missing does not mean all repositories.
        var ids = ImmutableArray.CreateBuilder<Guid>();
        if (scope.Value is not null && scope.Value != string.Empty) return null;
        foreach (var item in scope.GetChildren())
        {
            if (!Guid.TryParse(item.Value, out Guid id) || id == Guid.Empty || ids.Contains(id)) return null;
            ids.Add(id);
        }

        return new(new(new(section["CanonicalDatabaseIdentity"]!, section["TargetId"]!, batch,
            section["SelectionDigest"]!), publication),
            section["Mode"] == "IT" ? KnowledgeMode.IT : KnowledgeMode.Business, ids.ToImmutable(), log);
    }
}

internal sealed record OctoberWikiSelection(PublicationBinding Publication, KnowledgeMode Mode,
    ImmutableArray<Guid> RepositoryIds, string LogDirectory);

/// <summary>Frozen selection, purpose-specific live identity checks; never a Document disclosure grant.</summary>
internal abstract class OctoberLiveReadContext : IDisposable
{
    private readonly OctoberWikiSelection _selection;
    private readonly AuthenticationStateProvider _authentication;
    private readonly ClaimsIdentityOptions _claims;
    private readonly IOctoberKnowledgeDisclosurePolicy _policy;
    private readonly KnowledgePurpose _purpose;
    private long _generation;
    private bool _disposed;

    protected OctoberLiveReadContext(OctoberWikiSelection selection, AuthenticationStateProvider authentication,
        IOptions<IdentityOptions> identityOptions, IOctoberKnowledgeDisclosurePolicy policy, KnowledgePurpose purpose)
    {
        _selection = selection;
        _authentication = authentication;
        _claims = identityOptions.Value.ClaimsIdentity;
        _policy = policy;
        _purpose = purpose;
        _authentication.AuthenticationStateChanged += OnAuthenticationChanged;
    }

    public event Action? ContextChanged;

    protected async Task<ReadSelectionRequest?> GetRequestAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed) return null;
        long generation = Volatile.Read(ref _generation);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var subject = Subject((await _authentication.GetAuthenticationStateAsync().WaitAsync(deadline.Token)).User);
            if (subject is null) return null;
            var request = new ReadSelectionRequest(_selection.Publication, subject, _selection.Mode,
                _purpose, _selection.RepositoryIds);
            if (await _policy.CheckAsync(new(request, [], null), deadline.Token).WaitAsync(deadline.Token) != DisclosureStatus.Allowed)
                return null;
            var currentSubject = Subject((await _authentication.GetAuthenticationStateAsync().WaitAsync(deadline.Token)).User);
            deadline.Token.ThrowIfCancellationRequested();
            return !_disposed && generation == Volatile.Read(ref _generation) && currentSubject == subject
                ? request : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
    }

    private KnowledgeSubject? Subject(ClaimsPrincipal principal)
    {
        var identities = principal.Identities.Where(identity => identity.IsAuthenticated).ToArray();
        if (identities.Length != 1) return null;
        string? user = identities[0].FindFirst(_claims.UserIdClaimType)?.Value;
        string? stamp = identities[0].FindFirst(_claims.SecurityStampClaimType)?.Value;
        return string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(stamp) ? null : new(user, stamp);
    }

    private void OnAuthenticationChanged(Task<AuthenticationState> _)
    {
        if (_disposed) return;
        Interlocked.Increment(ref _generation);
        ContextChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Interlocked.Increment(ref _generation);
        _authentication.AuthenticationStateChanged -= OnAuthenticationChanged;
    }
}

internal sealed class OctoberExploreContextProvider(OctoberWikiSelection selection,
    AuthenticationStateProvider authentication, IOptions<IdentityOptions> identityOptions,
    IOctoberKnowledgeDisclosurePolicy policy)
    : OctoberLiveReadContext(selection, authentication, identityOptions, policy, KnowledgePurpose.Reader), IExploreContextProvider
{
    public async Task<ExploreContext?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        await GetRequestAsync(cancellationToken) is { } request ? new(request) : null;
}

internal sealed class OctoberChatContextProvider(OctoberWikiSelection selection,
    AuthenticationStateProvider authentication, IOptions<IdentityOptions> identityOptions,
    IOctoberKnowledgeDisclosurePolicy policy)
    : OctoberLiveReadContext(selection, authentication, identityOptions, policy, KnowledgePurpose.Chat), IOctoberChatContextProvider
{
    public Task<ReadSelectionRequest?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        GetRequestAsync(cancellationToken);
}

internal sealed class UnavailableOctoberChatContextProvider : IOctoberChatContextProvider
{
    public event Action? ContextChanged { add { } remove { } }

    public Task<ReadSelectionRequest?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ReadSelectionRequest?>(null);
    }
}

internal sealed class UnavailableOctoberChatSelectionService : IOctoberChatSelectionService
{
    public Task<OctoberChatSelectionResult> OpenAsync(ReadSelectionRequest? trustedSelection,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new OctoberChatSelectionResult(OctoberChatEntryStatus.Unavailable, null, null));
    }
}
