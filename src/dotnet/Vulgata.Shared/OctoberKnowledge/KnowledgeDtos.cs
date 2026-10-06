using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Shared.OctoberKnowledge;

/// <summary>Browser transport selection. The host supplies subject, purpose and database binding.</summary>
public sealed record OpenKnowledgeSelectionRequest(Guid BatchId, Guid PublicationId, KnowledgeMode Mode,
    ImmutableArray<Guid> RepositoryScope);

public sealed record SearchKnowledgeRequest(string SelectionToken, string Query = "",
    int PageSize = KnowledgeContract.DefaultPageSize, string? Continuation = null);

public sealed record ReadKnowledgeRequest(string SelectionToken, DocumentHandle Document, string? Continuation = null);

public sealed record RelatedKnowledgeRequest(string SelectionToken, DocumentHandle Document,
    int PageSize = KnowledgeContract.DefaultPageSize, string? Continuation = null);

/// <summary>Canonical owner response; immutable collections and exact handles are not redefined.</summary>
public sealed record KnowledgeSearchResponse(SearchPage Page);
public sealed record KnowledgeContentResponse(ContentPage Page);
public sealed record KnowledgeRelatedResponse(RelatedPage Page);
