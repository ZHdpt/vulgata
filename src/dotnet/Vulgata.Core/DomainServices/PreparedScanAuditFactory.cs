using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Sole authority for the stable ten-field Prepared Scan audit projection.
/// Relational ordinals and associations are retained but excluded from identity.
/// </summary>
public sealed class PreparedScanAuditFactory
{
    private PreparedScanAuditFactory()
    {
    }

    public static PreparedScanAuditDraft Create(
        Guid repositoryId,
        Guid scanRunId,
        string relativePath,
        PreparedScanAuditOutcome outcome,
        PreparedScanAuditReason reason,
        string? fileHash,
        long? rawByteCount,
        int? logicalLineCount,
        string? fileCodeUnitIdentityFingerprint,
        int? pairSelectionOrdinal,
        int ordinal = 0,
        Guid? preparedScanManifestEntryId = null)
    {
        if (repositoryId == Guid.Empty)
        {
            throw new ArgumentException("Repository identity cannot be empty.", nameof(repositoryId));
        }

        if (scanRunId == Guid.Empty)
        {
            throw new ArgumentException("Scan Run identity cannot be empty.", nameof(scanRunId));
        }

        string path = AnalysisContractGuard.RequireRelativePath(relativePath);
        if (rawByteCount < 0 || logicalLineCount < 0 || pairSelectionOrdinal < 1 || ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawByteCount),
                "Audit counts and ordinals must be nonnegative and pair ordinals must be positive.");
        }

        ValidateShape(
            outcome,
            fileCodeUnitIdentityFingerprint,
            pairSelectionOrdinal,
            preparedScanManifestEntryId);

        PreparedCanonicalArtifact artifact = PreparedScanIdentityBuilders.Create(
            "vulgata/story-12.2/prepared-scan-audit/1.0",
            [
                new CanonicalField(1, CanonicalEnvelope.Uuid(repositoryId)),
                new CanonicalField(2, CanonicalEnvelope.Uuid(scanRunId)),
                new CanonicalField(3, CanonicalEnvelope.String(path)),
                new CanonicalField(4, CanonicalEnvelope.Enum(outcome.ToString())),
                new CanonicalField(5, CanonicalEnvelope.Enum(reason.ToString())),
                new CanonicalField(6, NullableHash(fileHash)),
                new CanonicalField(7, NullableUInt64(rawByteCount)),
                new CanonicalField(8, NullableUInt64(logicalLineCount)),
                new CanonicalField(9, NullableHash(fileCodeUnitIdentityFingerprint)),
                new CanonicalField(10, NullableUInt64(pairSelectionOrdinal)),
            ]);
        Guid id = PreparedScanIdentityBuilders.CreateDeterministicIdentity(
            "PreparedScanAudit",
            artifact.Fingerprint);
        return new(
            id,
            repositoryId,
            scanRunId,
            ordinal,
            path,
            outcome,
            reason,
            fileHash,
            rawByteCount,
            logicalLineCount,
            fileCodeUnitIdentityFingerprint,
            pairSelectionOrdinal,
            preparedScanManifestEntryId,
            artifact.Fingerprint);
    }

    private static void ValidateShape(
        PreparedScanAuditOutcome outcome,
        string? fileCodeUnitIdentityFingerprint,
        int? pairSelectionOrdinal,
        Guid? preparedScanManifestEntryId)
    {
        bool hasUnit = fileCodeUnitIdentityFingerprint is not null;
        bool hasPairOrdinal = pairSelectionOrdinal is not null;
        bool hasEntry = preparedScanManifestEntryId is not null;
        bool valid = outcome switch
        {
            PreparedScanAuditOutcome.Selected => hasUnit && hasPairOrdinal && hasEntry,
            PreparedScanAuditOutcome.Eligible => hasUnit && !hasPairOrdinal && !hasEntry,
            PreparedScanAuditOutcome.Filtered or PreparedScanAuditOutcome.Skipped or
                PreparedScanAuditOutcome.Warning => !hasUnit && !hasPairOrdinal && !hasEntry,
            _ => false,
        };
        if (!valid || preparedScanManifestEntryId == Guid.Empty)
        {
            throw new ArgumentException("The audit outcome has an invalid canonical nullability shape.");
        }
    }

    private static CanonicalValue NullableHash(string? value)
    {
        if (value is null)
        {
            return CanonicalEnvelope.Null();
        }

        if (value.Length != 64 || value.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("Audit hashes must be lowercase SHA-256 values.", nameof(value));
        }

        return CanonicalEnvelope.Bytes(Convert.FromHexString(value));
    }

    private static CanonicalValue NullableUInt64(long? value) => value is null
        ? CanonicalEnvelope.Null()
        : CanonicalEnvelope.UInt64(checked((ulong)value.Value));
}
