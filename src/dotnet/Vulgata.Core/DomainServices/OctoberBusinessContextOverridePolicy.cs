using System.Collections.Immutable;
using System.Text;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

/// <summary>Run-specific advisory text for an existing selected context slot; contains no caller-chosen record identity.</summary>
public sealed record BusinessAdvisoryContextEntry(Guid RepositoryId, OctoberContextScope Scope, string Text);

public sealed record BusinessAdvisoryContextOverride(ImmutableArray<BusinessAdvisoryContextEntry> Entries);

/// <summary>Derives additional advisory versions without replacing frozen corpus or resolution provenance.</summary>
public static class OctoberBusinessContextOverridePolicy
{
    private static readonly UTF8Encoding _utf8 = new(false, true);

    public static bool ValidateShape(BusinessAdvisoryContextOverride contextOverride)
    {
        if (contextOverride is null || contextOverride.Entries.IsDefault ||
            contextOverride.Entries.Length > OctoberBusinessSynthesisPolicy.MaximumAdvisoryContextEntries)
            return false;
        var slots = new HashSet<(Guid, OctoberContextScope)>();
        foreach (var entry in contextOverride.Entries)
            if (entry is null || entry.RepositoryId == Guid.Empty ||
                entry.Scope is not (OctoberContextScope.System or OctoberContextScope.Repository) ||
                string.IsNullOrWhiteSpace(entry.Text) || entry.Text.Length > 10_000 ||
                !slots.Add((entry.RepositoryId, entry.Scope))) return false;
        try
        {
            foreach (var entry in contextOverride.Entries) _ = _utf8.GetByteCount(entry.Text);
            return Bounded(contextOverride.Entries);
        }
        catch (ArgumentException) { return false; }
    }

    public static ImmutableArray<BusinessSynthesisAdvisoryContext> Project(
        ImmutableArray<BusinessSynthesisAdvisoryContext> original, BusinessAdvisoryContextOverride? contextOverride)
    {
        if (contextOverride is null) return original;
        if (!ValidateShape(contextOverride)) throw new ArgumentException("Invalid advisory context override.", nameof(contextOverride));
        ValidateContext(original);
        var replacements = contextOverride.Entries.ToDictionary(entry => (entry.RepositoryId, entry.Scope));
        if (replacements.Keys.Any(slot => !original.Any(entry => (entry.RepositoryId, entry.Scope) == slot)))
            throw new ArgumentException("An override requires an existing selected advisory slot.", nameof(contextOverride));
        var projected = original.Select(entry => replacements.TryGetValue((entry.RepositoryId, entry.Scope), out var replacement)
            ? entry with { Text = replacement.Text, Contributor = entry.Contributor with { ContentDigest = OctoberKnowledgePolicy.Hash(replacement.Text) } }
            : entry).ToImmutableArray();
        ValidateContext(projected);
        return projected;
    }

