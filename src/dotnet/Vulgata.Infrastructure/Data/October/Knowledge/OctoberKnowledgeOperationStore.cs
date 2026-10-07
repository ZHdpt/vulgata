using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Exact durable lookup only. Absence is never evidence that an outstanding commit cannot finish.</summary>
internal sealed class OctoberKnowledgeOperationStore(IDbContextFactory<OctoberDbContext> contexts)
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public async Task<KnowledgeOperation?> FindAsync(OperationKey operation, CancellationToken cancellationToken)
    {
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        return await context.KnowledgeOperations.AsNoTracking().SingleOrDefaultAsync(x =>
            x.BatchId == operation.BatchId && x.Kind == operation.Kind && x.OperationId == operation.OperationId, cancellationToken);
    }

    public static ImmutableArray<Contributor> Contributors(KnowledgeOperation operation) =>
        JsonSerializer.Deserialize<ImmutableArray<Contributor>>(operation.ContributorsJson, _json);

    public static KnowledgeWriteResult Result(KnowledgeOperation operation) =>
        JsonSerializer.Deserialize<KnowledgeWriteResult>(operation.CommittedResultJson, _json) ??
        throw new JsonException("Missing committed operation result.");
}
