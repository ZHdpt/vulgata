namespace Vulgata.Core.DomainServices;

public enum AnalysisPortFailureReason
{
    None,
    NotReady,
    Rejected,
    TimedOut,
    Cancelled,
    MalformedResponse,
    IntegrityMismatch,
    WorkspaceConflict,
    ProcessFailed,
    CleanupFailed,
    Unavailable,
}

public enum AnalysisOperationStatus
{
    Succeeded,
    Failed,
}

public enum AnalysisSymbolKind
{
    Type,
    Method,
    Property,
    Field,
    Constructor,
}

public sealed class AnalysisDeadline
{
    public AnalysisDeadline(long unixTimeMilliseconds)
    {
        if (unixTimeMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unixTimeMilliseconds));
        }

        UnixTimeMilliseconds = unixTimeMilliseconds;
    }

    public long UnixTimeMilliseconds { get; }
}

public sealed class AnalysisScope
{
    public AnalysisScope(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string workingCopyBindingToken,
        string runtimeIdentityHash,
        AnalysisDeadline deadline)
    {
        if (repositoryId == Guid.Empty)
        {
            throw new ArgumentException("Repository identity is required.", nameof(repositoryId));
        }

        if (scanRunId == Guid.Empty)
        {
            throw new ArgumentException("Scan Run identity is required.", nameof(scanRunId));
        }

        RepositoryId = repositoryId;
        ScanRunId = scanRunId;
        ProfiledRevision = AnalysisContractGuard.RequireRevision(profiledRevision);
        WorkingCopyBindingToken = AnalysisContractGuard.RequireMachineCode(
            workingCopyBindingToken,
            128,
            nameof(workingCopyBindingToken));
        RuntimeIdentityHash = AnalysisContractGuard.RequireSha256(
            runtimeIdentityHash,
            nameof(runtimeIdentityHash));
        Deadline = deadline ?? throw new ArgumentNullException(nameof(deadline));
    }

    public Guid RepositoryId { get; }
    public Guid ScanRunId { get; }
    public string ProfiledRevision { get; }
    public string WorkingCopyBindingToken { get; }
    public string RuntimeIdentityHash { get; }
    public AnalysisDeadline Deadline { get; }
}

public sealed class AnalysisOperationBounds
{
    public AnalysisOperationBounds(int itemLimit, int byteLimit)
    {
        ItemLimit = AnalysisContractGuard.RequireLimit(itemLimit, 1_000, nameof(itemLimit));
        ByteLimit = AnalysisContractGuard.RequireLimit(byteLimit, 1_048_576, nameof(byteLimit));
    }

    public int ItemLimit { get; }
    public int ByteLimit { get; }
}

public sealed class AnalysisSymbolQuery
{
    public AnalysisSymbolQuery(
        string searchAtom,
        AnalysisSymbolKind kind,
        AnalysisOperationBounds bounds)
    {
        SearchAtom = AnalysisContractGuard.RequireText(searchAtom, 256, nameof(searchAtom));
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Kind = kind;
        Bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
    }

    public string SearchAtom { get; }
    public AnalysisSymbolKind Kind { get; }
    public AnalysisOperationBounds Bounds { get; }
}

public sealed class AnalysisSymbolRequest
{
    public AnalysisSymbolRequest(string exactSymbol, AnalysisOperationBounds bounds)
    {
        ExactSymbol = AnalysisContractGuard.RequireText(exactSymbol, 512, nameof(exactSymbol));
        Bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
    }

    public string ExactSymbol { get; }
    public AnalysisOperationBounds Bounds { get; }
}

public sealed class AnalysisSourcePosition
{
    public AnalysisSourcePosition(int line, int character)
    {
        if (line <= 0 || character <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Positions are one-based.");
        }

        Line = line;
        Character = character;
    }

    public int Line { get; }
    public int Character { get; }
}

public sealed class AnalysisLineRange
{
    public AnalysisLineRange(int startLine, int endLine)
    {
        if (startLine <= 0 || endLine < startLine)
        {
            throw new ArgumentOutOfRangeException(nameof(startLine));
        }

        StartLine = startLine;
        EndLine = endLine;
    }

    public int StartLine { get; }
    public int EndLine { get; }
}

public sealed class JavaNavigationRequest
{
    public JavaNavigationRequest(
        string relativePath,
        AnalysisSourcePosition? position,
        AnalysisOperationBounds bounds)
    {
        RelativePath = AnalysisContractGuard.RequireRelativePath(relativePath);
        Position = position;
        Bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
    }

    public string RelativePath { get; }
    public AnalysisSourcePosition? Position { get; }
    public AnalysisOperationBounds Bounds { get; }
}

