using System.Text;
using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Finite bounds shared by the provider-independent Worker Agent contracts.
/// Provider adapters may enforce stricter limits, but never looser ones.
/// </summary>
public static class WorkerAgentContractLimits
{
    public const int SupportedSchemaVersion = 1;
    public const int MaximumSourceSliceUtf8Bytes = 65_536;
    public const int MaximumSourceSliceLines = 2_000;
    public const int MaximumProviderRequestUtf8Bytes = 131_072;
    public const int MaximumProviderResponseUtf8Bytes = 65_536;
    public const int MaximumJsonDepth = 32;
    public const int MaximumStructuralContextItems = 256;
    public const int MaximumCommunicationEvidenceItems = 256;
    public const int MaximumReferenceDocumentVersionIds = 1_024;
    public const int MaximumBusinessMainSteps = 32;
}

internal static class WorkerAgentContractText
{
    internal static string RequireBoundedSafeText(
        string value,
        int maximumUtf8Bytes,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Any(character =>
                character <= '\u001f' ||
                character is >= '\u007f' and <= '\u009f') ||
            Encoding.UTF8.GetByteCount(normalized) > maximumUtf8Bytes)
        {
            throw new ArgumentException(
                "The Worker Agent text exceeds its finite bound or contains control characters.",
                parameterName);
        }

        try
        {
            KnowledgeTextSafety.EnsureSafe(normalized);
        }
        catch (KnowledgeStoreContractException ex)
        {
            throw new ArgumentException(
                "The Worker Agent text was rejected by the sensitive-content guard.",
                parameterName,
                ex);
        }