    public static FlowPlanningInput Apply(FlowPlanningInput input, BusinessAdvisoryContextOverride? contextOverride)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (contextOverride is null) return input;
        _ = EffectiveContributors(input);
        var projected = Project(input.AdvisoryContext, contextOverride);
        var effective = input with
        {
            AdvisoryContext = projected,
            AdditionalAdvisoryContributors = Canonical(projected.Select(entry => entry.Contributor)
                .Where(contributor => !input.Contributors.Contains(contributor)))
        };
        _ = EffectiveContributors(effective);
        return effective;
    }

    public static ImmutableArray<Contributor> EffectiveContributors(FlowPlanningInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Contributors.IsDefault || input.AdditionalAdvisoryContributors.IsDefault ||
            input.Contributors.Any(contributor => contributor is null) ||
            input.AdditionalAdvisoryContributors.Any(contributor => contributor is null) ||
            input.AdditionalAdvisoryContributors.Length > OctoberBusinessSynthesisPolicy.MaximumAdvisoryContextEntries)
            throw new ArgumentException("Invalid original or additional advisory ledger.", nameof(input));
        ValidateContext(input.AdvisoryContext);
        foreach (var entry in input.AdvisoryContext)
        {
            if (input.Corpus?.Documents.IsDefault != false || !input.Corpus.Documents.Any(document => document.RepositoryId == entry.RepositoryId) ||
                !input.Contributors.Any(original => SameIdentity(original, entry.Contributor)))
                throw new ArgumentException("Effective advisory context must retain an original corpus identity.", nameof(input));
        }
        var expected = input.AdvisoryContext.Select(entry => entry.Contributor)
            .Where(contributor => !input.Contributors.Contains(contributor)).ToHashSet();
        if (input.AdditionalAdvisoryContributors.Distinct().Count() != input.AdditionalAdvisoryContributors.Length ||
            !expected.SetEquals(input.AdditionalAdvisoryContributors))
            throw new ArgumentException("Additional contributors must exactly match derived advisory versions.", nameof(input));
        return Canonical(input.Contributors.Concat(input.AdditionalAdvisoryContributors));
    }

    private static void ValidateContext(ImmutableArray<BusinessSynthesisAdvisoryContext> context)
    {
        if (context.IsDefault || context.Length > OctoberBusinessSynthesisPolicy.MaximumAdvisoryContextEntries)
            throw new ArgumentException("Invalid advisory context.", nameof(context));
        var slots = new HashSet<(Guid, OctoberContextScope)>();
        var systemValues = new Dictionary<Contributor, string>();
        foreach (var entry in context)
        {
            if (entry is null || entry.RepositoryId == Guid.Empty || !slots.Add((entry.RepositoryId, entry.Scope)) ||
                string.IsNullOrWhiteSpace(entry.Text) || entry.Contributor is not { } contributor ||
                contributor.AdvisoryRecordId is null || contributor.AdvisoryRecordId == Guid.Empty ||
                contributor.RootBindingIdentity is not null || contributor.SourceOrigin is not null || contributor.MetadataOrigin is not null ||
                contributor.ContentDigest != OctoberKnowledgePolicy.Hash(entry.Text))
                throw new ArgumentException("Invalid advisory context provenance.", nameof(context));
            _ = _utf8.GetByteCount(entry.Text);
            if (entry.Scope == OctoberContextScope.Repository)
            {
                if (contributor.Kind != ContributorKind.RepositoryAdvisory || contributor.RepositoryId != entry.RepositoryId || contributor.SystemId is not null)
                    throw new ArgumentException("Invalid repository advisory scope.", nameof(context));
            }
            else if (entry.Scope == OctoberContextScope.System)
            {
                if (contributor.Kind != ContributorKind.SystemAdvisory || contributor.RepositoryId is not null ||
                    contributor.SystemId is null || contributor.SystemId == Guid.Empty)
                    throw new ArgumentException("Invalid System advisory scope.", nameof(context));
                var identity = contributor with { ContentDigest = string.Empty };
                if (systemValues.TryGetValue(identity, out var text) && text != entry.Text)
                    throw new ArgumentException("A shared System context must have one effective value.", nameof(context));
                systemValues[identity] = entry.Text;
            }
            else throw new ArgumentException("Unsupported advisory scope.", nameof(context));
        }
        if (!Bounded(context)) throw new ArgumentException("The complete advisory context exceeds its byte bound.", nameof(context));
    }

    private static bool SameIdentity(Contributor left, Contributor right) =>
        (left with { ContentDigest = string.Empty }) == (right with { ContentDigest = string.Empty });

    private static ImmutableArray<Contributor> Canonical(IEnumerable<Contributor> contributors) => contributors.Distinct()
        .OrderBy(OctoberKnowledgePolicy.SerializeCanonical, StringComparer.Ordinal).ToImmutableArray();

    private static bool Bounded<T>(T value) => _utf8.GetByteCount(OctoberKnowledgePolicy.SerializeCanonical(value)) <=
        OctoberBusinessSynthesisPolicy.MaximumAdvisoryContextUtf8Bytes;
}
