using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.OctoberGenerator;

internal sealed record OctoberBusinessRootSetting(Guid RepositoryId, string RelativePath,
    string SemanticSelector, FlowRootKind Kind);

internal sealed record OctoberBusinessSettings(ImmutableArray<OctoberBusinessRootSetting> Roots,
    FlowPlanningLimits Limits, long MaximumAttemptTokens)
{
    public bool CaptureSemanticDiagnostics { get; init; }
    public bool EnableReferencedSourceRetrieval { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BusinessAdvisoryContextOverride? ContextOverride { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResolutionPolicyVersion { get; init; }

    internal bool IsValid() => !Roots.IsDefault && Roots.Length <= OctoberBusinessFinalizationPolicy.MaximumRoots &&
        Limits is { MaxNodes: >= 1, MaxEdges: >= 0, MaxDepth: >= 0 } &&
        Limits.MaxNodes <= OctoberFlowPlanner.MaximumNodes && Limits.MaxEdges <= OctoberFlowPlanner.MaximumEdges &&
        Limits.MaxDepth <= OctoberFlowPlanner.MaximumDepth && MaximumAttemptTokens > 0 &&
        Roots.All(root => root is not null && root.RepositoryId != Guid.Empty &&
            root.Kind == FlowRootKind.SelectedCodeUnit && ValidPath(root.RelativePath) &&
            !string.IsNullOrWhiteSpace(root.SemanticSelector) && root.SemanticSelector.Length <= 1024 &&
            root.SemanticSelector.Trim() == root.SemanticSelector && !root.SemanticSelector.Any(char.IsControl)) &&
        Roots.Distinct().Count() == Roots.Length &&
        OctoberResolutionPolicy.IsSupportedSelection(ResolutionPolicyVersion) &&
        (ContextOverride is null || OctoberBusinessContextOverridePolicy.ValidateShape(ContextOverride));

    internal ImmutableArray<FlowRootSelection> SelectRoots(FlowPlanningInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!IsValid() || input.Units.IsDefault)
            throw new InvalidOperationException("October Business root selection is invalid.");
        var selected = ImmutableArray.CreateBuilder<FlowRootSelection>();
        foreach (var root in Roots)
        {
            var units = input.Units.Where(unit => unit.PrimaryOrigin.RepositoryId == root.RepositoryId &&
                unit.PrimaryOrigin.RelativePath == root.RelativePath && unit.SemanticSelector == root.SemanticSelector).Take(2).ToArray();
            if (units.Length != 1)
                throw new InvalidOperationException("October Business root selection is not exact.");
            var unit = units[0];
            selected.Add(new(unit.UnitId, unit.Document, root.Kind, unit.SemanticSelector));
        }
        return selected.ToImmutable();
    }

    private static bool ValidPath(string? path) => !string.IsNullOrWhiteSpace(path) && path.Length <= 2048 &&
        path.Trim() == path && !path.StartsWith('/') && !path.Contains('\\') && !path.Contains(':') &&
        !path.Contains('*') && !path.Contains('?') && !path.Any(char.IsControl) &&
        path.Split('/').All(part => part is not ("" or "." or "..") && part.Trim() == part && !part.EndsWith('.'));
}
