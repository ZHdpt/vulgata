using System.Collections.Immutable;
using Vulgata.Core.DomainServices;

namespace Vulgata.OctoberGenerator;

public sealed record OctoberScanInventoryLimits(
    int MaximumDirectories = 128,
    int MaximumEntries = 4096,
    int MaximumDepth = 32);

public sealed record OctoberScanInventoryEntry(
    Guid RepositoryId,
    string RelativePath,
    PreparedSourceEntryKind Kind,
    string Disposition,
    string Reason,
    int Priority,
    long? ObservedByteLength);

public sealed record OctoberScanInventoryGap(Guid RepositoryId, string RelativeDirectory, string Reason);

/// <summary>Completeness describes bounded inventory traversal, never semantic analysis coverage.</summary>
public sealed record OctoberScanInventoryResult(
    bool IsComplete,
    ImmutableArray<OctoberScanInventoryEntry> Entries,
    ImmutableArray<OctoberScanInventoryGap> Gaps);

/// <summary>Inventories an explicit approved Repository-relative directory using contained listing only.</summary>
public sealed class OctoberScanInventory
{
    private readonly Func<Guid, PreparedSourceListRequest, CancellationToken, Task<PreparedSourceResult>> _listSource;

    public OctoberScanInventory(
        Func<Guid, PreparedSourceListRequest, CancellationToken, Task<PreparedSourceResult>> listSource)
    {
        _listSource = listSource ?? throw new ArgumentNullException(nameof(listSource));
    }

