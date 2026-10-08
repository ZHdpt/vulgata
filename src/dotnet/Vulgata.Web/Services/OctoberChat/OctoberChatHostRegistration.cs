using System.Globalization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Web.Data;
using Vulgata.Web.Explore;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>Explicit host composition. Registration neither obtains credentials nor dispatches a request.</summary>
internal static class OctoberChatHostRegistration
{
    internal static IServiceCollection AddOctoberChatHost(this IServiceCollection services,
        IConfiguration configuration, OctoberWikiSelection? selection)
    {
        var section = configuration.GetSection("October:Chat");
        bool enabled = bool.TryParse(section["Enabled"], out bool requested) && requested;
        bool providerValid = Guid.TryParse(section["ProviderId"], out Guid providerId) && providerId != Guid.Empty;
        bool requestsValid = Limit(section["MaximumConcurrentRequests"], 4, out int requests);
        bool executionsValid = Limit(section["MaximumConcurrentExecutions"], 16, out int executions);
        bool usable = selection is not null && enabled && providerValid && requestsValid && executionsValid;
        services.TryAddSingleton<IOctoberDialoguePayloadCodec, OctoberDialoguePayloadCodec>();
        services.TryAddSingleton(provider => new OctoberChatExecutionRegistry(
            provider.GetRequiredService<IOctoberDialoguePayloadCodec>(), (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(CommitEligibilityStatus.AuthorityUnavailable);
            }, usable ? executions : 16));
        services.TryAddSingleton<IOctoberDialogueExecutionAuthority>(provider => provider.GetRequiredService<OctoberChatExecutionRegistry>());
        services.TryAddSingleton<IOctoberDialogueCommitGuard>(provider => provider.GetRequiredService<OctoberChatExecutionRegistry>().CommitGuard);
        services.TryAddSingleton(_ => new OctoberChatRequestAdmission(usable ? requests : 4));

        if (usable)
        {
            services.TryAddScoped(provider => new OctoberChatHostScopeFactory(provider, selection!, providerId));
            services.TryAddScoped(provider => new OctoberChatApplication(
                provider.GetRequiredService<OctoberChatExecutionRegistry>(),
                provider.GetRequiredService<OctoberChatHostScopeFactory>().CreateAsync, InputDigestsAsync,
                attachmentContext: provider.GetRequiredService<IOctoberChatContextProvider>()));
        }
        else
        {
            // A configured October wiki must not silently route into the legacy composer.
            services.TryAddScoped(provider => new OctoberChatApplication(
                provider.GetRequiredService<OctoberChatExecutionRegistry>(),
                (_, token) => { token.ThrowIfCancellationRequested(); return Task.FromResult<OctoberChatTurnScope?>(null); },
                (_, _, token) => { token.ThrowIfCancellationRequested(); return Task.FromResult<OctoberChatInputDigests?>(null); }));
        }
        return services;
    }

    private static bool Limit(string? value, int maximum, out int result)
    {
        result = maximum;
        return value is null || int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result)
            && result > 0 && result <= maximum;
    }

    private static async Task<OctoberChatInputDigests?> InputDigestsAsync(KnowledgeMode mode, string question, CancellationToken token)
    {
        var result = await Agents.ChatExecution.October.OctoberChatInputDigests.CreateAsync(mode, question, token);
        return result is null ? null : new(result.PromptDigest, result.InitialInputDigest);
    }
}

/// <summary>Captures the originating user's live accessor, never a child circuit or Scan operator session.</summary>
internal sealed class OctoberChatHostScopeFactory
{
    private readonly IServiceScopeFactory _scopes;
    private readonly AuthenticationStateProvider _authentication;
    private readonly IOptions<IdentityOptions> _identityOptions;
    private readonly OctoberWikiSelection _selection;
    private readonly Guid _providerId;
    private readonly OctoberChatExecutionRegistry _registry;
    private readonly IOctoberDialoguePayloadCodec _codec;
    private readonly OctoberChatRequestAdmission _requests;
    private readonly OctoberChatAttachmentContext _attachment;

