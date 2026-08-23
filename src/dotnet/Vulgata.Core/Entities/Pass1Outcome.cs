using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Core.Entities;

public enum Pass1OutcomeStatus
{
    Succeeded,
    CompletedWithWarnings,
    FailedPass1,
}

public sealed class Pass1Outcome
{
    private const string _fingerprintDomain = "vulgata/story-12.3/pass1-outcome/1.0";

    private Pass1Outcome()
    {
    }

    public Guid PairExecutionId { get; private set; }
    public Pass1OutcomeStatus Status { get; private set; }
    public int DispatchedCount { get; private set; }
    public int CompletedCount { get; private set; }
    public int OrdinaryFailureCount { get; private set; }
    public int OrdinaryFailureBudget { get; private set; }
    public int HighValueSeedCount { get; private set; }
    public int SucceededHighValueSeedCount { get; private set; }
    public string? ReconciliationFingerprint { get; private set; }
    public int ReconciledEdgeCount { get; private set; }
    public string OutcomeFingerprint { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; private set; }

    public static Pass1Outcome Create(
        Guid pairExecutionId,
        Pass1OutcomeStatus status,
        int dispatchedCount,
        int completedCount,
        int ordinaryFailureCount,
        int highValueSeedCount,
        int succeededHighValueSeedCount,
        string? reconciliationFingerprint,
        int reconciledEdgeCount,
        IReadOnlyList<Pass1FailureInventoryEntry> failureInventory,
        DateTimeOffset completedAt)
    {
        if (pairExecutionId == Guid.Empty || !Enum.IsDefined(status) || dispatchedCount < 1 ||
            completedCount < 0 || completedCount > dispatchedCount || ordinaryFailureCount < 0 ||
            ordinaryFailureCount > dispatchedCount ||
            completedCount + ordinaryFailureCount > dispatchedCount ||
            status != Pass1OutcomeStatus.FailedPass1 &&
                completedCount + ordinaryFailureCount != dispatchedCount ||
            highValueSeedCount < 0 || highValueSeedCount > dispatchedCount ||
            succeededHighValueSeedCount < 0 || succeededHighValueSeedCount > highValueSeedCount ||
            reconciledEdgeCount < 0 || completedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The Pass 1 terminal accounting values are invalid.");
        }

        ArgumentNullException.ThrowIfNull(failureInventory);
        int budget = Math.Min(5, dispatchedCount / 10);
        Pass1FailureInventoryEntry[] canonicalInventory = failureInventory
            .OrderBy(value => value.PairSelectionOrdinal)
            .ThenBy(value => value.RepositoryId)
            .ThenBy(value => value.FileCodeUnitId)
            .ToArray();
        if (canonicalInventory.Length != ordinaryFailureCount ||
            canonicalInventory.Any(value => value.PairExecutionId != pairExecutionId) ||
            canonicalInventory.Select(value => value.FileCodeUnitId).Distinct().Count() != canonicalInventory.Length ||
            canonicalInventory.Select(value => value.PairSelectionOrdinal).Distinct().Count() != canonicalInventory.Length ||
            status == Pass1OutcomeStatus.Succeeded && ordinaryFailureCount != 0 ||
            status == Pass1OutcomeStatus.CompletedWithWarnings &&
                (ordinaryFailureCount == 0 || ordinaryFailureCount > budget ||
                 succeededHighValueSeedCount != highValueSeedCount) ||
            status == Pass1OutcomeStatus.FailedPass1 &&
                ordinaryFailureCount <= budget && succeededHighValueSeedCount == highValueSeedCount)
        {
            throw new ArgumentException("The Pass 1 outcome does not match its canonical failure inventory.");
        }

        string? reconciliation = NormalizeOptionalHash(reconciliationFingerprint);
        string fingerprint = CreateFingerprint(
            pairExecutionId,
            status,
            dispatchedCount,
            completedCount,
            ordinaryFailureCount,
            budget,
            highValueSeedCount,
            succeededHighValueSeedCount,
            reconciliation,
            reconciledEdgeCount,
            canonicalInventory);
        return new Pass1Outcome
        {
            PairExecutionId = pairExecutionId,
            Status = status,
            DispatchedCount = dispatchedCount,
            CompletedCount = completedCount,
            OrdinaryFailureCount = ordinaryFailureCount,
            OrdinaryFailureBudget = budget,
            HighValueSeedCount = highValueSeedCount,
            SucceededHighValueSeedCount = succeededHighValueSeedCount,
            ReconciliationFingerprint = reconciliation,
            ReconciledEdgeCount = reconciledEdgeCount,
            OutcomeFingerprint = fingerprint,
            CompletedAt = completedAt,
        };
    }

