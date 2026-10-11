using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Tests.October.Integration;

/// <summary>Fixed synthetic qualification coverage, not exclusive method identity or production root discovery.</summary>
internal static class OctoberRegistrationRootEvidence
{
    internal static bool CoversRegistration(ImmutableArray<SourceAnchor> anchors, PreparedOrigin expectedOrigin,
        int lineCount, int declarationLine, int returnLine)
    {
        if (lineCount < 1 || declarationLine < 1 || returnLine <= declarationLine || returnLine > lineCount
            || anchors.IsDefaultOrEmpty || anchors.Any(anchor => anchor is null || anchor.Origin != expectedOrigin
                || anchor.StartLine < 1 || anchor.EndLine < anchor.StartLine || anchor.EndLine > lineCount))
            throw new InvalidOperationException("Registration evidence has invalid source provenance or line bounds.");
        // Broader class/file anchors can include other behavior; this proves registration coverage only.
        return anchors.Any(anchor => anchor.StartLine <= declarationLine && anchor.EndLine >= declarationLine)
            && anchors.Any(anchor => anchor.StartLine <= returnLine && anchor.EndLine >= returnLine);
    }

    internal static Guid RequireUnique(IEnumerable<Guid> eligible)
    {
        Guid[] ids = eligible.ToArray();
        if (ids.Length != 1 || ids[0] == Guid.Empty)
            throw new InvalidOperationException("The offered registration root is absent or ambiguous.");
        return ids[0];
    }
}