public sealed class RevisionBoundSourceRequest
{
    public RevisionBoundSourceRequest(
        string relativePath,
        AnalysisLineRange range,
        string expectedSourceSha256,
        int byteLimit)
    {
        RelativePath = AnalysisContractGuard.RequireRelativePath(relativePath);
        Range = range ?? throw new ArgumentNullException(nameof(range));
        ExpectedSourceSha256 = AnalysisContractGuard.RequireSha256(
            expectedSourceSha256,
            nameof(expectedSourceSha256));
        ByteLimit = AnalysisContractGuard.RequireLimit(byteLimit, 1_048_576, nameof(byteLimit));
    }

    public string RelativePath { get; }
    public AnalysisLineRange Range { get; }
    public string ExpectedSourceSha256 { get; }
    public int ByteLimit { get; }
}

public sealed class AnalysisPortFailure
{
    public AnalysisPortFailure(
        AnalysisPortFailureReason reason,
        string failureCode,
        string diagnostic)
    {
        if (!Enum.IsDefined(reason) || reason == AnalysisPortFailureReason.None)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        Reason = reason;
        FailureCode = AnalysisContractGuard.RequireMachineCode(
            failureCode,
            64,
            nameof(failureCode));
        Diagnostic = AnalysisContractGuard.SanitizeDiagnostic(diagnostic);
    }

    public AnalysisPortFailureReason Reason { get; }
    public string FailureCode { get; }
    public string Diagnostic { get; }
}

public sealed record AnalysisSymbolEvidence(
    string Symbol,
    AnalysisSymbolKind Kind,
    string RelativePath,
    AnalysisLineRange Range);

public sealed record AnalysisRelationshipEvidence(
    string SourceSymbol,
    string TargetSymbol,
    string RelativePath,
    AnalysisLineRange Range);

public sealed class AnalysisIndexResult
{
    public AnalysisIndexResult(
        AnalysisOperationStatus status,
        string indexIdentity,
        long durationMilliseconds,
        int indexedFileCount,
        AnalysisPortFailure? failure)
    {
        Status = status;
        IndexIdentity = AnalysisContractGuard.RequireMachineCode(
            indexIdentity,
            128,
            nameof(indexIdentity));
        DurationMilliseconds = AnalysisContractGuard.RequireNonNegative(
            durationMilliseconds,
            nameof(durationMilliseconds));
        IndexedFileCount = AnalysisContractGuard.RequireNonNegative(
            indexedFileCount,
            nameof(indexedFileCount));
        Failure = failure;
    }

    public AnalysisOperationStatus Status { get; }
    public string IndexIdentity { get; }
    public long DurationMilliseconds { get; }
    public int IndexedFileCount { get; }
    public AnalysisPortFailure? Failure { get; }
}

public sealed class CodeGraphStatusResult
{
    public CodeGraphStatusResult(
        AnalysisOperationStatus status,
        bool ready,
        bool stale,
        int indexedFileCount,
        AnalysisPortFailure? failure)
    {
        Status = status;
        Ready = ready;
        Stale = stale;
        IndexedFileCount = AnalysisContractGuard.RequireNonNegative(
            indexedFileCount,
            nameof(indexedFileCount));
        Failure = failure;
    }

    public AnalysisOperationStatus Status { get; }
    public bool Ready { get; }
    public bool Stale { get; }
    public int IndexedFileCount { get; }
    public AnalysisPortFailure? Failure { get; }
}

public sealed class SymbolSearchResult
{
    public SymbolSearchResult(
        AnalysisOperationStatus status,
        IEnumerable<AnalysisSymbolEvidence> symbols,
        AnalysisPortFailure? failure)
    {
        Status = status;
        Symbols = AnalysisContractGuard.CopyBounded(symbols, 1_000, nameof(symbols));
        Failure = failure;
    }

    public AnalysisOperationStatus Status { get; }
    public IReadOnlyCollection<AnalysisSymbolEvidence> Symbols { get; }
    public AnalysisPortFailure? Failure { get; }
}

public sealed class RelationshipResult
{
    public RelationshipResult(
        AnalysisOperationStatus status,
        IEnumerable<AnalysisRelationshipEvidence> relationships,
        AnalysisPortFailure? failure)
    {
        Status = status;
        Relationships = AnalysisContractGuard.CopyBounded(
            relationships,
            1_000,
            nameof(relationships));
        Failure = failure;
    }

    public AnalysisOperationStatus Status { get; }
    public IReadOnlyCollection<AnalysisRelationshipEvidence> Relationships { get; }
    public AnalysisPortFailure? Failure { get; }
}

