namespace Vulgata.Core.DomainServices;

/// <summary>
/// Encrypts and decrypts secrets using ASP.NET Core Data Protection with purpose strings.
/// The purpose parameter scopes key derivation so ciphertext encrypted under one purpose
/// cannot be decrypted under another. Implementations must use
/// <see cref="Microsoft.AspNetCore.DataProtection.IDataProtectionProvider.CreateProtector(string)"/>
/// per call with the supplied purpose string.
/// </summary>
public interface ISecretEncryptionService
{
    /// <summary>
    /// Encrypts <paramref name="plain"/> under the supplied <paramref name="purpose"/> string.
    /// </summary>
    Task<string> EncryptAsync(string purpose, string plain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts <paramref name="cipher"/> under the supplied <paramref name="purpose"/> string.
    /// Returns empty string when <paramref name="cipher"/> is null or whitespace.
    /// </summary>
    Task<string> DecryptAsync(string purpose, string cipher, CancellationToken cancellationToken = default);
}
