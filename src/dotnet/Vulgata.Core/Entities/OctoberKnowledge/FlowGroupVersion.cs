using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Core.Entities.OctoberKnowledge;

/// <summary>Immutable root-specific directed slice, separate from the complete supplied input lineage.</summary>
public sealed record FlowGroupVersion(Guid GroupVersionId, LogicalFlow Flow, CorpusHandle Corpus,
    DocumentVersionReference RootVersion, ImmutableArray<DocumentVersionReference> Members,
    ImmutableArray<AcceptedRelationship> Relationships, ImmutableArray<FlowUncertainty> Uncertainties,
    ImmutableArray<FlowFrontier> Frontiers, FlowPlanningLimits Limits, BLDependency Dependencies);
