namespace Vulgata.Core.DomainServices;

/// <summary>
/// Represents a reviewed manifest entry for Backup B. Contains exact commit hash,
/// repository-relative file path, and the list of code unit entries to extract.
/// </summary>
public sealed record BackupBManifestEntry
{
    public required string RepositoryAlias { get; init; }
    public required string ExactCommit { get; init; }
    public required string FilePath { get; init; }
    public required IReadOnlyList<BackupBCodeUnitEntry> CodeUnits { get; init; }

    /// <summary>
    /// Validates the manifest entry structure.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RepositoryAlias))
            throw new ArgumentException("Repository alias cannot be empty", nameof(RepositoryAlias));

        if (string.IsNullOrWhiteSpace(ExactCommit))
            throw new ArgumentException("Exact commit cannot be empty", nameof(ExactCommit));

        if (!IsValidGitCommit(ExactCommit))
            throw new ArgumentException("Exact commit must be a valid Git commit hash", nameof(ExactCommit));

        if (string.IsNullOrWhiteSpace(FilePath))
            throw new ArgumentException("File path cannot be empty", nameof(FilePath));

        if (FilePath.StartsWith("/") || FilePath.StartsWith("\\"))
            throw new ArgumentException("File path must be repository-relative (no leading slash)", nameof(FilePath));

        if (FilePath.Contains(".."))
            throw new ArgumentException("File path cannot contain directory traversal", nameof(FilePath));

        if (CodeUnits is null || CodeUnits.Count == 0)
            throw new ArgumentException("At least one code unit entry is required", nameof(CodeUnits));

        foreach (var codeUnit in CodeUnits)
        {
            codeUnit.Validate();
        }
    }

    private static bool IsValidGitCommit(string commit)
    {
        if (string.IsNullOrWhiteSpace(commit))
            return false;

        // Git commits are 40-character hexadecimal strings
        if (commit.Length != 40)
            return false;

        return commit.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }
}

/// <summary>
/// Represents a code unit entry within a manifest entry. Contains symbol information,
/// line range, and language.
/// </summary>
public sealed record BackupBCodeUnitEntry
{
    public required string SymbolName { get; init; }
    public required string UnitType { get; init; }
    public required string Language { get; init; }
    public required int LineStart { get; init; }
    public required int LineEnd { get; init; }

    /// <summary>
    /// Validates the code unit entry structure.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SymbolName))
            throw new ArgumentException("Symbol name cannot be empty", nameof(SymbolName));

        if (SymbolName.Length > 512)
            throw new ArgumentException("Symbol name exceeds maximum length of 512", nameof(SymbolName));

        if (string.IsNullOrWhiteSpace(UnitType))
            throw new ArgumentException("Unit type cannot be empty", nameof(UnitType));

        if (string.IsNullOrWhiteSpace(Language))
            throw new ArgumentException("Language cannot be empty", nameof(Language));

        if (LineStart < 1)
            throw new ArgumentException("Line start must be at least 1", nameof(LineStart));

        if (LineEnd < LineStart)
            throw new ArgumentException("Line end must be >= line start", nameof(LineEnd));
    }
}

/// <summary>
/// Represents the complete Backup B manifest containing all reviewed source entries
/// for the demonstration run.
/// </summary>
public sealed record BackupBManifest
{
    public required string ManifestId { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<BackupBManifestEntry> Entries { get; init; }

    /// <summary>
    /// Validates the manifest structure.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ManifestId))
            throw new ArgumentException("Manifest ID cannot be empty", nameof(ManifestId));

        if (string.IsNullOrWhiteSpace(Description))
            throw new ArgumentException("Description cannot be empty", nameof(Description));

        if (Entries is null || Entries.Count == 0)
            throw new ArgumentException("At least one manifest entry is required", nameof(Entries));

        foreach (var entry in Entries)
        {
            entry.Validate();
        }
    }
}
