namespace Vulgata.Core.DomainServices;

public interface IRetainedSourceApplication
{
    Task<SourceCaptureResult> AcquireAsync(
        Guid repositoryId,
        string permittedRef,
        CancellationToken cancellationToken = default);

    Task<OpenSourceReadContextResult> OpenReadContextAsync(
        Guid repositoryId,
        string profiledRevision,
        SourceReadGrantRequest requestedGrant,
        CancellationToken cancellationToken = default);
}

public interface ISourceReadContext : IAsyncDisposable
{
    Guid ContextId { get; }

    Guid RepositoryId { get; }

    string ProfiledRevision { get; }

    Task<SourceEntryResult> GetEntryAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<SourceListResult> ListAsync(
        string relativeDirectory,
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default);

    Task<SourceBytesResult> ReadBytesAsync(
        string relativePath,
        long byteOffset,
        int maximumBytes,
        CancellationToken cancellationToken = default);

    Task<SourceTextResult> ReadTextAsync(
        string relativePath,
        long byteOffset,
        int maximumBytes,
        CancellationToken cancellationToken = default);
}

public interface ISourceRepositoryAuthorizationVerifier
{
    Task<AuthorizedSourceRepository?> AuthorizeAsync(
        SourceAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}

public enum SourceAuthorizationPurpose
{
    Capture,
    Read,
}

public sealed record SourceAuthorizationSubject(string SubjectId);

public sealed record SourceAuthorizationRequest(
    SourceAuthorizationSubject Subject,
    SourceAuthorizationPurpose Purpose,
    Guid RepositoryId,
    string? ProfiledRevision);

public sealed record AuthorizedSourceRepository(
    Guid RepositoryId,
    Guid? SystemId,
    string GitOrigin);

public enum SourceCaptureStatus
{
    Completed,
    Replay,
    Denied,
    Unsupported,
    DeadlineExceeded,
    Cancelled,
    IntegrityFailure,
    AcquisitionFailure,
    StorageFailure,
}

public sealed record SourceCaptureResult(
    SourceCaptureStatus Status,
    string Code,
    Guid CorrelationId,
    Guid RepositoryId,
    string? ProfiledRevision,
    string? GitObjectFormat,
    string? ManifestSha256,
    long? EntryCount,
    long? TotalBlobBytes,
    DateTimeOffset? CompletedAt);

public sealed record SourceReadGrantRequest
{
    public SourceReadGrantRequest(
        int? maximumOperations = null,
        int? maximumMetadataEntries = null,
        long? maximumReturnedBytes = null,
        int? maximumEntriesPerOperation = null,
        int? maximumBytesPerOperation = null,
        TimeSpan? deadline = null)
    {
        MaximumOperations = maximumOperations;
        MaximumMetadataEntries = maximumMetadataEntries;
        MaximumReturnedBytes = maximumReturnedBytes;
        MaximumEntriesPerOperation = maximumEntriesPerOperation;
        MaximumBytesPerOperation = maximumBytesPerOperation;
        Deadline = deadline;
    }

    public int? MaximumOperations { get; }

    public int? MaximumMetadataEntries { get; }

    public long? MaximumReturnedBytes { get; }

    public int? MaximumEntriesPerOperation { get; }

    public int? MaximumBytesPerOperation { get; }

    public TimeSpan? Deadline { get; }
}

public enum SourceReadStatus
{
    Succeeded,
    Denied,
    NotFoundOrUnavailable,
    Unsupported,
    InvalidRange,
    BudgetExceeded,
    DeadlineExceeded,
    Cancelled,
    IntegrityFailure,
    AcquisitionFailure,
    StorageFailure,
}

public enum SourceBudgetDimension
{
    Operations,
    MetadataEntries,
    ReturnedBytes,
    EntriesPerOperation,
    BytesPerOperation,
}

public sealed record OpenSourceReadContextResult(
    SourceReadStatus Status,
    string Code,
    Guid CorrelationId,
    Guid RepositoryId,
    string ProfiledRevision,
    string? ManifestSha256,
    ISourceReadContext? Context);

public enum SourceEntryKind
{
    Directory,
    RegularFile,
    ExecutableFile,
    SymbolicLink,
    Gitlink,
}

public enum SourceEntryCapability
{
    Metadata,
    ByteRange,
    StrictUtf8Text,
}

public sealed record SourceEntry(
    string RelativePath,
    SourceEntryKind Kind,
    long? ByteLength,
    string? ContentSha256,
    IReadOnlyList<SourceEntryCapability> Capabilities,
    string? LimitationCode);

public sealed record SourceLimitationSummary(long Ordinal, string LimitationCode);

public sealed record SourceEntryResult(
    SourceReadStatus Status,
    string Code,
    Guid CorrelationId,
    Guid RepositoryId,
    string ProfiledRevision,
    string? ManifestSha256,
    SourceEntry? Entry);

public sealed record SourceListResult(
    SourceReadStatus Status,
    string Code,
    Guid CorrelationId,
    Guid RepositoryId,
    string ProfiledRevision,
    string? ManifestSha256,
    IReadOnlyList<SourceEntry> Entries,
    IReadOnlyList<SourceLimitationSummary> Limitations,
    string? NextCursor,
    SourceBudgetDimension? ExhaustedDimension);

public sealed record SourceBytesResult(
    SourceReadStatus Status,
    string Code,
    Guid CorrelationId,
    Guid RepositoryId,
    string ProfiledRevision,
    string? ManifestSha256,
    string? RelativePath,
    ReadOnlyMemory<byte> Content,
    long? ByteOffset,
    int? ByteLength,
    long? EntryByteLength,
    string? EntrySha256,
    string? LimitationCode,
    SourceBudgetDimension? ExhaustedDimension);

public sealed record SourceTextResult(
    SourceReadStatus Status,
    string Code,
    Guid CorrelationId,
    Guid RepositoryId,
    string ProfiledRevision,
    string? ManifestSha256,
    string? RelativePath,
    string? Content,
    long? ByteOffset,
    int? ByteLength,
    long? EntryByteLength,
    string? EntrySha256,
    string? LimitationCode,
    SourceBudgetDimension? ExhaustedDimension);
