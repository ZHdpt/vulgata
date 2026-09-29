namespace Vulgata.Web.Data;

public enum OctoberWebMode
{
    Ordinary,
    AuthorizationOnly,
    SelectedData
}

public sealed record OctoberWebStartupSelection(
    OctoberWebMode Mode,
    string? TargetId,
    Guid? BatchId,
    string? ActivationGrant);

public static class OctoberWebStartupSelector
{
    public static OctoberWebStartupSelection Resolve(IConfiguration configuration)
    {
        throw new NotImplementedException();
    }
}

public enum OctoberCertificateIdentityStatus
{
    Authenticated,
    Unauthenticated,
    AuthorityUnavailable,
    Cancelled
}

public sealed record OctoberCertificateIdentityResult(
    OctoberCertificateIdentityStatus Status,
    string? Sha256Fingerprint);

public interface IOctoberAuthenticatedCertificateSource
{
    Task<OctoberCertificateIdentityResult> ReadAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken);
}

public sealed record OctoberGeneratorCertificateAllowlist(
    IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedFingerprintsByTarget);

public enum OctoberGeneratorCertificateStatus
{
    Authorized,
    Unauthenticated,
    Forbidden,
    AuthorityUnavailable,
    Cancelled
}

public sealed record OctoberGeneratorCertificateDecision(
    OctoberGeneratorCertificateStatus Status);

public sealed class OctoberGeneratorCertificateVerifier
{
    public OctoberGeneratorCertificateVerifier(
        IOctoberAuthenticatedCertificateSource source,
        OctoberGeneratorCertificateAllowlist allowlist)
    {
    }

    public Task<OctoberGeneratorCertificateDecision> VerifyAsync(
        HttpContext httpContext,
        string targetId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new OctoberGeneratorCertificateDecision(
            OctoberGeneratorCertificateStatus.AuthorityUnavailable));
    }
}
