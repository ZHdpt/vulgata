using System.Security.Cryptography;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Creates the canonical identity artifact for one Repository-specific
/// Prepared Scan initiation execution.
/// </summary>
public static class PreparedScanInitiationIdentityBuilder
{
    private const string _domain =
        "vulgata/story-12.5/prepared-scan-initiation-execution/1.0";

    public static PreparedCanonicalArtifact CreateExecution(
        Guid authorizationUserId,
        string effectiveAuthorizationMode,
        Guid systemId,
        Guid repositoryId,
        string requestedRef,
        string profiledRevision,
        string promptIdentity,
        string documentSchemaIdentity,
        string runtimeIdentityJson,
        string runtimeIdentityHash)
    {
        ThrowIfEmpty(authorizationUserId, nameof(authorizationUserId));
        ThrowIfEmpty(systemId, nameof(systemId));
        ThrowIfEmpty(repositoryId, nameof(repositoryId));

        string mode = RequireAuthorizationMode(effectiveAuthorizationMode);
        string gitRef = RequireFullRef(requestedRef);
        string revision = RequireRevision(profiledRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentSchemaIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeIdentityJson);
        string runtimeHash = AnalysisContractGuard.RequireSha256(
            runtimeIdentityHash,
            nameof(runtimeIdentityHash));

        byte[] envelope = CanonicalEnvelope.Create(
            _domain,
            [
                new CanonicalField(1, CanonicalEnvelope.Uuid(authorizationUserId)),
                new CanonicalField(2, CanonicalEnvelope.Enum(mode)),
                new CanonicalField(3, CanonicalEnvelope.Uuid(systemId)),
                new CanonicalField(4, CanonicalEnvelope.Uuid(repositoryId)),
                new CanonicalField(5, CanonicalEnvelope.String(gitRef)),
                new CanonicalField(6, CanonicalEnvelope.String(revision)),
                new CanonicalField(7, CanonicalEnvelope.String(promptIdentity)),
                new CanonicalField(8, CanonicalEnvelope.String(documentSchemaIdentity)),
                new CanonicalField(9, CanonicalEnvelope.String(runtimeIdentityJson)),
                new CanonicalField(10, CanonicalEnvelope.Bytes(Convert.FromHexString(runtimeHash))),
            ]);

        return new PreparedCanonicalArtifact(
            envelope,
            Convert.ToHexStringLower(SHA256.HashData(envelope)));
    }

    private static string RequireAuthorizationMode(string value) => value switch
    {
        "Administrator" => value,
        "SystemOwner" => value,
        _ => throw new ArgumentException(
            "The effective authorization mode must be Administrator or SystemOwner.",
            nameof(value)),
    };

    private static string RequireFullRef(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if ((!value.StartsWith("refs/heads/", StringComparison.Ordinal) &&
                !value.StartsWith("refs/tags/", StringComparison.Ordinal)) ||
            value.Length > 1_024 ||
            value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)) ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.EndsWith("/", StringComparison.Ordinal) ||
            value.EndsWith(".lock", StringComparison.Ordinal))
        {
            throw new ArgumentException("A safe full Git ref is required.", nameof(value));
        }

        return value;
    }

    private static string RequireRevision(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 40 ||
            value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "An exact lowercase 40-hex Profiled Revision is required.",
                nameof(value));
        }

        return value;
    }

    private static void ThrowIfEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A non-empty identity is required.", parameterName);
        }
    }
}
