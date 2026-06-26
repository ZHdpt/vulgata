using Microsoft.AspNetCore.Identity;

namespace Vulgata.Web.Data;

public class BcryptPasswordHasher<TUser> : IPasswordHasher<TUser> where TUser : class
{
    public string HashPassword(TUser user, string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, 12, BCrypt.Net.HashType.SHA384);
    }

    public PasswordVerificationResult VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrEmpty(providedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        return BCrypt.Net.BCrypt.EnhancedVerify(providedPassword, hashedPassword)
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.Failed;
    }
}