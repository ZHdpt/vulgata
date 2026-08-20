using System.Collections.ObjectModel;

namespace Vulgata.Infrastructure.CommunicationDetection;

public static class CommunicationCatalogEvaluator
{
    public static IReadOnlyList<CommunicationCatalogMatch> Evaluate(
        CommunicationPatternCatalog catalog,
        SafeXmlProjection projection)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(projection);
        var matches = new List<CommunicationCatalogMatch>();
        foreach (CommunicationPatternRule rule in catalog.Rules)
        {
            if (!GlobMatches(rule.PathGlob, projection.CanonicalPath))
            {
                continue;
            }

            for (int index = 0; index < projection.Nodes.Count; index++)
            {
                SafeXmlNode node = projection.Nodes[index];
                if (!StepMatches(rule.Selector[^1], node) ||
                    !SelectorMatches(rule.Selector, projection.Nodes, index) ||
                    !AncestorMatches(rule.Ancestor, projection.Nodes, index) ||
                    !AttributesMatch(rule.AttributeTests, node.Attributes))
                {
                    continue;
                }

                string? identifier = Extract(rule.Extraction, node);
                if (identifier is null)
                {
                    continue;
                }

                identifier = identifier.Trim();
                if (rule.RemovePrefix is not null &&
                    identifier.StartsWith(rule.RemovePrefix, StringComparison.Ordinal))
                {
                    identifier = identifier[rule.RemovePrefix.Length..].Trim();
                }

                if (identifier.Length is 0 or > SafeXmlReader.MaximumEvidenceCharacters)
                {
                    continue;
                }

                matches.Add(new(
                    rule.CommunicationKind,
                    rule.Rule,
                    rule.Provenance,
                    node.Locator,
                    identifier,
                    rule.Identity));
            }
        }

        return new ReadOnlyCollection<CommunicationCatalogMatch>(matches);
    }

    private static bool SelectorMatches(
        IReadOnlyList<CommunicationPatternElementStep> selector,
        IReadOnlyList<SafeXmlNode> nodes,
        int nodeIndex)
    {
        int expectedDepth = nodes[nodeIndex].Depth;
        int selectorIndex = selector.Count - 1;
        for (int index = nodeIndex; index >= 0 && selectorIndex >= 0; index--)
        {
            SafeXmlNode candidate = nodes[index];
            if (candidate.Depth == expectedDepth && StepMatches(selector[selectorIndex], candidate))
            {
                selectorIndex--;
                expectedDepth--;
            }
        }

        return selectorIndex < 0;
    }

    private static bool AncestorMatches(
        IReadOnlyList<CommunicationPatternElementStep>? ancestor,
        IReadOnlyList<SafeXmlNode> nodes,
        int nodeIndex) =>
        ancestor is null || SelectorMatches(ancestor, nodes, nodeIndex - 1 < 0 ? nodeIndex : nodeIndex - 1);

    private static bool StepMatches(CommunicationPatternElementStep step, SafeXmlNode node) =>
        StringComparer.Ordinal.Equals(step.NamespaceUri, node.NamespaceUri) &&
        StringComparer.Ordinal.Equals(step.LocalName, node.LocalName);

    private static bool AttributesMatch(
        IReadOnlyList<CommunicationPatternAttributeTest> tests,
        IReadOnlyList<SafeXmlAttribute> attributes) =>
        tests.All(test => attributes.Any(attribute =>
            StringComparer.Ordinal.Equals(test.NamespaceUri, attribute.NamespaceUri) &&
            StringComparer.Ordinal.Equals(test.LocalName, attribute.LocalName) &&
            (test.EqualsValue is null || StringComparer.Ordinal.Equals(test.EqualsValue, attribute.Value))));

    private static string? Extract(CommunicationPatternExtraction extraction, SafeXmlNode node) =>
        extraction.Kind == CommunicationPatternExtractionKind.ElementText
            ? node.Text
            : node.Attributes.FirstOrDefault(attribute =>
                StringComparer.Ordinal.Equals(extraction.NamespaceUri, attribute.NamespaceUri) &&
                StringComparer.Ordinal.Equals(extraction.LocalName, attribute.LocalName))?.Value;

    private static bool GlobMatches(string pattern, string path)
    {
        string[] patternSegments = pattern.Split('/');
        string[] pathSegments = path.Split('/');
        return MatchSegments(patternSegments, 0, pathSegments, 0);
    }

    private static bool MatchSegments(
        IReadOnlyList<string> pattern,
        int patternIndex,
        IReadOnlyList<string> path,
        int pathIndex)
    {
        if (patternIndex == pattern.Count)
        {
            return pathIndex == path.Count;
        }

        if (pattern[patternIndex] == "**")
        {
            for (int index = pathIndex; index <= path.Count; index++)
            {
                if (MatchSegments(pattern, patternIndex + 1, path, index))
                {
                    return true;
                }
            }

            return false;
        }

        return pathIndex < path.Count &&
            MatchSegment(pattern[patternIndex], path[pathIndex]) &&
            MatchSegments(pattern, patternIndex + 1, path, pathIndex + 1);
    }

    private static bool MatchSegment(string pattern, string value)
    {
        int[,] state = new int[pattern.Length + 1, value.Length + 1];
        state[0, 0] = 1;
        for (int p = 0; p < pattern.Length; p++)
        {
            for (int v = 0; v <= value.Length; v++)
            {
                if (state[p, v] == 0)
                {
                    continue;
                }

                if (pattern[p] == '*')
                {
                    for (int next = v; next <= value.Length; next++) state[p + 1, next] = 1;
                }
                else if (pattern[p] == '?')
                {
                    if (v < value.Length) state[p + 1, v + 1] = 1;
                }
                else if (v < value.Length && pattern[p] == value[v])
                {
                    state[p + 1, v + 1] = 1;
                }
            }
        }

        return state[pattern.Length, value.Length] == 1;
    }
}
