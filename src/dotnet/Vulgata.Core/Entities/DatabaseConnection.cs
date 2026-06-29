namespace Vulgata.Core.Entities;

public sealed class DatabaseConnection
{
    private DatabaseConnection()
    {
    }

    private DatabaseConnection(
        Guid repositoryId,
        string encryptedConnectionString,
        DatabaseType databaseType,
        string? encryptedUsername,
        string? encryptedPassword,
        DateTimeOffset now)
    {
        if (repositoryId == Guid.Empty)
        {
            throw new ArgumentException("仓库标识不能为空。", nameof(repositoryId));
        }

        Id = Guid.NewGuid();
        RepositoryId = repositoryId;
        CreatedAt = now;
        UpdatedAt = now;

        UpdateEncryptedDetails(encryptedConnectionString, databaseType, encryptedUsername, encryptedPassword, now);
    }

    public Guid Id { get; private set; }

    public Guid RepositoryId { get; private set; }

    public string EncryptedConnectionString { get; private set; } = string.Empty;

    public DatabaseType DatabaseType { get; private set; }

    public string? EncryptedUsername { get; private set; }

    public string? EncryptedPassword { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Repository Repository { get; private set; } = null!;

    public static DatabaseConnection Create(
        Guid repositoryId,
        string encryptedConnectionString,
        DatabaseType databaseType,
        string? encryptedUsername,
        string? encryptedPassword,
        DateTimeOffset now) =>
        new(repositoryId, encryptedConnectionString, databaseType, encryptedUsername, encryptedPassword, now);

    public void UpdateEncryptedDetails(
        string encryptedConnectionString,
        DatabaseType databaseType,
        string? encryptedUsername,
        string? encryptedPassword,
        DateTimeOffset now)
    {
        EncryptedConnectionString = NormalizeRequired(encryptedConnectionString);
        DatabaseType = databaseType;
        EncryptedUsername = NormalizeOptional(encryptedUsername);
        EncryptedPassword = NormalizeOptional(encryptedPassword);
        UpdatedAt = now;
    }

    private static string NormalizeRequired(string value) => (value ?? string.Empty).Trim();

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
