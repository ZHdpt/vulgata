namespace Vulgata.Core.Entities;

public enum CommunicationKind
{
    Rpc,
    Http,
}

public enum ClassificationLevel
{
    Confirmed,
    Hint,
    Warning,
}

public enum ConfidenceBand
{
    High,
    Conservative,
    None,
}

public enum EvidenceProvenance
{
    StructuralAnnotation,
    FrameworkFact,
    PackageManifest,
    MatchedConfiguration,
    Import,
    Detector,
}

public enum EvidenceRule
{
    RpcDirectAnnotation,
    RpcMatchedConfiguration,
    HttpControllerAnnotation,
    HttpClientAnnotation,
    HttpMatchedConfiguration,
    RpcImportOnly,
    HttpImportOnly,
    MalformedEvidence,
    UnsafePath,
    BoundExceeded,
    ContradictoryEvidence,
    UnsupportedFamily,
}

public sealed class CommunicationKindClassification
{
    public const string UnknownRelativePath = "(unknown)";
    public const string PreparedSchemaIdentity = "communication-classification/1.0";

    private CommunicationKindClassification()
    {
    }

    private CommunicationKindClassification(
        Guid preScanResultId,
        CommunicationKind? kind,
        ClassificationLevel level,
        ConfidenceBand confidence,
        EvidenceRule rule,
        EvidenceProvenance provenance,
        string relativePath,
        string locator,
        int occurrenceCount,
        string fingerprint)
    {
        Id = Guid.NewGuid();
        PreScanResultId = preScanResultId;
        Kind = kind;
        Level = level;
        Confidence = confidence;
        Rule = rule;
        Provenance = provenance;
        RelativePath = relativePath;
        Locator = locator;
        OccurrenceCount = occurrenceCount;
        Fingerprint = fingerprint;
    }

    public Guid Id { get; private set; }
    public Guid PreScanResultId { get; private set; }
    public CommunicationKind? Kind { get; private set; }
    public ClassificationLevel Level { get; private set; }
    public ConfidenceBand Confidence { get; private set; }
    public EvidenceRule Rule { get; private set; }
    public EvidenceProvenance Provenance { get; private set; }
    public string RelativePath { get; private set; } = string.Empty;
    public string Locator { get; private set; } = string.Empty;
    public int OccurrenceCount { get; private set; }
    public string Fingerprint { get; private set; } = string.Empty;
    public string? CatalogRuleIdentity { get; private set; }
    public PreScanResult? PreScanResult { get; private set; }