        return normalized;
    }

    internal static string RequireBoundedSafeLayoutText(
        string value,
        int maximumUtf8Bytes,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Any(character =>
                character <= '\u001f' && character != '\n' ||
                character is >= '\u007f' and <= '\u009f') ||
            Encoding.UTF8.GetByteCount(normalized) > maximumUtf8Bytes)
        {
            throw new ArgumentException(
                "The Worker Agent layout text exceeds its finite bound or contains forbidden controls.",
                parameterName);
        }

        try
        {
            KnowledgeTextSafety.EnsureSafe(normalized);
        }
        catch (KnowledgeStoreContractException ex)
        {
            throw new ArgumentException(
                "The Worker Agent layout text was rejected by the sensitive-content guard.",
                parameterName,
                ex);
        }

        return normalized;
    }

    internal static string RequireRepositoryRelativePath(
        string value,
        string parameterName)
    {
        string path = RequireBoundedSafeText(
            value,
            KnowledgeDocumentLimits.MaximumRepositoryPathBytes,
            parameterName).Replace('\\', '/');
        string[] segments = path.Split('/');
        if (Path.IsPathRooted(path) ||
            path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':' ||
            segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new ArgumentException(
                "The Worker Agent path must be Repository-relative.",
                parameterName);
        }

        return path;
    }
}

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
    private readonly Guid? _codeUnitId;
    private readonly BoundedSourceSlice? _sourceSlice;

    private WorkerAgentJobPacket(
        Guid jobId,
        WorkerAgentJobKind kind,
        Guid scanExecutionId,
        Guid repositoryId,
        Guid systemId,
        Guid scanRunId,
        Guid? codeUnitId,
        string profiledRevision,
        BoundedSourceSlice? sourceSlice,
        WorkerAgentJobPayload payload)
    {
        JobId = jobId;
        Kind = kind;
        ScanExecutionId = scanExecutionId;
        RepositoryId = repositoryId;
        SystemId = systemId;
        ScanRunId = scanRunId;
        _codeUnitId = codeUnitId;
        ProfiledRevision = profiledRevision;
        _sourceSlice = sourceSlice;
        Payload = payload;
    }

    public Guid JobId { get; }
    public WorkerAgentJobKind Kind { get; }
    public Guid ScanExecutionId { get; }
    public Guid RepositoryId { get; }
    public Guid SystemId { get; }
    public Guid ScanRunId { get; }
    public Guid CodeUnitId => _codeUnitId ?? throw new InvalidOperationException(
        "The prepared Pass 2 packet has no single Code Unit.");
    public string ProfiledRevision { get; }
    public BoundedSourceSlice SourceSlice => _sourceSlice ?? throw new InvalidOperationException(
        "The prepared Pass 2 packet has no source slice.");
    public int SchemaVersion => WorkerAgentContractLimits.SupportedSchemaVersion;
    public WorkerAgentJobPayload Payload { get; }

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
            slice,
            Pass1CodeLogicJobPayload.CreateValidated(
                codeUnitId,
                slice,
                [],
                [],
                [],
                []));
    }

    /// <summary>
    /// Constructs the typed Pass 1 packet used by Story 11.3. The common
    /// header remains separate from the exhaustive payload union.
    /// </summary>
    public static WorkerAgentJobPacket Create(
        Guid jobId,
        WorkerAgentJobKind kind,
        Guid scanExecutionId,
        Guid repositoryId,
        Guid systemId,
        Guid scanRunId,
        string profiledRevision,
        int schemaVersion,
        WorkerAgentJobPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (jobId == Guid.Empty ||
            scanExecutionId == Guid.Empty ||
            repositoryId == Guid.Empty ||
            systemId == Guid.Empty ||
            scanRunId == Guid.Empty)
        {
            throw new ArgumentException("Worker Agent packet identifiers must be non-empty.");
        }

        if (!Enum.IsDefined(kind) || payload.Kind != kind)
        {
            throw new ArgumentException(
                "The Worker Agent job kind and payload must be one matching closed variant.",
                nameof(payload));
        }

        if (schemaVersion != WorkerAgentContractLimits.SupportedSchemaVersion)
        {
            throw new ArgumentException(
                "The Worker Agent schema version is not supported.",
                nameof(schemaVersion));
        }

        string revision = RequireProfiledRevision(profiledRevision);
        Guid? codeUnitId;
        BoundedSourceSlice? sourceSlice;
        switch (payload)
        {
            case Pass1CodeLogicJobPayload pass1
                when kind == WorkerAgentJobKind.Pass1CodeLogicDocument:
                codeUnitId = pass1.CodeUnitId;
                sourceSlice = pass1.SourceSlice;
                break;
            case Pass2BusinessLogicJobPayload pass2
                when kind == WorkerAgentJobKind.Pass2BusinessLogicDocument &&
                    pass2.SystemId == systemId &&
                    pass2.RepositoryId == repositoryId:
                codeUnitId = null;
                sourceSlice = null;
                break;
            default:
                throw new ArgumentException(
                    "The Worker Agent payload is not on the closed allowlist.",
                    nameof(payload));
        }

        return new(
            jobId,
            kind,
            scanExecutionId,
            repositoryId,
            systemId,
            scanRunId,
            codeUnitId,
            revision,
            sourceSlice,
            payload);
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
            if ((long)lineEnd - lineStart + 1 >
                WorkerAgentContractLimits.MaximumSourceSliceLines)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lineEnd),
                    "The source slice exceeds the finite line limit.");
            }

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

/// <summary>
/// Exhaustive base type for Worker Agent job payload variants.
/// </summary>
public abstract class WorkerAgentJobPayload
{
    private protected WorkerAgentJobPayload()
    {
    }

    public abstract WorkerAgentJobKind Kind { get; }
}

/// <summary>
/// One server-generated Source Anchor identity issued to a Pass 1 worker.
/// The model must echo these identifiers verbatim so that anchor IDs stay
/// globally unique across documents. Carries no Evidence Locator because
/// prepared anchors are issued before any model-authored evidence exists.
/// </summary>
public sealed record WorkerAgentPreparedSourceAnchor
{
    public Guid Id { get; }
    public string RepositoryRelativePath { get; }
    public int LineStart { get; }
    public int LineEnd { get; }
    public string SourceHash { get; }

    public WorkerAgentPreparedSourceAnchor(
        Guid id,
        string repositoryRelativePath,
        int lineStart,
        int lineEnd,
        string sourceHash)
    {
        Id = id;
        RepositoryRelativePath = repositoryRelativePath;
        LineStart = lineStart;
        LineEnd = lineEnd;
        SourceHash = sourceHash;
    }
}