public sealed class RelatedFilesResult
{
    public RelatedFilesResult(
        AnalysisOperationStatus status,
        IEnumerable<string> relativePaths,
        AnalysisPortFailure? failure)
    {
        Status = status;
        RelativePaths = AnalysisContractGuard.CopyBounded(
            relativePaths.Select(AnalysisContractGuard.RequireRelativePath),
            1_000,
            nameof(relativePaths));
        Failure = failure;
    }

    public AnalysisOperationStatus Status { get; }
    public IReadOnlyCollection<string> RelativePaths { get; }
    public AnalysisPortFailure? Failure { get; }
}

public sealed class JavaNavigationResult
{
    public JavaNavigationResult(
        AnalysisOperationStatus status,
        IEnumerable<AnalysisSymbolEvidence> evidence,
        AnalysisPortFailure? failure)
    {
        Status = status;
        Evidence = AnalysisContractGuard.CopyBounded(evidence, 1_000, nameof(evidence));
        Failure = failure;
    }

    public AnalysisOperationStatus Status { get; }
    public IReadOnlyCollection<AnalysisSymbolEvidence> Evidence { get; }
    public AnalysisPortFailure? Failure { get; }
}

public sealed class SourceExcerptResult
{
    public SourceExcerptResult(
        AnalysisOperationStatus status,
        Guid repositoryId,
        string profiledRevision,
        string relativePath,
        AnalysisLineRange range,
        string sourceSha256,
        string excerpt,
        int byteCount,
        AnalysisPortFailure? failure)
    {
        Status = status;
        RepositoryId = repositoryId;
        ProfiledRevision = AnalysisContractGuard.RequireRevision(profiledRevision);
        RelativePath = AnalysisContractGuard.RequireRelativePath(relativePath);
        Range = range ?? throw new ArgumentNullException(nameof(range));
        SourceSha256 = AnalysisContractGuard.RequireSha256(sourceSha256, nameof(sourceSha256));
        Excerpt = AnalysisContractGuard.RequireExcerpt(excerpt, 1_048_576, nameof(excerpt));
        ByteCount = AnalysisContractGuard.RequireLimit(byteCount, 1_048_576, nameof(byteCount));
        Failure = failure;
    }

    public AnalysisOperationStatus Status { get; }
    public Guid RepositoryId { get; }
    public string ProfiledRevision { get; }
    public string RelativePath { get; }
    public AnalysisLineRange Range { get; }
    public string SourceSha256 { get; }
    public string Excerpt { get; }
    public int ByteCount { get; }
    public AnalysisPortFailure? Failure { get; }
}

public sealed record AnalysisHealthCheck(string Component, AnalysisRuntimeHealthStatus Status);

public sealed class AnalysisHealthProbeResult
{
    public AnalysisHealthProbeResult(
        AnalysisRuntimeHealthStatus status,
        string failureCode,
        string diagnostic,
        long observedAtUnixMilliseconds,
        IEnumerable<AnalysisHealthCheck> componentChecks,
        string? runtimeIdentityHash = null)
    {
        Status = status;
        FailureCode = AnalysisContractGuard.RequireMachineCode(
            failureCode,
            64,
            nameof(failureCode));
        Diagnostic = string.IsNullOrWhiteSpace(diagnostic)
            ? string.Empty
            : AnalysisContractGuard.SanitizeDiagnostic(diagnostic);
        ObservedAtUnixMilliseconds = AnalysisContractGuard.RequireNonNegative(
            observedAtUnixMilliseconds,
            nameof(observedAtUnixMilliseconds));
        ComponentChecks = AnalysisContractGuard.CopyBounded(
            componentChecks.OrderBy(check => check.Component, StringComparer.Ordinal),
            32,
            nameof(componentChecks));
        RuntimeIdentityHash = status == AnalysisRuntimeHealthStatus.Ready &&
            runtimeIdentityHash is not null
            ? AnalysisContractGuard.RequireSha256(
                runtimeIdentityHash,
                nameof(runtimeIdentityHash))
            : null;
    }

    public AnalysisRuntimeHealthStatus Status { get; }
    public string FailureCode { get; }
    public string Diagnostic { get; }
    public long ObservedAtUnixMilliseconds { get; }
    public IReadOnlyCollection<AnalysisHealthCheck> ComponentChecks { get; }
    public string? RuntimeIdentityHash { get; }
}

public interface ICodeGraphAnalysisPort
{
    Task<AnalysisIndexResult> IndexAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken);

    Task<AnalysisIndexResult> RebuildAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken);

    Task<CodeGraphStatusResult> GetStatusAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken);

    Task<SymbolSearchResult> SearchSymbolsAsync(
        AnalysisScope scope,
        AnalysisSymbolQuery query,
        CancellationToken cancellationToken);

    Task<RelationshipResult> GetCallersAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        CancellationToken cancellationToken);

    Task<RelationshipResult> GetCalleesAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        CancellationToken cancellationToken);

    Task<RelatedFilesResult> GetRelatedFilesAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        CancellationToken cancellationToken);
}

