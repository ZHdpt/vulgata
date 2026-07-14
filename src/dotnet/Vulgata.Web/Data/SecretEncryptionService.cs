using Microsoft.AspNetCore.DataProtection;
using Vulgata.Core.DomainServices;

namespace Vulgata.Web.Data;

/// <summary>
/// Single <see cref="ISecretEncryptionService"/> implementation backed by ASP.NET Core Data Protection.
/// The protector is created per call from the supplied <c>purpose</c> string.
/// <see cref="IDataProtectionProvider"/> is a singleton; <see cref="IDataProtector"/> instances are
/// thread-safe and cheap to construct (key derivation is cached internally by the framework).
/// </summary>
public sealed class SecretEncryptionService(IDataProtectionProvider dataProtectionProvider) : ISecretEncryptionService
{
    public Task<string> EncryptAsync(string purpose, string plain, CancellationToken cancellationToken = default)
    {
        string normalizedValue = (plain ?? string.Empty).Trim();
        var protector = dataProtectionProvider.CreateProtector(purpose);
        return Task.FromResult(protector.Protect(normalizedValue));
    }

    public Task<string> DecryptAsync(string purpose, string cipher, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cipher))
        {
            return Task.FromResult(string.Empty);
        }

        var protector = dataProtectionProvider.CreateProtector(purpose);
        return Task.FromResult(protector.Unprotect(cipher));
    }
}
