namespace Vulgata.Core.Entities;

public sealed class SourceSnapshot
{
    private SourceSnapshot()
    {
    }

    private SourceSnapshot(
        Guid repositoryId,
        string profiledRevision,
        string gitObjectFormat,
        string manifestSha256,
        long entryCount,
        long totalBlobBytes,
        string internalStorageKey,
        DateTimeOffset completedAt)
    {
        if (repositoryId == Guid.Empty)
        {
            throw new ArgumentException("A Repository identifier is required.", nameof(repositoryId));
        }

        string requiredObjectFormat = RequireGitObjectFormat(gitObjectFormat);
        string requiredRevision = RequireProfiledRevision(profiledRevision, requiredObjectFormat);
        string requiredManifestSha256 = RequireLowercaseHex(
            manifestSha256,
            64,
            nameof(manifestSha256),
            "A lowercase SHA-256 manifest digest is required.");
        if (entryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entryCount), "Entry count cannot be negative.");
        }

        if (totalBlobBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalBlobBytes),
                "Total blob bytes cannot be negative.");
        }

        string requiredStorageKey = RequireInternalStorageKey(internalStorageKey);
        if (completedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Completion time must use UTC.", nameof(completedAt));
        }

        Id = Guid.NewGuid();
        RepositoryId = repositoryId;
        ProfiledRevision = requiredRevision;
        GitObjectFormat = requiredObjectFormat;
        ManifestSchemaIdentity = RetainedSourceManifestSchemaIdentity;
        ManifestSha256 = requiredManifestSha256;
        EntryCount = entryCount;
        TotalBlobBytes = totalBlobBytes;
        InternalStorageKey = requiredStorageKey;
        CompletedAt = completedAt;
    }

    public const string RetainedSourceManifestSchemaIdentity = "retained-source-manifest/1.0";

    public Guid Id { get; private set; }

    public Guid RepositoryId { get; private set; }

    public string ProfiledRevision { get; private set; } = string.Empty;

    public string GitObjectFormat { get; private set; } = string.Empty;

    public string ManifestSchemaIdentity { get; private set; } = string.Empty;

    public string ManifestSha256 { get; private set; } = string.Empty;

    public long EntryCount { get; private set; }

    public long TotalBlobBytes { get; private set; }

    public string InternalStorageKey { get; private set; } = string.Empty;

    public DateTimeOffset CompletedAt { get; private set; }

    public Repository? Repository { get; private set; }

    public static SourceSnapshot Create(
        Guid repositoryId,
        string profiledRevision,
        string gitObjectFormat,
        string manifestSha256,
        long entryCount,
        long totalBlobBytes,
        string internalStorageKey,
        DateTimeOffset completedAt) =>
        new(
            repositoryId,
            profiledRevision,
            gitObjectFormat,
            manifestSha256,
            entryCount,
            totalBlobBytes,
            internalStorageKey,
            completedAt);

    private static string RequireGitObjectFormat(string? value)
    {
        if (value is not ("sha1" or "sha256"))
        {
            throw new ArgumentException(
                "Git object format must be sha1 or sha256.",
                nameof(value));
        }

        return value;
    }

    private static string RequireProfiledRevision(string? value, string gitObjectFormat)
    {
        int expectedLength = gitObjectFormat == "sha1" ? 40 : 64;
        return RequireLowercaseHex(
            value,
            expectedLength,
            nameof(value),
            "A full lowercase Git commit matching the object format is required.");
    }

    private static string RequireLowercaseHex(
        string? value,
        int expectedLength,
        string parameterName,
        string message)
    {
        if (value is null ||
            value.Length != expectedLength ||
            value.Any(static character =>
                !char.IsAsciiHexDigit(character) || char.IsAsciiLetterUpper(character)))
        {
            throw new ArgumentException(message, parameterName);
        }

        return value;
    }

    private static string RequireInternalStorageKey(string? value)
    {
        const int maximumLength = 2_048;
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > maximumLength ||
            value[0] == '/' ||
            value.Contains('\\', StringComparison.Ordinal) ||
            value.Contains(':', StringComparison.Ordinal) ||
            value.Any(char.IsControl) ||
            value.Split('/').Any(static segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException(
                "A bounded contained relative storage key is required.",
                nameof(value));
        }

        return value;
    }
}