public interface IJavaAnalysisSessionFactory
{
    Task<IJavaAnalysisSession> OpenAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken);
}

public interface IJavaAnalysisSession : IAsyncDisposable
{
    Task<JavaNavigationResult> GetDocumentSymbolsAsync(
        AnalysisScope scope,
        JavaNavigationRequest request,
        CancellationToken cancellationToken);

    Task<JavaNavigationResult> GetDefinitionAsync(
        AnalysisScope scope,
        JavaNavigationRequest request,
        CancellationToken cancellationToken);

    Task<JavaNavigationResult> GetReferencesAsync(
        AnalysisScope scope,
        JavaNavigationRequest request,
        CancellationToken cancellationToken);

    Task<JavaNavigationResult> GetImplementationsAsync(
        AnalysisScope scope,
        JavaNavigationRequest request,
        CancellationToken cancellationToken);
}

public interface IRevisionBoundSourceReader
{
    Task<SourceExcerptResult> ReadAsync(
        AnalysisScope scope,
        RevisionBoundSourceRequest request,
        CancellationToken cancellationToken);
}

public interface IAnalysisRuntimeHealthProbe
{
    Task<AnalysisHealthProbeResult> CheckConfiguredIntegrityAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken);
}

internal static class AnalysisContractGuard
{
    public static string RequireRevision(string value)
    {
        string revision = RequireText(value, 64, nameof(value)).ToLowerInvariant();
        if (revision.Length < 7 || revision.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A bounded Profiled Revision is required.", nameof(value));
        }

        return revision;
    }

    public static string RequireSha256(string value, string parameterName)
    {
        string hash = RequireText(value, 64, parameterName).ToLowerInvariant();
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A SHA-256 value is required.", parameterName);
        }

        return hash;
    }

    public static string RequireMachineCode(string value, int maximumLength, string parameterName)
    {
        string code = RequireText(value, maximumLength, parameterName).ToLowerInvariant();
        if (code.Any(character =>
            !char.IsAsciiLetterOrDigit(character) &&
            character != '_' &&
            character != '-' &&
            character != '.'))
        {
            throw new ArgumentException("A normalized machine code is required.", parameterName);
        }

        return code;
    }

    public static string RequireRelativePath(string value)
    {
        string path = RequireText(value, 1_024, nameof(value)).Replace('\\', '/');
        if (path.StartsWith('/') ||
            path.Contains(':') ||
            path.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("A contained Repository-relative path is required.", nameof(value));
        }

        return path;
    }

    public static string RequireText(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The value is outside its bounded contract.", parameterName);
        }

        return normalized;
    }

    public static string RequireExcerpt(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        if (value.Length > maximumLength ||
            value.Any(character => char.IsControl(character) && character != '\n'))
        {
            throw new ArgumentException("The value is outside its bounded contract.", parameterName);
        }

        return value;
    }

    public static string SanitizeDiagnostic(string value)
    {
        string diagnostic = RequireText(value, 256, nameof(value));
        string[] safe = diagnostic
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token =>
                !token.Contains('/') &&
                !token.Contains('\\') &&
                !token.Contains("stderr", StringComparison.OrdinalIgnoreCase) &&
                !IsSensitiveDiagnosticToken(token))
            .ToArray();
        return safe.Length == 0 ? "analysis_failed" : string.Join(' ', safe);
    }

    private static bool IsSensitiveDiagnosticToken(string token)
    {
        int separatorIndex = token.IndexOf('=');
        if (separatorIndex <= 0)
        {
            return false;
        }

        ReadOnlySpan<char> key = token.AsSpan(0, separatorIndex);
        return key.Equals("token", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("secret", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("password", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("api_key", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("apikey", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("access_key", StringComparison.OrdinalIgnoreCase);
    }

    public static int RequireLimit(int value, int maximum, string parameterName)
    {
        if (value <= 0 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value;
    }

    public static int RequireNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value;
    }

    public static long RequireNonNegative(long value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value;
    }

    public static IReadOnlyCollection<T> CopyBounded<T>(
        IEnumerable<T> values,
        int maximumCount,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        T[] copy = values.Take(maximumCount + 1).ToArray();
        if (copy.Length > maximumCount || copy.Any(value => value is null))
        {
            throw new ArgumentException("The result exceeds its bounded contract.", parameterName);
        }

        return Array.AsReadOnly(copy);
    }
}
