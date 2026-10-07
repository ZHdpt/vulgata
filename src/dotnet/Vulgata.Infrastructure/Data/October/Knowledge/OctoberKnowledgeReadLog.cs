using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

public enum KnowledgeReadOperation { OpenSelection, Search, ReadExact, GetRelated }
public sealed record KnowledgeReadLogEntry(PublicationBinding Publication, Guid CorrelationId,
    KnowledgeReadOperation Operation, KnowledgeReadStatus Status);

public interface IOctoberKnowledgeReadLog
{
    Task<bool> AppendAsync(KnowledgeReadLogEntry entry, CancellationToken cancellationToken = default);
}
