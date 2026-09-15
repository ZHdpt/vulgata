namespace Vulgata.Infrastructure.Source;

internal enum TrustedSourceWorkspacePurpose
{
    SourceAnalysis,
}

internal enum TrustedSourceWorkspaceStatus
{
    Succeeded,
    Denied,
    Unsupported,
    Cancelled,
    IntegrityFailure,
    StorageFailure,
}

internal sealed record TrustedSourceWorkspaceResult(
    TrustedSourceWorkspaceStatus Status,
    string Code,
    Guid CorrelationId,
    ITrustedSourceWorkspaceLease? Workspace);

internal interface ITrustedSourceWorkspaceProvider
{
    Task<TrustedSourceWorkspaceResult> AcquireAsync(
        VerifiedRetainedSourceObject source,
        TrustedSourceWorkspacePurpose purpose,
        CancellationToken cancellationToken = default);
}

internal interface ITrustedSourceWorkspaceLease : IAsyncDisposable
{
    string RootPath { get; }
    Guid RepositoryId { get; }
    string ProfiledRevision { get; }
    string ManifestSha256 { get; }
}
