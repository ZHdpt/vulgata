using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Owner boundary for atomic rooted BL publication and exact finalization reconciliation.</summary>
public sealed class OctoberFlowPublicationStore : IOctoberFlowPublicationStore
{
    private readonly IDbContextFactory<OctoberDbContext> _contexts;
    private readonly IOctoberKnowledgeProducerAuthorization _authorization;
    private readonly IOctoberCommitSubmissionGuard _guard;
    private readonly IOctoberKnowledgeOperationLog _log;

    public OctoberFlowPublicationStore(IDbContextFactory<OctoberDbContext> contexts,
        IOctoberKnowledgeProducerAuthorization producerAuthorization, IOctoberCommitSubmissionGuard commitGuard,
        IOctoberKnowledgeOperationLog operationLog)
    {
        _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        _authorization = producerAuthorization ?? throw new ArgumentNullException(nameof(producerAuthorization));
        _guard = commitGuard ?? throw new ArgumentNullException(nameof(commitGuard));
        _log = operationLog ?? throw new ArgumentNullException(nameof(operationLog));
    }

    public Task<BusinessFinalizationResult> PublishAsync(FlowFinalizationPublicationRequest request,
        CancellationToken cancellationToken = default) => Task.FromResult(Indeterminate(request.Operation));

    public Task<BusinessFinalizationResult> LookupAsync(CorpusHandle corpus, OperationKey operation,
        string canonicalPayloadDigest, CancellationToken cancellationToken = default) => Task.FromResult(Indeterminate(operation));

    private static BusinessFinalizationResult Indeterminate(OperationKey operation) =>
        new(BusinessFinalizationStatus.CommitIndeterminate, operation, null, new(true, []))
        {
            PersistenceStatus = KnowledgeWriteStatus.CommitIndeterminate
        };
}