/// <summary>
/// Exact source identity and server-built allowlists for one Pass 1 Code Unit.
/// No source bytes are carried by this contract.
/// </summary>
public sealed class Pass1CodeLogicJobPayload : WorkerAgentJobPayload
{
    private Pass1CodeLogicJobPayload(
        Guid codeUnitId,
        WorkerAgentJobPacket.BoundedSourceSlice sourceSlice,
        IReadOnlyList<WorkerAgentStructuralContext> neighboringStructuralContext,
        IReadOnlyList<WorkerAgentCommunicationEvidence> approvedCommunicationEvidence,
        IReadOnlyList<Guid> allowedReferenceDocumentVersionIds,
        IReadOnlyList<WorkerAgentPreparedSourceAnchor> preparedSourceAnchors)
    {
        CodeUnitId = codeUnitId;
        SourceSlice = sourceSlice;
        NeighboringStructuralContext = neighboringStructuralContext;
        ApprovedCommunicationEvidence = approvedCommunicationEvidence;
        AllowedReferenceDocumentVersionIds = allowedReferenceDocumentVersionIds;
        PreparedSourceAnchors = preparedSourceAnchors;
    }

    public override WorkerAgentJobKind Kind => WorkerAgentJobKind.Pass1CodeLogicDocument;
    public Guid CodeUnitId { get; }
    public WorkerAgentJobPacket.BoundedSourceSlice SourceSlice { get; }
    public string RepositoryRelativePath => SourceSlice.FilePath;
    public int LineStart => SourceSlice.LineStart;
    public int LineEnd => SourceSlice.LineEnd;
    public string SourceHash => SourceSlice.SourceHash;
    public string FileIdentityHash => SourceSlice.FileIdentityHash;
    public IReadOnlyList<WorkerAgentStructuralContext> NeighboringStructuralContext { get; }
    public IReadOnlyList<WorkerAgentCommunicationEvidence> ApprovedCommunicationEvidence { get; }
    public IReadOnlyList<Guid> AllowedReferenceDocumentVersionIds { get; }

    /// <summary>
    /// Server-generated Source Anchor identities the model must echo verbatim.
    /// Guarantees globally unique anchor identifiers across documents and
    /// prevents the model from synthesizing colliding identifiers.
    /// </summary>
    public IReadOnlyList<WorkerAgentPreparedSourceAnchor> PreparedSourceAnchors { get; }

    public static Pass1CodeLogicJobPayload Create(
        Guid codeUnitId,
        string repositoryRelativePath,
        int lineStart,
        int lineEnd,
        string symbolName,
        CodeUnitType unitType,
        string? language,
        string sourceHash,
        string fileIdentityHash,
        IReadOnlyList<WorkerAgentStructuralContext> neighboringStructuralContext,
        IReadOnlyList<WorkerAgentCommunicationEvidence> approvedCommunicationEvidence,
        IReadOnlyList<Guid> allowedReferenceDocumentVersionIds,
        IReadOnlyList<WorkerAgentPreparedSourceAnchor>? preparedSourceAnchors = null)
    {
        if (codeUnitId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Code Unit identifier must be non-empty.",
                nameof(codeUnitId));
        }

        WorkerAgentJobPacket.BoundedSourceSlice sourceSlice =
            WorkerAgentJobPacket.BoundedSourceSlice.Create(
                repositoryRelativePath,
                lineStart,
                lineEnd,
                symbolName,
                unitType,
                language,
                sourceHash,
                fileIdentityHash);

