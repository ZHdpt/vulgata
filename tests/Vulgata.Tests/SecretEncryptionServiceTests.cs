using Vulgata.Tests.TestInfrastructure;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

/// <summary>
/// Unit tests for the unified <see cref="ISecretEncryptionService"/> seam.
/// Pins the three behaviors the previous two-service duplication offered:
/// round-trip per purpose, isolation between purposes (wrong-purpose decrypt fails),
/// and empty-cipher handling.
/// </summary>
public sealed class SecretEncryptionServiceTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(SecretPurposes.LlmApiKey, "sk-deepseek-abc-123")]
    [InlineData(SecretPurposes.DatabaseConnection, "Host=localhost;Port=5432;Database=vulgata")]
    [InlineData(SecretPurposes.LlmApiKey, "")]
    [InlineData(SecretPurposes.DatabaseConnection, "   ")]
    public async Task EncryptThenDecrypt_RoundTripsPlainText(string purpose, string plain)
    {
        var service = BuildService();

        string cipher = await service.EncryptAsync(purpose, plain);
        string decrypted = await service.DecryptAsync(purpose, cipher);

        Assert.Equal((plain ?? string.Empty).Trim(), decrypted);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Decrypt_WithWrongPurpose_ThrowsCryptographicException()
    {
        var service = BuildService();

        string cipher = await service.EncryptAsync(SecretPurposes.LlmApiKey, "sk-secret");
        _ = await Assert.ThrowsAsync<CryptographicException>(() =>
            service.DecryptAsync(SecretPurposes.DatabaseConnection, cipher));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Decrypt_WithEmptyCipher_ReturnsEmptyString()
    {
        var service = BuildService();

        Assert.Equal(string.Empty, await service.DecryptAsync(SecretPurposes.LlmApiKey, string.Empty));
        Assert.Equal(string.Empty, await service.DecryptAsync(SecretPurposes.LlmApiKey, "   "));
        Assert.Equal(string.Empty, await service.DecryptAsync(SecretPurposes.LlmApiKey, null!));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Encrypt_TrimWhitespaceBeforeEncryption()
    {
        var service = BuildService();

        string cipher = await service.EncryptAsync(SecretPurposes.LlmApiKey, "  sk-secret  ");
        string decrypted = await service.DecryptAsync(SecretPurposes.LlmApiKey, cipher);

        Assert.Equal("sk-secret", decrypted);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Encrypt_DifferentInvocationsProduceDifferentCiphertext()
    {
        var service = BuildService();

        string first = await service.EncryptAsync(SecretPurposes.DatabaseConnection, "same-plain");
        string second = await service.EncryptAsync(SecretPurposes.DatabaseConnection, "same-plain");

        Assert.NotEqual(first, second);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        var service = BuildService();

        string cipher = await service.EncryptAsync(SecretPurposes.LlmApiKey, "sk-secret");
        // Flip a character near the end to tamper.
        char[] chars = cipher.ToCharArray();
        chars[chars.Length - 1] = chars[chars.Length - 1] == 'a' ? 'b' : 'a';
        string tampered = new string(chars);

        _ = await Assert.ThrowsAsync<CryptographicException>(() =>
            service.DecryptAsync(SecretPurposes.LlmApiKey, tampered));
    }

    private static ISecretEncryptionService BuildService()
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        var provider = services.BuildServiceProvider();
        return new SecretEncryptionService(provider.GetRequiredService<IDataProtectionProvider>());
    }
}
