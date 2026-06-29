using Microsoft.AspNetCore.DataProtection;

namespace Vulgata.Web.Data;

public interface IApiKeyEncryptionService
{
    Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken = default);

    Task<string> DecryptAsync(string encryptedText, CancellationToken cancellationToken = default);
}

public sealed class ApiKeyEncryptionService(IDataProtectionProvider dataProtectionProvider) : IApiKeyEncryptionService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Vulgata.LlmProviders.ApiKey");

    public Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken = default)
    {
        string normalizedValue = (plainText ?? string.Empty).Trim();
        return Task.FromResult(_protector.Protect(normalizedValue));
    }

    public Task<string> DecryptAsync(string encryptedText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(encryptedText))
        {
            return Task.FromResult(string.Empty);
        }

        return Task.FromResult(_protector.Unprotect(encryptedText));
    }
}
