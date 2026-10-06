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
            services.AddScoped<IOctoberKnowledgeReader, UnavailableOctoberKnowledgeReader>();
            return services;
        }

        // Factories are lazy: registration never connects, migrates, selects a latest wiki or obtains producer authority.
        services.AddScoped<IOctoberPublishedKnowledgeSource, OctoberKnowledgePublishedSource>();
        services.AddScoped<IOctoberKnowledgeSelectionAuthority, OctoberKnowledgeSelectionAuthority>();
        services.AddScoped<IOctoberKnowledgeReadLog>(_ => new OctoberKnowledgeOperationLog(ownedLogDirectory, timeProvider));
        services.AddScoped(provider => new OctoberKnowledgeCursorProtector(
            provider.GetRequiredService<IOctoberKnowledgeTokenProtector>(), timeProvider));
        services.AddScoped<IOctoberKnowledgeReader>(provider => new OctoberKnowledgeReader(
            provider.GetRequiredService<IOctoberPublishedKnowledgeSource>(), configuredPublication,
            provider.GetRequiredService<IOctoberKnowledgeDisclosurePolicy>(),
            provider.GetRequiredService<OctoberKnowledgeCursorProtector>(),
            provider.GetRequiredService<IOctoberKnowledgeReadLog>()));
        return services;
    }
}

internal sealed class UnavailableOctoberKnowledgeReader : IOctoberKnowledgeReader
{
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