        return CreateValidated(
            codeUnitId,
            sourceSlice,
            neighboringStructuralContext,
            approvedCommunicationEvidence,
            allowedReferenceDocumentVersionIds,
            preparedSourceAnchors ?? []);
    }

    internal static Pass1CodeLogicJobPayload CreateValidated(
        Guid codeUnitId,
        WorkerAgentJobPacket.BoundedSourceSlice sourceSlice,
        IReadOnlyList<WorkerAgentStructuralContext> neighboringStructuralContext,
        IReadOnlyList<WorkerAgentCommunicationEvidence> approvedCommunicationEvidence,
        IReadOnlyList<Guid> allowedReferenceDocumentVersionIds,
        IReadOnlyList<WorkerAgentPreparedSourceAnchor> preparedSourceAnchors)
    {
        ArgumentNullException.ThrowIfNull(neighboringStructuralContext);
        ArgumentNullException.ThrowIfNull(approvedCommunicationEvidence);
        ArgumentNullException.ThrowIfNull(allowedReferenceDocumentVersionIds);
        ArgumentNullException.ThrowIfNull(preparedSourceAnchors);
        if (neighboringStructuralContext.Count >
                WorkerAgentContractLimits.MaximumStructuralContextItems ||
            !IsStrictlyAscending(
                neighboringStructuralContext,
                item => item.CodeUnitId))
        {
            throw new ArgumentException(
                "Structural context must be bounded, unique, and canonically ordered.",
                nameof(neighboringStructuralContext));
        }

        if (approvedCommunicationEvidence.Count >
                WorkerAgentContractLimits.MaximumCommunicationEvidenceItems ||
            !IsStrictlyAscending(
                approvedCommunicationEvidence,
                item => item.EvidenceId))
        {
            throw new ArgumentException(
                "Communication evidence must be bounded, unique, and canonically ordered.",
                nameof(approvedCommunicationEvidence));
        }

        if (allowedReferenceDocumentVersionIds.Count >
                WorkerAgentContractLimits.MaximumReferenceDocumentVersionIds ||
            allowedReferenceDocumentVersionIds.Any(id => id == Guid.Empty) ||
            !IsStrictlyAscending(allowedReferenceDocumentVersionIds))
        {
            throw new ArgumentException(
                "Allowed reference Document Version identifiers must be bounded, unique, and canonically ordered.",
                nameof(allowedReferenceDocumentVersionIds));
        }

        if (preparedSourceAnchors.Count > KnowledgeDocumentLimits.MaximumAnchors ||
            preparedSourceAnchors.Any(anchor =>
                !StringComparer.Ordinal.Equals(
                    anchor.RepositoryRelativePath,
                    sourceSlice.FilePath) ||
                anchor.LineStart < sourceSlice.LineStart ||
                anchor.LineEnd > sourceSlice.LineEnd ||
                anchor.LineEnd < anchor.LineStart ||
                !StringComparer.Ordinal.Equals(anchor.SourceHash, sourceSlice.SourceHash)) ||
            !IsStrictlyAscending(preparedSourceAnchors, anchor => anchor.Id))
        {
            throw new ArgumentException(
                "Prepared Source Anchors must be bounded, canonical, and inside the exact source slice.",
                nameof(preparedSourceAnchors));
        }

        return new(
            codeUnitId,
            sourceSlice,
            Array.AsReadOnly(neighboringStructuralContext.ToArray()),
            Array.AsReadOnly(approvedCommunicationEvidence.ToArray()),
            Array.AsReadOnly(allowedReferenceDocumentVersionIds.ToArray()),
            Array.AsReadOnly(preparedSourceAnchors.ToArray()));
    }

    private static bool IsStrictlyAscending(IReadOnlyList<Guid> values)
    {
        for (int index = 1; index < values.Count; index++)
        {
            if (values[index - 1].CompareTo(values[index]) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStrictlyAscending<T>(
        IReadOnlyList<T> values,
        Func<T, Guid> identifierSelector)
    {
        Guid? previous = null;
        foreach (T value in values)
        {
            Guid identifier = identifierSelector(value);
            if (identifier == Guid.Empty ||
                previous.HasValue && previous.Value.CompareTo(identifier) >= 0)
            {
                return false;
            }

            previous = identifier;
        }

        return true;
    }
}

/// <summary>
/// One bounded neighboring Code Unit supplied as structural context.
/// </summary>
/// <summary>
/// The exact bounded projection of one current Code Logic Document Version
/// supplied to prepared Pass 2 synthesis. Relationships and source bytes are
/// deliberately absent.
/// </summary>
public sealed class WorkerAgentCodeLogicDocumentVersionProjection
{
    private WorkerAgentCodeLogicDocumentVersionProjection(
        Guid logicalDocumentId,
        Guid documentVersionId,
        string title,
        string repositoryRelativePath,
        string contentMarkdown,
        IReadOnlyList<WorkerAgentSourceAnchor> sourceAnchors,
        IReadOnlyList<CurrentDocumentSection> sections)
    {
        LogicalDocumentId = logicalDocumentId;
        DocumentVersionId = documentVersionId;
        Title = title;
        RepositoryRelativePath = repositoryRelativePath;
        ContentMarkdown = contentMarkdown;
        SourceAnchors = sourceAnchors;
        Sections = sections;
    }

    public Guid LogicalDocumentId { get; }
    public Guid DocumentVersionId { get; }
    public string Title { get; }
    public string RepositoryRelativePath { get; }
    public string ContentMarkdown { get; }
    public IReadOnlyList<WorkerAgentSourceAnchor> SourceAnchors { get; }
    public IReadOnlyList<CurrentDocumentSection> Sections { get; }

    internal static WorkerAgentCodeLogicDocumentVersionProjection Create(
        CurrentKnowledge current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.LogicalDocumentId == Guid.Empty ||
            current.DocumentVersionId == Guid.Empty ||
            current.DocumentType != DocumentType.CodeLogic ||
            current.SourceAnchors is null ||
            current.Sections is null ||
            current.SourceAnchors.Count is < 1 or > KnowledgeDocumentLimits.MaximumAnchors ||
            current.Sections.Count > KnowledgeDocumentLimits.MaximumSections)
        {
            throw new ArgumentException(
                "The current Code Logic Document Version projection is invalid.",
                nameof(current));
        }

        string title = WorkerAgentContractText.RequireBoundedSafeText(
            current.Title,
            KnowledgeDocumentLimits.MaximumShortTextBytes,
            nameof(current));
        string path = WorkerAgentContractText.RequireRepositoryRelativePath(
            current.RepositoryRelativePath,
            nameof(current));
        string markdown = WorkerAgentContractText.RequireBoundedSafeLayoutText(
            current.ContentMarkdown,
            KnowledgeDocumentLimits.MaximumSectionMarkdownBytes,
            nameof(current));
        WorkerAgentSourceAnchor[] anchors = current.SourceAnchors
            .Select(anchor => WorkerAgentSourceAnchor.Create(
                anchor.Id,
                anchor.RepositoryRelativePath,
                anchor.EvidenceLocator,
                anchor.LineStart,
                anchor.LineEnd,
                anchor.SourceHash))
            .ToArray();
        if (!IsStrictlyAscending(anchors.Select(anchor => anchor.Id)) ||
            anchors.Any(anchor => !StringComparer.Ordinal.Equals(
                anchor.RepositoryRelativePath,
                path)))
        {
            throw new ArgumentException(
                "The current Code Logic Source Anchors are not canonical.",
                nameof(current));
        }

        CurrentDocumentSection[] sections = current.Sections.ToArray();
        for (int index = 0; index < sections.Length; index++)
        {
            CurrentDocumentSection section = sections[index];
            if (section.StableSectionId == Guid.Empty ||
                section.SectionOrder != index + 1 ||
                !Enum.IsDefined(section.SectionKind) ||
                section.HasRestrictedDocumentReference)
            {
                throw new ArgumentException(
                    "The current Code Logic sections are not canonical and authorized.",
                    nameof(current));
            }

            _ = WorkerAgentContractText.RequireBoundedSafeText(
                section.Heading,
                KnowledgeDocumentLimits.MaximumShortTextBytes,
                nameof(current));
            _ = WorkerAgentContractText.RequireBoundedSafeLayoutText(
                section.ContentMarkdown,
                KnowledgeDocumentLimits.MaximumSectionMarkdownBytes,
                nameof(current));
            _ = WorkerAgentContractText.RequireBoundedSafeText(
                section.SearchText,
                KnowledgeDocumentLimits.MaximumSectionSearchTextBytes,
                nameof(current));
        }

        return new(
            current.LogicalDocumentId,
            current.DocumentVersionId,
            title,
            path,
            markdown,
            Array.AsReadOnly(anchors),
            Array.AsReadOnly(sections));
    }

    private static bool IsStrictlyAscending(IEnumerable<Guid> identifiers)
    {
        Guid? previous = null;
        foreach (Guid identifier in identifiers)
        {
            if (identifier == Guid.Empty ||
                previous.HasValue && previous.Value.CompareTo(identifier) >= 0)
            {
                return false;
            }

            previous = identifier;
        }

        return true;
    }
}

/// <summary>
/// One fixed prepared flow, its ordered evidence, and the exact current Code
/// Logic Document Version projections used by Pass 2.
/// </summary>
public sealed class Pass2BusinessLogicJobPayload : WorkerAgentJobPayload
{
    private Pass2BusinessLogicJobPayload(
        string preparedFlowSchemaIdentity,
        Guid manifestId,
        Guid flowId,
        Guid systemId,
        Guid repositoryId,
        IReadOnlyList<PreparedPass2TraceEvidence> traceEvidence,
        IReadOnlyList<WorkerAgentCodeLogicDocumentVersionProjection> codeLogicDocumentVersions)
    {
        PreparedFlowSchemaIdentity = preparedFlowSchemaIdentity;
        ManifestId = manifestId;
        FlowId = flowId;
        SystemId = systemId;
        RepositoryId = repositoryId;
        TraceEvidence = traceEvidence;
        CodeLogicDocumentVersions = codeLogicDocumentVersions;
    }

    public override WorkerAgentJobKind Kind => WorkerAgentJobKind.Pass2BusinessLogicDocument;
    public string PreparedFlowSchemaIdentity { get; }
    public Guid ManifestId { get; }
    public Guid FlowId { get; }
    public Guid SystemId { get; }
    public Guid RepositoryId { get; }
    public IReadOnlyList<PreparedPass2TraceEvidence> TraceEvidence { get; }
    public IReadOnlyList<WorkerAgentCodeLogicDocumentVersionProjection> CodeLogicDocumentVersions { get; }

    public static Pass2BusinessLogicJobPayload Create(
        PreparedPass2Flow flow,
        IReadOnlyList<CurrentKnowledge> currentCodeLogicDocuments)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(currentCodeLogicDocuments);
        if (currentCodeLogicDocuments.Count != flow.Members.Count)
        {
            throw new ArgumentException(
                "Pass 2 requires the exact prepared Code Logic Document Version set.",
                nameof(currentCodeLogicDocuments));
        }

        var projections = new WorkerAgentCodeLogicDocumentVersionProjection[
            currentCodeLogicDocuments.Count];
        Guid? previousVersionId = null;
        for (int index = 0; index < currentCodeLogicDocuments.Count; index++)
        {
            CurrentKnowledge current = currentCodeLogicDocuments[index];
            PreparedPass2FlowMember member = flow.Members[index];
            if (current.RepositoryId != flow.RepositoryId ||
                current.LogicalDocumentId != member.LogicalDocumentId ||
                previousVersionId.HasValue &&
                    previousVersionId.Value.CompareTo(current.DocumentVersionId) >= 0)
            {
                throw new ArgumentException(
                    "Pass 2 Code Logic Document Versions must be exact and canonical.",
                    nameof(currentCodeLogicDocuments));
            }

            projections[index] = WorkerAgentCodeLogicDocumentVersionProjection.Create(current);
            previousVersionId = current.DocumentVersionId;
        }

        return new(
            flow.SchemaIdentity,
            flow.ManifestId,
            flow.FlowId,
            flow.SystemId,
            flow.RepositoryId,
            Array.AsReadOnly(flow.TraceEvidence.ToArray()),
            Array.AsReadOnly(projections));
    }

    public static Pass2BusinessLogicJobPayload Create(
        ResolvedPreparedPass2Flow resolvedFlow)
    {
        ArgumentNullException.ThrowIfNull(resolvedFlow);
        PreparedPass2RuntimeBinding binding = resolvedFlow.RuntimeBinding ??
            throw new PreparedPass2FlowContractException();
        if (!binding.IsCanonical() ||
            !binding.Flow.IsExactMatch(resolvedFlow.Flow) ||
            resolvedFlow.CodeLogicDocuments.Count != binding.Members.Count)
        {
            throw new PreparedPass2FlowContractException();
        }

        var projections = new WorkerAgentCodeLogicDocumentVersionProjection[
            resolvedFlow.CodeLogicDocuments.Count];
        for (int index = 0; index < projections.Length; index++)
        {
            CurrentKnowledge current = resolvedFlow.CodeLogicDocuments[index];
            PreparedPass2RuntimeMember member = binding.Members[index];
            PreparedPass2FlowMember flowMember = binding.Flow.Members[index];
            if (member.Order != index + 1 ||
                flowMember.Order != member.Order ||
                flowMember.JobId != member.JobId ||
                flowMember.CodeUnitId != member.CodeUnitId ||
                flowMember.LogicalDocumentId != member.LogicalDocumentId ||
                current.RepositoryId != member.RepositoryId ||
                current.LogicalDocumentId != member.LogicalDocumentId ||
                current.DocumentVersionId != member.DocumentVersionId ||
                !current.SourceAnchors.Select(anchor => anchor.Id)
                    .SequenceEqual(member.SourceAnchorIds))
            {
                throw new PreparedPass2FlowContractException();
            }

            projections[index] = WorkerAgentCodeLogicDocumentVersionProjection.Create(current);
        }

        return new(
            binding.Flow.SchemaIdentity,
            binding.Flow.ManifestId,
            binding.Flow.FlowId,
            binding.Flow.SystemId,
            binding.Flow.RepositoryId,
            Array.AsReadOnly(binding.Flow.TraceEvidence.ToArray()),
            Array.AsReadOnly(projections));
    }
}

