using System.Text;
using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// The bounded, schema-validated input to one Worker Agent job, dispatched by
/// the Sequential Scan Runner through <see cref="IWorkerAgentDispatcher"/>.
/// Story 11.2 defines the contract; Story 11.3 supplies the LLM-backed
/// implementation. Immutable; constructed through <see cref="Create"/>, which
/// validates every invariant and applies the Story 11.1 sensitive-content
/// guard. A rejected value throws <see cref="ArgumentException"/>; the
/// rejected value and any provider diagnostic do not cross the contract.
/// </summary>
public sealed class WorkerAgentJobPacket
{
    private const int MaximumFilePathUtf8Bytes = 2_048;
    private const int MaximumSymbolNameUtf8Bytes = 512;
    private const int MaximumProfiledRevisionLength = 64;

    private WorkerAgentJobPacket(
        Guid jobId,
        WorkerAgentJobKind kind,
        Guid scanExecutionId,
        Guid repositoryId,
        Guid systemId,
        Guid scanRunId,
        Guid codeUnitId,
        string profiledRevision,
        BoundedSourceSlice sourceSlice)
    {
        JobId = jobId;
        Kind = kind;
        ScanExecutionId = scanExecutionId;
        RepositoryId = repositoryId;
        SystemId = systemId;
        ScanRunId = scanRunId;
        CodeUnitId = codeUnitId;
        ProfiledRevision = profiledRevision;
        SourceSlice = sourceSlice;
    }

    public Guid JobId { get; }
    public WorkerAgentJobKind Kind { get; }
    public Guid ScanExecutionId { get; }
    public Guid RepositoryId { get; }
    public Guid SystemId { get; }
    public Guid ScanRunId { get; }
    public Guid CodeUnitId { get; }
    public string ProfiledRevision { get; }
    public BoundedSourceSlice SourceSlice { get; }

