using BCrypt.Net;
using Microsoft.AspNetCore.Identity;

namespace Vulgata.Web.Data;

public class BcryptPasswordHasher<TUser> : IPasswordHasher<TUser> where TUser : class
{
    private static readonly string[] _supportedPrefixes = ["$2a$", "$2b$", "$2y$"];

    public string HashPassword(TUser user, string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, 12, HashType.SHA384);
    }

    public PasswordVerificationResult VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword)
            || string.IsNullOrEmpty(providedPassword)
            || !HasSupportedBcryptPrefix(hashedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        try
        {
            return BCrypt.Net.BCrypt.EnhancedVerify(providedPassword, hashedPassword)
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.Failed;
        }
        catch (SaltParseException)
        {
            return PasswordVerificationResult.Failed;
        }
        catch (HashInformationException)
        {
            return PasswordVerificationResult.Failed;
        }
    }

    private static bool HasSupportedBcryptPrefix(string hashedPassword)
    {
        foreach (string prefix in _supportedPrefixes)
        {
            if (hashedPassword.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