    public async Task<OctoberScanInventoryResult> BuildAsync(Guid repositoryId, string relativeDirectory,
        IReadOnlyList<OctoberScanSourceRule> rules, OctoberScanInventoryLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        OctoberScanInventoryLimits bound = limits ?? new();
        if (repositoryId == Guid.Empty || !SafePath(relativeDirectory, allowRoot: true) || rules is null ||
            rules.Count is 0 or > 1024 || bound.MaximumDirectories is < 1 or > 4096 ||
            bound.MaximumEntries is < 1 or > 100000 || bound.MaximumDepth is < 0 or > 128 ||
            rules.Any(rule => rule is null || rule.RepositoryId == Guid.Empty ||
                !SafePath(rule.RelativePathPrefix, allowRoot: true) ||
                rule.Disposition is not ("selected" or "supporting" or "excluded" or "unsupported") ||
                string.IsNullOrWhiteSpace(rule.Reason) || rule.Reason.Length > 512 || rule.Reason.Any(char.IsControl)) ||
            rules.Select(rule => (rule.RepositoryId, rule.RelativePathPrefix)).Distinct().Count() != rules.Count)
            return new(false, [], [new(repositoryId, SafePath(relativeDirectory, true) ? relativeDirectory : string.Empty, "invalid-inventory-input")]);

        var entries = new Dictionary<string, OctoberScanInventoryEntry>(StringComparer.Ordinal);
        var gaps = new HashSet<OctoberScanInventoryGap>();
        var directories = new SortedSet<string>(StringComparer.Ordinal) { relativeDirectory };
        var depths = new Dictionary<string, int>(StringComparer.Ordinal) { [relativeDirectory] = 0 };
        var visited = new HashSet<string>(StringComparer.Ordinal);
        OctoberScanSourceRule[] applicable = rules.Where(rule => rule.RepositoryId == repositoryId)
            .OrderByDescending(rule => rule.RelativePathPrefix.Length)
            .ThenBy(rule => rule.RelativePathPrefix, StringComparer.Ordinal).ToArray();
        bool stopped = false;
        while (directories.Count > 0)
        {
            string directory = directories.Min!;
            directories.Remove(directory);
            if (cancellationToken.IsCancellationRequested)
            {
                Gap(directory, "cancelled");
                stopped = true;
                break;
            }
            if (visited.Count >= bound.MaximumDirectories)
            {
                Gap(directory, "directory-limit");
                stopped = true;
                break;
            }
            visited.Add(directory);
            PreparedSourceResult page;
            try
            {
                page = await _listSource(repositoryId,
                    new PreparedSourceListRequest(directory, PreparedSourceCallLimits.MaximumListEntries), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Gap(directory, "cancelled");
                stopped = true;
                break;
            }
            catch (Exception)
            {
                Gap(directory, "listing-unavailable");
                continue;
            }
            if (page is null || page.RepositoryId != repositoryId || page.Operation != PreparedSourceOperation.List)
            {
                Gap(directory, "invalid-listing-binding");
                continue;
            }
            if (page.Outcome is PreparedSourceOutcome.Denied or PreparedSourceOutcome.AuthorityUnavailable or PreparedSourceOutcome.ChangedInput)
            {
                // Do not return previously observed source metadata after authority loss
                // or combine directory observations across a changed source boundary.
                entries.Clear();
                gaps.Clear();
                directories.Clear();
                Gap(relativeDirectory, "source-authority-or-identity-lost");
                stopped = true;
                break;
            }
            if (page.Outcome is not (PreparedSourceOutcome.Complete or PreparedSourceOutcome.CompleteEmpty) || page.Continuation is not null)
                Gap(directory, "incomplete-directory");
            if (page.Outcome is not (PreparedSourceOutcome.Complete or PreparedSourceOutcome.CompleteEmpty or PreparedSourceOutcome.Incomplete or PreparedSourceOutcome.Limited))
                continue;

            foreach (PreparedSourceLimitation limitation in page.Limitations.Take(PreparedSourceGrantLimits.Default.MaximumMetadataEntries))
            {
                if (limitation.Location.RepositoryId != repositoryId || !DirectChild(directory, limitation.Location.RelativePath))
                    Gap(directory, "invalid-limitation-binding");
                else Gap(limitation.Location.RelativePath, "source-limitation");
            }
            if (page.Limitations.Count > PreparedSourceGrantLimits.Default.MaximumMetadataEntries ||
                page.Entries.Count > PreparedSourceGrantLimits.Default.MaximumMetadataEntries)
                Gap(directory, "metadata-limit");
            foreach (PreparedSourceEntry entry in page.Entries.Take(PreparedSourceGrantLimits.Default.MaximumMetadataEntries)
                .OrderBy(entry => entry.Location.RelativePath, StringComparer.Ordinal))
            {
                string path = entry.Location.RelativePath;
                if (entry.Location.RepositoryId != repositoryId || !DirectChild(directory, path) ||
                    !Enum.IsDefined(entry.Kind) || entry.ObservedByteLength is < 0)
                {
                    Gap(directory, "invalid-entry-binding");
                    continue;
                }
                if (entry.Kind == PreparedSourceEntryKind.Directory)
                {
                    if (entries.ContainsKey(path)) { Gap(directory, "conflicting-entry"); continue; }
                    int depth = depths[directory] + 1;
                    if (depth > bound.MaximumDepth) Gap(path, "depth-limit");
                    else if (!depths.ContainsKey(path))
                    {
                        if (depths.Count >= bound.MaximumDirectories) Gap(path, "directory-limit");
                        else { depths.Add(path, depth); directories.Add(path); }
                    }
                    continue;
                }
                if (depths.ContainsKey(path)) { Gap(directory, "conflicting-entry"); continue; }
                if (entries.Count >= bound.MaximumEntries && !entries.ContainsKey(path))
                {
                    Gap(directory, "entry-limit");
                    stopped = true;
                    break;
                }
                OctoberScanSourceRule? rule = applicable.FirstOrDefault(rule => Matches(rule.RelativePathPrefix, path));
                var item = new OctoberScanInventoryEntry(repositoryId, path, entry.Kind,
                    entry.Kind == PreparedSourceEntryKind.NonTraversableLink ? "unsupported" : rule?.Disposition ?? "excluded",
                    entry.Kind == PreparedSourceEntryKind.NonTraversableLink ? "non-traversable-link" : rule?.Reason ?? "outside-configured-source-rules",
                    rule?.Priority ?? 0, entry.ObservedByteLength);
                if (entries.TryGetValue(path, out OctoberScanInventoryEntry? previous) && previous != item)
                    Gap(directory, "conflicting-entry");
                else entries[path] = item;
                if (entry.Kind == PreparedSourceEntryKind.NonTraversableLink) Gap(path, "non-traversable-link");
            }
            if (stopped) break;
        }
        if (stopped)
            foreach (string pending in directories) Gap(pending, "unvisited-region");
        return new(gaps.Count == 0,
            entries.Values.OrderByDescending(entry => entry.Priority).ThenBy(entry => entry.RelativePath, StringComparer.Ordinal).ToImmutableArray(),
            gaps.OrderBy(gap => gap.RelativeDirectory, StringComparer.Ordinal).ThenBy(gap => gap.Reason, StringComparer.Ordinal).ToImmutableArray());

        void Gap(string path, string reason) => gaps.Add(new(repositoryId, path, reason));
    }

    private static bool Matches(string prefix, string path)
        => prefix.Length == 0 || path == prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal);

    private static bool DirectChild(string directory, string path)
    {
        if (!SafePath(path, allowRoot: false)) return false;
        int separator = path.LastIndexOf('/');
        return (separator < 0 ? string.Empty : path[..separator]) == directory;
    }

    private static bool SafePath(string? path, bool allowRoot)
        => path is not null && path.Length <= 2048 && !path.Contains('\\') && !path.Contains(':') &&
            !path.Contains('*') && !path.Contains('?') && !path.Any(char.IsControl) &&
            (path.Length == 0 ? allowRoot : path.Split('/').All(part =>
                part is not ("" or "." or "..") && part.Trim() == part && !part.EndsWith('.')));
}
