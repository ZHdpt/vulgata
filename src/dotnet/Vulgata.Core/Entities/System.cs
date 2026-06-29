namespace Vulgata.Core.Entities;

public sealed class System
{
    private readonly List<Repository> _repositories = [];
    private readonly List<SystemOwnerAssignment> _ownerAssignments = [];

    private System()
    {
    }

    public System(string name, string? description, string? context, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        CreatedAt = now;
        UpdatedAt = now;
        UpdateDetails(name, description, context, now);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Context { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<Repository> Repositories => _repositories;

    public IReadOnlyCollection<SystemOwnerAssignment> OwnerAssignments => _ownerAssignments;

    public void UpdateDetails(string name, string? description, string? context, DateTimeOffset now)
    {
        string trimmedName = (name ?? string.Empty).Trim();

        Name = trimmedName;
        NormalizedName = NormalizeName(trimmedName);
        Description = NormalizeOptional(description);
        Context = NormalizeOptional(context);
        UpdatedAt = now;
    }

    public Repository AddRepository(
        string name,
        string gitUrl,
        string? description,
        string? context,
        DateTimeOffset now)
    {
        Repository repository = Repository.Create(Id, name, gitUrl, description, context, now);
        _repositories.Add(repository);
        UpdatedAt = now;
        return repository;
    }

    public bool RemoveRepository(Guid repositoryId, DateTimeOffset now)
    {
        Repository? repository = _repositories.FirstOrDefault(r => r.Id == repositoryId);
        if (repository is null)
        {
            return false;
        }

        _repositories.Remove(repository);
        UpdatedAt = now;
        return true;
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