    private static string CreateFingerprint(
        Guid pairExecutionId,
        Pass1OutcomeStatus status,
        int dispatchedCount,
        int completedCount,
        int ordinaryFailureCount,
        int ordinaryFailureBudget,
        int highValueSeedCount,
        int succeededHighValueSeedCount,
        string? reconciliationFingerprint,
        int reconciledEdgeCount,
        IReadOnlyList<Pass1FailureInventoryEntry> failureInventory)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string field in new[]
                 {
                     _fingerprintDomain,
                     pairExecutionId.ToString("D"),
                     status.ToString(),
                     dispatchedCount.ToString(CultureInfo.InvariantCulture),
                     completedCount.ToString(CultureInfo.InvariantCulture),
                     ordinaryFailureCount.ToString(CultureInfo.InvariantCulture),
                     ordinaryFailureBudget.ToString(CultureInfo.InvariantCulture),
                     highValueSeedCount.ToString(CultureInfo.InvariantCulture),
                     succeededHighValueSeedCount.ToString(CultureInfo.InvariantCulture),
                     reconciliationFingerprint ?? string.Empty,
                     reconciledEdgeCount.ToString(CultureInfo.InvariantCulture),
                 })
        {
            AppendField(hash, field);
        }

        foreach (Pass1FailureInventoryEntry entry in failureInventory)
        {
            AppendField(hash, entry.Id.ToString("D"));
            AppendField(hash, entry.FileCodeUnitId.ToString("D"));
            AppendField(hash, entry.RepositoryId.ToString("D"));
            AppendField(hash, entry.PairSelectionOrdinal.ToString(CultureInfo.InvariantCulture));
            AppendField(hash, entry.ManifestOrdinal.ToString(CultureInfo.InvariantCulture));
            AppendField(hash, entry.SegmentOrdinal.ToString(CultureInfo.InvariantCulture));
            AppendField(hash, entry.RepositoryRelativePath);
            AppendField(hash, entry.FailureCategory.ToString());
            AppendField(hash, entry.FailureCode);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendField(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static string? NormalizeOptionalHash(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Length != 64 || value.Any(character =>
                !char.IsAsciiHexDigit(character) || char.IsAsciiLetterUpper(character)))
        {
            throw new ArgumentException("A lower-case SHA-256 reconciliation fingerprint is required.", nameof(value));
        }

        return value;
    }
}

public enum Pass1OrdinaryFailureCategory
{
    ProviderTransient,
    WorkerTransient,
    InvalidResult,
}

public sealed class Pass1FailureInventoryEntry
{
    private const string _identityDomain = "vulgata/story-12.3/pass1-failure-inventory/1.0";

    private Pass1FailureInventoryEntry()
    {
    }

    public Guid Id { get; private set; }
    public Guid PairExecutionId { get; private set; }
    public Guid FileCodeUnitId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public int PairSelectionOrdinal { get; private set; }
    public int ManifestOrdinal { get; private set; }
    public int SegmentOrdinal { get; private set; }
    public string RepositoryRelativePath { get; private set; } = string.Empty;
    public Pass1OrdinaryFailureCategory FailureCategory { get; private set; }
    public string FailureCode { get; private set; } = string.Empty;

    public static Pass1FailureInventoryEntry Create(
        Guid pairExecutionId,
        CodeUnit fileCodeUnit,
        int pairSelectionOrdinal,
        int manifestOrdinal,
        Pass1OrdinaryFailureCategory failureCategory,
        string failureCode)
    {
        if (pairExecutionId == Guid.Empty || fileCodeUnit.Id == Guid.Empty ||
            fileCodeUnit.RepositoryId == Guid.Empty || fileCodeUnit.SegmentOrdinal is null or < 1 ||
            pairSelectionOrdinal < 1 || manifestOrdinal < 1 || !Enum.IsDefined(failureCategory))
        {
            throw new ArgumentException("A complete prepared failed-file identity is required.");
        }

        string code = NormalizeFailureCode(failureCode);
        byte[] envelope = Encoding.UTF8.GetBytes(string.Join(
            '\n',
            _identityDomain,
            pairExecutionId.ToString("D"),
            fileCodeUnit.Id.ToString("D")));
        byte[] identity = SHA256.HashData(envelope)[..16];
        identity[6] = (byte)((identity[6] & 0x0f) | 0x80);
        identity[8] = (byte)((identity[8] & 0x3f) | 0x80);
        return new Pass1FailureInventoryEntry
        {
            Id = new Guid(identity, bigEndian: true),
            PairExecutionId = pairExecutionId,
            FileCodeUnitId = fileCodeUnit.Id,
            RepositoryId = fileCodeUnit.RepositoryId,
            PairSelectionOrdinal = pairSelectionOrdinal,
            ManifestOrdinal = manifestOrdinal,
            SegmentOrdinal = fileCodeUnit.SegmentOrdinal.Value,
            RepositoryRelativePath = fileCodeUnit.FilePath,
            FailureCategory = failureCategory,
            FailureCode = code,
        };
    }

    private static string NormalizeFailureCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string normalized = value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        if (normalized.Length is < 1 or > 64 || normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException("A bounded machine-readable failure code is required.", nameof(value));
        }

        return normalized;
    }
}
