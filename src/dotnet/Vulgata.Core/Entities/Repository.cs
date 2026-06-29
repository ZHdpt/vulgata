namespace Vulgata.Core.Entities;

public sealed class Repository
{
    private Repository()
    {
    }

    private Repository(
        Guid systemId,
        string name,
        string gitUrl,
        string? description,
        string? context,
        DateTimeOffset now)
    {
        if (systemId == Guid.Empty)
        {
            throw new ArgumentException("系统标识不能为空。", nameof(systemId));
        }

        Id = Guid.NewGuid();
        SystemId = systemId;
        CreatedAt = now;
        UpdatedAt = now;

        UpdateDetails(name, gitUrl, description, context, now);
    }

    public Guid Id { get; private set; }

    public Guid SystemId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string GitUrl { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Context { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public System System { get; private set; } = null!;

    public static Repository Create(
        Guid systemId,
        string name,
        string gitUrl,
        string? description,
        string? context,
        DateTimeOffset now) =>
        new(systemId, name, gitUrl, description, context, now);

    public void UpdateDetails(
        string name,
        string gitUrl,
        string? description,
        string? context,
        DateTimeOffset now)
    {
        string trimmedName = (name ?? string.Empty).Trim();
        string trimmedGitUrl = (gitUrl ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ArgumentException("仓库名称不能为空。", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(trimmedGitUrl))
        {
            throw new ArgumentException("Git 地址不能为空。", nameof(gitUrl));
        }

        Name = trimmedName;
        NormalizedName = NormalizeName(trimmedName);
        GitUrl = trimmedGitUrl;
        Description = NormalizeOptional(description);
        Context = NormalizeOptional(context);
        UpdatedAt = now;
    }

    public static string NormalizeName(string name) => (name ?? string.Empty).Trim().ToUpperInvariant();

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
