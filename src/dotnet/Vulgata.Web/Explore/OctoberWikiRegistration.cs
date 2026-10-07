using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Web.Data;

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
        services.AddOctoberKnowledgeReads(selection.Publication, selection.LogDirectory);
        services.AddScoped<IExploreContextProvider>(provider => new OctoberExploreContextProvider(
            selection, provider.GetRequiredService<AuthenticationStateProvider>(),
            provider.GetRequiredService<IOptions<IdentityOptions>>(),
            provider.GetRequiredService<IOctoberKnowledgeDisclosurePolicy>()));
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

/// <summary>One frozen reader selection per circuit, with live identity and owner policy on every observation.</summary>
internal sealed class OctoberExploreContextProvider : IExploreContextProvider, IDisposable
{
    private readonly OctoberWikiSelection _selection;
    private readonly AuthenticationStateProvider _authentication;
    private readonly ClaimsIdentityOptions _claims;
    private readonly IOctoberKnowledgeDisclosurePolicy _policy;
    private long _generation;
    private bool _disposed;

    public OctoberExploreContextProvider(OctoberWikiSelection selection, AuthenticationStateProvider authentication,
        IOptions<IdentityOptions> identityOptions, IOctoberKnowledgeDisclosurePolicy policy)
    {
        _selection = selection;
        _authentication = authentication;
        _claims = identityOptions.Value.ClaimsIdentity;
        _policy = policy;
        _authentication.AuthenticationStateChanged += OnAuthenticationChanged;
    }

    public event Action? ContextChanged;

    public async Task<ExploreContext?> GetCurrentAsync(CancellationToken cancellationToken = default)
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
                KnowledgePurpose.Reader, _selection.RepositoryIds);
            if (await _policy.CheckAsync(new(request, [], null), deadline.Token).WaitAsync(deadline.Token) != DisclosureStatus.Allowed)
                return null;
            var currentSubject = Subject((await _authentication.GetAuthenticationStateAsync().WaitAsync(deadline.Token)).User);
            deadline.Token.ThrowIfCancellationRequested();
            return !_disposed && generation == Volatile.Read(ref _generation) && currentSubject == subject
                ? new(request) : null;
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
