using System.Collections.Immutable;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

public sealed record FlowPlanningInputResult(KnowledgeReadStatus Status, FlowPlanningInput? Input);

/// <summary>
/// Private producer read of exact immutable corpus and complete accepted resolution inputs.
/// The owner validates stored provenance and current all-contributor authority before disclosure.
/// Roots are explicit selections; supplying a root cannot mint a CodeUnit or grant source authority.
/// Incomplete resolution, inconsistent storage and unavailable authority produce no partial input.
/// </summary>
public interface IOctoberFlowPlanningInputLoader
{
    Task<FlowPlanningInputResult> LoadAsync(CorpusHandle corpus, ImmutableArray<FlowRootSelection> roots,
        CancellationToken cancellationToken = default);
}