    public static CommunicationKindClassification Create(
        Guid preScanResultId,
        CommunicationKind? kind,
        ClassificationLevel level,
        ConfidenceBand confidence,
        EvidenceRule rule,
        EvidenceProvenance provenance,
        string relativePath,
        string locator,
        int occurrenceCount,
        string fingerprint)
    {
        if (preScanResultId == Guid.Empty)
        {
            throw new ArgumentException(
                "A communication classification requires its Pre-Scan Result identity.",
                nameof(preScanResultId));
        }

        ValidateClosedValues(kind, level, confidence, rule, provenance);
        ValidateCompatibility(kind, level, confidence, rule, provenance);
        string safePath = RequireSafeRelativePath(relativePath, level);
        string safeLocator = RequireSafeLocator(locator, safePath);
        if (occurrenceCount is < 1 or > 250_000)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrenceCount));
        }

        return new(
            preScanResultId,
            kind,
            level,
            confidence,
            rule,
            provenance,
            safePath,
            safeLocator,
            occurrenceCount,
            RequireLowercaseSha256(fingerprint));
    }

    private static void ValidateClosedValues(
        CommunicationKind? kind,
        ClassificationLevel level,
        ConfidenceBand confidence,
        EvidenceRule rule,
        EvidenceProvenance provenance)
    {
        if ((kind.HasValue && !Enum.IsDefined(kind.Value)) ||
            !Enum.IsDefined(level) ||
            !Enum.IsDefined(confidence) ||
            !Enum.IsDefined(rule) ||
            !Enum.IsDefined(provenance))
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Classification values must be closed.");
        }
    }

    private static void ValidateCompatibility(
        CommunicationKind? kind,
        ClassificationLevel level,
        ConfidenceBand confidence,
        EvidenceRule rule,
        EvidenceProvenance provenance)
    {
        bool compatible = level switch
        {
            ClassificationLevel.Confirmed =>
                confidence == ConfidenceBand.High &&
                IsCompatibleConfirmed(kind, rule, provenance),
            ClassificationLevel.Hint =>
                confidence == ConfidenceBand.Conservative &&
                IsCompatibleHint(kind, rule, provenance),
            ClassificationLevel.Warning =>
                confidence == ConfidenceBand.None &&
                IsCompatibleWarning(kind, rule, provenance),
            _ => false,
        };
        if (!compatible)
        {
            throw new ArgumentException(
                "Classification level, confidence, kind, rule, and provenance are incompatible.");
        }
    }

    private static bool IsCompatibleConfirmed(
        CommunicationKind? kind,
        EvidenceRule rule,
        EvidenceProvenance provenance) =>
        (kind, rule, provenance) switch
        {
            (CommunicationKind.Rpc, EvidenceRule.RpcDirectAnnotation,
                EvidenceProvenance.StructuralAnnotation) => true,
            (CommunicationKind.Rpc, EvidenceRule.RpcMatchedConfiguration,
                EvidenceProvenance.MatchedConfiguration) => true,
            (CommunicationKind.Http, EvidenceRule.HttpControllerAnnotation,
                EvidenceProvenance.StructuralAnnotation) => true,
            (CommunicationKind.Http, EvidenceRule.HttpClientAnnotation,
                EvidenceProvenance.StructuralAnnotation) => true,
            (CommunicationKind.Http, EvidenceRule.HttpMatchedConfiguration,
                EvidenceProvenance.MatchedConfiguration) => true,
            _ => false,
        };

    private static bool IsCompatibleHint(
        CommunicationKind? kind,
        EvidenceRule rule,
        EvidenceProvenance provenance)
    {
        bool compatibleRule =
            (kind == CommunicationKind.Rpc && rule == EvidenceRule.RpcImportOnly) ||
            (kind == CommunicationKind.Http && rule == EvidenceRule.HttpImportOnly);
        return compatibleRule &&
            provenance is EvidenceProvenance.Import or
                EvidenceProvenance.PackageManifest or
                EvidenceProvenance.FrameworkFact;
    }

    private static bool IsCompatibleWarning(
        CommunicationKind? kind,
        EvidenceRule rule,
        EvidenceProvenance provenance)
    {
        if (provenance != EvidenceProvenance.Detector ||
            rule is not (
                EvidenceRule.MalformedEvidence or
                EvidenceRule.UnsafePath or
                EvidenceRule.BoundExceeded or
                EvidenceRule.ContradictoryEvidence or
                EvidenceRule.UnsupportedFamily))
        {
            return false;
        }

        return rule != EvidenceRule.UnsupportedFamily || kind is null;
    }

    private static string RequireSafeRelativePath(
        string relativePath,
        ClassificationLevel level)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (relativePath.Length > 2_048 ||
            relativePath.Any(char.IsControl) ||
            relativePath.Length != relativePath.Trim().Length ||
            relativePath[0] is '/' or '\\' ||
            Path.IsPathRooted(relativePath) ||
            (relativePath.Length >= 2 &&
                char.IsAsciiLetter(relativePath[0]) &&
                relativePath[1] == ':'))
        {
            throw new ArgumentException(
                "Evidence paths must be bounded, normalized Repository-relative paths.",
                nameof(relativePath));
        }

        string normalized = relativePath.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        if (segments.Any(segment => segment is "" or "." or "..") ||
            (normalized == UnknownRelativePath && level != ClassificationLevel.Warning))
        {
            throw new ArgumentException(
                "Evidence paths must be bounded, normalized Repository-relative paths.",
                nameof(relativePath));
        }

        return normalized;
    }

    private static string RequireSafeLocator(string locator, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locator);
        if (locator.Length > 4_096 ||
            locator.Length != locator.Trim().Length ||
            locator.Any(char.IsControl) ||
            !locator.StartsWith($"{relativePath}#", StringComparison.Ordinal) ||
            locator.Length == relativePath.Length + 1)
        {
            throw new ArgumentException(
                "An Evidence Locator must be a bounded Repository-relative path and selector.",
                nameof(locator));
        }

        return locator;
    }

    private static string RequireLowercaseSha256(string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        if (fingerprint.Length != 64 ||
            fingerprint.Any(character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "The classification fingerprint must be lowercase SHA-256.",
                nameof(fingerprint));
        }

        return fingerprint;
    }
}