    internal OctoberChatHostScopeFactory(IServiceProvider origin, OctoberWikiSelection selection, Guid providerId)
    {
        _scopes = origin.GetRequiredService<IServiceScopeFactory>();
        _authentication = origin.GetRequiredService<AuthenticationStateProvider>();
        _identityOptions = origin.GetRequiredService<IOptions<IdentityOptions>>();
        _registry = origin.GetRequiredService<OctoberChatExecutionRegistry>();
        _codec = origin.GetRequiredService<IOctoberDialoguePayloadCodec>();
        _requests = origin.GetRequiredService<OctoberChatRequestAdmission>();
        _attachment = origin.GetRequiredService<OctoberChatAttachmentContext>();
        _selection = selection;
        _providerId = providerId;
    }

    internal async Task<OctoberChatTurnScope?> CreateAsync(ReadSelectionRequest request, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (request is null || request.Publication != _selection.Publication || request.Subject is null
            || request.Purpose != KnowledgePurpose.Chat || request.Mode is not (KnowledgeMode.Business or KnowledgeMode.IT)
            || request.RepositoryScope.IsDefault || request.RepositoryScope.Length != _selection.RepositoryIds.Length
            || request.RepositoryScope.Distinct().Count() != request.RepositoryScope.Length
            || !request.RepositoryScope.Order().SequenceEqual(_selection.RepositoryIds.Order())) return null;

        var scope = _scopes.CreateAsyncScope();
        OctoberChatContextProvider? context = null;
        OctoberDialogueStore? store = null;
        bool transferred = false;
        try
        {
            var services = scope.ServiceProvider;
            var live = new OctoberKnowledgeIdentityAdapter(_scopes,
                async cancellation => (await _authentication.GetAuthenticationStateAsync().WaitAsync(cancellation)).User);
            var policy = new OctoberKnowledgeDisclosurePolicy(live,
                services.GetRequiredService<IOctoberKnowledgeSelectionAuthority>());
            context = new(_selection with { Mode = request.Mode }, _authentication, _identityOptions, policy, request.Subject);
            var current = await context.GetCurrentAsync(token);
            if (current is null || current.Subject != request.Subject) return null;
            token.ThrowIfCancellationRequested();
            var dependencies = new OctoberKnowledgeDependencyAuthority(policy,
                services.GetRequiredService<IOctoberKnowledgeDisclosureProtector>());
            var reader = new OctoberKnowledgeReader(services.GetRequiredService<IOctoberPublishedKnowledgeSource>(),
                _selection.Publication, policy,
                new OctoberKnowledgeCursorProtector(services.GetRequiredService<IOctoberKnowledgeTokenProtector>()),
                services.GetRequiredService<IOctoberKnowledgeReadLog>(), dependencies);
            store = new(services.GetRequiredService<IDbContextFactory<OctoberDbContext>>(), _selection.Publication.Knowledge,
                policy, dependencies, _registry, _registry.CommitGuard, _codec, TimeProvider.System);
            var providers = new OctoberChatProviderBindingService(_providerId, _scopes,
                services.GetRequiredService<ISecretEncryptionService>());
            var result = new OctoberChatTurnScope(context, store, _codec, reader, reader, dependencies,
                _registry, providers, _requests, () => CleanupAsync(store, context, scope), policy);
            token.ThrowIfCancellationRequested();
            _attachment.BindOperation(request);
            transferred = true;
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
        finally { if (!transferred) await CleanupAsync(store, context, scope); }
    }

    private static async ValueTask CleanupAsync(OctoberDialogueStore? store, OctoberChatContextProvider? context,
        AsyncServiceScope scope)
    {
        bool failed = false;
        try { if (store is not null) await store.DisposeAsync(); }
        catch (Exception) { failed = true; }
        try { context?.Dispose(); }
        catch (Exception) { failed = true; }
        try { await scope.DisposeAsync(); }
        catch (Exception) { failed = true; }
        if (failed) throw new InvalidOperationException("October Chat host cleanup could not be confirmed.");
    }
}
