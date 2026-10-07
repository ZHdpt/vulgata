using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Core.Entities.OctoberKnowledge;

/// <summary>Stable root identity; mutable ranges, document versions and graph connectivity are excluded.</summary>
public sealed record LogicalFlow(Guid LogicalFlowId, Guid BatchId, Guid RootUnitId,
    FlowRootKind RootKind, string RootSelector, Guid HomeRepositoryId);