    /// <summary>
    /// Construct a bounded <see cref="WorkerAgentJobPacket"/>. Validates every
    /// invariant per <c>contracts/worker-agent-job-packet.md</c>: closed kind
    /// allowlist, non-empty identity Guids, bounded lowercase Git OID
    /// ProfiledRevision, closed <see cref="BoundedSourceSlice"/> allowlist,
    /// finite UTF-8/count limits, Form-C normalization, and the Story 11.1
    /// sensitive-content guard. No source text is carried — only identity and
    /// hashes. Throws <see cref="ArgumentException"/> on rejection.
    /// </summary>
    public static WorkerAgentJobPacket Create(
        Guid jobId,
        WorkerAgentJobKind kind,
        Guid scanExecutionId,
        Guid repositoryId,
        Guid systemId,
        Guid scanRunId,
        Guid codeUnitId,
        string profiledRevision,
        string filePath,
        int lineStart,
        int lineEnd,
        string symbolName,
        CodeUnitType unitType,
        string? language,
        string sourceHash,
        string fileIdentityHash)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Worker Agent job identifier must be non-empty.",
                nameof(jobId));
        }

        if (!Enum.IsDefined(kind) || kind != WorkerAgentJobKind.Pass1CodeLogicDocument)
        {
            throw new ArgumentException(
                "The Worker Agent job kind is not on the closed allowlist.",
                nameof(kind));
        }

        if (scanExecutionId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Scan execution identifier must be non-empty.",
                nameof(scanExecutionId));
        }

        if (repositoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Repository identifier must be non-empty.",
                nameof(repositoryId));
        }

        if (systemId == Guid.Empty)
        {
            throw new ArgumentException(
                "The System identifier must be non-empty.",
                nameof(systemId));
        }

        if (scanRunId == Guid.Empty)
        {
            throw new ArgumentException(
                "The ScanRun identifier must be non-empty.",
                nameof(scanRunId));
        }

        if (codeUnitId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Code Unit identifier must be non-empty.",
                nameof(codeUnitId));
        }

        string revision = RequireProfiledRevision(profiledRevision);
        BoundedSourceSlice slice = BoundedSourceSlice.Create(
            filePath,
            lineStart,
            lineEnd,
            symbolName,
            unitType,
            language,
            sourceHash,
            fileIdentityHash);

        return new(
            jobId,
            kind,
            scanExecutionId,
            repositoryId,
            systemId,
            scanRunId,
            codeUnitId,
            revision,
            slice);
    }

    private static string RequireProfiledRevision(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim();
        if (normalized.Length > MaximumProfiledRevisionLength ||
            normalized.Any(character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "The Profiled Revision must be a bounded lowercase Git object ID.",
                nameof(value));
        }

        return normalized;
    }

    /// <summary>
    /// Closed allowlist of bounded source-slice fields. No source text is
    /// carried — only identity, range, and hashes. Every string is subject to
    /// the Story 11.1 sensitive-content guard.
    /// </summary>
    public sealed class BoundedSourceSlice
    {
        private BoundedSourceSlice(
            string filePath,
            int lineStart,
            int lineEnd,
            string symbolName,
            CodeUnitType unitType,
            string? language,
            string sourceHash,
            string fileIdentityHash)
        {
            FilePath = filePath;
            LineStart = lineStart;
            LineEnd = lineEnd;
            SymbolName = symbolName;
            UnitType = unitType;
            Language = language;
            SourceHash = sourceHash;
            FileIdentityHash = fileIdentityHash;
        }

        public string FilePath { get; }
        public int LineStart { get; }
        public int LineEnd { get; }
        public string SymbolName { get; }
        public CodeUnitType UnitType { get; }
        public string? Language { get; }
        public string SourceHash { get; }
        public string FileIdentityHash { get; }

        public static BoundedSourceSlice Create(
            string filePath,
            int lineStart,
            int lineEnd,
            string symbolName,
            CodeUnitType unitType,
            string? language,
            string sourceHash,
            string fileIdentityHash)
        {
            string path = RequireBoundedText(filePath, MaximumFilePathUtf8Bytes, nameof(filePath));
            if (Path.IsPathRooted(path) || path.Split('/').Any(segment => segment == ".."))
            {
                throw new ArgumentException(
                    "The file path must be repository-relative.",
                    nameof(filePath));
            }

            // Reuse the Story 11.1 path normalization discipline.
            path = path.Replace('\\', '/').TrimStart('/');

            EnsureSafeContent(path, nameof(filePath));

            ArgumentOutOfRangeException.ThrowIfLessThan(lineStart, 1, nameof(lineStart));
            ArgumentOutOfRangeException.ThrowIfLessThan(lineEnd, lineStart, nameof(lineEnd));

            string symbol = RequireBoundedText(symbolName, MaximumSymbolNameUtf8Bytes, nameof(symbolName));
            EnsureSafeContent(symbol, nameof(symbolName));

            if (!Enum.IsDefined(unitType))
            {
                throw new ArgumentException(
                    "The Code Unit type is not on the closed allowlist.",
                    nameof(unitType));
            }

            string? normalizedLanguage = null;
            if (!string.IsNullOrWhiteSpace(language))
            {
                normalizedLanguage = CodeUnit.NormalizeLanguage(language);
                EnsureSafeContent(normalizedLanguage, nameof(language));
            }

            string source = RequireSha256(sourceHash, nameof(sourceHash));
            string fileIdentity = RequireSha256(fileIdentityHash, nameof(fileIdentityHash));

            return new(
                path,
                lineStart,
                lineEnd,
                symbol,
                unitType,
                normalizedLanguage,
                source,
                fileIdentity);
        }

        private static string RequireBoundedText(string value, int maximumUtf8Bytes, string parameterName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            string normalized = value.Trim().Normalize(NormalizationForm.FormC);
            if (normalized.Any(character =>
                    character <= '\u001f' ||
                    character is >= '\u007f' and <= '\u009f'))
            {
                throw new ArgumentException(
                    "The text contains forbidden control characters.",
                    parameterName);
            }

            int byteCount = Encoding.UTF8.GetByteCount(normalized);
            if (byteCount > maximumUtf8Bytes)
            {
                throw new ArgumentException(
                    "The text exceeds the finite UTF-8 byte limit.",
                    parameterName);
            }

            return normalized;
        }

        private static string RequireSha256(string value, string parameterName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            string normalized = value.Trim().ToLowerInvariant();
            if (normalized.Length != 64 ||
                normalized.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new ArgumentException(
                    "The hash must be a lowercase SHA-256.",
                    parameterName);
            }

            return normalized;
        }

        private static void EnsureSafeContent(string value, string parameterName)
        {
            try
            {
                KnowledgeTextSafety.EnsureSafe(value);
            }
            catch (KnowledgeStoreContractException ex)
            {
                throw new ArgumentException(
                    "The content was rejected by the sensitive-content guard.",
                    parameterName,
                    ex);
            }
        }
    }
}
