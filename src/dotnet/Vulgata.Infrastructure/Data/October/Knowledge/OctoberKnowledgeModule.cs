using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Explicit read-only feature activation; the host owns contexts and shared Web composition.</summary>
public static class OctoberKnowledgeModule
{
    public static IServiceCollection AddOctoberKnowledgeReads(this IServiceCollection services,
        PublicationBinding? configuredPublication, string? ownedLogDirectory = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (configuredPublication?.Knowledge is not { } binding || configuredPublication.PublicationId == Guid.Empty ||
            binding.BatchId == Guid.Empty || string.IsNullOrWhiteSpace(binding.CanonicalDatabaseIdentity) ||
            string.IsNullOrWhiteSpace(binding.TargetId) || string.IsNullOrWhiteSpace(binding.SelectionDigest) ||
            string.IsNullOrWhiteSpace(ownedLogDirectory) || !Path.IsPathFullyQualified(ownedLogDirectory) || !Directory.Exists(ownedLogDirectory))
        {
            services.AddScoped<UnavailableOctoberKnowledgeReader>();
            services.AddScoped<IOctoberKnowledgeReader>(provider => provider.GetRequiredService<UnavailableOctoberKnowledgeReader>());
            services.AddScoped<IOctoberKnowledgeDisclosedReader>(provider => provider.GetRequiredService<UnavailableOctoberKnowledgeReader>());
            return services;
        }

        // Factories are lazy: registration never connects, migrates, selects a latest wiki or obtains producer authority.
        services.AddScoped<IOctoberPublishedKnowledgeSource, OctoberKnowledgePublishedSource>();
        services.AddScoped<IOctoberKnowledgeSelectionAuthority, OctoberKnowledgeSelectionAuthority>();
        services.AddScoped<IOctoberKnowledgeReadLog>(_ => new OctoberKnowledgeOperationLog(ownedLogDirectory, timeProvider));
        services.AddScoped(provider => new OctoberKnowledgeCursorProtector(
            provider.GetRequiredService<IOctoberKnowledgeTokenProtector>(), timeProvider));
        services.AddScoped(provider =>
        {
            var source = provider.GetRequiredService<IOctoberPublishedKnowledgeSource>();
            var policy = provider.GetRequiredService<IOctoberKnowledgeDisclosurePolicy>();
            var cursors = provider.GetRequiredService<OctoberKnowledgeCursorProtector>();
            var log = provider.GetRequiredService<IOctoberKnowledgeReadLog>();
            var authority = provider.GetService<OctoberKnowledgeDependencyAuthority>();
            return authority is null ? new OctoberKnowledgeReader(source, configuredPublication, policy, cursors, log)
                : new OctoberKnowledgeReader(source, configuredPublication, policy, cursors, log, authority);
        });
        services.AddScoped<IOctoberKnowledgeReader>(provider => provider.GetRequiredService<OctoberKnowledgeReader>());
        services.AddScoped<IOctoberKnowledgeDisclosedReader>(provider => provider.GetRequiredService<OctoberKnowledgeReader>());
        return services;
    }
}

internal sealed class UnavailableOctoberKnowledgeReader : IOctoberKnowledgeReader, IOctoberKnowledgeDisclosedReader
{
    public Task<KnowledgeDisclosureReadResult<SearchPage>> SearchAsync(KnowledgeSearchRequest request,
        KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
        Task.FromResult(new KnowledgeDisclosureReadResult<SearchPage>(Status(cancellationToken), null));
    public Task<KnowledgeDisclosureReadResult<ContentPage>> ReadExactAsync(ExactReadRequest request,
        KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
        Task.FromResult(new KnowledgeDisclosureReadResult<ContentPage>(Status(cancellationToken), null));
    public Task<KnowledgeDisclosureReadResult<RelatedPage>> GetRelatedAsync(RelatedReadRequest request,
        KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
        Task.FromResult(new KnowledgeDisclosureReadResult<RelatedPage>(Status(cancellationToken), null));

    private static KnowledgeReadStatus Status(CancellationToken token) => token.IsCancellationRequested
        ? KnowledgeReadStatus.Cancelled : KnowledgeReadStatus.Unavailable;
    public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ReadSelectionResult(Status(cancellationToken), null));
    public Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SearchPage(Status(cancellationToken), [], null, null));
    public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ContentPage(Status(cancellationToken), null, null, [], null, null));
    public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RelatedPage(Status(cancellationToken), [], [], null, null));
}
