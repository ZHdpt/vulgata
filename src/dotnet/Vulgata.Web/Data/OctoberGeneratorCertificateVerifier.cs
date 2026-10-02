using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Vulgata.Web.Data;

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

internal sealed record OctoberWebTargetAllowlist(IReadOnlyDictionary<string, string> CanonicalDatabaseIdentities);

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
    private readonly IOctoberAuthenticatedCertificateSource _source;
    private readonly OctoberGeneratorCertificateAllowlist _allowlist;

    public OctoberGeneratorCertificateVerifier(
        IOctoberAuthenticatedCertificateSource source,
        OctoberGeneratorCertificateAllowlist allowlist)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _allowlist = allowlist ?? throw new ArgumentNullException(nameof(allowlist));
    }

    public async Task<OctoberGeneratorCertificateDecision> VerifyAsync(
        HttpContext httpContext,
        string targetId,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new OctoberGeneratorCertificateDecision(OctoberGeneratorCertificateStatus.Cancelled);
        }

        OctoberCertificateIdentityResult identity;
        try
        {
            identity = await _source.ReadAsync(httpContext, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new OctoberGeneratorCertificateDecision(OctoberGeneratorCertificateStatus.Cancelled);
        }
        catch (Exception)
        {
            return new OctoberGeneratorCertificateDecision(OctoberGeneratorCertificateStatus.AuthorityUnavailable);
        }

        OctoberGeneratorCertificateStatus status = identity.Status switch
        {
            OctoberCertificateIdentityStatus.Unauthenticated => OctoberGeneratorCertificateStatus.Unauthenticated,
            OctoberCertificateIdentityStatus.AuthorityUnavailable => OctoberGeneratorCertificateStatus.AuthorityUnavailable,
            OctoberCertificateIdentityStatus.Cancelled => OctoberGeneratorCertificateStatus.Cancelled,
            OctoberCertificateIdentityStatus.Authenticated => IsAllowed(identity.Sha256Fingerprint, targetId)
                ? OctoberGeneratorCertificateStatus.Authorized
                : OctoberGeneratorCertificateStatus.Forbidden,
            _ => OctoberGeneratorCertificateStatus.AuthorityUnavailable
        };

        return new OctoberGeneratorCertificateDecision(status);
    }

    private bool IsAllowed(string? fingerprint, string targetId)
    {
        if (fingerprint is null || fingerprint.Length != 64 ||
            !fingerprint.All(Uri.IsHexDigit))
        {
            return false;
        }

        string normalizedFingerprint = fingerprint.ToLowerInvariant();
        foreach ((string allowedTarget, IReadOnlySet<string> allowedFingerprints) in
                 _allowlist.AllowedFingerprintsByTarget)
        {
            if (string.Equals(allowedTarget, targetId, StringComparison.Ordinal))
            {
                return allowedFingerprints.Any(allowedFingerprint =>
                    string.Equals(allowedFingerprint, normalizedFingerprint, StringComparison.Ordinal));
            }
        }

        return false;
    }
}

public sealed class OctoberConnectionCertificateSource : IOctoberAuthenticatedCertificateSource
{
    public async Task<OctoberCertificateIdentityResult> ReadAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.IsHttps)
        {
            return new OctoberCertificateIdentityResult(OctoberCertificateIdentityStatus.Unauthenticated, null);
        }

        try
        {
            X509Certificate2? certificate =
                await httpContext.Connection.GetClientCertificateAsync(cancellationToken);
            if (certificate is null)
            {
                return new OctoberCertificateIdentityResult(OctoberCertificateIdentityStatus.Unauthenticated, null);
            }

            using X509Chain chain = new();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            if (!chain.Build(certificate))
            {
                return new OctoberCertificateIdentityResult(OctoberCertificateIdentityStatus.Unauthenticated, null);
            }

            string fingerprint = Convert.ToHexStringLower(SHA256.HashData(certificate.RawData));
            return new OctoberCertificateIdentityResult(
                OctoberCertificateIdentityStatus.Authenticated, fingerprint);
        }
        catch (OperationCanceledException)
        {
            return new OctoberCertificateIdentityResult(OctoberCertificateIdentityStatus.Cancelled, null);
        }
        catch (Exception)
        {
            return new OctoberCertificateIdentityResult(OctoberCertificateIdentityStatus.AuthorityUnavailable, null);
        }
    }
}

internal static class OctoberGeneratorCertificateConfiguration
{
    public static OctoberWebTargetAllowlist ReadTargets(IConfiguration configuration)
    {
        Dictionary<string, string> identities = new(StringComparer.Ordinal);
        foreach (IConfigurationSection target in configuration.GetSection("October:Web:AllowedTargets").GetChildren())
        {
            string? targetId = target["TargetId"];
            string? identity = target["CanonicalDatabaseIdentity"];
            if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(identity) ||
                !identities.TryAdd(targetId, identity))
            {
                throw new InvalidOperationException("Invalid October Web target allowlist.");
            }
        }

        if (identities.Count == 0)
        {
            throw new InvalidOperationException("Invalid October Web target allowlist.");
        }

        return new OctoberWebTargetAllowlist(identities);
    }

    public static OctoberGeneratorCertificateAllowlist ReadAllowlist(IConfiguration configuration)
    {
        HashSet<string> targetIds = new(StringComparer.Ordinal);
        foreach (IConfigurationSection target in configuration.GetSection("October:Web:AllowedTargets").GetChildren())
        {
            string? targetId = target["TargetId"];
            if (string.IsNullOrWhiteSpace(targetId) ||
                string.IsNullOrWhiteSpace(target["CanonicalDatabaseIdentity"]) ||
                !targetIds.Add(targetId))
            {
                throw new InvalidOperationException("Invalid October Web target allowlist.");
            }
        }

        Dictionary<string, HashSet<string>> byTarget = new(StringComparer.Ordinal);
        HashSet<string> fingerprints = new(StringComparer.Ordinal);
        foreach (IConfigurationSection certificate in
                 configuration.GetSection("October:Web:GeneratorCertificates").GetChildren())
        {
            string? fingerprint = certificate["Sha256Fingerprint"]?.ToLowerInvariant();
            string[] scopedTargets = certificate.GetSection("TargetIds").GetChildren()
                .Select(target => target.Value ?? string.Empty)
                .ToArray();
            if (fingerprint is null || fingerprint.Length != 64 ||
                !fingerprint.All(Uri.IsHexDigit) ||
                !fingerprints.Add(fingerprint) ||
                scopedTargets.Length == 0 ||
                scopedTargets.Any(target => !targetIds.Contains(target)) ||
                scopedTargets.Distinct(StringComparer.Ordinal).Count() != scopedTargets.Length)
            {
                throw new InvalidOperationException("Invalid October Web certificate allowlist.");
            }

            foreach (string targetId in scopedTargets)
            {
                if (!byTarget.TryGetValue(targetId, out HashSet<string>? allowed))
                {
                    allowed = new HashSet<string>(StringComparer.Ordinal);
                    byTarget.Add(targetId, allowed);
                }

                allowed.Add(fingerprint);
            }
        }

        if (targetIds.Count == 0 || byTarget.Count == 0)
        {
            throw new InvalidOperationException("Invalid October Web certificate allowlist.");
        }

        return new OctoberGeneratorCertificateAllowlist(
            byTarget.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlySet<string>)pair.Value,
                StringComparer.Ordinal));
    }
}