public sealed record WorkerAgentStructuralContext
{
    private WorkerAgentStructuralContext(
        Guid codeUnitId,
        string repositoryRelativePath,
        string symbolName)
    {
        CodeUnitId = codeUnitId;
        RepositoryRelativePath = repositoryRelativePath;
        SymbolName = symbolName;
    }

    public Guid CodeUnitId { get; }
    public string RepositoryRelativePath { get; }
    public string SymbolName { get; }

    public static WorkerAgentStructuralContext Create(
        Guid codeUnitId,
        string repositoryRelativePath,
        string symbolName)
    {
        if (codeUnitId == Guid.Empty)
        {
            throw new ArgumentException(
                "The neighboring Code Unit identifier must be non-empty.",
                nameof(codeUnitId));
        }

        string path = WorkerAgentContractText.RequireRepositoryRelativePath(
            repositoryRelativePath,
            nameof(repositoryRelativePath));
        string symbol = WorkerAgentContractText.RequireBoundedSafeText(
            symbolName,
            KnowledgeDocumentLimits.MaximumShortTextBytes,
            nameof(symbolName));
        return new(codeUnitId, path, symbol);
    }
}

/// <summary>
/// One approved, bounded communication fact supplied to Pass 1.
/// </summary>
public sealed record WorkerAgentCommunicationEvidence
{
    private WorkerAgentCommunicationEvidence(Guid evidenceId, string kind, string description)
    {
        EvidenceId = evidenceId;
        Kind = kind;
        Description = description;
    }

    public Guid EvidenceId { get; }
    public string Kind { get; }
    public string Description { get; }

    public static WorkerAgentCommunicationEvidence Create(
        Guid evidenceId,
        string kind,
        string description)
    {
        if (evidenceId == Guid.Empty)
        {
            throw new ArgumentException(
                "The communication evidence identifier must be non-empty.",
                nameof(evidenceId));
        }

        string normalizedKind = WorkerAgentContractText.RequireBoundedSafeText(
            kind,
            KnowledgeDocumentLimits.MaximumShortTextBytes,
            nameof(kind));
        string normalizedDescription = WorkerAgentContractText.RequireBoundedSafeText(
            description,
            KnowledgeDocumentLimits.MaximumShortTextBytes,
            nameof(description));
        return new(evidenceId, normalizedKind, normalizedDescription);
    }
}
