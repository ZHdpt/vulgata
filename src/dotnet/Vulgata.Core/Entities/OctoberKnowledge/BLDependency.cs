using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Core.Entities.OctoberKnowledge;

/// <summary>
/// Exact complete supplied input ledger for synthesis, independent of slice membership and citations.
/// Contributors retain advisory, source and metadata identities and digests, including uncited input.
/// This domain value has no persistence mapping in the controlled planner increment.
/// </summary>
public sealed record BLDependency(ImmutableArray<DocumentVersionReference> Documents,
    ImmutableArray<AcceptedRelationship> Relationships, ImmutableArray<FlowUncertainty> Uncertainties,
    ImmutableArray<Contributor> Contributors)
{
    public ImmutableArray<FlowFileUncertainty> FileUncertainties { get; init; } = [];
}
