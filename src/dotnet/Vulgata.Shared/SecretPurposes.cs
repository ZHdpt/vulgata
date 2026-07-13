namespace Vulgata.Shared;

/// <summary>
/// Canonical purpose strings for <c>ISecretEncryptionService</c>.
/// These map 1:1 to <c>IDataProtectionProvider.CreateProtector(purpose)</c> calls.
/// Never derive purposes from user input — always pass a constant from this class.
/// </summary>
/// <remarks>
/// <para>
/// Purpose strings must remain byte-for-byte stable across releases. Rotating a
/// purpose string invalidates all existing ciphertext; rotation requires a
/// migration step (read old purpose, write new purpose) before the constant changes.
/// </para>
/// <para>
/// The two constants below match the strings historically embedded in
/// <c>ApiKeyEncryptionService</c> and <c>DatabaseConnectionEncryptionService</c>.
/// Existing ciphertext decrypts without migration.
/// </para>
/// </remarks>
public static class SecretPurposes
{
    /// <summary>LLM provider API keys stored on <c>LlmProvider.EncryptedApiKey</c>.</summary>
    public const string LlmApiKey = "Vulgata.LlmProviders.ApiKey";

    /// <summary>Repository database connection credentials (connection string, username, password).</summary>
    public const string DatabaseConnection = "Vulgata.Repositories.DatabaseConnection";
}
