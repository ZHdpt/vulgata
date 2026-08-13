namespace Vulgata.Core.DomainServices;

/// <summary>
/// Result of a Backup B provisioning operation.
/// </summary>
public sealed record BackupBProvisioningResult
{
    /// <summary>
    /// The created ScanRun identifiers, keyed by repository alias.
    /// </summary>
    public required IReadOnlyDictionary<string, Guid> ScanRunIds { get; init; }

    /// <summary>
    /// The created CodeUnit identifiers, keyed by (repository alias, file path, symbol name).
    /// </summary>
    public required IReadOnlyDictionary<(string RepositoryAlias, string FilePath, string SymbolName), Guid> CodeUnitIds { get; init; }

    /// <summary>
    /// The approved PreScanResult identifiers, keyed by repository alias.
    /// </summary>
    public required IReadOnlyDictionary<string, Guid> PreScanResultIds { get; init; }

    /// <summary>
    /// The system identifier used for all repositories.
    /// </summary>
    public required Guid SystemId { get; init; }

    /// <summary>
    /// The repository identifiers, keyed by alias.
    /// </summary>
    public required IReadOnlyDictionary<string, Guid> RepositoryIds { get; init; }
}

/// <summary>
/// Provisioning input for Backup B.
/// </summary>
public sealed record BackupBProvisioningRequest
{
    public required string SystemName { get; init; }
    public required string SystemDescription { get; init; }
    public required IReadOnlyDictionary<string, BackupBRepositoryInput> Repositories { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SystemName))
            throw new ArgumentException("System name cannot be empty", nameof(SystemName));

        if (Repositories is null || Repositories.Count == 0)
            throw new ArgumentException("At least one repository is required", nameof(Repositories));

        foreach (var (alias, input) in Repositories)
        {
            if (string.IsNullOrWhiteSpace(alias))
                throw new ArgumentException("Repository alias cannot be empty");

            input.Validate(alias);
        }
    }
}

/// <summary>
/// Repository input for Backup B provisioning.
/// </summary>
public sealed record BackupBRepositoryInput
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string CloneUrl { get; init; }

    /// <summary>
    /// Local path to a git working directory that contains the exact commit.
    /// Used by BackupBGitSourceReader to run 'git show' against.
    /// </summary>
    public required string LocalGitPath { get; init; }
    public required string ExpectedCommit { get; init; }
    public required IReadOnlyList<BackupBManifestEntry> ManifestEntries { get; init; }

    public void Validate(string alias)
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException($"Repository name cannot be empty for alias '{alias}'", nameof(Name));

        if (string.IsNullOrWhiteSpace(CloneUrl))
            throw new ArgumentException($"Clone URL cannot be empty for alias '{alias}'", nameof(CloneUrl));

        if (string.IsNullOrWhiteSpace(LocalGitPath))
            throw new ArgumentException($"Local git path cannot be empty for alias '{alias}'", nameof(LocalGitPath));

        if (string.IsNullOrWhiteSpace(ExpectedCommit))
            throw new ArgumentException($"Expected commit cannot be empty for alias '{alias}'", nameof(ExpectedCommit));

        if (ManifestEntries is null || ManifestEntries.Count == 0)
            throw new ArgumentException($"At least one manifest entry is required for alias '{alias}'", nameof(ManifestEntries));

        foreach (var entry in ManifestEntries)
        {
            entry.Validate();
        }
    }
}
